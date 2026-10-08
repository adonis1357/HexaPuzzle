using System;

namespace Bow.Core
{
    /// <summary>
    /// 구간형 바람 모델. (seed, 경기시간)의 순수 함수이므로 네트워크 양쪽에서 동일하다.
    ///
    /// 경기 시간을 길이 windHoldSeconds 의 구간으로 나누고, 구간마다 seed 기반 목표 풍속(−max~+max)을 정한다.
    /// 구간이 바뀌는 시점부터 windTransitionSeconds 동안 이전 값 → 새 값으로 smoothstep 전환하고, 나머지 시간은 유지한다.
    /// HUD는 다음 구간의 바람(NextWind)과 변화까지 남은 시간(SecondsUntilChange)을 미리 보여준다.
    /// </summary>
    public sealed class WindModel
    {
        private readonly DuelConfig cfg;
        private readonly int seed;

        public float MaxAbsWind { get { return cfg.windMaxSpeed; } }

        public WindModel(int seed, DuelConfig config)
        {
            cfg = config;
            this.seed = seed;
        }

        /// <summary>k번째 구간의 목표 풍속 (결정론)</summary>
        public float SegmentTarget(int k)
        {
            if (k < 0) k = 0;
            DeterministicRandom rng = new DeterministicRandom(seed ^ (int)((uint)(k + 1) * 0x9E3779B1u) ^ 0x5A17B0);
            // 너무 약한 바람이 연속되지 않도록: 크기는 0.5~max 사이, 부호는 랜덤
            float mag = rng.Range(0.5f, cfg.windMaxSpeed);
            float sign = rng.NextFloat() < 0.5f ? -1f : 1f;
            return mag * sign;
        }

        private int SegmentIndex(float t) { return t <= 0f ? 0 : (int)(t / cfg.windHoldSeconds); }

        public float GetWind(float matchTime)
        {
            float t = matchTime < 0f ? 0f : matchTime;
            int k = SegmentIndex(t);
            float cur = SegmentTarget(k);
            if (k == 0) return cur; // 첫 구간은 전환 없이 시작
            float prev = SegmentTarget(k - 1);
            float tIn = t - k * cfg.windHoldSeconds;
            if (tIn >= cfg.windTransitionSeconds) return cur;
            float u = tIn / cfg.windTransitionSeconds;
            float s = u * u * (3f - 2f * u);
            return prev + (cur - prev) * s;
        }

        /// <summary>다음 구간에 불 바람</summary>
        public float GetNextWind(float matchTime)
        {
            float t = matchTime < 0f ? 0f : matchTime;
            return SegmentTarget(SegmentIndex(t) + 1);
        }

        /// <summary>다음 바람 변화(구간 경계)까지 남은 초</summary>
        public float SecondsUntilChange(float matchTime)
        {
            float t = matchTime < 0f ? 0f : matchTime;
            int k = SegmentIndex(t);
            return (k + 1) * cfg.windHoldSeconds - t;
        }

        /// <summary>현재 전환(바람 변화) 진행 중인지</summary>
        public bool IsTransitioning(float matchTime)
        {
            float t = matchTime < 0f ? 0f : matchTime;
            int k = SegmentIndex(t);
            if (k == 0) return false;
            return (t - k * cfg.windHoldSeconds) < cfg.windTransitionSeconds;
        }

        /// <summary>화살에 가해지는 수평 가속도 (m/s²)</summary>
        public float GetWindAccel(float matchTime)
        {
            return cfg.windAccelFactor * GetWind(matchTime);
        }
    }
}

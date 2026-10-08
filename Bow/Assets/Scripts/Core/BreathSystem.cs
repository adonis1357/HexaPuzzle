using System;

namespace Bow.Core
{
    /// <summary>
    /// 장전 딜레이 & 호흡(Breath) 시스템 — 게임의 핵심 리듬.
    ///
    /// 발사 후 D초 동안 장전(사격 불가). 장전 완료 후 추가로 기다린 시간 h(초과 대기)에 따라
    /// 다음 딜레이가 줄어든다. 효율 E(h) = (h/P)·e^(1−h/P)는 P=10초에서 정점(1.0)을 찍고 그 뒤 감소한다.
    ///   D_next = B × (1 − maxReduction × E(h))
    /// </summary>
    public sealed class BreathSystem
    {
        private readonly DuelConfig cfg;

        /// <summary>현재 적용 중인 장전 딜레이 (초)</summary>
        public float CurrentDelay { get; private set; }
        /// <summary>마지막 발사 시각 (경기 시간). 발사 전이면 음수.</summary>
        public float LastFireTime { get; private set; }
        /// <summary>장전 완료 시각</summary>
        public float ReadyTime { get; private set; }
        /// <summary>마지막 발사에 적용된 호흡 효율 (HUD 표시용)</summary>
        public float LastEfficiency { get; private set; }
        /// <summary>마지막 발사 시의 초과 대기 시간</summary>
        public float LastOverhold { get; private set; }
        public int ShotCount { get; private set; }

        public BreathSystem(DuelConfig config, float matchStartTime)
        {
            cfg = config;
            // 경기 시작 직후 초기 장전 3초 (바람을 읽을 시간)
            CurrentDelay = 3.0f;
            LastFireTime = matchStartTime - 1e-3f;
            ReadyTime = matchStartTime + CurrentDelay;
            LastEfficiency = 0f;
            LastOverhold = 0f;
            ShotCount = 0;
        }

        /// <summary>호흡 효율 E(h) ∈ [0,1]</summary>
        public static float Efficiency(float overhold, float peak)
        {
            if (overhold <= 0f || peak <= 0f) return 0f;
            float r = overhold / peak;
            return r * MathF.Exp(1f - r);
        }

        public float Efficiency(float overhold) { return Efficiency(overhold, cfg.breathPeak); }

        public bool IsReady(float t) { return t >= ReadyTime; }

        /// <summary>장전까지 남은 시간 (0 이상)</summary>
        public float CooldownRemaining(float t)
        {
            float r = ReadyTime - t;
            return r > 0f ? r : 0f;
        }

        /// <summary>장전 진행률 0→1</summary>
        public float CooldownProgress(float t)
        {
            if (CurrentDelay <= 0f) return 1f;
            float p = (t - LastFireTime) / CurrentDelay;
            if (p < 0f) p = 0f;
            if (p > 1f) p = 1f;
            return p;
        }

        /// <summary>현재 시점의 초과 대기 h (장전 완료 후 경과 시간). 장전 전이면 0.</summary>
        public float Overhold(float t)
        {
            float h = t - ReadyTime;
            return h > 0f ? h : 0f;
        }

        /// <summary>지금 발사하면 적용될 효율</summary>
        public float CurrentEfficiency(float t) { return Efficiency(Overhold(t)); }

        /// <summary>지금 발사했을 때의 다음 딜레이 (HUD 미리보기용)</summary>
        public float PreviewNextDelay(float t)
        {
            return ComputeNextDelay(Overhold(t));
        }

        public float ComputeNextDelay(float overhold)
        {
            float e = Efficiency(overhold);
            return cfg.baseDelay * (1f - cfg.maxDelayReduction * e);
        }

        /// <summary>현재 호흡 효율에 따른 추 미터 주기 (높을수록 느려짐 = 쉬움)</summary>
        public float MeterPeriod(float t)
        {
            return cfg.meterBasePeriod * (1f + cfg.breathMeterSlowdown * CurrentEfficiency(t));
        }

        /// <summary>
        /// 발사 처리. 초과 대기 h로 다음 딜레이를 계산해 적용한다.
        /// 장전 전 호출 시 false 반환 (발사 불가).
        /// </summary>
        public bool TryFire(float t)
        {
            if (!IsReady(t)) return false;
            float h = Overhold(t);
            LastOverhold = h;
            LastEfficiency = Efficiency(h);
            CurrentDelay = ComputeNextDelay(h);
            LastFireTime = t;
            ReadyTime = t + CurrentDelay;
            ShotCount++;
            return true;
        }
    }
}

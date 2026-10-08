using System;

namespace Bow.Core
{
    /// <summary>
    /// 정확도 추(錘) 미터. 조준 확정 후 추가 좌↔우로 왕복하고, 탭 시점의 위치 p ∈ [-1, 1]이 오차가 된다.
    /// 중앙(|p| &lt; perfectThreshold)에서 멈추면 Perfect.
    /// </summary>
    public sealed class AccuracyMeter
    {
        private readonly DuelConfig cfg;
        private float startTime;
        private float period;

        public bool IsRunning { get; private set; }

        public AccuracyMeter(DuelConfig config) { cfg = config; }

        public void Start(float t, float periodSeconds)
        {
            startTime = t;
            period = periodSeconds > 0.05f ? periodSeconds : 0.05f;
            IsRunning = true;
        }

        public void Stop() { IsRunning = false; }

        /// <summary>추 위치 p(t) = −cos(2π·(t − t0)/period). 왼쪽 끝(−1)에서 시작 ↔ 오른쪽(+1)</summary>
        public float Position(float t)
        {
            if (!IsRunning) return 0f;
            return -MathF.Cos(2f * MathF.PI * (t - startTime) / period);
        }

        public float Elapsed(float t) { return IsRunning ? (t - startTime) : 0f; }

        public bool IsTimedOut(float t) { return IsRunning && (t - startTime) >= cfg.meterTimeout; }

        public bool IsPerfect(float p) { return MathF.Abs(p) < cfg.perfectThreshold; }

        /// <summary>추 오프셋 → 각도 오차(°)</summary>
        public float AngleError(float p) { return p * cfg.meterMaxAngleError; }
    }
}

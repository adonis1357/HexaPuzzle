using System;

namespace Bow.Core
{
    /// <summary>
    /// 바람 모델. w(t)는 (seed, 경기시간)의 순수 함수이므로 네트워크 양쪽에서 동일하다.
    /// w(t) = A1·sin(2πt/T1 + φ1) + A2·sin(2πt/T2 + φ2) + A3·sin(2πt/T3 + φ3)  [m/s, +는 오른쪽]
    /// </summary>
    public sealed class WindModel
    {
        private readonly DuelConfig cfg;
        private readonly float phase1, phase2, phase3;

        public float MaxAbsWind { get { return cfg.windAmp1 + cfg.windAmp2 + cfg.windAmp3; } }

        public WindModel(int seed, DuelConfig config)
        {
            cfg = config;
            DeterministicRandom rng = new DeterministicRandom(seed ^ 0x5A17B0);
            phase1 = rng.Range(0f, MathF.PI * 2f);
            phase2 = rng.Range(0f, MathF.PI * 2f);
            phase3 = rng.Range(0f, MathF.PI * 2f);
        }

        public float GetWind(float matchTime)
        {
            float t = matchTime;
            float w = cfg.windAmp1 * MathF.Sin(2f * MathF.PI * t / cfg.windPeriod1 + phase1)
                    + cfg.windAmp2 * MathF.Sin(2f * MathF.PI * t / cfg.windPeriod2 + phase2)
                    + cfg.windAmp3 * MathF.Sin(2f * MathF.PI * t / cfg.windPeriod3 + phase3);
            return w;
        }

        /// <summary>화살에 가해지는 수평 가속도 (m/s²)</summary>
        public float GetWindAccel(float matchTime)
        {
            return cfg.windAccelFactor * GetWind(matchTime);
        }
    }
}

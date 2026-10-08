using System;

namespace Bow.Core
{
    /// <summary>봇 난이도 프로필 (GDD 7장)</summary>
    [Serializable]
    public class BotProfile
    {
        public string name = "보통";
        public float angleSigma = 3.0f;       // 각도 오차 σ(°)
        public float powerSigma = 0.05f;      // 힘 오차 σ
        public float windAwareness = 0.7f;    // 현재 바람 반영률
        public float deviationSigma = 0.35f;  // 추 미터 오차 σ (p)
        public float minOverhold = 6f;        // 호흡 대기 h 범위
        public float maxOverhold = 13f;
        public float headshotChance = 0.2f;   // 머리를 노릴 확률

        public static BotProfile Easy()
        {
            return new BotProfile { name = "쉬움", angleSigma = 5f, powerSigma = 0.08f, windAwareness = 0.4f, deviationSigma = 0.5f, minOverhold = 8f, maxOverhold = 16f, headshotChance = 0.1f };
        }
        public static BotProfile Normal()
        {
            return new BotProfile { name = "보통", angleSigma = 3f, powerSigma = 0.05f, windAwareness = 0.7f, deviationSigma = 0.35f, minOverhold = 6f, maxOverhold = 13f, headshotChance = 0.2f };
        }
        public static BotProfile Hard()
        {
            return new BotProfile { name = "어려움", angleSigma = 1.5f, powerSigma = 0.03f, windAwareness = 0.9f, deviationSigma = 0.2f, minOverhold = 9f, maxOverhold = 11f, headshotChance = 0.35f };
        }
    }

    /// <summary>
    /// 봇 사격 계획: 상수 바람 가정의 해석 탄도로 (각도, 힘) 격자 탐색 → 최소 오차 조합 + 가우시안 노이즈.
    /// </summary>
    public static class BotBrain
    {
        public static ShotParams Plan(int shooterId, BotProfile profile, MatchSetup setup, float windNow,
                                      DeterministicRandom rng, DuelConfig cfg, float launchTime, float breathEff)
        {
            int shooter = shooterId == 0 ? 0 : 1;
            int target = 1 - shooter;
            int facing = MatchSetup.Facing(shooter);
            Vec2 sFeet = setup.Feet(shooter);
            Vec2 tFeet = setup.Feet(target);

            // 로컬 프레임(전방 +x)에서의 목표 상대 위치
            bool aimHead = rng.NextFloat() < profile.headshotChance;
            float targetY = tFeet.y + (aimHead ? cfg.headCenterY : (cfg.bodyBottomY + cfg.bodyTopY) * 0.5f);
            float dx = (tFeet.x - sFeet.x) * facing - cfg.bowAnchorX;
            float dy = targetY - (sFeet.y + cfg.bowAnchorY);
            float aLocal = cfg.windAccelFactor * windNow * profile.windAwareness * facing;

            float bestErr = float.MaxValue;
            float bestAngle = 45f, bestPower = 0.6f;
            for (float ang = 15f; ang <= 75f; ang += 1.5f)
            {
                float rad = ang * MathF.PI / 180f;
                for (float pow = 0.15f; pow <= 1.0001f; pow += 0.025f)
                {
                    float v = cfg.ArrowSpeed(pow);
                    float vy = v * MathF.Sin(rad);
                    float vx = v * MathF.Cos(rad);
                    // 0.5 g t² − vy t + dy = 0 → 하강 중 교차 시각
                    float disc = vy * vy - 2f * cfg.gravity * dy;
                    if (disc < 0f) continue;
                    float t = (vy + MathF.Sqrt(disc)) / cfg.gravity;
                    if (t <= 0f) continue;
                    float x = vx * t + 0.5f * aLocal * t * t;
                    float err = MathF.Abs(x - dx);
                    // 낮은 각도(빠른 화살)를 약간 선호: 바람 노출 시간이 짧다
                    err += t * 0.05f;
                    if (err < bestErr) { bestErr = err; bestAngle = ang; bestPower = pow; }
                }
            }

            float angle = cfg.ClampAngle(bestAngle + rng.Gaussian(profile.angleSigma));
            float power = bestPower + rng.Gaussian(profile.powerSigma);
            if (power < 0f) power = 0f; if (power > 1f) power = 1f;
            float dev = rng.Gaussian(profile.deviationSigma);
            if (dev < -1f) dev = -1f; if (dev > 1f) dev = 1f;
            return new ShotParams(shooter, angle, power, dev, launchTime, breathEff);
        }

        /// <summary>다음 사격까지 기다릴 초과 대기 h (호흡 시스템 활용)</summary>
        public static float PickOverhold(BotProfile profile, DeterministicRandom rng)
        {
            return rng.Range(profile.minOverhold, profile.maxOverhold);
        }
    }
}

using UnityEngine;

namespace Bow.Art
{
    /// <summary>이징 함수 모음 (아트 사양서 §7)</summary>
    public static class Ease
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        public static float OutQuart(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u * u; }
        public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
        public static float InOutSine(float t) { t = Mathf.Clamp01(t); return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f; }
        public static float SmoothStep(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        public static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float OutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }

        /// <summary>지수 추종 계수: 1 − e^(−k·dt)</summary>
        public static float Follow(float k, float dt) { return 1f - Mathf.Exp(-k * dt); }
    }

    /// <summary>공통 타이밍 상수 (아트 사양서 §7)</summary>
    public static class ArtConstants
    {
        public const float DrawFollowRate = 20f;
        public const float RecoilDuration = 0.30f;
        public const float BowKickDuration = 0.18f;
        public const float StringSnapFreq = 18f;
        public const float StringSnapDecay = 0.06f;
        public const float HitFlashBody = 0.14f;
        public const float HitFlashHead = 0.22f;
        public const float HitShakeBody = 0.28f;
        public const float HitShakeHead = 0.40f;
        public const float DeathFallDuration = 0.90f;
        public const float DeathBounceDuration = 0.30f;
        public const float DeathFadeDuration = 1.0f;
        public const float HitStopHead = 0.08f;
        public const float HitStopBody = 0.06f;
        public const float SlowMoScale = 0.35f;
        public const float SlowMoDuration = 0.25f;
        public const float ZoomPunchIn = 0.10f;
        public const float ZoomPunchOut = 0.20f;
        public const float ArrowStickDuration = 3.0f;
        public const float TrailLifetime = 0.40f;
        public const float PerfectFlashDuration = 0.18f;
        public const float RingPulseDuration = 0.15f;
        public const float HpGhostDelay = 0.40f;
        public const float HpGhostShrink = 0.50f;
        public const float ResultOverlayFade = 0.50f;
        public const float DamageNumberRise = 0.80f;
        public const float PendulumFreezeOnTap = 0.25f;

        // 소팅 오더 (§2.2)
        public const int SortSky = -100;
        public const int SortMountainFar = -90;
        public const int SortMountainMid = -80;
        public const int SortMountainNear = -70;
        public const int SortWindBack = -20;
        public const int SortGround = -10;
        public const int SortArcher = 0;
        public const int SortBow = 2;
        public const int SortTrail = 9;
        public const int SortArrow = 10;
        public const int SortVfx = 20;
        public const int SortWindFront = 30;
    }
}

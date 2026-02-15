using UnityEngine;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// Unified visual constants for all block systems.
    /// Provides consistent easing functions, effect parameters, and color helpers.
    /// </summary>
    public static class VisualConstants
    {
        // ============================================================
        // Easing Functions
        // ============================================================

        public static float EaseOutCubic(float t)
        {
            t = 1f - t;
            return 1f - t * t * t;
        }

        public static float EaseOutQuart(float t)
        {
            t = 1f - t;
            return 1f - t * t * t * t;
        }

        public static float EaseInQuad(float t)
        {
            return t * t;
        }

        public static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float p = 0.3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ============================================================
        // Debris Parameters (2 tiers: Base / Large)
        // ============================================================

        // Base tier (per-block destruction)
        public const float DebrisBaseSizeMin = 4f;
        public const float DebrisBaseSizeMax = 11f;
        public const float DebrisBaseSpeedMin = 160f;
        public const float DebrisBaseSpeedMax = 420f;
        public const float DebrisGravity = -850f;
        public const float DebrisRotSpeedMin = -720f;
        public const float DebrisRotSpeedMax = 720f;
        public const float DebrisLifetimeMin = 0.25f;
        public const float DebrisLifetimeMax = 0.5f;
        public const int DebrisBaseCount = 6;

        // Large tier (center explosions like Bomb)
        public const float DebrisLargeSizeMin = 5f;
        public const float DebrisLargeSizeMax = 14f;
        public const float DebrisLargeSpeedMin = 200f;
        public const float DebrisLargeSpeedMax = 500f;
        public const float DebrisLargeLifetimeMin = 0.3f;
        public const float DebrisLargeLifetimeMax = 0.55f;
        public const int DebrisLargeCount = 10;

        // ============================================================
        // Spark Parameters (3 tiers: Small / Medium / Large)
        // ============================================================

        public const float SparkDeceleration = 0.93f;

        // Small (per-block)
        public const int SparkSmallCount = 6;
        public const float SparkSmallSizeMin = 3f;
        public const float SparkSmallSizeMax = 7f;
        public const float SparkSmallSpeedMin = 100f;
        public const float SparkSmallSpeedMax = 300f;
        public const float SparkSmallLifetimeMin = 0.1f;
        public const float SparkSmallLifetimeMax = 0.25f;

        // Medium (launch effects)
        public const int SparkMediumCount = 12;
        public const float SparkMediumSizeMin = 3f;
        public const float SparkMediumSizeMax = 8f;
        public const float SparkMediumSpeedMin = 120f;
        public const float SparkMediumSpeedMax = 350f;
        public const float SparkMediumLifetimeMin = 0.12f;
        public const float SparkMediumLifetimeMax = 0.3f;

        // Large (center explosions)
        public const int SparkLargeCount = 18;
        public const float SparkLargeSizeMin = 3f;
        public const float SparkLargeSizeMax = 9f;
        public const float SparkLargeSpeedMin = 150f;
        public const float SparkLargeSpeedMax = 450f;
        public const float SparkLargeLifetimeMin = 0.15f;
        public const float SparkLargeLifetimeMax = 0.35f;

        // ============================================================
        // Impact Wave (2 tiers: Small / Large)
        // ============================================================

        // Small (per-block)
        public const float WaveSmallInitialSize = 10f;
        public const float WaveSmallDuration = 0.18f;
        public const float WaveSmallExpand = 4.5f;
        public const float WaveSmallAlpha = 0.55f;

        // Large (center)
        public const float WaveLargeInitialSize = 10f;
        public const float WaveLargeDuration = 0.35f;
        public const float WaveLargeExpand = 10f;
        public const float WaveLargeAlpha = 0.6f;

        // ============================================================
        // Block Destruction
        // ============================================================

        public const float DestroyDuration = 0.14f;
        public const float DestroySqueezePeak = 0.25f; // scaleX overshoot

        // ============================================================
        // Screen Shake (3 tiers: Small / Medium / Large)
        // ============================================================

        // Small (per-block hit)
        public const float ShakeSmallIntensity = 4f;
        public const float ShakeSmallDuration = 0.06f;

        // Medium (launch / Donut / XBlock)
        public const float ShakeMediumIntensity = 8f;
        public const float ShakeMediumDuration = 0.12f;

        // Large (Bomb / Laser)
        public const float ShakeLargeIntensity = 12f;
        public const float ShakeLargeDuration = 0.2f;

        // ============================================================
        // Center Flash
        // ============================================================

        public const float FlashInitialSize = 30f;
        public const float FlashDuration = 0.35f;
        public const float FlashExpand = 7f;

        // System-specific flash colors (pastel)
        public static readonly Color FlashColorDrill = new Color(1f, 0.96f, 0.92f);
        public static readonly Color FlashColorBomb = new Color(1f, 0.94f, 0.90f);
        public static readonly Color FlashColorLaser = new Color(0.92f, 0.94f, 1f);

        // === Background / Border color constants (hardcoding 제거용) ===
        public static readonly Color SlotBackgroundColor = new Color(0.96f, 0.93f, 0.90f, 0.28f);
        public static readonly Color SlotBorderColor = new Color(0.92f, 0.88f, 0.85f, 0.30f);
        public static readonly Color SlotEmptyColor = new Color(0.96f, 0.93f, 0.90f, 0.04f);
        public static readonly Color WarningBlinkColor = new Color(0.95f, 0.72f, 0.68f, 0.8f);
        public static readonly Color WarningDimColor = new Color(0.85f, 0.60f, 0.55f, 0.5f);

        // ============================================================
        // Color Helpers
        // ============================================================

        /// <summary>
        /// Standard brighten: adds +0.08 to each channel (pastel-safe)
        /// </summary>
        public static Color Brighten(Color c)
        {
            return new Color(
                Mathf.Min(1f, c.r + 0.08f),
                Mathf.Min(1f, c.g + 0.08f),
                Mathf.Min(1f, c.b + 0.08f),
                c.a
            );
        }

        /// <summary>
        /// Standard darken: multiply by 0.82 (pastel - very gentle darkening)
        /// </summary>
        public static Color Darken(Color c)
        {
            return new Color(c.r * 0.82f, c.g * 0.82f, c.b * 0.82f, c.a);
        }

        /// <summary>
        /// Bomb-specific warm brighten (gentle warm shift)
        /// </summary>
        public static Color BombBrighten(Color c)
        {
            return new Color(
                Mathf.Min(1f, c.r + 0.2f),
                Mathf.Min(1f, c.g + 0.15f),
                Mathf.Min(1f, c.b + 0.05f),
                c.a
            );
        }

        /// <summary>
        /// Laser-specific cool brighten (gentle cool shift)
        /// </summary>
        public static Color LaserBrighten(Color c)
        {
            return new Color(
                Mathf.Min(1f, c.r + 0.15f),
                Mathf.Min(1f, c.g + 0.15f),
                Mathf.Min(1f, c.b + 0.2f),
                c.a
            );
        }

        /// <summary>
        /// Drill-specific brighten (gentle warm tint)
        /// </summary>
        public static Color DrillBrighten(Color c)
        {
            return new Color(
                Mathf.Min(1f, c.r + 0.18f),
                Mathf.Min(1f, c.g + 0.18f),
                Mathf.Min(1f, c.b + 0.1f),
                c.a
            );
        }

        // ============================================================
        // Cascade Multiplier
        // ============================================================

        /// <summary>
        /// Returns cascade intensity multiplier based on depth.
        /// depth 0=1.0x, 1=1.15x, 2=1.3x, 3=1.45x, 4+=1.8x (capped)
        /// </summary>
        public static float GetCascadeMultiplier(int depth)
        {
            return Mathf.Min(1f + depth * 0.15f, 1.8f);
        }

        // ============================================================
        // Spawn Animation
        // ============================================================

        public const float SpawnDuration = 0.25f;
        public const float SpawnOvershoot = 1.15f;
        public const float SpawnStartScale = 0.5f;
        public const float SpawnGlowAlpha = 0.4f;

        // ============================================================
        // Pre-Fire Compression (모든 특수 블록 공통)
        // ============================================================

        public const float PreFireDuration = 0.12f;
        public const float PreFireScaleMin = 0.78f;   // 압축 최소 (더 강한 압축)
        public const float PreFireScaleMax = 1.18f;    // 팽창 최대 (더 강한 팽창)
        public const float PreFireBrightenAmount = 0.35f;

        // Bomb 전용 (레거시 호환)
        public const float BombCompressionDuration = 0.05f;
        public const float BombCompressionScale = 1.15f;

        // ============================================================
        // Hit Stop (특수 블록 발동 시 타임스케일 조작)
        // ============================================================

        public const float HitStopDurationLarge = 0.06f;   // Bomb, Laser
        public const float HitStopDurationMedium = 0.045f;  // Donut, XBlock
        public const float HitStopDurationSmall = 0.03f;   // Drill
        public const float HitStopSlowMoDuration = 0.08f;
        public const float HitStopSlowMoScale = 0.3f;
        public const float HitStopCooldown = 0.3f;

        // 히트 스톱 쿨다운 상태 추적
        private static float lastHitStopTime = -1f;
        public static bool CanHitStop()
        {
            return Time.unscaledTime - lastHitStopTime > HitStopCooldown;
        }
        public static void RecordHitStop()
        {
            lastHitStopTime = Time.unscaledTime;
        }

        // ============================================================
        // Destroy Flash (파괴 순간 백색 오버레이)
        // ============================================================

        public const float DestroyFlashDuration = 0.08f;
        public const float DestroyFlashAlpha = 0.85f;
        public const float DestroyFlashSizeMultiplier = 1.5f;

        // ============================================================
        // Match Highlight Pulse (매칭 블록 펄스)
        // ============================================================

        public const float MatchPulseScale = 1.15f;
        public const float MatchPulseDuration = 0.15f;
        public const float MatchPulseOverlayAlpha = 0.5f;
        public const float MatchPulseStagger = 0.02f;

        // ============================================================
        // Dual Easing Destroy (확대 후 찌그러짐)
        // ============================================================

        public const float DestroyExpandPhaseRatio = 0.25f;  // 전체의 25%를 확대에 사용
        public const float DestroyExpandScale = 1.2f;       // 확대 최대 배율

        // ============================================================
        // Zoom Punch (특수 블록 발동 시 보드 줌)
        // ============================================================

        public const float ZoomPunchScaleLarge = 1.04f;    // Bomb, Laser
        public const float ZoomPunchScaleSmall = 1.025f;    // Donut, XBlock, Drill
        public const float ZoomPunchInDuration = 0.04f;
        public const float ZoomPunchOutDuration = 0.12f;

        // ============================================================
        // Bloom Simulation (센터 플래시 뒤 블룸 레이어)
        // ============================================================

        public const float BloomSizeMultiplier = 2.2f;
        public const float BloomAlphaMultiplier = 0.35f;
        public const float BloomLag = 0.02f;

        // ============================================================
        // Score Popup
        // ============================================================

        public const float PopupSmallSize = 28f;
        public const float PopupMediumSize = 34f;
        public const float PopupLargeSize = 42f;
        public const float PopupEpicSize = 50f;
        public const float PopupBaseDuration = 0.8f;
        public const float PopupTravelDistance = 100f;

        // ============================================================
        // Combo Display
        // ============================================================

        public const float ComboScaleIn = 1.3f;
        public const float ComboAnimDuration = 0.3f;
        public const float ComboIdleTimeout = 1.5f;

        // ============================================================
        // Score Counter Animation
        // ============================================================

        public const float ScoreCountDuration = 0.5f;

        // ============================================================
        // Move Counter
        // ============================================================

        public const float MoveBounceScale = 1.15f;
        public const float MoveBounceDuration = 0.2f;
        public const float MovePulseSpeed = 0.5f;
    }
}

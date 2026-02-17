using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// Crystal Hex UI 통합 디자인 상수 및 헬퍼
    /// </summary>
    public static class UITheme
    {
        // ── 배경 ──
        public static readonly Color DarkOverlay = new Color(0.06f, 0.04f, 0.12f, 0.92f);
        public static readonly Color PanelBg     = new Color(0.10f, 0.08f, 0.18f, 0.95f);

        // ── 버튼 Base ──
        public static readonly Color BtnNeutral  = new Color(0.14f, 0.12f, 0.22f, 0.90f);
        public static readonly Color BtnPrimary  = new Color(0.20f, 0.45f, 0.85f, 1.0f);
        public static readonly Color BtnSuccess  = new Color(0.18f, 0.60f, 0.35f, 1.0f);
        public static readonly Color BtnDanger   = new Color(0.75f, 0.18f, 0.22f, 1.0f);
        public static readonly Color BtnGold     = new Color(0.80f, 0.65f, 0.15f, 1.0f);

        // ── 텍스트 ──
        public static readonly Color TextPrimary   = Color.white;
        public static readonly Color TextSecondary  = new Color(0.70f, 0.72f, 0.80f, 0.85f);
        public static readonly Color TextGold       = new Color(1.0f, 0.84f, 0.0f, 1.0f);
        public static readonly Color TextDanger     = new Color(1.0f, 0.30f, 0.30f, 1.0f);
        public static readonly Color TextMuted      = new Color(0.50f, 0.50f, 0.55f, 0.7f);

        // ── HUD ──
        public static readonly Color BadgeBg       = new Color(0.10f, 0.08f, 0.16f, 0.75f);
        public static readonly Color BadgeBorder   = new Color(0.30f, 0.35f, 0.55f, 0.40f);
        public static readonly Color LabelColor    = new Color(0.70f, 0.72f, 0.80f, 0.80f);

        // ── 공통 ──
        public static readonly Color BorderGlow    = new Color(0.45f, 0.55f, 0.85f, 0.35f);
        public static readonly Color OutlineColor  = new Color(0f, 0f, 0f, 0.60f);

        /// <summary>
        /// 색상을 밝게 (+0.12)
        /// </summary>
        public static Color Highlight(Color c)
        {
            return new Color(
                Mathf.Clamp01(c.r + 0.12f),
                Mathf.Clamp01(c.g + 0.12f),
                Mathf.Clamp01(c.b + 0.12f),
                c.a
            );
        }

        /// <summary>
        /// 색상을 어둡게 (x0.75)
        /// </summary>
        public static Color Press(Color c)
        {
            return new Color(c.r * 0.75f, c.g * 0.75f, c.b * 0.75f, c.a);
        }

        /// <summary>
        /// 버튼 ColorBlock 생성 (normal/highlighted/pressed/disabled)
        /// </summary>
        public static ColorBlock MakeButtonColors(Color baseColor)
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor      = baseColor;
            cb.highlightedColor = Highlight(baseColor);
            cb.pressedColor     = Press(baseColor);
            cb.selectedColor    = Highlight(baseColor);
            cb.disabledColor    = new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, baseColor.a * 0.5f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.1f;
            return cb;
        }

        /// <summary>
        /// Outline 컴포넌트 추가/설정
        /// </summary>
        public static Outline ApplyOutline(GameObject go, Color color, Vector2 distance)
        {
            var outline = go.GetComponent<Outline>();
            if (outline == null)
                outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            return outline;
        }
    }
}

using UnityEngine;

namespace HexaPuzzle
{
    /// <summary>4가지 아이템 타입 — 망치 / 스왑 / 라인 / 역회전</summary>
    public enum ItemType
    {
        Hammer  = 0,
        Swap    = 1,
        Line    = 2,
        Reverse = 3,
    }

    /// <summary>
    /// 아이템별 색상 정의. 웹 디자인의 oklch 값을 sRGB로 변환한 값입니다.
    /// HexFrame_Gold + HexInner_Fill 스프라이트를 이 색으로 Tint 하면
    /// 4가지 아이템이 자동으로 시각적으로 구분됩니다.
    /// </summary>
    public static class ItemColors
    {
        // oklch(0.72 0.20 hue) → sRGB approximation
        public static readonly Color Hammer  = new Color(0.95f, 0.59f, 0.30f, 1f); // hue 35  주황
        public static readonly Color Swap    = new Color(0.42f, 0.60f, 0.95f, 1f); // hue 230 파랑
        public static readonly Color Line    = new Color(0.78f, 0.85f, 0.32f, 1f); // hue 95  노랑/연두
        public static readonly Color Reverse = new Color(0.82f, 0.50f, 0.96f, 1f); // hue 305 보라

        public static Color Get(ItemType t)
        {
            switch (t)
            {
                case ItemType.Hammer:  return Hammer;
                case ItemType.Swap:    return Swap;
                case ItemType.Line:    return Line;
                case ItemType.Reverse: return Reverse;
                default:               return Color.white;
            }
        }

        /// <summary>로컬라이즈된 이름 (UI 표시용)</summary>
        public static string GetDisplayName(ItemType t)
        {
            switch (t)
            {
                case ItemType.Hammer:  return "망치";
                case ItemType.Swap:    return "스왑";
                case ItemType.Line:    return "라인";
                case ItemType.Reverse: return "역회전";
                default:               return t.ToString();
            }
        }
    }
}

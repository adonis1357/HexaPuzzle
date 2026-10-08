using UnityEngine;

namespace Bow.Art
{
    /// <summary>「먹과 한지」 팔레트 (아트 사양서 §1.1). 금박은 호흡 링/Perfect 전용.</summary>
    public static class Palette
    {
        public static Color Hex(string hex)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return Color.magenta;
        }

        // 한지
        public static readonly Color Paper = Hex("#F3EAD3");
        public static readonly Color PaperL = Hex("#FBF6E6");
        public static readonly Color PaperD = Hex("#E2D5B3");
        public static readonly Color PaperTop = Hex("#E9DFC4");
        public static readonly Color PaperHorizon = Hex("#F8F1DC");
        // 먹
        public static readonly Color Ink = Hex("#1E1B18");
        public static readonly Color InkL = Hex("#4A4540");
        public static readonly Color InkD = Hex("#0E0C0B");
        // 주사 (플레이어)
        public static readonly Color Red = Hex("#B5312B");
        public static readonly Color RedL = Hex("#D4554D");
        public static readonly Color RedD = Hex("#86221E");
        // 쪽빛 (상대)
        public static readonly Color Blue = Hex("#2F5D8A");
        public static readonly Color BlueL = Hex("#5483B0");
        public static readonly Color BlueD = Hex("#1F4263");
        // 금박
        public static readonly Color Gold = Hex("#C9A227");
        public static readonly Color GoldL = Hex("#E8CB5A");
        public static readonly Color GoldPeak = Hex("#F0D775");
        public static readonly Color GoldD = Hex("#8E741A");
        // 회색 먹
        public static readonly Color Grey = Hex("#8A857C");
        public static readonly Color GreyL = Hex("#B8B3A8");
        public static readonly Color GreyD = Hex("#5E5A53");
        public static readonly Color Fiber = Hex("#C9B98F");

        public static Color Team(int playerId) { return playerId == 0 ? Red : Blue; }
        public static Color TeamL(int playerId) { return playerId == 0 ? RedL : BlueL; }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}

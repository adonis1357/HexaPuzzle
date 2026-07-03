using System;
using UnityEngine;

namespace HexaPuzzle.StageClear
{
    /// <summary>팝업 비주얼 테마.</summary>
    public enum StageClearTheme
    {
        /// <summary>크림/코랄 톤 — 밝은 양피지 느낌</summary>
        Parchment = 0,
        /// <summary>다크 네이비/골드 톤 — 인게임과 자연스럽게 어울리는 톤</summary>
        Mystic    = 1,
    }

    /// <summary>스테이지 클리어 결과 데이터. <see cref="StageClearPopup.Show"/> 에 전달.</summary>
    [Serializable]
    public struct StageClearResult
    {
        [Range(0, 3)] public int stars;
        public int score;
        public int gold;

        public int stageBest;     // 이 스테이지의 직전 최고 점수
        public int myBest;        // 전체 최고 점수

        public bool isNewStageBest;
        public bool isNewMyBest;

        /// <summary>켜진 별 색상 오버라이드 (alpha>0 일 때 테마 accent 대신 사용 — 난이도 색).</summary>
        public Color starColor;

        public static StageClearResult Demo(int stars = 3, int score = 19980, int gold = 10, bool isNew = true)
            => new StageClearResult
            {
                stars = stars,
                score = score,
                gold = gold,
                stageBest = score,
                myBest = score,
                isNewStageBest = isNew,
                isNewMyBest    = isNew,
            };
    }

    /// <summary>테마별 색상 팔레트. 런타임/에디터 양쪽에서 사용.</summary>
    public static class StageClearPalette
    {
        // ─── 공통 ────────────────────────────────────────────────────────────
        public static readonly Color Gold      = new Color(0.92f, 0.78f, 0.36f, 1f); // oklch(0.78 0.14 85)
        public static readonly Color GoldMid   = new Color(0.78f, 0.62f, 0.22f, 1f);
        public static readonly Color GoldDeep  = new Color(0.50f, 0.36f, 0.12f, 1f);
        public static readonly Color Coral     = new Color(0.83f, 0.39f, 0.27f, 1f); // oklch(0.62 0.18 35)
        public static readonly Color CoralDeep = new Color(0.62f, 0.25f, 0.18f, 1f);

        // ─── Parchment ───────────────────────────────────────────────────────
        public static readonly Color ParchBg       = new Color(0.971f, 0.952f, 0.910f, 1f);
        public static readonly Color ParchBgDeep   = new Color(0.913f, 0.880f, 0.820f, 1f);
        public static readonly Color ParchInk      = new Color(0.290f, 0.230f, 0.165f, 1f);
        public static readonly Color ParchSub      = new Color(0.515f, 0.450f, 0.370f, 1f);
        public static readonly Color ParchDivider  = new Color(0.760f, 0.715f, 0.640f, 1f);
        public static readonly Color ParchInner    = new Color(0.992f, 0.978f, 0.945f, 1f);

        // ─── Mystic ──────────────────────────────────────────────────────────
        public static readonly Color MysticBg      = new Color(0.140f, 0.135f, 0.205f, 1f);
        public static readonly Color MysticBgTop   = new Color(0.215f, 0.195f, 0.280f, 1f);
        public static readonly Color MysticInk     = new Color(0.952f, 0.940f, 0.890f, 1f);
        public static readonly Color MysticSub     = new Color(0.720f, 0.665f, 0.535f, 1f);
        public static readonly Color MysticInner   = new Color(0.115f, 0.110f, 0.175f, 0.55f);
        public static readonly Color MysticDivider = new Color(0.500f, 0.435f, 0.265f, 0.35f);

        public struct Resolved
        {
            public Color cardBg, cardBgTop, ink, sub, divider, innerPanel, accent, accentDeep, border;
            public Color buttonFill, buttonFillBottom, buttonShadow, buttonText;
        }

        public static Resolved Resolve(StageClearTheme theme)
        {
            if (theme == StageClearTheme.Mystic)
            {
                return new Resolved
                {
                    cardBg     = MysticBg,
                    cardBgTop  = MysticBgTop,
                    ink        = MysticInk,
                    sub        = MysticSub,
                    divider    = MysticDivider,
                    innerPanel = MysticInner,
                    accent     = Gold,
                    accentDeep = GoldDeep,
                    border     = Gold,
                    buttonFill       = Gold,
                    buttonFillBottom = GoldMid,
                    buttonShadow     = GoldDeep,
                    buttonText       = new Color(0.22f, 0.16f, 0.06f, 1f),
                };
            }
            // Parchment
            return new Resolved
            {
                cardBg     = ParchBg,
                cardBgTop  = ParchBgDeep,
                ink        = ParchInk,
                sub        = ParchSub,
                divider    = ParchDivider,
                innerPanel = ParchInner,
                accent     = Coral,
                accentDeep = CoralDeep,
                border     = Coral,
                buttonFill       = Coral,
                buttonFillBottom = CoralDeep,
                buttonShadow     = CoralDeep,
                buttonText       = Color.white,
            };
        }
    }
}

using UnityEngine;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 로비(레벨 선택) 화면 전용 다크 글래스 디자인 토큰.
    /// 인게임 HUD(UIManager.MissionPanelDark)와 통일된 어두운 네이비 글래스 + 단일 골드 액센트.
    /// 검증 통과(8.8/10)된 로비 리디자인의 공용 토큰 — 새 로비 요소는 이 값을 기본으로 사용.
    /// 색/스프라이트를 한 곳에서 관리해 화면 간 일관성을 유지한다.
    /// </summary>
    public static class LobbyTheme
    {
        // ── 글래스 패널 ─────────────────────────────────────────
        public static readonly Color PanelBg      = new Color(0.078f, 0.110f, 0.204f, 0.85f); // 다크 글래스 패널
        public static readonly Color PanelStrong  = new Color(0.055f, 0.078f, 0.157f, 0.96f); // 헤더/내비용 진한 글래스
        public static readonly Color Border       = new Color(0.471f, 0.588f, 0.863f, 0.22f); // 글래스 테두리
        public static readonly Color BorderStrong = new Color(0.471f, 0.588f, 0.863f, 0.40f); // 구분선/강조 테두리

        // ── 액센트 (단일 골드) ──────────────────────────────────
        public static readonly Color Gold     = new Color(1f, 0.816f, 0.420f, 1f); // #FFD06B 단일 골드(전 화면 동일)
        public static readonly Color GoldText = new Color(1f, 0.902f, 0.659f, 1f); // #FFE6A8 골드 숫자

        // ── 텍스트 위계 ─────────────────────────────────────────
        public static readonly Color TextPrimary   = new Color(0.918f, 0.941f, 1f, 1f);    // #EAF0FF 주 텍스트
        public static readonly Color TextSecondary = new Color(0.722f, 0.769f, 0.863f, 1f); // #B8C4DC 보조(숫자·정보)
        public static readonly Color TextCaption   = new Color(0.604f, 0.651f, 0.769f, 1f); // #9AA6C4 캡션/라벨

        // ── 둥근 글래스 스프라이트 (9-slice 캐시) ───────────────
        private static Sprite _glassPanel, _glassChip;

        /// <summary>다크 글래스 패널(카드/시트용) — 라운드 16, 테두리 2px.</summary>
        public static Sprite GlassPanelSprite => _glassPanel != null ? _glassPanel
            : (_glassPanel = ClaudeTheme.MakeRounded(64, 16, PanelBg, Border, 2));

        /// <summary>다크 글래스 칩(재화/뱃지용) — 라운드 12, 테두리 1px.</summary>
        public static Sprite GlassChipSprite => _glassChip != null ? _glassChip
            : (_glassChip = ClaudeTheme.MakeRounded(48, 12, PanelBg, Border, 1));
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 클로드(Anthropic) 디자인 팔레트 + UI 헬퍼.
    /// 따뜻한 크림 배경 + 코랄 포인트 + 슬레이트 텍스트, 둥근 모서리, 차분한 톤.
    /// UI 색/패널 스프라이트를 한 곳에서 관리해 일관성을 유지한다(레이아웃은 변경하지 않음).
    /// </summary>
    public static class ClaudeTheme
    {
        // ── 팔레트 ─────────────────────────────────────────────
        public static readonly Color PaperBg   = new Color(0.941f, 0.933f, 0.902f, 1f);  // #F0EEE6 종이 크림
        public static readonly Color Surface   = new Color(0.980f, 0.976f, 0.961f, 1f);  // #FAF9F5 패널
        public static readonly Color Card      = new Color(1f, 1f, 1f, 1f);              // 카드 화이트
        public static readonly Color Coral     = new Color(0.851f, 0.467f, 0.341f, 1f);  // #D97757 클로드 코랄
        public static readonly Color CoralDark = new Color(0.757f, 0.373f, 0.235f, 1f);  // #C15F3C 진한 코랄
        public static readonly Color CoralSoft = new Color(0.957f, 0.890f, 0.859f, 1f);  // 코랄 틴트 배경
        public static readonly Color Ink       = new Color(0.169f, 0.165f, 0.149f, 1f);  // #2B2A26 본문 텍스트
        public static readonly Color InkMuted  = new Color(0.431f, 0.420f, 0.384f, 1f);  // #6E6B62 보조 텍스트
        public static readonly Color Border    = new Color(0.846f, 0.827f, 0.776f, 1f);  // #D8D3C6 부드러운 테두리
        public static readonly Color Overlay   = new Color(0.12f, 0.11f, 0.10f, 0.58f);  // 따뜻한 딤 오버레이
        public static readonly Color Success   = new Color(0.42f, 0.58f, 0.40f, 1f);     // 차분한 세이지(성공)
        public static readonly Color Danger     = new Color(0.80f, 0.35f, 0.30f, 1f);    // 위험/경고

        // ── 다크 표면 (밝은 리치텍스트가 들어가는 대화 패널용) ──
        public static readonly Color DarkSurface = new Color(0.149f, 0.141f, 0.129f, 0.97f); // #262624 따뜻한 차콜
        public static readonly Color TextLight   = new Color(0.937f, 0.925f, 0.898f, 1f);    // 다크 위 크림 텍스트
        public static readonly Color TitleWarm   = new Color(0.98f, 0.80f, 0.52f, 1f);       // 다크 위 따뜻한 골드 타이틀

        // ── 둥근 패널 스프라이트 (9-slice 캐시) ────────────────
        private static Sprite _panel, _card, _coral, _soft, _darkPanel;

        /// <summary>차콜 다크 패널 + 소프트 코랄 테두리 (밝은 텍스트/리치텍스트용)</summary>
        public static Sprite DarkPanelSprite => _darkPanel != null ? _darkPanel
            : (_darkPanel = MakeRounded(64, 22, DarkSurface, new Color(Coral.r, Coral.g, Coral.b, 0.55f), 3));

        /// <summary>크림 패널 + 테두리 (일반 카드/팝업 배경)</summary>
        public static Sprite PanelSprite => _panel != null ? _panel : (_panel = MakeRounded(64, 22, Surface, Border, 3));
        /// <summary>화이트 카드 + 옅은 테두리</summary>
        public static Sprite CardSprite  => _card  != null ? _card  : (_card  = MakeRounded(64, 18, Card, Border, 2));
        /// <summary>코랄 채움(주 버튼)</summary>
        public static Sprite CoralSprite => _coral != null ? _coral : (_coral = MakeRounded(64, 20, Coral, new Color(0f,0f,0f,0f), 0));
        /// <summary>코랄 틴트 배경(보조 영역/뱃지)</summary>
        public static Sprite SoftSprite  => _soft  != null ? _soft  : (_soft  = MakeRounded(64, 18, CoralSoft, new Color(0f,0f,0f,0f), 0));

        /// <summary>둥근 모서리(+선택적 테두리) 9-slice 스프라이트 생성. SDF 라운드렉트.</summary>
        public static Sprite MakeRounded(int size, int radius, Color fill, Color border, int borderW)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x, r, size - 1 - r);
                    float cy = Mathf.Clamp(y, r, size - 1 - r);
                    float dx = x - cx, dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c;
                    if (dist > r) c = new Color(0f, 0f, 0f, 0f);                 // 모서리 바깥 → 투명
                    else if (borderW > 0 && dist > r - borderW) c = border;      // 테두리 링
                    else c = fill;                                               // 채움
                    // 가장자리 안티앨리어싱 (1px 페더)
                    if (c.a > 0f && dist > r - 1f && dist <= r)
                        c.a *= Mathf.Clamp01(r - dist);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            int b = radius + Mathf.Max(borderW, 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // ── 버튼 스타일 ────────────────────────────────────────
        /// <summary>주 버튼: 코랄 배경 + 흰 텍스트.</summary>
        public static void StylePrimary(Button btn, Image bg, Text label = null)
        {
            if (bg != null) { bg.sprite = CoralSprite; bg.type = Image.Type.Sliced; bg.color = Coral; }
            ApplyButtonTransition(btn, Coral, CoralDark);
            if (label != null) label.color = Color.white;
        }

        /// <summary>보조 버튼: 크림 배경 + 테두리 + 슬레이트 텍스트.</summary>
        public static void StyleSecondary(Button btn, Image bg, Text label = null)
        {
            if (bg != null) { bg.sprite = PanelSprite; bg.type = Image.Type.Sliced; bg.color = Surface; }
            ApplyButtonTransition(btn, Surface, CoralSoft);
            if (label != null) label.color = Ink;
        }

        private static void ApplyButtonTransition(Button btn, Color normal, Color pressed)
        {
            if (btn == null) return;
            btn.transition = Selectable.Transition.ColorTint;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1f, 1f, 1f, 1f);
            c.pressedColor = new Color(0.92f, 0.90f, 0.86f, 1f);
            c.selectedColor = Color.white;
            c.fadeDuration = 0.08f;
            btn.colors = c;
        }

        // ══════════════════════════════════════════════════════════
        // 다크글래스 팝업 (인게임 HUD 톤 통일 — 미션패널/MOVES프레임/게이지/골드칩과 어울림)
        // 모든 모달 팝업의 배경/버튼/딤/텍스트를 한 곳에서 통일. 2026-06-29.
        // ══════════════════════════════════════════════════════════
        public static readonly Color PopupOverlay        = new Color(0.04f, 0.06f, 0.12f, 0.74f); // 일관 다크 딤
        public static readonly Color PopupTitle          = new Color(0.96f, 0.83f, 0.52f, 1f);    // 골드 타이틀
        public static readonly Color PopupText           = new Color(0.90f, 0.92f, 0.97f, 1f);    // 라이트 본문
        public static readonly Color PopupTextMuted      = new Color(0.62f, 0.68f, 0.80f, 1f);    // 보조 텍스트
        public static readonly Color PopupPrimaryLabel   = new Color(0.22f, 0.13f, 0.02f, 1f);    // 골드 버튼 위 다크 라벨
        public static readonly Color PopupSecondaryLabel = new Color(0.88f, 0.92f, 0.98f, 1f);    // 다크 버튼 위 라이트 라벨

        /// <summary>팝업 배경 패널 — 클로드 디자인 다크글래스 9-slice(UI/popup_panel). 없으면 DarkPanelSprite 폴백.</summary>
        public static void ApplyPopupPanel(Image img)
        {
            if (img == null) return;
            var s = Resources.Load<Sprite>("UI/popup_panel");
            if (s != null) { img.sprite = s; img.type = Image.Type.Sliced; img.color = Color.white; }
            else { img.sprite = DarkPanelSprite; img.color = DarkSurface; }
        }

        /// <summary>주 버튼(확인/구매/계속) — 골드 글래스 9-slice. label은 다크 라벨색.</summary>
        public static void StylePopupPrimary(Button btn, Image bg, Text label = null)
        {
            var s = Resources.Load<Sprite>("UI/popup_btn_primary");
            if (bg != null)
            {
                if (s != null) { bg.sprite = s; bg.type = Image.Type.Sliced; bg.color = Color.white; }
                else { bg.sprite = CoralSprite; bg.type = Image.Type.Sliced; bg.color = Coral; }
            }
            ApplyButtonTransition(btn, Color.white, new Color(0.88f, 0.88f, 0.88f));
            if (label != null) label.color = PopupPrimaryLabel;
        }

        /// <summary>보조 버튼(취소/나가기) — 다크글래스 9-slice + 스틸 림. label은 라이트 라벨색.</summary>
        public static void StylePopupSecondary(Button btn, Image bg, Text label = null)
        {
            var s = Resources.Load<Sprite>("UI/popup_btn_secondary");
            if (bg != null)
            {
                if (s != null) { bg.sprite = s; bg.type = Image.Type.Sliced; bg.color = Color.white; }
                else { bg.sprite = PanelSprite; bg.type = Image.Type.Sliced; bg.color = DarkSurface; }
            }
            ApplyButtonTransition(btn, Color.white, new Color(0.82f, 0.86f, 0.95f));
            if (label != null) label.color = PopupSecondaryLabel;
        }
    }
}

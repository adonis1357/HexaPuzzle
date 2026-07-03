using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 다크 글래스 디자인 토큰(LobbyTheme) 기반 공용 UI 컴포넌트 팩토리.
    /// 전 화면이 이 팩토리만 호출해 글래스 패널/칩/버튼/HUD프레임/라벨값/배지/진행바를 일관 생성한다.
    /// (Step 1 — UI 전면 재디자인 프로그램의 기반 레이어. Dev_UI_Redesign_Program.md 참조)
    /// 공유 캐시 스프라이트를 흰색으로 굽고 Image.color로 틴트 → 화면당 텍스처 1개 미만(GC 최소).
    /// </summary>
    public static class UIFactory
    {
        // ── 공유 캐시 스프라이트 (흰색으로 굽고 color로 틴트) ──────────
        private static Sprite _whiteCard;   // 버튼/카드 채움 (라운드 14)
        private static Sprite _whiteCircle; // 원형 배지 (라운드 20)

        /// <summary>라운드14 흰 카드 스프라이트(틴트용). 버튼/채움 베이스.</summary>
        public static Sprite WhiteCardSprite => _whiteCard != null ? _whiteCard
            : (_whiteCard = ClaudeTheme.MakeRounded(48, 14, Color.white, new Color(0f, 0f, 0f, 0f), 0));

        /// <summary>원형 흰 스프라이트(틴트용). 배지/도트 베이스.</summary>
        public static Sprite WhiteCircleSprite => _whiteCircle != null ? _whiteCircle
            : (_whiteCircle = ClaudeTheme.MakeRounded(40, 20, Color.white, new Color(0f, 0f, 0f, 0f), 0));

        // ── 내부 헬퍼 ─────────────────────────────────────────────
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // ── 패널/바 ───────────────────────────────────────────────
        /// <summary>다크 글래스 패널(카드/시트 베이스, 라운드16·테두리2px).</summary>
        public static GameObject MakeGlassPanel(string name, Transform parent)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LobbyTheme.GlassPanelSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            return rt.gameObject;
        }

        /// <summary>평면 단색 바(헤더/내비 강패널·구분선). 풀스트레치 가능.</summary>
        public static GameObject MakeSolidBar(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return rt.gameObject;
        }

        // ── 칩 ────────────────────────────────────────────────────
        /// <summary>다크 글래스 칩(재화/뱃지 배경, 라운드12·테두리1px). 라벨/값은 호출자가 추가.</summary>
        public static GameObject MakeGlassChip(string name, Transform parent)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LobbyTheme.GlassChipSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            return rt.gameObject;
        }

        // ── 버튼 ──────────────────────────────────────────────────
        /// <summary>1차 버튼(골드 채움 + 진한 골드 텍스트). 1순위 액션.</summary>
        public static Button MakePrimaryButton(Transform parent, Font font, string label, out Text labelText, int fontSize = 16)
        {
            var rt = NewRect("PrimaryButton", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = WhiteCardSprite; img.type = Image.Type.Sliced; img.color = LobbyTheme.Gold;
            var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var lrt = NewRect("Label", rt.transform); Stretch(lrt);
            labelText = lrt.gameObject.AddComponent<Text>();
            labelText.font = font; labelText.fontSize = fontSize; labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.353f, 0.239f, 0f, 1f); // 진한 골드
            labelText.raycastTarget = false; labelText.text = label;
            return btn;
        }

        /// <summary>2차 버튼(글래스 + 테두리, 밝은 텍스트). 보조 액션.</summary>
        public static Button MakeSecondaryButton(Transform parent, Font font, string label, out Text labelText, int fontSize = 16)
        {
            var rt = NewRect("SecondaryButton", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LobbyTheme.GlassChipSprite; img.type = Image.Type.Sliced; img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
            var lrt = NewRect("Label", rt.transform); Stretch(lrt);
            labelText = lrt.gameObject.AddComponent<Text>();
            labelText.font = font; labelText.fontSize = fontSize; labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = LobbyTheme.TextPrimary; labelText.raycastTarget = false; labelText.text = label;
            return btn;
        }

        // ── HUD 프레임 / 라벨+값 ──────────────────────────────────
        /// <summary>글래스 프레임 안에 값(큼) 위, 라벨(작음) 아래로 쌓는 HUD 프레임. VH-2 위계.</summary>
        public static GameObject MakeHudFrame(string name, Transform parent, Font font, string value, string label,
            out Text valueText, out Text labelText, int valueSize = 22, int labelSize = 12)
        {
            var panel = MakeGlassPanel(name, parent);
            MakeLabeledValueInto(panel.transform, font, value, label, out valueText, out labelText, valueSize, labelSize);
            return panel;
        }

        /// <summary>프레임 없는 값(큼)+라벨(작음) 수직 스택. 컨테이너 GameObject 반환.</summary>
        public static GameObject MakeLabeledValue(string name, Transform parent, Font font, string value, string label,
            out Text valueText, out Text labelText, int valueSize = 24, int labelSize = 12)
        {
            var rt = NewRect(name, parent);
            MakeLabeledValueInto(rt.transform, font, value, label, out valueText, out labelText, valueSize, labelSize);
            return rt.gameObject;
        }

        private static void MakeLabeledValueInto(Transform container, Font font, string value, string label,
            out Text valueText, out Text labelText, int valueSize, int labelSize)
        {
            var vrt = NewRect("Value", container);
            vrt.anchorMin = new Vector2(0f, 0.42f); vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            valueText = vrt.gameObject.AddComponent<Text>();
            valueText.font = font; valueText.fontSize = valueSize; valueText.alignment = TextAnchor.LowerCenter;
            valueText.color = LobbyTheme.TextPrimary; valueText.raycastTarget = false; valueText.text = value;

            var lrt = NewRect("Label", container);
            lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(1f, 0.42f);
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            labelText = lrt.gameObject.AddComponent<Text>();
            labelText.font = font; labelText.fontSize = labelSize; labelText.alignment = TextAnchor.UpperCenter;
            labelText.color = LobbyTheme.TextCaption; labelText.raycastTarget = false; labelText.text = label;
        }

        // ── 배지 ──────────────────────────────────────────────────
        /// <summary>원형 색상 배지 + 글리프(색+형태 이중단서, AC-2). 색맹 대응.</summary>
        public static GameObject MakeBadge(string name, Transform parent, Font font, string glyph, Color bgColor, Color glyphColor, float size = 24f)
        {
            var rt = NewRect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = WhiteCircleSprite; img.type = Image.Type.Sliced; img.color = bgColor;
            img.raycastTarget = false;
            var grt = NewRect("Glyph", rt.transform); Stretch(grt);
            var gT = grt.gameObject.AddComponent<Text>();
            gT.font = font; gT.fontSize = Mathf.RoundToInt(size * 0.5f); gT.alignment = TextAnchor.MiddleCenter;
            gT.color = glyphColor; gT.raycastTarget = false; gT.text = glyph;
            return rt.gameObject;
        }

        // ── 진행바 ─────────────────────────────────────────────────
        /// <summary>트랙+채움 진행바. 반환: Fill RectTransform(anchorMax.x로 채움률 갱신).</summary>
        public static RectTransform MakeProgressBar(string name, Transform parent, float fill, Color fillColor)
        {
            var rt = NewRect(name, parent);
            var track = rt.gameObject.AddComponent<Image>();
            track.color = new Color(0.10f, 0.13f, 0.25f, 1f); track.raycastTarget = false;
            var frt = NewRect("Fill", rt.transform);
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            var fimg = frt.gameObject.AddComponent<Image>();
            fimg.color = fillColor; fimg.raycastTarget = false;
            return frt;
        }
    }
}

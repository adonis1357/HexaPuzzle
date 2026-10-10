using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Bow.Art;
using Bow.Core;

namespace Bow.UI
{
    /// <summary>로비 화면 (아트 §6.1): 엔소 타이틀 「활」 + 봇 대전(난이도 3) / LAN 호스트 / LAN 참가.</summary>
    public sealed class LobbyView : MonoBehaviour
    {
        public System.Action<BotProfile> OnBotMatch;
        public System.Action OnHost;
        public System.Action<string> OnJoin;
        /// <summary>캐릭터 선택 패널에서 미리보기 대상이 바뀜 (null = 패널 닫힘)</summary>
        public System.Action<CharacterDef> OnCharacterPreview;

        private RectTransform charPanel;
        private Text charName, charTheme, charDesc, charIndexText, charTrait;
        private Image radarImg;
        private Text[] radarLabels, radarValues;
        private readonly System.Collections.Generic.Dictionary<string, Sprite> radarCache = new System.Collections.Generic.Dictionary<string, Sprite>();
        private const float RadarR = 160f;

        private static Vector2 AxisPoint(int i, int n, float r)
        {
            float a = (90f + 360f * i / n) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        private Image[,] statBlocks;   // [6, 5]
        private Text[] statNums;
        private Button charButton;
        private int charIndex;
        private RectTransform titleGroup;

        private Canvas canvas;
        private RectTransform root;
        private RectTransform mainButtons, difficultyPanel, lanPanel;
        private InputField ipField;
        private Text statusText, titleText;
        private Image enso;

        private static readonly Vector2 C = new Vector2(0.5f, 0.5f), TC = new Vector2(0.5f, 1f);

        public void Build()
        {
            canvas = UiFactory.CreateCanvas("Lobby", 20);
            canvas.transform.SetParent(transform, false);
            root = (RectTransform)canvas.transform;

            // 배경 (한지 그라디언트는 월드 카메라 배경이 담당) — 하단 먹 번짐 띠
            Image bottom = UiFactory.MakeImage(root, "inkBand", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Ink, 0.25f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1080f, 240f));
            bottom.rectTransform.anchorMin = new Vector2(0f, 0f); bottom.rectTransform.anchorMax = new Vector2(1f, 0f); bottom.rectTransform.sizeDelta = new Vector2(0f, 240f);

            // 엔소 + 타이틀 (캐릭터 선택 시 숨김)
            titleGroup = UiFactory.Rect(root, "titleGroup");
            UiFactory.Stretch(titleGroup);
            enso = UiFactory.MakeImage(titleGroup, "enso", ProceduralSprites.Enso(), Palette.Ink, TC, C, new Vector2(0f, -560f), new Vector2(560f, 560f));
            enso.type = Image.Type.Filled; enso.fillMethod = Image.FillMethod.Radial360; enso.fillOrigin = (int)Image.Origin360.Left; enso.fillClockwise = false; enso.fillAmount = 0f;
            titleText = UiFactory.MakeText(titleGroup, "title", "활", 220, Palette.WithAlpha(Palette.Ink, 0f), TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -560f), new Vector2(560f, 300f), true);
            Image seal = UiFactory.MakeImage(titleGroup, "seal", ProceduralSprites.WhiteRect(), Palette.Red, TC, C, new Vector2(200f, -760f), new Vector2(96f, 96f));
            UiFactory.MakeText(seal.transform, "sealText", "활", 56, Palette.PaperL, TextAnchor.MiddleCenter, C, C, Vector2.zero, new Vector2(96f, 96f), true);
            UiFactory.MakeText(titleGroup, "subtitle", "실시간 궁수 대전 · 프로토타입", 36, Palette.Grey, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -900f), new Vector2(800f, 50f));

            // 메인 버튼
            mainButtons = UiFactory.Rect(root, "mainButtons");
            UiFactory.Stretch(mainButtons);
            UiFactory.MakeButton(mainButtons, "봇 대전", TC, C, new Vector2(0f, -1070f), new Vector2(560f, 140f), true, () => ShowPanel(difficultyPanel));
            UiFactory.MakeButton(mainButtons, "LAN 호스트", TC, C, new Vector2(0f, -1242f), new Vector2(560f, 140f), false, () => { if (OnHost != null) OnHost(); });
            UiFactory.MakeButton(mainButtons, "LAN 참가", TC, C, new Vector2(0f, -1414f), new Vector2(560f, 140f), false, () => ShowPanel(lanPanel));
            charButton = UiFactory.MakeButton(mainButtons, "캐릭터", TC, C, new Vector2(0f, -1574f), new Vector2(560f, 120f), false, () => OpenCharacterPanel());
            charButton.GetComponentInChildren<Text>().fontSize = 44;
            RefreshCharButtonLabel();

            // 캐릭터 선택 패널 (미리보기는 월드의 ArcherView가 담당)
            charPanel = UiFactory.Rect(root, "charPanel");
            UiFactory.Stretch(charPanel);
            UiFactory.MakeText(charPanel, "label", "캐릭터 선택", 44, Palette.Grey, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -200f), new Vector2(600f, 50f));
            charName = UiFactory.MakeText(charPanel, "name", "", 96, Palette.Ink, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -290f), new Vector2(900f, 110f), true);
            charTheme = UiFactory.MakeText(charPanel, "theme", "", 34, Palette.Red, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -370f), new Vector2(900f, 40f));
            // 능력치 육각형 (오른쪽), 미리보기 궁수는 월드(왼쪽)
            RectTransform radarRt = UiFactory.Rect(charPanel, "radar");
            UiFactory.Place(radarRt, TC, C, new Vector2(250f, -720f), new Vector2(440f, 440f));
            // 차트 본체: 프로시저럴 텍스처 (반지름 160px = 텍스처 크기 × 0.36 → 444px)
            radarImg = UiFactory.MakeImage(radarRt, "chart", null, Color.white, C, C, Vector2.zero, new Vector2(RadarR / 0.36f, RadarR / 0.36f));
            radarLabels = new Text[6]; radarValues = new Text[6];
            for (int i = 0; i < 6; i++)
            {
                Vector2 pt = AxisPoint(i, 6, RadarR + 46f);
                radarLabels[i] = UiFactory.MakeText(radarRt, "lbl" + i, CharacterStats.Labels[i], 28, Palette.Ink, TextAnchor.MiddleCenter, C, C, pt + new Vector2(0f, 12f), new Vector2(160f, 34f));
                radarValues[i] = UiFactory.MakeText(radarRt, "val" + i, "3", 30, Palette.Red, TextAnchor.MiddleCenter, C, C, pt + new Vector2(0f, -20f), new Vector2(160f, 34f), true);
            }
            // 능력치 표: 항목 | ■■■□□ | 수치 (6행)
            RectTransform table = UiFactory.Rect(charPanel, "statTable");
            UiFactory.Place(table, TC, new Vector2(0.5f, 1f), new Vector2(0f, -990f), new Vector2(720f, 6 * 40f + 16f));
            Image tableBg = UiFactory.MakeImage(table, "bg", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.PaperL, 0.6f), C, C, Vector2.zero, new Vector2(720f, 6 * 40f + 16f));
            UiFactory.MakeImage(table, "border", ProceduralSprites.RoundedBox(), Palette.WithAlpha(Palette.Ink, 0.6f), C, C, Vector2.zero, new Vector2(720f, 6 * 40f + 16f)).type = Image.Type.Sliced;
            statBlocks = new Image[6, 5]; statNums = new Text[6];
            for (int i = 0; i < 6; i++)
            {
                float y = -8f - 20f - i * 40f;
                UiFactory.MakeText(table, "lbl" + i, CharacterStats.Labels[i], 30, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(28f, y), new Vector2(160f, 36f));
                for (int k = 0; k < 5; k++)
                    statBlocks[i, k] = UiFactory.MakeImage(table, "blk" + i + "_" + k, ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Ink, 0.12f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(200f + k * 78f, y), new Vector2(70f, 24f));
                statNums[i] = UiFactory.MakeText(table, "num" + i, "3", 32, Palette.Ink, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-28f, y), new Vector2(80f, 36f), true);
            }
            charTrait = UiFactory.MakeText(charPanel, "trait", "", 32, Palette.Ink, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -1270f), new Vector2(1000f, 44f), true);
            charDesc = UiFactory.MakeText(charPanel, "desc", "", 34, Palette.Grey, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -1318f), new Vector2(900f, 46f));
            charIndexText = UiFactory.MakeText(charPanel, "index", "", 30, Palette.Grey, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -1362f), new Vector2(300f, 40f));
            UiFactory.MakeButton(charPanel, "◀", new Vector2(0f, 1f), C, new Vector2(90f, -1450f), new Vector2(130f, 130f), false, () => StepCharacter(-1));
            UiFactory.MakeButton(charPanel, "▶", new Vector2(1f, 1f), C, new Vector2(-90f, -1450f), new Vector2(130f, 130f), false, () => StepCharacter(1));
            UiFactory.MakeButton(charPanel, "이 캐릭터로", TC, C, new Vector2(0f, -1450f), new Vector2(560f, 130f), true, () => CloseCharacterPanel());
            charPanel.gameObject.SetActive(false);

            // 난이도 패널
            difficultyPanel = UiFactory.Rect(root, "difficulty");
            UiFactory.Stretch(difficultyPanel);
            UiFactory.MakeText(difficultyPanel, "label", "상대 난이도", 44, Palette.Ink, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -1010f), new Vector2(600f, 50f));
            UiFactory.MakeButton(difficultyPanel, "쉬움", TC, C, new Vector2(0f, -1110f), new Vector2(560f, 120f), false, () => Pick(BotProfile.Easy()));
            UiFactory.MakeButton(difficultyPanel, "보통", TC, C, new Vector2(0f, -1260f), new Vector2(560f, 120f), true, () => Pick(BotProfile.Normal()));
            UiFactory.MakeButton(difficultyPanel, "어려움", TC, C, new Vector2(0f, -1410f), new Vector2(560f, 120f), false, () => Pick(BotProfile.Hard()));
            UiFactory.MakeButton(difficultyPanel, "뒤로", TC, C, new Vector2(0f, -1560f), new Vector2(300f, 90f), false, () => ShowPanel(mainButtons));
            difficultyPanel.gameObject.SetActive(false);

            // LAN 참가 패널
            lanPanel = UiFactory.Rect(root, "lan");
            UiFactory.Stretch(lanPanel);
            UiFactory.MakeText(lanPanel, "label", "호스트 IP 주소 (같은 Wi-Fi)", 40, Palette.Ink, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -1010f), new Vector2(800f, 50f));
            ipField = UiFactory.MakeInput(lanPanel, "예: 192.168.0.10", TC, C, new Vector2(0f, -1110f), new Vector2(640f, 110f));
            ipField.text = PlayerPrefs.GetString("hwal_last_ip", "");
            UiFactory.MakeButton(lanPanel, "접속", TC, C, new Vector2(0f, -1270f), new Vector2(560f, 130f), true, () =>
            {
                string ip = ipField.text.Trim();
                if (ip.Length == 0) { SetStatus("IP 주소를 입력하세요"); return; }
                PlayerPrefs.SetString("hwal_last_ip", ip);
                if (OnJoin != null) OnJoin(ip);
            });
            UiFactory.MakeButton(lanPanel, "뒤로", TC, C, new Vector2(0f, -1430f), new Vector2(300f, 90f), false, () => ShowPanel(mainButtons));
            lanPanel.gameObject.SetActive(false);

            statusText = UiFactory.MakeText(root, "status", "", 34, Palette.Grey, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1000f, 44f));

            StartCoroutine(Intro());
        }

        private void Pick(BotProfile p) { if (OnBotMatch != null) OnBotMatch(p); }

        private void ShowPanel(RectTransform panel)
        {
            mainButtons.gameObject.SetActive(panel == mainButtons);
            difficultyPanel.gameObject.SetActive(panel == difficultyPanel);
            lanPanel.gameObject.SetActive(panel == lanPanel);
            if (charPanel != null) charPanel.gameObject.SetActive(false);
            if (titleGroup != null) titleGroup.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------
        // 캐릭터 선택
        // ---------------------------------------------------------------
        public static string SelectedCharacterId
        {
            get { return PlayerPrefs.GetString("hwal_char_id", CharacterCatalog.At(0).id); }
            set { PlayerPrefs.SetString("hwal_char_id", value); PlayerPrefs.Save(); }
        }

        private void RefreshCharButtonLabel()
        {
            if (charButton == null) return;
            charButton.GetComponentInChildren<Text>().text = "캐릭터: " + CharacterCatalog.Get(SelectedCharacterId).name;
        }

        private void OpenCharacterPanel()
        {
            charIndex = CharacterCatalog.IndexOf(SelectedCharacterId);
            mainButtons.gameObject.SetActive(false);
            difficultyPanel.gameObject.SetActive(false);
            lanPanel.gameObject.SetActive(false);
            titleGroup.gameObject.SetActive(false);
            charPanel.gameObject.SetActive(true);
            ShowCharacter();
        }

        private void StepCharacter(int delta)
        {
            charIndex = ((charIndex + delta) % CharacterCatalog.Count + CharacterCatalog.Count) % CharacterCatalog.Count;
            ShowCharacter();
        }

        private void ShowCharacter()
        {
            CharacterDef d = CharacterCatalog.At(charIndex);
            charName.text = d.name;
            charTheme.text = d.theme + " · 활: " + d.bowStyle;
            charDesc.text = d.desc;
            CharacterStats st = d.stats ?? new CharacterStats();
            int[] v = st.Values;
            Sprite chart;
            if (!radarCache.TryGetValue(d.id, out chart))
            {
                chart = ProceduralSprites.RadarChart(v, 5, 256, Palette.WithAlpha(Palette.Red, 0.55f), Palette.Ink, Palette.WithAlpha(Palette.Ink, 0.22f));
                radarCache[d.id] = chart;
            }
            radarImg.sprite = chart;
            for (int i = 0; i < 6; i++)
            {
                Color vc = v[i] >= 4 ? Palette.Red : (v[i] <= 2 ? Palette.Grey : Palette.Ink);
                radarValues[i].text = v[i].ToString();
                radarValues[i].color = vc;
                statNums[i].text = v[i].ToString();
                statNums[i].color = vc;
                for (int k = 0; k < 5; k++)
                    statBlocks[i, k].color = k < v[i] ? vc : Palette.WithAlpha(Palette.Ink, 0.12f);
            }
            charTrait.text = "특징: " + st.Highlights()
                + "  ·  체력 " + st.MaxHp(100) + "  속도 ×" + st.SpeedMul.ToString("F2") + "  바람 ×" + st.WindMul.ToString("F2")
                + "  장전 ×" + st.DelayMul.ToString("F2");
            charIndexText.text = (charIndex + 1) + " / " + CharacterCatalog.Count;
            if (OnCharacterPreview != null) OnCharacterPreview(d);
        }

        private void CloseCharacterPanel()
        {
            SelectedCharacterId = CharacterCatalog.At(charIndex).id;
            RefreshCharButtonLabel();
            if (OnCharacterPreview != null) OnCharacterPreview(null);
            ShowPanel(mainButtons);
        }

        private IEnumerator Intro()
        {
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.unscaledDeltaTime;
                enso.fillAmount = Ease.OutCubic(t / 0.9f);
                yield return null;
            }
            enso.fillAmount = 1f;
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                titleText.color = Palette.WithAlpha(Palette.Ink, t / 0.3f);
                yield return null;
            }
            titleText.color = Palette.Ink;
        }

        public void SetStatus(string s) { statusText.text = s; }
        public void ShowMain() { ShowPanel(mainButtons); }
        public void SetVisible(bool v) { canvas.gameObject.SetActive(v); }
    }
}

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

            // 엔소 + 타이틀
            enso = UiFactory.MakeImage(root, "enso", ProceduralSprites.Enso(), Palette.Ink, TC, C, new Vector2(0f, -560f), new Vector2(560f, 560f));
            enso.type = Image.Type.Filled; enso.fillMethod = Image.FillMethod.Radial360; enso.fillOrigin = (int)Image.Origin360.Left; enso.fillClockwise = false; enso.fillAmount = 0f;
            titleText = UiFactory.MakeText(root, "title", "활", 220, Palette.WithAlpha(Palette.Ink, 0f), TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -560f), new Vector2(560f, 300f), true);
            Image seal = UiFactory.MakeImage(root, "seal", ProceduralSprites.WhiteRect(), Palette.Red, TC, C, new Vector2(200f, -760f), new Vector2(96f, 96f));
            UiFactory.MakeText(seal.transform, "sealText", "활", 56, Palette.PaperL, TextAnchor.MiddleCenter, C, C, Vector2.zero, new Vector2(96f, 96f), true);
            UiFactory.MakeText(root, "subtitle", "실시간 궁수 대전 · 프로토타입", 36, Palette.Grey, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -900f), new Vector2(800f, 50f));

            // 메인 버튼
            mainButtons = UiFactory.Rect(root, "mainButtons");
            UiFactory.Stretch(mainButtons);
            UiFactory.MakeButton(mainButtons, "봇 대전", TC, C, new Vector2(0f, -1070f), new Vector2(560f, 140f), true, () => ShowPanel(difficultyPanel));
            UiFactory.MakeButton(mainButtons, "LAN 호스트", TC, C, new Vector2(0f, -1242f), new Vector2(560f, 140f), false, () => { if (OnHost != null) OnHost(); });
            UiFactory.MakeButton(mainButtons, "LAN 참가", TC, C, new Vector2(0f, -1414f), new Vector2(560f, 140f), false, () => ShowPanel(lanPanel));

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

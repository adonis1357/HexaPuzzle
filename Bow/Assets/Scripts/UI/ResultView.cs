using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Bow.Art;

namespace Bow.UI
{
    /// <summary>결과 화면 (아트 §6.3): 승리/패배/무승부 + 통계 패널 + 다시하기/로비</summary>
    public sealed class ResultView : MonoBehaviour
    {
        public System.Action OnRetry;
        public System.Action OnLobby;

        private Canvas canvas;
        private RectTransform root;
        private Image overlay;
        private RectTransform panel;
        private Text mainText, statsText;
        private Image seal;
        private Button retryButton;

        private static readonly Vector2 C = new Vector2(0.5f, 0.5f), TC = new Vector2(0.5f, 1f);

        public void Build()
        {
            canvas = UiFactory.CreateCanvas("Result", 30);
            canvas.transform.SetParent(transform, false);
            root = (RectTransform)canvas.transform;
            overlay = UiFactory.MakeImage(root, "overlay", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Paper, 0f), C, C, Vector2.zero, Vector2.zero);
            UiFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            mainText = UiFactory.MakeText(root, "main", "", 200, Palette.Red, TextAnchor.MiddleCenter, TC, C, new Vector2(0f, -620f), new Vector2(900f, 260f), true);
            seal = UiFactory.MakeImage(root, "seal", ProceduralSprites.WhiteRect(), Palette.Red, TC, C, new Vector2(0f, -620f), new Vector2(220f, 220f));
            seal.gameObject.SetActive(false);

            panel = UiFactory.Rect(root, "panel");
            UiFactory.Place(panel, TC, C, new Vector2(0f, -1150f), new Vector2(840f, 520f));
            Image face = panel.gameObject.AddComponent<Image>();
            face.sprite = ProceduralSprites.WhiteRect(); face.color = Palette.WithAlpha(Palette.PaperL, 0.95f);
            UiFactory.MakeImage(panel, "border", ProceduralSprites.RoundedBox(), Palette.Ink, C, C, Vector2.zero, new Vector2(840f, 520f)).type = Image.Type.Sliced;
            statsText = UiFactory.MakeText(panel, "stats", "", 40, Palette.Ink, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -50f), new Vector2(720f, 280f));
            statsText.lineSpacing = 1.3f;
            retryButton = UiFactory.MakeButton(panel, "다시하기", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-190f, 40f), new Vector2(340f, 110f), true, () => { if (OnRetry != null) OnRetry(); });
            UiFactory.MakeButton(panel, "로비", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(190f, 40f), new Vector2(340f, 110f), false, () => { if (OnLobby != null) OnLobby(); });
            canvas.gameObject.SetActive(false);
        }

        /// <param name="outcome">1 승리, -1 패배, 0 무승부</param>
        public void Show(int outcome, string stats, bool allowRetry)
        {
            canvas.gameObject.SetActive(true);
            statsText.text = stats;
            retryButton.interactable = allowRetry;
            panel.gameObject.SetActive(false);
            seal.gameObject.SetActive(false);
            StopAllCoroutines();
            StartCoroutine(ShowRoutine(outcome));
        }

        private IEnumerator ShowRoutine(int outcome)
        {
            float t = 0f;
            while (t < ArtConstants.ResultOverlayFade)
            {
                t += Time.unscaledDeltaTime;
                overlay.color = Palette.WithAlpha(outcome < 0 ? Palette.Hex("#CFC6B0") : Palette.Paper, 0.85f * Ease.OutCubic(t / ArtConstants.ResultOverlayFade));
                yield return null;
            }
            if (outcome > 0)
            {
                mainText.text = "승리"; mainText.color = Palette.Red; mainText.fontSize = 200;
                seal.gameObject.SetActive(true);
                Bow.Audio.BowAudio.I.Play("victory_stinger", 0.9f);
                t = 0f;
                while (t < 0.35f)
                {
                    t += Time.unscaledDeltaTime;
                    float s = Mathf.LerpUnclamped(2.2f, 1f, Ease.OutBack(t / 0.35f));
                    seal.rectTransform.localScale = new Vector3(s, s, 1f);
                    seal.color = Palette.WithAlpha(Palette.Red, 0.25f);
                    yield return null;
                }
                seal.rectTransform.localScale = Vector3.one;
                StartCoroutine(GoldConfetti());
            }
            else if (outcome < 0)
            {
                mainText.text = "패배"; mainText.fontSize = 200;
                Bow.Audio.BowAudio.I.Play("defeat_stinger", 0.9f);
                Vector2 basePos = new Vector2(0f, -620f);
                t = 0f;
                while (t < 0.6f)
                {
                    t += Time.unscaledDeltaTime;
                    float u = Ease.OutCubic(t / 0.6f);
                    mainText.rectTransform.anchoredPosition = basePos + new Vector2(0f, 30f * (1f - u));
                    mainText.color = Palette.WithAlpha(Palette.Grey, u);
                    yield return null;
                }
                mainText.rectTransform.anchoredPosition = basePos;
            }
            else
            {
                mainText.text = "무승부"; mainText.fontSize = 160; mainText.color = Palette.Ink;
            }
            panel.gameObject.SetActive(true);
        }

        private IEnumerator GoldConfetti()
        {
            Image[] bits = new Image[24];
            float[] vy = new float[24], rot = new float[24];
            for (int i = 0; i < 24; i++)
            {
                bits[i] = UiFactory.MakeImage(root, "gold", ProceduralSprites.WhiteRect(), Palette.Gold, TC, C, new Vector2(Random.Range(-500f, 500f), Random.Range(0f, 300f)), new Vector2(12f, 12f));
                vy[i] = Random.Range(120f, 220f); rot[i] = Random.Range(-180f, 180f);
            }
            float t = 0f;
            while (t < 3f)
            {
                float dt = Time.unscaledDeltaTime; t += dt;
                for (int i = 0; i < 24; i++)
                {
                    if (bits[i] == null) continue;
                    bits[i].rectTransform.anchoredPosition += new Vector2(0f, -vy[i] * dt);
                    bits[i].rectTransform.Rotate(0f, 0f, rot[i] * dt);
                    bits[i].color = Palette.WithAlpha(Palette.Gold, Mathf.Clamp01((3f - t) / 0.5f));
                }
                yield return null;
            }
            for (int i = 0; i < 24; i++) if (bits[i] != null) Destroy(bits[i].gameObject);
        }

        public void Hide() { StopAllCoroutines(); canvas.gameObject.SetActive(false); }
    }
}

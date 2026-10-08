using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Bow.Art;

namespace Bow.UI
{
    /// <summary>
    /// 경기 HUD (아트 §2.3~2.7, §4.5). Overlay Canvas 위에 코드로 생성.
    /// MatchController가 매 프레임 Set* 메서드로 값을 밀어 넣는다.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private static readonly Vector2 TL = new Vector2(0f, 1f), TR = new Vector2(1f, 1f), TC = new Vector2(0.5f, 1f);
        private static readonly Vector2 BC = new Vector2(0.5f, 0f), C = new Vector2(0.5f, 0.5f), BL = new Vector2(0f, 0f);

        private Canvas canvas;
        private RectTransform root;
        private Camera cam;

        // HP
        private readonly Image[] hpFill = new Image[2];
        private readonly Image[] hpGhost = new Image[2];
        private readonly Text[] hpText = new Text[2];
        private readonly float[] ghostTarget = new float[2];
        private readonly Coroutine[] ghostRoutine = new Coroutine[2];
        private Text[] nameText = new Text[2];

        // 타이머
        private Text timerText;
        private int lastShownSecond = -1;

        // 바람
        private RectTransform windBar, windHead, nextBar, nextHead;
        private Image windBarImg, windHeadImg, nextBarImg, nextHeadImg;
        private Text windText, windNextText;
        private float windLenCur = 20f, windSignCur = 1f;

        // 호흡 링
        private RectTransform ringRoot;
        private Image ringFill, ringTrack, ringGlow;
        private Text ringBig, ringSmall;
        private bool ringWasReady, ringWasPeak;

        // 추 미터
        private RectTransform meterRoot, pendulum, pendulumString, autoLine;
        private Image pendulumImg, meterBorder;
        private CanvasGroup meterGroup;

        // 조준
        private Text aimText;
        private RectTransform aimRt;

        // 메시지/섬광/카운트다운
        private Text message, countdown, statusText;
        private Image flash;
        private Coroutine messageRoutine;

        public System.Action OnExitPressed;

        public void Build(Camera camera)
        {
            cam = camera;
            canvas = UiFactory.CreateCanvas("HUD", 10);
            canvas.transform.SetParent(transform, false);
            root = (RectTransform)canvas.transform;

            // 세이프 에어리어 상단 패딩
            float topInset = Screen.height > 0 ? (Screen.height - Screen.safeArea.yMax) / Screen.height * 1920f : 0f;

            // ---- HP 바 ----
            for (int i = 0; i < 2; i++)
            {
                bool me = i == 0;
                Vector2 anchor = me ? TL : TR;
                float sx = me ? 1f : -1f;
                nameText[i] = UiFactory.MakeText(root, "name" + i, me ? "나" : "상대", 40, Palette.Ink, me ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, anchor, anchor, new Vector2(40f * sx, -40f - topInset), new Vector2(400f, 44f));
                Image frame = UiFactory.MakeImage(root, "hpFrame" + i, ProceduralSprites.WhiteRect(), Palette.Ink, anchor, anchor, new Vector2(40f * sx, -90f - topInset), new Vector2(400f, 36f));
                Image inner = UiFactory.MakeImage(frame.transform, "inner", ProceduralSprites.WhiteRect(), Palette.Paper, C, C, Vector2.zero, new Vector2(394f, 30f));
                hpGhost[i] = UiFactory.MakeImage(frame.transform, "ghost", ProceduralSprites.WhiteRect(), Palette.PaperD, C, C, Vector2.zero, new Vector2(394f, 30f));
                hpFill[i] = UiFactory.MakeImage(frame.transform, "fill", ProceduralSprites.WhiteRect(), Palette.Team(i), C, C, Vector2.zero, new Vector2(394f, 30f));
                hpGhost[i].type = Image.Type.Filled; hpGhost[i].fillMethod = Image.FillMethod.Horizontal; hpGhost[i].fillOrigin = me ? 0 : 1; hpGhost[i].fillAmount = 1f;
                hpFill[i].type = Image.Type.Filled; hpFill[i].fillMethod = Image.FillMethod.Horizontal; hpFill[i].fillOrigin = me ? 0 : 1; hpFill[i].fillAmount = 1f;
                hpText[i] = UiFactory.MakeText(frame.transform, "hpText", "100", 26, Palette.PaperL, TextAnchor.MiddleCenter, C, C, Vector2.zero, new Vector2(394f, 30f), true);
                ghostTarget[i] = 1f;
            }

            // ---- 타이머 ----
            timerText = UiFactory.MakeText(root, "timer", "2:00", 72, Palette.Ink, TextAnchor.MiddleCenter, TC, TC, new Vector2(0f, -40f - topInset), new Vector2(200f, 100f));

            // ---- 바람 인디케이터 ----
            RectTransform windRoot = UiFactory.Rect(root, "wind");
            UiFactory.Place(windRoot, TC, TC, new Vector2(0f, -170f - topInset), new Vector2(360f, 100f));
            UiFactory.MakeImage(windRoot, "center", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Ink, 0.4f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(6f, 40f));
            windBarImg = UiFactory.MakeImage(windRoot, "bar", ProceduralSprites.WhiteRect(), Palette.Ink, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -28f), new Vector2(20f, 8f));
            windBar = windBarImg.rectTransform;
            windHeadImg = UiFactory.MakeImage(windRoot, "head", ProceduralSprites.Triangle(), Palette.Ink, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(20f, -28f), new Vector2(28f, 28f));
            windHead = windHeadImg.rectTransform;
            windText = UiFactory.MakeText(windRoot, "windText", "바람 0.0 m/s", 38, Palette.Ink, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(360f, 44f));
            // 다음 바람 미리보기 (회색, 작게) + 변화까지 남은 초
            nextBarImg = UiFactory.MakeImage(windRoot, "nextBar", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Grey, 0.7f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -108f), new Vector2(20f, 5f));
            nextBar = nextBarImg.rectTransform;
            nextHeadImg = UiFactory.MakeImage(windRoot, "nextHead", ProceduralSprites.Triangle(), Palette.WithAlpha(Palette.Grey, 0.7f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(20f, -108f), new Vector2(18f, 18f));
            nextHead = nextHeadImg.rectTransform;
            windNextText = UiFactory.MakeText(windRoot, "windNext", "", 28, Palette.Grey, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(420f, 36f));

            // ---- 호흡 링 ----
            ringRoot = UiFactory.Rect(root, "ring");
            UiFactory.Place(ringRoot, BC, C, new Vector2(0f, 400f), new Vector2(240f, 240f));
            ringTrack = UiFactory.MakeImage(ringRoot, "track", ProceduralSprites.Ring(), Palette.WithAlpha(Palette.Ink, 0.15f), C, C, Vector2.zero, new Vector2(240f, 240f));
            ringGlow = UiFactory.MakeImage(ringRoot, "glow", ProceduralSprites.Ring(), Palette.WithAlpha(Palette.GoldPeak, 0f), C, C, Vector2.zero, new Vector2(240f, 240f));
            ringFill = UiFactory.MakeImage(ringRoot, "fill", ProceduralSprites.Ring(), Palette.Grey, C, C, Vector2.zero, new Vector2(240f, 240f));
            ringFill.type = Image.Type.Filled; ringFill.fillMethod = Image.FillMethod.Radial360; ringFill.fillOrigin = (int)Image.Origin360.Top; ringFill.fillClockwise = true; ringFill.fillAmount = 1f;
            UiFactory.MakeImage(ringRoot, "tick12", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Ink, 0.5f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(6f, 28f));
            UiFactory.MakeImage(ringRoot, "tick6", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Ink, 0.5f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(4f, 20f));
            ringBig = UiFactory.MakeText(ringRoot, "big", "", 56, Palette.Grey, TextAnchor.MiddleCenter, C, C, new Vector2(0f, 14f), new Vector2(200f, 60f), true);
            ringSmall = UiFactory.MakeText(ringRoot, "small", "", 28, Palette.Grey, TextAnchor.MiddleCenter, C, C, new Vector2(0f, -30f), new Vector2(200f, 34f));

            // ---- 추 미터 ----
            meterRoot = UiFactory.Rect(root, "meter");
            UiFactory.Place(meterRoot, BC, C, new Vector2(0f, 150f), new Vector2(800f, 56f));
            meterGroup = meterRoot.gameObject.AddComponent<CanvasGroup>();
            meterGroup.alpha = 0.25f;
            UiFactory.MakeImage(meterRoot, "track", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Ink, 0.12f), C, C, Vector2.zero, new Vector2(800f, 56f));
            meterBorder = UiFactory.MakeImage(meterRoot, "border", ProceduralSprites.RoundedBox(), Palette.Ink, C, C, Vector2.zero, new Vector2(806f, 62f));
            meterBorder.type = Image.Type.Sliced;
            UiFactory.MakeImage(meterRoot, "good", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Gold, 0.15f), C, C, Vector2.zero, new Vector2(320f, 56f));
            float perfectW = 0.24f * 400f; // |p|<0.12 → 96px
            UiFactory.MakeImage(meterRoot, "perfect", ProceduralSprites.WhiteRect(), Palette.WithAlpha(Palette.Gold, 0.85f), C, C, Vector2.zero, new Vector2(perfectW, 56f));
            pendulumString = UiFactory.MakeImage(meterRoot, "string", ProceduralSprites.WhiteRect(), Palette.Ink, C, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(5f, 72f)).rectTransform;
            pendulumImg = UiFactory.MakeImage(meterRoot, "pendulum", ProceduralSprites.Circle(), Palette.Ink, C, C, Vector2.zero, new Vector2(44f, 44f));
            pendulum = pendulumImg.rectTransform;
            autoLine = UiFactory.MakeImage(meterRoot, "autoLine", ProceduralSprites.WhiteRect(), Palette.Ink, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, -8f), new Vector2(800f, 4f)).rectTransform;

            // ---- 조준 텍스트 ----
            aimText = UiFactory.MakeText(root, "aim", "", 36, Palette.Ink, TextAnchor.MiddleCenter, C, C, Vector2.zero, new Vector2(240f, 50f));
            aimRt = aimText.rectTransform;
            aimText.gameObject.SetActive(false);

            // ---- 메시지 / 카운트다운 / 섬광 / 상태 ----
            message = UiFactory.MakeText(root, "message", "", 96, Palette.Gold, TextAnchor.MiddleCenter, C, C, new Vector2(0f, 300f), new Vector2(900f, 120f), true);
            message.gameObject.SetActive(false);
            countdown = UiFactory.MakeText(root, "countdown", "", 220, Palette.Ink, TextAnchor.MiddleCenter, C, C, new Vector2(0f, 200f), new Vector2(600f, 260f), true);
            countdown.gameObject.SetActive(false);
            flash = UiFactory.MakeImage(root, "flash", ProceduralSprites.WhiteRect(), new Color(1f, 1f, 1f, 0f), C, C, Vector2.zero, Vector2.zero);
            UiFactory.Stretch(flash.rectTransform);
            flash.gameObject.SetActive(false);
            statusText = UiFactory.MakeText(root, "status", "", 28, Palette.Grey, TextAnchor.MiddleLeft, BL, BL, new Vector2(40f, 40f), new Vector2(700f, 36f));

            UiFactory.MakeButton(root, "나가기", TR, TR, new Vector2(-40f, -150f - topInset), new Vector2(180f, 70f), false, () => { if (OnExitPressed != null) OnExitPressed(); })
                .GetComponentInChildren<Text>().fontSize = 34;
        }

        public void SetNames(string me, string opponent) { nameText[0].text = me; nameText[1].text = opponent; }
        public void SetStatus(string s) { statusText.text = s; }

        // ---------------------------------------------------------------
        // HP
        // ---------------------------------------------------------------
        public void SetHp(int slot, int hp, int max, bool animate)
        {
            float r = max > 0 ? (float)hp / max : 0f;
            hpFill[slot].fillAmount = r;
            hpText[slot].text = hp.ToString();
            if (!animate) { hpGhost[slot].fillAmount = r; return; }
            if (ghostRoutine[slot] != null) StopCoroutine(ghostRoutine[slot]);
            ghostRoutine[slot] = StartCoroutine(GhostShrink(slot, r));
        }

        private IEnumerator GhostShrink(int slot, float target)
        {
            float t = 0f;
            while (t < ArtConstants.HpGhostDelay) { t += Time.unscaledDeltaTime; yield return null; }
            float from = hpGhost[slot].fillAmount; t = 0f;
            while (t < ArtConstants.HpGhostShrink)
            {
                t += Time.unscaledDeltaTime;
                hpGhost[slot].fillAmount = Mathf.Lerp(from, target, Ease.OutCubic(t / ArtConstants.HpGhostShrink));
                yield return null;
            }
            hpGhost[slot].fillAmount = target;
        }

        // ---------------------------------------------------------------
        // 타이머
        // ---------------------------------------------------------------
        public void SetTimer(float remaining)
        {
            int s = Mathf.CeilToInt(remaining);
            if (s < 0) s = 0;
            timerText.text = (s / 60) + ":" + (s % 60).ToString("00");
            bool warn = s <= 10;
            timerText.color = warn ? Palette.Red : Palette.Ink;
            if (warn && s != lastShownSecond && s > 0)
            {
                StartCoroutine(Pulse(timerText.rectTransform, 1.15f, 0.2f));
                Bow.Audio.BowAudio.I.Play("time_warning", 0.45f, 1f + 0.02f * (10 - s), 1f + 0.02f * (10 - s));
            }
            lastShownSecond = s;
        }

        private IEnumerator Pulse(RectTransform rt, float peak, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = t / dur;
                float s = u < 0.5f ? Mathf.Lerp(1f, peak, Ease.OutBack(u * 2f)) : Mathf.Lerp(peak, 1f, (u - 0.5f) * 2f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        // ---------------------------------------------------------------
        // 바람 (screenWind: 화면 기준 부호, +는 오른쪽)
        // ---------------------------------------------------------------
        /// <param name="screenWind">현재 바람 (화면 기준 부호)</param>
        /// <param name="nextScreenWind">다음 구간 바람</param>
        /// <param name="secondsToChange">변화까지 남은 초</param>
        /// <param name="transitioning">바람 변화 진행 중</param>
        public void SetWind(float screenWind, float nextScreenWind, float secondsToChange, bool transitioning)
        {
            // 다음 바람 미리보기
            float an = Mathf.Abs(nextScreenWind);
            float nextLen = (20f + 120f * Mathf.Min(an / 7f, 1f)) * 0.6f;
            float nsign = nextScreenWind >= 0f ? 1f : -1f;
            nextBar.sizeDelta = new Vector2(nextLen, 5f);
            nextBar.localScale = new Vector3(nsign, 1f, 1f);
            nextHead.anchoredPosition = new Vector2(nextLen * nsign, -108f);
            nextHead.localScale = new Vector3(nsign, 1f, 1f);
            if (transitioning)
            {
                windNextText.text = "바람 변화 중";
                windNextText.color = Palette.Red;
            }
            else
            {
                windNextText.text = "다음 " + an.ToString("F1") + " m/s · " + Mathf.CeilToInt(secondsToChange) + "초 후";
                windNextText.color = secondsToChange <= 3f ? Palette.Red : Palette.Grey;
            }

            float aw = Mathf.Abs(screenWind);
            float targetLen = 20f + 120f * Mathf.Min(aw / 7f, 1f);
            windLenCur = Mathf.Lerp(windLenCur, targetLen, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            float sign = screenWind >= 0f ? 1f : -1f;
            windSignCur = Mathf.MoveTowards(windSignCur, sign, Time.unscaledDeltaTime / 0.12f * 2f);
            windBar.sizeDelta = new Vector2(windLenCur, 8f);
            windBar.localScale = new Vector3(windSignCur, 1f, 1f);
            windHead.anchoredPosition = new Vector2(windLenCur * windSignCur, -28f);
            windHead.localScale = new Vector3(windSignCur, 1f, 1f);
            windText.text = "바람 " + aw.ToString("F1") + " m/s";
            windText.color = aw >= 5f ? Palette.Red : Palette.Ink;
        }

        // ---------------------------------------------------------------
        // 호흡 링 (아트 §4.5 5구간)
        // ---------------------------------------------------------------
        /// <param name="cooldownProgress">장전 진행 0→1</param>
        /// <param name="ready">장전 완료 여부</param>
        /// <param name="overhold">초과 대기 h</param>
        /// <param name="efficiency">E(h)</param>
        /// <param name="nextDelay">지금 쏘면 적용될 다음 딜레이</param>
        public void SetBreath(float cooldownRemaining, float cooldownProgress, bool ready, float overhold, float efficiency, float nextDelay, float peak)
        {
            if (!ready)
            {
                ringFill.fillAmount = 1f - cooldownProgress;
                ringFill.color = Palette.WithAlpha(Palette.Grey, 0.85f);
                ringBig.text = cooldownRemaining.ToString("F1");
                ringBig.color = Palette.Grey;
                ringSmall.text = "장전 중";
                ringSmall.color = Palette.Grey;
                ringWasReady = false; ringWasPeak = false;
                return;
            }
            if (!ringWasReady)
            {
                ringWasReady = true;
                StartCoroutine(Pulse(ringRoot, 1.12f, ArtConstants.RingPulseDuration));
                Bow.Audio.BowAudio.I.Play("reload_ready", 0.5f);
            }
            if (overhold <= peak)
            {
                float h = overhold;
                ringFill.fillAmount = h / peak;
                ringFill.color = h < 7f ? Color.Lerp(Palette.GoldD, Palette.Gold, h / 7f) : Color.Lerp(Palette.Gold, Palette.GoldL, (h - 7f) / 3f);
                bool peakZone = h >= peak - 0.4f;
                if (peakZone && !ringWasPeak)
                {
                    ringWasPeak = true;
                    StartCoroutine(GlowBurst());
                    Bow.Audio.BowAudio.I.Play("breath_peak", 0.6f);
                }
                ringBig.text = peakZone ? "MAX" : h.ToString("F1");
                ringBig.color = peakZone ? Palette.GoldPeak : Palette.Gold;
                ringSmall.text = "효율 " + Mathf.RoundToInt(efficiency * 100f) + "% · 다음 " + nextDelay.ToString("F1") + "s";
                ringSmall.color = Palette.GoldD;
                if (peakZone) { float s = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 0.8f); ringRoot.localScale = new Vector3(s, s, 1f); }
            }
            else
            {
                ringRoot.localScale = Vector3.one;
                float t = Mathf.Clamp01((overhold - peak) / 20f);
                ringFill.fillAmount = efficiency;
                ringFill.color = Color.Lerp(Palette.GoldL, Palette.Grey, t);
                ringBig.text = overhold.ToString("F0");
                ringBig.color = Color.Lerp(Palette.Gold, Palette.Grey, t);
                ringSmall.text = "효율 " + Mathf.RoundToInt(efficiency * 100f) + "% · 다음 " + nextDelay.ToString("F1") + "s";
                ringSmall.color = Palette.Grey;
            }
        }

        private IEnumerator GlowBurst()
        {
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.unscaledDeltaTime;
                float u = Ease.OutCubic(t / 0.45f);
                float s = Mathf.Lerp(1f, 1.5f, u);
                ringGlow.rectTransform.localScale = new Vector3(s, s, 1f);
                ringGlow.color = Palette.WithAlpha(Palette.GoldPeak, Mathf.Lerp(0.8f, 0f, u));
                yield return null;
            }
            ringGlow.color = Palette.WithAlpha(Palette.GoldPeak, 0f);
            ringGlow.rectTransform.localScale = Vector3.one;
            // 정점 금빛 점 6개 방사
            for (int i = 0; i < 6; i++)
            {
                Image dot = UiFactory.MakeImage(ringRoot, "dot", ProceduralSprites.Circle(), Palette.GoldL, C, C, Vector2.zero, new Vector2(10f, 10f));
                StartCoroutine(RadiateDot(dot, i * 60f));
            }
        }

        private IEnumerator RadiateDot(Image dot, float angle)
        {
            float t = 0f;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            while (t < 0.5f)
            {
                t += Time.unscaledDeltaTime;
                float u = Ease.OutCubic(t / 0.5f);
                dot.rectTransform.anchoredPosition = dir * (120f * u);
                dot.color = Palette.WithAlpha(Palette.GoldL, 1f - u);
                yield return null;
            }
            Destroy(dot.gameObject);
        }

        /// <summary>발사 시 링 알파 0.4로 0.1s 페이드 후 복구</summary>
        public void BreathFired()
        {
            ringWasReady = false; ringWasPeak = false;
            ringRoot.localScale = Vector3.one;
            StartCoroutine(RingDip());
        }

        private IEnumerator RingDip()
        {
            CanvasGroup g = ringRoot.GetComponent<CanvasGroup>();
            if (g == null) g = ringRoot.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0.4f;
            float t = 0f;
            while (t < 0.1f) { t += Time.unscaledDeltaTime; yield return null; }
            g.alpha = 1f;
        }

        // ---------------------------------------------------------------
        // 추 미터
        // ---------------------------------------------------------------
        public void SetMeter(bool active, float p, bool perfectNow, float timeoutFraction, float efficiency)
        {
            meterGroup.alpha = active ? 1f : 0.25f;
            float x = p * 400f;
            pendulum.anchoredPosition = new Vector2(x, 0f);
            pendulumString.anchoredPosition = new Vector2(x, 0f);
            pendulumImg.color = perfectNow && active ? Palette.Gold : Palette.Ink;
            float s = perfectNow && active ? 1.15f : 1f;
            pendulum.localScale = new Vector3(s, s, 1f);
            autoLine.sizeDelta = new Vector2(active ? 800f * (1f - timeoutFraction) : 0f, 4f);
            meterBorder.color = Color.Lerp(Palette.Ink, Palette.Gold, efficiency);
        }

        // ---------------------------------------------------------------
        // 조준 텍스트 (월드 → 스크린)
        // ---------------------------------------------------------------
        public void SetAimInfo(bool visible, Vector3 worldPos, float angle, float power)
        {
            aimText.gameObject.SetActive(visible);
            if (!visible) return;
            aimRt.anchoredPosition = WorldToCanvas(worldPos);
            aimText.text = Mathf.RoundToInt(angle) + "° · " + Mathf.RoundToInt(power * 100f) + "%";
        }

        public Vector2 WorldToCanvas(Vector3 world)
        {
            Vector2 sp = cam.WorldToScreenPoint(world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out local);
            return local;
        }

        // ---------------------------------------------------------------
        // 메시지 / 섬광 / 데미지 숫자 / 카운트다운
        // ---------------------------------------------------------------
        public void ShowMessage(string text, Color color, float hold = 0.8f, int size = 96)
        {
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = StartCoroutine(MessageRoutine(text, color, hold, size));
        }

        private IEnumerator MessageRoutine(string text, Color color, float hold, int size)
        {
            message.gameObject.SetActive(true);
            message.text = text; message.fontSize = size;
            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(1.6f, 1f, Ease.OutBack(t / 0.15f));
                message.rectTransform.localScale = new Vector3(s, s, 1f);
                message.color = color;
                yield return null;
            }
            t = 0f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                message.color = Palette.WithAlpha(color, 1f - t / 0.3f);
                yield return null;
            }
            message.gameObject.SetActive(false);
        }

        public void Flash(Color color, float alpha, float duration)
        {
            StartCoroutine(FlashRoutine(color, alpha, duration));
        }

        private IEnumerator FlashRoutine(Color color, float alpha, float dur)
        {
            flash.gameObject.SetActive(true);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                flash.color = Palette.WithAlpha(color, Mathf.Lerp(alpha, 0f, Ease.OutCubic(t / dur)));
                yield return null;
            }
            flash.color = Palette.WithAlpha(color, 0f);
            flash.gameObject.SetActive(false);
        }

        public void ShowDamage(Vector3 worldPos, int damage, Color color, int size)
        {
            Text t = UiFactory.MakeText(root, "dmg", "-" + damage, size, color, TextAnchor.MiddleCenter, C, C, WorldToCanvas(worldPos), new Vector2(240f, 80f), true);
            StartCoroutine(DamageRise(t, color));
        }

        private IEnumerator DamageRise(Text t, Color color)
        {
            Vector2 start = t.rectTransform.anchoredPosition;
            float rise = 1.2f * (Screen.height / (2f * cam.orthographicSize)) * (1920f / Screen.height);
            float e = 0f;
            while (e < ArtConstants.DamageNumberRise)
            {
                e += Time.unscaledDeltaTime;
                float u = e / ArtConstants.DamageNumberRise;
                t.rectTransform.anchoredPosition = start + new Vector2(0f, rise * Ease.OutCubic(u));
                float a = u > 0.625f ? 1f - (u - 0.625f) / 0.375f : 1f;
                t.color = Palette.WithAlpha(color, a);
                yield return null;
            }
            Destroy(t.gameObject);
        }

        public void SetCountdown(string text, bool visible)
        {
            countdown.gameObject.SetActive(visible);
            if (visible && countdown.text != text)
            {
                countdown.text = text;
                StartCoroutine(Pulse(countdown.rectTransform, 1.3f, 0.25f));
            }
        }

        public void SetVisible(bool v) { canvas.gameObject.SetActive(v); }
    }
}

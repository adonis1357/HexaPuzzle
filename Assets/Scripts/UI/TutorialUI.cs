// =====================================================================================
// TutorialUI.cs — 튜토리얼 프로시저럴 UI 컴포넌트
// =====================================================================================
// 외부 에셋 없이 UI.Image + RectTransform으로 모든 튜토리얼 UI를 동적 생성.
// 오버레이, 대화 패널, 손가락 가이드, 스포트라이트, 힌트 배너 등.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 튜토리얼 전용 프로시저럴 UI 시스템
    /// TutorialManager에서 생성/제어
    /// </summary>
    public class TutorialUI : MonoBehaviour
    {
        // ============================================================
        // UI 요소 참조 (프로시저럴 생성됨)
        // ============================================================
        private Canvas canvas;
        private Font font;

        // 딤 오버레이 (반투명 검은 배경)
        private GameObject dimOverlayObj;
        private Image dimOverlayImg;

        // 스포트라이트 (4개 rect로 구멍 효과)
        private GameObject spotlightRoot;
        private Image[] spotlightRects = new Image[4]; // 상,하,좌,우

        // 대화 패널
        private GameObject dialogPanelObj;
        private GameObject dialogBgObj;
        private Text dialogCharText;
        private Text dialogTitleText;
        private Text dialogMsgText;
        private Text tapPromptText;

        // 손가락 가이드
        private GameObject fingerGuideObj;
        private Image fingerGuideImg;

        // 힌트 배너 (상단)
        private GameObject hintBannerObj;
        private Text hintBannerText;

        // 스킵 버튼
        private GameObject skipBtnObj;
        private Button skipBtn;

        // 탭 감지용 투명 버튼 (오버레이 위)
        private GameObject tapAreaObj;
        private Button tapAreaBtn;

        // 애니메이션 코루틴 추적
        private Coroutine fingerBounceCoroutine;
        private Coroutine tapBlinkCoroutine;
        private Coroutine hintFadeCoroutine;

        // 콜백
        private Action onTapCallback;
        private Action onSkipCallback;

        // ============================================================
        // 초기화
        // ============================================================

        /// <summary>
        /// 모든 UI 요소를 프로시저럴 생성
        /// </summary>
        public void Initialize(Canvas parentCanvas)
        {
            canvas = parentCanvas;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            CreateDimOverlay();
            CreateSpotlight();
            CreateDialogPanel();
            CreateFingerGuide();
            CreateHintBanner();
            CreateSkipButton();
            CreateTapArea();

            HideAll();
            Debug.Log("[TutorialUI] 초기화 완료");
        }

        // ============================================================
        // UI 요소 생성
        // ============================================================

        private void CreateDimOverlay()
        {
            dimOverlayObj = new GameObject("TutorialDimOverlay");
            dimOverlayObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = dimOverlayObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            dimOverlayImg = dimOverlayObj.AddComponent<Image>();
            dimOverlayImg.color = new Color(0f, 0f, 0f, 0.6f);
            dimOverlayImg.raycastTarget = true; // 터치 차단
        }

        private void CreateSpotlight()
        {
            spotlightRoot = new GameObject("TutorialSpotlight");
            spotlightRoot.transform.SetParent(canvas.transform, false);
            RectTransform rootRt = spotlightRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 4개의 검은 rect로 직사각형 구멍 효과
            string[] names = { "SpotTop", "SpotBottom", "SpotLeft", "SpotRight" };
            for (int i = 0; i < 4; i++)
            {
                GameObject obj = new GameObject(names[i]);
                obj.transform.SetParent(spotlightRoot.transform, false);
                RectTransform srt = obj.AddComponent<RectTransform>();
                srt.anchorMin = Vector2.zero;
                srt.anchorMax = Vector2.one;
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;

                Image img = obj.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.65f);
                img.raycastTarget = true;
                spotlightRects[i] = img;
            }
        }

        private void CreateDialogPanel()
        {
            // 대화 패널 루트 (하단 배치)
            dialogPanelObj = new GameObject("TutorialDialogPanel");
            dialogPanelObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = dialogPanelObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 30f);
            rt.sizeDelta = new Vector2(-60f, 260f); // 양쪽 30px 여백

            // 배경
            dialogBgObj = new GameObject("DialogBg");
            dialogBgObj.transform.SetParent(dialogPanelObj.transform, false);
            RectTransform bgRt = dialogBgObj.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            Image bgImg = dialogBgObj.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.18f, 0.92f);
            bgImg.raycastTarget = false;

            // 테두리 효과 (밝은 선)
            GameObject border = new GameObject("DialogBorder");
            border.transform.SetParent(dialogBgObj.transform, false);
            RectTransform borderRt = border.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-2f, -2f);
            borderRt.offsetMax = new Vector2(2f, 2f);
            Image borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(0.4f, 0.6f, 0.9f, 0.5f);
            borderImg.raycastTarget = false;
            border.transform.SetAsFirstSibling();

            // 캐릭터 이름
            GameObject charObj = new GameObject("DialogCharName");
            charObj.transform.SetParent(dialogPanelObj.transform, false);
            dialogCharText = charObj.AddComponent<Text>();
            dialogCharText.font = font;
            dialogCharText.fontSize = 28;
            dialogCharText.fontStyle = FontStyle.Bold;
            dialogCharText.color = new Color(0.5f, 0.8f, 1f);
            dialogCharText.alignment = TextAnchor.UpperLeft;
            dialogCharText.raycastTarget = false;
            RectTransform charRt = charObj.GetComponent<RectTransform>();
            charRt.anchorMin = new Vector2(0f, 1f);
            charRt.anchorMax = new Vector2(1f, 1f);
            charRt.pivot = new Vector2(0f, 1f);
            charRt.anchoredPosition = new Vector2(24f, -16f);
            charRt.sizeDelta = new Vector2(-48f, 36f);

            // 제목
            GameObject titleObj = new GameObject("DialogTitle");
            titleObj.transform.SetParent(dialogPanelObj.transform, false);
            dialogTitleText = titleObj.AddComponent<Text>();
            dialogTitleText.font = font;
            dialogTitleText.fontSize = 32;
            dialogTitleText.fontStyle = FontStyle.Bold;
            dialogTitleText.color = new Color(1f, 0.9f, 0.4f);
            dialogTitleText.alignment = TextAnchor.UpperLeft;
            dialogTitleText.raycastTarget = false;
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(24f, -56f);
            titleRt.sizeDelta = new Vector2(-48f, 40f);

            // 메시지 본문
            GameObject msgObj = new GameObject("DialogMessage");
            msgObj.transform.SetParent(dialogPanelObj.transform, false);
            dialogMsgText = msgObj.AddComponent<Text>();
            dialogMsgText.font = font;
            dialogMsgText.fontSize = 26;
            dialogMsgText.color = Color.white;
            dialogMsgText.alignment = TextAnchor.UpperLeft;
            dialogMsgText.lineSpacing = 1.2f;
            dialogMsgText.raycastTarget = false;
            RectTransform msgRt = msgObj.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0f, 0f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.offsetMin = new Vector2(24f, 50f);
            msgRt.offsetMax = new Vector2(-24f, -100f);

            // "탭하여 계속" 안내
            GameObject tapObj = new GameObject("TapPrompt");
            tapObj.transform.SetParent(dialogPanelObj.transform, false);
            tapPromptText = tapObj.AddComponent<Text>();
            tapPromptText.font = font;
            tapPromptText.fontSize = 22;
            tapPromptText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            tapPromptText.alignment = TextAnchor.LowerRight;
            tapPromptText.text = "탭하여 계속 ▶";
            tapPromptText.raycastTarget = false;
            RectTransform tapRt = tapObj.GetComponent<RectTransform>();
            tapRt.anchorMin = new Vector2(0f, 0f);
            tapRt.anchorMax = new Vector2(1f, 0f);
            tapRt.pivot = new Vector2(1f, 0f);
            tapRt.anchoredPosition = new Vector2(-24f, 14f);
            tapRt.sizeDelta = new Vector2(-48f, 30f);
        }

        private void CreateFingerGuide()
        {
            fingerGuideObj = new GameObject("TutorialFingerGuide");
            fingerGuideObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = fingerGuideObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80f, 100f);

            // 손가락 모양: 원형(손끝) + 직사각형(손가락)
            GameObject fingerTip = new GameObject("FingerTip");
            fingerTip.transform.SetParent(fingerGuideObj.transform, false);
            fingerGuideImg = fingerTip.AddComponent<Image>();
            fingerGuideImg.color = new Color(1f, 1f, 1f, 0.85f);
            fingerGuideImg.raycastTarget = false;
            RectTransform tipRt = fingerTip.GetComponent<RectTransform>();
            tipRt.anchoredPosition = new Vector2(0f, 10f);
            tipRt.sizeDelta = new Vector2(40f, 40f);

            // 손가락 몸통
            GameObject fingerBody = new GameObject("FingerBody");
            fingerBody.transform.SetParent(fingerGuideObj.transform, false);
            Image bodyImg = fingerBody.AddComponent<Image>();
            bodyImg.color = new Color(1f, 1f, 1f, 0.7f);
            bodyImg.raycastTarget = false;
            RectTransform bodyRt = fingerBody.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(10f, -30f);
            bodyRt.sizeDelta = new Vector2(24f, 50f);

            // 원형 파동 효과
            GameObject ripple = new GameObject("FingerRipple");
            ripple.transform.SetParent(fingerGuideObj.transform, false);
            Image rippleImg = ripple.AddComponent<Image>();
            rippleImg.color = new Color(0.4f, 0.7f, 1f, 0.3f);
            rippleImg.raycastTarget = false;
            RectTransform rippleRt = ripple.GetComponent<RectTransform>();
            rippleRt.anchoredPosition = new Vector2(0f, 10f);
            rippleRt.sizeDelta = new Vector2(70f, 70f);
        }

        private void CreateHintBanner()
        {
            hintBannerObj = new GameObject("TutorialHintBanner");
            hintBannerObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = hintBannerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -80f);
            rt.sizeDelta = new Vector2(-40f, 70f);

            // 배경
            Image bg = hintBannerObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.15f, 0.3f, 0.88f);
            bg.raycastTarget = false;

            // 텍스트
            GameObject textObj = new GameObject("HintText");
            textObj.transform.SetParent(hintBannerObj.transform, false);
            hintBannerText = textObj.AddComponent<Text>();
            hintBannerText.font = font;
            hintBannerText.fontSize = 26;
            hintBannerText.color = new Color(0.9f, 0.95f, 1f);
            hintBannerText.alignment = TextAnchor.MiddleCenter;
            hintBannerText.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 4f);
            textRt.offsetMax = new Vector2(-16f, -4f);
        }

        private void CreateSkipButton()
        {
            skipBtnObj = new GameObject("TutorialSkipBtn");
            skipBtnObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = skipBtnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -30f);
            rt.sizeDelta = new Vector2(120f, 50f);

            Image bg = skipBtnObj.AddComponent<Image>();
            bg.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
            bg.raycastTarget = true;

            skipBtn = skipBtnObj.AddComponent<Button>();
            skipBtn.onClick.AddListener(() => onSkipCallback?.Invoke());

            // 텍스트
            GameObject textObj = new GameObject("SkipText");
            textObj.transform.SetParent(skipBtnObj.transform, false);
            Text skipText = textObj.AddComponent<Text>();
            skipText.font = font;
            skipText.fontSize = 24;
            skipText.text = "건너뛰기";
            skipText.color = new Color(0.8f, 0.8f, 0.8f);
            skipText.alignment = TextAnchor.MiddleCenter;
            skipText.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }

        private void CreateTapArea()
        {
            tapAreaObj = new GameObject("TutorialTapArea");
            tapAreaObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = tapAreaObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = tapAreaObj.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
            img.raycastTarget = true;

            tapAreaBtn = tapAreaObj.AddComponent<Button>();
            tapAreaBtn.onClick.AddListener(() => onTapCallback?.Invoke());

            // 스킵 버튼이 위에 오도록 순서 조정은 ShowDialog에서 처리
        }

        // ============================================================
        // 표시 메서드
        // ============================================================

        /// <summary>
        /// 대화 팝업 표시
        /// </summary>
        public void ShowDialog(string character, string title, string message, bool showTapPrompt, Action onTap)
        {
            onTapCallback = onTap;

            // 텍스트 설정
            dialogCharText.text = string.IsNullOrEmpty(character) ? "" : character;
            dialogCharText.gameObject.SetActive(!string.IsNullOrEmpty(character));
            dialogTitleText.text = string.IsNullOrEmpty(title) ? "" : title;
            dialogTitleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            dialogMsgText.text = message;
            tapPromptText.gameObject.SetActive(showTapPrompt);

            // 제목 없을 때 메시지 위치 조정
            RectTransform msgRt = dialogMsgText.GetComponent<RectTransform>();
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(character))
                msgRt.offsetMax = new Vector2(-24f, -20f);
            else if (string.IsNullOrEmpty(title))
                msgRt.offsetMax = new Vector2(-24f, -56f);
            else
                msgRt.offsetMax = new Vector2(-24f, -100f);

            // 표시
            dimOverlayObj.SetActive(true);
            dialogPanelObj.SetActive(true);
            tapAreaObj.SetActive(showTapPrompt);
            skipBtnObj.SetActive(true);

            // 순서 보장 (오버레이 < 대화 < 탭영역 < 스킵)
            dimOverlayObj.transform.SetAsLastSibling();
            dialogPanelObj.transform.SetAsLastSibling();
            tapAreaObj.transform.SetAsLastSibling();
            skipBtnObj.transform.SetAsLastSibling();

            // 탭 깜빡임 애니메이션
            if (showTapPrompt)
            {
                if (tapBlinkCoroutine != null) StopCoroutine(tapBlinkCoroutine);
                tapBlinkCoroutine = StartCoroutine(AnimateTapPromptBlink());
            }

            // 슬라이드 인 애니메이션
            StartCoroutine(AnimateDialogIn());
        }

        /// <summary>
        /// 대화 팝업 숨기기
        /// </summary>
        public void HideDialog()
        {
            if (tapBlinkCoroutine != null) { StopCoroutine(tapBlinkCoroutine); tapBlinkCoroutine = null; }
            dialogPanelObj.SetActive(false);
            dimOverlayObj.SetActive(false);
            tapAreaObj.SetActive(false);
        }

        /// <summary>
        /// 스포트라이트 하이라이트 표시 (직사각형 구멍)
        /// </summary>
        public void ShowHighlight(Vector2 canvasPos, float radius)
        {
            spotlightRoot.SetActive(true);

            // Canvas 기준 좌표에서 직사각형 구멍 위치 계산
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            float canvasW = scaler != null ? scaler.referenceResolution.x : 1080f;
            float canvasH = scaler != null ? scaler.referenceResolution.y : 1920f;

            float halfW = canvasW / 2f;
            float halfH = canvasH / 2f;

            // canvasPos는 Canvas 중앙 기준 → 절대 좌표로 변환
            float cx = halfW + canvasPos.x;
            float cy = halfH + canvasPos.y;

            float left = cx - radius;
            float right = cx + radius;
            float bottom = cy - radius;
            float top = cy + radius;

            // 4개 rect 배치 (상, 하, 좌, 우) — Anchored 좌표계
            // 상단: 구멍 위
            SetSpotlightRect(spotlightRects[0], 0, top, canvasW, canvasH - top);
            // 하단: 구멍 아래
            SetSpotlightRect(spotlightRects[1], 0, 0, canvasW, bottom);
            // 좌측: 구멍 왼쪽 (상하단 제외 높이)
            SetSpotlightRect(spotlightRects[2], 0, bottom, left, top - bottom);
            // 우측: 구멍 오른쪽
            SetSpotlightRect(spotlightRects[3], right, bottom, canvasW - right, top - bottom);

            spotlightRoot.transform.SetAsLastSibling();
        }

        private void SetSpotlightRect(Image img, float x, float y, float w, float h)
        {
            RectTransform rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
        }

        /// <summary>
        /// 스포트라이트 숨기기
        /// </summary>
        public void HideHighlight()
        {
            spotlightRoot.SetActive(false);
        }

        /// <summary>
        /// 손가락 가이드 표시
        /// </summary>
        public void ShowFingerGuide(Vector2 canvasPos)
        {
            fingerGuideObj.SetActive(true);
            RectTransform rt = fingerGuideObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = canvasPos;

            fingerGuideObj.transform.SetAsLastSibling();

            if (fingerBounceCoroutine != null) StopCoroutine(fingerBounceCoroutine);
            fingerBounceCoroutine = StartCoroutine(AnimateFingerBounce());
        }

        /// <summary>
        /// 손가락 가이드 숨기기
        /// </summary>
        public void HideFingerGuide()
        {
            if (fingerBounceCoroutine != null) { StopCoroutine(fingerBounceCoroutine); fingerBounceCoroutine = null; }
            fingerGuideObj.SetActive(false);
        }

        /// <summary>
        /// 상단 힌트 배너 표시 (자동 소멸)
        /// </summary>
        public void ShowHintBanner(string message, float duration = 4f)
        {
            hintBannerText.text = message;
            hintBannerObj.SetActive(true);
            hintBannerObj.transform.SetAsLastSibling();

            if (hintFadeCoroutine != null) StopCoroutine(hintFadeCoroutine);
            hintFadeCoroutine = StartCoroutine(AnimateHintBanner(duration));
        }

        /// <summary>
        /// 힌트 배너 숨기기
        /// </summary>
        public void HideHintBanner()
        {
            if (hintFadeCoroutine != null) { StopCoroutine(hintFadeCoroutine); hintFadeCoroutine = null; }
            hintBannerObj.SetActive(false);
        }

        /// <summary>
        /// 스킵 버튼 표시
        /// </summary>
        public void ShowSkipButton(Action onSkip)
        {
            onSkipCallback = onSkip;
            skipBtnObj.SetActive(true);
            skipBtnObj.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 모든 UI 숨기기
        /// </summary>
        public void HideAll()
        {
            if (dimOverlayObj != null) dimOverlayObj.SetActive(false);
            if (spotlightRoot != null) spotlightRoot.SetActive(false);
            if (dialogPanelObj != null) dialogPanelObj.SetActive(false);
            if (fingerGuideObj != null) fingerGuideObj.SetActive(false);
            if (hintBannerObj != null) hintBannerObj.SetActive(false);
            if (skipBtnObj != null) skipBtnObj.SetActive(false);
            if (tapAreaObj != null) tapAreaObj.SetActive(false);

            if (fingerBounceCoroutine != null) { StopCoroutine(fingerBounceCoroutine); fingerBounceCoroutine = null; }
            if (tapBlinkCoroutine != null) { StopCoroutine(tapBlinkCoroutine); tapBlinkCoroutine = null; }
            if (hintFadeCoroutine != null) { StopCoroutine(hintFadeCoroutine); hintFadeCoroutine = null; }
        }

        /// <summary>
        /// 모든 GO 파괴
        /// </summary>
        public void Cleanup()
        {
            HideAll();
            if (dimOverlayObj != null) Destroy(dimOverlayObj);
            if (spotlightRoot != null) Destroy(spotlightRoot);
            if (dialogPanelObj != null) Destroy(dialogPanelObj);
            if (fingerGuideObj != null) Destroy(fingerGuideObj);
            if (hintBannerObj != null) Destroy(hintBannerObj);
            if (skipBtnObj != null) Destroy(skipBtnObj);
            if (tapAreaObj != null) Destroy(tapAreaObj);
        }

        /// <summary>
        /// 딤 오버레이만 끄기 (ForcedAction 등에서 사용)
        /// </summary>
        public void SetDimOverlayActive(bool active)
        {
            if (dimOverlayObj != null) dimOverlayObj.SetActive(active);
        }

        /// <summary>
        /// 탭 영역 활성화/비활성화
        /// </summary>
        public void SetTapAreaActive(bool active, Action onTap = null)
        {
            if (onTap != null) onTapCallback = onTap;
            if (tapAreaObj != null) tapAreaObj.SetActive(active);
        }

        // ============================================================
        // 애니메이션 코루틴
        // ============================================================

        private IEnumerator AnimateDialogIn()
        {
            RectTransform rt = dialogPanelObj.GetComponent<RectTransform>();
            float startY = -300f;
            float endY = 30f;
            float duration = 0.3f;
            float elapsed = 0f;

            rt.anchoredPosition = new Vector2(0f, startY);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseOutBack
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, eased));
                yield return null;
            }
            rt.anchoredPosition = new Vector2(0f, endY);
        }

        private IEnumerator AnimateDialogOut()
        {
            RectTransform rt = dialogPanelObj.GetComponent<RectTransform>();
            float startY = rt.anchoredPosition.y;
            float endY = -300f;
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t; // EaseIn
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, eased));
                yield return null;
            }
            dialogPanelObj.SetActive(false);
        }

        private IEnumerator AnimateFingerBounce()
        {
            RectTransform rt = fingerGuideObj.GetComponent<RectTransform>();
            Vector2 basePos = rt.anchoredPosition;
            float bounceHeight = 20f;
            float speed = 2.5f;

            while (true)
            {
                float offset = Mathf.Sin(Time.unscaledTime * speed * Mathf.PI) * bounceHeight;
                rt.anchoredPosition = basePos + new Vector2(0f, offset);
                yield return null;
            }
        }

        private IEnumerator AnimateTapPromptBlink()
        {
            while (true)
            {
                float alpha = 0.4f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                if (tapPromptText != null)
                    tapPromptText.color = new Color(0.7f, 0.7f, 0.7f, alpha);
                yield return null;
            }
        }

        private IEnumerator AnimateHintBanner(float duration)
        {
            // 슬라이드 인
            RectTransform rt = hintBannerObj.GetComponent<RectTransform>();
            float startY = 40f;
            float endY = -80f;
            float animTime = 0.3f;
            float elapsed = 0f;

            rt.anchoredPosition = new Vector2(0f, startY);
            while (elapsed < animTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animTime);
                float eased = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
                rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, endY, eased));
                yield return null;
            }

            // 유지
            yield return new WaitForSecondsRealtime(duration);

            // 페이드 아웃
            Image bg = hintBannerObj.GetComponent<Image>();
            Color origBg = bg.color;
            Color origText = hintBannerText.color;
            float fadeTime = 0.5f;
            elapsed = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeTime);
                bg.color = new Color(origBg.r, origBg.g, origBg.b, origBg.a * (1f - t));
                hintBannerText.color = new Color(origText.r, origText.g, origText.b, origText.a * (1f - t));
                yield return null;
            }

            hintBannerObj.SetActive(false);
            // 색상 복원
            bg.color = origBg;
            hintBannerText.color = origText;
        }
    }
}

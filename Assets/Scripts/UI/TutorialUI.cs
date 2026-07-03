// =====================================================================================
// TutorialUI.cs — 튜토리얼 프로시저럴 UI 컴포넌트
// =====================================================================================
// 외부 에셋 없이 UI.Image + RectTransform으로 모든 튜토리얼 UI를 동적 생성.
// 오버레이, 대화 패널, 손가락 가이드, 스포트라이트(몰딩 전환), 힌트 배너 등.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Text;
using JewelsHexaPuzzle.Utils;

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

        // 딤 오버레이 (반투명 검은 배경 — 하이라이트 없을 때만 사용)
        private GameObject dimOverlayObj;
        private Image dimOverlayImg;

        // 스포트라이트: 4개 검은 rect로 "구멍" 효과 + 글로우 테두리
        private GameObject spotlightRoot;
        private Image[] spotlightRects = new Image[4]; // 상,하,좌,우
        private GameObject spotlightGlow; // 구멍 테두리 밝은 링
        private Image spotlightGlowImg;

        // 대화 패널 (하단 — 일반 설명용)
        private GameObject dialogPanelObj;
        private GameObject dialogBgObj;
        private Text dialogCharText;
        private Text dialogTitleText;
        private Text dialogMsgText;
        private Text tapPromptText;
        private GameObject dialogIconObj;       // 왼쪽 아이콘 (해금 인트로용, 기본 비활성)
        private Image dialogIconImg;
        private GameObject dialogPortraitObj;   // 화자(엘라시온) 초상화 — 대화 패널 위로 솟은 흉상
        private Image dialogPortraitImg;
        private bool dialogHasCharacter;        // 현재 대화에 화자명이 있는지 (초상화 표시 조건)

        // 화자 초상화 (Resources/UI/portrait_elasion) — 대화 패널 흉상용 (허리까지)
        private static Sprite cachedNarratorPortrait;
        private static bool narratorPortraitLoadTried;
        // 화자 전신 초상화 (Resources/UI/portrait_elasion_full) — 말풍선용 (화면 가장자리 전신)
        private static Sprite cachedNarratorFullBody;
        private static bool narratorFullBodyLoadTried;

        // 말풍선 (UI 하이라이트 설명용)
        private GameObject speechBubbleObj;
        private Image speechBubbleBgImg;
        private GameObject speechBubbleTailObj;
        private Image speechBubbleTailImg;
        private Text speechCharText;
        private Text speechTitleText;
        private Text speechMsgText;
        private Text speechTapText;
        private GameObject speechPortraitObj;   // 화자 초상화 흉상 (말풍선 좌상단 코너)
        private Image speechPortraitImg;
        private Coroutine speechBubbleMoveCoroutine;
        private Coroutine speechTapBlinkCoroutine;
        private static Sprite cachedSpeechBubbleSprite;
        private static Sprite cachedSpeechTailSprite;
        private const float SPEECH_BUBBLE_W = 560f;
        private const float SPEECH_BUBBLE_H = 230f;
        private const float SPEECH_GAP = 40f; // 타겟과 말풍선 간격

        // 손가락 가이드
        private GameObject fingerGuideObj;
        private Image fingerGuideImg;

        // 드래그 연결선 (스왑 튜토리얼용)
        private GameObject dragLineObj;
        private Image dragLineImg;
        private Coroutine dragLinePulseCoroutine;

        // 힌트 배너 (상단)
        private GameObject hintBannerObj;
        private Text hintBannerText;

        // 하단 가이드 배너 (ForcedAction 중 지속 표시)
        private GameObject bottomHintObj;
        private Text bottomHintText;

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
        private Coroutine highlightMorphCoroutine;
        private Coroutine glowPulseCoroutine;

        // 스포트라이트 어두운 영역 클릭 시 호출 (튜토리얼 잘못 클릭 토스트용)
        private Action spotlightClickCallback = null;

        /// <summary>스포트라이트 어두운 영역 클릭 시 호출될 콜백 등록 (null로 해제).</summary>
        public void SetSpotlightClickCallback(Action onClick)
        {
            spotlightClickCallback = onClick;
        }

        // 하이라이트 상태 (Canvas 절대 좌표계, 좌하단=0,0)
        private Rect currentHighlightRect;
        private bool hasActiveHighlight = false;
        private const float HIGHLIGHT_MORPH_DURATION = 0.38f;
        private const float HIGHLIGHT_FADE_DURATION = 0.22f;
        private const float SPOTLIGHT_DIM_ALPHA = 0.72f;

        // 콜백
        private Action onTapCallback;

        // ============================================================
        // ★ 타자기 효과 (typewriter) — 텍스트를 한 글자씩 순차 표시
        //   사용자 입력 처리:
        //     - 타이핑 중 첫 탭 → 3배 빠르게
        //     - 타이핑 중 두 번째 탭 → 즉시 전체 표시
        //     - 완료 후 1초 동안 잠금 (다음 단계 진행 차단)
        //     - 1초 경과 후 탭 → 다음 단계 진행 (onTapCallback)
        // ============================================================
        private const float TYPEWRITER_NORMAL_CPS = 25f;     // 보통 읽는 속도 (chars per second)
        private const float TYPEWRITER_FAST_MULTIPLIER = 3f; // 탭 시 3배 가속
        private const float TYPEWRITER_LOCK_AFTER_COMPLETE = 1.0f; // 완료 후 1초 동안 advance 잠금

        private Coroutine typewriterCo;
        private Text typewriterTarget;
        private string typewriterFullText;
        private bool typewriterFastMode;          // 첫 탭 후 true (3배 속도)
        private bool typewriterComplete;          // 모두 표시되었는지
        private bool typewriterInstantComplete;   // 두 번째 탭으로 즉시 완료 요청
        private float typewriterCompleteRealTime; // 완료된 시점의 realtime (1초 락 측정)
        private System.Action onTypewriterAdvanceReady; // 1초 락 만료 시 호출 (탭 안내 표시 등)

        // 사전 파싱된 공개 단위
        //   - 일반 글자: 한 단위 = 한 글자
        //   - 색상/리치 태그가 둘러싼 영역: 한 단위 = 그 영역 전체 (한 번에 출력 → 코드 노출 없음)
        private string[] typewriterUnitContents;
        private bool[] typewriterUnitIsStyled;
        private int typewriterTotalUnits;

        /// <summary>타자기 코루틴 시작 — target에 fullText를 한 글자씩 표시.
        /// onAdvanceReady: 텍스트 완전 표시 + 1초 락 경과 후 호출 (탭 안내 활성화 등)
        /// </summary>
        private void StartTypewriter(Text target, string fullText, System.Action onAdvanceReady = null)
        {
            if (typewriterCo != null) { StopCoroutine(typewriterCo); typewriterCo = null; }
            typewriterTarget = target;
            typewriterFullText = fullText ?? "";
            typewriterFastMode = false;
            typewriterComplete = false;
            typewriterInstantComplete = false;
            typewriterCompleteRealTime = 0f;
            onTypewriterAdvanceReady = onAdvanceReady;

            // ★ 텍스트를 "공개 단위" 배열로 사전 파싱 (색상/리치 영역은 통째로 한 단위)
            ParseTypewriterUnits(typewriterFullText,
                out typewriterUnitContents, out typewriterUnitIsStyled);
            typewriterTotalUnits = typewriterUnitContents.Length;

            if (target != null)
            {
                // ★ 방어: supportRichText 강제 활성화 — false였다면 마크업이 그대로 텍스트로 노출됨
                target.supportRichText = true;
                // 시작 시 풀 텍스트 레이아웃 + 전부 투명 → 첫 글자가 등장할 때 Y/X 흔들림 없음
                target.text = BuildTypewriterText(0);
            }
            typewriterCo = StartCoroutine(TypewriterCoroutine());
        }

        private IEnumerator TypewriterCoroutine()
        {
            // ★ 단위 기반 진행: 일반 글자는 1단위/틱, 색상 영역은 통째로 1단위/틱
            //   → 색상 영역은 항상 한 번에 등장 (마크업 코드가 절대 노출되지 않음)
            int unitsRevealed = 0;

            while (unitsRevealed < typewriterTotalUnits && !typewriterInstantComplete)
            {
                unitsRevealed++;
                if (typewriterTarget != null)
                    typewriterTarget.text = BuildTypewriterText(unitsRevealed);
                float cps = typewriterFastMode
                    ? TYPEWRITER_NORMAL_CPS * TYPEWRITER_FAST_MULTIPLIER
                    : TYPEWRITER_NORMAL_CPS;
                yield return new WaitForSecondsRealtime(1f / cps);
            }
            // 자연 완료 또는 즉시 완료 처리 — 풀 텍스트 (색상 적용된 최종 형태)
            if (typewriterTarget != null) typewriterTarget.text = typewriterFullText;
            typewriterComplete = true;
            typewriterCompleteRealTime = Time.realtimeSinceStartup;

            // ★ 1초 락 후 advance ready 콜백 — 탭 안내 활성화 트리거
            yield return new WaitForSecondsRealtime(TYPEWRITER_LOCK_AFTER_COMPLETE);
            var cb = onTypewriterAdvanceReady;
            onTypewriterAdvanceReady = null;
            cb?.Invoke();
            typewriterCo = null;
        }

        /// <summary>
        /// 텍스트를 "공개 단위(unit)" 배열로 사전 파싱.
        ///   - 일반 글자: 한 글자 = 한 단위 (isStyled = false)
        ///   - 리치 태그(&lt;color=...&gt;, &lt;b&gt; 등)로 감싼 영역: 그 영역 전체 = 한 단위 (isStyled = true)
        ///     → 마크업 + 안의 글자 + 닫는 태그까지 통째로 한 번에 등장
        ///     → "&lt;c&gt;ole&lt;/c&gt;" 같은 코드가 한 글자씩 노출되는 일이 절대 없음
        ///   중첩 태그(&lt;color&gt;&lt;b&gt;...&lt;/b&gt;&lt;/color&gt;)는 최외곽 깊이가 0이 될 때까지 하나의 단위로 묶임
        /// </summary>
        private static void ParseTypewriterUnits(string text, out string[] contents, out bool[] isStyled)
        {
            if (string.IsNullOrEmpty(text))
            {
                contents = new string[0];
                isStyled = new bool[0];
                return;
            }

            var contentList = new System.Collections.Generic.List<string>(text.Length);
            var styledList = new System.Collections.Generic.List<bool>(text.Length);
            int n = text.Length;
            int i = 0;
            int tagDepth = 0;
            var chunkSb = new StringBuilder();

            while (i < n)
            {
                char ch = text[i];
                if (ch == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end > i)
                    {
                        string tag = text.Substring(i, end - i + 1);
                        bool isClosing = tag.Length >= 2 && tag[1] == '/';

                        if (tagDepth == 0 && !isClosing)
                        {
                            // 새 색상/스타일 영역 시작
                            tagDepth = 1;
                            chunkSb.Clear();
                            chunkSb.Append(tag);
                        }
                        else if (tagDepth > 0)
                        {
                            chunkSb.Append(tag);
                            if (isClosing) tagDepth--;
                            else tagDepth++;
                            if (tagDepth == 0)
                            {
                                // 영역 종료 → 한 단위로 추가
                                contentList.Add(chunkSb.ToString());
                                styledList.Add(true);
                                chunkSb.Clear();
                            }
                        }
                        else
                        {
                            // 스탠드얼론 닫는 태그 (비정상) — 그냥 1단위로
                            contentList.Add(tag);
                            styledList.Add(false);
                        }
                        i = end + 1;
                        continue;
                    }
                }

                if (tagDepth > 0)
                {
                    chunkSb.Append(ch);
                }
                else
                {
                    contentList.Add(ch.ToString());
                    styledList.Add(false);
                }
                i++;
            }

            // 미완료 chunk(닫는 태그 누락) — 안전을 위해 한 단위로 처리
            if (chunkSb.Length > 0)
            {
                contentList.Add(chunkSb.ToString());
                styledList.Add(true);
            }

            contents = contentList.ToArray();
            isStyled = styledList.ToArray();
        }

        /// <summary>
        /// unitsRevealed 만큼의 단위를 공개하고, 나머지는 투명 wrap으로 자리만 차지.
        ///   - 풀 레이아웃이 처음부터 잡혀 있어 멀티라인 시 Y 흔들림 없음
        ///   - 색상 영역은 단위 단위로 통째 출력되므로 마크업이 시각적으로 노출되지 않음
        /// </summary>
        private string BuildTypewriterText(int unitsRevealed)
        {
            if (typewriterUnitContents == null || typewriterTotalUnits == 0) return "";

            var sb = new StringBuilder(typewriterFullText.Length + 32);
            int show = Mathf.Clamp(unitsRevealed, 0, typewriterTotalUnits);

            // 1) 공개된 단위 — 그대로 (스타일/마크업 포함)
            for (int u = 0; u < show; u++)
                sb.Append(typewriterUnitContents[u]);

            // 2) 미공개 단위 — 마크업 제거 후 투명 wrap (자리만 차지, 시각적으로 안 보임)
            if (show < typewriterTotalUnits)
            {
                sb.Append("<color=#00000000>");
                for (int u = show; u < typewriterTotalUnits; u++)
                {
                    if (typewriterUnitIsStyled[u])
                        AppendStripped(sb, typewriterUnitContents[u]);
                    else
                        sb.Append(typewriterUnitContents[u]);
                }
                sb.Append("</color>");
            }

            return sb.ToString();
        }

        /// <summary>StringBuilder에 텍스트의 마크업(&lt;...&gt;)을 제거한 결과만 append.</summary>
        private static void AppendStripped(StringBuilder sb, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            int n = text.Length;
            int i = 0;
            while (i < n)
            {
                char ch = text[i];
                if (ch == '<')
                {
                    int end = text.IndexOf('>', i);
                    if (end > i) { i = end + 1; continue; }
                }
                sb.Append(ch);
                i++;
            }
        }

        /// <summary>탭 입력 처리 — 타이핑 중이면 가속/즉시 완료, 완료 후엔 false 반환 (외부 onTap 호출 안 함).</summary>
        /// <returns>탭이 다음 단계 진행을 의미하면 true, 타자기 내부에서만 처리되었으면 false</returns>
        private bool HandleTypewriterTap()
        {
            if (!typewriterComplete)
            {
                if (!typewriterFastMode)
                {
                    // 첫 탭 → 3배 속도
                    typewriterFastMode = true;
                }
                else
                {
                    // 두 번째 탭 → 즉시 완료 (코루틴이 다음 yield에서 while 탈출 → 1초 락 진입)
                    typewriterInstantComplete = true;
                }
                return false; // advance 차단
            }

            // 완료 후 1초 잠금 검사
            if (Time.realtimeSinceStartup - typewriterCompleteRealTime < TYPEWRITER_LOCK_AFTER_COMPLETE)
                return false; // 잠금 중 — advance 차단

            return true; // 진행 가능
        }

        /// <summary>외부에서 타자기 상태가 advance 가능한지 확인 (수동 체크용)</summary>
        public bool IsTypewriterReadyToAdvance()
        {
            if (!typewriterComplete) return false;
            return Time.realtimeSinceStartup - typewriterCompleteRealTime >= TYPEWRITER_LOCK_AFTER_COMPLETE;
        }

        /// <summary>onTap 콜백을 타자기-인식 핸들러로 래핑.
        /// 사용법: onTapCallback = WrapTapWithTypewriter(originalOnTap);
        /// </summary>
        private Action WrapTapWithTypewriter(Action originalOnTap)
        {
            return () =>
            {
                bool shouldAdvance = HandleTypewriterTap();
                if (shouldAdvance) originalOnTap?.Invoke();
            };
        }
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
            CreateSpeechBubble();
            CreateFingerGuide();
            CreateHintBanner();
            CreateBottomHint();   // ★ 하단 힌트 UI 생성 (useBottomHint=true 스텝에서 사용)
            CreateSkipButton();
            CreateTapArea();

            HideAll();
            Debug.Log("[TutorialUI] 초기화 완료");
        }

        // ============================================================
        // UI 요소 생성
        // ============================================================

        /// <summary>
        /// 튜토리얼 텍스트 가독성 보장용 아웃라인 추가 헬퍼.
        /// 리치 텍스트 색상(밝은 청록/연두/파랑 등)이 배경과 섞여 안 보이는 것 방지.
        /// 어두운 배경용: 검정 아웃라인 / 밝은 배경(말풍선)용: 흰색 아웃라인.
        /// </summary>
        private static void AddTextOutline(GameObject textObj, Color color, Vector2 distance)
        {
            if (textObj == null) return;
            var outline = textObj.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = textObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

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
            dimOverlayImg.color = ClaudeTheme.Overlay; // 따뜻한 딤
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
                srt.anchorMax = Vector2.zero;
                srt.pivot = Vector2.zero;

                Image img = obj.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, SPOTLIGHT_DIM_ALPHA);
                img.raycastTarget = true;
                spotlightRects[i] = img;

                // 스포트라이트 어두운 영역 클릭 시 콜백 (튜토리얼에서 잘못 클릭 안내용)
                Button btn = obj.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => spotlightClickCallback?.Invoke());
            }

            // 밝은 테두리 링 (구멍 윤곽 강조 — 몰딩 효과)
            spotlightGlow = new GameObject("SpotGlow");
            spotlightGlow.transform.SetParent(spotlightRoot.transform, false);
            RectTransform grt = spotlightGlow.AddComponent<RectTransform>();
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.zero;
            grt.pivot = new Vector2(0.5f, 0.5f);

            spotlightGlowImg = spotlightGlow.AddComponent<Image>();
            spotlightGlowImg.color = new Color(1f, 0.95f, 0.5f, 0f); // 애니메이션으로 페이드인
            spotlightGlowImg.raycastTarget = false;
            // 외곽선 역할: Outline 컴포넌트 추가
            Outline outline = spotlightGlow.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.95f, 0.5f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
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
            bgImg.sprite = ClaudeTheme.DarkPanelSprite;   // 다크 차콜 패널 (밝은 리치텍스트 가독)
            bgImg.type = Image.Type.Sliced;
            bgImg.color = ClaudeTheme.DarkSurface;
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
            borderImg.sprite = ClaudeTheme.PanelSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(ClaudeTheme.Coral.r, ClaudeTheme.Coral.g, ClaudeTheme.Coral.b, 0.22f); // 코랄 소프트 헤일로
            borderImg.raycastTarget = false;
            border.transform.SetAsFirstSibling();

            // 캐릭터 이름
            GameObject charObj = new GameObject("DialogCharName");
            charObj.transform.SetParent(dialogPanelObj.transform, false);
            dialogCharText = charObj.AddComponent<Text>();
            dialogCharText.font = font;
            dialogCharText.fontSize = 28;
            dialogCharText.fontStyle = FontStyle.Bold;
            dialogCharText.color = ClaudeTheme.Coral; // 코랄 (다크 위 가독)
            dialogCharText.alignment = TextAnchor.UpperLeft;
            dialogCharText.raycastTarget = false;
            dialogCharText.supportRichText = false; // <이름> 꺾쇠를 리치 태그로 파싱하지 않도록
            AddTextOutline(charObj, new Color(0f, 0f, 0f, 0.6f), new Vector2(1.4f, -1.4f));
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
            dialogTitleText.color = ClaudeTheme.TitleWarm; // 따뜻한 골드 (다크 위)
            dialogTitleText.alignment = TextAnchor.UpperLeft;
            dialogTitleText.raycastTarget = false;
            AddTextOutline(titleObj, new Color(0f, 0f, 0f, 0.6f), new Vector2(1.4f, -1.4f));
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
            dialogMsgText.color = ClaudeTheme.TextLight; // 크림 본문 (다크 위)
            dialogMsgText.alignment = TextAnchor.UpperLeft;
            dialogMsgText.lineSpacing = 1.2f;
            dialogMsgText.raycastTarget = false;
            AddTextOutline(msgObj, new Color(0f, 0f, 0f, 0.6f), new Vector2(1.4f, -1.4f));
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
            tapPromptText.color = new Color(0.82f, 0.80f, 0.76f, 0.85f); // 다크 위 밝은 회색
            tapPromptText.alignment = TextAnchor.LowerRight;
            tapPromptText.text = "탭하여 계속 ▶";
            tapPromptText.raycastTarget = false;
            RectTransform tapRt = tapObj.GetComponent<RectTransform>();
            tapRt.anchorMin = new Vector2(0f, 0f);
            tapRt.anchorMax = new Vector2(1f, 0f);
            tapRt.pivot = new Vector2(1f, 0f);
            tapRt.anchoredPosition = new Vector2(-24f, 14f);
            tapRt.sizeDelta = new Vector2(-48f, 30f);

            // 왼쪽 아이콘 (해금 인트로용 — 기본 비활성)
            // 레이아웃: 캐릭터명(상단 헤더) 아래, 타이틀+메시지 블록과 수직 평행
            // 아이콘 상단(y=200) ≈ 타이틀 상단(y=204), 아이콘 하단(y=50) = 메시지 하단(y=50)
            dialogIconObj = new GameObject("DialogIcon");
            dialogIconObj.transform.SetParent(dialogPanelObj.transform, false);
            RectTransform iconRt = dialogIconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0f);
            iconRt.anchorMax = new Vector2(0f, 0f);
            iconRt.pivot = new Vector2(0f, 0f);
            iconRt.anchoredPosition = new Vector2(24f, 50f);
            iconRt.sizeDelta = new Vector2(150f, 150f);
            dialogIconImg = dialogIconObj.AddComponent<Image>();
            dialogIconImg.raycastTarget = false;
            dialogIconImg.preserveAspect = true;
            dialogIconObj.SetActive(false);

            // 화자 초상화 (엘라시온) — 대화 패널 좌측 상단에서 위로 솟은 상반신 흉상.
            //   PNG는 상반신만 크롭(머리~상체)되어 있고 하단은 알파 페이드 → 박스 위에 자연스럽게 얹힘.
            //   왼쪽 배치 → 정방향(오른쪽/정면을 봄, 중앙을 향함). characterName이 있을 때만 활성화.
            dialogPortraitObj = new GameObject("DialogPortrait");
            dialogPortraitObj.transform.SetParent(dialogPanelObj.transform, false);
            RectTransform portraitRt = dialogPortraitObj.AddComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0f, 1f);   // 패널 좌상단 기준
            portraitRt.anchorMax = new Vector2(0f, 1f);
            portraitRt.pivot = new Vector2(0f, 0f);        // 흉상 하단-좌측이 기준점
            portraitRt.anchoredPosition = new Vector2(14f, -6f); // 패널 상단에서 위로 솟음(6px 살짝 겹침)
            portraitRt.sizeDelta = new Vector2(288f, 282f); // 허리까지(전신 크롭, 망토 넓음) 비율 ≈ 1.023:1
            dialogPortraitImg = dialogPortraitObj.AddComponent<Image>();
            dialogPortraitImg.raycastTarget = false;
            dialogPortraitImg.preserveAspect = true;
            dialogPortraitImg.sprite = LoadNarratorPortrait();
            dialogPortraitObj.SetActive(false);
        }

        /// <summary>화자(엘라시온) 초상화 스프라이트 로드 (1회 캐시).</summary>
        private static Sprite LoadNarratorPortrait()
        {
            if (cachedNarratorPortrait == null && !narratorPortraitLoadTried)
            {
                narratorPortraitLoadTried = true;
                cachedNarratorPortrait = Resources.Load<Sprite>("UI/portrait_elasion");
                if (cachedNarratorPortrait == null)
                    Debug.LogWarning("[TutorialUI] 초상화 로드 실패: Resources/UI/portrait_elasion");
            }
            return cachedNarratorPortrait;
        }

        /// <summary>화자 전신 초상화 스프라이트 로드 (말풍선용, 1회 캐시).</summary>
        private static Sprite LoadNarratorFullBody()
        {
            if (cachedNarratorFullBody == null && !narratorFullBodyLoadTried)
            {
                narratorFullBodyLoadTried = true;
                cachedNarratorFullBody = Resources.Load<Sprite>("UI/portrait_elasion_full");
                if (cachedNarratorFullBody == null)
                    Debug.LogWarning("[TutorialUI] 전신 초상화 로드 실패: Resources/UI/portrait_elasion_full");
            }
            return cachedNarratorFullBody;
        }

        // ============================================================
        // 말풍선 (UI 하이라이트용 — 타겟 옆에 꼬리 달린 둥근 박스)
        // ============================================================
        private void CreateSpeechBubble()
        {
            speechBubbleObj = new GameObject("TutorialSpeechBubble");
            speechBubbleObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = speechBubbleObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(SPEECH_BUBBLE_W, SPEECH_BUBBLE_H);

            // 배경 (둥근 모서리 프로시저럴) — 진한 네이비 + 시안 테두리로 리치 텍스트 가독성 향상
            speechBubbleBgImg = speechBubbleObj.AddComponent<Image>();
            if (cachedSpeechBubbleSprite == null)
                cachedSpeechBubbleSprite = CreateRoundedRectSprite(
                    (int)SPEECH_BUBBLE_W, (int)SPEECH_BUBBLE_H, 28,
                    ClaudeTheme.DarkSurface,                // 다크 차콜 배경 (밝은 리치텍스트 가독)
                    new Color(ClaudeTheme.Coral.r, ClaudeTheme.Coral.g, ClaudeTheme.Coral.b, 0.85f), // 코랄 테두리
                    4);
            speechBubbleBgImg.sprite = cachedSpeechBubbleSprite;
            speechBubbleBgImg.raycastTarget = true; // 탭 수신

            // 꼬리 (삼각형 — 타겟 방향 회전)
            speechBubbleTailObj = new GameObject("SpeechTail");
            speechBubbleTailObj.transform.SetParent(speechBubbleObj.transform, false);
            RectTransform tailRt = speechBubbleTailObj.AddComponent<RectTransform>();
            tailRt.sizeDelta = new Vector2(42f, 28f);
            tailRt.pivot = new Vector2(0.5f, 1f); // 상단 중앙 (꼬리 밑변이 말풍선에 붙음)

            speechBubbleTailImg = speechBubbleTailObj.AddComponent<Image>();
            if (cachedSpeechTailSprite == null)
                cachedSpeechTailSprite = CreateSpeechTailSprite(
                    42, 28,
                    ClaudeTheme.DarkSurface,                // 배경과 동일한 차콜
                    new Color(ClaudeTheme.Coral.r, ClaudeTheme.Coral.g, ClaudeTheme.Coral.b, 0.85f), // 코랄 테두리
                    4);
            speechBubbleTailImg.sprite = cachedSpeechTailSprite;
            speechBubbleTailImg.raycastTarget = false;

            // 캐릭터 이름 (상단 왼쪽, 파란색)
            GameObject charObj = new GameObject("SpeechCharName");
            charObj.transform.SetParent(speechBubbleObj.transform, false);
            speechCharText = charObj.AddComponent<Text>();
            speechCharText.font = font;
            speechCharText.fontSize = 26;
            speechCharText.fontStyle = FontStyle.Bold;
            speechCharText.color = ClaudeTheme.Coral; // 코랄 (다크 배경용)
            speechCharText.alignment = TextAnchor.UpperLeft;
            speechCharText.raycastTarget = false;
            speechCharText.supportRichText = false; // <이름> 꺾쇠를 리치 태그로 파싱하지 않도록
            AddTextOutline(charObj, new Color(0f, 0f, 0f, 0.6f), new Vector2(1.4f, -1.4f));
            RectTransform charRt = charObj.GetComponent<RectTransform>();
            charRt.anchorMin = new Vector2(0f, 1f);
            charRt.anchorMax = new Vector2(1f, 1f);
            charRt.pivot = new Vector2(0f, 1f);
            charRt.anchoredPosition = new Vector2(22f, -14f);
            charRt.sizeDelta = new Vector2(-44f, 32f);

            // 제목 (굵은 진한 색)
            GameObject titleObj = new GameObject("SpeechTitle");
            titleObj.transform.SetParent(speechBubbleObj.transform, false);
            speechTitleText = titleObj.AddComponent<Text>();
            speechTitleText.font = font;
            speechTitleText.fontSize = 30;
            speechTitleText.fontStyle = FontStyle.Bold;
            speechTitleText.color = ClaudeTheme.TitleWarm; // 따뜻한 골드 (다크 배경용)
            speechTitleText.alignment = TextAnchor.UpperLeft;
            speechTitleText.raycastTarget = false;
            AddTextOutline(titleObj, new Color(0f, 0f, 0f, 0.6f), new Vector2(1.4f, -1.4f));
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(22f, -48f);
            titleRt.sizeDelta = new Vector2(-44f, 38f);

            // 메시지 본문
            GameObject msgObj = new GameObject("SpeechMessage");
            msgObj.transform.SetParent(speechBubbleObj.transform, false);
            speechMsgText = msgObj.AddComponent<Text>();
            speechMsgText.font = font;
            speechMsgText.fontSize = 24;
            speechMsgText.color = ClaudeTheme.TextLight; // 크림 본문 (다크 배경용)
            speechMsgText.alignment = TextAnchor.UpperLeft;
            speechMsgText.lineSpacing = 1.18f;
            speechMsgText.raycastTarget = false;
            AddTextOutline(msgObj, new Color(0f, 0f, 0f, 0.6f), new Vector2(1.4f, -1.4f));
            RectTransform msgRt = msgObj.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0f, 0f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.offsetMin = new Vector2(22f, 44f);
            msgRt.offsetMax = new Vector2(-22f, -90f);

            // "탭하여 계속 ▶" (하단 오른쪽, 작게)
            GameObject tapObj = new GameObject("SpeechTap");
            tapObj.transform.SetParent(speechBubbleObj.transform, false);
            speechTapText = tapObj.AddComponent<Text>();
            speechTapText.font = font;
            speechTapText.fontSize = 20;
            speechTapText.color = new Color(0.82f, 0.80f, 0.76f, 0.85f); // 다크 위 밝은 회색
            speechTapText.alignment = TextAnchor.LowerRight;
            speechTapText.text = "탭하여 계속 ▶";
            speechTapText.raycastTarget = false;
            RectTransform tapRt = tapObj.GetComponent<RectTransform>();
            tapRt.anchorMin = new Vector2(0f, 0f);
            tapRt.anchorMax = new Vector2(1f, 0f);
            tapRt.pivot = new Vector2(1f, 0f);
            tapRt.anchoredPosition = new Vector2(-22f, 12f);
            tapRt.sizeDelta = new Vector2(-44f, 26f);

            // 화자 전신 초상화 — 말풍선과 겹치지 않도록 화면 가장자리(말풍선 반대편)에 세움.
            //   ★ 말풍선이 아니라 Canvas의 자식 → 말풍선 위치와 독립적으로 화면 기준 배치.
            //   pivot=하단 중앙 → localScale.x 반전 시 위치 변동 없이 좌우만 뒤집힘.
            //   크기/위치/반전은 PositionSpeechPortrait()에서 말풍선 화면 위치에 따라 결정.
            speechPortraitObj = new GameObject("SpeechFullBodyPortrait");
            speechPortraitObj.transform.SetParent(canvas.transform, false);
            RectTransform spRt = speechPortraitObj.AddComponent<RectTransform>();
            spRt.pivot = new Vector2(0.5f, 0f);      // 하단 중앙 (반전 대칭, 바닥에 섬)
            speechPortraitImg = speechPortraitObj.AddComponent<Image>();
            speechPortraitImg.raycastTarget = false;
            speechPortraitImg.preserveAspect = true;
            speechPortraitImg.sprite = LoadNarratorFullBody();
            speechPortraitObj.SetActive(false);

            // 말풍선 탭 → 다음 단계
            Button btn = speechBubbleObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onTapCallback?.Invoke());

            speechBubbleObj.SetActive(false);
        }

        private void CreateFingerGuide()
        {
            fingerGuideObj = new GameObject("TutorialFingerGuide");
            fingerGuideObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = fingerGuideObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80f, 100f);

            // ★ 구조 재설계: 손가락이 위에서 아래로 누르는 모양
            //   fingerGuideObj 원점 = 손끝(탭 위치). 몸통은 원점보다 위쪽에 배치.
            //   → ShowFingerGuide(canvasPos) 호출 시 손끝이 정확히 canvasPos에 맞춰짐.

            // 손끝 (contact point — 원점에 배치)
            GameObject fingerTip = new GameObject("FingerTip");
            fingerTip.transform.SetParent(fingerGuideObj.transform, false);
            fingerGuideImg = fingerTip.AddComponent<Image>();
            fingerGuideImg.color = new Color(1f, 1f, 1f, 0.85f);
            fingerGuideImg.raycastTarget = false;
            RectTransform tipRt = fingerTip.GetComponent<RectTransform>();
            tipRt.anchoredPosition = new Vector2(0f, 0f); // 원점 = 탭 위치
            tipRt.sizeDelta = new Vector2(40f, 40f);

            // 손가락 몸통 (손끝 위쪽으로 뻗음 → 위에서 눌러내리는 느낌)
            GameObject fingerBody = new GameObject("FingerBody");
            fingerBody.transform.SetParent(fingerGuideObj.transform, false);
            Image bodyImg = fingerBody.AddComponent<Image>();
            bodyImg.color = new Color(1f, 1f, 1f, 0.7f);
            bodyImg.raycastTarget = false;
            RectTransform bodyRt = fingerBody.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(-10f, 40f); // 손끝 위쪽, 약간 왼쪽 기울기
            bodyRt.sizeDelta = new Vector2(24f, 50f);

            // 원형 파동 효과 (손끝 지점)
            GameObject ripple = new GameObject("FingerRipple");
            ripple.transform.SetParent(fingerGuideObj.transform, false);
            Image rippleImg = ripple.AddComponent<Image>();
            rippleImg.color = new Color(0.4f, 0.7f, 1f, 0.3f);
            rippleImg.raycastTarget = false;
            RectTransform rippleRt = ripple.GetComponent<RectTransform>();
            rippleRt.anchoredPosition = new Vector2(0f, 0f);
            rippleRt.sizeDelta = new Vector2(70f, 70f);
        }

        private void CreateBottomHint()
        {
            bottomHintObj = new GameObject("TutorialBottomHint");
            bottomHintObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = bottomHintObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 80f); // 화면 하단 80px 위
            rt.sizeDelta = new Vector2(-40f, 80f);

            // 배경 (둥근 직사각형 느낌)
            Image bg = bottomHintObj.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.15f, 0.3f, 0.88f);
            bg.raycastTarget = false;

            // 밝은 테두리
            Outline bgOutline = bottomHintObj.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0.5f, 0.7f, 1f, 0.65f);
            bgOutline.effectDistance = new Vector2(2f, -2f);

            GameObject textObj = new GameObject("BottomHintText");
            textObj.transform.SetParent(bottomHintObj.transform, false);
            bottomHintText = textObj.AddComponent<Text>();
            bottomHintText.font = font;
            bottomHintText.fontSize = 28;
            bottomHintText.fontStyle = FontStyle.Bold;
            bottomHintText.color = new Color(1f, 0.95f, 0.7f, 1f);
            bottomHintText.alignment = TextAnchor.MiddleCenter;
            bottomHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bottomHintText.verticalOverflow = VerticalWrapMode.Overflow;
            bottomHintText.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20f, 8f);
            textRt.offsetMax = new Vector2(-20f, -8f);

            Outline textOutline = textObj.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);

            bottomHintObj.SetActive(false);
        }

        /// <summary>하단 가이드 배너 표시 (ForcedAction 중 지속 표시, 자동 소멸 없음).
        /// 타자기 효과로 한 글자씩 표시 — 텍스트가 길어도 부드럽게 등장.</summary>
        public void ShowBottomHint(string message)
        {
            if (bottomHintObj == null) return;
            bottomHintObj.SetActive(true);
            bottomHintObj.transform.SetAsLastSibling();
            // ★ 타자기 효과로 표시 (Dialog와 동일 방식). BottomHint는 advance 콜백이 없어 단순 reveal만.
            StartTypewriter(bottomHintText, message);
        }

        /// <summary>하단 가이드 배너 숨김</summary>
        public void HideBottomHint()
        {
            if (bottomHintObj != null) bottomHintObj.SetActive(false);
            // ★ BottomHint를 표시 중이었다면 타자기 정리
            if (typewriterTarget == bottomHintText)
            {
                if (typewriterCo != null) { StopCoroutine(typewriterCo); typewriterCo = null; }
                typewriterTarget = null;
                typewriterComplete = true;
            }
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
            AddTextOutline(textObj, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.5f, -1.5f));
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
        public void ShowDialog(string character, string title, string message, bool showTapPrompt, Action onTap, Sprite iconSprite = null)
        {
            // ★ 타자기 효과 — onTap을 래핑해 타이핑 중 탭은 가속/즉시 완료, 완료 1초 후 탭만 advance
            onTapCallback = WrapTapWithTypewriter(onTap);

            // 텍스트 설정 — 캐릭터명은 <> 꺾쇠로 감싸서 표시 (캐릭터/제목은 즉시 표시)
            // 화자명은 실제 대사(message)가 있을 때만 표시 — 말하지 않는(빈 메시지) 단계에서는 <이름> 숨김
            bool speaking = !string.IsNullOrEmpty(character) && !string.IsNullOrEmpty(message);
            dialogCharText.text = speaking ? $"<{character}>" : "";
            dialogCharText.gameObject.SetActive(speaking);
            // 화자 초상화: 캐릭터명이 있을 때만. 실제 표시는 딤(전체 내레이션) 상태에서만 —
            //   딤이 꺼진 상호작용 단계(강제 액션 등)에서는 그리드를 가리지 않도록 숨김.
            //   (UpdateDialogPortraitVisibility를 딤 설정 직후 + SetDimOverlayActive에서 호출)
            dialogHasCharacter = !string.IsNullOrEmpty(character);
            if (dialogHasCharacter && dialogPortraitImg != null && dialogPortraitImg.sprite == null)
                dialogPortraitImg.sprite = LoadNarratorPortrait();
            dialogTitleText.text = string.IsNullOrEmpty(title) ? "" : title;
            dialogTitleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            // ★ 탭 안내: 즉시 숨김 — 타자기 + 1초 락 만료 시점에 advance ready 콜백으로 활성화
            tapPromptText.gameObject.SetActive(false);
            System.Action onAdvanceReady = null;
            if (showTapPrompt)
            {
                onAdvanceReady = () =>
                {
                    if (tapPromptText != null && dialogPanelObj != null && dialogPanelObj.activeSelf)
                    {
                        tapPromptText.gameObject.SetActive(true);
                        if (tapBlinkCoroutine != null) StopCoroutine(tapBlinkCoroutine);
                        tapBlinkCoroutine = StartCoroutine(AnimateTapPromptBlink());
                    }
                };
            }
            // 메시지: 타자기 효과로 한 글자씩 표시 (완료 + 1초 후 onAdvanceReady 호출)
            StartTypewriter(dialogMsgText, message, onAdvanceReady);

            // 아이콘 표시 설정 — 있으면 왼쪽 아이콘 + 타이틀/메시지를 오른쪽으로 밀어 수직 평행 정렬
            // (캐릭터명은 상단 헤더로 전체 폭 유지 — 아이콘은 캐릭터명 아래 영역에 배치됨)
            bool hasIcon = iconSprite != null;
            if (dialogIconObj != null)
            {
                dialogIconObj.SetActive(hasIcon);
                if (hasIcon) dialogIconImg.sprite = iconSprite;
            }

            // 캐릭터명: 항상 상단 전체폭 (헤더 역할)
            RectTransform charRt = dialogCharText.GetComponent<RectTransform>();
            charRt.anchoredPosition = new Vector2(24f, -16f);
            charRt.sizeDelta = new Vector2(-48f, 36f);

            // 타이틀·메시지 좌측 시작점: 아이콘(24~174) 오른쪽 16px 여백
            float textLeft = hasIcon ? 190f : 24f;
            RectTransform titleRt = dialogTitleText.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(textLeft, -56f);
            titleRt.sizeDelta = new Vector2(-(textLeft + 24f), 40f);

            // 메시지: 아이콘과 수직 평행 — 세로 중앙 정렬로 짧은 텍스트도 타이틀과 균형 유지
            RectTransform msgRt = dialogMsgText.GetComponent<RectTransform>();
            msgRt.offsetMin = new Vector2(textLeft, 50f);
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(character))
                msgRt.offsetMax = new Vector2(-24f, -20f);
            else if (string.IsNullOrEmpty(title))
                msgRt.offsetMax = new Vector2(-24f, -56f);
            else
                msgRt.offsetMax = new Vector2(-24f, -100f);

            // 메시지 정렬: 아이콘 있을 때는 MiddleLeft로 아이콘 중심과 시각적 균형 맞춤
            dialogMsgText.alignment = hasIcon ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;

            // 표시 — dim overlay는 spotlight가 활성이면 꺼둠 (중복 디밍 방지)
            bool spotlightActive = spotlightRoot != null && spotlightRoot.activeSelf;
            dimOverlayObj.SetActive(!spotlightActive);
            dialogPanelObj.SetActive(true);
            tapAreaObj.SetActive(showTapPrompt);
            skipBtnObj.SetActive(true);
            UpdateDialogPortraitVisibility();

            // 순서 보장 (오버레이/스포트라이트 < 대화 < 탭영역 < 스킵)
            if (dimOverlayObj.activeSelf) dimOverlayObj.transform.SetAsLastSibling();
            if (spotlightActive) spotlightRoot.transform.SetAsLastSibling();
            dialogPanelObj.transform.SetAsLastSibling();
            tapAreaObj.transform.SetAsLastSibling();
            skipBtnObj.transform.SetAsLastSibling();

            // ★ 탭 깜빡임은 타자기 완료 + 1초 락 후 onAdvanceReady 콜백에서 시작 (즉시 시작 X)

            // 슬라이드 인 애니메이션
            StartCoroutine(AnimateDialogIn());
        }

        /// <summary>
        /// 대화 팝업 숨기기
        /// </summary>
        public void HideDialog()
        {
            if (tapBlinkCoroutine != null) { StopCoroutine(tapBlinkCoroutine); tapBlinkCoroutine = null; }
            // ★ 타자기 정리 — 다음 스텝 진입 시 stale 코루틴이 새 텍스트 덮어쓰지 않도록
            if (typewriterCo != null) { StopCoroutine(typewriterCo); typewriterCo = null; }
            typewriterTarget = null;
            typewriterComplete = true; // advance 차단 해제 (Hide 후 즉시 재사용 가능)
            dialogPanelObj.SetActive(false);
            dimOverlayObj.SetActive(false);
            tapAreaObj.SetActive(false);
        }

        /// <summary>
        /// 원형 하이라이트 (하위 호환용)
        /// </summary>
        public void ShowHighlight(Vector2 canvasPos, float radius)
        {
            ShowHighlightRect(canvasPos, new Vector2(radius * 2f, radius * 2f));
        }

        /// <summary>
        /// 직사각형 하이라이트 표시 — 몰딩 전환 애니메이션 적용
        /// canvasPos: Canvas 중앙 기준 좌표
        /// size: 하이라이트 영역 크기
        /// </summary>
        public void ShowHighlightRect(Vector2 canvasPos, Vector2 size)
        {
            (float canvasW, float canvasH) = GetCanvasSize();
            float cx = canvasW * 0.5f + canvasPos.x;
            float cy = canvasH * 0.5f + canvasPos.y;

            Rect targetRect = new Rect(
                cx - size.x * 0.5f,
                cy - size.y * 0.5f,
                size.x,
                size.y
            );
            AnimateHighlightTo(targetRect);
        }

        /// <summary>
        /// 실제 UI의 RectTransform을 직접 타겟팅 — 정확한 위치/크기 자동 계산
        /// padding: 여백 (px)
        /// </summary>
        public void ShowHighlightForTarget(RectTransform target, float padding = 16f)
        {
            if (target == null) return;

            Rect targetRect = ConvertToCanvasAbsRect(target, padding);
            if (targetRect.width <= 0f || targetRect.height <= 0f) return;
            AnimateHighlightTo(targetRect);
        }

        /// <summary>
        /// RectTransform을 Canvas 절대좌표 Rect로 변환 (좌하단=0,0 기준)
        /// </summary>
        private Rect ConvertToCanvasAbsRect(RectTransform target, float padding)
        {
            RectTransform canvasRt = canvas.transform as RectTransform;
            Vector3[] worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);

            // Canvas 로컬 좌표계로 변환 (Canvas pivot 0.5,0.5 기준 → 중앙 기준 좌표)
            Vector2 localMin = canvasRt.InverseTransformPoint(worldCorners[0]);
            Vector2 localMax = canvasRt.InverseTransformPoint(worldCorners[2]);

            (float canvasW, float canvasH) = GetCanvasSize();
            float halfW = canvasW * 0.5f;
            float halfH = canvasH * 0.5f;

            // 중앙 기준 → 좌하단 기준 절대 좌표
            float absMinX = localMin.x + halfW - padding;
            float absMinY = localMin.y + halfH - padding;
            float absMaxX = localMax.x + halfW + padding;
            float absMaxY = localMax.y + halfH + padding;

            // 화면 밖 클램프
            absMinX = Mathf.Max(0f, absMinX);
            absMinY = Mathf.Max(0f, absMinY);
            absMaxX = Mathf.Min(canvasW, absMaxX);
            absMaxY = Mathf.Min(canvasH, absMaxY);

            return new Rect(absMinX, absMinY, absMaxX - absMinX, absMaxY - absMinY);
        }

        private (float, float) GetCanvasSize()
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            RectTransform canvasRt = canvas.transform as RectTransform;
            float w = scaler != null && scaler.referenceResolution.x > 0f
                ? scaler.referenceResolution.x
                : (canvasRt != null ? canvasRt.rect.width : 1080f);
            float h = scaler != null && scaler.referenceResolution.y > 0f
                ? scaler.referenceResolution.y
                : (canvasRt != null ? canvasRt.rect.height : 1920f);
            return (w, h);
        }

        /// <summary>
        /// 이전 하이라이트 Rect → 새 Rect로 부드럽게 전환 (몰딩)
        /// </summary>
        private void AnimateHighlightTo(Rect target)
        {
            // 스포트라이트 활성화, dim overlay 비활성화 (충돌 방지)
            spotlightRoot.SetActive(true);
            if (dimOverlayObj != null && dimOverlayObj.activeSelf)
                dimOverlayObj.SetActive(false);

            // 대화/탭/스킵 Z-order 재보장
            spotlightRoot.transform.SetAsLastSibling();
            if (dialogPanelObj != null && dialogPanelObj.activeSelf)
                dialogPanelObj.transform.SetAsLastSibling();
            if (tapAreaObj != null && tapAreaObj.activeSelf)
                tapAreaObj.transform.SetAsLastSibling();
            if (skipBtnObj != null && skipBtnObj.activeSelf)
                skipBtnObj.transform.SetAsLastSibling();

            Rect startRect;
            if (hasActiveHighlight)
            {
                // 현재 위치에서 새 위치로 몰딩 전환
                startRect = currentHighlightRect;
            }
            else
            {
                // 첫 등장: 타겟 중심에 점으로 시작 → 확장 (pop-in)
                Vector2 center = new Vector2(
                    target.x + target.width * 0.5f,
                    target.y + target.height * 0.5f
                );
                startRect = new Rect(center.x, center.y, 0f, 0f);
                // 스포트라이트 알파 초기화
                foreach (var img in spotlightRects)
                    if (img != null) img.color = new Color(0f, 0f, 0f, 0f);
            }

            if (highlightMorphCoroutine != null) StopCoroutine(highlightMorphCoroutine);
            highlightMorphCoroutine = StartCoroutine(MorphHighlightCoroutine(startRect, target, HIGHLIGHT_MORPH_DURATION, !hasActiveHighlight));

            hasActiveHighlight = true;
            currentHighlightRect = target;

            // 테두리 글로우 펄스 시작
            if (glowPulseCoroutine != null) StopCoroutine(glowPulseCoroutine);
            glowPulseCoroutine = StartCoroutine(AnimateGlowPulse());
        }

        private IEnumerator MorphHighlightCoroutine(Rect from, Rect to, float duration, bool fadeIn)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseInOutCubic — 자연스러운 몰딩 커브
                float eased = t < 0.5f
                    ? 4f * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

                Rect curr = new Rect(
                    Mathf.Lerp(from.x, to.x, eased),
                    Mathf.Lerp(from.y, to.y, eased),
                    Mathf.Lerp(from.width, to.width, eased),
                    Mathf.Lerp(from.height, to.height, eased)
                );

                ApplySpotlightRect(curr);

                // 첫 등장 시 어둡기 페이드인
                if (fadeIn)
                {
                    float alpha = Mathf.Lerp(0f, SPOTLIGHT_DIM_ALPHA, eased);
                    foreach (var img in spotlightRects)
                        if (img != null) img.color = new Color(0f, 0f, 0f, alpha);
                }

                yield return null;
            }
            ApplySpotlightRect(to);
            if (fadeIn)
            {
                foreach (var img in spotlightRects)
                    if (img != null) img.color = new Color(0f, 0f, 0f, SPOTLIGHT_DIM_ALPHA);
            }
        }

        /// <summary>
        /// 4개의 rect로 구멍 배치 (상/하/좌/우) + 글로우 테두리 동기화
        /// </summary>
        private void ApplySpotlightRect(Rect hole)
        {
            (float canvasW, float canvasH) = GetCanvasSize();

            float left = Mathf.Max(0f, hole.x);
            float right = Mathf.Min(canvasW, hole.x + hole.width);
            float bottom = Mathf.Max(0f, hole.y);
            float top = Mathf.Min(canvasH, hole.y + hole.height);

            // 상단: 구멍 위
            SetSpotlightRect(spotlightRects[0], 0, top, canvasW, canvasH - top);
            // 하단: 구멍 아래
            SetSpotlightRect(spotlightRects[1], 0, 0, canvasW, bottom);
            // 좌측: 구멍 왼쪽 (상하단 제외 높이)
            SetSpotlightRect(spotlightRects[2], 0, bottom, left, top - bottom);
            // 우측: 구멍 오른쪽
            SetSpotlightRect(spotlightRects[3], right, bottom, canvasW - right, top - bottom);

            // 테두리 글로우 동기화
            if (spotlightGlow != null)
            {
                RectTransform grt = spotlightGlow.GetComponent<RectTransform>();
                grt.pivot = new Vector2(0.5f, 0.5f);
                grt.anchoredPosition = new Vector2(left + (right - left) * 0.5f, bottom + (top - bottom) * 0.5f);
                grt.sizeDelta = new Vector2(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom));
            }
        }

        private void SetSpotlightRect(Image img, float x, float y, float w, float h)
        {
            RectTransform rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        // ============================================================
        // 말풍선 통합 API (UI 설명 전용)
        // ============================================================

        /// <summary>
        /// RectTransform 타겟을 밝게 + 그 옆에 말풍선으로 설명 표시.
        /// 기존 하단 대화 패널 대신 사용 — UI 지칭 시 직관적인 말풍선.
        /// </summary>
        public void ShowHighlightDialog(RectTransform target, float padding,
            string character, string title, string message, bool showTap, Action onTap)
        {
            if (target == null) return;
            ShowHighlightForTarget(target, padding);
            ShowSpeechBubbleForRect(currentHighlightRect, character, title, message, showTap, onTap);
        }

        /// <summary>
        /// 직사각형 크기 기반 하이라이트 + 말풍선.
        /// </summary>
        public void ShowHighlightDialog(Vector2 canvasPos, Vector2 size,
            string character, string title, string message, bool showTap, Action onTap)
        {
            ShowHighlightRect(canvasPos, size);
            ShowSpeechBubbleForRect(currentHighlightRect, character, title, message, showTap, onTap);
        }

        /// <summary>
        /// 원형 반경 기반 하이라이트 + 말풍선 (하위 호환).
        /// </summary>
        public void ShowHighlightDialogCircle(Vector2 canvasPos, float radius,
            string character, string title, string message, bool showTap, Action onTap)
        {
            ShowHighlight(canvasPos, radius);
            ShowSpeechBubbleForRect(currentHighlightRect, character, title, message, showTap, onTap);
        }

        /// <summary>
        /// 스포트라이트(딤 오버레이) 없이 주어진 Rect 근처에 말풍선만 표시.
        /// 블록 클릭/드래그가 필요한 튜토리얼 단계용 — dim 오버레이로 인한 입력 차단 방지.
        /// </summary>
        public void ShowSpeechBubbleOnly(Vector2 canvasPos, Vector2 size,
            string character, string title, string message, bool showTap, Action onTap)
        {
            // ★ 진행 중인 하이라이트 페이드 중단 — 남은 spotlight rect의 raycastTarget이
            //   블록 클릭을 가로막는 것을 확실히 방지
            if (highlightMorphCoroutine != null) { StopCoroutine(highlightMorphCoroutine); highlightMorphCoroutine = null; }
            if (glowPulseCoroutine != null) { StopCoroutine(glowPulseCoroutine); glowPulseCoroutine = null; }

            // dim overlay와 spotlight 비활성 → 블록 클릭 pass-through
            if (dimOverlayObj != null) dimOverlayObj.SetActive(false);
            if (spotlightRoot != null) spotlightRoot.SetActive(false);
            hasActiveHighlight = false;

            // highlightRect만 내부용으로 설정 (말풍선 배치에 사용)
            Rect rect = new Rect(canvasPos - size * 0.5f, size);
            currentHighlightRect = rect;

            ShowSpeechBubbleForRect(rect, character, title, message, showTap, onTap);

            // 말풍선이 블록 클릭 가로막지 않도록 raycast 끔 (탭도 안 받음)
            if (speechBubbleBgImg != null) speechBubbleBgImg.raycastTarget = false;
        }

        /// <summary>
        /// 말풍선을 지정된 하이라이트 Rect 근처에 배치해 표시.
        /// 타겟 위/아래 중 공간 여유 있는 쪽에 배치 + 꼬리로 타겟 가리킴.
        /// </summary>
        private void ShowSpeechBubbleForRect(Rect highlightRect,
            string character, string title, string message, bool showTap, Action onTap)
        {
            // ★ 타자기 효과 + advance 잠금 적용 (Dialog와 동일)
            onTapCallback = WrapTapWithTypewriter(onTap);

            // 텍스트 설정 — 캐릭터명은 <> 꺾쇠로 감싸서 표시 (캐릭터/제목은 즉시 표시)
            bool hasChar = !string.IsNullOrEmpty(character);
            bool hasTitle = !string.IsNullOrEmpty(title);
            // 화자명은 실제 대사(message)가 있을 때만 표시 — 말하지 않는(빈 메시지) 단계에서는 <이름> 숨김
            bool speakingSpeech = hasChar && !string.IsNullOrEmpty(message);
            speechCharText.text = speakingSpeech ? $"<{character}>" : "";
            speechCharText.gameObject.SetActive(speakingSpeech);
            // 화자 전신 초상화 (화면 가장자리): 캐릭터명 있을 때만. 위치/크기/반전은 PositionSpeechBubble에서.
            if (speechPortraitObj != null)
            {
                if (hasChar && speechPortraitImg != null && speechPortraitImg.sprite == null)
                    speechPortraitImg.sprite = LoadNarratorFullBody();
                speechPortraitObj.SetActive(hasChar && speechPortraitImg != null && speechPortraitImg.sprite != null);
            }
            speechTitleText.text = hasTitle ? title : "";
            speechTitleText.gameObject.SetActive(hasTitle);
            // ★ 탭 안내: 즉시 숨김 — 타자기 + 1초 락 만료 시점에 advance ready 콜백으로 활성화
            speechTapText.gameObject.SetActive(false);
            System.Action onSpeechAdvanceReady = null;
            if (showTap)
            {
                onSpeechAdvanceReady = () =>
                {
                    if (speechTapText != null && speechBubbleObj != null && speechBubbleObj.activeSelf)
                    {
                        speechTapText.gameObject.SetActive(true);
                        if (speechTapBlinkCoroutine != null) StopCoroutine(speechTapBlinkCoroutine);
                        speechTapBlinkCoroutine = StartCoroutine(AnimateSpeechTapBlink());
                    }
                };
            }
            // 메시지: 타자기 효과로 한 글자씩 표시 (완료 + 1초 후 onSpeechAdvanceReady 호출)
            StartTypewriter(speechMsgText, message ?? "", onSpeechAdvanceReady);

            // 메시지 영역 동적 조정 (char/title 유무에 따라)
            RectTransform msgRt = speechMsgText.GetComponent<RectTransform>();
            if (!hasChar && !hasTitle)
                msgRt.offsetMax = new Vector2(-22f, -18f);
            else if (!hasTitle)
                msgRt.offsetMax = new Vector2(-22f, -52f);
            else if (!hasChar)
                msgRt.offsetMax = new Vector2(-22f, -58f);
            else
                msgRt.offsetMax = new Vector2(-22f, -90f);

            speechBubbleObj.SetActive(true);

            // showTap=false면 말풍선 자체도 raycast 차단 해제 → 뒤의 스포트라이트로 pass-through
            // (탭 없이 안내만 표시하는 경우, 말풍선 탭해도 토스트 콜백이 활성화되도록)
            if (speechBubbleBgImg != null) speechBubbleBgImg.raycastTarget = showTap;
            if (speechBubbleTailImg != null) speechBubbleTailImg.raycastTarget = false;

            // 위치 계산 + 꼬리 방향 설정
            PositionSpeechBubble(highlightRect);

            // 탭 영역: 화면 아무 곳이나 탭해도 다음으로 넘어가도록 활성화
            // (같은 onTapCallback을 공유하므로 말풍선 위 탭도, 바깥 탭도 동일 결과)
            if (showTap && tapAreaObj != null)
            {
                tapAreaObj.SetActive(true);
                // Z-order: 스포트라이트 → 탭영역 → 말풍선 → 스킵 버튼
                tapAreaObj.transform.SetAsLastSibling();
                speechBubbleObj.transform.SetAsLastSibling();
                if (skipBtnObj != null && skipBtnObj.activeSelf)
                    skipBtnObj.transform.SetAsLastSibling();
            }
            else
            {
                speechBubbleObj.transform.SetAsLastSibling();
            }

            // ★ 탭 깜빡임은 타자기 완료 + 1초 락 후 onSpeechAdvanceReady 콜백에서 시작 (즉시 시작 X)

            // 등장 애니메이션 (스케일 pop-in)
            if (speechBubbleMoveCoroutine != null) StopCoroutine(speechBubbleMoveCoroutine);
            speechBubbleMoveCoroutine = StartCoroutine(AnimateSpeechPopIn());
        }

        /// <summary>
        /// 말풍선 위치/꼬리 방향 계산 및 적용.
        /// 타겟이 화면 상반부면 말풍선을 아래에, 하반부면 위에 배치.
        /// 수평 위치는 타겟 중심을 따르되 화면 경계 내로 클램프.
        /// </summary>
        private void PositionSpeechBubble(Rect highlightRect)
        {
            (float canvasW, float canvasH) = GetCanvasSize();

            RectTransform bubbleRt = speechBubbleObj.GetComponent<RectTransform>();
            RectTransform tailRt = speechBubbleTailObj.GetComponent<RectTransform>();

            float halfBubbleW = SPEECH_BUBBLE_W * 0.5f;
            float halfBubbleH = SPEECH_BUBBLE_H * 0.5f;

            float targetCenterX = highlightRect.x + highlightRect.width * 0.5f;
            float targetTop = highlightRect.y + highlightRect.height;
            float targetBottom = highlightRect.y;

            // 배치 방향 결정: 타겟 아래 공간 vs 위 공간 비교
            float spaceBelow = targetBottom - 30f; // 화면 하단 여유
            float spaceAbove = canvasH - targetTop - 30f;
            bool placeAbove = spaceAbove >= SPEECH_BUBBLE_H + SPEECH_GAP || spaceAbove > spaceBelow;

            // 수직 위치
            float bubbleCenterY;
            if (placeAbove)
                bubbleCenterY = targetTop + SPEECH_GAP + halfBubbleH;
            else
                bubbleCenterY = targetBottom - SPEECH_GAP - halfBubbleH;

            // 화면 경계 클램프 (수직)
            bubbleCenterY = Mathf.Clamp(bubbleCenterY, halfBubbleH + 20f, canvasH - halfBubbleH - 20f);

            // 수평 위치: 타겟 중심을 따르되 경계 클램프
            float bubbleCenterX = Mathf.Clamp(targetCenterX, halfBubbleW + 20f, canvasW - halfBubbleW - 20f);

            bubbleRt.anchoredPosition = new Vector2(bubbleCenterX, bubbleCenterY);

            // 화자 전신 초상화: 말풍선 반대편 화면 가장자리에 세워 겹침 방지 + 항상 중앙(설명 대상)을 향함.
            PositionSpeechPortrait(bubbleCenterX > canvasW * 0.5f, canvasW, canvasH);

            // 꼬리 방향/위치
            //   스프라이트 pivot=(0.5, 1) (스프라이트 상단 = 뾰족)이므로
            //   회전 180°하면 스프라이트가 뒤집혀 밑변이 위/뾰족이 아래로 그려짐 → 말풍선 밖으로 향함
            //   placeAbove=true (말풍선 위 + 꼬리 아래) → rotation 180° (밑변이 말풍선에 붙고 뾰족이 아래)
            //   placeAbove=false (말풍선 아래 + 꼬리 위) → rotation 0°  (밑변이 말풍선에 붙고 뾰족이 위)
            float tailLocalX = Mathf.Clamp(targetCenterX - bubbleCenterX, -halfBubbleW + 30f, halfBubbleW - 30f);
            if (placeAbove)
            {
                tailRt.anchorMin = new Vector2(0.5f, 0f);
                tailRt.anchorMax = new Vector2(0.5f, 0f);
                tailRt.pivot = new Vector2(0.5f, 1f);
                // rotation 180°일 때 RectTransform 내용은 pivot **위쪽**으로 렌더링되므로,
                // 꼬리 전체를 sizeDelta.y(꼬리 높이)만큼 아래로 내려야 말풍선 밖으로 돌출됨.
                // 2px는 테두리와 자연스럽게 이어지도록 안쪽 겹침
                tailRt.anchoredPosition = new Vector2(tailLocalX, -tailRt.sizeDelta.y + 2f);
                tailRt.localEulerAngles = new Vector3(0f, 0f, 180f); // 뾰족 아래로
            }
            else
            {
                tailRt.anchorMin = new Vector2(0.5f, 1f);
                tailRt.anchorMax = new Vector2(0.5f, 1f);
                tailRt.pivot = new Vector2(0.5f, 1f);
                tailRt.anchoredPosition = new Vector2(tailLocalX, tailRt.sizeDelta.y - 2f);
                tailRt.localEulerAngles = Vector3.zero; // 뾰족 위로
            }
        }

        /// <summary>
        /// 말풍선 화자 전신 초상화를 말풍선과 겹치지 않게 화면 가장자리(말풍선 반대편)에 세운다.
        ///   bubbleOnRight=true(말풍선이 화면 오른쪽) → 초상화는 화면 왼쪽 가장자리(정방향, 오른쪽=중앙을 바라봄).
        ///   bubbleOnRight=false(왼쪽) → 초상화는 화면 오른쪽 가장자리 + 좌우반전(왼쪽=중앙을 바라봄).
        ///   바닥에 서고, 바깥쪽 일부는 화면 밖으로 살짝 걸쳐 중앙 공간을 적게 차지한다.
        ///   z-order는 말풍선보다 아래(뒤) — 혹시 겹쳐도 말풍선이 가려지지 않도록.
        /// </summary>
        private void PositionSpeechPortrait(bool bubbleOnRight, float canvasW, float canvasH)
        {
            if (speechPortraitObj == null || !speechPortraitObj.activeSelf) return;
            RectTransform rt = speechPortraitObj.GetComponent<RectTransform>();

            // 전신 크기: 화면 높이의 약 50% (스프라이트 비율 유지 → 폭 자동)
            Sprite sp = speechPortraitImg != null ? speechPortraitImg.sprite : null;
            float aspect = (sp != null && sp.rect.height > 0) ? (sp.rect.width / sp.rect.height) : 0.68f;
            float figH = canvasH * 0.50f;
            float figW = figH * aspect;
            rt.sizeDelta = new Vector2(figW, figH);

            float half = figW * 0.5f;
            const float clipOut = 0.20f; // 바깥쪽 20%는 화면 밖으로 걸침 (망토는 화면 밖, 몸통은 가장자리에서 안쪽으로)
            bool figureOnRight = !bubbleOnRight; // 말풍선 반대편

            rt.anchorMin = rt.anchorMax = new Vector2(figureOnRight ? 1f : 0f, 0f); // 바닥 모서리
            float cx = figureOnRight ? -(half - figW * clipOut) : (half - figW * clipOut);
            rt.anchoredPosition = new Vector2(cx, 0f); // 바닥에 섬

            // 오른쪽 배치면 좌우반전(왼쪽=중앙을 바라봄)
            Vector3 s = speechPortraitObj.transform.localScale;
            s.x = figureOnRight ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            speechPortraitObj.transform.localScale = s;

            // 말풍선보다 뒤로 (말풍선/탭영역이 위에 오도록 — 호출부에서 다시 올림)
            speechPortraitObj.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 말풍선 숨기기 (페이드 아웃).
        /// </summary>
        public void HideSpeechBubble()
        {
            // 전체 화면 탭 영역 비활성화 (다른 UI가 탭 수신하도록 복원)
            if (tapAreaObj != null) tapAreaObj.SetActive(false);
            // 전신 초상화도 함께 숨김
            if (speechPortraitObj != null) speechPortraitObj.SetActive(false);

            // ★ 타자기 정리
            if (typewriterCo != null) { StopCoroutine(typewriterCo); typewriterCo = null; }
            typewriterTarget = null;
            typewriterComplete = true;

            if (speechBubbleObj == null || !speechBubbleObj.activeSelf) return;
            if (speechTapBlinkCoroutine != null) { StopCoroutine(speechTapBlinkCoroutine); speechTapBlinkCoroutine = null; }
            if (speechBubbleMoveCoroutine != null) StopCoroutine(speechBubbleMoveCoroutine);
            speechBubbleMoveCoroutine = StartCoroutine(AnimateSpeechFadeOut());
        }

        /// <summary>
        /// 하이라이트 + 말풍선 함께 숨김.
        /// </summary>
        public void HideHighlightDialog()
        {
            HideSpeechBubble();
            HideHighlight();
        }

        private IEnumerator AnimateSpeechPopIn()
        {
            Transform t = speechBubbleObj.transform;
            t.localScale = new Vector3(0.7f, 0.7f, 1f);
            float duration = 0.28f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                // EaseOutBack
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float eased = 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
                float s = Mathf.Lerp(0.7f, 1f, eased);
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        private IEnumerator AnimateSpeechFadeOut()
        {
            Transform t = speechBubbleObj.transform;
            Vector3 startScale = t.localScale;
            float duration = 0.18f;
            float elapsed = 0f;
            Color bgOrig = speechBubbleBgImg.color;
            Color tailOrig = speechBubbleTailImg.color;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.Lerp(1f, 0.85f, p);
                t.localScale = new Vector3(s, s, 1f);
                float a = 1f - p;
                speechBubbleBgImg.color = new Color(bgOrig.r, bgOrig.g, bgOrig.b, bgOrig.a * a);
                speechBubbleTailImg.color = new Color(tailOrig.r, tailOrig.g, tailOrig.b, tailOrig.a * a);
                yield return null;
            }
            speechBubbleObj.SetActive(false);
            t.localScale = Vector3.one;
            speechBubbleBgImg.color = bgOrig;
            speechBubbleTailImg.color = tailOrig;
            speechBubbleMoveCoroutine = null;
        }

        private IEnumerator AnimateSpeechTapBlink()
        {
            while (true)
            {
                if (speechTapText != null)
                {
                    float a = 0.45f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                    speechTapText.color = new Color(0.85f, 0.85f, 0.85f, a); // 어두운 배경용 밝은 회색
                }
                yield return null;
            }
        }

        // ============================================================
        // 말풍선 스프라이트 생성 (프로시저럴)
        // ============================================================

        /// <summary>
        /// 둥근 모서리 직사각형 스프라이트 생성 (프로시저럴).
        /// 4모서리는 원호, 테두리 두께만큼 border 색, 내부는 fill 색.
        /// </summary>
        private static Sprite CreateRoundedRectSprite(int w, int h, int radius, Color fill, Color border, int borderWidth)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 가장 가까운 "모서리 원 중심" 구하기 (모서리가 아니면 dist=0)
                    int cornerCx = Mathf.Clamp(x, radius, w - radius - 1);
                    int cornerCy = Mathf.Clamp(y, radius, h - radius - 1);
                    float dx = x - cornerCx;
                    float dy = y - cornerCy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // 직사각형 테두리까지 거리 (가장 가까운 변)
                    float edgeDist = Mathf.Min(
                        Mathf.Min(x, w - 1 - x),
                        Mathf.Min(y, h - 1 - y));

                    Color c;
                    if (dist == 0f)
                    {
                        // 중앙 사각형 영역 (모서리 아님)
                        if (edgeDist < borderWidth)
                            c = border;
                        else
                            c = fill;
                    }
                    else if (dist <= radius - borderWidth)
                    {
                        c = fill;
                    }
                    else if (dist <= radius - 0.5f)
                    {
                        c = border;
                    }
                    else if (dist <= radius + 0.5f)
                    {
                        // 안티앨리어싱 가장자리
                        float aa = 1f - (dist - (radius - 0.5f));
                        c = new Color(border.r, border.g, border.b, border.a * Mathf.Clamp01(aa));
                    }
                    else
                    {
                        c = Color.clear;
                    }

                    pixels[y * w + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 말풍선 꼬리 삼각형 스프라이트 (아래로 뾰족 — 기본).
        /// pivot은 (0.5, 1) 상단 중앙 기준으로 Sprite 생성 시 적용.
        /// 회전으로 4방향 모두 사용 가능.
        /// </summary>
        private static Sprite CreateSpeechTailSprite(int w, int h, Color fill, Color border, int borderWidth)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            // 상단(y=h-1)이 밑변(넓음), 하단(y=0)이 뾰족한 끝점
            for (int y = 0; y < h; y++)
            {
                float t = 1f - (float)y / (h - 1); // 0=뾰족(bottom), 1=밑변(top)
                float halfWidth = (w * 0.5f - 1f) * t;
                int xMin = Mathf.FloorToInt(w * 0.5f - halfWidth);
                int xMax = Mathf.CeilToInt(w * 0.5f + halfWidth);

                for (int x = 0; x < w; x++)
                {
                    if (x < xMin || x > xMax) continue;
                    // 테두리 영역 판정 (양 변에서 borderWidth 이내)
                    int distFromEdge = Mathf.Min(x - xMin, xMax - x);
                    if (distFromEdge < borderWidth)
                        pixels[y * w + x] = border;
                    else
                        pixels[y * w + x] = fill;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f);
        }

        /// <summary>
        /// 스포트라이트 숨기기 — 수축하며 페이드 아웃
        /// </summary>
        public void HideHighlight()
        {
            if (!hasActiveHighlight || spotlightRoot == null || !spotlightRoot.activeSelf)
            {
                if (spotlightRoot != null) spotlightRoot.SetActive(false);
                hasActiveHighlight = false;
                return;
            }

            if (highlightMorphCoroutine != null) StopCoroutine(highlightMorphCoroutine);
            if (glowPulseCoroutine != null) { StopCoroutine(glowPulseCoroutine); glowPulseCoroutine = null; }
            highlightMorphCoroutine = StartCoroutine(FadeOutHighlightCoroutine());
        }

        private IEnumerator FadeOutHighlightCoroutine()
        {
            Rect start = currentHighlightRect;
            Vector2 center = new Vector2(
                start.x + start.width * 0.5f,
                start.y + start.height * 0.5f
            );
            float elapsed = 0f;

            while (elapsed < HIGHLIGHT_FADE_DURATION)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / HIGHLIGHT_FADE_DURATION);
                float eased = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic

                // 수축: 중심으로 크기 감소
                float w = Mathf.Lerp(start.width, 0f, eased);
                float h = Mathf.Lerp(start.height, 0f, eased);
                Rect curr = new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
                ApplySpotlightRect(curr);

                // 검은 판 페이드 아웃
                float alpha = Mathf.Lerp(SPOTLIGHT_DIM_ALPHA, 0f, eased);
                foreach (var img in spotlightRects)
                    if (img != null) img.color = new Color(0f, 0f, 0f, alpha);

                // 글로우 페이드
                if (spotlightGlowImg != null)
                {
                    Color c = spotlightGlowImg.color;
                    spotlightGlowImg.color = new Color(c.r, c.g, c.b, c.a * (1f - t));
                }

                yield return null;
            }

            spotlightRoot.SetActive(false);
            // 복원
            foreach (var img in spotlightRects)
                if (img != null) img.color = new Color(0f, 0f, 0f, SPOTLIGHT_DIM_ALPHA);
            if (spotlightGlowImg != null)
                spotlightGlowImg.color = new Color(1f, 0.95f, 0.5f, 0f);

            hasActiveHighlight = false;
            highlightMorphCoroutine = null;
        }

        private IEnumerator AnimateGlowPulse()
        {
            if (spotlightGlowImg == null) yield break;

            // 페이드 인
            float fadeIn = 0.3f;
            float elapsed = 0f;
            while (elapsed < fadeIn)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeIn);
                Outline o = spotlightGlow.GetComponent<Outline>();
                if (o != null)
                    o.effectColor = new Color(1f, 0.95f, 0.5f, 0.9f * t);
                yield return null;
            }

            // 지속 펄스
            while (true)
            {
                Outline o = spotlightGlow.GetComponent<Outline>();
                if (o != null)
                {
                    float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 2.8f);
                    o.effectColor = new Color(1f, 0.95f, 0.5f, 0.9f * pulse);
                }
                yield return null;
            }
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
        /// 두 Canvas 좌표 사이에 점선 스타일 드래그 연결선 표시 (스왑 튜토리얼).
        /// 호출마다 기존 선이 있으면 제거 후 재생성.
        /// </summary>
        public void ShowDragLine(Vector2 fromCanvasPos, Vector2 toCanvasPos)
        {
            HideDragLine();

            dragLineObj = new GameObject("TutorialDragLine");
            dragLineObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = dragLineObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f); // 왼쪽 끝이 pivot

            Vector2 delta = toCanvasPos - fromCanvasPos;
            float dist = delta.magnitude;
            float angleDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            rt.anchoredPosition = fromCanvasPos;
            rt.sizeDelta = new Vector2(dist, 14f);
            rt.localEulerAngles = new Vector3(0, 0, angleDeg);

            dragLineImg = dragLineObj.AddComponent<Image>();
            dragLineImg.color = new Color(1f, 0.85f, 0.3f, 0.85f); // 밝은 노랑 반투명
            dragLineImg.raycastTarget = false;

            // 손가락 가이드 아래에 오도록 dragLineObj를 먼저 배치 (손가락이 위)
            dragLineObj.transform.SetAsLastSibling();
            if (fingerGuideObj != null && fingerGuideObj.activeSelf)
                fingerGuideObj.transform.SetAsLastSibling();

            if (dragLinePulseCoroutine != null) StopCoroutine(dragLinePulseCoroutine);
            dragLinePulseCoroutine = StartCoroutine(AnimateDragLinePulse());
        }

        /// <summary>드래그 연결선 숨김/파괴</summary>
        public void HideDragLine()
        {
            if (dragLinePulseCoroutine != null) { StopCoroutine(dragLinePulseCoroutine); dragLinePulseCoroutine = null; }
            if (dragLineObj != null) { Destroy(dragLineObj); dragLineObj = null; dragLineImg = null; }
        }

        private IEnumerator AnimateDragLinePulse()
        {
            while (dragLineImg != null)
            {
                float t = Mathf.PingPong(Time.unscaledTime * 1.2f, 1f);
                float alpha = Mathf.Lerp(0.5f, 0.95f, t);
                dragLineImg.color = new Color(1f, 0.85f, 0.3f, alpha);
                yield return null;
            }
        }

        /// <summary>
        /// 손가락 드래그 애니메이션 — 한 좌표에서 다른 좌표로 반복 이동.
        /// 시작점에서 터치 → 천천히 끝점으로 이동 → 짧은 대기 → 다시 시작점으로 워프 → 반복.
        /// </summary>
        public void ShowFingerDrag(Vector2 fromCanvasPos, Vector2 toCanvasPos)
        {
            fingerGuideObj.SetActive(true);
            RectTransform rt = fingerGuideObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = fromCanvasPos;

            fingerGuideObj.transform.SetAsLastSibling();

            if (fingerBounceCoroutine != null) StopCoroutine(fingerBounceCoroutine);
            fingerBounceCoroutine = StartCoroutine(AnimateFingerDrag(fromCanvasPos, toCanvasPos));
        }

        private IEnumerator AnimateFingerDrag(Vector2 from, Vector2 to)
        {
            RectTransform rt = fingerGuideObj.GetComponent<RectTransform>();
            if (rt == null) yield break;

            while (fingerGuideObj != null && fingerGuideObj.activeSelf)
            {
                // 1. 시작점에서 잠깐 대기 (터치 유지감)
                rt.anchoredPosition = from;
                rt.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(0.25f);

                // 2. 끝점까지 이동 (0.8s EaseInOut)
                float dur = 0.8f;
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    // EaseInOutCubic
                    float eased = t < 0.5f
                        ? 4f * t * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
                    rt.anchoredPosition = Vector2.Lerp(from, to, eased);
                    // 드래그 중 스케일 살짝 축소 (누르는 느낌)
                    rt.localScale = Vector3.one * (0.9f + 0.05f * Mathf.Sin(elapsed * 20f));
                    yield return null;
                }

                // 3. 끝점에서 잠깐 대기 (놓는 느낌)
                rt.anchoredPosition = to;
                rt.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(0.3f);

                // 4. 살짝 페이드되는 동안 시작점으로 워프 (순환)
                // 간단히 알파 애니메이션 생략하고 바로 워프 (시각적 혼란 최소화)
            }
        }

        /// <summary>
        /// 상단 힌트 배너 표시 (자동 소멸)
        /// </summary>
        public void ShowHintBanner(string message, float duration = 4f)
        {
            hintBannerText.text = message;
            hintBannerObj.SetActive(true);
            hintBannerObj.transform.SetAsLastSibling();

            // 이전 페이드 중단 + 알파 풀 복원 (중간 상태로 멈춘 흔적 제거)
            if (hintFadeCoroutine != null) StopCoroutine(hintFadeCoroutine);
            Image bg = hintBannerObj.GetComponent<Image>();
            if (bg != null)
            {
                Color c = bg.color; c.a = 0.88f; bg.color = c;
            }
            if (hintBannerText != null)
            {
                Color tc = hintBannerText.color; tc.a = 1f; hintBannerText.color = tc;
            }

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
            if (speechBubbleObj != null) speechBubbleObj.SetActive(false);
            if (speechPortraitObj != null) speechPortraitObj.SetActive(false); // Canvas 자식 전신 초상화
            if (fingerGuideObj != null) fingerGuideObj.SetActive(false);
            if (hintBannerObj != null) hintBannerObj.SetActive(false);
            if (bottomHintObj != null) bottomHintObj.SetActive(false);
            if (skipBtnObj != null) skipBtnObj.SetActive(false);
            if (tapAreaObj != null) tapAreaObj.SetActive(false);

            if (fingerBounceCoroutine != null) { StopCoroutine(fingerBounceCoroutine); fingerBounceCoroutine = null; }
            if (tapBlinkCoroutine != null) { StopCoroutine(tapBlinkCoroutine); tapBlinkCoroutine = null; }
            if (hintFadeCoroutine != null) { StopCoroutine(hintFadeCoroutine); hintFadeCoroutine = null; }
            if (highlightMorphCoroutine != null) { StopCoroutine(highlightMorphCoroutine); highlightMorphCoroutine = null; }
            if (glowPulseCoroutine != null) { StopCoroutine(glowPulseCoroutine); glowPulseCoroutine = null; }
            // ★ 타자기 정리
            if (typewriterCo != null) { StopCoroutine(typewriterCo); typewriterCo = null; }
            typewriterTarget = null;
            typewriterComplete = true;
            if (speechBubbleMoveCoroutine != null) { StopCoroutine(speechBubbleMoveCoroutine); speechBubbleMoveCoroutine = null; }
            if (speechTapBlinkCoroutine != null) { StopCoroutine(speechTapBlinkCoroutine); speechTapBlinkCoroutine = null; }
            HideDragLine();
            hasActiveHighlight = false;
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
            if (speechBubbleObj != null) Destroy(speechBubbleObj);
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
            UpdateDialogPortraitVisibility(); // 딤 끄면 초상화도 숨겨 그리드를 가리지 않음
        }

        /// <summary>
        /// 대화 패널 화자 초상화 표시 갱신.
        /// 조건: 화자명 있음 + 대화 패널 활성 + 딤(전체 내레이션) 활성 + 스프라이트 로드됨.
        /// 딤이 꺼진 상호작용 단계에서는 숨겨 주요 튜토리얼 영역(그리드/버튼)을 가리지 않는다.
        /// </summary>
        private void UpdateDialogPortraitVisibility()
        {
            if (dialogPortraitObj == null) return;
            bool show = dialogHasCharacter
                && dialogPanelObj != null && dialogPanelObj.activeSelf
                && dimOverlayObj != null && dimOverlayObj.activeSelf
                && dialogPortraitImg != null && dialogPortraitImg.sprite != null;
            if (dialogPortraitObj.activeSelf != show) dialogPortraitObj.SetActive(show);
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
            Vector2 basePos = rt.anchoredPosition; // 탭 타겟 위치 (최저점)
            float liftHeight = 36f;                // 손가락이 들리는 최대 높이
            float cycleDuration = 0.9f;            // 한 주기(들었다 누르기까지)

            // ★ 단방향 오실레이션: basePos(최저점) ↔ basePos + liftHeight(최고점)
            //   → 최저점에서 손끝이 정확히 탭 위치에 맞춰짐
            //   주기 구성: [0, 0.4) 상승(ease out) → [0.4, 0.55) 정점 유지
            //              → [0.55, 0.8) 하강(ease in, 더 빠름) → [0.8, 1.0) 눌림 유지
            while (true)
            {
                float t = (Time.unscaledTime % cycleDuration) / cycleDuration;
                float offset;

                if (t < 0.4f)
                {
                    // 상승: 0 → 1 (ease out)
                    float u = t / 0.4f;
                    offset = 1f - Mathf.Pow(1f - u, 3f);
                }
                else if (t < 0.55f)
                {
                    // 정점 유지
                    offset = 1f;
                }
                else if (t < 0.8f)
                {
                    // 하강: 1 → 0 (ease in, 빠른 프레스 느낌)
                    float u = (t - 0.55f) / 0.25f;
                    offset = 1f - u * u;
                }
                else
                {
                    // 눌림 유지 (최저점)
                    offset = 0f;
                }

                rt.anchoredPosition = basePos + new Vector2(0f, offset * liftHeight);
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

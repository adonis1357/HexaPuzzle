using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Utils;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// UI 전체 관리
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [Header("HUD Elements")]
        [SerializeField] private Text turnText;
        [SerializeField] private Text stageText;
        [SerializeField] private Text goldText;
        [SerializeField] private Text hudGoldText; // 게임 중 골드 표시

        [Header("Move Counter")]
        [SerializeField] private Image moveProgressRing;
        [SerializeField] private Text moveMaxText;

        [Header("Stage Clear Detail")]
        [SerializeField] private Text clearTitleText;
        [SerializeField] private Text clearBaseScoreText;
        [SerializeField] private Text clearTurnBonusText;
        [SerializeField] private Text clearEfficiencyBonusText;
        [SerializeField] private Text clearTotalScoreText;
        [SerializeField] private Image[] starImages;

        [Header("Mission Display")]
        [SerializeField] private Transform missionContainer;
        [SerializeField] private GameObject missionItemPrefab;
        [SerializeField] private MissionUI[] missionSlots;

        [Header("Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button rotationToggleButton;
        [SerializeField] private Image rotationDirectionIcon;

        [Header("Item Buttons")]
        [SerializeField] private ItemButtonUI[] itemButtons;
        /// <summary>아이템 버튼 배열 외부 접근</summary>
        public ItemButtonUI[] ItemButtons => itemButtons;

        [Header("Popups")]
        [SerializeField] private GameObject pausePopup;
        [SerializeField] private GameObject gameOverPopup;
        [SerializeField] private GameObject stageClearPopup;
        [SerializeField] private GameObject helpPopup;

        [Header("Popup Buttons")]
        [SerializeField] private Button pauseOutButton;
        [SerializeField] private Button pauseHelpButton;
        [SerializeField] private Button pauseRetryButton;
        [SerializeField] private Button gameOverOutButton;
        [SerializeField] private Button gameOverBuyButton;
        [SerializeField] private Button gameOverRetryButton;
        [SerializeField] private Button stageClearNextButton;

        [Header("Rotation Direction Icons")]
        [SerializeField] private Sprite clockwiseIcon;
        [SerializeField] private Sprite counterClockwiseIcon;

        [Header("Animation")]
        [SerializeField] private float popupAnimationDuration = 0.3f;

        private bool isClockwise = true;

        // 점수 카운팅 애니메이션
        private int displayedScore = 0;
        private Coroutine scoreCountCoroutine;
        private Color scoreDefaultColor = Color.white;
        private Color scoreHighlightColor = new Color(1f, 0.84f, 0f); // Gold

        // 이동 횟수 애니메이션
        private int maxTurns = 30;
        private bool isInfiniteMode = false;
        private Coroutine turnBounceCoroutine;
        private Coroutine turnPulseCoroutine;

        // 미션 진행도 (단일 미션 하위호환)
        public static Text gameMissionCountText;
        public static RectTransform gameMissionIconRect;
        public static Text gameMissionRewardText;

        // 복수 미션 진행도
        public static List<Text> gameMissionCountTexts = new List<Text>();

        // ★ 미션 행 하단 진행바 fill (anchorMax.x = 진행률) + 목표 수량 (행 인덱스 동기)
        public static List<RectTransform> gameMissionProgressFills = new List<RectTransform>();
        public static List<int> gameMissionTargets = new List<int>();
        public static RectTransform gameMissionContainerRect;

        // 다음 미션 미리보기
        public static RectTransform nextMissionPreviewRect;

        public void SetTurnText(Text text) { turnText = text; }
        public void SetScoreText(Text text)
        {
            goldText = text;
            if (text != null) scoreDefaultColor = text.color;
        }
        public void SetGoldText(Text text) { hudGoldText = text; }
        public void SetGameOverPopup(GameObject popup) { if (gameOverPopup == null) gameOverPopup = popup; }

        private void Start()
        {
            SetupButtons();
            HideAllPopups();
            InitToastPool();

            if (goldText != null)
                scoreDefaultColor = goldText.color;
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtons()
        {
            // 일시정지 버튼
            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseButtonClicked);

            // 회전 방향 토글
            if (rotationToggleButton != null)
                rotationToggleButton.onClick.AddListener(OnRotationToggleClicked);

            // 일시정지 팝업 버튼들
            if (pauseOutButton != null)
                pauseOutButton.onClick.AddListener(OnOutButtonClicked);
            if (pauseHelpButton != null)
                pauseHelpButton.onClick.AddListener(OnHelpButtonClicked);
            if (pauseRetryButton != null)
                pauseRetryButton.onClick.AddListener(OnRetryButtonClicked);

            // 게임오버 팝업 버튼들
            if (gameOverOutButton != null)
                gameOverOutButton.onClick.AddListener(OnOutButtonClicked);
            if (gameOverBuyButton != null)
                gameOverBuyButton.onClick.AddListener(OnBuyTurnButtonClicked);
            if (gameOverRetryButton != null)
                gameOverRetryButton.onClick.AddListener(OnRetryButtonClicked);

            // 스테이지 클리어 버튼
            if (stageClearNextButton != null)
                stageClearNextButton.onClick.AddListener(OnNextStageButtonClicked);
        }

        // ============================================================
        // HUD 업데이트
        // ============================================================

        /// <summary>
        /// 턴 표시 업데이트 (바운스 애니메이션 + 프로그레스 링)
        /// </summary>
        public void UpdateTurnDisplay(int turns)
        {
            if (turnText == null) return;

            JewelsHexaPuzzle.Utils.NumberRoller.Roll(turnText, turns, v => v.ToString());

            // 무한모드: 경고/펄스 비활성화, 항상 흰색
            if (isInfiniteMode)
            {
                turnText.color = Color.white;
                StopTurnPulse();

                // 바운스 애니메이션만 적용
                if (turnBounceCoroutine != null)
                    StopCoroutine(turnBounceCoroutine);
                turnBounceCoroutine = StartCoroutine(TurnBounceAnimation());
                return;
            }

            // 최대 턴 표시
            if (moveMaxText != null)
                moveMaxText.text = $"/{maxTurns}";

            // 프로그레스 링 업데이트
            if (moveProgressRing != null)
            {
                float ratio = maxTurns > 0 ? (float)turns / maxTurns : 0f;
                moveProgressRing.fillAmount = ratio;

                // 색상 전환: 흰색(100%~30%) → 주황(30%~17%) → 빨강(17%~0%)
                if (ratio > 0.3f)
                    moveProgressRing.color = Color.white;
                else if (ratio > 0.17f)
                    moveProgressRing.color = new Color(1f, 0.6f, 0f); // 주황
                else
                    moveProgressRing.color = Color.red;
            }

            // 턴 색상 + 위험 애니메이션
            if (turns <= 3)
            {
                turnText.color = new Color(1f, 0.15f, 0.15f); // 강한 빨간
                StartTurnPulse(0.3f);
            }
            else if (turns <= 5)
            {
                turnText.color = Color.red;
                StartTurnPulse(VisualConstants.MovePulseSpeed);
            }
            else
            {
                turnText.color = Color.white;
                StopTurnPulse();
            }

            // 바운스 애니메이션
            if (turnBounceCoroutine != null)
                StopCoroutine(turnBounceCoroutine);
            turnBounceCoroutine = StartCoroutine(TurnBounceAnimation());
        }

        /// <summary>
        /// 무한모드 설정
        /// </summary>
        public void SetInfiniteMode(bool infinite)
        {
            isInfiniteMode = infinite;
            if (infinite)
            {
                // 프로그레스 링, 최대 턴 표시 비활성화
                if (moveProgressRing != null)
                    moveProgressRing.gameObject.SetActive(false);
                if (moveMaxText != null)
                    moveMaxText.gameObject.SetActive(false);

                // 펄스 중지
                StopTurnPulse();
            }
            else
            {
                if (moveProgressRing != null)
                    moveProgressRing.gameObject.SetActive(true);
                if (moveMaxText != null)
                    moveMaxText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 최대 턴 수 설정 (게임 시작 시)
        /// </summary>
        public void SetMaxTurns(int max)
        {
            maxTurns = max;
            if (moveMaxText != null)
                moveMaxText.text = $"/{maxTurns}";
        }

        /// <summary>
        /// 스테이지 표시 업데이트
        /// </summary>
        public void UpdateStageDisplay(int stage)
        {
            if (stageText != null)
            {
                stageText.text = stage.ToString();
            }
        }

        /// <summary>
        /// 점수(골드) 표시 업데이트 (카운팅 애니메이션)
        /// </summary>
        public void UpdateScoreDisplay(int score)
        {
            if (goldText == null) return;

            if (score == 0)
            {
                // 리셋 시 즉시 반영
                displayedScore = 0;
                goldText.text = "0";
                goldText.color = scoreDefaultColor;
                if (scoreCountCoroutine != null)
                    StopCoroutine(scoreCountCoroutine);
                return;
            }

            if (scoreCountCoroutine != null)
                StopCoroutine(scoreCountCoroutine);
            scoreCountCoroutine = StartCoroutine(ScoreCountAnimation(score));
        }

        // ============================================================
        // HUD 애니메이션
        // ============================================================

        /// <summary>
        /// 점수 카운팅 애니메이션
        /// </summary>
        private IEnumerator ScoreCountAnimation(int targetScore)
        {
            int startScore = displayedScore;
            float duration = VisualConstants.ScoreCountDuration;
            float elapsed = 0f;

            // 하이라이트 색상으로 전환
            goldText.color = scoreHighlightColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutQuart(elapsed / duration);
                displayedScore = (int)Mathf.Lerp(startScore, targetScore, t);
                goldText.text = FormatNumber(displayedScore);
                yield return null;
            }

            displayedScore = targetScore;
            goldText.text = FormatNumber(targetScore);

            // 색상 복원 페이드
            float fadeElapsed = 0f;
            float fadeDuration = 0.3f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                float t = fadeElapsed / fadeDuration;
                goldText.color = Color.Lerp(scoreHighlightColor, scoreDefaultColor, t);
                yield return null;
            }
            goldText.color = scoreDefaultColor;
        }

        /// <summary>
        /// 골드 표시 업데이트
        /// </summary>
        public void UpdateGoldDisplay(int gold)
        {
            if (hudGoldText != null)
            {
                // 골드는 또렷하게 카운트업 보이도록 더 긴 가시 지속시간(min 0.35s, 상한 0.7s)
                JewelsHexaPuzzle.Utils.NumberRoller.Roll(hudGoldText, gold, v => v.ToString(), 0.7f, 0.35f);
            }
        }

        /// <summary>
        /// 골드 팝업 표시 (떠오르는 텍스트 애니메이션)
        /// </summary>
        public void ShowGoldPopup(int amount, Vector3 worldPos)
        {
            StartCoroutine(GoldPopupCoroutine(amount, worldPos));
        }

        /// <summary>
        /// 골드 팝업 코루틴
        /// </summary>
        private IEnumerator GoldPopupCoroutine(int amount, Vector3 worldPos)
        {
            // Canvas를 찾기
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield break;

            // 월드 좌표를 스크린 좌표로 변환
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // 팝업 GameObject 생성
            GameObject popupObj = new GameObject("GoldPopup_" + amount);
            popupObj.transform.SetParent(canvas.transform, false);

            // RectTransform 설정
            RectTransform popupRt = popupObj.AddComponent<RectTransform>();
            popupRt.anchoredPosition = screenPos;
            popupRt.sizeDelta = new Vector2(100f, 50f);

            // Text 컴포넌트 추가
            Text popupText = popupObj.AddComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            popupText.font = font;
            popupText.fontSize = 32;
            popupText.fontStyle = FontStyle.Bold;
            popupText.alignment = TextAnchor.MiddleCenter;
            popupText.color = new Color(1f, 0.84f, 0f); // 노란색
            popupText.raycastTarget = false;
            popupText.text = "+" + amount;

            // 떠오르는 애니메이션
            float duration = 1.2f;
            float elapsed = 0f;
            Vector3 startPos = popupRt.anchoredPosition;
            Vector3 endPos = startPos + Vector3.up * 80f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 위치 이동 (ease out)
                float eased = VisualConstants.EaseOutCubic(t);
                popupRt.anchoredPosition = Vector3.Lerp(startPos, endPos, eased);

                // 투명도 감소 (마지막 0.3초)
                if (elapsed > duration * 0.7f)
                {
                    float fadeT = (elapsed - duration * 0.7f) / (duration * 0.3f);
                    popupText.color = new Color(1f, 0.84f, 0f, 1f - fadeT);
                }

                yield return null;
            }

            Destroy(popupObj);
        }

        /// <summary>
        /// 턴 사용 시 바운스 애니메이션
        /// </summary>
        private IEnumerator TurnBounceAnimation()
        {
            if (turnText == null) yield break;

            RectTransform rt = turnText.GetComponent<RectTransform>();
            if (rt == null) yield break;

            float duration = VisualConstants.MoveBounceDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = VisualConstants.EaseOutBack(t);
                // 1.0 → 1.15 → 1.0
                float scale;
                if (t < 0.3f)
                    scale = Mathf.Lerp(1f, VisualConstants.MoveBounceScale, t / 0.3f);
                else
                    scale = Mathf.Lerp(VisualConstants.MoveBounceScale, 1f, (t - 0.3f) / 0.7f);

                rt.localScale = Vector3.one * scale;
                yield return null;
            }

            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// 턴 위험 시 펄스 애니메이션 시작
        /// </summary>
        private void StartTurnPulse(float speed)
        {
            if (turnPulseCoroutine != null) StopCoroutine(turnPulseCoroutine);
            turnPulseCoroutine = StartCoroutine(TurnPulseAnimation(speed));
        }

        private void StopTurnPulse()
        {
            if (turnPulseCoroutine != null)
            {
                StopCoroutine(turnPulseCoroutine);
                turnPulseCoroutine = null;
            }
            if (turnText != null)
            {
                Color c = turnText.color;
                c.a = 1f;
                turnText.color = c;
            }
        }

        private IEnumerator TurnPulseAnimation(float cycleTime)
        {
            while (true)
            {
                float elapsed = 0f;
                // 페이드 아웃
                while (elapsed < cycleTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / cycleTime;
                    float alpha = Mathf.Lerp(1f, 0.4f, t);
                    if (turnText != null)
                    {
                        Color c = turnText.color;
                        c.a = alpha;
                        turnText.color = c;
                    }
                    yield return null;
                }
                // 페이드 인
                elapsed = 0f;
                while (elapsed < cycleTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / cycleTime;
                    float alpha = Mathf.Lerp(0.4f, 1f, t);
                    if (turnText != null)
                    {
                        Color c = turnText.color;
                        c.a = alpha;
                        turnText.color = c;
                    }
                    yield return null;
                }
            }
        }

        // ============================================================
        // 미션 표시
        // ============================================================

        /// <summary>
        /// 미션 표시 업데이트
        /// </summary>
        public void UpdateMissionDisplay(MissionData[] missions)
        {
            if (missionSlots == null) return;

            for (int i = 0; i < missionSlots.Length; i++)
            {
                if (i < missions.Length && missions[i] != null)
                {
                    missionSlots[i].gameObject.SetActive(true);
                    missionSlots[i].SetMission(missions[i]);
                }
                else
                {
                    missionSlots[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 미션 진행도 업데이트
        /// </summary>
        public void UpdateMissionProgress(int index, int current, int target)
        {
            if (missionSlots != null && index < missionSlots.Length)
            {
                missionSlots[index].UpdateProgress(current, target);
            }
        }

        /// <summary>
        /// 아이템 버튼 업데이트
        /// </summary>
        public void UpdateItemButtons(ItemData[] items)
        {
            Debug.Log($"[아이템진단4] UpdateItemButtons 호출됨 itemButtons={(itemButtons != null ? itemButtons.Length.ToString() : "null")} items={items?.Length}");
            if (itemButtons == null) return;

            for (int i = 0; i < itemButtons.Length; i++)
            {
                Debug.Log($"[아이템진단4] 버튼[{i}]={itemButtons[i]} null={itemButtons[i] == null}");
                if (i < items.Length)
                {
                    itemButtons[i].SetItem(items[i]);
                }
            }
        }

        // ============================================================
        // 팝업 관리
        // ============================================================

        /// <summary>
        /// 일시정지 팝업 표시
        /// </summary>
        public void ShowPausePopup()
        {
            ShowPopup(pausePopup);
        }

        /// <summary>
        /// 일시정지 팝업 숨김
        /// </summary>
        public void HidePausePopup()
        {
            HidePopup(pausePopup);
        }

        /// <summary>
        /// 게임오버 팝업 표시
        /// </summary>
        public void ShowGameOverPopup()
        {
            ShowPopup(gameOverPopup);
        }

        /// <summary>
        /// 게임오버 팝업 숨김
        /// </summary>
        public void HideGameOverPopup()
        {
            HidePopup(gameOverPopup);
        }

        /// <summary>
        /// 스테이지 클리어 팝업 표시 (기본)
        /// </summary>
        public void ShowStageClearPopup()
        {
            if (stageClearPopup == null)
                CreateClearPopup();
            ShowPopup(stageClearPopup);
        }

        /// <summary>
        /// 스테이지 클리어 팝업 표시 (점수 브레이크다운 + 골드 포함)
        /// </summary>
        public void ShowStageClearPopup(StageSummaryData summary, int goldReward = 0)
        {
            if (stageClearPopup == null)
                CreateClearPopup();
            ShowPopup(stageClearPopup);
            StartCoroutine(AnimateStageClearBreakdown(summary, goldReward));
        }

        /// <summary>
        /// 스테이지 클리어 팝업 표시 (골드만 포함, summary 없음)
        /// </summary>
        public void ShowStageClearPopup(int goldReward)
        {
            // 팝업이 없으면 동적으로 생성
            if (stageClearPopup == null)
            {
                CreateClearPopup();
            }

            ShowPopup(stageClearPopup);
            StartCoroutine(AnimateSimpleStageClearWithGold(goldReward));
        }

        /// <summary>
        /// 도움말 팝업 표시
        /// </summary>
        public void ShowHelpPopup()
        {
            HidePopup(pausePopup);
            ShowPopup(helpPopup);
        }

        // ============================================================
        // 스테이지 클리어 브레이크다운 애니메이션
        // ============================================================

        private IEnumerator AnimateStageClearBreakdown(StageSummaryData summary, int goldReward = 0)
        {
            // 팝업 등장 대기
            yield return new WaitForSeconds(popupAnimationDuration + 0.2f);

            // 타이틀 표시
            if (clearTitleText != null)
            {
                clearTitleText.text = "STAGE CLEAR!\n수고하셨습니다!\n멋진 플레이였어요!";
            }

            // 획득 점수 표시 (카운팅 애니메이션)
            if (clearBaseScoreText != null)
            {
                yield return StartCoroutine(AnimateScoreLine(clearBaseScoreText, summary.totalScore, 0.5f, "획득 점수: {0}"));
                yield return new WaitForSeconds(0.15f);
            }

            // 획득 골드 표시 (카운팅 애니메이션)
            if (clearTurnBonusText != null)
            {
                yield return StartCoroutine(AnimateScoreLine(clearTurnBonusText, goldReward, 0.4f, "+{0} 골드"));

                // 골드 스케일 펀치
                RectTransform goldRt = clearTurnBonusText.GetComponent<RectTransform>();
                if (goldRt != null)
                {
                    float punchDuration = 0.2f;
                    float punchElapsed = 0f;
                    while (punchElapsed < punchDuration)
                    {
                        punchElapsed += Time.unscaledDeltaTime;
                        float t = punchElapsed / punchDuration;
                        float scale;
                        if (t < 0.4f)
                            scale = Mathf.Lerp(1f, 1.2f, t / 0.4f);
                        else
                            scale = Mathf.Lerp(1.2f, 1f, (t - 0.4f) / 0.6f);
                        goldRt.localScale = Vector3.one * scale;
                        yield return null;
                    }
                    goldRt.localScale = Vector3.one;
                }
            }

            // 최고 점수 표시
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(ShowHighScoreInPopup(summary.totalScore));
        }

        private IEnumerator AnimateScoreLine(Text textComp, int targetValue, float duration, string format = null)
        {
            if (textComp == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = VisualConstants.EaseOutQuart(elapsed / duration);
                int current = (int)Mathf.Lerp(0, targetValue, t);

                if (format != null)
                    textComp.text = string.Format(format, FormatNumber(current));
                else
                    textComp.text = FormatNumber(current);

                yield return null;
            }

            if (format != null)
                textComp.text = string.Format(format, FormatNumber(targetValue));
            else
                textComp.text = FormatNumber(targetValue);
        }

        private IEnumerator StarPopAnimation(Image star)
        {
            if (star == null) yield break;

            RectTransform rt = star.GetComponent<RectTransform>();
            if (rt == null) yield break;

            float duration = 0.25f;
            float elapsed = 0f;

            rt.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = VisualConstants.EaseOutBack(t);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }

            rt.localScale = Vector3.one;
        }

        /// <summary>
        /// 간단한 스테이지 클리어 애니메이션 (골드 정보만)
        /// </summary>
        private IEnumerator AnimateSimpleStageClearWithGold(int goldReward)
        {
            // 팝업 등장 대기
            yield return new WaitForSeconds(popupAnimationDuration + 0.2f);

            // 타이틀 표시
            if (clearTitleText != null)
            {
                clearTitleText.text = "STAGE CLEAR!\n수고하셨습니다!\n멋진 플레이였어요!";
            }

            // 점수 표시
            if (clearBaseScoreText != null)
            {
                ScoreManager sm = FindObjectOfType<ScoreManager>();
                int score = sm != null ? sm.CurrentScore : 0;
                yield return StartCoroutine(AnimateScoreLine(clearBaseScoreText, score, 0.4f, "획득 점수: {0}"));
                yield return new WaitForSeconds(0.15f);
            }

            // 골드 표시
            if (clearTurnBonusText != null)
            {
                yield return StartCoroutine(AnimateScoreLine(clearTurnBonusText, goldReward, 0.4f, "+{0} 골드"));
            }

            // 최고 점수 표시
            ScoreManager sm2 = FindObjectOfType<ScoreManager>();
            int popupScore = sm2 != null ? sm2.CurrentScore : 0;
            yield return new WaitForSeconds(0.2f);
            yield return StartCoroutine(ShowHighScoreInPopup(popupScore));
        }

        /// <summary>
        /// 클리어/게임오버 팝업 안에 최고 점수 표시
        /// </summary>
        private IEnumerator ShowHighScoreInPopup(int currentScore)
        {
            // 부모 팝업 결정 (클리어 팝업 우선)
            Transform parent = null;
            if (stageClearPopup != null && stageClearPopup.activeSelf)
                parent = stageClearPopup.transform;
            else if (gameOverPopup != null && gameOverPopup.activeSelf)
                parent = gameOverPopup.transform;

            if (parent == null) yield break;

            ScoreManager sm = FindObjectOfType<ScoreManager>();
            if (sm == null) yield break;

            // 기존 HighScoreContainer 제거 (팝업 재사용 시 중첩 방지)
            Transform existingHs = parent.Find("HighScoreContainer");
            if (existingHs != null)
                Destroy(existingHs.gameObject);

            int stage = GameManager.Instance != null ? GameManager.Instance.CurrentStage : 1;
            int levelBest = sm.GetLevelHighScore(stage);
            int personalBest = sm.GetPersonalLevelBest(stage);
            // TryUpdateLevelHighScore에서 저장해둔 갱신 전 이전 기록 사용
            int prevLevelBest = sm.PreviousLevelBest;
            int prevPersonalBest = sm.PreviousPersonalBest;
            // 이미 저장 후이므로 levelBest == currentScore이면 신기록
            bool isNewLevelBest = currentScore > prevLevelBest && currentScore > 0;
            bool isNewPersonalBest = currentScore > prevPersonalBest && currentScore > 0;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 최고 점수 컨테이너
            GameObject hsContainer = new GameObject("HighScoreContainer");
            hsContainer.transform.SetParent(parent, false);
            RectTransform hsRt = hsContainer.AddComponent<RectTransform>();
            hsRt.anchoredPosition = new Vector2(0f, -80f);
            hsRt.sizeDelta = new Vector2(350f, 100f);

            // 레벨 최고 점수
            GameObject levelBestObj = new GameObject("LevelBestText");
            levelBestObj.transform.SetParent(hsContainer.transform, false);
            RectTransform lbRt = levelBestObj.AddComponent<RectTransform>();
            lbRt.anchoredPosition = new Vector2(0f, 25f);
            lbRt.sizeDelta = new Vector2(350f, 36f);
            Text lbText = levelBestObj.AddComponent<Text>();
            lbText.font = font;
            lbText.fontSize = 20;
            lbText.fontStyle = FontStyle.Bold;
            lbText.alignment = TextAnchor.MiddleCenter;
            lbText.raycastTarget = false;
            lbText.color = isNewLevelBest ? new Color(1f, 1f, 0.3f) : new Color(1f, 0.85f, 0.3f, 0.9f);
            // 초기 텍스트: 갱신 시 이전 기록 표시, 아니면 현재 기록 표시
            lbText.text = isNewLevelBest
                ? string.Format("BEST: {0:N0}", prevLevelBest)
                : string.Format("BEST: {0:N0}", levelBest);
            Outline lbOutline = levelBestObj.AddComponent<Outline>();
            lbOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            lbOutline.effectDistance = new Vector2(1, 1);

            // 개인 최고 점수
            GameObject personalBestObj = new GameObject("PersonalBestText");
            personalBestObj.transform.SetParent(hsContainer.transform, false);
            RectTransform pbRt = personalBestObj.AddComponent<RectTransform>();
            pbRt.anchoredPosition = new Vector2(0f, -25f);
            pbRt.sizeDelta = new Vector2(350f, 36f);
            Text pbText = personalBestObj.AddComponent<Text>();
            pbText.font = font;
            pbText.fontSize = 18;
            pbText.alignment = TextAnchor.MiddleCenter;
            pbText.raycastTarget = false;
            pbText.color = isNewPersonalBest ? new Color(0.5f, 1f, 0.5f) : new Color(0.7f, 0.9f, 1f, 0.9f);
            pbText.text = isNewPersonalBest
                ? string.Format("MY BEST: {0:N0}", prevPersonalBest)
                : string.Format("MY BEST: {0:N0}", personalBest);
            Outline pbOutline = personalBestObj.AddComponent<Outline>();
            pbOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            pbOutline.effectDistance = new Vector2(1, 1);

            // 등장 애니메이션 (페이드인 + 슬라이드)
            CanvasGroup cg = hsContainer.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            float startY = hsRt.anchoredPosition.y - 20f;
            float targetY = hsRt.anchoredPosition.y;
            float fadeDuration = 0.4f;
            float fadeElapsed = 0f;

            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float t = VisualConstants.EaseOutQuart(fadeElapsed / fadeDuration);
                cg.alpha = t;
                hsRt.anchoredPosition = new Vector2(0f, Mathf.Lerp(startY, targetY, t));
                yield return null;
            }
            cg.alpha = 1f;
            hsRt.anchoredPosition = new Vector2(0f, targetY);

            // 신기록 갱신 시 카운트업 애니메이션: 이전 점수 → 새 점수
            if (isNewLevelBest || isNewPersonalBest)
            {
                yield return new WaitForSecondsRealtime(0.3f);

                float countUpDuration = 0.8f;
                float countElapsed = 0f;

                while (countElapsed < countUpDuration)
                {
                    countElapsed += Time.unscaledDeltaTime;
                    float t = VisualConstants.EaseOutQuart(countElapsed / countUpDuration);

                    if (isNewLevelBest)
                    {
                        int displayVal = (int)Mathf.Lerp(prevLevelBest, currentScore, t);
                        lbText.text = string.Format("BEST: {0:N0}", displayVal);
                    }
                    if (isNewPersonalBest)
                    {
                        int displayVal = (int)Mathf.Lerp(prevPersonalBest, currentScore, t);
                        pbText.text = string.Format("MY BEST: {0:N0}", displayVal);
                    }

                    yield return null;
                }

                // 최종 값 확정
                if (isNewLevelBest)
                    lbText.text = string.Format("BEST: {0:N0}", currentScore);
                if (isNewPersonalBest)
                    pbText.text = string.Format("MY BEST: {0:N0}", currentScore);

                // "NEW RECORD!" 뱃지 팝 애니메이션
                yield return new WaitForSecondsRealtime(0.15f);
                yield return StartCoroutine(ShowNewRecordBadge(hsContainer.transform, isNewLevelBest, isNewPersonalBest));
            }
        }

        /// <summary>
        /// 신기록 달성 시 "NEW RECORD!" 뱃지 팝 애니메이션
        /// </summary>
        private IEnumerator ShowNewRecordBadge(Transform container, bool showLevelBadge, bool showPersonalBadge)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            List<RectTransform> badges = new List<RectTransform>();

            if (showLevelBadge)
            {
                GameObject badge = new GameObject("LevelNewRecordBadge");
                badge.transform.SetParent(container, false);
                RectTransform brt = badge.AddComponent<RectTransform>();
                brt.anchoredPosition = new Vector2(155f, 25f);
                brt.sizeDelta = new Vector2(120f, 24f);
                brt.localScale = Vector3.zero;
                Text bt = badge.AddComponent<Text>();
                bt.font = font;
                bt.fontSize = 13;
                bt.fontStyle = FontStyle.Bold;
                bt.alignment = TextAnchor.MiddleCenter;
                bt.raycastTarget = false;
                bt.color = new Color(1f, 0.95f, 0.2f);
                bt.text = "★ NEW!";
                Outline bo = badge.AddComponent<Outline>();
                bo.effectColor = new Color(0.6f, 0.3f, 0f, 0.9f);
                bo.effectDistance = new Vector2(1, 1);
                badges.Add(brt);
            }

            if (showPersonalBadge)
            {
                GameObject badge = new GameObject("PersonalNewRecordBadge");
                badge.transform.SetParent(container, false);
                RectTransform brt = badge.AddComponent<RectTransform>();
                brt.anchoredPosition = new Vector2(155f, -25f);
                brt.sizeDelta = new Vector2(120f, 24f);
                brt.localScale = Vector3.zero;
                Text bt = badge.AddComponent<Text>();
                bt.font = font;
                bt.fontSize = 13;
                bt.fontStyle = FontStyle.Bold;
                bt.alignment = TextAnchor.MiddleCenter;
                bt.raycastTarget = false;
                bt.color = new Color(0.4f, 1f, 0.4f);
                bt.text = "★ NEW!";
                Outline bo = badge.AddComponent<Outline>();
                bo.effectColor = new Color(0f, 0.3f, 0f, 0.9f);
                bo.effectDistance = new Vector2(1, 1);
                badges.Add(brt);
            }

            // 뱃지 팝 애니메이션 (EaseOutBack으로 튀어나오는 느낌)
            float popDuration = 0.3f;
            float popElapsed = 0f;
            while (popElapsed < popDuration)
            {
                popElapsed += Time.unscaledDeltaTime;
                float t = popElapsed / popDuration;
                float scale = VisualConstants.EaseOutBack(t);
                foreach (var brt in badges)
                {
                    if (brt != null)
                        brt.localScale = Vector3.one * scale;
                }
                yield return null;
            }
            foreach (var brt in badges)
            {
                if (brt != null)
                    brt.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 골드 보상 표시 (팝업 내에서)
        /// </summary>
        private void DisplayGoldReward(int goldAmount)
        {
            // Canvas 찾기
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 임시 골드 표시 텍스트 생성
            GameObject goldDisplayObj = new GameObject("GoldRewardDisplay");

            // stageClearPopup이 있으면 그 안에, 없으면 Canvas에 직접 생성
            Transform parent = (stageClearPopup != null) ? stageClearPopup.transform : canvas.transform;
            goldDisplayObj.transform.SetParent(parent, false);

            RectTransform goldDisplayRt = goldDisplayObj.AddComponent<RectTransform>();
            goldDisplayRt.anchoredPosition = new Vector2(0f, -150f); // 타이틀 아래
            goldDisplayRt.sizeDelta = new Vector2(300f, 60f);

            Text goldDisplayText = goldDisplayObj.AddComponent<Text>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goldDisplayText.font = font;
            goldDisplayText.fontSize = 28;
            goldDisplayText.fontStyle = FontStyle.Bold;
            goldDisplayText.alignment = TextAnchor.MiddleCenter;
            goldDisplayText.color = new Color(1f, 0.84f, 0f); // 노란색
            goldDisplayText.raycastTarget = false;
            goldDisplayText.text = $"💰 +{goldAmount} 골드";

            // 스케일 펀치 애니메이션
            StartCoroutine(GoldRewardPopAnimation(goldDisplayRt));
        }

        /// <summary>
        /// 골드 보상 팝업 애니메이션
        /// </summary>
        private IEnumerator GoldRewardPopAnimation(RectTransform target)
        {
            float duration = 0.4f;
            float elapsed = 0f;
            Vector3 originalScale = target.localScale;

            target.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = VisualConstants.EaseOutBack(t);
                target.localScale = Vector3.one * scale;
                yield return null;
            }

            target.localScale = Vector3.one;
        }

        // ============================================================
        // 팝업 애니메이션
        // ============================================================

        /// <summary>
        /// 팝업 표시 (애니메이션 포함)
        /// </summary>
        private void ShowPopup(GameObject popup)
        {
            if (popup == null) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayPopupOpen();
            popup.SetActive(true);
            StartCoroutine(AnimatePopupIn(popup));
        }

        /// <summary>
        /// 팝업 숨김
        /// </summary>
        private void HidePopup(GameObject popup)
        {
            if (popup == null) return;

            StartCoroutine(AnimatePopupOut(popup));
        }

        /// <summary>
        /// 모든 팝업 숨김
        /// </summary>
        private void HideAllPopups()
        {
            if (pausePopup != null) pausePopup.SetActive(false);
            if (gameOverPopup != null) gameOverPopup.SetActive(false);
            if (stageClearPopup != null) stageClearPopup.SetActive(false);
            if (helpPopup != null) helpPopup.SetActive(false);
        }

        /// <summary>
        /// 클리어 팝업 동적 생성
        /// </summary>
        private void CreateClearPopup()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 팝업 루트 (반투명 배경)
            GameObject popupRoot = new GameObject("StageClearPopup");
            popupRoot.transform.SetParent(canvas.transform, false);
            RectTransform popupRootRect = popupRoot.AddComponent<RectTransform>();
            popupRootRect.anchorMin = Vector2.zero;
            popupRootRect.anchorMax = Vector2.one;
            popupRootRect.sizeDelta = Vector2.zero;
            popupRootRect.anchoredPosition = Vector2.zero;

            // 배경 (반투명 검정)
            GameObject background = new GameObject("Background");
            background.transform.SetParent(popupRoot.transform, false);
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = ClaudeTheme.Overlay;
            bgImage.raycastTarget = true;

            // 팝업 패널 (중앙)
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(popupRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(650, 650);
            panelRect.anchoredPosition = Vector2.zero;

            // 패널 배경 (크림 둥근 패널)
            Image panelBg = panel.AddComponent<Image>();
            panelBg.sprite = ClaudeTheme.PanelSprite;
            panelBg.type = Image.Type.Sliced;
            panelBg.color = ClaudeTheme.Surface;

            // 패널 테두리 (소프트 코랄)
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(ClaudeTheme.Coral.r, ClaudeTheme.Coral.g, ClaudeTheme.Coral.b, 0.35f);
            outline.effectDistance = new Vector2(2, 2);

            // 패널 코너 라운드 처리 (직사각형은 가능하지만 정확한 라운드는 어려우므로 스케일로 표현)

            // 타이틀 텍스트
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(550, 140);
            titleRect.anchoredPosition = new Vector2(0, 200);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.text = "LEVEL CLEAR!\n수고하셨습니다!\n멋진 플레이였어요!";
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.color = ClaudeTheme.CoralDark; // 코랄 (클리어 타이틀)
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            clearTitleText = titleText;

            // 획득 점수 텍스트
            GameObject scoreObj = new GameObject("Score");
            scoreObj.transform.SetParent(panel.transform, false);
            RectTransform scoreRect = scoreObj.AddComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            scoreRect.sizeDelta = new Vector2(500, 50);
            scoreRect.anchoredPosition = new Vector2(0, 75);

            Text scoreText = scoreObj.AddComponent<Text>();
            scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scoreText.text = "획득 점수: 0";
            scoreText.fontSize = 32;
            scoreText.alignment = TextAnchor.MiddleCenter;
            scoreText.color = ClaudeTheme.Ink;
            clearBaseScoreText = scoreText;

            // 획득 골드 텍스트
            GameObject goldObj = new GameObject("Gold");
            goldObj.transform.SetParent(panel.transform, false);
            RectTransform goldRect = goldObj.AddComponent<RectTransform>();
            goldRect.anchorMin = new Vector2(0.5f, 0.5f);
            goldRect.anchorMax = new Vector2(0.5f, 0.5f);
            goldRect.sizeDelta = new Vector2(500, 50);
            goldRect.anchoredPosition = new Vector2(0, 25);

            Text goldTextComp = goldObj.AddComponent<Text>();
            goldTextComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            goldTextComp.text = "💰 +0 골드";
            goldTextComp.fontSize = 32;
            goldTextComp.alignment = TextAnchor.MiddleCenter;
            goldTextComp.color = new Color(1f, 0.84f, 0f, 1f); // 노란색
            clearTurnBonusText = goldTextComp; // 골드 표시용으로 재사용

            // 확인 버튼
            GameObject buttonObj = new GameObject("ConfirmButton");
            buttonObj.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(220, 65);
            buttonRect.anchoredPosition = new Vector2(0, -230);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.sprite = ClaudeTheme.CoralSprite;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = ClaudeTheme.Coral; // 주 버튼 (코랄)

            Button buttonComponent = buttonObj.AddComponent<Button>();
            buttonComponent.targetGraphic = buttonImage;
            ClaudeTheme.StylePrimary(buttonComponent, buttonImage);

            // 버튼 텍스트
            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform, false);
            RectTransform buttonTextRect = buttonTextObj.AddComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;

            Text buttonText = buttonTextObj.AddComponent<Text>();
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.text = "확인";
            buttonText.fontSize = 24;
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;

            // 버튼 클릭 이벤트
            buttonComponent.onClick.AddListener(() =>
            {
                HidePopup(popupRoot);
                GameManager.Instance?.ReturnToLobby();
            });

            stageClearPopup = popupRoot;
            Debug.Log("[UIManager] 클리어 팝업 동적 생성 완료");
        }

        /// <summary>
        /// 팝업 등장 애니메이션
        /// </summary>
        private IEnumerator AnimatePopupIn(GameObject popup)
        {
            CanvasGroup canvasGroup = popup.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = popup.AddComponent<CanvasGroup>();

            Transform content = popup.transform.GetChild(0);

            float elapsed = 0f;
            canvasGroup.alpha = 0f;

            if (content != null)
                content.localScale = Vector3.one * 0.8f;

            while (elapsed < popupAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / popupAnimationDuration;

                canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

                if (content != null)
                    content.localScale = Vector3.Lerp(Vector3.one * 0.8f, Vector3.one, t);

                yield return null;
            }

            canvasGroup.alpha = 1f;
            if (content != null)
                content.localScale = Vector3.one;
        }

        /// <summary>
        /// 팝업 퇴장 애니메이션
        /// </summary>
        private IEnumerator AnimatePopupOut(GameObject popup)
        {
            CanvasGroup canvasGroup = popup.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                popup.SetActive(false);
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < popupAnimationDuration * 0.5f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / (popupAnimationDuration * 0.5f);

                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

                yield return null;
            }

            popup.SetActive(false);
            canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// 숫자 포맷팅 (1000 → 1,000)
        /// </summary>
        private string FormatNumber(int number)
        {
            return string.Format("{0:N0}", number);
        }

        // ============================================================
        // 버튼 이벤트 핸들러
        // ============================================================

        private void OnPauseButtonClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            GameManager.Instance?.PauseGame();
        }

        private void OnRotationToggleClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            isClockwise = !isClockwise;

            if (rotationDirectionIcon != null)
            {
                rotationDirectionIcon.sprite = isClockwise ? clockwiseIcon : counterClockwiseIcon;
            }

            GameManager.Instance?.ToggleRotationDirection();
        }

        private void OnOutButtonClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            GameManager.Instance?.ExitToLobby();
        }

        private void OnHelpButtonClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            ShowHelpPopup();
        }

        private void OnRetryButtonClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            HideAllPopups();
            Time.timeScale = 1f;
            GameManager.Instance?.RetryStage();
        }

        private void OnBuyTurnButtonClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            // 턴 구매 프로세스
            // TODO: IAP 연동
            Debug.Log("Buy Turn Clicked");

            // 테스트용: 5턴 추가
            GameManager.Instance?.AddTurns(5);
            HideGameOverPopup();
        }

        private void OnNextStageButtonClicked()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            HidePopup(stageClearPopup);
            GameManager.Instance?.NextStage();
        }

        /// <summary>
        /// 팝업 외부 터치 시 닫기
        /// </summary>
        public void OnPopupBackgroundClicked(GameObject popup)
        {
            if (popup == pausePopup)
            {
                GameManager.Instance?.ResumeGame();
            }
            else if (popup == helpPopup)
            {
                HidePopup(helpPopup);
                ShowPopup(pausePopup);
            }
        }

        // ============================================================
        // 생존 미션 UI
        // ============================================================

        private GameObject survivalMissionPanel;
        private Text missionWaveText;
        private Text missionDescText;
        private Image missionProgressFill;
        private Text missionCountText;
        private Coroutine missionSlideCoroutine;
        private Coroutine missionCompleteCoroutine;

        /// <summary>
        /// 프로시저럴 생존 미션 UI 생성 (좌상단)
        /// </summary>
        public void CreateSurvivalMissionUI(Canvas canvas)
        {
            if (survivalMissionPanel != null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 메인 패널
            survivalMissionPanel = new GameObject("SurvivalMissionPanel");
            survivalMissionPanel.transform.SetParent(canvas.transform, false);
            RectTransform panelRt = survivalMissionPanel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(10f, -10f);
            panelRt.sizeDelta = new Vector2(320f, 100f);

            Image panelBg = survivalMissionPanel.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.05f, 0.15f, 0.75f);
            panelBg.raycastTarget = false;

            // 웨이브/미션 번호
            GameObject waveObj = new GameObject("WaveText");
            waveObj.transform.SetParent(survivalMissionPanel.transform, false);
            missionWaveText = waveObj.AddComponent<Text>();
            missionWaveText.font = font;
            missionWaveText.fontSize = 13;
            missionWaveText.color = new Color(0.6f, 0.8f, 1f);
            missionWaveText.alignment = TextAnchor.MiddleLeft;
            missionWaveText.raycastTarget = false;
            missionWaveText.text = "WAVE 1 - MISSION #1";
            RectTransform waveRt = waveObj.GetComponent<RectTransform>();
            waveRt.anchorMin = new Vector2(0f, 1f);
            waveRt.anchorMax = new Vector2(1f, 1f);
            waveRt.pivot = new Vector2(0f, 1f);
            waveRt.anchoredPosition = new Vector2(12f, -6f);
            waveRt.sizeDelta = new Vector2(-24f, 20f);

            // 미션 설명
            GameObject descObj = new GameObject("DescText");
            descObj.transform.SetParent(survivalMissionPanel.transform, false);
            missionDescText = descObj.AddComponent<Text>();
            missionDescText.font = font;
            missionDescText.fontSize = 16;
            missionDescText.color = Color.white;
            missionDescText.alignment = TextAnchor.MiddleLeft;
            missionDescText.raycastTarget = false;
            missionDescText.text = "";
            RectTransform descRt = descObj.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0f, 1f);
            descRt.anchoredPosition = new Vector2(12f, -28f);
            descRt.sizeDelta = new Vector2(-24f, 24f);

            // 진행도 바 배경
            GameObject barBg = new GameObject("ProgressBarBg");
            barBg.transform.SetParent(survivalMissionPanel.transform, false);
            Image barBgImg = barBg.AddComponent<Image>();
            barBgImg.color = new Color(0.2f, 0.2f, 0.3f, 0.8f);
            barBgImg.raycastTarget = false;
            RectTransform barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0f, 0f);
            barBgRt.anchorMax = new Vector2(1f, 0f);
            barBgRt.pivot = new Vector2(0f, 0f);
            barBgRt.anchoredPosition = new Vector2(12f, 10f);
            barBgRt.sizeDelta = new Vector2(-80f, 16f);

            // 진행도 바 필
            GameObject barFill = new GameObject("ProgressBarFill");
            barFill.transform.SetParent(barBg.transform, false);
            missionProgressFill = barFill.AddComponent<Image>();
            missionProgressFill.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            missionProgressFill.raycastTarget = false;
            missionProgressFill.type = Image.Type.Filled;
            missionProgressFill.fillMethod = Image.FillMethod.Horizontal;
            missionProgressFill.fillAmount = 0f;
            RectTransform barFillRt = barFill.GetComponent<RectTransform>();
            barFillRt.anchorMin = Vector2.zero;
            barFillRt.anchorMax = Vector2.one;
            barFillRt.offsetMin = Vector2.zero;
            barFillRt.offsetMax = Vector2.zero;

            // 카운트 텍스트 (바 우측)
            GameObject countObj = new GameObject("CountText");
            countObj.transform.SetParent(survivalMissionPanel.transform, false);
            missionCountText = countObj.AddComponent<Text>();
            missionCountText.font = font;
            missionCountText.fontSize = 14;
            missionCountText.color = new Color(0.8f, 0.9f, 1f);
            missionCountText.alignment = TextAnchor.MiddleRight;
            missionCountText.raycastTarget = false;
            missionCountText.text = "0/0";
            RectTransform countRt = countObj.GetComponent<RectTransform>();
            countRt.anchorMin = new Vector2(1f, 0f);
            countRt.anchorMax = new Vector2(1f, 0f);
            countRt.pivot = new Vector2(1f, 0f);
            countRt.anchoredPosition = new Vector2(-12f, 10f);
            countRt.sizeDelta = new Vector2(60f, 16f);

            // 초기 숨김 (첫 미션 배정 시 표시)
            survivalMissionPanel.SetActive(false);
        }

        /// <summary>
        /// 새 미션 표시 — 좌측에서 슬라이드 인 (EaseOutBack)
        /// </summary>
        public void ShowNewMission(SurvivalMission mission)
        {
            if (survivalMissionPanel == null || mission == null) return;

            missionWaveText.text = $"WAVE {mission.waveNumber} - MISSION #{mission.missionNumber}";
            missionDescText.text = mission.description;
            missionProgressFill.fillAmount = 0f;

            // 보석 수집 미션이면 해당 보석 색상으로 진행바 표시
            Color barColor = GetMissionBarColor(mission);
            missionProgressFill.color = barColor;

            // 설명 텍스트에 보석 색상 강조
            if (mission.type == SurvivalMissionType.CollectGem && mission.targetGemType != GemType.None)
                missionDescText.color = Color.Lerp(Color.white, GemColors.GetColor(mission.targetGemType), 0.4f);
            else
                missionDescText.color = Color.white;

            if (mission.type == SurvivalMissionType.CollectMulti)
                missionCountText.text = $"{mission.currentCount}/{mission.targetCount} + {mission.currentCount2}/{mission.targetCount2}";
            else
                missionCountText.text = $"{mission.currentCount}/{mission.targetCount}";

            survivalMissionPanel.SetActive(true);

            if (missionSlideCoroutine != null) StopCoroutine(missionSlideCoroutine);
            missionSlideCoroutine = StartCoroutine(SlideInMission());
        }

        /// <summary>
        /// 미션 진행도 업데이트
        /// </summary>
        public void UpdateSurvivalMissionProgress(SurvivalMission mission)
        {
            if (survivalMissionPanel == null || mission == null) return;

            float prevFill = missionProgressFill.fillAmount;
            float newFill = Mathf.Clamp01(mission.Progress);
            missionProgressFill.fillAmount = newFill;

            if (mission.type == SurvivalMissionType.CollectMulti)
                missionCountText.text = $"{mission.currentCount}/{mission.targetCount} + {mission.currentCount2}/{mission.targetCount2}";
            else
                missionCountText.text = $"{mission.currentCount}/{mission.targetCount}";

            // 진행도가 증가했을 때 바 반짝임
            if (newFill > prevFill)
                StartCoroutine(ProgressBarFlash());
        }

        private IEnumerator ProgressBarFlash()
        {
            if (missionProgressFill == null) yield break;
            Color origColor = missionProgressFill.color;
            Color flashColor = new Color(
                Mathf.Min(origColor.r + 0.3f, 1f),
                Mathf.Min(origColor.g + 0.3f, 1f),
                Mathf.Min(origColor.b + 0.3f, 1f),
                1f);

            missionProgressFill.color = flashColor;

            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                missionProgressFill.color = Color.Lerp(flashColor, origColor, t);
                yield return null;
            }
            missionProgressFill.color = origColor;
        }

        /// <summary>
        /// 미션 완료 애니메이션 — 진행바 금색 + 패널 펄스 + "CLEAR!" 텍스트
        /// </summary>
        public void AnimateMissionComplete(int reward)
        {
            if (survivalMissionPanel == null) return;
            if (missionCompleteCoroutine != null) StopCoroutine(missionCompleteCoroutine);
            missionCompleteCoroutine = StartCoroutine(MissionCompleteAnimation(reward));
        }

        private Color GetMissionBarColor(SurvivalMission mission)
        {
            switch (mission.type)
            {
                case SurvivalMissionType.CollectGem:
                    Color gc = GemColors.GetColor(mission.targetGemType);
                    return new Color(gc.r, gc.g, gc.b, 0.9f);
                case SurvivalMissionType.CollectMulti:
                    // 두 색상의 중간색
                    Color c1 = GemColors.GetColor(mission.targetGemType);
                    Color c2 = GemColors.GetColor(mission.targetGemType2);
                    Color avg = (c1 + c2) * 0.5f;
                    return new Color(avg.r, avg.g, avg.b, 0.9f);
                case SurvivalMissionType.CollectAny:
                    return new Color(0.9f, 0.9f, 0.95f, 0.9f); // 밝은 흰색
                default:
                    return new Color(0.3f, 0.8f, 1f, 0.9f);    // 기본 파란색
            }
        }

        private IEnumerator SlideInMission()
        {
            RectTransform rt = survivalMissionPanel.GetComponent<RectTransform>();
            Vector2 target = new Vector2(10f, -10f);
            Vector2 start = new Vector2(-340f, -10f);
            rt.anchoredPosition = start;

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseOutBack
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
                yield return null;
            }
            rt.anchoredPosition = target;
        }

        private IEnumerator MissionCompleteAnimation(int reward)
        {
            // 1. 진행바 금색 전환
            if (missionProgressFill != null)
            {
                missionProgressFill.fillAmount = 1f;
                missionProgressFill.color = new Color(1f, 0.84f, 0f, 1f); // 금색
            }

            // 2. "CLEAR!" 텍스트 표시
            if (missionDescText != null)
                missionDescText.text = $"CLEAR! +{reward} MOVES";

            // 3. 패널 펄스 애니메이션
            RectTransform rt = survivalMissionPanel.GetComponent<RectTransform>();
            Vector3 origScale = rt.localScale;
            float pulseDuration = 0.3f;
            float elapsed = 0f;

            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pulseDuration);
                float scale = 1f + 0.1f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = origScale;

            // 4. "+1" 텍스트가 미션 패널 → 턴 UI로 날아가는 연출
            yield return new WaitForSeconds(0.3f);

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();

            if (canvas != null && turnText != null && reward > 0)
            {
                RectTransform canvasRt = canvas.GetComponent<RectTransform>();
                Camera cam = canvas.worldCamera;

                // 시작 위치: 미션 패널 중앙 → 캔버스 로컬 좌표
                Vector2 screenStart = RectTransformUtility.WorldToScreenPoint(cam, survivalMissionPanel.transform.position);
                Vector2 localStart;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenStart, cam, out localStart);

                // 도착 위치: 턴 텍스트 중앙 → 캔버스 로컬 좌표
                Vector2 screenEnd = RectTransformUtility.WorldToScreenPoint(cam, turnText.transform.position);
                Vector2 localEnd;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenEnd, cam, out localEnd);

                // reward 개수만큼 "+1" 날리기 (0.12초 간격)
                for (int i = 0; i < reward; i++)
                {
                    StartCoroutine(TurnRewardFlyAnimation(canvas.transform, canvasRt, localStart, localEnd));
                    if (i < reward - 1)
                        yield return new WaitForSeconds(0.12f);
                }

                // 마지막 "+1" 도착 대기 (비행 시간 0.45초)
                yield return new WaitForSeconds(0.55f);
            }
            else
            {
                // 캔버스/턴텍스트 없으면 대기만
                yield return new WaitForSeconds(0.5f);
            }

            // 5. 슬라이드 아웃
            Vector2 current = rt.anchoredPosition;
            Vector2 offScreen = new Vector2(-340f, current.y);
            float slideDuration = 0.3f;
            elapsed = 0f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = t * t; // EaseInQuad
                rt.anchoredPosition = Vector2.Lerp(current, offScreen, eased);
                yield return null;
            }

            survivalMissionPanel.SetActive(false);
            rt.anchoredPosition = new Vector2(10f, -10f);
        }

        // ============================================================
        // 턴 보상 날아가기 연출
        // ============================================================

        /// <summary>
        /// "+1" 텍스트가 미션 패널에서 턴 UI로 베지어 곡선을 따라 이동하는 시각 효과
        /// (턴 추가는 GameManager.OnSurvivalMissionCompleted에서 처리)
        /// </summary>
        private IEnumerator TurnRewardFlyAnimation(Transform canvasTransform, RectTransform canvasRt, Vector2 localStart, Vector2 localEnd)
        {
            // "+1" 텍스트 오브젝트 생성
            GameObject flyObj = new GameObject("TurnRewardFly");
            flyObj.transform.SetParent(canvasTransform, false);

            Text flyText = flyObj.AddComponent<Text>();
            flyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            flyText.text = "+1";
            flyText.fontSize = 28;
            flyText.fontStyle = FontStyle.Bold;
            flyText.alignment = TextAnchor.MiddleCenter;
            flyText.color = new Color(0.2f, 1f, 0.4f, 1f); // 밝은 초록색
            flyText.raycastTarget = false;
            flyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            flyText.verticalOverflow = VerticalWrapMode.Overflow;

            // 아웃라인 (가독성)
            Outline outline = flyObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            RectTransform flyRt = flyObj.GetComponent<RectTransform>();
            flyRt.sizeDelta = new Vector2(60f, 40f);
            flyRt.anchoredPosition = localStart;

            // 곡선 제어점 (위로 볼록한 포물선 + 약간의 랜덤 오프셋)
            Vector2 mid = (localStart + localEnd) * 0.5f;
            float curveHeight = Random.Range(60f, 120f);
            Vector2 controlPoint = mid + Vector2.up * curveHeight + Vector2.right * Random.Range(-30f, 30f);

            // 비행 애니메이션
            float duration = 0.45f;
            float elapsed = 0f;
            Color startColor = flyText.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseInOutQuad (부드러운 시작+끝)
                float eased = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                // 2차 베지어 곡선 경로
                float oneMinusT = 1f - eased;
                Vector2 pos = oneMinusT * oneMinusT * localStart
                            + 2f * oneMinusT * eased * controlPoint
                            + eased * eased * localEnd;
                flyRt.anchoredPosition = pos;

                // 스케일: 출발 시 크게 → 도착 시 축소
                float scale = Mathf.Lerp(1.3f, 0.7f, eased);
                flyRt.localScale = Vector3.one * scale;

                // 밝기: 도착 시 더 밝게
                float brightness = Mathf.Lerp(1f, 1.5f, eased);
                flyText.color = new Color(
                    Mathf.Min(startColor.r * brightness, 1f),
                    Mathf.Min(startColor.g * brightness, 1f),
                    Mathf.Min(startColor.b * brightness, 1f),
                    1f);

                // 트레일 효과 (매 3프레임마다)
                if (Time.frameCount % 3 == 0)
                {
                    Color trailColor = new Color(startColor.r, startColor.g, startColor.b, 0.3f);
                    StartCoroutine(GemFlyTrail(canvasTransform, pos, 10f, trailColor));
                }

                yield return null;
            }

            // 도착: 턴 텍스트 펄스 애니메이션 (턴은 GameManager에서 이미 추가됨)
            if (turnText != null)
                StartCoroutine(TurnTextArrivalPulse());

            // 도착: 플래시 이펙트
            StartCoroutine(GemArrivalFlash(canvasTransform, localEnd, new Color(0.2f, 1f, 0.4f)));

            Destroy(flyObj);
        }

        /// <summary>
        /// "+1" 도착 시 턴 텍스트 펄스 (스케일 + 초록색 하이라이트)
        /// </summary>
        private IEnumerator TurnTextArrivalPulse()
        {
            if (turnText == null) yield break;

            RectTransform rt = turnText.GetComponent<RectTransform>();
            Color origColor = turnText.color;

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 스케일 펄스
                float scale = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = Vector3.one * scale;

                // 밝은 초록색 → 원래색 전환
                turnText.color = Color.Lerp(new Color(0.3f, 1f, 0.5f), origColor, t);

                yield return null;
            }

            rt.localScale = Vector3.one;
            turnText.color = origColor;
        }

        // ============================================================
        // 보석 날아가기 연출
        // ============================================================

        private int gemFlyActiveCount = 0;
        private const int GEM_FLY_MAX = 8; // 동시 최대 개수

        /// <summary>
        /// 보석이 미션 패널로 날아가는 연출
        /// 블록 제거 시 호출 — 색상별 작은 원이 곡선 경로로 이동
        /// </summary>
        public void SpawnGemFlyEffect(Vector3 worldPos, GemType gemType)
        {
            if (survivalMissionPanel == null || !survivalMissionPanel.activeInHierarchy) return;
            if (gemFlyActiveCount >= GEM_FLY_MAX) return;

            StartCoroutine(GemFlyAnimation(worldPos, gemType));
        }

        private IEnumerator GemFlyAnimation(Vector3 worldPos, GemType gemType)
        {
            gemFlyActiveCount++;

            // 시차 랜덤 딜레이 (0~0.15초)
            float startDelay = Random.Range(0f, 0.15f);
            if (startDelay > 0f)
                yield return new WaitForSeconds(startDelay);

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) { gemFlyActiveCount--; yield break; }

            // 보석 오브젝트 생성
            GameObject gem = new GameObject("GemFly");
            gem.transform.SetParent(canvas.transform, false);

            var img = gem.AddComponent<Image>();
            img.raycastTarget = false;
            Color gemColor = GemColors.GetColor(gemType);
            img.color = gemColor;

            RectTransform gemRt = gem.GetComponent<RectTransform>();
            float size = Random.Range(14f, 20f);
            gemRt.sizeDelta = new Vector2(size, size);

            // 시작 위치 (월드 → 스크린 → 캔버스 로컬)
            Camera cam = Camera.main;
            if (cam == null) { Destroy(gem); gemFlyActiveCount--; yield break; }

            Vector2 screenStart = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
            Vector2 localStart;
            RectTransform canvasRt = canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, screenStart, canvas.worldCamera, out localStart);

            // 도착 위치 (미션 패널 중앙)
            RectTransform panelRt = survivalMissionPanel.GetComponent<RectTransform>();
            // 패널의 월드 위치 → 캔버스 로컬
            Vector2 screenEnd = RectTransformUtility.WorldToScreenPoint(cam, survivalMissionPanel.transform.position);
            Vector2 localEnd;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, screenEnd, canvas.worldCamera, out localEnd);

            gemRt.anchoredPosition = localStart;

            // 곡선 제어점 (위로 볼록한 포물선)
            Vector2 mid = (localStart + localEnd) * 0.5f;
            float curveHeight = Random.Range(80f, 160f);
            Vector2 controlPoint = mid + Vector2.up * curveHeight + Vector2.right * Random.Range(-40f, 40f);

            // 애니메이션
            float duration = Random.Range(0.4f, 0.55f);
            float elapsed = 0f;

            // 트레일용 이전 위치
            Color trailColor = new Color(gemColor.r, gemColor.g, gemColor.b, 0.4f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseInQuad (시작 느리고 끝에 가속)
                float eased = t * t;

                // 2차 베지어 곡선
                Vector2 p0 = localStart;
                Vector2 p1 = controlPoint;
                Vector2 p2 = localEnd;
                float oneMinusT = 1f - eased;
                Vector2 pos = oneMinusT * oneMinusT * p0 + 2f * oneMinusT * eased * p1 + eased * eased * p2;
                gemRt.anchoredPosition = pos;

                // 이동하면서 축소 + 밝아지기
                float scale = Mathf.Lerp(1f, 0.5f, eased);
                gemRt.localScale = Vector3.one * scale;

                // 밝기 증가 (도착 지점 가까울수록 빛남)
                float brightness = Mathf.Lerp(1f, 1.5f, eased);
                img.color = new Color(
                    Mathf.Min(gemColor.r * brightness, 1f),
                    Mathf.Min(gemColor.g * brightness, 1f),
                    Mathf.Min(gemColor.b * brightness, 1f),
                    1f);

                // 트레일 스폰 (매 3프레임마다)
                if (Time.frameCount % 3 == 0)
                    StartCoroutine(GemFlyTrail(canvas.transform, pos, size * scale * 0.6f, trailColor));

                yield return null;
            }

            // 도착: 패널 카운트 펄스
            if (missionCountText != null)
                StartCoroutine(CountTextPulse());

            // 도착 플래시
            StartCoroutine(GemArrivalFlash(canvas.transform, localEnd, gemColor));

            Destroy(gem);
            gemFlyActiveCount--;
        }

        /// <summary>
        /// 보석 트레일 (잔상)
        /// </summary>
        private IEnumerator GemFlyTrail(Transform parent, Vector2 pos, float size, Color color)
        {
            GameObject trail = new GameObject("GemTrail");
            trail.transform.SetParent(parent, false);

            var img = trail.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;

            RectTransform rt = trail.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = pos;

            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                img.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
                rt.localScale = Vector3.one * (1f - t * 0.5f);
                yield return null;
            }

            Destroy(trail);
        }

        /// <summary>
        /// 보석 도착 플래시
        /// </summary>
        private IEnumerator GemArrivalFlash(Transform parent, Vector2 pos, Color color)
        {
            GameObject flash = new GameObject("GemArrivalFlash");
            flash.transform.SetParent(parent, false);

            var img = flash.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0.8f);

            RectTransform rt = flash.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(8f, 8f);
            rt.anchoredPosition = pos;

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float s = 1f + t * 4f;
                rt.sizeDelta = new Vector2(8f * s, 8f * s);
                img.color = new Color(color.r, color.g, color.b, 0.8f * (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 카운트 텍스트 도착 펄스
        /// </summary>
        private IEnumerator CountTextPulse()
        {
            if (missionCountText == null) yield break;

            RectTransform rt = missionCountText.GetComponent<RectTransform>();
            Color origColor = missionCountText.color;

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = Vector3.one * scale;

                // 밝은 노란색 → 원래색
                missionCountText.color = Color.Lerp(new Color(1f, 0.95f, 0.5f), origColor, t);

                yield return null;
            }

            rt.localScale = Vector3.one;
            missionCountText.color = origColor;
        }

        /// <summary>
        /// 미션 행의 아이콘/수량 메트릭(몬스터면 얼굴 2배 + 수량 우측 이동) 일괄 계산.
        /// 모든 미션 UI 경로(활성/단일/대기/미리보기)가 공용으로 사용 → 중복 제거 + 일관성 보장.
        /// 비몬스터는 base 값 그대로(기존 동작 유지). 반환값에 스케일 반영.
        /// </summary>
        public struct MissionRowMetrics { public float iconSize, iconX, countX, countW, containerWidth; }

        public static MissionRowMetrics GetMissionRowMetrics(MissionData mission, float scale,
            float baseIcon = 60f, float baseIconX = 13f, float baseCountX = 75f, float baseCountW = 112f, float baseWidth = 196f,
            bool unifiedBigIcon = false)
        {
            // ★ unifiedBigIcon=true면 미션 타입 무관 모두 '큰 아이콘 2배 + 우측 카운트' 레이아웃으로 통일
            //   (활성/대기 미션 — 블록 제거 미션을 몬스터 제거 미션과 동일 위치로 맞춤, 사용자 요청).
            //   false면 기존 동작(몬스터만 2배) — NEXT 미리보기 컴팩트 레이아웃 보존용.
            bool isMon = mission != null && mission.type == MissionType.RemoveEnemy;
            bool bigIcon = unifiedBigIcon || isMon;
            var m = new MissionRowMetrics();
            float fullIcon = (bigIcon ? baseIcon * 2f : baseIcon) * scale;
            // ★ 블록(비몬스터) 미션 아이콘은 10% 축소해 미션 UI 틀 안에 들어가게 (몬스터 얼굴은 살짝 넘침 허용 — 그대로)
            m.iconSize = (bigIcon && !isMon) ? fullIcon * 0.9f : fullIcon;
            m.iconX = baseIconX * scale + (fullIcon - m.iconSize) * 0.5f; // 축소분 좌우 중앙 정렬
            float gap = 12f * scale;
            m.countX = bigIcon ? (baseIconX * scale + fullIcon + gap) : (baseCountX * scale);  // 카운트는 풀사이즈 기준(몬스터와 정렬 유지)
            m.countW = (bigIcon ? 64f : baseCountW) * scale;             // 큰 아이콘이면 수량 칸 폭 축소(겹침 방지)
            m.containerWidth = bigIcon ? (m.countX + m.countW + 10f * scale) : (baseWidth * scale);
            return m;
        }

        /// <summary>
        /// 미션 패널 다크 글래스 배경색 (게임 우주 톤과 조화) — 스프라이트 로드 실패 시 폴백색.
        /// </summary>
        private static readonly Color MissionPanelDark = new Color(0.086f, 0.118f, 0.22f, 0.85f);

        /// <summary>
        /// 미션 패널 공통 배경 — 클로드 디자인 다크글래스 9-slice 스프라이트(UI/mission_panel) 적용.
        /// MOVES 프레임과 동일한 다크블루 글래스+스틸블루 림 톤으로 인게임 HUD와 어울리게 통일.
        /// 활성=불투명(α≈0.96), NEXT/대기=낮은 α로 위계 표현. 스프라이트 없으면 MissionPanelDark 폴백.
        /// </summary>
        /// <summary>
        /// UI 그레이스케일 머티리얼(UI/Grayscale 셰이더) — 미션 대기 UI 등을 진짜 회색으로 처리할 때 사용.
        /// Resources/Materials/MissionGrayscale 로드(1회 캐시). 로드 실패 시 null(호출처는 회색 틴트로 폴백).
        /// </summary>
        private static Material _grayscaleMat;
        public static Material GetGrayscaleMaterial()
        {
            if (_grayscaleMat != null) return _grayscaleMat;
            _grayscaleMat = Resources.Load<Material>("Materials/MissionGrayscale");
            return _grayscaleMat;
        }

        /// <summary>root 하위 모든 Image(자식 포함)에 그레이스케일 머티리얼을 적용한다.
        /// skip에 해당하는 GameObject 하위는 건너뜀(예: 컬러 배지). 머티리얼 없으면 회색 틴트 폴백.</summary>
        public static void ApplyGrayscaleToImages(GameObject root, GameObject skip = null)
        {
            if (root == null) return;
            var mat = GetGrayscaleMaterial();
            var imgs = root.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img == null) continue;
                if (skip != null && img.transform.IsChildOf(skip.transform)) continue;
                if (mat != null) img.material = mat;
                else img.color = new Color(img.color.r * 0.45f + 0.3f, img.color.g * 0.45f + 0.31f, img.color.b * 0.45f + 0.33f, img.color.a); // 폴백: 탈채도 근사
            }
        }

        public static void ApplyMissionPanelSprite(Image img, float alpha = 0.96f)
        {
            if (img == null) return;
            var spr = Resources.Load<Sprite>("UI/mission_panel");
            if (spr != null)
            {
                img.sprite = spr;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                // 폴백: 기존 평면 다크글래스 (알파만 반영)
                img.color = new Color(MissionPanelDark.r, MissionPanelDark.g, MissionPanelDark.b, MissionPanelDark.a * alpha);
            }
        }

        /// <summary>미션 종류별 액센트 색 (좌측 보더 + 진행바 fill).</summary>
        private static Color GetMissionAccentColor(MissionData mission)
        {
            if (mission == null) return new Color(1f, 1f, 1f, 0.8f);
            if (mission.type == MissionType.RemoveEnemy) return new Color(1f, 0.62f, 0.24f); // 몬스터 = 주황
            if (mission.targetGemType != GemType.None) return GemColors.GetColor(mission.targetGemType);
            return new Color(1f, 1f, 1f, 0.8f);
        }

        /// <summary>
        /// 미션 행 공통 비주얼 부착: 좌측 액센트 스트립 + 하단 진행바(track/fill).
        /// fill의 anchorMax.x를 진행률로 갱신한다 (UpdateMissionRowProgress).
        /// </summary>
        private static RectTransform AttachMissionRowVisuals(GameObject rowObj, MissionData mission, float scale)
        {
            Color accent = GetMissionAccentColor(mission);

            // 좌측 액센트 스트립 (4px, 세로 풀스트레치)
            GameObject accentObj = new GameObject("AccentBar");
            accentObj.transform.SetParent(rowObj.transform, false);
            RectTransform aRt = accentObj.AddComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0, 0);
            aRt.anchorMax = new Vector2(0, 1);
            aRt.pivot = new Vector2(0, 0.5f);
            // ★ 세로 라인(액센트)을 미션 패널 "바깥 왼쪽"으로 빼서 3px 간격으로 분리(사용자 요청).
            //   pivot (0,0.5)라 anchoredPosition.x=액센트 왼쪽 끝. 폭 w이면 오른쪽 끝 = x+w. 패널 좌단(x=0)에서 3px 떨어지게 x = -(w+3).
            //   공유 메서드라 모든 행/열(2번째 줄 포함)에 동일 적용.
            float accentW = Mathf.Max(3f, 4f * scale);
            aRt.anchoredPosition = new Vector2(-(accentW + 3f), 0f);
            aRt.sizeDelta = new Vector2(accentW, 0);
            Image aImg = accentObj.AddComponent<Image>();
            aImg.color = accent;
            aImg.raycastTarget = false;

            // 하단 진행바 트랙 (가로 풀스트레치 - 좌우 마진)
            GameObject trackObj = new GameObject("ProgressTrack");
            trackObj.transform.SetParent(rowObj.transform, false);
            RectTransform tRt = trackObj.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0, 0);
            tRt.anchorMax = new Vector2(1, 0);
            tRt.pivot = new Vector2(0.5f, 0);
            tRt.anchoredPosition = new Vector2(0, 6f * scale);
            tRt.sizeDelta = new Vector2(-22f * scale, Mathf.Max(4f, 5f * scale));
            Image tImg = trackObj.AddComponent<Image>();
            tImg.color = new Color(1f, 1f, 1f, 0.15f);
            tImg.raycastTarget = false;

            // 진행바 fill — anchorMax.x = 진행률 (0 시작)
            GameObject fillObj = new GameObject("ProgressFill");
            fillObj.transform.SetParent(trackObj.transform, false);
            RectTransform fRt = fillObj.AddComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero;
            fRt.anchorMax = new Vector2(0f, 1f);
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fRt.pivot = new Vector2(0, 0.5f);
            Image fImg = fillObj.AddComponent<Image>();
            fImg.color = accent;
            fImg.raycastTarget = false;

            return fRt;
        }

        /// <summary>
        /// 미션 행 진행바 갱신 — remaining(남은 개수) 기준으로 fill 비율 계산.
        /// 카운트다운 연출과 같은 틱에서 호출되어 부드럽게 차오른다.
        /// </summary>
        public static void UpdateMissionRowProgress(int idx, int remaining)
        {
            if (idx < 0 || idx >= gameMissionProgressFills.Count) return;
            RectTransform fillRt = gameMissionProgressFills[idx];
            if (fillRt == null) return;
            int target = idx < gameMissionTargets.Count ? gameMissionTargets[idx] : 0;
            if (target <= 0) return;
            float ratio = Mathf.Clamp01(1f - (float)remaining / target);
            fillRt.anchorMax = new Vector2(ratio, 1f);
        }

        /// <summary>
        /// 게임 중 왼쪽 상단에 미션 UI 생성
        /// </summary>
        public void CreateGameMissionUI(Canvas canvas, MissionData mission)
        {
            if (mission == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ★ 공용 헬퍼로 몬스터 얼굴 2배 + 수량 우측 이동 메트릭 계산 (중복 제거)
            var mm = GetMissionRowMetrics(mission, 1f, unifiedBigIcon: true);
            float mIconSize = mm.iconSize, mIconX = mm.iconX, mCountX = mm.countX, mCountW = mm.countW, mWidth = mm.containerWidth;

            // 미션 컨테이너 (레벨 모드와 동일한 컴팩트 레이아웃)
            float rowHeight = 90f;
            GameObject missionObj = new GameObject("GameMissionUI");
            missionObj.transform.SetParent(canvas.transform, false);
            RectTransform missionRt = missionObj.AddComponent<RectTransform>();
            missionRt.anchorMin = new Vector2(0, 1);
            missionRt.anchorMax = new Vector2(0, 1);
            missionRt.pivot = new Vector2(0, 1);
            missionRt.anchoredPosition = new Vector2(20, -20);
            missionRt.sizeDelta = new Vector2(mWidth, rowHeight + 20f);

            // 배경 패널 (클로드 디자인 다크글래스 9-slice — MOVES 프레임과 통일)
            Image bgImage = missionObj.AddComponent<Image>();
            ApplyMissionPanelSprite(bgImage, 0.96f);
            bgImage.raycastTarget = false;

            // ★ 좌측 액센트 + 하단 진행바
            RectTransform progressFill = AttachMissionRowVisuals(missionObj, mission, 1f);
            gameMissionProgressFills.Add(progressFill);
            gameMissionTargets.Add(mission.targetCount);

            // 미션 아이콘 (몬스터면 2배)
            GameObject iconObj = new GameObject("MissionIcon");
            iconObj.transform.SetParent(missionObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0.5f);
            iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(mIconX, 0);
            iconRt.sizeDelta = new Vector2(mIconSize, mIconSize);

            Image iconImage = iconObj.AddComponent<Image>();
            SetMissionIconForType(iconImage, mission);
            iconImage.type = Image.Type.Simple;
            iconImage.raycastTarget = false;

            Outline iconOutline = iconObj.AddComponent<Outline>();
            ApplyMissionIconOutline(iconOutline, mission);

            // 미션 진행도 숫자 (아이콘 옆 — 몬스터면 우측 이동)
            GameObject countObj = new GameObject("Count");
            countObj.transform.SetParent(missionObj.transform, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0, 0.5f);
            countRt.anchorMax = new Vector2(0, 0.5f);
            countRt.pivot = new Vector2(0, 0.5f);
            // 통일 레이아웃: 수량이 우측(2배 아이콘 옆)이라 우하단 '이동 +N' 보상과 가까워짐 → 위로 6px 올려 간격 확보
            countRt.anchoredPosition = new Vector2(mCountX, 6f);
            countRt.sizeDelta = new Vector2(mCountW, 60);

            Text countText = countObj.AddComponent<Text>();
            countText.font = font;
            countText.fontSize = 48;
            countText.fontStyle = FontStyle.Bold;
            countText.alignment = TextAnchor.MiddleLeft;
            countText.color = Color.white;
            countText.raycastTarget = false;
            countText.text = mission.targetCount.ToString();
            // 검은색 아웃라인 (2겹으로 두꺼운 효과)
            Outline countOutline = countObj.AddComponent<Outline>();
            countOutline.effectColor = Color.black;
            countOutline.effectDistance = new Vector2(2, 2);
            Shadow countShadow = countObj.AddComponent<Shadow>();
            countShadow.effectColor = Color.black;
            countShadow.effectDistance = new Vector2(-2, -2);

            // ★ 미션 완료 시 받는 이동 횟수 보상 표시 (우하단 배지)
            AttachMoveRewardBadge(mission, missionObj.transform, 1f);

            // 미션 UI 컨테이너 저장 (나중에 애니메이션에서 사용)
            missionObj.name = "GameMissionUI_Level1";

            // static 필드에 참조 저장 (GameManager에서 접근)
            gameMissionCountText = countText;
            gameMissionIconRect = missionRt;
        }

        /// <summary>
        /// 미션 행 우하단에 "이동 +N" 보상 배지 부착 — Stage 모드 전용.
        /// 미션 1개 완료 시 받는 이동 횟수(MissionBalance.GetOrAssignMoveReward — 미션별 1~5 균일 랜덤)를 가시화한다.
        /// (NEXT 미리보기의 "moves +n" 표기와 동일 스타일/위치 컨벤션)
        /// </summary>
        public void AttachMoveRewardBadge(MissionData mission, Transform rowParent, float scale)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentGameMode != GameMode.Stage) return;
            int reward = MissionBalance.GetOrAssignMoveReward(mission); // 미션별 1~5 균일 랜덤 (이 미션 인스턴스에 1회 롤·저장)
            if (reward <= 0) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject rewardObj = new GameObject("MoveRewardText");
            rewardObj.transform.SetParent(rowParent, false);
            RectTransform rt = rewardObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            // ★ 진행바 위로 올리고 크게 — 보상은 미션의 핵심 동기라 가시성 강화.
            //   우/하단 인셋에 고정 하한(9/10px)을 둬 라운드 코너(9-slice 고정 ~9px)를 확실히 벗어나게
            //   해 텍스트가 패널 밖으로 삐지지 않도록 함(스케일이 작아도 코너 컷아웃 안으로 안 들어감).
            rt.anchoredPosition = new Vector2(-Mathf.Max(9f, 8f * scale), Mathf.Max(10f, 12f * scale));
            rt.sizeDelta = new Vector2(134f * scale, 30f * scale); // 폰트 확대분 여유 (클립 방지)
            Text txt = rewardObj.AddComponent<Text>();
            txt.font = font;
            // ★ 활성 미션 보상 "또렷하게"(사용자 요청): 21→25로 키워 흰 획이 굵어지면 검은 아웃라인 위로 또렷.
            //   (작은 흰 글자+두꺼운 아웃라인이 회색/비활성처럼 보이던 문제 개선)
            txt.fontSize = Mathf.Max(15, Mathf.RoundToInt(25f * scale));
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.LowerRight;
            txt.color = Color.white; // 이동 보상 텍스트 — 흰색 (사용자 요청)
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = $"이동 +{reward}";
            // 크리스프 아웃라인 + 그림자 — 어두운 패널 위에서 흰 글자가 또렷이 떠 보이게(활성 위계 강조).
            Outline ol = rewardObj.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.92f);
            ol.effectDistance = new Vector2(1.5f, 1.5f);
            Shadow sh = rewardObj.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
            sh.effectDistance = new Vector2(-1.5f, -1.5f);
        }

        // ============================================================
        // 미션 완료 이동보상 — 초록 영혼 비행 연출 (VFX 구동: 부딪힐 때 보상 적용)
        // ============================================================
        private static Sprite _soulOrbSprite;
        /// <summary>부드러운 방사형 원(흰색, 색은 Image.color 틴트). 영혼/트레일/임팩트 공용. 1회 생성 캐시.</summary>
        private static Sprite SoulOrbSprite()
        {
            // ★ 버그 수정: 도메인 리로드 비활성 환경에서 static 스프라이트가 Play 세션 간 잔류 →
            //   런타임 텍스처는 Play 종료 시 파괴 → stale(texture 죽음) → 렌더 0. 텍스처 유효성까지 검사.
            if (_soulOrbSprite != null && _soulOrbSprite.texture != null) return _soulOrbSprite;
            const int SZ = 64; float c = (SZ - 1) * 0.5f, r = SZ * 0.5f;
            var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
            var px = new Color[SZ * SZ];
            for (int y = 0; y < SZ; y++)
                for (int x = 0; x < SZ; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                    float a = Mathf.Clamp01(1f - d); a *= a; // 중심 진하고 가장자리 페이드
                    px[y * SZ + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            tex.filterMode = FilterMode.Bilinear; tex.wrapMode = TextureWrapMode.Clamp;
            _soulOrbSprite = Sprite.Create(tex, new Rect(0, 0, SZ, SZ), new Vector2(0.5f, 0.5f), 100f);
            return _soulOrbSprite;
        }

        private Vector2 CanvasLocalOf(Canvas canvas, RectTransform target)
        {
            RectTransform canvasRt = canvas.transform as RectTransform;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, cam, out var local);
            return local;
        }

        /// <summary>
        /// 미션 완료 이동보상 연출: 완료 미션 행의 초록 '이동 +N' 텍스트가 사라지고, 초록 영혼이
        /// 이동횟수 HUD로 자연스럽게 날아가 부딪히며 임팩트와 함께 사라지고, 그 순간 onApply()로
        /// 실제 이동 횟수가 증가한다(정확한 타이밍). 출발/목표를 못 찾으면 즉시 적용(데이터 안전).
        /// </summary>
        public void PlayMoveRewardSoul(int reward, int missionIndex, System.Action onApply)
        {
            if (reward <= 0) { onApply?.Invoke(); return; }
            Canvas canvas = turnText != null ? turnText.canvas : null;
            if (canvas == null || turnText == null) { onApply?.Invoke(); return; }

            RectTransform sourceRt = null;
            if (gameMissionCountTexts != null && missionIndex >= 0 && missionIndex < gameMissionCountTexts.Count
                && gameMissionCountTexts[missionIndex] != null)
            {
                Transform row = gameMissionCountTexts[missionIndex].transform.parent;
                if (row != null)
                {
                    Transform badge = row.Find("MoveRewardText");
                    sourceRt = (badge != null ? badge : gameMissionCountTexts[missionIndex].transform) as RectTransform;
                }
            }
            if (sourceRt == null) { onApply?.Invoke(); return; }

            StartCoroutine(MoveRewardSoulCoroutine(canvas, sourceRt, onApply));
        }

        private IEnumerator MoveRewardSoulCoroutine(Canvas canvas, RectTransform sourceRt, System.Action onApply)
        {
            Color soulCol = Color.white; // 이동 보상 영혼 — 흰색 (사용자 요청)
            Vector2 startPos = CanvasLocalOf(canvas, sourceRt);
            Vector2 endPos = CanvasLocalOf(canvas, turnText.rectTransform);
            Vector2 mid = (startPos + endPos) * 0.5f + new Vector2(0f, 120f); // 위로 솟는 아크

            var badgeText = sourceRt.GetComponent<Text>();
            var badgeOutline = sourceRt.GetComponent<Outline>();

            GameObject soul = new GameObject("MoveRewardSoul");
            soul.transform.SetParent(canvas.transform, false);
            var soulRt = soul.AddComponent<RectTransform>();
            soulRt.sizeDelta = new Vector2(46f, 46f);
            soulRt.anchoredPosition = startPos;
            var soulImg = soul.AddComponent<Image>();
            soulImg.sprite = SoulOrbSprite(); soulImg.color = soulCol; soulImg.raycastTarget = false;
            soul.transform.SetAsLastSibling();
            GameObject glow = new GameObject("Glow"); glow.transform.SetParent(soul.transform, false);
            var glowRt = glow.AddComponent<RectTransform>();
            glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-18f, -18f); glowRt.offsetMax = new Vector2(18f, 18f);
            var glowImg = glow.AddComponent<Image>();
            glowImg.sprite = SoulOrbSprite();
            glowImg.color = new Color(soulCol.r, soulCol.g, soulCol.b, 0.4f); glowImg.raycastTarget = false;
            glow.transform.SetAsFirstSibling();

            float dur = 0.68f, t = 0f; int frame = 0;
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                float u = Mathf.Clamp01(t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f); // easeInOut
                Vector2 p = (1 - u) * (1 - u) * startPos + 2 * (1 - u) * u * mid + u * u * endPos;
                p += new Vector2(Mathf.Sin(t * 22f) * 5f, Mathf.Cos(t * 18f) * 4f) * (1f - u); // 영혼 떨림
                if (soulRt != null)
                {
                    soulRt.anchoredPosition = p;
                    soulRt.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.65f, u) * (1f + Mathf.Sin(u * Mathf.PI) * 0.3f);
                }
                float fade = 1f - Mathf.Clamp01(t * 2.4f);
                if (badgeText != null) { var c = badgeText.color; c.a = fade; badgeText.color = c; }
                if (badgeOutline != null) { var c = badgeOutline.effectColor; c.a = fade * 0.7f; badgeOutline.effectColor = c; }
                if ((frame++ % 2) == 0) SpawnSoulTrail(canvas, p, soulCol);
                yield return null;
            }
            if (badgeText != null) Destroy(badgeText.gameObject);
            if (soul != null) Destroy(soul);

            SpawnImpactBurst(canvas, endPos, soulCol);
            onApply?.Invoke();                       // ★ 정확한 타이밍에 실제 데이터 적용 (숫자 증가)
            yield return StartCoroutine(TurnTextPop());
        }

        private void SpawnSoulTrail(Canvas canvas, Vector2 pos, Color col)
        {
            GameObject g = new GameObject("SoulTrail"); g.transform.SetParent(canvas.transform, false);
            var rt = g.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(30f, 30f); rt.anchoredPosition = pos;
            var img = g.AddComponent<Image>(); img.sprite = SoulOrbSprite();
            img.color = new Color(col.r, col.g, col.b, 0.5f); img.raycastTarget = false;
            g.transform.SetAsLastSibling();
            StartCoroutine(FadeShrinkDestroy(rt, img, 0.35f));
        }
        private IEnumerator FadeShrinkDestroy(RectTransform rt, Image img, float dur)
        {
            float t = 0f; Color c0 = img.color; Vector3 s0 = rt.localScale;
            while (t < 1f && rt != null)
            {
                t += Time.deltaTime / dur;
                if (img != null) { var c = c0; c.a = c0.a * (1f - t); img.color = c; }
                if (rt != null) rt.localScale = s0 * (1f - 0.5f * t);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }
        private void SpawnImpactBurst(Canvas canvas, Vector2 pos, Color col)
        {
            GameObject flash = new GameObject("RewardImpactFlash"); flash.transform.SetParent(canvas.transform, false);
            var frt = flash.AddComponent<RectTransform>(); frt.sizeDelta = new Vector2(44f, 44f); frt.anchoredPosition = pos;
            var fimg = flash.AddComponent<Image>(); fimg.sprite = SoulOrbSprite();
            fimg.color = new Color(col.r, col.g, col.b, 0.85f); fimg.raycastTarget = false;
            flash.transform.SetAsLastSibling();
            StartCoroutine(FlashExpand(frt, fimg, 0.3f));
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                GameObject p = new GameObject("RewardSpark"); p.transform.SetParent(canvas.transform, false);
                var prt = p.AddComponent<RectTransform>(); prt.sizeDelta = new Vector2(13f, 13f); prt.anchoredPosition = pos;
                var pimg = p.AddComponent<Image>(); pimg.sprite = SoulOrbSprite(); pimg.color = col; pimg.raycastTarget = false;
                p.transform.SetAsLastSibling();
                StartCoroutine(SparkFly(prt, pimg, dir * Random.Range(42f, 72f), 0.42f));
            }
        }
        private IEnumerator FlashExpand(RectTransform rt, Image img, float dur)
        {
            float t = 0f; Vector2 s0 = rt.sizeDelta; Color c0 = img.color;
            while (t < 1f && rt != null)
            {
                t += Time.deltaTime / dur;
                rt.sizeDelta = Vector2.Lerp(s0, s0 * 3.2f, t);
                if (img != null) { var c = c0; c.a = c0.a * (1f - t); img.color = c; }
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }
        private IEnumerator SparkFly(RectTransform rt, Image img, Vector2 disp, float dur)
        {
            float t = 0f; Vector2 p0 = rt.anchoredPosition; Color c0 = img.color;
            while (t < 1f && rt != null)
            {
                t += Time.deltaTime / dur;
                float e = 1f - Mathf.Pow(1f - t, 2f);
                rt.anchoredPosition = p0 + disp * e;
                if (img != null) { var c = c0; c.a = c0.a * (1f - t); img.color = c; }
                rt.localScale = Vector3.one * (1f - 0.6f * t);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }
        private IEnumerator TurnTextPop()
        {
            if (turnText == null) yield break;
            var rt = turnText.rectTransform; float t = 0f, dur = 0.32f;
            while (t < 1f && rt != null)
            {
                t += Time.deltaTime / dur;
                rt.localScale = Vector3.one * (1f + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.35f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        // (정리됨) CreateGameMissionUI(MissionData[] missions) 제거 — 호출처 없는 dead code였음.
        //   복수 미션은 GameManager가 CreateIndividualMissionRow로 행 단위 생성하는 경로만 사용.

        /// <summary>
        /// 레벨 모드용 빈 다음 미션 플레이스홀더 생성 (위치만 확보, 내용 없음).
        /// 무한도전과 동일한 레이아웃을 위해 다음 미션 영역을 비워둔다.
        /// </summary>
        public void CreateEmptyNextMissionPlaceholder(Canvas canvas)
        {
            // 기존 플레이스홀더 제거
            GameObject existing = GameObject.Find("NextMissionPlaceholder");
            if (existing != null) Destroy(existing);

            // NextMissionPreview와 동일한 크기/위치 (137×77, 좌상단)
            GameObject placeholderObj = new GameObject("NextMissionPlaceholder");
            placeholderObj.transform.SetParent(canvas.transform, false);
            RectTransform rt = placeholderObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20);
            rt.sizeDelta = new Vector2(137, 77);

            // 배경 없음 (위치 참조용 투명 컨테이너)
            // Image 컴포넌트 없이 RectTransform만 사용

            // nextMissionPreviewRect 설정 (위치 계산에 사용)
            nextMissionPreviewRect = rt;
        }

        /// <summary>
        /// 개별 미션 행 생성 (순차 등장용, 자체 배경 포함).
        /// 복수 미션을 하나씩 등장시킬 때 각 행을 독립적으로 생성.
        /// </summary>
        public RectTransform CreateIndividualMissionRow(Canvas canvas, MissionData mission, int index, int totalMissions = 1)
        {
            if (mission == null) return null;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 미션 3개 이상이면 30% 축소
            float scale = totalMissions >= 3 ? 0.7f : 1.0f;
            float rowHeight = 90f * scale;
            int fontSize = Mathf.RoundToInt(48f * scale);

            // ★ 공용 헬퍼로 몬스터 얼굴 2배 + 수량 우측 이동 메트릭 계산 (중복 제거)
            var rm = GetMissionRowMetrics(mission, scale, unifiedBigIcon: true);
            float iconX = rm.iconX, iconSize = rm.iconSize, countX = rm.countX, countW = rm.countW, containerWidth = rm.containerWidth;

            GameObject rowObj = new GameObject($"GameMissionUI_Row_{index}");
            rowObj.transform.SetParent(canvas.transform, false);
            RectTransform rowRt = rowObj.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0, 1);
            rowRt.anchorMax = new Vector2(0, 1);
            rowRt.pivot = new Vector2(0, 1);
            rowRt.anchoredPosition = new Vector2(40, -102); // 애니메이션에서 오버라이드됨
            rowRt.sizeDelta = new Vector2(containerWidth, rowHeight + 20f * scale);

            // 배경 패널 (클로드 디자인 다크글래스 9-slice — MOVES 프레임과 통일)
            Image bgImage = rowObj.AddComponent<Image>();
            ApplyMissionPanelSprite(bgImage, 0.96f);
            bgImage.raycastTarget = false;

            // ★ 좌측 액센트 + 하단 진행바
            RectTransform progressFill = AttachMissionRowVisuals(rowObj, mission, scale);
            gameMissionProgressFills.Add(progressFill);
            gameMissionTargets.Add(mission.targetCount);

            // 미션 아이콘
            GameObject iconObj = new GameObject("MissionIcon");
            iconObj.transform.SetParent(rowObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0.5f);
            iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(iconX, 0);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);

            Image iconImage = iconObj.AddComponent<Image>();
            SetMissionIconForType(iconImage, mission);
            iconImage.type = Image.Type.Simple;
            iconImage.raycastTarget = false;

            Outline iconOutline = iconObj.AddComponent<Outline>();
            ApplyMissionIconOutline(iconOutline, mission);

            // 미션 진행도 숫자 (아이콘 옆)
            GameObject countObj = new GameObject("Count");
            countObj.transform.SetParent(rowObj.transform, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0, 0.5f);
            countRt.anchorMax = new Vector2(0, 0.5f);
            countRt.pivot = new Vector2(0, 0.5f);
            // 통일 레이아웃: 수량을 위로 6px 올려 우하단 '이동 +N' 보상과 간격 확보 (전 미션 타입 동일)
            countRt.anchoredPosition = new Vector2(countX, 6f);
            countRt.sizeDelta = new Vector2(countW, iconSize);

            Text countText = countObj.AddComponent<Text>();
            countText.font = font;
            countText.fontSize = fontSize;
            countText.fontStyle = FontStyle.Bold;
            countText.alignment = TextAnchor.MiddleLeft;
            countText.color = Color.white;
            countText.raycastTarget = false;
            countText.text = mission.targetCount.ToString();

            // 검은색 아웃라인 (2겹으로 두꺼운 효과)
            Outline countOutline = countObj.AddComponent<Outline>();
            countOutline.effectColor = Color.black;
            countOutline.effectDistance = new Vector2(2, 2);
            Shadow countShadow = countObj.AddComponent<Shadow>();
            countShadow.effectColor = Color.black;
            countShadow.effectDistance = new Vector2(-2, -2);

            // ★ 미션 완료 시 받는 이동 횟수 보상 표시 (우하단 배지)
            AttachMoveRewardBadge(mission, rowObj.transform, scale);

            // 카운트 텍스트 리스트에 추가
            gameMissionCountTexts.Add(countText);

            // 첫 번째 행: 단일 미션 호환용 static 필드에도 저장
            if (index == 0)
            {
                gameMissionCountText = countText;
                gameMissionIconRect = rowRt;
            }

            return rowRt;
        }

        /// <summary>
        /// 다음 미션 미리보기 UI 생성 (현재 미션 대비 70% 크기, 좌상단)
        /// </summary>
        public void CreateNextMissionPreviewUI(Canvas canvas, MissionData nextMission, int reward)
        {
            if (nextMission == null) return;

            // 기존 미리보기 제거
            GameObject existing = GameObject.Find("NextMissionPreview");
            if (existing != null) Destroy(existing);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ★ 공용 헬퍼로 몬스터 얼굴 2배 + 수량 우측 이동 (NEXT 미리보기 base 49/67/110/137)
            var pm = GetMissionRowMetrics(nextMission, 1f, baseIcon: 49f, baseIconX: 10f, baseCountX: 67f, baseCountW: 110f, baseWidth: 137f);
            float pIconSize = pm.iconSize, pIconX = pm.iconX, pCountX = pm.countX, pCountW = pm.countW, previewW = pm.containerWidth;

            // 컨테이너 (현재 미션 196×110의 70% = 137×77; 몬스터면 폭 확장)
            GameObject previewObj = new GameObject("NextMissionPreview");
            previewObj.transform.SetParent(canvas.transform, false);
            RectTransform previewRt = previewObj.AddComponent<RectTransform>();
            previewRt.anchorMin = new Vector2(0, 1);
            previewRt.anchorMax = new Vector2(0, 1);
            previewRt.pivot = new Vector2(0, 1);
            previewRt.anchoredPosition = new Vector2(20, -20);
            previewRt.sizeDelta = new Vector2(previewW, 77);

            // 배경 (클로드 디자인 다크글래스 9-slice — 활성 미션과 동일 스프라이트, 낮은 α로 NEXT 위계 표현)
            Image bgImage = previewObj.AddComponent<Image>();
            ApplyMissionPanelSprite(bgImage, 0.62f);
            bgImage.raycastTarget = false;

            // "NEXT" 라벨 (좌상단)
            GameObject labelObj = new GameObject("NextLabel");
            labelObj.transform.SetParent(previewObj.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0, 1);
            labelRt.anchorMax = new Vector2(0, 1);
            labelRt.pivot = new Vector2(0, 1);
            labelRt.anchoredPosition = new Vector2(4, -2);
            labelRt.sizeDelta = new Vector2(50, 16);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 12;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.UpperLeft;
            labelText.color = new Color(1f, 1f, 1f, 0.7f);
            labelText.raycastTarget = false;
            labelText.text = "NEXT";
            Outline labelOutline = labelObj.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            labelOutline.effectDistance = new Vector2(1, 1);

            // 미션 아이콘 (70% = 49×49)
            GameObject iconObj = new GameObject("NextMissionIcon");
            iconObj.transform.SetParent(previewObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0.5f);
            iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(pIconX, 0);
            iconRt.sizeDelta = new Vector2(pIconSize, pIconSize);

            Image iconImage = iconObj.AddComponent<Image>();
            SetMissionIconForType(iconImage, nextMission);
            iconImage.type = Image.Type.Simple;
            iconImage.raycastTarget = false;
            Outline iconOutline = iconObj.AddComponent<Outline>();
            iconOutline.effectColor = Color.white;
            iconOutline.effectDistance = new Vector2(1, 1);

            // 카운트 텍스트 (70% fontSize = 34)
            GameObject countObj = new GameObject("NextCount");
            countObj.transform.SetParent(previewObj.transform, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0, 0.5f);
            countRt.anchorMax = new Vector2(0, 0.5f);
            countRt.pivot = new Vector2(0, 0.5f);
            // 몬스터 미션: 수량을 위로 6px 올려 우하단 'moves +N' 보상과 간격 확보 (활성 미션과 동일)
            countRt.anchoredPosition = new Vector2(pCountX, nextMission.type == MissionType.RemoveEnemy ? 6f : 0f);
            countRt.sizeDelta = new Vector2(pCountW, 49);
            Text countText = countObj.AddComponent<Text>();
            countText.font = font;
            countText.fontSize = 34;
            countText.fontStyle = FontStyle.Bold;
            countText.alignment = TextAnchor.MiddleLeft;
            countText.color = new Color(1f, 1f, 1f, 0.8f);
            countText.raycastTarget = false;
            countText.text = nextMission.targetCount.ToString();
            Outline countOutline = countObj.AddComponent<Outline>();
            countOutline.effectColor = Color.black;
            countOutline.effectDistance = new Vector2(1, 1);

            // 보상 텍스트 "moves +n" (우하단, 작은 크기)
            GameObject rewardObj = new GameObject("NextRewardText");
            rewardObj.transform.SetParent(previewObj.transform, false);
            RectTransform rewardRt = rewardObj.AddComponent<RectTransform>();
            rewardRt.anchorMin = new Vector2(1, 0);
            rewardRt.anchorMax = new Vector2(1, 0);
            rewardRt.pivot = new Vector2(1, 0);
            // 우/하단 인셋을 라운드 코너(~9px)보다 크게 둬 텍스트가 패널 밖으로 안 삐지게
            rewardRt.anchoredPosition = new Vector2(-8f, 9f);
            rewardRt.sizeDelta = new Vector2(90, 16f);
            Text rewardText = rewardObj.AddComponent<Text>();
            rewardText.font = font;
            rewardText.fontSize = 13;
            rewardText.fontStyle = FontStyle.Bold;
            rewardText.alignment = TextAnchor.LowerRight;
            rewardText.color = new Color(0.2f, 1f, 0.4f, 0.7f);
            rewardText.raycastTarget = false;
            rewardText.text = $"moves +{reward}";
            Outline rewardOutline = rewardObj.AddComponent<Outline>();
            rewardOutline.effectColor = new Color(0f, 0f, 0f, 0.4f);
            rewardOutline.effectDistance = new Vector2(1, 1);

            nextMissionPreviewRect = previewRt;
            Debug.Log($"[UIManager] 다음 미션 미리보기 UI 생성: {nextMission.targetCount}개");
        }

        /// <summary>
        /// 무한도전 미션 보상 텍스트 표시 ("+N" 형태)
        /// </summary>
        public void SetMissionRewardText(int reward)
        {
            if (gameMissionIconRect == null) return;

            // 기존 보상 텍스트 제거
            if (gameMissionRewardText != null)
                Destroy(gameMissionRewardText.gameObject);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 프레임 오른쪽 하단에 "moves +n" 표시
            GameObject rewardObj = new GameObject("RewardText");
            rewardObj.transform.SetParent(gameMissionIconRect, false);
            RectTransform rewardRt = rewardObj.AddComponent<RectTransform>();
            rewardRt.anchorMin = new Vector2(1, 0);
            rewardRt.anchorMax = new Vector2(1, 0);
            rewardRt.pivot = new Vector2(1, 0);
            rewardRt.anchoredPosition = new Vector2(-8f, 6f);
            rewardRt.sizeDelta = new Vector2(120, 22f);

            gameMissionRewardText = rewardObj.AddComponent<Text>();
            gameMissionRewardText.font = font;
            gameMissionRewardText.fontSize = 18;
            gameMissionRewardText.fontStyle = FontStyle.Bold;
            gameMissionRewardText.alignment = TextAnchor.LowerRight;
            gameMissionRewardText.color = new Color(0.2f, 1f, 0.4f, 1f); // 밝은 녹색
            gameMissionRewardText.raycastTarget = false;
            gameMissionRewardText.text = $"moves +{reward}";

            Outline outline = rewardObj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, 1);
        }

        /// <summary>
        /// 게임 미션 UI 정리 (로비 전환 시 호출)
        /// </summary>
        public void CleanupGameMissionUI()
        {
            // ★ 모든 미션 UI 오브젝트를 캔버스 자식에서 이름으로 일괄 수거 후 제거.
            //   (기존 코드는 행 루프에 else break가 있어 비연속/중복 행이 남아 얼굴이 중첩됐음 +
            //    "GameMissionUI"(단일 패널)는 아예 정리 대상에 없었음 → 둘 다 해결)
            Canvas cv = FindObjectOfType<Canvas>();
            if (cv != null)
            {
                var toDestroy = new System.Collections.Generic.List<GameObject>();
                foreach (Transform child in cv.transform)
                {
                    string n = child.name;
                    if (n == "GameMissionUI" || n == "GameMissionUI_Level1" || n == "GameMissionUI_Multi"
                        || n.StartsWith("GameMissionUI_Row_")
                        || n == "GameMissionUI_LockChips" // ★ 동적 한도 잠금 슬롯 칩
                        || n == "NextMissionPreview" || n == "NextMissionPlaceholder")
                        toDestroy.Add(child.gameObject);
                }
                foreach (var go in toDestroy) Destroy(go);
            }
            else
            {
                // 폴백(캔버스 미발견): 이름 기반 Find — 행은 0~9 전부 검사(break 없음)
                DestroyMissionObjByName("GameMissionUI");
                DestroyMissionObjByName("GameMissionUI_Level1");
                DestroyMissionObjByName("GameMissionUI_Multi");
                DestroyMissionObjByName("NextMissionPreview");
                DestroyMissionObjByName("NextMissionPlaceholder");
                DestroyMissionObjByName("GameMissionUI_LockChips"); // ★ 동적 한도 잠금 슬롯 칩
                for (int i = 0; i < 10; i++) DestroyMissionObjByName($"GameMissionUI_Row_{i}");
            }

            // static 필드 초기화
            gameMissionCountText = null;
            gameMissionIconRect = null;
            gameMissionRewardText = null;
            gameMissionCountTexts.Clear();
            gameMissionProgressFills.Clear();
            gameMissionTargets.Clear();
            gameMissionContainerRect = null;
            nextMissionPreviewRect = null;
        }

        private void DestroyMissionObjByName(string n)
        {
            GameObject go = GameObject.Find(n);
            if (go != null) Destroy(go);
        }

        /// <summary>
        /// 미션 UI 등장 애니메이션 (왼쪽에서 슬라이드인 + 스케일 펀치)
        /// </summary>
        // 미션 등장 애니메이션은 GameManager.AnimateMissionEntranceCoroutine에서 처리

        /// <summary>
        /// 미션 아이콘 아웃라인 색상/두께 결정.
        /// - 새 몬스터가 첫 등장하는 스테이지: <b>노란색 + 두꺼운 아웃라인</b>으로 강조
        /// - 그 외: 기본 흰색 아웃라인
        /// </summary>
        public void ApplyMissionIconOutline(Outline outline, MissionData mission)
        {
            if (outline == null) return;

            // ★ 몬스터 얼굴 PNG 미션은 Outline 전부 비활성화 — Outline이 얼굴 이미지를 복제해
            //   여러 장 겹쳐 보이는 문제. 외곽선은 추후 이미지 자체에 구워서 적용 예정.
            //   같은 GameObject에 붙은 모든 Outline 컴포넌트를 끈다.
            if (mission != null && mission.type == MissionType.RemoveEnemy
                && GoblinSystem.GetGoblinFaceSprite(mission.targetEnemyType) != null)
            {
                foreach (var ol in outline.gameObject.GetComponents<Outline>())
                {
                    ol.effectColor = new Color(0f, 0f, 0f, 0f);
                    ol.effectDistance = Vector2.zero;
                    ol.enabled = false;
                }
                return;
            }

            bool isNewMonsterStage = false;
            if (mission != null && mission.type == MissionType.RemoveEnemy)
            {
                int curStage = GameManager.Instance != null ? GameManager.Instance.CurrentStage : 0;
                if (curStage > 0)
                    isNewMonsterStage = MonsterFirstAppearance.IsFirstAppearance(curStage, mission.targetEnemyType);
            }

            outline.enabled = true;  // 얼굴 미션에서 꺼졌을 수 있으므로 복원
            if (isNewMonsterStage)
            {
                // 신규 몬스터 강조: 황금빛 노란색 + 두꺼운 아웃라인
                outline.effectColor = new Color(1.0f, 0.85f, 0.10f, 1.0f);
                outline.effectDistance = new Vector2(3, 3);
            }
            else
            {
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(2, 2);
            }
        }

        /// <summary>
        /// 미션 타입에 따라 적절한 아이콘을 Image에 적용
        /// </summary>
        // ★ 미션 아이콘 캐시 (감사 M7) — 호출마다 256×256 프로시저럴 텍스처(262KB)를 새로 만들고
        //   이전 스프라이트를 Destroy하지 않아 스테이지 로드당 ~0.5MB씩 누수되던 것을 키별 1회 생성으로 교체.
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _missionIconCache
            = new System.Collections.Generic.Dictionary<string, Sprite>();
        private static Sprite CachedIcon(string key, System.Func<Sprite> creator)
        {
            if (_missionIconCache.TryGetValue(key, out var s) && s != null) return s;
            s = creator();
            _missionIconCache[key] = s;
            return s;
        }

        public void SetMissionIconForType(Image iconImage, MissionData mission)
        {
            MissionType mType = mission.type;
            GemType gemType = mission.targetGemType;

            // ★ 재호출 시 이전 오버레이 자식(ShieldOverlay/SpecialOverlay/HealCross 등) 잔존 → 얼굴/아이콘 중첩 방지
            for (int ci = iconImage.transform.childCount - 1; ci >= 0; ci--)
                Destroy(iconImage.transform.GetChild(ci).gameObject);
            iconImage.color = Color.white;
            iconImage.rectTransform.localScale = Vector3.one; // 마법사 등 scale 반전 잔재 초기화

            if (mType == MissionType.CollectGem || mType == MissionType.CollectMultiGem)
            {
                if (gemType != GemType.None)
                {
                    Sprite gemSprite = GemSpriteProvider.GetGemSprite(gemType);
                    if (gemSprite != null)
                    {
                        iconImage.sprite = gemSprite;
                        if (GemSpriteProvider.NeedsTinting(gemType))
                            iconImage.color = GemColors.GetColor(gemType);
                        else
                            iconImage.color = Color.white;
                    }
                    else
                    {
                        iconImage.sprite = CachedIcon("SingleHex_" + gemType, () => CreateSingleColorHexIcon(GemColors.GetColor(gemType)));
                    }
                }
                else
                {
                    Sprite missionIcon = Resources.Load<Sprite>("Icons/MissionIcon");
                    if (missionIcon != null)
                        iconImage.sprite = missionIcon;
                    else
                        iconImage.sprite = CachedIcon("Mission", CreateProceduralMissionIcon);
                }
            }
            else if (mType == MissionType.ProcessGem)
            {
                if (gemType != GemType.None)
                    iconImage.sprite = CachedIcon("Process_" + gemType, () => CreateProcessGemIcon(GemColors.GetColor(gemType)));
                else
                    iconImage.sprite = CachedIcon("Process_default", () => CreateProcessGemIcon(new Color(0.9f, 0.85f, 0.3f)));
            }
            else if (mType == MissionType.CreateSpecialGem)
            {
                iconImage.sprite = CachedIcon("SpecialGem", CreateSpecialGemIcon);
                iconImage.color = Color.white;
            }
            // === 특수 블록별 생성 미션 아이콘 (6색 육각형 배경 + 특수 블록 오버레이) ===
            else if (mType == MissionType.CreateDrillVertical ||
                     mType == MissionType.CreateDrillSlash ||
                     mType == MissionType.CreateDrillBackSlash ||
                     mType == MissionType.CreateDrillAny ||
                     mType == MissionType.CreateBomb ||
                     mType == MissionType.CreateRainbow ||
                     mType == MissionType.CreateXBlock ||
                     mType == MissionType.CreateDrone)
            {
                // 배경: 6색 육각형 기본 블록
                iconImage.sprite = CachedIcon("MultiHex", MissionUIHelper.CreateMultiColorHexagonSprite);
                iconImage.color = Color.white;

                // 오버레이: 특수 블록 아이콘을 위에 겹침
                Sprite overlaySprite = null;
                switch (mType)
                {
                    case MissionType.CreateDrillVertical:
                        overlaySprite = HexBlock.GetDrillIconSprite(DrillDirection.Vertical); break;
                    case MissionType.CreateDrillSlash:
                        overlaySprite = HexBlock.GetDrillIconSprite(DrillDirection.Slash); break;
                    case MissionType.CreateDrillBackSlash:
                        overlaySprite = HexBlock.GetDrillIconSprite(DrillDirection.BackSlash); break;
                    case MissionType.CreateDrillAny:
                        overlaySprite = HexBlock.GetDrillAnyIconSprite(); break;
                    case MissionType.CreateBomb:
                        overlaySprite = BombBlockSystem.GetBombIconSprite(); break;
                    case MissionType.CreateRainbow:
                        overlaySprite = DonutBlockSystem.GetDonutIconSprite(); break;
                    case MissionType.CreateXBlock:
                        overlaySprite = XBlockSystem.GetXBlockIconSprite(); break;
                    case MissionType.CreateDrone:
                        overlaySprite = DroneBlockSystem.GetDroneIconSprite(); break;
                }

                if (overlaySprite != null)
                {
                    GameObject overlayObj = new GameObject("SpecialOverlay");
                    overlayObj.transform.SetParent(iconImage.transform, false);
                    RectTransform overlayRt = overlayObj.AddComponent<RectTransform>();
                    overlayRt.anchorMin = Vector2.zero;
                    overlayRt.anchorMax = Vector2.one;
                    overlayRt.sizeDelta = Vector2.zero;
                    overlayRt.anchoredPosition = Vector2.zero;
                    Image overlayImg = overlayObj.AddComponent<Image>();
                    overlayImg.sprite = overlaySprite;
                    overlayImg.color = Color.white;
                    overlayImg.raycastTarget = false;
                }
            }
            else if (mType == MissionType.CreatePerfectGem)
            {
                iconImage.sprite = CachedIcon("PerfectGem", CreatePerfectGemIcon);
                iconImage.color = Color.white;
            }
            else if (mType == MissionType.TriggerBigBang)
            {
                Sprite bombIcon = Resources.Load<Sprite>("Icons/icon_bomb");
                if (bombIcon != null)
                {
                    iconImage.sprite = bombIcon;
                    iconImage.color = new Color(1f, 0.6f, 0.1f);
                }
                else
                {
                    iconImage.sprite = CachedIcon("Explosion", CreateExplosionIcon);
                }
            }
            else if (mType == MissionType.RemoveVinyl || mType == MissionType.RemoveDoubleVinyl)
            {
                iconImage.sprite = CachedIcon("Vinyl_" + (mType == MissionType.RemoveDoubleVinyl), () => CreateVinylIcon(mType == MissionType.RemoveDoubleVinyl));
            }
            else if (mType == MissionType.ReachScore)
            {
                iconImage.sprite = CachedIcon("ScoreTarget", CreateScoreTargetIcon);
                iconImage.color = Color.white;
            }
            else if (mType == MissionType.RemoveEnemy)
            {
                // ★ 몬스터 미션 아이콘: 얼굴 클로즈업 이미지 우선 (Resources/Goblins/face_*.png)
                //   얼굴 PNG가 있는 10종은 정면 얼굴로 표시(틴트/뒤집기/오버레이 불필요),
                //   얼굴 없는 타입(Lv2 등)은 기존 전신 스프라이트 분기로 폴백.
                Sprite faceSprite = GoblinSystem.GetGoblinFaceSprite(mission.targetEnemyType);
                // 적군 타입별 아이콘 분기
                if (faceSprite != null)
                {
                    iconImage.sprite = faceSprite;
                    iconImage.color = Color.white;
                    iconImage.preserveAspect = true;
                }
                else if (mission.targetEnemyType == EnemyType.ArcherGoblin)
                {
                    iconImage.sprite = GoblinSystem.GetArcherGoblinSprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.ArmoredGoblin)
                {
                    iconImage.sprite = GoblinSystem.GetArmoredGoblinSprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.ShieldGoblin)
                {
                    iconImage.sprite = GoblinSystem.GetShieldGoblinSprite();
                    // 방패 오버레이 추가 (고블린 앞에 방패 표시)
                    Sprite shieldSprite = GoblinSystem.GetShieldSprite();
                    if (shieldSprite != null)
                    {
                        GameObject shieldOverlay = new GameObject("ShieldOverlay");
                        shieldOverlay.transform.SetParent(iconImage.transform, false);
                        RectTransform shieldRt = shieldOverlay.AddComponent<RectTransform>();
                        shieldRt.anchorMin = new Vector2(0.5f, 0.5f);
                        shieldRt.anchorMax = new Vector2(0.5f, 0.5f);
                        shieldRt.pivot = new Vector2(0.5f, 0.5f);
                        shieldRt.anchoredPosition = new Vector2(0f, -8f); // 고블린 앞(아래쪽)에 배치
                        shieldRt.sizeDelta = new Vector2(40f, 44f);
                        Image shieldImg = shieldOverlay.AddComponent<Image>();
                        shieldImg.sprite = shieldSprite;
                        shieldImg.color = Color.white;
                        shieldImg.raycastTarget = false;
                    }
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.BombGoblin)
                {
                    iconImage.sprite = GoblinSystem.GetBombGoblinSprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.HealerGoblin)
                {
                    // 힐러: 일반 고블린 스프라이트 + 연두색 틴트 + 십자 마크
                    iconImage.sprite = GoblinSystem.GetGoblinSprite();
                    iconImage.color = new Color(0.3f, 0.9f, 0.4f, 1f);

                    // 십자 마크 오버레이
                    GameObject crossH = new GameObject("HealCrossH");
                    crossH.transform.SetParent(iconImage.transform, false);
                    RectTransform chRt = crossH.AddComponent<RectTransform>();
                    chRt.anchoredPosition = new Vector2(0f, 4f);
                    chRt.sizeDelta = new Vector2(20f, 6f);
                    Image chImg = crossH.AddComponent<Image>();
                    chImg.color = new Color(1f, 1f, 1f, 0.9f);
                    chImg.raycastTarget = false;

                    GameObject crossV = new GameObject("HealCrossV");
                    crossV.transform.SetParent(iconImage.transform, false);
                    RectTransform cvRt = crossV.AddComponent<RectTransform>();
                    cvRt.anchoredPosition = new Vector2(0f, 4f);
                    cvRt.sizeDelta = new Vector2(6f, 20f);
                    Image cvImg = crossV.AddComponent<Image>();
                    cvImg.color = new Color(1f, 1f, 1f, 0.9f);
                    cvImg.raycastTarget = false;
                }
                else if (mission.targetEnemyType == EnemyType.HeavyGoblin)
                {
                    // 헤비급: 전용 스프라이트 (진한 갈색 + 뿔) 사용
                    iconImage.sprite = GoblinSystem.GetHeavyGoblinSprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.WizardGoblin)
                {
                    // 마법사: 전용 스프라이트 + 스케일 반전 (pivot 중앙 기준)
                    iconImage.sprite = GoblinSystem.GetWizardGoblinSprite();
                    iconImage.color = Color.white;
                    // pivot을 중앙으로 변경하여 스케일 반전 시 위치 어긋남 방지
                    iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    iconImage.rectTransform.anchoredPosition = new Vector2(32f, 0f);
                    iconImage.rectTransform.sizeDelta = new Vector2(54f, 54f);
                    iconImage.rectTransform.localScale = new Vector3(-1f, -1f, 1f);
                }
                else if (mission.targetEnemyType == EnemyType.ThiefGoblin)
                {
                    // 도둑 고블린: 전용 스프라이트
                    iconImage.sprite = GoblinSystem.GetThiefGoblinSprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.WitchGoblin)
                {
                    // 마녀 고블린: 전용 스프라이트 + 스케일 반전
                    iconImage.sprite = GoblinSystem.GetWitchGoblinSprite();
                    iconImage.color = Color.white;
                    iconImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    iconImage.rectTransform.anchoredPosition = new Vector2(32f, 0f);
                    iconImage.rectTransform.sizeDelta = new Vector2(54f, 54f);
                    iconImage.rectTransform.localScale = new Vector3(-1f, -1f, 1f);
                }
                else if (mission.targetEnemyType == EnemyType.Goblin)
                {
                    iconImage.sprite = GoblinSystem.GetGoblinSprite();
                    iconImage.color = Color.white;
                }
                // === Lv2 몬스터 아이콘 (기존 스프라이트 + 색상 틴트로 구분) ===
                // === Lv2 몬스터 아이콘 (Lv2 전용 스프라이트 — 피부색만 붉은색) ===
                else if (mission.targetEnemyType == EnemyType.GoblinLv2)
                {
                    iconImage.sprite = GoblinSystem.GetGoblinLv2Sprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.ArmoredGoblinLv2)
                {
                    iconImage.sprite = GoblinSystem.GetArmoredLv2Sprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.ArcherGoblinLv2)
                {
                    iconImage.sprite = GoblinSystem.GetArcherLv2Sprite();
                    iconImage.color = Color.white;
                }
                else if (mission.targetEnemyType == EnemyType.ShieldGoblinLv2)
                {
                    iconImage.sprite = GoblinSystem.GetShieldLv2Sprite();
                    iconImage.color = Color.white;
                    // 방패 오버레이
                    Sprite shieldSprite = GoblinSystem.GetShieldSprite();
                    if (shieldSprite != null)
                    {
                        GameObject shieldOverlay = new GameObject("ShieldOverlayLv2");
                        shieldOverlay.transform.SetParent(iconImage.transform, false);
                        RectTransform shieldRt = shieldOverlay.AddComponent<RectTransform>();
                        shieldRt.anchorMin = new Vector2(0.5f, 0.5f);
                        shieldRt.anchorMax = new Vector2(0.5f, 0.5f);
                        shieldRt.pivot = new Vector2(0.5f, 0.5f);
                        shieldRt.anchoredPosition = new Vector2(0f, -8f);
                        shieldRt.sizeDelta = new Vector2(40f, 44f);
                        Image shieldImg = shieldOverlay.AddComponent<Image>();
                        shieldImg.sprite = shieldSprite;
                        shieldImg.color = Color.white;
                        shieldImg.raycastTarget = false;
                    }
                }
                else
                {
                    iconImage.sprite = CachedIcon("Enemy", CreateEnemyIcon);
                    iconImage.color = Color.white;
                }
            }
            else if (mType == MissionType.AchieveCombo)
            {
                iconImage.sprite = CachedIcon("Combo", CreateComboIcon);
            }
            else if (mType == MissionType.RemoveDirtMound)
            {
                // 흙더미 제거 미션 — HexBlock의 흙더미 스프라이트(2/3) 동일 시각 사용
                iconImage.sprite = JewelsHexaPuzzle.Core.HexBlock.GetDirtMoundSprite(level: 2);
                iconImage.color = Color.white;
                iconImage.preserveAspect = true;
            }
            else if (mType == MissionType.MoveItem)
            {
                iconImage.sprite = CachedIcon("MoveItem", CreateMoveItemIcon);
            }
            else if (mType == MissionType.SingleTurnRemoval)
            {
                iconImage.sprite = CachedIcon("SingleTurn", CreateSingleTurnRemovalIcon);
                iconImage.color = Color.white;
            }
            else if (mType == MissionType.AchieveCascade)
            {
                iconImage.sprite = CachedIcon("Cascade", CreateCascadeIcon);
                iconImage.color = Color.white;
            }
            else if (mType == MissionType.UseSpecial)
            {
                iconImage.sprite = CachedIcon("UseSpecial", CreateUseSpecialIcon);
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = CachedIcon("Mission", CreateProceduralMissionIcon);
            }
        }

        /// <summary>
        /// 보석 가공 아이콘 (육각형 + 상향 화살표)
        /// </summary>
        private Sprite CreateProcessGemIcon(Color gemColor)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size * 0.38f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float distance = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x);
                    if (angle < 0) angle += Mathf.PI * 2f;

                    float hexAngle = Mathf.PI / 3f;
                    float sectorAngle = angle % hexAngle;
                    float cosAngle = Mathf.Cos(sectorAngle - hexAngle / 2f);
                    float hexDist = outerRadius * cosAngle;

                    // 상향 화살표 (삼각형) - 우상단에 작게
                    float arrowCx = size * 0.72f;
                    float arrowCy = size * 0.72f;
                    float ax = x - arrowCx;
                    float ay = y - arrowCy;
                    bool isArrow = (ay > 0 && ay < size * 0.22f &&
                                    Mathf.Abs(ax) < ay * 0.7f);

                    if (isArrow)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else if (distance < hexDist)
                    {
                        float gradient = 1f - (distance / hexDist) * 0.2f;
                        Color c = gemColor * gradient;
                        c.a = 1f;

                        if (distance > hexDist * 0.85f)
                            c = Color.Lerp(c, Color.white, 0.6f);

                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 특수 블록 생성 아이콘 (6각 별)
        /// </summary>
        private Sprite CreateSpecialGemIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerR = size * 0.42f;
            float innerR = outerR * 0.5f;
            Color starColor = new Color(1f, 0.85f, 0.2f); // 금색

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float dist = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x);
                    if (angle < 0) angle += Mathf.PI * 2f;

                    // 6각 별 형태
                    float starAngle = Mathf.PI / 3f;
                    float sector = angle % starAngle;
                    float t = Mathf.Abs(sector - starAngle / 2f) / (starAngle / 2f);
                    float starDist = Mathf.Lerp(outerR, innerR, t);

                    if (dist < starDist)
                    {
                        float brightness = 1f - (dist / starDist) * 0.3f;
                        Color c = starColor * brightness;
                        c.a = 1f;
                        if (dist < starDist * 0.25f)
                            c = Color.Lerp(c, Color.white, 0.5f);
                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 완전 보석 아이콘 (다이아몬드 형태)
        /// </summary>
        private Sprite CreatePerfectGemIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size * 0.4f;
            Color diamondColor = new Color(0.6f, 0.9f, 1f); // 밝은 하늘색

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 다이아몬드(마름모) 판정
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    float diamondDist = dx / radius + dy / (radius * 1.3f);

                    if (diamondDist < 1f)
                    {
                        float brightness = 1f - diamondDist * 0.4f;
                        Color c = diamondColor * brightness;
                        c.a = 1f;

                        // 중앙 광채
                        if (diamondDist < 0.3f)
                            c = Color.Lerp(c, Color.white, 0.6f * (1f - diamondDist / 0.3f));

                        // 테두리
                        if (diamondDist > 0.88f)
                            c = Color.white;

                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 폭발 아이콘 (빅뱅)
        /// </summary>
        private Sprite CreateExplosionIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size * 0.42f;
            Color explosionColor = new Color(1f, 0.5f, 0.1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float dist = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x);
                    if (angle < 0) angle += Mathf.PI * 2f;

                    // 8각 폭발 패턴
                    float spikes = 8f;
                    float spikeRadius = radius * (0.7f + 0.3f * Mathf.Abs(Mathf.Sin(angle * spikes / 2f)));

                    if (dist < spikeRadius)
                    {
                        float t = dist / spikeRadius;
                        Color c = Color.Lerp(Color.white, explosionColor, t);
                        c.a = 1f;
                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 비닐 제거 아이콘
        /// </summary>
        private Sprite CreateVinylIcon(bool isDouble)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size * 0.4f;
            Color vinylColor = isDouble
                ? new Color(0.8f, 0.6f, 0.2f, 0.85f)
                : new Color(0.7f, 0.7f, 0.7f, 0.7f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float dist = pos.magnitude;

                    if (dist < outerRadius)
                    {
                        Color c = vinylColor;
                        // 반투명 줄무늬 (비닐 느낌)
                        float stripe = Mathf.Sin((x + y) * 0.3f) * 0.5f + 0.5f;
                        c.a = vinylColor.a * (0.7f + stripe * 0.3f);
                        // 광택 효과
                        if (dist < outerRadius * 0.3f)
                            c = Color.Lerp(c, Color.white, 0.3f * (1f - dist / (outerRadius * 0.3f)));
                        // X 표시 (제거 의미)
                        float xDist = Mathf.Min(
                            Mathf.Abs(pos.x - pos.y) / 1.414f,
                            Mathf.Abs(pos.x + pos.y) / 1.414f);
                        if (xDist < size * 0.03f && dist > outerRadius * 0.15f)
                            c = new Color(0.9f, 0.2f, 0.2f);
                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 점수 달성 아이콘 (별 모양)
        /// </summary>
        private Sprite CreateScoreTargetIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerR = size * 0.42f;
            float innerR = outerR * 0.4f;
            Color starColor = new Color(1f, 0.85f, 0f); // 금색 별

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float dist = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x) + Mathf.PI / 2f;
                    if (angle < 0) angle += Mathf.PI * 2f;

                    // 5각 별
                    float starAngle = Mathf.PI * 2f / 5f;
                    float sector = angle % starAngle;
                    float t = Mathf.Abs(sector - starAngle / 2f) / (starAngle / 2f);
                    float starDist = Mathf.Lerp(outerR, innerR, t);

                    if (dist < starDist)
                    {
                        float brightness = 1f - (dist / starDist) * 0.25f;
                        Color c = starColor * brightness;
                        c.a = 1f;
                        if (dist < starDist * 0.2f)
                            c = Color.Lerp(c, Color.white, 0.5f);
                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 적군 제거 아이콘 (회색 육각형 + X표시)
        /// </summary>
        private Sprite CreateEnemyIcon()
        {
            // 고블린 스프라이트를 미션 아이콘으로 사용
            return GoblinSystem.GetGoblinSprite();
        }

        /// <summary>
        /// 콤보 달성 아이콘 (번개 모양)
        /// </summary>
        private Sprite CreateComboIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Color boltColor = new Color(1f, 0.9f, 0.2f); // 금색 번개

            // 번개 폴리곤 정의 (정규화 좌표 0~1)
            Vector2[] boltShape = new Vector2[]
            {
                new Vector2(0.55f, 1.0f),
                new Vector2(0.35f, 0.58f),
                new Vector2(0.52f, 0.58f),
                new Vector2(0.42f, 0.0f),
                new Vector2(0.7f, 0.48f),
                new Vector2(0.52f, 0.48f),
                new Vector2(0.65f, 1.0f)
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;
                    float ny = (float)y / size;

                    // 폴리곤 내부 판정 (ray casting)
                    bool inside = false;
                    for (int i = 0, j = boltShape.Length - 1; i < boltShape.Length; j = i++)
                    {
                        if ((boltShape[i].y > ny) != (boltShape[j].y > ny) &&
                            nx < (boltShape[j].x - boltShape[i].x) * (ny - boltShape[i].y) / (boltShape[j].y - boltShape[i].y) + boltShape[i].x)
                        {
                            inside = !inside;
                        }
                    }

                    if (inside)
                    {
                        float centerDist = Mathf.Abs(nx - 0.5f) * 2f;
                        Color c = Color.Lerp(Color.white, boltColor, centerDist);
                        c.a = 1f;
                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 물건 옮기기 아이콘 (하향 화살표)
        /// </summary>
        private Sprite CreateMoveItemIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Color arrowColor = new Color(0.3f, 0.8f, 1f); // 밝은 하늘색

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;
                    float ny = (float)y / size;

                    bool isArrow = false;

                    // 화살표 몸통 (세로 직사각형)
                    if (nx > 0.38f && nx < 0.62f && ny > 0.25f && ny < 0.7f)
                        isArrow = true;

                    // 화살표 머리 (아래 삼각형)
                    float arrowHeadY = 0.7f;
                    if (ny >= arrowHeadY && ny < 0.95f)
                    {
                        float progress = (ny - arrowHeadY) / (0.95f - arrowHeadY);
                        float halfWidth = 0.3f * (1f - progress);
                        if (Mathf.Abs(nx - 0.5f) < halfWidth)
                            isArrow = true;
                    }

                    if (isArrow)
                    {
                        pixels[y * size + x] = arrowColor;
                        pixels[y * size + x].a = 1f;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 단색 육각형 아이콘 프로시저럴 생성
        /// </summary>
        private Sprite CreateSingleColorHexIcon(Color gemColor)
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size * 0.44f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float distance = pos.magnitude;

                    // 육각형 판정 (flat-top)
                    float angle = Mathf.Atan2(pos.y, pos.x);
                    if (angle < 0) angle += Mathf.PI * 2f;

                    // 육각형 내접 거리 계산
                    float hexAngle = Mathf.PI / 3f; // 60도
                    float sectorAngle = angle % hexAngle;
                    float cosAngle = Mathf.Cos(sectorAngle - hexAngle / 2f);
                    float hexDist = outerRadius * cosAngle;

                    if (distance < hexDist)
                    {
                        // 내부: 메인 색상 + 약간의 그라데이션
                        float gradient = 1f - (distance / hexDist) * 0.2f;
                        Color c = gemColor * gradient;
                        c.a = 1f;

                        // 테두리 (외곽 10%) — 흰색 통일
                        if (distance > hexDist * 0.88f)
                        {
                            c = Color.white;
                        }

                        // 중앙 하이라이트
                        if (distance < hexDist * 0.3f)
                        {
                            float highlight = 1f - (distance / (hexDist * 0.3f));
                            c = Color.Lerp(c, Color.white, highlight * 0.25f);
                        }

                        pixels[y * size + x] = c;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 미션 진행도 업데이트 + 블록 수집 이펙트
        /// </summary>
        public void UpdateMissionWithBlockCollectEffect(MissionData mission, int currentCount, Vector3 blockWorldPos)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 숫자 카운트다운
            Text countText = canvas.GetComponentInChildren<Text>(includeInactive: true);
            if (countText != null && countText.gameObject.name.Contains("Count"))
            {
                int remaining = mission.targetCount - currentCount;
                StartCoroutine(CountDownAnimation(countText, remaining, remaining + 1));
            }

            // 블록 수집 이펙트
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                Input.mousePosition,
                canvas.worldCamera,
                out Vector2 localPos);

            // 미션 아이콘 위치
            GameObject missionUI = GameObject.Find("GameMissionUI_Level1");
            if (missionUI != null)
            {
                RectTransform missionRt = missionUI.GetComponent<RectTransform>();
                StartCoroutine(BlockFlyToMissionEffect(missionRt.anchoredPosition, blockWorldPos, canvas));
            }
        }

        /// <summary>
        /// 카운트다운 애니메이션
        /// </summary>
        private IEnumerator CountDownAnimation(Text countText, int to, int from)
        {
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                int current = Mathf.RoundToInt(Mathf.Lerp(from, to, progress));
                countText.text = current.ToString();
                yield return null;
            }

            countText.text = to.ToString();
        }

        /// <summary>
        /// 블록이 미션 아이콘으로 날아드는 이펙트
        /// </summary>
        private IEnumerator BlockFlyToMissionEffect(Vector2 targetScreenPos, Vector3 startWorldPos, Canvas canvas)
        {
            // 블록 모양 오브젝트 생성
            GameObject blockVisual = new GameObject("BlockFly");
            blockVisual.transform.SetParent(canvas.transform, false);

            RectTransform blockRt = blockVisual.AddComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, startWorldPos),
                canvas.worldCamera,
                out Vector2 startScreenPos);

            blockRt.anchoredPosition = startScreenPos;
            blockRt.sizeDelta = new Vector2(40, 40);

            // 블록 색상 (랜덤)
            Color blockColor = new Color(Random.value, Random.value, Random.value);
            Image blockImage = blockVisual.AddComponent<Image>();
            blockImage.color = blockColor;

            // 육각형 스프라이트 사용
            blockImage.sprite = HexBlock.GetHexFlashSprite();

            float duration = 0.5f;
            float elapsed = 0f;

            // 파티클 이펙트 (별과 반짝임)
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 위치 이동 (이징: easeInQuad)
                Vector2 newPos = Vector2.Lerp(startScreenPos, targetScreenPos, VisualConstants.EaseInQuad(t));
                blockRt.anchoredPosition = newPos;

                // 스케일 감소
                blockRt.localScale = Vector3.one * (1f - t * 0.7f);

                // 회전
                blockRt.rotation = Quaternion.AngleAxis(t * 720f, Vector3.forward);

                // 투명도 감소
                blockImage.color = new Color(blockColor.r, blockColor.g, blockColor.b, 1f - t);

                yield return null;
            }

            Destroy(blockVisual);
        }

        /// <summary>
        /// 미션 아이콘 프로시저럴 생성 (fallback)
        /// </summary>
        private Sprite CreateProceduralMissionIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Color[] colors = new Color[]
            {
                new Color(0.93f, 0.18f, 0.18f),  // Red
                new Color(0.18f, 0.78f, 0.28f),  // Green
                new Color(0.15f, 0.45f, 0.95f),  // Blue
                new Color(1.0f, 0.82f, 0.08f),   // Yellow
                new Color(0.62f, 0.2f, 0.88f),   // Purple
                new Color(1.0f, 0.5f, 0.05f)     // Orange
            };

            Vector2 center = Vector2.one * (size / 2f);
            float radius = size * 0.4f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float angle = Mathf.Atan2(pos.y, pos.x);
                    if (angle < 0) angle += Mathf.PI * 2;

                    float distance = pos.magnitude;

                    if (distance < radius)
                    {
                        int sector = Mathf.FloorToInt((angle / (Mathf.PI * 2)) * 6) % 6;
                        pixels[y * size + x] = colors[sector];

                        if (distance < radius * 0.1f)
                            pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = new Color(0, 0, 0, 0);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 한 턴 제거 미션 아이콘 (번개 모양 — 한 턴에 많이 제거)
        /// </summary>
        private Sprite CreateSingleTurnRemovalIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0, 0, 0, 0);

            Vector2 center = Vector2.one * (size / 2f);
            float radius = size * 0.42f;
            Color bgColor = new Color(0.95f, 0.55f, 0.1f, 1f);    // 오렌지 배경
            Color boltColor = new Color(1f, 1f, 0.85f, 1f);        // 밝은 노란 번개

            // 원형 배경
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < radius)
                        pixels[y * size + x] = bgColor;
                    else if (dist < radius + 3f)
                        pixels[y * size + x] = new Color(1f, 0.75f, 0.3f, 0.5f);
                }
            }

            // 번개 모양 (두꺼운 지그재그 선)
            Vector2[] bolt = new Vector2[]
            {
                new Vector2(0.55f, 0.85f), new Vector2(0.35f, 0.55f),
                new Vector2(0.55f, 0.55f), new Vector2(0.3f, 0.15f),
                new Vector2(0.6f, 0.45f), new Vector2(0.45f, 0.45f),
                new Vector2(0.7f, 0.85f)
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / size;
                    float fy = (float)y / size;
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist >= radius) continue;

                    if (IsPointInBoltShape(fx, fy, bolt))
                        pixels[y * size + x] = boltColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 번개 영역 판정 (두꺼운 지그재그 선)
        /// </summary>
        private bool IsPointInBoltShape(float px, float py, Vector2[] pts)
        {
            float thickness = 0.06f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(new Vector2(px, py) - a, ab) / Vector2.Dot(ab, ab));
                Vector2 proj = a + t * ab;
                float dist = Vector2.Distance(new Vector2(px, py), proj);
                if (dist < thickness) return true;
            }
            return false;
        }

        /// <summary>
        /// 연쇄 미션 아이콘 (3개 하향 화살표 — 캐스케이드)
        /// </summary>
        private Sprite CreateCascadeIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0, 0, 0, 0);

            Vector2 center = Vector2.one * (size / 2f);
            float radius = size * 0.42f;
            Color bgColor = new Color(0.2f, 0.7f, 0.95f, 1f);     // 파란 배경

            // 원형 배경
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < radius)
                        pixels[y * size + x] = bgColor;
                    else if (dist < radius + 3f)
                        pixels[y * size + x] = new Color(0.4f, 0.8f, 1f, 0.5f);
                }
            }

            // 3개 하향 화살표 (연쇄 표현)
            float[] arrowCY = { 0.72f, 0.52f, 0.32f };
            float arrowW = 0.18f;
            float arrowH = 0.12f;
            float stemW = 0.04f;
            float stemH = 0.08f;

            for (int a = 0; a < 3; a++)
            {
                float cy = arrowCY[a];
                float alpha = 1f - a * 0.2f;
                Color ac = new Color(1f, 1f, 1f, alpha);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float fx = (float)x / size;
                        float fy = (float)y / size;
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist >= radius) continue;

                        // 줄기 (위쪽)
                        if (fy >= cy && fy <= cy + stemH &&
                            fx >= 0.5f - stemW && fx <= 0.5f + stemW)
                        {
                            pixels[y * size + x] = ac;
                        }

                        // 삼각형 화살 헤드 (아래 방향)
                        if (fy <= cy && fy >= cy - arrowH)
                        {
                            float t = (cy - fy) / arrowH;
                            float halfW = arrowW * (1f - t);
                            if (fx >= 0.5f - halfW && fx <= 0.5f + halfW)
                                pixels[y * size + x] = ac;
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 특수 블록 사용 미션 아이콘 (6각 별 + 탭 인디케이터)
        /// </summary>
        private Sprite CreateUseSpecialIcon()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0, 0, 0, 0);

            Vector2 center = Vector2.one * (size / 2f);
            float radius = size * 0.42f;
            Color bgColor = new Color(0.7f, 0.25f, 0.85f, 1f);    // 보라 배경
            Color starColor = new Color(1f, 0.95f, 0.6f, 1f);      // 금색 별

            // 원형 배경
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < radius)
                        pixels[y * size + x] = bgColor;
                    else if (dist < radius + 3f)
                        pixels[y * size + x] = new Color(0.8f, 0.4f, 0.95f, 0.5f);
                }
            }

            // 별 모양 (6각 별)
            Vector2 starCenter = new Vector2(0.5f, 0.55f);
            float outerR = 0.25f;
            float innerR = 0.12f;
            int points = 6;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = (float)x / size;
                    float fy = (float)y / size;
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist >= radius) continue;

                    Vector2 p = new Vector2(fx, fy) - starCenter;
                    float angle = Mathf.Atan2(p.y, p.x);
                    if (angle < 0) angle += Mathf.PI * 2;
                    float sector = angle / (Mathf.PI * 2) * (points * 2);
                    int seg = Mathf.FloorToInt(sector);
                    float segT = sector - seg;
                    float r = (seg % 2 == 0)
                        ? Mathf.Lerp(outerR, innerR, segT)
                        : Mathf.Lerp(innerR, outerR, segT);
                    if (p.magnitude < r)
                        pixels[y * size + x] = starColor;
                }
            }

            // 탭 인디케이터 (작은 원)
            Vector2 tapCenter = new Vector2(size * 0.5f, size * 0.25f);
            float tapR = size * 0.06f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), tapCenter);
                    float cd = Vector2.Distance(new Vector2(x, y), center);
                    if (cd >= radius) continue;
                    if (d < tapR)
                        pixels[y * size + x] = new Color(1f, 1f, 1f, 0.9f);
                    else if (d < tapR + 4f)
                        pixels[y * size + x] = new Color(1f, 1f, 1f, 0.4f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
        }

        // ============================================================
        // 토스트 메시지 (풀링 + 인터럽트 방식)
        // ============================================================

        private GameObject toastPoolObj;       // 풀링된 토스트 GameObject
        private RectTransform toastRt;         // 토스트 RectTransform
        private Text toastLabel;               // 토스트 Text
        private Outline toastOutline;          // 토스트 Outline
        private Coroutine currentToastCoroutine; // 현재 실행 중인 토스트 코루틴 (항상 1개만)
        private Vector2 toastStartPos = new Vector2(0f, -370f); // 상단 중앙 기준 시작 위치 (기존 -270 → -370, 100px 아래로 이동)

        /// <summary>
        /// 토스트 풀 오브젝트 초기 생성 (Start에서 호출)
        /// </summary>
        private void InitToastPool()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            toastPoolObj = new GameObject("ToastMessage");
            toastPoolObj.transform.SetParent(canvas.transform, false);

            toastRt = toastPoolObj.AddComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0.5f, 1f);   // 상단 중앙
            toastRt.anchorMax = new Vector2(0.5f, 1f);
            toastRt.pivot     = new Vector2(0.5f, 1f);
            toastRt.anchoredPosition = toastStartPos;
            toastRt.sizeDelta = new Vector2(500f, 60f);

            toastLabel = toastPoolObj.AddComponent<Text>();
            toastLabel.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            toastLabel.fontSize  = 34;  // 기존 26 → 34 (약 30% 증가)
            toastLabel.fontStyle = FontStyle.Bold;
            toastLabel.alignment = TextAnchor.MiddleCenter;
            toastLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            toastLabel.verticalOverflow   = VerticalWrapMode.Overflow;
            toastLabel.color     = Color.white;
            toastLabel.raycastTarget = false;
            toastLabel.text = "";

            toastOutline = toastPoolObj.AddComponent<Outline>();
            toastOutline.effectColor    = new Color(0f, 0f, 0f, 1f);
            toastOutline.effectDistance = new Vector2(2f, -2f);

            toastPoolObj.SetActive(false);
        }

        /// <summary>
        /// 화면 상단 중앙에 표시되는 토스트 메시지.
        /// 배경 없음, 흰색 텍스트 + 검정 Outline.
        /// 0.5초 대기 → 1초 위로 100px 이동 + 페이드아웃.
        /// 항상 1개의 메인 코루틴만 실행. 이전 토스트는 즉시 위로 밀려 사라지는 전용 클론으로 분리.
        /// </summary>
        public void ShowToast(string message)
        {
            if (toastPoolObj == null) InitToastPool();
            if (toastPoolObj == null) return;

            // 이전 토스트가 아직 화면에 있으면: 진행 중 코루틴 중단 후
            // 현재 상태를 클론으로 분리해 "즉시 위로 올라가며 사라지는" 전용 애니메이션으로 전환
            if (currentToastCoroutine != null)
            {
                StopCoroutine(currentToastCoroutine);
                currentToastCoroutine = null;
            }

            if (toastPoolObj.activeSelf && !string.IsNullOrEmpty(toastLabel.text))
                SpawnDyingToastClone();

            // 새 메시지 표시 (메인 풀 초기화)
            toastLabel.color = new Color(1f, 1f, 1f, 0f);
            toastOutline.effectColor = new Color(0f, 0f, 0f, 0f);
            toastRt.anchoredPosition = toastStartPos;

            toastLabel.text  = message;
            toastLabel.color = Color.white;
            toastOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            toastRt.anchoredPosition = toastStartPos;
            toastPoolObj.SetActive(true);

            currentToastCoroutine = StartCoroutine(ToastCoroutine());
        }

        /// <summary>
        /// 현재 표시 중인 토스트의 시각 상태를 복제한 임시 오브젝트를 생성해
        /// 대기 없이 빠르게 위로 올라가며 사라지는 애니메이션만 재생한 뒤 파괴한다.
        /// </summary>
        private void SpawnDyingToastClone()
        {
            Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (canvas == null) return;

            GameObject clone = new GameObject("ToastMessage_Dying");
            clone.transform.SetParent(canvas.transform, false);

            RectTransform cloneRt = clone.AddComponent<RectTransform>();
            cloneRt.anchorMin = toastRt.anchorMin;
            cloneRt.anchorMax = toastRt.anchorMax;
            cloneRt.pivot     = toastRt.pivot;
            cloneRt.sizeDelta = toastRt.sizeDelta;
            cloneRt.anchoredPosition = toastRt.anchoredPosition;

            Text cloneLabel = clone.AddComponent<Text>();
            cloneLabel.font      = toastLabel.font;
            cloneLabel.fontSize  = toastLabel.fontSize;
            cloneLabel.fontStyle = toastLabel.fontStyle;
            cloneLabel.alignment = toastLabel.alignment;
            cloneLabel.horizontalOverflow = toastLabel.horizontalOverflow;
            cloneLabel.verticalOverflow   = toastLabel.verticalOverflow;
            cloneLabel.color     = toastLabel.color.a > 0f ? toastLabel.color : Color.white;
            cloneLabel.raycastTarget = false;
            cloneLabel.text = toastLabel.text;

            Outline cloneOutline = clone.AddComponent<Outline>();
            cloneOutline.effectColor    = new Color(0f, 0f, 0f, cloneLabel.color.a);
            cloneOutline.effectDistance = toastOutline.effectDistance;

            StartCoroutine(DyingToastCoroutine(clone, cloneRt, cloneLabel, cloneOutline));
        }

        /// <summary>
        /// 클론 토스트: 대기 없이 0.5초 동안 위로 70px + 페이드아웃 후 파괴
        /// (속도 절반, 이동 거리 30% 감소)
        /// </summary>
        private IEnumerator DyingToastCoroutine(GameObject go, RectTransform rt, Text label, Outline outline)
        {
            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos   = startPos + Vector2.up * 70f;
            float fadeDuration = 0.5f;
            float startAlpha = label.color.a;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (go == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                float alpha = Mathf.Lerp(startAlpha, 0f, t);
                label.color = new Color(1f, 1f, 1f, alpha);
                outline.effectColor = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }

            if (go != null) Destroy(go);
        }

        /// <summary>
        /// 토스트 메인 코루틴: 0.5초 대기 → 1초 위로 100px + 페이드아웃
        /// </summary>
        private IEnumerator ToastCoroutine()
        {
            // 0.5초 대기
            float waitElapsed = 0f;
            while (waitElapsed < 0.5f)
            {
                if (toastPoolObj == null) yield break;
                waitElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 1초 동안 위로 100px 이동 + 알파 0
            Vector2 startPos = toastRt.anchoredPosition;
            Vector2 endPos   = startPos + Vector2.up * 100f;
            float fadeDuration = 1f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (toastPoolObj == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);

                toastRt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                float alpha = Mathf.Lerp(1f, 0f, t);
                toastLabel.color = new Color(1f, 1f, 1f, alpha);
                toastOutline.effectColor = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }

            toastPoolObj.SetActive(false);
            currentToastCoroutine = null;
        }
    }

    /// <summary>
    /// 미션 아이콘 공용 헬퍼 (UIManager, MissionUI 공통 사용)
    /// </summary>
    internal static class MissionUIHelper
    {
        private static Sprite _cachedMultiColorHex;
        private static Sprite _cachedCheckSprite;

        /// <summary>
        /// 초록색 체크마크 스프라이트 생성 (캐싱)
        /// 미션 완료 시 숫자 0 대신 표시
        /// </summary>
        public static Sprite CreateCheckMarkSprite()
        {
            if (_cachedCheckSprite != null) return _cachedCheckSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            // 배경 투명
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // 초록색 원형 배경
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float circleRadius = size * 0.44f;
            Color bgColor = new Color(0.15f, 0.75f, 0.3f, 1f); // 선명한 초록
            Color checkColor = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float dist = Vector2.Distance(p, center);
                    if (dist <= circleRadius)
                    {
                        // 원형 배경 (약간 밝은 그라데이션)
                        float t = dist / circleRadius;
                        pixels[y * size + x] = Color.Lerp(bgColor, bgColor * 0.8f, t * 0.3f);
                    }
                }
            }

            // 체크마크 그리기 (✓ 모양, 두꺼운 선)
            // 체크의 세 꼭짓점: 좌측 중간, 하단 중앙 약간 좌, 우상단
            Vector2 p1 = new Vector2(size * 0.24f, size * 0.50f); // 시작점 (좌측)
            Vector2 p2 = new Vector2(size * 0.42f, size * 0.30f); // 꺾이는 점 (하단)
            Vector2 p3 = new Vector2(size * 0.76f, size * 0.72f); // 끝점 (우상단)
            float lineWidth = size * 0.09f; // 두꺼운 선

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    // p1 → p2 선분과의 거리
                    float d1 = DistToSegment(p, p1, p2);
                    // p2 → p3 선분과의 거리
                    float d2 = DistToSegment(p, p2, p3);
                    float minD = Mathf.Min(d1, d2);

                    if (minD < lineWidth)
                    {
                        // 안티앨리어싱 (부드러운 가장자리)
                        float alpha = Mathf.Clamp01(1f - (minD - lineWidth + 1.5f) / 1.5f);
                        Color existing = pixels[y * size + x];
                        pixels[y * size + x] = Color.Lerp(existing, checkColor, alpha);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedCheckSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
            return _cachedCheckSprite;
        }

        /// <summary>
        /// 점과 선분 사이의 거리
        /// </summary>
        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.magnitude;
            if (len < 0.001f) return Vector2.Distance(p, a);
            Vector2 dir = ab / len;
            Vector2 ap = p - a;
            float proj = Mathf.Clamp(Vector2.Dot(ap, dir), 0, len);
            Vector2 closest = a + dir * proj;
            return Vector2.Distance(p, closest);
        }

        /// <summary>
        /// 6색 구역 분할 육각형 스프라이트 생성 (캐싱)
        /// </summary>
        public static Sprite CreateMultiColorHexagonSprite()
        {
            if (_cachedMultiColorHex != null) return _cachedMultiColorHex;

            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Color[] gemColors = new Color[]
            {
                GemColors.GetColor(GemType.Red),
                GemColors.GetColor(GemType.Orange),
                GemColors.GetColor(GemType.Yellow),
                GemColors.GetColor(GemType.Green),
                GemColors.GetColor(GemType.Blue),
                GemColors.GetColor(GemType.Purple)
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size * 0.44f;

            // flat-top 육각형 꼭짓점 (0°, 60°, 120°, ...) 계산
            // flat-top: 꼭짓점이 좌우(0°,180°)에 위치
            Vector2[] verts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f; // 0°, 60°, 120°, 180°, 240°, 300°
                verts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);

                    // 육각형 내부 판정 (ray-casting)
                    if (!IsPointInHex(p, verts))
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    Vector2 pos = p - center;
                    float angle = Mathf.Atan2(pos.y, pos.x);
                    if (angle < 0) angle += Mathf.PI * 2;

                    // 6개 섹터 (각 섹터 60도)
                    int sector = Mathf.FloorToInt((angle / (Mathf.PI * 2)) * 6) % 6;
                    Color col = gemColors[sector];

                    // 테두리: 꼭짓점 방사선 경계에 얇은 어두운 선
                    float sectorAngle = angle / (Mathf.PI * 2) * 6f;
                    float edgeDist = Mathf.Abs(sectorAngle - Mathf.Round(sectorAngle));
                    if (edgeDist < 0.04f)
                    {
                        col = Color.Lerp(col, new Color(0.15f, 0.15f, 0.15f, 1f), 0.6f);
                    }

                    // 외곽 테두리 (육각형 바깥 가까이)
                    float hexDist = HexEdgeDistance(p, center, verts);
                    if (hexDist < 3f)
                    {
                        col = Color.Lerp(col, new Color(0.1f, 0.1f, 0.1f, 1f), 0.7f);
                    }

                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _cachedMultiColorHex = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f);
            return _cachedMultiColorHex;
        }

        /// <summary>
        /// 점이 육각형 내부에 있는지 (ray-casting)
        /// </summary>
        private static bool IsPointInHex(Vector2 p, Vector2[] verts)
        {
            int n = verts.Length;
            int crossings = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = verts[i], b = verts[(i + 1) % n];
                if ((a.y > p.y) != (b.y > p.y))
                {
                    float xCross = (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x;
                    if (p.x < xCross) crossings++;
                }
            }
            return crossings % 2 == 1;
        }

        /// <summary>
        /// 점에서 육각형 가장 가까운 변까지의 거리
        /// </summary>
        private static float HexEdgeDistance(Vector2 p, Vector2 center, Vector2[] verts)
        {
            float minDist = float.MaxValue;
            int n = verts.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = verts[i], b = verts[(i + 1) % n];
                Vector2 ab = b - a;
                float len = ab.magnitude;
                Vector2 dir = ab / len;
                Vector2 ap = p - a;
                float proj = Mathf.Clamp(Vector2.Dot(ap, dir), 0, len);
                Vector2 closest = a + dir * proj;
                float dist = Vector2.Distance(p, closest);
                if (dist < minDist) minDist = dist;
            }
            return minDist;
        }
    }

    /// <summary>
    /// 미션 UI 컴포넌트
    /// </summary>
    [System.Serializable]
    public class MissionUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text countText;
        [SerializeField] private RectTransform frameRect;

        private MissionData missionData;
        private int displayCount = 0;

        private void Awake()
        {
            // 좌측 상단에 사각 프레임 설정
            if (frameRect == null)
                frameRect = GetComponent<RectTransform>();

            if (frameRect != null)
            {
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.zero;
                frameRect.pivot = Vector2.zero;
                frameRect.anchoredPosition = new Vector2(20, -20);
            }
        }

        public void SetMission(MissionData data)
        {
            missionData = data;
            displayCount = data.targetCount - data.currentCount;

            // 미션 타입에 따른 아이콘 생성
            if (iconImage != null)
            {
                if (data.type == MissionType.CollectGem && data.targetGemType == GemType.None)
                {
                    // 아무 색 보석 모으기: 6색 구역 분할 육각형
                    iconImage.sprite = CreateMultiColorHexagon();
                }
                else if (data.icon != null)
                {
                    iconImage.sprite = data.icon;
                    iconImage.color = GemColors.GetColor(data.targetGemType);
                }
            }

            UpdateProgress(data.currentCount, data.targetCount);
        }

        public void UpdateProgress(int current, int target)
        {
            int remaining = target - current;
            // 0 이하가 되지 않도록 제한
            remaining = Mathf.Max(0, remaining);

            if (countText != null)
            {
                // 숫자 감소 애니메이션
                StopAllCoroutines();
                StartCoroutine(AnimateCountDown(displayCount, remaining));
            }
        }

        /// <summary>
        /// 숫자 감소 애니메이션 (부드러운 효과)
        /// </summary>
        private IEnumerator AnimateCountDown(int from, int to)
        {
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                int current = Mathf.RoundToInt(Mathf.Lerp(from, to, progress));
                // 0 이하가 되지 않도록 제한
                current = Mathf.Max(0, current);

                if (countText != null)
                {
                    countText.text = $"x {current}";
                }

                yield return null;
            }

            displayCount = Mathf.Max(0, to);
            if (countText != null)
            {
                // 미션 완료 시 체크마크 표시
                if (displayCount <= 0)
                {
                    countText.text = "";
                    ShowCheckMark();
                }
                else
                {
                    countText.text = $"x {displayCount}";
                }
            }
        }

        /// <summary>
        /// 카운트 텍스트 위치에 초록색 체크마크 아이콘 표시
        /// </summary>
        private void ShowCheckMark()
        {
            if (countText == null) return;

            Transform existing = countText.transform.Find("CheckMark");
            if (existing != null) return;

            GameObject checkObj = new GameObject("CheckMark");
            checkObj.transform.SetParent(countText.transform, false);
            RectTransform checkRt = checkObj.AddComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0, 0.5f);
            checkRt.anchorMax = new Vector2(0, 0.5f);
            checkRt.pivot = new Vector2(0, 0.5f);

            float checkSize = countText.fontSize * 1.2f;
            checkRt.sizeDelta = new Vector2(checkSize, checkSize);
            checkRt.anchoredPosition = new Vector2(4, 0);

            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.sprite = MissionUIHelper.CreateCheckMarkSprite();
            checkImg.color = Color.white;
            checkImg.raycastTarget = false;

            // 등장 애니메이션
            StartCoroutine(CheckMarkAppearCoroutine(checkRt));
        }

        private IEnumerator CheckMarkAppearCoroutine(RectTransform checkRt)
        {
            if (checkRt == null) yield break;
            float duration = 0.25f;
            float elapsed = 0f;
            checkRt.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float scale = t < 0.6f
                    ? Mathf.Lerp(0f, 1.3f, t / 0.6f)
                    : Mathf.Lerp(1.3f, 1f, (t - 0.6f) / 0.4f);
                if (checkRt != null)
                    checkRt.localScale = Vector3.one * scale;
                yield return null;
            }
            if (checkRt != null)
                checkRt.localScale = Vector3.one;
        }

        /// <summary>
        /// 6색 구역 분할 육각형 생성 (프로시저럴)
        /// </summary>
        private Sprite CreateMultiColorHexagon()
        {
            return MissionUIHelper.CreateMultiColorHexagonSprite();
        }
    }

    /// <summary>
    /// 아이템 버튼 UI 컴포넌트
    /// </summary>
    [System.Serializable]
    public class ItemButtonUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text countText;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Text lockStageText;
        [SerializeField] private Button button;

        private ItemData itemData;

        /// <summary>이 버튼에 연결된 아이템 타입</summary>
        public ItemType CurrentItemType => itemData != null ? itemData.type : (ItemType)0;
        /// <summary>내부 Button 컴포넌트 참조</summary>
        public Button ButtonComponent => button;

        // 게이지 바 UI (동적 생성)
        private Image gaugeBarBg;
        private Image gaugeBarFill;
        private Text gaugeCountText;
        private bool gaugeBarCreated = false;
        private bool wasGaugeFull = false;

        // ★ 헥사 게이지 비주얼 (Charge 패키지) — 가로 바 대체
        private Image hexInnerGray;   // 회색 헥사 바탕
        private Image hexColorFill;   // 헥사 fill (Filled Vertical Bottom)
        private Image hexFrame;       // 골드 헥사 프레임
        private Image hexGlyphImg;    // 아이템 글리프
        private GameObject hexGlow;   // READY 글로우

        /// <summary>ItemType → Charge 글리프 스프라이트 로드 (없으면 null)</summary>
        private static Sprite LoadChargeGlyph(ItemType type)
        {
            string n = null;
            switch (type)
            {
                case ItemType.Hammer:          n = "glyph_hammer"; break;
                case ItemType.Bomb:            n = "glyph_swap"; break;   // Bomb = 스왑
                case ItemType.SSD:             n = "glyph_line"; break;   // SSD = 라인
                case ItemType.ReverseRotation: n = "glyph_reverse"; break;
            }
            return n != null ? Resources.Load<Sprite>($"Items/Charge/{n}") : null;
        }

        /// <summary>버튼 전체 크기에 헥사 레이어 Image 생성 (anchor stretch)</summary>
        private Image CreateHexLayer(string objName, Sprite sp, Color color)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.sprite = sp;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public void SetItem(ItemData data)
        {
            itemData = data;
            Debug.Log($"[아이템진단3] SetItem 호출됨 item={data.type} button={button} iconImage={iconImage}");

            if (iconImage != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
            }

            bool isUnlocked = data.unlockStage <= GameManager.Instance.CurrentStage;

            if (lockOverlay != null)
            {
                lockOverlay.SetActive(!isUnlocked);
            }

            if (lockStageText != null)
            {
                lockStageText.text = $"stage {data.unlockStage}";
            }

            // 역회전: 게이지 없이 항상 활성화
            bool isReverseRotation = (data.type == ItemType.ReverseRotation);

            if (isReverseRotation)
            {
                if (gaugeBarBg != null) gaugeBarBg.gameObject.SetActive(false);
                if (gaugeBarFill != null) gaugeBarFill.gameObject.SetActive(false);
                if (gaugeCountText != null) gaugeCountText.gameObject.SetActive(false);

                if (isUnlocked && countText != null)
                    countText.text = "∞";

                if (button != null)
                {
                    button.interactable = isUnlocked;
                    if (iconImage != null)
                    {
                        Color c = iconImage.color;
                        c.a = isUnlocked ? 1f : 0.4f;
                        iconImage.color = c;
                    }
                }
            }
            else
            {
                // 게이지 바 생성 (한 번만)
                if (!gaugeBarCreated)
                    CreateGaugeBar(data.type);

                // 카운트 기반 갱신
                int gaugeCount = ItemManager.Instance != null ? ItemManager.Instance.GetGaugeCount(data.type) : 0;
                float gauge = gaugeCount / 10f;
                UpdateGaugeBar(gauge);

                // 게이지 카운트 텍스트
                if (gaugeCountText != null)
                    gaugeCountText.text = $"{gaugeCount}/10";

                if (isUnlocked && countText != null)
                    countText.text = $"{gaugeCount}/10";

                bool canUse = ItemManager.Instance != null ? ItemManager.Instance.CanUseItem(data.type) : false;

                // HammerGauge가 Hammer 버튼의 interactable을 관리하므로 여기서 덮어쓰지 않음
                bool hammerGaugeManaged = (data.type == ItemType.Hammer && JewelsHexaPuzzle.Items.HammerGauge.Instance != null);

                if (button != null && !hammerGaugeManaged)
                {
                    button.interactable = isUnlocked && canUse;

                    if (iconImage != null)
                    {
                        Color c = iconImage.color;
                        c.a = (isUnlocked && canUse) ? 1f : 0.4f;
                        iconImage.color = c;
                    }
                }

                // 활성화 연출: 카운트가 10에 도달한 순간
                if (canUse && !wasGaugeFull)
                {
                    wasGaugeFull = true;
                    StartCoroutine(GaugeFullActivationEffect());
                }
                else if (!canUse)
                {
                    wasGaugeFull = false;
                }
            }
        }

        /// <summary>헥사 게이지 동적 생성 — 골드 프레임 + 헥사 fill + 글리프 + READY 글로우</summary>
        private void CreateGaugeBar(ItemType type)
        {
            gaugeBarCreated = true;
            RectTransform btnRt = GetComponent<RectTransform>();
            if (btnRt == null) return;

            // 아이템 색상 (기존 연결 젬 색 유지)
            GemType linkedGem = ItemManager.GetLinkedGemType(type);
            Color fillColor = (linkedGem != GemType.None) ? GemColors.GetColor(linkedGem) : Color.white;

            // 레이어 순서: InnerGray(뒤) → ColorFill → Glyph → Frame → Glow(앞)
            // 1. 회색 헥사 바탕
            hexInnerGray = CreateHexLayer("HexInnerGray", Resources.Load<Sprite>("Items/Charge/charge_inner_gray"), Color.white);

            // 2. 헥사 fill (Filled Vertical Bottom — 아래에서 위로 차오름)
            hexColorFill = CreateHexLayer("HexColorFill", Resources.Load<Sprite>("Items/Charge/charge_inner_fill"), fillColor);
            hexColorFill.type = Image.Type.Filled;
            hexColorFill.fillMethod = Image.FillMethod.Vertical;
            hexColorFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            hexColorFill.fillAmount = 0f;
            gaugeBarFill = hexColorFill; // 호환용 참조

            // 3. 글리프 (ItemType 매핑, 없으면 기존 iconImage 유지)
            Sprite glyph = LoadChargeGlyph(type);
            if (glyph != null)
            {
                hexGlyphImg = CreateHexLayer("HexGlyph", glyph, Color.white);
                // 글리프는 헥사 내부에 약간 작게
                RectTransform grt = hexGlyphImg.rectTransform;
                grt.anchorMin = new Vector2(0.18f, 0.18f);
                grt.anchorMax = new Vector2(0.82f, 0.82f);
                grt.offsetMin = Vector2.zero;
                grt.offsetMax = Vector2.zero;
                // 기존 사각 아이콘 숨김 (헥사 글리프로 대체)
                if (iconImage != null) iconImage.enabled = false;
            }

            // 4. 골드 헥사 프레임 (외곽선)
            hexFrame = CreateHexLayer("HexFrame", Resources.Load<Sprite>("Items/Charge/charge_frame"), Color.white);

            // 5. READY 글로우 (100% 시 활성)
            hexGlow = CreateHexLayer("HexGlow", Resources.Load<Sprite>("Items/Charge/charge_glow"), new Color(1f, 0.92f, 0.55f, 1f)).gameObject;
            hexGlow.SetActive(false);

            // 카운트 텍스트 (헥사 하단)
            GameObject textObj = new GameObject("GaugeCountText");
            textObj.transform.SetParent(transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0.25f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            gaugeCountText = textObj.AddComponent<Text>();
            gaugeCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gaugeCountText.fontSize = 14;
            gaugeCountText.fontStyle = FontStyle.Bold;
            gaugeCountText.alignment = TextAnchor.MiddleCenter;
            gaugeCountText.color = Color.white;
            gaugeCountText.raycastTarget = false;
            gaugeCountText.text = "0/10";
            var countOutline = textObj.AddComponent<Outline>();
            countOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            countOutline.effectDistance = new Vector2(1f, -1f);
            textObj.transform.SetAsLastSibling(); // 텍스트 최상위
        }

        /// <summary>게이지 100% 도달 시 활성화 연출 (3회 플래시 + 스케일 펄스)</summary>
        private IEnumerator GaugeFullActivationEffect()
        {
            if (iconImage == null) yield break;

            Color originalColor = iconImage.color;
            RectTransform rt = GetComponent<RectTransform>();
            Vector3 originalScale = rt != null ? rt.localScale : Vector3.one;

            // 3회 플래시 + 펄스
            for (int i = 0; i < 3; i++)
            {
                // 플래시 ON
                iconImage.color = Color.white;
                if (rt != null) rt.localScale = originalScale * 1.2f;
                yield return new WaitForSeconds(0.1f);

                // 플래시 OFF
                iconImage.color = originalColor;
                if (rt != null) rt.localScale = originalScale;
                yield return new WaitForSeconds(0.05f);
            }

            // 최종 복원
            iconImage.color = originalColor;
            if (rt != null) rt.localScale = originalScale;
        }

        /// <summary>헥사 게이지 채움 갱신 (0~1) — Filled Vertical fillAmount</summary>
        private void UpdateGaugeBar(float gauge)
        {
            float g = Mathf.Clamp01(gauge);
            if (hexColorFill != null)
                hexColorFill.fillAmount = g;

            // 100% 도달 시 글로우 활성
            if (hexGlow != null)
            {
                bool full = g >= 1f;
                if (hexGlow.activeSelf != full) hexGlow.SetActive(full);
            }
        }

        public void OnItemClicked()
        {
            if (itemData != null)
            {
                // 게이지 기반 아이템 사용
                if (ItemManager.Instance != null && ItemManager.Instance.CanUseItem(itemData.type))
                {
                    ItemManager.Instance.UseItem(itemData.type);
                }
            }
        }
    }
}

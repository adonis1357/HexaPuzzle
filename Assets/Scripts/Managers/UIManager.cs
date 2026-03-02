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
        public static RectTransform gameMissionContainerRect;

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

            turnText.text = turns.ToString();

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
                hudGoldText.text = gold.ToString();
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
            if (itemButtons == null) return;

            for (int i = 0; i < itemButtons.Length; i++)
            {
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
                int stage = GameManager.Instance != null ? GameManager.Instance.CurrentStage : 1;
                clearTitleText.text = $"STAGE {stage} CLEAR!";
                clearTitleText.text += "\n수고하셨습니다!\n멋진 플레이였어요!";
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
                int stage = GameManager.Instance != null ? GameManager.Instance.CurrentStage : 1;
                clearTitleText.text = $"STAGE {stage} CLEAR!";
                clearTitleText.text += "\n수고하셨습니다!\n멋진 플레이였어요!";
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
            bgImage.color = new Color(0, 0, 0, 0.7f);
            bgImage.raycastTarget = true;

            // 팝업 패널 (중앙)
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(popupRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(650, 650);
            panelRect.anchoredPosition = Vector2.zero;

            // 패널 배경 (어두운 보라/남색)
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.2f, 0.15f, 0.35f, 0.95f);

            // 패널 테두리 (금색)
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.84f, 0.68f, 0.1f, 1f);
            outline.effectDistance = new Vector2(3, 3);

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
            titleText.text = "STAGE CLEAR!\n수고하셨습니다!\n멋진 플레이였어요!";
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.UpperCenter;
            titleText.color = new Color(1f, 0.84f, 0f, 1f); // 노란색
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
            scoreText.color = Color.white;
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
            buttonImage.color = new Color(0.5f, 0.2f, 0.7f, 1f); // 보라색

            Button buttonComponent = buttonObj.AddComponent<Button>();
            buttonComponent.targetGraphic = buttonImage;

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

            float duration = 0.4f;
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

            // 4. 잠시 표시 유지 후 슬라이드 아웃
            yield return new WaitForSeconds(0.8f);

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
        /// 게임 중 왼쪽 상단에 미션 UI 생성
        /// </summary>
        public void CreateGameMissionUI(Canvas canvas, MissionData mission)
        {
            if (mission == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 미션 컨테이너
            GameObject missionObj = new GameObject("GameMissionUI");
            missionObj.transform.SetParent(canvas.transform, false);
            RectTransform missionRt = missionObj.AddComponent<RectTransform>();
            missionRt.anchorMin = new Vector2(0, 1);
            missionRt.anchorMax = new Vector2(0, 1);
            missionRt.pivot = new Vector2(0, 1);
            missionRt.anchoredPosition = new Vector2(20, -20);
            missionRt.sizeDelta = new Vector2(400, 200);

            // 배경 패널 (밝은 라벤더 톤)
            Image bgImage = missionObj.AddComponent<Image>();
            bgImage.color = new Color(0.82f, 0.78f, 0.93f, 0.92f);
            bgImage.raycastTarget = false;

            // 미션 아이콘 (다색 육각형) - 2배 확대
            GameObject iconObj = new GameObject("MissionIcon");
            iconObj.transform.SetParent(missionObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0.5f);
            iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(30, 0);
            iconRt.sizeDelta = new Vector2(140, 140);

            Image iconImage = iconObj.AddComponent<Image>();
            SetMissionIconForType(iconImage, mission);
            iconImage.type = Image.Type.Simple;
            iconImage.raycastTarget = false;
            Outline iconOutline = iconObj.AddComponent<Outline>();
            iconOutline.effectColor = Color.white;
            iconOutline.effectDistance = new Vector2(2, 2);

            // 미션 진행도 숫자 (아이콘 옆)
            GameObject countObj = new GameObject("Count");
            countObj.transform.SetParent(missionObj.transform, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0, 0.5f);
            countRt.anchorMax = new Vector2(0, 0.5f);
            countRt.pivot = new Vector2(0, 0.5f);
            countRt.anchoredPosition = new Vector2(180, 0);
            countRt.sizeDelta = new Vector2(180, 140);

            Text countText = countObj.AddComponent<Text>();
            countText.font = font;
            countText.fontSize = 56;
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

            // 미션 UI 컨테이너 저장 (나중에 애니메이션에서 사용)
            missionObj.name = "GameMissionUI_Level1";

            // static 필드에 참조 저장 (GameManager에서 접근)
            gameMissionCountText = countText;
            gameMissionIconRect = missionRt;
        }

        /// <summary>
        /// 게임 중 왼쪽 상단에 복수 미션 UI 생성 (미션 배열 오버로드)
        /// 각 미션별 색상 아이콘 + 카운트를 세로로 나열
        /// </summary>
        public void CreateGameMissionUI(Canvas canvas, MissionData[] missions)
        {
            if (missions == null || missions.Length == 0) return;

            // 미션이 1개면 기존 단일 미션 UI 사용
            if (missions.Length == 1)
            {
                CreateGameMissionUI(canvas, missions[0]);
                return;
            }

            // 복수 미션 리스트 초기화
            gameMissionCountTexts.Clear();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 미션 컨테이너 (배경 패널)
            float rowHeight = 90f;
            float containerHeight = missions.Length * rowHeight + 20f; // 패딩 포함

            GameObject containerObj = new GameObject("GameMissionUI_Multi");
            containerObj.transform.SetParent(canvas.transform, false);
            RectTransform containerRt = containerObj.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0, 1);
            containerRt.anchorMax = new Vector2(0, 1);
            containerRt.pivot = new Vector2(0, 1);
            containerRt.anchoredPosition = new Vector2(20, -20);
            containerRt.sizeDelta = new Vector2(280, containerHeight);

            // 밝은 라벤더 톤 배경
            Image containerBg = containerObj.AddComponent<Image>();
            containerBg.color = new Color(0.82f, 0.78f, 0.93f, 0.92f);
            containerBg.raycastTarget = false;

            gameMissionContainerRect = containerRt;

            // 각 미션 행 생성
            for (int i = 0; i < missions.Length; i++)
            {
                MissionData mission = missions[i];
                float yOffset = -10f - (i * rowHeight); // 상단부터 아래로

                // === 미션 행 컨테이너 ===
                GameObject rowObj = new GameObject($"MissionRow_{i}");
                rowObj.transform.SetParent(containerObj.transform, false);
                RectTransform rowRt = rowObj.AddComponent<RectTransform>();
                rowRt.anchorMin = new Vector2(0, 1);
                rowRt.anchorMax = new Vector2(1, 1);
                rowRt.pivot = new Vector2(0, 1);
                rowRt.anchoredPosition = new Vector2(0, yOffset);
                rowRt.sizeDelta = new Vector2(0, rowHeight);

                // === 미션 아이콘 (단색 육각형 블록) ===
                GameObject iconObj = new GameObject($"MissionIcon_{i}");
                iconObj.transform.SetParent(rowObj.transform, false);
                RectTransform iconRt = iconObj.AddComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0, 0.5f);
                iconRt.anchorMax = new Vector2(0, 0.5f);
                iconRt.pivot = new Vector2(0, 0.5f);
                iconRt.anchoredPosition = new Vector2(15, 0);
                iconRt.sizeDelta = new Vector2(70, 70);

                Image iconImage = iconObj.AddComponent<Image>();
                iconImage.raycastTarget = false;

                SetMissionIconForType(iconImage, mission);
                Outline iconOutline = iconObj.AddComponent<Outline>();
                iconOutline.effectColor = Color.white;
                iconOutline.effectDistance = new Vector2(2, 2);

                // === 카운트 텍스트 ===
                GameObject countObj = new GameObject($"MissionCount_{i}");
                countObj.transform.SetParent(rowObj.transform, false);
                RectTransform countRt = countObj.AddComponent<RectTransform>();
                countRt.anchorMin = new Vector2(0, 0.5f);
                countRt.anchorMax = new Vector2(0, 0.5f);
                countRt.pivot = new Vector2(0, 0.5f);
                countRt.anchoredPosition = new Vector2(95, 0);
                countRt.sizeDelta = new Vector2(160, 70);

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

                // 리스트에 추가
                gameMissionCountTexts.Add(countText);
            }

            // 하위호환: 첫 번째 미션 참조를 기존 static 필드에도 저장
            if (gameMissionCountTexts.Count > 0)
                gameMissionCountText = gameMissionCountTexts[0];
            gameMissionIconRect = containerRt;
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

            GameObject rewardObj = new GameObject("RewardText");
            rewardObj.transform.SetParent(gameMissionIconRect, false);
            RectTransform rewardRt = rewardObj.AddComponent<RectTransform>();
            rewardRt.anchorMin = new Vector2(0, 0);
            rewardRt.anchorMax = new Vector2(1, 0);
            rewardRt.pivot = new Vector2(0.5f, 0);
            rewardRt.anchoredPosition = new Vector2(0, 8f);
            rewardRt.sizeDelta = new Vector2(0, 28f);

            gameMissionRewardText = rewardObj.AddComponent<Text>();
            gameMissionRewardText.font = font;
            gameMissionRewardText.fontSize = 22;
            gameMissionRewardText.fontStyle = FontStyle.Bold;
            gameMissionRewardText.alignment = TextAnchor.MiddleCenter;
            gameMissionRewardText.color = new Color(0.2f, 1f, 0.4f, 1f); // 밝은 녹색
            gameMissionRewardText.raycastTarget = false;
            gameMissionRewardText.text = $"+{reward}";

            Outline outline = rewardObj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, 1);
        }

        /// <summary>
        /// 게임 미션 UI 정리 (로비 전환 시 호출)
        /// </summary>
        public void CleanupGameMissionUI()
        {
            // 단일 미션 UI 제거
            GameObject singleMissionUI = GameObject.Find("GameMissionUI_Level1");
            if (singleMissionUI != null)
                Destroy(singleMissionUI);

            // 복수 미션 UI 제거
            GameObject multiMissionUI = GameObject.Find("GameMissionUI_Multi");
            if (multiMissionUI != null)
                Destroy(multiMissionUI);

            // static 필드 초기화
            gameMissionCountText = null;
            gameMissionIconRect = null;
            gameMissionRewardText = null;
            gameMissionCountTexts.Clear();
            gameMissionContainerRect = null;
        }

        /// <summary>
        /// 미션 UI 등장 애니메이션 (왼쪽에서 슬라이드인 + 스케일 펀치)
        /// </summary>
        // 미션 등장 애니메이션은 GameManager.AnimateMissionEntranceCoroutine에서 처리

        /// <summary>
        /// 미션 타입에 따라 적절한 아이콘을 Image에 적용
        /// </summary>
        private void SetMissionIconForType(Image iconImage, MissionData mission)
        {
            MissionType mType = mission.type;
            GemType gemType = mission.targetGemType;

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
                        iconImage.sprite = CreateSingleColorHexIcon(GemColors.GetColor(gemType));
                    }
                }
                else
                {
                    Sprite missionIcon = Resources.Load<Sprite>("Icons/MissionIcon");
                    if (missionIcon != null)
                        iconImage.sprite = missionIcon;
                    else
                        iconImage.sprite = CreateProceduralMissionIcon();
                }
            }
            else if (mType == MissionType.ProcessGem)
            {
                if (gemType != GemType.None)
                    iconImage.sprite = CreateProcessGemIcon(GemColors.GetColor(gemType));
                else
                    iconImage.sprite = CreateProcessGemIcon(new Color(0.9f, 0.85f, 0.3f));
            }
            else if (mType == MissionType.CreateSpecialGem)
            {
                iconImage.sprite = CreateSpecialGemIcon();
                iconImage.color = Color.white;
            }
            // === 특수 블록별 생성 미션 아이콘 (6색 육각형 배경 + 특수 블록 오버레이) ===
            else if (mType == MissionType.CreateDrillVertical ||
                     mType == MissionType.CreateDrillSlash ||
                     mType == MissionType.CreateDrillBackSlash ||
                     mType == MissionType.CreateBomb ||
                     mType == MissionType.CreateRainbow)
            {
                // 배경: 6색 육각형 기본 블록
                iconImage.sprite = MissionUIHelper.CreateMultiColorHexagonSprite();
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
                    case MissionType.CreateBomb:
                        overlaySprite = BombBlockSystem.GetBombIconSprite(); break;
                    case MissionType.CreateRainbow:
                        overlaySprite = DonutBlockSystem.GetDonutIconSprite(); break;
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
                iconImage.sprite = CreatePerfectGemIcon();
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
                    iconImage.sprite = CreateExplosionIcon();
                }
            }
            else if (mType == MissionType.RemoveVinyl || mType == MissionType.RemoveDoubleVinyl)
            {
                iconImage.sprite = CreateVinylIcon(mType == MissionType.RemoveDoubleVinyl);
            }
            else if (mType == MissionType.ReachScore)
            {
                iconImage.sprite = CreateScoreTargetIcon();
                iconImage.color = Color.white;
            }
            else if (mType == MissionType.RemoveEnemy)
            {
                iconImage.sprite = CreateEnemyIcon();
            }
            else if (mType == MissionType.AchieveCombo)
            {
                iconImage.sprite = CreateComboIcon();
            }
            else if (mType == MissionType.MoveItem)
            {
                iconImage.sprite = CreateMoveItemIcon();
            }
            else
            {
                iconImage.sprite = CreateProceduralMissionIcon();
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
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size * 0.4f;
            Color enemyColor = GemColors.GetColor(GemType.Gray);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float dist = pos.magnitude;
                    float angle = Mathf.Atan2(pos.y, pos.x);
                    if (angle < 0) angle += Mathf.PI * 2f;

                    float hexAngle = Mathf.PI / 3f;
                    float sectorAngle = angle % hexAngle;
                    float cosAngle = Mathf.Cos(sectorAngle - hexAngle / 2f);
                    float hexDist = outerRadius * cosAngle;

                    if (dist < hexDist)
                    {
                        Color c = enemyColor;
                        // X 표시 (퇴치 의미)
                        float xDist = Mathf.Min(
                            Mathf.Abs(pos.x - pos.y) / 1.414f,
                            Mathf.Abs(pos.x + pos.y) / 1.414f);
                        if (xDist < size * 0.04f && dist > hexDist * 0.15f)
                            c = new Color(0.9f, 0.15f, 0.15f);
                        else if (dist > hexDist * 0.85f)
                            c = Color.Lerp(c, Color.white, 0.4f);
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

        public void SetItem(ItemData data)
        {
            itemData = data;

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

            if (isUnlocked)
            {
                if (countText != null)
                {
                    countText.text = data.count > 0 ? data.count.ToString() : "+";
                }
            }

            if (button != null)
            {
                button.interactable = isUnlocked && data.count > 0;
            }
        }

        public void OnItemClicked()
        {
            if (itemData != null && itemData.count > 0)
            {
                // 아이템 사용
                ItemManager itemManager = FindObjectOfType<ItemManager>();
                itemManager?.UseItem(itemData.type);
            }
            else
            {
                // 구매 UI 표시
                Debug.Log($"Open purchase for {itemData?.type}");
            }
        }
    }
}

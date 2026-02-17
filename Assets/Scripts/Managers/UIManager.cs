using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

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

        public void SetTurnText(Text text) { turnText = text; }
        public void SetScoreText(Text text)
        {
            goldText = text;
            if (text != null) scoreDefaultColor = text.color;
        }
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
            ShowPopup(stageClearPopup);
        }

        /// <summary>
        /// 스테이지 클리어 팝업 표시 (점수 브레이크다운 포함)
        /// </summary>
        public void ShowStageClearPopup(StageSummaryData summary)
        {
            ShowPopup(stageClearPopup);
            StartCoroutine(AnimateStageClearBreakdown(summary));
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

        private IEnumerator AnimateStageClearBreakdown(StageSummaryData summary)
        {
            // 팝업 등장 대기
            yield return new WaitForSeconds(popupAnimationDuration + 0.2f);

            // 타이틀 표시
            if (clearTitleText != null)
            {
                int stage = GameManager.Instance != null ? GameManager.Instance.CurrentStage : 1;
                clearTitleText.text = $"STAGE {stage} CLEAR!";
            }

            // 순차 등장: 각 줄 0.2초 간격으로 카운팅
            yield return StartCoroutine(AnimateScoreLine(clearBaseScoreText, summary.baseScore, 0.4f));
            yield return new WaitForSeconds(0.15f);

            if (summary.remainingTurnsBonus > 0)
            {
                yield return StartCoroutine(AnimateScoreLine(clearTurnBonusText,
                    summary.remainingTurnsBonus, 0.4f, $"+{{0}} (x{summary.remainingTurns})"));
                yield return new WaitForSeconds(0.15f);
            }
            else if (clearTurnBonusText != null)
            {
                clearTurnBonusText.text = "+0";
            }

            if (summary.efficiencyBonus > 0)
            {
                yield return StartCoroutine(AnimateScoreLine(clearEfficiencyBonusText,
                    summary.efficiencyBonus, 0.3f, "+{0}"));
                yield return new WaitForSeconds(0.15f);
            }
            else if (clearEfficiencyBonusText != null)
            {
                clearEfficiencyBonusText.text = "+0";
            }

            // 총점: 강조 표시
            if (clearTotalScoreText != null)
            {
                yield return StartCoroutine(AnimateScoreLine(clearTotalScoreText,
                    summary.totalScore, 0.6f));

                // 스케일 펀치
                RectTransform totalRt = clearTotalScoreText.GetComponent<RectTransform>();
                if (totalRt != null)
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
                        totalRt.localScale = Vector3.one * scale;
                        yield return null;
                    }
                    totalRt.localScale = Vector3.one;
                }
            }

            // 별 등급 표시
            yield return new WaitForSeconds(0.2f);
            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length && i < summary.starRating; i++)
                {
                    if (starImages[i] != null)
                    {
                        starImages[i].enabled = true;
                        starImages[i].color = new Color(1f, 0.84f, 0f); // Gold
                        // EaseOutBack 스케일
                        yield return StartCoroutine(StarPopAnimation(starImages[i]));
                        yield return new WaitForSeconds(0.15f);
                    }
                }
                // 나머지 별 비활성화
                for (int i = summary.starRating; i < starImages.Length; i++)
                {
                    if (starImages[i] != null)
                    {
                        starImages[i].enabled = true;
                        starImages[i].color = new Color(0.3f, 0.3f, 0.3f, 0.5f); // 어둡게
                    }
                }
            }
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
    }

    /// <summary>
    /// 미션 UI 컴포넌트
    /// </summary>
    [System.Serializable]
    public class MissionUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text countText;
        [SerializeField] private Image progressFill;

        private MissionData missionData;

        public void SetMission(MissionData data)
        {
            missionData = data;

            if (iconImage != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.color = GemColors.GetColor(data.targetGemType);
            }

            UpdateProgress(data.currentCount, data.targetCount);
        }

        public void UpdateProgress(int current, int target)
        {
            if (countText != null)
            {
                countText.text = $"x {target - current}";
            }

            if (progressFill != null)
            {
                progressFill.fillAmount = (float)current / target;
            }
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

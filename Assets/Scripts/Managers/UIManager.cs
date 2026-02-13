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
        private Coroutine turnBounceCoroutine;
        private Coroutine turnPulseCoroutine;

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
            HideAllPopups();
            Time.timeScale = 1f;
            GameManager.Instance?.RetryStage();
        }

        private void OnBuyTurnButtonClicked()
        {
            // 턴 구매 프로세스
            // TODO: IAP 연동
            Debug.Log("Buy Turn Clicked");

            // 테스트용: 5턴 추가
            GameManager.Instance?.AddTurns(5);
            HideGameOverPopup();
        }

        private void OnNextStageButtonClicked()
        {
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

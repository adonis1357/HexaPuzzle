using UnityEngine;
using UnityEngine.UI;
using System.Collections;
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
        
        private void Start()
        {
            SetupButtons();
            HideAllPopups();
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
        
        /// <summary>
        /// 턴 표시 업데이트
        /// </summary>
        public void UpdateTurnDisplay(int turns)
        {
            if (turnText != null)
            {
                turnText.text = turns.ToString();
                
                // 턴이 적으면 색상 변경
                turnText.color = turns <= 5 ? Color.red : Color.white;
            }
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
        /// 점수(골드) 표시 업데이트
        /// </summary>
        public void UpdateScoreDisplay(int score)
        {
            if (goldText != null)
            {
                goldText.text = FormatNumber(score);
            }
        }
        
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
        /// 스테이지 클리어 팝업 표시
        /// </summary>
        public void ShowStageClearPopup()
        {
            ShowPopup(stageClearPopup);
        }
        
        /// <summary>
        /// 도움말 팝업 표시
        /// </summary>
        public void ShowHelpPopup()
        {
            HidePopup(pausePopup);
            ShowPopup(helpPopup);
        }
        
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
        
        // 버튼 이벤트 핸들러들
        private void OnPauseButtonClicked()
        {
            GameManager.Instance?.PauseGame();
        }
        
        private void OnRotationToggleClicked()
        {
            isClockwise = !isClockwise;
            
            if (rotationDirectionIcon != null)
            {
                rotationDirectionIcon.sprite = isClockwise ? clockwiseIcon : counterClockwiseIcon;
            }
            
            GameManager.Instance?.ToggleRotationDirection();
        }
        
        private void OnOutButtonClicked()
        {
            GameManager.Instance?.ExitToLobby();
        }
        
        private void OnHelpButtonClicked()
        {
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

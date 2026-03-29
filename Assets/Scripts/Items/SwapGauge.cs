using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    public class SwapGauge : MonoBehaviour
    {
        public static SwapGauge Instance { get; private set; }

        public enum GaugeState { Inactive, Ready, UseReady }

        private static readonly Color COLOR_INACTIVE  = new Color(0f, 0.2f, 0f, 1f);
        private static readonly Color COLOR_READY     = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color COLOR_USE_READY = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color COLOR_FLASH     = Color.white;

        private const int CHARGE_PER_USE = 50;
        private int gauge = 0;
        private int usesAvailable = 0;
        private GaugeState currentState = GaugeState.Inactive;

        private int GetMaxGauge()
        {
            int level = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetSwapLevel() : 0;
            return CHARGE_PER_USE * (1 + level);
        }
        private int CalculateUses() => gauge / CHARGE_PER_USE;

        private static readonly Color COLOR_OUTLINE_ACTIVE = new Color(1f, 0.9f, 0f, 1f);
        private static readonly Color COLOR_OUTLINE_OFF = new Color(0f, 0f, 0f, 0f);

        private Button itemButton;
        private SwapItem swapItem;
        private Image buttonImage;
        private Outline buttonOutline;
        private bool initialized = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            gauge = 0;
            currentState = GaugeState.Inactive;
            StartCoroutine(AutoInitCoroutine());
        }

        private IEnumerator AutoInitCoroutine()
        {
            yield return null;
            yield return null;

            int round = 0;
            while (!initialized)
            {
                round++;
                TryFindButton();
                if (initialized)
                {
                    Debug.Log($"[SwapGauge] 초기화 성공: {itemButton.gameObject.name} (라운드 {round})");
                    yield break;
                }
                float wait = round <= 5 ? 0.5f : (round <= 15 ? 2f : 5f);
                yield return new WaitForSeconds(wait);
            }
        }

        private void TryFindButton()
        {
            if (initialized) return;

            // 1순위: UIManager.ItemButtons에서 ItemType.Bomb (스왑 아이템)
            if (itemButton == null)
            {
                var uiMgr = FindObjectOfType<UIManager>();
                if (uiMgr != null && uiMgr.ItemButtons != null)
                {
                    foreach (var ib in uiMgr.ItemButtons)
                    {
                        if (ib != null && ib.CurrentItemType == ItemType.Bomb && ib.ButtonComponent != null)
                        {
                            itemButton = ib.ButtonComponent;
                            break;
                        }
                    }
                }
            }

            // 2순위: 이름 검색
            if (itemButton == null)
            {
                foreach (var btn in FindObjectsOfType<Button>())
                {
                    if (btn == null) continue;
                    string n = btn.gameObject.name.ToLower();
                    if (n.Contains("swap") || n.Contains("스왑"))
                    {
                        itemButton = btn;
                        break;
                    }
                }
            }

            // 3순위: SwapItem에서 직접
            if (itemButton == null)
            {
                if (swapItem == null) swapItem = FindObjectOfType<SwapItem>();
                if (swapItem != null)
                {
                    var btn = swapItem.GetComponentInChildren<Button>();
                    if (btn != null) itemButton = btn;
                }
            }

            if (swapItem == null) swapItem = FindObjectOfType<SwapItem>();

            if (itemButton != null)
            {
                initialized = true;
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(OnButtonClicked);

                buttonImage = itemButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.type = Image.Type.Filled;
                    buttonImage.fillMethod = Image.FillMethod.Vertical;
                    buttonImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                }

                buttonOutline = itemButton.GetComponent<Outline>();
                if (buttonOutline == null)
                    buttonOutline = itemButton.gameObject.AddComponent<Outline>();
                buttonOutline.effectColor = COLOR_OUTLINE_OFF;

                ForceAlphaOne(itemButton.gameObject);
                SetState(GaugeState.Inactive);
            }
        }

        private void SetState(GaugeState newState)
        {
            currentState = newState;
            RefreshUI();
        }

        private void OnButtonClicked()
        {
            if (JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode()) { AddGaugeEditor(); return; }

            if (currentState == GaugeState.Ready)
            {
                SetState(GaugeState.UseReady);
                if (swapItem == null) swapItem = FindObjectOfType<SwapItem>();
                if (swapItem != null) swapItem.Activate();
            }
            else if (currentState == GaugeState.UseReady)
            {
                if (swapItem != null) swapItem.Deactivate();
                SetState(GaugeState.Ready);
            }
        }

        public void OnItemUsed()
        {
            gauge = Mathf.Max(0, gauge - CHARGE_PER_USE);
            usesAvailable = CalculateUses();
            int maxG = GetMaxGauge();
            if (gauge >= maxG && usesAvailable >= 1) SetState(GaugeState.Ready);
            else SetState(GaugeState.Inactive);
        }

        public void OnItemCancelled()
        {
            if (currentState == GaugeState.UseReady)
                SetState(GaugeState.Ready);
        }

        public void ResetGauge()
        {
            gauge = 0; usesAvailable = 0;
            currentState = GaugeState.Inactive;
            if (itemButton != null) itemButton.interactable = false;
            RefreshUI();
        }

        public void AddGauge(int amount)
        {
            if (currentState != GaugeState.Inactive) return;
            int maxG = GetMaxGauge();
            gauge = Mathf.Min(gauge + amount, maxG);
            usesAvailable = CalculateUses();
            if (gauge >= maxG && currentState == GaugeState.Inactive)
            {
                SetState(GaugeState.Ready);
                StartCoroutine(FlashEffect());
            }
            else { RefreshUI(); }
        }

        public void OnTurnEnd() { AddGauge(5); }

        private IEnumerator FlashEffect()
        {
            if (buttonImage == null) yield break;
            for (int i = 0; i < 3; i++)
            {
                buttonImage.color = COLOR_FLASH;
                yield return new WaitForSeconds(0.1f);
                buttonImage.color = COLOR_READY;
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void RefreshUI()
        {
            if (buttonImage == null) return;
            int maxG = GetMaxGauge();
            float ratio = maxG > 0 ? gauge / (float)maxG : 0f;
            switch (currentState)
            {
                case GaugeState.Inactive:
                    buttonImage.fillAmount = ratio;
                    buttonImage.color = COLOR_READY;
                    if (itemButton != null) itemButton.interactable = JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode();
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_OFF;
                    break;
                case GaugeState.Ready:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_READY;
                    if (itemButton != null) itemButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    break;
                case GaugeState.UseReady:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_USE_READY;
                    if (itemButton != null) itemButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    break;
            }
        }

        private void ForceAlphaOne(GameObject obj)
        {
            if (obj == null) return;
            Transform t = obj.transform;
            while (t != null)
            {
                CanvasGroup cg = t.GetComponent<CanvasGroup>();
                if (cg != null && cg.alpha < 1f) cg.alpha = 1f;
                t = t.parent;
            }
        }

        public int CurrentGauge => gauge;
        public GaugeState CurrentState => currentState;

        /// <summary>에디터 테스트용: gauge를 CHARGE_PER_USE만큼 증가 (1레이어 추가)</summary>
        public void AddGaugeEditor()
        {
            Debug.Log($"[EditorGauge] 스왑 게이지추가모드 진입 — gauge: {gauge} → {Mathf.Min(gauge + CHARGE_PER_USE, GetMaxGauge())}");
            gauge = Mathf.Min(gauge + CHARGE_PER_USE, GetMaxGauge());
            usesAvailable = CalculateUses();
            if (gauge >= CHARGE_PER_USE && currentState == GaugeState.Inactive)
                SetState(GaugeState.Ready);
            else
                RefreshUI();
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    public class HammerGauge : MonoBehaviour
    {
        public static HammerGauge Instance { get; private set; }

        public enum HammerState { Inactive, Ready, UseReady, UseReady1 }

        // 색상
        private static readonly Color COLOR_INACTIVE    = new Color(0.3f, 0f, 0f, 1f);
        private static readonly Color COLOR_READY       = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color COLOR_USE_READY   = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color COLOR_USE_READY1  = new Color(1f, 0.1f, 0.1f, 1f);
        private static readonly Color COLOR_FLASH       = Color.white;
        private static readonly Color COLOR_OUTLINE_ACTIVE  = new Color(1f, 0.9f, 0f, 1f);
        private static readonly Color COLOR_OUTLINE_BRIGHT  = new Color(1f, 1f, 0.3f, 1f);
        private static readonly Color COLOR_OUTLINE_OFF     = new Color(0f, 0f, 0f, 0f);

        private const int CHARGE_PER_USE = 50;
        private int gauge = 0;
        private HammerState currentState = HammerState.Inactive;

        private int GetMaxGauge()
        {
            int level = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetHammerLevel() : 0;
            return CHARGE_PER_USE * (1 + level);
        }

        private Button hammerButton;
        private HammerItem hammerItem;
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
            currentState = HammerState.Inactive;
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
                if (initialized) yield break;
                float wait = round <= 5 ? 0.5f : (round <= 15 ? 2f : 5f);
                yield return new WaitForSeconds(wait);
            }
        }

        private void TryFindButton()
        {
            if (initialized) return;
            if (hammerButton == null)
            {
                if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();
                if (hammerItem != null && hammerItem.HammerButton != null)
                    hammerButton = hammerItem.HammerButton;
            }
            if (hammerButton == null)
            {
                var uiMgr = FindObjectOfType<UIManager>();
                if (uiMgr != null && uiMgr.ItemButtons != null)
                    foreach (var ib in uiMgr.ItemButtons)
                        if (ib != null && ib.CurrentItemType == ItemType.Hammer && ib.ButtonComponent != null)
                        { hammerButton = ib.ButtonComponent; break; }
            }
            if (hammerButton == null)
                foreach (var btn in FindObjectsOfType<Button>())
                {
                    if (btn == null) continue;
                    string n = btn.gameObject.name.ToLower();
                    if (n.Contains("hammer") || n.Contains("망치")) { hammerButton = btn; break; }
                }
            if (hammerButton == null)
            {
                if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();
                if (hammerItem != null) { var btn = hammerItem.GetComponentInChildren<Button>(); if (btn != null) hammerButton = btn; }
            }
            if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();

            if (hammerButton != null)
            {
                initialized = true;
                hammerButton.onClick.RemoveAllListeners();
                hammerButton.onClick.AddListener(OnHammerButtonClicked);
                buttonImage = hammerButton.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.type = Image.Type.Filled;
                    buttonImage.fillMethod = Image.FillMethod.Vertical;
                    buttonImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                }
                buttonOutline = hammerButton.GetComponent<Outline>();
                if (buttonOutline == null) buttonOutline = hammerButton.gameObject.AddComponent<Outline>();
                buttonOutline.effectColor = COLOR_OUTLINE_OFF;
                ForceAlphaOne(hammerButton.gameObject);
                SetState(HammerState.Inactive);
            }
        }

        private void SetState(HammerState newState)
        {
            currentState = newState;
            RefreshUI();
        }

        // ============================================================
        // 버튼 클릭: Ready → UseReady → UseReady1(조건부) → Inactive
        // ============================================================

        private void OnHammerButtonClicked()
        {
            if (currentState == HammerState.Ready)
            {
                // Ready → UseReady (기본 망치)
                SetState(HammerState.UseReady);
                if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();
                if (hammerItem != null) hammerItem.Activate();
            }
            else if (currentState == HammerState.UseReady)
            {
                // UseReady → UseReady1 (게이지 100+ && Lv1 해금) 또는 Inactive
                bool canUpgrade = gauge >= 100
                    && SkillTreeManager.Instance != null
                    && SkillTreeManager.Instance.GetHammerLevel() >= 1;

                if (canUpgrade)
                {
                    SetState(HammerState.UseReady1);
                    // HammerItem은 이미 Activate 상태 유지
                }
                else
                {
                    if (hammerItem != null) hammerItem.Deactivate();
                    SetState(gauge >= CHARGE_PER_USE ? HammerState.Ready : HammerState.Inactive);
                }
            }
            else if (currentState == HammerState.UseReady1)
            {
                // UseReady1 → Inactive
                if (hammerItem != null) hammerItem.Deactivate();
                SetState(gauge >= CHARGE_PER_USE ? HammerState.Ready : HammerState.Inactive);
            }
        }

        // ============================================================
        // 외부 호출
        // ============================================================

        /// <summary>기본 망치 사용 완료 (1칸 파괴) — 게이지 50 소모</summary>
        public void OnHammerUsed()
        {
            gauge = Mathf.Max(0, gauge - CHARGE_PER_USE);
            SetState(gauge >= CHARGE_PER_USE ? HammerState.Ready : HammerState.Inactive);
        }

        /// <summary>1레벨 망치 사용 완료 (7칸 파괴) — 게이지 100 소모</summary>
        public void OnHammerUsedLevel1()
        {
            gauge = Mathf.Max(0, gauge - CHARGE_PER_USE * 2);
            SetState(gauge >= CHARGE_PER_USE ? HammerState.Ready : HammerState.Inactive);
        }

        public void OnHammerCancelled()
        {
            if (currentState == HammerState.UseReady || currentState == HammerState.UseReady1)
                SetState(gauge >= CHARGE_PER_USE ? HammerState.Ready : HammerState.Inactive);
        }

        public void ResetGauge()
        {
            gauge = 0;
            currentState = HammerState.Inactive;
            if (hammerButton != null) hammerButton.interactable = false;
            RefreshUI();
        }

        // ============================================================
        // 게이지 충전: 50 이상이면 Ready
        // ============================================================

        public void AddGauge(int amount)
        {
            if (currentState != HammerState.Inactive) return;
            int maxG = GetMaxGauge();
            gauge = Mathf.Min(gauge + amount, maxG);

            if (gauge >= CHARGE_PER_USE && currentState == HammerState.Inactive)
            {
                SetState(HammerState.Ready);
                StartCoroutine(FlashEffect());
            }
            else
            {
                RefreshUI();
            }
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

        // ============================================================
        // UI
        // ============================================================

        private void RefreshUI()
        {
            if (buttonImage == null) return;
            int maxG = GetMaxGauge();
            float ratio = maxG > 0 ? gauge / (float)maxG : 0f;

            switch (currentState)
            {
                case HammerState.Inactive:
                    buttonImage.fillAmount = ratio;
                    buttonImage.color = COLOR_READY;
                    if (hammerButton != null) hammerButton.interactable = false;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_OFF;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one;
                    break;

                case HammerState.Ready:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_READY;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one;
                    break;

                case HammerState.UseReady:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_USE_READY;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one;
                    break;

                case HammerState.UseReady1:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_USE_READY1;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_BRIGHT;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one * 1.2f;
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
        public HammerState CurrentState => currentState;
    }
}

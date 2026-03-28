using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    /// <summary>
    /// 망치 게이지 3단계 상태머신.
    /// Inactive(0~19) → Ready(20/활성) → UseReady(망치 모드) → 사용 시 Inactive / 취소 시 Ready.
    /// 버튼 탐색 실패 시 무한 재시도 (절대 코루틴 종료 안 함).
    /// </summary>
    public class HammerGauge : MonoBehaviour
    {
        public static HammerGauge Instance { get; private set; }

        public enum HammerState { Inactive, Ready, UseReady }

        // 색상 상수
        private static readonly Color COLOR_INACTIVE  = new Color(0.3f, 0f, 0f, 1f);
        private static readonly Color COLOR_READY     = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color COLOR_USE_READY = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color COLOR_FLASH     = Color.white;

        // 상태
        private const int CHARGE_PER_USE = 10; // 1회 사용 = 10 충전
        private int gauge = 0;
        private int usesAvailable = 0; // 현재 사용 가능 횟수
        private HammerState currentState = HammerState.Inactive;

        /// <summary>스킬 레벨 기반 최대 게이지 (10/20/30/40)</summary>
        private int GetMaxGauge()
        {
            int level = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetHammerLevel() : 0;
            return CHARGE_PER_USE * (1 + level); // Lv0=10, Lv1=20, Lv2=30, Lv3=40
        }

        /// <summary>현재 사용 가능 횟수 (gauge / CHARGE_PER_USE)</summary>
        private int CalculateUses() => gauge / CHARGE_PER_USE;

        private static readonly Color COLOR_OUTLINE_ACTIVE = new Color(1f, 0.9f, 0f, 1f);
        private static readonly Color COLOR_OUTLINE_OFF = new Color(0f, 0f, 0f, 0f);

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

        // ============================================================
        // 자동 초기화 — 무한 재시도 (절대 종료 안 함)
        // ============================================================

        private IEnumerator AutoInitCoroutine()
        {
            // 초기 2프레임 대기
            yield return null;
            yield return null;

            int round = 0;
            while (!initialized)
            {
                round++;
                Debug.Log($"[HammerGauge] 버튼 탐색 라운드 {round}");

                TryFindButton();

                if (initialized)
                {
                    Debug.Log($"[HammerGauge] ★ 초기화 성공: {hammerButton.gameObject.name} (라운드 {round})");
                    yield break;
                }

                // 점점 간격 늘려서 재시도 (0.5초 → 2초 → 5초)
                float wait = round <= 5 ? 0.5f : (round <= 15 ? 2f : 5f);
                Debug.Log($"[HammerGauge] 버튼 못 찾음, {wait}초 후 재시도...");
                yield return new WaitForSeconds(wait);
            }
        }

        private void TryFindButton()
        {
            if (initialized) return;

            // ---- 1순위: HammerItem.HammerButton ----
            if (hammerButton == null)
            {
                if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();
                if (hammerItem != null && hammerItem.HammerButton != null)
                {
                    hammerButton = hammerItem.HammerButton;
                    Debug.Log("[HammerGauge] 탐색 1순위 성공: HammerItem.HammerButton");
                }
            }

            // ---- 2순위: UIManager.ItemButtons에서 ItemType.Hammer ----
            if (hammerButton == null)
            {
                var uiMgr = FindObjectOfType<UIManager>();
                if (uiMgr != null && uiMgr.ItemButtons != null)
                {
                    foreach (var ib in uiMgr.ItemButtons)
                    {
                        if (ib != null && ib.CurrentItemType == ItemType.Hammer && ib.ButtonComponent != null)
                        {
                            hammerButton = ib.ButtonComponent;
                            Debug.Log("[HammerGauge] 탐색 2순위 성공: UIManager.ItemButtons");
                            break;
                        }
                    }
                }
            }

            // ---- 3순위: FindObjectsOfType<Button> 이름 검색 ----
            if (hammerButton == null)
            {
                foreach (var btn in FindObjectsOfType<Button>())
                {
                    if (btn == null) continue;
                    string n = btn.gameObject.name.ToLower();
                    if (n.Contains("hammer") || n.Contains("망치"))
                    {
                        hammerButton = btn;
                        Debug.Log($"[HammerGauge] 탐색 3순위 성공: 이름 '{btn.gameObject.name}'");
                        break;
                    }
                }
            }

            // ---- 4순위: HammerItem 컴포넌트에서 Button 직접 ----
            if (hammerButton == null)
            {
                if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();
                if (hammerItem != null)
                {
                    var btn = hammerItem.GetComponentInChildren<Button>();
                    if (btn != null)
                    {
                        hammerButton = btn;
                        Debug.Log("[HammerGauge] 탐색 4순위 성공: HammerItem.GetComponentInChildren<Button>");
                    }
                }
            }

            // HammerItem도 확보
            if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();

            // ---- 초기화 완료 ----
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

                // Outline 확보 (없으면 추가)
                buttonOutline = hammerButton.GetComponent<Outline>();
                if (buttonOutline == null)
                    buttonOutline = hammerButton.gameObject.AddComponent<Outline>();
                buttonOutline.effectColor = COLOR_OUTLINE_OFF;

                ForceAlphaOne(hammerButton.gameObject);
                SetState(HammerState.Inactive);
            }
        }

        // ============================================================
        // 상태 전환
        // ============================================================

        private void SetState(HammerState newState)
        {
            currentState = newState;
            RefreshUI();
        }

        // ============================================================
        // 버튼 클릭 처리
        // ============================================================

        private void OnHammerButtonClicked()
        {
            if (currentState == HammerState.Ready)
            {
                SetState(HammerState.UseReady);
                if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();
                if (hammerItem != null) hammerItem.Activate();
            }
            else if (currentState == HammerState.UseReady)
            {
                if (hammerItem != null) hammerItem.Deactivate();
                SetState(HammerState.Ready);
            }
        }

        // ============================================================
        // 외부 호출: 사용 완료 / 취소
        // ============================================================

        public void OnHammerUsed()
        {
            gauge = Mathf.Max(0, gauge - CHARGE_PER_USE);
            usesAvailable = CalculateUses();

            if (usesAvailable >= 1)
                SetState(HammerState.Ready); // 아직 사용 가능 횟수 남음
            else
                SetState(HammerState.Inactive);
        }

        public void OnHammerCancelled()
        {
            if (currentState == HammerState.UseReady)
                SetState(HammerState.Ready);
        }

        /// <summary>게임 재시작 시 전체 초기화</summary>
        public void ResetGauge()
        {
            gauge = 0;
            usesAvailable = 0;
            currentState = HammerState.Inactive;
            if (hammerButton != null) hammerButton.interactable = false;
            RefreshUI();
        }

        // ============================================================
        // 게이지 충전
        // ============================================================

        public void AddGauge(int amount)
        {
            if (currentState != HammerState.Inactive) return;

            int maxG = GetMaxGauge();
            gauge = Mathf.Min(gauge + amount, maxG);
            usesAvailable = CalculateUses();

            if (usesAvailable >= 1 && currentState == HammerState.Inactive)
            {
                SetState(HammerState.Ready);
                StartCoroutine(FlashEffect());
            }
            else
            {
                RefreshUI();
            }
        }

        public void OnTurnEnd()
        {
            AddGauge(3);
        }

        // ============================================================
        // 활성화 연출
        // ============================================================

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
        // UI 갱신 (색상 상수만 사용)
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
                    break;

                case HammerState.Ready:
                    buttonImage.fillAmount = Mathf.Max(ratio, (float)usesAvailable / Mathf.Max(1, maxG / CHARGE_PER_USE));
                    buttonImage.color = COLOR_READY;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    break;

                case HammerState.UseReady:
                    buttonImage.fillAmount = Mathf.Max(ratio, (float)usesAvailable / Mathf.Max(1, maxG / CHARGE_PER_USE));
                    buttonImage.color = COLOR_USE_READY;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    break;
            }
        }

        /// <summary>자신과 모든 부모의 CanvasGroup alpha를 1f로 강제 설정</summary>
        private void ForceAlphaOne(GameObject obj)
        {
            if (obj == null) return;
            Transform t = obj.transform;
            while (t != null)
            {
                CanvasGroup cg = t.GetComponent<CanvasGroup>();
                if (cg != null && cg.alpha < 1f)
                {
                    cg.alpha = 1f;
                    Debug.Log($"[HammerGauge] CanvasGroup alpha 강제 1f: {t.name}");
                }
                t = t.parent;
            }
        }

        // ============================================================
        // 외부 조회
        // ============================================================

        public int CurrentGauge => gauge;
        public HammerState CurrentState => currentState;
    }
}

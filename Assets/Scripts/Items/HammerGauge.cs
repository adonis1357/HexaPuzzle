using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    public class HammerGauge : MonoBehaviour
    {
        public static HammerGauge Instance { get; private set; }

        public enum HammerState
        {
            Inactive,    // 비활성
            Ready,       // gaugeLayer>=1, 클릭 대기
            UseReady0,   // 기본 망치 준비 (gaugeLayer>=1)
            UseReady1,   // 1레벨 망치 준비 (gaugeLayer>=2 AND Level1 해금)
            UseReady2,   // 2레벨 망치 준비 (gaugeLayer>=3 AND Level2 해금)
            UseReady3    // 3레벨 망치 준비 (gaugeLayer>=4 AND Level3 해금)
        }

        // 레이어별 색상
        private static readonly Color COLOR_LAYER_0 = new Color(0.8f, 0.1f, 0.1f, 1f);    // 기본 빨간
        private static readonly Color COLOR_LAYER_1 = new Color(1f, 0f, 0f, 1f);           // 진한 빨간
        private static readonly Color COLOR_LAYER_2 = new Color(1f, 0.3f, 0f, 1f);         // 주황빨간
        private static readonly Color COLOR_LAYER_3 = new Color(1f, 0.5f, 0f, 1f);         // 밝은 주황

        // UseReady 오버레이 색상
        private static readonly Color COLOR_USE_READY0 = new Color(1f, 0.2f, 0.2f, 1f);    // 빨간 오버레이
        private static readonly Color COLOR_USE_READY1 = new Color(0.9f, 0.05f, 0.05f, 1f);// 진한 빨간+불꽃
        private static readonly Color COLOR_USE_READY2 = new Color(1f, 0.3f, 0f, 1f);      // 주황빨간
        private static readonly Color COLOR_USE_READY3 = new Color(1f, 0.5f, 0f, 1f);      // 밝은 주황

        private static readonly Color COLOR_FLASH       = Color.white;
        private static readonly Color COLOR_OUTLINE_ACTIVE  = new Color(1f, 0.9f, 0f, 1f);
        private static readonly Color COLOR_OUTLINE_BRIGHT  = new Color(1f, 1f, 0.3f, 1f);
        private static readonly Color COLOR_OUTLINE_OFF     = new Color(0f, 0f, 0f, 0f);

        private const int LAYER_SIZE = 50;

        // 레이어 시스템
        private int gaugeLayer = 0;       // 0~4 (완성된 레이어 수)
        private int gaugeInLayer = 0;     // 0~49 (현재 레이어 내 진행률)

        private HammerState currentState = HammerState.Inactive;

        private Button hammerButton;
        private HammerItem hammerItem;
        private Image buttonImage;
        private Outline buttonOutline;
        private Text layerText;
        private bool initialized = false;

        /// <summary>현재 해금된 스킬에 따른 최대 레이어 수</summary>
        public int GetCurrentMaxLayer() => GetMaxLayer();
        private int GetMaxLayer()
        {
            int level = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetHammerLevel() : 0;
            // HammerLevel1 미해금=1, Level1=2, Level2=3, Level3=4
            return 1 + level;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void Start()
        {
            gaugeLayer = 0;
            gaugeInLayer = 0;
            currentState = HammerState.Inactive;
            // 즉시 버튼을 찾아 비활성화 색상 적용 (AutoInit 대기 전 흰색 방지)
            ApplyInactiveColorImmediate();
            StartCoroutine(AutoInitCoroutine());
        }

        /// <summary>Start에서 즉시 호출: 버튼이 이미 존재하면 비활성화 색상 즉시 적용</summary>
        private void ApplyInactiveColorImmediate()
        {
            Button btn = null;
            var hi = FindObjectOfType<HammerItem>();
            if (hi != null && hi.HammerButton != null) btn = hi.HammerButton;
            if (btn == null)
            {
                var uiMgr = FindObjectOfType<UIManager>();
                if (uiMgr != null && uiMgr.ItemButtons != null)
                    foreach (var ib in uiMgr.ItemButtons)
                        if (ib != null && ib.CurrentItemType == ItemType.Hammer && ib.ButtonComponent != null)
                        { btn = ib.ButtonComponent; break; }
            }
            if (btn != null)
            {
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = COLOR_LAYER_0;
                    img.fillAmount = 0f;
                }
                btn.interactable = false;
            }
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

                // 레이어 숫자 텍스트 생성
                CreateLayerText();

                ForceAlphaOne(hammerButton.gameObject);
                SetState(HammerState.Inactive);
            }
        }

        private void CreateLayerText()
        {
            if (hammerButton == null) return;

            // 기존 텍스트가 있으면 재사용
            var existing = hammerButton.transform.Find("LayerText");
            if (existing != null)
            {
                layerText = existing.GetComponent<Text>();
                return;
            }

            GameObject textObj = new GameObject("LayerText");
            textObj.transform.SetParent(hammerButton.transform, false);

            layerText = textObj.AddComponent<Text>();
            layerText.text = "";
            layerText.fontSize = 24;
            layerText.fontStyle = FontStyle.Bold;
            layerText.alignment = TextAnchor.UpperRight;
            layerText.color = Color.white;
            layerText.raycastTarget = false;
            layerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Outline 추가
            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.anchoredPosition = new Vector2(-11f, -8f);
        }

        private void SetState(HammerState newState)
        {
            currentState = newState;
            RefreshUI();
        }

        // ============================================================
        // 버튼 클릭: Ready → UseReady0 → UseReady1 → ... → Inactive
        // ============================================================

        private void OnHammerButtonClicked()
        {
            if (JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode()) { AddGaugeEditor(); return; }

            if (currentState == HammerState.Ready)
            {
                // Ready → UseReady0 (기본 망치)
                SetState(HammerState.UseReady0);
                if (hammerItem == null) hammerItem = FindObjectOfType<HammerItem>();
                if (hammerItem != null) hammerItem.Activate();
            }
            else if (currentState == HammerState.UseReady0)
            {
                // UseReady0 → UseReady1 (조건 충족 시) 또는 Inactive
                if (CanUseLevel(1))
                {
                    SetState(HammerState.UseReady1);
                }
                else
                {
                    CancelAndReturn();
                }
            }
            else if (currentState == HammerState.UseReady1)
            {
                // UseReady1 → UseReady2 (조건 충족 시) 또는 Inactive
                if (CanUseLevel(2))
                {
                    SetState(HammerState.UseReady2);
                }
                else
                {
                    CancelAndReturn();
                }
            }
            else if (currentState == HammerState.UseReady2)
            {
                // UseReady2 → UseReady3 (조건 충족 시) 또는 Inactive
                if (CanUseLevel(3))
                {
                    SetState(HammerState.UseReady3);
                }
                else
                {
                    CancelAndReturn();
                }
            }
            else if (currentState == HammerState.UseReady3)
            {
                // UseReady3 → Inactive
                CancelAndReturn();
            }
        }

        /// <summary>해당 레벨 사용 가능 여부 (게이지 레이어 + 스킬 해금)</summary>
        private bool CanUseLevel(int level)
        {
            int hammerLevel = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetHammerLevel() : 0;
            // Level1: gaugeLayer>=2 AND HammerLevel1 해금
            // Level2: gaugeLayer>=3 AND HammerLevel2 해금
            // Level3: gaugeLayer>=4 AND HammerLevel3 해금
            return gaugeLayer >= (level + 1) && hammerLevel >= level;
        }

        private void CancelAndReturn()
        {
            if (hammerItem != null) hammerItem.Deactivate();
            SetState(gaugeLayer >= 1 ? HammerState.Ready : HammerState.Inactive);
        }

        // ============================================================
        // 외부 호출
        // ============================================================

        /// <summary>망치 사용 완료 — 현재 UseReady 레벨에 따라 게이지 레이어 차감</summary>
        public void OnHammerUsedWithLevel(int layerCost)
        {
            gaugeLayer = Mathf.Max(0, gaugeLayer - layerCost);
            SetState(gaugeLayer >= 1 ? HammerState.Ready : HammerState.Inactive);
        }

        /// <summary>기본 망치 사용 완료 (1칸 파괴) — 레이어 1 소모</summary>
        public void OnHammerUsed()
        {
            OnHammerUsedWithLevel(1);
        }

        /// <summary>1레벨 망치 사용 완료 (7칸 파괴) — 레이어 2 소모</summary>
        public void OnHammerUsedLevel1()
        {
            OnHammerUsedWithLevel(2);
        }

        /// <summary>2레벨 망치 사용 완료 (19칸 파괴) — 레이어 3 소모</summary>
        public void OnHammerUsedLevel2()
        {
            OnHammerUsedWithLevel(3);
        }

        /// <summary>3레벨 망치 사용 완료 (37칸 파괴) — 레이어 4 소모</summary>
        public void OnHammerUsedLevel3()
        {
            OnHammerUsedWithLevel(4);
        }

        public void OnHammerCancelled()
        {
            if (currentState == HammerState.UseReady0 || currentState == HammerState.UseReady1
                || currentState == HammerState.UseReady2 || currentState == HammerState.UseReady3)
                SetState(gaugeLayer >= 1 ? HammerState.Ready : HammerState.Inactive);
        }

        public void ResetGauge()
        {
            gaugeLayer = 0;
            gaugeInLayer = 0;
            currentState = HammerState.Inactive;
            if (hammerButton != null) hammerButton.interactable = false;
            RefreshUI();
        }

        // ============================================================
        // 게이지 충전: 레이어 시스템
        // ============================================================

        public void AddGauge(int amount)
        {
            if (currentState != HammerState.Inactive && currentState != HammerState.Ready) return;

            int maxLayer = GetMaxLayer();

            // 이미 최대 레이어이면 더 이상 증가 안 함
            if (gaugeLayer >= maxLayer) return;

            gaugeInLayer += amount;

            // 레이어 승격 처리
            while (gaugeInLayer >= LAYER_SIZE && gaugeLayer < maxLayer)
            {
                gaugeInLayer -= LAYER_SIZE;
                gaugeLayer++;
            }

            // 최대 레이어 도달 시 잔여 게이지 초기화
            if (gaugeLayer >= maxLayer)
            {
                gaugeInLayer = 0;
            }

            if (gaugeLayer >= 1 && currentState == HammerState.Inactive)
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
            Color readyColor = GetLayerColor(gaugeLayer - 1);
            for (int i = 0; i < 3; i++)
            {
                buttonImage.color = COLOR_FLASH;
                yield return new WaitForSeconds(0.1f);
                buttonImage.color = readyColor;
                yield return new WaitForSeconds(0.1f);
            }
        }

        // ============================================================
        // UI
        // ============================================================

        private Color GetLayerColor(int layer)
        {
            switch (layer)
            {
                case 0: return COLOR_LAYER_0;
                case 1: return COLOR_LAYER_1;
                case 2: return COLOR_LAYER_2;
                case 3: return COLOR_LAYER_3;
                default: return COLOR_LAYER_3;
            }
        }

        private void RefreshUI()
        {
            if (buttonImage == null) return;

            switch (currentState)
            {
                case HammerState.Inactive:
                    // fillAmount = 현재 레이어 내 진행률
                    buttonImage.fillAmount = gaugeInLayer / (float)LAYER_SIZE;
                    buttonImage.color = GetLayerColor(gaugeLayer);
                    if (hammerButton != null) hammerButton.interactable = JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode();
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_OFF;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one;
                    break;

                case HammerState.Ready:
                    // 현재 진행 중인 레이어의 fillAmount
                    int maxLayer = GetMaxLayer();
                    if (gaugeLayer >= maxLayer)
                        buttonImage.fillAmount = 1f;
                    else
                        buttonImage.fillAmount = gaugeInLayer / (float)LAYER_SIZE;
                    buttonImage.color = GetLayerColor(Mathf.Max(0, gaugeLayer - 1));
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one;
                    break;

                case HammerState.UseReady0:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_USE_READY0;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_ACTIVE;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one;
                    break;

                case HammerState.UseReady1:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_USE_READY1;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_BRIGHT;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one * 1.15f;
                    break;

                case HammerState.UseReady2:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_USE_READY2;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_BRIGHT;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one * 1.25f;
                    break;

                case HammerState.UseReady3:
                    buttonImage.fillAmount = 1f;
                    buttonImage.color = COLOR_USE_READY3;
                    if (hammerButton != null) hammerButton.interactable = true;
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_BRIGHT;
                    if (hammerButton != null) hammerButton.transform.localScale = Vector3.one * 1.35f;
                    break;
            }

            // 레이어 텍스트 업데이트
            UpdateLayerText();
        }

        private void UpdateLayerText()
        {
            if (layerText == null) return;

            if (gaugeLayer <= 0)
            {
                layerText.text = "";
            }
            else if (currentState == HammerState.Inactive || currentState == HammerState.Ready)
            {
                // 비활성화/Ready: 숫자만 표시
                layerText.text = gaugeLayer.ToString();
            }
            else
            {
                // UseReady 상태: gaugeLayer/소모량 형식
                int cost = 1;
                switch (currentState)
                {
                    case HammerState.UseReady0: cost = 1; break;
                    case HammerState.UseReady1: cost = 2; break;
                    case HammerState.UseReady2: cost = 3; break;
                    case HammerState.UseReady3: cost = 4; break;
                }
                layerText.text = $"{gaugeLayer}/{cost}";
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

        public int GaugeLayer => gaugeLayer;
        public int GaugeInLayer => gaugeInLayer;
        public int TotalGauge => gaugeLayer * LAYER_SIZE + gaugeInLayer;
        public HammerState CurrentState => currentState;

        // 하위 호환용 (기존 코드에서 CurrentGauge 참조하는 곳)
        public int CurrentGauge => TotalGauge;

        /// <summary>에디터 테스트용: gaugeLayer 1 증가. 풀이면 0으로 초기화</summary>
        public void AddGaugeEditor()
        {
            int maxLayer = GetMaxLayer();
            bool isFull = gaugeLayer >= maxLayer && gaugeInLayer == 0;
            if (isFull)
            {
                Debug.Log($"[EditorGauge] 망치 게이지 풀 → 초기화 (gaugeLayer: {gaugeLayer} → 0)");
                gaugeLayer = 0;
                gaugeInLayer = 0;
                SetState(HammerState.Inactive);
            }
            else
            {
                Debug.Log($"[EditorGauge] 망치 게이지 증가 — gaugeLayer: {gaugeLayer} → {Mathf.Min(gaugeLayer + 1, maxLayer)}");
                gaugeLayer = Mathf.Min(gaugeLayer + 1, maxLayer);
                gaugeInLayer = 0;
                if (gaugeLayer >= 1 && currentState == HammerState.Inactive)
                    SetState(HammerState.Ready);
                else
                    RefreshUI();
            }
        }
    }
}

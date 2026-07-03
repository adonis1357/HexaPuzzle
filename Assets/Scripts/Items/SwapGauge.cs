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

        private static readonly Color COLOR_OUTLINE_ACTIVE = new Color(1f, 0.9f, 0f, 1f);
        private static readonly Color COLOR_OUTLINE_OFF = new Color(0f, 0f, 0f, 0f);

        // ★ 레이어(단계)별 요구 게이지 — 1~4단계 모두 10 (각 단계 동일). 인덱스 0=1단계.
        private static readonly int[] LAYER_THRESHOLDS = { 10, 10, 10, 10 };
        private int LayerThreshold(int layer) => LAYER_THRESHOLDS[Mathf.Clamp(layer, 0, LAYER_THRESHOLDS.Length - 1)];

        // 레이어 시스템 (HammerGauge와 동일)
        private int gaugeLayer = 0;       // 0~4 (완성된 레이어 수)
        private int gaugeInLayer = 0;     // 현재 레이어 내 진행 (그 단계 임계값 미만)

        private GaugeState currentState = GaugeState.Inactive;

        private Button itemButton;
        private SwapItem swapItem;
        private Image buttonImage;
        private Outline buttonOutline;
        private Text layerText;
        private bool initialized = false;

        /// <summary>현재 해금된 스킬에 따른 최대 레이어 수</summary>
        public int GetCurrentMaxLayer()
        {
            return 4; // ★ 기본 능력치로 4단계까지 충전 (리워드 없이)
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
            currentState = GaugeState.Inactive;
            ApplyInactiveColorImmediate();
            StartCoroutine(AutoInitCoroutine());
        }

        private void ApplyInactiveColorImmediate()
        {
            Button btn = null;
            var uiMgr = FindObjectOfType<UIManager>();
            if (uiMgr != null && uiMgr.ItemButtons != null)
                foreach (var ib in uiMgr.ItemButtons)
                    if (ib != null && ib.CurrentItemType == ItemType.Bomb && ib.ButtonComponent != null)
                    { btn = ib.ButtonComponent; break; }
            if (btn == null)
            {
                var si = FindObjectOfType<SwapItem>();
                if (si != null) btn = si.GetComponentInChildren<Button>();
            }
            if (btn != null)
            {
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = COLOR_READY;
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

                CreateLayerText();

                ForceAlphaOne(itemButton.gameObject);
                SetState(GaugeState.Inactive);
            }
        }

        private void CreateLayerText()
        {
            if (itemButton == null) return;
            var existing = itemButton.transform.Find("LayerText");
            if (existing != null) { layerText = existing.GetComponent<Text>(); return; }

            GameObject textObj = new GameObject("LayerText");
            textObj.transform.SetParent(itemButton.transform, false);

            layerText = textObj.AddComponent<Text>();
            layerText.text = "";
            layerText.fontSize = 24;
            layerText.fontStyle = FontStyle.Bold;
            layerText.alignment = TextAnchor.UpperRight;
            layerText.color = Color.white;
            layerText.raycastTarget = false;
            layerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.anchoredPosition = new Vector2(-25f, -12f);
        }

        private void UpdateLayerText()
        {
            if (layerText == null) return;
            if (gaugeLayer <= 0)
                layerText.text = "";
            else
                layerText.text = gaugeLayer.ToString();
        }

        private void SetState(GaugeState newState)
        {
            currentState = newState;
            RefreshUI();
            // ★ 보류 충전 반영 (감사 M12) — UseReady 중 도착한 영혼 충전을 상태 복귀 시 합산
            if (pendingGauge > 0 &&
                (newState == GaugeState.Inactive || newState == GaugeState.Ready))
            {
                int p = pendingGauge;
                pendingGauge = 0;
                AddGauge(p);
            }
        }

        // UseReady 등 충전 불가 상태에 도착한 충전 보류분 (상태 복귀 시 합산 — 감사 M12)
        private int pendingGauge = 0;

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
                SetState(gaugeLayer >= 1 ? GaugeState.Ready : GaugeState.Inactive);
            }
        }

        public void ActivateUseReady()
        {
            // ★ 토글 — 차지바 버튼 탭으로 활성/취소 모두 가능(기존 활성화 전용이라 버튼으로 취소가 안 되던 버그 수정).
            if (currentState == GaugeState.Ready)
            {
                SetState(GaugeState.UseReady);
                if (swapItem == null) swapItem = FindObjectOfType<SwapItem>();
                if (swapItem != null) swapItem.Activate();
            }
            else if (currentState == GaugeState.UseReady)
            {
                if (swapItem != null) swapItem.Deactivate();
                SetState(gaugeLayer >= 1 ? GaugeState.Ready : GaugeState.Inactive);
            }
        }

        public void OnItemUsed()
        {
            gaugeLayer = Mathf.Max(0, gaugeLayer - 1);
            gaugeInLayer = 0;
            SetState(gaugeLayer >= 1 ? GaugeState.Ready : GaugeState.Inactive);
        }

        /// <summary>게이지 소모: amount 레이어 직접 차감 (거리 1=1, 2=2, 3=3, 4=4)</summary>
        public void ConsumeGauge(int amount)
        {
            gaugeLayer = Mathf.Max(0, gaugeLayer - amount);
            gaugeInLayer = 0;
            SetState(gaugeLayer >= 1 ? GaugeState.Ready : GaugeState.Inactive);
        }

        public void OnItemCancelled()
        {
            if (currentState == GaugeState.UseReady)
                SetState(gaugeLayer >= 1 ? GaugeState.Ready : GaugeState.Inactive);
        }

        public void ResetGauge()
        {
            gaugeLayer = 0;
            gaugeInLayer = 0;
            currentState = GaugeState.Inactive;
            if (itemButton != null) itemButton.interactable = false;
            RefreshUI();
        }

        // ============================================================
        // 게이지 충전: 레이어 시스템 (HammerGauge와 동일)
        // ============================================================

        public void AddGauge(int amount)
        {
            // ★ 사용 중(UseReady 등) 도착한 충전은 버리지 않고 보류 → 상태 복귀 시 합산 (감사 M12)
            if (currentState != GaugeState.Inactive && currentState != GaugeState.Ready)
            {
                pendingGauge += amount;
                return;
            }

            int maxLayer = GetCurrentMaxLayer();

            // 이미 최대 레이어이면 더 이상 증가 안 함
            if (gaugeLayer >= maxLayer) return;

            gaugeInLayer += amount;

            // 레이어 승격 처리
            while (gaugeLayer < maxLayer && gaugeInLayer >= LayerThreshold(gaugeLayer))
            {
                gaugeInLayer -= LayerThreshold(gaugeLayer);
                gaugeLayer++;
                if (AudioManager.Instance != null) AudioManager.Instance.PlayGaugeFullSound(); // ★ 효과음: 게이지 레이어 완성
            }

            // 최대 레이어 도달 시 잔여 게이지 초기화
            if (gaugeLayer >= maxLayer)
            {
                gaugeInLayer = 0;
            }

            if (gaugeLayer >= 1 && currentState == GaugeState.Inactive)
            {
                SetState(GaugeState.Ready);
                StartCoroutine(FlashEffect());
            }
            else
            {
                RefreshUI();
            }
        }

        public void OnTurnEnd() { AddGauge(1); } // 단계 임계값 10/8/6/4에 맞춰 턴당 패시브 충전 +5→+1 (매칭 위주로)

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

            switch (currentState)
            {
                case GaugeState.Inactive:
                    buttonImage.fillAmount = gaugeInLayer / (float)LayerThreshold(gaugeLayer);
                    buttonImage.color = COLOR_READY;
                    if (itemButton != null) itemButton.interactable = JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode();
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_OFF;
                    break;
                case GaugeState.Ready:
                    int maxLayer = GetCurrentMaxLayer();
                    if (gaugeLayer >= maxLayer)
                        buttonImage.fillAmount = 1f;
                    else
                        buttonImage.fillAmount = gaugeInLayer / (float)LayerThreshold(gaugeLayer);
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
            UpdateLayerText();
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

        /// <summary>외부에서 UI 즉시 갱신 호출용</summary>
        public void ForceRefreshUI() { RefreshUI(); }

        /// <summary>UseReady 상태에서의 사용 레벨 (gaugeLayer - 1, 0~3). 스킬 해금 기준 제한 적용.</summary>
        public int GetUseReadyLevel()
        {
            // ★ 게이지는 4단계까지 차지만, 사용 레벨(능력)은 리워드(SwapLevel)만큼만 — 리워드 있어야 상위 레벨 사용.
            int level = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetSwapLevel() : 0;
            int rawLevel = Mathf.Max(0, gaugeLayer - 1);
            return Mathf.Min(rawLevel, level);
        }

        public int GaugeLayer => gaugeLayer;
        public int GaugeInLayer => gaugeInLayer;
        /// <summary>현재 채우는 단계의 진행 비율 (gaugeInLayer / 그 단계 임계값) — 차지바 fill용.</summary>
        public float CurrentLayerFillRatio => Mathf.Clamp01(gaugeInLayer / (float)LayerThreshold(gaugeLayer));
        public int TotalGauge { get { int t = gaugeInLayer; for (int i = 0; i < gaugeLayer; i++) t += LayerThreshold(i); return t; } }
        public int CurrentGauge => TotalGauge;
        public GaugeState CurrentState => currentState;

        /// <summary>에디터 테스트용: gaugeLayer 1 증가. 풀이면 0으로 초기화</summary>
        public void AddGaugeEditor()
        {
            int maxLayer = GetCurrentMaxLayer();
            bool isFull = gaugeLayer >= maxLayer && gaugeInLayer == 0;
            if (isFull)
            {
                Debug.Log($"[EditorGauge] 스왑 게이지 풀 → 초기화 (gaugeLayer: {gaugeLayer} → 0)");
                gaugeLayer = 0;
                gaugeInLayer = 0;
                SetState(GaugeState.Inactive);
            }
            else
            {
                Debug.Log($"[EditorGauge] 스왑 게이지 증가 — gaugeLayer: {gaugeLayer} → {Mathf.Min(gaugeLayer + 1, maxLayer)}");
                gaugeLayer = Mathf.Min(gaugeLayer + 1, maxLayer);
                gaugeInLayer = 0;
                if (gaugeLayer >= 1 && currentState == GaugeState.Inactive)
                    SetState(GaugeState.Ready);
                else
                    RefreshUI();
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    public class LineGauge : MonoBehaviour
    {
        public static LineGauge Instance { get; private set; }

        public enum GaugeState { Inactive, Ready, UseReady }

        private static readonly Color COLOR_INACTIVE  = new Color(0.2f, 0f, 0.3f, 1f);
        private static readonly Color COLOR_READY     = new Color(0.7f, 0.2f, 1f, 1f);
        private static readonly Color COLOR_USE_READY = new Color(0.7f, 0.2f, 1f, 1f);
        private static readonly Color COLOR_FLASH     = Color.white;

        private static readonly Color COLOR_OUTLINE_ACTIVE = new Color(1f, 0.9f, 0f, 1f);
        private static readonly Color COLOR_OUTLINE_OFF = new Color(0f, 0f, 0f, 0f);

        private const int LAYER_SIZE = 50;

        // 레이어 시스템 (HammerGauge와 동일)
        private int gaugeLayer = 0;       // 0~4 (완성된 레이어 수)
        private int gaugeInLayer = 0;     // 0~49 (현재 레이어 내 진행률)

        private GaugeState currentState = GaugeState.Inactive;

        private Button itemButton;
        private LineDrawItem lineItem;
        private Image buttonImage;
        private Outline buttonOutline;
        private Text layerText;
        private bool initialized = false;

        /// <summary>현재 해금된 스킬에 따른 최대 레이어 수</summary>
        public int GetCurrentMaxLayer()
        {
            int level = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetLineLevel() : 0;
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
                    if (ib != null && ib.CurrentItemType == ItemType.SixWayLaser && ib.ButtonComponent != null)
                    { btn = ib.ButtonComponent; break; }
            if (btn == null)
            {
                var li = FindObjectOfType<LineDrawItem>();
                if (li != null) btn = li.GetComponentInChildren<Button>();
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
                    Debug.Log($"[LineGauge] 초기화 성공: {itemButton.gameObject.name} (라운드 {round})");
                    yield break;
                }
                float wait = round <= 5 ? 0.5f : (round <= 15 ? 2f : 5f);
                yield return new WaitForSeconds(wait);
            }
        }

        private void TryFindButton()
        {
            if (initialized) return;

            // 1순위: UIManager.ItemButtons에서 ItemType.SixWayLaser
            if (itemButton == null)
            {
                var uiMgr = FindObjectOfType<UIManager>();
                if (uiMgr != null && uiMgr.ItemButtons != null)
                {
                    foreach (var ib in uiMgr.ItemButtons)
                    {
                        if (ib != null && ib.CurrentItemType == ItemType.SixWayLaser && ib.ButtonComponent != null)
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
                    if (n.Contains("line") || n.Contains("laser") || n.Contains("라인"))
                    {
                        itemButton = btn;
                        break;
                    }
                }
            }

            // 3순위: LineDrawItem에서 직접
            if (itemButton == null)
            {
                if (lineItem == null) lineItem = FindObjectOfType<LineDrawItem>();
                if (lineItem != null)
                {
                    var btn = lineItem.GetComponentInChildren<Button>();
                    if (btn != null) itemButton = btn;
                }
            }

            if (lineItem == null) lineItem = FindObjectOfType<LineDrawItem>();

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
            rt.anchoredPosition = new Vector2(-21f, -12f);
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
        }

        private void OnButtonClicked()
        {
            if (JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode()) { AddGaugeEditor(); return; }

            if (currentState == GaugeState.Ready)
            {
                SetState(GaugeState.UseReady);
                if (lineItem == null) lineItem = FindObjectOfType<LineDrawItem>();
                if (lineItem != null) lineItem.Activate();
            }
            else if (currentState == GaugeState.UseReady)
            {
                if (lineItem != null) lineItem.Deactivate();
                SetState(GaugeState.Ready);
            }
        }

        public void OnItemUsed()
        {
            gaugeLayer = Mathf.Max(0, gaugeLayer - 1);
            gaugeInLayer = 0;
            SetState(gaugeLayer >= 1 ? GaugeState.Ready : GaugeState.Inactive);
        }

        /// <summary>게이지 소모: amount 레이어 직접 차감</summary>
        public void ConsumeGauge(int amount)
        {
            gaugeLayer = Mathf.Max(0, gaugeLayer - amount);
            gaugeInLayer = 0;
            SetState(gaugeLayer >= 1 ? GaugeState.Ready : GaugeState.Inactive);
        }

        public void OnItemCancelled()
        {
            if (currentState == GaugeState.UseReady)
                SetState(GaugeState.Ready);
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
            if (currentState != GaugeState.Inactive && currentState != GaugeState.Ready) return;

            int maxLayer = GetCurrentMaxLayer();

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

            switch (currentState)
            {
                case GaugeState.Inactive:
                    buttonImage.fillAmount = gaugeInLayer / (float)LAYER_SIZE;
                    buttonImage.color = COLOR_READY;
                    if (itemButton != null) itemButton.interactable = JewelsHexaPuzzle.Core.EditorTestSystem.IsGaugeAddMode();
                    if (buttonOutline != null) buttonOutline.effectColor = COLOR_OUTLINE_OFF;
                    break;
                case GaugeState.Ready:
                    int maxLayer = GetCurrentMaxLayer();
                    if (gaugeLayer >= maxLayer)
                        buttonImage.fillAmount = 1f;
                    else
                        buttonImage.fillAmount = gaugeInLayer / (float)LAYER_SIZE;
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

        /// <summary>UseReady 상태에서의 사용 레벨 (gaugeLayer - 1, 0~3). 스킬 해금 기준 제한 적용.</summary>
        public int GetUseReadyLevel()
        {
            int level = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetLineLevel() : 0;
            int maxLevel = level;
            int rawLevel = Mathf.Max(0, gaugeLayer - 1);
            return Mathf.Min(rawLevel, maxLevel);
        }

        public int GaugeLayer => gaugeLayer;
        public int GaugeInLayer => gaugeInLayer;
        public int TotalGauge => gaugeLayer * LAYER_SIZE + gaugeInLayer;
        public int CurrentGauge => TotalGauge;
        public GaugeState CurrentState => currentState;

        /// <summary>에디터 테스트용: gaugeLayer 1 증가. 풀이면 0으로 초기화</summary>
        public void AddGaugeEditor()
        {
            int maxLayer = GetCurrentMaxLayer();
            bool isFull = gaugeLayer >= maxLayer && gaugeInLayer == 0;
            if (isFull)
            {
                Debug.Log($"[EditorGauge] 라인 게이지 풀 → 초기화 (gaugeLayer: {gaugeLayer} → 0)");
                gaugeLayer = 0;
                gaugeInLayer = 0;
                SetState(GaugeState.Inactive);
            }
            else
            {
                Debug.Log($"[EditorGauge] 라인 게이지 증가 — gaugeLayer: {gaugeLayer} → {Mathf.Min(gaugeLayer + 1, maxLayer)}");
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

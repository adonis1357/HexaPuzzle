using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// MP(마나포인트) 시스템 관리
    /// - 기본 MP 30 (구독 시 100으로 상승) — SubscriptionManager.IsSubscribed 기반
    /// - 게임 시작/나갈 때 초기화
    /// - 특수 블록 클릭 발동 시 MP 소모
    /// - 아이템 사용 시 MP 소모
    /// - MP 부족 시 사용 불가
    /// </summary>
    public class MPManager : MonoBehaviour
    {
        public static MPManager Instance { get; private set; }

        // ============================================================
        // 최대 MP 상수 (구독 여부에 따라 결정)
        // ============================================================
        public const int DEFAULT_MAX_MP = 30;       // 일반 플레이어
        public const int SUBSCRIBED_MAX_MP = 100;   // 프리미엄 구독자

        // ============================================================
        // MP 데이터
        // ============================================================
        private JewelsHexaPuzzle.Utils.ObscuredInt currentMP = DEFAULT_MAX_MP; // ★ 보안: 메모리 치트 내성
        private JewelsHexaPuzzle.Utils.ObscuredInt maxMP = DEFAULT_MAX_MP;

        /// <summary>현재 MP</summary>
        public int CurrentMP => currentMP;

        /// <summary>최대 MP</summary>
        public int MaxMP => maxMP;

        /// <summary>현재 채움 비율 (0~1)</summary>
        public float FillRatio => maxMP > 0 ? (float)currentMP / maxMP : 0f;

        // ============================================================
        // 특수 블록 MP 비용
        // ============================================================
        private static readonly Dictionary<SpecialBlockType, int> specialBlockCosts = new Dictionary<SpecialBlockType, int>
        {
            { SpecialBlockType.Drill, 5 },
            { SpecialBlockType.Bomb, 6 },
            { SpecialBlockType.Drone, 7 },
            { SpecialBlockType.Rainbow, 10 },
            { SpecialBlockType.XBlock, 10 }
        };

        // ============================================================
        // 아이템 MP 비용
        //   ★ 특수블록 직접 사용 비용(Drill 5 / Bomb 6 / Drone 7 / Rainbow·XBlock 10)보다
        //     다소 높게 책정 — 아이템은 매칭 없이 즉시 효과를 발휘하므로 가치가 더 높음.
        // ============================================================
        private static readonly Dictionary<ItemType, int> itemCosts = new Dictionary<ItemType, int>
        {
            { ItemType.Hammer, 10 },           // 망치 (2026-07-02 사용자 조정: 12→10)
            { ItemType.Bomb, 9 },              // 스왑 (ItemType.Bomb = 스왑 아이템, 13→9)
            { ItemType.SSD, 8 },               // 라인 (ItemType.SSD = 라인 아이템, 14→8)
            { ItemType.ReverseRotation, 5 }    // 역회전 (15→5)
        };

        // ============================================================
        // 이벤트
        // ============================================================

        /// <summary>MP 변경 시 발생 (currentMP, maxMP)</summary>
        public event System.Action<int, int> OnMPChanged;

        // ============================================================
        // 초기화
        // ============================================================

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // ★ 구독 상태에 맞춰 최대 MP 결정 (기본 30, 구독 시 100)
            ApplySubscriptionMaxMP();
        }

        private void Start()
        {
            // 구독 매니저 이벤트 구독 (이벤트 발생 순서상 Start에서 안전)
            if (SubscriptionManager.Instance != null)
                SubscriptionManager.Instance.OnSubscriptionChanged += ApplySubscriptionMaxMP;
        }

        private void OnDestroy()
        {
            if (SubscriptionManager.Instance != null)
                SubscriptionManager.Instance.OnSubscriptionChanged -= ApplySubscriptionMaxMP;
        }

        /// <summary>
        /// 구독 상태 → maxMP 결정. 구매/만료 시 자동 호출되어 게이지 갱신 이벤트 발생.
        /// </summary>
        private void ApplySubscriptionMaxMP()
        {
            bool isSubscribed = SubscriptionManager.Instance != null
                                && SubscriptionManager.Instance.IsSubscribed;
            int newMax = isSubscribed ? SUBSCRIBED_MAX_MP : DEFAULT_MAX_MP;
            if (newMax == maxMP) return;

            maxMP = newMax;
            currentMP = Mathf.Min(currentMP, maxMP); // 최대값 초과 시 클램프
            OnMPChanged?.Invoke(currentMP, maxMP);
            Debug.Log($"[MPManager] 구독 상태 갱신: maxMP = {maxMP} (구독={isSubscribed})");
        }

        // ============================================================
        // 공개 API
        // ============================================================

        /// <summary>
        /// MP 초기화 (게임 시작 또는 나갈 때 호출)
        /// </summary>
        public void ResetMP()
        {
            currentMP = maxMP;
            OnMPChanged?.Invoke(currentMP, maxMP);
            Debug.Log($"[MPManager] MP 초기화: {currentMP}/{maxMP}");
        }

        /// <summary>
        /// 이벤트 없이 MP 초기화 (게이지 UI가 비활성 상태일 때 사용)
        /// </summary>
        public void ResetMPSilent()
        {
            currentMP = maxMP;
            Debug.Log($"[MPManager] MP 조용히 초기화: {currentMP}/{maxMP}");
        }

        /// <summary>
        /// MP가 충분한지 확인
        /// </summary>
        public bool CanAfford(int cost)
        {
            if (currentMP < cost) lastRequiredCost = cost; // 구매 팝업이 '필요한 만큼'을 알 수 있게 기록
            return currentMP >= cost;
        }

        /// <summary>
        /// MP 소모 시도. 성공 시 true 반환, 부족 시 false.
        /// worldPos가 지정되면 해당 위치에 "-N" 파란 텍스트 팝업 표시.
        /// </summary>
        public bool TryConsumeMP(int cost, Vector3? worldPos = null)
        {
            if (currentMP < cost)
            {
                lastRequiredCost = cost; // '필요한 만큼' 충전 계산용
                Debug.Log($"[MPManager] MP 부족: 현재 {currentMP}, 필요 {cost}");
                return false;
            }

            currentMP -= cost;
            OnMPChanged?.Invoke(currentMP, maxMP);
            Debug.Log($"[MPManager] MP 소모 {cost}: {currentMP + cost} → {currentMP}/{maxMP}");

            // ★ MP 소모 팝업: 사용 위치에 파란색 "-N" 텍스트 표시
            if (worldPos.HasValue)
                SpawnMPPopup(cost, worldPos.Value);
            else
                SpawnMPPopupAtGauge(cost);

            return true;
        }

        // ============================================================
        // MP 소모 팝업
        // ============================================================

        /// <summary>
        /// 월드 좌표(블록 위치)에 "-N" 파란 팝업 생성 (데미지 텍스트와 동일한 스타일).
        /// 데미지 팝업처럼 블록 바로 위(약 25px)에서 시작해 위로 떠오르며 페이드아웃.
        /// 점수 팝업(흰색/금색 +N)과는 색상으로 시각적 분리됨.
        /// </summary>
        private void SpawnMPPopup(int cost, Vector3 worldPos)
        {
            Canvas canvas = FindCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[MPManager] Canvas 미발견 — 팝업 표시 실패");
                return;
            }

            GameObject popup = CreateMPPopupObject(cost, canvas);
            RectTransform rt = popup.GetComponent<RectTransform>();

            // ★ 캔버스 카메라 일관성 — ScorePopupManager와 동일한 패턴
            //   SS-Overlay 캔버스: canvas.worldCamera == null → WorldToScreenPoint/LocalPoint 모두 null 카메라 처리
            //   SS-Camera/World: canvas.worldCamera 사용
            Camera uiCamera = canvas.worldCamera;
            RectTransform canvasRt = canvas.transform as RectTransform;

            // 월드→스크린→캔버스 로컬 좌표 변환
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPos);
            Vector2 localPoint;
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRt, screenPoint, uiCamera, out localPoint);

            if (!ok)
            {
                Debug.LogWarning($"[MPManager] 좌표 변환 실패 — worldPos={worldPos}, screenPoint={screenPoint}");
                Destroy(popup);
                return;
            }

            // ★ 블록 위 40px — 팝업 2배 확대(60px 높이) 반영해 블록과 겹치지 않도록 상향
            rt.anchoredPosition = localPoint + new Vector2(0f, 40f);

            // 최상위 z-order로 가져와 다른 UI에 가려지지 않도록
            popup.transform.SetAsLastSibling();

            Debug.Log($"[MPManager] MP 팝업 -{cost} 표시: world={worldPos} → local={localPoint}");

            StartCoroutine(AnimateMPPopup(rt));
        }

        /// <summary>
        /// MP 부족 피드백 — 빨간 "마나 부족" 팝업을 사용 위치에 표시.
        /// 데미지 팝업과 동일한 스타일(작은 박스, Bold, 위로 떠오르며 페이드).
        /// 능력 실행이 차단됐음을 사용자에게 시각적으로 알림.
        /// </summary>
        public void SpawnInsufficientPopup(Vector3 worldPos)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMPInsufficientSound(); // ★ 효과음: MP 부족 거부
            Canvas canvas = FindCanvas();
            if (canvas == null) return;

            GameObject popup = new GameObject("MPInsufficientPopup");
            popup.transform.SetParent(canvas.transform, false);
            RectTransform rt = popup.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240f, 60f); // 2배 확대 (120x30 → 240x60)

            Text text = popup.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 44; // 22 → 44 (2배)
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1.0f, 0.30f, 0.30f, 1.0f); // 빨간색 (데미지 톤)
            text.raycastTarget = false;
            text.text = "마나 부족";

            // 빨간색과 어울리는 진한 적갈색 아웃라인 (큰 폰트에 맞춰 두께 ↑)
            Outline outline = popup.AddComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.0f, 0.0f, 0.95f);
            outline.effectDistance = new Vector2(2.5f, -2.5f); // 1.5 → 2.5

            // 좌표 변환 (MP 소모 팝업과 동일 패턴)
            Camera uiCamera = canvas.worldCamera;
            RectTransform canvasRt = canvas.transform as RectTransform;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPos);
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, uiCamera, out localPoint))
            {
                Destroy(popup);
                return;
            }

            // 블록 위 40px — 팝업 2배 확대(60px 높이) 반영해 블록과 겹치지 않도록
            rt.anchoredPosition = localPoint + new Vector2(0f, 40f);
            popup.transform.SetAsLastSibling();

            Debug.Log($"[MPManager] 마나 부족 팝업 표시: world={worldPos} → local={localPoint}");

            StartCoroutine(AnimateMPPopup(rt));

            // ★ 마나 부족 → 마나 충전 팝업(필요한 만큼, 1마나=10골드) 표시
            ShowManaPurchasePopup();
        }

        /// <summary>
        /// MP 게이지 위치에 "-N" 파란 팝업 생성 (좌표 없을 때 폴백)
        /// </summary>
        private void SpawnMPPopupAtGauge(int cost)
        {
            Canvas canvas = FindCanvas();
            if (canvas == null) return;

            GameObject popup = CreateMPPopupObject(cost, canvas);
            RectTransform rt = popup.GetComponent<RectTransform>();

            // 게이지 위치 (화면 좌측 상단 부근)
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(60, -80);

            StartCoroutine(AnimateMPPopup(rt));
        }

        /// <summary>
        /// MP 팝업 GameObject 생성 — 데미지 팝업의 2배 사이즈로 강조.
        /// 차이점: 색상은 파란색, 텍스트 앞에 마이너스 부호.
        /// </summary>
        private GameObject CreateMPPopupObject(int cost, Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject popup = new GameObject("MPPopup");
            popup.transform.SetParent(canvas.transform, false);
            RectTransform rt = popup.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 60f); // 데미지 팝업 2배 (60x30 → 120x60)

            Text text = popup.AddComponent<Text>();
            text.font = font;
            text.fontSize = 44; // 데미지 22의 2배
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.30f, 0.65f, 1.0f, 1.0f); // 밝은 하늘색-파랑 (가독성 ↑)
            text.raycastTarget = false;
            text.text = $"-{cost}";

            // 파란색과 어울리는 진한 네이비 아웃라인 (큰 폰트에 맞춰 두께 ↑)
            Outline outline = popup.AddComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.05f, 0.25f, 0.95f);
            outline.effectDistance = new Vector2(2.5f, -2.5f); // 1.5 → 2.5 (큰 폰트 가독성)

            return popup;
        }

        /// <summary>
        /// 팝업 애니메이션: 위로 45px 올라가면서 페이드아웃 (0.9초)
        /// 사용자 요청: 유지시간 50% 증가 (0.6 → 0.9), 상승 거리도 비례 증가 (30 → 45)
        /// </summary>
        private IEnumerator AnimateMPPopup(RectTransform rt)
        {
            if (rt == null) yield break;

            Vector2 startPos = rt.anchoredPosition;
            Text text = rt.GetComponent<Text>();
            Color startColor = text != null ? text.color : Color.blue;
            Outline outline = rt.GetComponent<Outline>();
            Color outlineStartColor = outline != null ? outline.effectColor : Color.black;

            float duration = 0.9f;     // 0.6 × 1.5 = 0.9 (유지시간 50% 증가)
            float riseDistance = 45f;  // 30 × 1.5 = 45 (상승 속도 동일 유지)
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 위로 이동 (EaseOutQuad)
                float eased = 1f - (1f - t) * (1f - t);
                rt.anchoredPosition = startPos + new Vector2(0, riseDistance * eased);

                // 페이드아웃 (후반 50%에서 시작)
                float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
                if (text != null)
                    text.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                if (outline != null)
                    outline.effectColor = new Color(outlineStartColor.r, outlineStartColor.g, outlineStartColor.b, alpha);

                yield return null;
            }

            if (rt != null)
                Destroy(rt.gameObject);
        }

        private Canvas FindCanvas()
        {
            // ★ HexGrid 부모 캔버스를 우선 사용 (블록과 동일 캔버스 → 좌표 변환 일관성)
            //   다른 UI 코드(GameManager)의 패턴과 동일: hexGrid.GetComponentInParent<Canvas>()
            var hexGrid = Object.FindObjectOfType<JewelsHexaPuzzle.Core.HexGrid>();
            if (hexGrid != null)
            {
                Canvas c = hexGrid.GetComponentInParent<Canvas>();
                if (c != null) return c;
            }
            // 폴백: 씬에서 첫 번째 Canvas
            return Object.FindObjectOfType<Canvas>();
        }

        /// <summary>
        /// MP 회복 (미래 확장용)
        /// </summary>
        public void AddMP(int amount)
        {
            if (amount <= 0) return;
            currentMP = Mathf.Min(currentMP + amount, maxMP);
            OnMPChanged?.Invoke(currentMP, maxMP);
            Debug.Log($"[MPManager] MP 회복 +{amount}: {currentMP}/{maxMP}");
        }

        /// <summary>에디터용: MP를 특정 값으로 직접 설정</summary>
        public void SetMP(int value)
        {
            currentMP = Mathf.Clamp(value, 0, maxMP);
            OnMPChanged?.Invoke(currentMP, maxMP);
        }

        // ============================================================
        // 특수 블록 비용 조회
        // ============================================================

        /// <summary>특수 블록의 MP 소모량 반환 (미등록 시 0)</summary>
        public int GetSpecialBlockCost(SpecialBlockType type)
        {
            return specialBlockCosts.ContainsKey(type) ? specialBlockCosts[type] : 0;
        }

        /// <summary>특수 블록 발동 가능 여부 (MP 충분 여부)</summary>
        public bool CanActivateSpecialBlock(SpecialBlockType type)
        {
            int cost = GetSpecialBlockCost(type);
            if (cost <= 0) return true; // 비용 없는 특수 블록은 항상 가능
            return currentMP >= cost;
        }

        // ============================================================
        // 아이템 비용 조회
        // ============================================================

        /// <summary>아이템의 MP 소모량 반환 (미등록 시 0)</summary>
        public int GetItemCost(ItemType type)
        {
            return itemCosts.ContainsKey(type) ? itemCosts[type] : 0;
        }

        /// <summary>아이템 사용 가능 여부 (MP 충분 여부)</summary>
        public bool CanUseItem(ItemType type)
        {
            int cost = GetItemCost(type);
            if (cost <= 0) return true; // 비용 없는 아이템은 항상 가능
            return currentMP >= cost;
        }

        // ============================================================
        // 마나 구매 팝업 (골드 100 → 마나 10)
        // ============================================================
        private const int GOLD_PER_MANA = 10;          // 마나 1당 골드 (1마나 = 10골드)
        private int lastRequiredCost = 0;              // 마지막 부족 행동의 요구 MP — 구매 팝업이 '필요한 만큼' 계산
        private int pendingPurchaseMana = 0;           // 현재 구매 팝업이 충전할 마나량
        private int pendingPurchaseCost = 0;           // 현재 구매 팝업의 골드 비용
        private GameObject manaPurchasePopupObj;        // 현재 떠 있는 팝업 (중복 방지)

        /// <summary>팝업이 떠 있는지 (InputSystem 등에서 입력 차단 판단용)</summary>
        public bool IsManaPurchasePopupOpen => manaPurchasePopupObj != null;

        /// <summary>
        /// 마나 부족 시 표시되는 충전 팝업 — 필요한 만큼만(부족 행동 요구 MP − 현재 MP) 1마나=10골드로 충전.
        /// 클로드 디자인 마나 포션 이미지 포함. 이미 떠 있으면 무시(중복 방지). 이미 가득이면 표시 안 함.
        /// </summary>
        public void ShowManaPurchasePopup()
        {
            if (manaPurchasePopupObj != null) return;          // 이미 떠 있음
            if (currentMP >= maxMP) return;                    // 이미 가득 → 구매 불필요

            Canvas canvas = FindCanvas();
            if (canvas == null) return;

            // 그리드 입력 차단 (팝업 동안 블록 회전 방지)
            var input = Object.FindObjectOfType<JewelsHexaPuzzle.Core.InputSystem>();
            if (input != null) input.SetEnabled(false);

            // ── 배경 오버레이 (반투명, 클릭 시 취소) ──
            manaPurchasePopupObj = new GameObject("ManaPurchasePopup");
            manaPurchasePopupObj.transform.SetParent(canvas.transform, false);
            manaPurchasePopupObj.transform.SetAsLastSibling();
            var popupRt = manaPurchasePopupObj.AddComponent<RectTransform>();
            popupRt.anchorMin = Vector2.zero; popupRt.anchorMax = Vector2.one;
            popupRt.offsetMin = Vector2.zero; popupRt.offsetMax = Vector2.zero;
            var overlay = manaPurchasePopupObj.AddComponent<Image>();
            overlay.color = JewelsHexaPuzzle.Utils.ClaudeTheme.PopupOverlay;
            overlay.raycastTarget = true;
            var bgBtn = manaPurchasePopupObj.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(CloseManaPurchasePopup);

            // ── 패널 ──
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(manaPurchasePopupObj.transform, false);
            var panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(560f, 480f);
            var panelImg = panel.AddComponent<Image>();
            JewelsHexaPuzzle.Utils.ClaudeTheme.ApplyPopupPanel(panelImg); // 다크글래스 통일
            panelImg.raycastTarget = true; // 패널 클릭이 배경(취소)으로 전달되지 않도록 흡수
            var panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.45f, 0.55f, 1f, 0.8f);
            panelOutline.effectDistance = new Vector2(3f, 3f);

            // ── 필요한 만큼만 충전: (마지막 부족 행동 요구 MP − 현재 MP), 최소 1, 가용 최대로 클램프 ──
            int deficit = lastRequiredCost - currentMP;
            int needed = deficit > 0 ? deficit : (maxMP - currentMP);
            needed = Mathf.Clamp(needed, 1, Mathf.Max(1, maxMP - currentMP));
            pendingPurchaseMana = needed;
            pendingPurchaseCost = needed * GOLD_PER_MANA; // 1마나 = 10골드

            // ── 마나 포션 이미지 (클로드 디자인, Resources/UI/mana_potion) ──
            GameObject potionObj = new GameObject("PotionIcon");
            potionObj.transform.SetParent(panel.transform, false);
            var potionRt = potionObj.AddComponent<RectTransform>();
            potionRt.anchorMin = potionRt.anchorMax = new Vector2(0.5f, 0.5f);
            potionRt.pivot = new Vector2(0.5f, 0.5f);
            potionRt.anchoredPosition = new Vector2(0f, 168f);
            potionRt.sizeDelta = new Vector2(124f, 124f);
            var potionImg = potionObj.AddComponent<Image>();
            potionImg.sprite = Resources.Load<Sprite>("UI/mana_potion");
            potionImg.preserveAspect = true;
            potionImg.raycastTarget = false;

            // ── 제목 ──
            CreatePopupText(panel, "마나 부족!", new Vector2(0f, 78f),
                            new Vector2(520f, 60f), 46, FontStyle.Bold,
                            new Color(1f, 0.45f, 0.45f));

            // ── 설명 (필요한 만큼·실제 비용) ──
            CreatePopupText(panel, $"마나 {pendingPurchaseMana} 을(를) {pendingPurchaseCost} 골드에\n충전하시겠습니까?",
                            new Vector2(0f, 6f), new Vector2(520f, 100f), 34, FontStyle.Normal, Color.white);

            // ── 보유 골드 ──
            int gold = GameManager.Instance != null ? GameManager.Instance.CurrentGold : 0;
            CreatePopupText(panel, $"보유 골드: {gold}", new Vector2(0f, -68f),
                            new Vector2(520f, 50f), 30, FontStyle.Bold, new Color(1f, 0.85f, 0.3f));

            // ── 구매 / 취소 버튼 ──
            bool canAfford = gold >= pendingPurchaseCost;
            CreatePopupButton(panel, canAfford ? "충전" : "골드 부족", new Vector2(-130f, -170f),
                              canAfford ? new Color(0.25f, 0.65f, 0.35f) : new Color(0.4f, 0.4f, 0.42f),
                              canAfford ? (UnityEngine.Events.UnityAction)OnConfirmManaPurchase : CloseManaPurchasePopup);
            CreatePopupButton(panel, "취소", new Vector2(130f, -170f),
                              new Color(0.6f, 0.3f, 0.32f), CloseManaPurchasePopup);

            Debug.Log($"[MPManager] 마나 충전 팝업: 필요 {pendingPurchaseMana}마나({pendingPurchaseCost}골드), 보유 골드 {gold}, 요구비용 {lastRequiredCost}");
        }

        /// <summary>구매 확정 — 골드 차감 성공 시 마나 회복 후 닫기.</summary>
        private void OnConfirmManaPurchase()
        {
            if (pendingPurchaseCost > 0 && GameManager.Instance != null && GameManager.Instance.SpendGold(pendingPurchaseCost))
            {
                AddMP(pendingPurchaseMana);
                Debug.Log($"[MPManager] 마나 충전 성공: -{pendingPurchaseCost}골드 → +{pendingPurchaseMana}마나 (1마나=10골드)");
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
            }
            CloseManaPurchasePopup();
        }

        /// <summary>팝업 닫기 + 그리드 입력 복원(게임 중일 때).</summary>
        private void CloseManaPurchasePopup()
        {
            if (manaPurchasePopupObj != null)
            {
                Destroy(manaPurchasePopupObj);
                manaPurchasePopupObj = null;
            }
            var input = Object.FindObjectOfType<JewelsHexaPuzzle.Core.InputSystem>();
            if (input != null
                && GameManager.Instance != null
                && GameManager.Instance.CurrentState == GameState.Playing)
                input.SetEnabled(true);
        }

        private void CreatePopupText(GameObject parent, string content, Vector2 anchoredPos,
                                     Vector2 size, int fontSize, FontStyle style, Color color)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = content;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void CreatePopupButton(GameObject parent, string label, Vector2 anchoredPos,
                                       Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Button_" + label);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(200f, 80f);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(go.transform, false);
            var trt = txtObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = txtObj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 34;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.text = label;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    /// <summary>
    /// 블록 스왑 아이템
    /// 인접한 두 블록의 위치를 교환합니다.
    /// 드래그 앤 드롭 방식: 블록 터치 → 인접 블록으로 드래그 → 릴리스 시 스왑
    /// </summary>
    public class SwapItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button swapButton;
        [SerializeField] private Image backgroundOverlay;
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;
        [SerializeField] private MatchingSystem matchingSystem;
        [SerializeField] private InputSystem inputSystem;

        [Header("Settings")]
        [SerializeField] private Color activeOverlayColor = new Color(0f, 1f, 0f, 0.15f);

        private bool isActive = false;
        private bool isProcessing = false;

        public bool IsActive => isActive;

        // 드래그 상태
        private HexBlock selectedBlock = null;
        private HexBlock dragTargetBlock = null;
        private bool isDragging = false;

        // 이펙트 부모
        private Transform effectParent;

        // 화면 흔들림 중첩 관리
        private int shakeCount = 0;
        private Vector3 shakeOriginalPos;

        // 대기 애니메이션 코루틴 참조
        private Coroutine idleAnimCoroutine;

        // 선택/드래그 하이라이트 오브젝트
        private GameObject selectedHighlight;
        private GameObject targetHighlight;

        // 버튼 기본 색상 (활성화 색상의 어두운 버전)
        private Color btnOriginalColor = new Color(0.24f, 0.42f, 0.60f, 0.92f);
        private static readonly Color BtnActiveColor = new Color(0.4f, 0.7f, 1f, 1f);

        private void Start()
        {
            AutoFindReferences();
            SetupUI();
            SetupEffectParent();
            if (backgroundOverlay != null)
            {
                backgroundOverlay.gameObject.SetActive(false);
                backgroundOverlay.raycastTarget = false;
            }
            // 버튼 색상은 SwapGauge가 관리
        }

        private void AutoFindReferences()
        {
            if (hexGrid == null) hexGrid = FindObjectOfType<HexGrid>();
            if (blockRemovalSystem == null) blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            if (matchingSystem == null) matchingSystem = FindObjectOfType<MatchingSystem>();
            if (inputSystem == null) inputSystem = FindObjectOfType<InputSystem>();
        }

        private void SetupUI()
        {
            if (swapButton != null)
                swapButton.onClick.AddListener(OnSwapButtonClicked);
        }

        private void SetupEffectParent()
        {
            if (hexGrid == null) return;
            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("SwapEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);
                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                effectLayer.transform.SetAsLastSibling();
                effectParent = effectLayer.transform;
            }
            else
            {
                effectParent = hexGrid.transform;
            }
        }

        private void OnSwapButtonClicked()
        {
            if (isProcessing) return;

            // 활성 상태에서 다시 클릭 → 비활성화
            if (isActive)
            {
                Deactivate();
                return;
            }

            // MP 체크: MP가 부족하면 사용 불가
            if (MPManager.Instance != null && !MPManager.Instance.CanUseItem(ItemType.Bomb))
            {
                Debug.Log($"[SwapItem] MP 부족: 필요 {MPManager.Instance.GetItemCost(ItemType.Bomb)}, 현재 {MPManager.Instance.CurrentMP}");
                var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                return;
            }

            Activate();
        }

        // ============================================================
        // 활성화/비활성화
        // ============================================================

        public void Activate()
        {
            if (isActive || isProcessing) return;

            // 다른 아이템 비활성화 (오버레이 중첩 방지)
            DeactivateOtherItems();

            isActive = true;
            if (inputSystem != null) inputSystem.SetEnabled(false);

            // 오버레이 페이드인
            if (backgroundOverlay != null)
            {
                backgroundOverlay.gameObject.SetActive(true);
                backgroundOverlay.color = new Color(activeOverlayColor.r, activeOverlayColor.g, activeOverlayColor.b, 0f);
                StartCoroutine(FadeOverlay(0f, activeOverlayColor.a, 0.15f));
            }

            // 버튼 활성화 펄스
            if (swapButton != null)
            {
                var img = swapButton.GetComponent<Image>();
                // 색상은 SwapGauge가 관리
                StartCoroutine(ButtonActivatePulse());
            }

            // 버튼 글로우 링
            if (swapButton != null)
                StartCoroutine(ButtonGlowRing());

            Debug.Log("[SwapItem] Activated");
        }

        public void Deactivate()
        {
            isActive = false;

            // 선택 해제
            ClearSelection();

            // 처리 중이 아닐 때만 입력 복원
            if (!isProcessing && inputSystem != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
                inputSystem.SetEnabled(true);

            // 오버레이 페이드아웃
            if (backgroundOverlay != null)
            {
                StartCoroutine(FadeOverlayThenHide(backgroundOverlay.color.a, 0f, 0.12f));
            }

            // 버튼 비활성화 연출
            if (swapButton != null)
            {
                StartCoroutine(ButtonDeactivateAnim());
            }

            Debug.Log("[SwapItem] Deactivated");
        }

        private void ClearSelection()
        {
            if (selectedBlock != null)
            {
                selectedBlock.SetHighlighted(false);
                selectedBlock = null;
            }
            if (dragTargetBlock != null)
            {
                dragTargetBlock.SetHighlighted(false);
                dragTargetBlock = null;
            }
            isDragging = false;

            if (selectedHighlight != null) { Destroy(selectedHighlight); selectedHighlight = null; }
            if (targetHighlight != null) { Destroy(targetHighlight); targetHighlight = null; }
        }

        // ============================================================
        // 입력 처리
        // ============================================================

        private void Update()
        {
            if (!isActive || isProcessing) return;
            // 구매 팝업 열려있으면 입력 차단
            if (GameManager.Instance != null && GameManager.Instance.IsPurchasePopupOpen) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseSwapInput();
#else
            HandleTouchSwapInput();
#endif
        }

        private void HandleMouseSwapInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // 버튼 클릭 확인 (스왑 버튼이면 무시)
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.IsPointerOverGameObject())
                {
                    var pd = new UnityEngine.EventSystems.PointerEventData(es);
                    pd.position = Input.mousePosition;
                    var results = new List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(pd, results);
                    foreach (var r in results)
                    {
                        if (r.gameObject == swapButton?.gameObject)
                            return;
                    }
                }

                // 블록 선택
                HexBlock block = FindBlockAtPosition(Input.mousePosition);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None && block.Data.CanMove())
                {
                    selectedBlock = block;
                    selectedBlock.SetHighlighted(true);
                    isDragging = true;
                    CreateSelectionIndicator(block);
                }
                else
                {
                    // 유효하지 않은 블록 → 비활성화
                    Deactivate();
                }
            }

            // 드래그 중
            if (isDragging && selectedBlock != null)
            {
                UpdateDragTarget(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(0) && isDragging)
            {
                isDragging = false;
                if (selectedBlock != null && dragTargetBlock != null)
                {
                    HexBlock blockA = selectedBlock;
                    HexBlock blockB = dragTargetBlock;

                    // 게이지 즉시 차감 (사용 결정 시점)
                    if (SwapGauge.Instance != null)
                        SwapGauge.Instance.OnItemUsed();

                    isProcessing = true;
                    Deactivate();
                    StartCoroutine(ExecuteSwap(blockA, blockB));
                }
                else
                {
                    ClearSelection();
                }
            }
        }

        private void HandleTouchSwapInput()
        {
            if (Input.touchCount == 0) return;
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // 버튼 클릭 확인
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && es.IsPointerOverGameObject(touch.fingerId))
                {
                    var pd = new UnityEngine.EventSystems.PointerEventData(es);
                    pd.position = touch.position;
                    var results = new List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(pd, results);
                    foreach (var r in results)
                    {
                        if (r.gameObject == swapButton?.gameObject)
                            return;
                    }
                }

                HexBlock block = FindBlockAtPosition(touch.position);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None && block.Data.CanMove())
                {
                    selectedBlock = block;
                    selectedBlock.SetHighlighted(true);
                    isDragging = true;
                    CreateSelectionIndicator(block);
                }
                else
                {
                    Deactivate();
                }
            }

            if (touch.phase == TouchPhase.Moved && isDragging && selectedBlock != null)
            {
                UpdateDragTarget(touch.position);
            }

            if (touch.phase == TouchPhase.Ended && isDragging)
            {
                isDragging = false;
                if (selectedBlock != null && dragTargetBlock != null)
                {
                    HexBlock blockA = selectedBlock;
                    HexBlock blockB = dragTargetBlock;

                    // 게이지 즉시 차감 (사용 결정 시점)
                    if (SwapGauge.Instance != null)
                        SwapGauge.Instance.OnItemUsed();

                    isProcessing = true;
                    Deactivate();
                    StartCoroutine(ExecuteSwap(blockA, blockB));
                }
                else
                {
                    ClearSelection();
                }
            }

            if (touch.phase == TouchPhase.Canceled)
            {
                isDragging = false;
                ClearSelection();
            }
        }

        /// <summary>
        /// 드래그 중 인접 블록 감지 및 하이라이트
        /// </summary>
        private void UpdateDragTarget(Vector2 screenPos)
        {
            if (selectedBlock == null || hexGrid == null) return;

            // 이전 드래그 대상 해제
            if (dragTargetBlock != null)
            {
                dragTargetBlock.SetHighlighted(false);
                dragTargetBlock = null;
            }
            if (targetHighlight != null) { Destroy(targetHighlight); targetHighlight = null; }

            // 현재 위치의 블록 감지
            HexBlock hoverBlock = FindBlockAtPosition(screenPos);
            if (hoverBlock == null || hoverBlock == selectedBlock) return;

            // 인접 블록인지 확인
            if (selectedBlock.Coord.DistanceTo(hoverBlock.Coord) != 1) return;

            // CanMove 체크
            if (hoverBlock.Data == null || hoverBlock.Data.gemType == GemType.None || !hoverBlock.Data.CanMove()) return;

            dragTargetBlock = hoverBlock;
            dragTargetBlock.SetHighlighted(true);
            CreateTargetIndicator(hoverBlock);
        }

        // ============================================================
        // 블록 감지
        // ============================================================

        private HexBlock FindBlockAtPosition(Vector2 screenPos)
        {
            if (hexGrid == null) return null;
            HexBlock closest = null;
            float closestDist = float.MaxValue;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt == null) continue;
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out localPoint);
                if (rt.rect.Contains(localPoint))
                {
                    float dist = localPoint.sqrMagnitude;
                    if (dist < closestDist) { closestDist = dist; closest = block; }
                }
            }
            return closest;
        }

        // ============================================================
        // 스왑 실행
        // ============================================================

        private IEnumerator ExecuteSwap(HexBlock blockA, HexBlock blockB)
        {
            isProcessing = true;
            Debug.Log($"[SwapItem] Swapping blocks: {blockA.Coord} <-> {blockB.Coord}");

            // MP 소모 (두 블록 중간 위치에 팝업)
            if (MPManager.Instance != null)
            {
                Vector3 midPos = (blockA.transform.position + blockB.transform.position) * 0.5f;
                MPManager.Instance.TryConsumeMP(MPManager.Instance.GetItemCost(ItemType.Bomb), midPos);
            }

            RectTransform rtA = blockA.GetComponent<RectTransform>();
            RectTransform rtB = blockB.GetComponent<RectTransform>();
            Vector2 posA = rtA.anchoredPosition;
            Vector2 posB = rtB.anchoredPosition;

            // 1. 스왑 애니메이션
            yield return StartCoroutine(SwapAnimation(blockA, blockB, posA, posB));

            // 2. 위치 복원 + 데이터 교환
            rtA.anchoredPosition = posA;
            rtB.anchoredPosition = posB;
            BlockData dataA = blockA.Data.Clone();
            BlockData dataB = blockB.Data.Clone();
            blockA.SetBlockData(dataB);
            blockB.SetBlockData(dataA);

            // 3. 스왑 이펙트
            Color colorA = GemColors.GetColor(dataA.gemType);
            Color colorB = GemColors.GetColor(dataB.gemType);
            StartCoroutine(SwapSparkEffect(posA, posB, colorA, colorB));

            // 4. 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRotateSound();

            yield return new WaitForSeconds(0.05f);

            // 5. 매칭 체크
            if (matchingSystem != null)
            {
                var matches = matchingSystem.FindMatches();
                if (matches.Count > 0)
                {
                    if (AudioManager.Instance != null)
                    {
                        int total = 0;
                        foreach (var m in matches) total += m.blocks.Count;
                        AudioManager.Instance.PlayMatchSound(total);
                    }
                    Debug.Log($"[SwapItem] Match found! {matches.Count} groups");
                }
                else
                {
                    Debug.Log("[SwapItem] No match, swap maintained");
                }
            }

            // 6. 낙하 + 캐스케이드 처리
            if (blockRemovalSystem != null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.IsItemAction = true;

                blockRemovalSystem.TriggerFallOnly();
                while (blockRemovalSystem.IsProcessing) yield return null;
            }

            // 7. 완료
            isProcessing = false;
            if (inputSystem != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
                inputSystem.SetEnabled(true);

            Debug.Log("[SwapItem] Swap complete");
        }

        // ============================================================
        // 스왑 애니메이션 (아크 궤적)
        // ============================================================

        private IEnumerator SwapAnimation(HexBlock blockA, HexBlock blockB, Vector2 posA, Vector2 posB)
        {
            RectTransform rtA = blockA.GetComponent<RectTransform>();
            RectTransform rtB = blockB.GetComponent<RectTransform>();

            float duration = 0.25f;
            float elapsed = 0f;
            float arcHeight = Vector2.Distance(posA, posB) * 0.3f;

            // 블록을 최상위로 올려 다른 블록 위에 표시
            int origIndexA = rtA.GetSiblingIndex();
            int origIndexB = rtB.GetSiblingIndex();
            rtA.SetAsLastSibling();
            rtB.SetAsLastSibling();

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                // 블록 A: posA → posB (위쪽 아크)
                Vector2 currentA = Vector2.Lerp(posA, posB, eased);
                float arcOffsetA = Mathf.Sin(t * Mathf.PI) * arcHeight;
                currentA.y += arcOffsetA;
                rtA.anchoredPosition = currentA;

                // 블록 B: posB → posA (아래쪽 아크)
                Vector2 currentB = Vector2.Lerp(posB, posA, eased);
                float arcOffsetB = Mathf.Sin(t * Mathf.PI) * arcHeight * -0.5f;
                currentB.y += arcOffsetB;
                rtB.anchoredPosition = currentB;

                // 스케일 펄스 (중간에 약간 커짐)
                float scalePulse = 1f + 0.1f * Mathf.Sin(t * Mathf.PI);
                rtA.localScale = Vector3.one * scalePulse;
                rtB.localScale = Vector3.one * scalePulse;

                yield return null;
            }

            // 스케일 복원
            rtA.localScale = Vector3.one;
            rtB.localScale = Vector3.one;
        }

        // ============================================================
        // VFX
        // ============================================================

        /// <summary>스왑 경로에 스파크 이펙트</summary>
        private IEnumerator SwapSparkEffect(Vector2 posA, Vector2 posB, Color colorA, Color colorB)
        {
            if (effectParent == null) yield break;

            int sparkCount = 6;
            Vector2 mid = (posA + posB) / 2f;

            for (int i = 0; i < sparkCount; i++)
            {
                float t = (float)i / sparkCount;
                Vector2 sparkPos = Vector2.Lerp(posA, posB, t);
                Color sparkColor = Color.Lerp(colorA, colorB, t);

                GameObject spark = new GameObject($"SwapSpark_{i}");
                spark.transform.SetParent(effectParent, false);
                RectTransform srt = spark.AddComponent<RectTransform>();
                srt.anchoredPosition = sparkPos + new Vector2(Random.Range(-5f, 5f), Random.Range(-5f, 5f));
                srt.sizeDelta = new Vector2(6f, 6f);

                Image img = spark.AddComponent<Image>();
                img.color = VisualConstants.Brighten(sparkColor);
                img.raycastTarget = false;

                StartCoroutine(AnimateSpark(spark, 0.3f + Random.Range(0f, 0.15f)));
            }
        }

        private IEnumerator AnimateSpark(GameObject spark, float duration)
        {
            if (spark == null) yield break;
            RectTransform rt = spark.GetComponent<RectTransform>();
            Image img = spark.GetComponent<Image>();
            if (rt == null || img == null) { Destroy(spark); yield break; }

            Vector2 startPos = rt.anchoredPosition;
            Vector2 velocity = new Vector2(Random.Range(-30f, 30f), Random.Range(10f, 40f));
            Color startColor = img.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                rt.anchoredPosition = startPos + velocity * t;
                velocity.y -= 80f * Time.deltaTime; // 중력

                float alpha = 1f - VisualConstants.EaseOutCubic(t);
                img.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                float scale = 1f - t * 0.5f;
                rt.localScale = Vector3.one * scale;

                yield return null;
            }

            Destroy(spark);
        }

        /// <summary>선택된 블록 인디케이터 (펄스 링)</summary>
        private void CreateSelectionIndicator(HexBlock block)
        {
            if (effectParent == null || block == null) return;
            if (selectedHighlight != null) Destroy(selectedHighlight);

            RectTransform blockRt = block.GetComponent<RectTransform>();
            if (blockRt == null) return;

            selectedHighlight = new GameObject("SwapSelectHL");
            selectedHighlight.transform.SetParent(block.transform, false);
            RectTransform hlRt = selectedHighlight.AddComponent<RectTransform>();
            hlRt.anchoredPosition = Vector2.zero;
            hlRt.sizeDelta = new Vector2(60f, 60f);

            Image hlImg = selectedHighlight.AddComponent<Image>();
            hlImg.color = new Color(0.4f, 0.85f, 1f, 0.5f);
            hlImg.raycastTarget = false;

            StartCoroutine(PulseIndicator(selectedHighlight));
        }

        /// <summary>드래그 대상 블록 인디케이터</summary>
        private void CreateTargetIndicator(HexBlock block)
        {
            if (effectParent == null || block == null) return;
            if (targetHighlight != null) Destroy(targetHighlight);

            targetHighlight = new GameObject("SwapTargetHL");
            targetHighlight.transform.SetParent(block.transform, false);
            RectTransform hlRt = targetHighlight.AddComponent<RectTransform>();
            hlRt.anchoredPosition = Vector2.zero;
            hlRt.sizeDelta = new Vector2(60f, 60f);

            Image hlImg = targetHighlight.AddComponent<Image>();
            hlImg.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            hlImg.raycastTarget = false;
        }

        private IEnumerator PulseIndicator(GameObject indicator)
        {
            if (indicator == null) yield break;
            RectTransform rt = indicator.GetComponent<RectTransform>();
            if (rt == null) yield break;

            while (indicator != null)
            {
                float t = Time.time * 3f;
                float scale = 1f + 0.15f * Mathf.Sin(t);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
        }

        // ============================================================
        // 버튼 연출 (HammerItem 패턴)
        // ============================================================

        private IEnumerator ButtonActivatePulse()
        {
            if (swapButton == null) yield break;
            Transform btnTransform = swapButton.transform;
            Vector3 origScale = Vector3.one;

            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
                btnTransform.localScale = origScale * scale;
                yield return null;
            }
            btnTransform.localScale = origScale;
        }

        private IEnumerator ButtonGlowRing()
        {
            if (swapButton == null) yield break;
            Transform parent = swapButton.transform.parent != null ? swapButton.transform.parent : swapButton.transform;

            GameObject glow = new GameObject("SwapGlowRing");
            glow.transform.SetParent(parent, false);
            glow.transform.localPosition = swapButton.transform.localPosition;
            glow.transform.SetSiblingIndex(swapButton.transform.GetSiblingIndex());

            var img = glow.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(0.4f, 0.7f, 1f, 0.5f);

            RectTransform rt = glow.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80f, 80f);

            float duration = 0.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + 0.6f * VisualConstants.EaseOutCubic(t);
                rt.localScale = Vector3.one * scale;
                float alpha = 0.5f * (1f - VisualConstants.EaseOutCubic(t));
                img.color = new Color(0.4f, 0.7f, 1f, alpha);
                yield return null;
            }

            Destroy(glow);
        }

        private IEnumerator ButtonDeactivateAnim()
        {
            if (swapButton == null) yield break;
            Transform btnTransform = swapButton.transform;
            Image btnImg = swapButton.GetComponent<Image>();

            float duration = 0.15f;
            float elapsed = 0f;
            Color startColor = btnImg != null ? btnImg.color : BtnActiveColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale = 1f - 0.1f * Mathf.Sin(t * Mathf.PI);
                btnTransform.localScale = Vector3.one * scale;

                // 색상은 SwapGauge가 관리

                yield return null;
            }

            btnTransform.localScale = Vector3.one;
        }

        // ============================================================
        // 오버레이 페이드
        // ============================================================

        private IEnumerator FadeOverlay(float from, float to, float duration)
        {
            if (backgroundOverlay == null) yield break;
            Color c = backgroundOverlay.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(from, to, VisualConstants.EaseOutCubic(t));
                backgroundOverlay.color = c;
                yield return null;
            }
            c.a = to;
            backgroundOverlay.color = c;
        }

        private IEnumerator FadeOverlayThenHide(float from, float to, float duration)
        {
            yield return StartCoroutine(FadeOverlay(from, to, duration));
            if (backgroundOverlay != null)
                backgroundOverlay.gameObject.SetActive(false);
        }

        // ============================================================
        // 이펙트 정리
        // ============================================================

        public void CleanupEffects()
        {
            if (effectParent == null) return;
            foreach (Transform child in effectParent)
            {
                if (child.name.StartsWith("SwapSpark") || child.name.StartsWith("SwapGlow"))
                    Destroy(child.gameObject);
            }
        }

        /// <summary>다른 활성 아이템 비활성화 (오버레이 중첩 방지)</summary>
        private void DeactivateOtherItems()
        {
            var hammer = FindObjectOfType<HammerItem>();
            if (hammer != null && hammer.IsActive) hammer.Deactivate();
            var lineDraw = FindObjectOfType<LineDrawItem>();
            if (lineDraw != null && lineDraw.IsActive) lineDraw.Deactivate();
            var reverse = FindObjectOfType<ReverseRotationItem>();
            if (reverse != null && reverse.IsActive) reverse.Deactivate();
        }
    }
}

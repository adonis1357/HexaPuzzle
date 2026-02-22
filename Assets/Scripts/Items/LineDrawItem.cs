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
    /// 한붓그리기 아이템
    /// 같은 색상의 인접 블록을 드래그로 연결하여 한 번에 제거합니다.
    /// 최소 2개 이상 연결 필요, 최대 제한 없음.
    /// 다른 색상 접촉 시 체인 리셋 (아이템은 활성 유지).
    /// 비활성화: 빈 공간 클릭 또는 버튼 재클릭.
    /// </summary>
    public class LineDrawItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button lineDrawButton;
        [SerializeField] private Image backgroundOverlay;
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;
        [SerializeField] private InputSystem inputSystem;

        [Header("Settings")]
        [SerializeField] private Color activeOverlayColor = new Color(0.9f, 0.6f, 0.2f, 0.15f);

        // 상태
        private bool isActive = false;
        private bool isProcessing = false;
        private bool isDrawing = false;

        public bool IsActive => isActive;

        // 체인 데이터
        private List<HexBlock> chain = new List<HexBlock>();
        private GemType chainColor = GemType.None;

        // 비주얼
        private List<GameObject> lineSegments = new List<GameObject>();
        private List<GameObject> blockHighlights = new List<GameObject>();

        // 이펙트 부모
        private Transform effectParent;

        // 화면 흔들림 중첩 관리
        private int shakeCount = 0;
        private Vector3 shakeOriginalPos;

        // 대기 애니메이션 코루틴 참조
        private Coroutine idleAnimCoroutine;

        // 버튼 기본 색상
        private static readonly Color BtnDefaultColor = new Color(0.25f, 0.25f, 0.35f, 0.9f);
        private static readonly Color BtnActiveColor = new Color(0.9f, 0.6f, 0.2f, 1f);

        // 라인 비주얼 설정
        private const float LINE_WIDTH = 6f;
        private const float LINE_ALPHA = 0.7f;
        private const float HIGHLIGHT_ALPHA = 0.4f;
        private const float HIGHLIGHT_SIZE = 70f;

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
        }

        private void AutoFindReferences()
        {
            if (hexGrid == null) hexGrid = FindObjectOfType<HexGrid>();
            if (blockRemovalSystem == null) blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            if (inputSystem == null) inputSystem = FindObjectOfType<InputSystem>();
        }

        private void SetupUI()
        {
            if (lineDrawButton != null)
                lineDrawButton.onClick.AddListener(OnLineDrawButtonClicked);
        }

        private void SetupEffectParent()
        {
            if (hexGrid == null) return;
            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("LineDrawEffectLayer");
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

        private void OnLineDrawButtonClicked()
        {
            if (isProcessing) return;
            if (isActive) Deactivate();
            else Activate();
        }

        // ============================================================
        // 활성화 연출
        // ============================================================

        public void Activate()
        {
            if (isActive || isProcessing) return;
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
            if (lineDrawButton != null)
            {
                var img = lineDrawButton.GetComponent<Image>();
                if (img != null) img.color = BtnActiveColor;
                StartCoroutine(ButtonActivatePulse());
            }

            // 버튼 글로우 링
            if (lineDrawButton != null)
                StartCoroutine(ButtonGlowRing());

            // 대기 애니메이션 시작
            idleAnimCoroutine = StartCoroutine(IdleAnimation());

            Debug.Log("[LineDrawItem] Activated");
        }

        public void Deactivate()
        {
            isActive = false;
            isDrawing = false;

            // 체인 비주얼 정리
            ClearChainVisuals();
            chain.Clear();
            chainColor = GemType.None;

            // 대기 애니메이션 중단
            if (idleAnimCoroutine != null)
            {
                StopCoroutine(idleAnimCoroutine);
                idleAnimCoroutine = null;
            }

            // 버튼 아이콘 스케일 복원
            if (lineDrawButton != null)
            {
                // 아이콘 자식 요소들의 스케일 복원
                foreach (Transform child in lineDrawButton.transform)
                {
                    if (child.name != "LineDrawLabel")
                        child.localScale = Vector3.one;
                }
            }

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
            if (lineDrawButton != null)
            {
                StartCoroutine(ButtonDeactivateAnim());
            }

            Debug.Log("[LineDrawItem] Deactivated");
        }

        // ============================================================
        // 체인 비주얼 관리
        // ============================================================

        private void ClearChainVisuals()
        {
            // 라인 세그먼트 제거
            foreach (var line in lineSegments)
            {
                if (line != null) Destroy(line);
            }
            lineSegments.Clear();

            // 블록 하이라이트 제거
            foreach (var hl in blockHighlights)
            {
                if (hl != null) Destroy(hl);
            }
            blockHighlights.Clear();

            // 체인 블록 하이라이트 해제
            foreach (var block in chain)
            {
                if (block != null) block.SetHighlighted(false);
            }
        }

        private void CreateBlockHighlight(HexBlock block)
        {
            if (block == null) return;

            Color gemColor = GemColors.GetColor(block.Data.gemType);

            GameObject hl = new GameObject("LineDrawHL");
            hl.transform.SetParent(block.transform, false);
            RectTransform hlRt = hl.AddComponent<RectTransform>();
            hlRt.anchoredPosition = Vector2.zero;
            hlRt.sizeDelta = new Vector2(HIGHLIGHT_SIZE, HIGHLIGHT_SIZE);

            Image hlImg = hl.AddComponent<Image>();
            hlImg.color = new Color(gemColor.r, gemColor.g, gemColor.b, HIGHLIGHT_ALPHA);
            hlImg.raycastTarget = false;

            blockHighlights.Add(hl);
            block.SetHighlighted(true);
        }

        private void CreateLineSegment(HexBlock fromBlock, HexBlock toBlock)
        {
            if (fromBlock == null || toBlock == null || effectParent == null) return;

            RectTransform fromRt = fromBlock.GetComponent<RectTransform>();
            RectTransform toRt = toBlock.GetComponent<RectTransform>();
            if (fromRt == null || toRt == null) return;

            // 두 블록의 월드 위치에서 라인 계산
            Vector3 fromPos = fromBlock.transform.position;
            Vector3 toPos = toBlock.transform.position;
            Vector3 midPos = (fromPos + toPos) / 2f;
            float distance = Vector3.Distance(fromPos, toPos);
            float angle = Mathf.Atan2(toPos.y - fromPos.y, toPos.x - fromPos.x) * Mathf.Rad2Deg;

            Color gemColor = GemColors.GetColor(chainColor);

            GameObject line = new GameObject("LineDrawSeg");
            line.transform.SetParent(effectParent, false);
            line.transform.position = midPos;

            Image lineImg = line.AddComponent<Image>();
            lineImg.color = new Color(gemColor.r, gemColor.g, gemColor.b, LINE_ALPHA);
            lineImg.raycastTarget = false;

            RectTransform lineRt = line.GetComponent<RectTransform>();
            lineRt.sizeDelta = new Vector2(distance, LINE_WIDTH);
            lineRt.localRotation = Quaternion.Euler(0, 0, angle);

            lineSegments.Add(line);
        }

        private void RemoveLastChainVisual()
        {
            // 마지막 하이라이트 제거
            if (blockHighlights.Count > 0)
            {
                int lastIdx = blockHighlights.Count - 1;
                if (blockHighlights[lastIdx] != null)
                    Destroy(blockHighlights[lastIdx]);
                blockHighlights.RemoveAt(lastIdx);
            }

            // 마지막 라인 세그먼트 제거
            if (lineSegments.Count > 0)
            {
                int lastIdx = lineSegments.Count - 1;
                if (lineSegments[lastIdx] != null)
                    Destroy(lineSegments[lastIdx]);
                lineSegments.RemoveAt(lastIdx);
            }
        }

        // ============================================================
        // 입력 처리
        // ============================================================

        private void Update()
        {
            if (!isActive || isProcessing) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // 버튼 클릭 확인 (자체 버튼이면 무시)
                if (IsClickOnButton(Input.mousePosition)) return;

                HexBlock block = FindBlockAtPosition(Input.mousePosition);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                {
                    StartChain(block);
                }
                else
                {
                    // 빈 공간 클릭 → 비활성화
                    Deactivate();
                }
            }

            // 드래그 중
            if (isDrawing && Input.GetMouseButton(0))
            {
                UpdateChain(Input.mousePosition);
            }

            // 릴리스
            if (Input.GetMouseButtonUp(0) && isDrawing)
            {
                FinishChain();
            }
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 0) return;
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                // 버튼 클릭 확인
                if (IsClickOnButton(touch.position)) return;

                HexBlock block = FindBlockAtPosition(touch.position);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                {
                    StartChain(block);
                }
                else
                {
                    Deactivate();
                }
            }

            if (touch.phase == TouchPhase.Moved && isDrawing)
            {
                UpdateChain(touch.position);
            }

            if (touch.phase == TouchPhase.Ended && isDrawing)
            {
                FinishChain();
            }

            if (touch.phase == TouchPhase.Canceled && isDrawing)
            {
                // 터치 취소 → 체인 리셋
                ClearChainVisuals();
                chain.Clear();
                chainColor = GemType.None;
                isDrawing = false;
            }
        }

        private bool IsClickOnButton(Vector2 screenPos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject())
            {
                var pd = new UnityEngine.EventSystems.PointerEventData(es);
                pd.position = screenPos;
                var results = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(pd, results);
                foreach (var r in results)
                {
                    if (r.gameObject == lineDrawButton?.gameObject)
                        return true;
                }
            }
            return false;
        }

        // ============================================================
        // 체인 관리
        // ============================================================

        private void StartChain(HexBlock block)
        {
            // 이전 체인 정리
            ClearChainVisuals();
            chain.Clear();

            chainColor = block.Data.gemType;
            chain.Add(block);
            isDrawing = true;

            CreateBlockHighlight(block);

            Debug.Log($"[LineDrawItem] Chain started: {block.Coord}, color={chainColor}");
        }

        private void UpdateChain(Vector2 screenPos)
        {
            if (chain.Count == 0) return;

            HexBlock hoverBlock = FindBlockAtPosition(screenPos);
            if (hoverBlock == null) return;

            // 되돌아가기: 직전 블록으로 돌아가면 마지막 블록 제거
            if (chain.Count >= 2 && hoverBlock == chain[chain.Count - 2])
            {
                HexBlock removedBlock = chain[chain.Count - 1];
                removedBlock.SetHighlighted(false);
                chain.RemoveAt(chain.Count - 1);
                RemoveLastChainVisual();
                return;
            }

            // 이미 체인에 있는 블록이면 무시
            if (chain.Contains(hoverBlock)) return;

            // 인접 블록인지 확인
            HexBlock lastBlock = chain[chain.Count - 1];
            if (lastBlock.Coord.DistanceTo(hoverBlock.Coord) != 1) return;

            // 유효한 블록인지 확인
            if (hoverBlock.Data == null || hoverBlock.Data.gemType == GemType.None) return;

            // 같은 색상인지 확인
            if (hoverBlock.Data.gemType == chainColor)
            {
                // 체인에 추가
                chain.Add(hoverBlock);
                CreateBlockHighlight(hoverBlock);
                CreateLineSegment(lastBlock, hoverBlock);

                // 사운드 피드백
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayRotateSound();
            }
            else
            {
                // 다른 색상 → 체인 리셋 (아이템은 활성 유지)
                ClearChainVisuals();
                chain.Clear();
                chainColor = GemType.None;
                isDrawing = false;

                Debug.Log("[LineDrawItem] Chain reset: different color");
            }
        }

        private void FinishChain()
        {
            isDrawing = false;

            if (chain.Count >= 2)
            {
                // 2개 이상 → 제거 실행
                Debug.Log($"[LineDrawItem] Chain complete: {chain.Count} blocks");

                // Deactivate()가 chain을 클리어하므로 먼저 복사
                List<HexBlock> blocksToRemove = new List<HexBlock>(chain);
                GemType removedColor = chainColor;

                isProcessing = true;
                Deactivate();
                StartCoroutine(ExecuteRemoval(blocksToRemove, removedColor));
            }
            else
            {
                // 1개 이하 → 체인 리셋 (아이템은 활성 유지)
                ClearChainVisuals();
                chain.Clear();
                chainColor = GemType.None;
            }
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
        // 제거 시퀀스
        // ============================================================

        private IEnumerator ExecuteRemoval(List<HexBlock> blocksToRemove, GemType removedColor)
        {
            isProcessing = true;
            Debug.Log($"[LineDrawItem] Removing {blocksToRemove.Count} blocks");

            // 1. HitStop + ZoomPunch (체인 길이에 비례)
            float hitStopDur = Mathf.Min(blocksToRemove.Count * 0.005f, VisualConstants.HitStopDurationSmall);
            StartCoroutine(HitStop(hitStopDur));
            StartCoroutine(ZoomPunch(blocksToRemove.Count >= 5 ? VisualConstants.ZoomPunchScaleLarge : VisualConstants.ZoomPunchScaleSmall));

            // 2. 순차적 블록 파괴 연출
            for (int i = 0; i < blocksToRemove.Count; i++)
            {
                HexBlock block = blocksToRemove[i];
                if (block == null || block.Data == null) continue;

                Vector3 blockPos = block.transform.position;
                Color blockColor = GemColors.GetColor(block.Data.gemType);

                // DestroyFlash
                StartCoroutine(DestroyFlashOverlay(block));

                // Bloom
                StartCoroutine(BloomLayer(blockPos, blockColor, VisualConstants.FlashInitialSize * 0.7f, VisualConstants.FlashDuration));

                // 파편 + 스파크
                SpawnDebris(block);
                SpawnSparks(blockPos, blockColor);

                // DualEasing Destroy
                StartCoroutine(DualEasingDestroy(block));

                // 짧은 딜레이 (순차 효과)
                yield return new WaitForSeconds(0.03f);
            }

            // 3. 블록 데이터 클리어 (비주얼 애니메이션 후)
            yield return new WaitForSeconds(0.1f);
            foreach (var block in blocksToRemove)
            {
                if (block != null)
                {
                    block.ClearData();
                    block.transform.localScale = Vector3.one;
                }
            }

            // 4. 화면 흔들림 (체인 길이에 비례)
            float shakeIntensity = Mathf.Lerp(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallIntensity * 2f,
                Mathf.Clamp01((blocksToRemove.Count - 2f) / 8f));
            StartCoroutine(ScreenShake(shakeIntensity, VisualConstants.ShakeSmallDuration));

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMatchSound(blocksToRemove.Count);

            if (blockRemovalSystem != null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.IsItemAction = true;

                blockRemovalSystem.TriggerFallOnly();
                while (blockRemovalSystem.IsProcessing) yield return null;
            }

            // 6. 완료
            isProcessing = false;
            if (inputSystem != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
                inputSystem.SetEnabled(true);

            Debug.Log("[LineDrawItem] Removal complete");
        }

        // ============================================================
        // Pre-Fire Compression (미사용, 향후 확장용)
        // ============================================================

        // ============================================================
        // HitStop
        // ============================================================

        private IEnumerator HitStop(float stopDuration)
        {
            if (!VisualConstants.CanHitStop()) yield break;
            VisualConstants.RecordHitStop();

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(stopDuration);

            float elapsed = 0f;
            while (elapsed < VisualConstants.HitStopSlowMoDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.HitStopSlowMoDuration);
                Time.timeScale = Mathf.Lerp(VisualConstants.HitStopSlowMoScale, 1f, VisualConstants.EaseOutCubic(t));
                yield return null;
            }
            Time.timeScale = 1f;
        }

        // ============================================================
        // ZoomPunch
        // ============================================================

        private IEnumerator ZoomPunch(float targetScale)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = target.localScale;
            Vector3 punchScale = origScale * targetScale;

            float elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchInDuration);
                target.localScale = Vector3.Lerp(origScale, punchScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchOutDuration);
                target.localScale = Vector3.Lerp(punchScale, origScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            target.localScale = origScale;
        }

        // ============================================================
        // DestroyFlash
        // ============================================================

        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            GameObject flash = new GameObject("LineDrawFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;

            var img = flash.AddComponent<Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float baseSize = 90f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(baseSize, baseSize * 0.866f);

            float elapsed = 0f;
            while (elapsed < VisualConstants.DestroyFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.DestroyFlashDuration);
                img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha * (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        // ============================================================
        // Bloom Layer
        // ============================================================

        private IEnumerator BloomLayer(Vector3 pos, Color color, float initSize, float duration)
        {
            yield return new WaitForSeconds(VisualConstants.BloomLag);

            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            GameObject bloom = new GameObject("LineDrawBloom");
            bloom.transform.SetParent(parent, false);
            bloom.transform.position = pos;
            bloom.transform.SetAsFirstSibling();

            var img = bloom.AddComponent<Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier);

            RectTransform rt = bloom.GetComponent<RectTransform>();
            float bloomSize = initSize * VisualConstants.BloomSizeMultiplier;
            rt.sizeDelta = new Vector2(bloomSize, bloomSize);

            float remainDuration = duration - VisualConstants.BloomLag;
            float elapsed = 0f;

            while (elapsed < remainDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / remainDuration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                rt.sizeDelta = new Vector2(bloomSize * scale, bloomSize * scale);
                img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier * (1f - t));
                yield return null;
            }

            Destroy(bloom);
        }

        // ============================================================
        // DualEasing Destroy
        // ============================================================

        private IEnumerator DualEasingDestroy(HexBlock block)
        {
            if (block == null) yield break;

            float totalDuration = VisualConstants.DestroyDuration;
            float expandPhase = totalDuration * VisualConstants.DestroyExpandPhaseRatio;
            float shrinkPhase = totalDuration * (1f - VisualConstants.DestroyExpandPhaseRatio);

            // Phase 1: 확대
            float elapsed = 0f;
            while (elapsed < expandPhase)
            {
                if (block == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / expandPhase);
                float scale = Mathf.Lerp(1f, VisualConstants.DestroyExpandScale, VisualConstants.EaseOutCubic(t));
                block.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            // Phase 2: 찌그러짐 + 축소
            elapsed = 0f;
            while (elapsed < shrinkPhase)
            {
                if (block == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkPhase);
                float eased = VisualConstants.EaseInQuad(t);

                float scaleBase = Mathf.Lerp(VisualConstants.DestroyExpandScale, 0f, eased);
                float squeezeX = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(t * Mathf.PI);
                float scaleX = scaleBase * squeezeX;
                float scaleY = scaleBase;

                block.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                yield return null;
            }

            block.transform.localScale = Vector3.zero;
        }

        // ============================================================
        // Screen Shake
        // ============================================================

        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;

            if (shakeCount == 0)
                shakeOriginalPos = target.localPosition;
            shakeCount++;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float decay = 1f - VisualConstants.EaseInQuad(t);
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                target.localPosition = shakeOriginalPos + new Vector3(x, y, 0);
                yield return null;
            }

            shakeCount--;
            if (shakeCount <= 0)
            {
                shakeCount = 0;
                target.localPosition = shakeOriginalPos;
            }
        }

        // ============================================================
        // Debris (파편)
        // ============================================================

        private void SpawnDebris(HexBlock block)
        {
            if (block == null) return;
            Color c = Color.gray;
            if (block.Data != null) c = GemColors.GetColor(block.Data.gemType);
            Vector3 center = block.transform.position;
            // 체인이므로 파편 수를 약간 줄임
            int count = Mathf.Max(2, VisualConstants.DebrisBaseCount / 2);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateDebris(center, c));
        }

        private IEnumerator AnimateDebris(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : transform.root;

            GameObject shard = new GameObject("LineDrawDebris");
            shard.transform.SetParent(parent, false);
            shard.transform.position = center;

            var img = shard.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = VisualConstants.Brighten(color);

            RectTransform rt = shard.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax);
            rt.sizeDelta = new Vector2(size, size);
            rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.DebrisBaseSpeedMin, VisualConstants.DebrisBaseSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            float rotSpd = Random.Range(VisualConstants.DebrisRotSpeedMin, VisualConstants.DebrisRotSpeedMax);
            float life = Random.Range(VisualConstants.DebrisLifetimeMin, VisualConstants.DebrisLifetimeMax);
            float elapsed = 0f;

            while (elapsed < life)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / life;
                vel.y += VisualConstants.DebrisGravity * Time.deltaTime;
                Vector3 pos = shard.transform.position;
                pos.x += vel.x * Time.deltaTime;
                pos.y += vel.y * Time.deltaTime;
                shard.transform.position = pos;
                vel *= 0.97f;
                rt.localRotation = Quaternion.Euler(0, 0, rt.localEulerAngles.z + rotSpd * Time.deltaTime);
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);
                color.a = 1f - t;
                img.color = color;
                yield return null;
            }
            Destroy(shard);
        }

        // ============================================================
        // Sparks (스파크)
        // ============================================================

        private void SpawnSparks(Vector3 center, Color color)
        {
            // 체인이므로 스파크 수를 약간 줄임
            int count = Mathf.Max(2, VisualConstants.SparkSmallCount / 2);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateSpark(center, color));
        }

        private IEnumerator AnimateSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : transform.root;

            GameObject spark = new GameObject("LineDrawSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<Image>();
            img.raycastTarget = false;

            Color sparkColor = new Color(
                Mathf.Min(1f, color.r + 0.3f),
                Mathf.Min(1f, color.g + 0.3f),
                Mathf.Min(1f, color.b + 0.3f),
                1f
            );
            img.color = sparkColor;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkSmallSizeMin, VisualConstants.SparkSmallSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkSmallSpeedMin, VisualConstants.SparkSmallSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float life = Random.Range(VisualConstants.SparkSmallLifetimeMin, VisualConstants.SparkSmallLifetimeMax);
            float elapsed = 0f;

            while (elapsed < life)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / life;
                Vector3 pos = spark.transform.position;
                pos.x += vel.x * Time.deltaTime;
                pos.y += vel.y * Time.deltaTime;
                spark.transform.position = pos;
                vel *= VisualConstants.SparkDeceleration;
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);
                sparkColor.a = 1f - t;
                img.color = sparkColor;
                yield return null;
            }
            Destroy(spark);
        }

        // ============================================================
        // 버튼 연출 (HammerItem 패턴)
        // ============================================================

        private IEnumerator ButtonActivatePulse()
        {
            if (lineDrawButton == null) yield break;
            Transform btnTransform = lineDrawButton.transform;
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
            if (lineDrawButton == null) yield break;
            Transform parent = lineDrawButton.transform.parent != null ? lineDrawButton.transform.parent : lineDrawButton.transform;

            GameObject glow = new GameObject("LineDrawGlowRing");
            glow.transform.SetParent(parent, false);
            glow.transform.localPosition = lineDrawButton.transform.localPosition;
            glow.transform.SetSiblingIndex(lineDrawButton.transform.GetSiblingIndex());

            var img = glow.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(0.9f, 0.6f, 0.2f, 0.5f);

            RectTransform rt = glow.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60f, 60f);

            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * 1.5f;
                rt.sizeDelta = new Vector2(60f * scale, 60f * scale);
                img.color = new Color(0.9f, 0.6f, 0.2f, 0.5f * (1f - t));
                yield return null;
            }
            Destroy(glow);
        }

        private IEnumerator IdleAnimation()
        {
            float phase = 0f;
            while (isActive)
            {
                phase += Time.deltaTime;

                // 버튼 색상 글로우 펄스
                if (lineDrawButton != null)
                {
                    float glowT = 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f / 1.2f);
                    float brightness = Mathf.Lerp(0.85f, 1f, glowT);
                    var img = lineDrawButton.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(
                            BtnActiveColor.r * brightness,
                            BtnActiveColor.g * brightness,
                            BtnActiveColor.b * brightness,
                            BtnActiveColor.a
                        );
                }

                yield return null;
            }
        }

        private IEnumerator ButtonDeactivateAnim()
        {
            if (lineDrawButton == null) yield break;
            Transform btnTransform = lineDrawButton.transform;
            var btnImg = lineDrawButton.GetComponent<Image>();

            float duration = 0.15f;
            float elapsed = 0f;
            Color startColor = btnImg != null ? btnImg.color : BtnActiveColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale = 1f - 0.1f * Mathf.Sin(t * Mathf.PI);
                btnTransform.localScale = Vector3.one * scale;

                if (btnImg != null)
                    btnImg.color = Color.Lerp(startColor, BtnDefaultColor, VisualConstants.EaseOutCubic(t));

                yield return null;
            }

            btnTransform.localScale = Vector3.one;
            if (btnImg != null) btnImg.color = BtnDefaultColor;
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
        // 정리
        // ============================================================

        private void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--)
            {
                Transform child = effectParent.GetChild(i);
                if (child.name.StartsWith("LineDraw"))
                    Destroy(child.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (lineDrawButton != null)
                lineDrawButton.onClick.RemoveListener(OnLineDrawButtonClicked);
            ClearChainVisuals();
            CleanupEffects();
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    public class HammerItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button hammerButton;
        [SerializeField] private Image hammerIcon;
        [SerializeField] private Image backgroundOverlay;
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;
        [SerializeField] private DrillBlockSystem drillSystem;
        [SerializeField] private InputSystem inputSystem;

        [Header("Settings")]
        [SerializeField] private Color activeOverlayColor = new Color(1f, 0f, 0f, 0.15f);

        private bool isActive = false;
        private bool isProcessing = false;

        public bool IsActive => isActive;
        public Button HammerButton => hammerButton;

        // 이펙트 부모
        private Transform effectParent;

        // 화면 흔들림 중첩 관리
        private int shakeCount = 0;
        private Vector3 shakeOriginalPos;

        // 대기 애니메이션 코루틴 참조
        private Coroutine idleAnimCoroutine;

        // 드래그 상태 필드
        private bool isDragging = false;
        private HexBlock dragCenterBlock = null;
        private HexCoord dragCenter;
        private int currentDragLevel = 0;
        private List<GameObject> previewOverlays = new List<GameObject>();

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
            if (drillSystem == null) drillSystem = FindObjectOfType<DrillBlockSystem>();
            if (inputSystem == null) inputSystem = FindObjectOfType<InputSystem>();
        }

        private void SetupUI()
        {
            if (hammerButton != null)
                hammerButton.onClick.AddListener(OnHammerButtonClicked);
        }

        private void SetupEffectParent()
        {
            if (hexGrid == null) return;
            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("HammerEffectLayer");
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

        private void OnHammerButtonClicked()
        {
            if (isProcessing) return;

            if (isActive)
            {
                Deactivate();
                return;
            }

            // MP 체크
            if (MPManager.Instance != null && !MPManager.Instance.CanUseItem(ItemType.Hammer))
            {
                Debug.Log($"[HammerItem] MP 부족: 필요 {MPManager.Instance.GetItemCost(ItemType.Hammer)}, 현재 {MPManager.Instance.CurrentMP}");
                var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                return;
            }

            Activate();
        }

        // ============================================================
        // 활성화 연출
        // ============================================================

        public void Activate()
        {
            if (isActive || isProcessing) return;

            DeactivateOtherItems();

            isActive = true;
            if (inputSystem != null) inputSystem.SetEnabled(false);

            if (backgroundOverlay != null)
            {
                backgroundOverlay.gameObject.SetActive(true);
                backgroundOverlay.color = new Color(activeOverlayColor.r, activeOverlayColor.g, activeOverlayColor.b, 0f);
                StartCoroutine(FadeOverlay(0f, activeOverlayColor.a, 0.15f));
            }

            if (hammerButton != null)
                StartCoroutine(ButtonActivatePulse());

            if (hammerButton != null)
                StartCoroutine(ButtonGlowRing());

            idleAnimCoroutine = StartCoroutine(IdleAnimation());

            Debug.Log("[HammerItem] Activated");
        }

        public void Deactivate()
        {
            isActive = false;

            // 드래그 상태 정리
            isDragging = false;
            dragCenterBlock = null;
            currentDragLevel = 0;
            ClearPreview();

            if (idleAnimCoroutine != null)
            {
                StopCoroutine(idleAnimCoroutine);
                idleAnimCoroutine = null;
            }

            if (hammerIcon != null)
                hammerIcon.transform.localScale = Vector3.one;

            if (!isProcessing && inputSystem != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
                inputSystem.SetEnabled(true);

            if (backgroundOverlay != null)
                StartCoroutine(FadeOverlayThenHide(backgroundOverlay.color.a, 0f, 0.12f));

            if (hammerButton != null)
                StartCoroutine(ButtonDeactivateAnim());

            if (HammerGauge.Instance != null)
                HammerGauge.Instance.OnHammerCancelled();

            Debug.Log("[HammerItem] Deactivated");
        }

        private IEnumerator ButtonActivatePulse()
        {
            if (hammerButton == null) yield break;
            Transform btnTransform = hammerButton.transform;
            Vector3 origScale = btnTransform.localScale;

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

            if (hammerIcon != null)
            {
                float shakeDur = 0.15f;
                elapsed = 0f;
                while (elapsed < shakeDur)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / shakeDur;
                    float angle = Mathf.Sin(t * Mathf.PI * 4f) * 3f * (1f - t);
                    hammerIcon.transform.localRotation = Quaternion.Euler(0, 0, angle);
                    yield return null;
                }
                hammerIcon.transform.localRotation = Quaternion.identity;
            }
        }

        private IEnumerator ButtonGlowRing()
        {
            if (hammerButton == null) yield break;
            Transform parent = hammerButton.transform.parent != null ? hammerButton.transform.parent : hammerButton.transform;

            GameObject glow = new GameObject("HammerGlowRing");
            glow.transform.SetParent(parent, false);
            glow.transform.localPosition = hammerButton.transform.localPosition;
            glow.transform.SetSiblingIndex(hammerButton.transform.GetSiblingIndex());

            var img = glow.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(0.9f, 0.15f, 0.15f, 0.5f);

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
                img.color = new Color(0.9f, 0.15f, 0.15f, 0.5f * (1f - t));
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

                if (hammerIcon != null)
                {
                    float breathScale = 1f + 0.05f * Mathf.Sin(phase * Mathf.PI * 2f / 1.5f);
                    hammerIcon.transform.localScale = Vector3.one * breathScale;
                }

                yield return null;
            }
        }

        private IEnumerator ButtonDeactivateAnim()
        {
            if (hammerButton == null) yield break;
            Transform btnTransform = hammerButton.transform;

            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f - 0.1f * Mathf.Sin(t * Mathf.PI);
                btnTransform.localScale = Vector3.one * scale;
                yield return null;
            }

            btnTransform.localScale = Vector3.one;
        }

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
        // 드래그 기반 타격 시스템
        // ============================================================

        private void Update()
        {
            if (!isActive || isProcessing) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPurchasePopupOpen) return;

            if (Input.GetMouseButtonDown(0))
            {
                HandleDragStart(Input.mousePosition);
            }
            else if (isDragging && Input.GetMouseButton(0))
            {
                HandleDragUpdate(Input.mousePosition);
            }
            else if (isDragging && Input.GetMouseButtonUp(0))
            {
                HandleDragEnd();
            }
        }

        /// <summary>드래그에서 axial 거리에 따른 파괴 레벨 결정</summary>
        private int DetermineDragLevel(int axialDist)
        {
            int gl = HammerGauge.Instance != null ? HammerGauge.Instance.GaugeLayer : 0;
            int hammerLevel = SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetHammerLevel() : 0;

            if (axialDist >= 3 && gl >= 3 && hammerLevel >= 2) return 3;
            if (axialDist >= 2 && gl >= 2 && hammerLevel >= 1) return 2;
            if (gl >= 1) return 1;
            return 1; // 최소 레벨1
        }

        /// <summary>레벨별 파괴 대상 좌표 목록 반환</summary>
        private List<HexCoord> GetDestructionCoords(HexCoord center, int level)
        {
            HashSet<HexCoord> coords = new HashSet<HexCoord>();

            switch (level)
            {
                case 1:
                    // 중심 + axial 거리 1 이내 = 7칸
                    foreach (var c in HexCoord.GetHexesInRadius(center, 1))
                        coords.Add(c);
                    break;

                case 2:
                    // 반지름 2 전체 = 19칸
                    foreach (var c in HexCoord.GetHexesInRadius(center, 2))
                        coords.Add(c);
                    break;

                case 3:
                    // 반지름 3 전체 = 37칸
                    foreach (var c in HexCoord.GetHexesInRadius(center, 3))
                        coords.Add(c);
                    break;

                default:
                    // 기본: 7칸
                    foreach (var c in HexCoord.GetHexesInRadius(center, 1))
                        coords.Add(c);
                    break;
            }

            return new List<HexCoord>(coords);
        }

        /// <summary>레벨별 게이지 레이어 소모량</summary>
        private int GetLayerCost(int level)
        {
            return level;
        }

        private void HandleDragStart(Vector2 screenPos)
        {
            // 버튼 클릭 감지
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject())
            {
                var pd = new UnityEngine.EventSystems.PointerEventData(es);
                pd.position = screenPos;
                var results = new List<UnityEngine.EventSystems.RaycastResult>();
                es.RaycastAll(pd, results);
                foreach (var r in results)
                {
                    if (r.gameObject == hammerButton?.gameObject)
                        return;
                }
            }

            HexBlock clickedBlock = FindBlockAtPosition(screenPos);

            if (clickedBlock != null && clickedBlock.Data != null && clickedBlock.Data.gemType != GemType.None)
            {
                dragCenterBlock = clickedBlock;
                dragCenter = clickedBlock.Coord;
                isDragging = true;
                currentDragLevel = DetermineDragLevel(0);
                ShowPreview(dragCenter, currentDragLevel);
            }
            else
            {
                Deactivate();
            }
        }

        private void HandleDragUpdate(Vector2 screenPos)
        {
            if (dragCenterBlock == null) return;

            HexBlock currentBlock = FindBlockAtPosition(screenPos);
            int axialDist = 0;

            if (currentBlock != null && currentBlock.Data != null)
            {
                axialDist = dragCenter.DistanceTo(currentBlock.Coord);
            }

            int newLevel = DetermineDragLevel(axialDist);

            if (newLevel != currentDragLevel)
            {
                currentDragLevel = newLevel;
                ShowPreview(dragCenter, currentDragLevel);
            }
        }

        private void HandleDragEnd()
        {
            isDragging = false;
            ClearPreview();

            if (dragCenterBlock == null || dragCenterBlock.Data == null || dragCenterBlock.Data.gemType == GemType.None)
            {
                dragCenterBlock = null;
                currentDragLevel = 0;
                return;
            }

            int level = currentDragLevel;
            HexBlock centerBlock = dragCenterBlock;
            HexCoord center = dragCenter;

            // 몬스터 직접 타격
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var destructionCoords = GetDestructionCoords(center, level);
                foreach (var coord in destructionCoords)
                    GoblinSystem.Instance.ApplyDamageAtPosition(coord, 1);
            }

            isProcessing = true;
            Deactivate();
            StartCoroutine(SmashByLevel(centerBlock, level));

            dragCenterBlock = null;
            currentDragLevel = 0;
        }

        // ============================================================
        // 범위 미리보기 UI
        // ============================================================

        private void ShowPreview(HexCoord center, int level)
        {
            ClearPreview();
            var coords = GetDestructionCoords(center, level);
            foreach (var coord in coords)
            {
                if (hexGrid == null) continue;
                if (!hexGrid.IsValidCoord(coord)) continue;
                HexBlock block = hexGrid.GetBlock(coord);
                if (block == null) continue;

                GameObject overlay = new GameObject("HammerPreview");
                overlay.transform.SetParent(block.transform, false);
                overlay.transform.localPosition = Vector3.zero;

                var img = overlay.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = new Color(1f, 0.2f, 0.2f, 0.35f); // 반투명 빨간

                RectTransform rt = overlay.GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(80f, 80f);

                previewOverlays.Add(overlay);
            }
        }

        private void ClearPreview()
        {
            foreach (var obj in previewOverlays)
            {
                if (obj != null) Destroy(obj);
            }
            previewOverlays.Clear();
        }

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
        // 레벨별 파괴 통합 메서드
        // ============================================================

        private IEnumerator SmashByLevel(HexBlock centerBlock, int level)
        {
            isProcessing = true;
            HexCoord center = centerBlock.Coord;
            int layerCost = GetLayerCost(level);
            Debug.Log($"[HammerItem] SmashByLevel {level} at {center} (cost: {layerCost} layers)");

            // MP 소모
            if (MPManager.Instance != null)
            {
                int baseCost = MPManager.Instance.GetItemCost(ItemType.Hammer);
                int mpCost = baseCost * (level + 1);
                MPManager.Instance.TryConsumeMP(mpCost, centerBlock.transform.position);
            }

            // 중심 블록 파괴 애니메이션
            yield return StartCoroutine(SmashAnimation(centerBlock));

            // 파괴 대상 좌표 수집
            var destructionCoords = GetDestructionCoords(center, level);

            // 중심 블록 파괴
            DestroyBlockAtCoord(centerBlock);

            // 나머지 블록 파괴 (중심 제외)
            if (level >= 1 && hexGrid != null)
            {
                foreach (var coord in destructionCoords)
                {
                    if (coord == center) continue;
                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;

                    if (block.Data.specialType == SpecialBlockType.None)
                    {
                        GameManager.Instance?.OnSingleGemDestroyedForMission(block.Data.gemType);
                        StartCoroutine(SmashAnimation(block));
                        block.ClearData();
                        block.transform.localScale = Vector3.one;
                    }
                }

                yield return new WaitForSeconds(0.15f);
            }

            // 낙하
            if (blockRemovalSystem != null)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.IsItemAction = true;

                blockRemovalSystem.TriggerFallOnly();
                while (blockRemovalSystem.IsProcessing) yield return null;
            }

            isProcessing = false;
            if (inputSystem != null && GameManager.Instance != null &&
                GameManager.Instance.CurrentState == GameState.Playing)
                inputSystem.SetEnabled(true);

            // 게이지 레이어 차감
            if (HammerGauge.Instance != null)
                HammerGauge.Instance.OnHammerUsedWithLevel(layerCost);
        }

        /// <summary>블록 파괴 헬퍼: 특수 블록이면 드릴 발동, 아니면 일반 파괴</summary>
        private void DestroyBlockAtCoord(HexBlock block)
        {
            if (block == null || block.Data == null) return;

            bool isSpecial = block.Data.specialType != SpecialBlockType.None;
            SpecialBlockType specialType = block.Data.specialType;

            if (isSpecial)
            {
                switch (specialType)
                {
                    case SpecialBlockType.Drill:
                        if (drillSystem != null)
                            drillSystem.ActivateDrill(block);
                        break;
                    default:
                        // 다른 특수 블록은 일반 파괴
                        GameManager.Instance?.OnSingleGemDestroyedForMission(block.Data.gemType);
                        block.ClearData();
                        block.transform.localScale = Vector3.one;
                        break;
                }
            }
            else
            {
                GameManager.Instance?.OnSingleGemDestroyedForMission(block.Data.gemType);
                block.ClearData();
                block.transform.localScale = Vector3.one;
            }
        }

        // ============================================================
        // SmashAnimation — VFX 표준 통합
        // ============================================================

        private IEnumerator SmashAnimation(HexBlock block)
        {
            if (block == null) yield break;
            Vector3 origPos = block.transform.position;

            yield return StartCoroutine(PreFireCompression(block));

            StartCoroutine(HitStop(VisualConstants.HitStopDurationSmall));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));
            StartCoroutine(DestroyFlashOverlay(block));
            Color blockColor = block.Data != null ? GemColors.GetColor(block.Data.gemType) : Color.gray;
            StartCoroutine(BloomLayer(origPos, blockColor, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            SpawnDebris(block);
            SpawnSparks(origPos, blockColor);
            StartCoroutine(ImpactWave(origPos, blockColor));

            StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));

            yield return StartCoroutine(DualEasingDestroy(block));

            yield return new WaitForSeconds(0.03f);
        }

        // ============================================================
        // Pre-Fire Compression
        // ============================================================

        private IEnumerator PreFireCompression(HexBlock block)
        {
            if (block == null) yield break;
            float elapsed = 0f;
            float duration = VisualConstants.PreFireDuration;

            Image blockImg = block.GetComponent<Image>();
            Color origColor = blockImg != null ? blockImg.color : Color.white;

            while (elapsed < duration)
            {
                if (block == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.5f)
                {
                    float ct = t / 0.5f;
                    scale = Mathf.Lerp(1f, VisualConstants.PreFireScaleMin, VisualConstants.EaseInQuad(ct));
                }
                else
                {
                    float et = (t - 0.5f) / 0.5f;
                    scale = Mathf.Lerp(VisualConstants.PreFireScaleMin, VisualConstants.PreFireScaleMax, VisualConstants.EaseOutCubic(et));
                }

                block.transform.localScale = Vector3.one * scale;

                if (blockImg != null)
                {
                    float brighten = t * VisualConstants.PreFireBrightenAmount;
                    blockImg.color = new Color(
                        Mathf.Min(1f, origColor.r + brighten),
                        Mathf.Min(1f, origColor.g + brighten),
                        Mathf.Min(1f, origColor.b + brighten),
                        origColor.a
                    );
                }

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            if (blockImg != null) blockImg.color = origColor;
        }

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
            bool isOwner = VisualConstants.TryBeginZoomPunch();
            if (!isOwner) yield break;

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
            VisualConstants.EndZoomPunch();
        }

        // ============================================================
        // DestroyFlash
        // ============================================================

        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            GameObject flash = new GameObject("HammerDestroyFlash");
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

            GameObject bloom = new GameObject("HammerBloomLayer");
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
        // Impact Wave
        // ============================================================

        private IEnumerator ImpactWave(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            GameObject wave = new GameObject("HammerImpactWave");
            wave.transform.SetParent(parent, false);
            wave.transform.position = pos;

            var image = wave.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha);

            RectTransform rt = wave.GetComponent<RectTransform>();
            float initSize = VisualConstants.WaveSmallInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            float duration = VisualConstants.WaveSmallDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.WaveSmallExpand - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha * (1f - t));
                yield return null;
            }

            Destroy(wave);
        }

        // ============================================================
        // Screen Shake
        // ============================================================

        private IEnumerator ScreenShake(float intensity, float duration)
        {
            bool isOwner = VisualConstants.TryBeginScreenShake();
            if (!isOwner) yield break;

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
            VisualConstants.EndScreenShake();
        }

        // ============================================================
        // Debris
        // ============================================================

        private void SpawnDebris(HexBlock block)
        {
            if (block == null) return;
            Color c = Color.gray;
            if (block.Data != null) c = GemColors.GetColor(block.Data.gemType);
            Vector3 center = block.transform.position;
            for (int i = 0; i < VisualConstants.DebrisBaseCount; i++)
                StartCoroutine(AnimateDebris(center, c));
        }

        private IEnumerator AnimateDebris(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : transform.root;

            GameObject shard = new GameObject("HammerDebris");
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
        // Sparks
        // ============================================================

        private void SpawnSparks(Vector3 center, Color color)
        {
            for (int i = 0; i < VisualConstants.SparkSmallCount; i++)
                StartCoroutine(AnimateSpark(center, color));
        }

        private IEnumerator AnimateSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : transform.root;

            GameObject spark = new GameObject("HammerSpark");
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
        // 정리
        // ============================================================

        private void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);
        }

        private void DeactivateOtherItems()
        {
            var swap = FindObjectOfType<SwapItem>();
            if (swap != null && swap.IsActive) swap.Deactivate();
            var lineDraw = FindObjectOfType<LineDrawItem>();
            if (lineDraw != null && lineDraw.IsActive) lineDraw.Deactivate();
            var reverse = FindObjectOfType<ReverseRotationItem>();
            if (reverse != null && reverse.IsActive) reverse.Deactivate();
        }

        private void OnDestroy()
        {
            if (hammerButton != null)
                hammerButton.onClick.RemoveListener(OnHammerButtonClicked);
            CleanupEffects();
        }
    }
}

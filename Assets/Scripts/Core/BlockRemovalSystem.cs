using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    public class BlockRemovalSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private MatchingSystem matchingSystem;
        [SerializeField] private DrillBlockSystem drillSystem;
        [SerializeField] private BombBlockSystem bombSystem;
        [SerializeField] private DonutBlockSystem donutSystem;
        [SerializeField] private XBlockSystem xBlockSystem;
        [SerializeField] private LaserBlockSystem laserSystem;

        [Header("Animation Settings")]
        [SerializeField] private float matchHighlightDuration = 0.15f;
        [SerializeField] private float removeAnimationDuration = 0.14f;
        [SerializeField] private float cascadeDelay = 0.1f;

        [Header("Fall Physics")]
        [SerializeField] private float gravity = 2500f;
        [SerializeField] private float maxFallSpeed = 1500f;
        [SerializeField] private float bounceRatio = 0.3f;
        [SerializeField] private float bounceThreshold = 50f;

        private bool isProcessing = false;
        private bool isFalling = false;  // 낙하 재진입 방지

        // Cascade depth tracking (Phase 6B)
        private int currentCascadeDepth = 0;
        public int CurrentCascadeDepth => currentCascadeDepth;

        // �� ����� ���� ���� ��ġ (���� ���� �� �ѹ� ĳ��)
        private Dictionary<HexBlock, Vector2> slotPositions = new Dictionary<HexBlock, Vector2>();
        private Dictionary<int, List<HexBlock>> columnCache = null;
        private bool slotsCached = false;

        public event System.Action<int, int, Vector3> OnBlocksRemoved; // (blockCount, cascadeDepth, avgPosition)
        public event System.Action OnCascadeComplete;
        public event System.Action OnBigBang;

        public bool IsProcessing => isProcessing;

        /// <summary>
        /// Stuck 상태 복구용 - 모든 플래그 리셋
        /// </summary>
        public void ForceReset()
        {
            StopAllCoroutines();
            isProcessing = false;
            isFalling = false;
            currentCascadeDepth = 0;

            // 코루틴 중단으로 남은 이펙트 오브젝트 정리
            CleanupOrphanedEffects();

            // 블록 시각 상태 복원 (scale, rotation, position) - 캐시 클리어 전에 수행
            RestoreAllBlockStates();

            slotsCached = false;
            Debug.LogWarning("[BlockRemovalSystem] ForceReset called");
        }

        /// <summary>
        /// 코루틴 중단으로 남은 임시 이펙트 오브젝트 정리
        /// (Spark, LightningArc → this.transform 자식 / ElectricArc, SpecialImpact → block.transform 자식)
        /// </summary>
        private void CleanupOrphanedEffects()
        {
            // 1. this.transform의 임시 이펙트 자식 정리
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.name.StartsWith("Spark") || child.name.StartsWith("LightningArc") ||
                    child.name.StartsWith("DestroyFlash") || child.name.StartsWith("BloomLayer") ||
                    child.name.StartsWith("MatchPulseGlow"))
                    Destroy(child);
            }

            // 2. 블록 transform의 임시 이펙트 자식 정리
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null) continue;
                    for (int i = block.transform.childCount - 1; i >= 0; i--)
                    {
                        GameObject child = block.transform.GetChild(i).gameObject;
                        if (child.name.StartsWith("ElectricArc") ||
                            child.name.StartsWith("SpecialImpact") ||
                            child.name.StartsWith("SpawnGlow") ||
                            child.name.StartsWith("DestroyFlash") ||
                            child.name.StartsWith("MatchPulseGlow"))
                            Destroy(child);
                    }
                }
            }

            // 3. Reset cascade depth
            currentCascadeDepth = 0;
        }

        /// <summary>
        /// 모든 블록의 시각 상태 복원 (낙하/삭제 애니메이션 중단 시)
        /// </summary>
        private void RestoreAllBlockStates()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                block.transform.localScale = Vector3.one;
                block.transform.localRotation = Quaternion.identity;
                if (slotPositions.ContainsKey(block))
                    SetBlockAnchoredPosition(block, slotPositions[block]);
            }
        }

        /// <summary>
        /// 슬롯 캐시 무효화 (스테이지 전환 시 호출)
        /// </summary>
        public void InvalidateSlotCache()
        {
            slotsCached = false;
            slotPositions.Clear();
            columnCache = null;
        }


        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[BlockRemovalSystem] HexGrid auto-found: " + hexGrid.name);
            }
            if (matchingSystem == null)
                matchingSystem = FindObjectOfType<MatchingSystem>();
            if (drillSystem == null)
                drillSystem = FindObjectOfType<DrillBlockSystem>();
            if (bombSystem == null)
                bombSystem = FindObjectOfType<BombBlockSystem>();
            if (donutSystem == null)
                donutSystem = FindObjectOfType<DonutBlockSystem>();
            if (xBlockSystem == null)
                xBlockSystem = FindObjectOfType<XBlockSystem>();
            if (laserSystem == null)
                laserSystem = FindObjectOfType<LaserBlockSystem>();




        }

        /// <summary>
        /// ���� ��ġ�� �� ĳ�ø� �ѹ��� ���� (�Ǵ� ����)
        /// ����� ���� ��ġ�� �����ϹǷ� ���� �ִϸ��̼� �߿��� ���� ��ġ�� �� �� ����
        /// </summary>
        private void EnsureSlotsCached()
        {
            if (slotsCached && slotPositions.Count > 0) return;

            slotPositions.Clear();
            columnCache = new Dictionary<int, List<HexBlock>>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                RectTransform rt = block.GetComponent<RectTransform>();
                Vector2 pos = rt != null ? rt.anchoredPosition : (Vector2)block.transform.localPosition;
                slotPositions[block] = pos;

                int colKey = Mathf.RoundToInt(pos.x);
                if (!columnCache.ContainsKey(colKey))
                    columnCache[colKey] = new List<HexBlock>();
                columnCache[colKey].Add(block);
            }

            // Y��ǥ �������� ���� (�Ʒ�����)
            foreach (var key in columnCache.Keys.ToList())
            {
                columnCache[key] = columnCache[key]
                    .OrderBy(b => slotPositions[b].y)
                    .ToList();
            }

            slotsCached = true;
            Debug.Log($"[BlockRemovalSystem] Cached {slotPositions.Count} slot positions, {columnCache.Count} columns");
        }

        private void SetBlockAnchoredPosition(HexBlock block, Vector2 pos)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = pos;
            else block.transform.localPosition = new Vector3(pos.x, pos.y, 0);
        }

        private void RestoreBlockToSlot(HexBlock block)
        {
            if (block == null) return;
            if (slotPositions.TryGetValue(block, out Vector2 pos))
                SetBlockAnchoredPosition(block, pos);
            block.transform.localScale = Vector3.one;
        }

        // ============================================================
        // ��Ī ó��
        // ============================================================

        public void ProcessMatches(List<MatchingSystem.MatchGroup> matches)
        {
            if (isProcessing || matches == null || matches.Count == 0) return;
            StartCoroutine(ProcessMatchesCoroutine(matches));
        }

/// <summary>
        /// 매칭 처리 + pending 특수블록 동시 발동 (연쇄 처리용)
        /// 낙하 후 매칭이 발견되었을 때, pending 특수블록과 함께 동시 실행
        /// </summary>
        public void ProcessMatchesWithPendingSpecials(List<MatchingSystem.MatchGroup> matches, List<HexBlock> pendingSpecials)
        {
            if (isProcessing) return;
            bool hasMatches = matches != null && matches.Count > 0;
            bool hasPending = pendingSpecials != null && pendingSpecials.Count > 0;
            if (!hasMatches && !hasPending) return;
            StartCoroutine(ProcessMatchesWithPendingCoroutine(matches, pendingSpecials));
        }

private IEnumerator ProcessMatchesWithPendingCoroutine(List<MatchingSystem.MatchGroup> matches, List<HexBlock> pendingSpecials)
        {
            isProcessing = true;
            EnsureSlotsCached();

            // Safety: matches 유효성 검증
            if (matches == null || matches.Count == 0)
            {
                // pending만 있는 경우도 처리
                if (pendingSpecials != null && pendingSpecials.Count > 0)
                {
                    yield return StartCoroutine(ProcessMatchesInline(null, pendingSpecials));
                }
                else
                {
                    isProcessing = false;
                    yield break;
                }
            }
            else
            {
                // 인라인 처리 (재귀 없음)
                yield return StartCoroutine(ProcessMatchesInline(matches, pendingSpecials));
            }

            // 낙하 + 연쇄 처리 (완전 반복문)
            yield return StartCoroutine(CascadeWithPendingLoop());
            // Note: CascadeWithPendingLoop 내부에서 isProcessing = false 및 OnCascadeComplete 호출
        }


private IEnumerator ProcessMatchesCoroutine(List<MatchingSystem.MatchGroup> matches)
        {
            isProcessing = true;
            EnsureSlotsCached();

            // Safety: matches 유효성 검증
            if (matches == null || matches.Count == 0)
            {
                isProcessing = false;
                yield break;
            }

            // 인라인 처리 (재귀 없음)
            yield return StartCoroutine(ProcessMatchesInline(matches, null));

            // 낙하 + 연쇄 처리 (완전 반복문)
            yield return StartCoroutine(CascadeWithPendingLoop());
            // Note: CascadeWithPendingLoop 내부에서 isProcessing = false 및 OnCascadeComplete 호출
        }

/// <summary>
        /// 특수 블록 발동 + 완료 대기 (통합, BlockRemovalSystem 내부용)
        /// 새 특수 블록 추가 시 case만 추가
        /// </summary>
private IEnumerator ActivateSpecialAndWaitLocal(HexBlock block)
        {
            // Safety: 블록이 이미 파괴되었거나 데이터가 없으면 즉시 종료
            if (block == null || block.Data == null || block.gameObject == null) yield break;
            
            // 발동 전 specialType 캐싱 (발동 중 Data가 변경될 수 있음)
            SpecialBlockType cachedType = block.Data.specialType;
            if (cachedType == SpecialBlockType.None) yield break;
            
            float timeout = 5f;
            float waited = 0f;

            // 발동 직전 데이터 재검증 (동시 발동 중 다른 특수 블록이 이 블록을 파괴했을 수 있음)
            if (block.Data == null || block.Data.gemType == GemType.None || block.Data.specialType == SpecialBlockType.None)
            {
                Debug.LogWarning($"[BRS] ActivateSpecialAndWaitLocal: block data invalidated before activation (cachedType={cachedType})");
                yield break;
            }

            switch (cachedType)
            {
                case SpecialBlockType.Drill:
                    if (drillSystem != null)
                    {
                        drillSystem.ActivateDrill(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (drillSystem.IsDrilling && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (drillSystem.IsDrilling) { Debug.LogError("[BRS] Drill timeout! ForceReset"); drillSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Bomb:
                    if (bombSystem != null)
                    {
                        bombSystem.ActivateBomb(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (bombSystem.IsBombing && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (bombSystem.IsBombing) { Debug.LogError("[BRS] Bomb timeout! ForceReset"); bombSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Rainbow:
                    if (donutSystem != null)
                    {
                        donutSystem.ActivateDonut(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (donutSystem.IsActivating && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (donutSystem.IsActivating) { Debug.LogError("[BRS] Donut timeout! ForceReset"); donutSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.XBlock:
                    if (xBlockSystem != null)
                    {
                        xBlockSystem.ActivateXBlock(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (xBlockSystem.IsActivating && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (xBlockSystem.IsActivating) { Debug.LogError("[BRS] XBlock timeout! ForceReset"); xBlockSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Laser:
                    if (laserSystem != null)
                    {
                        laserSystem.ActivateLaser(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (laserSystem.IsActivating && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (laserSystem.IsActivating) { Debug.LogError("[BRS] Laser timeout! ForceReset"); laserSystem.ForceReset(); }
                    }
                    break;
            }
        }

        /// <summary>
        /// 폭탄 합체 애니메이션 - 5개 블록이 중앙으로 빨려들어가며 폭탄 생성
        /// </summary>
/// <summary>
        /// 특수 블록 합체 애니메이션 (통합)
        /// 모든 특수 블록이 동일한 합체 연출을 사용
        /// 블록 수에 따라 자연스럽게 연출 강도 조절
        /// </summary>
        private IEnumerator SpecialBlockMergeAnimation(
            List<HexBlock> blocks, HexBlock spawnBlock,
            SpecialBlockType specialType, DrillDirection drillDirection, GemType gemType)
        {
            if (blocks == null || spawnBlock == null) yield break;

            Vector3 targetPos = spawnBlock.transform.position;
            // 블록 수에 따라 드라마 조절 (4개=드릴, 5+개=폭탄 등)
            float mergeDuration = 0.4f + blocks.Count * 0.02f;
            float rotationMult = blocks.Count >= 5 ? 360f : 180f;
            float swirlAmount = blocks.Count >= 5 ? 20f : 0f;

            Dictionary<HexBlock, Vector3> startPositions = new Dictionary<HexBlock, Vector3>();
            Dictionary<HexBlock, Vector3> startScales = new Dictionary<HexBlock, Vector3>();

            foreach (var block in blocks)
            {
                if (block != null && block != spawnBlock)
                {
                    startPositions[block] = block.transform.position;
                    startScales[block] = block.transform.localScale;
                }
            }

            // 전기 이펙트 + 번개선
            List<GameObject> electricObjects = new List<GameObject>();
            List<GameObject> arcLines = new List<GameObject>();
            foreach (var block in blocks)
                if (block != null)
                    electricObjects.Add(CreateElectricArcObject(block.transform));

            for (int i = 0; i < blocks.Count; i++)
                for (int j = i + 1; j < blocks.Count; j++)
                    if (blocks[i] != null && blocks[j] != null)
                        arcLines.Add(CreateLightningArc(blocks[i].transform, blocks[j].transform));

            // 스파크
            foreach (var block in blocks)
                if (block != null)
                    StartCoroutine(SpawnSparks(block.transform.position, mergeDuration));

            // 합체 애니메이션
            float elapsed = 0f;
            while (elapsed < mergeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / mergeDuration;
                float easeT = 1f - Mathf.Pow(1f - t, 3f);

                foreach (var kvp in startPositions)
                {
                    HexBlock block = kvp.Key;
                    if (block == null) continue;

                    Vector3 startPos = kvp.Value;

                    // 공통 경로: 곡선 이동 + 소용돌이 (블록 수에 따라 강도)
                    Vector3 diff = targetPos - startPos;
                    Vector3 perpendicular = new Vector3(-diff.y, diff.x, 0).normalized * swirlAmount * (1f - easeT);
                    Vector3 midPoint = (startPos + targetPos) / 2f + Vector3.up * 15f * (1f - easeT);
                    Vector3 currentPos;
                    if (easeT < 0.5f)
                        currentPos = Vector3.Lerp(startPos, midPoint, easeT * 2f);
                    else
                        currentPos = Vector3.Lerp(midPoint, targetPos, (easeT - 0.5f) * 2f);
                    currentPos += perpendicular * Mathf.Sin(easeT * Mathf.PI);
                    block.transform.position = currentPos;

                    float pulse = 1f + 0.06f * Mathf.Sin(t * Mathf.PI * 8f);
                    float shrink = 1f - easeT * 0.9f;
                    block.transform.localScale = startScales[block] * shrink * pulse;

                    float rotZ = easeT * rotationMult * (block.GetInstanceID() % 2 == 0 ? 1f : -1f);
                    block.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                }

                UpdateElectricArcs(electricObjects, t);
                UpdateLightningArcs(arcLines, t);

                float spawnPulse = 1f + 0.1f * Mathf.Sin(t * Mathf.PI * 10f);
                spawnBlock.transform.localScale = Vector3.one * spawnPulse;

                yield return null;
            }

            // 정리
            foreach (var obj in electricObjects) if (obj != null) Destroy(obj);
            foreach (var arc in arcLines) if (arc != null) Destroy(arc);

            foreach (var kvp in startPositions)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.transform.localRotation = Quaternion.identity;
                    kvp.Key.transform.localScale = Vector3.zero;
                }
            }
            spawnBlock.transform.localScale = Vector3.one;
            spawnBlock.transform.localRotation = Quaternion.identity;

            // 특수 블록 생성 (통합 디스패쳐)
            CreateSpecialBlock(spawnBlock, specialType, drillDirection, gemType);

            // 임팩트 이펙트 (모든 특수 블록 동일)
            StartCoroutine(SpecialSpawnImpact(spawnBlock));

            yield return new WaitForSeconds(0.15f);
        }

        /// <summary>
        /// 특수 블록 생성 디스패쳐 (통합)
        /// 새 특수 블록 추가 시 case만 추가
        /// </summary>
private void CreateSpecialBlock(HexBlock block, SpecialBlockType type, DrillDirection direction, GemType gemType)
        {
            switch (type)
            {
                case SpecialBlockType.Drill:
                    if (drillSystem != null)
                        drillSystem.CreateDrillBlock(block, direction, gemType);
                    break;
                case SpecialBlockType.Bomb:
                    if (bombSystem != null)
                        bombSystem.CreateBombBlock(block, gemType);
                    break;
                case SpecialBlockType.Rainbow:
                    if (donutSystem != null)
                        donutSystem.CreateDonutBlock(block, gemType);
                    break;
                case SpecialBlockType.XBlock:
                    if (xBlockSystem != null)
                        xBlockSystem.CreateXBlock(block, gemType);
                    break;
                case SpecialBlockType.Laser:
                    if (laserSystem != null)
                        laserSystem.CreateLaserBlock(block, gemType);
                    break;
            }
        }

// ActivateDrillAndWaitLocal/ActivateBombAndWaitLocal → ActivateSpecialAndWaitLocal로 통합됨


        /// <summary>
        /// �帱 ���� �ִϸ��̼� - 4�� ����� �߾����� �������� ��� + ���� ����Ʈ
        /// </summary>
// DrillMergeAnimation/BombMergeAnimation → SpecialBlockMergeAnimation로 통합됨

        /// <summary>
        /// ���� ����Ʈ - ��� �ֺ��� ���� ���� ȿ��
        /// </summary>
private IEnumerator ElectricEffect(HexBlock block, float duration)
        {
            if (block == null) yield break;
            // 이제 DrillMergeAnimation에서 직접 처리하므로 빈 메서드로 유지
            yield return null;
        }

        /// <summary>
        /// �帱 ���� ���� �÷��� ����Ʈ
        /// </summary>
private IEnumerator DrillSpawnFlash(HexBlock block)
        {
            // DrillSpawnImpact로 대체
            if (block == null) yield break;
            yield return null;
        }

/// <summary>
        /// 블록에 전기 아크 오브젝트 생성
        /// </summary>
        private GameObject CreateElectricArcObject(Transform parent)
        {
            GameObject obj = new GameObject("ElectricArc");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = Vector3.zero;

            var image = obj.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(0.4f, 0.7f, 1f, 0.8f);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60f, 60f);

            return obj;
        }

        /// <summary>
        /// 두 블록 사이 번개선 생성
        /// </summary>
        private GameObject CreateLightningArc(Transform from, Transform to)
        {
            GameObject obj = new GameObject("LightningArc");
            obj.transform.SetParent(transform, false);

            var image = obj.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(0.6f, 0.85f, 1f, 0.7f);

            RectTransform rt = obj.GetComponent<RectTransform>();
            Vector3 mid = (from.position + to.position) / 2f;
            rt.position = mid;

            float dist = Vector3.Distance(from.position, to.position);
            rt.sizeDelta = new Vector2(dist, 3f);

            Vector3 dir = to.position - from.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            return obj;
        }

        /// <summary>
        /// 전기 아크 업데이트 (플리커 + 크기 변화)
        /// </summary>
        private void UpdateElectricArcs(List<GameObject> arcs, float progress)
        {
            foreach (var obj in arcs)
            {
                if (obj == null) continue;
                var image = obj.GetComponent<UnityEngine.UI.Image>();
                var rt = obj.GetComponent<RectTransform>();
                if (image == null || rt == null) continue;

                float flicker = Random.Range(0.3f, 1f);
                float alpha = flicker * (1f - progress * 0.5f);
                image.color = new Color(
                    0.4f + Random.Range(0f, 0.3f),
                    0.7f + Random.Range(0f, 0.2f),
                    1f,
                    alpha * 0.9f
                );

                rt.localPosition = new Vector3(
                    Random.Range(-4f, 4f),
                    Random.Range(-4f, 4f),
                    0
                );

                float scale = Random.Range(0.6f, 1.3f) * (1f - progress * 0.3f);
                rt.sizeDelta = new Vector2(60f * scale, 60f * scale);
            }
        }

        /// <summary>
        /// 번개선 업데이트 (위치 추적 + 플리커)
        /// </summary>
        private void UpdateLightningArcs(List<GameObject> arcs, float progress)
        {
            foreach (var obj in arcs)
            {
                if (obj == null) continue;
                var image = obj.GetComponent<UnityEngine.UI.Image>();
                var rt = obj.GetComponent<RectTransform>();
                if (image == null || rt == null) continue;

                float flicker = Random.Range(0.2f, 1f);
                image.color = new Color(
                    0.5f + Random.Range(0f, 0.4f),
                    0.8f + Random.Range(0f, 0.2f),
                    1f,
                    flicker * (1f - progress) * 0.7f
                );

                float thickness = Random.Range(1.5f, 5f) * (1f - progress * 0.5f);
                Vector2 size = rt.sizeDelta;
                size.y = thickness;
                rt.sizeDelta = size;

                float jitter = Random.Range(-3f, 3f);
                Vector3 pos = rt.localPosition;
                pos.y += jitter;
                rt.localPosition = pos;
            }
        }

        /// <summary>
        /// 스파크 파티클 생성
        /// </summary>
        private IEnumerator SpawnSparks(Vector3 center, float duration)
        {
            float elapsed = 0f;
            float spawnInterval = 0.04f;
            float nextSpawn = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= nextSpawn)
                {
                    nextSpawn += spawnInterval;
                    StartCoroutine(AnimateSpark(center));
                }
                yield return null;
            }
        }

        /// <summary>
        /// 개별 스파크 애니메이션
        /// </summary>
        private IEnumerator AnimateSpark(Vector3 center)
        {
            GameObject spark = new GameObject("Spark");
            spark.transform.SetParent(transform, false);
            spark.transform.position = center;

            var image = spark.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(3f, 8f);
            rt.sizeDelta = new Vector2(size, size);

            // 랜덤 방향
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(80f, 200f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            // 밝은 전기색
            Color sparkColor = new Color(
                Random.Range(0.5f, 0.8f),
                Random.Range(0.7f, 1f),
                1f,
                1f
            );
            image.color = sparkColor;

            float lifetime = Random.Range(0.1f, 0.25f);
            float elapsedTime = 0f;

            while (elapsedTime < lifetime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / lifetime;

                Vector3 pos = spark.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                spark.transform.position = pos;

                // 감속
                velocity *= 0.95f;

                // 페이드 아웃 + 축소
                sparkColor.a = 1f - t;
                image.color = sparkColor;
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);

                yield return null;
            }

            Destroy(spark);
        }

        /// <summary>
        /// 드릴 생성 임팩트 - 충격파 + 밝은 플래시
        /// </summary>
/// <summary>
        /// 특수 블록 생성 임팩트 - 충격파 + 밝은 플래시 (모든 특수 블록 공통)
        /// </summary>
        private IEnumerator SpecialSpawnImpact(HexBlock block)
        {
            if (block == null) yield break;

            // 1) 밝은 플래시
            GameObject flashObj = new GameObject("SpecialImpactFlash");
            flashObj.transform.SetParent(block.transform, false);
            flashObj.transform.localPosition = Vector3.zero;

            var flashImage = flashObj.AddComponent<UnityEngine.UI.Image>();
            flashImage.sprite = HexBlock.GetHexFlashSprite();
            flashImage.color = new Color(0.8f, 0.9f, 1f, 1f);
            flashImage.raycastTarget = false;

            RectTransform flashRt = flashObj.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(30f, 30f);

            // 2) 충격파 링
            GameObject ringObj = new GameObject("SpecialImpactRing");
            ringObj.transform.SetParent(block.transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            var ringImage = ringObj.AddComponent<UnityEngine.UI.Image>();
            ringImage.sprite = HexBlock.GetHexFlashSprite();
            ringImage.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            ringImage.raycastTarget = false;

            RectTransform ringRt = ringObj.GetComponent<RectTransform>();
            ringRt.sizeDelta = new Vector2(10f, 10f);

            float punchDuration = 0.15f;
            float impactDuration = 0.3f;
            float elapsed = 0f;

            // 스파크 버스트
            for (int i = 0; i < 12; i++)
                StartCoroutine(AnimateSpark(block.transform.position));

            while (elapsed < impactDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / impactDuration;

                float flashScale = 1f + t * 6f;
                flashRt.sizeDelta = new Vector2(30f * flashScale, 30f * flashScale);
                flashImage.color = new Color(0.8f, 0.9f, 1f, (1f - t) * 0.8f);

                float ringScale = 1f + t * 8f;
                ringRt.sizeDelta = new Vector2(10f * ringScale, 10f * ringScale);
                ringImage.color = new Color(0.5f, 0.8f, 1f, (1f - t) * 0.5f);

                if (elapsed < punchDuration)
                {
                    float pt = elapsed / punchDuration;
                    float punch = 1f + 0.3f * Mathf.Sin(pt * Mathf.PI);
                    block.transform.localScale = Vector3.one * punch;
                }
                else
                {
                    block.transform.localScale = Vector3.one;
                }

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            Destroy(flashObj);
            Destroy(ringObj);
        }


        private IEnumerator AnimateRemove(HexBlock block)
        {
            if (block == null) yield break;

            // 화이트 플래시 오버레이
            StartCoroutine(DestroyFlashOverlay(block));

            float elapsed = 0f;
            float duration = VisualConstants.DestroyDuration;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (t < expandRatio)
                {
                    // Phase 1: 확대 (0~20%)
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = Vector3.one * scale;
                }
                else
                {
                    // Phase 2: 찌그러짐 + 축소 (20~100%)
                    float shrinkT = (t - expandRatio) / (1f - expandRatio);
                    float sx = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(shrinkT * Mathf.PI);
                    float sy = Mathf.Max(0f, 1f - VisualConstants.EaseInQuad(shrinkT));
                    block.transform.localScale = new Vector3(sx, sy, 1f);
                }

                yield return null;
            }

            block.transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// 파괴 순간 백색 플래시 오버레이
        /// </summary>
        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            GameObject flash = new GameObject("DestroyFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 60f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

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

        /// <summary>
        /// 매칭 블록 펄스 애니메이션 - 크기 1→1.08→1 + 백색 글로우
        /// </summary>
        private IEnumerator MatchPulse(HexBlock block, float delay)
        {
            if (block == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (block == null) yield break;

            // 백색 글로우 오버레이
            GameObject glow = new GameObject("MatchPulseGlow");
            glow.transform.SetParent(block.transform, false);
            glow.transform.localPosition = Vector3.zero;

            var glowImg = glow.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(1f, 1f, 1f, 0f);

            RectTransform glowRt = glow.GetComponent<RectTransform>();
            glowRt.sizeDelta = new Vector2(60f, 60f);

            float elapsed = 0f;
            float duration = VisualConstants.MatchPulseDuration;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutElastic 기반 스케일 펄스
                float pulseT = VisualConstants.EaseOutElastic(t);
                float scale = 1f + (VisualConstants.MatchPulseScale - 1f) * (1f - pulseT);
                block.transform.localScale = origScale * scale;

                // 글로우 알파: 전반부 페이드인, 후반부 페이드아웃
                float alpha = t < 0.3f
                    ? VisualConstants.MatchPulseOverlayAlpha * (t / 0.3f)
                    : VisualConstants.MatchPulseOverlayAlpha * (1f - (t - 0.3f) / 0.7f);
                if (glowImg != null)
                    glowImg.color = new Color(1f, 1f, 1f, alpha);

                yield return null;
            }

            if (block != null)
                block.transform.localScale = origScale;
            if (glow != null)
                Destroy(glow);
        }

        // ============================================================
        // ���� ó��
        // ============================================================

/// <summary>
        /// 낙하 후 pendingActivation 플래그가 있는 특수 블록을 수집하고 플래그 해제
        /// </summary>
        /// <summary>
        /// 낙하 → pending + 매칭 확인 → 처리 → 반복 (통합 연쇄 루프)
        /// 모든 매칭/특수블록 처리 후 호출하여 연쇄를 완전히 처리
        /// </summary>
private IEnumerator CascadeWithPendingLoop()
        {
            int maxIterations = 20;
            int iteration = 0;
            bool fatalError = false;
            currentCascadeDepth = 0;

            while (iteration < maxIterations && !fatalError)
            {
                iteration++;
                currentCascadeDepth = iteration - 1;

                // 1. 낙하 처리
                yield return StartCoroutine(ProcessFalling());
                yield return new WaitForSeconds(cascadeDelay);

                // 2. pending 블록 수집
                List<HexBlock> cascadePending = CollectAndClearPendingSpecials();

                // 3. 매칭 확인
                List<MatchingSystem.MatchGroup> cascadeMatches = null;
                if (matchingSystem != null)
                {
                    var m = matchingSystem.FindMatches();
                    if (m != null && m.Count > 0) cascadeMatches = m;
                }

                // 4. 아무것도 없으면 연쇄 종료
                if (cascadePending.Count == 0 && cascadeMatches == null)
                {
                    Debug.Log($"[BRS] Cascade loop ended at iteration #{iteration} (nothing to process)");
                    break;
                }

                // 5. 매칭 있음 (pending과 함께 또는 단독) → 인라인 처리 후 루프 반복
                if (cascadeMatches != null)
                {
                    Debug.Log($"[BRS] Cascade #{iteration}: {(cascadePending.Count > 0 ? cascadePending.Count + " pending + " : "")}{cascadeMatches.Count} matches");
                    yield return StartCoroutine(ProcessMatchesInline(cascadeMatches, cascadePending.Count > 0 ? cascadePending : null));
                    continue;
                }

                // 6. pending만 있음 → 발동 후 루프 반복
                if (cascadePending.Count > 0)
                {
                    Debug.Log($"[BRS] Cascade #{iteration}: {cascadePending.Count} pending specials only");
                    // 유효한 블록만 필터링
                    List<HexBlock> validPending = new List<HexBlock>();
                    foreach (var sp in cascadePending)
                    {
                        if (sp != null && sp.Data != null && sp.gameObject != null && sp.Data.specialType != SpecialBlockType.None)
                            validPending.Add(sp);
                    }
                    
                    if (validPending.Count == 0)
                    {
                        Debug.LogWarning("[BRS] All pending specials became invalid. Breaking cascade.");
                        break;
                    }
                    
                    List<Coroutine> cos = new List<Coroutine>();
                    foreach (var sp in validPending)
                        cos.Add(StartCoroutine(ActivateSpecialAndWaitLocal(sp)));
                    foreach (var co in cos) yield return co;
                    continue;
                }
            }

            if (iteration >= maxIterations)
                Debug.LogError($"[BRS] CascadeWithPendingLoop hit max iterations ({maxIterations})! Breaking.");

            // === 항상 도달하는 최종 정리 ===
            currentCascadeDepth = 0;
            isProcessing = false;
            OnCascadeComplete?.Invoke();
            Debug.Log($"[BRS] CascadeWithPendingLoop completed. isProcessing=false");
        }

private List<HexBlock> CollectAndClearPendingSpecials()
        {
            List<HexBlock> result = new List<HexBlock>();
            if (hexGrid == null) return result;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null &&
                    block.Data.pendingActivation &&
                    block.Data.specialType != SpecialBlockType.None)
                {
                    block.StopWarningBlink();
                    block.Data.pendingActivation = false;
                    result.Add(block);
                }
            }
            if (result.Count > 0)
                Debug.Log($"[BlockRemovalSystem] Collected {result.Count} pending specials for cascade");
            return result;
        }

private IEnumerator ProcessFalling()
        {
            if (hexGrid == null || columnCache == null) yield break;

            List<FallAnimation> existingAnimations = new List<FallAnimation>();
            List<FallAnimation> newBlockAnimations = new List<FallAnimation>();

            Dictionary<int, float> columnBaseDelay = new Dictionary<int, float>();
            foreach (var key in columnCache.Keys)
                columnBaseDelay[key] = Random.Range(0f, 0.04f);

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float colDelay = columnBaseDelay[kvp.Key];

                List<BlockData> dataList = new List<BlockData>();
                List<int> sourceSlots = new List<int>();

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    {
                        dataList.Add(block.Data.Clone());
                        sourceSlots.Add(i);
                    }
                }

                int emptyCount = column.Count - dataList.Count;
                if (emptyCount == 0) continue;

                for (int i = 0; i < column.Count; i++)
                {
                    if (column[i] != null)
                    {
                        column[i].HideVisuals();
                        RestoreBlockToSlot(column[i]);
                    }
                }

                float maxExistingDelay = 0f;
                for (int i = 0; i < dataList.Count; i++)
                {
                    int targetSlot = i;
                    int sourceSlot = sourceSlots[i];
                    HexBlock targetBlock = column[targetSlot];
                    if (targetBlock == null) continue;

                    if (sourceSlot != targetSlot)
                    {
                        Vector2 startPos = slotPositions.ContainsKey(column[sourceSlot]) ? slotPositions[column[sourceSlot]] : slotPositions[targetBlock];
                        SetBlockAnchoredPosition(targetBlock, startPos);
                    }

                    targetBlock.SetBlockData(dataList[i]);
                    targetBlock.transform.localScale = Vector3.one;

                    if (sourceSlot != targetSlot && slotPositions.ContainsKey(column[sourceSlot]))
                    {
                        Vector2 startPos = slotPositions[column[sourceSlot]];
                        int fallDistance = sourceSlot - targetSlot;
                        float heightDelay = fallDistance * 0.025f;
                        float jitter = Random.Range(0f, 0.02f);
                        float totalDelay = colDelay + heightDelay + jitter;

                        if (totalDelay > maxExistingDelay)
                            maxExistingDelay = totalDelay;

                        existingAnimations.Add(new FallAnimation
                        {
                            block = targetBlock,
                            startY = startPos.y,
                            targetY = slotPositions[targetBlock].y,
                            delay = totalDelay,
                            gravityMult = Random.Range(0.92f, 1.08f),
                            maxSpeedMult = Random.Range(0.90f, 1.10f),
                        });
                    }
                }

                float topY = slotPositions[column[column.Count - 1]].y;
                float spawnOffset = 120f;
                float newBlockBaseDelay = maxExistingDelay + 0.08f;

                for (int i = 0; i < emptyCount; i++)
                {
                    int targetSlot = dataList.Count + i;
                    HexBlock targetBlock = column[targetSlot];
                    if (targetBlock == null) continue;
                    Vector2 targetPos = slotPositions.ContainsKey(targetBlock) ? slotPositions[targetBlock] : Vector2.zero;

                    GemType randomGem = GemTypeHelper.GetRandom();
                    BlockData newData = new BlockData(randomGem);

                    float startY = topY + spawnOffset + (i * 80f);
                    SetBlockAnchoredPosition(targetBlock, new Vector2(targetPos.x, startY));
                    targetBlock.SetBlockData(newData);
                    targetBlock.transform.localScale = Vector3.one;

                    float newDelay = newBlockBaseDelay + i * 0.04f + Random.Range(0f, 0.025f);

                    newBlockAnimations.Add(new FallAnimation
                    {
                        block = targetBlock,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = newDelay,
                        gravityMult = Random.Range(0.90f, 1.10f),
                        maxSpeedMult = Random.Range(0.88f, 1.12f),
                        isNewBlock = true,
                    });
                }
            }

            int totalCount = existingAnimations.Count + newBlockAnimations.Count;
            if (totalCount == 0)
            {
                yield break;
            }

            int completedCount = 0;

            foreach (var anim in existingAnimations)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            foreach (var anim in newBlockAnimations)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            // 타임아웃 포함 대기 (최대 8초)
            float waitElapsed = 0f;
            while (completedCount < totalCount && waitElapsed < 8f)
            {
                waitElapsed += Time.deltaTime;
                yield return null;
            }

            if (completedCount < totalCount)
            {
                Debug.LogError($"[BRS] ProcessFalling timeout! {completedCount}/{totalCount} completed. Force finishing.");
                // 강제로 모든 블록을 슬롯 위치로 복원
                foreach (var anim in existingAnimations)
                    if (anim.block != null) RestoreBlockToSlot(anim.block);
                foreach (var anim in newBlockAnimations)
                    if (anim.block != null) RestoreBlockToSlot(anim.block);
            }
        }

        /// <summary>
        /// ���� ��� ���� �ִϸ��̼�
        /// ����� �̹� �����Ͱ� �����ǰ� ���� ��ġ�� ��ġ�� ����
        /// </summary>
private IEnumerator AnimateFall(FallAnimation anim, System.Action onComplete)
        {
            if (anim.block == null) { onComplete?.Invoke(); yield break; }

            if (anim.delay > 0)
                yield return new WaitForSeconds(anim.delay);

            HexBlock block = anim.block;

            // 딜레이 후 블록이 파괴되었을 수 있음
            if (block == null) { onComplete?.Invoke(); yield break; }

            if (!slotPositions.ContainsKey(block)) { onComplete?.Invoke(); yield break; }
            Vector2 slotPos = slotPositions[block];

            float currentY = anim.startY;
            float velocity = 0f;
            float targetY = anim.targetY;
            float blockGravity = gravity * anim.gravityMult;
            float blockMaxSpeed = maxFallSpeed * anim.maxSpeedMult;

            int bounceCount = 0;
            int maxBounces = 2;
            float elapsed = 0f;
            float maxDuration = 5f; // 안전장치: 최대 5초

            while (elapsed < maxDuration)
            {
                // 블록이 중간에 파괴되면 즉시 완료 처리
                if (block == null)
                {
                    onComplete?.Invoke();
                    yield break;
                }

                elapsed += Time.deltaTime;
                velocity -= blockGravity * Time.deltaTime;
                velocity = Mathf.Max(velocity, -blockMaxSpeed);
                currentY += velocity * Time.deltaTime;

                if (currentY <= targetY)
                {
                    currentY = targetY;

                    if (bounceCount < maxBounces && Mathf.Abs(velocity) > bounceThreshold)
                    {
                        velocity = -velocity * bounceRatio;
                        bounceCount++;
                        if (block != null) StartCoroutine(SquashEffect(block));
                    }
                    else
                    {
                        if (block != null)
                        {
                            SetBlockAnchoredPosition(block, slotPos);
                            block.transform.localScale = Vector3.one;
                            // Spawn pop-in animation for new blocks
                            if (anim.isNewBlock)
                                StartCoroutine(SpawnPopAnimation(block));
                        }
                        onComplete?.Invoke();
                        yield break;
                    }
                }

                if (block != null)
                    SetBlockAnchoredPosition(block, new Vector2(slotPos.x, currentY));
                yield return null;
            }

            // 타임아웃: 강제 완료
            if (block != null)
            {
                SetBlockAnchoredPosition(block, slotPos);
                block.transform.localScale = Vector3.one;
            }
            Debug.LogWarning($"[BRS] AnimateFall timeout for block at slot Y={targetY}");
            onComplete?.Invoke();
        }

        private IEnumerator SquashEffect(HexBlock block)
        {
            if (block == null) yield break;
            float duration = 0.08f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scaleX = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                float scaleY = 1f - 0.1f * Mathf.Sin(t * Mathf.PI);
                block.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                yield return null;
            }
            block.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// �� ����� ������ ���� �ִϸ��̼����� ä�� (���ڱ� ����� ���� ����)
        /// </summary>
private void FillEmptyBlocksWithAnimation()
        {
            if (hexGrid == null || columnCache == null) return;

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
                float colDelay = Random.Range(0f, 0.03f);
                int newBlockIndex = 0;

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];

                    // ★ 데이터가 있는 블록은 위치를 건드리지 않음 (낙하 중일 수 있음)
                    if (block.Data != null && block.Data.gemType != GemType.None)
                        continue;

                    RestoreBlockToSlot(block);

                    GemType randomGem = GemTypeHelper.GetRandom();
                    block.SetBlockData(new BlockData(randomGem));
                    block.transform.localScale = Vector3.one;

                    Vector2 slotPos = slotPositions[block];
                    float startY = topY + 120f + newBlockIndex * 80f;
                    SetBlockAnchoredPosition(block, new Vector2(slotPos.x, startY));

                    StartCoroutine(AnimateFall(new FallAnimation
                    {
                        block = block,
                        startY = startY,
                        targetY = slotPos.y,
                        delay = colDelay + newBlockIndex * 0.04f + Random.Range(0f, 0.02f),
                        gravityMult = Random.Range(0.90f, 1.10f),
                        maxSpeedMult = Random.Range(0.88f, 1.12f),
                    }, null));

                    newBlockIndex++;
                    Debug.LogWarning($"[BlockRemovalSystem] Force filled empty block at column {kvp.Key}, slot {i}");
                }
            }
        }

        // ============================================================
        // BigBang
        // ============================================================

        /// <summary>
        /// 낙하만 처리 (드릴 파괴 후 호출)
        /// </summary>
        /// <summary>
        /// 낙하만 처리 (매칭 체크 없음) - 외부에서 코루틴으로 직접 호출 가능
        /// 특수 블록과 동시 실행할 때 사용
        /// </summary>
public IEnumerator ProcessFallingCoroutinePublic()
        {
            if (isFalling)
            {
                Debug.LogWarning("[BlockRemovalSystem] ProcessFallingCoroutinePublic skipped - already falling");
                yield break;
            }
            isFalling = true;
            EnsureSlotsCached();
            yield return StartCoroutine(ProcessFalling());
            isFalling = false;
        }

        
public void TriggerFallOnly()
        {
            if (isProcessing) return;
            StartCoroutine(FallOnlyCoroutine());
        }

        private IEnumerator FallOnlyCoroutine()
        {
            isProcessing = true;
            EnsureSlotsCached();

            yield return StartCoroutine(ProcessFalling());

            yield return new WaitForSeconds(cascadeDelay);

            // 낙하 후 연쇄 매칭 확인
            if (matchingSystem != null)
            {
                var newMatches = matchingSystem.FindMatches();
                if (newMatches.Count > 0)
                {
                    yield return StartCoroutine(ProcessMatchesCoroutine(newMatches));
                    yield break;
                }
            }

            isProcessing = false;
            OnCascadeComplete?.Invoke();
        }

        
        /// <summary>
        /// 게임 시작 연출: 모든 블록을 위에서 낙하시킨다 (데이터는 이미 세팅됨)
        /// </summary>
        public void TriggerStartDrop()
        {
            if (isProcessing) return;
            StartCoroutine(StartDropCoroutine());
        }

        private IEnumerator StartDropCoroutine()
        {
            isProcessing = true;
            EnsureSlotsCached();

            if (hexGrid == null || columnCache == null) { isProcessing = false; yield break; }

            int completedCount = 0;
            int totalCount = 0;
            List<FallAnimation> anims = new List<FallAnimation>();

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
                float colDelay = Random.Range(0f, 0.06f);

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    Vector2 targetPos = slotPositions[block];

                    // 위치를 화면 위로 이동 (아래 블록일수록 낮은 startY → 먼저 도착)
                    float startY = topY + 200f + i * 70f;
                    SetBlockAnchoredPosition(block, new Vector2(targetPos.x, startY));
                    block.transform.localScale = Vector3.one;

                    // 아래 블록(i=0)이 먼저 떨어지도록 딜레이: 위 블록일수록 딜레이가 큼
                    float heightDelay = i * 0.03f;

                    anims.Add(new FallAnimation
                    {
                        block = block,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = colDelay + heightDelay + Random.Range(0f, 0.02f),
                        gravityMult = Random.Range(0.88f, 1.12f),
                        maxSpeedMult = Random.Range(0.85f, 1.15f),
                    });
                    totalCount++;
                }
            }

            foreach (var anim in anims)
                StartCoroutine(AnimateFall(anim, () => completedCount++));
            float dropWaitTime = 0f;
            while (completedCount < totalCount && dropWaitTime < 10f)
            {
                dropWaitTime += Time.deltaTime;
                yield return null;
            }


            isProcessing = false;
            OnCascadeComplete?.Invoke();

        }

public void TriggerBigBang()
        {
            if (isProcessing) return;
            StartCoroutine(BigBangCoroutine());
        }

        private IEnumerator BigBangCoroutine()
        {
            isProcessing = true;
            EnsureSlotsCached();
            OnBigBang?.Invoke();

            if (hexGrid == null) { isProcessing = false; yield break; }

            foreach (var block in hexGrid.GetAllBlocks())
                if (block.Data != null && block.Data.gemType != GemType.None)
                    StartCoroutine(AnimateRemove(block));

            yield return new WaitForSeconds(removeAnimationDuration + 0.02f);

            foreach (var block in hexGrid.GetAllBlocks())
            {
                block.ClearData();
                RestoreBlockToSlot(block);
            }

            int completedCount = 0;
            int totalCount = 0;
            List<FallAnimation> anims = new List<FallAnimation>();

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
                float colDelay = Random.Range(0f, 0.06f);

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    Vector2 targetPos = slotPositions[block];

                    GemType randomGem = GemTypeHelper.GetRandom();
                    block.SetBlockData(new BlockData(randomGem));
                    block.transform.localScale = Vector3.one;

                    float startY = topY + 150f + (column.Count - i) * 60f;
                    SetBlockAnchoredPosition(block, new Vector2(targetPos.x, startY));

                    float heightDelay = (column.Count - i) * 0.03f;

                    anims.Add(new FallAnimation
                    {
                        block = block,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = colDelay + heightDelay + Random.Range(0f, 0.02f),
                        gravityMult = Random.Range(0.88f, 1.12f),
                        maxSpeedMult = Random.Range(0.85f, 1.15f),
                    });
                    totalCount++;
                }
            }

            foreach (var anim in anims)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            float bangWaitTime = 0f;
            while (completedCount < totalCount && bangWaitTime < 10f)
            {
                bangWaitTime += Time.deltaTime;
                yield return null;
            }

            isProcessing = false;
            OnCascadeComplete?.Invoke();
        }

        // ============================================================
        // Data
        // ============================================================

        private struct FallAnimation
        {
            public HexBlock block;
            public float startY;
            public float targetY;
            public float delay;
            public float gravityMult;
            public float maxSpeedMult;
            public bool isNewBlock;
        }
    

/// <summary>
        /// Spawn pop-in animation for newly created blocks after fall.
        /// Scales from 0.5x -> 1.15x overshoot -> 1.0x with EaseOutBack.
        /// Includes a white glow overlay that fades out.
        /// </summary>
        private IEnumerator SpawnPopAnimation(HexBlock block)
        {
            if (block == null) yield break;

            float duration = VisualConstants.SpawnDuration;

            // Create glow overlay
            GameObject glow = new GameObject("SpawnGlow");
            glow.transform.SetParent(block.transform, false);
            glow.transform.localPosition = Vector3.zero;

            var glowImg = glow.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(1f, 1f, 1f, VisualConstants.SpawnGlowAlpha);

            RectTransform glowRt = glow.GetComponent<RectTransform>();
            glowRt.sizeDelta = new Vector2(50f, 50f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutBack: 0.5 -> overshoot 1.15 -> settle 1.0
                float eased = VisualConstants.EaseOutBack(t);
                float scale = Mathf.Lerp(VisualConstants.SpawnStartScale, 1f, eased);
                if (block != null)
                    block.transform.localScale = Vector3.one * scale;

                // Glow fade out
                if (glowImg != null)
                    glowImg.color = new Color(1f, 1f, 1f, VisualConstants.SpawnGlowAlpha * (1f - t));

                yield return null;
            }

            if (block != null)
                block.transform.localScale = Vector3.one;
            if (glow != null)
                Destroy(glow);
        }

/// <summary>
        /// 매칭 처리 인라인 (isProcessing 관리 없음, cascade 호출 없음)
        /// highlight → 특수블록 생성 → 일반블록 삭제 → 기존 특수블록 발동
        /// pendingSpecials가 있으면 함께 동시 발동
        /// </summary>
        private IEnumerator ProcessMatchesInline(List<MatchingSystem.MatchGroup> matches, List<HexBlock> pendingSpecials)
        {
            if (matches == null || matches.Count == 0)
            {
                // 매칭 없이 pending만 있는 경우: pending 발동만 처리
                if (pendingSpecials != null && pendingSpecials.Count > 0)
                {
                    List<Coroutine> pendingCos = new List<Coroutine>();
                    foreach (var sp in pendingSpecials)
                    {
                        if (sp != null && sp.Data != null && sp.Data.specialType != SpecialBlockType.None)
                        {
                            sp.SetMatched(false);
                            pendingCos.Add(StartCoroutine(ActivateSpecialAndWaitLocal(sp)));
                        }
                    }
                    foreach (var co in pendingCos) yield return co;
                }
                yield break;
            }

            // 1. 매칭 하이라이트 + 펄스 애니메이션
            List<HexBlock> allMatchedBlocks = new List<HexBlock>();
            Vector3 matchCenter = Vector3.zero;
            int centerCount = 0;

            foreach (var match in matches)
            {
                foreach (var block in match.blocks)
                {
                    if (block != null)
                    {
                        block.SetMatched(true);
                        if (!allMatchedBlocks.Contains(block))
                        {
                            allMatchedBlocks.Add(block);
                            matchCenter += block.transform.position;
                            centerCount++;
                        }
                    }
                }
            }
            if (centerCount > 0) matchCenter /= centerCount;

            // 중심에서 가까운 순서대로 시차 펄스
            allMatchedBlocks.Sort((a, b) =>
            {
                float dA = Vector3.Distance(a.transform.position, matchCenter);
                float dB = Vector3.Distance(b.transform.position, matchCenter);
                return dA.CompareTo(dB);
            });
            for (int i = 0; i < allMatchedBlocks.Count; i++)
            {
                StartCoroutine(MatchPulse(allMatchedBlocks[i], i * VisualConstants.MatchPulseStagger));
            }

            yield return new WaitForSeconds(matchHighlightDuration);

            // 2. 특수 블록 생성
            HashSet<HexBlock> newlyCreatedSpecials = new HashSet<HexBlock>();

            foreach (var match in matches)
            {
                if (match.createdSpecialType != SpecialBlockType.None && match.specialSpawnBlock != null)
                {
                    yield return StartCoroutine(SpecialBlockMergeAnimation(
                        match.blocks, match.specialSpawnBlock, match.createdSpecialType,
                        match.drillDirection, match.gemType));
                    newlyCreatedSpecials.Add(match.specialSpawnBlock);
                }
            }

            // 3. 블록 분류: 일반 블록 삭제, 기존 특수 블록 발동
            HashSet<HexBlock> blocksToRemove = new HashSet<HexBlock>();
            List<HexBlock> matchSpecialBlocks = new List<HexBlock>();

            foreach (var match in matches)
            {
                foreach (var block in match.blocks)
                {
                    if (block == null || block.Data == null) continue;

                    if (block.Data.specialType != SpecialBlockType.None)
                    {
                        if (newlyCreatedSpecials.Contains(block)) continue;
                        if (!matchSpecialBlocks.Contains(block))
                            matchSpecialBlocks.Add(block);
                    }
                    else
                    {
                        blocksToRemove.Add(block);
                    }
                }
            }

            // 4. 제거될 블록의 평균 위치 계산 (점수 팝업용)
            Vector3 avgPosition = Vector3.zero;
            int posCount = 0;
            foreach (var block in blocksToRemove)
            {
                if (block != null)
                {
                    avgPosition += block.transform.position;
                    posCount++;
                }
            }
            if (posCount > 0) avgPosition /= posCount;

            // 5. 삭제 애니메이션 (일반 블록만)
            foreach (var block in blocksToRemove)
                StartCoroutine(AnimateRemove(block));

            yield return new WaitForSeconds(removeAnimationDuration + 0.02f);

            // 6. 일반 블록 데이터 클리어
            foreach (var block in blocksToRemove)
            {
                if (block != null)
                {
                    block.ClearData();
                    block.SetMatched(false);
                    RestoreBlockToSlot(block);
                    block.transform.localScale = Vector3.one;
                }
            }

            OnBlocksRemoved?.Invoke(blocksToRemove.Count, currentCascadeDepth, avgPosition);

            // 6. 매칭 특수블록 + pending 특수블록 동시 발동
            List<HexBlock> allSpecialsToActivate = new List<HexBlock>();

            foreach (var sp in matchSpecialBlocks)
            {
                if (sp != null && sp.Data != null)
                {
                    sp.SetMatched(false);
                    allSpecialsToActivate.Add(sp);
                }
            }

            if (pendingSpecials != null)
            {
                foreach (var sp in pendingSpecials)
                {
                    if (sp != null && sp.Data != null && !allSpecialsToActivate.Contains(sp))
                    {
                        Debug.Log($"[BRS] Pending special simultaneous: {sp.Coord} type={sp.Data.specialType}");
                        allSpecialsToActivate.Add(sp);
                    }
                }
            }

            if (allSpecialsToActivate.Count > 0)
            {
                Debug.Log($"[BRS] Activating {allSpecialsToActivate.Count} specials (match+pending)");
                List<Coroutine> activationCoroutines = new List<Coroutine>();
                foreach (var specialBlock in allSpecialsToActivate)
                {
                    if (specialBlock == null || specialBlock.Data == null) continue;
                    Debug.Log($"[BRS] Activate: {specialBlock.Data.specialType} at {specialBlock.Coord}");
                    activationCoroutines.Add(StartCoroutine(ActivateSpecialAndWaitLocal(specialBlock)));
                }
                foreach (var co in activationCoroutines)
                    yield return co;
            }
        }
}
}
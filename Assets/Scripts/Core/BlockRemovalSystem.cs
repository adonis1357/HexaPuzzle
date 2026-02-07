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

        [Header("Animation Settings")]
        [SerializeField] private float matchHighlightDuration = 0.15f;
        [SerializeField] private float removeAnimationDuration = 0.2f;
        [SerializeField] private float cascadeDelay = 0.1f;

        [Header("Fall Physics")]
        [SerializeField] private float gravity = 2500f;
        [SerializeField] private float maxFallSpeed = 1500f;
        [SerializeField] private float bounceRatio = 0.3f;
        [SerializeField] private float bounceThreshold = 50f;
        

        private bool isProcessing = false;

        // �� ����� ���� ���� ��ġ (���� ���� �� �ѹ� ĳ��)
        private Dictionary<HexBlock, Vector2> slotPositions = new Dictionary<HexBlock, Vector2>();
        private Dictionary<int, List<HexBlock>> columnCache = null;
        private bool slotsCached = false;

        public event System.Action<int> OnBlocksRemoved;
        public event System.Action OnCascadeComplete;
        public event System.Action OnBigBang;

        public bool IsProcessing => isProcessing;

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

private IEnumerator ProcessMatchesCoroutine(List<MatchingSystem.MatchGroup> matches)
        {
            isProcessing = true;
            EnsureSlotsCached();

            // 1. 매칭 하이라이트
            foreach (var match in matches)
                foreach (var block in match.blocks)
                    if (block != null) block.SetMatched(true);

            yield return new WaitForSeconds(matchHighlightDuration);

            // 2. 드릴 생성 처리 (합체 애니메이션 먼저)
            // 이번 턴에 새로 생성된 드릴 블록 추적
            HashSet<HexBlock> newlyCreatedDrills = new HashSet<HexBlock>();

            foreach (var match in matches)
            {
                if (match.createsDrill && match.drillSpawnBlock != null && drillSystem != null)
                {
                    yield return StartCoroutine(DrillMergeAnimation(match.blocks, match.drillSpawnBlock, match.drillDirection, match.gemType));
                    newlyCreatedDrills.Add(match.drillSpawnBlock);
                }
            }

            // 3. 블록 분류: 일반 블록은 삭제, 기존 특수 블록은 능력 발동
            HashSet<HexBlock> blocksToRemove = new HashSet<HexBlock>();
            List<HexBlock> specialBlocksToActivate = new List<HexBlock>();

            foreach (var match in matches)
            {
                foreach (var block in match.blocks)
                {
                    if (block == null || block.Data == null) continue;

                    if (block.Data.specialType != SpecialBlockType.None)
                    {
                        // 이번 턴에 새로 생성된 드릴은 자동 실행하지 않음
                        if (newlyCreatedDrills.Contains(block))
                            continue;

                        if (!specialBlocksToActivate.Contains(block))
                            specialBlocksToActivate.Add(block);
                    }
                    else
                    {
                        blocksToRemove.Add(block);
                    }
                }
            }

            // 4. 삭제 애니메이션 (일반 블록만)
            foreach (var block in blocksToRemove)
                StartCoroutine(AnimateRemove(block));

            yield return new WaitForSeconds(removeAnimationDuration + 0.02f);

            // 5. 일반 블록 데이터 클리어
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

            OnBlocksRemoved?.Invoke(blocksToRemove.Count);

            // 6. 기존 특수 블록 능력 발동 (이미 존재하던 특수 블록만)
            foreach (var specialBlock in specialBlocksToActivate)
            {
                if (specialBlock == null || specialBlock.Data == null) continue;
                specialBlock.SetMatched(false);

                switch (specialBlock.Data.specialType)
                {
                    case SpecialBlockType.Drill:
                        if (drillSystem != null)
                        {
                            Debug.Log($"[BlockRemovalSystem] Auto-activating existing Drill at {specialBlock.Coord}");
                            drillSystem.ActivateDrill(specialBlock);
                            yield return new WaitForSeconds(0.3f);
                            while (drillSystem.IsDrilling)
                                yield return null;
                        }
                        break;
                }
            }

            // 7. 낙하 처리
            yield return StartCoroutine(ProcessFalling());

            // 8. 연쇄 매칭
            yield return new WaitForSeconds(cascadeDelay);

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
        /// �帱 ���� �ִϸ��̼� - 4�� ����� �߾����� �������� ��� + ���� ����Ʈ
        /// </summary>
private IEnumerator DrillMergeAnimation(List<HexBlock> blocks, HexBlock spawnBlock, DrillDirection direction, GemType gemType)
        {
            if (blocks == null || spawnBlock == null) yield break;

            Vector3 targetPos = spawnBlock.transform.position;
            float mergeDuration = 0.45f;

            // 각 블록의 시작 위치와 스케일 저장
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

            // 전기 이펙트 시작 (블록 간 번개선)
            List<GameObject> electricObjects = new List<GameObject>();
            foreach (var block in blocks)
            {
                if (block != null)
                {
                    var obj = CreateElectricArcObject(block.transform);
                    electricObjects.Add(obj);
                }
            }

            // 블록 간 번개선 연결 이펙트
            List<GameObject> arcLines = new List<GameObject>();
            for (int i = 0; i < blocks.Count; i++)
            {
                for (int j = i + 1; j < blocks.Count; j++)
                {
                    if (blocks[i] != null && blocks[j] != null)
                    {
                        var arc = CreateLightningArc(blocks[i].transform, blocks[j].transform);
                        arcLines.Add(arc);
                    }
                }
            }

            // 스파크 파티클 시작
            List<Coroutine> sparkCoroutines = new List<Coroutine>();
            foreach (var block in blocks)
            {
                if (block != null)
                    sparkCoroutines.Add(StartCoroutine(SpawnSparks(block.transform.position, mergeDuration)));
            }

            // 합체 애니메이션
            float elapsed = 0f;
            while (elapsed < mergeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / mergeDuration;
                float easeT = 1f - Mathf.Pow(1f - t, 3f); // ease out cubic

                foreach (var kvp in startPositions)
                {
                    HexBlock block = kvp.Key;
                    if (block != null)
                    {
                        // 곡선 경로 이동 (약간의 호 형태)
                        Vector3 startPos = kvp.Value;
                        Vector3 midPoint = (startPos + targetPos) / 2f + Vector3.up * 15f * (1f - easeT);
                        Vector3 currentPos;
                        if (easeT < 0.5f)
                            currentPos = Vector3.Lerp(startPos, midPoint, easeT * 2f);
                        else
                            currentPos = Vector3.Lerp(midPoint, targetPos, (easeT - 0.5f) * 2f);
                        block.transform.position = currentPos;

                        // 스케일 감소 (펄스 효과 포함)
                        float pulse = 1f + 0.05f * Mathf.Sin(t * Mathf.PI * 6f);
                        float shrink = 1f - easeT * 0.9f;
                        block.transform.localScale = startScales[block] * shrink * pulse;

                        // 회전 효과
                        float rotZ = easeT * 180f * (block.GetInstanceID() % 2 == 0 ? 1f : -1f);
                        block.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                    }
                }

                // 전기 이펙트 업데이트
                UpdateElectricArcs(electricObjects, t);
                UpdateLightningArcs(arcLines, t);

                // 스폰 블록 펄스
                float spawnPulse = 1f + 0.08f * Mathf.Sin(t * Mathf.PI * 8f);
                spawnBlock.transform.localScale = Vector3.one * spawnPulse;

                yield return null;
            }

            // 전기 이펙트 정리
            foreach (var obj in electricObjects)
                if (obj != null) Destroy(obj);
            foreach (var arc in arcLines)
                if (arc != null) Destroy(arc);

            // 합체 완료 - 회전 리셋
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

            // 드릴 생성 (스폰 블록에)
            drillSystem.CreateDrillBlock(spawnBlock, direction, gemType);

            // 드릴 생성 임팩트 이펙트
            StartCoroutine(DrillSpawnImpact(spawnBlock));

            yield return new WaitForSeconds(0.15f);
        }

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
        private IEnumerator DrillSpawnImpact(HexBlock block)
        {
            if (block == null) yield break;

            // 1) 밝은 플래시
            GameObject flashObj = new GameObject("DrillImpactFlash");
            flashObj.transform.SetParent(block.transform, false);
            flashObj.transform.localPosition = Vector3.zero;

            var flashImage = flashObj.AddComponent<UnityEngine.UI.Image>();
            flashImage.color = new Color(0.8f, 0.9f, 1f, 1f);
            flashImage.raycastTarget = false;

            RectTransform flashRt = flashObj.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(30f, 30f);

            // 2) 충격파 링
            GameObject ringObj = new GameObject("DrillImpactRing");
            ringObj.transform.SetParent(block.transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            var ringImage = ringObj.AddComponent<UnityEngine.UI.Image>();
            ringImage.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            ringImage.raycastTarget = false;

            RectTransform ringRt = ringObj.GetComponent<RectTransform>();
            ringRt.sizeDelta = new Vector2(10f, 10f);

            // 스폰 블록 스케일 펀치
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

                // 플래시: 빠르게 확장 후 페이드
                float flashScale = 1f + t * 6f;
                flashRt.sizeDelta = new Vector2(30f * flashScale, 30f * flashScale);
                flashImage.color = new Color(0.8f, 0.9f, 1f, (1f - t) * 0.8f);

                // 충격파 링: 확장
                float ringScale = 1f + t * 8f;
                ringRt.sizeDelta = new Vector2(10f * ringScale, 10f * ringScale);
                ringImage.color = new Color(0.5f, 0.8f, 1f, (1f - t) * 0.5f);

                // 블록 스케일 펀치
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

            float elapsed = 0f;
            while (elapsed < removeAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / removeAnimationDuration;
                float scale;
                if (t < 0.2f)
                    scale = 1f + (t / 0.2f) * 0.1f;
                else
                    scale = 1.1f * (1f - (t - 0.2f) / 0.8f);
                block.transform.localScale = Vector3.one * Mathf.Max(0, scale);
                yield return null;
            }

            block.transform.localScale = Vector3.zero;
            // ClearData�� ���⼭ ȣ������ ���� - ProcessMatchesCoroutine���� �ϰ� ó��
        }

        // ============================================================
        // ���� ó��
        // ============================================================

        private IEnumerator ProcessFalling()
        {
            if (hexGrid == null || columnCache == null) yield break;

            List<FallAnimation> allAnimations = new List<FallAnimation>();

            // ���� ���� ���� (������ �̼��ϰ� �ٸ� ������ ���� ����)
            Dictionary<int, float> columnBaseDelay = new Dictionary<int, float>();
            foreach (var key in columnCache.Keys)
                columnBaseDelay[key] = Random.Range(0f, 0.04f);

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float colDelay = columnBaseDelay[kvp.Key];

                // 1. ������ �ִ� ��� ���� (�Ʒ�����)
                List<BlockData> dataList = new List<BlockData>();
                List<int> sourceSlots = new List<int>();

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    if (block.Data != null && block.Data.gemType != GemType.None)
                    {
                        dataList.Add(block.Data.Clone());
                        sourceSlots.Add(i);
                    }
                }

                int emptyCount = column.Count - dataList.Count;
                if (emptyCount == 0) continue;

                // 2. ��� ����� ���� ��ġ�� �����ϰ� Ŭ����
                for (int i = 0; i < column.Count; i++)
                {
                    column[i].HideVisuals();
                    RestoreBlockToSlot(column[i]);
                }

                // 3. ���� �����͸� �Ʒ����� ä���, ���� �ִϸ��̼� �غ�
                for (int i = 0; i < dataList.Count; i++)
                {
                    int targetSlot = i;
                    int sourceSlot = sourceSlots[i];
                    HexBlock targetBlock = column[targetSlot];

                    targetBlock.SetBlockData(dataList[i]);
                    targetBlock.transform.localScale = Vector3.one;

                    if (sourceSlot != targetSlot)
                    {
                        Vector2 startPos = slotPositions[column[sourceSlot]];
                        SetBlockAnchoredPosition(targetBlock, startPos);

                        // ���� ����ϼ��� �� �ʰ� ��� (�Ʒ��� ������ �������Ƿ�)
                        int fallDistance = sourceSlot - targetSlot;
                        float heightDelay = (sourceSlot - targetSlot) * 0.025f;
                        float jitter = Random.Range(0f, 0.02f);

                        allAnimations.Add(new FallAnimation
                        {
                            block = targetBlock,
                            startY = startPos.y,
                            targetY = slotPositions[targetBlock].y,
                            delay = colDelay + heightDelay + jitter,
                            gravityMult = Random.Range(0.92f, 1.08f),
                            maxSpeedMult = Random.Range(0.90f, 1.10f),
                        });
                    }
                }

                // 4. �� ��� ���� (������ ������)
                float topY = slotPositions[column[column.Count - 1]].y;
                float spawnOffset = 120f;

                // ���� ����� ��� ������ �� �� ����� ��Ÿ������ �⺻ ����
                float existingFallBase = emptyCount * 0.03f;

                for (int i = 0; i < emptyCount; i++)
                {
                    int targetSlot = dataList.Count + i;
                    HexBlock targetBlock = column[targetSlot];
                    Vector2 targetPos = slotPositions[targetBlock];

                    GemType randomGem = (GemType)Random.Range(1, 6);
                    BlockData newData = new BlockData(randomGem);

                    float startY = topY + spawnOffset + (i * 80f);

                    targetBlock.SetBlockData(newData);
                    targetBlock.transform.localScale = Vector3.one;
                    SetBlockAnchoredPosition(targetBlock, new Vector2(targetPos.x, startY));

                    // �� ���: ������� �ణ�� �ʰ� + ���� ����
                    float newDelay = colDelay + existingFallBase + i * 0.04f + Random.Range(0f, 0.025f);

                    allAnimations.Add(new FallAnimation
                    {
                        block = targetBlock,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = newDelay,
                        gravityMult = Random.Range(0.90f, 1.10f),
                        maxSpeedMult = Random.Range(0.88f, 1.12f),
                    });
                }
            }

            if (allAnimations.Count == 0)
            {
                FillEmptyBlocksWithAnimation();
                yield break;
            }

            int completedCount = 0;
            int totalCount = allAnimations.Count;

            foreach (var anim in allAnimations)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            while (completedCount < totalCount)
                yield return null;

            FillEmptyBlocksWithAnimation();
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
            Vector2 slotPos = slotPositions[block];

            float currentY = anim.startY;
            float velocity = 0f;
            float targetY = anim.targetY;
            float blockGravity = gravity * anim.gravityMult;
            float blockMaxSpeed = maxFallSpeed * anim.maxSpeedMult;

            int bounceCount = 0;
            int maxBounces = 2;

            while (true)
            {
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
                        StartCoroutine(SquashEffect(block));
                    }
                    else
                    {
                        SetBlockAnchoredPosition(block, slotPos);
                        block.transform.localScale = Vector3.one;
                        onComplete?.Invoke();
                        yield break;
                    }
                }

                SetBlockAnchoredPosition(block, new Vector2(slotPos.x, currentY));
                yield return null;
            }
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
            if (hexGrid == null) return;

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
                float colDelay = Random.Range(0f, 0.03f);
                int newBlockIndex = 0;

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    RestoreBlockToSlot(block);

                    if (block.Data == null || block.Data.gemType == GemType.None)
                    {
                        GemType randomGem = (GemType)Random.Range(1, 6);
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
        }

        // ============================================================
        // BigBang
        // ============================================================

        /// <summary>
        /// 낙하만 처리 (드릴 파괴 후 호출)
        /// </summary>
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

                    GemType randomGem = (GemType)Random.Range(1, 6);
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

            while (completedCount < totalCount)
                yield return null;

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
            public float gravityMult;   // ���� �߷� ���� (�ڿ������� ����)
            public float maxSpeedMult;  // ���� �ִ�ӵ� ����
        }
    }
}
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
        [SerializeField] private float staggerDelay = 0.03f;

        private bool isProcessing = false;

        // 각 블록의 고정 슬롯 위치 (게임 시작 시 한번 캐시)
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
        /// 슬롯 위치와 열 캐시를 한번만 생성 (또는 갱신)
        /// 블록의 원래 위치를 저장하므로 낙하 애니메이션 중에도 원래 위치를 알 수 있음
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

            // Y좌표 오름차순 정렬 (아래→위)
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
        // 매칭 처리
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

            // 2. 드릴 생성
            foreach (var match in matches)
            {
                if (match.createsDrill && match.drillSpawnBlock != null && drillSystem != null)
                    drillSystem.CreateDrillBlock(match.drillSpawnBlock, match.drillDirection, match.gemType);
            }

            // 3. 제거할 블록 수집
            HashSet<HexBlock> blocksToRemove = new HashSet<HexBlock>();
            foreach (var match in matches)
                foreach (var block in match.blocks)
                    if (block != null && block.Data != null && block.Data.specialType != SpecialBlockType.Drill)
                        blocksToRemove.Add(block);

            // 4. 제거 애니메이션 (ClearData는 여기서 하지 않음)
            foreach (var block in blocksToRemove)
                StartCoroutine(AnimateRemove(block));

            yield return new WaitForSeconds(removeAnimationDuration + 0.02f);

            // 5. 데이터 제거 및 위치 복원
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

            // 6. 낙하 처리 (모든 블록 위치 복원 후)
            yield return StartCoroutine(ProcessFalling());

            // 7. 연쇄 매칭
            yield return new WaitForSeconds(cascadeDelay);

            if (matchingSystem != null)
            {
                var newMatches = matchingSystem.FindMatches();
                if (newMatches.Count > 0)
                {
                    yield return StartCoroutine(ProcessMatchesCoroutine(newMatches));
                    yield break; // 재귀 호출이 isProcessing을 관리
                }
            }

            isProcessing = false;
            OnCascadeComplete?.Invoke();
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
            // ClearData는 여기서 호출하지 않음 - ProcessMatchesCoroutine에서 일괄 처리
        }

        // ============================================================
        // 낙하 처리
        // ============================================================

        private IEnumerator ProcessFalling()
        {
            if (hexGrid == null || columnCache == null) yield break;

            List<FallAnimation> allAnimations = new List<FallAnimation>();

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;

                // 1. 데이터 있는 블록 수집 (아래부터)
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

                // 2. 모든 블록을 슬롯 위치로 복원하고 클리어
                for (int i = 0; i < column.Count; i++)
                {
                    column[i].HideVisuals();
                    RestoreBlockToSlot(column[i]);
                }

                // 3. 기존 데이터를 아래부터 채우고, 낙하 애니메이션 준비
                for (int i = 0; i < dataList.Count; i++)
                {
                    int targetSlot = i;
                    int sourceSlot = sourceSlots[i];
                    HexBlock targetBlock = column[targetSlot];

                    // 데이터 설정
                    targetBlock.SetBlockData(dataList[i]);
                    targetBlock.transform.localScale = Vector3.one;

                    if (sourceSlot != targetSlot)
                    {
                        // 시작 위치로 즉시 이동 (원래 소스 위치에서 출발)
                        Vector2 startPos = slotPositions[column[sourceSlot]];
                        SetBlockAnchoredPosition(targetBlock, startPos);

                        allAnimations.Add(new FallAnimation
                        {
                            block = targetBlock,
                            startY = startPos.y,
                            targetY = slotPositions[targetBlock].y,
                            delay = (column.Count - sourceSlot) * staggerDelay,
                        });
                    }
                    // sourceSlot == targetSlot이면 제자리 → 이미 올바른 위치
                }

                // 4. 새 블록 생성 (위에서 떨어짐)
                float topY = slotPositions[column[column.Count - 1]].y;
                float spawnOffset = 120f;

                for (int i = 0; i < emptyCount; i++)
                {
                    int targetSlot = dataList.Count + i;
                    HexBlock targetBlock = column[targetSlot];
                    Vector2 targetPos = slotPositions[targetBlock];

                    GemType randomGem = (GemType)Random.Range(1, 6);
                    BlockData newData = new BlockData(randomGem);

                    float startY = topY + spawnOffset + (i * 80f);

                    // 데이터 설정 + 화면 위 시작 위치
                    targetBlock.SetBlockData(newData);
                    targetBlock.transform.localScale = Vector3.one;
                    SetBlockAnchoredPosition(targetBlock, new Vector2(targetPos.x, startY));

                    allAnimations.Add(new FallAnimation
                    {
                        block = targetBlock,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = (emptyCount - i - 1) * staggerDelay + dataList.Count * staggerDelay,
                    });
                }
            }

            if (allAnimations.Count == 0)
            {
                // 낙하할 것이 없어도 빈 블록 체크
                FillEmptyBlocksWithAnimation();
                yield break;
            }

            // 모든 낙하 애니메이션 동시 시작 및 완료 대기
            int completedCount = 0;
            int totalCount = allAnimations.Count;

            foreach (var anim in allAnimations)
            {
                StartCoroutine(AnimateFall(anim, () => completedCount++));
            }

            // 모든 애니메이션 완료 대기
            while (completedCount < totalCount)
                yield return null;

            // 최종 검증 - 빈 블록에도 낙하 애니메이션 적용
            FillEmptyBlocksWithAnimation();
        }

        /// <summary>
        /// 물리 기반 낙하 애니메이션
        /// 블록은 이미 데이터가 설정되고 시작 위치에 배치된 상태
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

            int bounceCount = 0;
            int maxBounces = 2;

            while (true)
            {
                velocity -= gravity * Time.deltaTime;
                velocity = Mathf.Max(velocity, -maxFallSpeed);
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
                        // 낙하 완료 - 정확한 슬롯 위치로 고정
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
        /// 빈 블록이 있으면 낙하 애니메이션으로 채움 (갑자기 생기는 현상 방지)
        /// </summary>
        private void FillEmptyBlocksWithAnimation()
        {
            if (hexGrid == null) return;

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
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

                        // 위에서 떨어지는 애니메이션
                        Vector2 slotPos = slotPositions[block];
                        float startY = topY + 120f + newBlockIndex * 80f;
                        SetBlockAnchoredPosition(block, new Vector2(slotPos.x, startY));

                        StartCoroutine(AnimateFall(new FallAnimation
                        {
                            block = block,
                            startY = startY,
                            targetY = slotPos.y,
                            delay = newBlockIndex * staggerDelay,
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

            // 모든 블록에 낙하 애니메이션
            int completedCount = 0;
            int totalCount = 0;
            List<FallAnimation> anims = new List<FallAnimation>();

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    Vector2 targetPos = slotPositions[block];

                    GemType randomGem = (GemType)Random.Range(1, 6);
                    block.SetBlockData(new BlockData(randomGem));
                    block.transform.localScale = Vector3.one;

                    float startY = topY + 150f + (column.Count - i) * 60f;
                    SetBlockAnchoredPosition(block, new Vector2(targetPos.x, startY));

                    anims.Add(new FallAnimation
                    {
                        block = block,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = (column.Count - i) * staggerDelay * 0.5f,
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
        }
    }
}
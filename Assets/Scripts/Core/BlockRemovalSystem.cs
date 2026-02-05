using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 블록 제거 및 낙하 시스템
    /// 현실적인 중력 낙하 애니메이션 포함
    /// </summary>
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

        private Dictionary<int, List<HexBlock>> columnCache = null;
        private Dictionary<HexBlock, Vector2> originalPositions = new Dictionary<HexBlock, Vector2>();

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

        private void BuildColumnCache()
        {
            if (hexGrid == null) return;

            columnCache = new Dictionary<int, List<HexBlock>>();
            originalPositions.Clear();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                Vector2 pos = GetBlockAnchoredPosition(block);
                originalPositions[block] = pos;

                int colKey = Mathf.RoundToInt(pos.x);

                if (!columnCache.ContainsKey(colKey))
                    columnCache[colKey] = new List<HexBlock>();

                columnCache[colKey].Add(block);
            }

            foreach (var key in columnCache.Keys.ToList())
            {
                columnCache[key] = columnCache[key]
                    .OrderBy(b => GetBlockAnchoredPosition(b).y)
                    .ToList();
            }
        }

        private Vector2 GetBlockAnchoredPosition(HexBlock block)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null)
                return rt.anchoredPosition;
            return block.transform.localPosition;
        }

        private void SetBlockAnchoredPosition(HexBlock block, Vector2 pos)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = pos;
            else
                block.transform.localPosition = new Vector3(pos.x, pos.y, 0);
        }

        private void RestoreBlockPosition(HexBlock block)
        {
            if (block == null) return;
            if (originalPositions.TryGetValue(block, out Vector2 origPos))
            {
                SetBlockAnchoredPosition(block, origPos);
            }
            block.transform.localScale = Vector3.one;
        }

        public void ProcessMatches(List<MatchingSystem.MatchGroup> matches)
        {
            if (isProcessing || matches == null || matches.Count == 0) return;
            StartCoroutine(ProcessMatchesCoroutine(matches));
        }

        private IEnumerator ProcessMatchesCoroutine(List<MatchingSystem.MatchGroup> matches)
        {
            isProcessing = true;
            BuildColumnCache();

            // 1. 매칭 하이라이트
            foreach (var match in matches)
            {
                foreach (var block in match.blocks)
                {
                    if (block != null)
                        block.SetMatched(true);
                }
            }
            yield return new WaitForSeconds(matchHighlightDuration);

            // 2. 드릴 생성
            foreach (var match in matches)
            {
                if (match.createsDrill && match.drillSpawnBlock != null && drillSystem != null)
                {
                    drillSystem.CreateDrillBlock(match.drillSpawnBlock, match.drillDirection, match.gemType);
                }
            }

            // 3. 제거할 블록 수집
            HashSet<HexBlock> blocksToRemove = new HashSet<HexBlock>();
            foreach (var match in matches)
            {
                foreach (var block in match.blocks)
                {
                    if (block != null && block.Data != null && block.Data.specialType != SpecialBlockType.Drill)
                    {
                        blocksToRemove.Add(block);
                    }
                }
            }

            // 4. 제거 애니메이션
            foreach (var block in blocksToRemove)
            {
                StartCoroutine(AnimateRemove(block));
            }
            yield return new WaitForSeconds(removeAnimationDuration);

            // 5. 데이터 제거 및 위치 복원
            foreach (var block in blocksToRemove)
            {
                if (block != null)
                {
                    block.ClearData();
                    block.SetMatched(false);
                    RestoreBlockPosition(block);
                }
            }

            OnBlocksRemoved?.Invoke(blocksToRemove.Count);

            // 6. 현실적인 낙하 처리
            yield return StartCoroutine(ProcessFallingWithPhysics());

            // 7. 연쇄 매칭
            yield return new WaitForSeconds(cascadeDelay);

            if (matchingSystem != null)
            {
                var newMatches = matchingSystem.FindMatches();
                if (newMatches.Count > 0)
                {
                    yield return StartCoroutine(ProcessMatchesCoroutine(newMatches));
                }
            }

            isProcessing = false;
            OnCascadeComplete?.Invoke();
        }

        private IEnumerator AnimateRemove(HexBlock block)
        {
            if (block == null) yield break;

            Vector3 startScale = Vector3.one;
            float elapsed = 0f;

            while (elapsed < removeAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / removeAnimationDuration;
                float scale = 1f - t;
                if (t < 0.2f)
                    scale = 1f + (t / 0.2f) * 0.1f;
                else
                    scale = 1.1f * (1f - (t - 0.2f) / 0.8f);

                block.transform.localScale = startScale * Mathf.Max(0, scale);
                yield return null;
            }

            block.transform.localScale = Vector3.zero;
            block.ClearData();
        }

        /// <summary>
        /// 현실적인 물리 기반 낙하 처리 (깜빡임 없음)
        /// 핵심: 블록을 숨기지 않고, 시작 위치로 먼저 이동시킨 후 데이터 교체
        /// </summary>
        private IEnumerator ProcessFallingWithPhysics()
        {
            if (hexGrid == null || columnCache == null) yield break;

            List<FallData> allFallData = new List<FallData>();

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;

                // 1. 데이터가 있는 블록 수집
                List<BlockData> existingData = new List<BlockData>();
                List<int> sourceIndices = new List<int>();

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    if (block.Data != null && block.Data.gemType != GemType.None)
                    {
                        existingData.Add(block.Data.Clone());
                        sourceIndices.Add(i);
                    }
                }

                int emptyCount = column.Count - existingData.Count;
                if (emptyCount == 0) continue;

                // 2. 낙하가 필요한 블록 처리 (깜빡임 방지)
                //    - 먼저 원본 위치에서 데이터를 클리어하고
                //    - 타겟 블록에 데이터를 설정하면서 동시에 시작 위치로 이동

                // 2a. 제자리 블록은 그대로 유지
                HashSet<int> occupiedTargets = new HashSet<int>();
                for (int i = 0; i < existingData.Count; i++)
                {
                    if (sourceIndices[i] == i)
                    {
                        // 제자리 - 아무것도 안 함
                        occupiedTargets.Add(i);
                    }
                }

                // 2b. 낙하할 블록의 원본 위치 클리어 (타겟과 겹치지 않는 것만)
                for (int i = 0; i < existingData.Count; i++)
                {
                    int sourceIdx = sourceIndices[i];
                    int targetIdx = i;

                    if (sourceIdx != targetIdx && !occupiedTargets.Contains(sourceIdx))
                    {
                        column[sourceIdx].ClearData();
                        RestoreBlockPosition(column[sourceIdx]);
                    }
                }

                // 2c. 빈 슬롯(위쪽)도 클리어
                for (int i = existingData.Count; i < column.Count; i++)
                {
                    if (!occupiedTargets.Contains(i))
                    {
                        column[i].ClearData();
                        RestoreBlockPosition(column[i]);
                    }
                }

                // 3. 낙하 애니메이션 데이터 생성
                for (int i = 0; i < existingData.Count; i++)
                {
                    int targetIdx = i;
                    int sourceIdx = sourceIndices[i];

                    if (sourceIdx != targetIdx)
                    {
                        HexBlock targetBlock = column[targetIdx];
                        Vector2 startPos = originalPositions[column[sourceIdx]];
                        Vector2 targetPos = originalPositions[targetBlock];

                        // 타겟 블록에 데이터 설정 + 시작 위치로 즉시 이동 (깜빡임 없음)
                        targetBlock.SetBlockData(existingData[i]);
                        targetBlock.transform.localScale = Vector3.one;
                        SetBlockAnchoredPosition(targetBlock, startPos);

                        allFallData.Add(new FallData
                        {
                            block = targetBlock,
                            startY = startPos.y,
                            targetY = targetPos.y,
                            delay = (column.Count - sourceIdx) * staggerDelay,
                        });
                    }
                }

                // 4. 새 블록 생성
                float topY = originalPositions[column[column.Count - 1]].y;
                float spawnOffset = 100f;

                for (int i = 0; i < emptyCount; i++)
                {
                    int targetIdx = existingData.Count + i;
                    HexBlock targetBlock = column[targetIdx];
                    Vector2 targetPos = originalPositions[targetBlock];

                    GemType randomGem = (GemType)Random.Range(1, 6);
                    BlockData newData = new BlockData(randomGem);

                    float startY = topY + spawnOffset + (i * 80f);

                    // 데이터 설정 + 시작 위치로 즉시 이동 (깜빡임 없음)
                    targetBlock.SetBlockData(newData);
                    targetBlock.transform.localScale = Vector3.one;
                    SetBlockAnchoredPosition(targetBlock, new Vector2(targetPos.x, startY));

                    allFallData.Add(new FallData
                    {
                        block = targetBlock,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = (emptyCount - i - 1) * staggerDelay + existingData.Count * staggerDelay,
                    });
                }
            }

            if (allFallData.Count == 0) yield break;

            // 모든 낙하 애니메이션 시작
            List<Coroutine> coroutines = new List<Coroutine>();
            foreach (var fallData in allFallData)
            {
                coroutines.Add(StartCoroutine(AnimateFallWithPhysics(fallData)));
            }

            foreach (var co in coroutines)
            {
                yield return co;
            }

            VerifyAllBlocksHaveData();
        }

        /// <summary>
        /// 물리 기반 낙하 애니메이션 - 중력 가속 + 바운스
        /// 블록은 이미 데이터가 설정되고 시작 위치에 있음 (깜빡임 없음)
        /// </summary>
        private IEnumerator AnimateFallWithPhysics(FallData fallData)
        {
            if (fallData.block == null) yield break;

            if (fallData.delay > 0)
                yield return new WaitForSeconds(fallData.delay);

            HexBlock block = fallData.block;
            Vector2 originalPos = originalPositions[block];

            float currentY = fallData.startY;
            float velocity = 0f;
            float targetY = fallData.targetY;

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
                        SetBlockAnchoredPosition(block, new Vector2(originalPos.x, targetY));
                        block.transform.localScale = Vector3.one;
                        yield break;
                    }
                }

                SetBlockAnchoredPosition(block, new Vector2(originalPos.x, currentY));
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

        private void VerifyAllBlocksHaveData()
        {
            int filledCount = 0;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                RestoreBlockPosition(block);

                if (block.Data == null || block.Data.gemType == GemType.None)
                {
                    GemType randomGem = (GemType)Random.Range(1, 6);
                    block.SetBlockData(new BlockData(randomGem));
                    filledCount++;
                }
            }

            if (filledCount > 0)
            {
                Debug.LogWarning($"[BlockRemovalSystem] Force filled {filledCount} empty blocks");
            }
        }

        public void TriggerBigBang()
        {
            if (isProcessing) return;
            StartCoroutine(BigBangCoroutine());
        }

        private IEnumerator BigBangCoroutine()
        {
            isProcessing = true;
            BuildColumnCache();
            OnBigBang?.Invoke();

            if (hexGrid == null)
            {
                isProcessing = false;
                yield break;
            }

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block.Data != null && block.Data.gemType != GemType.None)
                    StartCoroutine(AnimateRemove(block));
            }

            yield return new WaitForSeconds(removeAnimationDuration);

            foreach (var block in hexGrid.GetAllBlocks())
            {
                block.ClearData();
                RestoreBlockPosition(block);
            }

            yield return StartCoroutine(BigBangFallAnimation());

            isProcessing = false;
            OnCascadeComplete?.Invoke();
        }

        private IEnumerator BigBangFallAnimation()
        {
            List<FallData> allFallData = new List<FallData>();

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = originalPositions[column[column.Count - 1]].y;

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    Vector2 targetPos = originalPositions[block];

                    GemType randomGem = (GemType)Random.Range(1, 6);
                    BlockData newData = new BlockData(randomGem);

                    float startY = topY + 150f + (column.Count - i) * 60f;

                    block.SetBlockData(newData);
                    block.transform.localScale = Vector3.one;
                    SetBlockAnchoredPosition(block, new Vector2(targetPos.x, startY));

                    allFallData.Add(new FallData
                    {
                        block = block,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = (column.Count - i) * staggerDelay * 0.5f,
                    });
                }
            }

            List<Coroutine> coroutines = new List<Coroutine>();
            foreach (var fallData in allFallData)
            {
                coroutines.Add(StartCoroutine(AnimateFallWithPhysics(fallData)));
            }

            foreach (var co in coroutines)
            {
                yield return co;
            }

            VerifyAllBlocksHaveData();
        }

        private struct FallData
        {
            public HexBlock block;
            public float startY;
            public float targetY;
            public float delay;
        }
    }
}
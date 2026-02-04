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
        [SerializeField] private float gravity = 2500f;           // 중력 가속도 (픽셀/초²)
        [SerializeField] private float maxFallSpeed = 1500f;      // 최대 낙하 속도
        [SerializeField] private float bounceRatio = 0.3f;        // 바운스 비율 (0 = 바운스 없음)
        [SerializeField] private float bounceThreshold = 50f;     // 바운스 발생 최소 속도
        [SerializeField] private float staggerDelay = 0.03f;      // 블록 간 낙하 시작 딜레이

        private bool isProcessing = false;

        // 열 캐시: 화면 X좌표 기준으로 그룹화된 블록들
        private Dictionary<int, List<HexBlock>> columnCache = null;
        // 각 블록의 원래 위치 저장
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

        /// <summary>
        /// 열 캐시 및 원래 위치 저장
        /// </summary>
        private void BuildColumnCache()
        {
            if (hexGrid == null) return;

            columnCache = new Dictionary<int, List<HexBlock>>();
            originalPositions.Clear();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                Vector2 pos = GetBlockAnchoredPosition(block);
                originalPositions[block] = pos;

                // 화면 X좌표를 정수로 반올림하여 열 키로 사용
                int colKey = Mathf.RoundToInt(pos.x);

                if (!columnCache.ContainsKey(colKey))
                    columnCache[colKey] = new List<HexBlock>();

                columnCache[colKey].Add(block);
            }

            // 각 열을 Y좌표 기준으로 정렬 (아래→위, Y가 작을수록 아래)
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

        /// <summary>
        /// 블록을 원래 위치로 복원
        /// </summary>
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
                // 약간의 팝 효과 추가
                float scale = 1f - t;
                if (t < 0.2f)
                    scale = 1f + (t / 0.2f) * 0.1f; // 처음에 살짝 커짐
                else
                    scale = 1.1f * (1f - (t - 0.2f) / 0.8f);

                block.transform.localScale = startScale * Mathf.Max(0, scale);
                yield return null;
            }

            // 스케일 0으로 설정하고 즉시 데이터 클리어 (잔상 방지)
            block.transform.localScale = Vector3.zero;
            block.ClearData();  // 여기서 바로 클리어하여 잔상 제거
        }

        /// <summary>
        /// 현실적인 물리 기반 낙하 처리
        /// </summary>
        private IEnumerator ProcessFallingWithPhysics()
        {
            if (hexGrid == null || columnCache == null) yield break;

            List<FallData> allFallData = new List<FallData>();

            // 각 열 처리 - 낙하 데이터 준비
            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;

                // 1. 현재 열에서 데이터가 있는 블록들의 데이터 수집
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

                // 2. 모든 블록 데이터 클리어
                for (int i = 0; i < column.Count; i++)
                {
                    column[i].ClearData();
                    RestoreBlockPosition(column[i]);
                }

                // 3. 기존 데이터 낙하 준비
                for (int i = 0; i < existingData.Count; i++)
                {
                    int targetIdx = i;
                    int sourceIdx = sourceIndices[i];

                    HexBlock targetBlock = column[targetIdx];
                    Vector2 targetPos = originalPositions[targetBlock];

                    if (sourceIdx != targetIdx)
                    {
                        // 낙하 필요
                        HexBlock sourceBlock = column[sourceIdx];
                        Vector2 startPos = originalPositions[sourceBlock];

                        allFallData.Add(new FallData
                        {
                            block = targetBlock,
                            data = existingData[i],
                            startY = startPos.y,
                            targetY = targetPos.y,
                            delay = (column.Count - sourceIdx) * staggerDelay,
                            isNewBlock = false
                        });
                    }
                    else
                    {
                        // 제자리 - 바로 데이터 설정
                        targetBlock.SetBlockData(existingData[i]);
                    }
                }

                // 4. 새 블록 생성 및 낙하 준비
                float topY = originalPositions[column[column.Count - 1]].y;
                float spawnOffset = 100f; // 화면 위에서 시작

                for (int i = 0; i < emptyCount; i++)
                {
                    int targetIdx = existingData.Count + i;
                    HexBlock targetBlock = column[targetIdx];
                    Vector2 targetPos = originalPositions[targetBlock];

                    GemType randomGem = (GemType)Random.Range(1, 6);
                    BlockData newData = new BlockData(randomGem);

                    float startY = topY + spawnOffset + (i * 80f);

                    allFallData.Add(new FallData
                    {
                        block = targetBlock,
                        data = newData,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = (emptyCount - i - 1) * staggerDelay + existingData.Count * staggerDelay,
                        isNewBlock = true
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

            // 모든 애니메이션 완료 대기
            foreach (var co in coroutines)
            {
                yield return co;
            }

            // 최종 검증
            VerifyAllBlocksHaveData();
        }

        /// <summary>
        /// 물리 기반 낙하 애니메이션 - 중력 가속 + 바운스
        /// </summary>
        private IEnumerator AnimateFallWithPhysics(FallData fallData)
        {
            if (fallData.block == null) yield break;

            // 딜레이
            if (fallData.delay > 0)
                yield return new WaitForSeconds(fallData.delay);

            HexBlock block = fallData.block;
            Vector2 originalPos = originalPositions[block];

            // 데이터 설정 및 시작 위치로 이동
            block.SetBlockData(fallData.data);
            block.transform.localScale = Vector3.one;

            float currentY = fallData.startY;
            float velocity = 0f;
            float targetY = fallData.targetY;

            // 시작 위치 설정
            SetBlockAnchoredPosition(block, new Vector2(originalPos.x, currentY));

            int bounceCount = 0;
            int maxBounces = 2;

            while (true)
            {
                // 중력 적용
                velocity -= gravity * Time.deltaTime;

                // 최대 속도 제한
                velocity = Mathf.Max(velocity, -maxFallSpeed);

                // 위치 업데이트
                currentY += velocity * Time.deltaTime;

                // 목표 위치 도달 체크
                if (currentY <= targetY)
                {
                    currentY = targetY;

                    // 바운스 처리
                    if (bounceCount < maxBounces && Mathf.Abs(velocity) > bounceThreshold)
                    {
                        velocity = -velocity * bounceRatio;
                        bounceCount++;

                        // 바운스 시 살짝 스쿼시 효과
                        StartCoroutine(SquashEffect(block));
                    }
                    else
                    {
                        // 낙하 완료
                        SetBlockAnchoredPosition(block, new Vector2(originalPos.x, targetY));
                        block.transform.localScale = Vector3.one;
                        yield break;
                    }
                }

                SetBlockAnchoredPosition(block, new Vector2(originalPos.x, currentY));
                yield return null;
            }
        }

        /// <summary>
        /// 착지 시 스쿼시 효과
        /// </summary>
        private IEnumerator SquashEffect(HexBlock block)
        {
            if (block == null) yield break;

            float duration = 0.08f;
            float elapsed = 0f;

            // 스쿼시 (납작해짐)
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
        /// 모든 블록이 데이터를 가지고 있는지 검증 및 강제 채우기
        /// </summary>
        private void VerifyAllBlocksHaveData()
        {
            int filledCount = 0;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                // 위치 복원
                RestoreBlockPosition(block);

                // 데이터 검증
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

            // 모든 블록에 새 데이터 할당 후 위에서 떨어지는 연출
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

                    allFallData.Add(new FallData
                    {
                        block = block,
                        data = newData,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = (column.Count - i) * staggerDelay * 0.5f,
                        isNewBlock = true
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
            public BlockData data;
            public float startY;
            public float targetY;
            public float delay;
            public bool isNewBlock;
        }
    }
}
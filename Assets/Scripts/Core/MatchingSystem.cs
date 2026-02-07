using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 매칭 시스템 - 삼각형 형태 매칭
    /// 3개의 블록이 서로 면과 면이 맞닿아 삼각형을 이루어야 매칭됨
    /// 직선 형태는 매칭되지 않음
    /// </summary>
    public class MatchingSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private DrillBlockSystem drillSystem;

        public event System.Action<List<MatchGroup>> OnMatchFound;

        public class MatchGroup
        {
            public List<HexBlock> blocks = new List<HexBlock>();
            public GemType gemType;
            public int score;
            public bool createsDrill;
            public DrillDirection drillDirection;
            public HexBlock drillSpawnBlock;
        }

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[MatchingSystem] HexGrid auto-found: " + hexGrid.name);
            }

            if (drillSystem == null)
            {
                drillSystem = FindObjectOfType<DrillBlockSystem>();
            }

            // === 방향 매핑 검증 로그 ===
                                }

        /// <summary>
        /// 삼각형 매칭 찾기
        /// </summary>
        public List<MatchGroup> FindMatches()
        {
            List<MatchGroup> allMatches = new List<MatchGroup>();
            HashSet<string> foundTriangles = new HashSet<string>(); // 중복 방지

            if (hexGrid == null)
            {
                Debug.LogWarning("[MatchingSystem] HexGrid is null!");
                return allMatches;
            }

            // 모든 블록에 대해 삼각형 검사
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block.Data == null || block.Data.gemType == GemType.None) continue;
                if (block.Data.specialType == SpecialBlockType.FixedBlock) continue;

                // 이 블록을 포함하는 모든 삼각형 찾기
                var triangles = FindTrianglesContaining(block);

                foreach (var triangle in triangles)
                {
                    // 삼각형 고유 키 생성 (정렬된 좌표)
                    string key = GetTriangleKey(triangle);

                    if (!foundTriangles.Contains(key))
                    {
                        foundTriangles.Add(key);

                        MatchGroup group = new MatchGroup
                        {
                            blocks = triangle,
                            gemType = block.Data.gemType,
                            score = CalculateScore(triangle.Count)
                        };

                        allMatches.Add(group);
                    }
                }
            }

            // 인접한 삼각형들을 병합 (4개 이상 연결 시 드릴 생성 가능)
            allMatches = MergeAdjacentMatches(allMatches);

            if (allMatches.Count > 0)
            {
                OnMatchFound?.Invoke(allMatches);
            }

            return allMatches;
        }

        /// <summary>
        /// 특정 블록을 포함하는 삼각형 찾기
        /// 삼각형 조건: 3개 블록이 모두 서로 이웃이어야 함
        /// </summary>
        private List<List<HexBlock>> FindTrianglesContaining(HexBlock centerBlock)
        {
            List<List<HexBlock>> triangles = new List<List<HexBlock>>();

            GemType targetType = centerBlock.Data.gemType;
            var neighbors = hexGrid.GetNeighbors(centerBlock.Coord);

            // 같은 색상의 이웃만 필터링
            var sameColorNeighbors = neighbors
                .Where(n => n.Data != null &&
                           n.Data.gemType == targetType &&
                           n.Data.specialType != SpecialBlockType.FixedBlock)
                .ToList();

            // 2개의 이웃 조합 검사
            for (int i = 0; i < sameColorNeighbors.Count; i++)
            {
                for (int j = i + 1; j < sameColorNeighbors.Count; j++)
                {
                    HexBlock neighbor1 = sameColorNeighbors[i];
                    HexBlock neighbor2 = sameColorNeighbors[j];

                    // 핵심 조건: neighbor1과 neighbor2도 서로 이웃이어야 삼각형
                    if (AreNeighbors(neighbor1.Coord, neighbor2.Coord))
                    {
                        triangles.Add(new List<HexBlock> { centerBlock, neighbor1, neighbor2 });
                    }
                }
            }

            return triangles;
        }

        /// <summary>
        /// 두 좌표가 이웃인지 확인
        /// </summary>
        private bool AreNeighbors(HexCoord a, HexCoord b)
        {
            return a.DistanceTo(b) == 1;
        }

        /// <summary>
        /// 삼각형 고유 키 생성 (중복 방지용)
        /// </summary>
        private string GetTriangleKey(List<HexBlock> triangle)
        {
            var coords = triangle
                .Select(b => b.Coord)
                .OrderBy(c => c.q)
                .ThenBy(c => c.r)
                .ToList();

            return $"{coords[0].q},{coords[0].r}|{coords[1].q},{coords[1].r}|{coords[2].q},{coords[2].r}";
        }

        /// <summary>
        /// 인접한 삼각형 매칭을 병합
        /// </summary>
        private List<MatchGroup> MergeAdjacentMatches(List<MatchGroup> matches)
        {
            if (matches.Count <= 1) return matches;

            List<MatchGroup> mergedMatches = new List<MatchGroup>();
            HashSet<int> usedIndices = new HashSet<int>();

            for (int i = 0; i < matches.Count; i++)
            {
                if (usedIndices.Contains(i)) continue;

                MatchGroup merged = new MatchGroup
                {
                    blocks = new List<HexBlock>(matches[i].blocks),
                    gemType = matches[i].gemType,
                    score = matches[i].score
                };

                usedIndices.Add(i);

                // 같은 색상의 인접한 매칭 병합
                bool foundMore = true;
                while (foundMore)
                {
                    foundMore = false;

                    for (int j = 0; j < matches.Count; j++)
                    {
                        if (usedIndices.Contains(j)) continue;
                        if (matches[j].gemType != merged.gemType) continue;

                        // 인접한지 확인 (공유 블록이 있는지)
                        bool isAdjacent = matches[j].blocks.Any(b => merged.blocks.Contains(b)) ||
                                         matches[j].blocks.Any(b => merged.blocks.Any(mb => AreNeighbors(b.Coord, mb.Coord)));

                        if (isAdjacent)
                        {
                            // 병합
                            foreach (var block in matches[j].blocks)
                            {
                                if (!merged.blocks.Contains(block))
                                    merged.blocks.Add(block);
                            }
                            usedIndices.Add(j);
                            foundMore = true;
                        }
                    }
                }

                // 점수 재계산
                merged.score = CalculateScore(merged.blocks.Count);

                // 4개 이상이면 드릴 생성 가능 체크
                if (merged.blocks.Count >= 4)
                {
                    CheckForDrillPattern(merged);
                }

                mergedMatches.Add(merged);
            }

            return mergedMatches;
        }

        /// <summary>
        /// 드릴 패턴 체크 - 두 삼각형이 중첩되어 4개 블록이 되는 경우
        /// 공유하는 2개 블록의 방향으로 드릴 방향 결정, 아래쪽 블록에 드릴 생성
        /// </summary>
private void CheckForDrillPattern(MatchGroup group)
        {
            if (group.blocks.Count != 4) return;

            List<HexBlock> sharedBlocks = new List<HexBlock>();
            List<HexBlock> outerBlocks = new List<HexBlock>();

            Dictionary<HexBlock, int> neighborCounts = new Dictionary<HexBlock, int>();
            foreach (var block in group.blocks)
            {
                int count = 0;
                foreach (var other in group.blocks)
                {
                    if (block != other && AreNeighbors(block.Coord, other.Coord))
                        count++;
                }
                neighborCounts[block] = count;
            }

            int maxCount = neighborCounts.Values.Max();
            var candidates = group.blocks.Where(b => neighborCounts[b] == maxCount).ToList();

            if (candidates.Count >= 2)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    for (int j = i + 1; j < candidates.Count; j++)
                    {
                        if (AreNeighbors(candidates[i].Coord, candidates[j].Coord))
                        {
                            sharedBlocks.Add(candidates[i]);
                            sharedBlocks.Add(candidates[j]);
                            goto foundShared;
                        }
                    }
                }
            }
            return;

        foundShared:
            foreach (var block in group.blocks)
            {
                if (!sharedBlocks.Contains(block))
                    outerBlocks.Add(block);
            }

            if (sharedBlocks.Count != 2 || outerBlocks.Count != 2) return;

            // === 방향 결정: 공유 블록 2개의 나열 방향 = 발사 방향 ===
            // (정상 확인된 중앙 패턴: 공유블록 r축 나열 → BackSlash 발사)
            HexCoord sharedDelta = sharedBlocks[1].Coord - sharedBlocks[0].Coord;
            DrillDirection direction = GetDrillDirectionFromSharedDelta(sharedDelta);

            Debug.Log($"[MatchingSystem] Shared=({sharedBlocks[0].Coord.q},{sharedBlocks[0].Coord.r})->({sharedBlocks[1].Coord.q},{sharedBlocks[1].Coord.r}) delta=({sharedDelta.q},{sharedDelta.r}) dir={direction}");

            // === 생성 위치 우선순위 ===
            List<HexBlock> priorityOrder = new List<HexBlock>();

            Vector2 sPos0 = GetBlockScreenPosition(sharedBlocks[0]);
            Vector2 sPos1 = GetBlockScreenPosition(sharedBlocks[1]);
            if (sPos0.y <= sPos1.y)
            {
                priorityOrder.Add(sharedBlocks[0]);
                priorityOrder.Add(sharedBlocks[1]);
            }
            else
            {
                priorityOrder.Add(sharedBlocks[1]);
                priorityOrder.Add(sharedBlocks[0]);
            }

            if (outerBlocks.Count >= 2)
            {
                Vector2 oPos0 = GetBlockScreenPosition(outerBlocks[0]);
                Vector2 oPos1 = GetBlockScreenPosition(outerBlocks[1]);
                if (oPos0.y <= oPos1.y)
                {
                    priorityOrder.Add(outerBlocks[0]);
                    priorityOrder.Add(outerBlocks[1]);
                }
                else
                {
                    priorityOrder.Add(outerBlocks[1]);
                    priorityOrder.Add(outerBlocks[0]);
                }
            }

            HexBlock spawnBlock = null;
            foreach (var block in priorityOrder)
            {
                if (block.Data != null && block.Data.specialType == SpecialBlockType.None)
                {
                    spawnBlock = block;
                    break;
                }
            }

            if (spawnBlock == null) return;

            group.createsDrill = true;
            group.drillDirection = direction;
            group.drillSpawnBlock = spawnBlock;

            Debug.Log($"[MatchingSystem] DRILL: Direction={direction}, Spawn=({spawnBlock.Coord.q},{spawnBlock.Coord.r})");
        }

private DrillDirection GetDrillDirectionFromSharedDelta(HexCoord delta)
        {
            // 공유 블록 나열 방향의 **수직** 방향으로 발사
            // 정규화
            int nq = delta.q == 0 ? 0 : (delta.q > 0 ? 1 : -1);
            int nr = delta.r == 0 ? 0 : (delta.r > 0 ? 1 : -1);

            // DrillBlockSystem.GetDirectionDelta 기준:
            //   Vertical  = (0, ±1)    화면상 ↘↖
            //   Slash     = (±1, ∓ 1)  화면상 ↗↙
            //   BackSlash = (±1, 0)    화면상 →←
            //
            // 공유블록 나열 → 수직 발사:
            //   (0, ±1)   r축 나열 (↘↖) → 수직 = Slash (↗↙)
            //   (±1, ∓ 1) s축 나열 (↗↙) → 수직 = BackSlash (→←)
            //   (±1, 0)   q축 나열 (→←) → 수직 = Vertical (↘↖)

            if (nq == 0 && (nr == 1 || nr == -1))
            {
                Debug.Log($"[MatchingSystem] SharedDelta ({delta.q},{delta.r}) r축 -> Slash");
                return DrillDirection.Slash;
            }
            if ((nq == 1 && nr == -1) || (nq == -1 && nr == 1))
            {
                Debug.Log($"[MatchingSystem] SharedDelta ({delta.q},{delta.r}) s축 -> BackSlash");
                return DrillDirection.BackSlash;
            }
            if ((nq == 1 && nr == 0) || (nq == -1 && nr == 0))
            {
                Debug.Log($"[MatchingSystem] SharedDelta ({delta.q},{delta.r}) q축 -> Vertical");
                return DrillDirection.Vertical;
            }

            Debug.LogWarning($"[MatchingSystem] SharedDelta ({delta.q},{delta.r}) unexpected -> Vertical");
            return DrillDirection.Vertical;
        }

        private Vector2 GetBlockScreenPosition(HexBlock block)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null)
                return rt.anchoredPosition;
            return block.transform.localPosition;
        }

        private int CalculateScore(int blockCount)
        {
            int baseScore = blockCount * 100;
            int bonus = Mathf.Max(0, (blockCount - 3)) * 50;
            return baseScore + bonus;
        }

        public bool IsBlockInMatch(HexBlock block)
        {
            var matches = FindMatches();
            foreach (var match in matches)
            {
                if (match.blocks.Contains(block))
                    return true;
            }
            return false;
        }
    


}
}
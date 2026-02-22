using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
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
            public SpecialBlockType createdSpecialType = SpecialBlockType.None;
            public HexBlock specialSpawnBlock;
            public DrillDirection drillDirection;
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
                drillSystem = FindObjectOfType<DrillBlockSystem>();
        }

public List<MatchGroup> FindMatches()
        {
            List<MatchGroup> allMatches = new List<MatchGroup>();
            HashSet<string> foundTriangles = new HashSet<string>();

            if (hexGrid == null)
            {
                Debug.LogWarning("[MatchingSystem] HexGrid is null!");
                return allMatches;
            }

            // 1) 링(도넛) 매칭 최우선 감지 — X블록 패턴이 다른 매칭보다 우선
            var ringMatches = FindRingMatches();
            HashSet<HexBlock> ringUsedBlocks = new HashSet<HexBlock>();
            foreach (var ring in ringMatches)
                foreach (var block in ring.blocks)
                    ringUsedBlocks.Add(block);

            int totalBlocks = 0;
            int validBlocks = 0;
            int nullDataBlocks = 0;
            int noneGemBlocks = 0;
            int fixedBlocks = 0;

            // 2) 삼각형 매칭 — 링에 사용된 블록 제외
            foreach (var block in hexGrid.GetAllBlocks())
            {
                totalBlocks++;
                if (block.Data == null) { nullDataBlocks++; continue; }
                if (block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) { noneGemBlocks++; continue; }
                if (block.Data.specialType == SpecialBlockType.FixedBlock) { fixedBlocks++; continue; }
                // 색상도둑이 있는 블록은 매칭에서 제외
                if (block.CurrentEnemyType == EnemyType.Chromophage) { noneGemBlocks++; continue; }
                // ChaosOverlord Chromophage 효과: 매칭에서 제외
                if (EnemySystem.Instance != null && EnemySystem.Instance.IsMatchExcluded(block)) { noneGemBlocks++; continue; }
                validBlocks++;

                if (ringUsedBlocks.Contains(block)) continue;

                var triangles = FindTrianglesContaining(block);
                foreach (var triangle in triangles)
                {
                    // 링 블록이 포함된 삼각형은 제외
                    bool containsRingBlock = false;
                    foreach (var tb in triangle)
                    {
                        if (ringUsedBlocks.Contains(tb)) { containsRingBlock = true; break; }
                    }
                    if (containsRingBlock) continue;

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

            Debug.Log($"[MatchingSystem] FindMatches scan: total={totalBlocks}, valid={validBlocks}, nullData={nullDataBlocks}, noneGem={noneGemBlocks}, fixed={fixedBlocks}, triangles={allMatches.Count}, rings={ringMatches.Count}");

            // 3) 삼각형 매칭만 병합 (링은 병합하지 않음)
            allMatches = MergeAdjacentMatches(allMatches);

            // 4) 링 매칭 추가
            allMatches.AddRange(ringMatches);

            if (allMatches.Count > 0)
                OnMatchFound?.Invoke(allMatches);

            return allMatches;
        }

/// <summary>
        /// 링(도넛) 매칭 찾기 - 어떤 좌표의 6방향 이웃이 모두 같은 색일 때
        /// 삼각형 매칭으로는 감지 불가능한 패턴 (중앙이 다른 색일 경우)
        /// </summary>
private List<MatchGroup> FindRingMatches()
        {
            List<MatchGroup> rings = new List<MatchGroup>();
            HashSet<string> foundRings = new HashSet<string>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                HexCoord center = block.Coord;
                var neighbors = hexGrid.GetNeighbors(center);
                if (neighbors.Count < 6) continue;

                if (block.Data == null || block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) continue;
                if (block.Data.specialType == SpecialBlockType.FixedBlock) continue;
                // 색상도둑이 있는 블록은 링 매칭에서 제외
                if (block.CurrentEnemyType == EnemyType.Chromophage) continue;

                GemType ringColor = GemType.None;
                bool validRing = true;
                List<HexBlock> ringBlocks = new List<HexBlock>();

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.Data == null || neighbor.Data.gemType == GemType.None ||
                        neighbor.Data.gemType == GemType.Gray ||
                        neighbor.Data.specialType == SpecialBlockType.FixedBlock)
                    { validRing = false; break; }
                    // 색상도둑이 있는 블록은 링 매칭에서 제외
                    if (neighbor.CurrentEnemyType == EnemyType.Chromophage)
                    { validRing = false; break; }

                    if (ringColor == GemType.None)
                        ringColor = neighbor.Data.gemType;
                    else if (neighbor.Data.gemType != ringColor)
                    { validRing = false; break; }

                    ringBlocks.Add(neighbor);
                }

                if (!validRing || ringBlocks.Count < 6 || ringColor == GemType.None) continue;

                // 중앙과 링 색이 같으면 삼각형 시스템이 처리하므로 스킵
                GemType centerColor = block.Data.gemType;
                if (centerColor == ringColor) continue;

                // 중복 방지
                var sortedCoords = ringBlocks.Select(b => b.Coord).OrderBy(c => c.q).ThenBy(c => c.r).ToList();
                string key = string.Join("|", sortedCoords.Select(c => $"{c.q},{c.r}"));
                if (foundRings.Contains(key)) continue;
                foundRings.Add(key);

                // 링 6개만 삭제, 중앙에 X블록 생성, 중앙 색상 사용
                MatchGroup group = new MatchGroup
                {
                    blocks = ringBlocks,
                    gemType = centerColor,
                    score = CalculateScore(ringBlocks.Count),
                    createdSpecialType = SpecialBlockType.XBlock,
                    specialSpawnBlock = block
                };

                rings.Add(group);
                Debug.Log($"[MatchingSystem] RING X-BLOCK: center=({center}) centerColor={centerColor}, ringColor={ringColor}");
            }
            return rings;
        }


        private List<List<HexBlock>> FindTrianglesContaining(HexBlock centerBlock)
        {
            List<List<HexBlock>> triangles = new List<List<HexBlock>>();
            GemType targetType = centerBlock.Data.gemType;

            // Gray 블록은 매칭 불가
            if (targetType == GemType.Gray) return triangles;

            var neighbors = hexGrid.GetNeighbors(centerBlock.Coord);

            var sameColorNeighbors = neighbors
                .Where(n => n.Data != null &&
                           n.Data.gemType == targetType &&
                           n.Data.gemType != GemType.Gray &&
                           n.Data.specialType != SpecialBlockType.FixedBlock)
                .ToList();

            for (int i = 0; i < sameColorNeighbors.Count; i++)
            {
                for (int j = i + 1; j < sameColorNeighbors.Count; j++)
                {
                    if (AreNeighbors(sameColorNeighbors[i].Coord, sameColorNeighbors[j].Coord))
                        triangles.Add(new List<HexBlock> { centerBlock, sameColorNeighbors[i], sameColorNeighbors[j] });
                }
            }
            return triangles;
        }

        private bool AreNeighbors(HexCoord a, HexCoord b)
        {
            return a.DistanceTo(b) == 1;
        }

        private string GetTriangleKey(List<HexBlock> triangle)
        {
            var coords = triangle.Select(b => b.Coord).OrderBy(c => c.q).ThenBy(c => c.r).ToList();
            return $"{coords[0].q},{coords[0].r}|{coords[1].q},{coords[1].r}|{coords[2].q},{coords[2].r}";
        }

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

                bool foundMore = true;
                while (foundMore)
                {
                    foundMore = false;
                    for (int j = 0; j < matches.Count; j++)
                    {
                        if (usedIndices.Contains(j)) continue;
                        if (matches[j].gemType != merged.gemType) continue;

                        bool isAdjacent = matches[j].blocks.Any(b => merged.blocks.Contains(b)) ||
                                         matches[j].blocks.Any(b => merged.blocks.Any(mb => AreNeighbors(b.Coord, mb.Coord)));
                        if (isAdjacent)
                        {
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

                merged.score = CalculateScore(merged.blocks.Count);

                // 특수 블록 우선순위: 도넛(7+링) > 폭탄(5+) > 드릴(4)
                // 레이저 제거됨 — 6매칭도 폭탄으로 처리
                // 뭉친 형태(compact)만 허용: 중심 블록의 그룹 내 인접 수로 검증
                // 비정형 병합(흩어진 형태)에서는 특수 블록 미생성
                if (merged.blocks.Count >= 7)
                {
                    if (!CheckForDonutPattern(merged))
                        CheckForBombPattern(merged);
                }
                else if (merged.blocks.Count >= 5)
                {
                    CheckForBombPattern(merged);
                }
                else if (merged.blocks.Count >= 4)
                {
                    CheckForDrillPattern(merged);
                }

                mergedMatches.Add(merged);
            }
            return mergedMatches;
        }

        // ============================================================
        // 특수 블록 패턴 체크
        // ============================================================



        private bool CheckForDonutPattern(MatchGroup group)
        {
            if (group.blocks.Count < 7) return false;

            HashSet<HexCoord> groupCoords = new HashSet<HexCoord>();
            Dictionary<HexCoord, HexBlock> coordToBlock = new Dictionary<HexCoord, HexBlock>();
            foreach (var block in group.blocks)
            {
                groupCoords.Add(block.Coord);
                coordToBlock[block.Coord] = block;
            }

            HashSet<HexCoord> candidateCenters = new HashSet<HexCoord>(groupCoords);
            foreach (var coord in groupCoords)
                foreach (var neighbor in coord.GetAllNeighbors())
                    candidateCenters.Add(neighbor);

            HexCoord? bestCenter = null;
            int bestCount = 0;

            foreach (var center in candidateCenters)
            {
                var neighbors = center.GetAllNeighbors();
                bool allInGroup = true;
                int count = 0;
                foreach (var n in neighbors)
                {
                    if (groupCoords.Contains(n)) count++;
                    else { allInGroup = false; break; }
                }
                if (!allInGroup || count < 6) continue;
                if (bestCenter == null || count > bestCount)
                {
                    bestCenter = center;
                    bestCount = count;
                }
            }

            if (bestCenter == null) return false;

            HexBlock spawnBlock = null;
            float lowestY = float.MaxValue;
            foreach (var nc in bestCenter.Value.GetAllNeighbors())
            {
                if (!coordToBlock.ContainsKey(nc)) continue;
                HexBlock ringBlock = coordToBlock[nc];
                if (ringBlock.Data == null || ringBlock.Data.specialType != SpecialBlockType.None) continue;
                Vector2 pos = GetBlockScreenPosition(ringBlock);
                if (pos.y < lowestY) { lowestY = pos.y; spawnBlock = ringBlock; }
            }

            if (spawnBlock == null && groupCoords.Contains(bestCenter.Value))
            {
                HexBlock centerBlock = coordToBlock[bestCenter.Value];
                if (centerBlock.Data != null && centerBlock.Data.specialType == SpecialBlockType.None)
                    spawnBlock = centerBlock;
            }

            if (spawnBlock == null) return false;

            group.createdSpecialType = SpecialBlockType.Rainbow;
            group.specialSpawnBlock = spawnBlock;
            Debug.Log($"[MatchingSystem] DONUT: Count={group.blocks.Count}, Center=({bestCenter.Value}), Spawn=({spawnBlock.Coord})");
            return true;
        }

        private void CheckForBombPattern(MatchGroup group)
        {
            if (group.blocks.Count < 5) return;

            HexBlock centerBlock = null;
            int maxNeighborCount = 0;

            foreach (var block in group.blocks)
            {
                if (block.Data == null || block.Data.specialType != SpecialBlockType.None) continue;

                int neighborCount = 0;
                foreach (var other in group.blocks)
                    if (block != other && AreNeighbors(block.Coord, other.Coord))
                        neighborCount++;

                if (neighborCount > maxNeighborCount)
                {
                    maxNeighborCount = neighborCount;
                    centerBlock = block;
                }
                else if (neighborCount == maxNeighborCount && centerBlock != null)
                {
                    Vector2 pos = GetBlockScreenPosition(block);
                    Vector2 currentPos = GetBlockScreenPosition(centerBlock);
                    if (pos.y < currentPos.y) centerBlock = block;
                }
            }

            if (centerBlock == null) return;

            // 뭉친 형태 검증: 중심 블록이 그룹 내 4개 이상과 인접해야 함
            if (maxNeighborCount < 4)
            {
                Debug.Log($"[MatchingSystem] BOMB REJECTED: Count={group.blocks.Count}, MaxNeighbor={maxNeighborCount} < 4 (비정형)");
                return;
            }

            group.createdSpecialType = SpecialBlockType.Bomb;
            group.specialSpawnBlock = centerBlock;
            Debug.Log($"[MatchingSystem] BOMB: Count={group.blocks.Count}, Center=({centerBlock.Coord}), NeighborCount={maxNeighborCount}");
        }

private void CheckForLaserPattern(MatchGroup group)
        {
            if (group.blocks.Count != 6) return;

            // 레이저: 폭탄과 동일한 중앙 부록 선택 로직 (이웃 수 최대인 블록)
            HexBlock centerBlock = null;
            int maxNeighborCount = 0;

            foreach (var block in group.blocks)
            {
                if (block.Data == null || block.Data.specialType != SpecialBlockType.None) continue;

                int neighborCount = 0;
                foreach (var other in group.blocks)
                    if (block != other && AreNeighbors(block.Coord, other.Coord))
                        neighborCount++;

                if (neighborCount > maxNeighborCount)
                {
                    maxNeighborCount = neighborCount;
                    centerBlock = block;
                }
                else if (neighborCount == maxNeighborCount && centerBlock != null)
                {
                    Vector2 pos = GetBlockScreenPosition(block);
                    Vector2 currentPos = GetBlockScreenPosition(centerBlock);
                    if (pos.y < currentPos.y) centerBlock = block;
                }
            }

            if (centerBlock == null) return;

            // 뭉친 형태 검증: 중심 블록이 그룹 내 5개와 인접해야 함
            if (maxNeighborCount < 5)
            {
                Debug.Log($"[MatchingSystem] LASER REJECTED: Count={group.blocks.Count}, MaxNeighbor={maxNeighborCount} < 5 (비정형)");
                return;
            }

            group.createdSpecialType = SpecialBlockType.Laser;
            group.specialSpawnBlock = centerBlock;
            Debug.Log($"[MatchingSystem] LASER: Count={group.blocks.Count}, Center=({centerBlock.Coord}), NeighborCount={maxNeighborCount}");
        }


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
                    if (block != other && AreNeighbors(block.Coord, other.Coord)) count++;
                neighborCounts[block] = count;
            }

            int maxCount = neighborCounts.Values.Max();
            var candidates = group.blocks.Where(b => neighborCounts[b] == maxCount).ToList();

            if (candidates.Count >= 2)
            {
                for (int i = 0; i < candidates.Count; i++)
                    for (int j = i + 1; j < candidates.Count; j++)
                        if (AreNeighbors(candidates[i].Coord, candidates[j].Coord))
                        {
                            sharedBlocks.Add(candidates[i]);
                            sharedBlocks.Add(candidates[j]);
                            goto foundShared;
                        }
            }
            return;

        foundShared:
            foreach (var block in group.blocks)
                if (!sharedBlocks.Contains(block)) outerBlocks.Add(block);

            if (sharedBlocks.Count != 2 || outerBlocks.Count != 2) return;

            HexCoord sharedDelta = sharedBlocks[1].Coord - sharedBlocks[0].Coord;
            DrillDirection direction = GetDrillDirectionFromShared(sharedDelta);

            List<HexBlock> priorityOrder = new List<HexBlock>();
            Vector2 sPos0 = GetBlockScreenPosition(sharedBlocks[0]);
            Vector2 sPos1 = GetBlockScreenPosition(sharedBlocks[1]);
            if (sPos0.y <= sPos1.y) { priorityOrder.Add(sharedBlocks[0]); priorityOrder.Add(sharedBlocks[1]); }
            else { priorityOrder.Add(sharedBlocks[1]); priorityOrder.Add(sharedBlocks[0]); }

            if (outerBlocks.Count >= 2)
            {
                Vector2 oPos0 = GetBlockScreenPosition(outerBlocks[0]);
                Vector2 oPos1 = GetBlockScreenPosition(outerBlocks[1]);
                if (oPos0.y <= oPos1.y) { priorityOrder.Add(outerBlocks[0]); priorityOrder.Add(outerBlocks[1]); }
                else { priorityOrder.Add(outerBlocks[1]); priorityOrder.Add(outerBlocks[0]); }
            }

            HexBlock spawnBlock = null;
            foreach (var block in priorityOrder)
                if (block.Data != null && block.Data.specialType == SpecialBlockType.None)
                { spawnBlock = block; break; }

            if (spawnBlock == null) return;

            group.createdSpecialType = SpecialBlockType.Drill;
            group.drillDirection = direction;
            group.specialSpawnBlock = spawnBlock;
            Debug.Log($"[MatchingSystem] DRILL: Direction={direction}, Spawn=({spawnBlock.Coord})");
        }

        private DrillDirection GetDrillDirectionFromShared(HexCoord sharedDelta)
        {
            int nq = sharedDelta.q == 0 ? 0 : (sharedDelta.q > 0 ? 1 : -1);
            int nr = sharedDelta.r == 0 ? 0 : (sharedDelta.r > 0 ? 1 : -1);

            if (nq == 0 && (nr == 1 || nr == -1)) return DrillDirection.Vertical;
            if ((nq == 1 && nr == -1) || (nq == -1 && nr == 1)) return DrillDirection.Slash;
            if ((nq == 1 || nq == -1) && nr == 0) return DrillDirection.BackSlash;

            return DrillDirection.Vertical;
        }

        // ============================================================
        // 유틸리티
        // ============================================================

        private Vector2 GetBlockScreenPosition(HexBlock block)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null) return rt.anchoredPosition;
            return block.transform.localPosition;
        }

        private int CalculateScore(int blockCount)
        {
            return blockCount * 100 + Mathf.Max(0, (blockCount - 3)) * 50;
        }

        public bool IsBlockInMatch(HexBlock block)
        {
            var matches = FindMatches();
            foreach (var match in matches)
                if (match.blocks.Contains(block)) return true;
            return false;
        }

        // ============================================================
        // 무한 모드: 매칭 가능 여부 체크
        // ============================================================

        /// <summary>
        /// 보드에 가능한 움직임이 있는지 확인
        /// 모든 삼각형 클러스터를 순회하며 CW/CCW 가상 회전 후 매칭 여부 확인
        /// SetBlockDataSilent로 비주얼 업데이트 없이 데이터만 교체하여 성능 최적화
        /// </summary>
        public bool HasPossibleMoves()
        {
            if (hexGrid == null) return false;

            // 이미 매칭이 있으면 true
            if (HasAnyTriangleMatch()) return true;

            // 모든 유효한 삼각형 클러스터 열거
            var clusters = GetAllClusters();

            foreach (var cluster in clusters)
            {
                // 원본 데이터 참조 저장
                BlockData d0 = cluster[0].Data;
                BlockData d1 = cluster[1].Data;
                BlockData d2 = cluster[2].Data;

                // CW 회전 시뮬레이션 (비주얼 업데이트 없음)
                cluster[0].SetBlockDataSilent(d2);
                cluster[1].SetBlockDataSilent(d0);
                cluster[2].SetBlockDataSilent(d1);

                bool hasCWMatch = HasAnyTriangleMatch();

                // 원복
                cluster[0].SetBlockDataSilent(d0);
                cluster[1].SetBlockDataSilent(d1);
                cluster[2].SetBlockDataSilent(d2);

                if (hasCWMatch) return true;

                // CCW 회전 시뮬레이션
                cluster[0].SetBlockDataSilent(d1);
                cluster[1].SetBlockDataSilent(d2);
                cluster[2].SetBlockDataSilent(d0);

                bool hasCCWMatch = HasAnyTriangleMatch();

                // 원복
                cluster[0].SetBlockDataSilent(d0);
                cluster[1].SetBlockDataSilent(d1);
                cluster[2].SetBlockDataSilent(d2);

                if (hasCCWMatch) return true;
            }

            return false;
        }

        /// <summary>
        /// 삼각형 매칭이 하나라도 있는지 빠르게 확인 (이벤트 미발생, 병합/링 체크 생략)
        /// </summary>
        private bool HasAnyTriangleMatch()
        {
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block.Data == null) continue;
                if (block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) continue;
                if (block.Data.specialType == SpecialBlockType.FixedBlock) continue;

                var triangles = FindTrianglesContaining(block);
                if (triangles.Count > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 모든 유효한 삼각형 클러스터(3블록) 수집
        /// 중복 방지: 좌표 정렬 기반 HashSet
        /// </summary>
        private List<HexBlock[]> GetAllClusters()
        {
            List<HexBlock[]> clusters = new List<HexBlock[]>();
            HashSet<string> seen = new HashSet<string>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;

                var neighbors = hexGrid.GetNeighbors(block.Coord);

                for (int i = 0; i < neighbors.Count; i++)
                {
                    for (int j = i + 1; j < neighbors.Count; j++)
                    {
                        // 이웃 쌍이 서로 인접해야 삼각형
                        if (neighbors[i].Coord.DistanceTo(neighbors[j].Coord) != 1) continue;

                        // 중복 방지 키 생성
                        var coords = new HexCoord[] { block.Coord, neighbors[i].Coord, neighbors[j].Coord };
                        System.Array.Sort(coords, (a, b) => a.q != b.q ? a.q.CompareTo(b.q) : a.r.CompareTo(b.r));
                        string key = $"{coords[0].q},{coords[0].r}|{coords[1].q},{coords[1].r}|{coords[2].q},{coords[2].r}";

                        if (seen.Contains(key)) continue;
                        seen.Add(key);

                        // 회전 불가 블록(FixedBlock) 포함 클러스터 제외
                        if (block.Data.specialType == SpecialBlockType.FixedBlock) continue;
                        if (neighbors[i].Data != null && neighbors[i].Data.specialType == SpecialBlockType.FixedBlock) continue;
                        if (neighbors[j].Data != null && neighbors[j].Data.specialType == SpecialBlockType.FixedBlock) continue;

                        clusters.Add(new HexBlock[] { block, neighbors[i], neighbors[j] });
                    }
                }
            }

            return clusters;
        }
    }
}

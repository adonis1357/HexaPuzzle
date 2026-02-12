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

            int totalBlocks = 0;
            int validBlocks = 0;
            int nullDataBlocks = 0;
            int noneGemBlocks = 0;
            int fixedBlocks = 0;

            // 1) 삼각형 매칭
            foreach (var block in hexGrid.GetAllBlocks())
            {
                totalBlocks++;
                if (block.Data == null) { nullDataBlocks++; continue; }
                if (block.Data.gemType == GemType.None) { noneGemBlocks++; continue; }
                if (block.Data.specialType == SpecialBlockType.FixedBlock) { fixedBlocks++; continue; }
                validBlocks++;

                var triangles = FindTrianglesContaining(block);
                foreach (var triangle in triangles)
                {
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

            Debug.Log($"[MatchingSystem] FindMatches scan: total={totalBlocks}, valid={validBlocks}, nullData={nullDataBlocks}, noneGem={noneGemBlocks}, fixed={fixedBlocks}, triangles={allMatches.Count}");

            // 2) 삼각형 매칭만 병합 (링은 병합하지 않음)
            allMatches = MergeAdjacentMatches(allMatches);

            // 3) 링(도넛) 매칭 - 병합 후 별도로 추가 (링 6개만 삭제, 중앙에 특수블록)
            var ringMatches = FindRingMatches();
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

                if (block.Data == null || block.Data.gemType == GemType.None) continue;
                if (block.Data.specialType == SpecialBlockType.FixedBlock) continue;

                GemType ringColor = GemType.None;
                bool validRing = true;
                List<HexBlock> ringBlocks = new List<HexBlock>();

                foreach (var neighbor in neighbors)
                {
                    if (neighbor.Data == null || neighbor.Data.gemType == GemType.None ||
                        neighbor.Data.specialType == SpecialBlockType.FixedBlock)
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
            var neighbors = hexGrid.GetNeighbors(centerBlock.Coord);

            var sameColorNeighbors = neighbors
                .Where(n => n.Data != null &&
                           n.Data.gemType == targetType &&
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

                // 특수 블록 우선순위: 도넛(7+링) > 레이저(6) > 폭탄(5) > 드릴(4)
                // X블록은 FindRingMatches에서 별도 감지 (6이웃 동색 도넛 패턴)
                if (merged.blocks.Count >= 7)
                {
                    if (!CheckForDonutPattern(merged))
                        CheckForBombPattern(merged);
                }
                else if (merged.blocks.Count == 6)
                {
                    CheckForLaserPattern(merged);
                }
                else if (merged.blocks.Count == 5)
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
    }
}

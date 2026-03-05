using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    public class HexGrid : MonoBehaviour
    {
        [Header("Grid Settings")]
        [SerializeField] private int gridRadius = 5;
        [SerializeField] private float hexSize = 50f;  // 중심에서 꼭지점까지 거리

        [Header("References")]
        [SerializeField] private GameObject hexBlockPrefab;
        [SerializeField] private Transform gridContainer;

        private Dictionary<HexCoord, HexBlock> blocks = new Dictionary<HexCoord, HexBlock>();
        private List<HexCoord> allCoords = new List<HexCoord>();
        private GameObject backgroundGridContainer;

        // Flat-top 육각형 기하학:
        // 너비 (width) = 2 * size
        // 높이 (height) = sqrt(3) * size
        // 수평 간격 = 1.5 * size (너비의 3/4)
        // 수직 간격 = sqrt(3) * size (높이와 동일)

        public float HexSize => hexSize;
        public int BlockCount => blocks.Count;

        private void Awake()
        {
            if (gridContainer == null)
                gridContainer = transform;

            // 에디터 테스트 시스템 자동 추가
            if (gameObject.GetComponent<EditorTestSystem>() == null)
            {
                gameObject.AddComponent<EditorTestSystem>();
            }
        }

        public void InitializeGrid()
        {
            ClearGrid();
            GenerateGridCoordinates();
            CreateBackgroundGrid();
            CreateBlocks();
        }

        private void GenerateGridCoordinates()
        {
            allCoords.Clear();
            allCoords = HexCoord.GetHexesInRadius(new HexCoord(0, 0), gridRadius);
            Debug.Log("Generated " + allCoords.Count + " hex coordinates");
        }

        private void CreateBackgroundGrid()
        {
            if (backgroundGridContainer != null)
                Destroy(backgroundGridContainer);

            backgroundGridContainer = new GameObject("GridBackground");
            backgroundGridContainer.transform.SetParent(gridContainer, false);

            RectTransform bgRT = backgroundGridContainer.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta = Vector2.zero;

            // 블록보다 뒤에 렌더링되도록 첫 번째 자식으로
            backgroundGridContainer.transform.SetAsFirstSibling();

            float hexWidth = 2f * hexSize;
            float hexHeight = Mathf.Sqrt(3f) * hexSize;

            Sprite fillSprite = HexBlock.GetHexFlashSprite();
            Sprite borderSprite = HexBlock.GetHexBorderSprite();

            foreach (var coord in allCoords)
            {
                Vector2 pos = CalculateFlatTopHexPosition(coord);

                // 셀 컨테이너
                GameObject cellObj = new GameObject("BgCell");
                cellObj.transform.SetParent(backgroundGridContainer.transform, false);

                RectTransform cellRT = cellObj.AddComponent<RectTransform>();
                cellRT.anchoredPosition = pos;
                cellRT.sizeDelta = new Vector2(hexWidth, hexHeight);

                // 어두운 배경 (움푹 들어간 바닥)
                Image bgImg = cellObj.AddComponent<Image>();
                bgImg.sprite = fillSprite;
                bgImg.color = new Color(0.03f, 0.02f, 0.06f, 0.22f);
                bgImg.raycastTarget = false;
                bgImg.type = Image.Type.Simple;

                // 밝은 테두리 (빛 받는 가장자리)
                GameObject borderObj = new GameObject("Border");
                borderObj.transform.SetParent(cellObj.transform, false);

                RectTransform borderRT = borderObj.AddComponent<RectTransform>();
                borderRT.anchorMin = Vector2.zero;
                borderRT.anchorMax = Vector2.one;
                borderRT.offsetMin = Vector2.zero;
                borderRT.offsetMax = Vector2.zero;

                Image borderImg = borderObj.AddComponent<Image>();
                borderImg.sprite = borderSprite;
                borderImg.color = new Color(0.95f, 0.92f, 0.90f, 0.30f);
                borderImg.raycastTarget = false;
                borderImg.type = Image.Type.Simple;
            }
        }

        private void CreateBlocks()
        {
            // Flat-top 육각형 크기
            float hexWidth = 2f * hexSize;              // 너비 = 2 * size
            float hexHeight = Mathf.Sqrt(3f) * hexSize; // 높이 = sqrt(3) * size

            foreach (var coord in allCoords)
            {
                Vector2 worldPos = CalculateFlatTopHexPosition(coord);

                GameObject blockObj = Instantiate(hexBlockPrefab, gridContainer);
                blockObj.name = "HexBlock_" + coord.q + "_" + coord.r;

                RectTransform rectTransform = blockObj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = worldPos;
                    rectTransform.sizeDelta = new Vector2(hexWidth, hexHeight);
                }
                else
                {
                    blockObj.transform.localPosition = new Vector3(worldPos.x, worldPos.y, 0);
                }

                HexBlock block = blockObj.GetComponent<HexBlock>();
                if (block == null)
                    block = blockObj.AddComponent<HexBlock>();

                block.Initialize(coord, this);
                blocks[coord] = block;
            }

            Debug.Log($"[HexGrid] Created {blocks.Count} blocks");
        }

        /// <summary>
        /// Flat-top 육각형 위치 계산 - 면과 면이 정확히 맞닿음
        /// </summary>
        private Vector2 CalculateFlatTopHexPosition(HexCoord coord)
        {
            // Flat-top 배치 공식:
            // x = size * 3/2 * q
            // y = size * sqrt(3) * (r + q/2)
            float x = hexSize * 1.5f * coord.q;
            float y = hexSize * Mathf.Sqrt(3f) * (coord.r + coord.q / 2f);

            return new Vector2(x, -y);  // Y 반전
        }

        public void PopulateWithRandomGems()
        {
            foreach (var block in blocks.Values)
            {
                GemType randomGem = GemTypeHelper.GetRandom();
                // Gray 블록 생성 방지
                while (randomGem == GemType.Gray)
                    randomGem = GemTypeHelper.GetRandom();

                BlockData data = new BlockData(randomGem);
                block.SetBlockData(data);
            }
        }

        /// <summary>
        /// 매칭이 발생하지 않도록 랜덤 배치 (각 블록의 이웃과 같은 색이 2개 이상 연속되지 않도록)
        /// </summary>
        public void PopulateWithNoMatches()
        {
            foreach (var block in blocks.Values)
                block.ClearData();

            foreach (var coord in allCoords)
            {
                HexBlock block = blocks[coord];
                HashSet<GemType> forbidden = new HashSet<GemType>();

                // 이 블록의 모든 이웃 중 이미 데이터가 있는 것들
                var neighborCoords = coord.GetAllNeighbors();
                List<HexCoord> filledNeighbors = new List<HexCoord>();
                foreach (var nc in neighborCoords)
                {
                    if (blocks.ContainsKey(nc) && blocks[nc].Data != null && blocks[nc].Data.gemType != GemType.None)
                        filledNeighbors.Add(nc);
                }

                // [삼각형 매칭 방지] 이웃 쌍 중 서로 인접한 쌍을 찾고, 둘 다 같은 색이면 그 색 금지
                for (int i = 0; i < filledNeighbors.Count; i++)
                {
                    for (int j = i + 1; j < filledNeighbors.Count; j++)
                    {
                        HexCoord n1 = filledNeighbors[i];
                        HexCoord n2 = filledNeighbors[j];
                        // n1과 n2가 서로 인접해야 삼각형
                        if (n1.DistanceTo(n2) == 1)
                        {
                            GemType g1 = blocks[n1].Data.gemType;
                            GemType g2 = blocks[n2].Data.gemType;
                            if (g1 == g2)
                                forbidden.Add(g1);
                        }
                    }
                }

                // [링(도넛) 매칭 방지] 이 블록을 중심으로 이웃 6칸이 모두 같은 색이면 그 색 금지
                // (이 블록이 중심이 되어 링 매칭이 완성되는 것을 방지)
                ForbidRingCenter(coord, forbidden);

                // [링(도넛) 매칭 방지] 이 블록이 링의 일부가 되는 경우도 방지
                // 각 이웃을 중심으로, 그 중심의 나머지 이웃들이 모두 같은 색이면
                // 이 블록도 그 색이 되면 링이 완성되므로 금지
                ForbidRingMember(coord, forbidden);

                // 허용된 색 목록 (Gray 제외)
                List<GemType> allowed = new List<GemType>();
                for (int g = 1; g <= GemTypeHelper.ActiveGemTypeCount; g++)
                {
                    GemType gt = (GemType)g;
                    if (!forbidden.Contains(gt) && gt != GemType.Gray) allowed.Add(gt);
                }

                GemType chosen;
                if (allowed.Count > 0)
                    chosen = allowed[Random.Range(0, allowed.Count)];
                else
                    chosen = GemTypeHelper.GetRandom();

                // Gray 방지 (최종 확인)
                while (chosen == GemType.Gray)
                    chosen = GemTypeHelper.GetRandom();

                block.SetBlockData(new BlockData(chosen));
            }

            Debug.Log("[HexGrid] Populated with no-match gems (삼각형+링 매칭 방지)");
        }

        /// <summary>
        /// 특정 좌표의 블록 데이터를 사전 배치 (튜토리얼용).
        /// PopulateWithNoMatches() 이후 호출하여 원하는 좌표만 덮어쓴다.
        /// </summary>
        public void SetPresetBlocks(Dictionary<HexCoord, GemType> presets)
        {
            foreach (var kvp in presets)
            {
                if (blocks.TryGetValue(kvp.Key, out var block))
                {
                    block.SetBlockData(new BlockData(kvp.Value));
                }
            }
            Debug.Log($"[HexGrid] SetPresetBlocks: {presets.Count}개 블록 사전 배치 완료");
        }

        /// <summary>
        /// [링 방지 - 중심] 이 좌표를 중심으로 이웃 6칸이 모두 같은 색이면 그 색 금지.
        /// 아직 배치되지 않은 이웃이 있으면 링이 완성 불가능하므로 무시.
        /// </summary>
        private void ForbidRingCenter(HexCoord center, HashSet<GemType> forbidden)
        {
            var neighborCoords = center.GetAllNeighbors();
            List<GemType> neighborColors = new List<GemType>();

            foreach (var nc in neighborCoords)
            {
                if (!blocks.ContainsKey(nc)) return; // 유효 좌표 아님 → 6칸 미만
                var nb = blocks[nc];
                if (nb.Data == null || nb.Data.gemType == GemType.None) return; // 미배치 → 링 불가
                neighborColors.Add(nb.Data.gemType);
            }

            if (neighborColors.Count < 6) return;

            // 6칸 전부 같은 색인지 확인
            GemType ringColor = neighborColors[0];
            for (int i = 1; i < neighborColors.Count; i++)
            {
                if (neighborColors[i] != ringColor) return; // 다른 색 → 링 불가
            }

            // 링 매칭 조건: 중심 색 ≠ 링 색 → 중심이 어떤 색이든 링 성립
            // 따라서 이 색을 금지하지 않고, 중심을 링 색과 같게 만들면 삼각형으로 처리됨
            // 하지만 중심이 링 색과 다르면 링 매칭 → 링 색을 제외한 모든 색이 위험
            // 가장 안전한 방법: 중심을 링 색과 동일하게 강제 (삼각형은 이미 별도 방지)
            // → forbidden에 링 색 외의 모든 색을 추가하는 대신, 링 색만 허용
            // 단, 삼각형 forbidden과 충돌할 수 있으므로 링 색을 forbidden에서 제거하지 않고
            // 간단히: 링 색이 아닌 모든 활성 색상을 금지
            for (int g = 1; g <= GemTypeHelper.ActiveGemTypeCount; g++)
            {
                GemType gt = (GemType)g;
                if (gt != ringColor && gt != GemType.Gray)
                    forbidden.Add(gt);
            }
        }

        /// <summary>
        /// [링 방지 - 구성원] 각 이웃 center의 나머지 이웃 5칸이 모두 같은 색이면,
        /// 이 블록이 그 색이 되면 링이 완성되므로 그 색 금지.
        /// </summary>
        private void ForbidRingMember(HexCoord current, HashSet<GemType> forbidden)
        {
            // current의 이웃들을 순회 — 각 이웃을 잠재적 "링 중심"으로 취급
            var myNeighbors = current.GetAllNeighbors();
            foreach (var potentialCenter in myNeighbors)
            {
                if (!blocks.ContainsKey(potentialCenter)) continue;

                // potentialCenter의 이웃 6칸 확인 (current 포함)
                var centerNeighbors = potentialCenter.GetAllNeighbors();
                bool hasCurrent = false;
                GemType commonColor = GemType.None;
                bool allSame = true;
                int filledCount = 0;

                foreach (var cn in centerNeighbors)
                {
                    if (cn.Equals(current))
                    {
                        hasCurrent = true;
                        continue; // current는 아직 미배치 → 스킵
                    }

                    if (!blocks.ContainsKey(cn)) { allSame = false; break; }
                    var nb = blocks[cn];
                    if (nb.Data == null || nb.Data.gemType == GemType.None) { allSame = false; break; }

                    if (commonColor == GemType.None)
                        commonColor = nb.Data.gemType;
                    else if (nb.Data.gemType != commonColor)
                    { allSame = false; break; }

                    filledCount++;
                }

                if (!hasCurrent || !allSame || filledCount < 5 || commonColor == GemType.None) continue;

                // potentialCenter의 이웃 5칸이 모두 commonColor
                // current가 commonColor가 되면 6칸 전부 같은 색 → 링 완성
                // 단, 중심(potentialCenter)의 색이 commonColor와 같으면 삼각형으로 처리 (링 아님)
                var centerBlock = blocks[potentialCenter];
                if (centerBlock.Data != null && centerBlock.Data.gemType == commonColor) continue;

                // 중심 색이 다르거나 미배치 → commonColor 금지
                forbidden.Add(commonColor);
            }
        }


        public HexBlock GetBlock(HexCoord coord)
        {
            blocks.TryGetValue(coord, out HexBlock block);
            return block;
        }

        public bool IsValidCoord(HexCoord coord)
        {
            return blocks.ContainsKey(coord);
        }

        public List<HexBlock> GetNeighbors(HexCoord coord)
        {
            List<HexBlock> neighbors = new List<HexBlock>();

            foreach (var neighborCoord in coord.GetAllNeighbors())
            {
                if (blocks.TryGetValue(neighborCoord, out HexBlock neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        /// <summary>
        /// 블록의 UI 위치 가져오기 (RectTransform 사용)
        /// </summary>
        private Vector2 GetBlockPosition(HexBlock block)
        {
            if (block == null) return Vector2.zero;

            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null)
            {
                return rt.anchoredPosition;
            }
            return block.transform.localPosition;
        }

        /// <summary>
        /// 블록이 회전 가능한 상태인지 확인 (데이터 있고, 젬 있고, 이동 가능)
        /// </summary>
        private bool IsBlockRotatable(HexBlock block)
        {
            return block != null && block.Data != null &&
                   block.Data.gemType != GemType.None && block.Data.CanMove();
        }

        public (HexBlock, HexBlock, HexBlock)? GetClusterAtPosition(Vector2 localPos)
        {
            if (blocks.Count == 0)
            {
                return null;
            }

            // 블록 반경 (터치가 이 범위 안에 있어야 유효)
            float maxTouchDistance = hexSize * 1.0f;

            HexBlock closestBlock = null;
            float closestDist = float.MaxValue;

            foreach (var block in blocks.Values)
            {
                // 빈 블록(GemType.None)은 가장 가까운 블록 후보에서 제외
                if (!IsBlockRotatable(block)) continue;

                Vector2 blockPos = GetBlockPosition(block);
                float dist = Vector2.Distance(localPos, blockPos);

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestBlock = block;
                }
            }

            // 가장 가까운 블록이 너무 멀면 null (블록 영역 밖 터치)
            if (closestBlock == null || closestDist > maxTouchDistance)
            {
                return null;
            }

            var neighbors = GetNeighbors(closestBlock.Coord);

            if (neighbors.Count < 2)
            {
                return null;
            }

            // 삼각형 중심까지의 최대 허용 거리
            float maxTriangleDist = hexSize * 1.2f;

            (HexBlock, HexBlock, HexBlock)? bestTriangle = null;
            float bestDist = float.MaxValue;

            // 중심 블록 기준 삼각형 찾기
            for (int i = 0; i < neighbors.Count; i++)
            {
                for (int j = i + 1; j < neighbors.Count; j++)
                {
                    HexBlock n1 = neighbors[i];
                    HexBlock n2 = neighbors[j];

                    // 빈 블록이 포함된 삼각형은 제외
                    if (!IsBlockRotatable(n1) || !IsBlockRotatable(n2)) continue;

                    if (!AreNeighbors(n1.Coord, n2.Coord)) continue;

                    Vector2 center = (
                        GetBlockPosition(closestBlock) +
                        GetBlockPosition(n1) +
                        GetBlockPosition(n2)
                    ) / 3f;

                    float dist = Vector2.Distance(localPos, center);

                    if (dist < bestDist && dist < maxTriangleDist)
                    {
                        bestDist = dist;
                        bestTriangle = (closestBlock, n1, n2);
                    }
                }
            }

            // 이웃 블록 기준 삼각형도 확인
            foreach (var neighbor in neighbors)
            {
                // 빈 이웃은 스킵
                if (!IsBlockRotatable(neighbor)) continue;

                var neighborNeighbors = GetNeighbors(neighbor.Coord);

                for (int i = 0; i < neighborNeighbors.Count; i++)
                {
                    for (int j = i + 1; j < neighborNeighbors.Count; j++)
                    {
                        HexBlock n1 = neighborNeighbors[i];
                        HexBlock n2 = neighborNeighbors[j];

                        bool hasClosest = (n1 == closestBlock || n2 == closestBlock);
                        if (!hasClosest) continue;

                        // 빈 블록이 포함된 삼각형은 제외
                        if (!IsBlockRotatable(n1) || !IsBlockRotatable(n2)) continue;

                        if (!AreNeighbors(n1.Coord, n2.Coord)) continue;
                        if (!AreNeighbors(neighbor.Coord, n1.Coord)) continue;
                        if (!AreNeighbors(neighbor.Coord, n2.Coord)) continue;

                        Vector2 center = (
                            GetBlockPosition(neighbor) +
                            GetBlockPosition(n1) +
                            GetBlockPosition(n2)
                        ) / 3f;

                        float dist = Vector2.Distance(localPos, center);

                        if (dist < bestDist && dist < maxTriangleDist)
                        {
                            bestDist = dist;
                            bestTriangle = (neighbor, n1, n2);
                        }
                    }
                }
            }

            // 최종 검증: 반환 전 삼각형 유효성 재확인
            if (bestTriangle.HasValue)
            {
                var (b1, b2, b3) = bestTriangle.Value;
                bool valid = AreNeighbors(b1.Coord, b2.Coord) &&
                             AreNeighbors(b2.Coord, b3.Coord) &&
                             AreNeighbors(b1.Coord, b3.Coord);
                if (!valid)
                {
                    Debug.LogError($"[HexGrid] INVALID triangle! " +
                        $"({b1.Coord}, {b2.Coord}, {b3.Coord}) " +
                        $"dist: {b1.Coord.DistanceTo(b2.Coord)},{b2.Coord.DistanceTo(b3.Coord)},{b1.Coord.DistanceTo(b3.Coord)}");
                    return null;
                }
            }

            return bestTriangle;
        }

        private bool AreNeighbors(HexCoord a, HexCoord b)
        {
            return a.DistanceTo(b) == 1;
        }

        public void ClearGrid()
        {
            foreach (var block in blocks.Values)
            {
                if (block != null && block.gameObject != null)
                    Destroy(block.gameObject);
            }
            blocks.Clear();
            allCoords.Clear();

            if (backgroundGridContainer != null)
            {
                Destroy(backgroundGridContainer);
                backgroundGridContainer = null;
            }
        }

        public IEnumerable<HexBlock> GetAllBlocks()
        {
            return blocks.Values;
        }

        public List<HexBlock> FindBlocksByType(GemType gemType)
        {
            List<HexBlock> result = new List<HexBlock>();

            foreach (var block in blocks.Values)
            {
                if (block.Data != null && block.Data.gemType == gemType)
                    result.Add(block);
            }

            return result;
        }

        private void OnDestroy()
        {
            ClearGrid();
        }
    }
}
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

                // 이웃 쌍 중 서로 인접한 쌍을 찾고, 둘 다 같은 색이면 그 색 금지
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

            Debug.Log("[HexGrid] Populated with no-match gems");
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
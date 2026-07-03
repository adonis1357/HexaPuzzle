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
        public int GridRadius => gridRadius;
        public int BlockCount => blocks.Count;

        // ============================================================
        // 런타임 그리드 재구성 (Stage 1 7블록 2.5배 모드 등)
        // ============================================================
        /// <summary>
        /// 그리드 반경을 런타임 변경. 적용을 위해서는 InitializeGrid()를 재호출해야 함.
        /// </summary>
        public void SetGridRadius(int radius)
        {
            gridRadius = Mathf.Max(1, radius);
            Debug.Log($"[HexGrid] gridRadius = {gridRadius} 설정됨 (InitializeGrid 호출 필요)");
        }

        /// <summary>
        /// 육각형 크기를 런타임 변경. 적용을 위해서는 InitializeGrid()를 재호출해야 함.
        /// </summary>
        public void SetHexSize(float size)
        {
            hexSize = Mathf.Max(10f, size);
            Debug.Log($"[HexGrid] hexSize = {hexSize} 설정됨 (InitializeGrid 호출 필요)");
        }

        /// <summary>
        /// 특정 q열의 그리드 내 최소 r값 (가장 위쪽 블록의 r좌표).
        /// ★ 실제 존재하는 블록 기반 — Stage 1/2/3처럼 외곽 블록을 제거한 컴팩트 그리드도 정확히 처리.
        /// </summary>
        public int GetTopR(int q)
        {
            int topR = int.MaxValue;
            bool found = false;
            foreach (var kvp in blocks)
            {
                if (kvp.Key.q != q) continue;
                found = true;
                if (kvp.Key.r < topR) topR = kvp.Key.r;
            }
            if (found) return topR;
            // 폴백: 이론적 반경 기반 (호환성 유지)
            return Mathf.Max(-gridRadius, -q - gridRadius);
        }

        /// <summary>
        /// 특정 q열의 그리드 내 최대 r값 (가장 아래쪽 블록의 r좌표).
        /// 컴팩트 그리드 대응.
        /// </summary>
        public int GetBottomR(int q)
        {
            int bottomR = int.MinValue;
            bool found = false;
            foreach (var kvp in blocks)
            {
                if (kvp.Key.q != q) continue;
                found = true;
                if (kvp.Key.r > bottomR) bottomR = kvp.Key.r;
            }
            if (found) return bottomR;
            // 폴백: 이론적 반경 기반
            return Mathf.Min(gridRadius, -q + gridRadius);
        }

        /// <summary>
        /// 특정 q열에 블록이 하나라도 있는지 확인.
        /// 컴팩트 그리드의 외곽 컬럼은 false 반환 → 몬스터 이동·소환 경계 검사 가능.
        /// </summary>
        public bool ColumnHasBlocks(int q)
        {
            foreach (var kvp in blocks)
                if (kvp.Key.q == q) return true;
            return false;
        }

        /// <summary>
        /// 그리드 상단 빈 공간 3줄의 소환 가능 좌표 목록 반환
        /// 고블린 소환 위치로 사용.
        /// ★ 실제 존재하는 블록(blocks 딕셔너리) 기반으로 동작 →
        ///    Stage 1/2처럼 외곽 블록을 제거한 컴팩트 그리드에서도
        ///    필드 최상단 위 2~3칸에 정확히 소환 좌표 생성.
        /// </summary>
        public List<HexCoord> GetExtendedTopCoords()
        {
            var coords = new List<HexCoord>();

            // 각 컬럼의 최상단 r(가장 작은 r) 수집 — 실제 존재하는 블록 기반
            var topRPerColumn = new Dictionary<int, int>();
            foreach (var kvp in blocks)
            {
                int q = kvp.Key.q;
                int r = kvp.Key.r;
                if (!topRPerColumn.ContainsKey(q) || r < topRPerColumn[q])
                    topRPerColumn[q] = r;
            }

            // 각 컬럼별로 최상단 위 3줄을 소환 좌표로 추가
            foreach (var kvp in topRPerColumn)
            {
                int q = kvp.Key;
                int topR = kvp.Value;
                for (int row = 1; row <= 3; row++)
                {
                    coords.Add(new HexCoord(q, topR - row));
                }
            }
            return coords;
        }

        /// <summary>
        /// 해당 좌표가 메인 그리드 범위 내인지 확인
        /// </summary>
        public bool IsInsideGrid(HexCoord coord)
        {
            return blocks.ContainsKey(coord);
        }

        /// <summary>
        /// 블록 필드 + 소환 영역 전체를 포함하는 게임 필드 범위 체크.
        /// 드릴 쿠션 반사, 몬스터 이동 경계 등에서 사용.
        /// ★ 실제 존재하는 블록 기반 — 컴팩트 그리드(Stage 1/2/3)에서도 정확히 동작.
        /// </summary>
        public bool IsInGameField(HexCoord coord)
        {
            if (blocks.ContainsKey(coord)) return true;
            // 해당 컬럼에 블록이 없으면 게임 필드 밖
            if (!ColumnHasBlocks(coord.q)) return false;
            int rMin = GetTopR(coord.q);
            if (coord.r >= rMin - 3 && coord.r < rMin) return true;
            return false;
        }

        /// <summary>
        /// gridContainer의 Transform 반환 (고블린 등 외부 오브젝트 배치용)
        /// </summary>
        public Transform GridContainer => gridContainer;

        private void Awake()
        {
            if (gridContainer == null)
                gridContainer = transform;

            // 에디터 테스트 시스템 자동 추가
            // ★ 에디터/개발 빌드 전용 (감사 H6) — 릴리스 빌드에 치트 패널(블록 설치/몬스터 소환/MP 추가)이
            //   노출되지 않도록 생성 자체를 차단. 모든 참조처는 null 체크/정적 폴백이 있어 안전.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (gameObject.GetComponent<EditorTestSystem>() == null)
            {
                gameObject.AddComponent<EditorTestSystem>();
            }
#endif
        }

        /// <summary>그리드 전체 Y 오프셋 (블록 필드를 아래로 이동)</summary>
        public const float FIELD_Y_OFFSET = -100f;

        public void InitializeGrid()
        {
            // ★ 소환 영역 제한 초기화 (버그 수정): 컴팩트 튜토리얼 스테이지(1~9)가 SetExtendedVisibleArea로
            //   좁힌 columnRadius/visibleRows가 다음 풀-반경 스테이지(10+)로 새어, 경계 밖(q=±5 등)에 소환된
            //   고블린이 무효 셀에 갇혀 영영 움직이지 못하던 버그를 차단. (튜토리얼 스테이지는 InitializeGrid
            //   이후 SetExtendedVisibleArea를 다시 호출하므로 제한이 정상 재적용됨)
            currentExtendedColumnRadius = -1; // 제한 없음(= gridRadius 전체)
            currentExtendedVisibleRows = 3;   // 소환 영역 3줄 전체

            ClearGrid();
            GenerateGridCoordinates();
            CreateBackgroundGrid();
            CreateBlocks();

            // 블록 필드 전체를 아래로 이동 (절대값 설정으로 누적 방지)
            RectTransform containerRt = gridContainer.GetComponent<RectTransform>();
            if (containerRt != null)
            {
                Vector2 pos = containerRt.anchoredPosition;
                containerRt.anchoredPosition = new Vector2(pos.x, FIELD_Y_OFFSET);
            }
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

            // 그리드 상단 3줄 확장 (연한 빈 셀 — 블록 스폰 영역 시각화)
            CreateExtendedTopCells(backgroundGridContainer.transform, fillSprite, borderSprite, hexWidth, hexHeight);
        }

        /// <summary>
        /// 그리드 상단에 3줄의 연한 빈 셀을 추가 생성.
        /// 블록이 떨어져 들어오는 영역을 시각적으로 표시하며,
        /// 위로 갈수록 투명도가 낮아져 자연스럽게 페이드아웃됩니다.
        /// </summary>
        private void CreateExtendedTopCells(Transform parent, Sprite fillSprite, Sprite borderSprite, float hexWidth, float hexHeight)
        {
            const int extendRows = 3;

            // 기존 fill/border의 기본 알파값
            const float baseFillAlpha = 0.22f;
            const float baseBorderAlpha = 0.30f;

            // 확장 셀은 기존 대비 연하게 (35%)
            const float extendAlphaRatio = 0.35f;

            for (int q = -gridRadius; q <= gridRadius; q++)
            {
                // 현재 그리드에서 이 열의 최소 r값 (= 화면 최상단)
                int rMin = Mathf.Max(-gridRadius, -q - gridRadius);

                for (int row = 1; row <= extendRows; row++)
                {
                    int r = rMin - row;
                    Vector2 pos = CalculateFlatTopHexPosition(new HexCoord(q, r));

                    // 행별 페이드: 가까운 행(row=1)이 가장 진하고, 먼 행(row=3)이 가장 연함
                    float fadeMul = 1f - (row - 1) * 0.3f; // 1행=1.0, 2행=0.7, 3행=0.4

                    float fillAlpha = baseFillAlpha * extendAlphaRatio * fadeMul;
                    float borderAlpha = baseBorderAlpha * extendAlphaRatio * fadeMul;

                    // 셀 컨테이너 — 이름에 q/row 메타정보 저장 (Stage 1/2 컴팩트 제한 시 분류용)
                    GameObject cellObj = new GameObject($"BgCellExt_Q{q}_R{row}");
                    cellObj.transform.SetParent(parent, false);

                    RectTransform cellRT = cellObj.AddComponent<RectTransform>();
                    cellRT.anchoredPosition = pos;
                    cellRT.sizeDelta = new Vector2(hexWidth, hexHeight);

                    // 어두운 배경
                    Image bgImg = cellObj.AddComponent<Image>();
                    bgImg.sprite = fillSprite;
                    bgImg.color = new Color(0.03f, 0.02f, 0.06f, fillAlpha);
                    bgImg.raycastTarget = false;
                    bgImg.type = Image.Type.Simple;

                    // 밝은 테두리
                    GameObject borderObj = new GameObject("Border");
                    borderObj.transform.SetParent(cellObj.transform, false);

                    RectTransform borderRT = borderObj.AddComponent<RectTransform>();
                    borderRT.anchorMin = Vector2.zero;
                    borderRT.anchorMax = Vector2.one;
                    borderRT.offsetMin = Vector2.zero;
                    borderRT.offsetMax = Vector2.zero;

                    Image brdImg = borderObj.AddComponent<Image>();
                    brdImg.sprite = borderSprite;
                    brdImg.color = new Color(0.95f, 0.92f, 0.90f, borderAlpha);
                    brdImg.raycastTarget = false;
                    brdImg.type = Image.Type.Simple;
                }
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
        /// 외부 시스템(GoblinSystem 등)에서도 확장 좌표 위치 계산에 사용
        /// </summary>
        public Vector2 CalculateFlatTopHexPosition(HexCoord coord)
        {
            // Flat-top 배치 공식:
            // x = size * 3/2 * q
            // y = size * sqrt(3) * (r + q/2)
            float x = hexSize * 1.5f * coord.q;
            float y = hexSize * Mathf.Sqrt(3f) * (coord.r + coord.q / 2f);

            return new Vector2(x, -y);  // Y 반전
        }

        /// <summary>
        /// 헥스 좌표를 월드 좌표로 변환.
        /// CalculateFlatTopHexPosition(로컬/앵커 좌표)를 gridContainer의 Transform을 통해
        /// 월드 좌표로 변환합니다. 블록이 없는 소환 영역 좌표도 정확한 월드 위치를 반환.
        /// </summary>
        public Vector3 HexToWorldPosition(HexCoord coord)
        {
            Vector2 local = CalculateFlatTopHexPosition(coord);
            return gridContainer.TransformPoint(new Vector3(local.x, local.y, 0f));
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

                // [링타겟 레이저 매칭 방지] 이 블록을 중심으로 이웃 6칸이 모두 같은 색이면 그 색 금지
                // (이 블록이 중심이 되어 링 매칭이 완성되는 것을 방지)
                ForbidRingCenter(coord, forbidden);

                // [링타겟 레이저 매칭 방지] 이 블록이 링의 일부가 되는 경우도 방지
                // 각 이웃을 중심으로, 그 중심의 나머지 이웃들이 모두 같은 색이면
                // 이 블록도 그 색이 되면 링이 완성되므로 금지
                ForbidRingMember(coord, forbidden);

                // 허용된 색 목록 (Gray 제외)
                // ★ AllowedColorsOverride가 설정되어 있으면 그 안에서만 선택 (Stage 1 R/G 전용 등)
                List<GemType> allowed = new List<GemType>();
                if (GemTypeHelper.AllowedColorsOverride != null && GemTypeHelper.AllowedColorsOverride.Length > 0)
                {
                    foreach (var gt in GemTypeHelper.AllowedColorsOverride)
                    {
                        if (gt == GemType.Gray || gt == GemType.None) continue;
                        if (!forbidden.Contains(gt)) allowed.Add(gt);
                    }
                }
                else
                {
                    for (int g = 1; g <= GemTypeHelper.ActiveGemTypeCount; g++)
                    {
                        GemType gt = (GemType)g;
                        if (!forbidden.Contains(gt) && gt != GemType.Gray) allowed.Add(gt);
                    }
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
                block.HideVisuals(); // 낙하 시작 전까지 숨김 (StartDropCoroutine에서 활성화)
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

        /// <summary>
        /// 확장 top 영역(소환 지역) 셀들을 현재 실제 블록의 컬럼별 최상단 바로 위로 재배치.
        /// Stage 2처럼 외곽 블록을 제거해 그리드가 작아진 경우, BgCellExt가 원래 위치(반경 5 기준)에
        /// 머무르면 필드 상단과 4~6칸 거리가 생김 → 이 메서드로 실제 상단에 부착.
        ///
        /// 이름 형식 "BgCellExt_Q{q}_R{row}"에서 q와 row를 파싱.
        /// 새 좌표: (q, actualTopR(q) - row)에 해당하는 worldPos로 anchoredPosition 재설정.
        /// </summary>
        public void RealignExtendedCellsToActualTops()
        {
            if (backgroundGridContainer == null) return;

            // 각 컬럼의 실제 최상단 r 수집 (블록 딕셔너리 기반)
            var topRPerColumn = new Dictionary<int, int>();
            foreach (var kvp in blocks)
            {
                int q = kvp.Key.q;
                int r = kvp.Key.r;
                if (!topRPerColumn.ContainsKey(q) || r < topRPerColumn[q])
                    topRPerColumn[q] = r;
            }

            int realigned = 0;
            for (int i = 0; i < backgroundGridContainer.transform.childCount; i++)
            {
                var child = backgroundGridContainer.transform.GetChild(i);
                if (child == null) continue;
                if (!child.name.StartsWith("BgCellExt_")) continue;

                int q, row;
                if (!ParseBgCellExtName(child.name, out q, out row)) continue;

                if (!topRPerColumn.ContainsKey(q)) continue; // 해당 컬럼에 블록 없음 → 건드리지 않음

                int newR = topRPerColumn[q] - row;
                Vector2 newPos = CalculateFlatTopHexPosition(new HexCoord(q, newR));
                var rt = child as RectTransform ?? child.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = newPos;
                    realigned++;
                }
            }
            Debug.Log($"[HexGrid] BgCellExt 재배치: {realigned}개 셀을 실제 필드 상단 바로 위로 이동");
        }

        /// <summary>
        /// 확장 top 영역(소환 지역)을 지정한 컬럼 반경 + 줄 수로 제한.
        /// 필드를 좁힌 스테이지(예: Stage 2 반경 2)에서 소환 영역도 같은 폭으로 모이도록.
        /// columnRadius: |q| ≤ columnRadius 인 컬럼만 표시 (-1이면 제한 없음)
        /// visibleRows: 1~3, 1이면 row=1만, 2면 row=1+2, 3이면 모두 표시
        ///
        /// ★ BgCellExt 이름 형식: "BgCellExt_Q{q}_R{row}" — 이름에서 q/row 파싱.
        /// </summary>
        public void SetExtendedVisibleArea(int columnRadius, int visibleRows)
        {
            if (backgroundGridContainer == null) return;
            if (visibleRows < 0) visibleRows = 0;
            if (visibleRows > 3) visibleRows = 3;

            // ★ 현재 설정 저장 — GoblinSystem이 이동 경계 검사에 사용
            currentExtendedColumnRadius = columnRadius;
            currentExtendedVisibleRows = visibleRows;

            int kept = 0, hidden = 0;
            for (int i = 0; i < backgroundGridContainer.transform.childCount; i++)
            {
                var child = backgroundGridContainer.transform.GetChild(i);
                if (child == null) continue;
                if (!child.name.StartsWith("BgCellExt_")) continue;

                // 이름 파싱: "BgCellExt_Q{q}_R{row}"
                int q, row;
                if (!ParseBgCellExtName(child.name, out q, out row)) continue;

                bool qOk = (columnRadius < 0) || (Mathf.Abs(q) <= columnRadius);
                bool rOk = (row >= 1) && (row <= visibleRows);
                bool show = qOk && rOk;
                child.gameObject.SetActive(show);
                if (show) kept++; else hidden++;
            }
            Debug.Log($"[HexGrid] 확장 셀 제한 (columnRadius={columnRadius}, rows={visibleRows}): 유지={kept}, 숨김={hidden}");
        }

        // ============================================================
        // 소환 영역 현재 설정 (GoblinSystem 이동 경계 검사용)
        // ============================================================

        /// <summary>현재 소환 영역 컬럼 반경 (-1 = 제한 없음, gridRadius 사용)</summary>
        private int currentExtendedColumnRadius = -1;
        /// <summary>현재 소환 영역 가시 줄 수 (기본 3)</summary>
        private int currentExtendedVisibleRows = 3;

        /// <summary>현재 소환 영역의 컬럼 반경 (-1이면 gridRadius 그대로)</summary>
        public int CurrentExtendedColumnRadius => currentExtendedColumnRadius;
        /// <summary>현재 소환 영역의 가시 줄 수 (1~3)</summary>
        public int CurrentExtendedVisibleRows => currentExtendedVisibleRows;

        /// <summary>
        /// 좌표가 몬스터 이동에 유효한 칸인지 (블록 필드 + 가시 소환 영역).
        /// - 블록 필드: 해당 컬럼에 블록이 존재하고 r이 [topR, bottomR] 범위
        /// - 소환 영역: 컬럼이 columnRadius 내부 + r이 topR-1 ~ topR-visibleRows 범위
        /// </summary>
        public bool IsValidMonsterCell(HexCoord coord)
        {
            // 컬럼 자체에 블록이 없으면 무효
            if (!ColumnHasBlocks(coord.q)) return false;

            int topR = GetTopR(coord.q);
            int bottomR = GetBottomR(coord.q);

            // 블록 필드 내부 (topR ≤ r ≤ bottomR)
            if (coord.r >= topR && coord.r <= bottomR) return true;

            // 소환 영역 (topR보다 위 — r이 더 작음)
            // row = topR - coord.r (1이면 가장 가까운 윗줄, visibleRows까지 허용)
            int rowFromTop = topR - coord.r;
            if (rowFromTop < 1 || rowFromTop > currentExtendedVisibleRows) return false;

            // 컬럼 반경 제한 (소환 영역 columnRadius)
            if (currentExtendedColumnRadius >= 0 && Mathf.Abs(coord.q) > currentExtendedColumnRadius)
                return false;

            return true;
        }

        /// <summary>
        /// "BgCellExt_Q{q}_R{row}" 이름 파싱.
        /// </summary>
        private static bool ParseBgCellExtName(string name, out int q, out int row)
        {
            q = 0; row = 0;
            try
            {
                // "BgCellExt_Q{q}_R{row}"
                int qIdx = name.IndexOf("_Q");
                int rIdx = name.IndexOf("_R", qIdx + 2);
                if (qIdx < 0 || rIdx < 0) return false;
                string qStr = name.Substring(qIdx + 2, rIdx - (qIdx + 2));
                string rStr = name.Substring(rIdx + 2);
                if (!int.TryParse(qStr, out q)) return false;
                if (!int.TryParse(rStr, out row)) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 확장 top 영역(소환 지역)의 가시 줄 수를 조절.
        /// - 3: 기본 (모든 확장 셀 표시)
        /// - 2: 가장 위 1줄 숨김 (Stage 2용)
        /// - 1: 가장 위 2줄 숨김
        /// - 0: 확장 셀 전부 숨김
        /// InitializeGrid 호출 후 언제든 런타임 조절 가능. 다음 InitializeGrid 시 원복.
        /// </summary>
        public void SetExtendedRowsVisible(int visibleRows)
        {
            if (backgroundGridContainer == null) return;
            if (visibleRows < 0) visibleRows = 0;
            if (visibleRows > 3) visibleRows = 3;

            // BgCellExt들을 수집해 Y로 정렬
            var extCells = new List<RectTransform>();
            for (int i = 0; i < backgroundGridContainer.transform.childCount; i++)
            {
                var child = backgroundGridContainer.transform.GetChild(i);
                if (child != null && child.name == "BgCellExt")
                {
                    var rt = child as RectTransform ?? child.GetComponent<RectTransform>();
                    if (rt != null) extCells.Add(rt);
                }
            }

            if (extCells.Count == 0) return;

            // 각 컬럼마다 y가 작을수록(하단) row 1, 클수록(상단) row 3
            // 각 확장 셀의 row index 판정: 전체 중 고유 Y값들을 오름차순 정렬해 상위/하위 구분
            var uniqueYs = new List<float>();
            foreach (var rt in extCells)
            {
                float y = rt.anchoredPosition.y;
                bool exists = false;
                foreach (var u in uniqueYs)
                {
                    if (Mathf.Abs(u - y) < 0.5f) { exists = true; break; }
                }
                if (!exists) uniqueYs.Add(y);
            }
            uniqueYs.Sort();  // 오름차순: 작은 y가 먼저 (하단 row)

            // 표시 가능 y: 하위 visibleRows 개의 y값
            var visibleYs = new HashSet<float>();
            int keepCount = Mathf.Min(visibleRows, uniqueYs.Count);
            for (int i = 0; i < keepCount; i++)
                visibleYs.Add(uniqueYs[i]);

            foreach (var rt in extCells)
            {
                float y = rt.anchoredPosition.y;
                bool show = false;
                foreach (var v in visibleYs)
                {
                    if (Mathf.Abs(v - y) < 0.5f) { show = true; break; }
                }
                rt.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// ★ Stage 1 전용: 특정 좌표의 블록을 그리드에서 영구 제거.
        /// blocks 딕셔너리에서 삭제 + GameObject 파괴. 매칭/낙하/리필 시스템에서 완전히 배제됨.
        /// 다음 스테이지 진입 시 InitializeGrid → ClearGrid → CreateBlocks로 자동 복원.
        /// </summary>
        public void RemoveBlockPermanently(HexCoord coord)
        {
            if (blocks.TryGetValue(coord, out HexBlock block))
            {
                if (block != null && block.gameObject != null)
                    Destroy(block.gameObject);
                blocks.Remove(coord);
            }
            allCoords.Remove(coord);
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
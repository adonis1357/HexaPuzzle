// =====================================================================================
// LaserBlockSystem.cs - 레이저 특수 블록 시스템
// =====================================================================================
//
// [큰 그림 설명]
// 이 파일은 "레이저" 특수 블록의 모든 동작을 담당합니다.
//
// 레이저 블록이란?
//   - 플레이어가 정확히 6개의 같은 색 블록을 한 번에 매칭하면 생성되는 강력한 특수 블록입니다.
//   - 발동하면 육각형 그리드의 3개 축(수직/슬래시/백슬래시) 방향으로
//     별 모양의 빔을 발사하여, 직선상에 있는 모든 블록을 파괴합니다.
//   - 마치 "별 모양 폭탄"처럼 중심에서 6갈래 광선이 뻗어나가는 것을 상상하면 됩니다.
//
// 동작 흐름:
//   1. 레이저 블록 생성 (CreateLaserBlock) - 6매칭 달성 시 호출
//   2. 레이저 발동 (ActivateLaser) - 캐스케이드 중 또는 플레이어 액션으로 트리거
//   3. 발동 전 압축 애니메이션 (블록이 움찔했다가 부풀어 오르는 연출)
//   4. 3축 x 양방향 = 6방향으로 빔 발사 + 직선상 블록 순차 파괴
//   5. 점수 계산 및 미션 시스템에 결과 보고
//
// 육각형 그리드의 3축이란?
//   - 수직(Vertical): 위아래 방향
//   - 슬래시(Slash): 오른쪽 위 <-> 왼쪽 아래 대각선 ( / 방향)
//   - 백슬래시(BackSlash): 왼쪽 위 <-> 오른쪽 아래 대각선 ( \ 방향)
//   이 3축 각각 양방향(+/-)으로 빔이 나가므로 총 6갈래 빔이 됩니다.
//
// =====================================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 레이저 특수 블록 시스템 - 육각형 그리드의 3축으로 빔을 발사하여 직선상 블록을 모두 파괴합니다.
    /// 정확히 6개 매칭 시 생성되며, 발동 시 중심에서 6갈래 광선이 뻗어나가는 강력한 특수 블록입니다.
    /// 비유: 눈송이 모양으로 레이저가 뻗어나가면서 경로 위의 모든 블록을 녹여버린다고 생각하면 됩니다.
    /// </summary>
    public class LaserBlockSystem : MonoBehaviour
    {
        // ============================================================
        // 인스펙터에서 연결하는 참조 (References)
        // ============================================================

        [Header("References")]
        /// <summary>
        /// 육각형 그리드 - 블록들이 배치된 게임판 전체를 관리하는 객체입니다.
        /// 레이저가 발사될 때 어떤 블록이 경로에 있는지 확인하기 위해 필요합니다.
        /// </summary>
        [SerializeField] private HexGrid hexGrid;

        /// <summary>
        /// 블록 제거 시스템 - 블록이 파괴될 때의 처리를 담당합니다.
        /// 캐스케이드(연쇄 반응) 깊이에 따라 이펙트 크기를 조절하는 데 사용됩니다.
        /// </summary>
        [SerializeField] private BlockRemovalSystem removalSystem;

        // ============================================================
        // 레이저 설정값 (Laser Settings)
        // ============================================================

        [Header("Laser Settings")]
        /// <summary>
        /// 레이저가 블록 하나를 파괴하고 다음 블록으로 넘어가는 시간 간격 (초).
        /// 값이 작을수록 블록이 빠르게 연쇄 파괴됩니다.
        /// 비유: 도미노가 쓰러지는 속도라고 생각하면 됩니다.
        /// </summary>
        [SerializeField] private float laserSpeed = 0.04f;

        /// <summary>
        /// 레이저 빔 이펙트(광선)가 화면에 보이는 시간 (초).
        /// 빔이 뻗어나간 후 이 시간 동안 빛나다가 서서히 사라집니다.
        /// </summary>
        [SerializeField] private float beamDuration = 0.3f;

        // ============================================================
        // 이벤트 및 상태 관리
        // ============================================================

        /// <summary>
        /// 레이저 발동이 완료되면 호출되는 이벤트. 점수(int)를 파라미터로 전달합니다.
        /// 다른 시스템(예: 점수 매니저)이 이 이벤트를 구독하여 점수를 반영합니다.
        /// </summary>
        public event System.Action<int> OnLaserComplete;

        /// <summary>
        /// 현재 동시에 실행 중인 레이저의 수.
        /// 여러 레이저가 동시에 발동될 수 있으므로 카운터로 관리합니다.
        /// 모든 레이저가 완료되면 0이 됩니다.
        /// </summary>
        private int activeLaserCount = 0;

        /// <summary>
        /// 레이저 경로에서 발견된 다른 특수 블록들의 대기열.
        /// 레이저가 다른 특수 블록(예: 폭탄, 드릴)을 만나면 바로 파괴하지 않고
        /// 이 목록에 넣어두었다가 나중에 연쇄 발동시킵니다.
        /// 비유: "나중에 터뜨릴 폭탄 목록"이라고 생각하면 됩니다.
        /// </summary>
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        /// <summary>
        /// 현재 레이저 발동 중인 블록들의 집합.
        /// 같은 블록이 중복으로 처리되는 것을 방지합니다.
        /// </summary>
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        /// <summary>
        /// 하나라도 레이저가 발동 중이면 true를 반환합니다.
        /// 게임 시스템이 "아직 레이저 처리 중이니 기다려라"라고 판단하는 데 사용됩니다.
        /// </summary>
        public bool IsActivating => activeLaserCount > 0;

        /// <summary>
        /// 특정 블록이 현재 레이저 발동 중인지 확인합니다.
        /// 다른 시스템이 이 블록을 건드리면 안 되는지 체크하는 용도입니다.
        /// </summary>
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);

        /// <summary>
        /// 긴급 초기화 - 모든 레이저 코루틴을 중단하고 상태를 깨끗이 리셋합니다.
        /// 게임이 비정상 상태에 빠졌을 때(예: 무한 루프, 스턱) 안전장치로 호출됩니다.
        /// 비유: 기계가 고장났을 때 누르는 "비상 정지 버튼"과 같습니다.
        /// </summary>
        public void ForceReset()
        {
            StopAllCoroutines();
            activeLaserCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            CleanupEffects();
            Debug.Log("[LaserBlockSystem] ForceReset called");
        }

        /// <summary>
        /// 코루틴 중단으로 화면에 남아있는 이펙트 오브젝트들을 일괄 삭제합니다.
        /// 레이저 빔, 폭발 효과 등이 중단 후에도 화면에 남아있는 것을 방지합니다.
        /// 비유: 무대 위에 남아있는 소품을 모두 치우는 청소 작업입니다.
        /// </summary>
        private void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);
        }

        /// <summary>
        /// 레이저 경로에서 발견되어 연쇄 발동을 기다리는 특수 블록 목록을 외부에 공개합니다.
        /// BlockRemovalSystem이 이 목록을 확인하고 순서대로 발동시킵니다.
        /// </summary>
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;

        /// <summary>
        /// 레이저 이펙트(빔, 폭발, 파편 등)가 생성될 부모 Transform.
        /// 모든 시각 효과는 이 부모 아래에 생성되어, 정리할 때 한꺼번에 처리할 수 있습니다.
        /// </summary>
        private Transform effectParent;

        /// <summary>
        /// 레이저 아이콘 스프라이트의 캐시.
        /// 한 번 생성하면 재사용하여 매번 새로 그리지 않습니다 (성능 최적화).
        /// </summary>
        private static Sprite laserIconSprite;

        // ============================================================
        // 초기화 (Start)
        // ============================================================

        /// <summary>
        /// Unity가 게임 시작 시 자동으로 호출하는 초기화 메서드.
        /// HexGrid와 BlockRemovalSystem 참조를 설정하고, 이펙트 레이어를 준비합니다.
        /// 인스펙터에서 연결이 안 되어 있으면 씬에서 자동으로 찾습니다.
        /// </summary>
        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[LaserBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[LaserBlockSystem] HexGrid not found!");
            }
            if (removalSystem == null)
                removalSystem = FindObjectOfType<BlockRemovalSystem>();

            SetupEffectParent();
        }

        /// <summary>
        /// 레이저 이펙트들이 생성될 전용 레이어(부모 오브젝트)를 설정합니다.
        /// Canvas 위에 투명한 레이어를 하나 만들어서, 모든 레이저 시각 효과가 이 위에 그려집니다.
        /// 비유: 그림을 그릴 때 본체 위에 투명 OHP 필름을 올려놓고 이펙트를 그리는 것과 같습니다.
        /// </summary>
        private void SetupEffectParent()
        {
            if (hexGrid == null) return;

            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("LaserEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);

                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                // 가장 위에 배치하여 다른 UI 요소 위에 이펙트가 보이도록 합니다
                effectLayer.transform.SetAsLastSibling();
                effectParent = effectLayer.transform;
            }
            else
            {
                effectParent = hexGrid.transform;
            }
        }

        // ============================================================
        // 레이저 아이콘 스프라이트 생성
        // - 게임 내에서 레이저 블록을 표시할 때 사용하는 아이콘 이미지를
        //   코드로 직접 그립니다 (프로시저럴 생성).
        // ============================================================

        /// <summary>
        /// 레이저 블록의 아이콘 스프라이트를 반환합니다.
        /// 처음 호출 시 코드로 생성하고, 이후에는 캐시된 것을 재사용합니다.
        /// 비유: 처음에 도장을 한 번 만들어두면, 그 다음부터는 찍기만 하면 되는 것과 같습니다.
        /// </summary>
        public static Sprite GetLaserIconSprite()
        {
            if (laserIconSprite == null)
                laserIconSprite = CreateLaserSprite(128);
            return laserIconSprite;
        }

        /// <summary>
        /// 레이저 아이콘을 프로시저럴(코드)로 직접 그립니다.
        /// 3개 축 방향의 직선을 겹쳐서 별 모양을 만들고, 중앙에 밝은 원을 추가합니다.
        /// 비유: 연필로 종이에 3개의 직선을 서로 60도 간격으로 교차해서 그리면 별 모양이 되는 것처럼,
        ///       픽셀 하나하나에 색을 칠하여 아이콘을 만드는 과정입니다.
        /// </summary>
        /// <param name="size">아이콘의 가로/세로 픽셀 크기 (정사각형)</param>
        /// <returns>완성된 레이저 아이콘 스프라이트</returns>
        private static Sprite CreateLaserSprite(int size)
        {
            // 빈 텍스처(도화지) 생성
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            // 모든 픽셀을 투명하게 초기화 (깨끗한 도화지)
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float lineLength = size * 0.42f;  // 각 직선의 길이 (아이콘 크기의 42%)
            float lineWidth = size * 0.06f;   // 각 직선의 두께 (아이콘 크기의 6%)

            // 3축 각도: 수직(90도), 슬래시(30도), 백슬래시(-30도)
            // 이 3개의 직선이 겹쳐져서 별 모양이 됩니다
            float[] angles = { 90f, 30f, -30f };

            // 각 축에 대해 직선을 그립니다
            foreach (float angleDeg in angles)
            {
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));   // 직선 방향
                Vector2 perp = new Vector2(-dir.y, dir.x);  // 직선에 수직인 방향 (두께 판단용)

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 p = new Vector2(x, y) - center;
                        float along = Vector2.Dot(p, dir);              // 직선 방향으로의 거리
                        float across = Mathf.Abs(Vector2.Dot(p, perp)); // 직선에서 벗어난 거리

                        // 직선의 범위 안에 있는 픽셀만 색칠
                        if (Mathf.Abs(along) <= lineLength && across <= lineWidth)
                        {
                            // 가장자리로 갈수록 투명해지는 효과 (부드러운 테두리)
                            float edgeFade = 1f - (across / lineWidth);
                            // 직선 끝부분으로 갈수록 투명해지는 효과 (자연스러운 끝처리)
                            float tipFade = 1f - Mathf.Max(0, (Mathf.Abs(along) - lineLength * 0.7f) / (lineLength * 0.3f));
                            float alpha = edgeFade * tipFade;

                            // 중앙이 더 밝은 글로우(빛나는) 효과
                            float glow = Mathf.Pow(edgeFade, 2f);
                            Color c = new Color(
                                0.65f + glow * 0.15f,  // 파란빛이 도는 밝은 색
                                0.82f + glow * 0.08f,
                                0.98f,
                                alpha * 0.9f
                            );

                            // 이미 더 밝은(불투명한) 픽셀이 있으면 덮어쓰지 않음
                            int idx = y * size + x;
                            if (pixels[idx].a < c.a)
                                pixels[idx] = c;
                        }
                    }
                }
            }

            // 중앙에 밝은 원 그리기 (별의 중심점, 빛이 모이는 곳)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < size * 0.1f)
                    {
                        float a = 1f - dist / (size * 0.1f);
                        Color bright = new Color(0.9f, 0.95f, 1f, a);
                        int idx = y * size + x;
                        pixels[idx] = Color.Lerp(pixels[idx], bright, a);
                    }
                }
            }

            // 완성된 픽셀 데이터를 텍스처에 적용하고 스프라이트로 변환
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 레이저 블록 생성
        // - 매칭 시스템이 정확히 6개 매칭을 감지하면 이 메서드를 호출하여
        //   일반 블록을 레이저 특수 블록으로 변환합니다.
        // ============================================================

        /// <summary>
        /// 기존 블록을 레이저 특수 블록으로 변환합니다.
        /// 비유: 일반 구슬에 "레이저" 스티커를 붙여서 특수 구슬로 업그레이드하는 것과 같습니다.
        /// </summary>
        /// <param name="block">레이저로 변환할 블록</param>
        /// <param name="gemType">블록의 보석 색상 (레이저 빔 색상에 영향)</param>
        public void CreateLaserBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData laserData = new BlockData(gemType);
            laserData.specialType = SpecialBlockType.Laser;
            block.SetBlockData(laserData);
            Debug.Log($"[LaserBlockSystem] Created laser at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // 레이저 발동
        // - 레이저 블록이 매칭되거나 다른 특수 블록에 의해 터질 때 호출됩니다.
        // - 실제 동작은 코루틴(LaserCoroutine)에서 시간에 걸쳐 진행됩니다.
        // ============================================================

        /// <summary>
        /// 레이저 블록을 발동시킵니다. 6갈래 빔 발사 → 직선상 블록 파괴 → 점수 계산의 전체 과정을 시작합니다.
        /// 비유: 대포의 발사 버튼을 누르는 것과 같습니다. 누르면 LaserCoroutine이 실제 발사를 수행합니다.
        /// </summary>
        /// <param name="laserBlock">발동시킬 레이저 블록</param>
        public void ActivateLaser(HexBlock laserBlock)
        {
            if (laserBlock == null) return;
            if (laserBlock.Data == null || laserBlock.Data.specialType != SpecialBlockType.Laser)
                return;
            Debug.Log($"[LaserBlockSystem] Activating laser at {laserBlock.Coord}");
            StartCoroutine(LaserCoroutine(laserBlock));
        }

        /// <summary>
        /// 레이저 발동의 전체 과정을 시간에 걸쳐 실행하는 코루틴(비동기 처리).
        ///
        /// 실행 순서:
        /// 1단계: 발동 전 압축 애니메이션 (블록이 움찔하는 예고 동작)
        /// 2단계: 히트스톱 + 줌펀치 (화면이 잠깐 멈추고 흔들리는 임팩트 연출)
        /// 3단계: 레이저 블록 자체를 클리어 (빈 칸으로 만듦)
        /// 4단계: 3축 x 2방향 = 6방향으로 파괴 대상 블록 수집
        /// 5단계: 파괴 대상의 점수 미리 계산
        /// 6단계: 중앙 폭발 이펙트 + 화면 흔들림 + 6방향 빔 이펙트 표시
        /// 7단계: 6방향 동시에 블록 순차 파괴 (각 방향은 안쪽→바깥쪽 순서)
        /// 8단계: 최종 점수 합산 및 미션 시스템에 결과 보고
        /// </summary>
        private IEnumerator LaserCoroutine(HexBlock laserBlock)
        {
            // 활성 레이저 카운터 증가 (IsActivating 판단에 사용)
            activeLaserCount++;
            activeBlocks.Add(laserBlock);

            // 레이저의 시작 위치와 색상 저장 (이후 이펙트에서 사용)
            HexCoord startCoord = laserBlock.Coord;
            Vector3 laserWorldPos = laserBlock.transform.position;
            Color laserColor = GemColors.GetColor(laserBlock.Data.gemType);

            Debug.Log($"[LaserBlockSystem] === LASER ACTIVATED === Coord={startCoord}");

            // [1단계] 발동 전 압축 애니메이션
            // 블록이 1.0 -> 0.78 -> 1.18 크기로 변하며 "힘을 모으는" 느낌을 줍니다
            yield return StartCoroutine(PreFireCompression(laserBlock));

            // [2단계] 히트스톱: 발사 순간 게임 시간을 잠깐 멈춰서 강렬한 타격감을 줍니다
            // 마치 만화에서 강한 공격이 터지는 순간 한 프레임이 멈추는 것과 같습니다
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));

            // [2단계] 줌펀치: 화면 전체가 살짝 확대되었다 돌아오는 연출
            // 강한 충격이 화면 밖까지 전해지는 느낌을 줍니다
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            // [3단계] 레이저 블록 자체의 데이터를 비움 (발사 후 빈 칸이 됨)
            laserBlock.ClearData();

            // [4단계] 6방향 파괴 대상 블록 수집
            // 3축(수직/슬래시/백슬래시) x 2방향(+/-) = 총 6갈래 직선을 탐색합니다
            DrillDirection[] axes = { DrillDirection.Vertical, DrillDirection.Slash, DrillDirection.BackSlash };
            List<List<HexBlock>> allTargetLines = new List<List<HexBlock>>();

            foreach (var axis in axes)
            {
                // 각 축의 양방향(+, -)으로 직선상의 블록들을 수집
                List<HexBlock> positive = GetBlocksInDirection(startCoord, axis, true);
                List<HexBlock> negative = GetBlocksInDirection(startCoord, axis, false);
                allTargetLines.Add(positive);
                allTargetLines.Add(negative);
            }

            // 전체 파괴 대상 블록 수 합산 (로그용)
            int totalTargets = 0;
            foreach (var line in allTargetLines)
                totalTargets += line.Count;

            // [5단계] 파괴 대상 블록들의 점수 미리 계산
            // ClearData가 호출되기 전에 블록 정보를 읽어서 점수를 산출합니다
            int blockScoreSum = 0;
            int basicBlockCount = 0;  // 기본 블록(GemType 1-5) 카운트
            var gemCountsByColor = new Dictionary<GemType, int>(); // 색상별 미션 카운팅용
            var sm = GameManager.Instance?.GetComponent<ScoreManager>();
            foreach (var line in allTargetLines)
            {
                foreach (var t in line)
                {
                    if (t == null || t.Data == null || t.Data.gemType == GemType.None) continue;
                    // 일반 블록과 고정 블록만 점수 계산 (다른 특수 블록은 별도 발동)
                    if (t.Data.specialType != SpecialBlockType.None && t.Data.specialType != SpecialBlockType.FixedBlock) continue;
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(t.Data.tier);

                    // 기본 블록(빨/파/초/노/보) 카운트 - 미션 보고용
                    if ((int)t.Data.gemType >= 1 && (int)t.Data.gemType <= 5)
                    {
                        basicBlockCount++;
                        if (gemCountsByColor.ContainsKey(t.Data.gemType))
                            gemCountsByColor[t.Data.gemType]++;
                        else
                            gemCountsByColor[t.Data.gemType] = 1;
                    }

                    // 적군(장애물) 블록 처리: 가시/체인/회색 블록은 추가 점수
                    if (sm != null)
                    {
                        if (t.Data.hasThorn)
                            sm.AddEnemyScore(EnemyType.ThornParasite, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, t.transform.position);
                        if (t.Data.hasChain)
                            sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, t.transform.position);
                        if (t.Data.gemType == GemType.Gray)
                            sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, t.transform.position);
                    }
                }
            }

            Debug.Log($"[LaserBlockSystem] Total targets across 3 axes: {totalTargets}");

            // [6단계] 시각 효과 일제히 시작
            // 중앙에서 빛나는 폭발 플래시
            StartCoroutine(LaserCenterFlash(laserWorldPos, laserColor));
            // 화면 전체가 흔들리는 효과 (강한 충격 표현)
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            // 3축 x 2방향 = 6개의 레이저 빔 이펙트(광선)를 동시에 발사
            for (int i = 0; i < axes.Length; i++)
            {
                StartCoroutine(LaserBeamEffect(laserWorldPos, axes[i], true, laserColor));
                StartCoroutine(LaserBeamEffect(laserWorldPos, axes[i], false, laserColor));
            }

            // 이펙트가 시작된 직후 잠깐 대기 (시각적으로 빔이 뻗어나가는 것이 보이도록)
            yield return new WaitForSeconds(0.05f);

            // [7단계] 6방향 동시 파괴
            // 각 방향의 블록들은 안쪽에서 바깥쪽으로 순차적으로 파괴되지만,
            // 6개 방향은 서로 병렬(동시)로 실행됩니다.
            // 비유: 폭죽의 6갈래 불꽃이 동시에 뻗어나가면서, 각 불꽃은 순서대로 터지는 것
            List<Coroutine> lineCoroutines = new List<Coroutine>();
            foreach (var line in allTargetLines)
            {
                if (line.Count > 0)
                    lineCoroutines.Add(StartCoroutine(DestroyLine(line, laserColor, laserWorldPos)));
            }

            // 색상별 미션 카운팅 — 파괴 애니메이션 시작 직후, 완료 대기 전에 호출
            GameManager.Instance?.OnSpecialBlockDestroyedBlocksByColor(gemCountsByColor, "Laser");

            // 모든 방향의 파괴가 끝날 때까지 대기
            foreach (var co in lineCoroutines)
                yield return co;

            // [8단계] 최종 점수 계산: 기본 300점(레이저 발동 보너스) + 파괴된 블록들의 티어별 점수
            int totalScore = 300 + blockScoreSum;
            Debug.Log($"[LaserBlockSystem] === LASER COMPLETE === Score={totalScore} (base:300 + blockTierSum:{blockScoreSum})");

            // 미션 시스템에 파괴 결과 보고 (기본 블록만 카운트, 특수 블록은 제외)
            List<HexBlock> allLaserTargets = new List<HexBlock>();
            List<HexBlock> basicBlocksOnly = new List<HexBlock>();

            foreach (var line in allTargetLines)
                allLaserTargets.AddRange(line);

            foreach (var target in allLaserTargets)
            {
                if (target != null && target.Data != null && target.Data.gemType != GemType.None)
                {
                    // 기본 블록만 포함: 특수블록 제외
                    if (target.Data.specialType == SpecialBlockType.None || target.Data.specialType == SpecialBlockType.FixedBlock)
                    {
                        basicBlocksOnly.Add(target);
                        Debug.Log($"[LaserBlockSystem]   BasicBlock: {target.Coord}, gemType={target.Data.gemType}");
                    }
                }
            }

            Debug.Log($"[LaserBlockSystem] Passing {basicBlocksOnly.Count} basic blocks to MissionSystem (from {allLaserTargets.Count} total targets)");

            // 미션 시스템에 "레이저로 이만큼 블록을 파괴했다"고 알림
            MissionSystem ms = Object.FindObjectOfType<MissionSystem>();
            if (ms != null)
                ms.OnSpecialBlockDestroyedBlocks(basicBlocksOnly);

            // 레이저 완료 이벤트 발생 (점수 매니저 등에 알림)
            OnLaserComplete?.Invoke(totalScore);
            activeBlocks.Remove(laserBlock);
            activeLaserCount--;
        }

        // ============================================================
        // 방향별 블록 수집
        // - 레이저 중심에서 특정 방향(축)으로 직선을 그으며
        //   경로에 있는 모든 블록을 리스트로 모읍니다.
        // ============================================================

        /// <summary>
        /// 레이저 중심 좌표에서 특정 방향으로 직선상에 있는 블록들을 모두 수집합니다.
        /// 비유: 레이저 발사 전에 "이 방향으로 쏘면 어떤 블록들이 맞을까?" 미리 확인하는 것입니다.
        /// </summary>
        /// <param name="start">레이저 중심 좌표</param>
        /// <param name="direction">탐색할 축 방향 (수직/슬래시/백슬래시)</param>
        /// <param name="positive">true면 양(+) 방향, false면 음(-) 방향으로 탐색</param>
        /// <returns>경로에 있는 블록들의 목록 (가까운 순서대로)</returns>
        private List<HexBlock> GetBlocksInDirection(HexCoord start, DrillDirection direction, bool positive)
        {
            List<HexBlock> blocks = new List<HexBlock>();
            if (hexGrid == null) return blocks;

            // 이동 방향의 좌표 변화량 (한 칸 이동 시 q, r이 얼마나 변하는지)
            HexCoord delta = GetDirectionDelta(direction, positive);
            HexCoord current = start + delta;  // 시작점의 바로 다음 칸부터 탐색
            int maxSteps = 20;  // 안전장치: 최대 20칸까지만 탐색 (무한 루프 방지)
            int step = 0;
            while (hexGrid.IsValidCoord(current) && step < maxSteps)
            {
                HexBlock block = hexGrid.GetBlock(current);
                // 유효한 블록(데이터가 있고, 빈 칸이 아닌)만 수집
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    blocks.Add(block);
                current = current + delta;  // 다음 칸으로 이동
                step++;
            }
            return blocks;
        }

        /// <summary>
        /// 주어진 축 방향과 양/음 방향에 따라, 한 칸 이동 시의 좌표 변화량(델타)를 반환합니다.
        /// 육각형 그리드에서는 이웃 칸으로 이동할 때 q, r 좌표가 특정 패턴으로 변합니다.
        ///
        /// - Vertical(수직): q 변화 없이 r만 +/-1 (위아래 이동)
        /// - Slash(/방향): q와 r이 반대로 +/-1 (대각선 이동)
        /// - BackSlash(\방향): q만 +/-1, r 변화 없음 (대각선 이동)
        /// </summary>
        private HexCoord GetDirectionDelta(DrillDirection direction, bool positive)
        {
            int sign = positive ? 1 : -1;
            switch (direction)
            {
                case DrillDirection.Vertical:  return new HexCoord(0, sign);
                case DrillDirection.Slash:     return new HexCoord(sign, -sign);
                case DrillDirection.BackSlash: return new HexCoord(sign, 0);
                default: return new HexCoord(0, sign);
            }
        }

        // ============================================================
        // 라인 파괴
        // - 한 방향의 직선에 있는 블록들을 안쪽에서 바깥쪽으로
        //   순서대로 파괴합니다.
        // ============================================================

        /// <summary>
        /// 한 방향 직선의 블록들을 순차적으로 파괴합니다.
        /// 레이저 중심에서 가까운 블록부터 순서대로 파괴하여, 빔이 뻗어나가며 파괴하는 느낌을 줍니다.
        ///
        /// 특수 블록을 만나면 바로 파괴하지 않고 "대기열"에 넣어서 나중에 연쇄 발동시킵니다.
        /// 비유: 도미노를 쓰러뜨리다가 큰 폭탄을 만나면, 일단 표시해두고 나중에 따로 터뜨리는 것
        /// </summary>
        /// <param name="targets">파괴할 블록 목록 (가까운 순서)</param>
        /// <param name="laserColor">레이저 빔의 색상</param>
        /// <param name="center">레이저 중심 위치 (이펙트 방향 계산용)</param>
private IEnumerator DestroyLine(List<HexBlock> targets, Color laserColor, Vector3 center)
        {
            // 파괴 애니메이션 코루틴들을 모아서 마지막에 전부 완료될 때까지 대기
            List<Coroutine> destroyCoroutines = new List<Coroutine>();

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;

                // Bug #15 수정: 여러 레이저가 동시에 실행될 때 이미 클리어된 블록 건너뛰기
                if (target.Data == null || target.Data.gemType == GemType.None) continue;

                // 방패(ReflectionShield) 적군이 있으면 레이저를 흡수하여 이 블록은 살아남음
                if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(target))
                    continue;

                // 경로에 다른 특수 블록(폭탄, 드릴 등)이 있는 경우:
                // 즉시 파괴하지 않고 대기열에 넣어 나중에 연쇄 발동
                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    Debug.Log($"[LaserBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
                    if (!pendingSpecialBlocks.Contains(target))
                    {
                        pendingSpecialBlocks.Add(target);
                        target.SetPendingActivation();    // "곧 발동됩니다" 상태로 표시
                        target.StartWarningBlink(10f);    // 경고 깜빡임 시작
                    }
                }
                else
                {
                    // 일반 블록: 파괴 애니메이션과 함께 제거
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithLaser(target, blockColor, center)));
                }

                // 블록 사이에 짧은 간격을 두어 "빔이 뻗어나가며 파괴하는" 순차적 느낌을 줌
                yield return new WaitForSeconds(laserSpeed);
            }

            // 모든 파괴 애니메이션이 완료될 때까지 대기 (ClearData 보장)
            foreach (var co in destroyCoroutines)
                yield return co;
        }

        // ============================================================
        // 블록 파괴 이펙트
        // - 레이저에 맞은 개별 블록이 파괴될 때의 시각 효과와 애니메이션
        // ============================================================

        /// <summary>
        /// 레이저에 맞은 블록 하나를 파괴하는 애니메이션을 실행합니다.
        ///
        /// 연출 순서:
        /// 1. 화이트 플래시: 블록 위에 하얀 빛이 번쩍 (피격 순간 표현)
        /// 2. 임팩트 웨이브: 블록 위치에서 충격파가 퍼져나감
        /// 3. 파편: 블록 조각이 사방으로 흩어지는 효과
        /// 4. 이중 이징 파괴: 블록이 살짝 커졌다가 → 찌그러지며 사라짐
        ///
        /// 비유: 풍선이 터질 때 잠깐 부풀었다가 펑! 하고 터지면서 조각이 날리는 것과 비슷합니다.
        /// </summary>
        /// <param name="block">파괴할 블록</param>
        /// <param name="blockColor">블록의 색상 (파편/이펙트 색상에 사용)</param>
        /// <param name="laserCenter">레이저 중심 위치</param>
        private IEnumerator DestroyBlockWithLaser(HexBlock block, Color blockColor, Vector3 laserCenter)
        {
            if (block == null) yield break;

            Vector3 blockPos = block.transform.position;

            // 화이트 플래시 오버레이 (하얀 빛 번쩍 효과)
            StartCoroutine(DestroyFlashOverlay(block));

            // 임팩트 웨이브 (충격파 퍼짐 효과)
            StartCoroutine(LaserImpactWave(blockPos, blockColor));

            // 파편 효과: 블록 조각들이 사방으로 튀어나감
            // 캐스케이드(연쇄) 깊이가 깊을수록 파편이 더 많이 생성 (더 화려해짐)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int debrisCount = Mathf.RoundToInt((VisualConstants.DebrisBaseCount / 2 + Random.Range(0, 2)) * cascadeMult);
            for (int i = 0; i < debrisCount; i++)
                StartCoroutine(AnimateDebris(blockPos, blockColor));

            // 이중 이징(Dual Easing) 파괴 애니메이션:
            // 전반부(20%): 블록이 살짝 확대됨 (터지기 직전 부풀어 오르는 느낌)
            // 후반부(80%): 블록이 좌우로 찌그러지면서 세로로 줄어들어 사라짐
            float duration = VisualConstants.DestroyDuration;
            float elapsed = 0f;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;  // 확대 구간 비율 (약 0.2 = 20%)
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);  // 0~1 사이의 진행률

                if (t < expandRatio)
                {
                    // 전반부: 부드럽게 확대 (EaseOutCubic - 처음에 빠르고 끝에 느림)
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = origScale * scale;
                }
                else
                {
                    // 후반부: 가로로 찌그러지면서(Squeeze) 세로로 축소되어 사라짐
                    float shrinkT = (t - expandRatio) / (1f - expandRatio);
                    float sx = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(shrinkT * Mathf.PI);  // 가로 찌그러짐
                    float sy = Mathf.Max(0f, 1f - VisualConstants.EaseInQuad(shrinkT));  // 세로 축소
                    block.transform.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);
                }

                yield return null;
            }

            // 원래 크기로 복원 후 블록 데이터 클리어 (빈 칸으로 만듦)
            block.transform.localScale = Vector3.one;
            block.ClearData();
        }

        // ============================================================
        // 레이저 시각 이펙트 모음
        // - 레이저 발동 시 화면에 표시되는 다양한 시각 효과들
        // ============================================================

        /// <summary>
        /// 레이저 중앙에서 터지는 폭발 플래시 효과.
        /// 레이저가 발사되는 순간, 중심점에서 밝은 빛이 번쩍하고 퍼져나갑니다.
        /// 블룸(후광) 레이어와 스파크(불꽃) 입자도 함께 생성됩니다.
        /// 비유: 폭죽의 중심에서 빛이 터지는 순간
        /// </summary>
        /// <param name="pos">폭발 중심 위치 (월드 좌표)</param>
        /// <param name="color">레이저 블록의 색상</param>
        private IEnumerator LaserCenterFlash(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 레이어: 메인 플래시 뒤에 큰 후광(글로우)을 깔아줌
            StartCoroutine(BloomLayer(pos, VisualConstants.FlashColorLaser, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // 메인 플래시 오브젝트 생성 (밝은 빛 원형)
            GameObject flash = new GameObject("LaserCenterFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;  // 터치 입력을 가로채지 않도록 설정
            img.color = new Color(VisualConstants.FlashColorLaser.r, VisualConstants.FlashColorLaser.g, VisualConstants.FlashColorLaser.b, 1f);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            // 스파크(불꽃 입자) 생성: 중심에서 사방으로 작은 빛 조각들이 튀어나감
            // 캐스케이드 깊이에 따라 스파크 수가 증가 (연쇄가 깊을수록 더 화려)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkLargeCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
                StartCoroutine(AnimateSpark(pos, color));

            // 플래시가 점점 커지면서 투명해지는 애니메이션
            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);  // 점점 확대
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                // 시간이 지남에 따라 투명해짐 (번쩍 → 서서히 사라짐)
                img.color = new Color(VisualConstants.FlashColorLaser.r, VisualConstants.FlashColorLaser.g, VisualConstants.FlashColorLaser.b, (1f - t) * 0.9f);
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 레이저 빔(광선) 이펙트 - 얇은 광선이 중심에서 한 방향으로 길게 뻗어나갑니다.
        /// 외곽 빔(색상)과 내부 코어 빔(흰색)으로 구성되어, 광선의 중심이 더 밝게 보입니다.
        ///
        /// 연출 순서:
        /// 1. 빔이 0에서 최대 길이까지 빠르게 뻗어나감 (0.12초)
        /// 2. 빔이 떨리며(shimmer) 점점 가늘어지다가 사라짐
        ///
        /// 비유: 레이저 포인터를 켜면 빛이 쭉 뻗어나갔다가 꺼지는 것과 비슷합니다.
        /// </summary>
        /// <param name="pos">빔 시작점 (레이저 중심 위치)</param>
        /// <param name="direction">빔 방향 축 (수직/슬래시/백슬래시)</param>
        /// <param name="positive">양(+) 방향이면 true, 음(-) 방향이면 false</param>
        /// <param name="color">빔 색상</param>
        private IEnumerator LaserBeamEffect(Vector3 pos, DrillDirection direction, bool positive, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 외곽 빔 오브젝트 생성 (블록 색상의 밝은 버전)
            GameObject beam = new GameObject("LaserBeam");
            beam.transform.SetParent(parent, false);
            beam.transform.position = pos;

            var img = beam.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            Color beamColor = VisualConstants.LaserBrighten(color);  // 색상을 더 밝게
            beamColor.a = 0.9f;
            img.color = beamColor;

            RectTransform rt = beam.GetComponent<RectTransform>();
            float angle = GetDirectionAngle(direction, positive);  // 빔의 회전 각도 계산
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            rt.pivot = new Vector2(0.5f, 0f);  // 아래쪽 중심을 기준으로 위로 뻗어나감
            rt.sizeDelta = new Vector2(6f, 0f);  // 초기 길이 0 (점점 늘어남)

            // 내부 코어 빔 (외곽 빔 안쪽의 더 밝은 흰색 선)
            // 비유: 형광등의 중심부가 테두리보다 더 밝게 빛나는 것과 같습니다
            GameObject coreBeam = new GameObject("LaserCoreBeam");
            coreBeam.transform.SetParent(beam.transform, false);
            coreBeam.transform.localPosition = Vector3.zero;
            coreBeam.transform.localRotation = Quaternion.identity;

            var coreImg = coreBeam.AddComponent<UnityEngine.UI.Image>();
            coreImg.raycastTarget = false;
            coreImg.color = new Color(1f, 1f, 1f, 0.8f);  // 밝은 흰색

            RectTransform coreRt = coreBeam.GetComponent<RectTransform>();
            coreRt.pivot = new Vector2(0.5f, 0f);
            coreRt.sizeDelta = new Vector2(2f, 0f);  // 외곽보다 가느다란 선

            // [1단계] 빔 확장: 길이가 0에서 최대치까지 빠르게 늘어남
            float extendDuration = 0.12f;
            float elapsed = 0f;
            float maxLength = 800f;  // 빔의 최대 길이 (픽셀)

            while (elapsed < extendDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / extendDuration;
                float easeT = VisualConstants.EaseOutCubic(t);  // 처음에 빠르고 끝에 느려지는 곡선
                rt.sizeDelta = new Vector2(6f, maxLength * easeT);
                coreRt.sizeDelta = new Vector2(2f, maxLength * easeT);
                yield return null;
            }

            // [2단계] 빔 유지 + 페이드아웃 + 떨림(shimmer) 효과
            // 빔이 미세하게 떨리면서 점점 가늘어지고 투명해집니다
            float fadeDuration = beamDuration;
            elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                // 미세한 두께 떨림 (shimmer): 빔이 살아있는 것처럼 미세하게 진동
                float shimmer = 1f + 0.15f * Mathf.Sin(elapsed * 30f);
                float width = Mathf.Lerp(6f, 2f, t) * shimmer;  // 점점 가늘어짐
                rt.sizeDelta = new Vector2(width, maxLength);
                coreRt.sizeDelta = new Vector2(Mathf.Max(1f, width * 0.3f), maxLength);

                // 투명도 감소 (서서히 사라짐)
                beamColor.a = 0.9f * (1f - t);
                img.color = beamColor;
                coreImg.color = new Color(1f, 1f, 1f, 0.8f * (1f - t));

                yield return null;
            }

            Destroy(beam);
        }

        /// <summary>
        /// 레이저가 블록에 맞을 때 발생하는 충격파(웨이브) 이펙트.
        /// 작은 원이 블록 위치에서 확장되면서 투명해집니다.
        /// 비유: 물에 돌을 던졌을 때 퍼지는 파문과 같습니다.
        /// </summary>
        /// <param name="pos">충격파 중심 위치</param>
        /// <param name="color">충격파 색상</param>
        private IEnumerator LaserImpactWave(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject wave = new GameObject("LaserImpactWave");
            wave.transform.SetParent(parent, false);
            wave.transform.position = pos;

            var image = wave.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha);

            RectTransform rt = wave.GetComponent<RectTransform>();
            float initSize = VisualConstants.WaveSmallInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            // 원이 점점 커지면서 투명해지는 애니메이션
            float duration = VisualConstants.WaveSmallDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.WaveSmallExpand - 1f);  // 확대
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha * (1f - t));  // 투명해짐
                yield return null;
            }

            Destroy(wave);
        }

        /// <summary>
        /// 스파크(불꽃 입자) 하나를 애니메이션합니다.
        /// 중심점에서 랜덤한 방향으로 튀어나가며, 점점 느려지고 투명해지면서 사라집니다.
        /// 비유: 불꽃놀이에서 터진 후 하늘에서 떨어지는 작은 불씨 하나의 움직임입니다.
        /// </summary>
        /// <param name="center">스파크가 시작되는 중심 위치</param>
        /// <param name="color">스파크 색상</param>
        private IEnumerator AnimateSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject spark = new GameObject("LaserSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            // 밝게 처리된 색상 (반짝이는 느낌)
            Color bright = VisualConstants.LaserBrighten(color);
            bright.a = 1f;
            img.color = bright;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkMediumSizeMin, VisualConstants.SparkMediumSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            // 랜덤한 방향과 속도로 튀어나감
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkMediumSpeedMin, VisualConstants.SparkMediumSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(VisualConstants.SparkMediumLifetimeMin, VisualConstants.SparkMediumLifetimeMax);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                // 매 프레임 위치 업데이트 (속도에 따라 이동)
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= VisualConstants.SparkDeceleration;  // 점점 감속 (마찰)
                bright.a = 1f - t;  // 점점 투명해짐
                img.color = bright;
                float s = size * (1f - t * 0.5f);  // 점점 작아짐
                rt.sizeDelta = new Vector2(s, s);
                yield return null;
            }

            Destroy(spark);
        }

        /// <summary>
        /// 파편(Debris) 하나를 애니메이션합니다.
        /// 블록이 파괴될 때 작은 조각이 사방으로 튀어나가며, 중력의 영향을 받아 아래로 떨어지고
        /// 회전하면서 점점 투명해지다가 사라집니다.
        ///
        /// 비유: 유리잔이 깨질 때 조각들이 사방으로 튀면서 바닥에 떨어지는 것과 같습니다.
        /// 스파크(불꽃)와의 차이점: 파편은 중력이 적용되고 회전하며, 직사각형 모양입니다.
        /// </summary>
        /// <param name="center">파편이 시작되는 위치</param>
        /// <param name="color">파편 색상 (원래 블록 색에서 약간 변형)</param>
        private IEnumerator AnimateDebris(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject debris = new GameObject("LaserDebris");
            debris.transform.SetParent(parent, false);
            debris.transform.position = center;

            var image = debris.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;

            // 원래 색상에서 약간의 랜덤 변화를 주어 자연스러운 느낌 부여
            float variation = Random.Range(-0.15f, 0.15f);
            Color debrisColor = new Color(
                Mathf.Clamp01(color.r + variation),
                Mathf.Clamp01(color.g + variation),
                Mathf.Clamp01(color.b + variation),
                1f
            );
            image.color = debrisColor;

            // 파편 크기: 가로/세로가 다른 직사각형 (조각 느낌)
            RectTransform rt = debris.GetComponent<RectTransform>();
            float w = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax * 0.9f);
            float h = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax * 0.7f);
            rt.sizeDelta = new Vector2(w, h);

            // 랜덤 방향과 속도로 튀어나감
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.DebrisBaseSpeedMin, VisualConstants.DebrisBaseSpeedMax);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float rotSpeed = Random.Range(VisualConstants.DebrisRotSpeedMin, VisualConstants.DebrisRotSpeedMax);  // 회전 속도
            float lifetime = Random.Range(VisualConstants.DebrisLifetimeMin, VisualConstants.DebrisLifetimeMax);
            float elapsed = 0f;
            float rot = Random.Range(0f, 360f);  // 초기 회전 각도

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                // 중력 적용: 시간이 갈수록 아래로 떨어짐
                velocity.y += VisualConstants.DebrisGravity * Time.deltaTime;
                Vector3 pos = debris.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                debris.transform.position = pos;

                // 회전: 파편이 빙글빙글 돌면서 떨어짐
                rot += rotSpeed * Time.deltaTime;
                debris.transform.localRotation = Quaternion.Euler(0, 0, rot);

                // 투명도: 시간이 지남에 따라 서서히 투명해짐 (가속 곡선)
                debrisColor.a = 1f - t * t;
                image.color = debrisColor;
                // 크기: 점점 작아짐
                float shrink = 1f - t * 0.5f;
                rt.sizeDelta = new Vector2(w * shrink, h * shrink);

                yield return null;
            }

            Destroy(debris);
        }

        // ============================================================
        // 화면 흔들림 (Screen Shake)
        // - 강한 임팩트를 표현하기 위해 게임판 전체를 잠깐 흔듭니다.
        // - Bug #11 수정: 여러 흔들림이 동시에 실행될 때 위치가 어긋나는
        //   문제를 shakeCount 카운터로 해결
        // ============================================================

        /// <summary>
        /// 오브젝트를 서서히 투명하게 만든 후 삭제합니다.
        /// 이펙트가 갑자기 사라지지 않고 자연스럽게 페이드아웃 되도록 합니다.
        /// </summary>
        private IEnumerator FadeOutAndDestroy(GameObject obj)
        {
            var img = obj.GetComponent<UnityEngine.UI.Image>();
            if (img == null) { Destroy(obj); yield break; }

            Color c = img.color;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = 1f - t;  // 투명도를 1(불투명)에서 0(투명)으로
                img.color = c;
                yield return null;
            }

            Destroy(obj);
        }

        /// <summary>
        /// 현재 동시에 실행 중인 화면 흔들림의 수.
        /// 여러 흔들림이 동시에 일어나도, 마지막 흔들림이 끝나야 원래 위치로 복귀합니다.
        /// </summary>
        private int shakeCount = 0;

        /// <summary>
        /// 화면 흔들림 시작 전의 원래 위치를 저장합니다.
        /// 흔들림이 끝나면 이 위치로 정확히 돌아옵니다.
        /// </summary>
        private Vector3 shakeOriginalPos;

        /// <summary>
        /// 화면(게임판)을 일정 시간 동안 흔듭니다.
        /// 레이저 발동 같은 강한 충격을 플레이어에게 체감시키는 연출입니다.
        /// 흔들림 강도는 시간이 지남에 따라 점점 줄어듭니다(감쇠).
        ///
        /// 비유: 지진이 나면 처음에 강하게 흔들리다가 점점 잦아드는 것과 같습니다.
        /// </summary>
        /// <param name="intensity">흔들림 강도 (픽셀 단위, 클수록 더 많이 흔들림)</param>
        /// <param name="duration">흔들림 지속 시간 (초)</param>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            // 첫 번째 흔들림이면 원래 위치를 저장
            if (shakeCount == 0)
                shakeOriginalPos = target.localPosition;
            shakeCount++;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float decay = 1f - VisualConstants.EaseInQuad(t);  // 감쇠 곡선: 점점 약해짐
                // 랜덤한 x, y 방향으로 흔들림
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                target.localPosition = shakeOriginalPos + new Vector3(x, y, 0);
                yield return null;
            }

            shakeCount--;
            // 모든 동시 흔들림이 끝나면 원래 위치로 복귀
            if (shakeCount <= 0)
            {
                shakeCount = 0;
                target.localPosition = shakeOriginalPos;
            }
        }

        // ============================================================
        // 유틸리티 메서드
        // - 방향 계산, 각도 변환 등의 보조 기능
        // ============================================================

        /// <summary>
        /// 드릴 방향과 양/음 여부를 화면상의 각도(degree)로 변환합니다.
        /// 레이저 빔 이펙트가 화면에서 올바른 방향을 가리키도록 회전값을 계산합니다.
        ///
        /// 좌표계 설명:
        /// - 빔의 피봇(기준점)이 아래쪽에 있고, 0도가 위쪽 방향입니다.
        /// - Y축이 반전된 UI 좌표계를 사용합니다.
        /// - positive가 false이면 반대 방향(+180도)이 됩니다.
        /// </summary>
        /// <param name="direction">축 방향</param>
        /// <param name="positive">양(+) 방향이면 true</param>
        /// <returns>화면상의 회전 각도 (도 단위)</returns>
        private float GetDirectionAngle(DrillDirection direction, bool positive)
        {
            // Y반전 좌표계 기반 실제 화면 방향 (beam pivot=bottom, 0도=up)
            // Vertical(0,+r): 화면 아래, Slash(+q,-r): 화면 우상, BackSlash(+q,0): 화면 우하
            float angle;
            switch (direction)
            {
                case DrillDirection.Vertical:  angle = 180f;  break;  // 아래쪽
                case DrillDirection.Slash:     angle = -60f;  break;  // 오른쪽 위
                case DrillDirection.BackSlash: angle = -120f; break;  // 오른쪽 아래
                default: angle = 180f; break;
            }
            return positive ? angle : angle + 180f;  // 음 방향은 반대쪽
        }

        // ============================================================
        // Phase 1 VFX 공통 유틸리티 메서드
        // - 모든 특수 블록 시스템에서 공통으로 사용하는 이펙트 패턴들
        // - 통일된 연출 품질을 위해 VisualConstants의 설정값을 활용합니다.
        // ============================================================

        /// <summary>
        /// 발동 전 압축(Pre-Fire Compression) 애니메이션.
        /// 블록이 발동되기 직전에 "힘을 모으는" 느낌의 크기 변화를 줍니다.
        ///
        /// 동작: 원래 크기(1.0) -> 압축(0.78) -> 팽창(1.18) -> 원래 크기(1.0)
        /// 비유: 스프링을 누르면 먼저 압축되었다가 튕겨 올라가는 것과 같습니다.
        /// </summary>
        /// <param name="block">애니메이션을 적용할 블록</param>
        private IEnumerator PreFireCompression(HexBlock block)
        {
            if (block == null) yield break;

            float elapsed = 0f;
            float duration = VisualConstants.PreFireDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.5f)
                {
                    // 전반부: 원래 크기 -> 압축 (EaseInQuad: 처음에 천천히, 끝에 빠르게)
                    float ct = t / 0.5f;
                    scale = Mathf.Lerp(1f, VisualConstants.PreFireScaleMin, VisualConstants.EaseInQuad(ct));
                }
                else
                {
                    // 후반부: 압축 -> 팽창 (EaseOutCubic: 처음에 빠르게 튕겨나감)
                    float et = (t - 0.5f) / 0.5f;
                    scale = Mathf.Lerp(VisualConstants.PreFireScaleMin, VisualConstants.PreFireScaleMax, VisualConstants.EaseOutCubic(et));
                }

                block.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            block.transform.localScale = Vector3.one;  // 원래 크기로 복원
        }

        /// <summary>
        /// 히트스톱(Hit Stop) 효과 - 게임 시간을 잠깐 멈추는 연출.
        /// 강한 공격이 터지는 순간 시간이 멈추고, 이후 슬로모션으로 복귀합니다.
        /// 쿨다운이 있어서 짧은 시간에 너무 자주 발동되지 않습니다.
        ///
        /// 비유: 만화나 애니메이션에서 강한 펀치가 맞는 순간 화면이 멈추는 것,
        ///       또는 축구에서 골이 들어가는 순간 슬로모션으로 보여주는 것과 같습니다.
        /// </summary>
        /// <param name="stopDuration">시간 정지 지속 시간 (초, 실시간 기준)</param>
        private IEnumerator HitStop(float stopDuration)
        {
            // 쿨다운 체크: 최근에 히트스톱이 발동했으면 건너뜀
            if (!VisualConstants.CanHitStop()) yield break;
            VisualConstants.RecordHitStop();

            // 시간 완전 정지
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(stopDuration);  // 실시간으로 대기 (게임 시간 멈춰도 동작)

            // 슬로모션에서 점진적으로 정상 속도로 복귀
            float elapsed = 0f;
            while (elapsed < VisualConstants.HitStopSlowMoDuration)
            {
                elapsed += Time.unscaledDeltaTime;  // 게임 시간이 아닌 실제 시간 사용
                float t = Mathf.Clamp01(elapsed / VisualConstants.HitStopSlowMoDuration);
                Time.timeScale = Mathf.Lerp(VisualConstants.HitStopSlowMoScale, 1f, VisualConstants.EaseOutCubic(t));
                yield return null;
            }
            Time.timeScale = 1f;  // 정상 속도 복원
        }

        /// <summary>
        /// 줌펀치(Zoom Punch) 효과 - 화면 전체가 살짝 확대되었다가 원래 크기로 돌아옵니다.
        /// 강한 충격이 발생했을 때 화면이 "출렁"하는 느낌을 줍니다.
        ///
        /// 비유: TV에서 폭발 장면이 나올 때 카메라가 순간적으로 줌인되었다가 돌아오는 것과 같습니다.
        /// </summary>
        /// <param name="targetScale">확대 배율 (1.0보다 클수록 더 많이 확대됨)</param>
        private IEnumerator ZoomPunch(float targetScale)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = target.localScale;       // 원래 크기 저장
            Vector3 punchScale = origScale * targetScale; // 확대된 크기

            // 확대 (Zoom In)
            float elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchInDuration);
                target.localScale = Vector3.Lerp(origScale, punchScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            // 축소 (Zoom Out) - 원래 크기로 복귀
            elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchOutDuration);
                target.localScale = Vector3.Lerp(punchScale, origScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            target.localScale = origScale;  // 정확히 원래 크기로 복원
        }

        /// <summary>
        /// 파괴 플래시 오버레이 - 블록이 파괴되는 순간 하얀 빛이 번쩍하는 효과.
        /// 블록 위에 흰색 반투명 이미지를 겹치고, 서서히 투명해지게 합니다.
        /// 비유: 사진 찍을 때 플래시가 터지는 것처럼, 파괴 순간 하얗게 빛나는 효과입니다.
        /// </summary>
        /// <param name="block">플래시를 표시할 블록</param>
        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            // 블록 위에 겹치는 흰색 이미지 생성
            GameObject flash = new GameObject("DestroyFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha);  // 반투명 흰색

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 60f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

            // 시간이 지남에 따라 투명해짐 (번쩍 -> 사라짐)
            float elapsed = 0f;
            while (elapsed < VisualConstants.DestroyFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.DestroyFlashDuration);
                img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha * (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 블룸(Bloom) 레이어 - 메인 플래시 뒤에 깔리는 큰 후광 효과.
        /// 메인 플래시보다 크고 약한 빛이 뒤에서 은은하게 빛나, 빛의 깊이감을 줍니다.
        ///
        /// 비유: 가로등 주변에 안개가 있을 때 빛이 넓게 퍼져 보이는 것과 같습니다.
        ///       메인 플래시가 "전구"라면, 블룸은 전구 주변의 "빛 번짐"입니다.
        /// </summary>
        /// <param name="pos">블룸 중심 위치</param>
        /// <param name="color">블룸 색상</param>
        /// <param name="initSize">초기 크기</param>
        /// <param name="duration">전체 지속 시간</param>
        private IEnumerator BloomLayer(Vector3 pos, Color color, float initSize, float duration)
        {
            // 메인 플래시보다 살짝 늦게 시작 (레이어링 효과)
            yield return new WaitForSeconds(VisualConstants.BloomLag);

            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject bloom = new GameObject("BloomLayer");
            bloom.transform.SetParent(parent, false);
            bloom.transform.position = pos;
            bloom.transform.SetAsFirstSibling();  // 다른 이펙트 뒤에 배치 (후광이므로)

            var img = bloom.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier);

            RectTransform rt = bloom.GetComponent<RectTransform>();
            float bloomSize = initSize * VisualConstants.BloomSizeMultiplier;  // 메인보다 큰 크기
            rt.sizeDelta = new Vector2(bloomSize, bloomSize);

            // 블룸 시작 지연 이후의 남은 시간 동안 확대 + 투명화
            float remainDuration = duration - VisualConstants.BloomLag;
            float elapsed = 0f;

            while (elapsed < remainDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / remainDuration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                rt.sizeDelta = new Vector2(bloomSize * scale, bloomSize * scale);
                img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier * (1f - t));
                yield return null;
            }

            Destroy(bloom);
        }
    }
}
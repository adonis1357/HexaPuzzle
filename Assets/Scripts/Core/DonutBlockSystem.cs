// ========================================================================
// DonutBlockSystem.cs - 도넛(무지개) 특수 블록 시스템
// ========================================================================
//
// [한 줄 요약]
// 같은 색상의 블록을 게임판 전체에서 한번에 제거하는 강력한 특수 블록입니다.
//
// [비유로 이해하기]
// 일반 블록이 "한 명씩 잡는 사냥꾼"이라면,
// 도넛 블록은 "같은 옷을 입은 사람을 전부 찾아내는 레이더"와 같습니다.
// 예를 들어 빨간색 도넛 블록이 발동하면, 게임판 위의 모든 빨간 블록이
// 중심에서부터 파도처럼 순서대로 사라집니다.
//
// [생성 조건]
// - 7개 이상의 블록이 한 번에 매칭되거나
// - 링(고리) 모양의 매칭 패턴이 감지될 때 자동 생성
//
// [발동 흐름]
// 1. 발동 전 "움찔" 압축 애니메이션 (Pre-Fire Compression)
// 2. 히트스탑 (잠깐 멈춤) + 줌 펀치 (화면 확대/축소) 연출
// 3. 도넛 블록 자체를 제거
// 4. 같은 색 블록을 모두 찾아 거리순으로 정렬
// 5. 중심에서부터 무지개 링 확산 이펙트 + 화면 흔들림
// 6. 가까운 블록부터 순서대로 파괴 (파도 효과)
// 7. 점수 계산 및 미션 시스템에 결과 전달
//
// [주요 이펙트]
// - 무지개색 회전 링, 무지개 플래시, 무지개 스파크(불꽃)
// - 블록 간 연결선 (레인보우 커넥션 라인)
// - 블록 파괴 시 흰색 플래시 + 회전하며 축소되는 애니메이션
//
// [관련 파일]
// - BlockData.cs: SpecialBlockType.Rainbow 열거형 정의
// - MatchingSystem.cs: 도넛 생성 조건 감지
// - BlockRemovalSystem.cs: 캐스케이드 루프에서 도넛 발동 호출
// - VisualConstants.cs: 이펙트 수치 상수 (크기, 속도, 지속시간 등)
// - HexBlock.cs: 블록 비주얼 및 데이터 관리
// ========================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 도넛(무지개/Rainbow) 특수 블록 시스템.
    ///
    /// 이 클래스는 게임에서 가장 강력한 특수 블록 중 하나인 "도넛 블록"을 담당합니다.
    /// 도넛 블록이 발동하면, 게임판 전체에서 같은 색상의 블록을 모두 찾아내어
    /// 중심에서부터 파도처럼 순차적으로 파괴합니다.
    ///
    /// 비유: 도넛 블록은 마치 "색상 감지 폭탄"과 같습니다.
    /// 빨간색 도넛이 터지면, 게임판 위의 모든 빨간 블록이 연쇄적으로 사라집니다.
    ///
    /// 생성 조건: 7개 이상 매칭 또는 링(고리) 모양 매칭 패턴 감지 시 생성됩니다.
    /// </summary>
    public class DonutBlockSystem : MonoBehaviour
    {
        // ============================================================
        // Inspector에서 설정하는 참조 (References)
        // ============================================================

        [Header("References")]
        /// <summary>
        /// 게임판(육각형 그리드)에 대한 참조.
        /// 블록 위치 조회, 같은 색 블록 검색 등에 사용됩니다.
        /// </summary>
        [SerializeField] private HexGrid hexGrid;

        /// <summary>
        /// 블록 제거 시스템에 대한 참조.
        /// 캐스케이드(연쇄 파괴) 깊이 정보를 가져와 이펙트 강도를 조절하는 데 사용됩니다.
        /// </summary>
        [SerializeField] private BlockRemovalSystem removalSystem;

        // ============================================================
        // Inspector에서 조절하는 설정값들
        // ============================================================

        [Header("Donut Settings")]
        /// <summary>
        /// 파도 효과의 딜레이 (초 단위).
        /// 블록을 순차적으로 파괴할 때, 각 블록 사이의 시간 간격입니다.
        /// 값이 작을수록 빠르게 연쇄 파괴됩니다.
        /// 비유: 도미노를 쓰러뜨릴 때 도미노 간의 간격과 비슷합니다.
        /// </summary>
        [SerializeField] private float waveDelay = 0.04f;

        /// <summary>
        /// 이펙트 링(고리)의 회전 속도 (도/초).
        /// 도넛 발동 시 중심에서 회전하는 무지개 링의 속도입니다.
        /// </summary>
        [SerializeField] private float ringRotationSpeed = 360f;

        [Header("Effect Settings")]
        /// <summary>
        /// 중심 폭발 시 생성되는 무지개 스파크(불꽃) 개수.
        /// 많을수록 화려한 이펙트가 연출됩니다.
        /// </summary>
        [SerializeField] private int sparkCount = 24;

        // ============================================================
        // 이벤트
        // ============================================================

        /// <summary>
        /// 도넛 블록의 발동이 완전히 끝났을 때 호출되는 이벤트.
        /// int 파라미터는 획득한 총 점수입니다.
        /// 외부 시스템(점수 매니저 등)이 이 이벤트를 구독하여 점수를 반영합니다.
        /// </summary>
        public event System.Action<int> OnDonutComplete;

        // ============================================================
        // 내부 상태 추적 변수들
        // ============================================================

        /// <summary>
        /// 현재 발동 중인 도넛 블록의 수.
        /// 여러 도넛이 동시에 발동할 수 있으므로 카운터로 추적합니다.
        /// 0보다 크면 "아직 도넛이 작업 중"이라는 뜻입니다.
        /// </summary>
        private int activeDonutCount = 0;

        /// <summary>
        /// 도넛에 의해 발견된 "대기 중인 특수 블록" 목록.
        /// 도넛이 같은 색 블록을 파괴하다가 다른 특수 블록(폭탄, 드릴 등)을 만나면,
        /// 즉시 파괴하지 않고 이 목록에 넣어둡니다.
        /// 나중에 BlockRemovalSystem이 이 목록을 확인하여 연쇄 발동시킵니다.
        /// 비유: "나중에 터뜨릴 폭탄 목록"과 같습니다.
        /// </summary>
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        /// <summary>
        /// 현재 도넛 효과가 진행 중인 블록들의 집합.
        /// 같은 블록이 중복으로 처리되는 것을 방지합니다.
        /// </summary>
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        // ============================================================
        // 외부에서 상태를 확인할 수 있는 프로퍼티들
        // ============================================================

        /// <summary>
        /// 도넛 블록이 현재 발동 중인지 여부.
        /// true면 "아직 도넛 효과가 진행 중"이라는 뜻입니다.
        /// GameManager 등이 이 값을 확인하여 다음 턴으로 넘어가지 않도록 합니다.
        /// </summary>
        public bool IsActivating => activeDonutCount > 0;

        /// <summary>
        /// 도넛이 발견한 "대기 중인 특수 블록" 목록에 대한 외부 접근자.
        /// BlockRemovalSystem이 이 목록을 읽어 연쇄 발동을 처리합니다.
        /// </summary>
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;

        /// <summary>
        /// 특정 블록이 현재 도넛 효과의 대상인지 확인합니다.
        /// 다른 시스템이 동일 블록을 중복 처리하지 않도록 방지하는 용도입니다.
        /// </summary>
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);

        /// <summary>
        /// 도넛 시스템을 강제로 초기화합니다.
        ///
        /// 게임이 비정상적으로 멈추거나, 스테이지가 강제 종료될 때 호출됩니다.
        /// 모든 진행 중인 코루틴(애니메이션)을 중단하고,
        /// 남아 있는 이펙트 오브젝트를 제거하며,
        /// 내부 상태를 깨끗하게 초기화합니다.
        ///
        /// 비유: 공연 도중 갑자기 조명을 끄고 무대를 정리하는 것과 같습니다.
        /// </summary>
        public void ForceReset()
        {
            activeDonutCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.Log("[DonutBlockSystem] ForceReset called");
        }

        /// <summary>
        /// 화면에 남아 있는 모든 이펙트 오브젝트를 일괄 삭제합니다.
        ///
        /// 코루틴이 중간에 중단되면 이펙트 오브젝트가 화면에 남을 수 있습니다.
        /// 이 메서드는 이펙트 전용 부모(effectParent) 아래의 모든 자식 오브젝트를
        /// 역순으로 순회하며 삭제합니다.
        ///
        /// 비유: 파티가 끝난 후 장식품을 한꺼번에 치우는 청소부와 같습니다.
        /// </summary>
        private void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);
        }


        /// <summary>
        /// 이펙트 오브젝트들의 부모 Transform.
        /// 모든 시각 효과(스파크, 링, 플래시 등)는 이 오브젝트 아래에 생성됩니다.
        /// 이렇게 하면 이펙트를 한곳에서 관리하고 정리하기 쉽습니다.
        /// </summary>
        private Transform effectParent;

        /// <summary>
        /// 도넛 아이콘 스프라이트의 정적 캐시.
        /// 한 번 생성하면 게임이 끝날 때까지 재사용합니다.
        /// static이므로 모든 DonutBlockSystem 인스턴스가 같은 스프라이트를 공유합니다.
        /// </summary>
        private static Sprite donutIconSprite;

        // ============================================================
        // 초기화 (Unity 생명주기)
        // ============================================================

        /// <summary>
        /// Unity가 게임 시작 시 자동으로 호출하는 초기화 메서드.
        ///
        /// 필요한 참조(HexGrid, BlockRemovalSystem)를 자동으로 찾고,
        /// 이펙트를 표시할 전용 레이어를 설정합니다.
        /// Inspector에서 참조를 미리 연결하지 않았더라도,
        /// 씬(Scene)에서 자동으로 찾아 연결합니다.
        /// </summary>
        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[DonutBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[DonutBlockSystem] HexGrid not found!");
            }
            if (removalSystem == null)
                removalSystem = FindObjectOfType<BlockRemovalSystem>();

            SetupEffectParent();
        }

        /// <summary>
        /// 이펙트 전용 레이어(부모 오브젝트)를 생성합니다.
        ///
        /// Canvas(UI 캔버스) 위에 "DonutEffectLayer"라는 전용 레이어를 만들어서,
        /// 모든 도넛 이펙트가 이 레이어 위에 표시되도록 합니다.
        /// 이렇게 하면 이펙트가 항상 블록 위에 그려지고,
        /// 정리할 때도 이 레이어만 비우면 됩니다.
        ///
        /// 비유: 화가가 그림 위에 투명 필름을 올려놓고 그 위에 특수 효과를 그리는 것과 같습니다.
        /// </summary>
        private void SetupEffectParent()
        {
            if (hexGrid == null) return;

            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("DonutEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);

                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                // 캔버스 전체를 덮도록 앵커를 꽉 채움 (좌하단~우상단)
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                // 가장 위에 표시되도록 마지막 자식으로 설정
                effectLayer.transform.SetAsLastSibling();
                effectParent = effectLayer.transform;
            }
            else
            {
                // Canvas를 못 찾으면 HexGrid 자체를 부모로 사용 (폴백)
                effectParent = hexGrid.transform;
            }
        }

        // ============================================================
        // 도넛 아이콘 스프라이트 생성
        // - 코드로 직접 도넛 모양 이미지를 만듭니다 (외부 이미지 파일 불필요)
        // ============================================================

        /// <summary>
        /// 도넛 아이콘 스프라이트를 반환합니다.
        /// 이미 만들어진 것이 있으면 재사용하고, 없으면 새로 생성합니다.
        /// UI에서 도넛 블록 아이콘을 표시할 때 사용됩니다.
        /// </summary>
        public static Sprite GetDonutIconSprite()
        {
            if (donutIconSprite == null)
                donutIconSprite = CreateDonutSprite(128);
            return donutIconSprite;
        }

        /// <summary>
        /// 도넛 모양의 스프라이트를 코드로 직접 그려서 생성합니다 (프로시저럴 생성).
        ///
        /// 외부 이미지 파일 없이 픽셀 단위로 도넛 모양을 그립니다:
        /// - 바깥 원과 안쪽 원 사이의 링(고리) 영역에 무지개 그라디언트를 칠합니다.
        /// - 각도에 따라 색상이 변하므로 무지개색 도넛이 됩니다.
        /// - 3D 입체감을 위해 링 중심부에 하이라이트(밝은 부분)를 추가합니다.
        /// - 테두리가 부드럽게 보이도록 안티앨리어싱(가장자리 흐림) 처리를 합니다.
        /// - 도넛 중앙에 작은 빛나는 점을 추가합니다.
        ///
        /// 비유: 컴퓨터가 붓 대신 수학 공식으로 도넛 그림을 그리는 것과 같습니다.
        /// </summary>
        /// <param name="size">스프라이트의 가로/세로 크기 (픽셀 단위, 정사각형)</param>
        /// <returns>생성된 도넛 모양 스프라이트</returns>
        private static Sprite CreateDonutSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            // 모든 픽셀을 투명으로 초기화
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);  // 이미지 중심점
            float outerRadius = size * 0.42f;  // 바깥쪽 원의 반지름
            float innerRadius = size * 0.22f;  // 안쪽 원의 반지름 (이 사이가 도넛 링)

            // 모든 픽셀을 순회하며 도넛 모양을 그림
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float dist = Vector2.Distance(p, center);  // 현재 픽셀과 중심 사이 거리

                    // 도넛 링 영역 (안쪽 원~바깥 원 사이에만 색을 칠함)
                    if (dist >= innerRadius && dist <= outerRadius)
                    {
                        // 무지개 그라디언트: 각도에 따라 HSV 색상환에서 색 결정
                        // 12시 방향은 빨강, 시계 방향으로 주황→노랑→초록→파랑→보라 순
                        float angle = Mathf.Atan2(p.y - center.y, p.x - center.x);
                        float hue = (angle / (2f * Mathf.PI) + 1f) % 1f;
                        Color c = Color.HSVToRGB(hue, 0.30f, 0.95f); // 파스텔톤 무지개 (채도 낮게)

                        // 3D 입체감 효과: 링의 정중앙 부분을 약간 밝게 (볼록해 보이는 효과)
                        float ringCenter = (outerRadius + innerRadius) * 0.5f;
                        float ringT = 1f - Mathf.Abs(dist - ringCenter) / ((outerRadius - innerRadius) * 0.5f);
                        float highlight = Mathf.Pow(ringT, 2f) * 0.20f;
                        c = new Color(
                            Mathf.Min(1f, c.r + highlight),
                            Mathf.Min(1f, c.g + highlight),
                            Mathf.Min(1f, c.b + highlight),
                            1f
                        );

                        // 엣지 안티앨리어싱: 도넛 테두리를 부드럽게 처리
                        // 바깥쪽/안쪽 경계에서 투명도를 점진적으로 변화시킴
                        float outerAA = Mathf.Clamp01((outerRadius - dist) * 2f);
                        float innerAA = Mathf.Clamp01((dist - innerRadius) * 2f);
                        c.a = outerAA * innerAA;

                        pixels[y * size + x] = c;
                    }

                    // 중앙 빛나는 점: 도넛 구멍 한가운데에 작은 빛 포인트
                    float centerDist = Vector2.Distance(p, center);
                    if (centerDist < size * 0.08f)
                    {
                        float sa = Mathf.Clamp01((size * 0.08f - centerDist) / (size * 0.08f));
                        Color sparkColor = new Color(1f, 1f, 0.9f, sa * 0.6f);  // 따뜻한 흰색 빛
                        pixels[y * size + x] = Color.Lerp(pixels[y * size + x], sparkColor, sa);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 도넛 블록 생성
        // - 매칭 조건을 만족하면 MatchingSystem이 이 메서드를 호출합니다
        // ============================================================

        /// <summary>
        /// 지정된 블록을 도넛(무지개) 특수 블록으로 변환합니다.
        ///
        /// 기존 블록의 데이터를 도넛 블록 데이터로 교체합니다.
        /// gemType은 나중에 발동할 때 "어떤 색을 제거할지" 결정하는 데 사용됩니다.
        ///
        /// 비유: 일반 병사에게 "무지개 폭탄"을 장착시키는 것과 같습니다.
        /// </summary>
        /// <param name="block">도넛으로 변환할 블록</param>
        /// <param name="gemType">이 도넛이 제거할 대상 색상 (예: Red이면 모든 빨간 블록 제거)</param>
        public void CreateDonutBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData donutData = new BlockData(gemType);
            donutData.specialType = SpecialBlockType.Rainbow;  // "무지개" 특수 블록으로 지정
            block.SetBlockData(donutData);
            Debug.Log($"[DonutBlockSystem] Created donut(rainbow) at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // 도넛 발동 (핵심 로직)
        // - 플레이어가 도넛 블록을 매칭하면 호출됩니다
        // ============================================================

        /// <summary>
        /// 도넛 블록을 발동시킵니다.
        ///
        /// 유효성 검사 후 비동기 코루틴(DonutCoroutine)을 시작합니다.
        /// 코루틴을 사용하는 이유는, 파괴 이펙트가 시간에 걸쳐 순차적으로
        /// 진행되어야 하기 때문입니다 (한 프레임에 다 끝나면 이펙트가 안 보임).
        /// </summary>
        /// <param name="donutBlock">발동시킬 도넛 블록</param>
        public void ActivateDonut(HexBlock donutBlock)
        {
            if (donutBlock == null) return;
            if (donutBlock.Data == null || donutBlock.Data.specialType != SpecialBlockType.Rainbow)
                return;
            Debug.Log($"[DonutBlockSystem] Activating donut at {donutBlock.Coord}");
            StartCoroutine(DonutCoroutine(donutBlock));
        }

        /// <summary>
        /// 도넛 발동의 전체 흐름을 담당하는 핵심 코루틴.
        ///
        /// [실행 순서 상세]
        /// 1단계: Pre-Fire 압축 애니메이션 (블록이 움찔하며 힘을 모으는 연출)
        /// 2단계: 히트스탑(순간 정지) + 줌 펀치(화면 확대/축소)로 임팩트 연출
        /// 3단계: 도넛 블록 자체의 데이터를 제거 (빈 칸으로 변환)
        /// 4단계: 게임판 전체에서 같은 색 블록을 모두 수집
        /// 5단계: 중심 이펙트(무지개 플래시 + 회전 링) + 링 확산 이펙트 + 화면 흔들림
        /// 6단계: 수집한 블록을 거리순으로 정렬 (가까운 것부터 파괴)
        /// 7단계: 각 블록을 순차적으로 파괴 (waveDelay 간격으로 파도 효과)
        ///        - 일반 블록: 무지개 이펙트와 함께 파괴
        ///        - 특수 블록: pendingSpecialBlocks에 추가 (나중에 연쇄 발동)
        /// 8단계: 모든 파괴 애니메이션 완료 대기
        /// 9단계: 점수 계산 (기본 500점 + 파괴된 블록 티어별 점수 합산)
        /// 10단계: 미션 시스템에 결과 알림, 완료 이벤트 발생
        ///
        /// 비유: 폭탄이 "카운트다운 → 폭발 → 충격파 확산 → 잔해 처리 → 결과 보고"
        ///       순서로 진행되는 것과 같습니다.
        /// </summary>
        private IEnumerator DonutCoroutine(HexBlock donutBlock)
        {
            // --- 상태 등록: "나 지금 작업 시작합니다" ---
            activeDonutCount++;
            activeBlocks.Add(donutBlock);

            // 발동 위치, 색상 등 기본 정보 저장
            HexCoord donutCoord = donutBlock.Coord;           // 도넛의 그리드 좌표
            Vector3 donutWorldPos = donutBlock.transform.position;  // 도넛의 화면상 위치
            GemType targetGemType = donutBlock.Data.gemType;  // 제거할 대상 색상
            Color donutColor = GemColors.GetColor(targetGemType);   // 대상 색상의 실제 Color 값

            Debug.Log($"[DonutBlockSystem] === DONUT ACTIVATED === Coord={donutCoord}, TargetColor={targetGemType}");

            // [1단계] Pre-Fire 압축 애니메이션
            // 블록이 1.0 → 0.78 → 1.18 크기로 변하며 "힘을 모으는" 느낌 연출
            yield return StartCoroutine(PreFireCompression(donutBlock));

            // [2단계] 히트스탑 + 줌 펀치 (동시에 실행)
            // 히트스탑: 게임 시간을 잠깐 멈춰서 "타격감" 연출
            StartCoroutine(HitStop(VisualConstants.HitStopDurationMedium));
            // 줌 펀치: 게임판 전체가 살짝 확대됐다 돌아오는 연출
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // [3단계] 도넛 블록 자체를 제거 (빈 칸으로 만듦)
            donutBlock.ClearData();

            // [4단계] 같은 색 블록 전부 수집
            // 게임판의 모든 블록을 순회하며 같은 gemType인 블록을 찾음
            List<HexBlock> targets = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;  // 빈 블록 무시
                    if (block == donutBlock) continue;  // 자기 자신 제외

                    if (block.Data.gemType == targetGemType)
                        targets.Add(block);
                }
            }

            Debug.Log($"[DonutBlockSystem] Targets: {targets.Count} same-color blocks ({targetGemType})");

            // [5단계] 화려한 이펙트 동시 발생
            // 중심에서 무지개 플래시 + 회전 링 이펙트
            StartCoroutine(DonutCenterEffect(donutWorldPos, donutColor));
            // 중심에서 바깥으로 퍼지는 무지개 링 확산 이펙트
            StartCoroutine(RainbowRingExpand(donutWorldPos));
            // 화면 흔들림 (임팩트감 강화)
            StartCoroutine(ScreenShake(VisualConstants.ShakeMediumIntensity, VisualConstants.ShakeMediumDuration));

            // 이펙트가 살짝 퍼진 후 파괴 시작 (0.15초 대기)
            yield return new WaitForSeconds(0.15f);

            // [6단계] 거리순 정렬: 도넛 중심에서 가까운 블록부터 파괴
            // 이렇게 하면 "중심에서 바깥으로 퍼지는 파도" 효과가 자연스럽게 연출됨
            targets.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, donutWorldPos);
                float distB = Vector3.Distance(b.transform.position, donutWorldPos);
                return distA.CompareTo(distB);
            });

            // [7단계] 파도처럼 순차 파괴
            List<Coroutine> destroyCoroutines = new List<Coroutine>();  // 파괴 애니메이션 추적용
            int blockScoreSum = 0;     // 파괴된 블록들의 티어별 점수 합계
            int basicBlockCount = 0;   // 기본 블록(일반 색상 1~5번) 카운트
            var gemCountsByColor = new Dictionary<GemType, int>(); // 색상별 미션 카운팅용

            // 파괴 전에 색상별 미션 카운트를 사전 수집 (파도 루프 시작 전)
            foreach (var preTarget in targets)
            {
                if (preTarget == null || preTarget.Data == null || preTarget.Data.gemType == GemType.None) continue;
                if (preTarget.Data.specialType == SpecialBlockType.FixedBlock) continue;
                if (preTarget.Data.specialType != SpecialBlockType.None) continue; // 특수 블록 제외
                if ((int)preTarget.Data.gemType >= 1 && (int)preTarget.Data.gemType <= 5)
                {
                    if (gemCountsByColor.ContainsKey(preTarget.Data.gemType))
                        gemCountsByColor[preTarget.Data.gemType]++;
                    else
                        gemCountsByColor[preTarget.Data.gemType] = 1;
                }
            }
            // 색상별 미션 카운팅 — 파괴 애니메이션 시작 직후, 완료 대기 전에 호출
            GameManager.Instance?.OnSpecialBlockDestroyedBlocksByColor(gemCountsByColor, "Donut");

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;
                if (target.Data == null || target.Data.gemType == GemType.None) continue;

                // FixedBlock(고정 블록)은 파괴 불가 - 건너뛰기
                // 비유: 철벽처럼 단단한 블록은 도넛으로도 깰 수 없음
                if (target.Data.specialType == SpecialBlockType.FixedBlock)
                    continue;

                // 다른 특수 블록(폭탄, 드릴 등)을 만나면 바로 파괴하지 않고 "대기열"에 추가
                // 나중에 BlockRemovalSystem이 이 대기열을 확인하여 연쇄 발동시킴
                // 비유: 도넛이 폭탄을 발견하면 "이건 내가 처리하지 말고 폭탄 전문가한테 넘기자"
                if (target.Data.specialType != SpecialBlockType.None)
                {
                    Debug.Log($"[DonutBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
                    if (!pendingSpecialBlocks.Contains(target))
                    {
                        pendingSpecialBlocks.Add(target);
                        target.SetPendingActivation();     // "곧 발동될 예정" 상태로 표시
                        target.StartWarningBlink(10f);     // 깜빡임으로 곧 터질 것임을 시각적으로 알림
                    }
                }
                else
                {
                    // 일반 블록: 점수 계산 후 무지개 이펙트와 함께 파괴
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);

                    // 기본 블록 카운트 (GemType 1~5: Red, Blue, Green, Yellow, Purple)
                    if ((int)target.Data.gemType >= 1 && (int)target.Data.gemType <= 5)
                    {
                        basicBlockCount++;
                        if (gemCountsByColor.ContainsKey(target.Data.gemType))
                            gemCountsByColor[target.Data.gemType]++;
                        else
                            gemCountsByColor[target.Data.gemType] = 1;
                    }

                    // 적군 블록에 대한 추가 점수 처리
                    // (가시 기생충, 사슬 앵커, 색 포식자 등 특수 상태의 블록)
                    var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                    if (sm != null)
                    {
                        if (target.Data.hasThorn)   // 가시(Thorn) 상태 블록
                            sm.AddEnemyScore(EnemyType.ThornParasite, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        if (target.Data.hasChain)   // 사슬(Chain) 상태 블록
                            sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        if (target.Data.gemType == GemType.Gray)  // 회색(Chromophage) 블록
                            sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                    }

                    // 무지개 파괴 이펙트 시작 (비동기 - 동시에 여러 블록이 파괴 진행)
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithRainbow(target, blockColor, donutWorldPos)));
                }

                // waveDelay만큼 대기 후 다음 블록 처리 (파도 효과)
                yield return new WaitForSeconds(waveDelay);
            }

            // [8단계] 모든 파괴 애니메이션이 완전히 끝날 때까지 대기
            // 이렇게 해야 블록의 ClearData()가 확실히 호출된 후 다음 단계로 넘어감
            foreach (var co in destroyCoroutines)
                yield return co;

            // [9단계] 최종 점수 계산
            // 도넛 기본 보너스 500점 + 파괴된 블록들의 티어별 점수 합산
            int totalScore = 500 + blockScoreSum;
            Debug.Log($"[DonutBlockSystem] === DONUT COMPLETE === Score={totalScore} (base:500 + blockTierSum:{blockScoreSum}), Destroyed={targets.Count}");

            // [10단계] 미션 시스템에 파괴 결과 전달 (기본 블록만)
            // 미션 진행도 업데이트를 위해, 파괴된 기본 블록 목록을 전달함
            // 특수 블록은 미션 카운트에서 제외 (별도 처리됨)
            List<HexBlock> basicBlocksOnly = new List<HexBlock>();
            foreach (var target in targets)
            {
                if (target != null && target.Data != null && target.Data.gemType != GemType.None)
                {
                    // 기본 블록만 포함: 특수블록 제외 (FixedBlock은 파괴 안 됐지만 안전하게 포함)
                    if (target.Data.specialType == SpecialBlockType.None || target.Data.specialType == SpecialBlockType.FixedBlock)
                    {
                        basicBlocksOnly.Add(target);
                        Debug.Log($"[DonutBlockSystem]   BasicBlock: {target.Coord}, gemType={target.Data.gemType}");
                    }
                }
            }

            Debug.Log($"[DonutBlockSystem] Passing {basicBlocksOnly.Count} basic blocks to MissionSystem (from {targets.Count} total targets)");

            // 미션 시스템에 알림 (예: "빨간 블록 10개 수집" 미션 진행도 갱신)
            MissionSystem ms = Object.FindObjectOfType<MissionSystem>();
            if (ms != null)
                ms.OnSpecialBlockDestroyedBlocks(basicBlocksOnly);

            // 완료 이벤트 발생 (점수 매니저 등 구독자에게 점수 전달)
            OnDonutComplete?.Invoke(totalScore);

            // --- 상태 해제: "작업 끝났습니다" ---
            activeBlocks.Remove(donutBlock);
            activeDonutCount--;
        }

        // ============================================================
        // 이펙트 메서드들
        // - 도넛 발동 시 화면에 표시되는 시각 효과를 담당합니다
        // - 모든 이펙트는 UI.Image 기반이며 코루틴으로 애니메이션합니다
        // ============================================================

        /// <summary>
        /// 도넛 중심에서 발생하는 메인 이펙트입니다.
        ///
        /// 세 가지 시각 요소로 구성됩니다:
        /// 1. 무지개 플래시: 중심에서 커지며 퍼지는 밝은 원, 색이 무지개처럼 변합니다
        /// 2. 회전 링 (4겹): 서로 다른 색상의 링이 반대 방향으로 회전하며 확대됩니다
        /// 3. 무지개 스파크 버스트: 무지개색 불꽃이 사방으로 튀어나갑니다
        ///
        /// 추가로 블룸(Bloom) 레이어가 플래시 뒤에 깔려 은은한 광채를 만듭니다.
        ///
        /// 비유: 불꽃놀이의 중심부 - 밝은 빛이 퍼지고, 고리가 회전하고, 불꽃이 튑니다.
        /// </summary>
        /// <param name="pos">이펙트가 표시될 월드 좌표 (도넛 블록의 위치)</param>
        /// <param name="color">도넛 블록의 대상 색상</param>
        private IEnumerator DonutCenterEffect(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 레이어: 플래시 뒤에 깔리는 은은한 글로우 (먼저 시작해서 뒤에 표시)
            StartCoroutine(BloomLayer(pos, new Color(1f, 1f, 0.8f), VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // 1) 무지개 플래시: 중심에서 확장되며 색이 무지개처럼 변하는 밝은 원
            GameObject flash = new GameObject("DonutFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.sprite = HexBlock.GetHexFlashSprite();  // 육각형 모양 기본 스프라이트
            flashImg.raycastTarget = false;  // 터치 입력을 가로채지 않도록 설정
            flashImg.color = new Color(1f, 1f, 0.8f, 1f);  // 따뜻한 흰색

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            flashRt.sizeDelta = new Vector2(initSize, initSize);

            // 2) 회전 링 (4겹): 서로 다른 색의 링이 반대 방향으로 회전
            int ringCount = 4;
            GameObject[] rings = new GameObject[ringCount];
            for (int r = 0; r < ringCount; r++)
            {
                rings[r] = CreateDonutRing(pos, parent, r);
            }

            // 3) 스파크 버스트: 무지개색 불꽃이 사방으로 퍼짐
            // 캐스케이드(연쇄) 깊이에 따라 불꽃 수가 증가 (연쇄가 깊을수록 더 화려해짐)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int totalSparks = Mathf.RoundToInt(sparkCount * cascadeMult);
            for (int i = 0; i < totalSparks; i++)
            {
                // 각 스파크에 고른 무지개색 할당 (0번=빨강, 중간=초록, 마지막=보라)
                float hue = (float)i / totalSparks;
                Color sparkColor = Color.HSVToRGB(hue, 0.9f, 1f);
                StartCoroutine(RainbowSpark(pos, sparkColor));
            }

            // --- 시간에 따른 애니메이션 루프 ---
            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);  // 0~1 진행률
                float eased = VisualConstants.EaseOutCubic(t);  // 부드러운 감속 커브

                // 플래시 애니메이션: 커지면서 + 페이드아웃 + 무지개색 변화
                float flashScale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                flashRt.sizeDelta = new Vector2(initSize * flashScale, initSize * flashScale);
                float hue = (t * 2f) % 1f;  // 시간에 따라 색상환을 2바퀴 회전
                Color flashColor = Color.HSVToRGB(hue, 0.5f, 1f);
                flashColor.a = (1f - t) * 0.8f;  // 시간이 지나면서 투명해짐
                flashImg.color = flashColor;

                // 각 링의 애니메이션: 회전 + 확장 + 투명해짐
                for (int r = 0; r < ringCount; r++)
                {
                    if (rings[r] == null) continue;
                    float ringT = Mathf.Clamp01(t - r * 0.08f);  // 링마다 약간의 시간차
                    float ringEased = VisualConstants.EaseOutCubic(ringT);
                    float ringScale = 1f + ringEased * (8f + r * 2.5f);  // 바깥 링일수록 더 크게 확장
                    var rt = rings[r].GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(15f * ringScale, 15f * ringScale);
                    // 짝수 링은 시계 방향, 홀수 링은 반시계 방향 회전
                    rings[r].transform.localRotation = Quaternion.Euler(0, 0, elapsed * ringRotationSpeed * (r % 2 == 0 ? 1 : -1));
                    var img = rings[r].GetComponent<UnityEngine.UI.Image>();
                    if (img != null)
                    {
                        Color rc = img.color;
                        rc.a = (1f - ringT) * 0.6f;  // 시간이 지나면서 투명해짐
                        img.color = rc;
                    }
                }

                yield return null;  // 다음 프레임까지 대기
            }

            // 애니메이션 종료 후 오브젝트 정리
            Destroy(flash);
            for (int r = 0; r < ringCount; r++)
                if (rings[r] != null) Destroy(rings[r]);
        }

        /// <summary>
        /// 도넛 회전 링 하나를 생성합니다.
        ///
        /// DonutCenterEffect에서 호출되며, 각 링은 서로 다른 무지개색을 가집니다.
        /// index에 따라 빨강, 초록, 파랑 등의 색상이 할당됩니다.
        ///
        /// 비유: 올림픽 오륜기처럼 겹쳐진 색색의 링을 하나씩 만드는 것입니다.
        /// </summary>
        /// <param name="pos">링의 중심 위치</param>
        /// <param name="parent">링 오브젝트의 부모 Transform</param>
        /// <param name="index">링 번호 (0부터 시작, 색상 결정에 사용)</param>
        /// <returns>생성된 링 게임오브젝트</returns>
        private GameObject CreateDonutRing(Vector3 pos, Transform parent, int index)
        {
            GameObject ring = new GameObject($"DonutRing_{index}");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;

            var img = ring.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;

            // index에 따라 색상환에서 균등하게 분배된 색상 할당
            float hue = (float)index / 3f;
            Color c = Color.HSVToRGB(hue, 0.8f, 1f);  // 선명한 무지개색
            c.a = 0.7f;  // 약간 투명하게
            img.color = c;

            RectTransform rt = ring.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(15f, 15f);  // 초기 크기 (이후 애니메이션으로 확대됨)

            return ring;
        }

        /// <summary>
        /// 도넛 중심에서 게임판 전체로 퍼지는 무지개 링 확산 이펙트.
        ///
        /// 큰 원이 중심에서부터 빠르게 커지면서 회전합니다.
        /// 색상은 시간에 따라 무지개처럼 변하고, 점점 투명해집니다.
        ///
        /// 이 이펙트는 "충격파"의 시각적 표현입니다.
        /// 도넛이 발동했다는 것을 플레이어에게 확실히 알려주는 역할을 합니다.
        ///
        /// 비유: 물에 돌을 던졌을 때 퍼지는 파문(물결)과 같습니다.
        /// </summary>
        /// <param name="center">확산의 중심점 (도넛 블록의 위치)</param>
        private IEnumerator RainbowRingExpand(Vector3 center)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            float initSize = VisualConstants.WaveLargeInitialSize;

            // 확산 링 오브젝트 생성
            GameObject ring = new GameObject("RainbowExpand");
            ring.transform.SetParent(parent, false);
            ring.transform.position = center;

            var img = ring.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.8f, 0.2f, VisualConstants.WaveLargeAlpha);  // 금색 계열 시작

            RectTransform rt = ring.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(initSize, initSize);

            float duration = 0.6f;  // 확산 총 소요 시간
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                // 시간에 따라 30배까지 확대 (게임판 전체를 덮을 정도로 커짐)
                float scale = 1f + eased * 30f;
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);

                // 무지개색 변화: 시간에 따라 색상환을 3바퀴 회전
                float hue = (t * 3f) % 1f;
                Color c = Color.HSVToRGB(hue, 0.7f, 1f);
                c.a = (1f - t) * 0.4f;  // 커지면서 점점 투명해짐
                img.color = c;

                // 확산하면서 180도 회전 (시각적 다이나미즘)
                ring.transform.localRotation = Quaternion.Euler(0, 0, t * 180f);

                yield return null;
            }

            Destroy(ring);
        }

        /// <summary>
        /// 개별 블록을 무지개 이펙트와 함께 파괴하는 애니메이션.
        ///
        /// 각 블록이 파괴될 때 다음 연출이 동시에 진행됩니다:
        /// 1. 흰색 플래시 오버레이 (순간적으로 하얗게 빛남)
        /// 2. 무지개 연결선 (도넛 중심 → 이 블록까지 빛줄기)
        /// 3. 무지개 스파크 (블록 위치에서 불꽃이 튐)
        /// 4. 이중 이징 파괴 애니메이션:
        ///    - 전반부 (20%): 살짝 커짐 (부풀어 오르는 느낌)
        ///    - 후반부 (80%): 좌우로 찌그러지며 작아져서 사라짐
        /// 5. 미세 회전: 30도까지 살짝 회전 (무지개 특유의 개성)
        ///
        /// 비유: 비눗방울이 터질 때 잠깐 빛나다가 찌그러지며 사라지는 것과 같습니다.
        /// </summary>
        /// <param name="block">파괴할 블록</param>
        /// <param name="blockColor">블록의 색상 (이펙트 색상에 사용)</param>
        /// <param name="center">도넛 중심 위치 (연결선의 시작점)</param>
        private IEnumerator DestroyBlockWithRainbow(HexBlock block, Color blockColor, Vector3 center)
        {
            if (block == null) yield break;

            Vector3 blockPos = block.transform.position;

            // 동시에 여러 이펙트 시작
            StartCoroutine(DestroyFlashOverlay(block));           // 1. 흰색 플래시
            StartCoroutine(RainbowConnectionLine(center, blockPos));  // 2. 무지개 연결선

            // 3. 무지개 스파크 (캐스케이드 깊이에 따라 개수 증가)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkSmallCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
            {
                float hue = Random.Range(0f, 1f);  // 랜덤 무지개색
                Color sparkColor = Color.HSVToRGB(hue, 0.9f, 1f);
                StartCoroutine(RainbowSpark(blockPos, sparkColor));
            }

            // 4. 이중 이징 파괴 애니메이션 + 5. 미세 회전
            float duration = VisualConstants.DestroyDuration;
            float elapsed = 0f;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;  // 확장 단계 비율 (약 20%)
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (t < expandRatio)
                {
                    // === 전반부: 확장 단계 (부풀어 오름) ===
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = origScale * scale;
                }
                else
                {
                    // === 후반부: 찌그러지며 축소 단계 (사라짐) ===
                    float shrinkT = (t - expandRatio) / (1f - expandRatio);
                    // 가로(sx): sin 곡선으로 좌우 펄럭임 (찌그러짐)
                    float sx = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(shrinkT * Mathf.PI);
                    // 세로(sy): 위아래로 줄어들어 납작해짐
                    float sy = Mathf.Max(0f, 1f - VisualConstants.EaseInQuad(shrinkT));
                    block.transform.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);
                }

                // 미세 회전: 30도까지 살짝 돌아감 (무지개/도넛 고유 개성)
                float rot = t * 30f;
                block.transform.localRotation = Quaternion.Euler(0, 0, rot);

                yield return null;
            }

            // 파괴 완료: 원래 크기/회전으로 복원 후 데이터 제거
            block.transform.localScale = Vector3.one;
            block.transform.localRotation = Quaternion.identity;
            block.ClearData();  // 블록 데이터 초기화 (빈 칸으로 만듦)
        }

        /// <summary>
        /// 무지개색 스파크(불꽃) 하나의 애니메이션.
        ///
        /// 중심점에서 랜덤 방향으로 튀어나가며,
        /// 시간이 지남에 따라 색이 무지개처럼 변하고, 점점 느려지다 사라집니다.
        ///
        /// DonutCenterEffect와 DestroyBlockWithRainbow에서 여러 개가 동시에 생성되어
        /// "불꽃 터짐" 효과를 연출합니다.
        ///
        /// 비유: 불꽃놀이에서 터진 후 사방으로 흩어지는 작은 불씨 하나와 같습니다.
        /// </summary>
        /// <param name="center">스파크가 시작되는 중심 위치</param>
        /// <param name="color">스파크의 초기 색상</param>
        private IEnumerator RainbowSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject spark = new GameObject("DonutSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = color;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkMediumSizeMin, VisualConstants.SparkMediumSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            // 랜덤 방향과 속도로 발사
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkMediumSpeedMin, VisualConstants.SparkMediumSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(VisualConstants.SparkMediumLifetimeMin, VisualConstants.SparkMediumLifetimeMax);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;  // 0~1 진행률

                // 위치 이동 (속도에 감속 적용 - 점점 느려짐)
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= VisualConstants.SparkDeceleration;  // 매 프레임 속도 감소

                // 무지개 색상 회전: HSV 색상값(Hue)을 시간에 따라 변경
                // 빨강 → 주황 → 노랑 → 초록 → 파랑 → 보라 → 빨강... 순환
                float h, s, v;
                Color.RGBToHSV(color, out h, out s, out v);
                h = (h + Time.deltaTime * 2f) % 1f;  // 초당 색상환 2바퀴 회전
                Color newColor = Color.HSVToRGB(h, s, v);
                newColor.a = 1f - t;  // 시간이 지나면서 투명해짐
                img.color = newColor;
                color = newColor;

                // 크기도 시간에 따라 약간 줄어듦
                float sc = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(sc, sc);

                yield return null;
            }

            Destroy(spark);
        }

        /// <summary>
        /// 도넛 중심에서 파괴 대상 블록까지 연결되는 무지개색 빛줄기 이펙트.
        ///
        /// 두 점 사이에 가느다란 선을 그리고,
        /// 색이 무지개처럼 변하면서 점점 가늘어지고 투명해져 사라집니다.
        ///
        /// 이 이펙트로 "도넛이 저 블록을 노리고 있다"는 것을 시각적으로 표현합니다.
        ///
        /// 비유: 레이저 포인터로 대상을 가리키는 것과 같습니다.
        /// </summary>
        /// <param name="from">선의 시작점 (도넛 중심)</param>
        /// <param name="to">선의 끝점 (파괴 대상 블록 위치)</param>
        private IEnumerator RainbowConnectionLine(Vector3 from, Vector3 to)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject line = new GameObject("RainbowLine");
            line.transform.SetParent(parent, false);

            // 선의 중간점에 위치시킴
            Vector3 mid = (from + to) / 2f;
            line.transform.position = mid;

            var img = line.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            // 두 점 사이 거리와 각도를 계산하여 얇은 직사각형을 회전 배치
            RectTransform rt = line.GetComponent<RectTransform>();
            float dist = Vector3.Distance(from, to);
            Vector3 dir = to - from;
            float lineAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, lineAngle);
            rt.sizeDelta = new Vector2(dist, 3f);  // 길이=두 점 사이 거리, 두께=3px

            float duration = 0.2f;  // 매우 짧은 수명 (순간적으로 번쩍)
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 무지개색 변화
                float hue = (t * 2f) % 1f;
                Color c = Color.HSVToRGB(hue, 0.8f, 1f);
                c.a = (1f - t) * 0.6f;  // 페이드아웃
                img.color = c;

                // 시간에 따라 선이 가늘어짐 (3px → 1px)
                float width = Mathf.Lerp(3f, 1f, t);
                rt.sizeDelta = new Vector2(dist, width);

                yield return null;
            }

            Destroy(line);
        }

        // ============================================================
        // 화면 흔들림 (Screen Shake)
        // - 도넛 발동 시 화면 전체가 흔들려서 임팩트감을 줍니다
        // ============================================================

        /// <summary>
        /// 오브젝트를 페이드아웃(점점 투명해짐)시킨 후 삭제하는 범용 유틸리티.
        /// 이펙트 오브젝트가 갑자기 사라지지 않고 자연스럽게 퇴장하도록 합니다.
        /// </summary>
        /// <param name="obj">페이드아웃 후 삭제할 게임오브젝트</param>
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
                c.a = 1f - t;  // 0.3초에 걸쳐 투명도를 1 → 0으로
                img.color = c;
                yield return null;
            }

            Destroy(obj);
        }

        /// <summary>
        /// 현재 중첩된 화면 흔들림의 수.
        /// 여러 흔들림이 동시에 발생할 수 있으므로 카운터로 추적합니다.
        /// 모든 흔들림이 끝나야(shakeCount==0) 원래 위치로 복원됩니다.
        /// </summary>
        private int shakeCount = 0;

        /// <summary>
        /// 화면 흔들림이 시작되기 전의 원래 위치.
        /// 흔들림이 끝나면 이 위치로 정확히 복원합니다.
        /// </summary>
        private Vector3 shakeOriginalPos;

        /// <summary>
        /// 화면 흔들림 효과.
        ///
        /// 게임판(hexGrid)의 위치를 랜덤하게 흔들어 임팩트감을 줍니다.
        /// 시간이 지남에 따라 흔들림 강도가 점점 줄어들다 멈춥니다 (감쇠 진동).
        ///
        /// 여러 흔들림이 동시에 발생해도 안전합니다:
        /// - shakeCount로 중첩 관리
        /// - 첫 번째 흔들림 시작 시 원래 위치를 저장
        /// - 마지막 흔들림이 끝나면 원래 위치로 정확히 복원
        ///
        /// 비유: 지진이 발생한 것처럼 화면이 진동하다 잦아드는 것입니다.
        /// </summary>
        /// <param name="intensity">흔들림 강도 (픽셀 단위, 클수록 크게 흔들림)</param>
        /// <param name="duration">흔들림 지속 시간 (초)</param>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            // 첫 번째 흔들림일 때만 원래 위치 저장 (중첩 시 원래 위치 보존)
            if (shakeCount == 0)
                shakeOriginalPos = target.localPosition;
            shakeCount++;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float decay = 1f - VisualConstants.EaseInQuad(t);  // 시간에 따라 감쇠 (1→0)
                // 랜덤 방향으로 흔들되, 시간이 지나면서 흔들림 폭이 줄어듦
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                target.localPosition = shakeOriginalPos + new Vector3(x, y, 0);
                yield return null;
            }

            shakeCount--;
            // 모든 흔들림이 끝나면 정확히 원래 위치로 복원
            if (shakeCount <= 0)
            {
                shakeCount = 0;
                target.localPosition = shakeOriginalPos;
            }
        }

        // ============================================================
        // Phase 1 VFX: 공통 유틸리티 메서드
        // - 도넛뿐 아니라 모든 특수 블록이 공통으로 사용하는 연출 기법들입니다
        // - 각 특수 블록 시스템마다 동일한 패턴이 구현되어 있습니다
        // ============================================================

        /// <summary>
        /// Pre-Fire 압축 애니메이션 (발동 전 "힘 모으기" 연출).
        ///
        /// 블록이 발동되기 직전, 크기가 변하는 애니메이션입니다:
        /// - 전반부: 1.0 → 0.78 (쪼그라듦, 힘을 모으는 느낌)
        /// - 후반부: 0.78 → 1.18 (튀어오름, 발사 준비 완료)
        /// - 완료 후: 원래 크기(1.0)로 복원
        ///
        /// 이 연출 덕분에 플레이어가 "곧 뭔가 터지겠구나!"라고 예감할 수 있습니다.
        ///
        /// 비유: 농구 선수가 점프하기 전에 무릎을 구부리는 동작과 같습니다.
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
                    // === 전반부: 쪼그라듦 (1.0 → PreFireScaleMin) ===
                    float ct = t / 0.5f;
                    scale = Mathf.Lerp(1f, VisualConstants.PreFireScaleMin, VisualConstants.EaseInQuad(ct));
                }
                else
                {
                    // === 후반부: 팽창 (PreFireScaleMin → PreFireScaleMax) ===
                    float et = (t - 0.5f) / 0.5f;
                    scale = Mathf.Lerp(VisualConstants.PreFireScaleMin, VisualConstants.PreFireScaleMax, VisualConstants.EaseOutCubic(et));
                }

                block.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            block.transform.localScale = Vector3.one;  // 원래 크기로 복원
        }

        /// <summary>
        /// 히트스탑 (Hit Stop) - 순간 멈춤 + 슬로모션 연출.
        ///
        /// 강한 타격감을 주기 위해 게임 시간을 조작하는 기법입니다:
        /// 1. Time.timeScale을 0으로 설정 (게임 완전 정지)
        /// 2. 짧은 시간(실제 시간 기준) 대기
        /// 3. 느린 슬로모션으로 서서히 복구 (0 → 1로 점진적 복원)
        ///
        /// 쿨다운 시스템으로 너무 자주 발생하지 않도록 제한됩니다.
        ///
        /// 비유: 격투 게임에서 강한 펀치가 맞는 순간 화면이 잠깐 멈추는 것과 같습니다.
        /// </summary>
        /// <param name="stopDuration">완전 정지 유지 시간 (실제 시간 기준, 초)</param>
        private IEnumerator HitStop(float stopDuration)
        {
            // 쿨다운 체크: 너무 자주 히트스탑이 발생하면 건너뜀
            if (!VisualConstants.CanHitStop()) yield break;
            VisualConstants.RecordHitStop();

            // 1단계: 완전 정지
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(stopDuration);  // 실제 시간으로 대기 (게임 시간은 멈춰있으므로)

            // 2단계: 슬로모션에서 서서히 정상 속도로 복귀
            float elapsed = 0f;
            while (elapsed < VisualConstants.HitStopSlowMoDuration)
            {
                elapsed += Time.unscaledDeltaTime;  // 실제 시간 기준으로 진행
                float t = Mathf.Clamp01(elapsed / VisualConstants.HitStopSlowMoDuration);
                // 느린 속도(SlowMoScale) → 정상 속도(1.0)로 부드럽게 전환
                Time.timeScale = Mathf.Lerp(VisualConstants.HitStopSlowMoScale, 1f, VisualConstants.EaseOutCubic(t));
                yield return null;
            }
            Time.timeScale = 1f;  // 정상 속도 확실히 복원
        }

        /// <summary>
        /// 줌 펀치 (Zoom Punch) - 화면 전체 확대/축소 펄스 효과.
        ///
        /// 게임판 전체가 잠깐 커졌다가 원래 크기로 돌아오는 연출입니다.
        /// 히트스탑과 함께 사용하면 강력한 타격감을 줍니다.
        ///
        /// 동작 순서:
        /// 1. 원래 크기 → targetScale 크기로 빠르게 확대
        /// 2. targetScale 크기 → 원래 크기로 부드럽게 복귀
        ///
        /// 비유: 카메라가 갑자기 한 발짝 앞으로 다가갔다가 뒤로 물러나는 것입니다.
        /// </summary>
        /// <param name="targetScale">최대 확대 배율 (예: 1.05 = 5% 확대)</param>
        private IEnumerator ZoomPunch(float targetScale)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = target.localScale;        // 원래 크기 저장
            Vector3 punchScale = origScale * targetScale;  // 확대 목표 크기

            // 1단계: 빠르게 확대 (ZoomPunchInDuration 동안)
            float elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchInDuration);
                target.localScale = Vector3.Lerp(origScale, punchScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            // 2단계: 부드럽게 원래 크기로 복귀 (ZoomPunchOutDuration 동안)
            elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchOutDuration);
                target.localScale = Vector3.Lerp(punchScale, origScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            target.localScale = origScale;  // 원래 크기 확실히 복원
        }

        /// <summary>
        /// 블록 파괴 시 순간적으로 하얗게 빛나는 플래시 오버레이.
        ///
        /// 블록 위에 흰색 반투명 이미지를 겹쳐 놓고,
        /// 빠르게 투명해지도록 애니메이션합니다.
        /// "블록이 빛나며 사라진다"는 느낌을 줍니다.
        ///
        /// 비유: 카메라 플래시가 터지듯 순간적으로 하얗게 빛나는 효과입니다.
        /// </summary>
        /// <param name="block">플래시 효과를 적용할 블록</param>
        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            // 블록의 자식으로 흰색 오버레이 생성
            GameObject flash = new GameObject("DestroyFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;  // 블록 중심에 위치

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha);  // 흰색 반투명

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 60f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

            // 시간에 따라 투명해짐 (빠르게 사라짐)
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
        /// 블룸(Bloom) 레이어 - 메인 플래시 뒤에 깔리는 은은한 광채 효과.
        ///
        /// 플래시보다 약간 늦게 시작되고(BloomLag), 더 크고 더 투명합니다.
        /// 플래시와 겹쳐져서 "빛이 퍼져나가는" 글로우 효과를 만듭니다.
        ///
        /// SetAsFirstSibling()으로 렌더링 순서를 뒤로 보내서,
        /// 다른 이펙트들 뒤에 은은하게 깔리도록 합니다.
        ///
        /// 비유: 전구 주변에 보이는 은은한 후광(Halo)과 같습니다.
        /// </summary>
        /// <param name="pos">블룸 중심 위치</param>
        /// <param name="color">블룸 색상</param>
        /// <param name="initSize">초기 크기</param>
        /// <param name="duration">전체 지속 시간 (BloomLag 포함)</param>
        private IEnumerator BloomLayer(Vector3 pos, Color color, float initSize, float duration)
        {
            // 메인 플래시보다 약간 늦게 시작 (시간차 연출)
            yield return new WaitForSeconds(VisualConstants.BloomLag);

            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject bloom = new GameObject("BloomLayer");
            bloom.transform.SetParent(parent, false);
            bloom.transform.position = pos;
            bloom.transform.SetAsFirstSibling();  // 다른 이펙트들 뒤에 렌더링되도록

            var img = bloom.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier);

            RectTransform rt = bloom.GetComponent<RectTransform>();
            float bloomSize = initSize * VisualConstants.BloomSizeMultiplier;  // 플래시보다 크게
            rt.sizeDelta = new Vector2(bloomSize, bloomSize);

            float remainDuration = duration - VisualConstants.BloomLag;
            float elapsed = 0f;

            while (elapsed < remainDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / remainDuration);
                float eased = VisualConstants.EaseOutCubic(t);
                // 플래시와 동일한 비율로 확대
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                rt.sizeDelta = new Vector2(bloomSize * scale, bloomSize * scale);
                // 서서히 투명해짐
                img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier * (1f - t));
                yield return null;
            }

            Destroy(bloom);
        }
    }
}

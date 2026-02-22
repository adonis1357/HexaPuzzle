// ============================================================================
// XBlockSystem.cs - X블록 특수 블록 시스템
// ============================================================================
//
// [한줄 요약]
// X블록은 "링 매칭"으로 생성되며, 발동하면 게임판 전체에서
// 같은 색상의 블록을 한번에 모두 제거하는 강력한 특수 블록입니다.
//
// [X블록이란?]
// 육각형 게임판에서 중앙 블록 주변 6개가 모두 같은 색일 때 (=링 매칭)
// 중앙 위치에 X블록이 생성됩니다.
// 비유하자면, 꽃잎 6개가 같은 색으로 둘러싼 꽃의 중심에 X 표시가 나타나는 것입니다.
//
// [발동 효과]
// X블록이 매칭에 포함되어 발동하면:
// 1. 발동 전 "쿵!" 하고 압축했다가 팽창하는 사전 애니메이션 재생
// 2. 게임판 전체를 스캔하여 X블록과 같은 색상의 블록을 모두 찾음
// 3. X자 형태의 화려한 이펙트와 함께 충격파가 퍼져나감
// 4. 중심에서 가까운 블록부터 파도처럼 순차적으로 파괴
// 5. 각 블록이 파괴될 때 45도 회전하며 찌그러지는 애니메이션 재생
//
// [처리 흐름]
// CreateXBlock() → ActivateXBlock() → XBlockCoroutine() → 이펙트 + 순차 파괴
//
// ============================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// X블록 특수 블록 시스템 클래스.
    /// 링 매칭(중앙 블록 주변 6개가 모두 같은 색)으로 생성되며,
    /// 발동 시 게임판 전체에서 같은 색 블록을 한꺼번에 제거하는 "색상 청소기" 역할.
    /// 비유: 물감을 뿌리면 같은 색 물감이 전부 사라지는 마법 같은 블록.
    /// </summary>
    public class XBlockSystem : MonoBehaviour
    {
        // ============================================================
        // 인스펙터에서 설정하는 참조 변수들 (에디터에서 드래그 앤 드롭으로 연결)
        // ============================================================

        [Header("References")]
        /// <summary>
        /// 육각형 게임판(그리드) 참조. 블록 위치 정보와 전체 블록 목록을 가져올 때 사용.
        /// 비유: X블록이 "게임판 지도"를 보고 같은 색 블록을 찾는 데 필요한 지도 자체.
        /// </summary>
        [SerializeField] private HexGrid hexGrid;

        /// <summary>
        /// 블록 제거 시스템 참조. 캐스케이드(연쇄 폭발) 깊이 정보를 가져올 때 사용.
        /// 연쇄가 깊어질수록 이펙트가 더 화려해지는 데 이 정보가 필요함.
        /// </summary>
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("XBlock Settings")]
        /// <summary>
        /// 블록을 순차적으로 파괴할 때 각 블록 사이의 대기 시간 (초).
        /// 값이 작을수록 빠르게 연쇄 파괴되고, 클수록 "파도"처럼 천천히 퍼져나감.
        /// 기본값 0.03초 = 거의 즉시지만 약간의 순차 느낌을 줌.
        /// </summary>
        [SerializeField] private float waveDelay = 0.03f;

        [Header("Effect Settings")]
        /// <summary>
        /// X블록 발동 시 중심에서 뿜어져 나오는 불꽃(스파크) 개수.
        /// 연쇄 깊이에 따라 실제로는 이보다 더 많은 스파크가 생길 수 있음.
        /// </summary>
        [SerializeField] private int sparkCount = 20;

        // ============================================================
        // 이벤트 및 상태 추적 변수들
        // ============================================================

        /// <summary>
        /// X블록 발동이 완전히 끝났을 때 발생하는 이벤트.
        /// 정수 인자는 획득한 총 점수. 외부 시스템(점수 관리 등)이 이 이벤트를 구독해서 처리.
        /// 비유: "X블록 임무 완료!" 라고 방송하는 것. 점수판이 이 방송을 듣고 점수를 올림.
        /// </summary>
        public event System.Action<int> OnXBlockComplete;

        /// <summary>
        /// 현재 동시에 발동 중인 X블록의 수.
        /// 여러 X블록이 동시에 터질 수 있기 때문에 카운터로 추적.
        /// 0이면 모든 X블록 발동이 끝난 것.
        /// </summary>
        private int activeXBlockCount = 0;

        /// <summary>
        /// X블록이 파괴하려는 대상 중 "특수 블록"들을 따로 모아두는 목록.
        /// 특수 블록은 바로 파괴하지 않고, 나중에 연쇄 발동시키기 위해 대기열에 넣음.
        /// 비유: "이건 그냥 부수면 안 돼, 나중에 따로 터뜨려야 해" 리스트.
        /// </summary>
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        /// <summary>
        /// 현재 발동 중인(애니메이션이 재생 중인) X블록들의 집합.
        /// 같은 블록이 중복 발동되지 않도록 방지하는 "진행 중" 목록.
        /// </summary>
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        // ============================================================
        // 외부에서 읽을 수 있는 속성들 (프로퍼티)
        // ============================================================

        /// <summary>
        /// X블록이 현재 발동 중인지 확인. true면 아직 애니메이션/파괴가 진행 중.
        /// 다른 시스템이 "X블록 아직 일하고 있어?" 하고 물어볼 때 사용.
        /// </summary>
        public bool IsActivating => activeXBlockCount > 0;

        /// <summary>
        /// 발동 대기 중인 특수 블록 목록을 외부에서 읽을 수 있게 공개.
        /// BlockRemovalSystem이 이 목록을 보고 연쇄 발동을 처리함.
        /// </summary>
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;

        /// <summary>
        /// 특정 블록이 현재 X블록 발동에 의해 처리 중인지 확인.
        /// </summary>
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);

        /// <summary>
        /// 강제 초기화. 게임이 멈추거나 비정상 상태에 빠졌을 때 모든 것을 리셋.
        /// 비유: 기계가 고장 났을 때 전원을 껐다 켜는 "비상 리셋 버튼".
        /// 모든 진행 중 코루틴을 중단하고, 남아있는 이펙트를 청소하고, 카운터를 0으로.
        /// </summary>
        public void ForceReset()
        {
            activeXBlockCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.Log("[XBlockSystem] ForceReset called");
        }

        /// <summary>
        /// 화면에 남아있는 이펙트 오브젝트를 모두 삭제하는 청소 메서드.
        /// 코루틴이 중간에 중단되면 이펙트가 화면에 영원히 남을 수 있는데,
        /// 이 메서드가 그런 "쓰레기 이펙트"들을 깨끗이 치워줌.
        /// 비유: 파티가 끝난 뒤 바닥에 떨어진 색종이를 쓸어 담는 청소부.
        /// </summary>
        private void CleanupEffects()
        {
            if (effectParent == null) return;
            // 이펙트 부모 아래의 모든 자식 오브젝트를 역순으로 삭제
            // (역순인 이유: 앞에서부터 삭제하면 인덱스가 꼬이기 때문)
            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);
        }


        /// <summary>
        /// 이펙트 오브젝트들이 모일 부모 Transform.
        /// 모든 시각 효과(플래시, 스파크, 파동 등)는 이 밑에 생성됨.
        /// 비유: 이펙트들의 "수납장". 정리할 때 이 수납장만 비우면 됨.
        /// </summary>
        private Transform effectParent;

        /// <summary>
        /// X블록 아이콘 스프라이트를 캐시(저장)해두는 정적 변수.
        /// 한 번 만들어두면 게임 내내 재사용. 매번 새로 만들면 메모리 낭비이므로.
        /// static이라 XBlockSystem 인스턴스가 여러 개여도 아이콘은 하나만 존재.
        /// </summary>
        private static Sprite xBlockIconSprite;

        // ============================================================
        // 초기화 메서드들
        // ============================================================

        /// <summary>
        /// Unity가 게임 시작 시 자동으로 호출하는 초기화 메서드.
        /// 게임판(HexGrid)과 블록 제거 시스템(BlockRemovalSystem)을 찾아서 연결하고,
        /// 이펙트를 표시할 부모 오브젝트를 설정함.
        /// 비유: 일꾼이 출근해서 도구(그리드, 제거 시스템)를 챙기고 작업대(이펙트 레이어)를 세팅하는 과정.
        /// </summary>
        private void Start()
        {
            // hexGrid가 인스펙터에서 연결되지 않았으면 씬에서 자동으로 찾아봄
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[XBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[XBlockSystem] HexGrid not found!");
            }
            // removalSystem도 마찬가지로 자동 탐색
            if (removalSystem == null)
                removalSystem = FindObjectOfType<BlockRemovalSystem>();

            // 이펙트 표시용 레이어 생성
            SetupEffectParent();
        }

        /// <summary>
        /// 이펙트(시각 효과)를 표시할 전용 레이어를 생성하는 메서드.
        /// Canvas 위에 투명한 레이어를 하나 깔아서, 모든 이펙트가 그 위에 표시되도록 함.
        /// 비유: 그림 위에 투명 필름을 깔고, 그 필름 위에 반짝이를 뿌리는 것.
        ///       필름만 걷어내면 반짝이가 깔끔하게 치워짐.
        /// </summary>
        private void SetupEffectParent()
        {
            if (hexGrid == null) return;

            // Canvas(UI 화면)를 찾아서 그 아래에 이펙트 레이어를 생성
            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                // "XBlockEffectLayer"라는 이름의 빈 오브젝트를 Canvas 자식으로 생성
                GameObject effectLayer = new GameObject("XBlockEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);

                // 화면 전체를 덮도록 RectTransform 설정 (앵커: 좌하단~우상단)
                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;    // 좌하단 (0,0)
                rt.anchorMax = Vector2.one;     // 우상단 (1,1)
                rt.sizeDelta = Vector2.zero;    // 앵커에 맞춰 꽉 채움
                rt.anchoredPosition = Vector2.zero;

                // 가장 위에 표시되도록 (다른 UI 위에 이펙트가 보이게)
                effectLayer.transform.SetAsLastSibling();
                effectParent = effectLayer.transform;
            }
            else
            {
                // Canvas를 못 찾으면 게임판 자체를 이펙트 부모로 사용 (대안)
                effectParent = hexGrid.transform;
            }
        }

        // ============================================================
        // X블록 아이콘 스프라이트 생성
        // 블록 위에 표시되는 "X" 모양 그래픽을 코드로 직접 그리는 영역
        // (이 게임은 외부 이미지 파일 없이 코드로 모든 그래픽을 생성함)
        // ============================================================

        /// <summary>
        /// X블록 아이콘 스프라이트를 가져오는 공개 메서드.
        /// 이미 만들어져 있으면 캐시된 것을 반환하고, 없으면 새로 생성.
        /// 비유: 도장이 이미 만들어져 있으면 꺼내 쓰고, 없으면 새로 조각하는 것.
        /// </summary>
        public static Sprite GetXBlockIconSprite()
        {
            if (xBlockIconSprite == null)
                xBlockIconSprite = CreateXSprite(128); // 128x128 픽셀 크기로 생성 (해상도 개선)
            return xBlockIconSprite;
        }

        /// <summary>
        /// X 아이콘을 프로시저럴(코드로 직접 그리기) 방식으로 생성.
        /// 64x64 픽셀의 텍스처에 굵은 X 모양을 그리고, 가장자리에 글로우(빛남) 효과 추가.
        /// 비유: 작은 도화지(64x64 점)에 파스텔 색연필로 X를 그리고, 주변을 살짝 번지게 하는 것.
        /// </summary>
        /// <param name="size">텍스처 크기 (가로=세로, 정사각형). 기본 64픽셀.</param>
        private static Sprite CreateXSprite(int size)
        {
            // 빈 텍스처(도화지) 생성
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear; // 부드러운 보간 필터
            Color[] pixels = new Color[size * size];

            // 모든 픽셀을 투명하게 초기화 (깨끗한 도화지)
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f); // 텍스처 중심점
            float armLength = size * 0.35f;   // X의 팔 길이 (중심에서 끝까지)
            float armWidth = size * 0.1f;     // X의 팔 두께
            float glowRadius = size * 0.06f;  // 글로우(빛남) 효과의 반경

            // --- 1단계: X 모양 그리기 ---
            // 모든 픽셀을 순회하며, 해당 위치가 X의 두 대각선에 가까운지 계산
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 중심 기준으로 현재 픽셀의 상대 위치 계산
                    Vector2 p = new Vector2(x, y) - center;

                    // X의 두 대각선까지의 거리 계산
                    // 대각선 1: y = x (우상향 45도 선)
                    float dist1 = Mathf.Abs(p.x - p.y) / 1.414f;
                    // 대각선 2: y = -x (좌상향 135도 선)
                    float dist2 = Mathf.Abs(p.x + p.y) / 1.414f;

                    // 중심에서 너무 먼 픽셀은 건너뜀 (팔 길이 제한)
                    float fromCenter = p.magnitude;
                    if (fromCenter > armLength + glowRadius) continue;

                    // 두 대각선 중 더 가까운 쪽까지의 거리
                    float minDist = Mathf.Min(dist1, dist2);

                    // 코어 X 부분 (파스텔 라벤더 색상으로 채움)
                    if (minDist < armWidth && fromCenter <= armLength)
                    {
                        float edge = minDist / armWidth;
                        float aa = Mathf.Clamp01(1f - (minDist - armWidth + 1.5f) / 1.5f);

                        // 3D 느낌을 주기 위해 중앙이 밝고 가장자리가 어두운 그라데이션
                        float highlight = Mathf.Pow(1f - edge, 2f) * 0.3f;
                        Color c = new Color(0.88f + highlight * 0.05f, 0.80f + highlight * 0.05f, 0.96f, aa);
                        pixels[y * size + x] = c;
                    }
                    // 글로우 부분 (X 주변을 살짝 빛나게 - 파스텔 라벤더 빛)
                    else if (minDist < armWidth + glowRadius && fromCenter <= armLength + glowRadius)
                    {
                        float glowT = (minDist - armWidth) / glowRadius;
                        float glowAlpha = Mathf.Clamp01(1f - glowT) * 0.4f;

                        // 팔 끝쪽 가장자리는 자연스럽게 사라지도록 페이드
                        float edgeFade = Mathf.Clamp01((armLength + glowRadius - fromCenter) / glowRadius);
                        glowAlpha *= edgeFade;

                        Color glowColor = new Color(0.82f, 0.76f, 0.95f, glowAlpha);
                        if (pixels[y * size + x].a < glowAlpha)
                            pixels[y * size + x] = glowColor;
                    }
                }
            }

            // --- 2단계: 중앙에 반짝이는 밝은 점 추가 ---
            // X의 교차점 중앙에 작은 하이라이트를 넣어 보석 느낌을 줌
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float centerDist = Vector2.Distance(new Vector2(x, y), center);
                    if (centerDist < size * 0.1f)
                    {
                        float sa = Mathf.Clamp01((size * 0.1f - centerDist) / (size * 0.1f));
                        Color sparkColor = new Color(1f, 1f, 0.95f, sa * 0.8f); // 약간 노란빛 흰색
                        pixels[y * size + x] = Color.Lerp(pixels[y * size + x], sparkColor, sa);
                    }
                }
            }

            // 완성된 픽셀 데이터를 텍스처에 적용하고 스프라이트로 변환
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // X블록 생성
        // 매칭 시스템이 링 매칭을 감지하면 이 메서드를 호출하여 X블록을 만듦
        // ============================================================

        /// <summary>
        /// 지정된 블록을 X블록으로 변환하는 메서드.
        /// 매칭 시스템이 "여기에 X블록을 만들어!" 하고 호출함.
        /// 기존 블록의 데이터를 X블록 데이터로 교체하는 방식.
        /// 비유: 일반 블록에게 "X" 모자를 씌워서 특수 블록으로 승격시키는 것.
        /// </summary>
        /// <param name="block">X블록으로 변환할 대상 블록 (보통 링 매칭의 중앙 블록)</param>
        /// <param name="gemType">X블록의 색상. 발동 시 이 색상의 블록들이 전부 파괴됨.</param>
        public void CreateXBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            // 새로운 블록 데이터를 만들어서 specialType을 XBlock으로 설정
            BlockData xData = new BlockData(gemType);
            xData.specialType = SpecialBlockType.XBlock;
            block.SetBlockData(xData);
            Debug.Log($"[XBlockSystem] Created X-block at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // X블록 발동 (핵심 로직)
        // X블록이 매칭에 포함되면 이 메서드가 호출되어 발동 프로세스 시작
        // ============================================================

        /// <summary>
        /// X블록 발동을 시작하는 진입점 메서드.
        /// 유효성 검사 후 실제 발동 코루틴(XBlockCoroutine)을 시작.
        /// 비유: 폭탄의 뇌관을 당기는 동작. 당긴 후에는 XBlockCoroutine이 알아서 폭발 처리.
        /// </summary>
        /// <param name="xBlock">발동할 X블록</param>
        public void ActivateXBlock(HexBlock xBlock)
        {
            if (xBlock == null) return;
            // X블록이 아닌 블록이 실수로 여기에 오면 무시
            if (xBlock.Data == null || xBlock.Data.specialType != SpecialBlockType.XBlock)
                return;
            Debug.Log($"[XBlockSystem] Activating X-block at {xBlock.Coord}");
            StartCoroutine(XBlockCoroutine(xBlock));
        }

        /// <summary>
        /// X블록 발동의 전체 과정을 처리하는 메인 코루틴.
        /// 이 하나의 메서드 안에서 사전 애니메이션 → 대상 탐색 → 이펙트 재생 → 순차 파괴 → 점수 계산까지 모두 수행.
        ///
        /// 전체 흐름:
        /// 1. 사전 압축 애니메이션 (블록이 움찔하며 힘을 모으는 느낌)
        /// 2. 히트스톱 + 줌펀치 (게임판 전체가 잠깐 멈추며 임팩트 강조)
        /// 3. X블록 자신의 데이터를 지움 (자기 자신은 사라짐)
        /// 4. 게임판을 스캔하여 같은 색 블록들을 모두 수집
        /// 5. 중심부 X 이펙트 + 파동 이펙트 + 화면 흔들림
        /// 6. 거리순으로 정렬 후 파도처럼 순차 파괴
        /// 7. 점수 계산 및 미션 시스템에 알림
        ///
        /// 비유: 폭탄이 터지는 전체 시나리오. 카운트다운(압축) → 폭발(이펙트) → 파편(블록 파괴) → 피해 보고(점수)
        /// </summary>
        private IEnumerator XBlockCoroutine(HexBlock xBlock)
        {
            // --- 발동 시작: 카운터 증가 및 활성 목록에 등록 ---
            activeXBlockCount++;
            activeBlocks.Add(xBlock);

            // X블록의 위치, 색상 정보를 미리 저장 (나중에 블록 데이터가 지워져도 사용하기 위해)
            HexCoord xCoord = xBlock.Coord;            // X블록의 그리드 좌표
            Vector3 xWorldPos = xBlock.transform.position; // X블록의 화면상 위치
            GemType targetGemType = xBlock.Data.gemType;   // 파괴할 대상 색상
            Color xColor = GemColors.GetColor(targetGemType); // 색상의 실제 RGB 값

            Debug.Log($"[XBlockSystem] === X-BLOCK ACTIVATED === Coord={xCoord}, TargetColor={targetGemType}");

            // --- 1단계: 사전 압축 애니메이션 ---
            // 블록이 "쿵" 하고 작아졌다가 커지는 모션. 발동 전 긴장감을 줌.
            yield return StartCoroutine(PreFireCompression(xBlock));

            // --- 2단계: 히트스톱 (화면 일시 정지) ---
            // 게임 시간을 잠깐 멈춰서 "강한 충격" 느낌을 줌 (격투게임의 히트스톱과 같은 원리)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationMedium));

            // --- 3단계: 줌 펀치 (게임판 확대→축소) ---
            // 게임판 전체가 살짝 커졌다가 돌아오는 효과. 충격파 느낌.
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // --- 4단계: X블록 자신의 데이터 삭제 ---
            // X블록 자체는 사라지고, 이제부터 같은 색 블록들을 파괴하는 단계
            xBlock.ClearData();

            // --- 5단계: 게임판에서 같은 색 블록 전부 수집 ---
            // 비유: 빨간 X블록이 터지면, 게임판 전체를 훑으며 빨간 블록을 모두 "표적 목록"에 넣음
            List<HexBlock> targets = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue; // 빈 블록은 무시
                    if (block == xBlock) continue; // 자기 자신은 제외

                    // 같은 색상이면 표적에 추가
                    if (block.Data.gemType == targetGemType)
                        targets.Add(block);
                }
            }

            Debug.Log($"[XBlockSystem] Targets: {targets.Count} same-color blocks ({targetGemType})");

            // --- 6단계: 화려한 이펙트 재생 ---
            // X자 중심 이펙트 (플래시 + X자 빔 + 충격파 링 + 스파크 다발)
            StartCoroutine(XCenterEffect(xWorldPos, xColor));

            // 원형 파동 이펙트 (중심에서 바깥으로 퍼지는 동심원)
            StartCoroutine(XWaveExpand(xWorldPos, xColor));

            // 화면 흔들림 (게임판 전체가 덜덜 떨림)
            StartCoroutine(ScreenShake(VisualConstants.ShakeMediumIntensity, VisualConstants.ShakeMediumDuration));

            // 이펙트가 펼쳐질 시간을 살짝 대기
            yield return new WaitForSeconds(0.15f);

            // --- 7단계: 거리순 정렬 ---
            // 중심에서 가까운 블록부터 파괴하여 "파도가 퍼져나가는" 느낌을 줌
            targets.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, xWorldPos);
                float distB = Vector3.Distance(b.transform.position, xWorldPos);
                return distA.CompareTo(distB);
            });

            // --- 8단계: 파도처럼 순차 파괴 ---
            List<Coroutine> destroyCoroutines = new List<Coroutine>(); // 파괴 애니메이션 추적용
            int blockScoreSum = 0;      // 파괴된 블록들의 기본 점수 합계
            int basicBlockCount = 0;    // 기본 블록(일반 젬) 카운트
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
            GameManager.Instance?.OnSpecialBlockDestroyedBlocksByColor(gemCountsByColor, "XBlock");

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;
                if (target.Data == null || target.Data.gemType == GemType.None) continue;

                // FixedBlock(고정 블록)은 어떤 공격으로도 파괴 불가 - 건너뜀
                if (target.Data.specialType == SpecialBlockType.FixedBlock)
                    continue;

                // 대상이 특수 블록(드릴, 폭탄 등)이면 바로 파괴하지 않고 "대기열"에 넣음
                // 나중에 BlockRemovalSystem이 이들을 차례로 연쇄 발동시킴
                if (target.Data.specialType != SpecialBlockType.None)
                {
                    Debug.Log($"[XBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
                    if (!pendingSpecialBlocks.Contains(target))
                    {
                        pendingSpecialBlocks.Add(target);
                        target.SetPendingActivation();  // "곧 발동됩니다" 상태 표시
                        target.StartWarningBlink(10f);  // 깜빡이는 경고 애니메이션 시작
                    }
                }
                else
                {
                    // 일반 블록이면 점수 계산 후 파괴 애니메이션 시작
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);

                    // 기본 젬(GemType 1~5: Red, Blue, Green, Yellow, Purple) 카운트
                    if ((int)target.Data.gemType >= 1 && (int)target.Data.gemType <= 5)
                    {
                        basicBlockCount++;
                        if (gemCountsByColor.ContainsKey(target.Data.gemType))
                            gemCountsByColor[target.Data.gemType]++;
                        else
                            gemCountsByColor[target.Data.gemType] = 1;
                    }

                    // 적군 점수 처리 (가시, 체인, 회색 블록 등의 방해 요소 제거 보너스)
                    var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                    if (sm != null)
                    {
                        // 가시(Thorn) 기생 블록 제거 시 적군 점수
                        if (target.Data.hasThorn)
                            sm.AddEnemyScore(EnemyType.ThornParasite, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        // 체인(Chain) 속박 블록 제거 시 적군 점수
                        if (target.Data.hasChain)
                            sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        // 회색(Gray) 블록 제거 시 적군 점수
                        if (target.Data.gemType == GemType.Gray)
                            sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                    }

                    // 블록 파괴 애니메이션 시작 (X 회전하며 찌그러지는 이펙트)
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithX(target, blockColor, xWorldPos)));
                }

                // 다음 블록 파괴 전 약간 대기 (파도 느낌)
                yield return new WaitForSeconds(waveDelay);
            }

            // --- 9단계: 모든 파괴 애니메이션 완료 대기 ---
            // 모든 블록의 ClearData()가 확실히 호출되도록 기다림
            foreach (var co in destroyCoroutines)
                yield return co;

            // --- 10단계: 점수 계산 ---
            // 기본 점수 500점 + 파괴된 블록들의 티어별 점수 합산
            int totalScore = 500 + blockScoreSum;
            Debug.Log($"[XBlockSystem] === X-BLOCK COMPLETE === Score={totalScore} (base:500 + blockTierSum:{blockScoreSum}), Destroyed={targets.Count}");

            // --- 11단계: 미션 시스템에 파괴 결과 알림 ---
            // "빨간 블록 10개 모으기" 같은 미션 진행률을 업데이트하기 위해
            // 파괴된 기본 블록만 골라서 미션 시스템에 전달 (특수 블록은 제외)
            List<HexBlock> basicBlocksOnly = new List<HexBlock>();
            foreach (var target in targets)
            {
                if (target != null && target.Data != null && target.Data.gemType != GemType.None)
                {
                    // 일반 블록 또는 고정 블록만 미션 카운트에 포함
                    if (target.Data.specialType == SpecialBlockType.None || target.Data.specialType == SpecialBlockType.FixedBlock)
                    {
                        basicBlocksOnly.Add(target);
                        Debug.Log($"[XBlockSystem]   BasicBlock: {target.Coord}, gemType={target.Data.gemType}");
                    }
                }
            }

            Debug.Log($"[XBlockSystem] Passing {basicBlocksOnly.Count} basic blocks to MissionSystem (from {targets.Count} total targets)");

            // 미션 시스템에 알림 (미션 진행 업데이트)
            MissionSystem ms = Object.FindObjectOfType<MissionSystem>();
            if (ms != null)
                ms.OnSpecialBlockDestroyedBlocks(basicBlocksOnly);

            // --- 12단계: 완료 이벤트 발생 및 상태 정리 ---
            OnXBlockComplete?.Invoke(totalScore); // 외부 시스템에 "끝났어요, 점수는 이만큼!" 알림
            activeBlocks.Remove(xBlock);
            activeXBlockCount--;
        }

        // ============================================================
        // 이펙트 (시각 효과) 메서드들
        // X블록 발동 시 화면에 표시되는 화려한 연출을 담당
        // ============================================================

        /// <summary>
        /// X블록 중앙에서 재생되는 메인 이펙트 코루틴.
        /// X자 형태의 밝은 빔 + 중앙 플래시 + 충격파 링 + 불꽃 다발을
        /// 동시에 재생하여 강렬한 발동 연출을 만듦.
        ///
        /// 구성 요소:
        /// - 블룸 레이어: 뒤쪽에 깔리는 부드러운 빛 번짐
        /// - 중앙 플래시: 밝은 빛이 확장되며 사라짐
        /// - X자 라인 2개: 45도, -45도로 교차하는 빔
        /// - 충격파 링: 원형으로 퍼지는 파동
        /// - 스파크 다발: 대각선 방향으로 튀는 불꽃들
        ///
        /// 비유: 불꽃놀이에서 X자 모양으로 폭죽이 터지는 장면.
        /// </summary>
        /// <param name="pos">이펙트가 재생될 위치 (X블록이 있던 자리)</param>
        /// <param name="color">이펙트의 기본 색상 (X블록의 젬 색상)</param>
        private IEnumerator XCenterEffect(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // [배경] 블룸 레이어 - 메인 플래시 뒤에 깔리는 부드러운 빛 번짐
            StartCoroutine(BloomLayer(pos, color, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // [1] 밝은 중앙 플래시 - 발동 순간 화면이 번쩍하는 효과
            GameObject flash = new GameObject("XBlockFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.raycastTarget = false; // 터치 입력을 가로채지 않도록
            flashImg.color = new Color(color.r, color.g, color.b, 1f);

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            flashRt.sizeDelta = new Vector2(initSize, initSize);

            // [2] X자 형태의 두 줄 (45도 + -45도 = X자 교차)
            GameObject line1 = CreateXLine(pos, parent, 45f, color);  // / 방향 빔
            GameObject line2 = CreateXLine(pos, parent, -45f, color); // \ 방향 빔

            // [3] 충격파 링 - 원형으로 퍼지는 파동 (물에 돌을 던지면 퍼지는 동심원 같은 것)
            GameObject ring = new GameObject("XBlockRing");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;

            var ringImg = ring.AddComponent<UnityEngine.UI.Image>();
            ringImg.sprite = HexBlock.GetHexFlashSprite();
            ringImg.raycastTarget = false;
            Color darkColor = VisualConstants.Darken(color); // 약간 어두운 색으로 차별화
            ringImg.color = new Color(darkColor.r, darkColor.g, darkColor.b, 0.7f);

            RectTransform ringRt = ring.GetComponent<RectTransform>();
            float ringInitSize = VisualConstants.WaveLargeInitialSize;
            ringRt.sizeDelta = new Vector2(ringInitSize, ringInitSize);

            // [4] 스파크(불꽃) 다발 - 대각선 방향으로 튀는 작은 불꽃들
            // 연쇄(캐스케이드)가 깊어질수록 불꽃이 더 많아짐 (연쇄 보너스 시각 피드백)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int totalSparks = Mathf.RoundToInt(sparkCount * cascadeMult);
            for (int i = 0; i < totalSparks; i++)
            {
                Color sparkColor = VisualConstants.Brighten(color); // 밝은 색 스파크
                sparkColor.a = 1f;
                StartCoroutine(XSpark(pos, sparkColor));
            }

            // --- 애니메이션 루프: 모든 이펙트가 확장되며 사라짐 ---
            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration); // 0→1 진행률
                float eased = VisualConstants.EaseOutCubic(t); // 빠르게 시작, 느리게 끝나는 이징

                // 플래시: 점점 커지면서 투명해짐
                float flashScale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                flashRt.sizeDelta = new Vector2(initSize * flashScale, initSize * flashScale);
                flashImg.color = new Color(color.r, color.g, color.b, (1f - t) * 0.9f);

                // X 라인: 길어지면서 얇아지고 투명해짐 (0px → 320px)
                AnimateXLine(line1, eased, 45f);
                AnimateXLine(line2, eased, -45f);

                // 충격파 링: 커지면서 사라짐
                float ringScale = 1f + eased * (VisualConstants.WaveLargeExpand - 1f);
                ringRt.sizeDelta = new Vector2(ringInitSize * ringScale, ringInitSize * ringScale);
                ringImg.color = new Color(darkColor.r, darkColor.g, darkColor.b, (1f - t) * 0.5f);

                yield return null; // 다음 프레임까지 대기
            }

            // 모든 이펙트 오브젝트 삭제 (깔끔한 정리)
            Destroy(flash);
            Destroy(line1);
            Destroy(line2);
            Destroy(ring);
        }

        /// <summary>
        /// X자의 한쪽 팔(빔 라인)을 생성하는 헬퍼 메서드.
        /// 가느다란 직사각형 이미지를 원하는 각도로 회전시켜 빔을 만듦.
        /// 비유: 레이저 포인터 빔을 특정 각도로 쏘는 것.
        /// </summary>
        /// <param name="pos">빔의 시작 위치 (X블록 중앙)</param>
        /// <param name="parent">부모 Transform (이펙트 레이어)</param>
        /// <param name="angle">회전 각도 (45도 또는 -45도)</param>
        /// <param name="color">빔 색상</param>
        /// <returns>생성된 빔 오브젝트 (나중에 애니메이션 및 삭제용)</returns>
        private GameObject CreateXLine(Vector3 pos, Transform parent, float angle, Color color)
        {
            GameObject line = new GameObject("XLine");
            line.transform.SetParent(parent, false);
            line.transform.position = pos;
            line.transform.localRotation = Quaternion.Euler(0, 0, angle); // Z축 기준 회전

            var img = line.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            Color lineColor = VisualConstants.Brighten(color); // 밝은 색으로
            lineColor.a = 0.9f; // 약간 반투명
            img.color = lineColor;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(5f, 20f); // 초기: 폭 5px, 길이 20px (짧은 상태에서 시작)

            return line;
        }

        /// <summary>
        /// X자 빔 라인의 매 프레임 애니메이션을 처리.
        /// 시간이 지남에 따라 빔이 길어지고 얇아지며 투명해짐.
        /// 비유: 검을 휘두를 때 잔상이 늘어나다가 사라지는 느낌.
        /// </summary>
        /// <param name="line">애니메이션할 빔 오브젝트</param>
        /// <param name="easedT">이징이 적용된 진행률 (0=시작, 1=끝)</param>
        /// <param name="angle">빔 각도 (사용하지 않지만 시그니처 유지)</param>
        private void AnimateXLine(GameObject line, float easedT, float angle)
        {
            if (line == null) return;
            var rt = line.GetComponent<RectTransform>();
            var img = line.GetComponent<UnityEngine.UI.Image>();
            if (rt == null || img == null) return;

            // 빔이 20px에서 320px까지 늘어남 (EaseOutCubic으로 빠르게 뻗어나감)
            float length = 20f + easedT * 300f;
            // 동시에 폭은 5px에서 2.5px로 줄어듦 (날카로워지는 느낌)
            float width = 5f * (1f - easedT * 0.5f);
            rt.sizeDelta = new Vector2(width, length);

            // 점점 투명해짐
            Color c = img.color;
            c.a = (1f - easedT) * 0.8f;
            img.color = c;
        }

        /// <summary>
        /// X 웨이브(파동) 확산 이펙트 코루틴.
        /// X블록 중심에서 바깥으로 큰 원형 파동이 퍼져나가는 효과.
        /// 비유: 호수에 큰 돌을 던졌을 때 퍼져나가는 물결.
        /// XCenterEffect의 충격파 링보다 더 크고 느리게 퍼짐.
        /// </summary>
        /// <param name="center">파동의 중심점</param>
        /// <param name="color">파동 색상</param>
        private IEnumerator XWaveExpand(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            float initSize = VisualConstants.WaveLargeInitialSize;

            // 원형 파동 오브젝트 생성
            GameObject wave = new GameObject("XWaveExpand");
            wave.transform.SetParent(parent, false);
            wave.transform.position = center;

            var img = wave.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0.5f); // 반투명

            RectTransform rt = wave.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(initSize, initSize);

            // 파동이 퍼져나가면서 사라지는 애니메이션
            float duration = VisualConstants.WaveLargeDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                // 파동 크기 확장 (2.5배 계수로 일반 파동보다 더 크게)
                float scale = 1f + eased * (VisualConstants.WaveLargeExpand - 1f) * 2.5f;
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);

                // 퍼지면서 점점 투명해짐
                img.color = new Color(color.r, color.g, color.b, (1f - t) * 0.35f);

                yield return null;
            }

            Destroy(wave);
        }

        /// <summary>
        /// 개별 블록이 X블록에 의해 파괴될 때의 애니메이션 코루틴.
        /// 블록이 잠깐 팽창했다가 45도 회전하며 찌그러져 사라짐.
        /// X블록만의 독특한 파괴 모션으로, 다른 특수 블록과 차별화됨.
        ///
        /// 애니메이션 단계:
        /// 1. 화이트 플래시 (순간 하얀 빛)
        /// 2. 마이크로 X 플래시 (작은 X자가 반짝)
        /// 3. 스파크 방출
        /// 4. 팽창 → 찌그러짐 + 45도 회전 → 소멸
        ///
        /// 비유: 풍선이 잠깐 부풀었다가 비틀어지며 터지는 느낌.
        /// </summary>
        /// <param name="block">파괴할 대상 블록</param>
        /// <param name="blockColor">블록의 색상 (이펙트 색상에 사용)</param>
        /// <param name="center">X블록 중심 위치 (방향 계산에 사용)</param>
        private IEnumerator DestroyBlockWithX(HexBlock block, Color blockColor, Vector3 center)
        {
            if (block == null) yield break;

            Vector3 blockPos = block.transform.position;

            // 화이트 플래시 오버레이 (블록 위에 하얀 빛이 번쩍)
            StartCoroutine(DestroyFlashOverlay(block));

            // 마이크로 X 플래시 (블록 위치에서 작은 X자가 반짝 - X블록의 정체성 유지)
            StartCoroutine(MicroXFlash(blockPos, blockColor));

            // X형 스파크 (대각선 방향으로 작은 불꽃 방출)
            // 연쇄 깊이에 따라 스파크 수가 증가
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkSmallCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
            {
                Color sparkColor = VisualConstants.Brighten(blockColor);
                sparkColor.a = 1f;
                StartCoroutine(XSpark(blockPos, sparkColor));
            }

            // --- 이중 이징 파괴 애니메이션 + 45도 회전 ---
            // 앞부분(20%): 살짝 팽창 / 뒷부분(80%): 찌그러지며 축소
            float duration = VisualConstants.DestroyDuration;
            float elapsed = 0f;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio; // 팽창 구간 비율 (약 20%)
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (t < expandRatio)
                {
                    // 팽창 구간: 블록이 살짝 커짐 (터지기 직전의 부풀어 오름)
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = origScale * scale;
                }
                else
                {
                    // 찌그러짐+축소 구간: 가로로 눌리면서 세로로 줄어듦 (쭈그러져서 소멸)
                    float shrinkT = (t - expandRatio) / (1f - expandRatio);
                    float sx = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(shrinkT * Mathf.PI); // 가로 압축
                    float sy = Mathf.Max(0f, 1f - VisualConstants.EaseInQuad(shrinkT)); // 세로 축소
                    block.transform.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);
                }

                // 45도 회전 (X의 정체성을 보여주는 독특한 회전 모션)
                float rot = t * 45f;
                block.transform.localRotation = Quaternion.Euler(0, 0, rot);

                yield return null;
            }

            // 애니메이션 종료 후 블록을 원래 상태로 복원하고 데이터 삭제
            block.transform.localScale = Vector3.one;
            block.transform.localRotation = Quaternion.identity;
            block.ClearData(); // 블록 데이터 삭제 = 빈 칸이 됨
        }

        /// <summary>
        /// X형 스파크(불꽃) 하나의 애니메이션 코루틴.
        /// 대각선 방향(45도, 135도, 225도, 315도)으로 발사되어 점점 사라짐.
        /// 일반 스파크와 달리 X 모양(대각선)으로 튀는 것이 특징.
        /// 비유: X자 폭죽에서 네 대각선 방향으로 튀는 작은 불티.
        /// </summary>
        /// <param name="center">스파크 발사 시작 위치</param>
        /// <param name="color">스파크 색상</param>
        private IEnumerator XSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 작은 사각형 이미지로 스파크 표현
            GameObject spark = new GameObject("XSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = color;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkMediumSizeMin, VisualConstants.SparkMediumSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            // X 방향 (대각선 위주)으로 발사 방향 결정
            // 45도, 135도, 225도, 315도 중 하나를 기본으로 하고 +-25도 랜덤 편차 추가
            float[] xAngles = { 45f, 135f, 225f, 315f };
            float baseAngle = xAngles[Random.Range(0, xAngles.Length)];
            float angle = (baseAngle + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkMediumSpeedMin, VisualConstants.SparkMediumSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed; // 속도 벡터

            float lifetime = Random.Range(VisualConstants.SparkMediumLifetimeMin, VisualConstants.SparkMediumLifetimeMax);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                // 위치 이동 (속도대로 날아감)
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                // 감속 (공기 저항처럼 점점 느려짐)
                vel *= VisualConstants.SparkDeceleration;

                // 시간에 따라 투명해짐
                color.a = 1f - t;
                img.color = color;

                // 크기도 점점 작아짐
                float sc = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(sc, sc);

                yield return null;
            }

            Destroy(spark);
        }

        /// <summary>
        /// 마이크로 X 플래시 - 블록 파괴 직전에 재생되는 작은 X자 플래시.
        /// 블록 위치에서 아주 작은 X가 반짝하고 사라짐.
        /// X블록 시스템의 정체성(X 모양)을 블록 하나하나의 파괴에도 반영하는 디테일 이펙트.
        /// 비유: 블록에 X 도장을 찍고 사라지게 하는 느낌.
        /// </summary>
        /// <param name="pos">플래시 위치</param>
        /// <param name="color">플래시 색상</param>
        private IEnumerator MicroXFlash(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // X의 두 대각선을 작은 직사각형 2개로 표현
            // 라인 1: / 방향 (45도)
            GameObject l1 = new GameObject("MicroXFlash1");
            l1.transform.SetParent(parent, false);
            l1.transform.position = pos;
            l1.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            var img1 = l1.AddComponent<UnityEngine.UI.Image>();
            img1.raycastTarget = false;
            Color bright = VisualConstants.Brighten(color);
            bright.a = 0.8f;
            img1.color = bright;
            RectTransform rt1 = l1.GetComponent<RectTransform>();
            rt1.sizeDelta = new Vector2(2f, 15f); // 가느다란 작은 빔

            // 라인 2: \ 방향 (-45도)
            GameObject l2 = new GameObject("MicroXFlash2");
            l2.transform.SetParent(parent, false);
            l2.transform.position = pos;
            l2.transform.localRotation = Quaternion.Euler(0, 0, -45f);
            var img2 = l2.AddComponent<UnityEngine.UI.Image>();
            img2.raycastTarget = false;
            img2.color = bright;
            RectTransform rt2 = l2.GetComponent<RectTransform>();
            rt2.sizeDelta = new Vector2(2f, 15f);

            // 0.08초 동안 빠르게 커지면서 사라짐 (매우 짧은 플래시)
            float duration = 0.08f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 길이가 3배까지 늘어남
                float scale = 1f + t * 2f;
                rt1.sizeDelta = new Vector2(2f, 15f * scale);
                rt2.sizeDelta = new Vector2(2f, 15f * scale);

                // 동시에 투명해짐
                bright.a = 0.8f * (1f - t);
                img1.color = bright;
                img2.color = bright;

                yield return null;
            }

            Destroy(l1);
            Destroy(l2);
        }

        // ============================================================
        // 화면 흔들림 (Screen Shake)
        // X블록 발동 시 게임판 전체를 떨리게 하는 효과
        // ============================================================

        /// <summary>
        /// UI 오브젝트를 서서히 투명하게 만들고 삭제하는 유틸리티 코루틴.
        /// 이펙트가 갑자기 사라지지 않고 자연스럽게 페이드아웃되도록 함.
        /// 비유: 전구가 서서히 꺼지는 것처럼 부드럽게 사라짐.
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
                c.a = 1f - t; // 알파값을 1→0으로 줄여 투명하게
                img.color = c;
                yield return null;
            }

            Destroy(obj);
        }

        /// <summary>
        /// 현재 동시에 실행 중인 화면 흔들림의 수.
        /// 여러 흔들림이 동시에 실행될 때 원래 위치를 올바르게 복원하기 위한 카운터.
        /// </summary>
        private int shakeCount = 0;

        /// <summary>
        /// 화면 흔들림 시작 전의 원래 위치. 흔들림이 끝나면 이 위치로 복원.
        /// </summary>
        private Vector3 shakeOriginalPos;

        /// <summary>
        /// 화면(게임판) 흔들림 효과 코루틴.
        /// 게임판 전체를 랜덤하게 좌우상하로 떨리게 하여 충격감을 줌.
        /// 시간이 지날수록 떨림이 약해지며 자연스럽게 멈춤.
        /// 비유: 지진이 나서 테이블이 흔들리다가 점점 잠잠해지는 것.
        /// </summary>
        /// <param name="intensity">흔들림의 세기 (픽셀 단위). 클수록 격하게 흔들림.</param>
        /// <param name="duration">흔들림 지속 시간 (초)</param>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            // 첫 번째 흔들림일 때만 원래 위치를 저장 (중첩 흔들림 시 위치 안전 보장)
            if (shakeCount == 0)
                shakeOriginalPos = target.localPosition;
            shakeCount++;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // decay: 시간이 지남에 따라 흔들림 세기가 줄어드는 감쇠 계수
                float decay = 1f - VisualConstants.EaseInQuad(t);
                // 랜덤한 방향으로 흔들림 (세기 * 감쇠)
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                target.localPosition = shakeOriginalPos + new Vector3(x, y, 0);
                yield return null;
            }

            // 흔들림 종료: 카운터 감소 및 원래 위치 복원
            shakeCount--;
            if (shakeCount <= 0)
            {
                shakeCount = 0;
                target.localPosition = shakeOriginalPos; // 원래 위치로 정확히 복원
            }
        }

        // ============================================================
        // Phase 1 VFX: 공통 유틸리티 메서드
        // 모든 특수 블록 시스템이 공유하는 표준화된 시각 효과 패턴
        // ============================================================

        /// <summary>
        /// 사전 압축(Pre-Fire Compression) 애니메이션 코루틴.
        /// 특수 블록이 발동하기 직전에 재생되는 "힘을 모으는" 모션.
        /// 블록이 작아졌다가(압축) 커지는(팽창) 2단계 애니메이션.
        ///
        /// 타임라인:
        /// 전반(0~50%): 1.0 → 0.78 (쪼그라듦 - 힘을 모으는 중)
        /// 후반(50~100%): 0.78 → 1.18 (팽창 - 에너지 방출 직전)
        ///
        /// 비유: 점프하기 전에 무릎을 굽혔다가 펴는 것. 또는 주먹을 뒤로 당겼다가 내지르는 것.
        /// </summary>
        /// <param name="block">압축 애니메이션을 재생할 블록</param>
        private IEnumerator PreFireCompression(HexBlock block)
        {
            if (block == null) yield break;

            float elapsed = 0f;
            float duration = VisualConstants.PreFireDuration; // 약 0.12초

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.5f)
                {
                    // 전반: 압축 (1.0 → PreFireScaleMin, 약 0.78)
                    float ct = t / 0.5f;
                    scale = Mathf.Lerp(1f, VisualConstants.PreFireScaleMin, VisualConstants.EaseInQuad(ct));
                }
                else
                {
                    // 후반: 팽창 (PreFireScaleMin → PreFireScaleMax, 약 1.18)
                    float et = (t - 0.5f) / 0.5f;
                    scale = Mathf.Lerp(VisualConstants.PreFireScaleMin, VisualConstants.PreFireScaleMax, VisualConstants.EaseOutCubic(et));
                }

                block.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            block.transform.localScale = Vector3.one; // 원래 크기로 복원
        }

        /// <summary>
        /// 히트스톱(Hit Stop) 효과 코루틴.
        /// 게임 시간을 일시적으로 멈추거나 느리게 하여 강력한 충격감을 연출.
        /// 격투 게임에서 강한 공격이 맞았을 때 화면이 잠깐 멈추는 그 효과.
        ///
        /// 동작 순서:
        /// 1. Time.timeScale = 0 (완전 정지)
        /// 2. 짧은 대기 (실제 시간으로 대기 - WaitForSecondsRealtime 사용)
        /// 3. 슬로모션에서 정상 속도로 서서히 복귀
        ///
        /// 비유: 영화에서 폭발 장면이 슬로모션으로 나오다가 정상 속도로 돌아오는 것.
        /// 쿨다운 관리로 너무 자주 발생하지 않도록 제어됨.
        /// </summary>
        /// <param name="stopDuration">완전 정지 시간 (실제 시간 기준, 초)</param>
        private IEnumerator HitStop(float stopDuration)
        {
            // 쿨다운 체크: 최근에 히트스톱이 발생했으면 건너뜀 (연출 과잉 방지)
            if (!VisualConstants.CanHitStop()) yield break;
            VisualConstants.RecordHitStop(); // 쿨다운 타이머 기록

            // 1단계: 완전 정지
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(stopDuration); // 실제 시간으로 대기

            // 2단계: 슬로모션에서 정상 속도로 서서히 복귀
            float elapsed = 0f;
            while (elapsed < VisualConstants.HitStopSlowMoDuration)
            {
                elapsed += Time.unscaledDeltaTime; // 시간 정지 중이므로 unscaled 시간 사용
                float t = Mathf.Clamp01(elapsed / VisualConstants.HitStopSlowMoDuration);
                // 슬로모 스케일(약 0.3배속)에서 1.0(정상)으로 부드럽게 복귀
                Time.timeScale = Mathf.Lerp(VisualConstants.HitStopSlowMoScale, 1f, VisualConstants.EaseOutCubic(t));
                yield return null;
            }
            Time.timeScale = 1f; // 최종 안전 장치: 정상 속도 확인
        }

        /// <summary>
        /// 줌 펀치(Zoom Punch) 효과 코루틴.
        /// 게임판 전체가 살짝 확대됐다가 원래 크기로 돌아오는 임팩트 연출.
        /// 비유: 강한 충격이 올 때 카메라가 잠깐 "쿵!" 하고 흔들리며 확대되는 느낌.
        ///       만화에서 펀치가 맞았을 때 화면이 커졌다가 돌아오는 연출.
        /// </summary>
        /// <param name="targetScale">최대 확대 비율 (1.0보다 큰 값. 예: 1.02 = 2% 확대)</param>
        private IEnumerator ZoomPunch(float targetScale)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = target.localScale;
            Vector3 punchScale = origScale * targetScale; // 목표 확대 크기

            // 확대 단계: 원래 크기 → 확대 크기
            float elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchInDuration);
                target.localScale = Vector3.Lerp(origScale, punchScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            // 복원 단계: 확대 크기 → 원래 크기
            elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchOutDuration);
                target.localScale = Vector3.Lerp(punchScale, origScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            target.localScale = origScale; // 최종 안전 장치: 정확히 원래 크기로
        }

        /// <summary>
        /// 블록 파괴 시 화이트 플래시 오버레이 코루틴.
        /// 블록 위에 하얀 반투명 이미지를 겹쳐서 "번쩍!" 하는 효과.
        /// 파괴 순간을 강조하는 짧고 밝은 플래시.
        /// 비유: 카메라 플래시가 터지는 것처럼 잠깐 하얗게 빛남.
        /// </summary>
        /// <param name="block">플래시를 표시할 블록</param>
        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            // 블록 위에 하얀 이미지 오버레이 생성
            GameObject flash = new GameObject("DestroyFlash");
            flash.transform.SetParent(block.transform, false); // 블록의 자식으로 배치
            flash.transform.localPosition = Vector3.zero;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha); // 하얀색 반투명

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 60f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

            // 서서히 투명해지며 사라짐
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
        /// 블룸 레이어(Bloom Layer) 코루틴.
        /// 메인 플래시 뒤에 깔리는 부드럽고 큰 빛 번짐 효과.
        /// 메인 플래시보다 약간 늦게 시작되어 "빛이 번지는" 느낌을 줌.
        /// 비유: 밝은 조명 뒤에 생기는 부드러운 후광(halo) 효과.
        ///       사진에서 역광일 때 생기는 빛 번짐과 비슷함.
        /// </summary>
        /// <param name="pos">블룸 중심 위치</param>
        /// <param name="color">블룸 색상</param>
        /// <param name="initSize">초기 크기</param>
        /// <param name="duration">전체 지속 시간</param>
        private IEnumerator BloomLayer(Vector3 pos, Color color, float initSize, float duration)
        {
            // BloomLag만큼 지연 후 시작 (메인 플래시보다 살짝 늦게)
            yield return new WaitForSeconds(VisualConstants.BloomLag);

            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 오브젝트 생성 (가장 뒤에 배치 - SetAsFirstSibling)
            GameObject bloom = new GameObject("BloomLayer");
            bloom.transform.SetParent(parent, false);
            bloom.transform.position = pos;
            bloom.transform.SetAsFirstSibling(); // 다른 이펙트 뒤에 깔리도록

            var img = bloom.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier);

            RectTransform rt = bloom.GetComponent<RectTransform>();
            float bloomSize = initSize * VisualConstants.BloomSizeMultiplier; // 메인보다 큰 크기
            rt.sizeDelta = new Vector2(bloomSize, bloomSize);

            // 확장되면서 사라지는 애니메이션
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

// ============================================================================
// XBlockSystem.cs - 타겟 레이져 특수 블록 시스템
// ============================================================================
//
// [한줄 요약]
// 타겟 레이져는 "링 매칭"으로 생성되며, 발동하면 게임판 전체에서
// 같은 색상의 블록을 레이져로 순차 파괴하는 강력한 특수 블록입니다.
//
// [타겟 레이져란?]
// 육각형 게임판에서 중앙 블록 주변 6개가 모두 같은 색일 때 (=링 매칭)
// 중앙 위치에 타겟 레이져가 생성됩니다.
//
// [발동 효과]
// 타겟 레이져가 매칭에 포함되어 발동하면:
// 1. 발동 전 "쿵!" 하고 압축했다가 팽창하는 사전 애니메이션 재생
// 2. 게임판 전체를 스캔하여 같은 색상의 블록을 모두 찾아 타겟 마킹
// 3. 레이져 총이 등장하여 각 타겟을 향해 총구를 회전
// 4. 시안색 레이져 빔을 순차 발사, 맞은 블록은 즉시 파괴
// 5. 모든 타겟 파괴 후 레이져 총이 자폭
//
// [처리 흐름]
// CreateXBlock() → ActivateXBlock() → XBlockCoroutine() → 타겟 마킹 + 레이져 발사
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
    /// 타겟 레이져 특수 블록 시스템 클래스.
    /// 링 매칭(중앙 블록 주변 6개가 모두 같은 색)으로 생성되며,
    /// 발동 시 게임판 전체에서 같은 색 블록을 레이져로 순차 파괴하는 역할.
    /// </summary>
    public class XBlockSystem : MonoBehaviour
    {
        // ============================================================
        // 인스펙터에서 설정하는 참조 변수들 (에디터에서 드래그 앤 드롭으로 연결)
        // ============================================================

        [Header("References")]
        /// <summary>
        /// 육각형 게임판(그리드) 참조. 블록 위치 정보와 전체 블록 목록을 가져올 때 사용.
        /// 타겟 레이져가 "게임판 지도"를 보고 같은 색 블록을 찾는 데 필요한 지도 자체.
        /// </summary>
        [SerializeField] private HexGrid hexGrid;

        /// <summary>
        /// 블록 제거 시스템 참조. 캐스케이드(연쇄 폭발) 깊이 정보를 가져올 때 사용.
        /// 연쇄가 깊어질수록 이펙트가 더 화려해지는 데 이 정보가 필요함.
        /// </summary>
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("Target Laser Settings")]
        /// <summary>
        /// 레이져 발사 간격 (초). 값이 작을수록 빠른 연사.
        /// </summary>
        [SerializeField] private float waveDelay = 0.03f;

        [Header("Effect Settings")]
        /// <summary>
        /// 레이져 건 자폭 시 파편 개수.
        /// </summary>
        [SerializeField] private int sparkCount = 20;

        // ============================================================
        // 이벤트 및 상태 추적 변수들
        // ============================================================

        /// <summary>
        /// 타겟 레이져 발동 완료 이벤트. 정수 인자는 획득 총 점수.
        /// </summary>
        public event System.Action<int> OnXBlockComplete;

        /// <summary>
        /// 현재 동시에 발동 중인 타겟 레이져 수.
        /// </summary>
        private int activeXBlockCount = 0;

        /// <summary>
        /// 파괴 대상 중 연쇄 발동 가능한 특수 블록들의 대기열.
        /// </summary>
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        /// <summary>
        /// 현재 발동 중인 타겟 레이져 블록들의 집합.
        /// </summary>
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        // ============================================================
        // 외부에서 읽을 수 있는 속성들 (프로퍼티)
        // ============================================================

        /// <summary>
        /// 타겟 레이져가 현재 발동 중인지 확인.
        /// </summary>
        public bool IsActivating => activeXBlockCount > 0;

        /// <summary>
        /// 발동 대기 중인 특수 블록 목록을 외부에서 읽을 수 있게 공개.
        /// BlockRemovalSystem이 이 목록을 보고 연쇄 발동을 처리함.
        /// </summary>
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;

        /// <summary>
        /// 특정 블록이 현재 타겟 레이져 발동에 의해 처리 중인지 확인.
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
            Debug.Log("[타겟 레이져] ForceReset 호출");
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
        /// 타겟 레이져 아이콘 스프라이트 캐시. 레이져 총 형태의 프로시저럴 스프라이트.
        /// </summary>
        private static Sprite laserGunIconSprite;

        /// <summary>
        /// 타겟 십자선 스프라이트 캐시.
        /// </summary>
        private static Sprite crosshairSprite;

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
                    Debug.Log("[타겟 레이져] HexGrid 자동 연결: " + hexGrid.name);
                else
                    Debug.LogError("[타겟 레이져] HexGrid를 찾을 수 없습니다!");
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
                // "TargetLaserEffectLayer" 빈 오브젝트를 Canvas 자식으로 생성
                GameObject effectLayer = new GameObject("TargetLaserEffectLayer");
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
        // 타겟 레이져 아이콘 스프라이트 생성
        // 블록 위에 표시되는 레이져 총 모양 그래픽을 코드로 직접 그림
        // ============================================================

        /// <summary>
        /// 타겟 레이져 아이콘 스프라이트를 가져오는 공개 메서드.
        /// </summary>
        public static Sprite GetXBlockIconSprite()
        {
            if (laserGunIconSprite == null)
                laserGunIconSprite = CreateLaserGunSprite(256);
            return laserGunIconSprite;
        }

        /// <summary>
        /// 타겟 십자선 스프라이트를 가져오는 메서드.
        /// </summary>
        public static Sprite GetCrosshairSprite()
        {
            if (crosshairSprite == null)
                crosshairSprite = CreateCrosshairSprite(64);
            return crosshairSprite;
        }

        /// <summary>
        /// 레이져 총 프로시저럴 스프라이트 생성 (256x256)
        /// 방향성 있는 레이져 총: 총신 + 빛나는 총구 + 손잡이 + 조준경
        /// 전체가 우상단 45° 방향을 향함
        /// </summary>
        private static Sprite CreateLaserGunSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float cos45 = 0.7071f;
            float sin45 = 0.7071f;

            // 색상 정의
            Color metalBase = new Color(0.55f, 0.50f, 0.72f);    // 라벤더 메탈
            Color metalLight = new Color(0.80f, 0.75f, 0.95f);   // 밝은 라벤더
            Color metalDark = new Color(0.35f, 0.30f, 0.50f);    // 어두운 라벤더
            Color cyanGlow = new Color(0f, 1f, 1f);              // 시안 총구
            Color handleColor = new Color(0.25f, 0.22f, 0.35f);  // 손잡이

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;

                    // 45° 회전된 로컬 좌표 (총이 우상단을 향하도록)
                    float lx = p.x * cos45 + p.y * sin45;   // 총신 방향 (길이)
                    float ly = -p.x * sin45 + p.y * cos45;  // 총신 수직 (폭)

                    // --- 총신 (메인 바디) ---
                    float barrelLength = size * 0.38f;
                    float barrelWidth = size * 0.09f;
                    float barrelStart = -size * 0.08f;
                    // 총구 쪽으로 약간 좁아짐
                    float taperFactor = Mathf.Lerp(1f, 0.7f, Mathf.Clamp01((lx - barrelStart) / barrelLength));
                    float effectiveWidth = barrelWidth * taperFactor;

                    if (lx > barrelStart && lx < barrelStart + barrelLength && Mathf.Abs(ly) < effectiveWidth)
                    {
                        float edgeT = Mathf.Abs(ly) / effectiveWidth;
                        float lengthT = (lx - barrelStart) / barrelLength;
                        float aa = Mathf.Clamp01((effectiveWidth - Mathf.Abs(ly)) * 2f);

                        // 원통형 셰이딩
                        float shade = 1f - edgeT * edgeT * 0.5f;
                        shade *= Mathf.Lerp(0.9f, 1f, lengthT);

                        // 스페큘러
                        float spec = Mathf.Pow(Mathf.Clamp01(1f - edgeT * 2f), 3f) * 0.2f;

                        Color col = Color.Lerp(metalDark, metalLight, shade);
                        col.r += spec; col.g += spec; col.b += spec;
                        col.a = aa;
                        pixels[y * size + x] = col;
                    }

                    // --- 총구 글로우 (에너지 충전) ---
                    float muzzleX = barrelStart + barrelLength;
                    Vector2 muzzleLocal = new Vector2(muzzleX, 0);
                    float muzzleDist = Vector2.Distance(new Vector2(lx, ly), muzzleLocal);
                    float muzzleR = size * 0.08f;
                    float glowR = size * 0.14f;

                    if (muzzleDist < glowR)
                    {
                        float glowT = muzzleDist / glowR;
                        float glowAlpha;
                        Color glowCol;

                        if (muzzleDist < muzzleR)
                        {
                            // 코어: 밝은 흰색-시안
                            float coreT = muzzleDist / muzzleR;
                            glowCol = Color.Lerp(Color.white, cyanGlow, coreT * 0.4f);
                            glowAlpha = 1f;
                        }
                        else
                        {
                            // 외곽 글로우: 시안 페이드
                            float outerT = (muzzleDist - muzzleR) / (glowR - muzzleR);
                            glowCol = cyanGlow;
                            glowAlpha = Mathf.Pow(1f - outerT, 2f) * 0.6f;
                        }

                        glowCol.a = glowAlpha;
                        Color existing = pixels[y * size + x];
                        pixels[y * size + x] = Color.Lerp(existing, glowCol, glowAlpha);
                    }

                    // --- 손잡이 (하단) ---
                    float handleTop = -size * 0.02f;
                    float handleBottom = -size * 0.18f;
                    float handleW = size * 0.06f;
                    float handleCenter = -size * 0.02f;

                    if (ly < handleTop && ly > handleBottom && Mathf.Abs(lx - handleCenter) < handleW)
                    {
                        float hEdge = Mathf.Abs(lx - handleCenter) / handleW;
                        float haa = Mathf.Clamp01((handleW - Mathf.Abs(lx - handleCenter)) * 2f);
                        float hShade = 1f - hEdge * hEdge * 0.4f;
                        Color hCol = Color.Lerp(handleColor * 0.7f, handleColor, hShade);
                        hCol.a = haa;
                        Color ex = pixels[y * size + x];
                        if (ex.a < haa)
                            pixels[y * size + x] = hCol;
                    }

                    // --- 조준경 (총신 상단 삼각형) ---
                    float sightBase = size * 0.05f;
                    float sightH = size * 0.06f;
                    float sightX = size * 0.08f;

                    if (ly > barrelWidth * 0.5f && ly < barrelWidth * 0.5f + sightH
                        && Mathf.Abs(lx - sightX) < sightBase * (1f - (ly - barrelWidth * 0.5f) / sightH))
                    {
                        float saa = Mathf.Clamp01((sightBase * (1f - (ly - barrelWidth * 0.5f) / sightH) - Mathf.Abs(lx - sightX)) * 3f);
                        Color sCol = metalBase;
                        sCol.a = saa;
                        Color ex2 = pixels[y * size + x];
                        if (ex2.a < saa)
                            pixels[y * size + x] = sCol;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 타겟 십자선 프로시저럴 스프라이트 생성 (64x64)
        /// 빨간 원형 링 + 십자선
        /// </summary>
        private static Sprite CreateCrosshairSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float ringR = size * 0.42f;
            float ringW = size * 0.06f;
            float crossW = size * 0.04f;
            float crossGap = size * 0.12f;
            Color red = new Color(1f, 0.15f, 0.1f, 0.9f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;
                    float dist = p.magnitude;

                    // 원형 링
                    float ringDist = Mathf.Abs(dist - ringR);
                    if (ringDist < ringW)
                    {
                        float aa = Mathf.Clamp01((ringW - ringDist) * 3f);
                        Color c = red;
                        c.a *= aa;
                        pixels[y * size + x] = c;
                    }

                    // 십자선 (중앙 간격 있음)
                    if (dist > crossGap && dist < ringR + ringW)
                    {
                        bool isHLine = Mathf.Abs(p.y) < crossW;
                        bool isVLine = Mathf.Abs(p.x) < crossW;
                        if (isHLine || isVLine)
                        {
                            float lineEdge = isHLine ? Mathf.Abs(p.y) / crossW : Mathf.Abs(p.x) / crossW;
                            float laa = Mathf.Clamp01((1f - lineEdge) * 2f) * 0.85f;
                            Color lc = red;
                            lc.a = laa;
                            Color ex = pixels[y * size + x];
                            pixels[y * size + x] = new Color(
                                Mathf.Max(ex.r, lc.r * laa),
                                Mathf.Max(ex.g, lc.g * laa),
                                Mathf.Max(ex.b, lc.b * laa),
                                Mathf.Max(ex.a, laa)
                            );
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 타겟 레이져 생성
        // 매칭 시스템이 링 매칭을 감지하면 이 메서드를 호출하여 타겟 레이져를 만듦
        // ============================================================

        /// <summary>
        /// 지정된 블록을 타겟 레이져로 변환하는 메서드.
        /// </summary>
        public void CreateXBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData xData = new BlockData(gemType);
            xData.specialType = SpecialBlockType.XBlock;
            block.SetBlockData(xData);
            Debug.Log($"[타겟 레이져] 생성 완료: {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // 타겟 레이져 발동 (핵심 로직)
        // ============================================================

        /// <summary>
        /// 타겟 레이져 발동 시작 진입점.
        /// </summary>
        public void ActivateXBlock(HexBlock xBlock)
        {
            if (xBlock == null) return;
            if (xBlock.Data == null || xBlock.Data.specialType != SpecialBlockType.XBlock)
                return;
            Debug.Log($"[타겟 레이져] 발동 시작: {xBlock.Coord}");
            StartCoroutine(XBlockCoroutine(xBlock));
        }

        /// <summary>
        /// 타겟 레이져 발동의 전체 과정을 처리하는 메인 코루틴.
        /// 흐름: PreFire → HitStop → ZoomPunch → ClearData → 타겟 수집 →
        ///       타겟 마킹(십자선) → 레이져 건 등장 → 순차 레이져 발사 → 자폭 → 점수
        /// </summary>
        private IEnumerator XBlockCoroutine(HexBlock xBlock)
        {
            activeXBlockCount++;
            activeBlocks.Add(xBlock);

            HexCoord xCoord = xBlock.Coord;
            Vector3 xWorldPos = xBlock.transform.position;
            GemType targetGemType = xBlock.Data.gemType;
            Color xColor = GemColors.GetColor(targetGemType);

            Debug.Log($"[타겟 레이져] === 발동 === Coord={xCoord}, 타겟색상={targetGemType}");

            // --- 1단계: 사전 압축 애니메이션 ---
            yield return StartCoroutine(PreFireCompression(xBlock));

            // --- 2단계: 히트스톱 + 줌 펀치 ---
            StartCoroutine(HitStop(VisualConstants.HitStopDurationMedium));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // --- 3단계: 자신 데이터 삭제 ---
            xBlock.ClearData();

            // --- 4단계: 같은 색 타겟 수집 (블록 + 고블린 통합) ---
            // 통합 타겟 리스트: (월드좌표, HexBlock or null, GoblinData or null)
            List<(Vector3 worldPos, HexBlock block, GoblinData goblin)> unifiedTargets =
                new List<(Vector3, HexBlock, GoblinData)>();

            // 4a. 같은 색 블록 타겟 수집
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block.Data.gemType == GemType.Gray) continue; // 회색 블록 타겟 제외
                    if (block.Data.isShell) continue;                 // ★ 쉘(껍데기) 블록 타겟 제외
                    if (block.Data.isCracked) continue;               // ★ 깨진 블록 타겟 제외
                    if (block == xBlock) continue;
                    if (block.Data.gemType == targetGemType)
                        unifiedTargets.Add((block.transform.position, block, null));
                }
            }

            // 4b. 살아있는 고블린 타겟 수집
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();
                foreach (var goblin in aliveGoblins)
                {
                    if (goblin.visualObject != null)
                        unifiedTargets.Add((goblin.visualObject.transform.position, null, goblin));
                }
            }

            int blockTargetCount = unifiedTargets.FindAll(t => t.block != null).Count;
            int goblinTargetCount = unifiedTargets.FindAll(t => t.goblin != null).Count;
            Debug.Log($"[타겟 레이져] 타겟 수: 블록={blockTargetCount}({targetGemType}), 고블린={goblinTargetCount}");

            // 거리순 정렬 (가까운 것부터 발사)
            unifiedTargets.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.worldPos, xWorldPos);
                float distB = Vector3.Distance(b.worldPos, xWorldPos);
                return distA.CompareTo(distB);
            });

            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // --- 5단계: 타겟 마킹 (십자선 표시) ---
            List<GameObject> crosshairs = new List<GameObject>();
            for (int i = 0; i < unifiedTargets.Count; i++)
            {
                var ut = unifiedTargets[i];
                // 블록 타겟이면 FixedBlock 제외
                if (ut.block != null && ut.block.Data.specialType == SpecialBlockType.FixedBlock) continue;

                // 십자선 아이콘 생성
                GameObject ch = new GameObject($"LaserCrosshair_{i}");
                ch.transform.SetParent(parent, false);
                ch.transform.position = ut.worldPos;

                var chImg = ch.AddComponent<UnityEngine.UI.Image>();
                chImg.sprite = GetCrosshairSprite();
                chImg.raycastTarget = false;
                // 고블린 타겟은 붉은색 더 강하게
                chImg.color = ut.goblin != null
                    ? new Color(1f, 0f, 0f, 0f)
                    : new Color(1f, 0.2f, 0.1f, 0f);

                RectTransform chRt = ch.GetComponent<RectTransform>();
                chRt.sizeDelta = new Vector2(50f, 50f);
                ch.transform.localScale = Vector3.zero;

                crosshairs.Add(ch);
            }

            // 십자선 등장 애니메이션 (모두 동시에 0→1 스케일, 0.15초)
            {
                float dur = 0.15f;
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    float scale = VisualConstants.EaseOutCubic(t);
                    float alpha = t;
                    foreach (var ch in crosshairs)
                    {
                        if (ch == null) continue;
                        ch.transform.localScale = Vector3.one * scale;
                        var img = ch.GetComponent<UnityEngine.UI.Image>();
                        if (img != null)
                        {
                            Color c = img.color;
                            img.color = new Color(c.r, c.g, c.b, alpha * 0.9f);
                        }
                    }
                    yield return null;
                }
            }

            // 짧은 유지 (0.1초)
            yield return new WaitForSeconds(0.1f);

            // --- 6단계: 레이져 건 등장 ---
            GameObject laserGun = new GameObject("LaserGunIcon");
            laserGun.transform.SetParent(parent, false);
            laserGun.transform.position = xWorldPos;

            var gunImg = laserGun.AddComponent<UnityEngine.UI.Image>();
            gunImg.sprite = GetXBlockIconSprite();
            gunImg.raycastTarget = false;
            gunImg.color = Color.white;

            RectTransform gunRt = laserGun.GetComponent<RectTransform>();
            float gunSize = 75f;
            gunRt.sizeDelta = new Vector2(gunSize, gunSize);
            laserGun.transform.localScale = Vector3.zero;

            // 레이져 건 등장 애니메이션 (0→1.5 스케일, 0.1초)
            {
                float dur = 0.1f;
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    float scale = VisualConstants.EaseOutCubic(t) * 1.5f;
                    laserGun.transform.localScale = Vector3.one * scale;
                    yield return null;
                }
                laserGun.transform.localScale = Vector3.one * 1.5f;
            }

            // --- 7단계: 순차 레이져 발사 루프 (블록 + 고블린 통합) ---
            int blockScoreSum = 0;
            int crosshairIdx = 0;
            for (int i = 0; i < unifiedTargets.Count; i++)
            {
                var ut = unifiedTargets[i];

                // === 블록 타겟 처리 ===
                if (ut.block != null)
                {
                    HexBlock target = ut.block;
                    if (target.Data == null || target.Data.gemType == GemType.None)
                    {
                        if (crosshairIdx < crosshairs.Count && crosshairs[crosshairIdx] != null)
                            Destroy(crosshairs[crosshairIdx]);
                        crosshairIdx++;
                        continue;
                    }
                    if (target.Data.specialType == SpecialBlockType.FixedBlock)
                    {
                        continue; // FixedBlock은 십자선도 안 만듦
                    }

                    // 연쇄 발동 가능한 특수 블록은 pending에 등록
                    if (IsChainActivatable(target.Data.specialType))
                    {
                        if (!pendingSpecialBlocks.Contains(target))
                        {
                            pendingSpecialBlocks.Add(target);
                            target.SetPendingActivation();
                            target.StartWarningBlink(10f);
                        }
                        if (crosshairIdx < crosshairs.Count && crosshairs[crosshairIdx] != null)
                            Destroy(crosshairs[crosshairIdx]);
                        crosshairIdx++;
                        continue;
                    }

                    Vector3 targetPos = target.transform.position;

                    // 레이져 발사 (블록)
                    yield return StartCoroutine(FireLaserBeam(laserGun, xWorldPos, targetPos, parent, i, xColor));

                    // 타겟 히트: 흰색 플래시 + 블록 즉시 파괴
                    StartCoroutine(DestroyFlashOverlay(target));

                    // 미션 카운팅
                    if (target.Data.gemType != GemType.None)
                        GameManager.Instance?.OnSingleGemDestroyedForMission(target.Data.gemType);

                    // 적군 점수 처리
                    var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                    if (sm != null)
                    {
                        if (target.Data.hasThorn)
                            sm.AddEnemyScore(EnemyType.ThornParasite, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        if (target.Data.hasChain)
                            sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        if (target.Data.gemType == GemType.Gray)
                            sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                    }

                    // 점수 합산
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);

                    // 고블린 데미지: 해당 좌표 + 인접 6칸 (좌표별 누적 일괄 적용)
                    if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                    {
                        var damageMap = new Dictionary<HexCoord, int>();
                        damageMap[target.Coord] = 1;
                        foreach (var neighbor in target.Coord.GetAllNeighbors())
                        {
                            if (damageMap.ContainsKey(neighbor))
                                damageMap[neighbor] += 1;
                            else
                                damageMap[neighbor] = 1;
                        }
                        GoblinSystem.Instance.ApplyBatchDamage(damageMap);
                    }

                    // 블록 즉시 파괴 (ClearData)
                    target.ClearData();
                }
                // === 고블린 타겟 처리 ===
                else if (ut.goblin != null)
                {
                    GoblinData goblin = ut.goblin;
                    if (!goblin.isAlive || goblin.visualObject == null)
                    {
                        if (crosshairIdx < crosshairs.Count && crosshairs[crosshairIdx] != null)
                            Destroy(crosshairs[crosshairIdx]);
                        crosshairIdx++;
                        continue;
                    }

                    // 고블린의 현재 위치 (이동했을 수 있으므로 실시간 갱신)
                    Vector3 targetPos = goblin.visualObject.transform.position;

                    // 레이져 발사 (고블린)
                    yield return StartCoroutine(FireLaserBeam(laserGun, xWorldPos, targetPos, parent, i, xColor));

                    // 고블린 데미지 1
                    GoblinSystem.Instance.ApplyDamageAtPosition(goblin.position, 1);
                }

                // 십자선 제거
                if (crosshairIdx < crosshairs.Count && crosshairs[crosshairIdx] != null)
                    Destroy(crosshairs[crosshairIdx]);
                crosshairIdx++;

                // 타겟 간 간격 (초고속 연사)
                yield return new WaitForSeconds(0.005f);
            }

            // --- 8단계: 레이져 건 자폭 ---
            {
                // 흰색 플래시
                if (gunImg != null) gunImg.color = Color.white;
                yield return new WaitForSeconds(0.05f);

                // 파편 산개
                int debrisCount = 8;
                for (int d = 0; d < debrisCount; d++)
                {
                    float angle = d * (360f / debrisCount) + Random.Range(-10f, 10f);
                    float rad = angle * Mathf.Deg2Rad;
                    StartCoroutine(LaserDebris(xWorldPos, new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)), xColor));
                }

                // 스케일 축소 → 소멸 (0.15초)
                float dur = 0.15f;
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    float scale = 1.5f * (1f - VisualConstants.EaseInQuad(t));
                    laserGun.transform.localScale = Vector3.one * scale;
                    if (gunImg != null)
                        gunImg.color = new Color(1f, 1f, 1f, 1f - t);
                    yield return null;
                }
                Destroy(laserGun);
            }

            // 남은 십자선 정리
            foreach (var ch in crosshairs)
                if (ch != null) Destroy(ch);

            // 화면 흔들림
            StartCoroutine(ScreenShake(VisualConstants.ShakeMediumIntensity * 0.5f, VisualConstants.ShakeMediumDuration * 0.5f));

            // --- 9단계: 점수 계산 ---
            int totalScore = 800 + blockScoreSum;
            Debug.Log($"[타겟 레이져] === 완료 === 점수={totalScore} (기본:800 + 블록:{blockScoreSum}), 타겟={unifiedTargets.Count}");

            // --- 10단계: 완료 이벤트 ---
            OnXBlockComplete?.Invoke(totalScore);
            activeBlocks.Remove(xBlock);
            activeXBlockCount--;
        }

        /// <summary>
        /// 레이져 건 자폭 시 파편 하나의 애니메이션.
        /// </summary>
        private IEnumerator LaserDebris(Vector3 origin, Vector2 direction, Color color)
        {
            Transform par = effectParent != null ? effectParent : hexGrid.transform;

            GameObject debris = new GameObject("LaserDebris");
            debris.transform.SetParent(par, false);
            debris.transform.position = origin;

            var img = debris.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(color.r * 0.8f + 0.2f, color.g * 0.8f + 0.2f, color.b * 0.8f + 0.2f, 1f);

            RectTransform rt = debris.GetComponent<RectTransform>();
            float sz = Random.Range(4f, 8f);
            rt.sizeDelta = new Vector2(sz, sz);

            float speed = Random.Range(120f, 220f);
            Vector2 vel = direction * speed;
            float lifetime = Random.Range(0.2f, 0.35f);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                debris.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= 0.95f;
                img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - t);
                float sc = sz * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(sc, sc);
                yield return null;
            }

            Destroy(debris);
        }

        /// <summary>
        /// 레이져 빔 발사 공통 코루틴 (블록/고블린 공용).
        /// 총구 회전 → 빔 즉시 표시 → 빔 후퇴(뒤→앞) 순서로 처리.
        /// </summary>
        private IEnumerator FireLaserBeam(GameObject laserGun, Vector3 gunPos, Vector3 targetPos,
            Transform parent, int index, Color beamColor)
        {
            // 총구 즉시 회전
            {
                Vector3 dir = targetPos - gunPos;
                float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 45f;
                laserGun.transform.localRotation = Quaternion.Euler(0, 0, targetAngle);
            }

            // 빔 방향/길이 계산
            Vector3 beamDir = targetPos - gunPos;
            float beamAngle = Mathf.Atan2(beamDir.y, beamDir.x) * Mathf.Rad2Deg;
            float beamLength = Vector3.Distance(gunPos, targetPos);

            // 코어 빔 (밝은 시안, 두꺼운 라인)
            GameObject beam = new GameObject($"LaserBeam_{index}");
            beam.transform.SetParent(parent, false);
            beam.transform.position = gunPos;
            beam.transform.localRotation = Quaternion.Euler(0, 0, beamAngle);

            var beamImg = beam.AddComponent<UnityEngine.UI.Image>();
            beamImg.raycastTarget = false;
            beamImg.color = new Color(0.7f, 1f, 1f, 1f);

            RectTransform beamRt = beam.GetComponent<RectTransform>();
            beamRt.pivot = new Vector2(0f, 0.5f);
            beamRt.sizeDelta = new Vector2(beamLength, 10f);

            // 글로우 빔 (넓고 투명한 외곽 글로우)
            GameObject beamGlow = new GameObject($"LaserBeamGlow_{index}");
            beamGlow.transform.SetParent(parent, false);
            beamGlow.transform.position = gunPos;
            beamGlow.transform.localRotation = Quaternion.Euler(0, 0, beamAngle);

            var glowImg = beamGlow.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(0f, 1f, 1f, 0.4f);

            RectTransform glowRt = beamGlow.GetComponent<RectTransform>();
            glowRt.pivot = new Vector2(0f, 0.5f);
            glowRt.sizeDelta = new Vector2(beamLength, 24f);

            // 빔 후퇴: 뒤(총구)에서 앞(타겟)으로 빠르게 줄어듦
            {
                float retractDur = 0.025f;
                float elapsed = 0f;
                while (elapsed < retractDur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / retractDur);
                    float eased = VisualConstants.EaseInQuad(t);

                    Vector3 newStart = Vector3.Lerp(gunPos, targetPos, eased);
                    beam.transform.position = newStart;
                    beamGlow.transform.position = newStart;

                    float remainLen = beamLength * (1f - eased);
                    beamRt.sizeDelta = new Vector2(remainLen, 10f * (1f - eased * 0.5f));
                    glowRt.sizeDelta = new Vector2(remainLen, 24f * (1f - eased * 0.5f));

                    beamImg.color = new Color(0.7f, 1f, 1f, 1f - eased * 0.7f);
                    glowImg.color = new Color(0f, 1f, 1f, 0.4f * (1f - eased));

                    yield return null;
                }
                Destroy(beam);
                Destroy(beamGlow);
            }
        }

        /// <summary>
        /// 연쇄 발동 가능한 특수 블록인지 판별.
        /// TimeBomb, MoveBlock, FixedBlock은 발동 로직이 없으므로
        /// 일반 블록처럼 파괴해야 함 (pending 마킹 금지).
        /// </summary>
        private static bool IsChainActivatable(SpecialBlockType type)
        {
            return type == SpecialBlockType.Drill ||
                   type == SpecialBlockType.Bomb ||
                   type == SpecialBlockType.Rainbow ||
                   type == SpecialBlockType.XBlock ||
                   type == SpecialBlockType.Drone;
        }

        // ============================================================
        // 이펙트 (시각 효과) 메서드들
        // 타겟 레이져 발동 시 화면에 표시되는 연출을 담당
        // ============================================================

        // ============================================================
        // 화면 흔들림 (Screen Shake)
        // 타겟 레이져 발동 시 게임판 전체를 떨리게 하는 효과
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
        /// <summary>
        /// 화면(게임판) 흔들림 효과 코루틴.
        /// </summary>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            bool isOwner = VisualConstants.TryBeginScreenShake();
            if (!isOwner) yield break;

            Transform target = hexGrid != null ? hexGrid.transform : transform;

            float elapsed = 0f;

            try
            {
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float decay = 1f - VisualConstants.EaseInQuad(t);
                    float x = Random.Range(-1f, 1f) * intensity * decay;
                    float y = Random.Range(-1f, 1f) * intensity * decay;
                    target.localPosition = new Vector3(x, y, 0);
                    yield return null;
                }
            }
            finally
            {
                target.localPosition = Vector3.zero;
                VisualConstants.EndScreenShake();
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
            // 다수 특수 블록 동시 발동 시 줌 펀치는 하나만 실행
            bool isOwner = VisualConstants.TryBeginZoomPunch();
            if (!isOwner) yield break;

            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = Vector3.one;
            Vector3 punchScale = origScale * targetScale; // 목표 확대 크기

            // 확대 단계: 원래 크기 → 확대 크기
            float elapsed = 0f;
            try
            {
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
            }
            finally
            {
                target.localScale = Vector3.one; // 최종 안전 장치: 정확히 원래 크기로
                VisualConstants.EndZoomPunch();
            }
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

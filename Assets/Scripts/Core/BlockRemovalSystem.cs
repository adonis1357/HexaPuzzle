// =====================================================================================
// BlockRemovalSystem.cs — 블록 제거 시스템 (게임의 "엔진" 역할)
// =====================================================================================
//
// [한줄 요약]
// 매칭된 블록을 삭제하고, 빈 자리를 중력처럼 떨어뜨려 채우고, 새 블록을 생성하는
// "연쇄 반응(캐스케이드)"의 핵심 시스템입니다.
//
// [비유로 이해하기]
// 이 시스템은 마치 "자판기"와 같습니다:
// 1. 음료(블록)를 꺼내면(매칭 삭제)
// 2. 위의 음료들이 아래로 떨어지고(낙하 물리)
// 3. 맨 위에 새 음료가 채워지며(리필)
// 4. 우연히 또 같은 색이 맞으면 다시 제거됩니다(연쇄 반응)
// 이 과정을 더 이상 매칭이 없을 때까지 반복합니다.
//
// [처리 흐름]
// 매칭 감지 -> 하이라이트 연출 -> 특수 블록 생성(합체 연출) -> 일반 블록 삭제
// -> 기존 특수 블록 발동 -> 낙하 물리 -> 빈 칸 리필 -> 새 매칭 확인 -> (반복, 최대 20회)
//
// [주요 책임]
// - 매칭된 블록의 삭제 애니메이션 (축소, 플래시 등)
// - 특수 블록 합체 애니메이션 (번개 + 소용돌이로 모이는 연출)
// - 중력 기반 낙하 물리 (가속도, 바운스 포함)
// - 새 블록 생성 및 위에서 떨어지는 연출
// - 연쇄 반응(캐스케이드) 루프 관리
// - 특수 블록(드릴, 폭탄 등) 발동 조율
// - 빅뱅(전체 리셋), 게임 시작 낙하 연출
// - 미션 시스템에 제거/생성 이벤트 알림
// =====================================================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 블록 제거 시스템: 매칭된 블록을 삭제하고, 빈 자리를 낙하+리필로 채우며,
    /// 연쇄 반응(캐스케이드)을 관리하는 게임의 핵심 엔진.
    /// </summary>
    public class BlockRemovalSystem : MonoBehaviour
    {
        // ============================================================
        // 참조 (Inspector에서 연결하는 다른 시스템들)
        // - 이 시스템이 일을 처리할 때 필요한 "도구"들입니다.
        // ============================================================
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;                    // 육각형 격자판 (블록들이 배치되는 판)
        [SerializeField] private MatchingSystem matchingSystem;      // 매칭 감지기 (같은 색 블록 찾기)
        [SerializeField] private DrillBlockSystem drillSystem;       // 드릴 특수 블록 시스템 (한 줄 파괴)
        [SerializeField] private BombBlockSystem bombSystem;         // 폭탄 특수 블록 시스템 (주변 폭발)
        [SerializeField] private DonutBlockSystem donutSystem;       // 무지개(도넛) 특수 블록 시스템 (같은 색 전체 파괴)
        [SerializeField] private XBlockSystem xBlockSystem;          // 타겟 레이져 특수 블록 시스템 (같은 색 전체 파괴)
        [SerializeField] private DroneBlockSystem droneSystem;       // 드론 특수 블록 시스템 (우선순위 단일 타격)
        private SpecialBlockComboSystem comboSystem;                  // 특수 블록 합성 시스템 (즉시 합성용)

        // ============================================================
        // 애니메이션 설정값 (시간 관련)
        // - 블록이 사라지거나 떨어지는 속도를 조절하는 "타이밍 설정"
        // ============================================================
        [Header("Animation Settings")]
        [SerializeField] private float matchHighlightDuration = 0.15f;  // 매칭 블록이 반짝이는 시간 (초)
        [SerializeField] private float removeAnimationDuration = 0.14f; // 블록 삭제 애니메이션 시간 (초)
        [SerializeField] private float cascadeDelay = 0.1f;             // 연쇄 반응 사이의 대기 시간 (초)

        // ============================================================
        // 낙하 물리 설정 (블록이 떨어질 때의 물리 법칙)
        // - 실제 물건이 떨어지는 것처럼 자연스러운 움직임을 만드는 설정
        // ============================================================
        [Header("Fall Physics")]
        [SerializeField] private float gravity = 2500f;        // 중력 가속도 (클수록 빨리 떨어짐)
        [SerializeField] private float maxFallSpeed = 1500f;   // 최대 낙하 속도 (너무 빨라지지 않게 제한)
        [SerializeField] private float bounceRatio = 0.3f;     // 바운스 비율 (착지 시 튕기는 정도, 0.3 = 30%)
        [SerializeField] private float bounceThreshold = 50f;  // 바운스 최소 속도 (이 이하면 안 튕김)

        // ============================================================
        // 내부 상태 플래그
        // ============================================================
        private bool isProcessing = false;   // 현재 블록 제거/연쇄 처리 중인지 여부 (중복 실행 방지)
        private bool isFalling = false;      // 현재 낙하 애니메이션 진행 중인지 여부 (재진입 방지)

        // 연쇄 깊이 추적: 현재 몇 번째 연쇄인지 기록 (콤보 계산용)
        // 예: 0=첫 매칭, 1=연쇄 1회, 2=연쇄 2회...
        private int currentCascadeDepth = 0;
        /// <summary>현재 연쇄(캐스케이드) 깊이를 외부에서 읽을 수 있는 속성</summary>
        public int CurrentCascadeDepth => currentCascadeDepth;

        // 진행 하트비트: ProcessMatchesInline 등에서 주기적으로 갱신하여 WaitForBRSComplete 타임아웃 방지
        private float lastProgressTime = 0f;
        /// <summary>BRS 내부 진행 시각 (Time.time 기준)</summary>
        public float LastProgressTime => lastProgressTime;

        // 현재 처리 중인 매칭 그룹 수 (동시 다색 매칭 보너스 계산용)
        private int currentMatchGroupCount = 0;
        /// <summary>현재 처리 중인 매칭 그룹 수 (동시 다색 매칭 보너스용)</summary>
        public int CurrentMatchGroupCount => currentMatchGroupCount;

        // 각 블록의 "원래 자리(슬롯 위치)"를 캐싱 (매번 계산하지 않고 한 번 저장해두고 재사용)
        // - 블록이 낙하 후 정확한 위치로 돌아가야 하므로 원래 좌표를 기억해두는 것
        private Dictionary<HexBlock, Vector2> slotPositions = new Dictionary<HexBlock, Vector2>();
        // 같은 X좌표(열)에 있는 블록들을 묶어서 저장 (낙하 처리 시 열 단위로 처리하기 위함)
        private Dictionary<int, List<HexBlock>> columnCache = null;
        // 슬롯 캐시가 이미 생성되었는지 여부
        private bool slotsCached = false;

        // ============================================================
        // 이벤트 (다른 시스템에 "이런 일이 일어났다"고 알리는 신호)
        // - 라디오 방송처럼, 관심 있는 시스템이 이 신호를 받아서 반응합니다
        // ============================================================

        /// <summary>블록이 제거되었을 때 발생 (제거 개수, 연쇄 깊이, 평균 위치) - 점수 팝업용</summary>
        public event System.Action<int, int, Vector3> OnBlocksRemoved;
        /// <summary>모든 연쇄 반응이 끝났을 때 발생 - GameManager가 다음 턴을 진행하는 신호</summary>
        public event System.Action OnCascadeComplete;
        /// <summary>빅뱅(전체 블록 리셋)이 시작될 때 발생</summary>
        public event System.Action OnBigBang;

        /// <summary>미션 시스템용: 특정 색상의 보석이 몇 개 제거되었는지 상세 알림</summary>
        public event System.Action<int, GemType, int> OnGemsRemovedDetailed; // (count, gemType, cascadeDepth)
        /// <summary>미션 시스템용: 특수 블록이 새로 생성되었을 때 알림 (타입, 드릴방향)</summary>
        public event System.Action<SpecialBlockType, DrillDirection> OnSpecialBlockCreated;

        // 블록 착지 시 "찌그러짐(squash)" 이펙트가 동시에 여러 번 실행되지 않도록 추적하는 딕셔너리
        private Dictionary<HexBlock, Coroutine> squashEffectCoroutines = new Dictionary<HexBlock, Coroutine>();
        /// <summary>적군 블록이 제거되었을 때 알림 (어떤 블록, 어떤 적 타입)</summary>
        public event System.Action<HexBlock, EnemyType> OnEnemyRemoved;
        /// <summary>특수 블록이 발동(사용)되었을 때 알림 - 미션 추적용</summary>
        public event System.Action OnSpecialBlockUsed;
        /// <summary>보석이 수집될 때 시각 연출용 이벤트 (화면 위치, 보석 색상)</summary>
        public event System.Action<Vector3, GemType> OnGemCollectedVisual;

        /// <summary>현재 블록 제거/연쇄 처리 중인지 외부에서 확인하는 속성</summary>
        public bool IsProcessing => isProcessing;

        /// <summary>
        /// [비상 리셋] 게임이 멈춘(Stuck) 상태일 때 호출하는 긴급 복구 메서드.
        /// 비유하자면, 자판기가 고장났을 때 "리셋 버튼"을 누르는 것과 같습니다.
        /// 진행 중인 모든 애니메이션을 중단하고, 상태 플래그를 초기화하고,
        /// 남아있는 이펙트를 정리하고, 블록들을 원래 위치/크기로 복원합니다.
        /// </summary>
        public void ForceReset()
        {
            StopAllCoroutines();
            isProcessing = false;
            isFalling = false;
            currentCascadeDepth = 0;

            // SquashEffect 코루틴 추적 정리
            squashEffectCoroutines.Clear();

            // 코루틴 중단으로 남은 이펙트 오브젝트 정리
            CleanupOrphanedEffects();

            // 블록 시각 상태 복원 (scale, rotation, position) - 캐시 클리어 전에 수행
            RestoreAllBlockStates();

            slotsCached = false;
            Debug.Log("[BlockRemovalSystem] ForceReset called");
        }

        /// <summary>
        /// [이펙트 청소부] 강제 중단 등으로 화면에 남아버린 임시 이펙트(불꽃, 번개선 등)를 찾아서 제거.
        /// 비유하자면, 파티가 끝난 뒤 바닥에 떨어진 색종이를 쓸어담는 것과 같습니다.
        /// - this.transform의 자식: Spark(불꽃), LightningArc(번개선), DestroyFlash(파괴 플래시) 등
        /// - 각 블록 transform의 자식: ElectricArc(전기 아크), SpecialImpact(특수 충격파) 등
        /// </summary>
        private void CleanupOrphanedEffects()
        {
            // 1. this.transform의 임시 이펙트 자식 정리
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.name.StartsWith("Spark") || child.name.StartsWith("LightningArc") ||
                    child.name.StartsWith("DestroyFlash") || child.name.StartsWith("BloomLayer") ||
                    child.name.StartsWith("MatchPulseGlow") || child.name.StartsWith("GrayShard") ||
                    child.name.StartsWith("CrackedShard") || child.name.StartsWith("CrackedBurstRing") ||
                    child.name.StartsWith("AdjDmgEdgeLine"))
                    Destroy(child);
            }

            // 2. 블록 transform의 임시 이펙트 자식 정리
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null) continue;
                    for (int i = block.transform.childCount - 1; i >= 0; i--)
                    {
                        GameObject child = block.transform.GetChild(i).gameObject;
                        if (child.name.StartsWith("ElectricArc") ||
                            child.name.StartsWith("SpecialImpact") ||
                            child.name.StartsWith("SpawnGlow") ||
                            child.name.StartsWith("DestroyFlash") ||
                            child.name.StartsWith("MatchPulseGlow") ||
                            child.name.StartsWith("GrayCrack") ||
                            child.name.StartsWith("CrackedBurstFlash"))
                            Destroy(child);
                    }
                }
            }

            // 3. Reset cascade depth
            currentCascadeDepth = 0;
        }

        /// <summary>
        /// [블록 원상 복구] 모든 블록의 시각 상태(크기, 회전, 위치)를 원래대로 되돌림.
        /// 낙하나 삭제 애니메이션 도중 강제 중단되면 블록이 이상한 크기/위치에 남을 수 있는데,
        /// 이 메서드가 모든 블록을 "정상 상태"로 돌려놓습니다.
        /// </summary>
        private void RestoreAllBlockStates()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                block.transform.localScale = Vector3.one;
                block.transform.localRotation = Quaternion.identity;
                if (slotPositions.ContainsKey(block))
                    SetBlockAnchoredPosition(block, slotPositions[block]);
            }
        }

        /// <summary>
        /// [캐시 비우기] 슬롯(블록 자리) 위치 캐시를 무효화합니다.
        /// 스테이지가 바뀌면 그리드 구조가 달라지므로, 이전에 저장해둔 위치 정보를 버리고
        /// 다음에 다시 계산하도록 합니다. 스테이지 전환 시 반드시 호출해야 합니다.
        /// </summary>
        public void InvalidateSlotCache()
        {
            slotsCached = false;
            slotPositions.Clear();
            columnCache = null;
        }


        /// <summary>
        /// [초기화] Unity가 게임 시작 시 자동 호출하는 메서드.
        /// Inspector에서 연결되지 않은 시스템 참조를 자동으로 찾아서 연결합니다.
        /// 마치 "부품이 빠졌으면 창고에서 찾아서 끼워넣는" 과정입니다.
        /// </summary>
        private void Start()
        {
            // hexGrid가 Inspector에서 연결되지 않았으면 씬에서 자동 탐색
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[BlockRemovalSystem] HexGrid auto-found: " + hexGrid.name);
            }
            if (matchingSystem == null)
                matchingSystem = FindObjectOfType<MatchingSystem>();
            if (drillSystem == null)
                drillSystem = FindObjectOfType<DrillBlockSystem>();
            if (bombSystem == null)
                bombSystem = FindObjectOfType<BombBlockSystem>();
            if (bombSystem == null)
            {
                // 씬에 BombBlockSystem이 없으면 자동 생성
                bombSystem = gameObject.AddComponent<BombBlockSystem>();
                Debug.LogWarning("[BlockRemovalSystem] BombBlockSystem이 씬에 없어 자동 생성됨");
            }
            if (donutSystem == null)
                donutSystem = FindObjectOfType<DonutBlockSystem>();
            if (xBlockSystem == null)
                xBlockSystem = FindObjectOfType<XBlockSystem>();
            if (droneSystem == null)
                droneSystem = FindObjectOfType<DroneBlockSystem>();
            comboSystem = FindObjectOfType<SpecialBlockComboSystem>();
        }

        /// <summary>
        /// [슬롯 위치 캐시 생성] 모든 블록의 "원래 자리(슬롯)" 좌표를 한 번만 계산해서 저장합니다.
        /// 비유하자면, 극장 좌석표를 한 번 만들어두면 누가 자리를 비워도 어디로 돌아가야 하는지 알 수 있는 것처럼,
        /// 블록이 낙하/삭제 애니메이션 중에도 원래 자리를 정확히 알 수 있게 합니다.
        /// 또한 블록들을 X좌표(열) 기준으로 묶어서 열 단위 낙하 처리를 가능하게 합니다.
        /// </summary>
        private void EnsureSlotsCached()
        {
            if (slotsCached && slotPositions.Count > 0) return;

            slotPositions.Clear();
            columnCache = new Dictionary<int, List<HexBlock>>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                RectTransform rt = block.GetComponent<RectTransform>();
                Vector2 pos = rt != null ? rt.anchoredPosition : (Vector2)block.transform.localPosition;
                slotPositions[block] = pos;

                int colKey = Mathf.RoundToInt(pos.x);
                if (!columnCache.ContainsKey(colKey))
                    columnCache[colKey] = new List<HexBlock>();
                columnCache[colKey].Add(block);
            }

            // Y좌표 오름차순으로 정렬 (아래쪽부터 = 작은 Y값부터)
            // 이렇게 하면 column[0]이 가장 아래 블록, column[끝]이 가장 위 블록이 됩니다
            foreach (var key in columnCache.Keys.ToList())
            {
                columnCache[key] = columnCache[key]
                    .OrderBy(b => slotPositions[b].y)
                    .ToList();
            }

            slotsCached = true;
            Debug.Log($"[BlockRemovalSystem] Cached {slotPositions.Count} slot positions, {columnCache.Count} columns");
        }

        /// <summary>
        /// [블록 위치 설정] 블록을 지정한 2D 좌표로 이동시킵니다.
        /// UI(RectTransform)를 사용하는 경우와 일반 Transform을 사용하는 경우 모두 처리합니다.
        /// </summary>
        private void SetBlockAnchoredPosition(HexBlock block, Vector2 pos)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = pos;
            else block.transform.localPosition = new Vector3(pos.x, pos.y, 0);
        }

        /// <summary>
        /// [블록을 원래 자리로 복원] 블록을 캐시에 저장된 슬롯 위치로 되돌리고 크기를 정상(1배)으로 리셋합니다.
        /// 애니메이션이 끝나거나 강제 중단된 후 블록을 정위치로 돌려놓을 때 사용합니다.
        /// </summary>
        private void RestoreBlockToSlot(HexBlock block)
        {
            if (block == null) return;
            if (slotPositions.TryGetValue(block, out Vector2 pos))
                SetBlockAnchoredPosition(block, pos);
            block.transform.localScale = Vector3.one;
        }

        // ============================================================
        // 매칭 처리 (Matching Processing)
        // - 매칭된 블록 그룹을 받아서 삭제 + 연쇄 반응을 시작하는 진입점
        // ============================================================

        /// <summary>
        /// [매칭 처리 시작] 매칭된 블록 그룹 리스트를 받아서 삭제 + 연쇄 처리를 시작합니다.
        /// 이미 처리 중이거나 매칭이 없으면 무시합니다.
        /// InputSystem -> RotationSystem -> MatchingSystem -> 여기로 전달됩니다.
        /// </summary>
        public void ProcessMatches(List<MatchingSystem.MatchGroup> matches)
        {
            if (isProcessing || matches == null || matches.Count == 0) return;
            StartCoroutine(ProcessMatchesCoroutine(matches));
        }

        /// <summary>
        /// [매칭 + 대기 중 특수블록 동시 처리] 매칭된 블록과 "대기 중(pending)"인 특수 블록을 동시에 처리합니다.
        /// 연쇄 반응 중 낙하 후 새 매칭이 발견되었을 때, 이전 턴에서 생성되어 아직 발동하지 않은
        /// 특수 블록(pending)과 함께 동시에 처리하기 위한 메서드입니다.
        /// </summary>
        public void ProcessMatchesWithPendingSpecials(List<MatchingSystem.MatchGroup> matches, List<HexBlock> pendingSpecials)
        {
            if (isProcessing) return;
            bool hasMatches = matches != null && matches.Count > 0;
            bool hasPending = pendingSpecials != null && pendingSpecials.Count > 0;
            if (!hasMatches && !hasPending) return;
            StartCoroutine(ProcessMatchesWithPendingCoroutine(matches, pendingSpecials));
        }

        /// <summary>
        /// [매칭+대기특수블록 동시 처리 코루틴] 매칭과 pending 특수블록을 함께 처리하고,
        /// 이후 연쇄 반응 루프(CascadeWithPendingLoop)를 실행합니다.
        /// 코루틴 = 한 프레임에 다 처리하지 않고, 여러 프레임에 걸쳐 애니메이션을 보여주는 방식.
        /// </summary>
        private IEnumerator ProcessMatchesWithPendingCoroutine(List<MatchingSystem.MatchGroup> matches, List<HexBlock> pendingSpecials)
        {
            isProcessing = true;
            lastProgressTime = Time.time;
            EnsureSlotsCached();  // 블록 원래 위치 캐시 확인

            // 안전 검사: 매칭 데이터가 유효한지 확인
            if (matches == null || matches.Count == 0)
            {
                // 매칭은 없지만 대기 중 특수블록만 있는 경우도 처리
                if (pendingSpecials != null && pendingSpecials.Count > 0)
                {
                    yield return StartCoroutine(ProcessMatchesInline(null, pendingSpecials));
                }
                else
                {
                    isProcessing = false;
                    yield break;  // 아무것도 없으면 즉시 종료
                }
            }
            else
            {
                // 매칭 + 특수블록을 인라인(한 메서드 안에서) 처리 (재귀 호출 없음)
                yield return StartCoroutine(ProcessMatchesInline(matches, pendingSpecials));
            }

            // 낙하 + 연쇄 처리를 반복문으로 실행 (더 이상 매칭이 없을 때까지)
            yield return StartCoroutine(CascadeWithPendingLoop());
            // 참고: CascadeWithPendingLoop 내부에서 isProcessing = false 및 OnCascadeComplete 호출
        }


        /// <summary>
        /// [매칭 처리 코루틴 (기본)] 매칭 그룹만 처리하는 코루틴입니다 (대기 특수블록 없음).
        /// 처리 흐름: 매칭 인라인 처리 -> 연쇄 반응 루프(낙하 + 재매칭 반복)
        /// </summary>
        private IEnumerator ProcessMatchesCoroutine(List<MatchingSystem.MatchGroup> matches)
        {
            isProcessing = true;
            lastProgressTime = Time.time;
            EnsureSlotsCached();

            // 안전 검사: 매칭 데이터가 비어있으면 즉시 종료
            if (matches == null || matches.Count == 0)
            {
                isProcessing = false;
                yield break;
            }

            // 매칭을 인라인(한 메서드 안에서)으로 처리 (재귀 호출 없이)
            yield return StartCoroutine(ProcessMatchesInline(matches, null));

            // 낙하 + 연쇄 처리를 반복문으로 실행 (더 이상 매칭이 없을 때까지)
            yield return StartCoroutine(CascadeWithPendingLoop());
            // 참고: CascadeWithPendingLoop 내부에서 isProcessing = false 및 OnCascadeComplete 호출
        }

        /// <summary>
        /// [특수 블록 발동 + 완료 대기] 특수 블록(드릴, 폭탄 등)을 발동시키고 완료될 때까지 기다립니다.
        /// 비유하자면, "폭죽에 불을 붙이고 터질 때까지 기다리는" 역할입니다.
        /// 각 특수 블록 타입별로 해당 시스템의 발동 메서드를 호출하고,
        /// 최대 5초간 완료를 기다립니다 (타임아웃 시 강제 리셋).
        /// 새 특수 블록 추가 시 switch문에 case만 추가하면 됩니다.
        /// </summary>
        private IEnumerator ActivateSpecialAndWaitLocal(HexBlock block)
        {
            // Safety: 블록이 이미 파괴되었거나 데이터가 없으면 즉시 종료
            if (block == null || block.Data == null || block.gameObject == null) yield break;

            // 발동 전 specialType 캐싱 (발동 중 Data가 변경될 수 있음)
            SpecialBlockType cachedType = block.Data.specialType;
            if (cachedType == SpecialBlockType.None) yield break;

            lastProgressTime = Time.time; // 하트비트: 특수 블록 발동 시작
            float timeout = 5f;
            float waited = 0f;

            // 발동 직전 데이터 재검증 (동시 발동 중 다른 특수 블록이 이 블록을 파괴했을 수 있음)
            if (block.Data == null || block.Data.gemType == GemType.None || block.Data.specialType == SpecialBlockType.None)
            {
                Debug.LogWarning($"[BRS] ActivateSpecialAndWaitLocal: block data invalidated before activation (cachedType={cachedType}). Cleaning up.");
                // 잔존 pending 상태 정리 (깜빡임 정지 + 블록 제거)
                block.StopWarningBlink();
                if (block.Data != null && block.Data.gemType != GemType.None)
                    block.ClearData();
                yield break;
            }

            // 미션 시스템에 특수 블록 사용 알림
            OnSpecialBlockUsed?.Invoke();

            // 특수 블록 자체의 색상도 미션에 기여 (발동 시 소멸되므로 일반 블록 제거와 동일하게 카운팅)
            if (block.Data.gemType != GemType.None)
                GameManager.Instance?.OnSingleGemDestroyedForMission(block.Data.gemType);

            switch (cachedType)
            {
                case SpecialBlockType.Drill:
                    if (drillSystem != null)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayDrillSound();
                        drillSystem.ActivateDrill(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (drillSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (drillSystem.IsBlockActive(block)) { Debug.LogError("[BRS] Drill timeout! ForceReset"); drillSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Bomb:
                    Debug.Log($"[폭탄실행] BlockRemovalSystem.Bomb case 진입, block={block?.Coord}, bombSystem={bombSystem != null}");
                    if (bombSystem != null)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayBombSound();
                        Debug.Log($"[폭탄실행] bombSystem.ActivateBomb 호출 직전");
                        bombSystem.ActivateBomb(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (bombSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (bombSystem.IsBlockActive(block)) { Debug.LogError("[BRS] Bomb timeout! ForceReset"); bombSystem.ForceReset(); }
                        Debug.Log($"[폭탄실행] bombSystem.ActivateBomb 완료");
                    }
                    else
                    {
                        Debug.LogError($"[폭탄실행] bombSystem이 NULL! BombBlockSystem 컴포넌트가 씬에 없음");
                    }
                    break;

                case SpecialBlockType.Rainbow:
                    if (donutSystem != null)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayDonutSound();
                        donutSystem.ActivateDonut(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (donutSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (donutSystem.IsBlockActive(block)) { Debug.LogError("[BRS] Donut timeout! ForceReset"); donutSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.XBlock:
                    if (xBlockSystem != null)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayXBlockSound();
                        xBlockSystem.ActivateXBlock(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (xBlockSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (xBlockSystem.IsBlockActive(block)) { Debug.LogError("[BRS] 타겟 레이져 timeout! ForceReset"); xBlockSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Drone:
                    if (droneSystem != null)
                    {
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayDroneSound();
                        droneSystem.ActivateDrone(block);
                        float droneTimeout = 5f;
                        float droneWaited = 0f;
                        while (droneSystem.IsActivating && droneWaited < droneTimeout)
                        {
                            droneWaited += Time.deltaTime;
                            yield return null;
                        }
                        if (droneSystem.IsActivating)
                        {
                            Debug.LogWarning("[BRS] Drone activation timeout! ForceReset.");
                            droneSystem.ForceReset();
                        }
                    }
                    break;

                default:
                    // 발동 로직이 없는 타입(TimeBomb, MoveBlock 등)이 pending으로 잘못 진입한 경우
                    // 블록을 파괴하여 캐스케이드가 멈추지 않도록 함
                    Debug.LogWarning($"[BRS] ActivateSpecialAndWaitLocal: non-activatable type {cachedType} at {block.Coord} — clearing data");
                    block.ClearData();
                    break;
            }
        }

        /// <summary>
        /// [특수 블록 합체 애니메이션] 매칭된 블록들이 중앙으로 "빨려들어가며" 합쳐지는 연출.
        /// 비유하자면, 여러 개의 작은 구슬이 소용돌이 치며 하나로 합쳐져 큰 보석이 되는 장면입니다.
        ///
        /// 연출 구성요소:
        /// 1. 전기 아크 + 번개선: 블록 사이에 전기가 튀는 효과
        /// 2. 스파크: 주변에 불꽃이 튀는 효과
        /// 3. 합체 이동: 곡선 경로 + 소용돌이로 중앙에 모이는 효과
        /// 4. 축소 + 회전: 모이면서 작아지고 회전하는 효과
        ///
        /// 블록 수에 따라 연출 강도가 자동 조절됩니다:
        /// - 4개(드릴): 짧은 합체, 적은 소용돌이
        /// - 5개+(폭탄 등): 긴 합체, 큰 소용돌이, 360도 회전
        /// </summary>
        private IEnumerator SpecialBlockMergeAnimation(
            List<HexBlock> blocks, HexBlock spawnBlock,
            SpecialBlockType specialType, DrillDirection drillDirection, GemType gemType)
        {
            if (blocks == null || spawnBlock == null) yield break;

            Vector3 targetPos = spawnBlock.transform.position;
            // 블록 수에 따라 드라마 조절 (4개=드릴, 5+개=폭탄 등)
            float mergeDuration = 0.4f + blocks.Count * 0.02f;
            float rotationMult = blocks.Count >= 5 ? 360f : 180f;
            float swirlAmount = blocks.Count >= 5 ? 20f : 0f;

            Dictionary<HexBlock, Vector3> startPositions = new Dictionary<HexBlock, Vector3>();
            Dictionary<HexBlock, Vector3> startScales = new Dictionary<HexBlock, Vector3>();

            foreach (var block in blocks)
            {
                if (block != null && block != spawnBlock &&
                    (block.Data == null || block.Data.specialType == SpecialBlockType.None))
                {
                    startPositions[block] = block.transform.position;
                    startScales[block] = block.transform.localScale;
                }
            }

            // 전기 이펙트 + 번개선
            List<GameObject> electricObjects = new List<GameObject>();
            List<GameObject> arcLines = new List<GameObject>();
            foreach (var block in blocks)
                if (block != null)
                    electricObjects.Add(CreateElectricArcObject(block.transform));

            for (int i = 0; i < blocks.Count; i++)
                for (int j = i + 1; j < blocks.Count; j++)
                    if (blocks[i] != null && blocks[j] != null)
                        arcLines.Add(CreateLightningArc(blocks[i].transform, blocks[j].transform));

            // 스파크
            foreach (var block in blocks)
                if (block != null)
                    StartCoroutine(SpawnSparks(block.transform.position, mergeDuration));

            // 합체 애니메이션
            float elapsed = 0f;
            while (elapsed < mergeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / mergeDuration;
                float easeT = 1f - Mathf.Pow(1f - t, 3f);

                foreach (var kvp in startPositions)
                {
                    HexBlock block = kvp.Key;
                    if (block == null) continue;

                    Vector3 startPos = kvp.Value;

                    // 공통 경로: 곡선 이동 + 소용돌이 (블록 수에 따라 강도)
                    Vector3 diff = targetPos - startPos;
                    Vector3 perpendicular = new Vector3(-diff.y, diff.x, 0).normalized * swirlAmount * (1f - easeT);
                    Vector3 midPoint = (startPos + targetPos) / 2f + Vector3.up * 15f * (1f - easeT);
                    Vector3 currentPos;
                    if (easeT < 0.5f)
                        currentPos = Vector3.Lerp(startPos, midPoint, easeT * 2f);
                    else
                        currentPos = Vector3.Lerp(midPoint, targetPos, (easeT - 0.5f) * 2f);
                    currentPos += perpendicular * Mathf.Sin(easeT * Mathf.PI);
                    block.transform.position = currentPos;

                    float pulse = 1f + 0.06f * Mathf.Sin(t * Mathf.PI * 8f);
                    float shrink = 1f - easeT * 0.9f;
                    block.transform.localScale = startScales[block] * shrink * pulse;

                    float rotZ = easeT * rotationMult * (block.GetInstanceID() % 2 == 0 ? 1f : -1f);
                    block.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                }

                UpdateElectricArcs(electricObjects, t);
                UpdateLightningArcs(arcLines, t);

                if (spawnBlock != null)
                {
                    float spawnPulse = 1f + 0.1f * Mathf.Sin(t * Mathf.PI * 10f);
                    spawnBlock.transform.localScale = Vector3.one * spawnPulse;
                }

                yield return null;
            }

            // 정리
            foreach (var obj in electricObjects) if (obj != null) Destroy(obj);
            foreach (var arc in arcLines) if (arc != null) Destroy(arc);

            foreach (var kvp in startPositions)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.transform.localRotation = Quaternion.identity;
                    kvp.Key.transform.localScale = Vector3.zero;
                    // ★ 합체 애니메이션에서 transform.position으로 이동된 블록을
                    // 정확한 슬롯의 anchoredPosition으로 복원 (위치 어긋남 방지)
                    if (slotPositions.TryGetValue(kvp.Key, out Vector2 slotPos))
                        SetBlockAnchoredPosition(kvp.Key, slotPos);
                }
            }
            if (spawnBlock != null)
            {
                spawnBlock.transform.localScale = Vector3.one;
                spawnBlock.transform.localRotation = Quaternion.identity;
            }
            // ★ 스폰 블록도 슬롯 위치로 복원
            if (slotPositions.TryGetValue(spawnBlock, out Vector2 spawnSlotPos))
                SetBlockAnchoredPosition(spawnBlock, spawnSlotPos);

            // 특수 블록 생성 (통합 디스패쳐)
            CreateSpecialBlock(spawnBlock, specialType, drillDirection, gemType);

            // 임팩트 이펙트 (모든 특수 블록 동일)
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySpecialImpactSound();
            StartCoroutine(SpecialSpawnImpact(spawnBlock));

            yield return new WaitForSeconds(0.15f);
        }

        /// <summary>
        /// [특수 블록 생성 디스패쳐] 합체 애니메이션이 끝난 후, 실제로 특수 블록을 생성하는 메서드.
        /// "디스패쳐(배분기)"라는 이름처럼, 블록 타입에 따라 적절한 시스템에 생성을 위임합니다.
        /// 새 특수 블록 종류를 추가할 때는 switch문에 case만 추가하면 됩니다.
        /// </summary>
        private void CreateSpecialBlock(HexBlock block, SpecialBlockType type, DrillDirection direction, GemType gemType)
        {
            // 특수 블록 생성 시 고블린 폭탄 제거
            if (block.Data != null && block.Data.hasGoblinBomb)
            {
                block.Data.hasGoblinBomb = false;
                block.Data.goblinBombCountdown = 0;
            }

            switch (type)
            {
                case SpecialBlockType.Drill:
                    if (drillSystem != null)
                        drillSystem.CreateDrillBlock(block, direction, gemType);
                    break;
                case SpecialBlockType.Bomb:
                    if (bombSystem != null)
                        bombSystem.CreateBombBlock(block, gemType);
                    break;
                case SpecialBlockType.Rainbow:
                    if (donutSystem != null)
                        donutSystem.CreateDonutBlock(block, gemType);
                    break;
                case SpecialBlockType.XBlock:
                    if (xBlockSystem != null)
                        xBlockSystem.CreateXBlock(block, gemType);
                    break;
                case SpecialBlockType.Drone:
                    if (droneSystem != null)
                        droneSystem.CreateDroneBlock(block, gemType);
                    break;
            }
        }

// 참고: ActivateDrillAndWaitLocal/ActivateBombAndWaitLocal은 ActivateSpecialAndWaitLocal로 통합되었습니다.

// 참고: DrillMergeAnimation/BombMergeAnimation은 SpecialBlockMergeAnimation로 통합되었습니다.

        /// <summary>
        /// [전기 이펙트 (레거시)] 블록 주변에 전기 효과를 표시하던 메서드.
        /// 현재는 합체 애니메이션(SpecialBlockMergeAnimation)에서 직접 처리하므로 빈 메서드로 유지.
        /// </summary>
        private IEnumerator ElectricEffect(HexBlock block, float duration)
        {
            if (block == null) yield break;
            // 이제 DrillMergeAnimation에서 직접 처리하므로 빈 메서드로 유지
            yield return null;
        }

        /// <summary>
        /// [드릴 생성 플래시 (레거시)] 드릴 생성 시 플래시 효과를 표시하던 메서드.
        /// 현재는 SpecialSpawnImpact(특수 블록 공통 임팩트)로 대체되어 빈 메서드로 유지.
        /// </summary>
        private IEnumerator DrillSpawnFlash(HexBlock block)
        {
            // DrillSpawnImpact로 대체
            if (block == null) yield break;
            yield return null;
        }

/// <summary>
        /// 블록에 전기 아크 오브젝트 생성
        /// </summary>
        private GameObject CreateElectricArcObject(Transform parent)
        {
            GameObject obj = new GameObject("ElectricArc");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = Vector3.zero;

            var image = obj.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(0.4f, 0.7f, 1f, 0.8f);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60f, 60f);

            return obj;
        }

        /// <summary>
        /// 두 블록 사이 번개선 생성
        /// </summary>
        private GameObject CreateLightningArc(Transform from, Transform to)
        {
            GameObject obj = new GameObject("LightningArc");
            obj.transform.SetParent(transform, false);

            var image = obj.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(0.6f, 0.85f, 1f, 0.7f);

            RectTransform rt = obj.GetComponent<RectTransform>();
            Vector3 mid = (from.position + to.position) / 2f;
            rt.position = mid;

            float dist = Vector3.Distance(from.position, to.position);
            rt.sizeDelta = new Vector2(dist, 3f);

            Vector3 dir = to.position - from.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, angle);

            return obj;
        }

        /// <summary>
        /// 전기 아크 업데이트 (플리커 + 크기 변화)
        /// </summary>
        private void UpdateElectricArcs(List<GameObject> arcs, float progress)
        {
            foreach (var obj in arcs)
            {
                if (obj == null) continue;
                var image = obj.GetComponent<UnityEngine.UI.Image>();
                var rt = obj.GetComponent<RectTransform>();
                if (image == null || rt == null) continue;

                float flicker = Random.Range(0.3f, 1f);
                float alpha = flicker * (1f - progress * 0.5f);
                image.color = new Color(
                    0.4f + Random.Range(0f, 0.3f),
                    0.7f + Random.Range(0f, 0.2f),
                    1f,
                    alpha * 0.9f
                );

                rt.localPosition = new Vector3(
                    Random.Range(-4f, 4f),
                    Random.Range(-4f, 4f),
                    0
                );

                float scale = Random.Range(0.6f, 1.3f) * (1f - progress * 0.3f);
                rt.sizeDelta = new Vector2(60f * scale, 60f * scale);
            }
        }

        /// <summary>
        /// 번개선 업데이트 (위치 추적 + 플리커)
        /// </summary>
        private void UpdateLightningArcs(List<GameObject> arcs, float progress)
        {
            foreach (var obj in arcs)
            {
                if (obj == null) continue;
                var image = obj.GetComponent<UnityEngine.UI.Image>();
                var rt = obj.GetComponent<RectTransform>();
                if (image == null || rt == null) continue;

                float flicker = Random.Range(0.2f, 1f);
                image.color = new Color(
                    0.5f + Random.Range(0f, 0.4f),
                    0.8f + Random.Range(0f, 0.2f),
                    1f,
                    flicker * (1f - progress) * 0.7f
                );

                float thickness = Random.Range(1.5f, 5f) * (1f - progress * 0.5f);
                Vector2 size = rt.sizeDelta;
                size.y = thickness;
                rt.sizeDelta = size;

                float jitter = Random.Range(-3f, 3f);
                Vector3 pos = rt.localPosition;
                pos.y += jitter;
                rt.localPosition = pos;
            }
        }

        /// <summary>
        /// 스파크 파티클 생성
        /// </summary>
        private IEnumerator SpawnSparks(Vector3 center, float duration)
        {
            float elapsed = 0f;
            float spawnInterval = 0.04f;
            float nextSpawn = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= nextSpawn)
                {
                    nextSpawn += spawnInterval;
                    StartCoroutine(AnimateSpark(center));
                }
                yield return null;
            }
        }

        /// <summary>
        /// 개별 스파크 애니메이션
        /// </summary>
        private IEnumerator AnimateSpark(Vector3 center)
        {
            GameObject spark = new GameObject("Spark");
            spark.transform.SetParent(transform, false);
            spark.transform.position = center;

            var image = spark.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(3f, 8f);
            rt.sizeDelta = new Vector2(size, size);

            // 랜덤 방향
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(80f, 200f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            // 밝은 전기색
            Color sparkColor = new Color(
                Random.Range(0.5f, 0.8f),
                Random.Range(0.7f, 1f),
                1f,
                1f
            );
            image.color = sparkColor;

            float lifetime = Random.Range(0.1f, 0.25f);
            float elapsedTime = 0f;

            while (elapsedTime < lifetime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / lifetime;

                Vector3 pos = spark.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                spark.transform.position = pos;

                // 감속
                velocity *= 0.95f;

                // 페이드 아웃 + 축소
                sparkColor.a = 1f - t;
                image.color = sparkColor;
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);

                yield return null;
            }

            Destroy(spark);
        }

        /// <summary>
        /// 드릴 생성 임팩트 - 충격파 + 밝은 플래시
        /// </summary>
/// <summary>
        /// 특수 블록 생성 임팩트 - 충격파 + 밝은 플래시 (모든 특수 블록 공통)
        /// </summary>
        private IEnumerator SpecialSpawnImpact(HexBlock block)
        {
            if (block == null) yield break;

            // 1) 밝은 플래시
            GameObject flashObj = new GameObject("SpecialImpactFlash");
            flashObj.transform.SetParent(block.transform, false);
            flashObj.transform.localPosition = Vector3.zero;

            var flashImage = flashObj.AddComponent<UnityEngine.UI.Image>();
            flashImage.sprite = HexBlock.GetHexFlashSprite();
            flashImage.color = new Color(0.8f, 0.9f, 1f, 1f);
            flashImage.raycastTarget = false;

            RectTransform flashRt = flashObj.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(30f, 30f);

            // 2) 충격파 링
            GameObject ringObj = new GameObject("SpecialImpactRing");
            ringObj.transform.SetParent(block.transform, false);
            ringObj.transform.localPosition = Vector3.zero;

            var ringImage = ringObj.AddComponent<UnityEngine.UI.Image>();
            ringImage.sprite = HexBlock.GetHexFlashSprite();
            ringImage.color = new Color(0.5f, 0.8f, 1f, 0.8f);
            ringImage.raycastTarget = false;

            RectTransform ringRt = ringObj.GetComponent<RectTransform>();
            ringRt.sizeDelta = new Vector2(10f, 10f);

            float punchDuration = 0.15f;
            float impactDuration = 0.3f;
            float elapsed = 0f;

            // 스파크 버스트
            for (int i = 0; i < 12; i++)
                StartCoroutine(AnimateSpark(block.transform.position));

            while (elapsed < impactDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / impactDuration;

                float flashScale = 1f + t * 6f;
                flashRt.sizeDelta = new Vector2(30f * flashScale, 30f * flashScale);
                flashImage.color = new Color(0.8f, 0.9f, 1f, (1f - t) * 0.8f);

                float ringScale = 1f + t * 8f;
                ringRt.sizeDelta = new Vector2(10f * ringScale, 10f * ringScale);
                ringImage.color = new Color(0.5f, 0.8f, 1f, (1f - t) * 0.5f);

                if (elapsed < punchDuration)
                {
                    float pt = elapsed / punchDuration;
                    float punch = 1f + 0.3f * Mathf.Sin(pt * Mathf.PI);
                    block.transform.localScale = Vector3.one * punch;
                }
                else
                {
                    block.transform.localScale = Vector3.one;
                }

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            Destroy(flashObj);
            Destroy(ringObj);
        }


        /// <summary>
        /// [블록 삭제 애니메이션] 블록이 사라지는 2단계 연출입니다.
        /// 비유하자면, 풍선이 "빵!" 하고 터지는 것처럼:
        /// Phase 1 (0~20%): 약간 확대 (터지기 직전 부풀어 오르는 느낌)
        /// Phase 2 (20~100%): 좌우로 찌그러지면서 세로로 쪼그라들며 사라짐
        /// 동시에 백색 플래시 오버레이도 표시됩니다.
        /// </summary>
        private IEnumerator AnimateRemove(HexBlock block)
        {
            if (block == null) yield break;

            // 파괴 순간 백색 플래시를 겹쳐서 표시
            StartCoroutine(DestroyFlashOverlay(block));

            float elapsed = 0f;
            float duration = VisualConstants.DestroyDuration;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (t < expandRatio)
                {
                    // Phase 1: 확대 (0~20%)
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = Vector3.one * scale;
                }
                else
                {
                    // Phase 2: 찌그러짐 + 축소 (20~100%)
                    float shrinkT = (t - expandRatio) / (1f - expandRatio);
                    float sx = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(shrinkT * Mathf.PI);
                    float sy = Mathf.Max(0f, 1f - VisualConstants.EaseInQuad(shrinkT));
                    block.transform.localScale = new Vector3(sx, sy, 1f);
                }

                yield return null;
            }

            block.transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// 삼각형 매칭 블록 축소 제거 애니메이션
        /// 블록이 중심을 향해 작아지면서 자연스럽게 사라짐
        /// </summary>
        private IEnumerator AnimateShrinkRemove(HexBlock block)
        {
            if (block == null) yield break;

            // 이미 합체 애니메이션으로 scale=0인 블록은 축소 애니메이션 생략
            if (block.transform.localScale.sqrMagnitude < 0.001f)
            {
                yield break;
            }

            float elapsed = 0f;
            float duration = removeAnimationDuration; // 0.14초
            float startScale = block.transform.localScale.x;

            while (elapsed < duration)
            {
                if (block == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseInBack: 살짝 커졌다가 빠르게 축소 (탄력감)
                float easeT = t * t * (2.7f * t - 1.7f);
                float scale = Mathf.Max(0f, startScale * (1f - easeT));

                block.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            if (block != null)
                block.transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// 회색(적군) 블록 폭발 이펙트
        /// 인접 매칭으로 제거될 때 사용 — 빠른 팽창 + 밝은 플래시 + 충격파 링 + 파편 산개
        /// </summary>
        private IEnumerator AnimateGrayCrumble(HexBlock block)
        {
            if (block == null) yield break;

            Vector3 burstCenter = block.transform.position;

            // 1. 밝은 백색 플래시 (폭발 순간)
            GameObject flash = new GameObject("GrayBurstFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;
            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.raycastTarget = false;
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.color = new Color(1f, 1f, 1f, 0.95f);
            RectTransform flashRt = flash.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(30f, 30f);

            // 2. 충격파 링
            GameObject ring = new GameObject("GrayBurstRing");
            ring.transform.SetParent(transform, false);
            ring.transform.position = burstCenter;
            var ringImg = ring.AddComponent<UnityEngine.UI.Image>();
            ringImg.raycastTarget = false;
            ringImg.sprite = HexBlock.GetHexBorderSprite();
            ringImg.color = new Color(0.8f, 0.8f, 0.85f, 0.9f);
            RectTransform ringRt = ring.GetComponent<RectTransform>();
            ringRt.sizeDelta = new Vector2(20f, 20f);

            // 3. 파편 대량 산개 (폭발적)
            for (int i = 0; i < 14; i++)
                StartCoroutine(AnimateGrayShard(burstCenter));

            // 4. 블록 빠른 팽창 → 소멸
            float duration = 0.18f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 블록: 빠르게 확대되며 투명해짐 (팡! 터지는 느낌)
                float expandScale = 1f + t * 1.5f;
                block.transform.localScale = Vector3.one * expandScale;

                // 플래시: 급팽창 + 페이드아웃
                float flashScale = 1f + t * 8f;
                flashRt.sizeDelta = new Vector2(30f * flashScale, 30f * flashScale);
                flashImg.color = new Color(1f, 1f, 1f, 0.95f * (1f - t * t));

                // 충격파 링: 확산
                float ringScale = 1f + t * 6f;
                ringRt.sizeDelta = new Vector2(20f * ringScale, 20f * ringScale);
                ringImg.color = new Color(0.8f, 0.8f, 0.85f, 0.9f * (1f - t));

                yield return null;
            }

            block.transform.localScale = Vector3.zero;
            block.transform.localRotation = Quaternion.identity;

            // 잔여 글로우 페이드 (0.12초)
            float fadeDuration = 0.12f;
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                flashImg.color = new Color(1f, 1f, 1f, 0.1f * (1f - t));
                ringImg.color = new Color(0.8f, 0.8f, 0.85f, 0.15f * (1f - t));
                float rs = 1f + t * 3f;
                ringRt.sizeDelta = new Vector2(20f * (7f + rs), 20f * (7f + rs));
                yield return null;
            }

            Destroy(flash);
            Destroy(ring);
        }

        /// <summary>
        /// 회색 블록 폭발 파편 (밝은 톤, 빠르고 넓게 산개)
        /// </summary>
        private IEnumerator AnimateGrayShard(Vector3 center)
        {
            GameObject shard = new GameObject("GrayShard");
            shard.transform.SetParent(transform, false);
            shard.transform.position = center;

            var image = shard.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;

            RectTransform rt = shard.GetComponent<RectTransform>();
            float size = Random.Range(3f, 9f);
            rt.sizeDelta = new Vector2(size, size);

            // 빠르고 넓은 산개
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(150f, 350f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            // 밝은 회색~흰색 톤 (시원한 느낌)
            float gray = Random.Range(0.6f, 0.95f);
            Color shardColor = new Color(gray, gray, gray + 0.05f, 1f);
            image.color = shardColor;

            float lifetime = Random.Range(0.2f, 0.4f);
            float elapsedTime = 0f;

            while (elapsedTime < lifetime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / lifetime;

                Vector3 pos = shard.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                shard.transform.position = pos;

                velocity *= 0.94f;

                shardColor.a = 1f - t * t;
                image.color = shardColor;
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);

                shard.transform.Rotate(0, 0, velocity.magnitude * Time.deltaTime * 0.8f);

                yield return null;
            }

            Destroy(shard);
        }

        /// <summary>
        /// 인접 매칭 대미지 시 대미지 받은 블록의 해당 면에 붉은색 라인 표시
        /// damagedCoord: 대미지 받은 블록 좌표, sourceCoord: 대미지 준 블록 좌표
        /// 0.5초 동안 표시 후 페이드아웃
        /// </summary>
        private IEnumerator ShowAdjacentDamageEdgeLine(HexCoord damagedCoord, HexCoord sourceCoord)
        {
            if (hexGrid == null) yield break;

            float hexSize = hexGrid.HexSize;
            Vector2 centerPos = hexGrid.CalculateFlatTopHexPosition(damagedCoord);

            // 대미지 준 블록 방향 결정 (6방향 중 어느 면인지)
            HexCoord delta = new HexCoord(sourceCoord.q - damagedCoord.q, sourceCoord.r - damagedCoord.r);
            int dirIndex = -1;
            for (int i = 0; i < 6; i++)
            {
                if (HexCoord.Directions[i].q == delta.q && HexCoord.Directions[i].r == delta.r)
                {
                    dirIndex = i;
                    break;
                }
            }
            if (dirIndex < 0) yield break;

            // Flat-top 육각형 꼭짓점 각도: 0°, 60°, 120°, 180°, 240°, 300°
            // 면 i는 꼭짓점 i와 꼭짓점 (i+1)%6 사이의 변
            // Directions 순서(axial): (1,0)=0°, (1,-1)=60°, (0,-1)=120°, (-1,0)=180°, (-1,1)=240°, (0,1)=300°
            // Flat-top hex 꼭짓점: angle = 60*k (k=0~5)
            //   k=0: 0° (오른쪽), k=1: 60° (오른쪽 위), k=2: 120° (왼쪽 위)
            //   k=3: 180° (왼쪽), k=4: 240° (왼쪽 아래), k=5: 300° (오른쪽 아래)
            // 면 dirIndex의 양 끝 꼭짓점 매핑 (flat-top, Y 반전)
            // dir 0 (1,0)  → 꼭짓점 5,0  (300°~0° = 오른쪽 면)
            // dir 1 (1,-1) → 꼭짓점 0,1  (0°~60° = 오른쪽 위 면)
            // dir 2 (0,-1) → 꼭짓점 1,2  (60°~120° = 왼쪽 위 면)
            // dir 3 (-1,0) → 꼭짓점 2,3  (120°~180° = 왼쪽 면)
            // dir 4 (-1,1) → 꼭짓점 3,4  (180°~240° = 왼쪽 아래 면)
            // dir 5 (0,1)  → 꼭짓점 4,5  (240°~300° = 오른쪽 아래 면)
            int v1 = (dirIndex + 5) % 6; // 첫 꼭짓점
            int v2 = dirIndex;            // 둘째 꼭짓점

            float angleDeg1 = 60f * v1;
            float angleDeg2 = 60f * v2;
            float rad1 = angleDeg1 * Mathf.Deg2Rad;
            float rad2 = angleDeg2 * Mathf.Deg2Rad;

            // 꼭짓점 좌표 (flat-top: x=cos, y=sin, Y 반전)
            Vector2 p1 = centerPos + new Vector2(Mathf.Cos(rad1), -Mathf.Sin(rad1)) * hexSize;
            Vector2 p2 = centerPos + new Vector2(Mathf.Cos(rad2), -Mathf.Sin(rad2)) * hexSize;

            // 면 라인 오브젝트 생성
            Vector2 mid = (p1 + p2) * 0.5f;
            float lineLen = Vector2.Distance(p1, p2);
            Vector2 lineDir = p2 - p1;
            float lineAngle = Mathf.Atan2(lineDir.y, lineDir.x) * Mathf.Rad2Deg;

            Transform parent = hexGrid.GridContainer;

            GameObject lineObj = new GameObject("AdjDmgEdgeLine");
            lineObj.transform.SetParent(parent, false);
            RectTransform lrt = lineObj.AddComponent<RectTransform>();
            lrt.anchoredPosition = mid;
            lrt.sizeDelta = new Vector2(lineLen, 4f); // 두께 4px
            lrt.localEulerAngles = new Vector3(0, 0, lineAngle);

            UnityEngine.UI.Image limg = lineObj.AddComponent<UnityEngine.UI.Image>();
            Color edgeColor = new Color(1f, 0.15f, 0.1f, 0.9f); // 붉은색
            limg.color = edgeColor;
            limg.raycastTarget = false;

            // 0.5초 페이드아웃
            float fadeDur = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDur);
                float alpha = 0.9f * (1f - t);
                if (limg != null)
                    limg.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, alpha);
                yield return null;
            }

            if (lineObj != null) Destroy(lineObj);
        }

        /// <summary>
        /// 깨진 블록(isCracked) 전용 붉은색 폭발 이펙트
        /// 깨진 블록이 매칭 삭제될 때 — 강렬한 붉은 플래시 + 충격파 링 + 붉은 파편
        /// </summary>
        private IEnumerator AnimateCrackedBurst(HexBlock block)
        {
            if (block == null) yield break;

            Vector3 burstCenter = block.transform.position;

            // 블록 색상 캐싱 (파편 색상용)
            Color blockColor = Color.gray;
            if (block.Data != null && block.Data.gemType != GemType.None)
                blockColor = GemColors.GetColor(block.Data.gemType);

            // 1. 7개 파편 산개 — 길쭉한 삼각형/사각형/오각형 혼합, 크기 제각각
            int[] shardShapes = { 3, 4, 5, 3, 5, 4, 3 };
            for (int i = 0; i < 7; i++)
                StartCoroutine(AnimateCrackedShard(burstCenter, blockColor, shardShapes[i]));

            // 2. 백색 순간 플래시 (파쇄 순간 강조)
            GameObject flash = new GameObject("CrackedBurstFlash");
            flash.transform.SetParent(transform, false);
            flash.transform.position = burstCenter;
            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.raycastTarget = false;
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.color = new Color(1f, 1f, 1f, 0.7f);
            RectTransform flashRt = flash.GetComponent<RectTransform>();
            float flashSize = hexGrid != null ? hexGrid.HexSize * 1.2f : 60f;
            flashRt.sizeDelta = new Vector2(flashSize, flashSize);

            // 3. 블록 즉시 소멸 (팽창 없이 빠르게 축소)
            float duration = 0.08f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (block == null) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                block.transform.localScale = Vector3.one * (1f - VisualConstants.EaseInQuad(t));
                float fScale = 1f + t * 1.5f;
                flashRt.sizeDelta = new Vector2(flashSize * fScale, flashSize * fScale);
                flashImg.color = new Color(1f, 1f, 1f, 0.7f * (1f - t));
                yield return null;
            }

            if (block != null)
            {
                block.transform.localScale = Vector3.zero;
                block.transform.localRotation = Quaternion.identity;
            }

            Destroy(flash);
        }

        // ── 금간 블록 파편용 다각형 스프라이트 캐시 ──
        private static Dictionary<int, Sprite> crackedPolygonCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// 길쭉한 다각형 스프라이트 생성 (삼각형/사각형/오각형)
        /// 32×32 텍스처에 꼭짓점을 배치하고 내부를 채움
        /// </summary>
        private static Sprite GetCrackedPolygonSprite(int sides)
        {
            if (crackedPolygonCache.TryGetValue(sides, out Sprite cached) && cached != null)
                return cached;

            int texSize = 32;
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.ARGB32, false);
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32[] pixels = new Color32[texSize * texSize];

            // 다각형 꼭짓점 (중심 기준, 약간 불규칙)
            Vector2[] verts = new Vector2[sides];
            float cx = texSize / 2f;
            float cy = texSize / 2f;
            float radius = texSize / 2f - 1f;
            float startAngle = -Mathf.PI / 2f;

            for (int i = 0; i < sides; i++)
            {
                float a = startAngle + 2f * Mathf.PI * i / sides;
                verts[i] = new Vector2(cx + radius * Mathf.Cos(a), cy + radius * Mathf.Sin(a));
            }

            // 래스터화: point-in-polygon 테스트
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    pixels[y * texSize + x] = PointInPolygon(x + 0.5f, y + 0.5f, verts) ? white : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.filterMode = FilterMode.Bilinear;
            tex.Apply();

            Sprite spr = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));
            crackedPolygonCache[sides] = spr;
            return spr;
        }

        /// <summary>
        /// 점이 다각형 내부에 있는지 판정 (레이캐스팅 알고리즘)
        /// </summary>
        private static bool PointInPolygon(float px, float py, Vector2[] polygon)
        {
            bool inside = false;
            int j = polygon.Length - 1;
            for (int i = 0; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > py) != (polygon[j].y > py) &&
                    px < (polygon[j].x - polygon[i].x) * (py - polygon[i].y) /
                         (polygon[j].y - polygon[i].y) + polygon[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// 금간 블록 파편 조각 — 길쭉한 삼각형/사각형/오각형 모양
        /// 크기 제각각, 살짝 터져 나오며 회전+중력+페이드
        /// </summary>
        private IEnumerator AnimateCrackedShard(Vector3 center, Color baseColor, int sides)
        {
            GameObject shard = new GameObject("CrackedShard");
            shard.transform.SetParent(transform, false);
            shard.transform.position = center;

            var image = shard.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.sprite = GetCrackedPolygonSprite(sides);

            RectTransform rt = shard.GetComponent<RectTransform>();

            // 크기 제각각 + 길쭉한 비율 (elongation 1.4~2.6)
            float baseSize = Random.Range(8f, 18f);
            float elongation = Random.Range(1.4f, 2.6f);
            float w, h;
            if (Random.value > 0.5f) { w = baseSize; h = baseSize * elongation; }
            else { w = baseSize * elongation; h = baseSize; }
            rt.sizeDelta = new Vector2(w, h);

            // 살짝 터져 나오는 산개 — 부드러운 속도
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(70f, 180f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            float gravity = Random.Range(120f, 280f);

            // 블록 색상 기반 밝기 변화
            float brightness = Random.Range(0.75f, 1.25f);
            Color shardColor = new Color(
                Mathf.Clamp01(baseColor.r * brightness),
                Mathf.Clamp01(baseColor.g * brightness),
                Mathf.Clamp01(baseColor.b * brightness),
                1f);
            image.color = shardColor;

            // 회전 (모든 조각 회전, 속도 제각각)
            float rotSpeed = Random.Range(-500f, 500f);
            float currentRot = Random.Range(0f, 360f);

            float lifetime = Random.Range(0.28f, 0.48f);
            float elapsedTime = 0f;

            while (elapsedTime < lifetime)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / lifetime;

                // 이동 (중력 적용 → 아래로 떨어짐)
                Vector3 pos = shard.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                velocity.y -= gravity * Time.deltaTime;
                velocity.x *= 0.97f;
                shard.transform.position = pos;

                // 회전
                currentRot += rotSpeed * Time.deltaTime;
                shard.transform.localRotation = Quaternion.Euler(0, 0, currentRot);

                // 축소 + 페이드
                float shrink = Mathf.Max(0.25f, 1f - t * 0.75f);
                rt.localScale = Vector3.one * shrink;
                image.color = new Color(shardColor.r, shardColor.g, shardColor.b, 1f - VisualConstants.EaseInQuad(t));

                yield return null;
            }

            Destroy(shard);
        }

        /// <summary>
        /// 파괴 순간 백색 플래시 오버레이
        /// </summary>
        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            GameObject flash = new GameObject("DestroyFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 60f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

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
        /// 매칭 블록 펄스 애니메이션 - 크기 1→1.08→1 + 백색 글로우
        /// </summary>
        private IEnumerator MatchPulse(HexBlock block, float delay)
        {
            if (block == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (block == null) yield break;

            // 백색 글로우 오버레이
            GameObject glow = new GameObject("MatchPulseGlow");
            glow.transform.SetParent(block.transform, false);
            glow.transform.localPosition = Vector3.zero;

            var glowImg = glow.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(1f, 1f, 1f, 0f);

            RectTransform glowRt = glow.GetComponent<RectTransform>();
            glowRt.sizeDelta = new Vector2(60f, 60f);

            float elapsed = 0f;
            float duration = VisualConstants.MatchPulseDuration;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutElastic 기반 스케일 펄스
                float pulseT = VisualConstants.EaseOutElastic(t);
                float scale = 1f + (VisualConstants.MatchPulseScale - 1f) * (1f - pulseT);
                block.transform.localScale = origScale * scale;

                // 글로우 알파: 전반부 페이드인, 후반부 페이드아웃
                float alpha = t < 0.3f
                    ? VisualConstants.MatchPulseOverlayAlpha * (t / 0.3f)
                    : VisualConstants.MatchPulseOverlayAlpha * (1f - (t - 0.3f) / 0.7f);
                if (glowImg != null)
                    glowImg.color = new Color(1f, 1f, 1f, alpha);

                yield return null;
            }

            if (block != null)
                block.transform.localScale = origScale;
            if (glow != null)
                Destroy(glow);
        }

        // ============================================================
        // ���� ó��
        // ============================================================

/// <summary>
        /// 낙하 후 pendingActivation 플래그가 있는 특수 블록을 수집하고 플래그 해제
        /// </summary>
        /// <summary>
        /// 낙하 → pending + 매칭 확인 → 처리 → 반복 (통합 연쇄 루프)
        /// 모든 매칭/특수블록 처리 후 호출하여 연쇄를 완전히 처리
        /// </summary>
private IEnumerator CascadeWithPendingLoop()
        {
            int maxIterations = 20;
            int iteration = 0;
            bool fatalError = false;
            currentCascadeDepth = 0;

            try
            {

            while (iteration < maxIterations && !fatalError)
            {
                iteration++;
                currentCascadeDepth = iteration - 1;
                lastProgressTime = Time.time; // 하트비트

                // 캐스케이드 콤보 사운드 (피치 상승)
                if (currentCascadeDepth > 0 && AudioManager.Instance != null)
                    AudioManager.Instance.PlayComboSound(currentCascadeDepth);

                // ★ 낙하 전 안전장치: 데이터가 비어있는 블록을 슬롯으로 강제 복원
                // (특수 블록 파괴 후 ClearData만 호출되고 위치가 복원되지 않은 블록 대비)
                SnapClearedBlocksToSlots();

                // 1. 낙하 처리
                yield return StartCoroutine(ProcessFalling());
                yield return new WaitForSeconds(cascadeDelay);

                // 2. pending 블록 수집
                List<HexBlock> cascadePending = CollectAndClearPendingSpecials();

                // 3. 매칭 확인
                List<MatchingSystem.MatchGroup> cascadeMatches = null;
                if (matchingSystem != null)
                {
                    var m = matchingSystem.FindMatches();
                    if (m != null && m.Count > 0) cascadeMatches = m;
                }

                // 4. 아무것도 없으면 연쇄 종료
                if (cascadePending.Count == 0 && cascadeMatches == null)
                {
                    Debug.Log($"[BRS] Cascade loop ended at iteration #{iteration} (nothing to process)");
                    break;
                }

                // 5. 매칭 있음 (pending과 함께 또는 단독) → 인라인 처리 후 루프 반복
                if (cascadeMatches != null)
                {
                    Debug.Log($"[BRS] Cascade #{iteration}: {(cascadePending.Count > 0 ? cascadePending.Count + " pending + " : "")}{cascadeMatches.Count} matches");
                    yield return StartCoroutine(ProcessMatchesInline(cascadeMatches, cascadePending.Count > 0 ? cascadePending : null));
                    continue;
                }

                // 6. pending만 있음 → 발동 후 루프 반복
                if (cascadePending.Count > 0)
                {
                    Debug.Log($"[BRS] Cascade #{iteration}: {cascadePending.Count} pending specials only");
                    // 유효한 블록만 필터링
                    List<HexBlock> validPending = new List<HexBlock>();
                    foreach (var sp in cascadePending)
                    {
                        if (sp != null && sp.Data != null && sp.gameObject != null && sp.Data.specialType != SpecialBlockType.None)
                            validPending.Add(sp);
                    }

                    if (validPending.Count == 0)
                    {
                        Debug.LogWarning("[BRS] All pending specials became invalid. Breaking cascade.");
                        break;
                    }

                    List<Coroutine> cos = new List<Coroutine>();
                    foreach (var sp in validPending)
                        cos.Add(StartCoroutine(ActivateSpecialAndWaitLocal(sp)));
                    foreach (var co in cos) yield return co;

                    // ★ pending 특수 블록 발동 후 즉시 낙하 — 블록 안착 후 다음 반복 진행
                    SnapClearedBlocksToSlots();
                    yield return StartCoroutine(ProcessFalling());
                    yield return new WaitForSeconds(cascadeDelay);
                    continue;
                }
            }

            if (iteration >= maxIterations)
                Debug.LogError($"[BRS] CascadeWithPendingLoop hit max iterations ({maxIterations})! Breaking.");

            } // end try
            finally
            {
                // === 예외 발생 여부와 관계없이 항상 실행되는 최종 정리 ===
                // 안전망: 루프 종료 후에도 남아있는 pending 블록의 깜빡임 정리
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block != null && block.Data != null && block.Data.pendingActivation)
                        {
                            Debug.LogWarning($"[BRS] Leftover pending block at {block.Coord} type={block.Data.specialType} — clearing pending state");
                            block.StopWarningBlink();
                            block.Data.pendingActivation = false;
                        }
                    }
                }
                currentCascadeDepth = 0;
                isProcessing = false;
                OnCascadeComplete?.Invoke();
                Debug.Log($"[BRS] CascadeWithPendingLoop completed. isProcessing=false");
            }
        }

private List<HexBlock> CollectAndClearPendingSpecials()
        {
            List<HexBlock> result = new List<HexBlock>();
            if (hexGrid == null) return result;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null &&
                    block.Data.pendingActivation &&
                    block.Data.specialType != SpecialBlockType.None)
                {
                    block.StopWarningBlink();
                    block.Data.pendingActivation = false;
                    result.Add(block);
                }
            }
            if (result.Count > 0)
                Debug.Log($"[BlockRemovalSystem] Collected {result.Count} pending specials for cascade");
            return result;
        }

        // ============================================================
        // 낙하 일시 중단 (드릴 쿠션 등 발사체 진행 중 낙하 방지)
        // ============================================================
        private bool isFallingSuspended = false;

        /// <summary>낙하 일시 중단 — 드릴 발사체 진행 중 호출</summary>
        public void SuspendFalling() { isFallingSuspended = true; }

        /// <summary>낙하 재개 — 모든 드릴 발사체 완료 후 호출</summary>
        public void ResumeFalling() { isFallingSuspended = false; }

        public bool IsFallingSuspended => isFallingSuspended;

private IEnumerator ProcessFalling()
        {
            if (hexGrid == null || columnCache == null) yield break;

            // 낙하 일시 중단 중이면 대기
            while (isFallingSuspended)
                yield return null;

            List<FallAnimation> existingAnimations = new List<FallAnimation>();
            List<FallAnimation> newBlockAnimations = new List<FallAnimation>();

            Dictionary<int, float> columnBaseDelay = new Dictionary<int, float>();
            foreach (var key in columnCache.Keys)
                columnBaseDelay[key] = Random.Range(0f, 0.04f);

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float colDelay = columnBaseDelay[kvp.Key];

                List<BlockData> dataList = new List<BlockData>();
                List<int> sourceSlots = new List<int>();

                // GravityWarper + Heavy 고블린 점유: 고정 슬롯 수집 (쉘 블록은 낙하 가능)
                HashSet<int> anchoredSlots = new HashSet<int>();
                HashSet<HexCoord> heavyOccupied = null;
                if (GoblinSystem.Instance != null)
                    heavyOccupied = GoblinSystem.Instance.GetHeavyOccupiedCoords();
                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    if (block != null && block.Data != null &&
                        block.Data.IsGravityAnchored())
                        anchoredSlots.Add(i);
                    // Heavy 고블린 점유 좌표: 블록이 실제로 존재할 때만 고정 (삭제된 빈 칸은 낙하 허용)
                    if (block != null && heavyOccupied != null && heavyOccupied.Contains(block.Coord)
                        && block.Data != null && block.Data.gemType != GemType.None)
                        anchoredSlots.Add(i);
                }

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    {
                        if (anchoredSlots.Contains(i))
                            continue; // GravityWarper 고정 — 낙하에서 제외
                        dataList.Add(block.Data.Clone());
                        sourceSlots.Add(i);
                    }
                }

                int emptyCount = column.Count - dataList.Count - anchoredSlots.Count;
                if (emptyCount == 0) continue;

                // ★ 먼저 모든 블록을 정확한 슬롯 위치로 강제 복원한 뒤 비주얼 숨김
                // (합체 애니메이션 등으로 anchoredPosition이 어긋난 경우 대비)
                // GravityWarper 고정 블록만 낙하에서 제외, 쉘 블록은 낙하 참여
                for (int i = 0; i < column.Count; i++)
                {
                    if (column[i] != null)
                    {
                        // GravityWarper 고정 블록은 비주얼과 위치를 유지
                        if (anchoredSlots.Contains(i))
                            continue;

                        column[i].HideVisuals();
                        // anchoredPosition을 슬롯 위치로 강제 설정
                        if (slotPositions.ContainsKey(column[i]))
                            SetBlockAnchoredPosition(column[i], slotPositions[column[i]]);
                        column[i].transform.localScale = Vector3.one;
                        column[i].transform.localRotation = Quaternion.identity;
                    }
                }

                // non-anchored 슬롯 인덱스 수집 (아래에서 위 순서)
                // GravityWarper 고정 블록이 중간에 있어도 정확하게 건너뛰어 데이터 덮어쓰기 방지
                List<int> freeSlots = new List<int>();
                for (int slot = 0; slot < column.Count; slot++)
                {
                    if (!anchoredSlots.Contains(slot))
                        freeSlots.Add(slot);
                }

                float maxExistingDelay = 0f;
                for (int i = 0; i < dataList.Count; i++)
                {
                    if (i >= freeSlots.Count) break;
                    int targetSlot = freeSlots[i];
                    int sourceSlot = sourceSlots[i];
                    HexBlock targetBlock = column[targetSlot];
                    if (targetBlock == null) continue;

                    if (sourceSlot != targetSlot)
                    {
                        // ★ 소스 슬롯의 캐시된 슬롯 위치 사용 (블록의 현재 위치가 아닌 원래 슬롯 위치)
                        Vector2 startPos = slotPositions.ContainsKey(column[sourceSlot]) ? slotPositions[column[sourceSlot]] : slotPositions[targetBlock];
                        SetBlockAnchoredPosition(targetBlock, startPos);
                    }

                    targetBlock.SetBlockData(dataList[i]);
                    targetBlock.transform.localScale = Vector3.one;

                    if (sourceSlot != targetSlot && slotPositions.ContainsKey(column[sourceSlot]))
                    {
                        Vector2 startPos = slotPositions[column[sourceSlot]];
                        int fallDistance = sourceSlot - targetSlot;
                        float heightDelay = fallDistance * 0.025f;
                        float jitter = Random.Range(0f, 0.02f);
                        float totalDelay = colDelay + heightDelay + jitter;

                        if (totalDelay > maxExistingDelay)
                            maxExistingDelay = totalDelay;

                        existingAnimations.Add(new FallAnimation
                        {
                            block = targetBlock,
                            startY = startPos.y,
                            targetY = slotPositions[targetBlock].y,
                            delay = totalDelay,
                            gravityMult = Random.Range(0.92f, 1.08f),
                            maxSpeedMult = Random.Range(0.90f, 1.10f),
                        });
                    }
                }

                float topY = slotPositions[column[column.Count - 1]].y;
                // 스폰 오프셋: 빈 셀 3줄 위에서 생성 (필드 맨위 + 4칸)
                float cellHeight = hexGrid != null ? hexGrid.HexSize * Mathf.Sqrt(3f) : 87f;
                float spawnOffset = cellHeight * 4f;
                float newBlockBaseDelay = maxExistingDelay + 0.08f;

                if (emptyCount > 0)
                    Debug.Log($"[BRS] ★ ProcessFalling 스폰: topY={topY:F1}, cellHeight={cellHeight:F1}, spawnOffset={spawnOffset:F1}, topY+offset={topY + spawnOffset:F1}");

                for (int i = 0; i < emptyCount; i++)
                {
                    // freeSlots에서 기존 블록 이후의 빈 슬롯 사용
                    int freeIdx = dataList.Count + i;
                    if (freeIdx >= freeSlots.Count) break;
                    int targetSlot = freeSlots[freeIdx];

                    HexBlock targetBlock = column[targetSlot];
                    if (targetBlock == null) continue;
                    Vector2 targetPos = slotPositions.ContainsKey(targetBlock) ? slotPositions[targetBlock] : Vector2.zero;

                    GemType randomGem = GemTypeHelper.GetRandom();
                    BlockData newData = new BlockData(randomGem);

                    float startY = topY + spawnOffset + (i * 80f);
                    SetBlockAnchoredPosition(targetBlock, new Vector2(targetPos.x, startY));
                    targetBlock.SetBlockData(newData);
                    targetBlock.transform.localScale = Vector3.one;

                    float newDelay = newBlockBaseDelay + i * 0.04f + Random.Range(0f, 0.025f);

                    newBlockAnimations.Add(new FallAnimation
                    {
                        block = targetBlock,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = newDelay,
                        gravityMult = Random.Range(0.90f, 1.10f),
                        maxSpeedMult = Random.Range(0.88f, 1.12f),
                        isNewBlock = true,
                    });
                }
            }

            int totalCount = existingAnimations.Count + newBlockAnimations.Count;
            if (totalCount == 0)
            {
                yield break;
            }

            // ★ 물리적 충돌 감지: 고블린 타겟 수집 → 각 FallAnimation에 주입
            Dictionary<int, List<(GoblinData goblin, float yPos)>> goblinTargets = null;
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                goblinTargets = GoblinSystem.Instance.GetGoblinCollisionTargets(hexGrid);

            if (goblinTargets != null && goblinTargets.Count > 0)
            {
                // 기존 블록 낙하: 시작Y가 고블린Y보다 높은 경우만 충돌 가능
                for (int i = 0; i < existingAnimations.Count; i++)
                {
                    var anim = existingAnimations[i];
                    int q = anim.block != null ? anim.block.Coord.q : int.MinValue;
                    if (goblinTargets.TryGetValue(q, out var targets))
                    {
                        anim.collisionTargets = targets.Where(t => anim.startY > t.yPos).ToList();
                        if (anim.collisionTargets.Count == 0) anim.collisionTargets = null;
                        existingAnimations[i] = anim;
                    }
                }
                // 새 블록 낙하: 그리드 위에서 스폰 → 대부분 고블린Y보다 높음
                for (int i = 0; i < newBlockAnimations.Count; i++)
                {
                    var anim = newBlockAnimations[i];
                    int q = anim.block != null ? anim.block.Coord.q : int.MinValue;
                    if (goblinTargets.TryGetValue(q, out var targets))
                    {
                        anim.collisionTargets = targets.Where(t => anim.startY > t.yPos).ToList();
                        if (anim.collisionTargets.Count == 0) anim.collisionTargets = null;
                        newBlockAnimations[i] = anim;
                    }
                }
            }

            int completedCount = 0;

            foreach (var anim in existingAnimations)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            foreach (var anim in newBlockAnimations)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            // 타임아웃 포함 대기 (최대 8초)
            float waitElapsed = 0f;
            while (completedCount < totalCount && waitElapsed < 8f)
            {
                waitElapsed += Time.deltaTime;
                yield return null;
            }

            if (completedCount < totalCount)
            {
                Debug.LogError($"[BRS] ProcessFalling timeout! {completedCount}/{totalCount} completed. Force finishing.");
            }

            // ★ 낙하 완료 후 모든 블록의 위치를 슬롯으로 강제 스냅 (위치 어긋남 방지)
            SnapAllBlocksToSlots();

            // ★ 물리적 충돌 대미지: 낙하 중 AnimateFall에서 실시간 적용됨
            // 사망 대기열에 있는 고블린들의 DeathAnimation 처리
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.HasPendingFallDeaths)
            {
                yield return StartCoroutine(GoblinSystem.Instance.ProcessPendingFallDeaths());
            }

            // ★ Heavy 고블린 낙하 체크: 점유 블록이 삭제되어 아래가 비었으면 자동 하강
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                ProcessHeavyGoblinFall();
            }
        }

        /// <summary>
        /// Heavy 고블린 낙하 처리: 점유 블록이 삭제되어 아래가 모두 빈 칸이면
        /// Heavy 전체(3블록)를 아래로 이동. 더 이상 내려갈 수 없을 때까지 반복.
        /// </summary>
        private void ProcessHeavyGoblinFall()
        {
            if (GoblinSystem.Instance == null || hexGrid == null) return;

            List<GoblinData> heavyGoblins = GoblinSystem.Instance.GetHeavyGoblins();
            if (heavyGoblins == null || heavyGoblins.Count == 0) return;

            foreach (var goblin in heavyGoblins)
            {
                if (goblin.occupiedCoords == null || goblin.occupiedCoords.Count == 0) continue;

                // 더 이상 내려갈 수 없을 때까지 반복
                bool moved = true;
                while (moved)
                {
                    moved = false;

                    // 각 점유 좌표의 아래(r+1) 위치 확인
                    bool canFall = true;
                    foreach (var coord in goblin.occupiedCoords)
                    {
                        HexCoord below = new HexCoord(coord.q, coord.r + 1);

                        // 아래 좌표가 자기 자신의 점유 좌표면 이동 가능
                        if (goblin.occupiedCoords.Contains(below))
                            continue;

                        // 그리드 유효 좌표인지 확인
                        if (!hexGrid.IsValidCoord(below))
                        {
                            canFall = false;
                            break;
                        }

                        // 해당 좌표의 블록이 빈 칸인지 확인
                        HexBlock blockBelow = hexGrid.GetBlock(below);
                        if (blockBelow == null || blockBelow.Data == null || blockBelow.Data.gemType != GemType.None)
                        {
                            canFall = false;
                            break;
                        }
                    }

                    if (canFall)
                    {
                        // occupiedCoords 전체를 r+1로 이동
                        HexCoord delta = new HexCoord(0, 1);
                        GoblinSystem.Instance.MoveHeavyOccupiedCoords(goblin, delta);
                        goblin.position = new HexCoord(goblin.position.q, goblin.position.r + 1);

                        // 비주얼 위치 갱신
                        Vector2 newCenter = GoblinSystem.Instance.CalculateHeavyCenterPosition(goblin.occupiedCoords);
                        if (goblin.visualObject != null)
                        {
                            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
                            if (rt != null)
                                rt.anchoredPosition = newCenter;
                        }

                        moved = true;
                    }
                }
            }
        }

        /// <summary>
        /// 단일 열 낙하+리필 처리 (드릴 등 발사체가 블록 제거 시 즉시 호출)
        /// 해당 열의 빈 슬롯을 감지하여 기존 블록 낙하 + 새 블록 스폰을 수행.
        /// skipGoblinCollision=true이면 낙하 중 고블린 충돌 대미지를 건너뜀 (드릴 발사체가 직접 처리)
        /// 반환된 Coroutine을 yield하면 낙하 완료까지 대기 가능
        /// </summary>
        public Coroutine ProcessFallingForColumn(HexBlock destroyedBlock, bool skipGoblinCollision = false)
        {
            if (destroyedBlock == null || hexGrid == null) return null;
            EnsureSlotsCached();
            if (columnCache == null) return null;

            // 파괴된 블록의 X 좌표로 열 키 결정
            if (!slotPositions.TryGetValue(destroyedBlock, out Vector2 blockPos)) return null;
            int colKey = Mathf.RoundToInt(blockPos.x);

            if (!columnCache.ContainsKey(colKey)) return null;

            return StartCoroutine(ProcessFallingForColumnCoroutine(colKey, skipGoblinCollision));
        }

        private IEnumerator ProcessFallingForColumnCoroutine(int colKey, bool skipGoblinCollision = false)
        {
            // 드릴 블록 삭제 후 0.1초 딜레이 뒤 낙하 시작
            yield return new WaitForSeconds(0.1f);

            if (!columnCache.ContainsKey(colKey)) yield break;

            List<HexBlock> column = columnCache[colKey];
            List<FallAnimation> existingAnims = new List<FallAnimation>();
            List<FallAnimation> newBlockAnims = new List<FallAnimation>();

            float colDelay = Random.Range(0f, 0.02f);

            List<BlockData> dataList = new List<BlockData>();
            List<int> sourceSlots = new List<int>();

            // 고정 슬롯 수집 (GravityWarper + Heavy 고블린 점유)
            HashSet<int> anchoredSlots = new HashSet<int>();
            HashSet<HexCoord> heavyOccupiedCol = null;
            if (GoblinSystem.Instance != null)
                heavyOccupiedCol = GoblinSystem.Instance.GetHeavyOccupiedCoords();
            for (int i = 0; i < column.Count; i++)
            {
                HexBlock block = column[i];
                if (block != null && block.Data != null &&
                    block.Data.IsGravityAnchored())
                    anchoredSlots.Add(i);
                // Heavy 고블린 점유 좌표: 블록이 실제로 존재할 때만 고정 (삭제된 빈 칸은 낙하 허용)
                if (block != null && heavyOccupiedCol != null && heavyOccupiedCol.Contains(block.Coord)
                    && block.Data != null && block.Data.gemType != GemType.None)
                    anchoredSlots.Add(i);
            }

            for (int i = 0; i < column.Count; i++)
            {
                HexBlock block = column[i];
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                {
                    if (anchoredSlots.Contains(i)) continue;
                    dataList.Add(block.Data.Clone());
                    sourceSlots.Add(i);
                }
            }

            int emptyCount = column.Count - dataList.Count - anchoredSlots.Count;
            if (emptyCount == 0) yield break;

            // 비주얼 숨김 (고정 블록 제외)
            for (int i = 0; i < column.Count; i++)
            {
                if (column[i] != null)
                {
                    if (anchoredSlots.Contains(i)) continue;
                    column[i].HideVisuals();
                    if (slotPositions.ContainsKey(column[i]))
                        SetBlockAnchoredPosition(column[i], slotPositions[column[i]]);
                    column[i].transform.localScale = Vector3.one;
                    column[i].transform.localRotation = Quaternion.identity;
                }
            }

            // freeSlots 수집
            List<int> freeSlots = new List<int>();
            for (int slot = 0; slot < column.Count; slot++)
            {
                if (!anchoredSlots.Contains(slot))
                    freeSlots.Add(slot);
            }

            // 기존 블록 재배치
            float maxExistingDelay = 0f;
            for (int i = 0; i < dataList.Count; i++)
            {
                if (i >= freeSlots.Count) break;
                int targetSlot = freeSlots[i];
                int sourceSlot = sourceSlots[i];
                HexBlock targetBlock = column[targetSlot];
                if (targetBlock == null) continue;

                if (sourceSlot != targetSlot)
                {
                    Vector2 startPos = slotPositions.ContainsKey(column[sourceSlot]) ? slotPositions[column[sourceSlot]] : slotPositions[targetBlock];
                    SetBlockAnchoredPosition(targetBlock, startPos);
                }

                targetBlock.SetBlockData(dataList[i]);
                targetBlock.transform.localScale = Vector3.one;

                if (sourceSlot != targetSlot && slotPositions.ContainsKey(column[sourceSlot]))
                {
                    Vector2 startPos = slotPositions[column[sourceSlot]];
                    int fallDistance = sourceSlot - targetSlot;
                    float heightDelay = fallDistance * 0.025f;
                    float totalDelay = colDelay + heightDelay + Random.Range(0f, 0.02f);
                    if (totalDelay > maxExistingDelay) maxExistingDelay = totalDelay;

                    existingAnims.Add(new FallAnimation
                    {
                        block = targetBlock,
                        startY = startPos.y,
                        targetY = slotPositions[targetBlock].y,
                        delay = totalDelay,
                        gravityMult = Random.Range(0.92f, 1.08f),
                        maxSpeedMult = Random.Range(0.90f, 1.10f),
                    });
                }
            }

            // 새 블록 스폰
            float topY = slotPositions[column[column.Count - 1]].y;
            float cellHeight = hexGrid != null ? hexGrid.HexSize * Mathf.Sqrt(3f) : 87f;
            float spawnOffset = cellHeight * 4f;
            float newBlockBaseDelay = maxExistingDelay + 0.08f;

            for (int i = 0; i < emptyCount; i++)
            {
                int freeIdx = dataList.Count + i;
                if (freeIdx >= freeSlots.Count) break;
                int targetSlot = freeSlots[freeIdx];
                HexBlock targetBlock = column[targetSlot];
                if (targetBlock == null) continue;
                Vector2 targetPos = slotPositions.ContainsKey(targetBlock) ? slotPositions[targetBlock] : Vector2.zero;

                GemType randomGem = GemTypeHelper.GetRandom();
                BlockData newData = new BlockData(randomGem);

                float startY = topY + spawnOffset + (i * 80f);
                SetBlockAnchoredPosition(targetBlock, new Vector2(targetPos.x, startY));
                targetBlock.SetBlockData(newData);
                targetBlock.transform.localScale = Vector3.one;

                float newDelay = newBlockBaseDelay + i * 0.04f + Random.Range(0f, 0.025f);
                newBlockAnims.Add(new FallAnimation
                {
                    block = targetBlock,
                    startY = startY,
                    targetY = targetPos.y,
                    delay = newDelay,
                    gravityMult = Random.Range(0.90f, 1.10f),
                    maxSpeedMult = Random.Range(0.88f, 1.12f),
                    isNewBlock = true,
                });
            }

            int totalCount = existingAnims.Count + newBlockAnims.Count;
            if (totalCount == 0) yield break;

            // ★ 물리적 충돌 감지: 고블린 타겟 수집 → 각 FallAnimation에 주입
            // skipGoblinCollision=true이면 건너뜀 (드릴 발사체가 직접 대미지 처리)
            if (!skipGoblinCollision)
            {
                Dictionary<int, List<(GoblinData goblin, float yPos)>> goblinTargets = null;
                if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                    goblinTargets = GoblinSystem.Instance.GetGoblinCollisionTargets(hexGrid);

                if (goblinTargets != null && goblinTargets.Count > 0)
                {
                    for (int i = 0; i < existingAnims.Count; i++)
                    {
                        var anim = existingAnims[i];
                        int q = anim.block != null ? anim.block.Coord.q : int.MinValue;
                        if (goblinTargets.TryGetValue(q, out var targets))
                        {
                            anim.collisionTargets = targets.Where(t => anim.startY > t.yPos).ToList();
                            if (anim.collisionTargets.Count == 0) anim.collisionTargets = null;
                            existingAnims[i] = anim;
                        }
                    }
                    for (int i = 0; i < newBlockAnims.Count; i++)
                    {
                        var anim = newBlockAnims[i];
                        int q = anim.block != null ? anim.block.Coord.q : int.MinValue;
                        if (goblinTargets.TryGetValue(q, out var targets))
                        {
                            anim.collisionTargets = targets.Where(t => anim.startY > t.yPos).ToList();
                            if (anim.collisionTargets.Count == 0) anim.collisionTargets = null;
                            newBlockAnims[i] = anim;
                        }
                    }
                }
            }

            int completedCount = 0;
            foreach (var anim in existingAnims)
                StartCoroutine(AnimateFall(anim, () => completedCount++));
            foreach (var anim in newBlockAnims)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            // 타임아웃 포함 대기
            float waitElapsed = 0f;
            while (completedCount < totalCount && waitElapsed < 8f)
            {
                waitElapsed += Time.deltaTime;
                yield return null;
            }

            // 열 내 블록 위치 스냅
            foreach (var block in column)
            {
                if (block != null && slotPositions.TryGetValue(block, out Vector2 slotPos))
                {
                    SetBlockAnchoredPosition(block, slotPos);
                    block.transform.localScale = Vector3.one;
                    block.transform.localRotation = Quaternion.identity;
                }
            }

            // ★ 낙하 충돌 사망 처리
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.HasPendingFallDeaths)
            {
                yield return StartCoroutine(GoblinSystem.Instance.ProcessPendingFallDeaths());
            }
        }

        /// <summary>
        /// 모든 블록을 캐시된 슬롯 위치로 강제 스냅 (낙하 완료 후 위치 어긋남 방지)
        /// </summary>
        private void SnapAllBlocksToSlots()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                if (slotPositions.TryGetValue(block, out Vector2 slotPos))
                {
                    // ★ 조건 없이 모든 블록을 슬롯 위치로 강제 스냅
                    // AnimateFall에서 첫 착지 시 이미 onComplete를 호출하므로
                    // 이 시점에서는 모든 블록이 슬롯에 있어야 함
                    SetBlockAnchoredPosition(block, slotPos);
                }
                // 스케일/로테이션도 보정
                block.transform.localScale = Vector3.one;
                block.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 데이터가 비어있는(ClearData 후) 블록만 슬롯으로 강제 복원
        /// 특수 블록 파괴 후 ClearData만 호출되고 위치가 복원되지 않은 블록 대비
        /// </summary>
        private void SnapClearedBlocksToSlots()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                if (block.Data == null || block.Data.gemType == GemType.None)
                {
                    if (slotPositions.TryGetValue(block, out Vector2 slotPos))
                    {
                        SetBlockAnchoredPosition(block, slotPos);
                        block.transform.localScale = Vector3.one;
                        block.transform.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        /// <summary>
        /// ���� ��� ���� �ִϸ��̼�
        /// ����� �̹� �����Ͱ� �����ǰ� ���� ��ġ�� ��ġ�� ����
        /// </summary>
private IEnumerator AnimateFall(FallAnimation anim, System.Action onComplete)
        {
            if (anim.block == null) { onComplete?.Invoke(); yield break; }

            if (anim.delay > 0)
                yield return new WaitForSeconds(anim.delay);

            HexBlock block = anim.block;

            // 딜레이 후 블록이 파괴되었을 수 있음
            if (block == null) { onComplete?.Invoke(); yield break; }

            if (!slotPositions.ContainsKey(block)) { onComplete?.Invoke(); yield break; }
            Vector2 slotPos = slotPositions[block];

            // 블록 스케일 초기화 (이전 애니메이션 영향 제거)
            if (block != null)
                block.transform.localScale = Vector3.one;

            float currentY = anim.startY;
            float velocity = 0f;
            float targetY = anim.targetY;
            float blockGravity = gravity * anim.gravityMult;
            float blockMaxSpeed = maxFallSpeed * anim.maxSpeedMult;

            int bounceCount = 0;
            int maxBounces = 2;
            float elapsed = 0f;
            float maxDuration = 5f; // 안전장치: 최대 5초
            bool completionInvoked = false; // ★ onComplete 중복 호출 방지

            // ★ 물리적 충돌 감지: 고블린 타겟이 있으면 프레임별 Y 통과 검사
            var collisionTargets = anim.collisionTargets;
            HashSet<GoblinData> hitGoblins = (collisionTargets != null && collisionTargets.Count > 0)
                ? new HashSet<GoblinData>() : null;

            while (elapsed < maxDuration)
            {
                // 블록이 중간에 파괴되면 즉시 완료 처리
                if (block == null)
                {
                    if (!completionInvoked) { completionInvoked = true; onComplete?.Invoke(); }
                    yield break;
                }

                elapsed += Time.deltaTime;
                float prevY = currentY; // 충돌 검사용 이전 Y
                velocity -= blockGravity * Time.deltaTime;
                velocity = Mathf.Max(velocity, -blockMaxSpeed);
                currentY += velocity * Time.deltaTime;

                // ★ 고블린 충돌 검사: prevY > goblinY >= currentY이면 통과
                if (hitGoblins != null && GoblinSystem.Instance != null)
                {
                    for (int ci = 0; ci < collisionTargets.Count; ci++)
                    {
                        var target = collisionTargets[ci];
                        if (!hitGoblins.Contains(target.goblin)
                            && target.goblin.isAlive
                            && prevY > target.yPos
                            && currentY <= target.yPos)
                        {
                            hitGoblins.Add(target.goblin);
                            GoblinSystem.Instance.ApplyIndividualFallDamage(target.goblin);
                        }
                    }
                }

                if (currentY <= targetY)
                {
                    currentY = targetY;

                    if (bounceCount < maxBounces && Mathf.Abs(velocity) > bounceThreshold)
                    {
                        velocity = -velocity * bounceRatio;
                        bounceCount++;

                        // ★ 첫 착지(첫 바운스) 시점에 블록을 슬롯에 확정 배치하고 onComplete 호출
                        // 바운스 시각 효과는 SquashEffect로만 표현 (위치는 이동하지 않음)
                        if (!completionInvoked)
                        {
                            SetBlockAnchoredPosition(block, slotPos);
                            completionInvoked = true;
                            onComplete?.Invoke();
                        }

                        if (block != null)
                        {
                            // 기존 SquashEffect 코루틴이 실행 중이면 중단 후 새로 시작
                            if (squashEffectCoroutines.ContainsKey(block))
                            {
                                StopCoroutine(squashEffectCoroutines[block]);
                                squashEffectCoroutines.Remove(block);
                            }

                            Coroutine squashCo = StartCoroutine(SquashEffect(block));
                            squashEffectCoroutines[block] = squashCo;

                            // 블록 착지 사운드
                            if (AudioManager.Instance != null)
                                AudioManager.Instance.PlayBlockLandSound();
                        }

                        // ★ 바운스로 Y좌표가 위로 올라가지 않도록 — 위치는 슬롯에 고정
                        // 바운스 물리 시뮬레이션은 계속하되 위치 업데이트는 하지 않음
                        // SquashEffect가 착지 느낌을 시각적으로 표현
                        while (elapsed < maxDuration)
                        {
                            if (block == null) yield break;
                            elapsed += Time.deltaTime;
                            velocity -= blockGravity * Time.deltaTime;
                            velocity = Mathf.Max(velocity, -blockMaxSpeed);
                            float simY = currentY + velocity * Time.deltaTime;

                            if (simY <= targetY)
                            {
                                // 다시 바닥에 닿음
                                if (Mathf.Abs(velocity) <= bounceThreshold || bounceCount >= maxBounces)
                                {
                                    // 바운스 종료
                                    break;
                                }
                                bounceCount++;
                                velocity = -velocity * bounceRatio;
                                currentY = targetY;

                                // 추가 바운스 스쿼시
                                if (block != null)
                                {
                                    if (squashEffectCoroutines.ContainsKey(block))
                                    {
                                        StopCoroutine(squashEffectCoroutines[block]);
                                        squashEffectCoroutines.Remove(block);
                                    }
                                    Coroutine squashCo2 = StartCoroutine(SquashEffect(block));
                                    squashEffectCoroutines[block] = squashCo2;
                                }
                            }
                            else
                            {
                                currentY = simY;
                            }
                            // ★ 위치는 항상 슬롯에 고정 (바운스 중에도 이동하지 않음)
                            yield return null;
                        }
                        // 바운스 완전 종료
                        if (block != null)
                        {
                            SetBlockAnchoredPosition(block, slotPos);
                            block.transform.localScale = Vector3.one;
                            if (anim.isNewBlock)
                                StartCoroutine(SpawnPopAnimation(block));
                        }
                        yield break;
                    }
                    else
                    {
                        // 바운스 없이 바로 착지
                        if (block != null)
                        {
                            SetBlockAnchoredPosition(block, slotPos);
                            block.transform.localScale = Vector3.one;
                            if (anim.isNewBlock)
                                StartCoroutine(SpawnPopAnimation(block));
                        }
                        if (!completionInvoked) { completionInvoked = true; onComplete?.Invoke(); }
                        yield break;
                    }
                }

                if (block != null)
                    SetBlockAnchoredPosition(block, new Vector2(slotPos.x, currentY));
                yield return null;
            }

            // 타임아웃: 강제 완료
            if (block != null)
            {
                SetBlockAnchoredPosition(block, slotPos);
                block.transform.localScale = Vector3.one;
            }
            Debug.LogWarning($"[BRS] AnimateFall timeout for block at slot Y={targetY}");
            if (!completionInvoked) { completionInvoked = true; onComplete?.Invoke(); }
        }

        private IEnumerator SquashEffect(HexBlock block)
        {
            if (block == null) yield break;

            // 안전장치: 시작 전 스케일 정규화 (이전 애니메이션 잔여 스케일 보정)
            if (block.transform.localScale != Vector3.one)
            {
                block.transform.localScale = Vector3.one;
            }

            float duration = 0.08f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (block == null)
                {
                    squashEffectCoroutines.Remove(block);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 스케일 계산 (최대 1.15배, 최소 0.9배)
                float scaleX = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                float scaleY = 1f - 0.1f * Mathf.Sin(t * Mathf.PI);

                // 안전장치: 스케일을 1.0 이상으로 설정하지 않도록 함
                // scaleX는 최대 1.15까지 증가할 수 있지만, 이는 의도된 동작
                // 중요한 것은 애니메이션 종료 후 1.0으로 리셋하는 것

                block.transform.localScale = new Vector3(scaleX, scaleY, 1f);
                yield return null;
            }

            // 애니메이션 후 반드시 1.0으로 리셋
            if (block != null)
            {
                block.transform.localScale = Vector3.one;
            }

            // 코루틴 추적에서 제거
            squashEffectCoroutines.Remove(block);
        }

        /// <summary>
        /// �� ����� ������ ���� �ִϸ��̼����� ä�� (���ڱ� ����� ���� ����)
        /// </summary>
private void FillEmptyBlocksWithAnimation()
        {
            if (hexGrid == null || columnCache == null) return;

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
                float colDelay = Random.Range(0f, 0.03f);
                int newBlockIndex = 0;

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];

                    // ★ 데이터가 있는 블록은 위치를 건드리지 않음 (낙하 중일 수 있음)
                    if (block.Data != null && block.Data.gemType != GemType.None)
                        continue;

                    RestoreBlockToSlot(block);

                    GemType randomGem = GemTypeHelper.GetRandom();
                    // GemTypeHelper.GetRandom()은 이미 Gray를 필터링함
                    block.SetBlockData(new BlockData(randomGem));
                    Debug.Log($"[BRS MoveRowsUp] 새 블록 생성: {block.Coord}, gemType={randomGem}({(int)randomGem})");
                    block.transform.localScale = Vector3.one;

                    Vector2 slotPos = slotPositions[block];
                    float spawnBase = hexGrid != null ? hexGrid.HexSize * Mathf.Sqrt(3f) * 4f : 347f;
                    float startY = topY + spawnBase + newBlockIndex * 80f;
                    SetBlockAnchoredPosition(block, new Vector2(slotPos.x, startY));

                    StartCoroutine(AnimateFall(new FallAnimation
                    {
                        block = block,
                        startY = startY,
                        targetY = slotPos.y,
                        delay = colDelay + newBlockIndex * 0.04f + Random.Range(0f, 0.02f),
                        gravityMult = Random.Range(0.90f, 1.10f),
                        maxSpeedMult = Random.Range(0.88f, 1.12f),
                    }, null));

                    newBlockIndex++;
                    Debug.LogWarning($"[BlockRemovalSystem] Force filled empty block at column {kvp.Key}, slot {i}");
                }
            }
        }

        // ============================================================
        // BigBang
        // ============================================================

        /// <summary>
        /// 낙하만 처리 (드릴 파괴 후 호출)
        /// </summary>
        /// <summary>
        /// 낙하만 처리 (매칭 체크 없음) - 외부에서 코루틴으로 직접 호출 가능
        /// 특수 블록과 동시 실행할 때 사용
        /// </summary>
public IEnumerator ProcessFallingCoroutinePublic()
        {
            if (isFalling)
            {
                Debug.LogWarning("[BlockRemovalSystem] ProcessFallingCoroutinePublic skipped - already falling");
                yield break;
            }
            isFalling = true;
            EnsureSlotsCached();
            yield return StartCoroutine(ProcessFalling());
            isFalling = false;
        }

        
public void TriggerFallOnly()
        {
            if (isProcessing) return;
            StartCoroutine(FallOnlyCoroutine());
        }

        private IEnumerator FallOnlyCoroutine()
        {
            isProcessing = true;
            lastProgressTime = Time.time;
            EnsureSlotsCached();

            yield return StartCoroutine(ProcessFalling());

            yield return new WaitForSeconds(cascadeDelay);

            // 낙하 후 연쇄 매칭 확인
            if (matchingSystem != null)
            {
                var newMatches = matchingSystem.FindMatches();
                if (newMatches.Count > 0)
                {
                    yield return StartCoroutine(ProcessMatchesCoroutine(newMatches));
                    yield break;
                }
            }

            isProcessing = false;
            OnCascadeComplete?.Invoke();
        }

        
        /// <summary>
        /// 게임 시작 연출: 모든 블록을 위에서 낙하시킨다 (데이터는 이미 세팅됨)
        /// </summary>
        public void TriggerStartDrop()
        {
            if (isProcessing) return;
            StartCoroutine(StartDropCoroutine());
        }

        private IEnumerator StartDropCoroutine()
        {
            isProcessing = true;
            lastProgressTime = Time.time;
            EnsureSlotsCached();

            if (hexGrid == null || columnCache == null) { isProcessing = false; yield break; }

            int completedCount = 0;
            int totalCount = 0;
            List<FallAnimation> anims = new List<FallAnimation>();

            Debug.Log($"[BRS] ★ StartDropCoroutine: 블록 스폰 위치 설정 시작");
            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
                float colDelay = Random.Range(0f, 0.06f);

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    Vector2 targetPos = slotPositions[block];

                    // 위치를 화면 위로 이동 — 빈 셀 3줄 위에서 생성 (필드 맨위 + 4칸)
                    float cellH = hexGrid != null ? hexGrid.HexSize * Mathf.Sqrt(3f) : 87f;
                    float startY = topY + cellH * 4f + i * 70f;
                    SetBlockAnchoredPosition(block, new Vector2(targetPos.x, startY));
                    block.transform.localScale = Vector3.one;
                    block.UpdateVisuals(); // 화면 위 위치에서 비주얼 활성화 (빈 공간에서 낙하 시작)

                    // 아래 블록(i=0)이 먼저 떨어지도록 딜레이: 위 블록일수록 딜레이가 큼
                    float heightDelay = i * 0.03f;

                    anims.Add(new FallAnimation
                    {
                        block = block,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = colDelay + heightDelay + Random.Range(0f, 0.02f),
                        gravityMult = Random.Range(0.88f, 1.12f),
                        maxSpeedMult = Random.Range(0.85f, 1.15f),
                    });
                    totalCount++;
                }
            }

            foreach (var anim in anims)
                StartCoroutine(AnimateFall(anim, () => completedCount++));
            float dropWaitTime = 0f;
            while (completedCount < totalCount && dropWaitTime < 10f)
            {
                dropWaitTime += Time.deltaTime;
                yield return null;
            }


            isProcessing = false;
            OnCascadeComplete?.Invoke();

        }

public void TriggerBigBang()
        {
            if (isProcessing) return;
            StartCoroutine(BigBangCoroutine());
        }

        private IEnumerator BigBangCoroutine()
        {
            isProcessing = true;
            lastProgressTime = Time.time;
            EnsureSlotsCached();
            OnBigBang?.Invoke();

            if (hexGrid == null) { isProcessing = false; yield break; }

            // 매칭 이펙트 비활성화
            // foreach (var block in hexGrid.GetAllBlocks())
            //     if (block.Data != null && block.Data.gemType != GemType.None)
            //         StartCoroutine(AnimateRemove(block));

            yield return new WaitForSeconds(0.02f);

            foreach (var block in hexGrid.GetAllBlocks())
            {
                block.ClearData();
                RestoreBlockToSlot(block);
            }

            int completedCount = 0;
            int totalCount = 0;
            List<FallAnimation> anims = new List<FallAnimation>();

            foreach (var kvp in columnCache)
            {
                List<HexBlock> column = kvp.Value;
                float topY = slotPositions[column[column.Count - 1]].y;
                float colDelay = Random.Range(0f, 0.06f);

                for (int i = 0; i < column.Count; i++)
                {
                    HexBlock block = column[i];
                    Vector2 targetPos = slotPositions[block];

                    GemType randomGem = GemTypeHelper.GetRandom();
                    // GemTypeHelper.GetRandom()은 이미 Gray를 필터링함
                    block.SetBlockData(new BlockData(randomGem));
                    Debug.Log($"[BRS FastSpawn] 새 블록 생성: {block.Coord}, gemType={randomGem}({(int)randomGem})");
                    block.transform.localScale = Vector3.one;

                    float bigBangCellH = hexGrid != null ? hexGrid.HexSize * Mathf.Sqrt(3f) : 87f;
                    float startY = topY + bigBangCellH * 4f + (column.Count - i) * 60f;
                    SetBlockAnchoredPosition(block, new Vector2(targetPos.x, startY));

                    float heightDelay = (column.Count - i) * 0.03f;

                    anims.Add(new FallAnimation
                    {
                        block = block,
                        startY = startY,
                        targetY = targetPos.y,
                        delay = colDelay + heightDelay + Random.Range(0f, 0.02f),
                        gravityMult = Random.Range(0.88f, 1.12f),
                        maxSpeedMult = Random.Range(0.85f, 1.15f),
                    });
                    totalCount++;
                }
            }

            foreach (var anim in anims)
                StartCoroutine(AnimateFall(anim, () => completedCount++));

            float bangWaitTime = 0f;
            while (completedCount < totalCount && bangWaitTime < 10f)
            {
                bangWaitTime += Time.deltaTime;
                yield return null;
            }

            isProcessing = false;
            OnCascadeComplete?.Invoke();
        }

        // ============================================================
        // Data
        // ============================================================

        private struct FallAnimation
        {
            public HexBlock block;
            public float startY;
            public float targetY;
            public float delay;
            public float gravityMult;
            public float maxSpeedMult;
            public bool isNewBlock;
            // 물리적 충돌 감지용: 이 블록이 낙하 중 확인할 고블린 타겟들
            public List<(GoblinData goblin, float yPos)> collisionTargets;
        }
    

/// <summary>
        /// Spawn pop-in animation for newly created blocks after fall.
        /// Scales from 0.5x -> 1.15x overshoot -> 1.0x with EaseOutBack.
        /// Includes a white glow overlay that fades out.
        /// </summary>
        private IEnumerator SpawnPopAnimation(HexBlock block)
        {
            if (block == null) yield break;

            float duration = VisualConstants.SpawnDuration;

            // Create glow overlay
            GameObject glow = new GameObject("SpawnGlow");
            glow.transform.SetParent(block.transform, false);
            glow.transform.localPosition = Vector3.zero;

            var glowImg = glow.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(1f, 1f, 1f, VisualConstants.SpawnGlowAlpha);

            RectTransform glowRt = glow.GetComponent<RectTransform>();
            glowRt.sizeDelta = new Vector2(50f, 50f);

            // 시작 스케일 저장
            Vector3 startScale = Vector3.one * VisualConstants.SpawnStartScale;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (block == null)
                {
                    if (glow != null) Destroy(glow);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutBack: 0.5 -> overshoot 1.15 -> settle 1.0
                float eased = VisualConstants.EaseOutBack(t);
                float scale = Mathf.Lerp(VisualConstants.SpawnStartScale, 1f, eased);
                block.transform.localScale = Vector3.one * scale;

                // Glow fade out
                if (glowImg != null)
                    glowImg.color = new Color(1f, 1f, 1f, VisualConstants.SpawnGlowAlpha * (1f - t));

                yield return null;
            }

            // 확실히 1.0으로 리셋
            if (block != null)
                block.transform.localScale = Vector3.one;
            if (glow != null)
                Destroy(glow);
        }

/// <summary>
        /// 매칭 처리 인라인 (isProcessing 관리 없음, cascade 호출 없음)
        /// highlight → 특수블록 생성 → 일반블록 삭제 → 기존 특수블록 발동
        /// pendingSpecials가 있으면 함께 동시 발동
        /// </summary>
        private IEnumerator ProcessMatchesInline(List<MatchingSystem.MatchGroup> matches, List<HexBlock> pendingSpecials)
        {
            // 매칭 그룹 수 기록 (동시 다색 매칭 보너스 계산용)
            currentMatchGroupCount = matches?.Count ?? 0;

            // ★ 디버그: 진입 로그
            lastProgressTime = Time.time; // 하트비트
            Debug.Log($"[BRS] ▶ ProcessMatchesInline 진입: matches={matches?.Count ?? 0}, pending={pendingSpecials?.Count ?? 0}");

            if (matches == null || matches.Count == 0)
            {
                // 매칭 없이 pending만 있는 경우: pending 발동만 처리
                if (pendingSpecials != null && pendingSpecials.Count > 0)
                {
                    List<Coroutine> pendingCos = new List<Coroutine>();
                    foreach (var sp in pendingSpecials)
                    {
                        if (sp != null && sp.Data != null && sp.Data.specialType != SpecialBlockType.None)
                        {
                            sp.SetMatched(false);
                            pendingCos.Add(StartCoroutine(ActivateSpecialAndWaitLocal(sp)));
                        }
                    }
                    foreach (var co in pendingCos) yield return co;
                }
                yield break;
            }

            // ★ 디버그: 각 매칭 그룹 상세
            for (int dbg = 0; dbg < matches.Count; dbg++)
            {
                var mg = matches[dbg];
                string coords = string.Join(", ", mg.blocks.Select(b => b != null ? $"({b.Coord.q},{b.Coord.r})" : "null"));
                Debug.Log($"[BRS] ▶ 입력 그룹[{dbg}]: color={mg.gemType}, count={mg.blocks.Count}, " +
                    $"specialType={mg.createdSpecialType}, spawnBlock={mg.specialSpawnBlock?.Coord}, blocks=[{coords}]");
            }

            // 0. 특수 블록 생성 그룹과 일반 매칭 그룹 분리
            // 특수 블록 생성 매칭(4개 이상)을 먼저 처리하고 0.5초 후 일반 매칭(3개) 처리
            List<MatchingSystem.MatchGroup> specialMatches = new List<MatchingSystem.MatchGroup>();
            List<MatchingSystem.MatchGroup> normalMatches = new List<MatchingSystem.MatchGroup>();

            foreach (var match in matches)
            {
                if (match.createdSpecialType != SpecialBlockType.None && match.specialSpawnBlock != null)
                    specialMatches.Add(match);
                else
                    normalMatches.Add(match);
            }

            Debug.Log($"[BRS] ▶ 분리 결과: specialMatches={specialMatches.Count}, normalMatches={normalMatches.Count}");

            // 특수 블록 생성 그룹이 있고 일반 그룹도 있으면 → 2단계로 분리 처리
            if (specialMatches.Count > 0 && normalMatches.Count > 0)
            {
                Debug.Log($"[BRS] ★★★ Phase A/B 분리 처리 시작: 특수={specialMatches.Count}그룹, 일반={normalMatches.Count}그룹");

                // Phase A: 특수 블록 생성 그룹 먼저 처리
                Debug.Log("[BRS] ★ Phase A 시작");
                yield return StartCoroutine(ProcessMatchesInline(specialMatches, pendingSpecials));
                lastProgressTime = Time.time; // 하트비트
                Debug.Log("[BRS] ★ Phase A 완료");

                // ★ 버그 수정: Phase A↔B 사이에 ProcessFalling을 실행하면
                // normalMatches에 저장된 HexBlock 참조의 Data가 컬럼 재배치로 변경되어
                // Phase B에서 일반 매칭이 처리되지 않는 버그가 발생.
                // ProcessFalling은 CascadeWithPendingLoop에서 자동 실행되므로 여기서 제거.
                // 제거된 블록의 비주얼만 슬롯으로 복귀시켜 Phase B 애니메이션과 겹치지 않도록 처리.
                SnapClearedBlocksToSlots();
                yield return new WaitForSeconds(cascadeDelay);

                // Phase B: 일반 매칭 그룹 처리 (pending은 이미 Phase A에서 처리했으므로 null)
                Debug.Log($"[BRS] ★★★ Phase B 시작: 일반 매칭 {normalMatches.Count}그룹 처리");
                yield return StartCoroutine(ProcessMatchesInline(normalMatches, null));
                lastProgressTime = Time.time; // 하트비트
                Debug.Log("[BRS] ★★★ Phase B 완료");
                yield break;
            }

            // 1. 매칭 하이라이트 + 펄스 애니메이션
            List<HexBlock> allMatchedBlocks = new List<HexBlock>();
            Vector3 matchCenter = Vector3.zero;
            int centerCount = 0;

            foreach (var match in matches)
            {
                foreach (var block in match.blocks)
                {
                    if (block != null)
                    {
                        block.SetMatched(true);
                        if (!allMatchedBlocks.Contains(block))
                        {
                            allMatchedBlocks.Add(block);
                            matchCenter += block.transform.position;
                            centerCount++;
                        }
                    }
                }
            }
            if (centerCount > 0) matchCenter /= centerCount;

            // 중심에서 가까운 순서대로 시차 펄스
            allMatchedBlocks.Sort((a, b) =>
            {
                float dA = Vector3.Distance(a.transform.position, matchCenter);
                float dB = Vector3.Distance(b.transform.position, matchCenter);
                return dA.CompareTo(dB);
            });
            // 매칭 감지 톤
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayMatchDetectTone();

            // 매칭 펄스 효과 비활성화 (중앙 파티클 표시 안 함)
            // for (int i = 0; i < allMatchedBlocks.Count; i++)
            // {
            //     StartCoroutine(MatchPulse(allMatchedBlocks[i], i * VisualConstants.MatchPulseStagger));
            // }

            yield return new WaitForSeconds(matchHighlightDuration);

            // [튜토리얼 Pause Hook 1] 매칭 하이라이트 완료 → 특수 블록 생성 전
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnMatchHighlightComplete();
                while (TutorialManager.Instance.IsPausedForTutorial)
                    yield return null;
            }

            // 2. 특수 블록 생성
            HashSet<HexBlock> newlyCreatedSpecials = new HashSet<HexBlock>();
            // ★ 합체 중 이미 제자리 삭제된 깨진 블록 (blocksToRemove에서 제외용)
            HashSet<HexBlock> alreadyRemovedCracked = new HashSet<HexBlock>();

            foreach (var match in matches)
            {
                Debug.Log($"[BRS] 매칭 처리: count={match.blocks.Count}, specialType={match.createdSpecialType}, spawnBlock={match.specialSpawnBlock?.Coord}");
                if (match.createdSpecialType != SpecialBlockType.None && match.specialSpawnBlock != null)
                {
                    Debug.Log($"[BRS] 특수 블록 생성 시작: {match.createdSpecialType} at ({match.specialSpawnBlock.Coord})");
                    // 특수 블록 합체 사운드
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySpecialGemSound();

                    // ★ 깨진 블록 혼합 매칭: 깨진 블록은 제자리 파쇄, 건강한 블록만 합체
                    List<HexBlock> mergeBlocks = match.blocks;
                    if (match.hasCrackedBlocks && !match.allBlocksCracked)
                    {
                        // 깨진 블록을 분리하여 즉시 붉은색 폭발 삭제
                        List<HexBlock> crackedBlocks = match.blocks
                            .Where(b => b != null && b.Data != null && b.Data.isCracked).ToList();
                        mergeBlocks = match.blocks
                            .Where(b => b != null && (b.Data == null || !b.Data.isCracked)).ToList();

                        // 깨진 블록: 제자리에서 붉은색 폭발 (합체에 참여하지 않음)
                        foreach (var crackedBlock in crackedBlocks)
                        {
                            StartCoroutine(AnimateCrackedBurst(crackedBlock));
                            alreadyRemovedCracked.Add(crackedBlock);
                            // ★ 블록 데이터 즉시 정리: ProcessFalling이 빈 슬롯으로 인식하도록
                            // AnimateCrackedBurst는 첫 프레임에서 색상을 캐싱하므로 안전
                            crackedBlock.ClearData();
                            crackedBlock.SetMatched(false);
                            RestoreBlockToSlot(crackedBlock);
                        }
                        Debug.Log($"[BRS] 깨진 블록 {crackedBlocks.Count}개 제자리 삭제+데이터 정리, 건강한 블록 {mergeBlocks.Count}개 합체 → {match.createdSpecialType} 생성");
                    }

                    yield return StartCoroutine(SpecialBlockMergeAnimation(
                        mergeBlocks, match.specialSpawnBlock, match.createdSpecialType,
                        match.drillDirection, match.gemType));
                    lastProgressTime = Time.time; // 하트비트
                    newlyCreatedSpecials.Add(match.specialSpawnBlock);

                    // 미션 시스템에 특수 블록 생성 알림 (타입 + 드릴방향)
                    OnSpecialBlockCreated?.Invoke(match.createdSpecialType, match.drillDirection);

                    // [튜토리얼 Pause Hook 2] 드릴 생성 완료 후
                    if (match.createdSpecialType == SpecialBlockType.Drill && TutorialManager.Instance != null)
                    {
                        TutorialManager.Instance.OnDrillCreated(match.specialSpawnBlock.Coord);
                        while (TutorialManager.Instance.IsPausedForTutorial)
                            yield return null;
                    }

                    // 생성 가산점
                    var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                    if (sm != null)
                        sm.AddCreationBonus(
                            match.createdSpecialType,
                            currentCascadeDepth,
                            match.specialSpawnBlock.transform.position);

                    // X블록 생성 시 기존 특수 블록과 즉시 합성
                    if (match.createdSpecialType == SpecialBlockType.XBlock &&
                        match.preExistingSpecialType != SpecialBlockType.None &&
                        comboSystem != null)
                    {
                        Debug.Log($"[BRS] X블록 즉시 합성: 기존={match.preExistingSpecialType} at ({match.specialSpawnBlock.Coord})");
                        match.specialSpawnBlock.ClearData();
                        newlyCreatedSpecials.Remove(match.specialSpawnBlock);
                        yield return StartCoroutine(comboSystem.ExecuteInPlaceCombo(
                            match.specialSpawnBlock.Coord,
                            match.specialSpawnBlock.transform.position,
                            match.gemType,
                            match.preExistingSpecialType,
                            match.preExistingDrillDirection));
                    }
                }
            }

            // 3. 블록 분류: 일반 블록 삭제, 기존 특수 블록 발동
            HashSet<HexBlock> blocksToRemove = new HashSet<HexBlock>();
            List<HexBlock> matchSpecialBlocks = new List<HexBlock>();

            foreach (var match in matches)
            {
                foreach (var block in match.blocks)
                {
                    if (block == null || block.Data == null) continue;

                    // ★ 합체 중 이미 제자리 삭제된 깨진 블록은 건너뛰기
                    if (alreadyRemovedCracked.Contains(block)) continue;

                    // 색상도둑 제거 이벤트 발동 + 파괴 이펙트 + 오디오
                    if (block.CurrentEnemyType == EnemyType.Chromophage)
                    {
                        OnEnemyRemoved?.Invoke(block, EnemyType.Chromophage);
                        // 색상도둑 파괴 애니메이션 시작 (회색 폭발 이펙트) - 비활성화됨
                        // StartCoroutine(AnimateGrayCrumble(block));
                        // 색상도둑 제거 SFX 재생
                        if (AudioManager.Instance != null)
                            AudioManager.Instance.PlayChromophageRemovalSound();
                        Debug.Log($"[BRS] 색상도둑 제거: ({block.Coord})");
                    }

                    if (block.Data.specialType != SpecialBlockType.None)
                    {
                        if (newlyCreatedSpecials.Contains(block)) continue;
                        if (!matchSpecialBlocks.Contains(block))
                            matchSpecialBlocks.Add(block);
                    }
                    else
                    {
                        blocksToRemove.Add(block);
                    }
                }
            }

            // 4. 제거될 블록의 평균 위치 계산 (점수 팝업용)
            Vector3 avgPosition = Vector3.zero;
            int posCount = 0;
            foreach (var block in blocksToRemove)
            {
                if (block != null)
                {
                    avgPosition += block.transform.position;
                    posCount++;
                }
            }
            if (posCount > 0) avgPosition /= posCount;

            // 5. 인접 회색(적군) 블록 수집 + 인접 쉘 블록 제거 + 인접 체인 해제
            //    삭제되는 블록 + 새로 생성된 특수 블록 모두의 인접을 확인
            HashSet<HexBlock> grayToRemove = new HashSet<HexBlock>();
            HashSet<HexBlock> shellToRemove = new HashSet<HexBlock>();
            HashSet<HexBlock> allAffected = new HashSet<HexBlock>(blocksToRemove);
            foreach (var sp in newlyCreatedSpecials)
                allAffected.Add(sp);
            // ★ 이미 제거된 깨진 블록도 인접 검사에 포함 (인접 회색 블록 제거용)
            foreach (var cb in alreadyRemovedCracked)
            {
                if (cb != null) allAffected.Add(cb);
            }

            // 인접 대미지 면 라인 이펙트용: (대미지 받는 블록, 대미지 준 블록 좌표) 기록
            List<(HexBlock damaged, HexCoord sourceCoord)> adjacentDamageEdges = new List<(HexBlock, HexCoord)>();

            foreach (var block in allAffected)
            {
                if (block == null) continue;
                foreach (var neighbor in hexGrid.GetNeighbors(block.Coord))
                {
                    if (neighbor == null || neighbor.Data == null) continue;
                    if (neighbor.Data.gemType == GemType.Gray && !allAffected.Contains(neighbor))
                        grayToRemove.Add(neighbor);
                    // 쉘 블록(크랙 회색): 인접 매칭으로 제거
                    if (neighbor.Data.isShell && !allAffected.Contains(neighbor))
                    {
                        shellToRemove.Add(neighbor);
                        adjacentDamageEdges.Add((neighbor, block.Coord));
                    }
                    if (neighbor.Data.hasChain)
                    {
                        neighbor.RemoveChain();
                        neighbor.Data.enemyType = EnemyType.None;
                        Debug.Log($"[BRS] 체인 해제: ({neighbor.Coord}) - 인접 블록 매칭으로 해제");
                        if (EnemySystem.Instance != null)
                            EnemySystem.Instance.RegisterKill(new EnemyKillData {
                                enemyType = EnemyType.ChainAnchor, method = RemovalMethod.Match, condition = RemovalCondition.ChainBroken });
                        var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                        if (sm != null)
                            sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.Match,
                                RemovalCondition.ChainBroken, neighbor.transform.position);
                    }
                    if (neighbor.Data.hasThorn && !allAffected.Contains(neighbor))
                    {
                        // 가시 기생충: 인접 매칭 시 제거 (점수 0점)
                        neighbor.Data.hasThorn = false;
                        neighbor.Data.enemyType = EnemyType.None;
                        neighbor.UpdateVisuals();
                        Debug.Log($"[BRS] 가시 기생충 제거: ({neighbor.Coord})");
                        if (EnemySystem.Instance != null)
                            EnemySystem.Instance.RegisterKill(new EnemyKillData {
                                enemyType = EnemyType.ThornParasite, method = RemovalMethod.Match, condition = RemovalCondition.Normal });
                    }
                }
            }

            // 5b. ChaosOverlord 생존 체크 (3회 공격 필요)
            if (EnemySystem.Instance != null)
            {
                List<HexBlock> survived = new List<HexBlock>();
                foreach (var block in blocksToRemove)
                {
                    if (block != null && block.Data != null && block.Data.enemyType == EnemyType.ChaosOverlord)
                    {
                        if (EnemySystem.Instance.ProcessChaosHit(block, RemovalMethod.Match))
                            survived.Add(block); // 아직 살아있음
                    }
                }
                foreach (var s in survived)
                    blocksToRemove.Remove(s);
            }

            // 5c. 보석 날아가기 연출 (데이터 클리어 전에 발생) - 비활성화됨
            // foreach (var match in matches)
            // {
            //     foreach (var block in match.blocks)
            //     {
            //         if (block != null && blocksToRemove.Contains(block) &&
            //             match.gemType != GemType.None && match.gemType != GemType.Gray)
            //         {
            //             OnGemCollectedVisual?.Invoke(block.transform.position, match.gemType);
            //         }
            //     }
            // }

            // 5d. 미션 시스템 보고: 각 기본 블록 파괴 시 개별 보고 (Stage/Infinite 모두 지원)
            // ClearData() 전에 호출해야 gemType이 유효함
            // 깨진 블록(isCracked) 및 껍데기 블록(isShell)은 미션 카운트에서 제외
            // + 아이템 게이지 충전
            Dictionary<GemType, int> gemCountsForGauge = new Dictionary<GemType, int>();
            foreach (var block in blocksToRemove)
            {
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                {
                    bool isBroken = block.Data.isCracked || block.Data.isShell;

                    if (!isBroken)
                    {
                        // 일반 블록: 미션 보고 + 게이지 +2 (OnSingleGemDestroyedForMission 내부에서)
                        GameManager.Instance?.OnSingleGemDestroyedForMission(block.Data.gemType);

                        GemType gt = block.Data.gemType;
                        if (gemCountsForGauge.ContainsKey(gt))
                            gemCountsForGauge[gt]++;
                        else
                            gemCountsForGauge[gt] = 1;
                    }
                    else
                    {
                        // 깨진 블록: 미션 제외, 게이지 +1 (일반의 절반)
                        GemType gt = block.Data.gemType;
                        if (gt == GemType.Red && JewelsHexaPuzzle.Items.HammerGauge.Instance != null)
                            JewelsHexaPuzzle.Items.HammerGauge.Instance.AddGauge(1);
                        if (gt == GemType.Green && JewelsHexaPuzzle.Items.SwapGauge.Instance != null)
                            JewelsHexaPuzzle.Items.SwapGauge.Instance.AddGauge(1);
                        if (gt == GemType.Purple && JewelsHexaPuzzle.Items.LineGauge.Instance != null)
                            JewelsHexaPuzzle.Items.LineGauge.Instance.AddGauge(1);
                    }
                }
            }

            // 아이템 게이지 일괄 충전 (기존 ItemManager + 새 ItemGaugeController 동시)
            if (ItemManager.Instance != null)
            {
                foreach (var kvp in gemCountsForGauge)
                    ItemManager.Instance.AddGauge(kvp.Key, kvp.Value);
            }

            // ★ 새 독립 게이지 컨트롤러 연동
            if (JewelsHexaPuzzle.UI.ItemGaugeController.Instance != null)
            {
                foreach (var kvp in gemCountsForGauge)
                {
                    for (int gi = 0; gi < kvp.Value; gi++)
                        JewelsHexaPuzzle.UI.ItemGaugeController.Instance.OnBlockRemoved(kvp.Key);
                }
            }

            // ★ 게이지 충전: 일반 블록 ×2, 깨진 블록 ×1
            int normalBlockCount = 0;
            int crackedBlockCount = 0;
            foreach (var block in blocksToRemove)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.isCracked || block.Data.isShell) crackedBlockCount++;
                else normalBlockCount++;
            }
            int totalGaugePerColor = normalBlockCount * 2 + crackedBlockCount * 1;

            if (JewelsHexaPuzzle.Items.HammerGauge.Instance != null)
            {
                int redCount = gemCountsForGauge.ContainsKey(GemType.Red) ? gemCountsForGauge[GemType.Red] : 0;
                if (redCount > 0)
                    JewelsHexaPuzzle.Items.HammerGauge.Instance.AddGauge(redCount * 2);
            }

            if (JewelsHexaPuzzle.Items.SwapGauge.Instance != null)
            {
                int greenCount = gemCountsForGauge.ContainsKey(GemType.Green) ? gemCountsForGauge[GemType.Green] : 0;
                if (greenCount > 0)
                    JewelsHexaPuzzle.Items.SwapGauge.Instance.AddGauge(greenCount * 2);
            }

            if (JewelsHexaPuzzle.Items.LineGauge.Instance != null)
            {
                int purpleCount = gemCountsForGauge.ContainsKey(GemType.Purple) ? gemCountsForGauge[GemType.Purple] : 0;
                if (purpleCount > 0)
                    JewelsHexaPuzzle.Items.LineGauge.Instance.AddGauge(purpleCount * 2);
            }

            // 5e. 모든 블록이 깨져서 특수 블록 생성이 차단된 경우 메시지 표시
            foreach (var match in matches)
            {
                if (match.allBlocksCracked && match.crackedBlockedSpecial != SpecialBlockType.None)
                {
                    string specialName = match.crackedBlockedSpecial switch
                    {
                        SpecialBlockType.Drill => "드릴",
                        SpecialBlockType.Bomb => "폭탄",
                        SpecialBlockType.Rainbow => "레인보우",
                        SpecialBlockType.XBlock => "타겟",
                        SpecialBlockType.Drone => "드론",
                        _ => "특수 블록"
                    };

                    GameManager.Instance?.ShowFloatingMessage(
                        $"모든 블록이 깨져 {specialName} 생성에 실패 하였습니다");
                    Debug.Log($"[BRS] 모든 블록 깨짐 → {match.crackedBlockedSpecial} 생성 완전 차단");
                }
            }

            // 5f. 매칭 데미지 — 직격 + 인접 (블록 타입별 분리 처리)
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                // 모든 제거 대상 블록을 하나로 합침 (blocksToRemove + alreadyRemovedCracked + 합체 블록)
                var allRemovedBlocks = new List<HexBlock>(blocksToRemove);
                foreach (var cb in alreadyRemovedCracked)
                {
                    if (cb != null && !allRemovedBlocks.Contains(cb))
                        allRemovedBlocks.Add(cb);
                }
                foreach (var match in matches)
                {
                    if (match.createdSpecialType == SpecialBlockType.None) continue;
                    foreach (var block in match.blocks)
                    {
                        if (block != null && !allRemovedBlocks.Contains(block))
                            allRemovedBlocks.Add(block);
                    }
                }

                // 좌표별 데미지 누적 (같은 몬스터가 여러 블록에 인접하면 각 1씩 누적)
                Dictionary<HexCoord, int> damageMap = new Dictionary<HexCoord, int>();
                // 방패 내구도 감소 좌표 (일반 블록 직격만 해당)
                HashSet<HexCoord> shieldDamageCoords = new HashSet<HexCoord>();

                foreach (var block in allRemovedBlocks)
                {
                    if (block == null || block.Data == null) continue;
                    HexCoord blockCoord = block.Coord;
                    bool isCracked = block.Data.isCracked || block.Data.isShell;

                    // === 규칙 1: 직격 — 매칭된 블록 위치에 몬스터가 있으면 ===
                    // 일반 블록: 본체 1 + 방패 내구도 1
                    // 깨진 블록: 본체 1 + 방패 내구도 감소 없음
                    if (damageMap.ContainsKey(blockCoord))
                        damageMap[blockCoord] += 1;
                    else
                        damageMap[blockCoord] = 1;

                    if (!isCracked)
                        shieldDamageCoords.Add(blockCoord); // 일반 블록 직격만 방패 내구도 감소

                    // === 규칙 2: 인접 — 일반 블록만 인접 6방향 데미지 ===
                    if (!isCracked)
                    {
                        foreach (var neighbor in blockCoord.GetAllNeighbors())
                        {
                            if (damageMap.ContainsKey(neighbor))
                                damageMap[neighbor] += 1;
                            else
                                damageMap[neighbor] = 1;
                            // 인접 데미지는 방패 내구도 감소 없음 (shieldDamageCoords에 추가 안 함)
                        }
                    }
                    // 깨진 블록: 인접 데미지 없음 (블록 제거만 처리)
                }

                // === 데미지 적용 ===
                foreach (var kvp in damageMap)
                {
                    HexCoord coord = kvp.Key;
                    int totalDmg = kvp.Value;

                    var goblin = GoblinSystem.Instance.GetGoblinAt(coord);
                    if (goblin == null) continue;

                    // 방패 고블린: 방패 내구도가 모두 깎이기 전까지 본체 HP 보호
                    if (goblin.isShielded)
                    {
                        // 일반 블록 직격 위치면 방패 내구도 1 감소
                        if (shieldDamageCoords.Contains(coord))
                            GoblinSystem.Instance.ApplyShieldDamagePublic(coord, 1);
                        // 방패 활성 중에는 본체 데미지 차단
                        continue;
                    }

                    // 일반 몬스터 / 방패 파괴된 고블린: 본체에 데미지
                    GoblinSystem.Instance.ApplyDamageAtPosition(coord, totalDmg);
                }
            }

            // 6. 삭제 애니메이션 (축소하며 사라짐) + 파괴 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBlockDestroySound();

            // 모든 블록의 삭제 애니메이션을 병렬 시작 (깨진 블록은 산산조각 파괴)
            List<Coroutine> shrinkCoroutines = new List<Coroutine>();
            foreach (var block in blocksToRemove)
            {
                if (block != null)
                {
                    // 금간 블록: block.Data.isCracked로 직접 판별 (hasCrackedBlocks 의존 제거)
                    if (block.Data != null && block.Data.isCracked
                        && !alreadyRemovedCracked.Contains(block))
                        shrinkCoroutines.Add(StartCoroutine(AnimateCrackedBurst(block)));
                    else
                        shrinkCoroutines.Add(StartCoroutine(AnimateShrinkRemove(block)));
                }
            }

            // 모든 삭제 애니메이션 완료 대기
            foreach (var co in shrinkCoroutines)
                yield return co;
            lastProgressTime = Time.time; // 하트비트: 삭제 애니메이션 완료

            // 7. 일반 블록 데이터 클리어
            foreach (var block in blocksToRemove)
            {
                if (block != null)
                {
                    block.ClearData();
                    block.SetMatched(false);
                    RestoreBlockToSlot(block);
                    block.transform.localScale = Vector3.one;
                }
            }

            // 매칭 점수 이벤트 발동 (ScoreManager가 처리)
            OnBlocksRemoved?.Invoke(blocksToRemove.Count, currentCascadeDepth, avgPosition);

            // (미션 카운팅은 5d에서 OnSingleGemDestroyedForMission으로 처리됨)

            // 7b. Divider 분열 처리 (매칭 제거 시)
            if (EnemySystem.Instance != null)
            {
                List<HexBlock> removedDividers = new List<HexBlock>();
                foreach (var block in blocksToRemove)
                {
                    if (block != null && block.Data != null && block.Data.enemyType == EnemyType.Divider)
                        removedDividers.Add(block);
                }
                if (removedDividers.Count > 0)
                    EnemySystem.Instance.ProcessDividerSplits(removedDividers, RemovalMethod.Match);
            }

            // 8. 인접 회색 블록 딜레이 제거 (0.3초 후 균열 이펙트)
            if (grayToRemove.Count > 0)
            {
                yield return new WaitForSeconds(0.3f);

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayBlockDestroySound();

                foreach (var gray in grayToRemove)
                {
                    // 회색 블록 폭발 이펙트 비활성화
                    // StartCoroutine(AnimateGrayCrumble(gray));
                    // 회색 블록(색상도둑) 매칭 제거 점수
                    var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                    if (sm != null)
                        sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.Match,
                            RemovalCondition.Normal, gray.transform.position);
                }

                yield return new WaitForSeconds(VisualConstants.DestroyDuration + 0.05f);

                foreach (var gray in grayToRemove)
                {
                    if (gray != null)
                    {
                        gray.ClearData();
                        gray.SetMatched(false);
                        RestoreBlockToSlot(gray);
                        gray.transform.localScale = Vector3.one;
                    }
                }

                // 회색 블록(적군) 제거 점수 이벤트 발동
                OnBlocksRemoved?.Invoke(grayToRemove.Count, currentCascadeDepth, avgPosition);
            }

            // 8b. 인접 쉘 블록(크랙 회색) 제거 + 면 라인 이펙트
            if (shellToRemove.Count > 0)
            {
                // 면 라인 이펙트 표시: 대미지 받은 면에 붉은색 라인 (0.5초 페이드아웃)
                foreach (var edge in adjacentDamageEdges)
                {
                    if (edge.damaged != null)
                        StartCoroutine(ShowAdjacentDamageEdgeLine(edge.damaged.Coord, edge.sourceCoord));
                }

                foreach (var shell in shellToRemove)
                {
                    if (shell != null && shell.Data != null)
                    {
                        shell.ClearData();
                        shell.SetMatched(false);
                        RestoreBlockToSlot(shell);
                        shell.transform.localScale = Vector3.one;
                    }
                }
                Debug.Log($"[BRS] 인접 매칭으로 쉘 블록 {shellToRemove.Count}개 제거");
            }

            // 6. 매칭 특수블록 발동 (매칭에서 직접 생긴 특수 블록만)
            List<HexBlock> matchSpecialsToActivate = new List<HexBlock>();
            foreach (var sp in matchSpecialBlocks)
            {
                if (sp != null && sp.Data != null)
                {
                    sp.SetMatched(false);
                    matchSpecialsToActivate.Add(sp);
                }
            }

            if (matchSpecialsToActivate.Count > 0)
            {
                Debug.Log($"[BRS] Activating {matchSpecialsToActivate.Count} match specials");
                List<Coroutine> activationCoroutines = new List<Coroutine>();
                foreach (var specialBlock in matchSpecialsToActivate)
                {
                    if (specialBlock == null || specialBlock.Data == null) continue;
                    Debug.Log($"[BRS] Activate match special: {specialBlock.Data.specialType} at {specialBlock.Coord}");
                    activationCoroutines.Add(StartCoroutine(ActivateSpecialAndWaitLocal(specialBlock)));
                }
                foreach (var co in activationCoroutines)
                    yield return co;
                lastProgressTime = Time.time; // 하트비트: 특수 블록 발동 완료

                // 매칭 특수 블록 발동 후 낙하 처리
                SnapClearedBlocksToSlots();
                yield return StartCoroutine(ProcessFalling());
                yield return new WaitForSeconds(cascadeDelay);
            }

            // 7. pending 특수블록 발동 (낙하 완료 후 실행)
            if (pendingSpecials != null && pendingSpecials.Count > 0)
            {
                List<HexBlock> validPending = new List<HexBlock>();
                foreach (var sp in pendingSpecials)
                {
                    if (sp != null && sp.Data != null
                        && sp.Data.specialType != SpecialBlockType.None
                        && !matchSpecialsToActivate.Contains(sp))
                    {
                        validPending.Add(sp);
                    }
                }

                if (validPending.Count > 0)
                {
                    Debug.Log($"[BRS] Activating {validPending.Count} pending specials (after fall)");
                    List<Coroutine> pendingCoroutines = new List<Coroutine>();
                    foreach (var sp in validPending)
                    {
                        if (sp == null || sp.Data == null) continue;
                        Debug.Log($"[BRS] Activate pending: {sp.Data.specialType} at {sp.Coord}");
                        sp.SetMatched(false);
                        pendingCoroutines.Add(StartCoroutine(ActivateSpecialAndWaitLocal(sp)));
                    }
                    foreach (var co in pendingCoroutines)
                        yield return co;

                    // pending 특수 블록 발동 후 낙하 처리
                    SnapClearedBlocksToSlots();
                    yield return StartCoroutine(ProcessFalling());
                    yield return new WaitForSeconds(cascadeDelay);
                }
            }
        }
}
}
// ============================================================================
// DrillBlockSystem.cs — 드릴 특수 블록 시스템
// ============================================================================
//
// [개요]
//   드릴은 4개 블록이 일직선으로 매칭되었을 때 생성되는 특수 블록입니다.
//   발동하면 마치 총알처럼 투사체가 양방향으로 발사되어,
//   지나가는 경로 위의 블록들을 모두 파괴합니다.
//
// [동작 흐름]
//   1. 4매칭 직선 감지 → CreateDrillBlock()으로 드릴 블록 생성
//   2. 드릴 블록이 매칭에 포함되면 → ActivateDrill()로 발동
//   3. 발동 시:
//      - "쿵!" 하는 압축 애니메이션 (Pre-Fire Compression)
//      - 중앙에서 폭발 이펙트 + 양방향으로 투사체 발사
//      - 투사체가 지나가는 블록마다 파편과 충격파를 남기며 파괴
//      - 경로에 다른 특수 블록이 있으면 연쇄 발동 예약
//   4. 모든 파괴 완료 → 점수 계산 → 미션 시스템에 결과 통보
//
// [방향 종류]
//   - Vertical: 위아래 세로 방향 (|)
//   - Slash: 오른쪽 위~왼쪽 아래 대각선 (/)
//   - BackSlash: 오른쪽 아래~왼쪽 위 대각선 (\)
//
// [비유]
//   볼링에서 공이 일직선으로 핀을 쓰러뜨리듯,
//   드릴 투사체가 한 줄의 블록들을 관통하며 모두 부순다고 생각하면 됩니다.
// ============================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 드릴 특수 블록의 생성, 발동, 시각 효과를 모두 담당하는 시스템 클래스.
    /// 투사체가 한 방향으로 날아가며 경로 위의 블록들을 차례로 파괴합니다.
    /// </summary>
    public class DrillBlockSystem : MonoBehaviour
    {
        // ============================================================
        // 인스펙터에서 설정하는 참조 및 설정값
        // ============================================================

        [Header("References")]
        /// <summary>
        /// 육각형 그리드 전체를 관리하는 HexGrid 참조.
        /// 블록 위치, 좌표 검증, 이웃 탐색 등에 사용됩니다.
        /// </summary>
        [SerializeField] private HexGrid hexGrid;

        /// <summary>
        /// 블록 제거(캐스케이드) 시스템 참조.
        /// 현재 캐스케이드 깊이(연쇄 단계)에 따라 이펙트 강도를 조절할 때 사용합니다.
        /// </summary>
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("Drill Settings")]
        /// <summary>
        /// 블록 하나를 파괴하고 다음 블록으로 넘어가기까지의 대기 시간(초).
        /// 값이 작을수록 드릴이 빠르게 관통합니다. (기본 0.06초)
        /// </summary>
        [SerializeField] private float drillSpeed = 0.06f;

        /// <summary>
        /// 투사체(드릴 총알)의 이동 속도(픽셀/초).
        /// 값이 클수록 투사체가 빠르게 날아갑니다. (기본 1200)
        /// </summary>
        [SerializeField] private float projectileSpeed = 1200f;

        // ============================================================
        // 이벤트 및 상태 추적 변수
        // ============================================================

        /// <summary>
        /// 드릴 발동이 완료될 때 호출되는 이벤트.
        /// int 매개변수는 이번 드릴로 획득한 총 점수입니다.
        /// BlockRemovalSystem이 이 이벤트를 구독하여 점수를 반영합니다.
        /// </summary>
        public event System.Action<int> OnDrillComplete;

        /// <summary>
        /// 현재 동시에 발동 중인 드릴의 개수.
        /// 여러 드릴이 동시에 터질 수 있으므로 카운터로 관리합니다.
        /// </summary>
        private int activeDrillCount = 0;

        /// <summary>
        /// 드릴이 경로 위에서 발견한 "다른 특수 블록" 목록.
        /// 드릴은 특수 블록을 직접 파괴하지 않고, 이 목록에 추가해두면
        /// 나중에 BlockRemovalSystem이 연쇄 발동시킵니다.
        /// (비유: "나중에 터뜨려야 할 폭탄 목록"을 메모해두는 것)
        /// </summary>
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        /// <summary>
        /// 현재 발동 중(애니메이션 진행 중)인 드릴 블록들의 집합.
        /// 발동 중인 블록은 다른 시스템이 건드리지 못하도록 보호합니다.
        /// </summary>
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        // ============================================================
        // 외부에서 읽을 수 있는 상태 프로퍼티
        // ============================================================

        /// <summary>
        /// 지금 드릴이 발동 중인지 여부. true이면 하나 이상의 드릴이 날아가는 중입니다.
        /// </summary>
        public bool IsDrilling => activeDrillCount > 0;

        /// <summary>
        /// 드릴이 경로에서 발견한 연쇄 발동 대기 중인 특수 블록 목록.
        /// </summary>
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;

        /// <summary>
        /// 특정 블록이 현재 드릴 발동 중(보호 상태)인지 확인합니다.
        /// </summary>
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);

        /// <summary>
        /// 비상 초기화. 드릴이 무한 루프에 빠지거나 예상치 못한 상황이 발생했을 때
        /// 모든 드릴 상태를 강제로 리셋합니다.
        /// (비유: 기계가 멈추지 않을 때 비상 정지 버튼을 누르는 것)
        /// </summary>
        public void ForceReset()
        {
            activeDrillCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.Log("[DrillBlockSystem] ForceReset called");
        }

        /// <summary>
        /// 코루틴이 중단되었을 때 화면에 남아있는 이펙트 오브젝트들을 모두 제거합니다.
        /// (비유: 불꽃놀이가 끝났는데 잔해가 남아있으면 청소하는 것)
        /// </summary>
        private void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);
        }



        // 이펙트(시각 효과) 오브젝트들이 생성될 부모 Transform.
        // Canvas 내부에 별도 레이어로 만들어서 블록 위에 이펙트가 그려지도록 합니다.
        private Transform effectParent;

        // ============================================================
        // 초기화
        // ============================================================

        /// <summary>
        /// 게임 시작 시 필요한 참조를 자동으로 찾고, 이펙트 레이어를 세팅합니다.
        /// </summary>
        private void Start()
        {
            // HexGrid가 인스펙터에서 연결되지 않았다면 씬에서 자동으로 찾습니다
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[DrillBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[DrillBlockSystem] HexGrid not found!");
            }
            // BlockRemovalSystem도 마찬가지로 자동 탐색
            if (removalSystem == null)
                removalSystem = FindObjectOfType<BlockRemovalSystem>();

            // Canvas 내부에 이펙트 전용 컨테이너(폴더) 생성
            SetupEffectParent();
        }

        /// <summary>
        /// 이펙트가 그려질 전용 레이어를 Canvas 안에 생성합니다.
        /// 이 레이어는 그리드보다 위에 위치하여, 이펙트가 블록 위에 표시됩니다.
        /// (비유: 투명 필름을 그림 위에 올려놓고 그 위에 특수효과를 그리는 것)
        /// </summary>
        private void SetupEffectParent()
        {
            if (hexGrid == null) return;

            // GridContainer의 부모(Canvas) 아래에 이펙트 레이어 생성
            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("DrillEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);

                // 화면 전체를 덮는 RectTransform 설정
                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                // 가장 마지막 자식으로 배치 → 다른 UI 위에 그려짐
                effectLayer.transform.SetAsLastSibling();

                effectParent = effectLayer.transform;
                Debug.Log("[DrillBlockSystem] Effect layer created under Canvas");
            }
            else
            {
                // Canvas를 못 찾으면 hexGrid 자체를 부모로 사용 (비상용)
                effectParent = hexGrid.transform;
                Debug.LogWarning("[DrillBlockSystem] Canvas not found, using hexGrid as effect parent");
            }
        }

        // ============================================================
        // 드릴 생성
        // ============================================================

        /// <summary>
        /// 일반 블록을 드릴 특수 블록으로 변환합니다.
        /// 4개 블록이 일직선으로 매칭되면 MatchingSystem이 이 메서드를 호출합니다.
        /// (비유: 일반 블록에 "드릴 능력"을 부여하는 것)
        /// </summary>
        /// <param name="block">드릴로 변환할 블록</param>
        /// <param name="direction">드릴이 발사될 방향 (세로/대각선 등)</param>
        /// <param name="gemType">블록의 보석 색상 (드릴의 색상을 결정)</param>
        public void CreateDrillBlock(HexBlock block, DrillDirection direction, GemType gemType)
        {
            if (block == null) return;
            BlockData drillData = new BlockData(gemType);
            drillData.specialType = SpecialBlockType.Drill;
            drillData.drillDirection = direction;
            block.SetBlockData(drillData);
            block.ShowDrillIndicator(direction);
            Debug.Log($"[DrillBlockSystem] Created drill at {block.Coord}, direction={direction}");
        }

        // ============================================================
        // 드릴 발동
        // ============================================================

        /// <summary>
        /// 드릴 블록을 발동시킵니다.
        /// 이 메서드가 호출되면 투사체가 양방향으로 발사되어 경로의 블록들을 파괴합니다.
        /// (비유: 방아쇠를 당기는 것 — 이후 투사체가 알아서 날아감)
        /// </summary>
        /// <param name="drillBlock">발동시킬 드릴 블록</param>
        public void ActivateDrill(HexBlock drillBlock)
        {
            if (drillBlock == null) return;
            if (drillBlock.Data == null || drillBlock.Data.specialType != SpecialBlockType.Drill)
                return;
            Debug.Log($"[DrillBlockSystem] Activating drill at {drillBlock.Coord}, direction={drillBlock.Data.drillDirection}");
            StartCoroutine(DrillCoroutine(drillBlock));
        }

        /// <summary>
        /// 드릴 발동의 전체 과정을 처리하는 메인 코루틴.
        ///
        /// 실행 순서:
        ///   1단계: 압축 애니메이션 (발사 전 "에너지 모으기" 연출)
        ///   2단계: 히트스톱 + 줌펀치 (잠깐 멈추며 임팩트 강조)
        ///   3단계: 블록 데이터 클리어 (드릴 자체는 사라짐)
        ///   4단계: 양방향 타겟 수집 (경로에 있는 블록들 목록화)
        ///   5단계: 점수 미리 계산 (블록이 파괴되기 전에 정보 저장)
        ///   6단계: 발사 이펙트 + 양방향 투사체 발사
        ///   7단계: 양방향 투사체 모두 완료 대기
        ///   8단계: 최종 점수 통보 및 미션 시스템에 결과 전달
        /// </summary>
private IEnumerator DrillCoroutine(HexBlock drillBlock)
        {
            // 발동 중인 드릴 카운터 증가 및 보호 목록에 등록
            activeDrillCount++;
            activeBlocks.Add(drillBlock);

            // 동시 발동 시 첫 번째 드릴만 메인 이펙트 재생
            bool isFirstDrill = (activeDrillCount == 1);

            // 드릴의 방향, 시작 좌표, 위치, 색상 정보를 미리 저장
            DrillDirection direction = drillBlock.Data.drillDirection;
            HexCoord startCoord = drillBlock.Coord;
            Vector3 drillWorldPos = drillBlock.transform.position;
            Color drillColor = GetDrillColor(drillBlock);

            Debug.Log($"[DrillBlockSystem] === DRILL ACTIVATED === Coord={startCoord}, Direction={direction}, isFirstDrill={isFirstDrill}");

            // [1단계] 발사 전 압축 애니메이션 — 첫 번째 드릴만
            if (isFirstDrill)
                yield return StartCoroutine(PreFireCompression(drillBlock));

            // [2단계] 히트스톱 — 첫 번째 드릴만
            if (isFirstDrill)
                StartCoroutine(HitStop(VisualConstants.HitStopDurationSmall));

            // [2단계] 줌펀치 — 첫 번째 드릴만
            if (isFirstDrill)
                StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // [3단계] 드릴 블록 자체의 데이터를 지움 (투사체는 별도로 날아감)
            drillBlock.ClearData();

            // [4단계] 양방향으로 경로에 있는 블록들을 수집
            // 예: Vertical이면 위쪽(positive)과 아래쪽(negative)
            List<HexBlock> targets1 = GetBlocksInDirection(startCoord, direction, true);
            List<HexBlock> targets2 = GetBlocksInDirection(startCoord, direction, false);

            Debug.Log($"[DrillBlockSystem] Targets: positive={targets1.Count}, negative={targets2.Count}");
            foreach (var t in targets1)
                Debug.Log($"[DrillBlockSystem]   +target: {t.Coord} pos={t.transform.position}");
            foreach (var t in targets2)
                Debug.Log($"[DrillBlockSystem]   -target: {t.Coord} pos={t.transform.position}");

            // [5단계] 파괴 대상 블록의 점수를 미리 계산
            // (블록이 파괴되면 데이터가 사라지므로, 파괴 전에 점수 정보를 저장해둡니다)
            int blockScoreSum = 0;
            int basicBlockCount = 0;  // 기본 블록(GemType 1-5) 카운트
            var gemCountsByColor = new Dictionary<GemType, int>(); // 색상별 미션 카운팅용
            var sm = GameManager.Instance?.GetComponent<ScoreManager>();
            List<HexBlock> allDrillTargets = new List<HexBlock>(targets1);
            allDrillTargets.AddRange(targets2);
            foreach (var t in allDrillTargets)
            {
                if (t == null || t.Data == null || t.Data.gemType == GemType.None) continue;
                // 특수 블록은 점수 계산에서 제외 (직접 파괴하지 않으므로)
                if (t.Data.specialType != SpecialBlockType.None && t.Data.specialType != SpecialBlockType.FixedBlock) continue;
                blockScoreSum += ScoreCalculator.GetBlockBaseScore(t.Data.tier);

                // 기본 블록 카운트 (GemType 1-5)
                if ((int)t.Data.gemType >= 1 && (int)t.Data.gemType <= 5)
                {
                    basicBlockCount++;
                    // 색상별 카운트 (미션 기여용)
                    if (gemCountsByColor.ContainsKey(t.Data.gemType))
                        gemCountsByColor[t.Data.gemType]++;
                    else
                        gemCountsByColor[t.Data.gemType] = 1;
                }

                // 적군(장애물) 블록 점수 처리 — 가시, 사슬, 회색 블록 등
                if (sm != null)
                {
                    if (t.Data.hasThorn)
                        sm.AddEnemyScore(EnemyType.ThornParasite, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                    if (t.Data.hasChain)
                        sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                    if (t.Data.gemType == GemType.Gray)
                        sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                    if (t.Data.enemyType != EnemyType.None && t.Data.enemyType != EnemyType.Chromophage
                        && t.Data.enemyType != EnemyType.ChainAnchor && t.Data.enemyType != EnemyType.ThornParasite)
                        sm.AddEnemyScore(t.Data.enemyType, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                }
            }

            // [6단계] 중앙에서 발사 이펙트 재생 (섬광 + 빛줄기 + 스파크) — 첫 번째 드릴만
            if (isFirstDrill)
                StartCoroutine(DrillLaunchEffect(drillWorldPos, direction, drillColor));

            // 양방향 동시에 투사체 발사 — 두 코루틴이 동시에 실행됨
            Coroutine drill1 = StartCoroutine(DrillLineWithProjectile(
                drillWorldPos, targets1, direction, true, drillColor, isFirstDrill));
            Coroutine drill2 = StartCoroutine(DrillLineWithProjectile(
                drillWorldPos, targets2, direction, false, drillColor, isFirstDrill));

            // 색상별 미션 카운팅 — 파괴 애니메이션 시작 직후, 완료 대기 전에 호출
            GameManager.Instance?.OnSpecialBlockDestroyedBlocksByColor(gemCountsByColor, "Drill");

            // [7단계] 양방향 투사체가 모두 끝날 때까지 대기
            yield return drill1;
            yield return drill2;

            // [8단계] 최종 점수 계산 (드릴 기본 100점 + 파괴한 블록들의 점수 합)
            int totalScore = 100 + blockScoreSum;
            Debug.Log($"[DrillBlockSystem] === DRILL COMPLETE === Score={totalScore} (base:100 + blockTierSum:{blockScoreSum})");

            // 미션 시스템에 파괴된 블록들 알림 (기본 블록만 — 특수 블록은 별도 처리됨)
            List<HexBlock> basicBlocksOnly = new List<HexBlock>();
            foreach (var t in allDrillTargets)
            {
                if (t != null && t.Data != null && t.Data.gemType != GemType.None)
                {
                    // 기본 블록만 포함: 특수블록 제외
                    if (t.Data.specialType == SpecialBlockType.None || t.Data.specialType == SpecialBlockType.FixedBlock)
                    {
                        basicBlocksOnly.Add(t);
                        Debug.Log($"[DrillBlockSystem]   BasicBlock: {t.Coord}, gemType={t.Data.gemType}");
                    }
                }
            }

            Debug.Log($"[DrillBlockSystem] Passing {basicBlocksOnly.Count} basic blocks to MissionSystem (from {allDrillTargets.Count} total targets)");

            // 미션 시스템에 "드릴이 이 블록들을 파괴했다"고 알림
            MissionSystem ms = Object.FindObjectOfType<MissionSystem>();
            if (ms != null)
            {
                ms.OnSpecialBlockDestroyedBlocks(basicBlocksOnly);
            }
            else
            {
                Debug.LogWarning("[DrillBlockSystem] MissionSystem not found!");
            }

            // 드릴 완료 이벤트 발생 → 점수 반영
            OnDrillComplete?.Invoke(totalScore);
            // 보호 목록에서 제거 및 카운터 감소
            activeBlocks.Remove(drillBlock);
            activeDrillCount--;
        }

        // ============================================================
        // 방향별 블록 수집
        // (드릴 경로 위에 어떤 블록들이 있는지 목록을 만듭니다)
        // ============================================================

        /// <summary>
        /// 지정된 방향으로 한 칸씩 이동하면서, 경로에 있는 블록들을 수집합니다.
        /// (비유: 볼링 레인 위에 서 있는 핀들을 순서대로 확인하는 것)
        /// </summary>
        /// <param name="start">시작 좌표 (드릴이 있던 위치)</param>
        /// <param name="direction">드릴 방향 (세로/대각선 등)</param>
        /// <param name="positive">양의 방향이면 true, 음의 방향이면 false</param>
        /// <returns>경로 위의 블록 목록 (시작점에서 가까운 순서)</returns>
private List<HexBlock> GetBlocksInDirection(HexCoord start, DrillDirection direction, bool positive)
        {
            List<HexBlock> blocks = new List<HexBlock>();
            if (hexGrid == null) return blocks;

            // 한 칸 이동할 때의 좌표 변화량(delta)을 구합니다
            HexCoord delta = GetDirectionDelta(direction, positive);
            HexCoord current = start + delta; // 시작점 다음 칸부터 탐색
            int maxSteps = 20; // 무한 루프 방지용 최대 이동 횟수
            int step = 0;
            // 그리드 범위 안에 있는 한 계속 전진
            while (hexGrid.IsValidCoord(current) && step < maxSteps)
            {
                HexBlock block = hexGrid.GetBlock(current);
                // 유효한 블록이 있으면 목록에 추가
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    blocks.Add(block);
                current = current + delta; // 다음 칸으로 전진
                step++;
            }
            return blocks;
        }

        /// <summary>
        /// 각 DrillDirection에 따른 육각형 좌표 이동량(delta)을 반환합니다.
        ///
        /// 육각형 그리드에서의 방향 매핑:
        ///   - Vertical (세로 |): r축 방향으로 이동 → (0, +1) 또는 (0, -1)
        ///   - Slash (대각선 /): s축 방향으로 이동 → (+1, -1) 또는 (-1, +1)
        ///   - BackSlash (대각선 \): q축 방향으로 이동 → (+1, 0) 또는 (-1, 0)
        ///
        /// (비유: 체스에서 룩은 직선, 비숍은 대각선으로 이동하듯,
        ///  육각형에서도 각 방향마다 정해진 이동 규칙이 있습니다)
        /// </summary>
private HexCoord GetDirectionDelta(DrillDirection direction, bool positive)
        {
            // Flat-top hex 배치 기준 (CalculateFlatTopHexPosition)
            // x = 1.5*q, y = -√3*(r + q/2)
            // (0,±1) = 순수 세로, (±1,0) = \ 방향, (±1,∓1) = / 방향
            int sign = positive ? 1 : -1;
            switch (direction)
            {
                // Vertical = r축: 화면상 순수 세로 ↕
                case DrillDirection.Vertical:  return new HexCoord(0, sign);
                // Slash = s축: 화면상 / 방향 (세로에서 시계 60°)
                case DrillDirection.Slash:     return new HexCoord(sign, -sign);
                // BackSlash = q축: 화면상 \ 방향 (세로에서 시계 120°)
                case DrillDirection.BackSlash: return new HexCoord(sign, 0);
                default: return new HexCoord(0, sign);
            }
        }

        // ============================================================
        // 드릴 투사체 + 라인 파괴 애니메이션
        // (실제로 투사체가 날아가면서 블록을 하나씩 부수는 과정)
        // ============================================================

        /// <summary>
        /// 한 방향으로 투사체를 발사하여 경로 위의 블록들을 순서대로 파괴합니다.
        ///
        /// 동작 과정:
        ///   1. 투사체 오브젝트 생성 (드릴 모양의 작은 탄환)
        ///   2. 첫 번째 타겟을 향해 투사체 이동
        ///   3. 타겟 도달 → 블록 파괴 (파편 + 충격파 이펙트)
        ///   4. 다음 타겟으로 이동 → 반복
        ///   5. 모든 타겟 처리 후 투사체가 화면 밖으로 사라짐
        ///
        /// 특수 블록을 만나면: 파괴하지 않고 "연쇄 발동 대기 목록"에 추가합니다.
        /// (비유: 총알이 날아가다가 폭탄을 만나면 직접 터뜨리지 않고 "나중에 터뜨려라"라고 표시해두는 것)
        /// </summary>
private IEnumerator DrillLineWithProjectile(
            Vector3 startPos, List<HexBlock> targets, DrillDirection direction,
            bool positive, Color drillColor, bool showEffects = true)
        {
            if (targets.Count == 0) yield break;
            // 각 블록 파괴 애니메이션의 코루틴 핸들을 저장 (나중에 완료 대기용)
            List<Coroutine> destroyCoroutines = new List<Coroutine>();

            // 첫 번째 타겟 방향으로 투사체의 초기 각도 계산
            Vector3 firstTargetPos = targets[0].transform.position;
            Vector3 initDir = (firstTargetPos - startPos).normalized;
            float initAngle = Mathf.Atan2(initDir.y, initDir.x) * Mathf.Rad2Deg;

            // 투사체(드릴 탄환) 생성 — 첫 번째 드릴만
            GameObject projectile = showEffects ? CreateProjectileWithAngle(startPos, initAngle, drillColor) : null;

            Vector3 currentPos = startPos;
            Vector3 lastMoveDir = initDir; // 마지막 이동 방향 (투사체 퇴장 시 사용)

            // 각 타겟 블록을 순서대로 방문하며 파괴
            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;

                Vector3 targetPos = target.transform.position;
                lastMoveDir = (targetPos - currentPos).normalized;

                // 투사체가 다음 타겟을 향하도록 회전
                float moveAngle = Mathf.Atan2(lastMoveDir.y, lastMoveDir.x) * Mathf.Rad2Deg;
                if (projectile != null)
                    projectile.transform.rotation = Quaternion.Euler(0, 0, moveAngle - 90f);

                // 투사체가 현재 위치에서 타겟까지 이동하는 애니메이션
                float distance = Vector3.Distance(currentPos, targetPos);
                float travelTime = Mathf.Max(distance / projectileSpeed, 0.01f);
                float elapsed = 0f;
                Vector3 moveStart = currentPos;

                while (elapsed < travelTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / travelTime;
                    // 투사체를 시작점에서 타겟까지 선형 보간으로 이동
                    if (projectile != null)
                        projectile.transform.position = Vector3.Lerp(moveStart, targetPos, t);
                    yield return null;
                }

                currentPos = targetPos;

                // 안전 검사: 다른 드릴/폭탄 등이 동시에 이 블록을 처리했을 수 있음
                if (target.Data == null || target.Data.gemType == GemType.None) continue;

                // 반사 방패(ReflectionShield) 적군: 특수 블록 공격을 흡수
                if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(target))
                {
                    if (showEffects)
                        StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));
                    continue; // 방패가 흡수 → 블록 보존, 다음 타겟으로
                }

                // 경로 위의 블록이 "다른 특수 블록"인 경우: 직접 파괴하지 않고 연쇄 예약
                if (target.Data.specialType != SpecialBlockType.None && target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    Debug.Log($"[DrillBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
                    if (!pendingSpecialBlocks.Contains(target))
                    {
                        pendingSpecialBlocks.Add(target);
                        // 곧 발동될 것임을 시각적으로 표시 (빨간 테두리 + 점멸)
                        target.SetPendingActivation();
                        target.StartWarningBlink(10f);
                    }
                    if (showEffects)
                        StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));
                }
                else
                {
                    // 일반 블록: 파편과 함께 파괴 애니메이션 실행
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    float drillAngle = GetDirectionAngle(direction, positive);
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithDebris(target, blockColor, drillAngle, showEffects)));
                    if (showEffects)
                        StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));
                }

                // 블록 사이 약간의 간격을 두어 "하나씩 뚫어가는" 느낌 연출
                yield return new WaitForSeconds(drillSpeed);
            }

            // --- 투사체 퇴장 애니메이션 ---
            // 마지막 블록을 지난 후 투사체가 서서히 사라지며 화면 밖으로 나감

            if (lastMoveDir == Vector3.zero)
                lastMoveDir = initDir;

            // 투사체의 모든 이미지 컴포넌트와 초기 투명도를 수집
            List<UnityEngine.UI.Image> imgs = new List<UnityEngine.UI.Image>();
            List<float> initAlphas = new List<float>();
            if (projectile != null)
            {
                foreach (var img in projectile.GetComponentsInChildren<UnityEngine.UI.Image>())
                {
                    imgs.Add(img);
                    initAlphas.Add(img.color.a);
                }
            }

            // 퇴장: 점점 투명해지고 작아지면서 마지막 방향으로 날아감
            float exitDuration = 0.3f;
            float exitElapsed = 0f;
            Vector3 exitStart = currentPos;

            while (exitElapsed < exitDuration && projectile != null)
            {
                exitElapsed += Time.deltaTime;
                float t = exitElapsed / exitDuration;

                // 마지막 이동 방향으로 계속 전진
                projectile.transform.position = exitStart + lastMoveDir * projectileSpeed * exitElapsed;

                // 서서히 투명해짐
                float fade = 1f - t;
                projectile.transform.localScale = Vector3.one * (1f - t * 0.5f);

                for (int i = 0; i < imgs.Count; i++)
                {
                    if (imgs[i] != null)
                    {
                        Color c = imgs[i].color;
                        c.a = initAlphas[i] * fade;
                        imgs[i].color = c;
                    }
                }

                yield return null;
            }

            // 투사체 오브젝트 제거
            if (projectile != null) Destroy(projectile);

            // 모든 블록 파괴 애니메이션이 완료될 때까지 대기
            // (ClearData가 확실히 호출되도록 보장)
            foreach (var co in destroyCoroutines)
                yield return co;

        }

        // ============================================================
        // 투사체 생성 - Canvas 내부에 UI.Image로 생성
        // (드릴이 발사하는 "총알" 모양의 오브젝트를 프로그래밍으로 그립니다)
        // ============================================================

        /// <summary>
        /// 드릴 투사체를 생성합니다 (방향 기반 버전).
        /// DrillDirection과 양/음 방향으로부터 각도를 계산하여 CreateProjectileWithAngle을 호출합니다.
        /// </summary>
private GameObject CreateProjectile(Vector3 worldPos, DrillDirection direction, bool positive, Color color)
        {
            float angle = GetDirectionAngle(direction, positive);
            return CreateProjectileWithAngle(worldPos, angle, color);
        }

        /// <summary>
        /// 드릴 투사체(탄환) 오브젝트를 프로시저럴(코드로 직접 그리는 방식)로 생성합니다.
        ///
        /// 투사체 구조 (아래에서 위 순서로):
        ///   - Glow: 부드러운 빛나는 원 (아우라처럼 주변을 밝힘)
        ///   - DrillBody: 뾰족한 드릴 모양의 본체 (실제 총알 모양)
        ///   - Trail: 꼬리 불꽃 (로켓 엔진처럼 뒤에 불꽃이 나옴)
        ///   - Coreline: 중심을 관통하는 밝은 선 (광선 느낌)
        ///
        /// worldAngleDeg은 실제 이동 방향의 각도(도 단위)이며,
        /// 투사체 스프라이트가 위쪽이 앞이므로 -90도 보정합니다.
        /// </summary>
        private GameObject CreateProjectileWithAngle(Vector3 worldPos, float worldAngleDeg, Color color)
        {
            // 최상위 투사체 오브젝트 생성
            GameObject obj = new GameObject("DrillProjectile");
            obj.transform.SetParent(effectParent != null ? effectParent : hexGrid.transform, false);
            obj.transform.position = worldPos;
            // 이동 방향에 맞게 회전 (-90도 보정: 스프라이트의 "위"가 진행 방향)
            obj.transform.rotation = Quaternion.Euler(0, 0, worldAngleDeg - 90f);

            // 색상 변형: 밝은 버전과 어두운 버전을 만들어 입체감 표현
            Color bright = VisualConstants.DrillBrighten(color);
            bright.a = 1f;
            Color dark = VisualConstants.Darken(color);
            dark.a = 1f;

            // 각 파트의 스프라이트(이미지)를 코드로 생성
            Sprite drillSprite = GenerateDrillSprite(color, bright, dark);  // 드릴 본체
            Sprite glowSprite = GenerateCircleSprite(32, new Color(bright.r, bright.g, bright.b, 0.3f)); // 글로우
            Sprite trailSprite = GenerateTrailSprite(color, bright);  // 꼬리 불꽃

            // --- 1. Glow (빛나는 원) ---
            // 투사체 주변에 은은하게 빛나는 후광 효과
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(obj.transform, false);
            glow.transform.localPosition = Vector3.zero;
            var glowImg = glow.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false; // 터치 입력을 방해하지 않음
            glowImg.sprite = glowSprite;
            glowImg.color = Color.white;
            glow.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
            // 글로우가 맥동(커졌다 작아졌다)하는 애니메이션 시작
            StartCoroutine(PulseGlow(glow.GetComponent<RectTransform>(), glowImg));

            // --- 2. DrillBody (드릴 본체) ---
            // 뾰족한 드릴 모양의 메인 이미지
            GameObject body = new GameObject("DrillBody");
            body.transform.SetParent(obj.transform, false);
            body.transform.localPosition = Vector3.zero;
            var bodyImg = body.AddComponent<UnityEngine.UI.Image>();
            bodyImg.raycastTarget = false;
            bodyImg.sprite = drillSprite;
            bodyImg.color = Color.white;
            bodyImg.preserveAspect = true; // 비율 유지
            body.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 34f);

            // --- 3. Trail (꼬리 불꽃) ---
            // 투사체 뒤쪽에 나타나는 잔상/불꽃 효과
            GameObject trail = new GameObject("Trail");
            trail.transform.SetParent(obj.transform, false);
            trail.transform.localPosition = new Vector3(0, -18f, 0); // 본체 아래쪽에 위치
            var trailImg = trail.AddComponent<UnityEngine.UI.Image>();
            trailImg.raycastTarget = false;
            trailImg.sprite = trailSprite;
            trailImg.color = Color.white;
            trailImg.preserveAspect = false;
            RectTransform trailRt = trail.GetComponent<RectTransform>();
            trailRt.sizeDelta = new Vector2(10f, 22f);
            trailRt.pivot = new Vector2(0.5f, 1f); // 위쪽 중심이 기준점
            // 꼬리 불꽃이 출렁이는 애니메이션 시작
            StartCoroutine(AnimateTrail(trailRt, trailImg, bright));

            // --- 4. Coreline (중심 광선) ---
            // 드릴 중심을 관통하는 밝은 흰색 선 (마감 효과)
            GameObject coreline = new GameObject("Coreline");
            coreline.transform.SetParent(obj.transform, false);
            coreline.transform.localPosition = Vector3.zero;
            var coreImg = coreline.AddComponent<UnityEngine.UI.Image>();
            coreImg.raycastTarget = false;
            coreImg.color = new Color(1f, 1f, 1f, 0.7f); // 반투명 흰색
            RectTransform coreRt = coreline.GetComponent<RectTransform>();
            coreRt.sizeDelta = new Vector2(2f, 28f); // 가늘고 긴 선

            // 글로우를 맨 뒤로 보내서 다른 요소 뒤에 그려지게 함
            glow.transform.SetAsFirstSibling();
            return obj;
        }

        // ============================================================
        // 프로시저럴 스프라이트 생성 메서드들
        // (외부 이미지 파일 없이, 코드로 직접 픽셀을 찍어서 이미지를 만듭니다)
        // ============================================================

        /// <summary>
        /// 드릴 본체의 스프라이트를 픽셀 단위로 생성합니다.
        /// 아래쪽은 넓고, 위쪽으로 갈수록 뾰족해지는 "탄환" 모양입니다.
        /// 가장자리는 어둡고 중심은 밝아서 입체감이 납니다.
        /// 아래쪽에는 작은 "날개(Fins)"도 달려 있습니다.
        /// </summary>
        private Sprite GenerateDrillSprite(Color baseColor, Color bright, Color dark)
        {
            int w = 32, h = 48; // 이미지 크기: 32x48 픽셀
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear; // 부드러운 보간
            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear; // 투명으로 초기화

            float cx = w / 2f; // 가로 중심
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1); // 0(아래) ~ 1(위)
                float halfW; // 해당 높이에서의 반 너비

                // 60% 지점까지는 완만하게 좁아지고, 그 위부터는 급격히 뾰족해짐
                if (t < 0.6f)
                    halfW = Mathf.Lerp(6.5f, 3.5f, t / 0.6f);
                else
                    halfW = Mathf.Lerp(3.5f, 0f, Mathf.Pow((t - 0.6f) / 0.4f, 1.5f));

                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx >= halfW) continue; // 드릴 모양 바깥은 투명
                    float edge = dx / Mathf.Max(halfW, 0.01f); // 가장자리까지의 비율
                    Color c;
                    // 위쪽 끝(뾰족한 부분): 밝은 색
                    if (t > 0.7f) c = Color.Lerp(bright, baseColor, edge);
                    // 아래쪽 끝(밑바닥): 어두운 색
                    else if (t < 0.12f) c = Color.Lerp(dark, baseColor, edge * 0.5f);
                    // 중간 부분: 하이라이트 + 그림자로 입체감
                    else
                    {
                        float hl = Mathf.Pow(1f - edge, 3f); // 중심이 밝은 하이라이트
                        c = Color.Lerp(baseColor, bright, hl * 0.5f);
                        c = Color.Lerp(c, dark, edge * 0.25f); // 가장자리는 약간 어둡게
                    }
                    c.a = Mathf.Clamp01((halfW - dx) * 2.5f); // 가장자리 안티앨리어싱
                    px[y * w + x] = c;
                }
            }

            // Fins (날개) — 드릴 아래쪽 양 옆에 작은 날개 추가
            for (int y = 0; y < 10; y++)
            {
                float ft = (float)y / 9f;
                float finW = Mathf.Lerp(9f, 5f, ft); // 아래쪽일수록 넓은 날개
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx >= 4f && dx < finW) // 본체 바깥~날개 끝 범위
                    {
                        float ef = (dx - 4f) / Mathf.Max(finW - 4f, 0.01f);
                        Color fc = dark; fc.a = (1f - ef) * (1f - ft * 0.6f);
                        Color ex = px[y * w + x];
                        px[y * w + x] = Color.Lerp(ex, fc, fc.a); // 기존 색과 블렌딩
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 원형 글로우(빛나는 원) 스프라이트를 생성합니다.
        /// 중심이 밝고 바깥으로 갈수록 투명해지는 부드러운 원입니다.
        /// (비유: 어두운 밤에 손전등을 켜면 보이는 둥근 빛 테두리)
        /// </summary>
        /// <param name="sz">이미지 크기(정사각형 한 변의 픽셀 수)</param>
        /// <param name="col">원의 색상</param>
        private Sprite GenerateCircleSprite(int sz, Color col)
        {
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            float c = sz / 2f, r = sz / 2f; // 중심점과 반지름
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    // 중심으로부터의 거리 비율 (0=중심, 1=가장자리)
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / r;
                    // 원 안이면 거리에 따라 투명도 조절 (중심이 가장 불투명)
                    px[y * sz + x] = d < 1f
                        ? new Color(col.r, col.g, col.b, col.a * (1f - d * d))
                        : Color.clear;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 투사체 꼬리 불꽃 스프라이트를 생성합니다.
        /// 위쪽은 좁고 투명, 아래쪽은 넓고 밝아서 "불꽃이 퍼지는" 느낌입니다.
        /// (비유: 로켓 엔진에서 뿜어져 나오는 화염의 모양)
        /// </summary>
        private Sprite GenerateTrailSprite(Color baseColor, Color bright)
        {
            int w = 16, h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[w * h];
            float cx = w / 2f;
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1); // 0(위, 좁음) ~ 1(아래, 넓음)
                float halfW = Mathf.Lerp(0.5f, w / 2f - 1f, t); // 아래로 갈수록 넓어짐
                float alpha = Mathf.Lerp(0f, 0.65f, t); // 아래로 갈수록 불투명
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx >= halfW) { px[y * w + x] = Color.clear; continue; }
                    float ef = dx / Mathf.Max(halfW, 0.01f);
                    Color c = Color.Lerp(bright, baseColor, ef); // 중심은 밝고 가장자리는 기본색
                    c.a = alpha * (1f - ef * ef);
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f);
        }

        // ============================================================
        // 투사체 부속 애니메이션
        // ============================================================

        /// <summary>
        /// 글로우(빛나는 원)가 심장처럼 "두근두근" 맥동하는 애니메이션.
        /// 투사체가 살아있는 듯한 느낌을 줍니다.
        /// </summary>
        private IEnumerator PulseGlow(RectTransform rt, UnityEngine.UI.Image img)
        {
            float time = 0f;
            Color baseColor = img.color;
            while (rt != null && img != null)
            {
                time += Time.deltaTime * 8f; // 빠른 맥동 주기
                float pulse = 1f + 0.3f * Mathf.Sin(time); // 크기: 1.0 ~ 1.3
                rt.sizeDelta = new Vector2(56f * pulse, 56f * pulse);
                baseColor.a = 0.25f + 0.2f * Mathf.Sin(time); // 투명도도 함께 변화
                img.color = baseColor;
                yield return null;
            }
        }

        /// <summary>
        /// 꼬리 불꽃이 출렁이며 길이와 투명도가 변하는 애니메이션.
        /// 로켓 불꽃이 흔들리는 것 같은 효과를 냅니다.
        /// </summary>
private IEnumerator AnimateTrail(RectTransform rt, UnityEngine.UI.Image img, Color baseColor)
        {
            float time = 0f;
            while (rt != null && img != null)
            {
                time += Time.deltaTime * 10f;
                // 트레일 길이 변화 (출렁이는 느낌)
                float len = 35f + 8f * Mathf.Sin(time);
                rt.sizeDelta = new Vector2(5f, len);
                // 투명도 변화
                float alpha = 0.25f + 0.1f * Mathf.Sin(time * 1.3f);
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
        }


        // ============================================================
        // 블록 파괴 + 파편 이펙트
        // (투사체가 블록에 도달했을 때 실행되는 파괴 연출)
        // ============================================================

        /// <summary>
        /// 블록을 파편과 함께 파괴하는 애니메이션.
        ///
        /// 연출 순서:
        ///   1. 흰색 플래시 (번쩍!)
        ///   2. 충격파 (물결처럼 퍼지는 원)
        ///   3. 파편 생성 (블록 조각들이 사방으로 튀어나감)
        ///   4. 이중 이징 파괴: 블록이 살짝 커졌다가(확대) → 찌그러지면서 사라짐(축소)
        ///   5. 블록 데이터 초기화 (ClearData)
        ///
        /// (비유: 유리가 깨질 때 — 순간 빛이 번쩍하고, 충격파가 퍼지고, 파편이 날아감)
        /// </summary>
        /// <param name="block">파괴할 블록</param>
        /// <param name="blockColor">블록의 색상 (파편 색상에 사용)</param>
        /// <param name="drillAngle">드릴 진행 방향 각도 (파편이 수직으로 퍼지도록)</param>
        /// <param name="showEffects">이펙트 표시 여부 (동시 드릴 중복 방지용)</param>
        private IEnumerator DestroyBlockWithDebris(HexBlock block, Color blockColor, float drillAngle = -1f, bool showEffects = true)
        {
            if (block == null) yield break;

            Vector3 pos = block.transform.position;

            if (showEffects)
            {
                // 1. 화이트 플래시 — 블록 위에 흰색이 번쩍 나타났다 사라짐
                StartCoroutine(DestroyFlashOverlay(block));

                // 2. 충격파 — 원이 커지면서 퍼져나감
                StartCoroutine(ImpactWave(pos, blockColor));

                // 블록 파괴 사운드
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayBlockDestroySound();

                // 3. 파편 생성 — 블록 조각들이 드릴 방향의 수직으로 튀어나감
                float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
                int count = Mathf.RoundToInt((VisualConstants.DebrisBaseCount + Random.Range(0, 3)) * cascadeMult);
                for (int i = 0; i < count; i++)
                    StartCoroutine(AnimateDebris(pos, blockColor, drillAngle));
            }

            // 4. 이중 이징 파괴 애니메이션
            //    전반부: 블록이 20% 정도 확대됨 (터지기 직전 팽창)
            //    후반부: 가로로 찌그러지면서 세로로 납작해짐 (으스러짐)
            float duration = VisualConstants.DestroyDuration;
            float elapsed = 0f;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (t < expandRatio)
                {
                    // 전반부: 팽창 (EaseOutCubic → 처음에 빠르고 나중에 느려짐)
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = origScale * scale;
                }
                else
                {
                    // 후반부: 찌그러지며 축소 (가로는 볼록해졌다가, 세로는 사라짐)
                    float shrinkT = (t - expandRatio) / (1f - expandRatio);
                    float sx = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(shrinkT * Mathf.PI);
                    float sy = Mathf.Max(0f, 1f - VisualConstants.EaseInQuad(shrinkT));
                    block.transform.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);
                }

                yield return null;
            }

            // 5. 블록을 원래 크기로 되돌리고 데이터 초기화 (빈 칸이 됨)
            block.transform.localScale = Vector3.one;
            block.ClearData();
        }

        /// <summary>
        /// 파편 하나의 애니메이션을 담당합니다.
        /// 블록이 파괴될 때 작은 조각이 특정 방향으로 날아가다가,
        /// 중력에 의해 아래로 떨어지면서 서서히 사라집니다.
        /// (비유: 유리 파편이 튀어나갔다가 바닥으로 떨어지는 것)
        /// </summary>
        /// <param name="center">파편이 생성되는 위치 (블록 중심)</param>
        /// <param name="color">파편의 색상</param>
        /// <param name="directionAngle">드릴 진행 방향 각도. 파편은 이 방향의 수직으로 퍼짐</param>
        private IEnumerator AnimateDebris(Vector3 center, Color color, float directionAngle = -1f)
        {
            // 파편 오브젝트 생성
            GameObject debris = new GameObject("Debris");
            debris.transform.SetParent(effectParent != null ? effectParent : hexGrid.transform, false);
            debris.transform.position = center;

            var image = debris.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;

            // 색상에 약간의 변화를 줘서 자연스러운 파편 느낌
            float variation = Random.Range(-0.15f, 0.15f);
            Color debrisColor = new Color(
                Mathf.Clamp01(color.r + variation),
                Mathf.Clamp01(color.g + variation),
                Mathf.Clamp01(color.b + variation),
                1f
            );
            image.color = debrisColor;

            // 파편 크기 설정 (랜덤)
            RectTransform rt = debris.GetComponent<RectTransform>();
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            float w = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax);
            float h = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax * 0.9f);
            rt.sizeDelta = new Vector2(w, h);

            // 파편의 발사 방향 결정
            float angle;
            if (directionAngle >= 0f)
            {
                // 드릴 방향의 수직(perpendicular) 방향으로 퍼짐 (약간의 랜덤 퍼짐 포함)
                // (비유: 총알이 지나간 방향의 양옆으로 파편이 튀는 것)
                float perpAngle = (directionAngle + 90f) * Mathf.Deg2Rad;
                float spread = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
                angle = perpAngle + spread;
                if (Random.value > 0.5f) angle += Mathf.PI; // 반대쪽으로도 튐
            }
            else
            {
                // 방향 정보가 없으면 360도 전방향 랜덤
                angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            }
            float speed = Random.Range(VisualConstants.DebrisBaseSpeedMin, VisualConstants.DebrisBaseSpeedMax) * cascadeMult;
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float rotSpeed = Random.Range(VisualConstants.DebrisRotSpeedMin, VisualConstants.DebrisRotSpeedMax); // 회전 속도
            float lifetime = Random.Range(VisualConstants.DebrisLifetimeMin, VisualConstants.DebrisLifetimeMax); // 수명
            float elapsed = 0f;
            float rot = Random.Range(0f, 360f); // 초기 회전 각도

            // 파편 이동 루프: 날아가다 → 중력으로 떨어짐 → 투명해지며 사라짐
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                // 중력 적용 (시간이 지날수록 아래로 떨어짐)
                velocity.y += VisualConstants.DebrisGravity * Time.deltaTime;
                Vector3 pos = debris.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                debris.transform.position = pos;

                // 회전 (빙글빙글 돌면서 날아감)
                rot += rotSpeed * Time.deltaTime;
                debris.transform.localRotation = Quaternion.Euler(0, 0, rot);

                // 서서히 투명해짐
                debrisColor.a = 1f - t * t;
                image.color = debrisColor;
                // 서서히 작아짐
                float shrink = 1f - t * 0.5f;
                rt.sizeDelta = new Vector2(w * shrink, h * shrink);

                yield return null;
            }

            Destroy(debris);
        }

        /// <summary>
        /// 충격파 이펙트: 블록 파괴 시 원이 커지면서 퍼져나가는 물결 효과.
        /// (비유: 물에 돌을 던졌을 때 퍼져나가는 동심원)
        /// </summary>
        private IEnumerator ImpactWave(Vector3 pos, Color color)
        {
            // 충격파 원 오브젝트 생성
            GameObject wave = new GameObject("ImpactWave");
            wave.transform.SetParent(effectParent != null ? effectParent : hexGrid.transform, false);
            wave.transform.position = pos;

            var image = wave.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha);

            RectTransform rt = wave.GetComponent<RectTransform>();
            float initSize = VisualConstants.WaveSmallInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            float duration = VisualConstants.WaveSmallDuration;
            float elapsed = 0f;

            // 원이 커지면서(expand) 동시에 투명해짐(fade out)
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t); // 처음에 빠르게 퍼지고 나중에 느려짐
                float scale = 1f + eased * (VisualConstants.WaveSmallExpand - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha * (1f - t));
                yield return null;
            }

            Destroy(wave);
        }

        // ============================================================
        // 드릴 발사 시작 이펙트
        // (드릴이 발동하는 순간, 중앙에서 터지는 화려한 폭발 연출)
        // ============================================================

        /// <summary>
        /// 드릴 발사 순간의 중앙 폭발 이펙트.
        /// 블룸(후광), 중앙 섬광, 양방향 빛줄기, 스파크(불꽃), 화면 흔들림이
        /// 동시에 실행되어 임팩트 있는 발사 연출을 만듭니다.
        /// (비유: 대포가 발사될 때 포구에서 섬광이 번쩍이고 연기가 퍼지는 것)
        /// </summary>
        private IEnumerator DrillLaunchEffect(Vector3 pos, DrillDirection direction, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 레이어: 메인 섬광 뒤에 은은하게 퍼지는 큰 빛 (뒷배경 빛)
            StartCoroutine(BloomLayer(pos, VisualConstants.FlashColorDrill, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // 중앙 섬광: 밝은 빛이 번쩍 나타났다 사라짐
            GameObject flash = new GameObject("LaunchFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(VisualConstants.FlashColorDrill.r, VisualConstants.FlashColorDrill.g, VisualConstants.FlashColorDrill.b, 1f);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            // 양방향 빛줄기: 드릴 발사 방향으로 뻗어나가는 짧은 빔
            StartCoroutine(LaunchBeam(pos, direction, true, color));
            StartCoroutine(LaunchBeam(pos, direction, false, color));

            // 스파크 버스트: 여러 개의 작은 불꽃이 사방으로 튀어나감
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkMediumCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
                StartCoroutine(LaunchSpark(pos, color));

            // 화면 흔들림: 발사 임팩트로 화면이 미세하게 흔들림
            StartCoroutine(ScreenShake(VisualConstants.ShakeMediumIntensity, VisualConstants.ShakeMediumDuration));

            // 섬광이 커지면서 투명해지는 애니메이션
            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                img.color = new Color(VisualConstants.FlashColorDrill.r, VisualConstants.FlashColorDrill.g, VisualConstants.FlashColorDrill.b, 1f - t);
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 발사 순간 드릴 방향으로 뻗어나가는 짧은 빛줄기(빔) 이펙트.
        /// 좁은 직사각형이 빠르게 길어졌다가 사라집니다.
        /// (비유: 레이저 총에서 잠깐 뻗어나오는 빛줄기)
        /// </summary>
        private IEnumerator LaunchBeam(Vector3 pos, DrillDirection direction, bool positive, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject beam = new GameObject("LaunchBeam");
            beam.transform.SetParent(parent, false);
            beam.transform.position = pos;

            var img = beam.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0.7f);

            RectTransform rt = beam.GetComponent<RectTransform>();
            float angle = GetDirectionAngle(direction, positive);
            rt.localRotation = Quaternion.Euler(0, 0, angle); // 드릴 방향으로 회전
            rt.pivot = new Vector2(0.5f, 0f); // 아래 중심이 기준점 (위쪽으로 뻗어남)
            rt.sizeDelta = new Vector2(8f, 0f); // 처음에는 길이 0

            float duration = 0.15f;
            float elapsed = 0f;

            // 빔이 빠르게 길어지면서 동시에 좁아지고 투명해짐
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.sizeDelta = new Vector2(8f * (1f - t * 0.5f), 120f * t); // 너비 줄고, 높이 늘어남
                img.color = new Color(color.r, color.g, color.b, 0.7f * (1f - t)); // 서서히 투명
                yield return null;
            }

            Destroy(beam);
        }

        /// <summary>
        /// 발사 스파크(불꽃 입자) 하나의 애니메이션.
        /// 작은 밝은 점이 사방으로 날아갔다가 감속하며 사라집니다.
        /// (비유: 불꽃놀이에서 터지는 작은 불꽃 하나하나)
        /// </summary>
        private IEnumerator LaunchSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 스파크 오브젝트 생성
            GameObject spark = new GameObject("Spark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = VisualConstants.DrillBrighten(color); // 밝은 색으로

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkMediumSizeMin, VisualConstants.SparkMediumSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            // 랜덤 방향 + 랜덤 속도로 발사
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkMediumSpeedMin, VisualConstants.SparkMediumSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(VisualConstants.SparkMediumLifetimeMin, VisualConstants.SparkMediumLifetimeMax);
            float elapsed = 0f;
            Color c = img.color;

            // 날아가면서 감속 + 투명해짐 + 작아짐
            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= VisualConstants.SparkDeceleration; // 점점 느려짐
                c.a = 1f - t; // 투명해짐
                img.color = c;
                float s = size * (1f - t * 0.5f); // 작아짐
                rt.sizeDelta = new Vector2(s, s);
                yield return null;
            }

            Destroy(spark);
        }

        // ============================================================
        // 화면 흔들림 (Screen Shake)
        // 드릴 발동이나 블록 파괴 시 화면이 미세하게 떨려서 타격감을 줍니다.
        // Bug #11 fix: 여러 흔들림이 동시에 실행되어도 위치가 어긋나지 않도록
        //              shakeCount로 중첩 관리합니다.
        // ============================================================

        /// <summary>
        /// 현재 동시에 실행 중인 화면 흔들림의 개수.
        /// 마지막 흔들림이 끝날 때만 원래 위치로 복귀합니다.
        /// </summary>
        private int shakeCount = 0;

        /// <summary>
        /// 흔들림 시작 시 저장한 원래 위치. 흔들림이 끝나면 이 위치로 돌아옵니다.
        /// </summary>
        private Vector3 shakeOriginalPos;

        /// <summary>
        /// 화면을 랜덤하게 흔드는 코루틴.
        /// 시간이 지날수록 흔들림이 약해지다가(decay) 멈춥니다.
        /// (비유: 지진이 점점 약해지는 것)
        /// </summary>
        /// <param name="intensity">흔들림의 최대 세기 (픽셀 단위)</param>
        /// <param name="duration">흔들림 지속 시간(초)</param>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            // 첫 번째 흔들림이면 현재 위치를 "원래 위치"로 저장
            if (shakeCount == 0)
                shakeOriginalPos = target.localPosition;
            shakeCount++;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 시간이 지날수록 흔들림이 약해짐 (EaseInQuad 감쇠)
                float decay = 1f - VisualConstants.EaseInQuad(t);
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                target.localPosition = shakeOriginalPos + new Vector3(x, y, 0);
                yield return null;
            }

            // 흔들림 카운터 감소. 마지막 흔들림이 끝나면 원래 위치로 복원
            shakeCount--;
            if (shakeCount <= 0)
            {
                shakeCount = 0;
                target.localPosition = shakeOriginalPos;
            }
        }

        // ============================================================
        // 유틸리티 메서드
        // ============================================================

        /// <summary>
        /// 오브젝트를 서서히 투명하게 만들고 제거합니다.
        /// 갑자기 사라지지 않고 부드럽게 페이드아웃됩니다.
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
                c.a = 1f - t;
                img.color = c;
                yield return null;
            }

            Destroy(obj);
        }

        /// <summary>
        /// 드릴 블록의 보석 색상을 가져옵니다.
        /// 이 색상은 투사체, 이펙트 등의 색상으로 사용됩니다.
        /// </summary>
        private Color GetDrillColor(HexBlock block)
        {
            if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                return GemColors.GetColor(block.Data.gemType);
            return Color.white;
        }

        /// <summary>
        /// 각 DrillDirection의 화면상 각도(도 단위)를 반환합니다.
        ///
        /// Flat-top 육각형 기준 화면 각도:
        ///   - Vertical: -90도 (아래쪽), positive면 아래, 반대면 위
        ///   - Slash: +30도 (오른쪽 위), positive면 오른쪽 위
        ///   - BackSlash: -30도 (오른쪽 아래), positive면 오른쪽 아래
        ///
        /// positive=false이면 180도를 더해 반대 방향이 됩니다.
        /// </summary>
private float GetDirectionAngle(DrillDirection direction, bool positive)
        {
            // Flat-top hex 배치 기준 화면 각도
            // CalculateFlatTopHexPosition: x=1.5*q, y=-√3*(r+q/2)
            // (0,1) → (0, -√3) → 아래쪽 = -90° (positive = 아래쪽)
            // (1,-1) → (1.5, √3/2) → 오른쪽 위 = 30°
            // (1,0) → (1.5, -√3/2) → 오른쪽 아래 = -30°
            float angle;
            switch (direction)
            {
                case DrillDirection.Vertical:  angle = -90f; break; // r축: 순수 세로 (아래)
                case DrillDirection.Slash:     angle = 30f;  break; // s축: / 방향 (오른쪽 위)
                case DrillDirection.BackSlash: angle = -30f; break; // q축: \ 방향 (오른쪽 아래)
                default: angle = -90f; break;
            }
            return positive ? angle : angle + 180f;
        }

        /// <summary>
        /// 드릴 방향의 월드 좌표 단위 벡터를 반환합니다.
        /// 각도를 라디안으로 변환하여 (cos, sin) 벡터를 만듭니다.
        /// </summary>
        private Vector3 GetWorldDirection(DrillDirection direction, bool positive)
        {
            float angle = GetDirectionAngle(direction, positive) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
        }

        // ============================================================
        // 패턴 감지 (레거시 호환용 — 현재는 사용하지 않음)
        // 이전에는 이 메서드로 드릴 패턴을 감지했지만,
        // 현재는 MatchingSystem에서 직접 처리합니다.
        // ============================================================

        /// <summary>
        /// [레거시] 매칭된 블록들에서 드릴 패턴(4매칭 직선)을 감지합니다.
        /// 현재는 MatchingSystem이 이 역할을 대신하므로, 항상 null을 반환합니다.
        /// </summary>
        public DrillDirection? DetectDrillPattern(List<HexBlock> matchedBlocks)
        {
            if (matchedBlocks == null || matchedBlocks.Count < 4)
                return null;
            return null;
        }

        // ============================================================
        // Phase 1 VFX: 공통 유틸리티 메서드
        // 모든 특수 블록이 공통으로 사용하는 시각 효과 패턴들입니다.
        // ============================================================

        /// <summary>
        /// Pre-Fire 압축 애니메이션: 블록이 발동 직전에 "움츠렸다가 팽창"합니다.
        ///
        /// 애니메이션 흐름:
        ///   전반부 (0~50%): 1.0 → 0.85 축소 (에너지를 모으는 느낌)
        ///   후반부 (50~100%): 0.85 → 1.12 팽창 (발사 직전 팽창)
        ///   완료 후: 1.0으로 복원
        ///
        /// (비유: 권투 선수가 주먹을 뒤로 당겼다가(축소) 앞으로 내지르는(팽창) 것)
        /// </summary>
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
                    // 전반부: 축소 (EaseInQuad → 점점 빨라짐)
                    float ct = t / 0.5f;
                    scale = Mathf.Lerp(1f, VisualConstants.PreFireScaleMin, VisualConstants.EaseInQuad(ct));
                }
                else
                {
                    // 후반부: 팽창 (EaseOutCubic → 빠르게 커졌다가 안정)
                    float et = (t - 0.5f) / 0.5f;
                    scale = Mathf.Lerp(VisualConstants.PreFireScaleMin, VisualConstants.PreFireScaleMax, VisualConstants.EaseOutCubic(et));
                }

                block.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            block.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Hit Stop: 게임 시간을 잠깐 완전히 멈춘 뒤, 천천히 복구합니다.
        /// 강한 타격의 임팩트를 강조하기 위한 연출입니다.
        ///
        /// 흐름: 시간 정지(0%) → 슬로모션(30%) → 정상 속도(100%)
        ///
        /// (비유: 만화에서 주먹이 얼굴에 맞는 순간 화면이 멈추는 것)
        /// 쿨다운이 있어서 너무 자주 실행되지 않도록 제한합니다.
        /// </summary>
        private IEnumerator HitStop(float stopDuration)
        {
            // 쿨다운 확인: 너무 자주 실행되면 오히려 어색함
            if (!VisualConstants.CanHitStop()) yield break;
            VisualConstants.RecordHitStop();

            // 시간을 완전히 멈춤
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(stopDuration); // 실제 시간 기준 대기

            // 슬로모션에서 정상 속도로 서서히 복구
            float elapsed = 0f;
            while (elapsed < VisualConstants.HitStopSlowMoDuration)
            {
                elapsed += Time.unscaledDeltaTime; // 시간이 멈춰도 흐르는 실제 시간 사용
                float t = Mathf.Clamp01(elapsed / VisualConstants.HitStopSlowMoDuration);
                Time.timeScale = Mathf.Lerp(VisualConstants.HitStopSlowMoScale, 1f, VisualConstants.EaseOutCubic(t));
                yield return null;
            }
            Time.timeScale = 1f; // 최종적으로 정상 속도 보장
        }

        /// <summary>
        /// Zoom Punch: 게임 보드 전체가 잠깐 확대됐다가 원래 크기로 돌아옵니다.
        /// 폭발이나 강한 충격의 타격감을 화면 전체로 전달합니다.
        ///
        /// 흐름: 원래 크기 → 살짝 확대 → 원래 크기
        ///
        /// (비유: 카메라가 잠깐 줌인했다가 줌아웃하는 것)
        /// </summary>
        private IEnumerator ZoomPunch(float targetScale)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = target.localScale;
            Vector3 punchScale = origScale * targetScale; // 확대될 크기

            // 줌인 (원래 → 확대)
            float elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchInDuration);
                target.localScale = Vector3.Lerp(origScale, punchScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            // 줌아웃 (확대 → 원래)
            elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchOutDuration);
                target.localScale = Vector3.Lerp(punchScale, origScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            target.localScale = origScale; // 최종적으로 원래 크기 보장
        }

        /// <summary>
        /// 블록 파괴 순간의 백색 플래시 오버레이.
        /// 블록 위에 흰색 사각형이 번쩍 나타났다가 서서히 사라집니다.
        /// (비유: 카메라 플래시가 터지는 것)
        /// </summary>
        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            // 플래시 오브젝트를 블록의 자식으로 생성 (블록과 함께 움직임)
            GameObject flash = new GameObject("DestroyFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha); // 반투명 흰색

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
        /// 블룸(Bloom) 레이어: 메인 섬광 뒤에서 은은하게 퍼지는 큰 빛 효과.
        /// 메인 플래시보다 약간 늦게 시작하고, 더 크고 부드러운 빛을 보여줍니다.
        /// (비유: 폭발 뒤에 남는 은은한 잔광 — 사진에서 역광 효과처럼)
        /// </summary>
        /// <param name="pos">블룸 중심 위치</param>
        /// <param name="color">블룸 색상</param>
        /// <param name="initSize">초기 크기</param>
        /// <param name="duration">전체 지속 시간</param>
        private IEnumerator BloomLayer(Vector3 pos, Color color, float initSize, float duration)
        {
            // 메인 플래시보다 살짝 늦게 시작 (시차를 두어 깊이감 연출)
            yield return new WaitForSeconds(VisualConstants.BloomLag);

            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 오브젝트를 맨 뒤(FirstSibling)에 배치 → 다른 이펙트 뒤에 그려짐
            GameObject bloom = new GameObject("BloomLayer");
            bloom.transform.SetParent(parent, false);
            bloom.transform.position = pos;
            bloom.transform.SetAsFirstSibling();

            var img = bloom.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier);

            RectTransform rt = bloom.GetComponent<RectTransform>();
            float bloomSize = initSize * VisualConstants.BloomSizeMultiplier; // 메인 플래시보다 큼
            rt.sizeDelta = new Vector2(bloomSize, bloomSize);

            // 커지면서 투명해짐
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

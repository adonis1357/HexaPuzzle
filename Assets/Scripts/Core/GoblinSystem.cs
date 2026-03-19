// ============================================================================
// GoblinSystem.cs - 고블린 적군 시스템
// ============================================================================
// 고블린은 그리드 상단 빈 공간 3줄에서 포털을 통해 소환되며,
// 매 턴 블록이 있는 방향을 우선하여 이동한다.
// 이동 전 블록을 공격하여 파괴한 뒤 해당 위치로 이동.
// 블록 낙하 시 고블린에게 충돌 데미지를 주어 제거하는 메카닉.
//
// EnemySystem과 별개의 독립 시스템 (고블린은 블록 오버레이가 아닌 독립 엔티티)
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 고블린 스테이지 설정 데이터
    /// </summary>
    [System.Serializable]
    public class GoblinStageConfig
    {
        public int minSpawnPerTurn;    // 턴당 최소 소환 수
        public int maxSpawnPerTurn;    // 턴당 최대 소환 수
        public int missionKillCount;   // 미션 목표 제거 수
        public int maxOnBoard = 8;     // 보드 최대 동시 존재 수

        // 궁수/갑옷/방패 고블린 비율 (0.0 ~ 1.0, 나머지는 기본 몽둥이)
        public float archerRatio = 0f;   // 궁수 고블린 소환 확률
        public float armoredRatio = 0f;  // 갑옷 고블린 소환 확률
        public float shieldRatio = 0f;   // 방패 고블린 소환 확률
        // 궁수/갑옷/방패 HP
        public int archerHp = 1;         // 궁수 HP=1 (최상단줄 고정, 이동불가, 낙하면역)
        public int armoredHp = 15;       // 갑옷 HP (기본보다 높음, 방어형)
        public int shieldGoblinHp = 6;   // 방패 고블린 HP (기본과 동일)
        public int shieldHp = 3;         // 방패 내구도 (드릴 3회 차단 후 파괴)
    }

    /// <summary>
    /// 고블린 개체 데이터
    /// </summary>
    public class GoblinData
    {
        public HexCoord position;       // 현재 위치 (그리드 외부 좌표 포함)
        public int hp = 6;              // 현재 체력
        public int maxHp = 6;           // 최대 체력
        public GameObject visualObject; // 비주얼 GameObject
        public Image goblinImage;       // 고블린 스프라이트
        public Image hpBarBg;           // HP바 배경
        public Image hpBarFill;         // HP바 채움
        public Text hpText;            // HP 숫자 텍스트
        public bool isAlive = true;     // 생존 여부
        public bool isFlashing = false; // DamageFlashAnimation 진행 중 (중복 방지)
        public bool isArcher = false;   // 궁수 고블린 여부
        public bool isArmored = false;  // 갑옷 고블린 여부
        public bool isShieldType = false;      // 방패 고블린 타입 (파괴 후에도 유지, 킬 추적용)
        public bool isShielded = false;        // 현재 방패 활성 여부
        public int shieldHp = 3;              // 방패 내구도
        public int shieldTurnCounter = 0;     // 2턴 행동 카운터
        public List<GameObject> stuckDrills = null;  // 박힌 드릴 비주얼
        public Image shieldImage;             // 방패 Image
        public Text shieldHpText;             // 방패 HP 숫자 텍스트

        // 데미지 팝업 누적 표시용
        public float lastDamageTime = -1f;           // 마지막 데미지 시간
        public int accumulatedDamage = 0;            // 누적 데미지 (짧은 시간 내)
        public GameObject activeDamagePopup;         // 현재 활성 데미지 팝업
        public Text activeDamageText;                // 팝업 텍스트 참조
    }

    /// <summary>
    /// 고블린 적군 시스템 - 독립 엔티티 기반
    /// 그리드 상단에서 소환, 아래로 이동, 블록 공격, 낙하 충돌로 제거
    /// </summary>
    public class GoblinSystem : MonoBehaviour
    {
        // ============================================================
        // 싱글톤
        // ============================================================
        public static GoblinSystem Instance { get; private set; }

        // ============================================================
        // 참조
        // ============================================================
        private HexGrid hexGrid;
        private GoblinStageConfig currentConfig;
        private List<GoblinData> goblins = new List<GoblinData>();
        private int totalKills = 0;
        private int totalSpawned = 0; // 총 소환된 고블린 수 (미션 목표까지만 소환)

        // ★ 타입별 킬 카운터 (미션 초과 소환 방지용)
        private int regularKills = 0;
        private int armoredKills = 0;
        private int archerKills = 0;
        private int shieldKills = 0;

        // ★ 타입별 소환 카운터 (kills+alive 대신 확실한 소환 수 추적)
        private int regularSpawned = 0;
        private int armoredSpawned = 0;
        private int archerSpawned = 0;
        private int shieldSpawned = 0;

        // 낙하 충돌 대미지로 사망한 고블린 대기열 (ProcessFalling 완료 후 처리)
        private List<GoblinData> pendingFallDeaths = new List<GoblinData>();

        // 프로시저럴 스프라이트 캐시
        private static Sprite goblinSprite;
        private static Sprite archerGoblinSprite;
        private static Sprite armoredGoblinSprite;
        private static Sprite shieldGoblinSprite;
        private static Sprite shieldSprite;
        private static Sprite stuckDrillSprite;
        private static Sprite portalSprite;

        // 아래쪽 3방향 (flat-top hex: 화면 아래로 이동)
        private static readonly HexCoord[] DownDirections = new HexCoord[]
        {
            new HexCoord(1, 0),    // 오른쪽 아래
            new HexCoord(0, 1),    // 바로 아래
            new HexCoord(-1, 1)    // 왼쪽 아래
        };

        // 궁수 고블린 공격 관리
        private int archerTurnCounter = 0; // 턴 카운터 (2턴마다 공격)

        // 화면 흔들림 중첩 관리 (다른 시스템과 동일한 패턴)
        private int shakeCount = 0;
        private Vector3 shakeOriginalPos;

        // ============================================================
        // 이벤트
        // ============================================================
        /// <summary>고블린 제거 시 호출 (totalKills, isArmored, isArcher, isShieldType)</summary>
        public event System.Action<int, bool, bool, bool> OnGoblinKilled;

        /// <summary>현재 보드 위 고블린 수</summary>
        public int AliveCount => goblins.Count(g => g.isAlive);

        /// <summary>총 제거 수</summary>
        public int TotalKills => totalKills;

        /// <summary>미션 완료 시 true → 추가 소환 중단 (이동/공격은 계속)</summary>
        public bool MissionComplete { get; set; } = false;

        /// <summary>
        /// 고블린 사망 시 타입별 킬 카운터 증가 (내부 헬퍼)
        /// </summary>
        private void IncrementTypeKill(GoblinData goblin)
        {
            if (goblin.isArcher) archerKills++;
            else if (goblin.isArmored) armoredKills++;
            else if (goblin.isShieldType) shieldKills++;
            else regularKills++;
        }

        /// <summary>
        /// StageManager에서 특정 고블린 타입의 미션 목표 수를 조회.
        /// 해당 타입 미션이 없으면 0 반환 (→ 소환하지 않음).
        /// </summary>
        private int GetMissionTargetForType(bool isArmored, bool isArcher, bool isShieldType = false)
        {
            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[GoblinSystem] GetMissionTargetForType: GameManager.Instance == null");
                return 0;
            }
            var stageManager = GameManager.Instance.StageManagerRef;
            if (stageManager == null)
            {
                Debug.LogWarning("[GoblinSystem] GetMissionTargetForType: StageManagerRef == null");
                return 0;
            }

            var missions = stageManager.GetMissionProgress();
            if (missions == null || missions.Length == 0)
            {
                Debug.LogWarning($"[GoblinSystem] GetMissionTargetForType: missions null 또는 빈 배열 (length={missions?.Length})");
                // ★ 폴백: missionKillCount 기반 (미션 데이터 로드 실패 시)
                if (currentConfig != null && !isArmored && !isArcher && !isShieldType)
                    return currentConfig.missionKillCount;
                return 0;
            }

            EnemyType targetType;
            if (isArcher) targetType = EnemyType.ArcherGoblin;
            else if (isArmored) targetType = EnemyType.ArmoredGoblin;
            else if (isShieldType) targetType = EnemyType.ShieldGoblin;
            else targetType = EnemyType.Goblin;

            foreach (var mp in missions)
            {
                if (mp.mission.type == MissionType.RemoveEnemy &&
                    mp.mission.targetEnemyType == targetType)
                    return mp.mission.targetCount;
            }
            return 0; // 이 타입의 미션 없음
        }

        /// <summary>
        /// 모든 RemoveEnemy 미션의 targetCount 합산.
        /// 미션 기반 소환 하드캡 계산용.
        /// </summary>
        private int GetTotalMissionTarget()
        {
            if (GameManager.Instance == null) return 0;
            var stageManager = GameManager.Instance.StageManagerRef;
            if (stageManager == null) return 0;

            var missions = stageManager.GetMissionProgress();
            if (missions == null) return 0;

            int total = 0;
            foreach (var mp in missions)
            {
                if (mp.mission.type == MissionType.RemoveEnemy)
                    total += mp.mission.targetCount;
            }
            return total;
        }

        // ============================================================
        // 초기화
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 스테이지 시작 시 초기화
        /// </summary>
        public void Initialize(HexGrid grid, GoblinStageConfig config)
        {
            hexGrid = grid;
            currentConfig = config;
            totalKills = 0;
            totalSpawned = 0;
            regularKills = 0;
            armoredKills = 0;
            archerKills = 0;
            shieldKills = 0;
            regularSpawned = 0;
            armoredSpawned = 0;
            archerSpawned = 0;
            shieldSpawned = 0;
            MissionComplete = false;

            // 기존 고블린 정리
            CleanupAll();

            Debug.Log($"[GoblinSystem] 초기화: 소환 {config.minSpawnPerTurn}~{config.maxSpawnPerTurn}/턴, " +
                      $"목표 {config.missionKillCount}킬, 최대 {config.maxOnBoard}마리");
        }

        /// <summary>
        /// 모든 고블린 정리
        /// </summary>
        public void CleanupAll()
        {
            foreach (var goblin in goblins)
            {
                if (goblin.visualObject != null)
                    Destroy(goblin.visualObject);
            }
            goblins.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ============================================================
        // 턴 처리 메인 루프
        // ============================================================

        /// <summary>
        /// 매 턴 호출: 이동(공격 포함) → 소환
        /// GameManager에서 캐스케이드 완료 후 호출
        /// 고블린은 블록이 있는 방향을 우선 선택 → 공격 후 파괴 → 이동
        /// </summary>
        public IEnumerator ProcessTurn()
        {
            if (currentConfig == null || hexGrid == null) yield break;

            // 1단계: 기존 근접 고블린 이동 (블록 공격+파괴 통합) — 궁수는 이동하지 않음
            yield return StartCoroutine(MoveAllGoblins());

            // ★ 이동 후 위치 겹침 검증 + 해소
            ResolveOverlappingPositions();

            // 2단계: 궁수 고블린 공격 (2턴마다)
            archerTurnCounter++;
            if (archerTurnCounter >= 2)
            {
                archerTurnCounter = 0;
                yield return StartCoroutine(ArcherAttackPhase());
            }
            else
            {
                // 대기 턴: 궁수 대기 모션
                yield return StartCoroutine(ArcherIdleAnimation());
            }

            // 3단계: 새 고블린 소환
            yield return StartCoroutine(SpawnGoblins());

            // ★ 스폰 후 위치 겹침 검증 + 해소
            ResolveOverlappingPositions();

            // 4단계: 크랙/쉘 블록 엣지 연결 체크
            yield return StartCoroutine(CheckAndApplyEdgeConnection());
        }

        /// <summary>
        /// 모든 생존 고블린의 위치 겹침을 검사하고, 겹치는 고블린을 인접 빈 칸으로 강제 이동.
        /// 스폰/이동 후 안전장치로 호출.
        /// </summary>
        private void ResolveOverlappingPositions()
        {
            var aliveGoblins = goblins.Where(g => g.isAlive).ToList();
            if (aliveGoblins.Count <= 1) return;

            // 좌표별 고블린 그룹화
            var positionMap = new Dictionary<HexCoord, List<GoblinData>>();
            foreach (var g in aliveGoblins)
            {
                if (!positionMap.ContainsKey(g.position))
                    positionMap[g.position] = new List<GoblinData>();
                positionMap[g.position].Add(g);
            }

            // 점유 좌표 집합 (겹침 해소 중 업데이트)
            var occupiedCoords = new HashSet<HexCoord>(positionMap.Keys);

            foreach (var kvp in positionMap)
            {
                if (kvp.Value.Count <= 1) continue;

                // 첫 번째 고블린은 유지, 나머지를 빈 인접 칸으로 이동
                Debug.LogWarning($"[GoblinSystem] ⚠️ 위치 겹침 감지: {kvp.Key} 에 {kvp.Value.Count}마리");
                for (int i = 1; i < kvp.Value.Count; i++)
                {
                    var goblin = kvp.Value[i];
                    HexCoord newPos = FindNearestEmptyPosition(goblin.position, occupiedCoords);
                    if (newPos != goblin.position)
                    {
                        Debug.LogWarning($"[GoblinSystem] → 겹침 해소: ({goblin.position}) → ({newPos})");
                        goblin.position = newPos;
                        occupiedCoords.Add(newPos);

                        // 비주얼 위치도 즉시 갱신
                        if (goblin.visualObject != null && hexGrid != null)
                        {
                            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
                            if (rt != null)
                                rt.anchoredPosition = hexGrid.CalculateFlatTopHexPosition(newPos);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 주어진 좌표에서 가장 가까운 빈 위치를 찾음 (확장 영역 포함).
        /// BFS 방식으로 인접 칸 탐색.
        /// </summary>
        private HexCoord FindNearestEmptyPosition(HexCoord origin, HashSet<HexCoord> occupiedCoords)
        {
            int gridRadius = hexGrid.GridRadius;
            var visited = new HashSet<HexCoord> { origin };
            var queue = new Queue<HexCoord>();

            // 인접 6방향 시작
            foreach (var dir in HexCoord.Directions)
            {
                var next = origin + dir;
                if (!visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            while (queue.Count > 0)
            {
                var candidate = queue.Dequeue();

                // 유효 범위 확인 (그리드 + 확장 3줄)
                if (Mathf.Abs(candidate.q) > gridRadius) continue;
                int rMin = hexGrid.GetTopR(candidate.q);
                int rMax = Mathf.Min(gridRadius, -candidate.q + gridRadius);
                if (candidate.r < rMin - 3 || candidate.r > rMax) continue;

                // 비어있으면 사용
                if (!occupiedCoords.Contains(candidate))
                    return candidate;

                // 다음 탐색
                foreach (var dir in HexCoord.Directions)
                {
                    var next = candidate + dir;
                    if (!visited.Contains(next))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }

            return origin; // 빈 칸 없으면 원래 위치 유지
        }

        // ============================================================
        // 1단계: 고블린 이동
        // ============================================================

        private IEnumerator MoveAllGoblins()
        {
            // 궁수 고블린은 이동하지 않음 — 근접 고블린만 이동
            var aliveGoblins = goblins.Where(g => g.isAlive && !g.isArcher).ToList();
            if (aliveGoblins.Count == 0) yield break;

            // 방패 고블린 2턴 행동 체크: 홀수 턴에는 이동/공격 스킵
            var skippedShieldGoblins = new List<GoblinData>();
            foreach (var goblin in aliveGoblins)
            {
                if (goblin.isShielded)
                {
                    goblin.shieldTurnCounter++;
                    if (goblin.shieldTurnCounter % 2 != 0)
                        skippedShieldGoblins.Add(goblin);
                }
            }
            // 스킵 대상을 이동 목록에서 제외
            aliveGoblins = aliveGoblins.Where(g => !skippedShieldGoblins.Contains(g)).ToList();
            if (aliveGoblins.Count == 0) yield break;

            // ──────────────────────────────────────────────
            // 1단계: 블록이 있는 방향으로 공격 + 파괴 (동시)
            // ──────────────────────────────────────────────
            // ★★★ 아래쪽(r 큰) 고블린부터 순차 처리 → 뭉침 해소
            // 이동 결정된 고블린의 "새 위치"만 차단, "이전 위치"는 해제
            Dictionary<GoblinData, HexCoord> moveTargets = new Dictionary<GoblinData, HexCoord>();
            List<GoblinData> goblinsWithBlockTarget = new List<GoblinData>();
            Dictionary<GoblinData, HexBlock> attackTargetBlocks = new Dictionary<GoblinData, HexBlock>();

            // 이미 선점된 좌표 추적 (고블린끼리 같은 곳 이동 방지)
            // ★ 이동하지 않는 고블린(궁수, 스킵된 방패)의 위치를 미리 선점
            HashSet<HexCoord> reservedCoords = new HashSet<HexCoord>();
            foreach (var g in skippedShieldGoblins)
                reservedCoords.Add(g.position);
            // 궁수 고블린도 자리 선점 (이동 안 하므로)
            foreach (var g in goblins.Where(g => g.isAlive && g.isArcher))
                reservedCoords.Add(g.position);
            // 아직 이동 결정 안 된 고블린의 현재 위치도 일단 점유로 등록
            HashSet<HexCoord> pendingPositions = new HashSet<HexCoord>();
            foreach (var g in aliveGoblins)
                pendingPositions.Add(g.position);

            // ★ r이 큰(아래쪽) 고블린부터 처리 → 아래 고블린이 먼저 빠져야 위 고블린이 이동 가능
            var sortedGoblins = aliveGoblins.OrderByDescending(g => g.position.r).ToList();

            // 최대 2패스: 1패스에서 이동 못한 고블린을 2패스에서 재시도
            HashSet<GoblinData> resolved = new HashSet<GoblinData>();
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (var goblin in sortedGoblins)
                {
                    if (resolved.Contains(goblin)) continue;

                    var result = TryDecideMoveTarget(goblin, reservedCoords, pendingPositions);

                    if (result.found)
                    {
                        moveTargets[goblin] = result.target;
                        reservedCoords.Add(result.target);
                        pendingPositions.Remove(goblin.position); // 이전 위치 해제 → 위 고블린이 여기로 이동 가능
                        resolved.Add(goblin);

                        if (result.attackBlock != null)
                        {
                            goblinsWithBlockTarget.Add(goblin);
                            attackTargetBlocks[goblin] = result.attackBlock;
                        }
                    }
                }
            }

            // 이동 못한 고블린도 현재 위치 유지 (선점에서 자연 해제)


            // ──────────────────────────────────────────────
            // 2단계: 블록 공격 애니메이션 (동시 실행)
            // ──────────────────────────────────────────────
            if (goblinsWithBlockTarget.Count > 0)
            {
                List<Coroutine> attackCoroutines = new List<Coroutine>();
                foreach (var goblin in goblinsWithBlockTarget)
                {
                    HexBlock block = attackTargetBlocks[goblin];
                    HexCoord targetCoord = moveTargets[goblin];
                    attackCoroutines.Add(StartCoroutine(AnimateAttackAndDestroy(goblin, block, targetCoord)));
                }

                // 모든 공격 애니메이션 대기
                foreach (var co in attackCoroutines)
                    yield return co;
            }

            // ──────────────────────────────────────────────
            // 3단계: 모든 고블린 이동 (동시 실행)
            // 고블린은 공격 후 항상 타겟 위치로 이동
            // ──────────────────────────────────────────────
            List<Coroutine> moveCoroutines = new List<Coroutine>();
            // ★ 최종 위치 겹침 방지: 이동 적용 전 중복 좌표 검증
            HashSet<HexCoord> finalPositions = new HashSet<HexCoord>();
            foreach (var kvp in moveTargets)
            {
                var goblin = kvp.Key;
                var target = kvp.Value;

                if (finalPositions.Contains(target))
                {
                    // 위치 겹침 감지 → 이 고블린은 이동 취소 (현재 위치 유지)
                    Debug.LogWarning($"[GoblinSystem] 이동 겹침 감지! ({goblin.position}) → ({target}) 취소");
                    continue;
                }
                finalPositions.Add(target);

                goblin.position = target;
                Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(target);
                moveCoroutines.Add(StartCoroutine(AnimateGoblinMove(goblin, worldPos)));
            }

            foreach (var co in moveCoroutines)
                yield return co;

            // 안전장치: 모든 애니메이션 완료 후 hexGrid 위치 강제 리셋
            // MicroShake/ScreenShake가 완전히 끝나지 않았을 경우를 대비
            if (hexGrid != null)
            {
                hexGrid.transform.localPosition = Vector3.zero;
                shakeCount = 0;
            }
        }

        /// <summary>
        /// 고블린 이동 타겟 결정 헬퍼.
        /// reservedCoords: 이미 다른 고블린이 선점한 목적지.
        /// pendingPositions: 아직 이동 결정 안 된 고블린의 현재 위치 (점유 취급).
        /// </summary>
        private struct MoveDecision
        {
            public bool found;
            public HexCoord target;
            public HexBlock attackBlock;
        }

        private MoveDecision TryDecideMoveTarget(GoblinData goblin, HashSet<HexCoord> reservedCoords, HashSet<HexCoord> pendingPositions)
        {
            MoveDecision result = new MoveDecision { found = false, target = goblin.position, attackBlock = null };

            // 좌표가 점유 상태인지 체크 (선점 + 아직 이동 안 한 다른 고블린 위치)
            System.Func<HexCoord, bool> isOccupied = (coord) =>
            {
                if (reservedCoords.Contains(coord)) return true;
                // pendingPositions에 있되, 자기 자신의 위치는 제외
                if (pendingPositions.Contains(coord) && coord != goblin.position) return true;
                return false;
            };

            // 열 블록 기준 중앙 판정
            bool isAtColumnCenter = false;
            int gQ = goblin.position.q;
            if (Mathf.Abs(gQ) <= hexGrid.GridRadius)
            {
                int colRMin = hexGrid.GetTopR(gQ);
                int colRMax = Mathf.Min(hexGrid.GridRadius, -gQ + hexGrid.GridRadius);
                int colCenterR = (colRMin + colRMax) / 2;
                isAtColumnCenter = (goblin.position.r >= colCenterR);
            }

            HexCoord[] directionsToUse = isAtColumnCenter ? HexCoord.Directions : DownDirections;
            var shuffled = directionsToUse.OrderBy(x => Random.value).ToArray();

            // 1차: 공격 가능한 블록(안 깨진 블록) 방향 우선 탐색
            foreach (var dir in shuffled)
            {
                HexCoord target = goblin.position + dir;
                if (Mathf.Abs(target.q) > hexGrid.GridRadius) continue;

                int rMin = hexGrid.GetTopR(target.q);
                int rMax = Mathf.Min(hexGrid.GridRadius, -target.q + hexGrid.GridRadius);
                if (target.r < rMin || target.r > rMax) continue;

                if (isOccupied(target)) continue;

                HexBlock block = hexGrid.GetBlock(target);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None
                    && !block.Data.isShell && !block.Data.isCracked)
                {
                    result.found = true;
                    result.target = target;
                    result.attackBlock = block;
                    return result;
                }
            }

            // 2차: 공격 가능한 블록이 없을 때
            if (isAtColumnCenter)
            {
                var reshuffled = HexCoord.Directions.OrderBy(x => Random.value).ToArray();
                foreach (var dir in reshuffled)
                {
                    HexCoord target = goblin.position + dir;
                    if (Mathf.Abs(target.q) > hexGrid.GridRadius) continue;

                    int rMin = hexGrid.GetTopR(target.q);
                    int rMax = Mathf.Min(hexGrid.GridRadius, -target.q + hexGrid.GridRadius);
                    if (target.r < rMin || target.r > rMax) continue;

                    if (isOccupied(target)) continue;

                    HexBlock block = hexGrid.GetBlock(target);
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    {
                        result.found = true;
                        result.target = target;
                        if (!block.Data.isCracked && !block.Data.isShell)
                            result.attackBlock = block;
                        return result;
                    }
                }
            }
            else
            {
                // 중앙 아닐 때: 아래 3방향 빈 공간으로 이동 시도
                // ★ 바로 아래(0,1) 우선 → 막히면 대각선 아래 좌(-1,1)/우(1,0)로 우회
                HexCoord[] downDirs = {
                    new HexCoord(0, 1),   // 바로 아래 (최우선)
                    new HexCoord(-1, 1),  // 왼쪽 아래
                    new HexCoord(1, 0)    // 오른쪽 아래
                };
                // 대각선 방향은 랜덤 셔플 (좌/우 편향 방지)
                if (Random.value > 0.5f)
                {
                    var temp = downDirs[1];
                    downDirs[1] = downDirs[2];
                    downDirs[2] = temp;
                }

                foreach (var dir in downDirs)
                {
                    HexCoord downTarget = goblin.position + dir;
                    bool canMove = true;

                    if (Mathf.Abs(downTarget.q) > hexGrid.GridRadius) canMove = false;

                    if (canMove)
                    {
                        int rMin = hexGrid.GetTopR(downTarget.q);
                        int rMax = Mathf.Min(hexGrid.GridRadius, -downTarget.q + hexGrid.GridRadius);
                        if (downTarget.r < rMin - 3 || downTarget.r > rMax) canMove = false;
                    }

                    if (canMove && isOccupied(downTarget)) canMove = false;

                    if (canMove && hexGrid.IsInsideGrid(downTarget))
                    {
                        HexBlock block = hexGrid.GetBlock(downTarget);
                        if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                            canMove = false; // 블록 있는 곳은 스킵
                    }

                    if (canMove)
                    {
                        result.found = true;
                        result.target = downTarget;
                        return result;
                    }
                }
            }

            return result;
        }

        private IEnumerator AnimateGoblinMove(GoblinData goblin, Vector2 targetPos)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 startPos = rt.anchoredPosition;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            rt.anchoredPosition = targetPos;
        }

        // ============================================================
        // 블록 공격 + 파괴 (이동 전 실행)
        // ============================================================

        /// <summary>
        /// 이동 방향의 블록을 공격 → 파괴 → 이후 이동 가능하도록 처리
        /// 고블린이 블록 방향으로 돌진 → 타격 → 블록 파괴 연출 → 원위치 복귀
        /// </summary>
        private IEnumerator AnimateAttackAndDestroy(GoblinData goblin, HexBlock block, HexCoord targetCoord)
        {
            if (goblin.visualObject == null || block == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 origPos = rt.anchoredPosition;
            Vector2 targetWorldPos = hexGrid.CalculateFlatTopHexPosition(targetCoord);
            // 돌진 위치: 현재 위치에서 타겟 방향으로 40% 지점
            Vector2 lungePos = Vector2.Lerp(origPos, targetWorldPos, 0.4f);

            Vector3 origScale = rt.localScale;
            float elapsed;

            // 1. 몽둥이 올리기 (스케일 확대 + 회전)
            float dur1 = 0.12f;
            elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur1;
                rt.localScale = origScale * (1f + 0.25f * t);
                rt.localEulerAngles = new Vector3(0, 0, -18f * t);
                yield return null;
            }

            // 2. 타겟 방향으로 돌진 + 내려치기
            float dur2 = 0.08f;
            elapsed = 0f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur2);
                rt.anchoredPosition = Vector2.Lerp(origPos, lungePos, VisualConstants.EaseOutCubic(t));
                rt.localScale = origScale * (1.25f - 0.25f * t);
                rt.localEulerAngles = new Vector3(0, 0, -18f + 30f * t);
                yield return null;
            }

            // 3. 타격 순간: 특수 블록은 기능 해제+크랙, 일반 블록은 금간 → 껍데기(쉘) 처리
            if (block.Data != null && block.Data.specialType != SpecialBlockType.None
                && block.Data.specialType != SpecialBlockType.MoveBlock
                && block.Data.specialType != SpecialBlockType.FixedBlock)
            {
                // 특수 블록 (Drill, Bomb, Rainbow, XBlock, Drone 등): 발동 없이 기능만 제거 + 크랙
                // 색상은 유지, 특수 아이콘만 사라지고 일반 크랙 블록으로 전환
                var data = block.Data;
                var removedType = data.specialType;
                data.specialType = SpecialBlockType.None;
                data.isCracked = true;
                block.SetBlockData(data);
                Debug.Log($"[GoblinSystem] 공격→특수블록 기능 해제: ({goblin.position}) → ({targetCoord}) {removedType} → 일반 크랙 블록");
            }
            else if (block.Data != null && !block.Data.isCracked)
            {
                // 첫 타격: 블록에 금이 감 (완전 파괴 아님)
                var data = block.Data;
                data.isCracked = true;
                block.SetBlockData(data);
                Debug.Log($"[GoblinSystem] 공격→금간: ({goblin.position}) → ({targetCoord}) 블록에 금이 감");
            }
            else if (block.Data != null && block.Data.isCracked && !block.Data.isShell)
            {
                // 두 번째 타격: 가운데 깨지고 테두리만 남은 껍데기(쉘) 상태
                // 매칭 불가 + 낙하 장애물로 인식
                var data = block.Data;
                data.isShell = true;
                data.specialType = SpecialBlockType.None; // 특수 블록 해제
                data.tier = BlockTier.Normal;
                block.SetBlockData(data);
                Debug.Log($"[GoblinSystem] 공격→껍데기: ({goblin.position}) → ({targetCoord}) 테두리만 남은 장애물");
            }
            else
            {
                // 이미 껍데기 블록: 완전 파괴
                block.ClearData();
                Debug.Log($"[GoblinSystem] 공격→완전파괴: ({goblin.position}) → ({targetCoord}) 껍데기 블록 파괴");
            }

            // 타격 이펙트 (블록 위치에 플래시)
            StartCoroutine(BlockDestroyFlash(targetWorldPos));

            // 화면 미세 흔들림 (shakeCount 패턴 적용)
            if (hexGrid != null)
                StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));

            // 4. 원위치 복귀
            float dur3 = 0.12f;
            elapsed = 0f;
            while (elapsed < dur3)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / dur3));
                rt.anchoredPosition = Vector2.Lerp(lungePos, origPos, t);
                rt.localScale = Vector3.Lerp(origScale * 1.0f, origScale, t);
                rt.localEulerAngles = Vector3.Lerp(new Vector3(0, 0, 12f), Vector3.zero, t);
                yield return null;
            }

            rt.anchoredPosition = origPos;
            rt.localScale = origScale;
            rt.localEulerAngles = Vector3.zero;
        }

        /// <summary>
        /// 블록 파괴 시 해당 위치에 파괴 플래시 이펙트
        /// </summary>
        private IEnumerator BlockDestroyFlash(Vector2 position)
        {
            Transform parent = hexGrid.GridContainer;

            GameObject flash = new GameObject("GoblinDestroyFlash");
            flash.transform.SetParent(parent, false);

            RectTransform frt = flash.AddComponent<RectTransform>();
            frt.anchoredPosition = position;
            float size = hexGrid.HexSize * 1.8f;
            frt.sizeDelta = new Vector2(size, size);

            Image fimg = flash.AddComponent<Image>();
            fimg.sprite = HexBlock.GetHexFlashSprite();
            fimg.color = new Color(1f, 0.6f, 0.2f, 0.9f); // 주황색 플래시
            fimg.raycastTarget = false;

            // 확장 + 페이드아웃
            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + 0.4f * VisualConstants.EaseOutCubic(t);
                frt.localScale = Vector3.one * scale;
                fimg.color = new Color(1f, 0.6f, 0.2f, 0.9f * (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 화면 흔들림 코루틴 (다른 시스템과 동일한 shakeCount 패턴 적용).
        /// Bug fix: 기존 MicroShake는 여러 고블린이 동시에 공격할 때
        /// 각각 orig 위치를 잘못 캡처하여 hexGrid.transform.localPosition이
        /// 영구적으로 어긋나는 문제가 있었음.
        /// </summary>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            // 다수 특수 블록 동시 발동 시 필드 바운스는 하나만 실행
            bool isOwner = VisualConstants.TryBeginScreenShake();
            if (!isOwner) yield break;

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
            VisualConstants.EndScreenShake();
        }

        // ============================================================
        // 3단계: 고블린 소환
        // ============================================================

        private IEnumerator SpawnGoblins()
        {
            if (currentConfig == null)
            {
                Debug.LogWarning("[GoblinSystem] SpawnGoblins 취소: currentConfig == null");
                yield break;
            }

            // 미션 완료 시 추가 소환 중단
            if (MissionComplete)
            {
                Debug.Log("[GoblinSystem] SpawnGoblins 취소: MissionComplete");
                yield break;
            }

            // 보드 최대 동시 존재 수 제한 (maxOnBoard 이하일 때만 소환)
            int aliveCount = AliveCount;
            int boardSlots = currentConfig.maxOnBoard - aliveCount;
            if (boardSlots <= 0) yield break;

            // ★ 타입별 미션 목표 조회 (StageManager 연동)
            int regularTarget = GetMissionTargetForType(false, false);
            int armoredTarget = GetMissionTargetForType(true, false);
            int archerTarget = GetMissionTargetForType(false, true);
            int shieldTarget = GetMissionTargetForType(false, false, true);

            // ★★★ 핵심 수정: 소환 카운터 기반 잔여 계산 (kills+alive 대신 spawned 사용)
            // kills+alive는 타이밍에 따라 totalSpawned와 불일치 가능 → 초과 소환 원인
            int regularRemaining = Mathf.Max(0, regularTarget - regularSpawned);
            int armoredRemaining = Mathf.Max(0, armoredTarget - armoredSpawned);
            int archerRemaining = Mathf.Max(0, archerTarget - archerSpawned);
            int shieldRemaining = Mathf.Max(0, shieldTarget - shieldSpawned);
            int totalRemaining = regularRemaining + armoredRemaining + archerRemaining + shieldRemaining;

            // ★ 미션 기반 하드캡: totalSpawned 기반 (확실한 소환 수 추적)
            int totalMissionTarget = GetTotalMissionTarget();
            if (totalMissionTarget > 0)
            {
                // 미션 목표 합 - 이미 소환한 수 = 앞으로 소환 가능한 최대
                int hardCap = Mathf.Max(0, totalMissionTarget - totalSpawned);
                totalRemaining = Mathf.Min(totalRemaining, hardCap);
            }
            else if (totalRemaining <= 0)
            {
                // RemoveEnemy 미션이 없는 스테이지: missionKillCount 폴백
                totalRemaining = Mathf.Max(0, currentConfig.missionKillCount - totalSpawned);
                regularRemaining = totalRemaining;
            }
            if (totalRemaining <= 0) yield break;

            Debug.Log($"[GoblinSystem] 소환 제한: 미션합={totalMissionTarget}, 총소환={totalSpawned}, " +
                      $"기본({regularSpawned}/{regularTarget}) 갑옷({armoredSpawned}/{armoredTarget}) " +
                      $"활({archerSpawned}/{archerTarget}) 방패({shieldSpawned}/{shieldTarget})");

            int spawnCount = Random.Range(currentConfig.minSpawnPerTurn, currentConfig.maxSpawnPerTurn + 1);
            spawnCount = Mathf.Min(spawnCount, boardSlots);        // 보드 최대 수 초과 방지
            spawnCount = Mathf.Min(spawnCount, totalRemaining);    // 미션 초과 소환 방지

            if (spawnCount <= 0) yield break;

            Debug.Log($"[GoblinSystem] 소환 계획: {spawnCount}마리 (몽둥이잔여={regularRemaining}, 갑옷잔여={armoredRemaining}, 활잔여={archerRemaining}, 방패잔여={shieldRemaining})");

            // 소환 가능 위치 분류: 궁수는 가장 윗줄(row=3)만, 근접/갑옷은 아래 2줄
            var allExtended = hexGrid.GetExtendedTopCoords();
            var occupiedPositions = new HashSet<HexCoord>(goblins.Where(g => g.isAlive).Select(g => g.position));

            // 좌표 분류: 최상단(궁수), 중간+하단(일반/갑옷), 하단만(방패)
            var topRowCoords = new List<HexCoord>();    // rMin - 3: 궁수 전용
            var lowerRowCoords = new List<HexCoord>();  // rMin - 2, rMin - 1: 일반/갑옷
            var bottomRowCoords = new List<HexCoord>(); // rMin - 1만: 방패 전용
            int gridRadius = hexGrid.GridRadius;
            for (int q = -gridRadius; q <= gridRadius; q++)
            {
                int rMin = hexGrid.GetTopR(q);
                var topCoord = new HexCoord(q, rMin - 3);
                if (!occupiedPositions.Contains(topCoord))
                    topRowCoords.Add(topCoord);
                var mid = new HexCoord(q, rMin - 2);
                var bot = new HexCoord(q, rMin - 1);
                if (!occupiedPositions.Contains(mid)) lowerRowCoords.Add(mid);
                if (!occupiedPositions.Contains(bot))
                {
                    lowerRowCoords.Add(bot);
                    bottomRowCoords.Add(bot); // 하단줄은 방패용으로도 등록
                }
            }

            // 소환 목록 생성: 먼저 타입 결정, 그 다음 위치 배정
            List<Coroutine> spawnCoroutines = new List<Coroutine>();
            var shuffledTop = topRowCoords.OrderBy(x => Random.value).ToList();
            var shuffledLower = lowerRowCoords.OrderBy(x => Random.value).ToList();
            var shuffledBottom = bottomRowCoords.OrderBy(x => Random.value).ToList();

            // ★ 위치 겹침 방지: 소환할 때마다 사용된 좌표를 기록
            var usedPositions = new HashSet<HexCoord>(occupiedPositions);

            for (int i = 0; i < spawnCount; i++)
            {
                // ★★★ 미션 잔여량 기반 타입 결정 (ratio 무시, 미션에 없는 타입은 절대 소환 안 됨)
                bool isArcher = false;
                bool isArmored = false;
                bool isShieldType = false;
                int goblinHp = 6;
                int spawnShieldHp = currentConfig.shieldHp;

                // 사용 가능한 슬롯 수 계산 (usedPositions 기반)
                int availableTop = 0;
                foreach (var c in shuffledTop) { if (!usedPositions.Contains(c)) availableTop++; }
                int availableBottom = 0;
                foreach (var c in shuffledBottom) { if (!usedPositions.Contains(c)) availableBottom++; }
                int availableLower = 0;
                foreach (var c in shuffledLower) { if (!usedPositions.Contains(c)) availableLower++; }

                // 잔여량 합계로 가중 랜덤 → 미션 비율 정확 매칭
                int totalPool = regularRemaining + armoredRemaining + archerRemaining + shieldRemaining;
                if (totalPool <= 0) break; // 미션 수량 모두 소환 완료

                float roll = Random.value * totalPool;
                float threshold = 0f;

                // 방패 → 갑옷 → 궁수 → 기본 순서로 체크 (빈 슬롯 필요 조건 포함)
                threshold += shieldRemaining;
                if (roll < threshold && shieldRemaining > 0 && availableBottom > 0)
                {
                    isShieldType = true;
                    goblinHp = currentConfig.shieldGoblinHp;
                    shieldRemaining--;
                }
                else
                {
                    threshold += armoredRemaining;
                    if (roll < threshold && armoredRemaining > 0 && (availableLower > 0 || availableTop > 0))
                    {
                        isArmored = true;
                        goblinHp = currentConfig.armoredHp;
                        armoredRemaining--;
                    }
                    else
                    {
                        threshold += archerRemaining;
                        if (roll < threshold && archerRemaining > 0 && availableTop > 0)
                        {
                            isArcher = true;
                            goblinHp = currentConfig.archerHp;
                            archerRemaining--;
                        }
                        else if (regularRemaining > 0 && (availableLower > 0 || availableTop > 0))
                        {
                            regularRemaining--;
                        }
                        else
                        {
                            // 위치 부족으로 특정 타입 스킵된 경우, 남은 타입 중 소환 가능한 것 선택
                            if (shieldRemaining > 0 && availableBottom > 0)
                            {
                                isShieldType = true;
                                goblinHp = currentConfig.shieldGoblinHp;
                                shieldRemaining--;
                            }
                            else if (armoredRemaining > 0 && (availableLower > 0 || availableTop > 0))
                            {
                                isArmored = true;
                                goblinHp = currentConfig.armoredHp;
                                armoredRemaining--;
                            }
                            else if (archerRemaining > 0 && availableTop > 0)
                            {
                                isArcher = true;
                                goblinHp = currentConfig.archerHp;
                                archerRemaining--;
                            }
                            else
                            {
                                continue; // 전체 소환 불가
                            }
                        }
                    }
                }

                // 위치 배정: 궁수→최상단, 방패→최하단, 나머지→중간+하단
                // ★ usedPositions로 겹침 완전 방지
                HexCoord coord = default;
                bool foundCoord = false;

                if (isArcher)
                {
                    foreach (var c in shuffledTop)
                    {
                        if (!usedPositions.Contains(c)) { coord = c; foundCoord = true; break; }
                    }
                    if (!foundCoord) continue;
                }
                else if (isShieldType)
                {
                    foreach (var c in shuffledBottom)
                    {
                        if (!usedPositions.Contains(c)) { coord = c; foundCoord = true; break; }
                    }
                    if (!foundCoord) continue;
                }
                else
                {
                    // 일반/갑옷: 중간+하단 우선, 없으면 최상단
                    foreach (var c in shuffledLower)
                    {
                        if (!usedPositions.Contains(c)) { coord = c; foundCoord = true; break; }
                    }
                    if (!foundCoord)
                    {
                        foreach (var c in shuffledTop)
                        {
                            if (!usedPositions.Contains(c)) { coord = c; foundCoord = true; break; }
                        }
                    }
                    if (!foundCoord) continue;
                }

                // ★ 사용된 좌표 즉시 등록 → 다음 고블린과 절대 겹치지 않음
                usedPositions.Add(coord);

                GoblinData newGoblin = new GoblinData
                {
                    position = coord,
                    hp = goblinHp,
                    maxHp = goblinHp,
                    isAlive = true,
                    isArcher = isArcher,
                    isArmored = isArmored,
                    isShieldType = isShieldType,
                    isShielded = isShieldType,
                    shieldHp = isShieldType ? spawnShieldHp : 0,
                    shieldTurnCounter = 0,
                    stuckDrills = isShieldType ? new List<GameObject>() : null
                };

                goblins.Add(newGoblin);
                totalSpawned++;
                // ★ 타입별 소환 카운터 증가 (미션 초과 소환 방지의 핵심)
                if (isArcher) archerSpawned++;
                else if (isArmored) armoredSpawned++;
                else if (isShieldType) shieldSpawned++;
                else regularSpawned++;
                string typeStr = isArcher ? "궁수" : isArmored ? "갑옷" : isShieldType ? "방패" : "기본";
                Debug.Log($"[GoblinSystem] {typeStr} 고블린 소환 at ({coord}), HP={goblinHp}, 누적소환={totalSpawned}");
                spawnCoroutines.Add(StartCoroutine(SpawnSingleGoblin(newGoblin)));
            }

            foreach (var co in spawnCoroutines)
                yield return co;

            int totalMT = GetTotalMissionTarget();
            Debug.Log($"[GoblinSystem] {spawnCoroutines.Count}마리 소환 완료 | 보드 {AliveCount}/{currentConfig.maxOnBoard} | " +
                      $"누적소환 {totalSpawned}/{totalMT} (기본{regularSpawned} 갑옷{armoredSpawned} 활{archerSpawned} 방패{shieldSpawned})");
        }

        private IEnumerator SpawnSingleGoblin(GoblinData goblin)
        {
            Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(goblin.position);

            // 포털 이펙트 생성
            yield return StartCoroutine(PortalSpawnEffect(worldPos));

            // 고블린 비주얼 생성
            CreateGoblinVisual(goblin, worldPos);

            // 등장 포효 모션
            yield return StartCoroutine(RoarAnimation(goblin));
        }

        // ============================================================
        // 포털 소환 이펙트
        // ============================================================

        private IEnumerator PortalSpawnEffect(Vector2 position)
        {
            // 시공간 수축 도플러 사운드 재생
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayEnemySpawnSound();

            Transform parent = hexGrid.GridContainer;

            // 포털 오브젝트 생성
            GameObject portal = new GameObject("GoblinPortal");
            portal.transform.SetParent(parent, false);

            RectTransform prt = portal.AddComponent<RectTransform>();
            prt.anchoredPosition = position;
            float portalSize = hexGrid.HexSize * 2f;
            prt.sizeDelta = new Vector2(portalSize, portalSize * 0.6f); // 타원형
            prt.localScale = Vector3.zero;

            Image pimg = portal.AddComponent<Image>();
            if (portalSprite == null)
                portalSprite = CreatePortalSprite(128);
            pimg.sprite = portalSprite;
            pimg.color = new Color(0.4f, 0.2f, 0.9f, 0.8f);
            pimg.raycastTarget = false;

            // 포털 가장자리 회전 링 (장식용)
            GameObject ring = new GameObject("PortalRing");
            ring.transform.SetParent(portal.transform, false);
            RectTransform rrt = ring.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0.5f);
            rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(portalSize * 1.2f, portalSize * 0.72f);
            Image rimg = ring.AddComponent<Image>();
            rimg.sprite = portalSprite;
            rimg.color = new Color(0.6f, 0.3f, 1f, 0.4f);
            rimg.raycastTarget = false;

            // 1단계: 포털 등장 (0→full 스케일, 0.3초)
            float dur1 = 0.3f;
            float elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / dur1));
                prt.localScale = Vector3.one * t;
                // 링 회전
                rrt.localEulerAngles = new Vector3(0, 0, elapsed * 360f);
                yield return null;
            }
            prt.localScale = Vector3.one;

            // 2단계: 유지 + 링 회전 (0.3초)
            elapsed = 0f;
            float dur2 = 0.3f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                rrt.localEulerAngles = new Vector3(0, 0, (dur1 + elapsed) * 360f);
                // 포털 펄스 효과
                float pulse = 1f + 0.05f * Mathf.Sin(elapsed * Mathf.PI * 6f);
                prt.localScale = Vector3.one * pulse;
                yield return null;
            }

            // 3단계: 포털 축소 → 사라짐 (0.2초)
            float dur3 = 0.2f;
            elapsed = 0f;
            while (elapsed < dur3)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / dur3);
                prt.localScale = Vector3.one * t;
                pimg.color = new Color(0.4f, 0.2f, 0.9f, 0.8f * t);
                rrt.localEulerAngles = new Vector3(0, 0, (dur1 + dur2 + elapsed) * 360f);
                yield return null;
            }

            Destroy(portal);
        }

        // ============================================================
        // 고블린 비주얼 생성
        // ============================================================

        private void CreateGoblinVisual(GoblinData goblin, Vector2 position)
        {
            Transform parent = hexGrid.GridContainer;

            // 메인 오브젝트
            GameObject obj = new GameObject("Goblin");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            float size = hexGrid.HexSize * 1.6f;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.zero; // 소환 시 0에서 시작

            // 고블린 타입별 스프라이트 선택
            Sprite selectedSprite;
            if (goblin.isArcher)
            {
                if (archerGoblinSprite == null)
                    archerGoblinSprite = CreateArcherGoblinSprite(256);
                selectedSprite = archerGoblinSprite;
            }
            else if (goblin.isArmored)
            {
                if (armoredGoblinSprite == null)
                    armoredGoblinSprite = CreateArmoredGoblinSprite(256);
                selectedSprite = armoredGoblinSprite;
            }
            else if (goblin.isShieldType)
            {
                if (shieldGoblinSprite == null)
                    shieldGoblinSprite = CreateShieldGoblinSprite(256);
                selectedSprite = shieldGoblinSprite;
            }
            else
            {
                if (goblinSprite == null)
                    goblinSprite = CreateGoblinSprite(256);
                selectedSprite = goblinSprite;
            }

            Image img = obj.AddComponent<Image>();
            img.sprite = selectedSprite;
            img.color = Color.white;
            img.raycastTarget = false;

            goblin.visualObject = obj;
            goblin.goblinImage = img;

            // 방패 고블린: 방패 오버레이 추가
            if (goblin.isShielded)
            {
                if (shieldSprite == null)
                    shieldSprite = CreateShieldSprite(128);

                GameObject shieldObj = new GameObject("Shield");
                shieldObj.transform.SetParent(obj.transform, false);

                RectTransform shieldRt = shieldObj.AddComponent<RectTransform>();
                shieldRt.anchorMin = new Vector2(0.5f, 0.5f);
                shieldRt.anchorMax = new Vector2(0.5f, 0.5f);
                // 방패를 고블린 앞(아래쪽)에 배치
                shieldRt.anchoredPosition = new Vector2(0, -size * 0.18f);
                shieldRt.sizeDelta = new Vector2(size * 0.7f, size * 0.6f);

                Image shieldImg = shieldObj.AddComponent<Image>();
                shieldImg.sprite = shieldSprite;
                shieldImg.color = Color.white;
                shieldImg.raycastTarget = false;

                goblin.shieldImage = shieldImg;

                // 방패 HP 숫자 텍스트
                GameObject shieldHpObj = new GameObject("ShieldHPText");
                shieldHpObj.transform.SetParent(shieldObj.transform, false);

                RectTransform shpRt = shieldHpObj.AddComponent<RectTransform>();
                shpRt.anchorMin = new Vector2(0.5f, 0.5f);
                shpRt.anchorMax = new Vector2(0.5f, 0.5f);
                shpRt.anchoredPosition = Vector2.zero;
                shpRt.sizeDelta = new Vector2(size * 0.5f, size * 0.4f);

                Text shieldText = shieldHpObj.AddComponent<Text>();
                shieldText.text = $"{goblin.shieldHp}";
                shieldText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                shieldText.fontSize = Mathf.RoundToInt(size * 0.28f);
                shieldText.fontStyle = FontStyle.Bold;
                shieldText.alignment = TextAnchor.MiddleCenter;
                shieldText.color = new Color(0.2f, 0.15f, 0.05f, 1f); // 진한 금색/갈색
                shieldText.raycastTarget = false;

                // 외곽선 추가
                Outline shieldOutline = shieldHpObj.AddComponent<Outline>();
                shieldOutline.effectColor = new Color(1f, 1f, 1f, 0.8f);
                shieldOutline.effectDistance = new Vector2(1f, -1f);

                goblin.shieldHpText = shieldText;
            }

            // HP바 생성
            CreateHPBar(goblin, obj.transform, size);
        }

        private void CreateHPBar(GoblinData goblin, Transform parent, float goblinSize)
        {
            float barWidth = goblinSize * 0.6f;
            float barHeight = 6f;
            float offsetY = -goblinSize * 0.45f;

            // HP바 배경
            GameObject bgObj = new GameObject("HPBarBg");
            bgObj.transform.SetParent(parent, false);

            RectTransform bgRt = bgObj.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0.5f);
            bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = new Vector2(0, offsetY);
            bgRt.sizeDelta = new Vector2(barWidth, barHeight);

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            bgImg.raycastTarget = false;

            // HP바 채움
            GameObject fillObj = new GameObject("HPBarFill");
            fillObj.transform.SetParent(bgObj.transform, false);

            RectTransform fillRt = fillObj.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);

            // pivot을 왼쪽으로 설정하여 오른쪽에서 줄어들게
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);

            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.9f, 0.2f); // 초록색
            fillImg.raycastTarget = false;

            goblin.hpBarBg = bgImg;
            goblin.hpBarFill = fillImg;

            // HP 숫자 텍스트 (HP바 위에 표시)
            GameObject textObj = new GameObject("HPText");
            textObj.transform.SetParent(parent, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, offsetY - barHeight - 2f);
            textRt.sizeDelta = new Vector2(barWidth * 2f, 36f);

            Text hpTxt = textObj.AddComponent<Text>();
            hpTxt.text = $"{goblin.hp}";
            hpTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpTxt.fontSize = 27;
            hpTxt.alignment = TextAnchor.MiddleCenter;
            hpTxt.color = Color.white;
            hpTxt.raycastTarget = false;

            // 텍스트 외곽선 (가독성) - 1px 두께
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            goblin.hpText = hpTxt;
        }

        private void UpdateHPBar(GoblinData goblin)
        {
            if (goblin.hpBarFill == null) return;

            float hpRatio = (float)goblin.hp / goblin.maxHp;
            RectTransform fillRt = goblin.hpBarFill.GetComponent<RectTransform>();

            // 스케일 X로 HP 비율 표현
            fillRt.localScale = new Vector3(hpRatio, 1f, 1f);

            // 색상 변화: 녹색 → 노랑 → 빨강
            if (hpRatio > 0.5f)
            {
                float t = (hpRatio - 0.5f) / 0.5f;
                goblin.hpBarFill.color = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.9f, 0.2f), t);
            }
            else
            {
                float t = hpRatio / 0.5f;
                goblin.hpBarFill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.9f, 0.1f), t);
            }

            // HP 숫자 텍스트 업데이트
            if (goblin.hpText != null)
                goblin.hpText.text = $"{goblin.hp}";
        }

        // ============================================================
        // 포효(등장) 모션
        // ============================================================

        private IEnumerator RoarAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();

            // 1. 등장: 0 → 1 스케일 (0.25초)
            float dur1 = 0.25f;
            float elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / dur1));
                rt.localScale = Vector3.one * t;
                yield return null;
            }
            rt.localScale = Vector3.one;

            // 2. 포효: 1.0 → 1.3 → 1.0 (0.3초)
            float dur2 = 0.3f;
            elapsed = 0f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur2;
                float scale;
                if (t < 0.4f)
                {
                    scale = 1f + 0.3f * VisualConstants.EaseOutCubic(t / 0.4f);
                }
                else
                {
                    scale = 1.3f - 0.3f * VisualConstants.EaseOutBack((t - 0.4f) / 0.6f);
                }
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;

            // 3. 충격파 링 이펙트
            StartCoroutine(ShockwaveEffect(goblin));
        }

        private IEnumerator ShockwaveEffect(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            Transform parent = goblin.visualObject.transform.parent;
            Vector2 pos = goblin.visualObject.GetComponent<RectTransform>().anchoredPosition;

            GameObject wave = new GameObject("GoblinShockwave");
            wave.transform.SetParent(parent, false);

            RectTransform wrt = wave.AddComponent<RectTransform>();
            wrt.anchoredPosition = pos;
            wrt.sizeDelta = new Vector2(20f, 20f);

            Image wimg = wave.AddComponent<Image>();
            if (portalSprite == null)
                portalSprite = CreatePortalSprite(128);
            wimg.sprite = portalSprite;
            wimg.color = new Color(0.3f, 0.8f, 0.2f, 0.6f);
            wimg.raycastTarget = false;

            float duration = 0.3f;
            float elapsed = 0f;
            float maxSize = hexGrid.HexSize * 3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(20f, maxSize, VisualConstants.EaseOutCubic(t));
                wrt.sizeDelta = new Vector2(size, size);
                wimg.color = new Color(0.3f, 0.8f, 0.2f, 0.6f * (1f - t));
                yield return null;
            }

            Destroy(wave);
        }

        // ============================================================
        // 낙하 충돌 데미지
        // ============================================================

        // ★ ApplyFallDamage 제거됨 — 물리적 충돌 기반 ApplyIndividualFallDamage로 대체
        // 낙하 중 블록이 고블린 Y좌표를 통과할 때 AnimateFall에서 직접 호출

        /// <summary>
        /// 간단한 낙하 데미지 적용 (열 기반이 아닌 개별 좌표 기반)
        /// 특정 좌표에 블록이 낙하/정착했을 때 호출
        /// </summary>
        public void ApplyDamageAtPosition(HexCoord position, int damage = 1)
        {
            var goblin = goblins.FirstOrDefault(g => g.isAlive && g.position == position);
            if (goblin == null) return;

            // ★ 방패 활성 시: 특수 공격 대미지를 방패에 전달
            if (goblin.isShielded)
            {
                ApplyShieldDamage(goblin, 1);
                return;
            }

            goblin.hp -= damage;

            // ★ 즉시 HP바 반영 (코루틴 대기 없이)
            UpdateHPBar(goblin);

            // ★ 데미지 숫자 팝업 표시 (각 타격마다 개별 "-1")
            if (goblin.visualObject != null)
                SpawnDamagePopups(goblin, damage);

            // 피격 플래시 + 흔들림 애니메이션
            StartCoroutine(DamageFlashAnimation(goblin));

            Debug.Log($"[GoblinSystem] 위치 데미지: ({position}) -{damage} → HP {goblin.hp}/{goblin.maxHp}");

            if (goblin.hp <= 0)
            {
                goblin.hp = 0;
                goblin.isAlive = false;
                totalKills++;
                IncrementTypeKill(goblin);
                OnGoblinKilled?.Invoke(totalKills, goblin.isArmored, goblin.isArcher, goblin.isShieldType);
                StartCoroutine(DeathAnimation(goblin));
            }
        }

        /// <summary>
        /// 복수 좌표에서 발생한 데미지를 고블린별로 누적하여 일괄 적용.
        /// 몬스터가 서있는 블록 + 주변 블록이 동시에 삭제되면 해당 수량만큼 대미지.
        /// key=좌표, value=해당 좌표에 가할 데미지.
        /// directHitCoords: 고블린 자리에서 직접 매칭된 좌표 (바닥 매칭).
        /// </summary>
        public void ApplyBatchDamage(Dictionary<HexCoord, int> damageMap, HashSet<HexCoord> directHitCoords = null)
        {
            if (damageMap == null || damageMap.Count == 0) return;

            // 고블린별 누적 데미지 계산
            Dictionary<GoblinData, int> goblinDamage = new Dictionary<GoblinData, int>();
            foreach (var kvp in damageMap)
            {
                var goblin = goblins.FirstOrDefault(g => g.isAlive && g.position == kvp.Key);
                if (goblin == null) continue;

                if (goblinDamage.ContainsKey(goblin))
                    goblinDamage[goblin] += kvp.Value;
                else
                    goblinDamage[goblin] = kvp.Value;
            }

            // 누적된 대미지를 한번에 적용
            foreach (var kvp in goblinDamage)
            {
                GoblinData goblin = kvp.Key;
                int totalDamage = kvp.Value;

                // ★ 방패 활성 시: 바닥 매칭 또는 바로 아래 매칭 → 방패에 1 대미지, 나머지 차단
                if (goblin.isShielded)
                {
                    bool isDirectHit = directHitCoords != null && directHitCoords.Contains(goblin.position);

                    // ★ 방패 아래 방향(+r) 매칭 체크: 고블린 바로 아래 블록이 매칭되면 대미지
                    bool isBelowHit = false;
                    if (!isDirectHit && directHitCoords != null)
                    {
                        // flat-top hex 아래 3방향: (0,1), (-1,1), (1,0)
                        HexCoord[] belowDirs = {
                            new HexCoord(0, 1),
                            new HexCoord(-1, 1),
                            new HexCoord(1, 0)
                        };
                        foreach (var dir in belowDirs)
                        {
                            if (directHitCoords.Contains(goblin.position + dir))
                            {
                                isBelowHit = true;
                                break;
                            }
                        }
                    }

                    if (isDirectHit || isBelowHit)
                        ApplyShieldDamage(goblin, 1);
                    // 그 외 인접 대미지는 완전 차단
                    continue;
                }

                goblin.hp -= totalDamage;
                UpdateHPBar(goblin);

                // 각 타격마다 개별 "-1" 팝업 (대미지 수만큼)
                if (goblin.visualObject != null)
                    SpawnDamagePopups(goblin, totalDamage);

                StartCoroutine(DamageFlashAnimation(goblin));

                Debug.Log($"[GoblinSystem] 일괄 데미지: ({goblin.position}) -{totalDamage} → HP {goblin.hp}/{goblin.maxHp}");

                if (goblin.hp <= 0)
                {
                    goblin.hp = 0;
                    goblin.isAlive = false;
                    totalKills++;
                    IncrementTypeKill(goblin);
                    OnGoblinKilled?.Invoke(totalKills, goblin.isArmored, goblin.isArcher, goblin.isShieldType);
                    StartCoroutine(DeathAnimation(goblin));
                }
            }
        }

        /// <summary>
        /// 특정 좌표에 고블린이 있는지 확인
        /// </summary>
        public bool HasGoblinAt(HexCoord coord)
        {
            return goblins.Any(g => g.isAlive && g.position == coord);
        }

        /// <summary>
        /// 살아있는 모든 고블린 목록 반환
        /// </summary>
        public List<GoblinData> GetAliveGoblins()
        {
            return goblins.Where(g => g.isAlive).ToList();
        }

        /// <summary>
        /// 특정 열(q)에 있는 살아있는 고블린 목록
        /// </summary>
        public List<GoblinData> GetGoblinsInColumn(int q)
        {
            return goblins.Where(g => g.isAlive && g.position.q == q).ToList();
        }

        // ============================================================
        // 데미지 / 사망 애니메이션
        // ============================================================

        private IEnumerator DamageFlashAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            // HP바는 ApplyDamageAtPosition에서 즉시 업데이트됨 (여기서는 생략)

            // 빨간색 플래시 (피격 표현) — 원래 색은 항상 White 기준
            if (goblin.goblinImage != null)
            {
                float duration = 0.15f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    if (goblin.goblinImage == null) yield break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    // 흰→빨강→흰 (원색은 항상 White로 고정)
                    if (t < 0.5f)
                        goblin.goblinImage.color = Color.Lerp(Color.white, Color.red, t * 2f);
                    else
                        goblin.goblinImage.color = Color.Lerp(Color.red, Color.white, (t - 0.5f) * 2f);
                    yield return null;
                }

                if (goblin.goblinImage != null)
                    goblin.goblinImage.color = Color.white;
            }

            // 흔들림
            if (goblin.visualObject != null)
            {
                RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
                Vector2 orig = rt.anchoredPosition;
                float shakeDur = 0.1f;
                float elapsed2 = 0f;

                while (elapsed2 < shakeDur)
                {
                    if (goblin.visualObject == null) yield break;
                    elapsed2 += Time.deltaTime;
                    float intensity = 4f * (1f - elapsed2 / shakeDur);
                    rt.anchoredPosition = orig + new Vector2(
                        Random.Range(-intensity, intensity),
                        Random.Range(-intensity, intensity));
                    yield return null;
                }
                if (goblin.visualObject != null)
                    rt.anchoredPosition = orig;
            }
        }

        // ============================================================
        // 데미지 팝업 시스템 — 누적 표시 (-1 → -2 → -3)
        // ============================================================

        /// <summary>
        /// 누적 데미지 팝업 윈도우 (초).
        /// 이 시간 내에 반복 데미지가 들어오면 기존 팝업에 누적 표시.
        /// </summary>
        private const float DAMAGE_ACCUMULATE_WINDOW = 0.5f;

        /// <summary>
        /// 데미지 팝업 — 짧은 시간 내 반복 데미지는 누적 표시 (-1 → -2 → -3).
        /// 최초 데미지 시 새 팝업 생성, 이후 데미지는 기존 팝업 숫자 갱신.
        /// </summary>
        private void SpawnDamagePopups(GoblinData goblin, int totalDamage)
        {
            if (goblin.visualObject == null || hexGrid == null) return;

            if (Time.time - goblin.lastDamageTime < DAMAGE_ACCUMULATE_WINDOW
                && goblin.activeDamagePopup != null)
            {
                // 기존 팝업에 누적 (팝업은 제자리 유지)
                goblin.accumulatedDamage += totalDamage;
                goblin.lastDamageTime = Time.time;
                if (goblin.activeDamageText != null)
                    goblin.activeDamageText.text = $"-{goblin.accumulatedDamage}";
                // 숫자 증가 시 스케일 펄스 강조
                if (goblin.activeDamagePopup != null)
                    StartCoroutine(DamagePopupPulse(goblin.activeDamagePopup.GetComponent<RectTransform>()));
            }
            else
            {
                // 이전 팝업은 파괴하지 않음 — 자연 소멸 (위로 떠오르며 페이드)
                // 새 누적 팝업 시작
                goblin.accumulatedDamage = totalDamage;
                goblin.lastDamageTime = Time.time;
                StartCoroutine(AccumulatingDamagePopup(goblin));
            }
        }

        /// <summary>
        /// 누적 데미지 팝업 코루틴.
        /// 누적 윈도우 동안 대기 → 추가 데미지 없으면 위로 떠오르며 페이드아웃.
        /// </summary>
        private IEnumerator AccumulatingDamagePopup(GoblinData goblin)
        {
            if (goblin.visualObject == null || hexGrid == null) yield break;

            Transform parent = goblin.visualObject.transform.parent;

            // 팝업 오브젝트 생성
            GameObject popup = new GameObject("DamagePopup");
            popup.transform.SetParent(parent, false);

            RectTransform popupRt = popup.AddComponent<RectTransform>();
            RectTransform goblinRt = goblin.visualObject.GetComponent<RectTransform>();
            popupRt.anchoredPosition = goblinRt.anchoredPosition + new Vector2(0, hexGrid.HexSize * 0.5f);
            popupRt.sizeDelta = new Vector2(60f, 30f);

            // 텍스트
            var textObj = new GameObject("DmgText");
            textObj.transform.SetParent(popup.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = $"-{goblin.accumulatedDamage}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.3f, 0.2f, 1f);
            text.raycastTarget = false;

            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 고블린에 팝업 참조 저장
            goblin.activeDamagePopup = popup;
            goblin.activeDamageText = text;

            // 누적 대기: 새로운 데미지가 없을 때까지 대기 (팝업은 제자리 유지)
            while (Time.time - goblin.lastDamageTime < DAMAGE_ACCUMULATE_WINDOW)
            {
                if (popup == null) yield break;
                // 텍스트 실시간 갱신 (SpawnDamagePopups에서도 갱신하지만 안전장치)
                if (text != null)
                    text.text = $"-{goblin.accumulatedDamage}";
                yield return null;
            }

            // ★ 누적 완료 — 먼저 활성 팝업 참조 해제 (새 히트 시 새 팝업 생성 가능)
            // 이전 팝업(자기 자신)은 독립적으로 떠오르며 사라짐 (공존)
            if (goblin.activeDamagePopup == popup)
            {
                goblin.activeDamagePopup = null;
                goblin.activeDamageText = null;
            }

            // 위로 떠오르며 페이드아웃
            Vector2 startPos = popupRt.anchoredPosition;
            float duration = 0.6f;
            float elapsed = 0f;
            float riseHeight = 30f;

            while (elapsed < duration)
            {
                if (popup == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 위로 이동 (EaseOut)
                float rise = riseHeight * VisualConstants.EaseOutCubic(t);
                popupRt.anchoredPosition = startPos + new Vector2(0, rise);

                // 절반 이후부터 페이드아웃
                float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
                text.color = new Color(1f, 0.3f, 0.2f, alpha);
                outline.effectColor = new Color(0, 0, 0, 0.8f * alpha);

                yield return null;
            }

            if (popup != null) Destroy(popup);
        }

        /// <summary>
        /// 데미지 숫자 증가 시 스케일 펄스 (강조 효과)
        /// </summary>
        private IEnumerator DamagePopupPulse(RectTransform rt)
        {
            if (rt == null) yield break;
            float duration = 0.1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // 1.0 → 1.3 → 1.0 스케일 바운스
                float scale = t < 0.5f
                    ? Mathf.Lerp(1f, 1.3f, t * 2f)
                    : Mathf.Lerp(1.3f, 1f, (t - 0.5f) * 2f);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private IEnumerator DeathAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 deathPos = rt.anchoredPosition;

            // 1. 흰색 플래시
            if (goblin.goblinImage != null)
                goblin.goblinImage.color = Color.white;
            yield return new WaitForSeconds(0.08f);

            // WaitForSeconds 동안 오브젝트가 파괴될 수 있음
            if (goblin.visualObject == null || rt == null) yield break;

            // 2. 파편 산개
            StartCoroutine(SpawnDeathDebris(rt.parent, deathPos));

            // 3. 스케일 축소 → 사라짐
            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (goblin.visualObject == null || rt == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 스핀 + 축소
                rt.localScale = Vector3.one * (1f - t);
                rt.localEulerAngles = new Vector3(0, 0, t * 360f);

                if (goblin.goblinImage != null)
                    goblin.goblinImage.color = new Color(1f, 1f, 1f, 1f - t);

                yield return null;
            }

            // 정리
            if (goblin.visualObject != null)
                Destroy(goblin.visualObject);
            goblin.visualObject = null;

            Debug.Log($"[GoblinSystem] 고블린 제거: ({goblin.position}), 총 킬: {totalKills}");
        }

        private IEnumerator SpawnDeathDebris(Transform parent, Vector2 position)
        {
            int debrisCount = Random.Range(8, 13);
            List<GameObject> debrisList = new List<GameObject>();

            for (int i = 0; i < debrisCount; i++)
            {
                GameObject debris = new GameObject("GoblinDebris");
                debris.transform.SetParent(parent, false);

                RectTransform drt = debris.AddComponent<RectTransform>();
                drt.anchoredPosition = position;
                float dSize = Random.Range(4f, 10f);
                drt.sizeDelta = new Vector2(dSize, dSize);

                Image dimg = debris.AddComponent<Image>();
                // 녹색 파편 (고블린 색상)
                dimg.color = new Color(
                    Random.Range(0.15f, 0.45f),
                    Random.Range(0.5f, 0.85f),
                    Random.Range(0.1f, 0.3f),
                    1f);
                dimg.raycastTarget = false;

                debrisList.Add(debris);
            }

            // 파편 애니메이션
            float duration = 0.4f;
            float elapsed = 0f;
            Vector2[] velocities = new Vector2[debrisCount];
            float[] rotSpeeds = new float[debrisCount];

            for (int i = 0; i < debrisCount; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(100f, 300f);
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                rotSpeeds[i] = Random.Range(-720f, 720f);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < debrisList.Count; i++)
                {
                    if (debrisList[i] == null) continue;
                    RectTransform drt = debrisList[i].GetComponent<RectTransform>();
                    drt.anchoredPosition = position + velocities[i] * t;
                    drt.localEulerAngles = new Vector3(0, 0, rotSpeeds[i] * t);

                    Image dimg = debrisList[i].GetComponent<Image>();
                    if (dimg != null)
                    {
                        Color c = dimg.color;
                        c.a = 1f - t;
                        dimg.color = c;
                    }
                }
                yield return null;
            }

            foreach (var d in debrisList)
            {
                if (d != null) Destroy(d);
            }
        }

        // ============================================================
        // 프로시저럴 스프라이트 생성
        // ============================================================

        /// <summary>
        /// 고블린 프로시저럴 스프라이트 (256x256)
        /// 녹색 몸체 + 눈 + 몽둥이 + 뾰족한 귀
        /// </summary>
        private static Sprite CreateGoblinSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            // 투명 초기화
            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++)
                clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // === 몸체 (녹색 원형, 반지름 ~45% of size) ===
            int bodyRadius = (int)(size * 0.35f);
            Color bodyColor = new Color(0.3f, 0.65f, 0.2f);
            Color bodyDark = new Color(0.2f, 0.5f, 0.12f);
            Color bodyLight = new Color(0.4f, 0.78f, 0.3f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < bodyRadius)
                    {
                        // 그라데이션 (왼쪽 상단 밝게)
                        float shade = Mathf.Clamp01((dx + dy) / (bodyRadius * 2f) + 0.5f);
                        Color c = Color.Lerp(bodyLight, bodyDark, shade);
                        tex.SetPixel(x, y, c);
                    }
                    else if (dist < bodyRadius + 2f)
                    {
                        // 안티앨리어싱 가장자리
                        float alpha = 1f - (dist - bodyRadius) / 2f;
                        tex.SetPixel(x, y, new Color(bodyDark.r, bodyDark.g, bodyDark.b, alpha));
                    }
                }
            }

            // === 뾰족한 귀 (왼쪽, 오른쪽) ===
            Color earColor = new Color(0.25f, 0.55f, 0.18f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColor);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColor);

            // === 눈 (흰색 동공 + 빨간 눈동자) ===
            int eyeRadius = (int)(size * 0.07f);
            int eyeOffsetX = (int)(size * 0.12f);
            int eyeOffsetY = (int)(size * 0.06f);

            // 왼쪽 눈
            DrawFilledCircle(tex, cx - eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx - eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.8f, 0.1f, 0.1f));

            // 오른쪽 눈
            DrawFilledCircle(tex, cx + eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx + eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.8f, 0.1f, 0.1f));

            // === 입 (짧은 곡선) ===
            Color mouthColor = new Color(0.15f, 0.35f, 0.1f);
            for (int i = -12; i <= 12; i++)
            {
                int mx = cx + i;
                int my = cy - (int)(size * 0.08f) - Mathf.Abs(i) / 3;
                if (mx >= 0 && mx < size && my >= 0 && my < size)
                    tex.SetPixel(mx, my, mouthColor);
                if (mx >= 0 && mx < size && my - 1 >= 0)
                    tex.SetPixel(mx, my - 1, mouthColor);
            }

            // === 몽둥이 (오른쪽 위 대각선) ===
            Color clubColor = new Color(0.55f, 0.35f, 0.15f);
            Color clubDark = new Color(0.4f, 0.25f, 0.1f);
            int clubStartX = cx + bodyRadius - 10;
            int clubStartY = cy + 5;
            int clubEndX = cx + bodyRadius + 35;
            int clubEndY = cy + bodyRadius + 30;

            // 몽둥이 자루 (직선)
            DrawThickLine(tex, clubStartX, clubStartY, clubEndX, clubEndY, 4, clubColor);

            // 몽둥이 머리 (타원)
            DrawFilledCircle(tex, clubEndX, clubEndY, 12, clubDark);
            DrawFilledCircle(tex, clubEndX - 2, clubEndY + 2, 10, clubColor);

            // === 이빨 (작은 삼각형 2개) ===
            Color toothColor = new Color(0.95f, 0.95f, 0.85f);
            int toothY = cy - (int)(size * 0.08f) - 2;
            DrawTriangle(tex, cx - 5, toothY, cx - 2, toothY - 7, cx + 1, toothY, toothColor);
            DrawTriangle(tex, cx + 3, toothY, cx + 6, toothY - 7, cx + 9, toothY, toothColor);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 궁수 고블린 스프라이트 — 보라색 몸체 + 활 + 화살통
        /// </summary>
        private static Sprite CreateArcherGoblinSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // === 몸체 (보라색 계열) ===
            int bodyRadius = (int)(size * 0.35f);
            Color bodyColor = new Color(0.45f, 0.25f, 0.65f);
            Color bodyDark = new Color(0.3f, 0.15f, 0.5f);
            Color bodyLight = new Color(0.6f, 0.35f, 0.8f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < bodyRadius)
                    {
                        float shade = Mathf.Clamp01((dx + dy) / (bodyRadius * 2f) + 0.5f);
                        tex.SetPixel(x, y, Color.Lerp(bodyLight, bodyDark, shade));
                    }
                    else if (dist < bodyRadius + 2f)
                    {
                        float alpha = 1f - (dist - bodyRadius) / 2f;
                        tex.SetPixel(x, y, new Color(bodyDark.r, bodyDark.g, bodyDark.b, alpha));
                    }
                }
            }

            // === 뾰족한 귀 ===
            Color earColor = new Color(0.38f, 0.2f, 0.55f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColor);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColor);

            // === 눈 (흰색 + 노란 눈동자 — 날카로운 인상) ===
            int eyeRadius = (int)(size * 0.07f);
            int eyeOffsetX = (int)(size * 0.12f);
            int eyeOffsetY = (int)(size * 0.06f);
            DrawFilledCircle(tex, cx - eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx - eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.9f, 0.7f, 0.1f));
            DrawFilledCircle(tex, cx + eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx + eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.9f, 0.7f, 0.1f));

            // === 입 ===
            Color mouthColor = new Color(0.2f, 0.1f, 0.35f);
            for (int i = -10; i <= 10; i++)
            {
                int mx = cx + i;
                int my = cy - (int)(size * 0.08f) - Mathf.Abs(i) / 3;
                if (mx >= 0 && mx < size && my >= 0 && my < size) tex.SetPixel(mx, my, mouthColor);
                if (mx >= 0 && mx < size && my - 1 >= 0) tex.SetPixel(mx, my - 1, mouthColor);
            }

            // === 활 (왼쪽 — 곡선 형태) ===
            Color bowColor = new Color(0.6f, 0.35f, 0.1f);
            Color bowStringColor = new Color(0.85f, 0.85f, 0.8f);
            int bowCx = cx - bodyRadius - 8;
            int bowCy = cy;
            // 활 몸체 (반원)
            for (int angle = -60; angle <= 60; angle++)
            {
                float rad = angle * Mathf.Deg2Rad;
                int bx = bowCx + (int)(28 * Mathf.Sin(rad));
                int by = bowCy + (int)(28 * Mathf.Cos(rad));
                for (int t = -2; t <= 2; t++)
                {
                    if (bx + t >= 0 && bx + t < size && by >= 0 && by < size)
                        tex.SetPixel(bx + t, by, bowColor);
                }
            }
            // 활시위 (직선)
            int stringTopY = bowCy + (int)(28 * Mathf.Cos(-60 * Mathf.Deg2Rad));
            int stringBotY = bowCy + (int)(28 * Mathf.Cos(60 * Mathf.Deg2Rad));
            int stringX = bowCx + (int)(28 * Mathf.Sin(-60 * Mathf.Deg2Rad));
            DrawThickLine(tex, stringX, stringTopY, stringX, stringBotY, 1, bowStringColor);

            // === 화살 (오른쪽 위 대각선) ===
            Color arrowColor = new Color(0.7f, 0.5f, 0.2f);
            int arrowStartX = cx + bodyRadius - 15;
            int arrowStartY = cy + 5;
            int arrowEndX = cx + bodyRadius + 30;
            int arrowEndY = cy + bodyRadius + 20;
            DrawThickLine(tex, arrowStartX, arrowStartY, arrowEndX, arrowEndY, 2, arrowColor);
            // 화살촉 (삼각형)
            Color arrowHeadColor = new Color(0.5f, 0.5f, 0.55f);
            DrawTriangle(tex, arrowEndX - 5, arrowEndY + 2, arrowEndX + 8, arrowEndY + 10, arrowEndX + 2, arrowEndY - 5, arrowHeadColor);

            // === 이빨 ===
            Color toothColor = new Color(0.95f, 0.95f, 0.85f);
            int toothY = cy - (int)(size * 0.08f) - 2;
            DrawTriangle(tex, cx - 4, toothY, cx - 1, toothY - 6, cx + 2, toothY, toothColor);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 갑옷 고블린 스프라이트 — 은색 갑옷 + 방패 + 투구
        /// </summary>
        private static Sprite CreateArmoredGoblinSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // === 몸체 (진한 녹색 — 기본보다 어두움) ===
            int bodyRadius = (int)(size * 0.35f);
            Color bodyColor = new Color(0.25f, 0.5f, 0.2f);
            Color bodyDark = new Color(0.15f, 0.38f, 0.1f);
            Color bodyLight = new Color(0.35f, 0.62f, 0.28f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < bodyRadius)
                    {
                        float shade = Mathf.Clamp01((dx + dy) / (bodyRadius * 2f) + 0.5f);
                        tex.SetPixel(x, y, Color.Lerp(bodyLight, bodyDark, shade));
                    }
                    else if (dist < bodyRadius + 2f)
                    {
                        float alpha = 1f - (dist - bodyRadius) / 2f;
                        tex.SetPixel(x, y, new Color(bodyDark.r, bodyDark.g, bodyDark.b, alpha));
                    }
                }
            }

            // === 갑옷 (은색 흉갑 — 중앙 하단 타원) ===
            Color armorColor = new Color(0.7f, 0.72f, 0.75f);
            Color armorDark = new Color(0.5f, 0.52f, 0.55f);
            int armorRadiusX = (int)(bodyRadius * 0.7f);
            int armorRadiusY = (int)(bodyRadius * 0.55f);
            int armorCy = cy - (int)(bodyRadius * 0.15f);
            for (int y2 = armorCy - armorRadiusY; y2 <= armorCy + armorRadiusY; y2++)
            {
                for (int x2 = cx - armorRadiusX; x2 <= cx + armorRadiusX; x2++)
                {
                    if (x2 < 0 || x2 >= size || y2 < 0 || y2 >= size) continue;
                    float nx = (float)(x2 - cx) / armorRadiusX;
                    float ny = (float)(y2 - armorCy) / armorRadiusY;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        float shine = Mathf.Clamp01(0.5f + nx * 0.3f + ny * 0.2f);
                        tex.SetPixel(x2, y2, Color.Lerp(armorDark, armorColor, shine));
                    }
                }
            }

            // === 투구 (상단 은색 반원) ===
            Color helmetColor = new Color(0.65f, 0.68f, 0.72f);
            int helmetRadius = (int)(bodyRadius * 0.55f);
            int helmetCy = cy + (int)(bodyRadius * 0.35f);
            for (int y2 = helmetCy; y2 <= helmetCy + helmetRadius; y2++)
            {
                for (int x2 = cx - helmetRadius; x2 <= cx + helmetRadius; x2++)
                {
                    if (x2 < 0 || x2 >= size || y2 < 0 || y2 >= size) continue;
                    float dx = x2 - cx;
                    float dy = y2 - helmetCy;
                    if (dx * dx + dy * dy <= helmetRadius * helmetRadius)
                    {
                        float shine = Mathf.Clamp01(0.4f + dx / (helmetRadius * 2f));
                        tex.SetPixel(x2, y2, Color.Lerp(armorDark, helmetColor, shine));
                    }
                }
            }
            // 투구 장식 (중앙 세로 줄)
            for (int y2 = helmetCy; y2 <= helmetCy + helmetRadius; y2++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (cx + ox >= 0 && cx + ox < size && y2 >= 0 && y2 < size)
                        tex.SetPixel(cx + ox, y2, new Color(0.8f, 0.75f, 0.4f)); // 금색 줄
                }
            }

            // === 뾰족한 귀 (투구 옆으로 삐져나옴) ===
            Color earColor = new Color(0.2f, 0.42f, 0.15f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 12, cy + bodyRadius + 22,
                         cx - bodyRadius + 18, cy + bodyRadius + 5, earColor);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 12, cy + bodyRadius + 22,
                         cx + bodyRadius - 18, cy + bodyRadius + 5, earColor);

            // === 눈 (흰색 + 빨간 눈동자, 투구 아래로 들여다보는 느낌) ===
            int eyeRadius = (int)(size * 0.06f);
            int eyeOffsetX = (int)(size * 0.11f);
            int eyeOffsetY = (int)(size * 0.04f);
            DrawFilledCircle(tex, cx - eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx - eyeOffsetX + 1, cy + eyeOffsetY, eyeRadius / 2, new Color(0.8f, 0.15f, 0.1f));
            DrawFilledCircle(tex, cx + eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx + eyeOffsetX + 1, cy + eyeOffsetY, eyeRadius / 2, new Color(0.8f, 0.15f, 0.1f));

            // === 입 ===
            Color mouthColor = new Color(0.12f, 0.3f, 0.08f);
            for (int i = -10; i <= 10; i++)
            {
                int mx = cx + i;
                int my = cy - (int)(size * 0.09f) - Mathf.Abs(i) / 4;
                if (mx >= 0 && mx < size && my >= 0 && my < size) tex.SetPixel(mx, my, mouthColor);
            }

            // === 방패 (왼쪽, 작은 원형) ===
            Color shieldColor = new Color(0.55f, 0.58f, 0.62f);
            Color shieldBorder = new Color(0.4f, 0.42f, 0.45f);
            int shieldCx = cx - bodyRadius + 5;
            int shieldCy = cy - 5;
            int shieldR = (int)(bodyRadius * 0.4f);
            DrawFilledCircle(tex, shieldCx, shieldCy, shieldR, shieldColor);
            DrawFilledCircle(tex, shieldCx, shieldCy, shieldR - 3, new Color(0.6f, 0.62f, 0.68f));
            // 방패 십자 문양
            for (int t = -shieldR + 4; t <= shieldR - 4; t++)
            {
                if (shieldCx + t >= 0 && shieldCx + t < size)
                    tex.SetPixel(shieldCx + t, shieldCy, shieldBorder);
                if (shieldCy + t >= 0 && shieldCy + t < size)
                    tex.SetPixel(shieldCx, shieldCy + t, shieldBorder);
            }

            // === 몽둥이 (오른쪽) ===
            Color clubColor = new Color(0.55f, 0.35f, 0.15f);
            Color clubDark = new Color(0.4f, 0.25f, 0.1f);
            int clubStartX = cx + bodyRadius - 10;
            int clubStartY = cy + 5;
            int clubEndX = cx + bodyRadius + 30;
            int clubEndY = cy + bodyRadius + 25;
            DrawThickLine(tex, clubStartX, clubStartY, clubEndX, clubEndY, 4, clubColor);
            DrawFilledCircle(tex, clubEndX, clubEndY, 10, clubDark);
            DrawFilledCircle(tex, clubEndX - 2, clubEndY + 2, 8, clubColor);

            // === 이빨 ===
            Color toothColor = new Color(0.95f, 0.95f, 0.85f);
            int toothY2 = cy - (int)(size * 0.09f) - 2;
            DrawTriangle(tex, cx - 5, toothY2, cx - 2, toothY2 - 6, cx + 1, toothY2, toothColor);
            DrawTriangle(tex, cx + 3, toothY2, cx + 6, toothY2 - 6, cx + 9, toothY2, toothColor);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 포털 스프라이트 (원형 글로우, 128x128)
        /// </summary>
        private static Sprite CreatePortalSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            int cx = size / 2;
            int cy = size / 2;
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist < radius)
                    {
                        // 중앙은 밝고 가장자리는 어두운 원형 그라데이션
                        float t = dist / radius;
                        float alpha = 1f - t * t;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 방패 고블린 스프라이트 — 청록색 몸체 + 뾰족한 귀 (방패는 별도 오버레이)
        /// </summary>
        private static Sprite CreateShieldGoblinSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // === 몸체 (청록색 계열 — 다른 고블린과 구별) ===
            int bodyRadius = (int)(size * 0.35f);
            Color bodyColor = new Color(0.2f, 0.5f, 0.5f);
            Color bodyDark = new Color(0.12f, 0.35f, 0.38f);
            Color bodyLight = new Color(0.3f, 0.65f, 0.6f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < bodyRadius)
                    {
                        float shade = Mathf.Clamp01((dx + dy) / (bodyRadius * 2f) + 0.5f);
                        tex.SetPixel(x, y, Color.Lerp(bodyLight, bodyDark, shade));
                    }
                    else if (dist < bodyRadius + 2f)
                    {
                        float alpha = 1f - (dist - bodyRadius) / 2f;
                        tex.SetPixel(x, y, new Color(bodyDark.r, bodyDark.g, bodyDark.b, alpha));
                    }
                }
            }

            // === 뾰족한 귀 ===
            Color earColor = new Color(0.15f, 0.4f, 0.42f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColor);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColor);

            // === 눈 (흰색 + 하늘색 눈동자) ===
            int eyeRadius = (int)(size * 0.07f);
            int eyeOffsetX = (int)(size * 0.12f);
            int eyeOffsetY = (int)(size * 0.06f);
            DrawFilledCircle(tex, cx - eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx - eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.3f, 0.7f, 0.9f));
            DrawFilledCircle(tex, cx + eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx + eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.3f, 0.7f, 0.9f));

            // === 입 ===
            Color mouthColor = new Color(0.1f, 0.28f, 0.3f);
            for (int i = -10; i <= 10; i++)
            {
                int mx = cx + i;
                int my = cy - (int)(size * 0.08f) - Mathf.Abs(i) / 3;
                if (mx >= 0 && mx < size && my >= 0 && my < size) tex.SetPixel(mx, my, mouthColor);
                if (mx >= 0 && mx < size && my - 1 >= 0) tex.SetPixel(mx, my - 1, mouthColor);
            }

            // === 이빨 ===
            Color toothColor = new Color(0.95f, 0.95f, 0.85f);
            int toothY = cy - (int)(size * 0.08f) - 2;
            DrawTriangle(tex, cx - 5, toothY, cx - 2, toothY - 7, cx + 1, toothY, toothColor);
            DrawTriangle(tex, cx + 3, toothY, cx + 6, toothY - 7, cx + 9, toothY, toothColor);

            // === 몽둥이 (오른쪽 위) ===
            Color clubColor = new Color(0.55f, 0.35f, 0.15f);
            Color clubDark = new Color(0.4f, 0.25f, 0.1f);
            int clubStartX = cx + bodyRadius - 10;
            int clubStartY = cy + 5;
            int clubEndX = cx + bodyRadius + 35;
            int clubEndY = cy + bodyRadius + 30;
            DrawThickLine(tex, clubStartX, clubStartY, clubEndX, clubEndY, 4, clubColor);
            DrawFilledCircle(tex, clubEndX, clubEndY, 12, clubDark);
            DrawFilledCircle(tex, clubEndX - 2, clubEndY + 2, 10, clubColor);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 방패 스프라이트 — 둥근 사각형, 메탈릭 회색 + 금속 테두리 + 십자 문양
        /// </summary>
        private static Sprite CreateShieldSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // 둥근 사각형 방패 (SDF 기반)
            float halfW = size * 0.4f;
            float halfH = size * 0.45f;
            float cornerRadius = size * 0.12f;

            Color shieldBase = new Color(0.6f, 0.62f, 0.66f);
            Color shieldLight = new Color(0.78f, 0.8f, 0.84f);
            Color shieldDark = new Color(0.4f, 0.42f, 0.46f);
            Color borderColor = new Color(0.5f, 0.48f, 0.35f); // 금색 테두리

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - cx) - halfW + cornerRadius;
                    float dy = Mathf.Abs(y - cy) - halfH + cornerRadius;
                    float dist = Mathf.Sqrt(
                        Mathf.Max(0, dx) * Mathf.Max(0, dx) +
                        Mathf.Max(0, dy) * Mathf.Max(0, dy)) - cornerRadius;

                    if (dist < -3f)
                    {
                        // 내부: 메탈릭 그라데이션
                        float nx = (float)(x - cx) / halfW;
                        float ny = (float)(y - cy) / halfH;
                        float shine = Mathf.Clamp01(0.5f + nx * 0.25f + ny * 0.15f);
                        tex.SetPixel(x, y, Color.Lerp(shieldDark, shieldLight, shine));
                    }
                    else if (dist < 0f)
                    {
                        // 테두리 (3px)
                        tex.SetPixel(x, y, borderColor);
                    }
                    else if (dist < 2f)
                    {
                        // 안티앨리어싱
                        float alpha = 1f - dist / 2f;
                        tex.SetPixel(x, y, new Color(borderColor.r, borderColor.g, borderColor.b, alpha));
                    }
                }
            }

            // 십자 문양 (중앙)
            Color crossColor = new Color(0.5f, 0.48f, 0.35f, 0.7f);
            int crossThick = 2;
            for (int t = (int)(-halfH + 8); t <= (int)(halfH - 8); t++)
            {
                for (int ox = -crossThick; ox <= crossThick; ox++)
                {
                    int px = cx + ox;
                    int py = cy + t;
                    if (px >= 0 && px < size && py >= 0 && py < size)
                        tex.SetPixel(px, py, crossColor);
                }
            }
            for (int t = (int)(-halfW + 8); t <= (int)(halfW - 8); t++)
            {
                for (int oy = -crossThick; oy <= crossThick; oy++)
                {
                    int px = cx + t;
                    int py = cy + oy;
                    if (px >= 0 && px < size && py >= 0 && py < size)
                        tex.SetPixel(px, py, crossColor);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 박힌 드릴 스프라이트 — 작은 삼각형/화살표 (드릴이 박힌 표시)
        /// </summary>
        private static Sprite CreateStuckDrillSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // 화살촉 형태 (아래를 향하는 삼각형)
            Color drillColor = new Color(0.55f, 0.55f, 0.6f);
            Color drillDark = new Color(0.38f, 0.38f, 0.42f);

            // 메인 삼각형 (아래 방향 화살촉)
            int tipY = cy - (int)(size * 0.35f);
            int baseY = cy + (int)(size * 0.15f);
            int baseHalfW = (int)(size * 0.2f);
            DrawTriangle(tex, cx - baseHalfW, baseY, cx, tipY, cx + baseHalfW, baseY, drillColor);

            // 어두운 그라데이션 (왼쪽 반)
            for (int y = tipY; y <= baseY; y++)
            {
                for (int x = 0; x < cx; x++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                    {
                        Color existing = tex.GetPixel(x, y);
                        if (existing.a > 0.5f)
                            tex.SetPixel(x, y, Color.Lerp(existing, drillDark, 0.3f));
                    }
                }
            }

            // 자루 (위쪽 사각형)
            Color shaftColor = new Color(0.5f, 0.35f, 0.2f);
            int shaftTop = cy + (int)(size * 0.35f);
            int shaftHalfW = (int)(size * 0.08f);
            for (int y = baseY; y <= shaftTop; y++)
            {
                for (int x = cx - shaftHalfW; x <= cx + shaftHalfW; x++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        tex.SetPixel(x, y, shaftColor);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 드로잉 헬퍼
        // ============================================================

        private static void DrawFilledCircle(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int size = tex.width;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= size || y < 0 || y >= size) continue;
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius)
                        tex.SetPixel(x, y, color);
                }
            }
        }

        private static void DrawTriangle(Texture2D tex, int x0, int y0, int x1, int y1, int x2, int y2, Color color)
        {
            int size = tex.width;
            int minX = Mathf.Max(0, Mathf.Min(x0, Mathf.Min(x1, x2)));
            int maxX = Mathf.Min(size - 1, Mathf.Max(x0, Mathf.Max(x1, x2)));
            int minY = Mathf.Max(0, Mathf.Min(y0, Mathf.Min(y1, y2)));
            int maxY = Mathf.Min(size - 1, Mathf.Max(y0, Mathf.Max(y1, y2)));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // 바리센트릭 좌표로 삼각형 내부 판정
                    float d = (float)((y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2));
                    if (Mathf.Abs(d) < 0.001f) continue;

                    float a = ((y1 - y2) * (x - x2) + (x2 - x1) * (y - y2)) / d;
                    float b = ((y2 - y0) * (x - x2) + (x0 - x2) * (y - y2)) / d;
                    float c = 1f - a - b;

                    if (a >= 0f && b >= 0f && c >= 0f)
                        tex.SetPixel(x, y, color);
                }
            }
        }

        private static void DrawThickLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color color)
        {
            int size = tex.width;
            float dx = x1 - x0;
            float dy = y1 - y0;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 1f) return;

            dx /= length;
            dy /= length;

            for (float t = 0; t < length; t += 0.5f)
            {
                int cx = (int)(x0 + dx * t);
                int cy = (int)(y0 + dy * t);

                for (int oy = -thickness / 2; oy <= thickness / 2; oy++)
                {
                    for (int ox = -thickness / 2; ox <= thickness / 2; ox++)
                    {
                        int px = cx + ox;
                        int py = cy + oy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                            tex.SetPixel(px, py, color);
                    }
                }
            }
        }

        // ============================================================
        // 이펙트 정리
        // ============================================================

        /// <summary>
        /// 고블린 관련 이펙트 정리
        /// </summary>
        public void CleanupEffects()
        {
            if (hexGrid == null || hexGrid.GridContainer == null) return;

            string[] effectNames = { "GoblinPortal", "GoblinShockwave", "GoblinHitFlash", "GoblinDebris", "ShieldBreakFlash", "ShieldDebris" };

            foreach (Transform child in hexGrid.GridContainer)
            {
                if (child == null) continue;
                foreach (var name in effectNames)
                {
                    if (child.name == name)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        // ============================================================
        // 유틸리티
        // ============================================================

        /// <summary>
        /// 현재 보드의 모든 살아있는 고블린 위치 반환
        /// </summary>
        public List<HexCoord> GetAllGoblinPositions()
        {
            return goblins.Where(g => g.isAlive).Select(g => g.position).ToList();
        }

        /// <summary>
        /// 활성 상태인지 (config가 설정되어 있는지)
        /// </summary>
        public bool IsActive => currentConfig != null;

        /// <summary>
        /// 낙하 충돌로 사망 대기 중인 고블린이 있는지
        /// </summary>
        public bool HasPendingFallDeaths => pendingFallDeaths.Count > 0;

        // ============================================================
        // 물리적 충돌 기반 낙하 대미지
        // ============================================================

        /// <summary>
        /// BlockRemovalSystem이 낙하 시작 전 호출.
        /// 살아있는 모든 고블린의 열(q)별 Y좌표를 반환하여 충돌 타겟으로 사용.
        /// </summary>
        public Dictionary<int, List<(GoblinData goblin, float yPos)>> GetGoblinCollisionTargets(HexGrid grid)
        {
            var result = new Dictionary<int, List<(GoblinData goblin, float yPos)>>();
            if (grid == null) return result;

            foreach (var goblin in goblins.Where(g => g.isAlive))
            {
                int q = goblin.position.q;
                float yPos = grid.CalculateFlatTopHexPosition(goblin.position).y;

                if (!result.ContainsKey(q))
                    result[q] = new List<(GoblinData, float)>();
                result[q].Add((goblin, yPos));
            }
            return result;
        }

        /// <summary>
        /// AnimateFall에서 블록이 고블린 Y좌표를 통과할 때 호출 (1 대미지).
        /// 비코루틴 — 즉시 실행.
        /// </summary>
        public void ApplyIndividualFallDamage(GoblinData goblin)
        {
            if (goblin == null || !goblin.isAlive) return;

            // 궁수 고블린은 낙하 데미지 면역
            if (goblin.isArcher) return;

            // 방패 활성 시 낙하 데미지 면역
            if (goblin.isShielded) return;

            goblin.hp -= 1;
            Debug.Log($"[GoblinSystem] 낙하 충돌 대미지: ({goblin.position}) HP {goblin.hp + 1} → {goblin.hp} (-1)");

            UpdateHPBar(goblin);

            // 데미지 팝업 (누적 시스템)
            if (goblin.visualObject != null)
                SpawnDamagePopups(goblin, 1);

            // 첫 히트에만 플래시 (중복 방지)
            if (!goblin.isFlashing && goblin.visualObject != null)
            {
                goblin.isFlashing = true;
                StartCoroutine(DamageFlashWithReset(goblin));
            }

            // 사망 처리 (대기열에 추가, 나중에 ProcessPendingFallDeaths에서 처리)
            if (goblin.hp <= 0)
            {
                goblin.hp = 0;
                goblin.isAlive = false;
                if (!pendingFallDeaths.Contains(goblin))
                    pendingFallDeaths.Add(goblin);
            }
        }

        /// <summary>
        /// DamageFlashAnimation + isFlashing 리셋
        /// </summary>
        private IEnumerator DamageFlashWithReset(GoblinData goblin)
        {
            yield return StartCoroutine(DamageFlashAnimation(goblin));
            goblin.isFlashing = false;
        }

        /// <summary>
        /// ProcessFalling 완료 후 호출. 낙하 충돌로 사망한 고블린들의 DeathAnimation 실행.
        /// </summary>
        public IEnumerator ProcessPendingFallDeaths()
        {
            if (pendingFallDeaths.Count == 0) yield break;

            // 사망 고블린들의 DeathAnimation 실행
            List<Coroutine> deathCoroutines = new List<Coroutine>();
            foreach (var goblin in pendingFallDeaths)
            {
                totalKills++;
                IncrementTypeKill(goblin);
                OnGoblinKilled?.Invoke(totalKills, goblin.isArmored, goblin.isArcher, goblin.isShieldType);
                if (goblin.visualObject != null)
                    deathCoroutines.Add(StartCoroutine(DeathAnimation(goblin)));
            }

            // 모든 사망 애니메이션 완료 대기
            foreach (var c in deathCoroutines)
                yield return c;

            pendingFallDeaths.Clear();
            goblins.RemoveAll(g => !g.isAlive && g.visualObject == null);
        }

        /// <summary>
        /// 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetGoblinSprite()
        {
            if (goblinSprite == null)
                goblinSprite = CreateGoblinSprite(256);
            return goblinSprite;
        }

        /// <summary>
        /// 궁수 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetArcherGoblinSprite()
        {
            if (archerGoblinSprite == null)
                archerGoblinSprite = CreateArcherGoblinSprite(256);
            return archerGoblinSprite;
        }

        /// <summary>
        /// 갑옷 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetArmoredGoblinSprite()
        {
            if (armoredGoblinSprite == null)
                armoredGoblinSprite = CreateArmoredGoblinSprite(256);
            return armoredGoblinSprite;
        }

        /// <summary>
        /// 방패 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetShieldGoblinSprite()
        {
            if (shieldGoblinSprite == null)
                shieldGoblinSprite = CreateShieldGoblinSprite(256);
            return shieldGoblinSprite;
        }

        /// <summary>
        /// 방패 스프라이트 외부 접근용 (미션 아이콘 오버레이 등)
        /// </summary>
        public static Sprite GetShieldSprite()
        {
            if (shieldSprite == null)
                shieldSprite = CreateShieldSprite(128);
            return shieldSprite;
        }

        // ============================================================
        // 궁수 고블린: 원거리 공격 시스템
        // ============================================================
        // 2턴마다 안 깨진 블록을 화살로 공격, 화살 궤적 + 0.3초 페이드아웃

        /// <summary>
        /// 궁수 공격 턴: 모든 궁수가 동시에 화살 발사
        /// </summary>
        private IEnumerator ArcherAttackPhase()
        {
            var archers = goblins.Where(g => g.isAlive && g.isArcher).ToList();
            if (archers.Count == 0) yield break;

            // 공격 대상 블록 선택: 안 깨진(isCracked=false, isShell=false) 일반 블록
            var validTargets = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null
                    && block.Data.gemType != GemType.None
                    && !block.Data.isCracked && !block.Data.isShell)
                {
                    validTargets.Add(block);
                }
            }

            if (validTargets.Count == 0) yield break;

            // 각 궁수별 타겟 랜덤 선택 + 동시 공격
            List<Coroutine> attackCoroutines = new List<Coroutine>();
            foreach (var archer in archers)
            {
                if (archer.visualObject == null) continue;

                // 랜덤 타겟 블록
                HexBlock target = validTargets[Random.Range(0, validTargets.Count)];
                attackCoroutines.Add(StartCoroutine(ArcherShootArrow(archer, target)));
            }

            foreach (var co in attackCoroutines)
                yield return co;
        }

        /// <summary>
        /// 궁수 대기 모션 (공격하지 않는 턴)
        /// </summary>
        private IEnumerator ArcherIdleAnimation()
        {
            var archers = goblins.Where(g => g.isAlive && g.isArcher && g.visualObject != null).ToList();
            if (archers.Count == 0) yield break;

            // 가볍게 좌우 흔들리는 대기 모션 (0.3초)
            float dur = 0.3f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                float swing = Mathf.Sin(t * Mathf.PI * 2f) * 3f; // ±3도 흔들림
                foreach (var archer in archers)
                {
                    if (archer.visualObject == null) continue;
                    archer.visualObject.transform.localEulerAngles = new Vector3(0, 0, swing);
                }
                yield return null;
            }

            // 원래 회전으로 복귀
            foreach (var archer in archers)
            {
                if (archer.visualObject == null) continue;
                archer.visualObject.transform.localEulerAngles = Vector3.zero;
            }
        }

        /// <summary>
        /// 궁수 단일 공격: 활 당기기 → 화살 발사 → 궤적 페이드아웃 → 블록 크랙
        /// </summary>
        private IEnumerator ArcherShootArrow(GoblinData archer, HexBlock targetBlock)
        {
            if (archer.visualObject == null || targetBlock == null) yield break;

            RectTransform archerRt = archer.visualObject.GetComponent<RectTransform>();
            Vector2 archerPos = archerRt.anchoredPosition;
            Vector2 targetPos = hexGrid.CalculateFlatTopHexPosition(targetBlock.Coord);

            Transform parent = hexGrid.GridContainer;

            // ── 1단계: 활 당기기 모션 (0.2초) ──
            float drawDur = 0.2f;
            float elapsed = 0f;
            Vector3 originalScale = archerRt.localScale;
            while (elapsed < drawDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / drawDur);
                // 약간 뒤로 기울며 활 시위 당기기
                float lean = Mathf.Sin(t * Mathf.PI * 0.5f) * -8f;
                archerRt.localEulerAngles = new Vector3(0, 0, lean);
                float squeeze = 1f - 0.1f * Mathf.Sin(t * Mathf.PI);
                archerRt.localScale = new Vector3(squeeze * originalScale.x, (2f - squeeze) * originalScale.y, 1f);
                yield return null;
            }

            // ── 2단계: 발사 반동 (0.08초) ──
            float recoilDur = 0.08f;
            elapsed = 0f;
            while (elapsed < recoilDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / recoilDur);
                archerRt.localEulerAngles = new Vector3(0, 0, 5f * (1f - t));
                float expand = 1f + 0.15f * (1f - t);
                archerRt.localScale = new Vector3(expand * originalScale.x, (2f - expand) * originalScale.y, 1f);
                yield return null;
            }
            archerRt.localScale = originalScale;
            archerRt.localEulerAngles = Vector3.zero;

            // ── 3단계: 화살 투사체 비행 (0.83초 — 느린 포물선) ──
            GameObject arrow = new GameObject("ArcherArrow");
            arrow.transform.SetParent(parent, false);
            RectTransform arrowRt = arrow.AddComponent<RectTransform>();
            arrowRt.sizeDelta = new Vector2(40f, 12f); // 기존 2배 사이즈
            arrowRt.anchoredPosition = archerPos;

            Image arrowImg = arrow.AddComponent<Image>();
            arrowImg.color = new Color(0.7f, 0.5f, 0.2f);
            arrowImg.raycastTarget = false;

            // 비행 중 실시간 궤적 세그먼트 생성
            List<GameObject> trailSegments = new List<GameObject>();

            // 거리에 비례한 포물선 높이 (최소 80, 먼 거리일수록 더 높게)
            float dist = Vector2.Distance(archerPos, targetPos);
            float arcPeak = Mathf.Max(80f, dist * 0.35f);

            float flightDur = 0.83f; // 기존 0.25초 대비 약 30% 속도
            elapsed = 0f;
            Vector2 prevPos = archerPos;
            float trailInterval = 0.03f; // 궤적 세그먼트 생성 간격
            float timeSinceLastTrail = 0f;
            while (elapsed < flightDur)
            {
                elapsed += Time.deltaTime;
                timeSinceLastTrail += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flightDur);
                // 포물선 궤적 (위로 큰 호)
                float arcHeight = Mathf.Sin(t * Mathf.PI) * arcPeak;
                Vector2 pos = Vector2.Lerp(archerPos, targetPos, t) + Vector2.up * arcHeight;
                arrowRt.anchoredPosition = pos;

                // 화살 회전: 이동 방향(접선)을 따라 회전
                Vector2 velocity = pos - prevPos;
                if (velocity.sqrMagnitude > 0.01f)
                {
                    float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                    arrowRt.localEulerAngles = new Vector3(0, 0, angle);
                }

                // 일정 간격마다 궤적 세그먼트 추가
                if (timeSinceLastTrail >= trailInterval && velocity.sqrMagnitude > 0.01f)
                {
                    timeSinceLastTrail = 0f;
                    float segLen = Vector2.Distance(prevPos, pos);
                    if (segLen > 1f)
                    {
                        GameObject seg = new GameObject("ArrowTrail");
                        seg.transform.SetParent(parent, false);
                        RectTransform srt = seg.AddComponent<RectTransform>();
                        Vector2 mid = (prevPos + pos) * 0.5f;
                        srt.anchoredPosition = mid;
                        srt.sizeDelta = new Vector2(segLen, 3f);
                        Vector2 segDir = pos - prevPos;
                        float segAngle = Mathf.Atan2(segDir.y, segDir.x) * Mathf.Rad2Deg;
                        srt.localEulerAngles = new Vector3(0, 0, segAngle);
                        Image simg = seg.AddComponent<Image>();
                        simg.color = new Color(0.9f, 0.6f, 0.2f, 0.7f);
                        simg.raycastTarget = false;
                        trailSegments.Add(seg);
                    }
                }

                prevPos = pos;
                yield return null;
            }

            Destroy(arrow);

            // ── 4단계: 착탄 이펙트 + 블록 크랙 ──
            // 타격 플래시
            GameObject flash = new GameObject("ArrowHitFlash");
            flash.transform.SetParent(parent, false);
            RectTransform flashRt = flash.AddComponent<RectTransform>();
            flashRt.anchoredPosition = targetPos;
            flashRt.sizeDelta = new Vector2(60f, 60f);
            Image flashImg = flash.AddComponent<Image>();
            flashImg.color = new Color(1f, 0.8f, 0.3f, 0.9f);
            flashImg.raycastTarget = false;
            Destroy(flash, 0.15f);

            // 블록 상태 변경 (근접 고블린과 동일한 크랙 로직)
            ApplyArcherDamageToBlock(targetBlock);

            // ── 5단계: 궤적 페이드아웃 (0.3초) ──
            StartCoroutine(FadeTrailSegments(trailSegments, 0.3f));

            yield return new WaitForSeconds(0.1f); // 착탄 후 약간의 딜레이
        }

        /// <summary>
        /// 궁수 화살에 의한 블록 크랙 처리 (근접 고블린과 동일)
        /// </summary>
        private void ApplyArcherDamageToBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return;

            // 특수 블록 (MoveBlock/FixedBlock 제외): 기능 해제 + 크랙
            if (block.Data.specialType != SpecialBlockType.None
                && block.Data.specialType != SpecialBlockType.MoveBlock
                && block.Data.specialType != SpecialBlockType.FixedBlock)
            {
                block.Data.specialType = SpecialBlockType.None;
                block.Data.isCracked = true;
                block.UpdateVisuals();
                Debug.Log($"[GoblinSystem] 궁수 화살: 특수블록 ({block.Coord}) 기능 해제 + 크랙");
                return;
            }

            // 일반 블록: 크랙
            if (!block.Data.isCracked && !block.Data.isShell)
            {
                block.Data.isCracked = true;
                block.UpdateVisuals();
                Debug.Log($"[GoblinSystem] 궁수 화살: ({block.Coord}) 크랙 적용");
            }
        }

        /// <summary>
        /// 이미 생성된 궤적 세그먼트들을 페이드아웃 후 파괴
        /// </summary>
        private IEnumerator FadeTrailSegments(List<GameObject> segments, float fadeDuration)
        {
            if (segments == null || segments.Count == 0) yield break;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float alpha = 0.7f * (1f - t);

                foreach (var seg in segments)
                {
                    if (seg == null) continue;
                    Image simg = seg.GetComponent<Image>();
                    if (simg != null)
                        simg.color = new Color(0.9f, 0.6f, 0.2f, alpha);
                }
                yield return null;
            }

            foreach (var seg in segments)
            {
                if (seg != null) Destroy(seg);
            }
        }

        // ============================================================
        // 방패 고블린: 드릴 차단 시스템
        // ============================================================

        /// <summary>
        /// 특정 좌표에 방패 활성 고블린이 있는지 확인
        /// </summary>
        public bool HasShieldGoblinAt(HexCoord coord)
        {
            return goblins.Any(g => g.isAlive && g.isShielded && g.position == coord);
        }

        /// <summary>
        /// 드릴이 방패 고블린 위치에 도달했을 때 호출.
        /// 방패에 드릴을 박고 내구도 감소. 방패 파괴 시 isShielded = false.
        /// 반환: true면 드릴이 차단됨 (관통 불가), false면 방패 없음.
        /// </summary>
        public bool TryBlockDrill(HexCoord position)
        {
            var goblin = goblins.FirstOrDefault(g => g.isAlive && g.isShielded && g.position == position);
            if (goblin == null) return false;

            // 드릴은 박힌 비주얼 추가
            StartCoroutine(AddStuckDrillVisual(goblin));

            // ★ 방패가 드릴을 막았을 때 고블린 전체 들썩임 연출
            StartCoroutine(ShieldJoltEffect(goblin));

            // 방패에 1 대미지
            ApplyShieldDamage(goblin, 1);

            return true;
        }

        /// <summary>
        /// 방패에 대미지를 적용. 방패 HP 감소 + 비주얼 업데이트.
        /// 방패 파괴 시 일반 고블린으로 전환 (HP=8).
        /// </summary>
        private void ApplyShieldDamage(GoblinData goblin, int damage)
        {
            if (!goblin.isShielded) return;

            goblin.shieldHp -= damage;
            StartCoroutine(ShieldHitEffect(goblin));

            // 방패 HP 숫자 업데이트
            UpdateShieldHPText(goblin);

            Debug.Log($"[GoblinSystem] 방패 대미지: ({goblin.position}) 방패HP {goblin.shieldHp + damage} → {goblin.shieldHp}");

            if (goblin.shieldHp <= 0)
            {
                goblin.isShielded = false;

                // 방패 파괴 후 본체 HP = 8
                goblin.hp = 8;
                goblin.maxHp = 8;
                UpdateHPBar(goblin);

                StartCoroutine(ShieldBreakEffect(goblin));
                Debug.Log($"[GoblinSystem] 방패 파괴! ({goblin.position}) HP=8 일반 고블린으로 전환");
            }
        }

        /// <summary>
        /// 방패 HP 숫자 텍스트 업데이트
        /// </summary>
        private void UpdateShieldHPText(GoblinData goblin)
        {
            if (goblin.shieldHpText != null)
                goblin.shieldHpText.text = $"{Mathf.Max(0, goblin.shieldHp)}";
        }

        /// <summary>
        /// 방패에 박힌 드릴 비주얼 추가 (EaseOutBack 기반 박히는 애니메이션)
        /// </summary>
        private IEnumerator AddStuckDrillVisual(GoblinData goblin)
        {
            if (goblin.shieldImage == null) yield break;

            if (stuckDrillSprite == null)
                stuckDrillSprite = CreateStuckDrillSprite(64);

            Transform shieldTransform = goblin.shieldImage.transform;

            GameObject drillObj = new GameObject("StuckDrill");
            drillObj.transform.SetParent(shieldTransform, false);

            RectTransform drt = drillObj.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);

            // 랜덤 위치 오프셋 (방패 내부)
            float offsetX = Random.Range(-12f, 12f);
            float offsetY = Random.Range(-8f, 8f);
            drt.anchoredPosition = new Vector2(offsetX, offsetY);
            drt.sizeDelta = new Vector2(18f, 18f);
            drt.localScale = Vector3.zero;
            drt.localEulerAngles = new Vector3(0, 0, Random.Range(-30f, 30f));

            Image dimg = drillObj.AddComponent<Image>();
            dimg.sprite = stuckDrillSprite;
            dimg.color = new Color(0.6f, 0.6f, 0.65f);
            dimg.raycastTarget = false;

            if (goblin.stuckDrills != null)
                goblin.stuckDrills.Add(drillObj);

            // 박히는 애니메이션 (EaseOutBack)
            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (drillObj == null) yield break;
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / duration));
                drt.localScale = Vector3.one * t;
                yield return null;
            }
            if (drillObj != null)
                drt.localScale = Vector3.one;
        }

        /// <summary>
        /// 드릴이 방패에 막혔을 때 고블린 전체가 들썩이는 연출.
        /// 충격으로 뒤(위)로 밀렸다가 EaseOutElastic으로 원래 위치로 복귀.
        /// </summary>
        private IEnumerator ShieldJoltEffect(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 origPos = rt.anchoredPosition;
            Vector3 origScale = rt.localScale;

            // Phase 1: 충격 — 위(뒤)로 밀림 + 납작 스퀴즈 (0.06s)
            float pushDist = 12f;   // 위로 밀리는 거리
            float impactDur = 0.06f;
            float elapsed = 0f;
            while (elapsed < impactDur)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / impactDur);
                // 위로 밀림
                rt.anchoredPosition = origPos + new Vector2(0, pushDist * t);
                // 납작 스퀴즈 (가로 넓어지고 세로 줄어듦)
                float squash = Mathf.Lerp(1f, 0.85f, t);
                float stretch = Mathf.Lerp(1f, 1.12f, t);
                rt.localScale = new Vector3(origScale.x * stretch, origScale.y * squash, 1f);
                yield return null;
            }

            // Phase 2: 복귀 — EaseOutElastic 바운스로 원위치 (0.35s)
            float bounceDur = 0.35f;
            elapsed = 0f;
            Vector2 pushedPos = origPos + new Vector2(0, pushDist);
            while (elapsed < bounceDur)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDur);

                // EaseOutElastic 커브
                float elastic;
                if (t <= 0f) elastic = 0f;
                else if (t >= 1f) elastic = 1f;
                else
                {
                    float p = 0.3f;
                    elastic = Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
                }

                // 위치 복귀
                rt.anchoredPosition = Vector2.Lerp(pushedPos, origPos, elastic);

                // 스케일 복귀 (EaseOutBack)
                float scaleT = t * t * (2.7f * t - 1.7f);
                if (scaleT > 1f) scaleT = 1f;
                float sx = Mathf.Lerp(1.12f, 1f, scaleT);
                float sy = Mathf.Lerp(0.85f, 1f, scaleT);
                rt.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);

                yield return null;
            }

            // 최종 원래 값 보장
            if (rt != null)
            {
                rt.anchoredPosition = origPos;
                rt.localScale = origScale;
            }
        }

        /// <summary>
        /// 방패 피격 이펙트: 백색 플래시 + 고블린 전체 들썩임 (위로 튀었다 바운스)
        /// </summary>
        private IEnumerator ShieldHitEffect(GoblinData goblin)
        {
            if (goblin.shieldImage == null) yield break;

            Image shieldImg = goblin.shieldImage;
            Color origColor = shieldImg.color;

            // 백색 플래시
            float flashDur = 0.12f;
            float elapsed = 0f;
            while (elapsed < flashDur)
            {
                if (shieldImg == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flashDur);
                if (t < 0.5f)
                    shieldImg.color = Color.Lerp(origColor, Color.white, t * 2f);
                else
                    shieldImg.color = Color.Lerp(Color.white, origColor, (t - 0.5f) * 2f);
                yield return null;
            }
            if (shieldImg != null)
                shieldImg.color = origColor;

            // ★ 고블린 전체 들썩임: 위로 튀어올랐다 → 바운스하며 복귀
            if (goblin.visualObject == null) yield break;
            RectTransform goblinRt = goblin.visualObject.GetComponent<RectTransform>();
            if (goblinRt == null) yield break;
            Vector2 origPos = goblinRt.anchoredPosition;

            // Phase 1: 위로 빠르게 튀어오름 (0.08초)
            float riseHeight = 12f;
            float riseDur = 0.08f;
            elapsed = 0f;
            while (elapsed < riseDur)
            {
                if (goblinRt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / riseDur);
                // EaseOutQuad: 빠르게 시작, 서서히 감속
                float eased = 1f - (1f - t) * (1f - t);
                goblinRt.anchoredPosition = origPos + new Vector2(0f, -riseHeight * eased);
                yield return null;
            }

            // Phase 2: 바운스하며 복귀 (0.18초) — 감쇠 사인파
            float bounceDur = 0.18f;
            elapsed = 0f;
            while (elapsed < bounceDur)
            {
                if (goblinRt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDur);
                // 감쇠 바운스: sin(2π*t) * (1-t)^2
                float decay = (1f - t) * (1f - t);
                float bounce = Mathf.Sin(t * Mathf.PI * 2.5f) * decay;
                goblinRt.anchoredPosition = origPos + new Vector2(0f, -riseHeight * bounce);
                yield return null;
            }

            if (goblinRt != null)
                goblinRt.anchoredPosition = origPos;
        }

        /// <summary>
        /// 방패 파괴 이펙트: 금속 파편 7개 (중력+회전) + 백색 플래시 + 박힌 드릴 제거
        /// </summary>
        private IEnumerator ShieldBreakEffect(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            Transform parent = goblin.visualObject.transform.parent;
            RectTransform goblinRt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 breakPos = goblinRt.anchoredPosition;

            // 방패 위치 (고블린 아래쪽)
            float goblinSize = goblinRt.sizeDelta.x;
            Vector2 shieldCenter = breakPos + new Vector2(0, -goblinSize * 0.18f);

            // 1. 백색 플래시
            GameObject flash = new GameObject("ShieldBreakFlash");
            flash.transform.SetParent(parent, false);
            RectTransform frt = flash.AddComponent<RectTransform>();
            frt.anchoredPosition = shieldCenter;
            frt.sizeDelta = new Vector2(goblinSize * 1.2f, goblinSize * 1.0f);
            Image fimg = flash.AddComponent<Image>();
            fimg.sprite = HexBlock.GetHexFlashSprite();
            fimg.color = new Color(1f, 1f, 1f, 0.9f);
            fimg.raycastTarget = false;

            // 2. 방패 Image와 박힌 드릴 비주얼 즉시 파괴
            if (goblin.shieldImage != null)
            {
                Destroy(goblin.shieldImage.gameObject);
                goblin.shieldImage = null;
            }
            if (goblin.stuckDrills != null)
            {
                foreach (var drill in goblin.stuckDrills)
                {
                    if (drill != null) Destroy(drill);
                }
                goblin.stuckDrills.Clear();
            }

            // 3. 금속 파편 7개 생성
            int debrisCount = 7;
            List<GameObject> debrisList = new List<GameObject>();
            Vector2[] velocities = new Vector2[debrisCount];
            float[] rotSpeeds = new float[debrisCount];

            for (int i = 0; i < debrisCount; i++)
            {
                GameObject debris = new GameObject("ShieldDebris");
                debris.transform.SetParent(parent, false);

                RectTransform drt = debris.AddComponent<RectTransform>();
                drt.anchoredPosition = shieldCenter;
                float dSize = Random.Range(6f, 14f);
                drt.sizeDelta = new Vector2(dSize, dSize * Random.Range(0.5f, 1.2f));

                Image dimg = debris.AddComponent<Image>();
                // 금속 파편 색상 (은색~회색 변주)
                float metalShade = Random.Range(0.45f, 0.8f);
                dimg.color = new Color(metalShade, metalShade * 1.02f, metalShade * 1.05f, 1f);
                dimg.raycastTarget = false;

                debrisList.Add(debris);

                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(120f, 280f);
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                rotSpeeds[i] = Random.Range(-540f, 540f);
            }

            // 파편 애니메이션 (중력 + 회전 + 페이드)
            float duration = 0.5f;
            float elapsed = 0f;
            float gravity = -400f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 플래시 페이드아웃
                if (flash != null)
                {
                    float flashScale = 1f + 0.3f * VisualConstants.EaseOutCubic(t);
                    frt.localScale = Vector3.one * flashScale;
                    fimg.color = new Color(1f, 1f, 1f, 0.9f * (1f - t));
                }

                for (int i = 0; i < debrisList.Count; i++)
                {
                    if (debrisList[i] == null) continue;
                    RectTransform drt2 = debrisList[i].GetComponent<RectTransform>();

                    // 위치: 초기속도 + 중력
                    Vector2 pos = shieldCenter + velocities[i] * elapsed +
                                  new Vector2(0, 0.5f * gravity * elapsed * elapsed);
                    drt2.anchoredPosition = pos;
                    drt2.localEulerAngles = new Vector3(0, 0, rotSpeeds[i] * elapsed);

                    Image dimg2 = debrisList[i].GetComponent<Image>();
                    if (dimg2 != null)
                    {
                        Color c = dimg2.color;
                        c.a = 1f - t;
                        dimg2.color = c;
                    }
                }
                yield return null;
            }

            // 정리
            if (flash != null) Destroy(flash);
            foreach (var d in debrisList)
            {
                if (d != null) Destroy(d);
            }

            // 화면 흔들림
            if (hexGrid != null)
                StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity * 1.2f, VisualConstants.ShakeSmallDuration));
        }

        // ============================================================
        // 3단계: 직선 라인 전체 크랙 → 쉘 변환 체크
        // ============================================================
        // 육각 그리드의 3방향 직선 라인(q일정, r일정, s일정) 중
        // 한 라인의 모든 블록이 깨진(isCracked) 또는 쉘(isShell)이면
        // 해당 라인의 깨진 블록을 쉘(회색)로 변환.
        // ============================================================

        /// <summary>
        /// 3방향 직선 라인을 검사하여, 라인 전체가 크랙/쉘이면 쉘로 변환 + 이펙트.
        /// </summary>
        private IEnumerator CheckAndApplyEdgeConnection()
        {
            if (hexGrid == null) yield break;

            int radius = hexGrid.GridRadius;

            // 모든 블록을 좌표 맵으로 수집
            var blockMap = new Dictionary<HexCoord, HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null)
                    blockMap[block.Coord] = block;
            }

            // 완전 크랙 라인의 블록을 수집
            List<List<HexBlock>> fullCrackedLines = new List<List<HexBlock>>();

            // ── 방향 1: q 일정 (세로 줄) ──
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-radius, -q - radius);
                int rMax = Mathf.Min(radius, -q + radius);
                var line = new List<HexBlock>();
                bool allCrackedOrShell = true;

                for (int r = rMin; r <= rMax; r++)
                {
                    var coord = new HexCoord(q, r);
                    if (blockMap.TryGetValue(coord, out var block) && block.Data != null
                        && block.Data.gemType != GemType.None)
                    {
                        if (block.Data.isCracked || block.Data.isShell)
                            line.Add(block);
                        else
                        {
                            allCrackedOrShell = false;
                            break;
                        }
                    }
                    else
                    {
                        allCrackedOrShell = false;
                        break;
                    }
                }

                if (allCrackedOrShell && line.Count >= 2)
                    fullCrackedLines.Add(line);
            }

            // ── 방향 2: r 일정 (가로 줄) ──
            for (int r = -radius; r <= radius; r++)
            {
                int qMin = Mathf.Max(-radius, -r - radius);
                int qMax = Mathf.Min(radius, -r + radius);
                var line = new List<HexBlock>();
                bool allCrackedOrShell = true;

                for (int q = qMin; q <= qMax; q++)
                {
                    var coord = new HexCoord(q, r);
                    if (blockMap.TryGetValue(coord, out var block) && block.Data != null
                        && block.Data.gemType != GemType.None)
                    {
                        if (block.Data.isCracked || block.Data.isShell)
                            line.Add(block);
                        else
                        {
                            allCrackedOrShell = false;
                            break;
                        }
                    }
                    else
                    {
                        allCrackedOrShell = false;
                        break;
                    }
                }

                if (allCrackedOrShell && line.Count >= 2)
                    fullCrackedLines.Add(line);
            }

            // ── 방향 3: s 일정 (s = -q-r, 대각선 줄) ──
            for (int s = -radius; s <= radius; s++)
            {
                // s = -q - r → q = -r - s
                int rMin = Mathf.Max(-radius, -s - radius);
                int rMax = Mathf.Min(radius, -s + radius);
                var line = new List<HexBlock>();
                bool allCrackedOrShell = true;

                for (int r = rMin; r <= rMax; r++)
                {
                    int q = -r - s;
                    var coord = new HexCoord(q, r);
                    if (blockMap.TryGetValue(coord, out var block) && block.Data != null
                        && block.Data.gemType != GemType.None)
                    {
                        if (block.Data.isCracked || block.Data.isShell)
                            line.Add(block);
                        else
                        {
                            allCrackedOrShell = false;
                            break;
                        }
                    }
                    else
                    {
                        allCrackedOrShell = false;
                        break;
                    }
                }

                if (allCrackedOrShell && line.Count >= 2)
                    fullCrackedLines.Add(line);
            }

            if (fullCrackedLines.Count == 0) yield break;

            // 변환 대상 수집 (이미 쉘인 블록 제외, 중복 제거)
            HashSet<HexBlock> convertSet = new HashSet<HexBlock>();
            foreach (var line in fullCrackedLines)
            {
                foreach (var block in line)
                {
                    if (block != null && block.Data != null && !block.Data.isShell)
                        convertSet.Add(block);
                }
            }

            if (convertSet.Count == 0) yield break;

            List<HexBlock> blocksToConvert = new List<HexBlock>(convertSet);
            Debug.Log($"[GoblinSystem] 직선 라인 크랙 감지! {fullCrackedLines.Count}개 라인, {blocksToConvert.Count}개 블록 쉘 변환");

            // 이펙트 + 변환 실행
            yield return StartCoroutine(EdgeConnectionEffect(blocksToConvert, fullCrackedLines));
        }

        /// <summary>
        /// 엣지 연결 시 변환 이펙트: 순차 전파 펄스 → 쉘 변환 → 화면 흔들림.
        /// </summary>
        private IEnumerator EdgeConnectionEffect(List<HexBlock> blocksToConvert, List<List<HexBlock>> allComponents)
        {
            Transform parent = hexGrid.GridContainer;
            float hexSize = hexGrid.HexSize;

            // ── 1. 경고 플래시: 전체 연결 컴포넌트에 붉은 펄스 ──
            // 모든 컴포넌트의 블록에 경고 펄스 (이미 쉘인 것도 포함)
            var allBlocks = new List<HexBlock>();
            foreach (var comp in allComponents)
                allBlocks.AddRange(comp);

            // 붉은 경고 펄스 (0.3초)
            var warningFlashes = new List<GameObject>();
            foreach (var block in allBlocks)
            {
                if (block == null) continue;
                RectTransform blockRt = block.GetComponent<RectTransform>();
                if (blockRt == null) continue;

                GameObject flash = new GameObject("EdgeWarningFlash");
                flash.transform.SetParent(parent, false);
                RectTransform frt = flash.AddComponent<RectTransform>();
                frt.anchoredPosition = blockRt.anchoredPosition;
                frt.sizeDelta = new Vector2(hexSize * 1.6f, hexSize * 1.6f);

                Image fimg = flash.AddComponent<Image>();
                fimg.sprite = HexBlock.GetHexFlashSprite();
                fimg.color = new Color(1f, 0.2f, 0.1f, 0f); // 붉은색, 초기 투명
                fimg.raycastTarget = false;
                warningFlashes.Add(flash);
            }

            // 경고 펄스 페이드 인/아웃
            float warnDuration = 0.35f;
            float warnElapsed = 0f;
            while (warnElapsed < warnDuration)
            {
                warnElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(warnElapsed / warnDuration);
                // 0→0.4: 페이드인, 0.4→1.0: 페이드아웃
                float alpha;
                if (t < 0.4f)
                    alpha = VisualConstants.EaseOutCubic(t / 0.4f) * 0.7f;
                else
                    alpha = 0.7f * (1f - VisualConstants.EaseInQuad((t - 0.4f) / 0.6f));

                foreach (var flash in warningFlashes)
                {
                    if (flash == null) continue;
                    Image img = flash.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(1f, 0.2f, 0.1f, alpha);
                }
                yield return null;
            }

            foreach (var flash in warningFlashes)
            {
                if (flash != null) Destroy(flash);
            }

            // ── 2. 순차 전파 변환: BFS depth 순으로 블록별 딜레이 ──
            // blocksToConvert를 BFS depth 순으로 정렬 (첫 블록부터 전파)
            float delayPerBlock = 0.04f;
            for (int i = 0; i < blocksToConvert.Count; i++)
            {
                var block = blocksToConvert[i];
                if (block == null || block.Data == null) continue;

                // 쉘로 변환
                block.Data.isShell = true;
                block.Data.isCracked = true;
                block.Data.specialType = SpecialBlockType.None;
                block.Data.tier = BlockTier.Normal;
                block.UpdateVisuals();

                // 벽돌 내려놓기 사운드
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayShellConvertSound();

                // 변환 플래시 이펙트 (비차단)
                StartCoroutine(ShellConvertFlash(block));

                yield return new WaitForSeconds(delayPerBlock);
            }

            // ── 3. 화면 흔들림 ──
            if (hexGrid != null)
                StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity * 1.5f, VisualConstants.ShakeSmallDuration * 1.5f));

            // 최종 안정화 대기
            yield return new WaitForSeconds(0.15f);

            Debug.Log($"[GoblinSystem] 엣지 연결 변환 완료: {blocksToConvert.Count}개 블록 → 쉘");
        }

        /// <summary>
        /// 개별 블록의 쉘 변환 플래시 이펙트 (백색 → 회색 전환).
        /// </summary>
        private IEnumerator ShellConvertFlash(HexBlock block)
        {
            if (block == null) yield break;

            Transform parent = hexGrid.GridContainer;
            RectTransform blockRt = block.GetComponent<RectTransform>();
            if (blockRt == null) yield break;

            float hexSize = hexGrid.HexSize;

            // 백색 플래시 오버레이
            GameObject flash = new GameObject("ShellConvertFlash");
            flash.transform.SetParent(parent, false);
            RectTransform frt = flash.AddComponent<RectTransform>();
            frt.anchoredPosition = blockRt.anchoredPosition;
            frt.sizeDelta = new Vector2(hexSize * 1.4f, hexSize * 1.4f);

            Image fimg = flash.AddComponent<Image>();
            fimg.sprite = HexBlock.GetHexFlashSprite();
            fimg.color = new Color(1f, 1f, 1f, 0.85f);
            fimg.raycastTarget = false;

            // 스케일 펀치 + 페이드아웃 (0.2초)
            float duration = 0.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + 0.2f * (1f - VisualConstants.EaseOutCubic(t));
                frt.localScale = Vector3.one * scale;
                fimg.color = new Color(1f, 1f, 1f, 0.85f * (1f - VisualConstants.EaseInQuad(t)));
                yield return null;
            }

            if (flash != null) Destroy(flash);
        }
    }
}

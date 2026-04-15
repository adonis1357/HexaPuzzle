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
        public int shieldGoblinHp = 10;  // 방패 고블린 HP
        public int shieldHp = 3;         // 방패 내구도 (드릴 3회 차단 후 파괴)
        public int bombGoblinHp = 10;    // 폭탄 고블린 HP
        public float heavyRatio = 0f;    // 헤비급 고블린 소환 확률
        public int heavyGoblinHp = 36;   // 헤비급 고블린 HP
        public float thiefRatio = 0f;    // 도둑 고블린 소환 확률
        public int thiefGoblinHp = 12;   // 도둑 고블린 HP
        public float wizardRatio = 0f;   // 마법사 고블린 소환 확률
        public int wizardGoblinHp = 3;   // 마법사 고블린 HP (기본 3)
        public float witchRatio = 0f;    // 마녀 고블린 소환 확률
        public int witchGoblinHp = 6;    // 마녀 고블린 HP (기본 6)
        public int undeadHp = 3;         // 언데드 HP (기본 3)

        // Lv2 몬스터 비율 (0.0 ~ 1.0) — 해당 기본 타입 소환 시 Lv2로 교체
        public float goblinLv2Ratio = 0f;    // 몽둥이 Lv2 비율
        public float armoredLv2Ratio = 0f;   // 갑옷 Lv2 비율
        public float archerLv2Ratio = 0f;    // 궁수 Lv2 비율
        public float shieldLv2Ratio = 0f;    // 방패 Lv2 비율
    }

    /// <summary>
    /// 고블린 개체 데이터
    /// </summary>
    public class GoblinData
    {
        public HexCoord position;       // 현재 위치 (그리드 외부 좌표 포함)
        public int hp = 5;              // 현재 체력 (몽둥이 기본)
        public int maxHp = 5;           // 최대 체력
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
        public bool isBomb = false;            // 폭탄 고블린 여부
        public bool isHealer = false;          // 힐러 고블린 여부
        public bool isInBlockField = false;    // 힐러 전용: 블록 필드 진입 여부 (한 번 true면 유지)
        public bool isHeavy = false;           // 헤비급 고블린 여부 (3블록 삼각형 점유)
        public bool isWizard = false;          // 마법사 고블린 여부 (소환 영역 전용)
        public int wizardTurnCounter = 0;      // 마법사 턴 카운터 (5턴마다 번개 특수 공격)
        public bool isThief = false;           // 도둑 고블린 여부
        public bool isStealth = false;         // 은신 상태 여부
        public bool isWitch = false;           // 마녀 고블린 여부
        public int witchSummonCounter = 0;     // 마녀 소환 턴 카운터 (6턴마다 소환)
        public int witchMoveCounter = 0;       // 마녀 이동 턴 카운터 (2턴마다 이동)
        public int witchOwnerIndex = -1;       // 마녀 고유 ID (언데드가 소속 마녀 식별)
        public bool isUndead = false;          // 언데드 여부 (마녀가 소환한 하수인)
        public int undeadOwnerIndex = -1;      // 소속 마녀의 witchOwnerIndex
        public bool justSummoned = false;      // 소환된 턴에는 이동/공격 안 함
        public int monsterLevel = 1;           // 몬스터 레벨 (1=기본, 2=엘리트)
        public bool stealthProtected = false;  // 드릴 회피 후 캐스케이드 동안 은신 보호
        public int thiefAttackPhase = 0;       // 0=공격A(수리검), 1=공격B(은신)
        public List<HexCoord> thiefVisitedPositions = new List<HexCoord>(); // 방문한 좌표 (되돌아갈 수 없음)
        public List<HexCoord> occupiedCoords = new List<HexCoord>(); // Heavy일 때 점유하는 3개 좌표
        public int heavyTurnCounter = 0;       // Heavy 전용 턴 카운터 (3턴마다 점프)
        public int healerTurnCounter = 0;      // 힐러 턴 카운터
        public int bombTurnCounter = 0;        // 폭탄 고블린 턴 카운터 (3턴마다 설치)
        public bool isShielded = false;        // 현재 방패 활성 여부
        public int shieldHp = 3;              // 방패 내구도
        public int shieldTurnCounter = 0;     // 2턴 행동 카운터
        public List<GameObject> stuckDrills = null;  // 박힌 드릴 비주얼
        public Image shieldImage;             // 방패 Image
        public Text shieldHpText;             // 방패 HP 숫자 텍스트
        public Vector2 shieldOriginalAnchorPos; // 방패 Image 원래 anchoredPosition (생성 시 1회 저장)
        public Coroutine activeShieldBlockCo;   // 실행 중인 ShieldBlockEffect 코루틴 (중복 방지)

        // 데미지 팝업 누적 표시용
        public float lastDamageTime = -1f;           // 마지막 데미지 시간
        public int accumulatedDamage = 0;            // 누적 데미지 (짧은 시간 내)
        public GameObject activeDamagePopup;         // 현재 활성 데미지 팝업
        public Text activeDamageText;                // 팝업 텍스트 참조

        // HP 카운트다운 표시용
        public int displayedHp = -1;                 // 현재 화면에 표시 중인 HP (-1 = 초기화 안 됨)
        public bool isCountingDown = false;          // 카운트다운 코루틴 진행 중

        // ============================================================
        // 중앙화된 타입별 점수/면역 판정 (새 타입 추가 시 여기만 수정)
        // ============================================================

        /// <summary>드론 타겟팅 점수. 높을수록 우선 타격.</summary>
        public int DroneTargetScore
        {
            get
            {
                // 은신 상태 도둑 고블린은 드론 타겟팅 제외
                if (isThief && isStealth) return 0;
                if (isUndead) return 1; // 언데드는 낮은 우선순위
                if (isHeavy) return 6;
                if (isWitch) return 5;
                if (isBomb) return 5;
                if (isHealer) return 5;
                if (isWizard) return 4;
                if (isShielded) return 4;
                if (isThief) return 3;
                if (isArcher) return 3;
                if (isArmored) return 2;
                return 1;
            }
        }

        /// <summary>블록 낙하 데미지 면역 여부</summary>
        public bool IsImmuneToFallDamage => isArcher || isHealer || isWizard || isWitch || (isThief && isStealth);
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
        private BlockRemovalSystem blockRemovalSystem;
        private GoblinStageConfig currentConfig;
        public GoblinStageConfig CurrentConfig => currentConfig;
        private List<GoblinData> goblins = new List<GoblinData>();
        private int totalKills = 0;
        private int totalSpawned = 0; // 총 소환된 고블린 수 (미션 목표까지만 소환)

        // ★ 타입별 킬 카운터 (미션 초과 소환 방지용)
        private int regularKills = 0;
        private int armoredKills = 0;
        private int archerKills = 0;
        private int shieldKills = 0;
        private int bombKills = 0;
        private int healerKills = 0;
        private int heavyKills = 0;
        private int wizardKills = 0;
        private int thiefKills = 0;
        private int witchKills = 0;
        private int goblinLv2Kills = 0;
        private int armoredLv2Kills = 0;
        private int archerLv2Kills = 0;
        private int shieldLv2Kills = 0;

        // ★ 타입별 소환 카운터 (kills+alive 대신 확실한 소환 수 추적)
        private int regularSpawned = 0;
        private int armoredSpawned = 0;
        private int archerSpawned = 0;
        private int shieldSpawned = 0;
        private int bombSpawned = 0;
        private int healerSpawned = 0;
        private int heavySpawned = 0;
        private int wizardSpawned = 0;
        private int thiefSpawned = 0;
        private int witchSpawned = 0;
        private int goblinLv2Spawned = 0;
        private int armoredLv2Spawned = 0;
        private int archerLv2Spawned = 0;
        private int shieldLv2Spawned = 0;

        // 마녀 고유 ID 카운터 (각 마녀의 언데드 풀 구분용)
        private int nextWitchOwnerIndex = 0;

        // 2단계 소환 시스템
        private bool useWaveSpawn = false;      // 웨이브 소환 모드 활성 여부
        private bool secondWaveSpawned = false;  // 2차 소환 완료 여부
        private int firstWaveCount = 0;          // 1차 소환 수
        private int totalWaveTarget = 0;         // 전체 웨이브 소환 목표

        // 낙하 충돌 대미지로 사망한 고블린 대기열 (ProcessFalling 완료 후 처리)
        private List<GoblinData> pendingFallDeaths = new List<GoblinData>();

        // 프로시저럴 스프라이트 캐시
        private static Sprite goblinSprite;
        private static Sprite archerGoblinSprite;
        private static Sprite armoredGoblinSprite;
        private static Sprite shieldGoblinSprite;
        private static Sprite bombGoblinSprite;

        // Lv2 엘리트 전용 스프라이트 (피부색만 붉은색으로 교체)
        private static Sprite goblinLv2Sprite;
        private static Sprite archerLv2Sprite;
        private static Sprite armoredLv2Sprite;
        private static Sprite shieldLv2Sprite;

        // Lv2 엘리트 피부색 (진한 크림슨 레드 계열)
        private static readonly Color Lv2BodyColor = new Color(0.7f, 0.15f, 0.12f);
        private static readonly Color Lv2BodyDark = new Color(0.5f, 0.08f, 0.06f);
        private static readonly Color Lv2BodyLight = new Color(0.85f, 0.25f, 0.18f);
        private static readonly Color Lv2EarColor = new Color(0.6f, 0.1f, 0.08f);
        private static Sprite heavyGoblinSprite;
        private static Sprite wizardGoblinSprite;
        private static Sprite thiefGoblinSprite;
        private static Sprite witchGoblinSprite;
        private static Sprite undeadGoblinSprite;
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
        // shakeCount/shakeOriginalPos 제거됨 — VisualConstants 중앙 관리

        // ============================================================
        // 이벤트
        // ============================================================
        /// <summary>고블린 제거 시 호출 (totalKills, isArmored, isArcher, isShieldType, isBomb, isHealer, isHeavy, isWizard, isThief, isWitch)</summary>
        public event System.Action<int, bool, bool, bool, bool, bool, bool, bool, bool, bool, int> OnGoblinKilled;

        /// <summary>현재 보드 위 고블린 수</summary>
        public int AliveCount => goblins.Count(g => g.isAlive);

        /// <summary>현재 생존 고블린 수 반환 (MonsterSpawnController 연동용)</summary>
        public int GetAliveGoblinCount() => AliveCount;

        /// <summary>전체 미션 목표 수 반환 (MonsterSpawnController 연동용)</summary>
        public int GetTotalMissionTargetPublic()
        {
            return GetTotalMissionTarget(); // ★ 활성 미션 목표만 반환 (missionKillCount 폴백 제거)
        }

        /// <summary>총 제거 수</summary>
        public int TotalKills => totalKills;

        /// <summary>미션 완료 시 true → 추가 소환 중단 (이동/공격은 계속)</summary>
        public bool MissionComplete { get; set; } = false;

        /// <summary>
        /// 고블린 사망 시 타입별 킬 카운터 증가 (내부 헬퍼)
        /// </summary>
        private void IncrementTypeKill(GoblinData goblin)
        {
            if (goblin.isUndead) return; // 언데드는 킬 카운터 미집계
            if (goblin.isWitch) witchKills++;
            else if (goblin.isThief) thiefKills++;
            else if (goblin.isHeavy) heavyKills++;
            else if (goblin.isWizard) wizardKills++;
            else if (goblin.isBomb) bombKills++;
            else if (goblin.isHealer) healerKills++;
            else if (goblin.monsterLevel == 2 && goblin.isArcher) archerLv2Kills++;
            else if (goblin.monsterLevel == 2 && goblin.isArmored) armoredLv2Kills++;
            else if (goblin.monsterLevel == 2 && goblin.isShieldType) shieldLv2Kills++;
            else if (goblin.monsterLevel == 2) goblinLv2Kills++; // Lv2 몽둥이
            else if (goblin.isArcher) archerKills++;
            else if (goblin.isArmored) armoredKills++;
            else if (goblin.isShieldType) shieldKills++;
            else regularKills++;
        }

        /// <summary>
        /// StageManager에서 특정 고블린 타입의 미션 목표 수를 조회.
        /// 해당 타입 미션이 없으면 0 반환 (→ 소환하지 않음).
        /// </summary>
        private int GetMissionTargetForType(bool isArmored, bool isArcher, bool isShieldType = false,
            bool isBomb = false, bool isHealer = false, bool isHeavy = false, bool isWizard = false,
            bool isThief = false, bool isWitch = false,
            bool isGoblinLv2 = false, bool isArmoredLv2 = false, bool isArcherLv2 = false, bool isShieldLv2 = false)
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

            // ★ 활성 미션만 조회 (대기 미션은 활성화 전까지 소환 안 함)
            var missions = stageManager.GetMissionProgress();
            if (missions == null || missions.Length == 0)
            {
                Debug.LogWarning($"[GoblinSystem] GetMissionTargetForType: missions null 또는 빈 배열 (length={missions?.Length})");
                return 0; // ★ 미션 없으면 소환 없음 (missionKillCount 폴백 제거)
            }

            EnemyType targetType;
            if (isShieldLv2) targetType = EnemyType.ShieldGoblinLv2;
            else if (isArcherLv2) targetType = EnemyType.ArcherGoblinLv2;
            else if (isArmoredLv2) targetType = EnemyType.ArmoredGoblinLv2;
            else if (isGoblinLv2) targetType = EnemyType.GoblinLv2;
            else if (isWitch) targetType = EnemyType.WitchGoblin;
            else if (isThief) targetType = EnemyType.ThiefGoblin;
            else if (isWizard) targetType = EnemyType.WizardGoblin;
            else if (isHeavy) targetType = EnemyType.HeavyGoblin;
            else if (isHealer) targetType = EnemyType.HealerGoblin;
            else if (isBomb) targetType = EnemyType.BombGoblin;
            else if (isArcher) targetType = EnemyType.ArcherGoblin;
            else if (isArmored) targetType = EnemyType.ArmoredGoblin;
            else if (isShieldType) targetType = EnemyType.ShieldGoblin;
            else targetType = EnemyType.Goblin;

            // ★ 미완료 활성 미션만 합산
            int total = 0;
            foreach (var mp in missions)
            {
                if (mp.isComplete) continue;
                if (mp.mission.type == MissionType.RemoveEnemy &&
                    mp.mission.targetEnemyType == targetType)
                    total += mp.mission.targetCount;
            }
            return total;
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

            // ★ 활성 미션만 조회
            var missions = stageManager.GetMissionProgress();
            if (missions == null) return 0;

            // ★ 미완료 활성 미션만 합산
            int total = 0;
            foreach (var mp in missions)
            {
                if (mp.isComplete) continue;
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
            if (blockRemovalSystem == null)
                blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            currentConfig = config;
            totalKills = 0;
            totalSpawned = 0;
            regularKills = 0;
            armoredKills = 0;
            archerKills = 0;
            shieldKills = 0;
            bombKills = 0;
            healerKills = 0;
            heavyKills = 0;
            wizardKills = 0;
            regularSpawned = 0;
            armoredSpawned = 0;
            healerSpawned = 0;
            heavySpawned = 0;
            wizardSpawned = 0;
            archerSpawned = 0;
            shieldSpawned = 0;
            bombSpawned = 0;
            thiefSpawned = 0;
            goblinLv2Kills = 0;
            armoredLv2Kills = 0;
            archerLv2Kills = 0;
            shieldLv2Kills = 0;
            goblinLv2Spawned = 0;
            armoredLv2Spawned = 0;
            archerLv2Spawned = 0;
            shieldLv2Spawned = 0;
            useWaveSpawn = false;
            secondWaveSpawned = false;
            firstWaveCount = 0;
            totalWaveTarget = 0;
            MissionComplete = false;
            pendingImmediateSpawn = false;

            // 기존 폭탄 정리 (그리드 블록 데이터에서 제거)
            CleanupAllGoblinBombs();

            // 기존 고블린 정리
            CleanupAll();

            Debug.Log($"[GoblinSystem] 초기화: 소환 {config.minSpawnPerTurn}~{config.maxSpawnPerTurn}/턴, " +
                      $"목표 {config.missionKillCount}킬, 최대 {config.maxOnBoard}마리");
        }

        // ============================================================
        // 2단계 웨이브 소환 시스템
        // ============================================================

        /// <summary>
        /// 1차 소환: 게임 시작 시 전체 미션 몬스터 수의 40~50%를 소환.
        /// GameManager.StartGameCoroutine에서 호출.
        /// </summary>
        public IEnumerator SpawnFirstWave()
        {
            useWaveSpawn = true;
            int totalMission = GetTotalMissionTarget();
            totalWaveTarget = totalMission;

            // 활성 미션 몬스터 전체를 즉시 소환 (100%)
            firstWaveCount = totalMission;

            // maxOnBoard를 활성 미션 몬스터 총수에 맞춰 동적 확장
            if (currentConfig != null && currentConfig.maxOnBoard < totalMission)
            {
                Debug.Log($"[GoblinSystem] maxOnBoard 동적 확장: {currentConfig.maxOnBoard} → {totalMission}");
                currentConfig.maxOnBoard = totalMission;
            }

            Debug.Log($"[GoblinSystem] 1차 웨이브 소환: {firstWaveCount}/{totalWaveTarget}마리 (활성 미션 전체)");
            yield return StartCoroutine(SpawnWaveBatch(firstWaveCount));
        }

        /// <summary>
        /// 2차 소환: 이동 횟수 50% 시점에 나머지 몬스터를 모두 소환.
        /// GameManager.UseTurn에서 조건 충족 시 호출.
        /// </summary>
        public IEnumerator SpawnSecondWave()
        {
            if (secondWaveSpawned) yield break;
            secondWaveSpawned = true;

            int remaining = totalWaveTarget - firstWaveCount;
            if (remaining <= 0)
            {
                Debug.Log("[GoblinSystem] 2차 웨이브: 남은 소환 수 없음");
                yield break;
            }

            Debug.Log($"[GoblinSystem] 2차 웨이브 소환: {remaining}마리 (나머지 전부)");
            yield return StartCoroutine(SpawnWaveBatch(remaining));
        }

        /// <summary>
        /// 웨이브 배치 소환: 지정 수만큼 타입 비율에 맞게 소환.
        /// maxOnBoard 제한 적용, 초과분은 소환하지 않음.
        /// </summary>
        private IEnumerator SpawnWaveBatch(int targetCount)
        {
            if (currentConfig == null || targetCount <= 0) yield break;

            // 미션 완료 시 추가 소환 중단
            if (MissionComplete) yield break;

            // 보드 최대 동시 존재 수 제한
            int aliveCount = AliveCount;
            int boardSlots = currentConfig.maxOnBoard - aliveCount;
            if (boardSlots <= 0) yield break;

            // 타입별 미션 목표 조회
            int regularTarget = GetMissionTargetForType(false, false);
            int armoredTarget = GetMissionTargetForType(true, false);
            int archerTarget = GetMissionTargetForType(false, true);
            int shieldTarget = GetMissionTargetForType(false, false, true);
            int bombTarget = GetMissionTargetForType(false, false, false, true);
            int healerTarget = GetMissionTargetForType(false, false, false, false, true);
            int heavyTarget = GetMissionTargetForType(false, false, false, false, false, true);
            int wizardTarget = GetMissionTargetForType(false, false, false, false, false, false, true);
            int thiefTarget = GetMissionTargetForType(false, false, false, false, false, false, false, true);
            int witchTarget = GetMissionTargetForType(false, false, false, false, false, false, false, false, true);
            int goblinLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, true);
            int armoredLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, false, true);
            int archerLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, false, false, true);
            int shieldLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, false, false, false, true);

            int regularRemaining = Mathf.Max(0, regularTarget - regularSpawned);
            int armoredRemaining = Mathf.Max(0, armoredTarget - armoredSpawned);
            int archerRemaining = Mathf.Max(0, archerTarget - archerSpawned);
            int shieldRemaining = Mathf.Max(0, shieldTarget - shieldSpawned);
            int bombRemaining = Mathf.Max(0, bombTarget - bombSpawned);
            int healerRemaining = Mathf.Max(0, healerTarget - healerSpawned);
            int heavyRemaining = Mathf.Max(0, heavyTarget - heavySpawned);
            int wizardRemaining = Mathf.Max(0, wizardTarget - wizardSpawned);
            int thiefRemaining = Mathf.Max(0, thiefTarget - thiefSpawned);
            int witchRemaining = Mathf.Max(0, witchTarget - witchSpawned);
            int goblinLv2Remaining = Mathf.Max(0, goblinLv2Target - goblinLv2Spawned);
            int armoredLv2Remaining = Mathf.Max(0, armoredLv2Target - armoredLv2Spawned);
            int archerLv2Remaining = Mathf.Max(0, archerLv2Target - archerLv2Spawned);
            int shieldLv2Remaining = Mathf.Max(0, shieldLv2Target - shieldLv2Spawned);

            int totalMissionTarget = GetTotalMissionTarget();
            // ★ 미션 목표 기반 하드캡 (missionKillCount 폴백 제거 — 활성 미션에 없는 타입 소환 방지)
            int hardCap = Mathf.Max(0, totalMissionTarget - totalSpawned);

            int spawnCount = Mathf.Min(targetCount, boardSlots);
            spawnCount = Mathf.Min(spawnCount, hardCap);
            if (spawnCount <= 0) yield break;

            Debug.Log($"[GoblinSystem] 웨이브 배치 소환 계획: {spawnCount}마리 (목표={targetCount}, 슬롯={boardSlots}, 하드캡={hardCap})");

            // 소환 가능 위치 분류
            var occupiedPositions = new HashSet<HexCoord>(goblins.Where(g => g.isAlive).Select(g => g.position));
            // Heavy 고블린의 모든 점유 좌표도 등록
            foreach (var g in goblins.Where(g => g.isAlive && g.isHeavy && g.occupiedCoords != null))
                foreach (var c in g.occupiedCoords) occupiedPositions.Add(c);
            int gridRadius = hexGrid.GridRadius;

            var topRowCoords = new List<HexCoord>();
            var lowerRowCoords = new List<HexCoord>();
            var bottomRowCoords = new List<HexCoord>();
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
                    bottomRowCoords.Add(bot);
                }
            }

            var shuffledTop = topRowCoords.OrderBy(x => Random.value).ToList();
            var shuffledLower = lowerRowCoords.OrderBy(x => Random.value).ToList();
            var shuffledBottom = bottomRowCoords.OrderBy(x => Random.value).ToList();
            var usedPositions = new HashSet<HexCoord>(occupiedPositions);

            var pendingSpawns = new List<(GoblinData goblin, bool isHeavy)>();
            int wizardsSpawnedThisWave = 0; // ★ 이번 웨이브 내 마법사 소환 수 (1회 제한)

            for (int i = 0; i < spawnCount; i++)
            {
                bool isArcher = false;
                bool isArmored = false;
                bool isShieldType = false;
                bool isBombType = false;
                bool isHealerType = false;
                bool isHeavyType = false;
                bool isThiefType = false;
                int goblinHp = 5;
                int spawnShieldHp = currentConfig.shieldHp;

                int availableTop = 0;
                foreach (var c in shuffledTop) { if (!usedPositions.Contains(c)) availableTop++; }
                int availableBottom = 0;
                foreach (var c in shuffledBottom) { if (!usedPositions.Contains(c)) availableBottom++; }
                int availableLower = 0;
                foreach (var c in shuffledLower) { if (!usedPositions.Contains(c)) availableLower++; }

                // Heavy 고블린: 삼각형 3블록 위치 탐색 (별도 위치 할당)
                if (heavyRemaining > 0)
                {
                    var heavyCoords = FindHeavySpawnPosition(usedPositions);
                    if (heavyCoords != null)
                    {
                        isHeavyType = true;
                        goblinHp = currentConfig.heavyGoblinHp;
                        heavyRemaining--;

                        // Heavy 고블린은 3개 좌표 점유 — position은 첫 번째 좌표
                        foreach (var c in heavyCoords)
                            usedPositions.Add(c);

                        GoblinData heavyGoblin = new GoblinData
                        {
                            position = heavyCoords[0],
                            hp = goblinHp,
                            maxHp = goblinHp,
                            displayedHp = goblinHp,
                            isAlive = true,
                            isHeavy = true,
                            occupiedCoords = new List<HexCoord>(heavyCoords)
                        };

                        goblins.Add(heavyGoblin);
                        totalSpawned++;
                        heavySpawned++;

                        Debug.Log($"[GoblinSystem] 웨이브 헤비급 고블린 소환 at ({heavyCoords[0]}), 점유={string.Join(",", heavyCoords)}, HP={goblinHp}");
                        pendingSpawns.Add((heavyGoblin, true));
                        continue;
                    }
                }

                // Wizard 고블린: 소환 영역(상단)에서만 소환 — ★ 같은 웨이브에서 1명만 소환
                // 여러 턴에 걸쳐 2명 이상 존재 가능, 같은 웨이브에서만 1명 제한
                if (wizardRemaining > 0 && availableTop > 0 && wizardsSpawnedThisWave < 1)
                {
                    HexCoord wizCoord = default;
                    bool wizFound = false;
                    foreach (var c in shuffledTop)
                    {
                        if (!usedPositions.Contains(c)) { wizCoord = c; wizFound = true; break; }
                    }
                    if (wizFound)
                    {
                        usedPositions.Add(wizCoord);
                        wizardRemaining--;

                        int wizHp = currentConfig.wizardGoblinHp;
                        GoblinData wizGoblin = new GoblinData
                        {
                            position = wizCoord,
                            hp = wizHp,
                            maxHp = wizHp,
                            displayedHp = wizHp,
                            isAlive = true,
                            isWizard = true
                        };

                        goblins.Add(wizGoblin);
                        totalSpawned++;
                        wizardSpawned++;
                        wizardsSpawnedThisWave++; // ★ 웨이브 내 소환 카운트

                        Debug.Log($"[GoblinSystem] 웨이브 마법사 고블린 소환 at ({wizCoord}), HP=3, 누적소환={totalSpawned}");
                        pendingSpawns.Add((wizGoblin, false));
                        continue;
                    }
                }

                // 도둑 고블린: lowerRowCoords에서 소환
                if (thiefRemaining > 0 && availableLower > 0)
                {
                    HexCoord thiefCoord = default;
                    bool thiefFound = false;
                    foreach (var c in shuffledLower)
                    {
                        if (!usedPositions.Contains(c)) { thiefCoord = c; thiefFound = true; break; }
                    }
                    if (thiefFound)
                    {
                        usedPositions.Add(thiefCoord);
                        thiefRemaining--;

                        GoblinData thiefGoblin = new GoblinData
                        {
                            position = thiefCoord,
                            hp = currentConfig.thiefGoblinHp,
                            maxHp = currentConfig.thiefGoblinHp,
                            displayedHp = currentConfig.thiefGoblinHp,
                            isAlive = true,
                            isThief = true,
                            thiefAttackPhase = 0,
                            thiefVisitedPositions = new List<HexCoord> { thiefCoord }
                        };

                        goblins.Add(thiefGoblin);
                        totalSpawned++;
                        thiefSpawned++;

                        Debug.Log($"[GoblinSystem] 웨이브 도둑 고블린 소환 at ({thiefCoord}), HP={currentConfig.thiefGoblinHp}, 누적소환={totalSpawned}");
                        pendingSpawns.Add((thiefGoblin, false));
                        continue;
                    }
                }

                // Witch 고블린: 소환 영역(상단)에서만 소환 — 같은 웨이브에서 1명만
                if (witchRemaining > 0 && availableTop > 0)
                {
                    HexCoord witchCoord = default;
                    bool witchFound = false;
                    foreach (var c in shuffledTop)
                    {
                        if (!usedPositions.Contains(c)) { witchCoord = c; witchFound = true; break; }
                    }
                    if (witchFound)
                    {
                        usedPositions.Add(witchCoord);
                        witchRemaining--;

                        int witchHp = currentConfig.witchGoblinHp;
                        GoblinData witchGoblin = new GoblinData
                        {
                            position = witchCoord,
                            hp = witchHp,
                            maxHp = witchHp,
                            displayedHp = witchHp,
                            isAlive = true,
                            isWitch = true,
                            witchOwnerIndex = nextWitchOwnerIndex++,
                            witchSummonCounter = 0,
                            witchMoveCounter = 0
                        };

                        goblins.Add(witchGoblin);
                        totalSpawned++;
                        witchSpawned++;

                        Debug.Log($"[GoblinSystem] 웨이브 마녀 고블린 소환 at ({witchCoord}), HP={witchHp}, ownerIdx={witchGoblin.witchOwnerIndex}");
                        pendingSpawns.Add((witchGoblin, false));
                        continue;
                    }
                }

                // Lv2 타입을 포함한 전체 잔여량으로 가중 랜덤
                bool isLv2 = false;
                int totalPool = regularRemaining + armoredRemaining + archerRemaining + shieldRemaining
                    + bombRemaining + healerRemaining
                    + goblinLv2Remaining + armoredLv2Remaining + archerLv2Remaining + shieldLv2Remaining;
                if (totalPool <= 0) break;

                float roll = Random.value * totalPool;
                float threshold = 0f;

                // 폭탄 → 힐러 → 방패 → 갑옷 → 궁수 → Lv2방패 → Lv2갑옷 → Lv2궁수 → Lv2몽둥이 → 기본 순서
                threshold += bombRemaining;
                if (roll < threshold && bombRemaining > 0 && (availableLower > 0 || availableTop > 0))
                {
                    isBombType = true;
                    goblinHp = currentConfig.bombGoblinHp;
                    bombRemaining--;
                }
                else
                {
                    threshold += healerRemaining;
                    if (roll < threshold && healerRemaining > 0 && availableTop > 0)
                    {
                        bool hasDamaged = goblins.Any(g => g.isAlive && g.hp < g.maxHp);
                        if (hasDamaged)
                        {
                            isHealerType = true;
                            goblinHp = 2;
                            healerRemaining--;
                        }
                    }
                }
                if (!isBombType && !isHealerType)
                {
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
                            // ★ Lv2 타입들 (HP 2배) — 직접 분기 처리
                            else
                            {
                                threshold += shieldLv2Remaining;
                                if (roll < threshold && shieldLv2Remaining > 0 && availableBottom > 0)
                                {
                                    isShieldType = true; isLv2 = true;
                                    goblinHp = currentConfig.shieldGoblinHp * 2;
                                    spawnShieldHp = currentConfig.shieldHp * 2;
                                    shieldLv2Remaining--;
                                }
                                else
                                {
                                    threshold += armoredLv2Remaining;
                                    if (roll < threshold && armoredLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                    {
                                        isArmored = true; isLv2 = true;
                                        goblinHp = currentConfig.armoredHp * 2;
                                        armoredLv2Remaining--;
                                    }
                                    else
                                    {
                                        threshold += archerLv2Remaining;
                                        if (roll < threshold && archerLv2Remaining > 0 && availableTop > 0)
                                        {
                                            isArcher = true; isLv2 = true;
                                            goblinHp = currentConfig.archerHp * 2;
                                            archerLv2Remaining--;
                                        }
                                        else
                                        {
                                            threshold += goblinLv2Remaining;
                                            if (roll < threshold && goblinLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                            {
                                                isLv2 = true;
                                                goblinHp = 10; // 기본5 × 2
                                                goblinLv2Remaining--;
                                            }
                                            else if (regularRemaining > 0 && (availableLower > 0 || availableTop > 0))
                                            {
                                                regularRemaining--;
                                            }
                                            else
                                            {
                                                // 위치 부족 폴백: 남은 타입 중 소환 가능한 것 선택
                                                if (bombRemaining > 0 && (availableLower > 0 || availableTop > 0))
                                                { isBombType = true; goblinHp = currentConfig.bombGoblinHp; bombRemaining--; }
                                                else if (shieldLv2Remaining > 0 && availableBottom > 0)
                                                { isShieldType = true; isLv2 = true; goblinHp = currentConfig.shieldGoblinHp * 2; spawnShieldHp = currentConfig.shieldHp * 2; shieldLv2Remaining--; }
                                                else if (shieldRemaining > 0 && availableBottom > 0)
                                                { isShieldType = true; goblinHp = currentConfig.shieldGoblinHp; shieldRemaining--; }
                                                else if (armoredLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                                { isArmored = true; isLv2 = true; goblinHp = currentConfig.armoredHp * 2; armoredLv2Remaining--; }
                                                else if (armoredRemaining > 0 && (availableLower > 0 || availableTop > 0))
                                                { isArmored = true; goblinHp = currentConfig.armoredHp; armoredRemaining--; }
                                                else if (archerLv2Remaining > 0 && availableTop > 0)
                                                { isArcher = true; isLv2 = true; goblinHp = currentConfig.archerHp * 2; archerLv2Remaining--; }
                                                else if (archerRemaining > 0 && availableTop > 0)
                                                { isArcher = true; goblinHp = currentConfig.archerHp; archerRemaining--; }
                                                else if (goblinLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                                { isLv2 = true; goblinHp = 10; goblinLv2Remaining--; }
                                                else { continue; }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // 위치 배정: 궁수→최상단, 방패→최하단, 나머지→중간+하단
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

                usedPositions.Add(coord);

                GoblinData newGoblin = new GoblinData
                {
                    position = coord,
                    hp = goblinHp,
                    maxHp = goblinHp,
                    displayedHp = goblinHp,
                    isAlive = true,
                    isArcher = isArcher,
                    isArmored = isArmored,
                    isBomb = isBombType,
                    bombTurnCounter = 1,
                    isHealer = isHealerType,
                    healerTurnCounter = 0,
                    isShieldType = isShieldType,
                    isShielded = isShieldType,
                    shieldHp = isShieldType ? spawnShieldHp : 0,
                    shieldTurnCounter = 0,
                    stuckDrills = isShieldType ? new List<GameObject>() : null,
                    monsterLevel = isLv2 ? 2 : 1
                };

                goblins.Add(newGoblin);
                totalSpawned++;
                if (isBombType) bombSpawned++;
                else if (isHealerType) healerSpawned++;
                else if (isLv2 && isArcher) archerLv2Spawned++;
                else if (isLv2 && isArmored) armoredLv2Spawned++;
                else if (isLv2 && isShieldType) shieldLv2Spawned++;
                else if (isLv2) goblinLv2Spawned++;
                else if (isArcher) archerSpawned++;
                else if (isArmored) armoredSpawned++;
                else if (isShieldType) shieldSpawned++;
                else regularSpawned++;

                if (isHealerType)
                    PerformHeal(newGoblin);

                string typeStr = isHealerType ? "힐러" : isBombType ? "폭탄" : isArcher ? "궁수" : isArmored ? "갑옷" : isShieldType ? "방패" : "기본";
                Debug.Log($"[GoblinSystem] 웨이브 {typeStr} 고블린 소환 at ({coord}), HP={goblinHp}, 누적소환={totalSpawned}");
                pendingSpawns.Add((newGoblin, false));
            }

            // ★ 순차적 빠른 소환 연출: 셔플 후 촘촘한 간격으로 다이나믹하게 소환
            yield return StartCoroutine(RapidSequentialSpawn(pendingSpawns));

            // ★ 마녀 초기 소환: 비주얼 생성 후 언데드 3마리 즉시 소환
            yield return StartCoroutine(WitchInitialSummon());

            int totalMT = GetTotalMissionTarget();
            Debug.Log($"[GoblinSystem] 웨이브 배치 {pendingSpawns.Count}마리 소환 완료 | 보드 {AliveCount}/{currentConfig.maxOnBoard} | " +
                      $"누적소환 {totalSpawned}/{totalMT}");
        }

        /// <summary>
        /// 외부(MonsterSpawnController)에서 호출 가능한 배치 소환 공개 래퍼.
        /// 내부 SpawnWaveBatch를 그대로 위임한다.
        /// ★ MonsterSpawnController 사용 시 SpawnGoblins(턴당 소환)를 비활성화.
        /// </summary>
        public IEnumerator SpawnWaveBatchPublic(int targetCount)
        {
            useWaveSpawn = true; // ★ MonsterSpawnController가 소환을 관리하므로 턴당 SpawnGoblins 비활성화
            yield return StartCoroutine(SpawnWaveBatch(targetCount));
        }

        // ============================================================
        // Heavy 고블린 점유 좌표 시스템
        // ============================================================

        /// <summary>
        /// 모든 생존 Heavy 고블린이 점유하는 좌표 집합 반환.
        /// InputSystem에서 회전 클러스터 점유 블록 회전 불가 판정에 사용.
        /// </summary>
        public HashSet<HexCoord> GetHeavyOccupiedCoords()
        {
            var coords = new HashSet<HexCoord>();
            foreach (var g in goblins)
            {
                if (g.isAlive && g.isHeavy && g.occupiedCoords != null)
                {
                    foreach (var c in g.occupiedCoords)
                        coords.Add(c);
                }
            }
            return coords;
        }

        /// <summary>
        /// 특정 좌표가 Heavy 고블린 점유 좌표인지 확인
        /// </summary>
        public bool IsHeavyOccupied(HexCoord coord)
        {
            foreach (var g in goblins)
            {
                if (g.isAlive && g.isHeavy && g.occupiedCoords != null && g.occupiedCoords.Contains(coord))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 살아있는 모든 Heavy 고블린 목록 반환
        /// </summary>
        public List<GoblinData> GetHeavyGoblins()
        {
            var result = new List<GoblinData>();
            foreach (var g in goblins)
            {
                if (g.isAlive && g.isHeavy)
                    result.Add(g);
            }
            return result;
        }

        /// <summary>
        /// Heavy 고블린 소환 시 유효한 삼각형 3블록 위치를 탐색.
        /// flat-top 헥사에서 인접 3블록이 면으로 맞닿는 삼각형 구조 탐색.
        /// 3블록 모두 비어있고, 다른 고블린이 없는 위치만 반환.
        /// </summary>
        private List<HexCoord> FindHeavySpawnPosition(HashSet<HexCoord> usedPositions)
        {
            if (hexGrid == null) return null;

            int gridRadius = hexGrid.GridRadius;
            var spawnArea = new List<HexCoord>();

            // 소환 영역: 상단 3줄 (기존 소환 영역과 동일)
            for (int q = -gridRadius; q <= gridRadius; q++)
            {
                int rMin = hexGrid.GetTopR(q);
                for (int row = 1; row <= 3; row++)
                    spawnArea.Add(new HexCoord(q, rMin - row));
            }

            // 랜덤 순서로 탐색
            var shuffled = spawnArea.OrderBy(x => Random.value).ToList();

            // 삼각형 패턴: 중심 + 인접 2블록 (면으로 맞닿는 삼각형)
            // flat-top hex에서 삼각형 패턴:
            // 아래 삼각형: center, center+(1,0), center+(0,1)
            // 위 삼각형: center, center+(-1,0), center+(0,-1)
            // 등 다양한 3블록 조합
            HexCoord[][] triangleOffsets = new HexCoord[][]
            {
                // 아래쪽 삼각형 (∇ 형태)
                new HexCoord[] { new HexCoord(0,0), new HexCoord(1,0), new HexCoord(0,1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,1), new HexCoord(0,1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,0), new HexCoord(-1,1) },
                // 위쪽 삼각형 (△ 형태)
                new HexCoord[] { new HexCoord(0,0), new HexCoord(1,-1), new HexCoord(1,0) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(0,-1), new HexCoord(1,-1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,0), new HexCoord(0,-1) },
            };

            foreach (var center in shuffled)
            {
                foreach (var offsets in triangleOffsets)
                {
                    var coords = new List<HexCoord>();
                    bool valid = true;

                    for (int i = 0; i < 3; i++)
                    {
                        var coord = new HexCoord(center.q + offsets[i].q, center.r + offsets[i].r);
                        // 소환 영역 범위 확인
                        if (Mathf.Abs(coord.q) > gridRadius)
                        {
                            valid = false;
                            break;
                        }
                        int rMin = hexGrid.GetTopR(coord.q);
                        int rMax = Mathf.Min(gridRadius, -coord.q + gridRadius);
                        if (coord.r < rMin - 3 || coord.r > rMax)
                        {
                            valid = false;
                            break;
                        }
                        // 다른 고블린/기존 점유 확인
                        if (usedPositions.Contains(coord))
                        {
                            valid = false;
                            break;
                        }
                        coords.Add(coord);
                    }

                    if (valid && coords.Count == 3)
                        return coords;
                }
            }

            // 폴백: 삼각형 위치를 찾지 못한 경우, 빈 단일 좌표에 소환
            // (3블록 점유 대신 1블록 점유로 축소 — 소환 자체가 스킵되는 것 방지)
            foreach (var center in shuffled)
            {
                if (!usedPositions.Contains(center))
                {
                    Debug.LogWarning($"[GoblinSystem] FindHeavySpawnPosition: 삼각형 위치 없음 → 단일 좌표 폴백 ({center})");
                    return new List<HexCoord> { center, center, center }; // 3개 동일 좌표 (occupiedCoords 크기 유지)
                }
            }

            Debug.LogError("[GoblinSystem] FindHeavySpawnPosition: 소환 가능 위치 완전 없음");
            return null; // 소환 영역 자체가 가득 찬 경우
        }

        /// <summary>
        /// Heavy 고블린 이동/점프 시 새 점유 좌표에 있는 일반 고블린을 밀어낸다.
        /// heavyCoords 중심에서 가장 먼 인접 빈 칸으로 이동시킴.
        /// </summary>
        private void PushGoblinAway(GoblinData goblin, List<HexCoord> heavyCoords)
        {
            if (goblin == null || !goblin.isAlive || goblin.isHeavy) return;
            if (heavyCoords == null || heavyCoords.Count == 0) return;

            // Heavy 좌표 중심 계산
            float centerQ = 0f, centerR = 0f;
            foreach (var c in heavyCoords) { centerQ += c.q; centerR += c.r; }
            centerQ /= heavyCoords.Count;
            centerR /= heavyCoords.Count;

            // 점유 좌표 수집 (다른 고블린 + Heavy의 새 좌표)
            var occupiedCoords = new HashSet<HexCoord>();
            foreach (var g in goblins.Where(g => g.isAlive && g != goblin))
            {
                if (g.isHeavy && g.occupiedCoords != null)
                    foreach (var c in g.occupiedCoords) occupiedCoords.Add(c);
                else
                    occupiedCoords.Add(g.position);
            }
            foreach (var c in heavyCoords)
                occupiedCoords.Add(c);

            // 인접 6방향 중 비어있는 칸 탐색
            var candidates = new List<(HexCoord coord, float dist)>();
            int gridRadius = hexGrid.GridRadius;

            foreach (var dir in HexCoord.Directions)
            {
                var candidate = goblin.position + dir;

                // 범위 확인 (그리드 + 확장 3줄)
                if (Mathf.Abs(candidate.q) > gridRadius) continue;
                int rMin = hexGrid.GetTopR(candidate.q);
                int rMax = Mathf.Min(gridRadius, -candidate.q + gridRadius);
                if (candidate.r < rMin - 3 || candidate.r > rMax) continue;

                // 점유 확인
                if (occupiedCoords.Contains(candidate)) continue;

                // 그리드 내부에 블록이 있으면 이동 불가
                if (hexGrid.IsInsideGrid(candidate))
                {
                    HexBlock block = hexGrid.GetBlock(candidate);
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                        continue;
                }

                // Heavy 중심에서의 거리 계산 (멀수록 우선)
                float dist = Mathf.Abs(candidate.q - centerQ) + Mathf.Abs(candidate.r - centerR)
                           + Mathf.Abs((-candidate.q - candidate.r) - (-centerQ - centerR));
                candidates.Add((candidate, dist));
            }

            HexCoord newPos;
            if (candidates.Count > 0)
            {
                // 가장 먼 칸 선택
                candidates.Sort((a, b) => b.dist.CompareTo(a.dist));
                newPos = candidates[0].coord;
            }
            else
            {
                // 빈 칸 없으면 r이 작은 방향(소환 영역) 쪽으로 강제 이동
                var upDirs = new HexCoord[]
                {
                    new HexCoord(0, -1),   // 바로 위
                    new HexCoord(1, -1),   // 오른쪽 위
                    new HexCoord(-1, 0)    // 왼쪽 위
                };
                HexCoord fallback = goblin.position;
                foreach (var dir in upDirs)
                {
                    var candidate = goblin.position + dir;
                    if (Mathf.Abs(candidate.q) > gridRadius) continue;
                    int rMin = hexGrid.GetTopR(candidate.q);
                    if (candidate.r < rMin - 3) continue;
                    int rMax = Mathf.Min(gridRadius, -candidate.q + gridRadius);
                    if (candidate.r > rMax) continue;
                    if (!occupiedCoords.Contains(candidate))
                    {
                        fallback = candidate;
                        break;
                    }
                }
                newPos = fallback;
            }

            if (newPos == goblin.position) return; // 이동 불가

            Debug.Log($"[GoblinSystem] 고블린 밀어내기: ({goblin.position}) → ({newPos})");
            goblin.position = newPos;

            // 비주얼 위치 즉시 갱신
            if (goblin.visualObject != null && hexGrid != null)
            {
                RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = hexGrid.CalculateFlatTopHexPosition(newPos);
            }
        }

        /// <summary>
        /// Heavy 고블린의 새 좌표에 있는 일반 고블린들을 모두 밀어냄.
        /// </summary>
        private void PushGoblinsFromHeavyCoords(List<HexCoord> heavyCoords, GoblinData heavySelf)
        {
            if (heavyCoords == null || heavyCoords.Count == 0) return;
            var heavySet = new HashSet<HexCoord>(heavyCoords);

            // Heavy의 새 좌표에 있는 일반 고블린 찾기
            var toPush = new List<GoblinData>();
            foreach (var g in goblins)
            {
                if (!g.isAlive || g == heavySelf || g.isHeavy) continue;
                if (heavySet.Contains(g.position))
                    toPush.Add(g);
            }

            // 밀어내기 실행
            foreach (var g in toPush)
                PushGoblinAway(g, heavyCoords);
        }

        /// <summary>
        /// Heavy 고블린의 occupiedCoords를 delta만큼 이동
        /// </summary>
        public void MoveHeavyOccupiedCoords(GoblinData goblin, HexCoord delta)
        {
            if (goblin.occupiedCoords == null) return;
            for (int i = 0; i < goblin.occupiedCoords.Count; i++)
            {
                goblin.occupiedCoords[i] = new HexCoord(
                    goblin.occupiedCoords[i].q + delta.q,
                    goblin.occupiedCoords[i].r + delta.r);
            }
        }

        /// <summary>
        /// Heavy 고블린의 occupiedCoords 중심(무게중심) 위치를 월드 좌표로 계산
        /// </summary>
        public Vector2 CalculateHeavyCenterPosition(List<HexCoord> coords)
        {
            if (coords == null || coords.Count == 0) return Vector2.zero;
            Vector2 sum = Vector2.zero;
            foreach (var c in coords)
                sum += hexGrid.CalculateFlatTopHexPosition(c);
            return sum / coords.Count;
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
            CleanupAllGoblinBombs();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ============================================================
        // 에디터 전용: 고블린 즉시 생성/제거 (EditorTestSystem에서 호출)
        // ============================================================

        /// <summary>
        /// 에디터에서 특정 좌표에 고블린을 즉시 배치한다.
        /// GoblinSystem이 초기화되지 않았으면 더미 config로 자동 초기화.
        /// </summary>
        public void EditorSpawnGoblin(HexCoord position, bool isArmored, bool isArcher, bool isShieldType,
            bool isBombType = false, bool isHealerType = false, bool isHeavyType = false, bool isWizardType = false,
            bool isThiefType = false, bool isWitchType = false, int monsterLevel = 1)
        {
            if (hexGrid == null)
                hexGrid = FindObjectOfType<HexGrid>();
            if (hexGrid == null) return;

            // GoblinSystem이 비활성 상태면 더미 config로 초기화
            if (currentConfig == null)
            {
                var dummyConfig = new GoblinStageConfig
                {
                    minSpawnPerTurn = 0,
                    maxSpawnPerTurn = 0,
                    missionKillCount = 99,
                    maxOnBoard = 20
                };
                Initialize(hexGrid, dummyConfig);
            }

            // 해당 위치에 이미 고블린이 있으면 먼저 제거
            EditorRemoveGoblin(position);

            // 고블린 데이터 생성
            var goblin = new GoblinData();
            goblin.position = position;
            goblin.isArmored = isArmored;
            goblin.isArcher = isArcher;
            goblin.isShieldType = isShieldType;
            goblin.isShielded = isShieldType;
            goblin.isBomb = isBombType;
            goblin.isHealer = isHealerType;
            goblin.isHeavy = isHeavyType;
            goblin.isWizard = isWizardType;
            goblin.isThief = isThiefType;
            goblin.isWitch = isWitchType;
            goblin.monsterLevel = monsterLevel;

            // 타입별 HP 설정
            if (isWitchType)
            {
                goblin.hp = currentConfig.witchGoblinHp;
                goblin.maxHp = currentConfig.witchGoblinHp;
                goblin.witchOwnerIndex = nextWitchOwnerIndex++;
            }
            else if (isThiefType)
            {
                goblin.hp = 12;
                goblin.maxHp = 12;
            }
            else if (isWizardType)
            {
                goblin.hp = 3;
                goblin.maxHp = 3;
            }
            else if (isHeavyType)
            {
                goblin.hp = currentConfig.heavyGoblinHp;
                goblin.maxHp = currentConfig.heavyGoblinHp;
            }
            else if (isBombType)
            {
                goblin.hp = currentConfig.bombGoblinHp;
                goblin.maxHp = currentConfig.bombGoblinHp;
                goblin.bombTurnCounter = 1;
            }
            else if (isHealerType)
            {
                goblin.hp = 2;
                goblin.maxHp = 2;
                goblin.healerTurnCounter = 0;
            }
            else if (isArcher)
            {
                goblin.hp = currentConfig.archerHp;
                goblin.maxHp = currentConfig.archerHp;
            }
            else if (isArmored)
            {
                goblin.hp = currentConfig.armoredHp;
                goblin.maxHp = currentConfig.armoredHp;
            }
            else if (isShieldType)
            {
                goblin.hp = currentConfig.shieldGoblinHp;
                goblin.maxHp = currentConfig.shieldGoblinHp;
                goblin.shieldHp = currentConfig.shieldHp;
            }
            else
            {
                goblin.hp = 5;
                goblin.maxHp = 5;
            }

            // Lv2: HP 2배
            if (monsterLevel >= 2)
            {
                goblin.hp *= 2;
                goblin.maxHp *= 2;
                goblin.displayedHp = goblin.hp;
                if (isShieldType) goblin.shieldHp *= 2;
            }

            // Heavy 고블린: occupiedCoords 설정 (비주얼 생성 전에 필요)
            if (isHeavyType)
            {
                goblin.occupiedCoords = new List<HexCoord> { position };
                var neighbor1 = new HexCoord(position.q + 1, position.r);
                var neighbor2 = new HexCoord(position.q, position.r + 1);
                goblin.occupiedCoords.Add(neighbor1);
                goblin.occupiedCoords.Add(neighbor2);
            }

            goblins.Add(goblin);

            // 비주얼 즉시 생성 (포털 이펙트 없이)
            Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(position);
            if (isHeavyType)
            {
                // Heavy: 점유 3칸 중앙 위치에 큰 전용 비주얼
                Vector2 centerPos = Vector2.zero;
                foreach (var oc in goblin.occupiedCoords)
                    centerPos += hexGrid.CalculateFlatTopHexPosition(oc);
                centerPos /= goblin.occupiedCoords.Count;
                CreateHeavyGoblinVisual(goblin, centerPos);
            }
            else
            {
                CreateGoblinVisual(goblin, worldPos);
            }
            if (goblin.visualObject != null)
                goblin.visualObject.GetComponent<RectTransform>().localScale = Vector3.one;

            UpdateHPBar(goblin);
            Debug.Log($"[GoblinSystem] 에디터 배치: ({position}) armored={isArmored} archer={isArcher} shield={isShieldType} bomb={isBombType} healer={isHealerType} heavy={isHeavyType} HP={goblin.hp}");
        }

        /// <summary>
        /// 에디터에서 특정 좌표의 고블린을 즉시 제거한다.
        /// </summary>
        public void EditorRemoveGoblin(HexCoord position)
        {
            var goblin = goblins.Find(g => g.isAlive && g.position == position);
            if (goblin == null) return;

            goblin.isAlive = false;
            if (goblin.visualObject != null)
                Destroy(goblin.visualObject);
            goblins.Remove(goblin);
            Debug.Log($"[GoblinSystem] 에디터 제거: ({position})");
        }

        /// <summary>
        /// 에디터에서 특정 좌표에 있는 고블린의 타입을 조회한다.
        /// 반환: 0=없음, 1=몽둥이, 2=갑옷, 3=궁수, 4=방패, 5=폭탄, 6=헤비, 7=힐러, 8=마법사, 9=도둑, 10=마녀
        ///       11=몽둥이Lv2, 12=갑옷Lv2, 13=궁수Lv2, 14=방패Lv2
        /// </summary>
        public int EditorGetGoblinType(HexCoord position)
        {
            var goblin = goblins.Find(g => g.isAlive && g.position == position);
            // Heavy 고블린: occupiedCoords로도 탐색
            if (goblin == null)
                goblin = goblins.Find(g => g.isAlive && g.isHeavy && g.occupiedCoords != null && g.occupiedCoords.Contains(position));
            if (goblin == null) return 0;
            if (goblin.isWitch) return 10;
            if (goblin.isThief) return 9;
            if (goblin.isWizard) return 8;
            if (goblin.isHeavy) return 6;
            if (goblin.isHealer) return 7;
            if (goblin.isBomb) return 5;
            // Lv2 타입 (monsterLevel == 2)
            if (goblin.monsterLevel >= 2)
            {
                if (goblin.isArmored) return 12;
                if (goblin.isArcher) return 13;
                if (goblin.isShieldType) return 14;
                return 11; // 몽둥이 Lv2
            }
            if (goblin.isArmored) return 2;
            if (goblin.isArcher) return 3;
            if (goblin.isShieldType) return 4;
            if (goblin.isUndead) return 0; // 언데드: 가장 낮은 순서
            return 1; // 몽둥이
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

            // ★ 드릴 회피 보호 플래그 초기화 (이전 턴 캐스케이드에서 설정된 플래그)
            ClearStealthProtection();

            // ★ justSummoned 플래그 초기화 (이전 턴에 소환된 언데드의 행동 허용)
            foreach (var g in goblins)
                g.justSummoned = false;

            // 0단계: 고블린 폭탄 카운트다운 감소 + 폭발 (매 턴 시작 시)
            yield return StartCoroutine(DecrementBombCountdowns());

            // 1단계: 근접 고블린 이동 (블록 공격+파괴 통합) — 궁수/폭탄 고블린 제외
            yield return StartCoroutine(MoveAllGoblins());

            // ★ 이동 후 위치 겹침 검증 + 해소 (우선순위 기반)
            CheckAndResolveOverlap();

            // 1.4단계: Heavy 고블린 이동 (3칸 전체 이동)
            yield return StartCoroutine(HeavyGoblinMovePhase());

            // 1.5단계: 폭탄 고블린 페이즈
            yield return StartCoroutine(BombGoblinPhase());

            // 1.6단계: 힐러 고블린 페이즈
            yield return StartCoroutine(HealerGoblinPhase());

            // 1.7단계: 마법사 고블린 페이즈
            yield return StartCoroutine(WizardGoblinPhase());

            // 1.8단계: 도둑 고블린 행동
            yield return StartCoroutine(ThiefGoblinPhase());

            // 1.9단계: 마녀 고블린 페이즈 (언데드 소환 + 이동)
            yield return StartCoroutine(WitchGoblinPhase());

            // 1.95단계: 언데드 행동 (이동 + 공격)
            yield return StartCoroutine(UndeadGoblinPhase());

            // ★ 설치 후 위치 겹침 검증 (우선순위 기반)
            CheckAndResolveOverlap();

            // 1.98단계: 마녀 언데드 재소환 (모든 몬스터 행동 후 처리)
            yield return StartCoroutine(ProcessPendingWitchResummon());

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

            // ★ 대기 미션 활성화로 예약된 지연 소환 실행
            if (pendingImmediateSpawn)
            {
                pendingImmediateSpawn = false;
                yield return StartCoroutine(ExecuteDeferredSpawn());
            }

            // ★ 스폰 후 위치 겹침 검증 + 해소 (우선순위 기반)
            CheckAndResolveOverlap();

            // 4단계: 크랙/쉘 블록 엣지 연결 체크
            yield return StartCoroutine(CheckAndApplyEdgeConnection());
        }

        /// <summary>
        /// 고블린 타입별 우선순위 반환. 숫자가 클수록 강한 고블린.
        /// Heavy=6, Shield=5, BombGoblin=4, Armored/Archer=3, Healer=2, Regular=1
        /// </summary>
        private int GetGoblinPriority(GoblinData g)
        {
            if (g.isHeavy) return 6;
            if (g.isWitch) return 5;
            if (g.isShieldType) return 5;
            if (g.isWizard) return 4;
            if (g.isBomb) return 4;
            if (g.isArmored || g.isArcher) return 3;
            if (g.isThief) return 3;
            if (g.isHealer) return 2;
            if (g.isUndead) return 1;
            return 1;
        }

        /// <summary>
        /// 우선순위 기반 겹침 방지 시스템.
        /// Heavy↔일반, Heavy↔Heavy, 일반↔일반 세 가지 케이스를 처리.
        /// </summary>
        private void CheckAndResolveOverlap()
        {
            var aliveGoblins = goblins.Where(g => g.isAlive).ToList();
            if (aliveGoblins.Count <= 1) return;

            // 전체 점유 좌표 집합 (이동 시 실시간 업데이트)
            var occupiedCoords = new HashSet<HexCoord>();
            foreach (var g in aliveGoblins)
            {
                if (g.isHeavy && g.occupiedCoords != null)
                    foreach (var c in g.occupiedCoords) occupiedCoords.Add(c);
                else
                    occupiedCoords.Add(g.position);
            }

            // ── 케이스 1 & 2: Heavy 고블린 관련 겹침 ──
            var heavyGoblins = aliveGoblins.Where(g => g.isHeavy && g.occupiedCoords != null).ToList();
            foreach (var heavy in heavyGoblins)
            {
                var heavySet = new HashSet<HexCoord>(heavy.occupiedCoords);

                // 케이스 1: Heavy vs 일반 — 일반 고블린이 Heavy 점유 칸 안에 있으면 밀어냄
                var normalGoblins = aliveGoblins.Where(g => !g.isHeavy).ToList();
                foreach (var normal in normalGoblins)
                {
                    if (!heavySet.Contains(normal.position)) continue;

                    Debug.LogWarning($"[GoblinSystem] ⚠️ Heavy↔일반 겹침: {normal.position} (Heavy 점유 내)");
                    // Heavy가 우선 — 일반 고블린을 빈 칸으로 이동
                    HexCoord newPos = FindNearestEmptyPosition(normal.position, occupiedCoords);
                    if (newPos != normal.position)
                    {
                        Debug.LogWarning($"[GoblinSystem] → 겹침 해소(일반 이동): ({normal.position}) → ({newPos})");
                        occupiedCoords.Remove(normal.position);
                        normal.position = newPos;
                        occupiedCoords.Add(newPos);
                        UpdateGoblinVisualPosition(normal, newPos);
                    }
                }

                // 케이스 2: Heavy vs Heavy — occupiedCoords 겹침 시 낮은 우선순위 Heavy 이동
                foreach (var other in heavyGoblins)
                {
                    if (other == heavy) continue;
                    if (other.occupiedCoords == null) continue;

                    bool overlaps = other.occupiedCoords.Any(c => heavySet.Contains(c));
                    if (!overlaps) continue;

                    Debug.LogWarning($"[GoblinSystem] ⚠️ Heavy↔Heavy 겹침 감지");
                    // 우선순위 낮은 쪽(동률이면 나중 인덱스)을 이동
                    GoblinData toMove = GetGoblinPriority(heavy) >= GetGoblinPriority(other) ? other : heavy;
                    GoblinData staying = toMove == heavy ? other : heavy;

                    // Heavy 이동: FindHeavyJumpTriangles로 삼각형 자리 탐색
                    var staySet = new HashSet<HexCoord>(staying.occupiedCoords);
                    // 이동할 Heavy의 현재 좌표를 occupiedCoords에서 제거
                    foreach (var c in toMove.occupiedCoords) occupiedCoords.Remove(c);

                    var allOccupiedForSearch = new HashSet<HexCoord>(occupiedCoords);
                    var candidates = FindHeavyJumpTriangles(toMove.position, hexGrid.GridRadius + 3, toMove, allOccupiedForSearch);
                    if (candidates.Count > 0)
                    {
                        var best = candidates[0];
                        HexCoord newCoord = best[0]; // Heavy position = occupiedCoords[0]
                        Debug.LogWarning($"[GoblinSystem] → Heavy 겹침 해소: ({toMove.position}) → ({newCoord})");
                        foreach (var c in best) occupiedCoords.Add(c);
                        toMove.occupiedCoords = best;
                        toMove.position = newCoord;
                        // Heavy 비주얼은 3칸 중심(Vector2) 기준
                        if (toMove.visualObject != null && hexGrid != null)
                        {
                            RectTransform rt = toMove.visualObject.GetComponent<RectTransform>();
                            if (rt != null) rt.anchoredPosition = CalculateHeavyCenterPosition(best);
                        }
                    }
                    else
                    {
                        // 대안 없으면 원래 좌표 복원
                        foreach (var c in toMove.occupiedCoords) occupiedCoords.Add(c);
                        Debug.LogWarning($"[GoblinSystem] Heavy 겹침 해소 실패: 이동 가능한 삼각형 없음");
                    }
                }
            }

            // ── 케이스 3: 일반 vs 일반 — 같은 position ──
            var normalMap = new Dictionary<HexCoord, List<GoblinData>>();
            foreach (var g in aliveGoblins.Where(g => !g.isHeavy))
            {
                if (!normalMap.ContainsKey(g.position))
                    normalMap[g.position] = new List<GoblinData>();
                normalMap[g.position].Add(g);
            }

            foreach (var kvp in normalMap)
            {
                if (kvp.Value.Count <= 1) continue;

                Debug.LogWarning($"[GoblinSystem] ⚠️ 일반 겹침: {kvp.Key} 에 {kvp.Value.Count}마리");
                // 우선순위 내림차순 정렬 — 높은 쪽이 먼저 (유지), 나머지 이동
                var sorted = kvp.Value.OrderByDescending(g => GetGoblinPriority(g)).ToList();
                for (int i = 1; i < sorted.Count; i++)
                {
                    var goblin = sorted[i];
                    HexCoord newPos = FindNearestEmptyPosition(goblin.position, occupiedCoords);
                    if (newPos != goblin.position)
                    {
                        Debug.LogWarning($"[GoblinSystem] → 겹침 해소: ({goblin.position}) → ({newPos})");
                        occupiedCoords.Remove(goblin.position);
                        goblin.position = newPos;
                        occupiedCoords.Add(newPos);
                        UpdateGoblinVisualPosition(goblin, newPos);
                    }
                }
            }
        }

        /// <summary>
        /// 고블린의 비주얼 오브젝트 위치를 즉시 갱신하는 헬퍼.
        /// </summary>
        private void UpdateGoblinVisualPosition(GoblinData goblin, HexCoord coord)
        {
            if (goblin.visualObject == null || hexGrid == null) return;
            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition = hexGrid.CalculateFlatTopHexPosition(coord);
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
            // Heavy 고블린의 모든 점유 좌표도 등록
            foreach (var g in aliveGoblins)
            {
                if (g.isHeavy && g.occupiedCoords != null)
                    foreach (var c in g.occupiedCoords)
                        occupiedCoords.Add(c);
            }

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
            // 궁수/마법사/도둑/마녀/언데드 고블린은 이동하지 않음 — 근접 고블린만 이동
            var aliveGoblins = goblins.Where(g => g.isAlive && !g.isArcher && !g.isWizard && !g.isThief && !g.isWitch && !g.isUndead).ToList();
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

            // 폭탄 고블린은 모든 턴에서 MoveAllGoblins에서 제외 (공격하지 않고 이동만 — BombGoblinPhase에서 통합 처리)
            var skippedBombGoblins = new List<GoblinData>();
            foreach (var goblin in aliveGoblins)
            {
                if (goblin.isBomb)
                    skippedBombGoblins.Add(goblin);
            }

            // 폭탄 고블린 턴 카운터 증가 (설치 턴 여부 판단 후 증가)
            foreach (var goblin in aliveGoblins)
            {
                if (goblin.isBomb)
                    goblin.bombTurnCounter++;
            }

            // 스킵 대상을 이동 목록에서 제외 (힐러도 자체 페이즈에서 처리, Heavy는 별도 이동 처리)
            aliveGoblins = aliveGoblins.Where(g => !skippedShieldGoblins.Contains(g) && !skippedBombGoblins.Contains(g) && !g.isHealer && !g.isHeavy).ToList();
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
            // ★ 이동하지 않는 고블린(궁수, 스킵된 방패, 설치 턴 폭탄)의 위치를 미리 선점
            HashSet<HexCoord> reservedCoords = new HashSet<HexCoord>();
            foreach (var g in skippedShieldGoblins)
                reservedCoords.Add(g.position);
            foreach (var g in skippedBombGoblins)
                reservedCoords.Add(g.position);
            // 궁수 고블린도 자리 선점 (이동 안 하므로)
            foreach (var g in goblins.Where(g => g.isAlive && g.isArcher))
                reservedCoords.Add(g.position);
            // Heavy 고블린 점유 좌표 선점 (별도 이동 처리)
            foreach (var g in goblins.Where(g => g.isAlive && g.isHeavy && g.occupiedCoords != null))
                foreach (var c in g.occupiedCoords)
                    reservedCoords.Add(c);
            // 도둑 고블린도 자리 선점 (자체 이동 처리)
            foreach (var g in goblins.Where(g => g.isAlive && g.isThief))
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
            if (hexGrid != null)
            {
                hexGrid.transform.localPosition = Vector3.zero;
                VisualConstants.ResetScreenShake();
            }
        }

        // ============================================================
        // Heavy 고블린 이동 페이즈
        // ============================================================

        /// <summary>
        /// 현재 삼각형에서 1~2블록이 겹치는 인접 삼각형 목록을 탐색.
        /// 각 삼각형은 3개 좌표로 구성되며, 모두 유효 범위 내 + 비어있거나 자신의 좌표여야 함.
        /// </summary>
        private List<List<HexCoord>> FindAdjacentTriangles(List<HexCoord> currentTriangle, HashSet<HexCoord> allOccupied, GoblinData self)
        {
            var results = new List<List<HexCoord>>();
            if (currentTriangle == null || currentTriangle.Count < 3) return results;

            // 삼각형 패턴 6가지 (FindHeavySpawnPosition과 동일)
            HexCoord[][] triangleOffsets = new HexCoord[][]
            {
                // 아래쪽 삼각형 (∇ 형태)
                new HexCoord[] { new HexCoord(0,0), new HexCoord(1,0), new HexCoord(0,1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,1), new HexCoord(0,1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,0), new HexCoord(-1,1) },
                // 위쪽 삼각형 (△ 형태)
                new HexCoord[] { new HexCoord(0,0), new HexCoord(1,-1), new HexCoord(1,0) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(0,-1), new HexCoord(1,-1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,0), new HexCoord(0,-1) },
            };

            var currentSet = new HashSet<HexCoord>(currentTriangle);
            var foundSets = new HashSet<string>(); // 중복 방지

            // 현재 삼각형의 각 좌표 주변 탐색
            foreach (var baseCoord in currentTriangle)
            {
                // baseCoord를 포함하는 삼각형 후보 탐색
                for (int cq = baseCoord.q - 1; cq <= baseCoord.q + 1; cq++)
                {
                    for (int cr = baseCoord.r - 1; cr <= baseCoord.r + 1; cr++)
                    {
                        var center = new HexCoord(cq, cr);
                        foreach (var offsets in triangleOffsets)
                        {
                            var candidate = new List<HexCoord>();
                            bool valid = true;

                            for (int i = 0; i < 3; i++)
                            {
                                var coord = new HexCoord(center.q + offsets[i].q, center.r + offsets[i].r);
                                candidate.Add(coord);
                            }

                            // 겹침 수 계산: 1~2블록 겹쳐야 인접 삼각형
                            int overlapCount = 0;
                            foreach (var c in candidate)
                            {
                                if (currentSet.Contains(c)) overlapCount++;
                            }
                            if (overlapCount < 1 || overlapCount > 2)
                                continue;

                            // 동일 삼각형(3개 모두 겹침) 제외
                            if (overlapCount == 3)
                                continue;

                            // 3개 좌표 유효성 검사
                            foreach (var coord in candidate)
                            {
                                // 범위 확인 (그리드 + 확장 3줄)
                                if (Mathf.Abs(coord.q) > hexGrid.GridRadius)
                                { valid = false; break; }

                                int rMin = hexGrid.GetTopR(coord.q);
                                int rMax = Mathf.Min(hexGrid.GridRadius, -coord.q + hexGrid.GridRadius);
                                if (coord.r < rMin - 3 || coord.r > rMax)
                                { valid = false; break; }

                                // 자신의 좌표가 아니면서 다른 고블린이 점유 중이면 불가
                                if (!currentSet.Contains(coord) && allOccupied.Contains(coord))
                                { valid = false; break; }
                            }

                            if (!valid) continue;

                            // 중복 방지 (좌표 정렬 후 키 생성)
                            var sorted = candidate.OrderBy(c => c.q).ThenBy(c => c.r).ToList();
                            string key = $"{sorted[0]},{sorted[1]},{sorted[2]}";
                            if (foundSets.Contains(key)) continue;
                            foundSets.Add(key);

                            results.Add(candidate);
                        }
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Heavy 고블린 이동: 인접 삼각형 기반 이동 (1~2블록 겹침).
        /// 상단 절반이면 아래쪽만, 중간 이하이면 모든 방향 랜덤.
        /// turnCounter에 따라 3턴마다 점프 실행.
        /// </summary>
        private IEnumerator HeavyGoblinMovePhase()
        {
            var heavyGoblins = goblins.Where(g => g.isAlive && g.isHeavy).ToList();
            if (heavyGoblins.Count == 0) yield break;

            // 점유 좌표 수집: Heavy 고블린만 (일반 고블린은 밀어내기 가능하므로 제외)
            var allOccupied = new HashSet<HexCoord>();
            foreach (var g in goblins.Where(g => g.isAlive && g.isHeavy))
            {
                if (g.occupiedCoords != null)
                    foreach (var c in g.occupiedCoords) allOccupied.Add(c);
            }

            foreach (var heavy in heavyGoblins)
            {
                if (heavy.occupiedCoords == null || heavy.occupiedCoords.Count < 3) continue;

                // 턴 카운터 증가
                heavy.heavyTurnCounter++;

                // 3턴마다 점프 실행
                if (heavy.heavyTurnCounter % 3 == 0)
                {
                    yield return StartCoroutine(HeavyGoblinJump(heavy, allOccupied));
                    continue;
                }

                // 인접 삼각형 탐색
                var adjacentTriangles = FindAdjacentTriangles(heavy.occupiedCoords, allOccupied, heavy);
                if (adjacentTriangles.Count == 0) continue;

                // 방향 필터: 그리드 중심 r 기준 상단 절반이면 아래쪽만
                float currentCenterR = 0f;
                foreach (var c in heavy.occupiedCoords)
                    currentCenterR += c.r;
                currentCenterR /= heavy.occupiedCoords.Count;

                // 그리드 중심 r 계산 (q=0 기준)
                int gridCenterR = (hexGrid.GetTopR(0) + hexGrid.GridRadius) / 2;

                List<List<HexCoord>> filteredTriangles;
                if (currentCenterR < gridCenterR)
                {
                    // 상단 절반: 아래쪽 방향만 (새 중심 r > 현재 중심 r)
                    filteredTriangles = new List<List<HexCoord>>();
                    foreach (var tri in adjacentTriangles)
                    {
                        float newCenterR = 0f;
                        foreach (var c in tri) newCenterR += c.r;
                        newCenterR /= tri.Count;
                        if (newCenterR > currentCenterR)
                            filteredTriangles.Add(tri);
                    }
                    // 아래쪽 후보 없으면 전체에서 선택
                    if (filteredTriangles.Count == 0)
                        filteredTriangles = adjacentTriangles;
                }
                else
                {
                    // 중간 이하: 모든 방향 랜덤
                    filteredTriangles = adjacentTriangles;
                }

                // 랜덤 1개 선택
                var newCoords = filteredTriangles[Random.Range(0, filteredTriangles.Count)];

                // 이동 전: 새 좌표 중 그리드 내부의 블록 공격
                var currentSet = new HashSet<HexCoord>(heavy.occupiedCoords);
                foreach (var nc in newCoords)
                {
                    if (!currentSet.Contains(nc) && hexGrid.IsInsideGrid(nc))
                    {
                        HexBlock block = hexGrid.GetBlock(nc);
                        if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                        {
                            // 블록 공격 처리 (크랙 → 쉘 공통 메서드 사용)
                            if (!block.Data.isCracked && !block.Data.isShell)
                            {
                                block.Data.isCracked = true;
                                block.UpdateVisuals();
                                StartCoroutine(BlockFlashEffect(block.gameObject, Color.white));
                            }
                            else if (block.Data.isCracked && !block.Data.isShell)
                            {
                                ConvertToShellBlock(block);
                                StartCoroutine(BlockFlashEffect(block.gameObject, Color.white));
                            }
                        }
                    }
                }

                // 이동 전: 새 좌표에 있는 일반 고블린 밀어내기
                PushGoblinsFromHeavyCoords(newCoords, heavy);

                // occupiedCoords에서 기존 점유 해제
                foreach (var c in heavy.occupiedCoords)
                    allOccupied.Remove(c);

                // 좌표 갱신
                heavy.occupiedCoords = newCoords;
                heavy.position = newCoords[0];

                // 새 점유 등록
                foreach (var c in newCoords)
                    allOccupied.Add(c);

                // 비주얼 이동 애니메이션
                Vector2 newCenter = CalculateHeavyCenterPosition(newCoords);
                yield return StartCoroutine(AnimateGoblinMove(heavy, newCenter));

                // 점유 블록 압박 효과 재적용
                ApplyHeavyPressureEffect(heavy);

                // ★ 이동 완료: 새 점유 좌표의 특수 블록 발동 없이 단순 파괴
                CrushSpecialBlocksUnderHeavy(heavy.occupiedCoords);
            }
        }

        /// <summary>
        /// Heavy 고블린 점프: axial 거리 5 이내의 유효 삼각형으로 점프.
        /// 착지 시 3블록 isCracked + 화면 흔들림.
        /// </summary>
        private IEnumerator HeavyGoblinJump(GoblinData goblin, HashSet<HexCoord> allOccupied)
        {
            if (goblin.occupiedCoords == null || goblin.occupiedCoords.Count < 3) yield break;

            // 현재 중심 좌표 계산
            float avgQ = 0f, avgR = 0f;
            foreach (var c in goblin.occupiedCoords)
            {
                avgQ += c.q;
                avgR += c.r;
            }
            var approxCenter = new HexCoord(Mathf.RoundToInt(avgQ / 3f), Mathf.RoundToInt(avgR / 3f));

            // axial 거리 5 이내의 유효 삼각형 위치 탐색
            var candidates = FindHeavyJumpTriangles(approxCenter, 5, goblin, allOccupied);
            if (candidates.Count == 0)
            {
                Debug.Log($"[GoblinSystem] Heavy 점프 실패: 유효한 삼각형 없음 at {approxCenter}");
                yield break;
            }

            // 랜덤 1개 선택
            var targetCoords = candidates[Random.Range(0, candidates.Count)];

            // 기존 점유 해제
            foreach (var c in goblin.occupiedCoords)
                allOccupied.Remove(c);

            // 착지 전: 착지 예정 좌표에 있는 일반 고블린 밀어내기
            PushGoblinsFromHeavyCoords(targetCoords, goblin);

            // 점프 시작 사운드 — 무거운 저음 whoosh
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayHeavyJumpSound();

            // 점프 애니메이션
            RectTransform rt = goblin.visualObject != null ? goblin.visualObject.GetComponent<RectTransform>() : null;
            if (rt != null)
            {
                Vector2 startPos = rt.anchoredPosition;
                Vector2 targetCenter = CalculateHeavyCenterPosition(targetCoords);

                // 1단계: 위로 80px 상승 (0.15초, EaseOutCubic)
                float dur1 = 0.15f;
                float elapsed = 0f;
                Vector2 jumpUpPos = startPos + new Vector2(0, 80f);
                while (elapsed < dur1)
                {
                    if (goblin.visualObject == null) yield break;
                    elapsed += Time.deltaTime;
                    float t = VisualConstants.EaseOutCubic(Mathf.Clamp01(elapsed / dur1));
                    rt.anchoredPosition = Vector2.Lerp(startPos, jumpUpPos, t);
                    yield return null;
                }

                // 2단계: 목표 위치로 낙하 (0.15초, EaseInCubic)
                float dur2 = 0.15f;
                elapsed = 0f;
                Vector2 jumpMidPos = new Vector2(targetCenter.x, jumpUpPos.y);
                while (elapsed < dur2)
                {
                    if (goblin.visualObject == null) yield break;
                    elapsed += Time.deltaTime;
                    float raw = Mathf.Clamp01(elapsed / dur2);
                    float t = raw * raw * raw; // EaseInCubic
                    // 수평 이동 + 수직 낙하 동시
                    float x = Mathf.Lerp(jumpUpPos.x, targetCenter.x, t);
                    float y = Mathf.Lerp(jumpMidPos.y, targetCenter.y, t);
                    rt.anchoredPosition = new Vector2(x, y);
                    yield return null;
                }

                if (rt != null)
                    rt.anchoredPosition = targetCenter;
            }

            // 좌표 갱신
            goblin.occupiedCoords = targetCoords;
            goblin.position = targetCoords[0];

            // 새 점유 등록
            foreach (var c in targetCoords)
                allOccupied.Add(c);

            // 착지 사운드 — 지진 느낌의 강한 저음 충격음
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayHeavyLandSound();

            // ★ 착지: 점유 좌표의 특수 블록 발동 없이 단순 파괴 (이미 파괴된 좌표는 isCracked 루프에서 자동 스킵)
            CrushSpecialBlocksUnderHeavy(targetCoords);

            // 착지 효과: 3블록 isCracked 처리
            foreach (var coord in targetCoords)
            {
                if (hexGrid.IsInsideGrid(coord))
                {
                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    {
                        block.Data.isCracked = true;
                        // 특수 블록이면 specialType = None (MoveBlock/FixedBlock 제외)
                        if (block.Data.specialType != SpecialBlockType.None &&
                            block.Data.specialType != SpecialBlockType.MoveBlock &&
                            block.Data.specialType != SpecialBlockType.FixedBlock)
                        {
                            block.Data.specialType = SpecialBlockType.None;
                        }
                        block.UpdateVisuals();
                        StartCoroutine(BlockFlashEffect(block.gameObject, new Color(0.6f, 0.6f, 0.6f, 1f))); // 회색 플래시
                    }
                }
            }

            // 착지 화면 흔들림 (작은 강도)
            StartCoroutine(ScreenShake(3f, 0.15f));

            // 점유 블록 압박 효과 적용
            ApplyHeavyPressureEffect(goblin);

            Debug.Log($"[GoblinSystem] Heavy 점프 완료: {approxCenter} → {targetCoords[0]}, 점유={string.Join(",", targetCoords)}");
        }

        /// <summary>
        /// Heavy 점프용 삼각형 위치 탐색: 중심에서 axial 거리 maxDist 이내.
        /// 3개 좌표 모두 유효, 비어있거나 자신, 다른 고블린 없음.
        /// </summary>
        private List<List<HexCoord>> FindHeavyJumpTriangles(HexCoord center, int maxDist, GoblinData self, HashSet<HexCoord> allOccupied)
        {
            var results = new List<List<HexCoord>>();
            var selfCoords = new HashSet<HexCoord>(self.occupiedCoords);

            // 삼각형 패턴 (FindHeavySpawnPosition과 동일한 6가지)
            HexCoord[][] triangleOffsets = new HexCoord[][]
            {
                new HexCoord[] { new HexCoord(0,0), new HexCoord(1,0), new HexCoord(0,1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,1), new HexCoord(0,1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,0), new HexCoord(-1,1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(1,-1), new HexCoord(1,0) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(0,-1), new HexCoord(1,-1) },
                new HexCoord[] { new HexCoord(0,0), new HexCoord(-1,0), new HexCoord(0,-1) },
            };

            var hexesInRange = HexCoord.GetHexesInRadius(center, maxDist);
            var foundSets = new HashSet<string>(); // 중복 방지

            foreach (var hex in hexesInRange)
            {
                foreach (var offsets in triangleOffsets)
                {
                    var coords = new List<HexCoord>();
                    bool valid = true;

                    for (int i = 0; i < 3; i++)
                    {
                        var coord = new HexCoord(hex.q + offsets[i].q, hex.r + offsets[i].r);

                        // 범위 확인 (그리드 + 확장 3줄)
                        if (Mathf.Abs(coord.q) > hexGrid.GridRadius)
                        { valid = false; break; }

                        int rMin = hexGrid.GetTopR(coord.q);
                        int rMax = Mathf.Min(hexGrid.GridRadius, -coord.q + hexGrid.GridRadius);
                        if (coord.r < rMin - 3 || coord.r > rMax)
                        { valid = false; break; }

                        // 실제 블록 오브젝트가 존재하는 좌표인지 확인
                        // (그리드 확장 영역이나 빈 슬롯은 후보에서 제외)
                        if (!hexGrid.IsValidCoord(coord))
                        { valid = false; break; }

                        // 자신의 좌표가 아니면서 다른 고블린이 점유 중이면 불가
                        if (!selfCoords.Contains(coord) && allOccupied.Contains(coord))
                        { valid = false; break; }

                        coords.Add(coord);
                    }

                    if (!valid || coords.Count != 3) continue;

                    // 현재 위치와 완전히 동일하면 제외
                    int sameCount = 0;
                    foreach (var c in coords)
                        if (selfCoords.Contains(c)) sameCount++;
                    if (sameCount == 3) continue;

                    // 중복 방지
                    var sorted = coords.OrderBy(c => c.q).ThenBy(c => c.r).ToList();
                    string key = $"{sorted[0]},{sorted[1]},{sorted[2]}";
                    if (foundSets.Contains(key)) continue;
                    foundSets.Add(key);

                    results.Add(coords);
                }
            }

            return results;
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

            // 2차: 공격할 곳이 없으면 이동만 — 빈 칸(블록 없는 곳)으로 이동
            // 아래 방향 우선, 막히면 모든 방향 시도
            {
                // 아래 3방향 우선 시도 (바로 아래 → 대각선 아래)
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
                    HexCoord target = goblin.position + dir;
                    if (IsEmptyMovableCell(target, isOccupied))
                    {
                        result.found = true;
                        result.target = target;
                        return result;
                    }
                }

                // 아래 방향 모두 막힌 경우: 나머지 방향(위, 옆)도 시도
                HexCoord[] sideDirs = {
                    new HexCoord(-1, 0),  // 왼쪽 위
                    new HexCoord(1, -1),  // 오른쪽 위
                    new HexCoord(0, -1)   // 바로 위
                };
                var shuffledSide = sideDirs.OrderBy(x => Random.value).ToArray();
                foreach (var dir in shuffledSide)
                {
                    HexCoord target = goblin.position + dir;
                    if (IsEmptyMovableCell(target, isOccupied))
                    {
                        result.found = true;
                        result.target = target;
                        return result;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 이동 가능한 빈 칸인지 판정 (블록 없고, 점유 안 되고, 그리드 확장 범위 내)
        /// </summary>
        private bool IsEmptyMovableCell(HexCoord target, System.Func<HexCoord, bool> isOccupied)
        {
            if (Mathf.Abs(target.q) > hexGrid.GridRadius) return false;

            int rMin = hexGrid.GetTopR(target.q);
            int rMax = Mathf.Min(hexGrid.GridRadius, -target.q + hexGrid.GridRadius);
            // 그리드 내부 + 확장 3줄까지 허용
            if (target.r < rMin - 3 || target.r > rMax) return false;

            if (isOccupied(target)) return false;

            // 그리드 내부 셀이면 블록이 비어있어야 이동 가능
            if (hexGrid.IsInsideGrid(target))
            {
                HexBlock block = hexGrid.GetBlock(target);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    return false; // 블록 있는 곳은 이동 불가
            }

            return true;
        }

        private IEnumerator AnimateGoblinMove(GoblinData goblin, Vector2 targetPos)
        {
            if (goblin == null || goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 startPos = rt.anchoredPosition;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (goblin.visualObject == null || rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            if (rt != null)
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
                StartCoroutine(BlockFlashEffect(block.gameObject, Color.white));
                Debug.Log($"[GoblinSystem] 공격→특수블록 기능 해제: ({goblin.position}) → ({targetCoord}) {removedType} → 일반 크랙 블록");
            }
            else if (block.Data != null && !block.Data.isCracked)
            {
                // 첫 타격: 블록에 금이 감 (완전 파괴 아님)
                var data = block.Data;
                data.isCracked = true;
                block.SetBlockData(data);
                StartCoroutine(BlockFlashEffect(block.gameObject, Color.white));
                Debug.Log($"[GoblinSystem] 공격→금간: ({goblin.position}) → ({targetCoord}) 블록에 금이 감");
            }
            else if (block.Data != null && block.Data.isCracked && !block.Data.isShell)
            {
                // 두 번째 타격: 껍데기(쉘) 변환 (GoblinBomb 즉시 폭발 포함)
                ConvertToShellBlock(block);
                StartCoroutine(BlockFlashEffect(block.gameObject, Color.white));
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

            // 화면 미세 흔들림
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
        /// 화면 흔들림 코루틴 (중앙집중식 VisualConstants로 중복 실행 차단).
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
        // 3단계: 고블린 소환
        // ============================================================

        private IEnumerator SpawnGoblins()
        {
            // 웨이브 소환 모드에서는 턴당 소환 스킵 (1차/2차 웨이브로만 소환)
            if (useWaveSpawn)
            {
                Debug.Log("[GoblinSystem] SpawnGoblins 스킵: 웨이브 소환 모드");
                yield break;
            }

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
            int bombTarget = GetMissionTargetForType(false, false, false, true);
            int healerTarget = GetMissionTargetForType(false, false, false, false, true);
            int heavyTarget = GetMissionTargetForType(false, false, false, false, false, true);
            int wizardTarget = GetMissionTargetForType(false, false, false, false, false, false, true);
            int thiefTarget2 = GetMissionTargetForType(false, false, false, false, false, false, false, true);
            int witchTarget2 = GetMissionTargetForType(false, false, false, false, false, false, false, false, true);
            int goblinLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, true);
            int armoredLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, false, true);
            int archerLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, false, false, true);
            int shieldLv2Target = GetMissionTargetForType(false, false, false, false, false, false, false, false, false, false, false, false, true);

            // ★★★ 핵심 수정: 소환 카운터 기반 잔여 계산 (kills+alive 대신 spawned 사용)
            int regularRemaining = Mathf.Max(0, regularTarget - regularSpawned);
            int armoredRemaining = Mathf.Max(0, armoredTarget - armoredSpawned);
            int archerRemaining = Mathf.Max(0, archerTarget - archerSpawned);
            int shieldRemaining = Mathf.Max(0, shieldTarget - shieldSpawned);
            int bombRemaining = Mathf.Max(0, bombTarget - bombSpawned);
            int healerRemaining = Mathf.Max(0, healerTarget - healerSpawned);
            int heavyRemaining = Mathf.Max(0, heavyTarget - heavySpawned);
            int wizardRemaining = Mathf.Max(0, wizardTarget - wizardSpawned);
            int thiefRemaining2 = Mathf.Max(0, thiefTarget2 - thiefSpawned);
            int witchRemaining2 = Mathf.Max(0, witchTarget2 - witchSpawned);
            int goblinLv2Remaining = Mathf.Max(0, goblinLv2Target - goblinLv2Spawned);
            int armoredLv2Remaining = Mathf.Max(0, armoredLv2Target - armoredLv2Spawned);
            int archerLv2Remaining = Mathf.Max(0, archerLv2Target - archerLv2Spawned);
            int shieldLv2Remaining = Mathf.Max(0, shieldLv2Target - shieldLv2Spawned);
            int totalRemaining = regularRemaining + armoredRemaining + archerRemaining + shieldRemaining
                               + bombRemaining + healerRemaining + heavyRemaining + wizardRemaining + thiefRemaining2 + witchRemaining2
                               + goblinLv2Remaining + armoredLv2Remaining + archerLv2Remaining + shieldLv2Remaining;

            // ★ 미션 기반 하드캡: totalSpawned 기반 (활성 미션에 없는 타입은 절대 소환 안 됨)
            int totalMissionTarget = GetTotalMissionTarget();
            int hardCap2 = Mathf.Max(0, totalMissionTarget - totalSpawned);
            totalRemaining = Mathf.Min(totalRemaining, hardCap2);
            if (totalRemaining <= 0) yield break;

            Debug.Log($"[GoblinSystem] 소환 제한: 미션합={totalMissionTarget}, 총소환={totalSpawned}, 생존={aliveCount}, " +
                      $"기본({regularSpawned}/{regularTarget}) 갑옷({armoredSpawned}/{armoredTarget}) " +
                      $"활({archerSpawned}/{archerTarget}) 방패({shieldSpawned}/{shieldTarget}) 폭탄({bombSpawned}/{bombTarget}) " +
                      $"마법사({wizardSpawned}/{wizardTarget}) 도둑({thiefSpawned}/{thiefTarget2})");

            // ★★★ 활성 미션 몬스터 전량 즉시 소환
            int spawnCount = totalRemaining;
            spawnCount = Mathf.Min(spawnCount, boardSlots);        // 보드 최대 수 초과 방지

            if (spawnCount <= 0) yield break;

            Debug.Log($"[GoblinSystem] 소환 계획: {spawnCount}마리 (몽둥이잔여={regularRemaining}, 갑옷잔여={armoredRemaining}, 활잔여={archerRemaining}, 방패잔여={shieldRemaining}, 폭탄잔여={bombRemaining})");

            // 소환 가능 위치 분류: 궁수는 가장 윗줄(row=3)만, 근접/갑옷은 아래 2줄
            var allExtended = hexGrid.GetExtendedTopCoords();
            var occupiedPositions = new HashSet<HexCoord>(goblins.Where(g => g.isAlive).Select(g => g.position));
            // Heavy 고블린의 3칸 점유 좌표도 모두 등록 (겹침 방지)
            foreach (var hg in goblins.Where(g => g.isAlive && g.isHeavy && g.occupiedCoords != null))
                foreach (var c in hg.occupiedCoords) occupiedPositions.Add(c);

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
            var pendingSpawns2 = new List<(GoblinData goblin, bool isHeavy)>();
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
                bool isBombType = false;
                bool isHealerType = false;
                int goblinHp = 5; // 몽둥이 기본 HP
                int spawnShieldHp = currentConfig.shieldHp;

                // Heavy 고블린 우선 소환 (삼각형 3블록 위치 필요)
                if (heavyRemaining > 0)
                {
                    var heavyCoords = FindHeavySpawnPosition(usedPositions);
                    if (heavyCoords != null)
                    {
                        heavyRemaining--;
                        foreach (var c in heavyCoords)
                            usedPositions.Add(c);

                        GoblinData heavyGoblin = new GoblinData
                        {
                            position = heavyCoords[0],
                            hp = currentConfig.heavyGoblinHp,
                            maxHp = currentConfig.heavyGoblinHp,
                            displayedHp = currentConfig.heavyGoblinHp,
                            isAlive = true,
                            isHeavy = true,
                            occupiedCoords = new List<HexCoord>(heavyCoords)
                        };

                        goblins.Add(heavyGoblin);
                        totalSpawned++;
                        heavySpawned++;

                        Debug.Log($"[GoblinSystem] 턴당 헤비급 고블린 소환 at ({heavyCoords[0]}), HP={currentConfig.heavyGoblinHp}");
                        pendingSpawns2.Add((heavyGoblin, true));
                        continue;
                    }
                }

                // 사용 가능한 슬롯 수 계산 (usedPositions 기반)
                int availableTop = 0;
                foreach (var c in shuffledTop) { if (!usedPositions.Contains(c)) availableTop++; }
                int availableBottom = 0;
                foreach (var c in shuffledBottom) { if (!usedPositions.Contains(c)) availableBottom++; }
                int availableLower = 0;
                foreach (var c in shuffledLower) { if (!usedPositions.Contains(c)) availableLower++; }

                // 도둑 고블린: lowerRowCoords에서 소환
                if (thiefRemaining2 > 0 && availableLower > 0)
                {
                    HexCoord thiefCoord = default;
                    bool thiefFound = false;
                    foreach (var c in shuffledLower)
                    {
                        if (!usedPositions.Contains(c)) { thiefCoord = c; thiefFound = true; break; }
                    }
                    if (thiefFound)
                    {
                        usedPositions.Add(thiefCoord);
                        thiefRemaining2--;

                        GoblinData thiefGoblin = new GoblinData
                        {
                            position = thiefCoord,
                            hp = currentConfig.thiefGoblinHp,
                            maxHp = currentConfig.thiefGoblinHp,
                            displayedHp = currentConfig.thiefGoblinHp,
                            isAlive = true,
                            isThief = true,
                            thiefAttackPhase = 0,
                            thiefVisitedPositions = new List<HexCoord> { thiefCoord }
                        };

                        goblins.Add(thiefGoblin);
                        totalSpawned++;
                        thiefSpawned++;

                        Debug.Log($"[GoblinSystem] 턴당 도둑 고블린 소환 at ({thiefCoord}), HP={currentConfig.thiefGoblinHp}, 누적소환={totalSpawned}");
                        pendingSpawns2.Add((thiefGoblin, false));
                        continue;
                    }
                }

                // 마법사 고블린: 상단(topRow)에서 소환
                if (wizardRemaining > 0 && availableTop > 0)
                {
                    HexCoord wizCoord = default;
                    bool wizFound = false;
                    foreach (var c in shuffledTop)
                    {
                        if (!usedPositions.Contains(c)) { wizCoord = c; wizFound = true; break; }
                    }
                    if (wizFound)
                    {
                        usedPositions.Add(wizCoord);
                        wizardRemaining--;

                        GoblinData wizGoblin = new GoblinData
                        {
                            position = wizCoord,
                            hp = currentConfig.wizardGoblinHp,
                            maxHp = currentConfig.wizardGoblinHp,
                            displayedHp = currentConfig.wizardGoblinHp,
                            isAlive = true,
                            isWizard = true
                        };

                        goblins.Add(wizGoblin);
                        totalSpawned++;
                        wizardSpawned++;

                        Debug.Log($"[GoblinSystem] 턴당 마법사 고블린 소환 at ({wizCoord}), HP={currentConfig.wizardGoblinHp}, 누적소환={totalSpawned}");
                        pendingSpawns2.Add((wizGoblin, false));
                        continue;
                    }
                }

                // 마녀 고블린: 상단(topRow)에서 소환
                if (witchRemaining2 > 0 && availableTop > 0)
                {
                    HexCoord witchCoord = default;
                    bool witchFound = false;
                    foreach (var c in shuffledTop)
                    {
                        if (!usedPositions.Contains(c)) { witchCoord = c; witchFound = true; break; }
                    }
                    if (witchFound)
                    {
                        usedPositions.Add(witchCoord);
                        witchRemaining2--;

                        int witchHp = currentConfig.witchGoblinHp;
                        GoblinData witchGoblin = new GoblinData
                        {
                            position = witchCoord,
                            hp = witchHp,
                            maxHp = witchHp,
                            displayedHp = witchHp,
                            isAlive = true,
                            isWitch = true,
                            witchOwnerIndex = nextWitchOwnerIndex++,
                            witchSummonCounter = 0,
                            witchMoveCounter = 0
                        };

                        goblins.Add(witchGoblin);
                        totalSpawned++;
                        witchSpawned++;

                        Debug.Log($"[GoblinSystem] 턴당 마녀 고블린 소환 at ({witchCoord}), HP={witchHp}, ownerIdx={witchGoblin.witchOwnerIndex}");
                        pendingSpawns2.Add((witchGoblin, false));
                        continue;
                    }
                }

                // 잔여량 합계로 가중 랜덤 → 미션 비율 정확 매칭
                bool isLv2 = false;
                int totalPool = regularRemaining + armoredRemaining + archerRemaining + shieldRemaining + bombRemaining + healerRemaining
                    + goblinLv2Remaining + armoredLv2Remaining + archerLv2Remaining + shieldLv2Remaining;
                if (totalPool <= 0) break; // 미션 수량 모두 소환 완료

                float roll = Random.value * totalPool;
                float threshold = 0f;

                // 폭탄 → 방패 → 갑옷 → 궁수 → Lv2 방패 → Lv2 갑옷 → Lv2 궁수 → Lv2 기본 → 기본 순서로 체크
                threshold += bombRemaining;
                if (roll < threshold && bombRemaining > 0 && (availableLower > 0 || availableTop > 0))
                {
                    isBombType = true;
                    goblinHp = currentConfig.bombGoblinHp;
                    bombRemaining--;
                }
                else
                {
                    threshold += healerRemaining;
                    if (roll < threshold && healerRemaining > 0 && availableTop > 0)
                    {
                        // 힐러: 피해 입은 몬스터가 있을 때만 소환
                        bool hasDamaged = goblins.Any(g => g.isAlive && g.hp < g.maxHp);
                        if (hasDamaged)
                        {
                            isHealerType = true;
                            goblinHp = 2;
                            healerRemaining--;
                        }
                    }
                }
                if (!isBombType && !isHealerType)
                {
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
                            // Lv2 타입들 (HP 2배)
                            else
                            {
                                threshold += shieldLv2Remaining;
                                if (roll < threshold && shieldLv2Remaining > 0 && availableBottom > 0)
                                {
                                    isShieldType = true;
                                    isLv2 = true;
                                    goblinHp = currentConfig.shieldGoblinHp * 2;
                                    spawnShieldHp = currentConfig.shieldHp * 2;
                                    shieldLv2Remaining--;
                                }
                                else
                                {
                                    threshold += armoredLv2Remaining;
                                    if (roll < threshold && armoredLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                    {
                                        isArmored = true;
                                        isLv2 = true;
                                        goblinHp = currentConfig.armoredHp * 2;
                                        armoredLv2Remaining--;
                                    }
                                    else
                                    {
                                        threshold += archerLv2Remaining;
                                        if (roll < threshold && archerLv2Remaining > 0 && availableTop > 0)
                                        {
                                            isArcher = true;
                                            isLv2 = true;
                                            goblinHp = currentConfig.archerHp * 2;
                                            archerLv2Remaining--;
                                        }
                                        else
                                        {
                                            threshold += goblinLv2Remaining;
                                            if (roll < threshold && goblinLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                            {
                                                isLv2 = true;
                                                goblinHp = 5 * 2; // 기본 HP × 2
                                                goblinLv2Remaining--;
                                            }
                                            else if (regularRemaining > 0 && (availableLower > 0 || availableTop > 0))
                                            {
                                                regularRemaining--;
                                            }
                                            else
                                            {
                                                // 위치 부족으로 특정 타입 스킵된 경우, 남은 타입 중 소환 가능한 것 선택
                                                if (bombRemaining > 0 && (availableLower > 0 || availableTop > 0))
                                                {
                                                    isBombType = true;
                                                    goblinHp = currentConfig.bombGoblinHp;
                                                    bombRemaining--;
                                                }
                                                else if (shieldLv2Remaining > 0 && availableBottom > 0)
                                                {
                                                    isShieldType = true; isLv2 = true;
                                                    goblinHp = currentConfig.shieldGoblinHp * 2;
                                                    spawnShieldHp = currentConfig.shieldHp * 2;
                                                    shieldLv2Remaining--;
                                                }
                                                else if (shieldRemaining > 0 && availableBottom > 0)
                                                {
                                                    isShieldType = true;
                                                    goblinHp = currentConfig.shieldGoblinHp;
                                                    shieldRemaining--;
                                                }
                                                else if (armoredLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                                {
                                                    isArmored = true; isLv2 = true;
                                                    goblinHp = currentConfig.armoredHp * 2;
                                                    armoredLv2Remaining--;
                                                }
                                                else if (armoredRemaining > 0 && (availableLower > 0 || availableTop > 0))
                                                {
                                                    isArmored = true;
                                                    goblinHp = currentConfig.armoredHp;
                                                    armoredRemaining--;
                                                }
                                                else if (archerLv2Remaining > 0 && availableTop > 0)
                                                {
                                                    isArcher = true; isLv2 = true;
                                                    goblinHp = currentConfig.archerHp * 2;
                                                    archerLv2Remaining--;
                                                }
                                                else if (archerRemaining > 0 && availableTop > 0)
                                                {
                                                    isArcher = true;
                                                    goblinHp = currentConfig.archerHp;
                                                    archerRemaining--;
                                                }
                                                else if (goblinLv2Remaining > 0 && (availableLower > 0 || availableTop > 0))
                                                {
                                                    isLv2 = true;
                                                    goblinHp = 5 * 2;
                                                    goblinLv2Remaining--;
                                                }
                                                else
                                                {
                                                    continue; // 전체 소환 불가
                                                }
                                            }
                                        }
                                    }
                                }
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
                    displayedHp = goblinHp, // ★ 표시 HP 초기화
                    isAlive = true,
                    isArcher = isArcher,
                    isArmored = isArmored,
                    isBomb = isBombType,
                    bombTurnCounter = 1,
                    isHealer = isHealerType,
                    healerTurnCounter = 0,
                    isShieldType = isShieldType,
                    isShielded = isShieldType,
                    shieldHp = isShieldType ? spawnShieldHp : 0,
                    shieldTurnCounter = 0,
                    stuckDrills = isShieldType ? new List<GameObject>() : null,
                    monsterLevel = isLv2 ? 2 : 1
                };

                goblins.Add(newGoblin);
                totalSpawned++;
                // ★ 타입별 소환 카운터 증가 (미션 초과 소환 방지의 핵심)
                if (isLv2)
                {
                    if (isArcher) archerLv2Spawned++;
                    else if (isArmored) armoredLv2Spawned++;
                    else if (isShieldType) shieldLv2Spawned++;
                    else goblinLv2Spawned++;
                }
                else
                {
                    if (isBombType) bombSpawned++;
                    else if (isHealerType) healerSpawned++;
                    else if (isArcher) archerSpawned++;
                    else if (isArmored) armoredSpawned++;
                    else if (isShieldType) shieldSpawned++;
                    else regularSpawned++;
                }
                // 힐러: 소환 즉시 힐 실행
                if (isHealerType)
                    PerformHeal(newGoblin);

                string lvStr = isLv2 ? "Lv2 " : "";
                string typeStr = isHealerType ? "힐러" : isBombType ? "폭탄" : isArcher ? "궁수" : isArmored ? "갑옷" : isShieldType ? "방패" : "기본";
                Debug.Log($"[GoblinSystem] {lvStr}{typeStr} 고블린 소환 at ({coord}), HP={goblinHp}, 누적소환={totalSpawned}");
                pendingSpawns2.Add((newGoblin, false));
            }

            // ★ 순차적 빠른 소환 연출
            yield return StartCoroutine(RapidSequentialSpawn(pendingSpawns2));

            // ★ 마녀 초기 소환 (턴당 소환에서도)
            yield return StartCoroutine(WitchInitialSummon());

            int totalMT = GetTotalMissionTarget();
            Debug.Log($"[GoblinSystem] {pendingSpawns2.Count}마리 소환 완료 | 보드 {AliveCount}/{currentConfig.maxOnBoard} | " +
                      $"누적소환 {totalSpawned}/{totalMT} (기본{regularSpawned} 갑옷{armoredSpawned} 활{archerSpawned} 방패{shieldSpawned} 폭탄{bombSpawned})");
        }

        /// <summary>
        /// 대기 미션 활성화 시 즉시 소환 트리거 (GameManager에서 호출).
        /// 새로 활성화된 미션의 몬스터를 즉시 소환합니다.
        /// </summary>
        /// <summary>대기 미션 활성화 시 소환 예약 플래그 (ProcessTurn에서 실행)</summary>
        private bool pendingImmediateSpawn = false;

        /// <summary>언데드 전멸로 재소환 대기 중인 마녀 ownerIndex 목록 (ProcessTurn 끝에서 처리)</summary>
        private List<int> pendingWitchResummon = new List<int>();

        public void TriggerImmediateSpawn()
        {
            if (!IsActive || currentConfig == null) return;

            // ★ 즉시 소환하지 않고, 세션 종료 후 ProcessTurn에서 소환되도록 예약
            pendingImmediateSpawn = true;
            Debug.Log("[GoblinSystem] 대기 미션 활성화 → 소환 예약 (세션 종료 후 실행)");
        }

        /// <summary>
        /// 세션 종료 후 대기 미션 활성화에 의한 지연 소환 실행.
        /// ProcessTurn의 소환 단계에서 호출됨.
        /// </summary>
        private IEnumerator ExecuteDeferredSpawn()
        {
            // 소환 카운터를 현재 생존 수로 리셋 (완료된 미션의 누적 카운트 제거)
            RecalculateSpawnCounters();

            // maxOnBoard 동적 확장
            int activeMissionTotal = GetTotalMissionTarget();
            if (currentConfig != null && currentConfig.maxOnBoard < activeMissionTotal)
            {
                Debug.Log($"[GoblinSystem] maxOnBoard 동적 확장 (대기→활성): {currentConfig.maxOnBoard} → {activeMissionTotal}");
                currentConfig.maxOnBoard = activeMissionTotal;
            }

            // 필요한 소환 수 계산
            int neededSpawn = activeMissionTotal - AliveCount;
            if (neededSpawn > 0)
            {
                Debug.Log($"[GoblinSystem] 지연 소환 실행: {neededSpawn}마리 (활성목표={activeMissionTotal}, 생존={AliveCount})");
                yield return StartCoroutine(SpawnWaveBatch(neededSpawn));
            }
            else
            {
                Debug.Log("[GoblinSystem] 지연 소환 → 추가 소환 불필요 (이미 충분)");
                yield break;
            }
        }

        /// <summary>
        /// 소환 카운터를 현재 생존 고블린 수로 재계산.
        /// 미션 교체 시 누적 카운터가 새 미션 목표와 불일치하는 문제 해결.
        /// </summary>
        private void RecalculateSpawnCounters()
        {
            int prevTotal = totalSpawned;
            regularSpawned = 0;
            armoredSpawned = 0;
            archerSpawned = 0;
            shieldSpawned = 0;
            bombSpawned = 0;
            healerSpawned = 0;
            heavySpawned = 0;
            wizardSpawned = 0;
            thiefSpawned = 0;
            witchSpawned = 0;
            goblinLv2Spawned = 0;
            armoredLv2Spawned = 0;
            archerLv2Spawned = 0;
            shieldLv2Spawned = 0;
            totalSpawned = 0;

            foreach (var g in goblins)
            {
                if (!g.isAlive) continue;
                if (g.isUndead) continue; // 언데드는 미션 소환 카운터에서 제외
                totalSpawned++;
                if (g.isWitch) witchSpawned++;
                else if (g.isWizard) wizardSpawned++;
                else if (g.isHeavy) heavySpawned++;
                else if (g.isThief) thiefSpawned++;
                else if (g.isHealer) healerSpawned++;
                else if (g.isBomb) bombSpawned++;
                else if (g.monsterLevel == 2 && g.isShieldType) shieldLv2Spawned++;
                else if (g.monsterLevel == 2 && g.isArcher) archerLv2Spawned++;
                else if (g.monsterLevel == 2 && g.isArmored) armoredLv2Spawned++;
                else if (g.monsterLevel == 2 && !g.isArcher && !g.isArmored && !g.isShieldType) goblinLv2Spawned++;
                else if (g.isShieldType) shieldSpawned++;
                else if (g.isArcher) archerSpawned++;
                else if (g.isArmored) armoredSpawned++;
                else regularSpawned++;
            }

            Debug.Log($"[GoblinSystem] 소환 카운터 리셋: {prevTotal} → {totalSpawned} (생존 기반 재계산)");
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

        /// <summary>
        /// Heavy 고블린 소환: 3블록 중앙에 포털 + 비주얼 생성
        /// </summary>
        private IEnumerator SpawnSingleHeavyGoblin(GoblinData goblin)
        {
            Vector2 centerPos = CalculateHeavyCenterPosition(goblin.occupiedCoords);

            // 포털 이펙트 (중앙 위치에 생성)
            yield return StartCoroutine(PortalSpawnEffect(centerPos));

            // Heavy 전용 비주얼 생성
            CreateHeavyGoblinVisual(goblin, centerPos);

            // 점유 블록에 압박 효과 적용
            ApplyHeavyPressureEffect(goblin);

            // 등장 포효 모션
            yield return StartCoroutine(RoarAnimation(goblin));
        }

        // ============================================================
        // 빠른 순차 소환 시스템 — 다이나믹 연출
        // ============================================================

        /// <summary>
        /// 셔플된 순서로 촘촘하게 빠르게 순차 소환.
        /// 이쪽저쪽에서 다이나믹하게 등장하는 연출.
        /// </summary>
        private IEnumerator RapidSequentialSpawn(List<(GoblinData goblin, bool isHeavy)> spawns)
        {
            if (spawns == null || spawns.Count == 0) yield break;

            // 셔플: 이쪽저쪽 다이나믹 등장 효과
            for (int i = spawns.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = spawns[i];
                spawns[i] = spawns[j];
                spawns[j] = temp;
            }

            // 소환 사운드 1회 재생
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayEnemySpawnSound();

            float staggerDelay = 0.05f; // 소환 간 간격 (촘촘하게)
            Coroutine lastSpawnCo = null;

            for (int i = 0; i < spawns.Count; i++)
            {
                var (goblin, isHeavy) = spawns[i];

                if (isHeavy)
                {
                    lastSpawnCo = StartCoroutine(RapidSpawnHeavyGoblin(goblin));
                }
                else
                {
                    lastSpawnCo = StartCoroutine(RapidSpawnGoblin(goblin));
                }

                // 마지막 소환이 아니면 짧은 딜레이 후 다음 소환
                if (i < spawns.Count - 1)
                    yield return new WaitForSeconds(staggerDelay);
            }

            // 마지막 소환 애니메이션 완료 대기
            if (lastSpawnCo != null)
                yield return lastSpawnCo;
        }

        /// <summary>
        /// 빠른 일반 고블린 소환: 포털 플래시 + 즉시 비주얼 + 팝인
        /// </summary>
        private IEnumerator RapidSpawnGoblin(GoblinData goblin)
        {
            Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(goblin.position);

            // 빠른 포털 플래시 (fire-and-forget)
            StartCoroutine(RapidPortalFlash(worldPos));

            // 약간의 딜레이 후 비주얼 생성 (포털 펼쳐지는 동안)
            yield return new WaitForSeconds(0.04f);

            // 고블린 비주얼 생성
            CreateGoblinVisual(goblin, worldPos);

            // 빠른 팝인 애니메이션
            yield return StartCoroutine(RapidPopInAnimation(goblin));
        }

        /// <summary>
        /// 빠른 Heavy 고블린 소환
        /// </summary>
        private IEnumerator RapidSpawnHeavyGoblin(GoblinData goblin)
        {
            Vector2 centerPos = CalculateHeavyCenterPosition(goblin.occupiedCoords);

            // 빠른 포털 플래시 (fire-and-forget)
            StartCoroutine(RapidPortalFlash(centerPos));

            yield return new WaitForSeconds(0.04f);

            // Heavy 비주얼 생성
            CreateHeavyGoblinVisual(goblin, centerPos);
            ApplyHeavyPressureEffect(goblin);

            // 빠른 팝인 애니메이션
            yield return StartCoroutine(RapidPopInAnimation(goblin));
        }

        /// <summary>
        /// 빠른 포털 플래시 이펙트 (0.15초).
        /// 보라색 원형 플래시가 빠르게 펼쳐졌다 사라짐.
        /// </summary>
        private IEnumerator RapidPortalFlash(Vector2 position)
        {
            Transform parent = hexGrid.GridContainer;

            GameObject flash = new GameObject("RapidPortalFlash");
            flash.transform.SetParent(parent, false);

            RectTransform frt = flash.AddComponent<RectTransform>();
            frt.anchoredPosition = position;
            float flashSize = hexGrid.HexSize * 1.8f;
            frt.sizeDelta = new Vector2(flashSize, flashSize);
            frt.localScale = Vector3.zero;

            Image fimg = flash.AddComponent<Image>();
            if (portalSprite == null)
                portalSprite = CreatePortalSprite(128);
            fimg.sprite = portalSprite;
            fimg.color = new Color(0.5f, 0.25f, 1f, 0.9f);
            fimg.raycastTarget = false;

            // 팽창 (0.06초) — EaseOutCubic
            float dur1 = 0.06f;
            float elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutCubic(Mathf.Clamp01(elapsed / dur1));
                frt.localScale = Vector3.one * t * 1.1f;
                yield return null;
            }

            // 수축+페이드아웃 (0.09초)
            float dur2 = 0.09f;
            elapsed = 0f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur2);
                float scale = Mathf.Lerp(1.1f, 0.3f, t);
                frt.localScale = Vector3.one * scale;
                fimg.color = new Color(0.5f, 0.25f, 1f, 0.9f * (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 빠른 팝인 애니메이션 (0.18초).
        /// 0 → 1.2 → 1.0 빠른 바운스.
        /// </summary>
        private IEnumerator RapidPopInAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;
            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();

            // 1. 팽창: 0 → 1.2 (0.1초, EaseOutBack)
            float dur1 = 0.1f;
            float elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / dur1));
                rt.localScale = Vector3.one * t * 1.2f;
                yield return null;
            }

            // 2. 안정: 1.2 → 1.0 (0.08초)
            float dur2 = 0.08f;
            elapsed = 0f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur2);
                float scale = Mathf.Lerp(1.2f, 1f, VisualConstants.EaseOutCubic(t));
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;

            // 미니 충격파 (fire-and-forget)
            StartCoroutine(MiniShockwave(goblin));
        }

        /// <summary>
        /// 소형 충격파 이펙트 (빠른 소환용, 0.2초)
        /// </summary>
        private IEnumerator MiniShockwave(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            Transform parent = goblin.visualObject.transform.parent;
            Vector2 pos = goblin.visualObject.GetComponent<RectTransform>().anchoredPosition;

            GameObject wave = new GameObject("MiniShockwave");
            wave.transform.SetParent(parent, false);

            RectTransform wrt = wave.AddComponent<RectTransform>();
            wrt.anchoredPosition = pos;
            wrt.sizeDelta = new Vector2(10f, 10f);

            Image wimg = wave.AddComponent<Image>();
            if (portalSprite == null)
                portalSprite = CreatePortalSprite(128);
            wimg.sprite = portalSprite;
            wimg.color = new Color(0.3f, 0.8f, 0.2f, 0.5f);
            wimg.raycastTarget = false;

            float duration = 0.2f;
            float elapsed = 0f;
            float maxSize = hexGrid.HexSize * 2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(10f, maxSize, VisualConstants.EaseOutCubic(t));
                wrt.sizeDelta = new Vector2(size, size);
                wimg.color = new Color(0.3f, 0.8f, 0.2f, 0.5f * (1f - t));
                yield return null;
            }

            Destroy(wave);
        }

        // ============================================================
        // 포털 소환 이펙트 (기존 — 단독 소환용)
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
            obj.transform.SetAsLastSibling(); // ★ 블록보다 위에 렌더링

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            float size = hexGrid.HexSize * 1.6f;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.zero; // 소환 시 0에서 시작

            // 고블린 타입별 스프라이트 선택 (Lv2는 피부색만 붉은색으로 교체한 별도 스프라이트)
            Sprite selectedSprite;
            bool isLv2 = goblin.monsterLevel >= 2;

            if (goblin.isArcher)
            {
                if (isLv2)
                {
                    if (archerLv2Sprite == null)
                        archerLv2Sprite = CreateArcherGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
                    selectedSprite = archerLv2Sprite;
                }
                else
                {
                    if (archerGoblinSprite == null)
                        archerGoblinSprite = CreateArcherGoblinSprite(256);
                    selectedSprite = archerGoblinSprite;
                }
            }
            else if (goblin.isArmored)
            {
                if (isLv2)
                {
                    if (armoredLv2Sprite == null)
                        armoredLv2Sprite = CreateArmoredGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
                    selectedSprite = armoredLv2Sprite;
                }
                else
                {
                    if (armoredGoblinSprite == null)
                        armoredGoblinSprite = CreateArmoredGoblinSprite(256);
                    selectedSprite = armoredGoblinSprite;
                }
            }
            else if (goblin.isShieldType)
            {
                if (isLv2)
                {
                    if (shieldLv2Sprite == null)
                        shieldLv2Sprite = CreateShieldGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
                    selectedSprite = shieldLv2Sprite;
                }
                else
                {
                    if (shieldGoblinSprite == null)
                        shieldGoblinSprite = CreateShieldGoblinSprite(256);
                    selectedSprite = shieldGoblinSprite;
                }
            }
            else if (goblin.isBomb)
            {
                if (bombGoblinSprite == null)
                    bombGoblinSprite = CreateBombGoblinSprite(256);
                selectedSprite = bombGoblinSprite;
            }
            else if (goblin.isHealer)
            {
                if (goblinSprite == null)
                    goblinSprite = CreateGoblinSprite(256);
                selectedSprite = goblinSprite;
            }
            else if (goblin.isWizard)
            {
                if (wizardGoblinSprite == null)
                    wizardGoblinSprite = CreateWizardGoblinSprite(256);
                selectedSprite = wizardGoblinSprite;
            }
            else if (goblin.isThief)
            {
                if (thiefGoblinSprite == null)
                    thiefGoblinSprite = CreateThiefGoblinSprite(256);
                selectedSprite = thiefGoblinSprite;
            }
            else if (goblin.isWitch)
            {
                if (witchGoblinSprite == null)
                    witchGoblinSprite = CreateWitchGoblinSprite(256);
                selectedSprite = witchGoblinSprite;
            }
            else if (goblin.isUndead)
            {
                if (undeadGoblinSprite == null)
                    undeadGoblinSprite = CreateUndeadGoblinSprite(256);
                selectedSprite = undeadGoblinSprite;
            }
            else
            {
                // 기본 몽둥이 (Lv2이면 붉은 피부)
                if (isLv2)
                {
                    if (goblinLv2Sprite == null)
                        goblinLv2Sprite = CreateGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
                    selectedSprite = goblinLv2Sprite;
                }
                else
                {
                    if (goblinSprite == null)
                        goblinSprite = CreateGoblinSprite(256);
                    selectedSprite = goblinSprite;
                }
            }

            Image img = obj.AddComponent<Image>();
            img.sprite = selectedSprite;
            // 색상 결정: Lv2/힐러/언데드 등 타입별 색상 적용
            img.color = GetGoblinBaseColor(goblin);
            img.raycastTarget = false;

            // 마법사/마녀 고블린: 스프라이트 Y축 반전 보정 (텍스처 좌표계 → UI 좌표계)
            if (goblin.isWizard || goblin.isWitch)
                rt.localRotation = Quaternion.Euler(0, 0, 180);

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
                goblin.shieldOriginalAnchorPos = shieldObj.GetComponent<RectTransform>().anchoredPosition;

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

            // 힐러: 십자가 마크 오버레이
            if (goblin.isHealer)
            {
                // 가로 바
                GameObject crossH = new GameObject("HealCrossH");
                crossH.transform.SetParent(obj.transform, false);
                RectTransform chRt = crossH.AddComponent<RectTransform>();
                chRt.anchoredPosition = new Vector2(0f, size * 0.12f);
                chRt.sizeDelta = new Vector2(size * 0.35f, size * 0.1f);
                Image chImg = crossH.AddComponent<Image>();
                chImg.color = new Color(1f, 1f, 1f, 0.9f);
                chImg.raycastTarget = false;

                // 세로 바
                GameObject crossV = new GameObject("HealCrossV");
                crossV.transform.SetParent(obj.transform, false);
                RectTransform cvRt = crossV.AddComponent<RectTransform>();
                cvRt.anchoredPosition = new Vector2(0f, size * 0.12f);
                cvRt.sizeDelta = new Vector2(size * 0.1f, size * 0.35f);
                Image cvImg = crossV.AddComponent<Image>();
                cvImg.color = new Color(1f, 1f, 1f, 0.9f);
                cvImg.raycastTarget = false;
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

            // 마법사/마녀 고블린: 부모가 180도 회전이므로 HP바/텍스트 위치+회전 반대 보정
            if (goblin.isWizard || goblin.isWitch)
            {
                // 위치 반전: 부모 180도 회전으로 Y축이 뒤집히므로 양수 Y = 화면 아래
                bgRt.anchoredPosition = new Vector2(0, -offsetY);
                textRt.anchoredPosition = new Vector2(0, -offsetY + barHeight + 2f);
                // 회전 보정: 텍스트/바가 뒤집히지 않도록
                bgRt.localRotation = Quaternion.Euler(0, 0, 180);
                textRt.localRotation = Quaternion.Euler(0, 0, 180);
            }
        }

        // ============================================================
        // Heavy 고블린 비주얼
        // ============================================================

        /// <summary>
        /// Heavy 고블린 전용 비주얼 생성: 3블록 중앙에 큰 프로시저럴 스프라이트 배치
        /// </summary>
        private void CreateHeavyGoblinVisual(GoblinData goblin, Vector2 centerPosition)
        {
            Transform parent = hexGrid.GridContainer;

            // 메인 오브젝트
            GameObject obj = new GameObject("HeavyGoblin");
            obj.transform.SetParent(parent, false);
            obj.transform.SetAsLastSibling(); // ★ 블록보다 위에 렌더링

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = centerPosition;
            // 3블록 범위의 90% 크기
            float size = hexGrid.HexSize * 2.8f;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.zero; // 소환 시 0에서 시작

            // Heavy 전용 스프라이트
            if (heavyGoblinSprite == null)
                heavyGoblinSprite = CreateHeavyGoblinSprite(256);

            Image img = obj.AddComponent<Image>();
            img.sprite = heavyGoblinSprite;
            img.color = Color.white;
            img.raycastTarget = false;

            goblin.visualObject = obj;
            goblin.goblinImage = img;

            // HP바 생성 (큰 사이즈)
            CreateHPBar(goblin, obj.transform, size);
        }

        /// <summary>
        /// Heavy 고블린 점유 블록에 압박 효과 (스케일 Y 0.95)
        /// </summary>
        private void ApplyHeavyPressureEffect(GoblinData goblin)
        {
            if (goblin.occupiedCoords == null || hexGrid == null) return;

            foreach (var coord in goblin.occupiedCoords)
            {
                if (hexGrid.IsInsideGrid(coord))
                {
                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block != null)
                    {
                        RectTransform brt = block.GetComponent<RectTransform>();
                        if (brt != null)
                            brt.localScale = new Vector3(1f, 0.95f, 1f);
                    }
                }
            }
        }

        /// <summary>
        /// Heavy 고블린 프로시저럴 스프라이트 생성: 진한 갈색 근육질 고블린
        /// </summary>
        private static Sprite CreateHeavyGoblinSprite(int texSize)
        {
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[texSize * texSize];
            float center = texSize / 2f;
            float radius = texSize * 0.42f;

            Color bodyColor = new Color(0.35f, 0.25f, 0.15f, 1f);      // 진한 갈색
            Color darkAccent = new Color(0.25f, 0.17f, 0.10f, 1f);     // 더 진한 갈색 (그림자)
            Color eyeColor = new Color(1f, 0.3f, 0.1f, 1f);             // 붉은 눈
            Color hornColor = new Color(0.45f, 0.35f, 0.20f, 1f);       // 뿔 색

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    Color c = Color.clear;

                    // 몸통 (넓적한 타원)
                    float bodyW = radius * 1.1f;
                    float bodyH = radius * 0.95f;
                    float bodyDist = Mathf.Sqrt((dx * dx) / (bodyW * bodyW) + (dy * dy) / (bodyH * bodyH));

                    if (bodyDist < 1f)
                    {
                        // 기본 몸통 색 + 그림자 그라디언트
                        float shade = 1f - bodyDist * 0.3f;
                        c = Color.Lerp(darkAccent, bodyColor, shade);

                        // 어깨 강조 (위쪽 넓게)
                        if (dy > radius * 0.1f && Mathf.Abs(dx) > radius * 0.3f)
                            c = Color.Lerp(c, darkAccent, 0.2f);

                        // 눈 (2개, 붉은색)
                        float eyeY = center + radius * 0.2f;
                        float eyeSpacing = radius * 0.25f;
                        float eyeR = radius * 0.08f;

                        float leftEyeDist = Mathf.Sqrt((x - (center - eyeSpacing)) * (x - (center - eyeSpacing)) + (y - eyeY) * (y - eyeY));
                        float rightEyeDist = Mathf.Sqrt((x - (center + eyeSpacing)) * (x - (center + eyeSpacing)) + (y - eyeY) * (y - eyeY));

                        if (leftEyeDist < eyeR || rightEyeDist < eyeR)
                            c = eyeColor;

                        // 입 (넓은 일자)
                        float mouthY = center - radius * 0.05f;
                        if (Mathf.Abs(y - mouthY) < radius * 0.04f && Mathf.Abs(dx) < radius * 0.35f)
                            c = darkAccent;
                    }

                    // 뿔 (2개, 위쪽)
                    float hornBaseY = center + radius * 0.6f;
                    float hornSpacing = radius * 0.4f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float hx = center + side * hornSpacing;
                        float hornDx = x - hx;
                        float hornDy = y - hornBaseY;
                        if (hornDy > 0 && hornDy < radius * 0.5f)
                        {
                            float hornWidth = radius * 0.12f * (1f - hornDy / (radius * 0.5f));
                            if (Mathf.Abs(hornDx - side * hornDy * 0.3f) < hornWidth)
                                c = hornColor;
                        }
                    }

                    // 엣지 안티앨리어싱
                    if (bodyDist > 0.9f && bodyDist < 1f)
                        c.a *= 1f - (bodyDist - 0.9f) / 0.1f;

                    pixels[y * texSize + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 고블린의 기본 색상 반환 (Lv2/힐러/언데드 등 타입별 색상)
        /// 데미지 플래시 후 복원할 때 사용
        /// </summary>
        private Color GetGoblinBaseColor(GoblinData goblin)
        {
            if (goblin.isHealer) return new Color(0.3f, 0.9f, 0.4f, 1f);
            if (goblin.isUndead) return new Color(0.6f, 0.8f, 0.6f, 1f);
            if (goblin.monsterLevel >= 2)
            {
                return Color.white; // Lv2: 틴트 없음 (피부색은 스프라이트 텍스처에 구워짐)
            }
            return Color.white;
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

            // ★ HP 숫자 카운트다운 애니메이션 (0.1초 간격으로 1씩 감소)
            if (goblin.hpText != null)
            {
                // 표시 HP 초기화 안 됐으면 현재 maxHp로 설정
                if (goblin.displayedHp < 0)
                    goblin.displayedHp = goblin.maxHp;

                if (goblin.displayedHp != goblin.hp && !goblin.isCountingDown)
                {
                    StartCoroutine(HPCountdownAnimation(goblin));
                }
            }
        }

        /// <summary>
        /// HP 텍스트 카운트다운: displayedHp → hp까지 0.1초 간격으로 1씩 변화
        /// </summary>
        private IEnumerator HPCountdownAnimation(GoblinData goblin)
        {
            goblin.isCountingDown = true;

            while (goblin.displayedHp != goblin.hp)
            {
                if (goblin.hpText == null) break;

                // 1씩 목표값 방향으로 이동
                if (goblin.displayedHp > goblin.hp)
                    goblin.displayedHp--;
                else
                    goblin.displayedHp++;

                goblin.hpText.text = $"{Mathf.Max(0, goblin.displayedHp)}";

                // 숫자 변경 시 살짝 스케일 펀치
                if (goblin.hpText != null)
                {
                    RectTransform textRt = goblin.hpText.GetComponent<RectTransform>();
                    if (textRt != null)
                        StartCoroutine(HPTextPunch(textRt));
                }

                yield return new WaitForSeconds(0.1f);
            }

            // 최종값 보장
            if (goblin.hpText != null)
                goblin.hpText.text = $"{Mathf.Max(0, goblin.hp)}";
            goblin.displayedHp = goblin.hp;
            goblin.isCountingDown = false;
        }

        /// <summary>
        /// HP 숫자 변경 시 작은 스케일 펄스 (1.3→1.0, 0.08초)
        /// </summary>
        private IEnumerator HPTextPunch(RectTransform rt)
        {
            if (rt == null) yield break;
            float dur = 0.08f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float scale = Mathf.Lerp(1.3f, 1.0f, t);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
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
            // Heavy 고블린: occupiedCoords 중 하나라도 매칭되면 해당 Heavy에 데미지
            if (goblin == null)
                goblin = goblins.FirstOrDefault(g => g.isAlive && g.isHeavy && g.occupiedCoords != null && g.occupiedCoords.Contains(position));
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
                OnGoblinKilled?.Invoke(totalKills, goblin.isArmored, goblin.isArcher, goblin.isShieldType, goblin.isBomb, goblin.isHealer, goblin.isHeavy, goblin.isWizard, goblin.isThief, goblin.isWitch, goblin.monsterLevel);
                StartCoroutine(DeathAnimation(goblin));
                CheckUndeadDeathTrigger(goblin);
            }
        }

        /// <summary>
        /// 복수 좌표에서 발생한 데미지를 고블린별로 누적하여 일괄 적용.
        /// 몬스터가 서있는 블록 + 주변 블록이 동시에 삭제되면 해당 수량만큼 대미지.
        /// key=좌표, value=해당 좌표에 가할 데미지.
        /// directHitCoords: 고블린 자리에서 직접 매칭된 좌표 (바닥 매칭).
        /// </summary>
        public void ApplyBatchDamage(Dictionary<HexCoord, int> damageMap,
            HashSet<HexCoord> directHitCoords = null,
            HashSet<HexCoord> crackedDirectHitCoords = null,
            bool bypassShield = false)
        {
            if (damageMap == null || damageMap.Count == 0) return;

            // 고블린별 누적 데미지 계산
            Dictionary<GoblinData, int> goblinDamage = new Dictionary<GoblinData, int>();
            foreach (var kvp in damageMap)
            {
                var goblin = goblins.FirstOrDefault(g => g.isAlive && g.position == kvp.Key);
                // Heavy 고블린: occupiedCoords 중 하나라도 매칭
                if (goblin == null)
                    goblin = goblins.FirstOrDefault(g => g.isAlive && g.isHeavy && g.occupiedCoords != null && g.occupiedCoords.Contains(kvp.Key));
                if (goblin == null) continue;

                if (goblinDamage.ContainsKey(goblin))
                {
                    // ★ Heavy 고블린: 여러 occupiedCoords가 범위에 있어도 가장 큰 데미지 1번만
                    if (goblin.isHeavy)
                        goblinDamage[goblin] = Mathf.Max(goblinDamage[goblin], kvp.Value);
                    else
                        goblinDamage[goblin] += kvp.Value;
                }
                else
                    goblinDamage[goblin] = kvp.Value;
            }

            // 누적된 대미지를 한번에 적용
            foreach (var kvp in goblinDamage)
            {
                GoblinData goblin = kvp.Key;
                int totalDamage = kvp.Value;

                // ★ 방패 활성 시: 방패 내구도가 모두 깎이기 전까지 본체 HP 보호
                if (goblin.isShielded)
                {
                    if (bypassShield)
                    {
                        // 매칭 데미지: 방패 내구도 감소 없음 + 본체 데미지 없음 → 완전 차단
                        SpawnInfoPopup(goblin, "방어", Color.white);
                        continue;
                    }

                    bool isDirectHit = directHitCoords != null && directHitCoords.Contains(goblin.position);
                    bool isCrackedHit = crackedDirectHitCoords != null && crackedDirectHitCoords.Contains(goblin.position);

                    if (isDirectHit)
                    {
                        // 직접 타격 (폭탄/드릴 등) → 방패에 데미지 전달 (ApplyShieldDamage 내부에서 "방어" 팝업)
                        ApplyShieldDamage(goblin, totalDamage);
                    }
                    else if (isCrackedHit)
                    {
                        // 깨진 블록 직격: 방패 내구도 감소 없이 본체 데미지도 차단
                        Debug.Log($"[GoblinSystem] 깨진 블록으로는 방패를 뚫을 수 없습니다! ({goblin.position})");
                        SpawnInfoPopup(goblin, "방어", Color.white);
                    }
                    else
                    {
                        // 인접 데미지: 방패 내구도 감소 없이 본체 데미지 차단
                        SpawnInfoPopup(goblin, "방어", Color.white);
                    }
                    // 방패 활성 중에는 본체 데미지 차단
                    continue;
                }

                // ★ 깨진 블록 위치의 일반 몬스터: 1 대미지만 (누적 대미지 무시)
                bool onlyCrackedHit = crackedDirectHitCoords != null
                    && crackedDirectHitCoords.Contains(goblin.position)
                    && (directHitCoords == null || !directHitCoords.Contains(goblin.position));
                if (onlyCrackedHit)
                    totalDamage = 1;

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
                    OnGoblinKilled?.Invoke(totalKills, goblin.isArmored, goblin.isArcher, goblin.isShieldType, goblin.isBomb, goblin.isHealer, goblin.isHeavy, goblin.isWizard, goblin.isThief, goblin.isWitch, goblin.monsterLevel);
                    StartCoroutine(DeathAnimation(goblin));
                    CheckUndeadDeathTrigger(goblin);
                }
            }
        }

        /// <summary>
        /// 특정 좌표에 고블린이 있는지 확인
        /// </summary>
        public bool HasGoblinAt(HexCoord coord)
        {
            return goblins.Any(g => g.isAlive && g.position == coord)
                || goblins.Any(g => g.isAlive && g.isHeavy && g.occupiedCoords != null && g.occupiedCoords.Contains(coord));
        }

        /// <summary>
        /// Heavy 고블린이 이동/착지한 좌표에 있는 특수 블록을 발동 없이 단순 파괴.
        /// MoveBlock/FixedBlock은 제외. 파괴 후 낙하 처리 실행.
        /// </summary>
        private void CrushSpecialBlocksUnderHeavy(List<HexCoord> coords)
        {
            if (hexGrid == null || coords == null) return;

            foreach (var coord in coords)
            {
                if (!hexGrid.IsInsideGrid(coord)) continue;
                HexBlock block = hexGrid.GetBlock(coord);
                if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;

                var sType = block.Data.specialType;
                if (sType == SpecialBlockType.None) continue;
                // MoveBlock/FixedBlock은 Heavy도 무력화 불가
                if (sType == SpecialBlockType.MoveBlock || sType == SpecialBlockType.FixedBlock) continue;

                Debug.Log($"[GoblinSystem] Heavy 고블린 밟기: 특수 블록 무력화 {coord} ({sType}) — 블록 유지, 특수 기능만 제거");
                // 특수 기능만 제거 (블록 자체는 유지, 낙하 없음)
                block.Data.specialType = SpecialBlockType.None;
                block.Data.isCracked = true;
                block.UpdateVisuals();
                StartCoroutine(BlockFlashEffect(block.gameObject, new Color(0.6f, 0.6f, 0.6f, 1f))); // 회색 플래시
            }
        }

        /// <summary>
        /// 살아있는 모든 고블린 목록 반환
        /// </summary>
        public List<GoblinData> GetAliveGoblins()
        {
            return goblins.Where(g => g.isAlive).ToList();
        }

        /// <summary>
        /// 모든 살아있는 고블린 비주얼을 GridContainer 내 최상위로 재배치.
        /// 블록 리필/스폰 후 호출하여 고블린이 블록 위에 렌더링되도록 보장.
        /// </summary>
        public void BringGoblinsToFront()
        {
            foreach (var goblin in goblins)
            {
                if (goblin.isAlive && goblin.visualObject != null)
                    goblin.visualObject.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 특정 열(q)에 있는 살아있는 고블린 목록
        /// </summary>
        public List<GoblinData> GetGoblinsInColumn(int q)
        {
            return goblins.Where(g => g.isAlive && g.position.q == q).ToList();
        }

        /// <summary>
        /// 특정 좌표에 있는 살아있는 고블린 반환 (없으면 null)
        /// </summary>
        public GoblinData GetGoblinAt(HexCoord coord)
        {
            // 일반 고블린: position 매칭
            var normal = goblins.FirstOrDefault(g => g.isAlive && !g.isHeavy && g.position == coord);
            if (normal != null) return normal;
            // Heavy 고블린: occupiedCoords 중 하나라도 매칭
            return goblins.FirstOrDefault(g => g.isAlive && g.isHeavy && g.occupiedCoords != null && g.occupiedCoords.Contains(coord));
        }

        /// <summary>
        /// 폭탄 넉백: 폭발 범위 내 모든 몬스터를 폭탄→몬스터 정확한 직선상으로 밀어냄.
        /// explodeRange: 폭발 범위 (기본 2, 폭탄×폭탄은 4)
        /// 목적지 = bombPos + 방향 × (explodeRange + 1)
        /// </summary>
        public IEnumerator KnockbackFromBomb(HexCoord bombPos, int explodeRange = 2)
        {
            if (hexGrid == null) yield break;

            // 이동 완료된 몬스터 좌표 추적 (겹침 방지)
            var movedPositions = new HashSet<HexCoord>();

            var snapshot = new List<GoblinData>(goblins);

            // 넉백 동시 실행용 코루틴 리스트
            var knockbackCoroutines = new List<Coroutine>();

            foreach (var goblin in snapshot)
            {
                if (goblin == null || !goblin.isAlive || goblin.visualObject == null) continue;

                // ★ Heavy 고블린은 넉백 면역
                if (goblin.isHeavy) continue;

                HexCoord origPos = goblin.position;

                // cube distance
                int dq = origPos.q - bombPos.q;
                int dr = origPos.r - bombPos.r;
                int ds = -dq - dr;
                int dist = (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(ds)) / 2;

                if (dist > explodeRange) continue;

                // === 직선 방향 설정 (hex line lerp) ===
                float aq = bombPos.q, ar = bombPos.r, aS = -aq - ar;
                float bq, br, bS;

                if (dist == 0)
                {
                    HexCoord randDir = HexCoord.Directions[Random.Range(0, 6)];
                    bq = bombPos.q + randDir.q;
                    br = bombPos.r + randDir.r;
                    bS = -bq - br;
                    dist = 1;
                }
                else
                {
                    bq = origPos.q;
                    br = origPos.r;
                    bS = -bq - br;
                }

                // === 목적지 = bombPos + 방향 × (explodeRange+1+스킬보너스) 직접 계산 ===
                int skillBonus = 0;
                if (SkillTreeManager.Instance != null)
                    skillBonus = SkillTreeManager.Instance.GetBombKnockbackBonus();
                int destMultiplier = explodeRange + 1 + skillBonus;
                float t3 = (float)destMultiplier / dist;
                float destQ = aq + (bq - aq) * t3;
                float destR = ar + (br - ar) * t3;
                float destS = aS + (bS - aS) * t3;
                HexCoord targetDest = HexRoundCube(destQ, destR, destS);

                int targetBound = CheckBoundsType(targetDest);

                if (targetBound == 0)
                {
                    // 그리드 내부: 블록 존재 + 미점유 확인
                    bool canMove = false;
                    if (hexGrid.IsValidCoord(targetDest))
                    {
                        HexBlock block = hexGrid.GetBlock(targetDest);
                        if (block != null)
                        {
                            bool occupied = movedPositions.Contains(targetDest);
                            if (!occupied)
                            {
                                foreach (var g in goblins)
                                {
                                    if (g != null && g.isAlive && g != goblin && g.position == targetDest)
                                    { occupied = true; break; }
                                }
                            }
                            canMove = !occupied;
                        }
                    }

                    if (canMove)
                    {
                        goblin.position = targetDest;
                        movedPositions.Add(targetDest);
                        Vector2 destWorldPos = hexGrid.CalculateFlatTopHexPosition(targetDest);
                        knockbackCoroutines.Add(StartCoroutine(KnockbackFlyTo(goblin, destWorldPos)));
                    }
                    // 블록 없거나 점유 → 이동 불가, 제자리 유지
                }
                else if (targetBound == 1)
                {
                    // 소환지역: 해당 위치로 이동
                    goblin.position = targetDest;
                    movedPositions.Add(targetDest);
                    Vector2 spawnWorldPos = hexGrid.CalculateFlatTopHexPosition(targetDest);
                    knockbackCoroutines.Add(StartCoroutine(KnockbackFlyTo(goblin, spawnWorldPos)));
                }
                else
                {
                    // 완전 밖(boundResult==2): 방향 역추적으로 마지막 유효 좌표 찾기
                    HexCoord lastValid = origPos;
                    for (int n = 1; n <= destMultiplier; n++)
                    {
                        float tn = (float)n / dist;
                        float lnQ = aq + (bq - aq) * tn;
                        float lnR = ar + (br - ar) * tn;
                        float lnS = aS + (bS - aS) * tn;
                        HexCoord step = HexRoundCube(lnQ, lnR, lnS);
                        int stepBound = CheckBoundsType(step);
                        if (stepBound == 2) break;
                        lastValid = step;
                    }

                    // 목적지(targetDest)의 월드 좌표를 hex→world 변환으로 직접 계산
                    // flat-top hex: worldX = hexSize * 1.5 * q, worldY = hexSize * sqrt(3) * (r + q*0.5)
                    float hs = hexGrid.HexSize;
                    float destWX = hs * 1.5f * targetDest.q;
                    float destWY = hs * Mathf.Sqrt(3f) * (targetDest.r + targetDest.q * 0.5f);
                    // bombPos 기준 오프셋 → 현재 위치에서의 상대 이동량으로 변환
                    float origWX = hs * 1.5f * origPos.q;
                    float origWY = hs * Mathf.Sqrt(3f) * (origPos.r + origPos.q * 0.5f);
                    RectTransform grt = goblin.visualObject.GetComponent<RectTransform>();
                    Vector2 flyTarget = (grt != null) ? grt.anchoredPosition + new Vector2(destWX - origWX, -(destWY - origWY)) : Vector2.zero;
                    knockbackCoroutines.Add(StartCoroutine(KnockbackFlyAndDie(goblin, flyTarget)));
                }
            }

            // 모든 넉백 연출 동시 진행 후 완료 대기
            foreach (var co in knockbackCoroutines)
                yield return co;
        }

        /// <summary>
        /// 넉백 이동 연출: 현재 위치 → 목적지로 0.2초 비행
        /// </summary>
        private IEnumerator KnockbackFlyTo(GoblinData goblin, Vector2 targetPos)
        {
            if (goblin == null || goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 startPos = rt.anchoredPosition;
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseOutCubic
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, ease);
                yield return null;
            }
            rt.anchoredPosition = targetPos;
        }

        /// <summary>
        /// 넉백 즉사 연출: bombPos + 방향×N 월드 좌표까지 0.3초 이동 후 사망 처리.
        /// </summary>
        private IEnumerator KnockbackFlyAndDie(GoblinData goblin, Vector2 targetWorldPos)
        {
            if (goblin == null || goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 startPos = rt.anchoredPosition;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(startPos, targetWorldPos, t);
                yield return null;
            }
            rt.anchoredPosition = targetWorldPos;

            KillGoblinByKnockback(goblin);
        }

        /// <summary>
        /// Cube 좌표 float → 가장 가까운 정수 hex 좌표 반올림.
        /// q+r+s=0 제약 유지: 반올림 오차가 가장 큰 성분을 나머지 둘로 보정.
        /// </summary>
        private HexCoord HexRoundCube(float q, float r, float s)
        {
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float diffQ = Mathf.Abs(rq - q);
            float diffR = Mathf.Abs(rr - r);
            float diffS = Mathf.Abs(rs - s);

            if (diffQ > diffR && diffQ > diffS)
                rq = -rr - rs;
            else if (diffR > diffS)
                rr = -rq - rs;
            // else rs = -rq - rr (불필요 — axial은 q,r만 사용)

            return new HexCoord(rq, rr);
        }

        // ============================================================
        // 폭탄 넉백 시스템 (레거시 — 코루틴 기반)
        // ============================================================

        /// <summary>
        /// 폭탄 폭발 완료 후 범위 내 모든 고블린(4종 예외 없음)을 밖으로 밀어냄.
        /// 가까운 몬스터부터 순차 처리. 연쇄 넉백 지원.
        /// 그리드 상단 밖→스폰 영역, 좌/우/하단 밖→즉시 사망.
        /// 중복 호출 방지 플래그 포함.
        /// </summary>
        private bool isKnockbackRunning = false;

        public IEnumerator ApplyBombKnockback(HexCoord bombCoord, HashSet<HexCoord> ring1Coords, HashSet<HexCoord> ring2Coords)
        {
            if (hexGrid == null) yield break;
            if (isKnockbackRunning) yield break; // 중복 호출 방지
            isKnockbackRunning = true;

            try
            {
                // ★ 4종 모두 예외 없이 수집 (isArcher, isArmored, isShielded 등 조건 무시)
                var allInRange = goblins.Where(g => g.isAlive && (
                    g.position == bombCoord
                    || ring1Coords.Contains(g.position)
                    || ring2Coords.Contains(g.position)
                )).ToList();

                if (allInRange.Count == 0) yield break;

                Debug.Log($"[GoblinSystem] 넉백 대상: {allInRange.Count}마리 (범위 내 전체)");

                // 거리순 정렬 (가까운 몬스터부터 처리)
                allInRange.Sort((a, b) =>
                {
                    int distA = GetBombDistance(a.position, bombCoord, ring1Coords, ring2Coords);
                    int distB = GetBombDistance(b.position, bombCoord, ring1Coords, ring2Coords);
                    return distA.CompareTo(distB);
                });

                int totalKnocked = 0;

                foreach (var goblin in allInRange)
                {
                    if (!goblin.isAlive) continue; // 데미지로 이미 사망한 경우

                    // 넉백 방향 결정
                    HexCoord offset = goblin.position - bombCoord;
                    int currentDist = GetBombDistance(goblin.position, bombCoord, ring1Coords, ring2Coords);
                    HexCoord dir;

                    if (currentDist == 0)
                    {
                        // 직격(0칸): 그리드 안쪽으로 밀릴 수 있는 유효 방향 우선 랜덤
                        dir = PickValidKnockbackDirection(bombCoord);
                    }
                    else
                    {
                        // 1칸/2칸: 폭탄→몬스터 방향
                        dir = FindNearestHexDirection(offset);
                    }

                    // 넉백 목적지 거리 계산 (폭탄 중심 기준)
                    // - 직격(0칸): 랜덤 방향 3칸째 (3칸 이동)
                    // - 1칸: 4칸째 (3칸 이동)
                    // - 2칸 직선: 4칸째 (2칸 이동)
                    // - 2칸 꺾임: 5칸째 (3칸 이동)
                    int targetDistFromBomb;
                    if (currentDist == 0)
                    {
                        targetDistFromBomb = 3;
                    }
                    else if (currentDist == 1)
                    {
                        targetDistFromBomb = 4;
                    }
                    else
                    {
                        // 2칸: 직선/꺾임 판별
                        bool isStraight = IsStraightHexLine(offset);
                        targetDistFromBomb = isStraight ? 4 : 5;
                    }
                    // 이동 칸 수 = 목적지까지 거리 - 현재 거리
                    int neededDistance = targetDistFromBomb - currentDist;
                    if (neededDistance <= 0) neededDistance = 1;

                    // 목표 위치 계산 (블록 장애물만 체크, 고블린은 연쇄 대상)
                    HexCoord targetPos = goblin.position;
                    bool hitOutOfBounds = false;
                    HexCoord outOfBoundsPos = targetPos;

                    for (int step = 0; step < neededDistance; step++)
                    {
                        HexCoord nextPos = targetPos + dir;

                        // 경계 판정: 그리드 밖인지, 어느 방향 밖인지
                        int boundResult = CheckBoundsType(nextPos);
                        if (boundResult == 2)
                        {
                            // 좌/우/하단 밖 → 즉시 사망 처리 대상
                            hitOutOfBounds = true;
                            outOfBoundsPos = nextPos;
                            break;
                        }
                        // boundResult == 1 (상단 스폰 영역)은 유효 → 계속 진행

                        // 그리드 안쪽이면 블록 체크 (블록 있으면 멈춤)
                        if (hexGrid.IsInsideGrid(nextPos))
                        {
                            HexBlock block = hexGrid.GetBlock(nextPos);
                            if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                                break;
                        }

                        targetPos = nextPos;
                    }

                    // 좌/우/하단 밖으로 밀려남 → 넉백 애니메이션 후 사망
                    if (hitOutOfBounds)
                    {
                        Vector2 deathWorldPos = hexGrid.CalculateFlatTopHexPosition(outOfBoundsPos);
                        yield return StartCoroutine(AnimateKnockback(goblin, deathWorldPos));
                        KillGoblinByKnockback(goblin);
                        totalKnocked++;
                        Debug.Log($"[GoblinSystem] 넉백 사망: ({goblin.position}) → 그리드 밖 ({outOfBoundsPos})");
                        continue;
                    }

                    if (targetPos == goblin.position) continue;

                    // 연쇄 넉백: 목표 위치에 다른 몬스터가 있으면 같은 방향으로 1칸 추가
                    var blockingGoblin = goblins.FirstOrDefault(g =>
                        g.isAlive && g != goblin && g.position == targetPos);

                    if (blockingGoblin != null)
                    {
                        HexCoord chainTarget = targetPos + dir;
                        int chainBound = CheckBoundsType(chainTarget);

                        bool canChain = true;

                        // 블록 체크
                        if (canChain && hexGrid.IsInsideGrid(chainTarget))
                        {
                            HexBlock block = hexGrid.GetBlock(chainTarget);
                            if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                                canChain = false;
                        }

                        // 또 다른 고블린 체크
                        if (canChain && goblins.Any(g =>
                            g.isAlive && g != blockingGoblin && g != goblin && g.position == chainTarget))
                            canChain = false;

                        if (canChain && chainBound == 2)
                        {
                            // 연쇄 대상이 그리드 밖(좌/우/하단)으로 밀려남 → 사망
                            Vector2 chainDeathPos = hexGrid.CalculateFlatTopHexPosition(chainTarget);
                            yield return StartCoroutine(AnimateKnockback(blockingGoblin, chainDeathPos));
                            KillGoblinByKnockback(blockingGoblin);
                            totalKnocked++;
                            Debug.Log($"[GoblinSystem] 연쇄 넉백 사망: ({targetPos}) → ({chainTarget})");
                        }
                        else if (canChain)
                        {
                            // 연쇄 넉백: 방해 몬스터를 먼저 1칸 밀어냄
                            blockingGoblin.position = chainTarget;
                            Vector2 chainWorldPos = hexGrid.CalculateFlatTopHexPosition(chainTarget);
                            yield return StartCoroutine(AnimateKnockback(blockingGoblin, chainWorldPos));
                            totalKnocked++;
                            Debug.Log($"[GoblinSystem] 연쇄 넉백: ({targetPos}) → ({chainTarget})");
                        }
                        else
                        {
                            // 연쇄 불가 → 한 칸 앞에서 멈춤
                            targetPos = targetPos - dir;
                            if (targetPos == goblin.position) continue;
                        }
                    }

                    // 메인 넉백 애니메이션
                    goblin.position = targetPos;
                    Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(targetPos);
                    yield return StartCoroutine(AnimateKnockback(goblin, worldPos));
                    totalKnocked++;
                }

                if (totalKnocked > 0)
                    Debug.Log($"[GoblinSystem] 폭탄 넉백 완료: {totalKnocked}마리 처리");
            }
            finally
            {
                isKnockbackRunning = false;
            }
        }

        /// <summary>
        /// 넉백으로 그리드 밖에 밀려난 고블린을 즉시 사망 처리.
        /// </summary>
        public void KillGoblinByKnockback(GoblinData goblin)
        {
            if (!goblin.isAlive) return;
            goblin.hp = 0;
            goblin.isAlive = false;
            totalKills++;
            IncrementTypeKill(goblin);
            OnGoblinKilled?.Invoke(totalKills, goblin.isArmored, goblin.isArcher, goblin.isShieldType, goblin.isBomb, goblin.isHealer, goblin.isHeavy, goblin.isWizard, goblin.isThief, goblin.isWitch, goblin.monsterLevel);
            StartCoroutine(DeathAnimation(goblin));
            CheckUndeadDeathTrigger(goblin);
        }

        /// <summary>
        /// 좌표의 경계 타입을 판정.
        /// 반환: 0=그리드 내부 또는 유효 범위, 1=상단 스폰 영역, 2=좌/우/하단 밖 (사망 영역)
        /// </summary>
        public int CheckBoundsType(HexCoord pos)
        {
            if (hexGrid == null) return 2;
            int radius = hexGrid.GridRadius;

            // q 범위 벗어남 → 좌/우 밖
            if (Mathf.Abs(pos.q) > radius) return 2;

            int rMin = hexGrid.GetTopR(pos.q);
            int rMax = Mathf.Min(radius, -pos.q + radius);

            // 그리드 내부
            if (pos.r >= rMin && pos.r <= rMax) return 0;

            // 상단 밖 (r < rMin): 스폰 영역 (rMin - 3까지 유효)
            if (pos.r < rMin && pos.r >= rMin - 3) return 1;

            // 상단 더 바깥 (rMin - 3보다 위) → 스폰 영역 최상단으로 간주
            if (pos.r < rMin - 3) return 1;

            // 하단 밖 (r > rMax) → 사망
            return 2;
        }

        /// <summary>
        /// 폭탄 중심으로부터의 거리 (0=중심, 1=ring1, 2=ring2)
        /// </summary>
        private int GetBombDistance(HexCoord pos, HexCoord bombCoord,
            HashSet<HexCoord> ring1Coords, HashSet<HexCoord> ring2Coords)
        {
            if (pos == bombCoord) return 0;
            if (ring1Coords.Contains(pos)) return 1;
            if (ring2Coords.Contains(pos)) return 2;
            return 3;
        }

        /// <summary>
        /// 오프셋 벡터에서 가장 가까운 6방향 중 하나를 반환.
        /// axial 좌표를 cube 좌표로 변환 후 각도 비교.
        /// </summary>
        private HexCoord FindNearestHexDirection(HexCoord offset)
        {
            if (offset.q == 0 && offset.r == 0)
            {
                // 중심에 있는 경우: 랜덤 방향
                return HexCoord.Directions[Random.Range(0, 6)];
            }

            // Cube 좌표 변환: x=q, z=r, y=-q-r
            float ox = offset.q;
            float oz = offset.r;
            float oy = -ox - oz;

            // 월드 방향 벡터 계산 (flat-top hex)
            float worldX = ox * 1.5f;
            float worldY = Mathf.Sqrt(3f) * (oz + ox * 0.5f);

            float bestDot = float.MinValue;
            HexCoord bestDir = HexCoord.Directions[0];

            foreach (var dir in HexCoord.Directions)
            {
                float dx = dir.q * 1.5f;
                float dy = Mathf.Sqrt(3f) * (dir.r + dir.q * 0.5f);
                float dot = worldX * dx + worldY * dy;
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestDir = dir;
                }
            }

            return bestDir;
        }

        /// <summary>
        /// axial 오프셋이 6방향 단위벡터의 정수배인지 판별.
        /// 정확히 직선이면 true, 꺾인 칸이면 false.
        /// 예: (2,0), (0,-2), (-1,1)*2 등은 직선. (1,-2), (2,-1) 등은 꺾임.
        /// </summary>
        private bool IsStraightHexLine(HexCoord offset)
        {
            if (offset.q == 0 && offset.r == 0) return true;

            foreach (var dir in HexCoord.Directions)
            {
                // offset = dir * n (n > 0) 인지 확인
                if (dir.q != 0)
                {
                    if (offset.q % dir.q == 0)
                    {
                        int n = offset.q / dir.q;
                        if (n > 0 && offset.r == dir.r * n)
                            return true;
                    }
                }
                else if (dir.r != 0)
                {
                    if (offset.r % dir.r == 0)
                    {
                        int n = offset.r / dir.r;
                        if (n > 0 && offset.q == 0)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 직격(0칸) 넉백용: 그리드 밖으로 나가지 않는 유효 방향 우선 랜덤 선택.
        /// 유효 방향이 없으면 완전 랜덤 (이탈 시 즉사 처리는 호출자에서 수행).
        /// </summary>
        private HexCoord PickValidKnockbackDirection(HexCoord bombCoord)
        {
            var validDirs = new List<HexCoord>();
            foreach (var d in HexCoord.Directions)
            {
                // 3칸째 목적지가 사망 영역이 아닌지 확인
                HexCoord dest = bombCoord + new HexCoord(d.q * 3, d.r * 3);
                int bound = CheckBoundsType(dest);
                if (bound != 2) // 0(그리드 내부) 또는 1(스폰 영역) → 유효
                    validDirs.Add(d);
            }

            if (validDirs.Count > 0)
                return validDirs[Random.Range(0, validDirs.Count)];

            // 유효 방향 없으면 완전 랜덤
            return HexCoord.Directions[Random.Range(0, 6)];
        }

        /// <summary>
        /// 넉백 애니메이션: 빠르게 튕겨나가는 포물선 느낌
        /// </summary>
        public IEnumerator AnimateKnockback(GoblinData goblin, Vector2 targetPos)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 startPos = rt.anchoredPosition;
            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (goblin.visualObject == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // EaseOutBack: 살짝 튕기는 느낌
                float eased = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);

                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);
                yield return null;
            }

            if (goblin.visualObject != null)
                rt.anchoredPosition = targetPos;
        }

        // ============================================================
        // 데미지 / 사망 애니메이션
        // ============================================================

        /// <summary>
        /// <summary>
        /// <summary>
        /// 방패 막기 연출: 방패 Image를 위로 들어올렸다가 복귀.
        /// 방패 Image 없으면 몬스터 전체를 Z축 10도 기울임 후 복귀.
        /// </summary>
        private IEnumerator ShieldBlockEffect(GoblinData goblin)
        {
            if (goblin == null || goblin.visualObject == null) yield break;

            if (goblin.shieldImage != null)
            {
                RectTransform shieldRt = goblin.shieldImage.GetComponent<RectTransform>();
                if (shieldRt == null) yield break;

                // ★ 고정 원위치: GoblinData에 생성 시 1회 저장된 값 사용
                Vector2 fixedHome = goblin.shieldOriginalAnchorPos;
                Vector2 raisedPos = fixedHome + new Vector2(0, 20f);

                // 즉시 원위치 스냅 후 연출 시작 (이전 연출 잔여 위치 초기화)
                shieldRt.anchoredPosition = fixedHome;

                // 위로 올리기 (0.1초)
                float elapsed = 0f;
                while (elapsed < 0.1f)
                {
                    if (shieldRt == null) yield break;
                    elapsed += Time.deltaTime;
                    shieldRt.anchoredPosition = Vector2.Lerp(fixedHome, raisedPos, Mathf.Clamp01(elapsed / 0.1f));
                    yield return null;
                }
                if (shieldRt == null) { goblin.activeShieldBlockCo = null; yield break; }
                shieldRt.anchoredPosition = raisedPos;

                // 유지 (0.2초)
                yield return new WaitForSeconds(0.2f);

                // 복귀 (0.1초)
                elapsed = 0f;
                while (elapsed < 0.1f)
                {
                    if (shieldRt == null) { goblin.activeShieldBlockCo = null; yield break; }
                    elapsed += Time.deltaTime;
                    shieldRt.anchoredPosition = Vector2.Lerp(raisedPos, fixedHome, Mathf.Clamp01(elapsed / 0.1f));
                    yield return null;
                }

                // ★ 강제 스냅: 고정 원위치 확정
                if (shieldRt != null)
                    shieldRt.anchoredPosition = fixedHome;
            }
            else
            {
                // 방패 Image 없으면 몬스터 전체 기울이기
                RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
                if (rt == null) yield break;

                Quaternion originalRot = rt.localRotation;
                Quaternion tilted = originalRot * Quaternion.Euler(0, 0, 10f);

                float elapsed = 0f;
                while (elapsed < 0.1f)
                {
                    if (rt == null) yield break;
                    elapsed += Time.deltaTime;
                    rt.localRotation = Quaternion.Lerp(originalRot, tilted, Mathf.Clamp01(elapsed / 0.1f));
                    yield return null;
                }
                if (rt == null) { goblin.activeShieldBlockCo = null; yield break; }
                rt.localRotation = tilted;

                yield return new WaitForSeconds(0.2f);

                elapsed = 0f;
                while (elapsed < 0.1f)
                {
                    if (rt == null) { goblin.activeShieldBlockCo = null; yield break; }
                    elapsed += Time.deltaTime;
                    rt.localRotation = Quaternion.Lerp(tilted, originalRot, Mathf.Clamp01(elapsed / 0.1f));
                    yield return null;
                }

                if (rt != null)
                    rt.localRotation = originalRot;
            }

            goblin.activeShieldBlockCo = null;
        }

        /// 낙하 충격 연출: 위아래 흔들림 후 원위치 강제 스냅.
        /// 논리 좌표(HexCoord) 변경 없이 anchoredPosition만 임시 이동.
        /// </summary>
        private IEnumerator HitShakeEffect(GoblinData goblin)
        {
            if (goblin == null || goblin.visualObject == null || hexGrid == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            // ★ 헤비급: occupiedCoords 중앙, 일반: position 기준
            Vector2 homePos;
            if (goblin.isHeavy && goblin.occupiedCoords != null && goblin.occupiedCoords.Count > 0)
            {
                Vector2 center = Vector2.zero;
                foreach (var c in goblin.occupiedCoords)
                    center += hexGrid.CalculateFlatTopHexPosition(c);
                homePos = center / goblin.occupiedCoords.Count;
            }
            else
            {
                homePos = hexGrid.CalculateFlatTopHexPosition(goblin.position);
            }

            // 위로 5px
            rt.anchoredPosition = homePos + new Vector2(0, 5f);
            yield return new WaitForSeconds(0.05f);
            if (rt == null) yield break;

            // 아래로 -8px
            rt.anchoredPosition = homePos + new Vector2(0, -8f);
            yield return new WaitForSeconds(0.05f);
            if (rt == null) yield break;

            // 위로 4px
            rt.anchoredPosition = homePos + new Vector2(0, 4f);
            yield return new WaitForSeconds(0.04f);
            if (rt == null) yield break;

            // 아래로 -3px
            rt.anchoredPosition = homePos + new Vector2(0, -3f);
            yield return new WaitForSeconds(0.04f);
            if (rt == null) yield break;

            // ★ 원위치 강제 스냅 (homePos 재계산하여 정확성 보장)
            if (goblin.isHeavy && goblin.occupiedCoords != null && goblin.occupiedCoords.Count > 0)
            {
                Vector2 center = Vector2.zero;
                foreach (var c in goblin.occupiedCoords)
                    center += hexGrid.CalculateFlatTopHexPosition(c);
                rt.anchoredPosition = center / goblin.occupiedCoords.Count;
            }
            else
            {
                rt.anchoredPosition = hexGrid.CalculateFlatTopHexPosition(goblin.position);
            }
        }

        /// 궁수/힐러 낙하 회피 연출: 좌우 랜덤 15px 이동 → 0.1초 후 복귀 (총 0.2초)
        /// 논리 좌표(HexCoord) 변경 없이 GameObject transform만 임시 이동.
        /// </summary>
        private IEnumerator DodgeAnimation(GoblinData goblin)
        {
            if (goblin == null || goblin.visualObject == null) yield break;
            if (hexGrid == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            // 정확한 복귀 위치: 논리 좌표 기반 월드 좌표
            Vector2 homePos = hexGrid.CalculateFlatTopHexPosition(goblin.position);
            // 좌우 랜덤 방향
            float direction = Random.value > 0.5f ? 1f : -1f;
            Vector2 dodgePos = homePos + new Vector2(15f * direction, 0f);

            // 이동 (0.1초)
            float elapsed = 0f;
            while (elapsed < 0.1f)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.1f);
                rt.anchoredPosition = Vector2.Lerp(homePos, dodgePos, t);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = dodgePos;

            // 복귀 (0.1초)
            elapsed = 0f;
            while (elapsed < 0.1f)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.1f);
                rt.anchoredPosition = Vector2.Lerp(dodgePos, homePos, t);
                yield return null;
            }

            // 강제 스냅: 정확한 논리 좌표 기반 중앙 위치로 고정
            if (rt != null)
                rt.anchoredPosition = hexGrid.CalculateFlatTopHexPosition(goblin.position);
        }

        private IEnumerator DamageFlashAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            // HP바는 ApplyDamageAtPosition에서 즉시 업데이트됨 (여기서는 생략)

            // 빨간색 플래시 (피격 표현) — 원래 색으로 복원
            if (goblin.goblinImage != null)
            {
                Color baseColor = GetGoblinBaseColor(goblin);
                float duration = 0.15f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    if (goblin.goblinImage == null) yield break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    if (t < 0.5f)
                        goblin.goblinImage.color = Color.Lerp(baseColor, Color.red, t * 2f);
                    else
                        goblin.goblinImage.color = Color.Lerp(Color.red, baseColor, (t - 0.5f) * 2f);
                    yield return null;
                }

                if (goblin.goblinImage != null)
                    goblin.goblinImage.color = baseColor;
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
        /// 정보 메시지 팝업 (위로 떠오르며 페이드아웃) — 기본 금색
        /// </summary>
        private void SpawnInfoPopup(GoblinData goblin, string message)
        {
            SpawnInfoPopup(goblin, message, new Color(0.9f, 0.7f, 0.2f, 1f));
        }

        /// <summary>
        /// 정보 메시지 팝업 (위로 떠오르며 페이드아웃) — 색상 지정 오버로드
        /// </summary>
        private void SpawnInfoPopup(GoblinData goblin, string message, Color textColor)
        {
            if (goblin.visualObject == null || hexGrid == null) return;

            Transform parent = goblin.visualObject.transform.parent;
            if (parent == null) parent = hexGrid.transform;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject popup = new GameObject("InfoPopup");
            popup.transform.SetParent(parent, false);
            RectTransform rt = popup.AddComponent<RectTransform>();

            RectTransform goblinRt = goblin.visualObject.GetComponent<RectTransform>();
            rt.anchoredPosition = goblinRt.anchoredPosition + new Vector2(0, 30f);
            rt.sizeDelta = new Vector2(200, 40);

            Text text = popup.AddComponent<Text>();
            text.font = font;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            text.raycastTarget = false;
            text.text = message;

            Outline outline = popup.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1, -1);

            StartCoroutine(AnimateInfoPopup(rt, text, outline));
        }

        private System.Collections.IEnumerator AnimateInfoPopup(RectTransform rt, Text text, Outline outline)
        {
            if (rt == null) yield break;
            Vector2 startPos = rt.anchoredPosition;
            float duration = 1.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 위로 50px 떠오름
                float eased = 1f - (1f - t) * (1f - t);
                rt.anchoredPosition = startPos + new Vector2(0, 50f * eased);

                // 후반 40%에서 페이드아웃
                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                if (text != null) text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
                if (outline != null) outline.effectColor = new Color(outline.effectColor.r, outline.effectColor.g, outline.effectColor.b, alpha * 0.9f);

                yield return null;
            }

            if (rt != null) Destroy(rt.gameObject);
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
            // ★ 마녀 사망 시 소속 언데드 즉시 제거 (애니메이션 전에 바로 실행)
            if (goblin.isWitch)
            {
                StartCoroutine(RemoveAllUndeadOfWitch(goblin.witchOwnerIndex));
            }

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
        private static Sprite CreateGoblinSprite(int size,
            Color? bodyOverride = null, Color? darkOverride = null, Color? lightOverride = null, Color? earOverride = null)
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
            Color bodyColor = bodyOverride ?? new Color(0.3f, 0.65f, 0.2f);
            Color bodyDark = darkOverride ?? new Color(0.2f, 0.5f, 0.12f);
            Color bodyLight = lightOverride ?? new Color(0.4f, 0.78f, 0.3f);

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
            Color earColorVal = earOverride ?? new Color(0.25f, 0.55f, 0.18f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColorVal);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColorVal);

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
        private static Sprite CreateArcherGoblinSprite(int size,
            Color? bodyOverride = null, Color? darkOverride = null, Color? lightOverride = null, Color? earOverride = null)
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
            Color bodyColor = bodyOverride ?? new Color(0.45f, 0.25f, 0.65f);
            Color bodyDark = darkOverride ?? new Color(0.3f, 0.15f, 0.5f);
            Color bodyLight = lightOverride ?? new Color(0.6f, 0.35f, 0.8f);

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
            Color earColorVal = earOverride ?? new Color(0.38f, 0.2f, 0.55f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColorVal);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColorVal);

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
        private static Sprite CreateArmoredGoblinSprite(int size,
            Color? bodyOverride = null, Color? darkOverride = null, Color? lightOverride = null, Color? earOverride = null)
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
            Color bodyColor = bodyOverride ?? new Color(0.25f, 0.5f, 0.2f);
            Color bodyDark = darkOverride ?? new Color(0.15f, 0.38f, 0.1f);
            Color bodyLight = lightOverride ?? new Color(0.35f, 0.62f, 0.28f);

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
            Color earColorVal = earOverride ?? new Color(0.2f, 0.42f, 0.15f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 12, cy + bodyRadius + 22,
                         cx - bodyRadius + 18, cy + bodyRadius + 5, earColorVal);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 12, cy + bodyRadius + 22,
                         cx + bodyRadius - 18, cy + bodyRadius + 5, earColorVal);

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
        private static Sprite CreateShieldGoblinSprite(int size,
            Color? bodyOverride = null, Color? darkOverride = null, Color? lightOverride = null, Color? earOverride = null)
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
            Color bodyColor = bodyOverride ?? new Color(0.2f, 0.5f, 0.5f);
            Color bodyDark = darkOverride ?? new Color(0.12f, 0.35f, 0.38f);
            Color bodyLight = lightOverride ?? new Color(0.3f, 0.65f, 0.6f);

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
            Color earColorVal = earOverride ?? new Color(0.15f, 0.4f, 0.42f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColorVal);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColorVal);

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
        /// 폭탄 고블린 스프라이트 — 빨간색+검은색 계열, 폭탄 장식
        /// </summary>
        private static Sprite CreateBombGoblinSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // === 몸체 (진한 빨간-갈색 계열) ===
            int bodyRadius = (int)(size * 0.35f);
            Color bodyColor = new Color(0.55f, 0.15f, 0.1f);
            Color bodyDark = new Color(0.35f, 0.08f, 0.05f);
            Color bodyLight = new Color(0.7f, 0.25f, 0.15f);

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
            Color earColor = new Color(0.4f, 0.1f, 0.05f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColor);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColor);

            // === 눈 (흰색 + 빨간 눈동자 — 광기 표현) ===
            int eyeRadius = (int)(size * 0.07f);
            int eyeOffsetX = (int)(size * 0.12f);
            int eyeOffsetY = (int)(size * 0.06f);
            DrawFilledCircle(tex, cx - eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx - eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.9f, 0.15f, 0.1f));
            DrawFilledCircle(tex, cx + eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx + eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.9f, 0.15f, 0.1f));

            // === 입 (사악한 미소) ===
            Color mouthColor = new Color(0.2f, 0.05f, 0.02f);
            for (int i = -12; i <= 12; i++)
            {
                int mx = cx + i;
                int my = cy - (int)(size * 0.08f) + Mathf.Abs(i) / 4;
                if (mx >= 0 && mx < size && my >= 0 && my < size) tex.SetPixel(mx, my, mouthColor);
                if (mx >= 0 && mx < size && my - 1 >= 0) tex.SetPixel(mx, my - 1, mouthColor);
            }

            // === 이빨 ===
            Color toothColor = new Color(0.95f, 0.95f, 0.85f);
            int toothY = cy - (int)(size * 0.08f) - 2;
            DrawTriangle(tex, cx - 5, toothY, cx - 2, toothY - 7, cx + 1, toothY, toothColor);
            DrawTriangle(tex, cx + 3, toothY, cx + 6, toothY - 7, cx + 9, toothY, toothColor);

            // === 폭탄 (왼손 위에 둥근 검은 폭탄) ===
            int bombCx = cx - bodyRadius + 5;
            int bombCy = cy + bodyRadius + 15;
            int bombRadius = (int)(size * 0.12f);
            DrawFilledCircle(tex, bombCx, bombCy, bombRadius, new Color(0.15f, 0.12f, 0.1f));
            DrawFilledCircle(tex, bombCx - 2, bombCy + 2, bombRadius - 3, new Color(0.25f, 0.2f, 0.18f));
            // 도화선 (위로 향하는 짧은 선)
            Color fuseColor = new Color(0.8f, 0.6f, 0.2f);
            for (int j = 0; j < 12; j++)
            {
                int fx = bombCx + j / 4;
                int fy = bombCy + bombRadius + j;
                if (fx >= 0 && fx < size && fy >= 0 && fy < size) tex.SetPixel(fx, fy, fuseColor);
                if (fx + 1 < size && fy >= 0 && fy < size) tex.SetPixel(fx + 1, fy, fuseColor);
            }
            // 불꽃 (도화선 끝)
            Color sparkColor = new Color(1f, 0.7f, 0.2f);
            int sparkY = bombCy + bombRadius + 12;
            DrawFilledCircle(tex, bombCx + 3, sparkY, 4, sparkColor);
            DrawFilledCircle(tex, bombCx + 3, sparkY, 2, new Color(1f, 1f, 0.5f));

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
                // Heavy 고블린: occupiedCoords의 모든 열에 타겟 등록
                if (goblin.isHeavy && goblin.occupiedCoords != null)
                {
                    foreach (var coord in goblin.occupiedCoords)
                    {
                        int q = coord.q;
                        float yPos = grid.CalculateFlatTopHexPosition(coord).y;
                        if (!result.ContainsKey(q))
                            result[q] = new List<(GoblinData, float)>();
                        // 같은 고블린이 같은 열에 중복 등록되지 않도록
                        if (!result[q].Any(t => t.goblin == goblin))
                            result[q].Add((goblin, yPos));
                    }
                }
                else
                {
                    int q = goblin.position.q;
                    float yPos = grid.CalculateFlatTopHexPosition(goblin.position).y;

                    if (!result.ContainsKey(q))
                        result[q] = new List<(GoblinData, float)>();
                    result[q].Add((goblin, yPos));
                }
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

            // 궁수/힐러/마법사/마녀/은신도둑 고블린은 낙하 데미지 면역 → 회피 연출 + "회피" 팝업
            if (goblin.IsImmuneToFallDamage)
            {
                Debug.Log($"[회피조건] 진입 — archer={goblin.isArcher} healer={goblin.isHealer} wizard={goblin.isWizard} witch={goblin.isWitch} thief+stealth={goblin.isThief && goblin.isStealth} 위치={goblin.position} visualObject={goblin.visualObject != null}");
                if (goblin.visualObject != null)
                {
                    StartCoroutine(DodgeAnimation(goblin));
                    SpawnInfoPopup(goblin, "회피", Color.white);
                }
                return;
            }

            // 방패 활성 시 낙하 데미지 면역 → "방어" 팝업 + 방패 들기 연출
            if (goblin.isShielded)
            {
                SpawnInfoPopup(goblin, "방어", Color.white);
                // 기존 연출 중단 후 새 연출 시작 (위치 드리프트 방지)
                if (goblin.activeShieldBlockCo != null) StopCoroutine(goblin.activeShieldBlockCo);
                goblin.activeShieldBlockCo = StartCoroutine(ShieldBlockEffect(goblin));
                return;
            }

            goblin.hp -= 1;
            Debug.Log($"[GoblinSystem] 낙하 충돌 대미지: ({goblin.position}) HP {goblin.hp + 1} → {goblin.hp} (-1)");

            UpdateHPBar(goblin);

            // 데미지 팝업 + 충격 연출 (동시 실행)
            if (goblin.visualObject != null)
            {
                SpawnDamagePopups(goblin, 1);
                StartCoroutine(HitShakeEffect(goblin));
            }

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
                OnGoblinKilled?.Invoke(totalKills, goblin.isArmored, goblin.isArcher, goblin.isShieldType, goblin.isBomb, goblin.isHealer, goblin.isHeavy, goblin.isWizard, goblin.isThief, goblin.isWitch, goblin.monsterLevel);
                if (goblin.visualObject != null)
                    deathCoroutines.Add(StartCoroutine(DeathAnimation(goblin)));
            }

            // 모든 사망 애니메이션 완료 대기
            foreach (var c in deathCoroutines)
                yield return c;

            // 낙하 사망한 언데드가 있으면 마녀 언데드 전멸 체크
            var deadUndead = pendingFallDeaths.Where(g => g.isUndead).ToList();
            pendingFallDeaths.Clear();
            goblins.RemoveAll(g => !g.isAlive && g.visualObject == null);

            // 낙하 사망 후 마녀별 언데드 전멸 체크 (여러 마녀 한번에 처리)
            if (deadUndead.Count > 0)
            {
                var checkedOwners = new HashSet<int>();
                foreach (var undead in deadUndead)
                {
                    if (!checkedOwners.Add(undead.undeadOwnerIndex)) continue;
                    var witch = goblins.FirstOrDefault(g => g.isAlive && g.isWitch && g.witchOwnerIndex == undead.undeadOwnerIndex);
                    if (witch == null) continue;
                    int aliveCount = goblins.Count(g => g.isAlive && g.isUndead && g.undeadOwnerIndex == undead.undeadOwnerIndex);
                    if (aliveCount == 0)
                    {
                        Debug.Log($"[마녀] 낙하 사망으로 언데드 전멸 (ownerIdx={undead.undeadOwnerIndex})");
                        yield return StartCoroutine(WitchUndeadWipeout(witch));
                    }
                }
            }
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
        /// Lv2 기본 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetGoblinLv2Sprite()
        {
            if (goblinLv2Sprite == null)
                goblinLv2Sprite = CreateGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
            return goblinLv2Sprite;
        }

        /// <summary>
        /// Lv2 궁수 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetArcherLv2Sprite()
        {
            if (archerLv2Sprite == null)
                archerLv2Sprite = CreateArcherGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
            return archerLv2Sprite;
        }

        /// <summary>
        /// Lv2 갑옷 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetArmoredLv2Sprite()
        {
            if (armoredLv2Sprite == null)
                armoredLv2Sprite = CreateArmoredGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
            return armoredLv2Sprite;
        }

        /// <summary>
        /// Lv2 방패 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetShieldLv2Sprite()
        {
            if (shieldLv2Sprite == null)
                shieldLv2Sprite = CreateShieldGoblinSprite(256, Lv2BodyColor, Lv2BodyDark, Lv2BodyLight, Lv2EarColor);
            return shieldLv2Sprite;
        }

        /// <summary>
        /// 폭탄 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetBombGoblinSprite()
        {
            if (bombGoblinSprite == null)
                bombGoblinSprite = CreateBombGoblinSprite(256);
            return bombGoblinSprite;
        }

        /// <summary>
        /// 헤비급 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetHeavyGoblinSprite()
        {
            if (heavyGoblinSprite == null)
                heavyGoblinSprite = CreateHeavyGoblinSprite(256);
            return heavyGoblinSprite;
        }

        /// <summary>
        /// 마법사 고블린 스프라이트 외부 접근용 (에디터/미션 아이콘 등)
        /// </summary>
        public static Sprite GetWizardGoblinSprite()
        {
            if (wizardGoblinSprite == null)
                wizardGoblinSprite = CreateWizardGoblinSprite(256);
            return wizardGoblinSprite;
        }

        /// <summary>
        /// 마법사 고블린 프로시저럴 스프라이트 생성:
        /// 보라색 원형 몸통 + 삼각형 모자 + 모자 챙 + 지팡이(세로 막대+노란 별) + 흰색 눈 2개
        /// </summary>
        private static Sprite CreateWizardGoblinSprite(int texSize)
        {
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[texSize * texSize];
            float center = texSize / 2f;
            float radius = texSize * 0.30f;

            // 배경 투명
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color bodyColor = new Color(0.55f, 0.25f, 0.75f, 1f);       // 보라색 몸통
            Color hatColor = new Color(0.35f, 0.10f, 0.55f, 1f);        // 진한 보라 모자
            Color brimColor = new Color(0.30f, 0.08f, 0.50f, 1f);       // 모자 챙
            Color staffColor = new Color(0.45f, 0.30f, 0.20f, 1f);      // 지팡이 갈색
            Color starColor = new Color(1f, 0.9f, 0.2f, 1f);            // 지팡이 별 노란색
            Color eyeColor = Color.white;

            float bodyY = center + texSize * 0.05f; // 몸통 약간 아래

            // 1. 몸통 (보라색 원)
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - bodyY;
                    if (dx * dx + dy * dy <= radius * radius)
                        pixels[y * texSize + x] = bodyColor;
                }
            }

            // 2. 삼각형 모자 (몸통 위)
            float hatBase = center - radius * 0.6f;
            float hatTop = center - radius * 1.8f;
            float hatWidth = radius * 0.65f;
            for (int y = (int)hatTop; y < (int)(bodyY - radius * 0.7f); y++)
            {
                if (y < 0 || y >= texSize) continue;
                float t = (y - hatTop) / ((bodyY - radius * 0.7f) - hatTop);
                float halfW = hatWidth * t;
                for (int x = (int)(center - halfW); x <= (int)(center + halfW); x++)
                {
                    if (x >= 0 && x < texSize)
                        pixels[y * texSize + x] = hatColor;
                }
            }

            // 3. 모자 챙 (가로 직사각형)
            float brimY = bodyY - radius * 0.75f;
            float brimHalfW = radius * 0.9f;
            float brimH = texSize * 0.04f;
            for (int y = (int)brimY; y < (int)(brimY + brimH); y++)
            {
                if (y < 0 || y >= texSize) continue;
                for (int x = (int)(center - brimHalfW); x <= (int)(center + brimHalfW); x++)
                {
                    if (x >= 0 && x < texSize)
                        pixels[y * texSize + x] = brimColor;
                }
            }

            // 4. 지팡이 (오른쪽 세로 막대)
            float staffX = center + radius * 1.1f;
            float staffTop = center - radius * 1.2f;
            float staffBottom = bodyY + radius * 0.8f;
            float staffW = texSize * 0.025f;
            for (int y = (int)staffTop; y < (int)staffBottom; y++)
            {
                if (y < 0 || y >= texSize) continue;
                for (int x = (int)(staffX - staffW); x <= (int)(staffX + staffW); x++)
                {
                    if (x >= 0 && x < texSize)
                        pixels[y * texSize + x] = staffColor;
                }
            }

            // 5. 지팡이 상단 별 (노란 원)
            float starRadius = texSize * 0.055f;
            float starCenterY = staffTop - starRadius * 0.3f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - staffX;
                    float dy = y - starCenterY;
                    if (dx * dx + dy * dy <= starRadius * starRadius)
                        pixels[y * texSize + x] = starColor;
                }
            }

            // 6. 눈 (흰색 원 2개)
            float eyeRadius = texSize * 0.035f;
            float eyeY = bodyY - radius * 0.15f;
            float eyeSpacing = radius * 0.35f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    // 왼쪽 눈
                    float dlx = x - (center - eyeSpacing);
                    float dly = y - eyeY;
                    if (dlx * dlx + dly * dly <= eyeRadius * eyeRadius)
                        pixels[y * texSize + x] = eyeColor;
                    // 오른쪽 눈
                    float drx = x - (center + eyeSpacing);
                    if (drx * drx + dly * dly <= eyeRadius * eyeRadius)
                        pixels[y * texSize + x] = eyeColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 도둑 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        // ============================================================
        // 마녀 고블린 시스템 (소환 영역 전용, 언데드 소환)
        // ============================================================

        /// <summary>
        /// 마녀 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetWitchGoblinSprite()
        {
            if (witchGoblinSprite == null)
                witchGoblinSprite = CreateWitchGoblinSprite(256);
            return witchGoblinSprite;
        }

        /// <summary>
        /// 마녀 고블린 프로시저럴 스프라이트 생성:
        /// 검은 보라색 몸통 + 뾰족한 마녀 모자 + 긴 빗자루 + 녹색 눈 2개 + 입
        /// </summary>
        private static Sprite CreateWitchGoblinSprite(int texSize)
        {
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[texSize * texSize];
            float center = texSize / 2f;
            float radius = texSize * 0.28f;

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color bodyColor = new Color(0.35f, 0.15f, 0.45f, 1f);       // 진한 보라 몸통
            Color hatColor = new Color(0.15f, 0.05f, 0.25f, 1f);        // 어두운 보라 모자
            Color hatBandColor = new Color(0.6f, 0.2f, 0.1f, 1f);       // 모자 밴드 (갈색-빨강)
            Color skinColor = new Color(0.55f, 0.75f, 0.45f, 1f);       // 연한 녹색 피부
            Color eyeColor = new Color(0.2f, 1f, 0.3f, 1f);             // 밝은 녹색 눈
            Color pupilColor = new Color(0.05f, 0.1f, 0.05f, 1f);       // 검은 동공
            Color broomColor = new Color(0.5f, 0.35f, 0.2f, 1f);        // 빗자루 갈색

            float bodyY = center + texSize * 0.08f;

            // 1. 몸통 (보라색 원)
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - bodyY;
                    if (dx * dx + dy * dy <= radius * radius)
                        pixels[y * texSize + x] = bodyColor;
                }
            }

            // 2. 얼굴 (피부색 타원 — 몸통 중앙)
            float faceRadX = radius * 0.6f;
            float faceRadY = radius * 0.5f;
            float faceY = bodyY - radius * 0.05f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = (x - center) / faceRadX;
                    float dy = (y - faceY) / faceRadY;
                    if (dx * dx + dy * dy <= 1f)
                        pixels[y * texSize + x] = skinColor;
                }
            }

            // 3. 뾰족한 마녀 모자 (길쭉한 삼각형)
            float hatTop = center - radius * 2.2f;
            float hatBaseY = bodyY - radius * 0.6f;
            float hatWidth = radius * 0.7f;
            for (int y = (int)hatTop; y < (int)hatBaseY; y++)
            {
                if (y < 0 || y >= texSize) continue;
                float t = (y - hatTop) / (hatBaseY - hatTop);
                float halfW = hatWidth * t;
                // 모자 살짝 기울임 (오른쪽으로)
                float offset = (1f - t) * radius * 0.3f;
                for (int x = (int)(center - halfW + offset); x <= (int)(center + halfW + offset); x++)
                {
                    if (x >= 0 && x < texSize)
                        pixels[y * texSize + x] = hatColor;
                }
            }

            // 4. 모자 챙 (넓은 가로 타원)
            float brimY = hatBaseY;
            float brimHalfW = radius * 1.0f;
            float brimH = texSize * 0.045f;
            for (int y = (int)(brimY - brimH); y < (int)(brimY + brimH); y++)
            {
                if (y < 0 || y >= texSize) continue;
                float dy = (y - brimY) / brimH;
                float w = brimHalfW * Mathf.Sqrt(1f - dy * dy);
                for (int x = (int)(center - w); x <= (int)(center + w); x++)
                {
                    if (x >= 0 && x < texSize)
                        pixels[y * texSize + x] = hatColor;
                }
            }

            // 5. 모자 밴드
            float bandY = brimY + brimH * 0.3f;
            float bandH = texSize * 0.02f;
            for (int y = (int)(bandY - bandH); y <= (int)(bandY + bandH); y++)
            {
                if (y < 0 || y >= texSize) continue;
                for (int x = (int)(center - hatWidth * 0.55f); x <= (int)(center + hatWidth * 0.55f); x++)
                {
                    if (x >= 0 && x < texSize)
                        pixels[y * texSize + x] = hatBandColor;
                }
            }

            // 6. 눈 (녹색 원 2개 + 동공)
            float eyeRadius = texSize * 0.04f;
            float eyeY = faceY - radius * 0.08f;
            float eyeSpacing = radius * 0.3f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dlx = x - (center - eyeSpacing);
                    float dly = y - eyeY;
                    float drx = x - (center + eyeSpacing);
                    // 왼쪽 눈
                    if (dlx * dlx + dly * dly <= eyeRadius * eyeRadius)
                        pixels[y * texSize + x] = eyeColor;
                    // 오른쪽 눈
                    if (drx * drx + dly * dly <= eyeRadius * eyeRadius)
                        pixels[y * texSize + x] = eyeColor;
                    // 동공
                    float pupilR = eyeRadius * 0.45f;
                    if (dlx * dlx + dly * dly <= pupilR * pupilR)
                        pixels[y * texSize + x] = pupilColor;
                    if (drx * drx + dly * dly <= pupilR * pupilR)
                        pixels[y * texSize + x] = pupilColor;
                }
            }

            // 7. 입 (작은 호)
            float mouthY = faceY + radius * 0.2f;
            float mouthW = radius * 0.25f;
            for (int x = (int)(center - mouthW); x <= (int)(center + mouthW); x++)
            {
                if (x < 0 || x >= texSize) continue;
                float t = (x - (center - mouthW)) / (mouthW * 2f);
                int my = (int)(mouthY + Mathf.Sin(t * Mathf.PI) * radius * 0.08f);
                if (my >= 0 && my < texSize)
                    pixels[my * texSize + x] = new Color(0.3f, 0.1f, 0.15f, 1f);
            }

            // 8. 빗자루 (왼쪽 대각선)
            float broomStartX = center - radius * 1.2f;
            float broomStartY = bodyY + radius * 0.2f;
            float broomEndX = center - radius * 0.3f;
            float broomEndY = bodyY - radius * 0.3f;
            float broomLen = Mathf.Sqrt((broomEndX - broomStartX) * (broomEndX - broomStartX) +
                                        (broomEndY - broomStartY) * (broomEndY - broomStartY));
            for (float step = 0; step < broomLen; step += 0.5f)
            {
                float t2 = step / broomLen;
                int bx = (int)Mathf.Lerp(broomStartX, broomEndX, t2);
                int by = (int)Mathf.Lerp(broomStartY, broomEndY, t2);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (bx + dx >= 0 && bx + dx < texSize && by + dy >= 0 && by + dy < texSize)
                            pixels[(by + dy) * texSize + (bx + dx)] = broomColor;
            }
            // 빗자루 끝 (빗살)
            for (int i = -3; i <= 3; i++)
            {
                float bsX = broomStartX + i * 2;
                float bsY = broomStartY + Mathf.Abs(i) * 1.5f;
                for (int dy = 0; dy < (int)(texSize * 0.06f); dy++)
                {
                    int px = (int)(bsX + dy * 0.3f * Mathf.Sign(i));
                    int py = (int)(bsY + dy);
                    if (px >= 0 && px < texSize && py >= 0 && py < texSize)
                        pixels[py * texSize + px] = broomColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 언데드 고블린 프로시저럴 스프라이트 생성:
        /// 연한 녹색 원형 몸통 + 갈라진 눈 + 해골 입 패턴
        /// </summary>
        private static Sprite CreateUndeadGoblinSprite(int texSize)
        {
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[texSize * texSize];
            float center = texSize / 2f;
            float radius = texSize * 0.30f;

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color bodyColor = new Color(0.45f, 0.55f, 0.35f, 1f);     // 칙칙한 녹색
            Color boneColor = new Color(0.85f, 0.85f, 0.75f, 1f);     // 뼈 색상
            Color eyeColor = new Color(0.9f, 0.2f, 0.1f, 1f);         // 빨간 눈
            Color darkColor = new Color(0.15f, 0.2f, 0.1f, 1f);       // 어두운 녹색

            float bodyY = center + texSize * 0.05f;

            // 1. 몸통 (녹색 원 + 불규칙 가장자리)
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - bodyY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float edgeNoise = Mathf.Sin(angle * 7f) * radius * 0.06f;
                    if (dist <= radius + edgeNoise)
                        pixels[y * texSize + x] = bodyColor;
                }
            }

            // 2. 눈 (빨간 빛나는 원 2개)
            float eyeRadius = texSize * 0.04f;
            float eyeY = bodyY - radius * 0.2f;
            float eyeSpacing = radius * 0.35f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dlx = x - (center - eyeSpacing);
                    float dly = y - eyeY;
                    float drx = x - (center + eyeSpacing);
                    if (dlx * dlx + dly * dly <= eyeRadius * eyeRadius)
                        pixels[y * texSize + x] = eyeColor;
                    if (drx * drx + dly * dly <= eyeRadius * eyeRadius)
                        pixels[y * texSize + x] = eyeColor;
                }
            }

            // 3. 해골 입 (뼈 색 사각형 + 세로줄)
            float mouthY = bodyY + radius * 0.15f;
            float mouthW = radius * 0.4f;
            float mouthH = radius * 0.2f;
            for (int y = (int)(mouthY - mouthH); y <= (int)(mouthY + mouthH); y++)
            {
                if (y < 0 || y >= texSize) continue;
                for (int x = (int)(center - mouthW); x <= (int)(center + mouthW); x++)
                {
                    if (x < 0 || x >= texSize) continue;
                    pixels[y * texSize + x] = boneColor;
                    // 세로 줄 (이빨 간격)
                    int relX = x - (int)(center - mouthW);
                    if (relX % 6 < 2)
                        pixels[y * texSize + x] = darkColor;
                }
            }

            // 4. 크랙 무늬 (몸통에 갈라진 선)
            for (int i = 0; i < 3; i++)
            {
                float crackX = center + (i - 1) * radius * 0.4f;
                float crackStartY = bodyY - radius * 0.5f;
                float crackEndY = bodyY + radius * 0.3f;
                for (float cy = crackStartY; cy < crackEndY; cy += 1f)
                {
                    float wobble = Mathf.Sin(cy * 0.3f) * 2f;
                    int px = (int)(crackX + wobble);
                    int py = (int)cy;
                    if (px >= 0 && px < texSize && py >= 0 && py < texSize)
                    {
                        float dx = px - center;
                        float dy = py - bodyY;
                        if (dx * dx + dy * dy <= radius * radius * 0.85f)
                            pixels[py * texSize + px] = darkColor;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), Vector2.one * 0.5f, 100f);
        }

        /// <summary>
        /// 마녀 고블린 턴 페이즈: 소환 + 이동
        /// 등장 즉시 언데드 3마리 소환, 이후 6턴마다 재소환
        /// 2턴마다 소환 영역 내 이동
        /// </summary>
        private IEnumerator WitchGoblinPhase()
        {
            var aliveWitches = goblins.Where(g => g.isAlive && g.isWitch).ToList();
            if (aliveWitches.Count == 0) yield break;

            Debug.Log($"[마녀턴] 행동 시작 — {aliveWitches.Count}마리");

            foreach (var witch in aliveWitches)
            {
                if (!witch.isAlive || witch.visualObject == null) continue;

                witch.witchSummonCounter++;
                witch.witchMoveCounter++;

                // 소환 체크: 6턴마다 (첫 소환은 SpawnWaveBatch 직후 별도 처리)
                if (witch.witchSummonCounter >= 6)
                {
                    witch.witchSummonCounter = 0;
                    yield return StartCoroutine(WitchSummonUndead(witch));
                }

                // 이동 체크: 2턴마다
                if (witch.witchMoveCounter >= 2)
                {
                    witch.witchMoveCounter = 0;
                    var occupiedPositions = new HashSet<HexCoord>(goblins.Where(g => g.isAlive).Select(g => g.position));
                    foreach (var g in goblins.Where(g => g.isAlive && g.isHeavy && g.occupiedCoords != null))
                        foreach (var c in g.occupiedCoords) occupiedPositions.Add(c);

                    HexCoord moveTarget = FindWizardMoveTarget(witch.position, occupiedPositions);
                    if (!moveTarget.Equals(witch.position))
                    {
                        witch.position = moveTarget;
                        Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(moveTarget);
                        yield return StartCoroutine(AnimateGoblinMove(witch, worldPos));
                    }
                }
            }
        }

        /// <summary>
        /// 마녀가 언데드 3마리를 블록 필드에 소환.
        /// 가장자리 제외한 블록 중 하나를 랜덤 지정 → 해당 + 인접 6칸(최대 7) 중 3곳에 소환.
        /// </summary>
        private IEnumerator WitchSummonUndead(GoblinData witch)
        {
            if (hexGrid == null || !witch.isAlive) yield break;

            // 몽환적 소환 파티클 + 시전 모션 (재소환에서 호출 시 이미 실행되므로 중복 방지)
            if (witch.visualObject != null)
            {
                StartCoroutine(WitchSummonParticles(witch));
                yield return StartCoroutine(WitchCastPulse(witch));
            }

            // 가장자리 제외한 블록 좌표 수집
            int gridRadius = hexGrid.GridRadius;
            var innerCoords = new List<HexCoord>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                HexCoord c = block.Coord;
                // 가장자리 판단: |q|+|r|+|s| 중 최대값이 gridRadius와 같으면 가장자리
                int s = -c.q - c.r;
                if (Mathf.Max(Mathf.Abs(c.q), Mathf.Max(Mathf.Abs(c.r), Mathf.Abs(s))) < gridRadius)
                    innerCoords.Add(c);
            }
            if (innerCoords.Count == 0) yield break;

            // 중심 좌표 랜덤 선택
            HexCoord centerCoord = innerCoords[Random.Range(0, innerCoords.Count)];

            // 중심 + 인접 6칸 후보
            var candidates = new List<HexCoord> { centerCoord };
            foreach (var dir in HexCoord.Directions)
            {
                HexCoord neighbor = new HexCoord(centerCoord.q + dir.q, centerCoord.r + dir.r);
                if (hexGrid.IsInsideGrid(neighbor))
                    candidates.Add(neighbor);
            }

            // 고블린이 없는 위치만 필터 + 셔플
            var occupiedPositions = new HashSet<HexCoord>(goblins.Where(g => g.isAlive).Select(g => g.position));
            candidates = candidates.Where(c => !occupiedPositions.Contains(c)).ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temp;
            }

            int summonCount = Mathf.Min(3, candidates.Count);
            Debug.Log($"[마녀턴] 언데드 소환: 마녀({witch.position}) ownerIdx={witch.witchOwnerIndex}, 중심=({centerCoord}), 후보={candidates.Count}, 소환={summonCount}");

            for (int i = 0; i < summonCount; i++)
            {
                HexCoord spawnPos = candidates[i];
                int hp = currentConfig != null ? currentConfig.undeadHp : 3;

                GoblinData undead = new GoblinData
                {
                    position = spawnPos,
                    hp = hp,
                    maxHp = hp,
                    displayedHp = hp,
                    isAlive = true,
                    isUndead = true,
                    undeadOwnerIndex = witch.witchOwnerIndex,
                    justSummoned = true  // 소환된 턴에는 이동/공격 안 함
                };

                goblins.Add(undead);
                // 언데드는 totalSpawned에 포함하지 않음 (미션 소환 카운터 제외)

                Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(spawnPos);

                // 몽환적 소환 이펙트 (백그라운드) + 0.3초 대기 후 비주얼 생성
                Transform effectParent = witch.visualObject != null ? witch.visualObject.transform.parent : null;
                StartCoroutine(UndeadSpawnParticles(worldPos, effectParent));
                yield return new WaitForSeconds(0.3f);

                CreateGoblinVisual(undead, worldPos);

                // 등장 스케일 애니메이션: 0에서 팽창 → 수축 (몽환적 등장감)
                if (undead.visualObject != null)
                {
                    RectTransform rt = undead.visualObject.GetComponent<RectTransform>();
                    rt.localScale = Vector3.zero;
                    float elapsed = 0f, dur = 0.35f;
                    while (elapsed < dur)
                    {
                        if (rt == null) break;
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / dur);
                        // 탄성 바운스: 오버슈트 후 정착
                        float scale = t < 0.6f
                            ? t / 0.6f * 1.2f
                            : 1.2f - (t - 0.6f) / 0.4f * 0.2f;
                        rt.localScale = Vector3.one * scale;
                        yield return null;
                    }
                    if (rt != null) rt.localScale = Vector3.one;
                }

                Debug.Log($"[마녀턴] 언데드 소환 완료: ({spawnPos}), HP={hp}, owner={witch.witchOwnerIndex}");
            }
        }

        /// <summary>
        /// 마녀 시전 모션: 보라색 글로우 펄스
        /// </summary>
        private IEnumerator WitchCastPulse(GoblinData witch)
        {
            if (witch.visualObject == null) yield break;
            RectTransform rt = witch.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Color witchBaseColor = GetGoblinBaseColor(witch);
            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = Vector3.one * scale;
                if (witch.goblinImage != null)
                    witch.goblinImage.color = Color.Lerp(witchBaseColor, new Color(0.7f, 0.3f, 1f, 1f), Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
            if (witch.goblinImage != null) witch.goblinImage.color = GetGoblinBaseColor(witch);
        }

        /// <summary>
        /// 모든 몬스터 행동 후 대기 중인 마녀 재소환 처리.
        /// 캐스케이드/매칭 중 언데드 전멸 시 대기열에 등록된 마녀들의 재소환 실행.
        /// </summary>
        private IEnumerator ProcessPendingWitchResummon()
        {
            if (pendingWitchResummon.Count == 0) yield break;

            var ownerIndices = new List<int>(pendingWitchResummon);
            pendingWitchResummon.Clear();

            foreach (int ownerIdx in ownerIndices)
            {
                var witch = goblins.FirstOrDefault(g => g.isAlive && g.isWitch && g.witchOwnerIndex == ownerIdx);
                if (witch == null || witch.visualObject == null) continue;

                Debug.Log($"[마녀] 대기 재소환 실행: ownerIdx={ownerIdx}, 마녀({witch.position})");

                // 언데드 3마리 소환 (파티클 + 시전 모션은 WitchSummonUndead 내부에서 실행)
                yield return StartCoroutine(WitchSummonUndead(witch));
            }
        }

        /// <summary>
        /// 마녀 소환 시 몽환적 파티클 이펙트.
        /// 보라/분홍/시안 빛 입자가 마녀 주변에서 원형으로 퍼지며 천천히 사라짐.
        /// </summary>
        private IEnumerator WitchSummonParticles(GoblinData witch)
        {
            if (witch.visualObject == null) yield break;

            RectTransform witchRt = witch.visualObject.GetComponent<RectTransform>();
            if (witchRt == null) yield break;

            Transform parent = witchRt.parent;
            Vector2 center = witchRt.anchoredPosition;

            int particleCount = 18;
            var particles = new List<GameObject>();
            var particleData = new List<(RectTransform rt, Vector2 dir, float speed, float life, float maxLife, Color color)>();

            Color[] mysticColors = new Color[]
            {
                new Color(0.7f, 0.3f, 1f, 0.9f),   // 보라
                new Color(1f, 0.4f, 0.8f, 0.9f),    // 분홍
                new Color(0.4f, 0.8f, 1f, 0.9f),    // 시안
                new Color(0.9f, 0.6f, 1f, 0.9f),    // 연보라
                new Color(0.5f, 1f, 0.9f, 0.9f),    // 민트
            };

            for (int i = 0; i < particleCount; i++)
            {
                GameObject p = new GameObject($"WitchSummonParticle_{i}");
                p.transform.SetParent(parent, false);
                var img = p.AddComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;

                // 원형 스프라이트 (프로시저럴)
                int texSize = 16;
                Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
                float halfSize = texSize * 0.5f;
                for (int y = 0; y < texSize; y++)
                    for (int x = 0; x < texSize; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(halfSize, halfSize)) / halfSize;
                        float alpha = Mathf.Clamp01(1f - dist * dist); // 부드러운 원형 감쇠
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                tex.Apply();
                img.sprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));

                Color c = mysticColors[Random.Range(0, mysticColors.Length)];
                img.color = c;

                var rt = p.GetComponent<RectTransform>();
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float startRadius = Random.Range(8f, 20f);
                rt.anchoredPosition = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * startRadius;
                float size = Random.Range(6f, 14f);
                rt.sizeDelta = new Vector2(size, size);

                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = Random.Range(40f, 90f);
                float life = Random.Range(0.6f, 1.2f);

                particles.Add(p);
                particleData.Add((rt, dir, speed, 0f, life, c));
            }

            // 큰 글로우 레이어 (중앙)
            GameObject glowObj = new GameObject("WitchSummonGlow");
            glowObj.transform.SetParent(parent, false);
            var glowImg = glowObj.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(0.7f, 0.3f, 1f, 0f);
            var glowRt = glowObj.GetComponent<RectTransform>();
            glowRt.anchoredPosition = center;
            glowRt.sizeDelta = new Vector2(120f, 120f);

            // 원형 글로우 텍스처
            {
                int gSize = 32;
                Texture2D gTex = new Texture2D(gSize, gSize, TextureFormat.RGBA32, false);
                float gHalf = gSize * 0.5f;
                for (int y = 0; y < gSize; y++)
                    for (int x = 0; x < gSize; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(gHalf, gHalf)) / gHalf;
                        float alpha = Mathf.Clamp01(1f - dist);
                        alpha *= alpha; // 부드럽게 감쇠
                        gTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                gTex.Apply();
                glowImg.sprite = Sprite.Create(gTex, new Rect(0, 0, gSize, gSize), new Vector2(0.5f, 0.5f));
            }

            float totalDuration = 1.2f;
            float timer = 0f;

            while (timer < totalDuration)
            {
                timer += Time.deltaTime;
                float globalT = Mathf.Clamp01(timer / totalDuration);

                // 글로우: 빠르게 나타나고 천천히 사라짐
                if (glowRt != null)
                {
                    float glowAlpha = globalT < 0.3f
                        ? globalT / 0.3f * 0.4f
                        : 0.4f * (1f - (globalT - 0.3f) / 0.7f);
                    glowImg.color = new Color(0.7f, 0.3f, 1f, Mathf.Max(0f, glowAlpha));
                    float glowScale = 1f + globalT * 0.3f;
                    glowRt.localScale = Vector3.one * glowScale;
                }

                // 파티클 업데이트
                for (int i = particleData.Count - 1; i >= 0; i--)
                {
                    var (rt, dir, speed, life, maxLife, color) = particleData[i];
                    if (rt == null) continue;

                    float newLife = life + Time.deltaTime;
                    if (newLife >= maxLife)
                    {
                        if (particles[i] != null) Destroy(particles[i]);
                        continue;
                    }

                    float lifeT = newLife / maxLife;
                    // 나선형 이동 (약간 회전)
                    float spiralAngle = lifeT * 90f * Mathf.Deg2Rad;
                    Vector2 rotatedDir = new Vector2(
                        dir.x * Mathf.Cos(spiralAngle) - dir.y * Mathf.Sin(spiralAngle),
                        dir.x * Mathf.Sin(spiralAngle) + dir.y * Mathf.Cos(spiralAngle)
                    );
                    rt.anchoredPosition += rotatedDir * speed * Time.deltaTime * (1f - lifeT * 0.5f);

                    // 페이드아웃 + 크기 감소
                    float alpha = 1f - lifeT * lifeT;
                    rt.localScale = Vector3.one * (1f - lifeT * 0.4f);
                    var imgComp = particles[i].GetComponent<UnityEngine.UI.Image>();
                    if (imgComp != null)
                        imgComp.color = new Color(color.r, color.g, color.b, color.a * alpha);

                    particleData[i] = (rt, dir, speed, newLife, maxLife, color);
                }

                yield return null;
            }

            // 정리
            foreach (var p in particles)
                if (p != null) Destroy(p);
            if (glowObj != null) Destroy(glowObj);
        }

        /// <summary>
        /// 언데드 소환 위치에 몽환적 파티클 이펙트.
        /// 마녀 소환 파티클과 동일한 색감으로 소환 지점에서 안쪽으로 수렴 → 폭발.
        /// </summary>
        private IEnumerator UndeadSpawnParticles(Vector2 spawnPos, Transform parent)
        {
            if (parent == null) yield break;

            int particleCount = 12;
            var particles = new List<GameObject>();

            Color[] mysticColors = new Color[]
            {
                new Color(0.7f, 0.3f, 1f, 0.9f),   // 보라
                new Color(1f, 0.4f, 0.8f, 0.9f),    // 분홍
                new Color(0.4f, 0.8f, 1f, 0.9f),    // 시안
                new Color(0.9f, 0.6f, 1f, 0.9f),    // 연보라
                new Color(0.5f, 1f, 0.9f, 0.9f),    // 민트
            };

            // 원형 소프트 텍스처 (공용)
            int texSize = 16;
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            float halfTex = texSize * 0.5f;
            for (int y = 0; y < texSize; y++)
                for (int x = 0; x < texSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(halfTex, halfTex)) / halfTex;
                    float alpha = Mathf.Clamp01(1f - dist * dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            Sprite circleSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));

            // 파티클 생성: 바깥에서 중심으로 수렴
            float[] angles = new float[particleCount];
            float[] startRadii = new float[particleCount];
            RectTransform[] rts = new RectTransform[particleCount];
            Color[] colors = new Color[particleCount];

            for (int i = 0; i < particleCount; i++)
            {
                GameObject p = new GameObject($"UndeadSpawnParticle_{i}");
                p.transform.SetParent(parent, false);
                var img = p.AddComponent<UnityEngine.UI.Image>();
                img.raycastTarget = false;
                img.sprite = circleSprite;

                Color c = mysticColors[Random.Range(0, mysticColors.Length)];
                img.color = c;
                colors[i] = c;

                var rt = p.GetComponent<RectTransform>();
                float angle = (360f / particleCount * i + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                float radius = Random.Range(50f, 80f);
                angles[i] = angle;
                startRadii[i] = radius;
                rt.anchoredPosition = spawnPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                float size = Random.Range(8f, 14f);
                rt.sizeDelta = new Vector2(size, size);
                rts[i] = rt;

                particles.Add(p);
            }

            // 중앙 글로우
            GameObject glowObj = new GameObject("UndeadSpawnGlow");
            glowObj.transform.SetParent(parent, false);
            var glowImg = glowObj.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.color = new Color(0.5f, 0.2f, 0.8f, 0f);
            var glowRt = glowObj.GetComponent<RectTransform>();
            glowRt.anchoredPosition = spawnPos;
            glowRt.sizeDelta = new Vector2(80f, 80f);
            {
                int gSize = 32;
                Texture2D gTex = new Texture2D(gSize, gSize, TextureFormat.RGBA32, false);
                float gHalf = gSize * 0.5f;
                for (int y = 0; y < gSize; y++)
                    for (int x = 0; x < gSize; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(gHalf, gHalf)) / gHalf;
                        float a = Mathf.Clamp01(1f - dist);
                        a *= a;
                        gTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                gTex.Apply();
                glowImg.sprite = Sprite.Create(gTex, new Rect(0, 0, gSize, gSize), new Vector2(0.5f, 0.5f));
            }

            // Phase 1: 수렴 (0.4초) — 바깥에서 중심으로 나선 이동
            float convergeDur = 0.4f;
            float elapsed = 0f;
            while (elapsed < convergeDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / convergeDur);
                float easeT = t * t; // ease-in

                for (int i = 0; i < particleCount; i++)
                {
                    if (rts[i] == null) continue;
                    float r = startRadii[i] * (1f - easeT);
                    float spiralAngle = angles[i] + t * 180f * Mathf.Deg2Rad; // 나선 회전
                    rts[i].anchoredPosition = spawnPos + new Vector2(Mathf.Cos(spiralAngle), Mathf.Sin(spiralAngle)) * r;
                    float scale = 0.6f + 0.4f * (1f - t);
                    rts[i].localScale = Vector3.one * scale;
                }

                // 글로우 점점 밝아짐
                if (glowRt != null)
                {
                    float ga = t * 0.5f;
                    glowImg.color = new Color(0.5f, 0.2f, 0.8f, ga);
                    glowRt.localScale = Vector3.one * (1f - t * 0.3f);
                }

                yield return null;
            }

            // Phase 2: 폭발 확산 (0.35초) — 중심에서 바깥으로 퍼지며 페이드아웃
            float burstDur = 0.35f;
            elapsed = 0f;

            // 폭발 방향 재설정
            float[] burstAngles = new float[particleCount];
            for (int i = 0; i < particleCount; i++)
                burstAngles[i] = Random.Range(0f, 360f) * Mathf.Deg2Rad;

            while (elapsed < burstDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / burstDur);
                float easeT = 1f - (1f - t) * (1f - t); // ease-out

                for (int i = 0; i < particleCount; i++)
                {
                    if (rts[i] == null || particles[i] == null) continue;
                    float r = easeT * 60f;
                    rts[i].anchoredPosition = spawnPos + new Vector2(Mathf.Cos(burstAngles[i]), Mathf.Sin(burstAngles[i])) * r;
                    rts[i].localScale = Vector3.one * (0.6f * (1f - t));

                    var imgComp = particles[i].GetComponent<UnityEngine.UI.Image>();
                    if (imgComp != null)
                        imgComp.color = new Color(colors[i].r, colors[i].g, colors[i].b, colors[i].a * (1f - t));
                }

                // 글로우 플래시 후 소멸
                if (glowRt != null)
                {
                    float ga = t < 0.2f ? 0.5f + t / 0.2f * 0.3f : 0.8f * (1f - (t - 0.2f) / 0.8f);
                    glowImg.color = new Color(0.6f, 0.3f, 1f, Mathf.Max(0f, ga));
                    glowRt.localScale = Vector3.one * (0.7f + easeT * 0.6f);
                }

                yield return null;
            }

            // 정리
            foreach (var p in particles)
                if (p != null) Destroy(p);
            if (glowObj != null) Destroy(glowObj);
        }

        /// <summary>
        /// 언데드 행동 페이즈: 매턴 인접 블록으로 이동 + 주변 블록 1개 공격
        /// </summary>
        private IEnumerator UndeadGoblinPhase()
        {
            var aliveUndead = goblins.Where(g => g.isAlive && g.isUndead).ToList();
            if (aliveUndead.Count == 0) yield break;

            Debug.Log($"[언데드턴] 행동 시작 — {aliveUndead.Count}마리");

            var occupiedPositions = new HashSet<HexCoord>(goblins.Where(g => g.isAlive).Select(g => g.position));
            foreach (var g in goblins.Where(g => g.isAlive && g.isHeavy && g.occupiedCoords != null))
                foreach (var c in g.occupiedCoords) occupiedPositions.Add(c);

            foreach (var undead in aliveUndead)
            {
                if (!undead.isAlive || undead.visualObject == null) continue;

                // ★ 소환된 턴에는 이동/공격 안 함
                if (undead.justSummoned) continue;

                // 1. 이동: 블록 필드 내 인접 위치로 이동
                var neighbors = new List<HexCoord>();
                foreach (var dir in HexCoord.Directions)
                {
                    HexCoord n = new HexCoord(undead.position.q + dir.q, undead.position.r + dir.r);
                    if (hexGrid.IsInsideGrid(n) && !occupiedPositions.Contains(n))
                        neighbors.Add(n);
                }

                if (neighbors.Count > 0)
                {
                    HexCoord target = neighbors[Random.Range(0, neighbors.Count)];
                    occupiedPositions.Remove(undead.position);
                    undead.position = target;
                    occupiedPositions.Add(target);

                    Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(target);
                    yield return StartCoroutine(AnimateGoblinMove(undead, worldPos));
                }

                // 2. 공격: 현재 위치 주변 블록 중 하나를 랜덤 공격 (일반 크랙 공격)
                var attackTargets = new List<HexBlock>();
                foreach (var dir in HexCoord.Directions)
                {
                    HexCoord attackCoord = new HexCoord(undead.position.q + dir.q, undead.position.r + dir.r);
                    if (!hexGrid.IsInsideGrid(attackCoord)) continue;
                    HexBlock block = hexGrid.GetBlock(attackCoord);
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None
                        && !block.Data.isShell && !block.Data.isCracked)
                    {
                        attackTargets.Add(block);
                    }
                }

                if (attackTargets.Count > 0)
                {
                    HexBlock attackBlock = attackTargets[Random.Range(0, attackTargets.Count)];
                    // 일반 크랙 공격 (고블린 기본 공격과 동일)
                    attackBlock.Data.isCracked = true;
                    attackBlock.UpdateVisuals();
                    StartCoroutine(BlockFlashEffect(attackBlock.gameObject, new Color(0.6f, 1f, 0.6f, 1f)));
                    Debug.Log($"[언데드턴] 블록 크랙 공격: ({undead.position}) → ({attackBlock.Coord})");
                }
            }

            // ★ 마녀별 언데드 전멸 체크 → 마녀에 1 데미지 + 즉시 재소환
            yield return StartCoroutine(CheckAllUndeadDeadForWitches());
        }

        /// <summary>
        /// 특정 마녀의 모든 언데드가 전멸했는지 체크.
        /// 전멸 시 해당 마녀에 1 데미지 + 즉시 언데드 재소환.
        /// </summary>
        private IEnumerator CheckAllUndeadDeadForWitches()
        {
            var aliveWitches = goblins.Where(g => g.isAlive && g.isWitch).ToList();
            foreach (var witch in aliveWitches)
            {
                int aliveUndeadCount = goblins.Count(g => g.isAlive && g.isUndead && g.undeadOwnerIndex == witch.witchOwnerIndex);
                if (aliveUndeadCount == 0)
                {
                    yield return StartCoroutine(WitchUndeadWipeout(witch));
                }
            }
        }

        /// <summary>
        /// 언데드 전멸 시 마녀에 1 데미지 + 재소환 처리 (공통 로직).
        /// 사망 경로(매칭/특수블록/낙하/넉백)에서 호출.
        /// </summary>
        private IEnumerator WitchUndeadWipeout(GoblinData witch)
        {
            // 마녀에 1 데미지
            witch.hp -= 1;
            UpdateHPBar(witch);
            if (witch.visualObject != null)
                SpawnDamagePopups(witch, 1);
            StartCoroutine(DamageFlashAnimation(witch));
            Debug.Log($"[마녀] 언데드 전멸 → 마녀({witch.position}) 1 데미지, HP={witch.hp}/{witch.maxHp}");

            if (witch.hp <= 0)
            {
                witch.hp = 0;
                witch.isAlive = false;
                totalKills++;
                IncrementTypeKill(witch);
                OnGoblinKilled?.Invoke(totalKills, witch.isArmored, witch.isArcher, witch.isShieldType, witch.isBomb, witch.isHealer, witch.isHeavy, witch.isWizard, witch.isThief, witch.isWitch, witch.monsterLevel);
                StartCoroutine(DeathAnimation(witch));
            }
            else
            {
                // ★ 즉시 소환하지 않고, 모든 몬스터 행동 후 재소환 대기열에 등록
                if (!pendingWitchResummon.Contains(witch.witchOwnerIndex))
                {
                    pendingWitchResummon.Add(witch.witchOwnerIndex);
                    Debug.Log($"[마녀] 재소환 대기열 등록: ownerIdx={witch.witchOwnerIndex}");
                }
            }
            yield break;
        }

        /// <summary>
        /// 언데드가 사망했을 때 즉시 해당 마녀의 언데드 전멸 여부를 체크.
        /// 매칭/특수블록/낙하/넉백 등 모든 사망 경로에서 호출.
        /// </summary>
        private void CheckUndeadDeathTrigger(GoblinData killedGoblin)
        {
            if (!killedGoblin.isUndead) return;

            int ownerIdx = killedGoblin.undeadOwnerIndex;
            var witch = goblins.FirstOrDefault(g => g.isAlive && g.isWitch && g.witchOwnerIndex == ownerIdx);
            if (witch == null) return;

            // 해당 마녀의 남은 언데드 수 확인
            int aliveUndeadCount = goblins.Count(g => g.isAlive && g.isUndead && g.undeadOwnerIndex == ownerIdx);
            if (aliveUndeadCount == 0)
            {
                Debug.Log($"[마녀] 소속 언데드 전멸 감지 (ownerIdx={ownerIdx}) → 마녀 데미지 + 재소환 트리거");
                StartCoroutine(WitchUndeadWipeout(witch));
            }
        }

        /// <summary>
        /// 마녀 사망 시 소속 언데드 전부 즉시 제거
        /// </summary>
        private IEnumerator RemoveAllUndeadOfWitch(int witchOwnerIdx)
        {
            var undeadToRemove = goblins.Where(g => g.isAlive && g.isUndead && g.undeadOwnerIndex == witchOwnerIdx).ToList();
            if (undeadToRemove.Count == 0) yield break;

            Debug.Log($"[마녀사망] ownerIdx={witchOwnerIdx}의 언데드 {undeadToRemove.Count}마리 즉시 제거");

            List<Coroutine> deathCos = new List<Coroutine>();
            foreach (var undead in undeadToRemove)
            {
                undead.hp = 0;
                undead.isAlive = false;
                // 언데드 킬은 미션 카운트에 반영하지 않음 (OnGoblinKilled 미호출)
                if (undead.visualObject != null)
                    deathCos.Add(StartCoroutine(DeathAnimation(undead)));
            }
            foreach (var co in deathCos)
                yield return co;
        }

        /// <summary>
        /// 마녀 소환 직후 초기 언데드 소환 (SpawnWaveBatch에서 비주얼 생성 후 호출)
        /// </summary>
        public IEnumerator WitchInitialSummon()
        {
            var newWitches = goblins.Where(g => g.isAlive && g.isWitch && g.witchSummonCounter == 0 && g.visualObject != null).ToList();
            foreach (var witch in newWitches)
            {
                // 첫 소환으로 간주 — 카운터를 0으로 리셋 (6턴 후 다음 소환)
                witch.witchSummonCounter = 0;
                yield return StartCoroutine(WitchSummonUndead(witch));
            }
        }

        public static Sprite GetThiefGoblinSprite()
        {
            if (thiefGoblinSprite == null)
                thiefGoblinSprite = CreateThiefGoblinSprite(256);
            return thiefGoblinSprite;
        }

        /// <summary>
        /// 도둑 고블린 프로시저럴 스프라이트 생성:
        /// 진한 남색 원형 몸통 + 복면(눈만 보이는 마스크) + 수리검 장식
        /// </summary>
        private static Sprite CreateThiefGoblinSprite(int texSize)
        {
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[texSize * texSize];
            float center = texSize / 2f;
            float radius = texSize * 0.30f;

            // 배경 투명
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color bodyColor = new Color(0.15f, 0.15f, 0.30f, 1f);       // 진한 남색 몸통
            Color maskColor = new Color(0.10f, 0.10f, 0.10f, 1f);       // 검은 복면
            Color eyeColor = new Color(0.95f, 0.85f, 0.2f, 1f);         // 노란 눈 (날카로운 인상)
            Color shurikenColor = new Color(0.7f, 0.7f, 0.75f, 1f);     // 은색 수리검

            float bodyY = center + texSize * 0.05f; // 몸통 중심

            // 1. 원형 몸통
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - bodyY;
                    if (dx * dx + dy * dy <= radius * radius)
                        pixels[y * texSize + x] = bodyColor;
                }
            }

            // 2. 복면 (눈 슬릿만 남기고 얼굴 하반부 가림)
            float maskTop = bodyY - radius * 0.1f;
            float maskBottom = bodyY + radius * 0.55f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = x - center;
                    float dy = y - bodyY;
                    if (dx * dx + dy * dy <= radius * radius && y >= maskTop && y <= maskBottom)
                        pixels[y * texSize + x] = maskColor;
                }
            }

            // 3. 날카로운 눈 (좁고 긴 타원형, 노란색)
            float eyeWidth = texSize * 0.065f;
            float eyeHeight = texSize * 0.025f;
            float eyeY = bodyY - radius * 0.05f;
            float eyeSpacing = radius * 0.35f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    // 왼쪽 눈
                    float dlx = (x - (center - eyeSpacing)) / eyeWidth;
                    float dly = (y - eyeY) / eyeHeight;
                    if (dlx * dlx + dly * dly <= 1f)
                        pixels[y * texSize + x] = eyeColor;
                    // 오른쪽 눈
                    float drx = (x - (center + eyeSpacing)) / eyeWidth;
                    if (drx * drx + dly * dly <= 1f)
                        pixels[y * texSize + x] = eyeColor;
                }
            }

            // 4. 수리검 장식 (오른쪽 어깨 부근, 4방향 다이아몬드)
            float shurikenX = center + radius * 0.7f;
            float shurikenY = bodyY - radius * 0.6f;
            float shurikenSize = texSize * 0.06f;
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx = Mathf.Abs(x - shurikenX);
                    float dy = Mathf.Abs(y - shurikenY);
                    // 다이아몬드(마름모) 형태: |dx| + |dy| <= size
                    if (dx + dy <= shurikenSize)
                        pixels[y * texSize + x] = shurikenColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), Vector2.one * 0.5f, 100f);
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
        /// 특정 좌표의 방패 고블린 내구도를 반환. 방패 고블린이 없으면 0.
        /// </summary>
        public int GetShieldHpAt(HexCoord coord)
        {
            var goblin = goblins.FirstOrDefault(g => g.isAlive && g.isShielded && g.position == coord);
            return goblin != null ? goblin.shieldHp : 0;
        }

        /// <summary>
        /// 드릴이 방패 고블린 위치에 도달했을 때 호출 (측면 타격).
        /// 드릴 1발 = 방패 내구도 1 감소. 완전 차단(드릴 정지).
        /// 방패 내구도가 1이면 파괴 후 드릴 계속 진행.
        /// 반환: 0=방패 없음, 1=완전 차단(드릴 정지), 2=관통(방패 파괴 후 드릴 계속 진행)
        /// </summary>
        public int TryBlockDrill(HexCoord position)
        {
            var goblin = goblins.FirstOrDefault(g => g.isAlive && g.isShielded && g.position == position);
            if (goblin == null) return 0;

            StartCoroutine(ShieldJoltEffect(goblin));
            StartCoroutine(AddStuckDrillVisual(goblin));

            // 드릴 데미지 스킬 보너스 → 방패 내구도 감소에도 동일 적용
            int drillDmg = 1 + (SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetDrillDamageBonus() : 0);
            bool willBreak = goblin.shieldHp <= drillDmg;

            ApplyShieldDamage(goblin, drillDmg);

            if (willBreak)
            {
                // 방패 파괴 → 드릴 관통
                return 2;
            }
            else
            {
                // 방패 유지 → 드릴 차단
                return 1;
            }
        }

        /// <summary>
        /// 발동 위치의 방패 고블린에 단일 드릴 타격 1회 적용.
        /// TryBlockDrill과 달리 항상 1회만 타격하며, 순차 처리를 위해 사용.
        /// 반환: 0=방패 없음, 1=방패가 타격 흡수(아직 존재), 2=방패 파괴됨
        /// </summary>
        public int ApplySingleDrillToShield(HexCoord position)
        {
            var goblin = goblins.FirstOrDefault(g => g.isAlive && g.isShielded && g.position == position);
            if (goblin == null) return 0;

            StartCoroutine(ShieldJoltEffect(goblin));
            StartCoroutine(AddStuckDrillVisual(goblin));

            // 드릴 데미지 스킬 보너스 → 방패 내구도 감소에도 동일 적용
            int drillDmg = 1 + (SkillTreeManager.Instance != null ? SkillTreeManager.Instance.GetDrillDamageBonus() : 0);
            bool willBreak = goblin.shieldHp <= drillDmg;
            ApplyShieldDamage(goblin, drillDmg);

            if (willBreak)
            {
                Debug.Log($"[GoblinSystem] 단일 드릴 타격 → 방패 파괴! ({position})");
                return 2; // 방패 파괴됨
            }
            else
            {
                Debug.Log($"[GoblinSystem] 단일 드릴 타격 → 방패 흡수 (남은HP={goblin.shieldHp}) ({position})");
                return 1; // 방패 건재
            }
        }

        /// <summary>
        /// 방패에 대미지를 적용. 방패 HP 감소 + 비주얼 업데이트.
        /// 방패 파괴 시 일반 고블린으로 전환 (본체 HP 유지).
        /// </summary>
        /// <summary>좌표 기반 방패 데미지 (외부 호출용)</summary>
        public void ApplyShieldDamagePublic(HexCoord position, int damage)
        {
            var goblin = goblins.FirstOrDefault(g => g.isAlive && g.isShielded && g.position == position);
            if (goblin != null) ApplyShieldDamage(goblin, damage);
        }

        /// <summary>
        /// 방패가 데미지를 차단했을 때 "방어" 팝업 표시 (외부 호출용).
        /// 인접 매칭 데미지 등 방패 내구도 감소 없이 본체 데미지만 차단하는 경우 사용.
        /// </summary>
        public void ShowShieldBlockPopup(GoblinData goblin)
        {
            if (goblin == null || !goblin.isAlive || !goblin.isShielded) return;
            SpawnInfoPopup(goblin, "방어", Color.white);
            // 기존 연출 중단 후 새 연출 시작 (위치 드리프트 방지)
            if (goblin.activeShieldBlockCo != null) StopCoroutine(goblin.activeShieldBlockCo);
            goblin.activeShieldBlockCo = StartCoroutine(ShieldBlockEffect(goblin));
        }

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
                // ★ 방패파괴 텍스트 (방패피해와 동시 표시하지 않음)
                SpawnInfoPopup(goblin, "방패파괴", Color.red);

                goblin.isShielded = false;

                // 방패 파괴 → 본체 HP 유지 (소환 시 설정된 HP 그대로)
                UpdateHPBar(goblin);

                StartCoroutine(ShieldBreakEffect(goblin));
                Debug.Log($"[GoblinSystem] 방패 파괴! ({goblin.position}) HP={goblin.hp}/{goblin.maxHp} 일반 고블린으로 전환");
            }
            else
            {
                // ★ 방패피해 텍스트 (내구도 감소했지만 아직 건재)
                SpawnInfoPopup(goblin, "방패피해", new Color(1f, 0.6f, 0f, 1f));
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

        // ============================================================
        // 폭탄 고블린 시스템 (블록 일체형 GoblinBomb)
        // 폭탄은 BlockData.hasGoblinBomb 필드로 블록에 직접 설치.
        // 회전/낙하/매칭/특수 블록 생성 시 자동으로 블록과 함께 이동/제거됨.
        // ============================================================

        /// <summary>
        /// 폭탄 고블린 통합 페이즈: 블록이 있는 곳으로 이동 (공격 없음).
        /// - 설치 턴 ((bombTurnCounter-1) % 3 == 0): 현재 블록에 폭탄 설치
        /// - 비설치 턴: 1칸 이동만
        /// </summary>
        private IEnumerator BombGoblinPhase()
        {
            var aliveBombGoblins = goblins.Where(g => g.isAlive && g.isBomb).ToList();
            if (aliveBombGoblins.Count == 0) yield break;

            HashSet<HexCoord> occupiedCoords = new HashSet<HexCoord>();
            foreach (var g in goblins.Where(g => g.isAlive))
                occupiedCoords.Add(g.position);

            foreach (var goblin in aliveBombGoblins)
            {
                bool isInstallTurn = (goblin.bombTurnCounter - 1) % 3 == 0;
                bool installed = false;
                HexCoord bombPlacedAt = goblin.position; // 폭탄 설치 위치 기억

                // 설치 턴: 폭탄 설치
                if (isInstallTurn)
                {
                    installed = InstallGoblinBombOnBlock(goblin);
                }

                // 이동 칸 수: 설치 성공 시 2칸, 미설치 시 1칸
                int moveSteps = installed ? 2 : 1;

                for (int step = 0; step < moveSteps; step++)
                {
                    // 설치 후 이동: 폭탄에서 멀어지는 방향만 선택
                    HexCoord avoidCoord = installed ? bombPlacedAt : goblin.position;
                    HexCoord moveTarget = FindBombGoblinMoveTarget(goblin, occupiedCoords,
                        installed ? avoidCoord : (HexCoord?)null);
                    if (moveTarget == goblin.position) break;

                    occupiedCoords.Remove(goblin.position);
                    goblin.position = moveTarget;
                    occupiedCoords.Add(moveTarget);

                    Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(moveTarget);
                    yield return StartCoroutine(AnimateGoblinMove(goblin, worldPos));
                }
            }
        }

        /// <summary>
        /// 폭탄 고블린 이동 타겟 결정.
        /// ★ 블록이 있는 셀도 이동 가능 (공격 없이 블록 위에 서는 형태).
        /// 우선순위: 그리드 내부(블록 있는 곳) > 그리드 내부 빈 칸 > 그리드 외부.
        /// 아래 방향 우선.
        /// </summary>
        private HexCoord FindBombGoblinMoveTarget(GoblinData goblin, HashSet<HexCoord> occupiedCoords,
            HexCoord? avoidPos = null)
        {
            System.Func<HexCoord, bool> isOccByOther = (coord) =>
                occupiedCoords.Contains(coord) && coord != goblin.position;

            // 중앙 행 기준: 그리드 행 수 절반
            int centerR = hexGrid.GridRadius / 2;
            bool isUpperHalf = goblin.position.r < centerR;

            HexCoord[] directions;

            if (isUpperHalf)
            {
                HexCoord[] downDirs = {
                    new HexCoord(0, 1),
                    new HexCoord(-1, 1),
                    new HexCoord(1, 0)
                };
                if (Random.value > 0.5f)
                {
                    var temp = downDirs[1]; downDirs[1] = downDirs[2]; downDirs[2] = temp;
                }
                HexCoord[] sideDirs = {
                    new HexCoord(-1, 0),
                    new HexCoord(1, -1),
                    new HexCoord(0, -1)
                };
                directions = downDirs.Concat(sideDirs.OrderBy(x => Random.value)).ToArray();
            }
            else
            {
                directions = HexCoord.Directions.OrderBy(x => Random.value).ToArray();
            }

            // avoidPos가 설정된 경우: 폭탄에서 멀어지는 방향만 필터링
            // hex distance(target, avoidPos) > hex distance(current, avoidPos) 인 방향만 허용
            int currentDistToAvoid = 0;
            if (avoidPos.HasValue)
            {
                int dq = goblin.position.q - avoidPos.Value.q;
                int dr = goblin.position.r - avoidPos.Value.r;
                int ds = -dq - dr;
                currentDistToAvoid = Mathf.Max(Mathf.Abs(dq), Mathf.Max(Mathf.Abs(dr), Mathf.Abs(ds)));
            }

            // 1차: 그리드 내부 셀 (폭탄 회피 적용)
            foreach (var dir in directions)
            {
                HexCoord target = goblin.position + dir;
                if (!hexGrid.IsInsideGrid(target)) continue;
                if (isOccByOther(target)) continue;

                // 폭탄 회피: 이동 후 폭탄과의 거리가 현재보다 가까워지면 스킵
                if (avoidPos.HasValue)
                {
                    int tdq = target.q - avoidPos.Value.q;
                    int tdr = target.r - avoidPos.Value.r;
                    int tds = -tdq - tdr;
                    int targetDist = Mathf.Max(Mathf.Abs(tdq), Mathf.Max(Mathf.Abs(tdr), Mathf.Abs(tds)));
                    if (targetDist <= currentDistToAvoid) continue; // 가까워지거나 같으면 스킵
                }

                return target;
            }

            // 2차: 그리드 외부 셀 (소환 영역, 폭탄 회피 적용)
            foreach (var dir in directions)
            {
                HexCoord target = goblin.position + dir;
                if (hexGrid.IsInsideGrid(target)) continue;
                if (isOccByOther(target)) continue;
                if (Mathf.Abs(target.q) > hexGrid.GridRadius) continue;
                int rMin = hexGrid.GetTopR(target.q);
                int rMax = Mathf.Min(hexGrid.GridRadius, -target.q + hexGrid.GridRadius);
                if (target.r < rMin - 3 || target.r > rMax) continue;

                if (avoidPos.HasValue)
                {
                    int tdq = target.q - avoidPos.Value.q;
                    int tdr = target.r - avoidPos.Value.r;
                    int tds = -tdq - tdr;
                    int targetDist = Mathf.Max(Mathf.Abs(tdq), Mathf.Max(Mathf.Abs(tdr), Mathf.Abs(tds)));
                    if (targetDist <= currentDistToAvoid) continue;
                }

                return target;
            }

            return goblin.position;
        }

        /// <summary>
        /// 고블린 폭탄을 현재 서 있는 블록에 설치 (BlockData에 기록).
        /// 블록이 없거나 이미 폭탄이 있으면 설치 스킵.
        /// 반환: true=설치 성공, false=설치 실패
        /// </summary>
        // ============================================================
        // 힐러 고블린 페이즈
        // ============================================================

        private IEnumerator HealerGoblinPhase()
        {
            var aliveHealers = goblins.Where(g => g.isAlive && g.isHealer).ToList();
            if (aliveHealers.Count == 0) yield break;

            // 힐러 외 다른 고블린이 살아있는지 확인
            bool hasOtherAlive = goblins.Any(g => g.isAlive && !g.isHealer);

            HashSet<HexCoord> occupiedCoords = new HashSet<HexCoord>();
            foreach (var g in goblins.Where(g => g.isAlive))
                occupiedCoords.Add(g.position);

            foreach (var healer in aliveHealers)
            {
                healer.healerTurnCounter++;

                if (hasOtherAlive)
                {
                    // === 기존 행동: 다른 고블린이 있으면 힐링/이동 ===
                    if (healer.healerTurnCounter % 2 == 0)
                    {
                        PerformHeal(healer);
                    }
                    else
                    {
                        if (Random.value < 0.5f)
                        {
                            HexCoord moveTarget = FindHealerMoveTarget(healer, occupiedCoords);
                            if (moveTarget != healer.position)
                            {
                                occupiedCoords.Remove(healer.position);
                                healer.position = moveTarget;
                                occupiedCoords.Add(moveTarget);
                                Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(moveTarget);
                                yield return StartCoroutine(AnimateGoblinMove(healer, worldPos));
                            }
                        }
                    }
                }
                else
                {
                    // === 혼자 남았을 때: 블록 필드로 진입 → 공격 ===
                    Debug.Log($"[힐러] 혼자 남음! isInBlockField={healer.isInBlockField} 위치={healer.position}");

                    // isInBlockField 상태 갱신: 현재 위치가 그리드 내부이면 true
                    if (!healer.isInBlockField && hexGrid.IsInsideGrid(healer.position))
                        healer.isInBlockField = true;

                    // 이동
                    HexCoord moveTarget = FindHealerSoloMoveTarget(healer, occupiedCoords);
                    if (!moveTarget.Equals(healer.position))
                    {
                        occupiedCoords.Remove(healer.position);
                        healer.position = moveTarget;
                        occupiedCoords.Add(moveTarget);
                        Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(moveTarget);
                        yield return StartCoroutine(AnimateGoblinMove(healer, worldPos));

                        // 이동 후 블록 필드 진입 체크
                        if (!healer.isInBlockField && hexGrid.IsInsideGrid(healer.position))
                            healer.isInBlockField = true;
                    }

                    // 블록 필드 안에 있을 때만 공격: 인접 블록 3개를 ShellBlock으로 변환
                    if (healer.isInBlockField && hexGrid.IsInsideGrid(healer.position))
                    {
                        yield return StartCoroutine(HealerSoloAttack(healer));
                    }
                }
            }
        }

        /// <summary>
        /// 힐러 혼자일 때 이동 목표 탐색.
        /// 소환 영역: 아래쪽 방향으로 1칸 (블록 필드 진입 시도).
        /// 블록 필드: 인접 블록이 있는 칸으로만 이동 (소환 영역으로 돌아가지 않음).
        /// </summary>
        private HexCoord FindHealerSoloMoveTarget(GoblinData healer, HashSet<HexCoord> occupied)
        {
            if (hexGrid == null) return healer.position;

            HexCoord current = healer.position;
            var neighbors = current.GetAllNeighbors();

            if (!healer.isInBlockField)
            {
                // 소환 영역: 아래쪽 방향 우선 (r 증가 = 화면 아래)
                // DownDirections: (1,0), (0,1), (-1,1)
                foreach (var dir in DownDirections)
                {
                    HexCoord target = current + dir;
                    if (occupied.Contains(target)) continue;

                    // 그리드 내부거나 소환 영역 범위 내이면 이동
                    if (hexGrid.IsInsideGrid(target))
                        return target;

                    // 소환 영역 범위 체크
                    if (Mathf.Abs(target.q) <= hexGrid.GridRadius)
                    {
                        int rMin = hexGrid.GetTopR(target.q);
                        if (target.r >= rMin - 3 && target.r < rMin)
                            return target;
                    }
                }
            }
            else
            {
                // 블록 필드: 인접 블록이 있는 칸으로만 이동 (소환 영역 금지)
                var validTargets = new List<HexCoord>();
                foreach (var n in neighbors)
                {
                    if (!hexGrid.IsInsideGrid(n)) continue; // 소환 영역 돌아가기 금지
                    if (occupied.Contains(n)) continue;

                    HexBlock block = hexGrid.GetBlock(n);
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                        validTargets.Add(n);
                }

                if (validTargets.Count > 0)
                    return validTargets[Random.Range(0, validTargets.Count)];
            }

            return healer.position;
        }

        /// <summary>
        /// 힐러 혼자일 때 공격: 현재 위치 인접 블록 최대 3개를 ShellBlock으로 변환.
        /// </summary>
        private IEnumerator HealerSoloAttack(GoblinData healer)
        {
            if (hexGrid == null || !healer.isAlive) yield break;

            var neighbors = healer.position.GetAllNeighbors();
            int attackCount = 0;

            foreach (var n in neighbors)
            {
                if (attackCount >= 3) break;
                if (!hexGrid.IsInsideGrid(n)) continue;

                HexBlock block = hexGrid.GetBlock(n);
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None || block.Data.isShell) continue;

                if (!block.Data.isCracked)
                {
                    block.Data.isCracked = true;
                    block.UpdateVisuals();
                    StartCoroutine(BlockFlashEffect(block.gameObject, new Color(0.3f, 0.9f, 0.3f, 1f))); // 초록 플래시
                }
                else
                {
                    ConvertToShellBlock(block);
                    StartCoroutine(BlockFlashEffect(block.gameObject, new Color(0.3f, 0.9f, 0.3f, 1f))); // 초록 플래시
                }
                attackCount++;
            }

            if (attackCount > 0)
            {
                Debug.Log($"[힐러] 혼자 공격: ({healer.position}) → {attackCount}블록 변환");
                // 공격 모션: 스케일 펄스
                if (healer.visualObject != null)
                {
                    RectTransform rt = healer.visualObject.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        Vector3 baseScale = rt.localScale;
                        float elapsed = 0f;
                        while (elapsed < 0.15f)
                        {
                            if (rt == null) yield break;
                            elapsed += Time.deltaTime;
                            float t = Mathf.Clamp01(elapsed / 0.15f);
                            rt.localScale = baseScale * (1f + 0.1f * Mathf.Sin(t * Mathf.PI));
                            yield return null;
                        }
                        if (rt != null) rt.localScale = baseScale;
                    }
                }
            }
        }

        // ============================================================
        // 마법사 고블린 페이즈
        // ============================================================

        /// <summary>
        /// 마법사 고블린 턴 행동:
        /// 1. 파이어볼 공격: 블록 필드에서 일반/깨진 블록 랜덤 1개를 타겟 → 크랙/회색 변환
        /// 2. 소환 영역 내 인접 빈 칸으로 1칸 이동
        /// </summary>
        private IEnumerator WizardGoblinPhase()
        {
            var aliveWizards = goblins.Where(g => g.isAlive && g.isWizard).ToList();
            if (aliveWizards.Count == 0) yield break;

            Debug.Log($"[마법사턴] 행동 시작 — {aliveWizards.Count}마리");

            // 1. 각 마법사 공격: 5턴마다 번개, 그 외 파이어볼
            List<Coroutine> attackCoroutines = new List<Coroutine>();
            foreach (var wizard in aliveWizards)
            {
                if (wizard.visualObject == null) continue;
                wizard.wizardTurnCounter++;

                if (wizard.wizardTurnCounter % 5 == 0)
                {
                    // 특수 공격: 번개 (가장 적은 GemType 전체 타겟)
                    Debug.Log($"[마법사턴] 번개 공격! ({wizard.position}) turn={wizard.wizardTurnCounter}");
                    attackCoroutines.Add(StartCoroutine(WizardLightningAttack(wizard)));
                }
                else
                {
                    // 기본 공격: 파이어볼 (랜덤 1개)
                    attackCoroutines.Add(StartCoroutine(WizardFireballAttack(wizard)));
                }
            }
            foreach (var co in attackCoroutines)
                yield return co;

            // 2. 소환 영역 내 이동 (순차)
            var occupiedPositions = new HashSet<HexCoord>(goblins.Where(g => g.isAlive).Select(g => g.position));
            foreach (var g in goblins.Where(g => g.isAlive && g.isHeavy && g.occupiedCoords != null))
                foreach (var c in g.occupiedCoords) occupiedPositions.Add(c);

            foreach (var wizard in aliveWizards)
            {
                if (!wizard.isAlive) continue;
                HexCoord moveTarget = FindWizardMoveTarget(wizard.position, occupiedPositions);
                if (!moveTarget.Equals(wizard.position))
                {
                    occupiedPositions.Remove(wizard.position);
                    occupiedPositions.Add(moveTarget);
                    wizard.position = moveTarget;
                    Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(moveTarget);
                    yield return StartCoroutine(AnimateGoblinMove(wizard, worldPos));
                }
            }
        }

        /// <summary>
        /// 파이어볼 공격: 블록 필드에서 랜덤 타겟 선택 → 투사체 발사 → 블록 변환
        /// </summary>
        private IEnumerator WizardFireballAttack(GoblinData wizard)
        {
            if (wizard == null || !wizard.isAlive || wizard.visualObject == null || hexGrid == null) yield break;

            // 타겟 선택: 일반/깨진/특수 블록 모두 (회색/쉘 제외)
            var candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) continue;
                if (block.Data.isShell) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) yield break;
            HexBlock target = candidates[Random.Range(0, candidates.Count)];

            Debug.Log($"[마법사턴] 파이어볼: ({wizard.position}) → ({target.Coord}) cracked={target.Data.isCracked}");

            // 시전 모션: 스케일 펄스
            yield return StartCoroutine(WizardCastPulse(wizard));

            // 파이어볼 투사체 생성
            RectTransform wizRt = wizard.visualObject.GetComponent<RectTransform>();
            Vector2 startPos = wizRt.anchoredPosition;
            Vector2 targetPos = hexGrid.CalculateFlatTopHexPosition(target.Coord);

            // 지팡이 위치 오프셋 (오른쪽 위)
            float size = hexGrid.HexSize * 1.6f;
            Vector2 staffOffset = new Vector2(size * 0.35f, -size * 0.3f);
            Vector2 fireStart = startPos + staffOffset;

            Transform parent = hexGrid.GridContainer;
            GameObject fireball = new GameObject("Fireball");
            fireball.transform.SetParent(parent, false);

            RectTransform fbRt = fireball.AddComponent<RectTransform>();
            fbRt.anchoredPosition = fireStart;
            fbRt.sizeDelta = new Vector2(20f, 20f);

            UnityEngine.UI.Image fbImg = fireball.AddComponent<UnityEngine.UI.Image>();
            fbImg.color = new Color(1f, 0.35f, 0.1f, 1f); // 주황빨간
            fbImg.raycastTarget = false;

            // 이동 (0.4초, Sin 웨이브 좌우 흔들림)
            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (fireball == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector2 basePos = Vector2.Lerp(fireStart, targetPos, t);
                // 좌우 흔들림: Sin 파도
                Vector2 dir = (targetPos - fireStart).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x); // 수직 방향
                float wave = Mathf.Sin(t * Mathf.PI * 4f) * 5f * (1f - t);
                fbRt.anchoredPosition = basePos + perp * wave;

                // 크기 펄스
                float scale = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
                fbRt.localScale = Vector3.one * scale;

                yield return null;
            }

            if (fireball != null) Destroy(fireball);

            // 블록 변환
            if (target != null && target.Data != null)
            {
                // ★ 특수 블록 공격 시: 발동 없이 기능 파괴 + 크랙 (MoveBlock/FixedBlock 제외)
                if (target.Data.specialType != SpecialBlockType.None
                    && target.Data.specialType != SpecialBlockType.MoveBlock
                    && target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    var removedType = target.Data.specialType;
                    target.Data.specialType = SpecialBlockType.None;
                    target.Data.isCracked = true;
                    target.UpdateVisuals();
                    StartCoroutine(BlockFlashEffect(target.gameObject, new Color(1f, 0.4f, 0.1f, 1f)));
                    Debug.Log($"[마법사턴] 특수 블록 파괴: ({target.Coord}) {removedType} → 크랙");
                }
                else if (!target.Data.isCracked)
                {
                    // 일반 블록 → 깨진 블록
                    target.Data.isCracked = true;
                    target.UpdateVisuals();
                    StartCoroutine(BlockFlashEffect(target.gameObject, new Color(1f, 0.4f, 0.1f, 1f))); // 빨간 주황
                    Debug.Log($"[마법사턴] 블록 크랙: ({target.Coord})");
                }
                else
                {
                    // 깨진 블록 → 껍데기(쉘) 블록 (기존 ShellBlock과 동일 처리)
                    ConvertToShellBlock(target);
                    StartCoroutine(BlockFlashEffect(target.gameObject, new Color(1f, 0.4f, 0.1f, 1f))); // 빨간 주황
                    Debug.Log($"[마법사턴] 블록 쉘화: ({target.Coord})");
                }
            }
        }

        /// <summary>
        /// 블록을 ShellBlock(껍데기)으로 변환.
        /// 고블린 공격/마법사 공격에서 공통 사용.
        /// isShell=true, isCracked=false, specialType=None, tier=Normal → UpdateVisuals.
        /// </summary>
        private void ConvertToShellBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return;

            // ★ GoblinBomb이 설치된 블록이 쉘로 변환되면 즉시 폭발
            if (block.Data.hasGoblinBomb)
            {
                Debug.Log($"[GoblinSystem] 쉘 변환 시 GoblinBomb 즉시 폭발: ({block.Coord})");
                StartCoroutine(ExplodeBombAt(block.Coord));
                return; // 폭발이 블록 상태를 직접 처리하므로 쉘 변환 스킵
            }

            block.Data.isShell = true;
            block.Data.isCracked = false;
            block.Data.specialType = SpecialBlockType.None;
            block.Data.tier = BlockTier.Normal;
            block.UpdateVisuals();
        }

        /// <summary>
        /// 블록 번쩍임 이펙트: Image color를 flashColor로 즉시 변경 → 0.05초 → 원래 색상 복귀, 2회 반복 (총 0.1초).
        /// 블록 데미지/상태 변경 시 시각 피드백으로 사용.
        /// </summary>
        private IEnumerator BlockFlashEffect(GameObject blockObj, Color flashColor)
        {
            if (blockObj == null) yield break;
            var img = blockObj.GetComponent<UnityEngine.UI.Image>();
            if (img == null) img = blockObj.GetComponentInChildren<UnityEngine.UI.Image>();
            if (img == null) yield break;

            Color originalColor = img.color;

            for (int i = 0; i < 2; i++)
            {
                if (img == null || blockObj == null) yield break;
                img.color = flashColor;
                yield return new WaitForSeconds(0.025f);
                if (img == null || blockObj == null) yield break;
                img.color = originalColor;
                yield return new WaitForSeconds(0.025f);
            }
        }

        /// <summary>
        /// 마법사 시전 모션: 스케일 펄스 (0.15초)
        /// </summary>
        private IEnumerator WizardCastPulse(GoblinData wizard)
        {
            if (wizard.visualObject == null) yield break;
            RectTransform rt = wizard.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 baseScale = rt.localScale;
            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = 1f + 0.12f * Mathf.Sin(t * Mathf.PI);
                rt.localScale = baseScale * pulse;
                yield return null;
            }

            if (rt != null) rt.localScale = baseScale;
        }

        /// <summary>
        /// 소환 영역 내 인접 빈 칸 탐색 (마법사 이동용).
        /// 이동 불가 시 현재 위치 반환.
        /// </summary>
        private HexCoord FindWizardMoveTarget(HexCoord current, HashSet<HexCoord> occupied)
        {
            if (hexGrid == null) return current;
            int gridRadius = hexGrid.GridRadius;

            // 소환 영역: rMin-3 ~ rMin-1 (상단 3줄)
            var spawnCoords = hexGrid.GetExtendedTopCoords();
            var spawnSet = new HashSet<HexCoord>(spawnCoords);

            // 인접 6방향에서 소환 영역 내 빈 칸 탐색
            var neighbors = current.GetAllNeighbors();
            var validTargets = new List<HexCoord>();
            foreach (var n in neighbors)
            {
                if (!spawnSet.Contains(n)) continue;
                if (occupied.Contains(n)) continue;
                validTargets.Add(n);
            }

            if (validTargets.Count == 0) return current;
            return validTargets[Random.Range(0, validTargets.Count)];
        }

        // ============================================================
        // 마법사 번개 특수 공격 (5턴마다)
        // ============================================================

        /// <summary>
        /// 번개 특수 공격: 가장 적은 GemType 전체를 타겟으로 번개 연출 + 블록 변환.
        /// 1. 지팡이 들기 (위로 30px, 0.2초)
        /// 2. 지팡이 상단에서 번개 올라감 (0.3초)
        /// 3. 각 타겟에 번개 내려꽂힘 (0.05초 간격, 각 0.15초)
        /// 4. 지팡이 원위치 (0.1초)
        /// 5. 블록 변환: 일반→깨짐, 깨짐→회색
        /// </summary>
        private IEnumerator WizardLightningAttack(GoblinData wizard)
        {
            if (wizard == null || !wizard.isAlive || wizard.visualObject == null || hexGrid == null) yield break;

            // === 타겟 선정: GemType별 블록 수 집계 → 가장 적은 타입 전체 ===
            var gemCounts = new Dictionary<GemType, int>();
            var gemBlocks = new Dictionary<GemType, List<HexBlock>>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) continue;
                if (block.Data.isShell) continue;

                GemType gt = block.Data.gemType;
                if (!gemCounts.ContainsKey(gt)) { gemCounts[gt] = 0; gemBlocks[gt] = new List<HexBlock>(); }
                gemCounts[gt]++;
                gemBlocks[gt].Add(block);
            }

            if (gemCounts.Count == 0) yield break;

            // 가장 적은 수의 GemType (동점이면 enum 순서 우선)
            GemType targetGem = GemType.None;
            int minCount = int.MaxValue;
            foreach (var kvp in gemCounts)
            {
                if (kvp.Value < minCount || (kvp.Value == minCount && (int)kvp.Key < (int)targetGem))
                {
                    minCount = kvp.Value;
                    targetGem = kvp.Key;
                }
            }

            if (targetGem == GemType.None || !gemBlocks.ContainsKey(targetGem)) yield break;
            var targets = gemBlocks[targetGem];
            Debug.Log($"[마법사번개] 타겟: {targetGem} ({targets.Count}블록)");

            RectTransform wizRt = wizard.visualObject.GetComponent<RectTransform>();
            if (wizRt == null) yield break;
            Vector2 wizBasePos = wizRt.anchoredPosition;
            Transform parent = hexGrid.GridContainer;

            Color lightningColor = new Color(1f, 1f, 0.3f, 1f); // 밝은 노란 번개

            // === 1. 지팡이 들기: 위로 30px, 0.2초 ===
            float liftElapsed = 0f;
            while (liftElapsed < 0.2f)
            {
                if (wizRt == null) yield break;
                liftElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(liftElapsed / 0.2f);
                wizRt.anchoredPosition = Vector2.Lerp(wizBasePos, wizBasePos + new Vector2(0, 30f), t);
                yield return null;
            }

            // === 2. 번개 올라가는 연출: 지팡이 상단에서 화면 위로, 0.3초 ===
            float size = hexGrid.HexSize * 1.6f;
            Vector2 staffTop = wizRt.anchoredPosition + new Vector2(size * 0.35f, size * 0.5f);
            Vector2 skyTarget = staffTop + new Vector2(0, 200f);

            GameObject upBolt = new GameObject("LightningUp");
            upBolt.transform.SetParent(parent, false);
            RectTransform upRt = upBolt.AddComponent<RectTransform>();
            upRt.sizeDelta = new Vector2(24f, 120f); // 굵기 3배
            UnityEngine.UI.Image upImg = upBolt.AddComponent<UnityEngine.UI.Image>();
            upImg.color = lightningColor;
            upImg.raycastTarget = false;

            float upElapsed = 0f;
            while (upElapsed < 0.3f)
            {
                if (upBolt == null) yield break;
                upElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(upElapsed / 0.3f);
                upRt.anchoredPosition = Vector2.Lerp(staffTop, skyTarget, t);
                upImg.color = new Color(1f, 0.95f, 0.3f, 1f - t * 0.5f);
                // 좌우 흔들림
                float shake = Mathf.Sin(t * Mathf.PI * 6f) * 4f;
                upRt.anchoredPosition += new Vector2(shake, 0);
                yield return null;
            }
            if (upBolt != null) Destroy(upBolt);

            // === 2.5. 화면 전체 흰색 플래시 (0.05초) ===
            StartCoroutine(LightningScreenFlash(parent));

            // === 3. 각 타겟에 번개 내려꽂힘: 0.05초 간격, 각 0.15초 ===
            List<Coroutine> strikeCoroutines = new List<Coroutine>();
            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null || target.Data == null) continue;

                // 0.05초 간격 시작
                if (i > 0) yield return new WaitForSeconds(0.05f);

                Vector2 blockPos = hexGrid.CalculateFlatTopHexPosition(target.Coord);
                Vector2 boltStart = blockPos + new Vector2(0, 150f);

                strikeCoroutines.Add(StartCoroutine(AnimateLightningStrike(parent, boltStart, blockPos, lightningColor)));
            }
            // 마지막 번개 완료 대기
            foreach (var co in strikeCoroutines)
                yield return co;

            // === 3.5. 화면 셰이크 (0.1초) ===
            StartCoroutine(LightningShake(0.1f, 3f));

            // === 4. 지팡이 원위치: 0.1초 ===
            float downElapsed = 0f;
            Vector2 liftedPos = wizRt != null ? wizRt.anchoredPosition : wizBasePos;
            while (downElapsed < 0.1f)
            {
                if (wizRt == null) yield break;
                downElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(downElapsed / 0.1f);
                wizRt.anchoredPosition = Vector2.Lerp(liftedPos, wizBasePos, t);
                yield return null;
            }
            if (wizRt != null) wizRt.anchoredPosition = wizBasePos;

            // === 5. 블록 변환 ===
            foreach (var target in targets)
            {
                if (target == null || target.Data == null) continue;
                if (target.Data.gemType == GemType.Gray || target.Data.isShell) continue;

                // ★ 특수 블록 공격 시: 발동 없이 기능 파괴 + 크랙 (MoveBlock/FixedBlock 제외)
                if (target.Data.specialType != SpecialBlockType.None
                    && target.Data.specialType != SpecialBlockType.MoveBlock
                    && target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    var removedType = target.Data.specialType;
                    target.Data.specialType = SpecialBlockType.None;
                    target.Data.isCracked = true;
                    target.UpdateVisuals();
                    StartCoroutine(BlockFlashEffect(target.gameObject, new Color(1f, 1f, 0.2f, 1f)));
                    Debug.Log($"[마법사번개] 특수 블록 파괴: ({target.Coord}) {removedType} → 크랙");
                }
                else if (!target.Data.isCracked)
                {
                    target.Data.isCracked = true;
                    target.UpdateVisuals();
                    StartCoroutine(BlockFlashEffect(target.gameObject, new Color(1f, 1f, 0.2f, 1f)));
                }
                else
                {
                    // 깨진 블록 → 껍데기(쉘) 블록
                    ConvertToShellBlock(target);
                    StartCoroutine(BlockFlashEffect(target.gameObject, new Color(1f, 1f, 0.2f, 1f)));
                }
            }

            Debug.Log($"[마법사번개] 완료: {targets.Count}블록 변환");
        }

        /// <summary>
        /// 번개 낙뢰 1회 애니메이션 (강화): 3레이어 번개 + 착탄 임팩트 (0.15초)
        /// </summary>
        private IEnumerator AnimateLightningStrike(Transform parent, Vector2 startPos, Vector2 endPos, Color color)
        {
            // 3레이어 번개 (굵기/색상/밝기 다르게)
            Color brightYellow = new Color(1f, 1f, 0.3f, 1f);
            Color coreWhite = new Color(1f, 1f, 0.9f, 0.9f);
            Color outerYellow = new Color(1f, 0.9f, 0.2f, 0.6f);

            float[] widths = { 18f, 12f, 6f };     // 외곽 → 코어 (기존 6f의 3배)
            Color[] colors = { outerYellow, brightYellow, coreWhite };

            GameObject container = new GameObject("LightningStrike");
            container.transform.SetParent(parent, false);

            RectTransform[] boltRts = new RectTransform[3];
            for (int layer = 0; layer < 3; layer++)
            {
                GameObject bolt = new GameObject($"Bolt{layer}");
                bolt.transform.SetParent(container.transform, false);
                RectTransform brt = bolt.AddComponent<RectTransform>();
                brt.sizeDelta = new Vector2(widths[layer], 90f); // 높이도 3배
                brt.anchoredPosition = startPos;
                UnityEngine.UI.Image bimg = bolt.AddComponent<UnityEngine.UI.Image>();
                bimg.color = colors[layer];
                bimg.raycastTarget = false;
                boltRts[layer] = brt;
            }

            float elapsed = 0f;
            while (elapsed < 0.15f)
            {
                if (container == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.15f);
                Vector2 pos = Vector2.Lerp(startPos, endPos, t);
                float zigzag = Mathf.Sin(t * Mathf.PI * 8f) * 5f * (1f - t);
                float scaleY = 1f + 0.5f * t;

                for (int layer = 0; layer < 3; layer++)
                {
                    if (boltRts[layer] == null) continue;
                    // 각 레이어 약간 다른 오프셋
                    float layerOffset = (layer - 1) * 2f * Mathf.Sin(t * Mathf.PI * 12f);
                    boltRts[layer].anchoredPosition = pos + new Vector2(zigzag + layerOffset, 0);
                    boltRts[layer].localScale = new Vector3(1f, scaleY, 1f);
                }
                yield return null;
            }

            // 착탄 임팩트: 노란 원형 확산 (스케일 1→2, 알파 1→0, 0.2초)
            if (container != null) Destroy(container);
            StartCoroutine(LightningImpactEffect(parent, endPos));
        }

        /// <summary>착탄 임팩트: 노란 원형 확산 이펙트</summary>
        private IEnumerator LightningImpactEffect(Transform parent, Vector2 pos)
        {
            GameObject impact = new GameObject("LightningImpact");
            impact.transform.SetParent(parent, false);
            RectTransform irt = impact.AddComponent<RectTransform>();
            irt.anchoredPosition = pos;
            irt.sizeDelta = new Vector2(30f, 30f);
            irt.localScale = Vector3.one;

            UnityEngine.UI.Image iimg = impact.AddComponent<UnityEngine.UI.Image>();
            iimg.color = new Color(1f, 1f, 0.3f, 1f);
            iimg.raycastTarget = false;

            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                if (impact == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.2f);
                float scale = Mathf.Lerp(1f, 2f, t);
                irt.localScale = Vector3.one * scale;
                iimg.color = new Color(1f, 1f, 0.3f, 1f - t);
                yield return null;
            }
            if (impact != null) Destroy(impact);
        }

        /// <summary>화면 전체 흰색 플래시 (0.05초)</summary>
        private IEnumerator LightningScreenFlash(Transform parent)
        {
            Canvas canvas = parent.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield break;

            GameObject flash = new GameObject("LightningFlash");
            flash.transform.SetParent(canvas.transform, false);
            flash.transform.SetAsLastSibling();
            RectTransform frt = flash.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.sizeDelta = Vector2.zero;

            UnityEngine.UI.Image fimg = flash.AddComponent<UnityEngine.UI.Image>();
            fimg.color = new Color(1f, 1f, 0.9f, 0.6f);
            fimg.raycastTarget = false;

            yield return new WaitForSeconds(0.05f);
            if (flash != null) Destroy(flash);
        }

        /// <summary>화면 셰이크 (hexGrid.transform 흔들기)</summary>
        private IEnumerator LightningShake(float duration, float intensity)
        {
            if (hexGrid == null) yield break;
            Transform target = hexGrid.transform;
            Vector3 originalPos = target.localPosition;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float shake = intensity * (1f - t);
                float offsetX = Mathf.Sin(elapsed * 80f) * shake;
                target.localPosition = originalPos + new Vector3(offsetX, 0, 0);
                yield return null;
            }
            target.localPosition = originalPos;
        }

        /// <summary>힐러 고블린 힐 실행: 피해 입은 아군 랜덤 1마리에 최대 2 회복</summary>
        private void PerformHeal(GoblinData healer)
        {
            var damaged = goblins.Where(g => g.isAlive && g != healer && g.hp < g.maxHp).ToList();
            if (damaged.Count == 0) return;

            var target = damaged[Random.Range(0, damaged.Count)];
            int healAmount = Mathf.Min(2, target.maxHp - target.hp);
            if (healAmount <= 0) return;

            target.hp += healAmount;
            UpdateHPBar(target);

            // 힐러 모션 이펙트 (비차단)
            if (healer.visualObject != null)
                StartCoroutine(HealerMotionEffect(healer));

            // 힐 받는 몬스터 이펙트
            if (target.visualObject != null)
            {
                SpawnHealPopup(target, healAmount);
                StartCoroutine(HealTargetRingEffect(target));
            }

            Debug.Log($"[GoblinSystem] 힐러({healer.position}) → ({target.position}) HP+{healAmount} = {target.hp}/{target.maxHp}");
        }

        /// <summary>힐러 모션: 스케일 펄스 + 초록 파티클</summary>
        private IEnumerator HealerMotionEffect(GoblinData healer)
        {
            if (healer == null || healer.visualObject == null) yield break;
            RectTransform rt = healer.visualObject.GetComponent<RectTransform>();
            if (rt == null) yield break;

            // 스케일 펄스: 1.0 → 1.3 → 1.0 (0.4초)
            float dur = 0.4f;
            float el = 0f;
            while (el < dur)
            {
                if (rt == null) yield break;
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float scale = t < 0.5f
                    ? Mathf.Lerp(1f, 1.3f, t * 2f)
                    : Mathf.Lerp(1.3f, 1f, (t - 0.5f) * 2f);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;

            // 초록 파티클: 4~6개 원이 위로 퍼지며 페이드아웃 (0.3초)
            if (hexGrid == null || healer.visualObject == null) yield break;
            Transform parent = hexGrid.GridContainer;
            Vector2 basePos = rt.anchoredPosition;
            int count = Random.Range(4, 7);
            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject("HealParticle");
                p.transform.SetParent(parent, false);
                RectTransform pRt = p.AddComponent<RectTransform>();
                pRt.anchoredPosition = basePos;
                pRt.sizeDelta = new Vector2(8f, 8f);
                Image pImg = p.AddComponent<Image>();
                pImg.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                pImg.raycastTarget = false;

                float dx = Random.Range(-20f, 20f);
                float dy = Random.Range(15f, 40f);
                StartCoroutine(AnimateHealParticle(p, basePos, new Vector2(dx, dy)));
            }
        }

        private IEnumerator AnimateHealParticle(GameObject particle, Vector2 startPos, Vector2 offset)
        {
            if (particle == null) yield break;
            RectTransform rt = particle.GetComponent<RectTransform>();
            Image img = particle.GetComponent<Image>();
            float dur = 0.6f;
            float el = 0f;
            while (el < dur)
            {
                if (particle == null) yield break;
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                rt.anchoredPosition = startPos + offset * t;
                Color c = img.color;
                c.a = 0.9f * (1f - t);
                img.color = c;
                yield return null;
            }
            if (particle != null) Destroy(particle);
        }

        /// <summary>힐 대상 몬스터 주변 초록 링 이펙트</summary>
        private IEnumerator HealTargetRingEffect(GoblinData target)
        {
            if (target == null || target.visualObject == null || hexGrid == null) yield break;
            Transform parent = hexGrid.GridContainer;
            RectTransform tRt = target.visualObject.GetComponent<RectTransform>();
            if (tRt == null) yield break;

            GameObject ring = new GameObject("HealRing");
            ring.transform.SetParent(parent, false);
            RectTransform rRt = ring.AddComponent<RectTransform>();
            rRt.anchoredPosition = tRt.anchoredPosition;
            rRt.sizeDelta = new Vector2(20f, 20f);
            Image rImg = ring.AddComponent<Image>();
            rImg.color = new Color(0.2f, 1f, 0.3f, 0.7f);
            rImg.raycastTarget = false;

            float dur = 0.8f;
            float el = 0f;
            while (el < dur)
            {
                if (ring == null) yield break;
                el += Time.deltaTime;
                float t = Mathf.Clamp01(el / dur);
                float scale = 1f + t * 3f;
                rRt.localScale = Vector3.one * scale;
                Color c = rImg.color;
                c.a = 0.7f * (1f - t);
                rImg.color = c;
                yield return null;
            }
            if (ring != null) Destroy(ring);
        }

        /// <summary>힐 팝업: 초록색 +N 텍스트 (기존 데미지 팝업 패턴 재사용)</summary>
        private void SpawnHealPopup(GoblinData goblin, int amount)
        {
            if (goblin.visualObject == null || hexGrid == null) return;

            Transform parent = hexGrid.GridContainer;
            RectTransform goblinRt = goblin.visualObject.GetComponent<RectTransform>();
            if (goblinRt == null) return;

            GameObject popup = new GameObject("HealPopup");
            popup.transform.SetParent(parent, false);
            RectTransform popupRt = popup.AddComponent<RectTransform>();
            popupRt.anchoredPosition = goblinRt.anchoredPosition + new Vector2(0f, 30f);
            popupRt.sizeDelta = new Vector2(60f, 30f);

            Text popupText = popup.AddComponent<Text>();
            popupText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            popupText.fontSize = 22;
            popupText.alignment = TextAnchor.MiddleCenter;
            popupText.color = new Color(0.2f, 0.9f, 0.3f, 1f); // 초록색
            popupText.raycastTarget = false;
            popupText.text = $"+{amount}";

            Outline outline = popup.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.3f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            StartCoroutine(AnimateHealPopup(popup));
        }

        private IEnumerator AnimateHealPopup(GameObject popup)
        {
            if (popup == null) yield break;
            RectTransform rt = popup.GetComponent<RectTransform>();
            Text txt = popup.GetComponent<Text>();
            Vector2 startPos = rt.anchoredPosition;
            float duration = 1.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (popup == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = startPos + new Vector2(0f, 30f * t);
                if (txt != null)
                {
                    Color c = txt.color;
                    c.a = 1f - t * t;
                    txt.color = c;
                }
                yield return null;
            }
            if (popup != null) Destroy(popup);
        }

        /// <summary>힐러 이동: 소환 영역 내에서만 인접 1칸 이동</summary>
        private HexCoord FindHealerMoveTarget(GoblinData healer, HashSet<HexCoord> occupiedCoords)
        {
            var dirs = HexCoord.Directions.OrderBy(x => Random.value).ToArray();
            foreach (var dir in dirs)
            {
                HexCoord target = healer.position + dir;
                if (occupiedCoords.Contains(target) && target != healer.position) continue;

                // 힐러는 소환 영역 상단 2줄 안에서만 이동 (rMin-2 ~ rMin-1)
                if (Mathf.Abs(target.q) <= hexGrid.GridRadius)
                {
                    int rMin = hexGrid.GetTopR(target.q);
                    if (target.r >= rMin - 2 && target.r < rMin)
                        return target;
                }
            }
            return healer.position;
        }

        private bool InstallGoblinBombOnBlock(GoblinData goblin)
        {
            if (hexGrid == null) return false;
            if (!hexGrid.IsInsideGrid(goblin.position)) return false;

            HexBlock block = hexGrid.GetBlock(goblin.position);
            if (block == null || block.Data == null) return false;
            if (block.Data.gemType == GemType.None) return false;
            if (block.Data.hasGoblinBomb) return false;

            block.Data.hasGoblinBomb = true;
            block.Data.goblinBombCountdown = 3;
            block.UpdateVisuals();
            StartCoroutine(BlockFlashEffect(block.gameObject, new Color(1f, 0.6f, 0.1f, 1f))); // 주황 플래시

            Debug.Log($"[GoblinSystem] 폭탄 설치: ({goblin.position.q}, {goblin.position.r}), 카운트다운=3");

            if (hexGrid != null)
                StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity * 0.5f, VisualConstants.ShakeSmallDuration * 0.5f));

            return true;
        }

        /// <summary>
        /// 매 턴 시작 시: 그리드 전체 블록 스캔 → hasGoblinBomb인 블록 카운트다운 감소.
        /// 0에 도달하면 폭발 (중심+6이웃 = 7칸 → 깨진 회색 블록).
        /// </summary>
        private IEnumerator DecrementBombCountdowns()
        {
            if (hexGrid == null) yield break;

            List<HexCoord> toExplode = new List<HexCoord>();

            // 그리드 전체 스캔
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (!block.Data.hasGoblinBomb) continue;

                block.Data.goblinBombCountdown--;
                block.UpdateVisuals(); // 카운트다운 텍스트 갱신

                if (block.Data.goblinBombCountdown <= 0)
                {
                    toExplode.Add(block.Coord);
                }
            }

            // 폭발 처리
            foreach (var coord in toExplode)
            {
                yield return StartCoroutine(ExplodeBombAt(coord));
            }
        }

        /// <summary>
        /// 폭탄 폭발: 중심 좌표 + 6 이웃 (7칸) → 깨진 회색(쉘) 블록으로 변환.
        /// </summary>
        private IEnumerator ExplodeBombAt(HexCoord center)
        {
            if (hexGrid == null) yield break;

            Debug.Log($"[GoblinSystem] 고블린 폭탄 폭발: ({center.q}, {center.r})");

            // 중심 블록의 폭탄 플래그 제거
            HexBlock centerBlock = hexGrid.GetBlock(center);
            if (centerBlock != null && centerBlock.Data != null)
            {
                centerBlock.Data.hasGoblinBomb = false;
                centerBlock.Data.goblinBombCountdown = 0;
            }

            // 대상 좌표: 중심 + 6방향 이웃
            List<HexCoord> targetCoords = new List<HexCoord>();
            if (hexGrid.IsInsideGrid(center)) targetCoords.Add(center);
            HexCoord[] neighbors = center.GetAllNeighbors();
            foreach (var n in neighbors)
            {
                if (hexGrid.IsInsideGrid(n))
                    targetCoords.Add(n);
            }

            // ── 1. 경고 플래시 ──
            Transform parent = hexGrid.GridContainer;
            float hexSize = hexGrid.HexSize;
            List<GameObject> warningFlashes = new List<GameObject>();

            foreach (var coord in targetCoords)
            {
                HexBlock block = hexGrid.GetBlock(coord);
                if (block == null) continue;
                RectTransform blockRt = block.GetComponent<RectTransform>();
                if (blockRt == null) continue;

                GameObject wf = new GameObject("GoblinBombFlash");
                wf.transform.SetParent(parent, false);
                RectTransform wrt = wf.AddComponent<RectTransform>();
                wrt.anchoredPosition = blockRt.anchoredPosition;
                wrt.sizeDelta = new Vector2(hexSize * 1.2f, hexSize * 1.2f);

                Image wimg = wf.AddComponent<Image>();
                wimg.sprite = HexBlock.GetHexFlashSprite();
                wimg.color = new Color(1f, 0.3f, 0.1f, 0f);
                wimg.raycastTarget = false;
                warningFlashes.Add(wf);
            }

            float warnDuration = 0.35f;
            float warnElapsed = 0f;
            while (warnElapsed < warnDuration)
            {
                warnElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(warnElapsed / warnDuration);
                float alpha = t < 0.4f
                    ? VisualConstants.EaseOutCubic(t / 0.4f) * 0.7f
                    : 0.7f * (1f - VisualConstants.EaseInQuad((t - 0.4f) / 0.6f));

                foreach (var wf in warningFlashes)
                {
                    if (wf == null) continue;
                    var img = wf.GetComponent<Image>();
                    if (img != null) img.color = new Color(1f, 0.3f, 0.1f, alpha);
                }
                yield return null;
            }
            foreach (var wf in warningFlashes)
                if (wf != null) Destroy(wf);

            // ── 2. 블록을 쉘로 변환 (순차) ──
            int converted = 0;
            foreach (var coord in targetCoords)
            {
                HexBlock block = hexGrid.GetBlock(coord);
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.isShell) continue;

                // 쉘로 변환 + 폭탄 제거
                block.Data.isShell = true;
                block.Data.isCracked = true;
                block.Data.specialType = SpecialBlockType.None;
                block.Data.tier = BlockTier.Normal;
                block.Data.hasGoblinBomb = false;
                block.Data.goblinBombCountdown = 0;
                block.UpdateVisuals();
                converted++;

                StartCoroutine(ShellConvertFlash(block));
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayShellConvertSound();

                yield return new WaitForSeconds(0.03f);
            }

            // ── 3. 화면 흔들림 + 사운드 ──
            if (hexGrid != null)
                StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity * 2f, VisualConstants.ShakeSmallDuration * 1.5f));
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            yield return new WaitForSeconds(0.15f);
            Debug.Log($"[GoblinSystem] 고블린 폭탄 폭발 완료: {converted}개 블록 → 쉘");
        }

        /// <summary>
        /// 그리드 전체 블록에서 hasGoblinBomb 제거 (초기화/정리용)
        /// </summary>
        private void CleanupAllGoblinBombs()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null)
                {
                    block.Data.hasGoblinBomb = false;
                    block.Data.goblinBombCountdown = 0;
                }
            }
        }

        // ============================================================
        // 도둑 고블린: 은신 + 수리검 교차 공격 시스템
        // ============================================================

        /// <summary>
        /// 특정 좌표에 은신 상태 도둑 고블린이 있는지 확인 (DrillBlockSystem 연동용)
        /// </summary>
        public bool IsGoblinStealthAt(HexCoord coord)
        {
            foreach (var g in goblins)
                if (g.isAlive && g.isThief && g.isStealth && g.position == coord)
                    return true;
            return false;
        }

        /// <summary>
        /// 은신 도둑 고블린 드릴 회피 연출: "회피" 팝업 + 좌우 이동 애니메이션.
        /// DrillBlockSystem에서 호출.
        /// </summary>
        public void PlayDrillDodgeEffect(HexCoord coord)
        {
            foreach (var g in goblins)
            {
                if (g.isAlive && g.isThief && g.isStealth && g.position == coord && g.visualObject != null)
                {
                    StartCoroutine(DodgeAnimation(g));
                    SpawnInfoPopup(g, "회피", Color.white);
                    g.stealthProtected = true; // 캐스케이드 동안 은신 보호
                    break;
                }
            }
        }

        /// <summary>
        /// GoblinData 직접 지정 버전 — exit phase에서 사용.
        /// </summary>
        public void PlayDrillDodgeEffect(GoblinData goblin)
        {
            if (goblin == null || !goblin.isAlive || goblin.visualObject == null) return;
            StartCoroutine(DodgeAnimation(goblin));
            SpawnInfoPopup(goblin, "회피", Color.white);
            goblin.stealthProtected = true; // 캐스케이드 동안 은신 보호
        }

        /// <summary>
        /// 턴 시작 시 드릴 회피 보호 플래그 초기화.
        /// </summary>
        public void ClearStealthProtection()
        {
            foreach (var g in goblins)
                g.stealthProtected = false;
        }

        /// <summary>
        /// 은신 상태 비주얼 갱신 — 외부(BlockRemovalSystem)에서 은신 해제 시 호출.
        /// 불투명도 복원 + "은신 해제!" 팝업.
        /// </summary>
        public void UpdateStealthVisual(GoblinData goblin)
        {
            if (goblin == null || goblin.visualObject == null) return;
            if (!goblin.isStealth)
            {
                // 은신 해제 → 원래 색상 복원
                if (goblin.goblinImage != null)
                {
                    Color bc = GetGoblinBaseColor(goblin);
                    goblin.goblinImage.color = new Color(bc.r, bc.g, bc.b, 1f);
                }
                SpawnInfoPopup(goblin, "은신 해제!", new Color(1f, 0.5f, 0.2f));
            }
            else
            {
                // 은신 진입 → 반투명
                if (goblin.goblinImage != null)
                {
                    Color bc = GetGoblinBaseColor(goblin);
                    goblin.goblinImage.color = new Color(bc.r, bc.g, bc.b, 0.35f);
                }
            }
        }

        /// <summary>
        /// 도둑 고블린 행동 페이즈: 은신 해제 → 2칸 이동 → 공격(수리검/은신)
        /// </summary>
        private IEnumerator ThiefGoblinPhase()
        {
            var thieves = goblins.FindAll(g => g.isAlive && g.isThief);
            if (thieves.Count == 0) yield break;

            foreach (var thief in thieves)
            {
                if (!thief.isAlive) continue;

                // 페이즈 전환: 공격B→A 전환 시 은신 해제
                if (thief.thiefAttackPhase == 0 && thief.isStealth)
                {
                    thief.isStealth = false;
                    // 불투명도 복원 (원래 색상)
                    if (thief.goblinImage != null)
                    {
                        Color bc = GetGoblinBaseColor(thief);
                        thief.goblinImage.color = new Color(bc.r, bc.g, bc.b, 1f);
                    }
                    SpawnInfoPopup(thief, "은신 해제!", new Color(1f, 0.5f, 0.2f));
                    yield return new WaitForSeconds(0.2f);
                }

                // 2칸 이동 (방문 이력 기반 이동 제한)
                yield return StartCoroutine(ThiefMove(thief, 2));

                if (!thief.isAlive) continue;

                // 공격 페이즈에 따라 행동
                if (thief.thiefAttackPhase == 0)
                {
                    // 공격A: 수리검 투척
                    yield return StartCoroutine(ThiefShurikenAttack(thief));
                }
                else
                {
                    // 공격B: 은신 진입 + 인접 블록 쉘화
                    thief.isStealth = true;
                    if (thief.goblinImage != null)
                    {
                        Color bc = GetGoblinBaseColor(thief);
                        thief.goblinImage.color = new Color(bc.r, bc.g, bc.b, 0.35f);
                    }
                    SpawnInfoPopup(thief, "은신!", new Color(0.5f, 0.3f, 0.8f));
                    yield return new WaitForSeconds(0.2f);

                    yield return StartCoroutine(ThiefStealthAttack(thief));
                }

                // 다음 턴에 페이즈 교체
                thief.thiefAttackPhase = (thief.thiefAttackPhase + 1) % 2;
            }
        }

        /// <summary>
        /// 도둑 고블린 이동: steps칸 이동
        /// - 필드 중간(midR)보다 위: 아래쪽 방향 우선 이동 (필드 중심으로 침투)
        /// - 필드 중간 도달: 인접 랜덤 이동 (방문 이력 기반)
        /// </summary>
        private IEnumerator ThiefMove(GoblinData thief, int steps)
        {
            if (thief == null || !thief.isAlive || thief.visualObject == null) yield break;

            // 6방향 (flat-top hex)
            HexCoord[] allDirs = new HexCoord[]
            {
                new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1),
                new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1)
            };

            // 아래쪽 방향 (r이 증가하는 3방향: ↙(0,1), ↓(-1,1), →(1,0)은 아래가 아님)
            // flat-top hex에서 아래로 가는 방향: (0,1), (-1,1), (1,0)은 수평
            // r이 증가 = 아래쪽: (0,1)=↓, (-1,1)=↙, 이 2방향이 순수 아래
            // r이 같거나 감소하지 않는 방향 중 아래쪽에 해당하는 것들
            HexCoord[] downwardDirs = new HexCoord[]
            {
                new HexCoord(0, 1),   // 아래
                new HexCoord(-1, 1),  // 왼쪽 아래
                new HexCoord(1, 0),   // 오른쪽 (약간 아래 경향)
            };

            // 다른 고블린 위치 집합
            var occupiedByOthers = new HashSet<HexCoord>();
            foreach (var g in goblins)
            {
                if (g == thief || !g.isAlive) continue;
                occupiedByOthers.Add(g.position);
                if (g.isHeavy && g.occupiedCoords != null)
                    foreach (var c in g.occupiedCoords)
                        occupiedByOthers.Add(c);
            }

            for (int step = 0; step < steps; step++)
            {
                if (!thief.isAlive || thief.visualObject == null) yield break;

                // ★ 현재 위치가 필드 중간보다 위인지 판별
                int topR = hexGrid.GetTopR(thief.position.q);
                int bottomR = Mathf.Min(hexGrid.GridRadius, -thief.position.q + hexGrid.GridRadius);
                int midR = (topR + bottomR) / 2;
                bool aboveMiddle = (thief.position.r < midR);

                // 유효 이동지 수집
                var validMoves = new List<HexCoord>();
                var preferredMoves = new List<HexCoord>(); // 아래쪽 방향 (중간 미도달 시 우선)

                foreach (var dir in allDirs)
                {
                    HexCoord next = new HexCoord(thief.position.q + dir.q, thief.position.r + dir.r);
                    if (!IsValidThiefPosition(next)) continue;
                    if (thief.thiefVisitedPositions.Contains(next)) continue;
                    if (occupiedByOthers.Contains(next)) continue;
                    validMoves.Add(next);

                    // 아래쪽 방향 체크 (r이 증가하는 이동)
                    if (next.r > thief.position.r)
                        preferredMoves.Add(next);
                }

                // 유효한 이동지가 없으면 방문 이력 초기화 후 재시도
                if (validMoves.Count == 0)
                {
                    thief.thiefVisitedPositions.Clear();
                    thief.thiefVisitedPositions.Add(thief.position);
                    preferredMoves.Clear();

                    foreach (var dir in allDirs)
                    {
                        HexCoord next = new HexCoord(thief.position.q + dir.q, thief.position.r + dir.r);
                        if (!IsValidThiefPosition(next)) continue;
                        if (occupiedByOthers.Contains(next)) continue;
                        validMoves.Add(next);

                        if (next.r > thief.position.r)
                            preferredMoves.Add(next);
                    }

                    if (validMoves.Count == 0) break; // 정말 이동 불가
                }

                // ★ 타겟 선택: 중간 미도달 → 아래쪽 우선, 중간 도달 → 전체 랜덤
                HexCoord target;
                if (aboveMiddle && preferredMoves.Count > 0)
                {
                    // 아래쪽 방향 중 랜덤 (필드 중심을 향해 침투)
                    target = preferredMoves[Random.Range(0, preferredMoves.Count)];
                }
                else
                {
                    // 중간 도달 후: 인접 랜덤 이동
                    target = validMoves[Random.Range(0, validMoves.Count)];
                }

                // 현재 위치 방문 기록에 추가
                if (!thief.thiefVisitedPositions.Contains(thief.position))
                    thief.thiefVisitedPositions.Add(thief.position);

                // 점유 업데이트
                occupiedByOthers.Remove(thief.position);
                thief.position = target;
                occupiedByOthers.Add(target);

                // 이동 애니메이션
                Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(target);
                yield return StartCoroutine(AnimateGoblinMove(thief, worldPos));
            }

            // 최종 위치도 방문 기록에 추가
            if (!thief.thiefVisitedPositions.Contains(thief.position))
                thief.thiefVisitedPositions.Add(thief.position);
        }

        /// <summary>
        /// 도둑 고블린 이동 가능 좌표 판별 (그리드 내부 + 소환 영역 상단 3줄)
        /// </summary>
        private bool IsValidThiefPosition(HexCoord coord)
        {
            if (hexGrid.IsValidCoord(coord)) return true;
            int gridRadius = hexGrid.GridRadius;
            if (coord.q >= -gridRadius && coord.q <= gridRadius)
            {
                int rMin = hexGrid.GetTopR(coord.q);
                if (coord.r >= rMin - 3 && coord.r < rMin)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 공격A: 수리검 투척 — 2개 다른 방향으로 2~4칸 떨어진 블록 공격
        /// </summary>
        private IEnumerator ThiefShurikenAttack(GoblinData thief)
        {
            if (!thief.isAlive || thief.visualObject == null || hexGrid == null) yield break;

            // ★ 1차 타겟: 깨진 블록 우선, 없으면 정상 블록
            HexBlock primaryTarget = FindShurikenTarget(thief.position, null);
            if (primaryTarget == null)
            {
                SpawnInfoPopup(thief, "공격 불가", Color.gray);
                yield break;
            }

            // ★ 2차 타겟: 1차 타겟의 인접 블록 (깨진 블록 우선)
            HexBlock secondaryTarget = FindShurikenNeighborTarget(primaryTarget.Coord, thief.position);

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDrillSound();

            // ★ 두 수리검 동시 발사
            Coroutine co1 = StartCoroutine(ShurikenProjectile(thief, primaryTarget.Coord, primaryTarget));
            Coroutine co2 = null;
            if (secondaryTarget != null)
                co2 = StartCoroutine(ShurikenProjectile(thief, secondaryTarget.Coord, secondaryTarget));

            yield return co1;
            if (co2 != null) yield return co2;
        }

        /// <summary>
        /// 수리검 1차 타겟 탐색: 깨진 블록 우선 → 정상 블록 폴백
        /// 도둑 주변 2~4칸 범위, 6방향 스캔
        /// </summary>
        private HexBlock FindShurikenTarget(HexCoord origin, HexCoord? excludeCoord)
        {
            HexCoord[] allDirs = new HexCoord[]
            {
                new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1),
                new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1)
            };

            var crackedCandidates = new List<HexBlock>();
            var normalCandidates = new List<HexBlock>();

            foreach (var dir in allDirs)
            {
                for (int dist = 2; dist <= 4; dist++)
                {
                    HexCoord coord = new HexCoord(
                        origin.q + dir.q * dist,
                        origin.r + dir.r * dist
                    );
                    if (excludeCoord.HasValue && coord.q == excludeCoord.Value.q && coord.r == excludeCoord.Value.r)
                        continue;
                    if (!hexGrid.IsValidCoord(coord)) continue;

                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                    if (block.Data.isShell) continue;

                    if (block.Data.isCracked)
                        crackedCandidates.Add(block);
                    else
                        normalCandidates.Add(block);
                }
            }

            // 깨진 블록 우선, 없으면 정상 블록
            if (crackedCandidates.Count > 0)
                return crackedCandidates[Random.Range(0, crackedCandidates.Count)];
            if (normalCandidates.Count > 0)
                return normalCandidates[Random.Range(0, normalCandidates.Count)];
            return null;
        }

        /// <summary>
        /// 수리검 2차 타겟: 1차 타겟의 인접 6칸에서 깨진 블록 우선 → 정상 블록 폴백
        /// </summary>
        private HexBlock FindShurikenNeighborTarget(HexCoord primaryCoord, HexCoord thiefPos)
        {
            HexCoord[] neighborDirs = new HexCoord[]
            {
                new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1),
                new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1)
            };

            var crackedNeighbors = new List<HexBlock>();
            var normalNeighbors = new List<HexBlock>();

            foreach (var dir in neighborDirs)
            {
                HexCoord coord = new HexCoord(primaryCoord.q + dir.q, primaryCoord.r + dir.r);
                // 도둑 자신의 위치 제외
                if (coord.q == thiefPos.q && coord.r == thiefPos.r) continue;
                if (!hexGrid.IsValidCoord(coord)) continue;

                HexBlock block = hexGrid.GetBlock(coord);
                if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                if (block.Data.isShell) continue;

                if (block.Data.isCracked)
                    crackedNeighbors.Add(block);
                else
                    normalNeighbors.Add(block);
            }

            if (crackedNeighbors.Count > 0)
                return crackedNeighbors[Random.Range(0, crackedNeighbors.Count)];
            if (normalNeighbors.Count > 0)
                return normalNeighbors[Random.Range(0, normalNeighbors.Count)];
            return null;
        }

        /// <summary>
        /// 수리검 투사체: 궤적 라인 + 고속 이동 + 데미지 처리
        /// </summary>
        private IEnumerator ShurikenProjectile(GoblinData thief, HexCoord targetCoord, HexBlock targetBlock)
        {
            if (thief.visualObject == null || hexGrid == null) yield break;

            Transform parent = hexGrid.GridContainer;
            Vector2 startPos = hexGrid.CalculateFlatTopHexPosition(thief.position);
            Vector2 endPos = hexGrid.CalculateFlatTopHexPosition(targetCoord);

            // ★ 궤적 라인 (도둑→타겟 직선, 0.1초 후 페이드 소멸)
            StartCoroutine(ShurikenTrailLine(parent, startPos, endPos));

            // 수리검 이펙트 생성 (회전하는 다이아몬드)
            GameObject shuriken = new GameObject("ThiefShuriken");
            shuriken.transform.SetParent(parent, false);

            RectTransform srt = shuriken.AddComponent<RectTransform>();
            float shurikenSize = hexGrid.HexSize * 0.5f;
            srt.sizeDelta = new Vector2(shurikenSize, shurikenSize);
            srt.anchoredPosition = startPos;

            Image simg = shuriken.AddComponent<Image>();
            simg.color = new Color(0.75f, 0.75f, 0.8f, 1f);
            simg.raycastTarget = false;

            // ★ 비행 애니메이션 (0.15초 — 기존 0.3초의 2배 속도)
            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (shuriken == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                srt.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
                srt.localEulerAngles = new Vector3(0, 0, elapsed * 2160f); // 회전도 2배 빠르게
                yield return null;
            }

            // 히트 이펙트
            if (shuriken != null)
                Destroy(shuriken);

            // 블록 데미지 적용
            if (targetBlock != null && targetBlock.Data != null && targetBlock.Data.gemType != GemType.None)
            {
                // ★ 특수 블록 공격 시: 발동 없이 기능 파괴 + 크랙 (MoveBlock/FixedBlock 제외)
                if (targetBlock.Data.specialType != SpecialBlockType.None
                    && targetBlock.Data.specialType != SpecialBlockType.MoveBlock
                    && targetBlock.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    var removedType = targetBlock.Data.specialType;
                    targetBlock.Data.specialType = SpecialBlockType.None;
                    targetBlock.Data.isCracked = true;
                    targetBlock.UpdateVisuals();
                    Debug.Log($"[도둑수리검] 특수 블록 파괴: ({targetCoord}) {removedType} → 크랙");
                }
                else if (targetBlock.Data.isCracked)
                {
                    // 금간 블록 → 쉘 블록
                    ConvertToShellBlock(targetBlock);
                    Debug.Log($"[도둑수리검] 금간 블록 → 쉘: ({targetCoord})");
                }
                else if (!targetBlock.Data.isShell)
                {
                    // 정상 블록 → 금간
                    targetBlock.Data.isCracked = true;
                    targetBlock.UpdateVisuals();
                    Debug.Log($"[도둑수리검] 블록 금간: ({targetCoord})");
                }

                // 히트 플래시 이펙트
                if (targetBlock.gameObject != null)
                    StartCoroutine(BlockFlashEffect(targetBlock.gameObject, new Color(0.8f, 0.8f, 0.9f, 1f)));
            }
        }

        /// <summary>
        /// 수리검 궤적 라인: 도둑→타겟 직선을 즉시 표시 후 0.1초간 페이드 소멸
        /// </summary>
        private IEnumerator ShurikenTrailLine(Transform parent, Vector2 startPos, Vector2 endPos)
        {
            GameObject lineObj = new GameObject("ShurikenTrail");
            lineObj.transform.SetParent(parent, false);

            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(0.75f, 0.75f, 0.85f, 0.7f);
            lineImg.raycastTarget = false;

            RectTransform lineRt = lineObj.GetComponent<RectTransform>();
            Vector2 mid = (startPos + endPos) * 0.5f;
            lineRt.anchoredPosition = mid;

            float dist = Vector2.Distance(startPos, endPos);
            lineRt.sizeDelta = new Vector2(dist, 2.5f); // 얇은 궤적선

            float angle = Mathf.Atan2(endPos.y - startPos.y, endPos.x - startPos.x) * Mathf.Rad2Deg;
            lineRt.localRotation = Quaternion.Euler(0f, 0f, angle);

            // 0.1초간 페이드 아웃
            float fadeTime = 0.1f;
            float fadeElapsed = 0f;
            Color startColor = lineImg.color;
            while (fadeElapsed < fadeTime)
            {
                if (lineObj == null) yield break;
                fadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(fadeElapsed / fadeTime);
                lineImg.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
                yield return null;
            }

            if (lineObj != null) Destroy(lineObj);
        }

        /// <summary>
        /// 공격B: 은신 공격 — 인접 블록 중 하나를 쉘로 변환
        /// 연출: 원본 블록 훔쳐가기(축소+이동) → 회색 쉘 블록 놓기(이동+확대)
        /// </summary>
        private IEnumerator ThiefStealthAttack(GoblinData thief)
        {
            if (!thief.isAlive || hexGrid == null) yield break;

            // 6방향 인접 블록 탐색
            HexCoord[] allDirs = new HexCoord[]
            {
                new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1),
                new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1)
            };

            var validTargets = new List<HexBlock>();
            foreach (var dir in allDirs)
            {
                HexCoord adjCoord = new HexCoord(thief.position.q + dir.q, thief.position.r + dir.r);
                if (!hexGrid.IsValidCoord(adjCoord)) continue;
                HexBlock block = hexGrid.GetBlock(adjCoord);
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.isShell) continue;
                validTargets.Add(block);
            }

            if (validTargets.Count == 0)
            {
                SpawnInfoPopup(thief, "공격 불가", Color.gray);
                yield break;
            }

            // 랜덤 선택
            HexBlock target = validTargets[Random.Range(0, validTargets.Count)];
            Debug.Log($"[도둑은신] 인접 블록 쉘화: ({target.Coord})");

            // 도둑 & 블록 위치 계산
            Vector2 thiefPos = hexGrid.CalculateFlatTopHexPosition(thief.position);
            Vector2 blockPos = hexGrid.CalculateFlatTopHexPosition(target.Coord);
            Transform parent = hexGrid.transform;

            // 원본 블록 색상 캡처 (훔쳐가는 복제 이미지용)
            Color originalColor = Color.white;
            Sprite hexSprite = HexBlock.GetHexFlashSprite();
            if (target.Data != null)
                originalColor = GemColors.GetColor(target.Data.gemType);

            // ========== Phase 1: 블록 훔쳐가기 (원본 축소 → 도둑 쪽으로 이동) ==========
            // 원본 블록 위치에 복제 이미지 생성 (원본은 즉시 숨김)
            GameObject stolenGem = new GameObject("ThiefStolen");
            stolenGem.transform.SetParent(parent, false);
            RectTransform stolenRt = stolenGem.AddComponent<RectTransform>();
            stolenRt.anchoredPosition = blockPos;
            stolenRt.sizeDelta = new Vector2(50f, 50f);
            stolenRt.localScale = Vector3.one;
            UnityEngine.UI.Image stolenImg = stolenGem.AddComponent<UnityEngine.UI.Image>();
            stolenImg.sprite = hexSprite;
            stolenImg.color = originalColor;
            stolenImg.raycastTarget = false;

            // 원본 블록 비주얼 숨김 (데이터는 아직 유지)
            if (target.gameObject != null)
            {
                var bgImg = target.gameObject.transform.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
                var gemImg = target.gameObject.transform.Find("GemImage")?.GetComponent<UnityEngine.UI.Image>();
                if (bgImg != null) bgImg.enabled = false;
                if (gemImg != null) gemImg.enabled = false;
            }

            // 애니메이션: blockPos → thiefPos + 축소 (0.2초)
            float stealDur = 0.2f;
            float elapsed = 0f;
            while (elapsed < stealDur)
            {
                if (stolenRt == null) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stealDur);
                float ease = t * t; // ease-in
                stolenRt.anchoredPosition = Vector2.Lerp(blockPos, thiefPos, ease);
                float scale = Mathf.Lerp(1f, 0.3f, ease);
                stolenRt.localScale = new Vector3(scale, scale, 1f);
                stolenImg.color = new Color(originalColor.r, originalColor.g, originalColor.b, Mathf.Lerp(1f, 0.5f, ease));
                yield return null;
            }

            // 복제 이미지 제거
            if (stolenGem != null) Object.Destroy(stolenGem);

            // 짧은 대기 (도둑이 블록을 든 상태)
            yield return new WaitForSeconds(0.08f);

            // ========== Phase 2: 회색 쉘 블록 놓기 (도둑 → 원래 위치 + 확대) ==========
            Color shellGray = new Color(0.52f, 0.50f, 0.48f, 1f);

            GameObject shellGem = new GameObject("ThiefShellPlace");
            shellGem.transform.SetParent(parent, false);
            RectTransform shellRt = shellGem.AddComponent<RectTransform>();
            shellRt.anchoredPosition = thiefPos;
            shellRt.sizeDelta = new Vector2(50f, 50f);
            shellRt.localScale = new Vector3(0.3f, 0.3f, 1f);
            UnityEngine.UI.Image shellImg = shellGem.AddComponent<UnityEngine.UI.Image>();
            shellImg.sprite = hexSprite;
            shellImg.color = new Color(shellGray.r, shellGray.g, shellGray.b, 0.5f);
            shellImg.raycastTarget = false;

            // 애니메이션: thiefPos → blockPos + 확대 (0.2초)
            float placeDur = 0.2f;
            elapsed = 0f;
            while (elapsed < placeDur)
            {
                if (shellRt == null) break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / placeDur);
                float ease = 1f - (1f - t) * (1f - t); // ease-out
                shellRt.anchoredPosition = Vector2.Lerp(thiefPos, blockPos, ease);
                float scale = Mathf.Lerp(0.3f, 1f, ease);
                shellRt.localScale = new Vector3(scale, scale, 1f);
                shellImg.color = new Color(shellGray.r, shellGray.g, shellGray.b, Mathf.Lerp(0.5f, 1f, ease));
                yield return null;
            }

            // 복제 이미지 제거
            if (shellGem != null) Object.Destroy(shellGem);

            // ========== Phase 3: 실제 쉘 변환 + 비주얼 복원 ==========
            ConvertToShellBlock(target);

            // 놓기 완료 플래시 이펙트
            if (target.gameObject != null)
                StartCoroutine(BlockFlashEffect(target.gameObject, new Color(0.5f, 0.3f, 0.8f, 1f)));

            yield return new WaitForSeconds(0.1f);
        }
    }
}

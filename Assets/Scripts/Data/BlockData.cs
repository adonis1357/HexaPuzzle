using UnityEngine;

// ============================================================================
// BlockData.cs - 게임 데이터 정의 파일
// ============================================================================
// 이 파일은 게임에서 사용되는 모든 "종류"와 "상태"를 정의합니다.
// 쉽게 말해, 게임 속 블록이 어떤 색상인지, 어떤 특수 능력이 있는지,
// 적군이 어떤 종류인지 등을 분류하는 "카탈로그" 역할을 합니다.
//
// 주요 내용:
// - GemType: 보석(블록) 색상 종류 (빨강, 파랑, 초록 등)
// - SpecialBlockType: 특수 블록 종류 (드릴, 폭탄 등)
// - BlockTier: 블록 등급 (매칭할수록 등급이 올라감)
// - EnemyType: 적군 종류 (색상도둑, 사슬 등)
// - BlockData: 한 블록이 가진 모든 정보를 담는 데이터 클래스
// - GemTypeHelper: 랜덤 보석 생성 도우미
// - GemColors: 보석 색상에 대응하는 실제 RGB 색상값
// ============================================================================

namespace JewelsHexaPuzzle.Data
{
    /// <summary>
    /// 게임 모드 - 게임을 어떤 방식으로 플레이할지 결정
    /// </summary>
    public enum GameMode
    {
        Stage,      // 스테이지 모드: 정해진 미션을 클리어하며 진행
        Infinite    // 무한 모드: 끝없이 점수를 쌓는 모드
    }

    /// <summary>
    /// 스테이지 난이도 - 쉬움/보통/어려움 3단계
    /// </summary>
    public enum DifficultyType
    {
        Easy = 0,    // 쉬움 (초록 별 1개)
        Normal = 1,  // 보통 (노란 별 2개)
        Hard = 2     // 어려움 (빨간 별 3개)
    }

    /// <summary>
    /// 보석(젬) 타입 - 블록의 색상 종류
    /// 퍼즐 게임에서 같은 색상 3개를 모아 매칭하는 기본 단위입니다.
    /// 기본 5색(빨~보라) + 확장 6색(주황~자수정) + 특수 1색(회색)으로 구성됩니다.
    /// </summary>
    public enum GemType
    {
        None = 0,       // 빈 칸 (블록이 없는 상태)
        Red = 1,        // 빨간색 보석
        Blue = 2,       // 파란색 보석
        Green = 3,      // 초록색 보석
        Yellow = 4,     // 노란색 보석
        Purple = 5,     // 보라색 보석
        Orange = 6,     // 주황색 보석 (확장 색상, 난이도 높은 스테이지에서 등장)
        Ruby = 7,       // 루비 (빨간색 상위 등급)
        Emerald = 8,    // 에메랄드 (초록색 상위 등급)
        Sapphire = 9,   // 사파이어 (파란색 상위 등급)
        Amber = 10,     // 앰버 (노란색 상위 등급)
        Amethyst = 11,  // 아메시스트 (보라색 상위 등급)
        Gray = 12       // 회색 (적군 "색상도둑"에 의해 변환된 블록, 매칭 불가)
    }

    /// <summary>
    /// 특수 블록 타입 - 블록에 부여되는 특수 능력의 종류
    /// 여러 블록을 한꺼번에 매칭하면 특수 블록이 생성됩니다.
    /// 특수 블록을 터치(클릭)하면 강력한 효과가 발동됩니다.
    /// </summary>
    public enum SpecialBlockType
    {
        None,       // 일반 블록 (특수 능력 없음)
        MoveBlock,  // 이동 블록 (회전 시 함께 이동하는 블록)
        FixedBlock, // 고정 블록 (회전할 수 없는 블록, 장애물 역할)
        TimeBomb,   // 시한폭탄 (일정 턴 내에 제거하지 않으면 게임 오버)
        Drill,      // 드릴 (4개 매칭 시 생성, 한 방향 직선으로 블록을 뚫고 파괴)
        Bomb,       // 폭탄 (5개 이상 매칭 시 생성, 주변 블록을 원형으로 폭파)
        Rainbow,    // 무지개/도넛 (7개 이상 매칭 시 생성, 같은 색상 블록 전체 파괴)
        XBlock,     // X블록 (링 모양 매칭 시 생성, 같은 색상 전체 파괴)
        Drone       // 드론 (5개 직선 매칭 시 생성, 우선순위 기반 단일 타격)
    }

    /// <summary>
    /// 드릴 방향 - 드릴 블록이 발사되는 방향
    /// 육각형 그리드에서 3가지 축 방향 중 하나를 따라 뚫고 지나갑니다.
    /// </summary>
    public enum DrillDirection
    {
        Vertical,   // 세로 방향 (위아래, r축)
        Slash,      // 슬래시 방향 (/ 대각선, s축)
        BackSlash   // 백슬래시 방향 (\ 대각선, q축)
    }

    /// <summary>
    /// 적군 타입 - 플레이어를 방해하는 적군 종류
    /// 각 적군은 고유한 방해 능력을 가지고 있으며,
    /// 특정 조건을 만족해야 처치할 수 있습니다.
    /// </summary>
    public enum EnemyType
    {
        None = -1,              // 적군 없음 (일반 블록)
        Chromophage = 0,        // 색상도둑: 주변 블록을 회색으로 바꿔 매칭 불가로 만듦 | 별칭: 그레이
        ChainAnchor = 1,        // 속박의 사슬: 블록을 묶어 이동 불가로 만듦 | 별칭: 사슬
        ThornParasite = 2,      // 가시 기생충: 매칭 시 주변에 피해를 줌 | 별칭: 가시
        Divider = 3,            // 분열체: 파괴되면 2개로 분열됨 | 별칭: 분열
        GravityWarper = 4,      // 중력왜곡자: 블록 낙하를 방해함 (고정 효과) | 별칭: 중력
        ReflectionShield = 5,   // 반사방패: 보호막이 있어 여러 번 공격해야 파괴됨 | 별칭: 방패
        TimeFreezer = 6,        // 시간동결자: 특정 블록의 움직임을 얼림 | 별칭: 동결
        ResonanceTwin = 7,      // 공명쌍둥이: 2개가 쌍으로 존재, 동시에 파괴해야 처치 | 별칭: 쌍둥이
        ShadowSpore = 8,        // 그림자포자: 시간이 지나면 주변으로 번짐 | 별칭: 포자
        ChaosOverlord = 9,      // 카오스 군주: 여러 적군 능력을 동시에 사용하는 최종 보스 | 별칭: 카오스
        Goblin = 10,            // 고블린: 빈 공간에서 소환, 블록을 공격해 금간 블록으로 만듦 | 별칭: 고블린
        ArmoredGoblin = 11,     // 갑옷 고블린: HP가 높고 갑옷으로 무장한 고블린 | 별칭: 갑옷
        ArcherGoblin = 12,      // 활 고블린: 상단 고정 배치, 매턴 화살로 블록에 크랙 | 별칭: 활
        ShieldGoblin = 13,      // 방패 고블린: 드릴 차단 방패 장착, 3회 차단 후 파괴 | 별칭: 방패고블린
        BombGoblin = 14,        // 폭탄 고블린: 카운트다운 후 광역 폭발, 블록 대량 파괴 | 별칭: 폭탄고블린
        HealerGoblin = 15,     // 힐러 고블린: 피해 입은 아군 회복, 소환 영역에서 활동 | 별칭: 힐러
        HeavyGoblin = 16,      // 헤비급 고블린: 3블록 삼각형 점유, HP 36, 회전 불가 | 별칭: 헤비
        ThiefGoblin = 17,      // 도둑 고블린: 은신하며 블록을 훔치는 고블린 | 별칭: 도둑
        WizardGoblin = 18      // 마법사 고블린: 마법으로 블록을 변환하는 고블린 | 별칭: 마법사
    }

    /// <summary>
    /// 적군 이름 변환 도우미 - 적군 타입을 한글 이름으로 바꿔주는 유틸리티.
    /// 내부적으로 EnemyRegistry에 위임합니다. (하위 호환용 래퍼)
    /// </summary>
    public static class EnemyTypeHelper
    {
        /// <summary>
        /// 적군의 정식 명칭을 반환합니다.
        /// 예: Chromophage → "색상도둑"
        /// </summary>
        public static string GetName(EnemyType type)
        {
            return EnemyRegistry.GetName(type);
        }

        /// <summary>
        /// 적군의 별칭(짧은 이름)을 반환합니다.
        /// UI 공간이 부족할 때 사용하는 축약 이름입니다.
        /// 예: Chromophage → "그레이"
        /// </summary>
        public static string GetAlias(EnemyType type)
        {
            return EnemyRegistry.GetAlias(type);
        }
    }

    /// <summary>
    /// 카오스 효과 - 카오스 군주(최종 보스)가 동시에 사용할 수 있는 능력 조합
    /// [Flags] 속성으로 여러 효과를 동시에 가질 수 있습니다.
    /// 예: Chromophage | ChainAnchor = 색상도둑 + 사슬 능력 동시 사용
    /// </summary>
    [System.Flags]
    public enum ChaosEffect
    {
        None = 0,                   // 효과 없음
        Chromophage = 1 << 0,       // 색상도둑 능력 (블록을 회색으로 변환)
        ChainAnchor = 1 << 1,       // 사슬 능력 (블록 이동 불가)
        GravityWarper = 1 << 2,     // 중력왜곡 능력 (낙하 방해)
        ReflectionShield = 1 << 3,  // 반사방패 능력 (보호막)
        TimeFreezer = 1 << 4        // 시간동결 능력 (움직임 동결)
    }

    /// <summary>
    /// 제거 방법 - 블록이 어떤 방식으로 제거되었는지 기록
    /// 적군 처치 조건이나 미션 달성 판정에 사용됩니다.
    /// </summary>
    public enum RemovalMethod
    {
        Match,          // 일반 매칭으로 제거 (같은 색 3개 맞추기)
        SpecialBasic,   // 기본 특수 블록으로 제거 (드릴)
        SpecialAdvanced,// 고급 특수 블록으로 제거 (폭탄)
        Donut,          // 도넛(무지개) 블록으로 제거 (같은 색 전체 삭제)
        Cascade         // 캐스케이드 연쇄로 제거 (자동 연쇄 반응)
    }

    /// <summary>
    /// 제거 조건 - 적군을 처치할 때 필요한 선행 조건
    /// 일부 적군은 특정 조건을 먼저 만족해야 처치할 수 있습니다.
    /// </summary>
    public enum RemovalCondition
    {
        Normal,         // 일반 제거 (조건 없이 바로 처치 가능)
        ChainBroken,    // 체인 해제 후 제거 (먼저 사슬을 풀어야 함)
        ShieldBroken,   // 방패 파괴 후 제거 (먼저 보호막을 깨야 함)
        TwinPaired,     // 쌍둥이 동시 제거 (한 쌍을 동시에 파괴해야 함)
        ChaosWeakened   // 카오스 다회 공격 후 제거 (여러 번 때려야 함)
    }

    /// <summary>
    /// 적군 처치 데이터 - 적군이 어떻게 처치되었는지 기록하는 구조체
    /// 미션 시스템에서 "OO 적군을 XX 방법으로 처치하라" 같은 미션 판정에 사용됩니다.
    /// </summary>
    public struct EnemyKillData
    {
        public EnemyType enemyType;         // 처치된 적군의 종류
        public RemovalMethod method;        // 어떤 방법으로 처치했는지
        public RemovalCondition condition;  // 어떤 조건 하에 처치했는지
    }

    /// <summary>
    /// 블록 등급 - 같은 색상 블록을 반복 매칭하면 등급이 올라감
    /// 등급이 높을수록 더 많은 점수를 획득하며, 시각적으로도 화려해집니다.
    /// Normal → Tier1 → Tier2 → Tier3 → ProcessedGem (최종 가공 보석)
    /// </summary>
    public enum BlockTier
    {
        Normal = 0,         // 일반 등급 (처음 생성된 상태)
        Tier1 = 1,          // 1등급 (한 번 매칭에 참여한 블록)
        Tier2 = 2,          // 2등급 (두 번 매칭에 참여한 블록)
        Tier3 = 3,          // 3등급 (세 번 매칭에 참여한 블록)
        ProcessedGem = 4    // 최종 가공 보석 (최고 등급, 최대 점수)
    }

    /// <summary>
    /// 블록 데이터 - 게임판 위 한 칸(블록)이 가진 모든 정보를 담는 클래스
    ///
    /// 블록 하나의 "신분증"이라고 생각하면 됩니다.
    /// 색상, 특수 능력, 등급, 적군 정보 등 블록의 모든 상태를 기록합니다.
    ///
    /// [System.Serializable] 속성 덕분에 Unity 인스펙터에서 직접 편집할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class BlockData
    {
        // ── 기본 속성 ──
        public GemType gemType;                 // 보석 색상 (빨강, 파랑 등)
        public SpecialBlockType specialType;    // 특수 블록 종류 (드릴, 폭탄 등, 없으면 None)
        public int timeBombCount;               // 시한폭탄 남은 턴 수 (0이 되면 폭발)
        public int vinylLayer;                  // 비닐 레이어 수 (블록을 덮고 있는 장애물 겹 수)
        public bool hasChain;                   // 사슬에 묶여있는지 (true면 이동 불가)
        public bool hasThorn;                   // 가시가 있는지 (true면 매칭 시 주변에 피해)
        public DrillDirection drillDirection;   // 드릴 발사 방향 (세로/슬래시/백슬래시)
        public BlockTier tier;                  // 블록 등급 (매칭 참여 횟수에 따라 상승)
        public bool pendingActivation;          // 활성화 대기 중 (특수 블록이 곧 발동될 예정인지)
        public bool isCracked;                  // 깨진 블록 여부 (고블린 공격으로 금간 상태)
        public bool isShell;                    // 껍데기 블록 여부 (두 번째 공격으로 테두리만 남은 상태, 매칭 불가, 낙하 장애물)

        // ── 고블린 폭탄 시스템 필드 ──
        public bool hasGoblinBomb;              // 고블린 폭탄이 설치된 블록인지
        public int goblinBombCountdown;         // 폭탄 카운트다운 (0이면 폭발)

        // ── 적군 시스템 필드 ──
        public EnemyType enemyType;             // 이 블록에 붙어있는 적군 종류
        public int enemyShieldCount;            // 반사방패 내구도 (남은 보호막 횟수)
        public int enemySpreadTimer;            // 그림자포자 확산까지 남은 턴 수 (기본 3턴)
        public int enemyTwinId;                 // 공명쌍둥이 짝 ID (-1이면 짝 없음)
        public ChaosEffect chaosEffectMask;     // 카오스 군주가 사용 중인 능력 조합 (비트 플래그)
        public int chaosHitCount;               // 카오스 군주 누적 피격 횟수

        /// <summary>
        /// 기본 생성자 - 빈 블록(아무것도 없는 상태)을 생성합니다.
        /// 모든 값을 초기 상태로 설정합니다.
        /// </summary>
        public BlockData()
        {
            gemType = GemType.None;
            specialType = SpecialBlockType.None;
            timeBombCount = 0;
            vinylLayer = 0;
            hasChain = false;
            hasThorn = false;
            drillDirection = DrillDirection.Vertical;
            tier = BlockTier.Normal;
            enemyType = EnemyType.None;
            enemyShieldCount = 0;
            enemySpreadTimer = 3;
            enemyTwinId = -1;
            chaosEffectMask = ChaosEffect.None;
            chaosHitCount = 0;
        }

        /// <summary>
        /// 색상 지정 생성자 - 특정 색상의 블록을 생성합니다.
        /// 안전장치: 회색(Gray)이 입력되면 자동으로 랜덤 색상으로 변환합니다.
        /// (회색은 적군 전용 색상이므로 일반 블록에 사용하면 안 됩니다)
        /// </summary>
        public BlockData(GemType type)
        {
            // 회색 블록 방지: 무작위 기본 색상으로 변환 (GemTypeHelper 통일)
            if (type == GemType.Gray)
            {
                Debug.LogWarning("[BlockData] 회색 블록 생성 감지! GemTypeHelper.GetRandom()으로 변환");
                gemType = GemTypeHelper.GetRandom();
            }
            else
            {
                gemType = type;
            }

            specialType = SpecialBlockType.None;
            timeBombCount = 0;
            vinylLayer = 0;
            hasChain = false;
            hasThorn = false;
            drillDirection = DrillDirection.Vertical;
            tier = BlockTier.Normal;
            enemyType = EnemyType.None;
            enemyShieldCount = 0;
            enemySpreadTimer = 3;
            enemyTwinId = -1;
            chaosEffectMask = ChaosEffect.None;
            chaosHitCount = 0;
        }

        /// <summary>
        /// 복제 - 이 블록의 모든 데이터를 똑같이 복사한 새 BlockData를 만듭니다.
        /// 원본을 수정하지 않고 사본을 만들고 싶을 때 사용합니다.
        /// 주의: 복제 시 Gray 필터를 우회하여 원본 gemType을 그대로 보존합니다.
        /// (적군 블록의 Gray 색상이 랜덤 색상으로 변경되는 버그 방지)
        /// </summary>
        public BlockData Clone()
        {
            // 기본 생성자로 생성 후 gemType을 직접 대입 (Gray 필터 우회)
            BlockData cloned = new BlockData();
            cloned.gemType = this.gemType;
            cloned.specialType = this.specialType;
            cloned.timeBombCount = this.timeBombCount;
            cloned.vinylLayer = this.vinylLayer;
            cloned.hasChain = this.hasChain;
            cloned.hasThorn = this.hasThorn;
            cloned.drillDirection = this.drillDirection;
            cloned.tier = this.tier;
            cloned.pendingActivation = this.pendingActivation;
            cloned.enemyType = this.enemyType;
            cloned.enemyShieldCount = this.enemyShieldCount;
            cloned.enemySpreadTimer = this.enemySpreadTimer;
            cloned.enemyTwinId = this.enemyTwinId;
            cloned.chaosEffectMask = this.chaosEffectMask;
            cloned.chaosHitCount = this.chaosHitCount;
            cloned.isCracked = this.isCracked;
            cloned.isShell = this.isShell;
            cloned.hasGoblinBomb = this.hasGoblinBomb;
            cloned.goblinBombCountdown = this.goblinBombCountdown;
            return cloned;
        }

        /// <summary>
        /// 이 블록에 적군이 붙어있는지 확인합니다.
        /// </summary>
        public bool HasEnemy()
        {
            return enemyType != EnemyType.None;
        }

        /// <summary>
        /// 이 블록이 중력에 고정되어 낙하하지 않는지 확인합니다.
        /// 중력왜곡자이거나, 카오스 군주가 중력왜곡 능력을 사용 중이면 true입니다.
        /// </summary>
        public bool IsGravityAnchored()
        {
            return enemyType == EnemyType.GravityWarper ||
                   (enemyType == EnemyType.ChaosOverlord && (chaosEffectMask & ChaosEffect.GravityWarper) != 0);
        }

        /// <summary>
        /// 이 블록에 보호막이 남아있는지 확인합니다.
        /// 반사방패이거나 카오스 군주의 방패 능력이 활성화된 경우,
        /// 보호막 횟수(enemyShieldCount)가 0보다 크면 true입니다.
        /// </summary>
        public bool HasShield()
        {
            return (enemyType == EnemyType.ReflectionShield && enemyShieldCount > 0) ||
                   (enemyType == EnemyType.ChaosOverlord && (chaosEffectMask & ChaosEffect.ReflectionShield) != 0 && enemyShieldCount > 0);
        }

        /// <summary>
        /// 이 블록이 특수 블록인지 확인합니다. (드릴, 폭탄 등)
        /// </summary>
        public bool IsSpecial()
        {
            return specialType != SpecialBlockType.None;
        }

        /// <summary>드릴 블록인지 확인</summary>
        public bool IsDrill()
        {
            return specialType == SpecialBlockType.Drill;
        }

        /// <summary>도넛(무지개) 블록인지 확인</summary>
        public bool IsDonut()
        {
            return specialType == SpecialBlockType.Rainbow;
        }

        /// <summary>X블록인지 확인</summary>
        public bool IsXBlock()
        {
            return specialType == SpecialBlockType.XBlock;
        }

        /// <summary>폭탄 블록인지 확인</summary>
        public bool IsBomb()
        {
            return specialType == SpecialBlockType.Bomb;
        }

        /// <summary>드론 블록인지 확인</summary>
        public bool IsDrone()
        {
            return specialType == SpecialBlockType.Drone;
        }

        /// <summary>
        /// 이 블록이 이동(회전) 가능한지 확인합니다.
        /// 고정 블록이거나 사슬에 묶여있으면 이동할 수 없습니다.
        /// </summary>
        public bool CanMove()
        {
            return specialType != SpecialBlockType.FixedBlock && !hasChain;
        }
    }

    /// <summary>
    /// 보석 타입 도우미 - 랜덤 보석 색상을 생성하는 유틸리티 클래스
    ///
    /// 새 블록을 채울 때 "어떤 색상으로 만들지" 결정하는 데 사용됩니다.
    /// ActiveGemTypeCount 값을 바꾸면 게임에 등장하는 색상 수를 조절할 수 있습니다.
    /// (색상이 많을수록 매칭이 어려워져 난이도가 올라갑니다)
    /// </summary>
    public static class GemTypeHelper
    {
        /// <summary>
        /// 현재 활성화된 보석 색상 수
        /// 기본값 5 = Red, Blue, Green, Yellow, Purple
        /// 6으로 변경하면 Orange도 포함되어 난이도가 올라갑니다.
        /// </summary>
        public static int ActiveGemTypeCount = 5;

        /// <summary>
        /// 랜덤 보석 색상을 하나 반환합니다.
        /// 활성 색상 범위 내에서만 선택하며, 회색(Gray)은 절대 반환하지 않습니다.
        /// (회색은 적군 전용이므로 일반 블록에 사용하면 안 됩니다)
        /// </summary>
        public static GemType GetRandom()
        {
            GemType gem = (GemType)UnityEngine.Random.Range(1, ActiveGemTypeCount + 1);
            // 회색 블록 생성 방지 (ActiveGemTypeCount 변경으로 인한 버그 대비)
            while (gem == GemType.Gray)
                gem = (GemType)UnityEngine.Random.Range(1, ActiveGemTypeCount + 1);
            return gem;
        }
    }

    /// <summary>
    /// 보석 색상표 - 각 보석 타입에 대응하는 실제 화면 표시 색상(RGB)을 정의
    ///
    /// 게임 화면에서 블록을 칠할 때 이 색상표를 참조합니다.
    /// 상위 등급 보석(Ruby, Sapphire 등)은 기본 색상과 동일한 색을 사용합니다.
    /// </summary>
    public static class GemColors
    {
        /// <summary>
        /// 보석 타입에 해당하는 Unity Color 값을 반환합니다.
        /// </summary>
        public static Color GetColor(GemType type)
        {
            switch (type)
            {
                case GemType.Red:
                case GemType.Ruby:
                    return new Color(0.93f, 0.18f, 0.18f);    // 선명한 빨강
                case GemType.Blue:
                case GemType.Sapphire:
                    return new Color(0.15f, 0.45f, 0.95f);    // 선명한 파랑
                case GemType.Green:
                case GemType.Emerald:
                    return new Color(0.18f, 0.78f, 0.28f);    // 선명한 초록
                case GemType.Yellow:
                case GemType.Amber:
                    return new Color(1.0f, 0.82f, 0.08f);     // 선명한 노랑
                case GemType.Purple:
                case GemType.Amethyst:
                    return new Color(0.62f, 0.2f, 0.88f);     // 선명한 보라
                case GemType.Orange:
                    return new Color(1.0f, 0.5f, 0.05f);      // 선명한 주황
                case GemType.Gray:
                    return new Color(0.55f, 0.55f, 0.58f);     // 적군 회색
                default:
                    return new Color(0.75f, 0.68f, 0.6f);     // 베이지
            }
        }
    }
}

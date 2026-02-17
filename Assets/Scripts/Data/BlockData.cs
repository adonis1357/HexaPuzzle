using UnityEngine;

namespace JewelsHexaPuzzle.Data
{
    public enum GameMode
    {
        Stage,
        Infinite
    }

    public enum GemType
    {
        None = 0,
        Red = 1,
        Blue = 2,
        Green = 3,
        Yellow = 4,
        Purple = 5,
        Orange = 6,
        Ruby = 7,
        Emerald = 8,
        Sapphire = 9,
        Amber = 10,
        Amethyst = 11,
        Gray = 12
    }

    public enum SpecialBlockType
    {
        None,
        MoveBlock,
        FixedBlock,
        TimeBomb,
        Drill,
        Bomb,
        Rainbow,
        XBlock,
        Laser
    }

    public enum DrillDirection
    {
        Vertical,
        Slash,
        BackSlash
    }

    public enum EnemyType
    {
        None = -1,
        Chromophage = 0,    // 색상도둑 | 별칭: 그레이
        ChainAnchor = 1,    // 속박의 사슬 | 별칭: 사슬
        ThornParasite = 2,  // 가시 기생충 | 별칭: 가시
        Divider = 3,        // 분열체 | 별칭: 분열
        GravityWarper = 4,  // 중력왜곡자 | 별칭: 중력
        ReflectionShield = 5, // 반사방패 | 별칭: 방패
        TimeFreezer = 6,    // 시간동결자 | 별칭: 동결
        ResonanceTwin = 7,  // 공명쌍둥이 | 별칭: 쌍둥이
        ShadowSpore = 8,    // 그림자포자 | 별칭: 포자
        ChaosOverlord = 9   // 카오스 군주 | 별칭: 카오스
    }

    public static class EnemyTypeHelper
    {
        /// <summary>
        /// 적군 정식 명칭 반환
        /// </summary>
        public static string GetName(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Chromophage: return "색상도둑";
                case EnemyType.ChainAnchor: return "속박의 사슬";
                case EnemyType.ThornParasite: return "가시 기생충";
                case EnemyType.Divider: return "분열체";
                case EnemyType.GravityWarper: return "중력왜곡자";
                case EnemyType.ReflectionShield: return "반사방패";
                case EnemyType.TimeFreezer: return "시간동결자";
                case EnemyType.ResonanceTwin: return "공명쌍둥이";
                case EnemyType.ShadowSpore: return "그림자포자";
                case EnemyType.ChaosOverlord: return "카오스 군주";
                default: return "없음";
            }
        }

        /// <summary>
        /// 적군 별칭 (짧은 이름) 반환
        /// </summary>
        public static string GetAlias(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Chromophage: return "그레이";
                case EnemyType.ChainAnchor: return "사슬";
                case EnemyType.ThornParasite: return "가시";
                case EnemyType.Divider: return "분열";
                case EnemyType.GravityWarper: return "중력";
                case EnemyType.ReflectionShield: return "방패";
                case EnemyType.TimeFreezer: return "동결";
                case EnemyType.ResonanceTwin: return "쌍둥이";
                case EnemyType.ShadowSpore: return "포자";
                case EnemyType.ChaosOverlord: return "카오스";
                default: return "";
            }
        }
    }

    [System.Flags]
    public enum ChaosEffect
    {
        None = 0,
        Chromophage = 1 << 0,
        ChainAnchor = 1 << 1,
        GravityWarper = 1 << 2,
        ReflectionShield = 1 << 3,
        TimeFreezer = 1 << 4
    }

    public enum RemovalMethod
    {
        Match,          // 일반 매칭
        SpecialBasic,   // 드릴
        SpecialAdvanced,// 폭탄/레이저
        Donut,          // 도넛(무지개)
        Cascade         // 캐스케이드 연쇄
    }

    public enum RemovalCondition
    {
        Normal,         // 일반 제거
        ChainBroken,    // 체인 해제 후
        ShieldBroken,   // 방패 파괴 후
        TwinPaired,     // 쌍둥이 동시 제거
        ChaosWeakened   // 카오스 다회 공격
    }

    public struct EnemyKillData
    {
        public EnemyType enemyType;
        public RemovalMethod method;
        public RemovalCondition condition;
    }

    public enum BlockTier
    {
        Normal = 0,
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3,
        ProcessedGem = 4
    }

    [System.Serializable]
    public class BlockData
    {
        public GemType gemType;
        public SpecialBlockType specialType;
        public int timeBombCount;
        public int vinylLayer;
        public bool hasChain;
        public bool hasThorn;
        public DrillDirection drillDirection;
        public BlockTier tier;
        public bool pendingActivation;

        // 적군 시스템 필드
        public EnemyType enemyType;
        public int enemyShieldCount;     // ReflectionShield 내구도
        public int enemySpreadTimer;     // ShadowSpore 확산 타이머
        public int enemyTwinId;          // ResonanceTwin 페어 ID
        public ChaosEffect chaosEffectMask; // ChaosOverlord 효과 비트마스크
        public int chaosHitCount;        // ChaosOverlord 누적 피격

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

        public BlockData(GemType type)
        {
            gemType = type;
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

        public BlockData Clone()
        {
            return new BlockData
            {
                gemType = this.gemType,
                specialType = this.specialType,
                timeBombCount = this.timeBombCount,
                vinylLayer = this.vinylLayer,
                hasChain = this.hasChain,
                hasThorn = this.hasThorn,
                drillDirection = this.drillDirection,
                tier = this.tier,
                pendingActivation = this.pendingActivation,
                enemyType = this.enemyType,
                enemyShieldCount = this.enemyShieldCount,
                enemySpreadTimer = this.enemySpreadTimer,
                enemyTwinId = this.enemyTwinId,
                chaosEffectMask = this.chaosEffectMask,
                chaosHitCount = this.chaosHitCount
            };
        }

        public bool HasEnemy()
        {
            return enemyType != EnemyType.None;
        }

        public bool IsGravityAnchored()
        {
            return enemyType == EnemyType.GravityWarper ||
                   (enemyType == EnemyType.ChaosOverlord && (chaosEffectMask & ChaosEffect.GravityWarper) != 0);
        }

        public bool HasShield()
        {
            return (enemyType == EnemyType.ReflectionShield && enemyShieldCount > 0) ||
                   (enemyType == EnemyType.ChaosOverlord && (chaosEffectMask & ChaosEffect.ReflectionShield) != 0 && enemyShieldCount > 0);
        }

        public bool IsSpecial()
        {
            return specialType != SpecialBlockType.None;
        }

        public bool IsDrill()
        {
            return specialType == SpecialBlockType.Drill;
        }

        public bool IsDonut()
        {
            return specialType == SpecialBlockType.Rainbow;
        }

        public bool IsXBlock()
        {
            return specialType == SpecialBlockType.XBlock;
        }

        public bool IsLaser()
        {
            return specialType == SpecialBlockType.Laser;
        }

        public bool IsBomb()
        {
            return specialType == SpecialBlockType.Bomb;
        }

        public bool CanMove()
        {
            return specialType != SpecialBlockType.FixedBlock && !hasChain;
        }
    }

    public static class GemTypeHelper
    {
        /// <summary>
        /// 활성 보석 타입 수 (기본 5: Red~Purple, 6으로 변경 시 Orange 포함)
        /// </summary>
        public static int ActiveGemTypeCount = 5;

        /// <summary>
        /// 랜덤 보석 타입 반환 (활성 타입 범위 내에서)
        /// </summary>
        public static GemType GetRandom()
        {
            return (GemType)UnityEngine.Random.Range(1, ActiveGemTypeCount + 1);
        }
    }

    public static class GemColors
    {
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

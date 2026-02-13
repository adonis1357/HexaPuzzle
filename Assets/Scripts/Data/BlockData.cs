using UnityEngine;

namespace JewelsHexaPuzzle.Data
{
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
        Amethyst = 11
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
        public DrillDirection drillDirection;
        public BlockTier tier;
        public bool pendingActivation;

        public BlockData()
        {
            gemType = GemType.None;
            specialType = SpecialBlockType.None;
            timeBombCount = 0;
            vinylLayer = 0;
            hasChain = false;
            drillDirection = DrillDirection.Vertical;
            tier = BlockTier.Normal;
        }

        public BlockData(GemType type)
        {
            gemType = type;
            specialType = SpecialBlockType.None;
            timeBombCount = 0;
            vinylLayer = 0;
            hasChain = false;
            drillDirection = DrillDirection.Vertical;
            tier = BlockTier.Normal;
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
                drillDirection = this.drillDirection,
                tier = this.tier,
                pendingActivation = this.pendingActivation
            };
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
            return specialType != SpecialBlockType.FixedBlock;
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
                    return new Color(0.85f, 0.25f, 0.22f);    // 베리 레드 (따뜻한 붉은색)
                case GemType.Blue:
                case GemType.Sapphire:
                    return new Color(0.3f, 0.55f, 0.78f);     // 이슬 블루 (부드러운 하늘색)
                case GemType.Green:
                case GemType.Emerald:
                    return new Color(0.35f, 0.7f, 0.32f);     // 잎사귀 그린 (따뜻한 올리브)
                case GemType.Yellow:
                case GemType.Amber:
                    return new Color(0.92f, 0.75f, 0.2f);     // 꿀 골드 (진한 앰버)
                case GemType.Purple:
                case GemType.Amethyst:
                    return new Color(0.6f, 0.35f, 0.72f);     // 라벤더 (따뜻한 보라)
                case GemType.Orange:
                    return new Color(0.9f, 0.55f, 0.18f);     // 넥타 오렌지 (꿀벌 주황)
                default:
                    return new Color(0.55f, 0.45f, 0.35f);    // 밀랍 브라운
            }
        }
    }
}

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
        Rainbow
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
                tier = this.tier
            };
        }

        public bool CanMove()
        {
            return specialType != SpecialBlockType.FixedBlock && !hasChain;
        }

        public bool IsSpecial()
        {
            return specialType != SpecialBlockType.None;
        }

        public bool IsDrill()
        {
            return specialType == SpecialBlockType.Drill;
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
                    return new Color(0.9f, 0.2f, 0.2f);
                case GemType.Blue:
                case GemType.Sapphire:
                    return new Color(0.2f, 0.4f, 0.9f);
                case GemType.Green:
                case GemType.Emerald:
                    return new Color(0.2f, 0.8f, 0.3f);
                case GemType.Yellow:
                case GemType.Amber:
                    return new Color(0.95f, 0.85f, 0.2f);
                case GemType.Purple:
                case GemType.Amethyst:
                    return new Color(0.7f, 0.3f, 0.9f);
                case GemType.Orange:
                    return new Color(1f, 0.5f, 0.1f);
                default:
                    return Color.gray;
            }
        }
    }
}

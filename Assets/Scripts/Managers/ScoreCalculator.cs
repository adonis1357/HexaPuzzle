using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 점수 계산 공식 (static 유틸리티)
    /// </summary>
    public static class ScoreCalculator
    {
        // 블록 티어별 기본 점수
        private const int ScoreNormal = 50;
        private const int ScoreTier1 = 75;
        private const int ScoreTier2 = 100;
        private const int ScoreTier3 = 150;
        private const int ScoreProcessedGem = 200;

        // 매치 크기 보너스
        private const int Bonus3Match = 0;
        private const int Bonus4Match = 100;
        private const int Bonus5Match = 250;
        private const int Bonus6Match = 400;
        private const int Bonus7PlusMatch = 600;

        // 스테이지 클리어 보너스
        private const int PointsPerRemainingTurn = 200;
        private const int EfficiencyBonusPerfect = 2000;
        private const int EfficiencyBonusGreat = 1000;

        /// <summary>
        /// 블록 티어에 따른 기본 점수
        /// </summary>
        public static int GetBlockBaseScore(BlockTier tier)
        {
            switch (tier)
            {
                case BlockTier.Tier1: return ScoreTier1;
                case BlockTier.Tier2: return ScoreTier2;
                case BlockTier.Tier3: return ScoreTier3;
                case BlockTier.ProcessedGem: return ScoreProcessedGem;
                default: return ScoreNormal;
            }
        }

        /// <summary>
        /// 매치 크기에 따른 보너스 점수
        /// </summary>
        public static int GetMatchSizeBonus(int blockCount)
        {
            if (blockCount >= 7) return Bonus7PlusMatch;
            if (blockCount >= 6) return Bonus6Match;
            if (blockCount >= 5) return Bonus5Match;
            if (blockCount >= 4) return Bonus4Match;
            return Bonus3Match;
        }

        /// <summary>
        /// 매치 그룹 점수 계산 (블록 수 기반, 모두 Normal 티어 가정)
        /// </summary>
        public static int CalculateMatchGroupScore(int blockCount)
        {
            int baseScore = blockCount * ScoreNormal;
            int sizeBonus = GetMatchSizeBonus(blockCount);
            return baseScore + sizeBonus;
        }

        /// <summary>
        /// 캐스케이드 깊이에 따른 점수 배율
        /// depth 0: 1.0x, 1: 1.2x, 2: 1.4x, 3: 1.6x, 4+: 1.8x
        /// </summary>
        public static float GetCascadeScoreMultiplier(int depth)
        {
            float multiplier = 1.0f + depth * 0.2f;
            return multiplier > 1.8f ? 1.8f : multiplier;
        }

        /// <summary>
        /// 남은 턴 보너스 계산
        /// </summary>
        public static int CalculateRemainingTurnsBonus(int remainingTurns)
        {
            return remainingTurns > 0 ? remainingTurns * PointsPerRemainingTurn : 0;
        }

        /// <summary>
        /// 효율 보너스 계산
        /// </summary>
        public static int CalculateEfficiencyBonus(int turnsUsed, int turnLimit)
        {
            if (turnLimit <= 0) return 0;
            float ratio = (float)turnsUsed / turnLimit;
            if (ratio < 0.5f) return EfficiencyBonusPerfect;
            if (ratio < 0.7f) return EfficiencyBonusGreat;
            return 0;
        }
    }
}

using JewelsHexaPuzzle.Data;
using UnityEngine;

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

        // 특수 블록 생성 가산점
        private const int CreationBonusDrill = 150;
        private const int CreationBonusBomb = 300;
        private const int CreationBonusDonut = 800;

        // 복수 생성 보너스
        private const int MultiCreation2 = 200;
        private const int MultiCreation3Plus = 500;

        // 적군 기본 점수는 EnemyRegistry에서 중앙 관리

        // 제거 방법 배율
        private const float RemovalMultiplierMatch = 1.0f;
        private const float RemovalMultiplierSpecialBasic = 1.2f;
        private const float RemovalMultiplierSpecialAdvanced = 1.5f;
        private const float RemovalMultiplierDonut = 1.5f;

        // 특수 상황 보너스
        private const int BonusThornNoPenalty = 150;
        private const int BonusDividerNoSplit = 200;
        private const int BonusTwinSimultaneous = 500;
        private const int BonusShieldOneShot = 300;
        private const int BonusChaosOneShot = 1000;

        // 멀티킬 보너스
        private const int MultiKill2 = 200;
        private const int MultiKill3 = 500;
        private const int MultiKill4 = 1000;
        private const int MultiKill5Plus = 2000;

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

        // ============================================================
        // 특수 블록 생성 가산점
        // ============================================================

        /// <summary>
        /// 특수 블록 생성 가산점
        /// </summary>
        public static int GetSpecialBlockCreationBonus(SpecialBlockType type)
        {
            switch (type)
            {
                case SpecialBlockType.Drill: return CreationBonusDrill;
                case SpecialBlockType.Bomb: return CreationBonusBomb;
                case SpecialBlockType.Rainbow:
                case SpecialBlockType.XBlock: return CreationBonusDonut;
                default: return 0;
            }
        }

        /// <summary>
        /// 복수 생성 보너스
        /// </summary>
        public static int GetMultiCreationBonus(int count)
        {
            if (count >= 3) return MultiCreation3Plus;
            if (count >= 2) return MultiCreation2;
            return 0;
        }

        // ============================================================
        // 적군 파괴 점수
        // ============================================================

        /// <summary>
        /// 적군 기본 점수
        /// </summary>
        public static int GetEnemyBaseScore(EnemyType type)
        {
            return EnemyRegistry.GetBaseScore(type);
        }

        /// <summary>
        /// 제거 방법 배율
        /// </summary>
        public static float GetRemovalMethodMultiplier(RemovalMethod method)
        {
            switch (method)
            {
                case RemovalMethod.Match: return RemovalMultiplierMatch;
                case RemovalMethod.SpecialBasic: return RemovalMultiplierSpecialBasic;
                case RemovalMethod.SpecialAdvanced: return RemovalMultiplierSpecialAdvanced;
                case RemovalMethod.Donut: return RemovalMultiplierDonut;
                default: return 1.0f;
            }
        }

        /// <summary>
        /// 특수 상황 보너스
        /// </summary>
        public static int GetEnemySpecialBonus(EnemyType type, RemovalCondition condition)
        {
            switch (condition)
            {
                case RemovalCondition.Normal: return 0;
                case RemovalCondition.ChainBroken: return 0;
                case RemovalCondition.ShieldBroken: return BonusShieldOneShot;
                case RemovalCondition.TwinPaired: return BonusTwinSimultaneous;
                case RemovalCondition.ChaosWeakened: return BonusChaosOneShot;
                default: return 0;
            }
        }

        /// <summary>
        /// 멀티킬 보너스
        /// </summary>
        public static int GetMultiKillBonus(int count)
        {
            if (count >= 5) return MultiKill5Plus;
            if (count >= 4) return MultiKill4;
            if (count >= 3) return MultiKill3;
            if (count >= 2) return MultiKill2;
            return 0;
        }

        /// <summary>
        /// 단일 적군 점수 계산 (가시+매칭 → 0점)
        /// </summary>
        public static int CalculateEnemyScore(EnemyType type, RemovalMethod method, RemovalCondition condition)
        {
            // 가시 기생충 매칭 제거 시 파괴 점수 0점
            if (type == EnemyType.ThornParasite && method == RemovalMethod.Match)
                return 0;

            int baseScore = GetEnemyBaseScore(type);
            float multiplier = GetRemovalMethodMultiplier(method);
            int bonus = GetEnemySpecialBonus(type, condition);
            return Mathf.RoundToInt(baseScore * multiplier) + bonus;
        }
    }
}

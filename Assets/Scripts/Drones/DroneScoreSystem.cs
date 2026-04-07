using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

// ============================================================================
// DroneScoreSystem.cs - 드론 타겟팅 점수 계산 시스템
// ============================================================================
// 드론 블록 발동 시 최적 타격 대상을 결정하는 중앙화된 점수 시스템.
// 모든 드론 타겟팅(단일, 콤보)이 이 시스템을 통해 처리됨.
// 점수 상수는 밸런스 조절용으로 const 선언.
// ============================================================================

namespace JewelsHexaPuzzle.Drones
{
    /// <summary>
    /// 드론 콤보 타입 (점수 계산 시 콤보별 시뮬레이션 분기)
    /// </summary>
    public enum DroneComboType
    {
        Single,      // 일반 드론 단일 타격
        DroneDrill,  // 드론×드릴
        DroneBomb,   // 드론×폭탄
        DroneDrone   // 드론×드론
    }

    /// <summary>
    /// 드론 타겟팅 점수 계산 시스템 (static class)
    /// </summary>
    public static class DroneScoreSystem
    {
        // ============================================================
        // 몬스터 종류별 점수 배율 상수 (밸런스 조절용)
        // ============================================================
        public const int SCORE_MULT_REGULAR           = 10;
        public const int SCORE_MULT_ARMORED           = 20;
        public const int SCORE_MULT_SHIELD_DURABILITY = 60;
        public const int SCORE_MULT_SHIELD_BODY       = 30;
        public const int SCORE_MULT_BOMBGOBLIN        = 40;
        public const int SCORE_MULT_HEAVY             = 50;
        public const int SCORE_MULT_ARCHER            = 100;
        public const int SCORE_MULT_HEALER            = 150;
        public const int SCORE_MULT_WIZARD            = 200;
        public const int SCORE_MULT_GOBLINBOMB         = 80;

        // ============================================================
        // 1단계: 최우선 직접 타격 대상
        // ============================================================

        /// <summary>
        /// 최우선 직접 타격 대상 탐색.
        /// Wizard > Healer > Archer > Shield 순서. HP 낮은 순 우선.
        /// 소환 영역 몬스터도 포함.
        /// </summary>
        public static HexCoord? FindPriorityTarget()
        {
            if (GoblinSystem.Instance == null) return null;
            var all = GoblinSystem.Instance.GetAliveGoblins();
            if (all.Count == 0) return null;

            System.Func<GoblinData, bool>[] filters = {
                g => g.isWizard,
                g => g.isHealer,
                g => g.isArcher,
                g => g.isShielded
            };

            foreach (var filter in filters)
            {
                var candidates = all.Where(g => g.isAlive && filter(g)).ToList();
                if (candidates.Count == 0) continue;
                candidates.Sort((a, b) => a.hp.CompareTo(b.hp));
                Debug.Log($"[DroneScore] 1단계 직접 타격: {candidates[0].position} HP={candidates[0].hp}");
                return candidates[0].position;
            }
            return null;
        }

        // ============================================================
        // 몬스터별 데미지×배율 점수
        // ============================================================

        /// <summary>
        /// 특정 몬스터에게 damage만큼 데미지를 줄 때의 점수.
        /// 방패 고블린은 방패 내구도 데미지와 본체 데미지를 분리 계산.
        /// </summary>
        public static int GetMonsterDamageScore(GoblinData goblin, int damage)
        {
            if (goblin == null || !goblin.isAlive || damage <= 0) return 0;

            if (goblin.isShielded)
            {
                // 방패 고블린: 방패에 들어가는 데미지 vs 본체에 들어가는 데미지 분리
                int shieldDmg = Mathf.Min(damage, goblin.shieldHp);
                int bodyDmg = Mathf.Max(0, damage - goblin.shieldHp);
                return shieldDmg * SCORE_MULT_SHIELD_DURABILITY + bodyDmg * SCORE_MULT_SHIELD_BODY;
            }

            if (goblin.isWizard)    return damage * SCORE_MULT_WIZARD;
            if (goblin.isHealer)    return damage * SCORE_MULT_HEALER;
            if (goblin.isArcher)    return damage * SCORE_MULT_ARCHER;
            if (goblin.isHeavy)     return damage * SCORE_MULT_HEAVY;
            if (goblin.isBomb)      return damage * SCORE_MULT_BOMBGOBLIN;
            if (goblin.isArmored)   return damage * SCORE_MULT_ARMORED;
            return damage * SCORE_MULT_REGULAR;
        }

        // ============================================================
        // 낙하 데미지 시뮬레이션
        // ============================================================

        /// <summary>
        /// 특정 좌표의 블록이 파괴될 때 낙하로 발생하는 데미지 시뮬레이션.
        /// 같은 열(q)에서 destroyCoord보다 아래(r 큼)에 있는 몬스터에게 낙하 데미지.
        /// 낙하 면역 몬스터(IsImmuneToFallDamage)는 0점.
        /// </summary>
        public static int SimulateFallDamageScore(HexCoord destroyCoord, HexGrid hexGrid)
        {
            if (GoblinSystem.Instance == null || hexGrid == null) return 0;

            int totalScore = 0;
            var allGoblins = GoblinSystem.Instance.GetAliveGoblins();

            foreach (var g in allGoblins)
            {
                if (g.IsImmuneToFallDamage) continue;

                bool isBelow = false;
                // 일반 몬스터: 같은 열에서 아래에 있으면 낙하 대상
                if (g.position.q == destroyCoord.q && g.position.r > destroyCoord.r)
                    isBelow = true;

                // Heavy: occupiedCoords도 체크
                if (!isBelow && g.isHeavy && g.occupiedCoords != null)
                {
                    foreach (var oc in g.occupiedCoords)
                    {
                        if (oc.q == destroyCoord.q && oc.r > destroyCoord.r)
                        { isBelow = true; break; }
                    }
                }

                if (isBelow)
                    totalScore += GetMonsterDamageScore(g, 1); // 낙하 1칸 = 1 데미지
            }

            return totalScore;
        }

        // ============================================================
        // 위치 점수
        // ============================================================

        /// <summary>
        /// 위치 기반 보너스 점수.
        /// 몬스터가 없으면 0. 중앙 기준 5점, 아래 +1/위 -1, 소환 영역 +10~30.
        /// </summary>
        public static int GetPositionScore(HexCoord coord, HexGrid hexGrid)
        {
            if (GoblinSystem.Instance == null || hexGrid == null) return 0;
            var goblin = GoblinSystem.Instance.GetGoblinAt(coord);
            if (goblin == null) return 0;

            int score = 5; // 기본

            // 중앙(0,0) 기준 상하 보정
            score -= coord.r; // r 큰 = 아래 → 점수 높임 (부호 반전)
            // 실제로는 r 양수 = 아래이므로: score = 5 + r 방향
            score = 5 + coord.r; // 아래쪽일수록 높은 점수

            // 좌우 외곽 보너스
            score += Mathf.Abs(coord.q);

            // 소환 영역 보너스
            int topR = hexGrid.GetTopR(coord.q);
            if (coord.r == topR - 1) score += 10;       // 1번째 줄
            else if (coord.r == topR - 2) score += 20;  // 2번째 줄
            else if (coord.r == topR - 3) score += 30;  // 3번째 줄

            return Mathf.Max(0, score);
        }

        // ============================================================
        // 블록 총점 계산
        // ============================================================

        /// <summary>
        /// 특정 블록 좌표를 타격했을 때 총점.
        /// 직접 데미지 점수 + 낙하 데미지 점수 + GoblinBomb 점수 + 위치 점수.
        /// </summary>
        public static int CalcBlockScore(HexCoord coord, HexGrid hexGrid, DroneComboType comboType = DroneComboType.Single)
        {
            if (GoblinSystem.Instance == null || hexGrid == null) return 0;

            int score = 0;

            // GoblinBomb 점수
            HexBlock block = hexGrid.GetBlock(coord);
            if (block != null && block.Data != null && block.Data.hasGoblinBomb)
                score += SCORE_MULT_GOBLINBOMB;

            // 직접 타격 데미지
            int droneDmg = 1 + (SkillTreeManager.Instance != null
                ? SkillTreeManager.Instance.GetDroneTargetDamageBonus() : 0);

            GoblinData directGoblin = GoblinSystem.Instance.GetGoblinAt(coord);
            if (directGoblin != null)
                score += GetMonsterDamageScore(directGoblin, droneDmg);

            // 낙하 데미지 점수
            score += SimulateFallDamageScore(coord, hexGrid);

            // 위치 점수
            score += GetPositionScore(coord, hexGrid);

            return score;
        }

        // ============================================================
        // 2단계: 점수 기반 최적 타겟 선정
        // ============================================================

        /// <summary>
        /// 전체 블록에서 최고 점수 타겟 반환.
        /// 1단계 직접 타격 우선 → 2단계 점수 기반.
        /// excludeBlocks: 이미 선택된 블록 제외 (다중 드론).
        /// excludeDroneBlock: 드론 자신 제외.
        /// </summary>
        public static HexBlock FindBestTarget(HexGrid hexGrid, HexBlock excludeDroneBlock = null,
            HashSet<HexBlock> excludeBlocks = null, DroneComboType comboType = DroneComboType.Single)
        {
            if (hexGrid == null) return null;

            // 1단계: 최우선 직접 타격
            HexCoord? priority = FindPriorityTarget();
            if (priority.HasValue)
            {
                HexBlock directBlock = hexGrid.GetBlock(priority.Value);
                if (directBlock != null && directBlock.Data != null && directBlock.Data.gemType != GemType.None
                    && directBlock != excludeDroneBlock
                    && (excludeBlocks == null || !excludeBlocks.Contains(directBlock)))
                    return directBlock;

                // 소환 영역 → 같은 열 최상단 블록
                int q = priority.Value.q;
                int topR = hexGrid.GetTopR(q);
                HexBlock topBlock = hexGrid.GetBlock(new HexCoord(q, topR));
                if (topBlock != null && topBlock.Data != null && topBlock.Data.gemType != GemType.None
                    && topBlock != excludeDroneBlock
                    && (excludeBlocks == null || !excludeBlocks.Contains(topBlock)))
                {
                    Debug.Log($"[DroneScore] 소환 영역 → 열 최상단: ({q}, {topR})");
                    return topBlock;
                }
            }

            // 2단계: 전체 블록 점수 계산
            HexBlock bestBlock = null;
            int bestScore = 0;
            List<HexBlock> tiedBlocks = new List<HexBlock>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) continue;
                if (block == excludeDroneBlock) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;
                if (excludeBlocks != null && excludeBlocks.Contains(block)) continue;

                int score = CalcBlockScore(block.Coord, hexGrid, comboType);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestBlock = block;
                    tiedBlocks.Clear();
                    tiedBlocks.Add(block);
                }
                else if (score == bestScore && score > 0)
                {
                    tiedBlocks.Add(block);
                }
            }

            // 동점이면 랜덤 선택
            if (tiedBlocks.Count > 1)
            {
                bestBlock = tiedBlocks[Random.Range(0, tiedBlocks.Count)];
                Debug.Log($"[DroneScore] 동점 {tiedBlocks.Count}개 → 랜덤: {bestBlock.Coord} 점수={bestScore}");
            }
            else if (bestBlock != null)
            {
                Debug.Log($"[DroneScore] 최적 타겟: {bestBlock.Coord} 점수={bestScore}");
            }

            return bestBlock;
        }

        /// <summary>
        /// 다중 드론용: 점수 1위~N위 순차 타겟 반환.
        /// </summary>
        public static List<HexBlock> FindMultiTargets(HexGrid hexGrid, int count, HexBlock excludeDroneBlock = null,
            DroneComboType comboType = DroneComboType.Single)
        {
            var results = new List<HexBlock>();
            var excluded = new HashSet<HexBlock>();
            if (excludeDroneBlock != null) excluded.Add(excludeDroneBlock);

            for (int i = 0; i < count; i++)
            {
                HexBlock target = FindBestTarget(hexGrid, excludeDroneBlock, excluded, comboType);
                if (target == null) break;
                results.Add(target);
                excluded.Add(target);
            }

            return results;
        }
    }
}

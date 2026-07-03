using UnityEngine;
using System.Collections.Generic;

// ============================================================================
// SkillData.cs - 스킬 트리 데이터 정의
// ============================================================================
// 특수 블록 능력 업그레이드를 위한 스킬 트리 시스템의 데이터 구조.
// 스킬 종류, 레벨, 비용, 해금 조건 등을 정의합니다.
// ============================================================================

namespace JewelsHexaPuzzle.Data
{
    /// <summary>
    /// 스킬 종류 — 특수 블록별 업그레이드 스킬
    /// </summary>
    public enum SkillType
    {
        None = 0,

        // === 드릴 스킬 체인 ===
        DrillMove1 = 100,     // 드릴 1칸 이동 사용
        DrillMove2 = 101,     // 드릴 2칸 이동 사용
        DrillMove3 = 102,     // 드릴 3칸 이동 사용

        // === 폭탄 이동 스킬 체인 ===
        BombMove1 = 200,      // 폭탄 1칸 이동 사용
        BombMove2 = 201,      // 폭탄 2칸 이동 사용
        BombMove3 = 202,      // 폭탄 3칸 이동 사용

        // === 폭탄 넉백 스킬 체인 ===
        BombKnockback1 = 300,  // 폭탄 넉백 거리 +1
        BombKnockback2 = 301,  // 폭탄 넉백 거리 +2
        BombKnockback3 = 302,  // 폭탄 넉백 거리 +3

        // === 폭탄 데미지 스킬 체인 ===
        BombDamage1 = 400,     // 폭탄 전 범위 데미지 +1
        BombDamage2 = 401,     // 폭탄 전 범위 데미지 +2
        BombDamage3 = 402,     // 폭탄 전 범위 데미지 +3

        // === 드릴 강화 스킬 체인 ===
        DrillDamage1 = 500,    // 드릴 몬스터 데미지 +1
        DrillDamage2 = 501,    // 드릴 몬스터 데미지 +2
        DrillDamage3 = 502,    // 드릴 몬스터 데미지 +3

        // === 드릴 쿠션 반사 스킬 체인 ===
        DrillCushion1 = 550,       // 드릴 쿠션 1회
        DrillCushion2 = 551,       // 드릴 쿠션 2회
        DrillCushion3 = 552,       // 드릴 쿠션 3회

        // === 드론 타겟 데미지 스킬 체인 ===
        DroneTargetDamage1 = 600,  // 드론 타겟 데미지 +1
        DroneTargetDamage2 = 601,  // 드론 타겟 데미지 +2
        DroneTargetDamage3 = 602,  // 드론 타겟 데미지 +3

        // === 망치 아이템 레벨 체인 ===
        HammerLevel1 = 700,
        HammerLevel2 = 701,
        HammerLevel3 = 702,

        // === 스왑 아이템 레벨 체인 ===
        SwapLevel1 = 800,
        SwapLevel2 = 801,
        SwapLevel3 = 802,

        // === 라인 아이템 레벨 체인 ===
        LineLevel1 = 900,
        LineLevel2 = 901,
        LineLevel3 = 902,

        // === 연쇄폭탄 스킬 체인 ===
        ChainBomb1 = 1000,     // 소형 폭탄 1개 투척
        ChainBomb2 = 1001,     // 소형 폭탄 2개 투척
        ChainBomb3 = 1002,     // 소형 폭탄 3개 투척

        // === 타겟 강화 스킬 체인 ===
        TargetDamage1 = 1100,  // 타겟 데미지 +1
        TargetDamage2 = 1101,  // 타겟 데미지 +2
        TargetDamage3 = 1102,  // 타겟 데미지 +3

        // === 드릴 관통 스킬 체인 ===
        DrillPenetrate1 = 1200, // 드릴 몬스터 1마리 관통
        DrillPenetrate2 = 1201, // 드릴 몬스터 2마리 관통
        DrillPenetrate3 = 1202, // 드릴 몬스터 3마리 관통

        // === 드론 분신 스킬 체인 (+1/+2/+3 추가 드론, 서로 다른 우선순위 타겟) ===
        DroneClone1 = 1300,
        DroneClone2 = 1301,
        DroneClone3 = 1302,

        // === 직접 타격 데미지 스킬 체인 (+2/+4/+6, 기본 1 → 최대 7) ===
        DirectHit1 = 1400,
        DirectHit2 = 1401,
        DirectHit3 = 1402,

        // === 인접(범위) 타격 데미지 스킬 체인 (+1/+2/+3) ===
        AdjacentHit1 = 1500,
        AdjacentHit2 = 1501,
        AdjacentHit3 = 1502,

        // === 낙하 치명타 확률 스킬 체인 (기본 10% + 20/40/60 → 최대 70%) ===
        FallCritChance1 = 1600,
        FallCritChance2 = 1601,
        FallCritChance3 = 1602,

        // === 낙하 치명타 데미지 스킬 체인 (기본 +1, 추가 +1/+2/+3 → 최대 +4) ===
        FallCritDamage1 = 1700,
        FallCritDamage2 = 1701,
        FallCritDamage3 = 1702,
    }

    /// <summary>
    /// 스킬 해금 상태
    /// </summary>
    public enum SkillState
    {
        Locked,       // 선행 스킬 미해금 → 잠김 (어둡게 표시)
        Available,    // 해금 가능 (선행 조건 충족, 비용 지불 가능)
        Unlocked      // 이미 해금됨 (활성 상태)
    }

    /// <summary>
    /// 개별 스킬 노드 정의 — 스킬 트리의 한 노드
    /// </summary>
    [System.Serializable]
    public class SkillNodeData
    {
        public SkillType skillType;           // 스킬 종류
        public string skillName;              // 표시 이름 (한글)
        public string description;            // 스킬 설명
        public string usageDescription;       // 사용 방법 설명
        public int skillPointCost;            // 스킬 포인트 비용
        public int goldCost;                  // 골드 비용
        public SkillType prerequisite;        // 선행 스킬 (None이면 즉시 해금 가능)
        public Color nodeColor;               // 노드 표시 색상
        public string iconSymbol;             // 프로시저럴 아이콘 문자 (유니코드)

        /// <summary>
        /// 드릴 이동 칸 수 (DrillMove 스킬 전용)
        /// </summary>
        public int drillMoveRange;

        /// <summary>
        /// 스킬 카테고리 해금에 필요한 플레이어 레벨 (0 = 제한 없음)
        /// 카테고리 첫 스킬(1레벨)에만 설정. 2,3레벨은 prerequisite로 체인.
        /// </summary>
        public int requiredLevel;
    }

    /// <summary>
    /// 스킬 카테고리별 해금 레벨 매핑 (13카테고리 → 레벨 21~100)
    /// </summary>
    public static class SkillUnlockSchedule
    {
        /// <summary>
        /// 스킬 카테고리 첫 스킬의 해금 요구 레벨 반환 (0 = 초기 해금)
        /// ★ 아이템 게이지 파워업(망치/스왑/라인)은 게이지 시스템의 레이어 상한을
        ///    결정하므로 레벨 게이팅 없이 초기 해금 — 게이지 시스템 보존
        /// </summary>
        public static int GetRequiredLevel(SkillType firstSkillOfCategory)
        {
            switch (firstSkillOfCategory)
            {
                // ★ 초기 해금 (레벨 21, 스킬트리 오픈 즉시 사용 가능)
                case SkillType.DrillMove1:     return 0;  // 드릴 이동
                case SkillType.DrillDamage1:   return 0;  // 드릴 강화
                case SkillType.BombMove1:      return 0;  // 폭탄 이동
                case SkillType.BombDamage1:    return 0;  // 폭탄 강화
                // ★ 게이지 파워업 (게이지 시스템 레이어 상한) — 레벨 게이팅 없음
                case SkillType.HammerLevel1:   return 0;  // 망치 파워 (게이지)
                case SkillType.SwapLevel1:     return 0;  // 스왑 디딤 (게이지)
                case SkillType.LineLevel1:     return 0;  // 라인 연결 (게이지)

                // ★ 순차 해금 (전투 강화 스킬만)
                case SkillType.BombKnockback1:     return 30;  // 폭탄 넉백
                case SkillType.DrillCushion1:       return 35;  // 드릴 쿠션
                case SkillType.DroneTargetDamage1:  return 40;  // 드론 강화
                case SkillType.ChainBomb1:          return 55;  // 연쇄 폭탄
                case SkillType.TargetDamage1:       return 80;  // 타겟 강화
                case SkillType.DrillPenetrate1:     return 50;  // 드릴 관통 (90 → 50으로 인하)

                default: return 0;
            }
        }

        /// <summary>
        /// 특정 카테고리의 첫 스킬(Lv1) 타입 반환
        /// </summary>
        public static SkillType GetCategoryFirstSkill(SkillType anySkillInCategory)
        {
            int val = (int)anySkillInCategory;
            // 각 카테고리의 100단위 기본값으로 맵핑
            if (val >= 100 && val <= 102) return SkillType.DrillMove1;
            if (val >= 200 && val <= 202) return SkillType.BombMove1;
            if (val >= 300 && val <= 302) return SkillType.BombKnockback1;
            if (val >= 400 && val <= 402) return SkillType.BombDamage1;
            if (val >= 500 && val <= 502) return SkillType.DrillDamage1;
            if (val >= 550 && val <= 552) return SkillType.DrillCushion1;
            if (val >= 600 && val <= 602) return SkillType.DroneTargetDamage1;
            if (val >= 700 && val <= 702) return SkillType.HammerLevel1;
            if (val >= 800 && val <= 802) return SkillType.SwapLevel1;
            if (val >= 900 && val <= 902) return SkillType.LineLevel1;
            if (val >= 1000 && val <= 1002) return SkillType.ChainBomb1;
            if (val >= 1100 && val <= 1102) return SkillType.TargetDamage1;
            if (val >= 1200 && val <= 1202) return SkillType.DrillPenetrate1;
            if (val >= 1300 && val <= 1302) return SkillType.DroneClone1;
            if (val >= 1400 && val <= 1402) return SkillType.DirectHit1;
            if (val >= 1500 && val <= 1502) return SkillType.AdjacentHit1;
            if (val >= 1600 && val <= 1602) return SkillType.FallCritChance1;
            if (val >= 1700 && val <= 1702) return SkillType.FallCritDamage1;
            return SkillType.None;
        }

        /// <summary>
        /// 플레이어 최고 클리어 레벨 기준으로 해당 스킬 카테고리가 해금되었는지 확인
        /// </summary>
        public static bool IsCategoryUnlocked(SkillType skillType, int highestClearedLevel)
        {
            SkillType firstSkill = GetCategoryFirstSkill(skillType);
            int required = GetRequiredLevel(firstSkill);
            if (required <= 0) return true; // 초기 해금
            return highestClearedLevel >= required;
        }
    }

    /// <summary>
    /// 스킬 트리 전체 정의 — 모든 스킬 노드와 연결 관계
    /// </summary>
    public static class SkillTreeDefinition
    {
        private static List<SkillNodeData> _allSkills;

        /// <summary>
        /// 모든 스킬 노드 데이터 반환
        /// </summary>
        public static List<SkillNodeData> GetAllSkills()
        {
            if (_allSkills == null)
                InitializeSkills();
            return _allSkills;
        }

        /// <summary>드릴 스킬만 반환</summary>
        public static List<SkillNodeData> GetDrillSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all)
            {
                int v = (int)s.skillType;
                if (v >= 100 && v <= 199) result.Add(s);
            }
            return result;
        }

        /// <summary>폭탄 스킬만 반환</summary>
        public static List<SkillNodeData> GetBombSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all)
            {
                int v = (int)s.skillType;
                if (v >= 200 && v <= 299) result.Add(s);
            }
            return result;
        }

        /// <summary>폭탄 넉백 스킬만 반환</summary>
        public static List<SkillNodeData> GetBombKnockbackSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all)
            {
                int v = (int)s.skillType;
                if (v >= 300 && v <= 399) result.Add(s);
            }
            return result;
        }

        /// <summary>폭탄 데미지 스킬만 반환</summary>
        public static List<SkillNodeData> GetBombDamageSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all)
            {
                int v = (int)s.skillType;
                if (v >= 400 && v <= 499) result.Add(s);
            }
            return result;
        }

        /// <summary>드릴 강화 스킬만 반환</summary>
        public static List<SkillNodeData> GetDrillDamageSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all)
            {
                int v = (int)s.skillType;
                if (v >= 500 && v <= 549) result.Add(s);
            }
            return result;
        }

        /// <summary>드릴 쿠션 스킬만 반환</summary>
        public static List<SkillNodeData> GetDrillCushionSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 550 && v <= 599) result.Add(s); }
            return result;
        }

        /// <summary>드론 타겟 데미지 스킬만 반환</summary>
        public static List<SkillNodeData> GetDroneTargetDamageSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 600 && v <= 699) result.Add(s); }
            return result;
        }

        /// <summary>망치 아이템 스킬만 반환</summary>
        public static List<SkillNodeData> GetHammerSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 700 && v <= 799) result.Add(s); }
            return result;
        }

        /// <summary>스왑 아이템 스킬만 반환</summary>
        public static List<SkillNodeData> GetSwapSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 800 && v <= 899) result.Add(s); }
            return result;
        }

        /// <summary>라인 아이템 스킬만 반환</summary>
        public static List<SkillNodeData> GetLineSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 900 && v <= 999) result.Add(s); }
            return result;
        }

        public static List<SkillNodeData> GetChainBombSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 1000 && v <= 1099) result.Add(s); }
            return result;
        }

        public static List<SkillNodeData> GetTargetDamageSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 1100 && v <= 1199) result.Add(s); }
            return result;
        }

        /// <summary>드릴 관통 스킬만 반환</summary>
        public static List<SkillNodeData> GetDrillPenetrateSkills()
        {
            var all = GetAllSkills();
            var result = new List<SkillNodeData>();
            foreach (var s in all) { int v = (int)s.skillType; if (v >= 1200 && v <= 1299) result.Add(s); }
            return result;
        }

        /// <summary>
        /// 특정 스킬 타입의 노드 데이터 조회
        /// </summary>
        public static SkillNodeData GetSkill(SkillType type)
        {
            var skills = GetAllSkills();
            foreach (var s in skills)
            {
                if (s.skillType == type) return s;
            }
            return null;
        }

        private static void InitializeSkills()
        {
            // ★ 비용 스케일링: 급격한 증가 (레벨1≈10, 레벨2≈20, 레벨3≈50)
            // 카테고리별 미세한 차이 적용
            _allSkills = new List<SkillNodeData>
            {
                // === 드릴 이동 체인 (초기 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.DrillMove1,
                    skillName = "드릴 이동 I",
                    description = "드릴 블록을 인접 1칸으로 이동시킨 후 발동합니다.",
                    usageDescription = "드릴 블록을 길게 터치 → 인접 1칸으로 드래그 → 놓으면 해당 위치에서 드릴 발동",
                    skillPointCost = 8, goldCost = 80,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.3f, 0.75f, 1f), iconSymbol = "▶", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillMove2,
                    skillName = "드릴 이동 II",
                    description = "드릴 블록을 최대 2칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "드릴 블록을 길게 터치 → 최대 2칸 범위 내 드래그 → 놓으면 해당 위치에서 드릴 발동",
                    skillPointCost = 18, goldCost = 180,
                    prerequisite = SkillType.DrillMove1, requiredLevel = 25,
                    nodeColor = new Color(0.2f, 0.6f, 1f), iconSymbol = "▶▶", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillMove3,
                    skillName = "드릴 이동 III",
                    description = "드릴 블록을 최대 3칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "드릴 블록을 길게 터치 → 최대 3칸 범위 내 드래그 → 놓으면 해당 위치에서 드릴 발동",
                    skillPointCost = 45, goldCost = 450,
                    prerequisite = SkillType.DrillMove2, requiredLevel = 35,
                    nodeColor = new Color(0.1f, 0.4f, 0.9f), iconSymbol = "▶▶▶", drillMoveRange = 3
                },

                // === 폭탄 이동 체인 (초기 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.BombMove1,
                    skillName = "폭탄 이동 I",
                    description = "폭탄 블록을 인접 1칸으로 이동시킨 후 발동합니다.",
                    usageDescription = "폭탄 블록을 길게 터치 → 인접 1칸으로 드래그 → 놓으면 해당 위치에서 폭탄 발동",
                    skillPointCost = 9, goldCost = 85,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(1f, 0.5f, 0.2f), iconSymbol = "●", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombMove2,
                    skillName = "폭탄 이동 II",
                    description = "폭탄 블록을 최대 2칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "폭탄 블록을 길게 터치 → 최대 2칸 범위 내 드래그 → 놓으면 해당 위치에서 폭탄 발동",
                    skillPointCost = 19, goldCost = 190,
                    prerequisite = SkillType.BombMove1, requiredLevel = 25,
                    nodeColor = new Color(1f, 0.35f, 0.1f), iconSymbol = "●●", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombMove3,
                    skillName = "폭탄 이동 III",
                    description = "폭탄 블록을 최대 3칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "폭탄 블록을 길게 터치 → 최대 3칸 범위 내 드래그 → 놓으면 해당 위치에서 폭탄 발동",
                    skillPointCost = 47, goldCost = 460,
                    prerequisite = SkillType.BombMove2, requiredLevel = 35,
                    nodeColor = new Color(0.9f, 0.2f, 0.05f), iconSymbol = "●●●", drillMoveRange = 3
                },

                // === 폭탄 넉백 체인 (Lv.30 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.BombKnockback1,
                    skillName = "폭탄 넉백+1",
                    description = "폭탄 폭발 시 넉백 거리가 1칸 추가됩니다.",
                    usageDescription = "폭탄 폭발 범위 내 몬스터를 1칸 더 밀어냅니다.",
                    skillPointCost = 10, goldCost = 90,
                    prerequisite = SkillType.None, requiredLevel = 30,
                    nodeColor = new Color(1f, 0.6f, 0.3f), iconSymbol = "↗", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombKnockback2,
                    skillName = "폭탄 넉백+2",
                    description = "폭탄 폭발 시 넉백 거리가 2칸 추가됩니다.",
                    usageDescription = "폭탄 폭발 범위 내 몬스터를 2칸 더 밀어냅니다.",
                    skillPointCost = 20, goldCost = 200,
                    prerequisite = SkillType.BombKnockback1, requiredLevel = 40,
                    nodeColor = new Color(1f, 0.45f, 0.2f), iconSymbol = "↗↗", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombKnockback3,
                    skillName = "폭탄 넉백+3",
                    description = "폭탄 폭발 시 넉백 거리가 3칸 추가됩니다.",
                    usageDescription = "폭탄 폭발 범위 내 몬스터를 3칸 더 밀어냅니다.",
                    skillPointCost = 48, goldCost = 470,
                    prerequisite = SkillType.BombKnockback2, requiredLevel = 55,
                    nodeColor = new Color(0.95f, 0.3f, 0.1f), iconSymbol = "↗↗↗", drillMoveRange = 3
                },

                // === 폭탄 데미지 체인 (초기 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.BombDamage1,
                    skillName = "폭탄 강화1",
                    description = "폭탄 폭발 시 전 범위 데미지가 1 추가됩니다.",
                    usageDescription = "0칸=4, 1칸=3, 2칸=2 데미지",
                    skillPointCost = 9, goldCost = 85,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(1f, 0.3f, 0.1f), iconSymbol = "💥", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombDamage2,
                    skillName = "폭탄 강화2",
                    description = "폭탄 폭발 시 전 범위 데미지가 2 추가됩니다.",
                    usageDescription = "0칸=5, 1칸=4, 2칸=3 데미지",
                    skillPointCost = 19, goldCost = 190,
                    prerequisite = SkillType.BombDamage1, requiredLevel = 25,
                    nodeColor = new Color(0.9f, 0.15f, 0.05f), iconSymbol = "💥💥", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombDamage3,
                    skillName = "폭탄 강화3",
                    description = "폭탄 폭발 시 전 범위 데미지가 3 추가됩니다.",
                    usageDescription = "0칸=6, 1칸=5, 2칸=4 데미지",
                    skillPointCost = 47, goldCost = 460,
                    prerequisite = SkillType.BombDamage2, requiredLevel = 35,
                    nodeColor = new Color(0.8f, 0.05f, 0f), iconSymbol = "💥💥💥", drillMoveRange = 3
                },

                // === 드릴 강화 체인 (초기 해금) ===
                //   ★ 데미지 강화 — 양방향 드릴 발사체의 칸당 데미지 증가
                //     코드: drillDmg = 1 + GetDrillDamageBonus() → I=2, II=3, III=4
                new SkillNodeData
                {
                    skillType = SkillType.DrillDamage1,
                    skillName = "드릴 강화 I",
                    description = "양방향 드릴 데미지 +1 (1→2).",
                    usageDescription = "드릴 발사체가 통과하는 칸마다 데미지 2 (기본 1).",
                    skillPointCost = 8, goldCost = 80,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.4f, 0.8f, 1f), iconSymbol = "⇉", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillDamage2,
                    skillName = "드릴 강화 II",
                    description = "양방향 드릴 데미지 +2 (1→3).",
                    usageDescription = "드릴 발사체가 통과하는 칸마다 데미지 3 (기본 1).",
                    skillPointCost = 18, goldCost = 180,
                    prerequisite = SkillType.DrillDamage1, requiredLevel = 25,
                    nodeColor = new Color(0.25f, 0.65f, 1f), iconSymbol = "⇉⇉", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillDamage3,
                    skillName = "드릴 강화 III",
                    description = "양방향 드릴 데미지 +3 (1→4).",
                    usageDescription = "드릴 발사체가 통과하는 칸마다 데미지 4 (기본 1).",
                    skillPointCost = 45, goldCost = 450,
                    prerequisite = SkillType.DrillDamage2, requiredLevel = 35,
                    nodeColor = new Color(0.1f, 0.45f, 0.95f), iconSymbol = "⇉⇉⇉", drillMoveRange = 3
                },

                // === 드릴 쿠션 반사 체인 (Lv.35 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.DrillCushion1, skillName = "드릴 쿠션 I",
                    description = "드릴이 경계에서 1회 반사합니다.",
                    usageDescription = "드릴 발사 시 경계 도달 시 자동 반사 1회",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 35,
                    nodeColor = new Color(0.4f, 0.8f, 1f), iconSymbol = "↩", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillCushion2, skillName = "드릴 쿠션 II",
                    description = "드릴이 경계에서 2회 반사합니다.",
                    usageDescription = "드릴 발사 시 경계 도달 시 자동 반사 최대 2회",
                    skillPointCost = 20, goldCost = 200,
                    prerequisite = SkillType.DrillCushion1, requiredLevel = 45,
                    nodeColor = new Color(0.3f, 0.7f, 1f), iconSymbol = "↩↩", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillCushion3, skillName = "드릴 쿠션 III",
                    description = "드릴이 경계에서 3회 반사합니다.",
                    usageDescription = "드릴 발사 시 경계 도달 시 자동 반사 최대 3회",
                    skillPointCost = 50, goldCost = 500,
                    prerequisite = SkillType.DrillCushion2, requiredLevel = 60,
                    nodeColor = new Color(0.2f, 0.6f, 1f), iconSymbol = "↩↩↩", drillMoveRange = 3
                },

                // === 드론 타겟 데미지 체인 (Lv.40 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.DroneTargetDamage1,
                    skillName = "드론 강화1",
                    description = "드론 타겟 공격 시 데미지가 1 추가됩니다.",
                    usageDescription = "드론 타격 시 기본 1 + 추가 1 = 총 2 대미지",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 40,
                    nodeColor = new Color(0.3f, 0.7f, 0.95f), iconSymbol = "✈", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.DroneTargetDamage2,
                    skillName = "드론 강화2",
                    description = "드론 타겟 공격 시 데미지가 2 추가됩니다.",
                    usageDescription = "드론 타격 시 기본 1 + 추가 2 = 총 3 대미지",
                    skillPointCost = 21, goldCost = 210,
                    prerequisite = SkillType.DroneTargetDamage1, requiredLevel = 50,
                    nodeColor = new Color(0.2f, 0.55f, 0.9f), iconSymbol = "✈✈", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.DroneTargetDamage3,
                    skillName = "드론 강화3",
                    description = "드론 타겟 공격 시 데미지가 3 추가됩니다.",
                    usageDescription = "드론 타격 시 기본 1 + 추가 3 = 총 4 대미지",
                    skillPointCost = 50, goldCost = 500,
                    prerequisite = SkillType.DroneTargetDamage2, requiredLevel = 65,
                    nodeColor = new Color(0.1f, 0.4f, 0.85f), iconSymbol = "✈✈✈", drillMoveRange = 3
                },

                // === 망치 아이템 체인 (초기 해금 - 게이지 파워업) ===
                new SkillNodeData
                {
                    skillType = SkillType.HammerLevel1, skillName = "망치 파워1",
                    description = "망치 아이템 레벨 1 해금.", usageDescription = "망치 기본 해금",
                    skillPointCost = 11, goldCost = 110,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.9f, 0.2f, 0.2f), iconSymbol = "🔨", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.HammerLevel2, skillName = "망치 파워2",
                    description = "망치 아이템 레벨 2 강화.", usageDescription = "망치 강화",
                    skillPointCost = 22, goldCost = 220,
                    prerequisite = SkillType.HammerLevel1, requiredLevel = 25,
                    nodeColor = new Color(0.8f, 0.15f, 0.15f), iconSymbol = "🔨🔨", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.HammerLevel3, skillName = "망치 파워3",
                    description = "망치 아이템 레벨 3 최대 강화.", usageDescription = "망치 최대 강화",
                    skillPointCost = 52, goldCost = 520,
                    prerequisite = SkillType.HammerLevel2, requiredLevel = 35,
                    nodeColor = new Color(0.7f, 0.1f, 0.1f), iconSymbol = "🔨🔨🔨", drillMoveRange = 3
                },

                // === 연쇄폭탄 체인 (Lv.55 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.ChainBomb1, skillName = "폭탄 연쇄 I",
                    description = "폭탄 폭발 후 소형 폭탄 1개가 랜덤 블록에 투척됩니다.",
                    usageDescription = "폭탄 발동 → 소형 폭탄 1개 랜덤 블록 설치 → 세션 종료 후 소형 폭발 (중심 2뎀 + 주변 1뎀)",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 55,
                    nodeColor = new Color(1f, 0.4f, 0.15f), iconSymbol = "💣", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.ChainBomb2, skillName = "폭탄 연쇄 II",
                    description = "폭탄 폭발 후 소형 폭탄 2개가 랜덤 블록에 투척됩니다.",
                    usageDescription = "소형 폭탄 2개 투척. 폭탄 데미지 스킬 적용.",
                    skillPointCost = 21, goldCost = 210,
                    prerequisite = SkillType.ChainBomb1, requiredLevel = 70,
                    nodeColor = new Color(1f, 0.3f, 0.1f), iconSymbol = "💣", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.ChainBomb3, skillName = "폭탄 연쇄 III",
                    description = "폭탄 폭발 후 소형 폭탄 3개가 랜덤 블록에 투척됩니다.",
                    usageDescription = "소형 폭탄 3개 투척. 폭탄 강화 스킬 적용. 최대 강화.",
                    skillPointCost = 52, goldCost = 520,
                    prerequisite = SkillType.ChainBomb2, requiredLevel = 85,
                    nodeColor = new Color(0.95f, 0.2f, 0.05f), iconSymbol = "💣", drillMoveRange = 3
                },

                // === 스왑 아이템 체인 (초기 해금 - 게이지 파워업) ===
                new SkillNodeData
                {
                    skillType = SkillType.SwapLevel1, skillName = "스왑 디딤1",
                    description = "스왑 아이템 레벨 1 해금.", usageDescription = "스왑 기본 해금",
                    skillPointCost = 11, goldCost = 110,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.2f, 0.8f, 0.3f), iconSymbol = "↔", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.SwapLevel2, skillName = "스왑 디딤2",
                    description = "스왑 아이템 레벨 2 강화.", usageDescription = "스왑 강화",
                    skillPointCost = 22, goldCost = 220,
                    prerequisite = SkillType.SwapLevel1, requiredLevel = 25,
                    nodeColor = new Color(0.15f, 0.7f, 0.25f), iconSymbol = "↔↔", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.SwapLevel3, skillName = "스왑 디딤3",
                    description = "스왑 아이템 레벨 3 최대 강화.", usageDescription = "스왑 최대 강화",
                    skillPointCost = 53, goldCost = 530,
                    prerequisite = SkillType.SwapLevel2, requiredLevel = 35,
                    nodeColor = new Color(0.1f, 0.6f, 0.2f), iconSymbol = "↔↔↔", drillMoveRange = 3
                },

                // === 라인 아이템 체인 (초기 해금 - 게이지 파워업) ===
                new SkillNodeData
                {
                    skillType = SkillType.LineLevel1, skillName = "라인 연결1",
                    description = "라인 드래그 시 다른 색상 블록 1개를 브릿지로 사용 가능.", usageDescription = "브릿지 블록 1개 허용",
                    skillPointCost = 12, goldCost = 120,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.6f, 0.2f, 0.9f), iconSymbol = "━", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.LineLevel2, skillName = "라인 연결2",
                    description = "라인 드래그 시 다른 색상 블록 2개까지 브릿지로 사용 가능.", usageDescription = "브릿지 블록 2개 허용",
                    skillPointCost = 23, goldCost = 230,
                    prerequisite = SkillType.LineLevel1, requiredLevel = 25,
                    nodeColor = new Color(0.5f, 0.15f, 0.8f), iconSymbol = "━━", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.LineLevel3, skillName = "라인 연결3",
                    description = "라인 드래그 시 다른 색상 블록 3개까지 브릿지로 사용 가능.", usageDescription = "브릿지 블록 3개 허용",
                    skillPointCost = 55, goldCost = 550,
                    prerequisite = SkillType.LineLevel2, requiredLevel = 35,
                    nodeColor = new Color(0.4f, 0.1f, 0.7f), iconSymbol = "━━━", drillMoveRange = 3
                },

                // === 타겟 강화 체인 (Lv.80 해금) ===
                new SkillNodeData
                {
                    skillType = SkillType.TargetDamage1, skillName = "타겟 강화 I",
                    description = "모든 특수 블록의 몬스터 타겟 데미지 +1.",
                    usageDescription = "드릴/폭탄/드론 등 모든 특수 블록이 몬스터에 주는 데미지 +1",
                    skillPointCost = 12, goldCost = 120,
                    prerequisite = SkillType.None, requiredLevel = 80,
                    nodeColor = new Color(0.9f, 0.75f, 0.2f), iconSymbol = "⚔", drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.TargetDamage2, skillName = "타겟 강화 II",
                    description = "모든 특수 블록의 몬스터 타겟 데미지 +2.",
                    usageDescription = "모든 특수 블록 몬스터 데미지 +2",
                    skillPointCost = 24, goldCost = 240,
                    prerequisite = SkillType.TargetDamage1, requiredLevel = 90,
                    nodeColor = new Color(0.95f, 0.65f, 0.1f), iconSymbol = "⚔", drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.TargetDamage3, skillName = "타겟 강화 III",
                    description = "모든 특수 블록의 몬스터 타겟 데미지 +3. 최대 강화.",
                    usageDescription = "모든 특수 블록 몬스터 데미지 +3 (최대)",
                    skillPointCost = 55, goldCost = 550,
                    prerequisite = SkillType.TargetDamage2, requiredLevel = 100,
                    nodeColor = new Color(1f, 0.55f, 0.05f), iconSymbol = "⚔", drillMoveRange = 3
                },

                // === 드릴 관통 체인 (Lv.50 해금 — 90→50으로 인하) ===
                new SkillNodeData
                {
                    skillType = SkillType.DrillPenetrate1, skillName = "드릴 관통 I",
                    description = "드릴 관통 +1 — 기본 1마리에서 2마리 관통으로 강화합니다.",
                    usageDescription = "기본 관통 1마리 → 2마리 관통 후 3번째 몬스터에서 정지",
                    skillPointCost = 11, goldCost = 110,
                    prerequisite = SkillType.None, requiredLevel = 50,
                    nodeColor = new Color(0.85f, 0.5f, 0.2f), iconSymbol = "⟫", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillPenetrate2, skillName = "드릴 관통 II",
                    description = "드릴 관통 +2 — 3마리 관통으로 강화합니다.",
                    usageDescription = "3마리 관통 후 4번째 몬스터에서 정지",
                    skillPointCost = 22, goldCost = 220,
                    prerequisite = SkillType.DrillPenetrate1, requiredLevel = 65,
                    nodeColor = new Color(0.75f, 0.4f, 0.15f), iconSymbol = "⟫⟫", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillPenetrate3, skillName = "드릴 관통 III",
                    description = "드릴 관통 +3 — 4마리 관통으로 강화합니다. 최대 강화.",
                    usageDescription = "4마리 관통 후 5번째 몬스터에서 정지. 최대 강화.",
                    skillPointCost = 53, goldCost = 530,
                    prerequisite = SkillType.DrillPenetrate2, requiredLevel = 80,
                    nodeColor = new Color(0.65f, 0.3f, 0.1f), iconSymbol = "⟫⟫⟫", drillMoveRange = 0
                },

                // === 드론 분신 체인 (런별, 초기 등장) ===
                new SkillNodeData
                {
                    skillType = SkillType.DroneClone1, skillName = "드론 분신 I",
                    description = "드론이 1기 더 분신해 서로 다른 우선순위 타겟을 공격합니다.",
                    usageDescription = "드론 발동 시 총 2기가 우선순위 1·2 타겟으로 동시 비행",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.4f, 0.75f, 0.95f), iconSymbol = "✈", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.DroneClone2, skillName = "드론 분신 II",
                    description = "드론이 2기 더 분신해 서로 다른 우선순위 타겟을 공격합니다.",
                    usageDescription = "총 3기가 우선순위 1·2·3 타겟으로 동시 비행",
                    skillPointCost = 21, goldCost = 210,
                    prerequisite = SkillType.DroneClone1, requiredLevel = 0,
                    nodeColor = new Color(0.3f, 0.65f, 0.9f), iconSymbol = "✈✈", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.DroneClone3, skillName = "드론 분신 III",
                    description = "드론이 3기 더 분신해 서로 다른 우선순위 타겟을 공격합니다. 최대 강화.",
                    usageDescription = "총 4기가 우선순위 1·2·3·4 타겟으로 동시 비행",
                    skillPointCost = 52, goldCost = 520,
                    prerequisite = SkillType.DroneClone2, requiredLevel = 0,
                    nodeColor = new Color(0.2f, 0.55f, 0.85f), iconSymbol = "✈✈✈", drillMoveRange = 0
                },

                // === 직접 타격 데미지 체인 (+2/+4/+6, 기본 1 포함 카드 표시값 3/5/7) ===
                new SkillNodeData
                {
                    skillType = SkillType.DirectHit1, skillName = "직접 타격 I",
                    description = "특수 블록 직접 타격 데미지 +2 (기본 1 → 3).",
                    usageDescription = "드릴/드론/폭탄 중심 등 직접 명중 시 몬스터 데미지 +2",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.95f, 0.55f, 0.3f), iconSymbol = "🎯", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.DirectHit2, skillName = "직접 타격 II",
                    description = "특수 블록 직접 타격 데미지 +4 (기본 1 → 5).",
                    usageDescription = "직접 명중 시 몬스터 데미지 +4",
                    skillPointCost = 21, goldCost = 210,
                    prerequisite = SkillType.DirectHit1, requiredLevel = 0,
                    nodeColor = new Color(0.9f, 0.45f, 0.2f), iconSymbol = "🎯", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.DirectHit3, skillName = "직접 타격 III",
                    description = "특수 블록 직접 타격 데미지 +6 (기본 1 → 7). 최대 강화.",
                    usageDescription = "직접 명중 시 몬스터 데미지 +6 (최대 7)",
                    skillPointCost = 52, goldCost = 520,
                    prerequisite = SkillType.DirectHit2, requiredLevel = 0,
                    nodeColor = new Color(0.85f, 0.35f, 0.1f), iconSymbol = "🎯", drillMoveRange = 0
                },

                // === 인접(범위) 타격 데미지 체인 (+1/+2/+3) ===
                new SkillNodeData
                {
                    skillType = SkillType.AdjacentHit1, skillName = "인접 타격 I",
                    description = "범위(인접) 타격 데미지 +1.",
                    usageDescription = "폭발 인접 칸 등 범위 명중 시 몬스터 데미지 +1",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(1f, 0.65f, 0.35f), iconSymbol = "✸", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.AdjacentHit2, skillName = "인접 타격 II",
                    description = "범위(인접) 타격 데미지 +2.",
                    usageDescription = "범위 명중 시 몬스터 데미지 +2",
                    skillPointCost = 21, goldCost = 210,
                    prerequisite = SkillType.AdjacentHit1, requiredLevel = 0,
                    nodeColor = new Color(0.95f, 0.55f, 0.25f), iconSymbol = "✸✸", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.AdjacentHit3, skillName = "인접 타격 III",
                    description = "범위(인접) 타격 데미지 +3. 최대 강화.",
                    usageDescription = "범위 명중 시 몬스터 데미지 +3",
                    skillPointCost = 52, goldCost = 520,
                    prerequisite = SkillType.AdjacentHit2, requiredLevel = 0,
                    nodeColor = new Color(0.9f, 0.45f, 0.15f), iconSymbol = "✸✸✸", drillMoveRange = 0
                },

                // === 낙하 치명타 확률 체인 (기본 10% + 20/40/60 → 최대 70%) ===
                new SkillNodeData
                {
                    skillType = SkillType.FallCritChance1, skillName = "치명타 확률 I",
                    description = "낙하 치명타 확률 +20% (기본 10% → 30%).",
                    usageDescription = "블록 낙하 타격 시 치명타 발생 확률 +20%",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(0.95f, 0.8f, 0.25f), iconSymbol = "％", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.FallCritChance2, skillName = "치명타 확률 II",
                    description = "낙하 치명타 확률 +40% (기본 10% → 50%).",
                    usageDescription = "낙하 타격 치명타 확률 +40%",
                    skillPointCost = 21, goldCost = 210,
                    prerequisite = SkillType.FallCritChance1, requiredLevel = 0,
                    nodeColor = new Color(0.95f, 0.72f, 0.15f), iconSymbol = "％", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.FallCritChance3, skillName = "치명타 확률 III",
                    description = "낙하 치명타 확률 +60% (기본 10% → 70%). 최대 강화.",
                    usageDescription = "낙하 타격 치명타 확률 +60% (최대 70%)",
                    skillPointCost = 52, goldCost = 520,
                    prerequisite = SkillType.FallCritChance2, requiredLevel = 0,
                    nodeColor = new Color(0.9f, 0.62f, 0.1f), iconSymbol = "％", drillMoveRange = 0
                },

                // === 낙하 치명타 데미지 체인 (기본 +1, 추가 +1/+2/+3 → 최대 +4) ===
                new SkillNodeData
                {
                    skillType = SkillType.FallCritDamage1, skillName = "치명타 데미지 I",
                    description = "낙하 치명타 데미지 +1 (기본 +1 → +2).",
                    usageDescription = "낙하 치명타 발생 시 추가 데미지 +1",
                    skillPointCost = 10, goldCost = 100,
                    prerequisite = SkillType.None, requiredLevel = 0,
                    nodeColor = new Color(1f, 0.55f, 0.55f), iconSymbol = "✦", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.FallCritDamage2, skillName = "치명타 데미지 II",
                    description = "낙하 치명타 데미지 +2 (기본 +1 → +3).",
                    usageDescription = "낙하 치명타 발생 시 추가 데미지 +2",
                    skillPointCost = 21, goldCost = 210,
                    prerequisite = SkillType.FallCritDamage1, requiredLevel = 0,
                    nodeColor = new Color(0.95f, 0.4f, 0.4f), iconSymbol = "✦✦", drillMoveRange = 0
                },
                new SkillNodeData
                {
                    skillType = SkillType.FallCritDamage3, skillName = "치명타 데미지 III",
                    description = "낙하 치명타 데미지 +3 (기본 +1 → +4). 최대 강화.",
                    usageDescription = "낙하 치명타 발생 시 추가 데미지 +3 (최대 +4)",
                    skillPointCost = 52, goldCost = 520,
                    prerequisite = SkillType.FallCritDamage2, requiredLevel = 0,
                    nodeColor = new Color(0.9f, 0.3f, 0.3f), iconSymbol = "✦✦✦", drillMoveRange = 0
                },
            };
        }
    }
}

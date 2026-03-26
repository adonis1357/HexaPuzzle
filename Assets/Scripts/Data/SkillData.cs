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
        DrillDamage1 = 500,    // 드릴 발사체 +1 (총 3방향)
        DrillDamage2 = 501,    // 드릴 발사체 +2 (총 4방향)
        DrillDamage3 = 502,    // 드릴 발사체 +3 (총 5방향)
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
                if (v >= 500 && v <= 599) result.Add(s);
            }
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
            _allSkills = new List<SkillNodeData>
            {
                // === 드릴 스킬 체인 ===
                new SkillNodeData
                {
                    skillType = SkillType.DrillMove1,
                    skillName = "드릴 이동 I",
                    description = "드릴 블록을 인접 1칸으로 이동시킨 후 발동합니다.",
                    usageDescription = "드릴 블록을 길게 터치 → 인접 1칸으로 드래그 → 놓으면 해당 위치에서 드릴 발동",
                    skillPointCost = 1,
                    goldCost = 200,
                    prerequisite = SkillType.None,
                    nodeColor = new Color(0.3f, 0.75f, 1f),      // 하늘색
                    iconSymbol = "▶",
                    drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillMove2,
                    skillName = "드릴 이동 II",
                    description = "드릴 블록을 최대 2칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "드릴 블록을 길게 터치 → 최대 2칸 범위 내 드래그 → 놓으면 해당 위치에서 드릴 발동",
                    skillPointCost = 2,
                    goldCost = 500,
                    prerequisite = SkillType.DrillMove1,
                    nodeColor = new Color(0.2f, 0.6f, 1f),       // 파란색
                    iconSymbol = "▶▶",
                    drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillMove3,
                    skillName = "드릴 이동 III",
                    description = "드릴 블록을 최대 3칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "드릴 블록을 길게 터치 → 최대 3칸 범위 내 드래그 → 놓으면 해당 위치에서 드릴 발동",
                    skillPointCost = 3,
                    goldCost = 1000,
                    prerequisite = SkillType.DrillMove2,
                    nodeColor = new Color(0.1f, 0.4f, 0.9f),     // 진한 파란색
                    iconSymbol = "▶▶▶",
                    drillMoveRange = 3
                },

                // === 폭탄 스킬 체인 ===
                new SkillNodeData
                {
                    skillType = SkillType.BombMove1,
                    skillName = "폭탄 이동 I",
                    description = "폭탄 블록을 인접 1칸으로 이동시킨 후 발동합니다.",
                    usageDescription = "폭탄 블록을 길게 터치 → 인접 1칸으로 드래그 → 놓으면 해당 위치에서 폭탄 발동",
                    skillPointCost = 1,
                    goldCost = 200,
                    prerequisite = SkillType.None,
                    nodeColor = new Color(1f, 0.5f, 0.2f),        // 주황색
                    iconSymbol = "●",
                    drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombMove2,
                    skillName = "폭탄 이동 II",
                    description = "폭탄 블록을 최대 2칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "폭탄 블록을 길게 터치 → 최대 2칸 범위 내 드래그 → 놓으면 해당 위치에서 폭탄 발동",
                    skillPointCost = 2,
                    goldCost = 500,
                    prerequisite = SkillType.BombMove1,
                    nodeColor = new Color(1f, 0.35f, 0.1f),       // 진한 주황
                    iconSymbol = "●●",
                    drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombMove3,
                    skillName = "폭탄 이동 III",
                    description = "폭탄 블록을 최대 3칸까지 이동시킨 후 발동합니다.",
                    usageDescription = "폭탄 블록을 길게 터치 → 최대 3칸 범위 내 드래그 → 놓으면 해당 위치에서 폭탄 발동",
                    skillPointCost = 3,
                    goldCost = 1000,
                    prerequisite = SkillType.BombMove2,
                    nodeColor = new Color(0.9f, 0.2f, 0.05f),     // 빨간 주황
                    iconSymbol = "●●●",
                    drillMoveRange = 3
                },

                // === 폭탄 넉백 스킬 체인 ===
                new SkillNodeData
                {
                    skillType = SkillType.BombKnockback1,
                    skillName = "폭탄 넉백+1",
                    description = "폭탄 폭발 시 넉백 거리가 1칸 추가됩니다.",
                    usageDescription = "폭탄 폭발 범위 내 몬스터를 1칸 더 밀어냅니다.",
                    skillPointCost = 1,
                    goldCost = 200,
                    prerequisite = SkillType.None,
                    nodeColor = new Color(1f, 0.6f, 0.3f),        // 밝은 주황
                    iconSymbol = "↗",
                    drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombKnockback2,
                    skillName = "폭탄 넉백+2",
                    description = "폭탄 폭발 시 넉백 거리가 2칸 추가됩니다.",
                    usageDescription = "폭탄 폭발 범위 내 몬스터를 2칸 더 밀어냅니다.",
                    skillPointCost = 2,
                    goldCost = 500,
                    prerequisite = SkillType.BombKnockback1,
                    nodeColor = new Color(1f, 0.45f, 0.2f),       // 주황
                    iconSymbol = "↗↗",
                    drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombKnockback3,
                    skillName = "폭탄 넉백+3",
                    description = "폭탄 폭발 시 넉백 거리가 3칸 추가됩니다.",
                    usageDescription = "폭탄 폭발 범위 내 몬스터를 3칸 더 밀어냅니다.",
                    skillPointCost = 3,
                    goldCost = 1000,
                    prerequisite = SkillType.BombKnockback2,
                    nodeColor = new Color(0.95f, 0.3f, 0.1f),     // 진한 주황빨강
                    iconSymbol = "↗↗↗",
                    drillMoveRange = 3
                },

                // === 폭탄 데미지 스킬 체인 ===
                new SkillNodeData
                {
                    skillType = SkillType.BombDamage1,
                    skillName = "폭탄 데미지+1",
                    description = "폭탄 폭발 시 전 범위 데미지가 1 추가됩니다.",
                    usageDescription = "0칸=4, 1칸=3, 2칸=2 데미지",
                    skillPointCost = 1,
                    goldCost = 200,
                    prerequisite = SkillType.None,
                    nodeColor = new Color(1f, 0.3f, 0.1f),
                    iconSymbol = "💥",
                    drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombDamage2,
                    skillName = "폭탄 데미지+2",
                    description = "폭탄 폭발 시 전 범위 데미지가 2 추가됩니다.",
                    usageDescription = "0칸=5, 1칸=4, 2칸=3 데미지",
                    skillPointCost = 2,
                    goldCost = 500,
                    prerequisite = SkillType.BombDamage1,
                    nodeColor = new Color(0.9f, 0.15f, 0.05f),
                    iconSymbol = "💥💥",
                    drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.BombDamage3,
                    skillName = "폭탄 데미지+3",
                    description = "폭탄 폭발 시 전 범위 데미지가 3 추가됩니다.",
                    usageDescription = "0칸=6, 1칸=5, 2칸=4 데미지",
                    skillPointCost = 3,
                    goldCost = 1000,
                    prerequisite = SkillType.BombDamage2,
                    nodeColor = new Color(0.8f, 0.05f, 0f),
                    iconSymbol = "💥💥💥",
                    drillMoveRange = 3
                },

                // === 드릴 강화 스킬 체인 ===
                new SkillNodeData
                {
                    skillType = SkillType.DrillDamage1,
                    skillName = "드릴 강화 I",
                    description = "드릴 발사체 1개 추가 (총 3방향 발사).",
                    usageDescription = "기본 2방향 + 추가 1방향",
                    skillPointCost = 1,
                    goldCost = 200,
                    prerequisite = SkillType.None,
                    nodeColor = new Color(0.4f, 0.8f, 1f),
                    iconSymbol = "⇉",
                    drillMoveRange = 1
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillDamage2,
                    skillName = "드릴 강화 II",
                    description = "드릴 발사체 2개 추가 (총 4방향 발사).",
                    usageDescription = "기본 2방향 + 추가 2방향",
                    skillPointCost = 2,
                    goldCost = 500,
                    prerequisite = SkillType.DrillDamage1,
                    nodeColor = new Color(0.25f, 0.65f, 1f),
                    iconSymbol = "⇉⇉",
                    drillMoveRange = 2
                },
                new SkillNodeData
                {
                    skillType = SkillType.DrillDamage3,
                    skillName = "드릴 강화 III",
                    description = "드릴 발사체 3개 추가 (총 5방향 발사).",
                    usageDescription = "기본 2방향 + 추가 3방향",
                    skillPointCost = 3,
                    goldCost = 1000,
                    prerequisite = SkillType.DrillDamage2,
                    nodeColor = new Color(0.1f, 0.45f, 0.95f),
                    iconSymbol = "⇉⇉⇉",
                    drillMoveRange = 3
                },
            };
        }
    }
}

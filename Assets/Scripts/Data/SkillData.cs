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
            };
        }
    }
}

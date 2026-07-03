using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexaPuzzle.Skills
{
    /// <summary>한 스킬의 레벨별 수치/표기.</summary>
    [Serializable]
    public struct SkillLevel
    {
        public int    level;  // 1..maxLevel
        public int    value;  // 게임 로직이 읽는 수치 (예: 데미지 +N, 배치 ×N, 확률 %)
        public string label;  // UI 표기 (예: "+1", "×3", "30%")
    }

    /// <summary>
    /// 단일 스킬 정의. skills.json 의 한 항목과 1:1.
    /// 에디터 메뉴(HexaPuzzle → Skills → Build Catalog)가 json + 스프라이트로 자동 생성.
    /// 코드에서 직접 만들 수도 있습니다.
    /// </summary>
    [CreateAssetMenu(menuName = "HexaPuzzle/Skill Definition", fileName = "Skill_")]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string        id;        // json id (예: "drill_damage")
        public SkillCategory category;
        public string        nameKo;
        public string        nameEn;
        [TextArea] public string description;

        [Header("Icon")]
        public Sprite icon;             // 기본 아이콘 (레벨 무관)
        [Tooltip("레벨별로 아이콘이 다르면 채움 (index 0 = Lv.1). 비어있으면 icon 사용.")]
        public Sprite[] levelIcons;

        [Header("Progression")]
        [Tooltip("게임 로직이 읽는 스탯 키 (예: drillDamage). 본인 스탯 시스템과 매핑.")]
        public string stat;
        public int    baseValue;        // 스킬 미보유 시 기본값 (예: 치명 확률 10)
        public int    maxLevel = 1;
        public List<SkillLevel> levels = new List<SkillLevel>();

        [Header("Optional grouping")]
        public string powerup;          // "Drill"/"Bomb"/"Drone"/"Cannon" (해당 시)
        public string item;             // "Hammer"/"Swap"/"Line" (해당 시)
        public string[] mix;            // 혼합 배치 구성 (예: ["Drill","Bomb"])

        // ─── 조회 헬퍼 ──────────────────────────────────────────────
        public SkillId SkillId => SkillIds.Parse(id);

        /// <summary>레벨(1-base)의 아이콘. levelIcons 우선, 없으면 기본 icon.</summary>
        public Sprite IconFor(int level)
        {
            if (levelIcons != null && levelIcons.Length > 0)
            {
                int i = Mathf.Clamp(level - 1, 0, levelIcons.Length - 1);
                if (levelIcons[i] != null) return levelIcons[i];
            }
            return icon;
        }

        /// <summary>레벨(1-base)의 정의. 범위를 벗어나면 가장 가까운 레벨로 클램프.</summary>
        public SkillLevel LevelInfo(int level)
        {
            if (levels == null || levels.Count == 0)
                return new SkillLevel { level = level, value = level, label = level.ToString() };
            int i = Mathf.Clamp(level - 1, 0, levels.Count - 1);
            return levels[i];
        }

        /// <summary>레벨(1-base)의 게임 수치.</summary>
        public int ValueAt(int level) => LevelInfo(level).value;

        /// <summary>레벨(1-base)의 UI 표기.</summary>
        public string LabelAt(int level) => LevelInfo(level).label;
    }
}

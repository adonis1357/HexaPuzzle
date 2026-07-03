using System.Collections.Generic;
using UnityEngine;

namespace HexaPuzzle.Skills
{
    /// <summary>
    /// 모든 스킬 정의의 묶음 + 빠른 조회. 게임에서 단일 진입점으로 참조하세요.
    /// 에디터 메뉴(HexaPuzzle → Skills → Build Catalog)가 skills.json 으로 채웁니다.
    /// </summary>
    [CreateAssetMenu(menuName = "HexaPuzzle/Skill Catalog", fileName = "SkillCatalog")]
    public class SkillCatalog : ScriptableObject
    {
        public List<SkillDefinition> skills = new List<SkillDefinition>();

        private Dictionary<string, SkillDefinition> _byId;
        private Dictionary<SkillId, SkillDefinition> _byEnum;

        private void BuildIndex()
        {
            _byId = new Dictionary<string, SkillDefinition>();
            _byEnum = new Dictionary<SkillId, SkillDefinition>();
            foreach (var s in skills)
            {
                if (s == null || string.IsNullOrEmpty(s.id)) continue;
                _byId[s.id] = s;
                var e = s.SkillId;
                if (e != SkillId.None) _byEnum[e] = s;
            }
        }

        public SkillDefinition Get(string id)
        {
            if (_byId == null) BuildIndex();
            return _byId.TryGetValue(id, out var s) ? s : null;
        }

        public SkillDefinition Get(SkillId id)
        {
            if (_byEnum == null) BuildIndex();
            return _byEnum.TryGetValue(id, out var s) ? s : null;
        }

        /// <summary>카테고리별 목록 (UI 탭/필터용).</summary>
        public List<SkillDefinition> InCategory(SkillCategory cat)
        {
            var list = new List<SkillDefinition>();
            foreach (var s in skills) if (s != null && s.category == cat) list.Add(s);
            return list;
        }

        /// <summary>인덱스 캐시 무효화 (런타임에 skills 를 바꿨을 때).</summary>
        public void Rebuild() => BuildIndex();
    }
}

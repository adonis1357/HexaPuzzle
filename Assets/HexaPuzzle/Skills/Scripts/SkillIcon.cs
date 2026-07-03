using UnityEngine;
using UnityEngine.UI;

namespace HexaPuzzle.Skills
{
    /// <summary>
    /// 스킬 아이콘 1개를 UI Image 또는 SpriteRenderer 에 표시.
    /// SetSkill(def, level) 로 레벨별 아이콘까지 반영합니다.
    /// </summary>
    public class SkillIcon : MonoBehaviour
    {
        [Header("Target (둘 중 하나)")]
        [SerializeField] private Image          uiImage;        // UGUI 용
        [SerializeField] private SpriteRenderer spriteRenderer; // 월드 스프라이트 용

        [Header("State")]
        [SerializeField] private SkillDefinition skill;
        [SerializeField] private int level = 1;

        public SkillDefinition Skill => skill;
        public int Level => level;

        private void Reset()
        {
            uiImage = GetComponent<Image>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Awake()
        {
            if (uiImage == null) uiImage = GetComponent<Image>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            Apply();
        }

        public void SetSkill(SkillDefinition def, int lvl = 1)
        {
            skill = def;
            level = Mathf.Max(1, lvl);
            Apply();
        }

        public void SetLevel(int lvl)
        {
            level = Mathf.Max(1, lvl);
            Apply();
        }

        private void Apply()
        {
            var sprite = skill != null ? skill.IconFor(level) : null;
            if (uiImage != null)
            {
                uiImage.sprite = sprite;
                uiImage.enabled = sprite != null;
                uiImage.preserveAspect = true;
            }
            if (spriteRenderer != null) spriteRenderer.sprite = sprite;
        }
    }
}

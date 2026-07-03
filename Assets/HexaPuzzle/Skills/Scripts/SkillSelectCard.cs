using System;
using UnityEngine;
using UnityEngine.UI;

namespace HexaPuzzle.Skills
{
    /// <summary>
    /// 스킬 선택 카드 1장. (Skill Upgrade Select.html 의 .card 포팅)
    /// 아이콘·이름·레벨·설명·선택 버튼을 표시하고, 탭하면 onChosen 발생.
    /// SkillSelectPanel 이 프리팹으로 3장 생성해 사용합니다.
    /// </summary>
    public class SkillSelectCard : MonoBehaviour
    {
        [Header("Refs")]
        public SkillIcon icon;
        public Text      titleText;
        public Text      levelText;
        public Text      descText;
        public Button    chooseButton;
        public Image     frameTint;     // 파워업/아이템 색조 (선택)

        public SkillDefinition Skill { get; private set; }
        public int Level { get; private set; }

        public event Action<SkillSelectCard> onChosen;

        private void Awake()
        {
            if (chooseButton != null)
                chooseButton.onClick.AddListener(() => onChosen?.Invoke(this));
        }

        public void Bind(SkillDefinition def, int level)
        {
            Skill = def;
            Level = Mathf.Max(1, level);
            if (def == null) { gameObject.SetActive(false); return; }
            gameObject.SetActive(true);

            if (icon != null)      icon.SetSkill(def, Level);
            if (titleText != null) titleText.text = def.nameKo;
            if (levelText != null) levelText.text = "Lv." + Level + "  " + def.LabelAt(Level);
            if (descText != null)  descText.text = def.description;
        }
    }
}

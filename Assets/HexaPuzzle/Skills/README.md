# 헥사퍼즐 스킬 시스템 — Unity 에셋

레벨업 시 보여주는 **스킬 선택 UI** + 모든 **스킬 아이콘**(파워업 강화 / 아이템 강화 / 전투·치명타 / 즉시 랜덤 배치 / 메타)을 한 패키지로 묶은 에셋입니다.

```
Assets/HexaPuzzle/Skills/
├── Data/skills.json          ← 단일 소스 (모든 스킬 정의 + 레벨별 수치)
├── Sprites/                  ← 30종 스킬 아이콘 (512px, 투명 PNG, Sprite 임포트됨)
├── Scripts/
│   ├── SkillEnums.cs         ← SkillCategory / SkillId + json↔enum 변환
│   ├── SkillDefinition.cs    ← 스킬 1개 (ScriptableObject)
│   ├── SkillCatalog.cs       ← 전체 묶음 + 조회 (ScriptableObject)
│   ├── SkillIcon.cs          ← 아이콘 표시 컴포넌트 (Image/SpriteRenderer)
│   ├── SkillSelectCard.cs    ← 선택 카드 1장
│   ├── SkillSelectPanel.cs   ← 후보 N장 + 리롤 + 진행도
│   └── Editor/HexaPuzzleSkillsSetup.cs  ← 카탈로그 자동 빌드 메뉴
├── SkillCatalog.asset        ← 메뉴 실행 후 생성됨
└── Definitions/Skill_*.asset ← 메뉴 실행 후 생성됨
```

---

## ⚡ 빠른 시작 (3단계)

### 1️⃣ 카탈로그 빌드
Unity 상단 메뉴 **`HexaPuzzle → Skills → ⚡ Build Catalog From JSON`** 클릭.
→ `skills.json` 을 읽어 `Definitions/Skill_*.asset` 30개와 `SkillCatalog.asset` 을 생성하고, `Sprites/` 의 PNG 를 이름으로 자동 연결합니다.

### 2️⃣ 패널 배치
- Canvas 아래에 빈 패널 GameObject + `SkillSelectPanel` 컴포넌트 추가
- `catalog` = `SkillCatalog.asset`
- `cardParent` = 카드가 들어갈 `HorizontalLayoutGroup`
- `cardPrefab` = `SkillSelectCard` 가 붙은 카드 프리팹(아이콘/타이틀/레벨/설명/버튼 연결)

### 3️⃣ 게임에서 호출
```csharp
[SerializeField] SkillSelectPanel skillPanel;

void OnGaugeFull()                 // 달성 게이지가 가득 차면
{
    skillPanel.onSkillChosen += ApplySkill;
    skillPanel.Open();             // 후보 3장 자동 추첨 + 표시
}

void ApplySkill(SkillDefinition skill, int newLevel)
{
    int amount = skill.ValueAt(newLevel);   // 레벨별 수치
    // skill.stat 키로 본인 스탯 시스템에 반영
    Stats.Apply(skill.stat, amount);
    Debug.Log($"{skill.nameKo} → Lv.{newLevel} ({skill.LabelAt(newLevel)})");
}
```

---

## 📦 스킬 카테고리 (총 28 정의 / 30 스프라이트)

| 카테고리 | 스킬 | 레벨별 수치 |
|---|---|---|
| **파워업 강화** | 드릴(데미지/기동/쿠션/관통), 폭탄(데미지/기동/연쇄/넉백), 드론(데미지/분신), 레이저포(데미지) | 데미지·쿠션·넉백 +1~+3 등 |
| **아이템 강화** | 망치(공격력/범위), 스왑(디딤), 라인(데미지/연결) | 망치 +1~+3, 디딤 1~3칸, 연결 1~3종 |
| **전투/치명타** | 직접 타격, 인접 타격, 낙하 치명 확률, 치명 데미지 | 치명 확률 30/50/70%, 치명 데미지 +2/+3/+4 |
| **즉시 랜덤 배치** | 드릴/폭탄/드론/레이저포 소환, 혼합 2·3·4종 | 드릴 ×3→5, 폭탄·드론 ×2→4, 레이저포 ×1→3 |
| **메타 진행** | 달성 게이지 감소 | −1 / −2 / −3 (레벨별 아이콘) |

> 수치는 `skills.json` 에서 자유롭게 조정하고 메뉴를 다시 실행하면 반영됩니다.

---

## 🔑 핵심 API

```csharp
// 카탈로그 조회
SkillDefinition d = catalog.Get(SkillId.DrillDamage);   // enum
SkillDefinition d = catalog.Get("drill_damage");        // json id
catalog.InCategory(SkillCategory.Deploy);               // 카테고리 필터

// 스킬 정보
d.ValueAt(2);          // Lv.2 게임 수치 (int)
d.LabelAt(2);          // Lv.2 UI 표기 (예: "+2", "×4")
d.IconFor(2);          // Lv.2 아이콘 (레벨별 다르면 반영)
d.maxLevel; d.stat; d.baseValue;

// 진행도 (세이브와 연동)
skillPanel.LevelOf("drill_damage");      // 현재 보유 레벨 (0=미보유)
skillPanel.SetLevel("drill_damage", 2);
skillPanel.LoadProgress(savedDict);
skillPanel.Progress;                     // IReadOnlyDictionary<string,int>
```

---

## 🎨 아이콘 디자인 언어

- 골드 헥사 프레임 + 다크 웰 + 능력 글리프(우하단 골드 원형 배지)
- 파워업 색조: 드릴 hue 90 · 폭탄 35 · 드론 190 · 레이저포 225
- 아이템 색조: 망치 35 · 스왑 145 · 라인 305
- 전투 스킬: 적 hue 25 · 크리티컬 골드 별 · 게이지 감소는 적색 절단

원본 SVG 소스는 프로젝트 루트의 `*-variants.js`, `combat-skills.js`, `random-place-skills.js`, `gauge-reduce-skill.js` 참고. 아이콘을 재생성하려면 해당 HTML(예: `Combat Skill Icons.html`)에서 512px PNG 로 래스터화 후 `Sprites/` 에 교체하세요.

자세한 통합/확장 절차는 **CLAUDE_CODE_PROMPT.md** 참고.

# Claude Code 통합 지시서 — 헥사퍼즐 스킬 시스템

이 패키지(`Assets/HexaPuzzle/Skills/`)를 헥사퍼즐 Unity 프로젝트에 통합합니다.
레벨업 시 **스킬 선택 UI**를 띄우고, 선택한 스킬을 게임 스탯에 반영하는 것이 목표입니다.

---

## 0. 전제

- Unity 2021.3+ (UGUI). 네임스페이스 `HexaPuzzle.Skills`.
- 모든 스킬의 단일 소스는 **`Data/skills.json`** 입니다. 수치/이름/아이콘 매핑을 여기서 바꾸세요.
- 아이콘 PNG 30종은 이미 Sprite 로 임포트되어 있습니다(512 px, pivot center, alphaIsTransparency).

## 1. 가장 먼저 — 카탈로그 빌드

에디터 메뉴 실행: **`HexaPuzzle → Skills → ⚡ Build Catalog From JSON`**

생성물:
- `Definitions/Skill_<id>.asset` — 스킬별 ScriptableObject
- `SkillCatalog.asset` — 전체 묶음 (게임이 참조하는 단일 진입점)
- 스프라이트는 이름(`sprite`/`levelSprites`)으로 자동 연결됨

JSON 을 수정했으면 메뉴를 다시 실행하면 됩니다(기존 에셋 갱신, GUID 유지).

## 2. 씬 구성 (스킬 선택 패널)

> 프리팹이 아직 없으므로 **직접 만들어야 하는 부분**입니다. 아래 계층을 만드세요.

```
Canvas (Screen Space - Overlay)
└── SkillSelectPanel            [SkillSelectPanel.cs]
    ├── Dimmer (Image, 반투명 검정, stretch)
    ├── Banner/Title (Text "스킬 강화 선택")
    ├── Cards (HorizontalLayoutGroup, spacing 18)   ← cardParent
    └── RerollButton (Button)                        ← rerollButton
        ├── Label (Text)                             ← rerollLabel
        └── Cost  (Text)                             ← rerollCost

CardPrefab (프리팹으로 저장)   [SkillSelectCard.cs]
├── Frame (Image, 골드 그라데이션)
├── IconSlot/Icon (Image)      [SkillIcon.cs]        ← icon
├── Title (Text)                                     ← titleText
├── Level (Text)                                     ← levelText
├── Desc  (Text)                                     ← descText
└── ChooseButton (Button "선택")                     ← chooseButton
```

인스펙터 연결:
- `SkillSelectPanel.catalog` = `SkillCatalog.asset`
- `SkillSelectPanel.cardParent` = `Cards`
- `SkillSelectPanel.cardPrefab` = `CardPrefab`
- `SkillSelectCard` 의 각 Text/Image/Button 을 위 표대로 연결
- `SkillIcon.uiImage` = 카드의 `Icon`

스타일 참고용 원본: 프로젝트 루트 `Skill Upgrade Select.html`(카드 그리드/골드 프레임/색조), `Item Skill Select (In-Game).html`(인게임 톤).

## 3. 게임 로직 연결

```csharp
using HexaPuzzle.Skills;

public class SkillFlow : MonoBehaviour
{
    [SerializeField] SkillSelectPanel panel;

    void Start()
    {
        panel.onSkillChosen += Apply;
        // 리롤 비용 처리(골드 차감). false 반환 시 리롤 취소.
        panel.onRerollRequested = cost => Wallet.TrySpend(cost);
        // 세이브에서 진행도 복원
        panel.LoadProgress(Save.LoadSkillLevels());
    }

    // 달성 게이지가 가득 찼을 때 호출
    public void OnSkillGaugeFull() => panel.Open();

    void Apply(SkillDefinition skill, int newLevel)
    {
        int v = skill.ValueAt(newLevel);
        ApplyStat(skill.stat, v, skill);          // 아래 4장 참조
        Save.StoreSkillLevels(panel.Progress);    // 진행도 저장
    }
}
```

## 4. 스탯 매핑 (`skill.stat` → 게임 효과)

각 스킬은 `stat` 키와 레벨별 `value` 를 가집니다. 본인 스탯 시스템에 라우팅하세요.

| stat 키 | 의미 | value 예시 |
|---|---|---|
| `drillDamage`/`bombDamage`/`droneDamage`/`cannonDamage` | 파워업 데미지 가산 | +1~+3 |
| `drillMove`/`bombMove` | 1칸 이동 사용 해금 | 1 = ON |
| `drillCushion`/`bombKnockback`/`droneClone` | 보조 수치 가산 | +1~+3 |
| `drillPierce` | 관통 해금 | 1 = ON |
| `bombChain` | 연쇄 단계 | 1~3 |
| `hammerDamage`/`hammerRange`/`lineDamage` | 도구 수치 가산 | +1~+3 |
| `swapStep` | 스왑 건너뛰기 칸 수 | 1~3 |
| `lineConnect` | 연결 가능한 다른 블록 종류 수 | 1~3 |
| `directHit`/`adjacentHit` | 타격 배수/단계 | 1~3 |
| `critChancePct` | 낙하 치명 확률(%) (기본 `baseValue`=10) | 30/50/70 |
| `critBonus` | 치명 추가 데미지 (기본 `baseValue`=1) | +2/+3/+4 |
| `deployDrill`/`deployBomb`/`deployDrone`/`deployCannon` | 즉시 배치 개수 | 드릴 3~5, 폭탄·드론 2~4, 레이저포 1~3 |
| `deployMix2`/`deployMix3`/`deployMix4` | 혼합 배치(구성은 `mix[]`) | 종류 수 |
| `gaugeReduce` | 달성 게이지 감소 칸 | −1/−2/−3 |

> 즉시 배치 계열은 `skill.mix`(혼합)와 `skill.powerup`(단일)을 함께 참조해 보드에 무작위 소환하세요. `value` = 배치 개수.

## 5. 확장 방법 — 새 스킬 추가

1. `Data/skills.json` 에 항목 추가 (id, category, sprite, levels[], stat …)
2. `SkillEnums.cs` 의 `SkillId` enum 에 PascalCase 이름 추가 (예: `drill_speed` → `DrillSpeed`)
3. 아이콘 PNG 를 `Sprites/<sprite>.png` 로 추가 (기존과 같은 골드 헥사 스타일)
4. 메뉴 **Build Catalog From JSON** 다시 실행

## 6. 체크리스트

- [ ] 메뉴 실행 → 콘솔에 "카탈로그 빌드 완료 … 누락 0개" 확인
- [ ] `SkillCatalog.asset` 의 skills 30개, 각 icon 연결됨
- [ ] 패널 Open() → 후보 3장, 아이콘/레벨/설명 표시
- [ ] 카드 선택 → onSkillChosen 발생, 레벨 +1, 패널 닫힘
- [ ] 최대 레벨 스킬은 후보에서 제외됨
- [ ] 리롤 → onRerollRequested(cost) 호출, 후보 갱신

## 주의

- `skills.json` 의 `id` ↔ `SkillId` enum 이름은 항상 동기화 (snake_case ↔ PascalCase, `SkillIds.Parse/ToJsonId` 가 변환).
- 스프라이트 누락 시 메뉴가 경고 로그를 남깁니다 — 파일명/대소문자 확인.
- 아이콘 원본/재생성은 README 의 "아이콘 디자인 언어" 절 참고.

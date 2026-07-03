# 룬 스톤 블록 — Unity 에셋 사용 가이드

`Hex Crystals.html` 의 **Rune Stone** 스타일을 그대로 Unity 로 옮긴 패키지입니다. 6가지 색상 × 6가지 고유 룬 문양으로 색맹 접근성까지 확보된 헥사 퍼즐 블록.

---

## ⚡ 빠른 시작 (3 단계)

### 1️⃣ 임포트
`unity/Assets/HexaPuzzle/RuneBlocks/` 폴더 전체를 본인 Unity 프로젝트의 `Assets/` 아래로 복사 (`.meta` 포함).

### 2️⃣ 자동 셋업
Unity 상단 메뉴: **`HexaPuzzle → RuneBlocks → ⚡ Do Everything`**

→ 다음이 자동 생성됩니다:
- `Sprites/RuneStone_Ruby.png` … `RuneStone_Amethyst.png` (6장, 256×256, 절차 생성)
- `Prefabs/RuneBlock.prefab` (6장 스프라이트가 미리 연결된 단일 프리팹)
- `Scenes/RuneBlocksDemo.unity` (▶ Play 로 즉시 확인)

### 3️⃣ 본인 게임에 배치
```csharp
using HexaPuzzle.RuneBlocks;

public GameObject runePrefab;

void SpawnAt(Vector3 pos, RuneColor color) {
    var go = Instantiate(runePrefab, pos, Quaternion.identity);
    var block = go.GetComponent<RuneBlock>();
    block.SetColor(color);
    block.PlayPopIn();
}
```

---

## 🎨 6 컬러 × 6 룬

| Color | 한글 | OKLCH hue | 룬 |
|---|---|---|---|
| `Ruby` | 루비 (빨강) | 25° | 삼각형 (불) |
| `Amber` | 앰버 (주황) | 60° | 마름모 |
| `Citrine` | 시트린 (노랑) | 95° | 5각 별 |
| `Emerald` | 에메랄드 (초록) | 155° | 잎 (잎맥 포함) |
| `Sapphire` | 사파이어 (파랑) | 245° | 물방울 |
| `Amethyst` | 자수정 (보라) | 305° | 동심원 (신비의 눈) |

모든 색은 OKLCH 색상 공간에서 동일한 명도/채도를 유지하고 hue 만 회전합니다 — 어떤 두 색을 옆에 놓아도 한쪽이 튀지 않음.

---

## 📐 API

```csharp
namespace HexaPuzzle.RuneBlocks;

public enum RuneColor { Ruby, Amber, Citrine, Emerald, Sapphire, Amethyst }

public class RuneBlock : MonoBehaviour {
    public RuneColor Color { get; }
    public void SetColor(RuneColor c);       // 컬러+룬 일괄 변경
    public bool Matches(RuneBlock other);    // 매칭 로직용
    public void PlayPopIn();                 // Instantiate 직후
    public void PlayDestroyAndKill(Action onDone = null);  // 매칭 시
}

public static class RuneColorMeta {
    public static Entry Get(RuneColor c);    // englishName, koreanName, hue
    public static Entry[] All;
}
```

---

## 🎯 매칭 로직 예시

```csharp
// 3매치 검사
bool IsMatch3(RuneBlock a, RuneBlock b, RuneBlock c) =>
    a != null && a.Matches(b) && a.Matches(c);

// 매칭된 블록 제거
void DestroyMatched(List<RuneBlock> matched) {
    foreach (var b in matched) {
        b.PlayDestroyAndKill(() => OnBlockGone(b));
    }
}
```

---

## 🔧 메타데이터

- **언어**: C# (.NET Standard 2.0)
- **Unity 최소 버전**: 2020.3 LTS
- **종속성**: 없음 (UnityEngine 기본)
- **TextMeshPro 필요**: ❌
- **렌더 파이프라인**: Built-in / URP / HDRP 모두 OK
- **스프라이트 크기**: 256×256, PPU=256 (1 sprite = 1 world unit)
- **스프라이트 정렬**: SpriteRenderer.sortingOrder = 10
- **모바일**: iOS / Android OK

---

## ❓ 자주 묻는 질문

**Q. 스프라이트 크기를 더 키우고 싶다.**
`RuneBlockSetupMenu.SpriteSize` 를 512 등으로 바꾸고 `Generate Sprites` 메뉴 다시 실행.

**Q. 룬 문양만 다른 걸로 바꾸고 싶다.**
`RuneBlockSetupMenu.GetRuneStrokes(RuneColor)` 에 정의된 polyline 좌표를 수정. 좌표는 SVG viewBox 0..200 공간.

**Q. TextMeshPro/URP 로도 동작하나?**
스프라이트만 만드는 패키지라 어떤 렌더 파이프라인에서도 그대로 동작합니다.

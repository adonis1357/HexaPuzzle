# Claude Code 통합 지시서 — Rune Block 패키지

`Hex Crystals.html` 의 Rune Stone 스타일을 본인 헥사 퍼즐 프로젝트에 통합합니다.

---

## 🚀 한 줄 프롬프트 (가장 빠름)

```
이 Unity 프로젝트의 Assets/ 폴더에 /tmp/hexa-package/unity/Assets/HexaPuzzle/RuneBlocks 를 통합하고,
Assets/HexaPuzzle/RuneBlocks/CLAUDE_CODE_PROMPT.md 의 5단계 워크플로우를 그대로 실행해줘.
내 게임의 헥사 그리드에 RuneBlock 프리팹을 스폰하도록 자동 연결해줘.
```

---

## 📋 5단계 자동 통합 워크플로우

````
이 작업을 단계별로 진행하고, 각 단계 끝나면 결과를 보고하고 다음으로 넘어가.

# 작업 개요
헥사 퍼즐 게임에 6가지 컬러의 룬 스톤 블록을 통합한다.
- 각 컬러(Ruby/Amber/Citrine/Emerald/Sapphire/Amethyst)는 고유 룬 문양을 가짐
- 색맹 접근성: 색상 외에 문양으로도 구분 가능
- 매칭 단위는 동일 RuneColor

패키지 경로: /tmp/hexa-package/unity/Assets/HexaPuzzle/RuneBlocks/
(다른 위치면 사용자에게 물어봐)

# 1단계 — 패키지 복사
- /tmp/hexa-package/unity/Assets/HexaPuzzle/RuneBlocks/ 전체를 Assets/HexaPuzzle/ 아래로 복사
- .meta 파일도 모두 함께 복사 (Unity GUID 보존)
- Unity Editor 가 열려있다면 사용자에게 "메뉴 HexaPuzzle → RuneBlocks → ⚡ Do Everything 을 실행해줘" 안내
- 복사된 파일 목록 보고

# 2단계 — 프로젝트 분석
다음을 찾아서 보고:
- Unity 버전 (ProjectSettings/ProjectVersion.txt)
- 헥사 그리드 매니저 / 보드 클래스
  - "Board", "Grid", "Hex", "Tile" 등 단어로 grep
  - 자주 보이는 이름: BoardManager, HexGrid, TileManager, GameBoard
- 블록 스폰이 일어나는 곳
  - "Spawn", "Create", "Instantiate" + block/tile 관련
- 기존 컬러/타입 enum 이 있다면 그 정의
- 매칭 판정 메서드 (FindMatches, CheckMatch 등)

각 항목을 파일 경로 + 메서드 시그니처 한 줄로 표 정리.

# 3단계 — 통합 계획 제시
2단계 결과를 바탕으로 사용자에게:
- 본인 게임의 기존 컬러 enum 을 그대로 두고 RuneColor 로 매핑할지,
  아니면 본인 코드를 RuneColor 로 통일할지
- RuneBlock 컴포넌트를 본인의 기존 Block/Tile 클래스에 추가할지,
  아니면 RuneBlock 을 메인 컴포넌트로 사용할지
- 스폰 위치에 PlayPopIn() 을, 매칭 시 PlayDestroyAndKill() 을 연결할 위치
- 매칭 판정 함수에서 a.Matches(b) 패턴 사용 여부

확인 후에만 다음 단계로.

# 4단계 — 통합 코드 작성
승인 후 다음 작업:

(a) 블록 스폰 위치에서 RuneBlock 프리팹 사용:
```csharp
using HexaPuzzle.RuneBlocks;

public class BoardManager : MonoBehaviour {
    public GameObject runeBlockPrefab;   // RuneBlock.prefab

    RuneBlock SpawnBlock(Vector2Int cell, RuneColor color) {
        Vector3 worldPos = CellToWorld(cell);
        var go = Instantiate(runeBlockPrefab, worldPos, Quaternion.identity, transform);
        var block = go.GetComponent<RuneBlock>();
        block.SetColor(color);
        block.PlayPopIn();
        return block;
    }
}
```

(b) 매칭 시 사라짐 처리:
```csharp
void RemoveMatched(List<RuneBlock> matched) {
    foreach (var b in matched) {
        b.PlayDestroyAndKill(() => {
            // 점수 가산, 충전 게이지 + 등
            ScoreManager.Add(100);
        });
    }
}
```

(c) 매칭 판정에서 색 비교:
```csharp
bool Match3(RuneBlock a, RuneBlock b, RuneBlock c) {
    if (a == null || b == null || c == null) return false;
    return a.Matches(b) && a.Matches(c);
}
```

(d) 본인 기존 enum 과 매핑이 필요한 경우 (예: BlockColor):
```csharp
public static class ColorBridge {
    public static RuneColor ToRune(BlockColor c) => c switch {
        BlockColor.Red    => RuneColor.Ruby,
        BlockColor.Orange => RuneColor.Amber,
        BlockColor.Yellow => RuneColor.Citrine,
        BlockColor.Green  => RuneColor.Emerald,
        BlockColor.Blue   => RuneColor.Sapphire,
        BlockColor.Purple => RuneColor.Amethyst,
        _ => RuneColor.Ruby,
    };
}
```

# 5단계 — 검증 & 안내
- 모든 새 .cs 파일이 컴파일 가능한지 (using 누락, 타입 불일치 등) 검토
- 사용자에게 다음 안내:
  * Unity Editor → "HexaPuzzle → RuneBlocks → ⚡ Do Everything" 메뉴 실행
  * 6장 스프라이트 + 프리팹 + 데모 씬이 생성됨
  * 데모 씬 ▶ Play 로 6 컬러 동작 확인
  * 본인 보드 매니저의 spawn 메서드에 RuneBlock.prefab 참조 추가
  * 첫 매칭 테스트 후 색·룬이 의도대로 표시되는지 확인

# 주의
- .meta GUID 변경 금지
- 기존 코드 수정 전 git status 확인 / 백업
- 한국어 주석은 유지
- 컴파일 안 되는 코드는 절대 작성하지 말 것 — 불확실하면 사용자에게 질문
````

---

## 📐 통합 후 코드 예시

### 가장 단순한 통합 (전체 보드 1줄로 채우기)
```csharp
using HexaPuzzle.RuneBlocks;

public class TestBoard : MonoBehaviour {
    public GameObject runeBlockPrefab;

    void Start() {
        for (int i = 0; i < 6; i++) {
            var go = Instantiate(runeBlockPrefab, new Vector3(i * 1.4f - 3.5f, 0, 0), Quaternion.identity);
            go.GetComponent<RuneBlock>().SetColor((RuneColor)i);
        }
    }
}
```

### 본인의 헥사 그리드와 결합
```csharp
public class HexBoard : MonoBehaviour {
    public GameObject runeBlockPrefab;
    public int width = 7, height = 8;

    RuneBlock[,] grid;

    void Start() {
        grid = new RuneBlock[width, height];
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                var color = (RuneColor)Random.Range(0, 6);
                var pos = HexToWorld(x, y);
                var go = Instantiate(runeBlockPrefab, pos, Quaternion.identity, transform);
                var b = go.GetComponent<RuneBlock>();
                b.SetColor(color);
                b.PlayPopIn();
                grid[x, y] = b;
            }
        }
    }
}
```

---

## 🔧 자주 발생하는 이슈

### 스프라이트가 너무 작거나 크다
프리팹의 Transform Scale 을 조정하거나, `RuneBlockSetupMenu.SpriteSize` 를 변경 후 재생성. PPU 가 256 이므로 Scale 1 일 때 1 world unit = 256px.

### 블록끼리 가려지지 않게 정렬을 조정하고 싶다
`SpriteRenderer.sortingOrder` 또는 `sortingLayerName` 을 코드에서 조정. 기본은 10.

### 색이 너무 어둡거나 채도가 다르게 보인다
OKLCH 색상 공간에서 명도 L=0.68, C=0.20 로 생성됨. 톤 조정이 필요하면 `RuneBlockSetupMenu.GenerateRuneStone` 내의 `OklchToRgb(L, C, hue)` 인자를 수정 후 재생성.

### 룬 문양만 교체하고 싶다
`RuneBlockSetupMenu.GetRuneStrokes(RuneColor)` 에 정의된 polyline 좌표(SVG viewBox 0..200 공간)를 편집 후 재생성.

### 임포트 후 `[meta] disagrees with` 경고
.meta 파일이 빠졌거나 다른 GUID 와 충돌. 패키지 폴더를 통째로 다시 복사 (반드시 .meta 포함).

---

## 📦 패키지 메타데이터

- **공개 API**: `RuneBlock`, `RuneColor`, `RuneColorMeta`
- **에디터 메뉴**: `HexaPuzzle → RuneBlocks → ⚡ Do Everything`
- **생성 자산**: 6 스프라이트 + 1 프리팹 + 1 데모 씬
- **단일 프리팹**: 6 컬러를 코드로 토글하는 방식 (프리팹 수 최소화)

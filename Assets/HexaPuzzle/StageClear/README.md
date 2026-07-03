# 스테이지 클리어 팝업 — Unity 에셋 사용 가이드

스테이지 클리어 시 표시되는 결과 모달입니다. 두 가지 테마(밝은 `Parchment`, 어두운 `Mystic`)로 제공되며, 한 줄 호출로 어디서나 띄울 수 있습니다.

---

## 📦 무엇이 들어있나요?

- **3개 런타임 스크립트** — `StageClearPopup`(UI 컨트롤러), `StageClearController`(정적 라우터), `StageClearData`(결과 구조체 + 팔레트)
- **1개 에디터 메뉴** — `StageClearSetupMenu` (프리팹/씬 자동 생성, 스프라이트 절차 생성)
- **자동 생성 스프라이트 3장** — 둥근 사각형(`SC_Round`), 알약(`SC_Pill`), 별(`SC_Star`). 임포트 시 생성됩니다.
- **2개 프리팹** — `StageClearPopup_Parchment.prefab`, `StageClearPopup_Mystic.prefab` (에디터 메뉴가 생성)

---

## ⚡ 빠른 시작 (3단계)

### 1️⃣ 임포트
`unity/Assets/HexaPuzzle/StageClear/` 폴더 전체를 본인 Unity 프로젝트의 `Assets/` 아래로 복사 (`.meta` 포함).

### 2️⃣ 자동 셋업
Unity 상단 메뉴: **`HexaPuzzle → StageClear → ⚡ Do Everything`**

→ 다음이 자동으로 생성됩니다:
- `Assets/HexaPuzzle/StageClear/Sprites/` — 스프라이트 3장
- `Assets/HexaPuzzle/StageClear/Prefabs/StageClearPopup_Parchment.prefab`
- `Assets/HexaPuzzle/StageClear/Prefabs/StageClearPopup_Mystic.prefab`
- `Assets/HexaPuzzle/StageClear/Scenes/StageClearDemo.unity` — ▶ Play 누르면 자동 표시

### 3️⃣ 본인 게임에 배치

본인 게임의 HUD Canvas(또는 별도 풀스크린 Canvas)에 두 프리팹 중 원하는 것을 드래그.
어디서나 정적 호출로 팝업을 띄울 수 있습니다:

```csharp
using HexaPuzzle.StageClear;

void OnStageCleared(int score) {
    StageClearController.Show(new StageClearResult {
        stars = 3,
        score = score,
        gold  = 10,
        stageBest = PlayerPrefs.GetInt("stage1_best", 0),
        myBest    = PlayerPrefs.GetInt("my_best", 0),
        isNewStageBest = score > PlayerPrefs.GetInt("stage1_best", 0),
        isNewMyBest    = score > PlayerPrefs.GetInt("my_best", 0),
    });
}
```

---

## 🎮 사용 방법

### Show()/Hide() — 가장 단순한 사용

씬에 `StageClearPopup_*` 프리팹 인스턴스가 하나만 있으면 `StageClearController.Show()` 가 자동으로 찾아서 띄웁니다.

```csharp
StageClearController.Show(new StageClearResult {
    stars = 2, score = 14320, gold = 6,
    stageBest = 15000, myBest = 20100,
    isNewStageBest = false, isNewMyBest = false,
});
StageClearController.Hide();
```

### 확인 버튼 클릭 시 동작 지정

프리팹 인스턴스의 `StageClearPopup` 컴포넌트 → `On Confirm` 이벤트에 원하는 메서드 연결.
또는 코드에서:

```csharp
var popup = FindObjectOfType<StageClearPopup>();
popup.onConfirm.AddListener(() => {
    SceneManager.LoadScene("Lobby");
});
```

확인 버튼은 클릭 시 `onConfirm` 발사 후 자동으로 `Hide()` 합니다.

### 테마 동적 전환

```csharp
popup.theme = StageClearTheme.Mystic;
```
※ 색은 자동으로 다시 적용되지 않습니다. 런타임 테마 전환이 필요하면 두 프리팹을 모두 배치하고 활성화만 토글하는 방법을 권장.

---

## 📐 API 요약

```csharp
namespace HexaPuzzle.StageClear;

// 결과 데이터
public struct StageClearResult {
    public int stars;            // 0..3
    public int score, gold;
    public int stageBest, myBest;
    public bool isNewStageBest, isNewMyBest;
    public static StageClearResult Demo(int stars=3, int score=19980, int gold=10, bool isNew=true);
}

// 메인 컴포넌트
public class StageClearPopup : MonoBehaviour {
    public StageClearTheme theme;
    public UnityEvent onConfirm;
    public float showDuration, hideDuration, starStagger;
    public void Show(StageClearResult r);
    public void Hide();
    public void HandleConfirm(); // 버튼이 호출
}

// 정적 라우터
public static class StageClearController {
    public static void Show(StageClearResult r);
    public static void Hide();
}
```

---

## 🎨 디자인 노트

- **별 3개** — 클리어 등급. 가운데 별이 살짝 위로 올라와 시각적 리듬 형성.
- **점수 패널** — 안쪽 inset 박스로 점수를 강조, 그 아래 골드 보상 알약.
- **BEST 행 2줄** — 동일한 시각 스타일로 통일. 신기록 시에만 NEW 배지 표시.
- **확인 버튼** — Parchment 는 코랄, Mystic 은 골드. 깊이감을 주는 그림자.
- **애니메이션** — 카드 스케일 팝업 + 별 3개 순차 바운스. `Time.unscaledDeltaTime` 사용으로 일시정지 중에도 정상 재생.

---

## 🔧 트러블슈팅

자주 발생하는 이슈와 해결법은 `TROUBLESHOOTING.md` 참조.

---

## 📦 메타데이터

- **언어**: C# (.NET Standard 2.0)
- **Unity 최소 버전**: 2020.3 LTS
- **종속성**: `com.unity.ugui` (UnityEngine.UI)
- **TextMeshPro 필요**: ❌ 아니요 (Legacy Text 사용 — 한글 폰트만 추가하면 즉시 동작)
- **렌더 파이프라인**: Built-in / URP / HDRP 모두 OK
- **모바일**: iOS / Android 둘 다 OK

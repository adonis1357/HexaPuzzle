# 트러블슈팅 — Stage Clear 팝업

## Q. 한글이 □ 박스로 보여요
A. Unity Legacy Text(`UnityEngine.UI.Text`) 의 기본 폰트(`Arial`)는 한글 글리프가 없습니다. 해결법:
- `Assets/Fonts/` 에 한글 TTF(예: NanumGothic, Pretendard, NotoSansKR) 추가
- 프리팹 안의 모든 Text 컴포넌트 `Font` 를 그 폰트로 교체
- 또는 모든 Text 를 TextMeshProUGUI 로 교체 후 한글 SDF 폰트 사용

## Q. 확인 버튼이 안 눌려요
A. 거의 다음 중 하나:
- 씬에 `EventSystem` 이 없음 (`GameObject → UI → Event System`)
- Canvas 에 `GraphicRaycaster` 가 없음 (Canvas 자동 추가 컴포넌트라 보통은 있음)
- 다른 풀스크린 UI 가 위에 떠 있어서 raycast 를 가로챔
- Backdrop 의 `raycastTarget` 이 false (이건 의도된 동작 — 클릭으로 닫히게 하려면 true 로)

## Q. 팝업이 화면 어딘가 잘못된 위치에 나와요
A. Canvas 설정 확인:
- Render Mode 가 `Screen Space - Overlay` 또는 `Screen Space - Camera`
- CanvasScaler 의 UI Scale Mode 가 `Scale With Screen Size`
- Reference Resolution 이 의도한 해상도와 비슷 (1080×1920 / 1920×1080 등)
- 팝업 root 의 RectTransform 이 stretch (anchorMin=0,0 / anchorMax=1,1)

## Q. 점수가 너무 큰 숫자일 때 잘려서 보여요
A. `ScoreValue` Text 의 RectTransform 폭이 모자란 것. 다음 중:
- `HorizontalOverflow = Overflow` 로 (기본값이 그래야 함)
- 폰트 크기를 줄이거나 `Best Fit` 활성화
- 또는 점수를 짧게 포맷 (`{score:N0}` → `{score / 1000}K`)

## Q. 별 모양이 깨끗하지 않고 픽셀 계단처럼 보여요
A. `SC_Star.png` 가 128×128 으로 생성되는데 화면에서 더 크게 그리면 보간이 안 됨. 두 가지:
- `HexaPuzzle/StageClear/Sprites/SC_Star.png` 의 임포트 설정 → `Filter Mode = Bilinear`
- 또는 메뉴에서 더 큰 사이즈로 재생성 (`StageClearSetupMenu.GenerateStar` 의 `size: 256`)

## Q. 카드 모서리가 너무 각져요 / 너무 둥글어요
A. 절차적 생성된 둥근 사각형 스프라이트의 radius 가 결정. `StageClearSetupMenu.EnsureSprites()` 에서 `GenerateRound(..., radius: 20)` 의 숫자를 조정 후 메뉴 재실행.

## Q. 신기록 NEW 배지가 아무 때나 떠요
A. `isNewStageBest` / `isNewMyBest` 를 호출 측에서 정확히 비교해 넘겨야 함:
```csharp
isNewStageBest = score > prevStageBest, // == 이 아니라 >
```

## Q. 두 팝업이 동시에 떠요
A. `StageClearController.Show()` 는 자동으로 씬 안의 `StageClearPopup` 을 검색해서 사용. 두 테마 프리팹을 모두 배치했다면 사용하지 않는 쪽을 `SetActive(false)` 처리하거나, `StageClearController.popup` 에 명시적으로 할당.

## Q. 일시정지 상태에서 호출하면 애니메이션이 안 돌아요
A. 이미 `Time.unscaledDeltaTime` 을 사용하도록 구현되어 있어 `Time.timeScale = 0` 에서도 동작해야 함. 안 된다면 `StageClearPopup.gameObject.activeSelf` 와 `canvasGroup.alpha` 를 확인.

## Q. 빌드 후 모바일에서 한글이 안 보여요
A. Legacy Text 의 fallback 폰트 문제. 모바일에선 시스템 한글 폰트가 자동 fallback 되지 않을 수 있음. 명시적으로 한글 TTF 를 프로젝트에 포함하고 모든 Text 의 Font 를 거기로 지정.

## Q. iOS 빌드 시 IL2CPP 에러
A. 본 패키지는 IL2CPP 호환 코드만 사용. 만약 에러가 나면 `UnityEvent` 리플렉션 관련일 수 있으니 `link.xml` 에 `<assembly fullname="UnityEngine.UI" preserve="all"/>` 추가.

## Q. CanvasGroup 이 fade 가 안 되고 갑자기 나타나요
A. `Show()` 진입 시 `canvasGroup.alpha = 0` 으로 강제 리셋함. 하지만 만약 CanvasGroup 컴포넌트가 root 가 아닌 다른 곳에 붙어 있으면 그쪽이 alpha=1 로 강제될 수 있음. Hierarchy 에서 CanvasGroup 이 root 에만 있는지 확인.

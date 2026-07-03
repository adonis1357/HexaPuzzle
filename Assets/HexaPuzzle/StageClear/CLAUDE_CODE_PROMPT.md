# Claude Code 통합 지시서 — Stage Clear 팝업

스테이지 클리어 시 결과 모달(별 등급, 점수, 골드 보상, BEST 갱신)을 본인의 헥사퍼즐 프로젝트에 통합합니다.

---

## 🚀 한 줄 프롬프트 (가장 빠름)

```
이 Unity 프로젝트의 Assets/ 폴더에 /tmp/hexa-package/unity/Assets/HexaPuzzle/StageClear 를 통합하고,
Assets/HexaPuzzle/StageClear/CLAUDE_CODE_PROMPT.md 의 5단계 워크플로우를 그대로 실행해줘.
내 게임의 스테이지 클리어 지점을 찾아서 StageClearController.Show() 를 호출하도록 자동 연결해줘.
```

---

## 📋 5단계 자동 통합 워크플로우 (Claude Code에 그대로 복붙)

````
이 작업을 단계별로 진행해줘. 각 단계 끝나면 결과를 보고하고 다음으로 넘어가.

# 작업 개요
헥사퍼즐의 스테이지 클리어 팝업을 통합. 팝업이 보여줄 정보:
- 별 등급 (0~3)
- 획득 점수
- 골드 보상 (+N)
- 이 스테이지의 직전 최고 점수 (STAGE BEST)
- 전체 최고 점수 (MY BEST)
- 신기록 여부 (NEW 배지)

패키지 경로: /tmp/hexa-package/unity/Assets/HexaPuzzle/StageClear/
(다른 위치면 사용자에게 물어봐)

# 1단계 — 패키지 복사
- /tmp/hexa-package/unity/Assets/HexaPuzzle/StageClear/ 폴더 전체를 Assets/HexaPuzzle/ 아래로 복사
- .meta 파일도 모두 함께 복사 (Unity GUID 보존)
- 복사된 파일 목록 보고
- Unity Editor 가 열려있다면 사용자에게 "메뉴 HexaPuzzle → StageClear → ⚡ Do Everything 을 실행해줘" 라고 안내

# 2단계 — 프로젝트 분석
다음을 찾아서 보고:
- Unity 버전 (ProjectSettings/ProjectVersion.txt)
- 메인 게임 씬 (.unity 파일들)
- 스테이지 클리어가 판정되는 메서드:
  - "Clear", "Complete", "Win", "Success" 등 단어로 grep
  - 자주 보이는 이름: OnStageClear, CompleteStage, GameWin, LevelComplete, CheckClearCondition
- 점수가 보관되는 변수/매니저 (ScoreManager, GameData, PlayerScore 등)
- 골드/재화가 보관되는 곳 (CurrencyManager, GoldManager, Wallet 등)
- 별 등급 계산이 이미 있는지 (StarRating, GradeCalculator 등)
- BEST 점수 저장 위치 (PlayerPrefs, ScriptableObject SO, JSON 등)

각 항목을 파일 경로 + 메서드 시그니처 한 줄로 표 정리해서 보여줘.

# 3단계 — 통합 계획 제시
2단계 결과를 바탕으로 사용자에게:
- 어느 메서드 끝에 StageClearController.Show() 를 호출할지
- 별 등급은 어떻게 계산할지 (이미 있다면 그대로 사용, 없으면 점수 임계치 제안)
- 골드 보상은 어떻게 계산할지 (점수의 N%, 별 등급당 N개 등 — 사용자에게 옵션 제시)
- BEST 저장/조회를 PlayerPrefs 로 할지, 기존 시스템에 위임할지

확인을 받은 후에만 다음 단계로.

# 4단계 — 통합 코드 작성
승인 후 다음 작업:

(a) 본인 게임의 클리어 판정 메서드에 호출 추가:
```csharp
using HexaPuzzle.StageClear;

void OnStageCleared() {
    // ... 기존 정리 로직 ...

    int score = scoreManager.CurrentScore;
    int prevStageBest = PlayerPrefs.GetInt($"stage_{currentStage}_best", 0);
    int prevMyBest    = PlayerPrefs.GetInt("my_best", 0);

    int stars = CalculateStars(score);  // 사용자 게임의 등급 계산
    int gold  = stars * 10;             // 또는 다른 보상 공식

    bool newStageBest = score > prevStageBest;
    bool newMyBest    = score > prevMyBest;

    if (newStageBest) PlayerPrefs.SetInt($"stage_{currentStage}_best", score);
    if (newMyBest)    PlayerPrefs.SetInt("my_best", score);

    StageClearController.Show(new StageClearResult {
        stars = stars,
        score = score,
        gold  = gold,
        stageBest = Mathf.Max(score, prevStageBest),
        myBest    = Mathf.Max(score, prevMyBest),
        isNewStageBest = newStageBest,
        isNewMyBest    = newMyBest,
    });
}
```

(b) 확인 버튼이 눌렸을 때의 동작 — 보통 로비로 이동:
```csharp
void Start() {
    var popup = FindObjectOfType<StageClearPopup>();
    if (popup != null) popup.onConfirm.AddListener(GoToLobby);
}

void GoToLobby() {
    UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
}
```

(c) 두 가지 테마 중 어느 걸 사용할지:
- 인게임 톤이 어두우면 → `StageClearPopup_Mystic` 프리팹 사용
- 밝은 톤이나 양피지 느낌 → `StageClearPopup_Parchment` 프리팹 사용
- 메인 씬의 Canvas 자식으로 둘 중 하나를 드래그하거나, 별도 풀스크린 Canvas 에 배치

# 5단계 — 검증 & 안내
- 모든 새 .cs 파일이 컴파일 가능한 형태인지 (using 누락, 타입 불일치 등) 검토
- 사용자에게 다음 안내:
  * Unity Editor → "HexaPuzzle → StageClear → ⚡ Do Everything" 메뉴 실행
  * Sprites 3장 + Prefab 2개 + 데모 씬이 생성됨
  * 데모 씬 ▶ Play 로 두 테마 모두 동작 확인
  * 메인 씬에 원하는 테마의 프리팹 1개 배치
  * 본인 클리어 판정 코드에서 한 번 클리어해 보고 팝업이 뜨는지 확인

# 주의
- .meta GUID 변경 금지
- 기존 코드 수정 전 git status 확인 / 백업
- 한국어 주석은 유지
- 컴파일 안 되는 코드는 절대 작성하지 말 것 — 불확실하면 사용자에게 질문
- 한글 문자가 표시 안 되면 README 의 "한글 폰트" 섹션 안내
````

---

## 📐 통합 후 코드 예시

### 가장 단순한 통합
```csharp
using HexaPuzzle.StageClear;

public class GameLoop : MonoBehaviour {
    public ScoreManager scoreManager;
    public int currentStage;

    public void OnStageCleared() {
        int score = scoreManager.CurrentScore;
        StageClearController.Show(new StageClearResult {
            stars = score >= 18000 ? 3 : score >= 12000 ? 2 : 1,
            score = score,
            gold  = 10,
            stageBest = score,
            myBest    = score,
            isNewStageBest = true,
            isNewMyBest    = true,
        });
    }
}
```

### BEST 저장까지 포함한 정식 통합
```csharp
public void OnStageCleared() {
    int score = scoreManager.CurrentScore;
    string stageKey = $"best_stage_{currentStage}";

    int prevStageBest = PlayerPrefs.GetInt(stageKey, 0);
    int prevMyBest    = PlayerPrefs.GetInt("best_all", 0);

    bool newStageBest = score > prevStageBest;
    bool newMyBest    = score > prevMyBest;

    if (newStageBest) PlayerPrefs.SetInt(stageKey, score);
    if (newMyBest)    PlayerPrefs.SetInt("best_all", score);

    StageClearController.Show(new StageClearResult {
        stars = CalculateStars(score),
        score = score,
        gold  = CalculateGold(score),
        stageBest = Mathf.Max(score, prevStageBest),
        myBest    = Mathf.Max(score, prevMyBest),
        isNewStageBest = newStageBest,
        isNewMyBest    = newMyBest,
    });
}

int CalculateStars(int s) => s >= 18000 ? 3 : s >= 12000 ? 2 : 1;
int CalculateGold (int s) => 10 + s / 5000; // 점수 5천당 +1
```

---

## 🔧 자주 발생하는 이슈 + 해결 프롬프트

### 한글이 □ 로 깨져 보임
```
Unity Legacy Text 기본 폰트는 한글이 없음. 다음 중 하나로 해결:
1. NanumGothic 등 한글 TTF 를 Assets/Fonts/ 에 추가
2. 프리팹 내부의 모든 Text 컴포넌트의 Font 를 그 한글 폰트로 교체
3. 또는 StageClearPopup 의 모든 Text 를 TextMeshProUGUI 로 교체하고 한글 SDF 폰트 사용
```

### Show() 호출했는데 팝업이 안 보임
```
체크리스트:
1. 씬에 StageClearPopup_Parchment 또는 _Mystic 프리팹이 배치되어 있는가
2. 그 프리팹의 GameObject 가 비활성 상태로 시작하더라도 부모 Canvas 는 활성인가
3. Canvas 의 SortOrder 가 다른 UI 보다 위에 있는가 (필요시 별도 Canvas with sortOrder=100)
4. EventSystem 이 씬에 있는가 (확인 버튼 입력용)
```

### 두 팝업이 동시에 떠 있음
```
StageClearController.Show() 는 자동 검색 시 첫 번째로 찾은 팝업을 사용함.
씬에 두 팝업을 모두 두려면 명시적으로 다음과 같이 사용:
  controller.popup = useMystic ? mysticPopup : parchmentPopup;
또는 사용하지 않는 쪽을 SetActive(false) 처리.
```

### 시간 정지 중에도 애니메이션이 재생되어야 함
```
StageClearPopup 의 애니메이션은 이미 Time.unscaledDeltaTime 을 사용함.
Time.timeScale = 0 인 상태에서도 정상 동작.
```

### 점수 천 단위 콤마가 안 나옴
```
StageClearPopup.ApplyResult 에서 score.ToString("N0") 사용 중이므로
컴퓨터의 Culture 와 무관하게 콤마가 표시됨. 안 보이면 Text 컴포넌트의
Font Size 가 너무 커서 잘려있을 가능성. RectTransform 크기 확인.
```

---

## 📦 패키지 메타데이터

- **공개 API**:
  - `StageClearController.Show(StageClearResult)` — 어디서나 호출
  - `StageClearController.Hide()`
  - `StageClearPopup.Show(result)` / `.Hide()` / `.onConfirm`
- **테마**: `StageClearTheme.Parchment`(밝음), `StageClearTheme.Mystic`(어두움)
- **자동 셋업 메뉴**: `HexaPuzzle → StageClear → ⚡ Do Everything`

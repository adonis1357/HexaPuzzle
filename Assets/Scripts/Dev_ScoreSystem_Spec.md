# 점수 시스템 & HUD UI 개발 명세서

> **작성일:** 2026-02-14
> **작성자:** 개발 PM
> **수신:** 개발팀장
> **문서 버전:** v1.0

---

## 1. 개요

### 1-1. 배경 및 목적

기존에는 블록 제거 개수만 단순 합산하여 점수를 매기고, 텍스트만 갱신하는 수준이었습니다.
이번 업데이트에서는 모바일 퍼즐 게임(Royal Match, Candy Crush 등) 수준의 **체계적인 점수 책정 방식**과 **시각적 피드백 UI**를 도입합니다.

**핵심 변경 내용:**
- 블록 티어별 기본 점수 + 매치 크기 보너스 + 연쇄 배율 + 콤보 배율의 **다층 점수 산출 공식** 적용
- 블록 제거 위치에 떠오르는 **점수 팝업** 추가
- 연속 매치 시 화면 상단에 **콤보 표시** 추가
- 이동 횟수에 **원형 게이지 + 위험 알림** 시각 효과 추가
- 스테이지 클리어 시 **점수 브레이크다운 팝업** 표시

### 1-2. 변경 파일 목록

| 구분 | 파일 경로 | 변경 규모 |
|:----:|----------|:--------:|
| 신규 | `Managers/ScoreCalculator.cs` | 96줄 |
| 신규 | `UI/ScorePopupManager.cs` | 272줄 |
| 신규 | `UI/ComboDisplay.cs` | 207줄 |
| 수정 | `Managers/ScoreManager.cs` | 전면 재작성 (295줄) |
| 수정 | `Managers/GameManager.cs` | 중규모 |
| 수정 | `Managers/UIManager.cs` | 대규모 |
| 수정 | `Core/BlockRemovalSystem.cs` | 소규모 |
| 수정 | `Utils/VisualConstants.cs` | 소규모 |

> 모든 경로의 루트는 `Assets/Scripts/` 입니다.

---

## 2. 점수 계산 시스템

### 2-1. ScoreCalculator (신규 정적 유틸리티 클래스)

**경로:** `Managers/ScoreCalculator.cs`
**네임스페이스:** `JewelsHexaPuzzle.Managers`

이 클래스는 인스턴스 없이 호출할 수 있는 정적(static) 메서드만 제공하며, 순수 계산 로직만 포함합니다.

#### 블록 티어별 기본 점수

| BlockTier | 점수 | 설명 |
|:---------:|:----:|------|
| Normal (기본) | 50 | 일반 보석 |
| Tier1 | 75 | 1등급 보석 |
| Tier2 | 100 | 2등급 보석 |
| Tier3 | 150 | 3등급 보석 |
| ProcessedGem | 200 | 가공 보석 |

#### 매치 크기에 따른 추가 보너스

| 매치 블록 수 | 추가 보너스 | 생성되는 특수 블록 |
|:-----------:|:---------:|:-----------------:|
| 3개 (기본) | +0 | 없음 |
| 4개 | +100 | 드릴(Drill) |
| 5개 | +250 | 폭탄(Bomb) |
| 6개 | +400 | 레이저(Laser) |
| 7개 이상 | +600 | 도넛(Rainbow) |

#### 연쇄(캐스케이드) 배율

블록 제거 후 낙하로 인해 추가 매치가 발생하면 연쇄 깊이(depth)가 증가합니다.

| 연쇄 단계 | 배율 |
|:---------:|:----:|
| 0단계 (최초) | 1.0배 |
| 1단계 | 1.2배 |
| 2단계 | 1.4배 |
| 3단계 | 1.6배 |
| 4단계 이상 | 1.8배 (상한) |

**산출 공식:** `min(1.0 + 단계 * 0.2, 1.8)`

#### 스테이지 클리어 보너스

| 항목 | 계산 방식 |
|------|----------|
| 남은 턴 보너스 | 남은 턴 수 x 200 |
| 효율 보너스 | 턴 사용률 50% 미만: +2,000 / 70% 미만: +1,000 / 그 외: 없음 |

### 2-2. 최종 점수 산출 공식

#### (가) 일반 매치 점수

```
기본 점수 = 제거 블록 수 x 50 + 매치 크기 보너스
연쇄 적용 = 반올림(기본 점수 x 연쇄 배율)
최종 점수 = 반올림(연쇄 적용 x 콤보 배율)
```

**예시 — 5매치, 연쇄 2단계, 콤보 2회차:**
```
기본 점수 = 5 x 50 + 250 = 500
연쇄 적용 = 500 x 1.4 = 700
콤보 배율 = 1.0 + 2 x 0.5 = 2.0
최종 점수 = 700 x 2.0 = 1,400점
```

#### (나) 특수 블록 점수

각 특수 블록 시스템(Drill, Bomb 등)이 자체적으로 산출한 `rawScore`에 연쇄 배율과 콤보 배율을 곱합니다.

```
최종 점수 = 반올림(rawScore x 연쇄 배율 x 콤보 배율)
```

#### (다) 콤보 배율

```
콤보 배율 = 1.0 + min(현재 콤보, 최대 콤보) x 0.5
```

| 설정 항목 | 기본값 | Inspector 조정 |
|----------|:------:|:--------------:|
| 최대 콤보 | 4 | 가능 |
| 콤보당 증가분 | 0.5 | 가능 |
| 콤보 리셋 시간 | 3초 | 가능 |
| 최대 배율 | 3.0배 | (= 1.0 + 4 x 0.5) |

> 마지막 매치 이후 3초 이내에 추가 매치가 없으면 콤보가 0으로 초기화됩니다.

---

## 3. ScoreManager 상세

**경로:** `Managers/ScoreManager.cs`

### 3-1. Inspector 조정 가능 항목

| 필드명 | 타입 | 기본값 | 용도 |
|--------|:----:|:------:|------|
| `comboMultiplierBase` | int | 1 | 콤보 기본 배율 |
| `comboMultiplierIncrement` | float | 0.5 | 콤보 1회당 배율 증가분 |
| `comboResetTime` | float | 3.0 | 콤보 초기화까지의 유휴 시간(초) |
| `maxCombo` | int | 4 | 콤보 상한 (이 이상은 배율이 증가하지 않음) |

### 3-2. 이벤트 목록

| 이벤트명 | 시그니처 | 발생 시점 |
|---------|---------|----------|
| `OnScoreChanged` | `Action<int>` | 점수가 변경될 때 (현재 총점 전달) |
| `OnComboChanged` | `Action<int>` | 콤보 횟수가 변경될 때 (현재 콤보 수 전달) |
| `OnScorePopup` | `Action<int, Vector3>` | 점수 획득 시 (획득 점수, 월드 좌표 전달) |

### 3-3. 주요 메서드

| 메서드 | 역할 | 호출 위치 |
|--------|------|----------|
| `AddMatchScore(int blockCount, int cascadeDepth, Vector3 position)` | 일반 매치 점수 계산 및 가산 | GameManager.OnBlocksRemoved |
| `AddSpecialBlockScore(int rawScore, int cascadeDepth, Vector3 position)` | 특수 블록 점수 계산 및 가산 | GameManager.OnSpecialBlockCompleted |
| `AddScore(int baseScore, Vector3? popupPosition)` | 하위 호환용 범용 점수 가산 | (기존 코드) |
| `CalculateStageClearBonus(int remainingTurns, int turnLimit)` | 클리어 보너스 계산, 적용, 요약 데이터 반환 | GameManager.StageClear |
| `ResetScore()` | 점수/콤보/스테이지 추적값 전체 초기화 | GameManager.RetryStage |

### 3-4. 스테이지 요약 데이터 (`StageSummaryData`)

스테이지 클리어 시 UI에 전달되는 구조체입니다.

| 필드 | 타입 | 설명 |
|------|:----:|------|
| `baseScore` | int | 스테이지 중 획득한 기본 점수 합계 |
| `remainingTurns` | int | 클리어 시점의 남은 턴 수 |
| `remainingTurnsBonus` | int | 남은 턴 보너스 점수 |
| `efficiencyBonus` | int | 효율 보너스 점수 |
| `totalScore` | int | 최종 총점 (기본 + 보너스 모두 포함) |
| `highScore` | int | 역대 최고 점수 |
| `isNewHighScore` | bool | 신기록 달성 여부 |
| `starRating` | int | 별 등급 (0~3) |
| `maxComboReached` | int | 해당 스테이지에서 달성한 최대 콤보 |

### 3-5. 별 등급 산정 기준

목표 점수는 `턴 제한 x 500`으로 자동 계산됩니다.

| 달성 비율 | 등급 |
|:---------:|:----:|
| 200% 이상 | 3성 |
| 150% 이상 | 2성 |
| 100% 이상 | 1성 |
| 100% 미만 | 0성 |

### 3-6. 데이터 저장

- **저장 방식:** `PlayerPrefs`
- **저장 키:** `"HighScore"`, `"TotalGold"`
- **저장 시점:** 하이스코어 갱신 시 자동 저장

---

## 4. 이벤트 흐름도

### 4-1. 일반 매치 점수 흐름

```
사용자가 블록 회전
  └→ RotationSystem.OnRotationComplete(매치 발견 = true)
       ├→ GameManager.UseTurn()  ─→  UIManager.UpdateTurnDisplay()
       └→ BlockRemovalSystem.ProcessMatches(매치 목록)
            └→ [BRS 내부] 블록 제거 + 평균 위치 계산
                 └→ OnBlocksRemoved(블록수, 연쇄깊이, 평균위치)
                      └→ GameManager.OnBlocksRemoved()
                           └→ ScoreManager.AddMatchScore()
                                ├→ OnScoreChanged  →  UIManager.UpdateScoreDisplay()   [카운팅 애니메이션]
                                ├→ OnScorePopup    →  ScorePopupManager.ShowPopup()    [팝업 표시]
                                └→ OnComboChanged  →  ComboDisplay.OnComboChanged()    [콤보 표시]
```

### 4-2. 특수 블록 점수 흐름

```
특수 블록 발동 (Drill / Bomb / Laser 등)
  └→ 각 시스템의 OnXxxComplete(점수)
       └→ GameManager.OnSpecialBlockCompleted(점수)
            └→ ScoreManager.AddSpecialBlockScore(점수, 연쇄깊이, 위치)
                 └→ (위 4-1과 동일한 이벤트 체인)
```

### 4-3. 스테이지 클리어 흐름

```
미션 완료 감지
  └→ GameManager.StageClear()
       └→ ScoreManager.CalculateStageClearBonus(남은턴, 전체턴)
            └→ StageSummaryData 반환
       └→ UIManager.ShowStageClearPopup(요약 데이터)
            └→ AnimateStageClearBreakdown 코루틴 실행
                 └→ 점수 항목별 순차 등장 + 별 팝 애니메이션
```

### 4-4. BlockRemovalSystem 이벤트 시그니처 변경

| 항목 | 내용 |
|------|------|
| **변경 전** | `public event Action<int> OnBlocksRemoved;` |
| **변경 후** | `public event Action<int, int, Vector3> OnBlocksRemoved;` |
| **매개변수** | `(int 제거블록수, int 연쇄깊이, Vector3 평균위치)` |

- `평균위치`: 제거된 블록들의 월드 좌표 평균값으로, 점수 팝업 표시 위치에 사용됩니다.
- `연쇄깊이`: BRS 내부에서 관리하며 `CurrentCascadeDepth` 프로퍼티로 외부에 노출됩니다.

> **주의:** 이 이벤트를 기존에 구독하던 코드가 있다면, 시그니처를 `Action<int, int, Vector3>`로 맞춰야 합니다.

---

## 5. UI 시스템 상세

### 5-1. 점수 팝업 (`ScorePopupManager`)

**경로:** `UI/ScorePopupManager.cs`
**생성 방식:** `GameManager.EnsureUIComponents()`에서 Canvas 하위에 런타임 자동 생성 (씬 수동 배치 불필요)

#### 동작 원리
1. `ScoreManager.OnScorePopup` 이벤트를 구독합니다.
2. 점수 획득 시, 블록이 제거된 월드 좌표를 캔버스 로컬 좌표로 변환하여 팝업을 표시합니다.
3. 오브젝트 풀(초기 12개)을 사용하며, 부족하면 자동으로 확장합니다.

#### 점수 구간별 팝업 스타일

| 점수 구간 | 글자 크기 | 색상 | 지속 시간 | 이동 거리 |
|:---------:|:---------:|:----:|:---------:|:---------:|
| 0 ~ 299 | 28px | 흰색 | 0.6초 | 80px |
| 300 ~ 999 | 34px | 금색 (#FFD700) | 0.8초 | 100px |
| 1,000 ~ 2,999 | 42px 굵게 | 주황색 (#FF8C00) | 1.0초 | 120px |
| 3,000 이상 | 50px 굵게 | 붉은 금색 (#FF4500) | 1.2초 | 140px |

#### 팝업 애니메이션 순서
1. **등장** (0 ~ 0.15초): 크기 0 → 1.2배 → 1.0배 (EaseOutBack)
2. **상승** (전체 구간): Y축 위로 이동 (EaseOutQuart), X축 +-15px 미세 흔들림
3. **투명도**: 전체 시간의 60%까지 완전 불투명, 나머지 40%에서 서서히 사라짐 (EaseInQuad)
4. **외곽선**: 검정(투명도 0.6), 1.5px 간격, 본문과 동일하게 사라짐

### 5-2. 콤보 표시 (`ComboDisplay`)

**경로:** `UI/ComboDisplay.cs`
**생성 방식:** 동일하게 런타임 자동 생성

#### 위치 및 크기
- **앵커:** 화면 상단 중앙 (0.5, 1.0)
- **Y 오프셋:** -120px (HUD 바 아래)
- **크기:** 300 x 60

#### 콤보 단계별 연출

| 콤보 | 표시 텍스트 | 글자 크기 | 색상 | 특수 효과 |
|:----:|:----------:|:---------:|:----:|:---------:|
| 0 ~ 1 | (숨김) | - | - | - |
| x2 | "COMBO x2" | 32 보통 | 노란색 | 페이드인 + 확대 |
| x3 | "COMBO x3" | 38 굵게 | 주황색 | 바운스 등장 |
| x4 이상 | "COMBO x4!" | 44 굵게 | 붉은 주황색 | 흔들림 효과 |

#### 애니메이션 상세
- **등장:** EaseOutBack 이징, 크기 1.3배 → 1.0배, 0.3초
- **x4 이상 흔들림:** 0.15초 동안 랜덤 오프셋 (최대 4px에서 점차 감소)
- **자동 숨김:** 마지막 콤보 후 1.5초 경과 시 0.3초에 걸쳐 사라짐
- **콤보 종료:** 콤보가 2 미만으로 떨어지면 즉시 페이드아웃

### 5-3. 점수 카운팅 애니메이션 (UIManager)

기존 `goldText`(점수 텍스트)에 적용되는 애니메이션입니다.

| 항목 | 내용 |
|------|------|
| 동작 | 현재 표시 중인 숫자에서 목표 숫자까지 빠르게 올라가는 연출 |
| 지속 시간 | 0.5초 (`VisualConstants.ScoreCountDuration`) |
| 이징 함수 | EaseOutQuart (처음에 빠르고 끝에서 느려짐) |
| 색상 변화 | 카운팅 중 금색(#FFD700) → 완료 후 0.3초에 걸쳐 원래 색상 복원 |
| 리셋 처리 | `score = 0` 전달 시 즉시 "0" 표시, 진행 중 코루틴 즉시 중단 |

### 5-4. 이동 횟수 UI (UIManager)

#### (가) 턴 텍스트 바운스

턴을 사용할 때마다 숫자에 짧은 바운스 효과를 줍니다.

| 항목 | 값 |
|------|:--:|
| 최대 확대율 | 1.15배 |
| 지속 시간 | 0.2초 |
| 이징 | EaseOutBack |
| 흐름 | 1.0배 → 1.15배 → 1.0배 |

#### (나) 원형 프로그레스 링

`moveProgressRing` (Image, Filled 타입)을 Inspector에서 연결하면 동작합니다.

| 항목 | 내용 |
|------|------|
| 채움 비율 | `남은 턴 / 최대 턴` |
| 100% ~ 30% | 흰색 |
| 30% ~ 17% | 주황색 (#FF9900) |
| 17% ~ 0% | 빨간색 |

#### (다) 위험 알림 펄스

남은 턴이 적을 때 숫자가 깜빡이는 효과입니다.

| 남은 턴 | 텍스트 색상 | 깜빡임 주기 | 투명도 범위 |
|:-------:|:-----------:|:----------:|:----------:|
| 6턴 이상 | 흰색 | 없음 | 1.0 고정 |
| 3 ~ 5턴 | 빨간색 | 0.5초/회 | 0.4 ~ 1.0 |
| 1 ~ 3턴 | 진한 빨간색 (#FF2626) | 0.3초/회 | 0.4 ~ 1.0 |

#### (라) 최대 턴 표시

- `moveMaxText`: "/30" 형식으로 최대 턴 수를 표시합니다.
- `GameManager.StartGameCoroutine()`에서 `SetMaxTurns(initialTurns)`를 호출하여 초기값을 설정합니다.

### 5-5. 스테이지 클리어 브레이크다운 (UIManager)

`ShowStageClearPopup(StageSummaryData summary)` 호출 시 동작합니다.

#### 연출 타임라인

| 시간 | 대상 | 내용 |
|:----:|------|------|
| 0.0초 | 팝업 전체 | 기존 등장 애니메이션 (0.3초) |
| 0.5초 | 타이틀 | "STAGE X CLEAR!" 표시 |
| 0.5초 | 기본 점수 | 0 → baseScore 카운팅 (0.4초, EaseOutQuart) |
| 0.85초 | 남은 턴 보너스 | 0 → remainingTurnsBonus 카운팅 (0.4초), 형식: "+3,600 (x18)" |
| 1.2초 | 효율 보너스 | 0 → efficiencyBonus 카운팅 (0.3초), 형식: "+2,000" |
| 1.5초 | 총점 | 0 → totalScore 카운팅 (0.6초) + 크기 강조 (1.0 → 1.2 → 1.0배, 0.2초) |
| 1.9초 | 별 등급 | 획득한 별 순차 등장 (0.15초 간격, EaseOutBack 크기 0 → 1.0) |

- **획득 별:** 금색 (#FFD700)
- **미획득 별:** 어두운 회색 (투명도 0.5)
- **시간 기준:** `Time.unscaledDeltaTime` 사용 (일시정지 중에도 정상 재생)

---

## 6. GameManager 변경 사항

### 6-1. UI 컴포넌트 자동 생성 (`EnsureUIComponents`)

`InitializeSystems()` 마지막 단계에서 호출됩니다.

| 대상 | 조건 | 동작 |
|------|------|------|
| ScorePopupManager | 씬에 없을 경우 | Canvas 하위에 자동 생성 |
| ComboDisplay | 씬에 없을 경우 | Canvas 하위에 자동 생성 |

> 씬에 미리 배치할 필요가 없으며, 이미 존재하면 중복 생성하지 않습니다.

### 6-2. 이벤트 핸들러 변경

#### 블록 제거 핸들러

```csharp
// [변경 전]
private void OnBlocksRemoved(int score)
{
    scoreManager.AddScore(score);
}

// [변경 후]
private void OnBlocksRemoved(int blockCount, int cascadeDepth, Vector3 avgPosition)
{
    scoreManager.AddMatchScore(blockCount, cascadeDepth, avgPosition);
}
```

#### 특수 블록 완료 핸들러

```csharp
private void OnSpecialBlockCompleted(int score)
{
    int cascadeDepth = blockRemovalSystem?.CurrentCascadeDepth ?? 0;
    scoreManager.AddSpecialBlockScore(score, cascadeDepth, Vector3.zero);
    // ... (이하 기존 로직 동일)
}
```

#### 스테이지 클리어

```csharp
private void StageClear()
{
    // ...
    if (scoreManager != null)
    {
        var summary = scoreManager.CalculateStageClearBonus(currentTurns, initialTurns);
        uiManager.ShowStageClearPopup(summary);
    }
    else
    {
        uiManager.ShowStageClearPopup(); // 하위 호환용
    }
}
```

### 6-3. 추가된 필드 및 프로퍼티

| 항목 | 용도 |
|------|------|
| `public int InitialTurns` (읽기 전용) | 스테이지 시작 시 배정된 턴 수 |
| `private bool bigBangTriggered` | 빅뱅 발동 여부 추적 |
| `SetMaxTurns(initialTurns)` 호출 | StartGameCoroutine에서 UI 초기화 시 사용 |

---

## 7. VisualConstants 추가 상수

**경로:** `Utils/VisualConstants.cs`

### 신규 추가 항목

| 구분 | 상수명 | 값 | 용도 |
|------|--------|:--:|------|
| 점수 팝업 | `PopupSmallSize` | 28 | 소형 팝업 글자 크기 |
| | `PopupMediumSize` | 34 | 중형 |
| | `PopupLargeSize` | 42 | 대형 |
| | `PopupEpicSize` | 50 | 최대형 |
| | `PopupBaseDuration` | 0.8 | 기본 지속 시간(초) |
| | `PopupTravelDistance` | 100 | 기본 이동 거리(px) |
| 콤보 표시 | `ComboScaleIn` | 1.3 | 등장 시 초기 확대율 |
| | `ComboAnimDuration` | 0.3 | 등장 애니메이션 시간(초) |
| | `ComboIdleTimeout` | 1.5 | 자동 숨김까지의 유휴 시간(초) |
| 점수 카운팅 | `ScoreCountDuration` | 0.5 | 카운팅 애니메이션 시간(초) |
| 이동 횟수 | `MoveBounceScale` | 1.15 | 바운스 최대 확대율 |
| | `MoveBounceDuration` | 0.2 | 바운스 시간(초) |
| | `MovePulseSpeed` | 0.5 | 위험 알림 깜빡임 주기(초) |

### 기존 변경 사항

| 항목 | 변경 전 | 변경 후 |
|------|:-------:|:-------:|
| `GetCascadeMultiplier` 상한 | 1.6 | 1.8 |

---

## 8. Unity Inspector 설정 안내

### 8-1. 자동 처리 항목 (별도 작업 불필요)

| 대상 | 처리 방식 |
|------|----------|
| ScorePopupManager | `EnsureUIComponents()`에서 Canvas 하위에 자동 생성 |
| ComboDisplay | 동일 |
| ScoreManager 참조 | `AutoFindReferences()`에서 자동 탐색 |

### 8-2. 수동 연결이 필요한 항목

아래 항목들은 UIManager의 Inspector에서 연결해야 해당 기능이 활성화됩니다.
**모든 항목에 null 체크가 되어 있으므로, 연결하지 않아도 기존 기능에는 영향이 없습니다.**

#### UIManager 새 SerializeField 목록

| 필드명 | 타입 | 용도 | 필수 여부 |
|--------|:----:|------|:--------:|
| `moveProgressRing` | Image | 원형 턴 게이지 (Filled 타입) | 선택 |
| `moveMaxText` | Text | "/30" 형식의 최대 턴 표시 | 선택 |
| `clearTitleText` | Text | "STAGE X CLEAR!" 타이틀 | 선택 |
| `clearBaseScoreText` | Text | 기본 점수 항목 | 선택 |
| `clearTurnBonusText` | Text | 남은 턴 보너스 항목 | 선택 |
| `clearEfficiencyBonusText` | Text | 효율 보너스 항목 | 선택 |
| `clearTotalScoreText` | Text | 최종 총점 항목 | 선택 |
| `starImages` | Image[] | 별 이미지 3개 (배열 크기 3) | 선택 |

#### moveProgressRing 설정 순서

1. Canvas 하위에 Image 오브젝트 생성
2. Image Type → **Filled**
3. Fill Method → **Radial 360**
4. Fill Origin → **Top**
5. Clockwise → **체크**
6. UIManager Inspector의 `moveProgressRing` 슬롯에 드래그

#### starImages 설정 순서

1. `stageClearPopup` 하위에 Image 오브젝트 3개 생성
2. 각각 별 스프라이트 할당
3. UIManager Inspector의 `starImages` 배열에 순서대로 할당

---

## 9. 디버그 로그 형식

운영 중 점수 관련 문제를 추적할 수 있도록, 주요 지점에 Debug.Log를 포함하고 있습니다.

| 상황 | 로그 형식 |
|------|----------|
| 일반 매치 점수 | `Score +{최종점수} (base: {기본}, cascade: x{연쇄배율}, combo: x{콤보배율}) Total: {총점}` |
| 특수 블록 점수 | `Special Score +{최종점수} (raw: {원점수}, cascade: x{연쇄배율}, combo: x{콤보배율}) Total: {총점}` |
| UI 자동 생성 | `[GameManager] ScorePopupManager auto-created` |
| UI 자동 생성 | `[GameManager] ComboDisplay auto-created` |

---

## 10. 점수 시뮬레이션 예시

### 예시 1: 기본 3매치 (첫 번째 매치)

```
기본 점수 = 3 x 50 + 0 = 150
연쇄 배율 = 1.0배 (첫 매치)
콤보 배율 = 1.5배 (콤보 1회)
최종 점수 = 150 x 1.0 x 1.5 = 225점
→ 팝업: 소형 (흰색, 28px)
```

### 예시 2: 5매치 + 연쇄 2단계 + 콤보 2회

```
기본 점수 = 5 x 50 + 250 = 500
연쇄 배율 = 1.4배 (2단계)
콤보 배율 = 2.0배 (콤보 2회)
최종 점수 = 500 x 1.4 x 2.0 = 1,400점
→ 팝업: 대형 (주황색, 42px, 굵게)
```

### 예시 3: 스테이지 클리어 (30턴 중 12턴 사용)

```
남은 턴 = 18
남은 턴 보너스 = 18 x 200 = 3,600
턴 사용률 = 12 / 30 = 40% (50% 미만)
효율 보너스 = 2,000
추가 보너스 합계 = 5,600
```

---

## 11. 주의사항

### 11-1. 하위 호환성

| 항목 | 상태 |
|------|------|
| `ScoreManager.AddScore()` | 기존 호출부를 위해 유지됨 |
| `UIManager.ShowStageClearPopup()` (매개변수 없는 버전) | 기존 호출부를 위해 유지됨 |
| `BlockRemovalSystem.OnBlocksRemoved` | **시그니처 변경됨** — 외부에서 이 이벤트를 구독하는 코드가 있다면 반드시 수정 필요 |

### 11-2. 알려진 제약사항

| 항목 | 설명 | 해결 방향 |
|------|------|----------|
| 블록 티어 미반영 | 현재 `CalculateMatchGroupScore`는 모든 블록을 Normal(50점)로 가정 | BRS에서 블록별 티어 정보를 전달하면 `GetBlockBaseScore(tier)` 활용 가능 |
| 빌트인 폰트 사용 | 점수 팝업/콤보 모두 `LegacyRuntime.ttf` 사용 | 커스텀 폰트나 TMP(TextMeshPro)로 교체 필요 시 해당 생성부 수정 |
| Canvas 좌표 변환 | Screen Space - Overlay 모드에서 `worldCamera`가 null이 됨 | 현재 코드에서 정상 동작하나, 의도치 않은 위치 차이 발생 시 확인 필요 |

### 11-3. 성능 관련 사항

| 항목 | 내용 |
|------|------|
| 오브젝트 풀 | 점수 팝업 12개 사전 생성, 부족 시 자동 확장 (GC 부하 최소화) |
| 애니메이션 방식 | 모든 UI 애니메이션은 코루틴 기반 (DOTween 등 외부 라이브러리 미사용) |
| Time 기준 | 게임 중 애니메이션: `Time.deltaTime` / 클리어 연출: `Time.unscaledDeltaTime` |

---

## 12. 테스트 체크리스트

### (가) 기능 테스트

- [ ] 3매치 시 블록 제거 위치에 점수 팝업이 표시되는가 (소형, 흰색)
- [ ] 5매치 시 팝업의 크기와 색상이 상위 단계로 변경되는가
- [ ] 연쇄 2회 이상 발생 시 Console 로그에서 연쇄 배율 증가가 확인되는가
- [ ] 연속 매치 시 콤보 숫자가 증가하고, 화면 상단에 콤보 텍스트가 표시되는가
- [ ] 3초간 매치가 없으면 콤보가 리셋되고 텍스트가 사라지는가
- [ ] 콤보 x4 이상에서 텍스트에 흔들림 효과가 적용되는가
- [ ] 점수 텍스트가 숫자 카운팅 애니메이션으로 올라가며, 금색 하이라이트가 표시되는가
- [ ] 턴 사용 시 턴 숫자에 바운스(1.15배) 효과가 적용되는가
- [ ] 남은 5턴 이하에서 숫자가 빨간색으로 변하며 깜빡이는가
- [ ] 남은 3턴 이하에서 깜빡임이 더 빨라지고 색상이 더 진해지는가
- [ ] `moveProgressRing` 연결 시 게이지가 줄어들며 색상이 전환되는가
- [ ] 스테이지 클리어 시 점수 항목이 순차적으로 등장하는가
- [ ] 별 등급이 EaseOutBack 애니메이션으로 표시되는가
- [ ] ScoreManager Inspector에서 `maxCombo`, `comboResetTime` 값 변경 후 정상 반영되는가

### (나) 통합 테스트

- [ ] 특수 블록 연쇄 발동 시 점수가 정상적으로 누적되는가
- [ ] BigBang 발생 시 다수의 팝업이 동시에 표시되어도 오류 없이 동작하는가
- [ ] 일시정지(`timeScale=0`) 상태에서 스테이지 클리어 연출이 정상 재생되는가
- [ ] 스테이지 재시작 시 점수, 콤보, 스테이지 추적값이 모두 0으로 초기화되는가
- [ ] 하이스코어가 PlayerPrefs에 정상적으로 저장/복원되는가

### (다) 예외 상황 테스트

- [ ] `moveProgressRing`을 연결하지 않았을 때 오류 없이 동작하는가
- [ ] `starImages`를 연결하지 않았을 때 오류 없이 동작하는가
- [ ] ScoreManager가 없는 상태에서 GameManager가 정상 동작하는가 (null 체크)
- [ ] Canvas가 2개 이상 존재할 때 ScorePopupManager/ComboDisplay 위치가 정상인가

---

## 13. 향후 확장 방향 (참고)

| 우선순위 | 항목 | 설명 |
|:--------:|------|------|
| 높음 | 블록 티어별 점수 반영 | BRS에서 블록 티어 정보를 함께 전달하여 정밀 계산 |
| 높음 | 커스텀 폰트/TMP 적용 | 현재 빌트인 폰트를 프로젝트 전용 폰트로 교체 |
| 중간 | 점수 밸런싱 도구 | ScoreCalculator 상수를 ScriptableObject로 분리하여 기획자가 직접 조정 |
| 중간 | 리더보드 연동 | `StageSummaryData`를 서버에 전송하는 인터페이스 추가 |
| 낮음 | 콤보 보상 시스템 | `maxComboReached` 기반 추가 보상 로직 |

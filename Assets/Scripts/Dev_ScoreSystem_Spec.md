# HexaPuzzle 통합 점수 시스템 기획서

**작성일:** 2026-02-16 (통합)
**작성자:** 기획 디렉터 / 개발 PM
**문서 버전:** v2.0 (5개 점수 기획서 통합)
**관련 코드:** ScoreCalculator.cs, ScoreManager.cs, BlockRemovalSystem.cs

> 이 문서는 기존 5개 점수 관련 기획서를 통합한 문서입니다.
> - Dev_ScoreSystem_Spec.md (v1.0)
> - Dev_SpecialBlockScoring_Spec.md
> - Dev_SpecialBlockScoring_TaskRequest.md
> - Dev_EnemyScoring_Spec.md
> - Dev_SpecialBlockCreationBonus_Spec.md

---

## 목차

1. [개요](#1-개요)
2. [블록 기본 점수](#2-블록-기본-점수)
3. [일반 매치 점수](#3-일반-매치-점수)
4. [특수 블록 발동 점수](#4-특수-블록-발동-점수)
5. [특수 블록 생성 가산점](#5-특수-블록-생성-가산점)
6. [적군 파괴 점수](#6-적군-파괴-점수)
7. [연쇄(캐스케이드) 배율](#7-연쇄캐스케이드-배율)
8. [콤보 배율](#8-콤보-배율)
9. [스테이지 클리어 보너스](#9-스테이지-클리어-보너스)
10. [최종 점수 산출 공식 요약](#10-최종-점수-산출-공식-요약)
11. [UI 시스템](#11-ui-시스템)
12. [이벤트 흐름도](#12-이벤트-흐름도)
13. [ScoreCalculator 확장 명세](#13-scorecalculator-확장-명세)
14. [ScoreManager 상세](#14-scoremanager-상세)
15. [점수 시뮬레이션 예시](#15-점수-시뮬레이션-예시)
16. [테스트 체크리스트](#16-테스트-체크리스트)

> **참고:** 도넛과 X블록은 동일한 특수 블록입니다. 생성 조건만 다릅니다 (7+매치 → DonutBlockSystem, 링매치 → XBlockSystem).

---

## 1. 개요

모바일 퍼즐 게임(Royal Match, Candy Crush 등) 수준의 체계적인 점수 책정 방식을 적용합니다.

**점수 획득 경로:**
- 일반 매치 → 블록 티어별 기본 점수 + 매치 크기 보너스
- 특수 블록 발동 → 시스템 기본점수 + 파괴 블록 티어별 점수
- 특수 블록 생성 → 생성 가산점
- 적군 파괴 → 적군별 기본 파괴 점수 + 제거 방법 배율 + 특수 상황 보너스
- 스테이지 클리어 → 남은 턴 보너스 + 효율 보너스

**모든 점수에 공통 적용:**
- 연쇄(cascade) 배율: 1.0x ~ 1.8x
- 콤보 배율: 1.0x ~ 3.0x

### 변경 파일 목록

| 구분 | 파일 경로 | 변경 규모 |
|:----:|----------|:--------:|
| 신규 | `Managers/ScoreCalculator.cs` | 96줄 + 확장 |
| 신규 | `UI/ScorePopupManager.cs` | 272줄 |
| 신규 | `UI/ComboDisplay.cs` | 207줄 |
| 수정 | `Managers/ScoreManager.cs` | 전면 재작성 (295줄) |
| 수정 | `Managers/GameManager.cs` | 중규모 |
| 수정 | `Managers/UIManager.cs` | 대규모 |
| 수정 | `Core/BlockRemovalSystem.cs` | 소규모 |
| 수정 | `Core/DrillBlockSystem.cs` | 소규모 |
| 수정 | `Core/BombBlockSystem.cs` | 소규모 |
| 수정 | `Core/LaserBlockSystem.cs` | 소규모 |
| 수정 | `Core/DonutBlockSystem.cs` | 소규모 |
| 수정 | `Core/XBlockSystem.cs` | 소규모 |
| 수정 | `Utils/VisualConstants.cs` | 소규모 |

---

## 2. 블록 기본 점수

### 2.1 블록 티어별 기본 점수

| BlockTier | 점수 | 설명 |
|:---------:|:----:|------|
| Normal (기본) | 50 | 일반 보석 |
| Tier1 | 75 | 1등급 보석 |
| Tier2 | 100 | 2등급 보석 |
| Tier3 | 150 | 3등급 보석 |
| ProcessedGem | 200 | 가공 보석 |

`ScoreCalculator.GetBlockBaseScore(BlockTier tier)` 메서드로 조회합니다.

---

## 3. 일반 매치 점수

### 3.1 매치 크기 보너스

| 매치 블록 수 | 추가 보너스 | 생성되는 특수 블록 |
|:-----------:|:---------:|:-----------------:|
| 3개 (기본) | +0 | 없음 |
| 4개 | +100 | 드릴(Drill) |
| 5개 | +250 | 폭탄(Bomb) |
| 6개 | +400 | 레이저(Laser) |
| 7개 이상 | +600 | 도넛(Rainbow/XBlock) |

### 3.2 일반 매치 점수 공식

```
기본 점수 = 제거 블록 수 × 50 + 매치 크기 보너스
연쇄 적용 = 반올림(기본 점수 × 연쇄 배율)
최종 점수 = 반올림(연쇄 적용 × 콤보 배율)
```

---

## 4. 특수 블록 발동 점수

### 4.1 발동 점수 공식

특수 블록이 파괴하는 **각 블록의 티어별 기본 점수**를 합산합니다.

```
특수블록 점수 = 시스템 기본점수 + Σ(파괴 블록별 GetBlockBaseScore(tier))
```

### 4.2 시스템별 기본점수

| 시스템 | 기본점수 | 비고 |
|--------|:-------:|------|
| Drill | 100 | 양방향 직선 파괴 |
| Bomb | 200 | 중앙 + 인접 6칸 폭발 |
| Laser | 300 | 3축 × 양방향 (6방향) 직선 파괴 |
| Donut (=XBlock) | 500 | 같은 색 보드 전체 제거 |

### 4.3 변경 전후 비교

| 시스템 | 변경 전 (고정 단가) | 변경 후 (티어별 점수) |
|--------|-------------------|---------------------|
| Drill | `100 + 타겟수 × 50` | `100 + Σ GetBlockBaseScore(target.tier)` |
| Bomb | `200 + 타겟수 × 80` | `200 + Σ GetBlockBaseScore(target.tier)` |
| Laser | `300 + 타겟수 × 60` | `300 + Σ GetBlockBaseScore(target.tier)` |
| Donut | `500 + 타겟수 × 100` | `500 + Σ GetBlockBaseScore(target.tier)` |
| XBlock | `500 + 타겟수 × 100` | `500 + Σ GetBlockBaseScore(target.tier)` |

### 4.4 구현 핵심사항

- **tier 읽는 시점:** 반드시 `ClearData()` 호출 **전에** `target.Data.tier`를 읽어야 합니다.
- **pending 특수 블록 제외:** `pendingSpecialBlocks`에 추가되는 블록은 파괴되지 않으므로 점수 미포함. 기존 `if/else` 분기의 `else`(일반 블록 파괴)에서만 점수 누적.
- **using 추가:** 5개 특수 블록 시스템에 `using JewelsHexaPuzzle.Managers;` 필요.
- **동시 코루틴 안전성:** Drill/Laser의 양방향/6방향 코루틴은 단일 스레드이므로 경쟁 조건 없음.

### 4.5 구현 패턴 (5개 시스템 공통)

```csharp
// 1. 코루틴 시작부에 합산 변수 선언
int blockScoreSum = 0;

// 2. 타겟 블록 순회 시 (else 분기 = 일반 블록 파괴)
else
{
    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
    // ... 기존 파괴 처리
}

// 3. 최종 점수 계산
int totalScore = 시스템기본점수 + blockScoreSum;
Debug.Log($"[System] Score={totalScore} (base:{기본점수} + blockTierSum:{blockScoreSum})");
```

---

## 5. 특수 블록 생성 가산점

매치 점수와 별도로 **특수 블록 생성 순간에 즉시 지급**됩니다.

### 5.1 생성 가산점

| 특수 블록 | 필요 매치 | 생성 가산점 | 근거 |
|----------|----------|-----------|------|
| 드릴 (Drill) | 4개 매치 | **+150** | 가장 쉬운 특수 블록, 기본 보상 |
| 폭탄 (Bomb) | 5개 매치 | **+300** | 중급 난이도, 범위 공격 가치 |
| 레이저 (Laser) | 6개 매치 | **+500** | 6방향 관통, 높은 전략 가치 |
| 도넛 (Donut/XBlock) | 7+매치 또는 링매치 | **+800** | 전체 색상 제거, 최고 가치 |

### 5.2 복수 생성 보너스

| 한 턴 특수 블록 생성 수 | 추가 보너스 |
|:---------------------:|:---------:|
| 2개 | **+200** |
| 3개+ | **+500** |

### 5.3 생성 가산점 공식

```
creationBonus = GetSpecialBlockCreationBonus(specialType) × cascadeMultiplier
turnCreationBonus = sum(각 생성 가산점) + multiCreationBonus
```

### 5.4 구현 시점

`BlockRemovalSystem.ProcessMatchesInline()`에서 `CreateSpecialBlock()` 호출 직후 `ScoreManager`를 통해 가산합니다.

---

## 6. 적군 파괴 점수

### 6.1 적군별 기본 파괴 점수

| # | 적군 | 난이도 | 기본 파괴 점수 | 근거 |
|---|------|--------|-------------|------|
| 1 | 색상 도둑 (Chromophage) | ★ | **100** | 가장 쉬운 입문용, Normal 블록 2배 |
| 2 | 속박의 사슬 (Chain Anchor) | ★★ | **150** | 회전 불가 불편함 보상 |
| 3 | 가시 기생충 (Thorn Parasite) | ★★ | **200** | 턴 -1 페널티 리스크 보상 |
| 4 | 분열체 (Divider) | ★★★ | **300** | 확산 위험 대처 보상 |
| 5 | 중력 왜곡자 (Gravity Warper) | ★★★★ | **400** | 낙하 차단 해소 보상 |
| 6 | 반사 장막 (Reflection Shield) | ★★★★ | **450** | 2회 공격 필요 보상 |
| 7 | 시간 정지자 (Time Freezer) | ★★★★ | **500** | 2턴 소모 위험 보상 |
| 8 | 공명 트윈 (Resonance Twin) | ★★★★★ | **600** (개당) | 동시 제거 난이도 보상 |
| 9 | 어둠의 포자 (Shadow Spore) | ★★★★★ | **700** | 시간 압박 + 확산 대처 보상 |
| 10 | 카오스 군주 (Chaos Overlord) | ★★★★★★ | **1500** | 보스급 최고 보상 |

### 6.2 제거 방법별 배율

| 제거 방법 | 배율 | 설명 |
|----------|------|------|
| 일반 매칭 | **x1.0** | 기본 점수 |
| 특수 블록 (드릴/폭탄) | **x1.2** | 전략적 사용 보상 |
| 특수 블록 (레이저/도넛) | **x1.5** | 고급 특수 블록 보상 |
| 아이템 사용 | **x0.5** | 아이템은 편의 수단, 점수 메리트 감소 |

### 6.3 특수 상황 보너스

| 상황 | 추가 점수 | 설명 |
|------|----------|------|
| 가시 기생충 무페널티 제거 | **+150** | 특수 블록/아이템으로 페널티 없이 제거 시 |
| 분열체 무분열 제거 | **+200** | 폭탄/레이저/도넛으로 분열 없이 제거 시 |
| 공명 트윈 동시 제거 | **+500** (쌍 보너스) | 한 턴에 쌍둥이 둘 다 제거 시 추가 |
| 반사 장막 1회 제거 | **+300** | 도넛/일반 매칭으로 한 방에 제거 시 |
| 카오스 군주 1회 제거 | **+1000** | 도넛으로 한 방에 제거 시 |

### 6.4 멀티킬 보너스 (한 턴에 여러 적군 제거)

| 한 턴 적군 제거 수 | 추가 보너스 |
|:-----------------:|:---------:|
| 2마리 | **+200** |
| 3마리 | **+500** |
| 4마리 | **+1000** |
| 5마리+ | **+2000** |

### 6.5 적군 파괴 점수 공식

```
단일 적군: enemyScore = GetEnemyBaseScore(type) × removalMethodMultiplier + specialBonus
턴 전체:   turnEnemyScore = (sum(각 적군 점수) + multiKillBonus) × cascadeMultiplier
```

---

## 7. 연쇄(캐스케이드) 배율

블록 제거 후 낙하로 인해 추가 매치가 발생하면 연쇄 깊이(depth)가 증가합니다.

| 연쇄 단계 | 배율 |
|:---------:|:----:|
| 0단계 (최초) | 1.0배 |
| 1단계 | 1.2배 |
| 2단계 | 1.4배 |
| 3단계 | 1.6배 |
| 4단계 이상 | 1.8배 (상한) |

**산출 공식:** `min(1.0 + 단계 × 0.2, 1.8)`

---

## 8. 콤보 배율

연속 매치 시 콤보 횟수가 증가하며, 점수에 추가 배율이 적용됩니다.

```
콤보 배율 = 1.0 + min(현재 콤보, 최대 콤보) × 0.5
```

| 설정 항목 | 기본값 | Inspector 조정 |
|----------|:------:|:--------------:|
| 최대 콤보 | 4 | 가능 |
| 콤보당 증가분 | 0.5 | 가능 |
| 콤보 리셋 시간 | 3초 | 가능 |
| 최대 배율 | 3.0배 | (= 1.0 + 4 × 0.5) |

> 마지막 매치 이후 3초 이내에 추가 매치가 없으면 콤보가 0으로 초기화됩니다.

---

## 9. 스테이지 클리어 보너스

| 항목 | 계산 방식 |
|------|----------|
| 남은 턴 보너스 | 남은 턴 수 × 200 |
| 효율 보너스 | 턴 사용률 50% 미만: +2,000 / 70% 미만: +1,000 / 그 외: 없음 |

### 별 등급 산정 기준

목표 점수는 `턴 제한 × 500`으로 자동 계산됩니다.

| 달성 비율 | 등급 |
|:---------:|:----:|
| 200% 이상 | 3성 |
| 150% 이상 | 2성 |
| 100% 이상 | 1성 |
| 100% 미만 | 0성 |

---

## 10. 최종 점수 산출 공식 요약

### (가) 일반 매치

```
기본 점수 = 제거 블록 수 × 50 + 매치 크기 보너스
최종 = 반올림(기본 점수 × 연쇄 배율 × 콤보 배율)
```

### (나) 특수 블록 발동

```
발동 점수 = 시스템 기본점수 + Σ(파괴 블록별 GetBlockBaseScore(tier))
최종 = 반올림(발동 점수 × 연쇄 배율 × 콤보 배율)
```

### (다) 특수 블록 생성

```
생성 보너스 = GetSpecialBlockCreationBonus(type) × 연쇄 배율
+ 복수 생성 보너스 (2개: +200, 3개+: +500)
```

### (라) 적군 파괴

```
적군 점수 = GetEnemyBaseScore(type) × 제거 방법 배율 + 특수 상황 보너스
턴 전체 = (sum(각 적군 점수) + 멀티킬 보너스) × 연쇄 배율
```

### (마) 스테이지 클리어

```
클리어 보너스 = 남은 턴 × 200 + 효율 보너스
```

---

## 11. UI 시스템

### 11.1 점수 팝업 (ScorePopupManager)

**경로:** `UI/ScorePopupManager.cs`
**생성 방식:** Canvas 하위에 런타임 자동 생성 (씬 수동 배치 불필요)

#### 점수 구간별 팝업 스타일

| 점수 구간 | 글자 크기 | 색상 | 지속 시간 | 이동 거리 |
|:---------:|:---------:|:----:|:---------:|:---------:|
| 0 ~ 299 | 28px | 흰색 | 0.6초 | 80px |
| 300 ~ 999 | 34px | 금색 (#FFD700) | 0.8초 | 100px |
| 1,000 ~ 2,999 | 42px 굵게 | 주황색 (#FF8C00) | 1.0초 | 120px |
| 3,000 이상 | 50px 굵게 | 붉은 금색 (#FF4500) | 1.2초 | 140px |

#### 팝업 애니메이션

1. **등장** (0 ~ 0.15초): 크기 0 → 1.2배 → 1.0배 (EaseOutBack)
2. **상승** (전체 구간): Y축 위로 이동 (EaseOutQuart), X축 ±15px 미세 흔들림
3. **투명도**: 전체 시간의 60%까지 완전 불투명, 나머지 40%에서 서서히 사라짐 (EaseInQuad)

### 11.2 콤보 표시 (ComboDisplay)

**경로:** `UI/ComboDisplay.cs`
**위치:** 화면 상단 중앙, HUD 바 아래 (-120px)

| 콤보 | 표시 텍스트 | 글자 크기 | 색상 | 특수 효과 |
|:----:|:----------:|:---------:|:----:|:---------:|
| 0 ~ 1 | (숨김) | - | - | - |
| x2 | "COMBO x2" | 32 보통 | 노란색 | 페이드인 + 확대 |
| x3 | "COMBO x3" | 38 굵게 | 주황색 | 바운스 등장 |
| x4 이상 | "COMBO x4!" | 44 굵게 | 붉은 주황색 | 흔들림 효과 |

### 11.3 점수 카운팅 애니메이션

| 항목 | 내용 |
|------|------|
| 동작 | 현재 표시 중인 숫자에서 목표 숫자까지 빠르게 올라가는 연출 |
| 지속 시간 | 0.5초 (EaseOutQuart) |
| 색상 변화 | 카운팅 중 금색(#FFD700) → 완료 후 0.3초에 걸쳐 원래 색상 복원 |

### 11.4 이동 횟수 UI

#### 턴 텍스트 바운스

| 항목 | 값 |
|------|:--:|
| 최대 확대율 | 1.15배 |
| 지속 시간 | 0.2초 |
| 이징 | EaseOutBack |

#### 원형 프로그레스 링

| 구간 | 색상 |
|------|------|
| 100% ~ 30% | 흰색 |
| 30% ~ 17% | 주황색 (#FF9900) |
| 17% ~ 0% | 빨간색 |

#### 위험 알림 펄스

| 남은 턴 | 텍스트 색상 | 깜빡임 주기 |
|:-------:|:-----------:|:----------:|
| 6턴 이상 | 흰색 | 없음 |
| 3 ~ 5턴 | 빨간색 | 0.5초/회 |
| 1 ~ 3턴 | 진한 빨간색 (#FF2626) | 0.3초/회 |

### 11.5 스테이지 클리어 브레이크다운

| 시간 | 대상 | 내용 |
|:----:|------|------|
| 0.0초 | 팝업 전체 | 기존 등장 애니메이션 (0.3초) |
| 0.5초 | 기본 점수 | 0 → baseScore 카운팅 (0.4초) |
| 0.85초 | 남은 턴 보너스 | 0 → remainingTurnsBonus 카운팅 (0.4초) |
| 1.2초 | 효율 보너스 | 0 → efficiencyBonus 카운팅 (0.3초) |
| 1.5초 | 총점 | 0 → totalScore 카운팅 (0.6초) + 크기 강조 |
| 1.9초 | 별 등급 | 획득한 별 순차 등장 (0.15초 간격, EaseOutBack) |

---

## 12. 이벤트 흐름도

### 12.1 일반 매치 점수 흐름

```
사용자가 블록 회전
  └→ RotationSystem.OnRotationComplete
       ├→ GameManager.UseTurn() → UIManager.UpdateTurnDisplay()
       └→ BlockRemovalSystem.ProcessMatches(매치 목록)
            └→ [BRS 내부] 블록 제거 + 평균 위치 계산
                 └→ OnBlocksRemoved(블록수, 연쇄깊이, 평균위치)
                      └→ GameManager.OnBlocksRemoved()
                           └→ ScoreManager.AddMatchScore()
                                ├→ OnScoreChanged  → UIManager (카운팅)
                                ├→ OnScorePopup    → ScorePopupManager (팝업)
                                └→ OnComboChanged  → ComboDisplay (콤보)
```

### 12.2 특수 블록 발동 점수 흐름

```
특수 블록 발동 (Drill / Bomb / Laser / Donut)
  └→ 각 시스템의 OnXxxComplete(점수)
       └→ GameManager.OnSpecialBlockCompleted(점수)
            └→ ScoreManager.AddSpecialBlockScore(점수, 연쇄깊이, 위치)
```

### 12.3 특수 블록 생성 가산점 흐름

```
ProcessMatchesInline
  → 매치 그룹 순회
    → createdSpecialType != None?
      → CreateSpecialBlock() 호출
      → ScoreManager.AddCreationBonus(specialType, cascadeDepth, position)
  → 턴 내 생성 수 집계 → multiCreationBonus 가산
```

### 12.4 스테이지 클리어 흐름

```
미션 완료 감지
  └→ GameManager.StageClear()
       └→ ScoreManager.CalculateStageClearBonus(남은턴, 전체턴)
            └→ StageSummaryData 반환
       └→ UIManager.ShowStageClearPopup(요약 데이터)
```

### 12.5 BlockRemovalSystem 이벤트 시그니처

| 항목 | 내용 |
|------|------|
| **변경 전** | `public event Action<int> OnBlocksRemoved;` |
| **변경 후** | `public event Action<int, int, Vector3> OnBlocksRemoved;` |
| **매개변수** | `(int 제거블록수, int 연쇄깊이, Vector3 평균위치)` |

---

## 13. ScoreCalculator 확장 명세

**경로:** `Managers/ScoreCalculator.cs`

### 13.1 기존 상수 및 메서드

```csharp
// 블록 티어별 기본 점수
private const int ScoreNormal = 50;
private const int ScoreTier1 = 75;
private const int ScoreTier2 = 100;
private const int ScoreTier3 = 150;
private const int ScoreProcessedGem = 200;

// 매치 크기 보너스
private const int Bonus3Match = 0;
private const int Bonus4Match = 100;
private const int Bonus5Match = 250;
private const int Bonus6Match = 400;
private const int Bonus7PlusMatch = 600;

// 스테이지 클리어 보너스
private const int PointsPerRemainingTurn = 200;
private const int EfficiencyBonusPerfect = 2000;
private const int EfficiencyBonusGreat = 1000;

// 기존 메서드
public static int GetBlockBaseScore(BlockTier tier);
public static int GetMatchSizeBonus(int blockCount);
public static int CalculateMatchGroupScore(int blockCount);
public static float GetCascadeScoreMultiplier(int depth);
public static int CalculateRemainingTurnsBonus(int remainingTurns);
public static int CalculateEfficiencyBonus(int turnsUsed, int turnLimit);
```

### 13.2 추가: 특수 블록 생성 가산점

```csharp
// 특수 블록 생성 가산점
private const int CreationBonusDrill = 150;
private const int CreationBonusBomb = 300;
private const int CreationBonusLaser = 500;
private const int CreationBonusDonut = 800;  // 도넛 = X블록 동일

// 복수 생성 보너스
private const int MultiCreation2 = 200;
private const int MultiCreation3Plus = 500;

public static int GetSpecialBlockCreationBonus(SpecialBlockType type);
public static int GetMultiCreationBonus(int specialBlockCount);
```

### 13.3 추가: 적군 파괴 점수

```csharp
// 적군 기본 파괴 점수
private const int EnemyScoreChromophage = 100;
private const int EnemyScoreChainAnchor = 150;
private const int EnemyScoreThornParasite = 200;
private const int EnemyScoreDivider = 300;
private const int EnemyScoreGravityWarper = 400;
private const int EnemyScoreReflectionShield = 450;
private const int EnemyScoreTimeFreezer = 500;
private const int EnemyScoreResonanceTwin = 600;
private const int EnemyScoreShadowSpore = 700;
private const int EnemyScoreChaosOverlord = 1500;

// 제거 방법 배율
private const float RemovalMultiplierMatch = 1.0f;
private const float RemovalMultiplierSpecialBasic = 1.2f;   // 드릴/폭탄
private const float RemovalMultiplierSpecialAdvanced = 1.5f; // 레이저/도넛
private const float RemovalMultiplierItem = 0.5f;

// 특수 상황 보너스
private const int BonusThornNoPenalty = 150;
private const int BonusDividerNoSplit = 200;
private const int BonusTwinSimultaneous = 500;
private const int BonusShieldOneShot = 300;
private const int BonusChaosOneShot = 1000;

// 멀티킬 보너스
private const int MultiKill2 = 200;
private const int MultiKill3 = 500;
private const int MultiKill4 = 1000;
private const int MultiKill5Plus = 2000;

public static int GetEnemyBaseScore(EnemyType type);
public static float GetRemovalMethodMultiplier(RemovalMethod method);
public static int GetEnemySpecialBonus(EnemyType type, RemovalCondition condition);
public static int GetMultiKillBonus(int enemyCount);
public static int CalculateEnemyScore(EnemyType type, RemovalMethod method, RemovalCondition condition);
public static int CalculateTurnEnemyScore(List<EnemyKillData> kills, int cascadeDepth);
```

### 13.4 추가: 새 enum / 구조체

```csharp
public enum RemovalMethod
{
    Match,           // 일반 매칭
    SpecialBasic,    // 드릴/폭탄
    SpecialAdvanced, // 레이저/도넛
    Item             // 아이템 사용
}

public enum RemovalCondition
{
    Normal,              // 일반 제거
    ThornNoPenalty,      // 가시 기생충 무페널티 제거
    DividerNoSplit,      // 분열체 무분열 제거
    TwinSimultaneous,    // 공명 트윈 동시 제거
    ShieldOneShot,       // 반사 장막 1회 제거
    ChaosOneShot         // 카오스 군주 1회 제거
}

public struct EnemyKillData
{
    public EnemyType enemyType;
    public RemovalMethod method;
    public RemovalCondition condition;
}
```

---

## 14. ScoreManager 상세

**경로:** `Managers/ScoreManager.cs`

### 14.1 Inspector 조정 가능 항목

| 필드명 | 타입 | 기본값 | 용도 |
|--------|:----:|:------:|------|
| `comboMultiplierBase` | int | 1 | 콤보 기본 배율 |
| `comboMultiplierIncrement` | float | 0.5 | 콤보 1회당 배율 증가분 |
| `comboResetTime` | float | 3.0 | 콤보 초기화까지의 유휴 시간(초) |
| `maxCombo` | int | 4 | 콤보 상한 |

### 14.2 이벤트 목록

| 이벤트명 | 시그니처 | 발생 시점 |
|---------|---------|----------|
| `OnScoreChanged` | `Action<int>` | 점수가 변경될 때 (현재 총점 전달) |
| `OnComboChanged` | `Action<int>` | 콤보 횟수가 변경될 때 |
| `OnScorePopup` | `Action<int, Vector3>` | 점수 획득 시 (획득 점수, 월드 좌표) |

### 14.3 주요 메서드

| 메서드 | 역할 |
|--------|------|
| `AddMatchScore(int blockCount, int cascadeDepth, Vector3 position)` | 일반 매치 점수 계산 및 가산 |
| `AddSpecialBlockScore(int rawScore, int cascadeDepth, Vector3 position)` | 특수 블록 점수 계산 및 가산 |
| `AddCreationBonus(SpecialBlockType type, int cascadeDepth, Vector3 position)` | 특수 블록 생성 가산점 |
| `AddScore(int baseScore, Vector3? popupPosition)` | 하위 호환용 범용 점수 가산 |
| `CalculateStageClearBonus(int remainingTurns, int turnLimit)` | 클리어 보너스 계산 |
| `ResetScore()` | 점수/콤보/스테이지 추적값 전체 초기화 |

### 14.4 스테이지 요약 데이터

```csharp
public struct StageSummaryData
{
    public int baseScore;
    public int remainingTurns;
    public int remainingTurnsBonus;
    public int efficiencyBonus;
    public int totalScore;
    public int highScore;
    public bool isNewHighScore;
    public int starRating;       // 0~3
    public int maxComboReached;
}
```

---

## 15. 점수 시뮬레이션 예시

### 예시 1: 기본 3매치 (첫 번째 매치)

```
기본 점수 = 3 × 50 + 0 = 150
연쇄 배율 = 1.0배 (첫 매치)
콤보 배율 = 1.5배 (콤보 1회)
최종 점수 = 150 × 1.0 × 1.5 = 225점
→ 팝업: 소형 (흰색, 28px)
```

### 예시 2: 5매치 폭탄 생성 + 연쇄 2단계 + 콤보 2회

```
매치 점수 = 5 × 50 + 250 = 500
폭탄 생성 가산점 = +300
연쇄 배율 = 1.4배, 콤보 배율 = 2.0배
최종 매치 점수 = 500 × 1.4 × 2.0 = 1,400점
생성 가산 = 300 × 1.4 = 420점
→ 턴 합계: 1,820점
→ 팝업: 대형 (주황색, 42px, 굵게)
```

### 예시 3: Bomb 발동 (Normal 3개 + Tier3 3개 파괴)

```
발동 점수 = 200 + (50 × 3 + 150 × 3) = 200 + 600 = 800점
→ 연쇄/콤보 배율 추가 적용
```

### 예시 4: 폭탄으로 분열체 2마리 + 색상 도둑 1마리 동시 제거 (cascade depth 1)

```
분열체: 300 × 1.2 = 360 × 2마리 = 720
무분열 보너스: +200 × 2 = 400
색상 도둑: 100 × 1.2 = 120
멀티킬(3마리): +500
소계: 1,740
cascade 배율 (depth 1): ×1.2
→ 총 적군 파괴 점수: 2,088점
```

### 예시 5: 도넛으로 카오스 군주 1회 제거

```
기본 파괴 점수: 1,500 × 1.5 = 2,250
1회 제거 보너스: +1,000
→ 총 적군 파괴 점수: 3,250점
```

### 예시 6: 스테이지 클리어 (30턴 중 12턴 사용)

```
남은 턴 보너스 = 18 × 200 = 3,600
턴 사용률 = 12 / 30 = 40% (50% 미만) → 효율 보너스 = 2,000
추가 보너스 합계 = 5,600
```

---

## 16. 테스트 체크리스트

### (가) 일반 매치 / 특수 블록

- [ ] 3매치 시 블록 제거 위치에 점수 팝업 표시
- [ ] 5매치 시 팝업 크기와 색상이 상위 단계로 변경
- [ ] Tier3 블록을 Bomb으로 파괴 시 150점 반영 확인
- [ ] ProcessedGem 블록을 Donut으로 파괴 시 200점 반영 확인
- [ ] 특수 블록이 pending에 추가될 때 해당 블록 점수 미포함 확인
- [ ] Laser 6방향 동시 파괴 시 모든 방향 블록 점수 합산 확인

### (나) 특수 블록 생성 가산점

- [ ] 4매치 드릴 생성 시 +150 가산점 확인
- [ ] 7+매치 도넛 생성 시 +800 가산점 확인
- [ ] 한 턴에 2개 생성 시 +200 복수 생성 보너스 확인
- [ ] 캐스케이드 중 생성 시 cascade 배율 적용 확인

### (다) 연쇄 / 콤보

- [ ] 연쇄 2회 이상 발생 시 연쇄 배율 증가 확인
- [ ] 연속 매치 시 콤보 숫자 증가 + 콤보 텍스트 표시
- [ ] 3초간 매치 없으면 콤보 리셋
- [ ] 콤보 x4 이상에서 흔들림 효과

### (라) UI

- [ ] 점수 텍스트 카운팅 애니메이션 + 금색 하이라이트
- [ ] 턴 사용 시 턴 숫자 바운스 (1.15배)
- [ ] 남은 5턴 이하에서 빨간색 깜빡임
- [ ] 스테이지 클리어 시 점수 항목 순차 등장 + 별 애니메이션

### (마) 통합 / 예외

- [ ] 특수 블록 연쇄 발동 시 점수 정상 누적
- [ ] BigBang 발생 시 다수 팝업 동시 표시 오류 없음
- [ ] 스테이지 재시작 시 점수/콤보 전체 초기화
- [ ] UI 미연결 시 오류 없이 동작 (null 체크)

---

## 17. VisualConstants 추가 상수

| 구분 | 상수명 | 값 |
|------|--------|:--:|
| 점수 팝업 | `PopupSmallSize` / `MediumSize` / `LargeSize` / `EpicSize` | 28 / 34 / 42 / 50 |
| 점수 팝업 | `PopupBaseDuration` / `PopupTravelDistance` | 0.8초 / 100px |
| 콤보 표시 | `ComboScaleIn` / `ComboAnimDuration` / `ComboIdleTimeout` | 1.3 / 0.3초 / 1.5초 |
| 점수 카운팅 | `ScoreCountDuration` | 0.5초 |
| 이동 횟수 | `MoveBounceScale` / `MoveBounceDuration` / `MovePulseSpeed` | 1.15 / 0.2초 / 0.5초 |
| cascade 배율 상한 | `GetCascadeMultiplier` | 1.8 |

---

**이 문서는 게임 내 모든 점수 관련 시스템의 통합 기획서입니다.**
**관련 코드: ScoreCalculator.cs, ScoreManager.cs, BlockRemovalSystem.cs, 5개 특수 블록 시스템**

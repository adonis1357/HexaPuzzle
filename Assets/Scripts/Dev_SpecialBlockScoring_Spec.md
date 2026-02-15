# 특수 블록 파괴 시 블록당 기본 점수 추가 - 변경 명세서

> **작성일:** 2026-02-14
> **작성자:** 개발 PM
> **수신:** 개발팀장
> **문서 버전:** v1.0
> **우선순위:** 높음

---

## 1. 배경

현재 특수 블록(Drill, Bomb, Laser, Donut, XBlock)이 발동하여 블록을 파괴할 때,
파괴되는 개별 블록의 **티어별 기본 점수**가 반영되지 않습니다.

일반 매칭 경로에서는 `ScoreCalculator.GetBlockBaseScore(tier)`를 통해
Normal(50), Tier1(75), Tier2(100), Tier3(150), ProcessedGem(200)이 적용되지만,
특수 블록 경로에서는 **시스템별 고정 단가**(50~100)만 곱해지고 있습니다.

### 현재 점수 산출 비교

| 경로 | Tier3 블록 5개 파괴 시 |
|------|----------------------|
| 일반 매칭 | 5 × 150 + 매치보너스 = **1,000점** (배율 전) |
| Drill 파괴 | 100 + 5 × 50 = **350점** (티어 무관) |
| Bomb 파괴 | 200 + 5 × 80 = **600점** (티어 무관) |

**문제:** 고급 티어 블록을 특수 블록으로 파괴해도 점수적 이점이 없습니다.

---

## 2. 변경 목표

특수 블록이 파괴하는 **각 블록의 티어별 기본 점수**를 합산하여 최종 점수에 포함합니다.

### 변경 후 점수 공식

```
특수블록 점수 = 시스템 기본점수 + Σ(파괴 블록별 GetBlockBaseScore(tier)) + (타겟수 × 시스템 단가)
```

또는 더 간결하게, 기존 고정 단가를 **티어별 기본 점수로 대체**:

```
특수블록 점수 = 시스템 기본점수 + Σ(파괴 블록별 GetBlockBaseScore(tier))
```

### 권장 방안: 고정 단가를 티어별 기본 점수로 대체 (방안 B)

기존 고정 단가(`targets × 50` 등)를 제거하고, 대신 파괴되는 각 블록의 `tier`에 따른
`ScoreCalculator.GetBlockBaseScore(tier)` 값을 합산합니다.

| 시스템 | 현재 | 변경 후 |
|--------|------|---------|
| Drill | `100 + 타겟수 × 50` | `100 + Σ GetBlockBaseScore(target.tier)` |
| Bomb | `200 + 타겟수 × 80` | `200 + Σ GetBlockBaseScore(target.tier)` |
| Laser | `300 + 타겟수 × 60` | `300 + Σ GetBlockBaseScore(target.tier)` |
| Donut | `500 + 타겟수 × 100` | `500 + Σ GetBlockBaseScore(target.tier)` |
| XBlock | `500 + 타겟수 × 100` | `500 + Σ GetBlockBaseScore(target.tier)` |

> **참고:** 시스템 기본점수(100/200/300/500)는 유지합니다. 특수 블록 자체의 발동 보너스 역할입니다.

---

## 3. 변경 대상 파일

| 파일 | 변경 내용 | 규모 |
|------|----------|------|
| `Core/DrillBlockSystem.cs` | DrillCoroutine 내 점수 계산 수정 | 소규모 |
| `Core/BombBlockSystem.cs` | BombCoroutine 내 점수 계산 수정 | 소규모 |
| `Core/LaserBlockSystem.cs` | LaserCoroutine 내 점수 계산 수정 | 소규모 |
| `Core/DonutBlockSystem.cs` | DonutCoroutine 내 점수 계산 수정 | 소규모 |
| `Core/XBlockSystem.cs` | XBlockCoroutine 내 점수 계산 수정 | 소규모 |

**변경하지 않는 파일:** ScoreCalculator.cs, ScoreManager.cs (기존 API 그대로 활용)

---

## 4. 시스템별 변경 상세

### 4-1. DrillBlockSystem.cs

**위치:** `DrillCoroutine` 메서드 (약 173행)

**현재 코드:**
```csharp
int totalScore = 100 + (targets1.Count + targets2.Count) * 50;
```

**변경 방법:**
- `DrillLineWithProjectile` 내에서 각 타겟 블록을 파괴하기 전에 `target.Data.tier`를 읽어 점수를 누적합니다.
- 클래스 필드 또는 별도 리스트로 파괴된 블록의 tier 점수 합계를 추적합니다.

**변경 후 의사코드:**
```csharp
// DrillCoroutine 시작 시
int blockScoreSum = 0;

// DrillLineWithProjectile 내부에서 블록 파괴 직전
if (target.Data != null && target.Data.gemType != GemType.None)
{
    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
}

// DrillCoroutine 완료 시
int totalScore = 100 + blockScoreSum;
```

**주의사항:**
- `blockScoreSum`은 양방향(targets1, targets2) 코루틴에서 동시에 접근하므로,
  DrillCoroutine 레벨에서 변수를 선언하고 각 라인 코루틴에 참조를 전달하거나,
  각 라인의 결과를 합산하는 방식을 사용합니다.
- `pendingSpecialBlocks`에 추가되는 특수 블록은 점수에서 제외합니다 (파괴되지 않으므로).

### 4-2. BombBlockSystem.cs

**위치:** `BombCoroutine` 메서드 (약 289행)

**현재 코드:**
```csharp
int totalScore = 200 + targets.Count * 80;
```

**변경 후 의사코드:**
```csharp
int blockScoreSum = 0;

// 타겟 순회 시 (파괴 직전)
foreach (target in targets)
{
    if (target.Data.specialType == None || target.Data.specialType == FixedBlock)
        blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
    // specialType이 있으면 pending에 추가되므로 점수 미포함
}

int totalScore = 200 + blockScoreSum;
```

### 4-3. LaserBlockSystem.cs

**위치:** `LaserCoroutine` 메서드 (약 274행)

**현재 코드:**
```csharp
int totalScore = 300 + totalTargets * 60;
```

**변경 후 의사코드:**
```csharp
int blockScoreSum = 0;

// DestroyLine 내에서 일반 블록 파괴 시
blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);

int totalScore = 300 + blockScoreSum;
```

**주의사항:**
- 6방향 `DestroyLine` 코루틴이 동시 실행되므로, 점수 합산 시점에 주의가 필요합니다.
- 각 `DestroyLine`에서 파괴한 블록 수를 별도로 집계하여 `LaserCoroutine`에서 합산하는 방식을 권장합니다.

### 4-4. DonutBlockSystem.cs

**위치:** `DonutCoroutine` 메서드 (약 299행)

**현재 코드:**
```csharp
int totalScore = 500 + targets.Count * 100;
```

**변경 후 의사코드:**
```csharp
int blockScoreSum = 0;

// 타겟 순회 시
foreach (target in targets)
{
    if (target.Data.specialType == None)
        blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
}

int totalScore = 500 + blockScoreSum;
```

### 4-5. XBlockSystem.cs

**위치:** `XBlockCoroutine` 메서드 (약 315행)

**현재 코드:**
```csharp
int totalScore = 500 + targets.Count * 100;
```

**변경 후:** DonutBlockSystem과 동일한 패턴

---

## 5. 핵심 고려사항

### 5-1. tier 값 읽는 시점

블록의 `Data.tier` 값은 **파괴(ClearData) 전에** 읽어야 합니다.
현재 각 시스템에서 `ClearData()` 호출 전에 타겟 목록을 순회하는 구간이 있으므로,
해당 구간에서 tier를 읽으면 됩니다.

### 5-2. pending 특수 블록 제외

`pendingSpecialBlocks`에 추가되는 블록은 실제로 파괴되지 않으므로 점수에서 제외합니다.
현재 코드에서 이미 `if (target.Data.specialType != None)` 분기로 분리되어 있으므로,
`else` 분기(일반 블록 파괴)에서만 점수를 합산하면 됩니다.

### 5-3. using 추가

5개 특수 블록 시스템 파일에 `using JewelsHexaPuzzle.Managers;`가 없다면 추가해야 합니다.
(`ScoreCalculator`가 `JewelsHexaPuzzle.Managers` 네임스페이스에 위치)

### 5-4. 동시 코루틴 안전성

Drill과 Laser는 양방향/6방향 코루틴이 동시에 실행됩니다.
`int blockScoreSum`을 코루틴 간에 공유할 때 **경쟁 상태**(race condition)는
Unity 코루틴이 단일 스레드에서 실행되므로 발생하지 않습니다.
다만, 코루틴 완료 후 합산하는 방식이 더 명확합니다.

---

## 6. 점수 변동 예시

### Bomb 발동, Normal 3개 + Tier3 3개 파괴 시

**현재:**
```
200 + 6 × 80 = 680점
```

**변경 후:**
```
200 + (50 × 3 + 150 × 3) = 200 + 600 = 800점
```

### Laser 발동, Normal 12개 + ProcessedGem 2개 파괴 시

**현재:**
```
300 + 14 × 60 = 1,140점
```

**변경 후:**
```
300 + (50 × 12 + 200 × 2) = 300 + 1,000 = 1,300점
```

---

## 7. 테스트 항목

| # | 테스트 | 확인 사항 |
|---|--------|----------|
| 1 | Normal 블록만 있는 상태에서 Drill 발동 | 기본점수 50씩 합산되는가? |
| 2 | Tier3 블록을 Bomb으로 파괴 | 150점이 반영되는가? |
| 3 | ProcessedGem 블록을 Donut으로 파괴 | 200점이 반영되는가? |
| 4 | 특수 블록이 pending에 추가될 때 | 해당 블록은 점수에서 제외되는가? |
| 5 | Laser 6방향 동시 파괴 | 모든 방향의 블록 점수가 빠짐없이 합산되는가? |
| 6 | 연쇄 중 cascade 배율 적용 | `ScoreManager.AddSpecialBlockScore`에 cascadeDepth가 올바르게 전달되는가? |
| 7 | 콘솔 로그 | Debug.Log에 변경된 점수 산출이 정확히 출력되는가? |

---

## 8. 작업 예상 범위

- 5개 파일 × 약 5~10줄 변경 = 총 25~50줄 수정
- 기존 구조를 유지하면서 점수 계산 1줄만 변경하는 수준
- ScoreCalculator, ScoreManager는 변경 없음

# HexaPuzzle 특수 블록 생성 가산점 기획서

**작성일:** 2026-02-16
**작성자:** 기획 디렉터
**문서 버전:** v1.0
**관련 문서:** ScoreCalculator.cs, Dev_ScoreSystem_Spec.md

---

## 1. 기본 원칙

- 생성 난이도가 높을수록 가산점이 큼
- 매치 점수와 별도로 **생성 순간에 즉시 지급**
- 캐스케이드 중 자연 생성은 cascade 배율 적용
- 한 턴에 여러 특수 블록 생성 시 추가 보너스

> **참고:** 도넛과 X블록은 동일한 특수 블록입니다. 생성 조건만 다릅니다 (7+매치 → DonutBlockSystem, 링매치 → XBlockSystem).

---

## 2. 특수 블록별 생성 가산점

| 특수 블록 | 필요 매치 | 생성 가산점 | 근거 |
|----------|----------|-----------|------|
| 드릴 (Drill) | 4개 매치 | **+150** | 가장 쉬운 특수 블록, 기본 보상 |
| 폭탄 (Bomb) | 5개 매치 | **+300** | 중급 난이도, 범위 공격 가치 |
| 레이저 (Laser) | 6개 매치 | **+500** | 6방향 관통, 높은 전략 가치 |
| 도넛 (Donut/XBlock) | 7개+ 매치 또는 링매치 | **+800** | 전체 색상 제거, 최고 가치 |

---

## 3. 추가 보너스

| 상황 | 추가 점수 | 설명 |
|------|----------|------|
| 캐스케이드 중 특수 블록 생성 | cascade 배율 적용 | 연쇄 중 자연 생성은 더 가치 있음 |
| 한 턴에 특수 블록 2개 생성 | **+200** | 더블 생성 보너스 |
| 한 턴에 특수 블록 3개+ 생성 | **+500** | 트리플 이상 생성 보너스 |

---

## 4. 점수 계산 공식

### 4.1 단일 특수 블록 생성 점수

```
creationBonus = GetSpecialBlockCreationBonus(specialType) * cascadeMultiplier
```

### 4.2 턴 전체 생성 보너스

```
turnCreationBonus = sum(각 특수 블록 생성 가산점) + multiCreationBonus
```

---

## 5. 점수 계산 예시

### 예시 1: 5개 매치로 폭탄 생성
- 매치 점수: 5 x 50(Normal) + 250(크기 보너스) = 500
- 폭탄 생성 가산점: +300
- **턴 총점: 800점**

### 예시 2: 캐스케이드(depth 2) 중 레이저 생성
- 매치 점수: 6 x 50 + 400 = 700
- 레이저 생성 가산점: 500 x 1.4(cascade) = 700
- **턴 총점: 1,400점**

### 예시 3: 한 턴에 드릴 + 폭탄 동시 생성
- 드릴 매치: 4 x 50 + 100 = 300, 생성 가산점: +150
- 폭탄 매치: 5 x 50 + 250 = 500, 생성 가산점: +300
- 더블 생성 보너스: +200
- **턴 총점: 1,450점**

### 예시 4: 캐스케이드(depth 1) 중 도넛 생성
- 매치 점수: 7 x 50 + 600 = 950
- 도넛 생성 가산점: 800 x 1.2(cascade) = 960
- **턴 총점: 1,910점**

---

## 6. ScoreCalculator 확장 명세

### 6.1 새 상수

```csharp
// 특수 블록 생성 가산점
private const int CreationBonusDrill = 150;
private const int CreationBonusBomb = 300;
private const int CreationBonusLaser = 500;
private const int CreationBonusDonut = 800;  // 도넛 = X블록 동일

// 복수 생성 보너스
private const int MultiCreation2 = 200;
private const int MultiCreation3Plus = 500;
```

### 6.2 새 메서드

```csharp
/// 특수 블록 타입별 생성 가산점 반환
public static int GetSpecialBlockCreationBonus(SpecialBlockType type)
{
    switch (type)
    {
        case SpecialBlockType.Drill: return CreationBonusDrill;
        case SpecialBlockType.Bomb: return CreationBonusBomb;
        case SpecialBlockType.Laser: return CreationBonusLaser;
        case SpecialBlockType.Rainbow: return CreationBonusDonut;
        case SpecialBlockType.XBlock: return CreationBonusDonut;
        default: return 0;
    }
}

/// 복수 생성 보너스 반환
public static int GetMultiCreationBonus(int specialBlockCount)
{
    if (specialBlockCount >= 3) return MultiCreation3Plus;
    if (specialBlockCount >= 2) return MultiCreation2;
    return 0;
}
```

---

## 7. 밸런스 기준점

### 7.1 생성 가산점 vs 발동 점수 비교

| 특수 블록 | 생성 가산점 | 발동 기본 점수 | 비율 |
|----------|-----------|-------------|------|
| 드릴 | 150 | 100 + 블록당 50 | 생성 ≈ 발동의 30~50% |
| 폭탄 | 300 | 200 + 블록당 80 | 생성 ≈ 발동의 30~50% |
| 레이저 | 500 | 300 + 블록당 60 | 생성 ≈ 발동의 30~50% |
| 도넛 | 800 | 500 + 블록당 100 | 생성 ≈ 발동의 30~50% |

### 7.2 생성 가산점이 전체 점수에서 차지하는 비율 목표

- 일반 플레이: 전체 점수의 **10~15%**
- 고수 플레이 (다수 특수 블록 생성): **15~20%**

---

## 8. 구현 시점

생성 가산점은 `BlockRemovalSystem.ProcessMatchesInline()`에서 특수 블록을 생성하는 시점(`CreateSpecialBlock` 호출 직후)에 `ScoreManager`를 통해 가산합니다.

```
ProcessMatchesInline
  → 매치 그룹 순회
    → createdSpecialType != None?
      → CreateSpecialBlock() 호출
      → ScoreManager.AddCreationBonus(specialType, cascadeDepth, spawnPosition)
  → 턴 내 생성 수 집계 → multiCreationBonus 가산
```

---

## 9. 요약

| 특수 블록 | 생성 가산점 |
|----------|-----------|
| 드릴 | +150 |
| 폭탄 | +300 |
| 레이저 | +500 |
| 도넛 (=X블록) | +800 |
| 더블 생성 | +200 추가 |
| 트리플+ 생성 | +500 추가 |

특수 블록 생성 가산점을 통해 **대형 매치를 노리는 전략적 플레이**에 추가 보상을 제공하여, 단순 3매치 반복보다 특수 블록 생성을 유도합니다.

---

**이 기획서는 ScoreCalculator.cs 및 BlockRemovalSystem.cs와 연계하여 구현해야 합니다.**

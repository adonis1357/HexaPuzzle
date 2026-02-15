# [작업 요청] 특수 블록 파괴 시 블록당 기본 점수 반영

> **발신:** 개발 PM
> **수신:** 개발팀장
> **일자:** 2026-02-14
> **긴급도:** 높음
> **관련 명세:** `Dev_SpecialBlockScoring_Spec.md`

---

## 요청 사항

특수 블록(Drill, Bomb, Laser, Donut, XBlock) 발동 시 파괴되는 블록에
**티어별 기본 점수**가 반영되지 않는 문제가 확인되었습니다.
아래 내용대로 구현을 부탁드립니다.

---

## 현재 문제

각 특수 블록 시스템이 자체적으로 **고정 단가**를 곱해서 점수를 산출하고 있어,
파괴되는 블록이 Normal(50점)이든 Tier3(150점)이든 ProcessedGem(200점)이든 동일한 점수가 부여됩니다.

```
예) Bomb으로 Tier3 블록 6개 파괴
현재: 200 + 6 × 80 = 680점 (tier 무시)
기대: 200 + 6 × 150 = 1,100점 (tier 반영)
```

---

## 변경 내용

5개 특수 블록 시스템의 점수 계산에서 **고정 단가를 `ScoreCalculator.GetBlockBaseScore(target.Data.tier)`로 대체**해 주세요.

### 변경 대상 및 위치

| # | 파일 | 메서드 | 현재 코드 위치 (대략) |
|---|------|--------|---------------------|
| 1 | `Core/DrillBlockSystem.cs` | `DrillCoroutine` | 173행 부근 |
| 2 | `Core/BombBlockSystem.cs` | `BombCoroutine` | 289행 부근 |
| 3 | `Core/LaserBlockSystem.cs` | `LaserCoroutine` | 274행 부근 |
| 4 | `Core/DonutBlockSystem.cs` | `DonutCoroutine` | 299행 부근 |
| 5 | `Core/XBlockSystem.cs` | `XBlockCoroutine` | 315행 부근 |

### 변경 패턴 (5개 시스템 공통)

**1단계:** 파일 상단에 using 추가 (없는 경우)

```csharp
using JewelsHexaPuzzle.Managers;  // ScoreCalculator 접근용
```

**2단계:** 코루틴 시작부에 합산 변수 선언

```csharp
int blockScoreSum = 0;
```

**3단계:** 타겟 블록 파괴 분기에서 점수 누적 (else 분기 = 일반 블록 파괴)

```csharp
// 기존: 일반 블록 파괴 처리
else
{
    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);  // ← 추가
    Color blockColor = GemColors.GetColor(target.Data.gemType);
    destroyCoroutines.Add(StartCoroutine(DestroyBlockWith...(target, blockColor, ...)));
}
```

**4단계:** 최종 점수 계산 변경

```csharp
// 변경 전
int totalScore = 시스템기본점수 + targets.Count * 고정단가;

// 변경 후
int totalScore = 시스템기본점수 + blockScoreSum;
```

### 시스템별 기본점수 (유지)

| 시스템 | 기본점수 (변경 없음) |
|--------|-------------------|
| Drill | 100 |
| Bomb | 200 |
| Laser | 300 |
| Donut | 500 |
| XBlock | 500 |

---

## 구현 시 주의사항

1. **tier 읽는 시점:** 반드시 `ClearData()` 호출 **전에** `target.Data.tier`를 읽어야 합니다.
   현재 코드에서 이미 `ClearData()` 전에 타겟을 순회하고 있으므로 해당 위치에서 읽으면 됩니다.

2. **pending 특수 블록 제외:** `pendingSpecialBlocks`에 추가되는 블록은 파괴되지 않으므로
   점수에 포함하지 않습니다. 현재 `if/else` 분기가 이미 분리되어 있으므로
   `else` 쪽에서만 점수를 누적하면 됩니다.

3. **Drill / Laser 양방향 코루틴:** 양방향(또는 6방향) 코루틴이 동시 실행되지만
   Unity 코루틴은 단일 스레드이므로 경쟁 조건은 발생하지 않습니다.
   다만, `blockScoreSum` 변수를 코루틴 시작 메서드(`DrillCoroutine`/`LaserCoroutine`) 레벨에서
   선언하고 내부 코루틴에서 접근하는 구조가 필요합니다.
   - **Drill:** `DrillLineWithProjectile`에서 직접 누적하거나, 파괴 전 tier 합계를 미리 계산
   - **Laser:** `DestroyLine`에서 직접 누적하거나, 각 라인별 합계를 미리 계산

4. **Debug.Log 업데이트:** 기존 로그 형식에 맞춰 변경된 점수가 출력되도록 해 주세요.
   ```
   예) [BombBlockSystem] === BOMB COMPLETE === Score=1100 (base:200 + blockTierSum:900)
   ```

---

## 테스트 체크리스트

구현 후 아래 항목을 확인해 주세요:

- [ ] Normal 블록만 파괴 시: 블록당 50점 합산 확인
- [ ] Tier3 블록 파괴 시: 블록당 150점 합산 확인
- [ ] ProcessedGem 블록 파괴 시: 블록당 200점 합산 확인
- [ ] 특수 블록이 pending에 추가될 때: 해당 블록 점수 미포함 확인
- [ ] Laser 6방향 동시 파괴: 모든 방향 블록 점수 빠짐없이 합산 확인
- [ ] 연쇄(cascade) + 콤보 배율이 정상 적용되는지 확인
- [ ] 콘솔 로그에서 점수 산출 내역 확인

---

## 작업 범위 예상

- **수정 파일:** 5개
- **수정 분량:** 파일당 5~10줄 (총 25~50줄)
- **난이도:** 낮음 (기존 패턴 반복)
- **영향 범위:** 점수 계산만 변경, 이펙트/게임 로직 변경 없음
- **기존 API 변경:** 없음 (ScoreCalculator, ScoreManager 그대로)

상세 명세는 `Dev_SpecialBlockScoring_Spec.md`를 참고해 주세요.
궁금한 점이나 대안 의견이 있으시면 말씀 부탁드립니다.

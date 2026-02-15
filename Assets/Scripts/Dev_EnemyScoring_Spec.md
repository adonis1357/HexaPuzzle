# HexaPuzzle 적군 파괴 점수 체계 기획서

**작성일:** 2026-02-16
**작성자:** 기획 디렉터
**문서 버전:** v1.0
**관련 문서:** Dev_EnemySystem_Design.md, ScoreCalculator.cs

---

## 1. 기본 원칙

- 난이도가 높을수록 파괴 보상이 큼
- 블록 기본 점수(Normal 50)보다 항상 높아야 제거 동기 부여
- 특수 조건(무페널티 제거, 동시 제거 등) 달성 시 추가 보너스
- 아이템 사용은 편의 수단이므로 점수 메리트 감소

---

## 2. 적군별 기본 파괴 점수

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

---

## 3. 제거 방법별 보너스 배율

| 제거 방법 | 배율 | 설명 |
|----------|------|------|
| 일반 매칭 | **x1.0** | 기본 점수 |
| 특수 블록 (드릴/폭탄) | **x1.2** | 전략적 사용 보상 |
| 특수 블록 (레이저/도넛/X블록) | **x1.5** | 고급 특수 블록 보상 |
| 아이템 사용 | **x0.5** | 아이템은 편의 수단, 점수 메리트 감소 |

---

## 4. 특수 상황 보너스

| 상황 | 추가 점수 | 설명 |
|------|----------|------|
| 가시 기생충 무페널티 제거 | **+150** | 특수 블록/아이템으로 페널티 없이 제거 시 |
| 분열체 무분열 제거 | **+200** | 폭탄/레이저/도넛으로 분열 없이 제거 시 |
| 공명 트윈 동시 제거 | **+500** (쌍 보너스) | 한 턴에 쌍둥이 둘 다 제거 시 추가 |
| 반사 장막 1회 제거 | **+300** | 도넛/일반 매칭으로 한 방에 제거 시 |
| 카오스 군주 1회 제거 | **+1000** | 도넛/X블록으로 한 방에 제거 시 |
| 연쇄 중 적군 제거 | 기존 cascade 배율 적용 | cascade depth에 따라 1.0x~1.8x |

---

## 5. 멀티킬 보너스 (한 턴에 여러 적군 제거)

| 한 턴 적군 제거 수 | 추가 보너스 |
|------------------|-----------|
| 2마리 | **+200** |
| 3마리 | **+500** |
| 4마리 | **+1000** |
| 5마리+ | **+2000** |

---

## 6. 점수 계산 공식

### 6.1 단일 적군 파괴 점수

```
enemyScore = GetEnemyBaseScore(enemyType) * removalMethodMultiplier + specialBonus
```

### 6.2 턴 전체 적군 파괴 점수

```
turnEnemyScore = sum(각 적군 파괴 점수) + multiKillBonus
finalEnemyScore = turnEnemyScore * cascadeMultiplier
```

---

## 7. 점수 계산 예시

### 예시 1: 일반 매칭으로 색상 도둑 제거
- 기본 파괴 점수: 100 x 1.0 = **100점**

### 예시 2: 폭탄으로 분열체 2마리 + 색상 도둑 1마리 동시 제거 (cascade depth 1)
- 분열체: 300 x 1.2 = 360 x 2마리 = 720
- 무분열 보너스: +200 x 2 = 400
- 색상 도둑: 100 x 1.2 = 120
- 멀티킬(3마리): +500
- 소계: 1,740
- cascade 배율 (depth 1): x1.2
- **총 적군 파괴 점수: 2,088점**

### 예시 3: 도넛으로 카오스 군주 1회 제거
- 기본 파괴 점수: 1,500 x 1.5 = 2,250
- 1회 제거 보너스: +1,000
- **총 적군 파괴 점수: 3,250점**

### 예시 4: 레이저로 공명 트윈 쌍 동시 제거 (cascade depth 2)
- 공명 트윈 A: 600 x 1.5 = 900
- 공명 트윈 B: 600 x 1.5 = 900
- 동시 제거 보너스: +500
- 멀티킬(2마리): +200
- 소계: 2,500
- cascade 배율 (depth 2): x1.4
- **총 적군 파괴 점수: 3,500점**

### 예시 5: 일반 매칭으로 가시 기생충 제거 (페널티 발생)
- 기본 파괴 점수: 200 x 1.0 = 200
- 무페널티 보너스: 없음 (일반 매칭이므로)
- **총 적군 파괴 점수: 200점** (+ 턴 -1 페널티 발생)

---

## 8. ScoreCalculator 확장 명세

### 8.1 새 상수

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
private const float RemovalMultiplierSpecialAdvanced = 1.5f; // 레이저/도넛/X블록
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
```

### 8.2 새 메서드

```csharp
/// 적군 타입별 기본 파괴 점수 반환
public static int GetEnemyBaseScore(EnemyType type);

/// 제거 방법에 따른 배율 반환
public static float GetRemovalMethodMultiplier(RemovalMethod method);

/// 특수 상황 보너스 반환
public static int GetEnemySpecialBonus(EnemyType type, RemovalCondition condition);

/// 멀티킬 보너스 반환
public static int GetMultiKillBonus(int enemyCount);

/// 단일 적군 최종 점수 계산
public static int CalculateEnemyScore(EnemyType type, RemovalMethod method, RemovalCondition condition);

/// 턴 전체 적군 파괴 점수 계산
public static int CalculateTurnEnemyScore(List<EnemyKillData> kills, int cascadeDepth);
```

### 8.3 새 enum / 구조체

```csharp
public enum RemovalMethod
{
    Match,           // 일반 매칭
    SpecialBasic,    // 드릴/폭탄
    SpecialAdvanced, // 레이저/도넛/X블록
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

## 9. 밸런스 기준점

### 9.1 일반 블록 대비 적군 점수 비교

| 대상 | 점수 범위 |
|------|----------|
| Normal 블록 매칭 (3개) | 150 |
| Tier3 블록 매칭 (3개) | 450 |
| 색상 도둑 1마리 | 100~150 |
| 카오스 군주 1마리 | 1,500~3,250 |
| 폭탄 특수 블록 발동 | ~500 |
| 도넛 특수 블록 발동 | ~1,500 |

### 9.2 적군 점수가 전체에서 차지하는 비율 목표

- 초반 (Stage 1~15): 전체 점수의 **10~15%**
- 중반 (Stage 16~50): 전체 점수의 **20~30%**
- 후반 (Stage 51~100): 전체 점수의 **30~40%**

---

## 10. 요약

적군 파괴는 **위험 대비 보상** 원칙을 따릅니다.

- 쉬운 적(★): 100~200점 - 블록 2~4개 수준
- 중급 적(★★★~★★★★): 300~500점 - 특수 블록 발동급
- 고급 적(★★★★★): 600~700점 - 대형 매치급
- 보스(★★★★★★): 1,500점 - 도넛 발동급

멀티킬과 특수 조건 보너스를 통해 **전략적 플레이에 큰 점수 보상**을 제공하여 플레이어의 숙련도 향상 동기를 부여합니다.

---

**이 기획서는 Dev_EnemySystem_Design.md 및 ScoreCalculator.cs와 연계하여 구현해야 합니다.**

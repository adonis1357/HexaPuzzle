# 특수 블록 시스템 기술 명세서

| 항목 | 내용 |
|------|------|
| **작성일** | 2026-02-14 |
| **작성자** | 개발 PM |
| **수신** | 개발팀장 |
| **문서 버전** | v1.0 |
| **프로젝트** | MatchMine (Hexa Puzzle) |
| **네임스페이스** | `JewelsHexaPuzzle.Core` |

---

## 목차

1. [개요](#1-개요)
2. [생성 조건 상세](#2-생성-조건-상세)
3. [시스템별 상세 명세](#3-시스템별-상세-명세)
   - 3.1 DrillBlockSystem
   - 3.2 BombBlockSystem
   - 3.3 LaserBlockSystem
   - 3.4 DonutBlockSystem
   - 3.5 XBlockSystem
4. [공통 VFX 유틸리티](#4-공통-vfx-유틸리티)
5. [연쇄 처리 규칙](#5-연쇄-처리-규칙)
6. [Inspector 설정값 요약](#6-inspector-설정값-요약)
7. [ForceReset 및 안전장치](#7-forcereset-및-안전장치)
8. [주의사항 및 알려진 이슈](#8-주의사항-및-알려진-이슈)

---

## 1. 개요

본 문서는 MatchMine 헥사 퍼즐 프로젝트의 **4개 특수 블록 타입** (5개 코드 모듈)에 대한 기술 명세입니다. 각 시스템은 독립된 MonoBehaviour로 구현되어 있으며, `BlockRemovalSystem`과 연동하여 매칭 → 생성 → 발동 → 연쇄의 전체 흐름을 처리합니다.

> **중요:** 도넛(DonutBlockSystem)과 X블록(XBlockSystem)은 **동일한 특수 블록**입니다. 도넛 모양은 X블록의 시각적 표현이며, 생성 조건만 다릅니다 (7+매치 → DonutBlockSystem, 링매치 → XBlockSystem). 코드상 2개 시스템으로 분리되어 있으나 게임 내에서는 하나의 특수 블록 타입입니다.

### 특수 블록 요약 테이블

| 시스템 | SpecialBlockType | 소스 코드 규모 | 생성 조건 | 발동 효과 | 기본 점수 | 블록당 추가 점수 |
|--------|-----------------|---------------|----------|----------|----------|----------------|
| **DrillBlockSystem** | `Drill` | 1,101줄 | 4블록 매치 | 양방향 직선 파괴 (투사체) | 100 | +50 |
| **BombBlockSystem** | `Bomb` | 773줄 | 5블록 매치 | 중앙 + 인접 6칸 폭발 | 200 | +80 |
| **LaserBlockSystem** | `Laser` | 872줄 | 6블록 매치 | 3축 x 양방향 직선 파괴 (6방향) | 300 | +60 |
| **DonutBlockSystem** | `Rainbow` | 802줄 | 7+ 블록 매치 | 같은 색 보드 전체 제거 | 500 | +100 |
| **XBlockSystem** | `XBlock` | 833줄 | 링 매치 (중앙+6칸 동색) | 같은 색 보드 전체 제거 | 500 | +100 |

> **참고:** 위 DonutBlockSystem과 XBlockSystem은 **동일한 특수 블록**(도넛)의 2가지 생성 경로입니다. 발동 효과(같은 색 보드 전체 제거)와 점수가 동일합니다.

> **참고:** DonutBlock의 `SpecialBlockType`은 `Rainbow`로 정의되어 있습니다. 코드상 `BlockData.IsDonut()`은 `specialType == SpecialBlockType.Rainbow`를 확인합니다.

---

## 2. 생성 조건 상세

### 2.1 매칭 기반 생성 (MatchingSystem 패턴 판정)

특수 블록은 `MatchingSystem`이 매치 그룹을 판정한 후, 매치된 블록 수 및 패턴에 따라 결정됩니다.

| 매치 수 | 생성되는 특수 블록 | 판정 조건 |
|---------|------------------|----------|
| 4개 | Drill | 4블록 직선/L형 매치. `DetectDrillPattern()`으로 방향 결정 |
| 5개 | Bomb | 5블록 매치 |
| 6개 | Laser | 6블록 매치 |
| 7개 이상 | Donut (Rainbow) | 7개 이상 블록 매치 |

### 2.2 특수 패턴 기반 생성

| 패턴 | 생성되는 특수 블록 | 판정 조건 |
|------|------------------|----------|
| 링 매치 | XBlock | 중앙 블록 주변 6개가 모두 같은 색일 때, 중앙 위치에 생성 |

### 2.3 DrillDirection 판정

`DrillBlockSystem.DetectDrillPattern()` 메서드가 매칭된 블록 리스트를 분석하여 드릴 방향을 결정합니다.

| DrillDirection | hex 좌표 delta (positive/negative) | 화면상 방향 |
|---------------|-----------------------------------|-----------|
| `Vertical` | (0, +1) / (0, -1) | 화면 상하 (세로) |
| `Slash` | (+1, -1) / (-1, +1) | 화면 `/` 방향 (우상 ↔ 좌하) |
| `BackSlash` | (+1, 0) / (-1, 0) | 화면 `\` 방향 (우하 ↔ 좌상) |

각 방향의 화면상 각도:

| DrillDirection | 양(+) 방향 각도 | 음(-) 방향 |
|---------------|----------------|-----------|
| Vertical | -90도 (아래) | +90도 (위) |
| Slash | +30도 (우상) | -150도 (좌하) |
| BackSlash | -30도 (우하) | +150도 (좌상) |

---

## 3. 시스템별 상세 명세

---

### 3.1 DrillBlockSystem

**소스 파일:** `Assets/Scripts/Core/DrillBlockSystem.cs` (1,101줄)

#### 3.1.1 클래스 구조

```csharp
public class DrillBlockSystem : MonoBehaviour
{
    // SerializeField
    [SerializeField] private HexGrid hexGrid;
    [SerializeField] private BlockRemovalSystem removalSystem;
    [SerializeField] private float drillSpeed = 0.06f;
    [SerializeField] private float projectileSpeed = 1200f;
    [SerializeField] private int debrisCount = 6;
    [SerializeField] private float shakeIntensity = 8f;

    // Event
    public event System.Action<int> OnDrillComplete;

    // 상태
    public bool IsDrilling => activeDrillCount > 0;
    public List<HexBlock> PendingSpecialBlocks { get; }
}
```

#### 3.1.2 생성 흐름

1. `CreateDrillBlock(HexBlock block, DrillDirection direction, GemType gemType)` 호출
2. 블록의 `specialType`을 `Drill`로 설정, `drillDirection` 지정
3. 프로시저럴 아이콘 생성 (별도 Sprite 없음, 투사체 형태로 표현)

#### 3.1.3 발동 흐름

`ActivateDrill(HexBlock drillBlock)` 호출 시 다음 코루틴이 순차 실행됩니다.

```
1. PreFireCompression(drillBlock)
   → 블록 스케일: 1 → 0.85 (EaseInQuad) → 1.12 (EaseOutCubic), 0.1초
2. HitStop(Small = 0.02초)
   → TimeScale 0 → WaitForSecondsRealtime → SlowMo 복구
3. ZoomPunch(Small = 1.015x)
   → 보드 줌인(0.03초) → 줌아웃(0.08초)
4. ClearData
   → 블록 데이터 정리
5. 양방향 타겟 수집
   → GetBlocksInDirection() x 2 (positive + negative)
6. 발사 이펙트 (DrillLaunchEffect + LaunchBeam)
7. 양방향 동시 DrillLineWithProjectile
   → Coroutine drill1 + drill2 동시 시작, 양쪽 완료 대기
```

#### 3.1.4 투사체 구성

투사체는 `CreateProjectile()` 메서드로 프로시저럴 생성되며 다음 요소로 구성됩니다.

| 구성 요소 | 설명 |
|----------|------|
| **Glow** | 투사체 주변 발광 효과 |
| **DrillBody** | 투사체 본체 |
| **Trail** | 이동 궤적 잔상 |
| **Coreline** | 중심부 밝은 라인 |

#### 3.1.5 점수 계산

```
totalScore = 100 + (targets1.Count + targets2.Count) * 50
```

- 기본 점수: 100점
- 파괴 블록당: +50점
- `OnDrillComplete(totalScore)` 이벤트로 전파

#### 3.1.6 특수 블록 연쇄 처리

투사체 경로상 다른 특수 블록 히트 시:
1. `pendingSpecialBlocks`에 추가 (중복 방지)
2. 대상 블록에 `SetPendingActivation()` 호출
3. 대상 블록에 `StartWarningBlink(10f)` 호출 (10초간 점멸)
4. `FixedBlock`은 항상 제외 (`specialType != SpecialBlockType.FixedBlock` 체크)

---

### 3.2 BombBlockSystem

**소스 파일:** `Assets/Scripts/Core/BombBlockSystem.cs` (773줄)

#### 3.2.1 클래스 구조

```csharp
public class BombBlockSystem : MonoBehaviour
{
    // SerializeField
    [SerializeField] private HexGrid hexGrid;
    [SerializeField] private BlockRemovalSystem removalSystem;
    [SerializeField] private float explosionDelay = 0.08f;
    [SerializeField] private int debrisCount = 8;
    [SerializeField] private float shakeIntensity = 12f;

    // Event
    public event System.Action<int> OnBombComplete;

    // 상태
    public bool IsBombing => activeBombCount > 0;
    public List<HexBlock> PendingSpecialBlocks { get; }
}
```

#### 3.2.2 생성 흐름

1. 5블록 매치 판정 시 생성
2. 프로시저럴 아이콘: `CreateBombSprite(64)` (static 캐시)
   - 원형 본체 + 도화선 + 스파크

#### 3.2.3 발동 흐름

`ActivateBomb(HexBlock bombBlock)` 호출 시:

```
1. PreFireCompression(bombBlock)
   → 1 → 0.85 → 1.12, 0.1초
2. HitStop(Large = 0.04초)
3. ZoomPunch(Large = 1.025x)
4. ClearData
5. 6방향 이웃 수집 (hex 인접 6칸)
6. BombExplosionEffect + ScreenShake(Large)
   → 2중 충격파 링
   → 플래시 (FlashColorBomb: RGB 1.0, 0.9, 0.4)
   → 스파크 (Large 티어)
   → 파편 (Large 티어)
7. 순차 DestroyBlockWithExplosion
   → 각 블록에 대해 explosionDelay(0.08초) 간격으로 처리
```

#### 3.2.4 블록 파괴 연출

`DestroyBlockWithExplosion` 코루틴에서 각 블록 처리:

| 단계 | 설명 |
|------|------|
| pushDir 계산 | 폭탄 중앙에서 바깥으로 밀려나는 방향 벡터 |
| 이중 이징 | 확대(EaseOutCubic) → 찌그러짐(EaseInQuad) |
| DestroyFlashOverlay | 백색 오버레이 0.06초 |
| 파편 생성 | Large 티어 파편 |

#### 3.2.5 점수 계산

```
totalScore = 200 + targets.Count * 80
```

- 기본 점수: 200점
- 파괴 블록당: +80점
- `OnBombComplete(totalScore)` 이벤트로 전파

---

### 3.3 LaserBlockSystem

**소스 파일:** `Assets/Scripts/Core/LaserBlockSystem.cs` (872줄)

#### 3.3.1 클래스 구조

```csharp
public class LaserBlockSystem : MonoBehaviour
{
    // SerializeField
    [SerializeField] private HexGrid hexGrid;
    [SerializeField] private BlockRemovalSystem removalSystem;
    [SerializeField] private float laserSpeed = 0.04f;
    [SerializeField] private float beamDuration = 0.3f;
    [SerializeField] private float shakeIntensity = 10f;

    // Event
    public event System.Action<int> OnLaserComplete;

    // 상태
    public bool IsActivating => activeLaserCount > 0;
    public List<HexBlock> PendingSpecialBlocks { get; }
}
```

#### 3.3.2 생성 흐름

1. 6블록 매치 판정 시 생성
2. 프로시저럴 아이콘: `CreateLaserSprite(64)` (static 캐시)
   - 6각 별 모양 (3축 직선), 파란 계열 색상

#### 3.3.3 발동 흐름

`ActivateLaser(HexBlock laserBlock)` 호출 시:

```
1. PreFireCompression(laserBlock)
   → 1 → 0.85 → 1.12, 0.1초
2. HitStop(Large = 0.04초)
3. ZoomPunch(Large = 1.025x)
4. ClearData
5. 3축 x 2방향 타겟 수집
   → axes[] = { Vertical, Slash, BackSlash }
   → 각 축마다 positive + negative 방향
   → 총 6개 직선 (화면상 ★ 모양)
6. LaserCenterFlash (FlashColorLaser: RGB 0.7, 0.85, 1.0)
7. 6방향 LaserBeamEffect 동시 시작
8. ScreenShake(Large)
9. 6방향 동시 DestroyLine
   → 각 방향의 블록 리스트별 코루틴 동시 실행
```

#### 3.3.4 레이저 빔 구성

`LaserBeamEffect` 코루틴에서 생성되는 빔 구조:

| 구성 요소 | 크기 | 설명 |
|----------|------|------|
| **Main Beam** | 6px 너비 | 기본 레이저 빔, 블록 색상 기반 |
| **Core Beam** | 2px 너비 | 중심부 백색 빔 (고밝기) |
| **Shimmer** | 가변 | Sin파 기반 width 변조 (출렁임 효과) |

- `beamDuration`: 0.3초 (빔 표시 시간)

#### 3.3.5 점수 계산

```
totalScore = 300 + totalTargets * 60
```

- 기본 점수: 300점
- 파괴 블록당: +60점
- `OnLaserComplete(totalScore)` 이벤트로 전파

---

### 3.4 DonutBlockSystem

**소스 파일:** `Assets/Scripts/Core/DonutBlockSystem.cs` (802줄)

#### 3.4.1 클래스 구조

```csharp
public class DonutBlockSystem : MonoBehaviour
{
    // SerializeField
    [SerializeField] private HexGrid hexGrid;
    [SerializeField] private BlockRemovalSystem removalSystem;
    [SerializeField] private float waveDelay = 0.04f;
    [SerializeField] private float ringRotationSpeed = 360f;
    [SerializeField] private int sparkCount = 24;

    // Event
    public event System.Action<int> OnDonutComplete;

    // 상태
    public bool IsActivating => activeDonutCount > 0;
    public List<HexBlock> PendingSpecialBlocks { get; }
}
```

> **SpecialBlockType:** `Rainbow` (코드상 Donut = Rainbow)

#### 3.4.2 생성 흐름

1. 7개 이상 블록 매치 판정 시 생성
2. 프로시저럴 아이콘: `CreateDonutSprite(64)` (static 캐시)
   - 링(도넛) 모양 + 무지개 그라디언트

#### 3.4.3 발동 흐름

`ActivateDonut(HexBlock donutBlock)` 호출 시:

```
1. PreFireCompression(donutBlock)
   → 1 → 0.85 → 1.12, 0.1초
2. HitStop(Medium = 0.03초)
3. ZoomPunch(Small = 1.015x)
4. ClearData
5. 같은 색(gemType) 블록 보드 전체 수집
   → FixedBlock 제외
   → 특수 블록은 pendingSpecialBlocks에 추가
6. DonutCenterEffect
7. RainbowRingExpand (무지개 확장 링)
8. ScreenShake(Medium)
9. 거리순 정렬 (center → target 거리 기준)
10. 순차 DestroyBlockWithRainbow
    → waveDelay(0.04초) 간격
```

#### 3.4.4 이펙트 특징

| 이펙트 | 설명 |
|--------|------|
| **무지개 HSV 색상 회전** | 시간에 따라 Hue 값이 회전하여 무지개 색 순환 |
| **4중 회전 링** | `RainbowRingExpand`에서 4겹의 링이 동시에 확장하며 회전 (`ringRotationSpeed = 360도/초`) |
| **무지개 연결선** | 중앙(center)에서 각 타겟(target)으로의 연결선 표시 |

#### 3.4.5 블록 파괴 연출

`DestroyBlockWithRainbow` 코루틴:

| 단계 | 설명 |
|------|------|
| 이중 이징 | 확대(20% 구간, 1.12x) → 찌그러짐(나머지 구간) |
| 미세 회전 | 30도 회전 (rainbow identity 연출) |
| DestroyFlashOverlay | 백색 오버레이 0.06초 |

#### 3.4.6 점수 계산

```
totalScore = 500 + targets.Count * 100
```

- 기본 점수: 500점
- 파괴 블록당: +100점
- `OnDonutComplete(totalScore)` 이벤트로 전파

---

### 3.5 XBlockSystem

**소스 파일:** `Assets/Scripts/Core/XBlockSystem.cs` (833줄)

#### 3.5.1 클래스 구조

```csharp
public class XBlockSystem : MonoBehaviour
{
    // SerializeField
    [SerializeField] private HexGrid hexGrid;
    [SerializeField] private BlockRemovalSystem removalSystem;
    [SerializeField] private float waveDelay = 0.03f;
    [SerializeField] private int sparkCount = 20;

    // Event
    public event System.Action<int> OnXBlockComplete;

    // 상태
    public bool IsActivating => activeXBlockCount > 0;
    public List<HexBlock> PendingSpecialBlocks { get; }
}
```

#### 3.5.2 생성 조건

**링 매치 패턴:** 중앙 블록의 주변 6개가 모두 같은 색일 때, 중앙 위치에 XBlock이 생성됩니다.

> **도넛과 X블록은 동일한 특수 블록입니다.** DonutBlockSystem(7+매치)과 XBlockSystem(링매치)은 같은 블록의 2가지 생성 경로이며, 발동 효과는 동일합니다 (같은 색 보드 전체 제거).

#### 3.5.3 발동 흐름

`ActivateXBlock(HexBlock xBlock)` 호출 시:

```
1. PreFireCompression(xBlock)
   → 1 → 0.85 → 1.12, 0.1초
2. HitStop(Medium = 0.03초)
3. ZoomPunch(Small = 1.015x)
4. ClearData
5. 같은 색(gemType) 블록 보드 전체 수집
   → FixedBlock 제외
   → 특수 블록은 pendingSpecialBlocks에 추가
6. XCenterEffect
7. XWaveExpand (X자 확장 웨이브)
8. ScreenShake(Medium)
9. 거리순 정렬 (center → target 거리 기준)
10. 순차 DestroyBlockWithX
    → waveDelay(0.03초) 간격
```

#### 3.5.4 이펙트 특징

| 이펙트 | 설명 |
|--------|------|
| **X자 라인 플래시** | +/-45도 대각선 방향의 X 형태 플래시 |
| **대각선 방향 스파크 bias** | 스파크 파티클이 X축(대각선) 방향으로 편향 분포 |
| **MicroXFlash** | 각 블록 파괴 시 소형 X자 플래시 |

#### 3.5.5 블록 파괴 연출

`DestroyBlockWithX` 코루틴:

| 단계 | 설명 |
|------|------|
| 이중 이징 | 확대(20% 구간, 1.12x) → 찌그러짐(나머지 구간) |
| 45도 회전 | X identity를 나타내는 45도 회전 효과 |
| DestroyFlashOverlay | 백색 오버레이 0.06초 |

#### 3.5.6 점수 계산

```
totalScore = 500 + targets.Count * 100
```

- 기본 점수: 500점
- 파괴 블록당: +100점
- `OnXBlockComplete(totalScore)` 이벤트로 전파

---

## 4. 공통 VFX 유틸리티

5개 특수 블록 시스템은 동일한 VFX 유틸리티를 공통적으로 구현하고 있습니다. 상수값은 `VisualConstants` 정적 클래스(`Assets/Scripts/Utils/VisualConstants.cs`)에 통합 관리됩니다.

### 4.1 PreFireCompression (발동 전 압축 연출)

모든 특수 블록 발동 시 첫 번째로 실행되는 압축-팽창 애니메이션입니다.

| 파라미터 | 값 | 설명 |
|---------|-----|------|
| `PreFireDuration` | 0.1초 | 전체 애니메이션 시간 |
| `PreFireScaleMin` | 0.85 | 압축 최소 스케일 |
| `PreFireScaleMax` | 1.12 | 팽창 최대 스케일 |
| `PreFireBrightenAmount` | 0.25 | 밝기 증가량 |

```
애니메이션 곡선:
1.0 → 0.85 (EaseInQuad) → 1.12 (EaseOutCubic) → 발동
```

### 4.2 HitStop (타임스케일 조작)

특수 블록 발동 순간의 임팩트감을 위한 타임스케일 조작입니다.

| 파라미터 | 값 | 설명 |
|---------|-----|------|
| `HitStopDurationSmall` | 0.02초 | Drill용 |
| `HitStopDurationMedium` | 0.03초 | Donut, XBlock용 |
| `HitStopDurationLarge` | 0.04초 | Bomb, Laser용 |
| `HitStopSlowMoDuration` | 0.05초 | 복구 슬로모션 구간 |
| `HitStopSlowMoScale` | 0.4 | 슬로모션 시작 TimeScale |
| `HitStopCooldown` | 0.3초 | 연속 발동 방지 쿨다운 |

```
처리 흐름:
1. CanHitStop() 쿨다운 체크 → 불가 시 skip
2. RecordHitStop() 타임스탬프 기록
3. Time.timeScale = 0 (완전 정지)
4. WaitForSecondsRealtime(stopDuration)
5. SlowMo 복구: 0.4 → 1.0 (EaseOutCubic), 0.05초간
```

> **중요:** 쿨다운 0.3초 이내에 다시 호출되면 HitStop이 skip됩니다. `Time.unscaledTime` 기반으로 동작합니다.

### 4.3 ZoomPunch (보드 줌 효과)

특수 블록 발동 시 보드 전체에 적용되는 줌인-줌아웃 효과입니다.

| 파라미터 | 값 | 설명 |
|---------|-----|------|
| `ZoomPunchScaleSmall` | 1.015x | Drill, Donut, XBlock용 |
| `ZoomPunchScaleLarge` | 1.025x | Bomb, Laser용 |
| `ZoomPunchInDuration` | 0.03초 | 줌인 시간 |
| `ZoomPunchOutDuration` | 0.08초 | 줌아웃 시간 |

```
애니메이션 곡선:
1.0 → targetScale (0.03초) → 1.0 (0.08초)
```

### 4.4 HitStop / ZoomPunch 티어 배정

| 시스템 | Hit Stop 티어 | Hit Stop 시간 | Zoom Punch 티어 | Zoom Punch 스케일 |
|--------|-------------|-------------|----------------|-----------------|
| **Drill** | Small | 0.02초 | Small | 1.015x |
| **Bomb** | Large | 0.04초 | Large | 1.025x |
| **Laser** | Large | 0.04초 | Large | 1.025x |
| **Donut** | Medium | 0.03초 | Small | 1.015x |
| **XBlock** | Medium | 0.03초 | Small | 1.015x |

### 4.5 DestroyFlashOverlay (파괴 순간 백색 오버레이)

블록 파괴 순간에 표시되는 백색 플래시 오버레이입니다.

| 파라미터 | 값 |
|---------|-----|
| `DestroyFlashDuration` | 0.06초 |
| `DestroyFlashAlpha` | 0.7 |
| `DestroyFlashSizeMultiplier` | 1.3x |

### 4.6 BloomLayer (센터 플래시 발광 레이어)

센터 플래시 이후 표시되는 발광 레이어입니다.

| 파라미터 | 값 | 설명 |
|---------|-----|------|
| `BloomSizeMultiplier` | 1.8x | 원래 플래시 대비 크기 배율 |
| `BloomAlphaMultiplier` | 0.25 | 알파 값 (반투명) |
| `BloomLag` | 0.02초 | 플래시 대비 지연 시간 |

### 4.7 ScreenShake (화면 흔들림)

3티어로 구분된 화면 흔들림 효과입니다. 동시 실행에 안전하게 설계되어 있습니다.

| 티어 | 강도 (Intensity) | 지속 시간 (Duration) | 적용 시스템 |
|------|-----------------|--------------------|-----------|
| **Small** | 4 | 0.06초 | 블록 개별 히트 |
| **Medium** | 8 | 0.12초 | Donut, XBlock |
| **Large** | 12 | 0.20초 | Bomb, Laser |

- **동시 실행 안전:** `shakeCount` 기반으로 관리됩니다. 여러 shake가 동시에 실행되어도 마지막으로 끝나는 shake만 보드 위치를 원점으로 복구합니다.
- **감쇠:** `EaseInQuad` 함수 기반으로 시간에 따라 강도가 자연스럽게 감소합니다.

### 4.8 센터 플래시 (Center Flash)

특수 블록 발동 위치에 표시되는 플래시 효과입니다.

| 파라미터 | 값 |
|---------|-----|
| `FlashInitialSize` | 30 |
| `FlashDuration` | 0.35초 |
| `FlashExpand` | 7x |

시스템별 플래시 색상:

| 시스템 | 색상 상수 | RGB 값 |
|--------|----------|--------|
| Drill | `FlashColorDrill` | (1.0, 1.0, 0.8) - 따뜻한 황백색 |
| Bomb | `FlashColorBomb` | (1.0, 0.9, 0.4) - 따뜻한 황금색 |
| Laser | `FlashColorLaser` | (0.7, 0.85, 1.0) - 차가운 청백색 |

### 4.9 SetupEffectParent (이펙트 레이어)

각 시스템은 `Start()`에서 `SetupEffectParent()`를 호출하여 Canvas 내부에 전용 이펙트 레이어를 생성합니다.

| 시스템 | 이펙트 레이어 이름 |
|--------|------------------|
| DrillBlockSystem | `"DrillEffectLayer"` |
| BombBlockSystem | `"BombEffectLayer"` |
| LaserBlockSystem | `"LaserEffectLayer"` |
| DonutBlockSystem | `"DonutEffectLayer"` |
| XBlockSystem | `"XBlockEffectLayer"` |

생성 위치: `hexGrid.GetComponentInParent<Canvas>().transform` 하위

### 4.10 이중 이징 파괴 (Dual Easing Destroy)

모든 특수 블록의 블록 파괴 연출에 사용되는 공통 이징 패턴입니다.

| 파라미터 | 값 | 설명 |
|---------|-----|------|
| `DestroyExpandPhaseRatio` | 0.2 | 전체 시간의 20%를 확대 구간에 사용 |
| `DestroyExpandScale` | 1.12x | 확대 구간의 최대 스케일 |
| `DestroyDuration` | 0.14초 | 전체 파괴 애니메이션 시간 |

```
애니메이션 곡선:
확대 구간 (0~20%): 1.0 → 1.12x (EaseOutCubic)
찌그러짐 구간 (20~100%): 1.12x → 0.0 (EaseInQuad)
```

### 4.11 색상 유틸리티

`VisualConstants`에서 제공하는 시스템별 색상 조작 함수:

| 함수 | 특성 | 적용 대상 |
|------|------|----------|
| `Brighten(Color c)` | R/G/B 각 +0.3 | 범용 밝게 |
| `Darken(Color c)` | R/G/B 각 x0.6 | 범용 어둡게 |
| `BombBrighten(Color c)` | R+0.4, G+0.3, B+0.1 | Bomb 전용 (따뜻한 톤) |
| `LaserBrighten(Color c)` | R+0.3, G+0.3, B+0.4 | Laser 전용 (차가운 톤) |
| `DrillBrighten(Color c)` | R+0.35, G+0.35, B+0.2 | Drill 전용 (따뜻한 황색 톤) |

### 4.12 캐스케이드 배율

연쇄 깊이(depth)에 따른 이펙트 강도 배율:

```csharp
public static float GetCascadeMultiplier(int depth)
{
    return Mathf.Min(1f + depth * 0.15f, 1.8f);
}
```

| depth | 배율 |
|-------|------|
| 0 | 1.0x |
| 1 | 1.15x |
| 2 | 1.3x |
| 3 | 1.45x |
| 4+ | 1.8x (상한) |

---

## 5. 연쇄 처리 규칙

### 5.1 연쇄 발동 조건

특수 블록이 다른 특수 블록의 파괴 범위에 포함될 경우, 해당 특수 블록은 즉시 파괴되지 않고 **연쇄 대기 상태**로 전환됩니다.

### 5.2 연쇄 처리 흐름

```
1. 특수 블록 A 발동
   ↓
2. 파괴 범위 내 특수 블록 B 발견
   ↓
3. B를 pendingSpecialBlocks에 추가 (중복 방지: Contains 체크)
   ↓
4. B.SetPendingActivation() 호출
   → blockData.pendingActivation = true
   → 빨간색 테두리 표시
   ↓
5. B.StartWarningBlink(10f) 호출
   → 10초간 점멸 애니메이션 시작
   ↓
6. A의 발동 완료
   ↓
7. BlockRemovalSystem.CascadeWithPendingLoop() 진입
   ↓
8. 낙하 처리 (ProcessFalling)
   ↓
9. CollectAndClearPendingSpecials() 호출
   → 보드 전체에서 pendingActivation == true인 블록 수집
   → StopWarningBlink() 호출
   → pendingActivation = false로 리셋
   ↓
10. pending 블록들 순차 발동
    → ActivateSpecialAndWaitLocal() 코루틴
    ↓
11. 추가 매칭/pending 있으면 루프 반복 (최대 20회)
    ↓
12. 종료: isProcessing = false, OnCascadeComplete 이벤트 발사
```

### 5.3 CascadeWithPendingLoop 상세

`BlockRemovalSystem.CascadeWithPendingLoop()` 코루틴은 모든 연쇄를 완전히 처리하는 통합 루프입니다.

```
while (iteration < 20):
  1. ProcessFalling() → 낙하 + cascadeDelay
  2. CollectAndClearPendingSpecials() → pending 수집
  3. matchingSystem.FindMatches() → 새로운 매칭 확인
  4. pending == 0 AND matches == null → 루프 종료
  5. matches != null → ProcessMatchesInline() → continue
  6. pending > 0 → 유효성 필터링 → ActivateSpecialAndWaitLocal() → continue
```

- **최대 반복 횟수:** 20회 (안전장치)
- 20회 초과 시 `Debug.LogError` 출력 후 강제 종료

### 5.4 FixedBlock 제외 규칙

`SpecialBlockType.FixedBlock`은 모든 파괴 처리에서 제외됩니다.

- Drill: `target.Data.specialType != SpecialBlockType.FixedBlock` 체크
- Bomb: `target.Data.specialType != SpecialBlockType.FixedBlock` 체크
- Donut: `target.Data.specialType == SpecialBlockType.FixedBlock` 시 skip
- XBlock: 동일한 FixedBlock skip 로직
- Laser: 동일한 FixedBlock skip 로직

### 5.5 연쇄 대기 블록의 시각적 피드백

| 단계 | 시각 효과 |
|------|----------|
| `SetPendingActivation()` | 빨간색 테두리 표시 |
| `StartWarningBlink(10f)` | 10초간 점멸 애니메이션 |
| `StopWarningBlink()` | 실제 발동 시 점멸 종료 |

---

## 6. Inspector 설정값 요약

### 6.1 DrillBlockSystem

| 헤더 | 필드 | 타입 | 기본값 | 설명 |
|------|------|------|-------|------|
| References | hexGrid | HexGrid | null (auto-find) | 헥스 그리드 참조 |
| References | removalSystem | BlockRemovalSystem | null (auto-find) | 블록 제거 시스템 참조 |
| Drill Settings | drillSpeed | float | 0.06 | 드릴 이동 속도 (초/칸) |
| Drill Settings | projectileSpeed | float | 1200 | 투사체 이동 속도 (px/초) |
| Effect Settings | debrisCount | int | 6 | 파편 개수 |
| Effect Settings | shakeIntensity | float | 8 | 화면 흔들림 강도 |

### 6.2 BombBlockSystem

| 헤더 | 필드 | 타입 | 기본값 | 설명 |
|------|------|------|-------|------|
| References | hexGrid | HexGrid | null (auto-find) | 헥스 그리드 참조 |
| References | removalSystem | BlockRemovalSystem | null (auto-find) | 블록 제거 시스템 참조 |
| Bomb Settings | explosionDelay | float | 0.08 | 블록 간 폭발 지연 (초) |
| Effect Settings | debrisCount | int | 8 | 파편 개수 |
| Effect Settings | shakeIntensity | float | 12 | 화면 흔들림 강도 |

### 6.3 LaserBlockSystem

| 헤더 | 필드 | 타입 | 기본값 | 설명 |
|------|------|------|-------|------|
| References | hexGrid | HexGrid | null (auto-find) | 헥스 그리드 참조 |
| References | removalSystem | BlockRemovalSystem | null (auto-find) | 블록 제거 시스템 참조 |
| Laser Settings | laserSpeed | float | 0.04 | 레이저 진행 속도 (초/칸) |
| Laser Settings | beamDuration | float | 0.3 | 레이저 빔 표시 시간 (초) |
| Effect Settings | shakeIntensity | float | 10 | 화면 흔들림 강도 |

### 6.4 DonutBlockSystem

| 헤더 | 필드 | 타입 | 기본값 | 설명 |
|------|------|------|-------|------|
| References | hexGrid | HexGrid | null (auto-find) | 헥스 그리드 참조 |
| References | removalSystem | BlockRemovalSystem | null (auto-find) | 블록 제거 시스템 참조 |
| Donut Settings | waveDelay | float | 0.04 | 블록 간 파괴 지연 (초) |
| Donut Settings | ringRotationSpeed | float | 360 | 무지개 링 회전 속도 (도/초) |
| Effect Settings | sparkCount | int | 24 | 스파크 파티클 수 |

### 6.5 XBlockSystem

| 헤더 | 필드 | 타입 | 기본값 | 설명 |
|------|------|------|-------|------|
| References | hexGrid | HexGrid | null (auto-find) | 헥스 그리드 참조 |
| References | removalSystem | BlockRemovalSystem | null (auto-find) | 블록 제거 시스템 참조 |
| XBlock Settings | waveDelay | float | 0.03 | 블록 간 파괴 지연 (초) |
| Effect Settings | sparkCount | int | 20 | 스파크 파티클 수 |

### 6.6 VisualConstants 주요 상수 (코드 내 하드코딩)

VisualConstants는 `static class`이므로 Inspector에 노출되지 않습니다. 값 변경 시 소스 코드 수정이 필요합니다.

| 카테고리 | 상수 | 값 |
|---------|------|-----|
| PreFire | PreFireDuration | 0.1초 |
| PreFire | PreFireScaleMin / Max | 0.85 / 1.12 |
| HitStop | Small / Medium / Large | 0.02 / 0.03 / 0.04초 |
| HitStop | SlowMoDuration | 0.05초 |
| HitStop | SlowMoScale | 0.4 |
| HitStop | Cooldown | 0.3초 |
| ZoomPunch | ScaleSmall / ScaleLarge | 1.015 / 1.025 |
| ZoomPunch | InDuration / OutDuration | 0.03 / 0.08초 |
| DestroyFlash | Duration / Alpha / SizeMultiplier | 0.06초 / 0.7 / 1.3x |
| Bloom | SizeMultiplier / AlphaMultiplier / Lag | 1.8x / 0.25 / 0.02초 |
| Shake Small | Intensity / Duration | 4 / 0.06초 |
| Shake Medium | Intensity / Duration | 8 / 0.12초 |
| Shake Large | Intensity / Duration | 12 / 0.20초 |
| Destroy | Duration / ExpandPhaseRatio / ExpandScale | 0.14초 / 0.2 / 1.12x |
| Debris Base | Count / SpeedMin~Max / Gravity | 6 / 160~420 / -850 |
| Debris Large | Count / SpeedMin~Max | 10 / 200~500 |
| Spark Small | Count / SpeedMin~Max | 6 / 100~300 |
| Spark Medium | Count / SpeedMin~Max | 12 / 120~350 |
| Spark Large | Count / SpeedMin~Max | 18 / 150~450 |

---

## 7. ForceReset 및 안전장치

### 7.1 ForceReset 구현

5개 특수 블록 시스템은 모두 동일한 패턴의 `ForceReset()` 메서드를 구현하고 있습니다.

```csharp
public void ForceReset()
{
    activeCount = 0;                    // 활성 카운터 초기화
    pendingSpecialBlocks.Clear();       // pending 리스트 클리어
    StopAllCoroutines();                // 모든 코루틴 중단
    CleanupEffects();                   // 이펙트 오브젝트 일괄 정리
    Debug.LogWarning("[SystemName] ForceReset called");
}
```

`CleanupEffects()`는 `effectParent`의 자식 오브젝트를 역순으로 순회하며 `Destroy()`합니다.

### 7.2 GameManager의 ForceReset 호출 시점

`GameManager`에서 ForceReset이 호출되는 경우는 다음과 같습니다.

#### (A) PostRecovery 타임아웃

```csharp
// PostRecovery 중 timeout 발생 시
blockRemovalSystem.ForceReset();
drillSystem.ForceReset();
bombSystem.ForceReset();
donutSystem.ForceReset();
xBlockSystem.ForceReset();
laserSystem.ForceReset();
SetGameState(GameState.Playing);
```

#### (B) 스테이지 리셋

```csharp
// 스테이지 전체 리셋 시
rotationSystem.ForceReset();
blockRemovalSystem.ForceReset();
drillSystem.ForceReset();
bombSystem.ForceReset();
donutSystem.ForceReset();
xBlockSystem.ForceReset();
laserSystem.ForceReset();
// + 모든 블록의 pendingActivation/matched 상태 해제
```

#### (C) BRS 처리 중 재진입 방지

```csharp
// BRS가 아직 처리 중인데 새로운 매칭 시도 시
if (blockRemovalSystem.IsProcessing)
{
    blockRemovalSystem.ForceReset();
}
blockRemovalSystem.ProcessMatches(matches);
```

#### (D) PostRecovery 내 BRS 대기 타임아웃

```csharp
// PostRecovery 중 BRS가 3초 이상 busy 상태 시
if (blockRemovalSystem.IsProcessing)
{
    blockRemovalSystem.ForceReset();
}
```

### 7.3 CascadeWithPendingLoop 안전장치

| 안전장치 | 내용 |
|---------|------|
| 최대 반복 횟수 | 20회 (`maxIterations = 20`) |
| 유효성 필터링 | pending 블록의 null 체크, Data null 체크, gameObject null 체크, specialType != None 체크 |
| 무효 pending 시 | 모든 pending이 무효 → `break`로 루프 종료 |
| 최종 정리 | 루프 종료 후 반드시 `isProcessing = false`, `OnCascadeComplete` 발사 |

### 7.4 HitStop 쿨다운 안전장치

```csharp
private static float lastHitStopTime = -1f;

public static bool CanHitStop()
{
    return Time.unscaledTime - lastHitStopTime > HitStopCooldown; // 0.3초
}
```

- 연쇄 발동 시 HitStop이 0.3초 이내에 연속 호출되면 자동으로 skip됩니다.
- 이를 통해 연쇄 중 과도한 타임스케일 조작을 방지합니다.

### 7.5 참조 자동 탐색 (Auto-Find)

5개 시스템 모두 `Start()`에서 Inspector 참조가 null인 경우 `FindObjectOfType<>()`으로 자동 탐색합니다.

```csharp
if (hexGrid == null)
{
    hexGrid = FindObjectOfType<HexGrid>();
    if (hexGrid != null)
        Debug.Log("[System] HexGrid auto-found: " + hexGrid.name);
    else
        Debug.LogError("[System] HexGrid not found!");
}
if (removalSystem == null)
    removalSystem = FindObjectOfType<BlockRemovalSystem>();
```

---

## 8. 주의사항 및 알려진 이슈

### 8.1 아키텍처 주의사항

| 항목 | 설명 |
|------|------|
| **VFX 유틸리티 중복** | PreFireCompression, HitStop, ZoomPunch, ScreenShake 등의 공통 코루틴이 5개 시스템에 각각 독립 구현되어 있습니다. 현재 `VisualConstants` 클래스에 상수만 통합되어 있으며, 코루틴 로직 자체는 중복 상태입니다. |
| **프로시저럴 Sprite** | 모든 아이콘(Bomb, Laser, Donut, XBlock)이 런타임에 `Texture2D`를 생성하여 프로시저럴로 만들어집니다. static 캐시를 사용하지만, 도메인 리로드 시 재생성됩니다. |
| **effectParent 생명주기** | 각 시스템이 Canvas 하위에 독립적인 EffectLayer를 생성합니다. 씬 전환 또는 Canvas 재구성 시 참조가 끊어질 수 있습니다. |
| **TimeScale 조작** | HitStop이 `Time.timeScale`을 직접 조작합니다. 다른 시스템에서도 TimeScale을 사용하는 경우 충돌 가능성이 있습니다. 쿨다운 메커니즘으로 연속 호출은 방지되지만, 복구 실패 시 게임 속도가 비정상이 될 수 있습니다. |

### 8.2 성능 관련

| 항목 | 설명 |
|------|------|
| **동시 코루틴 수** | Laser의 경우 6방향 LaserBeamEffect + 6방향 DestroyLine이 동시에 실행될 수 있어, 최대 12개 이상의 코루틴이 병렬 실행됩니다. |
| **이펙트 오브젝트** | 프로시저럴 이펙트(Spark, Debris, Flash 등)가 `new GameObject()`로 매번 생성되므로, 대량 파괴 시 GC 부담이 발생할 수 있습니다. Object Pooling이 적용되어 있지 않습니다. |
| **Donut/XBlock 전체 탐색** | 같은 색 블록을 보드 전체에서 수집하므로, 보드 크기가 커질 경우 성능 영향이 있을 수 있습니다. |

### 8.3 SpecialBlockType 매핑 확인

코드상 enum과 시스템 간 매핑이 직관적이지 않은 부분이 있으므로 주의가 필요합니다.

```csharp
public enum SpecialBlockType
{
    None,
    MoveBlock,
    FixedBlock,
    TimeBomb,
    Drill,       // → DrillBlockSystem
    Bomb,        // → BombBlockSystem
    Rainbow,     // → DonutBlockSystem (이름 불일치 주의)
    XBlock,      // → XBlockSystem
    Laser        // → LaserBlockSystem
}
```

> `Rainbow` ↔ `DonutBlockSystem` 간의 네이밍 불일치에 주의해야 합니다. `BlockData.IsDonut()`은 `specialType == SpecialBlockType.Rainbow`를 체크합니다.

### 8.4 GemType 범위

```csharp
public static int ActiveGemTypeCount = 5; // 기본 5종: Red, Blue, Green, Yellow, Purple
```

- 기본 5종: Red(1), Blue(2), Green(3), Yellow(4), Purple(5)
- Orange(6)는 `ActiveGemTypeCount`를 6으로 변경해야 활성화됩니다.
- Ruby(7)~Amethyst(11)은 상위 티어 보석으로, 별도 활성화 조건이 필요합니다.
- Donut/XBlock의 "같은 색 전체 제거"는 `gemType` 기준이므로, 활성 GemType 수에 따라 파괴 범위가 달라집니다.

### 8.5 알려진 제한사항

| 항목 | 설명 |
|------|------|
| **CascadeWithPendingLoop 상한** | 최대 20회 반복 후 강제 종료됩니다. 이론적으로 20단계 이상의 연쇄는 처리되지 않습니다. |
| **ForceReset 후 이펙트 잔여** | `CleanupEffects()`가 `effectParent`의 직속 자식만 정리합니다. Canvas 외부에 생성된 이펙트 오브젝트는 잔류할 수 있습니다. |
| **HitStop 복구 실패** | HitStop 코루틴이 `StopAllCoroutines()`로 중단될 경우, `Time.timeScale`이 0 또는 0.4 상태로 남을 수 있습니다. ForceReset 시 TimeScale 복구 로직이 별도로 구현되어 있지 않으므로 주의가 필요합니다. |
| **동시 특수 블록 발동** | 여러 특수 블록이 동시에 발동될 경우, 각 시스템의 `activeDrillCount` / `activeBombCount` 등이 독립적으로 관리되므로 상태 추적에 주의해야 합니다. |

---

*본 문서는 소스 코드 기반으로 작성되었으며, 실제 동작은 런타임 환경에 따라 차이가 있을 수 있습니다.*

# 코어 게임 루프 시스템 개발 명세서

> **작성일:** 2026-02-14
> **작성자:** 개발 PM
> **수신:** 개발팀장
> **문서 버전:** v1.0

---

## 1. 개요

### 1-1. 배경 및 목적

본 문서는 헥사 퍼즐 프로젝트의 **코어 게임 루프**를 구성하는 8개 핵심 시스템에 대한 기술 명세서입니다.
좌표 계산부터 입력 처리, 블록 회전, 매칭 판정, 블록 제거 및 연쇄까지 전체 게임플레이 파이프라인을 다루며, 각 시스템의 내부 구조와 시스템 간 상호작용을 상세히 기술합니다.

**본 문서의 범위:**
- 게임플레이의 한 턴(입력 → 회전 → 매칭 → 제거 → 낙하 → 연쇄)을 구성하는 모든 시스템
- 각 시스템의 퍼블릭 인터페이스, 핵심 알고리즘, 설정값
- 시스템 간 이벤트 기반 통신 구조

**본 문서의 범위 밖:**
- 점수 계산 및 HUD UI (별도 문서 `Dev_ScoreSystem_Spec.md` 참조)
- 스테이지 구성 및 미션 시스템
- 특수 블록 개별 시스템(Drill, Bomb, Laser, Donut/XBlock)의 내부 상세 (※ 도넛과 X블록은 동일 특수 블록)

### 1-2. 핵심 시스템 목록

| 순번 | 시스템 | 파일 경로 | 코드 규모 | 역할 |
|:----:|--------|----------|:---------:|------|
| 1 | HexCoord | `Core/HexCoord.cs` | 177줄 | 헥사 축 좌표 계산 |
| 2 | HexGrid | `Core/HexGrid.cs` | 358줄 | 그리드 생성 및 블록 관리 |
| 3 | HexBlock | `Core/HexBlock.cs` | 1,002줄 | 블록 비주얼 컴포넌트 |
| 4 | BlockData | `Data/BlockData.cs` | 180줄 | 블록 데이터 모델 |
| 5 | InputSystem | `Core/InputSystem.cs` | 370줄 | 입력 처리 및 디스패치 |
| 6 | RotationSystem | `Core/RotationSystem.cs` | 308줄 | 3블록 클러스터 회전 |
| 7 | MatchingSystem | `Core/MatchingSystem.cs` | 517줄 | 삼각형/링 매칭 판정 |
| 8 | BlockRemovalSystem | `Core/BlockRemovalSystem.cs` | 1,770줄 | 블록 제거, 낙하, 연쇄 |

> 모든 경로의 루트는 `Assets/Scripts/` 입니다.

### 1-3. 게임 루프 요약

```
사용자 터치
  -> InputSystem: 클러스터 선택 또는 특수 블록 클릭
    -> RotationSystem: 3블록 120도 회전 시도
      -> MatchingSystem: 삼각형/링 매칭 검사
        -> BlockRemovalSystem: 매칭 블록 제거 + 특수 블록 생성/발동
          -> 낙하 + 빈 칸 채움
            -> 추가 매칭 검사 (연쇄 루프, 최대 20회)
              -> 연쇄 완료 -> 턴 종료
```

---

## 2. 좌표 시스템 (HexCoord)

**경로:** `Core/HexCoord.cs` (177줄)

### 2-1. 좌표 체계

헥사곤 그리드의 좌표 표현을 위해 **축 좌표계(Axial Coordinate System)**를 사용합니다.

| 좌표 | 설명 |
|:----:|------|
| `q` | 주 축 (열 방향) |
| `r` | 보조 축 (행 방향) |
| `s` | 파생 축 (항상 `s = -q - r`로 계산) |

세 좌표축의 합은 항상 0입니다: `q + r + s = 0`

### 2-2. 6방향 이웃 (Pointy-top 기준)

`Directions[6]` 배열로 정의된 6방향 오프셋입니다.

| 인덱스 | 방향 | (dq, dr) |
|:------:|:----:|:--------:|
| 0 | 오른쪽 위 | (+1, -1) |
| 1 | 오른쪽 | (+1, 0) |
| 2 | 오른쪽 아래 | (0, +1) |
| 3 | 왼쪽 아래 | (-1, +1) |
| 4 | 왼쪽 | (-1, 0) |
| 5 | 왼쪽 위 | (0, -1) |

### 2-3. 주요 메서드

| 메서드 | 시그니처 | 역할 |
|--------|---------|------|
| `ToWorldPosition` | `Vector2 ToWorldPosition(float hexSize)` | 축 좌표를 Unity 월드 좌표로 변환 |
| `FromWorldPosition` | `static HexCoord FromWorldPosition(Vector2 worldPos, float hexSize)` | 월드 좌표를 가장 가까운 헥사 좌표로 역변환 |
| `Round` | `static HexCoord Round(float q, float r)` | 부동소수점 좌표를 가장 가까운 정수 헥사 좌표로 반올림 |
| `DistanceTo` | `int DistanceTo(HexCoord other)` | 두 좌표 간 맨해튼 거리 (최소 이동 칸 수) |
| `GetHexesInRadius` | `static List<HexCoord> GetHexesInRadius(HexCoord center, int radius)` | 중심 좌표로부터 지정 반경 내의 모든 좌표 반환 |

### 2-4. 연산자 오버로드

| 연산자 | 동작 |
|:------:|------|
| `+` | 두 좌표의 q, r 합산 |
| `-` | 두 좌표의 q, r 차이 |
| `==` | q, r 값 동일 비교 |
| `!=` | q, r 값 비동일 비교 |

> **좌표 변환 공식 참고:**
> - Flat-top 헥사곤 기준 월드 좌표: `x = hexSize * 1.5 * q`, `y = hexSize * -sqrt(3) * (r + q/2)`
> - `Round()` 알고리즘은 큐브 좌표계 반올림 후 가장 큰 오차 축을 보정하는 방식입니다.

---

## 3. 그리드 시스템 (HexGrid)

**경로:** `Core/HexGrid.cs` (358줄)

### 3-1. Inspector 설정값

| 필드명 | 타입 | 기본값 | 용도 |
|--------|:----:|:------:|------|
| `gridRadius` | int | 5 | 그리드 반경 (중심으로부터의 최대 거리) |
| `hexSize` | float | 50 | 헥사곤 한 칸의 크기 (픽셀 단위) |
| `blockPrefab` | GameObject | (Inspector 연결) | 블록 프리팹 참조 |

### 3-2. Flat-top 배치 수식

그리드는 **Flat-top** 방식으로 배치됩니다. 축 좌표 `(q, r)`에서 월드 좌표 `(x, y)`로의 변환 공식은 다음과 같습니다.

```
x = hexSize * 1.5 * q
y = hexSize * -sqrt(3) * (r + q / 2)
```

### 3-3. 데이터 구조

내부적으로 `Dictionary<HexCoord, HexBlock>` 기반으로 블록을 관리합니다.

| 자료구조 | 키 | 값 | 용도 |
|---------|:--:|:--:|------|
| `blocks` (Dictionary) | HexCoord | HexBlock | 좌표 → 블록 빠른 조회 |

### 3-4. 주요 메서드

| 메서드 | 역할 | 상세 |
|--------|------|------|
| `InitializeGrid()` | 그리드 초기화 | `gridRadius` 범위 내 모든 유효 좌표에 `blockPrefab` 인스턴스 생성 |
| `PopulateWithNoMatches()` | 매칭 없는 초기 배치 | 무작위 배치 후 매칭 검사, 매칭 발견 시 재배치 (최대 **100회** 재시도) |
| `GetClusterAtPosition(Vector2 worldPos)` | 터치 위치의 3블록 클러스터 탐색 | 아래 3-5절 참조 |
| `GetNeighbors(HexCoord coord)` | 인접 6칸 반환 | 유효 좌표만 필터링하여 반환 |
| `GetBlock(HexCoord coord)` | 좌표로 블록 조회 | Dictionary 기반 O(1) 조회 |
| `FindBlocksByType(GemType)` | 젬 타입별 블록 검색 | 전체 순회 |
| `FindBlocksByType(SpecialBlockType)` | 특수 블록 타입별 검색 | 전체 순회 |
| `GetAllBlocks()` | 전체 블록 리스트 반환 | Dictionary의 Values를 List로 반환 |
| `IsValidCoord(HexCoord coord)` | 유효 좌표 확인 | Dictionary에 키 존재 여부 확인 |

### 3-5. 클러스터 탐색 알고리즘 (GetClusterAtPosition)

터치 위치에서 가장 가까운 **3블록 삼각형 클러스터**를 찾는 알고리즘입니다.

**탐색 과정:**

1. 터치 월드 좌표에서 가장 가까운 헥사 좌표로 변환합니다.
2. **클릭 반경** 검사: 가장 가까운 블록과의 거리가 `hexSize * 0.8` 이내인지 확인합니다.
3. 해당 블록을 중심으로 **6개 방향**의 삼각형 후보를 검사합니다.
4. 각 후보 삼각형은 중심 블록 + 인접 2블록으로 구성됩니다.
5. 터치 위치와 각 삼각형 중심까지의 거리를 비교하여 **가장 가까운 삼각형**을 선택합니다.

| 설정 항목 | 값 | 용도 |
|----------|:--:|------|
| 클릭 반경 | `hexSize * 0.8` | 터치 유효 범위 |
| 삼각형 후보 수 | 6개 | 방향당 1개 |

---

## 4. 블록 컴포넌트 (HexBlock)

**경로:** `Core/HexBlock.cs` (1,002줄)

### 4-1. 컴포넌트 의존성

- `RequireComponent(typeof(Image))`: Image 컴포넌트 필수
- `IPointerClickHandler`, `IPointerDownHandler`, `IPointerUpHandler` 인터페이스 구현

### 4-2. 비주얼 구성 요소

| 필드명 | 타입 | 역할 |
|--------|:----:|------|
| `backgroundImage` | Image | 헥사곤 배경 (SDF 기반 프로시저럴 생성) |
| `gemImage` | Image | 보석 이미지 (GemType별 색상 적용) |
| `borderImage` | Image | 테두리 (선택/매칭/경고 상태 표시) |
| `overlayImage` | Image | 오버레이 효과 |
| `drillIndicator` | Image | 드릴 방향 표시 아이콘 |
| `timerText` | Text | TimeBomb 카운트다운 표시 |

### 4-3. 프로시저럴 스프라이트 생성

정적(static) 메서드로 헥사곤 스프라이트를 런타임에 생성합니다. 생성된 스프라이트는 캐시하여 재사용합니다.

| 메서드 | 용도 | 특징 |
|--------|------|------|
| `CreateAAHexSprite` | 외곽 헥사곤 스프라이트 | SDF 기반 안티앨리어싱 |
| `CreateAAInnerHexSprite` | 내부 헥사곤 스프라이트 | SDF + bevel + specular 효과 |

### 4-4. 정적 캐시

모든 특수 블록 아이콘 스프라이트를 `static` 변수로 캐시합니다. 최초 1회 생성 후 모든 HexBlock 인스턴스에서 공유합니다.

### 4-5. 주요 메서드

| 메서드 | 역할 | 호출 시점 |
|--------|------|----------|
| `SetBlockData(BlockData)` | 블록 데이터 설정 + `UpdateVisuals()` 자동 호출 | 초기화, 낙하 후 새 블록 생성 |
| `UpdateVisuals()` | GemType별 색상 + SpecialBlockType별 아이콘 갱신 | 데이터 변경 시 |
| `UpdateSpecialIndicator()` | 모든 특수 블록 타입에 대한 통합 switch문 처리 | `UpdateVisuals()` 내부 호출 |
| `ClearData()` | 데이터 초기화 + 비주얼 리셋 | 블록 제거 시 |
| `SetMatched(bool)` | 매칭 상태 시각 표시 (하이라이트) | 매칭 판정 후 |
| `SetPendingActivation()` | 발동 대기 상태 설정 | 특수 블록 생성 직후 |
| `StartWarningBlink()` | 가속 빨간색 테두리 점멸 시작 | TimeBomb 카운트 위험 |
| `StopWarningBlink()` | 점멸 중단 | 블록 제거 또는 리셋 |

### 4-6. 외부 연동

| 시스템 | 역할 |
|--------|------|
| `GemSpriteProvider` | 젬 타입별 스프라이트 제공 |
| `GemMaterialManager` | 젬 타입별 머티리얼 관리 |

---

## 5. 데이터 모델 (BlockData)

**경로:** `Data/BlockData.cs` (180줄)

### 5-1. 열거형 정의

#### GemType (보석 타입)

| 값 | 이름 | 비고 |
|:--:|------|------|
| 0 | None | 빈 칸 |
| 1 | Red | 빨간색 |
| 2 | Orange | 주황색 |
| 3 | Yellow | 노란색 |
| 4 | Green | 초록색 |
| 5 | Blue | 파란색 |
| 6 | Purple | 보라색 |
| 7 | Pink | 분홍색 |
| 8 | Cyan | 청록색 |
| 9 | White | 흰색 |
| 10 | Black | 검정색 |

> **활성 젬 수:** `GemTypeHelper.ActiveGemTypeCount = 5`
> 게임에서 실제로 사용되는 젬 종류 수이며, `GetRandom()` 호출 시 이 범위 내에서 무작위 선택됩니다.

#### SpecialBlockType (특수 블록 타입)

| 값 | 이름 | 생성 조건 | 역할 |
|:--:|------|----------|------|
| 0 | None | - | 일반 블록 |
| 1 | Drill | 4매치 | 지정 방향으로 관통 제거 |
| 2 | Bomb | 5매치 | 주변 범위 폭발 제거 |
| 3 | Rainbow (Donut) | 7+매치 | 동색 전체 제거 |
| 4 | TimeBomb | 스테이지 배치 | 제한 턴 내 미제거 시 게임 오버 |
| 5 | FixedBlock | 스테이지 배치 | 이동 불가 장애물 |
| 6 | XBlock | 6칸 링매치 | 동색 전체 제거 (**도넛과 동일 블록**, 생성 조건만 다름) |
| 7 | Laser | 6매치 | 직선 방향 관통 제거 |
| 8 | Vinyl | 스테이지 배치 | 다층 보호막 (제거 시 레이어 감소) |

#### DrillDirection (드릴 방향)

| 값 | 이름 | 방향 |
|:--:|------|------|
| 0 | Vertical | 수직 |
| 1 | Slash | 우상-좌하 대각선 (/) |
| 2 | BackSlash | 좌상-우하 대각선 (\) |

#### BlockTier (블록 등급)

| 값 | 이름 | 기본 점수 | 설명 |
|:--:|------|:---------:|------|
| 0 | Normal | 50 | 일반 보석 |
| 1 | Tier1 | 75 | 1등급 보석 |
| 2 | Tier2 | 100 | 2등급 보석 |
| 3 | Tier3 | 150 | 3등급 보석 |
| 4 | ProcessedGem | 200 | 가공 보석 |

### 5-2. BlockData 클래스

블록 한 칸의 모든 상태 정보를 담는 데이터 클래스입니다.

| 필드명 | 타입 | 기본값 | 설명 |
|--------|:----:|:------:|------|
| `gemType` | GemType | None | 보석 색상 |
| `specialType` | SpecialBlockType | None | 특수 블록 타입 |
| `timeBombCount` | int | 0 | TimeBomb 남은 턴 수 |
| `vinylLayer` | int | 0 | Vinyl 남은 레이어 수 |
| `hasChain` | bool | false | 체인 잠금 여부 |
| `drillDirection` | DrillDirection | Vertical | 드릴 관통 방향 |
| `tier` | BlockTier | Normal | 블록 등급 |
| `pendingActivation` | bool | false | 발동 대기 상태 (생성 직후 ~ 발동 전) |

| 메서드 | 역할 |
|--------|------|
| `Clone()` | 모든 필드의 Deep Copy를 반환 (회전 시 데이터 교환에 사용) |

### 5-3. 유틸리티 클래스

| 클래스 | 역할 | 주요 멤버 |
|--------|------|----------|
| `GemTypeHelper` | 젬 타입 관련 유틸리티 | `ActiveGemTypeCount = 5`, `GetRandom()` |
| `GemColors` | 젬별 색상 매핑 | `static` 딕셔너리로 각 GemType에 대응하는 `Color` 정의 |

---

## 6. 입력 처리 (InputSystem)

**경로:** `Core/InputSystem.cs` (370줄)

### 6-1. 입력 모드

플랫폼에 따라 조건부로 마우스 또는 터치 입력을 처리합니다.

| 플랫폼 | 입력 방식 |
|--------|----------|
| 에디터 / PC | 마우스 입력 (`Input.mousePosition`) |
| 모바일 | 터치 입력 (`Input.GetTouch`) |

### 6-2. 클러스터 프리뷰

사용자가 터치/클릭한 위치에서 3블록 클러스터를 미리 하이라이트하여 보여줍니다.

| 동작 | 설명 |
|------|------|
| 터치 다운 | 클러스터 후보 탐색 → 3블록 하이라이트 표시 |
| 터치 업 | 회전 실행 또는 해제 |

### 6-3. 특수 블록 클릭 처리 (TryActivateSpecialBlock)

특수 블록을 직접 클릭하여 발동하는 기능입니다. 타입별로 클릭 반경이 다릅니다.

| 특수 블록 타입 | 클릭 반경 | 비고 |
|:-------------:|:---------:|------|
| Drill | `hexSize * 0.8` | 일반 반경 (넓음) |
| Bomb | `hexSize * 0.56` | 좁은 반경 (오클릭 방지) |
| XBlock | `hexSize * 0.56` | 좁은 반경 |
| Laser | `hexSize * 0.56` | 좁은 반경 |

> Bomb, XBlock, Laser는 주변 블록과 밀접하게 배치되므로, 좁은 클릭 반경(`hexSize * 0.56`)을 적용하여 의도하지 않은 인접 블록 클릭을 방지합니다.

### 6-4. 가드 체크 (입력 차단 조건)

아래 조건 중 하나라도 `true`이면 입력을 무시합니다.

| 가드 플래그 | 의미 |
|------------|------|
| `!isEnabled` | 입력 시스템 비활성화 상태 |
| `IsRotating` | 회전 애니메이션 진행 중 |
| `IsDrilling` | 드릴 발동 진행 중 |
| `IsBombing` | 폭탄 발동 진행 중 |
| `IsActivating` | 특수 블록 발동 진행 중 |
| `IsProcessing` | 블록 제거/연쇄 처리 진행 중 |

### 6-5. 이벤트

| 이벤트명 | 시그니처 | 발생 시점 |
|---------|---------|----------|
| `OnRotationRequested` | `Action<List<HexBlock>, bool>` | 3블록 클러스터 회전 요청 시 (블록 목록, 시계 방향 여부) |
| `OnSpecialBlockActivated` | `Action<HexBlock>` | 특수 블록 클릭 발동 시 |

---

## 7. 회전 시스템 (RotationSystem)

**경로:** `Core/RotationSystem.cs` (308줄)

### 7-1. Inspector 설정값

| 필드명 | 타입 | 기본값 | 용도 |
|--------|:----:|:------:|------|
| `rotationDuration` | float | 0.3 | 회전 애니메이션 지속 시간(초) |
| `rotationCurve` | AnimationCurve | (Inspector 설정) | 회전 이징 커브 |

### 7-2. 회전 로직 (TryRotate)

3블록 클러스터를 입력 방향으로 회전하고, 매칭 여부를 확인합니다.

**회전 시도 순서:**

```
1. 120도 회전 (1차 시도)
   -> 매칭 발견? -> 성공, 매칭 처리로 전달
   -> 매칭 없음? -> 2차 시도

2. 240도 회전 (2차 시도, 총 누적)
   -> 매칭 발견? -> 성공, 매칭 처리로 전달
   -> 매칭 없음? -> 원위치 복구

3. 원위치 복구 (360도 = 제자리)
   -> 회전 실패, 턴 미소모
```

### 7-3. 블록 정렬 (SortBlocksClockwise)

회전 전 3블록을 삼각형 중심 기준으로 **각도 순서**에 따라 정렬합니다. 이는 데이터 교환의 일관성을 보장합니다.

### 7-4. 데이터 교환 (SwapData)

`BlockData.Clone()` 기반의 안전한 데이터 교환을 수행합니다. 원본 데이터 손실을 방지하기 위해 반드시 Clone을 사용합니다.

| 방향 | 교환 규칙 (인덱스 기준) |
|:----:|:---------------------:|
| CW (시계 방향) | 0 <- 2, 1 <- 0, 2 <- 1 |
| CCW (반시계 방향) | 0 <- 1, 1 <- 2, 2 <- 0 |

> 인덱스는 `SortBlocksClockwise`로 정렬된 순서 기준입니다.

### 7-5. 회전 애니메이션 (AnimateRotation)

| 항목 | 내용 |
|------|------|
| 회전축 | Z축 (2D 평면 기준) |
| 회전 방식 | `Quaternion.Euler(0, 0, angle)` |
| 회전 중심 | 3블록 삼각형의 무게중심 |
| 회전 각도 | 120도 (CW: -120도, CCW: +120도) |
| 이징 | Inspector에서 설정한 `AnimationCurve` 적용 |

### 7-6. 강제 리셋 (ForceReset)

회전 중 예외 상황(stuck) 발생 시 모든 상태를 초기화합니다.

| 리셋 대상 | 동작 |
|----------|------|
| `IsRotating` 플래그 | `false`로 리셋 |
| 블록 위치 | 원래 좌표로 복원 |
| 진행 중 코루틴 | 중단 |

---

## 8. 매칭 시스템 (MatchingSystem)

**경로:** `Core/MatchingSystem.cs` (517줄)

### 8-1. 매칭 판정 개요

헥사 그리드에서는 전통적인 직선 3매치 대신 **삼각형 매칭**과 **링 매칭** 두 가지 패턴을 사용합니다.

| 매칭 유형 | 조건 | 결과 |
|----------|------|------|
| 삼각형 매칭 | 인접한 3칸이 삼각형을 이루며 동일 색상 | 기본 제거 + 특수 블록 생성 가능 |
| 링 매칭 | 중앙 블록 주변 6칸이 모두 동일 색상 | XBlock 생성 |

### 8-2. 주요 메서드

| 메서드 | 역할 | 반환 |
|--------|------|------|
| `FindMatches()` | 전체 그리드에서 삼각형 + 링 매칭 통합 탐색 | `List<MatchGroup>` |
| `FindTrianglesContaining(HexCoord coord)` | 특정 좌표를 포함하는 모든 동색 삼각형 탐색 | 삼각형 리스트 |
| `MergeAdjacentMatches()` | 겹치는 동색 삼각형을 하나의 큰 매치 그룹으로 병합 | 병합된 MatchGroup 리스트 |
| `FindRingMatches()` | 중앙 주변 6칸 동색 링 패턴 검사 | 링 MatchGroup 리스트 |

### 8-3. 삼각형 매칭 상세

`FindTrianglesContaining(coord)` 알고리즘:

1. 지정 좌표의 블록 색상을 기준색으로 설정합니다.
2. 6방향에 대해 해당 좌표 + 인접 2칸의 삼각형을 구성합니다.
3. 삼각형의 3블록이 모두 기준색과 동일하면 매칭으로 판정합니다.

### 8-4. 인접 매칭 병합 (MergeAdjacentMatches)

여러 삼각형이 동일 색상이고 블록이 겹치면, 하나의 큰 매치 그룹으로 병합합니다. 병합된 그룹의 크기에 따라 생성되는 특수 블록이 결정됩니다.

### 8-5. 특수 블록 생성 패턴

매치 그룹의 **블록 개수**에 따라 생성되는 특수 블록 타입이 결정됩니다.

| 매치 블록 수 | 생성 특수 블록 | SpecialBlockType | 비고 |
|:-----------:|:------------:|:----------------:|------|
| 3개 | 없음 | None | 일반 제거만 수행 |
| 4개 | 드릴 | Drill | 방향 자동 결정 (아래 참조) |
| 5개 | 폭탄 | Bomb | 범위 폭발 |
| 6개 | 레이저 | Laser | 직선 관통 |
| 7개 이상 | 도넛 (레인보우) | Rainbow | 동색 전체 제거 (도넛 = X블록, 동일 특수 블록) |

> **링 매칭(6칸 링):** 매치 블록 수와 무관하게 항상 **XBlock**을 생성합니다. (도넛과 동일한 특수 블록이며, 생성 조건만 다릅니다)

### 8-6. 드릴 방향 자동 결정

4매치로 Drill이 생성될 때, 매치 그룹의 블록 배치 형태를 분석하여 방향을 자동 결정합니다.

| DrillDirection | 조건 |
|:--------------:|------|
| Vertical | 블록이 수직 방향으로 배열된 경우 |
| Slash | 블록이 `/` 대각선 방향으로 배열된 경우 |
| BackSlash | 블록이 `\` 대각선 방향으로 배열된 경우 |

### 8-7. MatchGroup 구조체

매칭 판정 결과를 담는 데이터 구조입니다.

| 필드 | 타입 | 설명 |
|------|:----:|------|
| `blocks` | List\<HexBlock\> | 매칭된 블록 리스트 |
| `gemType` | GemType | 매칭된 색상 |
| `score` | int | 매치 점수 |
| `createdSpecialType` | SpecialBlockType | 생성할 특수 블록 타입 |
| `specialSpawnBlock` | HexBlock | 특수 블록을 생성할 위치의 블록 |
| `drillDirection` | DrillDirection | 드릴 생성 시 방향 |

---

## 9. 블록 제거 및 연쇄 시스템 (BlockRemovalSystem)

**경로:** `Core/BlockRemovalSystem.cs` (1,770줄)

프로젝트에서 가장 큰 단일 시스템이며, 매칭 결과 처리부터 블록 제거, 특수 블록 생성/발동, 낙하 물리, 연쇄 루프까지 전체 후처리 파이프라인을 담당합니다.

### 9-1. 외부 참조

| 참조 대상 | 역할 |
|----------|------|
| HexGrid | 그리드 조회 및 블록 접근 |
| MatchingSystem | 연쇄 시 추가 매칭 검사 |
| DrillBlockSystem | 드릴 블록 발동 처리 |
| BombBlockSystem | 폭탄 블록 발동 처리 |
| XBlockSystem | 도넛(X블록) 발동 처리 - 링매치 생성 경로 |
| LaserBlockSystem | 레이저 블록 발동 처리 |
| DonutBlockSystem | 도넛(레인보우) 발동 처리 - 7+매치 생성 경로 (※ XBlock과 동일 특수 블록) |

### 9-2. 이벤트

| 이벤트명 | 시그니처 | 발생 시점 |
|---------|---------|----------|
| `OnBlocksRemoved` | `Action<int, int, Vector3>` | 블록 제거 완료 시 (제거 수, 연쇄 깊이, 평균 위치) |
| `OnCascadeComplete` | `Action` | 모든 연쇄 처리 완료 시 |
| `OnBigBang` | `Action` | BigBang(전체 제거) 발동 시 |

### 9-3. 처리 파이프라인 (ProcessMatches → CascadeWithPendingLoop)

전체 처리 흐름은 다음과 같습니다.

```
ProcessMatches(matches) -- 매칭 처리 진입점
  └-> ProcessMatchesInline
       ├-> (1) 매칭 블록 하이라이트 표시
       ├-> (2) 특수 블록 생성 (합체 애니메이션 포함)
       ├-> (3) 일반 블록 제거 (OnBlocksRemoved 이벤트 발생)
       └-> (4) 특수 블록 발동 (ActivateSpecialAndWaitLocal)
            └-> CascadeWithPendingLoop -- 연쇄 루프 진입
                 ├-> 낙하 처리 (물리 기반)
                 ├-> 빈 칸 새 블록 채우기
                 ├-> pending 특수 블록 발동 확인
                 ├-> 추가 매칭 검사
                 └-> 매칭 발견 시 -> ProcessMatchesInline으로 재귀
                      (최대 20회 반복)
```

### 9-4. 낙하 물리 시스템

블록 제거 후 빈 칸을 채우기 위한 물리 기반 낙하 시스템입니다.

| 설정 항목 | 값 | 단위 | 설명 |
|----------|:--:|:----:|------|
| `gravity` | 2500 | px/s^2 | 중력 가속도 |
| `maxFallSpeed` | 1500 | px/s | 최대 낙하 속도 (속도 상한) |
| `bounceRatio` | 0.3 | - | 바운스 시 속도 감쇠 비율 (반발 계수) |
| `bounceThreshold` | 50 | px/s | 이 속도 이하에서 바운스 중단 (착지 판정) |

**낙하 과정:**
1. 제거된 블록 위의 블록들이 아래로 낙하합니다.
2. 목표 위치에 도달하면 `bounceRatio` 비율로 튕깁니다.
3. 바운스 속도가 `bounceThreshold` 이하가 되면 착지로 판정합니다.

### 9-5. 슬롯 캐시 시스템

낙하 경로 계산의 효율을 높이기 위해 슬롯 위치와 열(column) 정보를 캐시합니다.

| 캐시 항목 | 역할 |
|----------|------|
| `slotPositions` | 각 좌표의 월드 위치 캐시 |
| `columnCache` | 열(column)별 블록 좌표 리스트 (낙하 순서 계산용) |

### 9-6. 특수 블록 합체 애니메이션 (SpecialBlockMergeAnimation)

4매치 이상으로 특수 블록이 생성될 때 재생되는 시각 효과입니다.

| 연출 요소 | 설명 |
|----------|------|
| 전기 아크 | 매칭된 블록들 사이의 전기 효과 |
| 번개선 | 블록 → 생성 위치로 수렴하는 번개 라인 |
| 스파크 | 합체 완료 시점의 방사형 스파크 |

### 9-7. 특수 블록 생성 (CreateSpecialBlock)

`MatchGroup.createdSpecialType`에 따라 적절한 특수 블록을 생성합니다.

| 특수 블록 | 생성 위치 | 추가 설정 |
|----------|----------|----------|
| Drill | `specialSpawnBlock` | `drillDirection` 적용 |
| Bomb | `specialSpawnBlock` | - |
| Laser | `specialSpawnBlock` | - |
| Rainbow (Donut) | `specialSpawnBlock` | - |
| XBlock | `specialSpawnBlock` (링 중앙) | - |

### 9-8. 특수 블록 발동 (ActivateSpecialAndWaitLocal)

생성된 또는 기존의 특수 블록을 발동하고 완료를 대기합니다.

| 항목 | 내용 |
|------|------|
| 발동 방식 | 타입별 switch문으로 해당 시스템 호출 |
| 완료 대기 | 각 시스템의 완료 콜백 대기 |
| 안전 타임아웃 | **각 타입별 5초** (타임아웃 초과 시 강제 완료 처리) |

### 9-9. 연쇄 루프 (CascadeWithPendingLoop)

낙하 후 추가 매칭을 반복 검사하는 핵심 루프입니다.

| 설정 항목 | 값 | 설명 |
|----------|:--:|------|
| 최대 연쇄 횟수 | 20회 | 무한 루프 방지 |

**루프 1회 사이클:**

```
(1) 낙하 처리 (물리 기반)
(2) 빈 칸에 새 블록 생성
(3) pending 상태 특수 블록 확인 → 발동
(4) 추가 매칭 검사 (MatchingSystem.FindMatches)
(5) 매칭 발견 → ProcessMatchesInline → (1)로 복귀
(5') 매칭 없음 → 루프 종료, OnCascadeComplete 발생
```

### 9-10. BigBang

전체 그리드의 모든 블록을 제거하고 새로 채우는 특수 기능입니다.

| 단계 | 동작 |
|:----:|------|
| 1 | 모든 블록 데이터 초기화 (`ClearData`) |
| 2 | `OnBigBang` 이벤트 발생 |
| 3 | 새 블록으로 전체 낙하 채움 |

### 9-11. 강제 리셋 (ForceReset)

| 리셋 대상 | 동작 |
|----------|------|
| 처리 플래그 | 모든 `Is~` 플래그 `false`로 리셋 |
| 진행 중 이펙트 | 모든 VFX 즉시 중단 |
| 진행 중 코루틴 | 전체 코루틴 중단 |
| 블록 상태 | matched, pending 상태 초기화 |

---

## 10. 시스템 간 이벤트 흐름

### 10-1. 일반 회전 매칭 흐름 (1턴)

```
[사용자 터치]
  └-> InputSystem
       ├-> GetClusterAtPosition(worldPos)     [HexGrid 호출]
       ├-> 3블록 프리뷰 하이라이트
       └-> OnRotationRequested(blocks, clockwise)
            └-> RotationSystem.TryRotate(blocks, clockwise)
                 ├-> SortBlocksClockwise(blocks)
                 ├-> SwapData (Clone 기반)
                 ├-> AnimateRotation (0.3초)
                 └-> MatchingSystem.FindMatches()
                      ├-> [매칭 있음]
                      │    └-> BlockRemovalSystem.ProcessMatches(matches)
                      │         ├-> 하이라이트 표시
                      │         ├-> 특수 블록 생성 (MergeAnimation)
                      │         ├-> 일반 블록 제거
                      │         │    └-> OnBlocksRemoved(count, depth, pos)
                      │         │         └-> ScoreManager.AddMatchScore()
                      │         ├-> 특수 블록 발동 (5초 타임아웃)
                      │         └-> CascadeWithPendingLoop
                      │              ├-> 낙하 (gravity=2500)
                      │              ├-> 추가 매칭 검사
                      │              └-> [연쇄 완료] OnCascadeComplete
                      │                   └-> GameManager (턴 종료 처리)
                      └-> [매칭 없음]
                           └-> 원위치 복구 (턴 미소모)
```

### 10-2. 특수 블록 직접 클릭 발동 흐름

```
[사용자 터치 - 특수 블록 위치]
  └-> InputSystem.TryActivateSpecialBlock
       ├-> 클릭 반경 검사 (Bomb/XBlock/Laser: 0.56, Drill: 0.8)
       └-> OnSpecialBlockActivated(block)
            └-> BlockRemovalSystem.ActivateSpecialAndWaitLocal(block)
                 ├-> [Drill]  -> DrillBlockSystem.Activate()
                 ├-> [Bomb]   -> BombBlockSystem.Activate()
                 ├-> [XBlock] -> XBlockSystem.Activate()
                 ├-> [Laser]  -> LaserBlockSystem.Activate()
                 └-> [Donut]  -> DonutBlockSystem.Activate()
                      └-> 각 시스템 완료 콜백
                           └-> CascadeWithPendingLoop (연쇄 루프)
                                └-> OnCascadeComplete
```

### 10-3. 연쇄(Cascade) 내부 루프 상세

```
CascadeWithPendingLoop (반복 최대 20회)
  │
  ├-> [1] 낙하 처리
  │    ├-> columnCache 기반 열별 처리
  │    ├-> 물리: gravity=2500, max=1500, bounce=0.3
  │    └-> bounceThreshold=50 이하 시 착지
  │
  ├-> [2] 빈 칸 채우기
  │    └-> 새 블록 생성 (GemTypeHelper.GetRandom)
  │
  ├-> [3] pending 특수 블록 확인
  │    └-> pendingActivation == true인 블록 발동
  │         └-> ActivateSpecialAndWaitLocal (5초 타임아웃)
  │
  ├-> [4] MatchingSystem.FindMatches()
  │    ├-> [매칭 있음] -> ProcessMatchesInline -> [1]로 복귀
  │    └-> [매칭 없음] -> 루프 종료
  │
  └-> OnCascadeComplete 이벤트 발생
```

---

## 11. 주요 설정값 요약 테이블

### 11-1. 그리드 및 좌표

| 항목 | 값 | 위치 |
|------|:--:|------|
| 그리드 반경 | 5 | HexGrid.gridRadius |
| 헥사곤 크기 | 50px | HexGrid.hexSize |
| 배치 방식 | Flat-top | HexGrid (수식 기반) |
| 이웃 방향 수 | 6 | HexCoord.Directions |
| 활성 젬 종류 수 | 5 | GemTypeHelper.ActiveGemTypeCount |
| 초기 배치 최대 재시도 | 100회 | HexGrid.PopulateWithNoMatches |

### 11-2. 입력 및 클릭

| 항목 | 값 | 위치 |
|------|:--:|------|
| 일반 클릭 반경 | hexSize * 0.8 | InputSystem / HexGrid |
| 특수 블록 클릭 반경 (Bomb/XBlock/Laser) | hexSize * 0.56 | InputSystem |
| 특수 블록 클릭 반경 (Drill) | hexSize * 0.8 | InputSystem |

### 11-3. 회전

| 항목 | 값 | 위치 |
|------|:--:|------|
| 회전 지속 시간 | 0.3초 | RotationSystem.rotationDuration |
| 회전 각도 | 120도 | RotationSystem |
| 최대 시도 횟수 | 2회 (120도, 240도) | RotationSystem.TryRotate |

### 11-4. 매칭 패턴

| 매치 수 | 특수 블록 | 위치 |
|:-------:|:---------:|------|
| 3 | 없음 | MatchingSystem |
| 4 | Drill | MatchingSystem |
| 5 | Bomb | MatchingSystem |
| 6 | Laser | MatchingSystem |
| 7+ | Rainbow (Donut) | MatchingSystem |
| 6칸 링 | XBlock (=Donut, 동일 블록) | MatchingSystem.FindRingMatches |

### 11-5. 낙하 물리

| 항목 | 값 | 단위 | 위치 |
|------|:--:|:----:|------|
| 중력 가속도 | 2500 | px/s^2 | BlockRemovalSystem |
| 최대 낙하 속도 | 1500 | px/s | BlockRemovalSystem |
| 바운스 반발 계수 | 0.3 | - | BlockRemovalSystem |
| 바운스 중단 임계값 | 50 | px/s | BlockRemovalSystem |

### 11-6. 연쇄 및 안전장치

| 항목 | 값 | 위치 |
|------|:--:|------|
| 최대 연쇄 횟수 | 20회 | BlockRemovalSystem.CascadeWithPendingLoop |
| 특수 블록 발동 타임아웃 | 5초/타입 | BlockRemovalSystem.ActivateSpecialAndWaitLocal |

---

## 12. 알려진 안전장치

### 12-1. 타임아웃

| 대상 | 타임아웃 | 동작 |
|------|:--------:|------|
| 특수 블록 발동 (Drill/Bomb/XBlock/Laser/Donut) | 각 5초 | 시간 초과 시 강제 완료 처리, 다음 단계로 진행 |
| 연쇄 루프 (CascadeWithPendingLoop) | 최대 20회 | 20회 초과 시 강제 루프 종료, OnCascadeComplete 발생 |
| 초기 배치 (PopulateWithNoMatches) | 최대 100회 재시도 | 100회 초과 시 현재 배치 그대로 사용 |

### 12-2. ForceReset 메커니즘

시스템이 비정상 상태(stuck)에 빠졌을 때 복구할 수 있는 강제 리셋 기능입니다.

| 시스템 | ForceReset 대상 |
|--------|----------------|
| RotationSystem | `IsRotating` 플래그 리셋, 블록 위치 복원, 코루틴 중단 |
| BlockRemovalSystem | 모든 `Is~` 처리 플래그 리셋, VFX 중단, 코루틴 중단, 블록 matched/pending 상태 초기화 |

### 12-3. 가드 체크 (입력 레벨)

InputSystem의 가드 플래그 체계를 통해 처리 중 중복 입력을 원천 차단합니다.

| 가드 플래그 | 설정 주체 | 해제 주체 |
|------------|----------|----------|
| `IsRotating` | RotationSystem (회전 시작) | RotationSystem (회전 완료/실패) |
| `IsDrilling` | DrillBlockSystem (발동 시작) | DrillBlockSystem (발동 완료) |
| `IsBombing` | BombBlockSystem (발동 시작) | BombBlockSystem (발동 완료) |
| `IsActivating` | BlockRemovalSystem (특수 블록 발동 시작) | BlockRemovalSystem (발동 완료) |
| `IsProcessing` | BlockRemovalSystem (매칭 처리 시작) | BlockRemovalSystem (연쇄 완료) |

### 12-4. Clone 기반 데이터 안전성

RotationSystem의 `SwapData`에서 `BlockData.Clone()`을 사용하여 데이터를 교환합니다. 이는 참조 공유로 인한 데이터 오염을 방지하기 위한 안전장치입니다.

```
// 안전한 교환 (Clone 사용)
var temp = block0.data.Clone();
block0.SetBlockData(block2.data.Clone());
block1.SetBlockData(block0_original.Clone());
block2.SetBlockData(block1_original.Clone());
```

### 12-5. null 체크 및 방어적 프로그래밍

| 대상 | 방어 방식 |
|------|----------|
| HexGrid.GetBlock | Dictionary TryGetValue 기반 (키 없으면 null 반환) |
| GetNeighbors | 유효 좌표(IsValidCoord)만 필터링 |
| 특수 블록 시스템 참조 | null 체크 후 호출 (시스템 누락 시 skip) |
| 이벤트 발생 | `?.Invoke()` 패턴으로 구독자 없는 경우 안전 처리 |

---

## 13. 시스템별 의존 관계 요약

```
                    ┌─────────────┐
                    │  InputSystem │
                    └──────┬──────┘
                           │ OnRotationRequested
                           │ OnSpecialBlockActivated
                    ┌──────▼──────┐
                    │ RotationSys  │──── HexGrid (클러스터 조회)
                    └──────┬──────┘
                           │ 매칭 요청
                    ┌──────▼──────┐
                    │ MatchingSys  │──── HexGrid (블록 조회)
                    └──────┬──────┘
                           │ MatchGroup 리스트
              ┌────────────▼────────────┐
              │  BlockRemovalSystem     │
              │  (중앙 오케스트레이터)    │
              └──┬───┬───┬───┬───┬──┘
                 │   │   │   │   │
    ┌────────┐ ┌─▼─┐ ┌▼──┐ ┌▼──┐ ┌▼────┐
    │DrillSys│ │Bomb│ │X  │ │Las│ │Donut│
    │        │ │Sys │ │Blk│ │er │ │Sys  │
    └────────┘ └───┘ └───┘ └───┘ └─────┘
```

**의존 방향 원칙:**
- InputSystem → RotationSystem → MatchingSystem → BlockRemovalSystem (단방향)
- BlockRemovalSystem → 5개 특수 블록 시스템 (단방향)
- 모든 시스템 → HexGrid (공통 의존)
- 시스템 간 통신은 **이벤트(event) 기반**으로, 직접 참조를 최소화합니다.

---

## 14. 참고사항

### 14-1. 관련 문서

| 문서 | 내용 |
|------|------|
| `Dev_ScoreSystem_Spec.md` | 점수 계산 및 HUD UI 개발 명세서 |

### 14-2. 알려진 제약사항

| 항목 | 설명 | 비고 |
|------|------|------|
| HexBlock 코드 규모 | 1,002줄로 비주얼 로직과 데이터 관리가 혼재 | 향후 MVC 분리 검토 가능 |
| BlockRemovalSystem 코드 규모 | 1,770줄의 단일 파일 | 낙하, 연쇄, 특수 블록 발동을 모두 포함하여 규모가 큼 |
| 매칭 검사 범위 | FindMatches()는 전체 그리드를 순회 | 현재 gridRadius=5 수준에서는 성능 문제 없음 |
| ActiveGemTypeCount 하드코딩 | 5로 고정되어 있어 스테이지별 가변 젬 수 미지원 | 스테이지 데이터에서 주입하는 방식으로 확장 가능 |

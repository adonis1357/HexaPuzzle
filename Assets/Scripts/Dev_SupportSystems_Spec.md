# 지원 시스템 개발 명세서

| 항목 | 내용 |
|------|------|
| **문서 제목** | 지원 시스템 (Support Systems) 기술 명세서 |
| **작성일** | 2026-02-14 |
| **작성자** | 개발 PM |
| **수신** | 개발팀장 |
| **문서 버전** | v1.0 |
| **프로젝트** | JewelsHexaPuzzle (MatchMine) |

---

## 1. 개요

본 문서는 JewelsHexaPuzzle 프로젝트의 4개 지원 시스템에 대한 기술 명세서입니다. 각 시스템은 코어 게임 로직(HexGrid, MatchingSystem, BlockRemovalSystem 등)을 보조하며, 스테이지 진행/아이템/업적/오디오 기능을 담당합니다.

| 시스템 | 소스 파일 | 줄 수 | 핵심 역할 |
|--------|-----------|-------|-----------|
| **StageManager** | `Assets/Scripts/Managers/StageManager.cs` | 410줄 | 스테이지 데이터 관리, 미션 추적, 프로시저럴 스테이지 생성 |
| **ItemManager** | `Assets/Scripts/Managers/ItemManager.cs` | 520줄 | 5종 아이템 사용 흐름, 타겟 선택, 효과 실행, 수량 관리 |
| **AchievementManager** | `Assets/Scripts/Managers/AchievementManager.cs` | 383줄 | 통계 누적, 티어형 업적 해금, Pink Diamond 보상 지급 |
| **AudioManager** | `Assets/Scripts/Managers/AudioManager.cs` | 157줄 | BGM/SFX 재생, AudioSource 풀링, 볼륨 관리 |

**네임스페이스:** 모든 시스템은 `JewelsHexaPuzzle.Managers` 네임스페이스 하에 위치합니다.

---

## 2. 스테이지 & 미션 시스템 (StageManager)

### 2-1. 아키텍처

```
StageDatabase (ScriptableObject)
    └── List<StageData> stages
            ├── stageNumber
            ├── turnLimit
            ├── MissionData[] missions
            └── SpecialBlockPlacement[] specialBlocks

StageManager (MonoBehaviour)
    ├── LoadStage(stageNumber)
    │     ├── stageDatabase.GetStage() → 성공 시 사용
    │     └── GenerateDefaultStage() → 폴백 프로시저럴 생성
    ├── InitializeMissions()
    └── 이벤트 핸들러 (OnGemCollected, OnGemProcessed, ...)
```

- `StageDatabase`는 `ScriptableObject`로 구현되어 있으며, Unity 에디터의 `Create > JewelsHexaPuzzle > Stage Database` 메뉴에서 생성합니다.
- `stageDatabase` 참조가 null이거나 해당 스테이지 번호가 범위를 벗어나면 `GenerateDefaultStage()`가 자동 호출됩니다.

### 2-2. 데이터 구조

#### StageData

| 필드 | 타입 | 설명 |
|------|------|------|
| `stageNumber` | `int` | 스테이지 번호 |
| `turnLimit` | `int` | 제한 턴 수 |
| `missions` | `MissionData[]` | 미션 배열 |
| `specialBlocks` | `SpecialBlockPlacement[]` | 초기 특수 블록 강제 배치 좌표 |

#### MissionData

| 필드 | 타입 | 설명 |
|------|------|------|
| `type` | `MissionType` | 미션 유형 |
| `targetGemType` | `GemType` | 대상 보석 타입 (`GemType.None`이면 모든 타입 허용) |
| `secondaryGemType` | `GemType` | 보조 보석 타입 (복수 원석 채광 등에 사용) |
| `targetCount` | `int` | 목표 수량 |
| `currentCount` | `int` | 현재 진행 수량 |
| `icon` | `Sprite` | UI 표시용 아이콘 |

#### MissionProgress

| 필드 | 타입 | 설명 |
|------|------|------|
| `mission` | `MissionData` | 연결된 미션 데이터 |
| `currentCount` | `int` | 현재 달성 수량 |
| `isComplete` | `bool` | 완료 여부 |

#### SpecialBlockPlacement

| 필드 | 타입 | 설명 |
|------|------|------|
| `q` | `int` | 헥스 그리드 q좌표 |
| `r` | `int` | 헥스 그리드 r좌표 |
| `type` | `SpecialBlockType` | 배치할 특수 블록 타입 |
| `parameter` | `int` | 부가 파라미터 (시한폭탄 카운트, 비닐 레이어 수 등) |

### 2-3. MissionType 열거형

| 값 | 이름 | 설명 |
|----|------|------|
| 1 | `CollectGem` | 특정 원석 N개 채광 |
| 2 | `CollectMultiGem` | 복수 원석 채광 |
| 3 | `ProcessGem` | 보석 가공 N개 처리 |
| 4 | `CreateSpecialGem` | 특수 보석 N개 생성 |
| 5 | `CreatePerfectGem` | 완전 보석 생성 |
| 6 | `TriggerBigBang` | BigBang N회 발동 |
| 7 | `RemoveVinyl` | 비닐 N개 제거 |
| 8 | `RemoveDoubleVinyl` | 2중 비닐 N개 제거 |
| 9 | `MoveItem` | 물건 옮기기 |
| 10 | `ReachScore` | 목표 점수 달성 |

> **참고:** 실제 코드에서는 `CollectMultiGem(2)`, `CreatePerfectGem(5)`, `RemoveDoubleVinyl(8)`, `MoveItem(9)` 타입이 열거형에 정의되어 있으나, 이벤트 핸들러에서 별도 분기 처리가 구현되어 있지 않습니다. `OnVinylRemoved(bool isDouble)` 메서드에서 `RemoveDoubleVinyl`에 대한 분기만 존재합니다.

### 2-4. 이벤트 핸들러

StageManager는 GameManager 및 코어 시스템에서 호출되는 이벤트 핸들러를 제공합니다.

| 메서드 | 시그니처 | 호출 시점 | 처리 로직 |
|--------|----------|-----------|-----------|
| `OnGemCollected` | `(GemType gemType, int count)` | 블록 제거 시 | `CollectGem` 미션 중 `targetGemType`이 `None`이거나 일치하면 `count`만큼 누적 |
| `OnGemProcessed` | `(GemType centerType, GemType borderType)` | 보석 가공 시 | `ProcessGem` 미션 중 `targetGemType`이 `None`이거나 `centerType`과 일치하면 +1 |
| `OnSpecialGemCreated` | `()` | 특수 보석 생성 시 | `CreateSpecialGem` 미션에 +1 |
| `OnVinylRemoved` | `(bool isDouble)` | 비닐 제거 시 | `RemoveVinyl` 또는 `RemoveDoubleVinyl`(`isDouble=true` 시) 미션에 +1 |
| `OnBigBangTriggered` | `()` | BigBang 발동 시 | `TriggerBigBang` 미션에 +1 |
| `OnScoreAdded` | `(int totalScore)` | 점수 추가 시 | `ReachScore` 미션의 `currentCount`를 `totalScore`로 **덮어쓰기** (누적이 아님) |

> **주의사항:** `OnScoreAdded`는 다른 핸들러와 달리 누적(`+=`)이 아닌 대입(`=`)으로 동작합니다. 이는 점수가 항상 누적 합계로 전달되기 때문입니다.

### 2-5. 이벤트 (외부 알림)

| 이벤트 | 시그니처 | 발생 시점 |
|--------|----------|-----------|
| `OnMissionProgressUpdated` | `Action<MissionProgress[]>` | 모든 이벤트 핸들러 처리 후, `CheckMissionProgress()` 호출 시 |
| `OnMissionComplete` | `Action<int>` | 개별 미션 달성 시 (파라미터: 미션 인덱스) |

### 2-6. 스테이지 진행 판정

```csharp
public bool IsMissionComplete()
{
    foreach (var progress in missionProgress)
    {
        if (!progress.isComplete) return false;
    }
    return missionProgress.Count > 0;  // 미션이 1개 이상이어야 true
}
```

- 모든 미션의 `isComplete`가 `true`이고, 미션이 1개 이상 존재해야 스테이지 클리어로 판정됩니다.
- GameManager의 `OnCascadeComplete()` 및 `ProcessSpecialBlockAftermath()`에서 이 메서드를 호출하여 클리어 여부를 확인합니다.

### 2-7. 프로시저럴 스테이지 생성 (GenerateDefaultStage)

`stageDatabase`가 없거나 해당 스테이지가 없을 때 자동으로 호출됩니다.

#### 턴 제한 계산

```
turnLimit = 30 + (stageNumber / 10) * 5
```

| 스테이지 구간 | turnLimit |
|---------------|-----------|
| 1~9 | 30 |
| 10~19 | 35 |
| 20~29 | 40 |
| 30~39 | 45 |
| ... | ... |

#### 미션 수 계산

```
missionCount = min(1 + stageNumber / 20, 3)
```

| 스테이지 구간 | 미션 수 |
|---------------|---------|
| 1~19 | 1 |
| 20~39 | 2 |
| 40+ | 3 (최대) |

#### 난이도 스케일링

| 구간 | 가용 미션 타입 | 목표 수량 기준 |
|------|----------------|----------------|
| **초반** (1~9) | `CollectGem`만 | `10 + stageNumber * 2` |
| **중반** (10~29) | `CollectGem`, `ProcessGem`, `ReachScore` | `CollectGem`: `15 + stageNumber`, `ProcessGem`: `3 + stageNumber/10`, `ReachScore`: `1000 * stageNumber` |
| **후반** (30+) | `CollectGem`, `ProcessGem`, `CreateSpecialGem`, `RemoveVinyl`, `TriggerBigBang` | 각 타입별 개별 공식 적용 |

---

## 3. 아이템 시스템 (ItemManager)

### 3-1. 아키텍처

```
ItemManager (MonoBehaviour)
    ├── [SerializeField] HexGrid hexGrid
    ├── [SerializeField] UIManager uiManager
    ├── [SerializeField] BlockRemovalSystem blockRemovalSystem
    ├── [SerializeField] InputSystem inputSystem
    ├── Dictionary<ItemType, int> itemCounts (보유 수량)
    └── ItemData[] items (아이템 설정 데이터)
```

- 모든 참조 필드는 Inspector에서 할당하며, `blockRemovalSystem`은 `Start()`에서 `FindObjectOfType<>()`으로 자동 탐색 폴백이 있습니다.

### 3-2. ItemType 열거형

| 값 | 이름 | UI 이름 | 설명 | 해금 스테이지 | 기본 가격($) |
|----|------|---------|------|---------------|-------------|
| 1 | `Hammer` | HAMMER | 단일 블록/비닐/체인 제거 | 10 | 0.99 |
| 2 | `Bomb` | BOMB | 선택 블록 + 인접 6칸 제거 | 30 | 1.29 |
| 3 | `SixWayLaser` | 6-WAY LASER | 6방향 끝까지 제거 | 50 | 1.69 |
| 4 | `SSD` | SSD | BFS 동색 연결 블록 전부 제거 | 100 | 1.99 |
| 5 | `TurnPlus5` | (게임오버 시) | 남은 턴 +5 | - | - |

### 3-3. 사용 흐름

```
[사용자 아이템 버튼 터치]
        │
        ▼
   UseItem(ItemType)
        │
        ├── 수량 확인 (itemCounts >= 1)
        ├── 해금 확인 (CurrentStage >= unlockStage)
        ├── 입력 시스템 비활성화 (inputSystem.SetEnabled(false))
        ├── 타겟 선택 인디케이터 표시
        └── OnItemActivated 이벤트 발생
                │
                ▼
        [사용자 블록 터치]
                │
                ▼
        SelectTarget(screenPosition)
                │
                ├── 화면 좌표 → HexCoord 변환
                ├── HexGrid에서 블록 조회
                └── ExecuteItem 코루틴 시작
                        │
                        ├── 아이템 효과 실행
                        ├── 수량 차감 + 저장
                        ├── UI 업데이트
                        ├── blockRemovalSystem.TriggerFallOnly() (낙하 + 연쇄 처리)
                        ├── 입력 시스템 재활성화
                        └── OnItemUsed 이벤트 발생
```

### 3-4. 각 아이템 상세 동작

#### Hammer (ExecuteHammer)

1. 대상 블록의 `specialType`이 `FixedBlock`이면 **사용 불가** (로그 출력 후 종료)
2. 대상 블록에 `vinylLayer > 0`이면 비닐 레이어만 1단계 제거 → **종료**
3. 대상 블록에 `hasChain == true`이면 체인만 제거 → **종료**
4. 위 조건이 모두 아니면 블록 자체를 제거 (`SetBlockData(new BlockData())`)
5. **턴 소비 없음**

> **우선순위:** 비닐 > 체인 > 블록 본체. 한 번 사용 시 최우선 레이어 하나만 처리됩니다.

#### Bomb (ExecuteBomb)

1. 대상 블록 자신 + `hexGrid.GetNeighbors(target.Coord)` = 최대 7칸
2. 각 블록에 대해 `specialType != FixedBlock` 조건 확인 후 제거
3. **턴 소비 없음**

> BombBlockSystem의 `ActivateBomb`과 유사한 범위이나, ItemManager에서 **독립적으로** 실행됩니다.

#### SixWayLaser (ExecuteSixWayLaser)

1. 대상 블록 자신을 목록에 추가
2. 6방향(`dir = 0~5`) 각각에 대해 `HexCoord.GetNeighbor(dir)`를 반복 호출하여 보드 끝(`null`)까지 탐색
3. 탐색된 모든 블록 중 `FixedBlock`을 제외하고 제거
4. `laserLineRenderer`가 있으면 페이드 아웃 레이저 이펙트 표시 (0.5초)
5. **턴 소비 없음**

#### SSD - Same-color Sweep Destroy (ExecuteSSD)

1. 시작 블록의 `gemType`을 `targetType`으로 저장
2. **BFS** 탐색:
   - 인접 블록의 `gemType == targetType` 이거나 `tier >= BlockTier.ProcessedGem`이면 연결
   - 탐색 중 각 블록에 하이라이트 효과 (`SetHighlighted(true)`) + 0.05초 딜레이
3. 탐색 완료 후 0.3초 대기
4. 연결된 모든 블록 일괄 제거
5. **턴 소비 없음**
6. **코루틴 기반** (다른 아이템은 동기 실행)

> **참고:** BFS 연결 조건에 `tier >= ProcessedGem`이 포함되어, 가공된 보석은 색상과 무관하게 연결됩니다.

#### TurnPlus5

- `GameManager.AddTurns(5)` 호출로 구현 (ItemManager 내부에 직접 구현 없음)
- **타겟 선택 불필요** (즉시 사용)
- 게임오버 상태에서도 사용 가능 → `GameState.Playing`으로 복귀

### 3-5. 이벤트

| 이벤트 | 시그니처 | 발생 시점 |
|--------|----------|-----------|
| `OnItemUsed` | `Action<ItemType>` | 아이템 효과 실행 완료 후 |
| `OnItemActivated` | `Action<ItemType>` | 타겟 선택 모드 진입 시 |
| `OnItemCancelled` | `Action` | 아이템 사용 취소 시 |

### 3-6. 아이템 수량 관리

| 메서드 | 설명 |
|--------|------|
| `AddItem(ItemType, int amount)` | 수량 추가 (구매/보상) |
| `GetItemCount(ItemType)` | 보유 수량 조회 |
| `LoadItemCounts()` | PlayerPrefs에서 로드 (Start 시) |
| `SaveItemCounts()` | PlayerPrefs에 저장 (사용/추가 시) |

### 3-7. 낙하 후처리

아이템 사용 후 `blockRemovalSystem.TriggerFallOnly()`를 호출하여 빈 공간을 채우고, 연쇄 매칭이 발생할 수 있도록 합니다. 이 처리가 완료될 때까지(`IsProcessing == false`) 코루틴에서 대기합니다.

---

## 4. 업적 시스템 (AchievementManager)

### 4-1. 아키텍처

```
AchievementManager (MonoBehaviour, Singleton, DontDestroyOnLoad)
    ├── Dictionary<int, AchievementData> achievements  (정의)
    ├── Dictionary<int, AchievementProgress> progress   (진행도)
    └── Dictionary<StatType, long> stats                (통계)
```

- Singleton 패턴으로 구현되어 씬 전환 시에도 유지됩니다.
- `Awake()`에서 업적 정의 초기화 + PlayerPrefs 로드를 수행합니다.

### 4-2. StatType 열거형 (전체 목록)

| 카테고리 | StatType | 설명 |
|----------|----------|------|
| **원석 채광** | `EmeraldsCollected` | 에메랄드 채광 수 |
| | `RubiesCollected` | 루비 채광 수 |
| | `SapphiresCollected` | 사파이어 채광 수 |
| | `AmbersCollected` | 호박 채광 수 |
| | `AmethystsCollected` | 자수정 채광 수 |
| | `TotalGemsCollected` | 전체 원석 채광 수 |
| **보석 가공/획득** | `GemsProcessed` | 보석 가공 수 |
| | `GemsObtained` | 보석 획득 수 |
| | `SpecialGemsProcessed` | 특수 보석 가공 수 |
| | `SpecialGemsObtained` | 특수 보석 획득 수 |
| | `PerfectGemsProcessed` | 완전 보석 가공 수 |
| | `PerfectGemsObtained` | 완전 보석 획득 수 |
| **특수** | `BigBangs` | BigBang 발동 횟수 |
| | `VinylsRemoved` | 비닐 제거 횟수 |
| | `DoubleVinylsRemoved` | 2중 비닐 제거 횟수 |
| | `MoveBlocksRemoved` | 이동 블록 제거 횟수 |
| | `FixedBlocksRemoved` | 고정 블록 제거 횟수 |
| | `TimeBombsDefused` | 시한폭탄 해제 횟수 |
| | `ChainsRemoved` | 체인 제거 횟수 |
| **물건** | `ItemACollected` ~ `ItemECollected` | 물건 A~E 수집 횟수 |
| **기타** | `TotalTurns` | 총 턴 사용 횟수 |
| | `TotalGold` | 총 골드 획득량 |
| | `StagesCleared` | 스테이지 클리어 횟수 |

### 4-3. 업적 데이터 구조

#### AchievementData

| 필드 | 타입 | 설명 |
|------|------|------|
| `id` | `int` | 업적 고유 ID |
| `title` | `string` | 업적 제목 |
| `description` | `string` | 업적 설명 |
| `statType` | `StatType` | 연결된 통계 타입 |
| `targetValue` | `long` | 달성 목표값 |
| `pinkDiamondReward` | `int` | Pink Diamond 보상량 |
| `icon` | `Sprite` | UI 아이콘 |

#### AchievementProgress

| 필드 | 타입 | 설명 |
|------|------|------|
| `achievementId` | `int` | 업적 ID |
| `isCompleted` | `bool` | 달성 여부 |
| `isRewardClaimed` | `bool` | 보상 수령 여부 |

### 4-4. 업적 카테고리 및 ID 배정

#### 원석 채광 업적 (ID: 101~187)

각 보석 타입별 7단계 티어 구성입니다.

| 보석 | 기본 ID | StatType |
|------|---------|----------|
| 에메랄드 | 101~107 | `EmeraldsCollected` |
| 루비 | 121~127 | `RubiesCollected` |
| 사파이어 | 141~147 | `SapphiresCollected` |
| 호박 | 161~167 | `AmbersCollected` |
| 자수정 | 181~187 | `AmethystsCollected` |

**티어 공통 구조:**

| 티어 | 목표 | 보상 (Pink Diamond) |
|------|------|---------------------|
| 1 | 최초 1개 | 1 |
| 2 | 100개 | 2 |
| 3 | 1,000개 | 3 |
| 4 | 10,000개 | 5 |
| 5 | 100,000개 | 10 |
| 6 | 1,000,000개 | 50 |
| 7 | 10,000,000개 | 100 |

#### 보석 가공 업적 (ID: 301~305)

| ID | 제목 | 목표 | 보상 |
|----|------|------|------|
| 301 | 보석 최초 가공 | 1 | 10 |
| 302 | 보석 100개 가공 | 100 | 20 |
| 303 | 보석 1000개 가공 | 1,000 | 30 |
| 304 | 보석 10000개 가공 | 10,000 | 50 |
| 305 | 보석 100000개 가공 | 100,000 | 100 |

#### 보석 획득 업적 (ID: 501~507)

| ID | 제목 | 목표 | 보상 |
|----|------|------|------|
| 501 | 보석 최초 획득 | 1 | 10 |
| 502 | 보석 10개 획득 | 10 | 20 |
| 503 | 보석 100개 획득 | 100 | 30 |
| 504 | 보석 1000개 획득 | 1,000 | 50 |
| 505 | 보석 1만개 획득 | 10,000 | 70 |
| 506 | 보석 10만개 획득 | 100,000 | 100 |
| 507 | 보석 100만개 획득 | 1,000,000 | 200 |

#### 특수 보석 업적 (ID: 511~541)

| ID | 제목 | StatType | 목표 | 보상 |
|----|------|----------|------|------|
| 511 | 특수보석 최초 가공 | `SpecialGemsProcessed` | 1 | 50 |
| 512 | 특수보석 10개 가공 | `SpecialGemsProcessed` | 10 | 70 |
| 513 | 특수보석 100개 가공 | `SpecialGemsProcessed` | 100 | 100 |
| 521 | 특수보석 최초 획득 | `SpecialGemsObtained` | 1 | 50 |
| 531 | 완전보석 최초 가공 | `PerfectGemsProcessed` | 1 | 100 |
| 541 | 완전보석 최초 획득 | `PerfectGemsObtained` | 1 | 100 |

#### BigBang 업적 (ID: 551~556)

| ID | 제목 | 목표 | 보상 |
|----|------|------|------|
| 551 | 빅뱅 최초 발생 | 1 | 1,000 |
| 552 | 빅뱅 10회 발생 | 10 | 2,000 |
| 553 | 빅뱅 30회 발생 | 30 | 3,000 |
| 554 | 빅뱅 50회 발생 | 50 | 5,000 |
| 555 | 빅뱅 70회 발생 | 70 | 7,000 |
| 556 | 빅뱅 100회 발생 | 100 | 10,000 |

#### 기타 업적 (ID: 561~695)

| ID | 제목 | StatType | 목표 | 보상 |
|----|------|----------|------|------|
| 561 | 첫 비닐 벗기기 | `VinylsRemoved` | 1 | 1 |
| 571 | 첫 2중 비닐 벗기기 | `DoubleVinylsRemoved` | 1 | 1 |
| 671~675 | 턴 사용 (1/100/1000/1만/10만) | `TotalTurns` | 1~100,000 | 1~20 |
| 691~695 | 골드 획득 (1/1만/10만/100만/1000만) | `TotalGold` | 1~10,000,000 | 1~100 |

### 4-5. 처리 흐름

```
[게임 이벤트 발생]
        │
        ▼
IncrementStat(StatType, amount)
        │
        ├── stats[type] += amount
        │
        ▼
CheckAchievements(StatType)
        │
        ├── 해당 StatType의 모든 업적을 순회
        ├── currentValue >= targetValue 이면
        │        │
        │        ▼
        │   UnlockAchievement(id)
        │        │
        │        ├── isCompleted = true
        │        ├── OnAchievementUnlocked 이벤트 발생
        │        └── ClaimReward(id) 자동 호출
        │                 │
        │                 ├── isRewardClaimed = true
        │                 └── OnPinkDiamondEarned(reward) 이벤트 발생
        │
        └── SaveProgress()
```

> **자동 보상:** `UnlockAchievement()`가 호출되면 `ClaimReward()`가 즉시 자동 호출됩니다. 별도의 "보상 수령" UI 단계가 없습니다.

### 4-6. 이벤트

| 이벤트 | 시그니처 | 발생 시점 |
|--------|----------|-----------|
| `OnAchievementUnlocked` | `Action<AchievementData>` | 업적 해금 시 |
| `OnPinkDiamondEarned` | `Action<int>` | Pink Diamond 보상 지급 시 (파라미터: 보상량) |

### 4-7. 공개 API

| 메서드 | 반환 타입 | 설명 |
|--------|-----------|------|
| `IncrementStat(StatType, long)` | `void` | 통계 누적 (기본값 amount=1) |
| `GetStat(StatType)` | `long` | 현재 통계값 조회 |
| `GetAllAchievements()` | `List<AchievementData>` | 전체 업적 목록 |
| `GetProgress(int id)` | `AchievementProgress` | 특정 업적 진행도 조회 |
| `ClaimReward(int id)` | `void` | 보상 수령 (이미 자동 호출됨) |

---

## 5. 오디오 시스템 (AudioManager)

### 5-1. 아키텍처

```
AudioManager (MonoBehaviour, Singleton, DontDestroyOnLoad)
    ├── AudioSource bgmSource (BGM 전용, 루프)
    ├── AudioSource sfxSource (미사용 - 풀 기반으로 대체)
    └── List<AudioSource> sfxPool (SFX 풀, 초기 10개)
```

- Singleton 패턴이며, `DontDestroyOnLoad`로 씬 전환 시에도 유지됩니다.
- `Awake()`에서 부모가 있으면 분리(`SetParent(null)`) 후 DontDestroyOnLoad을 적용합니다.

### 5-2. AudioClip 필드 (Inspector 할당)

| 카테고리 | 필드명 | 설명 |
|----------|--------|------|
| **BGM** | `mainBGM` | 메인/로비 BGM |
| | `gameBGM` | 인게임 BGM |
| **SFX** | `rotateSound` | 블록 회전 효과음 |
| | `matchSound` | 3매치 효과음 |
| | `match4Sound` | 4매치 효과음 |
| | `match5Sound` | 5매치 이상 효과음 |
| | `processSound` | 보석 가공 효과음 |
| | `specialGemSound` | 특수 보석 생성/발동 효과음 |
| | `perfectGemSound` | 완전 보석 효과음 |
| | `bigBangSound` | BigBang 효과음 |
| | `comboSound` | 콤보 효과음 |
| | `failSound` | 실패 효과음 |
| | `buttonClick` | 버튼 클릭 효과음 |
| | `popupOpen` | 팝업 열림 효과음 |
| | `stageClear` | 스테이지 클리어 효과음 |
| | `gameOver` | 게임오버 효과음 |

### 5-3. 공개 API

| 메서드 | 설명 | 비고 |
|--------|------|------|
| `PlayBGM(AudioClip, bool loop)` | BGM 재생 | 기본 loop=true |
| `PlayMainBGM()` | 메인 BGM 재생 | `PlayBGM(mainBGM)` 단축 호출 |
| `PlayGameBGM()` | 인게임 BGM 재생 | `PlayBGM(gameBGM)` 단축 호출 |
| `StopBGM()` | BGM 정지 | |
| `PlaySFX(AudioClip, float)` | SFX 재생 | volumeMultiplier 기본값 1.0 |
| `PlayRotateSound()` | 회전 효과음 | |
| `PlayMatchSound(int count)` | 매치 효과음 | count에 따라 클립 분기 (아래 참조) |
| `PlayProcessSound()` | 보석 가공 효과음 | |
| `PlaySpecialGemSound()` | 특수 보석 효과음 | |
| `PlayPerfectGemSound()` | 완전 보석 효과음 | |
| `PlayBigBangSound()` | BigBang 효과음 | |
| `PlayComboSound()` | 콤보 효과음 | |
| `PlayFailSound()` | 실패 효과음 | |
| `PlayButtonClick()` | 버튼 클릭 효과음 | volumeMultiplier = 0.8 |
| `PlayPopupOpen()` | 팝업 열림 효과음 | |
| `PlayStageClear()` | 스테이지 클리어 효과음 | |
| `PlayGameOver()` | 게임오버 효과음 | |
| `SetBGMVolume(float)` | BGM 볼륨 설정 (0~1) | PlayerPrefs 자동 저장 |
| `SetSFXVolume(float)` | SFX 볼륨 설정 (0~1) | PlayerPrefs 자동 저장 |
| `MuteAll(bool)` | 전체 음소거 토글 | BGM + 모든 SFX 풀 |

### 5-4. PlayMatchSound 분기 로직

```csharp
public void PlayMatchSound(int count = 3)
{
    if (count >= 5) PlaySFX(match5Sound);      // 5매치 이상
    else if (count >= 4) PlaySFX(match4Sound);  // 4매치
    else PlaySFX(matchSound);                   // 3매치 (기본)
}
```

| count 값 | 재생 클립 |
|-----------|-----------|
| 3 이하 | `matchSound` |
| 4 | `match4Sound` |
| 5 이상 | `match5Sound` |

### 5-5. SFX 풀 메커니즘

1. `InitializeAudioPool()`에서 10개의 AudioSource를 자식 GameObject로 생성합니다.
2. `GetAvailableSource()`에서 `isPlaying == false`인 AudioSource를 반환합니다.
3. 모든 풀이 사용 중이면 새 AudioSource를 **동적으로 생성**하여 풀에 추가합니다.
4. AudioSource는 재생 완료 시 자동으로 `isPlaying = false`가 되어 풀에 반환됩니다 (Unity AudioSource 기본 동작).

> **참고:** 풀 크기에 상한이 없으므로, 극단적인 상황에서는 AudioSource가 무한히 증가할 수 있습니다. 실무에서는 상한을 설정하는 것을 권장합니다.

### 5-6. 볼륨 설정

| 항목 | 기본값 | PlayerPrefs 키 |
|------|--------|----------------|
| BGM 볼륨 | 0.7 | `BGMVolume` |
| SFX 볼륨 | 1.0 | `SFXVolume` |

- `SetBGMVolume()` / `SetSFXVolume()`은 `Mathf.Clamp01()`로 0~1 범위를 보장합니다.
- BGM 볼륨 변경 시 즉시 `bgmSource.volume`에 반영됩니다.
- SFX 볼륨은 다음 `PlaySFX()` 호출 시 적용됩니다 (이미 재생 중인 SFX에는 미반영).

---

## 6. 시스템 간 연동 관계

### 6-1. GameManager 중심 연동도

```
                        ┌─────────────────────────────────────────┐
                        │             GameManager                 │
                        │  (중앙 조율자, Singleton)                │
                        └───┬───────┬───────┬───────┬─────────────┘
                            │       │       │       │
               ┌────────────┘   ┌───┘   ┌───┘   ┌───┘
               ▼                ▼       ▼       ▼
        ┌─────────────┐ ┌──────────┐ ┌──────┐ ┌──────────────┐
        │StageManager │ │ItemManager│ │Audio │ │Achievement   │
        │             │ │          │ │Manager│ │Manager       │
        └──────┬──────┘ └────┬─────┘ └──┬───┘ └──────┬───────┘
               │             │          │             │
               ▼             ▼          ▼             ▼
        미션 진행 추적   아이템 효과   사운드 재생   통계 누적
        스테이지 판정   타겟 선택     BGM/SFX      업적 해금
```

### 6-2. 이벤트 연결 상세

| 발신 시스템 | 이벤트/호출 | 수신 시스템 | 처리 |
|-------------|------------|------------|------|
| BlockRemovalSystem | `OnBlocksRemoved` | GameManager | `ScoreManager.AddMatchScore()` + `StageManager.CheckMissionProgress()` |
| BlockRemovalSystem | `OnCascadeComplete` | GameManager | `StageManager.IsMissionComplete()` 확인 → 클리어/게임오버 판정 |
| BlockRemovalSystem | `OnBigBang` | GameManager | `bigBangTriggered = true` |
| 특수 블록 시스템들 | `On{Type}Complete` | GameManager | `ScoreManager.AddSpecialBlockScore()` + 후처리 코루틴 |
| GameManager | 직접 호출 | StageManager | `OnGemCollected`, `OnGemProcessed`, `OnSpecialGemCreated`, `OnBigBangTriggered`, `OnVinylRemoved`, `OnScoreAdded` |
| GameManager | 직접 호출 | AudioManager | 각 상황별 `Play*Sound()` 호출 |
| AchievementManager | `OnPinkDiamondEarned` | (UI 등) | Pink Diamond 보상 표시/지급 |
| ItemManager | `OnItemUsed` | (UI 등) | 아이템 사용 완료 표시 |

### 6-3. ItemManager와 코어 시스템 연동

```
ItemManager
    ├── HexGrid: 좌표 변환, 블록 조회, 이웃 블록 조회
    ├── InputSystem: 아이템 사용 시 입력 비활성화/재활성화
    ├── BlockRemovalSystem: 아이템 효과 후 TriggerFallOnly()로 낙하+연쇄
    └── UIManager: 아이템 버튼 UI 업데이트
```

### 6-4. AchievementManager 통계 입력 채널

AchievementManager의 `IncrementStat()`은 다양한 시스템에서 호출되어야 합니다.

| 호출 시점 | 대상 StatType | 호출 위치 (예상) |
|-----------|---------------|-----------------|
| 블록 매치 제거 시 | `{Gem}Collected`, `TotalGemsCollected` | BlockRemovalSystem / GameManager |
| 보석 가공 시 | `GemsProcessed`, `GemsObtained` | GameManager |
| 특수 보석 가공/획득 시 | `SpecialGemsProcessed`, `SpecialGemsObtained` | GameManager |
| BigBang 발동 시 | `BigBangs` | GameManager |
| 비닐 제거 시 | `VinylsRemoved`, `DoubleVinylsRemoved` | BlockRemovalSystem |
| 턴 사용 시 | `TotalTurns` | GameManager.UseTurn() |
| 골드 획득 시 | `TotalGold` | ScoreManager / 보상 시스템 |
| 스테이지 클리어 시 | `StagesCleared` | GameManager.StageClear() |

---

## 7. 저장 데이터 키 요약 테이블 (PlayerPrefs)

### 7-1. ItemManager

| 키 패턴 | 타입 | 설명 | 예시 |
|---------|------|------|------|
| `Item_{ItemType}` | `int` | 아이템 보유 수량 | `Item_Hammer`, `Item_Bomb`, `Item_SixWayLaser`, `Item_SSD`, `Item_TurnPlus5` |

### 7-2. AchievementManager

| 키 패턴 | 타입 | 설명 | 예시 |
|---------|------|------|------|
| `Stat_{StatType}` | `string` (long 파싱) | 통계값 | `Stat_EmeraldsCollected`, `Stat_BigBangs`, `Stat_TotalGold` |
| `Achievement_{id}` | `int` (0 또는 1) | 업적 완료 여부 | `Achievement_101`, `Achievement_551` |

> **참고:** 통계값은 `long` 범위를 지원하기 위해 `PlayerPrefs.SetString()`/`GetString()`으로 문자열 형태로 저장됩니다.

### 7-3. AudioManager

| 키 | 타입 | 기본값 | 설명 |
|----|------|--------|------|
| `BGMVolume` | `float` | 0.7 | BGM 볼륨 |
| `SFXVolume` | `float` | 1.0 | SFX 볼륨 |

### 7-4. 전체 키 수 추정

| 시스템 | 키 수 | 비고 |
|--------|-------|------|
| ItemManager | 5개 | ItemType 열거형 5개 |
| AchievementManager (Stats) | 23개 | StatType 열거형 전체 |
| AchievementManager (Achievements) | ~80개 | 업적 ID 수 |
| AudioManager | 2개 | BGMVolume, SFXVolume |
| **합계** | **~110개** | |

---

## 8. 향후 확장 가능 영역

### 8-1. StageManager

| 영역 | 설명 | 우선순위 |
|------|------|----------|
| StageDatabase 에디터 도구 | 스테이지 데이터를 시각적으로 편집하는 Custom Editor 개발 | 높음 |
| MissionType 확장 | `CollectMultiGem`, `CreatePerfectGem`, `MoveItem` 등 정의만 되어 있고 핸들러가 없는 미션 타입의 로직 구현 | 높음 |
| `RemoveDoubleVinyl` 분리 | 현재 `OnVinylRemoved(bool isDouble)`로 분기 처리 중이나, 별도 핸들러 분리 검토 | 보통 |
| 스테이지 이벤트 시스템 | 보스 스테이지, 시간 제한 스테이지 등 특수 스테이지 모드 | 낮음 |
| 서버 기반 스테이지 데이터 | ScriptableObject 대신 서버에서 스테이지 설정을 다운로드하는 구조 | 낮음 |

### 8-2. ItemManager

| 영역 | 설명 | 우선순위 |
|------|------|----------|
| TurnPlus5 자체 구현 | 현재 GameManager.AddTurns()에 의존하므로, ItemManager 내부에서 독립적으로 처리하도록 통합 | 높음 |
| 이펙트/VFX 구현 | 해머, 폭탄, 레이저 이펙트 구현 (현재 TODO 주석 상태) | 높음 |
| FixedBlock 처리 | 현재 Hammer에서만 FixedBlock 체크가 있으나, Bomb/Laser에서도 FixedBlock skip 로직 존재 - 일관성 검증 필요 | 보통 |
| 아이템 스토어 UI | 구매 흐름과 가격(`ItemData.price`) 연동 | 보통 |
| 신규 아이템 추가 | ItemType 열거형에 값 추가 + Execute 메서드 구현으로 확장 가능한 구조 | 낮음 |

### 8-3. AchievementManager

| 영역 | 설명 | 우선순위 |
|------|------|----------|
| IncrementStat 호출 누락 | 현재 GameManager/BlockRemovalSystem에서 `IncrementStat()` 호출 코드가 실제로 구현되어 있는지 확인 필요 | 높음 |
| 업적 UI | 업적 목록 표시, 진행도 바, 달성 알림 팝업 | 높음 |
| 보상 수령 분리 | 현재 자동 지급이지만, UI에서 수동 수령 방식으로 전환 검토 | 보통 |
| 일일/주간 업적 | 리셋 주기가 있는 업적 타입 추가 | 낮음 |
| 서버 동기화 | PlayerPrefs 대신 서버 기반 업적 저장 (치트 방지) | 낮음 |

### 8-4. AudioManager

| 영역 | 설명 | 우선순위 |
|------|------|----------|
| SFX 풀 상한 설정 | 현재 무제한 확장 → 최대 20~30개로 제한 + 경고 로그 | 높음 |
| BGM 크로스페이드 | 씬 전환 시 BGM 페이드 인/아웃 | 보통 |
| 이미 재생 중인 SFX 볼륨 반영 | `SetSFXVolume()` 호출 시 현재 재생 중인 SFX에도 볼륨 즉시 적용 | 보통 |
| AudioMixer 도입 | 직접 볼륨 제어 대신 Unity AudioMixer 그룹 활용 | 낮음 |
| 진동(Haptic) 연동 | 매치/폭발 시 모바일 진동 피드백 | 낮음 |

### 8-5. 공통 개선 사항

| 영역 | 설명 |
|------|------|
| **PlayerPrefs → 파일 기반 저장** | 현재 모든 시스템이 PlayerPrefs를 사용하지만, 데이터 양이 많아지면 JSON/Binary 파일 기반 저장으로 전환하는 것을 권장합니다 |
| **데이터 암호화** | PlayerPrefs는 평문 저장이므로, 아이템 수량/업적 등 민감 데이터의 암호화가 필요합니다 |
| **이벤트 버스 도입** | 현재 직접 참조/호출 방식에서 중앙 이벤트 버스 패턴으로 전환하면 시스템 간 결합도를 낮출 수 있습니다 |
| **단위 테스트** | 미션 진행도 계산, 업적 달성 조건, 아이템 효과 범위 등에 대한 단위 테스트 작성을 권장합니다 |

---

*본 문서는 실제 소스 코드(`StageManager.cs` 410줄, `ItemManager.cs` 520줄, `AchievementManager.cs` 383줄, `AudioManager.cs` 157줄)를 기반으로 작성되었습니다. 구현 시 본 명세와 소스 코드 간 차이가 발견되면 개발 PM에게 즉시 공유해 주시기 바랍니다.*

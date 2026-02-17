# 개발팀 구현 가이드: 미션 1 색상도둑 소탕

**작성일**: 2026-02-17
**대상**: 개발팀
**문서 버전**: v1.0
**상태**: 준비 완료 (데이터 클래스 제공됨)

---

## 📋 개요

미션 1 (Stage 1-10)의 구현을 위해 다음 2개의 클래스가 준비되었습니다:

### 준비된 파일
1. **Mission1StageData.cs** - 모든 Stage 1-10의 데이터 정의
2. **Mission1StageLoader.cs** - 스테이지 로드 및 배치 관리자

### 통합 흐름
```
GameManager 시작
    ↓
Mission1StageLoader.LoadStage(1~10, hexGrid, stageManager)
    ↓
고정 블록 배치 (FixedBlock 배치)
    ↓
적군 배치 (Chromophage 적용)
    ↓
StageManager에 미션 데이터 전달
    ↓
게임 시작 (튜토리얼 팝업 표시)
```

---

## 🔧 개발팀이 구현해야 할 항목

### Phase 1: 기본 통합 (높은 우선순위)

#### 1.1 HexBlock에 EnemyType 지원 추가
**파일**: `Assets/Scripts/Core/HexBlock.cs`

```csharp
// HexBlock.cs에 추가
public EnemyType enemyType { get; private set; } = EnemyType.None;

/// <summary>
/// 블록에 적군 설정
/// </summary>
public void SetEnemyType(EnemyType type)
{
    enemyType = type;

    // 색상도둑인 경우
    if (type == EnemyType.Chromophage)
    {
        // 색상도둑 시각 표현: 회색 오버레이 + 슬라임 아이콘
        ApplyChromophageVisuals();
    }

    // 다른 적군 타입도 추가 예정
}

private void ApplyChromophageVisuals()
{
    // 블록 색상을 회색으로 변경
    UpdateBlockColor(Color.gray);

    // 회색 슬라임 오버레이 추가 (프로시저럴)
    // 펄스 애니메이션 적용
}

/// <summary>
/// 블록이 색상도둑의 영향을 받는지 확인
/// </summary>
public bool HasEnemy()
{
    return enemyType != EnemyType.None;
}

/// <summary>
/// 특정 적군 타입 여부 확인
/// </summary>
public bool HasEnemyOfType(EnemyType type)
{
    return enemyType == type;
}
```

#### 1.2 MatchingSystem에서 색상도둑 처리
**파일**: `Assets/Scripts/Core/MatchingSystem.cs`

```csharp
// MatchingSystem.cs의 IsValidMatch 메서드 수정
private bool IsValidMatch(HexBlock block)
{
    // ... 기존 로직 ...

    // 색상도둑 블록은 매칭에서 제외
    if (block.HasEnemyOfType(EnemyType.Chromophage))
    {
        return false; // 이 블록은 매칭 대상에서 제외
    }

    return true;
}
```

#### 1.3 BlockRemovalSystem에 적군 제거 이벤트 추가
**파일**: `Assets/Scripts/Core/BlockRemovalSystem.cs`

```csharp
// BlockRemovalSystem.cs에 추가
/// <summary>
/// 적군이 붙은 블록이 제거될 때 호출
/// </summary>
public event System.Action<HexBlock, EnemyType> OnEnemyRemoved;

// 블록 제거 처리 중
private void RemoveBlock(HexBlock block)
{
    // 적군 제거 이벤트 발동
    if (block.HasEnemy())
    {
        OnEnemyRemoved?.Invoke(block, block.enemyType);
    }

    // ... 기존 제거 로직 ...
}
```

#### 1.4 StageManager에 미션 데이터 연동
**파일**: `Assets/Scripts/Managers/StageManager.cs`

```csharp
// StageManager.cs에 추가
/// <summary>
/// 미션 1 스테이지 데이터 설정
/// </summary>
public void SetMission1StageData(StageData stageData)
{
    currentStageData = stageData;
    InitializeMissions();

    // 적군 제거 이벤트 연동
    if (BlockRemovalSystem.Instance != null)
    {
        BlockRemovalSystem.Instance.OnEnemyRemoved += OnEnemyRemoved;
    }
}

/// <summary>
/// 적군 제거 시 호출
/// </summary>
private void OnEnemyRemoved(HexBlock block, EnemyType enemyType)
{
    // 미션 진행도 업데이트
    foreach (var mission in missionProgress)
    {
        if (mission.mission.type == MissionType.RemoveEnemy &&
            mission.mission.targetEnemyType == enemyType)
        {
            mission.currentCount++;
            CheckMissionCompletion(missionProgress.IndexOf(mission));
        }
    }

    OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
}
```

---

### Phase 2: 시각 표현 (중간 우선순위)

#### 2.1 색상도둑 비주얼 구현
**파일**: `Assets/Scripts/Core/HexBlock.cs`

**요구사항**:
- 회색 슬라임 오버레이 (반투명, 20% 불투명도)
- 펄스 애니메이션 (0.5초 주기)
- 강조 시 붉은 테두리

**구현 예**:
```csharp
private void ApplyChromophageVisuals()
{
    // 1. 회색 오버레이 생성
    var overlayColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // 회색 + 반투명

    // 2. 펄스 애니메이션
    StartCoroutine(PulseAnimation(1f));
}

private IEnumerator PulseAnimation(float duration)
{
    while (HasEnemy())
    {
        // 밝아짐 (0.25초)
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.2f, 0.4f, elapsed / 0.25f);
            UpdateOverlayAlpha(alpha);
            yield return null;
        }

        // 어두워짐 (0.25초)
        elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.4f, 0.2f, elapsed / 0.25f);
            UpdateOverlayAlpha(alpha);
            yield return null;
        }
    }
}
```

#### 2.2 UI 진행도 바
**파일**: `Assets/Scripts/UI/MissionProgressUI.cs` (신규)

**기능**:
- 좌상단: 적군 제거 진행도 표시
- "색상도둑 제거: X/3" 형태
- 각 적군 제거 시 실시간 업데이트

```csharp
public void UpdateEnemyProgress(int current, int target)
{
    missionProgressText.text = $"색상도둑 제거: {current}/{target}";
    progressBar.fillAmount = (float)current / target;
}
```

#### 2.3 이펙트 시스템
**파일**: `Assets/Scripts/Utils/VisualConstants.cs` (확장)

**색상도둑 제거 이펙트**:
- 밝은 에너지 파동 (흰색 + 금색)
- 파티클 폭발 (입자 20개)
- 효과음: 밝은 종 소리
- 지속 시간: 0.5초

---

### Phase 3: 스토리/튜토리얼 (낮은 우선순위)

#### 3.1 튜토리얼 팝업 시스템
**파일**: `Assets/Scripts/UI/TutorialPopup.cs` (신규)

**Stage 1 튜토리얼**:
```
제목: "색상도둑이 나타났어요!"
내용:
- "보석의 색이 사라졌어요!"
- "그레이 색상의 블록은 일반 매칭에 사용할 수 없어요."
- "특수 블록(드릴, 폭탄 등)을 사용하면 제거할 수 있어요!"
```

#### 3.2 오라클리온 대사 시스템
**파일**: `Assets/Scripts/UI/DialogueManager.cs`

**Stage 1-10 대사 통합**:
- Mission1StageData.StoryData에서 대사 자동 로드
- 게임 상황에 따라 동적 표시

#### 3.3 스토리 컷씬
**파일**: `Assets/Scripts/UI/CutscenePlayer.cs`

**Stage 1 시작 전 컷씬**:
```
루나가 크리스탈 숲에 발을 디딘다.
오라클리온: "자, 이게 보석의 격자야. 같은 색 3개가 모이면 정화된단다."
```

**Stage 10 보스 컷씬**:
```
루나가 크리스탈 숲의 중심에 도달한다.
거대한 색상도둑 무리 3개가 마주선다!
```

---

## 🧪 테스트 가이드

### 단위 테스트

#### Test 1: 적군 배치 확인
```csharp
[Test]
public void TestMission1Stage1EnemyPlacement()
{
    var stage = Mission1StageData.GetAllMission1Stages()[1];

    // 색상도둑 1개 배치 확인
    Assert.AreEqual(1, stage.enemyPlacements.Length);
    Assert.AreEqual(EnemyType.Chromophage, stage.enemyPlacements[0].enemyType);
    Assert.AreEqual(0, stage.enemyPlacements[0].coord.q);
    Assert.AreEqual(0, stage.enemyPlacements[0].coord.r);
}
```

#### Test 2: 미션 완료 조건
```csharp
[Test]
public void TestMission1CompletionCondition()
{
    var stage = Mission1StageData.GetAllMission1Stages()[1];
    var mission = stage.missions[0];

    // Stage 1: 색상도둑 1개 제거
    Assert.AreEqual(MissionType.RemoveEnemy, mission.type);
    Assert.AreEqual(1, mission.targetCount);
    Assert.AreEqual(EnemyType.Chromophage, mission.targetEnemyType);
}
```

#### Test 3: 게임 로드
```csharp
[Test]
public void TestMission1StageLoading()
{
    var loader = new Mission1StageLoader();
    var hexGrid = new HexGrid(5);
    var stageManager = new StageManager();

    // Stage 5 로드 (2개 적군 + 5개 고정 블록)
    loader.LoadStage(5, hexGrid, stageManager);

    // 적군 배치 확인
    Assert.AreEqual(2, GetPlacedEnemyCount(hexGrid));
    Assert.AreEqual(5, GetPlacedFixedBlockCount(hexGrid));
}
```

### 수동 테스트 (Play 모드)

#### 테스트 1: Stage 1 기본 플레이
1. Scene 열기: `Assets/Scenes/Game.unity`
2. 콘솔: `Mission1Helper.StartMission1Stage(1, hexGrid, stageManager)`
3. 확인사항:
   - 중앙 (0, 0)에 회색 블록 1개 배치 ✓
   - 색상도둑 블록이 일반 매칭 불가 ✓
   - 드릴로 제거 가능 ✓
   - 25턴 제한 표시 ✓

#### 테스트 2: Stage 10 보스전
1. Scene 열기: `Assets/Scenes/Game.unity`
2. 콘솔: `Mission1Helper.StartMission1Stage(10, hexGrid, stageManager)`
3. 확인사항:
   - 색상도둑 3개 배치 (좌, 중앙, 우) ✓
   - 고정 블록 5개 배치 ✓
   - 두 가지 미션 목표: 색상도둑 3개 + 콤보 20 ✓
   - 콤보 달성 시 보너스 경험치 ✓

#### 테스트 3: 난이도 곡선
모든 Stage (1~10)를 순서대로 플레이:
- 턴 제한 점진적 증가 ✓
- 적군 개수 증가 (1→2→3) ✓
- 고정 블록 증가 (0→6) ✓

---

## 📝 데이터 검증

### Mission1StageData 내용 확인
```csharp
// 콘솔에서 실행
Mission1Helper.PrintAllMission1StageInfo();

// 출력 예
=== 미션 1 스테이지 정보 ===
Stage 1: 크리스탈 숲
  난이도: 1 | 턴: 25
  적군: 1개 | 장애물: 0개
  미션: 색상도둑(회색) 1마리 제거
...
Stage 10: 크리스탈 숲
  난이도: 3 | 턴: 40
  적군: 3개 | 장애물: 5개
  미션: 색상도둑(회색) 3마리 제거
  미션: 20 콤보 달성
```

### 특정 스테이지 배치 확인
```csharp
// Stage 5 적군 배치 확인
Mission1Helper.PrintStageEnemyLayout(5);

// 출력 예
=== Stage 5 적군 배치 ===
  색상도둑 at (-1, -2)
  색상도둑 at (2, 2)

=== Stage 5 고정 블록 배치 ===
  고정 블록 at (-3, 1)
  고정 블록 at (-2, 0)
  ...
```

---

## 🔌 Integration Checklist

### BlockRemovalSystem과 통합
- [ ] OnEnemyRemoved 이벤트 구현
- [ ] 적군 제거 시 이벤트 발동
- [ ] 파티클 이펙트 재생

### StageManager와 통합
- [ ] SetMission1StageData() 메서드 추가
- [ ] OnEnemyRemoved 이벤트 리스너 연결
- [ ] 미션 진행도 자동 업데이트

### UIManager와 통합
- [ ] 진행도 바 UI 표시
- [ ] 미션 완료 시 축하 애니메이션
- [ ] 보스 스테이지 특별 UI

### AudioManager와 통합
- [ ] 색상도둑 제거 음향 (종 소리)
- [ ] 콤보 달성 사운드
- [ ] 보스전 배경음악

### DialogueManager와 통합
- [ ] StoryData에서 대사 자동 로드
- [ ] 게임 중 오라클리온 음성
- [ ] 클리어 후 축하 대사

---

## 🐛 알려진 이슈 및 주의사항

### 주의사항
1. **좌표계**: Axial Coordinate (q, r) 사용
   - q: 좌우 (음수=좌, 양수=우)
   - r: 위아래 (음수=위, 양수=아래)

2. **고정 블록 vs 적군**: 동시 배치 불가
   - 같은 좌표에 고정 블록과 적군 배치 X
   - 명확히 분리된 좌표 사용

3. **매칭 시스템**: 색상도둑 블록은 매칭 제외
   - MatchingSystem에서 필터링 필수
   - 일반 매칭 시도 시 "매칭 실패" 피드백

4. **이벤트 연결**: Stage 로드 시 필수
   - Mission1StageLoader 호출 후 자동 연결
   - 수동 연결 필요 시 명시적으로 호출

---

## 📚 참고 문서

- **기획 문서**: GameMissions_StoryArc.md
- **레벨 디자인**: LevelDesign_Mission1_ChromophageCleaning.md
- **적군 시스템**: Dev_EnemySystem_Design.md
- **핵심 좌표계**: HexCoord.cs

---

## 💬 질문 및 피드백

### 데이터 정책
- **Stage 1-10 데이터**: Mission1StageData.cs (고정, 변경 불필요)
- **로더 로직**: Mission1StageLoader.cs (필요시 확장 가능)
- **통합 포인트**: BlockRemovalSystem, StageManager (커스터마이징 필요)

### 변경 사항
Stage 1-10의 기본 설정이 필요하면:
1. 기획팀과 협의
2. Mission1StageData.cs에서 해당 메서드 수정
3. git에 변경사항 커밋

---

## ✅ 구현 순서 (추천)

**1주차**:
- [ ] Phase 1.1-1.4 기본 통합
- [ ] 단위 테스트 작성
- [ ] Stage 1-3 수동 테스트

**2주차**:
- [ ] Phase 2.1-2.3 시각 표현
- [ ] Stage 4-7 테스트
- [ ] 밸런스 조정

**3주차**:
- [ ] Phase 3.1-3.3 스토리/튜토리얼
- [ ] Stage 8-10 테스트
- [ ] 최종 QA

---

**이 가이드를 따라 구현하면 미션 1 (Stage 1-10)이 완성됩니다!** 🚀


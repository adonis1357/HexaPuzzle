# 개발팀 제출서: 미션 1 색상도둑 소탕 구현 패키지

**제출일**: 2026-02-17
**대상**: 개발팀 (HexaPuzzle 프로젝트)
**상태**: ✅ 준비 완료 (구현 단계 진입)

---

## 📦 제출 내용 요약

개발팀을 위해 **미션 1 (Stage 1-10) 완전한 구현 패키지**를 준비했습니다.

### 🎯 핵심 제공 사항

#### 1. 기획 문서 (3개)
| 문서 | 내용 | 상태 |
|------|------|------|
| **GameMissions_StoryArc.md** | 10개 미션 통합 설계서 | ✅ 완료 |
| **LevelDesign_Mission1_ChromophageCleaning.md** | Stage 1-10 상세 레벨 디자인 | ✅ 완료 |
| **IMPLEMENTATION_GUIDE_Mission1.md** | 개발팀 구현 가이드 | ✅ 완료 |

#### 2. 구현 코드 (2개 클래스)
| 파일 | 역할 | 라인 수 |
|------|------|--------|
| **Mission1StageData.cs** | Stage 1-10 모든 데이터 정의 | 450+ |
| **Mission1StageLoader.cs** | 스테이지 로드 및 배치 관리 | 300+ |

#### 3. 데이터 구조 (6개 클래스 확장)
```
StageData (Stage 1-10 전체 정보)
├─ MissionData (미션 조건 + RemoveEnemy, AchieveCombo 신규)
├─ EnemyPlacement (적군 배치 좌표)
├─ StoryData (스토리/대사/컷씬)
├─ DialogueCutscene (게임 중 대사)
└─ RewardData (보상 정보)
```

---

## 🚀 빠른 시작

### 개발팀이 할 일

```
Step 1: 기획 문서 검토
├─ GameMissions_StoryArc.md (미션 전체 컨셉)
├─ LevelDesign_Mission1_ChromophageCleaning.md (Stage 1-10 설계)
└─ 리뷰 완료 시 기획팀 확인

Step 2: 코드 분석
├─ Mission1StageData.cs (데이터 구조 이해)
├─ Mission1StageLoader.cs (로더 로직 이해)
└─ IMPLEMENTATION_GUIDE_Mission1.md (구현 방법 학습)

Step 3: 구현 시작
├─ Phase 1: HexBlock, MatchingSystem, BlockRemovalSystem 통합 (1-2일)
├─ Phase 2: 시각 표현 (회색 오버레이, 이펙트) (2-3일)
└─ Phase 3: 스토리/튜토리얼 (1-2일)

Step 4: 테스트 & QA
├─ 단위 테스트 실행
├─ Stage 1-10 수동 테스트
└─ 난이도 밸런스 조정
```

### 자동 테스트 (콘솔)

```csharp
// Stage 1-10 데이터 확인
Mission1Helper.PrintAllMission1StageInfo();

// Stage 5 배치 확인
Mission1Helper.PrintStageEnemyLayout(5);

// Stage 1 게임 시작
Mission1Helper.StartMission1Stage(1, hexGrid, stageManager);
```

---

## 📊 데이터 명세

### Stage 1-10 난이도 곡선

| Stage | 적군 | 장애물 | 턴 | 난이도 | 특징 |
|-------|------|--------|-----|--------|------|
| 1 | 1 | 0 | 25 | ⭐ | 튜토리얼, 중앙 배치 |
| 2 | 1 | 0 | 28 | ⭐ | 위치 변경 |
| 3 | 2 | 0 | 30 | ⭐⭐ | 대칭 배치 |
| 4 | 2 | 3 | 32 | ⭐⭐ | 고정 블록 추가 |
| 5 | 2 | 5 | 35 | ⭐⭐ | 복합 장애물 |
| 6 | 3 | 5 | 40 | ⭐⭐ | 3개 첫 등장 |
| 7 | 3 | 6 | 42 | ⭐⭐ | 대칭 배치 |
| 8 | 3 | 6 | 45 | ⭐⭐ | 불규칙 배치 |
| 9 | 3 | 6 | 42 | ⭐⭐ | 최종 도전 |
| 10 | 3 | 5 | 40 | ⭐⭐⭐ | 보스 + 콤보 20 |

### 색상도둑 (Chromophage) 메카닉

**특성**:
- 블록 색상을 회색으로 표시
- 일반 매칭 불가 (MatchingSystem에서 필터링)
- 특수 블록(드릴, 폭탄, 레이저, 도넛)으로 제거 가능
- 제거 시 밝은 에너지 이펙트

**구현 포인트**:
```csharp
// HexBlock.cs
public void SetEnemyType(EnemyType type)
{
    enemyType = type;
    if (type == EnemyType.Chromophage)
        ApplyChromophageVisuals(); // 회색 + 펄스
}

// MatchingSystem.cs
private bool IsValidMatch(HexBlock block)
{
    if (block.HasEnemyOfType(EnemyType.Chromophage))
        return false; // 매칭 제외
    return true;
}
```

---

## 🎬 스토리 흐름

### Stage 1-10 스토리 아크

```
Stage 1-2: 루나의 첫 적군 경험 (오라클리온 튜토리얼)
    ↓
Stage 3-5: 난이도 증가 (장애물 추가, 전략 개발)
    ↓
Stage 6-9: 대량의 색상도둑 처리 (3개 등장)
    ↓
Stage 10: 보스전 + 연쇄 목표 (최종 시험)
    ↓
엔딩: 크리스탈 숲 정화 완료 → Chapter 2 진행
```

### 주요 캐릭터 상호작용

- **오라클리온**: 튜토리얼 가이드 (Stage 1), 격려 (전체)
- **프리즘**: 감정 공유, 응원
- **루나**: 점진적 성장 (자신감 증가)

---

## 🔧 구현 체크리스트

### Phase 1: 기본 통합 (높은 우선순위, 1-2일)

- [ ] HexBlock.cs에 EnemyType 필드 추가
- [ ] HexBlock.SetEnemyType() 메서드 구현
- [ ] MatchingSystem.cs에서 색상도둑 필터링
- [ ] BlockRemovalSystem에 OnEnemyRemoved 이벤트
- [ ] StageManager에 SetMission1StageData() 연동
- [ ] 단위 테스트 작성

### Phase 2: 시각 표현 (중간 우선순위, 2-3일)

- [ ] 회색 슬라임 오버레이 구현
- [ ] 펄스 애니메이션 (0.5초 주기)
- [ ] 강조 시 붉은 테두리
- [ ] 제거 이펙트 (파티클, 음향)
- [ ] 진행도 바 UI
- [ ] 시각 테스트

### Phase 3: 스토리/튜토리얼 (낮은 우선순위, 1-2일)

- [ ] 튜토리얼 팝업 (Stage 1)
- [ ] 오라클리온 대사 통합
- [ ] 컷씬 플레이어 구현
- [ ] 보스 스테이지 특별 UI
- [ ] 스토리 테스트

### 통합 테스트 & QA (3-4일)

- [ ] Stage 1-10 순차 플레이
- [ ] 난이도 밸런스 검증
- [ ] 턴 제한 적절성 확인
- [ ] 이펙트 최적화
- [ ] 대사/음향 정말하게

---

## 📚 문서 구조

```
프로젝트 루트
├── Assets/Scripts/
│   ├── Data/
│   │   ├── Mission1StageData.cs ⭐ (Stage 1-10 데이터)
│   │   └── BlockData.cs (기존)
│   │
│   ├── Managers/
│   │   ├── Mission1StageLoader.cs ⭐ (로더)
│   │   └── StageManager.cs (기존, 통합 필요)
│   │
│   ├── Core/
│   │   ├── HexBlock.cs (기존, 통합 필요)
│   │   ├── MatchingSystem.cs (기존, 통합 필요)
│   │   └── BlockRemovalSystem.cs (기존, 통합 필요)
│   │
│   ├── GameMissions_StoryArc.md ⭐ (10개 미션 설계)
│   ├── LevelDesign_Mission1_ChromophageCleaning.md ⭐ (레벨 디자인)
│   └── IMPLEMENTATION_GUIDE_Mission1.md ⭐ (구현 가이드)
```

---

## 🎯 핵심 설계 원칙

### 1. 점진적 학습
```
Stage 1-2: "색상도둑이 뭐지?" → 개념 소개
Stage 3-5: "어떻게 처리하지?" → 전략 개발
Stage 6-8: "3개면 어려운데?" → 복합 처리
Stage 9: "정말 어렵네" → 도전의식
Stage 10: "해냈다!" → 성취감
```

### 2. 명확한 피드백
- **회색**: 색상도둑 블록 즉시 인식
- **매칭 불가**: "불가능함" 명확히 표시
- **특수 블록**: 드릴/폭탄으로 제거 가능 (성공 경험)
- **진행도 바**: "N/3" 실시간 표시

### 3. 충분한 플레이 타임
```
Stage 1: 1-2분 (쉬움)
Stage 2-5: 2-3분 (천천히)
Stage 6-9: 3-4분 (생각할 시간)
Stage 10: 4-5분 (보스전)
총 25-40분 (첫 엔딩까지)
```

---

## 💡 트러블슈팅

### 자주 나올 질문

**Q1: 색상도둑 블록이 왜 회색인가?**
```
A: 세계관상 "색상을 빼앗는 적군"이므로 회색으로 표현.
   플레이어가 즉시 "이건 다르다"고 인식하도록 설계.
```

**Q2: 고정 블록과 색상도둑을 동시에 배치할 수 있나?**
```
A: 아니오. Mission1StageData에서 분리된 배치 사용.
   같은 좌표에 배치 불가.
```

**Q3: Stage 10에 "콤보 20" 추가 목표가 왜 있나?**
```
A: 색상도둑 제거가 쉬워질 수 있으므로,
   "연쇄의 아름다움"이라는 부가 목표 추가.
   선택적 도전으로 플레이어에게 자유도 제공.
```

**Q4: 턴 제한이 아주 타이트한가?**
```
A: 각 Stage의 턴은 여유 있게 설정.
   - Stage 1: 25턴 (보통 10-15턴 필요)
   - Stage 10: 40턴 (보통 20-25턴 필요)
   시간 압박 < 게임 재미가 우선.
```

---

## ✅ 개발팀 체크리스트

### 제출 패키지 검토
- [ ] Mission1StageData.cs 코드 검토
- [ ] Mission1StageLoader.cs 로직 이해
- [ ] IMPLEMENTATION_GUIDE_Mission1.md 읽음

### 요구사항 명확화
- [ ] HexBlock 통합 방식 결정
- [ ] MatchingSystem 필터링 위치 결정
- [ ] Event 연결 방식 협의
- [ ] 시각 표현 기술(프로시저럴?) 결정

### 구현 계획 수립
- [ ] Phase 1-3 일정 계획
- [ ] 담당자 분배
- [ ] 테스트 환경 준비
- [ ] QA 크라이테리아 정의

---

## 🎓 학습 포인트

개발팀이 이 구현을 통해 배울 수 있는 것:

1. **적군 시스템**: EnemyType 데이터 구조화 및 메카닉 분리
2. **게임 루프**: InputSystem → RotationSystem → MatchingSystem → BlockRemovalSystem
3. **이벤트 기반 아키텍처**: OnEnemyRemoved 등 이벤트 연결
4. **데이터 기반 설계**: Mission1StageData로 모든 스테이지 정의
5. **UI/피드백**: 진행도, 이펙트, 대사로 명확한 피드백 제공

---

## 🚀 다음 단계 (After Stage 10)

이 구현이 완료되면:
- **미션 2-10**: 같은 패턴으로 다른 적군 추가 가능
- **무한 모드**: Stage 1-10 클리어 후 무한 미션 모드 오픈
- **새로운 콘텐츠**: 시즌 업데이트, 이벤트 스테이지 등

---

## 📞 소통 채널

### 개발팀 ↔ 기획팀
- **데이터 변경**: Mission1StageData.cs 수정 후 기획팀 확인
- **설계 질문**: IMPLEMENTATION_GUIDE_Mission1.md 참조
- **피드백**: 개발 과정 중 이슈 제기 환영

### 예상 질문 대응
- 좌표 문제 → HexCoord.cs, LevelDesign 맵 참조
- 메카닉 불명 → Dev_EnemySystem_Design.md 참조
- 스토리 연계 → GameMissions_StoryArc.md 참조

---

## 📋 최종 체크

### 제출 준비 완료 확인

✅ **기획 문서**
- [x] GameMissions_StoryArc.md (10개 미션 설계)
- [x] LevelDesign_Mission1_ChromophageCleaning.md (Stage 1-10 디자인)
- [x] IMPLEMENTATION_GUIDE_Mission1.md (구현 가이드)

✅ **구현 코드**
- [x] Mission1StageData.cs (450+ 줄)
- [x] Mission1StageLoader.cs (300+ 줄)

✅ **데이터 검증**
- [x] Stage 1-10 데이터 완전성 확인
- [x] 좌표 유효성 검증
- [x] 미션 조건 명확화
- [x] 난이도 곡선 검증

✅ **git 커밋**
- [x] 3개 커밋 완료 (기획 → 레벨 디자인 → 구현 코드)
- [x] 커밋 메시지 명확함

---

## 🎉 최종 결론

**미션 1 (Stage 1-10) 개발팀 제출 패키지 완성!**

### 제공되는 것:
1. ✅ 완전한 데이터 구조 (450+ 줄 코드)
2. ✅ 스테이지 로더 (300+ 줄 코드)
3. ✅ 구현 가이드 (Phase 1-3)
4. ✅ 테스트 방법 (단위 + 수동)
5. ✅ 문서화 (기획 + 레벨 디자인)

### 개발팀이 할 일:
1. 단계별 구현 (Phase 1→2→3)
2. 테스트 & QA
3. 피드백 & 반복

### 예상 완료 기간:
**약 1-2주 (적극 추진 시 1주 가능)**

---

**개발팀의 구현을 기다립니다!** 🚀✨


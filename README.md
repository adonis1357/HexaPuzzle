# 🔷 HexaPuzzle (MatchMine)

육각형 그리드 기반 매치-3 퍼즐 게임 — Unity (C#)

> 인접 3개 블록의 삼각형 클러스터를 회전시켜 색상 매칭을 만들고, 고블린을 처치하는 전략형 퍼즐 게임입니다.

---

## 게임 특징

| 항목 | 설명 |
|------|------|
| **렌더링** | 100% 프로시저럴 생성 (UI Canvas 기반, 외부 아트 에셋 없음) |
| **좌표계** | Axial coordinate (q, r), flat-top 육각형 |
| **그리드** | 반지름 5, 약 61블록, hexSize 50px |
| **타겟** | 모바일 (한 손 플레이, 터치 입력) |
| **사운드** | 프로시저럴 오디오 합성 (AudioClip 런타임 생성) |

---

## 개발 진행 상황

### ✅ 완료된 시스템

#### 코어 게임플레이
- [x] 육각형 그리드 생성 및 좌표계
- [x] 삼각형 클러스터 선택 (터치 입력)
- [x] 120° CW/CCW 회전
- [x] 매칭 패턴 감지 (삼각형, 링)
- [x] 캐스케이드 루프 (매칭 삭제 → 낙하 → 리필 → 재확인, 최대 20회)
- [x] 블록 티어 시스템 (Normal → Tier1 → Tier2 → Tier3 → ProcessedGem)

#### 특수 블록 (6종)
- [x] **드릴** — 4매칭 직선 생성, 한 방향 라인 파괴 + 발사체 연출
- [x] **폭탄** — 5+매칭 생성, 중심 + 인접 6칸 폭발
- [x] **레인보우(도넛)** — 7+매칭 또는 링, 해당 색상 전체 파괴
- [x] **X블록** — 링 매칭, 링 색상 전체 파괴
- [x] **드론** — 스마트 타겟팅, 자동 비행 파괴
- [x] **특수 블록 콤보** — 드릴×폭탄, 드릴×드론, 드론×드론 등 조합 시스템

#### 고블린 시스템 (4종)
- [x] **몽둥이 (Regular)** — HP 5, 기본 근접 공격
- [x] **갑옷 (Armored)** — HP 15, 높은 체력
- [x] **궁수 (Archer)** — HP 1, 최상단 고정, 2턴마다 원거리 공격
- [x] **방패 (Shield)** — HP 10, 방패 내구도 3, 2턴에 1번 행동
- [x] 낙하 데미지 (물리적 충돌 기반)
- [x] 쉘 블록 시스템 (고블린 2회 공격 시 생성)
- [x] HP바 + HP 카운트다운 애니메이션
- [x] 고블린 이동 AI (공격 우선 → 빈 칸 이동)

#### MP(마나포인트) 시스템
- [x] 특수 블록 발동 MP 소모 (Drill=5, Bomb=6, Drone=7, XBlock=10)
- [x] 아이템 사용 MP 소모 (Hammer=8, Swap=9, Line=11, Reverse=12)
- [x] 프로시저럴 육각형 게이지 UI (SDF 기반)
- [x] MP 부족 피드백 (빨간 깜빡임 + 흔들림)

#### 아이템 (4종)
- [x] **망치** — 단일 블록 파괴
- [x] **스왑** — 블록 위치 교환
- [x] **라인 드로우 (SSD)** — 라인 파괴
- [x] **역회전** — 회전 방향 반전

#### 스킬 트리
- [x] 스킬 데이터 구조 (SkillType, SkillState, SkillNodeData)
- [x] 스킬 트리 매니저 (해금 상태 추적, PlayerPrefs 저장)
- [x] 스킬 트리 UI (육각형 노드 + 연결 라인 + 상세 팝업)
- [x] 드릴 이동 스킬 체인 (1칸 → 2칸 → 3칸)
- [x] 에디터 전용 디버그 버튼 (SP 추가, 초기화)

#### 스테이지
- [x] 30개 스테이지 + 무한 도전 모드
- [x] 스테이지 1-10: 크리스탈 숲 (기본 몽둥이)
- [x] 스테이지 11: 드릴 튜토리얼
- [x] 스테이지 12-15: 궁수 + 몽둥이 + 갑옷 혼합
- [x] 스테이지 16-20: 방패 고블린 등장
- [x] 스테이지 21-30: 4종 전체 혼합 (보스: 25, 30)
- [x] 무한 모드 (Stage 31): 생존 미션

#### VFX 시스템 (통일 패턴 적용)
- [x] Pre-Fire Compression (발동 전 스케일 애니메이션)
- [x] Hit Stop (Time.timeScale 일시 정지)
- [x] Zoom Punch (스케일 펄스)
- [x] Destroy Flash (백색 플래시 오버레이)
- [x] Dual Easing Destroy (확장 → 스퀴즈 파괴)
- [x] Bloom Layer (센터 글로우)
- [x] Match Pulse (매칭 하이라이트)

#### 기타
- [x] 튜토리얼 시스템
- [x] 점수 시스템 (티어별 계산)
- [x] 콤보 표시 UI
- [x] 점수 팝업
- [x] 업적 시스템
- [x] 프로시저럴 오디오 (SFX 런타임 합성)
- [x] GameOver 이어하기 (골드 소모, 비용 점진 증가)

### 🔨 개발 중

- [ ] 폭탄 범위 확장 (2칸) + 거리별 차등 데미지 (직격 3, 1칸 2, 2칸 1)
- [ ] 폭탄 넉백 시스템 (고블린을 폭발 반대 방향으로 밀어냄)
- [ ] 드릴 드래그 앤 드랍 이동 메커닉 (스킬 트리 연동)

### 📋 계획

- [ ] 추가 특수 블록 종류 확장
- [ ] 스킬 트리 확장 (폭탄, 드론, 레인보우 스킬 체인)
- [ ] 스테이지 추가 (30+ 스테이지)
- [ ] 보스 전투 메커닉 강화

---

## 프로젝트 구조

```
Assets/Scripts/
├── Core/                 # 게임플레이 시스템 (16개 파일)
│   ├── HexGrid.cs            그리드 생성, 좌표계, 블록 스폰
│   ├── HexCoord.cs           축 좌표 구조체
│   ├── HexBlock.cs           블록 비주얼/상태, 티어 진행
│   ├── InputSystem.cs        터치 감지, 클러스터 선택
│   ├── RotationSystem.cs     120° CW/CCW 회전
│   ├── MatchingSystem.cs     패턴 감지 (삼각형/링)
│   ├── BlockRemovalSystem.cs 캐스케이드 루프, 낙하 물리
│   ├── DrillBlockSystem.cs   드릴 발사체, 라인 파괴
│   ├── BombBlockSystem.cs    폭발 이펙트, 방사형 파괴
│   ├── DonutBlockSystem.cs   레인보우, 색상별 전체 파괴
│   ├── XBlockSystem.cs       X패턴, 링 색상 파괴
│   ├── DroneBlockSystem.cs   드론 비행, 스마트 타겟팅
│   ├── SpecialBlockComboSystem.cs  특수 블록 합성
│   ├── GoblinSystem.cs       고블린 적군 시스템
│   ├── EnemySystem.cs        적 기본 시스템
│   └── EditorTestSystem.cs   에디터 테스트
│
├── Data/                 # 데이터 정의 (6개 파일)
│   ├── BlockData.cs          열거형, 데이터 클래스
│   ├── SkillData.cs          스킬 트리 데이터
│   ├── LevelRegistry.cs      레벨/스테이지 레지스트리
│   ├── LevelData.cs          레벨 데이터
│   ├── Mission1StageData.cs  미션1 스테이지 데이터
│   ├── TutorialData.cs       튜토리얼 데이터
│   └── EnemyRegistry.cs      적 레지스트리
│
├── Managers/             # 싱글톤 매니저 (14개 파일)
│   ├── GameManager.cs        상태 머신, 턴 카운터, 로비
│   ├── StageManager.cs       스테이지/미션 진행
│   ├── MissionSystem.cs      미션 판정
│   ├── SkillTreeManager.cs   스킬 해금/관리
│   ├── MPManager.cs          마나포인트 관리
│   ├── UIManager.cs          UI 팝업, HUD
│   ├── TutorialManager.cs    튜토리얼 진행
│   ├── ItemManager.cs        아이템 관리
│   ├── ScoreManager.cs       점수 관리
│   ├── ScoreCalculator.cs    티어별 점수 계산
│   ├── AchievementManager.cs 업적 시스템
│   ├── AudioManager.cs       SFX 풀, 오디오 연동
│   ├── GameSoundController.cs 사운드 제어
│   └── Mission1StageLoader.cs 미션1 스테이지 로드
│
├── UI/                   # UI 컴포넌트 (5개 파일)
│   ├── SkillTreeUI.cs        스킬 트리 페이지
│   ├── MPGaugeUI.cs          MP 게이지 (SDF 육각형)
│   ├── TutorialUI.cs         튜토리얼 UI
│   ├── ComboDisplay.cs       콤보 표시
│   └── ScorePopupManager.cs  점수 팝업
│
├── Items/                # 아이템 로직 (4개 파일)
│   ├── HammerItem.cs         망치
│   ├── SwapItem.cs           스왑
│   ├── LineDrawItem.cs       라인 드로우
│   └── ReverseRotationItem.cs 역회전
│
├── Utils/                # 유틸리티 (6개 파일)
│   ├── VisualConstants.cs    이징, 색상, 비주얼 상수
│   ├── ProceduralAudio.cs    런타임 사운드 합성
│   ├── GemSpriteProvider.cs  프로시저럴 젬 스프라이트
│   ├── GemMaterialManager.cs 젬 머티리얼 관리
│   ├── HexagonMeshGenerator.cs 육각형 메시 생성
│   └── UITheme.cs            UI 테마
│
├── Setup/                # 초기화
│   └── HammerItemSetup.cs
│
└── Editor/               # 에디터 도구
    └── IconPreviewGenerator.cs
```

**총 코드량**: ~57,700줄 (C#)

---

## 코어 게임 루프

```
InputSystem → RotationSystem → MatchingSystem → BlockRemovalSystem → GameManager → StageManager
   터치         120° 회전         패턴 감지        캐스케이드 루프       상태/턴        미션 판정
```

캐스케이드: 매칭 삭제 → 특수 블록 발동 → 낙하 물리 → 리필 → 재확인 (최대 20회)

---

## 기술 스택

- **엔진**: Unity
- **언어**: C# (96.1%)
- **렌더링**: UI.Image + RectTransform (Canvas 기반)
- **VFX**: 코루틴 기반 애니메이션, 프로시저럴 파티클
- **SFX**: 프로시저럴 AudioClip 합성 (외부 오디오 파일 없음)
- **데이터 저장**: PlayerPrefs

---

## 빌드 및 실행

1. Unity에서 프로젝트 열기
2. Unity 에디터에서 Play 버튼으로 테스트
3. 모바일 빌드: Build Settings → Android/iOS

---

*이 프로젝트는 [Claude Code](https://claude.com/claude-code)의 지원으로 개발되고 있습니다.*

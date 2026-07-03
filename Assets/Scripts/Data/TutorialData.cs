// =====================================================================================
// TutorialData.cs — 튜토리얼 데이터 정의
// =====================================================================================
// 튜토리얼 시퀀스, 스텝, 트리거 조건을 정의하는 데이터 클래스.
// 모든 튜토리얼 콘텐츠(대사, 하이라이트 위치, 강제 입력 등)를 하드코딩 방식으로 관리.
// =====================================================================================
using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.Data
{
    // ============================================================
    // Enum 정의
    // ============================================================

    /// <summary>
    /// 튜토리얼 스텝 유형
    /// </summary>
    public enum TutorialStepType
    {
        Dialog,         // 대화 팝업 (탭하면 다음으로)
        Highlight,      // 특정 영역 하이라이트 + 설명
        ForcedAction,   // 강제 입력 유도 (지정 위치만 터치 가능)
        FreePlayHint,   // 자유 플레이 + 화면 위 힌트 메시지
        WaitForEvent    // 특정 이벤트 대기 (매칭, 특수블록 생성 등)
    }

    /// <summary>
    /// 튜토리얼 시퀀스를 발동시키는 트리거 조건
    /// </summary>
    public enum TutorialTrigger
    {
        OnStageStart,           // 스테이지 시작 시
        OnFirstMatch,           // 첫 매칭 발생 시
        OnFirstSpecialCreate,   // 첫 특수블록 생성 시
        OnFirstBombUse,         // 첫 폭탄 발동 시
        OnFirstDrillUse,        // 첫 드릴 발동 시
        OnFirstItemUse,         // 첫 아이템 사용 시
        OnFirstEnemyEncounter,  // 첫 적군 등장 시
        OnTurnCount,            // N턴 경과 시
        OnMissionProgress       // 미션 진행도 달성 시
    }

    /// <summary>
    /// WaitForEvent 스텝에서 대기할 이벤트 유형
    /// </summary>
    public enum TutorialWaitEvent
    {
        None,
        RotationComplete,       // 회전 완료 (성공/실패 무관)
        MatchOccurred,          // 매칭 발생
        SpecialBlockCreated,    // 특수 블록 생성
        CascadeComplete,        // 캐스케이드 완료
        AnyInput,               // 아무 입력
        MatchHighlightPause,    // 매칭 하이라이트 후 ~ 특수 블록 생성 전 pause
        DrillCreatedPause,      // 드릴 생성 완료 후 pause
        DrillActivated,         // 드릴 발동 완료
        BombCreatedPause,       // 폭탄 생성 완료 후 pause
        BombActivated,          // 폭탄 발동 완료
        DroneCreatedPause,      // 드론 생성 완료 후 pause
        DroneActivated,         // 드론 발동 완료
        RainbowCreatedPause,    // 타겟 레이저 생성 완료 후 pause
        RainbowActivated,       // 레인보우 발동 완료
        XBlockCreatedPause,     // X블록 생성 완료 후 pause
        XBlockActivated,        // X블록 발동 완료
        HammerUsed,             // 망치 아이템 사용 완료 (블록 파괴 후)
        HammerActivated,        // 망치 버튼 클릭 → UseReady 상태 전환 (블록 선택 대기)
        SwapActivated,          // 스왑 버튼 클릭 → 타겟 선택 대기
        SwapUsed,               // 스왑 완료 (두 블록 위치 교환 성공)
        LineDrawActivated,      // 라인(SSD) 버튼 클릭 → 드래그 대기
        LineDrawUsed,           // 라인(SSD) 드래그 완료 (블록 파괴 후)
        ReverseActivated,       // 역회전 버튼 클릭 → 토글 활성화 (다음 회전 한 번 CCW로 전환)
        ComboActivated          // 두 특수 블록 합성(스왑) 발동 완료
    }

    // ============================================================
    // 데이터 클래스
    // ============================================================

    /// <summary>
    /// 튜토리얼 개별 스텝 데이터
    /// </summary>
    [System.Serializable]
    public class TutorialStep
    {
        public string id;                           // 고유 ID
        public TutorialStepType type;               // 스텝 유형
        public string characterName = "";           // 캐릭터 이름 (빈 문자열이면 시스템 메시지)
        public string title = "";                   // 팝업 제목
        public string message = "";                 // 팝업 메시지 본문

        // 하이라이트 설정
        public bool useHighlight = false;           // 하이라이트 사용 여부
        public Vector2 highlightScreenPos;          // 하이라이트 화면 좌표 (Canvas 중앙 기준)
        public float highlightRadius = 80f;         // 하이라이트 반경 (원형 — highlightSize가 0일 때)
        public Vector2 highlightSize;               // 하이라이트 직사각형 크기 (0이면 highlightRadius 사용)
        public string highlightTargetName;          // UI GameObject 이름 기반 자동 타겟팅 (지정 시 위치/크기 자동 계산)
        public float highlightPadding = 16f;        // Target 기반 타겟팅 시 여백 (px)

        // 강제 입력 설정
        public HexCoord[] allowedCoords;            // 터치 허용 좌표 (ForcedAction용)
        public bool showFingerGuide = false;        // 손가락 가이드 표시
        public Vector2 fingerGuidePos;              // 손가락 가이드 위치
        public bool useBottomHint = false;          // true면 상단 배너 대신 하단 BottomHint UI에 메시지 표시
        public string blockedToastMessage = "";     // 허용되지 않은 좌표 탭 시 표시할 토스트 메시지 (빈 문자열이면 기본 메시지)

        // 이벤트 대기 설정
        public TutorialWaitEvent waitEvent = TutorialWaitEvent.None;
        // Dialog 스텝에서 내용을 표시하기 전에 기다릴 이벤트 (None이면 즉시 표시).
        // 이 필드를 사용하면 WaitForEvent + Dialog를 한 스텝으로 합쳐 race 없이 원자적 처리 가능.
        public TutorialWaitEvent waitForEventBeforeShow = TutorialWaitEvent.None;
        // 이벤트 대기 타임아웃 초 단위 override (기본 카테고리 타임아웃 무시). 0 이하이면 기본값 사용.
        // 짧은 타임아웃으로 "이벤트 발생 시 실행, 없으면 스킵" 패턴 구현 (예: 옵셔널 캐스케이드 매칭 대기).
        public float waitTimeoutOverride = 0f;
        // true면 waitTimeoutOverride 타임아웃 시 시퀀스 전체 스킵이 아니라 다음 스텝으로 정상 진행.
        public bool waitTimeoutContinueOnSkip = false;

        // 블록 좌표 하이라이트 설정 (특수블록 튜토리얼용)
        public HexCoord[] highlightBlockCoords;        // 지정 좌표 블록 글로우 + 나머지 딤
        public bool forceDrillClick = false;            // 드릴 클릭 강제 (ForcedAction에서 드릴 좌표만 허용)
        public bool forceSpecialClick = false;          // 특수블록 클릭 강제 (생성된 특수블록 좌표만 허용, 범용)

        // 대화 아이콘 (해금 인트로용) — 설정 시 대화창 왼쪽에 특수블록 아이콘 + 오른쪽 정렬 텍스트
        public SpecialBlockType iconSpecialType = SpecialBlockType.None;

        // 진행 설정
        public bool pauseGame = false;              // 게임 일시정지 여부
        public float autoAdvanceDelay = 0f;         // 자동 진행 딜레이 (0이면 탭 대기)
        public float hintDuration = 4f;             // 힌트 표시 시간 (FreePlayHint용)
    }

    /// <summary>
    /// 튜토리얼 시퀀스 (여러 스텝의 묶음)
    /// </summary>
    [System.Serializable]
    public class TutorialSequence
    {
        public string sequenceId;                   // 시퀀스 고유 ID
        public TutorialTrigger trigger;             // 발동 조건
        public int triggerStage = -1;               // 발생 스테이지 (-1이면 모든 스테이지)
        public SpecialBlockType triggerSpecialType = SpecialBlockType.None; // 특수블록 트리거용
        public EnemyType triggerEnemyType = EnemyType.None;                // 적군 트리거용
        public TutorialStep[] steps;                // 스텝 배열
        public bool showOnce = true;                // 한 번만 표시
    }

    // ============================================================
    // 튜토리얼 콘텐츠 데이터베이스
    // ============================================================

    /// <summary>
    /// 모든 튜토리얼 시퀀스를 생성하는 정적 팩토리 클래스
    /// </summary>
    public static class TutorialDatabase
    {
        /// <summary>
        /// 모든 튜토리얼 시퀀스 목록 반환
        /// </summary>
        public static List<TutorialSequence> GetAllSequences()
        {
            var sequences = new List<TutorialSequence>();

            // ── 스테이지 1: 기초 온보딩 (클릭→회전→매칭 강조, 2회 연습) ──
            sequences.Add(GetStage1_Onboarding());

            // ── 스테이지 2: 흙더미 첫 등장 + 설명 ──
            sequences.Add(GetStage2_GoblinIntro());

            // ── 스테이지 3: 몽둥이 고블린 첫 등장 + 공략 자세히 설명 ──
            sequences.Add(GetStage3_GoblinIntro());

            // ── 스테이지 4: 드릴 특수블록 소개 + 해금 (인터랙티브) ──
            //   ★ Stage 3 (2026-04-27 재설계): 몽둥이 2→3 웨이브 — 드릴 튜토리얼은 Stage 4로 이동
            sequences.Add(GetStage4_DrillIntro());

            // ── 스테이지 6: 망치 아이템 소개 + 해금 ──
            sequences.Add(GetTut_Hammer());

            // ── 스테이지 8: 폭탄 소개 + 해금 ──
            sequences.Add(GetTut_Bomb());

            // ── 스테이지 13: 역회전 아이템 소개 + 해금 ──
            sequences.Add(GetTut_Reverse());

            // ── 스테이지 14: (Rainbow/XBlock 통합으로 빈 슬롯 — 몬스터 공략은 첫 등장 시 개별 hint로 제공) ──

            // ── 스테이지 19: 스왑 아이템 소개 + 해금 ──
            sequences.Add(GetTut_Swap());

            // ── 스테이지 22: 드론 소개 + 해금 ──
            sequences.Add(GetTut_Drone());

            // ── 스테이지 25: 라인 아이템 소개 + 해금 ──
            sequences.Add(GetTut_LineDraw());

            // ── 스테이지 23: 타겟 레이저 (Rainbow+XBlock 통합) 소개 + 해금 ──
            sequences.Add(GetTut_TargetLaser());

            // ── 스테이지 31, 33: 합성 튜토리얼 (조합당 1개, 강제 합성 포함) ──
            //   31: 동종 합성(드릴+드릴) — 첫 합성 학습
            //   33: 이종 합성(드릴+폭탄) — 다른 조합 시도 권유 (열린 안내)
            //   35~49는 튜토리얼 없이 자유 플레이로 다양한 합성 발견하도록 유도
            sequences.Add(MakeComboTutorial(31, "드릴+드릴",
                "6방향 드릴!\n세로·슬래시·백슬래시 3축을 동시에 관통해\n드릴 단일 발동보다 훨씬 넓은 범위를 파괴합니다.",
                "인접한 두 드릴을 스왑해 보세요!",
                isOpenEnded: false));

            sequences.Add(MakeComboTutorial(33, "드릴+폭탄",
                "폭발 드릴!\n주변 2링이 먼저 폭발한 뒤\n드릴 방향으로 확장 범위까지 관통합니다.",
                "드릴과 폭탄을 스왑해 보세요!\n앞으로 다양한 조합을 통해 합성을 해보세요!",
                isOpenEnded: true));

            // ── 상황별 힌트: 특수 블록 첫 생성 ──
            sequences.Add(GetHint_BombCreated());
            sequences.Add(GetHint_RainbowCreated());
            sequences.Add(GetHint_DroneCreated());
            sequences.Add(GetHint_XBlockCreated());

            // ── 상황별 힌트: 적군 첫 등장 (특성 + 공략법) ──
            sequences.Add(GetHint_Chromophage());
            sequences.Add(GetHint_ChainAnchor());
            sequences.Add(GetHint_ThornParasite());
            sequences.Add(GetHint_GravityWarper());
            sequences.Add(GetHint_ReflectionShield());
            // ★ GetHint_Goblin (몽둥이) 제거 — Stage 2 stage2_goblin_intro가 이미 자세히 다룸 (중복 방지)
            sequences.Add(GetHint_ArmoredGoblin());
            sequences.Add(GetHint_ArcherGoblin());
            sequences.Add(GetHint_ShieldGoblin());
            sequences.Add(GetHint_BombGoblin());
            sequences.Add(GetHint_HeavyGoblin());
            sequences.Add(GetHint_WizardGoblin());
            sequences.Add(GetHint_ThiefGoblin());
            sequences.Add(GetHint_WitchGoblin());

            return sequences;
        }

        // ============================================================
        // 스테이지 1: 기초 온보딩
        //   환영 → 회전 설명 → 중앙 강제 회전 → 매칭+낙하 관찰 →
        //   몬스터 낙하 데미지 설명 → 2차 회전 → UI 설명(점수/이동횟수/미션/몬스터) → 자유 플레이
        // ============================================================
        private static TutorialSequence GetStage1_Onboarding()
        {
            return new TutorialSequence
            {
                sequenceId = "stage1_onboarding",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 1,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // ═══════════════════════════════════════════════════════════
                    // 스테이지 1 새 디자인 (2026-04-23) — 회전 메커니즘 집중 튜토리얼
                    //  - 중앙 7블록 + R/G 2색
                    //  - 미션: 빨강 10개 → 초록 10개
                    //  - 3단계 강제 클릭으로 120°/240°/360° 회전 시나리오 설명
                    //  - 스포트라이트, 손가락 가이드, 하단 힌트 UI, 토스트 피드백 통합
                    // ═══════════════════════════════════════════════════════════
                    // ── Step 0: 환영 + 세계관 (고블린 습격 내용 제거, 블록 정화로 변경) ──
                    new TutorialStep
                    {
                        id = "s1_welcome",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "환영합니다!",
                        message = "이곳은 <color=#7FE3FF><b>정령섬</b></color>의 <color=#8EE080><b>엘프마을</b></color>!\n<color=#FFE066><b>정령 블록</b></color>을 <color=#B8F0FF>정화</color>하여\n마을에 <color=#FFD060>빛</color>을 되찾아 주세요.",
                        pauseGame = true
                    },
                    // ── Step 1: 미션 소개 (하이라이트: 미션 UI) ──
                    new TutorialStep
                    {
                        id = "s1_mission_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "첫 번째 미션",
                        message = "<color=#E06060><b>빨간 블록 10개</b></color>를 정화하세요!\n그 다음 <color=#8EE080><b>초록 블록 10개</b></color>를 정화하면 클리어!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "GameMissionUI",
                        highlightPadding = 20f,
                        highlightScreenPos = new Vector2(-370f, 810f),
                        highlightRadius = 140f
                    },
                    // ── Step 2: 매칭 규칙 설명 ──
                    new TutorialStep
                    {
                        id = "s1_matching_rule",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "매칭 규칙",
                        message = "같은 색 <color=#FFE066><b>3개를 삼각형</b></color> 모양으로 모으면\n블록이 <color=#B8F0FF>정화</color>됩니다!\n\n삼각형 3블록을 탭하면 <color=#FFE066><b>시계 방향</b></color>으로 120도씩 회전해요.",
                        pauseGame = true
                    },
                    // ═══════════════════════════════════════════════════════════
                    //  [Section 1] 120도 회전 강제 탭 — 빨간색 매칭 + 미션/이동/점수 설명
                    // ═══════════════════════════════════════════════════════════

                    // ── Step 3: 120도 회전 설명 ──
                    new TutorialStep
                    {
                        id = "s1_120_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "120도 회전",
                        message = "삼각형 중심을 <color=#FFE066><b>한 번 탭</b></color>하면\n블록이 <color=#FFE066><b>시계 방향으로 120도</b></color> 회전합니다.\n\n밝게 표시된 블록을 탭해 보세요!",
                        pauseGame = true
                    },
                    // ── Step 4: 강제 탭 #1 (중앙 클러스터 120° → 빨강 매칭) ──
                    //   보드 프리셋(SetupStage1TutorialBoard)에 따라 120도 CW 회전 시
                    //   (0,0)(1,-1)(1,0) 삼각형에서 빨강 3매칭 발생
                    new TutorialStep
                    {
                        id = "s1_force_rotate_120",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "회전 연습",
                        message = "밝은 삼각형 중심을 터치하여 회전!",
                        showFingerGuide = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 블록 중심을 탭하여 주세요!",
                        allowedCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0),
                            new HexCoord(1, 0),
                            new HexCoord(0, 1)
                        }
                    },
                    // ── Step 5: 매칭 하이라이트 Pause 대기 ──
                    new TutorialStep
                    {
                        id = "s1_wait_match_pause_1",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.MatchHighlightPause
                    },
                    // ── Step 6: 매칭 결과 + 미션 감소 설명 ──
                    new TutorialStep
                    {
                        id = "s1_120_match_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "120도 회전 성공!",
                        message = "<color=#E06060><b>빨간 블록 3개</b></color>가 매칭되어 정화됐어요!\n<color=#E06060>빨강 미션</color>이 <b>-3</b> 감소하고,\n<color=#FFE066>점수</color>를 획득합니다.\n\n매칭 성공 시 이동 횟수 <b>-1</b> 소모!",
                        pauseGame = true
                    },
                    // ── Step 7: 캐스케이드 연쇄 매칭 순간 설명 (옵션 — 3초 이내 발생 시만) ──
                    //   낙하하는 블록이 추가 매칭을 만들면 MatchHighlightPause가 다시 발사됨
                    //   BRS는 isPausedForTutorial로 멈춰서 매칭된 블록이 밝게 highlight된 상태 유지
                    //   → 3초 이내 발생하지 않으면 waitTimeoutContinueOnSkip으로 Dialog 건너뛰고 다음 스텝
                    new TutorialStep
                    {
                        id = "s1_cascade_match_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "연쇄 매칭!",
                        message = "위에서 낙하한 블록이\n<color=#FFE066><b>또 매칭</b></color>되었어요!\n\n연쇄가 많을수록 <color=#FF8844><b>보너스 점수</b></color>를 얻어요.",
                        pauseGame = true,
                        waitForEventBeforeShow = TutorialWaitEvent.MatchHighlightPause,
                        waitTimeoutOverride = 3f,             // 3초 이내 연쇄 매칭 발생 시만 표시
                        waitTimeoutContinueOnSkip = true      // 타임아웃 시 Dialog 건너뜀
                    },
                    // ── Step 9: 전체 캐스케이드 완료 대기 ──
                    new TutorialStep
                    {
                        id = "s1_wait_cascade_1",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.CascadeComplete
                    },
                    // ── Step 8: 점수 UI 하이라이트 ──
                    new TutorialStep
                    {
                        id = "s1_score_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "점수 게이지",
                        message = "상단 중앙에 <color=#FFE066><b>점수</b></color>가 표시됩니다.\n연쇄가 많을수록 높은 점수!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightScreenPos = new Vector2(0f, 920f),
                        highlightRadius = 120f
                    },
                    // ── Step 9: 이동 횟수 UI 하이라이트 ──
                    new TutorialStep
                    {
                        id = "s1_turns_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "이동 횟수",
                        message = "왼쪽 위 숫자는 <color=#B8F0FF><b>남은 이동 횟수</b></color>.\n매칭 성공 시마다 <b>-1</b> 감소해요.\n\n이동 횟수가 0이 되기 전에 미션 완료!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "HUD_TurnFrame",
                        highlightPadding = 14f,
                        highlightScreenPos = new Vector2(-200f, 915f),
                        highlightRadius = 100f
                    },

                    // ═══════════════════════════════════════════════════════════
                    //  [Section 2] 240도 회전 강제 탭 — 120도에 매칭 없으면 자동 240도 시도
                    // ═══════════════════════════════════════════════════════════

                    // ── Step 10: 240도 회전 설명 ──
                    new TutorialStep
                    {
                        id = "s1_240_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "240도 회전",
                        message = "<color=#FFE066><b>120도</b></color>에서 매칭이 없으면\n자동으로 <color=#FFE066><b>240도</b></color>까지 더 돌려봅니다!\n\n다시 한 번 밝은 블록을 탭해 보세요.",
                        pauseGame = true
                    },
                    // ── Step 11: 강제 탭 #2 (자동 240도 회전 시나리오) ──
                    //   리필된 블록의 무작위성으로 인해 실제로 120/240 중 어디서 매칭이 나올지는 상황마다 다름.
                    //   어느 쪽이든 RotationComplete 이벤트로 진행.
                    new TutorialStep
                    {
                        id = "s1_force_rotate_240",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "한 번 더!",
                        message = "밝은 삼각형 중심을 다시 탭!",
                        showFingerGuide = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 블록 중심을 탭하여 주세요!",
                        allowedCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0),
                            new HexCoord(1, 0),
                            new HexCoord(0, 1)
                        }
                    },
                    // ── Step 12: 회전 완료 대기 ──
                    new TutorialStep
                    {
                        id = "s1_wait_rotation_2",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.RotationComplete
                    },
                    // ── Step 13: 240도 설명 + 매칭 시 추가 진행 ──
                    new TutorialStep
                    {
                        id = "s1_240_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "자동 회전!",
                        message = "보세요! <color=#FFE066>120도</color>에서 매칭이 없으면\n자동으로 <color=#FFE066>240도</color>까지 돌려\n매칭을 찾으려 합니다.",
                        pauseGame = true
                    },

                    // ═══════════════════════════════════════════════════════════
                    //  [Section 3] 360도 회전 설명 — 매칭 없으면 원위치 + 이동횟수 소모 없음
                    // ═══════════════════════════════════════════════════════════

                    // ── Step 14: 360도 규칙 설명 (매우 중요) ──
                    new TutorialStep
                    {
                        id = "s1_360_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "360도 회전 규칙",
                        message = "<color=#FFE066>120도</color>와 <color=#FFE066>240도</color> 모두\n매칭이 없으면 <color=#B8F0FF><b>360도 돌아 원위치</b></color>!\n\n원위치로 돌아오기 때문에\n<color=#6EB4FF><b>이동 횟수도 소모되지 않아요!</b></color>\n부담 없이 자유롭게 회전을 시도해 보세요.",
                        pauseGame = true
                    },

                    // ═══════════════════════════════════════════════════════════
                    //  [Section 4] 자유 플레이 — 미션 완수 유도
                    // ═══════════════════════════════════════════════════════════

                    // ── Step 15: 자유 플레이 힌트 ──
                    new TutorialStep
                    {
                        id = "s1_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "남은 빨강을 모두 정화한 뒤 초록 미션에 도전!",
                        hintDuration = 5f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 2: 몽둥이 고블린 첫 등장 + 설명 (2026-04-25)
        //   - 필드: 반경 2 (19블록), 소환 영역 2줄
        //   - 미션: 몽둥이 고블린 3마리 처치
        //   - 튜토리얼: 고블린 외형 + 낙하 데미지 메커니즘 설명
        // ============================================================
        private static TutorialSequence GetStage2_GoblinIntro()
        {
            return new TutorialSequence
            {
                sequenceId = "stage2_goblin_intro",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 2,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 흙더미 첫 등장 — 정령마을 보호
                    new TutorialStep
                    {
                        id = "s2_dirt_first_warning",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "흙더미 출현!",
                        message = "<color=#7FE3FF><b>정령마을</b></color>의 외곽 블록이\n<color=#A06030><b>흙더미</b></color>로 뒤덮였어요!\n흙을 정화해 마을의 빛을 되찾아 주세요.",
                        pauseGame = true
                    },
                    // Step 1: 흙더미 메커니즘
                    new TutorialStep
                    {
                        id = "s2_dirt_mound_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "흙더미 장애물",
                        message = "<color=#A06030><b>흙더미 블록</b></color>은\n<color=#FF8844>회전·낙하 불가</color>예요.\n\n중앙 7블록을 회전시켜\n흙더미 블록과 <color=#B8F0FF>같은 색 3매칭</color>을 만들면\n흙더미가 한 단계씩 깎입니다.",
                        pauseGame = true
                    },
                    // Step 2: 흙더미 미션 설명 (미션 UI 하이라이트)
                    new TutorialStep
                    {
                        id = "s2_dirt_mission",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "이번 미션",
                        message = "외곽 라인의 <color=#A06030><b>흙더미 12개</b></color>를 모두 정화하세요!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "GameMissionUI",
                        highlightPadding = 20f,
                        highlightScreenPos = new Vector2(-370f, 810f),
                        highlightRadius = 140f
                    },
                    // Step 3: 자유 플레이
                    new TutorialStep
                    {
                        id = "s2_dirt_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "흙더미를 매칭으로 정화하세요!",
                        hintDuration = 5f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 3 (2026-04-27 재설계): 몽둥이 고블린 첫 등장 + 공략 자세히
        //   - 필드 반경 2 (19블록), 흙더미 없음
        //   - 미션: 몽둥이 고블린 2 → 3 순차 웨이브
        //   - 튜토리얼: 몽둥이 외형/특성 + 직접 인접 매칭 공략 + 블록 낙하 데미지 공략
        // ============================================================
        private static TutorialSequence GetStage3_GoblinIntro()
        {
            return new TutorialSequence
            {
                sequenceId = "stage3_goblin_intro",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 3,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 고블린 첫 등장 — 정령마을 보호
                    new TutorialStep
                    {
                        id = "s3_goblin_first_warning",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "고블린이 나타났다!",
                        message = "<color=#E06060><b>몽둥이 고블린</b></color>이\n처음으로 모습을 드러냈어요!\n\n<color=#7FE3FF><b>정령마을</b></color>을 침입자로부터\n반드시 <color=#FFD060><b>지켜내야</b></color> 합니다!",
                        pauseGame = true
                    },
                    // Step 1: 몽둥이 고블린 외형·특성 소개
                    new TutorialStep
                    {
                        id = "s3_goblin_appear",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "몽둥이 고블린",
                        message = "낡은 <color=#A06030><b>몽둥이</b></color>를 휘두르며\n매 턴 <color=#FFE066><b>한 칸씩</b></color> 아래로 내려와\n블록을 두드려 <color=#B8F0FF>정령의 빛</color>을 빼앗아갑니다.\n\n<color=#FF8844>2회 두들겨 맞은 블록</color>은\n회색 <b>껍질 블록</b>이 되어 매칭 불가!",
                        pauseGame = true
                    },
                    // Step 2: 공략 1 — 직접 인접 매칭
                    new TutorialStep
                    {
                        id = "s3_strategy_adjacent_match",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "공략 1: 직접 / 인접 매칭",
                        message = "<color=#FFD060><b>직접 매칭</b></color>:\n<color=#FF4040><b>고블린이 서 있는 자리</b></color>의 블록이\n다른 블록과 매칭되면 <color=#FF8844>1 데미지</color> + 방패 1단 깎임!\n\n<color=#FFD060><b>인접 매칭</b></color>:\n<color=#8EE080>고블린 바로 옆 블록</color>이 매칭되면\n옆에 있는 고블린에게 <color=#FF8844>1 데미지</color>.\n\n<color=#B8F0FF>매칭 한 번이 여러 고블린에 동시 피해</color>를\n줄 수 있어요 — 고블린이 모인 곳에서 매칭하세요!",
                        pauseGame = true
                    },
                    // Step 3: 공략 2 — 블록 낙하 데미지
                    new TutorialStep
                    {
                        id = "s3_strategy_fall_damage",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "공략 2: 블록 낙하 데미지",
                        message = "매칭으로 블록이 정화되면\n<color=#FFE066><b>위쪽 블록이 낙하</b></color>해요.\n\n낙하하는 블록이 고블린의 Y좌표를\n<color=#FF8844><b>통과할 때마다 1 데미지</b></color>!\n\n<color=#FFD060>고블린 위 같은 컬럼에서 매칭</color>하면\n낙하하는 블록 1개당 1 데미지가\n누적되어 한 번에 처치 가능합니다.",
                        pauseGame = true
                    },
                    // Step 4: 미션 안내 (미션 UI 하이라이트)
                    new TutorialStep
                    {
                        id = "s3_goblin_mission",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "이번 미션",
                        message = "먼저 <color=#E06060><b>몽둥이 고블린 2마리</b></color>를 처치하세요!\n그 후 <color=#E06060><b>3마리가 추가로</b></color> 등장합니다.\n\n총 <color=#FFE066><b>5마리</b></color>를 모두 처치하면 클리어!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "GameMissionUI",
                        highlightPadding = 20f,
                        highlightScreenPos = new Vector2(-370f, 810f),
                        highlightRadius = 140f
                    },
                    // Step 5: 자유 플레이
                    new TutorialStep
                    {
                        id = "s3_goblin_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "인접 매칭과 낙하 데미지로 고블린을 처치!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 4: 드릴 특수블록 소개 + 해금 (인터랙티브)
        // ============================================================
        private static TutorialSequence GetStage4_DrillIntro()
        {
            return new TutorialSequence
            {
                sequenceId = "stage4_drill",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 4,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 드릴 소개
                    new TutorialStep
                    {
                        id = "s4_drill_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새로운 힘: 드릴!",
                        message = "이번 스테이지부터 특수 블록을\n사용할 수 있어요!\n직접 만들어 볼까요?",
                        pauseGame = true
                    },
                    // Step 1: 드릴 패턴 하이라이트 (다이아몬드)
                    new TutorialStep
                    {
                        id = "s4_drill_pattern",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "드릴 생성 방법",
                        message = "같은 색 4개가 뭉쳐서\n다이아(마름모) 모양을 이루면\n드릴이 생성됩니다!\n파란 블록을 잘 보세요.",
                        pauseGame = true,
                        // ★ 회전 전 Blue 4블록 (드릴 재료) 현재 위치:
                        //   (0,0)·(0,1) = 회전 클러스터 내 Blue, (1,-1)·(2,-1) = 고정 Blue
                        //   회전 후에는 (0,0)(1,0)(1,-1)(2,-1)에서 다이아몬드 완성됨
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0), new HexCoord(0, 1),
                            new HexCoord(1,-1), new HexCoord(2,-1)
                        }
                    },
                    // Step 2: 강제 회전 유도
                    // ※ RotationComplete 대기 스텝을 제거 — Step 2 → Step 3로 바로 넘어가
                    //   MatchHighlightPause를 먼저 대기해야 BRS의 매칭 하이라이트 Pause Hook을 놓치지 않음.
                    //   (회전은 allowedCoords로 제한된 상태에서 백그라운드로 진행되며,
                    //    플레이어 회전 → BRS 매칭 감지 → Pause Hook 1 시점에 Step 3가 이미 대기 중)
                    new TutorialStep
                    {
                        id = "s4_force_rotate",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "회전",
                        message = "밝은 삼각형 중심을 터치하여 회전시키세요!",
                        showFingerGuide = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 블록 중심을 탭하여 주세요!",   // 다른 곳 탭 시 토스트
                        allowedCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0), new HexCoord(0, 1), new HexCoord(1, 0)
                        }
                    },
                    // Step 3: 매칭 하이라이트 pause 대기 (4개 다이아 매칭 감지 직후, 삭제 전 pause)
                    new TutorialStep
                    {
                        id = "s4_wait_match_pause",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.MatchHighlightPause
                    },
                    // Step 5: 매칭 설명
                    new TutorialStep
                    {
                        id = "s4_match_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "다이아 매칭!",
                        message = "4개가 다이아 모양으로 매칭되었어요!\n이 모양이 드릴이 됩니다.",
                        pauseGame = true,
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0), new HexCoord(1, 0),
                            new HexCoord(1,-1), new HexCoord(2,-1)
                        }
                    },
                    // Step 6: 드릴 완성 설명 (원자적 처리: race 없음)
                    new TutorialStep
                    {
                        id = "s4_drill_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "드릴 완성!",
                        message = "드릴이 만들어졌어요!\n드릴이 향한 방향으로\n드릴이 양쪽으로 발사됩니다.",
                        pauseGame = true,
                        waitForEventBeforeShow = TutorialWaitEvent.DrillCreatedPause
                    },
                    // Step 7: 드릴 클릭 강제 (사용법 학습)
                    //   - 기존 하단 Dialog Panel UI(스왑/라인 튜토리얼과 동일 스타일)로 지시 표시
                    //   - 다른 곳 탭 시 토스트 안내
                    //   - 탭할 때까지 세션 종료 금지 (다음 WaitForEvent.DrillActivated 타임아웃 3600초)
                    //   ※ characterName 설정 + forceDrillClick=true → HandleForcedActionStep이
                    //     자동으로 message/title을 ShowDialog에 전달 (BottomHint 우회)
                    new TutorialStep
                    {
                        id = "s4_force_drill_click",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "드릴 발사",
                        message = "밝은 드릴을 터치해 발사해 보세요!\n직선 방향으로 블록을 관통합니다!",
                        showFingerGuide = true,
                        forceDrillClick = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 드릴을 터치해 주세요!"
                    },
                    // Step 8: 드릴 발동 대기
                    new TutorialStep
                    {
                        id = "s4_wait_drill_activated",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.DrillActivated
                    },
                    // Step 9: 드릴 능력 설명
                    new TutorialStep
                    {
                        id = "s4_drill_effect",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "드릴 능력",
                        message = "마나 5를 써서 드릴 방향의 블록을\n파괴하고 몬스터에게 데미지를 줍니다!",
                        pauseGame = true
                    },
                    // Step 10: 마나 포인트(MP) 설명 — 첫 특수 블록이므로 한 번 짚고 넘어감
                    //         말풍선이 MP 게이지 옆으로 몰딩 이동 + 게이지 하이라이트
                    new TutorialStep
                    {
                        id = "s4_mp_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "마나 포인트",
                        message = "특수 블록을 클릭하면\n이 게이지의 마나가 소모돼요.\n\n· 마나가 부족하면 클릭 사용 불가\n· 매칭으로 발동은 언제든 가능\n\n마나를 아껴 결정적인 순간에 쓰세요!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "MPGaugeUI",
                        highlightPadding = 18f
                    },
                    // Step 11: 자유 플레이
                    new TutorialStep
                    {
                        id = "s4_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "4개 매칭으로 드릴을 만들어 보세요!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 6: 망치 아이템 소개 + 해금
        // ============================================================
        private static TutorialSequence GetTut_Hammer()
        {
            return new TutorialSequence
            {
                sequenceId = "tut_hammer",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 6,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 소개 — 망치 버튼을 처음부터 밝게 비춤 (이전 잔류 하이라이트 자동 교체)
                    new TutorialStep
                    {
                        id = "tut_hammer_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새 아이템: 망치!",
                        message = "망치를 사용하면 블록 하나를\n파괴할 수 있어요.\n\n오른쪽 아래에 망치 게이지가\n준비되어 있어요.",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "HammerButton",
                        highlightPadding = 18f
                    },
                    // Step 1: 게이지 시스템 설명 — 망치 버튼 하이라이트 유지
                    new TutorialStep
                    {
                        id = "tut_hammer_gauge_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "망치 게이지",
                        message = "망치는 빨간색 블록이 제거될 때마다\n게이지가 조금씩 차오릅니다.\n\n게이지가 가득 차면 망치를\n사용할 수 있어요!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "HammerButton",
                        highlightPadding = 18f
                    },
                    // Step 2: 자동 충전 안내 — 망치 버튼 하이라이트 유지
                    new TutorialStep
                    {
                        id = "tut_hammer_auto_charge",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "게이지 충전",
                        message = "튜토리얼을 위해 바로 채워드릴게요!\n\n게이지가 차오르는 것을 봐주세요.",
                        pauseGame = true,
                        autoAdvanceDelay = 2f,
                        useHighlight = true,
                        highlightTargetName = "HammerButton",
                        highlightPadding = 18f
                    },
                    // Step 3: 망치 버튼 강제 클릭 — 말풍선 + 화면 딤 + 스포트라이트 + 손가락 + 토스트
                    //         (기존 tut_hammer_charged Dialog와 tut_hammer_force_click ForcedAction을 통합)
                    //         탭으로 넘기지 못하며 망치 버튼 클릭으로만 진행
                    new TutorialStep
                    {
                        id = "tut_hammer_force_click",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        message = "", // TutorialManager가 말풍선으로 메시지 표시
                        showFingerGuide = false,
                        pauseGame = false
                    },
                    // Step 5: 망치 버튼 클릭 대기 (UseReady 상태 전환까지)
                    new TutorialStep
                    {
                        id = "tut_hammer_wait_activated",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.HammerActivated
                    },
                    // Step 5: 망치 활성화 완료 + 타겟 블록 클릭 강제
                    //         TutorialManager가 id 감지 시 블록 Glow + 타겟 블록 위에 말풍선 표시
                    //         HammerItem이 해당 좌표 외 클릭 시 토스트 "타겟 블록을 클릭해주세요!"
                    new TutorialStep
                    {
                        id = "tut_hammer_target_block",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        message = "", // TutorialManager가 말풍선으로 메시지 표시
                        showFingerGuide = false,
                        pauseGame = false
                    },
                    // Step 6: 망치 사용 대기 — 플레이어가 블록 터치로 실제 파괴할 때까지
                    new TutorialStep
                    {
                        id = "tut_hammer_wait_used",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.HammerUsed
                    },
                    // Step 5: 망치 능력 설명 — 낙하 드릴 효과 + 이동 횟수 미소모
                    new TutorialStep
                    {
                        id = "tut_hammer_effect",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "망치 능력",
                        message = "마나 12로 이동 횟수 없이\n블록 하나를 즉시 파괴!\n낙하 변화로 전략을 구상하세요.\n게이지가 차면 언제든 사용 가능!",
                        pauseGame = true
                    },
                    // Step 6: 자유 플레이
                    new TutorialStep
                    {
                        id = "tut_hammer_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "망치와 매칭을 조합해 드릴을 노려보세요!",
                        hintDuration = 5f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 8: 폭탄 특수블록 소개 + 해금 (인터랙티브)
        // ============================================================
        private static TutorialSequence GetTut_Bomb()
        {
            return new TutorialSequence
            {
                sequenceId = "tut_bomb",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 8,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 폭탄 소개
                    new TutorialStep
                    {
                        id = "tut_bomb_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새로운 힘: 폭탄!",
                        message = "더 강력한 특수 블록이 해금되었어요!\n직접 만들어 볼까요?",
                        pauseGame = true,
                        iconSpecialType = SpecialBlockType.Bomb
                    },
                    // Step 1: 폭탄 패턴 하이라이트 — 회전 전 파란 블록 5개의 **현재 위치**를 표시
                    //   preset: (1,0), (0,1), (-1,0), (-1,1), (0,-1) 모두 Blue
                    //   (회전 후에는 (0,0)이 추가 Blue가 되어 중심 뭉침 완성)
                    new TutorialStep
                    {
                        id = "tut_bomb_pattern",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "폭탄 생성방법",
                        message = "5개 블록을 한번에 뭉쳐서 매칭하면\n폭탄 💣이 생성됩니다!\n파란 블록의 위를 잘 보세요.",
                        pauseGame = true,
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord( 1, 0), new HexCoord( 0, 1),
                            new HexCoord(-1, 0), new HexCoord(-1, 1),
                            new HexCoord( 0,-1)
                        }
                    },
                    // Step 2: 강제 회전 유도 — 밝은 블록 3개만 탭 가능
                    //   다른 곳 탭 시 TutorialManager에서 토스트로 안내
                    new TutorialStep
                    {
                        id = "tut_bomb_force_rotate",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "회전",
                        message = "밝은 블록을 탭하여 회전해 보세요!",
                        showFingerGuide = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 블록 중심을 탭하여 주세요!",
                        allowedCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0), new HexCoord(0, 1), new HexCoord(1, 0)
                        }
                    },
                    // Step 3: 회전 완료 대기
                    new TutorialStep
                    {
                        id = "tut_bomb_wait_rotation",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.RotationComplete
                    },
                    // Step 4: 매칭 하이라이트 pause 대기
                    new TutorialStep
                    {
                        id = "tut_bomb_wait_match_pause",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.MatchHighlightPause
                    },
                    // Step 5: 매칭 설명
                    new TutorialStep
                    {
                        id = "tut_bomb_match_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "5개 매칭!",
                        message = "5개가 한 번에 매칭되었어요!\n이 뭉침이 폭탄이 됩니다.",
                        pauseGame = true,
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord(-1, 0), new HexCoord(0, 0),
                            new HexCoord(-1, 1), new HexCoord(0, 1),
                            new HexCoord(0, -1)
                        }
                    },
                    // Step 6: 폭탄 완성 + 사용법 + 마나 소모 설명
                    //   (원자적 처리: BombCreatedPause 이벤트가 발생한 후에 대화창 표시 → race 없음)
                    new TutorialStep
                    {
                        id = "tut_bomb_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "폭탄 완성!",
                        message = "폭탄을 탭하면 마나 6을 써서\n2칸 범위 블록을 모두 제거하고\n일반 몬스터를 폭발 범위 밖으로 넉백시킵니다.",
                        pauseGame = true,
                        iconSpecialType = SpecialBlockType.Bomb,
                        waitForEventBeforeShow = TutorialWaitEvent.BombCreatedPause
                    },
                    // Step 8: 자유 플레이 힌트
                    new TutorialStep
                    {
                        id = "tut_bomb_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "폭탄을 터치해 폭발시켜 보세요!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // 스테이지 11 드릴 튜토리얼은 레벨 디자인 변경(Stage 11 = 갑옷 고블린 첫 등장)으로 제거됨.
        //   드릴은 스테이지 3에서 이미 소개 완료.

        // ============================================================
        // 스테이지 26: 역회전 아이템 소개 + 해금 (망치/스왑/라인과 동일 인터랙티브 구조)
        //   intro → explain → force_click → wait_activated → force_rotate
        //   → wait_rotation → effect → freeplay
        // ============================================================
        private static TutorialSequence GetTut_Reverse()
        {
            return new TutorialSequence
            {
                sequenceId = "tut_reverse",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 26,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 소개 — 역회전 버튼 하이라이트
                    new TutorialStep
                    {
                        id = "tut_reverse_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새 아이템: 역회전!",
                        message = "새로운 아이템이 해금되었어요!\n화면 오른쪽 아래에 역회전 버튼이\n나타났습니다.",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "ReverseRotationButton",
                        highlightPadding = 18f
                    },
                    // Step 1: 사용법 설명 — 버튼 하이라이트 유지
                    new TutorialStep
                    {
                        id = "tut_reverse_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "역회전 사용법",
                        message = "역회전 버튼을 누르면\n다음 회전이 반대 방향으로 한 번 적용돼요!\n\n지금 보드에는 정회전이면 단순 3매칭이지만\n역회전이면 드릴이 만들어지는 배치가 있어요.\n직접 체험해 보세요!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "ReverseRotationButton",
                        highlightPadding = 18f
                    },
                    // Step 2: 역회전 버튼 강제 클릭 (망치/스왑/라인 동일 패턴 — id 기반 핸들러)
                    //   화면 딤 + 스포트라이트 + 손가락 가이드 + 잘못된 곳 토스트
                    new TutorialStep
                    {
                        id = "tut_reverse_force_click",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        message = "", // TutorialManager가 ShowHighlightDialog로 표시
                        showFingerGuide = false,
                        pauseGame = false
                    },
                    // Step 3: 역회전 버튼 클릭 대기
                    new TutorialStep
                    {
                        id = "tut_reverse_wait_activated",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.ReverseActivated
                    },
                    // Step 4: 강제 회전 유도 — 사전 배치된 클러스터(0,0)(1,0)(0,1)만 허용
                    //   reverse 활성 상태에서 CCW 회전 시 드릴 4-다이아몬드 생성됨을 체험
                    //   다른 곳 탭 시 토스트로 안내, 빈 공간/버튼 탭으로도 비활성화 안 됨
                    new TutorialStep
                    {
                        id = "tut_reverse_force_rotate",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "역회전 적용",
                        message = "밝은 삼각형 중심을 터치해\n역회전을 적용하세요!",
                        showFingerGuide = true,
                        pauseGame = false,
                        allowedCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0), new HexCoord(0, 1), new HexCoord(1, 0)
                        },
                        blockedToastMessage = "밝은 곳의 블록을 역회전 시켜주세요!"
                    },
                    // Step 5: 회전 완료 대기
                    new TutorialStep
                    {
                        id = "tut_reverse_wait_rotation",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.RotationComplete
                    },
                    // Step 6: 효과 마무리 설명 — 정회전이었으면 단순 매칭, 역회전이라 드릴 생성됨을 강조
                    new TutorialStep
                    {
                        id = "tut_reverse_effect",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "역회전 능력",
                        message = "마나 15로 역회전을 적용해 드릴이 생성됐어요!\n그냥 회전했다면 단순 3매칭에 그쳤을 거예요.\n\n매칭이 막힐 때나 특수 블록 패턴을\n역방향으로 만들어야 할 때 활용하세요.",
                        pauseGame = true
                    },
                    // Step 7: 자유 플레이
                    new TutorialStep
                    {
                        id = "tut_reverse_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "역회전을 활용해 새로운 매칭을 만들어 보세요!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // ============================================================
        // (구) 스테이지 14 몬스터 공략 가이드 — 첫 등장 시 개별 hint로 분산 (아래 GetHint_XxxGoblin 등 참조)
        // ============================================================

        // ============================================================
        // 스테이지 11: 스왑 아이템 소개 + 해금 (망치와 동일 구조)
        // ============================================================
        private static TutorialSequence GetTut_Swap()
        {
            return new TutorialSequence
            {
                sequenceId = "tut_swap",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 11,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 소개 — 스왑 버튼 하이라이트
                    new TutorialStep
                    {
                        id = "tut_swap_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새 아이템: 스왑!",
                        message = "두 블록의 위치를 바꾸는\n스왑 아이템이 해금됐어요.\n\n오른쪽 아래 스왑 버튼을 확인하세요.",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "SwapButton",
                        highlightPadding = 18f
                    },
                    // Step 1: 사용법 설명 — 스왑 버튼 하이라이트 유지
                    new TutorialStep
                    {
                        id = "tut_swap_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "스왑 사용법",
                        message = "스왑은 게이지가 찼을 때 사용할 수 있어요.\n게이지는 블록이 제거될수록\n조금씩 차오릅니다.",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "SwapButton",
                        highlightPadding = 18f
                    },
                    // Step 2: 게이지 충전 조건 안내 — 녹색 블록을 밝게 표시
                    new TutorialStep
                    {
                        id = "tut_swap_gauge_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "스왑 게이지",
                        message = "스왑 게이지는 녹색 블록이 제거될 때마다\n차오릅니다.\n\n게이지가 가득 차면 스왑을 사용할 수 있어요!",
                        pauseGame = true
                    },
                    // Step 3: 자동 충전 안내 — 게이지가 차오르는 과정 시각화
                    new TutorialStep
                    {
                        id = "tut_swap_auto_charge",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "게이지 충전",
                        message = "튜토리얼을 위해 바로 채워드릴게요!\n\n게이지가 차오르는 것을 봐주세요.",
                        pauseGame = true,
                        autoAdvanceDelay = 2f,
                        useHighlight = true,
                        highlightTargetName = "SwapButton",
                        highlightPadding = 18f
                    },
                    // Step 4: 스왑 버튼 강제 클릭 — 말풍선 + 딤 + 스포트라이트 + 토스트 (망치와 동일)
                    new TutorialStep
                    {
                        id = "tut_swap_force_click",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        message = "",
                        showFingerGuide = false,
                        pauseGame = false
                    },
                    // Step 5: 스왑 활성화 대기
                    new TutorialStep
                    {
                        id = "tut_swap_wait_activated",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.SwapActivated
                    },
                    // Step 6: 스왑 타겟 드래그 강제 — 2 블록 Glow + 손가락 드래그 애니메이션
                    //         TutorialManager가 id 감지 시 pendingSwapSourceCoord/DestCoord 사용
                    //         SwapItem이 허용 좌표 외 선택 시 토스트 "밝게 표시된 블록을 드래그해 주세요!"
                    new TutorialStep
                    {
                        id = "tut_swap_target_drag",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        message = "",
                        showFingerGuide = false,
                        pauseGame = false
                    },
                    // Step 7: 스왑 완료 대기
                    new TutorialStep
                    {
                        id = "tut_swap_wait_used",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.SwapUsed
                    },
                    // Step 8: 효과 설명 — 스왑으로 특수블록 유도 가능 + 이동횟수 미소모 안내
                    new TutorialStep
                    {
                        id = "tut_swap_effect",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "스왑 완료!",
                        message = "스왑은 이동 횟수 없이 마나 13으로\n특수 블록을 만들기 좋은 구도를 만들어요.\n전략적으로 활용해 보세요!",
                        pauseGame = true
                    },
                    // Step 9: 자유 플레이 (회전으로 폭탄 완성 유도)
                    new TutorialStep
                    {
                        id = "tut_swap_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "회전으로 폭탄을 완성해 보세요!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 20: 드론 특수블록 소개 + 해금 (인터랙티브)
        //   생성 + 사용까지 전체 플로우 — 스왑/라인 튜토리얼과 유사한 force_click+wait_used 구조:
        //   intro → pattern → force_rotate → wait_rotation → wait_match_pause → match_explain
        //   → explain(생성 직후 대화) → force_click(드론 강제 탭) → wait_activated
        //   → effect(능력 설명) → freeplay
        // ============================================================
        private static TutorialSequence GetTut_Drone()
        {
            return new TutorialSequence
            {
                sequenceId = "tut_drone",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 20,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 드론 소개
                    new TutorialStep
                    {
                        id = "tut_drone_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새로운 힘: 드론!",
                        message = "자동으로 전장을 분석해 공격하는\n스마트 특수 블록이 해금됐어요!\n직접 만들어 볼까요?",
                        pauseGame = true,
                        iconSpecialType = SpecialBlockType.Drone
                    },
                    // Step 1: 드론 패턴 하이라이트 — 회전 전 파란 블록 5개의 현재 위치 표시
                    new TutorialStep
                    {
                        id = "tut_drone_pattern",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "드론 생성 방법",
                        message = "5개 블록을 나비 모양으로 매칭하면\n드론 🛸이 생성됩니다!\n파란 블록의 위를 잘 보세요.",
                        pauseGame = true,
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord( 1, 0), new HexCoord( 0, 1),
                            new HexCoord( 1,-1), new HexCoord( 0,-1),
                            new HexCoord(-1, 1)
                        }
                    },
                    // Step 2: 강제 회전 유도 — 밝은 블록 3개만 탭 가능
                    new TutorialStep
                    {
                        id = "tut_drone_force_rotate",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "회전",
                        message = "밝은 블록을 탭하여 회전해 보세요!",
                        showFingerGuide = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 블록 중심을 탭하여 주세요!",
                        allowedCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0), new HexCoord(0, 1), new HexCoord(1, 0)
                        }
                    },
                    // Step 3: 회전 완료 대기
                    new TutorialStep
                    {
                        id = "tut_drone_wait_rotation",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.RotationComplete
                    },
                    // Step 4: 매칭 하이라이트 pause 대기
                    new TutorialStep
                    {
                        id = "tut_drone_wait_match_pause",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.MatchHighlightPause
                    },
                    // Step 5: 매칭 설명 — 회전 후 나비 패턴 5개 Blue 강조
                    new TutorialStep
                    {
                        id = "tut_drone_match_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "나비 매칭!",
                        message = "5개가 나비 모양으로 매칭되었어요!\n이 패턴이 드론이 됩니다.",
                        pauseGame = true,
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0),                          // 중심
                            new HexCoord(1, -1), new HexCoord(0, -1),    // 날개 A
                            new HexCoord(-1, 1), new HexCoord(0, 1)      // 날개 B
                        }
                    },
                    // Step 6: 드론 생성 완료 인사 (DroneCreatedPause 이벤트 발생 후 표시)
                    new TutorialStep
                    {
                        id = "tut_drone_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "드론 완성!",
                        message = "드론이 만들어졌어요!\n이제 드론을 탭해서\n자동 공격을 발동시켜 보세요.",
                        pauseGame = true,
                        iconSpecialType = SpecialBlockType.Drone,
                        waitForEventBeforeShow = TutorialWaitEvent.DroneCreatedPause
                    },
                    // Step 7: 드론 강제 클릭 — pendingSpecialCoord(드론 좌표)만 탭 허용
                    //   - 하단 BottomHint UI로 지시 표시
                    //   - 다른 곳 탭 시 토스트 안내
                    //   - 탭할 때까지 세션 종료 금지 (다음 WaitForEvent.DroneActivated 타임아웃 3600초)
                    new TutorialStep
                    {
                        id = "tut_drone_force_click",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "드론 발사",
                        message = "밝은 드론을 터치해 발사해 보세요!",
                        showFingerGuide = true,
                        forceSpecialClick = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 드론을 터치해 주세요!"
                    },
                    // Step 8: 드론 발동 대기
                    new TutorialStep
                    {
                        id = "tut_drone_wait_activated",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.DroneActivated
                    },
                    // Step 9: 드론 능력 설명 + 마나 소모 안내
                    new TutorialStep
                    {
                        id = "tut_drone_effect",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "드론 능력",
                        message = "드론은 마나 7을 써서 전장을 분석해\n가장 효과적인 타겟을 자동으로 공격합니다.\n몬스터가 있으면 몬스터, 없으면 미션 블록을 우선 처리해요.",
                        pauseGame = true
                    },
                    // Step 10: 자유 플레이 힌트
                    new TutorialStep
                    {
                        id = "tut_drone_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "나비 모양 매칭으로 드론을 또 만들어 보세요!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 17: 라인(SSD) 아이템 — 망치/스왑 튜토리얼과 동일 구조
        //   intro → explain → gauge_explain(보라 블록) → auto_charge
        //   → force_click → wait_activated → target_drag(경로 Glow+드래그)
        //   → wait_used → effect → freeplay
        // ============================================================
        private static TutorialSequence GetTut_LineDraw()
        {
            return new TutorialSequence
            {
                sequenceId = "tut_linedraw",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 17,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 소개 — 라인 버튼 하이라이트
                    new TutorialStep
                    {
                        id = "tut_linedraw_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새 아이템: 라인!",
                        message = "경로를 그려 블록을 파괴하는\n라인 아이템이 해금됐어요.\n\n오른쪽 아래 라인 버튼을 확인하세요.",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "LineDrawButton",
                        highlightPadding = 18f
                    },
                    // Step 1: 사용법 설명 — 라인 버튼 하이라이트 유지
                    new TutorialStep
                    {
                        id = "tut_linedraw_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "라인 사용법",
                        message = "라인은 게이지가 찼을 때 사용할 수 있어요.\n게이지는 블록이 제거될수록\n조금씩 차오릅니다.",
                        pauseGame = true,
                        useHighlight = true,
                        highlightTargetName = "LineDrawButton",
                        highlightPadding = 18f
                    },
                    // Step 2: 게이지 충전 조건 안내 — 보라 블록 Glow
                    new TutorialStep
                    {
                        id = "tut_linedraw_gauge_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "라인 게이지",
                        message = "라인 게이지는 보라색 블록이 제거될 때마다\n차오릅니다.\n\n게이지가 가득 차면 라인을 사용할 수 있어요!",
                        pauseGame = true
                    },
                    // Step 3: 자동 충전 안내 — 게이지가 차오르는 과정 시각화
                    new TutorialStep
                    {
                        id = "tut_linedraw_auto_charge",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "게이지 충전",
                        message = "튜토리얼을 위해 바로 채워드릴게요!\n\n게이지가 차오르는 것을 봐주세요.",
                        pauseGame = true,
                        autoAdvanceDelay = 2f,
                        useHighlight = true,
                        highlightTargetName = "LineDrawButton",
                        highlightPadding = 18f
                    },
                    // Step 4: 라인 버튼 강제 클릭 — 말풍선 + 딤 + 스포트라이트 + 토스트
                    new TutorialStep
                    {
                        id = "tut_linedraw_force_click",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        message = "",
                        showFingerGuide = false,
                        pauseGame = false
                    },
                    // Step 5: 라인 활성화 대기
                    new TutorialStep
                    {
                        id = "tut_linedraw_wait_activated",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.LineDrawActivated
                    },
                    // Step 6: 드래그 경로 강제 — 경로 블록 Glow + 연결선 + 손가락 드래그 애니메이션
                    //         TutorialManager가 id 감지 시 pendingLineDrawPath 를 Glow 처리
                    new TutorialStep
                    {
                        id = "tut_linedraw_target_drag",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        message = "",
                        showFingerGuide = false,
                        pauseGame = false
                    },
                    // Step 7: 라인 사용 완료 대기
                    new TutorialStep
                    {
                        id = "tut_linedraw_wait_used",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.LineDrawUsed
                    },
                    // Step 8: 효과 설명 (아이콘 없이 왼쪽 정렬)
                    new TutorialStep
                    {
                        id = "tut_linedraw_effect",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "라인 완료!",
                        message = "라인은 마나 14를 써서 긴 경로의\n블록을 한 번에 제거합니다.\n이동 횟수도 소모되지 않아요!",
                        pauseGame = true
                    },
                    // Step 9: 자유 플레이
                    new TutorialStep
                    {
                        id = "tut_linedraw_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "라인으로 같은 색 블록을 이어 제거해 보세요!",
                        hintDuration = 5f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 23: 타겟 레이저 (Rainbow/XBlock 통합) 소개 + 해금
        //   이전 Rainbow 튜토리얼 내용을 그대로 가져와 XBlock 해금 시점에 진행.
        //   링 매칭 → XBlock 생성 (XBlock이 해금된 상태이므로 fallback 없이 즉시 생성).
        // ============================================================
        private static TutorialSequence GetTut_TargetLaser()
        {
            return new TutorialSequence
            {
                sequenceId = "tut_targetlaser",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 23,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    // Step 0: 타겟 레이저 소개
                    new TutorialStep
                    {
                        id = "tut_targetlaser_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "새로운 힘: 타겟 레이저!",
                        message = "가장 강력한 특수 블록 중 하나!\n타겟 레이저가 해금되었어요!\n직접 만들어 볼까요?",
                        pauseGame = true,
                        iconSpecialType = SpecialBlockType.XBlock
                    },
                    // Step 1: 회전 전 파란 블록 6개 하이라이트 (회전 후 링을 이룰 후보)
                    new TutorialStep
                    {
                        id = "tut_targetlaser_pattern",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "타겟 레이저 생성 방법",
                        message = "블록을 링 모양으로 매칭하면\n타겟 레이저가 생성됩니다!\n6개 블록이 중심을 둘러싸는 형태예요.",
                        pauseGame = true,
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord(2, 1), new HexCoord(2, 0),
                            new HexCoord(0, 2), new HexCoord(1, 2),
                            new HexCoord(0, 0), new HexCoord(1, 0)
                        }
                    },
                    // Step 2: 강제 회전 유도
                    new TutorialStep
                    {
                        id = "tut_targetlaser_force_rotate",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "회전",
                        message = "밝은 삼각형을 터치해서\n링을 완성시키세요!",
                        showFingerGuide = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 블록 중심을 탭하여 주세요!",
                        allowedCoords = new HexCoord[]
                        {
                            new HexCoord(0, 0), new HexCoord(0, 1), new HexCoord(1, 0)
                        }
                    },
                    // Step 3: 회전 완료 대기
                    new TutorialStep
                    {
                        id = "tut_targetlaser_wait_rotation",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.RotationComplete
                    },
                    // Step 4: 매칭 하이라이트 pause 대기
                    new TutorialStep
                    {
                        id = "tut_targetlaser_wait_match_pause",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.MatchHighlightPause
                    },
                    // Step 5: 링 매칭 설명
                    new TutorialStep
                    {
                        id = "tut_targetlaser_match_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "링 매칭 완성!",
                        message = "6개 블록이 링을 이루었어요!\n이 패턴이 타겟 레이저가 됩니다.",
                        pauseGame = true,
                        highlightBlockCoords = new HexCoord[]
                        {
                            new HexCoord(2, 1), new HexCoord(2, 0),
                            new HexCoord(1, 0), new HexCoord(0, 1),
                            new HexCoord(0, 2), new HexCoord(1, 2)
                        }
                    },
                    // Step 6: 타겟 레이저 완성 + 사용법 + 마나 소모 설명
                    //   (Bomb 튜토리얼 패턴 참조: XBlockCreatedPause 이벤트 발생 후 원자적으로 표시
                    //    → 생성 직후 낙하/강제 클릭 타이밍 race 회피)
                    new TutorialStep
                    {
                        id = "tut_targetlaser_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "타겟 레이저 완성!",
                        message = "타겟 레이저를 탭하면 링 안쪽 색상의 모든 블록을\n거리순으로 순차 파괴합니다.\n넓은 범위를 한 번에 정리하기 좋아요.\n\n특수 블록 발동 시 마나 10을 소모합니다.",
                        pauseGame = true,
                        iconSpecialType = SpecialBlockType.XBlock,
                        waitForEventBeforeShow = TutorialWaitEvent.XBlockCreatedPause
                    },
                    // Step 7: 타겟 레이저 클릭 강제 (사용법 학습 — 드론/드릴 튜토리얼과 동일 패턴)
                    //   forceSpecialClick=true → HandleForcedActionStep이 X블록 좌표만 허용 + 글로우 + 손가락 가이드
                    //   characterName 설정 → 하단 Dialog Panel UI로 안내
                    new TutorialStep
                    {
                        id = "tut_targetlaser_force_click",
                        type = TutorialStepType.ForcedAction,
                        characterName = "엘라시온",
                        title = "타겟 레이저 발사",
                        message = "밝은 타겟 레이저를 탭해\n발동시켜 보세요!",
                        showFingerGuide = true,
                        forceSpecialClick = true,
                        pauseGame = false,
                        blockedToastMessage = "밝은 타겟 레이저를 터치해 주세요!"
                    },
                    // Step 8: 타겟 레이저 발동 대기
                    new TutorialStep
                    {
                        id = "tut_targetlaser_wait_activated",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.XBlockActivated
                    },
                    // Step 9: 타겟 레이저 능력 마무리 설명 (드론/드릴 튜토리얼과 동일 흐름)
                    new TutorialStep
                    {
                        id = "tut_targetlaser_effect",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = "타겟 레이저 능력",
                        message = "마나 10을 써서 링 안쪽 색상의 블록을\n거리 순으로 모두 파괴했어요!\n광역 정리에 최고예요.",
                        pauseGame = true
                    },
                    // Step 10: 자유 플레이 힌트 — 또 다른 타겟 레이저 만들기 유도
                    new TutorialStep
                    {
                        id = "tut_targetlaser_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "링 매칭으로 타겟 레이저를 또 만들어 보세요!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // ============================================================
        // 상황별 힌트: 특수 블록 첫 생성
        // ============================================================
        private static TutorialSequence GetHint_BombCreated()
        {
            return new TutorialSequence
            {
                sequenceId = "hint_bomb",
                trigger = TutorialTrigger.OnFirstSpecialCreate,
                triggerSpecialType = SpecialBlockType.Bomb,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = "hint_bomb_msg",
                        type = TutorialStepType.FreePlayHint,
                        message = "폭탄 완성! 터치하면 주변 7칸을 폭발시킵니다!",
                        hintDuration = 4f
                    }
                }
            };
        }

        private static TutorialSequence GetHint_RainbowCreated()
        {
            return new TutorialSequence
            {
                sequenceId = "hint_rainbow",
                trigger = TutorialTrigger.OnFirstSpecialCreate,
                triggerSpecialType = SpecialBlockType.Rainbow,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = "hint_rainbow_msg",
                        type = TutorialStepType.FreePlayHint,
                        message = "타겟 레이저 완성! 터치하면 같은 색 전체를 파괴합니다!",
                        hintDuration = 4f
                    }
                }
            };
        }

        private static TutorialSequence GetHint_DroneCreated()
        {
            return new TutorialSequence
            {
                sequenceId = "hint_drone",
                trigger = TutorialTrigger.OnFirstSpecialCreate,
                triggerSpecialType = SpecialBlockType.Drone,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = "hint_drone_msg",
                        type = TutorialStepType.FreePlayHint,
                        message = "드론 완성! 가장 필요한 곳을 자동으로 공격합니다!",
                        hintDuration = 4f
                    }
                }
            };
        }

        private static TutorialSequence GetHint_XBlockCreated()
        {
            return new TutorialSequence
            {
                sequenceId = "hint_xblock",
                trigger = TutorialTrigger.OnFirstSpecialCreate,
                triggerSpecialType = SpecialBlockType.XBlock,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = "hint_xblock_msg",
                        type = TutorialStepType.FreePlayHint,
                        message = "X블록 완성! 터치하면 해당 색상 전체를 파괴합니다!",
                        hintDuration = 4f
                    }
                }
            };
        }

        // ============================================================
        // 상황별 힌트: 적군 첫 등장 (특성 + 공략법)
        //   새 몬스터가 처음 등장하는 레벨에서 pauseGame=true 대화로 특성과 공략을 안내.
        //   showOnce=true 이므로 최초 1회만 표시.
        // ============================================================
        private static TutorialSequence MakeEnemyHint(string id, EnemyType type, string title, string message)
        {
            return new TutorialSequence
            {
                sequenceId = id,
                trigger = TutorialTrigger.OnFirstEnemyEncounter,
                triggerEnemyType = type,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = id + "_msg",
                        type = TutorialStepType.Dialog,
                        characterName = "엘라시온",
                        title = title,
                        message = message,
                        pauseGame = true
                    }
                }
            };
        }

        // ── 일반 적군 ──
        private static TutorialSequence GetHint_Chromophage() => MakeEnemyHint(
            "hint_chromophage", EnemyType.Chromophage,
            "적군: 색상도둑",
            "인접 블록의 색을 회색으로 바꿔\n매칭을 차단해요.\n\n공략: 색상도둑을 인접 매칭으로 먼저 제거!\n매칭이 불가능해지기 전에 끊어주세요.");

        private static TutorialSequence GetHint_ChainAnchor() => MakeEnemyHint(
            "hint_chain", EnemyType.ChainAnchor,
            "적군: 사슬",
            "사슬에 묶인 블록은 이동/회전 불가예요.\n\n공략: 사슬 블록의 인접 매칭으로\n사슬을 해제한 뒤 제거하세요.");

        private static TutorialSequence GetHint_ThornParasite() => MakeEnemyHint(
            "hint_thorn", EnemyType.ThornParasite,
            "적군: 가시 기생충",
            "매칭 시 주변 블록에 피해를 줘요.\n\n공략: 특수 블록(폭탄·드릴)으로\n거리를 두고 한 번에 제거!\n인접 매칭은 주변 블록을 손상시킵니다.");

        private static TutorialSequence GetHint_GravityWarper() => MakeEnemyHint(
            "hint_gravity", EnemyType.GravityWarper,
            "적군: 중력왜곡자",
            "이 블록 자체와 위쪽 블록은 낙하하지 않아요.\n\n공략: 중력왜곡자를 직접 제거해야\n위 블록들이 다시 떨어지며 정돈됩니다.");

        private static TutorialSequence GetHint_ReflectionShield() => MakeEnemyHint(
            "hint_shield", EnemyType.ReflectionShield,
            "적군: 반사방패",
            "여러 번 공격해야 방패가 깨져요.\n\n공략: 반복 매칭/드릴로 방패를 모두 벗긴 뒤\n마무리 공격으로 처치!");

        // ── 고블린 계열 ──
        // ★ Stage 2의 stage2_goblin_intro가 몽둥이 고블린을 자세히 다루므로
        //   GetHint_Goblin은 비등록 (GetAllSequences에서 제외). 기타 종류만 첫 등장 hint 발동.
        private static TutorialSequence GetHint_Goblin() => MakeEnemyHint(
            "hint_goblin_basic", EnemyType.Goblin,
            "<color=#E06060>몽둥이 고블린</color> 출현!",
            "낡은 몽둥이를 휘두르며\n<color=#FFD060>정령마을</color>에 침입한 약졸!\n\n매 턴 <b>한 칸씩</b> 내려와\n블록을 두드려 <color=#B8F0FF>정령의 빛</color>을 빼앗습니다.\n\n<color=#8EE080>공략</color>: 낙하 블록의 충돌로 처치!");

        private static TutorialSequence GetHint_ArmoredGoblin() => MakeEnemyHint(
            "hint_goblin_armored", EnemyType.ArmoredGoblin,
            "<color=#A0A0C0>갑옷 고블린</color> 등장!",
            "단단한 <b>강철 갑옷</b>으로 무장한 정예!\n일반 공격으로는 쉽게 쓰러지지 않습니다.\n\n<color=#FF8844>HP가 두 배</color>—\n낙하 데미지를 여러 번 누적해야 처치!\n\n<color=#8EE080>공략</color>: <color=#FFE066>특수 블록 광역 공격</color>으로 한 번에 분쇄!");

        private static TutorialSequence GetHint_ArcherGoblin() => MakeEnemyHint(
            "hint_goblin_archer", EnemyType.ArcherGoblin,
            "<color=#FF8844>활 고블린</color> 저격 개시!",
            "필드 끝에서 <b>화살을 겨누는 사수</b>!\n매 턴 멀리 있는 블록에 <color=#E06060>크랙</color>을 박아\n필드 전체를 천천히 망가뜨립니다.\n\n<color=#8EE080>공략</color>: <color=#FFE066>망치·드릴</color>로 최우선 제거!\n방치하면 모든 블록이 금 갑니다.");

        private static TutorialSequence GetHint_ShieldGoblin() => MakeEnemyHint(
            "hint_goblin_shield", EnemyType.ShieldGoblin,
            "<color=#7FE3FF>방패 고블린</color> 방어 자세!",
            "거대한 <b>강철 방패</b>로 드릴조차 막아냅니다.\n공격 <color=#FFE066>3회</color>까지 완전 차단!\n\n<color=#8EE080>공략</color>:\n<color=#FFE066>드릴을 반복 사용</color>해 방패를 깎고\n마지막 일격으로 가르세요!");

        private static TutorialSequence GetHint_BombGoblin() => MakeEnemyHint(
            "hint_goblin_bomb", EnemyType.BombGoblin,
            "<color=#FF4040>폭탄 고블린</color> 침투!",
            "블록에 <b>시한폭탄</b>을 설치하는 위험분자!\n카운트다운이 끝나면\n<color=#FF8844>광역 폭발로 필드를 초토화</color>합니다.\n\n<color=#8EE080>공략</color>:\n카운트다운 종료 전 <b>고블린을 처치</b>하거나\n폭탄 블록을 <color=#FFE066>매칭으로 해제</color>!");

        private static TutorialSequence GetHint_HeavyGoblin() => MakeEnemyHint(
            "hint_goblin_heavy", EnemyType.HeavyGoblin,
            "<color=#A06060>헤비 고블린</color> 진군!",
            "거대한 몸집으로 <b>3개 블록을 점유</b>!\n점유된 블록은 <color=#E06060>회전 불가</color>—\n전략 회전을 봉쇄당합니다. HP <color=#FF8844>6</color>.\n\n<color=#8EE080>공략</color>:\n인접 블록 매칭으로 낙하 데미지 누적,\n또는 <color=#FFE066>특수 블록 광역 공격</color>으로 일격!");

        private static TutorialSequence GetHint_WizardGoblin() => MakeEnemyHint(
            "hint_goblin_wizard", EnemyType.WizardGoblin,
            "<color=#C060FF>마법사 고블린</color> 영창!",
            "소환 영역에서 <b>원거리 마법</b>을 발사!\n매 턴 블록·특수 블록을 <color=#FF4040>파괴</color>합니다.\nHP <color=#FF8844>4</color>.\n\n<color=#8EE080>공략</color>:\n<color=#FFE066>특수 블록·아이템</color>으로 신속 제거!\n방치하면 필드가 빠르게 빈약해집니다.");

        private static TutorialSequence GetHint_ThiefGoblin() => MakeEnemyHint(
            "hint_goblin_thief", EnemyType.ThiefGoblin,
            "<color=#888888>도둑 고블린</color> 잠입!",
            "<b>은신과 수리검</b>을 번갈아 쓰는 그림자.\n2칸씩 빠르게 이동하며\n은신 중엔 <color=#FF4040>공격 무효</color>! HP <color=#FF8844>3</color>.\n\n<color=#8EE080>공략</color>:\n은신 해제 턴을 노려 매칭으로 일격!\n놓치면 <color=#FFE066>특수 블록</color>이 표적이 됩니다.");

        private static TutorialSequence GetHint_WitchGoblin() => MakeEnemyHint(
            "hint_goblin_witch", EnemyType.WitchGoblin,
            "<color=#FF60C0>마녀 고블린</color> 강림!",
            "소환 영역에서 <b>언데드 고블린</b>을 불러내는 마녀!\n방치하면 필드가 언데드로 가득합니다.\nHP <color=#FF8844>5</color>.\n\n<color=#8EE080>공략</color>:\n<color=#FFE066>최우선 처치</color>!\n마녀를 쓰러뜨리면 소환된 언데드도 함께 소멸합니다.");

        // ============================================================
        // 합성 튜토리얼 팩토리 (스테이지 31~50)
        // ============================================================

        /// <summary>
        /// 합성 튜토리얼 시퀀스 생성 헬퍼 (조합당 1개).
        ///   1) 도입 Dialog — 합성 개념 소개
        ///   2) 효과 Dialog — 조합 효과 설명
        ///   3) ForcedAction — (0,0)/(1,0) 두 특수 블록만 입력 허용 + 손가락 가이드
        ///   4) WaitForEvent — ComboActivated 대기 (스왑 완료까지)
        ///   5) 완료 Dialog — 성공 격려 (isOpenEnded=true면 "다른 조합 시도해보세요" 안내)
        ///   6) FreePlayHint — 자유 플레이 팁
        /// preset(두 특수 블록 (0,0)/(1,0) 배치)은 TutorialManager.SetupComboTutorialBoard에서 처리.
        /// </summary>
        private static TutorialSequence MakeComboTutorial(
            int stage, string comboName, string effectDescription, string tipMessage, bool isOpenEnded = false)
        {
            HexCoord[] comboCoords = new[] { new HexCoord(0, 0), new HexCoord(1, 0) };

            // 완료 메시지: 일반은 단순 성공 격려, 열린 모드는 다양한 합성 탐색 권유
            string doneMessage = isOpenEnded
                ? $"합성이 성공적으로 발동되었어요!\n\n특수 블록 조합은 정말 다양합니다.\n드릴·폭탄·X블록·드론을 자유롭게 조합해\n나만의 합성을 발견해 보세요!\n\n💡 {tipMessage}"
                : $"합성이 성공적으로 발동되었어요!\n💡 {tipMessage}";

            var steps = new TutorialStep[]
            {
                new TutorialStep
                {
                    id = $"combo{stage}_intro",
                    type = TutorialStepType.Dialog,
                    characterName = "엘라시온",
                    title = "합성: " + comboName,
                    message = "보드에 두 특수 블록이 나란히 있어요!\n두 블록을 서로 스왑하면 합성 효과가 발동됩니다.",
                    pauseGame = true
                },
                new TutorialStep
                {
                    id = $"combo{stage}_effect",
                    type = TutorialStepType.Dialog,
                    characterName = "엘라시온",
                    title = comboName + " 효과",
                    message = effectDescription ?? "",
                    pauseGame = true
                },
                new TutorialStep
                {
                    id = $"combo{stage}_force_swap",
                    type = TutorialStepType.ForcedAction,
                    characterName = "엘라시온",
                    title = "합성 실행",
                    message = "밝게 표시된 두 특수 블록을\n서로 스왑(드래그)하여 합성을 발동해 보세요!",
                    allowedCoords = comboCoords,
                    showFingerGuide = true,
                    pauseGame = false,
                    blockedToastMessage = "밝게 표시된 두 특수 블록을 스왑해주세요!"
                },
                new TutorialStep
                {
                    id = $"combo{stage}_wait_activated",
                    type = TutorialStepType.WaitForEvent,
                    waitEvent = TutorialWaitEvent.ComboActivated
                },
                new TutorialStep
                {
                    id = $"combo{stage}_done",
                    type = TutorialStepType.Dialog,
                    characterName = "엘라시온",
                    title = comboName + " 성공!",
                    message = doneMessage,
                    pauseGame = true
                },
                new TutorialStep
                {
                    id = $"combo{stage}_hint",
                    type = TutorialStepType.FreePlayHint,
                    message = isOpenEnded
                        ? "💥 다양한 합성을 탐험해 보세요! 모든 조합이 독특한 효과를 냅니다."
                        : "💥 " + tipMessage,
                    hintDuration = 5f
                }
            };

            return new TutorialSequence
            {
                sequenceId = $"combo_tut_stage{stage}",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = stage,
                showOnce = true,
                steps = steps
            };
        }
    }
}

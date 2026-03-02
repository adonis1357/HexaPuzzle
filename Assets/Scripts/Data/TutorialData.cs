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
        AnyInput                // 아무 입력
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
        public Vector2 highlightScreenPos;          // 하이라이트 화면 좌표
        public float highlightRadius = 80f;         // 하이라이트 반경

        // 강제 입력 설정
        public HexCoord[] allowedCoords;            // 터치 허용 좌표 (ForcedAction용)
        public bool showFingerGuide = false;        // 손가락 가이드 표시
        public Vector2 fingerGuidePos;              // 손가락 가이드 위치

        // 이벤트 대기 설정
        public TutorialWaitEvent waitEvent = TutorialWaitEvent.None;

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

            // ── 스테이지 1: 기초 온보딩 + 미션 설명 (강제 안내) ──
            sequences.Add(GetStage1_Onboarding());

            // ── 스테이지 3: 드릴 소개 ──
            sequences.Add(GetStage3_DrillIntro());

            // ── 상황별 힌트: 특수 블록 ──
            sequences.Add(GetHint_BombCreated());
            sequences.Add(GetHint_RainbowCreated());
            sequences.Add(GetHint_DroneCreated());
            sequences.Add(GetHint_XBlockCreated());

            // ── 상황별 힌트: 적군 ──
            sequences.Add(GetHint_Chromophage());
            sequences.Add(GetHint_ChainAnchor());

            return sequences;
        }

        // ============================================================
        // 스테이지 1: 기초 온보딩
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
                    new TutorialStep
                    {
                        id = "s1_welcome",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "환영합니다!",
                        message = "보석이 가득한 육각형 격자에요!\n같은 색 3개를 모으면 블록이 정화됩니다.",
                        pauseGame = true
                    },
                    new TutorialStep
                    {
                        id = "s1_explain_cluster",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "회전 방법",
                        message = "삼각형 모양의 3개 블록을 터치하면\n120도씩 회전시킬 수 있어요!",
                        pauseGame = true,
                        showFingerGuide = true
                    },
                    new TutorialStep
                    {
                        id = "s1_try_rotate",
                        type = TutorialStepType.ForcedAction,
                        characterName = "오라클리온",
                        message = "블록을 터치해서 회전시켜 보세요!",
                        showFingerGuide = true,
                        pauseGame = false
                    },
                    new TutorialStep
                    {
                        id = "s1_wait_rotation",
                        type = TutorialStepType.WaitForEvent,
                        waitEvent = TutorialWaitEvent.RotationComplete
                    },
                    new TutorialStep
                    {
                        id = "s1_match_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "매칭 성공!",
                        message = "잘했어요! 같은 색 블록이 모여 제거되었고\n위에서 새 블록이 떨어졌습니다.",
                        pauseGame = true
                    },
                    new TutorialStep
                    {
                        id = "s1_cascade_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "연쇄 반응",
                        message = "떨어진 블록이 또 매칭되면 연쇄 반응이 일어나요!\n연쇄가 많을수록 높은 점수를 얻습니다.",
                        pauseGame = true
                    },
                    // ── 미션 시스템 설명 ──
                    new TutorialStep
                    {
                        id = "s1_mission_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "미션 시스템",
                        message = "각 스테이지에는 미션이 있어요!\n화면 왼쪽 위를 봐주세요.",
                        pauseGame = true,
                        useHighlight = true,
                        highlightScreenPos = new Vector2(-370f, 810f),
                        highlightRadius = 140f
                    },
                    new TutorialStep
                    {
                        id = "s1_mission_detail",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "미션 목표",
                        message = "아이콘과 숫자가 미션 목표예요.\n특정 색 보석을 모으거나\n특수 블록을 만들어야 합니다!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightScreenPos = new Vector2(-370f, 810f),
                        highlightRadius = 140f
                    },
                    new TutorialStep
                    {
                        id = "s1_turns_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "이동 횟수",
                        message = "위쪽 숫자는 남은 이동 횟수예요.\n횟수가 0이 되기 전에\n미션을 완료하세요!",
                        pauseGame = true,
                        useHighlight = true,
                        highlightScreenPos = new Vector2(-200f, 915f),
                        highlightRadius = 100f
                    },
                    new TutorialStep
                    {
                        id = "s1_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "미션을 완료해 보세요!",
                        hintDuration = 5f
                    }
                }
            };
        }

        // ============================================================
        // 스테이지 3: 드릴 특수블록
        // ============================================================
        private static TutorialSequence GetStage3_DrillIntro()
        {
            return new TutorialSequence
            {
                sequenceId = "stage3_drill",
                trigger = TutorialTrigger.OnStageStart,
                triggerStage = 3,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = "s3_drill_intro",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "특수 블록: 드릴",
                        message = "4개 블록을 직선으로 매칭하면\n드릴 블록이 생성돼요!",
                        pauseGame = true
                    },
                    new TutorialStep
                    {
                        id = "s3_drill_explain",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "드릴 사용법",
                        message = "드릴을 터치하면 한 방향의\n블록을 모두 파괴합니다!\n방향은 드릴의 줄무늬로 알 수 있어요.",
                        pauseGame = true
                    },
                    new TutorialStep
                    {
                        id = "s3_freeplay",
                        type = TutorialStepType.FreePlayHint,
                        message = "4개 직선 매칭으로 드릴을 만들어 보세요!",
                        hintDuration = 6f
                    }
                }
            };
        }

        // ============================================================
        // 상황별 힌트: 특수 블록
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
                        message = "💣 폭탄이에요! 터치하면 주변 7칸을 폭발시킵니다!",
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
                        message = "🌈 무지개 도넛! 터치하면 같은 색 전체를 파괴합니다!",
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
                        message = "🚁 드론이에요! 가장 필요한 곳을 자동으로 공격합니다!",
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
                        message = "✖ X블록! 터치하면 해당 색상 전체를 파괴합니다!",
                        hintDuration = 4f
                    }
                }
            };
        }

        // ============================================================
        // 상황별 힌트: 적군
        // ============================================================
        private static TutorialSequence GetHint_Chromophage()
        {
            return new TutorialSequence
            {
                sequenceId = "hint_chromophage",
                trigger = TutorialTrigger.OnFirstEnemyEncounter,
                triggerEnemyType = EnemyType.Chromophage,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = "hint_chromophage_msg",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "적군: 색상도둑",
                        message = "색상도둑이 나타났어요!\n인접 블록의 색을 회색으로 바꿉니다.\n매칭으로 제거하세요!",
                        pauseGame = true
                    }
                }
            };
        }

        private static TutorialSequence GetHint_ChainAnchor()
        {
            return new TutorialSequence
            {
                sequenceId = "hint_chain",
                trigger = TutorialTrigger.OnFirstEnemyEncounter,
                triggerEnemyType = EnemyType.ChainAnchor,
                showOnce = true,
                steps = new TutorialStep[]
                {
                    new TutorialStep
                    {
                        id = "hint_chain_msg",
                        type = TutorialStepType.Dialog,
                        characterName = "오라클리온",
                        title = "적군: 사슬",
                        message = "사슬에 묶인 블록이에요!\n인접 블록을 매칭하면 사슬이 풀립니다.",
                        pauseGame = true
                    }
                }
            };
        }
    }
}

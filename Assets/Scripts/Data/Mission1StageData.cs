using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 미션 1: 색상도둑 소탕 - Stage 1-10 데이터
    ///
    /// 레벨 디자인 기획서:
    /// LevelDesign_Mission1_ChromophageCleaning.md 참조
    ///
    /// 난이도 곡선:
    /// Stage 1-2: 색상도둑 1개 (⭐ 튜토리얼)
    /// Stage 3-5: 색상도둑 2개 (⭐⭐ 중간)
    /// Stage 6-8: 색상도둑 2-3개 (⭐⭐ 중상)
    /// Stage 9: 색상도둑 3개 (⭐⭐ 약간 어려움)
    /// Stage 10: 색상도둑 3개 + 콤보 20 (⭐⭐⭐ 어려움, 보스)
    /// </summary>
    public static class Mission1StageData
    {
        /// <summary>
        /// 모든 미션 1 스테이지 데이터 반환
        /// </summary>
        public static Dictionary<int, StageData> GetAllMission1Stages()
        {
            return new Dictionary<int, StageData>
            {
                { 1, GetStage1() },
                { 2, GetStage2() },
                { 3, GetStage3() },
                { 4, GetStage4() },
                { 5, GetStage5() },
                { 6, GetStage6() },
                { 7, GetStage7() },
                { 8, GetStage8() },
                { 9, GetStage9() },
                { 10, GetStage10() },
                { 11, GetStage11() }
            };
        }

        // ============================================================
        // Stage 1: 적군 소개 - 색상도둑 1개
        // ============================================================
        private static StageData GetStage1()
        {
            StageData stage = new StageData
            {
                stageNumber = 1,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.None, // 모든 색상
                        targetCount = 100,
                        description = "기본 블록 100개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    chapterIntroduction = "루나가 크리스탈 숲에 발을 디딘다.",
                    beforeStageCutscene = "루나가 크리스탈 숲의 보석 블록들을 관찰한다.\n오라클리온: \"자, 이게 보석의 격자야. 같은 색 3개가 모이면 정화된단다.\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"먼저 기본을 익혀보자. 블록을 회전시켜 같은 색끼리 모아봐.\"",
                        "루나: \"알겠어요! 해볼게요!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"훌륭하군! 기본을 잘 익혔구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 2: 빨간색 30개 + 초록색 30개 제거
        // ============================================================
        private static StageData GetStage2()
        {
            StageData stage = new StageData
            {
                stageNumber = 2,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 20,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Red,
                        targetCount = 30,
                        description = "빨간색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Green,
                        targetCount = 30,
                        description = "초록색 블록 30개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 3: 파란색 블록 40개 제거 (1종 색상 수집)
        // ============================================================
        private static StageData GetStage3()
        {
            StageData stage = new StageData
            {
                stageNumber = 3,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Blue,
                        targetCount = 40,
                        description = "파란색 블록 40개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 4: 노란색 35 + 보라색 35 제거 (2종 색상 수집)
        // ============================================================
        private static StageData GetStage4()
        {
            StageData stage = new StageData
            {
                stageNumber = 4,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 22,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Yellow,
                        targetCount = 35,
                        description = "노란색 블록 35개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Purple,
                        targetCount = 35,
                        description = "보라색 블록 35개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 더 위험한 지역으로 진입한다.\n오라클리온: \"이 숲 깊숙이로 들어갈수록 오염이 심해지는군.\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"이번엔 두 가지 색상의 보석을 모아봐.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "프리즘: \"루나, 또 해냈어!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 5: 빨강 40 + 파랑 40 제거 + 세로 드릴 1개 생성
        // ============================================================
        private static StageData GetStage5()
        {
            StageData stage = new StageData
            {
                stageNumber = 5,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 25,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Red,
                        targetCount = 40,
                        description = "빨간색 블록 40개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Blue,
                        targetCount = 40,
                        description = "파란색 블록 40개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CreateDrillVertical,
                        targetCount = 1,
                        description = "세로 드릴 1개 생성"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나: \"보석들이 점점 다양해지네요.\"\n오라클리온: \"맞다. 더 많은 색상을 효율적으로 모아야 한다.\"",
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"루나는 할 수 있어! 이겨내!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"좋아. 계속 진행하자.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 6: 초록·노랑·보라 각 30개 제거 (3종 색상 수집)
        // ============================================================
        private static StageData GetStage6()
        {
            StageData stage = new StageData
            {
                stageNumber = 6,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 28,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Green,
                        targetCount = 30,
                        description = "초록색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Yellow,
                        targetCount = 30,
                        description = "노란색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Purple,
                        targetCount = 30,
                        description = "보라색 블록 30개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new[]
                {
                    new EnemyPlacement { coord = new HexCoord(-1, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, -1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(0, -2), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(-2, 2), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(2, -2), enemyType = EnemyType.None }
                },
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 숲의 더 깊숙한 곳으로 진입한다.\n프리즘: \"와... 색상도둑이 3개나!\"\n루나: \"이게 최종 시험일까요?\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"그렇도록 보이는군. 자신감을 잃지 말고 차근차근이다.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"좋아. 계속이다.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 7: 빨강 35 + 파랑 35 + 폭탄 1개 생성
        // ============================================================
        private static StageData GetStage7()
        {
            StageData stage = new StageData
            {
                stageNumber = 7,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 30,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Red,
                        targetCount = 35,
                        description = "빨간색 블록 35개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Blue,
                        targetCount = 35,
                        description = "파란색 블록 35개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CreateBomb,
                        targetCount = 1,
                        description = "폭탄 1개 생성"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new[]
                {
                    new EnemyPlacement { coord = new HexCoord(-1, 0), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, 0), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(0, -2), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(-2, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(2, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, 2), enemyType = EnemyType.None }
                },
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"계속해서 진행하거라.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "프리즘: \"루나, 계속 해내고 있어!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 8: 4색 각 30개 제거 (4종 색상 수집)
        // ============================================================
        private static StageData GetStage8()
        {
            StageData stage = new StageData
            {
                stageNumber = 8,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 32,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Red,
                        targetCount = 30,
                        description = "빨간색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Blue,
                        targetCount = 30,
                        description = "파란색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Yellow,
                        targetCount = 30,
                        description = "노란색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Purple,
                        targetCount = 30,
                        description = "보라색 블록 30개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new[]
                {
                    new EnemyPlacement { coord = new HexCoord(-1, -1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(0, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(-2, 0), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(0, 2), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(2, -1), enemyType = EnemyType.None }
                },
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"거의 다 왔다.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "루나: \"거의 끝이 보이네요!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 9: 4색 각 35개 제거 (4종 색상 수집, 고난이도)
        // ============================================================
        private static StageData GetStage9()
        {
            StageData stage = new StageData
            {
                stageNumber = 9,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 35,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Red,
                        targetCount = 35,
                        description = "빨간색 블록 35개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Green,
                        targetCount = 35,
                        description = "초록색 블록 35개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Yellow,
                        targetCount = 35,
                        description = "노란색 블록 35개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Purple,
                        targetCount = 35,
                        description = "보라색 블록 35개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new[]
                {
                    new EnemyPlacement { coord = new HexCoord(-1, 0), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(0, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, -2), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(-2, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(2, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, 2), enemyType = EnemyType.None }
                },
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 숲의 깊은 곳에 도달한다.\n거대한 색상도둑 무리가 모습을 드러낸다!",
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"와, 색상도둑이 3개나!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"완벽하다, 루나! 이제 넌 색상도둑의 방해를 이겨낼 수 있다!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 10: 챕터 1 보스 - 5색 각 30개 제거 (5종 색상 수집)
        // ============================================================
        private static StageData GetStage10()
        {
            StageData stage = new StageData
            {
                stageNumber = 10,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 38,
                difficulty = 3,
                isBossStage = true,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Red,
                        targetCount = 30,
                        description = "빨간색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Blue,
                        targetCount = 30,
                        description = "파란색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Green,
                        targetCount = 30,
                        description = "초록색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Yellow,
                        targetCount = 30,
                        description = "노란색 블록 30개 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.CollectGem,
                        targetGemType = GemType.Purple,
                        targetCount = 30,
                        description = "보라색 블록 30개 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new[]
                {
                    new EnemyPlacement { coord = new HexCoord(-1, 1), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, 0), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(0, 2), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(1, -2), enemyType = EnemyType.None },
                    new EnemyPlacement { coord = new HexCoord(2, 1), enemyType = EnemyType.None }
                },
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 크리스탈 숲의 중심에 도달한다.\n거대한 색상도둑 무리 3개가 마주선다!\n프리즘: \"와, 이게 우리가 이겨낼 수 있는 건가?\"\n루나: \"지금까지 배운 모든 것을 써야겠어요!\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"루나, 마지막 시험이다. 이 색상도둑들을 정화해보거라.\"",
                        "오라클리온: \"단순히 제거하는 것 외에... 연쇄의 흐름도 중요하다.\"",
                        "루나: \"알겠습니다! 색상도둑도 제거하고, 아름다운 연쇄도!\""
                    },
                    stageClearCutscene = "색상도둑들이 모두 정화되며 밝은 빛으로 사라진다.\n크리스탈 숲이 다시 생기를 되찾는다.\n루나가 숨을 고른다.\n루나: \"해냈어요! 색상도둑들을 모두 정화했어요!\"\n프리즘: \"우와! 루나, 정말 멋있었어!\"\n오라클리온: \"훌륭하군, 루나. 이제 넌 진정한 크리스탈 수호자의 길을 시작했다.\"",
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"이제 사파이어 호수로 나아가자. 더 큰 도전이 기다리고 있다.\"",
                        "루나: \"네, 준비됐어요!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0],
                rewards = new RewardData
                {
                    baseExperience = 150,
                    comboReward = 50,
                    perfectClearReward = 100,
                    badgeReward = "크리스탈 숲의 수호자"
                }
            };

            return stage;
        }

        // ============================================================
        // Stage 11: 드릴 튜토리얼 - 드릴 3개 생성
        // ============================================================
        private static StageData GetStage11()
        {
            StageData stage = new StageData
            {
                stageNumber = 11,
                chapterNumber = 2,
                chapterName = "사파이어 호수",
                turnLimit = 20,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.CreateDrillAny,
                        targetCount = 3,
                        description = "드릴 3개 생성"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "오라클리온: \"이제 특수 블록의 힘을 배워볼 시간이다.\"\n루나: \"특수 블록이요?\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"4개의 블록을 직선으로 맞추면 드릴이 만들어진다.\"\n루나: \"해볼게요!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"훌륭하군! 드릴의 힘을 잘 이해했구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }
    }
}

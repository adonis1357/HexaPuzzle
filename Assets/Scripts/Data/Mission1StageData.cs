using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 미션 1: 고블린 소탕 - Stage 1-10 데이터
    ///
    /// 레벨 디자인:
    /// 고블린이 그리드 상단 빈 공간 3줄에서 포털을 통해 소환되고,
    /// 매 턴 아래로 이동하며 블록을 공격해 "깨진 블록"으로 만든다.
    /// 블록 낙하 시 고블린에게 충돌 데미지를 주어 제거하는 미션.
    ///
    /// 난이도 곡선:
    /// Stage 1-2: 소환 0~1마리/턴, 제거 2~3마리 (⭐ 튜토리얼)
    /// Stage 3-4: 소환 1~2마리/턴, 제거 4~5마리 (⭐ 초급)
    /// Stage 5-6: 소환 1~2마리/턴, 제거 6~8마리 (⭐⭐ 중급)
    /// Stage 7-8: 소환 1~3마리/턴, 제거 10~12마리 (⭐⭐ 중상)
    /// Stage 9-10: 소환 2~4마리/턴, 제거 14~16마리 (⭐⭐⭐ 상급)
    ///
    /// 모든 스테이지 턴 제한: 15턴
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
        // Stage 1: 고블린 소개 - 고블린 2마리 제거
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
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    chapterIntroduction = "루나가 크리스탈 숲에 발을 디딘다.",
                    beforeStageCutscene = "숲 속에서 이상한 소리가 들린다...\n프리즘: \"저기 봐! 녹색 괴물들이 나타나고 있어!\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"저건 고블린이다. 블록을 공격해서 금간 블록으로 만들어 버리지.\"",
                        "오라클리온: \"블록을 낙하시켜 고블린에게 충돌 데미지를 줄 수 있단다.\"",
                        "루나: \"알겠어요! 블록을 떨어뜨려서 고블린을 처치할게요!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"훌륭하군! 고블린을 물리치는 법을 잘 익혔구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 2: 고블린 3마리 제거
        // ============================================================
        private static StageData GetStage2()
        {
            StageData stage = new StageData
            {
                stageNumber = 2,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 3,
                        description = "고블린 3마리 제거"
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
        // Stage 3: 고블린 4마리 제거
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
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 4,
                        description = "고블린 4마리 제거"
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
        // Stage 4: 고블린 5마리 제거
        // ============================================================
        private static StageData GetStage4()
        {
            StageData stage = new StageData
            {
                stageNumber = 4,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 5,
                        description = "고블린 5마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 더 깊은 숲으로 진입한다.\n오라클리온: \"고블린의 수가 점점 늘어나고 있구나.\"",
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"루나, 조심해! 고블린이 더 많이 나타나고 있어!\""
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
        // Stage 5: 고블린 6마리 제거
        // ============================================================
        private static StageData GetStage5()
        {
            StageData stage = new StageData
            {
                stageNumber = 5,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 6,
                        description = "고블린 6마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나: \"고블린들이 점점 더 몰려오네요.\"\n오라클리온: \"침착하게, 블록을 효율적으로 낙하시켜라.\"",
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
        // Stage 6: 고블린 8마리 제거
        // ============================================================
        private static StageData GetStage6()
        {
            StageData stage = new StageData
            {
                stageNumber = 6,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 8,
                        description = "고블린 8마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 숲의 더 깊숙한 곳으로 진입한다.\n프리즘: \"고블린 대장이 부하들을 더 보내고 있어!\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"자신감을 잃지 말고 차근차근 제거하거라.\""
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
        // Stage 7: 고블린 10마리 제거
        // ============================================================
        private static StageData GetStage7()
        {
            StageData stage = new StageData
            {
                stageNumber = 7,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 10,
                        description = "고블린 10마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"고블린의 공세가 더 거세지고 있다.\""
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
        // Stage 8: 고블린 12마리 제거
        // ============================================================
        private static StageData GetStage8()
        {
            StageData stage = new StageData
            {
                stageNumber = 8,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 12,
                        description = "고블린 12마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
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
        // Stage 9: 고블린 14마리 제거
        // ============================================================
        private static StageData GetStage9()
        {
            StageData stage = new StageData
            {
                stageNumber = 9,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 14,
                        description = "고블린 14마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 숲의 깊은 곳에 도달한다.\n거대한 고블린 무리가 모습을 드러낸다!",
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"와, 고블린이 엄청 많아!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"대단하다, 루나! 이제 마지막 시험만 남았구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 10: 챕터 1 보스 - 고블린 16마리 제거
        // ============================================================
        private static StageData GetStage10()
        {
            StageData stage = new StageData
            {
                stageNumber = 10,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 15,
                difficulty = 3,
                isBossStage = true,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 16,
                        description = "고블린 16마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 크리스탈 숲의 중심에 도달한다.\n거대한 고블린 대장이 몽둥이를 휘두르며 나타난다!\n프리즘: \"저게 고블린 대장인가 봐! 부하들이 끝없이 밀려와!\"\n루나: \"지금까지 배운 모든 것을 써야겠어요!\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"루나, 마지막 시험이다. 고블린 무리를 소탕하거라.\"",
                        "오라클리온: \"블록을 매칭시켜 낙하를 만들어 고블린을 쓰러뜨려라!\"",
                        "루나: \"알겠습니다! 꼭 해내겠어요!\""
                    },
                    stageClearCutscene = "고블린 대장이 쓰러지며 나머지 고블린들이 달아난다.\n크리스탈 숲이 다시 평화를 되찾는다.\n루나가 숨을 고른다.\n루나: \"해냈어요! 고블린들을 모두 물리쳤어요!\"\n프리즘: \"우와! 루나, 정말 멋있었어!\"\n오라클리온: \"훌륭하군, 루나. 이제 넌 진정한 크리스탈 수호자의 길을 시작했다.\"",
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

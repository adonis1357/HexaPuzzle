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
                { 11, GetStage11() },
                { 12, GetStage12() },
                { 13, GetStage13() },
                { 14, GetStage14() },
                { 15, GetStage15() },
                { 16, GetStage16() },
                { 17, GetStage17() },
                { 18, GetStage18() },
                { 19, GetStage19() },
                { 20, GetStage20() },
                { 21, GetStage21() },
                { 22, GetStage22() },
                { 23, GetStage23() },
                { 24, GetStage24() },
                { 25, GetStage25() },
                { 26, GetStage26() },
                { 27, GetStage27() },
                { 28, GetStage28() },
                { 29, GetStage29() },
                { 30, GetStage30() }
            };
        }

        // ============================================================
        // Stage 1: 고블린 소개 - 고블린 6마리 제거
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
                        targetCount = 6,
                        description = "고블린 6마리 제거"
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
        // Stage 2: 고블린 9마리 제거
        // ============================================================
        private static StageData GetStage2()
        {
            StageData stage = new StageData
            {
                stageNumber = 2,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 17,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 9,
                        description = "고블린 9마리 제거"
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
        // Stage 3: 고블린 12마리 제거
        // ============================================================
        private static StageData GetStage3()
        {
            StageData stage = new StageData
            {
                stageNumber = 3,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 18,
                difficulty = 1,
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 4: 고블린 15마리 제거
        // ============================================================
        private static StageData GetStage4()
        {
            StageData stage = new StageData
            {
                stageNumber = 4,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 20,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 15,
                        description = "고블린 15마리 제거"
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
        // Stage 5: 고블린 18마리 제거
        // ============================================================
        private static StageData GetStage5()
        {
            StageData stage = new StageData
            {
                stageNumber = 5,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 22,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 18,
                        description = "고블린 18마리 제거"
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
        // Stage 6: 고블린 24마리 제거
        // ============================================================
        private static StageData GetStage6()
        {
            StageData stage = new StageData
            {
                stageNumber = 6,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 23,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 18,
                        description = "몽둥이 고블린 18마리 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 4,
                        description = "갑옷 고블린 4마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 숲의 더 깊숙한 곳으로 진입한다.\n프리즘: \"고블린 대장이 부하들을 더 보내고 있어! 갑옷을 입은 고블린도 보여!\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"갑옷 고블린은 체력이 높다. 자신감을 잃지 말고 차근차근 제거하거라.\""
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
        // Stage 7: 고블린 30마리 제거
        // ============================================================
        private static StageData GetStage7()
        {
            StageData stage = new StageData
            {
                stageNumber = 7,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 25,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 19,
                        description = "몽둥이 고블린 19마리 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 7,
                        description = "갑옷 고블린 7마리 제거"
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
        // Stage 8: 고블린 36마리 제거
        // ============================================================
        private static StageData GetStage8()
        {
            StageData stage = new StageData
            {
                stageNumber = 8,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 27,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 21,
                        description = "몽둥이 고블린 21마리 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 10,
                        description = "갑옷 고블린 10마리 제거"
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
        // Stage 9: 고블린 42마리 제거
        // ============================================================
        private static StageData GetStage9()
        {
            StageData stage = new StageData
            {
                stageNumber = 9,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 28,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 22,
                        description = "몽둥이 고블린 22마리 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 14,
                        description = "갑옷 고블린 14마리 제거"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 숲의 깊은 곳에 도달한다.\n거대한 고블린 무리가 모습을 드러낸다!",
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"와, 고블린이 엄청 많아! 갑옷 고블린도 잔뜩이야!\""
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
        // Stage 10: 챕터 1 보스 - 고블린 48마리 제거
        // ============================================================
        private static StageData GetStage10()
        {
            StageData stage = new StageData
            {
                stageNumber = 10,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 30,
                difficulty = 3,
                isBossStage = true,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 22,
                        description = "몽둥이 고블린 22마리 제거"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 18,
                        description = "갑옷 고블린 18마리 제거"
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
        // Stage 11: 활 고블린 첫 등장 — 활 2마리(HP2) + 소규모 3종 혼합 입문
        // 턴 22 | 몽둥이 8 + 갑옷 4 + 활 2(HP2)
        // ============================================================
        private static StageData GetStage11()
        {
            StageData stage = new StageData
            {
                stageNumber = 11,
                chapterNumber = 2,
                chapterName = "사파이어 호수",
                turnLimit = 22,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 2,
                        description = "활 고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 8,
                        description = "고블린 8마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 4,
                        description = "갑옷 고블린 4마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "프리즘: \"저기... 저 고블린들은 활을 들고 있어!\"\n오라클리온: \"활 고블린이다. 멀리서 화살을 쏴 블록을 금간 블록으로 만들지.\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"활 고블린은 위에서 움직이지 않고 화살을 쏜다. 드론이나 드릴로 처치해야 한다.\"",
                        "루나: \"낙하 블록으로는 안 되나요?\"\n오라클리온: \"안타깝지만 그렇다. 신중하게 특수 블록을 활용하렴.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"잘했다! 활 고블린의 약점을 잘 파악했구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 12: 활 2마리(HP2) 유지 + 갑옷 비율 증가 — 적응기
        // 턴 25 | 몽둥이 10 + 갑옷 7 + 활 2(HP2)
        // ============================================================
        private static StageData GetStage12()
        {
            StageData stage = new StageData
            {
                stageNumber = 12,
                chapterNumber = 2,
                chapterName = "사파이어 호수",
                turnLimit = 25,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 2,
                        description = "활 고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 10,
                        description = "고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 7,
                        description = "갑옷 고블린 7마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "루나: \"고블린 무리가 점점 더 많아지고 있어요!\"\n오라클리온: \"침착하게 특수 블록을 활용하렴.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "프리즘: \"루나 실력이 정말 늘었어!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 13: 활 3마리(HP3) — 활 HP 첫 강화 + 갑옷 증가
        // 턴 28 | 몽둥이 12 + 갑옷 9 + 활 3(HP3)
        // ============================================================
        private static StageData GetStage13()
        {
            StageData stage = new StageData
            {
                stageNumber = 13,
                chapterNumber = 2,
                chapterName = "사파이어 호수",
                turnLimit = 28,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 3,
                        description = "활 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 12,
                        description = "고블린 12마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 9,
                        description = "갑옷 고블린 9마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"고블린 궁수들이 더 강해졌다. 화살을 3번은 맞아야 쓰러진다.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "루나: \"점점 어려워지지만 해낼 수 있어요!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 14: 활 3마리(HP3) + 대규모 군단 — 갑옷 주력 상급
        // 턴 32 | 몽둥이 14 + 갑옷 12 + 활 3(HP3)
        // ============================================================
        private static StageData GetStage14()
        {
            StageData stage = new StageData
            {
                stageNumber = 14,
                chapterNumber = 2,
                chapterName = "사파이어 호수",
                turnLimit = 32,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 3,
                        description = "활 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 14,
                        description = "고블린 14마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 12,
                        description = "갑옷 고블린 12마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"갑옷 고블린이 더 많아졌어! 조심해 루나!\"\n루나: \"걱정 마, 특수 블록으로 해결할 수 있어!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"강해졌구나. 이제 마지막 관문이 남았다.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 15: 챕터 2 보스 — 활 4마리(HP4) + 최대 규모 군단
        // 턴 35 | 몽둥이 15 + 갑옷 15 + 활 4(HP4)
        // ============================================================
        private static StageData GetStage15()
        {
            StageData stage = new StageData
            {
                stageNumber = 15,
                chapterNumber = 2,
                chapterName = "사파이어 호수",
                turnLimit = 35,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 4,
                        description = "활 고블린 4마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 15,
                        description = "고블린 15마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 15,
                        description = "갑옷 고블린 15마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "오라클리온: \"이곳은 사파이어 호수의 심장부다. 고블린 군단장이 기다리고 있다.\"",
                    stageIntroDialogues = new[]
                    {
                        "루나: \"모든 힘을 다해 싸울게요!\"\n프리즘: \"우리가 함께라면 해낼 수 있어!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"대단하구나! 사파이어 호수를 해방시켰다!\"\n루나: \"이제 다음 모험이 기다리고 있겠죠?\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 16: 방패 고블린 등장 — 방패3 + 갑옷8 + 궁수2 + 기본12
        // 턴 30 | 챕터 3: 철벽 요새
        // ============================================================
        private static StageData GetStage16()
        {
            StageData stage = new StageData
            {
                stageNumber = 16,
                chapterNumber = 3,
                chapterName = "철벽 요새",
                turnLimit = 30,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 3,
                        description = "방패 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 12,
                        description = "고블린 12마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "루나: \"저 고블린... 방패를 들고 있어!\"\n프리즘: \"드릴이 통하지 않을 수도 있어. 조심해!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "루나: \"방패도 결국 깨지는구나!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 17: 방패+갑옷 혼합 — 방패4 + 갑옷10 + 궁수3 + 기본13
        // 턴 33
        // ============================================================
        private static StageData GetStage17()
        {
            StageData stage = new StageData
            {
                stageNumber = 17,
                chapterNumber = 3,
                chapterName = "철벽 요새",
                turnLimit = 33,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 4,
                        description = "방패 고블린 4마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 10,
                        description = "갑옷 고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 13,
                        description = "고블린 13마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"방패 고블린과 갑옷 고블린이 함께 오고 있어!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "루나: \"점점 강해지고 있는 것 같아!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 18: 대규모 혼합 — 방패6 + 갑옷12 + 궁수4 + 기본14
        // 턴 36
        // ============================================================
        private static StageData GetStage18()
        {
            StageData stage = new StageData
            {
                stageNumber = 18,
                chapterNumber = 3,
                chapterName = "철벽 요새",
                turnLimit = 36,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 6,
                        description = "방패 고블린 6마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 12,
                        description = "갑옷 고블린 12마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"요새의 방어가 점점 강해지고 있다. 침착하게 대응하거라.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "프리즘: \"거의 다 왔어! 요새의 핵심이 코앞이야!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 19: 강화 혼합 — 방패8 + 갑옷14 + 궁수4 + 기본16
        // 턴 40
        // ============================================================
        private static StageData GetStage19()
        {
            StageData stage = new StageData
            {
                stageNumber = 19,
                chapterNumber = 3,
                chapterName = "철벽 요새",
                turnLimit = 40,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 8,
                        description = "방패 고블린 8마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 16,
                        description = "고블린 16마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 14,
                        description = "갑옷 고블린 14마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "루나: \"여기서 포기할 수 없어! 끝까지 가보자!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"놀라운 성장이다. 최후의 관문만 남았구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 20: 챕터 3 보스 — 방패10 + 갑옷15 + 궁수5 + 기본18
        // 턴 45 | 최대 규모 전투
        // ============================================================
        private static StageData GetStage20()
        {
            StageData stage = new StageData
            {
                stageNumber = 20,
                chapterNumber = 3,
                chapterName = "철벽 요새",
                turnLimit = 45,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 10,
                        description = "방패 고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 15,
                        description = "갑옷 고블린 15마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 5,
                        description = "활 고블린 5마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "오라클리온: \"철벽 요새의 최심부다. 고블린 대장군이 모든 병력을 동원했다.\"",
                    stageIntroDialogues = new[]
                    {
                        "루나: \"모든 것을 쏟아붓겠어!\"\n프리즘: \"함께라면 어떤 방패도 뚫을 수 있어!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"철벽 요새를 함락시켰다! 정말 대단하구나!\"\n루나: \"다음엔 어떤 모험이 기다리고 있을까?\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }
        // ============================================================
        // 챕터 4: 화산 심장 — 4종 몬스터 전체 혼합 (21~30)
        // ============================================================

        // ============================================================
        // Stage 21: 4종 혼합 입문 — 기본10 + 갑옷8 + 궁수3 + 방패4 = 25
        // 턴 32
        // ============================================================
        private static StageData GetStage21()
        {
            StageData stage = new StageData
            {
                stageNumber = 21,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 32,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 10,
                        description = "고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 8,
                        description = "갑옷 고블린 8마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 3,
                        description = "활 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 4,
                        description = "방패 고블린 4마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    chapterIntroduction = "루나가 화산 심장부로 향한다.",
                    beforeStageCutscene = "프리즘: \"여기 열기가 장난이 아니야!\"\n오라클리온: \"화산 심장부다. 고블린 사천왕이 모든 병종을 동원했다.\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"이제 모든 유형의 고블린이 동시에 나타난다. 전략적으로 대응하거라.\"",
                        "루나: \"지금까지 배운 모든 것을 활용할게요!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"좋아. 4종 혼합 전투에 잘 적응했구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 22: 궁수 강화 — 기본12 + 갑옷10 + 궁수5 + 방패5 = 32
        // 턴 35
        // ============================================================
        private static StageData GetStage22()
        {
            StageData stage = new StageData
            {
                stageNumber = 22,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 35,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 12,
                        description = "고블린 12마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 10,
                        description = "갑옷 고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 5,
                        description = "활 고블린 5마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 5,
                        description = "방패 고블린 5마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"활 고블린이 더 많아졌어! 뒤에서 계속 쏘고 있어!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "루나: \"궁수들도 문제없어!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 23: 방패 요새 — 기본8 + 갑옷12 + 궁수4 + 방패8 = 32
        // 턴 37 | 방패 비율 급증
        // ============================================================
        private static StageData GetStage23()
        {
            StageData stage = new StageData
            {
                stageNumber = 23,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 37,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 8,
                        description = "방패 고블린 8마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 12,
                        description = "갑옷 고블린 12마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 8,
                        description = "고블린 8마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 4,
                        description = "활 고블린 4마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"방패 고블린이 전면에 나섰다. 드론으로 직접 타격해야 한다.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "프리즘: \"방패를 이렇게 많이 부수다니, 대단해!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 24: 궁수 대공세 — 기본14 + 갑옷8 + 궁수7 + 방패6 = 35
        // 턴 38 | 궁수 최다 등장
        // ============================================================
        private static StageData GetStage24()
        {
            StageData stage = new StageData
            {
                stageNumber = 24,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 38,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 7,
                        description = "활 고블린 7마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 14,
                        description = "고블린 14마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 8,
                        description = "갑옷 고블린 8마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 6,
                        description = "방패 고블린 6마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"사방에서 화살이 날아와! 궁수들이 엄청 많아!\"\n루나: \"드릴과 드론으로 하나씩 처리할게!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"궁수 연대를 돌파했구나. 훌륭하다.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 25: 중간 보스 — 기본16 + 갑옷14 + 궁수6 + 방패8 = 44
        // 턴 42 | 보스 스테이지
        // ============================================================
        private static StageData GetStage25()
        {
            StageData stage = new StageData
            {
                stageNumber = 25,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 42,
                difficulty = 3,
                isBossStage = true,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 16,
                        description = "고블린 16마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 14,
                        description = "갑옷 고블린 14마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 6,
                        description = "활 고블린 6마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 8,
                        description = "방패 고블린 8마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 화산의 중심부에 도달한다.\n거대한 용암 폭포 앞에 고블린 사천왕 중 하나가 서 있다!\n프리즘: \"저건... 화염 장군이야!\"",
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"화염 장군이 모든 병종을 이끌고 있다. 전력을 다해야 한다.\"",
                        "루나: \"여기서 물러설 순 없어요!\""
                    },
                    stageClearCutscene = "화염 장군이 쓰러지며 용암 폭포가 잠잠해진다.\n루나: \"하나 쓰러뜨렸어!\"\n프리즘: \"하지만 아직 더 깊은 곳에 더 강한 적이 있어...\"",
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"잘 싸웠다. 하지만 진짜 시련은 이제부터다.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0],
                rewards = new RewardData
                {
                    baseExperience = 200,
                    comboReward = 60,
                    perfectClearReward = 120,
                    badgeReward = "화염 장군 격파"
                }
            };

            return stage;
        }

        // ============================================================
        // Stage 26: 갑옷 탱커 — 기본10 + 갑옷18 + 궁수5 + 방패7 = 40
        // 턴 40 | 갑옷 주력
        // ============================================================
        private static StageData GetStage26()
        {
            StageData stage = new StageData
            {
                stageNumber = 26,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 40,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 18,
                        description = "갑옷 고블린 18마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 10,
                        description = "고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 7,
                        description = "방패 고블린 7마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 5,
                        description = "활 고블린 5마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"갑옷 고블린이 벽처럼 밀려온다! 체력이 장난 아니야!\"\n오라클리온: \"폭탄과 드릴을 적극 활용하거라.\""
                    },
                    stageClearDialogues = new[]
                    {
                        "루나: \"아무리 단단해도 결국 무너지는 법이지!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 27: 방패+궁수 연합 — 기본12 + 갑옷12 + 궁수6 + 방패10 = 40
        // 턴 42 | 방패 대거 투입
        // ============================================================
        private static StageData GetStage27()
        {
            StageData stage = new StageData
            {
                stageNumber = 27,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 42,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 10,
                        description = "방패 고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 12,
                        description = "고블린 12마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 12,
                        description = "갑옷 고블린 12마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 6,
                        description = "활 고블린 6마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "오라클리온: \"방패병이 전면을 막고 궁수가 후방에서 지원한다. 최악의 조합이다.\"",
                        "루나: \"드론으로 방패를 먼저 부수고, 드릴로 궁수를 노릴게요!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "프리즘: \"완벽한 전략이었어!\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 28: 궁수+갑옷 연합 — 기본14 + 갑옷14 + 궁수8 + 방패8 = 44
        // 턴 44 | 궁수 최대 + 균형 편성
        // ============================================================
        private static StageData GetStage28()
        {
            StageData stage = new StageData
            {
                stageNumber = 28,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 44,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 14,
                        description = "고블린 14마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 14,
                        description = "갑옷 고블린 14마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 8,
                        description = "활 고블린 8마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 8,
                        description = "방패 고블린 8마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    stageIntroDialogues = new[]
                    {
                        "프리즘: \"모든 종류의 고블린이 균형 있게 나온다! 방심하면 안 돼!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"이 정도 전투를 해내다니, 놀랍구나.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 29: 최강 군단 — 기본16 + 갑옷16 + 궁수7 + 방패10 = 49
        // 턴 48 | 최대 규모
        // ============================================================
        private static StageData GetStage29()
        {
            StageData stage = new StageData
            {
                stageNumber = 29,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 48,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 16,
                        description = "고블린 16마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 16,
                        description = "갑옷 고블린 16마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 10,
                        description = "방패 고블린 10마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 7,
                        description = "활 고블린 7마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "화산 깊은 곳에서 땅이 울린다.\n오라클리온: \"마왕의 친위대다. 최정예 고블린 군단이 모두 집결했다.\"",
                    stageIntroDialogues = new[]
                    {
                        "루나: \"이 많은 고블린을... 해낼 수 있을까?\"\n프리즘: \"지금까지 해온 것처럼 하면 돼!\""
                    },
                    stageClearDialogues = new[]
                    {
                        "오라클리온: \"놀랍다... 최강 군단을 격파했다. 이제 마지막 관문뿐이다.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 30: 최종 보스 — 기본18 + 갑옷18 + 궁수8 + 방패12 = 56
        // 턴 52 | 챕터 4 보스
        // ============================================================
        private static StageData GetStage30()
        {
            StageData stage = new StageData
            {
                stageNumber = 30,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 52,
                difficulty = 3,
                isBossStage = true,
                missions = new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 18,
                        description = "고블린 18마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 18,
                        description = "갑옷 고블린 18마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 12,
                        description = "방패 고블린 12마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 8,
                        description = "활 고블린 8마리 처치"
                    }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = new StoryData
                {
                    beforeStageCutscene = "루나가 화산의 최심부, 마그마 왕좌에 도달한다.\n거대한 마왕 고블린이 일어선다!\n프리즘: \"저건... 고블린 마왕이야!\"\n오라클리온: \"루나, 이것이 최후의 전투다. 모든 것을 걸어라!\"",
                    stageIntroDialogues = new[]
                    {
                        "루나: \"여기까지 왔는데 물러설 순 없어! 모든 걸 쏟아붓겠어!\"\n프리즘: \"우리가 함께라면 이길 수 있어!\""
                    },
                    stageClearCutscene = "고블린 마왕이 쓰러지며 화산이 잠잠해진다.\n마그마가 식으며 크리스탈이 피어오른다.\n루나: \"해냈어... 정말로 해냈어!\"\n프리즘: \"루나, 넌 정말 최고야!\"\n오라클리온: \"축하한다, 루나. 넌 진정한 크리스탈 수호자가 되었다.\"\n오라클리온: \"하지만 이것은 끝이 아니다. 새로운 모험이 너를 기다리고 있을 것이다...\"",
                    stageClearDialogues = new[]
                    {
                        "루나: \"다음엔 어떤 모험이 기다리고 있을까요?\"\n오라클리온: \"그건 네가 직접 찾아가야 할 길이다, 수호자여.\""
                    }
                },
                tutorialFlags = new TutorialFlag[0],
                rewards = new RewardData
                {
                    baseExperience = 300,
                    comboReward = 80,
                    perfectClearReward = 200,
                    badgeReward = "화산 심장의 수호자"
                }
            };

            return stage;
        }
    }
}

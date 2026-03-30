using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 미션 1: 고블린 소탕 - Stage 1~70 데이터
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
                { 30, GetStage30() },
                { 31, GetStage31() },
                { 32, GetStage32() },
                { 33, GetStage33() },
                { 34, GetStage34() },
                { 35, GetStage35() },
                { 36, GetStage36() },
                { 37, GetStage37() },
                { 38, GetStage38() },
                { 39, GetStage39() },
                { 40, GetStage40() },
                { 41, GetStage41() },
                { 42, GetStage42() },
                { 43, GetStage43() },
                { 44, GetStage44() },
                { 45, GetStage45() },
                { 46, GetStage46() },
                { 47, GetStage47() },
                { 48, GetStage48() },
                { 49, GetStage49() },
                { 50, GetStage50() },
                { 51, GetStage51() },
                { 52, GetStage52() },
                { 53, GetStage53() },
                { 54, GetStage54() },
                { 55, GetStage55() },
                { 56, GetStage56() },
                { 57, GetStage57() },
                { 58, GetStage58() },
                { 59, GetStage59() },
                { 60, GetStage60() },
                { 61, GetStage61() },
                { 62, GetStage62() },
                { 63, GetStage63() },
                { 64, GetStage64() },
                { 65, GetStage65() },
                { 66, GetStage66() },
                { 67, GetStage67() },
                { 68, GetStage68() },
                { 69, GetStage69() },
                { 70, GetStage70() }
            };
        }

        // ============================================================
        // Stage 1: 기본5 = 5
        // 턴 15 | 미션처치 5
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
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 5,
                        description = "고블린 5마리 처치"
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
        // Stage 2: 기본8 = 8
        // 턴 17 | 미션처치 8
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
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 8,
                        description = "고블린 8마리 처치"
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
        // Stage 3: 기본6 + 갑옷2 = 8
        // 턴 18 | 미션처치 8
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
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 6,
                        description = "고블린 6마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
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
        // Stage 4: 기본9 = 9
        // 턴 20 | 미션처치 9
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
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 9,
                        description = "고블린 9마리 처치"
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
        // Stage 5: 기본8 + 갑옷1 = 9
        // 턴 22 | 미션처치 9
        // ============================================================
        private static StageData GetStage5()
        {
            StageData stage = new StageData
            {
                stageNumber = 5,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 22,
                difficulty = 1,
                missions =                 new[]
                {
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
                        targetCount = 1,
                        description = "갑옷 고블린 1마리 처치"
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
        // Stage 6: 기본10 + 갑옷2 = 12
        // 턴 25 | 미션처치 12
        // ============================================================
        private static StageData GetStage6()
        {
            StageData stage = new StageData
            {
                stageNumber = 6,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 25,
                difficulty = 1,
                missions =                 new[]
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
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
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
        // Stage 7: 기본8 + 갑옷4 = 12
        // 턴 27 | 미션처치 12
        // ============================================================
        private static StageData GetStage7()
        {
            StageData stage = new StageData
            {
                stageNumber = 7,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 27,
                difficulty = 1,
                missions =                 new[]
                {
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 8: 기본9 + 갑옷3 + 궁수2 = 14
        // 턴 30 | 미션처치 14
        // ============================================================
        private static StageData GetStage8()
        {
            StageData stage = new StageData
            {
                stageNumber = 8,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 30,
                difficulty = 1,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 9,
                        description = "고블린 9마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 3,
                        description = "갑옷 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 2,
                        description = "활 고블린 2마리 처치"
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
        // Stage 9: 기본6 + 갑옷5 + 궁수3 = 14
        // 턴 30 | 미션처치 14
        // ============================================================
        private static StageData GetStage9()
        {
            StageData stage = new StageData
            {
                stageNumber = 9,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 30,
                difficulty = 1,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 6,
                        description = "고블린 6마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 3,
                        description = "활 고블린 3마리 처치"
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
        // Stage 10: 기본8 + 갑옷5 + 궁수3 + 방패1 = 17
        // 턴 30 | 미션처치 17
        // ============================================================
        private static StageData GetStage10()
        {
            StageData stage = new StageData
            {
                stageNumber = 10,
                chapterNumber = 1,
                chapterName = "크리스탈 숲",
                turnLimit = 30,
                difficulty = 1,
                missions =                 new[]
                {
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
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
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
                        targetCount = 1,
                        description = "방패 고블린 1마리 처치"
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
        // Stage 11: 궁수5 = 5
        // 턴 15 | 미션처치 5
        // ============================================================
        private static StageData GetStage11()
        {
            StageData stage = new StageData
            {
                stageNumber = 11,
                chapterNumber = 2,
                chapterName = "안개의 골짜기",
                turnLimit = 15,
                difficulty = 1,
                missions =                 new[]
                {
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 12: 기본2 + 궁수6 = 8
        // 턴 20 | 미션처치 8
        // ============================================================
        private static StageData GetStage12()
        {
            StageData stage = new StageData
            {
                stageNumber = 12,
                chapterNumber = 2,
                chapterName = "안개의 골짜기",
                turnLimit = 20,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 13: 기본6 + 갑옷2 + 궁수1 = 9
        // 턴 22 | 미션처치 9
        // ============================================================
        private static StageData GetStage13()
        {
            StageData stage = new StageData
            {
                stageNumber = 13,
                chapterNumber = 2,
                chapterName = "안개의 골짜기",
                turnLimit = 22,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 6,
                        description = "고블린 6마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArcherGoblin,
                        targetCount = 1,
                        description = "활 고블린 1마리 처치"
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
        // Stage 14: 갑옷2 + 궁수7 = 9
        // 턴 25 | 미션처치 9
        // ============================================================
        private static StageData GetStage14()
        {
            StageData stage = new StageData
            {
                stageNumber = 14,
                chapterNumber = 2,
                chapterName = "안개의 골짜기",
                turnLimit = 25,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 15: 기본2 + 갑옷3 + 궁수6 + 방패1 = 12
        // 턴 30 | 미션처치 12
        // ============================================================
        private static StageData GetStage15()
        {
            StageData stage = new StageData
            {
                stageNumber = 15,
                chapterNumber = 2,
                chapterName = "안개의 골짜기",
                turnLimit = 30,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 3,
                        description = "갑옷 고블린 3마리 처치"
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
                        targetCount = 1,
                        description = "방패 고블린 1마리 처치"
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
        // Stage 16: 기본2 + 방패6 = 8
        // 턴 22 | 미션처치 8
        // ============================================================
        private static StageData GetStage16()
        {
            StageData stage = new StageData
            {
                stageNumber = 16,
                chapterNumber = 3,
                chapterName = "얼어붙은 성채",
                turnLimit = 22,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 17: 기본2 + 갑옷2 + 방패5 = 9
        // 턴 25 | 미션처치 9
        // ============================================================
        private static StageData GetStage17()
        {
            StageData stage = new StageData
            {
                stageNumber = 17,
                chapterNumber = 3,
                chapterName = "얼어붙은 성채",
                turnLimit = 25,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 18: 기본3 + 궁수2 + 방패4 = 9
        // 턴 25 | 미션처치 9
        // ============================================================
        private static StageData GetStage18()
        {
            StageData stage = new StageData
            {
                stageNumber = 18,
                chapterNumber = 3,
                chapterName = "얼어붙은 성채",
                turnLimit = 25,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 3,
                        description = "고블린 3마리 처치"
                    },
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
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 4,
                        description = "방패 고블린 4마리 처치"
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
        // Stage 19: 갑옷2 + 궁수3 + 방패7 = 12
        // 턴 30 | 미션처치 12
        // ============================================================
        private static StageData GetStage19()
        {
            StageData stage = new StageData
            {
                stageNumber = 19,
                chapterNumber = 3,
                chapterName = "얼어붙은 성채",
                turnLimit = 30,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
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
                        targetCount = 7,
                        description = "방패 고블린 7마리 처치"
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
        // Stage 20: 기본2 + 갑옷5 + 궁수2 + 방패5 = 14
        // 턴 32 | 미션처치 14
        // ============================================================
        private static StageData GetStage20()
        {
            StageData stage = new StageData
            {
                stageNumber = 20,
                chapterNumber = 3,
                chapterName = "얼어붙은 성채",
                turnLimit = 32,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
                    },
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
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 5,
                        description = "방패 고블린 5마리 처치"
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
        // Stage 21: 갑옷6 + 방패6 = 12
        // 턴 30 | 미션처치 12
        // ============================================================
        private static StageData GetStage21()
        {
            StageData stage = new StageData
            {
                stageNumber = 21,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 30,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 6,
                        description = "갑옷 고블린 6마리 처치"
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 22: 기본2 + 갑옷5 + 궁수3 + 방패4 = 14
        // 턴 32 | 미션처치 14
        // ============================================================
        private static StageData GetStage22()
        {
            StageData stage = new StageData
            {
                stageNumber = 22,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 32,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 23: 기본3 + 갑옷5 + 궁수5 + 방패4 = 17
        // 턴 35 | 미션처치 17
        // ============================================================
        private static StageData GetStage23()
        {
            StageData stage = new StageData
            {
                stageNumber = 23,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 35,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 3,
                        description = "고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
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
                        targetCount = 4,
                        description = "방패 고블린 4마리 처치"
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
        // Stage 24: 기본2 + 갑옷9 + 궁수3 + 방패4 = 18
        // 턴 38 | 미션처치 18
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
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 9,
                        description = "갑옷 고블린 9마리 처치"
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
                storyData = null,
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
        // Stage 26: 기본2 + 갑옷11 + 궁수2 + 방패2 = 17
        // 턴 35 | 미션처치 17
        // ============================================================
        private static StageData GetStage26()
        {
            StageData stage = new StageData
            {
                stageNumber = 26,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 35,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 11,
                        description = "갑옷 고블린 11마리 처치"
                    },
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
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 2,
                        description = "방패 고블린 2마리 처치"
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
        // Stage 27: 갑옷9 + 방패9 = 18
        // 턴 38 | 미션처치 18
        // ============================================================
        private static StageData GetStage27()
        {
            StageData stage = new StageData
            {
                stageNumber = 27,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 38,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 9,
                        description = "갑옷 고블린 9마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 9,
                        description = "방패 고블린 9마리 처치"
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
        // Stage 28: 기본3 + 갑옷8 + 궁수4 + 방패6 = 21
        // 턴 40 | 미션처치 21
        // ============================================================
        private static StageData GetStage28()
        {
            StageData stage = new StageData
            {
                stageNumber = 28,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 40,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 3,
                        description = "고블린 3마리 처치"
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
                        targetCount = 4,
                        description = "활 고블린 4마리 처치"
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
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };

            return stage;
        }

        // ============================================================
        // Stage 29: 기본3 + 갑옷6 + 궁수6 + 방패8 = 23
        // 턴 42 | 미션처치 23
        // ============================================================
        private static StageData GetStage29()
        {
            StageData stage = new StageData
            {
                stageNumber = 29,
                chapterNumber = 4,
                chapterName = "화산 심장",
                turnLimit = 42,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 3,
                        description = "고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 6,
                        description = "갑옷 고블린 6마리 처치"
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
                storyData = null,
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

        // ============================================================
        // Stage 31: 기본6 + 폭탄2 = 8
        // 턴 30 | 미션처치 8
        // ============================================================
        private static StageData GetStage31()
        {
            StageData stage = new StageData
            {
                stageNumber = 31,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 30,
                difficulty = 1,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 6,
                        description = "고블린 6마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 2,
                        description = "폭탄 고블린 2마리 처치"
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
        // Stage 32: 기본4 + 폭탄5 = 9
        // 턴 27 | 미션처치 9
        // ============================================================
        private static StageData GetStage32()
        {
            StageData stage = new StageData
            {
                stageNumber = 32,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 27,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 4,
                        description = "고블린 4마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 5,
                        description = "폭탄 고블린 5마리 처치"
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
        // Stage 33: 궁수3 + 폭탄6 = 9
        // 턴 27 | 미션처치 9
        // ============================================================
        private static StageData GetStage33()
        {
            StageData stage = new StageData
            {
                stageNumber = 33,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 27,
                difficulty = 2,
                missions =                 new[]
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 34: 갑옷3 + 폭탄6 = 9
        // 턴 23 | 미션처치 9
        // ============================================================
        private static StageData GetStage34()
        {
            StageData stage = new StageData
            {
                stageNumber = 34,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 23,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 3,
                        description = "갑옷 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 35: 기본3 + 궁수3 + 방패3 + 폭탄3 = 12
        // 턴 22 | 미션처치 12
        // ============================================================
        private static StageData GetStage35()
        {
            StageData stage = new StageData
            {
                stageNumber = 35,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 22,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 3,
                        description = "고블린 3마리 처치"
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
                        targetCount = 3,
                        description = "방패 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 3,
                        description = "폭탄 고블린 3마리 처치"
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
        // Stage 36: 기본3 + 갑옷2 + 폭탄7 = 12
        // 턴 30 | 미션처치 12
        // ============================================================
        private static StageData GetStage36()
        {
            StageData stage = new StageData
            {
                stageNumber = 36,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 30,
                difficulty = 1,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 3,
                        description = "고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 7,
                        description = "폭탄 고블린 7마리 처치"
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
        // Stage 37: 갑옷2 + 궁수2 + 폭탄8 = 12
        // 턴 26 | 미션처치 12
        // ============================================================
        private static StageData GetStage37()
        {
            StageData stage = new StageData
            {
                stageNumber = 37,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 26,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 2,
                        description = "갑옷 고블린 2마리 처치"
                    },
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 8,
                        description = "폭탄 고블린 8마리 처치"
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
        // Stage 38: 방패6 + 폭탄6 = 12
        // 턴 25 | 미션처치 12
        // ============================================================
        private static StageData GetStage38()
        {
            StageData stage = new StageData
            {
                stageNumber = 38,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 25,
                difficulty = 2,
                missions =                 new[]
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 39: 갑옷3 + 방패3 + 폭탄6 = 12
        // 턴 22 | 미션처치 12
        // ============================================================
        private static StageData GetStage39()
        {
            StageData stage = new StageData
            {
                stageNumber = 39,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 22,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 3,
                        description = "갑옷 고블린 3마리 처치"
                    },
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 40: 기본2 + 갑옷3 + 방패5 + 폭탄4 = 14
        // 턴 20 | 미션처치 14
        // ============================================================
        private static StageData GetStage40()
        {
            StageData stage = new StageData
            {
                stageNumber = 40,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 20,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 3,
                        description = "갑옷 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 5,
                        description = "방패 고블린 5마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 4,
                        description = "폭탄 고블린 4마리 처치"
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
        // Stage 41: 갑옷5 + 방패3 + 폭탄6 = 14
        // 턴 30 | 미션처치 14
        // ============================================================
        private static StageData GetStage41()
        {
            StageData stage = new StageData
            {
                stageNumber = 41,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 30,
                difficulty = 1,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
                    },
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 42: 기본2 + 궁수5 + 방패3 + 폭탄4 = 14
        // 턴 26 | 미션처치 14
        // ============================================================
        private static StageData GetStage42()
        {
            StageData stage = new StageData
            {
                stageNumber = 42,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 26,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
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
                        targetCount = 3,
                        description = "방패 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 4,
                        description = "폭탄 고블린 4마리 처치"
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
        // Stage 43: 기본2 + 갑옷3 + 폭탄9 = 14
        // 턴 25 | 미션처치 14
        // ============================================================
        private static StageData GetStage43()
        {
            StageData stage = new StageData
            {
                stageNumber = 43,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 25,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 3,
                        description = "갑옷 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 9,
                        description = "폭탄 고블린 9마리 처치"
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
        // Stage 44: 궁수2 + 방패6 + 폭탄6 = 14
        // 턴 21 | 미션처치 14
        // ============================================================
        private static StageData GetStage44()
        {
            StageData stage = new StageData
            {
                stageNumber = 44,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 21,
                difficulty = 3,
                missions =                 new[]
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
                        targetEnemyType = EnemyType.ShieldGoblin,
                        targetCount = 6,
                        description = "방패 고블린 6마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 45: 갑옷5 + 궁수3 + 방패3 + 폭탄6 = 17
        // 턴 20 | 미션처치 17
        // ============================================================
        private static StageData GetStage45()
        {
            StageData stage = new StageData
            {
                stageNumber = 45,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 20,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
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
                        targetCount = 3,
                        description = "방패 고블린 3마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 46: 갑옷5 + 방패3 + 폭탄9 = 17
        // 턴 30 | 미션처치 17
        // ============================================================
        private static StageData GetStage46()
        {
            StageData stage = new StageData
            {
                stageNumber = 46,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 30,
                difficulty = 1,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
                    },
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 9,
                        description = "폭탄 고블린 9마리 처치"
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
        // Stage 47: 갑옷5 + 방패6 + 폭탄6 = 17
        // 턴 25 | 미션처치 17
        // ============================================================
        private static StageData GetStage47()
        {
            StageData stage = new StageData
            {
                stageNumber = 47,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 25,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 5,
                        description = "갑옷 고블린 5마리 처치"
                    },
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 48: 기본2 + 갑옷6 + 방패4 + 폭탄6 = 18
        // 턴 24 | 미션처치 18
        // ============================================================
        private static StageData GetStage48()
        {
            StageData stage = new StageData
            {
                stageNumber = 48,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 24,
                difficulty = 2,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.Goblin,
                        targetCount = 2,
                        description = "고블린 2마리 처치"
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 6,
                        description = "갑옷 고블린 6마리 처치"
                    },
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 6,
                        description = "폭탄 고블린 6마리 처치"
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
        // Stage 49: 갑옷4 + 방패3 + 폭탄11 = 18
        // 턴 20 | 미션처치 18
        // ============================================================
        private static StageData GetStage49()
        {
            StageData stage = new StageData
            {
                stageNumber = 49,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 20,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 4,
                        description = "갑옷 고블린 4마리 처치"
                    },
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
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 11,
                        description = "폭탄 고블린 11마리 처치"
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
        // Stage 50: 갑옷6 + 궁수3 + 방패4 + 폭탄8 = 21
        // 턴 18 | 미션처치 21
        // ============================================================
        private static StageData GetStage50()
        {
            StageData stage = new StageData
            {
                stageNumber = 50,
                chapterNumber = 5,
                chapterName = "폭염의 화약고",
                turnLimit = 18,
                difficulty = 3,
                missions =                 new[]
                {
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.ArmoredGoblin,
                        targetCount = 6,
                        description = "갑옷 고블린 6마리 처치"
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
                    },
                    new MissionData
                    {
                        type = MissionType.RemoveEnemy,
                        targetEnemyType = EnemyType.BombGoblin,
                        targetCount = 8,
                        description = "폭탄 고블린 8마리 처치"
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
        // Stage 51: 힐러2 + 기본3 + 갑옷2 + 폭탄1 = 8
        // 턴 30 | 미션처치 8
        // ============================================================
        private static StageData GetStage51()
        {
            StageData stage = new StageData
            {
                stageNumber = 51,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 30,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.Goblin, targetCount = 3, description = "고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 1, description = "폭탄 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 52: 힐러2 + 갑옷3 + 방패2 + 폭탄2 = 9
        // 턴 27 | 미션처치 9
        // ============================================================
        private static StageData GetStage52()
        {
            StageData stage = new StageData
            {
                stageNumber = 52,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 27,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 3, description = "갑옷 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 2, description = "폭탄 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 53: 힐러3 + 폭탄3 + 궁수2 + 기본1 = 9
        // 턴 26 | 미션처치 9
        // ============================================================
        private static StageData GetStage53()
        {
            StageData stage = new StageData
            {
                stageNumber = 53,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 26,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.Goblin, targetCount = 1, description = "고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 2, description = "활 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 3, description = "폭탄 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 3, description = "힐러 고블린 3마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 54: 힐러2 + 폭탄4 + 방패2 + 갑옷2 = 10
        // 턴 22 | 미션처치 10
        // ============================================================
        private static StageData GetStage54()
        {
            StageData stage = new StageData
            {
                stageNumber = 54,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 22,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 4, description = "폭탄 고블린 4마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 55: 힐러3 + 폭탄4 + 방패2 + 궁수2 + 갑옷1 = 12
        // 턴 20 | 미션처치 12
        // ============================================================
        private static StageData GetStage55()
        {
            StageData stage = new StageData
            {
                stageNumber = 55,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 20,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 1, description = "갑옷 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 2, description = "활 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 4, description = "폭탄 고블린 4마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 3, description = "힐러 고블린 3마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 56: 힐러2 + 기본3 + 갑옷2 + 궁수2 + 폭탄2 = 11
        // 턴 30 | 미션처치 11
        // ============================================================
        private static StageData GetStage56()
        {
            StageData stage = new StageData
            {
                stageNumber = 56,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 30,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.Goblin, targetCount = 3, description = "고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 2, description = "활 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 2, description = "폭탄 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 57: 힐러2 + 폭탄4 + 갑옷3 + 방패2 = 11
        // 턴 25 | 미션처치 11
        // ============================================================
        private static StageData GetStage57()
        {
            StageData stage = new StageData
            {
                stageNumber = 57,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 25,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 3, description = "갑옷 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 4, description = "폭탄 고블린 4마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 58: 힐러3 + 폭탄4 + 궁수3 + 방패2 = 12
        // 턴 24 | 미션처치 12
        // ============================================================
        private static StageData GetStage58()
        {
            StageData stage = new StageData
            {
                stageNumber = 58,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 24,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 3, description = "활 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 4, description = "폭탄 고블린 4마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 3, description = "힐러 고블린 3마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 59: 힐러3 + 폭탄5 + 갑옷3 + 방패2 = 13
        // 턴 20 | 미션처치 13
        // ============================================================
        private static StageData GetStage59()
        {
            StageData stage = new StageData
            {
                stageNumber = 59,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 20,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 3, description = "갑옷 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 5, description = "폭탄 고블린 5마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 3, description = "힐러 고블린 3마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 60: 힐러4 + 폭탄5 + 갑옷3 + 방패3 + 궁수2 = 17
        // 턴 18 | 미션처치 17
        // ============================================================
        private static StageData GetStage60()
        {
            StageData stage = new StageData
            {
                stageNumber = 60,
                chapterNumber = 6,
                chapterName = "치유의 늪",
                turnLimit = 18,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 3, description = "갑옷 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 2, description = "활 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 3, description = "방패 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 5, description = "폭탄 고블린 5마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 4, description = "힐러 고블린 4마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 61: 헤비1 + 기본3 + 갑옷2 + 힐러1 = 7
        // 턴 30 | 미션처치 7
        // ============================================================
        private static StageData GetStage61()
        {
            StageData stage = new StageData
            {
                stageNumber = 61,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 30,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 1, description = "헤비 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.Goblin, targetCount = 3, description = "고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 1, description = "힐러 고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 62: 헤비1 + 폭탄3 + 갑옷2 + 힐러1 + 궁수1 = 8
        // 턴 27 | 미션처치 8
        // ============================================================
        private static StageData GetStage62()
        {
            StageData stage = new StageData
            {
                stageNumber = 62,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 27,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 1, description = "헤비 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 3, description = "폭탄 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 1, description = "힐러 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 1, description = "활 고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 63: 헤비1 + 폭탄3 + 방패2 + 힐러2 + 기본1 = 9
        // 턴 26 | 미션처치 9
        // ============================================================
        private static StageData GetStage63()
        {
            StageData stage = new StageData
            {
                stageNumber = 63,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 26,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 1, description = "헤비 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 3, description = "폭탄 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.Goblin, targetCount = 1, description = "고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 64: 헤비2 + 폭탄3 + 갑옷2 + 힐러1 + 방패1 = 9
        // 턴 22 | 미션처치 9
        // ============================================================
        private static StageData GetStage64()
        {
            StageData stage = new StageData
            {
                stageNumber = 64,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 22,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 2, description = "헤비 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 3, description = "폭탄 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 1, description = "힐러 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 1, description = "방패 고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 65: 헤비2 + 폭탄4 + 방패2 + 힐러2 + 궁수2 = 12
        // 턴 20 | 미션처치 12
        // ============================================================
        private static StageData GetStage65()
        {
            StageData stage = new StageData
            {
                stageNumber = 65,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 20,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 2, description = "헤비 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 4, description = "폭탄 고블린 4마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 2, description = "활 고블린 2마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 66: 헤비1 + 기본3 + 갑옷2 + 궁수2 + 폭탄2 + 힐러1 = 11
        // 턴 30 | 미션처치 11
        // ============================================================
        private static StageData GetStage66()
        {
            StageData stage = new StageData
            {
                stageNumber = 66,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 30,
                difficulty = 1,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 1, description = "헤비 고블린 1마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.Goblin, targetCount = 3, description = "고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 2, description = "활 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 2, description = "폭탄 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 1, description = "힐러 고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 67: 헤비2 + 폭탄4 + 갑옷2 + 방패2 + 힐러1 = 11
        // 턴 25 | 미션처치 11
        // ============================================================
        private static StageData GetStage67()
        {
            StageData stage = new StageData
            {
                stageNumber = 67,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 25,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 2, description = "헤비 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 4, description = "폭탄 고블린 4마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 2, description = "갑옷 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 1, description = "힐러 고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 68: 헤비2 + 폭탄4 + 궁수3 + 방패2 + 힐러1 = 12
        // 턴 24 | 미션처치 12
        // ============================================================
        private static StageData GetStage68()
        {
            StageData stage = new StageData
            {
                stageNumber = 68,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 24,
                difficulty = 2,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 2, description = "헤비 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 4, description = "폭탄 고블린 4마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 3, description = "활 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 1, description = "힐러 고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 69: 헤비2 + 폭탄5 + 갑옷3 + 방패2 + 힐러1 = 13
        // 턴 20 | 미션처치 13
        // ============================================================
        private static StageData GetStage69()
        {
            StageData stage = new StageData
            {
                stageNumber = 69,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 20,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 2, description = "헤비 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 5, description = "폭탄 고블린 5마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 3, description = "갑옷 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 2, description = "방패 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 1, description = "힐러 고블린 1마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

        // ============================================================
        // Stage 70: 헤비3 + 폭탄5 + 갑옷3 + 방패3 + 힐러2 + 궁수2 = 18
        // 턴 18 | 미션처치 18
        // ============================================================
        private static StageData GetStage70()
        {
            StageData stage = new StageData
            {
                stageNumber = 70,
                chapterNumber = 7,
                chapterName = "거인의 둥지",
                turnLimit = 18,
                difficulty = 3,
                missions = new[]
                {
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HeavyGoblin, targetCount = 3, description = "헤비 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.BombGoblin, targetCount = 5, description = "폭탄 고블린 5마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArmoredGoblin, targetCount = 3, description = "갑옷 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ShieldGoblin, targetCount = 3, description = "방패 고블린 3마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.HealerGoblin, targetCount = 2, description = "힐러 고블린 2마리 처치" },
                    new MissionData { type = MissionType.RemoveEnemy, targetEnemyType = EnemyType.ArcherGoblin, targetCount = 2, description = "활 고블린 2마리 처치" }
                },
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = null,
                tutorialFlags = new TutorialFlag[0]
            };
            return stage;
        }

    }
}

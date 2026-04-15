using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 미션 1: 고블린 소탕 - Stage 1~100 데이터
    /// 설계 원칙:
    ///   - 미션 리스트 3~15개 (순차 소환)
    ///   - 미션당 수량: 기본 몬스터(몽둥이/갑옷) 2~12, 특수 몬스터 1~3
    ///   - 1~3종 타입을 적절히 섞어 구성
    ///   - 동시 활성 미션 최대 4개 (MAX_ACTIVE_MISSIONS=4)
    ///   - 점진적 난이도 상승 + 변칙 레벨 (짧은 리스트/높은 수량)
    /// </summary>
    public static class Mission1StageData
    {
        // 헬퍼 — 미션 데이터 생성 간소화
        private static MissionData M(EnemyType enemy, int count, string desc)
        {
            return new MissionData
            {
                type = MissionType.RemoveEnemy,
                targetEnemyType = enemy,
                targetCount = count,
                description = desc
            };
        }

        // 약칭
        private static MissionData G(int n) => M(EnemyType.Goblin, n, $"고블린 {n}마리 처치");
        private static MissionData A(int n) => M(EnemyType.ArmoredGoblin, n, $"갑옷 고블린 {n}마리 처치");
        private static MissionData R(int n) => M(EnemyType.ArcherGoblin, n, $"활 고블린 {n}마리 처치");
        private static MissionData S(int n) => M(EnemyType.ShieldGoblin, n, $"방패 고블린 {n}마리 처치");
        private static MissionData B(int n) => M(EnemyType.BombGoblin, n, $"폭탄 고블린 {n}마리 처치");
        private static MissionData H(int n) => M(EnemyType.HealerGoblin, n, $"힐러 고블린 {n}마리 처치");
        private static MissionData V(int n) => M(EnemyType.HeavyGoblin, n, $"헤비 고블린 {n}마리 처치");
        private static MissionData W(int n) => M(EnemyType.WizardGoblin, n, $"마법사 고블린 {n}마리 처치");
        private static MissionData T(int n) => M(EnemyType.ThiefGoblin, n, $"도둑 고블린 {n}마리 처치");
        private static MissionData Wi(int n) => M(EnemyType.WitchGoblin, n, $"마녀 고블린 {n}마리 처치");

        // Lv2 약칭
        private static MissionData G2(int n) => M(EnemyType.GoblinLv2, n, $"엘리트 고블린 {n}마리 처치");
        private static MissionData A2(int n) => M(EnemyType.ArmoredGoblinLv2, n, $"엘리트 갑옷 {n}마리 처치");
        private static MissionData R2(int n) => M(EnemyType.ArcherGoblinLv2, n, $"엘리트 궁수 {n}마리 처치");
        private static MissionData S2(int n) => M(EnemyType.ShieldGoblinLv2, n, $"엘리트 방패 {n}마리 처치");

        /// <summary>
        /// 모든 미션 1 스테이지 데이터 반환
        /// </summary>
        public static Dictionary<int, StageData> GetAllMission1Stages()
        {
            var stages = new Dictionary<int, StageData>();
            for (int i = 1; i <= 150; i++)
                stages[i] = GetStage(i);
            return stages;
        }

        private static StageData GetStage(int n)
        {
            switch (n)
            {
                case 1: return GetStage1();
                case 2: return GetStage2();
                case 3: return GetStage3();
                case 4: return GetStage4();
                case 5: return GetStage5();
                case 6: return GetStage6();
                case 7: return GetStage7();
                case 8: return GetStage8();
                case 9: return GetStage9();
                case 10: return GetStage10();
                case 11: return GetStage11();
                case 12: return GetStage12();
                case 13: return GetStage13();
                case 14: return GetStage14();
                case 15: return GetStage15();
                case 16: return GetStage16();
                case 17: return GetStage17();
                case 18: return GetStage18();
                case 19: return GetStage19();
                case 20: return GetStage20();
                case 21: return GetStage21();
                case 22: return GetStage22();
                case 23: return GetStage23();
                case 24: return GetStage24();
                case 25: return GetStage25();
                case 26: return GetStage26();
                case 27: return GetStage27();
                case 28: return GetStage28();
                case 29: return GetStage29();
                case 30: return GetStage30();
                case 31: return GetStage31();
                case 32: return GetStage32();
                case 33: return GetStage33();
                case 34: return GetStage34();
                case 35: return GetStage35();
                case 36: return GetStage36();
                case 37: return GetStage37();
                case 38: return GetStage38();
                case 39: return GetStage39();
                case 40: return GetStage40();
                case 41: return GetStage41();
                case 42: return GetStage42();
                case 43: return GetStage43();
                case 44: return GetStage44();
                case 45: return GetStage45();
                case 46: return GetStage46();
                case 47: return GetStage47();
                case 48: return GetStage48();
                case 49: return GetStage49();
                case 50: return GetStage50();
                case 51: return GetStage51();
                case 52: return GetStage52();
                case 53: return GetStage53();
                case 54: return GetStage54();
                case 55: return GetStage55();
                case 56: return GetStage56();
                case 57: return GetStage57();
                case 58: return GetStage58();
                case 59: return GetStage59();
                case 60: return GetStage60();
                case 61: return GetStage61();
                case 62: return GetStage62();
                case 63: return GetStage63();
                case 64: return GetStage64();
                case 65: return GetStage65();
                case 66: return GetStage66();
                case 67: return GetStage67();
                case 68: return GetStage68();
                case 69: return GetStage69();
                case 70: return GetStage70();
                case 71: return GetStage71();
                case 72: return GetStage72();
                case 73: return GetStage73();
                case 74: return GetStage74();
                case 75: return GetStage75();
                case 76: return GetStage76();
                case 77: return GetStage77();
                case 78: return GetStage78();
                case 79: return GetStage79();
                case 80: return GetStage80();
                case 81: return GetStage81();
                case 82: return GetStage82();
                case 83: return GetStage83();
                case 84: return GetStage84();
                case 85: return GetStage85();
                case 86: return GetStage86();
                case 87: return GetStage87();
                case 88: return GetStage88();
                case 89: return GetStage89();
                case 90: return GetStage90();
                case 91: return GetStage91();
                case 92: return GetStage92();
                case 93: return GetStage93();
                case 94: return GetStage94();
                case 95: return GetStage95();
                case 96: return GetStage96();
                case 97: return GetStage97();
                case 98: return GetStage98();
                case 99: return GetStage99();
                case 100: return GetStage100();
                // === 엘리트 전장 (101-150) — Lv2 몬스터 등장 ===
                case 101: return GetStage101();
                case 102: return GetStage102();
                case 103: return GetStage103();
                case 104: return GetStage104();
                case 105: return GetStage105();
                case 106: return GetStage106();
                case 107: return GetStage107();
                case 108: return GetStage108();
                case 109: return GetStage109();
                case 110: return GetStage110();
                case 111: return GetStage111();
                case 112: return GetStage112();
                case 113: return GetStage113();
                case 114: return GetStage114();
                case 115: return GetStage115();
                case 116: return GetStage116();
                case 117: return GetStage117();
                case 118: return GetStage118();
                case 119: return GetStage119();
                case 120: return GetStage120();
                case 121: return GetStage121();
                case 122: return GetStage122();
                case 123: return GetStage123();
                case 124: return GetStage124();
                case 125: return GetStage125();
                case 126: return GetStage126();
                case 127: return GetStage127();
                case 128: return GetStage128();
                case 129: return GetStage129();
                case 130: return GetStage130();
                case 131: return GetStage131();
                case 132: return GetStage132();
                case 133: return GetStage133();
                case 134: return GetStage134();
                case 135: return GetStage135();
                case 136: return GetStage136();
                case 137: return GetStage137();
                case 138: return GetStage138();
                case 139: return GetStage139();
                case 140: return GetStage140();
                case 141: return GetStage141();
                case 142: return GetStage142();
                case 143: return GetStage143();
                case 144: return GetStage144();
                case 145: return GetStage145();
                case 146: return GetStage146();
                case 147: return GetStage147();
                case 148: return GetStage148();
                case 149: return GetStage149();
                case 150: return GetStage150();
                default: return GetStage1();
            }
        }

        // 스테이지 생성 헬퍼
        private static StageData Stg(int num, int ch, string chName, int turns, int diff, MissionData[] missions, StoryData story = null, int maxActive = 0)
        {
            return new StageData
            {
                stageNumber = num,
                chapterNumber = ch,
                chapterName = chName,
                turnLimit = turns,
                difficulty = diff,
                missions = missions,
                enemyPlacements = new EnemyPlacement[0],
                fixedBlockPlacements = new EnemyPlacement[0],
                storyData = story,
                tutorialFlags = new TutorialFlag[0],
                maxActiveMissions = maxActive
            };
        }

        // ============================================================
        // Chapter 1: 크리스탈 숲 (1-10) — 고블린만
        // 몽둥이 단독이므로 maxActive=1, 웨이브 단위로 순차 소환
        // 점진적 난이도: 웨이브 수·수량 증가, 변칙 레벨 포함
        // ============================================================

        private static StageData GetStage1()
        {
            // 튜토리얼: 소규모 3웨이브
            return Stg(1, 1, "크리스탈 숲", 12, 1,
                new[] { G(2), G(2), G(3) },
                new StoryData
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
                maxActive: 1);
        }

        private static StageData GetStage2()
        {
            // 3웨이브, 수량 살짝 증가
            return Stg(2, 1, "크리스탈 숲", 13, 1,
                new[] { G(3), G(2), G(3) },
                maxActive: 1);
        }

        private static StageData GetStage3()
        {
            // 4웨이브 등장, 소규모 다수
            return Stg(3, 1, "크리스탈 숲", 14, 1,
                new[] { G(2), G(3), G(2), G(3) },
                maxActive: 1);
        }

        private static StageData GetStage4()
        {
            // 3웨이브, 후반 증가형
            return Stg(4, 1, "크리스탈 숲", 14, 1,
                new[] { G(2), G(3), G(4) },
                maxActive: 1);
        }

        private static StageData GetStage5()
        {
            // 변칙 — 2웨이브, 한 번에 많이
            return Stg(5, 1, "크리스탈 숲", 15, 1,
                new[] { G(5), G(6) },
                maxActive: 1);
        }

        private static StageData GetStage6()
        {
            // 4웨이브, 균등 배분
            return Stg(6, 1, "크리스탈 숲", 15, 1,
                new[] { G(3), G(3), G(3), G(3) },
                maxActive: 1);
        }

        private static StageData GetStage7()
        {
            // 5웨이브, 점점 강해짐
            return Stg(7, 1, "크리스탈 숲", 16, 1,
                new[] { G(2), G(2), G(3), G(3), G(4) },
                maxActive: 1);
        }

        private static StageData GetStage8()
        {
            // 변칙 — 3웨이브, 대규모
            return Stg(8, 1, "크리스탈 숲", 16, 1,
                new[] { G(4), G(4), G(5) },
                maxActive: 1);
        }

        private static StageData GetStage9()
        {
            // 5웨이브, 파도형 (적-많-적-많-많)
            return Stg(9, 1, "크리스탈 숲", 17, 1,
                new[] { G(2), G(4), G(2), G(4), G(3) },
                maxActive: 1);
        }

        // 챕터 보스 — 변칙 (3웨이브, 고수량)
        private static StageData GetStage10()
        {
            return Stg(10, 1, "크리스탈 숲", 18, 2,
                new[] { G(4), G(5), G(6) },
                maxActive: 1);
        }

        // ============================================================
        // Chapter 2: 안개의 골짜기 (11-20) — 고블린 + 갑옷
        // 갑옷 첫 등장, 2종 조합으로 다양성 확보
        // maxActive=2 (2종 동시 표시), 후반부터 3~4 혼합
        // ============================================================

        private static StageData GetStage11()
        {
            // 갑옷 첫 등장: 몽둥이 → 갑옷 순차
            return Stg(11, 2, "안개의 골짜기", 14, 1,
                new[] { G(5), A(2) });
        }

        private static StageData GetStage12()
        {
            // 3웨이브: 갑옷-몽둥이-갑옷 교차
            return Stg(12, 2, "안개의 골짜기", 15, 1,
                new[] { A(2), G(4), A(3) },
                maxActive: 1);
        }

        private static StageData GetStage13()
        {
            // 동시 2종
            return Stg(13, 2, "안개의 골짜기", 15, 1,
                new[] { G(6), A(4) });
        }

        private static StageData GetStage14()
        {
            // 4웨이브 교차: 갑옷-몽둥이 번갈아
            return Stg(14, 2, "안개의 골짜기", 16, 1,
                new[] { A(2), G(3), A(3), G(4) },
                maxActive: 1);
        }

        // 변칙 — 갑옷 집중
        private static StageData GetStage15()
        {
            return Stg(15, 2, "안개의 골짜기", 15, 1,
                new[] { A(5), G(3) });
        }

        private static StageData GetStage16()
        {
            // 5웨이브 순차 소환
            return Stg(16, 2, "안개의 골짜기", 17, 1,
                new[] { G(3), A(2), G(3), A(2), G(4) },
                maxActive: 1);
        }

        private static StageData GetStage17()
        {
            // 동시 2종, 대규모
            return Stg(17, 2, "안개의 골짜기", 17, 2,
                new[] { G(8), A(5) });
        }

        private static StageData GetStage18()
        {
            // 4웨이브 강도 상승형
            return Stg(18, 2, "안개의 골짜기", 18, 2,
                new[] { G(2), A(3), G(4), A(5) },
                maxActive: 1);
        }

        private static StageData GetStage19()
        {
            // 변칙 — 갑옷 대량
            return Stg(19, 2, "안개의 골짜기", 18, 2,
                new[] { A(6), G(5) });
        }

        // 챕터 보스 — 동시 2종 대규모
        private static StageData GetStage20()
        {
            return Stg(20, 2, "안개의 골짜기", 19, 2,
                new[] { G(7), A(8) });
        }

        // ============================================================
        // Chapter 3: 화산 심장 (21-30) — + 궁수, 방패
        // 기본 4~7, 궁수/방패 2~3, 리스트 3~5
        // ============================================================

        private static StageData GetStage21()
        {
            return Stg(21, 3, "화산 심장", 16, 1,
                new[] { G(5), R(2), A(3) });
        }

        private static StageData GetStage22()
        {
            return Stg(22, 3, "화산 심장", 17, 1,
                new[] { R(3), G(6), A(2) });  // R2+R1 합산
        }

        private static StageData GetStage23()
        {
            return Stg(23, 3, "화산 심장", 18, 1,
                new[] { A(4), R(2), G(5), S(2) });
        }

        // 변칙 — 방패 등장, 짧은 리스트
        private static StageData GetStage24()
        {
            return Stg(24, 3, "화산 심장", 15, 1,
                new[] { G(6), S(3), R(2) });
        }

        // 스토리 스테이지
        private static StageData GetStage25()
        {
            return Stg(25, 3, "화산 심장", 20, 2,
                new[] { R(3), S(2), A(4), G(5) },
                new StoryData
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
                });
        }

        private static StageData GetStage26()
        {
            return Stg(26, 3, "화산 심장", 18, 2,
                new[] { S(2), G(6), R(2), A(3) });
        }

        private static StageData GetStage27()
        {
            return Stg(27, 3, "화산 심장", 20, 2,
                new[] { A(5), S(2), R(2), G(4) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage28()
        {
            return Stg(28, 3, "화산 심장", 16, 2,
                new[] { G(8), S(3), R(3) });
        }

        private static StageData GetStage29()
        {
            return Stg(29, 3, "화산 심장", 22, 2,
                new[] { R(3), S(2), A(4), G(5), R(2) });
        }

        // 챕터 보스 — 스토리
        private static StageData GetStage30()
        {
            return Stg(30, 3, "화산 심장", 22, 2,
                new[] { S(3), R(3), A(5), G(6) },
                new StoryData
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
                });
        }

        // ============================================================
        // Chapter 4: 폭염의 화약고 (31-40) — + 폭탄
        // 기본 4~8, 폭탄 2~3, 리스트 3~6
        // ============================================================

        private static StageData GetStage31()
        {
            return Stg(31, 4, "폭염의 화약고", 18, 1,
                new[] { G(5), B(2), A(3) });
        }

        private static StageData GetStage32()
        {
            return Stg(32, 4, "폭염의 화약고", 18, 1,
                new[] { B(4), G(6), A(2) });  // B2+B2 합산
        }

        private static StageData GetStage33()
        {
            return Stg(33, 4, "폭염의 화약고", 20, 1,
                new[] { A(4), B(2), R(2), G(5) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage34()
        {
            return Stg(34, 4, "폭염의 화약고", 16, 1,
                new[] { G(8), B(3), A(4) });
        }

        private static StageData GetStage35()
        {
            return Stg(35, 4, "폭염의 화약고", 22, 2,
                new[] { B(2), S(2), G(6), R(2), A(3) });
        }

        private static StageData GetStage36()
        {
            return Stg(36, 4, "폭염의 화약고", 22, 2,
                new[] { R(3), B(2), G(5), A(4), S(2) });
        }

        private static StageData GetStage37()
        {
            return Stg(37, 4, "폭염의 화약고", 23, 2,
                new[] { B(3), G(6), A(3), R(2), S(2) });
        }

        // 변칙 — 짧고 무거운
        private static StageData GetStage38()
        {
            return Stg(38, 4, "폭염의 화약고", 17, 2,
                new[] { A(5), B(3), S(3) });
        }

        private static StageData GetStage39()
        {
            return Stg(39, 4, "폭염의 화약고", 24, 2,
                new[] { G(6), B(3), R(2), S(2), A(4) });
        }

        // 챕터 보스
        private static StageData GetStage40()
        {
            return Stg(40, 4, "폭염의 화약고", 22, 2,
                new[] { B(3), A(5), R(3), G(8) });
        }

        // ============================================================
        // Chapter 5: 그림자 협곡 (41-50) — + 힐러
        // 기본 4~8, 힐러 1~2, 폭탄 2~3, 리스트 3~6
        // ============================================================

        private static StageData GetStage41()
        {
            return Stg(41, 5, "그림자 협곡", 20, 2,
                new[] { G(5), H(2), A(4), B(2) });
        }

        private static StageData GetStage42()
        {
            return Stg(42, 5, "그림자 협곡", 22, 2,
                new[] { H(2), G(6), A(3), B(2), R(2) });
        }

        private static StageData GetStage43()
        {
            return Stg(43, 5, "그림자 협곡", 22, 2,
                new[] { B(2), H(2), G(5), S(2), A(3) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage44()
        {
            return Stg(44, 5, "그림자 협곡", 18, 2,
                new[] { G(8), H(2), B(3) });
        }

        private static StageData GetStage45()
        {
            return Stg(45, 5, "그림자 협곡", 24, 2,
                new[] { A(4), H(2), B(2), G(6), S(2), R(2) });
        }

        private static StageData GetStage46()
        {
            return Stg(46, 5, "그림자 협곡", 24, 2,
                new[] { R(3), H(2), G(5), A(4), B(2) });
        }

        private static StageData GetStage47()
        {
            return Stg(47, 5, "그림자 협곡", 25, 2,
                new[] { H(2), B(3), A(4), G(6), R(2) });
        }

        // 변칙 — 짧고 무거운
        private static StageData GetStage48()
        {
            return Stg(48, 5, "그림자 협곡", 18, 2,
                new[] { B(3), H(2), G(6), S(3) });
        }

        private static StageData GetStage49()
        {
            return Stg(49, 5, "그림자 협곡", 26, 2,
                new[] { G(7), A(4), H(2), B(2), R(2) });
        }

        // 챕터 보스
        private static StageData GetStage50()
        {
            return Stg(50, 5, "그림자 협곡", 24, 3,
                new[] { H(2), B(3), R(3), A(5), G(8) });
        }

        // ============================================================
        // Chapter 6: 폭풍의 탑 (51-60) — + 헤비
        // 기본 4~8, 헤비 1~2, 리스트 3~6
        // ============================================================

        private static StageData GetStage51()
        {
            return Stg(51, 6, "폭풍의 탑", 22, 2,
                new[] { G(5), V(2), A(4), B(2) });
        }

        private static StageData GetStage52()
        {
            return Stg(52, 6, "폭풍의 탑", 23, 2,
                new[] { V(2), G(6), A(3), H(2), R(2) });
        }

        private static StageData GetStage53()
        {
            return Stg(53, 6, "폭풍의 탑", 24, 2,
                new[] { B(2), V(2), G(5), A(4), S(2) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage54()
        {
            return Stg(54, 6, "폭풍의 탑", 18, 2,
                new[] { V(2), G(8), B(3) });
        }

        private static StageData GetStage55()
        {
            return Stg(55, 6, "폭풍의 탑", 26, 2,
                new[] { A(5), V(2), H(2), G(6), B(2) });
        }

        private static StageData GetStage56()
        {
            return Stg(56, 6, "폭풍의 탑", 26, 2,
                new[] { V(2), B(3), G(6), A(4), R(2) });
        }

        private static StageData GetStage57()
        {
            return Stg(57, 6, "폭풍의 탑", 28, 2,
                new[] { R(3), V(2), B(2), G(7), A(3), H(2) });
        }

        // 변칙 — 짧고 무거운
        private static StageData GetStage58()
        {
            return Stg(58, 6, "폭풍의 탑", 19, 3,
                new[] { V(3), B(3), G(6) });
        }

        private static StageData GetStage59()
        {
            return Stg(59, 6, "폭풍의 탑", 28, 2,
                new[] { G(7), V(2), A(4), B(3), S(2) });
        }

        // 챕터 보스
        private static StageData GetStage60()
        {
            return Stg(60, 6, "폭풍의 탑", 26, 3,
                new[] { V(3), B(3), A(5), G(8), R(3) });
        }

        // ============================================================
        // Chapter 7: 거인의 둥지 (61-70) — + 마법사
        // 기본 4~8, 마법사 1~2, 리스트 3~7
        // ============================================================

        private static StageData GetStage61()
        {
            return Stg(61, 7, "거인의 둥지", 24, 2,
                new[] { G(6), W(2), V(2), A(4) });
        }

        private static StageData GetStage62()
        {
            return Stg(62, 7, "거인의 둥지", 25, 2,
                new[] { W(2), G(5), V(2), B(2), A(3) });
        }

        private static StageData GetStage63()
        {
            return Stg(63, 7, "거인의 둥지", 26, 2,
                new[] { A(5), W(2), V(2), G(6), B(2) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage64()
        {
            return Stg(64, 7, "거인의 둥지", 19, 2,
                new[] { W(2), V(2), G(8) });
        }

        private static StageData GetStage65()
        {
            return Stg(65, 7, "거인의 둥지", 28, 2,
                new[] { B(3), W(2), V(2), G(5), A(4), H(2) });
        }

        private static StageData GetStage66()
        {
            return Stg(66, 7, "거인의 둥지", 28, 2,
                new[] { V(2), W(2), G(6), B(2), A(4), R(2) });
        }

        private static StageData GetStage67()
        {
            return Stg(67, 7, "거인의 둥지", 30, 2,
                new[] { W(2), B(3), V(2), G(7), A(3), S(2) });
        }

        // 변칙 — 짧고 무거운
        private static StageData GetStage68()
        {
            return Stg(68, 7, "거인의 둥지", 20, 3,
                new[] { W(2), V(3), B(3), G(6) });
        }

        private static StageData GetStage69()
        {
            return Stg(69, 7, "거인의 둥지", 30, 2,
                new[] { G(8), W(2), V(2), A(4), B(3), R(2) });
        }

        // 챕터 보스
        private static StageData GetStage70()
        {
            return Stg(70, 7, "거인의 둥지", 28, 3,
                new[] { W(2), V(3), B(3), A(6), G(10) });
        }

        // ============================================================
        // Chapter 8: 마법사의 서재 (71-80) — 모든 기본 타입
        // 기본 5~10, 특수 2~3, 리스트 4~8
        // ============================================================

        private static StageData GetStage71()
        {
            return Stg(71, 8, "마법사의 서재", 28, 2,
                new[] { W(2), G(6), V(2), B(2), A(4) });
        }

        private static StageData GetStage72()
        {
            return Stg(72, 8, "마법사의 서재", 28, 2,
                new[] { V(2), W(2), B(2), G(7), A(4) });
        }

        private static StageData GetStage73()
        {
            return Stg(73, 8, "마법사의 서재", 30, 2,
                new[] { B(3), H(2), W(2), G(6), V(2), A(5) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage74()
        {
            return Stg(74, 8, "마법사의 서재", 20, 3,
                new[] { W(2), V(3), G(10) });
        }

        private static StageData GetStage75()
        {
            return Stg(75, 8, "마법사의 서재", 32, 2,
                new[] { G(8), A(5), W(2), V(2), B(3), R(3) });
        }

        private static StageData GetStage76()
        {
            return Stg(76, 8, "마법사의 서재", 32, 2,
                new[] { W(2), V(2), B(3), G(7), A(5), H(2) });
        }

        private static StageData GetStage77()
        {
            return Stg(77, 8, "마법사의 서재", 32, 3,
                new[] { H(2), W(2), V(2), G(8), B(3), A(4) });
        }

        // 변칙 — 짧고 무거운
        private static StageData GetStage78()
        {
            return Stg(78, 8, "마법사의 서재", 22, 3,
                new[] { W(2), V(3), B(3), G(8) });
        }

        private static StageData GetStage79()
        {
            return Stg(79, 8, "마법사의 서재", 34, 3,
                new[] { B(3), W(2), V(2), G(8), A(5), H(2), R(3) });
        }

        // 챕터 보스
        private static StageData GetStage80()
        {
            return Stg(80, 8, "마법사의 서재", 30, 3,
                new[] { W(2), V(3), B(3), A(6), G(10), R(3) });
        }

        // ============================================================
        // Chapter 9: 도둑의 은신처 (81-90) — + 도둑
        // 기본 5~10, 도둑 2~3, 리스트 4~8
        // ============================================================

        private static StageData GetStage81()
        {
            return Stg(81, 9, "도둑의 은신처", 28, 2,
                new[] { G(6), T(2), V(2), A(4), B(2) });
        }

        private static StageData GetStage82()
        {
            return Stg(82, 9, "도둑의 은신처", 30, 2,
                new[] { T(2), G(7), V(2), B(2), A(4) });
        }

        private static StageData GetStage83()
        {
            return Stg(83, 9, "도둑의 은신처", 30, 2,
                new[] { A(5), T(2), V(2), G(6), B(3), W(2) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage84()
        {
            return Stg(84, 9, "도둑의 은신처", 22, 3,
                new[] { T(3), V(2), G(10) });
        }

        private static StageData GetStage85()
        {
            return Stg(85, 9, "도둑의 은신처", 32, 2,
                new[] { B(3), T(2), W(2), V(2), G(7), A(5) });
        }

        private static StageData GetStage86()
        {
            return Stg(86, 9, "도둑의 은신처", 32, 2,
                new[] { T(3), V(2), W(2), G(6), B(3), A(4) });
        }

        private static StageData GetStage87()
        {
            return Stg(87, 9, "도둑의 은신처", 34, 3,
                new[] { W(2), T(2), V(2), B(3), G(8), A(5) });
        }

        // 변칙 — 짧고 무거운
        private static StageData GetStage88()
        {
            return Stg(88, 9, "도둑의 은신처", 22, 3,
                new[] { T(3), V(3), G(8), B(3) });
        }

        private static StageData GetStage89()
        {
            return Stg(89, 9, "도둑의 은신처", 34, 3,
                new[] { G(8), T(3), W(2), V(2), A(5), B(3) });
        }

        // 챕터 보스
        private static StageData GetStage90()
        {
            return Stg(90, 9, "도둑의 은신처", 32, 3,
                new[] { T(3), W(2), V(3), B(3), A(6), G(10) });
        }

        // ============================================================
        // Chapter 10: 마녀의 영역 (91-100) — + 마녀
        // 기본 5~12, 마녀 1~3, 리스트 4~8
        // ============================================================

        private static StageData GetStage91()
        {
            return Stg(91, 10, "마녀의 영역", 30, 3,
                new[] { Wi(2), G(6), T(2), A(4) });
        }

        private static StageData GetStage92()
        {
            return Stg(92, 10, "마녀의 영역", 32, 3,
                new[] { Wi(2), V(2), T(2), G(7), A(4) });
        }

        private static StageData GetStage93()
        {
            return Stg(93, 10, "마녀의 영역", 32, 3,
                new[] { Wi(2), T(2), V(2), G(8), B(3), A(4) });
        }

        // 변칙 — 짧은 리스트, 높은 수량
        private static StageData GetStage94()
        {
            return Stg(94, 10, "마녀의 영역", 24, 3,
                new[] { Wi(3), T(3), G(10) });
        }

        private static StageData GetStage95()
        {
            return Stg(95, 10, "마녀의 영역", 34, 3,
                new[] { Wi(2), G(8), T(2), V(2), A(5), B(3) });
        }

        private static StageData GetStage96()
        {
            return Stg(96, 10, "마녀의 영역", 34, 3,
                new[] { Wi(2), V(2), T(2), B(3), G(7), A(5), R(3) });
        }

        private static StageData GetStage97()
        {
            return Stg(97, 10, "마녀의 영역", 35, 3,
                new[] { Wi(2), T(3), V(2), G(8), B(3), A(5) });
        }

        // 변칙 — 짧고 무거운 (최종 난이도)
        private static StageData GetStage98()
        {
            return Stg(98, 10, "마녀의 영역", 25, 3,
                new[] { Wi(3), T(3), V(3), G(12) });
        }

        private static StageData GetStage99()
        {
            return Stg(99, 10, "마녀의 영역", 35, 3,
                new[] { Wi(2), G(10), T(3), V(2), A(5), B(3) });
        }

        // 최종 보스 + 엔딩 스토리
        private static StageData GetStage100()
        {
            return Stg(100, 10, "마녀의 영역", 35, 4,
                new[] { Wi(3), T(3), V(3), B(3), A(6), G(12) },
                new StoryData
                {
                    beforeStageCutscene = "루나가 최종 전장의 중심에 도달한다.\n모든 종류의 고블린들이 한꺼번에 몰려든다!\n프리즘: \"이건... 역대 최대 규모야!\"\n오라클리온: \"루나, 이것이 진정한 최후의 결전이다!\"",
                    stageIntroDialogues = new[]
                    {
                        "루나: \"지금까지 배운 모든 것을 쏟아부을게요!\"\n프리즘: \"우리 셋이 함께라면 반드시 이길 수 있어!\""
                    },
                    stageClearCutscene = "모든 고블린이 물러가고 평화가 찾아온다.\n크리스탈이 찬란하게 빛나며 숲이 되살아난다.\n루나: \"드디어... 모든 고블린을 물리쳤어!\"\n프리즘: \"루나, 넌 진짜 대단해!\"\n오라클리온: \"네 여정은 여기서 끝나지 않는다. 새로운 세계가 너를 기다리고 있을 것이다...\"",
                    stageClearDialogues = new[]
                    {
                        "루나: \"고마워요, 오라클리온. 그리고 프리즘도.\"\n오라클리온: \"진정한 수호자여, 언제든 다시 만나게 될 것이다.\""
                    }
                });
        }

        // ============================================================
        // 엘리트 전장 (101~150) — Lv2 몬스터 + 기존 몬스터 다채롭게 혼합
        // 설계 원칙:
        //   - 첫 레벨(101)부터 다양한 몬스터 조합 (단조로운 단일 타입 지양)
        //   - 기존 특수 몬스터(폭탄/마법사/도둑/마녀/헤비/힐러)와 Lv2를 섞어 풍성한 전투
        //   - 변칙 레벨(짧지만 빡빡 / 길지만 물량전) 포함
        //   - 챕터별 테마: 서서히 Lv2 비율 증가, 특수 몬스터 복합도 상승
        // ============================================================

        // === 챕터 11: 엘리트 초원 (101-110) — 다양한 기존 몬스터 + Lv2 첫 등장 ===
        private static StageData GetStage101()
        {
            // 기존 4종 + 엘리트 몽둥이 첫 등장
            return Stg(101, 11, "엘리트 초원", 22, 2,
                new[] { G(4), A(3), R(2), G2(2) });
        }
        private static StageData GetStage102()
        {
            // 폭탄 고블린 + 엘리트 갑옷 첫 등장
            return Stg(102, 11, "엘리트 초원", 24, 2,
                new[] { B(2), G(4), A(3), A2(2) });
        }
        private static StageData GetStage103()
        {
            // 방패 + 궁수 + 엘리트 몽둥이
            return Stg(103, 11, "엘리트 초원", 24, 2,
                new[] { S(2), R(3), G(3), G2(3) });
        }
        private static StageData GetStage104()
        {
            // 도둑 첫 등장 + 다종 혼합 — 변칙(짧고 빡빡)
            return Stg(104, 11, "엘리트 초원", 20, 2,
                new[] { T(2), G2(3), A2(2) });
        }
        private static StageData GetStage105()
        {
            // 힐러 + 폭탄 + 엘리트
            return Stg(105, 11, "엘리트 초원", 25, 2,
                new[] { H(2), B(2), G(3), G2(3), A(3) });
        }
        private static StageData GetStage106()
        {
            // 마법사 + 갑옷 + 엘리트 궁수 첫 등장
            return Stg(106, 11, "엘리트 초원", 26, 2,
                new[] { W(1), A(4), R(2), R2(2), G2(3) });
        }
        private static StageData GetStage107()
        {
            // 엘리트 방패 첫 등장 + 기존 혼합
            return Stg(107, 11, "엘리트 초원", 26, 2,
                new[] { S(3), G(4), S2(2), G2(3) });
        }
        private static StageData GetStage108()
        {
            // 헤비 + 엘리트 4종 혼합
            return Stg(108, 11, "엘리트 초원", 28, 3,
                new[] { V(1), G2(4), A2(3), R(3), S(2) });
        }
        private static StageData GetStage109()
        {
            // 마녀 + 기존 기본 + 엘리트 — 변칙(물량)
            return Stg(109, 11, "엘리트 초원", 30, 3,
                new[] { Wi(1), G(5), A(4), G2(4), A2(2) });
        }
        private static StageData GetStage110()
        {
            // 챕터 보스: 특수 3종 + 엘리트 전종
            return Stg(110, 11, "엘리트 초원", 32, 3,
                new[] { B(3), T(2), G2(5), A2(3), R2(2), S2(2), G(4) });
        }

        // === 챕터 12: 엘리트 협곡 (111-120) — Lv2 비율 증가, 특수 몬스터 복합 ===
        private static StageData GetStage111()
        {
            // 마법사 + 엘리트 궁수/갑옷
            return Stg(111, 12, "엘리트 협곡", 26, 2,
                new[] { W(2), R2(3), A2(3), G(4) });
        }
        private static StageData GetStage112()
        {
            // 도둑 + 폭탄 + 엘리트 방패
            return Stg(112, 12, "엘리트 협곡", 27, 3,
                new[] { T(2), B(3), S2(2), G2(4), A(3) });
        }
        private static StageData GetStage113()
        {
            // 마녀 + 힐러 + 엘리트 혼합
            return Stg(113, 12, "엘리트 협곡", 28, 3,
                new[] { Wi(1), H(2), G2(4), A2(3), R(3) });
        }
        private static StageData GetStage114()
        {
            // 헤비 + 엘리트 4종 — 변칙(적은 수 고체력)
            return Stg(114, 12, "엘리트 협곡", 25, 3,
                new[] { V(2), G2(3), A2(2), S2(2) });
        }
        private static StageData GetStage115()
        {
            // 전 기본 타입 + Lv2 기본
            return Stg(115, 12, "엘리트 협곡", 30, 3,
                new[] { G(4), A(3), R(3), S(2), G2(4), A2(2) });
        }
        private static StageData GetStage116()
        {
            // 마법사 + 도둑 콤보 + 엘리트
            return Stg(116, 12, "엘리트 협곡", 30, 3,
                new[] { W(2), T(2), G2(5), R2(3), A(4) });
        }
        private static StageData GetStage117()
        {
            // 마녀 + 폭탄 + 엘리트 방패/갑옷
            return Stg(117, 12, "엘리트 협곡", 32, 3,
                new[] { Wi(1), B(3), A2(4), S2(3), G(4) });
        }
        private static StageData GetStage118()
        {
            // 힐러 + 헤비 + 엘리트 궁수 — 변칙(힐러가 계속 회복)
            return Stg(118, 12, "엘리트 협곡", 28, 3,
                new[] { H(2), V(2), R2(3), G2(4), A(3) });
        }
        private static StageData GetStage119()
        {
            // 도둑 + 마녀 + 엘리트 전종
            return Stg(119, 12, "엘리트 협곡", 33, 3,
                new[] { T(2), Wi(1), G2(5), A2(3), R2(2), S2(2) });
        }
        private static StageData GetStage120()
        {
            // 챕터 보스: 특수 전종 + 엘리트 물량
            return Stg(120, 12, "엘리트 협곡", 35, 3,
                new[] { Wi(1), B(3), W(1), G2(6), A2(4), R2(3), S2(2), G(4) });
        }

        // === 챕터 13: 엘리트 화산 (121-130) — 고난도 혼합, 변칙 패턴 ===
        private static StageData GetStage121()
        {
            // 마녀 + 도둑 + 엘리트 갑옷/몽둥이
            return Stg(121, 13, "엘리트 화산", 30, 3,
                new[] { Wi(1), T(2), G2(5), A2(3), G(4) });
        }
        private static StageData GetStage122()
        {
            // 헤비 + 폭탄 + 엘리트 궁수
            return Stg(122, 13, "엘리트 화산", 30, 3,
                new[] { V(2), B(3), R2(3), G2(4), A(3) });
        }
        private static StageData GetStage123()
        {
            // 마법사 + 힐러 + 엘리트 방패/갑옷
            return Stg(123, 13, "엘리트 화산", 30, 3,
                new[] { W(2), H(2), A2(4), S2(3), G2(3) });
        }
        private static StageData GetStage124()
        {
            // 마녀2 + 엘리트 — 변칙(마녀 집중)
            return Stg(124, 13, "엘리트 화산", 28, 3,
                new[] { Wi(2), G2(5), A2(3), R(3) });
        }
        private static StageData GetStage125()
        {
            // 도둑 + 폭탄 + 엘리트 전종
            return Stg(125, 13, "엘리트 화산", 32, 3,
                new[] { T(2), B(3), G2(5), A2(3), R2(2), S2(2) });
        }
        private static StageData GetStage126()
        {
            // 헤비2 + 마법사 + 엘리트 물량
            return Stg(126, 13, "엘리트 화산", 33, 3,
                new[] { V(2), W(2), G2(5), A2(4), R2(3) });
        }
        private static StageData GetStage127()
        {
            // 마녀 + 힐러 + 폭탄 + 엘리트 방패
            return Stg(127, 13, "엘리트 화산", 33, 3,
                new[] { Wi(1), H(2), B(3), S2(3), G2(4), A(4) });
        }
        private static StageData GetStage128()
        {
            // 도둑 + 헤비 + 엘리트 궁수/갑옷 — 변칙(은신+탱크)
            return Stg(128, 13, "엘리트 화산", 30, 3,
                new[] { T(3), V(2), R2(3), A2(4), G(4) });
        }
        private static StageData GetStage129()
        {
            // 마녀 + 마법사 + 엘리트 전종
            return Stg(129, 13, "엘리트 화산", 35, 4,
                new[] { Wi(1), W(2), G2(6), A2(4), R2(3), S2(2) });
        }
        private static StageData GetStage130()
        {
            // 챕터 보스: 전 특수 + 엘리트 대규모
            return Stg(130, 13, "엘리트 화산", 38, 4,
                new[] { Wi(2), B(3), T(2), V(2), G2(6), A2(4), R2(3), S2(3) });
        }

        // === 챕터 14: 엘리트 심연 (131-140) — 극한 난이도, 복합 전략 ===
        private static StageData GetStage131()
        {
            // 마녀 + 도둑 + 엘리트 대규모 혼합
            return Stg(131, 14, "엘리트 심연", 33, 3,
                new[] { Wi(2), T(2), G2(5), A2(4), R(3), B(3) });
        }
        private static StageData GetStage132()
        {
            // 헤비 + 마법사 + 엘리트 방패/궁수
            return Stg(132, 14, "엘리트 심연", 33, 3,
                new[] { V(2), W(2), S2(3), R2(3), G2(4), A(4) });
        }
        private static StageData GetStage133()
        {
            // 마녀 + 힐러 + 엘리트 전종
            return Stg(133, 14, "엘리트 심연", 35, 3,
                new[] { Wi(1), H(2), G2(6), A2(4), R2(3), S(3) });
        }
        private static StageData GetStage134()
        {
            // 헤비2 + 폭탄 + 엘리트 갑옷/방패 — 변칙(탱크 집중)
            return Stg(134, 14, "엘리트 심연", 30, 4,
                new[] { V(2), B(4), A2(5), S2(3), G(4) });
        }
        private static StageData GetStage135()
        {
            // 마녀 + 도둑 + 마법사 + 엘리트
            return Stg(135, 14, "엘리트 심연", 36, 4,
                new[] { Wi(1), T(2), W(2), G2(5), A2(4), R2(3) });
        }
        private static StageData GetStage136()
        {
            // 힐러 + 폭탄 대규모 + 엘리트 전종
            return Stg(136, 14, "엘리트 심연", 36, 4,
                new[] { H(2), B(4), G2(6), A2(4), R2(3), S2(2) });
        }
        private static StageData GetStage137()
        {
            // 마녀2 + 도둑 + 헤비 + 엘리트 — 고난도
            return Stg(137, 14, "엘리트 심연", 38, 4,
                new[] { Wi(2), T(2), V(2), G2(5), A2(4), R2(3), S(3) });
        }
        private static StageData GetStage138()
        {
            // 마법사 + 힐러 + 엘리트 물량전
            return Stg(138, 14, "엘리트 심연", 36, 4,
                new[] { W(2), H(2), G2(6), A2(5), R2(4), S2(3) });
        }
        private static StageData GetStage139()
        {
            // 마녀2 + 도둑3 — 변칙(은신 지옥)
            return Stg(139, 14, "엘리트 심연", 35, 4,
                new[] { Wi(2), T(3), G2(6), A2(4), B(3) });
        }
        private static StageData GetStage140()
        {
            // 챕터 보스: 전 특수 + 엘리트 대규모
            return Stg(140, 14, "엘리트 심연", 40, 4,
                new[] { Wi(2), V(2), B(4), T(2), W(2), G2(7), A2(5), R2(3), S2(3) });
        }

        // === 챕터 15: 최종 엘리트 (141-150) — 전면전, 최고 난이도 ===
        private static StageData GetStage141()
        {
            // 마녀 + 헤비 + 폭탄 + 엘리트 4종
            return Stg(141, 15, "최종 엘리트", 36, 4,
                new[] { Wi(1), V(2), B(3), G2(6), A2(4), R2(3), S2(3) });
        }
        private static StageData GetStage142()
        {
            // 마녀2 + 도둑 + 마법사 + 엘리트
            return Stg(142, 15, "최종 엘리트", 37, 4,
                new[] { Wi(2), T(3), W(2), G2(6), A2(4), R2(3) });
        }
        private static StageData GetStage143()
        {
            // 헤비2 + 힐러 + 엘리트 물량
            return Stg(143, 15, "최종 엘리트", 37, 4,
                new[] { V(2), H(2), G2(7), A2(5), R2(3), S2(3) });
        }
        private static StageData GetStage144()
        {
            // 마녀2 + 폭탄 + 엘리트 전종 — 변칙(마녀+폭탄 시너지)
            return Stg(144, 15, "최종 엘리트", 35, 4,
                new[] { Wi(2), B(4), G2(5), A2(4), R2(3), S2(3) });
        }
        private static StageData GetStage145()
        {
            // 도둑 + 헤비 + 마법사 + 엘리트 대규모
            return Stg(145, 15, "최종 엘리트", 38, 4,
                new[] { T(3), V(2), W(2), G2(7), A2(5), R2(4), S2(3) });
        }
        private static StageData GetStage146()
        {
            // 마녀2 + 힐러 + 도둑 + 엘리트 전종
            return Stg(146, 15, "최종 엘리트", 38, 4,
                new[] { Wi(2), H(2), T(2), G2(7), A2(5), R2(4), S2(3) });
        }
        private static StageData GetStage147()
        {
            // 헤비3 + 마법사 + 엘리트 물량 — 변칙(탱커 지옥)
            return Stg(147, 15, "최종 엘리트", 40, 4,
                new[] { V(3), W(2), G2(8), A2(5), R2(4), S2(3) });
        }
        private static StageData GetStage148()
        {
            // 마녀2 + 전 특수 + 엘리트 전종
            return Stg(148, 15, "최종 엘리트", 42, 4,
                new[] { Wi(2), B(4), T(3), V(2), G2(8), A2(5), R2(4), S2(3) });
        }
        private static StageData GetStage149()
        {
            // 마녀3 + 도둑3 + 엘리트 대규모 — 변칙(극한)
            return Stg(149, 15, "최종 엘리트", 42, 4,
                new[] { Wi(3), T(3), V(2), G2(9), A2(6), R2(4), S2(4) });
        }
        private static StageData GetStage150()
        {
            // ★ 최종 보스: 전 특수 + 엘리트 전종 총출동
            return Stg(150, 15, "최종 엘리트", 45, 4,
                new[] { Wi(3), V(3), B(4), T(3), W(2), H(2), G2(10), A2(6), R2(5), S2(4) },
                new StoryData
                {
                    beforeStageCutscene = "루나가 엘리트 전장의 최심부에 도착한다.\n강화된 고블린 엘리트 군단이 총출동한다!\n프리즘: \"이건... 평범한 고블린이 아니야! 엘리트급이라고!\"\n오라클리온: \"루나, 진정한 힘을 보여줄 때다.\"",
                    stageIntroDialogues = new[]
                    {
                        "루나: \"더 강해진 적들... 하지만 나도 성장했어!\"\n프리즘: \"우리의 실력을 증명할 때야!\""
                    },
                    stageClearCutscene = "엘리트 고블린 군단이 완전히 무너진다.\n강화된 크리스탈이 더욱 찬란하게 빛난다.\n루나: \"해냈어... 엘리트 군단까지 물리쳤어!\"\n프리즘: \"루나, 넌 이제 진정한 전사야!\"\n오라클리온: \"대단하다, 수호자여. 하지만 더 큰 도전이 기다리고 있을지 모른다...\"",
                    stageClearDialogues = new[]
                    {
                        "루나: \"어떤 적이 와도 준비됐어요!\"\n오라클리온: \"그 자신감, 바로 수호자의 자격이다.\""
                    }
                });
        }
    }
}

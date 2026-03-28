using UnityEngine;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Data
{
    /// <summary>
    /// 레벨 레지스트리 - 모든 레벨 데이터를 중앙 관리.
    /// 새 레벨 추가 시 RegisterLevels()에 항목만 추가하면 됨.
    /// </summary>
    public static class LevelRegistry
    {
        private static Dictionary<int, LevelData> levels;
        private static bool isInitialized = false;

        /// <summary>
        /// 레지스트리 초기화 (최초 1회)
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;
            levels = new Dictionary<int, LevelData>();
            RegisterLevels();
            LoadUnlockState();
            isInitialized = true;
        }

        /// <summary>
        /// 레지스트리 강제 재초기화 (에디터 도메인 리로드 대응)
        /// </summary>
        public static void ForceReinitialize()
        {
            isInitialized = false;
            Initialize();
        }

        /// <summary>
        /// 레벨 데이터 가져오기
        /// </summary>
        public static LevelData GetLevel(int levelId)
        {
            Initialize();
            return levels.TryGetValue(levelId, out var data) ? data : null;
        }

        /// <summary>
        /// 모든 레벨 데이터 목록 (정렬)
        /// </summary>
        public static List<LevelData> GetAllLevels()
        {
            Initialize();
            var list = new List<LevelData>(levels.Values);
            list.Sort((a, b) => a.levelId.CompareTo(b.levelId));
            return list;
        }

        /// <summary>
        /// 해금된 레벨만 반환
        /// </summary>
        public static List<LevelData> GetUnlockedLevels()
        {
            Initialize();
            var list = new List<LevelData>();
            foreach (var level in levels.Values)
            {
                if (!level.isLocked)
                    list.Add(level);
            }
            list.Sort((a, b) => a.levelId.CompareTo(b.levelId));
            return list;
        }

        /// <summary>
        /// 레벨 해금 처리 + 영속 저장
        /// </summary>
        public static void UnlockLevel(int levelId)
        {
            Initialize();
            if (levels.TryGetValue(levelId, out var data))
            {
                data.isLocked = false;
                PlayerPrefs.SetInt($"Level_{levelId}_Unlocked", 1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 모든 레벨을 해금 처리 + 영속 저장
        /// </summary>
        public static void UnlockAllLevels()
        {
            Initialize();
            foreach (var level in levels.Values)
            {
                level.isLocked = false;
                PlayerPrefs.SetInt($"Level_{level.levelId}_Unlocked", 1);
            }
            PlayerPrefs.Save();
            Debug.Log($"[LevelRegistry] 모든 레벨 해금 완료 ({levels.Count}개)");
        }

        /// <summary>
        /// 총 레벨 수
        /// </summary>
        public static int LevelCount
        {
            get { Initialize(); return levels.Count; }
        }

        // ============================================================
        // PlayerPrefs에서 해금 상태 로드
        // ============================================================
        private static void LoadUnlockState()
        {
            foreach (var level in levels.Values)
            {
                if (PlayerPrefs.GetInt($"Level_{level.levelId}_Unlocked", 0) == 1)
                    level.isLocked = false;
            }
        }

        // ============================================================
        // 레벨 등록 - 새 레벨 추가는 여기에만 항목 추가
        // ============================================================
        private static void RegisterLevels()
        {
            // --- 레벨 1~10: Stage 모드 (크리스탈 숲 - 고블린 소탕) ---
            RegisterStageLevels();

            // --- 레벨 11: Stage 모드 (드릴 튜토리얼) ---
            Register(new LevelData
            {
                levelId = 11,
                levelName = "STAGE 11",
                subtitle = "드릴 튜토리얼",
                gameMode = GameMode.Stage,
                difficultyType = DifficultyType.Easy,
                isLocked = true,
                unlockRequirement = 10,
                lobbyDisplay = new LobbyDisplayConfig
                {
                    backgroundColor = new Color(0.7f, 0.3f, 0.5f),
                    borderColor = new Color(1f, 0.5f, 0.7f),
                    buttonSize = 200f
                }
            });

            // --- 레벨 12~15: Stage 모드 (활+몽둥이+갑옷 혼합) ---
            RegisterStages12To15();

            // --- 레벨 16~20: Stage 모드 (방패 고블린 등장) ---
            RegisterStages16To20();

            // --- 레벨 21~30: Stage 모드 (화산 심장 — 4종 전체 혼합) ---
            RegisterStages21To30();

            // --- 레벨 31~50: Stage 모드 (폭염의 화약고 — 폭탄고블린 등장) ---
            RegisterStages31To50();

            // --- 레벨 51~60: Stage 모드 (치유의 늪 — 힐러고블린 등장) ---
            RegisterStages51To60();

            // --- 마지막 레벨: Infinite 모드 (무한 도전) — 항상 스테이지 레벨 뒤에 배치 ---
            RegisterInfiniteLevel();
        }

        /// <summary>
        /// 무한 도전 레벨 등록 — 항상 마지막 스테이지 +1 ID로 등록
        /// </summary>
        private static void RegisterInfiniteLevel()
        {
            // 현재 등록된 레벨 중 가장 큰 ID + 1
            int infiniteId = 0;
            foreach (var id in levels.Keys)
            {
                if (id > infiniteId) infiniteId = id;
            }
            infiniteId += 1; // 마지막 스테이지 다음

            int lastStageId = infiniteId - 1; // 해금 조건: 직전 스테이지 클리어

            Register(new LevelData
            {
                levelId = infiniteId,
                levelName = "무한 도전",
                subtitle = "생존 미션",
                gameMode = GameMode.Infinite,
                difficultyType = DifficultyType.Easy,
                isLocked = true,
                unlockRequirement = lastStageId,
                infiniteConfig = new InfiniteConfig
                {
                    initialMoves = 15,
                    activeGemTypeCount = 5
                },
                lobbyDisplay = new LobbyDisplayConfig
                {
                    backgroundColor = new Color(0.15f, 0.1f, 0.3f),
                    borderColor = new Color(0.4f, 0.2f, 0.8f),
                    buttonSize = 200f
                }
            });

            Debug.Log($"[LevelRegistry] 무한 도전 등록: levelId={infiniteId}, 해금조건=Stage {lastStageId} 클리어");
        }

        /// <summary>
        /// Stage 모드 레벨 1~10 등록
        /// Mission1StageData에서 subtitle 정보만 참조하여 등록.
        /// 실제 StageData 로드는 GameManager에서 수행.
        /// </summary>
        private static void RegisterStageLevels()
        {
            // 난이도별 색상 그라데이션
            Color[] bgColors = new Color[]
            {
                new Color(0.2f, 0.6f, 0.8f),   // 1: 밝은 파랑
                new Color(0.2f, 0.55f, 0.75f),  // 2
                new Color(0.25f, 0.5f, 0.7f),   // 3
                new Color(0.3f, 0.5f, 0.65f),   // 4
                new Color(0.35f, 0.45f, 0.6f),  // 5
                new Color(0.4f, 0.4f, 0.6f),    // 6
                new Color(0.45f, 0.35f, 0.6f),  // 7
                new Color(0.5f, 0.3f, 0.6f),    // 8
                new Color(0.55f, 0.25f, 0.6f),  // 9
                new Color(0.6f, 0.2f, 0.55f)    // 10: 보스 (보라)
            };

            // 스테이지별 부제목 (고블린 소탕 미션)
            string[] subtitles = new string[]
            {
                "고블린 2마리 제거",     // 1
                "고블린 3마리 제거",     // 2
                "고블린 4마리 제거",     // 3
                "고블린 5마리 제거",     // 4
                "고블린 6마리 제거",     // 5
                "고블린 8마리 제거",     // 6
                "고블린 10마리 제거",    // 7
                "고블린 12마리 제거",    // 8
                "고블린 14마리 제거",    // 9
                "고블린 16마리 제거"     // 10
            };

            for (int i = 1; i <= 10; i++)
            {
                int idx = i - 1;
                Color bgColor = bgColors[idx];
                Color borderColor = new Color(
                    Mathf.Min(bgColor.r + 0.2f, 1f),
                    Mathf.Min(bgColor.g + 0.2f, 1f),
                    Mathf.Min(bgColor.b + 0.2f, 1f)
                );

                Register(new LevelData
                {
                    levelId = i,
                    levelName = $"STAGE {i}",
                    subtitle = subtitles[idx],
                    gameMode = GameMode.Stage,
                    difficultyType = DifficultyType.Easy,
                    isLocked = i > 1,
                    unlockRequirement = i > 1 ? i - 1 : 0,
                    lobbyDisplay = new LobbyDisplayConfig
                    {
                        backgroundColor = bgColor,
                        borderColor = borderColor,
                        buttonSize = 200f
                    }
                });
            }
        }

        /// <summary>
        /// Stage 모드 레벨 12~15 등록 (활+몽둥이+갑옷 고블린 혼합)
        /// </summary>
        private static void RegisterStages12To15()
        {
            Color[] bgColors = new Color[]
            {
                new Color(0.65f, 0.25f, 0.5f),  // 12
                new Color(0.7f, 0.2f, 0.45f),   // 13
                new Color(0.75f, 0.15f, 0.4f),  // 14
                new Color(0.8f, 0.1f, 0.35f)    // 15: 챕터 보스
            };

            string[] subtitles = new string[]
            {
                "궁수3 + 고블린12 + 갑옷8",
                "궁수3 + 고블린14 + 갑옷11",
                "궁수4 + 고블린15 + 갑옷15",
                "궁수4 + 고블린17 + 갑옷18"
            };

            for (int i = 12; i <= 15; i++)
            {
                int idx = i - 12;
                Color bgColor = bgColors[idx];
                Color borderColor = new Color(
                    Mathf.Min(bgColor.r + 0.2f, 1f),
                    Mathf.Min(bgColor.g + 0.2f, 1f),
                    Mathf.Min(bgColor.b + 0.2f, 1f)
                );

                Register(new LevelData
                {
                    levelId = i,
                    levelName = $"STAGE {i}",
                    subtitle = subtitles[idx],
                    gameMode = GameMode.Stage,
                    difficultyType = DifficultyType.Normal,
                    isLocked = true,
                    unlockRequirement = i - 1,
                    lobbyDisplay = new LobbyDisplayConfig
                    {
                        backgroundColor = bgColor,
                        borderColor = borderColor,
                        buttonSize = 200f
                    }
                });
            }
        }

        /// <summary>
        /// Stage 모드 레벨 16~20 등록 (방패 고블린 등장 — 궁수+갑옷+방패 혼합)
        /// </summary>
        private static void RegisterStages16To20()
        {
            Color[] bgColors = new Color[]
            {
                new Color(0.20f, 0.35f, 0.60f),  // 16: 짙은 파랑
                new Color(0.18f, 0.30f, 0.65f),  // 17
                new Color(0.15f, 0.25f, 0.70f),  // 18
                new Color(0.12f, 0.20f, 0.75f),  // 19
                new Color(0.10f, 0.15f, 0.80f)   // 20: 챕터 보스
            };

            string[] subtitles = new string[]
            {
                "궁수2 + 갑옷8 + 방패3 + 기본12",   // 16
                "궁수3 + 갑옷10 + 방패4 + 기본13",  // 17
                "궁수4 + 갑옷12 + 방패6 + 기본14",  // 18
                "궁수4 + 갑옷14 + 방패8 + 기본16",  // 19
                "궁수5 + 갑옷15 + 방패10 + 기본18"  // 20
            };

            for (int i = 16; i <= 20; i++)
            {
                int idx = i - 16;
                Color bgColor = bgColors[idx];
                Color borderColor = new Color(
                    Mathf.Min(bgColor.r + 0.2f, 1f),
                    Mathf.Min(bgColor.g + 0.2f, 1f),
                    Mathf.Min(bgColor.b + 0.2f, 1f)
                );

                Register(new LevelData
                {
                    levelId = i,
                    levelName = $"STAGE {i}",
                    subtitle = subtitles[idx],
                    gameMode = GameMode.Stage,
                    difficultyType = DifficultyType.Normal,
                    isLocked = true,
                    unlockRequirement = i - 1,
                    lobbyDisplay = new LobbyDisplayConfig
                    {
                        backgroundColor = bgColor,
                        borderColor = borderColor,
                        buttonSize = 200f
                    }
                });
            }
        }

        /// <summary>
        /// Stage 모드 레벨 21~30 등록 (화산 심장 — 4종 전체 혼합)
        /// </summary>
        private static void RegisterStages21To30()
        {
            Color[] bgColors = new Color[]
            {
                new Color(0.70f, 0.25f, 0.15f),  // 21: 화산 주황
                new Color(0.75f, 0.22f, 0.12f),  // 22
                new Color(0.78f, 0.18f, 0.10f),  // 23
                new Color(0.82f, 0.15f, 0.08f),  // 24
                new Color(0.85f, 0.12f, 0.12f),  // 25: 중간 보스
                new Color(0.80f, 0.10f, 0.18f),  // 26
                new Color(0.75f, 0.08f, 0.22f),  // 27
                new Color(0.70f, 0.06f, 0.28f),  // 28
                new Color(0.65f, 0.05f, 0.35f),  // 29
                new Color(0.60f, 0.04f, 0.40f)   // 30: 최종 보스
            };

            string[] subtitles = new string[]
            {
                "기본10 + 갑옷8 + 궁수3 + 방패4",   // 21
                "기본12 + 갑옷10 + 궁수5 + 방패5",  // 22
                "방패8 + 갑옷12 + 기본8 + 궁수4",   // 23
                "궁수7 + 기본14 + 갑옷8 + 방패6",   // 24
                "★ 기본16 + 갑옷14 + 궁수6 + 방패8", // 25: 보스
                "갑옷18 + 기본10 + 방패7 + 궁수5",  // 26
                "방패10 + 기본12 + 갑옷12 + 궁수6",  // 27
                "기본14 + 갑옷14 + 궁수8 + 방패8",  // 28
                "기본16 + 갑옷16 + 방패10 + 궁수7",  // 29
                "★ 기본18 + 갑옷18 + 궁수8 + 방패12" // 30: 최종 보스
            };

            for (int i = 21; i <= 30; i++)
            {
                int idx = i - 21;
                Color bgColor = bgColors[idx];
                Color borderColor = new Color(
                    Mathf.Min(bgColor.r + 0.2f, 1f),
                    Mathf.Min(bgColor.g + 0.2f, 1f),
                    Mathf.Min(bgColor.b + 0.2f, 1f)
                );

                Register(new LevelData
                {
                    levelId = i,
                    levelName = $"STAGE {i}",
                    subtitle = subtitles[idx],
                    gameMode = GameMode.Stage,
                    difficultyType = DifficultyType.Hard,
                    isLocked = true,
                    unlockRequirement = i - 1,
                    lobbyDisplay = new LobbyDisplayConfig
                    {
                        backgroundColor = bgColor,
                        borderColor = borderColor,
                        buttonSize = 200f
                    }
                });
            }
        }

        /// <summary>
        /// Stage 모드 레벨 31~50 등록 (폭염의 화약고 — 폭탄고블린 등장, 5종 혼합)
        /// </summary>
        private static void RegisterStages31To50()
        {
            Color[] bgColors = new Color[]
            {
                new Color(0.80f, 0.45f, 0.10f),  // 31: 화약고 주황
                new Color(0.82f, 0.40f, 0.10f),  // 32
                new Color(0.84f, 0.36f, 0.10f),  // 33
                new Color(0.86f, 0.32f, 0.10f),  // 34
                new Color(0.88f, 0.28f, 0.12f),  // 35: 입문 마무리
                new Color(0.85f, 0.24f, 0.15f),  // 36
                new Color(0.82f, 0.20f, 0.18f),  // 37
                new Color(0.80f, 0.16f, 0.22f),  // 38
                new Color(0.78f, 0.13f, 0.26f),  // 39
                new Color(0.75f, 0.10f, 0.30f),  // 40: 5종 첫 등장
                new Color(0.70f, 0.08f, 0.35f),  // 41
                new Color(0.65f, 0.07f, 0.40f),  // 42
                new Color(0.60f, 0.06f, 0.45f),  // 43
                new Color(0.55f, 0.05f, 0.50f),  // 44
                new Color(0.50f, 0.04f, 0.55f),  // 45: 고강도
                new Color(0.45f, 0.04f, 0.58f),  // 46
                new Color(0.40f, 0.03f, 0.62f),  // 47
                new Color(0.35f, 0.03f, 0.66f),  // 48
                new Color(0.30f, 0.02f, 0.70f),  // 49
                new Color(0.25f, 0.02f, 0.75f)   // 50: 챕터 보스
            };

            string[] subtitles = new string[]
            {
                "기본4 + 폭탄1",                                    // 31: Easy
                "기본3 + 폭탄3",                                    // 32: Normal
                "궁수2 + 폭탄4",                                    // 33: Normal
                "갑옷2 + 폭탄4",                                    // 34: Hard
                "기본2 + 궁수2 + 방패2 + 폭탄2",                    // 35: Hard
                "기본2 + 갑옷1 + 폭탄5",                            // 36: Easy
                "갑옷1 + 궁수1 + 폭탄6",                            // 37: Normal
                "방패4 + 폭탄4",                                    // 38: Normal
                "갑옷2 + 방패2 + 폭탄4",                            // 39: Hard
                "★ 기본1 + 갑옷2 + 방패3 + 폭탄3",                  // 40: Hard
                "갑옷3 + 방패2 + 폭탄4",                            // 41: Easy
                "기본1 + 궁수3 + 방패2 + 폭탄3",                    // 42: Normal
                "기본1 + 갑옷2 + 폭탄6",                            // 43: Normal
                "궁수1 + 방패4 + 폭탄4",                            // 44: Hard
                "갑옷3 + 궁수2 + 방패2 + 폭탄4",                    // 45: Hard
                "갑옷3 + 방패2 + 폭탄6",                            // 46: Easy
                "갑옷3 + 방패4 + 폭탄4",                            // 47: Normal
                "기본1 + 갑옷4 + 방패3 + 폭탄4",                    // 48: Normal
                "갑옷3 + 방패2 + 폭탄7",                            // 49: Hard
                "★ 갑옷4 + 궁수2 + 방패3 + 폭탄5"                   // 50: Hard
            };

            for (int i = 31; i <= 50; i++)
            {
                int idx = i - 31;
                Color bgColor = bgColors[idx];
                Color borderColor = new Color(
                    Mathf.Min(bgColor.r + 0.2f, 1f),
                    Mathf.Min(bgColor.g + 0.2f, 1f),
                    Mathf.Min(bgColor.b + 0.2f, 1f)
                );

                // 난이도 패턴: 쉬움→보통→보통→어려움→어려움 (5레벨 반복)
                DifficultyType[] diffPattern = {
                    DifficultyType.Easy, DifficultyType.Normal, DifficultyType.Normal,
                    DifficultyType.Hard, DifficultyType.Hard
                };
                DifficultyType diffType = diffPattern[idx % 5];

                Register(new LevelData
                {
                    levelId = i,
                    levelName = $"STAGE {i}",
                    subtitle = subtitles[idx],
                    gameMode = GameMode.Stage,
                    difficultyType = diffType,
                    isLocked = true,
                    unlockRequirement = i - 1,
                    lobbyDisplay = new LobbyDisplayConfig
                    {
                        backgroundColor = bgColor,
                        borderColor = borderColor,
                        buttonSize = 200f
                    }
                });
            }
        }

        private static void RegisterStages51To60()
        {
            Color[] bgColors = new Color[]
            {
                new Color(0.15f, 0.55f, 0.25f),  // 51: 치유의 늪 초록
                new Color(0.18f, 0.58f, 0.28f),  // 52
                new Color(0.20f, 0.62f, 0.30f),  // 53
                new Color(0.22f, 0.66f, 0.32f),  // 54
                new Color(0.25f, 0.70f, 0.35f),  // 55
                new Color(0.20f, 0.60f, 0.30f),  // 56
                new Color(0.18f, 0.55f, 0.28f),  // 57
                new Color(0.15f, 0.50f, 0.25f),  // 58
                new Color(0.12f, 0.45f, 0.22f),  // 59
                new Color(0.10f, 0.40f, 0.20f)   // 60
            };

            string[] subtitles = new string[]
            {
                "힐러2 + 기본3 + 갑옷2 + 폭탄1",                  // 51: Easy
                "힐러2 + 갑옷3 + 방패2 + 폭탄2",                  // 52: Normal
                "힐러3 + 폭탄3 + 궁수2 + 기본1",                  // 53: Normal
                "힐러2 + 폭탄4 + 방패2 + 갑옷2",                  // 54: Hard
                "힐러3 + 폭탄4 + 방패2 + 궁수2 + 갑옷1",          // 55: Hard
                "힐러2 + 기본3 + 갑옷2 + 궁수2 + 폭탄2",          // 56: Easy
                "힐러2 + 폭탄4 + 갑옷3 + 방패2",                  // 57: Normal
                "힐러3 + 폭탄4 + 궁수3 + 방패2",                  // 58: Normal
                "힐러3 + 폭탄5 + 갑옷3 + 방패2",                  // 59: Hard
                "★ 힐러4 + 폭탄5 + 갑옷3 + 방패3 + 궁수2"         // 60: Hard
            };

            for (int i = 51; i <= 60; i++)
            {
                int idx = i - 51;
                Color bgColor = bgColors[idx];
                Color borderColor = new Color(
                    Mathf.Min(bgColor.r + 0.2f, 1f),
                    Mathf.Min(bgColor.g + 0.2f, 1f),
                    Mathf.Min(bgColor.b + 0.2f, 1f)
                );

                DifficultyType[] diffPattern = {
                    DifficultyType.Easy, DifficultyType.Normal, DifficultyType.Normal,
                    DifficultyType.Hard, DifficultyType.Hard
                };
                DifficultyType diffType = diffPattern[idx % 5];

                Register(new LevelData
                {
                    levelId = i,
                    levelName = $"STAGE {i}",
                    subtitle = subtitles[idx],
                    gameMode = GameMode.Stage,
                    difficultyType = diffType,
                    isLocked = true,
                    unlockRequirement = i - 1,
                    lobbyDisplay = new LobbyDisplayConfig
                    {
                        backgroundColor = bgColor,
                        borderColor = borderColor,
                        buttonSize = 200f
                    }
                });
            }
        }

        private static void Register(LevelData level)
        {
            levels[level.levelId] = level;
        }
    }
}

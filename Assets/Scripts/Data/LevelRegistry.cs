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
            // --- 레벨 1~10: Stage 모드 (크리스탈 숲 - 색상도둑 소탕) ---
            RegisterStageLevels();

            // --- 레벨 11: Stage 모드 (드릴 튜토리얼) ---
            Register(new LevelData
            {
                levelId = 11,
                levelName = "STAGE 11",
                subtitle = "드릴 튜토리얼",
                gameMode = GameMode.Stage,
                difficulty = 1,
                isLocked = true,
                unlockRequirement = 10,
                lobbyDisplay = new LobbyDisplayConfig
                {
                    backgroundColor = new Color(0.7f, 0.3f, 0.5f),
                    borderColor = new Color(1f, 0.5f, 0.7f),
                    buttonSize = 200f
                }
            });

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
                difficulty = 0,
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

            // 스테이지별 부제목
            string[] subtitles = new string[]
            {
                "기본 블록 100개 제거",          // 1 (유지)
                "빨강 30 + 초록 30 제거",        // 2 (유지)
                "파란색 블록 40개 제거",          // 3
                "노랑 35 + 보라 35 제거",        // 4
                "빨강 40 + 파랑 40 제거",        // 5
                "초록·노랑·보라 각 30개 제거",    // 6
                "빨강·파랑·초록 각 35개 제거",    // 7
                "4색 각 30개 제거",              // 8
                "4색 각 35개 제거",              // 9
                "5색 각 30개 제거"               // 10
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
                    difficulty = i >= 10 ? 3 : Mathf.Min(1 + i / 3, 3),
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

        private static void Register(LevelData level)
        {
            levels[level.levelId] = level;
        }
    }
}

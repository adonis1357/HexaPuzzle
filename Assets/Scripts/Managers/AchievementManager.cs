using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 업적 시스템
    /// 기획서 4. 업적 리스트 구현
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }
        
        
        
        
        // 업적 데이터
        private Dictionary<int, AchievementData> achievements = new Dictionary<int, AchievementData>();
        private Dictionary<int, AchievementProgress> progress = new Dictionary<int, AchievementProgress>();
        
        // 통계
        private Dictionary<StatType, long> stats = new Dictionary<StatType, long>();
        
        // 이벤트
        public event System.Action<AchievementData> OnAchievementUnlocked;
        public event System.Action<int> OnPinkDiamondEarned;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAchievements();
                LoadProgress();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        /// <summary>
        /// 업적 데이터 초기화
        /// </summary>
        private void InitializeAchievements()
        {
            // 원석 채광 업적 (No. 1-5)
            AddGemCollectionAchievements(GemType.Emerald, 100, "에메랄드");
            AddGemCollectionAchievements(GemType.Ruby, 120, "루비");
            AddGemCollectionAchievements(GemType.Sapphire, 140, "사파이어");
            AddGemCollectionAchievements(GemType.Amber, 160, "호박");
            AddGemCollectionAchievements(GemType.Amethyst, 180, "자수정");
            
            // 보석 가공 업적 (No. 6-25)
            AddGemProcessingAchievements();
            
            // 보석 획득 업적 (No. 26)
            AddTieredAchievement(501, "보석 최초 획득", StatType.GemsObtained, 1, 10);
            AddTieredAchievement(502, "보석 10개 획득", StatType.GemsObtained, 10, 20);
            AddTieredAchievement(503, "보석 100개 획득", StatType.GemsObtained, 100, 30);
            AddTieredAchievement(504, "보석 1000개 획득", StatType.GemsObtained, 1000, 50);
            AddTieredAchievement(505, "보석 1만개 획득", StatType.GemsObtained, 10000, 70);
            AddTieredAchievement(506, "보석 10만개 획득", StatType.GemsObtained, 100000, 100);
            AddTieredAchievement(507, "보석 100만개 획득", StatType.GemsObtained, 1000000, 200);
            
            // 특수보석 업적 (No. 27-28)
            AddTieredAchievement(511, "특수보석 최초 가공", StatType.SpecialGemsProcessed, 1, 50);
            AddTieredAchievement(512, "특수보석 10개 가공", StatType.SpecialGemsProcessed, 10, 70);
            AddTieredAchievement(513, "특수보석 100개 가공", StatType.SpecialGemsProcessed, 100, 100);
            AddTieredAchievement(521, "특수보석 최초 획득", StatType.SpecialGemsObtained, 1, 50);
            
            // 완전보석 업적 (No. 29-30)
            AddTieredAchievement(531, "완전보석 최초 가공", StatType.PerfectGemsProcessed, 1, 100);
            AddTieredAchievement(541, "완전보석 최초 획득", StatType.PerfectGemsObtained, 1, 100);
            
            // 빅뱅 업적 (No. 31)
            AddTieredAchievement(551, "빅뱅 최초 발생", StatType.BigBangs, 1, 1000);
            AddTieredAchievement(552, "빅뱅 10회 발생", StatType.BigBangs, 10, 2000);
            AddTieredAchievement(553, "빅뱅 30회 발생", StatType.BigBangs, 30, 3000);
            AddTieredAchievement(554, "빅뱅 50회 발생", StatType.BigBangs, 50, 5000);
            AddTieredAchievement(555, "빅뱅 70회 발생", StatType.BigBangs, 70, 7000);
            AddTieredAchievement(556, "빅뱅 100회 발생", StatType.BigBangs, 100, 10000);
            
            // 비닐 업적 (No. 32-33)
            AddTieredAchievement(561, "첫 비닐 벗기기", StatType.VinylsRemoved, 1, 1);
            AddTieredAchievement(571, "첫 2중 비닐 벗기기", StatType.DoubleVinylsRemoved, 1, 1);
            
            // 턴 업적 (No. 43)
            AddTieredAchievement(671, "첫 turn", StatType.TotalTurns, 1, 1);
            AddTieredAchievement(672, "turn 100번", StatType.TotalTurns, 100, 2);
            AddTieredAchievement(673, "turn 1000번", StatType.TotalTurns, 1000, 3);
            AddTieredAchievement(674, "turn 1만번", StatType.TotalTurns, 10000, 10);
            AddTieredAchievement(675, "turn 10만번", StatType.TotalTurns, 100000, 20);
            
            // 골드 업적 (No. 44)
            AddTieredAchievement(691, "첫 gold 획득", StatType.TotalGold, 1, 1);
            AddTieredAchievement(692, "gold 1만개 획득", StatType.TotalGold, 10000, 5);
            AddTieredAchievement(693, "gold 10만개 획득", StatType.TotalGold, 100000, 10);
            AddTieredAchievement(694, "gold 100만개 획득", StatType.TotalGold, 1000000, 50);
            AddTieredAchievement(695, "gold 1000만개 획득", StatType.TotalGold, 10000000, 100);
        }
        
        /// <summary>
        /// 원석 채광 업적 추가
        /// </summary>
        private void AddGemCollectionAchievements(GemType gemType, int baseIndex, string gemName)
        {
            int[] targets = { 1, 100, 1000, 10000, 100000, 1000000, 10000000 };
            int[] rewards = { 1, 2, 3, 5, 10, 50, 100 };
            string[] labels = { "최초", "100개", "1000개", "1만개", "10만개", "100만개", "1000만개" };
            
            StatType statType = GetGemStatType(gemType);
            
            for (int i = 0; i < targets.Length; i++)
            {
                int id = baseIndex + i + 1;
                string title = $"{gemName} 원석 {labels[i]} 채광";
                AddTieredAchievement(id, title, statType, targets[i], rewards[i]);
            }
        }
        
        /// <summary>
        /// 보석 가공 업적 추가
        /// </summary>
        private void AddGemProcessingAchievements()
        {
            // 간략화된 버전 - 전체 가공 업적
            int[] targets = { 1, 100, 1000, 10000, 100000 };
            int[] rewards = { 10, 20, 30, 50, 100 };
            
            for (int i = 0; i < targets.Length; i++)
            {
                int id = 301 + i;
                string title = targets[i] == 1 ? "보석 최초 가공" : $"보석 {targets[i]}개 가공";
                AddTieredAchievement(id, title, StatType.GemsProcessed, targets[i], rewards[i]);
            }
        }
        
        /// <summary>
        /// 단계별 업적 추가
        /// </summary>
        private void AddTieredAchievement(int id, string title, StatType statType, long target, int reward)
        {
            achievements[id] = new AchievementData
            {
                id = id,
                title = title,
                statType = statType,
                targetValue = target,
                pinkDiamondReward = reward
            };
        }
        
        /// <summary>
        /// 진행도 로드
        /// </summary>
        private void LoadProgress()
        {
            // 통계 로드
            foreach (StatType type in System.Enum.GetValues(typeof(StatType)))
            {
                long value = long.Parse(PlayerPrefs.GetString($"Stat_{type}", "0"));
                stats[type] = value;
            }
            
            // 업적 완료 상태 로드
            foreach (var kvp in achievements)
            {
                bool completed = PlayerPrefs.GetInt($"Achievement_{kvp.Key}", 0) == 1;
                progress[kvp.Key] = new AchievementProgress
                {
                    achievementId = kvp.Key,
                    isCompleted = completed,
                    isRewardClaimed = completed
                };
            }
        }
        
        /// <summary>
        /// 진행도 저장
        /// </summary>
        private void SaveProgress()
        {
            foreach (var kvp in stats)
            {
                PlayerPrefs.SetString($"Stat_{kvp.Key}", kvp.Value.ToString());
            }
            
            foreach (var kvp in progress)
            {
                PlayerPrefs.SetInt($"Achievement_{kvp.Key}", kvp.Value.isCompleted ? 1 : 0);
            }
            
            PlayerPrefs.Save();
        }
        
        /// <summary>
        /// 통계 증가
        /// </summary>
        public void IncrementStat(StatType type, long amount = 1)
        {
            if (!stats.ContainsKey(type))
                stats[type] = 0;
            
            stats[type] += amount;
            
            // 관련 업적 체크
            CheckAchievements(type);
            SaveProgress();
        }
        
        /// <summary>
        /// 업적 체크
        /// </summary>
        private void CheckAchievements(StatType type)
        {
            long currentValue = stats.ContainsKey(type) ? stats[type] : 0;
            
            foreach (var kvp in achievements)
            {
                if (kvp.Value.statType != type) continue;
                if (progress.ContainsKey(kvp.Key) && progress[kvp.Key].isCompleted) continue;
                
                if (currentValue >= kvp.Value.targetValue)
                {
                    UnlockAchievement(kvp.Key);
                }
            }
        }
        
        /// <summary>
        /// 업적 해금
        /// </summary>
        private void UnlockAchievement(int id)
        {
            if (!achievements.ContainsKey(id)) return;
            if (progress.ContainsKey(id) && progress[id].isCompleted) return;
            
            progress[id] = new AchievementProgress
            {
                achievementId = id,
                isCompleted = true,
                isRewardClaimed = false
            };
            
            AchievementData achievement = achievements[id];
            Debug.Log($"Achievement Unlocked: {achievement.title} (+{achievement.pinkDiamondReward} Pink Diamond)");
            
            OnAchievementUnlocked?.Invoke(achievement);
            
            // 자동 보상 지급
            ClaimReward(id);
        }
        
        /// <summary>
        /// 보상 수령
        /// </summary>
        public void ClaimReward(int id)
        {
            if (!progress.ContainsKey(id) || !progress[id].isCompleted || progress[id].isRewardClaimed)
                return;
            
            progress[id].isRewardClaimed = true;
            
            int reward = achievements[id].pinkDiamondReward;
            OnPinkDiamondEarned?.Invoke(reward);
            
            SaveProgress();
        }
        
        /// <summary>
        /// 원석 타입에 따른 통계 타입 반환
        /// </summary>
        private StatType GetGemStatType(GemType gemType)
        {
            return gemType switch
            {
                GemType.Emerald => StatType.EmeraldsCollected,
                GemType.Ruby => StatType.RubiesCollected,
                GemType.Sapphire => StatType.SapphiresCollected,
                GemType.Amber => StatType.AmbersCollected,
                GemType.Amethyst => StatType.AmethystsCollected,
                _ => StatType.TotalGemsCollected
            };
        }
        
        /// <summary>
        /// 통계 값 가져오기
        /// </summary>
        public long GetStat(StatType type)
        {
            return stats.ContainsKey(type) ? stats[type] : 0;
        }
        
        /// <summary>
        /// 모든 업적 목록 가져오기
        /// </summary>
        public List<AchievementData> GetAllAchievements()
        {
            return new List<AchievementData>(achievements.Values);
        }
        
        /// <summary>
        /// 업적 진행도 가져오기
        /// </summary>
        public AchievementProgress GetProgress(int id)
        {
            return progress.ContainsKey(id) ? progress[id] : null;
        }
    }
    
    /// <summary>
    /// 업적 데이터
    /// </summary>
    [System.Serializable]
    public class AchievementData
    {
        public int id;
        public string title;
        public string description;
        public StatType statType;
        public long targetValue;
        public int pinkDiamondReward;
        public Sprite icon;
    }
    
    /// <summary>
    /// 업적 진행도
    /// </summary>
    [System.Serializable]
    public class AchievementProgress
    {
        public int achievementId;
        public bool isCompleted;
        public bool isRewardClaimed;
    }
    
    /// <summary>
    /// 통계 타입
    /// </summary>
    public enum StatType
    {
        // 원석 채광
        EmeraldsCollected,
        RubiesCollected,
        SapphiresCollected,
        AmbersCollected,
        AmethystsCollected,
        TotalGemsCollected,
        
        // 보석 가공/획득
        GemsProcessed,
        GemsObtained,
        SpecialGemsProcessed,
        SpecialGemsObtained,
        PerfectGemsProcessed,
        PerfectGemsObtained,
        
        // 특수
        BigBangs,
        VinylsRemoved,
        DoubleVinylsRemoved,
        MoveBlocksRemoved,
        FixedBlocksRemoved,
        TimeBombsDefused,
        ChainsRemoved,
        
        // 물건
        ItemACollected,
        ItemBCollected,
        ItemCCollected,
        ItemDCollected,
        ItemECollected,
        
        // 기타
        TotalTurns,
        TotalGold,
        StagesCleared
    }
}

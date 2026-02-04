using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 스테이지 매니저
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        [Header("Stage Data")]
        [SerializeField] private StageDatabase stageDatabase;
        
        private StageData currentStageData;
        private List<MissionProgress> missionProgress = new List<MissionProgress>();
        
        // 이벤트
        public event System.Action<MissionProgress[]> OnMissionProgressUpdated;
        public event System.Action<int> OnMissionComplete;
        
        public StageData CurrentStageData => currentStageData;
        
        /// <summary>
        /// 스테이지 로드
        /// </summary>
        public void LoadStage(int stageNumber)
        {
            if (stageDatabase != null)
            {
                currentStageData = stageDatabase.GetStage(stageNumber);
            }
            else
            {
                // 기본 스테이지 생성
                currentStageData = GenerateDefaultStage(stageNumber);
            }
            
            InitializeMissions();
            
            Debug.Log($"Stage {stageNumber} loaded. Missions: {currentStageData.missions.Length}");
        }
        
        /// <summary>
        /// 기본 스테이지 생성 (데이터베이스 없을 때)
        /// </summary>
        private StageData GenerateDefaultStage(int stageNumber)
        {
            StageData stage = new StageData();
            stage.stageNumber = stageNumber;
            stage.turnLimit = 30 + (stageNumber / 10) * 5;
            
            // 스테이지 번호에 따른 미션 생성
            int missionCount = Mathf.Min(1 + stageNumber / 20, 3);
            stage.missions = new MissionData[missionCount];
            
            for (int i = 0; i < missionCount; i++)
            {
                stage.missions[i] = GenerateRandomMission(stageNumber, i);
            }
            
            return stage;
        }
        
        /// <summary>
        /// 랜덤 미션 생성
        /// </summary>
        private MissionData GenerateRandomMission(int stageNumber, int index)
        {
            MissionData mission = new MissionData();
            
            // 미션 타입 결정 (스테이지에 따라)
            if (stageNumber < 10)
            {
                // 초반: 단일 원석 채광
                mission.type = MissionType.CollectGem;
                mission.targetGemType = (GemType)Random.Range(1, 6);
                mission.targetCount = 10 + stageNumber * 2;
            }
            else if (stageNumber < 30)
            {
                // 중반: 복합 미션
                int missionRoll = Random.Range(0, 3);
                switch (missionRoll)
                {
                    case 0:
                        mission.type = MissionType.CollectGem;
                        mission.targetGemType = (GemType)Random.Range(1, 6);
                        mission.targetCount = 15 + stageNumber;
                        break;
                    case 1:
                        mission.type = MissionType.ProcessGem;
                        mission.targetCount = 3 + stageNumber / 10;
                        break;
                    case 2:
                        mission.type = MissionType.ReachScore;
                        mission.targetCount = 1000 * stageNumber;
                        break;
                }
            }
            else
            {
                // 후반: 고급 미션
                int missionRoll = Random.Range(0, 5);
                switch (missionRoll)
                {
                    case 0:
                        mission.type = MissionType.CollectGem;
                        mission.targetGemType = (GemType)Random.Range(1, 6);
                        mission.targetCount = 30 + stageNumber;
                        break;
                    case 1:
                        mission.type = MissionType.ProcessGem;
                        mission.targetCount = 5 + stageNumber / 15;
                        break;
                    case 2:
                        mission.type = MissionType.CreateSpecialGem;
                        mission.targetCount = 2 + stageNumber / 30;
                        break;
                    case 3:
                        mission.type = MissionType.RemoveVinyl;
                        mission.targetCount = 5 + stageNumber / 10;
                        break;
                    case 4:
                        mission.type = MissionType.TriggerBigBang;
                        mission.targetCount = 1;
                        break;
                }
            }
            
            return mission;
        }
        
        /// <summary>
        /// 미션 초기화
        /// </summary>
        private void InitializeMissions()
        {
            missionProgress.Clear();
            
            if (currentStageData?.missions == null) return;
            
            foreach (var mission in currentStageData.missions)
            {
                missionProgress.Add(new MissionProgress
                {
                    mission = mission,
                    currentCount = 0,
                    isComplete = false
                });
            }
        }
        
        /// <summary>
        /// 미션 진행도 체크
        /// </summary>
        public void CheckMissionProgress()
        {
            // GameManager에서 호출되어 현재 상태 확인
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
        }
        
        /// <summary>
        /// 원석 채광 시 호출
        /// </summary>
        public void OnGemCollected(GemType gemType, int count)
        {
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;
                
                if (progress.mission.type == MissionType.CollectGem)
                {
                    if (progress.mission.targetGemType == GemType.None || 
                        progress.mission.targetGemType == gemType)
                    {
                        progress.currentCount += count;
                        CheckMissionCompletion(i);
                    }
                }
            }
            
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
        }
        
        /// <summary>
        /// 보석 가공 시 호출
        /// </summary>
        public void OnGemProcessed(GemType centerType, GemType borderType)
        {
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;
                
                if (progress.mission.type == MissionType.ProcessGem)
                {
                    if (progress.mission.targetGemType == GemType.None ||
                        progress.mission.targetGemType == centerType)
                    {
                        progress.currentCount++;
                        CheckMissionCompletion(i);
                    }
                }
            }
            
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
        }
        
        /// <summary>
        /// 특수보석 생성 시 호출
        /// </summary>
        public void OnSpecialGemCreated()
        {
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;
                
                if (progress.mission.type == MissionType.CreateSpecialGem)
                {
                    progress.currentCount++;
                    CheckMissionCompletion(i);
                }
            }
            
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
        }
        
        /// <summary>
        /// 비닐 제거 시 호출
        /// </summary>
        public void OnVinylRemoved(bool isDouble)
        {
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;
                
                if (progress.mission.type == MissionType.RemoveVinyl ||
                    (isDouble && progress.mission.type == MissionType.RemoveDoubleVinyl))
                {
                    progress.currentCount++;
                    CheckMissionCompletion(i);
                }
            }
            
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
        }
        
        /// <summary>
        /// 빅뱅 발생 시 호출
        /// </summary>
        public void OnBigBangTriggered()
        {
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;
                
                if (progress.mission.type == MissionType.TriggerBigBang)
                {
                    progress.currentCount++;
                    CheckMissionCompletion(i);
                }
            }
            
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
        }
        
        /// <summary>
        /// 점수 추가 시 호출
        /// </summary>
        public void OnScoreAdded(int totalScore)
        {
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;
                
                if (progress.mission.type == MissionType.ReachScore)
                {
                    progress.currentCount = totalScore;
                    CheckMissionCompletion(i);
                }
            }
            
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
        }
        
        /// <summary>
        /// 미션 완료 체크
        /// </summary>
        private void CheckMissionCompletion(int index)
        {
            var progress = missionProgress[index];
            
            if (progress.currentCount >= progress.mission.targetCount && !progress.isComplete)
            {
                progress.isComplete = true;
                OnMissionComplete?.Invoke(index);
                Debug.Log($"Mission {index} complete!");
            }
        }
        
        /// <summary>
        /// 모든 미션 완료 여부
        /// </summary>
        public bool IsMissionComplete()
        {
            foreach (var progress in missionProgress)
            {
                if (!progress.isComplete) return false;
            }
            return missionProgress.Count > 0;
        }
        
        /// <summary>
        /// 현재 미션 진행도 가져오기
        /// </summary>
        public MissionProgress[] GetMissionProgress()
        {
            return missionProgress.ToArray();
        }
    }
    
    /// <summary>
    /// 스테이지 데이터
    /// </summary>
    [System.Serializable]
    public class StageData
    {
        public int stageNumber;
        public int turnLimit;
        public MissionData[] missions;
        public SpecialBlockPlacement[] specialBlocks;
    }
    
    /// <summary>
    /// 미션 데이터
    /// </summary>
    [System.Serializable]
    public class MissionData
    {
        public MissionType type;
        public GemType targetGemType;
        public GemType secondaryGemType;
        public int targetCount;
        public int currentCount;
        public Sprite icon;
    }
    
    /// <summary>
    /// 미션 진행도
    /// </summary>
    [System.Serializable]
    public class MissionProgress
    {
        public MissionData mission;
        public int currentCount;
        public bool isComplete;
    }
    
    /// <summary>
    /// 미션 타입
    /// </summary>
    public enum MissionType
    {
        CollectGem = 1,         // 원석 채광
        CollectMultiGem = 2,    // 복수 원석 채광
        ProcessGem = 3,         // 보석 가공
        CreateSpecialGem = 4,   // 특수보석 생성
        CreatePerfectGem = 5,   // 완전보석 생성
        TriggerBigBang = 6,     // 빅뱅 발생
        RemoveVinyl = 7,        // 비닐 제거
        RemoveDoubleVinyl = 8,  // 2중 비닐 제거
        MoveItem = 9,           // 물건 옮기기
        ReachScore = 10         // 점수 달성
    }
    
    /// <summary>
    /// 특수 블록 배치 데이터
    /// </summary>
    [System.Serializable]
    public class SpecialBlockPlacement
    {
        public int q;
        public int r;
        public SpecialBlockType type;
        public int parameter; // 시한폭탄 카운트, 비닐 레이어 등
    }
    
    /// <summary>
    /// 스테이지 데이터베이스 (ScriptableObject)
    /// </summary>
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "JewelsHexaPuzzle/Stage Database")]
    public class StageDatabase : ScriptableObject
    {
        public List<StageData> stages = new List<StageData>();
        
        public StageData GetStage(int stageNumber)
        {
            if (stageNumber <= 0 || stageNumber > stages.Count)
                return null;
            
            return stages[stageNumber - 1];
        }
    }
}

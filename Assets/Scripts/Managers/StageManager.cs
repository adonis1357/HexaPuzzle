using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 스테이지 매니저
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        [Header("Stage Data")]
        [SerializeField] private StageDatabase stageDatabase;

        [Header("Systems")]
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;

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
            // StageDatabase (ScriptableObject)에서 로드
            if (stageDatabase != null)
            {
                currentStageData = stageDatabase.GetStage(stageNumber);
            }

            // 폴백: 기본 생성
            if (currentStageData == null)
            {
                currentStageData = GenerateDefaultStage(stageNumber);
            }

            InitializeMissions();

            // 적군 제거 이벤트 연동
            if (blockRemovalSystem == null)
            {
                blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            }
            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.OnEnemyRemoved -= OnEnemyRemoved;  // 중복 구독 방지
                blockRemovalSystem.OnEnemyRemoved += OnEnemyRemoved;
            }

            Debug.Log($"Stage {stageNumber} loaded. Missions: {currentStageData.missions.Length}");
        }

        /// <summary>
        /// 외부에서 StageData를 직접 주입하여 로드 (LevelRegistry 경유 시 사용)
        /// </summary>
        public void LoadStageData(StageData data)
        {
            currentStageData = data;

            InitializeMissions();

            // 적군 제거 이벤트 연동
            if (blockRemovalSystem == null)
            {
                blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            }
            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.OnEnemyRemoved -= OnEnemyRemoved;  // 중복 구독 방지
                blockRemovalSystem.OnEnemyRemoved += OnEnemyRemoved;
            }

            Debug.Log($"Stage {data.stageNumber} loaded via StageData injection. Missions: {currentStageData.missions.Length}");
        }

        /// <summary>
        /// 적군 제거 시 호출 (미션 진행도 업데이트)
        /// </summary>
        private void OnEnemyRemoved(HexBlock block, EnemyType enemyType)
        {
            if (block == null) return;

            // 적군 제거 미션 진행도 업데이트
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var mission = missionProgress[i];
                if (mission.isComplete) continue;

                // RemoveEnemy 타입 미션 처리
                if (mission.mission.type == MissionType.RemoveEnemy &&
                    mission.mission.targetEnemyType == enemyType)
                {
                    mission.currentCount++;
                    CheckMissionCompletion(i);
                }
            }

            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
            Debug.Log($"[StageManager] 적군 제거: {EnemyTypeHelper.GetName(enemyType)}");
        }

        /// <summary>
        /// 고블린 제거 보고 (GoblinSystem에서 호출)
        /// isArmored에 따라 EnemyType.Goblin 또는 ArmoredGoblin 미션 진행도 업데이트
        /// </summary>
        public void ReportGoblinKill(bool isArmored, bool isArcher = false, bool isShieldType = false, bool isBomb = false)
        {
            EnemyType targetType;
            if (isBomb)
                targetType = EnemyType.BombGoblin;
            else if (isShieldType)
                targetType = EnemyType.ShieldGoblin;
            else if (isArcher)
                targetType = EnemyType.ArcherGoblin;
            else if (isArmored)
                targetType = EnemyType.ArmoredGoblin;
            else
                targetType = EnemyType.Goblin;

            for (int i = 0; i < missionProgress.Count; i++)
            {
                var mission = missionProgress[i];
                if (mission.isComplete) continue;

                if (mission.mission.type == MissionType.RemoveEnemy &&
                    mission.mission.targetEnemyType == targetType)
                {
                    mission.currentCount++;
                    CheckMissionCompletion(i);
                }
            }

            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
            string typeName = isBomb ? "폭탄" : isShieldType ? "방패" : (isArcher ? "활" : (isArmored ? "갑옷" : "몽둥이"));
            Debug.Log($"[StageManager] {typeName} 고블린 제거 보고");
        }

        /// <summary>
        /// 기본 스테이지 생성 (데이터베이스 없을 때)
        /// </summary>
        private StageData GenerateDefaultStage(int stageNumber)
        {
            StageData stage = new StageData();
            stage.stageNumber = stageNumber;

            // 레벨1: 무브 15회, 기본 블록 제거 100개 미션
            if (stageNumber == 1)
            {
                stage.turnLimit = 15;
                stage.missions = new MissionData[1];
                stage.missions[0] = new MissionData
                {
                    type = MissionType.CollectGem,
                    targetGemType = GemType.None,  // 모든 색상
                    targetCount = 100,
                    description = "기본 블록 제거 100개"
                };
            }
            else
            {
                stage.turnLimit = 30 + (stageNumber / 10) * 5;

                // 스테이지 번호에 따른 미션 생성
                int missionCount = Mathf.Min(1 + stageNumber / 20, 3);
                stage.missions = new MissionData[missionCount];

                for (int i = 0; i < missionCount; i++)
                {
                    stage.missions[i] = GenerateRandomMission(stageNumber, i);
                }
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
                mission.targetGemType = GemTypeHelper.GetRandom();
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
                        mission.targetGemType = GemTypeHelper.GetRandom();
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
                        mission.targetGemType = GemTypeHelper.GetRandom();
                        mission.targetCount = 30 + stageNumber;
                        break;
                    case 1:
                        mission.type = MissionType.ProcessGem;
                        mission.targetCount = 5 + stageNumber / 15;
                        break;
                    case 2:
                        // 랜덤 특수 블록 생성 미션
                        MissionType[] specialMissions = {
                            MissionType.CreateDrillVertical, MissionType.CreateDrillSlash,
                            MissionType.CreateDrillBackSlash, MissionType.CreateBomb,
                            MissionType.CreateRainbow
                        };
                        mission.type = specialMissions[Random.Range(0, specialMissions.Length)];
                        mission.targetCount = 1 + stageNumber / 30;
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

            if (currentStageData?.missions == null)
            {
                Debug.LogWarning("[StageManager] InitializeMissions: currentStageData or missions is null");
                return;
            }

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
            bool isBasicGem = (int)gemType >= 1 && (int)gemType <= 5;

            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;

                if (progress.mission.type == MissionType.CollectGem)
                {
                    if (progress.mission.targetGemType == GemType.None)
                    {
                        // 기본 블록만 카운트 (Red, Blue, Green, Yellow, Purple)
                        if (isBasicGem)
                        {
                            progress.currentCount += count;
                            CheckMissionCompletion(i);
                        }
                    }
                    else if (progress.mission.targetGemType == gemType)
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
        /// 특수 블록 생성 시 호출 (블록 타입 + 드릴 방향별 개별 미션 카운트)
        /// </summary>
        public void OnSpecialBlockCreatedDetailed(SpecialBlockType specialType, DrillDirection drillDir)
        {
            // 생성된 블록에 대응하는 미션 타입 결정
            MissionType targetMissionType = MissionType.CreateSpecialGem; // 기본값 (하위 호환)

            switch (specialType)
            {
                case SpecialBlockType.Drill:
                    switch (drillDir)
                    {
                        case DrillDirection.Vertical:  targetMissionType = MissionType.CreateDrillVertical; break;
                        case DrillDirection.Slash:      targetMissionType = MissionType.CreateDrillSlash; break;
                        case DrillDirection.BackSlash:  targetMissionType = MissionType.CreateDrillBackSlash; break;
                    }
                    break;
                case SpecialBlockType.Bomb:    targetMissionType = MissionType.CreateBomb; break;
                case SpecialBlockType.Rainbow: targetMissionType = MissionType.CreateRainbow; break;
                case SpecialBlockType.XBlock:  targetMissionType = MissionType.CreateXBlock; break;
                case SpecialBlockType.Drone:   targetMissionType = MissionType.CreateDrone; break;
            }

            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;

                // 정확히 일치하는 미션 타입만 카운트
                if (progress.mission.type == targetMissionType)
                {
                    progress.currentCount++;
                    CheckMissionCompletion(i);
                }
                // CreateDrillAny: 어떤 방향의 드릴이든 카운트
                else if (progress.mission.type == MissionType.CreateDrillAny &&
                         specialType == SpecialBlockType.Drill)
                {
                    progress.currentCount++;
                    CheckMissionCompletion(i);
                }
                // 구 CreateSpecialGem 미션은 모든 특수 블록 생성을 카운트 (하위 호환)
                else if (progress.mission.type == MissionType.CreateSpecialGem)
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

        private void OnDestroy()
        {
            // 이벤트 정리
            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.OnEnemyRemoved -= OnEnemyRemoved;
            }
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

        // Mission 1 확장 필드
        public int chapterNumber;
        public string chapterName;
        public int difficulty;
        public EnemyPlacement[] enemyPlacements;
        public EnemyPlacement[] fixedBlockPlacements;
        public StoryData storyData;
        public TutorialFlag[] tutorialFlags;
        public bool isBossStage = false;
        public RewardData rewards;
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
        public EnemyType targetEnemyType; // 적군 제거 미션용
        public int targetCount;
        public int currentCount;
        public Sprite icon;
        public string description; // 미션 설명
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
        CreateSpecialGem = 4,   // [사용 안 함] 구 특수보석 생성 (하위 호환용)
        CreatePerfectGem = 5,   // 완전보석 생성
        TriggerBigBang = 6,     // 빅뱅 발생
        RemoveVinyl = 7,        // 비닐 제거
        RemoveDoubleVinyl = 8,  // 2중 비닐 제거
        MoveItem = 9,           // 물건 옮기기
        ReachScore = 10,        // 점수 달성
        RemoveEnemy = 11,       // 적군 제거 (Mission 1)
        AchieveCombo = 12,      // 콤보 달성

        // === 특수 블록별 생성 미션 ===
        CreateDrillVertical = 20,   // 드릴 생성 (세로 ↕)
        CreateDrillSlash = 21,      // 드릴 생성 (슬래시 /)
        CreateDrillBackSlash = 22,  // 드릴 생성 (백슬래시 \)
        CreateDrillAny = 24,        // 드릴 생성 (아무 방향 — 3방향 모두 표시)
        CreateBomb = 23,            // 폭탄 생성
        CreateRainbow = 25,         // 레인보우(도넛) 생성
        CreateXBlock = 26,          // XBlock 생성
        CreateDrone = 27,           // 드론 생성

        // === 무한도전 전용 미션 ===
        SingleTurnRemoval = 30,     // 한 턴에 N개 제거
        AchieveCascade = 31,        // N연쇄 달성
        UseSpecial = 32             // 특수 블록 N회 사용
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

    /// <summary>
    /// 적군/고정 블록 배치
    /// </summary>
    [System.Serializable]
    public class EnemyPlacement
    {
        public HexCoord coord;
        public EnemyType enemyType;
    }

    /// <summary>
    /// 스토리 데이터
    /// </summary>
    [System.Serializable]
    public class StoryData
    {
        public string chapterIntroduction = "";
        public string beforeStageCutscene = "";
        public string[] stageIntroDialogues = new string[0];
        public DialogueCutscene[] midStageCutscenes = new DialogueCutscene[0];
        public string stageClearCutscene = "";
        public string[] stageClearDialogues = new string[0];
    }

    /// <summary>
    /// 게임 중 대사 컷씬
    /// </summary>
    [System.Serializable]
    public class DialogueCutscene
    {
        public CutsceneTrigger triggerType;
        public int triggerCount; // 예: AfterEnemyRemoval 이면 1,2,3...
        public string[] dialogues;
    }

    /// <summary>
    /// 컷씬 트리거 타입
    /// </summary>
    public enum CutsceneTrigger
    {
        AfterEnemyRemoval,      // N번째 적군 제거 후
        AfterMissionComplete,   // N번째 미션 완료 후
        AfterCombo,             // N콤보 달성 시
        AfterTurnCount          // N턴 경과 시
    }

    /// <summary>
    /// 튜토리얼 플래그
    /// </summary>
    public enum TutorialFlag
    {
        // 적군 소개
        ShowEnemyType_Chromophage,
        ShowEnemyType_ChainAnchor,
        ShowEnemyType_Thorn,
        ShowEnemyType_Divider,
        ShowEnemyType_GravityWarper,
        ShowEnemyType_ReflectionShield,
        ShowEnemyType_TimeFreezer,
        ShowEnemyType_ResonanceTwin,
        ShowEnemyType_ShadowSpore,
        ShowEnemyType_ChaosOverlord,

        // 특수 블록 소개
        ShowSpecialBlock_Drill,
        ShowSpecialBlock_Bomb,
        ShowSpecialBlock_Rainbow,
        ShowSpecialBlock_Drone,
        ShowSpecialBlock_XBlock,

        // 아이템 소개
        ShowItem_Hammer,
        ShowItem_ReverseRotation,

        // 미션 소개
        ShowMission_CollectGem,
        ShowMission_CreateSpecial,

        // 시스템 설명
        ExplainMatchingRestriction,
        ExplainCascadeChaining,
        ExplainTierSystem,
        ExplainRotationDirection,
        ExplainFreePlay,
        ExplainBasicRotation
    }

    /// <summary>
    /// 보상 데이터
    /// </summary>
    [System.Serializable]
    public class RewardData
    {
        public int baseExperience = 100;
        public int comboReward = 0;
        public int perfectClearReward = 0;
        public string badgeReward = "";
    }
}

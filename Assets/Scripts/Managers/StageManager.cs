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
        // ★ 미션 큐: 6개 초과 미션은 대기열에서 순차 활성화
        private Queue<MissionData> pendingMissions = new Queue<MissionData>();
        
        // 이벤트
        public event System.Action<MissionProgress[]> OnMissionProgressUpdated;
        public event System.Action<int> OnMissionComplete;
        /// <summary>미션 슬롯이 대기 미션으로 교체될 때 (slotIndex, newMission)</summary>
        public event System.Action<int, MissionData> OnMissionSlotReplaced;
        
        public StageData CurrentStageData => currentStageData;

        /// <summary>현재 활성 미션(완료 포함)의 MissionData 배열 반환 — UI 표시용</summary>
        public MissionData[] GetActiveMissions()
        {
            var active = new List<MissionData>();
            foreach (var p in missionProgress)
                active.Add(p.mission);
            return active.ToArray();
        }

        /// <summary>현재 활성 미션 중 미완료만 반환</summary>
        public MissionData[] GetIncompleteMissions()
        {
            var result = new List<MissionData>();
            foreach (var p in missionProgress)
            {
                if (!p.isComplete)
                    result.Add(p.mission);
            }
            return result.ToArray();
        }
        
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

            // 적군 제거 미션 진행도 업데이트 — 동일 타입 미션이 여러 개일 때 첫 번째만 증가
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
                    break; // ★ 첫 번째 매칭 미션만 카운트
                }
            }

            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
            Debug.Log($"[StageManager] 적군 제거: {EnemyTypeHelper.GetName(enemyType)}");
        }

        /// <summary>
        /// 고블린 제거 보고 (GoblinSystem에서 호출)
        /// isArmored에 따라 EnemyType.Goblin 또는 ArmoredGoblin 미션 진행도 업데이트
        /// </summary>
        /// <summary>
        /// 고블린 처치 플래그 → EnemyType 매핑 (ReportGoblinKill의 핵심 로직 추출, 검증 하니스에서 재사용).
        /// 소환 시 설정되는 플래그와 동일 의미 → 처치 시 올바른 미션 타입으로 매핑되는지 단일 출처.
        /// </summary>
        public static EnemyType MapKillFlagsToType(bool isArmored, bool isArcher, bool isShieldType,
            bool isBomb, bool isHealer, bool isHeavy, bool isWizard, bool isThief, bool isWitch, int monsterLevel)
        {
            // Lv2 판정 (기본 4종만 Lv2 존재)
            if (monsterLevel >= 2 && !isBomb && !isHealer && !isHeavy && !isWizard && !isThief && !isWitch)
            {
                if (isShieldType) return EnemyType.ShieldGoblinLv2;
                if (isArcher) return EnemyType.ArcherGoblinLv2;
                if (isArmored) return EnemyType.ArmoredGoblinLv2;
                return EnemyType.GoblinLv2;
            }
            if (isWitch) return EnemyType.WitchGoblin;
            if (isThief) return EnemyType.ThiefGoblin;
            if (isWizard) return EnemyType.WizardGoblin;
            if (isHeavy) return EnemyType.HeavyGoblin;
            if (isHealer) return EnemyType.HealerGoblin;
            if (isBomb) return EnemyType.BombGoblin;
            if (isShieldType) return EnemyType.ShieldGoblin;
            if (isArcher) return EnemyType.ArcherGoblin;
            if (isArmored) return EnemyType.ArmoredGoblin;
            return EnemyType.Goblin;
        }

        public void ReportGoblinKill(bool isArmored, bool isArcher = false, bool isShieldType = false,
            bool isBomb = false, bool isHealer = false, bool isHeavy = false, bool isWizard = false,
            bool isThief = false, bool isWitch = false, int monsterLevel = 1)
        {
            EnemyType targetType = MapKillFlagsToType(isArmored, isArcher, isShieldType, isBomb,
                isHealer, isHeavy, isWizard, isThief, isWitch, monsterLevel);

            // 동일 타입 미션이 여러 개일 때 첫 번째 미완료 미션만 증가
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var mission = missionProgress[i];
                if (mission.isComplete) continue;

                if (mission.mission.type == MissionType.RemoveEnemy &&
                    mission.mission.targetEnemyType == targetType)
                {
                    mission.currentCount++;
                    CheckMissionCompletion(i);
                    break; // ★ 첫 번째 매칭 미션만 카운트
                }
            }

            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
            string typeName = isThief ? "도둑" : isHeavy ? "헤비" : isHealer ? "힐러" : isBomb ? "폭탄" : isShieldType ? "방패" : (isArcher ? "활" : (isArmored ? "갑옷" : "몽둥이"));
            Debug.Log($"[StageManager] {typeName} 고블린 제거 보고");
        }

        /// <summary>
        /// 기본 스테이지 생성 (데이터베이스 없을 때)
        /// </summary>
        private StageData GenerateDefaultStage(int stageNumber)
        {
            StageData stage = new StageData();
            stage.stageNumber = stageNumber;

            // 레벨1: 새 튜토리얼 디자인 — Red 10개 → Green 10개 순차 미션, 이동 15회
            if (stageNumber == 1)
            {
                stage.turnLimit = 15;
                stage.missions = new MissionData[2];
                stage.missions[0] = new MissionData
                {
                    type = MissionType.CollectGem,
                    targetGemType = GemType.Red,
                    targetCount = 10,
                    description = "빨간 블록 10개 정화"
                };
                stage.missions[1] = new MissionData
                {
                    type = MissionType.CollectGem,
                    targetGemType = GemType.Green,
                    targetCount = 10,
                    description = "초록 블록 10개 정화"
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
        /// 미션 초기화 — 최대 4종 제한, 초과 시 대기열
        /// </summary>
        private const int MAX_ACTIVE_MISSIONS = MissionBalance.MAX_ACTIVE_MISSIONS;   // 단일 소스 (로비 뱃지와 동일 값 보장)

        private void InitializeMissions()
        {
            missionProgress.Clear();
            pendingMissions.Clear();

            if (currentStageData?.missions == null)
            {
                Debug.LogWarning("[StageManager] InitializeMissions: currentStageData or missions is null");
                return;
            }

            // ★ 미션 다중화 (MissionBalance) — 구간별 목표 미션 수까지 수집 미션 자동 보충.
            //   부족분은 대기 미션 큐로 들어가 기존 순차 활성화 시스템이 그대로 처리한다.
            var missions = MissionBalance.AugmentMissions(currentStageData.stageNumber, currentStageData.missions);
            if (missions.Length > currentStageData.missions.Length)
                Debug.Log($"[StageManager] 미션 보충: 기존 {currentStageData.missions.Length}종 → {missions.Length}종 (Lv{currentStageData.stageNumber})");

            // ★ 동적 미션 활성한도: 시작 = 밴드 최소값, 이동 30 소비마다 TryUnlockMissionSlot()로 +1 (밴드 최대까지).
            //   기존 StageData.maxActiveMissions(1/4 고정)는 폐기 — MissionBalance 밴드 공식이 단일 소스.
            dynamicActiveLimit = MissionBalance.GetMissionLimitMin(currentStageData.stageNumber);
            int activeLimit = dynamicActiveLimit;

            Debug.Log($"[StageManager] ★ InitializeMissions: 전체 미션 {missions.Length}종, 활성 한도={activeLimit} (최대 {MissionBalance.GetMissionLimitMax(currentStageData.stageNumber)})");

            // ★ 미션 큐 시스템: 처음 activeLimit개 활성화, 나머지 대기열
            for (int i = 0; i < missions.Length; i++)
            {
                if (missionProgress.Count < activeLimit)
                {
                    missionProgress.Add(new MissionProgress
                    {
                        mission = missions[i],
                        currentCount = 0,
                        isComplete = false
                    });
                    Debug.Log($"[StageManager] 활성 미션 추가: [{i}] {missions[i].description}");
                }
                else
                {
                    pendingMissions.Enqueue(missions[i]);
                    Debug.Log($"[StageManager] 대기 미션 추가: [{i}] {missions[i].description}");
                }
            }

            Debug.Log($"[StageManager] ★ 미션 큐 결과: 활성 {missionProgress.Count}종, 대기 {pendingMissions.Count}종");

            // ★ 자동 해금: 시작부터 대기 ≤ 잠금이면 즉시 활성화 (인트로가 늘어난 활성 미션을 그대로 표시)
            AutoUnlockIfPendingFits();

            // ★ 미션 타겟 색 강조 갱신 (플레이테스트 개선 #2)
            RefreshMissionTargetColors();
        }

        /// <summary>
        /// 활성(미완료) 수집 미션의 타겟 색을 MissionTargetColors 레지스트리에 반영하고
        /// 전체 블록 외곽선을 일괄 갱신한다. (새 블록은 UpdateVisuals 경로에서 자동 반영)
        /// </summary>
        private HexGrid cachedHexGridForHighlight;
        private void RefreshMissionTargetColors()
        {
            var colors = new List<GemType>();
            foreach (var p in missionProgress)
            {
                if (p == null || p.isComplete || p.mission == null) continue;
                var m = p.mission;
                if (m.type == MissionType.CollectGem || m.type == MissionType.CollectMultiGem)
                {
                    if (m.targetGemType != GemType.None) colors.Add(m.targetGemType);
                    if (m.secondaryGemType != GemType.None) colors.Add(m.secondaryGemType);
                }
            }
            MissionTargetColors.Set(colors);

            // 보드 위 기존 블록 외곽선 일괄 갱신 (1회성 — 프레임 비용 없음)
            if (cachedHexGridForHighlight == null)
                cachedHexGridForHighlight = FindObjectOfType<HexGrid>();
            if (cachedHexGridForHighlight != null)
            {
                foreach (var b in cachedHexGridForHighlight.GetAllBlocks())
                    if (b != null) b.RefreshBorderColor();
            }
        }

        /// <summary>
        /// 대기 미션을 활성 슬롯으로 승격합니다.
        /// 활성 미션이 완료되어 빈 슬롯이 생기면 호출됩니다.
        /// </summary>
        private void PromotePendingMissions()
        {
            if (pendingMissions.Count == 0) return;

            // 완료된 슬롯을 대기 미션으로 교체 (in-place)
            for (int i = 0; i < missionProgress.Count && pendingMissions.Count > 0; i++)
            {
                if (missionProgress[i].isComplete)
                {
                    var nextMission = pendingMissions.Dequeue();
                    missionProgress[i] = new MissionProgress
                    {
                        mission = nextMission,
                        currentCount = 0,
                        isComplete = false
                    };
                    OnMissionSlotReplaced?.Invoke(i, nextMission);
                    Debug.Log($"[StageManager] 미션 슬롯 [{i}] 교체: {nextMission.description} (남은 대기: {pendingMissions.Count})");
                }
            }

            // UI 갱신
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());

            // ★ 자동 해금: 승격으로 대기가 줄어 잠금 수 이하가 되면 즉시 전부 해금·활성화
            AutoUnlockIfPendingFits();
        }

        /// <summary>대기 중인 미션 수</summary>
        public int PendingMissionCount => pendingMissions.Count;

        // ★ 동적 미션 활성한도 — 시작=밴드 Min, 이동 30 소비마다 +1(밴드 Max 클램프).
        private int dynamicActiveLimit = MissionBalance.MAX_ACTIVE_MISSIONS;

        /// <summary>미션 한도 슬롯 해금 시 발화 (newLimit). UI 잠금 슬롯 갱신 + 해금 연출용.</summary>
        public event System.Action<int> OnMissionSlotUnlocked;

        /// <summary>현재 동시 활성 미션 한도 (동적 — 이동 30 소비마다 해금으로 증가).</summary>
        public int CurrentActiveLimit => dynamicActiveLimit;

        /// <summary>현재 스테이지의 활성한도 상한 (밴드 Max).</summary>
        public int MaxActiveLimit =>
            currentStageData != null
                ? MissionBalance.GetMissionLimitMax(currentStageData.stageNumber)
                : MissionBalance.MAX_ACTIVE_MISSIONS;

        /// <summary>
        /// 미션 한도 슬롯 1개 해금 — 이동(턴) 30 소비마다 GameManager가 호출.
        /// 한도 +1 후 대기 미션이 있으면 즉시 새 활성 슬롯으로 승격(몬스터 소환은
        /// 기존 OnMissionSlotReplaced 구독 경로가 처리). 상한 도달 시 false.
        /// </summary>
        public bool TryUnlockMissionSlot()
        {
            if (currentStageData == null) return false;
            int max = MissionBalance.GetMissionLimitMax(currentStageData.stageNumber);
            if (dynamicActiveLimit >= max) return false;

            dynamicActiveLimit++;
            // 새 슬롯 즉시 채움 — 대기 미션 큐 선두를 활성으로.
            //   (OnMissionSlotReplaced는 "완료 슬롯 교체" 전용 애니 경로라 여기선 쓰지 않는다 —
            //    추가 슬롯의 UI 재구축/연출/소환은 OnMissionSlotUnlocked 구독자(GameManager)가 담당.)
            if (pendingMissions.Count > 0)
            {
                var next = pendingMissions.Dequeue();
                missionProgress.Add(new MissionProgress
                {
                    mission = next,
                    currentCount = 0,
                    isComplete = false
                });
                RefreshMissionTargetColors();
            }
            Debug.Log($"[StageManager] ★ 미션 한도 해금: {dynamicActiveLimit - 1} → {dynamicActiveLimit} (최대 {max}), 활성 {missionProgress.Count}, 대기 {pendingMissions.Count}");
            OnMissionSlotUnlocked?.Invoke(dynamicActiveLimit);
            OnMissionProgressUpdated?.Invoke(missionProgress.ToArray());
            return true;
        }

        /// <summary>
        /// ★ 자동 해금(2026-07-02 사용자): 남은 대기 미션 수가 잠금 슬롯 수 이하로 떨어지는 순간
        /// (잠금1↔대기1, 잠금2↔대기2 …) 이동 해금 카운트와 무관하게 즉시 전부 해금·활성화.
        /// 대기가 잠금보다 많으면 기존 15이동 게이트 유지. 대기 수가 변하는 지점
        /// (초기화/승격/이동해금 직후)에서 호출된다.
        /// </summary>
        public void AutoUnlockIfPendingFits()
        {
            if (currentStageData == null) return;
            int guard = 0;
            while (guard++ < 10)
            {
                int locked = MissionBalance.GetMissionLimitMax(currentStageData.stageNumber) - dynamicActiveLimit;
                int pending = pendingMissions.Count;
                if (pending <= 0 || locked <= 0 || pending > locked) break;
                Debug.Log($"[StageManager] ★ 자동 해금: 대기 {pending} ≤ 잠금 {locked} — 이동 카운트 무시 즉시 활성화");
                if (!TryUnlockMissionSlot()) break;
            }
        }

        /// <summary>활성 + 대기 미션 전체 MissionData 반환 (UI 초기 생성용)</summary>
        public MissionData[] GetAllMissionData()
        {
            var all = new List<MissionData>();
            foreach (var p in missionProgress)
                all.Add(p.mission);
            foreach (var m in pendingMissions)
                all.Add(m);
            return all.ToArray();
        }

        /// <summary>
        /// 활성 + 대기 미션 전체를 MissionProgress 배열로 반환 (고블린 소환 시스템용).
        /// 대기 미션은 currentCount=0, isComplete=false 상태로 변환.
        /// </summary>
        public MissionProgress[] GetAllMissionProgress()
        {
            var all = new List<MissionProgress>();
            foreach (var p in missionProgress)
                all.Add(p);
            foreach (var m in pendingMissions)
                all.Add(new MissionProgress { mission = m, currentCount = 0, isComplete = false });
            return all.ToArray();
        }

        /// <summary>현재 활성(미완료+완료) 미션 수</summary>
        public int ActiveMissionCount => missionProgress.Count;

        /// <summary>대기열의 다음 미션을 제거하지 않고 반환 (UI 미리보기용)</summary>
        public MissionData PeekNextPendingMission()
        {
            return pendingMissions.Count > 0 ? pendingMissions.Peek() : null;
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
        /// 흙더미 1개가 완전 제거(2/3 → 1/3 → 0의 마지막 단계)될 때 호출.
        /// RemoveDirtMound 미션 진행도 +1.
        /// </summary>
        public void OnDirtMoundRemoved()
        {
            for (int i = 0; i < missionProgress.Count; i++)
            {
                var progress = missionProgress[i];
                if (progress.isComplete) continue;
                if (progress.mission.type != MissionType.RemoveDirtMound) continue;

                progress.currentCount += 1;
                CheckMissionCompletion(i);
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

                // ★ 대기 미션이 있으면 승격
                if (pendingMissions.Count > 0)
                    PromotePendingMissions();

                // ★ 미션 타겟 색 강조 갱신 — 완료된 미션 색 해제 + 승격된 미션 색 추가 (개선 #2)
                RefreshMissionTargetColors();
            }
        }
        
        /// <summary>
        /// 모든 미션 완료 여부
        /// </summary>
        public bool IsMissionComplete()
        {
            // ★ 대기 미션이 남아있으면 아직 미완료
            if (pendingMissions.Count > 0) return false;

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
    /// ★ 미션 다중화 + 보상 밸런스 규칙 (전체 레벨 공통 적용)
    ///
    /// 설계 의도:
    ///   - 레벨마다 목표 미션 수를 보장 — 기존 데이터의 미션이 부족하면 수집 미션을 자동 추가.
    ///   - 미션 1개 완료마다 이동 횟수를 보상 → 미션이 늘어나도 막히지 않고 플레이가 길어짐.
    ///   - 플레이가 길어진 만큼 주황 블록 파괴 누적 → 리워드 선택 기회 증가 (요청 사항).
    ///   - 21+ 레벨(주황 등장 구간)에서는 첫 추가 미션을 주황 수집으로 — 리워드 게이지와 직접 시너지.
    ///
    /// 밸런스 근거: 추가 수집 미션 1개(N개 수집) ≈ 매칭 3~6회 ≈ 이동 4~6회 소모.
    ///   미션 보상(+4~+7 이동)이 이를 거의 상쇄해 "미션 추가 = 플레이 연장"이 되고
    ///   난이도 급상승은 없다. 후반 구간은 수집량과 보상을 함께 올려 텐션 유지.
    ///
    /// | 구간       | 목표 미션 수 | 추가 수집량 | 미션 완료 보상(이동) |
    /// |-----------|------------|-----------|-------------------|
    /// | Lv 1-5    | 2          | 8         | +4                |
    /// | Lv 6-10   | 3          | 10        | +4                |
    /// | Lv 11-20  | 3          | 12        | +4                |
    /// | Lv 21-35  | 4          | 14        | +5                |
    /// | Lv 36-50  | 4          | 16        | +5                |
    /// | Lv 51-75  | 5          | 18        | +6                |
    /// | Lv 76-100 | 5          | 20        | +6                |
    /// | Lv 101+   | 6          | 22        | +7                |
    /// </summary>
    public static class MissionBalance
    {
        /// <summary>구간별 목표 미션 수 — 기존 미션이 이보다 적으면 수집 미션으로 보충.</summary>
        public static int GetTargetMissionCount(int stage)
        {
            if (stage <= 5) return 2;
            if (stage <= 20) return 3;
            // 21+ (리워드 등장 구간): 밴드별 "대기 미션 수"를 min→max로 선형 램프 + 활성제한(4)을 더해 목표 총 미션 수 산출.
            //   대기 목표(밴드): 21~30:2~3 / 31~40:2~4 / 41~50:2~5 / 51~60:3~5 / 61~70:3~6
            //                   71~80:3~7 / 81~90:4~7 / 91~100:4~8 / 101~150:5~9
            //   전 스테이지 base 미션 ≤ (대기+4)임을 데이터로 확인 → 보충으로 total=target 도달, 대기=램프값과 정확히 일치.
            //   (대기 미션 증가 = 주황 블록 파괴 기회↑ → 리워드 게이지 충전 빈도↑, 기존 의도 유지)
            return GetBandPendingTarget(stage) + MAX_ACTIVE_MISSIONS;
        }

        /// <summary>21+ 스테이지의 목표 "대기 미션 수" (밴드 내 min→max 선형 램프). 밴드 경계에서 다음 챕터로 리셋.</summary>
        public static int GetBandPendingTarget(int stage)
        {
            // ★ 2026-07-02 사용자: 시작(레벨 21 = 2) 유지, 150에서 기존(9)의 2배(18)로 —
            //   구 밴드 계단식(2→9)을 폐기하고 전 구간 선형으로 자연 증가.
            //   예: 30→3, 50→6, 80→9, 110→13, 150→18.
            if (stage <= 21) return 2;
            if (stage >= 150) return 18;
            return 2 + Mathf.RoundToInt((stage - 21) * 16f / 129f);
        }

        /// <summary>
        /// 추가 수집 미션 1개의 수집 목표량.
        /// ★ 가상 플레이테스트(800판) 보정: 6색 전환(Lv21+)에서 색상별 수집 속도가 −44% 급감
        ///   (2.27→1.28개/무브)하므로, 21+ 구간 수집량을 하향해 체감 비용을 5색 구간과 연속되게 유지.
        ///   (기존 14~22 → 체감 11~17무브의 절벽 / 보정 10~18 → 체감 7.8~14무브의 완만한 곡선)
        /// </summary>
        public static int GetCollectAmount(int stage)
        {
            if (stage <= 5) return 8;     // 5색(1-2는 3색): ≈3.5무브
            if (stage <= 10) return 10;   // ≈4.4무브
            if (stage <= 20) return 12;   // ≈5.3무브
            if (stage <= 35) return 10;   // 6색 전환: ≈7.8무브 (기존 14=11무브 절벽 해소)
            if (stage <= 50) return 12;   // ≈9.4무브
            if (stage <= 75) return 14;   // ≈11.0무브
            if (stage <= 100) return 16;  // ≈12.5무브
            return 18;                    // ≈14.1무브
        }

        /// <summary>
        /// 미션 1개 완료 시 이동 횟수 보상 — 미션별 **1~5 균일 랜덤(각 20%)**. (사용자 요청 2026-06-26: 기존 구간별 4~8이 과대)
        /// 최초 필요 시(프리뷰 또는 지급) 1회만 롤해 MissionData.moveReward에 저장 → 프리뷰 배지와 실제 지급이 항상 일치.
        /// mission이 null인 폴백 경로에서도 1~5를 반환(보상 누락 방지).
        /// </summary>
        public static int GetOrAssignMoveReward(MissionData mission)
        {
            if (mission == null) return UnityEngine.Random.Range(1, 6);            // 폴백: 1~5
            if (mission.moveReward <= 0) mission.moveReward = UnityEngine.Random.Range(1, 6); // 1~5 균일(Range는 [1,6) → 1·2·3·4·5 각 20%)
            return mission.moveReward;
        }

        /// <summary>
        /// 스테이지에서 실제 스폰되는 색상 풀 (GameManager.StartGameCoroutine의 색상 제약과 동기 유지 필수).
        /// Stage 1-2: R/G/B 3색 / 3-20: 5색 / 21+: 6색(주황 포함, 주황이 첫 순위).
        /// </summary>
        public static GemType[] GetAvailableColors(int stage)
        {
            if (stage <= 2)
                return new[] { GemType.Red, GemType.Green, GemType.Blue };
            if (stage <= 20)
                return new[] { GemType.Red, GemType.Blue, GemType.Green, GemType.Yellow, GemType.Purple };
            return new[] { GemType.Orange, GemType.Red, GemType.Blue, GemType.Green, GemType.Yellow, GemType.Purple };
        }

        private static string ColorNameKo(GemType g)
        {
            switch (g)
            {
                case GemType.Red:    return "빨간";
                case GemType.Blue:   return "파란";
                case GemType.Green:  return "초록";
                case GemType.Yellow: return "노란";
                case GemType.Purple: return "보라";
                case GemType.Orange: return "주황";
                default:             return g.ToString();
            }
        }

        /// <summary>
        /// 기존 미션 배열에 목표 수까지 수집 미션을 보충해 반환 (부족하지 않으면 원본 그대로).
        /// 색상 선택은 레벨 기반 결정적 순환 — 재시작해도 같은 미션 구성.
        /// 21+에서는 주황 미수집 시 주황을 최우선 추가 (리워드 시너지).
        /// </summary>
        public static MissionData[] AugmentMissions(int stage, MissionData[] baseMissions)
        {
            int target = GetTargetMissionCount(stage);
            int baseCount = baseMissions != null ? baseMissions.Length : 0;
            if (baseCount >= target) return baseMissions;

            var colors = GetAvailableColors(stage);

            // 기존 수집 미션이 쓰는 색은 후순위 (미션 다양성)
            var used = new System.Collections.Generic.HashSet<GemType>();
            if (baseMissions != null)
            {
                foreach (var m in baseMissions)
                {
                    if (m == null) continue;
                    if ((m.type == MissionType.CollectGem || m.type == MissionType.CollectMultiGem)
                        && m.targetGemType != GemType.None)
                        used.Add(m.targetGemType);
                }
            }

            var result = new System.Collections.Generic.List<MissionData>();
            if (baseMissions != null) result.AddRange(baseMissions);

            int amount = GetCollectAmount(stage);
            int cursor = stage; // 레벨 기반 결정적 시드
            int toAdd = target - baseCount;
            for (int k = 0; k < toAdd; k++)
            {
                GemType pick = GemType.None;
                // 1순위: 21+에서 주황 미사용이면 주황 (리워드 게이지 직접 충전)
                if (stage >= 21 && !used.Contains(GemType.Orange))
                {
                    pick = GemType.Orange;
                }
                else
                {
                    // 2순위: 미사용 색 순환 탐색
                    for (int t = 0; t < colors.Length; t++)
                    {
                        var c = colors[(cursor + t) % colors.Length];
                        if (!used.Contains(c)) { pick = c; break; }
                    }
                    // 전부 사용 중이면 순환 그대로
                    if (pick == GemType.None) pick = colors[(cursor + k) % colors.Length];
                }
                used.Add(pick);
                cursor++;

                result.Add(new MissionData
                {
                    type = MissionType.CollectGem,
                    targetGemType = pick,
                    targetCount = amount,
                    description = $"{ColorNameKo(pick)} 블록 {amount}개 수집"
                });
            }

            return result.ToArray();
        }

        /// <summary>동시 활성 가능한 미션 수 기본값 (스테이지가 maxActiveMissions를 지정하지 않을 때).</summary>
        public const int MAX_ACTIVE_MISSIONS = 4;

        // ============================================================
        // ★ 동적 미션 활성한도 밴드 (2026-07 사용자 설계, 07-02 확장)
        //   레벨 시작 시 한도 = Min, 이동(턴) 15 소비마다 +1, Max 클램프.
        //   잠금 슬롯 수(Max−Min): 11~20=1, 21~50=2, 51~80=3, 81~110=3, 111~150=4
        //   (150 기준 잠금 4 = 구(2)의 2배, 중간 밴드는 선형 보간으로 정형 상승 — 사용자 요청).
        //   기존 StageData.maxActiveMissions(1 또는 4 고정)는 폐기 — 이 밴드 공식이 단일 소스.
        // ============================================================
        /// <summary>스테이지 시작 시 미션 활성한도 (밴드 최소값).</summary>
        public static int GetMissionLimitMin(int stage)
        {
            if (stage <= 20) return 1;
            if (stage <= 80) return 2;
            return 3;
        }

        /// <summary>스테이지의 미션 활성한도 상한 (이동 15 소비마다 해금되는 최대).
        /// 잠금 슬롯 수(Max−Min)는 1→2→3→4→5 선형 상승, 150에서 5개(시스템 상한 6 이내 — 2026-07-02 정정).</summary>
        public static int GetMissionLimitMax(int stage)
        {
            if (stage <= 10) return 1;    // 잠금 0
            if (stage <= 20) return 2;    // 1+1 (잠금 1)
            if (stage <= 50) return 4;    // 2+2 (잠금 2)
            if (stage <= 80) return 5;    // 2+3 (잠금 3)
            if (stage <= 110) return 7;   // 3+4 (잠금 4)
            return 8;                      // 3+5 (잠금 5)
        }

        /// <summary>
        /// 스테이지의 "대기 미션 수"를 반환 — StageManager.InitializeMissions의 대기열(pendingMissions.Count)과 동일.
        /// = max(0, (보충 후 전체 미션 수) − 동시 활성 제한). MissionBalance 보충 미션까지 포함하므로 실제 인게임 대기 수와 정확히 일치.
        /// 로비 뱃지 표시와 게임 로직이 공유하는 단일 산식(중복 방지). 미정의 스테이지(StageData null)는 0.
        /// </summary>
        public static int GetPendingMissionCount(StageData data)
        {
            if (data == null) return 0;
            var baseMissions = data.missions ?? new MissionData[0];
            var augmented = AugmentMissions(data.stageNumber, baseMissions);
            int total = augmented != null ? augmented.Length : 0;
            // ★ 동적 한도: 시작 한도 = 밴드 최소값 (InitializeMissions와 동일 산식 유지)
            int activeLimit = GetMissionLimitMin(data.stageNumber);
            int pending = total - activeLimit;
            return pending > 0 ? pending : 0;
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
        /// <summary>동시 활성 미션 최대 수 (0이면 기본값 MAX_ACTIVE_MISSIONS 사용)</summary>
        public int maxActiveMissions = 0;
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
        public int moveReward;     // 미션 완료 시 이동 보상 (1~5 균일 랜덤, 최초 필요 시 1회 롤·저장 → 프리뷰=지급 일치)
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
        RemoveDirtMound = 13,   // 흙더미 제거 (Mission 1+ 확장)

        // === 특수 블록별 생성 미션 ===
        CreateDrillVertical = 20,   // 드릴 생성 (세로 ↕)
        CreateDrillSlash = 21,      // 드릴 생성 (슬래시 /)
        CreateDrillBackSlash = 22,  // 드릴 생성 (백슬래시 \)
        CreateDrillAny = 24,        // 드릴 생성 (아무 방향 — 3방향 모두 표시)
        CreateBomb = 23,            // 폭탄 생성
        CreateRainbow = 25,         // 레인보우타겟 레이저 생성
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

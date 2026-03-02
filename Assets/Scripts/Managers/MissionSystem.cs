using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.Managers
{
    // ============================================================
    // 무한 미션 생존 시스템
    // ============================================================

    public enum SurvivalMissionType
    {
        CollectGem,         // 특정 색상 보석 N개 수집
        CollectAny,         // 아무 보석 N개 수집
        CreateSpecial,      // 특수 블록 N개 생성
        AchieveCombo,       // 콤보 N 달성
        AchieveCascade,     // N연쇄 달성
        SingleTurnRemoval,  // 한 턴에 N개 제거
        ProcessGem,         // 보석 가공 N회
        ReachScore,         // 누적 점수 N점 달성
        UseSpecial,         // 특수 블록 N개 사용
        CollectMulti        // 2가지 색상 각 N개 수집
    }

    [System.Serializable]
    public class SurvivalMission
    {
        public SurvivalMissionType type;
        public GemType targetGemType;
        public GemType targetGemType2;              // CollectMulti용 두 번째 색상
        public SpecialBlockType targetSpecialType;
        public int targetCount;
        public int targetCount2;                    // CollectMulti용 두 번째 목표
        public int currentCount;
        public int currentCount2;                   // CollectMulti용 두 번째 카운트
        public string description;
        public int missionNumber;
        public int waveNumber;
        public int reward;

        public bool IsComplete
        {
            get
            {
                if (type == SurvivalMissionType.CollectMulti)
                    return currentCount >= targetCount && currentCount2 >= targetCount2;
                return currentCount >= targetCount;
            }
        }

        public float Progress
        {
            get
            {
                if (type == SurvivalMissionType.CollectMulti)
                {
                    float p1 = targetCount > 0 ? (float)currentCount / targetCount : 1f;
                    float p2 = targetCount2 > 0 ? (float)currentCount2 / targetCount2 : 1f;
                    return (p1 + p2) * 0.5f;
                }
                return targetCount > 0 ? (float)currentCount / targetCount : 1f;
            }
        }
    }

    public class WaveConfig
    {
        public int startMission;
        public int endMission;
        public int rewardMin;
        public int rewardMax;
        public float difficultyScale;
        public SurvivalMissionType[] missionPool;
    }

    /// <summary>
    /// 무한 미션 생존 시스템 — 싱글턴 MonoBehaviour
    /// 미션 생성/추적/보상 전담
    /// </summary>
    public class MissionSystem : MonoBehaviour
    {
        public static MissionSystem Instance { get; private set; }

        // 이벤트
        public event System.Action<SurvivalMission> OnMissionAssigned;
        public event System.Action<SurvivalMission> OnMissionProgressChanged;
        public event System.Action<SurvivalMission, int> OnMissionCompleted; // (mission, reward)

        // 현재 미션
        private SurvivalMission currentMission;
        public SurvivalMission CurrentMission => currentMission;

        // 다음 미션 미리보기 (UI 표시용 사전 생성)
        private SurvivalMission nextPreviewMission;
        public SurvivalMission NextPreviewMission => nextPreviewMission;

        // 미션 카운터
        private int completedMissionCount = 0;
        public int CompletedMissionCount => completedMissionCount;

        // 턴 내 누적 (SingleTurnRemoval용)
        private int turnRemovalCount = 0;

        // 캐스케이드 최대 깊이 (AchieveCascade용)
        private int maxCascadeThisTurn = 0;

        // 보류 완료 플래그
        private bool pendingMissionComplete = false;

        // 점수 기준점 (ReachScore용)
        private int scoreBaseline = 0;

        // 웨이브 설정 — 보석 수집 미션 중심
        // missionPool에서 동일 타입을 여러 번 넣으면 가중치가 올라감
        // 보상: 점진적 감소 (초반 관대 → 후반 엄격, 최소 3)
        private static readonly WaveConfig[] waves = new WaveConfig[]
        {
            // 웨이브 1 (#1~3): 보석 수집 전용, 초반 넉넉 — 보상 9~10
            new WaveConfig {
                startMission = 1, endMission = 3,
                rewardMin = 9, rewardMax = 10, difficultyScale = 1.0f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectAny, SurvivalMissionType.CollectAny }
            },
            // 웨이브 2 (#4~6): 보석 수집 전용 — 보상 8~9
            new WaveConfig {
                startMission = 4, endMission = 6,
                rewardMin = 8, rewardMax = 9, difficultyScale = 1.1f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectAny, SurvivalMissionType.CollectAny }
            },
            // 웨이브 3 (#7~10): 보석 수집 위주 + CollectMulti 등장 — 보상 7~8
            new WaveConfig {
                startMission = 7, endMission = 10,
                rewardMin = 7, rewardMax = 8, difficultyScale = 1.3f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectAny, SurvivalMissionType.CollectAny,
                    SurvivalMissionType.CollectMulti }
            },
            // 웨이브 4 (#11~15): 보석 수집 60% + 기타 미션 — 보상 6~7
            new WaveConfig {
                startMission = 11, endMission = 15,
                rewardMin = 6, rewardMax = 7, difficultyScale = 1.5f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectAny, SurvivalMissionType.CollectMulti,
                    SurvivalMissionType.CreateSpecial, SurvivalMissionType.AchieveCombo, SurvivalMissionType.UseSpecial }
            },
            // 웨이브 5 (#16~20): 보석 수집 55% + 다양한 미션 — 보상 5~6
            new WaveConfig {
                startMission = 16, endMission = 20,
                rewardMin = 5, rewardMax = 6, difficultyScale = 1.7f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectAny, SurvivalMissionType.CollectMulti,
                    SurvivalMissionType.CreateSpecial, SurvivalMissionType.AchieveCombo, SurvivalMissionType.UseSpecial }
            },
            // 웨이브 6 (#21~30): 보석 수집 50% + 다양한 미션 — 보상 4~5
            new WaveConfig {
                startMission = 21, endMission = 30,
                rewardMin = 4, rewardMax = 5, difficultyScale = 2.0f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectAny, SurvivalMissionType.CollectMulti, SurvivalMissionType.CollectMulti,
                    SurvivalMissionType.CreateSpecial, SurvivalMissionType.AchieveCombo,
                    SurvivalMissionType.SingleTurnRemoval, SurvivalMissionType.UseSpecial }
            },
            // 웨이브 7 (#31~45): 보석 수집 40% + 고난이도 — 보상 3~4
            new WaveConfig {
                startMission = 31, endMission = 45,
                rewardMin = 3, rewardMax = 4, difficultyScale = 2.3f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectMulti, SurvivalMissionType.CollectMulti,
                    SurvivalMissionType.CreateSpecial, SurvivalMissionType.AchieveCombo,
                    SurvivalMissionType.AchieveCascade, SurvivalMissionType.SingleTurnRemoval,
                    SurvivalMissionType.UseSpecial }
            },
            // 웨이브 8 (#46+): 최고 난이도 — 보상 고정 3
            new WaveConfig {
                startMission = 46, endMission = 9999,
                rewardMin = 3, rewardMax = 3, difficultyScale = 2.5f,
                missionPool = new[] {
                    SurvivalMissionType.CollectGem, SurvivalMissionType.CollectGem,
                    SurvivalMissionType.CollectMulti, SurvivalMissionType.CollectMulti,
                    SurvivalMissionType.CreateSpecial, SurvivalMissionType.AchieveCombo,
                    SurvivalMissionType.AchieveCascade, SurvivalMissionType.SingleTurnRemoval,
                    SurvivalMissionType.UseSpecial }
            }
        };

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        // ============================================================
        // 공개 API
        // ============================================================

        /// <summary>
        /// 생존 모드 시작 — 초기화 + 첫 미션 배정
        /// </summary>
        public void StartSurvival()
        {
            completedMissionCount = 0;
            turnRemovalCount = 0;
            maxCascadeThisTurn = 0;
            pendingMissionComplete = false;
            currentMission = null;
            nextPreviewMission = null;
            scoreBaseline = 0;

            AssignNextMission();
        }

        /// <summary>
        /// 생존 모드 시작 — InfiniteConfig 기반 오버로드.
        /// LevelRegistry에서 로드된 InfiniteConfig를 적용하여 시작.
        /// </summary>
        public void StartSurvival(InfiniteConfig config)
        {
            if (config == null)
            {
                StartSurvival();
                return;
            }

            // 활성 보석 색상 수 적용
            if (config.activeGemTypeCount > 0)
            {
                GemTypeHelper.ActiveGemTypeCount = config.activeGemTypeCount;
            }

            // 기본 StartSurvival 호출
            StartSurvival();
        }

        /// <summary>
        /// 다음 미션 배정
        /// 사전 생성된 nextPreviewMission이 있으면 그것을 사용, 없으면 새로 생성
        /// </summary>
        public void AssignNextMission()
        {
            int nextNumber = completedMissionCount + 1;

            // 사전 생성된 미리보기 미션이 있으면 사용
            if (nextPreviewMission != null)
            {
                currentMission = nextPreviewMission;
                nextPreviewMission = null;
            }
            else
            {
                WaveConfig wave = GetWaveForMission(nextNumber);
                SurvivalMissionType mType = wave.missionPool[Random.Range(0, wave.missionPool.Length)];
                currentMission = GenerateMission(mType, nextNumber, wave);
            }

            pendingMissionComplete = false;

            // ReachScore 기준점 설정
            if (currentMission.type == SurvivalMissionType.ReachScore)
            {
                var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                if (sm != null)
                    scoreBaseline = sm.CurrentScore;
            }

            // 다음 미션 미리보기 사전 생성
            GenerateNextPreview();

            Debug.Log($"[MissionSystem] 미션 #{nextNumber} 배정: {currentMission.description} (웨이브 {currentMission.waveNumber}, 보상 +{currentMission.reward})");
            OnMissionAssigned?.Invoke(currentMission);
        }

        /// <summary>
        /// 다음 미션 미리보기 사전 생성 (UI 표시용)
        /// </summary>
        private void GenerateNextPreview()
        {
            int previewNumber = completedMissionCount + 2; // 현재 +1이 배정됨, +2가 다음
            WaveConfig wave = GetWaveForMission(previewNumber);
            SurvivalMissionType mType = wave.missionPool[Random.Range(0, wave.missionPool.Length)];
            nextPreviewMission = GenerateMission(mType, previewNumber, wave);
            Debug.Log($"[MissionSystem] 다음 미션 미리보기 생성: #{previewNumber} {nextPreviewMission.description}");
        }

        // ============================================================
        // 이벤트 수신 메서드 (GameManager에서 연결)
        // ============================================================

        /// <summary>
        /// 보석 제거 추적
        /// </summary>
        public void OnGemsRemoved(int count, GemType gemType, int cascadeDepth)
        {
            if (currentMission == null || currentMission.IsComplete) return;

            // 턴 내 누적
            turnRemovalCount += count;

            // 캐스케이드 깊이 추적
            if (cascadeDepth > maxCascadeThisTurn)
                maxCascadeThisTurn = cascadeDepth;

            switch (currentMission.type)
            {
                case SurvivalMissionType.CollectGem:
                    if (gemType == currentMission.targetGemType)
                    {
                        currentMission.currentCount += count;
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.CollectAny:
                    if (gemType != GemType.None && gemType != GemType.Gray)
                    {
                        currentMission.currentCount += count;
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.CollectMulti:
                    if (gemType == currentMission.targetGemType)
                    {
                        currentMission.currentCount += count;
                        NotifyProgress();
                    }
                    else if (gemType == currentMission.targetGemType2)
                    {
                        currentMission.currentCount2 += count;
                        NotifyProgress();
                    }
                    break;
            }

            CheckPendingComplete();
        }

        /// <summary>
        /// 특수 블록 생성 추적
        /// </summary>
        public void OnSpecialBlockCreated(SpecialBlockType type, DrillDirection drillDir = DrillDirection.Vertical)
        {
            if (currentMission == null || currentMission.IsComplete) return;

            if (currentMission.type == SurvivalMissionType.CreateSpecial)
            {
                if (currentMission.targetSpecialType == SpecialBlockType.None || currentMission.targetSpecialType == type)
                {
                    currentMission.currentCount++;
                    NotifyProgress();
                    CheckPendingComplete();
                }
            }
        }

        /// <summary>
        /// 특수 블록 사용(발동) 추적
        /// </summary>
        public void OnSpecialBlockUsed()
        {
            if (currentMission == null || currentMission.IsComplete) return;

            if (currentMission.type == SurvivalMissionType.UseSpecial)
            {
                currentMission.currentCount++;
                NotifyProgress();
                CheckPendingComplete();
            }
        }

        /// <summary>
        /// 특수 블록 실행으로 제거된 블록들 추적 (CollectAny, CollectGem, CollectMulti 미션용)
        /// </summary>
        public void OnSpecialBlockDestroyedBlocks(List<HexBlock> destroyedBlocks)
        {
            if (currentMission == null || currentMission.IsComplete || destroyedBlocks == null || destroyedBlocks.Count == 0)
                return;

            // 블록들을 색상별로 그룹화
            Dictionary<GemType, int> gemTypeCount = new Dictionary<GemType, int>();

            foreach (var block in destroyedBlocks)
            {
                if (block != null && block.Data != null)
                {
                    GemType gemType = block.Data.gemType;

                    if (gemType != GemType.None && gemType != GemType.Gray)
                    {
                        if (!gemTypeCount.ContainsKey(gemType))
                            gemTypeCount[gemType] = 0;
                        gemTypeCount[gemType]++;
                    }
                }
            }

            // 미션 타입별로 처리
            switch (currentMission.type)
            {
                case SurvivalMissionType.CollectGem:
                    if (gemTypeCount.ContainsKey(currentMission.targetGemType))
                    {
                        currentMission.currentCount += gemTypeCount[currentMission.targetGemType];
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.CollectAny:
                    int totalCount = 0;
                    foreach (var count in gemTypeCount.Values)
                        totalCount += count;
                    if (totalCount > 0)
                    {
                        currentMission.currentCount += totalCount;
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.CollectMulti:
                    if (gemTypeCount.ContainsKey(currentMission.targetGemType))
                    {
                        currentMission.currentCount += gemTypeCount[currentMission.targetGemType];
                        NotifyProgress();
                    }
                    if (gemTypeCount.ContainsKey(currentMission.targetGemType2))
                    {
                        currentMission.currentCount2 += gemTypeCount[currentMission.targetGemType2];
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.SingleTurnRemoval:
                    int turnTotal = 0;
                    foreach (var count in gemTypeCount.Values)
                        turnTotal += count;
                    turnRemovalCount += turnTotal;
                    break;
            }

            CheckPendingComplete();
        }

        /// <summary>
        /// 특수 블록 실행으로 제거된 블록들 추적 (색상별 Dictionary 버전)
        /// GameManager에서 통합 호출됨 (Stage/Survival 모드 공통 경로)
        /// </summary>
        public void OnSpecialBlockDestroyedByColor(Dictionary<GemType, int> gemCounts)
        {
            if (currentMission == null || currentMission.IsComplete || gemCounts == null || gemCounts.Count == 0)
                return;

            switch (currentMission.type)
            {
                case SurvivalMissionType.CollectGem:
                    if (gemCounts.ContainsKey(currentMission.targetGemType))
                    {
                        currentMission.currentCount += gemCounts[currentMission.targetGemType];
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.CollectAny:
                    int totalCount = 0;
                    foreach (var count in gemCounts.Values)
                        totalCount += count;
                    if (totalCount > 0)
                    {
                        currentMission.currentCount += totalCount;
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.CollectMulti:
                    if (gemCounts.ContainsKey(currentMission.targetGemType))
                    {
                        currentMission.currentCount += gemCounts[currentMission.targetGemType];
                        NotifyProgress();
                    }
                    if (gemCounts.ContainsKey(currentMission.targetGemType2))
                    {
                        currentMission.currentCount2 += gemCounts[currentMission.targetGemType2];
                        NotifyProgress();
                    }
                    break;

                case SurvivalMissionType.SingleTurnRemoval:
                    int turnTotal = 0;
                    foreach (var count in gemCounts.Values)
                        turnTotal += count;
                    turnRemovalCount += turnTotal;
                    break;
            }

            CheckPendingComplete();
        }

        /// <summary>
        /// 보석 가공 추적
        /// </summary>
        public void OnGemProcessed()
        {
            if (currentMission == null || currentMission.IsComplete) return;

            if (currentMission.type == SurvivalMissionType.ProcessGem)
            {
                currentMission.currentCount++;
                NotifyProgress();
                CheckPendingComplete();
            }
        }

        /// <summary>
        /// 콤보 추적
        /// </summary>
        public void OnComboReached(int combo)
        {
            if (currentMission == null || currentMission.IsComplete) return;

            if (currentMission.type == SurvivalMissionType.AchieveCombo)
            {
                if (combo >= currentMission.targetCount)
                {
                    currentMission.currentCount = currentMission.targetCount;
                    NotifyProgress();
                    CheckPendingComplete();
                }
                else if (combo > currentMission.currentCount)
                {
                    currentMission.currentCount = combo;
                    NotifyProgress();
                }
            }
        }

        /// <summary>
        /// 점수 추적
        /// </summary>
        public void OnScoreChanged(int totalScore)
        {
            if (currentMission == null || currentMission.IsComplete) return;

            if (currentMission.type == SurvivalMissionType.ReachScore)
            {
                int gained = totalScore - scoreBaseline;
                if (gained > currentMission.currentCount)
                {
                    currentMission.currentCount = gained;
                    NotifyProgress();
                    CheckPendingComplete();
                }
            }
        }

        /// <summary>
        /// 턴 종료 처리 — 캐스케이드 완료 후 호출
        /// SingleTurnRemoval 체크 + 보류 완료 처리
        /// </summary>
        public void OnTurnEnd()
        {
            // SingleTurnRemoval 체크
            if (currentMission != null && !currentMission.IsComplete &&
                currentMission.type == SurvivalMissionType.SingleTurnRemoval)
            {
                if (turnRemovalCount > currentMission.currentCount)
                {
                    currentMission.currentCount = turnRemovalCount;
                    NotifyProgress();
                }
                if (currentMission.IsComplete)
                    pendingMissionComplete = true;
            }

            // AchieveCascade 체크
            if (currentMission != null && !currentMission.IsComplete &&
                currentMission.type == SurvivalMissionType.AchieveCascade)
            {
                if (maxCascadeThisTurn > currentMission.currentCount)
                {
                    currentMission.currentCount = maxCascadeThisTurn;
                    NotifyProgress();
                }
                if (currentMission.IsComplete)
                    pendingMissionComplete = true;
            }

            // 보류 완료 처리
            if (pendingMissionComplete && currentMission != null && currentMission.IsComplete)
            {
                CompleteMission();
            }

            // 턴 내 누적 리셋
            turnRemovalCount = 0;
            maxCascadeThisTurn = 0;
        }

        // ============================================================
        // 내부 메서드
        // ============================================================

        private void NotifyProgress()
        {
            if (currentMission != null)
                OnMissionProgressChanged?.Invoke(currentMission);
        }

        private void CheckPendingComplete()
        {
            if (currentMission != null && currentMission.IsComplete)
                pendingMissionComplete = true;
        }

        private void CompleteMission()
        {
            if (currentMission == null) return;

            completedMissionCount++;
            int reward = currentMission.reward;

            Debug.Log($"[MissionSystem] 미션 #{currentMission.missionNumber} 완료! 보상: +{reward} 이동");
            OnMissionCompleted?.Invoke(currentMission, reward);

            pendingMissionComplete = false;
            currentMission = null;
        }

        private WaveConfig GetWaveForMission(int missionNumber)
        {
            for (int i = waves.Length - 1; i >= 0; i--)
            {
                if (missionNumber >= waves[i].startMission)
                    return waves[i];
            }
            return waves[0];
        }

        private int GetWaveNumber(int missionNumber)
        {
            for (int i = waves.Length - 1; i >= 0; i--)
            {
                if (missionNumber >= waves[i].startMission)
                    return i + 1;
            }
            return 1;
        }

        // ============================================================
        // 미션 생성
        // ============================================================

        private SurvivalMission GenerateMission(SurvivalMissionType type, int missionNumber, WaveConfig wave)
        {
            var m = new SurvivalMission
            {
                type = type,
                missionNumber = missionNumber,
                waveNumber = GetWaveNumber(missionNumber),
                reward = Random.Range(wave.rewardMin, wave.rewardMax + 1)
            };

            float scale = wave.difficultyScale;

            switch (type)
            {
                case SurvivalMissionType.CollectGem:
                    m.targetGemType = GetRandomGemType();
                    m.targetCount = ScaledTarget(8, 14, scale);
                    m.description = $"{GetGemName(m.targetGemType)} 보석 {m.targetCount}개 모으기";
                    break;

                case SurvivalMissionType.CollectAny:
                    m.targetCount = ScaledTarget(15, 24, scale);
                    m.description = $"보석 {m.targetCount}개 모으기";
                    break;

                case SurvivalMissionType.CreateSpecial:
                    m.targetSpecialType = GetRandomSpecialType();
                    m.targetCount = ScaledTarget(1, 2, scale * 0.7f);
                    if (m.targetCount < 1) m.targetCount = 1;
                    m.description = $"{GetSpecialName(m.targetSpecialType)} {m.targetCount}개 만들기";
                    break;

                case SurvivalMissionType.AchieveCombo:
                    m.targetCount = ScaledTarget(3, 4, scale * 0.6f);
                    if (m.targetCount < 2) m.targetCount = 2;
                    m.description = $"콤보 {m.targetCount} 달성";
                    break;

                case SurvivalMissionType.AchieveCascade:
                    m.targetCount = ScaledTarget(2, 3, scale * 0.5f);
                    if (m.targetCount < 2) m.targetCount = 2;
                    m.description = $"{m.targetCount}연쇄 달성";
                    break;

                case SurvivalMissionType.SingleTurnRemoval:
                    m.targetCount = ScaledTarget(10, 17, scale);
                    m.description = $"한 턴에 {m.targetCount}개 제거";
                    break;

                case SurvivalMissionType.ProcessGem:
                    m.targetCount = ScaledTarget(1, 2, scale * 0.5f);
                    if (m.targetCount < 1) m.targetCount = 1;
                    m.description = $"보석 가공 {m.targetCount}회";
                    break;

                case SurvivalMissionType.ReachScore:
                    m.targetCount = ScaledTarget(800, 1200, scale) * 10;
                    m.description = $"{m.targetCount:N0}점 획득";
                    break;

                case SurvivalMissionType.UseSpecial:
                    m.targetCount = ScaledTarget(1, 2, scale * 0.6f);
                    if (m.targetCount < 1) m.targetCount = 1;
                    m.description = $"특수 블록 {m.targetCount}개 사용";
                    break;

                case SurvivalMissionType.CollectMulti:
                    m.targetGemType = GetRandomGemType();
                    m.targetGemType2 = GetRandomGemTypeExcluding(m.targetGemType);
                    int multiTarget = ScaledTarget(3, 5, scale * 0.6f);
                    if (multiTarget < 3) multiTarget = 3;
                    m.targetCount = multiTarget;
                    m.targetCount2 = multiTarget;
                    m.description = $"{GetGemName(m.targetGemType)} {m.targetCount}개 + {GetGemName(m.targetGemType2)} {m.targetCount2}개";
                    break;
            }

            return m;
        }

        private int ScaledTarget(int min, int max, float scale)
        {
            float baseVal = Random.Range(min, max + 1);
            return Mathf.RoundToInt(baseVal * scale);
        }

        private GemType GetRandomGemType()
        {
            return (GemType)Random.Range(1, GemTypeHelper.ActiveGemTypeCount + 1);
        }

        private GemType GetRandomGemTypeExcluding(GemType exclude)
        {
            GemType result;
            int safety = 20;
            do
            {
                result = GetRandomGemType();
                safety--;
            } while (result == exclude && safety > 0);
            return result;
        }

        private SpecialBlockType GetRandomSpecialType()
        {
            SpecialBlockType[] types = { SpecialBlockType.Drill, SpecialBlockType.Bomb };
            return types[Random.Range(0, types.Length)];
        }

        private string GetGemName(GemType type)
        {
            switch (type)
            {
                case GemType.Red: return "빨간";
                case GemType.Blue: return "파란";
                case GemType.Green: return "초록";
                case GemType.Yellow: return "노란";
                case GemType.Purple: return "보라";
                case GemType.Orange: return "주황";
                default: return "보석";
            }
        }

        private string GetSpecialName(SpecialBlockType type)
        {
            switch (type)
            {
                case SpecialBlockType.Drill: return "드릴";
                case SpecialBlockType.Bomb: return "폭탄";
                case SpecialBlockType.Rainbow: return "무지개";
                case SpecialBlockType.XBlock: return "X블록";
                default: return "특수 블록";
            }
        }
    }
}

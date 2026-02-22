using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 미션 1 스테이지 로더
    ///
    /// 역할:
    /// - Mission1StageData에서 Stage 1-10 데이터 로드
    /// - HexGrid에 적군/고정 블록 배치
    /// - StageManager와 통합하여 게임 시작
    ///
    /// 사용 예시:
    /// Mission1StageLoader loader = new Mission1StageLoader();
    /// loader.LoadStage(1, hexGrid, stageManager);
    /// </summary>
    public class Mission1StageLoader : MonoBehaviour
    {
        private HexGrid hexGrid;
        private StageManager stageManager;

        /// <summary>
        /// Stage 데이터를 로드하고 HexGrid에 배치
        /// </summary>
        public void LoadStage(int stageNumber, HexGrid targetHexGrid, StageManager targetStageManager)
        {
            if (stageNumber < 1 || stageNumber > 10)
            {
                Debug.LogError($"미션 1은 Stage 1-10만 지원합니다. 요청: {stageNumber}");
                return;
            }

            hexGrid = targetHexGrid;
            stageManager = targetStageManager;

            // 스테이지 데이터 가져오기
            var allStages = Mission1StageData.GetAllMission1Stages();
            if (!allStages.TryGetValue(stageNumber, out var stageData))
            {
                Debug.LogError($"Stage {stageNumber} 데이터를 찾을 수 없습니다.");
                return;
            }

            // 1. 보드 초기화
            hexGrid.InitializeGrid();

            // 2. 고정 블록 배치
            PlaceFixedBlocks(stageData);

            // 3. 적군 배치
            PlaceEnemies(stageData);

            // 4. StageManager에 스테이지 정보 전달
            LoadStageToManager(stageData);

            // 5. 튜토리얼 플래그 처리
            ProcessTutorialFlags(stageData);

            // 6. 스토리 컷씬 표시 (선택사항)
            ShowStoryContent(stageData);

            Debug.Log($"[Mission1StageLoader] Stage {stageNumber} 로드 완료");
        }

        /// <summary>
        /// 고정 블록(장애물) 배치
        /// </summary>
        private void PlaceFixedBlocks(StageData stageData)
        {
            if (stageData.fixedBlockPlacements == null || stageData.fixedBlockPlacements.Length == 0)
                return;

            foreach (var placement in stageData.fixedBlockPlacements)
            {
                if (hexGrid.IsValidCoord(placement.coord))
                {
                    var block = hexGrid.GetBlock(placement.coord);
                    if (block != null)
                    {
                        block.Data.specialType = SpecialBlockType.FixedBlock;
                        block.UpdateVisuals();
                        Debug.Log($"고정 블록 배치: {placement.coord}");
                    }
                }
            }
        }

        /// <summary>
        /// 적군 배치
        /// </summary>
        private void PlaceEnemies(StageData stageData)
        {
            if (stageData.enemyPlacements == null || stageData.enemyPlacements.Length == 0)
                return;

            foreach (var placement in stageData.enemyPlacements)
            {
                if (hexGrid.IsValidCoord(placement.coord))
                {
                    var block = hexGrid.GetBlock(placement.coord);
                    if (block != null)
                    {
                        block.SetEnemyType(placement.enemyType);
                        Debug.Log($"적군 배치: {placement.enemyType} at {placement.coord}");
                    }
                }
            }
        }

        /// <summary>
        /// StageManager에 스테이지 정보 로드
        /// </summary>
        private void LoadStageToManager(StageData stageData)
        {
            if (stageManager == null)
                return;

            // StageManager의 기존 LoadStage 대신 미션 데이터만 설정
            // (구현 세부사항은 StageManager.cs에서 처리)

            Debug.Log($"[StageManager] Stage {stageData.stageNumber} 정보 로드");
            Debug.Log($"  - 턴 제한: {stageData.turnLimit}");
            Debug.Log($"  - 난이도: {stageData.difficulty}");
            Debug.Log($"  - 미션 개수: {stageData.missions.Length}");
            Debug.Log($"  - 적군 개수: {stageData.enemyPlacements.Length}");
            Debug.Log($"  - 보스 스테이지: {stageData.isBossStage}");

            foreach (var mission in stageData.missions)
            {
                Debug.Log($"  미션: {mission.description}");
            }
        }

        /// <summary>
        /// 튜토리얼 플래그 처리
        /// </summary>
        private void ProcessTutorialFlags(StageData stageData)
        {
            if (stageData.tutorialFlags == null || stageData.tutorialFlags.Length == 0)
                return;

            // 튜토리얼 팝업 표시 로직
            // 예: 색상도둑 설명, 드릴 사용법 등

            // 이 부분은 UIManager와 연계하여 구현
        }

        /// <summary>
        /// 스토리 컨텐츠 표시
        /// </summary>
        private void ShowStoryContent(StageData stageData)
        {
            if (stageData.storyData == null)
                return;

            // 스테이지 시작 전 컷씬/대사 표시
            if (!string.IsNullOrEmpty(stageData.storyData.beforeStageCutscene))
            {
                Debug.Log($"[스토리] {stageData.storyData.beforeStageCutscene}");
                // UIManager.ShowCutscene(stageData.storyData.beforeStageCutscene);
            }

            // 게임 시작 시 오라클리온 대사
            if (stageData.storyData.stageIntroDialogues != null && stageData.storyData.stageIntroDialogues.Length > 0)
            {
                Debug.Log($"[게임 시작 대사]");
                foreach (var dialogue in stageData.storyData.stageIntroDialogues)
                {
                    Debug.Log($"  {dialogue}");
                }
                // UIManager.ShowDialogues(stageData.storyData.stageIntroDialogues);
            }
        }
    }

    /// <summary>
    /// 게임 시작 시 미션 1을 로드하는 헬퍼 메서드
    /// GameManager 또는 메인 씬에서 호출
    /// </summary>
    public static class Mission1Helper
    {
        /// <summary>
        /// 미션 1 Stage 1-10 중 특정 스테이지 시작
        /// </summary>
        public static void StartMission1Stage(int stageNumber, HexGrid hexGrid, StageManager stageManager)
        {
            if (stageNumber < 1 || stageNumber > 10)
            {
                Debug.LogError($"미션 1은 Stage 1-10만 지원합니다. 요청: {stageNumber}");
                return;
            }

            var loader = new GameObject("Mission1Loader").AddComponent<Mission1StageLoader>();
            loader.LoadStage(stageNumber, hexGrid, stageManager);
        }

        /// <summary>
        /// 미션 1 모든 스테이지 정보 출력 (디버그용)
        /// </summary>
        public static void PrintAllMission1StageInfo()
        {
            Debug.Log("=== 미션 1 스테이지 정보 ===");
            var stages = Mission1StageData.GetAllMission1Stages();

            foreach (var kvp in stages)
            {
                var stageData = kvp.Value;
                Debug.Log($"\nStage {stageData.stageNumber}: {stageData.chapterName}");
                Debug.Log($"  난이도: {stageData.difficulty} | 턴: {stageData.turnLimit}");
                Debug.Log($"  적군: {stageData.enemyPlacements.Length}개 | 장애물: {stageData.fixedBlockPlacements.Length}개");

                foreach (var mission in stageData.missions)
                {
                    Debug.Log($"  미션: {mission.description}");
                }
            }
        }

        /// <summary>
        /// 특정 스테이지의 적군 배치 정보 출력 (디버그용)
        /// </summary>
        public static void PrintStageEnemyLayout(int stageNumber)
        {
            var stages = Mission1StageData.GetAllMission1Stages();
            if (!stages.TryGetValue(stageNumber, out var stageData))
            {
                Debug.LogError($"Stage {stageNumber} 찾을 수 없음");
                return;
            }

            Debug.Log($"\n=== Stage {stageNumber} 적군 배치 ===");
            foreach (var enemy in stageData.enemyPlacements)
            {
                Debug.Log($"  {EnemyTypeHelper.GetName(enemy.enemyType)} at ({enemy.coord.q}, {enemy.coord.r})");
            }

            Debug.Log($"\n=== Stage {stageNumber} 고정 블록 배치 ===");
            foreach (var block in stageData.fixedBlockPlacements)
            {
                Debug.Log($"  고정 블록 at ({block.coord.q}, {block.coord.r})");
            }
        }
    }
}

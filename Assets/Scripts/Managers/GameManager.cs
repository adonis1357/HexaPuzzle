using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 게임 전체 흐름 관리
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Core Systems")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private RotationSystem rotationSystem;
        [SerializeField] private MatchingSystem matchingSystem;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;
        [SerializeField] private InputSystem inputSystem;
        [SerializeField] private DrillBlockSystem drillSystem;


        [Header("Managers")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private ItemManager itemManager;

        [Header("Game Settings")]
        [SerializeField] private int initialTurns = 30;

        // 게임 상태
        private GameState currentState = GameState.Loading;
        private int currentTurns;
        private int currentStage = 1;
        private bool isPaused = false;

        // 프로퍼티
        public GameState CurrentState => currentState;
        public int CurrentTurns => currentTurns;
        public int CurrentStage => currentStage;
        public bool IsPaused => isPaused;

        // 이벤트
        public event System.Action<GameState> OnGameStateChanged;
        public event System.Action<int> OnTurnChanged;
        public event System.Action OnGameOver;
        public event System.Action OnStageClear;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            AutoFindReferences();
            InitializeSystems();
        }

        private void Start()
        {
            StartGame();
        }

        /// <summary>
        /// 참조가 없으면 자동으로 찾기
        /// </summary>
        private void AutoFindReferences()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[GameManager] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[GameManager] HexGrid not found!");
            }

            if (rotationSystem == null)
            {
                rotationSystem = FindObjectOfType<RotationSystem>();
                if (rotationSystem != null)
                    Debug.Log("[GameManager] RotationSystem auto-found");
            }

            if (matchingSystem == null)
            {
                matchingSystem = FindObjectOfType<MatchingSystem>();
                if (matchingSystem != null)
                    Debug.Log("[GameManager] MatchingSystem auto-found");
            }

            if (blockRemovalSystem == null)
            {
                blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
                if (blockRemovalSystem != null)
                    Debug.Log("[GameManager] BlockRemovalSystem auto-found");
            }

            if (drillSystem == null)
            {
                drillSystem = FindObjectOfType<DrillBlockSystem>();
                if (drillSystem != null)
                    Debug.Log("[GameManager] DrillBlockSystem auto-found");
            }

            
if (inputSystem == null)
            {
                inputSystem = FindObjectOfType<InputSystem>();
                if (inputSystem != null)
                    Debug.Log("[GameManager] InputSystem auto-found");
            }

            if (uiManager == null)
                uiManager = FindObjectOfType<UIManager>();

            if (scoreManager == null)
                scoreManager = FindObjectOfType<ScoreManager>();

            if (stageManager == null)
                stageManager = FindObjectOfType<StageManager>();

            if (itemManager == null)
                itemManager = FindObjectOfType<ItemManager>();
        }

        /// <summary>
        /// 시스템 초기화
        /// </summary>
        private void InitializeSystems()
        {
            // 이벤트 연결
            if (rotationSystem != null)
            {
                rotationSystem.OnRotationComplete += OnRotationComplete;
                rotationSystem.OnRotationStarted += OnRotationStarted;
            }

            if (matchingSystem != null)
            {
                matchingSystem.OnMatchFound += OnMatchFound;
            }

            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.OnBlocksRemoved += OnBlocksRemoved;
                blockRemovalSystem.OnCascadeComplete += OnCascadeComplete;
                blockRemovalSystem.OnBigBang += OnBigBang;

            if (drillSystem != null)
            {
                drillSystem.OnDrillComplete += OnDrillCompleted;
            }

            }
        }

        /// <summary>
        /// 게임 시작
        /// </summary>
        public void StartGame()
        {
            StartCoroutine(StartGameCoroutine());
        }

        private IEnumerator StartGameCoroutine()
        {
            SetGameState(GameState.Loading);

            // 그리드 초기화
            if (hexGrid != null)
            {
                hexGrid.InitializeGrid();
                yield return new WaitForSeconds(0.5f);

                hexGrid.PopulateWithRandomGems();
            }

            // 턴 설정
            currentTurns = initialTurns;

            // UI 업데이트
            UpdateUI();

            yield return new WaitForSeconds(0.5f);

            // 초기 매칭 제거 (최대 10회만 시도)
            yield return StartCoroutine(RemoveInitialMatches());

            SetGameState(GameState.Playing);
            Debug.Log("Game Started! State: Playing");
        }

        /// <summary>
        /// 초기 매칭 제거 (게임 시작 시) - 최대 10회 제한
        /// </summary>
        private IEnumerator RemoveInitialMatches()
        {
            if (matchingSystem == null || blockRemovalSystem == null)
            {
                Debug.LogWarning("MatchingSystem or BlockRemovalSystem is null");
                yield break;
            }

            int maxIterations = 10;
            int iteration = 0;

            var matches = matchingSystem.FindMatches();

            while (matches != null && matches.Count > 0 && iteration < maxIterations)
            {
                iteration++;
                Debug.Log($"Initial match removal iteration {iteration}, found {matches.Count} matches");

                // 매칭된 블록들을 새 블록으로 교체
                foreach (var match in matches)
                {
                    foreach (var block in match.blocks)
                    {
                        if (block != null && block.Data != null)
                        {
                            GemType newGem = (GemType)Random.Range(1, 6);
                            block.SetBlockData(new BlockData(newGem));
                        }
                    }
                }

                yield return new WaitForSeconds(0.1f);

                matches = matchingSystem.FindMatches();
            }

            if (iteration >= maxIterations)
            {
                Debug.Log("Max iterations reached for initial match removal");
            }
        }

        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        private void SetGameState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;

            // 입력 시스템 제어
            if (inputSystem != null)
            {
                inputSystem.SetEnabled(newState == GameState.Playing);
            }

            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"Game State: {newState}");
        }

        /// <summary>
        /// 회전 시작 이벤트
        /// </summary>
        private void OnRotationStarted()
        {
            SetGameState(GameState.Processing);
        }

        /// <summary>
        /// 회전 완료 이벤트
        /// </summary>
        private void OnRotationComplete(bool matchFound)
        {
            if (matchFound)
            {
                // 턴 소모
                UseTurn();

                // 매칭 처리
                if (matchingSystem != null && blockRemovalSystem != null)
                {
                    var matches = matchingSystem.FindMatches();
                    if (matches.Count > 0)
                    {
                        blockRemovalSystem.ProcessMatches(matches);
                    }
                    else
                    {
                        SetGameState(GameState.Playing);
                    }
                }
            }
            else
            {
                // 매칭 실패 - 턴 소모 없음
                SetGameState(GameState.Playing);
            }
        }

        /// <summary>
        /// 매칭 발견 이벤트
        /// </summary>
        private void OnMatchFound(System.Collections.Generic.List<MatchingSystem.MatchGroup> matches)
        {
            Debug.Log($"Matches found: {matches.Count} groups");
        }

        /// <summary>
        /// 블록 제거 완료 이벤트
        /// </summary>
        private void OnBlocksRemoved(int score)
        {
            if (scoreManager != null)
            {
                scoreManager.AddScore(score);
            }

            // 미션 체크
            if (stageManager != null)
            {
                stageManager.CheckMissionProgress();
            }
        }

        /// <summary>
        /// 연쇄 완료 이벤트
        /// </summary>
        private void OnCascadeComplete()
        {
            // 시한폭탄 체크
            CheckTimeBombs();

            // 미션 완료 확인
            if (stageManager != null && stageManager.IsMissionComplete())
            {
                StageClear();
                return;
            }

            // 게임오버 확인
            if (currentTurns <= 0)
            {
                GameOver();
                return;
            }

            SetGameState(GameState.Playing);
        }

        /// <summary>
        /// 빅뱅 이벤트
        /// </summary>
        /// <summary>
        /// 드릴 완료 이벤트 - 낙하 처리 트리거
        /// </summary>
        private void OnDrillCompleted(int score)
        {
            Debug.Log($"Drill completed! Score: {score}");
            if (scoreManager != null)
                scoreManager.AddScore(score);

            // 드릴로 파괴된 블록들의 낙하 + 연쇄 매칭 처리
            if (blockRemovalSystem != null)
            {
                // ProcessMatches에서 빈 매칭으로 낙하만 트리거
                StartCoroutine(ProcessDrillAftermath());
            }
        }

private IEnumerator ProcessDrillAftermath()
        {
            yield return new WaitForSeconds(0.1f);

            // 납하 처리
            blockRemovalSystem.TriggerFallOnly();
            while (blockRemovalSystem.IsProcessing)
                yield return null;

            // 드릴이 발견한 특수블록들 순차적으로 발동
            if (drillSystem != null && drillSystem.PendingSpecialBlocks.Count > 0)
            {
                List<HexBlock> pending = new List<HexBlock>(drillSystem.PendingSpecialBlocks);
                drillSystem.PendingSpecialBlocks.Clear();

                foreach (var specialBlock in pending)
                {
                    if (specialBlock == null || specialBlock.Data == null) continue;
                    if (specialBlock.Data.specialType == SpecialBlockType.None) continue;

                    Debug.Log($"[GameManager] Activating pending special block: {specialBlock.Coord} type={specialBlock.Data.specialType}");

                    switch (specialBlock.Data.specialType)
                    {
                        case SpecialBlockType.Drill:
                            drillSystem.ActivateDrill(specialBlock);
                            yield return new WaitForSeconds(0.1f);
                            while (drillSystem.IsDrilling)
                                yield return null;
                            // 이 드릴이 또 특수블록을 발견했을 수 있으므로 재귀적 처리
                            yield return StartCoroutine(ProcessDrillAftermath());
                            yield break;
                    }
                }
            }

            // 연쇄 매칭 확인
            if (matchingSystem != null)
            {
                var matches = matchingSystem.FindMatches();
                if (matches.Count > 0)
                {
                    blockRemovalSystem.ProcessMatches(matches);
                    yield break;
                }
            }
        }

        
private void OnBigBang()
        {
            Debug.Log("BIG BANG triggered!");
        }

        /// <summary>
        /// 턴 사용
        /// </summary>
        private void UseTurn()
        {
            currentTurns--;
            OnTurnChanged?.Invoke(currentTurns);
            UpdateUI();

            Debug.Log($"Turn used. Remaining: {currentTurns}");
        }

        /// <summary>
        /// 시한폭탄 체크
        /// </summary>
        private void CheckTimeBombs()
        {
            if (hexGrid == null) return;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null &&
                    block.Data.specialType == SpecialBlockType.TimeBomb)
                {
                    if (block.DecrementTimeBomb())
                    {
                        GameOver();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (uiManager != null)
            {
                uiManager.UpdateTurnDisplay(currentTurns);
                uiManager.UpdateStageDisplay(currentStage);

                if (scoreManager != null)
                {
                    uiManager.UpdateScoreDisplay(scoreManager.CurrentScore);
                }
            }
        }

        /// <summary>
        /// 스테이지 클리어
        /// </summary>
        private void StageClear()
        {
            SetGameState(GameState.StageClear);
            OnStageClear?.Invoke();

            Debug.Log($"Stage {currentStage} Clear!");

            if (uiManager != null)
            {
                uiManager.ShowStageClearPopup();
            }
        }

        /// <summary>
        /// 게임 오버
        /// </summary>
        private void GameOver()
        {
            SetGameState(GameState.GameOver);
            OnGameOver?.Invoke();

            Debug.Log("Game Over!");

            if (uiManager != null)
            {
                uiManager.ShowGameOverPopup();
            }
        }

        /// <summary>
        /// 다음 스테이지로
        /// </summary>
        public void NextStage()
        {
            currentStage++;
            StartGame();
        }

        /// <summary>
        /// 스테이지 재시작
        /// </summary>
        public void RetryStage()
        {
            if (scoreManager != null)
            {
                scoreManager.ResetScore();
            }
            StartGame();
        }

        /// <summary>
        /// 일시정지
        /// </summary>
        public void PauseGame()
        {
            if (currentState != GameState.Playing) return;

            isPaused = true;
            Time.timeScale = 0f;

            if (inputSystem != null)
            {
                inputSystem.SetEnabled(false);
            }

            if (uiManager != null)
            {
                uiManager.ShowPausePopup();
            }
        }

        /// <summary>
        /// 재개
        /// </summary>
        public void ResumeGame()
        {
            isPaused = false;
            Time.timeScale = 1f;

            if (inputSystem != null && currentState == GameState.Playing)
            {
                inputSystem.SetEnabled(true);
            }

            if (uiManager != null)
            {
                uiManager.HidePausePopup();
            }
        }

        /// <summary>
        /// 턴 추가 (아이템 또는 구매)
        /// </summary>
        public void AddTurns(int amount)
        {
            currentTurns += amount;
            OnTurnChanged?.Invoke(currentTurns);
            UpdateUI();

            if (currentState == GameState.GameOver && currentTurns > 0)
            {
                SetGameState(GameState.Playing);

                if (uiManager != null)
                {
                    uiManager.HideGameOverPopup();
                }
            }
        }

        /// <summary>
        /// 회전 방향 토글
        /// </summary>
        public void ToggleRotationDirection()
        {
            if (rotationSystem != null)
            {
                rotationSystem.ToggleRotationDirection();
            }
        }

        /// <summary>
        /// 로비로 나가기
        /// </summary>
        public void ExitToLobby()
        {
            Time.timeScale = 1f;
            Debug.Log("Exit to Lobby");
        }

        private void OnDestroy()
        {
            if (rotationSystem != null)
            {
                rotationSystem.OnRotationComplete -= OnRotationComplete;
                rotationSystem.OnRotationStarted -= OnRotationStarted;
            }

            if (matchingSystem != null)
            {
                matchingSystem.OnMatchFound -= OnMatchFound;
            }

            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.OnBlocksRemoved -= OnBlocksRemoved;
                blockRemovalSystem.OnCascadeComplete -= OnCascadeComplete;
                blockRemovalSystem.OnBigBang -= OnBigBang;

            if (drillSystem != null)
            {
                drillSystem.OnDrillComplete -= OnDrillCompleted;
            }

            }
        }
    }

    /// <summary>
    /// 게임 상태 열거형
    /// </summary>
    public enum GameState
    {
        Loading,
        Playing,
        Processing,
        Paused,
        StageClear,
        GameOver
    }
}
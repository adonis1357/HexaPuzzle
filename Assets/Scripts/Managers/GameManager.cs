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
                [SerializeField] private BombBlockSystem bombSystem;
        [SerializeField] private DonutBlockSystem donutSystem;
        [SerializeField] private XBlockSystem xBlockSystem;
        [SerializeField] private LaserBlockSystem laserSystem;




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

        private bool isProcessingChainDrill = false;
        private bool isInPostRecovery = false;
        private bool isPaused = false;

        // Stuck 상태 감지 워치독
        private float processingStartTime = 0f;
        private const float STUCK_TIMEOUT = 8f; // 8초 이상 Processing 상태면 복구태면 복구


        // 프로퍼티
        public GameState CurrentState => currentState;
        public int CurrentTurns => currentTurns;
        public int CurrentStage => currentStage;
        public int InitialTurns => initialTurns;
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
        /// 워치독: Processing 상태가 너무 오래 지속되면 강제 복구
        /// </summary>
private void Update()
        {
            if (currentState == GameState.Processing && !isPaused)
            {
                float elapsed = Time.time - processingStartTime;

                if (isInPostRecovery)
                {
                    // PostRecovery 자체가 15초 이상 걸리면 강제 복구
                    if (elapsed > 15f)
                    {
                        Debug.LogError($"[GameManager] PostRecovery timeout after {elapsed:F1}s! Force resetting to Playing.");
                        StopAllCoroutines();
                        isInPostRecovery = false;
                        isProcessingChainDrill = false;
                        if (blockRemovalSystem != null) blockRemovalSystem.ForceReset();
                        if (drillSystem != null) drillSystem.ForceReset();
                        if (bombSystem != null) bombSystem.ForceReset();
                        if (donutSystem != null) donutSystem.ForceReset();
                        if (xBlockSystem != null) xBlockSystem.ForceReset();
                        if (laserSystem != null) laserSystem.ForceReset();
                        SetGameState(GameState.Playing);
                        if (inputSystem != null) inputSystem.SetEnabled(true);
                    }
                }
                else
                {
                    if (elapsed > STUCK_TIMEOUT)
                    {
                        Debug.LogWarning($"[GameManager] STUCK DETECTED! Processing for {elapsed:F1}s. Force recovering...");
                        Debug.LogWarning($"[GameManager] Flags: isProcessingChainDrill={isProcessingChainDrill}, BRS.IsProcessing={blockRemovalSystem?.IsProcessing}");
                        ForceRecoverFromStuck();
                    }
                }
            }
        }

        /// <summary>
        /// Stuck 상태에서 강제 복구
        /// </summary>
private void ForceRecoverFromStuck()
        {
            Debug.LogWarning($"[GameManager] Flags before reset: isProcessingChainDrill={isProcessingChainDrill}, BRS.IsProcessing={blockRemovalSystem?.IsProcessing}, Rotating={rotationSystem?.IsRotating}");

            // 모든 코루틴 중지 (GameManager 코루틴만)
            StopAllCoroutines();

            // 모든 플래그 리셋
            isProcessingChainDrill = false;
            isInPostRecovery = false;

            // RotationSystem 리셋
            if (rotationSystem != null)
                rotationSystem.ForceReset();

            // BlockRemovalSystem 리셋 (내부 코루틴도 중지됨)
            if (blockRemovalSystem != null)
                blockRemovalSystem.ForceReset();

            // 모든 특수 블록 시스템 리셋
            if (drillSystem != null) drillSystem.ForceReset();
            if (bombSystem != null) bombSystem.ForceReset();
            if (donutSystem != null) donutSystem.ForceReset();
            if (xBlockSystem != null) xBlockSystem.ForceReset();
            if (laserSystem != null) laserSystem.ForceReset();

            // pending 플래그 전체 클리어 + matched 상태 해제
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block != null && block.Data != null)
                    {
                        block.Data.pendingActivation = false;
                        block.SetMatched(false);
                    }
                }
            }

            // 타이머 리셋
            processingStartTime = Time.time;

            Debug.LogWarning("[GameManager] Force recovered - starting post-recovery cleanup");

            // 복구 후 보드 정리 (낙하 + 매칭 처리)
            StartCoroutine(PostRecoveryCleanup());
        }

/// <summary>
        /// 강제 복구 후 보드 정리: 낙하 처리 + 미처리 매칭 해결
        /// </summary>
private IEnumerator PostRecoveryCleanup()
        {
            isInPostRecovery = true;
            SetGameState(GameState.Processing);
            processingStartTime = Time.time;

            yield return new WaitForSeconds(0.3f);

            // 1. 모든 블록 위치 복원 (slotPositions 기반)
            // BRS의 ProcessFalling이 정상적으로 처리하도록 낙하만 수행
            if (blockRemovalSystem != null)
            {
                yield return StartCoroutine(blockRemovalSystem.ProcessFallingCoroutinePublic());
            }
            yield return new WaitForSeconds(0.2f);

            // 2. 매칭 확인 - 있으면 BRS에 위임 (풀 cascade)
            if (matchingSystem != null && blockRemovalSystem != null)
            {
                var matches = matchingSystem.FindMatches();
                if (matches != null && matches.Count > 0)
                {
                    Debug.Log($"[GameManager] PostRecovery: Found {matches.Count} matches, delegating to BRS...");
                    
                    // BRS가 준비될 때까지 대기
                    float brsWait = 0f;
                    while (blockRemovalSystem.IsProcessing && brsWait < 3f)
                    {
                        brsWait += Time.deltaTime;
                        yield return null;
                    }
                    if (blockRemovalSystem.IsProcessing)
                    {
                        Debug.LogError("[GameManager] PostRecovery: BRS still busy after 3s. Force resetting.");
                        blockRemovalSystem.ForceReset();
                        yield return new WaitForSeconds(0.1f);
                    }
                    
                    // BRS에 위임 - BRS가 cascade까지 모두 처리하고 OnCascadeComplete를 발사함
                    // OnCascadeComplete 핸들러에서 Playing 상태로 전환됨
                    isInPostRecovery = false;
                    isProcessingChainDrill = false;
                    blockRemovalSystem.ProcessMatches(matches);
                    // BRS cascade 완료 대기 (타임아웃 포함)
                    yield return StartCoroutine(WaitForBRSComplete("PostRecovery"));
                    
                    // BRS가 OnCascadeComplete를 이미 발사했으므로 여기서는 상태만 확인
                    if (currentState == GameState.Processing)
                    {
                        SetGameState(GameState.Playing);
                        if (inputSystem != null) inputSystem.SetEnabled(true);
                    }
                    Debug.Log("[GameManager] PostRecoveryCleanup completed (via BRS cascade)");
                    yield break;
                }
                else
                {
                    Debug.Log("[GameManager] PostRecovery: No matches found, board is clean.");
                }
            }

            // 3. Playing 상태로 복귀
            isInPostRecovery = false;
            isProcessingChainDrill = false;
            SetGameState(GameState.Playing);
            if (inputSystem != null)
                inputSystem.SetEnabled(true);
            Debug.Log("[GameManager] PostRecoveryCleanup completed -> Playing");
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

            if (bombSystem == null)
            {
                bombSystem = FindObjectOfType<BombBlockSystem>();
                if (bombSystem != null)
                    Debug.Log("[GameManager] BombBlockSystem auto-found");
            }

            if (donutSystem == null)
            {
                donutSystem = FindObjectOfType<DonutBlockSystem>();
                if (donutSystem != null)
                    Debug.Log("[GameManager] DonutBlockSystem auto-found");
            }

            if (xBlockSystem == null)
            {
                xBlockSystem = FindObjectOfType<XBlockSystem>();
                if (xBlockSystem != null)
                    Debug.Log("[GameManager] XBlockSystem auto-found");
            }

            if (laserSystem == null)
            {
                laserSystem = FindObjectOfType<LaserBlockSystem>();
                if (laserSystem != null)
                    Debug.Log("[GameManager] LaserBlockSystem auto-found");
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
            }

            if (drillSystem != null)
                drillSystem.OnDrillComplete += OnSpecialBlockCompleted;

            if (bombSystem != null)
                bombSystem.OnBombComplete += OnSpecialBlockCompleted;

            if (donutSystem != null)
                donutSystem.OnDonutComplete += OnSpecialBlockCompleted;


            if (xBlockSystem != null)
                xBlockSystem.OnXBlockComplete += OnSpecialBlockCompleted;

            if (laserSystem != null)
                laserSystem.OnLaserComplete += OnSpecialBlockCompleted;

            // UI 시스템 자동 초기화 (ScorePopupManager, ComboDisplay)
            EnsureUIComponents();
        }

        /// <summary>
        /// ScorePopupManager와 ComboDisplay가 없으면 자동 생성
        /// </summary>
        private void EnsureUIComponents()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // ScorePopupManager
            if (FindObjectOfType<JewelsHexaPuzzle.UI.ScorePopupManager>() == null)
            {
                GameObject popupMgr = new GameObject("ScorePopupManager");
                popupMgr.transform.SetParent(canvas.transform, false);
                popupMgr.AddComponent<JewelsHexaPuzzle.UI.ScorePopupManager>();
                Debug.Log("[GameManager] ScorePopupManager auto-created");
            }

            // ComboDisplay
            if (FindObjectOfType<JewelsHexaPuzzle.UI.ComboDisplay>() == null)
            {
                GameObject comboDisp = new GameObject("ComboDisplay");
                comboDisp.transform.SetParent(canvas.transform, false);
                comboDisp.AddComponent<JewelsHexaPuzzle.UI.ComboDisplay>();
                Debug.Log("[GameManager] ComboDisplay auto-created");
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

            // 스테이지 전환 시 슬롯 캐시 무효화
            if (blockRemovalSystem != null)
                blockRemovalSystem.InvalidateSlotCache();

            if (hexGrid != null)
            {
                hexGrid.InitializeGrid();
                yield return new WaitForSeconds(0.3f);

                // 매칭 없는 블록으로 배치
                hexGrid.PopulateWithNoMatches();

                if (blockRemovalSystem != null)
                {
                    blockRemovalSystem.TriggerStartDrop();
                    while (blockRemovalSystem.IsProcessing)
                        yield return null;
                }
            }

            currentTurns = initialTurns;
            if (uiManager != null)
                uiManager.SetMaxTurns(initialTurns);

            UpdateUI();

            // 게임 BGM 시작
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayGameBGM();

            SetGameState(GameState.Playing);
            Debug.Log("Game Started! State: Playing");
        }

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
                            GemType newGem = GemTypeHelper.GetRandom();
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

            // Processing 상태 진입 시 타이머 시작
            if (newState == GameState.Processing)
                processingStartTime = Time.time;


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
            Debug.Log($"[GameManager] OnRotationComplete: matchFound={matchFound}, state={currentState}, BRS.IsProcessing={blockRemovalSystem?.IsProcessing}");

            if (matchFound)
            {
                // 턴 소모
                UseTurn();

                // 매칭 처리
                if (matchingSystem != null && blockRemovalSystem != null)
                {
                    var matches = matchingSystem.FindMatches();
                    Debug.Log($"[GameManager] OnRotationComplete: FindMatches returned {matches.Count} groups");

                    if (matches.Count > 0)
                    {
                        if (blockRemovalSystem.IsProcessing)
                        {
                            Debug.LogWarning("[GameManager] BRS still processing! Force resetting before ProcessMatches.");
                            blockRemovalSystem.ForceReset();
                        }
                        blockRemovalSystem.ProcessMatches(matches);
                    }
                    else
                    {
                        Debug.LogWarning("[GameManager] Rotation found match but FindMatches returned 0! Reverting to Playing.");
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
        private void OnBlocksRemoved(int blockCount, int cascadeDepth, Vector3 avgPosition)
        {
            if (scoreManager != null)
            {
                scoreManager.AddMatchScore(blockCount, cascadeDepth, avgPosition);
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
            // ProcessSpecialBlockAftermath가 실행 중이면 상태 복원을 그쪽에서 처리
            if (isProcessingChainDrill)
            {
                Debug.Log("[GameManager] OnCascadeComplete skipped - ProcessSpecialBlockAftermath is managing state");
                return;
            }

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
/// <summary>
        /// 특수 블록(드릴/폭탄 등) 완료 통합 이벤트
        /// 새 특수 블록 추가 시 해당 시스템의 OnXxxComplete 이벤트를 이 메서드에 연결하면 됨
        /// </summary>
private void OnSpecialBlockCompleted(int score)
        {
            Debug.Log($"[GameManager] Special block completed! Score: {score}");
            if (scoreManager != null)
            {
                int cascadeDepth = blockRemovalSystem != null ? blockRemovalSystem.CurrentCascadeDepth : 0;
                scoreManager.AddSpecialBlockScore(score, cascadeDepth, Vector3.zero);
            }

            // 복구 중이면 무시 (이중 처리 방지)
            if (isInPostRecovery) return;

            if (isProcessingChainDrill) return;

            // BlockRemovalSystem 내부 cascade에서 특수블록이 발동된 경우
            // 이미 내부적으로 연쇄 처리 중이므로 GameManager가 개입하면 데드락 발생
            if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) return;

            // 유저 직접 클릭으로 발동된 경우: 낙하 + pending 연쇄 처리 트리거
            SetGameState(GameState.Processing);
            StartCoroutine(ProcessSpecialBlockAftermath());
        }

/// <summary>
        /// 특수 블록 발동 후 통합 후처리
        /// 1) 낙하 처리
        /// 2) pendingActivation 플래그가 있는 특수 블록 연쇄 발동
        /// 3) 연쇄 매칭 확인
        /// 새 특수 블록 추가 시 ActivateSpecialAndWait의 switch에 case만 추가하면 됨
        /// </summary>

        /// <summary>
        /// BRS가 준비될 때까지 대기 (타임아웃 포함)
        /// ProcessMatches/ProcessMatchesWithPendingSpecials 호출 전에 반드시 호출
        /// </summary>
        private IEnumerator WaitForBRSReady()
        {
            if (blockRemovalSystem == null || !blockRemovalSystem.IsProcessing) yield break;
            Debug.LogWarning("[GameManager] WaitForBRSReady: BRS is processing, waiting...");
            float waited = 0f;
            while (blockRemovalSystem.IsProcessing && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (blockRemovalSystem.IsProcessing)
            {
                Debug.LogError("[GameManager] WaitForBRSReady timeout! Force resetting.");
                blockRemovalSystem.ForceReset();
            }
        }

        /// <summary>
        /// BRS 처리 완료 대기 (타임아웃 + 미시작 감지)
        /// ProcessMatches 호출 후에 사용
        /// </summary>
        private IEnumerator WaitForBRSComplete(string callerName)
        {
            if (blockRemovalSystem == null) yield break;

            // ProcessMatches가 guard에 의해 무시된 경우 감지:
            // 코루틴 시작을 위해 1프레임 대기
            yield return null;

            if (!blockRemovalSystem.IsProcessing)
            {
                // ProcessMatches가 실제로 시작되지 않았음 (guard에 의해 무시됨)
                Debug.LogWarning($"[GameManager] {callerName} did not start (BRS guard rejected). Skipping wait.");
                yield break;
            }

            float elapsed = 0f;
            while (blockRemovalSystem.IsProcessing && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (blockRemovalSystem.IsProcessing)
            {
                Debug.LogError($"[GameManager] {callerName} timeout after {elapsed:F1}s! Force resetting.");
                blockRemovalSystem.ForceReset();
            }
        }

private IEnumerator ProcessSpecialBlockAftermath()
        {
            yield return new WaitForSeconds(0.1f);
            isProcessingChainDrill = true;

            int maxLoops = 20;
            int loop = 0;

            while (loop < maxLoops)
            {
                loop++;

                // 1. 모든 특수 블록 시스템의 pending 목록 클리어
                if (drillSystem != null) drillSystem.PendingSpecialBlocks.Clear();
                if (bombSystem != null) bombSystem.PendingSpecialBlocks.Clear();
                if (donutSystem != null) donutSystem.PendingSpecialBlocks.Clear();
                if (xBlockSystem != null) xBlockSystem.PendingSpecialBlocks.Clear();
                if (laserSystem != null) laserSystem.PendingSpecialBlocks.Clear();

                // 2. 낙하 전: pendingActivation 블록의 블링크만 중지
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block != null && block.Data != null && block.Data.pendingActivation)
                            block.StopWarningBlink();
                    }
                }

                // 3. Safety: BRS가 아직 처리 중이면 완료 대기
                if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing)
                {
                    Debug.LogWarning("[GameManager] BRS still processing before falling. Waiting...");
                    float waited = 0f;
                    while (blockRemovalSystem.IsProcessing && waited < 5f)
                    {
                        waited += Time.deltaTime;
                        yield return null;
                    }
                    if (blockRemovalSystem.IsProcessing)
                    {
                        Debug.LogError("[GameManager] BRS timeout before falling! Force resetting.");
                        blockRemovalSystem.ForceReset();
                    }
                }

                // 4. 낙하 처리
                if (blockRemovalSystem != null)
                {
                    yield return StartCoroutine(blockRemovalSystem.ProcessFallingCoroutinePublic());
                }
                yield return new WaitForSeconds(0.05f);

                // 5. 낙하 후 pending 블록 재수집
                List<HexBlock> pendingBlocks = new List<HexBlock>();
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block != null && block.Data != null &&
                            block.Data.pendingActivation &&
                            block.Data.specialType != SpecialBlockType.None)
                        {
                            block.Data.pendingActivation = false;
                            pendingBlocks.Add(block);
                        }
                    }
                }

                Debug.Log($"[GameManager] Aftermath loop #{loop}: {pendingBlocks.Count} pending specials found");

                // 6. 매칭 확인
                List<MatchingSystem.MatchGroup> newMatches = null;
                if (matchingSystem != null)
                {
                    var matches = matchingSystem.FindMatches();
                    if (matches.Count > 0) newMatches = matches;
                }

                // 7. 아무것도 없으면 종료
                if (pendingBlocks.Count == 0 && newMatches == null)
                {
                    Debug.Log($"[GameManager] Aftermath loop ended at #{loop} (nothing to process)");
                    break;
                }

                // 8. 매칭이 있으면 BRS에 위임 (BRS 내부에서 cascade 처리)
                if (newMatches != null)
                {
                    yield return StartCoroutine(WaitForBRSReady());

                    if (pendingBlocks.Count > 0)
                    {
                        Debug.Log($"[GameManager] Aftermath: {pendingBlocks.Count} pending + {newMatches.Count} matches -> BRS");
                        blockRemovalSystem.ProcessMatchesWithPendingSpecials(newMatches, pendingBlocks);
                    }
                    else
                    {
                        Debug.Log($"[GameManager] Aftermath: {newMatches.Count} matches -> BRS");
                        blockRemovalSystem.ProcessMatches(newMatches);
                    }
                    yield return StartCoroutine(WaitForBRSComplete("Aftermath-BRS"));
                    // BRS cascade가 모든 연쇄를 처리하므로 루프 종료
                    break;
                }

                // 9. pending만 있으면 독립 발동 후 루프 반복
                if (pendingBlocks.Count > 0)
                {
                    Debug.Log($"[GameManager] Aftermath: activating {pendingBlocks.Count} pending specials");
                    List<Coroutine> activationCoroutines = new List<Coroutine>();
                    foreach (var specialBlock in pendingBlocks)
                    {
                        if (specialBlock == null || specialBlock.Data == null) continue;
                        if (specialBlock.Data.specialType == SpecialBlockType.None) continue;
                        activationCoroutines.Add(StartCoroutine(ActivateSpecialAndWait(specialBlock)));
                    }
                    foreach (var co in activationCoroutines)
                        yield return co;
                    continue;
                }
            }

            if (loop >= maxLoops)
                Debug.LogError($"[GameManager] ProcessSpecialBlockAftermath hit max loops ({maxLoops})!");

            // === 항상 도달하는 최종 상태 복원 ===
            isProcessingChainDrill = false;

            CheckTimeBombs();

            if (stageManager != null && stageManager.IsMissionComplete())
            {
                StageClear();
                yield break;
            }

            if (currentTurns <= 0)
            {
                GameOver();
                yield break;
            }

            SetGameState(GameState.Playing);
            Debug.Log("[GameManager] ProcessSpecialBlockAftermath completed -> Playing");
        }

/// <summary>
        /// 낙하 처리 후 콜백 호출 - 특수 블록과 동시 실행용
        /// </summary>
// FallAndSignal은 더 이상 사용하지 않음 - 낙하 완료 후 pending 블록 처리로 변경됨
        private IEnumerator FallAndSignal(System.Action onComplete)
        {
            if (blockRemovalSystem != null)
            {
                yield return StartCoroutine(blockRemovalSystem.ProcessFallingCoroutinePublic());
            }
            onComplete?.Invoke();
        }


// ActivateDrillAndWait/ActivateBombAndWait → ActivateSpecialAndWait로 통합됨

// OnDrillCompleted/OnBombCompleted → OnSpecialBlockCompleted로 통합됨

// ProcessDrillAftermath/ProcessBombAftermath → ProcessSpecialBlockAftermath로 통합됨

/// <summary>
        /// 특수 블록 발동 + 완료 대기 (통합)
        /// 새 특수 블록 추가 시 case만 추가
        /// </summary>
private IEnumerator ActivateSpecialAndWait(HexBlock block)
        {
            if (block == null || block.Data == null) yield break;
            isProcessingChainDrill = true;
            float timeout = 5f;
            float waited = 0f;

            switch (block.Data.specialType)
            {
                case SpecialBlockType.Drill:
                    if (drillSystem != null)
                    {
                        drillSystem.ActivateDrill(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (drillSystem.IsDrilling && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (drillSystem.IsDrilling) { Debug.LogError("[GM] Drill timeout!"); drillSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Bomb:
                    if (bombSystem != null)
                    {
                        bombSystem.ActivateBomb(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (bombSystem.IsBombing && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (bombSystem.IsBombing) { Debug.LogError("[GM] Bomb timeout!"); bombSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Rainbow:
                    if (donutSystem != null)
                    {
                        donutSystem.ActivateDonut(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (donutSystem.IsActivating && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (donutSystem.IsActivating) { Debug.LogError("[GM] Donut timeout!"); donutSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.XBlock:
                    if (xBlockSystem != null)
                    {
                        xBlockSystem.ActivateXBlock(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (xBlockSystem.IsActivating && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (xBlockSystem.IsActivating) { Debug.LogError("[GM] XBlock timeout!"); xBlockSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Laser:
                    if (laserSystem != null)
                    {
                        laserSystem.ActivateLaser(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (laserSystem.IsActivating && waited < timeout) { waited += Time.deltaTime; yield return null; }
                        if (laserSystem.IsActivating) { Debug.LogError("[GM] Laser timeout!"); laserSystem.ForceReset(); }
                    }
                    break;
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

            // 턴 부족 경고 사운드 (3턴 이하)
            if (currentTurns <= 3 && currentTurns > 0 && AudioManager.Instance != null)
                AudioManager.Instance.PlayWarningBeep();

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

            // BGM 정지 + 스테이지 클리어 사운드
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
                AudioManager.Instance.PlayStageClear();
            }

            Debug.Log($"Stage {currentStage} Clear!");

            if (uiManager != null)
            {
                if (scoreManager != null)
                {
                    var summary = scoreManager.CalculateStageClearBonus(currentTurns, initialTurns);
                    uiManager.ShowStageClearPopup(summary);
                }
                else
                {
                    uiManager.ShowStageClearPopup();
                }
            }
        }

        /// <summary>
        /// 게임 오버
        /// </summary>
        private void GameOver()
        {
            SetGameState(GameState.GameOver);
            OnGameOver?.Invoke();

            // BGM 정지 + 게임 오버 사운드
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
                AudioManager.Instance.PlayGameOver();
            }

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
                matchingSystem.OnMatchFound -= OnMatchFound;

            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.OnBlocksRemoved -= OnBlocksRemoved;
                blockRemovalSystem.OnCascadeComplete -= OnCascadeComplete;
                blockRemovalSystem.OnBigBang -= OnBigBang;
            }

            if (drillSystem != null)
                drillSystem.OnDrillComplete -= OnSpecialBlockCompleted;

            if (bombSystem != null)
                bombSystem.OnBombComplete -= OnSpecialBlockCompleted;

            if (donutSystem != null)
                donutSystem.OnDonutComplete -= OnSpecialBlockCompleted;

            if (xBlockSystem != null)
                xBlockSystem.OnXBlockComplete -= OnSpecialBlockCompleted;

            if (laserSystem != null)
                laserSystem.OnLaserComplete -= OnSpecialBlockCompleted;
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
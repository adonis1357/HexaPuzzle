using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Items;
using JewelsHexaPuzzle.UI;
using JewelsHexaPuzzle.Utils;

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

        [SerializeField] private DroneBlockSystem droneSystem;
        [SerializeField] private EnemySystem enemySystem;
        private GoblinSystem goblinSystem;
        private MPGaugeUI mpGaugeUI;
        private ShellDangerOverlay shellDangerOverlay;
        private SkillTreeUI skillTreeUI;


        [Header("Managers")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private StageManager stageManager;
        /// <summary>외부 시스템(GoblinSystem 등)에서 미션 진행도 조회용</summary>
        public StageManager StageManagerRef => stageManager;
        [SerializeField] private ItemManager itemManager;
        [SerializeField] private MissionSystem missionSystem;

        [Header("Game Settings")]
        [SerializeField] private int initialTurns = 30;

        // 게임 상태
        private GameState currentState = GameState.Loading;
        private JewelsHexaPuzzle.Utils.ObscuredInt currentTurns; // ★ 보안: 메모리 치트 내성
        private int currentStage = 1;
        private JewelsHexaPuzzle.Utils.ObscuredInt currentGold = 0; // ★ 보안: 메모리 치트 내성 (GameGuardian 방어)

        // 무한 모드
        private GameMode currentGameMode = GameMode.Infinite;
        private int rotationCount = 0;
        public GameMode CurrentGameMode => currentGameMode;
        public int RotationCount => rotationCount;

        private bool isProcessingChainDrill = false;
        private bool secondWaveTriggered = false; // 고블린 2차 웨이브 소환 완료 여부
        private float lastAftermathProgressTime = 0f;  // ProcessSpecialBlockAftermath 진행 추적 타임스탬프
        private bool isInPostRecovery = false;
        private bool isPaused = false;
        private bool isItemAction = false;

        public bool IsItemAction { get => isItemAction; set => isItemAction = value; }

        // 게임오버(레벨 실패) 원인 — 실패 화면 버튼 분기용 (이동소진=이동횟수추가 / 데드락·필드고착=지원폭격)
        public enum GameOverReason { MovesExhausted, MatchingDeadlock, FieldShelled }
        private GameOverReason lastGameOverReason = GameOverReason.MovesExhausted;
        // 지원폭격: 매칭 불가(데드락) 실패 시 100골드로 정중앙에 폭격(연쇄 3단계, 리워드 무시) 후 게임 속행
        private const int SUPPORT_BOMBING_COST = 100;
        private bool isSupportBombing = false;

        // Stuck 상태 감지 워치독
        private float processingStartTime = 0f;
        // ★ 하드 캡용 (감사 H7) — Processing 진입 시점 고정(unscaled). systemsActive 리셋의 영향을 받지 않음.
        private float hardProcessingStartTime = 0f;
        private const float STUCK_TIMEOUT = 8f; // 8초 이상 Processing 상태면 복구태면 복구

        // 미션 진행도 UI 추적 (이벤트 핸들러용)
        private int[] lastDisplayedCounts;
        private Coroutine[] stageMissionCountDownCos;    // 레벨 미션별 카운트다운 코루틴

        // 대기 미션 인디케이터 (화면 좌상단)
        private GameObject pendingIndicatorObj;
        private Text pendingIndicatorCountText;
        private Image pendingIndicatorIcon;

        // 무한도전 미션 순차 감소 추적
        private int infiniteMissionDisplayed = -1;      // 현재 화면에 표시된 remaining 값
        private int infiniteMissionTarget = -1;          // 목표 remaining 값
        private bool infiniteMissionComplete = false;
        private Coroutine infiniteMissionCountDownCo = null;


        // 프로퍼티
        public GameState CurrentState => currentState;
        public int CurrentTurns => currentTurns;
        /// <summary>미션 완료(StageClear 진입) 순간의 남은 이동횟수 — 드릴 변환 소진 전 값.
        /// 학습형 자동플레이 fitness의 "잔여이동" 측정에 사용(팝업 시점 CurrentTurns=0과 구분).</summary>
        public int TurnsAtStageClear { get; private set; }
        public int CurrentStage => currentStage;
        public int InitialTurns => initialTurns;
        public bool IsPaused => isPaused;
        public int CurrentGold => currentGold;
        public bool IsProcessingChainDrill => isProcessingChainDrill;
        public SkillTreeUI SkillTreeUIRef => skillTreeUI;

        /// <summary>
        /// 스킬트리 페이지가 활성 상태인지 — InputSystem이 이 동안 그리드 입력을 차단해
        /// 스킬 업그레이드 탭이 뒤 레이어(그리드/로비)로 전달되는 것을 방지한다.
        /// </summary>
        public bool IsSkillTreeVisible => skillTreeUI != null && skillTreeUI.IsVisible;

        // 이벤트
        public event System.Action<GameState> OnGameStateChanged;
        public event System.Action<int> OnTurnChanged;
        public event System.Action OnGameOver;
        public event System.Action OnStageClear;
        public event System.Action<int> OnGoldChanged;

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

            // ★ 릴리스 빌드 로그 게이트 (감사 M5) — 프로젝트 전체 Debug.Log 997건이 무가드 상태라
            //   모바일 릴리스에서 호출마다 스택트레이스 수집+문자열 보간 비용이 발생.
            //   Warning 미만 로그를 차단하고 Log 스택트레이스를 끔 (LogError/Warning은 유지 → 크래시 리포트 보존).
            if (!Debug.isDebugBuild)
            {
                Debug.unityLogger.filterLogType = LogType.Warning;
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            }

            // UI 렌더링 품질 설정
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            Application.targetFrameRate = 60;

            // TutorialManager 싱글톤 자동 생성 (기능 해금 시스템 필수)
            if (TutorialManager.Instance == null)
            {
                var tutObj = new GameObject("TutorialManager");
                tutObj.transform.SetParent(transform);
                tutObj.AddComponent<TutorialManager>();
                Debug.Log("[GameManager] TutorialManager 싱글톤 자동 생성");
            }

            // ★ SubscriptionManager 싱글톤 자동 생성 (MPManager 이전에 생성 필요 — Start 이벤트 구독)
            if (SubscriptionManager.Instance == null)
            {
                var subObj = new GameObject("SubscriptionManager");
                subObj.transform.SetParent(transform);
                subObj.AddComponent<SubscriptionManager>();
            }

            // MPManager 싱글톤 자동 생성
            if (MPManager.Instance == null)
            {
                var mpObj = new GameObject("MPManager");
                mpObj.transform.SetParent(transform);
                mpObj.AddComponent<MPManager>();
            }

            // SkillTreeManager 싱글톤 자동 생성
            if (SkillTreeManager.Instance == null)
            {
                var skillObj = new GameObject("SkillTreeManager");
                skillObj.transform.SetParent(transform);
                skillObj.AddComponent<SkillTreeManager>();
            }

            // ★ SkillUpgradeOfferSystem 싱글톤 자동 생성 (오렌지 10개 → 스킬 학습 기회)
            if (SkillUpgradeOfferSystem.Instance == null)
            {
                var offerObj = new GameObject("SkillUpgradeOfferSystem");
                offerObj.transform.SetParent(transform);
                offerObj.AddComponent<SkillUpgradeOfferSystem>();
            }

            // 아이템 게이지 싱글톤 자동 생성
            if (JewelsHexaPuzzle.Items.HammerGauge.Instance == null)
            {
                var obj = new GameObject("HammerGaugeController");
                obj.transform.SetParent(transform);
                obj.AddComponent<JewelsHexaPuzzle.Items.HammerGauge>();
            }
            if (JewelsHexaPuzzle.Items.SwapGauge.Instance == null)
            {
                var obj = new GameObject("SwapGaugeController");
                obj.transform.SetParent(transform);
                obj.AddComponent<JewelsHexaPuzzle.Items.SwapGauge>();
            }
            if (JewelsHexaPuzzle.Items.LineGauge.Instance == null)
            {
                var obj = new GameObject("LineGaugeController");
                obj.transform.SetParent(transform);
                obj.AddComponent<JewelsHexaPuzzle.Items.LineGauge>();
            }

            // MonsterSpawnController 싱글톤 자동 생성
            if (MonsterSpawnController.Instance == null)
            {
                var spawnCtrlObj = new GameObject("MonsterSpawnController");
                spawnCtrlObj.transform.SetParent(transform);
                spawnCtrlObj.AddComponent<MonsterSpawnController>();
            }

            AutoFindReferences();
            InitializeSystems();
            LoadGold();
        }

        private void Start()
        {
            // CanvasScaler를 Screen Size 모드로 변경 (9:16 등 다양한 비율 대응)
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                ConfigureCanvasScaler(canvas);

                // 클로드 디자인 인게임 배경 (최하위 레이어)
                CreateInGameBackground(canvas);

                // HUD UI 생성 (이동횟수, 누적 점수)
                CreateHUDElements(canvas);

                // SFX 토글 버튼 생성 (우하단 상단)
                CreateSoundToggleButton(canvas);

                // BGM 토글 버튼 생성 (좌측 대각선 — 삼각형 클러스터)
                CreateBGMToggleButton(canvas);

                // 로비 복귀 버튼 생성 (우하단 하단 — 삼각형 클러스터)
                CreateLobbyExitButton(canvas);

                // ★ 인게임 → 로비 전환 시 보이는 풀스크린 페이드 오버레이 (입력 차단 + 처리 딜레이 가림)
                if (JewelsHexaPuzzle.UI.LobbyTransitionOverlay.Instance == null)
                {
                    GameObject overlayObj = new GameObject("LobbyTransitionOverlay");
                    var lto = overlayObj.AddComponent<JewelsHexaPuzzle.UI.LobbyTransitionOverlay>();
                    lto.Initialize(canvas.transform);
                }

                // ★ ChargeBar 패키지 통합: 헥사 충전 게이지 4개 아이템 버튼 생성
                //   - 충전율은 구버전 게이지(HammerGauge/SwapGauge/LineGauge)에서 직접 읽음
                //   - 탭 → 게이지 ActivateUseReady()/역회전 onClick 위임 (기존 발동 + 튜토리얼 이벤트)
                //   - 구버전 아이템 버튼은 CanvasGroup alpha 0으로 숨김(기능 유지) + 개명
                CreateChargeBar(canvas);

                // 클로드 디자인 스테이지 클리어 팝업(Parchment) 런타임 배치 (Resources 프리팹)
                CreateStageClearPopup(canvas);

                // 회전 방향 토글 버튼 제거됨

                // 게임오버 팝업 생성
                CreateGameOverPopup(canvas);

                // 망치 UI 생성
                if (FindObjectOfType<HammerItem>() == null)
                    CreateHammerUI(canvas);

                // 스왑 UI 생성
                if (FindObjectOfType<SwapItem>() == null)
                    CreateSwapUI(canvas);

                // 라인 UI 생성
                if (FindObjectOfType<LineDrawItem>() == null)
                    CreateLineDrawUI(canvas);

                // 역회전 UI 생성
                if (FindObjectOfType<JewelsHexaPuzzle.Items.ReverseRotationItem>() == null)
                    CreateReverseRotationUI(canvas);

                // 골드 추가 버튼 생성 (좌측 하단) — 치트 버튼: 에디터/개발 빌드 전용 (감사 H6 정책)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                CreateGoldAddButton(canvas);
#endif

                // 아이템 수량 변경 이벤트 구독
                if (ItemManager.Instance != null)
                {
                    ItemManager.Instance.OnItemCountChanged += UpdateItemCountBadge;
                }

                // MP 게이지 UI 생성
                if (mpGaugeUI == null)
                {
                    GameObject gaugeObj = new GameObject("MPGaugeUI");
                    mpGaugeUI = gaugeObj.AddComponent<MPGaugeUI>();
                    mpGaugeUI.Initialize(canvas.transform);
                }

                // 쉘 위험 비네트 오버레이 생성 (화면 외곽 붉은 그라데이션)
                if (shellDangerOverlay == null)
                {
                    GameObject overlayObj = new GameObject("ShellDangerOverlay");
                    shellDangerOverlay = overlayObj.AddComponent<ShellDangerOverlay>();
                    shellDangerOverlay.Initialize(canvas.transform);
                }

                // ★ 무입력 힌트 시스템 (10초 무회전 시 최다 정화 클러스터 펄스 강조)
                if (FindObjectOfType<RotationHintSystem>() == null)
                {
                    GameObject hintObj = new GameObject("RotationHintSystem");
                    hintObj.AddComponent<RotationHintSystem>();
                }

                // 특수 블록 테스트 버튼 패널 생성
                CreateTestBlockPanel(canvas);

                // 로비 UI 생성
                CreateLobbyUI(canvas);

                // 영구 스킬트리 UI 제거(런별 로그라이크 전환) — 생성하지 않음.
                //   스킬은 인게임 리워드 선택(SkillUpgradeOfferSystem)으로만 획득. skillTreeUI는 null 유지(참조부 null-가드).
            }
            // ★ 스킬 해금/초기화 시 특수 블록 아이콘 색상 갱신
            if (SkillTreeManager.Instance != null)
            {
                SkillTreeManager.Instance.OnSkillUnlocked += OnSkillUnlockedRefreshBlocks;
                SkillTreeManager.Instance.OnSkillTreeReset += OnSkillResetRefreshBlocks;
            }

            ShowLobby();
        }

        /// <summary>스킬 해금 시 모든 특수 블록 아이콘 색상 갱신</summary>
        private void OnSkillUnlockedRefreshBlocks(SkillType _)
        {
            RefreshAllSpecialBlockVisuals();
        }

        /// <summary>스킬 초기화 시 모든 특수 블록 아이콘 색상 갱신</summary>
        private void OnSkillResetRefreshBlocks()
        {
            RefreshAllSpecialBlockVisuals();
        }

        /// <summary>모든 블록의 UpdateVisuals 호출 (특수 블록 아이콘 색조 갱신)</summary>
        private void RefreshAllSpecialBlockVisuals()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null && block.Data.IsSpecial())
                    block.UpdateVisuals();
            }
        }

        /// <summary>
        /// CanvasScaler를 모바일 대응으로 설정 (Scale With Screen Size)
        /// </summary>
        private void ConfigureCanvasScaler(Canvas canvas)
        {
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler == null) return;

            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f; // 가로/세로 균형 스케일 (UI 화질 개선)
            scaler.referencePixelsPerUnit = 100f; // 스프라이트 렌더링 기준 해상도 통일
            scaler.dynamicPixelsPerUnit = 2f; // 텍스트/동적 UI 선명도 2배 향상
        }

        /// <summary>
        /// HUD 요소 동적 생성 (이동횟수: 우상단, 누적 점수: 상단 중앙)
        /// </summary>
        // HUD 직접 참조 (UIManager 연동 보장)
        private Text hudScoreText;
        private Text hudTurnText;
        private Text hudMaxTurnText;   // 무한도전 "max20" 표시
        private Text lobbyGoldText;
        private Text hudLevelBestText;    // 레벨 최고 점수
        private Text hudPersonalBestText; // 개인 최고 점수
        private Text hudDifficultyText;   // 인게임 난이도 표시 (상단)
        private Text hudLevelNumText;     // 인게임 레벨 번호 표시 (난이도 바로 밑)
        private GameObject sfxToggleBtnObj;   // SFX 토글 버튼 (로비/인게임 공용)
        private GameObject bgmToggleBtnObj;   // BGM 토글 버튼 (로비/인게임 공용)

        // 아이템 버튼 참조 (기능 해금 시스템용)
        private GameObject hammerButtonObj;
        private GameObject swapButtonObj;
        private GameObject lineDrawButtonObj;
        private GameObject reverseRotationButtonObj;

        private void CreateHUDElements(Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // === 이동횟수 (MY BEST 텍스트 아래, 중앙 정렬 + 프레임 UI) ===
            // 프레임 컨테이너
            GameObject turnFrameObj = new GameObject("HUD_TurnFrame");
            turnFrameObj.transform.SetParent(canvas.transform, false);
            RectTransform turnFrameRt = turnFrameObj.AddComponent<RectTransform>();
            turnFrameRt.anchorMin = new Vector2(0.5f, 1f);
            turnFrameRt.anchorMax = new Vector2(0.5f, 1f);
            turnFrameRt.pivot = new Vector2(0.5f, 1f);
            turnFrameRt.anchoredPosition = new Vector2(0f, -142f);
            turnFrameRt.sizeDelta = new Vector2(130f, 70f);

            // 프레임 배경 — 클로드 디자인 다크글래스 프레임 스프라이트 (Resources/UI/moves_frame, 챔퍼+틸 액센트)
            Image frameBg = turnFrameObj.AddComponent<Image>();
            var movesFrameSpr = Resources.Load<Sprite>("UI/moves_frame");
            if (movesFrameSpr != null)
            {
                frameBg.sprite = movesFrameSpr;
                frameBg.type = Image.Type.Simple;
                frameBg.color = Color.white;
            }
            else
            {
                // 폴백: 기존 반투명 어두운 패널 + 아웃라인
                frameBg.color = new Color(0.1f, 0.1f, 0.2f, 0.5f);
                var fo = turnFrameObj.AddComponent<Outline>();
                fo.effectColor = new Color(1f, 1f, 1f, 0.4f);
                fo.effectDistance = new Vector2(2, 2);
            }
            frameBg.raycastTarget = false;
            // 프레임 글로우 여백 반영: 살짝 키워 패널이 기존 크기감 유지
            turnFrameRt.sizeDelta = new Vector2(144f, 78f);

            // 이동횟수 숫자 (프레임 내부)
            GameObject turnObj = new GameObject("HUD_TurnText");
            turnObj.transform.SetParent(turnFrameObj.transform, false);
            RectTransform turnRt = turnObj.AddComponent<RectTransform>();
            turnRt.anchorMin = new Vector2(0, 0.3f);
            turnRt.anchorMax = new Vector2(1, 1);
            turnRt.offsetMin = new Vector2(5f, 0f);
            turnRt.offsetMax = new Vector2(-5f, -4f);
            Text turnLabel = turnObj.AddComponent<Text>();
            turnLabel.font = font;
            turnLabel.fontSize = 32;
            turnLabel.fontStyle = FontStyle.Bold;
            turnLabel.alignment = TextAnchor.MiddleCenter;
            turnLabel.color = Color.white;
            turnLabel.raycastTarget = false;
            turnLabel.resizeTextForBestFit = true;
            turnLabel.resizeTextMinSize = 14;
            turnLabel.resizeTextMaxSize = 32;
            turnLabel.verticalOverflow = VerticalWrapMode.Overflow;
            turnLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            turnLabel.text = currentGameMode == GameMode.Infinite ? "0" : initialTurns.ToString();
            // 텍스트 아웃라인 (가독성 강화)
            Outline turnOutline = turnObj.AddComponent<Outline>();
            turnOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            turnOutline.effectDistance = new Vector2(2, 2);
            hudTurnText = turnLabel;

            // MOVES 라벨 (프레임 하단)
            GameObject turnLabelObj = new GameObject("HUD_TurnLabel");
            turnLabelObj.transform.SetParent(turnFrameObj.transform, false);
            RectTransform turnLabelRt = turnLabelObj.AddComponent<RectTransform>();
            turnLabelRt.anchorMin = new Vector2(0, 0);
            turnLabelRt.anchorMax = new Vector2(1, 0.3f);
            turnLabelRt.offsetMin = Vector2.zero;
            turnLabelRt.offsetMax = Vector2.zero;
            Text turnLabelText = turnLabelObj.AddComponent<Text>();
            turnLabelText.font = font;
            turnLabelText.fontSize = 14;
            turnLabelText.fontStyle = FontStyle.Bold;
            turnLabelText.alignment = TextAnchor.MiddleCenter;
            turnLabelText.color = new Color(0.5f, 0.94f, 0.886f, 0.92f); // 틸 액센트(프레임과 통일)
            turnLabelText.raycastTarget = false;
            turnLabelText.text = "MOVES";

            // 무한도전 "max20" 표시 (프레임 좌상단)
            GameObject maxTurnObj = new GameObject("HUD_MaxTurnText");
            maxTurnObj.transform.SetParent(turnFrameObj.transform, false);
            RectTransform maxTurnRt = maxTurnObj.AddComponent<RectTransform>();
            maxTurnRt.anchorMin = new Vector2(0, 1);
            maxTurnRt.anchorMax = new Vector2(0, 1);
            maxTurnRt.pivot = new Vector2(0, 1);
            maxTurnRt.anchoredPosition = new Vector2(4f, -2f);
            maxTurnRt.sizeDelta = new Vector2(50f, 18f);
            Text maxTurnLabel = maxTurnObj.AddComponent<Text>();
            maxTurnLabel.font = font;
            maxTurnLabel.fontSize = 15;
            maxTurnLabel.alignment = TextAnchor.UpperLeft;
            maxTurnLabel.color = new Color(0.7f, 0.7f, 0.8f, 0.7f);
            maxTurnLabel.raycastTarget = false;
            maxTurnLabel.text = "max" + MAX_INFINITE_TURNS;
            hudMaxTurnText = maxTurnLabel;
            // Stage 모드에서는 숨김
            maxTurnObj.SetActive(currentGameMode == GameMode.Infinite);

            // === 누적 점수 (중앙 상단) ===
            GameObject scoreObj = new GameObject("HUD_ScoreText");
            scoreObj.transform.SetParent(canvas.transform, false);
            RectTransform scoreRt = scoreObj.AddComponent<RectTransform>();
            scoreRt.anchorMin = new Vector2(0.5f, 1f);
            scoreRt.anchorMax = new Vector2(0.5f, 1f);
            scoreRt.pivot = new Vector2(0.5f, 1f);
            scoreRt.anchoredPosition = new Vector2(0f, -20f);
            scoreRt.sizeDelta = new Vector2(300f, 50f);
            Text scoreLabel = scoreObj.AddComponent<Text>();
            scoreLabel.font = font;
            scoreLabel.fontSize = 36;
            scoreLabel.alignment = TextAnchor.MiddleCenter;
            scoreLabel.color = Color.white;
            scoreLabel.raycastTarget = false;
            scoreLabel.resizeTextForBestFit = true;
            scoreLabel.resizeTextMinSize = 14;
            scoreLabel.resizeTextMaxSize = 36;
            scoreLabel.verticalOverflow = VerticalWrapMode.Overflow;
            scoreLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            scoreLabel.text = "0";
            hudScoreText = scoreLabel;

            // 점수 라벨
            GameObject scoreLabelObj = new GameObject("HUD_ScoreLabel");
            scoreLabelObj.transform.SetParent(canvas.transform, false);
            RectTransform scoreLabelRt = scoreLabelObj.AddComponent<RectTransform>();
            scoreLabelRt.anchorMin = new Vector2(0.5f, 1f);
            scoreLabelRt.anchorMax = new Vector2(0.5f, 1f);
            scoreLabelRt.pivot = new Vector2(0.5f, 1f);
            scoreLabelRt.anchoredPosition = new Vector2(0f, -65f);
            scoreLabelRt.sizeDelta = new Vector2(300f, 24f);
            Text scoreLabelText = scoreLabelObj.AddComponent<Text>();
            scoreLabelText.font = font;
            scoreLabelText.fontSize = 16;
            scoreLabelText.alignment = TextAnchor.MiddleCenter;
            scoreLabelText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            scoreLabelText.raycastTarget = false;
            scoreLabelText.text = "SCORE";

            // === 레벨 정보 표시 (난이도 위, 레벨 번호 아래 — 2행) ===
            {
                var levelData = LevelRegistry.GetLevel(selectedStage);
                DifficultyType diff = levelData != null ? levelData.difficultyType : DifficultyType.Easy;

                string diffLabel;
                Color diffColor;
                switch (diff)
                {
                    case DifficultyType.Easy:
                        diffLabel = "쉬움";
                        diffColor = new Color(0.3f, 0.9f, 0.3f);
                        break;
                    case DifficultyType.Normal:
                        diffLabel = "보통";
                        diffColor = new Color(1f, 0.85f, 0.2f);
                        break;
                    case DifficultyType.Hard:
                    default:
                        diffLabel = "어려움";
                        diffColor = new Color(1f, 0.3f, 0.3f);
                        break;
                }

                // ★ 난이도 텍스트 (상단)
                GameObject diffObj = new GameObject("HUD_Difficulty");
                diffObj.transform.SetParent(canvas.transform, false);
                RectTransform diffRt = diffObj.AddComponent<RectTransform>();
                diffRt.anchorMin = new Vector2(0f, 1f);
                diffRt.anchorMax = new Vector2(0f, 1f);
                diffRt.pivot = new Vector2(0f, 1f);
                diffRt.anchoredPosition = new Vector2(190f, -20f); // ★ 난이도 텍스트(메달리온 제거, 위치 유지)
                diffRt.sizeDelta = new Vector2(200f, 24f);
                hudDifficultyText = diffObj.AddComponent<Text>();
                hudDifficultyText.font = font;
                hudDifficultyText.fontSize = 18;
                hudDifficultyText.fontStyle = FontStyle.Bold;
                hudDifficultyText.alignment = TextAnchor.MiddleLeft;
                hudDifficultyText.color = diffColor;
                hudDifficultyText.raycastTarget = false;
                hudDifficultyText.text = diffLabel;
                Outline diffOutline = diffObj.AddComponent<Outline>();
                diffOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
                diffOutline.effectDistance = new Vector2(1, 1);
                hudElements.Add(diffObj);

                // ★ 레벨 번호 텍스트 (난이도 바로 아래)
                GameObject levelNumObj = new GameObject("HUD_LevelNum");
                levelNumObj.transform.SetParent(canvas.transform, false);
                RectTransform levelNumRt = levelNumObj.AddComponent<RectTransform>();
                levelNumRt.anchorMin = new Vector2(0f, 1f);
                levelNumRt.anchorMax = new Vector2(0f, 1f);
                levelNumRt.pivot = new Vector2(0f, 1f);
                levelNumRt.anchoredPosition = new Vector2(190f, -44f); // ★ 난이도 바로 아래 (20+24=44)
                levelNumRt.sizeDelta = new Vector2(200f, 22f);
                hudLevelNumText = levelNumObj.AddComponent<Text>();
                hudLevelNumText.font = font;
                hudLevelNumText.fontSize = 16;
                hudLevelNumText.fontStyle = FontStyle.Bold;
                hudLevelNumText.alignment = TextAnchor.MiddleLeft;
                hudLevelNumText.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
                hudLevelNumText.raycastTarget = false;
                hudLevelNumText.text = $"LEVEL {selectedStage}";
                hudLevelNumText.color = new Color(0.92f, 0.95f, 1f, 0.95f); // 다크글래스 HUD 톤에 맞춘 크림화이트
                Outline levelNumOutline = levelNumObj.AddComponent<Outline>();
                levelNumOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
                levelNumOutline.effectDistance = new Vector2(1, 1);
                hudElements.Add(levelNumObj);

                // (난이도 메달리온 아이콘 제거 — 사용자 요청 2026-06-30. 텍스트만 표시.)
            }

            // === 레벨 최고 점수 (중앙 상단, SCORE 아래) ===
            int levelBest = scoreManager != null ? scoreManager.GetLevelHighScore(selectedStage) : 0;
            int personalBest = scoreManager != null ? scoreManager.GetPersonalLevelBest(selectedStage) : 0;

            GameObject levelBestObj = new GameObject("HUD_LevelBestText");
            levelBestObj.transform.SetParent(canvas.transform, false);
            RectTransform levelBestRt = levelBestObj.AddComponent<RectTransform>();
            levelBestRt.anchorMin = new Vector2(0.5f, 1f);
            levelBestRt.anchorMax = new Vector2(0.5f, 1f);
            levelBestRt.pivot = new Vector2(0.5f, 1f);
            levelBestRt.anchoredPosition = new Vector2(0f, -85f);
            levelBestRt.sizeDelta = new Vector2(300f, 22f);
            Text levelBestLabel = levelBestObj.AddComponent<Text>();
            levelBestLabel.font = font;
            levelBestLabel.fontSize = 14;
            levelBestLabel.alignment = TextAnchor.MiddleCenter;
            levelBestLabel.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            levelBestLabel.raycastTarget = false;
            levelBestLabel.text = levelBest > 0 ? string.Format("BEST: {0:N0}", levelBest) : "BEST: ---";
            hudLevelBestText = levelBestLabel;
            Outline levelBestOutline = levelBestObj.AddComponent<Outline>();
            levelBestOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            levelBestOutline.effectDistance = new Vector2(1, 1);

            GameObject personalBestObj = new GameObject("HUD_PersonalBestText");
            personalBestObj.transform.SetParent(canvas.transform, false);
            RectTransform personalBestRt = personalBestObj.AddComponent<RectTransform>();
            personalBestRt.anchorMin = new Vector2(0.5f, 1f);
            personalBestRt.anchorMax = new Vector2(0.5f, 1f);
            personalBestRt.pivot = new Vector2(0.5f, 1f);
            personalBestRt.anchoredPosition = new Vector2(0f, -105f);
            personalBestRt.sizeDelta = new Vector2(300f, 22f);
            Text personalBestLabel = personalBestObj.AddComponent<Text>();
            personalBestLabel.font = font;
            personalBestLabel.fontSize = 14;
            personalBestLabel.alignment = TextAnchor.MiddleCenter;
            personalBestLabel.color = new Color(0.7f, 0.9f, 1f, 0.9f);
            personalBestLabel.raycastTarget = false;
            personalBestLabel.text = personalBest > 0 ? string.Format("MY BEST: {0:N0}", personalBest) : "MY BEST: ---";
            hudPersonalBestText = personalBestLabel;
            Outline personalBestOutline = personalBestObj.AddComponent<Outline>();
            personalBestOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            personalBestOutline.effectDistance = new Vector2(1, 1);

            hudElements.Add(levelBestObj);
            hudElements.Add(personalBestObj);

            // === 골드 (우측 상단): 코인 + 숫자 수평 정렬 그룹 (GOLD 텍스트 제거 — 사용자 요청) ===
            //   HorizontalLayoutGroup + ContentSizeFitter → 코인이 항상 숫자 좌측에 완전 표시(겹침/잘림 0),
            //   childAlignment MiddleRight로 코인-숫자 수평(세로 중앙) 정렬. 숫자 폭이 변해도 코인은 자동으로 숫자 좌측 유지.
            GameObject goldGroupObj = new GameObject("HUD_GoldGroup");
            goldGroupObj.transform.SetParent(canvas.transform, false);
            RectTransform goldGroupRt = goldGroupObj.AddComponent<RectTransform>();
            goldGroupRt.anchorMin = goldGroupRt.anchorMax = new Vector2(1f, 1f);
            goldGroupRt.pivot = new Vector2(1f, 1f);
            goldGroupRt.anchoredPosition = new Vector2(-26f, -20f);
            var goldHL = goldGroupObj.AddComponent<HorizontalLayoutGroup>();
            goldHL.childAlignment = TextAnchor.MiddleRight;
            goldHL.spacing = 6f;
            goldHL.childControlWidth = true; goldHL.childControlHeight = true;
            goldHL.childForceExpandWidth = false; goldHL.childForceExpandHeight = false;
            var goldCSF = goldGroupObj.AddComponent<ContentSizeFitter>();
            goldCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            goldCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 골드 코인 (좌측, 항상 완전 표시) — 클로드 디자인 별 메달리온 금화 PNG 재사용
            GameObject goldCoinObj = new GameObject("HUD_GoldCoin");
            goldCoinObj.transform.SetParent(goldGroupObj.transform, false);
            goldCoinObj.AddComponent<RectTransform>();
            Image goldCoinImg = goldCoinObj.AddComponent<Image>();
            Sprite goldCoinSprite = Resources.Load<Sprite>("UI/gold_coin");
            if (goldCoinSprite != null) goldCoinImg.sprite = goldCoinSprite;
            goldCoinImg.raycastTarget = false;
            goldCoinImg.preserveAspect = true;
            var coinLE = goldCoinObj.AddComponent<LayoutElement>();
            coinLE.preferredWidth = 34f; coinLE.preferredHeight = 34f;

            // 골드 숫자 (우측, 코인과 수평 정렬)
            GameObject goldObj = new GameObject("HUD_GoldText");
            goldObj.transform.SetParent(goldGroupObj.transform, false);
            goldObj.AddComponent<RectTransform>();
            Text goldLabel = goldObj.AddComponent<Text>();
            goldLabel.font = font;
            goldLabel.fontSize = 28;
            goldLabel.fontStyle = FontStyle.Bold;
            goldLabel.alignment = TextAnchor.MiddleRight;
            goldLabel.color = new Color(1f, 0.816f, 0.42f); // 로비 골드 톤(#FFD06B)
            goldLabel.raycastTarget = false;
            goldLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            goldLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var goldLE = goldObj.AddComponent<LayoutElement>();
            goldLE.minHeight = 34f;
            JewelsHexaPuzzle.Utils.NumberRoller.SetImmediate(goldLabel, currentGold, v => v.ToString());
            Outline goldOutline = goldObj.AddComponent<Outline>();
            goldOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            goldOutline.effectDistance = new Vector2(1, 1);

            // UIManager에 연결
            if (uiManager != null)
            {
                uiManager.SetTurnText(turnLabel);
                uiManager.SetScoreText(scoreLabel);
                uiManager.SetGoldText(goldLabel);
            }

            // HUD 요소 추적 (로비에서 숨기기 용)
            hudElements.Add(turnFrameObj);
            hudElements.Add(scoreObj);
            hudElements.Add(scoreLabelObj);
            hudElements.Add(goldGroupObj); // 코인+숫자 그룹 (자식 포함 일괄 추적)

            Debug.Log("[GameManager] HUD 요소 생성 완료 (이동횟수 + 누적 점수 + 골드)");
        }

        /// <summary>
        /// 사운드 온/오프 토글 버튼 생성 (우측 하단)
        /// </summary>
        private void CreateSoundToggleButton(Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bool startMuted = AudioManager.Instance != null && AudioManager.Instance.IsSfxMuted;

            // 우측 하단 고정 배치
            // 버튼 컨테이너
            sfxToggleBtnObj = new GameObject("SoundToggleButton");
            sfxToggleBtnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = sfxToggleBtnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(-72f, 160f);
            btnRt.sizeDelta = new Vector2(84f, 84f);

            // 육각형 배경 (밝은 하늘색) — 기존 디자인 유지
            Image btnBg = sfxToggleBtnObj.AddComponent<Image>();
            btnBg.sprite = HexBlock.GetHexFlashSprite();
            btnBg.type = Image.Type.Simple;
            btnBg.preserveAspect = true;
            btnBg.color = new Color(0.55f, 0.70f, 0.85f, 0.90f);

            Button btn = sfxToggleBtnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.65f, 0.80f, 0.95f, 0.95f);
            btnColors.pressedColor = new Color(0.40f, 0.55f, 0.70f, 0.95f);
            btn.colors = btnColors;

            // ★ 외부 PNG 아이콘을 자식으로 배치 (Resources/UI/icon_sound_on/off — outlined 스타일)
            Sprite sfxOnSprite = Resources.Load<Sprite>("UI/icon_sound_on");
            Sprite sfxOffSprite = Resources.Load<Sprite>("UI/icon_sound_off");

            GameObject iconObj = new GameObject("SoundIcon");
            iconObj.transform.SetParent(sfxToggleBtnObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = startMuted ? sfxOffSprite : sfxOnSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(54f, 54f);   // 육각 84x84 안에 54x54 아이콘

            // 클릭 이벤트: 아이콘 sprite swap
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance == null) return;
                bool muted = AudioManager.Instance.ToggleSFXMute();
                iconImg.sprite = muted ? sfxOffSprite : sfxOnSprite;
            });

            // hudElements에 추가하지 않음 → 로비/인게임 모두 표시
            Debug.Log("[GameManager] SFX 토글 버튼 생성 완료 (우하단)");
        }

        // 로비 복귀 버튼 참조 (로비에서 숨기기 용)
        private GameObject lobbyExitBtnObj;

        /// <summary>
        /// 로비 복귀 버튼 생성 (사운드 버튼 아래, 문 모양 아이콘)
        /// </summary>
        private void CreateLobbyExitButton(Canvas canvas)
        {
            float btnSize = 84f;
            float hexHeight = btnSize * Mathf.Sqrt(3f) / 2f; // 육각형 높이 ≈ 72.75

            // 우측 하단 고정 배치 — 사운드 버튼과 5px 여백 (삼각형 클러스터)
            float gap = 5f;
            lobbyExitBtnObj = new GameObject("LobbyExitButton");
            lobbyExitBtnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = lobbyExitBtnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(-72f, 160f - hexHeight - gap);
            btnRt.sizeDelta = new Vector2(btnSize, btnSize);

            Image btnBg = lobbyExitBtnObj.AddComponent<Image>();
            btnBg.sprite = HexBlock.GetHexFlashSprite();
            btnBg.type = Image.Type.Simple;
            btnBg.preserveAspect = true;
            btnBg.color = new Color(0.85f, 0.65f, 0.55f, 0.90f); // 밝은 살구색

            Button btn = lobbyExitBtnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.95f, 0.75f, 0.65f, 0.95f);
            btnColors.pressedColor = new Color(0.65f, 0.50f, 0.40f, 0.95f);
            btn.colors = btnColors;

            // === 문 모양 프로시저럴 아이콘 ===

            // 문 프레임 (외곽)
            GameObject frame = new GameObject("DoorFrame");
            frame.transform.SetParent(lobbyExitBtnObj.transform, false);
            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.85f, 0.7f, 0.45f);
            frameImg.raycastTarget = false;
            RectTransform frameRt = frame.GetComponent<RectTransform>();
            frameRt.anchoredPosition = new Vector2(0f, 2f);
            frameRt.sizeDelta = new Vector2(30f, 40f);

            // 문 패널 (안쪽)
            GameObject panel = new GameObject("DoorPanel");
            panel.transform.SetParent(frame.transform, false);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.55f, 0.35f, 0.15f);
            panelImg.raycastTarget = false;
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(24f, 34f);

            // 문 손잡이
            GameObject knob = new GameObject("DoorKnob");
            knob.transform.SetParent(panel.transform, false);
            Image knobImg = knob.AddComponent<Image>();
            knobImg.color = new Color(1f, 0.85f, 0.3f);
            knobImg.raycastTarget = false;
            RectTransform knobRt = knob.GetComponent<RectTransform>();
            knobRt.anchoredPosition = new Vector2(6f, -2f);
            knobRt.sizeDelta = new Vector2(5f, 5f);

            // 나가기 화살표 (→)
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject arrow = new GameObject("ExitArrow");
            arrow.transform.SetParent(lobbyExitBtnObj.transform, false);
            Text arrowText = arrow.AddComponent<Text>();
            arrowText.font = font;
            arrowText.fontSize = 20;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = new Color(1f, 1f, 1f, 0.8f);
            arrowText.raycastTarget = false;
            arrowText.text = "\u2192";
            RectTransform arrowRt = arrow.GetComponent<RectTransform>();
            arrowRt.anchoredPosition = new Vector2(22f, 2f);
            arrowRt.sizeDelta = new Vector2(20f, 20f);

            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                ExitToLobby();
            });

            lobbyExitBtnObj.transform.SetAsLastSibling();
            hudElements.Add(lobbyExitBtnObj);
            Debug.Log("[GameManager] 로비 복귀 버튼 생성 완료 (사운드 버튼 아래)");
        }

        private GameObject inGameBackgroundObj;

        /// <summary>
        /// 클로드 디자인 인게임 배경(Resources/UI/Background_InGame)을 캔버스 최하위에 풀스크린 배치.
        /// 그리드/HUD보다 뒤(첫 형제)라 가리지 않는다.
        /// </summary>
        private void CreateInGameBackground(Canvas canvas)
        {
            if (inGameBackgroundObj != null) return;
            Sprite bg = Resources.Load<Sprite>("UI/Background_InGame");
            if (bg == null) return; // 에셋 없으면 스킵 (기존 화면 유지)

            inGameBackgroundObj = new GameObject("InGameBackground", typeof(RectTransform));
            inGameBackgroundObj.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)inGameBackgroundObj.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = inGameBackgroundObj.AddComponent<Image>();
            img.sprite = bg;
            img.color = Color.white;
            img.raycastTarget = false;
            img.preserveAspect = false; // 풀스크린 스트레치
            inGameBackgroundObj.transform.SetAsFirstSibling(); // 최하위(뒤)
        }

        private GameObject chargeBarObj;

        /// <summary>
        /// ChargeBar 패키지(헥사 충전 게이지) 생성 + 구버전 아이템 버튼 숨김.
        /// ChargeBar는 구버전 게이지/아이템 시스템의 비주얼 스킨 — 충전율은 게이지에서 읽고,
        /// 탭은 게이지 ActivateUseReady()/역회전 버튼 onClick에 위임(튜토리얼 이벤트 포함).
        /// </summary>
        private void CreateChargeBar(Canvas canvas)
        {
            if (chargeBarObj != null) return;
            chargeBarObj = new GameObject("ChargeBarController");
            chargeBarObj.transform.SetParent(canvas.transform, false);
            var cbc = chargeBarObj.AddComponent<JewelsHexaPuzzle.Items.ChargeBarController>();

            // ★ 새 버튼을 기존 아이템 버튼 4개 위치(우측 헥사 클러스터)에 그대로 배치
            //   — 인덱스 매핑: 0=망치, 1=스왑, 2=라인, 3=역회전 (ChargeBarController.Items 순서와 동일)
            Vector2[] itemPositions = new Vector2[] {
                GetItemButtonPosition(0), // 망치
                GetItemButtonPosition(1), // 스왑
                GetItemButtonPosition(2), // 라인
                GetItemButtonPosition(3), // 역회전
            };
            cbc.Build(canvas.transform, itemPositions);

            // 기존 아이템 버튼(헥사 비주얼) 숨김 — ChargeBar로 대체 (중복 방지)
            StartCoroutine(HideLegacyItemButtonsNextFrame());
        }

        private GameObject stageClearPopupObj; // 클로드 디자인 새 클리어 팝업 인스턴스

        /// <summary>
        /// 클로드 디자인 스테이지 클리어 팝업(Parchment) 프리팹을 Resources에서 로드해 캔버스에 배치(비활성).
        /// 에셋이 아직 없으면(에디터 자동셋업 미실행) 조용히 스킵 → 기존 팝업으로 폴백된다.
        /// </summary>
        private void CreateStageClearPopup(Canvas canvas)
        {
            if (stageClearPopupObj != null) return;
            var prefab = Resources.Load<GameObject>("StageClearPopup_Parchment");
            if (prefab == null) return; // 에셋 미생성 → 기존 클리어 팝업 폴백

            stageClearPopupObj = Instantiate(prefab, canvas.transform, false);
            stageClearPopupObj.name = "StageClearPopup";
            stageClearPopupObj.transform.SetAsLastSibling();
            stageClearPopupObj.SetActive(false);

            var popup = stageClearPopupObj.GetComponent<HexaPuzzle.StageClear.StageClearPopup>();
            if (popup != null)
            {
                // 확인 → 팝업 닫힘(HandleConfirm 내장 Hide) + 로비 복귀
                if (popup.onConfirm == null) popup.onConfirm = new UnityEngine.Events.UnityEvent();
                popup.onConfirm.RemoveAllListeners();
                popup.onConfirm.AddListener(() => ReturnToLobby());

                // ★ 프리팹 저장 시 런타임 AddListener(onClick→HandleConfirm)가 직렬화되지 않아
                //   인스턴스의 확인 버튼이 死버튼이 됨 → 런타임에 다시 배선
                if (popup.confirmButton != null)
                {
                    popup.confirmButton.onClick.RemoveAllListeners();
                    popup.confirmButton.onClick.AddListener(popup.HandleConfirm);
                }

                // 제목/라벨 한글·텍스트 교체
                if (popup.titleText != null) popup.titleText.text = "레벨 클리어";
                if (popup.stageBestLabel != null) popup.stageBestLabel.text = "LEVEL BEST";

                // 상단 별이 뒤집혀 있어 180° 회전해 똑바로 세움
                if (popup.stars != null)
                    foreach (var s in popup.stars)
                        if (s != null) s.localEulerAngles = new Vector3(0f, 0f, 180f);

                // ★ 팝업을 인게임 다크글래스+골드 톤으로 통일 재테마 (양피지/코랄 → 다크글래스/골드).
                //   ⚠️ 기존엔 popup.card가 null이면 패널 재색칠이 통째 스킵돼 양피지/코랄이 남았음 → 팝업 전체 이미지를 순회.
                popup.theme = HexaPuzzle.StageClear.StageClearTheme.Mystic; // 다크 테마(별/액센트 골드)
                foreach (var img in stageClearPopupObj.GetComponentsInChildren<Image>(true))
                {
                    if (img == null) continue;
                    var cc = img.color;
                    string nm = img.gameObject.name;
                    // 별 이미지 → 골드 통일
                    if (popup.starImages != null && System.Array.IndexOf(popup.starImages, img) >= 0)
                    { img.color = new Color(0.96f, 0.80f, 0.40f, cc.a); continue; }
                    // 배경 딤(거의 검정 반투명) 유지
                    if (cc.r < 0.16f && cc.g < 0.16f && cc.b < 0.16f) continue;
                    if (nm == "CardBody") { ClaudeTheme.ApplyPopupPanel(img); continue; }      // 메인 카드 → 다크글래스
                    if (nm == "ConfirmButton") { ClaudeTheme.StylePopupPrimary(null, img); continue; } // 확인 → 골드 주버튼
                    // 크림/밝은 패널(시트·내부·골드필) → 다크 인셋
                    if (cc.r > 0.55f && cc.g > 0.50f && cc.b > 0.42f)
                    { img.color = new Color(0.07f, 0.10f, 0.18f, 0.92f); continue; }
                    // 코랄/마룬/테라코타: 작은 뱃지·테두리 → 골드, 큰 박스(점수판) → 다크
                    if (cc.r > cc.g && cc.r > cc.b && cc.r > 0.3f)
                    {
                        bool accent = nm.Contains("Badge") || nm.Contains("Pill") || nm.Contains("Border") || nm.Contains("Frame");
                        img.color = accent ? new Color(0.92f, 0.78f, 0.36f, cc.a)
                                           : new Color(0.09f, 0.07f, 0.13f, 0.92f);
                    }
                }
                // ★ Shadow/Outline 이펙트의 코랄 effectColor → 중립 검정 (Image 루프가 못 잡는 마룬 그림자 제거).
                //   Outline은 Shadow 파생이라 한 번에 처리됨. 알파는 보존.
                foreach (var sh in stageClearPopupObj.GetComponentsInChildren<UnityEngine.UI.Shadow>(true))
                    if (sh != null) sh.effectColor = new Color(0f, 0f, 0f, sh.effectColor.a);

                // 텍스트 라이트 톤 relight + 타이틀 골드
                foreach (var t in stageClearPopupObj.GetComponentsInChildren<Text>(true))
                    if (t != null) t.color = ClaudeTheme.PopupText;
                if (popup.titleText != null) popup.titleText.color = ClaudeTheme.PopupTitle;
                if (popup.confirmButton != null)
                {
                    var cbTxt = popup.confirmButton.GetComponentInChildren<Text>();
                    if (cbTxt != null) cbTxt.color = ClaudeTheme.PopupPrimaryLabel;
                }
            }
            Debug.Log("[GameManager] 클로드 디자인 클리어 팝업(Parchment) 배치 완료");
        }

        private System.Collections.IEnumerator HideLegacyItemButtonsNextFrame()
        {
            // 아이템 버튼 생성(같은 Start 내 동기 생성) 완료 후 한 프레임 대기
            yield return null;

            // (1) UIManager 직렬화 itemButtons (구버전 잔재) — 있으면 숨김
            var um = uiManager != null ? uiManager : FindObjectOfType<UIManager>();
            if (um != null && um.ItemButtons != null)
            {
                foreach (var b in um.ItemButtons)
                    if (b != null) b.gameObject.SetActive(false);
            }

            // (2) 구버전 아이템 버튼(망치/스왑/라인/역회전) — ChargeBar로 대체.
            //     SetActive(false) 대신 CanvasGroup alpha 0으로 "기능 유지 + 비주얼만 숨김"
            //     → 게이지/아이템 컴포넌트가 계속 동작해 ChargeBar가 발동을 위임할 수 있다.
            //     이름을 바꿔 튜토리얼 FindUITarget이 ChargeBar 버튼(정식 이름)을 가리키도록 한다.
            HideLegacyItemButton(FindObjectOfType<HammerItem>(true)?.gameObject);
            HideLegacyItemButton(FindObjectOfType<SwapItem>(true)?.gameObject);
            HideLegacyItemButton(FindObjectOfType<LineDrawItem>(true)?.gameObject);
            HideLegacyItemButton(FindObjectOfType<JewelsHexaPuzzle.Items.ReverseRotationItem>(true)?.gameObject);

            Debug.Log("[GameManager] 구버전 아이템 버튼 숨김+개명 (ChargeBar 비주얼 스킨으로 대체)");
        }

        /// <summary>
        /// 구버전 아이템 버튼을 비주얼만 숨긴다(게이지/아이템 로직은 그대로 유지).
        /// CanvasGroup alpha 0 + 레이캐스트 차단 + 이름 끝에 "_legacy" 부여
        /// (튜토리얼 FindUITarget이 동일 이름의 ChargeBar 버튼을 찾도록).
        /// </summary>
        private void HideLegacyItemButton(GameObject btn)
        {
            if (btn == null) return;
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;

            // ★ 게이지(HammerGauge.TryFindButton 등)의 ForceAlphaOne()이 CanvasGroup.alpha를
            //   강제로 1로 되돌리는 문제 → 위치를 캔버스 밖으로 이동해 시각적으로 완전 차단.
            //   새 ChargeBar 버튼은 이미 GetItemButtonPosition 좌표 스냅샷으로 배치돼 영향 없음.
            var rt = btn.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = new Vector2(10000f, 10000f);

            // ★ 자식의 모든 Graphic(Image/Text/RawImage 등) 렌더링 자체를 차단.
            //    alpha를 외부에서 1로 되돌려도, Graphic.enabled=false면 화면에 그려지지 않는다.
            //    HammerItem 등 로직 컴포넌트는 그대로 살아있어 ChargeBar가 발동을 위임할 수 있다.
            foreach (var g in btn.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                g.enabled = false;

            // ★ LateUpdate에서 위치/알파/Graphic.enabled를 강제로 유지하는 가드를 부착.
            //    아이템 로직(HammerItem.Update 등)이 자기 이미지를 다시 켜더라도 매 프레임 차단.
            if (btn.GetComponent<LegacyButtonHider>() == null)
                btn.AddComponent<LegacyButtonHider>();

            if (!btn.name.EndsWith("_legacy")) btn.name += "_legacy";
        }

        /// <summary>
        /// BGM 토글 버튼 생성 (사운드/나가기 버튼 왼쪽 대각선 — 삼각형 클러스터 배치)
        /// 음표(♪) 프로시저럴 아이콘 사용
        /// </summary>
        private void CreateBGMToggleButton(Canvas canvas)
        {
            float btnSize = 84f;
            float hexRadius = btnSize / 2f; // 42
            float hexHeight = btnSize * Mathf.Sqrt(3f) / 2f; // ≈72.75

            // 삼각형 클러스터 배치: SFX(-72,160) 과 EXIT 사이 왼쪽 대각선
            // 면 간 5px 여백 적용
            float gap = 5f;
            float sfxBtnX = -72f;
            float sfxBtnY = 160f;
            float diagScale = (hexHeight + gap) / hexHeight; // 간격 보정 비율
            float bgmX = sfxBtnX - 1.5f * hexRadius * diagScale;
            float bgmY = sfxBtnY - (hexHeight / 2f) * diagScale;

            bool startMuted = AudioManager.Instance != null && AudioManager.Instance.IsBgmMuted;

            // 버튼 컨테이너
            bgmToggleBtnObj = new GameObject("BGMToggleButton");
            bgmToggleBtnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = bgmToggleBtnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 0f);
            btnRt.anchorMax = new Vector2(1f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(bgmX, bgmY);
            btnRt.sizeDelta = new Vector2(btnSize, btnSize);

            // 육각형 배경 (밝은 라벤더/보라) — 기존 디자인 유지
            Image btnBg = bgmToggleBtnObj.AddComponent<Image>();
            btnBg.sprite = HexBlock.GetHexFlashSprite();
            btnBg.type = Image.Type.Simple;
            btnBg.preserveAspect = true;
            btnBg.color = new Color(0.70f, 0.55f, 0.85f, 0.90f);

            Button btn = bgmToggleBtnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.80f, 0.65f, 0.95f, 0.95f);
            btnColors.pressedColor = new Color(0.50f, 0.40f, 0.65f, 0.95f);
            btn.colors = btnColors;

            // ★ 외부 PNG 아이콘을 자식으로 배치 (Resources/UI/icon_bgm_on/off — outlined 스타일)
            Sprite bgmOnSprite = Resources.Load<Sprite>("UI/icon_bgm_on");
            Sprite bgmOffSprite = Resources.Load<Sprite>("UI/icon_bgm_off");

            GameObject iconObj = new GameObject("BGMIcon");
            iconObj.transform.SetParent(bgmToggleBtnObj.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = startMuted ? bgmOffSprite : bgmOnSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchoredPosition = Vector2.zero;
            iconRt.sizeDelta = new Vector2(54f, 54f);

            // 클릭 이벤트: 아이콘 sprite swap
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance == null) return;
                bool muted = AudioManager.Instance.ToggleBGMMute();
                iconImg.sprite = muted ? bgmOffSprite : bgmOnSprite;
            });

            // hudElements에 추가하지 않음 → 로비/인게임 모두 표시
            Debug.Log($"[GameManager] BGM 토글 버튼 생성 완료 — 위치:({bgmX}, {bgmY})");
        }

        /// <summary>
        /// 모드 토글 버튼 생성 (좌하단 — 사운드 버튼과 대칭)
        /// </summary>
        private GameObject rotationToggleBtnObj;
        private RectTransform rotationArrowContainer;

        private void CreateRotationToggleButton(Canvas canvas)
        {
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float leftmostX = -hSize * 1.5f * 5f;
            float lowestY = hSize * Mathf.Sqrt(3f) * (-5f);

            // 버튼 컨테이너
            rotationToggleBtnObj = new GameObject("RotationToggleButton");
            rotationToggleBtnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = rotationToggleBtnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(leftmostX, lowestY);
            btnRt.sizeDelta = new Vector2(80f, 70f);

            Image btnBg = rotationToggleBtnObj.AddComponent<Image>();
            btnBg.color = new Color(0.2f, 0.2f, 0.3f, 0.85f);

            Button btn = rotationToggleBtnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.35f, 0.35f, 0.5f, 0.9f);
            btnColors.pressedColor = new Color(0.15f, 0.15f, 0.25f, 0.9f);
            btn.colors = btnColors;

            // 화살표 아이콘 컨테이너 (회전용)
            GameObject arrowObj = new GameObject("ArrowIcon");
            arrowObj.transform.SetParent(rotationToggleBtnObj.transform, false);
            rotationArrowContainer = arrowObj.AddComponent<RectTransform>();
            rotationArrowContainer.anchorMin = new Vector2(0.5f, 0.5f);
            rotationArrowContainer.anchorMax = new Vector2(0.5f, 0.5f);
            rotationArrowContainer.sizeDelta = new Vector2(40f, 40f);
            rotationArrowContainer.anchoredPosition = Vector2.zero;

            // 화살표 이미지 (▶ 유사 삼각형)
            Image arrowImg = arrowObj.AddComponent<Image>();
            arrowImg.color = Color.white;
            arrowImg.raycastTarget = false;

            // 초기 방향 표시 (시계 방향 = 0°, 반시계 = 180° Y 반전)
            UpdateRotationIcon();

            // 클릭 이벤트
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();

                // 회전 방향 토글
                if (inputSystem != null)
                {
                    inputSystem.ToggleRotationDirection();
                    UpdateRotationIcon();
                    Debug.Log($"[GameManager] 회전 방향 변경: {(inputSystem.IsClockwise ? "시계" : "반시계")}");
                }
            });

            rotationToggleBtnObj.transform.SetAsLastSibling();
            Debug.Log("[GameManager] 회전 방향 토글 버튼 생성 완료 (좌하단)");
        }

        private void UpdateRotationIcon()
        {
            if (rotationArrowContainer == null) return;
            bool clockwise = inputSystem != null && inputSystem.IsClockwise;
            // 시계: 기본 회전, 반시계: Y축 반전
            rotationArrowContainer.localScale = clockwise
                ? new Vector3(1f, 1f, 1f)
                : new Vector3(-1f, 1f, 1f);
        }

        // 게임오버 팝업 참조 (동적 생성)
        private GameObject gameOverPopupObj;
        private Text gameOverScoreText;
        private Text gameOverTitleText; // 타이틀 텍스트 참조 (GAME OVER / GAME END 변경용)

        // ★ 이동횟수 추가 구매 시스템
        private int continueCount = 0;           // 현재 레벨에서 이동횟수 추가 구매 횟수
        private const int CONTINUE_MOVES = 5;    // 추가 이동횟수 (고정)
        private const int CONTINUE_BASE_COST = 100; // 기본 골드 비용
        private Text continueCostText;           // 구매 버튼 비용 텍스트 참조
        private Button continueButton;           // 구매 버튼 참조
        private GameObject continueButtonObj;    // 이동횟수 추가 버튼 루트 (실패 원인별 토글)
        private GameObject supportBombingButtonObj; // 지원폭격 버튼 루트 (데드락 실패 시 표시)

        // 로비 UI 참조
        private GameObject lobbyContainer;
        private JewelsHexaPuzzle.UI.StageScrollBuilder stageScrollBuilder; // 스크롤 빌더
        private bool isLevelActivationMode = false; // 레벨 활성화 선택 모드

        // 스테이지 선택
        private int selectedStage = 1;
        public int SelectedStage => selectedStage;
        private LevelData selectedLevelData = null; // LevelRegistry에서 로드된 레벨 데이터

        // HUD 요소 참조 (로비에서 숨기기 용)
        private List<GameObject> hudElements = new List<GameObject>();
        private Text gameOverMovesText;

        /// <summary>
        /// 게임오버 팝업 프로시저럴 생성
        /// </summary>
        private void CreateGameOverPopup(Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // === 루트 컨테이너 (전체 화면 덮기) ===
            gameOverPopupObj = new GameObject("GameOverPopup");
            gameOverPopupObj.transform.SetParent(canvas.transform, false);
            RectTransform rootRt = gameOverPopupObj.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 어두운 배경 오버레이 (다크글래스 딤 — HUD 톤 통일)
            Image overlay = gameOverPopupObj.AddComponent<Image>();
            overlay.color = ClaudeTheme.PopupOverlay;
            overlay.raycastTarget = true;

            // === 중앙 패널 ===
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(gameOverPopupObj.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(500f, 480f);

            Image panelBg = panel.AddComponent<Image>();
            ClaudeTheme.ApplyPopupPanel(panelBg); // 다크글래스 팝업 패널 (HUD 통일)

            // === GAME OVER 타이틀 ===
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            titleRt.sizeDelta = new Vector2(400f, 60f);
            gameOverTitleText = titleObj.AddComponent<Text>();
            gameOverTitleText.font = font;
            gameOverTitleText.fontSize = 42;
            gameOverTitleText.alignment = TextAnchor.MiddleCenter;
            gameOverTitleText.color = ClaudeTheme.Danger;
            gameOverTitleText.raycastTarget = false;
            gameOverTitleText.text = "GAME OVER";

            // === 점수 표시 ===
            GameObject scoreObj = new GameObject("ScoreLabel");
            scoreObj.transform.SetParent(panel.transform, false);
            RectTransform scoreRt = scoreObj.AddComponent<RectTransform>();
            scoreRt.anchorMin = new Vector2(0.5f, 1f);
            scoreRt.anchorMax = new Vector2(0.5f, 1f);
            scoreRt.pivot = new Vector2(0.5f, 1f);
            scoreRt.anchoredPosition = new Vector2(0f, -110f);
            scoreRt.sizeDelta = new Vector2(400f, 36f);
            Text scoreLabelText = scoreObj.AddComponent<Text>();
            scoreLabelText.font = font;
            scoreLabelText.fontSize = 20;
            scoreLabelText.alignment = TextAnchor.MiddleCenter;
            scoreLabelText.color = ClaudeTheme.PopupTextMuted;
            scoreLabelText.raycastTarget = false;
            scoreLabelText.text = "SCORE";

            GameObject scoreValObj = new GameObject("ScoreValue");
            scoreValObj.transform.SetParent(panel.transform, false);
            RectTransform scoreValRt = scoreValObj.AddComponent<RectTransform>();
            scoreValRt.anchorMin = new Vector2(0.5f, 1f);
            scoreValRt.anchorMax = new Vector2(0.5f, 1f);
            scoreValRt.pivot = new Vector2(0.5f, 1f);
            scoreValRt.anchoredPosition = new Vector2(0f, -145f);
            scoreValRt.sizeDelta = new Vector2(400f, 50f);
            gameOverScoreText = scoreValObj.AddComponent<Text>();
            gameOverScoreText.font = font;
            gameOverScoreText.fontSize = 36;
            gameOverScoreText.alignment = TextAnchor.MiddleCenter;
            gameOverScoreText.color = new Color(1f, 0.84f, 0f);
            gameOverScoreText.raycastTarget = false;
            gameOverScoreText.text = "0";

            // === 이동 횟수 표시 (베스트 스코어 아래 배치) ===
            GameObject movesObj = new GameObject("MovesLabel");
            movesObj.transform.SetParent(panel.transform, false);
            RectTransform movesRt = movesObj.AddComponent<RectTransform>();
            movesRt.anchorMin = new Vector2(0.5f, 1f);
            movesRt.anchorMax = new Vector2(0.5f, 1f);
            movesRt.pivot = new Vector2(0.5f, 1f);
            movesRt.anchoredPosition = new Vector2(0f, -320f);
            movesRt.sizeDelta = new Vector2(400f, 36f);
            gameOverMovesText = movesObj.AddComponent<Text>();
            gameOverMovesText.font = font;
            gameOverMovesText.fontSize = 22;
            gameOverMovesText.alignment = TextAnchor.MiddleCenter;
            gameOverMovesText.color = ClaudeTheme.PopupTextMuted;
            gameOverMovesText.raycastTarget = false;
            gameOverMovesText.text = "MOVES: 0";

            // === ★ 이동횟수 추가 구매 버튼 ===
            GameObject continueObj = new GameObject("ContinueButton");
            continueObj.transform.SetParent(panel.transform, false);
            RectTransform continueRt = continueObj.AddComponent<RectTransform>();
            continueRt.anchorMin = new Vector2(0.5f, 0f);
            continueRt.anchorMax = new Vector2(0.5f, 0f);
            continueRt.pivot = new Vector2(0.5f, 0f);
            continueRt.anchoredPosition = new Vector2(0f, 100f);
            continueRt.sizeDelta = new Vector2(360f, 70f);

            Image continueBg = continueObj.AddComponent<Image>();
            ClaudeTheme.StylePopupPrimary(null, continueBg); // 골드 글래스 주버튼

            continueButton = continueObj.AddComponent<Button>();
            var continueColors = continueButton.colors;
            continueColors.normalColor = Color.white;
            continueColors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            continueColors.pressedColor = new Color(0.92f, 0.90f, 0.86f, 1f);
            continueColors.disabledColor = new Color(0.8f, 0.78f, 0.74f, 1f);
            continueButton.colors = continueColors;

            GameObject continueTextObj = new GameObject("ContinueText");
            continueTextObj.transform.SetParent(continueObj.transform, false);
            RectTransform continueTextRt = continueTextObj.AddComponent<RectTransform>();
            continueTextRt.anchorMin = Vector2.zero;
            continueTextRt.anchorMax = Vector2.one;
            continueTextRt.offsetMin = Vector2.zero;
            continueTextRt.offsetMax = Vector2.zero;
            continueCostText = continueTextObj.AddComponent<Text>();
            continueCostText.font = font;
            continueCostText.fontSize = 24;
            continueCostText.alignment = TextAnchor.MiddleCenter;
            continueCostText.color = ClaudeTheme.PopupPrimaryLabel;
            continueCostText.raycastTarget = false;
            continueCostText.text = $"+{CONTINUE_MOVES} 이동횟수  🪙 {CONTINUE_BASE_COST}G";

            Outline continueBtnOutline = continueTextObj.AddComponent<Outline>();
            continueBtnOutline.effectColor = new Color(0.3f, 0.2f, 0f, 0.8f);
            continueBtnOutline.effectDistance = new Vector2(1, -1);

            continueButton.onClick.AddListener(() =>
            {
                int cost = GetContinueCost();
                if (SpendGold(cost))
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                    continueCount++;
                    // 이동횟수 추가 + 게임 재개
                    currentTurns += CONTINUE_MOVES;
                    OnTurnChanged?.Invoke(currentTurns);
                    UpdateUI();
                    // 팝업 닫고 Playing 상태로 복귀
                    gameOverPopupObj.SetActive(false);
                    Time.timeScale = 1f;
                    SetGameState(GameState.Playing);
                    if (inputSystem != null) inputSystem.SetEnabled(true);
                    Debug.Log($"[GameManager] 이동횟수 +{CONTINUE_MOVES} 구매! 비용={cost}G, 누적 구매={continueCount}회, 남은 턴={currentTurns}");
                }
                else
                {
                    // 골드 부족 피드백
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayWarningBeep();
                    Debug.Log($"[GameManager] 골드 부족! 필요={cost}G, 보유={currentGold}G");
                    StartCoroutine(GoldInsufficientFeedback(continueObj));
                }
            });
            continueButtonObj = continueObj;

            // === ★ 지원폭격 버튼 (데드락 실패 전용 — 이동횟수 버튼과 같은 자리, 토글) ===
            GameObject bombingObj = new GameObject("SupportBombingButton");
            bombingObj.transform.SetParent(panel.transform, false);
            RectTransform bombingRt = bombingObj.AddComponent<RectTransform>();
            bombingRt.anchorMin = new Vector2(0.5f, 0f);
            bombingRt.anchorMax = new Vector2(0.5f, 0f);
            bombingRt.pivot = new Vector2(0.5f, 0f);
            bombingRt.anchoredPosition = new Vector2(0f, 100f);
            bombingRt.sizeDelta = new Vector2(360f, 70f);

            Image bombingBg = bombingObj.AddComponent<Image>();
            ClaudeTheme.StylePopupPrimary(null, bombingBg); // 골드 글래스 주버튼

            Button bombingButton = bombingObj.AddComponent<Button>();
            var bombingColors = bombingButton.colors;
            bombingColors.normalColor = Color.white;
            bombingColors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            bombingColors.pressedColor = new Color(0.92f, 0.90f, 0.86f, 1f);
            bombingColors.disabledColor = new Color(0.8f, 0.78f, 0.74f, 1f);
            bombingButton.colors = bombingColors;

            GameObject bombingTextObj = new GameObject("SupportBombingText");
            bombingTextObj.transform.SetParent(bombingObj.transform, false);
            RectTransform bombingTextRt = bombingTextObj.AddComponent<RectTransform>();
            bombingTextRt.anchorMin = Vector2.zero;
            bombingTextRt.anchorMax = Vector2.one;
            bombingTextRt.offsetMin = Vector2.zero;
            bombingTextRt.offsetMax = Vector2.zero;
            Text bombingText = bombingTextObj.AddComponent<Text>();
            bombingText.font = font;
            bombingText.fontSize = 24;
            bombingText.alignment = TextAnchor.MiddleCenter;
            bombingText.color = ClaudeTheme.PopupPrimaryLabel;
            bombingText.raycastTarget = false;
            bombingText.text = $"지원폭격  🪙 {SUPPORT_BOMBING_COST}G";

            Outline bombingOutline = bombingTextObj.AddComponent<Outline>();
            bombingOutline.effectColor = new Color(0.3f, 0.2f, 0f, 0.8f);
            bombingOutline.effectDistance = new Vector2(1, -1);

            bombingButton.onClick.AddListener(() =>
            {
                if (isSupportBombing) return; // 중복 클릭 방지
                if (SpendGold(SUPPORT_BOMBING_COST))
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                    Debug.Log($"[GameManager] 지원폭격 발동! 비용={SUPPORT_BOMBING_COST}G, 잔액={currentGold}G");
                    StartCoroutine(SupportBombingCoroutine());
                }
                else
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayWarningBeep();
                    Debug.Log($"[GameManager] 골드 부족! 지원폭격 필요={SUPPORT_BOMBING_COST}G, 보유={currentGold}G");
                    StartCoroutine(GoldInsufficientFeedback(bombingObj));
                }
            });
            supportBombingButtonObj = bombingObj;
            bombingObj.SetActive(false); // 기본 숨김 — GameOver(reason)에서 데드락일 때만 표시

            // === 나가기 버튼 ===
            GameObject exitObj = new GameObject("ExitButton");
            exitObj.transform.SetParent(panel.transform, false);
            RectTransform exitRt = exitObj.AddComponent<RectTransform>();
            exitRt.anchorMin = new Vector2(0.5f, 0f);
            exitRt.anchorMax = new Vector2(0.5f, 0f);
            exitRt.pivot = new Vector2(0.5f, 0f);
            exitRt.anchoredPosition = new Vector2(0f, 25f);
            exitRt.sizeDelta = new Vector2(360f, 60f);

            Image exitBg = exitObj.AddComponent<Image>();
            ClaudeTheme.StylePopupSecondary(null, exitBg); // 다크글래스 보조버튼

            Button exitBtn = exitObj.AddComponent<Button>();
            var exitColors = exitBtn.colors;
            exitColors.normalColor = Color.white;
            exitColors.highlightedColor = Color.white;
            exitColors.pressedColor = new Color(0.92f, 0.90f, 0.86f, 1f);
            exitBtn.colors = exitColors;

            GameObject exitTextObj = new GameObject("ExitText");
            exitTextObj.transform.SetParent(exitObj.transform, false);
            RectTransform exitTextRt = exitTextObj.AddComponent<RectTransform>();
            exitTextRt.anchorMin = Vector2.zero;
            exitTextRt.anchorMax = Vector2.one;
            exitTextRt.offsetMin = Vector2.zero;
            exitTextRt.offsetMax = Vector2.zero;
            Text exitText = exitTextObj.AddComponent<Text>();
            exitText.font = font;
            exitText.fontSize = 24;
            exitText.alignment = TextAnchor.MiddleCenter;
            exitText.color = ClaudeTheme.PopupSecondaryLabel; // 다크 버튼 → 라이트 텍스트
            exitText.raycastTarget = false;
            exitText.text = "나가기";

            exitBtn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                gameOverPopupObj.SetActive(false);
                // ★ ExitToLobby와 동일한 시스템 리셋 수행 (감사 M15)
                //   — 생략 시 런 스킬(SkillTree)/MP/몬스터 소환 상태가 다음 게임에 이월된다.
                Time.timeScale = 1f;
                ForceResetAllGameSystems();
                if (scoreManager != null) scoreManager.ResetScore();
                if (MPManager.Instance != null) MPManager.Instance.ResetMPSilent();
                if (MonsterSpawnController.Instance != null) MonsterSpawnController.Instance.Reset();
                if (SkillTreeManager.Instance != null) SkillTreeManager.Instance.ResetRunSkills(); // 런별: 로비 이탈 시 리워드 스킬 초기화
                JewelsHexaPuzzle.Data.GemTypeHelper.AllowedColorsOverride = null;
                JewelsHexaPuzzle.Data.GemTypeHelper.ActiveGemTypeCount = 5;
                JewelsHexaPuzzle.Data.MissionTargetColors.Clear(); // 미션 타겟 강조 잔존 방지
                if (Stage1AssistSystem.Instance != null) Stage1AssistSystem.Instance.Disable();
                ShowLobby();
            });

            // UIManager에 연결
            if (uiManager != null)
                uiManager.SetGameOverPopup(gameOverPopupObj);

            // 초기 비활성화
            gameOverPopupObj.SetActive(false);
            Debug.Log("[GameManager] 게임오버 팝업 생성 완료");
        }

        /// <summary>
        /// 게임오버 팝업 카운팅 애니메이션
        /// 0에서 최종 점수까지 숫자가 돌아가는 연출
        /// </summary>
        private IEnumerator AnimateGameOverPopup()
        {
            // 팝업 등장 대기
            yield return new WaitForSecondsRealtime(0.5f);

            // ★ 실패 화면에도 현재 누적 점수를 표시 (사용자 요청: SCORE 수치 표시)
            int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
            int finalMoves = currentGameMode == GameMode.Infinite ? rotationCount : currentTurns;

            // 이동 횟수 카운팅 (0.4초)
            if (gameOverMovesText != null)
            {
                float movesDuration = 0.4f;
                float movesElapsed = 0f;
                while (movesElapsed < movesDuration)
                {
                    movesElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(movesElapsed / movesDuration);
                    float eased = t * t * (3f - 2f * t); // SmoothStep
                    int currentMoves = Mathf.RoundToInt(Mathf.Lerp(0, finalMoves, eased));
                    gameOverMovesText.text = $"MOVES: {currentMoves}";
                    yield return null;
                }
                gameOverMovesText.text = $"MOVES: {finalMoves}";
            }

            yield return new WaitForSecondsRealtime(0.2f);

            // 점수 카운팅 (0.8초, 가속→감속 이징)
            if (gameOverScoreText != null && finalScore > 0)
            {
                float scoreDuration = 0.8f;
                float scoreElapsed = 0f;
                Color normalColor = new Color(1f, 0.84f, 0f); // 금색
                Color flashColor = Color.white;

                while (scoreElapsed < scoreDuration)
                {
                    scoreElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(scoreElapsed / scoreDuration);
                    // EaseOutQuart: 빠르게 올라가다 느려지는 곡선
                    float eased = 1f - Mathf.Pow(1f - t, 4f);
                    int currentScore = Mathf.RoundToInt(Mathf.Lerp(0, finalScore, eased));
                    gameOverScoreText.text = string.Format("{0:N0}", currentScore);

                    // 카운팅 중 색상 플래시
                    float colorT = Mathf.PingPong(scoreElapsed * 6f, 1f);
                    gameOverScoreText.color = Color.Lerp(normalColor, flashColor, colorT * 0.3f);

                    yield return null;
                }

                gameOverScoreText.text = string.Format("{0:N0}", finalScore);
                gameOverScoreText.color = normalColor;

                // 최종 스케일 펀치
                RectTransform scoreRt = gameOverScoreText.GetComponent<RectTransform>();
                if (scoreRt != null)
                {
                    float punchDuration = 0.25f;
                    float punchElapsed = 0f;
                    while (punchElapsed < punchDuration)
                    {
                        punchElapsed += Time.unscaledDeltaTime;
                        float pt = Mathf.Clamp01(punchElapsed / punchDuration);
                        float scale;
                        if (pt < 0.3f)
                            scale = Mathf.Lerp(1f, 1.25f, pt / 0.3f);
                        else
                            scale = Mathf.Lerp(1.25f, 1f, (pt - 0.3f) / 0.7f);
                        scoreRt.localScale = Vector3.one * scale;
                        yield return null;
                    }
                    scoreRt.localScale = Vector3.one;
                }
            }
            else if (gameOverScoreText != null)
            {
                gameOverScoreText.text = "0";
            }

            // 최고 점수 정보 표시 (스테이지 실패 → 베스트 미저장이므로 NEW RECORD 미표시)
            yield return new WaitForSecondsRealtime(0.3f);
            ShowGameOverHighScore(finalScore, false);
        }

        /// <summary>
        /// 게임오버 타이틀 텍스트/색상 변경 (무한도전: GAME END / 스테이지: GAME OVER)
        /// </summary>
        private void UpdateGameOverTitle(string title, Color color)
        {
            if (gameOverTitleText != null)
            {
                gameOverTitleText.text = title;
                gameOverTitleText.color = color;
            }
        }

        /// <summary>
        /// 무한도전 전용 게임 종료 팝업 애니메이션
        /// 실제 점수를 카운팅하고 베스트 스코어를 표시
        /// </summary>
        private IEnumerator AnimateGameOverPopupInfinite(int finalScore)
        {
            // 팝업 등장 대기
            yield return new WaitForSecondsRealtime(0.5f);

            int finalMoves = rotationCount;

            // 이동 횟수 카운팅 (0.4초)
            if (gameOverMovesText != null)
            {
                float movesDuration = 0.4f;
                float movesElapsed = 0f;
                while (movesElapsed < movesDuration)
                {
                    movesElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(movesElapsed / movesDuration);
                    float eased = t * t * (3f - 2f * t);
                    int currentMoves = Mathf.RoundToInt(Mathf.Lerp(0, finalMoves, eased));
                    gameOverMovesText.text = $"MOVES: {currentMoves}";
                    yield return null;
                }
                gameOverMovesText.text = $"MOVES: {finalMoves}";
            }

            yield return new WaitForSecondsRealtime(0.2f);

            // 점수 카운팅 (1.0초, EaseOutQuart)
            if (gameOverScoreText != null && finalScore > 0)
            {
                float scoreDuration = 1.0f;
                float scoreElapsed = 0f;
                Color normalColor = new Color(1f, 0.84f, 0f); // 금색
                Color flashColor = Color.white;

                while (scoreElapsed < scoreDuration)
                {
                    scoreElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(scoreElapsed / scoreDuration);
                    float eased = 1f - Mathf.Pow(1f - t, 4f);
                    int currentScore = Mathf.RoundToInt(Mathf.Lerp(0, finalScore, eased));
                    gameOverScoreText.text = string.Format("{0:N0}", currentScore);

                    // 카운팅 중 색상 플래시
                    float colorT = Mathf.PingPong(scoreElapsed * 6f, 1f);
                    gameOverScoreText.color = Color.Lerp(normalColor, flashColor, colorT * 0.3f);

                    yield return null;
                }

                gameOverScoreText.text = string.Format("{0:N0}", finalScore);
                gameOverScoreText.color = normalColor;

                // 최종 스케일 펀치
                RectTransform scoreRt = gameOverScoreText.GetComponent<RectTransform>();
                if (scoreRt != null)
                {
                    float punchDuration = 0.25f;
                    float punchElapsed = 0f;
                    while (punchElapsed < punchDuration)
                    {
                        punchElapsed += Time.unscaledDeltaTime;
                        float pt = Mathf.Clamp01(punchElapsed / punchDuration);
                        float scale = pt < 0.3f
                            ? Mathf.Lerp(1f, 1.3f, pt / 0.3f)
                            : Mathf.Lerp(1.3f, 1f, (pt - 0.3f) / 0.7f);
                        scoreRt.localScale = Vector3.one * scale;
                        yield return null;
                    }
                    scoreRt.localScale = Vector3.one;
                }
            }
            else if (gameOverScoreText != null)
            {
                gameOverScoreText.text = "0";
            }

            // 베스트 스코어 표시
            yield return new WaitForSecondsRealtime(0.3f);
            ShowGameOverHighScore(finalScore);
        }

        /// <summary>
        /// 게임오버 팝업에 최고 점수 정보 표시
        /// </summary>
        private void ShowGameOverHighScore(int currentScore, bool recordEligible = true)
        {
            if (gameOverPopupObj == null || scoreManager == null) return;

            // 반복 게임오버/지원폭격 재실패 시 중복 누적 방지 — 기존 표시 제거 후 재생성
            var existingHs = gameOverPopupObj.transform.Find("GameOverHighScore");
            if (existingHs != null) Destroy(existingHs.gameObject);

            int levelBest = scoreManager.GetLevelHighScore(selectedStage);
            int personalBest = scoreManager.GetPersonalLevelBest(selectedStage);
            // 실패 화면(recordEligible=false)에선 베스트가 저장되지 않으므로 NEW RECORD 표시 금지
            bool isNewLevelBest = recordEligible && currentScore >= levelBest && currentScore > 0;
            bool isNewPersonalBest = recordEligible && currentScore >= personalBest && currentScore > 0;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 최고 점수 컨테이너 (점수 아래, 무브수 위에 배치)
            GameObject hsObj = new GameObject("GameOverHighScore");
            hsObj.transform.SetParent(gameOverPopupObj.transform, false);
            RectTransform hsRt = hsObj.AddComponent<RectTransform>();
            hsRt.anchoredPosition = new Vector2(0f, -30f);
            hsRt.sizeDelta = new Vector2(350f, 70f);

            // 레벨 최고 점수 (모든 유저 통합)
            GameObject lbObj = new GameObject("LevelBestText");
            lbObj.transform.SetParent(hsObj.transform, false);
            RectTransform lbRt = lbObj.AddComponent<RectTransform>();
            lbRt.anchoredPosition = new Vector2(0f, 15f);
            lbRt.sizeDelta = new Vector2(350f, 28f);
            Text lbText = lbObj.AddComponent<Text>();
            lbText.font = font;
            lbText.fontSize = 18;
            lbText.fontStyle = FontStyle.Bold;
            lbText.alignment = TextAnchor.MiddleCenter;
            lbText.raycastTarget = false;
            lbText.color = isNewLevelBest ? new Color(1f, 1f, 0.3f) : new Color(1f, 0.85f, 0.3f, 0.9f);
            string levelBestStr = isNewLevelBest ? "NEW RECORD!" : string.Format("{0:N0}", levelBest);
            lbText.text = string.Format("BEST: {0}", levelBestStr);
            Outline lbOutline = lbObj.AddComponent<Outline>();
            lbOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            lbOutline.effectDistance = new Vector2(1, 1);

            // 개인 레벨별 최고 점수
            GameObject pbObj = new GameObject("PersonalBestText");
            pbObj.transform.SetParent(hsObj.transform, false);
            RectTransform pbRt = pbObj.AddComponent<RectTransform>();
            pbRt.anchoredPosition = new Vector2(0f, -15f);
            pbRt.sizeDelta = new Vector2(350f, 28f);
            Text pbText = pbObj.AddComponent<Text>();
            pbText.font = font;
            pbText.fontSize = 16;
            pbText.alignment = TextAnchor.MiddleCenter;
            pbText.raycastTarget = false;
            pbText.color = isNewPersonalBest ? new Color(0.5f, 1f, 0.5f) : new Color(0.7f, 0.9f, 1f, 0.9f);
            string personalBestStr = isNewPersonalBest ? "NEW RECORD!" : string.Format("{0:N0}", personalBest);
            pbText.text = string.Format("MY BEST: {0}", personalBestStr);
            Outline pbOutline = pbObj.AddComponent<Outline>();
            pbOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            pbOutline.effectDistance = new Vector2(1, 1);
        }

        /// <summary>
        /// 워치독: Processing 상태가 너무 오래 지속되면 강제 복구
        /// </summary>
        private void Update()
        {
            if (currentState == GameState.Processing && !isPaused)
            {
                // ★ 하드 캡 (감사 H7): 시스템 플래그가 "활성"을 주장해도 Processing이
                //   90초(unscaled) 이상 지속되면 무조건 강제 복구.
                //   IsProcessingTurn 등 플래그 누수 시 워치독 타이머가 무한 리셋되어
                //   복구 코드가 영원히 도달 불가해지는 catch-22의 최후 안전망.
                //   unscaled 기준이라 timeScale=0 잔류 시에도 만료된다.
                if (Time.unscaledTime - hardProcessingStartTime > 90f)
                {
                    Debug.LogError($"[GameManager] HARD CAP! Processing {Time.unscaledTime - hardProcessingStartTime:F0}s (unscaled) 초과 — 플래그 무시 강제 복구");
                    if (goblinSystem != null) goblinSystem.ForceClearProcessingTurn();
                    if (Time.timeScale <= 0f) Time.timeScale = 1f; // timeScale 잔류 복구
                    hardProcessingStartTime = Time.unscaledTime;   // 재진입 방지
                    ForceRecoverFromStuck();
                    return;
                }

                float elapsed = Time.time - processingStartTime;

                if (isInPostRecovery)
                {
                    // PostRecovery 자체가 15초 이상 걸리면 강제 복구
                    if (elapsed > 15f)
                    {
                        Debug.LogError($"[GameManager] PostRecovery timeout after {elapsed:F1}s! Force resetting to Playing.");
                        StopAllCoroutines();
                        // ★ HitStop 중도 사망으로 인한 timeScale 잔류 복구 (감사 M13)
                        if (Time.timeScale < 1f && !JewelsHexaPuzzle.Core.VisualConstants.IsGamePausedExternally())
                            Time.timeScale = 1f;
                        isInPostRecovery = false;
                        isProcessingChainDrill = false;
                        if (blockRemovalSystem != null) blockRemovalSystem.ForceReset();
                        if (drillSystem != null) drillSystem.ForceReset();
                        if (bombSystem != null) bombSystem.ForceReset();
                        if (donutSystem != null) donutSystem.ForceReset();
                        if (xBlockSystem != null) xBlockSystem.ForceReset();

            if (droneSystem != null) droneSystem.ForceReset();
                        SetGameState(GameState.Playing);
                        if (inputSystem != null) inputSystem.SetEnabled(true);
                    }
                }
                else
                {
                    if (elapsed > STUCK_TIMEOUT)
                    {
                        // BRS 또는 특수 블록 시스템이 활발히 처리 중이면 stuck이 아님 — 타이머 리셋
                        bool systemsActive = (blockRemovalSystem != null && blockRemovalSystem.IsProcessing)
                            || (drillSystem != null && drillSystem.IsDrilling)
                            || (bombSystem != null && bombSystem.IsBombing)
                            || (donutSystem != null && donutSystem.IsActivating)
                            || (xBlockSystem != null && xBlockSystem.IsActivating)
                            || (droneSystem != null && droneSystem.IsActivating)
                            // ★ GoblinSystem.ProcessTurn 진행 중이면 stuck 아님 (몬스터 액션 phase 정상 진행)
                            || (goblinSystem != null && goblinSystem.IsProcessingTurn)
                            // ProcessSpecialBlockAftermath 코루틴이 최근 5초 내 진행 보고가 있으면 활성으로 간주
                            || (isProcessingChainDrill && Time.time - lastAftermathProgressTime < 5f);

                        if (systemsActive)
                        {
                            Debug.Log($"[GameManager] Processing {elapsed:F1}s but systems still active. Resetting stuck timer.");
                            processingStartTime = Time.time;
                        }
                        else
                        {
                            Debug.LogWarning($"[GameManager] STUCK DETECTED! Processing for {elapsed:F1}s. Force recovering...");
                            Debug.LogWarning($"[GameManager] Flags: isProcessingChainDrill={isProcessingChainDrill}, BRS.IsProcessing={blockRemovalSystem?.IsProcessing}, " +
                                $"isInPostRecovery={isInPostRecovery}, isItemAction={isItemAction}");
                            Debug.LogWarning($"[GameManager] Systems: Drilling={drillSystem?.IsDrilling}, Bombing={bombSystem?.IsBombing}, " +
                                $"Donut={donutSystem?.IsActivating}, XBlock={xBlockSystem?.IsActivating}, Drone={droneSystem?.IsActivating}");
                            Debug.LogWarning($"[GameManager] Aftermath: lastProgress={Time.time - lastAftermathProgressTime:F1}s ago, " +
                                $"Rotating={rotationSystem?.IsRotating}, InputEnabled={inputSystem?.IsEnabled}");
                            Debug.LogWarning($"[GameManager] Tutorial: active={TutorialManager.Instance?.IsTutorialActive}, paused={TutorialManager.Instance?.IsPausedForTutorial}");
                            ForceRecoverFromStuck();
                        }
                    }
                }
            }

            // StageClear 상태 워치독: 클리어 시퀀스가 60초 이상 정지하면 강제 팝업 표시
            if (currentState == GameState.StageClear && !isPaused)
            {
                float elapsed = Time.time - processingStartTime;
                if (elapsed > 60f)
                {
                    Debug.LogWarning($"[GameManager] StageClear sequence timeout after {elapsed:F1}s! Force showing popup.");
                    StopAllCoroutines();
                    // ★ HitStop 중도 사망으로 인한 timeScale 잔류 복구 (감사 M13)
                    if (Time.timeScale < 1f && !JewelsHexaPuzzle.Core.VisualConstants.IsGamePausedExternally())
                        Time.timeScale = 1f;
                    isProcessingChainDrill = false;
                    if (blockRemovalSystem != null) blockRemovalSystem.ForceReset();
                    if (drillSystem != null) drillSystem.ForceReset();
                    if (bombSystem != null) bombSystem.ForceReset();
                    if (donutSystem != null) donutSystem.ForceReset();
                    if (xBlockSystem != null) xBlockSystem.ForceReset();
            if (droneSystem != null) droneSystem.ForceReset();

                    // 재진입 방지 (매 프레임 반복 호출 차단)
                    processingStartTime = float.MaxValue;

                    // 강제 클리어 팝업 표시
                    if (uiManager != null)
                        uiManager.ShowStageClearPopup(0);
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

            // ★ timeScale 잔류 복구 (감사 M13) — HitStop 코루틴이 StopAllCoroutines/ForceReset으로
            //   중도 사망하면 timeScale이 0 또는 슬로모 값으로 영구 잔류한다.
            //   모달/퍼즈가 의도적으로 0을 유지 중인 경우는 건드리지 않는다.
            if (Time.timeScale < 1f && !JewelsHexaPuzzle.Core.VisualConstants.IsGamePausedExternally())
                Time.timeScale = 1f;

            // 모든 플래그 리셋
            isProcessingChainDrill = false;
            isInPostRecovery = false;
            lastAftermathProgressTime = 0f;

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
            if (droneSystem != null) droneSystem.ForceReset();

            // ★ GoblinSystem stuck 복구: ProcessTurn이 갇혀있는 IsProcessingTurn=true 플래그 강제 리셋
            //    (없으면 InputSystem이 영원히 "몬스터 액션중입니다!" 토스트 + 회전 차단)
            if (goblinSystem != null) goblinSystem.ForceClearProcessingTurn();

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

            // 튜토리얼 pause 상태도 해제 (BRS가 무한 대기에 빠지는 것 방지)
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsPausedForTutorial)
            {
                Debug.LogWarning("[GameManager] ForceRecover: TutorialManager pause 해제");
                TutorialManager.Instance.ForceUnpause();
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
            if (bombSystem == null)
            {
                bombSystem = gameObject.AddComponent<BombBlockSystem>();
                Debug.LogWarning("[GameManager] BombBlockSystem이 씬에 없어 자동 생성됨");
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

            if (droneSystem == null)
            {
                droneSystem = FindObjectOfType<DroneBlockSystem>();
                if (droneSystem == null)
                {
                    GameObject droneObj = new GameObject("DroneBlockSystem");
                    droneSystem = droneObj.AddComponent<DroneBlockSystem>();
                    Debug.Log("[GameManager] DroneBlockSystem auto-created");
                }
                else
                    Debug.Log("[GameManager] DroneBlockSystem auto-found");
            }

            // 특수 블록 합성 시스템 초기화
            var comboSystem = FindObjectOfType<SpecialBlockComboSystem>();
            if (comboSystem == null)
            {
                GameObject comboObj = new GameObject("SpecialBlockComboSystem");
                comboSystem = comboObj.AddComponent<SpecialBlockComboSystem>();
                Debug.Log("[GameManager] SpecialBlockComboSystem auto-created");
            }
            comboSystem.Initialize(hexGrid);

            if (enemySystem == null)
            {
                enemySystem = FindObjectOfType<EnemySystem>();
                if (enemySystem == null)
                {
                    // 씬에 없으면 자동 생성
                    GameObject esObj = new GameObject("EnemySystem");
                    enemySystem = esObj.AddComponent<EnemySystem>();
                    Debug.Log("[GameManager] EnemySystem auto-created");
                }
                else
                {
                    Debug.Log("[GameManager] EnemySystem auto-found");
                }
            }

            // 고블린 시스템 자동 생성/찾기
            if (goblinSystem == null)
            {
                goblinSystem = FindObjectOfType<GoblinSystem>();
                if (goblinSystem == null)
                {
                    GameObject gsObj = new GameObject("GoblinSystem");
                    goblinSystem = gsObj.AddComponent<GoblinSystem>();
                    Debug.Log("[GameManager] GoblinSystem auto-created");
                }
                else
                {
                    Debug.Log("[GameManager] GoblinSystem auto-found");
                }
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

            if (missionSystem == null)
            {
                missionSystem = FindObjectOfType<MissionSystem>();
                if (missionSystem == null)
                {
                    GameObject msObj = new GameObject("MissionSystem");
                    missionSystem = msObj.AddComponent<MissionSystem>();
                    Debug.Log("[GameManager] MissionSystem auto-created");
                }
                else
                {
                    Debug.Log("[GameManager] MissionSystem auto-found");
                }
            }
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

            if (droneSystem != null)
                droneSystem.OnDroneComplete += OnSpecialBlockCompleted;

            // 튜토리얼 이벤트 연결
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnTutorialStarted += OnTutorialStarted;
                TutorialManager.Instance.OnTutorialEnded += OnTutorialEnded;
                TutorialManager.Instance.OnFeatureUnlocked += OnFeatureUnlocked;
            }

            // 스테이지 관리자 이벤트 연결 (Mission 1 미션 진행도)
            if (stageManager != null)
            {
                // Stage 모드는 나중에 StartGameCoroutine에서 별도로 등록
                // Infinite 모드만 여기서 등록
                if (currentGameMode != GameMode.Stage && uiManager != null)
                {
                    stageManager.OnMissionProgressUpdated += HandleInfiniteMissionProgressUpdated;
                }
                stageManager.OnMissionComplete += HandleMissionComplete;
            }

            // 미션 시스템 이벤트 연결
            if (missionSystem != null)
            {
                if (blockRemovalSystem != null)
                {
                    // OnGemsRemovedDetailed 구독 제거됨 — OnSingleGemDestroyedForMission으로 통일
                    blockRemovalSystem.OnSpecialBlockCreated += missionSystem.OnSpecialBlockCreated;
                    blockRemovalSystem.OnSpecialBlockUsed += missionSystem.OnSpecialBlockUsed;
                    // 보석 날아가기 연출 → UIManager
                    if (uiManager != null)
                        blockRemovalSystem.OnGemCollectedVisual += uiManager.SpawnGemFlyEffect;
                }
                if (scoreManager != null)
                {
                    scoreManager.OnScoreChanged += missionSystem.OnScoreChanged;
                    scoreManager.OnComboChanged += missionSystem.OnComboReached;
                }
                missionSystem.OnMissionCompleted += OnSurvivalMissionCompleted;
                missionSystem.OnMissionAssigned += OnSurvivalMissionAssigned;
                missionSystem.OnMissionProgressChanged += OnSurvivalMissionProgressChanged;
            }

            // StageManager에 특수 블록 생성 이벤트 연결 (스테이지 미션 진행용)
            if (stageManager != null && blockRemovalSystem != null)
            {
                blockRemovalSystem.OnSpecialBlockCreated += HandleSpecialBlockCreatedForStage;
            }

            // Stage 모드 이벤트 구독은 StartGameCoroutine에서 처리 (currentGameMode가 설정된 후)

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

            // HammerItem은 Start()에서 생성 (Canvas 완전 초기화 후)
        }

        // ============================================================
        // 아이템 버튼 공통 (육각형, 블록 대비 110% 크기, 지그재그 가로 배치)
        // ============================================================
        private const float ITEM_BTN_SIZE = 110f;
        private const float ITEM_BTN_GAP = 5f;
        private static readonly Color HAMMER_BTN_COLOR = new Color(0.0f, 0.70f, 0.70f, 0.92f);
        private static readonly Color SWAP_BTN_COLOR = new Color(0.85f, 0.35f, 0.65f, 0.92f);
        private static readonly Color LINEDRAW_BTN_COLOR = new Color(0.35f, 0.55f, 0.35f, 0.92f);
        private static readonly Color REVERSE_BTN_COLOR = new Color(0.60f, 0.40f, 0.80f, 0.92f);
        private static readonly Color GOLD_BTN_COLOR = new Color(0.85f, 0.65f, 0.10f, 0.92f);

        private Vector2 GetItemButtonPosition(int index)
        {
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float s = ITEM_BTN_SIZE / 2f; // 버튼 hexSize
            float gap = ITEM_BTN_GAP;
            float bs = s + gap / 1.5f; // gap 보정된 버튼 사이즈
            // flat-top axial 좌표 변환: x = bs*1.5*q, y = -(bs*sqrt(3)*(r + q/2))
            // 배치:
            //   [스왑(-1,1)]  [망치(0,0)]
            //      [라인(0,1)]
            //   [역회전(-1,2)]
            float sqrt3 = Mathf.Sqrt(3f);
            // 망치 (q=0, r=0)
            float x0 = 0f, y0 = 0f;
            // 스왑 (q=-1, r=1): x = bs*1.5*(-1) = -bs*1.5, y = -(bs*sqrt3*(1 + (-1)/2)) = -(bs*sqrt3*0.5)
            float x1 = -bs * 1.5f, y1 = -(bs * sqrt3 * 0.5f);
            // 라인 (q=0, r=1): x = 0, y = -(bs*sqrt3*1)
            float x2 = 0f, y2 = -(bs * sqrt3);
            // 역회전 (q=-1, r=2): x = -bs*1.5, y = -(bs*sqrt3*(2 + (-1)/2)) = -(bs*sqrt3*1.5) → 스왑 아래
            float x3 = -bs * 1.5f, y3 = -(bs * sqrt3 * 1.5f);
            // 무게중심 (4개 기준)
            float cx = (x0 + x1 + x2 + x3) / 4f;
            float cy = (y0 + y1 + y2 + y3) / 4f;
            // 그리드 중앙~오른쪽 끝 중간 + 100px 오른쪽 (기존 50 + 추가 50)
            float gridRight = hSize * 1.5f * 5f;
            float midX = gridRight / 2f + 80f;
            float btnHexH = ITEM_BTN_SIZE * sqrt3 / 2f;
            float baseY = hSize * sqrt3 * (-5f) - btnHexH * 0.3f - 5f - 100f;
            // 사용자 요청: 아이템 버튼 4개 전체를 오른쪽 20px, 아래 15px 이동 (down = -y)
            float offX = midX - cx + 20f;
            float offY = baseY - cy - 15f;
            if (index == 0) // 망치
                return new Vector2(x0 + offX, y0 + offY);
            else if (index == 1) // 스왑
                return new Vector2(x1 + offX, y1 + offY);
            else if (index == 2) // 라인
                return new Vector2(x2 + offX, y2 + offY);
            else // 역회전
                return new Vector2(x3 + offX, y3 + offY);
        }

        private (GameObject btnObj, Button btn, Image btnImage) CreateHexItemButton(
            Canvas canvas, string name, Vector2 position,
            Color bgColor, Color highlightColor, Color pressedColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = position;
            btnRt.sizeDelta = new Vector2(ITEM_BTN_SIZE, ITEM_BTN_SIZE);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.sprite = HexBlock.GetHexFlashSprite();
            btnImage.type = Image.Type.Simple;
            btnImage.preserveAspect = true;
            btnImage.color = bgColor;
            // 아웃라인
            GameObject outlineObj = new GameObject(name + "Outline");
            outlineObj.transform.SetParent(btnObj.transform, false);
            RectTransform outRt = outlineObj.AddComponent<RectTransform>();
            outRt.anchorMin = Vector2.zero; outRt.anchorMax = Vector2.one;
            outRt.offsetMin = Vector2.zero; outRt.offsetMax = Vector2.zero;
            Image outImg = outlineObj.AddComponent<Image>();
            outImg.sprite = HexBlock.GetHexBorderSprite();
            outImg.type = Image.Type.Simple;
            outImg.preserveAspect = true;
            outImg.color = new Color(1f, 1f, 1f, 0.6f);
            outImg.raycastTarget = false;
            var btn = btnObj.AddComponent<Button>();
            var bc = btn.colors; bc.normalColor = Color.white;
            bc.highlightedColor = highlightColor; bc.pressedColor = pressedColor;
            btn.colors = bc;
            return (btnObj, btn, btnImage);
        }

        // ============================================================
        // 아이템 수량 배지 (x99 스타일, 버튼 하단 중앙)
        // ============================================================
        private Dictionary<ItemType, Text> itemCountBadges = new Dictionary<ItemType, Text>();

        /// <summary>
        /// 아이템 버튼 MP 소모량 배지 — 비활성화 (게이지 시스템으로 대체됨)
        /// </summary>
        private void CreateItemCountBadge(GameObject btnObj, ItemType itemType)
        {
            // MP 배지 제거됨 — 아이템 게이지 시스템이 비용을 관리
        }

        /// <summary>
        /// 모든 아이템 MP 배지 업데이트 (MP 비용은 고정이므로 일반적으로 불필요)
        /// </summary>
        public void UpdateAllItemCountBadges()
        {
            if (MPManager.Instance == null) return;
            foreach (var kvp in itemCountBadges)
            {
                if (kvp.Value != null)
                {
                    int cost = MPManager.Instance.GetItemCost(kvp.Key);
                    kvp.Value.text = cost > 0 ? $"{cost}" : "0";
                }
            }
        }

        /// <summary>
        /// 특정 아이템 배지 업데이트 (호환성 유지)
        /// </summary>
        public void UpdateItemCountBadge(ItemType type, int count)
        {
            // MP 시스템으로 전환 — 아이템 수량 대신 MP 비용 표시 (값 불변)
            // 기존 콜백 호환을 위해 메서드 유지
        }

        // ============================================================
        // 아이템 구매 팝업 시스템
        // ============================================================
        private GameObject purchasePopupObj;

        /// <summary>
        /// 아이템 구매 팝업 표시
        /// </summary>
        public void ShowItemPurchasePopup(ItemType itemType)
        {
            if (purchasePopupObj != null)
            {
                Destroy(purchasePopupObj);
            }

            Canvas canvas = hexGrid != null ? hexGrid.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 팝업 오픈 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayPopupOpen();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int price = ItemManager.GetItemGoldPrice(itemType);
            string itemName = ItemManager.GetItemDisplayName(itemType);

            // 팝업 배경 오버레이
            purchasePopupObj = new GameObject("PurchasePopup");
            purchasePopupObj.transform.SetParent(canvas.transform, false);
            purchasePopupObj.transform.SetAsLastSibling();
            RectTransform popupRt = purchasePopupObj.AddComponent<RectTransform>();
            popupRt.anchorMin = Vector2.zero;
            popupRt.anchorMax = Vector2.one;
            popupRt.offsetMin = Vector2.zero;
            popupRt.offsetMax = Vector2.zero;

            // 반투명 배경
            Image bgOverlay = purchasePopupObj.AddComponent<Image>();
            bgOverlay.color = ClaudeTheme.PopupOverlay;

            // 닫기용 버튼 (배경 클릭)
            Button bgBtn = purchasePopupObj.AddComponent<Button>();
            bgBtn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayButtonClick();
                ClosePurchasePopup();
            });
            var bgBc = bgBtn.colors;
            bgBc.normalColor = Color.white;
            bgBc.highlightedColor = Color.white;
            bgBc.pressedColor = Color.white;
            bgBtn.colors = bgBc;

            // 팝업 패널
            GameObject panel = new GameObject("PurchasePanel");
            panel.transform.SetParent(purchasePopupObj.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(420f, 320f);
            Image panelBg = panel.AddComponent<Image>();
            ClaudeTheme.ApplyPopupPanel(panelBg); // 다크글래스 통일
            // 패널 클릭 시 배경 닫기 이벤트 전파 방지
            Button panelBlocker = panel.AddComponent<Button>();
            var pbColors = panelBlocker.colors;
            pbColors.normalColor = Color.white; pbColors.highlightedColor = Color.white;
            pbColors.pressedColor = Color.white; pbColors.selectedColor = Color.white;
            panelBlocker.colors = pbColors;
            panelBlocker.transition = Selectable.Transition.None;

            // 패널 테두리
            GameObject borderObj = new GameObject("PanelBorder");
            borderObj.transform.SetParent(panel.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-2f, -2f);
            borderRt.offsetMax = new Vector2(2f, 2f);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(1f, 0.84f, 0f, 0.7f);
            borderImg.raycastTarget = false;
            borderObj.transform.SetAsFirstSibling();

            // 타이틀
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "아이템 구매";
            titleText.font = font;
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.9f, 0.4f, 1f);
            titleText.raycastTarget = false;
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);
            titleRt.sizeDelta = new Vector2(0f, 30f);

            // 내용: 아이템 이름
            GameObject descObj = new GameObject("Description");
            descObj.transform.SetParent(panel.transform, false);
            Text descText = descObj.AddComponent<Text>();
            descText.text = itemName;
            descText.font = font;
            descText.fontSize = 20;
            descText.fontStyle = FontStyle.Bold;
            descText.alignment = TextAnchor.MiddleCenter;
            descText.color = Color.white;
            descText.raycastTarget = false;
            RectTransform descRt = descObj.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.1f, 0.72f);
            descRt.anchorMax = new Vector2(0.9f, 0.86f);
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;

            // 보유 골드 표시
            GameObject goldObj = new GameObject("GoldText");
            goldObj.transform.SetParent(panel.transform, false);
            Text goldText = goldObj.AddComponent<Text>();
            goldText.text = $"보유: {currentGold} 골드";
            goldText.font = font;
            goldText.fontSize = 14;
            goldText.alignment = TextAnchor.MiddleCenter;
            goldText.color = new Color(0.8f, 0.8f, 0.6f, 0.8f);
            goldText.raycastTarget = false;
            RectTransform goldRt = goldObj.GetComponent<RectTransform>();
            goldRt.anchorMin = new Vector2(0.1f, 0.60f);
            goldRt.anchorMax = new Vector2(0.9f, 0.72f);
            goldRt.offsetMin = Vector2.zero;
            goldRt.offsetMax = Vector2.zero;

            int price1 = price;
            int price3 = price * 3;
            bool canAfford1 = currentGold >= price1;
            bool canAfford3 = currentGold >= price3;
            ItemType capturedType = itemType;

            // === 1개 구매 버튼 ===
            GameObject buy1Obj = new GameObject("Buy1Button");
            buy1Obj.transform.SetParent(panel.transform, false);
            RectTransform buy1Rt = buy1Obj.AddComponent<RectTransform>();
            buy1Rt.anchorMin = new Vector2(0.06f, 0.30f);
            buy1Rt.anchorMax = new Vector2(0.48f, 0.58f);
            buy1Rt.offsetMin = Vector2.zero;
            buy1Rt.offsetMax = Vector2.zero;
            Image buy1Img = buy1Obj.AddComponent<Image>();
            buy1Img.color = canAfford1 ? new Color(0.2f, 0.65f, 0.3f, 1f) : new Color(0.35f, 0.35f, 0.4f, 1f);
            Button buy1Btn = buy1Obj.AddComponent<Button>();
            var buy1Bc = buy1Btn.colors;
            buy1Bc.normalColor = Color.white;
            buy1Bc.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            buy1Bc.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            buy1Btn.colors = buy1Bc;

            GameObject captured1Obj = buy1Obj;
            buy1Btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                StartCoroutine(PopupButtonReaction(captured1Obj));
                if (ItemManager.Instance != null && ItemManager.Instance.PurchaseItem(capturedType, 1))
                {
                    UpdateItemCountBadge(capturedType, ItemManager.Instance.GetItemCount(capturedType));
                    StartCoroutine(DelayedAction(0.12f, () => ClosePurchasePopup()));
                }
                else
                {
                    StartCoroutine(PopupButtonShake(captured1Obj));
                    ShowFloatingMessage("골드가 부족합니다");
                }
            });

            // 1개 구매 버튼 텍스트 (수량 + 가격)
            GameObject buy1TextObj = new GameObject("Buy1Text");
            buy1TextObj.transform.SetParent(buy1Obj.transform, false);
            Text buy1Text = buy1TextObj.AddComponent<Text>();
            buy1Text.text = $"1개 구매\n<size=14>{price1} 골드</size>";
            buy1Text.font = font;
            buy1Text.fontSize = 16;
            buy1Text.fontStyle = FontStyle.Bold;
            buy1Text.alignment = TextAnchor.MiddleCenter;
            buy1Text.color = canAfford1 ? Color.white : new Color(1f, 0.5f, 0.5f);
            buy1Text.raycastTarget = false;
            buy1Text.supportRichText = true;
            RectTransform buy1TextRt = buy1TextObj.GetComponent<RectTransform>();
            buy1TextRt.anchorMin = Vector2.zero;
            buy1TextRt.anchorMax = Vector2.one;
            buy1TextRt.offsetMin = Vector2.zero;
            buy1TextRt.offsetMax = Vector2.zero;

            // === 3개 구매 버튼 ===
            GameObject buy3Obj = new GameObject("Buy3Button");
            buy3Obj.transform.SetParent(panel.transform, false);
            RectTransform buy3Rt = buy3Obj.AddComponent<RectTransform>();
            buy3Rt.anchorMin = new Vector2(0.52f, 0.30f);
            buy3Rt.anchorMax = new Vector2(0.94f, 0.58f);
            buy3Rt.offsetMin = Vector2.zero;
            buy3Rt.offsetMax = Vector2.zero;
            Image buy3Img = buy3Obj.AddComponent<Image>();
            buy3Img.color = canAfford3 ? new Color(0.2f, 0.5f, 0.8f, 1f) : new Color(0.35f, 0.35f, 0.4f, 1f);
            Button buy3Btn = buy3Obj.AddComponent<Button>();
            var buy3Bc = buy3Btn.colors;
            buy3Bc.normalColor = Color.white;
            buy3Bc.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            buy3Bc.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            buy3Btn.colors = buy3Bc;

            GameObject captured3Obj = buy3Obj;
            buy3Btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                StartCoroutine(PopupButtonReaction(captured3Obj));
                if (ItemManager.Instance != null && ItemManager.Instance.PurchaseItem(capturedType, 3))
                {
                    UpdateItemCountBadge(capturedType, ItemManager.Instance.GetItemCount(capturedType));
                    StartCoroutine(DelayedAction(0.12f, () => ClosePurchasePopup()));
                }
                else
                {
                    StartCoroutine(PopupButtonShake(captured3Obj));
                    ShowFloatingMessage("골드가 부족합니다");
                }
            });

            // 3개 구매 버튼 텍스트
            GameObject buy3TextObj = new GameObject("Buy3Text");
            buy3TextObj.transform.SetParent(buy3Obj.transform, false);
            Text buy3Text = buy3TextObj.AddComponent<Text>();
            buy3Text.text = $"3개 구매\n<size=14>{price3} 골드</size>";
            buy3Text.font = font;
            buy3Text.fontSize = 16;
            buy3Text.fontStyle = FontStyle.Bold;
            buy3Text.alignment = TextAnchor.MiddleCenter;
            buy3Text.color = canAfford3 ? Color.white : new Color(1f, 0.5f, 0.5f);
            buy3Text.raycastTarget = false;
            buy3Text.supportRichText = true;
            RectTransform buy3TextRt = buy3TextObj.GetComponent<RectTransform>();
            buy3TextRt.anchorMin = Vector2.zero;
            buy3TextRt.anchorMax = Vector2.one;
            buy3TextRt.offsetMin = Vector2.zero;
            buy3TextRt.offsetMax = Vector2.zero;

            // === 취소 버튼 (하단 중앙) ===
            GameObject cancelBtnObj = new GameObject("CancelButton");
            cancelBtnObj.transform.SetParent(panel.transform, false);
            RectTransform cancelBtnRt = cancelBtnObj.AddComponent<RectTransform>();
            cancelBtnRt.anchorMin = new Vector2(0.25f, 0.06f);
            cancelBtnRt.anchorMax = new Vector2(0.75f, 0.24f);
            cancelBtnRt.offsetMin = Vector2.zero;
            cancelBtnRt.offsetMax = Vector2.zero;
            Image cancelBtnImg = cancelBtnObj.AddComponent<Image>();
            cancelBtnImg.color = new Color(0.45f, 0.3f, 0.3f, 1f);
            Button cancelBtn = cancelBtnObj.AddComponent<Button>();
            var cancelBc = cancelBtn.colors;
            cancelBc.normalColor = Color.white;
            cancelBc.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            cancelBc.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cancelBtn.colors = cancelBc;
            GameObject capturedCancelBtnObj = cancelBtnObj;
            cancelBtn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                StartCoroutine(PopupButtonReaction(capturedCancelBtnObj));
                StartCoroutine(DelayedAction(0.12f, () => ClosePurchasePopup()));
            });

            // 취소 버튼 텍스트
            GameObject cancelTextObj = new GameObject("CancelText");
            cancelTextObj.transform.SetParent(cancelBtnObj.transform, false);
            Text cancelText = cancelTextObj.AddComponent<Text>();
            cancelText.text = "취소";
            cancelText.font = font;
            cancelText.fontSize = 18;
            cancelText.fontStyle = FontStyle.Bold;
            cancelText.alignment = TextAnchor.MiddleCenter;
            cancelText.color = Color.white;
            cancelText.raycastTarget = false;
            RectTransform cancelTextRt = cancelTextObj.GetComponent<RectTransform>();
            cancelTextRt.anchorMin = Vector2.zero;
            cancelTextRt.anchorMax = Vector2.one;
            cancelTextRt.offsetMin = Vector2.zero;
            cancelTextRt.offsetMax = Vector2.zero;

            // 패널 등장 애니메이션
            StartCoroutine(PopupPanelAppear(panel));

            Debug.Log($"[GameManager] 구매 팝업 표시: {itemName}, 가격 {price} 골드");
        }

        /// <summary>
        /// 팝업 패널 등장 애니메이션 (스케일 0→1 바운스)
        /// </summary>
        private IEnumerator PopupPanelAppear(GameObject panel)
        {
            if (panel == null) yield break;
            Transform t = panel.transform;
            t.localScale = Vector3.zero;
            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                // 오버슈트 이징: 살짝 커졌다 돌아옴
                float scale = p < 0.7f
                    ? Mathf.Lerp(0f, 1.08f, p / 0.7f)
                    : Mathf.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                t.localScale = Vector3.one * scale;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>
        /// 팝업 버튼 클릭 리액션 (축소→복원 펄스)
        /// </summary>
        private IEnumerator PopupButtonReaction(GameObject btnObj)
        {
            if (btnObj == null) yield break;
            Transform t = btnObj.transform;
            float duration = 0.12f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float scale = 1f - 0.15f * Mathf.Sin(p * Mathf.PI);
                t.localScale = Vector3.one * scale;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        /// <summary>
        /// 팝업 버튼 실패 시 좌우 흔들림
        /// </summary>
        private IEnumerator PopupButtonShake(GameObject btnObj)
        {
            if (btnObj == null) yield break;
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (rt == null) yield break;
            Vector2 origPos = rt.anchoredPosition;
            float duration = 0.3f;
            float elapsed = 0f;
            float intensity = 5f;
            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float decay = 1f - p;
                float offsetX = Mathf.Sin(p * Mathf.PI * 6f) * intensity * decay;
                rt.anchoredPosition = origPos + new Vector2(offsetX, 0f);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = origPos;
        }

        /// <summary>
        /// 딜레이 후 액션 실행 헬퍼
        /// </summary>
        private IEnumerator DelayedAction(float delay, System.Action action)
        {
            yield return new WaitForSecondsRealtime(delay);
            action?.Invoke();
        }

        /// <summary>
        /// 구매 팝업 닫기
        /// </summary>
        public void ClosePurchasePopup()
        {
            if (purchasePopupObj != null)
            {
                Destroy(purchasePopupObj);
                purchasePopupObj = null;
            }
        }

        /// <summary>
        /// 구매 팝업이 열려있는지 확인
        /// </summary>
        public bool IsPurchasePopupOpen => purchasePopupObj != null;

        // ============================================================
        // 플로팅 메시지 (화면 상단 1/4 지점, 3초 유지 후 위로 페이드아웃)
        // ============================================================

        /// <summary>
        /// 화면 상단 1/4 지점에 메시지를 표시하고 3초 후 위로 올라가며 페이드아웃
        /// </summary>
        public void ShowFloatingMessage(string message)
        {
            Canvas canvas = hexGrid != null ? hexGrid.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return;
            StartCoroutine(FloatingMessageCoroutine(canvas, message));
        }

        private IEnumerator FloatingMessageCoroutine(Canvas canvas, string message)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject msgObj = new GameObject("FloatingMessage");
            msgObj.transform.SetParent(canvas.transform, false);
            msgObj.transform.SetAsLastSibling();

            Text msgText = msgObj.AddComponent<Text>();
            msgText.text = message;
            msgText.font = font;
            msgText.fontSize = 40;
            msgText.fontStyle = FontStyle.Bold;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.color = Color.white;
            msgText.raycastTarget = false;
            msgText.horizontalOverflow = HorizontalWrapMode.Overflow;
            msgText.verticalOverflow = VerticalWrapMode.Overflow;

            // 그림자 효과
            var shadow = msgObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            // 화면 상단 1/4 지점, 가로 중앙
            RectTransform rt = msgObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.75f);
            rt.anchorMax = new Vector2(0.5f, 0.75f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(400f, 40f);

            // 3초 대기
            yield return new WaitForSeconds(3f);

            // 위로 올라가면서 페이드아웃 (1초)
            float fadeDuration = 1f;
            float elapsed = 0f;
            Vector2 startPos = rt.anchoredPosition;

            while (elapsed < fadeDuration)
            {
                if (msgObj == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float eased = t * t; // EaseIn — 서서히 가속

                // 위로 이동 (30px)
                rt.anchoredPosition = startPos + new Vector2(0f, eased * 30f);

                // 알파 페이드아웃
                float alpha = 1f - t;
                msgText.color = new Color(1f, 1f, 1f, alpha);

                yield return null;
            }

            if (msgObj != null)
                Destroy(msgObj);
        }

        /// <summary>
        /// 캔버스에 남아있는 플로팅 메시지/골드 팝업 정리 (로비 전환 시 호출)
        /// </summary>
        private void CleanupFloatingMessages()
        {
            Canvas canvas = hexGrid != null ? hexGrid.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return;

            var toDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in canvas.transform)
            {
                if (child.name.StartsWith("FloatingMessage") || child.name.StartsWith("GoldPopup_"))
                    toDestroy.Add(child.gameObject);
            }
            foreach (var obj in toDestroy)
                Destroy(obj);
        }

        private Image CreateItemOverlay(Canvas canvas, string name, Color overlayColor)
        {
            GameObject overlayObj = new GameObject(name);
            overlayObj.transform.SetParent(canvas.transform, false);
            var overlay = overlayObj.AddComponent<Image>();
            overlay.color = overlayColor; overlay.raycastTarget = false;
            RectTransform overlayRt = overlayObj.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero; overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero; overlayRt.offsetMax = Vector2.zero;
            overlayObj.SetActive(false);
            return overlay;
        }

        private void CreateHammerUI(Canvas canvas)
        {
            var overlay = CreateItemOverlay(canvas, "HammerOverlay", new Color(0.6f, 1f, 0.6f, 0.15f));
            var (btnObj, btn, btnImage) = CreateHexItemButton(canvas, "HammerButton",
                GetItemButtonPosition(0), HAMMER_BTN_COLOR,
                new Color(0.0f, 0.85f, 0.85f, 1f), new Color(0.0f, 0.55f, 0.55f, 1f));
            // 망치 머리
            GameObject headObj = new GameObject("HammerHead");
            headObj.transform.SetParent(btnObj.transform, false);
            var headImg = headObj.AddComponent<Image>();
            headImg.color = new Color(0.85f, 0.85f, 0.9f, 1f); headImg.raycastTarget = false;
            RectTransform headRt = headObj.GetComponent<RectTransform>();
            headRt.anchoredPosition = new Vector2(0f, 9f);
            headRt.sizeDelta = new Vector2(40f, 18f);
            headRt.localRotation = Quaternion.Euler(0, 0, -15f);
            // 자루
            GameObject handleObj = new GameObject("HammerHandle");
            handleObj.transform.SetParent(btnObj.transform, false);
            var handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(0.6f, 0.4f, 0.2f, 1f); handleImg.raycastTarget = false;
            RectTransform handleRt = handleObj.GetComponent<RectTransform>();
            handleRt.anchoredPosition = new Vector2(3f, -11f);
            handleRt.sizeDelta = new Vector2(8f, 35f);
            handleRt.localRotation = Quaternion.Euler(0, 0, -15f);
            // 하이라이트
            GameObject hlObj = new GameObject("HammerHL");
            hlObj.transform.SetParent(headObj.transform, false);
            var hlImg = hlObj.AddComponent<Image>();
            hlImg.color = new Color(1f, 1f, 1f, 0.35f); hlImg.raycastTarget = false;
            RectTransform hlRt = hlObj.GetComponent<RectTransform>();
            hlRt.anchoredPosition = new Vector2(-5f, 3f); hlRt.sizeDelta = new Vector2(11f, 6f);
            CreateItemCountBadge(btnObj, ItemType.Hammer);
            btnObj.transform.SetAsLastSibling();
            var hammer = btnObj.AddComponent<HammerItem>();
            var type = typeof(HammerItem);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("hammerButton", flags)?.SetValue(hammer, btn);
            type.GetField("backgroundOverlay", flags)?.SetValue(hammer, overlay);
            hudElements.Add(btnObj);
            hammerButtonObj = btnObj;
            // 기능 미해금 시 숨김 (TutorialManager 없어도 숨김)
            if (TutorialManager.Instance == null || !TutorialManager.Instance.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_HAMMER))
                btnObj.SetActive(false);
            // ★ ChargeBar로 비주얼 위임 — 구버전 버튼 비주얼 즉시 차단 (race 제거).
            //   SetActive(true)되어도 LegacyButtonHider.OnEnable이 같은 프레임에 가드.
            HideLegacyItemButton(btnObj);
        }

        private void CreateSwapUI(Canvas canvas)
        {
            var overlay = CreateItemOverlay(canvas, "SwapOverlay", new Color(0.4f, 0.7f, 1f, 0.15f));
            var (btnObj, btn, btnImage) = CreateHexItemButton(canvas, "SwapButton",
                GetItemButtonPosition(1), SWAP_BTN_COLOR,
                new Color(0.95f, 0.45f, 0.75f, 1f), new Color(0.65f, 0.25f, 0.50f, 1f));
            // 양방향 화살표
            GameObject barObj = new GameObject("SwapBar");
            barObj.transform.SetParent(btnObj.transform, false);
            var barImg = barObj.AddComponent<Image>();
            barImg.color = Color.white; barImg.raycastTarget = false;
            RectTransform barRt = barObj.GetComponent<RectTransform>();
            barRt.anchoredPosition = new Vector2(0f, 3f); barRt.sizeDelta = new Vector2(37f, 5f);
            // 왼쪽 화살표
            for (int s = -1; s <= 1; s += 2)
            {
                float xDir = s * 16f;
                for (int a = 0; a < 2; a++)
                {
                    GameObject arr = new GameObject(s < 0 ? $"LeftArrow{a+1}" : $"RightArrow{a+1}");
                    arr.transform.SetParent(btnObj.transform, false);
                    var ai = arr.AddComponent<Image>();
                    ai.color = Color.white; ai.raycastTarget = false;
                    RectTransform art = arr.GetComponent<RectTransform>();
                    float yOff = a == 0 ? 9f : -3f;
                    float angle = a == 0 ? (s < 0 ? 40f : -40f) : (s < 0 ? -40f : 40f);
                    art.anchoredPosition = new Vector2(xDir, yOff);
                    art.sizeDelta = new Vector2(14f, 5f);
                    art.localRotation = Quaternion.Euler(0, 0, angle);
                }
            }
            CreateItemCountBadge(btnObj, ItemType.Bomb);
            btnObj.transform.SetAsLastSibling();
            var swap = btnObj.AddComponent<JewelsHexaPuzzle.Items.SwapItem>();
            var type = typeof(JewelsHexaPuzzle.Items.SwapItem);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("swapButton", flags)?.SetValue(swap, btn);
            type.GetField("backgroundOverlay", flags)?.SetValue(swap, overlay);
            hudElements.Add(btnObj);
            swapButtonObj = btnObj;
            // 기능 미해금 시 숨김 (TutorialManager 없어도 숨김)
            if (TutorialManager.Instance == null || !TutorialManager.Instance.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_SWAP))
                btnObj.SetActive(false);
            // ★ ChargeBar로 비주얼 위임 — 즉시 차단
            HideLegacyItemButton(btnObj);
        }

        private void CreateLineDrawUI(Canvas canvas)
        {
            var overlay = CreateItemOverlay(canvas, "LineDrawOverlay", new Color(0.9f, 0.6f, 0.2f, 0.15f));
            var (btnObj, btn, btnImage) = CreateHexItemButton(canvas, "LineDrawButton",
                GetItemButtonPosition(2), LINEDRAW_BTN_COLOR,
                new Color(0.45f, 0.65f, 0.45f, 1f), new Color(0.25f, 0.40f, 0.25f, 1f));
            // 연필 몸통
            GameObject bodyObj = new GameObject("PencilBody");
            bodyObj.transform.SetParent(btnObj.transform, false);
            var bodyImg = bodyObj.AddComponent<Image>();
            bodyImg.color = new Color(0.95f, 0.75f, 0.35f, 1f); bodyImg.raycastTarget = false;
            RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(3f, 5f);
            bodyRt.sizeDelta = new Vector2(33f, 8f);
            bodyRt.localRotation = Quaternion.Euler(0, 0, 40f);
            // 연필 촉
            GameObject tipObj = new GameObject("PencilTip");
            tipObj.transform.SetParent(btnObj.transform, false);
            var tipImg = tipObj.AddComponent<Image>();
            tipImg.color = new Color(0.35f, 0.35f, 0.35f, 1f); tipImg.raycastTarget = false;
            RectTransform tipRt = tipObj.GetComponent<RectTransform>();
            tipRt.anchoredPosition = new Vector2(-11f, -11f);
            tipRt.sizeDelta = new Vector2(11f, 8f);
            tipRt.localRotation = Quaternion.Euler(0, 0, 40f);
            // 지우개
            GameObject eraserObj = new GameObject("PencilEraser");
            eraserObj.transform.SetParent(btnObj.transform, false);
            var eraserImg = eraserObj.AddComponent<Image>();
            eraserImg.color = new Color(0.9f, 0.45f, 0.45f, 1f); eraserImg.raycastTarget = false;
            RectTransform eraserRt = eraserObj.GetComponent<RectTransform>();
            eraserRt.anchoredPosition = new Vector2(16f, 22f);
            eraserRt.sizeDelta = new Vector2(8f, 8f);
            eraserRt.localRotation = Quaternion.Euler(0, 0, 40f);
            // 라인 도트
            for (int i = 0; i < 3; i++)
            {
                GameObject dotObj = new GameObject($"LineDot_{i}");
                dotObj.transform.SetParent(btnObj.transform, false);
                var dotImg = dotObj.AddComponent<Image>();
                dotImg.color = new Color(1f, 1f, 1f, 0.6f); dotImg.raycastTarget = false;
                RectTransform dotRt = dotObj.GetComponent<RectTransform>();
                dotRt.anchoredPosition = new Vector2(-22f + i * 14f, -24f);
                dotRt.sizeDelta = new Vector2(4f, 4f);
            }
            CreateItemCountBadge(btnObj, ItemType.SSD);
            btnObj.transform.SetAsLastSibling();
            var lineDraw = btnObj.AddComponent<JewelsHexaPuzzle.Items.LineDrawItem>();
            var type = typeof(JewelsHexaPuzzle.Items.LineDrawItem);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("lineDrawButton", flags)?.SetValue(lineDraw, btn);
            type.GetField("backgroundOverlay", flags)?.SetValue(lineDraw, overlay);
            hudElements.Add(btnObj);
            lineDrawButtonObj = btnObj;
            // 기능 미해금 시 숨김 (TutorialManager 없어도 숨김)
            if (TutorialManager.Instance == null || !TutorialManager.Instance.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_LINEDRAW))
                btnObj.SetActive(false);
            // ★ ChargeBar로 비주얼 위임 — 즉시 차단
            HideLegacyItemButton(btnObj);
        }

        private void CreateReverseRotationUI(Canvas canvas)
        {
            // 즉시 효과 아이템 → backgroundOverlay 불필요
            var (btnObj, btn, btnImage) = CreateHexItemButton(canvas, "ReverseRotationButton",
                GetItemButtonPosition(3), REVERSE_BTN_COLOR,
                new Color(0.70f, 0.50f, 0.90f, 1f), new Color(0.40f, 0.20f, 0.60f, 1f));

            // 아이콘: 곡선 화살표 (회전 방향 표시)
            // 화살표 컨테이너 (방향 전환 시 scaleX 반전)
            GameObject arrowContainer = new GameObject("ArrowContainer");
            arrowContainer.transform.SetParent(btnObj.transform, false);
            RectTransform arrowContRt = arrowContainer.AddComponent<RectTransform>();
            arrowContRt.anchoredPosition = new Vector2(0f, 2f);
            arrowContRt.sizeDelta = new Vector2(50f, 50f);

            // 호(arc) 형태 화살표: 세그먼트 4개로 원호 표현
            float radius = 15f;
            int segCount = 4;
            for (int i = 0; i < segCount; i++)
            {
                float angle = -60f + i * 50f; // -60, -10, 40, 90
                float rad = angle * Mathf.Deg2Rad;

                GameObject seg = new GameObject($"ArcSeg_{i}");
                seg.transform.SetParent(arrowContainer.transform, false);
                var segImg = seg.AddComponent<Image>();
                segImg.color = Color.white;
                segImg.raycastTarget = false;
                RectTransform segRt = seg.GetComponent<RectTransform>();
                segRt.anchoredPosition = new Vector2(
                    Mathf.Cos(rad) * radius,
                    Mathf.Sin(rad) * radius
                );
                segRt.sizeDelta = new Vector2(13f, 5f);
                segRt.localRotation = Quaternion.Euler(0, 0, angle + 90f);
            }

            // 화살촉 (시계방향 끝)
            GameObject arrowHead1 = new GameObject("ArrowHead1");
            arrowHead1.transform.SetParent(arrowContainer.transform, false);
            var ah1Img = arrowHead1.AddComponent<Image>();
            ah1Img.color = Color.white;
            ah1Img.raycastTarget = false;
            RectTransform ah1Rt = arrowHead1.GetComponent<RectTransform>();
            float headAngle = 90f * Mathf.Deg2Rad;
            ah1Rt.anchoredPosition = new Vector2(
                Mathf.Cos(headAngle) * radius + 4f,
                Mathf.Sin(headAngle) * radius + 2f
            );
            ah1Rt.sizeDelta = new Vector2(10f, 5f);
            ah1Rt.localRotation = Quaternion.Euler(0, 0, 145f);

            // 화살촉 아래쪽 날개
            GameObject arrowHead2 = new GameObject("ArrowHead2");
            arrowHead2.transform.SetParent(arrowContainer.transform, false);
            var ah2Img = arrowHead2.AddComponent<Image>();
            ah2Img.color = Color.white;
            ah2Img.raycastTarget = false;
            RectTransform ah2Rt = arrowHead2.GetComponent<RectTransform>();
            ah2Rt.anchoredPosition = new Vector2(
                Mathf.Cos(headAngle) * radius + 4f,
                Mathf.Sin(headAngle) * radius - 3f
            );
            ah2Rt.sizeDelta = new Vector2(10f, 5f);
            ah2Rt.localRotation = Quaternion.Euler(0, 0, -145f);

            // "R" 텍스트 (중앙, 소형)
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("ReverseLabel");
            labelObj.transform.SetParent(btnObj.transform, false);
            var labelText = labelObj.AddComponent<Text>();
            labelText.text = "R";
            labelText.font = font;
            labelText.fontSize = 14;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(1f, 1f, 1f, 0.5f);
            labelText.raycastTarget = false;
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchoredPosition = new Vector2(0f, 2f);
            labelRt.sizeDelta = new Vector2(20f, 20f);

            // 수량 배지
            CreateItemCountBadge(btnObj, ItemType.ReverseRotation);
            btnObj.transform.SetAsLastSibling();

            // ReverseRotationItem 컴포넌트 연결
            var reverseItem = btnObj.AddComponent<JewelsHexaPuzzle.Items.ReverseRotationItem>();
            var type = typeof(JewelsHexaPuzzle.Items.ReverseRotationItem);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("reverseButton", flags)?.SetValue(reverseItem, btn);
            type.GetField("arrowContainer", flags)?.SetValue(reverseItem, arrowContRt);
            hudElements.Add(btnObj);
            reverseRotationButtonObj = btnObj;
            // 기능 미해금 시 숨김 (TutorialManager 없어도 숨김)
            if (TutorialManager.Instance == null || !TutorialManager.Instance.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_REVERSE))
                btnObj.SetActive(false);
            // ★ ChargeBar로 비주얼 위임 — 즉시 차단
            HideLegacyItemButton(btnObj);
        }

        // ============================================================
        // 골드 추가 버튼 (좌측 하단, 육각형)
        // ============================================================
        private const float GOLD_BTN_SIZE = 77f; // 기존 70에서 10% 확대

        private void CreateGoldAddButton(Canvas canvas)
        {
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float sqrt3 = Mathf.Sqrt(3f);
            // 왼쪽 하단 위치 계산 (아이템 버튼 대칭 위치)
            float gridLeft = -(hSize * 1.5f * 5f);
            float posX = gridLeft / 2f - 80f + 15f; // 오른쪽으로 15px 이동
            float btnHexH = GOLD_BTN_SIZE * sqrt3 / 2f;
            float posY = hSize * sqrt3 * (-5f) - btnHexH * 0.3f - 5f - 100f;

            // 70px 사이즈 육각형 버튼 직접 생성
            GameObject btnObj = new GameObject("GoldAddButton");
            btnObj.transform.SetParent(canvas.transform, false);
            goldCheatButtonObj = btnObj; // ★ 에디터 패널 토글 연동용 참조 (EditorTestSystem.ShowPanel)
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(posX, posY);
            btnRt.sizeDelta = new Vector2(GOLD_BTN_SIZE, GOLD_BTN_SIZE);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.sprite = HexBlock.GetHexFlashSprite();
            btnImage.type = Image.Type.Simple;
            btnImage.preserveAspect = true;
            btnImage.color = GOLD_BTN_COLOR;
            // 아웃라인
            GameObject outlineObj = new GameObject("GoldBtnOutline");
            outlineObj.transform.SetParent(btnObj.transform, false);
            RectTransform outRt = outlineObj.AddComponent<RectTransform>();
            outRt.anchorMin = Vector2.zero; outRt.anchorMax = Vector2.one;
            outRt.offsetMin = Vector2.zero; outRt.offsetMax = Vector2.zero;
            Image outImg = outlineObj.AddComponent<Image>();
            outImg.sprite = HexBlock.GetHexBorderSprite();
            outImg.type = Image.Type.Simple;
            outImg.preserveAspect = true;
            outImg.color = new Color(1f, 1f, 1f, 0.6f);
            outImg.raycastTarget = false;
            var btn = btnObj.AddComponent<Button>();
            var bc = btn.colors; bc.normalColor = Color.white;
            bc.highlightedColor = new Color(1f, 0.85f, 0.3f, 1f);
            bc.pressedColor = new Color(0.65f, 0.50f, 0.10f, 1f);
            btn.colors = bc;

            // 코인 원형 (70/110 비율로 축소)
            GameObject coinObj = new GameObject("CoinCircle");
            coinObj.transform.SetParent(btnObj.transform, false);
            var coinImg = coinObj.AddComponent<Image>();
            coinImg.color = new Color(1f, 0.85f, 0.2f, 1f);
            coinImg.raycastTarget = false;
            RectTransform coinRt = coinObj.GetComponent<RectTransform>();
            coinRt.anchoredPosition = new Vector2(0f, 3f);
            coinRt.sizeDelta = new Vector2(24f, 24f);

            // 코인 내부 원 (입체감)
            GameObject innerObj = new GameObject("CoinInner");
            innerObj.transform.SetParent(coinObj.transform, false);
            var innerImg = innerObj.AddComponent<Image>();
            innerImg.color = new Color(0.95f, 0.75f, 0.1f, 1f);
            innerImg.raycastTarget = false;
            RectTransform innerRt = innerObj.GetComponent<RectTransform>();
            innerRt.anchoredPosition = Vector2.zero;
            innerRt.sizeDelta = new Vector2(18f, 18f);

            // 코인 G 마크
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject gObj = new GameObject("GoldMark");
            gObj.transform.SetParent(coinObj.transform, false);
            Text gText = gObj.AddComponent<Text>();
            gText.text = "G";
            gText.font = font;
            gText.fontSize = 12;
            gText.fontStyle = FontStyle.Bold;
            gText.alignment = TextAnchor.MiddleCenter;
            gText.color = new Color(0.5f, 0.35f, 0.0f, 1f);
            gText.raycastTarget = false;
            RectTransform gRt = gObj.GetComponent<RectTransform>();
            gRt.anchoredPosition = Vector2.zero;
            gRt.sizeDelta = new Vector2(20f, 20f);

            // + 표시 (오른쪽 아래)
            GameObject plusObj = new GameObject("PlusSign");
            plusObj.transform.SetParent(btnObj.transform, false);
            Text plusText = plusObj.AddComponent<Text>();
            plusText.text = "+";
            plusText.font = font;
            plusText.fontSize = 14;
            plusText.fontStyle = FontStyle.Bold;
            plusText.alignment = TextAnchor.MiddleCenter;
            plusText.color = new Color(1f, 1f, 1f, 0.9f);
            plusText.raycastTarget = false;
            RectTransform plusRt = plusObj.GetComponent<RectTransform>();
            plusRt.anchoredPosition = new Vector2(10f, -9f);
            plusRt.sizeDelta = new Vector2(16f, 16f);

            // "+100" 라벨 (하단)
            GameObject labelObj = new GameObject("GoldLabel");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = "+100";
            labelText.font = font;
            labelText.fontSize = 9;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(1f, 1f, 1f, 0.85f);
            labelText.raycastTarget = false;
            var labelShadow = labelObj.AddComponent<Shadow>();
            labelShadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
            labelShadow.effectDistance = new Vector2(1f, -1f);
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 0f);
            labelRt.anchorMax = new Vector2(0.5f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 6f);
            labelRt.sizeDelta = new Vector2(50f, 14f);

            // 롱프레스 감지를 위해 onClick 대신 EventTrigger 사용
            btn.onClick.RemoveAllListeners(); // onClick 미사용
            var eventTrigger = btnObj.AddComponent<EventTrigger>();

            // PointerDown: 프레스 시작 시간 기록 + 롱프레스 모니터링 코루틴 시작
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener((data) =>
            {
                goldBtnPressTime = Time.unscaledTime;
                goldBtnLongPressTriggered = false;
                if (goldBtnLongPressCoroutine != null)
                    StopCoroutine(goldBtnLongPressCoroutine);
                goldBtnLongPressCoroutine = StartCoroutine(GoldButtonLongPressMonitor(btnObj));
            });
            eventTrigger.triggers.Add(pointerDown);

            // PointerUp: 5초 미만이면 일반 클릭(+100 골드), 롱프레스 발동 후면 무시
            var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener((data) =>
            {
                if (goldBtnLongPressCoroutine != null)
                {
                    StopCoroutine(goldBtnLongPressCoroutine);
                    goldBtnLongPressCoroutine = null;
                }

                if (!goldBtnLongPressTriggered)
                {
                    // 일반 클릭: +100 골드
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayButtonClick();
                    AddGold(100);
                    ShowFloatingMessage("+100 골드!");
                    StartCoroutine(GoldButtonPulse(btnObj));
                }
            });
            eventTrigger.triggers.Add(pointerUp);

            btnObj.transform.SetAsLastSibling();
            hudElements.Add(btnObj);
        }

        // ★ 골드 +100 치트 버튼 참조 — 에디터 패널 토글로 함께 숨김/표시 (EditorTestSystem 연동)
        private GameObject goldCheatButtonObj;
        public GameObject GoldCheatButtonObject => goldCheatButtonObj;

        // 골드 버튼 롱프레스 상태 추적
        private float goldBtnPressTime;
        private bool goldBtnLongPressTriggered;
        private Coroutine goldBtnLongPressCoroutine;
        private const float GOLD_LONG_PRESS_DURATION = 5f;

        /// <summary>
        /// 골드 버튼 롱프레스 모니터링 — 5초 이상 누르면 골드 초기화
        /// </summary>
        private IEnumerator GoldButtonLongPressMonitor(GameObject btnObj)
        {
            // 5초 대기 (unscaledTime 사용하여 게임 일시정지 중에도 동작)
            float elapsed = 0f;
            while (elapsed < GOLD_LONG_PRESS_DURATION)
            {
                elapsed = Time.unscaledTime - goldBtnPressTime;
                yield return null;
            }

            // 5초 이상 유지 → 골드 초기화
            goldBtnLongPressTriggered = true;
            goldBtnLongPressCoroutine = null;

            currentGold = 0;
            OnGoldChanged?.Invoke(currentGold);
            if (uiManager != null)
                uiManager.UpdateGoldDisplay(currentGold);
            SaveGold();

            ShowFloatingMessage("골드 초기화! (0G)");
            Debug.Log("[GameManager] 골드 롱프레스 초기화: 0");

            // 빨간 플래시 피드백
            if (btnObj != null)
                StartCoroutine(GoldButtonResetFlash(btnObj));
        }

        /// <summary>
        /// 골드 초기화 시 빨간 플래시 피드백
        /// </summary>
        private IEnumerator GoldButtonResetFlash(GameObject btnObj)
        {
            if (btnObj == null) yield break;
            var img = btnObj.GetComponent<Image>();
            if (img == null) yield break;

            Color origColor = img.color;
            Color flashColor = new Color(0.9f, 0.2f, 0.2f, 1f); // 빨간색

            // 3회 깜빡임
            for (int i = 0; i < 3; i++)
            {
                img.color = flashColor;
                yield return new WaitForSecondsRealtime(0.1f);
                img.color = origColor;
                yield return new WaitForSecondsRealtime(0.1f);
            }
        }

        private System.Collections.IEnumerator GoldButtonPulse(GameObject btnObj)
        {
            if (btnObj == null) yield break;
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 origScale = rt.localScale;
            float duration = 0.15f;
            float t = 0f;

            // 확대
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = t / duration;
                float scale = 1f + 0.15f * Mathf.Sin(p * Mathf.PI);
                rt.localScale = origScale * scale;
                yield return null;
            }
            rt.localScale = origScale;
        }

        // ============================================================
        // 특수 블록 테스트 버튼 패널 (좌측 하단, 임시 테스트용)
        // ============================================================
        private EditorTestSystem editorTestSystemRef;

        private void CreateTestBlockPanel(Canvas canvas)
        {
            // ★ 에디터/개발 빌드 전용 (감사 H6) — 릴리스 빌드에 치트 패널 미생성.
            //   editorTestSystemRef가 null이어도 모든 사용처는 null 체크가 있어 안전.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // EditorTestSystem 컴포넌트 확인/추가
            editorTestSystemRef = GetComponent<EditorTestSystem>();
            if (editorTestSystemRef == null)
            {
                editorTestSystemRef = gameObject.AddComponent<EditorTestSystem>();
            }

            // Canvas UI 초기화 (hexGrid 직접 전달)
            editorTestSystemRef.InitializeUI(canvas, hexGrid);

            // HUD 관리에 패널 추가
            if (editorTestSystemRef.PanelObject != null)
                hudElements.Add(editorTestSystemRef.PanelObject);
            // 좌측 최하단 에디터 토글 버튼도 HUD에 포함 (로비 전환 시 함께 숨김)
            if (editorTestSystemRef.EditorToggleButtonObject != null)
                hudElements.Add(editorTestSystemRef.EditorToggleButtonObject);

            // InputSystem에 직접 참조 전달 (FindObjectOfType 의존 제거)
            if (inputSystem != null)
                inputSystem.SetEditorTestSystem(editorTestSystemRef);

            Debug.Log("[GameManager] 특수 블록 테스트 버튼 패널 생성 완료");
#endif
        }

        /// <summary>
        /// 게임 시작
        /// </summary>
        public void StartGame()
        {
            // ★ 인게임 진입도 엘프공주 이미지 2초 트랜지션 — 로비 → 인게임 흐름을 시각적으로 가린다.
            //   콜백 안에서 StartCoroutine 호출 → 페이드 아웃과 게임 로딩이 자연스럽게 병행.
            PlayLobbyTransition(() => StartCoroutine(StartGameCoroutine()));
        }

        private IEnumerator StartGameCoroutine()
        {
            SetGameState(GameState.Loading);

            // HUD 텍스트 즉시 0 초기화 (이전 게임 값이 보이는 것 방지)
            if (hudTurnText != null) hudTurnText.text = "0";
            if (hudScoreText != null) hudScoreText.text = "0";

            // 이전 게임 이벤트 정리 (중복 구독 방지)
            CleanupGameEvents();

            // MP 게이지 먼저 활성화 → 이후 ResetMP()에서 OnMPChanged 이벤트 발생 시 코루틴 정상 동작
            if (mpGaugeUI != null)
                mpGaugeUI.gameObject.SetActive(true);
            if (MPManager.Instance != null)
                MPManager.Instance.ResetMP();

            // 아이템 게이지 초기화
            if (JewelsHexaPuzzle.Items.HammerGauge.Instance != null)
                JewelsHexaPuzzle.Items.HammerGauge.Instance.ResetGauge();
            if (JewelsHexaPuzzle.Items.SwapGauge.Instance != null)
                JewelsHexaPuzzle.Items.SwapGauge.Instance.ResetGauge();
            if (JewelsHexaPuzzle.Items.LineGauge.Instance != null)
                JewelsHexaPuzzle.Items.LineGauge.Instance.ResetGauge();
            // ★ ItemManager 게이지(역회전 스택 포함) 완전 초기화 — 다른 버튼들과 동일하게 빈 상태로 시작.
            if (ItemManager.Instance != null)
                ItemManager.Instance.ResetAllGauges();
            // ★ ChargeBar 표시 캐시 0으로 강제 — 이전 게임 잔재(풀 게이지 상태) 즉시 비움.
            var cbc = FindObjectOfType<JewelsHexaPuzzle.Items.ChargeBarController>();
            if (cbc != null) cbc.ResetDisplayCharges();

            // 무한모드: 생존 미션 시스템으로 시작
            if (currentGameMode == GameMode.Infinite)
            {
                rotationCount = 0;
                // InfiniteConfig에서 초기 이동 횟수 가져오기 (없으면 기본 15)
                int infiniteMoves = 15;
                if (selectedLevelData?.infiniteConfig != null)
                    infiniteMoves = selectedLevelData.infiniteConfig.initialMoves;
                currentTurns = infiniteMoves;
                if (uiManager != null)
                {
                    uiManager.SetInfiniteMode(false);
                    uiManager.SetMaxTurns(infiniteMoves);
                }
                // 무한도전 최대 이동 횟수 표시
                if (hudMaxTurnText != null)
                    hudMaxTurnText.gameObject.SetActive(true);
            }
            else
            {
                // Stage 모드: Mission1StageData에서 직접 StageData 로드
                if (stageManager != null)
                {
                    // Mission1StageData에서 StageData 시도
                    var allStages = Mission1StageData.GetAllMission1Stages();
                    if (allStages.TryGetValue(selectedStage, out StageData missionStageData))
                    {
                        stageManager.LoadStageData(missionStageData);
                    }
                    else
                    {
                        // 폴백: 기본 생성
                        stageManager.LoadStage(selectedStage);
                    }
                    initialTurns = stageManager.CurrentStageData?.turnLimit ?? 30;
                }

                // 턴 수를 먼저 초기화 (TriggerStartDrop → OnCascadeComplete 콜백에서
                // currentTurns <= 0 체크 시 GameOver 호출되는 타이밍 버그 방지)
                currentTurns = initialTurns;
                turnsConsumedThisStage = 0; // ★ 동적 미션한도: 누적 소비 이동 리셋 (15마다 한도 해금)
                grantedSlotUnlocks = 0;     // ★ 해금 크레딧 부여 누계 리셋
                pendingSlotUnlocks = 0;     // ★ 미집행 해금 큐 리셋
                goldSpentThisStage = 0;     // ★ 학습 데이터: 스테이지별 골드 소비 리셋
                continueCount = 0; // ★ 이동횟수 추가 구매 횟수 초기화
                secondWaveTriggered = false; // 고블린 2차 웨이브 플래그 초기화
                if (uiManager != null)
                {
                    uiManager.SetInfiniteMode(false);
                    uiManager.SetMaxTurns(initialTurns);
                }
                // Stage 모드: 최대 이동 횟수 표시 숨김
                if (hudMaxTurnText != null)
                    hudMaxTurnText.gameObject.SetActive(false);
            }

            // 스테이지 전환 시 슬롯 캐시 무효화
            if (blockRemovalSystem != null)
                blockRemovalSystem.InvalidateSlotCache();

            // ★ Stage별 블록 색상 제약
            //   - Stage 1: R/G/B 3색
            //   - Stage 2: R/G/B 3색 (몽둥이 고블린 첫 등장)
            //   - Stage 3~20: 5색 (R/B/G/Y/P) — 기본
            //   - Stage 21+: 6색 (주황색 추가, 난이도 상승)
            if (currentGameMode == GameMode.Stage && selectedStage == 1)
            {
                JewelsHexaPuzzle.Data.GemTypeHelper.AllowedColorsOverride = new[] {
                    JewelsHexaPuzzle.Data.GemType.Red,
                    JewelsHexaPuzzle.Data.GemType.Green,
                    JewelsHexaPuzzle.Data.GemType.Blue
                };
                JewelsHexaPuzzle.Data.GemTypeHelper.ActiveGemTypeCount = 5; // 안전 기본값 복원
                Debug.Log("[GameManager] Stage 1: 블록 색상 R/G/B 3색 제약 활성화");
            }
            else if (currentGameMode == GameMode.Stage && selectedStage == 2)
            {
                JewelsHexaPuzzle.Data.GemTypeHelper.AllowedColorsOverride = new[] {
                    JewelsHexaPuzzle.Data.GemType.Red,
                    JewelsHexaPuzzle.Data.GemType.Green,
                    JewelsHexaPuzzle.Data.GemType.Blue
                };
                JewelsHexaPuzzle.Data.GemTypeHelper.ActiveGemTypeCount = 5;
                Debug.Log("[GameManager] Stage 2: 블록 색상 R/G/B 3색 제약 활성화 (Stage 1과 동일)");
            }
            else
            {
                JewelsHexaPuzzle.Data.GemTypeHelper.AllowedColorsOverride = null;
                // ★ Stage 21+ : 주황색 포함 6색
                int colorCount = (currentGameMode == GameMode.Stage && selectedStage >= 21) ? 6 : 5;
                JewelsHexaPuzzle.Data.GemTypeHelper.ActiveGemTypeCount = colorCount;
                Debug.Log($"[GameManager] Stage {selectedStage}: 활성 색상 {colorCount}색 (5색 + 주황{(colorCount == 6 ? " 포함" : " 미포함")})");
            }

            if (hexGrid != null)
            {
                hexGrid.InitializeGrid();
                yield return new WaitForSeconds(0.3f);

                // 매칭 없는 블록으로 배치
                hexGrid.PopulateWithNoMatches();

                // ★ 아이템 버튼 해금 상태 재동기화 (SyncFeatureUnlocks 후 최신 반영)
                SyncItemButtonVisibility();

                if (blockRemovalSystem != null)
                {
                    // ★ 엘프공주 트랜지션이 끝날 때까지 블록 낙하 시작을 미룬다.
                    //   사용자 요청: "이미지 2초 표시 → 진입 후에 블록 낙하부터 시작"
                    //   PlayTransitionWithImage 사이클이 완전히 끝나야(=화면이 드러나야) TriggerStartDrop.
                    var lto = JewelsHexaPuzzle.UI.LobbyTransitionOverlay.Instance;
                    while (lto != null && lto.IsRunning)
                        yield return null;

                    // ★ 튜토리얼 보드 사전 배치 + 시퀀스 트리거를 트랜지션 종료 후로 이동.
                    //   (이전: 트랜지션 전에 호출돼 엘프공주 이미지와 튜토리얼 대화가 동시에 표시되던 버그)
                    if (TutorialManager.Instance != null)
                        TutorialManager.Instance.OnStageStart(selectedStage);

                    blockRemovalSystem.TriggerStartDrop();
                    while (blockRemovalSystem.IsProcessing)
                        yield return null;
                }
            }

            // ★ 어시스트 시스템 활성화/비활성화 (Stage 1·2 — 흙더미 등 회전 차단 환경 포함)
            //   매칭 불가능 시 재배치 + 10초 미조작 시 힌트 표시
            //   회전 차단 장애물(흙더미·사슬·FixedBlock) 고려한 매칭 가능성 검사
            if (currentGameMode == GameMode.Stage && (selectedStage == 1 || selectedStage == 2))
            {
                var assist = Stage1AssistSystem.Instance;
                if (assist == null)
                {
                    var go = new GameObject("Stage1AssistSystem");
                    go.transform.SetParent(transform, false);
                    assist = go.AddComponent<Stage1AssistSystem>();
                }
                assist.EnableForStage1();
            }
            else
            {
                if (Stage1AssistSystem.Instance != null)
                    Stage1AssistSystem.Instance.Disable();
            }

            // 고블린 시스템 초기화 (고블린 config가 있는 스테이지에서 활성화)
            if (goblinSystem != null && currentGameMode == GameMode.Stage && selectedStage >= 1)
            {
                var goblinConfig = GetGoblinConfigForStage(selectedStage);
                if (goblinConfig != null)
                {
                    goblinSystem.Initialize(hexGrid, goblinConfig);
                    // 고블린 킬 이벤트 → 미션 시스템 연동
                    goblinSystem.OnGoblinKilled -= OnGoblinKilledForMission;
                    goblinSystem.OnGoblinKilled += OnGoblinKilledForMission;
                    // MonsterSpawnController 초기화만 (소환 보류) — 미션별 순차 소환은 미션 UI 등장 시(SetupAndAnimateStageMission)
                    secondWaveTriggered = false;
                    if (MonsterSpawnController.Instance != null)
                    {
                        int totalMission = goblinSystem.GetTotalMissionTargetPublic();
                        yield return StartCoroutine(MonsterSpawnController.Instance.Initialize(totalMission, initialTurns, spawnNow: false));
                    }
                    Debug.Log($"[GameManager] 고블린 시스템 활성화 (몬스터는 미션 UI 등장 시 순차 소환): 스테이지 {selectedStage}");

                    // 고블린 출현 알림 메시지 — 실제 고블린이 등장하는 스테이지만
                    // (Stage 2처럼 missionKillCount=0/maxOnBoard=0 으로 고블린 등장이 없는 스테이지는 제외)
                    if (goblinConfig.missionKillCount > 0 && goblinConfig.maxOnBoard > 0)
                    {
                        ShowFloatingMessage("⚔️ 고블린이 출현! 블록을 떨어뜨려 처치하세요!");
                    }
                }
            }
            else if (goblinSystem != null)
            {
                goblinSystem.CleanupAll();
            }

            // 점수 리셋 (게임 시작 시마다) — 골드는 유지 (PlayerPrefs에서 로드된 누적 골드)
            if (scoreManager != null)
                scoreManager.ResetScore();
            // currentGold는 초기화하지 않음 (Awake에서 PlayerPrefs 로드한 값 유지)

            // 아이템 게임당 사용 횟수 리셋
            if (itemManager != null)
                itemManager.ResetPerGameUsage();

            // ★ 오렌지 리워드 시스템 초기화 + HUD 아이콘 바 재구성
            if (SkillUpgradeOfferSystem.Instance != null)
                SkillUpgradeOfferSystem.Instance.ResetForNewStage();

            UpdateUI();

            // 직접 참조 강제 동기화 — 무브수는 0부터 시작 (카운트업 예정)
            int targetTurns = currentTurns;
            if (hudScoreText != null) hudScoreText.text = "0";
            if (hudTurnText != null) hudTurnText.text = "0";

            // 골드 UI 동기화 (현재 보유 골드 표시)
            if (uiManager != null)
                uiManager.UpdateGoldDisplay(currentGold);

            // Stage 모드 미션 시스템 이벤트 구독 (currentGameMode가 이미 설정된 후)
            Debug.Log($"[GameManager] StartGameCoroutine: currentGameMode={currentGameMode}");
            Debug.Log($"[GameManager] stageManager != null: {stageManager != null}");
            Debug.Log($"[GameManager] blockRemovalSystem != null: {blockRemovalSystem != null}");
            if (currentGameMode == GameMode.Stage && stageManager != null && blockRemovalSystem != null)
            {
                Debug.Log($"[GameManager] Stage mode conditions met, subscribing...");
                // OnGemsRemovedDetailed 구독 제거됨 — OnSingleGemDestroyedForMission으로 통일
                blockRemovalSystem.OnSpecialBlockCreated += HandleSpecialBlockCreatedForStage;
                Debug.Log($"[GameManager] Stage mode: OnSpecialBlockCreated subscription SUCCESS!");
            }
            else
            {
                if (currentGameMode == GameMode.Stage)
                    Debug.LogError($"[GameManager] Stage mode subscription FAILED! stageManager={stageManager != null}, blockRemovalSystem={blockRemovalSystem != null}");
                else
                    Debug.Log($"[GameManager] Non-stage mode ({currentGameMode}), skipping stage subscription.");
            }

            // 게임 중 미션 UI 표시 (Stage 모드 전용 — 무한도전은 OnSurvivalMissionAssigned에서 처리)
            if (currentGameMode == GameMode.Stage && uiManager != null && stageManager != null && stageManager.CurrentStageData != null)
            {
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas != null && stageManager.CurrentStageData.missions.Length > 0)
                {
                    // ★ 활성 미션만 표시 (큐 시스템: 최대 6개)
                    var missions = stageManager.GetActiveMissions();

                    // 미션 등장 애니메이션 (무한도전과 동일한 슬라이드인)
                    yield return StartCoroutine(SetupAndAnimateStageMission(missions));

                    // 각 미션별 마지막 표시 카운트 추적 (인스턴스 필드에 저장)
                    lastDisplayedCounts = new int[missions.Length];
                    for (int i = 0; i < missions.Length; i++)
                        lastDisplayedCounts[i] = missions[i].targetCount;

                    // 미션 진행도 업데이트 콜백 (명명 메서드로 구독 — 해제 가능)
                    stageManager.OnMissionProgressUpdated += HandleMissionProgressUpdated;
                    // ★ 미션 슬롯 교체 시 애니메이션
                    stageManager.OnMissionSlotReplaced += HandleMissionSlotReplaced;
                    // ★ 동적 미션한도: 슬롯 해금 시 연출 + UI 재구축 (멱등 구독)
                    stageManager.OnMissionSlotUnlocked -= HandleMissionSlotUnlocked;
                    stageManager.OnMissionSlotUnlocked += HandleMissionSlotUnlocked;
                    // ★ 미션 완료 보상(이동 횟수) 핸들러 — Stage 모드 per-game 구독.
                    //   (기존 버그: InitializeSystems 1회 구독만 있어 CleanupGameEvents 해제 후 재구독 누락 →
                    //    2번째 게임부터 보상 미적용. 멱등 -=/+= 로 정확히 1회 구독 보장.)
                    stageManager.OnMissionComplete -= HandleMissionComplete;
                    stageManager.OnMissionComplete += HandleMissionComplete;

                    // ★ 대기 미션 인디케이터 (화면 좌상단)
                    if (stageManager.PendingMissionCount > 0)
                        CreatePendingMissionIndicator(canvas);

                    // ★ 동적 미션한도: 잠긴 한도 슬롯 칩 표시 (밴드 Max − 현재 한도)
                    RefreshMissionLockChips(canvas);
                }
            }

            // 게임 BGM 시작
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayGameBGM();

            // 무한모드: 생존 모드 시작 (미션 UI는 OnSurvivalMissionAssigned 콜백에서 생성)
            if (currentGameMode == GameMode.Infinite && missionSystem != null)
            {
                // 이전 레벨 미션 UI 잔존 방지 — 확실히 정리
                if (uiManager != null) uiManager.CleanupGameMissionUI();
                // InfiniteConfig가 있으면 커스텀 설정으로 시작
                if (selectedLevelData?.infiniteConfig != null)
                    missionSystem.StartSurvival(selectedLevelData.infiniteConfig);
                else
                    missionSystem.StartSurvival();
            }

            // 무브수 카운트업 애니메이션 (0 → targetTurns)
            yield return StartCoroutine(AnimateTurnCountUp(targetTurns));

            SetGameState(GameState.Playing);
            Debug.Log("Game Started! State: Playing");
        }

        /// <summary>
        /// 무브수 카운트업 애니메이션 (0 → target)
        /// 1단위로 올라가며 피치 상승 틱 사운드 재생
        /// </summary>
        private IEnumerator AnimateTurnCountUp(int target)
        {
            if (hudTurnText == null || target <= 0) yield break;

            // 총 소요 시간: 0.6~1.2초 (무브수에 따라 가변)
            float totalDuration = Mathf.Clamp(target * 0.04f, 0.6f, 1.2f);
            float interval = totalDuration / target;

            for (int i = 1; i <= target; i++)
            {
                hudTurnText.text = i.ToString();

                // 피치 상승 틱 사운드
                float progress = (float)i / target;
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayCountUpTick(progress);

                // 스케일 펀치 (작은 바운스)
                if (hudTurnText.transform != null)
                    StartCoroutine(TurnCountPulse(hudTurnText.transform));

                yield return new WaitForSeconds(interval);
            }

            // 최종값 확정
            hudTurnText.text = target.ToString();
        }

        /// <summary>
        /// 카운트업 시 숫자 펄스 애니메이션 (1.15 → 1.0 바운스)
        /// </summary>
        private IEnumerator TurnCountPulse(Transform target)
        {
            float duration = 0.08f;
            float elapsed = 0f;
            Vector3 originalScale = Vector3.one;
            Vector3 punchScale = new Vector3(1.15f, 1.15f, 1f);

            // 확대
            while (elapsed < duration * 0.4f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.4f);
                target.localScale = Vector3.Lerp(originalScale, punchScale, t);
                yield return null;
            }

            // 축소 복원
            elapsed = 0f;
            while (elapsed < duration * 0.6f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.6f);
                target.localScale = Vector3.Lerp(punchScale, originalScale, t);
                yield return null;
            }

            target.localScale = originalScale;
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
                            // Gray 블록 생성 방지
                            while (newGem == GemType.Gray)
                                newGem = GemTypeHelper.GetRandom();
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
            {
                processingStartTime = Time.time;
                // ★ 하드 캡 기준 시각 (감사 H7) — systemsActive 리셋과 무관하게 진입 시점 고정.
                //   unscaled 기준이라 timeScale=0 잔류 상황에서도 만료된다.
                hardProcessingStartTime = Time.unscaledTime;
            }

            // ★ Playing 복귀 시 교착(회전 매칭 불가) 체크 예약 — 보드가 안정된 뒤 1회 검사.
            //   회전 매칭 불가 + 망치/스왑/라인 사용 불가 + 같은 색 3개 미존재 → 게임오버.
            if (newState == GameState.Playing)
            {
                if (deadlockCheckCoroutine != null) StopCoroutine(deadlockCheckCoroutine);
                deadlockCheckCoroutine = StartCoroutine(DeadlockCheckCoroutine());
                // ★ 캐스케이드(Processing) 중 도달한 미션 슬롯 해금 크레딧을 보드 안정 후 집행
                DrainPendingSlotUnlocks();
            }


            // 입력 시스템 제어
            // 튜토리얼 활성 중에는 TutorialManager가 입력을 직접 관리하므로 덮어쓰지 않음
            if (inputSystem != null)
            {
                bool tutorialActive = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
                if (tutorialActive)
                {
                    // 튜토리얼 중에는 Playing 전환 시 입력을 자동 활성화하지 않음
                    // (TutorialManager가 ForcedAction/Dialog에서 직접 제어)
                    if (newState != GameState.Playing)
                        inputSystem.SetEnabled(false);
                    // Playing일 때는 튜토리얼의 현재 입력 상태 유지
                }
                else
                {
                    inputSystem.SetEnabled(newState == GameState.Playing);
                }
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

            // ★ 튜토리얼 RotationComplete 이벤트 전달
            if (TutorialManager.Instance != null)
                TutorialManager.Instance.OnRotationComplete(matchFound);

            if (matchFound)
            {
                // 적군 턴 시작
                if (enemySystem != null)
                    enemySystem.OnTurnStart();

                // TimeFreezer 비용 반영
                int turnCost = rotationSystem != null ? rotationSystem.LastRotationCost : 1;

                // 무한모드: 턴 소모 (생존 모드), 스테이지모드: 턴 소모
                if (currentGameMode == GameMode.Infinite)
                {
                    rotationCount++;
                    for (int tc = 0; tc < turnCost; tc++) UseTurn();
                }
                else
                {
                    for (int tc = 0; tc < turnCost; tc++) UseTurn();
                }

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
            // 진행 중이므로 stuck 타이머 리셋
            if (currentState == GameState.Processing)
                processingStartTime = Time.time;

            if (scoreManager != null)
            {
                // 동시 매칭 그룹 수 전달 (동시 다색 매칭 보너스 계산용)
                int matchGroupCount = blockRemovalSystem != null ? blockRemovalSystem.CurrentMatchGroupCount : 1;
                scoreManager.AddMatchScore(blockCount, cascadeDepth, avgPosition, matchGroupCount);

                // 실시간 점수 표시 업데이트
                if (uiManager != null)
                    uiManager.UpdateScoreDisplay(scoreManager.CurrentScore);
            }

            // 미션 체크
            if (stageManager != null)
            {
                stageManager.CheckMissionProgress();
            }
        }

        /// <summary>
        /// Stage 모드 보석 수집 추적
        /// </summary>
        private void HandleStageGemsRemoved(int count, GemType gemType, int cascadeDepth)
        {
            Debug.Log($"[GameManager] HandleStageGemsRemoved: count={count}, gemType={gemType}, cascadeDepth={cascadeDepth}");
            if (stageManager != null)
            {
                stageManager.OnGemCollected(gemType, count);
                Debug.Log($"[GameManager] Called stageManager.OnGemCollected({gemType}, {count})");
            }
            else
            {
                Debug.LogWarning("[GameManager] stageManager is null!");
            }
        }

        /// <summary>
        /// Stage 모드 미션 진행도 UI 업데이트 핸들러
        /// </summary>
        private void HandleMissionProgressUpdated(MissionProgress[] progressArray)
        {
            for (int idx = 0; idx < progressArray.Length; idx++)
            {
                var progress = progressArray[idx];
                int remaining = Mathf.Max(0, progress.mission.targetCount - progress.currentCount);

                // 변경 없으면 스킵
                if (lastDisplayedCounts == null || idx >= lastDisplayedCounts.Length) continue;
                if (remaining == lastDisplayedCounts[idx]) continue;

                // 타겟 텍스트 결정
                Text countText = null;
                if (UIManager.gameMissionCountTexts.Count > idx)
                    countText = UIManager.gameMissionCountTexts[idx];
                else if (idx == 0)
                    countText = UIManager.gameMissionCountText;

                if (countText == null) continue;

                Debug.Log($"[GameManager] Mission[{idx}] count update: {lastDisplayedCounts[idx]} → {remaining}");

                // 코루틴 배열 초기화 (필요 시)
                if (stageMissionCountDownCos == null || stageMissionCountDownCos.Length != lastDisplayedCounts.Length)
                    stageMissionCountDownCos = new Coroutine[lastDisplayedCounts.Length];

                // 기존 코루틴 정지 후 새로 시작
                if (stageMissionCountDownCos[idx] != null)
                    StopCoroutine(stageMissionCountDownCos[idx]);

                int from = lastDisplayedCounts[idx];
                lastDisplayedCounts[idx] = remaining;
                bool isComplete = progress.currentCount >= progress.mission.targetCount;

                stageMissionCountDownCos[idx] = StartCoroutine(
                    StageMissionSequentialCountDown(idx, countText, from, remaining, isComplete));
            }

            // 블록 수집 이펙트 제거됨 (BlockFlyEffectCoroutine)
        }

        /// <summary>
        /// 미션 큐 승격 시 UI 재구성 — 완료 미션 축소 애니메이션 → 재배치 → 새 미션 슬라이드인
        /// </summary>
        /// <summary>
        /// 미션 큐 승격 시 — UI를 재구성하지 않고 기존 프레임 유지.
        /// 대기 미션이 승격되었으므로 lastDisplayedCounts 배열만 확장.
        /// </summary>
        // ============================================================
        // 미션 슬롯 교체 시스템 (대기 미션 → 활성 슬롯)
        // ============================================================

        /// <summary>미션 슬롯이 대기 미션으로 교체될 때 호출</summary>
        private void HandleMissionSlotReplaced(int slotIndex, MissionData newMission)
        {
            Debug.Log($"[GameManager] ★ 미션 슬롯 [{slotIndex}] 교체 → {newMission.description}");

            // 즉시 lastDisplayedCounts 갱신 (OnMissionProgressUpdated가 뒤이어 발생 → 스킵되도록)
            if (lastDisplayedCounts != null && slotIndex < lastDisplayedCounts.Length)
                lastDisplayedCounts[slotIndex] = newMission.targetCount;

            // 기존 카운트다운 코루틴 정지
            if (stageMissionCountDownCos != null && slotIndex < stageMissionCountDownCos.Length)
            {
                if (stageMissionCountDownCos[slotIndex] != null)
                    StopCoroutine(stageMissionCountDownCos[slotIndex]);
                stageMissionCountDownCos[slotIndex] = null;
            }

            // 교체 애니메이션 시작
            StartCoroutine(AnimateMissionSlotReplacement(slotIndex, newMission));

            // ★ 새 미션이 RemoveEnemy면 즉시 몬스터 소환 트리거
            if (newMission.type == MissionType.RemoveEnemy && goblinSystem != null && goblinSystem.IsActive)
            {
                goblinSystem.TriggerImmediateSpawn();
                Debug.Log($"[GameManager] 대기 미션 활성화 → 몬스터 즉시 소환: {newMission.description}");
            }
        }

        // ============================================================
        // ★ 동적 미션 활성한도 — 잠금 슬롯 칩 + 해금 연출 (클로드 디자인 에셋)
        //   시작 한도=밴드 Min, 이동 30 소비마다 StageManager.TryUnlockMissionSlot() → 여기서 연출+재구축.
        // ============================================================
        private GameObject missionLockChipsObj;

        /// <summary>미션 UI 레이아웃 총 슬롯 수 = 활성 미션 + 잠금 칩(Max−현재한도).
        /// 잠금 칩이 포함되면 그 수까지 합산해 행 스케일/2열 전환을 결정한다(사용자 요청).</summary>
        private int GetMissionLayoutTotal(int activeCount)
        {
            int locked = stageManager != null
                ? Mathf.Max(0, stageManager.MaxActiveLimit - stageManager.CurrentActiveLimit)
                : 0;
            return Mathf.Max(1, activeCount + locked);
        }

        /// <summary>미션 슬롯 i의 레이아웃 위치 — 인트로(SetupAndAnimateStageMission)와 동일 산식.
        /// 칩(index >= total)도 같은 흐름으로 연속 배치된다.</summary>
        private Vector2 GetMissionSlotPosition(int index, int total, MissionData[] missionsForWidth, out float scale)
        {
            bool useTwoColumns = total >= 4;
            int leftColCount = useTwoColumns ? 3 : total;
            scale = total >= 3 ? 0.7f : 1.0f;
            float rowSpacing = (90f + 20f) * scale + 5f;
            float leftX = 40f;
            bool leftHasMonster = false;
            if (missionsForWidth != null)
                for (int li = 0; li < leftColCount && li < missionsForWidth.Length; li++)
                    if (missionsForWidth[li] != null && missionsForWidth[li].type == MissionType.RemoveEnemy) { leftHasMonster = true; break; }
            float leftColWidth = leftHasMonster ? 219f : 199f;
            float rightX = useTwoColumns ? (leftX + leftColWidth * scale + 13f) : 40f;
            if (!useTwoColumns || index < leftColCount)
                return new Vector2(leftX, -102f - index * rowSpacing);
            return new Vector2(rightX, -102f - (index - leftColCount) * rowSpacing);
        }

        /// <summary>잠긴 미션 한도 슬롯 칩 갱신 — (밴드 Max − 현재 한도)개를 활성 행 뒤 연속 슬롯 위치에 표시.
        /// 컨테이너 이름이 GameMissionUI_* 라 기존 CleanupGameMissionUI가 자동 정리한다.</summary>
        private void RefreshMissionLockChips(Canvas canvas = null)
        {
            if (missionLockChipsObj != null) { Destroy(missionLockChipsObj); missionLockChipsObj = null; }
            if (stageManager == null) return;
            int locked = stageManager.MaxActiveLimit - stageManager.CurrentActiveLimit;
            if (locked <= 0) return;
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            var prog = stageManager.GetMissionProgress();
            int active = prog != null ? prog.Length : 0;
            var missionsArr = new MissionData[active];
            for (int i = 0; i < active; i++) missionsArr[i] = prog[i].mission;
            int layoutTotal = GetMissionLayoutTotal(active); // ★ 활성+잠금 합산 스케일/배치

            missionLockChipsObj = new GameObject("GameMissionUI_LockChips");
            missionLockChipsObj.transform.SetParent(canvas.transform, false);
            var rootRt = missionLockChipsObj.AddComponent<RectTransform>();
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0, 1); rootRt.pivot = new Vector2(0, 1);
            rootRt.anchoredPosition = Vector2.zero; rootRt.sizeDelta = Vector2.zero;

            var lockSpr = Resources.Load<Sprite>("UI/mission_lock");
            Font lockFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color grayAccent = new Color(0.52f, 0.55f, 0.60f, 1f);   // 회색 세로선 (활성 주황 대비 비활성 위계)
            for (int k = 0; k < locked; k++)
            {
                float sc;
                Vector2 pos = GetMissionSlotPosition(active + k, layoutTotal, missionsArr, out sc);
                float rowH = (90f + 20f) * sc;
                float rowW = 199f * sc;                               // 미션 행(비몬스터) 패널 폭과 동일
                var chip = new GameObject($"LockChip_{k}");
                chip.transform.SetParent(missionLockChipsObj.transform, false);
                var rt = chip.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(rowW, rowH);               // ★ 미션 행과 동일 사이즈
                var bg = chip.AddComponent<Image>();
                UIManager.ApplyMissionPanelSprite(bg, 1f);
                bg.raycastTarget = false;
                var cgChip = chip.AddComponent<CanvasGroup>();
                cgChip.alpha = 0.82f;

                // ★ 좌측 세로 액센트 (다른 미션 UI와 동일 배치 — 패널 바깥 왼쪽 3px 간격, 색상 회색)
                var accent = new GameObject("AccentBar");
                accent.transform.SetParent(chip.transform, false);
                var aRt = accent.AddComponent<RectTransform>();
                float accentW = 4f * sc;
                aRt.anchorMin = new Vector2(0f, 0f); aRt.anchorMax = new Vector2(0f, 1f);
                aRt.pivot = new Vector2(0f, 0.5f);
                aRt.anchoredPosition = new Vector2(-(accentW + 3f), 0f);
                aRt.sizeDelta = new Vector2(accentW, 0f);
                var aImg = accent.AddComponent<Image>();
                aImg.color = grayAccent; aImg.raycastTarget = false;

                // 자물쇠 아이콘 (유지 — 미션 아이콘 자리, 좌측)
                var icon = new GameObject("Lock");
                icon.transform.SetParent(chip.transform, false);
                var irt = icon.AddComponent<RectTransform>();
                float iconSz = 64f * sc;
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f); irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(16f * sc, 0f);
                irt.sizeDelta = new Vector2(iconSz, iconSz);
                var iimg = icon.AddComponent<Image>();
                if (lockSpr != null) iimg.sprite = lockSpr;
                iimg.preserveAspect = true; iimg.raycastTarget = false;

                // ★ 해금까지 카운트다운 숫자 (미션 카운트 자리, 우측) — 이동 소비마다 갱신
                var cntObj = new GameObject("LockCount");
                cntObj.transform.SetParent(chip.transform, false);
                var cRt = cntObj.AddComponent<RectTransform>();
                cRt.anchorMin = new Vector2(0f, 0.5f); cRt.anchorMax = new Vector2(0f, 0.5f);
                cRt.pivot = new Vector2(0f, 0.5f);
                cRt.anchoredPosition = new Vector2(96f * sc, 3f * sc);
                cRt.sizeDelta = new Vector2(90f * sc, 64f * sc);
                var cTxt = cntObj.AddComponent<Text>();
                cTxt.font = lockFont;
                cTxt.fontSize = Mathf.RoundToInt(48f * sc);
                cTxt.fontStyle = FontStyle.Bold;
                cTxt.alignment = TextAnchor.MiddleLeft;
                cTxt.color = new Color(0.80f, 0.82f, 0.87f, 1f);      // 라이트 그레이 (회색 위계)
                cTxt.raycastTarget = false;
                cTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
                cTxt.verticalOverflow = VerticalWrapMode.Overflow;
                var cOl = cntObj.AddComponent<Outline>();
                cOl.effectColor = Color.black; cOl.effectDistance = new Vector2(1, -1);
                cTxt.text = GetLockCountdown(k).ToString();
            }
        }

        /// <summary>잠금 칩 k의 해금까지 남은 이동 수 — 집행된 해금 기준 다음 15 배수까지 + 이후 칩은 +15씩.
        /// 크레딧 부여 후 집행 대기 중(Processing)에는 0으로 표시되고, Playing 복귀 드레인 시 칩이 재구축된다.</summary>
        private int GetLockCountdown(int chipIndex)
        {
            int delivered = grantedSlotUnlocks - pendingSlotUnlocks; // 실제 집행 완료된 해금 수
            int toNext = MISSION_SLOT_UNLOCK_INTERVAL * (delivered + 1 + chipIndex) - turnsConsumedThisStage;
            return Mathf.Max(toNext, 0);
        }

        /// <summary>이동 소비 시 잠금 칩 카운트다운 갱신 (UseTurn에서 호출).</summary>
        private void UpdateMissionLockCountdowns()
        {
            if (missionLockChipsObj == null) return;
            for (int k = 0; k < missionLockChipsObj.transform.childCount; k++)
            {
                var cnt = missionLockChipsObj.transform.GetChild(k).Find("LockCount");
                if (cnt == null) continue;
                var t = cnt.GetComponent<Text>();
                if (t != null) t.text = GetLockCountdown(k).ToString();
            }
        }

        /// <summary>미션 한도 슬롯 해금 핸들러 — 해금 연출 후 미션 행/잠금 칩/대기 인디케이터 재구축.</summary>
        private void HandleMissionSlotUnlocked(int newLimit)
        {
            StartCoroutine(MissionSlotUnlockSequence());
        }

        private IEnumerator MissionSlotUnlockSequence()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield break;

            // 1) 첫 잠금 칩 위치에서 해금 버스트(클로드 디자인) + 자물쇠 팝
            Vector2 fxPos = new Vector2(140f, -160f);
            if (missionLockChipsObj != null && missionLockChipsObj.transform.childCount > 0)
            {
                var crt = (RectTransform)missionLockChipsObj.transform.GetChild(0);
                fxPos = crt.anchoredPosition + new Vector2(crt.sizeDelta.x * 0.5f, -crt.sizeDelta.y * 0.5f);
                // 자물쇠 아이콘 팝(확대+페이드) — 잠김 해제 체감
                var lockIcon = crt.Find("Lock") as RectTransform;
                if (lockIcon != null) StartCoroutine(PopAndFade(lockIcon));
            }

            var burstObj = new GameObject("MissionUnlockBurst");
            burstObj.transform.SetParent(canvas.transform, false);
            var brt = burstObj.AddComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0, 1); brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = fxPos;
            brt.sizeDelta = new Vector2(150f, 150f);
            var bimg = burstObj.AddComponent<Image>();
            bimg.sprite = Resources.Load<Sprite>("UI/mission_unlock_burst");
            bimg.raycastTarget = false;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayUnlockSound(); // ★ 효과음: 슬롯 잠금 해제(전용 차임으로 교체)

            float dur = 0.55f, el = 0f;
            while (el < dur)
            {
                if (burstObj == null) break;
                el += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(el / dur);
                float eased = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
                float s = Mathf.Lerp(0.4f, 1.7f, eased);
                brt.localScale = new Vector3(s, s, 1f);
                brt.localEulerAngles = new Vector3(0f, 0f, t * 90f);
                var c = bimg.color; c.a = 1f - t * t; bimg.color = c;
                yield return null;
            }
            if (burstObj != null) Destroy(burstObj);

            // 2) 미션 행 전체 재구축(새 슬롯 반영) + 잠금 칩 + 대기 인디케이터 갱신
            RebuildMissionRows(canvas);
            RefreshMissionLockChips(canvas);
            UpdatePendingMissionIndicator();

            // 3) 새로 활성화된 미션이 몬스터 미션이면 즉시 소환
            var prog = stageManager != null ? stageManager.GetMissionProgress() : null;
            if (prog != null && prog.Length > 0)
            {
                var newest = prog[prog.Length - 1];
                if (newest != null && !newest.isComplete && newest.mission != null
                    && newest.mission.type == MissionType.RemoveEnemy
                    && goblinSystem != null && goblinSystem.IsActive)
                {
                    goblinSystem.TriggerImmediateSpawn();
                    Debug.Log($"[GameManager] 한도 해금 → 새 미션 몬스터 즉시 소환: {newest.mission.description}");
                }
            }
        }

        /// <summary>자물쇠 아이콘 팝(1→1.35 확대 + 페이드아웃) — 해금 체감 연출.</summary>
        private IEnumerator PopAndFade(RectTransform rt)
        {
            var img = rt != null ? rt.GetComponent<Image>() : null;
            float dur = 0.35f, el = 0f;
            while (el < dur && rt != null)
            {
                el += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(el / dur);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, t);
                if (img != null) { var c = img.color; c.a = 1f - t; img.color = c; }
                yield return null;
            }
        }

        /// <summary>미션 행 전체 재구축 — 한도 해금으로 활성 미션 수가 늘었을 때 레이아웃(스케일/2열)을
        /// 새 총수 기준으로 일괄 재배치. 카운트/진행바는 실제 진행도로 동기화, lastDisplayedCounts 확장.</summary>
        private void RebuildMissionRows(Canvas canvas)
        {
            if (stageManager == null || uiManager == null || canvas == null) return;
            var prog = stageManager.GetMissionProgress();
            if (prog == null || prog.Length == 0) return;

            // 진행 중 카운트다운 코루틴 정지 (행 파괴 전)
            if (stageMissionCountDownCos != null)
                foreach (var co in stageMissionCountDownCos)
                    if (co != null) StopCoroutine(co);

            uiManager.CleanupGameMissionUI(); // GameMissionUI_* 전부 제거(잠금 칩 포함) + 정적 리스트 리셋
            missionLockChipsObj = null;

            int total = prog.Length;
            int layoutTotal = GetMissionLayoutTotal(total); // ★ 활성+잠금 합산으로 스케일/2열 결정
            var missionsArr = new MissionData[total];
            for (int i = 0; i < total; i++) missionsArr[i] = prog[i].mission;

            for (int i = 0; i < total; i++)
            {
                var rowRt = uiManager.CreateIndividualMissionRow(canvas, missionsArr[i], i, layoutTotal);
                if (rowRt == null) continue;
                float sc;
                rowRt.anchoredPosition = GetMissionSlotPosition(i, layoutTotal, missionsArr, out sc);
                var cgRow = rowRt.GetComponent<CanvasGroup>();
                if (cgRow != null) cgRow.alpha = 1f;
            }

            // 카운트/진행바 동기화 + lastDisplayedCounts 확장 (미확장 시 새 슬롯 카운트 갱신 누락)
            var newCounts = new int[total];
            for (int i = 0; i < total; i++)
            {
                int remaining = Mathf.Max(0, prog[i].mission.targetCount - prog[i].currentCount);
                newCounts[i] = remaining;
                if (UIManager.gameMissionCountTexts.Count > i && UIManager.gameMissionCountTexts[i] != null)
                    UIManager.gameMissionCountTexts[i].text = remaining.ToString();
                UIManager.UpdateMissionRowProgress(i, remaining);
            }
            lastDisplayedCounts = newCounts;
            stageMissionCountDownCos = new Coroutine[total];
            Debug.Log($"[GameManager] ★ 미션 행 재구축: {total}행 (한도 {stageManager.CurrentActiveLimit}/{stageManager.MaxActiveLimit})");
        }

        /// <summary>
        /// 미션 슬롯 교체 애니메이션: 완료 행 축소 제거 → 대기 미션 슬라이드인 → 활성화
        /// </summary>
        private IEnumerator AnimateMissionSlotReplacement(int slotIndex, MissionData newMission)
        {
            // Phase 0: 완료 이펙트(체크마크) 잠깐 보여주기
            yield return new WaitForSeconds(0.5f);

            // Phase 1: 완료된 미션 행 축소 → 사라짐
            string rowName = $"GameMissionUI_Row_{slotIndex}";
            GameObject oldRow = GameObject.Find(rowName);
            Vector2 targetPosition = new Vector2(40f, -102f); // 기본 위치

            if (oldRow != null)
            {
                RectTransform oldRt = oldRow.GetComponent<RectTransform>();
                if (oldRt != null)
                    targetPosition = oldRt.anchoredPosition;

                CanvasGroup oldCg = oldRow.GetComponent<CanvasGroup>();
                if (oldCg == null) oldCg = oldRow.AddComponent<CanvasGroup>();
                Vector3 startScale = oldRt != null ? oldRt.localScale : Vector3.one;

                // 축소 애니메이션 (0.3초)
                float shrinkDuration = 0.3f;
                float elapsed = 0f;
                while (elapsed < shrinkDuration)
                {
                    if (oldRow == null || oldRt == null) break;
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / shrinkDuration);
                    float eased = t * t; // ease-in
                    oldRt.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                    oldCg.alpha = 1f - eased;
                    yield return null;
                }

                if (oldRow != null) Destroy(oldRow);
            }

            yield return new WaitForSeconds(0.15f);

            // Phase 2: 대기 인디케이터 위치에서 새 행 생성 → 슬롯 위치로 슬라이드
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield break;

            int totalMissions = stageManager != null ? GetMissionLayoutTotal(stageManager.ActiveMissionCount) : 6;

            // 새 미션 행 생성 (UIManager 활용)
            RectTransform newRt = uiManager.CreateIndividualMissionRow(canvas, newMission, slotIndex, totalMissions);
            if (newRt == null) yield break;

            // gameMissionCountTexts 순서 정리 (CreateIndividualMissionRow가 끝에 추가하므로 올바른 인덱스로 이동)
            int lastIdx = UIManager.gameMissionCountTexts.Count - 1;
            if (lastIdx > slotIndex && slotIndex < UIManager.gameMissionCountTexts.Count)
            {
                UIManager.gameMissionCountTexts[slotIndex] = UIManager.gameMissionCountTexts[lastIdx];
                UIManager.gameMissionCountTexts.RemoveAt(lastIdx);
            }
            // ★ 진행바 fill/target 리스트도 동일하게 정렬 (행 인덱스 동기 유지)
            int lastFillIdx = UIManager.gameMissionProgressFills.Count - 1;
            if (lastFillIdx > slotIndex && slotIndex < UIManager.gameMissionProgressFills.Count)
            {
                UIManager.gameMissionProgressFills[slotIndex] = UIManager.gameMissionProgressFills[lastFillIdx];
                UIManager.gameMissionProgressFills.RemoveAt(lastFillIdx);
            }
            int lastTgtIdx = UIManager.gameMissionTargets.Count - 1;
            if (lastTgtIdx > slotIndex && slotIndex < UIManager.gameMissionTargets.Count)
            {
                UIManager.gameMissionTargets[slotIndex] = UIManager.gameMissionTargets[lastTgtIdx];
                UIManager.gameMissionTargets.RemoveAt(lastTgtIdx);
            }

            // 시작 위치: 대기 인디케이터 위치 (화면 좌상단)
            Vector2 startPos = pendingIndicatorObj != null
                ? pendingIndicatorObj.GetComponent<RectTransform>().anchoredPosition
                : new Vector2(12, -12);
            float startScaleVal = 0.5f;

            newRt.anchoredPosition = startPos;
            newRt.localScale = Vector3.one * startScaleVal;

            CanvasGroup newCg = newRt.GetComponent<CanvasGroup>();
            if (newCg == null) newCg = newRt.gameObject.AddComponent<CanvasGroup>();
            newCg.alpha = 0.5f;

            // 슬라이드 애니메이션 (0.4초, EaseOutBack)
            float slideDuration = 0.4f;
            float slideElapsed = 0f;

            while (slideElapsed < slideDuration)
            {
                if (newRt == null) yield break;
                slideElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(slideElapsed / slideDuration);
                float eased = VisualConstants.EaseOutBack(t);

                newRt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPosition, eased);
                float s = Mathf.LerpUnclamped(startScaleVal, 1f, eased);
                newRt.localScale = new Vector3(s, s, 1f);
                newCg.alpha = Mathf.Lerp(0.5f, 1f, t);

                yield return null;
            }

            // 최종값 확정
            if (newRt != null)
            {
                newRt.anchoredPosition = targetPosition;
                newRt.localScale = Vector3.one;
            }
            if (newCg != null) newCg.alpha = 1f;

            // Phase 3: 활성화 펄스
            yield return new WaitForSeconds(0.05f);
            if (newRt != null)
            {
                float pulseDuration = 0.15f;
                float pulseElapsed = 0f;
                while (pulseElapsed < pulseDuration)
                {
                    if (newRt == null) yield break;
                    pulseElapsed += Time.unscaledDeltaTime;
                    float t = pulseElapsed / pulseDuration;
                    float pulse = t < 0.5f
                        ? Mathf.Lerp(1f, 1.15f, t * 2f)
                        : Mathf.Lerp(1.15f, 1f, (t - 0.5f) * 2f);
                    newRt.localScale = new Vector3(pulse, pulse, 1f);
                    yield return null;
                }
                newRt.localScale = Vector3.one;
            }

            // 추적 데이터 갱신
            if (lastDisplayedCounts != null && slotIndex < lastDisplayedCounts.Length)
                lastDisplayedCounts[slotIndex] = newMission.targetCount;

            // 대기 인디케이터 업데이트
            UpdatePendingMissionIndicator();
        }

        // ============================================================
        // 대기 미션 인디케이터 (화면 좌상단, 아이콘+숫자)
        // ============================================================

        /// <summary>대기 미션 인디케이터 생성 — 화면 좌상단에 다음 대기 미션 아이콘+목표수 표시</summary>
        private void CreatePendingMissionIndicator(Canvas canvas)
        {
            if (pendingIndicatorObj != null) Destroy(pendingIndicatorObj);
            if (stageManager == null || stageManager.PendingMissionCount <= 0) return;

            var nextPending = stageManager.PeekNextPendingMission();
            if (nextPending == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 활성 미션 수에 따른 대기 인디케이터 크기 조정
            // 활성 2개 이하: 활성 UI가 크므로 대기 UI를 20% 축소하여 겹침 방지
            int totalActive = stageManager.ActiveMissionCount;
            float activeScale = totalActive >= 3 ? 0.7f : 1.0f;
            float pendingScale = totalActive <= 2 ? activeScale * 0.76f : activeScale * 0.96f;
            float rowHeight = 90f * pendingScale;
            int fontSize = Mathf.RoundToInt(48f * pendingScale);

            // ★ 대기(Pending) 미션도 활성 미션과 동일하게 몬스터 얼굴 2배 + 수량 우측 이동 (공용 헬퍼)
            var pm = UIManager.GetMissionRowMetrics(nextPending, pendingScale, unifiedBigIcon: true);
            float containerWidth = pm.containerWidth;
            float iconSize = pm.iconSize;
            float iconX = pm.iconX;
            float countX = pm.countX;
            float countW = pm.countW;

            // 컨테이너 — 화면 좌상단
            pendingIndicatorObj = new GameObject("PendingMissionIndicator");
            pendingIndicatorObj.transform.SetParent(canvas.transform, false);
            RectTransform rt = pendingIndicatorObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -12);
            rt.sizeDelta = new Vector2(containerWidth, rowHeight + 20f * pendingScale);

            // 배경 (클로드 디자인 다크글래스 9-slice — 활성 미션과 동일 스프라이트로 통일.
            //  CanvasGroup α0.7가 대기 위계를 표현하므로 색은 불투명으로 둠. 기존 라벤더는 우주 배경과 부조화라 폐기.)
            Image bg = pendingIndicatorObj.AddComponent<Image>();
            UIManager.ApplyMissionPanelSprite(bg, 1f);
            bg.raycastTarget = false;
            // ★ 대기 미션은 "전체적으로 회색" 처리 — 패널을 그레이스케일 머티리얼로 탈채도(파란 글래스→회색 글래스)
            var grayMat = UIManager.GetGrayscaleMaterial();
            if (grayMat != null) bg.material = grayMat;

            // 반투명 표시
            CanvasGroup cg = pendingIndicatorObj.AddComponent<CanvasGroup>();
            cg.alpha = 0.7f;

            // 미션 아이콘
            GameObject iconObj = new GameObject("PendingIcon");
            iconObj.transform.SetParent(pendingIndicatorObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0, 0.5f);
            iconRt.anchorMax = new Vector2(0, 0.5f);
            iconRt.pivot = new Vector2(0, 0.5f);
            iconRt.anchoredPosition = new Vector2(iconX, 0);
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);

            pendingIndicatorIcon = iconObj.AddComponent<Image>();
            pendingIndicatorIcon.raycastTarget = false;
            if (uiManager != null)
                uiManager.SetMissionIconForType(pendingIndicatorIcon, nextPending);

            // ★ 아이콘 + 오버레이(방패/십자 등) 전부 회색화 (대기 미션은 비활성 위계)
            if (grayMat != null)
                foreach (var img in iconObj.GetComponentsInChildren<Image>(true))
                    img.material = grayMat;
            else
                pendingIndicatorIcon.color = new Color(0.55f, 0.56f, 0.6f, pendingIndicatorIcon.color.a); // 폴백 탈채도 근사

            Outline iconOutline = iconObj.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0.78f, 0.80f, 0.84f, 1f); // 회색 톤에 맞춘 라이트 아웃라인
            iconOutline.effectDistance = new Vector2(1, 1);

            // 목표 수 텍스트
            GameObject countObj = new GameObject("PendingTargetCount");
            countObj.transform.SetParent(pendingIndicatorObj.transform, false);
            RectTransform countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0, 0.5f);
            countRt.anchorMax = new Vector2(0, 0.5f);
            countRt.pivot = new Vector2(0, 0.5f);
            countRt.anchoredPosition = new Vector2(countX, 6f); // 통일 레이아웃: 활성 미션과 동일하게 위로 6px (이동보상과 간격)
            countRt.sizeDelta = new Vector2(countW, iconSize);

            Text countText = countObj.AddComponent<Text>();
            countText.font = font;
            countText.fontSize = fontSize;
            countText.fontStyle = FontStyle.Bold;
            countText.alignment = TextAnchor.MiddleLeft;
            countText.color = new Color(0.82f, 0.84f, 0.88f); // 라이트 쿨그레이 — 회색 톤에 맞추되 가독성 유지
            countText.raycastTarget = false;
            countText.text = nextPending.targetCount.ToString();

            Outline countOutline = countObj.AddComponent<Outline>();
            countOutline.effectColor = Color.black;
            countOutline.effectDistance = new Vector2(1, -1);

            // ★ 이동 보상 '이동 +N' 표시 — 활성 미션과 동일 위치/스타일 (사용자 요청: 대기 미션에도 표시).
            //   CanvasGroup α0.7로 대기 위계는 자동 반영. AttachMoveRewardBadge가 우하단 정렬 + 스케일 처리.
            if (uiManager != null)
                uiManager.AttachMoveRewardBadge(nextPending, pendingIndicatorObj.transform, pendingScale);

            // 대기 잔여 수 배지 (2개 이상일 때만 표시)
            if (stageManager.PendingMissionCount > 1)
            {
                GameObject badgeObj = new GameObject("PendingBadge");
                badgeObj.transform.SetParent(pendingIndicatorObj.transform, false);
                RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(1, 1);
                badgeRt.anchorMax = new Vector2(1, 1);
                badgeRt.pivot = new Vector2(1, 1);
                badgeRt.anchoredPosition = new Vector2(10, 9);
                badgeRt.sizeDelta = new Vector2(42, 38); // 헥사(가로>세로) 비율 반영

                // ★ 대기 수량 배지 — 클로드 디자인 골드 헥사 스프라이트(회색 UI 위 포인트 컬러). 폴백=기존 빨강 원.
                Image badgeBg = badgeObj.AddComponent<Image>();
                Sprite badgeSpr = Resources.Load<Sprite>("UI/pending_badge");
                if (badgeSpr != null) { badgeBg.sprite = badgeSpr; badgeBg.color = Color.white; badgeBg.preserveAspect = true; }
                else badgeBg.color = new Color(0.9f, 0.3f, 0.2f, 0.95f);
                badgeBg.raycastTarget = false;

                GameObject numObj = new GameObject("BadgeNum");
                numObj.transform.SetParent(badgeObj.transform, false);
                RectTransform numRt = numObj.AddComponent<RectTransform>();
                numRt.anchorMin = new Vector2(0.5f, 0.5f);
                numRt.anchorMax = new Vector2(0.5f, 0.5f);
                numRt.pivot = new Vector2(0.5f, 0.5f);
                // 숫자 글리프 광학 보정 — 숫자마다 사이드베어링이 달라(4=좌편향, 8=대칭) 고정 nudge는 전 숫자
                // 만족 불가 → 두 극단의 중간값(+1.3px)으로 모든 숫자를 ±1.3px(시각상 중앙) 내로. 로비 배지와 동일.
                numRt.anchoredPosition = new Vector2(1.3f, 0f);
                numRt.sizeDelta = new Vector2(42, 38);

                pendingIndicatorCountText = numObj.AddComponent<Text>();
                pendingIndicatorCountText.font = font;
                pendingIndicatorCountText.fontSize = 20;
                pendingIndicatorCountText.fontStyle = FontStyle.Bold;
                pendingIndicatorCountText.alignment = TextAnchor.MiddleCenter;
                pendingIndicatorCountText.color = Color.white;
                pendingIndicatorCountText.raycastTarget = false;
                pendingIndicatorCountText.text = stageManager.PendingMissionCount.ToString();
                // 골드 헥사 위 흰 숫자 가독성 — 다크 골드 아웃라인
                Outline badgeNumOutline = numObj.AddComponent<Outline>();
                badgeNumOutline.effectColor = new Color(0.30f, 0.18f, 0.02f, 0.9f);
                badgeNumOutline.effectDistance = new Vector2(1.2f, -1.2f);
            }
        }

        /// <summary>대기 인디케이터 업데이트 — 다음 미션 아이콘/숫자 갱신, 0이면 제거</summary>
        private void UpdatePendingMissionIndicator()
        {
            if (stageManager == null) return;

            int remaining = stageManager.PendingMissionCount;
            if (remaining <= 0)
            {
                // 대기 미션 없음 → 인디케이터 제거
                if (pendingIndicatorObj != null) Destroy(pendingIndicatorObj);
                pendingIndicatorObj = null;
                pendingIndicatorCountText = null;
                pendingIndicatorIcon = null;
                return;
            }

            // 인디케이터 재생성 (다음 미션 아이콘/숫자 업데이트)
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
                CreatePendingMissionIndicator(canvas);
        }

        /// <summary>
        /// Infinite 모드 미션 진행도 UI 업데이트 핸들러
        /// </summary>
        private void HandleInfiniteMissionProgressUpdated(MissionProgress[] missionProgress)
        {
            if (uiManager == null) return;
            for (int i = 0; i < missionProgress.Length; i++)
            {
                var progress = missionProgress[i];
                uiManager.UpdateMissionProgress(i, progress.currentCount, progress.mission.targetCount);
            }
        }

        /// <summary>
        /// 미션 완료 핸들러
        /// </summary>
        private void HandleMissionComplete(int missionIndex)
        {
            Debug.Log($"[GameManager] Mission {missionIndex} completed!");

            // ★ 미션 완료 보상: 이동 횟수 지급 (미션별 1~5 균일 랜덤) — 마지막 미션도 지급(사용자 요청).
            //   - 마지막 미션(=스테이지 클리어 직전)은 클리어 시퀀스가 연출/HUD를 가리기 전에 '즉시' 지급.
            //   - 보상값은 완료된 미션 인스턴스(GetActiveMissions[missionIndex])에 저장된 롤을 읽어 프리뷰 배지와 일치.
            if (currentGameMode == GameMode.Stage && stageManager != null)
            {
                var activeForReward = stageManager.GetActiveMissions();
                MissionData completedMission = (missionIndex >= 0 && missionIndex < activeForReward.Length)
                    ? activeForReward[missionIndex] : null;
                int reward = MissionBalance.GetOrAssignMoveReward(completedMission);
                int captured = reward;
                // 이 시점엔 완료된 미션이 이미 isComplete 처리됨 → true면 '마지막 미션(스테이지 클리어 직전)'.
                bool isFinalMission = stageManager.IsMissionComplete();
                if (reward > 0 && uiManager != null)
                {
                    if (isFinalMission)
                    {
                        // ★ 마지막 미션(스테이지 클리어 직전): 이동 보상을 '즉시' 지급(사용자 요청).
                        //   주의: 이 추가 턴은 곧 스테이지가 끝나 직접 쓸 순 없지만, 클리어 보너스
                        //   (남은턴 → 보너스 드릴/골드/점수)에 반영된다 = 마지막 미션도 '실질 보상'을 받는 의도된 동작.
                        //   (구버전은 남은턴×보상 인플레 우려로 미지급했으나, 사용자 요청으로 지급으로 전환)
                        //   흰 영혼은 연출만 — 중복 적용 방지로 onApply=null.
                        AddTurns(captured);
                        uiManager.PlayMoveRewardSoul(reward, missionIndex, null);
                    }
                    else
                    {
                        // 일반 미션: 흰 영혼이 이동횟수 HUD에 도착하는 '순간' AddTurns 적용 (연출·수치 싱크).
                        //   (출발/목표 못 찾으면 PlayMoveRewardSoul이 즉시 onApply로 폴백 — 데이터 안전)
                        uiManager.PlayMoveRewardSoul(reward, missionIndex, () => AddTurns(captured));
                    }
                }
                else if (reward > 0)
                {
                    AddTurns(reward);
                }
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayMissionCompleteSound();
                Debug.Log($"[GameManager] 미션 완료 보상: 이동 +{reward} (Lv{selectedStage}, 마지막={isFinalMission})");
            }
        }

        /// <summary>
        /// 이전 게임 이벤트 정리 (중복 구독 방지)
        /// </summary>
        private void CleanupGameEvents()
        {
            if (stageManager != null)
            {
                stageManager.OnMissionProgressUpdated -= HandleMissionProgressUpdated;
                stageManager.OnMissionSlotReplaced -= HandleMissionSlotReplaced;
                stageManager.OnMissionProgressUpdated -= HandleInfiniteMissionProgressUpdated;
                stageManager.OnMissionComplete -= HandleMissionComplete;
            }
            if (blockRemovalSystem != null)
            {
                // OnGemsRemovedDetailed 구독 제거됨 — OnSingleGemDestroyedForMission으로 통일
                blockRemovalSystem.OnSpecialBlockCreated -= HandleSpecialBlockCreatedForStage;
            }
        }

        /// <summary>
        /// 특수 블록 생성 시 StageManager 미션 진행 알림 (블록별 개별 미션용)
        /// </summary>
        private void HandleSpecialBlockCreatedForStage(SpecialBlockType type, DrillDirection drillDir)
        {
            if (stageManager != null)
            {
                stageManager.OnSpecialBlockCreatedDetailed(type, drillDir);
                Debug.Log($"[GameManager] 특수 블록 생성 미션 카운트: {type} (드릴방향: {drillDir})");
            }
        }

        /// <summary>
        /// 특수 블록(드릴, 폭탄 등)이 파괴한 기본 블록 미션 카운팅 (하위호환)
        /// </summary>
        public void OnSpecialBlockDestroyedBasicBlocks(int basicBlockCount, string specialBlockType)
        {
            if (currentGameMode != GameMode.Stage) return;
            if (basicBlockCount <= 0) return;

            Debug.Log($"[GameManager] 💣 특수블록 파괴: {specialBlockType}로 기본블록 {basicBlockCount}개 제거");

            if (stageManager != null)
            {
                stageManager.OnGemCollected(GemType.None, basicBlockCount);
            }
        }

        /// <summary>
        /// 특수 블록이 파괴한 블록들을 색상별로 미션에 카운팅
        /// 각 특수 블록 시스템에서 파괴 대상의 gemType별 개수를 Dictionary로 전달
        /// </summary>
        public void OnSpecialBlockDestroyedBlocksByColor(Dictionary<GemType, int> gemCounts, string specialBlockType)
        {
            if (gemCounts == null || gemCounts.Count == 0) return;

            int total = 0;
            foreach (var kvp in gemCounts)
                total += kvp.Value;

            Debug.Log($"[GameManager] 💣 특수블록 파괴(색상별): {specialBlockType}로 총 {total}개 제거");

            // Stage 모드: stageManager에 보고
            if (currentGameMode == GameMode.Stage && stageManager != null)
            {
                foreach (var kvp in gemCounts)
                {
                    if (kvp.Value > 0)
                    {
                        Debug.Log($"  → {kvp.Key}: {kvp.Value}개");
                        stageManager.OnGemCollected(kvp.Key, kvp.Value);
                    }
                }
            }

            // 무한도전 모드: missionSystem에 보고
            if (currentGameMode == GameMode.Infinite && missionSystem != null)
            {
                missionSystem.OnSpecialBlockDestroyedByColor(gemCounts);
            }
        }

        // ★ 노랑 젬 1개 제거당 골드 (특수블록 색상 매핑: Yellow→Gold). 필요시 밸런스 조정.
        private const int GOLD_PER_YELLOW_GEM = 1;

        /// <summary>
        /// 특수 블록(드릴 등)이 개별 블록 1개를 파괴할 때 미션 카운팅
        /// 드릴의 연출과 동기화하여 1개씩 보고
        /// </summary>
        public void OnSingleGemDestroyedForMission(GemType gemType)
        {
            // 미션 보고 + 모든 리소스 게이지/수치 충전 (특수블록 등 영혼 연출이 없는 파괴 경로용)
            ReportGemForMissionOnly(gemType);
            ChargeResourcesForGem(gemType);
        }

        /// <summary>
        /// ★ 색상 1개 → 대응 리소스 게이지/수치 전부 충전 (미션 카운트 제외).
        /// Red→망치, Green→스왑, Purple→라인, Orange→리워드, Blue→마나, Yellow→골드.
        /// "미션엔 안 넣지만 게이지는 채워야 하는" 경로용 — 특수 블록 자신의 색상 소모 등.
        /// (ChargeGaugeForGem은 망치/스왑/라인/리워드만 다뤄 Blue·Yellow가 누락되던 것을 여기서 완성)
        /// </summary>
        public void ChargeResourcesForGem(GemType gemType)
        {
            ChargeGaugeForGem(gemType); // 망치/스왑/라인 + 주황 리워드 + 노랑 골드
            if (gemType == GemType.Blue && MPManager.Instance != null)
                MPManager.Instance.AddMP(1);
        }

        /// <summary>★ 미션 카운트 없이 게이지/리워드 보상만 1배 충전 — 라인 아이템 2배 보상 등 "추가 보상"용.
        ///   비깨짐: ChargeResourcesForGem(아이템게이지+주황RW+노랑골드+파랑MP). 깨짐/쉘: ChargeBrokenGem 규칙.</summary>
        public void ChargeBlockRewardOnly(GemType gemType, bool isBroken, bool isShell = false)
        {
            if (gemType == GemType.None) return;
            if (!isBroken) ChargeResourcesForGem(gemType);
            else ChargeBrokenGem(gemType, isShell);
        }

        /// <summary>
        /// ★ 깨진/쉘 블록 규칙 통일 오버로드 (감사 M11).
        /// 매칭 정화 경로는 깨진(isCracked)/점령(isShell) 블록을 미션 카운트에서 제외하고 게이지를 절반(+1)만
        /// 충전하는데, 특수블록/아이템 경로는 1인자 버전을 호출해 깨진 블록도 미션에 집계되는 비대칭이 있었다.
        /// 모든 파괴 경로는 가능하면 이 오버로드에 isBroken을 전달할 것.
        /// </summary>
        public void OnSingleGemDestroyedForMission(GemType gemType, bool isBroken, bool isShell = false)
        {
            if (!isBroken)
            {
                OnSingleGemDestroyedForMission(gemType);
                return;
            }
            // 깨진/쉘 블록: 미션 제외, 아이템 게이지(+1) + (쉘 아니면) MP·RW — ChargeBrokenGem으로 규칙 통일
            ChargeBrokenGem(gemType, isShell);
        }

        /// <summary>
        /// ★ 특수 블록(드릴/폭탄/드론/X/도넛 등)이 파괴한 블록 — 미션은 즉시 보고하고,
        /// 제거된 색상의 "영혼"이 대응 게이지로 날아가 도착할 때 게이지를 충전한다(부딪힘과 수치 증가 싱크).
        /// 일반 매칭(BlockRemovalSystem orbArrived)과 동일한 영혼 연출을 특수 블록 파괴 경로에도 부여.
        /// worldPos = 파괴되는 블록의 월드 위치(영혼 출발점).
        /// </summary>
        /// <summary>★ 파괴 시 영혼 억제 대상 블록 — 쉘(고블린에게 색을 빼앗긴) 또는 적(고블린)에게 점령된 블록.
        ///   이런 블록의 색은 플레이어 것이 아니므로 영혼이 나오지 않는다(충전은 유지).</summary>
        public static bool IsSoulSuppressedBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return false;
            if (block.Data.isShell) return true;                       // 색을 빼앗긴 쉘
            return GoblinSystem.Instance != null && GoblinSystem.Instance.HasGoblinAt(block.Coord); // 적 점령
        }

        public void OnSingleGemDestroyedForMission(GemType gemType, bool isBroken, Vector3 worldPos, bool suppressSoul = false)
        {
            if (gemType == GemType.None) return;
            // 미션 카운트는 즉시(게임 진행 로직). 깨진/쉘은 미션 제외.
            if (!isBroken) ReportGemForMissionOnly(gemType);
            // ★ 쉘(색 빼앗김) / 적(고블린) 점령 블록은 플레이어의 것이 아니므로 영혼도, 게이지 증가도 없다.
            //   (사용자 요청: 점령 블록은 증가 안 되는 게 맞고, 증가 안 되는 영혼이 나오는 것만 문제)
            if (suppressSoul) return;
            // 영혼이 게이지에 "도착하는 순간" 충전 — 연출과 수치 증가를 일치.
            //   (대응 게이지가 없거나 비활성이면 Spawn이 콜백을 즉시 호출 → 충전 누락 없음)
            JewelsHexaPuzzle.UI.GaugeOrbEffect.Spawn(worldPos, gemType, () =>
            {
                if (isBroken) ChargeBrokenGem(gemType);
                else ChargeResourcesForGem(gemType);
            });
        }

        /// <summary>★ 특수 블록 자신의 색상 — 영혼이 대응 게이지로 날아가 도착할 때 충전(미션 미집계).</summary>
        public void ChargeResourcesForGemWithSoul(GemType gemType, Vector3 worldPos)
        {
            if (gemType == GemType.None) return;
            JewelsHexaPuzzle.UI.GaugeOrbEffect.Spawn(worldPos, gemType, () => ChargeResourcesForGem(gemType));
        }

        /// <summary>깨진/쉘 블록 게이지 충전 — 아이템 게이지(+1)는 항상, 마나(파란)·리워드(주황)는 색을 잃지 않은(쉘 아님) 블록만.
        /// (사용자 요청 2026-06: 깨진 파란/주황 블록도 MP·RW 게이지 충전. 색이 빼앗긴 쉘은 제외.)</summary>
        private void ChargeBrokenGem(GemType gemType, bool isShell = false)
        {
            if (gemType == GemType.None) return;
            if (gemType == GemType.Red && JewelsHexaPuzzle.Items.HammerGauge.Instance != null)
                JewelsHexaPuzzle.Items.HammerGauge.Instance.AddGauge(1);
            if (gemType == GemType.Green && JewelsHexaPuzzle.Items.SwapGauge.Instance != null)
                JewelsHexaPuzzle.Items.SwapGauge.Instance.AddGauge(1);
            if (gemType == GemType.Purple && JewelsHexaPuzzle.Items.LineGauge.Instance != null)
                JewelsHexaPuzzle.Items.LineGauge.Instance.AddGauge(1);
            if (ItemManager.Instance != null)
                ItemManager.Instance.AddGauge(gemType, 1);
            if (JewelsHexaPuzzle.UI.ItemGaugeController.Instance != null)
                JewelsHexaPuzzle.UI.ItemGaugeController.Instance.OnBlockRemoved(gemType);

            // ★ 마나(파란)·리워드(주황): 색을 잃지 않은(쉘 아님) 깨진 블록도 충전
            if (!isShell)
            {
                if (gemType == GemType.Blue && MPManager.Instance != null)
                    MPManager.Instance.AddMP(1);
                if (gemType == GemType.Orange && SkillUpgradeOfferSystem.Instance != null)
                    SkillUpgradeOfferSystem.Instance.OnOrangeDestroyed();
            }
        }

        /// <summary>
        /// 미션 보고만 수행 (게이지/리워드/마나 충전 제외).
        /// 매칭 정화 경로에서 "영혼 오브 도착 시점"에 게이지를 충전하기 위해 분리한 진입점.
        /// 미션 판정/게임 진행에 영향을 주므로 블록 제거 즉시 호출한다.
        /// </summary>
        public void ReportGemForMissionOnly(GemType gemType)
        {
            if (gemType == GemType.None) return;

            // Stage 모드: stageManager에 보고
            if (currentGameMode == GameMode.Stage && stageManager != null)
            {
                stageManager.OnGemCollected(gemType, 1);
            }

            // 무한도전 모드: missionSystem에 보고
            if (currentGameMode == GameMode.Infinite)
            {
                if (missionSystem != null)
                {
                    missionSystem.OnGemsRemoved(1, gemType, 0);
                }
                else
                {
                    Debug.LogWarning($"[GameManager] ReportGemForMissionOnly: missionSystem is null! gemType={gemType}");
                }
            }
        }

        /// <summary>
        /// 아이템 게이지 + 리워드 게이지 충전 (일반 블록 1개 몫).
        /// 정화된 영혼 오브가 게이지 UI에 "도착하는 순간" 호출되어 연출과 수치 증가를 싱크한다.
        /// (마나(파란)는 색상별 매핑이 달라 호출처에서 MPManager.AddMP로 별도 처리)
        /// </summary>
        public void ChargeGaugeForGem(GemType gemType)
        {
            // ★ 아이템 게이지 충전 (모든 블록 파괴 경로에서 자동 적용) — 블록 1개당 +1 (단계 임계값 10/8/6/4 = 대략 필요 블록 수)
            if (gemType == GemType.Red && JewelsHexaPuzzle.Items.HammerGauge.Instance != null)
                JewelsHexaPuzzle.Items.HammerGauge.Instance.AddGauge(1);
            if (gemType == GemType.Green && JewelsHexaPuzzle.Items.SwapGauge.Instance != null)
                JewelsHexaPuzzle.Items.SwapGauge.Instance.AddGauge(1);
            if (gemType == GemType.Purple && JewelsHexaPuzzle.Items.LineGauge.Instance != null)
                JewelsHexaPuzzle.Items.LineGauge.Instance.AddGauge(1);

            // ★ 주황 블록 10개 제거당 스킬 1개 학습 기회 (스테이지 21+)
            if (gemType == GemType.Orange && SkillUpgradeOfferSystem.Instance != null)
                SkillUpgradeOfferSystem.Instance.OnOrangeDestroyed();

            // ★ 노랑(별) 블록 → 골드 즉시 증가. 모든 파괴 경로(일반 매칭 orbArrived + 특수 블록)에서 자동 적용.
            //   노랑은 대응 게이지 UI가 없어 영혼 오브 타겟이 없으므로 onArrived가 즉시 발동 → 골드가 바로 오른다.
            if (gemType == GemType.Yellow)
                AddGold(GOLD_PER_YELLOW_GEM);
        }

        /// <summary>
        /// 흙더미 1개 완전 제거 시 호출 (BlockRemovalSystem 매칭에서 dirtMound = 0이 될 때).
        /// Stage 모드에서 RemoveDirtMound 미션 진행도 +1.
        /// </summary>
        public void OnDirtMoundRemovedForMission()
        {
            if (currentGameMode != GameMode.Stage || stageManager == null) return;
            stageManager.OnDirtMoundRemoved();
        }

        // ============================================================
        // 튜토리얼 이벤트 핸들러
        // ============================================================

        /// <summary>
        /// 튜토리얼 시작 시 — 게임 상태를 보존하고 입력 제어를 TutorialManager에 위임
        /// </summary>
        private void OnTutorialStarted()
        {
            Debug.Log("[GameManager] 튜토리얼 시작 감지 — 입력 제어를 TutorialManager에 위임");
            // 현재 Playing 상태라면 입력 비활성화 (튜토리얼이 직접 제어)
            if (currentState == GameState.Playing && inputSystem != null)
                inputSystem.SetEnabled(false);
        }

        /// <summary>
        /// 튜토리얼 종료 시 — Playing 상태로 복원하고 입력 활성화
        /// </summary>
        private void OnTutorialEnded()
        {
            Debug.Log("[GameManager] 튜토리얼 종료 — Playing 상태 + 입력 활성화");
            if (currentState == GameState.Playing && inputSystem != null)
                inputSystem.SetEnabled(true);
        }

        /// <summary>
        /// 아이템 버튼 해금 상태 동기화 — IsFeatureUnlocked()에 따라 표시/숨김
        /// HideLobby(), StartGameCoroutine() 등에서 호출하여 항상 최신 상태 반영
        /// </summary>
        private void SyncItemButtonVisibility()
        {
            var tm = TutorialManager.Instance;
            // TutorialManager 없으면 모든 아이템 버튼 숨김 (안전 기본값)
            bool hammerOK = tm != null && tm.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_HAMMER);
            bool swapOK = tm != null && tm.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_SWAP);
            bool lineDrawOK = tm != null && tm.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_LINEDRAW);
            bool reverseOK = tm != null && tm.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_REVERSE);

            if (hammerButtonObj != null) hammerButtonObj.SetActive(hammerOK);
            if (swapButtonObj != null) swapButtonObj.SetActive(swapOK);
            if (lineDrawButtonObj != null) lineDrawButtonObj.SetActive(lineDrawOK);
            if (reverseRotationButtonObj != null) reverseRotationButtonObj.SetActive(reverseOK);
        }

        /// <summary>
        /// 기능 해금 시 — 아이템 버튼 표시
        /// </summary>
        private void OnFeatureUnlocked(string featureId)
        {
            Debug.Log($"[GameManager] 기능 해금 감지: {featureId}");
            switch (featureId)
            {
                case TutorialManager.FEATURE_ITEM_HAMMER:
                    if (hammerButtonObj != null) hammerButtonObj.SetActive(true);
                    break;
                case TutorialManager.FEATURE_ITEM_SWAP:
                    if (swapButtonObj != null) swapButtonObj.SetActive(true);
                    break;
                case TutorialManager.FEATURE_ITEM_LINEDRAW:
                    if (lineDrawButtonObj != null) lineDrawButtonObj.SetActive(true);
                    break;
                case TutorialManager.FEATURE_ITEM_REVERSE:
                    if (reverseRotationButtonObj != null) reverseRotationButtonObj.SetActive(true);
                    break;
            }
        }

        /// <summary>
        /// 연쇄 완료 이벤트
        /// </summary>
private void OnCascadeComplete()
        {
            // ★ 튜토리얼 CascadeComplete 이벤트 전달
            if (TutorialManager.Instance != null)
                TutorialManager.Instance.OnCascadeComplete();

            // Loading/Lobby 상태에서 호출된 경우 무시
            if (currentState == GameState.Loading || currentState == GameState.Lobby || currentState == GameState.StageClear)
            {
                Debug.Log($"[GameManager] OnCascadeComplete skipped - in {currentState} state");
                return;
            }

            // ProcessSpecialBlockAftermath가 실행 중이면 상태 복원을 그쪽에서 처리
            if (isProcessingChainDrill)
            {
                // 안전장치: ProcessSpecialBlockAftermath가 실제로 진행 중인지 확인
                // 마지막 진행 보고로부터 10초 이상 지났으면 코루틴이 죽은 것으로 간주
                float timeSinceProgress = Time.time - lastAftermathProgressTime;
                if (timeSinceProgress > 10f)
                {
                    Debug.LogWarning($"[GameManager] OnCascadeComplete: isProcessingChainDrill=true but no progress for {timeSinceProgress:F1}s! Resetting orphaned flag.");
                    isProcessingChainDrill = false;
                    // 아래로 계속 진행 (정상 OnCascadeComplete 처리)
                }
                else
                {
                    Debug.Log("[GameManager] OnCascadeComplete skipped - ProcessSpecialBlockAftermath is managing state");
                    return;
                }
            }

            // 아이템 액션이면 회색 블록 생성 없이 Playing 복귀
            if (isItemAction)
            {
                isItemAction = false;
                if (currentState == GameState.Processing)
                    processingStartTime = Time.time; // STUCK 방지

                // ★ 아이템으로 마지막 미션을 완성한 경우에도 스테이지 클리어를 판정한다.
                //   아이템 액션은 일반 턴 종료(OnCascadeCompleteTurnEnd)를 건너뛰므로
                //   미션 완료 체크가 누락돼 게임이 끝나지 않던 버그 수정. (스테이지 모드 한정)
                if (currentGameMode == GameMode.Stage && stageManager != null && stageManager.IsMissionComplete())
                {
                    StageClear();
                    return;
                }

                // ★ 아이템 액션 후에도 회전 매칭 데드락 체크 (재배치/게임오버) — 아이템은 턴 종료를 건너뛰므로 여기서 처리
                if (matchingSystem != null && !matchingSystem.HasPossibleMoves())
                {
                    if (HandleRotationDeadlock()) return;   // 게임오버면 중단
                }

                SetGameState(GameState.Playing);
                return;
            }

            // ★ 턴 종료 처리 전체를 try-catch로 보호
            // 예외 발생 시 Processing 상태에 영구 고착되는 것을 방지
            try
            {
                OnCascadeCompleteTurnEnd();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameManager] OnCascadeComplete 턴 종료 처리 예외! Playing으로 강제 복귀.\n{e.Message}\n{e.StackTrace}");
                SetGameState(GameState.Playing);
                if (inputSystem != null) inputSystem.SetEnabled(true);
            }
        }

        /// <summary>
        /// OnCascadeComplete에서 분리된 턴 종료 처리 (예외 보호용)
        /// </summary>
        private void OnCascadeCompleteTurnEnd()
        {
            // 턴 종료 보너스 (복수 생성 + 멀티킬)
            if (scoreManager != null)
            {
                int turnBonus = scoreManager.ApplyTurnEndBonuses();
                if (turnBonus > 0 && uiManager != null)
                    uiManager.UpdateScoreDisplay(scoreManager.CurrentScore);
            }

            // 시한폭탄 체크
            CheckTimeBombs();

            // 적군 턴 종료 처리 (쌍둥이 재생, 포자 확산, 카오스 리롤)
            // try-catch로 보호: 적군 처리 예외 시 미션 처리가 스킵되는 것 방지
            if (enemySystem != null)
            {
                try { enemySystem.OnTurnEnd(); }
                catch (System.Exception e) { Debug.LogError($"[GameManager] enemySystem.OnTurnEnd 예외: {e.Message}\n{e.StackTrace}"); }
            }

            // ★ 아이템 게이지: 턴 종료 시 +3 충전
            if (JewelsHexaPuzzle.Items.HammerGauge.Instance != null)
                JewelsHexaPuzzle.Items.HammerGauge.Instance.OnTurnEnd();
            if (JewelsHexaPuzzle.Items.SwapGauge.Instance != null)
                JewelsHexaPuzzle.Items.SwapGauge.Instance.OnTurnEnd();
            if (JewelsHexaPuzzle.Items.LineGauge.Instance != null)
                JewelsHexaPuzzle.Items.LineGauge.Instance.OnTurnEnd();

            // 무한모드: 미션 턴 종료 → 게임오버 체크 → 적군 스폰
            if (currentGameMode == GameMode.Infinite)
            {
                if (missionSystem != null)
                    missionSystem.OnTurnEnd();

                // 이동횟수 0 또는 필드 전체 쉘 잠식 → 게임오버 (동일 처리)
                if (currentTurns <= 0 || IsFieldFullyShelled())
                {
                    GameOver(IsFieldFullyShelled() ? GameOverReason.FieldShelled : GameOverReason.MovesExhausted);
                    return;
                }

                int enemyCount = 3 + (rotationCount / 10);
                StartCoroutine(SafeSpawnEnemiesAndCheckMoves(enemyCount));
                return;
            }

            // 스테이지모드: 미션 완료 확인
            if (stageManager != null && stageManager.IsMissionComplete())
            {
                StageClear();
                return;
            }

            // 스테이지모드: 게임오버 확인 (이동횟수 0 또는 필드 전체 쉘 잠식)
            if (currentTurns <= 0 || IsFieldFullyShelled())
            {
                GameOver(IsFieldFullyShelled() ? GameOverReason.FieldShelled : GameOverReason.MovesExhausted);
                return;
            }

            // 스테이지모드: EnemySystem으로 적군 스폰
            StartCoroutine(SafeSpawnEnemiesAndPlay());
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
            // StageClear/GameOver 상태에서는 후처리 불필요 (클리어 시퀀스가 관리)
            if (currentState == GameState.StageClear || currentState == GameState.GameOver)
                return;

            // ★ 지원폭격 중에는 전용 캐스케이드(SupportBombingCascade)가 처리 — 정상 후처리(턴종료/적군스폰) 우회
            if (isSupportBombing)
                return;

            // 진행 중이므로 stuck 타이머 리셋
            if (currentState == GameState.Processing)
                processingStartTime = Time.time;

            Debug.Log($"[GameManager] Special block completed! Score: {score}");
            if (scoreManager != null)
            {
                int cascadeDepth = blockRemovalSystem != null ? blockRemovalSystem.CurrentCascadeDepth : 0;
                scoreManager.AddSpecialBlockScore(score, cascadeDepth, Vector3.zero);

                // 실시간 점수 표시 업데이트
                if (uiManager != null)
                    uiManager.UpdateScoreDisplay(scoreManager.CurrentScore);
            }

            // 복구 중이면 무시 (이중 처리 방지)
            if (isInPostRecovery) return;

            if (isProcessingChainDrill)
            {
                // ProcessSpecialBlockAftermath 진행 추적 갱신 (orphan 감지용)
                lastAftermathProgressTime = Time.time;
                return;
            }

            // BlockRemovalSystem 내부 cascade에서 특수블록이 발동된 경우
            // 이미 내부적으로 연쇄 처리 중이므로 GameManager가 개입하면 데드락 발생
            if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) return;

            // 유저 직접 클릭으로 발동된 경우 (턴 차감은 InputSystem에서 발동 시작 시 처리)
            SetGameState(GameState.Processing);
            StartCoroutine(SafeProcessSpecialBlockAftermath());
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
            Debug.Log("[GameManager] WaitForBRSReady: BRS is processing, waiting..."); // 정상 핸드셰이크 알림 — Warning→Log 강등
            float waited = 0f;
            int lastDepth = blockRemovalSystem.CurrentCascadeDepth;
            while (blockRemovalSystem.IsProcessing && waited < 10f)
            {
                waited += Time.deltaTime;
                processingStartTime = Time.time; // stuck 오판 방지
                int curDepth = blockRemovalSystem.CurrentCascadeDepth;
                if (curDepth != lastDepth)
                {
                    lastDepth = curDepth;
                    waited = 0f;
                }
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
            int lastCascadeDepth = blockRemovalSystem.CurrentCascadeDepth;
            float lastHeartbeat = blockRemovalSystem.LastProgressTime;
            while (blockRemovalSystem.IsProcessing && elapsed < 15f)
            {
                elapsed += Time.deltaTime;
                processingStartTime = Time.time; // stuck 오판 방지
                // ★ 이중 턴 종료 방지(2026-07-03): aftermath가 BRS를 정상 대기하는 동안
                //   OnCascadeComplete의 10초 고아 판정이 오발하지 않도록 진행 타임스탬프 갱신.
                //   진짜 고아(코루틴 사망) 시에는 갱신 주체가 없어 워치독 기능은 그대로 유지된다.
                if (isProcessingChainDrill) lastAftermathProgressTime = Time.time;
                // 연쇄 깊이가 변하면 진행 중이므로 타이머 리셋
                int curDepth = blockRemovalSystem.CurrentCascadeDepth;
                if (curDepth != lastCascadeDepth)
                {
                    lastCascadeDepth = curDepth;
                    elapsed = 0f;
                }
                // BRS 내부 하트비트가 갱신되면 진행 중이므로 타이머 리셋
                float curHeartbeat = blockRemovalSystem.LastProgressTime;
                if (curHeartbeat != lastHeartbeat)
                {
                    lastHeartbeat = curHeartbeat;
                    elapsed = 0f;
                }
                yield return null;
            }
            if (blockRemovalSystem.IsProcessing)
            {
                Debug.LogError($"[GameManager] {callerName} timeout after {elapsed:F1}s! Force resetting.");
                blockRemovalSystem.ForceReset();
            }
        }

        /// <summary>
        /// ProcessSpecialBlockAftermath의 안전한 래퍼 — 예외 발생 시에도 isProcessingChainDrill 리셋 + Playing 전환 보장
        /// </summary>
        private IEnumerator SafeProcessSpecialBlockAftermath()
        {
            bool completed = false;
            try
            {
                yield return StartCoroutine(ProcessSpecialBlockAftermath());
                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    Debug.LogError("[GameManager] ProcessSpecialBlockAftermath 예외 발생! 플래그 리셋 + Playing 강제 복귀.");
                    isProcessingChainDrill = false;
                    lastAftermathProgressTime = 0f;
                    if (currentState == GameState.Processing)
                        SetGameState(GameState.Playing);
                    if (inputSystem != null) inputSystem.SetEnabled(true);
                }
            }
        }

private IEnumerator ProcessSpecialBlockAftermath()
        {
            yield return new WaitForSeconds(0.1f);
            isProcessingChainDrill = true;
            lastAftermathProgressTime = Time.time;

            int maxLoops = 20;
            int loop = 0;

            while (loop < maxLoops)
            {
                loop++;

                // 루프 진행 시 stuck 타이머 리셋 + aftermath 진행 추적
                processingStartTime = Time.time;
                lastAftermathProgressTime = Time.time;

                // 1. 모든 특수 블록 시스템의 pending 목록 클리어
                if (drillSystem != null) drillSystem.PendingSpecialBlocks.Clear();
                if (bombSystem != null) bombSystem.PendingSpecialBlocks.Clear();
                if (donutSystem != null) donutSystem.PendingSpecialBlocks.Clear();
                if (xBlockSystem != null) xBlockSystem.PendingSpecialBlocks.Clear();
                if (droneSystem != null) droneSystem.PendingSpecialBlocks.Clear();

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
                    Debug.Log("[GameManager] BRS still processing before falling. Waiting..."); // 정상 핸드셰이크 알림 — Warning→Log 강등
                    float waited = 0f;
                    int lastDepth = blockRemovalSystem.CurrentCascadeDepth;
                    // ★ 타임아웃 10초 — 드릴 쿠션 반사 + 다수 특수 블록 연쇄 시 5초 초과 가능
                    while (blockRemovalSystem.IsProcessing && waited < 10f)
                    {
                        waited += Time.deltaTime;
                        processingStartTime = Time.time; // stuck 오판 방지
                        // ★ 이중 턴 종료 방지: 정상 대기 중 고아 오판 차단 (WaitForBRSComplete와 동일 패턴)
                        if (isProcessingChainDrill) lastAftermathProgressTime = Time.time;
                        int curDepth = blockRemovalSystem.CurrentCascadeDepth;
                        if (curDepth != lastDepth)
                        {
                            lastDepth = curDepth;
                            waited = 0f;
                        }
                        // ★ 드릴/폭탄 등 특수 블록 활성 중이면 진행 중으로 간주 → 타이머 리셋
                        if ((drillSystem != null && drillSystem.IsDrilling)
                            || (bombSystem != null && bombSystem.IsBombing)
                            || (donutSystem != null && donutSystem.IsActivating)
                            || (droneSystem != null && droneSystem.IsActivating))
                        {
                            waited = 0f;
                        }
                        yield return null;
                    }
                    if (blockRemovalSystem.IsProcessing)
                    {
                        Debug.LogWarning("[GameManager] BRS timeout before falling — Force resetting.");
                        blockRemovalSystem.ForceReset();
                        if (drillSystem != null && drillSystem.IsDrilling) drillSystem.ForceReset();
                        yield return null;
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

            // 적군 턴 종료 처리 (예외 보호)
            if (enemySystem != null)
            {
                try { enemySystem.OnTurnEnd(); }
                catch (System.Exception e) { Debug.LogError($"[GameManager] Aftermath enemySystem.OnTurnEnd 예외: {e.Message}\n{e.StackTrace}"); }
            }

            // 무한모드: 미션 턴 종료 → 게임오버 체크 → 적군 스폰
            if (currentGameMode == GameMode.Infinite)
            {
                if (missionSystem != null)
                {
                    try { missionSystem.OnTurnEnd(); }
                    catch (System.Exception e) { Debug.LogError($"[GameManager] Aftermath missionSystem.OnTurnEnd 예외: {e.Message}\n{e.StackTrace}"); }
                }

                // 이동횟수 0 또는 필드 전체 쉘 잠식 → 게임오버 (동일 처리)
                if (currentTurns <= 0 || IsFieldFullyShelled())
                {
                    GameOver(IsFieldFullyShelled() ? GameOverReason.FieldShelled : GameOverReason.MovesExhausted);
                    yield break;
                }

                int enemyCount = 3 + (rotationCount / 10);
                yield return StartCoroutine(SafeSpawnEnemiesAndCheckMoves(enemyCount));
                Debug.Log("[GameManager] ProcessSpecialBlockAftermath completed (Infinite) -> Playing");
                yield break;
            }

            if (stageManager != null && stageManager.IsMissionComplete())
            {
                StageClear();
                yield break;
            }

            // 이동횟수 0 또는 필드 전체 쉘 잠식 → 게임오버 (동일 처리)
            if (currentTurns <= 0 || IsFieldFullyShelled())
            {
                GameOver(IsFieldFullyShelled() ? GameOverReason.FieldShelled : GameOverReason.MovesExhausted);
                yield break;
            }

            // EnemySystem을 통한 적군 스폰 후 Playing 전환
            yield return StartCoroutine(SafeSpawnEnemiesAndPlay());
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

            // stuck 타이머 리셋 + aftermath 진행 추적
            processingStartTime = Time.time;
            lastAftermathProgressTime = Time.time;

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
                        // ★ 드릴 타임아웃 10초 — 쿠션 반사 시 5초 초과 가능
                        float drillTimeout = 10f;
                        while (drillSystem.IsBlockActive(block) && waited < drillTimeout)
                        {
                            waited += Time.deltaTime;
                            processingStartTime = Time.time;
                            if (drillSystem.IsDrilling) waited = Mathf.Min(waited, drillTimeout * 0.5f);
                            yield return null;
                        }
                        if (drillSystem.IsBlockActive(block)) { Debug.LogError("[GM] Drill timeout!"); drillSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Bomb:
                    if (bombSystem != null)
                    {
                        bombSystem.ActivateBomb(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (bombSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; processingStartTime = Time.time; yield return null; }
                        if (bombSystem.IsBlockActive(block)) { Debug.LogError("[GM] Bomb timeout!"); bombSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Rainbow:
                    if (donutSystem != null)
                    {
                        donutSystem.ActivateDonut(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (donutSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; processingStartTime = Time.time; yield return null; }
                        if (donutSystem.IsBlockActive(block)) { Debug.LogError("[GM] Donut timeout!"); donutSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.XBlock:
                    if (xBlockSystem != null)
                    {
                        xBlockSystem.ActivateXBlock(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (xBlockSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; processingStartTime = Time.time; yield return null; }
                        if (xBlockSystem.IsBlockActive(block)) { Debug.LogError("[GM] XBlock timeout!"); xBlockSystem.ForceReset(); }
                    }
                    break;

                case SpecialBlockType.Drone:
                    if (droneSystem != null)
                    {
                        droneSystem.ActivateDrone(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (droneSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; processingStartTime = Time.time; yield return null; }
                        if (droneSystem.IsBlockActive(block)) { Debug.LogError("[GM] Drone timeout!"); droneSystem.ForceReset(); }
                    }
                    break;
            }
        }


        
private void OnBigBang()
        {
            Debug.Log("BIG BANG triggered!");
        }

        /// <summary>
        /// 외부에서 턴 1회 차감 (특수 블록 직접 클릭 시 InputSystem에서 호출)
        /// </summary>
        public void UseOneTurn()
        {
            UseTurn();
            Debug.Log($"[GameManager] UseOneTurn() called, remaining={currentTurns}");
        }

        /// <summary>
        /// 턴 사용
        /// </summary>
        // ★ 동적 미션한도: 이번 스테이지 누적 소비 이동 (이동보상과 무관하게 단조증가).
        //   15 소비마다 미션 활성한도 슬롯 1개 해금 (밴드 최대까지) — 30→15 (2026-07-02 사용자).
        private int turnsConsumedThisStage = 0;
        private const int MISSION_SLOT_UNLOCK_INTERVAL = 15;
        // ★ 해금 버그 수정(2026-07-03): 회전 경로의 UseTurn()은 항상 Processing 상태라
        //   "Playing && %15==0" 게이트를 영원히 통과 못 함 → 누적 크레딧 + Playing 복귀 시 집행으로 전환.
        private int grantedSlotUnlocks = 0;  // 이번 스테이지에 부여된 해금 크레딧 누계 (15 소비마다 +1)
        private int pendingSlotUnlocks = 0;  // 아직 집행되지 않은 해금 크레딧 (Playing 복귀 시 드레인)

        /// <summary>미집행 미션 슬롯 해금 크레딧을 집행 — Playing 상태(보드 안정)에서 호출.</summary>
        private void DrainPendingSlotUnlocks()
        {
            while (pendingSlotUnlocks > 0 && stageManager != null)
            {
                pendingSlotUnlocks--;
                stageManager.TryUnlockMissionSlot();
                // ★ 해금으로 대기가 줄어 "대기 ≤ 잠금"이 되면 나머지도 즉시 자동 해금
                stageManager.AutoUnlockIfPendingFits();
            }
        }

        private void UseTurn()
        {
            currentTurns--;
            OnTurnChanged?.Invoke(currentTurns);
            UpdateUI();

            // ★ 동적 미션한도: 누적 소비 15마다 해금 크레딧 부여 (무한모드 제외).
            //   나머지 연산이 아닌 누적 나눗셈이라 프레임을 놓쳐도 다음 UseTurn에서 따라잡는다.
            //   집행(연출·승격·소환)은 Playing 상태에서만 — Processing 중이면 복귀 시 드레인.
            turnsConsumedThisStage++;
            if (currentGameMode != GameMode.Infinite && stageManager != null)
            {
                int owed = turnsConsumedThisStage / MISSION_SLOT_UNLOCK_INTERVAL;
                if (owed > grantedSlotUnlocks)
                {
                    pendingSlotUnlocks += owed - grantedSlotUnlocks;
                    grantedSlotUnlocks = owed;
                    if (currentState == GameState.Playing) DrainPendingSlotUnlocks(); // 특수블록 직접 클릭 경로는 즉시 집행
                }
            }
            UpdateMissionLockCountdowns(); // ★ 잠금 칩 카운트다운 갱신 (해금까지 남은 이동)

            // ★ 역회전 충전 게이지: 이동횟수 소모마다 +1 (10회 = 풀충전)
            if (ItemManager.Instance != null) ItemManager.Instance.OnMoveConsumed();

            // 턴 부족 경고 사운드 (3턴 이하)
            if (currentTurns <= 3 && currentTurns > 0 && AudioManager.Instance != null)
                AudioManager.Instance.PlayWarningBeep();

            // MonsterSpawnController에 턴 종료 알림 (규칙2: 40% 이하 전부 소환, 규칙3: 3마리 이하 추가 소환)
            if (goblinSystem != null && goblinSystem.IsActive && MonsterSpawnController.Instance != null)
            {
                MonsterSpawnController.Instance.OnTurnEnd(currentTurns);
            }

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
                uiManager.UpdateGoldDisplay(currentGold);

                if (scoreManager != null)
                {
                    uiManager.UpdateScoreDisplay(scoreManager.CurrentScore);
                }
            }

            // 직접 참조 동기화 (UIManager 연동 실패 안전망)
            RefreshScoreDisplay();
            RefreshTurnDisplay();
        }

        private void RefreshScoreDisplay()
        {
            if (hudScoreText != null && scoreManager != null)
                JewelsHexaPuzzle.Utils.NumberRoller.Roll(hudScoreText, scoreManager.CurrentScore, v => string.Format("{0:N0}", v));

            // 최고 점수 HUD 실시간 갱신 (현재 점수가 최고를 넘으면 표시 업데이트)
            if (scoreManager != null)
            {
                int currentScore = scoreManager.CurrentScore;
                int levelBest = scoreManager.GetLevelHighScore(selectedStage);
                int personalBest = scoreManager.GetPersonalLevelBest(selectedStage);

                if (hudLevelBestText != null)
                {
                    int displayLevelBest = currentScore > levelBest ? currentScore : levelBest;
                    JewelsHexaPuzzle.Utils.NumberRoller.Roll(hudLevelBestText, displayLevelBest,
                        v => v > 0 ? string.Format("BEST: {0:N0}", v) : "BEST: ---");
                    // 새 기록이면 색상 강조
                    hudLevelBestText.color = currentScore > levelBest && levelBest > 0
                        ? new Color(1f, 1f, 0.3f, 1f)
                        : new Color(1f, 0.85f, 0.3f, 0.9f);
                }

                if (hudPersonalBestText != null)
                {
                    int displayPersonalBest = currentScore > personalBest ? currentScore : personalBest;
                    JewelsHexaPuzzle.Utils.NumberRoller.Roll(hudPersonalBestText, displayPersonalBest,
                        v => v > 0 ? string.Format("MY BEST: {0:N0}", v) : "MY BEST: ---");
                    hudPersonalBestText.color = currentScore > personalBest && personalBest > 0
                        ? new Color(0.5f, 1f, 0.5f, 1f)
                        : new Color(0.7f, 0.9f, 1f, 0.9f);
                }
            }
        }

        private void RefreshTurnDisplay()
        {
            if (hudTurnText != null)
                JewelsHexaPuzzle.Utils.NumberRoller.Roll(hudTurnText, currentTurns, v => v.ToString());
        }

        /// <summary>
        /// 스테이지 클리어
        /// </summary>
        private void StageClear()
        {
            // 기존 Processing 코루틴 모두 중단
            StopAllCoroutines();

            // Processing 플래그 정리
            isProcessingChainDrill = false;
            isInPostRecovery = false;

            // 특수 블록 시스템 리셋 (진행 중인 이펙트/코루틴 정리)
            if (blockRemovalSystem != null) blockRemovalSystem.ForceReset();
            if (drillSystem != null) drillSystem.ForceReset();
            if (bombSystem != null) bombSystem.ForceReset();
            if (donutSystem != null) donutSystem.ForceReset();
            if (xBlockSystem != null) xBlockSystem.ForceReset();
            if (droneSystem != null) droneSystem.ForceReset();

            SetGameState(GameState.StageClear);
            processingStartTime = Time.time; // StageClear 워치독 타이머 시작
            OnStageClear?.Invoke();

            // 다음 레벨 해금
            LevelRegistry.UnlockLevel(selectedStage + 1);

            // 레벨 해금에 연동된 기능(특수블록/아이템) 동기화
            if (TutorialManager.Instance != null)
                TutorialManager.Instance.SyncFeatureUnlocks();

            if (inputSystem != null)
                inputSystem.SetEnabled(false);

            StartCoroutine(StageClearSequence());
        }

        /// <summary>
        /// 스테이지 클리어 시퀀스: 이펙트 없이 즉시 결과 처리 → 클리어 팝업
        /// </summary>
        private IEnumerator StageClearSequence()
        {
            // BGM 정지
            if (AudioManager.Instance != null)
                AudioManager.Instance.StopBGM();

            Debug.Log($"Level {currentStage} Clear!");

            // ★ 드릴 변환 전 남은 턴수 스냅샷 (보상 계산용 — 1턴당 1골드)
            int turnsBeforeDrillConversion = currentTurns;
            TurnsAtStageClear = currentTurns; // 학습 fitness용: 미션 완료 순간 남은 이동(드릴 변환 전)

            // 남은 이동횟수만큼 랜덤 위치에 보너스 드릴 생성 (각 드릴 위치에 "+1" 골드 팝업)
            processingStartTime = Time.time;
            yield return StartCoroutine(SpawnBonusDrills(currentTurns));

            // ★ 페스티벌 모드 활성화 — 마지막 드릴 발동 + 캐스케이드 = 클리어 피날레 불꽃놀이
            //   점수 팝업이 더 크고 화려해짐 (1.6× 폰트 + 황금색 + 12방향 불꽃 스파크)
            var scorePopupMgr = Object.FindObjectOfType<JewelsHexaPuzzle.UI.ScorePopupManager>();
            if (scorePopupMgr != null) scorePopupMgr.SetFestivalMode(true);

            // 모든 특수 블록 동시 발동 (기존 + 새로 생성된 드릴 포함)
            processingStartTime = Time.time;
            yield return StartCoroutine(ActivateAllSpecialBlocks());

            // 모든 시스템이 완전히 멈출 때까지 대기
            processingStartTime = Time.time;
            yield return StartCoroutine(WaitForAllSystemsIdle());

            // 낙하 + 연쇄 매칭 처리 (완전 정지될 때까지 반복)
            processingStartTime = Time.time;
            yield return StartCoroutine(ProcessStageClearAftermath());

            // ★ 페스티벌 모드 종료 — 클리어 팝업 표시 시 일반 점수로 복귀
            if (scorePopupMgr != null) scorePopupMgr.SetFestivalMode(false);

            // 남은 이동횟수 스냅샷 — 드릴 변환 전 값 사용 (각 턴이 드릴+1골드로 전환됨)
            int remainingTurns = turnsBeforeDrillConversion;

            // ★ 보상 계산: SkillTreeManager의 최초 클리어 추적 시스템 사용
            int goldReward = 0;
            int spReward = 0;
            if (SkillTreeManager.Instance != null)
            {
                var rewards = SkillTreeManager.Instance.CalculateAndGrantClearRewards(selectedStage, remainingTurns);
                goldReward = rewards.goldReward;
                spReward = rewards.spReward;
            }
            else
            {
                // 폴백: 남은 이동횟수 × 1골드
                goldReward = remainingTurns;
            }

            // 점수 보너스 계산 (턴 초기화 전에 수행)
            StageSummaryData? summary = null;
            if (scoreManager != null)
                summary = scoreManager.CalculateStageClearBonus(remainingTurns, initialTurns);

            // 레벨별 최고 점수 갱신 (기존 ScoreManager 랭킹 시스템 — 유지)
            if (scoreManager != null)
                scoreManager.TryUpdateLevelHighScore(selectedStage);

            // 레벨별 획득 별 영속 저장 (로비 노드 표시용 — 최소 1성=클리어)
            if (scoreManager != null && summary.HasValue)
                scoreManager.SaveLevelStars(selectedStage, Mathf.Max(1, summary.Value.starRating));

            // 턴 0으로 표시
            currentTurns = 0;
            if (uiManager != null)
                uiManager.UpdateTurnDisplay(currentTurns);

            // 골드 즉시 지급
            if (goldReward > 0)
                AddGold(goldReward);

            // ★ 다음 레벨 해금
            int nextLevel = selectedStage + 1;
            LevelRegistry.UnlockLevel(nextLevel);
            Debug.Log($"[GameManager] 레벨 {nextLevel} 해금!");

            // 특수 블록 연쇄 완료 후 1초 대기 후 클리어 팝업 표시
            yield return new WaitForSeconds(1f);

            ShowStageClearResultPopup(summary, goldReward);

            // 팝업 표시 후 워치독 타임아웃 방지 (사용자가 확인 버튼 누를 때까지 대기)
            processingStartTime = float.MaxValue;
        }

        /// <summary>
        /// 클리어 결과 팝업 표시. 클로드 디자인 새 팝업(StageClearPopup)이 씬에 있으면 그것으로,
        /// 없으면 기존 UIManager 팝업으로 폴백한다.
        /// </summary>
        private void ShowStageClearResultPopup(StageSummaryData? summary, int goldReward)
        {
            int score = summary.HasValue ? summary.Value.totalScore
                       : (scoreManager != null ? scoreManager.CurrentScore : 0);

            bool newReady = Object.FindObjectOfType<HexaPuzzle.StageClear.StageClearPopup>(true) != null;
            if (newReady)
            {
                // 별 갯수 + 색상 = 게임 난이도 (Easy 1/녹색, Normal 2/금색, Hard 3/적색)
                int starCount; Color starColor;
                var lvl = LevelRegistry.GetLevel(selectedStage);
                DifficultyType diff = lvl != null ? lvl.difficultyType : DifficultyType.Normal;
                switch (diff)
                {
                    case DifficultyType.Easy: starCount = 1; starColor = new Color(0.30f, 0.82f, 0.40f, 1f); break;
                    case DifficultyType.Hard: starCount = 3; starColor = new Color(0.90f, 0.32f, 0.28f, 1f); break;
                    default:                  starCount = 2; starColor = new Color(1.00f, 0.82f, 0.22f, 1f); break;
                }

                // LEVEL BEST / MY BEST — 0에서 시작하는 전용 PlayerPrefs 추적
                string lvlKey = "sc_levelbest_" + selectedStage;
                const string myKey = "sc_mybest";
                int prevLevelBest = PlayerPrefs.GetInt(lvlKey, 0);
                int prevMyBest    = PlayerPrefs.GetInt(myKey, 0);
                bool newLevel = score > prevLevelBest;
                bool newMy    = score > prevMyBest;
                if (newLevel) PlayerPrefs.SetInt(lvlKey, score);
                if (newMy)    PlayerPrefs.SetInt(myKey, score);
                PlayerPrefs.Save();

                HexaPuzzle.StageClear.StageClearController.Show(new HexaPuzzle.StageClear.StageClearResult
                {
                    stars          = starCount,
                    starColor      = starColor,
                    score          = score,
                    gold           = goldReward,
                    stageBest      = Mathf.Max(score, prevLevelBest),
                    myBest         = Mathf.Max(score, prevMyBest),
                    isNewStageBest = newLevel,
                    isNewMyBest    = newMy,
                });
            }
            else if (uiManager != null)
            {
                if (summary.HasValue) uiManager.ShowStageClearPopup(summary.Value, goldReward);
                else uiManager.ShowStageClearPopup(goldReward);
            }
        }

        /// <summary>
        /// 보너스 드릴 생성: 남은 이동횟수만큼 랜덤 일반 블록을 드릴로 변환
        /// </summary>
        private IEnumerator SpawnBonusDrills(int count)
        {
            if (hexGrid == null || count <= 0) yield break;

            // 일반 블록(특수 블록이 아닌) 수집
            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.specialType != SpecialBlockType.None) continue;
                candidates.Add(block);
            }

            // 셔플 (Fisher-Yates)
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = temp;
            }

            int drillCount = Mathf.Min(count, candidates.Count);
            DrillDirection[] directions = { DrillDirection.Vertical, DrillDirection.Slash, DrillDirection.BackSlash };

            // 순차적으로 드릴 변환 (1개마다 이동횟수 UI 1씩 감소 + 드릴 위치에 "+1" 골드 팝업)
            for (int i = 0; i < drillCount; i++)
            {
                HexBlock block = candidates[i];

                // 블록 데이터를 드릴로 변환
                block.Data.specialType = SpecialBlockType.Drill;
                block.Data.drillDirection = directions[Random.Range(0, directions.Length)];
                block.UpdateVisuals();

                // 이동횟수 1 감소 + UI 즉시 갱신
                currentTurns = Mathf.Max(0, currentTurns - 1);
                OnTurnChanged?.Invoke(currentTurns);
                if (uiManager != null) uiManager.UpdateTurnDisplay(currentTurns);

                // 스케일 팝 애니메이션
                StartCoroutine(DrillSpawnPopAnimation(block.transform));

                // ★ 골드 +1 팝업 (데미지 텍스트 연출과 동일 — 위로 떠오르며 페이드아웃)
                StartCoroutine(SpawnGoldPopupAtBlock(block));

                // 사운드 효과
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayButtonClick();

                yield return new WaitForSeconds(0.15f);
            }

            if (drillCount > 0)
            {
                Debug.Log($"[GameManager] 보너스 드릴 {drillCount}개 생성 완료 (남은 턴: {count})");
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>
        /// 드릴 생성 시 팝 애니메이션 (0.5 → 1.15 → 1.0 스케일)
        /// </summary>
        private IEnumerator DrillSpawnPopAnimation(Transform target)
        {
            if (target == null) yield break;

            float duration = 0.2f;
            float elapsed = 0f;
            Vector3 originalScale = target.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.5f)
                    scale = Mathf.Lerp(0.5f, 1.15f, t / 0.5f);
                else
                    scale = Mathf.Lerp(1.15f, 1f, (t - 0.5f) / 0.5f);

                target.localScale = originalScale * scale;
                yield return null;
            }

            target.localScale = originalScale;
        }

        /// <summary>
        /// 드릴 변환 시 블록 위치에 "+1" 골드 팝업을 표시 (1골드 지급 알림).
        /// 연출은 고블린 데미지 팝업(GoblinSystem.AccumulatingDamagePopup)과 동일 패턴:
        /// 블록 위쪽에 생성 → 제자리 짧게 유지 → 위로 떠오르며 페이드아웃.
        /// </summary>
        private IEnumerator SpawnGoldPopupAtBlock(HexBlock block)
        {
            if (block == null || hexGrid == null) yield break;

            RectTransform blockRt = block.GetComponent<RectTransform>();
            if (blockRt == null) yield break;

            Transform parent = block.transform.parent;
            if (parent == null) yield break;

            // 팝업 컨테이너
            GameObject popup = new GameObject("GoldPopup_+1");
            popup.transform.SetParent(parent, false);

            RectTransform popupRt = popup.AddComponent<RectTransform>();
            popupRt.anchoredPosition = blockRt.anchoredPosition + new Vector2(0f, hexGrid.HexSize * 0.5f);
            popupRt.sizeDelta = new Vector2(60f, 30f);

            // 텍스트 ("+1" 골드색)
            var textObj = new GameObject("GoldText");
            textObj.transform.SetParent(popup.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = "+1";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            // 골드(노란 황금) 색상
            Color goldColor = new Color(1f, 0.82f, 0.18f, 1f);
            text.color = goldColor;
            text.raycastTarget = false;

            // 검은 외곽선 (가독성)
            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 제자리 짧은 유지 (임팩트)
            yield return new WaitForSeconds(0.1f);

            if (popup == null) yield break;

            // 위로 떠오르며 페이드아웃
            Vector2 startPos = popupRt.anchoredPosition;
            float duration = 0.6f;
            float elapsed = 0f;
            float riseHeight = 40f;

            while (elapsed < duration)
            {
                if (popup == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 위로 이동 (EaseOut)
                float rise = riseHeight * VisualConstants.EaseOutCubic(t);
                popupRt.anchoredPosition = startPos + new Vector2(0f, rise);

                // 절반 이후부터 페이드아웃
                float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
                text.color = new Color(goldColor.r, goldColor.g, goldColor.b, alpha);
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f * alpha);

                yield return null;
            }

            if (popup != null) Destroy(popup);
        }

        /// <summary>
        /// 모든 특수 블록 시스템 + BlockRemovalSystem이 완전히 idle 상태가 될 때까지 대기
        /// </summary>
        private IEnumerator WaitForAllSystemsIdle()
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                bool anyActive = false;

                if (drillSystem != null && drillSystem.IsDrilling) anyActive = true;
                if (bombSystem != null && bombSystem.IsBombing) anyActive = true;
                if (donutSystem != null && donutSystem.IsActivating) anyActive = true;
                if (xBlockSystem != null && xBlockSystem.IsActivating) anyActive = true;
                if (droneSystem != null && droneSystem.IsActivating) anyActive = true;
                if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) anyActive = true;

                if (!anyActive) break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (elapsed >= timeout)
                Debug.LogWarning("[GameManager] WaitForAllSystemsIdle 타임아웃 (10초)");
        }

        /// <summary>
        /// StageClear 전용 후처리: 낙하 + 연쇄 매칭 + 남은 특수 블록 발동을 완전 정지될 때까지 반복
        /// ProcessSpecialBlockAftermath와 유사하지만 턴 종료/적군 스폰/상태 전환 없음
        /// 연쇄 매칭으로 새로 생성된 특수 블록(pendingActivation 무관)도 포착하여 발동
        /// </summary>
        private IEnumerator ProcessStageClearAftermath()
        {
            yield return new WaitForSeconds(0.1f);

            int maxLoops = 20;
            int loop = 0;

            while (loop < maxLoops)
            {
                loop++;

                // 워치독 타이머 리셋 (루프가 진행 중이므로 stuck 아님)
                processingStartTime = Time.time;

                // 1. BRS가 아직 처리 중이면 완료 대기
                yield return StartCoroutine(WaitForBRSReady());

                // 2. 낙하 처리
                if (blockRemovalSystem != null)
                    yield return StartCoroutine(blockRemovalSystem.ProcessFallingCoroutinePublic());
                yield return new WaitForSeconds(0.05f);

                // 3. pending 블록 수집 (pendingActivation == true인 블록)
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

                // 4. 매칭 확인
                List<MatchingSystem.MatchGroup> newMatches = null;
                if (matchingSystem != null)
                {
                    var matches = matchingSystem.FindMatches();
                    if (matches.Count > 0) newMatches = matches;
                }

                // 5. 필드에 남아있는 발동 가능한 특수 블록 수집 (pendingActivation 무관)
                //    연쇄 매칭으로 새로 생성된 특수 블록을 포착하기 위함
                //    MoveBlock, FixedBlock, TimeBomb은 발동 대상이 아니므로 제외
                List<HexBlock> remainingSpecials = new List<HexBlock>();
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block != null && block.Data != null &&
                            block.Data.gemType != GemType.None &&
                            !pendingBlocks.Contains(block) &&
                            IsActivatableSpecial(block.Data.specialType))
                        {
                            remainingSpecials.Add(block);
                        }
                    }
                }

                Debug.Log($"[GameManager] StageClear aftermath loop #{loop}: " +
                    $"{pendingBlocks.Count} pending, " +
                    $"{(newMatches != null ? newMatches.Count : 0)} matches, " +
                    $"{remainingSpecials.Count} remaining specials");

                // 6. 아무것도 없으면 종료
                if (pendingBlocks.Count == 0 && newMatches == null && remainingSpecials.Count == 0)
                {
                    Debug.Log($"[GameManager] StageClear aftermath ended at #{loop}");
                    break;
                }

                // 7. 매칭이 있으면 BRS에 위임 (pending도 함께)
                if (newMatches != null)
                {
                    yield return StartCoroutine(WaitForBRSReady());

                    if (pendingBlocks.Count > 0)
                    {
                        Debug.Log($"[GameManager] StageClear aftermath: {pendingBlocks.Count} pending + {newMatches.Count} matches -> BRS");
                        blockRemovalSystem.ProcessMatchesWithPendingSpecials(newMatches, pendingBlocks);
                    }
                    else
                    {
                        Debug.Log($"[GameManager] StageClear aftermath: {newMatches.Count} matches -> BRS");
                        blockRemovalSystem.ProcessMatches(newMatches);
                    }

                    yield return StartCoroutine(WaitForBRSComplete("StageClear-Aftermath"));
                    yield return StartCoroutine(WaitForAllSystemsIdle());
                    continue;
                }

                // 8. pending + 남은 특수 블록을 합쳐서 발동
                List<HexBlock> allToActivate = new List<HexBlock>();
                allToActivate.AddRange(pendingBlocks);
                allToActivate.AddRange(remainingSpecials);

                if (allToActivate.Count > 0)
                {
                    Debug.Log($"[GameManager] StageClear aftermath: activating {allToActivate.Count} specials " +
                        $"({pendingBlocks.Count} pending + {remainingSpecials.Count} remaining)");
                    List<Coroutine> activations = new List<Coroutine>();
                    foreach (var block in allToActivate)
                    {
                        if (block == null || block.Data == null) continue;
                        if (block.Data.specialType == SpecialBlockType.None) continue;
                        activations.Add(StartCoroutine(ActivateSpecialAndWait(block)));
                    }
                    foreach (var co in activations)
                        yield return co;
                    yield return StartCoroutine(WaitForAllSystemsIdle());
                    continue;
                }
            }

            if (loop >= maxLoops)
                Debug.LogWarning($"[GameManager] ProcessStageClearAftermath 최대 루프 도달 ({maxLoops})!");

            // 최종 안전 대기
            yield return StartCoroutine(WaitForAllSystemsIdle());
            Debug.Log("[GameManager] ProcessStageClearAftermath 완료 - 모든 활동 정지");
        }

        /// <summary>
        /// 발동 가능한 특수 블록 타입인지 확인
        /// MoveBlock, FixedBlock, TimeBomb은 발동 대상이 아님
        /// </summary>
        private bool IsActivatableSpecial(SpecialBlockType type)
        {
            return type == SpecialBlockType.Drill ||
                   type == SpecialBlockType.Bomb ||
                   type == SpecialBlockType.Rainbow ||
                   type == SpecialBlockType.XBlock ||
                   type == SpecialBlockType.Drone;
        }

        /// <summary>
        /// 필드의 모든 특수 블록을 동시에 발동
        /// </summary>
        private IEnumerator ActivateAllSpecialBlocks()
        {
            if (hexGrid == null) yield break;

            List<HexBlock> drillBlocks = new List<HexBlock>();
            List<HexBlock> bombBlocks = new List<HexBlock>();
            List<HexBlock> donutBlocks = new List<HexBlock>();
            List<HexBlock> xBlocks = new List<HexBlock>();
            List<HexBlock> droneBlocks = new List<HexBlock>();

            // 필드의 모든 특수 블록 수집
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;

                if (block.Data.IsDrill())
                    drillBlocks.Add(block);
                else if (block.Data.IsBomb())
                    bombBlocks.Add(block);
                else if (block.Data.IsDonut())
                    donutBlocks.Add(block);
                else if (block.Data.IsXBlock())
                    xBlocks.Add(block);
                else if (block.Data.IsDrone())
                    droneBlocks.Add(block);
            }

            // 모든 특수 블록을 동시에 발동 (병렬 코루틴)
            List<Coroutine> activationCoroutines = new List<Coroutine>();

            // 드릴 발동
            foreach (var block in drillBlocks)
            {
                if (drillSystem != null)
                    activationCoroutines.Add(StartCoroutine(ActivateDrillBlock(block)));
            }

            // 폭탄 발동
            foreach (var block in bombBlocks)
            {
                if (bombSystem != null)
                    activationCoroutines.Add(StartCoroutine(ActivateBombBlock(block)));
            }

            // 타겟 레이저 발동
            foreach (var block in donutBlocks)
            {
                if (donutSystem != null)
                    activationCoroutines.Add(StartCoroutine(ActivateDonutBlock(block)));
            }

            // X블록 발동
            foreach (var block in xBlocks)
            {
                if (xBlockSystem != null)
                    activationCoroutines.Add(StartCoroutine(ActivateXBlock(block)));
            }

            // 드론 발동
            foreach (var block in droneBlocks)
            {
                if (droneSystem != null)
                    activationCoroutines.Add(StartCoroutine(ActivateDroneBlock(block)));
            }

            // 모든 발동 완료 대기
            foreach (var coroutine in activationCoroutines)
            {
                yield return coroutine;
            }

            Debug.Log($"[GameManager] 특수 블록 발동 완료: 드릴({drillBlocks.Count}), 폭탄({bombBlocks.Count}), 타겟 레이저({donutBlocks.Count}), X({xBlocks.Count}), 드론({droneBlocks.Count})");
        }

        /// <summary>
        /// 드릴 블록 발동
        /// </summary>
        private IEnumerator ActivateDrillBlock(HexBlock block)
        {
            if (drillSystem != null)
            {
                drillSystem.ActivateDrill(block);
                float waited = 0f;
                while (drillSystem.IsDrilling && waited < 5f)
                {
                    waited += Time.deltaTime;
                    processingStartTime = Time.time;
                    yield return null;
                }
                if (drillSystem.IsDrilling)
                {
                    Debug.LogWarning("[GameManager] ActivateDrillBlock timeout! ForceReset.");
                    drillSystem.ForceReset();
                }
            }
        }

        /// <summary>
        /// 폭탄 블록 발동
        /// </summary>
        private IEnumerator ActivateBombBlock(HexBlock block)
        {
            if (bombSystem != null)
            {
                bombSystem.ActivateBomb(block);
                float waited = 0f;
                while (bombSystem.IsBombing && waited < 5f)
                {
                    waited += Time.deltaTime;
                    processingStartTime = Time.time;
                    yield return null;
                }
                if (bombSystem.IsBombing)
                {
                    Debug.LogWarning("[GameManager] ActivateBombBlock timeout! ForceReset.");
                    bombSystem.ForceReset();
                }
            }
        }

        /// <summary>
        /// 타겟 레이저 블록 발동
        /// </summary>
        private IEnumerator ActivateDonutBlock(HexBlock block)
        {
            if (donutSystem != null)
            {
                donutSystem.ActivateDonut(block);
                float waited = 0f;
                while (donutSystem.IsActivating && waited < 5f)
                {
                    waited += Time.deltaTime;
                    processingStartTime = Time.time;
                    yield return null;
                }
                if (donutSystem.IsActivating)
                {
                    Debug.LogWarning("[GameManager] ActivateDonutBlock timeout! ForceReset.");
                    donutSystem.ForceReset();
                }
            }
        }

        /// <summary>
        /// X블록 발동
        /// </summary>
        private IEnumerator ActivateXBlock(HexBlock block)
        {
            if (xBlockSystem != null)
            {
                xBlockSystem.ActivateXBlock(block);
                float waited = 0f;
                while (xBlockSystem.IsActivating && waited < 5f)
                {
                    waited += Time.deltaTime;
                    processingStartTime = Time.time;
                    yield return null;
                }
                if (xBlockSystem.IsActivating)
                {
                    Debug.LogWarning("[GameManager] ActivateXBlock timeout! ForceReset.");
                    xBlockSystem.ForceReset();
                }
            }
        }

        /// <summary>
        /// 드론 블록 발동
        /// </summary>
        private IEnumerator ActivateDroneBlock(HexBlock block)
        {
            if (droneSystem != null)
            {
                droneSystem.ActivateDrone(block);
                float waited = 0f;
                while (droneSystem.IsActivating && waited < 5f)
                {
                    waited += Time.deltaTime;
                    processingStartTime = Time.time;
                    yield return null;
                }
                if (droneSystem.IsActivating)
                {
                    Debug.LogWarning("[GameManager] ActivateDroneBlock timeout! ForceReset.");
                    droneSystem.ForceReset();
                }
            }
        }

        /// <summary>
        /// 스케일 펀치 애니메이션
        /// </summary>
        private IEnumerator ScalePunchAnimation(Transform target, float duration, float punchScale)
        {
            Vector3 originalScale = target.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 0 -> punchScale -> 1.0
                float scale;
                if (t < 0.5f)
                {
                    scale = Mathf.Lerp(1f, punchScale, t * 2f);
                }
                else
                {
                    scale = Mathf.Lerp(punchScale, 1f, (t - 0.5f) * 2f);
                }

                target.localScale = originalScale * scale;
                yield return null;
            }

            target.localScale = originalScale;
        }

        /// <summary>
        /// 골드 카운팅 애니메이션: 0 -> goldAmount
        /// </summary>
        private IEnumerator CountGoldAnimation(int goldAmount)
        {
            float duration = 0.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                int displayGold = Mathf.RoundToInt(t * goldAmount);
                uiManager.UpdateGoldDisplay(displayGold);
                yield return null;
            }

            uiManager.UpdateGoldDisplay(goldAmount);
        }

        /// <summary>
        /// 골드 추가 및 저장
        /// </summary>
        public void AddGold(int amount)
        {
            currentGold += amount;
            OnGoldChanged?.Invoke(currentGold);
            uiManager.UpdateGoldDisplay(currentGold);
            SaveGold();
        }

        /// <summary>
        /// 골드 차감 (아이템 구매 등)
        /// </summary>
        /// <returns>차감 성공 여부</returns>
        public bool SpendGold(int amount)
        {
            if (currentGold < amount) return false;
            currentGold -= amount;
            goldSpentThisStage += amount; // ★ 학습 fitness: 이번 스테이지 골드 소비 누적 (마이너스 점수 환산용)
            OnGoldChanged?.Invoke(currentGold);
            if (uiManager != null)
                uiManager.UpdateGoldDisplay(currentGold);
            SaveGold();
            Debug.Log($"[GameManager] 골드 차감: -{amount}, 잔액: {currentGold}");
            return true;
        }

        // ★ 학습형 자동플레이 데이터: 이번 스테이지 골드 소비/이어하기 사용 횟수 (RecordResult가 읽음)
        private int goldSpentThisStage = 0;
        public int GoldSpentThisStage => goldSpentThisStage;
        public int ContinueCountThisStage => continueCount;

        // ★ 골드 지갑 저장 키 — ScoreManager의 통계 키("TotalGoldEarned")와 절대 겹치면 안 됨.
        //   (과거 ScoreManager가 같은 "TotalGold" 키에 누적 점수를 덮어써 지갑이 손상되는 버그가 있었음)
        private const string GOLD_WALLET_KEY = "TotalGold";

        /// <summary>
        /// 골드 저장 (PlayerPrefs)
        /// </summary>
        private void SaveGold()
        {
            // ★ 보안: HMAC 서명 저장 (평문 int 변조 차단)
            JewelsHexaPuzzle.Utils.SecurePrefs.SetInt(GOLD_WALLET_KEY, currentGold);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장된 골드 로드 (PlayerPrefs)
        /// </summary>
        private void LoadGold()
        {
            // ★ 보안: 서명 검증 로드 + 상한/하한 클램프 (오버플로·음수 변조 차단)
            currentGold = Mathf.Clamp(JewelsHexaPuzzle.Utils.SecurePrefs.GetInt(GOLD_WALLET_KEY, 0), 0, 9_999_999);
        }

        // ============================================================
        // 이동횟수 추가 구매 시스템
        // ============================================================

        /// <summary>
        /// 현재 이동횟수 추가 구매 비용 (100씩 가산: 100, 200, 300...)
        /// </summary>
        private int GetContinueCost()
        {
            return CONTINUE_BASE_COST * (continueCount + 1);
        }

        /// <summary>
        /// GameOver 팝업 표시 시 구매 버튼 비용 텍스트 갱신
        /// </summary>
        private void UpdateContinueCostDisplay()
        {
            int cost = GetContinueCost();
            if (continueCostText != null)
                continueCostText.text = $"+{CONTINUE_MOVES} 이동횟수  {cost}G";

            // 골드 부족 시 버튼 비활성화 표시
            if (continueButton != null)
                continueButton.interactable = (currentGold >= cost);
        }

        /// <summary>
        /// 골드 부족 시 버튼 흔들림 피드백
        /// </summary>
        private IEnumerator GoldInsufficientFeedback(GameObject buttonObj)
        {
            if (buttonObj == null) yield break;
            RectTransform rt = buttonObj.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 origPos = rt.anchoredPosition;
            float shakeDur = 0.3f;
            float elapsed = 0f;
            while (elapsed < shakeDur)
            {
                if (rt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float intensity = 6f * (1f - elapsed / shakeDur);
                rt.anchoredPosition = origPos + new Vector2(
                    Random.Range(-intensity, intensity), 0f);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = origPos;
        }

        // ============================================================
        // 생존 미션 콜백
        // ============================================================

        // 무한도전 최대 이동 횟수
        private const int MAX_INFINITE_TURNS = 20;

        private void OnSurvivalMissionCompleted(SurvivalMission mission, int reward)
        {
            // 턴 즉시 추가 (UI 애니메이션과 무관하게 항상 실행)
            AddTurns(reward);

            // 미션 완료 애니메이션 시작 (시각 효과만)
            if (uiManager != null)
            {
                uiManager.AnimateMissionComplete(reward);
            }

            // 다음 미션 배정은 MissionSystem.OnTurnEnd()에서 직접 처리
        }

        private Coroutine missionEntranceCoroutine;

        private void OnSurvivalMissionAssigned(SurvivalMission mission)
        {
            if (uiManager == null) return;

            // 무한도전 미션 순차 감소 필드 리셋 (CollectMulti는 양쪽 타겟 합산)
            int totalTarget = mission.targetCount;
            if (mission.type == SurvivalMissionType.CollectMulti)
                totalTarget += mission.targetCount2;
            infiniteMissionDisplayed = totalTarget;
            infiniteMissionTarget = totalTarget;
            infiniteMissionComplete = false;
            if (infiniteMissionCountDownCo != null)
            {
                StopCoroutine(infiniteMissionCountDownCo);
                infiniteMissionCountDownCo = null;
            }

            // 이전 등장 애니메이션 중단
            if (missionEntranceCoroutine != null)
                StopCoroutine(missionEntranceCoroutine);

            // 전체 흐름을 하나의 코루틴으로 래핑 (UI 생성 → 레이아웃 대기 → 애니메이션)
            missionEntranceCoroutine = StartCoroutine(SetupAndAnimateMission(mission));
        }

        /// <summary>
        /// 미션 UI 생성 + 등장 애니메이션을 하나의 코루틴으로 래핑.
        /// UI 생성 후 yield return null로 Canvas 레이아웃 갱신을 기다린 뒤 애니메이션 시작.
        /// </summary>
        private IEnumerator SetupAndAnimateMission(SurvivalMission mission)
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield break;

            // === 기존 UI 참조 캡처 ===
            RectTransform oldPreviewRt = UIManager.nextMissionPreviewRect;
            RectTransform oldCurrentRt = UIManager.gameMissionIconRect;
            bool hasOldPreview = (oldPreviewRt != null);
            bool hasOldCurrent = (oldCurrentRt != null);

            // 다음 미션 미리보기 위치/크기 (슬라이드 시작점)
            Vector2 previewPos = new Vector2(20f, -20f);
            Vector2 previewSize = new Vector2(196f, 77f);

            // 현재 미션 최종 위치/크기
            Vector2 currentMissionPos = new Vector2(40f, -102f);
            Vector2 currentMissionSize = new Vector2(280f, 110f);

            if (hasOldPreview)
            {
                // --- Phase 1: 완료 미션 하강+페이드아웃 + 미리보기→현재 미션 슬라이드 (동시 진행) ---

                // 완료 미션 하강 준비
                CanvasGroup oldCurrentCg = null;
                Vector2 oldCurrentStartPos = Vector2.zero;
                if (hasOldCurrent)
                {
                    oldCurrentCg = oldCurrentRt.GetComponent<CanvasGroup>();
                    if (oldCurrentCg == null)
                        oldCurrentCg = oldCurrentRt.gameObject.AddComponent<CanvasGroup>();
                    oldCurrentStartPos = oldCurrentRt.anchoredPosition;
                    // 완료된 미션임을 표시 — 배경색 살짝 녹색 틴트
                    Image oldBg = oldCurrentRt.GetComponent<Image>();
                    if (oldBg != null)
                        oldBg.color = new Color(0.4f, 0.85f, 0.5f, 0.92f);
                }

                // 미리보기 전환 준비
                CanvasGroup previewCg = oldPreviewRt.GetComponent<CanvasGroup>();
                if (previewCg == null)
                    previewCg = oldPreviewRt.gameObject.AddComponent<CanvasGroup>();

                // "NEXT" 라벨 페이드아웃 대상
                Transform nextLabel = oldPreviewRt.Find("NextLabel");

                float transitionDuration = 0.25f;
                float elapsed = 0f;

                // 미션 전환 효과음
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayMissionEntranceSound();

                Debug.Log($"[MissionEntrance] 미리보기→현재 미션 전환 시작 (완료 미션 하강 동시 진행)");

                while (elapsed < transitionDuration)
                {
                    if (oldPreviewRt == null) break;
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / transitionDuration);
                    float eased = VisualConstants.EaseOutBack(t);

                    // === 미리보기 → 현재 미션 위치 이동 ===
                    oldPreviewRt.anchoredPosition = Vector2.LerpUnclamped(previewPos, currentMissionPos, eased);
                    oldPreviewRt.sizeDelta = Vector2.LerpUnclamped(previewSize, currentMissionSize, eased);

                    // 배경 불투명도 강화: 0.55 → 0.92
                    Image bgImg = oldPreviewRt.GetComponent<Image>();
                    if (bgImg != null)
                    {
                        Color c = bgImg.color;
                        c.a = Mathf.Lerp(0.55f, 0.92f, t);
                        bgImg.color = c;
                    }

                    // "NEXT" 라벨 페이드아웃
                    if (nextLabel != null)
                    {
                        CanvasGroup labelCg = nextLabel.GetComponent<CanvasGroup>();
                        if (labelCg == null)
                            labelCg = nextLabel.gameObject.AddComponent<CanvasGroup>();
                        labelCg.alpha = 1f - t;
                    }

                    // === 완료 미션: 아래로 밀려남 + 축소 + 페이드아웃 ===
                    if (hasOldCurrent && oldCurrentRt != null)
                    {
                        // 아래로 130px 밀림
                        float slideDown = Mathf.Lerp(0f, 130f, t);
                        oldCurrentRt.anchoredPosition = oldCurrentStartPos + new Vector2(0f, -slideDown);

                        // 축소: 1.0 → 0.65
                        float shrink = Mathf.Lerp(1f, 0.65f, t);
                        oldCurrentRt.localScale = Vector3.one * shrink;

                        // 페이드아웃: 1.0 → 0.0
                        oldCurrentCg.alpha = 1f - t;
                    }

                    yield return null;
                }

                // Phase 1 완료: 완료 미션 UI 파괴
                if (hasOldCurrent && oldCurrentRt != null)
                    Destroy(oldCurrentRt.gameObject);
                UIManager.gameMissionIconRect = null;
                UIManager.gameMissionCountText = null;

                // 미리보기 UI 파괴
                if (oldPreviewRt != null)
                    Destroy(oldPreviewRt.gameObject);
                UIManager.nextMissionPreviewRect = null;
            }
            else
            {
                // 기존 미션 UI 정리 (미리보기가 없는 첫 시작 등)
                uiManager.CleanupGameMissionUI();
            }

            // === Phase 2: 실제 현재 미션 UI 생성 ===

            // 다음 미션 미리보기 UI 생성 (좌상단, 70% 크기)
            if (missionSystem != null)
            {
                SurvivalMission nextMission = missionSystem.NextPreviewMission;
                if (nextMission != null)
                {
                    MissionData nextMd = ConvertSurvivalToMissionData(nextMission);
                    uiManager.CreateNextMissionPreviewUI(canvas, nextMd, nextMission.reward);
                }
            }

            // 잔여 미션 UI 안전 정리
            GameObject oldMissionUI = GameObject.Find("GameMissionUI");
            if (oldMissionUI != null) Destroy(oldMissionUI);
            GameObject oldMultiUI = GameObject.Find("GameMissionUI_Multi");
            if (oldMultiUI != null) Destroy(oldMultiUI);
            GameObject oldLevel1UI = GameObject.Find("GameMissionUI_Level1");
            if (oldLevel1UI != null) Destroy(oldLevel1UI);

            // 현재 미션 UI 생성
            MissionData md = ConvertSurvivalToMissionData(mission);
            uiManager.CreateGameMissionUI(canvas, md);
            uiManager.SetMissionRewardText(mission.reward);

            RectTransform missionRt = UIManager.gameMissionIconRect;
            if (missionRt == null)
            {
                Debug.LogWarning("[MissionEntrance] gameMissionIconRect가 null — 애니메이션 건너뜀");
                yield break;
            }

            bool hasNextPreview = UIManager.nextMissionPreviewRect != null;
            Vector2 targetPos = hasNextPreview
                ? new Vector2(40f, -102f)
                : new Vector2(20f, -20f);

            if (hasOldPreview)
            {
                // 미리보기 전환이 있었으면: 현재 미션 위치에 바로 배치 + 팝 효과
                missionRt.anchoredPosition = targetPos;
                missionRt.localScale = Vector3.one * 1.1f;

                CanvasGroup cg = missionRt.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = missionRt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                yield return null;

                // 짧은 팝인 (페이드+스케일)
                float popDuration = 0.1f;
                float popElapsed = 0f;
                while (popElapsed < popDuration)
                {
                    if (missionRt == null) yield break;
                    popElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(popElapsed / popDuration);
                    missionRt.localScale = Vector3.one * Mathf.Lerp(1.1f, 1f, t);
                    cg.alpha = t;
                    yield return null;
                }
                if (missionRt != null) missionRt.localScale = Vector3.one;
                if (cg != null) cg.alpha = 1f;

                // 새 다음 미션 미리보기 페이드인
                RectTransform newPreviewRt = UIManager.nextMissionPreviewRect;
                if (newPreviewRt != null)
                {
                    CanvasGroup previewCg = newPreviewRt.GetComponent<CanvasGroup>();
                    if (previewCg == null)
                        previewCg = newPreviewRt.gameObject.AddComponent<CanvasGroup>();
                    previewCg.alpha = 0f;

                    float fadeDuration = 0.15f;
                    float fadeElapsed = 0f;
                    while (fadeElapsed < fadeDuration)
                    {
                        if (newPreviewRt == null) break;
                        fadeElapsed += Time.unscaledDeltaTime;
                        previewCg.alpha = Mathf.Clamp01(fadeElapsed / fadeDuration);
                        yield return null;
                    }
                    if (previewCg != null) previewCg.alpha = 1f;
                }
            }
            else
            {
                // 첫 미션: 기존 왼쪽 슬라이드인 애니메이션
                Vector2 startPos = new Vector2(targetPos.x - 420f, targetPos.y);
                missionRt.anchoredPosition = startPos;
                missionRt.localScale = Vector3.one * 0.6f;

                CanvasGroup cg = missionRt.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = missionRt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                yield return null;
                yield return null;
                missionRt.anchoredPosition = startPos;

                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayMissionEntranceSound();

                float slideDuration = 0.2f;
                float elapsed = 0f;
                while (elapsed < slideDuration)
                {
                    if (missionRt == null) yield break;
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / slideDuration);
                    float eased = VisualConstants.EaseOutBack(t);
                    missionRt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);
                    missionRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, eased);
                    cg.alpha = Mathf.Clamp01(t * 2.5f);
                    yield return null;
                }
                if (missionRt != null)
                {
                    missionRt.anchoredPosition = targetPos;
                    missionRt.localScale = Vector3.one;
                }
                if (cg != null) cg.alpha = 1f;
            }

            Debug.Log("[MissionEntrance] 애니메이션 완료");
            missionEntranceCoroutine = null;
        }

        /// <summary>
        /// 레벨 모드 미션 UI 등장 (무한도전과 동일한 위치/연출).
        /// 다음 미션 자리를 빈 플레이스홀더로 확보하고, 현재 미션을 좌측에서 슬라이드인.
        /// 복수 미션일 경우 하나씩 순차 등장.
        /// </summary>
        private IEnumerator SetupAndAnimateStageMission(MissionData[] missions)
        {
            // 이전 미션 UI 정리
            uiManager.CleanupGameMissionUI();

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null || missions == null || missions.Length == 0) yield break;

            // 다음 미션 자리 비워두기 (무한도전과 동일한 레이아웃 확보)
            uiManager.CreateEmptyNextMissionPlaceholder(canvas);

            // 2프레임 대기: Canvas 레이아웃 settle 보장
            yield return null;
            yield return null;

            if (missions.Length == 1 && GetMissionLayoutTotal(1) == 1)
            {
                // === 단일 미션(잠금 칩 없음): SetupAndAnimateMission과 동일한 슬라이드인 ===
                //   잠금 칩이 있으면(레벨 11~20 등) 아래 행 포맷 경로로 — 칩과 사이즈/배치 일관.
                uiManager.CreateGameMissionUI(canvas, missions[0]);

                RectTransform missionRt = UIManager.gameMissionIconRect;
                if (missionRt == null) yield break;

                // 플레이스홀더 아래 위치 (무한도전과 동일: 40, -102)
                Vector2 targetPos = new Vector2(40f, -102f);
                Vector2 startPos = new Vector2(targetPos.x - 420f, targetPos.y);

                missionRt.anchoredPosition = startPos;
                missionRt.localScale = Vector3.one * 0.6f;

                CanvasGroup cg = missionRt.GetComponent<CanvasGroup>();
                if (cg == null) cg = missionRt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                yield return null;
                missionRt.anchoredPosition = startPos;

                Debug.Log($"[StageMission] 단일 미션 슬라이드인 — start:{startPos} → target:{targetPos}");

                // 미션 등장 효과음
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayMissionEntranceSound();

                yield return StartCoroutine(AnimateMissionSlideIn(missionRt, targetPos));

                // ★ 미션 UI 등장 직후 해당 미션의 몬스터를 순차 소환
                yield return StartCoroutine(SpawnMissionMonstersForIntro(missions[0]));
            }
            else
            {
                // === 복수 미션: 개별 행으로 하나씩 순차 등장 ===
                // ★ 잠금 칩(Max−한도) 포함 총 슬롯 수로 스케일/2열 결정 (사용자 요청: 잠금 UI 포함 사이즈 조정)
                int layoutTotal = GetMissionLayoutTotal(missions.Length);
                // ★ 4개 이상: 2열 레이아웃 (1~3번 왼쪽열, 4번~ 오른쪽열)
                bool useTwoColumns = layoutTotal >= 4;
                int leftColCount = useTwoColumns ? 3 : layoutTotal; // 왼쪽열 최대 3개
                float scale = layoutTotal >= 3 ? 0.7f : 1.0f;
                float rowActualHeight = 90f * scale + 20f * scale; // 행 실제 높이 (배경 포함)
                float rowSpacing = rowActualHeight + 5f; // 행 높이 + 5px 간격
                float leftX = 40f;
                // ★ 왼쪽 열에 몬스터 미션(얼굴 2배라 폭 216)이 있으면 그 폭 기준, 아니면 196 — 오른쪽 열을 왼쪽 끝 +5px에 배치(겹침 방지)
                bool leftHasMonster = false;
                for (int li = 0; li < leftColCount && li < missions.Length; li++)
                    if (missions[li] != null && missions[li].type == MissionType.RemoveEnemy) { leftHasMonster = true; break; }
                // 콘텐츠가 우측 3px 이동(액센트와 분리)하며 패널 폭도 +3 → 오른쪽 열(두 번째 줄)도 그만큼 우측 이동(사용자 요청)
                float leftColWidth = leftHasMonster ? 219f : 199f;
                float rightX = useTwoColumns ? (leftX + leftColWidth * scale + 13f) : 40f; // 왼쪽 열 실제 끝 + 13px (오른쪽 열=두 번째 줄 우측 추가 이동 5+3, 사용자 요청)

                for (int i = 0; i < missions.Length; i++)
                {
                    RectTransform rowRt = uiManager.CreateIndividualMissionRow(canvas, missions[i], i, layoutTotal);
                    if (rowRt == null) continue;

                    // ★ 2열 레이아웃 위치 계산
                    float targetX;
                    float targetY;
                    if (!useTwoColumns || i < leftColCount)
                    {
                        // 왼쪽 열: 0~2번째 미션
                        targetX = leftX;
                        targetY = -102f - (i * rowSpacing);
                    }
                    else
                    {
                        // 오른쪽 열: 3번째 이후 미션 (4번째는 1번째 옆, 5번째는 4번째 아래...)
                        int rightIdx = i - leftColCount;
                        targetX = rightX;
                        targetY = -102f - (rightIdx * rowSpacing);
                    }

                    Vector2 targetPos = new Vector2(targetX, targetY);
                    Vector2 startPos = new Vector2(targetPos.x - 420f, targetY);

                    // 시작 상태: 화면 밖 + 축소 + 투명
                    rowRt.anchoredPosition = startPos;
                    rowRt.localScale = Vector3.one * 0.6f;

                    CanvasGroup cg = rowRt.GetComponent<CanvasGroup>();
                    if (cg == null) cg = rowRt.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = 0f;

                    yield return null;
                    rowRt.anchoredPosition = startPos;

                    Debug.Log($"[StageMission] 미션[{i}] 순차 슬라이드인 — start:{startPos} → target:{targetPos}");

                    // 미션 등장 효과음 (순번에 따라 피치 상승)
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayMissionEntranceSound(i, missions.Length);

                    // 순차 등장 애니메이션
                    yield return StartCoroutine(AnimateMissionSlideIn(rowRt, targetPos));

                    // ★ 미션 UI 등장 직후 해당 미션의 몬스터를 순차 소환 (모두 소환된 뒤 다음 미션 UI로)
                    yield return StartCoroutine(SpawnMissionMonstersForIntro(missions[i]));

                    // 다음 미션 등장 전 약간의 딜레이
                    if (i < missions.Length - 1)
                        yield return new WaitForSeconds(0.06f);
                }
            }

            Debug.Log($"[StageMission] 전체 미션 등장 완료 ({missions.Length}개)");
        }

        /// <summary>
        /// 미션 UI 등장 직후 해당 미션(RemoveEnemy)의 몬스터를 그 타입으로 순차 소환한다.
        /// MonsterSpawnController.SpawnMissionBatch(타입 제한) 경유 → 그 미션의 몬스터만 등장.
        /// 일반 블록/수집 미션 등 RemoveEnemy가 아니면 소환 없음.
        /// </summary>
        private IEnumerator SpawnMissionMonstersForIntro(MissionData mission)
        {
            if (mission == null || mission.type != MissionType.RemoveEnemy || mission.targetCount <= 0) yield break;
            if (MonsterSpawnController.Instance == null || goblinSystem == null || !goblinSystem.IsActive) yield break;
            yield return StartCoroutine(
                MonsterSpawnController.Instance.SpawnMissionBatch(mission.targetEnemyType, mission.targetCount));
            // 소환 연출을 보여준 뒤 다음 미션 UI로 (모두 소환된 다음 다음 미션)
            yield return new WaitForSeconds(0.25f);
        }

        /// <summary>
        /// 미션 행 슬라이드인 애니메이션 (EaseOutBack + 페이드인 + 스케일).
        /// SetupAndAnimateMission / SetupAndAnimateStageMission 공용.
        /// </summary>
        private IEnumerator AnimateMissionSlideIn(RectTransform rt, Vector2 targetPos)
        {
            if (rt == null) yield break;

            Vector2 startPos = rt.anchoredPosition;
            CanvasGroup cg = rt.GetComponent<CanvasGroup>();

            float slideDuration = 0.2f;
            float elapsed = 0f;

            while (elapsed < slideDuration)
            {
                if (rt == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / slideDuration);
                float eased = VisualConstants.EaseOutBack(t);

                rt.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, eased);
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, eased);
                if (cg != null) cg.alpha = Mathf.Clamp01(t * 2.5f);

                yield return null;
            }

            // 최종 값 확정
            if (rt != null)
            {
                rt.anchoredPosition = targetPos;
                rt.localScale = Vector3.one;
            }
            if (cg != null) cg.alpha = 1f;
        }

        private void OnSurvivalMissionProgressChanged(SurvivalMission mission)
        {
            if (UIManager.gameMissionCountText == null) return;

            // CollectMulti는 두 타겟 모두의 잔여량을 합산
            int remaining;
            if (mission.type == SurvivalMissionType.CollectMulti)
            {
                int r1 = Mathf.Max(0, mission.targetCount - mission.currentCount);
                int r2 = Mathf.Max(0, mission.targetCount2 - mission.currentCount2);
                remaining = r1 + r2;
            }
            else
            {
                remaining = Mathf.Max(0, mission.targetCount - mission.currentCount);
            }

            // 초기값 설정 (처음 호출 시)
            if (infiniteMissionDisplayed < 0)
            {
                int initTotal = mission.targetCount;
                if (mission.type == SurvivalMissionType.CollectMulti)
                    initTotal += mission.targetCount2;
                infiniteMissionDisplayed = initTotal;
            }

            // 타겟 업데이트
            infiniteMissionTarget = remaining;
            infiniteMissionComplete = mission.IsComplete;

            // 카운트다운 코루틴이 없으면 시작 (이미 실행 중이면 타겟만 업데이트됨)
            if (infiniteMissionCountDownCo == null && infiniteMissionDisplayed != infiniteMissionTarget)
            {
                infiniteMissionCountDownCo = StartCoroutine(InfiniteMissionSequentialCountDown());
            }
        }

        /// <summary>
        /// 무한도전 미션 순차 감소 코루틴.
        /// 타겟이 변경되어도 1단위씩 계속 감소하며, 타겟에 도달하면 종료.
        /// </summary>
        private IEnumerator InfiniteMissionSequentialCountDown()
        {
            Text countText = UIManager.gameMissionCountText;
            bool checkMarkShown = false;

            // 동적 간격: 남은 틱 수가 많으면 빠르게, 적으면 기본 속도
            const float normalInterval = 0.08f;  // 기본 틱 간격
            const float minInterval = 0.02f;     // 최소 틱 간격 (고속 모드)
            const float maxTotalTime = 0.5f;     // 전체 카운트다운 최대 소요 시간
            int soundSkipCounter = 0;            // 고속 시 사운드 간격 제어

            while (infiniteMissionDisplayed != infiniteMissionTarget && countText != null)
            {
                // 매 틱마다 남은 거리 기반으로 간격 재계산 (타겟 변경 시 자동 적응)
                int ticksRemaining = Mathf.Abs(infiniteMissionDisplayed - infiniteMissionTarget);
                float interval = Mathf.Clamp(maxTotalTime / Mathf.Max(1, ticksRemaining), minInterval, normalInterval);
                bool isFastMode = interval < normalInterval * 0.7f; // 기본 대비 30% 이상 빠르면 고속 모드

                // 1단위 증감
                if (infiniteMissionDisplayed > infiniteMissionTarget)
                    infiniteMissionDisplayed--;
                else
                    infiniteMissionDisplayed++;

                countText.text = infiniteMissionDisplayed.ToString();

                // 펄스 애니메이션 (고속 시 3틱마다 — 과도한 코루틴 방지)
                if (!isFastMode || soundSkipCounter % 3 == 0)
                    StartCoroutine(MissionCountPulse(countText.transform));

                // 틱 사운드 (고속 시 2틱마다 — 연속 재생 오버로드 방지)
                if (AudioManager.Instance != null && (!isFastMode || soundSkipCounter % 2 == 0))
                {
                    float progress = 1f - (float)infiniteMissionDisplayed / Mathf.Max(1f, infiniteMissionDisplayed + 3f);
                    AudioManager.Instance.PlayCountUpTick(progress);
                }

                soundSkipCounter++;

                // 0 도달 + 미션 완료 시 yield 전에 즉시 체크마크 표시
                // (레이스 컨디션 방지: yield 중 OnSurvivalMissionAssigned이 코루틴을 kill할 수 있음)
                if (infiniteMissionDisplayed <= 0 && infiniteMissionComplete)
                {
                    countText.text = "";
                    ShowCheckMarkOnText(countText);
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayMissionCompleteSound();
                    checkMarkShown = true;
                    break;
                }

                yield return new WaitForSeconds(interval);
            }

            // 루프 후 체크마크 미표시 시 추가 확인 (타겟 변경으로 즉시 0에 도달한 경우)
            if (!checkMarkShown && infiniteMissionComplete && infiniteMissionTarget <= 0 && countText != null)
            {
                countText.text = "";
                ShowCheckMarkOnText(countText);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayMissionCompleteSound();
            }

            infiniteMissionCountDownCo = null;
        }

        /// <summary>
        /// 미션 카운트 숫자 펄스 애니메이션 (1.0 → 1.2 → 1.0 스케일 바운스)
        /// </summary>
        private IEnumerator MissionCountPulse(Transform target)
        {
            if (target == null) yield break;

            float duration = 0.1f;
            float elapsed = 0f;
            Vector3 originalScale = Vector3.one;

            while (elapsed < duration)
            {
                if (target == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // 0→0.4: 확대 (1.0→1.2), 0.4→1.0: 축소 (1.2→1.0)
                float scale = t < 0.4f
                    ? Mathf.Lerp(1f, 1.2f, t / 0.4f)
                    : Mathf.Lerp(1.2f, 1f, (t - 0.4f) / 0.6f);
                target.localScale = originalScale * scale;
                yield return null;
            }

            if (target != null)
                target.localScale = originalScale;
        }

        /// <summary>
        /// SurvivalMission → MissionData 변환 (스테이지 미션 UI에서 사용)
        /// </summary>
        private MissionData ConvertSurvivalToMissionData(SurvivalMission sm)
        {
            var md = new MissionData();
            md.targetCount = sm.targetCount;
            md.currentCount = sm.currentCount;
            md.description = sm.description;

            switch (sm.type)
            {
                case SurvivalMissionType.CollectGem:
                    md.type = MissionType.CollectGem;
                    md.targetGemType = sm.targetGemType;
                    break;
                case SurvivalMissionType.CollectAny:
                    md.type = MissionType.CollectGem;
                    md.targetGemType = GemType.None;
                    break;
                case SurvivalMissionType.CollectMulti:
                    md.type = MissionType.CollectMultiGem;
                    md.targetGemType = sm.targetGemType;
                    md.secondaryGemType = sm.targetGemType2;
                    break;
                case SurvivalMissionType.CreateSpecial:
                    md.type = MissionType.CreateSpecialGem;
                    break;
                case SurvivalMissionType.AchieveCombo:
                    md.type = MissionType.AchieveCombo;
                    break;
                case SurvivalMissionType.ProcessGem:
                    md.type = MissionType.ProcessGem;
                    break;
                case SurvivalMissionType.ReachScore:
                    md.type = MissionType.ReachScore;
                    break;
                case SurvivalMissionType.SingleTurnRemoval:
                    md.type = MissionType.SingleTurnRemoval;
                    break;
                case SurvivalMissionType.AchieveCascade:
                    md.type = MissionType.AchieveCascade;
                    break;
                case SurvivalMissionType.UseSpecial:
                    md.type = MissionType.UseSpecial;
                    break;
                default:
                    md.type = MissionType.CollectGem;
                    md.targetGemType = GemType.None;
                    break;
            }
            return md;
        }

        /// <summary>
        /// 블록 필드의 모든(존재하는) 블록이 회색(쉘)인지 검사.
        /// 모두 쉘이면 더 이상 매칭이 불가능하므로 게임오버와 동일하게 처리.
        ///   - gemType == None인 빈 슬롯은 제외 (캐스케이드 중간 상태 고려)
        ///   - 최소 1블록 이상 존재해야 true (필드 완전 비어있는 경우 false)
        /// </summary>
        private bool IsFieldFullyShelled()
        {
            if (hexGrid == null) return false;
            int total = 0, shell = 0;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                total++;
                if (block.Data.isShell) shell++;
            }
            return total > 0 && shell == total;
        }

        // ============================================================
        // 교착(데드락) 감지 — 회전 매칭 불가 시 4종 체크 후 게임오버
        // ============================================================
        private Coroutine deadlockCheckCoroutine;
        private bool deadlockToastShown = false; // 같은 교착 상태에서 안내 토스트 1회만

        /// <summary>
        /// 보드 안정화 후 교착 검사.
        /// ① 회전으로 매칭 가능하면 정상.
        /// ② 교착이어도 망치/스왑/라인 중 하나라도 사용 가능(해금+게이지+MP)하면 계속 — 안내 토스트.
        /// ③ 아이템도 전부 불가 + 같은 색 3개 이상이 보드에 없으면 → 게임오버.
        /// </summary>
        private IEnumerator DeadlockCheckCoroutine()
        {
            // 낙하/스폰/연출 안정화 대기
            yield return new WaitForSeconds(0.35f);
            deadlockCheckCoroutine = null;

            // 검사 가능 조건 가드 — 상태가 바뀌었으면 다음 Playing 진입 때 재검사됨
            if (currentState != GameState.Playing || isPaused) yield break;
            if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) yield break;
            if (goblinSystem != null && goblinSystem.IsProcessingTurn) yield break;
            if (rotationSystem != null && rotationSystem.IsRotating) yield break;
            // 튜토리얼은 보드를 직접 제어(강제 배치/재배치)하므로 검사 제외
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive) yield break;
            if (matchingSystem == null || hexGrid == null) yield break;

            // ① 회전으로 매칭 가능? (기존 매칭 존재 포함)
            if (matchingSystem.HasPossibleMoves())
            {
                deadlockToastShown = false; // 교착 해소 → 다음 교착 때 토스트 재허용
                yield break;
            }

            // ② 아이템 탈출 수단 체크 (해금 + 게이지 충전 + MP 충분)
            bool hammerUsable = IsDeadlockEscapeItemUsable(
                hammerButtonObj,
                JewelsHexaPuzzle.Items.HammerGauge.Instance != null ? JewelsHexaPuzzle.Items.HammerGauge.Instance.GaugeLayer : 0,
                ItemType.Hammer);
            bool swapUsable = IsDeadlockEscapeItemUsable(
                swapButtonObj,
                JewelsHexaPuzzle.Items.SwapGauge.Instance != null ? JewelsHexaPuzzle.Items.SwapGauge.Instance.GaugeLayer : 0,
                ItemType.Bomb);   // ItemType.Bomb = 스왑 아이템 (MP 비용 매핑)
            bool lineUsable = IsDeadlockEscapeItemUsable(
                lineDrawButtonObj,
                JewelsHexaPuzzle.Items.LineGauge.Instance != null ? JewelsHexaPuzzle.Items.LineGauge.Instance.GaugeLayer : 0,
                ItemType.SSD);    // ItemType.SSD = 라인 아이템

            // ③ 같은 색 3개 이상 존재? (매칭 가능 블록만 — 쉘/고정/회색 제외)
            bool hasTriple = AnyColorHasTriple();

            Debug.Log($"[GameManager] 교착 감지 — 회전 매칭 불가. 망치={hammerUsable}, 스왑={swapUsable}, 라인={lineUsable}, 같은색3={hasTriple}");

            if (hammerUsable || swapUsable || lineUsable)
            {
                // 아이템으로 탈출 가능 — 1회 안내
                if (!deadlockToastShown && uiManager != null)
                {
                    deadlockToastShown = true;
                    uiManager.ShowToast("회전 매칭이 없습니다 — 아이템을 사용해 보세요!");
                }
                yield break;
            }

            if (hasTriple)
            {
                // ★ 아이템도 못 쓰지만 같은 색 3개는 존재 → 블록 재배치로 매칭 가능 보드 복구
                Debug.LogWarning("[GameManager] 교착 — 아이템 불가 + 같은 색 3개 존재 → 자동 재배치");
                if (uiManager != null)
                    uiManager.ShowToast("매칭 가능한 배치가 없어 블록을 섞습니다!");
                yield return StartCoroutine(ReshuffleBoardForDeadlock());
                // 재배치 후 재검사 예약 (성공 보드면 즉시 통과)
                if (deadlockCheckCoroutine != null) StopCoroutine(deadlockCheckCoroutine);
                deadlockCheckCoroutine = StartCoroutine(DeadlockCheckCoroutine());
                yield break;
            }

            // ★ 4종 모두 불가(같은 색 3개도 없음 — 셔플로도 해소 불가) → 게임오버
            Debug.LogWarning("[GameManager] 교착 게임오버: 회전 매칭 불가 + 아이템 3종 사용 불가 + 같은 색 3개 미존재");
            if (uiManager != null)
                uiManager.ShowToast("더 이상 매칭할 수 있는 블록이 없습니다!");
            yield return new WaitForSeconds(1.0f);

            // 대기 중 상태가 바뀌었으면(클리어/아이템 사용 등) 게임오버 취소
            if (currentState != GameState.Playing) yield break;
            GameOver(GameOverReason.MatchingDeadlock);
        }

        /// <summary>
        /// 교착 해소용 블록 재배치 — 일반 매칭 가능 블록들의 색만 무작위로 섞어
        /// "즉시 매칭 없음 + 회전 매칭 가능" 보드를 만든다 (최대 30회 시도).
        /// 특수블록/고정/쉘/회색은 자리·상태 유지(색 셔플 제외). 깨진 블록은 깨진 상태 유지한 채 색만 섞임.
        /// </summary>
        private IEnumerator ReshuffleBoardForDeadlock()
        {
            if (hexGrid == null || matchingSystem == null) yield break;

            // 입력 잠금 (섞는 동안 회전 방지)
            if (inputSystem != null) inputSystem.SetEnabled(false);

            var blocks = new List<HexBlock>();
            foreach (var b in hexGrid.GetAllBlocks())
            {
                if (b == null || b.Data == null) continue;
                if (b.Data.gemType == GemType.None || b.Data.gemType == GemType.Gray) continue;
                if (b.Data.isShell) continue;
                if (b.Data.specialType != SpecialBlockType.None) continue;
                if (b.Data.dirtMound > 0) continue;
                blocks.Add(b);
            }

            if (blocks.Count >= 3)
            {
                var colors = new List<GemType>();
                foreach (var b in blocks) colors.Add(b.Data.gemType);

                bool ok = false;
                for (int attempt = 0; attempt < 30 && !ok; attempt++)
                {
                    // Fisher-Yates 셔플
                    for (int i = colors.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        var t = colors[i]; colors[i] = colors[j]; colors[j] = t;
                    }
                    for (int i = 0; i < blocks.Count; i++)
                        blocks[i].Data.gemType = colors[i];

                    // 조건: 공짜 매칭 없음 + 회전으로 매칭 가능
                    ok = !matchingSystem.HasAnyMatchQuick() && matchingSystem.HasPossibleMoves();
                }
                // 30회 실패 시에도 마지막 배치 유지 — 공짜 매칭이면 캐스케이드가 처리, 매칭 불가면 재검사가 다시 셔플

                // 비주얼 갱신 + 가벼운 등장 펄스
                foreach (var b in blocks)
                {
                    b.UpdateVisuals();
                    b.transform.localScale = Vector3.one * 0.55f;
                }
                float dur = 0.25f, t2 = 0f;
                while (t2 < dur)
                {
                    t2 += Time.deltaTime;
                    float k = VisualConstants.EaseOutCubic(Mathf.Clamp01(t2 / dur));
                    float s = Mathf.Lerp(0.55f, 1f, k);
                    foreach (var b in blocks)
                        if (b != null) b.transform.localScale = Vector3.one * s;
                    yield return null;
                }
                foreach (var b in blocks)
                    if (b != null) b.transform.localScale = Vector3.one;

                if (AudioManager.Instance != null) AudioManager.Instance.PlayBlockLandSound();
                if (ok) { if (AudioManager.Instance != null) AudioManager.Instance.PlayReshuffleSound(); } // ★ 효과음: 재배치 샤라락
                Debug.Log($"[GameManager] 교착 재배치 완료 — {blocks.Count}블록 색 셔플 (성공={ok})");
            }

            if (inputSystem != null && currentState == GameState.Playing)
                inputSystem.SetEnabled(true);
        }

        /// <summary>교착 탈출용 아이템 사용 가능 판정 — 버튼 해금(활성) + 게이지 1레이어 이상 + MP 충분.</summary>
        private bool IsDeadlockEscapeItemUsable(GameObject buttonObj, int gaugeLayer, ItemType mpCostType)
        {
            if (buttonObj == null || !buttonObj.activeInHierarchy) return false; // 미해금/미표시
            if (gaugeLayer < 1) return false;                                     // 게이지 미충전
            if (MPManager.Instance != null && !MPManager.Instance.CanUseItem(mpCostType)) return false; // MP 부족
            return true;
        }

        /// <summary>보드에 같은 색 3개 이상이 존재하는지 (매칭 가능 블록만 — None/Gray/쉘/고정 제외).</summary>
        private bool AnyColorHasTriple()
        {
            if (hexGrid == null) return false;
            var counts = new Dictionary<GemType, int>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                var d = block.Data;
                if (d.gemType == GemType.None || d.gemType == GemType.Gray) continue;
                if (d.isShell) continue;                                    // 껍데기: 매칭 불가
                if (d.specialType == SpecialBlockType.FixedBlock) continue; // 고정: 회전/매칭 불가
                int c;
                counts.TryGetValue(d.gemType, out c);
                c++;
                if (c >= 3) return true;
                counts[d.gemType] = c;
            }
            return false;
        }

        /// <summary>
        /// 게임 오버 (무한도전: 게임 종료 컨셉 — 점수 획득 + 베스트 스코어 저장)
        /// </summary>
        private void GameOver(GameOverReason reason = GameOverReason.MovesExhausted)
        {
            // ★ 매칭 데드락 실패는 "재배치해도 매칭 불가"일 때만 도전 실패 팝업을 띄운다.
            //   (여러 데드락 경로가 경쟁하며 일부가 재배치 전에 GameOver를 호출 → 팝업이 잠깐 떴다가
            //    재배치되던 깜빡임 버그를 여기서 단일 차단. 팝업 표시 전에 재배치를 먼저 시도한다.)
            if (reason == GameOverReason.MatchingDeadlock
                && currentGameMode != GameMode.Infinite
                && currentState != GameState.GameOver
                && matchingSystem != null)
            {
                // ① 다른 경로가 이미 해소(매칭 가능)했으면 게임오버 취소
                if (matchingSystem.HasPossibleMoves())
                {
                    if (currentState != GameState.Playing) SetGameState(GameState.Playing);
                    if (inputSystem != null) inputSystem.SetEnabled(true);
                    return;
                }
                // ② 재배치로 매칭 가능 보드가 만들어지면 팝업 없이 계속 (성공 시에만 non-null)
                if (BoardHasColorWithAtLeast(3))
                {
                    var solvedCells = ReshuffleBoardToSolvable();
                    if (solvedCells != null)
                    {
                        Debug.Log($"[GameManager] 데드락 게임오버 직전 재배치 성공 → 팝업 생략, 계속 진행 ({solvedCells.Count}블록)");
                        StartCoroutine(ReshufflePulse(solvedCells));
                        if (currentState != GameState.Playing) SetGameState(GameState.Playing);
                        if (inputSystem != null) inputSystem.SetEnabled(true);
                        return;
                    }
                }
                // ③ 여기 도달 = 재배치해도 매칭 불가 → 정상 도전 실패 진행
            }

            lastGameOverReason = reason;
            SetGameState(GameState.GameOver);
            OnGameOver?.Invoke();

            // ★ 실패 원인별 버튼 토글: 데드락/필드고착(매칭할 블록 없음) → 지원폭격,
            //   이동소진 → 이동횟수 추가. (무한도전은 지원폭격 미제공)
            bool offerBombing = (currentGameMode != GameMode.Infinite)
                && (reason == GameOverReason.MatchingDeadlock || reason == GameOverReason.FieldShelled);
            if (supportBombingButtonObj != null) supportBombingButtonObj.SetActive(offerBombing);
            if (continueButtonObj != null) continueButtonObj.SetActive(!offerBombing);

            // BGM 정지 + 게임 오버 사운드
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
                AudioManager.Instance.PlayGameOver();
            }

            bool isInfinite = currentGameMode == GameMode.Infinite;

            if (isInfinite)
            {
                // 무한도전: 점수를 그대로 인정하고 베스트 스코어 갱신
                int finalScore = scoreManager != null ? scoreManager.CurrentScore : 0;
                Debug.Log($"Game End! (무한도전 종료, 점수: {finalScore})");

                // 베스트 스코어 갱신 시도
                if (scoreManager != null)
                    scoreManager.TryUpdateLevelHighScore(selectedStage);

                // 타이틀 변경: "GAME OVER" → "GAME END"
                UpdateGameOverTitle("GAME END", new Color(0.4f, 0.8f, 1f));

                if (gameOverScoreText != null)
                    gameOverScoreText.text = "0";
                if (gameOverMovesText != null)
                    gameOverMovesText.text = "MOVES: 0";

                if (uiManager != null)
                    uiManager.ShowGameOverPopup();

                StartCoroutine(AnimateGameOverPopupInfinite(finalScore));
            }
            else
            {
                // 스테이지 모드: 이동횟수 추가 구매 옵션 제공
                Debug.Log($"Game Over! (도전 실패, reason={reason})");

                UpdateGameOverTitle("도전 실패", new Color(1f, 0.3f, 0.3f));

                if (gameOverScoreText != null)
                    gameOverScoreText.text = "0";
                if (gameOverMovesText != null)
                    gameOverMovesText.text = "MOVES: 0";

                // ★ 이동횟수 추가 구매 버튼 비용 갱신
                UpdateContinueCostDisplay();

                if (uiManager != null)
                    uiManager.ShowGameOverPopup();

                StartCoroutine(AnimateGameOverPopup());
            }
        }

        /// <summary>
        /// 지원폭격 — 데드락(매칭 불가) 실패 시 100골드로 정중앙에 폭탄(연쇄 3단계, 리워드 무시) 투하 후 게임 속행.
        /// 턴 소진/적군 스폰 없이 캐스케이드(낙하+리필+재매칭)만 수행하고 Playing으로 복귀한다.
        /// </summary>
        private IEnumerator SupportBombingCoroutine()
        {
            // 팝업 닫기 + 시간 복원
            if (gameOverPopupObj != null) gameOverPopupObj.SetActive(false);
            Time.timeScale = 1f;

            isSupportBombing = true;
            SetGameState(GameState.Processing);
            if (inputSystem != null) inputSystem.SetEnabled(false);
            processingStartTime = Time.time;

            // 정중앙(0,0)에 폭탄 생성 + base 발동 + 강제 3연쇄
            HexCoord center = new HexCoord(0, 0);
            HexBlock centerBlock = hexGrid != null ? hexGrid.GetBlock(center) : null;
            if (centerBlock != null && bombSystem != null)
            {
                bombSystem.CreateBombBlock(centerBlock, GemType.Red); // base 색 (틴트 무관, 즉시 폭발)
                bombSystem.ActivateSupportBombing(centerBlock, 3);    // 리워드 무시 base 폭탄 + 강제 3연쇄
                // 폭발 완료 대기 (연쇄 포함, 안전 타임아웃 8초 unscaled)
                yield return null; // BombCoroutine 첫 진입(activeBombCount++) 보장
                float t = 0f;
                while (bombSystem.IsBombing && t < 8f)
                {
                    t += Time.unscaledDeltaTime;
                    processingStartTime = Time.time;
                    yield return null;
                }
            }
            else
            {
                Debug.LogWarning("[GameManager] 지원폭격: 중앙 블록/폭탄 시스템 없음 — 캐스케이드만 진행");
            }

            // 캐스케이드: 낙하 + 리필 + 재매칭 (턴종료/적군스폰 없음)
            yield return StartCoroutine(SupportBombingCascade());

            isSupportBombing = false;
            // 게임 속행
            SetGameState(GameState.Playing);
            if (inputSystem != null) inputSystem.SetEnabled(true);
            if (uiManager != null && scoreManager != null) uiManager.UpdateScoreDisplay(scoreManager.CurrentScore);
            Debug.Log("[GameManager] 지원폭격 완료 → 게임 속행 (Playing)");
        }

        /// <summary>
        /// 지원폭격 전용 캐스케이드 — 낙하+리필+재매칭만 반복. 턴 종료/적군 스폰/상태 전환 없음.
        /// </summary>
        private IEnumerator SupportBombingCascade()
        {
            int maxLoops = 20, loop = 0;
            while (loop < maxLoops)
            {
                loop++;
                processingStartTime = Time.time;

                yield return StartCoroutine(WaitForBRSReady());

                if (blockRemovalSystem != null)
                    yield return StartCoroutine(blockRemovalSystem.ProcessFallingCoroutinePublic());
                yield return new WaitForSeconds(0.05f);

                List<MatchingSystem.MatchGroup> newMatches = null;
                if (matchingSystem != null)
                {
                    var matches = matchingSystem.FindMatches();
                    if (matches.Count > 0) newMatches = matches;
                }

                if (newMatches == null)
                {
                    Debug.Log($"[GameManager] 지원폭격 캐스케이드 종료 (#{loop})");
                    break;
                }

                yield return StartCoroutine(WaitForBRSReady());
                blockRemovalSystem.ProcessMatches(newMatches);
                yield return StartCoroutine(WaitForBRSComplete("SupportBombing-Cascade"));
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

            // 무한도전: 최대 이동 횟수 제한
            if (currentGameMode == GameMode.Infinite && currentTurns > MAX_INFINITE_TURNS)
                currentTurns = MAX_INFINITE_TURNS;

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
        /// 로비 UI 프로시저럴 생성
        /// </summary>
        private void CreateLobbyUI(Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // LevelRegistry 강제 재초기화 (도메인 리로드 미사용 대응)
            LevelRegistry.ForceReinitialize();
            var allLevels = LevelRegistry.GetAllLevels();

            // === 루트 컨테이너 (전체 화면) ===
            lobbyContainer = new GameObject("LobbyContainer");
            lobbyContainer.transform.SetParent(canvas.transform, false);
            RectTransform rootRt = lobbyContainer.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 배경 — 클로드 디자인 로비 배경(엘프숲 황혼), 없으면 크림 폴백
            Image overlay = lobbyContainer.AddComponent<Image>();
            Sprite lobbyBg = Resources.Load<Sprite>("UI/Background_Lobby");
            if (lobbyBg != null) { overlay.sprite = lobbyBg; overlay.color = Color.white; overlay.preserveAspect = false; }
            else { overlay.color = ClaudeTheme.PaperBg; }
            overlay.raycastTarget = true;

            // === 배경 가독성 스크림 (다크 글래스가 잘 읽히도록 배경을 살짝 어둡게) ===
            GameObject scrimObj = new GameObject("LobbyScrim");
            scrimObj.transform.SetParent(lobbyContainer.transform, false);
            RectTransform scrimRt = scrimObj.AddComponent<RectTransform>();
            scrimRt.anchorMin = Vector2.zero;
            scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = Vector2.zero;
            scrimRt.offsetMax = Vector2.zero;
            Image scrimImg = scrimObj.AddComponent<Image>();
            scrimImg.color = new Color(0.039f, 0.063f, 0.125f, 0.45f); // #0A1020 45%
            scrimImg.raycastTarget = false;

            // === 상단 글래스 헤더바 (타이틀/골드보다 먼저 생성 → 뒤에 깔림) ===
            GameObject headerBar = new GameObject("LobbyHeaderBar");
            headerBar.transform.SetParent(lobbyContainer.transform, false);
            RectTransform headerRt = headerBar.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, 116f); // v3: 2단(아바타/XP + 재화칩)
            Image headerImg = headerBar.AddComponent<Image>();
            headerImg.color = LobbyTheme.PanelStrong;
            headerImg.raycastTarget = false;

            // 헤더 하단 1px 구분선
            GameObject headerBorder = new GameObject("HeaderBorder");
            headerBorder.transform.SetParent(headerBar.transform, false);
            RectTransform hbRt = headerBorder.AddComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0f, 0f);
            hbRt.anchorMax = new Vector2(1f, 0f);
            hbRt.pivot = new Vector2(0.5f, 0f);
            hbRt.anchoredPosition = Vector2.zero;
            hbRt.sizeDelta = new Vector2(0f, 1f);
            Image hbImg = headerBorder.AddComponent<Image>();
            hbImg.color = LobbyTheme.BorderStrong;
            hbImg.raycastTarget = false;

            // === 게임 타이틀 ===
            GameObject titleObj = new GameObject("LobbyTitle");
            titleObj.transform.SetParent(lobbyContainer.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);   // 글래스 헤더 안으로
            titleRt.sizeDelta = new Vector2(600f, 52f);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 38;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = LobbyTheme.Gold; // 단일 골드 #FFD06B
            titleText.raycastTarget = false;
            titleText.text = "HEXA PUZZLE";
            var lobbyTitleOutline = titleObj.AddComponent<Outline>();
            lobbyTitleOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            lobbyTitleOutline.effectDistance = new Vector2(1.5f, -1.5f);
            titleObj.SetActive(false); // v3: 타이틀 숨김 — 상단바를 아바타/재화로 대체

            // === 보유 골드 (우상단) — 인게임 HUD와 동일 위치·디자인(코인+숫자, 프레임 없음) (2026-07-02 사용자 요청) ===
            //   플레이어 레벨/하트/민트젬/햄버거 상단 UI는 삭제(CreateLobbyHeaderV3 폐기), 골드만 인게임 HUD_GoldGroup 복제.
            GameObject lobbyGoldGroup = new GameObject("LobbyGoldGroup");
            lobbyGoldGroup.transform.SetParent(lobbyContainer.transform, false);
            RectTransform lgRt = lobbyGoldGroup.AddComponent<RectTransform>();
            lgRt.anchorMin = lgRt.anchorMax = new Vector2(1f, 1f);
            lgRt.pivot = new Vector2(1f, 1f);
            lgRt.anchoredPosition = new Vector2(-26f, -20f); // 인게임 HUD_GoldGroup과 동일
            var lgHL = lobbyGoldGroup.AddComponent<HorizontalLayoutGroup>();
            lgHL.childAlignment = TextAnchor.MiddleRight;
            lgHL.spacing = 6f;
            lgHL.childControlWidth = true; lgHL.childControlHeight = true;
            lgHL.childForceExpandWidth = false; lgHL.childForceExpandHeight = false;
            var lgCSF = lobbyGoldGroup.AddComponent<ContentSizeFitter>();
            lgCSF.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            lgCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject lobbyCoinObj = new GameObject("LobbyGoldCoin");
            lobbyCoinObj.transform.SetParent(lobbyGoldGroup.transform, false);
            lobbyCoinObj.AddComponent<RectTransform>();
            Image lobbyCoinImg = lobbyCoinObj.AddComponent<Image>();
            var coinSpr = Resources.Load<Sprite>("UI/gold_coin");
            if (coinSpr != null) lobbyCoinImg.sprite = coinSpr;
            lobbyCoinImg.raycastTarget = false;
            lobbyCoinImg.preserveAspect = true;
            var lobbyCoinLE = lobbyCoinObj.AddComponent<LayoutElement>();
            lobbyCoinLE.preferredWidth = 34f; lobbyCoinLE.preferredHeight = 34f;

            GameObject lobbyGoldObj = new GameObject("LobbyGold");
            lobbyGoldObj.transform.SetParent(lobbyGoldGroup.transform, false);
            lobbyGoldObj.AddComponent<RectTransform>();
            lobbyGoldText = lobbyGoldObj.AddComponent<Text>();
            lobbyGoldText.font = font;
            lobbyGoldText.fontSize = 28;
            lobbyGoldText.fontStyle = FontStyle.Bold;
            lobbyGoldText.alignment = TextAnchor.MiddleRight;
            lobbyGoldText.color = new Color(1f, 0.816f, 0.42f); // 인게임 골드 톤(#FFD06B)
            lobbyGoldText.raycastTarget = false;
            lobbyGoldText.horizontalOverflow = HorizontalWrapMode.Overflow;
            lobbyGoldText.verticalOverflow = VerticalWrapMode.Overflow;
            var lobbyGoldLE = lobbyGoldObj.AddComponent<LayoutElement>();
            lobbyGoldLE.minHeight = 34f;
            JewelsHexaPuzzle.Utils.NumberRoller.SetImmediate(lobbyGoldText, currentGold, v => v.ToString());
            Outline lobbyGoldOutline = lobbyGoldObj.AddComponent<Outline>();
            lobbyGoldOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            lobbyGoldOutline.effectDistance = new Vector2(1, 1);

            // === 이어서 플레이 히어로 CTA (헤더 아래, 스크롤 위) ===
            //   스크롤보다 먼저 생성 → StageScrollBuilder.AdjustRootBounds가 히어로 하단을 읽어 스크롤 상단을 내림
            CreateContinueHeroCard(lobbyContainer, font);

            // === 스크롤 영역 (StageScrollBuilder로 구축 — 범위는 빌더가 동적 계산) ===
            GameObject scrollObj = new GameObject("LevelScrollView");
            scrollObj.transform.SetParent(lobbyContainer.transform, false);
            scrollObj.AddComponent<RectTransform>(); // StageScrollBuilder.AdjustRootBounds에서 설정

            stageScrollBuilder = scrollObj.AddComponent<JewelsHexaPuzzle.UI.StageScrollBuilder>();
            stageScrollBuilder.Build(scoreManager, font, OnLobbyStageSelected);

            // === 하단 내비게이션 바 제거됨 (레벨/상점/이벤트/랭킹 탭) — 스크롤 하단 경계는 AdjustRootBounds 폴백(하단 버튼) 사용 ===

            // === 구독 버튼 (좌측 상단, 골드 표시 옆) ===
            CreateSubscriptionLobbyButton(lobbyContainer, font);

            // === 스킬 트리 버튼 (좌측 하단, 튜토리얼 초기화 위) ===
            CreateSkillTreeLobbyButton(lobbyContainer, font);

            // === 개발용 버튼 4종 — 에디터/개발 빌드 전용 ===
            //   특히 '전체 초기화'는 PlayerPrefs.DeleteAll()로 전 진행도를 삭제하므로
            //   릴리스 빌드에 절대 노출되면 안 됨 (감사 C3).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // === 튜토리얼 초기화 버튼 (좌측 하단) ===
            CreateTutorialResetButton(lobbyContainer, font);

            // === 레벨 모두해금 버튼 (튜토리얼 초기화 버튼 오른쪽) ===
            CreateUnlockAllButton(lobbyContainer, font);

            // === 전체 초기화 버튼 (모두해금 버튼 오른쪽) ===
            CreateResetAllDataButton(lobbyContainer, font);

            // === 레벨 활성화 버튼 (초기화 버튼 오른쪽) ===
            CreateLevelActivationButton(lobbyContainer, font);
#endif

            lobbyContainer.SetActive(false);
            lobbyContainer.transform.SetAsLastSibling();
            Debug.Log($"[GameManager] 로비 UI 생성 완료 (레벨 {allLevels.Count}개)");
        }


        // 상단바 v3(아바타/플레이어Lv/XP/하트/민트젬/햄버거)는 사용자 요청으로 제거됨(2026-07-02).
        // 골드는 인게임 HUD_GoldGroup과 동일 위치·디자인의 LobbyGoldGroup으로 대체.
        // 하단 5탭 내비게이션 바(레벨/상점/이벤트/랭킹)는 사용자 요청으로 제거됨 (CreateLobbyBottomNav, OnLobbyNavTab).

        /// <summary>
        /// '이어서 플레이' 히어로 CTA 카드 생성 (헤더 아래, 스크롤 위).
        /// 진행 프론티어(무한도전 제외, 가장 높은 해금 스테이지)를 단일 1순위 액션으로 제시.
        /// </summary>
        private void CreateContinueHeroCard(GameObject parent, Font font)
        {
            var unlocked = LevelRegistry.GetUnlockedLevels();
            LevelData target = null;
            for (int i = unlocked.Count - 1; i >= 0; i--)
            {
                if (unlocked[i].gameMode != GameMode.Infinite) { target = unlocked[i]; break; }
            }
            if (target == null && unlocked.Count > 0) target = unlocked[0];
            if (target == null) return;
            int targetLevelId = target.levelId;

            // 카드 컨테이너 (헤더 아래, 좌우 16 여백, 상단 104 아래, 높이 92)
            GameObject card = new GameObject("LobbyHeroCard");
            card.transform.SetParent(parent.transform, false);
            RectTransform cardRt = card.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0f, 1f);
            cardRt.anchorMax = new Vector2(1f, 1f);
            cardRt.pivot = new Vector2(0.5f, 1f);
            cardRt.offsetMax = new Vector2(-16f, -124f); // 헤더(116) 아래
            cardRt.offsetMin = new Vector2(16f, -216f);
            Image cardBg = card.AddComponent<Image>();
            cardBg.sprite = LobbyTheme.GlassPanelSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = Color.white;

            // 좌측 골드 액센트 스트립
            GameObject strip = new GameObject("Accent");
            strip.transform.SetParent(card.transform, false);
            RectTransform stripRt = strip.AddComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(0f, 1f);
            stripRt.pivot = new Vector2(0f, 0.5f);
            stripRt.anchoredPosition = new Vector2(8f, 0f);
            stripRt.sizeDelta = new Vector2(3f, -20f);
            Image stripImg = strip.AddComponent<Image>();
            stripImg.color = LobbyTheme.Gold;
            stripImg.raycastTarget = false;

            // 다음 레벨 노드 칩 (글래스 칩 + 골드 번호)
            GameObject node = new GameObject("HeroNode");
            node.transform.SetParent(card.transform, false);
            RectTransform nodeRt = node.AddComponent<RectTransform>();
            nodeRt.anchorMin = new Vector2(0f, 0.5f);
            nodeRt.anchorMax = new Vector2(0f, 0.5f);
            nodeRt.pivot = new Vector2(0f, 0.5f);
            nodeRt.anchoredPosition = new Vector2(18f, 0f);
            nodeRt.sizeDelta = new Vector2(58f, 58f);
            Image nodeImg = node.AddComponent<Image>();
            nodeImg.sprite = LobbyTheme.GlassChipSprite;
            nodeImg.type = Image.Type.Sliced;
            nodeImg.color = Color.white;
            nodeImg.raycastTarget = false;
            GameObject numObj = new GameObject("Num");
            numObj.transform.SetParent(node.transform, false);
            RectTransform numRt = numObj.AddComponent<RectTransform>();
            numRt.anchorMin = Vector2.zero; numRt.anchorMax = Vector2.one;
            numRt.offsetMin = Vector2.zero; numRt.offsetMax = Vector2.zero;
            Text numText = numObj.AddComponent<Text>();
            numText.font = font; numText.fontSize = 24; numText.alignment = TextAnchor.MiddleCenter;
            numText.color = LobbyTheme.Gold; numText.raycastTarget = false;
            numText.text = targetLevelId.ToString();

            // 가운데 텍스트 (캡션 + 챕터명)
            GameObject mid = new GameObject("HeroText");
            mid.transform.SetParent(card.transform, false);
            RectTransform midRt = mid.AddComponent<RectTransform>();
            midRt.anchorMin = new Vector2(0f, 0f);
            midRt.anchorMax = new Vector2(1f, 1f);
            midRt.offsetMin = new Vector2(88f, 8f);
            midRt.offsetMax = new Vector2(-120f, -8f);
            GameObject capObj = new GameObject("Caption");
            capObj.transform.SetParent(mid.transform, false);
            RectTransform capRt = capObj.AddComponent<RectTransform>();
            capRt.anchorMin = new Vector2(0f, 0.5f); capRt.anchorMax = new Vector2(1f, 1f);
            capRt.offsetMin = Vector2.zero; capRt.offsetMax = Vector2.zero;
            Text capText = capObj.AddComponent<Text>();
            capText.font = font; capText.fontSize = 13; capText.alignment = TextAnchor.LowerLeft;
            capText.color = LobbyTheme.Gold; capText.raycastTarget = false;
            capText.text = $"이어서 플레이 · 레벨 {targetLevelId}";
            GameObject chObj = new GameObject("Chapter");
            chObj.transform.SetParent(mid.transform, false);
            RectTransform chRt = chObj.AddComponent<RectTransform>();
            chRt.anchorMin = new Vector2(0f, 0f); chRt.anchorMax = new Vector2(1f, 0.5f);
            chRt.offsetMin = Vector2.zero; chRt.offsetMax = Vector2.zero;
            Text chText = chObj.AddComponent<Text>();
            chText.font = font; chText.fontSize = 17; chText.alignment = TextAnchor.UpperLeft;
            chText.color = LobbyTheme.TextPrimary; chText.raycastTarget = false;
            chText.text = string.IsNullOrEmpty(target.chapterName)
                ? (target.subtitle ?? "")
                : $"제{target.chapterId}장 · {target.chapterName}";

            // 우측 PLAY 버튼 (골드, 글로우는 단일 1순위 액션이므로 유지)
            GameObject playBtnObj = new GameObject("HeroPlay");
            playBtnObj.transform.SetParent(card.transform, false);
            RectTransform playRt = playBtnObj.AddComponent<RectTransform>();
            playRt.anchorMin = new Vector2(1f, 0.5f);
            playRt.anchorMax = new Vector2(1f, 0.5f);
            playRt.pivot = new Vector2(1f, 0.5f);
            playRt.anchoredPosition = new Vector2(-14f, 0f);
            playRt.sizeDelta = new Vector2(92f, 60f);
            Image playBg = playBtnObj.AddComponent<Image>();
            playBg.sprite = ClaudeTheme.MakeRounded(64, 16, LobbyTheme.Gold, new Color(0f, 0f, 0f, 0f), 0);
            playBg.type = Image.Type.Sliced;
            playBg.color = Color.white;
            Button playButton = playBtnObj.AddComponent<Button>();
            playButton.targetGraphic = playBg;
            GameObject plObj = new GameObject("Label");
            plObj.transform.SetParent(playBtnObj.transform, false);
            RectTransform plRt = plObj.AddComponent<RectTransform>();
            plRt.anchorMin = Vector2.zero; plRt.anchorMax = Vector2.one;
            plRt.offsetMin = Vector2.zero; plRt.offsetMax = Vector2.zero;
            Text plText = plObj.AddComponent<Text>();
            plText.font = font; plText.fontSize = 18; plText.alignment = TextAnchor.MiddleCenter;
            plText.color = new Color(0.353f, 0.239f, 0f); // 진한 골드 텍스트
            plText.raycastTarget = false;
            plText.text = "▶ 시작";
            int captured = targetLevelId;
            playButton.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                OnLobbyStageSelected(captured);
            });
        }

        /// <summary>
        /// 로비 스테이지 버튼 클릭 콜백 (StageScrollBuilder에서 호출)
        /// </summary>
        private void OnLobbyStageSelected(int stageNum)
        {
            // ★ 레벨 활성화 모드: 클릭한 레벨까지 해금, 이후 잠금
            if (isLevelActivationMode)
            {
                isLevelActivationMode = false;
                LevelRegistry.UnlockLevelsUpTo(stageNum);

                // 기능 해금 동기화
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.ResetNotifiedFeatures();
                    TutorialManager.Instance.SyncFeatureUnlocks();
                }

                Debug.Log($"[GameManager] 레벨 활성화: 1~{stageNum} 해금 완료");

                // 로비 UI 재생성 (잠금/해금 비주얼 갱신)
                StartCoroutine(RefreshLobbyAfterLevelActivation(stageNum));
                return;
            }

            selectedStage = stageNum;

            var levelData = LevelRegistry.GetLevel(stageNum);
            if (levelData != null)
            {
                currentGameMode = levelData.gameMode;
                selectedLevelData = levelData;
            }
            else
            {
                currentGameMode = (stageNum == 1) ? GameMode.Stage : GameMode.Infinite;
                selectedLevelData = null;
            }

            HideLobby();
            StartGame();
        }

        /// <summary>
        /// 로비 좌측 하단에 튜토리얼 초기화 버튼 생성
        /// </summary>
        /// <summary>
        /// 스킬 트리 로비 버튼 (좌측 하단, 튜토리얼 초기화 버튼 위, 육각형)
        /// </summary>
        private void CreateSkillTreeLobbyButton(GameObject parent, Font font)
        {
            // ★ 스킬트리 버튼은 항상 생성하되, 표시 여부는 ShowLobby의 UpdateLobbyConditionalButtons에서 제어
            //   (레벨 21 이상 클리어 시 표시 + 첫 등장 힌트 안내)

            float btnSize = 80f;

            GameObject btnObj = new GameObject("SkillTreeButton");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            // 튜토리얼 초기화 버튼(20,20 size 180x45) 위에 배치
            btnRt.anchoredPosition = new Vector2(20f, 80f);
            btnRt.sizeDelta = new Vector2(btnSize, btnSize);

            // 육각형 배경
            Image hexBg = btnObj.AddComponent<Image>();
            hexBg.sprite = HexBlock.GetHexFlashSprite();
            hexBg.type = Image.Type.Simple;
            hexBg.preserveAspect = true;
            hexBg.color = new Color(0.35f, 0.55f, 0.85f, 0.92f); // 파란 계열

            // 육각형 테두리
            GameObject borderObj = new GameObject("HexBorder");
            borderObj.transform.SetParent(btnObj.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = HexBlock.GetHexBorderSprite();
            borderImg.type = Image.Type.Simple;
            borderImg.preserveAspect = true;
            borderImg.color = new Color(0.6f, 0.8f, 1f, 0.8f);
            borderImg.raycastTarget = false;

            // 버튼 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.9f, 0.95f, 1f);
            colors.pressedColor = new Color(0.6f, 0.7f, 0.8f);
            btn.colors = colors;

            // 아이콘: 별 모양 텍스트 (★)
            GameObject iconObj = new GameObject("SkillIcon");
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchoredPosition = new Vector2(0f, 13f);   // 상단으로 이동 — "스킬" 텍스트 자리 확보
            iconRt.sizeDelta = new Vector2(40f, 32f);         // 박스 축소 (별 글리프 영역만)
            Text iconText = iconObj.AddComponent<Text>();
            iconText.font = font;
            iconText.fontSize = 26;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = new Color(1f, 0.95f, 0.7f, 0.95f);
            iconText.raycastTarget = false;
            iconText.text = "\u2605"; // ★

            Outline iconOutline = iconObj.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0f, 0f, 0f, 0.5f);
            iconOutline.effectDistance = new Vector2(1f, 1f);

            // "스킬" 라벨 — 버튼 안쪽, 별 바로 아래에 배치 (겹치지 않도록 별 박스(-3) 아래로)
            //   별 박스 y∈[-3, +29], 라벨 박스 y∈[-23, -9] → 6px 간격
            GameObject labelObj = new GameObject("SkillLabel");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchoredPosition = new Vector2(0f, -16f);  // 버튼 안쪽 하단부 (별 아래)
            labelRt.sizeDelta = new Vector2(60f, 14f);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 13;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(1f, 0.95f, 0.85f, 0.95f);
            labelText.raycastTarget = false;
            labelText.text = "스킬";

            // 텍스트 가독성을 위한 어두운 아웃라인
            Outline labelOutline = labelObj.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            labelOutline.effectDistance = new Vector2(1f, -1f);

            // ★ 학습 가능한 스킬이 있을 때 표시되는 빨간 점 (우상단)
            //   조건: HasAnyLearnableSkill() == true (3시간 쿨다운 제거 — 학습 가능하면 항상 노출)
            //   크기 24px + 원형 스프라이트 + 흰 테두리 + 펄스 애니메이션 → 한눈에 띄도록
            GameObject dotObj = new GameObject("LearnableDot");
            dotObj.transform.SetParent(btnObj.transform, false);
            RectTransform dotRt = dotObj.AddComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(1f, 1f);
            dotRt.anchorMax = new Vector2(1f, 1f);
            // pivot을 우상단으로 → anchoredPosition (-3,-3) 시 점의 우상단 모서리가 버튼 우상단에서 3px 안쪽
            dotRt.pivot = new Vector2(1f, 1f);
            dotRt.anchoredPosition = new Vector2(-2f, -2f);   // 버튼 안쪽 우상단 모서리 라인에서 2px 여유
            dotRt.sizeDelta = new Vector2(8f, 8f);            // 원형으로 인식되는 최소 사이즈 (AA 포함)

            Image dotImg = dotObj.AddComponent<Image>();
            dotImg.sprite = GetOrCreateCircleSprite();        // 진짜 원형 스프라이트
            dotImg.type = Image.Type.Simple;
            dotImg.preserveAspect = true;
            dotImg.color = new Color(1.0f, 0.18f, 0.18f, 1.0f); // 강렬한 빨간색
            dotImg.raycastTarget = false;
            // 테두리 제거 — 깔끔한 단색 원

            // 펄스 애니메이션 (3초 주기, 0.85↔1.15 스케일) — 시선 끌기
            StartCoroutine(LearnableDotPulseLoop(dotRt));

            // 참조 저장 + 초기 상태 결정
            skillTreeRedDot = dotObj;
            UpdateSkillTreeRedDot();

            // 폴링 루프 시작 — 1분마다 재평가 (3시간 쿨다운 만료 자동 감지용)
            if (skillTreeRedDotPollCo != null) StopCoroutine(skillTreeRedDotPollCo);
            skillTreeRedDotPollCo = StartCoroutine(SkillTreeRedDotPollLoop());

            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                if (skillTreeUI != null)
                    skillTreeUI.Show();
            });

            // 초기 비활성 — ShowLobby의 UpdateLobbyConditionalButtons에서 조건부 표시
            btnObj.SetActive(false);
            // 스킬트리 버튼 참조 저장 (ShowLobby에서 토글)
            skillTreeButtonObj = btnObj;
        }

        /// <summary>스킬트리 버튼 GameObject 참조 (ShowLobby에서 조건부 활성화)</summary>
        private GameObject skillTreeButtonObj;

        // ============================================================
        // 스킬트리 알림 인디케이터 (빨간 점)
        // ============================================================

        /// <summary>로비 스킬트리 버튼 우상단 빨간 점 GameObject 참조</summary>
        private GameObject skillTreeRedDot;
        /// <summary>1분 주기 빨간 점 갱신 코루틴 핸들</summary>
        private Coroutine skillTreeRedDotPollCo;

        /// <summary>
        /// 빨간 점 표시 여부 재평가 후 GameObject 활성화 갱신.
        /// 외부 호출: 스킬 해금 직후, 스킬트리 닫힘 직후, 로비 진입 시.
        /// </summary>
        public void UpdateSkillTreeRedDot()
        {
            if (skillTreeRedDot == null) return;
            skillTreeRedDot.SetActive(ShouldShowSkillTreeRedDot());
        }

        /// <summary>
        /// 빨간 점 표시 조건: 학습 가능 스킬 존재 시 항상 표시 (이전 3시간 쿨다운 제거).
        ///   - SP/골드/선행/레벨 게이트가 모두 통과되어 즉시 해금 가능한 스킬이 1개 이상이면 ON
        ///   - 사용자가 스킬을 해금하거나 살 수 없는 상태가 되면 자동 OFF
        /// </summary>
        private bool ShouldShowSkillTreeRedDot()
        {
            if (SkillTreeManager.Instance == null) return false;
            return SkillTreeManager.Instance.HasAnyLearnableSkill();
        }

        // ============================================================
        // 빨간 점 펄스 애니메이션 + 원형 스프라이트 헬퍼
        // ============================================================

        /// <summary>
        /// 빨간 점 펄스 (스케일 0.85 ↔ 1.15) — 1초 주기에서 3초 주기로 완화.
        /// 사용자가 부담스럽지 않게 천천히 호흡하듯 펄스.
        /// </summary>
        private System.Collections.IEnumerator LearnableDotPulseLoop(RectTransform rt)
        {
            const float PULSE_PERIOD = 3f; // 3초 주기 (이전 1초의 1/3)
            while (rt != null)
            {
                if (rt.gameObject.activeInHierarchy)
                {
                    float t = Time.unscaledTime;
                    float scale = 1.0f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f / PULSE_PERIOD);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }
        }

        /// <summary>원형 스프라이트 (256×256, 캐시) — 노티피케이션 점 등에 사용.</summary>
        private static Sprite _learnableDotCircleSprite;
        private static Sprite GetOrCreateCircleSprite()
        {
            if (_learnableDotCircleSprite != null) return _learnableDotCircleSprite;
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            float radius = center;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float aa = 1.0f - Mathf.Clamp01(dist - (radius - 1f));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, aa);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _learnableDotCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _learnableDotCircleSprite;
        }

        /// <summary>1분마다 빨간 점 상태 재평가 — 3시간 쿨다운 만료 자동 감지용</summary>
        private System.Collections.IEnumerator SkillTreeRedDotPollLoop()
        {
            while (skillTreeRedDot != null)
            {
                yield return new WaitForSeconds(60f);
                UpdateSkillTreeRedDot();
            }
        }

        // ============================================================
        // 프리미엄 구독 시스템 (좌상단 로비 버튼 + 상세 팝업 + 구매 확정 팝업)
        // ============================================================

        private GameObject subscriptionButtonObj;       // 로비 좌상단 구독 버튼
        private Text subscriptionStatusText;            // 버튼 아래 "구독중 Xd Yh" 라벨
        private Coroutine subscriptionStatusCo;         // 라벨 1초 갱신 코루틴

        private void CreateSubscriptionLobbyButton(GameObject parent, Font font)
        {
            float btnSize = 80f;

            // 구독 버튼은 골드 표시(우상단)와 같은 상단 라인에 맞춤
            //   → 골드 top edge: anchoredPos.y = -20
            //   → 구독 버튼도 상단 = -20 (좌상단 anchor 기준)
            const float TOP_ALIGN_Y = -20f;

            GameObject btnObj = new GameObject("SubscriptionButton");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 1f);
            btnRt.anchorMax = new Vector2(0f, 1f);
            btnRt.pivot = new Vector2(0f, 1f);
            btnRt.anchoredPosition = new Vector2(20f, TOP_ALIGN_Y); // 골드와 같은 상단 라인
            btnRt.sizeDelta = new Vector2(btnSize, btnSize);

            // 헥스 배경
            Image hexBg = btnObj.AddComponent<Image>();
            hexBg.sprite = HexBlock.GetHexFlashSprite();
            hexBg.type = Image.Type.Simple;
            hexBg.preserveAspect = true;
            hexBg.color = new Color(0.95f, 0.55f, 0.20f, 0.95f); // 황금-주황 (프리미엄 느낌)

            // 헥스 테두리
            GameObject borderObj = new GameObject("HexBorder");
            borderObj.transform.SetParent(btnObj.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero; borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero; borderRt.offsetMax = Vector2.zero;
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = HexBlock.GetHexBorderSprite();
            borderImg.type = Image.Type.Simple;
            borderImg.preserveAspect = true;
            borderImg.color = new Color(1f, 0.85f, 0.4f, 0.9f);
            borderImg.raycastTarget = false;

            // 버튼
            Button btn = btnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(1f, 0.7f, 0.3f);
            btnColors.pressedColor = new Color(0.7f, 0.4f, 0.15f);
            btn.colors = btnColors;

            // 아이콘: ★ 별
            GameObject iconObj = new GameObject("PremiumIcon");
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchoredPosition = new Vector2(0f, 13f);
            iconRt.sizeDelta = new Vector2(40f, 32f);
            Text iconText = iconObj.AddComponent<Text>();
            iconText.font = font;
            iconText.fontSize = 28;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = new Color(1f, 1f, 0.85f, 1f);
            iconText.raycastTarget = false;
            iconText.text = "★"; // ★
            Outline iconOutline = iconObj.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0.4f, 0.2f, 0f, 0.8f);
            iconOutline.effectDistance = new Vector2(1f, -1f);

            // "구독" 라벨 (버튼 안)
            GameObject labelObj = new GameObject("SubLabel");
            labelObj.transform.SetParent(btnObj.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchoredPosition = new Vector2(0f, -16f);
            labelRt.sizeDelta = new Vector2(60f, 14f);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 13;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(1f, 0.95f, 0.85f, 0.95f);
            labelText.raycastTarget = false;
            labelText.text = "구독";
            Outline labelOutline = labelObj.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0.4f, 0.2f, 0f, 0.85f);
            labelOutline.effectDistance = new Vector2(1f, -1f);

            // 잔여 시간 라벨 (버튼 아래)
            GameObject statusObj = new GameObject("SubStatus");
            statusObj.transform.SetParent(parent.transform, false);
            RectTransform statusRt = statusObj.AddComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(0f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.anchoredPosition = new Vector2(20f, TOP_ALIGN_Y - btnSize - 8f);
            statusRt.sizeDelta = new Vector2(140f, 22f);
            Text statusText = statusObj.AddComponent<Text>();
            statusText.font = font;
            statusText.fontSize = 14;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.UpperLeft;
            statusText.color = new Color(1f, 0.92f, 0.5f, 1f);
            statusText.raycastTarget = false;
            statusText.text = "";
            Outline statusOutline = statusObj.AddComponent<Outline>();
            statusOutline.effectColor = new Color(0.2f, 0.1f, 0f, 0.85f);
            statusOutline.effectDistance = new Vector2(1f, -1f);

            subscriptionButtonObj = btnObj;
            subscriptionStatusText = statusText;
            subscriptionStatusObj = statusObj; // 라벨도 함께 토글하기 위해 참조 저장

            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                ShowSubscriptionDetailPopup();
            });

            // 1초마다 잔여 시간 갱신
            if (subscriptionStatusCo != null) StopCoroutine(subscriptionStatusCo);
            subscriptionStatusCo = StartCoroutine(SubscriptionStatusUpdateLoop());
            UpdateSubscriptionStatusLabel(); // 즉시 1회 갱신

            // 초기 비활성 — ShowLobby의 UpdateLobbyConditionalButtons에서 조건부 표시
            btnObj.SetActive(false);
            statusObj.SetActive(false);
        }

        /// <summary>구독 상태 라벨 GameObject (버튼과 함께 토글)</summary>
        private GameObject subscriptionStatusObj;

        private System.Collections.IEnumerator SubscriptionStatusUpdateLoop()
        {
            while (subscriptionStatusText != null)
            {
                UpdateSubscriptionStatusLabel();
                yield return new WaitForSeconds(1f);
            }
        }

        private void UpdateSubscriptionStatusLabel()
        {
            if (subscriptionStatusText == null) return;
            if (SubscriptionManager.Instance == null) { subscriptionStatusText.text = ""; return; }
            subscriptionStatusText.text = SubscriptionManager.Instance.GetStatusLabel();
        }

        // ============================================================
        // 구독 상세 팝업
        // ============================================================

        private GameObject subscriptionDetailPopup;

        private void ShowSubscriptionDetailPopup()
        {
            CloseSubscriptionDetailPopup();

            Canvas canvas = lobbyContainer != null ? lobbyContainer.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 풀스크린 어두운 오버레이 (외부 클릭 = 닫기)
            GameObject popup = new GameObject("SubscriptionDetailPopup");
            popup.transform.SetParent(canvas.transform, false);
            RectTransform popupRt = popup.AddComponent<RectTransform>();
            popupRt.anchorMin = Vector2.zero;
            popupRt.anchorMax = Vector2.one;
            popupRt.offsetMin = Vector2.zero;
            popupRt.offsetMax = Vector2.zero;
            Image bgImg = popup.AddComponent<Image>();
            bgImg.color = ClaudeTheme.PopupOverlay;
            Button bgBtn = popup.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(CloseSubscriptionDetailPopup); // 외부 클릭 닫기

            // 팝업 패널
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(popup.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(540f, 660f);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.13f, 0.10f, 0.08f, 0.97f);
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 0.85f, 0.4f, 0.9f);
            panelOutline.effectDistance = new Vector2(2f, -2f);
            // 패널 내부 클릭 흡수 (외부 닫힘 방지)
            Button panelBtn = panel.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None;

            // 제목
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -22f);
            titleRt.sizeDelta = new Vector2(-40f, 50f);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 30;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.92f, 0.55f, 1f);
            titleText.text = "★ 프리미엄 구독";
            titleText.raycastTarget = false;

            // 설명
            GameObject descObj = new GameObject("Desc");
            descObj.transform.SetParent(panel.transform, false);
            RectTransform descRt = descObj.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0f, 1f); descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0.5f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -78f);
            descRt.sizeDelta = new Vector2(-50f, 90f);
            Text descText = descObj.AddComponent<Text>();
            descText.font = font;
            descText.fontSize = 18;
            descText.alignment = TextAnchor.UpperCenter;
            descText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            descText.text = "구독하면 마나가 30 → 100으로 상승해요!\n특수 블록과 아이템을 자주 사용해\n전략의 폭을 넓혀 보세요.";
            descText.raycastTarget = false;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Overflow;

            // 4개 티어 버튼
            float tierY = -220f;
            float tierH = 70f;
            float gap = 14f;
            for (int i = 0; i < SubscriptionManager.Tiers.Length; i++)
            {
                var tier = SubscriptionManager.Tiers[i];
                GameObject tBtnObj = CreateTierButton(panel.transform, font, tier, tierY - i * (tierH + gap), tierH);
            }

            // "상세" 버튼 (혜택 자세히 보기)
            GameObject detailBtnObj = new GameObject("DetailButton");
            detailBtnObj.transform.SetParent(panel.transform, false);
            RectTransform detailBtnRt = detailBtnObj.AddComponent<RectTransform>();
            detailBtnRt.anchorMin = new Vector2(0f, 0f);
            detailBtnRt.anchorMax = new Vector2(0.5f, 0f);
            detailBtnRt.pivot = new Vector2(0.5f, 0f);
            detailBtnRt.anchoredPosition = new Vector2(135f, 16f);
            detailBtnRt.sizeDelta = new Vector2(-40f, 50f);
            Image detailBtnImg = detailBtnObj.AddComponent<Image>();
            detailBtnImg.color = new Color(0.30f, 0.20f, 0.10f, 1f);
            Button detailBtn = detailBtnObj.AddComponent<Button>();
            detailBtn.transition = Selectable.Transition.ColorTint;
            GameObject detailTextObj = new GameObject("Text");
            detailTextObj.transform.SetParent(detailBtnObj.transform, false);
            RectTransform detailTxtRt = detailTextObj.AddComponent<RectTransform>();
            detailTxtRt.anchorMin = Vector2.zero; detailTxtRt.anchorMax = Vector2.one;
            detailTxtRt.offsetMin = Vector2.zero; detailTxtRt.offsetMax = Vector2.zero;
            Text detailTxt = detailTextObj.AddComponent<Text>();
            detailTxt.font = font; detailTxt.fontSize = 18; detailTxt.fontStyle = FontStyle.Bold;
            detailTxt.alignment = TextAnchor.MiddleCenter;
            detailTxt.color = new Color(1f, 0.9f, 0.6f, 1f);
            detailTxt.text = "상세";
            detailTxt.raycastTarget = false;
            detailBtn.onClick.AddListener(ShowSubscriptionExtraDetail);

            // 닫기 X
            GameObject closeObj = new GameObject("Close");
            closeObj.transform.SetParent(panel.transform, false);
            RectTransform closeRt = closeObj.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f); closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-12f, -12f);
            closeRt.sizeDelta = new Vector2(40f, 40f);
            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.4f, 0.2f, 0.2f, 0.9f);
            Button closeBtn = closeObj.AddComponent<Button>();
            GameObject closeTxtObj = new GameObject("Text");
            closeTxtObj.transform.SetParent(closeObj.transform, false);
            RectTransform closeTxtRt = closeTxtObj.AddComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero; closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.offsetMin = Vector2.zero; closeTxtRt.offsetMax = Vector2.zero;
            Text closeTxt = closeTxtObj.AddComponent<Text>();
            closeTxt.font = font; closeTxt.fontSize = 22; closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            closeTxt.text = "X";
            closeTxt.raycastTarget = false;
            closeBtn.onClick.AddListener(CloseSubscriptionDetailPopup);

            subscriptionDetailPopup = popup;
            popup.transform.SetAsLastSibling();
        }

        private GameObject CreateTierButton(Transform parent, Font font, SubscriptionManager.Tier tier, float y, float height)
        {
            GameObject obj = new GameObject($"Tier_{tier.hours}h");
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(460f, height);
            Image bg = obj.AddComponent<Image>();
            bg.color = new Color(0.20f, 0.15f, 0.10f, 1f);
            Outline ol = obj.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 0.7f, 0.2f, 0.7f);
            ol.effectDistance = new Vector2(1f, -1f);
            Button btn = obj.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            var cs = btn.colors;
            cs.highlightedColor = new Color(1f, 0.85f, 0.5f);
            btn.colors = cs;

            // 라벨 (좌측)
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(obj.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.5f); labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(20f, 0f);
            labelRt.sizeDelta = new Vector2(200f, 40f);
            Text labelTxt = labelObj.AddComponent<Text>();
            labelTxt.font = font; labelTxt.fontSize = 22; labelTxt.fontStyle = FontStyle.Bold;
            labelTxt.alignment = TextAnchor.MiddleLeft;
            labelTxt.color = new Color(1f, 0.95f, 0.85f, 1f);
            labelTxt.text = tier.label;
            labelTxt.raycastTarget = false;

            // 가격 (우측)
            GameObject priceObj = new GameObject("Price");
            priceObj.transform.SetParent(obj.transform, false);
            RectTransform priceRt = priceObj.AddComponent<RectTransform>();
            priceRt.anchorMin = new Vector2(1f, 0.5f); priceRt.anchorMax = new Vector2(1f, 0.5f);
            priceRt.pivot = new Vector2(1f, 0.5f);
            priceRt.anchoredPosition = new Vector2(-20f, 0f);
            priceRt.sizeDelta = new Vector2(220f, 40f);
            Text priceTxt = priceObj.AddComponent<Text>();
            priceTxt.font = font; priceTxt.fontSize = 22; priceTxt.fontStyle = FontStyle.Bold;
            priceTxt.alignment = TextAnchor.MiddleRight;
            priceTxt.color = new Color(1f, 0.85f, 0.4f, 1f);
            priceTxt.text = $"{tier.priceWon:N0}원";
            priceTxt.raycastTarget = false;

            int hours = tier.hours;
            btn.onClick.AddListener(() => OnTierPurchased(hours));
            return obj;
        }

        private void ShowSubscriptionExtraDetail()
        {
            // 간단히 토스트로 추가 안내 (UIManager 활용)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowToast("프리미엄: 마나 100, 한 게임 더 자주 특수 블록 사용 가능!");
            }
        }

        private void CloseSubscriptionDetailPopup()
        {
            if (subscriptionDetailPopup != null)
            {
                Destroy(subscriptionDetailPopup);
                subscriptionDetailPopup = null;
            }
        }

        // ============================================================
        // 티어 클릭 → 구매 확정 팝업
        // ============================================================

        private GameObject purchaseConfirmPopup;

        private void OnTierPurchased(int hours)
        {
            // 상세 팝업 닫기
            CloseSubscriptionDetailPopup();

            // 실제 결제 프로세서 자리 (현재는 더미: 즉시 적용)
            if (SubscriptionManager.Instance == null) return;
            var result = SubscriptionManager.Instance.Subscribe(hours);

            ShowPurchaseConfirmPopup(result);
            UpdateSubscriptionStatusLabel(); // 즉시 라벨 갱신
        }

        private void ShowPurchaseConfirmPopup(SubscriptionManager.PurchaseResult result)
        {
            // 기존 팝업이 있으면 제거 (안전)
            if (purchaseConfirmPopup != null) Destroy(purchaseConfirmPopup);

            Canvas canvas = lobbyContainer != null ? lobbyContainer.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject popup = new GameObject("PurchaseConfirmPopup");
            popup.transform.SetParent(canvas.transform, false);
            RectTransform popupRt = popup.AddComponent<RectTransform>();
            popupRt.anchorMin = Vector2.zero; popupRt.anchorMax = Vector2.one;
            popupRt.offsetMin = Vector2.zero; popupRt.offsetMax = Vector2.zero;
            Image bgImg = popup.AddComponent<Image>();
            bgImg.color = ClaudeTheme.PopupOverlay;
            // ★ 외부 클릭 닫힘 차단 — Button 추가 + onClick 비워두면 모든 영역 클릭 흡수
            Button bgBtn = popup.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            // onClick에 빈 리스너 안 추가 → 외부 클릭 시 아무 동작 없음

            // 패널
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(popup.transform, false);
            RectTransform panelRt = panel.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f); panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(520f, 360f);
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.13f, 0.18f, 0.97f);
            Outline panelOl = panel.AddComponent<Outline>();
            panelOl.effectColor = new Color(0.5f, 0.85f, 1f, 0.85f);
            panelOl.effectDistance = new Vector2(2f, -2f);
            Button panelBtn = panel.AddComponent<Button>();
            panelBtn.transition = Selectable.Transition.None; // 패널 클릭 흡수

            // 제목
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(panel.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -22f);
            titleRt.sizeDelta = new Vector2(-40f, 44f);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.6f, 0.9f, 1f, 1f);
            titleText.text = "구매 완료!";
            titleText.raycastTarget = false;

            // 본문
            string body = BuildPurchaseConfirmMessage(result);
            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(panel.transform, false);
            RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f); bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(30f, 90f);
            bodyRt.offsetMax = new Vector2(-30f, -78f);
            Text bodyText = bodyObj.AddComponent<Text>();
            bodyText.font = font;
            bodyText.fontSize = 19;
            bodyText.alignment = TextAnchor.UpperCenter;
            bodyText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            bodyText.text = body;
            bodyText.raycastTarget = false;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.lineSpacing = 1.3f;

            // 확인 버튼 (하단)
            GameObject okObj = new GameObject("ConfirmButton");
            okObj.transform.SetParent(panel.transform, false);
            RectTransform okRt = okObj.AddComponent<RectTransform>();
            okRt.anchorMin = new Vector2(0.5f, 0f); okRt.anchorMax = new Vector2(0.5f, 0f);
            okRt.pivot = new Vector2(0.5f, 0f);
            okRt.anchoredPosition = new Vector2(0f, 18f);
            okRt.sizeDelta = new Vector2(180f, 56f);
            Image okImg = okObj.AddComponent<Image>();
            okImg.color = new Color(0.20f, 0.55f, 0.85f, 1f);
            Outline okOl = okObj.AddComponent<Outline>();
            okOl.effectColor = new Color(0.5f, 0.9f, 1f, 0.85f);
            okOl.effectDistance = new Vector2(1.5f, -1.5f);
            Button okBtn = okObj.AddComponent<Button>();
            GameObject okTxtObj = new GameObject("Text");
            okTxtObj.transform.SetParent(okObj.transform, false);
            RectTransform okTxtRt = okTxtObj.AddComponent<RectTransform>();
            okTxtRt.anchorMin = Vector2.zero; okTxtRt.anchorMax = Vector2.one;
            okTxtRt.offsetMin = Vector2.zero; okTxtRt.offsetMax = Vector2.zero;
            Text okTxt = okTxtObj.AddComponent<Text>();
            okTxt.font = font; okTxt.fontSize = 24; okTxt.fontStyle = FontStyle.Bold;
            okTxt.alignment = TextAnchor.MiddleCenter;
            okTxt.color = Color.white;
            okTxt.text = "확인";
            okTxt.raycastTarget = false;
            okBtn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                ClosePurchaseConfirmPopup();
            });

            purchaseConfirmPopup = popup;
            popup.transform.SetAsLastSibling();
        }

        private string BuildPurchaseConfirmMessage(SubscriptionManager.PurchaseResult result)
        {
            // 종료 시각 표시 (로컬 시간)
            System.DateTime endLocal = result.newEndUtc.ToLocalTime();
            string endStr = endLocal.ToString("yyyy-MM-dd HH:mm");

            if (result.wasSubscribed)
            {
                // 추가 구매 — 보너스 10% 안내
                int totalRemainHours = Mathf.FloorToInt((float)result.totalRemainingHours);
                return
                    $"{result.purchasedHours}시간 구독을 추가 구매했어요!\n\n" +
                    $"보너스 +{result.bonusHours}시간 (10%) 적립\n" +
                    $"이번 추가: 총 {result.totalAddedHours}시간\n\n" +
                    $"전체 잔여 시간: 약 {totalRemainHours}시간\n" +
                    $"종료 시각: {endStr}";
            }
            else
            {
                // 첫 구독
                return
                    $"{result.purchasedHours}시간 구독을 구매했어요!\n\n" +
                    $"마나가 30 → 100으로 상승했어요.\n" +
                    $"특수 블록과 아이템을 마음껏 사용해 보세요!\n\n" +
                    $"종료 시각: {endStr}";
            }
        }

        private void ClosePurchaseConfirmPopup()
        {
            if (purchaseConfirmPopup != null)
            {
                Destroy(purchaseConfirmPopup);
                purchaseConfirmPopup = null;
            }
        }

        // ============================================================
        // 조건부 로비 버튼 표시 + 첫 등장 힌트
        // ============================================================

        private const string KEY_SUB_HINT_SHOWN = "Subscription_HintShown";
        private const string KEY_SKILLTREE_HINT_SHOWN = "SkillTree_HintShown";

        /// <summary>
        /// 로비 표시 시마다 호출 — 진행 상황에 따라 조건부 버튼(구독/스킬트리) 활성화
        /// + 처음 등장한 버튼이 있으면 힌트(밝은 펄스 + 말풍선) 표시.
        /// </summary>
        private void UpdateLobbyConditionalButtons()
        {
            int highest = SkillTreeManager.Instance != null
                ? SkillTreeManager.Instance.GetHighestReachedLevel() : 0;

            // v3: 레거시 플로팅 스킬/구독 버튼은 상단바 아바타 + 하단 내비(스킬 탭)로 대체 → 항상 숨김
            bool showSkillTree = false;
            if (skillTreeButtonObj != null)
                skillTreeButtonObj.SetActive(showSkillTree);

            bool showSubscription = false;
            if (subscriptionButtonObj != null)
                subscriptionButtonObj.SetActive(showSubscription);
            if (subscriptionStatusObj != null)
                subscriptionStatusObj.SetActive(showSubscription);

            // 첫 등장 힌트 — 표시 조건 충족 + 힌트 미열람 시
            //   각 버튼 위에 펄스 애니메이션 + 옆에 말풍선 안내
            //   사용자가 말풍선을 탭하면 닫히고 PlayerPrefs 마킹
            if (showSkillTree && PlayerPrefs.GetInt(KEY_SKILLTREE_HINT_SHOWN, 0) == 0
                && skillTreeButtonObj != null)
            {
                ShowFirstAppearanceHint(
                    skillTreeButtonObj,
                    "스킬트리 해금!",
                    "스킬 포인트로 드릴/폭탄 등\n특수 블록을 강화하고\n새 능력을 해금해 보세요!",
                    KEY_SKILLTREE_HINT_SHOWN);
            }
            else if (showSubscription && PlayerPrefs.GetInt(KEY_SUB_HINT_SHOWN, 0) == 0
                     && subscriptionButtonObj != null)
            {
                // 스킬트리 힌트가 표시 중이면 구독 힌트는 다음 로비 진입에 표시 (한 번에 하나만)
                ShowFirstAppearanceHint(
                    subscriptionButtonObj,
                    "프리미엄 구독!",
                    "구독으로 마나를 30 → 100\n으로 늘려 보세요!\n특수 블록을 자주 사용 가능.",
                    KEY_SUB_HINT_SHOWN);
            }
        }

        /// <summary>
        /// 버튼 위에 펄스 애니메이션 + 옆에 말풍선을 표시하는 첫 등장 힌트.
        /// 말풍선/배경 탭 시 닫히고 PlayerPrefs에 hintShownKey=1 저장.
        /// </summary>
        private void ShowFirstAppearanceHint(GameObject targetBtn, string title, string body, string hintShownKey)
        {
            if (targetBtn == null) return;
            Canvas canvas = lobbyContainer != null ? lobbyContainer.GetComponentInParent<Canvas>() : FindObjectOfType<Canvas>();
            if (canvas == null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1) 펄스 애니메이션 (버튼 자체 스케일)
            StartHintPulse(targetBtn);

            // 2) 풀스크린 dim + 말풍선
            GameObject hintRoot = new GameObject("FirstAppearanceHint");
            hintRoot.transform.SetParent(canvas.transform, false);
            RectTransform rootRt = hintRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
            Image dim = hintRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.45f);
            Button dimBtn = hintRoot.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;

            // 말풍선 위치: 버튼 우측 옆 (좌상단 버튼이라 → 우측 표시)
            RectTransform btnRt = targetBtn.GetComponent<RectTransform>();
            Vector3 btnWorldPos = btnRt.position;
            // 캔버스 로컬 좌표 변환
            Camera uiCam = canvas.worldCamera;
            Vector2 screenPt = RectTransformUtility.WorldToScreenPoint(uiCam, btnWorldPos);
            Vector2 btnLocalPt;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRt, screenPt, uiCam, out btnLocalPt);

            // 말풍선 GameObject
            GameObject bubble = new GameObject("Bubble");
            bubble.transform.SetParent(hintRoot.transform, false);
            RectTransform bubbleRt = bubble.AddComponent<RectTransform>();
            bubbleRt.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleRt.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRt.pivot = new Vector2(0f, 0.5f); // 좌측 기준 (버튼 오른쪽에 펼쳐짐)
            bubbleRt.anchoredPosition = btnLocalPt + new Vector2(60f, 0f); // 버튼 우측 60px
            bubbleRt.sizeDelta = new Vector2(320f, 130f);
            Image bubbleBg = bubble.AddComponent<Image>();
            bubbleBg.color = new Color(0.10f, 0.13f, 0.20f, 0.97f);
            Outline bubbleOl = bubble.AddComponent<Outline>();
            bubbleOl.effectColor = new Color(1f, 0.85f, 0.4f, 0.9f);
            bubbleOl.effectDistance = new Vector2(2f, -2f);
            // 말풍선 클릭으로 닫기
            Button bubbleBtn = bubble.AddComponent<Button>();
            bubbleBtn.transition = Selectable.Transition.None;

            // 제목
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(bubble.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -10f);
            titleRt.sizeDelta = new Vector2(-20f, 32f);
            Text titleTxt = titleObj.AddComponent<Text>();
            titleTxt.font = font;
            titleTxt.fontSize = 20;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color = new Color(1f, 0.92f, 0.55f, 1f);
            titleTxt.text = title;
            titleTxt.raycastTarget = false;

            // 본문
            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(bubble.transform, false);
            RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f); bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(15f, 12f);
            bodyRt.offsetMax = new Vector2(-15f, -42f);
            Text bodyTxt = bodyObj.AddComponent<Text>();
            bodyTxt.font = font;
            bodyTxt.fontSize = 16;
            bodyTxt.alignment = TextAnchor.UpperCenter;
            bodyTxt.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            bodyTxt.text = body;
            bodyTxt.raycastTarget = false;
            bodyTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyTxt.verticalOverflow = VerticalWrapMode.Overflow;

            // 닫기 안내 (작은 텍스트)
            GameObject hintFootObj = new GameObject("Foot");
            hintFootObj.transform.SetParent(bubble.transform, false);
            RectTransform footRt = hintFootObj.AddComponent<RectTransform>();
            footRt.anchorMin = new Vector2(0f, 0f); footRt.anchorMax = new Vector2(1f, 0f);
            footRt.pivot = new Vector2(0.5f, 0f);
            footRt.anchoredPosition = new Vector2(0f, 5f);
            footRt.sizeDelta = new Vector2(-20f, 16f);
            Text footTxt = hintFootObj.AddComponent<Text>();
            footTxt.font = font;
            footTxt.fontSize = 12;
            footTxt.alignment = TextAnchor.MiddleCenter;
            footTxt.color = new Color(0.7f, 0.85f, 1f, 0.85f);
            footTxt.text = "탭하여 닫기";
            footTxt.raycastTarget = false;

            // 닫기 콜백
            System.Action close = () =>
            {
                PlayerPrefs.SetInt(hintShownKey, 1);
                PlayerPrefs.Save();
                if (hintRoot != null) Destroy(hintRoot);
                StopHintPulse(targetBtn);
            };
            dimBtn.onClick.AddListener(() => close());
            bubbleBtn.onClick.AddListener(() => close());

            hintRoot.transform.SetAsLastSibling();
        }

        /// <summary>활성 펄스 대상 버튼 — 코루틴 내부 자가 체크로 중단</summary>
        private System.Collections.Generic.HashSet<GameObject> activePulseTargets
            = new System.Collections.Generic.HashSet<GameObject>();

        private void StartHintPulse(GameObject btnObj)
        {
            if (btnObj == null || activePulseTargets.Contains(btnObj)) return;
            activePulseTargets.Add(btnObj);
            StartCoroutine(PulseHintGlowCoroutine(btnObj));
        }

        /// <summary>버튼 펄스 글로우 (스케일 1.0 ↔ 1.15) — 활성 셋에서 제거되면 자동 종료</summary>
        private System.Collections.IEnumerator PulseHintGlowCoroutine(GameObject btnObj)
        {
            RectTransform rt = btnObj != null ? btnObj.GetComponent<RectTransform>() : null;
            if (rt == null) yield break;
            Vector3 baseScale = Vector3.one;

            float t = 0f;
            while (btnObj != null && btnObj.activeSelf && activePulseTargets.Contains(btnObj))
            {
                t += Time.unscaledDeltaTime * 4f; // 주기 ~1.6초
                float pulse = (Mathf.Sin(t) * 0.5f + 0.5f) * 0.15f + 1.0f; // 1.0 ~ 1.15
                rt.localScale = baseScale * pulse;
                yield return null;
            }
            if (rt != null) rt.localScale = baseScale;
        }

        /// <summary>지정 버튼의 펄스 애니메이션 중단 + 스케일 복원</summary>
        private void StopHintPulse(GameObject btnObj)
        {
            if (btnObj == null) return;
            activePulseTargets.Remove(btnObj);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.one;
        }

        private void CreateTutorialResetButton(GameObject parent, Font font)
        {
            // 버튼 컨테이너
            GameObject btnObj = new GameObject("TutorialResetButton");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            btnRt.anchoredPosition = new Vector2(20f, 80f); // v3: 하단 내비 위로
            btnRt.sizeDelta = new Vector2(180f, 45f);

            // 배경
            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.25f, 0.2f, 0.35f, 0.85f);

            // 아웃라인
            Outline btnOutline = btnObj.AddComponent<Outline>();
            btnOutline.effectColor = new Color(0.6f, 0.5f, 0.8f, 0.6f);
            btnOutline.effectDistance = new Vector2(1f, 1f);

            // 버튼 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.7f, 1f);
            colors.pressedColor = new Color(0.7f, 0.6f, 0.5f, 1f);
            btn.colors = colors;

            // 텍스트
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 4f);
            textRt.offsetMax = new Vector2(-8f, -4f);
            Text btnText = textObj.AddComponent<Text>();
            btnText.font = font;
            btnText.fontSize = 16;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = new Color(0.85f, 0.8f, 0.95f, 1f);
            btnText.raycastTarget = false;
            btnText.text = "튜토리얼 초기화";

            // 클릭 이벤트
            btn.onClick.AddListener(() =>
            {
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.ResetAllTutorials();

                    // 피드백: 텍스트 변경 후 복원
                    btnText.text = "✓ 초기화 완료!";
                    btnText.color = new Color(0.4f, 1f, 0.5f, 1f);
                    StartCoroutine(ResetButtonFeedback(btnText));
                }
            });
        }

        /// <summary>
        /// 튜토리얼 초기화 버튼 피드백 코루틴
        /// </summary>
        private IEnumerator ResetButtonFeedback(Text btnText)
        {
            yield return new WaitForSeconds(1.5f);
            if (btnText != null)
            {
                btnText.text = "튜토리얼 초기화";
                btnText.color = new Color(0.85f, 0.8f, 0.95f, 1f);
            }
        }

        /// <summary>
        /// 레벨 해금/잠금 토글 버튼 생성 (튜토리얼 초기화 버튼 오른쪽)
        /// </summary>
        private void CreateUnlockAllButton(GameObject parent, Font font)
        {
            // 버튼 컨테이너
            GameObject btnObj = new GameObject("UnlockAllButton");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            btnRt.anchoredPosition = new Vector2(210f, 80f); // v3: 하단 내비 위로
            btnRt.sizeDelta = new Vector2(180f, 45f);

            // 배경
            Image btnBg = btnObj.AddComponent<Image>();

            // 아웃라인
            Outline btnOutline = btnObj.AddComponent<Outline>();
            btnOutline.effectDistance = new Vector2(1f, 1f);

            // 버튼 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.7f, 0.9f, 1f, 1f);
            colors.pressedColor = new Color(0.5f, 0.6f, 0.7f, 1f);
            btn.colors = colors;

            // 텍스트
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 4f);
            textRt.offsetMax = new Vector2(-8f, -4f);
            Text btnText = textObj.AddComponent<Text>();
            btnText.font = font;
            btnText.fontSize = 16;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.raycastTarget = false;

            // 현재 상태에 따라 초기 비주얼 설정
            UpdateUnlockToggleVisual(btnBg, btnOutline, btnText);

            // 클릭 이벤트 — 토글
            btn.onClick.AddListener(() =>
            {
                if (LevelRegistry.AreAllLevelsUnlocked())
                {
                    // 현재 모두해금 → 모두잠금
                    LevelRegistry.LockAllLevels();
                    btnText.text = "잠금 완료!";
                    btnText.color = new Color(1f, 0.6f, 0.4f, 1f);
                }
                else
                {
                    // 현재 잠김 → 모두해금
                    LevelRegistry.UnlockAllLevels();
                    btnText.text = "해금 완료!";
                    btnText.color = new Color(0.4f, 1f, 0.5f, 1f);
                }

                // ★ 기능 해금 상태 동기화 (notifiedFeatures 리셋 후 재빌드)
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.ResetNotifiedFeatures();
                    TutorialManager.Instance.SyncFeatureUnlocks();
                }

                // 로비 UI 재생성
                StartCoroutine(UnlockToggleButtonFeedback(btnBg, btnOutline, btnText));
            });
        }

        /// <summary>
        /// 해금/잠금 토글 버튼 비주얼 업데이트
        /// </summary>
        private void UpdateUnlockToggleVisual(Image bg, Outline outline, Text text)
        {
            if (LevelRegistry.AreAllLevelsUnlocked())
            {
                // 모두 해금 상태 → "모두잠금" 표시 (빨간 톤)
                bg.color = new Color(0.45f, 0.2f, 0.2f, 0.85f);
                outline.effectColor = new Color(0.9f, 0.4f, 0.4f, 0.6f);
                text.text = "모두잠금";
                text.color = new Color(1f, 0.8f, 0.8f, 1f);
            }
            else
            {
                // 잠김 상태 → "모두해금" 표시 (파란 톤)
                bg.color = new Color(0.2f, 0.3f, 0.45f, 0.85f);
                outline.effectColor = new Color(0.4f, 0.6f, 0.9f, 0.6f);
                text.text = "모두해금";
                text.color = new Color(0.8f, 0.9f, 1f, 1f);
            }
        }

        /// <summary>
        /// 해금/잠금 토글 버튼 피드백 코루틴 — 로비 UI 재생성 + 스크롤
        /// </summary>
        private IEnumerator UnlockToggleButtonFeedback(Image bg, Outline outline, Text btnText)
        {
            yield return new WaitForSeconds(0.8f);

            // 로비 UI 재생성으로 잠금 비주얼 갱신
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null && lobbyContainer != null)
            {
                Destroy(lobbyContainer);
                lobbyContainer = null;
                yield return null;
                CreateLobbyUI(canvas);
                ShowLobby();
            }
        }

        /// <summary>
        /// 전체 데이터 초기화 버튼 생성 (모두해금 버튼 오른쪽)
        /// 처음 게임을 시작하는 것처럼 모든 저장 데이터를 삭제
        /// </summary>
        private void CreateResetAllDataButton(GameObject parent, Font font)
        {
            // 버튼 컨테이너
            GameObject btnObj = new GameObject("ResetAllDataButton");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            btnRt.anchoredPosition = new Vector2(400f, 80f); // 모두해금 버튼(210) 오른쪽, v3 내비 위로
            btnRt.sizeDelta = new Vector2(140f, 45f);

            // 배경 (빨간 톤 — 위험한 작업 강조)
            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.45f, 0.15f, 0.15f, 0.85f);

            // 아웃라인
            Outline btnOutline = btnObj.AddComponent<Outline>();
            btnOutline.effectColor = new Color(1f, 0.3f, 0.3f, 0.6f);
            btnOutline.effectDistance = new Vector2(1f, 1f);

            // 버튼 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.8f, 0.8f, 1f);
            colors.pressedColor = new Color(0.7f, 0.5f, 0.5f, 1f);
            btn.colors = colors;

            // 텍스트
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(4f, 4f);
            textRt.offsetMax = new Vector2(-4f, -4f);
            Text btnText = textObj.AddComponent<Text>();
            btnText.font = font;
            btnText.fontSize = 16;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = new Color(1f, 0.85f, 0.85f, 1f);
            btnText.raycastTarget = false;
            btnText.text = "초기화";

            // 클릭 이벤트 — 전체 초기화 (씬 재로드로 완전 초기화)
            // ★ 2단계 확인: 첫 탭은 무장(라벨 변경), 3초 내 두 번째 탭에서만 실행 (오탭으로 전 진행도 삭제 방지)
            float armedTime = -999f;
            btn.onClick.AddListener(() =>
            {
                if (Time.unscaledTime - armedTime <= 3f)
                {
                    ResetAllGameData();
                }
                else
                {
                    armedTime = Time.unscaledTime;
                    btnText.text = "정말 초기화?";
                    StartCoroutine(ResetArmTimeout());
                    System.Collections.IEnumerator ResetArmTimeout()
                    {
                        yield return new WaitForSecondsRealtime(3f);
                        if (btnText != null) btnText.text = "초기화";
                    }
                }
            });
        }

        /// <summary>
        /// 모든 게임 데이터 초기화 — 처음 시작 상태로 복원
        /// PlayerPrefs 전체 삭제 + 씬 재로드로 모든 매니저 인메모리 상태 완전 초기화
        /// </summary>
        private void ResetAllGameData()
        {
            Debug.Log("[GameManager] ★★★ 전체 데이터 초기화 시작 ★★★");

            // PlayerPrefs 전체 삭제 (레벨해금, 점수, 골드, 스킬, 아이템, 튜토리얼, 업적 등 모두)
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[GameManager] PlayerPrefs 전체 삭제 완료");

            // LevelRegistry static 캐시 무효화 (씬 재로드 시 빈 PlayerPrefs에서 다시 로드)
            LevelRegistry.ForceReinitialize();

            // 씬 재로드 — 모든 매니저가 Awake()/Start()에서 빈 PlayerPrefs를 읽어 초기 상태로 복원
            Debug.Log("[GameManager] ★★★ 씬 재로드로 완전 초기화 ★★★");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        // ============================================================
        // 레벨 활성화 버튼 (특정 레벨까지만 해금하는 에디터 도구)
        // ============================================================

        /// <summary>
        /// 레벨 활성화 버튼 생성 (초기화 버튼 오른쪽)
        /// 클릭 시 활성화 모드 진입 → 레벨 버튼 클릭으로 해당 레벨까지 해금
        /// </summary>
        private void CreateLevelActivationButton(GameObject parent, Font font)
        {
            GameObject btnObj = new GameObject("LevelActivationButton");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            btnRt.anchoredPosition = new Vector2(550f, 80f); // 초기화(400) 오른쪽, v3 내비 위로
            btnRt.sizeDelta = new Vector2(150f, 45f);

            // 배경 (초록 톤 — 안전한 작업)
            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.15f, 0.35f, 0.2f, 0.85f);

            // 아웃라인
            Outline btnOutline = btnObj.AddComponent<Outline>();
            btnOutline.effectColor = new Color(0.3f, 0.7f, 0.4f, 0.6f);
            btnOutline.effectDistance = new Vector2(1f, 1f);

            // 버튼 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.8f, 1f, 0.8f, 1f);
            colors.pressedColor = new Color(0.5f, 0.7f, 0.5f, 1f);
            btn.colors = colors;

            // 텍스트
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(4f, 4f);
            textRt.offsetMax = new Vector2(-4f, -4f);
            Text btnText = textObj.AddComponent<Text>();
            btnText.font = font;
            btnText.fontSize = 14;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.color = new Color(0.8f, 1f, 0.85f, 1f);
            btnText.raycastTarget = false;
            btnText.text = "레벨활성화";

            // 클릭 이벤트 — 활성화 모드 토글
            btn.onClick.AddListener(() =>
            {
                isLevelActivationMode = !isLevelActivationMode;

                if (isLevelActivationMode)
                {
                    // 활성화 모드 ON: 잠긴 레벨도 클릭 가능하게
                    btnBg.color = new Color(0.1f, 0.55f, 0.2f, 0.95f);
                    btnText.text = "레벨 선택...";
                    btnText.color = new Color(0.3f, 1f, 0.4f, 1f);
                    btnOutline.effectColor = new Color(0.3f, 1f, 0.4f, 0.8f);

                    if (stageScrollBuilder != null)
                        stageScrollBuilder.ForceAllButtonsInteractable(true);

                    Debug.Log("[GameManager] 레벨 활성화 모드 ON — 레벨 버튼을 클릭하세요");
                }
                else
                {
                    // 활성화 모드 OFF: 원래 잠금 상태 복원
                    btnBg.color = new Color(0.15f, 0.35f, 0.2f, 0.85f);
                    btnText.text = "레벨활성화";
                    btnText.color = new Color(0.8f, 1f, 0.85f, 1f);
                    btnOutline.effectColor = new Color(0.3f, 0.7f, 0.4f, 0.6f);

                    if (stageScrollBuilder != null)
                        stageScrollBuilder.ForceAllButtonsInteractable(false);

                    Debug.Log("[GameManager] 레벨 활성화 모드 OFF");
                }
            });
        }

        /// <summary>
        /// 레벨 활성화 후 로비 UI 재생성 (잠금/해금 비주얼 갱신)
        /// </summary>
        private IEnumerator RefreshLobbyAfterLevelActivation(int activatedLevel)
        {
            yield return new WaitForSeconds(0.3f);

            // 로비 UI 재생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null && lobbyContainer != null)
            {
                Destroy(lobbyContainer);
                lobbyContainer = null;
                yield return null;
                CreateLobbyUI(canvas);
                ShowLobby();
            }

            Debug.Log($"[GameManager] 레벨 1~{activatedLevel} 활성화 완료, 로비 갱신");
        }

        /// <summary>
        /// 로비 화면 표시
        /// </summary>
        private void ShowLobby()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;

            SetGameState(GameState.Lobby);

            // 인게임 플로팅 메시지 잔상 정리 (StopAllCoroutines로 코루틴은 멈추지만 GameObject는 남음)
            CleanupFloatingMessages();

            // 고블린 시스템 정리 (남아있는 몬스터 제거)
            if (goblinSystem != null)
                goblinSystem.CleanupAll();

            // 미션 UI 제거
            if (uiManager != null)
                uiManager.CleanupGameMissionUI();

            // 대기 미션 인디케이터 정리
            if (pendingIndicatorObj != null) Destroy(pendingIndicatorObj);
            pendingIndicatorObj = null;
            pendingIndicatorCountText = null;
            pendingIndicatorIcon = null;

            // 그리드 숨기기 (SetActive 대신 CanvasGroup으로 — 다른 시스템의 FindObjectOfType 유지)
            if (hexGrid != null)
                SetCanvasGroupVisible(hexGrid.gameObject, false);

            // HUD 숨기기
            foreach (var hud in hudElements)
            {
                if (hud != null) hud.SetActive(false);
            }

            // MP 게이지 숨기기
            if (mpGaugeUI != null)
                mpGaugeUI.gameObject.SetActive(false);

            // 스킬 트리 페이지 숨기기 (열려있었다면)
            if (skillTreeUI != null && skillTreeUI.IsVisible)
                skillTreeUI.Hide();

            // 아이템 오버레이 강제 비활성화
            GameObject hammerOverlay = GameObject.Find("HammerOverlay");
            if (hammerOverlay != null) hammerOverlay.SetActive(false);
            GameObject swapOverlay = GameObject.Find("SwapOverlay");
            if (swapOverlay != null) swapOverlay.SetActive(false);

            if (lobbyContainer != null)
            {
                lobbyContainer.SetActive(true);
                lobbyContainer.transform.SetAsLastSibling();
            }

            // ★ 조건부 로비 버튼 표시 + 첫 등장 힌트
            UpdateLobbyConditionalButtons();

            // SFX/BGM 버튼을 lobbyContainer 위로 올려 로비에서도 보이게 함
            if (sfxToggleBtnObj != null) sfxToggleBtnObj.transform.SetAsLastSibling();
            if (bgmToggleBtnObj != null) bgmToggleBtnObj.transform.SetAsLastSibling();

            // 로비 골드 표시 갱신 (인게임과 동일 형식: 숫자만) — 보상 반영 시 또렷한 카운트업
            if (lobbyGoldText != null)
                JewelsHexaPuzzle.Utils.NumberRoller.Roll(lobbyGoldText, currentGold, v => v.ToString(), 0.7f, 0.35f);

            // 로비 레벨 버튼 해금 상태 + 최고 점수 갱신 + 스크롤 위치 설정
            if (stageScrollBuilder != null)
            {
                stageScrollBuilder.RefreshUnlockStates();
                stageScrollBuilder.RefreshHighScores();
                stageScrollBuilder.ScrollToHighestUnlocked();
            }

            Debug.Log("[GameManager] 로비 표시");
        }

        /// <summary>
        /// 로비 화면 숨기기
        /// </summary>
        private void HideLobby()
        {
            if (lobbyContainer != null)
                lobbyContainer.SetActive(false);

            // 그리드 표시
            if (hexGrid != null)
                SetCanvasGroupVisible(hexGrid.gameObject, true);

            // HUD 표시
            foreach (var hud in hudElements)
            {
                if (hud != null) hud.SetActive(true);
            }

            // ★ 기능 미해금 아이템 버튼 숨기기 (HUD 일괄 표시 후 잠긴 버튼 재숨김)
            SyncItemButtonVisibility();

            // ★ 인게임 난이도/레벨 표시 갱신 (현재 선택된 스테이지 기준)
            {
                var levelData = LevelRegistry.GetLevel(selectedStage);
                DifficultyType diff = levelData != null ? levelData.difficultyType : DifficultyType.Easy;
                string diffLabel;
                Color diffColor;
                switch (diff)
                {
                    case DifficultyType.Easy:
                        diffLabel = "쉬움"; diffColor = new Color(0.3f, 0.9f, 0.3f); break;
                    case DifficultyType.Normal:
                        diffLabel = "보통"; diffColor = new Color(1f, 0.85f, 0.2f); break;
                    case DifficultyType.Hard: default:
                        diffLabel = "어려움"; diffColor = new Color(1f, 0.3f, 0.3f); break;
                }
                if (hudDifficultyText != null)
                {
                    hudDifficultyText.text = diffLabel;
                    hudDifficultyText.color = diffColor;
                }
                if (hudLevelNumText != null)
                    hudLevelNumText.text = $"LEVEL {selectedStage}";
            }

            // 아이템 오버레이 강제 비활성화 (아이템 미활성 상태에서 오버레이가 보이지 않도록)
            GameObject hammerOverlay = GameObject.Find("HammerOverlay");
            if (hammerOverlay != null) hammerOverlay.SetActive(false);
            GameObject swapOverlay = GameObject.Find("SwapOverlay");
            if (swapOverlay != null) swapOverlay.SetActive(false);

            Debug.Log("[GameManager] 로비 숨기기");
        }

        /// <summary>
        /// CanvasGroup으로 가시성 제어 (SetActive 대신 — FindObjectOfType 유지)
        /// </summary>
        private void SetCanvasGroupVisible(GameObject obj, bool visible)
        {
            if (obj == null) return;
            CanvasGroup cg = obj.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = obj.AddComponent<CanvasGroup>();

            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }

        /// <summary>
        /// 로비로 나가기
        /// </summary>
        public void ExitToLobby()
        {
            PlayLobbyTransition(() =>
            {
                Time.timeScale = 1f;
                ForceResetAllGameSystems();
                if (scoreManager != null) scoreManager.ResetScore();
                if (MPManager.Instance != null) MPManager.Instance.ResetMPSilent();
                if (MonsterSpawnController.Instance != null) MonsterSpawnController.Instance.Reset();
                if (SkillTreeManager.Instance != null) SkillTreeManager.Instance.ResetRunSkills(); // 런별: 로비 이탈 시 리워드 스킬 초기화
                JewelsHexaPuzzle.Data.GemTypeHelper.AllowedColorsOverride = null;
                JewelsHexaPuzzle.Data.GemTypeHelper.ActiveGemTypeCount = 5;
                JewelsHexaPuzzle.Data.MissionTargetColors.Clear(); // 미션 타겟 강조 잔존 방지
                if (Stage1AssistSystem.Instance != null) Stage1AssistSystem.Instance.Disable();
                ShowLobby();
                Debug.Log("[GameManager] ExitToLobby: 로비로 돌아갑니다 (전환 오버레이)");
            });
        }

        /// <summary>
        /// 클리어 후 로비로 돌아가기 (스코어 리셋 없음)
        /// </summary>
        public void ReturnToLobby()
        {
            PlayLobbyTransition(() =>
            {
                Time.timeScale = 1f;
                ForceResetAllGameSystems();
                if (scoreManager != null) scoreManager.ResetScore();
                if (MPManager.Instance != null) MPManager.Instance.ResetMPSilent();
                if (MonsterSpawnController.Instance != null) MonsterSpawnController.Instance.Reset();
                if (SkillTreeManager.Instance != null) SkillTreeManager.Instance.ResetRunSkills(); // 런별: 로비 이탈 시 리워드 스킬 초기화
                JewelsHexaPuzzle.Data.GemTypeHelper.AllowedColorsOverride = null;
                JewelsHexaPuzzle.Data.GemTypeHelper.ActiveGemTypeCount = 5;
                JewelsHexaPuzzle.Data.MissionTargetColors.Clear(); // 미션 타겟 강조 잔존 방지
                if (Stage1AssistSystem.Instance != null) Stage1AssistSystem.Instance.Disable();
                ShowLobby();
                Debug.Log("[GameManager] ReturnToLobby: 로비로 돌아갑니다 (전환 오버레이)");
            });
        }

        /// <summary>
        /// 풀스크린 엘프공주 이미지 트랜지션으로 로비/인게임 전환 작업을 감싼다.
        /// 페이드 인 → 이미지 2초 노출 → workInsideFade 실행 → 페이드 아웃.
        /// 오버레이가 미초기화 상태이면 폴백으로 작업만 즉시 실행 (안전망 — 나가기 버튼이 막히지 않음).
        /// </summary>
        private void PlayLobbyTransition(System.Action workInsideFade)
        {
            var lto = JewelsHexaPuzzle.UI.LobbyTransitionOverlay.Instance;
            if (lto != null)
                lto.PlayTransitionWithImage(workInsideFade);
            else
            {
                Debug.LogWarning("[GameManager] LobbyTransitionOverlay 미초기화 — 폴백으로 즉시 실행");
                workInsideFade?.Invoke();
            }
        }

        /// <summary>
        /// 게임 나가기/재시작 시 모든 게임 시스템의 코루틴과 상태를 강제 초기화합니다.
        /// 드릴 투사체, 폭탄 이펙트 등 진행 중인 코루틴이 다음 게임에 영향주지 않도록 정리.
        /// </summary>
        private void ForceResetAllGameSystems()
        {
            // 1. GameManager 자체 코루틴 중지 + 상태 플래그 리셋
            StopAllCoroutines();
            isProcessingChainDrill = false;
            isInPostRecovery = false;
            processingStartTime = 0f;
            lastAftermathProgressTime = 0f;

            // 2. 블록 제거/낙하 시스템
            if (blockRemovalSystem != null)
                blockRemovalSystem.ForceReset();

            // 3. 모든 특수 블록 시스템
            if (drillSystem != null) drillSystem.ForceReset();
            if (bombSystem != null) bombSystem.ForceReset();
            if (donutSystem != null) donutSystem.ForceReset();
            if (xBlockSystem != null) xBlockSystem.ForceReset();
            if (droneSystem != null) droneSystem.ForceReset();

            // 4. 합성 시스템
            var comboSystem = FindObjectOfType<SpecialBlockComboSystem>();
            if (comboSystem != null)
                comboSystem.StopAllCoroutines();

            // 5. 아이템 시스템
            var lineItem = FindObjectOfType<JewelsHexaPuzzle.Items.LineDrawItem>();
            if (lineItem != null && lineItem.IsActive)
                lineItem.StopAllCoroutines();

            // 6. 튜토리얼 상태 완전 초기화 (완료 마킹 없음 — 재진입 시 반복)
            // 로비 이동 시 남아있던 대화/말풍선/스포트라이트/블록 글로우 등 제거
            if (TutorialManager.Instance != null)
                TutorialManager.Instance.AbortTutorial();

            // 7. 고블린 시스템 강제 정리 — 코루틴 중단 + 비주얼/이펙트/orphan 제거
            //    (ShowLobby에서도 호출되지만, 더 일찍 정리해 다음 시스템 동작 시 잔재 차단)
            if (goblinSystem != null)
                goblinSystem.CleanupAll();

            Debug.Log("[GameManager] ForceResetAllGameSystems: 모든 게임 시스템 초기화 완료");
        }

// ============================================================
        // EnemySystem 통합 적군 스폰
        // ============================================================

        /// <summary>
        /// EnemySystem을 통한 적군 스폰 후 Playing 전환
        /// </summary>
        private IEnumerator SpawnEnemiesViaSystemAndPlay()
        {
            processingStartTime = Time.time; // STUCK 방지: 적군 스폰 대기 중 타임아웃 방지

            // 고블린 턴 처리 (이동 → 공격 → 소환)
            if (goblinSystem != null && goblinSystem.IsActive)
            {
                yield return StartCoroutine(goblinSystem.ProcessTurn());
                processingStartTime = Time.time; // STUCK 방지 갱신

                // 고블린 턴 후 미션 완료 확인
                if (stageManager != null && stageManager.IsMissionComplete())
                {
                    StageClear();
                    yield break;
                }
            }

            if (enemySystem != null)
            {
                // [몬스터 비활성화] 적군 스폰 사운드 비활성화
                // if (AudioManager.Instance != null)
                //     AudioManager.Instance.PlayEnemySpawnSound();
                yield return StartCoroutine(enemySystem.SpawnEnemiesForStage(selectedStage, 3, rotationCount));
            }

            // ★ 회전 매칭 데드락: 회전으로 매칭 불가 시 → 3+ 같은 색이면 재배치, 아니면 게임오버 (사용자 요청)
            if (matchingSystem != null && !matchingSystem.HasPossibleMoves())
            {
                if (HandleRotationDeadlock()) yield break;   // 게임오버면 중단
            }

            SetGameState(GameState.Playing);

            // ★ 스킬 업그레이드 선택: 몬스터 이동이 모두 끝나고 플레이어 턴이 돌아온 지금 표시
            if (SkillUpgradeOfferSystem.Instance != null)
                SkillUpgradeOfferSystem.Instance.ShowPendingUpgradeIfAny();
        }

        // ============================================================
        // 회전 매칭 데드락 처리 (사용자 요청 2026-06):
        //   회전으로 매칭 불가(HasPossibleMoves=false) → 같은 색 3+ 있으면 매칭 가능 배치로 재배치,
        //   3+ 같은 색이 없으면 게임오버.
        // ============================================================

        /// <summary>재배치 대상(자유 교환 가능) 블록 — 색을 가진 일반/금간 블록. 특수/고정/쉘/체인/흙더미/고블린 점유 제외.</summary>
        private bool IsRearrangeableBlock(HexBlock b)
        {
            if (b == null || b.Data == null) return false;
            var gt = b.Data.gemType;
            if (gt == GemType.None || gt == GemType.Gray) return false;
            if (b.Data.specialType != SpecialBlockType.None) return false; // 특수/고정 블록
            if (b.Data.isShell) return false;                              // 색 빼앗긴 쉘(매칭 불가)
            if (b.Data.hasChain) return false;
            if (b.Data.dirtMound > 0) return false;
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.HasGoblinAt(b.Coord)) return false;
            return true;
        }

        /// <summary>매칭 가능한(재배치 대상) 블록 중 같은 색이 n개 이상 존재하는지.</summary>
        private bool BoardHasColorWithAtLeast(int n)
        {
            if (hexGrid == null) return false;
            var counts = new Dictionary<GemType, int>();
            foreach (var b in hexGrid.GetAllBlocks())
            {
                if (!IsRearrangeableBlock(b)) continue;
                var gt = b.Data.gemType;
                int c = counts.TryGetValue(gt, out var prev) ? prev + 1 : 1;
                counts[gt] = c;
                if (c >= n) return true;
            }
            return false;
        }

        /// <summary>
        /// 재배치 대상 블록들의 데이터를 무작위로 섞어 "즉시 매칭 없음 + 회전 매칭 가능" 배치를 만든다.
        /// 성공 시 재배치된 블록 리스트 반환(비주얼 갱신 완료), 실패 시 원복 후 null.
        /// </summary>
        private List<HexBlock> ReshuffleBoardToSolvable()
        {
            if (hexGrid == null || matchingSystem == null) return null;
            var cells = new List<HexBlock>();
            foreach (var b in hexGrid.GetAllBlocks())
                if (IsRearrangeableBlock(b)) cells.Add(b);
            if (cells.Count < 3) return null;

            var cellSet = new HashSet<HexBlock>(cells);
            var original = new BlockData[cells.Count];
            for (int i = 0; i < cells.Count; i++) original[i] = cells[i].Data;
            var datas = new List<BlockData>(original);

            // 무작위 셔플은 5~6색 밀집 보드에서 즉시 매칭(삼각형)을 거의 항상 만든다(평균 ~12개).
            //   → 셔플 후 "매칭에 포함된 재배치 블록"을 다른 셀과 교환해 즉시 매칭을 제거(repair)한다.
            //   데드락 보드엔 매칭이 없었고 특수 블록은 안 움직이므로, 셔플로 생긴 매칭은 항상 재배치 블록을
            //   포함 → repair로 제거 가능. 깨끗 + 회전 매칭 가능이면 채택.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                ShuffleInPlace(datas);
                for (int i = 0; i < cells.Count; i++) cells[i].SetBlockDataSilent(datas[i]);
                RepairImmediateMatches(cells, cellSet);

                if (!matchingSystem.HasAnyMatchQuick() && matchingSystem.HasPossibleMoves())
                {
                    foreach (var c in cells) c.UpdateVisuals();
                    return cells;
                }
            }
            // 폴백: 깨끗한 배치를 못 만들어도 회전/즉시 매칭이 가능하면 수용 (게임오버보다 재배치 우선)
            if (matchingSystem.HasPossibleMoves())
            {
                foreach (var c in cells) c.UpdateVisuals();
                return cells;
            }
            // 실패(극히 드묾) → 원복
            for (int i = 0; i < cells.Count; i++) cells[i].SetBlockDataSilent(original[i]);
            foreach (var c in cells) c.UpdateVisuals();
            return null;
        }

        /// <summary>즉시 매칭(삼각형) 제거 — 매칭에 포함된 재배치 블록을 무작위 다른 재배치 셀과 데이터 교환, 매칭이 없어질 때까지(제한).</summary>
        private void RepairImmediateMatches(List<HexBlock> cells, HashSet<HexBlock> cellSet)
        {
            for (int iter = 0; iter < 200; iter++)
            {
                if (!matchingSystem.HasAnyMatchQuick()) return;   // 깨끗
                var matches = matchingSystem.FindMatches();
                if (matches == null || matches.Count == 0) return;
                HexBlock a = null;
                foreach (var grp in matches)
                {
                    foreach (var bl in grp.blocks)
                        if (cellSet.Contains(bl)) { a = bl; break; }
                    if (a != null) break;
                }
                if (a == null) return;   // 매칭이 전부 비재배치(특수) 블록 → 교환 불가 (이론상 거의 없음)
                HexBlock b = cells[Random.Range(0, cells.Count)];
                if (a == b) continue;
                var tmp = a.Data; a.SetBlockDataSilent(b.Data); b.SetBlockDataSilent(tmp);
            }
        }

        private void ShuffleInPlace(List<BlockData> list)
        {
            for (int i = list.Count - 1; i > 0; i--)   // Fisher-Yates
            {
                int j = Random.Range(0, i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        /// <summary>회전 데드락 처리 — 3+ 같은 색이면 재배치, 아니면 게임오버. true 반환 시 게임오버(호출측 중단).</summary>
        private bool HandleRotationDeadlock()
        {
            if (BoardHasColorWithAtLeast(3))
            {
                var cells = ReshuffleBoardToSolvable();
                if (cells != null)
                {
                    Debug.Log($"[GameManager] 회전 매칭 데드락 → 보드 재배치 완료 ({cells.Count}블록)");
                    StartCoroutine(ReshufflePulse(cells));
                    return false;
                }
            }
            Debug.Log("[GameManager] 회전 매칭 데드락 → 3+ 같은 색 없음/재배치 실패 → 게임오버");
            GameOver(GameOverReason.MatchingDeadlock);
            return true;
        }

        /// <summary>재배치 시각 피드백 — 재배치된 블록 스케일 1→1.18→1 펄스(0.32초) + 안내 토스트.</summary>
        private IEnumerator ReshufflePulse(List<HexBlock> cells)
        {
            // ★ 재배치 안내 토스트 (데드락 해소 — 매칭 가능한 배치로 보드를 섞었음을 알림)
            if (uiManager != null)
                uiManager.ShowToast("<color=#FFD060><b>매칭이 막혀 보드를 재배치했어요!</b></color>");

            float dur = 0.32f, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(t / dur * Mathf.PI);   // 0→1→0
                float scale = 1f + 0.18f * k;
                foreach (var c in cells)
                    if (c != null) c.transform.localScale = Vector3.one * scale;
                yield return null;
            }
            foreach (var c in cells)
                if (c != null) c.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// EnemySystem을 통한 적군 스폰 후 매칭 가능 여부 체크
        /// </summary>
        private IEnumerator SpawnEnemiesViaSystemAndCheckMoves(int count)
        {
            processingStartTime = Time.time; // STUCK 방지: 적군 스폰 대기 중 타임아웃 방지
            if (enemySystem != null)
            {
                // [몬스터 비활성화] 적군 스폰 사운드 비활성화
                // if (AudioManager.Instance != null)
                //     AudioManager.Instance.PlayEnemySpawnSound();
                yield return StartCoroutine(enemySystem.SpawnEnemiesForStage(selectedStage, count, rotationCount));
            }

            if (matchingSystem != null && !matchingSystem.HasPossibleMoves())
            {
                // ★ 3+ 같은 색이면 매칭 가능 배치로 재배치 (사용자 요청)
                if (BoardHasColorWithAtLeast(3))
                {
                    var cells = ReshuffleBoardToSolvable();
                    if (cells != null)
                    {
                        Debug.Log($"[GameManager] 무한모드 회전 데드락 → 보드 재배치 ({cells.Count}블록)");
                        StartCoroutine(ReshufflePulse(cells));
                        SetGameState(GameState.Playing);
                        yield break;
                    }
                }
                if (HasActivatableSpecialBlocks())
                {
                    Debug.Log("[GameManager] EnemySystem 스폰 후: 매칭 불가 but 특수 블록 있음 → Playing");
                    SetGameState(GameState.Playing);
                    yield break;
                }
                Debug.Log("[GameManager] EnemySystem 스폰 후: 매칭 불가 → 게임오버");
                GameOver(GameOverReason.MatchingDeadlock);
                yield break;
            }

            SetGameState(GameState.Playing);
        }

        /// <summary>
        /// SpawnEnemiesViaSystemAndPlay의 안전한 래퍼 — 예외 발생 시에도 Playing 전환 보장
        /// </summary>
        private IEnumerator SafeSpawnEnemiesAndPlay()
        {
            bool stateRestored = false;
            try
            {
                yield return StartCoroutine(SpawnEnemiesViaSystemAndPlay());
                stateRestored = true;
            }
            finally
            {
                if (!stateRestored && currentState == GameState.Processing)
                {
                    Debug.LogError("[GameManager] SafeSpawnEnemiesAndPlay: 예외 발생! Playing으로 강제 복귀.");
                    SetGameState(GameState.Playing);
                }
            }
        }

        /// <summary>
        /// SpawnEnemiesViaSystemAndCheckMoves의 안전한 래퍼 — 예외 발생 시에도 Playing 전환 보장
        /// </summary>
        private IEnumerator SafeSpawnEnemiesAndCheckMoves(int count)
        {
            bool stateRestored = false;
            try
            {
                yield return StartCoroutine(SpawnEnemiesViaSystemAndCheckMoves(count));
                stateRestored = true;
            }
            finally
            {
                if (!stateRestored && currentState == GameState.Processing)
                {
                    Debug.LogError("[GameManager] SafeSpawnEnemiesAndCheckMoves: 예외 발생! Playing으로 강제 복귀.");
                    SetGameState(GameState.Playing);
                }
            }
        }

        // ============================================================
        // 적군(회색 블록) 생성 시스템 (레거시 — 하위호환)
        // ============================================================

        /// <summary>
        /// 적군 생성 후 Playing 상태로 전환
        /// </summary>
        private IEnumerator SpawnEnemiesAndPlay()
        {
            yield return StartCoroutine(SpawnEnemyBlocks(3));
            SetGameState(GameState.Playing);
        }

        /// <summary>
        /// 무한모드: 적군 생성 후 매칭 가능 여부 체크
        /// 매칭 불가 + 특수 블록 없음 → 게임오버
        /// 매칭 불가 + 특수 블록 있음 → Playing (특수 블록만 사용 가능)
        /// </summary>
        private IEnumerator SpawnEnemiesAndCheckMoves(int grayCount)
        {
            yield return StartCoroutine(SpawnEnemyBlocks(grayCount));

            // 매칭 가능 여부 체크
            if (matchingSystem != null && !matchingSystem.HasPossibleMoves())
            {
                // 특수 블록이 남아있으면 클릭으로 사용 가능
                if (HasActivatableSpecialBlocks())
                {
                    Debug.Log("[GameManager] 무한모드: 매칭 불가능하지만 특수 블록 사용 가능 → Playing 유지");
                    SetGameState(GameState.Playing);
                    yield break;
                }

                Debug.Log("[GameManager] 무한모드: 매칭 불가능 + 특수 블록 없음 → 게임오버");
                GameOver(GameOverReason.MatchingDeadlock);
                yield break;
            }

            SetGameState(GameState.Playing);
        }

        /// <summary>
        /// 보드에 클릭으로 활성화 가능한 특수 블록이 있는지 확인
        /// </summary>
        private bool HasActivatableSpecialBlocks()
        {
            if (hexGrid == null) return false;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                var st = block.Data.specialType;
                if (st == SpecialBlockType.Drill || st == SpecialBlockType.Bomb ||
                    st == SpecialBlockType.Rainbow || st == SpecialBlockType.XBlock ||
                    st == SpecialBlockType.Drone)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 랜덤 일반 블록을 회색(적군)으로 전환
        /// </summary>
        private IEnumerator SpawnEnemyBlocks(int count)
        {
            if (hexGrid == null) yield break;

            // 1차 후보: 일반 블록 (None, Gray 제외, 특수 블록 제외)
            List<HexBlock> candidates = new List<HexBlock>();
            List<HexBlock> specialCandidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue;

                if (block.Data.specialType == SpecialBlockType.None)
                    candidates.Add(block);
                else
                    specialCandidates.Add(block);
            }

            // 일반 블록이 부족하면 특수 블록도 후보에 추가
            if (candidates.Count < count && specialCandidates.Count > 0)
            {
                candidates.AddRange(specialCandidates);
                Debug.Log($"[GameManager] 일반 블록 부족 → 특수 블록 {specialCandidates.Count}개 후보 추가");
            }

            if (candidates.Count == 0) yield break;

            // 랜덤 선택 (후보가 count보다 적으면 가능한 만큼만)
            int spawnCount = Mathf.Min(count, candidates.Count);
            List<HexBlock> selected = new List<HexBlock>();
            for (int i = 0; i < spawnCount; i++)
            {
                int idx = Random.Range(0, candidates.Count);
                selected.Add(candidates[idx]);
                candidates.RemoveAt(idx);
            }

            // 적군 스폰 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayEnemySpawnSound();

            // 시차를 두고 전환 애니메이션 시작
            List<Coroutine> animations = new List<Coroutine>();
            for (int i = 0; i < selected.Count; i++)
            {
                animations.Add(StartCoroutine(AnimateGrayConversion(selected[i], i * 0.15f)));
            }

            // 모든 애니메이션 완료 대기
            foreach (var co in animations)
                yield return co;
        }

        /// <summary>
        /// 블록을 회색(적군)으로 전환하는 애니메이션
        /// </summary>
        private IEnumerator AnimateGrayConversion(HexBlock block, float delay)
        {
            if (block == null || block.Data == null) yield break;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (block == null || block.Data == null) yield break;

            // 원래 색상 저장
            Color originalColor = GemColors.GetColor(block.Data.gemType);
            Color grayColor = GemColors.GetColor(GemType.Gray);

            // 데이터를 Gray로 변경 (기본 생성자 사용, Gray 필터 우회)
            // enemyType = Chromophage 설정 → SetBlockData의 Gray 안전장치 통과
            var grayData = new BlockData();
            grayData.gemType = GemType.Gray;
            grayData.enemyType = EnemyType.Chromophage;
            block.SetBlockData(grayData);

            // 색상 전환 애니메이션 (0.3초)
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easeT = t * t * (3f - 2f * t); // SmoothStep

                // 색상 보간
                Color currentColor = Color.Lerp(originalColor, grayColor, easeT);

                // 스케일 펄스 (1.0 → 1.15 → 1.0)
                float scalePulse = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                block.transform.localScale = Vector3.one * scalePulse;

                // 흔들림 (감쇄)
                float shake = (1f - t) * 2f;
                float offsetX = Mathf.Sin(t * Mathf.PI * 8f) * shake;
                float offsetY = Mathf.Cos(t * Mathf.PI * 6f) * shake * 0.5f;

                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Vector2 basePos = rt.anchoredPosition;
                    rt.anchoredPosition = new Vector2(
                        basePos.x + offsetX * Time.deltaTime * 60f,
                        basePos.y + offsetY * Time.deltaTime * 60f);
                }

                yield return null;
            }

            // 최종 상태 확정
            block.transform.localScale = Vector3.one;
            block.UpdateVisuals();
            Debug.Log($"[GameManager] 적군 생성: ({block.Coord})");
        }

// ============================================================
        // 스테이지 2 적군 시스템 (색상도둑 + 속박의 사슬)
        // ============================================================

        /// <summary>
        /// 스테이지2: 적군 생성 후 매칭 가능 여부 체크
        /// </summary>
        private IEnumerator SpawnStage2EnemiesAndCheckMoves(int count)
        {
            yield return StartCoroutine(SpawnStage2Enemies(count));

            if (matchingSystem != null && !matchingSystem.HasPossibleMoves())
            {
                if (HasActivatableSpecialBlocks())
                {
                    Debug.Log("[GameManager] 스테이지2: 매칭 불가능하지만 특수 블록 사용 가능 → Playing 유지");
                    SetGameState(GameState.Playing);
                    yield break;
                }

                Debug.Log("[GameManager] 스테이지2: 매칭 불가능 → 게임오버");
                GameOver(GameOverReason.MatchingDeadlock);
                yield break;
            }

            SetGameState(GameState.Playing);
        }

        /// <summary>
        /// 스테이지2: 회색 블록 + 속박의 사슬 랜덤 혼합
        /// </summary>
        private IEnumerator SpawnStage2Enemies(int count)
        {
            if (hexGrid == null) yield break;

            // 후보 수집: 일반 블록 (None, Gray, 특수블록, 이미 체인 제외)
            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) continue;
                if (block.Data.specialType != SpecialBlockType.None) continue;
                if (block.Data.hasChain) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) yield break;

            int spawnCount = Mathf.Min(count, candidates.Count);
            List<HexBlock> selected = new List<HexBlock>();
            for (int i = 0; i < spawnCount; i++)
            {
                int idx = Random.Range(0, candidates.Count);
                selected.Add(candidates[idx]);
                candidates.RemoveAt(idx);
            }

            // 시차 애니메이션: 50:50 확률로 회색 블록 or 속박의 사슬
            List<Coroutine> animations = new List<Coroutine>();
            for (int i = 0; i < selected.Count; i++)
            {
                bool isGray = Random.value < 0.5f;
                if (isGray)
                    animations.Add(StartCoroutine(AnimateGrayConversion(selected[i], i * 0.15f)));
                else
                    animations.Add(StartCoroutine(AnimateChainBinding(selected[i], i * 0.15f)));
            }

            foreach (var co in animations)
                yield return co;
        }

        /// <summary>
        /// 속박의 사슬: 블록에 체인 부착 (회전 불가)
        /// </summary>
        private IEnumerator AnimateChainBinding(HexBlock block, float delay)
        {
            if (block == null || block.Data == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (block == null || block.Data == null) yield break;

            // 체인 부착
            block.Data.hasChain = true;
            block.UpdateVisuals();

            // 스케일 펄스 + 흔들림 애니메이션
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scalePulse = 1f + 0.12f * Mathf.Sin(t * Mathf.PI);
                block.transform.localScale = Vector3.one * scalePulse;

                float shake = (1f - t) * 1.5f;
                float offsetX = Mathf.Sin(t * Mathf.PI * 10f) * shake;

                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Vector2 basePos = rt.anchoredPosition;
                    rt.anchoredPosition = new Vector2(
                        basePos.x + offsetX * Time.deltaTime * 60f,
                        basePos.y);
                }

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            Debug.Log($"[GameManager] 속박의 사슬: ({block.Coord}) 체인 부착");
        }

        /// <summary>
        /// 미션 카운트다운 애니메이션
        /// </summary>
        /// <summary>
        /// 레벨 모드 미션 순차 감소 코루틴 (1단위씩 감소 + 펄스 + 사운드).
        /// </summary>
        private IEnumerator StageMissionSequentialCountDown(int idx, Text countText, int from, int to, bool isComplete)
        {
            if (countText == null) yield break;

            // 동적 간격: 틱 수가 많으면 빠르게, 적으면 기본 속도
            const float normalInterval = 0.08f;
            const float minInterval = 0.02f;
            const float maxTotalTime = 0.5f;
            int step = from > to ? -1 : 1;
            int totalTicks = Mathf.Abs(to - from);
            float interval = Mathf.Clamp(maxTotalTime / Mathf.Max(1, totalTicks), minInterval, normalInterval);
            bool isFastMode = interval < normalInterval * 0.7f;
            int tickIndex = 0;

            for (int v = from + step; step > 0 ? v <= to : v >= to; v += step)
            {
                if (countText == null) yield break;
                countText.text = v.ToString();
                UIManager.UpdateMissionRowProgress(idx, v); // ★ 진행바 동기 (틱마다 차오름)

                // 펄스 애니메이션 (고속 시 3틱마다)
                if (!isFastMode || tickIndex % 3 == 0)
                    StartCoroutine(MissionCountPulse(countText.transform));

                // 틱 사운드 (고속 시 2틱마다)
                if (AudioManager.Instance != null && (!isFastMode || tickIndex % 2 == 0))
                {
                    float progress = 1f - (float)Mathf.Abs(v) / Mathf.Max(1f, Mathf.Abs(from));
                    AudioManager.Instance.PlayCountUpTick(progress);
                }

                tickIndex++;
                yield return new WaitForSeconds(interval);
            }

            // 미션 완료 시 체크마크 표시 + 미션 완료 사운드
            if (isComplete || to <= 0)
            {
                UIManager.UpdateMissionRowProgress(idx, 0); // 진행바 100%
                if (countText != null)
                {
                    countText.text = "";
                    ShowCheckMarkOnText(countText);
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlayMissionCompleteSound();
                }
            }
            else if (countText != null)
            {
                countText.text = to.ToString();
                UIManager.UpdateMissionRowProgress(idx, to);
            }

            // 코루틴 참조 해제
            if (stageMissionCountDownCos != null && idx < stageMissionCountDownCos.Length)
                stageMissionCountDownCos[idx] = null;
        }

        /// <summary>
        /// 카운트 텍스트 위치에 초록색 체크마크 아이콘 표시
        /// </summary>
        private void ShowCheckMarkOnText(Text countText)
        {
            if (countText == null) return;

            // 이미 체크마크가 있으면 중복 생성 방지
            Transform existing = countText.transform.Find("CheckMark");
            if (existing != null) return;

            GameObject checkObj = new GameObject("CheckMark");
            checkObj.transform.SetParent(countText.transform, false);
            RectTransform checkRt = checkObj.AddComponent<RectTransform>();
            checkRt.anchorMin = new Vector2(0, 0.5f);
            checkRt.anchorMax = new Vector2(0, 0.5f);
            checkRt.pivot = new Vector2(0, 0.5f);

            // 텍스트 크기에 맞춰 체크 아이콘 크기 결정
            float checkSize = countText.fontSize * 1.2f;
            checkRt.sizeDelta = new Vector2(checkSize, checkSize);
            checkRt.anchoredPosition = new Vector2(4, 0);

            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.sprite = MissionUIHelper.CreateCheckMarkSprite();
            checkImg.color = Color.white;
            checkImg.raycastTarget = false;

            // 체크마크 등장 애니메이션 (스케일 펀치)
            StartCoroutine(CheckMarkAppearAnimation(checkRt));
        }

        /// <summary>
        /// 체크마크 등장 스케일 애니메이션
        /// </summary>
        private IEnumerator CheckMarkAppearAnimation(RectTransform checkRt)
        {
            if (checkRt == null) yield break;

            float duration = 0.25f;
            float elapsed = 0f;

            checkRt.localScale = Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                // 오버슈트 이징: 살짝 커졌다가 원래 크기로
                float scale = t < 0.6f
                    ? Mathf.Lerp(0f, 1.3f, t / 0.6f)
                    : Mathf.Lerp(1.3f, 1f, (t - 0.6f) / 0.4f);
                if (checkRt != null)
                    checkRt.localScale = Vector3.one * scale;
                yield return null;
            }

            if (checkRt != null)
                checkRt.localScale = Vector3.one;
        }

        /// <summary>
        /// 블록이 미션 아이콘으로 날아드는 이펙트
        /// </summary>
        private IEnumerator BlockFlyEffectCoroutine(Vector2 targetPos, Canvas canvas)
        {
            // 랜덤 블록 색상
            Color[] blockColors = new Color[]
            {
                GemColors.GetColor(GemType.Red),
                GemColors.GetColor(GemType.Green),
                GemColors.GetColor(GemType.Blue),
                GemColors.GetColor(GemType.Yellow),
                GemColors.GetColor(GemType.Purple),
                GemColors.GetColor(GemType.Orange)
            };
            Color blockColor = blockColors[Random.Range(0, blockColors.Length)];

            // 블록 시작 위치 (화면 중앙 근처)
            Vector2 startPos = new Vector2(Random.Range(-100f, 100f), Random.Range(-100f, 100f));

            // 블록 시각 오브젝트 생성
            GameObject blockVisual = new GameObject("BlockFly");
            blockVisual.transform.SetParent(canvas.transform, false);

            RectTransform blockRt = blockVisual.AddComponent<RectTransform>();
            blockRt.anchoredPosition = startPos;
            blockRt.sizeDelta = new Vector2(40, 40);

            Image blockImage = blockVisual.AddComponent<Image>();
            blockImage.color = blockColor;
            blockImage.sprite = HexBlock.GetHexFlashSprite();

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 위치 이동
                Vector2 newPos = Vector2.Lerp(startPos, targetPos, VisualConstants.EaseInQuad(t));
                blockRt.anchoredPosition = newPos;

                // 스케일 감소
                blockRt.localScale = Vector3.one * (1f - t * 0.7f);

                // 회전
                blockRt.rotation = Quaternion.AngleAxis(t * 720f, Vector3.forward);

                // 투명도
                blockImage.color = new Color(blockColor.r, blockColor.g, blockColor.b, 1f - t);

                yield return null;
            }

            Destroy(blockVisual);
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

            if (droneSystem != null)
                droneSystem.OnDroneComplete -= OnSpecialBlockCompleted;

            // 미션 이벤트 정리
            if (stageManager != null)
            {
                stageManager.OnMissionProgressUpdated -= HandleMissionProgressUpdated;
                stageManager.OnMissionSlotReplaced -= HandleMissionSlotReplaced;
                stageManager.OnMissionProgressUpdated -= HandleInfiniteMissionProgressUpdated;
                stageManager.OnMissionComplete -= HandleMissionComplete;
            }
            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.OnSpecialBlockCreated -= HandleSpecialBlockCreatedForStage;
            }

            // 고블린 시스템 이벤트 정리
            if (goblinSystem != null)
            {
                goblinSystem.OnGoblinKilled -= OnGoblinKilledForMission;
            }
        }

        // ============================================================
        // 고블린 시스템 관련 메서드
        // ============================================================

        /// <summary>
        /// 고블린 제거 시 미션 시스템에 보고
        /// blockRemovalSystem.OnEnemyRemoved 이벤트를 통해 StageManager에 전달
        /// </summary>
        private void OnGoblinKilledForMission(int totalKills, bool isArmored, bool isArcher, bool isShieldType, bool isBomb, bool isHealer, bool isHeavy, bool isWizard, bool isThief, bool isWitch, int monsterLevel)
        {
            string lvStr = monsterLevel >= 2 ? "Lv2 " : "";
            string typeName = isWitch ? "마녀" : isThief ? "도둑" : isWizard ? "마법사" : isHeavy ? "헤비" : isHealer ? "힐러" : isBomb ? "폭탄" : isShieldType ? "방패" : (isArcher ? "활" : (isArmored ? "갑옷" : "몽둥이"));
            Debug.Log($"[GameManager] {lvStr}{typeName} 고블린 제거 미션 보고: 총 {totalKills}킬");

            // StageManager에 고블린 타입 정보 전달
            if (stageManager != null)
            {
                stageManager.ReportGoblinKill(isArmored, isArcher, isShieldType, isBomb, isHealer, isHeavy, isWizard, isThief, isWitch, monsterLevel);

                // 미션 완료 시 추가 소환 중단
                if (stageManager.IsMissionComplete() && goblinSystem != null)
                {
                    goblinSystem.MissionComplete = true;
                    Debug.Log("[GameManager] 고블린 미션 완료 → 추가 소환 중단");
                }
            }
        }

        /// <summary>
        /// 스테이지별 고블린 설정 반환
        /// </summary>
        private GoblinStageConfig GetGoblinConfigForStage(int stage)
        {
            switch (stage)
            {
                // 스테이지 1~10: 챕터 1 — 크리스탈 숲 (기본 고블린, 단일 미션)
                // ★ Stage 1: 고블린 없음 (R/G 블록 매칭 튜토리얼에 집중)
                case 1: return null;
                // ★ Stage 2 (2026-04-27): 흙더미 학습 미션 — 고블린 등장 안 함 (missionKillCount = 0)
                case 2: return new GoblinStageConfig { minSpawnPerTurn = 0, maxSpawnPerTurn = 0, missionKillCount = 0, maxOnBoard = 0 };
                // ★ Stage 3 (2026-04-27): 반경 2 필드 + 몽둥이 고블린 2→3 순차 웨이브 (총 5마리)
                case 3: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 5, maxOnBoard = 3 };
                case 4: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4 };
                case 5: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 11, maxOnBoard = 4 };
                case 6: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4 };
                case 7: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 4 };
                case 8: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 13, maxOnBoard = 5 };
                case 9: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 15, maxOnBoard = 5 };
                case 10: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 15, maxOnBoard = 5 };
                // 스테이지 11~15: 챕터 2 — 안개의 골짜기 (궁수 등장)
                case 11: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 5, maxOnBoard = 3, archerHp = 1 }; // 궁수3 = 3
                case 12: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 8, maxOnBoard = 3, archerHp = 1 }; // 기본1 + 궁수4 = 5
                case 13: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본4 + 갑옷1 + 궁수1 = 6
                case 14: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4, archerHp = 1, armoredHp = 15 }; // 갑옷1 + 궁수5 = 6
                case 15: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본1 + 갑옷2 + 궁수4 + 방패1 = 8
                // 스테이지 16~20: 챕터 3 — 얼어붙은 성채 (방패 등장)
                // ★ ratio는 레거시 — SpawnGoblins에서 미션 잔여량 기반 가중 랜덤 사용
                case 16: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 8, maxOnBoard = 3,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷1 + 방패2 = 5
                case 17: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷1 + 궁수1 + 방패2 = 6
                case 18: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷1 + 궁수1 + 방패2 = 6
                case 19: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷2 + 궁수2 + 방패2 = 8
                case 20: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본3 + 갑옷2 + 궁수2 + 방패2 = 9
                // 스테이지 21~30: 챕터 4 — 화산 심장 (4종 전체 혼합)
                // ★ ratio는 레거시 — SpawnGoblins에서 미션 잔여량 기반 가중 랜덤 사용
                // HP 고정: 몽둥이5, 갑옷15, 활1, 방패10(내구3)
                case 21: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷2 + 궁수2 + 방패2 = 8
                case 22: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷3 + 궁수2 + 방패2 = 9
                case 23: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 17, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷3 + 궁수3 + 방패3 = 11
                case 24: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷4 + 궁수3 + 방패3 = 12
                case 25: return new GoblinStageConfig { minSpawnPerTurn = 3, maxSpawnPerTurn = 6, missionKillCount = 44, maxOnBoard = 12,
                    archerRatio = 0.14f, armoredRatio = 0.32f, shieldRatio = 0.18f, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본16 + 갑옷14 + 궁수6 + 방패8 = 44
                case 26: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 17, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷3 + 궁수3 + 방패3 = 11
                case 27: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷4 + 궁수3 + 방패3 = 12
                case 28: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 21, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷4 + 궁수4 + 방패4 = 14
                case 29: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 23, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본2 + 갑옷5 + 궁수4 + 방패4 = 15
                case 30: return new GoblinStageConfig { minSpawnPerTurn = 4, maxSpawnPerTurn = 7, missionKillCount = 56, maxOnBoard = 14,
                    archerRatio = 0.14f, armoredRatio = 0.32f, shieldRatio = 0.21f, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3 }; // 기본18 + 갑옷18 + 궁수8 + 방패12 = 56
                // ============================================================
                // 스테이지 31~50: 챕터 5 — 폭염의 화약고 (폭탄 고블린 등장)
                // HP: 몽둥이5, 갑옷15, 활1, 방패10(내구3), 폭탄10
                // ============================================================
                case 31: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 8, maxOnBoard = 3,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본4 + 폭탄1 = 5
                case 32: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 갑옷2 + 궁수1 + 폭탄1 = 6
                case 33: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 궁수2 + 폭탄2 = 6
                case 34: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 갑옷2 + 방패2 + 폭탄2 = 6
                case 35: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 궁수2 + 방패2 + 폭탄2 = 8
                case 36: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 갑옷2 + 궁수2 + 폭탄2 = 8
                case 37: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 갑옷2 + 궁수2 + 방패2 + 폭탄2 = 8
                case 38: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 갑옷2 + 궁수2 + 방패2 + 폭탄2 = 8
                case 39: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 갑옷2 + 궁수1 + 방패2 + 폭탄3 = 8
                case 40: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본1 + 갑옷2 + 궁수2 + 방패2 + 폭탄2 = 9
                case 41: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 갑옷2 + 궁수1 + 방패2 + 폭탄2 = 9
                case 42: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본1 + 갑옷2 + 궁수2 + 방패2 + 폭탄2 = 9
                case 43: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본1 + 갑옷2 + 궁수1 + 방패2 + 폭탄3 = 9
                case 44: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 갑옷2 + 궁수2 + 방패2 + 폭탄3 = 9
                case 45: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 17, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 갑옷2 + 궁수2 + 방패2 + 폭탄3 = 11
                case 46: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 17, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 갑옷2 + 궁수2 + 방패2 + 폭탄3 = 11
                case 47: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 17, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 갑옷3 + 궁수3 + 방패2 + 폭탄3 = 11
                case 48: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본1 + 갑옷3 + 궁수2 + 방패3 + 폭탄3 = 12
                case 49: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 }; // 기본2 + 갑옷2 + 궁수2 + 방패2 + 폭탄4 = 12
                case 50: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 21, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                // 스테이지 51~60: 챕터 6 — 치유의 늪 (힐러 등장)
                case 51: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 8, maxOnBoard = 4,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 52: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 53: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 54: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 10, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 55: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 12, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 56: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 11, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 57: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 11, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 58: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 12, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 59: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 13, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 60: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 17, maxOnBoard = 7,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };

                // 스테이지 61~70: 챕터 7 — 거인의 둥지 (헤비급 고블린 등장)
                case 61: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 7, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 62: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 8, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 63: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 64: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 65: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 12, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 66: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 11, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 67: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 11, maxOnBoard = 5,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 68: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 12, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 69: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 13, maxOnBoard = 6,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 70: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 7,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                // 스테이지 71~80: 챕터 8 — 마법사의 탑 (마법사 고블린 등장)
                case 71: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 8, maxOnBoard = 5,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 72: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 5,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 73: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 9, maxOnBoard = 5,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 74: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 11, maxOnBoard = 6,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 75: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 14, maxOnBoard = 7,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 76: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 6,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 77: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 13, maxOnBoard = 6,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 78: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 14, maxOnBoard = 6,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 79: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 16, maxOnBoard = 7,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 80: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 21, maxOnBoard = 8,
                    wizardGoblinHp = 3, archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };

                // ============================================================
                // 챕터 9: 도둑의 은신처 (81~90) — 도둑 고블린 등장
                // ============================================================
                case 81: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 12, maxOnBoard = 6,
                    thiefRatio = 0.15f, thiefGoblinHp = 12, archerRatio = 0.1f, armoredRatio = 0.1f,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 82: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 14, maxOnBoard = 7,
                    thiefRatio = 0.15f, thiefGoblinHp = 12, shieldRatio = 0.1f, archerRatio = 0.05f, armoredRatio = 0.1f,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 83: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 15, maxOnBoard = 7,
                    thiefRatio = 0.2f, thiefGoblinHp = 12, archerRatio = 0.1f, armoredRatio = 0.05f,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 84: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 16, maxOnBoard = 7,
                    thiefRatio = 0.15f, thiefGoblinHp = 12, shieldRatio = 0.1f, heavyRatio = 0.05f, armoredRatio = 0.1f,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 85: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 8,
                    thiefRatio = 0.2f, thiefGoblinHp = 12, archerRatio = 0.1f, armoredRatio = 0.1f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 86: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 17, maxOnBoard = 7,
                    thiefRatio = 0.2f, thiefGoblinHp = 12, shieldRatio = 0.15f, armoredRatio = 0.15f,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 87: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 20, maxOnBoard = 8,
                    thiefRatio = 0.2f, thiefGoblinHp = 12, heavyRatio = 0.1f, archerRatio = 0.1f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 88: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 22, maxOnBoard = 8,
                    thiefRatio = 0.2f, thiefGoblinHp = 12, shieldRatio = 0.1f, heavyRatio = 0.1f, armoredRatio = 0.1f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 89: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    thiefRatio = 0.25f, thiefGoblinHp = 12, heavyRatio = 0.1f, archerRatio = 0.1f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };
                case 90: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    thiefRatio = 0.25f, thiefGoblinHp = 12, shieldRatio = 0.1f, heavyRatio = 0.1f, armoredRatio = 0.1f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 15, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10, heavyGoblinHp = 36 };

                // ============================================================
                // 챕터 10: 최종 전장 (91~100) — 전 몬스터 총출동
                // ============================================================
                case 91: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 12, maxOnBoard = 7,
                    witchRatio = 0.15f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.1f, thiefGoblinHp = 12, heavyRatio = 0.08f, armoredRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 92: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 13, maxOnBoard = 7,
                    witchRatio = 0.15f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.1f, thiefGoblinHp = 12, heavyRatio = 0.1f, armoredRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 93: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 15, maxOnBoard = 8,
                    witchRatio = 0.15f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.1f, thiefGoblinHp = 12, heavyRatio = 0.1f, archerRatio = 0.08f, armoredRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 94: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 10, maxOnBoard = 7,
                    witchRatio = 0.2f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.15f, thiefGoblinHp = 12, heavyRatio = 0.1f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 95: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 17, maxOnBoard = 8,
                    witchRatio = 0.15f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.1f, thiefGoblinHp = 12, heavyRatio = 0.08f, shieldRatio = 0.08f, archerRatio = 0.08f, armoredRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 96: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 17, maxOnBoard = 8,
                    witchRatio = 0.15f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.1f, thiefGoblinHp = 12, heavyRatio = 0.08f, shieldRatio = 0.08f, archerRatio = 0.08f, armoredRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 97: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 17, maxOnBoard = 8,
                    witchRatio = 0.15f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.1f, thiefGoblinHp = 12, heavyRatio = 0.08f, shieldRatio = 0.08f, archerRatio = 0.08f, armoredRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 98: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 12, maxOnBoard = 8,
                    witchRatio = 0.2f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.15f, thiefGoblinHp = 12, heavyRatio = 0.1f, shieldRatio = 0.05f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 99: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 17, maxOnBoard = 9,
                    witchRatio = 0.15f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.12f, thiefGoblinHp = 12, heavyRatio = 0.1f, shieldRatio = 0.08f, archerRatio = 0.08f, armoredRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 100: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 24, maxOnBoard = 10,
                    witchRatio = 0.2f, witchGoblinHp = 6, undeadHp = 3,
                    thiefRatio = 0.12f, thiefGoblinHp = 12, shieldRatio = 0.08f, heavyRatio = 0.1f, armoredRatio = 0.08f, archerRatio = 0.08f,
                    wizardGoblinHp = 3,
                    archerHp = 1, armoredHp = 16, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40 };

                // ============================================================
                // 스테이지 101~150: 엘리트 전장 — Lv2 몬스터 등장
                // ============================================================

                // Ch11 (101-110): Lv2 기본 등장, 기존 몬스터와 혼합
                case 101: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    armoredHp = 15, archerHp = 1, shieldGoblinHp = 10, shieldHp = 3 };
                case 102: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 14, maxOnBoard = 5,
                    armoredHp = 15, archerHp = 1, shieldGoblinHp = 10, shieldHp = 3 };
                case 103: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 16, maxOnBoard = 6,
                    armoredHp = 15, archerHp = 1, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 104: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 16, maxOnBoard = 6,
                    armoredHp = 15, archerHp = 1, shieldGoblinHp = 10, shieldHp = 3 };
                case 105: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 6,
                    armoredHp = 15, archerHp = 1, shieldGoblinHp = 10, shieldHp = 3, bombGoblinHp = 10 };
                case 106: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 10 };
                case 107: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 20, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3 };
                case 108: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 20, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 36 };
                case 109: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 22, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11 };
                case 110: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };

                // Ch12 (111-120): Lv2 비율 증가 + 특수 몬스터 혼합
                case 111: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11,
                    wizardGoblinHp = 3 };
                case 112: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 18, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, heavyGoblinHp = 38 };
                case 113: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 20, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11 };
                case 114: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 20, maxOnBoard = 7,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3,
                    thiefGoblinHp = 12 };
                case 115: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 22, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 116: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 22, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, wizardGoblinHp = 3 };
                case 117: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11 };
                case 118: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3,
                    witchGoblinHp = 6, undeadHp = 3 };
                case 119: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 26, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 120: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40,
                    thiefGoblinHp = 12, wizardGoblinHp = 3 };

                // Ch13 (121-130): 전병종 + Lv2 혼합
                case 121: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 22, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11 };
                case 122: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 22, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, heavyGoblinHp = 38, wizardGoblinHp = 3 };
                case 123: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, thiefGoblinHp = 12 };
                case 124: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, witchGoblinHp = 6, undeadHp = 3 };
                case 125: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 26, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 126: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, thiefGoblinHp = 12, wizardGoblinHp = 3 };
                case 127: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 26, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11 };
                case 128: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 26, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, witchGoblinHp = 6, undeadHp = 3, heavyGoblinHp = 38 };
                case 129: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, thiefGoblinHp = 12 };
                case 130: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 30, maxOnBoard = 10,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40,
                    thiefGoblinHp = 12, wizardGoblinHp = 3, witchGoblinHp = 6, undeadHp = 3 };

                // Ch14 (131-140): Lv2 주력 + 고난도
                case 131: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11 };
                case 132: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, heavyGoblinHp = 38 };
                case 133: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 26, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, thiefGoblinHp = 12 };
                case 134: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 24, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, wizardGoblinHp = 3, witchGoblinHp = 6, undeadHp = 3 };
                case 135: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 38 };
                case 136: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 26, maxOnBoard = 8,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, thiefGoblinHp = 12 };
                case 137: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, witchGoblinHp = 6, undeadHp = 3 };
                case 138: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, heavyGoblinHp = 40, wizardGoblinHp = 3 };
                case 139: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 30, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, thiefGoblinHp = 12, heavyGoblinHp = 40 };
                case 140: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 32, maxOnBoard = 10,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40,
                    thiefGoblinHp = 12, wizardGoblinHp = 3, witchGoblinHp = 6, undeadHp = 3 };

                // Ch15 (141-150): 엘리트 총력전
                case 141: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 26, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40 };
                case 142: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, thiefGoblinHp = 12, wizardGoblinHp = 3 };
                case 143: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 28, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, witchGoblinHp = 6, undeadHp = 3 };
                case 144: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 30, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, heavyGoblinHp = 40, thiefGoblinHp = 12 };
                case 145: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 30, maxOnBoard = 10,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40,
                    wizardGoblinHp = 3, witchGoblinHp = 6, undeadHp = 3 };
                case 146: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 30, maxOnBoard = 9,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, thiefGoblinHp = 12 };
                case 147: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 32, maxOnBoard = 10,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40,
                    witchGoblinHp = 6, undeadHp = 3 };
                case 148: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 32, maxOnBoard = 10,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, thiefGoblinHp = 12, wizardGoblinHp = 3, heavyGoblinHp = 40 };
                case 149: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 34, maxOnBoard = 10,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40,
                    thiefGoblinHp = 12, witchGoblinHp = 6, undeadHp = 3 };
                case 150: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 36, maxOnBoard = 10,
                    armoredHp = 16, archerHp = 1, shieldGoblinHp = 11, shieldHp = 3, bombGoblinHp = 11, heavyGoblinHp = 40,
                    thiefGoblinHp = 12, wizardGoblinHp = 3, witchGoblinHp = 6, undeadHp = 3 };

                default: return null;
            }
        }
    }

    /// <summary>
    /// 게임 상태 열거형
    /// </summary>
    public enum GameState
    {
        Lobby,
        Loading,
        Playing,
        Processing,
        Paused,
        StageClear,
        GameOver
    }

    /// <summary>
    /// 구버전 아이템 버튼(HammerItem/SwapItem/LineDrawItem/ReverseRotationItem 호스트)을
    /// "기능 유지 + 시각적 완전 차단" 상태로 매 프레임 강제하는 가드.
    /// HammerGauge.ForceAlphaOne 등 외부 코드가 알파/Graphic을 되돌리거나
    /// 아이템 로직이 자기 위치를 재설정해도 매 프레임 다시 차단해 잔재가 보이지 않게 한다.
    /// ChargeBar 버튼이 동일 좌표에 정상 표시되며, 이 가드는 그 ChargeBar 비주얼에 영향을 주지 않는다
    /// (가드는 구버전 버튼 GameObject에만 부착).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class LegacyButtonHider : MonoBehaviour
    {
        private static readonly Vector2 OffCanvas = new Vector2(10000f, 10000f);
        private RectTransform _rt;
        private CanvasGroup _cg;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        // ★ SetActive(true) 호출 시 즉시 hide 강제 — LateUpdate를 기다리지 않고
        //   같은 프레임 안에서 비주얼 차단 (한 프레임 깜빡임 제거).
        private void OnEnable() => Apply();

        private void LateUpdate() => Apply();

        private void Apply()
        {
            // 1) 알파/레이캐스트 강제
            if (_cg != null)
            {
                if (_cg.alpha != 0f) _cg.alpha = 0f;
                if (_cg.interactable) _cg.interactable = false;
                if (_cg.blocksRaycasts) _cg.blocksRaycasts = false;
            }
            // 2) 자식 Graphic 렌더링 차단 유지
            var graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i].enabled) graphics[i].enabled = false;
            }
            // 3) 위치를 캔버스 밖으로 강제 (다른 코드가 되돌려도 매 프레임 다시 밀어냄)
            if (_rt != null && _rt.anchoredPosition != OffCanvas)
                _rt.anchoredPosition = OffCanvas;
        }
    }
}
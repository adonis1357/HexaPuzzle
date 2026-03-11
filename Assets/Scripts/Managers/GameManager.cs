using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Items;

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



        [Header("Managers")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private StageManager stageManager;
        [SerializeField] private ItemManager itemManager;
        [SerializeField] private MissionSystem missionSystem;

        [Header("Game Settings")]
        [SerializeField] private int initialTurns = 30;

        // 게임 상태
        private GameState currentState = GameState.Loading;
        private int currentTurns;
        private int currentStage = 1;
        private int currentGold = 0;

        // 무한 모드
        private GameMode currentGameMode = GameMode.Infinite;
        private int rotationCount = 0;
        public GameMode CurrentGameMode => currentGameMode;
        public int RotationCount => rotationCount;

        private bool isProcessingChainDrill = false;
        private float lastAftermathProgressTime = 0f;  // ProcessSpecialBlockAftermath 진행 추적 타임스탬프
        private bool isInPostRecovery = false;
        private bool isPaused = false;
        private bool isItemAction = false;

        public bool IsItemAction { get => isItemAction; set => isItemAction = value; }

        // Stuck 상태 감지 워치독
        private float processingStartTime = 0f;
        private const float STUCK_TIMEOUT = 8f; // 8초 이상 Processing 상태면 복구태면 복구

        // 미션 진행도 UI 추적 (이벤트 핸들러용)
        private int[] lastDisplayedCounts;
        private Coroutine[] stageMissionCountDownCos;    // 레벨 미션별 카운트다운 코루틴

        // 무한도전 미션 순차 감소 추적
        private int infiniteMissionDisplayed = -1;      // 현재 화면에 표시된 remaining 값
        private int infiniteMissionTarget = -1;          // 목표 remaining 값
        private bool infiniteMissionComplete = false;
        private Coroutine infiniteMissionCountDownCo = null;


        // 프로퍼티
        public GameState CurrentState => currentState;
        public int CurrentTurns => currentTurns;
        public int CurrentStage => currentStage;
        public int InitialTurns => initialTurns;
        public bool IsPaused => isPaused;
        public int CurrentGold => currentGold;
        public bool IsProcessingChainDrill => isProcessingChainDrill;

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

            // UI 렌더링 품질 설정
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            Application.targetFrameRate = 60;

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

                // HUD UI 생성 (이동횟수, 누적 점수)
                CreateHUDElements(canvas);

                // SFX 토글 버튼 생성 (우하단 상단)
                CreateSoundToggleButton(canvas);

                // BGM 토글 버튼 생성 (좌측 대각선 — 삼각형 클러스터)
                CreateBGMToggleButton(canvas);

                // 로비 복귀 버튼 생성 (우하단 하단 — 삼각형 클러스터)
                CreateLobbyExitButton(canvas);

                // 회전 방향 토글 버튼 제거됨

                // 게임오버 팝업 생성
                CreateGameOverPopup(canvas);

                // 망치 UI 생성
                if (FindObjectOfType<HammerItem>() == null)
                    CreateHammerUI(canvas);

                // 스왑 UI 생성
                if (FindObjectOfType<SwapItem>() == null)
                    CreateSwapUI(canvas);

                // 한붓그리기 UI 생성
                if (FindObjectOfType<LineDrawItem>() == null)
                    CreateLineDrawUI(canvas);

                // 역회전 UI 생성
                if (FindObjectOfType<JewelsHexaPuzzle.Items.ReverseRotationItem>() == null)
                    CreateReverseRotationUI(canvas);

                // 골드 추가 버튼 생성 (좌측 하단)
                CreateGoldAddButton(canvas);

                // 아이템 수량 변경 이벤트 구독
                if (ItemManager.Instance != null)
                {
                    ItemManager.Instance.OnItemCountChanged += UpdateItemCountBadge;
                }

                // 특수 블록 테스트 버튼 패널 생성
                CreateTestBlockPanel(canvas);

                // 로비 UI 생성
                CreateLobbyUI(canvas);
            }
            ShowLobby();
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
        private GameObject sfxToggleBtnObj;   // SFX 토글 버튼 (로비/인게임 공용)
        private GameObject bgmToggleBtnObj;   // BGM 토글 버튼 (로비/인게임 공용)

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

            // 프레임 배경 (반투명 어두운 패널)
            Image frameBg = turnFrameObj.AddComponent<Image>();
            frameBg.color = new Color(0.1f, 0.1f, 0.2f, 0.5f);
            frameBg.raycastTarget = false;

            // 프레임 테두리 (밝은 아웃라인)
            Outline frameOutline = turnFrameObj.AddComponent<Outline>();
            frameOutline.effectColor = new Color(1f, 1f, 1f, 0.4f);
            frameOutline.effectDistance = new Vector2(2, 2);

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
            turnLabelText.alignment = TextAnchor.MiddleCenter;
            turnLabelText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
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

            // === 골드 (우측 상단) ===
            GameObject goldObj = new GameObject("HUD_GoldText");
            goldObj.transform.SetParent(canvas.transform, false);
            RectTransform goldRt = goldObj.AddComponent<RectTransform>();
            goldRt.anchorMin = new Vector2(1f, 1f);
            goldRt.anchorMax = new Vector2(1f, 1f);
            goldRt.pivot = new Vector2(1f, 1f);
            goldRt.anchoredPosition = new Vector2(-30f, -20f);
            goldRt.sizeDelta = new Vector2(120f, 40f);
            Text goldLabel = goldObj.AddComponent<Text>();
            goldLabel.font = font;
            goldLabel.fontSize = 28;
            goldLabel.alignment = TextAnchor.MiddleRight;
            goldLabel.color = new Color(1f, 0.84f, 0f); // 노란색
            goldLabel.raycastTarget = false;
            goldLabel.resizeTextForBestFit = true;
            goldLabel.resizeTextMinSize = 14;
            goldLabel.resizeTextMaxSize = 28;
            goldLabel.verticalOverflow = VerticalWrapMode.Overflow;
            goldLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            goldLabel.text = currentGold.ToString();
            Outline goldOutline = goldObj.AddComponent<Outline>();
            goldOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            goldOutline.effectDistance = new Vector2(1, 1);

            // 골드 라벨
            GameObject goldLabelObj = new GameObject("HUD_GoldLabel");
            goldLabelObj.transform.SetParent(canvas.transform, false);
            RectTransform goldLabelRt = goldLabelObj.AddComponent<RectTransform>();
            goldLabelRt.anchorMin = new Vector2(1f, 1f);
            goldLabelRt.anchorMax = new Vector2(1f, 1f);
            goldLabelRt.pivot = new Vector2(1f, 1f);
            goldLabelRt.anchoredPosition = new Vector2(-30f, -58f);
            goldLabelRt.sizeDelta = new Vector2(120f, 20f);
            Text goldLabelText = goldLabelObj.AddComponent<Text>();
            goldLabelText.font = font;
            goldLabelText.fontSize = 14;
            goldLabelText.alignment = TextAnchor.MiddleRight;
            goldLabelText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            goldLabelText.raycastTarget = false;
            goldLabelText.text = "GOLD";

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
            hudElements.Add(goldObj);
            hudElements.Add(goldLabelObj);

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

            Image btnBg = sfxToggleBtnObj.AddComponent<Image>();
            btnBg.sprite = HexBlock.GetHexFlashSprite();
            btnBg.type = Image.Type.Simple;
            btnBg.preserveAspect = true;
            btnBg.color = new Color(0.55f, 0.70f, 0.85f, 0.90f); // 밝은 하늘색

            Button btn = sfxToggleBtnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.65f, 0.80f, 0.95f, 0.95f);
            btnColors.pressedColor = new Color(0.40f, 0.55f, 0.70f, 0.95f);
            btn.colors = btnColors;

            // 스피커 아이콘 (프로시저럴)
            // 스피커 본체
            GameObject speakerBody = new GameObject("SpeakerBody");
            speakerBody.transform.SetParent(sfxToggleBtnObj.transform, false);
            Image bodyImg = speakerBody.AddComponent<Image>();
            bodyImg.color = Color.white;
            bodyImg.raycastTarget = false;
            RectTransform bodyRt = speakerBody.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(-6f, 0f);
            bodyRt.sizeDelta = new Vector2(14f, 16f);

            // 스피커 콘
            GameObject speakerCone = new GameObject("SpeakerCone");
            speakerCone.transform.SetParent(sfxToggleBtnObj.transform, false);
            Image coneImg = speakerCone.AddComponent<Image>();
            coneImg.color = Color.white;
            coneImg.raycastTarget = false;
            RectTransform coneRt = speakerCone.GetComponent<RectTransform>();
            coneRt.anchoredPosition = new Vector2(6f, 0f);
            coneRt.sizeDelta = new Vector2(12f, 24f);

            // 음파 표시 (ON 상태)
            GameObject wave1 = new GameObject("SoundWave1");
            wave1.transform.SetParent(sfxToggleBtnObj.transform, false);
            Image wave1Img = wave1.AddComponent<Image>();
            wave1Img.color = new Color(1f, 1f, 1f, 0.6f);
            wave1Img.raycastTarget = false;
            RectTransform wave1Rt = wave1.GetComponent<RectTransform>();
            wave1Rt.anchoredPosition = new Vector2(18f, 0f);
            wave1Rt.sizeDelta = new Vector2(4f, 14f);

            GameObject wave2 = new GameObject("SoundWave2");
            wave2.transform.SetParent(sfxToggleBtnObj.transform, false);
            Image wave2Img = wave2.AddComponent<Image>();
            wave2Img.color = new Color(1f, 1f, 1f, 0.4f);
            wave2Img.raycastTarget = false;
            RectTransform wave2Rt = wave2.GetComponent<RectTransform>();
            wave2Rt.anchoredPosition = new Vector2(24f, 0f);
            wave2Rt.sizeDelta = new Vector2(4f, 20f);

            // X 표시 (OFF 상태)
            GameObject muteX = new GameObject("MuteX");
            muteX.transform.SetParent(sfxToggleBtnObj.transform, false);
            Text muteText = muteX.AddComponent<Text>();
            muteText.font = font;
            muteText.fontSize = 30;
            muteText.alignment = TextAnchor.MiddleCenter;
            muteText.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            muteText.raycastTarget = false;
            muteText.text = "X";
            RectTransform muteRt = muteX.GetComponent<RectTransform>();
            muteRt.anchorMin = Vector2.zero;
            muteRt.anchorMax = Vector2.one;
            muteRt.offsetMin = Vector2.zero;
            muteRt.offsetMax = Vector2.zero;

            // 초기 상태 반영
            wave1.SetActive(!startMuted);
            wave2.SetActive(!startMuted);
            muteX.SetActive(startMuted);
            if (startMuted)
            {
                bodyImg.color = new Color(0.5f, 0.5f, 0.5f);
                coneImg.color = new Color(0.5f, 0.5f, 0.5f);
            }

            // 클릭 이벤트
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance == null) return;
                bool muted = AudioManager.Instance.ToggleSFXMute();

                wave1.SetActive(!muted);
                wave2.SetActive(!muted);
                muteX.SetActive(muted);
                bodyImg.color = muted ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
                coneImg.color = muted ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
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

            // 육각형 배경 (밝은 보라/라벤더 계열)
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

            // === 음표(♪) 프로시저럴 아이콘 ===

            // 음표 머리 (타원형)
            GameObject noteHead = new GameObject("NoteHead");
            noteHead.transform.SetParent(bgmToggleBtnObj.transform, false);
            Image noteHeadImg = noteHead.AddComponent<Image>();
            noteHeadImg.color = Color.white;
            noteHeadImg.raycastTarget = false;
            RectTransform noteHeadRt = noteHead.GetComponent<RectTransform>();
            noteHeadRt.anchoredPosition = new Vector2(-4f, -8f);
            noteHeadRt.sizeDelta = new Vector2(14f, 10f);
            noteHeadRt.localEulerAngles = new Vector3(0f, 0f, -20f); // 약간 기울임

            // 음표 줄기 (세로 막대)
            GameObject noteStem = new GameObject("NoteStem");
            noteStem.transform.SetParent(bgmToggleBtnObj.transform, false);
            Image noteStemImg = noteStem.AddComponent<Image>();
            noteStemImg.color = Color.white;
            noteStemImg.raycastTarget = false;
            RectTransform noteStemRt = noteStem.GetComponent<RectTransform>();
            noteStemRt.anchoredPosition = new Vector2(4f, 6f);
            noteStemRt.sizeDelta = new Vector2(3f, 28f);

            // 음표 깃발 (상단 꼬리)
            GameObject noteFlag = new GameObject("NoteFlag");
            noteFlag.transform.SetParent(bgmToggleBtnObj.transform, false);
            Image noteFlagImg = noteFlag.AddComponent<Image>();
            noteFlagImg.color = new Color(1f, 1f, 1f, 0.8f);
            noteFlagImg.raycastTarget = false;
            RectTransform noteFlagRt = noteFlag.GetComponent<RectTransform>();
            noteFlagRt.anchoredPosition = new Vector2(10f, 16f);
            noteFlagRt.sizeDelta = new Vector2(8f, 12f);

            // 음파 표시 (ON 상태)
            GameObject wave1 = new GameObject("BGMWave1");
            wave1.transform.SetParent(bgmToggleBtnObj.transform, false);
            Image wave1Img = wave1.AddComponent<Image>();
            wave1Img.color = new Color(1f, 1f, 1f, 0.5f);
            wave1Img.raycastTarget = false;
            RectTransform wave1Rt = wave1.GetComponent<RectTransform>();
            wave1Rt.anchoredPosition = new Vector2(-16f, 10f);
            wave1Rt.sizeDelta = new Vector2(4f, 10f);

            GameObject wave2 = new GameObject("BGMWave2");
            wave2.transform.SetParent(bgmToggleBtnObj.transform, false);
            Image wave2Img = wave2.AddComponent<Image>();
            wave2Img.color = new Color(1f, 1f, 1f, 0.35f);
            wave2Img.raycastTarget = false;
            RectTransform wave2Rt = wave2.GetComponent<RectTransform>();
            wave2Rt.anchoredPosition = new Vector2(-22f, 10f);
            wave2Rt.sizeDelta = new Vector2(4f, 16f);

            // X 표시 (OFF 상태)
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject muteX = new GameObject("BGMMuteX");
            muteX.transform.SetParent(bgmToggleBtnObj.transform, false);
            Text muteText = muteX.AddComponent<Text>();
            muteText.font = font;
            muteText.fontSize = 30;
            muteText.alignment = TextAnchor.MiddleCenter;
            muteText.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            muteText.raycastTarget = false;
            muteText.text = "X";
            RectTransform muteRt = muteX.GetComponent<RectTransform>();
            muteRt.anchorMin = Vector2.zero;
            muteRt.anchorMax = Vector2.one;
            muteRt.offsetMin = Vector2.zero;
            muteRt.offsetMax = Vector2.zero;

            // 초기 상태 반영
            wave1.SetActive(!startMuted);
            wave2.SetActive(!startMuted);
            muteX.SetActive(startMuted);
            if (startMuted)
            {
                noteHeadImg.color = new Color(0.5f, 0.5f, 0.5f);
                noteStemImg.color = new Color(0.5f, 0.5f, 0.5f);
                noteFlagImg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            }

            // 클릭 이벤트: BGM만 토글
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance == null) return;
                bool muted = AudioManager.Instance.ToggleBGMMute();

                wave1.SetActive(!muted);
                wave2.SetActive(!muted);
                muteX.SetActive(muted);
                noteHeadImg.color = muted ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
                noteStemImg.color = muted ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
                noteFlagImg.color = muted ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : new Color(1f, 1f, 1f, 0.8f);
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

        // 로비 UI 참조
        private GameObject lobbyContainer;

        // 스테이지 선택
        private int selectedStage = 1;
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

            // 어두운 배경 오버레이
            Image overlay = gameOverPopupObj.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.75f);
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
            panelBg.color = new Color(0.12f, 0.1f, 0.18f, 0.95f);

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
            gameOverTitleText.color = new Color(1f, 0.3f, 0.3f);
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
            scoreLabelText.color = new Color(0.7f, 0.7f, 0.7f);
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
            gameOverMovesText.color = new Color(0.8f, 0.8f, 0.8f);
            gameOverMovesText.raycastTarget = false;
            gameOverMovesText.text = "MOVES: 0";

            // === 재시작 버튼 ===
            GameObject retryObj = new GameObject("RetryButton");
            retryObj.transform.SetParent(panel.transform, false);
            RectTransform retryRt = retryObj.AddComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(0.5f, 0f);
            retryRt.anchorMax = new Vector2(0.5f, 0f);
            retryRt.pivot = new Vector2(0.5f, 0f);
            retryRt.anchoredPosition = new Vector2(0f, 30f);
            retryRt.sizeDelta = new Vector2(220f, 60f);

            Image retryBg = retryObj.AddComponent<Image>();
            retryBg.color = new Color(0.2f, 0.6f, 0.9f, 1f);

            Button retryBtn = retryObj.AddComponent<Button>();
            var retryColors = retryBtn.colors;
            retryColors.highlightedColor = new Color(0.3f, 0.7f, 1f);
            retryColors.pressedColor = new Color(0.15f, 0.45f, 0.7f);
            retryBtn.colors = retryColors;

            GameObject retryTextObj = new GameObject("RetryText");
            retryTextObj.transform.SetParent(retryObj.transform, false);
            RectTransform retryTextRt = retryTextObj.AddComponent<RectTransform>();
            retryTextRt.anchorMin = Vector2.zero;
            retryTextRt.anchorMax = Vector2.one;
            retryTextRt.offsetMin = Vector2.zero;
            retryTextRt.offsetMax = Vector2.zero;
            Text retryText = retryTextObj.AddComponent<Text>();
            retryText.font = font;
            retryText.fontSize = 26;
            retryText.alignment = TextAnchor.MiddleCenter;
            retryText.color = Color.white;
            retryText.raycastTarget = false;
            retryText.text = "나가기";

            retryBtn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                gameOverPopupObj.SetActive(false);
                Time.timeScale = 1f;
                if (scoreManager != null) scoreManager.ResetScore();
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

            // 게임오버 시 점수는 0으로 표시
            int finalScore = 0;
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

            // 최고 점수 정보 표시
            yield return new WaitForSecondsRealtime(0.3f);
            ShowGameOverHighScore(finalScore);
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
        private void ShowGameOverHighScore(int currentScore)
        {
            if (gameOverPopupObj == null || scoreManager == null) return;

            int levelBest = scoreManager.GetLevelHighScore(selectedStage);
            int personalBest = scoreManager.GetPersonalLevelBest(selectedStage);
            bool isNewLevelBest = currentScore >= levelBest && currentScore > 0;
            bool isNewPersonalBest = currentScore >= personalBest && currentScore > 0;

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
            float baseY = hSize * sqrt3 * (-5f) - btnHexH * 0.3f - 5f;
            float offX = midX - cx;
            float offY = baseY - cy;
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
        /// 아이템 버튼 하단 중앙에 수량 배지 생성 (x99, 흰색 바탕 + 검정 아웃라인)
        /// </summary>
        private void CreateItemCountBadge(GameObject btnObj, ItemType itemType)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 배지 배경 (흰색, 하단 중앙)
            GameObject badgeBg = new GameObject("CountBadgeBg");
            badgeBg.transform.SetParent(btnObj.transform, false);
            Image bgImg = badgeBg.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.92f);
            bgImg.raycastTarget = false;
            RectTransform bgRt = badgeBg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0f);
            bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = new Vector2(0f, 14f);
            bgRt.sizeDelta = new Vector2(38f, 16f);

            // 배지 아웃라인 (검정, 배경 뒤)
            GameObject outlineObj = new GameObject("CountBadgeOutline");
            outlineObj.transform.SetParent(badgeBg.transform, false);
            Image outlineImg = outlineObj.AddComponent<Image>();
            outlineImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
            outlineImg.raycastTarget = false;
            RectTransform outlineRt = outlineObj.GetComponent<RectTransform>();
            outlineRt.anchorMin = Vector2.zero;
            outlineRt.anchorMax = Vector2.one;
            outlineRt.offsetMin = new Vector2(-1.5f, -1.5f);
            outlineRt.offsetMax = new Vector2(1.5f, 1.5f);
            outlineObj.transform.SetAsFirstSibling();

            // 수량 텍스트
            GameObject textObj = new GameObject("CountBadgeText");
            textObj.transform.SetParent(badgeBg.transform, false);
            Text countText = textObj.AddComponent<Text>();
            int count = ItemManager.Instance != null ? ItemManager.Instance.GetItemCount(itemType) : 0;
            countText.text = $"x{count}";
            countText.font = font;
            countText.fontSize = 11;
            countText.fontStyle = FontStyle.Bold;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            countText.raycastTarget = false;
            countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // 딕셔너리에 저장
            itemCountBadges[itemType] = countText;
        }

        /// <summary>
        /// 모든 아이템 수량 배지 업데이트
        /// </summary>
        public void UpdateAllItemCountBadges()
        {
            if (ItemManager.Instance == null) return;
            foreach (var kvp in itemCountBadges)
            {
                if (kvp.Value != null)
                {
                    int count = ItemManager.Instance.GetItemCount(kvp.Key);
                    kvp.Value.text = $"x{count}";
                }
            }
        }

        /// <summary>
        /// 특정 아이템 수량 배지 업데이트
        /// </summary>
        public void UpdateItemCountBadge(ItemType type, int count)
        {
            if (itemCountBadges.ContainsKey(type) && itemCountBadges[type] != null)
            {
                itemCountBadges[type].text = $"x{count}";
            }
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
            bgOverlay.color = new Color(0f, 0f, 0f, 0.6f);

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
            panelBg.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);
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
            float posY = hSize * sqrt3 * (-5f) - btnHexH * 0.3f - 5f;

            // 70px 사이즈 육각형 버튼 직접 생성
            GameObject btnObj = new GameObject("GoldAddButton");
            btnObj.transform.SetParent(canvas.transform, false);
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

            // 클릭 이벤트: 100 골드 추가
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayButtonClick();

                AddGold(100);
                ShowFloatingMessage("+100 골드!");

                // 버튼 펄스 리액션
                StartCoroutine(GoldButtonPulse(btnObj));
            });

            btnObj.transform.SetAsLastSibling();
            hudElements.Add(btnObj);
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

            // InputSystem에 직접 참조 전달 (FindObjectOfType 의존 제거)
            if (inputSystem != null)
                inputSystem.SetEditorTestSystem(editorTestSystemRef);

            Debug.Log("[GameManager] 특수 블록 테스트 버튼 패널 생성 완료");
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

            // HUD 텍스트 즉시 0 초기화 (이전 게임 값이 보이는 것 방지)
            if (hudTurnText != null) hudTurnText.text = "0";
            if (hudScoreText != null) hudScoreText.text = "0";

            // 이전 게임 이벤트 정리 (중복 구독 방지)
            CleanupGameEvents();

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

            if (hexGrid != null)
            {
                hexGrid.InitializeGrid();
                yield return new WaitForSeconds(0.3f);

                // 매칭 없는 블록으로 배치
                hexGrid.PopulateWithNoMatches();

                // 튜토리얼 보드 사전 배치 + 시퀀스 트리거
                if (TutorialManager.Instance != null)
                    TutorialManager.Instance.OnStageStart(selectedStage);

                if (blockRemovalSystem != null)
                {
                    blockRemovalSystem.TriggerStartDrop();
                    while (blockRemovalSystem.IsProcessing)
                        yield return null;
                }
            }

            // 고블린 시스템 초기화 (스테이지 1~10에서만 활성화)
            if (goblinSystem != null && currentGameMode == GameMode.Stage && selectedStage >= 1 && selectedStage <= 10)
            {
                var goblinConfig = GetGoblinConfigForStage(selectedStage);
                if (goblinConfig != null)
                {
                    goblinSystem.Initialize(hexGrid, goblinConfig);
                    // 고블린 킬 이벤트 → 미션 시스템 연동
                    goblinSystem.OnGoblinKilled -= OnGoblinKilledForMission;
                    goblinSystem.OnGoblinKilled += OnGoblinKilledForMission;
                    // 첫 턴 시작 시 즉시 고블린 소환
                    yield return StartCoroutine(goblinSystem.ProcessTurn());
                    Debug.Log($"[GameManager] 고블린 시스템 활성화 + 초기 소환 완료: 스테이지 {selectedStage}");

                    // 고블린 출현 알림 메시지
                    ShowFloatingMessage("⚔️ 고블린이 출현! 블록을 떨어뜨려 처치하세요!");
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
                    var missions = stageManager.CurrentStageData.missions;

                    // 미션 등장 애니메이션 (무한도전과 동일한 슬라이드인)
                    yield return StartCoroutine(SetupAndAnimateStageMission(missions));

                    // 각 미션별 마지막 표시 카운트 추적 (인스턴스 필드에 저장)
                    lastDisplayedCounts = new int[missions.Length];
                    for (int i = 0; i < missions.Length; i++)
                        lastDisplayedCounts[i] = missions[i].targetCount;

                    // 미션 진행도 업데이트 콜백 (명명 메서드로 구독 — 해제 가능)
                    stageManager.OnMissionProgressUpdated += HandleMissionProgressUpdated;
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
                processingStartTime = Time.time;


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
        }

        /// <summary>
        /// 이전 게임 이벤트 정리 (중복 구독 방지)
        /// </summary>
        private void CleanupGameEvents()
        {
            if (stageManager != null)
            {
                stageManager.OnMissionProgressUpdated -= HandleMissionProgressUpdated;
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

        /// <summary>
        /// 특수 블록(드릴 등)이 개별 블록 1개를 파괴할 때 미션 카운팅
        /// 드릴의 연출과 동기화하여 1개씩 보고
        /// </summary>
        public void OnSingleGemDestroyedForMission(GemType gemType)
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
                    Debug.LogWarning($"[GameManager] OnSingleGemDestroyedForMission: missionSystem is null! gemType={gemType}");
                }
            }
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
        /// 연쇄 완료 이벤트
        /// </summary>
private void OnCascadeComplete()
        {
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

            // 무한모드: 미션 턴 종료 → 게임오버 체크 → 적군 스폰
            if (currentGameMode == GameMode.Infinite)
            {
                if (missionSystem != null)
                    missionSystem.OnTurnEnd();

                if (currentTurns <= 0)
                {
                    GameOver();
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

            // 스테이지모드: 게임오버 확인
            if (currentTurns <= 0)
            {
                GameOver();
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
            Debug.LogWarning("[GameManager] WaitForBRSReady: BRS is processing, waiting...");
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
            while (blockRemovalSystem.IsProcessing && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                processingStartTime = Time.time; // stuck 오판 방지
                // 연쇄 깊이가 변하면 진행 중이므로 타이머 리셋
                int curDepth = blockRemovalSystem.CurrentCascadeDepth;
                if (curDepth != lastCascadeDepth)
                {
                    lastCascadeDepth = curDepth;
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
                    Debug.LogWarning("[GameManager] BRS still processing before falling. Waiting...");
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

                if (currentTurns <= 0)
                {
                    GameOver();
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

            if (currentTurns <= 0)
            {
                GameOver();
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
                        while (drillSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; processingStartTime = Time.time; yield return null; }
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
                hudScoreText.text = string.Format("{0:N0}", scoreManager.CurrentScore);

            // 최고 점수 HUD 실시간 갱신 (현재 점수가 최고를 넘으면 표시 업데이트)
            if (scoreManager != null)
            {
                int currentScore = scoreManager.CurrentScore;
                int levelBest = scoreManager.GetLevelHighScore(selectedStage);
                int personalBest = scoreManager.GetPersonalLevelBest(selectedStage);

                if (hudLevelBestText != null)
                {
                    int displayLevelBest = currentScore > levelBest ? currentScore : levelBest;
                    hudLevelBestText.text = displayLevelBest > 0
                        ? string.Format("BEST: {0:N0}", displayLevelBest)
                        : "BEST: ---";
                    // 새 기록이면 색상 강조
                    hudLevelBestText.color = currentScore > levelBest && levelBest > 0
                        ? new Color(1f, 1f, 0.3f, 1f)
                        : new Color(1f, 0.85f, 0.3f, 0.9f);
                }

                if (hudPersonalBestText != null)
                {
                    int displayPersonalBest = currentScore > personalBest ? currentScore : personalBest;
                    hudPersonalBestText.text = displayPersonalBest > 0
                        ? string.Format("MY BEST: {0:N0}", displayPersonalBest)
                        : "MY BEST: ---";
                    hudPersonalBestText.color = currentScore > personalBest && personalBest > 0
                        ? new Color(0.5f, 1f, 0.5f, 1f)
                        : new Color(0.7f, 0.9f, 1f, 0.9f);
                }
            }
        }

        private void RefreshTurnDisplay()
        {
            if (hudTurnText != null)
                hudTurnText.text = currentTurns.ToString();
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

            // 드릴 튜토리얼 완료 마킹 (stage 11 클리어 시)
            if (selectedStage == 11 && TutorialManager.Instance != null)
                TutorialManager.Instance.MarkCompleted("stage11_drill_tutorial");

            // 다음 레벨 해금
            LevelRegistry.UnlockLevel(selectedStage + 1);

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

            Debug.Log($"Stage {currentStage} Clear!");

            // 남은 이동횟수만큼 랜덤 위치에 보너스 드릴 생성
            processingStartTime = Time.time;
            yield return StartCoroutine(SpawnBonusDrills(currentTurns));

            // 모든 특수 블록 동시 발동 (기존 + 새로 생성된 드릴 포함)
            processingStartTime = Time.time;
            yield return StartCoroutine(ActivateAllSpecialBlocks());

            // 모든 시스템이 완전히 멈출 때까지 대기
            processingStartTime = Time.time;
            yield return StartCoroutine(WaitForAllSystemsIdle());

            // 낙하 + 연쇄 매칭 처리 (완전 정지될 때까지 반복)
            processingStartTime = Time.time;
            yield return StartCoroutine(ProcessStageClearAftermath());

            // 남은 이동횟수 스냅샷 (턴 초기화 전에 저장)
            int remainingTurns = currentTurns;

            // 골드 계산: 1턴당 50골드
            int goldReward = remainingTurns * 50;

            // 점수 보너스 계산 (턴 초기화 전에 수행)
            StageSummaryData? summary = null;
            if (scoreManager != null)
                summary = scoreManager.CalculateStageClearBonus(remainingTurns, initialTurns);

            // 레벨별 최고 점수 갱신
            if (scoreManager != null)
                scoreManager.TryUpdateLevelHighScore(selectedStage);

            // 턴 0으로 표시
            currentTurns = 0;
            if (uiManager != null)
                uiManager.UpdateTurnDisplay(currentTurns);

            // 골드 즉시 지급
            AddGold(goldReward);

            // 특수 블록 연쇄 완료 후 1초 대기 후 클리어 팝업 표시
            yield return new WaitForSeconds(1f);

            if (uiManager != null)
            {
                if (summary.HasValue)
                {
                    uiManager.ShowStageClearPopup(summary.Value, goldReward);
                }
                else
                {
                    uiManager.ShowStageClearPopup(goldReward);
                }
            }

            // 팝업 표시 후 워치독 타임아웃 방지 (사용자가 확인 버튼 누를 때까지 대기)
            processingStartTime = float.MaxValue;
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

            // 순차적으로 드릴 변환 (연출 효과)
            for (int i = 0; i < drillCount; i++)
            {
                HexBlock block = candidates[i];

                // 블록 데이터를 드릴로 변환
                block.Data.specialType = SpecialBlockType.Drill;
                block.Data.drillDirection = directions[Random.Range(0, directions.Length)];
                block.UpdateVisuals();

                // 스케일 팝 애니메이션
                StartCoroutine(DrillSpawnPopAnimation(block.transform));

                // 사운드 효과
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayButtonClick();

                yield return new WaitForSeconds(0.08f);
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

            // 무지개 발동
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

            Debug.Log($"[GameManager] 특수 블록 발동 완료: 드릴({drillBlocks.Count}), 폭탄({bombBlocks.Count}), 무지개({donutBlocks.Count}), X({xBlocks.Count}), 드론({droneBlocks.Count})");
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
        /// 무지개(도넛) 블록 발동
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
            OnGoldChanged?.Invoke(currentGold);
            if (uiManager != null)
                uiManager.UpdateGoldDisplay(currentGold);
            SaveGold();
            Debug.Log($"[GameManager] 골드 차감: -{amount}, 잔액: {currentGold}");
            return true;
        }

        /// <summary>
        /// 골드 저장 (PlayerPrefs)
        /// </summary>
        private void SaveGold()
        {
            PlayerPrefs.SetInt("TotalGold", currentGold);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장된 골드 로드 (PlayerPrefs)
        /// </summary>
        private void LoadGold()
        {
            currentGold = PlayerPrefs.GetInt("TotalGold", 0);
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

                float transitionDuration = 0.5f;
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
                float popDuration = 0.2f;
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

                    float fadeDuration = 0.3f;
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

                float slideDuration = 0.4f;
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

            if (missions.Length == 1)
            {
                // === 단일 미션: SetupAndAnimateMission과 동일한 슬라이드인 ===
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
            }
            else
            {
                // === 복수 미션: 개별 행으로 하나씩 순차 등장 ===
                float rowSpacing = 95f; // 90px 높이 + 5px 간격

                for (int i = 0; i < missions.Length; i++)
                {
                    RectTransform rowRt = uiManager.CreateIndividualMissionRow(canvas, missions[i], i);
                    if (rowRt == null) continue;

                    // 각 행의 최종 위치: 플레이스홀더 아래 + 행 간격
                    float targetY = -102f - (i * rowSpacing);
                    Vector2 targetPos = new Vector2(40f, targetY);
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

                    // 다음 미션 등장 전 약간의 딜레이
                    if (i < missions.Length - 1)
                        yield return new WaitForSeconds(0.12f);
                }
            }

            Debug.Log($"[StageMission] 전체 미션 등장 완료 ({missions.Length}개)");
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

            float slideDuration = 0.4f;
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
        /// 게임 오버 (무한도전: 게임 종료 컨셉 — 점수 획득 + 베스트 스코어 저장)
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
                // 스테이지 모드: 기존 로직 (점수 0 처리)
                Debug.Log("Game Over! (점수 0 처리)");

                UpdateGameOverTitle("GAME OVER", new Color(1f, 0.3f, 0.3f));

                if (gameOverScoreText != null)
                    gameOverScoreText.text = "0";
                if (gameOverMovesText != null)
                    gameOverMovesText.text = "MOVES: 0";

                if (uiManager != null)
                    uiManager.ShowGameOverPopup();

                StartCoroutine(AnimateGameOverPopup());
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

            // 배경 오버레이
            Image overlay = lobbyContainer.AddComponent<Image>();
            overlay.color = new Color(0.06f, 0.04f, 0.12f, 0.97f);
            overlay.raycastTarget = true;

            // === 게임 타이틀 ===
            GameObject titleObj = new GameObject("LobbyTitle");
            titleObj.transform.SetParent(lobbyContainer.transform, false);
            RectTransform titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -120f);
            titleRt.sizeDelta = new Vector2(600f, 70f);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 48;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.raycastTarget = false;
            titleText.text = "HEXA PUZZLE";

            // === 보유 골드 표시 (우측 상단 — 인게임과 동일한 위치) ===
            GameObject lobbyGoldObj = new GameObject("LobbyGold");
            lobbyGoldObj.transform.SetParent(lobbyContainer.transform, false);
            RectTransform lobbyGoldRt = lobbyGoldObj.AddComponent<RectTransform>();
            lobbyGoldRt.anchorMin = new Vector2(1f, 1f);
            lobbyGoldRt.anchorMax = new Vector2(1f, 1f);
            lobbyGoldRt.pivot = new Vector2(1f, 1f);
            lobbyGoldRt.anchoredPosition = new Vector2(-30f, -20f); // 인게임과 동일
            lobbyGoldRt.sizeDelta = new Vector2(120f, 40f);         // 인게임과 동일

            lobbyGoldText = lobbyGoldObj.AddComponent<Text>();
            lobbyGoldText.font = font;
            lobbyGoldText.fontSize = 28;
            lobbyGoldText.alignment = TextAnchor.MiddleRight;
            lobbyGoldText.color = new Color(1f, 0.84f, 0f);
            lobbyGoldText.raycastTarget = false;
            lobbyGoldText.resizeTextForBestFit = true;
            lobbyGoldText.resizeTextMinSize = 14;
            lobbyGoldText.resizeTextMaxSize = 28;
            lobbyGoldText.verticalOverflow = VerticalWrapMode.Overflow;
            lobbyGoldText.horizontalOverflow = HorizontalWrapMode.Overflow;
            lobbyGoldText.text = currentGold.ToString();

            Outline lobbyGoldOutline = lobbyGoldObj.AddComponent<Outline>();
            lobbyGoldOutline.effectColor = new Color(0f, 0f, 0f, 0.7f); // 인게임과 동일
            lobbyGoldOutline.effectDistance = new Vector2(1, 1);

            // GOLD 라벨 (인게임과 동일 위치)
            GameObject lobbyGoldLabelObj = new GameObject("LobbyGoldLabel");
            lobbyGoldLabelObj.transform.SetParent(lobbyContainer.transform, false);
            RectTransform lobbyGoldLabelRt = lobbyGoldLabelObj.AddComponent<RectTransform>();
            lobbyGoldLabelRt.anchorMin = new Vector2(1f, 1f);
            lobbyGoldLabelRt.anchorMax = new Vector2(1f, 1f);
            lobbyGoldLabelRt.pivot = new Vector2(1f, 1f);
            lobbyGoldLabelRt.anchoredPosition = new Vector2(-30f, -58f); // 인게임과 동일
            lobbyGoldLabelRt.sizeDelta = new Vector2(120f, 20f);
            Text lobbyGoldLabelText = lobbyGoldLabelObj.AddComponent<Text>();
            lobbyGoldLabelText.font = font;
            lobbyGoldLabelText.fontSize = 14;
            lobbyGoldLabelText.alignment = TextAnchor.MiddleRight;
            lobbyGoldLabelText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            lobbyGoldLabelText.raycastTarget = false;
            lobbyGoldLabelText.text = "GOLD";

            // === 스크롤 영역 (레벨 버튼 그리드) ===
            GameObject scrollObj = new GameObject("LevelScrollView");
            scrollObj.transform.SetParent(lobbyContainer.transform, false);
            RectTransform scrollRt = scrollObj.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(20f, 80f);
            scrollRt.offsetMax = new Vector2(-20f, -200f);

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;

            // 마스크
            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.01f);
            Mask mask = scrollObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // 콘텐츠 영역
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform, false);
            RectTransform contentRt = contentObj.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0.5f, 1f);
            contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;

            scrollRect.content = contentRt;

            // === 그리드 레이아웃으로 레벨 버튼 배치 (3열) ===
            int columns = 3;
            float buttonSize = 200f;
            float buttonGap = 25f;
            float spacing = buttonSize + buttonGap;
            int totalLevels = allLevels.Count;
            int rows = Mathf.CeilToInt((float)totalLevels / columns);
            float contentHeight = rows * spacing + buttonGap;
            contentRt.sizeDelta = new Vector2(columns * spacing + buttonGap, contentHeight);

            for (int i = 0; i < totalLevels; i++)
            {
                var level = allLevels[i];
                int col = i % columns;
                int row = i / columns;

                // 그리드 중앙 정렬
                float xOffset = (col - (columns - 1) * 0.5f) * spacing;
                float yOffset = -buttonGap - row * spacing - buttonSize * 0.5f;
                Vector2 pos = new Vector2(xOffset, yOffset);

                var display = level.lobbyDisplay ?? new LobbyDisplayConfig
                {
                    backgroundColor = new Color(0.3f, 0.3f, 0.5f),
                    borderColor = new Color(0.5f, 0.5f, 0.7f),
                    buttonSize = buttonSize
                };

                CreateStageButton(
                    contentObj,
                    font,
                    pos,
                    level.levelName,
                    level.subtitle ?? "",
                    display.backgroundColor,
                    display.borderColor,
                    level.levelId,
                    display.buttonSize > 0 ? display.buttonSize : buttonSize,
                    level.isLocked
                );
            }

            // === 튜토리얼 초기화 버튼 (좌측 하단) ===
            CreateTutorialResetButton(lobbyContainer, font);

            lobbyContainer.SetActive(false);
            lobbyContainer.transform.SetAsLastSibling();
            Debug.Log($"[GameManager] 로비 UI 생성 완료 (레벨 {totalLevels}개)");
        }

        /// <summary>
        /// 로비 좌측 하단에 튜토리얼 초기화 버튼 생성
        /// </summary>
        private void CreateTutorialResetButton(GameObject parent, Font font)
        {
            // 버튼 컨테이너
            GameObject btnObj = new GameObject("TutorialResetButton");
            btnObj.transform.SetParent(parent.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0f, 0f);
            btnRt.anchorMax = new Vector2(0f, 0f);
            btnRt.pivot = new Vector2(0f, 0f);
            btnRt.anchoredPosition = new Vector2(20f, 20f);
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
        /// 스테이지 버튼 생성 헬퍼
        /// </summary>
        private void CreateStageButton(GameObject parent, Font font, Vector2 position,
            string stageLabel, string subtitle, Color bgColor, Color borderColor, int stageNum,
            float btnSize = 250f, bool isLocked = false)
        {
            GameObject stageBtn = new GameObject($"Stage{stageNum}Button");
            stageBtn.transform.SetParent(parent.transform, false);
            RectTransform stageBtnRt = stageBtn.AddComponent<RectTransform>();
            stageBtnRt.anchorMin = new Vector2(0.5f, 0.5f);
            stageBtnRt.anchorMax = new Vector2(0.5f, 0.5f);
            stageBtnRt.pivot = new Vector2(0.5f, 0.5f);
            stageBtnRt.anchoredPosition = position;
            stageBtnRt.sizeDelta = new Vector2(btnSize, btnSize);

            // 잠긴 레벨: 색상 어둡게
            Color displayBg = isLocked ? bgColor * 0.4f : bgColor;
            Color displayBorder = isLocked ? borderColor * 0.4f : borderColor;

            // 육각형 배경
            Image hexBg = stageBtn.AddComponent<Image>();
            hexBg.sprite = HexBlock.GetHexFlashSprite();
            hexBg.color = displayBg;
            hexBg.type = Image.Type.Simple;
            hexBg.preserveAspect = true;

            Button btn = stageBtn.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = displayBg * 1.3f;
            btnColors.pressedColor = displayBg * 0.7f;
            btn.colors = btnColors;
            btn.targetGraphic = hexBg;

            // 잠긴 레벨: 버튼 비활성화
            if (isLocked)
            {
                btn.interactable = false;
            }

            // 육각형 테두리
            GameObject borderObj = new GameObject("HexBorder");
            borderObj.transform.SetParent(stageBtn.transform, false);
            RectTransform borderRt = borderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-8f, -8f);
            borderRt.offsetMax = new Vector2(8f, 8f);
            Image borderImg = borderObj.AddComponent<Image>();
            borderImg.sprite = HexBlock.GetHexBorderSprite();
            borderImg.color = displayBorder;
            borderImg.type = Image.Type.Simple;
            borderImg.preserveAspect = true;
            borderImg.raycastTarget = false;

            // 스테이지 텍스트
            GameObject stageTextObj = new GameObject("StageText");
            stageTextObj.transform.SetParent(stageBtn.transform, false);
            RectTransform stageTextRt = stageTextObj.AddComponent<RectTransform>();
            stageTextRt.anchorMin = new Vector2(0.5f, 0.5f);
            stageTextRt.anchorMax = new Vector2(0.5f, 0.5f);
            stageTextRt.pivot = new Vector2(0.5f, 0.5f);
            stageTextRt.anchoredPosition = new Vector2(0f, 20f);
            stageTextRt.sizeDelta = new Vector2(btnSize * 0.9f, 36f);
            Text stageText = stageTextObj.AddComponent<Text>();
            stageText.font = font;
            stageText.fontSize = isLocked ? 18 : 28;
            stageText.alignment = TextAnchor.MiddleCenter;
            stageText.color = isLocked ? new Color(0.5f, 0.5f, 0.6f) : new Color(0.85f, 0.9f, 1f);
            stageText.raycastTarget = false;
            stageText.text = isLocked ? "🔒" : stageLabel;

            if (!isLocked)
            {
                // 레벨 최고 점수 표시 (BEST: 전체 유저 최고, MY BEST: 개인 레벨 최고)
                int levelBest = scoreManager != null ? scoreManager.GetLevelHighScore(stageNum) : 0;
                int personalBest = scoreManager != null ? scoreManager.GetPersonalLevelBest(stageNum) : 0;

                if (levelBest > 0 || personalBest > 0)
                {
                    // 레벨 BEST (전체 유저 최고)
                    GameObject lbObj = new GameObject("LevelBest");
                    lbObj.transform.SetParent(stageBtn.transform, false);
                    RectTransform lbRt = lbObj.AddComponent<RectTransform>();
                    lbRt.anchorMin = new Vector2(0.5f, 0.5f);
                    lbRt.anchorMax = new Vector2(0.5f, 0.5f);
                    lbRt.pivot = new Vector2(0.5f, 0.5f);
                    lbRt.anchoredPosition = new Vector2(0f, -12f);
                    lbRt.sizeDelta = new Vector2(btnSize * 0.9f, 18f);
                    Text lbText = lbObj.AddComponent<Text>();
                    lbText.font = font;
                    lbText.fontSize = 12;
                    lbText.alignment = TextAnchor.MiddleCenter;
                    lbText.color = new Color(1f, 0.85f, 0.3f, 0.95f);
                    lbText.raycastTarget = false;
                    lbText.text = levelBest > 0 ? string.Format("BEST: {0:N0}", levelBest) : "";
                    Outline lbOutline = lbObj.AddComponent<Outline>();
                    lbOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                    lbOutline.effectDistance = new Vector2(1, 1);

                    // 개인 레벨별 MY BEST
                    GameObject pbObj = new GameObject("PersonalBest");
                    pbObj.transform.SetParent(stageBtn.transform, false);
                    RectTransform pbRt = pbObj.AddComponent<RectTransform>();
                    pbRt.anchorMin = new Vector2(0.5f, 0.5f);
                    pbRt.anchorMax = new Vector2(0.5f, 0.5f);
                    pbRt.pivot = new Vector2(0.5f, 0.5f);
                    pbRt.anchoredPosition = new Vector2(0f, -28f);
                    pbRt.sizeDelta = new Vector2(btnSize * 0.9f, 16f);
                    Text pbText = pbObj.AddComponent<Text>();
                    pbText.font = font;
                    pbText.fontSize = 10;
                    pbText.alignment = TextAnchor.MiddleCenter;
                    pbText.color = new Color(0.7f, 0.9f, 1f, 0.85f);
                    pbText.raycastTarget = false;
                    pbText.text = personalBest > 0 ? string.Format("MY: {0:N0}", personalBest) : "";
                    Outline pbOutline = pbObj.AddComponent<Outline>();
                    pbOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
                    pbOutline.effectDistance = new Vector2(1, 1);
                }
                else
                {
                    // 최고 점수 없으면 재생 아이콘 표시
                    GameObject playObj = new GameObject("PlayIcon");
                    playObj.transform.SetParent(stageBtn.transform, false);
                    RectTransform playRt = playObj.AddComponent<RectTransform>();
                    playRt.anchorMin = new Vector2(0.5f, 0.5f);
                    playRt.anchorMax = new Vector2(0.5f, 0.5f);
                    playRt.pivot = new Vector2(0.5f, 0.5f);
                    playRt.anchoredPosition = new Vector2(0f, -15f);
                    playRt.sizeDelta = new Vector2(40f, 40f);
                    Text playText = playObj.AddComponent<Text>();
                    playText.font = font;
                    playText.fontSize = 32;
                    playText.alignment = TextAnchor.MiddleCenter;
                    playText.color = Color.white;
                    playText.raycastTarget = false;
                    playText.text = "\u25B6";
                }
            }

            // 부제 텍스트
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(stageBtn.transform, false);
            RectTransform subtitleRt = subtitleObj.AddComponent<RectTransform>();
            subtitleRt.anchorMin = new Vector2(0.5f, 0.5f);
            subtitleRt.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleRt.pivot = new Vector2(0.5f, 0.5f);
            subtitleRt.anchoredPosition = new Vector2(0f, -48f);
            subtitleRt.sizeDelta = new Vector2(btnSize * 0.95f, 20f);
            Text subtitleText = subtitleObj.AddComponent<Text>();
            subtitleText.font = font;
            subtitleText.fontSize = 10;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.color = isLocked ? new Color(0.4f, 0.4f, 0.5f) : new Color(0.7f, 0.7f, 0.8f);
            subtitleText.raycastTarget = false;
            subtitleText.text = subtitle;

            // 버튼 클릭 — LevelRegistry에서 게임 모드 결정
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClick();
                selectedStage = stageNum;

                // LevelRegistry에서 레벨 데이터 조회하여 게임 모드 결정
                var levelData = LevelRegistry.GetLevel(stageNum);
                if (levelData != null)
                {
                    currentGameMode = levelData.gameMode;
                    selectedLevelData = levelData;
                }
                else
                {
                    // 폴백: 기존 로직
                    currentGameMode = (stageNum == 1) ? GameMode.Stage : GameMode.Infinite;
                    selectedLevelData = null;
                }

                HideLobby();
                StartGame();
            });
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

            // 그리드 숨기기 (SetActive 대신 CanvasGroup으로 — 다른 시스템의 FindObjectOfType 유지)
            if (hexGrid != null)
                SetCanvasGroupVisible(hexGrid.gameObject, false);

            // HUD 숨기기
            foreach (var hud in hudElements)
            {
                if (hud != null) hud.SetActive(false);
            }

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

            // SFX/BGM 버튼을 lobbyContainer 위로 올려 로비에서도 보이게 함
            if (sfxToggleBtnObj != null) sfxToggleBtnObj.transform.SetAsLastSibling();
            if (bgmToggleBtnObj != null) bgmToggleBtnObj.transform.SetAsLastSibling();

            // 로비 골드 표시 갱신 (인게임과 동일 형식: 숫자만)
            if (lobbyGoldText != null)
                lobbyGoldText.text = currentGold.ToString();

            // 로비 레벨 버튼 최고 점수 갱신
            RefreshLobbyHighScores();

            Debug.Log("[GameManager] 로비 표시");
        }

        /// <summary>
        /// 로비 레벨 버튼의 최고 점수 텍스트 갱신
        /// </summary>
        private void RefreshLobbyHighScores()
        {
            if (lobbyContainer == null || scoreManager == null) return;

            // 각 스테이지 버튼 내부의 LevelBest / PersonalBest 텍스트를 찾아 갱신
            var allLevels = LevelRegistry.GetAllLevels();
            for (int i = 0; i < allLevels.Count; i++)
            {
                int stageNum = allLevels[i].levelId;
                GameObject stageBtn = null;

                // Content 안에서 버튼 찾기
                Transform content = lobbyContainer.transform.Find("LevelScrollView/Content");
                if (content != null)
                    stageBtn = content.Find($"Stage{stageNum}Button")?.gameObject;

                if (stageBtn == null) continue;

                int levelBest = scoreManager.GetLevelHighScore(stageNum);
                int personalBest = scoreManager.GetPersonalLevelBest(stageNum);

                // LevelBest 텍스트 갱신 또는 생성
                Transform lbTr = stageBtn.transform.Find("LevelBest");
                Transform pbTr = stageBtn.transform.Find("PersonalBest");

                if (lbTr != null)
                {
                    Text lbText = lbTr.GetComponent<Text>();
                    if (lbText != null)
                        lbText.text = levelBest > 0 ? string.Format("BEST: {0:N0}", levelBest) : "";
                }
                else if (levelBest > 0 && !allLevels[i].isLocked)
                {
                    // 이전에 점수가 없어서 PlayIcon만 있었던 경우 → 새로 생성
                    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    float btnSize = stageBtn.GetComponent<RectTransform>().sizeDelta.x;

                    // PlayIcon 제거
                    Transform playIcon = stageBtn.transform.Find("PlayIcon");
                    if (playIcon != null) Destroy(playIcon.gameObject);

                    // LevelBest 생성
                    GameObject lbObj = new GameObject("LevelBest");
                    lbObj.transform.SetParent(stageBtn.transform, false);
                    RectTransform lbRt = lbObj.AddComponent<RectTransform>();
                    lbRt.anchorMin = new Vector2(0.5f, 0.5f);
                    lbRt.anchorMax = new Vector2(0.5f, 0.5f);
                    lbRt.pivot = new Vector2(0.5f, 0.5f);
                    lbRt.anchoredPosition = new Vector2(0f, -12f);
                    lbRt.sizeDelta = new Vector2(btnSize * 0.9f, 18f);
                    Text newLbText = lbObj.AddComponent<Text>();
                    newLbText.font = font;
                    newLbText.fontSize = 12;
                    newLbText.alignment = TextAnchor.MiddleCenter;
                    newLbText.color = new Color(1f, 0.85f, 0.3f, 0.95f);
                    newLbText.raycastTarget = false;
                    newLbText.text = string.Format("BEST: {0:N0}", levelBest);
                    Outline lbOutline = lbObj.AddComponent<Outline>();
                    lbOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                    lbOutline.effectDistance = new Vector2(1, 1);

                    // PersonalBest 생성
                    GameObject pbObj = new GameObject("PersonalBest");
                    pbObj.transform.SetParent(stageBtn.transform, false);
                    RectTransform pbRt = pbObj.AddComponent<RectTransform>();
                    pbRt.anchorMin = new Vector2(0.5f, 0.5f);
                    pbRt.anchorMax = new Vector2(0.5f, 0.5f);
                    pbRt.pivot = new Vector2(0.5f, 0.5f);
                    pbRt.anchoredPosition = new Vector2(0f, -28f);
                    pbRt.sizeDelta = new Vector2(btnSize * 0.9f, 16f);
                    Text newPbText = pbObj.AddComponent<Text>();
                    newPbText.font = font;
                    newPbText.fontSize = 10;
                    newPbText.alignment = TextAnchor.MiddleCenter;
                    newPbText.color = new Color(0.7f, 0.9f, 1f, 0.85f);
                    newPbText.raycastTarget = false;
                    newPbText.text = personalBest > 0 ? string.Format("MY: {0:N0}", personalBest) : "";
                    Outline pbOutline = pbObj.AddComponent<Outline>();
                    pbOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
                    pbOutline.effectDistance = new Vector2(1, 1);
                }

                if (pbTr != null)
                {
                    Text pbText = pbTr.GetComponent<Text>();
                    if (pbText != null)
                        pbText.text = personalBest > 0 ? string.Format("MY: {0:N0}", personalBest) : "";
                }
            }
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
            Time.timeScale = 1f;
            if (scoreManager != null) scoreManager.ResetScore();
            ShowLobby();
        }

        /// <summary>
        /// 클리어 후 로비로 돌아가기 (스코어 리셋 없음)
        /// </summary>
        public void ReturnToLobby()
        {
            Time.timeScale = 1f;
            if (scoreManager != null) scoreManager.ResetScore();
            Debug.Log("[GameManager] ReturnToLobby: 로비로 돌아갑니다");
            ShowLobby();
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
            SetGameState(GameState.Playing);
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
                if (HasActivatableSpecialBlocks())
                {
                    Debug.Log("[GameManager] EnemySystem 스폰 후: 매칭 불가 but 특수 블록 있음 → Playing");
                    SetGameState(GameState.Playing);
                    yield break;
                }
                Debug.Log("[GameManager] EnemySystem 스폰 후: 매칭 불가 → 게임오버");
                GameOver();
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
                GameOver();
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
                GameOver();
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
        private void OnGoblinKilledForMission(int totalKills)
        {
            Debug.Log($"[GameManager] 고블린 제거 미션 보고: 총 {totalKills}킬");

            // StageManager의 OnEnemyRemoved와 동일한 경로로 미션 진행도 업데이트
            if (stageManager != null)
            {
                // StageManager에 직접 접근하여 미션 업데이트
                stageManager.ReportGoblinKill();
            }
        }

        /// <summary>
        /// 스테이지별 고블린 설정 반환
        /// </summary>
        private GoblinStageConfig GetGoblinConfigForStage(int stage)
        {
            switch (stage)
            {
                case 1: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 2, maxOnBoard = 3 };
                case 2: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 3, maxOnBoard = 4 };
                case 3: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 1, missionKillCount = 4, maxOnBoard = 5 };
                case 4: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 5, maxOnBoard = 5 };
                case 5: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 6, maxOnBoard = 6 };
                case 6: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 2, missionKillCount = 8, maxOnBoard = 6 };
                case 7: return new GoblinStageConfig { minSpawnPerTurn = 1, maxSpawnPerTurn = 3, missionKillCount = 10, maxOnBoard = 7 };
                case 8: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 12, maxOnBoard = 7 };
                case 9: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 3, missionKillCount = 14, maxOnBoard = 8 };
                case 10: return new GoblinStageConfig { minSpawnPerTurn = 2, maxSpawnPerTurn = 4, missionKillCount = 16, maxOnBoard = 8 };
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
}
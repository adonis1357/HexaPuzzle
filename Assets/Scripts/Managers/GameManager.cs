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
        [SerializeField] private LaserBlockSystem laserSystem;
        [SerializeField] private EnemySystem enemySystem;



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
        private bool isInPostRecovery = false;
        private bool isPaused = false;
        private bool isItemAction = false;

        public bool IsItemAction { get => isItemAction; set => isItemAction = value; }

        // Stuck 상태 감지 워치독
        private float processingStartTime = 0f;
        private const float STUCK_TIMEOUT = 8f; // 8초 이상 Processing 상태면 복구태면 복구

        // 미션 진행도 UI 추적 (이벤트 핸들러용)
        private int[] lastDisplayedCounts;


        // 프로퍼티
        public GameState CurrentState => currentState;
        public int CurrentTurns => currentTurns;
        public int CurrentStage => currentStage;
        public int InitialTurns => initialTurns;
        public bool IsPaused => isPaused;
        public int CurrentGold => currentGold;

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

            QualitySettings.antiAliasing = 4;

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

                // 사운드 토글 버튼 생성 (우하단)
                CreateSoundToggleButton(canvas);

                // 로비 복귀 버튼 생성 (사운드 버튼 아래)
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
            scaler.matchWidthOrHeight = 0f; // 가로 기준 스케일 (그리드가 가로로 꽉 참)
        }

        /// <summary>
        /// HUD 요소 동적 생성 (이동횟수: 우상단, 누적 점수: 상단 중앙)
        /// </summary>
        // HUD 직접 참조 (UIManager 연동 보장)
        private Text hudScoreText;
        private Text hudTurnText;
        private Text lobbyGoldText;

        private void CreateHUDElements(Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // === 이동횟수 (우측 상단) ===
            GameObject turnObj = new GameObject("HUD_TurnText");
            turnObj.transform.SetParent(canvas.transform, false);
            RectTransform turnRt = turnObj.AddComponent<RectTransform>();
            turnRt.anchorMin = new Vector2(1f, 1f);
            turnRt.anchorMax = new Vector2(1f, 1f);
            turnRt.pivot = new Vector2(1f, 1f);
            turnRt.anchoredPosition = new Vector2(-30f, -20f);
            turnRt.sizeDelta = new Vector2(160f, 50f);
            Text turnLabel = turnObj.AddComponent<Text>();
            turnLabel.font = font;
            turnLabel.fontSize = 32;
            turnLabel.alignment = TextAnchor.MiddleRight;
            turnLabel.color = Color.white;
            turnLabel.raycastTarget = false;
            turnLabel.resizeTextForBestFit = true;
            turnLabel.resizeTextMinSize = 14;
            turnLabel.resizeTextMaxSize = 32;
            turnLabel.verticalOverflow = VerticalWrapMode.Overflow;
            turnLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            turnLabel.text = currentGameMode == GameMode.Infinite ? "0" : initialTurns.ToString();
            hudTurnText = turnLabel;

            // 이동횟수 라벨
            GameObject turnLabelObj = new GameObject("HUD_TurnLabel");
            turnLabelObj.transform.SetParent(canvas.transform, false);
            RectTransform turnLabelRt = turnLabelObj.AddComponent<RectTransform>();
            turnLabelRt.anchorMin = new Vector2(1f, 1f);
            turnLabelRt.anchorMax = new Vector2(1f, 1f);
            turnLabelRt.pivot = new Vector2(1f, 1f);
            turnLabelRt.anchoredPosition = new Vector2(-30f, -65f);
            turnLabelRt.sizeDelta = new Vector2(160f, 24f);
            Text turnLabelText = turnLabelObj.AddComponent<Text>();
            turnLabelText.font = font;
            turnLabelText.fontSize = 16;
            turnLabelText.alignment = TextAnchor.MiddleRight;
            turnLabelText.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            turnLabelText.raycastTarget = false;
            turnLabelText.text = "MOVES";

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

            // === 골드 (우측 상단, 이동횟수 아래) ===
            GameObject goldObj = new GameObject("HUD_GoldText");
            goldObj.transform.SetParent(canvas.transform, false);
            RectTransform goldRt = goldObj.AddComponent<RectTransform>();
            goldRt.anchorMin = new Vector2(1f, 1f);
            goldRt.anchorMax = new Vector2(1f, 1f);
            goldRt.pivot = new Vector2(1f, 1f);
            goldRt.anchoredPosition = new Vector2(-30f, -130f);
            goldRt.sizeDelta = new Vector2(160f, 50f);
            Text goldLabel = goldObj.AddComponent<Text>();
            goldLabel.font = font;
            goldLabel.fontSize = 32;
            goldLabel.alignment = TextAnchor.MiddleRight;
            goldLabel.color = new Color(1f, 0.84f, 0f); // 노란색
            goldLabel.raycastTarget = false;
            goldLabel.resizeTextForBestFit = true;
            goldLabel.resizeTextMinSize = 14;
            goldLabel.resizeTextMaxSize = 32;
            goldLabel.verticalOverflow = VerticalWrapMode.Overflow;
            goldLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            goldLabel.text = currentGold.ToString();

            // 골드 라벨
            GameObject goldLabelObj = new GameObject("HUD_GoldLabel");
            goldLabelObj.transform.SetParent(canvas.transform, false);
            RectTransform goldLabelRt = goldLabelObj.AddComponent<RectTransform>();
            goldLabelRt.anchorMin = new Vector2(1f, 1f);
            goldLabelRt.anchorMax = new Vector2(1f, 1f);
            goldLabelRt.pivot = new Vector2(1f, 1f);
            goldLabelRt.anchoredPosition = new Vector2(-30f, -175f);
            goldLabelRt.sizeDelta = new Vector2(160f, 24f);
            Text goldLabelText = goldLabelObj.AddComponent<Text>();
            goldLabelText.font = font;
            goldLabelText.fontSize = 16;
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
            hudElements.Add(turnObj);
            hudElements.Add(turnLabelObj);
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
            bool startMuted = AudioManager.Instance != null && AudioManager.Instance.IsMuted;

            // 망치 버튼과 같은 좌표 체계 — 바로 아래 배치
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float rightmostX = hSize * 1.5f * 5f;
            float lowestY = hSize * Mathf.Sqrt(3f) * (-5f);

            // 버튼 컨테이너
            GameObject btnObj = new GameObject("SoundToggleButton");
            btnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(rightmostX, lowestY - 90f);
            btnRt.sizeDelta = new Vector2(70f, 70f);

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.2f, 0.2f, 0.3f, 0.85f);

            Button btn = btnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.35f, 0.35f, 0.5f, 0.9f);
            btnColors.pressedColor = new Color(0.15f, 0.15f, 0.25f, 0.9f);
            btn.colors = btnColors;

            // 스피커 아이콘 (프로시저럴)
            // 스피커 본체
            GameObject speakerBody = new GameObject("SpeakerBody");
            speakerBody.transform.SetParent(btnObj.transform, false);
            Image bodyImg = speakerBody.AddComponent<Image>();
            bodyImg.color = Color.white;
            bodyImg.raycastTarget = false;
            RectTransform bodyRt = speakerBody.GetComponent<RectTransform>();
            bodyRt.anchoredPosition = new Vector2(-6f, 0f);
            bodyRt.sizeDelta = new Vector2(14f, 16f);

            // 스피커 콘
            GameObject speakerCone = new GameObject("SpeakerCone");
            speakerCone.transform.SetParent(btnObj.transform, false);
            Image coneImg = speakerCone.AddComponent<Image>();
            coneImg.color = Color.white;
            coneImg.raycastTarget = false;
            RectTransform coneRt = speakerCone.GetComponent<RectTransform>();
            coneRt.anchoredPosition = new Vector2(6f, 0f);
            coneRt.sizeDelta = new Vector2(12f, 24f);

            // 음파 표시 (ON 상태)
            GameObject wave1 = new GameObject("SoundWave1");
            wave1.transform.SetParent(btnObj.transform, false);
            Image wave1Img = wave1.AddComponent<Image>();
            wave1Img.color = new Color(1f, 1f, 1f, 0.6f);
            wave1Img.raycastTarget = false;
            RectTransform wave1Rt = wave1.GetComponent<RectTransform>();
            wave1Rt.anchoredPosition = new Vector2(18f, 0f);
            wave1Rt.sizeDelta = new Vector2(4f, 14f);

            GameObject wave2 = new GameObject("SoundWave2");
            wave2.transform.SetParent(btnObj.transform, false);
            Image wave2Img = wave2.AddComponent<Image>();
            wave2Img.color = new Color(1f, 1f, 1f, 0.4f);
            wave2Img.raycastTarget = false;
            RectTransform wave2Rt = wave2.GetComponent<RectTransform>();
            wave2Rt.anchoredPosition = new Vector2(24f, 0f);
            wave2Rt.sizeDelta = new Vector2(4f, 20f);

            // X 표시 (OFF 상태)
            GameObject muteX = new GameObject("MuteX");
            muteX.transform.SetParent(btnObj.transform, false);
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
                bool muted = AudioManager.Instance.ToggleMute();

                wave1.SetActive(!muted);
                wave2.SetActive(!muted);
                muteX.SetActive(muted);
                bodyImg.color = muted ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
                coneImg.color = muted ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            });

            hudElements.Add(btnObj);
            Debug.Log("[GameManager] 사운드 토글 버튼 생성 완료 (우하단)");
        }

        // 로비 복귀 버튼 참조 (로비에서 숨기기 용)
        private GameObject lobbyExitBtnObj;

        /// <summary>
        /// 로비 복귀 버튼 생성 (사운드 버튼 아래, 문 모양 아이콘)
        /// </summary>
        private void CreateLobbyExitButton(Canvas canvas)
        {
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float rightmostX = hSize * 1.5f * 5f;
            float lowestY = hSize * Mathf.Sqrt(3f) * (-5f);

            lobbyExitBtnObj = new GameObject("LobbyExitButton");
            lobbyExitBtnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = lobbyExitBtnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            // 사운드 버튼(lowestY - 90) 아래 80px
            btnRt.anchoredPosition = new Vector2(rightmostX, lowestY - 170f);
            btnRt.sizeDelta = new Vector2(70f, 70f);

            Image btnBg = lobbyExitBtnObj.AddComponent<Image>();
            btnBg.color = new Color(0.2f, 0.2f, 0.3f, 0.85f);

            Button btn = lobbyExitBtnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.45f, 0.3f, 0.2f, 0.9f);
            btnColors.pressedColor = new Color(0.3f, 0.2f, 0.12f, 0.9f);
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
            panelRt.sizeDelta = new Vector2(500f, 420f);

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
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 42;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.3f, 0.3f);
            titleText.raycastTarget = false;
            titleText.text = "GAME OVER";

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

            // === 이동 횟수 표시 ===
            GameObject movesObj = new GameObject("MovesLabel");
            movesObj.transform.SetParent(panel.transform, false);
            RectTransform movesRt = movesObj.AddComponent<RectTransform>();
            movesRt.anchorMin = new Vector2(0.5f, 1f);
            movesRt.anchorMax = new Vector2(0.5f, 1f);
            movesRt.pivot = new Vector2(0.5f, 1f);
            movesRt.anchoredPosition = new Vector2(0f, -210f);
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
            retryText.text = "RETRY";

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
                        // BRS 또는 특수 블록 시스템이 활발히 처리 중이면 stuck이 아님 — 타이머 리셋
                        bool systemsActive = (blockRemovalSystem != null && blockRemovalSystem.IsProcessing)
                            || (drillSystem != null && drillSystem.IsDrilling)
                            || (bombSystem != null && bombSystem.IsBombing)
                            || (donutSystem != null && donutSystem.IsActivating)
                            || (xBlockSystem != null && xBlockSystem.IsActivating)
                            || (laserSystem != null && laserSystem.IsActivating);

                        if (systemsActive)
                        {
                            Debug.Log($"[GameManager] Processing {elapsed:F1}s but systems still active. Resetting stuck timer.");
                            processingStartTime = Time.time;
                        }
                        else
                        {
                            Debug.LogWarning($"[GameManager] STUCK DETECTED! Processing for {elapsed:F1}s. Force recovering...");
                            Debug.LogWarning($"[GameManager] Flags: isProcessingChainDrill={isProcessingChainDrill}, BRS.IsProcessing={blockRemovalSystem?.IsProcessing}");
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
                    if (laserSystem != null) laserSystem.ForceReset();

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

            if (laserSystem != null)
                laserSystem.OnLaserComplete += OnSpecialBlockCompleted;

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
                    blockRemovalSystem.OnGemsRemovedDetailed += missionSystem.OnGemsRemoved;
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

        /// <summary>
        /// 망치 아이템 UI 직접 생성 (HammerItemSetup 우회)
        /// </summary>
        private void CreateHammerUI(Canvas canvas)
        {
            // 배경 오버레이 (활성화 시 표시)
            GameObject overlayObj = new GameObject("HammerOverlay");
            overlayObj.transform.SetParent(canvas.transform, false);
            var overlay = overlayObj.AddComponent<Image>();
            overlay.color = new Color(0.6f, 1f, 0.6f, 0.15f);
            overlay.raycastTarget = false;
            RectTransform overlayRt = overlayObj.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            overlayObj.SetActive(false);

            // 망치 버튼 — 그리드 기준 좌표 (가장 오른쪽 블록 x, 가장 아래 블록 y)
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float rightmostX = hSize * 1.5f * 5f;   // q=5 블록의 x 좌표
            float lowestY = hSize * Mathf.Sqrt(3f) * (-5f); // r=-5 블록의 y 좌표

            GameObject btnObj = new GameObject("HammerButton");
            btnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(rightmostX, lowestY);
            btnRt.sizeDelta = new Vector2(80f, 80f);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.25f, 0.25f, 0.35f, 0.9f);
            var btn = btnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.4f, 0.9f, 0.4f, 1f);
            btnColors.pressedColor = new Color(0.3f, 0.7f, 0.3f, 1f);
            btn.colors = btnColors;

            // 망치 머리
            GameObject headObj = new GameObject("HammerHead");
            headObj.transform.SetParent(btnObj.transform, false);
            var headImg = headObj.AddComponent<Image>();
            headImg.color = new Color(0.7f, 0.7f, 0.75f, 1f);
            headImg.raycastTarget = false;
            RectTransform headRt = headObj.GetComponent<RectTransform>();
            headRt.anchoredPosition = new Vector2(0f, 12f);
            headRt.sizeDelta = new Vector2(36f, 18f);
            headRt.localRotation = Quaternion.Euler(0, 0, -15f);

            // 망치 자루
            GameObject handleObj = new GameObject("HammerHandle");
            handleObj.transform.SetParent(btnObj.transform, false);
            var handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(0.55f, 0.35f, 0.15f, 1f);
            handleImg.raycastTarget = false;
            RectTransform handleRt = handleObj.GetComponent<RectTransform>();
            handleRt.anchoredPosition = new Vector2(2f, -12f);
            handleRt.sizeDelta = new Vector2(8f, 32f);
            handleRt.localRotation = Quaternion.Euler(0, 0, -15f);

            // 하이라이트
            GameObject hlObj = new GameObject("HammerHL");
            hlObj.transform.SetParent(headObj.transform, false);
            var hlImg = hlObj.AddComponent<Image>();
            hlImg.color = new Color(1f, 1f, 1f, 0.3f);
            hlImg.raycastTarget = false;
            RectTransform hlRt = hlObj.GetComponent<RectTransform>();
            hlRt.anchoredPosition = new Vector2(-5f, 3f);
            hlRt.sizeDelta = new Vector2(10f, 6f);

            // 라벨
            GameObject textObj = new GameObject("HammerLabel");
            textObj.transform.SetParent(btnObj.transform, false);
            var label = textObj.AddComponent<Text>();
            label.text = "망치";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0f);
            textRt.anchoredPosition = new Vector2(0f, 8f);
            textRt.sizeDelta = new Vector2(0f, 16f);

            // 최상위 렌더링
            btnObj.transform.SetAsLastSibling();

            // HammerItem 컴포넌트 추가 + 필드 직접 설정
            var hammer = btnObj.AddComponent<HammerItem>();
            var type = typeof(HammerItem);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("hammerButton", flags)?.SetValue(hammer, btn);
            type.GetField("backgroundOverlay", flags)?.SetValue(hammer, overlay);

            hudElements.Add(btnObj);
            // overlayObj는 hudElements에 추가하지 않음 (HideLobby에서 SetActive(true) 방지)
            Debug.Log("[GameManager] HammerItem UI 직접 생성 완료");
        }

        /// <summary>
        /// 스왑 아이템 UI 생성 (우측 하단, 망치 버튼 위)
        /// </summary>
        private void CreateSwapUI(Canvas canvas)
        {
            // 배경 오버레이 (활성화 시 표시)
            GameObject overlayObj = new GameObject("SwapOverlay");
            overlayObj.transform.SetParent(canvas.transform, false);
            var overlay = overlayObj.AddComponent<Image>();
            overlay.color = new Color(0.4f, 0.7f, 1f, 0.15f);
            overlay.raycastTarget = false;
            RectTransform overlayRt = overlayObj.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            overlayObj.SetActive(false);

            // 스왑 버튼 — 망치 버튼 위에 배치
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float rightmostX = hSize * 1.5f * 5f;
            float lowestY = hSize * Mathf.Sqrt(3f) * (-5f);

            GameObject btnObj = new GameObject("SwapButton");
            btnObj.transform.SetParent(canvas.transform, false);
            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(rightmostX, lowestY + 90f);
            btnRt.sizeDelta = new Vector2(80f, 80f);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.25f, 0.25f, 0.35f, 0.9f);
            var btn = btnObj.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.highlightedColor = new Color(0.4f, 0.7f, 1f, 1f);
            btnColors.pressedColor = new Color(0.3f, 0.5f, 0.8f, 1f);
            btn.colors = btnColors;

            // 양방향 화살표 아이콘 (↔) 프로시저럴 생성
            // 수평 바
            GameObject barObj = new GameObject("SwapBar");
            barObj.transform.SetParent(btnObj.transform, false);
            var barImg = barObj.AddComponent<Image>();
            barImg.color = Color.white;
            barImg.raycastTarget = false;
            RectTransform barRt = barObj.GetComponent<RectTransform>();
            barRt.anchoredPosition = new Vector2(0f, 2f);
            barRt.sizeDelta = new Vector2(30f, 4f);

            // 왼쪽 화살표 머리 (< 형태)
            GameObject leftArrow1 = new GameObject("LeftArrow1");
            leftArrow1.transform.SetParent(btnObj.transform, false);
            var la1Img = leftArrow1.AddComponent<Image>();
            la1Img.color = Color.white;
            la1Img.raycastTarget = false;
            RectTransform la1Rt = leftArrow1.GetComponent<RectTransform>();
            la1Rt.anchoredPosition = new Vector2(-14f, 7f);
            la1Rt.sizeDelta = new Vector2(12f, 4f);
            la1Rt.localRotation = Quaternion.Euler(0, 0, 40f);

            GameObject leftArrow2 = new GameObject("LeftArrow2");
            leftArrow2.transform.SetParent(btnObj.transform, false);
            var la2Img = leftArrow2.AddComponent<Image>();
            la2Img.color = Color.white;
            la2Img.raycastTarget = false;
            RectTransform la2Rt = leftArrow2.GetComponent<RectTransform>();
            la2Rt.anchoredPosition = new Vector2(-14f, -3f);
            la2Rt.sizeDelta = new Vector2(12f, 4f);
            la2Rt.localRotation = Quaternion.Euler(0, 0, -40f);

            // 오른쪽 화살표 머리 (> 형태)
            GameObject rightArrow1 = new GameObject("RightArrow1");
            rightArrow1.transform.SetParent(btnObj.transform, false);
            var ra1Img = rightArrow1.AddComponent<Image>();
            ra1Img.color = Color.white;
            ra1Img.raycastTarget = false;
            RectTransform ra1Rt = rightArrow1.GetComponent<RectTransform>();
            ra1Rt.anchoredPosition = new Vector2(14f, 7f);
            ra1Rt.sizeDelta = new Vector2(12f, 4f);
            ra1Rt.localRotation = Quaternion.Euler(0, 0, -40f);

            GameObject rightArrow2 = new GameObject("RightArrow2");
            rightArrow2.transform.SetParent(btnObj.transform, false);
            var ra2Img = rightArrow2.AddComponent<Image>();
            ra2Img.color = Color.white;
            ra2Img.raycastTarget = false;
            RectTransform ra2Rt = rightArrow2.GetComponent<RectTransform>();
            ra2Rt.anchoredPosition = new Vector2(14f, -3f);
            ra2Rt.sizeDelta = new Vector2(12f, 4f);
            ra2Rt.localRotation = Quaternion.Euler(0, 0, 40f);

            // 라벨
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject textObj = new GameObject("SwapLabel");
            textObj.transform.SetParent(btnObj.transform, false);
            var label = textObj.AddComponent<Text>();
            label.text = "스왑";
            label.font = font;
            label.fontSize = 12;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 0f);
            textRt.anchoredPosition = new Vector2(0f, 8f);
            textRt.sizeDelta = new Vector2(0f, 16f);

            // 최상위 렌더링
            btnObj.transform.SetAsLastSibling();

            // SwapItem 컴포넌트 추가 + 필드 직접 설정
            var swap = btnObj.AddComponent<JewelsHexaPuzzle.Items.SwapItem>();
            var type = typeof(JewelsHexaPuzzle.Items.SwapItem);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            type.GetField("swapButton", flags)?.SetValue(swap, btn);
            type.GetField("backgroundOverlay", flags)?.SetValue(swap, overlay);

            hudElements.Add(btnObj);
            // overlayObj는 hudElements에 추가하지 않음 (HideLobby에서 SetActive(true) 방지)
            Debug.Log("[GameManager] SwapItem UI 직접 생성 완료");
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

                if (blockRemovalSystem != null)
                {
                    blockRemovalSystem.TriggerStartDrop();
                    while (blockRemovalSystem.IsProcessing)
                        yield return null;
                }
            }

            // 점수 및 골드 리셋 (게임 시작 시마다)
            if (scoreManager != null)
                scoreManager.ResetScore();
            currentGold = 0;

            UpdateUI();

            // 직접 참조 강제 동기화
            if (hudScoreText != null) hudScoreText.text = "0";
            if (hudTurnText != null) hudTurnText.text = currentTurns.ToString();

            // 골드 UI 동기화
            if (uiManager != null)
                uiManager.UpdateGoldDisplay(currentGold);

            // Stage 모드 미션 시스템 이벤트 구독 (currentGameMode가 이미 설정된 후)
            Debug.Log($"[GameManager] StartGameCoroutine: currentGameMode={currentGameMode}");
            Debug.Log($"[GameManager] stageManager != null: {stageManager != null}");
            Debug.Log($"[GameManager] blockRemovalSystem != null: {blockRemovalSystem != null}");
            if (currentGameMode == GameMode.Stage && stageManager != null && blockRemovalSystem != null)
            {
                Debug.Log($"[GameManager] Stage mode conditions met, subscribing...");
                blockRemovalSystem.OnGemsRemovedDetailed += HandleStageGemsRemoved;
                Debug.Log($"[GameManager] Stage mode: OnGemsRemovedDetailed subscription SUCCESS!");
            }
            else
            {
                Debug.LogError($"[GameManager] Stage mode subscription FAILED! Mode check: {currentGameMode == GameMode.Stage}");
            }

            // 게임 중 미션 UI 표시
            if (uiManager != null && stageManager != null && stageManager.CurrentStageData != null)
            {
                Canvas canvas = FindObjectOfType<Canvas>();
                if (canvas != null && stageManager.CurrentStageData.missions.Length > 0)
                {
                    var missions = stageManager.CurrentStageData.missions;

                    // 복수 미션이면 배열 오버로드 사용, 단일이면 기존 방식
                    uiManager.CreateGameMissionUI(canvas, missions);

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

            // 무한모드: 미션 UI 생성 + 생존 모드 시작
            if (currentGameMode == GameMode.Infinite && missionSystem != null)
            {
                if (uiManager != null)
                {
                    Canvas canvas = FindObjectOfType<Canvas>();
                    if (canvas != null)
                        uiManager.CreateSurvivalMissionUI(canvas);
                }

                // InfiniteConfig가 있으면 커스텀 설정으로 시작
                if (selectedLevelData?.infiniteConfig != null)
                    missionSystem.StartSurvival(selectedLevelData.infiniteConfig);
                else
                    missionSystem.StartSurvival();
            }

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
                scoreManager.AddMatchScore(blockCount, cascadeDepth, avgPosition);

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

                // 복수 미션 UI 카운트 업데이트
                if (UIManager.gameMissionCountTexts.Count > idx)
                {
                    Text countText = UIManager.gameMissionCountTexts[idx];
                    if (countText != null && lastDisplayedCounts != null && idx < lastDisplayedCounts.Length && remaining != lastDisplayedCounts[idx])
                    {
                        Debug.Log($"[GameManager] Mission[{idx}] count update: {lastDisplayedCounts[idx]} → {remaining}");
                        StartCoroutine(CountDownCoroutine(countText, remaining, lastDisplayedCounts[idx]));
                        lastDisplayedCounts[idx] = remaining;
                    }
                }
                else if (idx == 0 && UIManager.gameMissionCountText != null)
                {
                    // 단일 미션 하위호환
                    if (lastDisplayedCounts != null && remaining != lastDisplayedCounts[0])
                    {
                        Debug.Log($"[GameManager] Mission count update: {lastDisplayedCounts[0]} → {remaining}");
                        StartCoroutine(CountDownCoroutine(UIManager.gameMissionCountText, remaining, lastDisplayedCounts[0]));
                        lastDisplayedCounts[0] = remaining;
                    }
                }
            }

            // 블록 수집 이펙트 (컨테이너 위치로)
            RectTransform flyTarget = UIManager.gameMissionContainerRect ?? UIManager.gameMissionIconRect;
            Canvas canvas = FindObjectOfType<Canvas>();
            if (flyTarget != null && canvas != null)
            {
                StartCoroutine(BlockFlyEffectCoroutine(flyTarget.anchoredPosition, canvas));
            }
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
                blockRemovalSystem.OnGemsRemovedDetailed -= HandleStageGemsRemoved;
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
            if (currentGameMode != GameMode.Stage) return;
            if (gemCounts == null || gemCounts.Count == 0) return;

            int total = 0;
            foreach (var kvp in gemCounts)
                total += kvp.Value;

            Debug.Log($"[GameManager] 💣 특수블록 파괴(색상별): {specialBlockType}로 총 {total}개 제거");

            if (stageManager != null)
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
                Debug.Log("[GameManager] OnCascadeComplete skipped - ProcessSpecialBlockAftermath is managing state");
                return;
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
            if (enemySystem != null)
                enemySystem.OnTurnEnd();

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
                StartCoroutine(SpawnEnemiesViaSystemAndCheckMoves(enemyCount));
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
            StartCoroutine(SpawnEnemiesViaSystemAndPlay());
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

            if (isProcessingChainDrill) return;

            // BlockRemovalSystem 내부 cascade에서 특수블록이 발동된 경우
            // 이미 내부적으로 연쇄 처리 중이므로 GameManager가 개입하면 데드락 발생
            if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) return;

            // 유저 직접 클릭으로 발동된 경우 (턴 차감은 InputSystem에서 발동 시작 시 처리)
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

private IEnumerator ProcessSpecialBlockAftermath()
        {
            yield return new WaitForSeconds(0.1f);
            isProcessingChainDrill = true;

            int maxLoops = 20;
            int loop = 0;

            while (loop < maxLoops)
            {
                loop++;

                // 루프 진행 시 stuck 타이머 리셋
                processingStartTime = Time.time;

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

            // 적군 턴 종료 처리
            if (enemySystem != null)
                enemySystem.OnTurnEnd();

            // 무한모드: 미션 턴 종료 → 게임오버 체크 → 적군 스폰
            if (currentGameMode == GameMode.Infinite)
            {
                if (missionSystem != null)
                    missionSystem.OnTurnEnd();

                if (currentTurns <= 0)
                {
                    GameOver();
                    yield break;
                }

                int enemyCount = 3 + (rotationCount / 10);
                yield return StartCoroutine(SpawnEnemiesViaSystemAndCheckMoves(enemyCount));
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
            yield return StartCoroutine(SpawnEnemiesViaSystemAndPlay());
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

            // stuck 타이머 리셋 (특수 블록 발동 진행 중이므로 stuck 아님)
            processingStartTime = Time.time;

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

                case SpecialBlockType.Laser:
                    if (laserSystem != null)
                    {
                        laserSystem.ActivateLaser(block);
                        yield return new WaitForSeconds(0.1f);
                        waited = 0f;
                        while (laserSystem.IsBlockActive(block) && waited < timeout) { waited += Time.deltaTime; processingStartTime = Time.time; yield return null; }
                        if (laserSystem.IsBlockActive(block)) { Debug.LogError("[GM] Laser timeout!"); laserSystem.ForceReset(); }
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
            if (laserSystem != null) laserSystem.ForceReset();

            SetGameState(GameState.StageClear);
            processingStartTime = Time.time; // StageClear 워치독 타이머 시작
            OnStageClear?.Invoke();

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
                if (laserSystem != null && laserSystem.IsActivating) anyActive = true;
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
                   type == SpecialBlockType.Laser;
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
            List<HexBlock> laserBlocks = new List<HexBlock>();

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
                else if (block.Data.IsLaser())
                    laserBlocks.Add(block);
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

            // 레이저 발동
            foreach (var block in laserBlocks)
            {
                if (laserSystem != null)
                    activationCoroutines.Add(StartCoroutine(ActivateLaserBlock(block)));
            }

            // 모든 발동 완료 대기
            foreach (var coroutine in activationCoroutines)
            {
                yield return coroutine;
            }

            Debug.Log($"[GameManager] 특수 블록 발동 완료: 드릴({drillBlocks.Count}), 폭탄({bombBlocks.Count}), 무지개({donutBlocks.Count}), X({xBlocks.Count}), 레이저({laserBlocks.Count})");
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
        /// 레이저 블록 발동
        /// </summary>
        private IEnumerator ActivateLaserBlock(HexBlock block)
        {
            if (laserSystem != null)
            {
                laserSystem.ActivateLaser(block);
                float waited = 0f;
                while (laserSystem.IsActivating && waited < 5f)
                {
                    waited += Time.deltaTime;
                    processingStartTime = Time.time;
                    yield return null;
                }
                if (laserSystem.IsActivating)
                {
                    Debug.LogWarning("[GameManager] ActivateLaserBlock timeout! ForceReset.");
                    laserSystem.ForceReset();
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

        private void OnSurvivalMissionCompleted(SurvivalMission mission, int reward)
        {
            AddTurns(reward);
            if (uiManager != null)
                uiManager.AnimateMissionComplete(reward);

            // 1.2초 후 다음 미션 배정
            StartCoroutine(AssignNextMissionDelayed());
        }

        private IEnumerator AssignNextMissionDelayed()
        {
            yield return new WaitForSeconds(1.2f);
            if (missionSystem != null)
                missionSystem.AssignNextMission();
        }

        private void OnSurvivalMissionAssigned(SurvivalMission mission)
        {
            if (uiManager != null)
                uiManager.ShowNewMission(mission);
        }

        private void OnSurvivalMissionProgressChanged(SurvivalMission mission)
        {
            if (uiManager != null)
                uiManager.UpdateSurvivalMissionProgress(mission);
        }

        /// <summary>
        /// 게임 오버
        /// </summary>
        private void GameOver()
        {
            SetGameState(GameState.GameOver);
            OnGameOver?.Invoke();

            // 랭킹에 점수 + 이동횟수 저장
            if (scoreManager != null)
                scoreManager.SaveScoreToRanking(scoreManager.CurrentScore, rotationCount);

            // BGM 정지 + 게임 오버 사운드
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBGM();
                AudioManager.Instance.PlayGameOver();
            }

            Debug.Log("Game Over!");

            // 게임오버 팝업 표시 + 카운팅 애니메이션
            if (gameOverScoreText != null)
                gameOverScoreText.text = "0";
            if (gameOverMovesText != null)
                gameOverMovesText.text = "MOVES: 0";

            if (uiManager != null)
                uiManager.ShowGameOverPopup();

            StartCoroutine(AnimateGameOverPopup());
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
        /// 로비 UI 프로시저럴 생성
        /// </summary>
        private void CreateLobbyUI(Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // LevelRegistry 초기화
            LevelRegistry.Initialize();
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

            // === 보유 골드 표시 (우측 상단) ===
            GameObject lobbyGoldObj = new GameObject("LobbyGold");
            lobbyGoldObj.transform.SetParent(lobbyContainer.transform, false);
            RectTransform lobbyGoldRt = lobbyGoldObj.AddComponent<RectTransform>();
            lobbyGoldRt.anchorMin = new Vector2(1f, 1f);
            lobbyGoldRt.anchorMax = new Vector2(1f, 1f);
            lobbyGoldRt.pivot = new Vector2(1f, 1f);
            lobbyGoldRt.anchoredPosition = new Vector2(-30f, -40f);
            lobbyGoldRt.sizeDelta = new Vector2(250f, 40f);

            lobbyGoldText = lobbyGoldObj.AddComponent<Text>();
            lobbyGoldText.font = font;
            lobbyGoldText.fontSize = 28;
            lobbyGoldText.alignment = TextAnchor.MiddleRight;
            lobbyGoldText.color = new Color(1f, 0.84f, 0f);
            lobbyGoldText.raycastTarget = false;
            lobbyGoldText.text = $"\ud83d\udcb0 {currentGold:N0}";

            Outline lobbyGoldOutline = lobbyGoldObj.AddComponent<Outline>();
            lobbyGoldOutline.effectColor = new Color(0, 0, 0, 0.8f);
            lobbyGoldOutline.effectDistance = new Vector2(1, 1);

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

            lobbyContainer.SetActive(false);
            lobbyContainer.transform.SetAsLastSibling();
            Debug.Log($"[GameManager] 로비 UI 생성 완료 (레벨 {totalLevels}개)");
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
                // 재생 아이콘 (잠긴 레벨에서는 숨김)
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

            // 부제 텍스트
            GameObject subtitleObj = new GameObject("Subtitle");
            subtitleObj.transform.SetParent(stageBtn.transform, false);
            RectTransform subtitleRt = subtitleObj.AddComponent<RectTransform>();
            subtitleRt.anchorMin = new Vector2(0.5f, 0.5f);
            subtitleRt.anchorMax = new Vector2(0.5f, 0.5f);
            subtitleRt.pivot = new Vector2(0.5f, 0.5f);
            subtitleRt.anchoredPosition = new Vector2(0f, -40f);
            subtitleRt.sizeDelta = new Vector2(btnSize * 0.95f, 24f);
            Text subtitleText = subtitleObj.AddComponent<Text>();
            subtitleText.font = font;
            subtitleText.fontSize = 11;
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

            // 로비 골드 표시 갱신
            if (lobbyGoldText != null)
                lobbyGoldText.text = $"\ud83d\udcb0 {currentGold:N0}";

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
                    st == SpecialBlockType.Laser)
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
        private IEnumerator CountDownCoroutine(Text countText, int to, int from)
        {
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                int current = Mathf.RoundToInt(Mathf.Lerp(from, to, progress));
                countText.text = current.ToString();
                yield return null;
            }

            countText.text = to.ToString();
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
                blockRemovalSystem.OnGemsRemovedDetailed -= HandleStageGemsRemoved;
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

            // 미션 이벤트 정리
            if (stageManager != null)
            {
                stageManager.OnMissionProgressUpdated -= HandleMissionProgressUpdated;
                stageManager.OnMissionProgressUpdated -= HandleInfiniteMissionProgressUpdated;
                stageManager.OnMissionComplete -= HandleMissionComplete;
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
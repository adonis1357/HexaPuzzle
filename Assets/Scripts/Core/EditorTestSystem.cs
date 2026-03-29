using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Items;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 에디터 테스트용 특수 블록 설치 시스템 (Canvas UI 기반)
    /// 좌측 하단에 육각형 버튼 패널 표시, 토글로 블록 설치
    /// + 색상 변경 버튼 (기본 블록 색상 교체)
    ///
    /// 입력 처리는 InputSystem이 담당 (이 클래스에 Update 없음)
    /// </summary>
    public class EditorTestSystem : MonoBehaviour
    {
        public static EditorTestSystem Instance { get; private set; }

        private HexGrid hexGrid;

        // 에디터 모드 상태
        private bool editorMode = false;
        private SpecialBlockType activeBlockType = SpecialBlockType.None;

        // 색상 모드 상태
        private bool colorMode = false;
        private GemType activeGemType = GemType.None;

        // 몬스터 모드 상태
        private bool monsterMode = false;
        private GameObject monsterButton;
        private Image monsterButtonBg;
        private Image monsterButtonOutline;

        // 게이지 추가 모드 상태
        public bool GaugeAddMode => gaugeAddMode;
        private bool gaugeAddMode = false;
        private GameObject gaugeAddButton;
        private Image gaugeAddButtonBg;
        private Image gaugeAddButtonOutline;

        // Canvas UI 참조 — 특수 블록 버튼
        private GameObject panelContainer;
        private GameObject[] specialBlockButtons;
        private Image[] buttonBackgrounds;
        private Image[] buttonOutlines;

        // Canvas UI 참조 — 색상 변경 버튼
        private GameObject[] colorBlockButtons;
        private Image[] colorButtonBackgrounds;
        private Image[] colorButtonOutlines;
        private int activeColorButtonIndex = -1;

        // 특수 블록 타입 배열 (드릴 3방향 + 드론 포함, 6개)
        private static readonly SpecialBlockType[] specialBlockTypes = new SpecialBlockType[]
        {
            SpecialBlockType.Bomb,
            SpecialBlockType.Drill,      // Vertical
            SpecialBlockType.Drill,      // Slash
            SpecialBlockType.Drill,      // BackSlash
            SpecialBlockType.XBlock,
            SpecialBlockType.Drone
        };

        // 드릴 방향 매핑
        private static readonly DrillDirection[] drillDirections = new DrillDirection[]
        {
            DrillDirection.Vertical,    // index 0 (Bomb - 미사용)
            DrillDirection.Vertical,    // index 1
            DrillDirection.Slash,       // index 2
            DrillDirection.BackSlash,   // index 3
            DrillDirection.Vertical,    // index 4 (XBlock - 미사용)
            DrillDirection.Vertical     // index 5 (Drone - 미사용)
        };

        private static readonly string[] buttonLabels = new string[]
        {
            "폭탄", "드릴↕", "드릴╱", "드릴╲", "엑스", "드론"
        };

        private static readonly Color[] buttonColors = new Color[]
        {
            new Color(0.75f, 0.30f, 0.20f, 0.90f),
            new Color(0.25f, 0.55f, 0.75f, 0.90f),
            new Color(0.25f, 0.55f, 0.75f, 0.90f),
            new Color(0.25f, 0.55f, 0.75f, 0.90f),
            new Color(0.80f, 0.65f, 0.20f, 0.90f),
            new Color(0.40f, 0.75f, 0.70f, 0.90f)
        };

        // 색상 변경 버튼 데이터
        private static readonly GemType[] colorGemTypes = new GemType[]
        {
            GemType.Red,
            GemType.Blue,
            GemType.Green,
            GemType.Yellow,
            GemType.Purple,
            GemType.Orange
        };

        private static readonly string[] colorButtonLabels = new string[]
        {
            "빨강", "파랑", "초록", "노랑", "보라", "주황"
        };

        private static readonly Color INACTIVE_BORDER = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color ACTIVE_BORDER = new Color(0.3f, 1f, 0.3f, 0.9f);

        private const float TEST_BTN_SIZE = 70f;
        private const float TEST_BTN_GAP = 4f;
        private const int BUTTONS_PER_COL = 3;

        private const float COLOR_BTN_SIZE = 55f;
        private const float COLOR_BTN_GAP = 3f;
        private const int COLOR_BUTTONS_PER_COL = 3;

        private bool isInitialized = false;
        private DrillDirection activeDrillDirection = DrillDirection.Vertical;
        private int activeButtonIndex = -1;

        /// <summary>
        /// 모드 전환이 발생한 프레임 번호.
        /// InputSystem은 이 프레임에서 입력을 무시해야 함.
        /// </summary>
        public int LastModeChangeFrame { get; private set; } = -1;

        // ============================================================
        // 초기화
        // ============================================================

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        public void InitializeUI(Canvas canvas, HexGrid grid)
        {
            if (isInitialized) return;
            hexGrid = grid;
            if (hexGrid == null)
                hexGrid = FindObjectOfType<HexGrid>();

            CreateTestButtonPanel(canvas);
            isInitialized = true;

            if (panelContainer != null)
                panelContainer.SetActive(false);

            Debug.Log("[EditorTestSystem] UI 초기화 완료");
        }

        public void ShowPanel(bool show)
        {
            if (panelContainer != null)
                panelContainer.SetActive(show);
            if (!show)
                DeactivateMode();
        }

        // ============================================================
        // 패널 생성
        // ============================================================

        private void CreateTestButtonPanel(Canvas canvas)
        {
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float leftmostX = -(hSize * 1.5f * 5f);
            float lowestY = hSize * Mathf.Sqrt(3f) * (-5f);

            panelContainer = new GameObject("TestBlockPanel");
            panelContainer.transform.SetParent(canvas.transform, false);
            RectTransform panelRt = panelContainer.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(400f, 400f);

            // === 특수 블록 버튼 생성 ===
            int totalButtons = specialBlockTypes.Length;
            specialBlockButtons = new GameObject[totalButtons];
            buttonBackgrounds = new Image[totalButtons];
            buttonOutlines = new Image[totalButtons];

            float sqrt3 = Mathf.Sqrt(3f);
            float btnHexH = TEST_BTN_SIZE * sqrt3 / 2f;

            for (int i = 0; i < totalButtons; i++)
            {
                int col = i / BUTTONS_PER_COL;
                int row = i % BUTTONS_PER_COL;

                float x = leftmostX + col * (TEST_BTN_SIZE * 0.75f + TEST_BTN_GAP);
                float y = lowestY - 70f - row * (btnHexH + TEST_BTN_GAP);
                if (col % 2 == 1)
                    y -= (btnHexH + TEST_BTN_GAP) / 2f;

                CreateSpecialBlockButton(i, new Vector2(x, y));
            }

            // === 색상 변경 버튼 생성 (특수 블록 버튼 아래) ===
            CreateColorBlockButtons(leftmostX, lowestY, btnHexH);

            // === 몬스터 배치 버튼 생성 (색상 버튼 아래) ===
            CreateMonsterButton(leftmostX, lowestY, btnHexH);

            // === 게이지 추가 버튼 생성 (몬스터 버튼 아래) ===
            CreateGaugeAddButton(leftmostX, lowestY, btnHexH);
        }

        private void CreateSpecialBlockButton(int index, Vector2 position)
        {
            string btnName = $"TestBtn_{index}_{buttonLabels[index]}";
            GameObject btnObj = new GameObject(btnName);
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = position;
            btnRt.sizeDelta = new Vector2(TEST_BTN_SIZE, TEST_BTN_SIZE);

            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = buttonColors[index];

            GameObject outlineObj = new GameObject("Outline");
            outlineObj.transform.SetParent(btnObj.transform, false);
            RectTransform outRt = outlineObj.AddComponent<RectTransform>();
            outRt.anchorMin = Vector2.zero; outRt.anchorMax = Vector2.one;
            outRt.offsetMin = Vector2.zero; outRt.offsetMax = Vector2.zero;
            Image outImg = outlineObj.AddComponent<Image>();
            outImg.sprite = HexBlock.GetHexBorderSprite();
            outImg.type = Image.Type.Simple;
            outImg.preserveAspect = true;
            outImg.color = INACTIVE_BORDER;
            outImg.raycastTarget = false;

            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(btnObj.transform, false);
            RectTransform iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.15f, 0.15f);
            iconRt.anchorMax = new Vector2(0.85f, 0.85f);
            iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.raycastTarget = false;
            iconImg.preserveAspect = true;

            Sprite iconSprite = GetIconSpriteForType(specialBlockTypes[index], index);
            if (iconSprite != null)
            {
                iconImg.sprite = iconSprite;
                iconImg.color = Color.white;
            }
            else
            {
                iconImg.color = Color.clear;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = buttonLabels[index];
            label.font = font;
            label.fontSize = 11;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 10f);
            labelRt.sizeDelta = new Vector2(0f, 16f);

            Button btn = btnObj.AddComponent<Button>();
            var bc = btn.colors;
            bc.normalColor = Color.white;
            bc.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            bc.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = bc;

            int capturedIndex = index;
            btn.onClick.AddListener(() => OnTestButtonClicked(capturedIndex));

            specialBlockButtons[index] = btnObj;
            buttonBackgrounds[index] = bgImage;
            buttonOutlines[index] = outImg;
        }

        // ============================================================
        // 색상 변경 버튼 생성
        // ============================================================

        private void CreateColorBlockButtons(float leftmostX, float lowestY, float specialBtnHexH)
        {
            int totalColorButtons = colorGemTypes.Length;
            colorBlockButtons = new GameObject[totalColorButtons];
            colorButtonBackgrounds = new Image[totalColorButtons];
            colorButtonOutlines = new Image[totalColorButtons];

            float sqrt3 = Mathf.Sqrt(3f);
            float colorBtnHexH = COLOR_BTN_SIZE * sqrt3 / 2f;

            // 특수 블록 버튼 영역의 최하단 Y 계산
            int specialCols = (specialBlockTypes.Length + BUTTONS_PER_COL - 1) / BUTTONS_PER_COL;
            float specialBottomY = lowestY - 70f - (BUTTONS_PER_COL - 1) * (specialBtnHexH + TEST_BTN_GAP)
                                   - (specialBtnHexH + TEST_BTN_GAP) / 2f; // 홀수 열 오프셋 고려
            float colorStartY = specialBottomY - 35f; // 특수 버튼 아래 35px 간격 (10px 하향)

            for (int i = 0; i < totalColorButtons; i++)
            {
                int col = i / COLOR_BUTTONS_PER_COL;
                int row = i % COLOR_BUTTONS_PER_COL;

                float x = leftmostX + col * (COLOR_BTN_SIZE * 0.75f + COLOR_BTN_GAP);
                float y = colorStartY - row * (colorBtnHexH + COLOR_BTN_GAP);
                if (col % 2 == 1)
                    y -= (colorBtnHexH + COLOR_BTN_GAP) / 2f;

                CreateColorButton(i, new Vector2(x, y));
            }
        }

        private void CreateColorButton(int index, Vector2 position)
        {
            GemType gemType = colorGemTypes[index];
            Color gemColor = GemColors.GetColor(gemType);

            // 버튼 배경색: 젬 색상을 약간 어둡게 (0.7 밝기, 0.9 알파)
            Color btnBgColor = new Color(gemColor.r * 0.7f, gemColor.g * 0.7f, gemColor.b * 0.7f, 0.90f);

            string btnName = $"TestBtn_Color_{index}_{colorButtonLabels[index]}";
            GameObject btnObj = new GameObject(btnName);
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = position;
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            // 배경 (육각형)
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = btnBgColor;

            // 테두리
            GameObject outlineObj = new GameObject("Outline");
            outlineObj.transform.SetParent(btnObj.transform, false);
            RectTransform outRt = outlineObj.AddComponent<RectTransform>();
            outRt.anchorMin = Vector2.zero; outRt.anchorMax = Vector2.one;
            outRt.offsetMin = Vector2.zero; outRt.offsetMax = Vector2.zero;
            Image outImg = outlineObj.AddComponent<Image>();
            outImg.sprite = HexBlock.GetHexBorderSprite();
            outImg.type = Image.Type.Simple;
            outImg.preserveAspect = true;
            outImg.color = INACTIVE_BORDER;
            outImg.raycastTarget = false;

            // 중앙 컬러 도트 (보석 색상 그대로)
            GameObject dotObj = new GameObject("ColorDot");
            dotObj.transform.SetParent(btnObj.transform, false);
            RectTransform dotRt = dotObj.AddComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0.25f, 0.25f);
            dotRt.anchorMax = new Vector2(0.75f, 0.75f);
            dotRt.offsetMin = Vector2.zero; dotRt.offsetMax = Vector2.zero;
            Image dotImg = dotObj.AddComponent<Image>();
            dotImg.sprite = HexBlock.GetHexFlashSprite();
            dotImg.type = Image.Type.Simple;
            dotImg.preserveAspect = true;
            dotImg.color = gemColor;
            dotImg.raycastTarget = false;

            // 라벨 텍스트
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = colorButtonLabels[index];
            label.font = font;
            label.fontSize = 10;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 8f);
            labelRt.sizeDelta = new Vector2(0f, 14f);

            // Button 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var bc = btn.colors;
            bc.normalColor = Color.white;
            bc.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            bc.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = bc;

            int capturedIndex = index;
            btn.onClick.AddListener(() => OnColorButtonClicked(capturedIndex));

            colorBlockButtons[index] = btnObj;
            colorButtonBackgrounds[index] = bgImage;
            colorButtonOutlines[index] = outImg;
        }

        // ============================================================
        // 몬스터 배치 버튼 생성
        // ============================================================

        private void CreateMonsterButton(float leftmostX, float lowestY, float specialBtnHexH)
        {
            float hSize = hexGrid != null ? hexGrid.HexSize : 50f;
            float sqrt3 = Mathf.Sqrt(3f);

            // 골드 추가 버튼과 동일한 좌표 계산 (GameManager.CreateGoldAddButton 참조)
            float goldBtnSize = 77f;
            float gridLeft = -(hSize * 1.5f * 5f);
            float goldX = gridLeft / 2f - 80f + 15f;
            float goldBtnHexH = goldBtnSize * sqrt3 / 2f;
            float goldY = hSize * sqrt3 * (-5f) - goldBtnHexH * 0.3f - 5f - 100f;

            // 골드 버튼 바로 아래에 배치 (간격 8px)
            float monsterBtnX = goldX;
            float monsterBtnY = goldY - goldBtnHexH / 2f - 8f - COLOR_BTN_SIZE * sqrt3 / 4f;

            // panelContainer 자식으로 배치 (hudElements 통해 표시/숨김 관리됨)
            string btnName = "TestBtn_Monster";
            GameObject btnObj = new GameObject(btnName);
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(monsterBtnX, monsterBtnY);
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            // 배경 (육각형) — 비활성 시 보라 계열
            Color monsterInactiveColor = new Color(0.55f, 0.30f, 0.65f, 0.90f);
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = monsterInactiveColor;

            // 테두리
            GameObject outlineObj = new GameObject("Outline");
            outlineObj.transform.SetParent(btnObj.transform, false);
            RectTransform outRt = outlineObj.AddComponent<RectTransform>();
            outRt.anchorMin = Vector2.zero; outRt.anchorMax = Vector2.one;
            outRt.offsetMin = Vector2.zero; outRt.offsetMax = Vector2.zero;
            Image outImg = outlineObj.AddComponent<Image>();
            outImg.sprite = HexBlock.GetHexBorderSprite();
            outImg.type = Image.Type.Simple;
            outImg.preserveAspect = true;
            outImg.color = INACTIVE_BORDER;
            outImg.raycastTarget = false;

            // 라벨 텍스트
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = "몬스터";
            label.font = font;
            label.fontSize = 11;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 10f);
            labelRt.sizeDelta = new Vector2(0f, 16f);

            // Button 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var bc = btn.colors;
            bc.normalColor = Color.white;
            bc.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            bc.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = bc;
            btn.onClick.AddListener(OnMonsterButtonClicked);

            monsterButton = btnObj;
            monsterButtonBg = bgImage;
            monsterButtonOutline = outImg;
        }

        // ============================================================
        // 게이지 추가 버튼 생성
        // ============================================================

        private void CreateGaugeAddButton(float leftmostX, float lowestY, float specialBtnHexH)
        {
            if (monsterButton == null) return;

            // 몬스터 버튼 바로 아래에 배치 (간격 8px)
            float sqrt3 = Mathf.Sqrt(3f);
            RectTransform monsterRt = monsterButton.GetComponent<RectTransform>();
            Vector2 monsterPos = monsterRt.anchoredPosition;
            float btnHalfH = COLOR_BTN_SIZE * sqrt3 / 4f;
            float gaugeBtnX = monsterPos.x;
            float gaugeBtnY = monsterPos.y - btnHalfH - 8f - btnHalfH;

            string btnName = "TestBtn_GaugeAdd";
            GameObject btnObj = new GameObject(btnName);
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(gaugeBtnX, gaugeBtnY);
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            // 배경 (육각형) — 비활성 시 청록 계열
            Color gaugeInactiveColor = new Color(0.20f, 0.55f, 0.55f, 0.90f);
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = gaugeInactiveColor;

            // 테두리
            GameObject outlineObj = new GameObject("Outline");
            outlineObj.transform.SetParent(btnObj.transform, false);
            RectTransform outRt = outlineObj.AddComponent<RectTransform>();
            outRt.anchorMin = Vector2.zero; outRt.anchorMax = Vector2.one;
            outRt.offsetMin = Vector2.zero; outRt.offsetMax = Vector2.zero;
            Image outImg = outlineObj.AddComponent<Image>();
            outImg.sprite = HexBlock.GetHexBorderSprite();
            outImg.type = Image.Type.Simple;
            outImg.preserveAspect = true;
            outImg.color = INACTIVE_BORDER;
            outImg.raycastTarget = false;

            // 라벨 텍스트
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = "게이지+";
            label.font = font;
            label.fontSize = 11;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 10f);
            labelRt.sizeDelta = new Vector2(0f, 16f);

            // Button 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var bc = btn.colors;
            bc.normalColor = Color.white;
            bc.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            bc.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = bc;
            btn.onClick.AddListener(OnGaugeAddButtonClicked);

            gaugeAddButton = btnObj;
            gaugeAddButtonBg = bgImage;
            gaugeAddButtonOutline = outImg;
        }

        // ============================================================
        // 게이지 추가 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnGaugeAddButtonClicked()
        {
            if (gaugeAddMode)
            {
                DeactivateMode();
                return;
            }

            // 다른 모드 해제
            editorMode = false;
            activeBlockType = SpecialBlockType.None;
            activeButtonIndex = -1;
            colorMode = false;
            activeGemType = GemType.None;
            activeColorButtonIndex = -1;
            monsterMode = false;

            gaugeAddMode = true;
            LastModeChangeFrame = Time.frameCount;

            UpdateAllButtonVisuals();
            Debug.Log($"[EditorTestSystem] 게이지 추가 모드 활성화 frame={Time.frameCount}");
        }

        /// <summary>
        /// 게이지 추가 모드에서 아이템 버튼 클릭 시 호출.
        /// 해당 아이템 게이지에 50 추가.
        /// </summary>
        public void AddGaugeToItem(string itemName)
        {
            if (!gaugeAddMode) return;

            switch (itemName.ToLower())
            {
                case "hammer":
                    if (HammerGauge.Instance != null)
                        HammerGauge.Instance.AddGaugeEditor(50);
                    Debug.Log("[EditorTestSystem] 망치 게이지 +50");
                    break;
                case "swap":
                    if (SwapGauge.Instance != null)
                        SwapGauge.Instance.AddGaugeEditor(50);
                    Debug.Log("[EditorTestSystem] 스왑 게이지 +50");
                    break;
                case "line":
                    if (LineGauge.Instance != null)
                        LineGauge.Instance.AddGaugeEditor(50);
                    Debug.Log("[EditorTestSystem] 라인 게이지 +50");
                    break;
            }
        }

        // ============================================================
        // 몬스터 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnMonsterButtonClicked()
        {
            // 이미 활성 → 비활성화
            if (monsterMode)
            {
                DeactivateMode();
                return;
            }

            // 다른 모드 해제
            editorMode = false;
            activeBlockType = SpecialBlockType.None;
            activeButtonIndex = -1;
            colorMode = false;
            activeGemType = GemType.None;
            activeColorButtonIndex = -1;
            gaugeAddMode = false;

            monsterMode = true;
            LastModeChangeFrame = Time.frameCount;

            UpdateAllButtonVisuals();
            Debug.Log($"[EditorTestSystem] 몬스터 모드 활성화 frame={Time.frameCount}");
        }

        // ============================================================
        // 몬스터 순환 배치 처리
        // ============================================================

        /// <summary>
        /// 몬스터 모드에서 블록 클릭 시 순환 배치.
        /// 순서: 몽둥이(1) → 갑옷(2) → 궁수(3) → 방패(4) → 제거(0) → 반복
        /// </summary>
        private bool TryPlaceMonster(HexBlock block)
        {
            if (block == null)
            {
                // 빈 공간 → 비활성화
                DeactivateMode();
                return true;
            }

            if (GoblinSystem.Instance == null)
            {
                Debug.LogWarning("[EditorTestSystem] GoblinSystem이 없어 몬스터를 배치할 수 없습니다.");
                return false;
            }

            HexCoord coord = block.Coord;
            int currentType = GoblinSystem.Instance.EditorGetGoblinType(coord);

            // 순환: 0→1(몽둥이) → 2(갑옷) → 3(궁수) → 4(방패) → 0(제거)
            int nextType = (currentType + 1) % 5;

            if (nextType == 0)
            {
                // 제거
                GoblinSystem.Instance.EditorRemoveGoblin(coord);
                Debug.Log($"[EditorTestSystem] 몬스터 제거: ({coord})");
            }
            else
            {
                bool isArmored = (nextType == 2);
                bool isArcher = (nextType == 3);
                bool isShield = (nextType == 4);
                GoblinSystem.Instance.EditorSpawnGoblin(coord, isArmored, isArcher, isShield);

                string typeName;
                switch (nextType)
                {
                    case 1: typeName = "몽둥이"; break;
                    case 2: typeName = "갑옷"; break;
                    case 3: typeName = "궁수"; break;
                    case 4: typeName = "방패"; break;
                    default: typeName = "???"; break;
                }
                Debug.Log($"[EditorTestSystem] 몬스터 배치: ({coord}) → {typeName}");
            }

            return true;
        }

        private Sprite GetIconSpriteForType(SpecialBlockType type, int index)
        {
            switch (type)
            {
                case SpecialBlockType.Bomb:
                    return BombBlockSystem.GetBombIconSprite();
                case SpecialBlockType.Drill:
                    if (index == 1) return HexBlock.GetDrillIconSprite(DrillDirection.Vertical);
                    if (index == 2) return HexBlock.GetDrillIconSprite(DrillDirection.Slash);
                    if (index == 3) return HexBlock.GetDrillIconSprite(DrillDirection.BackSlash);
                    return HexBlock.GetDrillIconSprite(DrillDirection.Vertical);
                case SpecialBlockType.XBlock:
                    return XBlockSystem.GetXBlockIconSprite();
                case SpecialBlockType.Drone:
                    return DroneBlockSystem.GetDroneIconSprite();
                default:
                    return null;
            }
        }

        // ============================================================
        // 특수 블록 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnTestButtonClicked(int index)
        {
            SpecialBlockType clickedType = specialBlockTypes[index];

            if (editorMode && activeButtonIndex == index)
            {
                DeactivateMode();
                return;
            }

            // 다른 모드가 활성 상태면 먼저 해제
            colorMode = false;
            activeGemType = GemType.None;
            activeColorButtonIndex = -1;
            monsterMode = false;
            gaugeAddMode = false;

            editorMode = true;
            activeBlockType = clickedType;
            activeButtonIndex = index;
            LastModeChangeFrame = Time.frameCount;

            if (clickedType == SpecialBlockType.Drill && index < drillDirections.Length)
                activeDrillDirection = drillDirections[index];

            UpdateAllButtonVisuals();
            Debug.Log($"[EditorTestSystem] 특수블록 활성화: {buttonLabels[index]} frame={Time.frameCount}");
        }

        // ============================================================
        // 색상 변경 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnColorButtonClicked(int index)
        {
            GemType clickedGem = colorGemTypes[index];

            // 이미 같은 색상 버튼 활성 → 비활성화
            if (colorMode && activeColorButtonIndex == index)
            {
                DeactivateMode();
                return;
            }

            // 다른 모드가 활성 상태면 먼저 해제
            editorMode = false;
            activeBlockType = SpecialBlockType.None;
            activeButtonIndex = -1;
            monsterMode = false;
            gaugeAddMode = false;

            colorMode = true;
            activeGemType = clickedGem;
            activeColorButtonIndex = index;
            LastModeChangeFrame = Time.frameCount;

            UpdateAllButtonVisuals();
            Debug.Log($"[EditorTestSystem] 색상 활성화: {colorButtonLabels[index]} ({clickedGem}) frame={Time.frameCount}");
        }

        // ============================================================
        // 버튼 시각 업데이트 (통합)
        // ============================================================

        private void UpdateAllButtonVisuals()
        {
            // 특수 블록 버튼 업데이트
            if (specialBlockButtons != null)
            {
                for (int i = 0; i < specialBlockButtons.Length; i++)
                {
                    if (specialBlockButtons[i] == null) continue;
                    bool isActive = (editorMode && i == activeButtonIndex);

                    if (buttonBackgrounds[i] != null)
                    {
                        buttonBackgrounds[i].color = isActive
                            ? new Color(
                                Mathf.Min(1f, buttonColors[i].r + 0.3f),
                                Mathf.Min(1f, buttonColors[i].g + 0.3f),
                                Mathf.Min(1f, buttonColors[i].b + 0.1f),
                                1f)
                            : buttonColors[i];
                    }

                    if (buttonOutlines[i] != null)
                        buttonOutlines[i].color = isActive ? ACTIVE_BORDER : INACTIVE_BORDER;

                    specialBlockButtons[i].transform.localScale = isActive
                        ? Vector3.one * 1.15f : Vector3.one;
                }
            }

            // 색상 변경 버튼 업데이트
            if (colorBlockButtons != null)
            {
                for (int i = 0; i < colorBlockButtons.Length; i++)
                {
                    if (colorBlockButtons[i] == null) continue;
                    bool isActive = (colorMode && i == activeColorButtonIndex);

                    Color gemColor = GemColors.GetColor(colorGemTypes[i]);
                    Color btnBgColor = new Color(gemColor.r * 0.7f, gemColor.g * 0.7f, gemColor.b * 0.7f, 0.90f);

                    if (colorButtonBackgrounds[i] != null)
                    {
                        colorButtonBackgrounds[i].color = isActive
                            ? new Color(
                                Mathf.Min(1f, gemColor.r + 0.1f),
                                Mathf.Min(1f, gemColor.g + 0.1f),
                                Mathf.Min(1f, gemColor.b + 0.1f),
                                1f)
                            : btnBgColor;
                    }

                    if (colorButtonOutlines[i] != null)
                        colorButtonOutlines[i].color = isActive ? ACTIVE_BORDER : INACTIVE_BORDER;

                    colorBlockButtons[i].transform.localScale = isActive
                        ? Vector3.one * 1.15f : Vector3.one;
                }
            }

            // 몬스터 버튼 업데이트
            if (monsterButton != null)
            {
                Color monsterInactiveColor = new Color(0.55f, 0.30f, 0.65f, 0.90f);
                Color monsterActiveColor = new Color(0.80f, 0.50f, 0.90f, 1f);

                if (monsterButtonBg != null)
                    monsterButtonBg.color = monsterMode ? monsterActiveColor : monsterInactiveColor;
                if (monsterButtonOutline != null)
                    monsterButtonOutline.color = monsterMode ? ACTIVE_BORDER : INACTIVE_BORDER;
                monsterButton.transform.localScale = monsterMode ? Vector3.one * 1.15f : Vector3.one;
            }

            // 게이지 추가 버튼 업데이트
            if (gaugeAddButton != null)
            {
                Color gaugeInactiveColor = new Color(0.20f, 0.55f, 0.55f, 0.90f);
                Color gaugeActiveColor = new Color(0.40f, 0.85f, 0.85f, 1f);

                if (gaugeAddButtonBg != null)
                    gaugeAddButtonBg.color = gaugeAddMode ? gaugeActiveColor : gaugeInactiveColor;
                if (gaugeAddButtonOutline != null)
                    gaugeAddButtonOutline.color = gaugeAddMode ? ACTIVE_BORDER : INACTIVE_BORDER;
                gaugeAddButton.transform.localScale = gaugeAddMode ? Vector3.one * 1.15f : Vector3.one;
            }
        }

        public void DeactivateMode()
        {
            editorMode = false;
            activeBlockType = SpecialBlockType.None;
            activeButtonIndex = -1;

            colorMode = false;
            activeGemType = GemType.None;
            activeColorButtonIndex = -1;

            monsterMode = false;
            gaugeAddMode = false;

            LastModeChangeFrame = Time.frameCount;

            // 특수 블록 버튼 초기화
            if (specialBlockButtons != null)
            {
                for (int i = 0; i < specialBlockButtons.Length; i++)
                {
                    if (specialBlockButtons[i] == null) continue;
                    if (buttonBackgrounds[i] != null)
                        buttonBackgrounds[i].color = buttonColors[i];
                    if (buttonOutlines[i] != null)
                        buttonOutlines[i].color = INACTIVE_BORDER;
                    specialBlockButtons[i].transform.localScale = Vector3.one;
                }
            }

            // 색상 변경 버튼 초기화
            if (colorBlockButtons != null)
            {
                for (int i = 0; i < colorBlockButtons.Length; i++)
                {
                    if (colorBlockButtons[i] == null) continue;
                    Color gemColor = GemColors.GetColor(colorGemTypes[i]);
                    if (colorButtonBackgrounds[i] != null)
                        colorButtonBackgrounds[i].color = new Color(gemColor.r * 0.7f, gemColor.g * 0.7f, gemColor.b * 0.7f, 0.90f);
                    if (colorButtonOutlines[i] != null)
                        colorButtonOutlines[i].color = INACTIVE_BORDER;
                    colorBlockButtons[i].transform.localScale = Vector3.one;
                }
            }

            // 몬스터 버튼 초기화
            if (monsterButton != null)
            {
                if (monsterButtonBg != null)
                    monsterButtonBg.color = new Color(0.55f, 0.30f, 0.65f, 0.90f);
                if (monsterButtonOutline != null)
                    monsterButtonOutline.color = INACTIVE_BORDER;
                monsterButton.transform.localScale = Vector3.one;
            }

            // 게이지 추가 버튼 초기화
            if (gaugeAddButton != null)
            {
                if (gaugeAddButtonBg != null)
                    gaugeAddButtonBg.color = new Color(0.20f, 0.55f, 0.55f, 0.90f);
                if (gaugeAddButtonOutline != null)
                    gaugeAddButtonOutline.color = INACTIVE_BORDER;
                gaugeAddButton.transform.localScale = Vector3.one;
            }

            Debug.Log("[EditorTestSystem] 비활성화");
        }

        // ============================================================
        // InputSystem에서 호출: 블록에 특수 블록 설치 또는 색상 변경
        // ============================================================

        /// <summary>
        /// 블록에 현재 활성화된 특수 블록을 설치/제거하거나 색상을 변경한다.
        /// 반환값: true면 블록을 찾아 처리함, false면 빈 공간
        /// </summary>
        public bool TryPlaceOnBlock(HexBlock block)
        {
            if (!editorMode && !colorMode && !monsterMode)
            {
                Debug.LogWarning("[EditorTestSystem] TryPlaceOnBlock 호출되었으나 모든 모드 false");
                return false;
            }

            if (monsterMode)
            {
                return TryPlaceMonster(block);
            }

            if (block != null)
            {
                if (colorMode)
                {
                    Debug.Log($"[EditorTestSystem] TryPlaceOnBlock(색상): {block.Coord}, 현재색={block.Data?.gemType}, 변경색={activeGemType}");
                    PlaceColorBlock(block);
                }
                else
                {
                    Debug.Log($"[EditorTestSystem] TryPlaceOnBlock(특수): {block.Coord}, 현재타입={block.Data?.specialType}, 설치타입={activeBlockType}");
                    PlaceSpecialBlock(block);
                }
                return true;
            }
            else
            {
                // 빈 공간 → 비활성화
                Debug.Log("[EditorTestSystem] TryPlaceOnBlock: 빈 공간 클릭 → 비활성화");
                DeactivateMode();
                return true; // 입력 소비 (회전 금지)
            }
        }

        private void PlaceSpecialBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return;

            // 같은 특수 블록 + 같은 드릴 방향이면 → 제거
            if (block.Data.specialType == activeBlockType &&
                (activeBlockType != SpecialBlockType.Drill ||
                 block.Data.drillDirection == activeDrillDirection))
            {
                block.Data.specialType = SpecialBlockType.None;
                block.Data.drillDirection = DrillDirection.Vertical;
                block.Data.timeBombCount = 0;
                Debug.Log($"[EditorTestSystem] {block.Coord}: 제거됨");
            }
            else
            {
                // 변경 또는 신규 설치
                block.Data.specialType = activeBlockType;
                if (activeBlockType == SpecialBlockType.Drill)
                    block.Data.drillDirection = activeDrillDirection;
                else if (activeBlockType == SpecialBlockType.TimeBomb)
                    block.Data.timeBombCount = 3;
                Debug.Log($"[EditorTestSystem] {block.Coord}: {activeBlockType} 설치됨");
            }

            block.UpdateVisuals();
        }

        /// <summary>
        /// 기본 블록의 색상을 변경한다.
        /// 특수 블록이 아닌 기본 블록만 색상 교체 대상.
        /// </summary>
        private void PlaceColorBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return;
            if (block.Data.gemType == GemType.None) return;

            // 이미 같은 색이면 무시
            if (block.Data.gemType == activeGemType)
            {
                Debug.Log($"[EditorTestSystem] {block.Coord}: 이미 {activeGemType} 색상");
                return;
            }

            // 색상 변경
            GemType oldColor = block.Data.gemType;
            block.Data.gemType = activeGemType;
            block.UpdateVisuals();
            Debug.Log($"[EditorTestSystem] {block.Coord}: 색상 변경 {oldColor} → {activeGemType}");
        }

        // ============================================================
        // 공개 프로퍼티
        // ============================================================

        public bool IsEditorModeActive => editorMode || colorMode || monsterMode || gaugeAddMode;
        public SpecialBlockType ActiveBlockType => activeBlockType;
        public GemType ActiveGemType => activeGemType;
        public bool IsColorMode => colorMode;
        public GameObject PanelObject => panelContainer;
    }
}

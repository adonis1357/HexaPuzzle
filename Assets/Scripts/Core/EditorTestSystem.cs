// ★ 보안(2026-07): 치트 패널 본체는 에디터/개발 빌드에서만 컴파일 — 릴리스 바이너리에서 완전 제외.
//   릴리스에서는 EditorTestSystemStub.cs의 무동작 스텁이 동일 API를 제공해 참조부가 그대로 컴파일된다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
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
        private HexGrid hexGrid;

        // 에디터 모드 상태
        private bool editorMode = false;
        private SpecialBlockType activeBlockType = SpecialBlockType.None;

        // 색상 모드 상태
        private bool colorMode = false;
        private GemType activeGemType = GemType.None;

        // 몬스터 모드 상태
        private bool monsterMode = false;
        private int currentMonsterIndex = 0; // MONSTER_CYCLE 인덱스 (0~Count-1) 또는 Count=DeleteMode
        private GameObject monsterButton;
        private Image monsterButtonBg;
        private Image monsterButtonOutline;
        private GameObject monsterPreviewObj; // 버튼 내 현재 선택 몬스터 미리보기 이미지
        private Text monsterLabel; // 버튼 라벨 텍스트 참조

        /// <summary>에디터 몬스터 타입 열거</summary>
        private enum EditorGoblinType { Regular, Armored, Archer, Shield, BombGoblin, Healer, Heavy, Wizard, Thief, Witch, RegularLv2, ArmoredLv2, ArcherLv2, ShieldLv2 }

        /// <summary>
        /// 에디터 버튼 몬스터 순환 목록.
        /// 새 몬스터 타입 추가 시 이 배열에 원하는 위치에 삽입하면
        /// 에디터 버튼 순환 및 필드 클릭 순환에 자동 적용됩니다.
        /// </summary>
        private static readonly EditorGoblinType[] MONSTER_CYCLE = {
            EditorGoblinType.Regular,
            EditorGoblinType.Armored,
            EditorGoblinType.Archer,
            EditorGoblinType.Shield,
            EditorGoblinType.BombGoblin,
            EditorGoblinType.Healer,
            EditorGoblinType.Heavy,
            EditorGoblinType.Wizard,
            EditorGoblinType.Thief,
            EditorGoblinType.Witch,
            EditorGoblinType.RegularLv2,
            EditorGoblinType.ArmoredLv2,
            EditorGoblinType.ArcherLv2,
            EditorGoblinType.ShieldLv2
        };

        /// <summary>MONSTER_CYCLE + DeleteMode 이름 배열 (UI 라벨용)</summary>
        private static string GetMonsterCycleName(int index)
        {
            if (index < 0 || index >= MONSTER_CYCLE.Length) return "삭제";
            switch (MONSTER_CYCLE[index])
            {
                case EditorGoblinType.Regular:      return "몽둥이";
                case EditorGoblinType.Armored:     return "갑옷";
                case EditorGoblinType.Archer:     return "궁수";
                case EditorGoblinType.Shield:     return "방패";
                case EditorGoblinType.BombGoblin: return "폭탄";
                case EditorGoblinType.Healer:     return "힐러";
                case EditorGoblinType.Heavy:      return "헤비";
                case EditorGoblinType.Wizard:    return "마법사";
                case EditorGoblinType.Thief:     return "도둑";
                case EditorGoblinType.Witch:       return "마녀";
                case EditorGoblinType.RegularLv2:  return "몽둥이Lv2";
                case EditorGoblinType.ArmoredLv2:  return "갑옷Lv2";
                case EditorGoblinType.ArcherLv2:   return "궁수Lv2";
                case EditorGoblinType.ShieldLv2:   return "방패Lv2";
                default: return "???";
            }
        }

        /// <summary>DeleteMode 인덱스 = MONSTER_CYCLE.Length</summary>
        private int DeleteModeIndex => MONSTER_CYCLE.Length;

        // 게이지 추가 모드
        private static EditorTestSystem _instance;
        private bool gaugeAddMode = false;
        private GameObject gaugeAddButton;
        private Image gaugeAddButtonBg;
        private Image gaugeAddButtonOutline;

        public static bool IsGaugeAddMode()
        {
            return _instance != null && _instance.gaugeAddMode;
        }

        // 스킬 해금 취소 모드
        private bool skillUnlockCancelMode = false;
        private GameObject skillCancelButton;
        private Image skillCancelButtonBg;
        private Image skillCancelButtonOutline;

        // ★ 에디터 패널 토글 버튼 (좌측 최하단) — 에디터 버튼 전체 표시/숨김
        private GameObject editorToggleButton;
        private Image editorToggleBg;
        private Text editorToggleLabel;
        private bool _lastEditorPanelActive;

        /// <summary>스킬 해금 취소 모드 활성 여부 (SkillTreeUI에서 참조)</summary>
        public bool IsSkillUnlockCancelMode => skillUnlockCancelMode;

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

        // ============================================================
        // 깨진/쉘 블록 변환 모드 (에디터 전용)
        // ============================================================
        //  - 멀쩡한 블록 클릭: isCracked=true (원래 색상 유지, 금간 상태)
        //  - 금간 블록 클릭: isShell=true + isCracked=true (회색 쉘 변환)
        //  - 쉘 블록 클릭: 다시 멀쩡한 블록으로 (Reset)
        // 그리고 일반 색상 버튼 활성 시 금간/쉘 블록 클릭 → 멀쩡한 해당 색상 블록 복원
        private bool crackedMode = false;
        private GameObject crackedButton;
        private Image crackedButtonBg;
        private Image crackedButtonOutline;

        // ============================================================
        // 흙더미 장애물 모드 (에디터 전용)
        // ============================================================
        //  - 멀쩡 블록 클릭: dirtMound = 2 (2/3 쌓임)
        //  - 2/3 블록 클릭: dirtMound = 1 (1/3 쌓임)
        //  - 1/3 블록 클릭: dirtMound = 0 (흙더미 제거)
        private bool dirtMode = false;
        private GameObject dirtButton;
        private Image dirtButtonBg;
        private Image dirtButtonOutline;

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

        public void InitializeUI(Canvas canvas, HexGrid grid)
        {
            if (isInitialized) return;
            _instance = this;
            hexGrid = grid;
            if (hexGrid == null)
                hexGrid = FindObjectOfType<HexGrid>();

            CreateTestButtonPanel(canvas);
            CreateEditorToggleButton(canvas);   // ★ 좌측 최하단 토글 버튼 (패널 밖, 항상 표시)
            isInitialized = true;

            if (panelContainer != null)
                panelContainer.SetActive(false);

            UpdateEditorToggleLabel();           // 초기 라벨/색 동기화

            Debug.Log("[EditorTestSystem] UI 초기화 완료");
        }

        public void ShowPanel(bool show)
        {
            if (panelContainer != null)
                panelContainer.SetActive(show);
            // ★ HUD의 골드 +100 치트 버튼도 함께 토글 — 패널 밖(canvas 직속)이라 명시 연동 필요
            var gm = JewelsHexaPuzzle.Managers.GameManager.Instance;
            if (gm != null && gm.GoldCheatButtonObject != null)
                gm.GoldCheatButtonObject.SetActive(show);
            if (!show)
                DeactivateMode();
            UpdateEditorToggleLabel();           // 패널 상태 변경 시 토글 라벨 동기화
        }

        // ============================================================
        // 에디터 패널 토글 버튼 (좌측 최하단)
        // ============================================================

        /// <summary>에디터 버튼 전체(panelContainer)를 표시/숨김 토글하는 버튼. 패널 밖(canvas 직속)에 두어 항상 보인다.</summary>
        private void CreateEditorToggleButton(Canvas canvas)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            editorToggleButton = new GameObject("EditorPanelToggleButton");
            editorToggleButton.transform.SetParent(canvas.transform, false);
            RectTransform rt = editorToggleButton.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);   // 화면 좌측 최하단
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(18f, 18f);
            rt.sizeDelta = new Vector2(104f, 60f);

            editorToggleBg = editorToggleButton.AddComponent<Image>();
            editorToggleBg.color = new Color(0.16f, 0.18f, 0.24f, 0.92f);

            Button btn = editorToggleButton.AddComponent<Button>();
            btn.targetGraphic = editorToggleBg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(ToggleEditorPanel);

            GameObject txtObj = new GameObject("Label");
            txtObj.transform.SetParent(editorToggleButton.transform, false);
            RectTransform trt = txtObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            editorToggleLabel = txtObj.AddComponent<Text>();
            editorToggleLabel.font = font;
            editorToggleLabel.fontSize = 20;
            editorToggleLabel.fontStyle = FontStyle.Bold;
            editorToggleLabel.alignment = TextAnchor.MiddleCenter;
            editorToggleLabel.color = Color.white;
            editorToggleLabel.raycastTarget = false;

            editorToggleButton.transform.SetAsLastSibling(); // 항상 최상단(다른 UI에 안 가려지게)
        }

        /// <summary>토글 버튼 클릭 — 에디터 패널 표시/숨김 반전.</summary>
        private void ToggleEditorPanel()
        {
            if (panelContainer == null) return;
            ShowPanel(!panelContainer.activeSelf); // ShowPanel 내부에서 라벨 동기화됨
        }

        /// <summary>현재 패널 상태에 맞춰 토글 버튼 라벨/색 갱신.</summary>
        private void UpdateEditorToggleLabel()
        {
            if (editorToggleLabel == null) return;
            bool shown = panelContainer != null && panelContainer.activeSelf;
            editorToggleLabel.text = shown ? "에디터\n숨김" : "에디터\n표시";
            if (editorToggleBg != null)
                editorToggleBg.color = shown
                    ? new Color(0.18f, 0.42f, 0.24f, 0.95f)   // 표시 중: 녹색
                    : new Color(0.16f, 0.18f, 0.24f, 0.92f);  // 숨김: 회색
        }

        // 토글 버튼 기본 위치/배치 상수
        private const float TOGGLE_DEFAULT_Y = 18f;  // 리워드 패널 숨김 시 최하단 위치
        private const float TOGGLE_GAP = 8f;         // 리워드 패널과의 간격

        /// <summary>에디터 UI z-order/배치 유지:
        /// (1) 에디터 버튼 패널(panelContainer)을 리워드 UI 앞(끝에서 2번째)으로,
        /// (2) 토글 버튼을 리워드 패널 바로 위에 배치 + 항상 최상단(끝).
        /// sibling 인덱스가 이미 맞으면 SetAsLastSibling을 호출하지 않아 매 프레임 캔버스 리빌드 churn을 피한다.</summary>
        private void PositionToggleButton()
        {
            if (editorToggleButton == null) return;
            Transform parent = editorToggleButton.transform.parent;
            if (parent == null) return;

            // (1) 에디터 버튼 패널(블록 설치 버튼 전체)을 보일 때만 리워드 UI 앞으로.
            //     목표: 끝에서 2번째(토글 바로 아래). 이미 그 위치(또는 그 이상)면 재정렬 안 함.
            if (panelContainer != null && panelContainer.activeSelf &&
                panelContainer.transform.parent == parent &&
                panelContainer.transform.GetSiblingIndex() < parent.childCount - 2)
            {
                panelContainer.transform.SetAsLastSibling(); // 일단 끝 → 아래에서 토글을 다시 끝으로 올려 끝-2위치 확정
            }

            // (2) 토글 버튼 위치 (리워드 패널 위, 없으면 기본 최하단)
            var rt = editorToggleButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                float targetY = TOGGLE_DEFAULT_Y;
                var reward = JewelsHexaPuzzle.Managers.SkillUpgradeOfferSystem.Instance;
                if (reward != null && reward.IsRewardPanelVisible)
                    targetY = reward.RewardPanelTopY + TOGGLE_GAP;
                if (Mathf.Abs(rt.anchoredPosition.y - targetY) > 0.5f)
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, targetY);
            }

            // 토글 버튼 항상 최상단(끝) — 다른 모든 UI 앞
            if (editorToggleButton.transform.GetSiblingIndex() != parent.childCount - 1)
                editorToggleButton.transform.SetAsLastSibling();
        }

        // HUD 표시/숨김 등 외부에서 panelContainer가 직접 켜지고 꺼질 때도 토글 라벨을 맞춰준다.
        private void Update()
        {
            PositionToggleButton(); // ★ 토글 버튼: 리워드 패널 위 배치 + 항상 최상단

            if (panelContainer == null || editorToggleLabel == null) return;
            bool active = panelContainer.activeSelf;
            if (active != _lastEditorPanelActive)
            {
                _lastEditorPanelActive = active;
                UpdateEditorToggleLabel();
                // ★ 골드 +100 치트 버튼도 패널 상태에 동기화 (외부 HUD 경로 방어)
                var gm = JewelsHexaPuzzle.Managers.GameManager.Instance;
                if (gm != null && gm.GoldCheatButtonObject != null &&
                    gm.GoldCheatButtonObject.activeSelf != active)
                    gm.GoldCheatButtonObject.SetActive(active);
            }
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

            // === 깨짐/쉘 변환 버튼 생성 (색상 버튼 옆) ===
            CreateCrackedBlockButton(leftmostX, lowestY, btnHexH);

            // === 흙더미 장애물 버튼 (깨짐 버튼 옆) ===
            CreateDirtMoundButton(leftmostX, lowestY, btnHexH);

            // === 몬스터 배치 버튼 생성 (색상 버튼 아래) ===
            CreateMonsterButton(leftmostX, lowestY, btnHexH);

            // === 게이지 추가 버튼 생성 (몬스터 버튼 아래) ===
            CreateGaugeAddButton();
            CreateMPAddButton();
            CreateSkillCancelButton();
            // (골드 +100은 GameManager의 기존 HUD 버튼("GoldAddButton")을 ShowPanel에서 함께 토글)
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
        // 깨짐/쉘 변환 버튼 생성 (쉘 블록처럼 디자인)
        // ============================================================

        private void CreateCrackedBlockButton(float leftmostX, float lowestY, float specialBtnHexH)
        {
            float sqrt3 = Mathf.Sqrt(3f);
            float colorBtnHexH = COLOR_BTN_SIZE * sqrt3 / 2f;

            // 컬러 버튼 2개 컬럼 오른쪽(col=2, row=0) 위치 — 색상 버튼 군과 이어지는 자리
            int col = 2;
            int row = 0;

            // 특수 블록 버튼 영역의 최하단 Y 계산 (색상 버튼과 동일 로직)
            float specialBottomY = lowestY - 70f - (BUTTONS_PER_COL - 1) * (specialBtnHexH + TEST_BTN_GAP)
                                   - (specialBtnHexH + TEST_BTN_GAP) / 2f;
            float colorStartY = specialBottomY - 35f;

            float x = leftmostX + col * (COLOR_BTN_SIZE * 0.75f + COLOR_BTN_GAP);
            float y = colorStartY - row * (colorBtnHexH + COLOR_BTN_GAP);
            if (col % 2 == 1)
                y -= (colorBtnHexH + COLOR_BTN_GAP) / 2f;
            // col=2 → 짝수이므로 오프셋 없음

            Vector2 position = new Vector2(x, y);

            GameObject btnObj = new GameObject("TestBtn_Cracked");
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = position;
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            // 배경 (육각형) — 쉘 회색 `shellGray = (0.52, 0.50, 0.48)`
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = new Color(0.52f, 0.50f, 0.48f, 0.95f);

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

            // 크랙 오버레이 — 어두운 사선 2~3개로 금이 간 느낌
            CreateCrackOverlay(btnObj, 0.3f,  45f);  // 주 크랙
            CreateCrackOverlay(btnObj, 0.22f, -30f); // 부 크랙
            CreateCrackOverlay(btnObj, 0.18f, 80f);  // 작은 크랙

            // 라벨 텍스트 ("깨짐")
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = "깨짐";
            label.font = font;
            label.fontSize = 10;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.9f);
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 8f);
            labelRt.sizeDelta = new Vector2(0f, 14f);

            // 라벨 가독성을 위한 외곽선
            Outline lblOutline = labelObj.AddComponent<Outline>();
            lblOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            lblOutline.effectDistance = new Vector2(1f, -1f);

            // Button 컴포넌트
            Button btn = btnObj.AddComponent<Button>();
            var bc = btn.colors;
            bc.normalColor = Color.white;
            bc.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            bc.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = bc;
            btn.onClick.AddListener(OnCrackedButtonClicked);

            crackedButton = btnObj;
            crackedButtonBg = bgImage;
            crackedButtonOutline = outImg;
        }

        /// <summary>
        /// 크랙 버튼의 크랙 오버레이 한 줄 생성 (얇고 어두운 사선).
        /// </summary>
        private void CreateCrackOverlay(GameObject parent, float lengthRatio, float angleDeg)
        {
            GameObject crackObj = new GameObject("Crack");
            crackObj.transform.SetParent(parent.transform, false);

            RectTransform rt = crackObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(COLOR_BTN_SIZE * lengthRatio * 2f, 2.5f);
            rt.localEulerAngles = new Vector3(0f, 0f, angleDeg);

            Image img = crackObj.AddComponent<Image>();
            img.color = new Color(0.12f, 0.10f, 0.08f, 0.9f); // HexBlock 쉘 크랙과 동일 색상
            img.raycastTarget = false;
        }

        // ============================================================
        // 흙더미 장애물 버튼 생성
        // ============================================================

        private void CreateDirtMoundButton(float leftmostX, float lowestY, float specialBtnHexH)
        {
            float sqrt3 = Mathf.Sqrt(3f);
            float colorBtnHexH = COLOR_BTN_SIZE * sqrt3 / 2f;

            // 깨짐 버튼(col=2, row=0) 바로 아래에 배치 (col=2, row=1)
            int col = 2;
            int row = 1;
            float specialBottomY = lowestY - 70f - (BUTTONS_PER_COL - 1) * (specialBtnHexH + TEST_BTN_GAP)
                                   - (specialBtnHexH + TEST_BTN_GAP) / 2f;
            float colorStartY = specialBottomY - 35f;
            float x = leftmostX + col * (COLOR_BTN_SIZE * 0.75f + COLOR_BTN_GAP);
            float y = colorStartY - row * (colorBtnHexH + COLOR_BTN_GAP);
            // col=2 짝수 → y 오프셋 없음
            Vector2 position = new Vector2(x, y);

            GameObject btnObj = new GameObject("TestBtn_DirtMound");
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = position;
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            // 배경 (육각형) — 갈색 (흙 색상)
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = new Color(0.47f, 0.32f, 0.18f, 0.95f); // 흙 갈색

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

            // 흙 더미 미리보기 (버튼 하단 절반에 어두운 갈색 막대)
            GameObject moundObj = new GameObject("MoundPreview");
            moundObj.transform.SetParent(btnObj.transform, false);
            RectTransform moundRt = moundObj.AddComponent<RectTransform>();
            moundRt.anchorMin = new Vector2(0.15f, 0.10f);
            moundRt.anchorMax = new Vector2(0.85f, 0.42f);
            moundRt.offsetMin = Vector2.zero; moundRt.offsetMax = Vector2.zero;
            Image moundImg = moundObj.AddComponent<Image>();
            moundImg.color = new Color(0.30f, 0.20f, 0.10f, 1f); // 짙은 갈색
            moundImg.raycastTarget = false;

            // 라벨
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = "흙더미";
            label.font = font;
            label.fontSize = 10;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.95f);
            label.raycastTarget = false;
            label.fontStyle = FontStyle.Bold;
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 8f);
            labelRt.sizeDelta = new Vector2(0f, 14f);
            Outline lblOutline = labelObj.AddComponent<Outline>();
            lblOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            lblOutline.effectDistance = new Vector2(1f, -1f);

            Button btn = btnObj.AddComponent<Button>();
            var bc = btn.colors;
            bc.normalColor = Color.white;
            bc.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            bc.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = bc;
            btn.onClick.AddListener(OnDirtButtonClicked);

            dirtButton = btnObj;
            dirtButtonBg = bgImage;
            dirtButtonOutline = outImg;
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
            monsterLabel = label;

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
        // 몬스터 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnMonsterButtonClicked()
        {
            if (!monsterMode)
            {
                // 다른 모드 해제 후 몬스터 모드 진입
                editorMode = false;
                activeBlockType = SpecialBlockType.None;
                activeButtonIndex = -1;
                colorMode = false;
                activeGemType = GemType.None;
                activeColorButtonIndex = -1;
                gaugeAddMode = false;
                crackedMode = false;
                dirtMode = false;

                monsterMode = true;
                currentMonsterIndex = 0; // MONSTER_CYCLE[0]부터 시작
                LastModeChangeFrame = Time.frameCount;

                UpdateMonsterPreview();
                UpdateAllButtonVisuals();
                Debug.Log($"[EditorTestSystem] 몬스터 모드: {GetMonsterCycleName(currentMonsterIndex)} (index={currentMonsterIndex})");
            }
            else
            {
                // 이미 몬스터 모드 → 다음 타입으로 순환
                currentMonsterIndex++;

                if (currentMonsterIndex > DeleteModeIndex)
                {
                    // DeleteMode 다음 → 비활성화
                    DeactivateMode();
                    Debug.Log("[EditorTestSystem] 몬스터 모드 비활성화 (순환 끝)");
                    return;
                }

                UpdateMonsterPreview();
                UpdateAllButtonVisuals();
                Debug.Log($"[EditorTestSystem] 몬스터 모드: {GetMonsterCycleName(currentMonsterIndex)} (index={currentMonsterIndex})");
            }
        }

        /// <summary>
        /// 현재 선택된 몬스터 타입의 미리보기 이미지를 버튼에 표시
        /// </summary>
        private void UpdateMonsterPreview()
        {
            if (monsterButton == null) return;

            // 기존 미리보기 제거
            if (monsterPreviewObj != null)
            {
                Object.Destroy(monsterPreviewObj);
                monsterPreviewObj = null;
            }

            // 라벨 텍스트 업데이트
            if (monsterLabel != null)
                monsterLabel.text = GetMonsterCycleName(currentMonsterIndex);

            float btnSize = COLOR_BTN_SIZE * 0.65f; // 버튼의 ~65% 크기

            if (currentMonsterIndex == DeleteModeIndex)
            {
                // DeleteMode: 빨간 X 텍스트
                monsterPreviewObj = new GameObject("MonsterPreview");
                monsterPreviewObj.transform.SetParent(monsterButton.transform, false);
                RectTransform rt = monsterPreviewObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 4f);
                rt.sizeDelta = new Vector2(btnSize, btnSize);

                Text xText = monsterPreviewObj.AddComponent<Text>();
                xText.text = "X";
                xText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                xText.fontSize = (int)(btnSize * 0.7f);
                xText.fontStyle = FontStyle.Bold;
                xText.alignment = TextAnchor.MiddleCenter;
                xText.color = Color.red;
                xText.raycastTarget = false;

                Outline xOutline = monsterPreviewObj.AddComponent<Outline>();
                xOutline.effectColor = new Color(0.3f, 0f, 0f, 1f);
                xOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else if (currentMonsterIndex < MONSTER_CYCLE.Length)
            {
                // 몬스터 스프라이트
                Sprite sprite = GetSpriteForGoblinType(MONSTER_CYCLE[currentMonsterIndex]);
                if (sprite == null) return;

                monsterPreviewObj = new GameObject("MonsterPreview");
                monsterPreviewObj.transform.SetParent(monsterButton.transform, false);
                RectTransform rt = monsterPreviewObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 4f);
                rt.sizeDelta = new Vector2(btnSize, btnSize);

                Image img = monsterPreviewObj.AddComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var curType = MONSTER_CYCLE[currentMonsterIndex];
                if (curType == EditorGoblinType.Healer) img.color = new Color(0.3f, 0.9f, 0.4f, 1f);
                else if (curType == EditorGoblinType.RegularLv2 || curType == EditorGoblinType.ArmoredLv2
                      || curType == EditorGoblinType.ArcherLv2 || curType == EditorGoblinType.ShieldLv2)
                    img.color = new Color(0.85f, 0.15f, 0.15f, 1f); // Lv2 통일 색상
                else img.color = Color.white;
                // 마법사: 스프라이트 Y축 반전 보정
                if (MONSTER_CYCLE[currentMonsterIndex] == EditorGoblinType.Wizard
                    || MONSTER_CYCLE[currentMonsterIndex] == EditorGoblinType.Witch)
                    rt.localRotation = Quaternion.Euler(0, 0, 180);
            }
        }

        /// <summary>
        /// EditorGoblinType별 스프라이트 반환 (MONSTER_CYCLE 기반)
        /// </summary>
        private Sprite GetSpriteForGoblinType(EditorGoblinType type)
        {
            switch (type)
            {
                case EditorGoblinType.Regular:    return GoblinSystem.GetGoblinSprite();
                case EditorGoblinType.Armored:    return GoblinSystem.GetArmoredGoblinSprite();
                case EditorGoblinType.Archer:     return GoblinSystem.GetArcherGoblinSprite();
                case EditorGoblinType.Shield:     return GoblinSystem.GetShieldGoblinSprite();
                case EditorGoblinType.BombGoblin: return GoblinSystem.GetBombGoblinSprite();
                case EditorGoblinType.Healer:      return GoblinSystem.GetGoblinSprite();
                case EditorGoblinType.Heavy:       return GoblinSystem.GetHeavyGoblinSprite();
                case EditorGoblinType.Wizard:      return GoblinSystem.GetWizardGoblinSprite();
                case EditorGoblinType.Thief:       return GoblinSystem.GetThiefGoblinSprite();
                case EditorGoblinType.Witch:       return GoblinSystem.GetWitchGoblinSprite();
                case EditorGoblinType.RegularLv2:  return GoblinSystem.GetGoblinSprite();
                case EditorGoblinType.ArmoredLv2:  return GoblinSystem.GetArmoredGoblinSprite();
                case EditorGoblinType.ArcherLv2:   return GoblinSystem.GetArcherGoblinSprite();
                case EditorGoblinType.ShieldLv2:   return GoblinSystem.GetShieldGoblinSprite();
                default: return null;
            }
        }

        // ============================================================
        // 몬스터 순환 배치 처리
        // ============================================================

        /// <summary>
        /// 몬스터 모드에서 블록 필드 클릭 시 배치/삭제/순환 교체.
        /// block이 null이면 빈 곳 → 즉시 비활성화.
        /// </summary>
        private bool TryPlaceMonster(HexBlock block)
        {
            if (block == null)
            {
                // 빈 곳 클릭 → 즉시 비활성화
                DeactivateMode();
                return true;
            }

            if (GoblinSystem.Instance == null)
            {
                Debug.LogWarning("[EditorTestSystem] GoblinSystem이 없어 몬스터를 배치할 수 없습니다.");
                return false;
            }

            PlaceMonsterAtCoord(block.Coord);
            return true;
        }

        /// <summary>
        /// EditorGetGoblinType 반환값 → MONSTER_CYCLE 인덱스 변환.
        /// MONSTER_CYCLE 배열에서 동적으로 탐색하므로 배열 변경 시 자동 대응.
        /// </summary>
        private int EditorTypeToMonsterCycleIndex(int editorType)
        {
            EditorGoblinType goblinType;
            switch (editorType)
            {
                case 1: goblinType = EditorGoblinType.Regular;    break;
                case 2: goblinType = EditorGoblinType.Armored;    break;
                case 3: goblinType = EditorGoblinType.Archer;     break;
                case 4: goblinType = EditorGoblinType.Shield;     break;
                case 5: goblinType = EditorGoblinType.BombGoblin; break;
                case 7: goblinType = EditorGoblinType.Healer;     break;
                case 6: goblinType = EditorGoblinType.Heavy;      break;
                case 8: goblinType = EditorGoblinType.Wizard;    break;
                case 9: goblinType = EditorGoblinType.Thief;     break;
                case 10: goblinType = EditorGoblinType.Witch;       break;
                case 11: goblinType = EditorGoblinType.RegularLv2;  break;
                case 12: goblinType = EditorGoblinType.ArmoredLv2;  break;
                case 13: goblinType = EditorGoblinType.ArcherLv2;   break;
                case 14: goblinType = EditorGoblinType.ShieldLv2;   break;
                default: return -1;
            }
            for (int i = 0; i < MONSTER_CYCLE.Length; i++)
            {
                if (MONSTER_CYCLE[i] == goblinType) return i;
            }
            return -1;
        }

        /// <summary>
        /// 좌표 기반 몬스터 배치/삭제/순환 교체 공통 로직.
        /// DeleteMode: 몬스터 있으면 삭제, 없으면 무동작.
        /// 기타: 몬스터 없으면 버튼 설정 타입으로 생성.
        /// 있으면 버튼 설정 타입을 시작점으로 MONSTER_CYCLE을 순환.
        /// 시작점에서 한 바퀴 돌아오면(= 시작 직전 타입 다음) 삭제.
        /// 예: 버튼=Healer(5) → Healer→Heavy→Regular→Armored→Archer→Shield→Bomb→삭제
        /// </summary>
        private void PlaceMonsterAtCoord(HexCoord coord)
        {
            int existingType = GoblinSystem.Instance.EditorGetGoblinType(coord);
            Debug.Log($"[에디터클릭] 위치={coord} 기존몬스터={existingType} 버튼타입={currentMonsterIndex}({GetMonsterCycleName(currentMonsterIndex)})");

            if (currentMonsterIndex == DeleteModeIndex)
            {
                // DeleteMode: 몬스터가 있으면 삭제
                if (existingType != 0)
                {
                    GoblinSystem.Instance.EditorRemoveGoblin(coord);
                    Debug.Log($"[EditorTestSystem] 몬스터 삭제: ({coord})");
                }
                return;
            }

            if (existingType != 0)
            {
                // 기존 몬스터가 있음 → MONSTER_CYCLE에서 현재 타입의 다음으로 교체
                int existingIndex = EditorTypeToMonsterCycleIndex(existingType);
                int len = MONSTER_CYCLE.Length;

                // 다음 인덱스 (순환)
                int nextIndex = (existingIndex + 1) % len;

                // 시작점(버튼 설정 타입) 직전 인덱스 = 한 바퀴 완료 지점
                // 현재 타입이 시작점 직전이면 → 다음 클릭에서 삭제
                int lastBeforeStart = (currentMonsterIndex - 1 + len) % len;
                if (existingIndex == lastBeforeStart)
                {
                    // 한 바퀴 완료 → 삭제
                    GoblinSystem.Instance.EditorRemoveGoblin(coord);
                    Debug.Log($"[EditorTestSystem] 몬스터 순환 삭제 (한 바퀴 완료): ({coord})");
                    return;
                }

                // 다음 타입으로 교체
                SpawnMonsterByCycleIndex(coord, nextIndex);
                Debug.Log($"[EditorTestSystem] 몬스터 순환 교체: ({coord}) → {GetMonsterCycleName(nextIndex)}");
            }
            else
            {
                // 몬스터 없음 → 버튼에 설정된 타입으로 생성
                SpawnMonsterByCycleIndex(coord, currentMonsterIndex);
                Debug.Log($"[EditorTestSystem] 몬스터 배치: ({coord}) → {GetMonsterCycleName(currentMonsterIndex)}");
            }
        }

        /// <summary>
        /// MONSTER_CYCLE 인덱스에 해당하는 몬스터를 좌표에 소환.
        /// EditorGoblinType → GoblinSystem.EditorSpawnGoblin bool 플래그 변환.
        /// </summary>
        private void SpawnMonsterByCycleIndex(HexCoord coord, int cycleIndex)
        {
            if (cycleIndex < 0 || cycleIndex >= MONSTER_CYCLE.Length) return;
            EditorGoblinType type = MONSTER_CYCLE[cycleIndex];

            bool isArmored = (type == EditorGoblinType.Armored || type == EditorGoblinType.ArmoredLv2);
            bool isArcher  = (type == EditorGoblinType.Archer || type == EditorGoblinType.ArcherLv2);
            bool isShield  = (type == EditorGoblinType.Shield || type == EditorGoblinType.ShieldLv2);
            bool isBomb    = (type == EditorGoblinType.BombGoblin);
            bool isHealer  = (type == EditorGoblinType.Healer);
            bool isHeavy   = (type == EditorGoblinType.Heavy);
            bool isWizard  = (type == EditorGoblinType.Wizard);
            bool isThief   = (type == EditorGoblinType.Thief);
            bool isWitch   = (type == EditorGoblinType.Witch);
            int mLevel = (type == EditorGoblinType.RegularLv2 || type == EditorGoblinType.ArmoredLv2
                       || type == EditorGoblinType.ArcherLv2 || type == EditorGoblinType.ShieldLv2) ? 2 : 1;
            GoblinSystem.Instance.EditorSpawnGoblin(coord, isArmored, isArcher, isShield, isBomb, isHealer, isHeavy, isWizard, isThief, isWitch, mLevel);
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
            crackedMode = false;
            dirtMode = false;

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

            colorMode = true;
            activeGemType = clickedGem;
            activeColorButtonIndex = index;
            crackedMode = false;
            dirtMode = false;
            LastModeChangeFrame = Time.frameCount;

            UpdateAllButtonVisuals();
            Debug.Log($"[EditorTestSystem] 색상 활성화: {colorButtonLabels[index]} ({clickedGem}) frame={Time.frameCount}");
        }

        // ============================================================
        // 깨짐/쉘 변환 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnCrackedButtonClicked()
        {
            // 이미 활성 → 비활성화
            if (crackedMode)
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
            dirtMode = false;

            crackedMode = true;
            LastModeChangeFrame = Time.frameCount;

            UpdateAllButtonVisuals();
            Debug.Log($"[EditorTestSystem] 깨짐/쉘 변환 모드 활성화 frame={Time.frameCount}");
        }

        // ============================================================
        // 흙더미 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnDirtButtonClicked()
        {
            if (dirtMode)
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
            crackedMode = false;

            dirtMode = true;
            LastModeChangeFrame = Time.frameCount;
            UpdateAllButtonVisuals();
            Debug.Log($"[EditorTestSystem] 흙더미 모드 활성화 frame={Time.frameCount}");
        }

        /// <summary>
        /// 흙더미 변환 — 3단계 순환:
        ///   멀쩡 → 2/3 (dirtMound=2)
        ///   2/3 → 1/3 (dirtMound=1)
        ///   1/3 → 멀쩡 (dirtMound=0, 흙더미 제거)
        /// 특수블록(MoveBlock 제외)은 흙더미 변환 대상 제외.
        /// </summary>
        private void PlaceDirtMound(HexBlock block)
        {
            if (block == null || block.Data == null) return;
            if (block.Data.gemType == GemType.None) return;
            if (block.Data.specialType != SpecialBlockType.None &&
                block.Data.specialType != SpecialBlockType.MoveBlock)
            {
                Debug.Log($"[EditorTestSystem] {block.Coord}: 특수블록({block.Data.specialType}) → 흙더미 변환 건너뜀");
                return;
            }

            int prev = block.Data.dirtMound;
            if (prev == 0) block.Data.dirtMound = 2;       // 멀쩡 → 2/3
            else if (prev == 2) block.Data.dirtMound = 1;  // 2/3 → 1/3
            else block.Data.dirtMound = 0;                  // 1/3 → 제거

            block.UpdateVisuals();
            Debug.Log($"[EditorTestSystem] {block.Coord}: 흙더미 {prev} → {block.Data.dirtMound}");
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

            // 깨짐 버튼 업데이트
            if (crackedButton != null)
            {
                Color inactiveShell = new Color(0.52f, 0.50f, 0.48f, 0.95f);
                Color activeShell   = new Color(0.68f, 0.64f, 0.58f, 1f); // 활성 시 밝게
                if (crackedButtonBg != null)
                    crackedButtonBg.color = crackedMode ? activeShell : inactiveShell;
                if (crackedButtonOutline != null)
                    crackedButtonOutline.color = crackedMode ? ACTIVE_BORDER : INACTIVE_BORDER;
                crackedButton.transform.localScale = crackedMode ? Vector3.one * 1.15f : Vector3.one;
            }

            // 흙더미 버튼 업데이트
            if (dirtButton != null)
            {
                Color inactiveDirt = new Color(0.47f, 0.32f, 0.18f, 0.95f);
                Color activeDirt   = new Color(0.65f, 0.45f, 0.25f, 1f);
                if (dirtButtonBg != null)
                    dirtButtonBg.color = dirtMode ? activeDirt : inactiveDirt;
                if (dirtButtonOutline != null)
                    dirtButtonOutline.color = dirtMode ? ACTIVE_BORDER : INACTIVE_BORDER;
                dirtButton.transform.localScale = dirtMode ? Vector3.one * 1.15f : Vector3.one;
            }

            // 스킬 취소 버튼 업데이트
            UpdateSkillCancelButtonVisual();
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
            currentMonsterIndex = 0;
            if (monsterPreviewObj != null) { Object.Destroy(monsterPreviewObj); monsterPreviewObj = null; }
            if (monsterLabel != null) monsterLabel.text = "몬스터";
            gaugeAddMode = false;
            crackedMode = false;
            dirtMode = false;
            skillUnlockCancelMode = false;
            UpdateSkillCancelButtonVisual();

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

            // 깨짐 버튼 초기화
            if (crackedButton != null)
            {
                if (crackedButtonBg != null)
                    crackedButtonBg.color = new Color(0.52f, 0.50f, 0.48f, 0.95f);
                if (crackedButtonOutline != null)
                    crackedButtonOutline.color = INACTIVE_BORDER;
                crackedButton.transform.localScale = Vector3.one;
            }

            // 흙더미 버튼 초기화
            if (dirtButton != null)
            {
                if (dirtButtonBg != null)
                    dirtButtonBg.color = new Color(0.47f, 0.32f, 0.18f, 0.95f);
                if (dirtButtonOutline != null)
                    dirtButtonOutline.color = INACTIVE_BORDER;
                dirtButton.transform.localScale = Vector3.one;
            }

            // 게이지 추가 버튼 초기화
            UpdateGaugeAddButtonVisual();

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
            if (!editorMode && !colorMode && !monsterMode && !gaugeAddMode && !crackedMode && !dirtMode)
            {
                Debug.LogWarning("[EditorTestSystem] TryPlaceOnBlock 호출되었으나 모든 모드 false");
                return false;
            }

            // 게이지 추가 모드: 빈 곳 클릭 시 비활성화 (아이템 버튼 클릭은 제외)
            if (gaugeAddMode)
            {
                // 클릭된 UI 오브젝트가 아이템 버튼이면 비활성화 건너뜀
                var selected = UnityEngine.EventSystems.EventSystem.current != null
                    ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
                if (selected != null && selected.GetComponentInParent<Button>() != null)
                {
                    Debug.Log("[EditorTestSystem] 게이지 추가 모드: 버튼 클릭 감지 → 비활성화 건너뜀");
                    return true;
                }
                Debug.Log("[EditorTestSystem] 게이지 추가 모드: 빈 곳 클릭 → 비활성화");
                DeactivateMode();
                return true;
            }

            if (monsterMode)
            {
                return TryPlaceMonster(block);
            }

            if (block != null)
            {
                if (dirtMode)
                {
                    Debug.Log($"[EditorTestSystem] TryPlaceOnBlock(흙더미): {block.Coord}, dirtMound={block.Data?.dirtMound}");
                    PlaceDirtMound(block);
                }
                else if (crackedMode)
                {
                    Debug.Log($"[EditorTestSystem] TryPlaceOnBlock(깨짐): {block.Coord}, isCracked={block.Data?.isCracked}, isShell={block.Data?.isShell}");
                    PlaceCrackedBlock(block);
                }
                else if (colorMode)
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
        /// ★ 깨진 블록(isCracked) / 쉘 블록(isShell)을 클릭하면 상태를 모두 해제하고
        ///    해당 색상의 멀쩡한 블록으로 복원한다.
        /// </summary>
        private void PlaceColorBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return;
            if (block.Data.gemType == GemType.None) return;

            bool wasCracked = block.Data.isCracked;
            bool wasShell = block.Data.isShell;
            bool hadDirt = block.Data.dirtMound > 0;

            // 깨진/쉘/흙더미 블록이면 상태 해제하고 색상 설정 (복원)
            if (wasCracked || wasShell || hadDirt)
            {
                GemType oldColor = block.Data.gemType;
                block.Data.isCracked = false;
                block.Data.isShell = false;
                block.Data.dirtMound = 0;
                block.Data.gemType = activeGemType;
                block.UpdateVisuals();
                Debug.Log($"[EditorTestSystem] {block.Coord}: 깨짐/쉘/흙더미 해제 + 색상 복원 (cracked={wasCracked}, shell={wasShell}, dirt={hadDirt}) {oldColor} → {activeGemType}");
                return;
            }

            // 이미 같은 색이면 무시
            if (block.Data.gemType == activeGemType)
            {
                Debug.Log($"[EditorTestSystem] {block.Coord}: 이미 {activeGemType} 색상");
                return;
            }

            // 색상 변경
            GemType oldColor2 = block.Data.gemType;
            block.Data.gemType = activeGemType;
            block.UpdateVisuals();
            Debug.Log($"[EditorTestSystem] {block.Coord}: 색상 변경 {oldColor2} → {activeGemType}");
        }

        /// <summary>
        /// 깨짐/쉘 변환 — 3단계 순환:
        ///   멀쩡한 색상 블록 → 금간 색상 블록 (isCracked=true)
        ///   금간 색상 블록 → 쉘 회색 블록 (isShell=true, isCracked=true)
        ///   쉘 블록 → 다시 멀쩡한 색상 블록 (isShell=false, isCracked=false, 원래 색상 유지)
        /// 특수블록(SpecialBlockType != None)은 깨짐 변환 대상 제외 (혼란 방지).
        /// </summary>
        private void PlaceCrackedBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return;
            if (block.Data.gemType == GemType.None) return;
            if (block.Data.specialType != SpecialBlockType.None &&
                block.Data.specialType != SpecialBlockType.MoveBlock)
            {
                Debug.Log($"[EditorTestSystem] {block.Coord}: 특수블록({block.Data.specialType}) → 깨짐 변환 건너뜀");
                return;
            }

            bool wasCracked = block.Data.isCracked;
            bool wasShell = block.Data.isShell;

            if (!wasCracked && !wasShell)
            {
                // 멀쩡 → 금간 (색상 유지)
                block.Data.isCracked = true;
                block.Data.isShell = false;
                block.UpdateVisuals();
                Debug.Log($"[EditorTestSystem] {block.Coord}: 멀쩡 → 금간 ({block.Data.gemType})");
            }
            else if (wasCracked && !wasShell)
            {
                // 금간 → 쉘 회색 변환
                block.Data.isCracked = true;
                block.Data.isShell = true;
                block.UpdateVisuals();
                Debug.Log($"[EditorTestSystem] {block.Coord}: 금간 → 쉘 회색");
            }
            else
            {
                // 쉘 → 멀쩡 복원
                block.Data.isShell = false;
                block.Data.isCracked = false;
                block.UpdateVisuals();
                Debug.Log($"[EditorTestSystem] {block.Coord}: 쉘 → 멀쩡 복원 ({block.Data.gemType})");
            }
        }

        // ============================================================
        // 게이지 추가 버튼
        // ============================================================

        private void CreateGaugeAddButton()
        {
            if (monsterButton == null || panelContainer == null) return;

            float sqrt3 = Mathf.Sqrt(3f);
            RectTransform monsterRt = monsterButton.GetComponent<RectTransform>();
            Vector2 monsterPos = monsterRt.anchoredPosition;
            float btnHalfH = COLOR_BTN_SIZE * sqrt3 / 4f;

            string btnName = "TestBtn_GaugeAdd";
            GameObject btnObj = new GameObject(btnName);
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(monsterPos.x, monsterPos.y - btnHalfH * 2f - 8f);
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            Color inactiveColor = new Color(0.20f, 0.55f, 0.55f, 0.90f);
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = inactiveColor;

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

        private void OnGaugeAddButtonClicked()
        {
            if (gaugeAddMode)
            {
                gaugeAddMode = false;
                UpdateGaugeAddButtonVisual();
                RefreshItemButtonInteractable();
                return;
            }

            // 다른 모드 해제
            if (editorMode || colorMode || monsterMode || crackedMode)
                DeactivateMode();

            gaugeAddMode = true;
            LastModeChangeFrame = Time.frameCount;
            UpdateGaugeAddButtonVisual();
            RefreshItemButtonInteractable();
            Debug.Log("[EditorTestSystem] 게이지 추가 모드 활성화");
        }

        /// <summary>gaugeAddMode 전환 시 아이템 버튼 interactable 상태 갱신</summary>
        private void RefreshItemButtonInteractable()
        {
            // 각 게이지의 버튼을 찾아서 interactable 갱신
            var hammerBtns = FindObjectsOfType<Button>();
            foreach (var btn in hammerBtns)
            {
                if (btn == null) continue;
                string n = btn.gameObject.name.ToLower();
                if (n.Contains("hammer") || n.Contains("망치") || n.Contains("swap") || n.Contains("스왑")
                    || n.Contains("line") || n.Contains("laser") || n.Contains("라인"))
                {
                    if (gaugeAddMode)
                        btn.interactable = true;
                    // 비활성화 시에는 각 Gauge의 RefreshUI가 자체적으로 처리
                }
            }
        }

        private void UpdateGaugeAddButtonVisual()
        {
            if (gaugeAddButton == null) return;
            Color inactiveColor = new Color(0.20f, 0.55f, 0.55f, 0.90f);
            Color activeColor = new Color(0.40f, 0.85f, 0.85f, 1f);

            if (gaugeAddButtonBg != null)
                gaugeAddButtonBg.color = gaugeAddMode ? activeColor : inactiveColor;
            if (gaugeAddButtonOutline != null)
                gaugeAddButtonOutline.color = gaugeAddMode ? ACTIVE_BORDER : INACTIVE_BORDER;
            gaugeAddButton.transform.localScale = gaugeAddMode ? Vector3.one * 1.15f : Vector3.one;
        }

        // ============================================================
        // MP 추가 버튼
        // ============================================================

        private void CreateMPAddButton()
        {
            if (gaugeAddButton == null || panelContainer == null) return;

            float sqrt3 = Mathf.Sqrt(3f);
            RectTransform gaugeRt = gaugeAddButton.GetComponent<RectTransform>();
            Vector2 gaugePos = gaugeRt.anchoredPosition;
            float btnHalfH = COLOR_BTN_SIZE * sqrt3 / 4f;

            string btnName = "TestBtn_MPAdd";
            GameObject btnObj = new GameObject(btnName);
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(gaugePos.x, gaugePos.y - btnHalfH * 2f - 8f);
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            Color mpColor = new Color(0.15f, 0.35f, 0.70f, 0.90f);
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = mpColor;

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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = "MP+";
            label.font = font;
            label.fontSize = 13;
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
            btn.onClick.AddListener(OnMPAddButtonClicked);
        }

        private void OnMPAddButtonClicked()
        {
            if (MPManager.Instance == null) return;

            if (MPManager.Instance.CurrentMP >= MPManager.Instance.MaxMP)
            {
                // 풀이면 0으로 초기화
                MPManager.Instance.SetMP(0);
                Debug.Log("[EditorTestSystem] MP 초기화 → 0");
            }
            else
            {
                MPManager.Instance.AddMP(10);
                Debug.Log($"[EditorTestSystem] MP +10 → {MPManager.Instance.CurrentMP}");
            }
        }

        // (구 CreateGoldAddButton 제거 — GameManager의 기존 HUD 골드 +100 버튼("GoldAddButton",
        //  롱프레스 기능 포함)과 중복이라 정리. 골드 버튼 토글 연동은 ShowPanel에서 처리)

        // ============================================================
        // 공개 프로퍼티
        // ============================================================

        public bool IsEditorModeActive => editorMode || colorMode || monsterMode || gaugeAddMode || crackedMode || dirtMode;
        public bool IsMonsterMode => monsterMode;
        public SpecialBlockType ActiveBlockType => activeBlockType;
        public GemType ActiveGemType => activeGemType;
        public bool IsColorMode => colorMode;
        public GameObject PanelObject => panelContainer;
        public GameObject EditorToggleButtonObject => editorToggleButton;

        /// <summary>
        /// 소환 영역 좌표에 몬스터를 배치한다 (InputSystem에서 호출).
        /// 그리드 밖 좌표이므로 HexBlock 없이 좌표 기반으로 직접 GoblinSystem 호출.
        /// </summary>
        public bool TryPlaceMonsterAtCoord(HexCoord coord)
        {
            if (!monsterMode) return false;
            if (GoblinSystem.Instance == null)
            {
                Debug.LogWarning("[EditorTestSystem] GoblinSystem이 없어 몬스터를 배치할 수 없습니다.");
                return false;
            }

            PlaceMonsterAtCoord(coord);
            return true;
        }

        // ============================================================
        // 스킬 해금 취소 버튼
        // ============================================================

        private void CreateSkillCancelButton()
        {
            // MP 추가 버튼 아래에 배치
            GameObject refBtn = null;
            var mpBtn = panelContainer != null ? panelContainer.transform.Find("TestBtn_MPAdd") : null;
            if (mpBtn != null) refBtn = mpBtn.gameObject;
            else if (gaugeAddButton != null) refBtn = gaugeAddButton;
            else if (monsterButton != null) refBtn = monsterButton;
            if (refBtn == null || panelContainer == null) return;

            float sqrt3 = Mathf.Sqrt(3f);
            RectTransform refRt = refBtn.GetComponent<RectTransform>();
            Vector2 refPos = refRt.anchoredPosition;
            float btnHalfH = COLOR_BTN_SIZE * sqrt3 / 4f;

            string btnName = "TestBtn_SkillCancel";
            GameObject btnObj = new GameObject(btnName);
            btnObj.transform.SetParent(panelContainer.transform, false);

            RectTransform btnRt = btnObj.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(refPos.x, refPos.y - btnHalfH * 2f - 8f);
            btnRt.sizeDelta = new Vector2(COLOR_BTN_SIZE, COLOR_BTN_SIZE);

            Color inactiveColor = new Color(0.65f, 0.25f, 0.25f, 0.90f);
            Image bgImage = btnObj.AddComponent<Image>();
            bgImage.sprite = HexBlock.GetHexFlashSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = true;
            bgImage.color = inactiveColor;

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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(btnObj.transform, false);
            Text label = labelObj.AddComponent<Text>();
            label.text = "스킬취소";
            label.font = font;
            label.fontSize = 10;
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
            btn.onClick.AddListener(OnSkillCancelButtonClicked);

            skillCancelButton = btnObj;
            skillCancelButtonBg = bgImage;
            skillCancelButtonOutline = outImg;
        }

        private void OnSkillCancelButtonClicked()
        {
            if (skillUnlockCancelMode)
            {
                skillUnlockCancelMode = false;
                UpdateSkillCancelButtonVisual();
                return;
            }

            // 다른 모드 해제
            if (editorMode || colorMode || monsterMode || gaugeAddMode || crackedMode)
                DeactivateMode();

            skillUnlockCancelMode = true;
            LastModeChangeFrame = Time.frameCount;
            UpdateSkillCancelButtonVisual();
            Debug.Log("[EditorTestSystem] 스킬 해금 취소 모드 활성화");
        }

        private void UpdateSkillCancelButtonVisual()
        {
            if (skillCancelButton == null) return;
            Color inactiveColor = new Color(0.65f, 0.25f, 0.25f, 0.90f);
            Color activeColor = new Color(1.0f, 0.40f, 0.40f, 1f);

            if (skillCancelButtonBg != null)
                skillCancelButtonBg.color = skillUnlockCancelMode ? activeColor : inactiveColor;
            if (skillCancelButtonOutline != null)
                skillCancelButtonOutline.color = skillUnlockCancelMode ? ACTIVE_BORDER : INACTIVE_BORDER;
            skillCancelButton.transform.localScale = skillUnlockCancelMode ? Vector3.one * 1.15f : Vector3.one;
        }
    }
}

#endif

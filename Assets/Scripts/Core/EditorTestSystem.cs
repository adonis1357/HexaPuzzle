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
    ///
    /// 입력 처리는 InputSystem이 담당 (이 클래스에 Update 없음)
    /// </summary>
    public class EditorTestSystem : MonoBehaviour
    {
        private HexGrid hexGrid;

        // 에디터 모드 상태
        private bool editorMode = false;
        private SpecialBlockType activeBlockType = SpecialBlockType.None;

        // Canvas UI 참조
        private GameObject panelContainer;
        private GameObject[] specialBlockButtons;
        private Image[] buttonBackgrounds;
        private Image[] buttonOutlines;

        // 특수 블록 타입 배열 (드릴 3방향 포함, 5개)
        private static readonly SpecialBlockType[] specialBlockTypes = new SpecialBlockType[]
        {
            SpecialBlockType.Bomb,
            SpecialBlockType.Drill,      // Vertical
            SpecialBlockType.Drill,      // Slash
            SpecialBlockType.Drill,      // BackSlash
            SpecialBlockType.XBlock
        };

        // 드릴 방향 매핑
        private static readonly DrillDirection[] drillDirections = new DrillDirection[]
        {
            DrillDirection.Vertical,    // index 0 (Bomb - 미사용)
            DrillDirection.Vertical,    // index 1
            DrillDirection.Slash,       // index 2
            DrillDirection.BackSlash    // index 3
        };

        private static readonly string[] buttonLabels = new string[]
        {
            "폭탄", "드릴↕", "드릴╱", "드릴╲", "엑스"
        };

        private static readonly Color[] buttonColors = new Color[]
        {
            new Color(0.75f, 0.30f, 0.20f, 0.90f),
            new Color(0.25f, 0.55f, 0.75f, 0.90f),
            new Color(0.25f, 0.55f, 0.75f, 0.90f),
            new Color(0.25f, 0.55f, 0.75f, 0.90f),
            new Color(0.80f, 0.65f, 0.20f, 0.90f)
        };

        private static readonly Color INACTIVE_BORDER = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color ACTIVE_BORDER = new Color(0.3f, 1f, 0.3f, 0.9f);

        private const float TEST_BTN_SIZE = 70f;
        private const float TEST_BTN_GAP = 4f;
        private const int BUTTONS_PER_COL = 3;

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
                float y = lowestY + 30f - row * (btnHexH + TEST_BTN_GAP);
                if (col % 2 == 1)
                    y -= (btnHexH + TEST_BTN_GAP) / 2f;

                CreateSpecialBlockButton(i, new Vector2(x, y));
            }
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
                default:
                    return null;
            }
        }

        // ============================================================
        // 버튼 클릭 → 모드 토글
        // ============================================================

        private void OnTestButtonClicked(int index)
        {
            SpecialBlockType clickedType = specialBlockTypes[index];

            if (editorMode && activeButtonIndex == index)
            {
                DeactivateMode();
                return;
            }

            editorMode = true;
            activeBlockType = clickedType;
            activeButtonIndex = index;
            LastModeChangeFrame = Time.frameCount;

            if (clickedType == SpecialBlockType.Drill && index < drillDirections.Length)
                activeDrillDirection = drillDirections[index];

            UpdateButtonVisuals();
            Debug.Log($"[EditorTestSystem] 활성화: {buttonLabels[index]} frame={Time.frameCount}");
        }

        private void UpdateButtonVisuals()
        {
            for (int i = 0; i < specialBlockButtons.Length; i++)
            {
                if (specialBlockButtons[i] == null) continue;
                bool isActive = (i == activeButtonIndex);

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

        public void DeactivateMode()
        {
            editorMode = false;
            activeBlockType = SpecialBlockType.None;
            activeButtonIndex = -1;
            LastModeChangeFrame = Time.frameCount;

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

            Debug.Log("[EditorTestSystem] 비활성화");
        }

        // ============================================================
        // InputSystem에서 호출: 블록에 특수 블록 설치
        // ============================================================

        /// <summary>
        /// 블록에 현재 활성화된 특수 블록을 설치/제거한다.
        /// 반환값: true면 블록을 찾아 처리함, false면 빈 공간
        /// </summary>
        public bool TryPlaceOnBlock(HexBlock block)
        {
            if (!editorMode)
            {
                Debug.LogWarning("[EditorTestSystem] TryPlaceOnBlock 호출되었으나 editorMode=false");
                return false;
            }

            if (block != null)
            {
                Debug.Log($"[EditorTestSystem] TryPlaceOnBlock: {block.Coord}, 현재타입={block.Data?.specialType}, 설치타입={activeBlockType}");
                PlaceSpecialBlock(block);
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

        // ============================================================
        // 공개 프로퍼티
        // ============================================================

        public bool IsEditorModeActive => editorMode;
        public SpecialBlockType ActiveBlockType => activeBlockType;
        public GameObject PanelObject => panelContainer;
    }
}

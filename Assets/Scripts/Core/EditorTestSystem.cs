using UnityEngine;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 에디터 테스트용 특수 블록 설치 시스템
    /// 우측 하단에 에디터 모드 토글 버튼 표시
    /// </summary>
    public class EditorTestSystem : MonoBehaviour
    {
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private InputSystem inputSystem;
        [SerializeField] private GameManager gameManager;

        // 에디터 모드 상태
        private bool editorMode = false;

        // 특수 블록 설치 모드 토글
        private SpecialBlockType activeBlockType = SpecialBlockType.None;

        // GUI 레이아웃 관련
        private const int BUTTON_SIZE = 60;
        private const int BUTTON_MARGIN = 10;
        private const int BUTTON_COUNT = 6; // Bomb, Drill, Donut, XBlock, Laser, TimeBomb
        private Rect editorModeButtonRect;
        private Rect specialBlockButtonsRect;

        // 특수 블록 타입 배열
        private SpecialBlockType[] specialBlockTypes = new SpecialBlockType[]
        {
            SpecialBlockType.Bomb,
            SpecialBlockType.Drill,
            SpecialBlockType.Rainbow,
            SpecialBlockType.XBlock,
            SpecialBlockType.Laser,
            SpecialBlockType.TimeBomb
        };

        private string[] specialBlockLabels = new string[]
        {
            "💣",
            "⚙️",
            "🍩",
            "✕",
            "⚡",
            "⏰"
        };

        private void Start()
        {
            if (hexGrid == null)
                hexGrid = FindObjectOfType<HexGrid>();
            if (inputSystem == null)
                inputSystem = FindObjectOfType<InputSystem>();
            if (gameManager == null)
                gameManager = GameManager.Instance;

            // 로비 상태면 이 시스템 비활성화
            if (gameManager != null && gameManager.CurrentState != GameState.Playing)
            {
                enabled = false;
            }

            // GUI 버튼 위치 계산
            CalculateButtonPositions();
        }

        private void CalculateButtonPositions()
        {
            // 우측 하단 기준
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // 에디터 모드 토글 버튼: 우측 하단 (90, 90) 크기
            editorModeButtonRect = new Rect(screenWidth - 100, screenHeight - 100, 90, 90);

            // 특수 블록 버튼들: 에디터 모드 토글 위에 배열
            int buttonsPerRow = 3;
            int rowCount = (BUTTON_COUNT + buttonsPerRow - 1) / buttonsPerRow;
            float buttonsWidth = buttonsPerRow * BUTTON_SIZE + (buttonsPerRow - 1) * BUTTON_MARGIN;
            float buttonsHeight = rowCount * BUTTON_SIZE + (rowCount - 1) * BUTTON_MARGIN;

            specialBlockButtonsRect = new Rect(
                screenWidth - buttonsWidth - BUTTON_MARGIN,
                screenHeight - 100 - buttonsHeight - 20,
                buttonsWidth,
                buttonsHeight
            );
        }

        private void OnGUI()
        {
            if (hexGrid == null) return;

            // 게임 상태가 Playing이 아니면 표시 안 함
            if (gameManager != null && gameManager.CurrentState != GameState.Playing)
                return;

            // 에디터 모드 토글 버튼 (우측 하단)
            GUI.backgroundColor = editorMode ? Color.green : Color.white;
            if (GUI.Button(editorModeButtonRect, editorMode ? "Editor\nON" : "Editor\nOFF"))
            {
                editorMode = !editorMode;
                if (!editorMode)
                {
                    activeBlockType = SpecialBlockType.None;
                }
                Debug.Log($"[EditorTestSystem] 에디터 모드: {(editorMode ? "활성화" : "비활성화")}");
            }
            GUI.backgroundColor = Color.white;

            // 에디터 모드가 활성화되어 있으면 특수 블록 버튼 표시
            if (editorMode)
            {
                DrawSpecialBlockButtons();
                HandleTouchInput();
            }
        }

        private void DrawSpecialBlockButtons()
        {
            int buttonsPerRow = 3;

            for (int i = 0; i < BUTTON_COUNT; i++)
            {
                int row = i / buttonsPerRow;
                int col = i % buttonsPerRow;

                float x = specialBlockButtonsRect.x + col * (BUTTON_SIZE + BUTTON_MARGIN);
                float y = specialBlockButtonsRect.y + row * (BUTTON_SIZE + BUTTON_MARGIN);

                Rect buttonRect = new Rect(x, y, BUTTON_SIZE, BUTTON_SIZE);

                SpecialBlockType blockType = specialBlockTypes[i];
                bool isActive = (activeBlockType == blockType);

                GUI.backgroundColor = isActive ? Color.green : new Color(0.8f, 0.8f, 0.8f);
                if (GUI.Button(buttonRect, specialBlockLabels[i], GUI.skin.box))
                {
                    if (activeBlockType == blockType)
                    {
                        // 같은 버튼이면 비활성화
                        activeBlockType = SpecialBlockType.None;
                    }
                    else
                    {
                        // 다른 버튼이면 활성화
                        activeBlockType = blockType;
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void HandleTouchInput()
        {
            if (activeBlockType == SpecialBlockType.None)
                return;

            // 마우스 클릭 감지
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 mousePos = Input.mousePosition;

                // GUI 버튼 위에 있으면 무시
                if (IsPointerOverGUIButton(mousePos))
                    return;

                // 월드 위치 변환
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10f));
                HexBlock clickedBlock = GetBlockAtWorldPosition(worldPos);

                if (clickedBlock != null)
                {
                    // 블록 클릭: 특수 블록 설치
                    ToggleSpecialBlock(clickedBlock, activeBlockType);
                }
                else
                {
                    // 블록이 아닌 곳 클릭: 에디터 모드 비활성화
                    activeBlockType = SpecialBlockType.None;
                    Debug.Log("[EditorTestSystem] 블록 아닌 곳 클릭 - 모드 비활성화");
                }
            }
        }

        private bool IsPointerOverGUIButton(Vector2 screenPos)
        {
            // 에디터 모드 버튼 위치 확인
            if (editorModeButtonRect.Contains(screenPos))
                return true;

            // 특수 블록 버튼들 위치 확인
            if (editorMode && specialBlockButtonsRect.Contains(screenPos))
                return true;

            return false;
        }

        private HexBlock GetBlockAtWorldPosition(Vector3 worldPos)
        {
            if (hexGrid == null) return null;

            // 모든 블록을 확인하여 가장 가까운 블록 찾기
            HexBlock closestBlock = null;
            float closestDist = float.MaxValue;
            const float hexSize = 50f;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null) continue;
                float dist = Vector3.Distance(block.transform.position, worldPos);
                if (dist < hexSize && dist < closestDist)
                {
                    closestDist = dist;
                    closestBlock = block;
                }
            }

            return closestBlock;
        }

        private void ToggleSpecialBlock(HexBlock block, SpecialBlockType targetType)
        {
            if (block == null || block.Data == null)
            {
                Debug.LogWarning("[EditorTestSystem] 선택된 블록이 유효하지 않음");
                return;
            }

            // 이미 같은 특수 블록이 있으면 제거
            if (block.Data.specialType == targetType)
            {
                block.Data.specialType = SpecialBlockType.None;
                ResetSpecialBlockData(block, targetType);
                Debug.Log($"[EditorTestSystem] {block.Coord}: {targetType} 제거됨");
            }
            else
            {
                // 다른 특수 블록이 있으면 제거하고 새로운 블록 설치
                if (block.Data.specialType != SpecialBlockType.None)
                {
                    Debug.Log($"[EditorTestSystem] {block.Coord}: 기존 {block.Data.specialType} 제거 후 {targetType} 설치");
                    ResetSpecialBlockData(block, block.Data.specialType);
                }

                block.Data.specialType = targetType;
                InitializeSpecialBlockData(block, targetType);
                Debug.Log($"[EditorTestSystem] {block.Coord}: {targetType} 설치됨");
            }

            // 블록 비주얼 업데이트
            block.UpdateVisuals();
        }

        private void InitializeSpecialBlockData(HexBlock block, SpecialBlockType type)
        {
            if (type == SpecialBlockType.Drill)
            {
                block.Data.drillDirection = DrillDirection.Vertical;
            }
            else if (type == SpecialBlockType.TimeBomb)
            {
                block.Data.timeBombCount = 3;
            }
        }

        private void ResetSpecialBlockData(HexBlock block, SpecialBlockType type)
        {
            if (type == SpecialBlockType.Drill)
            {
                block.Data.drillDirection = DrillDirection.Vertical;
            }
            else if (type == SpecialBlockType.TimeBomb)
            {
                block.Data.timeBombCount = 0;
            }
        }

        /// <summary>
        /// 에디터 모드 상태 반환
        /// </summary>
        public bool IsEditorModeActive => editorMode;

        /// <summary>
        /// 현재 활성화된 특수 블록 타입 반환
        /// </summary>
        public SpecialBlockType ActiveBlockType => activeBlockType;
    }
}

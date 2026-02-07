using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 입력 처리 시스템
    /// 삼각형 클러스터 선택 및 회전, 드릴 블록 클릭
    /// </summary>
    public class InputSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private RotationSystem rotationSystem;
        [SerializeField] private DrillBlockSystem drillSystem;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Canvas gameCanvas;

        // 상태
        private bool isEnabled = true;
        private HexBlock[] currentCluster = new HexBlock[3];
        private bool hasValidCluster = false;

        // 이벤트
        
        

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[InputSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[InputSystem] HexGrid not found in scene!");
            }

            if (rotationSystem == null)
            {
                rotationSystem = FindObjectOfType<RotationSystem>();
                if (rotationSystem != null)
                    Debug.Log("[InputSystem] RotationSystem auto-found: " + rotationSystem.name);
            }

            if (drillSystem == null)
            {
                drillSystem = FindObjectOfType<DrillBlockSystem>();
                if (drillSystem != null)
                    Debug.Log("[InputSystem] DrillBlockSystem auto-found: " + drillSystem.name);
            }

            if (gameCanvas == null)
            {
                gameCanvas = FindObjectOfType<Canvas>();
                if (gameCanvas != null)
                    Debug.Log("[InputSystem] Canvas auto-found: " + gameCanvas.name);
            }
        }

        private void Update()
        {
            if (!isEnabled) return;
            if (hexGrid == null || hexGrid.BlockCount == 0) return;
            if (rotationSystem != null && rotationSystem.IsRotating) return;
            if (drillSystem != null && drillSystem.IsDrilling) return;

            HandleInput();
        }

        private void HandleInput()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        private void HandleMouseInput()
        {
            Vector2 mousePos = Input.mousePosition;
            UpdateClusterPreview(mousePos);

            if (Input.GetMouseButtonDown(0))
            {
                // 먼저 드릴 블록 클릭 체크
                if (TryActivateDrill(mousePos))
                    return;

                ExecuteRotation();
            }
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    UpdateClusterPreview(touch.position);
                }

                if (touch.phase == TouchPhase.Ended)
                {
                    // 먼저 드릴 블록 클릭 체크
                    if (TryActivateDrill(touch.position))
                        return;

                    ExecuteRotation();
                }
            }
        }

        /// <summary>
        /// 드릴 블록 클릭 시 활성화
        /// </summary>
        private bool TryActivateDrill(Vector2 screenPosition)
        {
            if (drillSystem == null) return false;

            Vector2 localPos = ScreenToLocalPosition(screenPosition);
            HexBlock clickedBlock = GetBlockAtPosition(localPos);

            if (clickedBlock != null &&
                clickedBlock.Data != null &&
                clickedBlock.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drill)
            {
                Debug.Log($"[InputSystem] Drill block clicked at {clickedBlock.Coord}");
                drillSystem.ActivateDrill(clickedBlock);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 특정 위치의 블록 가져오기
        /// </summary>
        private HexBlock GetBlockAtPosition(Vector2 localPos)
        {
            if (hexGrid == null) return null;

            float closestDist = float.MaxValue;
            HexBlock closestBlock = null;
            float maxDist = hexGrid.HexSize * 0.8f;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                RectTransform rt = block.GetComponent<RectTransform>();
                Vector2 blockPos = rt != null ? rt.anchoredPosition : (Vector2)block.transform.localPosition;
                float dist = Vector2.Distance(localPos, blockPos);

                if (dist < closestDist && dist < maxDist)
                {
                    closestDist = dist;
                    closestBlock = block;
                }
            }

            return closestBlock;
        }

        private void UpdateClusterPreview(Vector2 screenPosition)
        {
            if (hexGrid == null || hexGrid.BlockCount == 0) return;

            Vector2 localPos = ScreenToLocalPosition(screenPosition);
            var cluster = hexGrid.GetClusterAtPosition(localPos);

            ClearHighlight();

            if (cluster.HasValue)
            {
                currentCluster[0] = cluster.Value.Item1;
                currentCluster[1] = cluster.Value.Item2;
                currentCluster[2] = cluster.Value.Item3;
                hasValidCluster = true;

                for (int i = 0; i < 3; i++)
                {
                    if (currentCluster[i] != null)
                        currentCluster[i].SetHighlighted(true);
                }
            }
            else
            {
                hasValidCluster = false;
            }
        }

        private void ExecuteRotation()
        {
            if (!hasValidCluster) return;
            if (rotationSystem == null) return;
            if (currentCluster[0] == null || currentCluster[1] == null || currentCluster[2] == null) return;

            rotationSystem.TryRotate(currentCluster[0], currentCluster[1], currentCluster[2]);

            ClearHighlight();
            hasValidCluster = false;
        }

        private void ClearHighlight()
        {
            for (int i = 0; i < 3; i++)
            {
                if (currentCluster[i] != null)
                {
                    currentCluster[i].SetHighlighted(false);
                    currentCluster[i] = null;
                }
            }
        }

        private Vector2 ScreenToLocalPosition(Vector2 screenPosition)
        {
            RectTransform gridRect = hexGrid.GetComponent<RectTransform>();

            if (gridRect != null && gameCanvas != null)
            {
                Vector2 localPoint;

                if (gameCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPosition, null, out localPoint);
                else
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, screenPosition, gameCanvas.worldCamera, out localPoint);

                return localPoint;
            }

            if (mainCamera != null)
            {
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 10f));
                return new Vector2(worldPos.x, worldPos.y);
            }

            return screenPosition;
        }

        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            if (!enabled)
            {
                ClearHighlight();
                hasValidCluster = false;
            }
        }

        public bool IsEnabled => isEnabled;
    }
}
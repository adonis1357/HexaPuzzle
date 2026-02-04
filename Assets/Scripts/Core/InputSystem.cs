using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 입력 처리 시스템
    /// 삼각형 클러스터 선택 및 회전
    /// </summary>
    public class InputSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private RotationSystem rotationSystem;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Canvas gameCanvas;

        // 상태
        private bool isEnabled = true;
        private HexBlock[] currentCluster = new HexBlock[3];
        private bool hasValidCluster = false;

        // 이벤트
        public event System.Action<List<HexBlock>> OnClusterSelected;
        public event System.Action OnInputCancelled;

        private void Awake()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Start()
        {
            // 참조가 없으면 자동으로 찾기
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

            // 그리드가 초기화되지 않았으면 무시
            if (hexGrid == null || hexGrid.BlockCount == 0) return;

            if (rotationSystem != null && rotationSystem.IsRotating) return;

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
            // 마우스 이동 - 클러스터 프리뷰
            Vector2 mousePos = Input.mousePosition;
            UpdateClusterPreview(mousePos);

            // 클릭 - 회전 실행
            if (Input.GetMouseButtonDown(0))
            {
                Debug.Log($"[InputSystem] Mouse clicked at screen position: {mousePos}");
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
                    ExecuteRotation();
                }
            }
        }

        /// <summary>
        /// 클러스터 프리뷰 업데이트
        /// </summary>
        private void UpdateClusterPreview(Vector2 screenPosition)
        {
            if (hexGrid == null || hexGrid.BlockCount == 0)
            {
                return;
            }

            // 화면 좌표를 로컬 좌표로 변환
            Vector2 localPos = ScreenToLocalPosition(screenPosition);

            // 삼각형 클러스터 찾기
            var cluster = hexGrid.GetClusterAtPosition(localPos);

            // 이전 하이라이트 해제
            ClearHighlight();

            if (cluster.HasValue)
            {
                currentCluster[0] = cluster.Value.Item1;
                currentCluster[1] = cluster.Value.Item2;
                currentCluster[2] = cluster.Value.Item3;
                hasValidCluster = true;

                // 새 하이라이트
                for (int i = 0; i < 3; i++)
                {
                    if (currentCluster[i] != null)
                    {
                        currentCluster[i].SetHighlighted(true);
                    }
                }
            }
            else
            {
                hasValidCluster = false;
            }
        }

        /// <summary>
        /// 회전 실행
        /// </summary>
        private void ExecuteRotation()
        {
            Debug.Log($"[InputSystem] ExecuteRotation called. hasValidCluster={hasValidCluster}");

            if (!hasValidCluster)
            {
                Debug.LogWarning("[InputSystem] No valid cluster selected!");
                return;
            }
            if (rotationSystem == null)
            {
                Debug.LogWarning("[InputSystem] RotationSystem is null!");
                return;
            }

            // 유효성 재확인
            if (currentCluster[0] == null || currentCluster[1] == null || currentCluster[2] == null)
            {
                Debug.LogWarning("Invalid cluster: null block found");
                return;
            }

            Debug.Log("Rotating cluster: " +
                currentCluster[0].Coord + ", " +
                currentCluster[1].Coord + ", " +
                currentCluster[2].Coord);

            // 회전 실행
            rotationSystem.TryRotate(currentCluster[0], currentCluster[1], currentCluster[2]);

            // 하이라이트 해제
            ClearHighlight();
            hasValidCluster = false;
        }

        /// <summary>
        /// 하이라이트 해제
        /// </summary>
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

        /// <summary>
        /// 화면 좌표를 그리드 로컬 좌표로 변환
        /// </summary>
        private Vector2 ScreenToLocalPosition(Vector2 screenPosition)
        {
            RectTransform gridRect = hexGrid.GetComponent<RectTransform>();

            if (gridRect != null && gameCanvas != null)
            {
                Vector2 localPoint;

                if (gameCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        gridRect, screenPosition, null, out localPoint);
                }
                else
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        gridRect, screenPosition, gameCanvas.worldCamera, out localPoint);
                }

                return localPoint;
            }

            // 폴백: 월드 좌표 변환
            if (mainCamera != null)
            {
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(
                    new Vector3(screenPosition.x, screenPosition.y, 10f));
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

            Debug.Log("InputSystem enabled: " + enabled);
        }

        public bool IsEnabled => isEnabled;
    }
}
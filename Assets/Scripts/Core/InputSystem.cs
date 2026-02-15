using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 입력 처리 시스템
    /// 삼각형 클러스터 선택 및 회전, 특수 블록 클릭
    /// </summary>
    public class InputSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private RotationSystem rotationSystem;
        [SerializeField] private DrillBlockSystem drillSystem;
        [SerializeField] private BombBlockSystem bombSystem;
        [SerializeField] private XBlockSystem xBlockSystem;
        [SerializeField] private LaserBlockSystem laserSystem;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;


        [SerializeField] private Camera mainCamera;
        [SerializeField] private Canvas gameCanvas;

        // 상태
        private bool isEnabled = true;
        private HexBlock[] currentCluster = new HexBlock[3];
        private bool hasValidCluster = false;

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

            if (bombSystem == null)
            {
                bombSystem = FindObjectOfType<BombBlockSystem>();
                if (bombSystem != null)
                    Debug.Log("[InputSystem] BombBlockSystem auto-found: " + bombSystem.name);
            }

            if (xBlockSystem == null)
            {
                xBlockSystem = FindObjectOfType<XBlockSystem>();
                if (xBlockSystem != null)
                    Debug.Log("[InputSystem] XBlockSystem auto-found: " + xBlockSystem.name);
            }

            if (laserSystem == null)
            {
                laserSystem = FindObjectOfType<LaserBlockSystem>();
                if (laserSystem != null)
                    Debug.Log("[InputSystem] LaserBlockSystem auto-found: " + laserSystem.name);
            }

            if (gameCanvas == null)
            {
                gameCanvas = FindObjectOfType<Canvas>();
                if (gameCanvas != null)
                    Debug.Log("[InputSystem] Canvas auto-found: " + gameCanvas.name);
            }

            if (blockRemovalSystem == null)
            {
                blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
                if (blockRemovalSystem != null)
                    Debug.Log("[InputSystem] BlockRemovalSystem auto-found: " + blockRemovalSystem.name);
            }
        }

private float lastBlockedLogTime = -10f;

private void Update()
        {
            // 클릭 시 입력 차단 원인 로그 (1초에 최대 1회)
            if (Input.GetMouseButtonDown(0) && Time.time - lastBlockedLogTime > 1f)
            {
                if (!isEnabled)
                { Debug.LogWarning("[InputSystem] BLOCKED: isEnabled=false"); lastBlockedLogTime = Time.time; }
                else if (hexGrid == null || hexGrid.BlockCount == 0)
                { Debug.LogWarning($"[InputSystem] BLOCKED: hexGrid null={hexGrid == null}, count={hexGrid?.BlockCount}"); lastBlockedLogTime = Time.time; }
                else if (rotationSystem != null && rotationSystem.IsRotating)
                { Debug.LogWarning("[InputSystem] BLOCKED: IsRotating=true"); lastBlockedLogTime = Time.time; }
                else if (drillSystem != null && drillSystem.IsDrilling)
                { Debug.LogWarning("[InputSystem] BLOCKED: IsDrilling=true"); lastBlockedLogTime = Time.time; }
                else if (bombSystem != null && bombSystem.IsBombing)
                { Debug.LogWarning("[InputSystem] BLOCKED: IsBombing=true"); lastBlockedLogTime = Time.time; }
                else if (xBlockSystem != null && xBlockSystem.IsActivating)
                { Debug.LogWarning("[InputSystem] BLOCKED: XBlock.IsActivating=true"); lastBlockedLogTime = Time.time; }
                else if (laserSystem != null && laserSystem.IsActivating)
                { Debug.LogWarning("[InputSystem] BLOCKED: Laser.IsActivating=true"); lastBlockedLogTime = Time.time; }
                else if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing)
                { Debug.LogWarning("[InputSystem] BLOCKED: BRS.IsProcessing=true"); lastBlockedLogTime = Time.time; }
            }

            if (!isEnabled) return;
            if (hexGrid == null || hexGrid.BlockCount == 0) return;
            if (rotationSystem != null && rotationSystem.IsRotating) return;
            if (drillSystem != null && drillSystem.IsDrilling) return;
            if (bombSystem != null && bombSystem.IsBombing) return;
            if (xBlockSystem != null && xBlockSystem.IsActivating) return;
            if (laserSystem != null && laserSystem.IsActivating) return;
            if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) return;

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
                if (TryActivateSpecialBlock(mousePos))
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
                    if (TryActivateSpecialBlock(touch.position))
                        return;

                    ExecuteRotation();
                }
            }
        }

        /// <summary>
        /// 특수 블록 클릭 시 활성화 (통합 디스패쳐)
        /// </summary>
        private bool TryActivateSpecialBlock(Vector2 screenPosition)
        {
            Vector2 localPos = ScreenToLocalPosition(screenPosition);
            HexBlock clickedBlock = GetBlockAtPosition(localPos);

            if (clickedBlock == null || clickedBlock.Data == null)
                return false;

            var specialType = clickedBlock.Data.specialType;

            // 드릴은 일반 클릭 범위
            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drill && drillSystem != null)
            {
                Debug.Log($"[InputSystem] Drill block clicked at {clickedBlock.Coord}");
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDrillSound();
                drillSystem.ActivateDrill(clickedBlock);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            // 폭탄은 더 좁은 클릭 범위 사용 (30% 축소)
            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Bomb && bombSystem != null)
            {
                HexBlock tightBlock = GetBlockAtPositionTight(localPos);
                if (tightBlock != null && tightBlock == clickedBlock)
                {
                    Debug.Log($"[InputSystem] Bomb block clicked at {clickedBlock.Coord}");
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayBombSound();
                    bombSystem.ActivateBomb(clickedBlock);
                    ClearHighlight();
                    hasValidCluster = false;
                    return true;
                }
                return false;
            }

            // X블록도 좁은 클릭 범위
            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.XBlock && xBlockSystem != null)
            {
                HexBlock tightBlock = GetBlockAtPositionTight(localPos);
                if (tightBlock != null && tightBlock == clickedBlock)
                {
                    Debug.Log($"[InputSystem] X-block clicked at {clickedBlock.Coord}");
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayXBlockSound();
                    xBlockSystem.ActivateXBlock(clickedBlock);
                    ClearHighlight();
                    hasValidCluster = false;
                    return true;
                }
                return false;
            }

            // 레이저도 좁은 클릭 범위
            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Laser && laserSystem != null)
            {
                HexBlock tightBlock = GetBlockAtPositionTight(localPos);
                if (tightBlock != null && tightBlock == clickedBlock)
                {
                    Debug.Log($"[InputSystem] Laser block clicked at {clickedBlock.Coord}");
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayLaserSound();
                    laserSystem.ActivateLaser(clickedBlock);
                    ClearHighlight();
                    hasValidCluster = false;
                    return true;
                }
                return false;
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

        private HexBlock GetBlockAtPositionTight(Vector2 localPos)
        {
            if (hexGrid == null) return null;

            float closestDist = float.MaxValue;
            HexBlock closestBlock = null;
            float maxDist = hexGrid.HexSize * 0.56f;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block.Data == null || block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.None)
                    continue;

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
            if (!hasValidCluster)
            {
                Debug.LogWarning("[InputSystem] ExecuteRotation: no valid cluster");
                return;
            }
            if (rotationSystem == null) return;
            if (currentCluster[0] == null || currentCluster[1] == null || currentCluster[2] == null) return;

            Debug.Log($"[InputSystem] ExecuteRotation: cluster=({currentCluster[0].Coord}, {currentCluster[1].Coord}, {currentCluster[2].Coord})");
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

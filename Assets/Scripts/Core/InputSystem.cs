using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
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
        [SerializeField] private DroneBlockSystem droneSystem;
        [SerializeField] private BlockRemovalSystem blockRemovalSystem;

        // 합성 시스템 참조
        private SpecialBlockComboSystem comboSystem;

        // 에디터 테스트 시스템 참조
        private EditorTestSystem cachedEditorTestSystem;

        /// <summary>
        /// 외부에서 EditorTestSystem 참조를 직접 설정
        /// </summary>
        public void SetEditorTestSystem(EditorTestSystem ets)
        {
            cachedEditorTestSystem = ets;
            Debug.Log($"[InputSystem] EditorTestSystem 참조 설정됨: {(ets != null ? "OK" : "null")}");
        }

        [SerializeField] private Camera mainCamera;
        [SerializeField] private Canvas gameCanvas;

        // 상태
        private bool isEnabled = true;
        private HexBlock[] currentCluster = new HexBlock[3];
        private bool hasValidCluster = false;

        // 튜토리얼 제한 모드 (특정 좌표만 터치 허용)
        private bool restrictedMode = false;
        private HashSet<HexCoord> allowedCoords = new HashSet<HexCoord>();

        // 드래그 취소 감지
        private bool isPointerDown = false;
        private Vector2 pointerDownPosition;
        private const float DRAG_CANCEL_THRESHOLD = 10f; // 10px 이상 이동 시 회전 취소

        // 합성 드래그 상태
        private HexBlock comboSource = null;       // 드래그 시작 특수블록
        private HexBlock comboTarget = null;       // 드래그 중 인접 특수블록
        private bool isDraggingCombo = false;       // 합성 드래그 진행 중
        private GameObject comboSourceHighlight;    // 소스 하이라이트 오브젝트
        private GameObject comboTargetHighlight;    // 타겟 하이라이트 오브젝트
        private GameObject comboLineObj;            // 연결선 오브젝트
        private bool comboAlsoDrillMove = false;   // 합성 모드에서 드릴 이동도 가능한 상태
        private HexBlock comboDrillMoveTarget = null; // 합성 모드 중 드릴 이동 타겟
        private int comboDrillMoveRange = 0;       // 합성 모드 중 드릴 이동 범위

        // 드릴 이동 드래그 상태 (스킬 트리)
        private bool isDraggingDrillMove = false;         // 드릴 이동 드래그 진행 중
        private HexBlock drillMoveSource = null;          // 드릴 이동 소스 블록
        private HexBlock drillMoveTarget = null;          // 드릴 이동 타겟 블록
        private int drillMoveRange = 0;                   // 현재 허용 이동 범위
        private List<GameObject> drillMoveHighlights = new List<GameObject>();  // 이동 가능 칸 하이라이트
        private GameObject drillMoveLineObj = null;        // 드릴 이동 연결선

        // 폭탄 이동 드래그 상태 (드릴과 동일 패턴)
        private bool isDraggingBombMove = false;
        private HexBlock bombMoveSource = null;
        private HexBlock bombMoveTarget = null;
        private int bombMoveRange = 0;
        private List<GameObject> bombMoveHighlights = new List<GameObject>();
        private GameObject bombMoveLineObj = null;

        // 회전 방향 (RotationSystem 연동)
        public bool IsClockwise => rotationSystem != null && rotationSystem.IsClockwise;

        public void ToggleRotationDirection()
        {
            if (rotationSystem != null)
                rotationSystem.ToggleRotationDirection();
        }

        /// <summary>
        /// 1회성 반시계 회전 설정 (아이템 사용 시 호출)
        /// </summary>
        public void SetOneTimeCounterClockwise()
        {
            if (rotationSystem != null)
                rotationSystem.SetOneTimeCounterClockwise();
        }

        /// <summary>
        /// 1회성 반시계 회전 해제 (아이템 비활성화 시 호출)
        /// </summary>
        public void ClearOneTimeCounterClockwise()
        {
            if (rotationSystem != null)
                rotationSystem.ClearOneTimeCounterClockwise();
        }

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

            if (droneSystem == null)
            {
                droneSystem = FindObjectOfType<DroneBlockSystem>();
                if (droneSystem != null)
                    Debug.Log("[InputSystem] DroneBlockSystem auto-found: " + droneSystem.name);
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

            comboSystem = FindObjectOfType<SpecialBlockComboSystem>();
            if (comboSystem != null)
                Debug.Log("[InputSystem] SpecialBlockComboSystem auto-found: " + comboSystem.name);

            cachedEditorTestSystem = FindObjectOfType<EditorTestSystem>();
        }

private void Update()
        {
            if (!isEnabled) return;
            if (hexGrid == null || hexGrid.BlockCount == 0) return;
            if (rotationSystem != null && rotationSystem.IsRotating) return;
            if (drillSystem != null && drillSystem.IsDrilling) return;
            if (bombSystem != null && bombSystem.IsBombing) return;
            if (xBlockSystem != null && xBlockSystem.IsActivating) return;
            if (droneSystem != null && droneSystem.IsActivating) return;
            if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) return;
            if (comboSystem != null && comboSystem.IsComboActive) return;

            // 구매 팝업 열려있으면 입력 차단
            if (GameManager.Instance != null && GameManager.Instance.IsPurchasePopupOpen) return;

            // 에디터 모드 활성화 시: 블록 설치만 처리, 회전/합성/발동 완전 차단
            if (cachedEditorTestSystem == null)
                cachedEditorTestSystem = FindObjectOfType<EditorTestSystem>();
            if (cachedEditorTestSystem != null && cachedEditorTestSystem.IsEditorModeActive)
            {
                HandleEditorPlacement();
                return;
            }
            // 에디터 모드 비활성 상태면 에디터 전용 입력 상태 초기화
            editorPointerDown = false;

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

        /// <summary>
        /// 테스트 버튼 패널 위의 클릭인지 확인
        /// </summary>
        private bool IsClickOnTestPanel(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            var pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = screenPos;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);
            foreach (var r in results)
            {
                if (r.gameObject != null && r.gameObject.name.StartsWith("TestBtn_"))
                    return true;
            }
            return false;
        }

        private void HandleMouseInput()
        {
            Vector2 mousePos = Input.mousePosition;

            if (Input.GetMouseButtonDown(0))
            {
                // 테스트 버튼 패널 위 클릭이면 무시 (Button.onClick에서 처리)
                if (IsClickOnTestPanel(mousePos))
                {
                    isPointerDown = false;
                    return;
                }

                isPointerDown = true;
                pointerDownPosition = mousePos;
                // 합성 드래그 시작 시도
                TryStartComboDrag(mousePos);
            }

            if (Input.GetMouseButton(0) && isPointerDown && isDraggingCombo)
            {
                // 합성 드래그 업데이트
                UpdateComboDrag(mousePos);
            }
            else if (Input.GetMouseButton(0) && isPointerDown && isDraggingDrillMove)
            {
                UpdateDrillMoveDrag(mousePos);
            }
            else if (Input.GetMouseButton(0) && isPointerDown && isDraggingBombMove)
            {
                UpdateBombMoveDrag(mousePos);
            }

            if (!isDraggingCombo && !isDraggingDrillMove && !isDraggingBombMove)
            {
                UpdateClusterPreview(mousePos);
            }

            if (Input.GetMouseButtonUp(0) && isPointerDown)
            {
                isPointerDown = false;

                // MouseUp 시점에서 에디터 모드가 활성화되었으면 (Button.onClick이 먼저 실행된 경우)
                // 회전/발동 대신 에디터 설치 처리
                if (cachedEditorTestSystem != null && cachedEditorTestSystem.IsEditorModeActive)
                {
                    ClearHighlight();
                    hasValidCluster = false;
                    CancelComboDrag();
                    CancelDrillMoveDrag();
                    CancelBombMoveDrag();
                    return;
                }

                if (isDraggingBombMove)
                {
                    FinishBombMoveDrag();
                    return;
                }

                if (isDraggingDrillMove)
                {
                    FinishDrillMoveDrag();
                    return;
                }

                if (isDraggingCombo)
                {
                    // 합성 드래그 종료
                    FinishComboDrag();
                    return;
                }

                float dragDistance = Vector2.Distance(pointerDownPosition, mousePos);

                if (dragDistance >= DRAG_CANCEL_THRESHOLD)
                {
                    ClearHighlight();
                    hasValidCluster = false;
                    return;
                }

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

                if (touch.phase == TouchPhase.Began)
                {
                    // 테스트 버튼 패널 위 터치이면 무시
                    if (IsClickOnTestPanel(touch.position))
                    {
                        isPointerDown = false;
                        return;
                    }

                    isPointerDown = true;
                    pointerDownPosition = touch.position;
                    TryStartComboDrag(touch.position);
                }

                if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && isPointerDown)
                {
                    if (isDraggingCombo)
                        UpdateComboDrag(touch.position);
                    else if (isDraggingDrillMove)
                        UpdateDrillMoveDrag(touch.position);
                    else if (isDraggingBombMove)
                        UpdateBombMoveDrag(touch.position);
                    else
                        UpdateClusterPreview(touch.position);
                }

                if (touch.phase == TouchPhase.Ended && isPointerDown)
                {
                    isPointerDown = false;

                    // TouchEnded 시점에서 에디터 모드가 활성화되었으면 회전/발동 차단
                    if (cachedEditorTestSystem != null && cachedEditorTestSystem.IsEditorModeActive)
                    {
                        ClearHighlight();
                        hasValidCluster = false;
                        CancelComboDrag();
                        CancelDrillMoveDrag();
                        CancelBombMoveDrag();
                        return;
                    }

                    if (isDraggingBombMove)
                    {
                        FinishBombMoveDrag();
                        return;
                    }

                    if (isDraggingDrillMove)
                    {
                        FinishDrillMoveDrag();
                        return;
                    }

                    if (isDraggingCombo)
                    {
                        FinishComboDrag();
                        return;
                    }

                    float dragDistance = Vector2.Distance(pointerDownPosition, touch.position);

                    if (dragDistance >= DRAG_CANCEL_THRESHOLD)
                    {
                        ClearHighlight();
                        hasValidCluster = false;
                        return;
                    }

                    if (TryActivateSpecialBlock(touch.position))
                        return;

                    ExecuteRotation();
                }

                if (touch.phase == TouchPhase.Canceled)
                {
                    isPointerDown = false;
                    CancelComboDrag();
                    ClearHighlight();
                    hasValidCluster = false;
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

            // ★ 튜토리얼 제한 모드: 허용 좌표가 아니면 발동 차단
            if (restrictedMode && allowedCoords.Count > 0 && !allowedCoords.Contains(clickedBlock.Coord))
                return false;

            var specialType = clickedBlock.Data.specialType;

            // 드릴은 일반 클릭 범위
            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drill && drillSystem != null)
            {
                // ★ MP 체크
                if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Drill))
                {
                    Debug.Log("[InputSystem] MP 부족: Drill 발동 불가");
                    var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                    if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                    return false;
                }
                Debug.Log($"[InputSystem] Drill block clicked at {clickedBlock.Coord}");
                // 발동 시작 시점에 이동횟수 1 차감
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                // MP 소모
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Drill), clickedBlock.transform.position);
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
                    // ★ MP 체크
                    if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Bomb))
                    {
                        Debug.Log("[InputSystem] MP 부족: Bomb 발동 불가");
                        var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                        if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                        return false;
                    }
                    Debug.Log($"[InputSystem] Bomb block clicked at {clickedBlock.Coord}");
                    // 발동 시작 시점에 이동횟수 1 차감
                    if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                    // MP 소모
                    if (MPManager.Instance != null)
                        MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Bomb), clickedBlock.transform.position);
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
                    // ★ MP 체크
                    if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.XBlock))
                    {
                        Debug.Log("[InputSystem] MP 부족: XBlock 발동 불가");
                        var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                        if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                        return false;
                    }
                    Debug.Log($"[InputSystem] X-block clicked at {clickedBlock.Coord}");
                    // 발동 시작 시점에 이동횟수 1 차감
                    if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                    // MP 소모
                    if (MPManager.Instance != null)
                        MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.XBlock), clickedBlock.transform.position);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayXBlockSound();
                    xBlockSystem.ActivateXBlock(clickedBlock);
                    ClearHighlight();
                    hasValidCluster = false;
                    return true;
                }
                return false;
            }

            // 드론도 좁은 클릭 범위
            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drone && droneSystem != null)
            {
                HexBlock tightBlock = GetBlockAtPositionTight(localPos);
                if (tightBlock != null && tightBlock == clickedBlock)
                {
                    // ★ MP 체크
                    if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Drone))
                    {
                        Debug.Log("[InputSystem] MP 부족: Drone 발동 불가");
                        var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                        if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                        return false;
                    }
                    Debug.Log($"[InputSystem] Drone block clicked at {clickedBlock.Coord}");
                    if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                    // MP 소모
                    if (MPManager.Instance != null)
                        MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Drone), clickedBlock.transform.position);
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayDroneSound();
                    droneSystem.ActivateDrone(clickedBlock);
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
                // ★ 튜토리얼 제한 모드: 클러스터의 모든 블록이 허용 좌표에 포함되어야 함
                if (restrictedMode && allowedCoords.Count > 0)
                {
                    HexBlock b0 = cluster.Value.Item1, b1 = cluster.Value.Item2, b2 = cluster.Value.Item3;
                    bool allAllowed = (b0 != null && allowedCoords.Contains(b0.Coord)) &&
                                     (b1 != null && allowedCoords.Contains(b1.Coord)) &&
                                     (b2 != null && allowedCoords.Contains(b2.Coord));
                    if (!allAllowed)
                    {
                        hasValidCluster = false;
                        return;
                    }
                }

                // ★ Heavy 고블린 점유 블록 회전 불가: 클러스터 좌표 중 하나라도 Heavy 점유이면 차단
                if (GoblinSystem.Instance != null)
                {
                    var heavyCoords = GoblinSystem.Instance.GetHeavyOccupiedCoords();
                    if (heavyCoords.Count > 0)
                    {
                        HexBlock hb0 = cluster.Value.Item1, hb1 = cluster.Value.Item2, hb2 = cluster.Value.Item3;
                        if ((hb0 != null && heavyCoords.Contains(hb0.Coord)) ||
                            (hb1 != null && heavyCoords.Contains(hb1.Coord)) ||
                            (hb2 != null && heavyCoords.Contains(hb2.Coord)))
                        {
                            hasValidCluster = false;
                            return;
                        }
                    }
                }

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

        // ============================================================
        // 합성 드래그 메서드
        // ============================================================

        /// <summary>합성 가능한 특수블록 타입인지 확인</summary>
        private bool IsComboableSpecial(JewelsHexaPuzzle.Data.SpecialBlockType type)
        {
            return type == JewelsHexaPuzzle.Data.SpecialBlockType.Drill ||
                   type == JewelsHexaPuzzle.Data.SpecialBlockType.Bomb ||
                   type == JewelsHexaPuzzle.Data.SpecialBlockType.XBlock ||
                   type == JewelsHexaPuzzle.Data.SpecialBlockType.Drone;
        }

        /// <summary>터치 시작 시 합성 드래그 시도</summary>
        private void TryStartComboDrag(Vector2 screenPos)
        {
            Vector2 localPos = ScreenToLocalPosition(screenPos);
            HexBlock block = GetBlockAtPosition(localPos);

            if (block != null && block.Data != null && IsComboableSpecial(block.Data.specialType))
            {
                // 인접에 합성 가능한 특수블록이 있는지 미리 확인
                bool hasComboNeighbor = false;
                if (comboSystem != null)
                {
                    var neighbors = hexGrid.GetNeighbors(block.Coord);
                    foreach (var n in neighbors)
                    {
                        if (n.Data != null && IsComboableSpecial(n.Data.specialType))
                        {
                            hasComboNeighbor = true;
                            break;
                        }
                    }
                }

                if (hasComboNeighbor)
                {
                    comboSource = block;
                    comboTarget = null;
                    comboDrillMoveTarget = null;
                    isDraggingCombo = true;
                    ClearHighlight();
                    hasValidCluster = false;
                    CreateComboHighlight(block, true);

                    // ★ 드릴 블록이면 합성 모드에서도 이동 하이라이트 동시 표시
                    comboAlsoDrillMove = false;
                    comboDrillMoveRange = 0;
                    if (block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drill)
                    {
                        int moveRange = 0;
                        if (SkillTreeManager.Instance != null)
                            moveRange = SkillTreeManager.Instance.GetDrillMoveRange();
                        if (moveRange > 0)
                        {
                            // MP 확인 (이동 가능한지만 체크, 소모는 실행 시)
                            bool canAfford = MPManager.Instance == null ||
                                MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Drill);
                            if (canAfford)
                            {
                                comboAlsoDrillMove = true;
                                comboDrillMoveRange = moveRange;
                                // 드릴 이동 하이라이트를 기존 메서드 재활용해서 표시
                                drillMoveSource = block;
                                drillMoveRange = moveRange;
                                ShowDrillMoveHighlights();
                                drillMoveSource = null; // 실제 드릴 이동 모드가 아니므로 리셋
                                Debug.Log($"[InputSystem] 합성+이동 동시 모드: {block.Coord} (이동범위: {moveRange}칸)");
                            }
                        }
                    }

                    Debug.Log($"[InputSystem] 합성 드래그 시작: {block.Coord} ({block.Data.specialType})");
                    return;
                }

                // 합성 대상 없고 드릴/폭탄 블록이면 → 이동 드래그 시도 (스킬 해금 시)
                if (block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drill)
                {
                    TryStartDrillMoveDrag(block);
                }
                else if (block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Bomb)
                {
                    TryStartBombMoveDrag(block);
                }
            }
        }

        /// <summary>드래그 중 인접 특수블록 또는 드릴 이동 타겟 감지</summary>
        private void UpdateComboDrag(Vector2 screenPos)
        {
            if (comboSource == null || comboSystem == null) return;

            Vector2 localPos = ScreenToLocalPosition(screenPos);
            HexBlock hoverBlock = GetBlockAtPosition(localPos);

            // 1) 합성 타겟 감지 (인접 특수블록)
            HexBlock newComboTarget = null;
            if (hoverBlock != null && hoverBlock != comboSource &&
                hoverBlock.Data != null && IsComboableSpecial(hoverBlock.Data.specialType) &&
                comboSource.Coord.DistanceTo(hoverBlock.Coord) == 1)
            {
                newComboTarget = hoverBlock;
            }

            // 2) 드릴 이동 타겟 감지 (비특수 블록, 범위 내)
            HexBlock newDrillTarget = null;
            if (comboAlsoDrillMove && newComboTarget == null &&
                hoverBlock != null && hoverBlock != comboSource && hoverBlock.Data != null)
            {
                // 특수블록이 아닌 일반 블록 (또는 MoveBlock/FixedBlock 제외)
                bool isNonComboBlock = !IsComboableSpecial(hoverBlock.Data.specialType);
                bool isNotFixed = hoverBlock.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.MoveBlock &&
                                  hoverBlock.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.FixedBlock;
                if (isNonComboBlock && isNotFixed)
                {
                    int dist = comboSource.Coord.DistanceTo(hoverBlock.Coord);
                    if (dist > 0 && dist <= comboDrillMoveRange)
                        newDrillTarget = hoverBlock;
                }
            }

            // 합성 타겟 업데이트
            if (newComboTarget != comboTarget)
            {
                if (comboTargetHighlight != null) { Destroy(comboTargetHighlight); comboTargetHighlight = null; }
                if (comboLineObj != null) { Destroy(comboLineObj); comboLineObj = null; }

                comboTarget = newComboTarget;

                if (comboTarget != null)
                {
                    CreateComboHighlight(comboTarget, false);
                    CreateComboLine();
                }
            }

            // 드릴 이동 타겟 업데이트 (합성 타겟이 있으면 드릴 이동은 해제)
            if (comboTarget != null) newDrillTarget = null;
            if (newDrillTarget != comboDrillMoveTarget)
            {
                // 이전 드릴 이동 하이라이트 제거
                if (drillMoveLineObj != null) { Destroy(drillMoveLineObj); drillMoveLineObj = null; }
                if (comboDrillMoveTarget != null) comboDrillMoveTarget.SetHighlighted(false);

                comboDrillMoveTarget = newDrillTarget;

                if (comboDrillMoveTarget != null)
                {
                    comboDrillMoveTarget.SetHighlighted(true);
                    // 연결선 생성 (drillMoveSource를 일시적으로 설정)
                    drillMoveSource = comboSource;
                    drillMoveTarget = comboDrillMoveTarget;
                    CreateDrillMoveLine();
                    drillMoveSource = null;
                    drillMoveTarget = null;
                }
            }
        }

        /// <summary>드래그 종료 시 합성/드릴이동 실행 또는 단독 발동</summary>
        private void FinishComboDrag()
        {
            if (comboSource != null && comboTarget != null && comboSystem != null)
            {
                // 드래그로 타겟까지 이동 → 합성 실행
                if (comboSystem.CanCombo(comboSource, comboTarget))
                {
                    // ★ 합성 MP 체크: 두 블록 비용 중 높은 쪽 적용
                    if (MPManager.Instance != null)
                    {
                        int sourceCost = MPManager.Instance.GetSpecialBlockCost(comboSource.Data.specialType);
                        int targetCost = MPManager.Instance.GetSpecialBlockCost(comboTarget.Data.specialType);
                        int comboCost = Mathf.Max(sourceCost, targetCost);
                        if (comboCost > 0 && !MPManager.Instance.CanAfford(comboCost))
                        {
                            Debug.Log($"[InputSystem] MP 부족: 합성 불가 (필요 {comboCost})");
                            var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                            if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                            CancelComboDrag();
                            return;
                        }
                        if (comboCost > 0)
                        {
                            // 합성 중간 위치에 팝업 표시
                            Vector3 comboMidPos = (comboSource.transform.position + comboTarget.transform.position) * 0.5f;
                            MPManager.Instance.TryConsumeMP(comboCost, comboMidPos);
                        }
                    }
                    Debug.Log($"[InputSystem] 합성 실행: {comboSource.Data.specialType} × {comboTarget.Data.specialType}");
                    // 이동횟수 1 차감
                    if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                    isEnabled = false;
                    comboSystem.ExecuteCombo(comboSource, comboTarget);
                    // 합성 완료 후 입력 재개는 ComboSystem에서 처리
                    StartCoroutine(WaitComboAndReenableInput());
                }
                CancelComboDrag();
            }
            else if (comboSource != null && comboDrillMoveTarget != null && comboAlsoDrillMove && drillSystem != null)
            {
                // ★ 합성 모드에서 드릴 이동 타겟으로 드래그 → 드릴 이동 실행
                HexBlock source = comboSource;
                HexBlock target = comboDrillMoveTarget;

                // MP 소모
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(
                        MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Drill),
                        target.transform.position);

                // 이동횟수 차감
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();

                // 블록 데이터 스왑
                var sourceDataClone = source.Data.Clone();
                var targetDataClone = target.Data.Clone();
                source.SetBlockData(targetDataClone);
                target.SetBlockData(sourceDataClone);

                Debug.Log($"[InputSystem] 합성모드→드릴 이동: {source.Coord} ↔ {target.Coord}, 발동 위치: {target.Coord}");

                // 드릴 발동 (이동된 위치에서)
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDrillSound();
                drillSystem.ActivateDrill(target);

                CancelComboDrag();
            }
            else if (comboSource != null && comboTarget == null && comboDrillMoveTarget == null)
            {
                // 드래그 없이 같은 자리에서 탭 → 단독 발동 시도
                float dragDist = Vector2.Distance(pointerDownPosition, Input.mousePosition);
                #if !UNITY_EDITOR && !UNITY_STANDALONE
                if (Input.touchCount > 0)
                    dragDist = Vector2.Distance(pointerDownPosition, Input.GetTouch(0).position);
                #endif

                HexBlock sourceBlock = comboSource;
                CancelComboDrag();

                if (dragDist < DRAG_CANCEL_THRESHOLD)
                {
                    // 단독 발동
                    Debug.Log($"[InputSystem] 합성 드래그 취소 → 단독 발동 시도: {sourceBlock.Coord} ({sourceBlock.Data.specialType})");
                    TryActivateSingleSpecialBlock(sourceBlock);
                }
            }
            else
            {
                CancelComboDrag();
            }
        }

        /// <summary>특수 블록 단독 발동 (블록 참조 직접 전달)</summary>
        private bool TryActivateSingleSpecialBlock(HexBlock block)
        {
            if (block == null || block.Data == null) return false;

            var specialType = block.Data.specialType;

            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drill && drillSystem != null)
            {
                // ★ MP 체크
                if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Drill))
                {
                    Debug.Log("[InputSystem] MP 부족: Drill 단독 발동 불가");
                    var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                    if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                    return false;
                }
                Debug.Log($"[InputSystem] Drill 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Drill), block.transform.position);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDrillSound();
                drillSystem.ActivateDrill(block);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Bomb && bombSystem != null)
            {
                // ★ MP 체크
                if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Bomb))
                {
                    Debug.Log("[InputSystem] MP 부족: Bomb 단독 발동 불가");
                    var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                    if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                    return false;
                }
                Debug.Log($"[InputSystem] Bomb 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Bomb), block.transform.position);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBombSound();
                bombSystem.ActivateBomb(block);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.XBlock && xBlockSystem != null)
            {
                // ★ MP 체크
                if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.XBlock))
                {
                    Debug.Log("[InputSystem] MP 부족: XBlock 단독 발동 불가");
                    var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                    if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                    return false;
                }
                Debug.Log($"[InputSystem] XBlock 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.XBlock), block.transform.position);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayXBlockSound();
                xBlockSystem.ActivateXBlock(block);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drone && droneSystem != null)
            {
                // ★ MP 체크
                if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Drone))
                {
                    Debug.Log("[InputSystem] MP 부족: Drone 단독 발동 불가");
                    var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                    if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                    return false;
                }
                Debug.Log($"[InputSystem] Drone 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Drone), block.transform.position);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDroneSound();
                droneSystem.ActivateDrone(block);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            return false;
        }

        private IEnumerator WaitComboAndReenableInput()
        {
            while (comboSystem != null && comboSystem.IsComboActive)
                yield return null;
            // 캐스케이드 완료 대기
            while (blockRemovalSystem != null && blockRemovalSystem.IsProcessing)
                yield return null;
            // ProcessSpecialBlockAftermath 처리 중이면 완전히 끝날 때까지 대기
            // (isProcessingChainDrill가 true이면 GameManager가 아직 후처리 중)
            while (GameManager.Instance != null && GameManager.Instance.IsProcessingChainDrill)
                yield return null;
            // 게임 상태가 Playing일 때만 입력 재활성화 (Processing 상태에서 재활성화 방지)
            if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.Playing)
                isEnabled = true;
        }

        /// <summary>합성 드래그 취소</summary>
        private void CancelComboDrag()
        {
            // 드릴 이동 관련 정리 (합성+이동 동시 모드)
            if (comboAlsoDrillMove)
            {
                ClearDrillMoveHighlights();
                if (drillMoveLineObj != null) { Destroy(drillMoveLineObj); drillMoveLineObj = null; }
                if (comboDrillMoveTarget != null) comboDrillMoveTarget.SetHighlighted(false);
            }
            comboDrillMoveTarget = null;
            comboAlsoDrillMove = false;
            comboDrillMoveRange = 0;

            comboSource = null;
            comboTarget = null;
            isDraggingCombo = false;
            if (comboSourceHighlight != null) { Destroy(comboSourceHighlight); comboSourceHighlight = null; }
            if (comboTargetHighlight != null) { Destroy(comboTargetHighlight); comboTargetHighlight = null; }
            if (comboLineObj != null) { Destroy(comboLineObj); comboLineObj = null; }
        }

        /// <summary>합성 대상 블록 하이라이트 생성</summary>
        private void CreateComboHighlight(HexBlock block, bool isSource)
        {
            if (block == null) return;

            GameObject hl = new GameObject(isSource ? "ComboSrcHL" : "ComboTgtHL");
            hl.transform.SetParent(block.transform, false);
            RectTransform rt = hl.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = hl.AddComponent<Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;
            // 소스: 노란 글로우, 타겟: 흰색 글로우
            img.color = isSource
                ? new Color(1f, 0.9f, 0.3f, 0.5f)
                : new Color(1f, 1f, 1f, 0.5f);

            if (isSource)
                comboSourceHighlight = hl;
            else
                comboTargetHighlight = hl;
        }

        /// <summary>소스-타겟 사이 연결선 생성</summary>
        private void CreateComboLine()
        {
            if (comboSource == null || comboTarget == null) return;

            RectTransform srcRt = comboSource.GetComponent<RectTransform>();
            RectTransform tgtRt = comboTarget.GetComponent<RectTransform>();
            if (srcRt == null || tgtRt == null) return;

            Vector2 srcPos = srcRt.anchoredPosition;
            Vector2 tgtPos = tgtRt.anchoredPosition;
            Vector2 mid = (srcPos + tgtPos) / 2f;
            float dist = Vector2.Distance(srcPos, tgtPos);
            float angle = Mathf.Atan2(tgtPos.y - srcPos.y, tgtPos.x - srcPos.x) * Mathf.Rad2Deg;

            comboLineObj = new GameObject("ComboLine");
            comboLineObj.transform.SetParent(hexGrid.transform, false);
            RectTransform lineRt = comboLineObj.AddComponent<RectTransform>();
            lineRt.anchoredPosition = mid;
            lineRt.sizeDelta = new Vector2(dist, 6f);
            lineRt.localRotation = Quaternion.Euler(0, 0, angle);
            var lineImg = comboLineObj.AddComponent<Image>();
            lineImg.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            lineImg.raycastTarget = false;
        }

        // ============================================================
        // 에디터 모드: 블록 설치 전용 입력 처리
        // 회전/합성/특수블록 발동은 여기서 절대 실행되지 않음
        // ============================================================

        // 에디터 모드 전용 상태 (일반 isPointerDown과 분리)
        private bool editorPointerDown = false;
        private Vector2 editorPointerDownPos;

        private void HandleEditorPlacement()
        {
            // 하이라이트/클러스터 상태 정리 (회전 절대 방지)
            if (hasValidCluster) ClearHighlight();
            hasValidCluster = false;
            if (isDraggingCombo) CancelComboDrag();
            isPointerDown = false;

#if UNITY_EDITOR || UNITY_STANDALONE
            // ── 에디터/PC: 마우스 입력 ──
            if (Input.GetMouseButtonDown(0))
            {
                // 테스트 버튼 위 클릭이면 Button.onClick에서 처리 → 여기선 무시
                if (IsClickOnTestPanel(Input.mousePosition))
                {
                    editorPointerDown = false;
                    return;
                }

                // 그리드 블록 위 클릭 시작
                editorPointerDown = true;
                editorPointerDownPos = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (editorPointerDown)
                {
                    editorPointerDown = false;
                    float drag = Vector2.Distance(editorPointerDownPos, (Vector2)Input.mousePosition);
                    if (drag < DRAG_CANCEL_THRESHOLD)
                    {
                        EditorPlaceAtScreen(Input.mousePosition);
                    }
                }
            }
#else
            // ── 모바일: 터치 입력 ──
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    if (IsClickOnTestPanel(touch.position))
                    {
                        editorPointerDown = false;
                        return;
                    }

                    editorPointerDown = true;
                    editorPointerDownPos = touch.position;
                }

                if (touch.phase == TouchPhase.Ended && editorPointerDown)
                {
                    editorPointerDown = false;
                    float drag = Vector2.Distance(editorPointerDownPos, touch.position);
                    if (drag < DRAG_CANCEL_THRESHOLD)
                    {
                        EditorPlaceAtScreen(touch.position);
                    }
                }

                if (touch.phase == TouchPhase.Canceled)
                {
                    editorPointerDown = false;
                }
            }
#endif
        }

        private void EditorPlaceAtScreen(Vector2 screenPos)
        {
            Vector2 localPos = ScreenToLocalPosition(screenPos);
            HexBlock block = GetBlockAtPosition(localPos);

            Debug.Log($"[InputSystem] EditorPlaceAtScreen: screenPos={screenPos}, block={block?.Coord.ToString() ?? "null"}");

            // EditorTestSystem에 위임
            cachedEditorTestSystem.TryPlaceOnBlock(block);
        }

        public void SetEnabled(bool enabled)
        {
            isEnabled = enabled;
            if (!enabled)
            {
                ClearHighlight();
                CancelComboDrag();
                hasValidCluster = false;
                isPointerDown = false;
            }
        }

        public bool IsEnabled => isEnabled;

        // ============================================================
        // 튜토리얼 제한 모드 (TutorialManager에서 호출)
        // ============================================================

        /// <summary>
        /// 제한 모드 설정: enabled=true이면 allowedCoords에 포함된 좌표만 터치 허용
        /// </summary>
        public void SetRestrictedMode(bool enabled, HashSet<HexCoord> coords = null)
        {
            restrictedMode = enabled;
            allowedCoords = coords ?? new HashSet<HexCoord>();
            Debug.Log($"[InputSystem] 제한 모드: {(enabled ? "ON" : "OFF")} (허용 좌표 {allowedCoords.Count}개)");
        }

        /// <summary>
        /// 현재 제한 모드 여부
        /// </summary>
        public bool IsRestrictedMode => restrictedMode;

        // ============================================================
        // 드릴 이동 드래그 (스킬 트리 연동)
        // ============================================================

        /// <summary>
        /// 드릴 이동 드래그 시작 시도 (스킬 해금 시만)
        /// </summary>
        private void TryStartDrillMoveDrag(HexBlock drillBlock)
        {
            if (drillBlock == null || hexGrid == null) return;

            // 스킬 트리에서 드릴 이동 범위 조회
            int moveRange = 0;
            if (SkillTreeManager.Instance != null)
                moveRange = SkillTreeManager.Instance.GetDrillMoveRange();

            if (moveRange <= 0) return; // 스킬 미해금

            // MP 체크 (드릴 이동은 드릴 발동과 동일 MP 소모)
            if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Drill))
            {
                Debug.Log("[InputSystem] MP 부족: 드릴 이동 불가");
                var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                return;
            }

            drillMoveSource = drillBlock;
            drillMoveTarget = null;
            drillMoveRange = moveRange;
            isDraggingDrillMove = true;
            ClearHighlight();
            hasValidCluster = false;

            // 이동 가능 칸 하이라이트 표시
            ShowDrillMoveHighlights();
            Debug.Log($"[InputSystem] 드릴 이동 드래그 시작: {drillBlock.Coord} (범위: {moveRange}칸)");
        }

        /// <summary>
        /// 드릴 이동 가능 칸 하이라이트 표시
        /// </summary>
        private void ShowDrillMoveHighlights()
        {
            ClearDrillMoveHighlights();
            if (drillMoveSource == null || hexGrid == null) return;

            // BFS로 이동 범위 내 모든 칸 수집
            var reachable = GetReachableCells(drillMoveSource.Coord, drillMoveRange);

            foreach (var coord in reachable)
            {
                HexBlock block = hexGrid.GetBlock(coord);
                if (block == null) continue;
                // 드릴 소스 자체는 제외
                if (coord.Equals(drillMoveSource.Coord)) continue;
                // MoveBlock/FixedBlock은 이동 불가
                if (block.Data != null && (block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.MoveBlock ||
                    block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.FixedBlock))
                    continue;

                // 하이라이트 오브젝트 생성
                GameObject highlight = new GameObject("DrillMoveHighlight");
                highlight.transform.SetParent(block.transform, false);
                RectTransform hlRt = highlight.AddComponent<RectTransform>();
                hlRt.anchoredPosition = Vector2.zero;
                hlRt.sizeDelta = new Vector2(60f, 60f);

                UnityEngine.UI.Image hlImg = highlight.AddComponent<UnityEngine.UI.Image>();
                hlImg.sprite = HexBlock.GetHexFlashSprite();
                hlImg.type = UnityEngine.UI.Image.Type.Simple;
                hlImg.preserveAspect = true;
                hlImg.color = new Color(0.3f, 0.8f, 1f, 0.35f); // 반투명 하늘색
                hlImg.raycastTarget = false;

                drillMoveHighlights.Add(highlight);
            }

            // 소스 블록 강조 (밝은 윤곽)
            if (drillMoveSource != null)
                drillMoveSource.SetHighlighted(true);
        }

        /// <summary>
        /// BFS로 이동 범위 내 좌표 수집
        /// </summary>
        private HashSet<HexCoord> GetReachableCells(HexCoord center, int range)
        {
            var result = new HashSet<HexCoord>();
            var visited = new HashSet<HexCoord>();
            var queue = new Queue<(HexCoord coord, int dist)>();

            queue.Enqueue((center, 0));
            visited.Add(center);

            while (queue.Count > 0)
            {
                var (coord, dist) = queue.Dequeue();
                result.Add(coord);

                if (dist >= range) continue;

                var neighbors = hexGrid.GetNeighbors(coord);
                foreach (var nb in neighbors)
                {
                    if (nb != null && !visited.Contains(nb.Coord) && hexGrid.IsValidCoord(nb.Coord))
                    {
                        visited.Add(nb.Coord);
                        queue.Enqueue((nb.Coord, dist + 1));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 드릴 이동 드래그 업데이트 (마우스/터치 이동 중)
        /// </summary>
        private void UpdateDrillMoveDrag(Vector2 screenPos)
        {
            if (drillMoveSource == null || hexGrid == null) return;

            Vector2 localPos = ScreenToLocalPosition(screenPos);
            HexBlock hoverBlock = GetBlockAtPosition(localPos);

            HexBlock newTarget = null;
            if (hoverBlock != null && hoverBlock != drillMoveSource &&
                hoverBlock.Data != null &&
                hoverBlock.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.MoveBlock &&
                hoverBlock.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.FixedBlock)
            {
                // 범위 내인지 확인
                int dist = drillMoveSource.Coord.DistanceTo(hoverBlock.Coord);
                if (dist > 0 && dist <= drillMoveRange)
                    newTarget = hoverBlock;
            }

            if (newTarget != drillMoveTarget)
            {
                // 이전 타겟 하이라이트 제거
                if (drillMoveLineObj != null) { Destroy(drillMoveLineObj); drillMoveLineObj = null; }
                if (drillMoveTarget != null) drillMoveTarget.SetHighlighted(false);

                drillMoveTarget = newTarget;

                if (drillMoveTarget != null)
                {
                    drillMoveTarget.SetHighlighted(true);
                    // 연결선 생성
                    CreateDrillMoveLine();
                }
            }
        }

        /// <summary>
        /// 드릴 이동 연결선 생성
        /// </summary>
        private void CreateDrillMoveLine()
        {
            if (drillMoveSource == null || drillMoveTarget == null || hexGrid == null) return;

            drillMoveLineObj = new GameObject("DrillMoveLine");
            drillMoveLineObj.transform.SetParent(hexGrid.transform, false);

            UnityEngine.UI.Image lineImg = drillMoveLineObj.AddComponent<UnityEngine.UI.Image>();
            lineImg.color = new Color(0.3f, 0.85f, 1f, 0.7f);
            lineImg.raycastTarget = false;

            RectTransform lineRt = drillMoveLineObj.GetComponent<RectTransform>();

            Vector2 srcPos = drillMoveSource.GetComponent<RectTransform>().anchoredPosition;
            Vector2 tgtPos = drillMoveTarget.GetComponent<RectTransform>().anchoredPosition;
            Vector2 mid = (srcPos + tgtPos) * 0.5f;
            float dist = Vector2.Distance(srcPos, tgtPos);
            float angle = Mathf.Atan2(tgtPos.y - srcPos.y, tgtPos.x - srcPos.x) * Mathf.Rad2Deg;

            lineRt.anchoredPosition = mid;
            lineRt.sizeDelta = new Vector2(dist, 4f);
            lineRt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// 드릴 이동 드래그 종료 — 스왑 + 발동
        /// </summary>
        private void FinishDrillMoveDrag()
        {
            if (drillMoveSource != null && drillMoveTarget != null && drillSystem != null)
            {
                // MP 소모
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(
                        MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Drill),
                        drillMoveTarget.transform.position);

                // 이동횟수 차감
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();

                // === 블록 데이터 스왑 ===
                // Clone하여 안전하게 교환
                var sourceDataClone = drillMoveSource.Data.Clone();
                var targetDataClone = drillMoveTarget.Data.Clone();

                // 타겟 블록 데이터를 소스 위치로
                drillMoveSource.SetBlockData(targetDataClone);

                // 소스(드릴) 데이터를 타겟 위치로
                drillMoveTarget.SetBlockData(sourceDataClone);

                Debug.Log($"[InputSystem] 드릴 이동 완료: {drillMoveSource.Coord} ↔ {drillMoveTarget.Coord}, 발동 위치: {drillMoveTarget.Coord}");

                // 드릴 발동 (이동된 위치에서)
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDrillSound();
                drillSystem.ActivateDrill(drillMoveTarget);
            }
            else if (drillMoveSource != null && drillMoveTarget == null)
            {
                // 드래그 없이 탭 → 기존 위치에서 단독 발동 시도
                float dragDist = Vector2.Distance(pointerDownPosition, Input.mousePosition);
                #if !UNITY_EDITOR && !UNITY_STANDALONE
                if (Input.touchCount > 0)
                    dragDist = Vector2.Distance(pointerDownPosition, Input.GetTouch(0).position);
                #endif

                if (dragDist < DRAG_CANCEL_THRESHOLD)
                {
                    HexBlock source = drillMoveSource;
                    CancelDrillMoveDrag();
                    TryActivateSingleSpecialBlock(source);
                    return;
                }
            }

            CancelDrillMoveDrag();
        }

        /// <summary>
        /// 드릴 이동 드래그 취소 — 하이라이트 정리
        /// </summary>
        private void CancelDrillMoveDrag()
        {
            ClearDrillMoveHighlights();

            if (drillMoveLineObj != null) { Destroy(drillMoveLineObj); drillMoveLineObj = null; }
            if (drillMoveSource != null) drillMoveSource.SetHighlighted(false);
            if (drillMoveTarget != null) drillMoveTarget.SetHighlighted(false);

            drillMoveSource = null;
            drillMoveTarget = null;
            isDraggingDrillMove = false;
            drillMoveRange = 0;
        }

        /// <summary>
        /// 드릴 이동 하이라이트 오브젝트 정리
        /// </summary>
        private void ClearDrillMoveHighlights()
        {
            foreach (var hl in drillMoveHighlights)
            {
                if (hl != null) Destroy(hl);
            }
            drillMoveHighlights.Clear();
        }

        // ============================================================
        // 폭탄 이동 드래그 (드릴 이동과 동일 패턴)
        // ============================================================

        private void TryStartBombMoveDrag(HexBlock bombBlock)
        {
            if (bombBlock == null || hexGrid == null) return;

            int moveRange = 0;
            if (SkillTreeManager.Instance != null)
                moveRange = SkillTreeManager.Instance.GetBombMoveRange();

            if (moveRange <= 0) return;

            if (MPManager.Instance != null && !MPManager.Instance.CanActivateSpecialBlock(JewelsHexaPuzzle.Data.SpecialBlockType.Bomb))
            {
                Debug.Log("[InputSystem] MP 부족: 폭탄 이동 불가");
                var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                return;
            }

            bombMoveSource = bombBlock;
            bombMoveTarget = null;
            bombMoveRange = moveRange;
            isDraggingBombMove = true;
            ClearHighlight();
            hasValidCluster = false;

            ShowBombMoveHighlights();
            Debug.Log($"[InputSystem] 폭탄 이동 드래그 시작: {bombBlock.Coord} (범위: {moveRange}칸)");
        }

        private void ShowBombMoveHighlights()
        {
            ClearBombMoveHighlights();
            if (bombMoveSource == null || hexGrid == null) return;

            var reachable = GetReachableCells(bombMoveSource.Coord, bombMoveRange);

            foreach (var coord in reachable)
            {
                HexBlock block = hexGrid.GetBlock(coord);
                if (block == null) continue;
                if (coord.Equals(bombMoveSource.Coord)) continue;
                if (block.Data != null && (block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.MoveBlock ||
                    block.Data.specialType == JewelsHexaPuzzle.Data.SpecialBlockType.FixedBlock))
                    continue;

                GameObject highlight = new GameObject("BombMoveHighlight");
                highlight.transform.SetParent(block.transform, false);
                RectTransform hlRt = highlight.AddComponent<RectTransform>();
                hlRt.anchoredPosition = Vector2.zero;
                hlRt.sizeDelta = new Vector2(60f, 60f);

                UnityEngine.UI.Image hlImg = highlight.AddComponent<UnityEngine.UI.Image>();
                hlImg.sprite = HexBlock.GetHexFlashSprite();
                hlImg.type = UnityEngine.UI.Image.Type.Simple;
                hlImg.preserveAspect = true;
                hlImg.color = new Color(1f, 0.5f, 0.2f, 0.35f); // 반투명 주황색
                hlImg.raycastTarget = false;

                bombMoveHighlights.Add(highlight);
            }

            if (bombMoveSource != null)
                bombMoveSource.SetHighlighted(true);
        }

        private void UpdateBombMoveDrag(Vector2 screenPos)
        {
            if (bombMoveSource == null || hexGrid == null) return;

            Vector2 localPos = ScreenToLocalPosition(screenPos);
            HexBlock hoverBlock = GetBlockAtPosition(localPos);

            HexBlock newTarget = null;
            if (hoverBlock != null && hoverBlock != bombMoveSource &&
                hoverBlock.Data != null &&
                hoverBlock.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.MoveBlock &&
                hoverBlock.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.FixedBlock)
            {
                int dist = bombMoveSource.Coord.DistanceTo(hoverBlock.Coord);
                if (dist > 0 && dist <= bombMoveRange)
                    newTarget = hoverBlock;
            }

            if (newTarget != bombMoveTarget)
            {
                if (bombMoveLineObj != null) { Destroy(bombMoveLineObj); bombMoveLineObj = null; }
                if (bombMoveTarget != null) bombMoveTarget.SetHighlighted(false);

                bombMoveTarget = newTarget;

                if (bombMoveTarget != null)
                {
                    bombMoveTarget.SetHighlighted(true);
                    CreateBombMoveLine();
                }
            }
        }

        private void CreateBombMoveLine()
        {
            if (bombMoveSource == null || bombMoveTarget == null || hexGrid == null) return;

            bombMoveLineObj = new GameObject("BombMoveLine");
            bombMoveLineObj.transform.SetParent(hexGrid.transform, false);

            UnityEngine.UI.Image lineImg = bombMoveLineObj.AddComponent<UnityEngine.UI.Image>();
            lineImg.color = new Color(1f, 0.5f, 0.2f, 0.7f); // 주황색
            lineImg.raycastTarget = false;

            RectTransform lineRt = bombMoveLineObj.GetComponent<RectTransform>();

            Vector2 srcPos = bombMoveSource.GetComponent<RectTransform>().anchoredPosition;
            Vector2 tgtPos = bombMoveTarget.GetComponent<RectTransform>().anchoredPosition;
            Vector2 mid = (srcPos + tgtPos) * 0.5f;
            float dist = Vector2.Distance(srcPos, tgtPos);
            float angle = Mathf.Atan2(tgtPos.y - srcPos.y, tgtPos.x - srcPos.x) * Mathf.Rad2Deg;

            lineRt.anchoredPosition = mid;
            lineRt.sizeDelta = new Vector2(dist, 4f);
            lineRt.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void FinishBombMoveDrag()
        {
            if (bombMoveSource != null && bombMoveTarget != null && bombSystem != null)
            {
                // MP 소모
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(
                        MPManager.Instance.GetSpecialBlockCost(JewelsHexaPuzzle.Data.SpecialBlockType.Bomb),
                        bombMoveTarget.transform.position);

                // 이동횟수 차감
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();

                // 블록 데이터 스왑
                var sourceDataClone = bombMoveSource.Data.Clone();
                var targetDataClone = bombMoveTarget.Data.Clone();

                bombMoveSource.SetBlockData(targetDataClone);
                bombMoveTarget.SetBlockData(sourceDataClone);

                Debug.Log($"[InputSystem] 폭탄 이동 완료: {bombMoveSource.Coord} ↔ {bombMoveTarget.Coord}, 발동 위치: {bombMoveTarget.Coord}");

                // 폭탄 발동 (이동된 위치에서)
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBombSound();
                bombSystem.ActivateBomb(bombMoveTarget);
            }
            else if (bombMoveSource != null && bombMoveTarget == null)
            {
                float dragDist = Vector2.Distance(pointerDownPosition, Input.mousePosition);
                #if !UNITY_EDITOR && !UNITY_STANDALONE
                if (Input.touchCount > 0)
                    dragDist = Vector2.Distance(pointerDownPosition, Input.GetTouch(0).position);
                #endif

                if (dragDist < DRAG_CANCEL_THRESHOLD)
                {
                    HexBlock source = bombMoveSource;
                    CancelBombMoveDrag();
                    TryActivateSingleSpecialBlock(source);
                    return;
                }
            }

            CancelBombMoveDrag();
        }

        private void CancelBombMoveDrag()
        {
            ClearBombMoveHighlights();

            if (bombMoveLineObj != null) { Destroy(bombMoveLineObj); bombMoveLineObj = null; }
            if (bombMoveSource != null) bombMoveSource.SetHighlighted(false);
            if (bombMoveTarget != null) bombMoveTarget.SetHighlighted(false);

            bombMoveSource = null;
            bombMoveTarget = null;
            isDraggingBombMove = false;
            bombMoveRange = 0;
        }

        private void ClearBombMoveHighlights()
        {
            foreach (var hl in bombMoveHighlights)
            {
                if (hl != null) Destroy(hl);
            }
            bombMoveHighlights.Clear();
        }
    }
}

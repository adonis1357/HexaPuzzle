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

            if (!isDraggingCombo)
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
                    return; // 이 프레임에서는 아무것도 하지 않음 — 다음 프레임부터 HandleEditorPlacement가 처리
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
                Debug.Log($"[InputSystem] Drill block clicked at {clickedBlock.Coord}");
                // 발동 시작 시점에 이동횟수 1 차감
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
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
                    // 발동 시작 시점에 이동횟수 1 차감
                    if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
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
                    // 발동 시작 시점에 이동횟수 1 차감
                    if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
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
                    Debug.Log($"[InputSystem] Drone block clicked at {clickedBlock.Coord}");
                    if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
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
            if (comboSystem == null) return;
            Vector2 localPos = ScreenToLocalPosition(screenPos);
            HexBlock block = GetBlockAtPosition(localPos);

            if (block != null && block.Data != null && IsComboableSpecial(block.Data.specialType))
            {
                // 인접에 합성 가능한 특수블록이 있는지 미리 확인
                bool hasComboNeighbor = false;
                var neighbors = hexGrid.GetNeighbors(block.Coord);
                foreach (var n in neighbors)
                {
                    if (n.Data != null && IsComboableSpecial(n.Data.specialType))
                    {
                        hasComboNeighbor = true;
                        break;
                    }
                }

                if (hasComboNeighbor)
                {
                    comboSource = block;
                    comboTarget = null;
                    isDraggingCombo = true;
                    ClearHighlight();
                    hasValidCluster = false;
                    CreateComboHighlight(block, true);
                    Debug.Log($"[InputSystem] 합성 드래그 시작: {block.Coord} ({block.Data.specialType})");
                }
            }
        }

        /// <summary>드래그 중 인접 특수블록 감지</summary>
        private void UpdateComboDrag(Vector2 screenPos)
        {
            if (comboSource == null || comboSystem == null) return;

            Vector2 localPos = ScreenToLocalPosition(screenPos);
            HexBlock hoverBlock = GetBlockAtPosition(localPos);

            HexBlock newTarget = null;
            if (hoverBlock != null && hoverBlock != comboSource &&
                hoverBlock.Data != null && IsComboableSpecial(hoverBlock.Data.specialType) &&
                comboSource.Coord.DistanceTo(hoverBlock.Coord) == 1)
            {
                newTarget = hoverBlock;
            }

            if (newTarget != comboTarget)
            {
                // 이전 타겟 하이라이트 제거
                if (comboTargetHighlight != null) { Destroy(comboTargetHighlight); comboTargetHighlight = null; }
                if (comboLineObj != null) { Destroy(comboLineObj); comboLineObj = null; }

                comboTarget = newTarget;

                if (comboTarget != null)
                {
                    CreateComboHighlight(comboTarget, false);
                    CreateComboLine();
                }
            }
        }

        /// <summary>드래그 종료 시 합성 실행 또는 단독 발동</summary>
        private void FinishComboDrag()
        {
            if (comboSource != null && comboTarget != null && comboSystem != null)
            {
                // 드래그로 타겟까지 이동 → 합성 실행
                if (comboSystem.CanCombo(comboSource, comboTarget))
                {
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
            else if (comboSource != null && comboTarget == null)
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
                Debug.Log($"[InputSystem] Drill 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDrillSound();
                drillSystem.ActivateDrill(block);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Bomb && bombSystem != null)
            {
                Debug.Log($"[InputSystem] Bomb 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBombSound();
                bombSystem.ActivateBomb(block);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.XBlock && xBlockSystem != null)
            {
                Debug.Log($"[InputSystem] XBlock 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
                if (AudioManager.Instance != null) AudioManager.Instance.PlayXBlockSound();
                xBlockSystem.ActivateXBlock(block);
                ClearHighlight();
                hasValidCluster = false;
                return true;
            }

            if (specialType == JewelsHexaPuzzle.Data.SpecialBlockType.Drone && droneSystem != null)
            {
                Debug.Log($"[InputSystem] Drone 단독 발동: {block.Coord}");
                if (GameManager.Instance != null) GameManager.Instance.UseOneTurn();
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
    }
}

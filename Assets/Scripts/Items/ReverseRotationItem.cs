using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    /// <summary>
    /// 역회전 아이템 (토글 활성화 방식)
    /// 버튼 클릭 → 활성화 (역방향 모드 + 셀로판지 오버레이) → 회전 수행 시 역방향으로 돌고 수량 소모 + 비활성화
    /// 활성 상태에서 다시 클릭 → 비활성화 (순방향 복귀, 수량 소모 없음)
    /// 아이콘은 활성/비활성 상관없이 항상 동일하게 유지
    /// </summary>
    public class ReverseRotationItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button reverseButton;
        [SerializeField] private Image backgroundOverlay;

        // 참조 (자동 탐색)
        private InputSystem inputSystem;
        private RotationSystem rotationSystem;
        private HexGrid hexGrid;

        // 상태 관리
        private bool isActive = false;
        private bool isProcessing = false;
        private bool pendingConsume = false; // 회전 완료 후 매칭 성공 시에만 소모

        // 버튼 기본 색상 (활성화 색상의 어두운 버전)
        private Color btnOriginalColor = new Color(0.48f, 0.33f, 0.60f, 0.92f);
        private static readonly Color BtnActiveColor = new Color(0.80f, 0.55f, 1f, 1f);

        // 오버레이 색상 (버튼 색상과 동일, 알파 75%)
        private Color overlayColor = new Color(0.6f, 0.4f, 0.8f, 0.25f);

        // 대기 애니메이션 코루틴 참조
        private Coroutine activeGlowCoroutine;

        public bool IsActive => isActive;

        // ============================================================
        // 초기화
        // ============================================================

        private void Start()
        {
            AutoFindReferences();
            SetupUI();

            // 버튼 초기 색상을 비활성화 색상으로 설정
            if (reverseButton != null)
            {
                var img = reverseButton.GetComponent<Image>();
                if (img != null) img.color = btnOriginalColor;
            }

            // 오버레이가 없으면 동적 생성 (전체 화면 덮는 판)
            if (backgroundOverlay == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindObjectOfType<Canvas>();
                if (canvas != null)
                {
                    GameObject overlayObj = new GameObject("ReverseRotationOverlay");
                    overlayObj.transform.SetParent(canvas.transform, false);
                    // 최상위에 배치 (전체 화면 덮기)
                    overlayObj.transform.SetAsLastSibling();

                    backgroundOverlay = overlayObj.AddComponent<Image>();
                    backgroundOverlay.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
                    backgroundOverlay.raycastTarget = false;

                    // 전체 화면 스트레치
                    RectTransform rt = overlayObj.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                }
            }

            // 오버레이 초기 비활성화
            if (backgroundOverlay != null)
            {
                backgroundOverlay.gameObject.SetActive(false);
                backgroundOverlay.raycastTarget = false;
            }

            // 회전 이벤트 구독
            if (rotationSystem != null)
            {
                rotationSystem.OnRotationStarted += OnRotationStarted;
                rotationSystem.OnRotationComplete += OnRotationCompleted;
            }
        }

        private void AutoFindReferences()
        {
            if (inputSystem == null) inputSystem = FindObjectOfType<InputSystem>();
            if (rotationSystem == null) rotationSystem = FindObjectOfType<RotationSystem>();
            if (hexGrid == null) hexGrid = FindObjectOfType<HexGrid>();
        }

        private void SetupUI()
        {
            if (reverseButton != null)
                reverseButton.onClick.AddListener(OnButtonClicked);
        }

        // ============================================================
        // 버튼 클릭 처리 (토글 방식)
        // ============================================================

        private void OnButtonClicked()
        {
            if (isProcessing) return;

            // 게임 상태 확인
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameState.Playing) return;
            if (GameManager.Instance.IsPurchasePopupOpen) return;

            // 회전 중이면 무시
            if (rotationSystem != null && rotationSystem.IsRotating) return;

            // 활성 상태에서 다시 클릭 → 비활성화 (수량 소모 없음)
            if (isActive)
            {
                Deactivate();
                return;
            }

            // MP 체크: MP가 부족하면 사용 불가
            if (MPManager.Instance != null && !MPManager.Instance.CanUseItem(ItemType.ReverseRotation))
            {
                Debug.Log($"[ReverseRotationItem] MP 부족: 필요 {MPManager.Instance.GetItemCost(ItemType.ReverseRotation)}, 현재 {MPManager.Instance.CurrentMP}");
                var gaugeUI = Object.FindObjectOfType<JewelsHexaPuzzle.UI.MPGaugeUI>();
                if (gaugeUI != null) gaugeUI.PlayInsufficientFeedback();
                return;
            }

            // 활성화
            Activate();
        }

        // ============================================================
        // 빈 공간 터치 감지 → 비활성화
        // ============================================================

        private void Update()
        {
            if (!isActive || isProcessing) return;
            if (GameManager.Instance != null && GameManager.Instance.IsPurchasePopupOpen) return;
            if (rotationSystem != null && rotationSystem.IsRotating) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0))
                CheckEmptySpaceClick(Input.mousePosition);
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                CheckEmptySpaceClick(Input.GetTouch(0).position);
#endif
        }

        /// <summary>
        /// 빈 공간 클릭 시 비활성화. 블록이나 자기 버튼 클릭은 무시.
        /// </summary>
        private void CheckEmptySpaceClick(Vector2 screenPos)
        {
            // 자기 버튼 클릭이면 무시 (OnButtonClicked에서 처리)
            if (IsClickOnSelfButton(screenPos)) return;

            // 다른 아이템 버튼 클릭이면 무시
            if (IsClickOnAnyUIButton(screenPos)) return;

            // 블록 위 클릭이면 무시 (정상 회전 입력)
            if (FindBlockAtPosition(screenPos) != null) return;

            // 빈 공간 클릭 → 비활성화
            Deactivate();
        }

        private bool IsClickOnSelfButton(Vector2 screenPos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;

            var pd = new UnityEngine.EventSystems.PointerEventData(es);
            pd.position = screenPos;
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(pd, results);
            foreach (var r in results)
            {
                if (r.gameObject == reverseButton?.gameObject)
                    return true;
            }
            return false;
        }

        private bool IsClickOnAnyUIButton(Vector2 screenPos)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;

            var pd = new UnityEngine.EventSystems.PointerEventData(es);
            pd.position = screenPos;
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(pd, results);
            foreach (var r in results)
            {
                if (r.gameObject.GetComponent<Button>() != null)
                    return true;
            }
            return false;
        }

        private HexBlock FindBlockAtPosition(Vector2 screenPos)
        {
            if (hexGrid == null) return null;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt == null) continue;
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out localPoint);
                if (rt.rect.Contains(localPoint))
                    return block;
            }
            return null;
        }

        // ============================================================
        // 활성화 연출
        // ============================================================

        public void Activate()
        {
            if (isActive || isProcessing) return;

            // 다른 아이템 비활성화 (오버레이 중첩 방지)
            DeactivateOtherItems();

            isActive = true;

            // 1회성 반시계 회전 설정
            if (inputSystem != null)
                inputSystem.SetOneTimeCounterClockwise();

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();

            // 셀로판지 오버레이 페이드인
            if (backgroundOverlay != null)
            {
                backgroundOverlay.gameObject.SetActive(true);
                backgroundOverlay.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
                StartCoroutine(FadeOverlay(0f, overlayColor.a, 0.15f));
            }

            // 버튼 활성화 색상
            if (reverseButton != null)
            {
                var img = reverseButton.GetComponent<Image>();
                if (img != null) img.color = BtnActiveColor;
            }

            // 버튼 피드백 VFX
            StartCoroutine(ButtonActivatePulse());
            StartCoroutine(ButtonGlowRing());

            // 활성 상태 글로우 (비활성화될 때까지 유지)
            activeGlowCoroutine = StartCoroutine(ActiveGlow());

            Debug.Log("[ReverseRotationItem] 활성화 (역방향 모드 ON)");
        }

        // ============================================================
        // 비활성화 연출
        // ============================================================

        public void Deactivate()
        {
            if (!isActive) return;
            isActive = false;

            // 역회전 플래그 해제 (아직 회전이 시작되지 않은 경우만)
            if (rotationSystem != null && rotationSystem.IsOneTimeCounterClockwiseActive)
            {
                if (inputSystem != null)
                    inputSystem.ClearOneTimeCounterClockwise();
            }

            // 활성 글로우 중단
            if (activeGlowCoroutine != null)
            {
                StopCoroutine(activeGlowCoroutine);
                activeGlowCoroutine = null;
            }

            // 셀로판지 오버레이 페이드아웃
            if (backgroundOverlay != null)
            {
                StartCoroutine(FadeOverlayThenHide(backgroundOverlay.color.a, 0f, 0.12f));
            }

            // 버튼 비활성화 연출
            if (reverseButton != null)
            {
                StartCoroutine(ButtonDeactivateAnim());
            }

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();

            Debug.Log("[ReverseRotationItem] 비활성화 (순방향 복귀)");
        }

        // ============================================================
        // 회전 이벤트 처리 — 역회전 사용 시 아이템 소모
        // ============================================================

        /// <summary>
        /// 회전이 시작될 때 호출. 역회전이 활성 상태면 소모 대기 상태로 전환하고 비활성화.
        /// 실제 아이템 소모는 매칭 성공 시에만 수행 (OnRotationCompleted에서 처리).
        /// </summary>
        private void OnRotationStarted()
        {
            if (!isActive) return;

            // 매칭 결과를 기다린 후 소모 (회전 완료 이벤트에서 처리)
            pendingConsume = true;

            Debug.Log("[ReverseRotationItem] 회전 시작 → 매칭 결과 대기 중");

            // 비활성화 (플래그는 RotateCoroutine에서 자동 리셋됨)
            isActive = false;

            // 활성 글로우 중단
            if (activeGlowCoroutine != null)
            {
                StopCoroutine(activeGlowCoroutine);
                activeGlowCoroutine = null;
            }

            // 셀로판지 오버레이 페이드아웃
            if (backgroundOverlay != null)
            {
                StartCoroutine(FadeOverlayThenHide(backgroundOverlay.color.a, 0f, 0.15f));
            }

            // 회전 완료 후 비주얼 복원
            StartCoroutine(DelayedDeactivateVisual());
        }

        /// <summary>
        /// 회전 완료 시 호출. 매칭 성공이면 아이템 소모, 실패면 수량 보존.
        /// </summary>
        private void OnRotationCompleted(bool matchFound)
        {
            if (!pendingConsume) return;
            pendingConsume = false;

            if (matchFound)
            {
                // 매칭 성공 → MP 소모
                if (MPManager.Instance != null)
                    MPManager.Instance.TryConsumeMP(MPManager.Instance.GetItemCost(ItemType.ReverseRotation));

                Debug.Log("[ReverseRotationItem] 매칭 성공 → MP 소모");
            }
            else
            {
                // 매칭 실패 → MP 보존
                Debug.Log("[ReverseRotationItem] 매칭 실패 → MP 보존");
            }
        }

        /// <summary>
        /// 회전 완료 후 버튼 비주얼 복원
        /// </summary>
        private IEnumerator DelayedDeactivateVisual()
        {
            // 회전 완료 대기
            while (rotationSystem != null && rotationSystem.IsRotating)
                yield return null;

            // 버튼 색상 복원
            if (reverseButton != null)
            {
                StartCoroutine(ButtonDeactivateAnim());
            }
        }

        // ============================================================
        // 오버레이 페이드 애니메이션
        // ============================================================

        private IEnumerator FadeOverlay(float fromAlpha, float toAlpha, float duration)
        {
            if (backgroundOverlay == null) yield break;
            float elapsed = 0f;
            Color c = backgroundOverlay.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                backgroundOverlay.color = c;
                yield return null;
            }
            c.a = toAlpha;
            backgroundOverlay.color = c;
        }

        private IEnumerator FadeOverlayThenHide(float fromAlpha, float toAlpha, float duration)
        {
            yield return StartCoroutine(FadeOverlay(fromAlpha, toAlpha, duration));
            if (backgroundOverlay != null)
                backgroundOverlay.gameObject.SetActive(false);
        }

        // ============================================================
        // 활성 상태 글로우 (활성화 동안 지속)
        // ============================================================

        /// <summary>
        /// 활성 상태 동안 버튼 펄스 글로우 유지
        /// </summary>
        private IEnumerator ActiveGlow()
        {
            if (reverseButton == null) yield break;

            var img = reverseButton.GetComponent<Image>();
            if (img == null) yield break;

            Color activeColor = BtnActiveColor;
            Color brightColor = new Color(0.90f, 0.70f, 1f, 1f);

            // 활성 상태 동안 펄스
            while (isActive)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
                img.color = Color.Lerp(activeColor, brightColor, pulse);
                yield return null;
            }
        }

        // ============================================================
        // 버튼 VFX
        // ============================================================

        /// <summary>
        /// 활성화 시 버튼 스케일 펄스: 1.0 → 1.2 → 1.0
        /// </summary>
        private IEnumerator ButtonActivatePulse()
        {
            isProcessing = true;

            if (reverseButton == null)
            {
                isProcessing = false;
                yield break;
            }

            Transform btnTransform = reverseButton.transform;

            float pulseDuration = 0.2f;
            float elapsed = 0f;
            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pulseDuration);
                float scale = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
                btnTransform.localScale = Vector3.one * scale;
                yield return null;
            }
            btnTransform.localScale = Vector3.one;

            isProcessing = false;
        }

        /// <summary>
        /// 비활성화 시 버튼 색상 복원 애니메이션
        /// </summary>
        private IEnumerator ButtonDeactivateAnim()
        {
            if (reverseButton == null) yield break;

            var img = reverseButton.GetComponent<Image>();
            if (img == null) yield break;

            Color currentColor = img.color;
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                img.color = Color.Lerp(currentColor, btnOriginalColor, eased);
                yield return null;
            }
            img.color = btnOriginalColor;
        }

        /// <summary>
        /// 버튼 주변 글로우 링 확장 + 페이드아웃
        /// </summary>
        private IEnumerator ButtonGlowRing()
        {
            if (reverseButton == null) yield break;

            Canvas canvas = reverseButton.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : reverseButton.transform.parent;

            GameObject glow = new GameObject("ReverseGlowRing");
            glow.transform.SetParent(parent, false);
            glow.transform.position = reverseButton.transform.position;

            var glowImg = glow.AddComponent<Image>();
            glowImg.raycastTarget = false;
            glowImg.sprite = HexBlock.GetHexFlashSprite();
            glowImg.type = Image.Type.Simple;
            glowImg.preserveAspect = true;
            glowImg.color = new Color(0.60f, 0.40f, 0.80f, 0.5f);

            RectTransform rt = glow.GetComponent<RectTransform>();
            float startSize = 110f;
            rt.sizeDelta = new Vector2(startSize, startSize);

            float duration = 0.35f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float size = startSize * (1f + eased * 0.8f);
                rt.sizeDelta = new Vector2(size, size);
                glowImg.color = new Color(0.60f, 0.40f, 0.80f, 0.5f * (1f - t));
                yield return null;
            }

            Destroy(glow);
        }

        // ============================================================
        // 정리
        // ============================================================

        /// <summary>다른 활성 아이템 비활성화 (오버레이 중첩 방지)</summary>
        private void DeactivateOtherItems()
        {
            var hammer = FindObjectOfType<HammerItem>();
            if (hammer != null && hammer.IsActive) hammer.Deactivate();
            var swap = FindObjectOfType<SwapItem>();
            if (swap != null && swap.IsActive) swap.Deactivate();
            var lineDraw = FindObjectOfType<LineDrawItem>();
            if (lineDraw != null && lineDraw.IsActive) lineDraw.Deactivate();
        }

        private void OnDestroy()
        {
            if (reverseButton != null)
                reverseButton.onClick.RemoveListener(OnButtonClicked);

            // 회전 이벤트 구독 해제
            if (rotationSystem != null)
            {
                rotationSystem.OnRotationStarted -= OnRotationStarted;
                rotationSystem.OnRotationComplete -= OnRotationCompleted;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Items
{
    /// <summary>
    /// 역회전 아이템 (1회성)
    /// 클릭 시 다음 회전 1회만 반시계로 실행하고 자동으로 시계방향으로 복귀합니다.
    /// 타겟 선택 불필요, 즉시 효과.
    /// </summary>
    public class ReverseRotationItem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button reverseButton;
        [SerializeField] private RectTransform arrowContainer;

        // 참조 (자동 탐색)
        private InputSystem inputSystem;
        private RotationSystem rotationSystem;

        // 피드백 진행 중 플래그
        private bool isProcessing = false;

        private void Start()
        {
            AutoFindReferences();
            SetupUI();
            UpdateDirectionIcon();
        }

        private void AutoFindReferences()
        {
            if (inputSystem == null) inputSystem = FindObjectOfType<InputSystem>();
            if (rotationSystem == null) rotationSystem = FindObjectOfType<RotationSystem>();
        }

        private void SetupUI()
        {
            if (reverseButton != null)
                reverseButton.onClick.AddListener(OnButtonClicked);
        }

        // ============================================================
        // 버튼 클릭 처리
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

            // 수량 체크
            if (ItemManager.Instance != null)
            {
                int count = ItemManager.Instance.GetItemCount(ItemType.ReverseRotation);
                if (count <= 0)
                {
                    // 구매 팝업 표시
                    if (GameManager.Instance != null)
                        GameManager.Instance.ShowItemPurchasePopup(ItemType.ReverseRotation);
                    return;
                }

                // 게임당 사용 제한 체크
                if (!ItemManager.Instance.CanUseItem(ItemType.ReverseRotation))
                {
                    Debug.Log("[ReverseRotationItem] 게임당 사용 제한 초과");
                    return;
                }
            }

            // 즉시 실행
            ExecuteReverse();
        }

        // ============================================================
        // 역회전 실행
        // ============================================================

        private void ExecuteReverse()
        {
            // 이미 1회성 역회전이 활성 상태면 무시
            if (rotationSystem != null && rotationSystem.IsOneTimeCounterClockwiseActive)
            {
                Debug.Log("[ReverseRotationItem] 이미 1회성 역회전 활성화 상태");
                return;
            }

            // 1. 1회성 반시계 회전 설정 (토글이 아닌 1회성)
            if (inputSystem != null)
                inputSystem.SetOneTimeCounterClockwise();

            // 2. 아이템 소모
            if (ItemManager.Instance != null)
                ItemManager.Instance.ConsumeItem(ItemType.ReverseRotation);

            // 3. 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayButtonClick();

            // 4. 아이콘 업데이트 (1회성이므로 항상 반시계 표시)
            UpdateDirectionIcon();

            // 5. 버튼 피드백 VFX + 1회성 활성 글로우
            StartCoroutine(ButtonFeedback());
            StartCoroutine(ReverseActiveGlow());

            Debug.Log("[ReverseRotationItem] 1회성 반시계 회전 활성화");
        }

        // ============================================================
        // 방향 아이콘 업데이트
        // ============================================================

        /// <summary>
        /// 화살표 방향 아이콘 업데이트
        /// CW: scaleX = 1 (기본), CCW: scaleX = -1 (좌우 반전)
        /// </summary>
        private void UpdateDirectionIcon()
        {
            if (arrowContainer == null) return;
            bool clockwise = rotationSystem != null && rotationSystem.IsClockwise;
            arrowContainer.localScale = clockwise
                ? new Vector3(1f, 1f, 1f)
                : new Vector3(-1f, 1f, 1f);
        }

        /// <summary>
        /// 1회성 역회전 활성 시 버튼 글로우 유지 (회전 완료까지)
        /// </summary>
        private IEnumerator ReverseActiveGlow()
        {
            if (reverseButton == null) yield break;

            var img = reverseButton.GetComponent<Image>();
            if (img == null) yield break;

            Color originalColor = img.color;
            Color activeColor = new Color(0.80f, 0.55f, 1f, 1f);

            // 1회성 역회전 활성 동안 펄스 글로우
            while (rotationSystem != null && rotationSystem.IsOneTimeCounterClockwiseActive)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 4f);
                img.color = Color.Lerp(originalColor, activeColor, pulse);
                yield return null;
            }

            // 역회전 완료 → 색상 복원 + 아이콘 방향 복원
            img.color = originalColor;
            UpdateDirectionIcon();
        }

        // ============================================================
        // 버튼 피드백 VFX
        // ============================================================

        /// <summary>
        /// 클릭 시 버튼 피드백: 펄스 + 화살표 스핀 + 글로우 링
        /// </summary>
        private IEnumerator ButtonFeedback()
        {
            isProcessing = true;

            if (reverseButton == null)
            {
                isProcessing = false;
                yield break;
            }

            Transform btnTransform = reverseButton.transform;
            var img = reverseButton.GetComponent<Image>();

            // 색상 플래시 (보라색 → 밝은 보라)
            Color originalColor = img != null ? img.color : Color.white;
            Color flashColor = new Color(0.80f, 0.55f, 1f, 1f);
            if (img != null)
                img.color = flashColor;

            // 글로우 링 (비동기)
            StartCoroutine(ButtonGlowRing());

            // 스케일 펄스 (1.0 → 1.2 → 1.0)
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

            // 화살표 180° 스핀 애니메이션
            if (arrowContainer != null)
            {
                float spinDur = 0.25f;
                elapsed = 0f;
                float startAngle = arrowContainer.localEulerAngles.z;
                while (elapsed < spinDur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / spinDur);
                    float eased = VisualConstants.EaseOutCubic(t);
                    arrowContainer.localRotation = Quaternion.Euler(0, 0, startAngle + 180f * eased);
                    yield return null;
                }
                // 최종 각도 정리 (0 또는 180 → 360=0으로 정규화)
                float finalAngle = (startAngle + 180f) % 360f;
                arrowContainer.localRotation = Quaternion.Euler(0, 0, finalAngle);
            }

            // 색상 복원 페이드
            if (img != null)
            {
                float fadeDur = 0.15f;
                elapsed = 0f;
                while (elapsed < fadeDur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDur);
                    img.color = Color.Lerp(flashColor, originalColor, VisualConstants.EaseOutCubic(t));
                    yield return null;
                }
                img.color = originalColor;
            }

            isProcessing = false;
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

        private void OnDestroy()
        {
            if (reverseButton != null)
                reverseButton.onClick.RemoveListener(OnButtonClicked);
        }
    }
}

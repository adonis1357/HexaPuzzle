using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 콤보 표시 UI
    /// ScoreManager.OnComboChanged 이벤트를 구독하여 자동 표시
    /// 화면 상단 HUD 아래에 콤보 텍스트를 에스컬레이팅 비주얼로 표시
    /// </summary>
    public class ComboDisplay : MonoBehaviour
    {
        private Text comboText;
        private Outline comboOutline;
        private RectTransform comboRect;
        private CanvasGroup canvasGroup;

        private ScoreManager scoreManager;
        private Coroutine animCoroutine;
        private Coroutine hideCoroutine;

        private int lastCombo = 0;

        private void Start()
        {
            CreateUI();

            scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager != null)
            {
                scoreManager.OnComboChanged += OnComboChanged;
            }
        }

        private void OnDestroy()
        {
            if (scoreManager != null)
            {
                scoreManager.OnComboChanged -= OnComboChanged;
            }
        }

        private void CreateUI()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            // 콤보 텍스트 오브젝트 생성
            GameObject go = new GameObject("ComboDisplayText");
            go.transform.SetParent(canvas.transform, false);

            comboRect = go.AddComponent<RectTransform>();
            comboRect.anchorMin = new Vector2(0.5f, 1f);
            comboRect.anchorMax = new Vector2(0.5f, 1f);
            comboRect.pivot = new Vector2(0.5f, 1f);
            comboRect.anchoredPosition = new Vector2(0f, -120f);
            comboRect.sizeDelta = new Vector2(300f, 60f);

            comboText = go.AddComponent<Text>();
            comboText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            comboText.alignment = TextAnchor.MiddleCenter;
            comboText.horizontalOverflow = HorizontalWrapMode.Overflow;
            comboText.verticalOverflow = VerticalWrapMode.Overflow;
            comboText.raycastTarget = false;
            comboText.text = "";

            comboOutline = go.AddComponent<Outline>();
            comboOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            comboOutline.effectDistance = new Vector2(2f, -2f);

            canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        private void OnComboChanged(int combo)
        {
            if (combo < 2)
            {
                // 콤보 종료: 페이드아웃
                if (lastCombo >= 2)
                {
                    if (hideCoroutine != null) StopCoroutine(hideCoroutine);
                    hideCoroutine = StartCoroutine(FadeOut());
                }
                lastCombo = combo;
                return;
            }

            lastCombo = combo;

            // 숨김 타이머 리셋
            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(AutoHide());

            // 등장 애니메이션
            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(ShowCombo(combo));
        }

        private IEnumerator ShowCombo(int combo)
        {
            if (comboText == null) yield break;

            // 콤보 레벨별 비주얼
            string suffix = combo >= 4 ? "!" : "";
            comboText.text = $"COMBO x{combo}{suffix}";

            Color targetColor;
            float targetSize;

            if (combo >= 4)
            {
                targetColor = new Color(1f, 0.27f, 0f); // Red-Orange
                targetSize = 44f;
                comboText.fontStyle = FontStyle.Bold;
            }
            else if (combo >= 3)
            {
                targetColor = new Color(1f, 0.55f, 0f); // Orange
                targetSize = 38f;
                comboText.fontStyle = FontStyle.Bold;
            }
            else
            {
                targetColor = new Color(1f, 0.84f, 0f); // Yellow
                targetSize = 32f;
                comboText.fontStyle = FontStyle.Normal;
            }

            comboText.fontSize = (int)targetSize;
            comboText.color = targetColor;

            // 등장 애니메이션: EaseOutBack scale 1.3 → 1.0
            float duration = VisualConstants.ComboAnimDuration;
            float elapsed = 0f;
            canvasGroup.alpha = 1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float eased = VisualConstants.EaseOutBack(t);
                float scale = Mathf.Lerp(VisualConstants.ComboScaleIn, 1f, eased);
                comboRect.localScale = Vector3.one * scale;

                yield return null;
            }

            comboRect.localScale = Vector3.one;

            // x4+ 셰이크 효과
            if (combo >= 4)
            {
                float shakeDuration = 0.15f;
                float shakeElapsed = 0f;
                Vector2 basePos = comboRect.anchoredPosition;

                while (shakeElapsed < shakeDuration)
                {
                    shakeElapsed += Time.deltaTime;
                    float shakeT = shakeElapsed / shakeDuration;
                    float intensity = 4f * (1f - shakeT);
                    comboRect.anchoredPosition = basePos + new Vector2(
                        Random.Range(-intensity, intensity),
                        Random.Range(-intensity, intensity)
                    );
                    yield return null;
                }

                comboRect.anchoredPosition = basePos;
            }
        }

        private IEnumerator AutoHide()
        {
            yield return new WaitForSeconds(VisualConstants.ComboIdleTimeout);
            yield return StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            if (canvasGroup == null) yield break;

            float duration = 0.3f;
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }
    }
}

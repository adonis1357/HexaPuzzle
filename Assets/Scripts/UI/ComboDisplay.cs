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
            comboRect.anchoredPosition = new Vector2(0f, -270f);
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

            // 기준 위치 (아래에서 시작, 위로 올라감)
            Vector2 basePos = new Vector2(0f, -270f);
            float riseDistance = 30f + combo * 5f; // 콤보 높을수록 더 위로
            Vector2 startPos = basePos;
            Vector2 endPos = basePos + new Vector2(0f, riseDistance);

            // 바운스 스케일 파라미터 (콤보 레벨별)
            float peakScale = combo >= 4 ? 1.6f : (combo >= 3 ? 1.45f : 1.3f);

            // Phase 1: 빠른 팽창 + 상승 (0.1초)
            float expandDur = 0.1f;
            float elapsed = 0f;
            canvasGroup.alpha = 1f;
            comboRect.localScale = Vector3.zero;
            comboRect.anchoredPosition = startPos;

            while (elapsed < expandDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / expandDur);
                // EaseOutQuad: 빠르게 시작 → 감속
                float eased = 1f - (1f - t) * (1f - t);
                float scale = Mathf.Lerp(0.3f, peakScale, eased);
                comboRect.localScale = Vector3.one * scale;
                comboRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased * 0.7f);
                yield return null;
            }

            // Phase 2: 바운스 수축 (peakScale → 1.0, 오버슈팅 바운스, 0.2초)
            float bounceDur = 0.2f;
            elapsed = 0f;

            while (elapsed < bounceDur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDur);
                // 감쇠 바운스: 큰 스케일에서 1.0으로 수렴, 중간에 한 번 0.9까지 찍음
                float bounce = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t) * 0.2f;
                float scale = Mathf.Lerp(peakScale, 1f, t) + bounce;
                comboRect.localScale = Vector3.one * scale;
                // 나머지 상승 마무리
                comboRect.anchoredPosition = Vector2.Lerp(
                    startPos + new Vector2(0f, riseDistance * 0.7f),
                    endPos, t);
                yield return null;
            }

            comboRect.localScale = Vector3.one;
            comboRect.anchoredPosition = endPos;

            // x4+ 셰이크 효과
            if (combo >= 4)
            {
                float shakeDuration = 0.15f;
                float shakeElapsed = 0f;

                while (shakeElapsed < shakeDuration)
                {
                    shakeElapsed += Time.deltaTime;
                    float shakeT = shakeElapsed / shakeDuration;
                    float intensity = 5f * (1f - shakeT);
                    comboRect.anchoredPosition = endPos + new Vector2(
                        Random.Range(-intensity, intensity),
                        Random.Range(-intensity, intensity)
                    );
                    yield return null;
                }

                comboRect.anchoredPosition = endPos;
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

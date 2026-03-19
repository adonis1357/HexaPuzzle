using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// MP 게이지 UI — 프로시저럴 육각형 통에 파란색 액체처럼 채워지는 게이지
    /// SDF 기반 flat-top 육각형, 아래→위 방향으로 채움 비율 표현
    /// </summary>
    public class MPGaugeUI : MonoBehaviour
    {
        // ============================================================
        // 상수
        // ============================================================
        private const int TEX_SIZE = 256;           // 스프라이트 텍스처 크기
        private const float GAUGE_SIZE = 70f;       // UI 크기 (픽셀)
        private const float FILL_ANIM_DURATION = 0.3f; // 채움 애니메이션 시간
        private const float MIN_FILL_CHANGE = 0.005f;  // 텍스처 재생성 최소 변화량

        // 색상 상수
        private static readonly Color BG_COLOR = new Color(0.12f, 0.14f, 0.22f, 0.85f);     // 빈 통 배경
        private static readonly Color FILL_COLOR_TOP = new Color(0.25f, 0.60f, 0.95f, 0.95f); // 액체 상단 (밝은 파랑)
        private static readonly Color FILL_COLOR_BOTTOM = new Color(0.15f, 0.35f, 0.75f, 0.95f); // 액체 하단 (진한 파랑)
        private static readonly Color BORDER_COLOR = new Color(0.55f, 0.65f, 0.85f, 0.9f);   // 테두리
        private static readonly Color LOW_MP_COLOR = new Color(0.95f, 0.3f, 0.25f, 0.95f);   // MP 부족 경고색

        // ============================================================
        // UI 요소
        // ============================================================
        private Image backgroundImage;
        private Image fillImage;
        private Image borderImage;
        private Text mpText;
        private RectTransform gaugeRect;

        // ============================================================
        // 상태
        // ============================================================
        private float displayedFillRatio = 1f;  // 현재 표시 중인 비율
        private float targetFillRatio = 1f;     // 목표 비율
        private Coroutine fillAnimCoroutine;
        private Sprite lastFillSprite;          // 재사용 방지용

        // 캐시된 스프라이트
        private Sprite bgSprite;
        private Sprite borderSprite;

        // ============================================================
        // 초기화
        // ============================================================

        /// <summary>
        /// MP 게이지 UI 초기화 — Canvas 하위에 생성
        /// </summary>
        public void Initialize(Transform parent)
        {
            // 루트 컨테이너
            gaugeRect = gameObject.GetComponent<RectTransform>();
            if (gaugeRect == null)
                gaugeRect = gameObject.AddComponent<RectTransform>();

            gaugeRect.SetParent(parent, false);
            gaugeRect.anchorMin = new Vector2(0.5f, 1f);
            gaugeRect.anchorMax = new Vector2(0.5f, 1f);
            gaugeRect.pivot = new Vector2(0.5f, 1f);
            gaugeRect.anchoredPosition = new Vector2(260f, -132f); // 이동횟수 프레임 오른쪽
            gaugeRect.sizeDelta = new Vector2(GAUGE_SIZE * 2f, GAUGE_SIZE * 2f);

            // 1. 배경 (어두운 빈 통)
            bgSprite = CreateHexBackgroundSprite(TEX_SIZE);
            GameObject bgObj = new GameObject("MPGauge_BG");
            bgObj.transform.SetParent(gaugeRect, false);
            backgroundImage = bgObj.AddComponent<Image>();
            backgroundImage.sprite = bgSprite;
            backgroundImage.color = Color.white;
            backgroundImage.raycastTarget = false;
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // 2. 채움 (파란색 액체)
            GameObject fillObj = new GameObject("MPGauge_Fill");
            fillObj.transform.SetParent(gaugeRect, false);
            fillImage = fillObj.AddComponent<Image>();
            fillImage.raycastTarget = false;
            RectTransform fillRt = fillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // 3. 테두리 (밝은 베벨)
            borderSprite = CreateHexBorderSprite(TEX_SIZE);
            GameObject borderObj = new GameObject("MPGauge_Border");
            borderObj.transform.SetParent(gaugeRect, false);
            borderImage = borderObj.AddComponent<Image>();
            borderImage.sprite = borderSprite;
            borderImage.color = Color.white;
            borderImage.raycastTarget = false;
            RectTransform borderRt = borderObj.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            // 4. MP 숫자 텍스트
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject textObj = new GameObject("MPGauge_Text");
            textObj.transform.SetParent(gaugeRect, false);
            mpText = textObj.AddComponent<Text>();
            mpText.font = font;
            mpText.fontSize = 16;
            mpText.fontStyle = FontStyle.Bold;
            mpText.alignment = TextAnchor.MiddleCenter;
            mpText.color = Color.white;
            mpText.raycastTarget = false;
            mpText.horizontalOverflow = HorizontalWrapMode.Overflow;
            mpText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // 텍스트 아웃라인 (가독성)
            Outline textOutline = textObj.AddComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            textOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // MP 라벨 (하단)
            GameObject labelObj = new GameObject("MPGauge_Label");
            labelObj.transform.SetParent(gaugeRect, false);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 10;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = new Color(0.75f, 0.85f, 1f, 0.8f);
            labelText.raycastTarget = false;
            labelText.text = "MP";
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0.25f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            // 초기 채움
            UpdateFillSprite(1f);
            UpdateText(100, 100);

            // MPManager 이벤트 구독
            if (MPManager.Instance != null)
            {
                MPManager.Instance.OnMPChanged += OnMPChanged;
            }

            Debug.Log("[MPGaugeUI] 초기화 완료");
        }

        private void OnDestroy()
        {
            if (MPManager.Instance != null)
            {
                MPManager.Instance.OnMPChanged -= OnMPChanged;
            }

            // 스프라이트 정리
            if (bgSprite != null) Destroy(bgSprite.texture);
            if (borderSprite != null) Destroy(borderSprite.texture);
            if (lastFillSprite != null) Destroy(lastFillSprite.texture);
        }

        // ============================================================
        // MP 갱신
        // ============================================================

        /// <summary>
        /// MPManager.OnMPChanged 이벤트 핸들러
        /// </summary>
        private void OnMPChanged(int current, int max)
        {
            UpdateGauge(current, max);
        }

        /// <summary>
        /// 게이지 갱신 (애니메이션 포함)
        /// </summary>
        public void UpdateGauge(int current, int max)
        {
            targetFillRatio = max > 0 ? (float)current / max : 0f;
            UpdateText(current, max);

            // 비활성 상태에서는 코루틴 사용 불가 → 즉시 값 적용
            if (!gameObject.activeInHierarchy)
            {
                displayedFillRatio = targetFillRatio;
                return;
            }

            // 채움 애니메이션
            if (fillAnimCoroutine != null)
                StopCoroutine(fillAnimCoroutine);
            fillAnimCoroutine = StartCoroutine(AnimateFill(displayedFillRatio, targetFillRatio));
        }

        private void UpdateText(int current, int max)
        {
            if (mpText != null)
            {
                mpText.text = current.ToString();

                // MP 20% 이하 시 빨간색 경고
                float ratio = max > 0 ? (float)current / max : 0f;
                mpText.color = ratio <= 0.2f ? LOW_MP_COLOR : Color.white;
            }
        }

        // ============================================================
        // 채움 애니메이션
        // ============================================================

        private IEnumerator AnimateFill(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < FILL_ANIM_DURATION)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutCubic(Mathf.Clamp01(elapsed / FILL_ANIM_DURATION));
                float currentRatio = Mathf.Lerp(from, to, t);
                UpdateFillSprite(currentRatio);
                yield return null;
            }

            UpdateFillSprite(to);
            displayedFillRatio = to;
            fillAnimCoroutine = null;
        }

        // ============================================================
        // MP 부족 피드백
        // ============================================================

        /// <summary>
        /// MP 부족 시 게이지 빨간 깜빡임 + 흔들림
        /// </summary>
        public void PlayInsufficientFeedback()
        {
            StartCoroutine(InsufficientFeedbackCoroutine());
        }

        private IEnumerator InsufficientFeedbackCoroutine()
        {
            if (gaugeRect == null) yield break;

            Vector2 originalPos = gaugeRect.anchoredPosition;
            float duration = 0.35f;
            float elapsed = 0f;
            int flashCount = 3;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 빨간 깜빡임
                int flashIndex = Mathf.FloorToInt(t * flashCount * 2);
                bool isFlash = (flashIndex % 2 == 0);
                if (borderImage != null)
                    borderImage.color = isFlash ? LOW_MP_COLOR : Color.white;

                // 좌우 흔들림
                float shake = Mathf.Sin(t * Mathf.PI * flashCount * 2) * 4f * (1f - t);
                gaugeRect.anchoredPosition = originalPos + new Vector2(shake, 0);

                yield return null;
            }

            // 원래 상태 복원
            if (borderImage != null)
                borderImage.color = Color.white;
            if (gaugeRect != null)
                gaugeRect.anchoredPosition = originalPos;
        }

        // ============================================================
        // 프로시저럴 스프라이트 생성
        // ============================================================

        /// <summary>
        /// flat-top 육각형 SDF (HexBlock.HexSignedDistance와 동일 패턴)
        /// 내부=음수, 외부=양수
        /// </summary>
        private static float HexSDF(Vector2 point, Vector2 center, float radius)
        {
            Vector2 p = point - center;
            float maxDist = float.MinValue;
            for (int i = 0; i < 6; i++)
            {
                // flat-top 육각형: 30° + i*60°
                float angle = (30f + i * 60f) * Mathf.Deg2Rad;
                Vector2 normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float edgeDist = radius * 0.8660254f; // sqrt(3)/2
                float dist = Vector2.Dot(p, normal) - edgeDist;
                if (dist > maxDist) maxDist = dist;
            }
            return maxDist;
        }

        /// <summary>
        /// 배경 스프라이트 — 어두운 반투명 육각형 (빈 통 느낌)
        /// </summary>
        private static Sprite CreateHexBackgroundSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 4f;
            float aa = 2.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float dist = HexSDF(point, center, radius);
                    float alpha = Mathf.Clamp01(1f - dist / aa);

                    if (alpha > 0f)
                    {
                        // 중앙에서 가장자리로 갈수록 약간 밝아지는 그라데이션 (깊이감)
                        float normalizedDist = Mathf.Clamp01(-dist / (radius * 0.8660254f));
                        float brightness = Mathf.Lerp(0.85f, 1f, 1f - normalizedDist);
                        Color col = BG_COLOR * brightness;
                        col.a = BG_COLOR.a * alpha;
                        pixels[y * size + x] = col;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 채움 스프라이트 — 아래→위 방향으로 fillRatio만큼 파란색 액체
        /// </summary>
        private void UpdateFillSprite(float fillRatio)
        {
            fillRatio = Mathf.Clamp01(fillRatio);

            // 변화량이 너무 작으면 스킵 (성능 최적화)
            if (lastFillSprite != null && Mathf.Abs(fillRatio - displayedFillRatio) < MIN_FILL_CHANGE)
                return;

            Sprite newSprite = CreateHexFillSprite(TEX_SIZE, fillRatio);

            if (fillImage != null)
                fillImage.sprite = newSprite;

            // 이전 스프라이트 정리
            if (lastFillSprite != null)
            {
                Destroy(lastFillSprite.texture);
                Destroy(lastFillSprite);
            }
            lastFillSprite = newSprite;
            displayedFillRatio = fillRatio;
        }

        /// <summary>
        /// 육각형 채움 스프라이트 생성 — SDF 기반, 아래→위 채움
        /// </summary>
        private static Sprite CreateHexFillSprite(int size, float fillRatio)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 6f; // 배경보다 약간 작게 (안쪽)
            float aa = 2.5f;
            float apothem = radius * 0.8660254f;

            // 육각형의 상하 경계 (flat-top: 꼭지점이 좌우에 있으므로 상하가 apothem)
            float bottomY = center.y - apothem;
            float topY = center.y + apothem;
            float fillLine = bottomY + (topY - bottomY) * fillRatio;

            // 채움 상단 경계면의 물결 효과 파라미터
            float waveAmplitude = 2.5f;
            float waveFrequency = 3f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float hexDist = HexSDF(point, center, radius);

                    // 육각형 내부가 아니면 투명
                    if (hexDist > aa)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float hexAlpha = Mathf.Clamp01(1f - hexDist / aa);

                    // 물결 효과가 적용된 채움 라인
                    float wave = Mathf.Sin((float)x / size * Mathf.PI * waveFrequency) * waveAmplitude;
                    float effectiveFillLine = fillLine + wave;

                    // 채움 라인 아래이면 파란색
                    float fillDist = point.y - effectiveFillLine;
                    float fillAlpha = Mathf.Clamp01(1f - fillDist / aa);

                    if (fillAlpha > 0f && hexAlpha > 0f)
                    {
                        // 그라데이션: 아래→위 진한 파랑→밝은 파랑
                        float heightRatio = Mathf.Clamp01((point.y - bottomY) / (topY - bottomY));
                        Color fillColor = Color.Lerp(FILL_COLOR_BOTTOM, FILL_COLOR_TOP, heightRatio);

                        // 가장자리 하이라이트 (입체감)
                        float edgeHighlight = Mathf.Clamp01(-hexDist / (radius * 0.2f));
                        fillColor = Color.Lerp(fillColor, fillColor * 1.15f, 1f - edgeHighlight);

                        // 상단 표면 하이라이트 (액체 표면 반사)
                        float surfaceDist = Mathf.Abs(fillDist);
                        if (surfaceDist < 6f && fillDist <= 0f)
                        {
                            float surfaceHighlight = Mathf.Clamp01(1f - surfaceDist / 6f) * 0.25f;
                            fillColor = Color.Lerp(fillColor, Color.white, surfaceHighlight);
                        }

                        fillColor.a = fillAlpha * hexAlpha * fillColor.a;
                        pixels[y * size + x] = fillColor;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 테두리 스프라이트 — 밝은 베벨 육각형 외곽선
        /// </summary>
        private static Sprite CreateHexBorderSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius - 6f;  // 테두리 두께 6px
            float aa = 2.5f;

            // 조명 방향 (좌상단 → 우하단)
            Vector2 lightDir = new Vector2(-0.707f, 0.707f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    float outerDist = HexSDF(point, center, outerRadius);
                    float innerDist = HexSDF(point, center, innerRadius);

                    float outerAlpha = Mathf.Clamp01(1f - outerDist / aa);
                    float innerAlpha = Mathf.Clamp01(innerDist / aa);
                    float ringAlpha = outerAlpha * innerAlpha;

                    if (ringAlpha > 0.001f)
                    {
                        // 방향성 베벨 (입체감)
                        Vector2 dir = (point - center);
                        float dirLen = dir.magnitude;
                        if (dirLen > 0.001f) dir /= dirLen;
                        float lightDot = Vector2.Dot(dir, lightDir);
                        float bevel = 0.8f + lightDot * 0.15f;

                        Color col = BORDER_COLOR * bevel;
                        col.a = ringAlpha * BORDER_COLOR.a;
                        pixels[y * size + x] = col;
                    }
                    else
                    {
                        // 소프트 외곽 글로우
                        float glowAlpha = Mathf.Clamp01(1f - outerDist / (aa * 2.5f)) * 0.08f;
                        if (glowAlpha > 0.001f)
                        {
                            pixels[y * size + x] = new Color(BORDER_COLOR.r, BORDER_COLOR.g, BORDER_COLOR.b, glowAlpha);
                        }
                        else
                        {
                            pixels[y * size + x] = Color.clear;
                        }
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

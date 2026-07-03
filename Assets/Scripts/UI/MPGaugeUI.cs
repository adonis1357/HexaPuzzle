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
        private Text mpText;        // 중앙 값
        private Text mpLabelText;   // 하단 "MP  max N"
        private RectTransform gaugeRect;
        private LiquidGaugeSlosh liquidSlosh;   // 입체 액체 채움 + 출렁임 구동

        // ============================================================
        // 상태
        // ============================================================
        private float displayedFillRatio = 1f;  // 현재 표시 중인 비율
        private float targetFillRatio = 1f;     // 목표 비율
        private Coroutine fillAnimCoroutine;
        private Coroutine insufficientFeedbackCoroutine; // 부족 피드백 중복 방지
        private Coroutine decreaseFlashCoroutine;        // MP 감소 시 외곽선 빨간 플래시
        private int previousMP = -1;                     // 감소 감지용 (-1 = 미초기화)
        private Vector2 gaugeOriginalPos;       // 부족 피드백 복원용 원래 위치 (첫 호출 시 캡처)
        private bool gaugeOriginalPosCaptured = false;
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

            // ★ 입체 액체 게이지 — 클로드 디자인 PNG 레이어(유리BG→액체→표면→유리Front) + 출렁임.
            //   (기존 프로시저럴 SDF 3레이어를 대체. 채움·출렁임은 LiquidGaugeSlosh가 구동)
            var liquidTint = new Color(0.30f, 0.62f, 0.95f, 1f);   // MP 파란 액체
            var surfaceTint = new Color(0.82f, 0.92f, 1f, 0.95f);  // 표면 메니스커스(밝은 파랑)
            var refs = LiquidHexGaugeBuilder.Build(gaugeRect, liquidTint, surfaceTint);
            backgroundImage = refs.bg;
            fillImage = refs.fill;
            borderImage = refs.front;   // 감소/부족 플래시 틴트 대상(유리 림)
            liquidSlosh = refs.slosh;

            // 4. 공용 3-존 텍스트 (RW 게이지와 동일 구조·폰트) — 상단(단계, MP는 비움)/중앙(값)/하단(MP max N)
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var txt = LiquidHexGaugeBuilder.BuildText(gaugeRect, font, Color.white, new Color(0.78f, 0.86f, 1f, 0.9f));
            mpText = txt.center;        // 중앙 값
            mpLabelText = txt.bottom;   // 하단 "MP  max N"
            mpLabelText.text = "MP";    // max는 UpdateText에서 동적 반영
            // txt.top: MP는 단계 개념이 없어 비워둠

            // 초기 채움 (슬로시 컴포넌트가 채움·출렁임 구동)
            if (liquidSlosh != null) liquidSlosh.SetTarget(1f, true);
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
            // ★ MP 감소 감지 → 외곽선 빨간 플래시 1초 페이드아웃
            //   (초기 호출은 previousMP=-1이라 감지 안 됨 — 정상 동작)
            if (previousMP >= 0 && current < previousMP)
            {
                TriggerDecreaseFlash();
            }
            previousMP = current;

            UpdateGauge(current, max);
        }

        /// <summary>
        /// MP 감소 시 외곽선 빨간 플래시 — 즉시 빨간색, 1초 동안 흰색으로 페이드.
        /// 부족 피드백(insufficientFeedback)이 진행 중이면 그 쪽이 우선 — 중첩 방지.
        /// </summary>
        private void TriggerDecreaseFlash()
        {
            if (borderImage == null) return;

            // 진행 중 부족 피드백이 있으면 그 쪽이 우선 (red flash + shake → 0.35초 후 자체 종료)
            if (insufficientFeedbackCoroutine != null) return;

            // 진행 중인 감소 플래시 중단 후 새로 시작 (연속 소모 시 매번 리셋)
            if (decreaseFlashCoroutine != null)
                StopCoroutine(decreaseFlashCoroutine);
            decreaseFlashCoroutine = StartCoroutine(DecreaseFlashCoroutine());
        }

        private IEnumerator DecreaseFlashCoroutine()
        {
            // 즉시 빨간색으로 점등
            borderImage.color = LOW_MP_COLOR;

            float duration = 1.0f;
            float elapsed = 0f;
            Color startColor = LOW_MP_COLOR;
            Color endColor = Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseOutCubic — 처음엔 빨간색 강하게, 후반부에 빠르게 흰색 복귀
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                // 부족 피드백이 시작되면 즉시 양보
                if (insufficientFeedbackCoroutine != null) yield break;

                borderImage.color = Color.Lerp(startColor, endColor, eased);
                yield return null;
            }

            borderImage.color = endColor;
            decreaseFlashCoroutine = null;
        }

        /// <summary>
        /// 게이지 갱신 — 목표 비율만 설정, 실제 채움은 Update()의 연속 보간이 따라간다.
        /// ★ 부드러운 증감 (기존: 0.3초 코루틴 재시작 방식 → 연속 변동 시 ease 곡선이 매번 리셋되어
        ///   체감상 뚝뚝 끊김. SmoothDamp 연속 보간으로 증가/감소 모두 흐르듯 표현)
        /// </summary>
        public void UpdateGauge(int current, int max)
        {
            targetFillRatio = max > 0 ? (float)current / max : 0f;
            UpdateText(current, max);
            // 채움·출렁임은 LiquidGaugeSlosh가 구동 (비활성 시 즉시 반영)
            if (liquidSlosh != null) liquidSlosh.SetTarget(targetFillRatio, !gameObject.activeInHierarchy);
        }

        private void UpdateText(int current, int max)
        {
            if (mpText != null)
            {
                JewelsHexaPuzzle.Utils.NumberRoller.Roll(mpText, current, v => v.ToString());

                // MP 20% 이하 시 빨간색 경고
                float ratio = max > 0 ? (float)current / max : 0f;
                mpText.color = ratio <= 0.2f ? LOW_MP_COLOR : Color.white;
            }
            // 하단: 라벨 + 최대치 ('MP  max N') — 맥스 값 변동 자동 반영
            if (mpLabelText != null) mpLabelText.text = $"MP  max {max}";
        }

        // (구 AnimateFill 코루틴 제거 — Update()의 SmoothDamp 연속 보간으로 대체)

        // ============================================================
        // MP 부족 피드백
        // ============================================================

        /// <summary>
        /// MP 부족 시 게이지 빨간 깜빡임 + 흔들림
        /// 연속 호출 시 진행 중 피드백을 즉시 중단하고 원래 위치 복원 후 새로 시작 → 위치 드리프트 방지
        /// </summary>
        public void PlayInsufficientFeedback()
        {
            if (gaugeRect == null) return;

            // 진행 중 피드백 중단 + 흔들리기 전 원래 위치로 강제 복원
            if (insufficientFeedbackCoroutine != null)
            {
                StopCoroutine(insufficientFeedbackCoroutine);
                insufficientFeedbackCoroutine = null;
                if (gaugeOriginalPosCaptured)
                    gaugeRect.anchoredPosition = gaugeOriginalPos;
                if (borderImage != null)
                    borderImage.color = Color.white;
            }

            // ★ 진행 중인 MP 감소 플래시도 중단 (부족 피드백이 우선)
            if (decreaseFlashCoroutine != null)
            {
                StopCoroutine(decreaseFlashCoroutine);
                decreaseFlashCoroutine = null;
                if (borderImage != null)
                    borderImage.color = Color.white;
            }

            // 원래 위치 1회만 캡처 (흔들림 중 캡처 방지)
            if (!gaugeOriginalPosCaptured)
            {
                gaugeOriginalPos = gaugeRect.anchoredPosition;
                gaugeOriginalPosCaptured = true;
            }

            insufficientFeedbackCoroutine = StartCoroutine(InsufficientFeedbackCoroutine());
        }

        private IEnumerator InsufficientFeedbackCoroutine()
        {
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
                gaugeRect.anchoredPosition = gaugeOriginalPos + new Vector2(shake, 0);

                yield return null;
            }

            // 원래 상태 복원
            if (borderImage != null)
                borderImage.color = Color.white;
            if (gaugeRect != null)
                gaugeRect.anchoredPosition = gaugeOriginalPos;

            insufficientFeedbackCoroutine = null;
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
        /// 채움 표시 갱신 — 아래→위 방향으로 fillRatio만큼 파란색 액체.
        /// ★ 텍스처 재생성 없이 Image.fillAmount만 조절 (감사 H1 성능 수정).
        ///   풀 채움 스프라이트에서 육각형이 차지하는 세로 구간(bottom~top apothem)으로 비율을 매핑한다.
        /// </summary>
        private void UpdateFillSprite(float fillRatio)
        {
            fillRatio = Mathf.Clamp01(fillRatio);
            if (fillImage == null) return;

            // ★ 가득 찬 상태(≈1.0)는 크롭 없이 전체 표시 — 육각형 상단 모서리에서 잘리지 않아
            //   "꽉 차 보이는" 상태 보장 (30/30인데 덜 차 보이던 문제 수정)
            if (fillRatio >= 0.999f)
            {
                fillImage.fillAmount = 1f;
                displayedFillRatio = fillRatio;
                return;
            }

            // 육각형 세로 구간 매핑 (CreateHexFillSprite와 동일 산식: radius=size/2-6, apothem=radius·√3/2)
            float radius = TEX_SIZE / 2f - 6f;
            float apothem = radius * 0.8660254f;
            float bottomNorm = (TEX_SIZE / 2f - apothem) / TEX_SIZE;
            float topNorm = (TEX_SIZE / 2f + apothem) / TEX_SIZE;
            fillImage.fillAmount = Mathf.Lerp(bottomNorm, topNorm, fillRatio);
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

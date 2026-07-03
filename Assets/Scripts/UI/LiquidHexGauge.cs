using UnityEngine;
using UnityEngine.UI;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 입체 육각 액체 게이지 — 클로드 디자인 PNG 레이어(유리BG→액체→표면→유리Front) + 출렁임.
    /// MP·RW 게이지 공용. 액체는 fillAmount(세로 Filled)로 채우고, 표면 메니스커스 밴드가 채움선에서
    /// 출렁(스프링 기반 tilt/bob)인다. 채움 변화 속도가 출렁임 충격을 준다.
    /// </summary>
    public static class LiquidHexGaugeBuilder
    {
        public class Refs
        {
            public Image bg;        // 유리 BG(액체 뒤)
            public Image fill;      // 액체(Filled)
            public RectTransform surface;
            public Image front;     // 유리 림/광택(액체 위) — 감소 플래시용 틴트 대상
            public LiquidGaugeSlosh slosh;
        }

        // gauge_liquid 헥사 텍스처 세로 점유 구간 (svg y24~232 / 256)
        private const float FILL_BOTTOM_NORM = 0.094f;
        private const float FILL_TOP_NORM = 0.906f;

        private static Image AddLayer(RectTransform root, string name, string sprite, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            var spr = Resources.Load<Sprite>(sprite);
            if (spr != null) img.sprite = spr;
            img.color = col; img.raycastTarget = false;
            return img;
        }

        /// <summary>root(정사각 RectTransform) 아래 액체 게이지 레이어 생성 + 슬로시 컴포넌트 부착.</summary>
        public static Refs Build(RectTransform root, Color liquidTint, Color surfaceTint)
        {
            var r = new Refs();
            float size = root.sizeDelta.x;

            // 1. 유리 BG (액체 뒤)
            r.bg = AddLayer(root, "Gauge_GlassBG", "UI/gauge_glass", Color.white);

            // 2. 헥사 마스크 클립 컨테이너 (표면 밴드가 헥사 밖으로 안 삐져나오게)
            var clipGO = new GameObject("Gauge_LiquidClip");
            clipGO.transform.SetParent(root, false);
            var clipRt = clipGO.AddComponent<RectTransform>();
            clipRt.anchorMin = Vector2.zero; clipRt.anchorMax = Vector2.one; clipRt.offsetMin = Vector2.zero; clipRt.offsetMax = Vector2.zero;
            var clipImg = clipGO.AddComponent<Image>();
            var liqSpr = Resources.Load<Sprite>("UI/gauge_liquid");
            if (liqSpr != null) clipImg.sprite = liqSpr;
            clipImg.color = Color.white; clipImg.raycastTarget = false;
            var mask = clipGO.AddComponent<Mask>();
            mask.showMaskGraphic = false; // 마스크 헥사 자체는 안 그림(클립만)

            // 2a. 액체 (Filled 세로, 클립 자식)
            var fillGO = new GameObject("Gauge_Liquid");
            fillGO.transform.SetParent(clipRt, false);
            var fillRt = fillGO.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            r.fill = fillGO.AddComponent<Image>();
            if (liqSpr != null) r.fill.sprite = liqSpr;
            r.fill.color = liquidTint;
            r.fill.type = Image.Type.Filled;
            r.fill.fillMethod = Image.FillMethod.Vertical;
            r.fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            r.fill.raycastTarget = false;

            // 2b. 표면 메니스커스 밴드 (클립 자식)
            var surfGO = new GameObject("Gauge_Surface");
            surfGO.transform.SetParent(clipRt, false);
            r.surface = surfGO.AddComponent<RectTransform>();
            r.surface.anchorMin = r.surface.anchorMax = new Vector2(0.5f, 0.5f);
            r.surface.pivot = new Vector2(0.5f, 0.5f);
            r.surface.sizeDelta = new Vector2(size, size * 48f / 256f);
            var surfImg = surfGO.AddComponent<Image>();
            var surfSpr = Resources.Load<Sprite>("UI/gauge_surface");
            if (surfSpr != null) surfImg.sprite = surfSpr;
            surfImg.color = surfaceTint; surfImg.raycastTarget = false;

            // 3. 유리 Front (림/광택, 액체 위)
            r.front = AddLayer(root, "Gauge_GlassFront", "UI/gauge_glass_front", Color.white);

            // 4. 슬로시 컴포넌트
            r.slosh = root.gameObject.GetComponent<LiquidGaugeSlosh>();
            if (r.slosh == null) r.slosh = root.gameObject.AddComponent<LiquidGaugeSlosh>();
            r.slosh.Configure(r.fill, r.surface, surfImg, size, FILL_BOTTOM_NORM, FILL_TOP_NORM);
            return r;
        }

        /// <summary>MP/RW 게이지 공용 3-존 텍스트 — 상단(단계, 작게)/중앙(값, 크게)/하단(라벨+max, 작게).
        /// 두 게이지가 동일 폰트 크기·구조를 갖도록 한 곳에서 생성한다.</summary>
        public class TextRefs { public Text top, center, bottom; }
        public const int CENTER_FONT = 20;  // 중앙 값 (MP·RW 동일)
        public const int SUB_FONT = 11;     // 상단 단계 / 하단 라벨·max (MP·RW 동일)

        public static TextRefs BuildText(RectTransform root, Font font, Color centerColor, Color subColor)
        {
            var r = new TextRefs();
            r.top    = MakeZone(root, "Gauge_TopText",    font, SUB_FONT,    FontStyle.Bold, subColor,    0.62f, 0.90f);
            r.center = MakeZone(root, "Gauge_CenterText", font, CENTER_FONT, FontStyle.Bold, centerColor, 0.36f, 0.64f);
            r.bottom = MakeZone(root, "Gauge_BottomText", font, SUB_FONT,    FontStyle.Bold, subColor,    0.10f, 0.36f);
            return r;
        }

        private static Text MakeZone(RectTransform root, string name, Font font, int size, FontStyle style, Color col, float yMin, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.fontStyle = style; t.alignment = TextAnchor.MiddleCenter;
            t.color = col; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var ol = go.AddComponent<Outline>(); ol.effectColor = new Color(0f, 0f, 0f, 0.85f); ol.effectDistance = new Vector2(1.2f, -1.2f);
            return t;
        }
    }

    /// <summary>
    /// 액체 채움(fillAmount SmoothDamp) + 표면 출렁임(스프링) 구동. 외부는 SetTarget(ratio)만 호출.
    /// </summary>
    public class LiquidGaugeSlosh : MonoBehaviour
    {
        private Image fill;
        private RectTransform surface;
        private Image surfaceImg;
        private float rectH, bottomNorm = 0.094f, topNorm = 0.906f;

        private float displayedFill = 1f, targetFill = 1f, fillVel;
        private float sloshPos, sloshVel;
        private float surfBaseA = 1f;
        private float surfBaseH = 26f;   // 수면 밴드 원본 높이(px) — Configure에서 캡처

        private const float FILL_SMOOTH = 0.28f;
        private const float SLOSH_STIFF = 135f;   // 스프링 강성(복원) — 약 1.85Hz 출렁
        private const float SLOSH_DAMP = 5.5f;    // 감쇠(약 1초 내 정착)
        private const float MOTION_COUPLE = 11f;   // 채움 속도 → 출렁 충격 결합(표현력↑, 0.7변화→amp~0.19)

        public void Configure(Image f, RectTransform s, Image sImg, float rectHeight, float bNorm, float tNorm)
        {
            fill = f; surface = s; surfaceImg = sImg; rectH = rectHeight; bottomNorm = bNorm; topNorm = tNorm;
            if (surfaceImg != null) surfBaseA = surfaceImg.color.a;
            if (surface != null && surface.sizeDelta.y > 1f) surfBaseH = surface.sizeDelta.y;
            Apply();
        }

        public void SetTarget(float ratio, bool immediate = false)
        {
            targetFill = Mathf.Clamp01(ratio);
            if (immediate) { displayedFill = targetFill; fillVel = 0f; sloshPos = 0f; sloshVel = 0f; Apply(); }
        }

        public float DisplayedFill => displayedFill;
        public float SloshPos => sloshPos; // 검증용

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // 채움 보간
            if (!Mathf.Approximately(displayedFill, targetFill))
            {
                displayedFill = Mathf.SmoothDamp(displayedFill, targetFill, ref fillVel, FILL_SMOOTH, Mathf.Infinity, dt);
                if (Mathf.Abs(displayedFill - targetFill) < 0.0015f) { displayedFill = targetFill; fillVel = 0f; }
            }

            // 출렁임 스프링 — 채움 속도(fillVel)가 액체에 관성 충격, 스프링 복원 + 감쇠로 settle
            sloshVel += fillVel * MOTION_COUPLE * dt;
            sloshVel += -SLOSH_STIFF * sloshPos * dt;
            sloshVel *= Mathf.Clamp01(1f - SLOSH_DAMP * dt);
            sloshPos += sloshVel * dt;
            if (Mathf.Abs(sloshPos) < 0.00005f && Mathf.Abs(sloshVel) < 0.0005f) { sloshPos = 0f; sloshVel = 0f; }

            Apply();
        }

        private void Apply()
        {
            if (fill == null) return;
            float fa = (displayedFill >= 0.999f) ? 1f : Mathf.Lerp(bottomNorm, topNorm, displayedFill);
            fill.fillAmount = fa;

            if (surface != null)
            {
                // ★ 수면 밴드 — 채움선(액체 윗면)에 얹히게 + 프레임 밖 가로 삐짐 방지.
                //   ① 밴드 상단을 채움선에 맞춤(중심을 bandHalf만큼 아래로) → 채움선보다 높게 보이던 문제 수정.
                //   ② 헥사 천장 안쪽으로 클램프 + 밴드 폭을 그 높이의 헥사 폭(flat-top 선형)에 동적 맞춤 → 프레임 밖 가로 라인 제거.
                float apothem = rectH * (topNorm - 0.5f);          // 액체 헥사 반높이(px)
                if (apothem < 1f) apothem = rectH * 0.4f;
                float fillLineY = rectH * (fa - 0.5f);             // 액체 윗면 y(채움선)
                // ① 가시 물결선(메니스커스)을 채움선에 정렬 — 전 구간 일치.
                //   gauge_surface 밝은 밴드는 스프라이트 y9~35(피크 y21). 피크(0.0625)를 채움선에 두면 밴드 윗부분(y9~21)이
                //   채움선 위 빈 유리로 ~0.31밴드높이 삐져나와 "수면 위에 떠 보임"(사용자 보고). → 밴드의 '단단한 상단'(알파>0.5
                //   y15 = 0.1875 위)을 채움선에 맞춰, 밝은 수면선이 실제 액체 윗면에 붙고 광택은 아래(액체쪽)로 흐르게 한다.
                // ★ 수면 정렬 전면 재작성(2026-07-02 사용자 "여전히 안 맞음 — 중심 배치 + 양끝 채움") — 스프라이트 실측 기반:
                //   [실측] gauge_liquid 알파 헥사: 중앙 최대폭 0.9375×rect, 천장/바닥 폭 0.477×rect → 테이퍼 0.4917(선형).
                //          알파 세로범위는 topNorm/bottomNorm과 정확히 일치(마스크 신뢰 가능).
                //   [실측] gauge_surface: 가시 수면선 = 이미지 세로 정중앙(y23/48), 가로 가시폭 = 이미지의 97%.
                //   ① 수면 이미지 "중심"을 채움선에 정렬(경계선 중앙 배치 — 구 0.30 오프셋 폐기).
                float meniscusY = Mathf.Clamp(fillLineY, -apothem + 1f, apothem - 1f);
                //   ② 밴드 폭 = 채움선 y의 실측 헥사 내폭 × (1/0.97) — 가시 수면이 양 끝 벽까지 정확히 채움.
                //      (마스크 알파가 정확하므로 미세 초과분은 벽에서 깔끔히 클립.)
                float frac = Mathf.Clamp01(Mathf.Abs(meniscusY) / apothem);
                float bandW = rectH * 0.9375f * (1f - 0.4917f * frac) * 1.031f;
                surface.sizeDelta = new Vector2(bandW, surfBaseH);
                surface.anchoredPosition = new Vector2(0f, meniscusY + sloshPos * 42f);  // 출렁 상하 bob
                //   ③ 틸트는 가장자리에서 감쇠(마스크가 클립하지만 회전 시 수면선 어긋남 최소화).
                float tiltScale = 1f - 0.6f * frac;
                surface.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(sloshPos * 26f * tiltScale, -13f, 13f));
                float sq = 1f + Mathf.Min(Mathf.Abs(sloshPos) * 0.4f, 0.28f);
                surface.localScale = new Vector3(1f, sq, 1f);
                if (surfaceImg != null)
                {
                    // ★ 모든 경우 수면 표시(사용자 요청) — 기존 저채움 페이드(displayedFill*9) 제거.
                    var c = surfaceImg.color;
                    c.a = surfBaseA;
                    surfaceImg.color = c;
                }
            }
        }
    }
}

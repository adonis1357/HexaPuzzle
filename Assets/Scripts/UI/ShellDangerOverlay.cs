using UnityEngine;
using UnityEngine.UI;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 화면 외곽 붉은 위험 표시 — 클로드 디자인 유기적 비네트(Resources/UI/danger_vignette).
    /// 회색(쉘) 블록이 필드의 일정 비율 이상을 차지하면 화면 가장자리에 부드러운 붉은 글로우가
    /// 번지며 "필드 잠식" 위험을 알린다. 하드한 4면 띠/코너 프레임 → 단일 소프트 비네트로 교체.
    ///
    ///   - &lt;50%: 비표시 (alpha 0)
    ///   - 50~65%: 약 / 65~80%: 중 / 80%+: 강 (alpha 상승)
    ///   - 50%+ 호흡 펄스 (느린 sin, 비율 ↑일수록 강해짐)
    ///
    /// ★ 레이어: HUD/UI 밑(뒤)에 표시 — 인게임 배경 바로 위에 배치해 보드·HUD보다 뒤로 간다
    ///   (사용자 요청: "위험 표시를 UI 밑에다"). 입력 차단 안 함(raycastTarget=false).
    /// </summary>
    public class ShellDangerOverlay : MonoBehaviour
    {
        // 임계값
        private const float WEAK_THRESHOLD     = 0.50f;
        private const float MODERATE_THRESHOLD = 0.65f;
        private const float STRONG_THRESHOLD   = 0.80f;

        private const float ALPHA_AT_WEAK     = 0.35f;
        private const float ALPHA_AT_MODERATE = 0.60f;
        private const float ALPHA_AT_STRONG   = 0.82f;
        private const float ALPHA_AT_MAX      = 1.00f;

        // 펄스 (50% 비율부터 활성, 50%→100% 연속 보간)
        private const float PULSE_SPEED_MIN     = 0.30f;
        private const float PULSE_SPEED_MAX     = 0.70f;
        private const float PULSE_AMPLITUDE_MIN = 0.10f;
        private const float PULSE_AMPLITUDE_MAX = 0.28f;

        private const float SMOOTH_LERP_RATE = 2.5f;

        private HexGrid hexGrid;
        private RectTransform rootRt;
        private Image vignette;

        private float smoothedAlpha = 0f;
        private float currentRatio = 0f;

        public void Initialize(Transform parent)
        {
            hexGrid = FindObjectOfType<HexGrid>();

            rootRt = gameObject.GetComponent<RectTransform>();
            if (rootRt == null) rootRt = gameObject.AddComponent<RectTransform>();
            rootRt.SetParent(parent, false);
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.localScale = Vector3.one;

            // 단일 풀스크린 비네트 이미지
            var img = gameObject.GetComponent<Image>();
            if (img == null) img = gameObject.AddComponent<Image>();
            vignette = img;
            var sprite = Resources.Load<Sprite>("UI/danger_vignette");
            if (sprite == null)
                Debug.LogWarning("[ShellDangerOverlay] Resources/UI/danger_vignette 로드 실패 — 위험 표시 생략");
            vignette.sprite = sprite;
            vignette.type = Image.Type.Simple;
            vignette.preserveAspect = false;   // 풀스크린 스트레치
            vignette.raycastTarget = false;
            vignette.color = new Color(1f, 1f, 1f, 0f); // 베이크된 색을 그대로 쓰고 alpha만 제어

            // ★ HUD/UI 밑(뒤) 배치 — 인게임 배경 바로 위(보드·HUD보다 뒤)
            Transform bg = parent.Find("InGameBackground");
            if (bg != null) rootRt.SetSiblingIndex(bg.GetSiblingIndex() + 1);
            else rootRt.SetAsFirstSibling();
        }

        // 쉘 비율은 블록 제거/변환 시에만 변함 → 0.3초 주기 샘플링
        private const float RATIO_SAMPLE_INTERVAL = 0.3f;
        private float _nextRatioSampleTime = 0f;
        private float _lastAppliedAlpha = -1f;

        private void Update()
        {
            if (vignette == null) return;
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid == null) return;
            }

            if (Time.unscaledTime >= _nextRatioSampleTime)
            {
                _nextRatioSampleTime = Time.unscaledTime + RATIO_SAMPLE_INTERVAL;
                int total = 0, shell = 0;
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    total++;
                    if (block.Data.isShell) shell++;
                }
                currentRatio = total > 0 ? (float)shell / total : 0f;
            }

            float targetAlpha = ComputeAlpha(currentRatio);
            smoothedAlpha = Mathf.Lerp(smoothedAlpha, targetAlpha, Time.unscaledDeltaTime * SMOOTH_LERP_RATE);
            if (Mathf.Abs(smoothedAlpha - targetAlpha) < 0.002f) smoothedAlpha = targetAlpha;

            float pulse = 0f;
            if (currentRatio >= WEAK_THRESHOLD)
            {
                float t = Mathf.InverseLerp(WEAK_THRESHOLD, 1.0f, currentRatio);
                float speed = Mathf.Lerp(PULSE_SPEED_MIN, PULSE_SPEED_MAX, t);
                float amp = Mathf.Lerp(PULSE_AMPLITUDE_MIN, PULSE_AMPLITUDE_MAX, t);
                pulse = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * speed) * amp;
            }

            float finalAlpha = Mathf.Clamp01(smoothedAlpha + pulse);
            if (Mathf.Approximately(finalAlpha, _lastAppliedAlpha)) return; // 유휴 가드
            _lastAppliedAlpha = finalAlpha;

            Color c = vignette.color;
            c.a = finalAlpha;
            vignette.color = c;
        }

        private static float ComputeAlpha(float ratio)
        {
            if (ratio < WEAK_THRESHOLD) return 0f;
            if (ratio < MODERATE_THRESHOLD)
            {
                float t = Mathf.InverseLerp(WEAK_THRESHOLD, MODERATE_THRESHOLD, ratio);
                return Mathf.Lerp(ALPHA_AT_WEAK, ALPHA_AT_MODERATE, t);
            }
            if (ratio < STRONG_THRESHOLD)
            {
                float t = Mathf.InverseLerp(MODERATE_THRESHOLD, STRONG_THRESHOLD, ratio);
                return Mathf.Lerp(ALPHA_AT_MODERATE, ALPHA_AT_STRONG, t);
            }
            float tt = Mathf.InverseLerp(STRONG_THRESHOLD, 1.0f, ratio);
            return Mathf.Lerp(ALPHA_AT_STRONG, ALPHA_AT_MAX, tt);
        }
    }
}

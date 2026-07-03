using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 블록 제거 위치에 떠오르는 점수 팝업 관리
    /// ScoreManager.OnScorePopup 이벤트를 구독하여 자동 표시
    /// </summary>
    public class ScorePopupManager : MonoBehaviour
    {
        private const int POOL_SIZE = 12;

        private Canvas parentCanvas;
        private RectTransform canvasRect;
        private Camera uiCamera;

        private List<PopupItem> pool = new List<PopupItem>();
        private ScoreManager scoreManager;

        // ★ 페스티벌 모드 — 스테이지 클리어 후 마지막 드릴 발동 시 활성화
        //   기본 팝업보다 더 크고 화려하게 표현 (불꽃놀이 같은 마무리 연출)
        private bool festivalMode = false;
        public void SetFestivalMode(bool on) { festivalMode = on; }

        private class PopupItem
        {
            public GameObject go;
            public RectTransform rt;
            public Text text;
            public Outline outline;
            public bool inUse;
        }

        private void Start()
        {
            parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
                parentCanvas = FindObjectOfType<Canvas>();

            if (parentCanvas != null)
            {
                canvasRect = parentCanvas.GetComponent<RectTransform>();
                uiCamera = parentCanvas.worldCamera;
            }

            scoreManager = FindObjectOfType<ScoreManager>();
            if (scoreManager != null)
            {
                scoreManager.OnScorePopup += ShowPopup;
            }

            InitializePool();
        }

        private void OnDestroy()
        {
            if (scoreManager != null)
            {
                scoreManager.OnScorePopup -= ShowPopup;
            }
        }

        private void InitializePool()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                PopupItem item = CreatePopupItem();
                item.go.SetActive(false);
                pool.Add(item);
            }
        }

        private PopupItem CreatePopupItem()
        {
            GameObject go = new GameObject("ScorePopup");
            go.transform.SetParent(transform, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 60f);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return new PopupItem
            {
                go = go,
                rt = rt,
                text = text,
                outline = outline,
                inUse = false
            };
        }

        private PopupItem GetFromPool()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].inUse)
                    return pool[i];
            }
            // 풀 부족 시 확장
            PopupItem item = CreatePopupItem();
            pool.Add(item);
            return item;
        }

        /// <summary>
        /// 점수 팝업 표시
        /// </summary>
        public void ShowPopup(int score, Vector3 worldPosition)
        {
            if (score <= 0) return;

            PopupItem item = GetFromPool();
            item.inUse = true;
            item.go.SetActive(true);

            // 월드 좌표를 캔버스 로컬 좌표로 변환
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out localPos);
            item.rt.anchoredPosition = localPos;

            // 티어 결정 (페스티벌 모드면 부스트 적용)
            PopupTier tier = GetTier(score);
            if (festivalMode)
            {
                // 폰트 크기 1.6배, 지속시간/이동거리 1.4배, 황금빛 색상으로 변경
                tier.fontSize *= 1.6f;
                tier.duration *= 1.4f;
                tier.travel *= 1.4f;
                tier.color = new Color(1.0f, 0.92f, 0.35f, 1f); // 황금색 (피날레 느낌)
            }

            item.text.text = "+" + FormatNumber(score);
            item.text.fontSize = (int)tier.fontSize;
            item.text.color = tier.color;
            item.text.fontStyle = FontStyle.Bold;

            // 페스티벌 모드: 팝업 주변에 불꽃 스파크 분출
            if (festivalMode)
            {
                StartCoroutine(SpawnFireworkSparks(localPos, tier.color));
                // 강조 강화: 외곽선 두께 증가
                if (item.outline != null)
                {
                    item.outline.effectColor = new Color(0.6f, 0.3f, 0.0f, 0.95f); // 갈색 (황금에 어울림)
                    item.outline.effectDistance = new Vector2(2.5f, -2.5f);
                }
            }
            else
            {
                // 기본 외곽선 복원 (풀 재사용 시 오염 방지)
                if (item.outline != null)
                {
                    item.outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
                    item.outline.effectDistance = new Vector2(1.5f, -1.5f);
                }
            }

            StartCoroutine(AnimatePopup(item, tier));
        }

        /// <summary>
        /// 페스티벌 모드에서 점수 팝업 주변에 불꽃놀이 같은 스파크를 분출.
        /// 12개 스파크가 8방향으로 퍼지며 fade-out.
        /// </summary>
        private IEnumerator SpawnFireworkSparks(Vector2 center, Color baseColor)
        {
            const int sparkCount = 12;
            const float radius = 90f;
            const float duration = 0.6f;

            // 스파크 GameObject들을 한 번에 생성
            var sparks = new List<(GameObject go, RectTransform rt, Image img, Vector2 dir)>();
            for (int i = 0; i < sparkCount; i++)
            {
                GameObject spark = new GameObject($"FireworkSpark_{i}");
                spark.transform.SetParent(transform, false);

                RectTransform rt = spark.AddComponent<RectTransform>();
                rt.anchoredPosition = center;
                rt.sizeDelta = new Vector2(10f, 10f);

                Image img = spark.AddComponent<Image>();
                // 다채로운 색상 무지개 — 황금/주황/빨강/하늘색 랜덤
                Color[] palette = {
                    new Color(1.0f, 0.92f, 0.35f, 1f),  // 황금
                    new Color(1.0f, 0.55f, 0.20f, 1f),  // 주황
                    new Color(1.0f, 0.30f, 0.30f, 1f),  // 빨강
                    new Color(0.45f, 0.85f, 1.00f, 1f), // 하늘
                    new Color(1.0f, 0.65f, 0.85f, 1f),  // 분홍
                };
                img.color = palette[Random.Range(0, palette.Length)];
                img.raycastTarget = false;

                // 8방향 + 약간의 랜덤 변동
                float angle = (360f / sparkCount) * i + Random.Range(-12f, 12f);
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                sparks.Add((spark, rt, img, dir));
            }

            // 애니메이션: 중심에서 바깥으로 퍼지며 페이드아웃 + 살짝 중력
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f); // EaseOutQuad
                float gravity = -t * t * 50f;             // 후반부 중력 효과

                foreach (var (go, rt, img, dir) in sparks)
                {
                    if (rt == null) continue;
                    rt.anchoredPosition = center + dir * (radius * eased) + new Vector2(0, gravity);
                    // 후반 50%부터 페이드
                    float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
                    var c = img.color;
                    c.a = alpha;
                    img.color = c;
                    // 크기 살짝 줄어듦
                    rt.sizeDelta = Vector2.Lerp(new Vector2(10f, 10f), new Vector2(4f, 4f), t);
                }
                yield return null;
            }

            // 정리
            foreach (var (go, _, _, _) in sparks)
            {
                if (go != null) Destroy(go);
            }
        }

        private struct PopupTier
        {
            public float fontSize;
            public Color color;
            public float duration;
            public float travel;
        }

        private PopupTier GetTier(int score)
        {
            // ★ 사용자 요구사항: 모든 점수 팝업을 흰색 폰트로 통일
            //   크기/지속시간/이동거리는 점수 규모에 따라 차등 유지 (시각적 임팩트)
            if (score >= 3000)
            {
                return new PopupTier
                {
                    fontSize = VisualConstants.PopupEpicSize,
                    color = Color.white,
                    duration = 1.2f,
                    travel = 140f
                };
            }
            if (score >= 1000)
            {
                return new PopupTier
                {
                    fontSize = VisualConstants.PopupLargeSize,
                    color = Color.white,
                    duration = 1.0f,
                    travel = 120f
                };
            }
            if (score >= 300)
            {
                return new PopupTier
                {
                    fontSize = VisualConstants.PopupMediumSize,
                    color = Color.white,
                    duration = 0.8f,
                    travel = 100f
                };
            }
            return new PopupTier
            {
                fontSize = VisualConstants.PopupSmallSize,
                color = Color.white,
                duration = 0.6f,
                travel = 80f
            };
        }

        private IEnumerator AnimatePopup(PopupItem item, PopupTier tier)
        {
            float elapsed = 0f;
            float duration = tier.duration;
            Vector2 startPos = item.rt.anchoredPosition;

            // 약간의 랜덤 오프셋
            float xJitter = Random.Range(-15f, 15f);

            // 등장: scale 0 → 1.2 → 1.0
            float scaleInDuration = 0.15f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Y 이동 (EaseOutQuart)
                float moveT = VisualConstants.EaseOutQuart(t);
                float yOffset = moveT * tier.travel;
                item.rt.anchoredPosition = startPos + new Vector2(xJitter * (1f - t), yOffset);

                // 스케일
                if (elapsed < scaleInDuration)
                {
                    float st = elapsed / scaleInDuration;
                    float scale = VisualConstants.EaseOutBack(st) * 1.2f;
                    if (scale > 1.2f) scale = 1.2f;
                    item.rt.localScale = Vector3.one * Mathf.Lerp(0f, 1f, scale / 1.2f);
                    // 처음에 0→1.2→1.0 효과
                    if (st > 0.6f)
                    {
                        float settle = (st - 0.6f) / 0.4f;
                        item.rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 1f, settle);
                    }
                    else
                    {
                        item.rt.localScale = Vector3.one * Mathf.Lerp(0f, 1.2f, st / 0.6f);
                    }
                }
                else
                {
                    item.rt.localScale = Vector3.one;
                }

                // 페이드: 60% 동안 유지, 나머지 40%에서 페이드아웃
                float alpha;
                if (t < 0.6f)
                {
                    alpha = 1f;
                }
                else
                {
                    float fadeT = (t - 0.6f) / 0.4f;
                    alpha = 1f - VisualConstants.EaseInQuad(fadeT);
                }

                Color c = item.text.color;
                c.a = alpha;
                item.text.color = c;

                Color oc = item.outline.effectColor;
                oc.a = 0.6f * alpha;
                item.outline.effectColor = oc;

                yield return null;
            }

            // 반환
            item.go.SetActive(false);
            item.rt.localScale = Vector3.one;
            item.inUse = false;
        }

        private string FormatNumber(int number)
        {
            return string.Format("{0:N0}", number);
        }
    }
}

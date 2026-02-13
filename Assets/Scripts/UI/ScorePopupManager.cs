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

            // 티어 결정
            PopupTier tier = GetTier(score);

            item.text.text = "+" + FormatNumber(score);
            item.text.fontSize = (int)tier.fontSize;
            item.text.color = tier.color;
            item.text.fontStyle = tier.fontSize >= VisualConstants.PopupLargeSize ? FontStyle.Bold : FontStyle.Normal;

            StartCoroutine(AnimatePopup(item, tier));
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
            if (score >= 3000)
            {
                return new PopupTier
                {
                    fontSize = VisualConstants.PopupEpicSize,
                    color = new Color(1f, 0.27f, 0f), // #FF4500
                    duration = 1.2f,
                    travel = 140f
                };
            }
            if (score >= 1000)
            {
                return new PopupTier
                {
                    fontSize = VisualConstants.PopupLargeSize,
                    color = new Color(1f, 0.55f, 0f), // #FF8C00
                    duration = 1.0f,
                    travel = 120f
                };
            }
            if (score >= 300)
            {
                return new PopupTier
                {
                    fontSize = VisualConstants.PopupMediumSize,
                    color = new Color(1f, 0.84f, 0f), // #FFD700
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

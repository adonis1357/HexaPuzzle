using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.UI
{
    /// <summary>
    /// 적군 대미지 팝업 관리
    /// - 0.1초 이내 같은 위치(근접) 대미지는 누적 표시 (-1 → -2 → -3)
    /// - 표시 후 위로 올라가며 1초 내 페이드아웃
    /// </summary>
    public class DamagePopupManager : MonoBehaviour
    {
        public static DamagePopupManager Instance { get; private set; }

        private const int POOL_SIZE = 8;
        private const float ACCUMULATE_WINDOW = 0.1f;
        private const float MERGE_DISTANCE = 80f;
        private const float POPUP_DURATION = 1.0f;
        private const float TRAVEL_DISTANCE = 60f;
        private const int FONT_SIZE = 26;

        private Canvas parentCanvas;
        private RectTransform canvasRect;
        private Camera uiCamera;

        private List<DamagePopupItem> pool = new List<DamagePopupItem>();
        private List<DamagePopupItem> activePopups = new List<DamagePopupItem>();

        private class DamagePopupItem
        {
            public GameObject go;
            public RectTransform rt;
            public Text text;
            public Outline outline;
            public bool inUse;
            public int damageCount;
            public float spawnTime;
            public Vector2 basePosition;
            public Coroutine animCoroutine;
        }

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
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

            InitializePool();
        }

        private void InitializePool()
        {
            for (int i = 0; i < POOL_SIZE; i++)
            {
                DamagePopupItem item = CreatePopupItem();
                item.go.SetActive(false);
                pool.Add(item);
            }
        }

        private DamagePopupItem CreatePopupItem()
        {
            GameObject go = new GameObject("DamagePopup");
            go.transform.SetParent(transform, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 40f);

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.fontSize = FONT_SIZE;
            text.fontStyle = FontStyle.Bold;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return new DamagePopupItem
            {
                go = go,
                rt = rt,
                text = text,
                outline = outline,
                inUse = false,
                damageCount = 0,
                spawnTime = 0f
            };
        }

        private DamagePopupItem GetFromPool()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].inUse)
                    return pool[i];
            }
            DamagePopupItem item = CreatePopupItem();
            pool.Add(item);
            return item;
        }

        /// <summary>
        /// 적군 대미지 표시 (월드 좌표 기준)
        /// 0.1초 이내 근접 위치에 기존 팝업이 있으면 누적
        /// </summary>
        public void ShowDamage(int damage, Vector3 worldPosition)
        {
            if (damage <= 0) return;

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, worldPosition);
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out localPos);

            // 0.1초 이내 근접 팝업 검색 → 누적
            float now = Time.time;
            for (int i = activePopups.Count - 1; i >= 0; i--)
            {
                DamagePopupItem existing = activePopups[i];
                if (!existing.inUse) continue;

                float timeDiff = now - existing.spawnTime;
                float dist = Vector2.Distance(localPos, existing.basePosition);

                if (timeDiff <= ACCUMULATE_WINDOW && dist <= MERGE_DISTANCE)
                {
                    // 누적: 대미지 증가, 텍스트 갱신, 타이머 리셋
                    existing.damageCount += damage;
                    existing.text.text = "-" + existing.damageCount;
                    existing.spawnTime = now;

                    // 누적 시 스케일 펀치
                    if (existing.animCoroutine != null)
                        StopCoroutine(existing.animCoroutine);
                    existing.animCoroutine = StartCoroutine(AnimateDamagePopup(existing));
                    return;
                }
            }

            // 새 팝업 생성
            DamagePopupItem item = GetFromPool();
            item.inUse = true;
            item.damageCount = damage;
            item.spawnTime = now;
            item.basePosition = localPos;
            item.go.SetActive(true);
            item.rt.anchoredPosition = localPos;
            item.rt.localScale = Vector3.one;
            item.text.text = "-" + damage;
            item.text.color = new Color(1f, 0.3f, 0.3f, 1f);
            item.outline.effectColor = new Color(0f, 0f, 0f, 0.7f);

            activePopups.Add(item);
            item.animCoroutine = StartCoroutine(AnimateDamagePopup(item));
        }

        private IEnumerator AnimateDamagePopup(DamagePopupItem item)
        {
            float elapsed = 0f;
            Vector2 startPos = item.basePosition;

            // 등장 스케일 펀치 (0 → 1.3 → 1.0)
            float punchDuration = 0.12f;
            float punchElapsed = 0f;
            while (punchElapsed < punchDuration)
            {
                if (item == null || !item.inUse) yield break;
                punchElapsed += Time.deltaTime;
                float pt = Mathf.Clamp01(punchElapsed / punchDuration);

                float scale;
                if (pt < 0.5f)
                    scale = Mathf.Lerp(0.5f, 1.3f, pt / 0.5f);
                else
                    scale = Mathf.Lerp(1.3f, 1.0f, (pt - 0.5f) / 0.5f);

                item.rt.localScale = Vector3.one * scale;
                yield return null;
            }

            item.rt.localScale = Vector3.one;

            // 위로 올라가며 페이드아웃 (1초)
            while (elapsed < POPUP_DURATION)
            {
                if (item == null || !item.inUse) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / POPUP_DURATION);

                // Y 이동: EaseOutQuart로 위로 올라감
                float moveT = VisualConstants.EaseOutQuart(t);
                float yOffset = moveT * TRAVEL_DISTANCE;
                item.rt.anchoredPosition = startPos + new Vector2(0f, yOffset);

                // 페이드: 처음 40% 유지, 나머지 60%에서 페이드아웃
                float alpha;
                if (t < 0.4f)
                {
                    alpha = 1f;
                }
                else
                {
                    float fadeT = (t - 0.4f) / 0.6f;
                    alpha = 1f - VisualConstants.EaseInQuad(fadeT);
                }

                Color c = item.text.color;
                c.a = alpha;
                item.text.color = c;

                Color oc = item.outline.effectColor;
                oc.a = 0.7f * alpha;
                item.outline.effectColor = oc;

                yield return null;
            }

            // 반환
            item.go.SetActive(false);
            item.rt.localScale = Vector3.one;
            item.inUse = false;
            item.animCoroutine = null;
            activePopups.Remove(item);
        }
    }
}

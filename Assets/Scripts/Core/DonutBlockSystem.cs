using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 도넛 특수 블록 시스템 (Rainbow)
    /// 7개 이상 매칭에서 도넛(링) 패턴 감지 시 생성
    /// 발동 시 같은 색 블록을 보드 전체에서 제거
    /// </summary>
    public class DonutBlockSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("Donut Settings")]
        [SerializeField] private float waveDelay = 0.04f;
        [SerializeField] private float ringRotationSpeed = 360f;

        [Header("Effect Settings")]
        [SerializeField] private int sparkCount = 24;

        public event System.Action<int> OnDonutComplete;

        private int activeDonutCount = 0;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        public bool IsActivating => activeDonutCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public void ForceReset()
        {
            activeDonutCount = 0;
            pendingSpecialBlocks.Clear();
            StopAllCoroutines();
            Debug.LogWarning("[DonutBlockSystem] ForceReset called");
        }


        // 이펙트 부모
        private Transform effectParent;

        // 도넛 아이콘 스프라이트 (static 캐시)
        private static Sprite donutIconSprite;

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[DonutBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[DonutBlockSystem] HexGrid not found!");
            }
            if (removalSystem == null)
                removalSystem = FindObjectOfType<BlockRemovalSystem>();

            SetupEffectParent();
        }

        private void SetupEffectParent()
        {
            if (hexGrid == null) return;

            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("DonutEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);

                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                effectLayer.transform.SetAsLastSibling();
                effectParent = effectLayer.transform;
            }
            else
            {
                effectParent = hexGrid.transform;
            }
        }

        // ============================================================
        // 도넛 아이콘 스프라이트 생성
        // ============================================================

        public static Sprite GetDonutIconSprite()
        {
            if (donutIconSprite == null)
                donutIconSprite = CreateDonutSprite(64);
            return donutIconSprite;
        }

        /// <summary>
        /// 도넛 아이콘 프로시저럴 생성 - 링 모양 + 무지개 그라디언트
        /// </summary>
        private static Sprite CreateDonutSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float outerRadius = size * 0.42f;
            float innerRadius = size * 0.22f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float dist = Vector2.Distance(p, center);

                    // 도넛 링 영역
                    if (dist >= innerRadius && dist <= outerRadius)
                    {
                        // 무지개 그라디언트 (각도 기반)
                        float angle = Mathf.Atan2(p.y - center.y, p.x - center.x);
                        float hue = (angle / (2f * Mathf.PI) + 1f) % 1f;
                        Color c = Color.HSVToRGB(hue, 0.8f, 0.95f);

                        // 3D 느낌 - 중심에서 바깥으로 하이라이트
                        float ringCenter = (outerRadius + innerRadius) * 0.5f;
                        float ringT = 1f - Mathf.Abs(dist - ringCenter) / ((outerRadius - innerRadius) * 0.5f);
                        float highlight = Mathf.Pow(ringT, 2f) * 0.3f;
                        c = new Color(
                            Mathf.Min(1f, c.r + highlight),
                            Mathf.Min(1f, c.g + highlight),
                            Mathf.Min(1f, c.b + highlight),
                            1f
                        );

                        // 엣지 안티앨리어싱
                        float outerAA = Mathf.Clamp01((outerRadius - dist) * 2f);
                        float innerAA = Mathf.Clamp01((dist - innerRadius) * 2f);
                        c.a = outerAA * innerAA;

                        pixels[y * size + x] = c;
                    }

                    // 중앙 빛나는 점
                    float centerDist = Vector2.Distance(p, center);
                    if (centerDist < size * 0.08f)
                    {
                        float sa = Mathf.Clamp01((size * 0.08f - centerDist) / (size * 0.08f));
                        Color sparkColor = new Color(1f, 1f, 0.9f, sa * 0.6f);
                        pixels[y * size + x] = Color.Lerp(pixels[y * size + x], sparkColor, sa);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 도넛 블록 생성
        // ============================================================

        public void CreateDonutBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData donutData = new BlockData(gemType);
            donutData.specialType = SpecialBlockType.Rainbow;
            block.SetBlockData(donutData);
            Debug.Log($"[DonutBlockSystem] Created donut(rainbow) at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // 도넛 발동
        // ============================================================

        public void ActivateDonut(HexBlock donutBlock)
        {
            if (donutBlock == null) return;
            if (donutBlock.Data == null || donutBlock.Data.specialType != SpecialBlockType.Rainbow)
                return;
            Debug.Log($"[DonutBlockSystem] Activating donut at {donutBlock.Coord}");
            StartCoroutine(DonutCoroutine(donutBlock));
        }

        private IEnumerator DonutCoroutine(HexBlock donutBlock)
        {
            activeDonutCount++;

            HexCoord donutCoord = donutBlock.Coord;
            Vector3 donutWorldPos = donutBlock.transform.position;
            GemType targetGemType = donutBlock.Data.gemType;
            Color donutColor = GemColors.GetColor(targetGemType);

            Debug.Log($"[DonutBlockSystem] === DONUT ACTIVATED === Coord={donutCoord}, TargetColor={targetGemType}");

            // 도넛 블록 자체 클리어
            donutBlock.ClearData();

            // 같은 색 블록 전부 수집
            List<HexBlock> targets = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block == donutBlock) continue;

                    if (block.Data.gemType == targetGemType)
                        targets.Add(block);
                }
            }

            Debug.Log($"[DonutBlockSystem] Targets: {targets.Count} same-color blocks ({targetGemType})");

            // 도넛 중심 이펙트
            StartCoroutine(DonutCenterEffect(donutWorldPos, donutColor));

            // 무지개 링 확산 이펙트
            StartCoroutine(RainbowRingExpand(donutWorldPos));

            yield return new WaitForSeconds(0.15f);

            // 거리순 정렬 (중심에서 가까운 것부터)
            targets.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, donutWorldPos);
                float distB = Vector3.Distance(b.transform.position, donutWorldPos);
                return distA.CompareTo(distB);
            });

            // 파도처럼 순차 파괴
            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;

                // 특수 블록이면 pending에 추가
                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    Debug.Log($"[DonutBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
                    if (!pendingSpecialBlocks.Contains(target))
                    {
                        pendingSpecialBlocks.Add(target);
                        target.SetPendingActivation();
                        target.StartWarningBlink(10f);
                    }
                }
                else
                {
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    StartCoroutine(DestroyBlockWithRainbow(target, blockColor, donutWorldPos));
                }

                yield return new WaitForSeconds(waveDelay);
            }

            // 완료 대기
            yield return new WaitForSeconds(0.2f);

            int totalScore = 500 + targets.Count * 100;
            Debug.Log($"[DonutBlockSystem] === DONUT COMPLETE === Score={totalScore}, Destroyed={targets.Count}");
            OnDonutComplete?.Invoke(totalScore);
            activeDonutCount--;
        }

        // ============================================================
        // 이펙트
        // ============================================================

        /// <summary>
        /// 중앙 도넛 이펙트 - 회전하는 링 + 무지개 플래시
        /// </summary>
        private IEnumerator DonutCenterEffect(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 1) 무지개 플래시
            GameObject flash = new GameObject("DonutFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(1f, 1f, 0.8f, 1f);

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(20f, 20f);

            // 2) 회전 링 (3중)
            GameObject[] rings = new GameObject[3];
            for (int r = 0; r < 3; r++)
            {
                rings[r] = CreateDonutRing(pos, parent, r);
            }

            // 3) 스파크 버스트
            for (int i = 0; i < sparkCount; i++)
            {
                float hue = (float)i / sparkCount;
                Color sparkColor = Color.HSVToRGB(hue, 0.9f, 1f);
                StartCoroutine(RainbowSpark(pos, sparkColor));
            }

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 플래시 확장 + 페이드 + 색상 회전
                float flashScale = 1f + t * 10f;
                flashRt.sizeDelta = new Vector2(20f * flashScale, 20f * flashScale);
                float hue = (t * 2f) % 1f;
                Color flashColor = Color.HSVToRGB(hue, 0.5f, 1f);
                flashColor.a = (1f - t) * 0.8f;
                flashImg.color = flashColor;

                // 링 회전 + 확장
                for (int r = 0; r < 3; r++)
                {
                    if (rings[r] == null) continue;
                    float ringT = Mathf.Clamp01(t - r * 0.1f);
                    float ringScale = 1f + ringT * (8f + r * 3f);
                    var rt = rings[r].GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(15f * ringScale, 15f * ringScale);
                    rings[r].transform.localRotation = Quaternion.Euler(0, 0, elapsed * ringRotationSpeed * (r % 2 == 0 ? 1 : -1));
                    var img = rings[r].GetComponent<UnityEngine.UI.Image>();
                    if (img != null)
                    {
                        Color rc = img.color;
                        rc.a = (1f - ringT) * 0.6f;
                        img.color = rc;
                    }
                }

                yield return null;
            }

            Destroy(flash);
            for (int r = 0; r < 3; r++)
                if (rings[r] != null) Destroy(rings[r]);
        }

        private GameObject CreateDonutRing(Vector3 pos, Transform parent, int index)
        {
            GameObject ring = new GameObject($"DonutRing_{index}");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;

            var img = ring.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            float hue = (float)index / 3f;
            Color c = Color.HSVToRGB(hue, 0.8f, 1f);
            c.a = 0.7f;
            img.color = c;

            RectTransform rt = ring.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(15f, 15f);

            return ring;
        }

        /// <summary>
        /// 무지개 링 확산 - 보드 전체로 퍼지는 컬러 웨이브
        /// </summary>
        private IEnumerator RainbowRingExpand(Vector3 center)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 외곽 링
            GameObject ring = new GameObject("RainbowExpand");
            ring.transform.SetParent(parent, false);
            ring.transform.position = center;

            var img = ring.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.8f, 0.2f, 0.6f);

            RectTransform rt = ring.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(10f, 10f);

            float duration = 0.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale = 1f + t * 30f;
                rt.sizeDelta = new Vector2(10f * scale, 10f * scale);

                float hue = (t * 3f) % 1f;
                Color c = Color.HSVToRGB(hue, 0.7f, 1f);
                c.a = (1f - t) * 0.4f;
                img.color = c;

                ring.transform.localRotation = Quaternion.Euler(0, 0, t * 180f);

                yield return null;
            }

            Destroy(ring);
        }

        /// <summary>
        /// 개별 블록 무지개 파괴 이펙트
        /// </summary>
        private IEnumerator DestroyBlockWithRainbow(HexBlock block, Color blockColor, Vector3 center)
        {
            if (block == null) yield break;

            Vector3 blockPos = block.transform.position;

            // 무지개 스파크
            for (int i = 0; i < 4; i++)
            {
                float hue = Random.Range(0f, 1f);
                Color sparkColor = Color.HSVToRGB(hue, 0.9f, 1f);
                StartCoroutine(RainbowSpark(blockPos, sparkColor));
            }

            // 블록 축소 + 색상 변화
            float duration = 0.15f;
            float elapsed = 0f;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 색상이 무지개처럼 변하면서 사라짐
                float shrink = 1f - t;
                block.transform.localScale = origScale * shrink;

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            block.ClearData();
        }

        /// <summary>
        /// 무지개색 스파크
        /// </summary>
        private IEnumerator RainbowSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject spark = new GameObject("DonutSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = color;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(4f, 10f);
            rt.sizeDelta = new Vector2(size, size);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(180f, 400f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(0.2f, 0.4f);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= 0.93f;

                // 색상 회전
                float h, s, v;
                Color.RGBToHSV(color, out h, out s, out v);
                h = (h + Time.deltaTime * 2f) % 1f;
                Color newColor = Color.HSVToRGB(h, s, v);
                newColor.a = 1f - t;
                img.color = newColor;
                color = newColor;

                float sc = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(sc, sc);

                yield return null;
            }

            Destroy(spark);
        }
    }
}
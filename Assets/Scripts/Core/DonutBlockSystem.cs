using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

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
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        public bool IsActivating => activeDonutCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);
        public void ForceReset()
        {
            activeDonutCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.LogWarning("[DonutBlockSystem] ForceReset called");
        }

        /// <summary>
        /// 코루틴 중단으로 남은 이펙트 오브젝트 일괄 정리
        /// </summary>
        private void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);
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
                        Color c = Color.HSVToRGB(hue, 0.30f, 0.95f); // 파스텔 무지개

                        // 3D 느낌 - 중심에서 바깥으로 하이라이트 (감쇠)
                        float ringCenter = (outerRadius + innerRadius) * 0.5f;
                        float ringT = 1f - Mathf.Abs(dist - ringCenter) / ((outerRadius - innerRadius) * 0.5f);
                        float highlight = Mathf.Pow(ringT, 2f) * 0.20f;
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
            activeBlocks.Add(donutBlock);

            HexCoord donutCoord = donutBlock.Coord;
            Vector3 donutWorldPos = donutBlock.transform.position;
            GemType targetGemType = donutBlock.Data.gemType;
            Color donutColor = GemColors.GetColor(targetGemType);

            Debug.Log($"[DonutBlockSystem] === DONUT ACTIVATED === Coord={donutCoord}, TargetColor={targetGemType}");

            // Pre-Fire 압축 애니메이션
            yield return StartCoroutine(PreFireCompression(donutBlock));

            // Hit Stop (Medium tier)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationMedium));

            // Zoom Punch (Small tier)
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // 발사 순간 블록 클리어
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

            // Screen shake (Medium tier - newly added for Donut)
            StartCoroutine(ScreenShake(VisualConstants.ShakeMediumIntensity, VisualConstants.ShakeMediumDuration));

            yield return new WaitForSeconds(0.15f);

            // 거리순 정렬 (중심에서 가까운 것부터)
            targets.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, donutWorldPos);
                float distB = Vector3.Distance(b.transform.position, donutWorldPos);
                return distA.CompareTo(distB);
            });

            // 파도처럼 순차 파괴 - 모든 파괴 코루틴을 추적
            List<Coroutine> destroyCoroutines = new List<Coroutine>();
            int blockScoreSum = 0;

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;
                if (target.Data == null || target.Data.gemType == GemType.None) continue;

                // FixedBlock은 파괴 불가 - 건너뛰기
                if (target.Data.specialType == SpecialBlockType.FixedBlock)
                    continue;

                // 특수 블록이면 pending에 추가
                if (target.Data.specialType != SpecialBlockType.None)
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
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithRainbow(target, blockColor, donutWorldPos)));
                }

                yield return new WaitForSeconds(waveDelay);
            }

            // 모든 파괴 애니메이션이 완료될 때까지 대기 (ClearData 보장)
            foreach (var co in destroyCoroutines)
                yield return co;

            int totalScore = 500 + blockScoreSum;
            Debug.Log($"[DonutBlockSystem] === DONUT COMPLETE === Score={totalScore} (base:500 + blockTierSum:{blockScoreSum}), Destroyed={targets.Count}");
            OnDonutComplete?.Invoke(totalScore);
            activeBlocks.Remove(donutBlock);
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

            // 블룸 레이어 (무지개 플래시 뒤에)
            StartCoroutine(BloomLayer(pos, new Color(1f, 1f, 0.8f), VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // 1) 무지개 플래시
            GameObject flash = new GameObject("DonutFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(1f, 1f, 0.8f, 1f);

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            flashRt.sizeDelta = new Vector2(initSize, initSize);

            // 2) 회전 링 (4중 - increased from 3)
            int ringCount = 4;
            GameObject[] rings = new GameObject[ringCount];
            for (int r = 0; r < ringCount; r++)
            {
                rings[r] = CreateDonutRing(pos, parent, r);
            }

            // 3) 스파크 버스트 (large tier with cascade)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int totalSparks = Mathf.RoundToInt(sparkCount * cascadeMult);
            for (int i = 0; i < totalSparks; i++)
            {
                float hue = (float)i / totalSparks;
                Color sparkColor = Color.HSVToRGB(hue, 0.9f, 1f);
                StartCoroutine(RainbowSpark(pos, sparkColor));
            }

            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                // 플래시 확장 + 페이드 + HSV 색상 회전 (donut identity)
                float flashScale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                flashRt.sizeDelta = new Vector2(initSize * flashScale, initSize * flashScale);
                float hue = (t * 2f) % 1f;
                Color flashColor = Color.HSVToRGB(hue, 0.5f, 1f);
                flashColor.a = (1f - t) * 0.8f;
                flashImg.color = flashColor;

                // 링 회전 + 확장
                for (int r = 0; r < ringCount; r++)
                {
                    if (rings[r] == null) continue;
                    float ringT = Mathf.Clamp01(t - r * 0.08f);
                    float ringEased = VisualConstants.EaseOutCubic(ringT);
                    float ringScale = 1f + ringEased * (8f + r * 2.5f);
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
            for (int r = 0; r < ringCount; r++)
                if (rings[r] != null) Destroy(rings[r]);
        }

        private GameObject CreateDonutRing(Vector3 pos, Transform parent, int index)
        {
            GameObject ring = new GameObject($"DonutRing_{index}");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;

            var img = ring.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
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

            float initSize = VisualConstants.WaveLargeInitialSize;

            // 외곽 링
            GameObject ring = new GameObject("RainbowExpand");
            ring.transform.SetParent(parent, false);
            ring.transform.position = center;

            var img = ring.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.8f, 0.2f, VisualConstants.WaveLargeAlpha);

            RectTransform rt = ring.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(initSize, initSize);

            float duration = 0.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                float scale = 1f + eased * 30f;
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);

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

            // 화이트 플래시 오버레이
            StartCoroutine(DestroyFlashOverlay(block));

            // Rainbow connection line from center to block
            StartCoroutine(RainbowConnectionLine(center, blockPos));

            // 무지개 스파크 (small tier with cascade)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkSmallCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
            {
                float hue = Random.Range(0f, 1f);
                Color sparkColor = Color.HSVToRGB(hue, 0.9f, 1f);
                StartCoroutine(RainbowSpark(blockPos, sparkColor));
            }

            // 이중 이징 + 미세 회전 (30도, 레인보우 정체성)
            float duration = VisualConstants.DestroyDuration;
            float elapsed = 0f;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (t < expandRatio)
                {
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = origScale * scale;
                }
                else
                {
                    float shrinkT = (t - expandRatio) / (1f - expandRatio);
                    float sx = 1f + VisualConstants.DestroySqueezePeak * Mathf.Sin(shrinkT * Mathf.PI);
                    float sy = Mathf.Max(0f, 1f - VisualConstants.EaseInQuad(shrinkT));
                    block.transform.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);
                }

                // Micro rotation (30 degrees - rainbow identity)
                float rot = t * 30f;
                block.transform.localRotation = Quaternion.Euler(0, 0, rot);

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            block.transform.localRotation = Quaternion.identity;
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
            float size = Random.Range(VisualConstants.SparkMediumSizeMin, VisualConstants.SparkMediumSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkMediumSpeedMin, VisualConstants.SparkMediumSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(VisualConstants.SparkMediumLifetimeMin, VisualConstants.SparkMediumLifetimeMax);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= VisualConstants.SparkDeceleration;

                // HSV 색상 회전 (donut identity preserved)
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

        /// <summary>
        /// Rainbow connection line from center to target block (donut identity)
        /// </summary>
        private IEnumerator RainbowConnectionLine(Vector3 from, Vector3 to)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject line = new GameObject("RainbowLine");
            line.transform.SetParent(parent, false);

            Vector3 mid = (from + to) / 2f;
            line.transform.position = mid;

            var img = line.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            RectTransform rt = line.GetComponent<RectTransform>();
            float dist = Vector3.Distance(from, to);
            Vector3 dir = to - from;
            float lineAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0, 0, lineAngle);
            rt.sizeDelta = new Vector2(dist, 3f);

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float hue = (t * 2f) % 1f;
                Color c = Color.HSVToRGB(hue, 0.8f, 1f);
                c.a = (1f - t) * 0.6f;
                img.color = c;

                float width = Mathf.Lerp(3f, 1f, t);
                rt.sizeDelta = new Vector2(dist, width);

                yield return null;
            }

            Destroy(line);
        }

        // ============================================================
        // 화면 흔들림 (newly added for Donut)
        // ============================================================

        private IEnumerator FadeOutAndDestroy(GameObject obj)
        {
            var img = obj.GetComponent<UnityEngine.UI.Image>();
            if (img == null) { Destroy(obj); yield break; }

            Color c = img.color;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = 1f - t;
                img.color = c;
                yield return null;
            }

            Destroy(obj);
        }

        private int shakeCount = 0;
        private Vector3 shakeOriginalPos;

        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            if (shakeCount == 0)
                shakeOriginalPos = target.localPosition;
            shakeCount++;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float decay = 1f - VisualConstants.EaseInQuad(t);
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                target.localPosition = shakeOriginalPos + new Vector3(x, y, 0);
                yield return null;
            }

            shakeCount--;
            if (shakeCount <= 0)
            {
                shakeCount = 0;
                target.localPosition = shakeOriginalPos;
            }
        }

        // ============================================================
        // Phase 1 VFX: 공통 유틸리티 메서드
        // ============================================================

        private IEnumerator PreFireCompression(HexBlock block)
        {
            if (block == null) yield break;

            float elapsed = 0f;
            float duration = VisualConstants.PreFireDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale;
                if (t < 0.5f)
                {
                    float ct = t / 0.5f;
                    scale = Mathf.Lerp(1f, VisualConstants.PreFireScaleMin, VisualConstants.EaseInQuad(ct));
                }
                else
                {
                    float et = (t - 0.5f) / 0.5f;
                    scale = Mathf.Lerp(VisualConstants.PreFireScaleMin, VisualConstants.PreFireScaleMax, VisualConstants.EaseOutCubic(et));
                }

                block.transform.localScale = Vector3.one * scale;
                yield return null;
            }

            block.transform.localScale = Vector3.one;
        }

        private IEnumerator HitStop(float stopDuration)
        {
            if (!VisualConstants.CanHitStop()) yield break;
            VisualConstants.RecordHitStop();

            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(stopDuration);

            float elapsed = 0f;
            while (elapsed < VisualConstants.HitStopSlowMoDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.HitStopSlowMoDuration);
                Time.timeScale = Mathf.Lerp(VisualConstants.HitStopSlowMoScale, 1f, VisualConstants.EaseOutCubic(t));
                yield return null;
            }
            Time.timeScale = 1f;
        }

        private IEnumerator ZoomPunch(float targetScale)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = target.localScale;
            Vector3 punchScale = origScale * targetScale;

            float elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchInDuration);
                target.localScale = Vector3.Lerp(origScale, punchScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < VisualConstants.ZoomPunchOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchOutDuration);
                target.localScale = Vector3.Lerp(punchScale, origScale, VisualConstants.EaseOutCubic(t));
                yield return null;
            }

            target.localScale = origScale;
        }

        private IEnumerator DestroyFlashOverlay(HexBlock block)
        {
            if (block == null) yield break;

            GameObject flash = new GameObject("DestroyFlash");
            flash.transform.SetParent(block.transform, false);
            flash.transform.localPosition = Vector3.zero;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 60f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

            float elapsed = 0f;
            while (elapsed < VisualConstants.DestroyFlashDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / VisualConstants.DestroyFlashDuration);
                img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha * (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        private IEnumerator BloomLayer(Vector3 pos, Color color, float initSize, float duration)
        {
            yield return new WaitForSeconds(VisualConstants.BloomLag);

            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject bloom = new GameObject("BloomLayer");
            bloom.transform.SetParent(parent, false);
            bloom.transform.position = pos;
            bloom.transform.SetAsFirstSibling();

            var img = bloom.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier);

            RectTransform rt = bloom.GetComponent<RectTransform>();
            float bloomSize = initSize * VisualConstants.BloomSizeMultiplier;
            rt.sizeDelta = new Vector2(bloomSize, bloomSize);

            float remainDuration = duration - VisualConstants.BloomLag;
            float elapsed = 0f;

            while (elapsed < remainDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / remainDuration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                rt.sizeDelta = new Vector2(bloomSize * scale, bloomSize * scale);
                img.color = new Color(color.r, color.g, color.b, VisualConstants.BloomAlphaMultiplier * (1f - t));
                yield return null;
            }

            Destroy(bloom);
        }
    }
}
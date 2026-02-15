using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 폭탄 특수 블록 시스템
    /// 5개 이상 매칭 시 생성, 다음 매칭에 포함되면 인접 6칸 폭발
    /// </summary>
    public class BombBlockSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("Bomb Settings")]
        [SerializeField] private float explosionDelay = 0.08f;

        public event System.Action<int> OnBombComplete;

        private int activeBombCount = 0;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        public bool IsBombing => activeBombCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);
        public void ForceReset()
        {
            activeBombCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.LogWarning("[BombBlockSystem] ForceReset called");
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


        // 이펙트 생성용 Canvas 내 부모 Transform
        private Transform effectParent;

        // 폭탄 아이콘 스프라이트 (static 캐시)
        private static Sprite bombIconSprite;

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[BombBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[BombBlockSystem] HexGrid not found!");
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
                GameObject effectLayer = new GameObject("BombEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);

                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                effectLayer.transform.SetAsLastSibling();

                effectParent = effectLayer.transform;
                Debug.Log("[BombBlockSystem] Effect layer created under Canvas");
            }
            else
            {
                effectParent = hexGrid.transform;
            }
        }

        // ============================================================
        // 폭탄 아이콘 스프라이트 생성
        // ============================================================

        public static Sprite GetBombIconSprite()
        {
            if (bombIconSprite == null)
                bombIconSprite = CreateBombSprite(64);
            return bombIconSprite;
        }

        /// <summary>
        /// 폭탄 아이콘 프로시저럴 생성 - 원형 본체 + 도화선
        /// </summary>
        private static Sprite CreateBombSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.48f, size * 0.4f);
            float bodyRadius = size * 0.3f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float dist = Vector2.Distance(p, center);

                    // 원형 본체 (파스텔 코랄 핑크)
                    if (dist < bodyRadius)
                    {
                        float edge = dist / bodyRadius;
                        float highlight = Mathf.Pow(1f - edge, 3f) * 0.3f;
                        Color c = new Color(0.92f + highlight * 0.05f, 0.68f + highlight * 0.05f, 0.70f + highlight * 0.05f, 1f);
                        // 하이라이트 (좌상단 빛)
                        Vector2 lightDir = (p - center).normalized;
                        float lightDot = Vector2.Dot(lightDir, new Vector2(-0.5f, 0.5f));
                        if (lightDot > 0)
                            c = Color.Lerp(c, new Color(0.98f, 0.85f, 0.86f, 1f), lightDot * 0.4f * (1f - edge));
                        // 안티앨리어싱
                        float aa = Mathf.Clamp01((bodyRadius - dist) * 2f);
                        c.a = aa;
                        pixels[y * size + x] = c;
                    }

                    // 도화선 (크림 브라운)
                    float fuseX = center.x + (y - (center.y + bodyRadius * 0.7f)) * 0.4f;
                    float fuseY = y;
                    if (fuseY > center.y + bodyRadius * 0.6f && fuseY < center.y + bodyRadius * 1.6f)
                    {
                        float fuseDist = Mathf.Abs(x - fuseX);
                        if (fuseDist < 2f)
                        {
                            float fa = Mathf.Clamp01((2f - fuseDist) * 0.8f);
                            Color fuseColor = new Color(0.82f, 0.70f, 0.58f, fa);
                            if (pixels[y * size + x].a < fa)
                                pixels[y * size + x] = fuseColor;
                        }
                    }

                    // 스파크 (파스텔 옐로)
                    float sparkCenterX = center.x + (center.y + bodyRadius * 1.5f - (center.y + bodyRadius * 0.7f)) * 0.4f;
                    float sparkCenterY = center.y + bodyRadius * 1.5f;
                    float sparkDist = Vector2.Distance(p, new Vector2(sparkCenterX, sparkCenterY));
                    if (sparkDist < size * 0.08f)
                    {
                        float sa = Mathf.Clamp01((size * 0.08f - sparkDist) / (size * 0.08f));
                        Color sparkColor = new Color(1f, 0.92f, 0.70f, sa * 0.9f);
                        pixels[y * size + x] = Color.Lerp(pixels[y * size + x], sparkColor, sa);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 폭탄 생성
        // ============================================================

        public void CreateBombBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData bombData = new BlockData(gemType);
            bombData.specialType = SpecialBlockType.Bomb;
            block.SetBlockData(bombData);
            Debug.Log($"[BombBlockSystem] Created bomb at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // 폭탄 발동
        // ============================================================

        public void ActivateBomb(HexBlock bombBlock)
        {
            if (bombBlock == null) return;
            if (bombBlock.Data == null || bombBlock.Data.specialType != SpecialBlockType.Bomb)
                return;
            Debug.Log($"[BombBlockSystem] Activating bomb at {bombBlock.Coord}");
            StartCoroutine(BombCoroutine(bombBlock));
        }

private IEnumerator BombCoroutine(HexBlock bombBlock)
        {
            activeBombCount++;
            activeBlocks.Add(bombBlock);

            HexCoord bombCoord = bombBlock.Coord;
            Vector3 bombWorldPos = bombBlock.transform.position;
            Color bombColor = GemColors.GetColor(bombBlock.Data.gemType);

            Debug.Log($"[BombBlockSystem] === BOMB ACTIVATED === Coord={bombCoord}");

            // Pre-Fire 압축 애니메이션 (통일)
            yield return StartCoroutine(PreFireCompression(bombBlock));

            // Hit Stop (Large tier)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));

            // Zoom Punch (Large tier)
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            // 발사 순간 블록 클리어
            bombBlock.ClearData();

            // 반경 2칸 타겟 수집 (1칸/2칸 분리)
            List<HexBlock> ring1Targets = new List<HexBlock>();
            List<HexBlock> ring2Targets = new List<HexBlock>();
            HashSet<HexCoord> visitedCoords = new HashSet<HexCoord>();
            visitedCoords.Add(bombCoord);

            if (hexGrid != null)
            {
                // 1칸 인접
                var ring1 = hexGrid.GetNeighbors(bombCoord);
                foreach (var neighbor in ring1)
                {
                    if (neighbor != null && !visitedCoords.Contains(neighbor.Coord))
                    {
                        visitedCoords.Add(neighbor.Coord);
                        if (neighbor.Data != null && neighbor.Data.gemType != GemType.None)
                            ring1Targets.Add(neighbor);
                    }
                }

                // 2칸 인접
                foreach (var ring1Block in ring1)
                {
                    if (ring1Block == null) continue;
                    var ring2 = hexGrid.GetNeighbors(ring1Block.Coord);
                    foreach (var neighbor in ring2)
                    {
                        if (neighbor != null && !visitedCoords.Contains(neighbor.Coord))
                        {
                            visitedCoords.Add(neighbor.Coord);
                            if (neighbor.Data != null && neighbor.Data.gemType != GemType.None)
                                ring2Targets.Add(neighbor);
                        }
                    }
                }
            }

            Debug.Log($"[BombBlockSystem] Targets: ring1={ring1Targets.Count}, ring2={ring2Targets.Count}");

            // === 1칸 동시 폭발 ===
            StartCoroutine(BombExplosionEffect(bombWorldPos, bombColor));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            List<Coroutine> destroyCoroutines = new List<Coroutine>();
            int blockScoreSum = 0;

            foreach (var target in ring1Targets)
            {
                if (target == null || target.Data == null || target.Data.gemType == GemType.None) continue;

                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
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
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithExplosion(target, blockColor, bombWorldPos)));
                }
            }

            // === 0.1초 후 2칸 동시 폭발 ===
            yield return new WaitForSeconds(0.1f);

            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity * 0.6f, VisualConstants.ShakeLargeDuration * 0.7f));

            foreach (var target in ring2Targets)
            {
                if (target == null || target.Data == null || target.Data.gemType == GemType.None) continue;

                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
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
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithExplosion(target, blockColor, bombWorldPos)));
                }
            }

            // 모든 파괴 애니메이션이 완료될 때까지 대기 (ClearData 보장)
            foreach (var co in destroyCoroutines)
                yield return co;

            int totalScore = 200 + blockScoreSum;
            Debug.Log($"[BombBlockSystem] === BOMB COMPLETE === Score={totalScore} (base:200 + blockTierSum:{blockScoreSum})");
            OnBombComplete?.Invoke(totalScore);
            activeBlocks.Remove(bombBlock);
            activeBombCount--;
        }

        // ============================================================
        // 폭발 이펙트
        // ============================================================

        /// <summary>
        /// 중앙 폭발 이펙트 - 큰 충격파 + 플래시 + 스파크
        /// </summary>
        private IEnumerator BombExplosionEffect(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 레이어 (메인 플래시 뒤에)
            StartCoroutine(BloomLayer(pos, VisualConstants.FlashColorBomb, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // 1) 중앙 밝은 플래시
            GameObject flash = new GameObject("BombFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(VisualConstants.FlashColorBomb.r, VisualConstants.FlashColorBomb.g, VisualConstants.FlashColorBomb.b, 1f);

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            flashRt.sizeDelta = new Vector2(initSize, initSize);

            // 2) 충격파 링 (2중) with stagger
            GameObject ring1 = CreateExplosionRing(pos, parent, new Color(1f, 0.6f, 0.1f, 0.8f));
            GameObject ring2 = CreateExplosionRing(pos, parent, new Color(1f, 0.3f, 0.1f, 0.5f));

            // 3) 스파크 버스트 (large tier)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkLargeCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
                StartCoroutine(ExplosionSpark(pos, color));

            // 4) 파편 (large tier)
            int count = Mathf.RoundToInt((VisualConstants.DebrisLargeCount + Random.Range(0, 4)) * cascadeMult);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateExplosionDebris(pos, color));

            // 애니메이션
            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                // 플래시 확장 + 페이드
                float flashScale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                flashRt.sizeDelta = new Vector2(initSize * flashScale, initSize * flashScale);
                flashImg.color = new Color(VisualConstants.FlashColorBomb.r, VisualConstants.FlashColorBomb.g, VisualConstants.FlashColorBomb.b, (1f - t) * 0.9f);

                // 충격파 링 1 (with easing)
                AnimateRing(ring1, eased, VisualConstants.WaveLargeExpand);
                // 충격파 링 2 (약간 느리게, enhanced stagger)
                float ring2T = Mathf.Clamp01(t * 0.7f);
                AnimateRing(ring2, VisualConstants.EaseOutCubic(ring2T), VisualConstants.WaveLargeExpand * 0.8f);

                yield return null;
            }

            Destroy(flash);
            Destroy(ring1);
            Destroy(ring2);
        }

        private GameObject CreateExplosionRing(Vector3 pos, Transform parent, Color color)
        {
            GameObject ring = new GameObject("ExplosionRing");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;

            var img = ring.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = color;

            RectTransform rt = ring.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(10f, 10f);

            return ring;
        }

        private void AnimateRing(GameObject ring, float t, float expandRate)
        {
            if (ring == null) return;
            var img = ring.GetComponent<UnityEngine.UI.Image>();
            var rt = ring.GetComponent<RectTransform>();
            if (img == null || rt == null) return;

            float scale = 1f + t * expandRate;
            rt.sizeDelta = new Vector2(10f * scale, 10f * scale);
            Color c = img.color;
            c.a = Mathf.Lerp(c.a, 0f, t);
            img.color = c;
        }

        /// <summary>
        /// 개별 블록 폭발 파괴 - 중앙에서 밀려나는 효과
        /// </summary>
        private IEnumerator DestroyBlockWithExplosion(HexBlock block, Color blockColor, Vector3 bombCenter)
        {
            if (block == null) yield break;

            Vector3 blockPos = block.transform.position;
            Vector3 pushDir = (blockPos - bombCenter).normalized;

            // 화이트 플래시 오버레이
            StartCoroutine(DestroyFlashOverlay(block));

            // 임팩트 웨이브
            StartCoroutine(ImpactWave(blockPos, blockColor));

            // 파편 (base tier)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int count = Mathf.RoundToInt((VisualConstants.DebrisBaseCount / 2 + Random.Range(0, 3)) * cascadeMult);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateExplosionDebris(blockPos, blockColor));

            // 이중 이징: 확대 → 밀려나기 + 찌그러짐
            float duration = VisualConstants.DestroyDuration;
            float elapsed = 0f;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;
            Vector3 origScale = block.transform.localScale;
            Vector3 origPos = block.transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 밀려나기
                float pushAmount = 15f * Mathf.Sin(t * Mathf.PI * 0.5f);
                block.transform.position = origPos + pushDir * pushAmount;

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

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            block.transform.position = origPos;
            block.ClearData();
        }

        private IEnumerator ImpactWave(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject wave = new GameObject("BombImpactWave");
            wave.transform.SetParent(parent, false);
            wave.transform.position = pos;

            var image = wave.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha);

            RectTransform rt = wave.GetComponent<RectTransform>();
            float initSize = VisualConstants.WaveSmallInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            float duration = VisualConstants.WaveSmallDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.WaveSmallExpand - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                image.color = new Color(color.r, color.g, color.b, VisualConstants.WaveSmallAlpha * (1f - t));
                yield return null;
            }

            Destroy(wave);
        }

        private IEnumerator ExplosionSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject spark = new GameObject("BombSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            Color bright = VisualConstants.BombBrighten(color);
            bright.a = 1f;
            img.color = bright;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkLargeSizeMin, VisualConstants.SparkLargeSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkLargeSpeedMin, VisualConstants.SparkLargeSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(VisualConstants.SparkLargeLifetimeMin, VisualConstants.SparkLargeLifetimeMax);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= VisualConstants.SparkDeceleration;
                bright.a = 1f - t;
                img.color = bright;
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);
                yield return null;
            }

            Destroy(spark);
        }

        private IEnumerator AnimateExplosionDebris(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject debris = new GameObject("BombDebris");
            debris.transform.SetParent(parent, false);
            debris.transform.position = center;

            var image = debris.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;

            float variation = Random.Range(-0.15f, 0.15f);
            Color debrisColor = new Color(
                Mathf.Clamp01(color.r + variation),
                Mathf.Clamp01(color.g + variation),
                Mathf.Clamp01(color.b + variation),
                1f
            );
            image.color = debrisColor;

            RectTransform rt = debris.GetComponent<RectTransform>();
            float w = Random.Range(VisualConstants.DebrisLargeSizeMin, VisualConstants.DebrisLargeSizeMax);
            float h = Random.Range(VisualConstants.DebrisLargeSizeMin, VisualConstants.DebrisLargeSizeMax * 0.85f);
            rt.sizeDelta = new Vector2(w, h);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.DebrisLargeSpeedMin, VisualConstants.DebrisLargeSpeedMax);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float rotSpeed = Random.Range(VisualConstants.DebrisRotSpeedMin, VisualConstants.DebrisRotSpeedMax);
            float lifetime = Random.Range(VisualConstants.DebrisLargeLifetimeMin, VisualConstants.DebrisLargeLifetimeMax);
            float elapsed = 0f;
            float rot = Random.Range(0f, 360f);

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                velocity.y += VisualConstants.DebrisGravity * Time.deltaTime;
                Vector3 pos = debris.transform.position;
                pos.x += velocity.x * Time.deltaTime;
                pos.y += velocity.y * Time.deltaTime;
                debris.transform.position = pos;

                rot += rotSpeed * Time.deltaTime;
                debris.transform.localRotation = Quaternion.Euler(0, 0, rot);

                debrisColor.a = 1f - t * t;
                image.color = debrisColor;
                float shrink = 1f - t * 0.5f;
                rt.sizeDelta = new Vector2(w * shrink, h * shrink);

                yield return null;
            }

            Destroy(debris);
        }

        // ============================================================
        // 화면 흔들림 (Bug #11 fix: 동시 실행 시 위치 드리프트 방지)
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
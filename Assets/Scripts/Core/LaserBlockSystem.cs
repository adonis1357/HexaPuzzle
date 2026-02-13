using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 레이저 특수 블록 시스템
    /// 6개 매칭 시 생성, 활성화 시 헥사 3축(★ 모양) 전체 직선 삭제
    /// </summary>
    public class LaserBlockSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("Laser Settings")]
        [SerializeField] private float laserSpeed = 0.04f;
        [SerializeField] private float beamDuration = 0.3f;

        [Header("Effect Settings")]
        [SerializeField] private float shakeIntensity = 10f;

        public event System.Action<int> OnLaserComplete;

        private int activeLaserCount = 0;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        public bool IsActivating => activeLaserCount > 0;

        public void ForceReset()
        {
            StopAllCoroutines();
            activeLaserCount = 0;
            pendingSpecialBlocks.Clear();
            CleanupEffects();
            Debug.LogWarning("[LaserBlockSystem] ForceReset called");
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

        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;

        private Transform effectParent;
        private static Sprite laserIconSprite;

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[LaserBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[LaserBlockSystem] HexGrid not found!");
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
                GameObject effectLayer = new GameObject("LaserEffectLayer");
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
        // 레이저 아이콘 스프라이트
        // ============================================================

        public static Sprite GetLaserIconSprite()
        {
            if (laserIconSprite == null)
                laserIconSprite = CreateLaserSprite(64);
            return laserIconSprite;
        }

        /// <summary>
        /// 레이저 아이콘 프로시저럴 생성 - ★ 모양 (3축 직선)
        /// </summary>
        private static Sprite CreateLaserSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float lineLength = size * 0.42f;
            float lineWidth = size * 0.06f;

            // 3축: 수직(90°), 슬래시(30°), 백슬래시(-30°)
            float[] angles = { 90f, 30f, -30f };

            foreach (float angleDeg in angles)
            {
                float angleRad = angleDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                Vector2 perp = new Vector2(-dir.y, dir.x);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 p = new Vector2(x, y) - center;
                        float along = Vector2.Dot(p, dir);
                        float across = Mathf.Abs(Vector2.Dot(p, perp));

                        if (Mathf.Abs(along) <= lineLength && across <= lineWidth)
                        {
                            float edgeFade = 1f - (across / lineWidth);
                            float tipFade = 1f - Mathf.Max(0, (Mathf.Abs(along) - lineLength * 0.7f) / (lineLength * 0.3f));
                            float alpha = edgeFade * tipFade;

                            float glow = Mathf.Pow(edgeFade, 2f);
                            Color c = new Color(
                                0.3f + glow * 0.7f,
                                0.6f + glow * 0.4f,
                                1f,
                                alpha * 0.9f
                            );

                            int idx = y * size + x;
                            if (pixels[idx].a < c.a)
                                pixels[idx] = c;
                        }
                    }
                }
            }

            // 중앙 밝은 원
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < size * 0.1f)
                    {
                        float a = 1f - dist / (size * 0.1f);
                        Color bright = new Color(0.9f, 0.95f, 1f, a);
                        int idx = y * size + x;
                        pixels[idx] = Color.Lerp(pixels[idx], bright, a);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 레이저 생성
        // ============================================================

        public void CreateLaserBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData laserData = new BlockData(gemType);
            laserData.specialType = SpecialBlockType.Laser;
            block.SetBlockData(laserData);
            Debug.Log($"[LaserBlockSystem] Created laser at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // 레이저 발동
        // ============================================================

        public void ActivateLaser(HexBlock laserBlock)
        {
            if (laserBlock == null) return;
            if (laserBlock.Data == null || laserBlock.Data.specialType != SpecialBlockType.Laser)
                return;
            Debug.Log($"[LaserBlockSystem] Activating laser at {laserBlock.Coord}");
            StartCoroutine(LaserCoroutine(laserBlock));
        }

        private IEnumerator LaserCoroutine(HexBlock laserBlock)
        {
            activeLaserCount++;

            HexCoord startCoord = laserBlock.Coord;
            Vector3 laserWorldPos = laserBlock.transform.position;
            Color laserColor = GemColors.GetColor(laserBlock.Data.gemType);

            Debug.Log($"[LaserBlockSystem] === LASER ACTIVATED === Coord={startCoord}");

            // Pre-Fire 압축 애니메이션
            yield return StartCoroutine(PreFireCompression(laserBlock));

            // Hit Stop (Large tier)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));

            // Zoom Punch (Large tier)
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            // 레이저 블록 자체 클리어
            laserBlock.ClearData();

            // 3축 × 양방향 = 6방향 타겟 수집
            DrillDirection[] axes = { DrillDirection.Vertical, DrillDirection.Slash, DrillDirection.BackSlash };
            List<List<HexBlock>> allTargetLines = new List<List<HexBlock>>();

            foreach (var axis in axes)
            {
                List<HexBlock> positive = GetBlocksInDirection(startCoord, axis, true);
                List<HexBlock> negative = GetBlocksInDirection(startCoord, axis, false);
                allTargetLines.Add(positive);
                allTargetLines.Add(negative);
            }

            int totalTargets = 0;
            foreach (var line in allTargetLines)
                totalTargets += line.Count;

            // 파괴 대상 블록의 티어별 기본 점수 미리 계산 (ClearData 전)
            int blockScoreSum = 0;
            foreach (var line in allTargetLines)
            {
                foreach (var t in line)
                {
                    if (t == null || t.Data == null || t.Data.gemType == GemType.None) continue;
                    if (t.Data.specialType != SpecialBlockType.None && t.Data.specialType != SpecialBlockType.FixedBlock) continue;
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(t.Data.tier);
                }
            }

            Debug.Log($"[LaserBlockSystem] Total targets across 3 axes: {totalTargets}");

            // 중앙 폭발 이펙트
            StartCoroutine(LaserCenterFlash(laserWorldPos, laserColor));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            // 3축 레이저 빔 이펙트
            for (int i = 0; i < axes.Length; i++)
            {
                StartCoroutine(LaserBeamEffect(laserWorldPos, axes[i], true, laserColor));
                StartCoroutine(LaserBeamEffect(laserWorldPos, axes[i], false, laserColor));
            }

            yield return new WaitForSeconds(0.05f);

            // 6방향 동시 파괴 (각 방향은 순차, 전체 방향은 병렬)
            List<Coroutine> lineCoroutines = new List<Coroutine>();
            foreach (var line in allTargetLines)
            {
                if (line.Count > 0)
                    lineCoroutines.Add(StartCoroutine(DestroyLine(line, laserColor, laserWorldPos)));
            }

            foreach (var co in lineCoroutines)
                yield return co;

            int totalScore = 300 + blockScoreSum;
            Debug.Log($"[LaserBlockSystem] === LASER COMPLETE === Score={totalScore} (base:300 + blockTierSum:{blockScoreSum})");
            OnLaserComplete?.Invoke(totalScore);
            activeLaserCount--;
        }

        // ============================================================
        // 방향별 블록 수집 (DrillBlockSystem과 동일 로직)
        // ============================================================

        private List<HexBlock> GetBlocksInDirection(HexCoord start, DrillDirection direction, bool positive)
        {
            List<HexBlock> blocks = new List<HexBlock>();
            if (hexGrid == null) return blocks;

            HexCoord delta = GetDirectionDelta(direction, positive);
            HexCoord current = start + delta;
            int maxSteps = 20;
            int step = 0;
            while (hexGrid.IsValidCoord(current) && step < maxSteps)
            {
                HexBlock block = hexGrid.GetBlock(current);
                if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                    blocks.Add(block);
                current = current + delta;
                step++;
            }
            return blocks;
        }

        private HexCoord GetDirectionDelta(DrillDirection direction, bool positive)
        {
            int sign = positive ? 1 : -1;
            switch (direction)
            {
                case DrillDirection.Vertical:  return new HexCoord(0, sign);
                case DrillDirection.Slash:     return new HexCoord(sign, -sign);
                case DrillDirection.BackSlash: return new HexCoord(sign, 0);
                default: return new HexCoord(0, sign);
            }
        }

        // ============================================================
        // 라인 파괴
        // ============================================================

private IEnumerator DestroyLine(List<HexBlock> targets, Color laserColor, Vector3 center)
        {
            List<Coroutine> destroyCoroutines = new List<Coroutine>();

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;

                // Bug #15 fix: target.Data가 동시 실행 중 null이 될 수 있음
                if (target.Data == null || target.Data.gemType == GemType.None) continue;

                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    Debug.Log($"[LaserBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
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
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithLaser(target, blockColor, center)));
                }

                yield return new WaitForSeconds(laserSpeed);
            }

            // 모든 파괴 애니메이션이 완료될 때까지 대기 (ClearData 보장)
            foreach (var co in destroyCoroutines)
                yield return co;
        }

        // ============================================================
        // 블록 파괴 이펙트
        // ============================================================

        private IEnumerator DestroyBlockWithLaser(HexBlock block, Color blockColor, Vector3 laserCenter)
        {
            if (block == null) yield break;

            Vector3 blockPos = block.transform.position;

            // 화이트 플래시 오버레이
            StartCoroutine(DestroyFlashOverlay(block));

            // 임팩트 이펙트
            StartCoroutine(LaserImpactWave(blockPos, blockColor));

            // 파편 (base tier with cascade)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int debrisCount = Mathf.RoundToInt((VisualConstants.DebrisBaseCount / 2 + Random.Range(0, 2)) * cascadeMult);
            for (int i = 0; i < debrisCount; i++)
                StartCoroutine(AnimateDebris(blockPos, blockColor));

            // 이중 이징 파괴: 확대 → 찌그러짐 + 축소
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

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            block.ClearData();
        }

        // ============================================================
        // 레이저 이펙트
        // ============================================================

        /// <summary>
        /// 중앙 폭발 플래시 (★ 모양)
        /// </summary>
        private IEnumerator LaserCenterFlash(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 레이어 (메인 플래시 뒤에)
            StartCoroutine(BloomLayer(pos, VisualConstants.FlashColorLaser, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            GameObject flash = new GameObject("LaserCenterFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(VisualConstants.FlashColorLaser.r, VisualConstants.FlashColorLaser.g, VisualConstants.FlashColorLaser.b, 1f);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            // 스파크 버스트 (large tier with cascade)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkLargeCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
                StartCoroutine(AnimateSpark(pos, color));

            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                img.color = new Color(VisualConstants.FlashColorLaser.r, VisualConstants.FlashColorLaser.g, VisualConstants.FlashColorLaser.b, (1f - t) * 0.9f);
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 레이저 빔 이펙트 - 얇은 광선이 뻗어나감
        /// </summary>
        private IEnumerator LaserBeamEffect(Vector3 pos, DrillDirection direction, bool positive, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // Main beam
            GameObject beam = new GameObject("LaserBeam");
            beam.transform.SetParent(parent, false);
            beam.transform.position = pos;

            var img = beam.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            Color beamColor = VisualConstants.LaserBrighten(color);
            beamColor.a = 0.9f;
            img.color = beamColor;

            RectTransform rt = beam.GetComponent<RectTransform>();
            float angle = GetDirectionAngle(direction, positive);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(6f, 0f);

            // White core beam (inner bright line)
            GameObject coreBeam = new GameObject("LaserCoreBeam");
            coreBeam.transform.SetParent(beam.transform, false);
            coreBeam.transform.localPosition = Vector3.zero;
            coreBeam.transform.localRotation = Quaternion.identity;

            var coreImg = coreBeam.AddComponent<UnityEngine.UI.Image>();
            coreImg.raycastTarget = false;
            coreImg.color = new Color(1f, 1f, 1f, 0.8f);

            RectTransform coreRt = coreBeam.GetComponent<RectTransform>();
            coreRt.pivot = new Vector2(0.5f, 0f);
            coreRt.sizeDelta = new Vector2(2f, 0f);

            // 빔 확장 (keeping existing cubic easing)
            float extendDuration = 0.12f;
            float elapsed = 0f;
            float maxLength = 800f;

            while (elapsed < extendDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / extendDuration;
                float easeT = VisualConstants.EaseOutCubic(t);
                rt.sizeDelta = new Vector2(6f, maxLength * easeT);
                coreRt.sizeDelta = new Vector2(2f, maxLength * easeT);
                yield return null;
            }

            // 빔 유지 + 페이드 + shimmer
            float fadeDuration = beamDuration;
            elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;

                // Width shimmer (micro pulse)
                float shimmer = 1f + 0.15f * Mathf.Sin(elapsed * 30f);
                float width = Mathf.Lerp(6f, 2f, t) * shimmer;
                rt.sizeDelta = new Vector2(width, maxLength);
                coreRt.sizeDelta = new Vector2(Mathf.Max(1f, width * 0.3f), maxLength);

                beamColor.a = 0.9f * (1f - t);
                img.color = beamColor;
                coreImg.color = new Color(1f, 1f, 1f, 0.8f * (1f - t));

                yield return null;
            }

            Destroy(beam);
        }

        private IEnumerator LaserImpactWave(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject wave = new GameObject("LaserImpactWave");
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

        private IEnumerator AnimateSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject spark = new GameObject("LaserSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            Color bright = VisualConstants.LaserBrighten(color);
            bright.a = 1f;
            img.color = bright;

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
                bright.a = 1f - t;
                img.color = bright;
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);
                yield return null;
            }

            Destroy(spark);
        }

        private IEnumerator AnimateDebris(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject debris = new GameObject("LaserDebris");
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
            float w = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax * 0.9f);
            float h = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax * 0.7f);
            rt.sizeDelta = new Vector2(w, h);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.DebrisBaseSpeedMin, VisualConstants.DebrisBaseSpeedMax);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float rotSpeed = Random.Range(VisualConstants.DebrisRotSpeedMin, VisualConstants.DebrisRotSpeedMax);
            float lifetime = Random.Range(VisualConstants.DebrisLifetimeMin, VisualConstants.DebrisLifetimeMax);
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
        // 유틸리티
        // ============================================================

        private float GetDirectionAngle(DrillDirection direction, bool positive)
        {
            float angle;
            switch (direction)
            {
                case DrillDirection.Vertical:  angle = -90f; break;
                case DrillDirection.Slash:     angle = 30f;  break;
                case DrillDirection.BackSlash: angle = -30f; break;
                default: angle = -90f; break;
            }
            return positive ? angle : angle + 180f;
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
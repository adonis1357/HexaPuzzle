using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// X 특수 블록 시스템
    /// 중앙 블록 주변 6개가 모두 같은 색일 때 중앙에 생성
    /// 발동 시 같은 색 블록을 보드 전체에서 제거
    /// </summary>
    public class XBlockSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("XBlock Settings")]
        [SerializeField] private float waveDelay = 0.03f;

        [Header("Effect Settings")]
        [SerializeField] private int sparkCount = 20;

        public event System.Action<int> OnXBlockComplete;

        private int activeXBlockCount = 0;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        public bool IsActivating => activeXBlockCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);
        public void ForceReset()
        {
            activeXBlockCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.LogWarning("[XBlockSystem] ForceReset called");
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

        // X 아이콘 스프라이트 (static 캐시)
        private static Sprite xBlockIconSprite;

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[XBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[XBlockSystem] HexGrid not found!");
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
                GameObject effectLayer = new GameObject("XBlockEffectLayer");
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
        // X 아이콘 스프라이트 생성
        // ============================================================

        public static Sprite GetXBlockIconSprite()
        {
            if (xBlockIconSprite == null)
                xBlockIconSprite = CreateXSprite(64);
            return xBlockIconSprite;
        }

        /// <summary>
        /// X 아이콘 프로시저럴 생성 - 굵은 X 마크 + 글로우
        /// </summary>
        private static Sprite CreateXSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float armLength = size * 0.35f;
            float armWidth = size * 0.1f;
            float glowRadius = size * 0.06f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y) - center;

                    // X의 두 대각선까지의 거리
                    // 대각선 1: y = x (45도)
                    float dist1 = Mathf.Abs(p.x - p.y) / 1.414f;
                    // 대각선 2: y = -x (135도)
                    float dist2 = Mathf.Abs(p.x + p.y) / 1.414f;

                    // 팔 길이 제한
                    float fromCenter = p.magnitude;
                    if (fromCenter > armLength + glowRadius) continue;

                    float minDist = Mathf.Min(dist1, dist2);

                    // 코어 X (파스텔 라벤더)
                    if (minDist < armWidth && fromCenter <= armLength)
                    {
                        float edge = minDist / armWidth;
                        float aa = Mathf.Clamp01(1f - (minDist - armWidth + 1.5f) / 1.5f);

                        // 3D 느낌 - 중앙이 밝고 가장자리가 어두움
                        float highlight = Mathf.Pow(1f - edge, 2f) * 0.3f;
                        Color c = new Color(0.88f + highlight * 0.05f, 0.80f + highlight * 0.05f, 0.96f, aa);
                        pixels[y * size + x] = c;
                    }
                    // 글로우 (파스텔 라벤더 글로우)
                    else if (minDist < armWidth + glowRadius && fromCenter <= armLength + glowRadius)
                    {
                        float glowT = (minDist - armWidth) / glowRadius;
                        float glowAlpha = Mathf.Clamp01(1f - glowT) * 0.4f;

                        // 가장자리 페이드
                        float edgeFade = Mathf.Clamp01((armLength + glowRadius - fromCenter) / glowRadius);
                        glowAlpha *= edgeFade;

                        Color glowColor = new Color(0.82f, 0.76f, 0.95f, glowAlpha);
                        if (pixels[y * size + x].a < glowAlpha)
                            pixels[y * size + x] = glowColor;
                    }
                }
            }

            // 중앙 빛나는 점
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float centerDist = Vector2.Distance(new Vector2(x, y), center);
                    if (centerDist < size * 0.1f)
                    {
                        float sa = Mathf.Clamp01((size * 0.1f - centerDist) / (size * 0.1f));
                        Color sparkColor = new Color(1f, 1f, 0.95f, sa * 0.8f);
                        pixels[y * size + x] = Color.Lerp(pixels[y * size + x], sparkColor, sa);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // X 블록 생성
        // ============================================================

        public void CreateXBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData xData = new BlockData(gemType);
            xData.specialType = SpecialBlockType.XBlock;
            block.SetBlockData(xData);
            Debug.Log($"[XBlockSystem] Created X-block at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // X 블록 발동
        // ============================================================

        public void ActivateXBlock(HexBlock xBlock)
        {
            if (xBlock == null) return;
            if (xBlock.Data == null || xBlock.Data.specialType != SpecialBlockType.XBlock)
                return;
            Debug.Log($"[XBlockSystem] Activating X-block at {xBlock.Coord}");
            StartCoroutine(XBlockCoroutine(xBlock));
        }

        private IEnumerator XBlockCoroutine(HexBlock xBlock)
        {
            activeXBlockCount++;
            activeBlocks.Add(xBlock);

            HexCoord xCoord = xBlock.Coord;
            Vector3 xWorldPos = xBlock.transform.position;
            GemType targetGemType = xBlock.Data.gemType;
            Color xColor = GemColors.GetColor(targetGemType);

            Debug.Log($"[XBlockSystem] === X-BLOCK ACTIVATED === Coord={xCoord}, TargetColor={targetGemType}");

            // Pre-Fire 압축 애니메이션
            yield return StartCoroutine(PreFireCompression(xBlock));

            // Hit Stop (Medium tier)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationMedium));

            // Zoom Punch (Small tier)
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // 발사 순간 블록 클리어
            xBlock.ClearData();

            // 같은 색 블록 전부 수집
            List<HexBlock> targets = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block == xBlock) continue;

                    if (block.Data.gemType == targetGemType)
                        targets.Add(block);
                }
            }

            Debug.Log($"[XBlockSystem] Targets: {targets.Count} same-color blocks ({targetGemType})");

            // X 중심 이펙트
            StartCoroutine(XCenterEffect(xWorldPos, xColor));

            // X 확산 이펙트
            StartCoroutine(XWaveExpand(xWorldPos, xColor));

            // Screen shake (Medium tier - newly added for XBlock)
            StartCoroutine(ScreenShake(VisualConstants.ShakeMediumIntensity, VisualConstants.ShakeMediumDuration));

            yield return new WaitForSeconds(0.15f);

            // 거리순 정렬 (중심에서 가까운 것부터)
            targets.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, xWorldPos);
                float distB = Vector3.Distance(b.transform.position, xWorldPos);
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
                    Debug.Log($"[XBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
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

                    // 적군 점수 (X블록 = SpecialAdvanced)
                    var sm = GameManager.Instance?.GetComponent<ScoreManager>();
                    if (sm != null)
                    {
                        if (target.Data.hasThorn)
                            sm.AddEnemyScore(EnemyType.ThornParasite, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        if (target.Data.hasChain)
                            sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                        if (target.Data.gemType == GemType.Gray)
                            sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.SpecialAdvanced,
                                RemovalCondition.Normal, target.transform.position);
                    }

                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithX(target, blockColor, xWorldPos)));
                }

                yield return new WaitForSeconds(waveDelay);
            }

            // 모든 파괴 애니메이션이 완료될 때까지 대기 (ClearData 보장)
            foreach (var co in destroyCoroutines)
                yield return co;

            int totalScore = 500 + blockScoreSum;
            Debug.Log($"[XBlockSystem] === X-BLOCK COMPLETE === Score={totalScore} (base:500 + blockTierSum:{blockScoreSum}), Destroyed={targets.Count}");
            OnXBlockComplete?.Invoke(totalScore);
            activeBlocks.Remove(xBlock);
            activeXBlockCount--;
        }

        // ============================================================
        // 이펙트
        // ============================================================

        /// <summary>
        /// 중앙 X 이펙트 - X자 플래시 + 충격파
        /// </summary>
        private IEnumerator XCenterEffect(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 레이어 (메인 플래시 뒤에)
            StartCoroutine(BloomLayer(pos, color, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // 1) 밝은 플래시
            GameObject flash = new GameObject("XBlockFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(color.r, color.g, color.b, 1f);

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            flashRt.sizeDelta = new Vector2(initSize, initSize);

            // 2) X자 형태의 두 줄
            GameObject line1 = CreateXLine(pos, parent, 45f, color);
            GameObject line2 = CreateXLine(pos, parent, -45f, color);

            // 3) 충격파 링
            GameObject ring = new GameObject("XBlockRing");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;

            var ringImg = ring.AddComponent<UnityEngine.UI.Image>();
            ringImg.sprite = HexBlock.GetHexFlashSprite();
            ringImg.raycastTarget = false;
            Color darkColor = VisualConstants.Darken(color);
            ringImg.color = new Color(darkColor.r, darkColor.g, darkColor.b, 0.7f);

            RectTransform ringRt = ring.GetComponent<RectTransform>();
            float ringInitSize = VisualConstants.WaveLargeInitialSize;
            ringRt.sizeDelta = new Vector2(ringInitSize, ringInitSize);

            // 4) 스파크 버스트 (large tier with cascade, diagonal bias preserved)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int totalSparks = Mathf.RoundToInt(sparkCount * cascadeMult);
            for (int i = 0; i < totalSparks; i++)
            {
                Color sparkColor = VisualConstants.Brighten(color);
                sparkColor.a = 1f;
                StartCoroutine(XSpark(pos, sparkColor));
            }

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
                flashImg.color = new Color(color.r, color.g, color.b, (1f - t) * 0.9f);

                // X 라인 확장 with EaseOutCubic (0 -> 320px)
                AnimateXLine(line1, eased, 45f);
                AnimateXLine(line2, eased, -45f);

                // 충격파 링 with easing
                float ringScale = 1f + eased * (VisualConstants.WaveLargeExpand - 1f);
                ringRt.sizeDelta = new Vector2(ringInitSize * ringScale, ringInitSize * ringScale);
                ringImg.color = new Color(darkColor.r, darkColor.g, darkColor.b, (1f - t) * 0.5f);

                yield return null;
            }

            Destroy(flash);
            Destroy(line1);
            Destroy(line2);
            Destroy(ring);
        }

        private GameObject CreateXLine(Vector3 pos, Transform parent, float angle, Color color)
        {
            GameObject line = new GameObject("XLine");
            line.transform.SetParent(parent, false);
            line.transform.position = pos;
            line.transform.localRotation = Quaternion.Euler(0, 0, angle);

            var img = line.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            Color lineColor = VisualConstants.Brighten(color);
            lineColor.a = 0.9f;
            img.color = lineColor;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(5f, 20f);

            return line;
        }

        private void AnimateXLine(GameObject line, float easedT, float angle)
        {
            if (line == null) return;
            var rt = line.GetComponent<RectTransform>();
            var img = line.GetComponent<UnityEngine.UI.Image>();
            if (rt == null || img == null) return;

            // X-line expands 0 -> 320px with EaseOutCubic
            float length = 20f + easedT * 300f;
            float width = 5f * (1f - easedT * 0.5f);
            rt.sizeDelta = new Vector2(width, length);

            Color c = img.color;
            c.a = (1f - easedT) * 0.8f;
            img.color = c;
        }

        /// <summary>
        /// X 웨이브 확산 - 같은 색상으로 퍼지는 웨이브
        /// </summary>
        private IEnumerator XWaveExpand(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            float initSize = VisualConstants.WaveLargeInitialSize;

            GameObject wave = new GameObject("XWaveExpand");
            wave.transform.SetParent(parent, false);
            wave.transform.position = center;

            var img = wave.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0.5f);

            RectTransform rt = wave.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(initSize, initSize);

            float duration = VisualConstants.WaveLargeDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                float scale = 1f + eased * (VisualConstants.WaveLargeExpand - 1f) * 2.5f;
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);

                img.color = new Color(color.r, color.g, color.b, (1f - t) * 0.35f);

                yield return null;
            }

            Destroy(wave);
        }

        /// <summary>
        /// 개별 블록 X 파괴 이펙트
        /// </summary>
        private IEnumerator DestroyBlockWithX(HexBlock block, Color blockColor, Vector3 center)
        {
            if (block == null) yield break;

            Vector3 blockPos = block.transform.position;

            // 화이트 플래시 오버레이
            StartCoroutine(DestroyFlashOverlay(block));

            // Micro X-flash before destruction (X identity)
            StartCoroutine(MicroXFlash(blockPos, blockColor));

            // X형 스파크 (small tier with cascade, diagonal bias preserved)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkSmallCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
            {
                Color sparkColor = VisualConstants.Brighten(blockColor);
                sparkColor.a = 1f;
                StartCoroutine(XSpark(blockPos, sparkColor));
            }

            // 이중 이징 + 45도 회전 (X 정체성 유지)
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

                // 45 degree rotation (X identity)
                float rot = t * 45f;
                block.transform.localRotation = Quaternion.Euler(0, 0, rot);

                yield return null;
            }

            block.transform.localScale = Vector3.one;
            block.transform.localRotation = Quaternion.identity;
            block.ClearData();
        }

        /// <summary>
        /// X 스파크 - 대각선 방향 위주로 발사
        /// </summary>
        private IEnumerator XSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject spark = new GameObject("XSpark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = color;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkMediumSizeMin, VisualConstants.SparkMediumSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            // X 방향 (대각선 위주 - X identity preserved)
            float[] xAngles = { 45f, 135f, 225f, 315f };
            float baseAngle = xAngles[Random.Range(0, xAngles.Length)];
            float angle = (baseAngle + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
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

                color.a = 1f - t;
                img.color = color;

                float sc = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(sc, sc);

                yield return null;
            }

            Destroy(spark);
        }

        /// <summary>
        /// Micro X-flash before block destruction (X identity polish)
        /// Quick flash in X shape at block position
        /// </summary>
        private IEnumerator MicroXFlash(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // Two small X lines
            GameObject l1 = new GameObject("MicroXFlash1");
            l1.transform.SetParent(parent, false);
            l1.transform.position = pos;
            l1.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            var img1 = l1.AddComponent<UnityEngine.UI.Image>();
            img1.raycastTarget = false;
            Color bright = VisualConstants.Brighten(color);
            bright.a = 0.8f;
            img1.color = bright;
            RectTransform rt1 = l1.GetComponent<RectTransform>();
            rt1.sizeDelta = new Vector2(2f, 15f);

            GameObject l2 = new GameObject("MicroXFlash2");
            l2.transform.SetParent(parent, false);
            l2.transform.position = pos;
            l2.transform.localRotation = Quaternion.Euler(0, 0, -45f);
            var img2 = l2.AddComponent<UnityEngine.UI.Image>();
            img2.raycastTarget = false;
            img2.color = bright;
            RectTransform rt2 = l2.GetComponent<RectTransform>();
            rt2.sizeDelta = new Vector2(2f, 15f);

            float duration = 0.08f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scale = 1f + t * 2f;
                rt1.sizeDelta = new Vector2(2f, 15f * scale);
                rt2.sizeDelta = new Vector2(2f, 15f * scale);

                bright.a = 0.8f * (1f - t);
                img1.color = bright;
                img2.color = bright;

                yield return null;
            }

            Destroy(l1);
            Destroy(l2);
        }

        // ============================================================
        // 화면 흔들림 (newly added for XBlock)
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
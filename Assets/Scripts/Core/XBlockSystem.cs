using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

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

        public bool IsActivating => activeXBlockCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public void ForceReset()
        {
            activeXBlockCount = 0;
            pendingSpecialBlocks.Clear();
            StopAllCoroutines();
            Debug.LogWarning("[XBlockSystem] ForceReset called");
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

                    // 코어 X (흰색, 불투명)
                    if (minDist < armWidth && fromCenter <= armLength)
                    {
                        float edge = minDist / armWidth;
                        float aa = Mathf.Clamp01(1f - (minDist - armWidth + 1.5f) / 1.5f);

                        // 3D 느낌 - 중앙이 밝고 가장자리가 어두움
                        float highlight = Mathf.Pow(1f - edge, 2f) * 0.3f;
                        Color c = new Color(0.95f + highlight * 0.05f, 0.95f + highlight * 0.05f, 1f, aa);
                        pixels[y * size + x] = c;
                    }
                    // 글로우 (부드러운 바깥 빛)
                    else if (minDist < armWidth + glowRadius && fromCenter <= armLength + glowRadius)
                    {
                        float glowT = (minDist - armWidth) / glowRadius;
                        float glowAlpha = Mathf.Clamp01(1f - glowT) * 0.4f;

                        // 가장자리 페이드
                        float edgeFade = Mathf.Clamp01((armLength + glowRadius - fromCenter) / glowRadius);
                        glowAlpha *= edgeFade;

                        Color glowColor = new Color(0.7f, 0.85f, 1f, glowAlpha);
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

            HexCoord xCoord = xBlock.Coord;
            Vector3 xWorldPos = xBlock.transform.position;
            GemType targetGemType = xBlock.Data.gemType;
            Color xColor = GemColors.GetColor(targetGemType);

            Debug.Log($"[XBlockSystem] === X-BLOCK ACTIVATED === Coord={xCoord}, TargetColor={targetGemType}");

            // X블록 자체 클리어
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

            yield return new WaitForSeconds(0.15f);

            // 거리순 정렬 (중심에서 가까운 것부터)
            targets.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a.transform.position, xWorldPos);
                float distB = Vector3.Distance(b.transform.position, xWorldPos);
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
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    StartCoroutine(DestroyBlockWithX(target, blockColor, xWorldPos));
                }

                yield return new WaitForSeconds(waveDelay);
            }

            // 완료 대기
            yield return new WaitForSeconds(0.2f);

            int totalScore = 500 + targets.Count * 100;
            Debug.Log($"[XBlockSystem] === X-BLOCK COMPLETE === Score={totalScore}, Destroyed={targets.Count}");
            OnXBlockComplete?.Invoke(totalScore);
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

            // 1) 밝은 플래시
            GameObject flash = new GameObject("XBlockFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(color.r, color.g, color.b, 1f);

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(25f, 25f);

            // 2) X자 형태의 두 줄
            GameObject line1 = CreateXLine(pos, parent, 45f, color);
            GameObject line2 = CreateXLine(pos, parent, -45f, color);

            // 3) 충격파 링
            GameObject ring = new GameObject("XBlockRing");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;

            var ringImg = ring.AddComponent<UnityEngine.UI.Image>();
            ringImg.raycastTarget = false;
            ringImg.color = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, 0.7f);

            RectTransform ringRt = ring.GetComponent<RectTransform>();
            ringRt.sizeDelta = new Vector2(10f, 10f);

            // 4) 스파크 버스트
            for (int i = 0; i < sparkCount; i++)
            {
                Color sparkColor = new Color(
                    Mathf.Min(1f, color.r + Random.Range(0f, 0.3f)),
                    Mathf.Min(1f, color.g + Random.Range(0f, 0.3f)),
                    Mathf.Min(1f, color.b + Random.Range(0f, 0.3f)),
                    1f
                );
                StartCoroutine(XSpark(pos, sparkColor));
            }

            float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 플래시 확장 + 페이드
                float flashScale = 1f + t * 8f;
                flashRt.sizeDelta = new Vector2(25f * flashScale, 25f * flashScale);
                flashImg.color = new Color(color.r, color.g, color.b, (1f - t) * 0.9f);

                // X 라인 확장
                AnimateXLine(line1, t, 45f);
                AnimateXLine(line2, t, -45f);

                // 충격파 링
                float ringScale = 1f + t * 12f;
                ringRt.sizeDelta = new Vector2(10f * ringScale, 10f * ringScale);
                ringImg.color = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, (1f - t) * 0.5f);

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

            Color lineColor = new Color(
                Mathf.Min(1f, color.r + 0.2f),
                Mathf.Min(1f, color.g + 0.2f),
                Mathf.Min(1f, color.b + 0.2f),
                0.9f
            );
            img.color = lineColor;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(5f, 20f);

            return line;
        }

        private void AnimateXLine(GameObject line, float t, float angle)
        {
            if (line == null) return;
            var rt = line.GetComponent<RectTransform>();
            var img = line.GetComponent<UnityEngine.UI.Image>();
            if (rt == null || img == null) return;

            float length = 20f + t * 300f;
            float width = 5f * (1f - t * 0.5f);
            rt.sizeDelta = new Vector2(width, length);

            Color c = img.color;
            c.a = (1f - t) * 0.8f;
            img.color = c;
        }

        /// <summary>
        /// X 웨이브 확산 - 같은 색상으로 퍼지는 웨이브
        /// </summary>
        private IEnumerator XWaveExpand(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject wave = new GameObject("XWaveExpand");
            wave.transform.SetParent(parent, false);
            wave.transform.position = center;

            var img = wave.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0.5f);

            RectTransform rt = wave.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(10f, 10f);

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float scale = 1f + t * 25f;
                rt.sizeDelta = new Vector2(10f * scale, 10f * scale);

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

            // X형 스파크
            for (int i = 0; i < 4; i++)
            {
                Color sparkColor = new Color(
                    Mathf.Min(1f, blockColor.r + Random.Range(0f, 0.2f)),
                    Mathf.Min(1f, blockColor.g + Random.Range(0f, 0.2f)),
                    Mathf.Min(1f, blockColor.b + Random.Range(0f, 0.2f)),
                    1f
                );
                StartCoroutine(XSpark(blockPos, sparkColor));
            }

            // 블록 축소 + X 플래시
            float duration = 0.15f;
            float elapsed = 0f;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float shrink = 1f - t;
                block.transform.localScale = origScale * shrink;

                // 약간의 회전 (X답게 45도로)
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
            float size = Random.Range(3f, 9f);
            rt.sizeDelta = new Vector2(size, size);

            // X 방향 (대각선 위주)
            float[] xAngles = { 45f, 135f, 225f, 315f };
            float baseAngle = xAngles[Random.Range(0, xAngles.Length)];
            float angle = (baseAngle + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
            float speed = Random.Range(150f, 380f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(0.15f, 0.35f);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= 0.93f;

                color.a = 1f - t;
                img.color = color;

                float sc = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(sc, sc);

                yield return null;
            }

            Destroy(spark);
        }
    }
}
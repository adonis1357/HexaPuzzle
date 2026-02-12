using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

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

        [Header("Effect Settings")]
        [SerializeField] private int debrisCount = 8;
        [SerializeField] private float shakeIntensity = 12f;

        public event System.Action<int> OnBombComplete;

        private int activeBombCount = 0;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        public bool IsBombing => activeBombCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public void ForceReset()
        {
            activeBombCount = 0;
            pendingSpecialBlocks.Clear();
            StopAllCoroutines();
            Debug.LogWarning("[BombBlockSystem] ForceReset called");
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

                    // 원형 본체
                    if (dist < bodyRadius)
                    {
                        float edge = dist / bodyRadius;
                        float highlight = Mathf.Pow(1f - edge, 3f) * 0.3f;
                        Color c = new Color(0.15f + highlight, 0.15f + highlight, 0.18f + highlight, 1f);
                        // 하이라이트 (좌상단 빛)
                        Vector2 lightDir = (p - center).normalized;
                        float lightDot = Vector2.Dot(lightDir, new Vector2(-0.5f, 0.5f));
                        if (lightDot > 0)
                            c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.55f, 1f), lightDot * 0.4f * (1f - edge));
                        // 안티앨리어싱
                        float aa = Mathf.Clamp01((bodyRadius - dist) * 2f);
                        c.a = aa;
                        pixels[y * size + x] = c;
                    }

                    // 도화선 (본체 위쪽에서 오른쪽 위로 곡선)
                    float fuseX = center.x + (y - (center.y + bodyRadius * 0.7f)) * 0.4f;
                    float fuseY = y;
                    if (fuseY > center.y + bodyRadius * 0.6f && fuseY < center.y + bodyRadius * 1.6f)
                    {
                        float fuseDist = Mathf.Abs(x - fuseX);
                        if (fuseDist < 2f)
                        {
                            float fa = Mathf.Clamp01((2f - fuseDist) * 0.8f);
                            Color fuseColor = new Color(0.4f, 0.3f, 0.2f, fa);
                            if (pixels[y * size + x].a < fa)
                                pixels[y * size + x] = fuseColor;
                        }
                    }

                    // 스파크 (도화선 끝)
                    float sparkCenterX = center.x + (center.y + bodyRadius * 1.5f - (center.y + bodyRadius * 0.7f)) * 0.4f;
                    float sparkCenterY = center.y + bodyRadius * 1.5f;
                    float sparkDist = Vector2.Distance(p, new Vector2(sparkCenterX, sparkCenterY));
                    if (sparkDist < size * 0.08f)
                    {
                        float sa = Mathf.Clamp01((size * 0.08f - sparkDist) / (size * 0.08f));
                        Color sparkColor = new Color(1f, 0.8f, 0.2f, sa * 0.9f);
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

            HexCoord bombCoord = bombBlock.Coord;
            Vector3 bombWorldPos = bombBlock.transform.position;
            Color bombColor = GemColors.GetColor(bombBlock.Data.gemType);

            Debug.Log($"[BombBlockSystem] === BOMB ACTIVATED === Coord={bombCoord}");

            // 폭탄 블록 자체 클리어
            bombBlock.ClearData();

            // 인접 6칸 타겟 수집
            List<HexBlock> targets = new List<HexBlock>();
            if (hexGrid != null)
            {
                var neighbors = hexGrid.GetNeighbors(bombCoord);
                foreach (var neighbor in neighbors)
                {
                    if (neighbor != null && neighbor.Data != null && neighbor.Data.gemType != GemType.None)
                        targets.Add(neighbor);
                }
            }

            Debug.Log($"[BombBlockSystem] Targets: {targets.Count} neighbors");

            // 폭발 시작 이펙트
            StartCoroutine(BombExplosionEffect(bombWorldPos, bombColor));
            StartCoroutine(ScreenShake(shakeIntensity, 0.2f));

            // 약간의 딜레이 후 주변 블록 파괴
            yield return new WaitForSeconds(0.05f);

            // 방사형으로 순차 파괴 - 모든 파괴 코루틴을 추적
            List<Coroutine> destroyCoroutines = new List<Coroutine>();

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;

                // 특수 블록이면 pending에 추가
                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    Debug.Log($"[BombBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
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
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithExplosion(target, blockColor, bombWorldPos)));
                }

                yield return new WaitForSeconds(explosionDelay);
            }

            // 모든 파괴 애니메이션이 완료될 때까지 대기 (ClearData 보장)
            foreach (var co in destroyCoroutines)
                yield return co;

            int totalScore = 200 + targets.Count * 80;
            Debug.Log($"[BombBlockSystem] === BOMB COMPLETE === Score={totalScore}");
            OnBombComplete?.Invoke(totalScore);
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

            // 1) 중앙 밝은 플래시
            GameObject flash = new GameObject("BombFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var flashImg = flash.AddComponent<UnityEngine.UI.Image>();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(1f, 0.9f, 0.4f, 1f);

            RectTransform flashRt = flash.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(30f, 30f);

            // 2) 충격파 링 (2중)
            GameObject ring1 = CreateExplosionRing(pos, parent, new Color(1f, 0.6f, 0.1f, 0.8f));
            GameObject ring2 = CreateExplosionRing(pos, parent, new Color(1f, 0.3f, 0.1f, 0.5f));

            // 3) 스파크 버스트 (대량)
            for (int i = 0; i < 16; i++)
                StartCoroutine(ExplosionSpark(pos, color));

            // 4) 파편
            int count = debrisCount + Random.Range(2, 6);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateExplosionDebris(pos, color));

            // 애니메이션
            float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 플래시 확장 + 페이드
                float flashScale = 1f + t * 8f;
                flashRt.sizeDelta = new Vector2(30f * flashScale, 30f * flashScale);
                flashImg.color = new Color(1f, 0.9f, 0.4f, (1f - t) * 0.9f);

                // 충격파 링 1
                AnimateRing(ring1, t, 12f);
                // 충격파 링 2 (약간 느리게)
                AnimateRing(ring2, Mathf.Clamp01(t * 0.8f), 10f);

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

            // 임팩트 웨이브
            StartCoroutine(ImpactWave(blockPos, blockColor));

            // 파편
            int count = debrisCount / 2 + Random.Range(0, 3);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateExplosionDebris(blockPos, blockColor));

            // 블록 밀려나기 + 축소
            float duration = 0.15f;
            float elapsed = 0f;
            Vector3 origScale = block.transform.localScale;
            Vector3 origPos = block.transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 밀려나기
                float pushAmount = 15f * Mathf.Sin(t * Mathf.PI * 0.5f);
                block.transform.position = origPos + pushDir * pushAmount;

                // 찌그러짐 + 축소
                float sx = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);
                float sy = 1f - t;
                block.transform.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);

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
            image.color = new Color(color.r, color.g, color.b, 0.5f);

            RectTransform rt = wave.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(10f, 10f);

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = 1f + t * 4f;
                rt.sizeDelta = new Vector2(10f * scale, 10f * scale);
                image.color = new Color(color.r, color.g, color.b, 0.5f * (1f - t));
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

            Color bright = new Color(
                Mathf.Min(1f, color.r + 0.4f),
                Mathf.Min(1f, color.g + 0.3f),
                Mathf.Min(1f, color.b + 0.1f),
                1f
            );
            img.color = bright;

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(3f, 9f);
            rt.sizeDelta = new Vector2(size, size);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(150f, 450f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(0.15f, 0.35f);
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= 0.92f;
                bright.a = 1f - t;
                img.color = bright;
                float s = size * (1f - t * 0.6f);
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
            float w = Random.Range(5f, 14f);
            float h = Random.Range(5f, 12f);
            rt.sizeDelta = new Vector2(w, h);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(200f, 500f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float gravityY = -900f;
            float rotSpeed = Random.Range(-900f, 900f);
            float lifetime = Random.Range(0.3f, 0.6f);
            float elapsed = 0f;
            float rot = Random.Range(0f, 360f);

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;

                velocity.y += gravityY * Time.deltaTime;
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
        // 화면 흔들림
        // ============================================================

        private IEnumerator ScreenShake(float intensity, float duration)
        {
            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 originalPos = target.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - (elapsed / duration);
                float x = Random.Range(-1f, 1f) * intensity * t;
                float y = Random.Range(-1f, 1f) * intensity * t;
                target.localPosition = originalPos + new Vector3(x, y, 0);
                yield return null;
            }

            target.localPosition = originalPos;
        }
    }
}
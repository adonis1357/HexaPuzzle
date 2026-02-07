using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    public class DrillBlockSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem removalSystem;

        [Header("Drill Settings")]
        [SerializeField] private float drillSpeed = 0.06f;
        [SerializeField] private float projectileSpeed = 1200f;

        [Header("Effect Settings")]
        [SerializeField] private int debrisCount = 6;
        [SerializeField] private float shakeIntensity = 8f;

        public event System.Action<int> OnDrillComplete;

        private bool isDrilling = false;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();

        public bool IsDrilling => isDrilling;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;


        // 이펙트 생성용 Canvas 내 부모 Transform
        private Transform effectParent;

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[DrillBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[DrillBlockSystem] HexGrid not found!");
            }
            if (removalSystem == null)
                removalSystem = FindObjectOfType<BlockRemovalSystem>();

            // Canvas 내부에 이펙트 컨테이너 생성
            SetupEffectParent();
        }

        private void SetupEffectParent()
        {
            if (hexGrid == null) return;
            
            // GridContainer의 부모(Canvas) 아래에 이펙트 레이어 생성
            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                GameObject effectLayer = new GameObject("DrillEffectLayer");
                effectLayer.transform.SetParent(canvas.transform, false);
                
                RectTransform rt = effectLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                // GridContainer보다 위에 렌더링
                effectLayer.transform.SetAsLastSibling();
                
                effectParent = effectLayer.transform;
                Debug.Log("[DrillBlockSystem] Effect layer created under Canvas");
            }
            else
            {
                effectParent = hexGrid.transform;
                Debug.LogWarning("[DrillBlockSystem] Canvas not found, using hexGrid as effect parent");
            }
        }

        // ============================================================
        // 드릴 생성
        // ============================================================

        public void CreateDrillBlock(HexBlock block, DrillDirection direction, GemType gemType)
        {
            if (block == null) return;
            BlockData drillData = new BlockData(gemType);
            drillData.specialType = SpecialBlockType.Drill;
            drillData.drillDirection = direction;
            block.SetBlockData(drillData);
            block.ShowDrillIndicator(direction);
            Debug.Log($"[DrillBlockSystem] Created drill at {block.Coord}, direction={direction}");
        }

        // ============================================================
        // 드릴 발동
        // ============================================================

        public void ActivateDrill(HexBlock drillBlock)
        {
            if (isDrilling || drillBlock == null) return;
            if (drillBlock.Data == null || drillBlock.Data.specialType != SpecialBlockType.Drill)
                return;
            Debug.Log($"[DrillBlockSystem] Activating drill at {drillBlock.Coord}, direction={drillBlock.Data.drillDirection}");
            StartCoroutine(DrillCoroutine(drillBlock));
        }

private IEnumerator DrillCoroutine(HexBlock drillBlock)
        {
            isDrilling = true;
            pendingSpecialBlocks.Clear();

            DrillDirection direction = drillBlock.Data.drillDirection;
            HexCoord startCoord = drillBlock.Coord;
            Vector3 drillWorldPos = drillBlock.transform.position;
            Color drillColor = GetDrillColor(drillBlock);

            Debug.Log($"[DrillBlockSystem] === DRILL ACTIVATED === Coord={startCoord}, Direction={direction}, WorldPos={drillWorldPos}");

            // 드릴 블록 자체를 먼저 클리어
            drillBlock.ClearData();

            // 양방향 타겟 수집
            List<HexBlock> targets1 = GetBlocksInDirection(startCoord, direction, true);
            List<HexBlock> targets2 = GetBlocksInDirection(startCoord, direction, false);

            Debug.Log($"[DrillBlockSystem] Targets: positive={targets1.Count}, negative={targets2.Count}");
            foreach (var t in targets1)
                Debug.Log($"[DrillBlockSystem]   +target: {t.Coord} pos={t.transform.position}");
            foreach (var t in targets2)
                Debug.Log($"[DrillBlockSystem]   -target: {t.Coord} pos={t.transform.position}");

            // 드릴 발사 시작 이펙트 (중앙 폭발)
            StartCoroutine(DrillLaunchEffect(drillWorldPos, direction, drillColor));

            // 양방향 동시 발사
            Coroutine drill1 = StartCoroutine(DrillLineWithProjectile(
                drillWorldPos, targets1, direction, true, drillColor));
            Coroutine drill2 = StartCoroutine(DrillLineWithProjectile(
                drillWorldPos, targets2, direction, false, drillColor));

            yield return drill1;
            yield return drill2;

            int totalScore = 100 + (targets1.Count + targets2.Count) * 50;
            Debug.Log($"[DrillBlockSystem] === DRILL COMPLETE === Score={totalScore}");
            OnDrillComplete?.Invoke(totalScore);
            isDrilling = false;
        }

        // ============================================================
        // 방향별 블록 수집
        // ============================================================

        private List<HexBlock> GetBlocksInDirection(HexCoord start, DrillDirection direction, bool positive)
        {
            List<HexBlock> blocks = new List<HexBlock>();
            if (hexGrid == null) return blocks;

            HexCoord delta = GetDirectionDelta(direction, positive);
            HexCoord current = start + delta;

            int maxSteps = 20; // 안전장치
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

        /// <summary>
        /// DrillDirection에 따른 hex 좌표 이동 delta
        /// Vertical: r축 (화면상 상하)
        /// Slash: q+,r- 대각선 (화면상 / 방향)
        /// BackSlash: q축 (화면상 수평... 이름과 다르지만 hex구조상 이렇게 됨)
        /// </summary>
        private HexCoord GetDirectionDelta(DrillDirection direction, bool positive)
        {
            int sign = positive ? 1 : -1;
            switch (direction)
            {
                case DrillDirection.Vertical:  return new HexCoord(0, sign);       // r축 = 화면 상하
                case DrillDirection.Slash:     return new HexCoord(sign, -sign);   // 대각 / 방향
                case DrillDirection.BackSlash: return new HexCoord(sign, 0);       // q축 = 화면 수평(약간 대각)
                default: return new HexCoord(0, sign);
            }
        }

        // ============================================================
        // 드릴 투사체 + 라인 파괴 애니메이션
        // ============================================================

private IEnumerator DrillLineWithProjectile(
            Vector3 startPos, List<HexBlock> targets, DrillDirection direction,
            bool positive, Color drillColor)
        {
            if (targets.Count == 0) yield break;

            Vector3 firstTargetPos = targets[0].transform.position;
            Vector3 initDir = (firstTargetPos - startPos).normalized;
            float initAngle = Mathf.Atan2(initDir.y, initDir.x) * Mathf.Rad2Deg;

            GameObject projectile = CreateProjectileWithAngle(startPos, initAngle, drillColor);

            Vector3 currentPos = startPos;
            Vector3 lastMoveDir = initDir;

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null) continue;

                Vector3 targetPos = target.transform.position;
                lastMoveDir = (targetPos - currentPos).normalized;

                float moveAngle = Mathf.Atan2(lastMoveDir.y, lastMoveDir.x) * Mathf.Rad2Deg;
                if (projectile != null)
                    projectile.transform.rotation = Quaternion.Euler(0, 0, moveAngle - 90f);

                float distance = Vector3.Distance(currentPos, targetPos);
                float travelTime = Mathf.Max(distance / projectileSpeed, 0.01f);
                float elapsed = 0f;
                Vector3 moveStart = currentPos;

                while (elapsed < travelTime)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / travelTime;
                    if (projectile != null)
                        projectile.transform.position = Vector3.Lerp(moveStart, targetPos, t);
                    yield return null;
                }

                currentPos = targetPos;

                if (target.Data.specialType != SpecialBlockType.None && target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    Debug.Log($"[DrillBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
                    if (!pendingSpecialBlocks.Contains(target))
                        pendingSpecialBlocks.Add(target);
                    StartCoroutine(ScreenShake(shakeIntensity * 0.5f, 0.06f));
                }
                else
                {
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    StartCoroutine(DestroyBlockWithDebris(target, blockColor));
                    StartCoroutine(ScreenShake(shakeIntensity * 0.5f, 0.06f));
                }

                yield return new WaitForSeconds(drillSpeed);
            }

            if (lastMoveDir == Vector3.zero)
                lastMoveDir = initDir;

            List<UnityEngine.UI.Image> imgs = new List<UnityEngine.UI.Image>();
            List<float> initAlphas = new List<float>();
            if (projectile != null)
            {
                foreach (var img in projectile.GetComponentsInChildren<UnityEngine.UI.Image>())
                {
                    imgs.Add(img);
                    initAlphas.Add(img.color.a);
                }
            }

            float exitDuration = 0.3f;
            float exitElapsed = 0f;
            Vector3 exitStart = currentPos;

            while (exitElapsed < exitDuration && projectile != null)
            {
                exitElapsed += Time.deltaTime;
                float t = exitElapsed / exitDuration;

                projectile.transform.position = exitStart + lastMoveDir * projectileSpeed * exitElapsed;

                float fade = 1f - t;
                projectile.transform.localScale = Vector3.one * (1f - t * 0.5f);

                for (int i = 0; i < imgs.Count; i++)
                {
                    if (imgs[i] != null)
                    {
                        Color c = imgs[i].color;
                        c.a = initAlphas[i] * fade;
                        imgs[i].color = c;
                    }
                }

                yield return null;
            }

            if (projectile != null) Destroy(projectile);
        }

        // ============================================================
        // 투사체 생성 - Canvas 내부에 UI.Image로 생성
        // ============================================================

private GameObject CreateProjectile(Vector3 worldPos, DrillDirection direction, bool positive, Color color)
        {
            float angle = GetDirectionAngle(direction, positive);
            return CreateProjectileWithAngle(worldPos, angle, color);
        }

        /// <summary>
        /// <summary>
        /// 발사체 생성 - worldAngle은 실제 이동 방향의 각도 (Atan2 결과, 도 단위)
        /// Y+ 방향이 전진이므로 -90도 보정
        /// </summary>
        private GameObject CreateProjectileWithAngle(Vector3 worldPos, float worldAngleDeg, Color color)
        {
            GameObject obj = new GameObject("DrillProjectile");
            obj.transform.SetParent(effectParent != null ? effectParent : hexGrid.transform, false);
            obj.transform.position = worldPos;
            obj.transform.rotation = Quaternion.Euler(0, 0, worldAngleDeg - 90f);

            Color bright = new Color(
                Mathf.Min(1f, color.r + 0.35f),
                Mathf.Min(1f, color.g + 0.35f),
                Mathf.Min(1f, color.b + 0.35f), 1f);
            Color dark = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);

            Sprite drillSprite = GenerateDrillSprite(color, bright, dark);
            Sprite glowSprite = GenerateCircleSprite(32, new Color(bright.r, bright.g, bright.b, 0.3f));
            Sprite trailSprite = GenerateTrailSprite(color, bright);

            // Glow
            GameObject glow = new GameObject("Glow");
            glow.transform.SetParent(obj.transform, false);
            glow.transform.localPosition = Vector3.zero;
            var glowImg = glow.AddComponent<UnityEngine.UI.Image>();
            glowImg.raycastTarget = false;
            glowImg.sprite = glowSprite;
            glowImg.color = Color.white;
            glow.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
            StartCoroutine(PulseGlow(glow.GetComponent<RectTransform>(), glowImg));

            // Drill body
            GameObject body = new GameObject("DrillBody");
            body.transform.SetParent(obj.transform, false);
            body.transform.localPosition = Vector3.zero;
            var bodyImg = body.AddComponent<UnityEngine.UI.Image>();
            bodyImg.raycastTarget = false;
            bodyImg.sprite = drillSprite;
            bodyImg.color = Color.white;
            bodyImg.preserveAspect = true;
            body.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 34f);

            // Trail
            GameObject trail = new GameObject("Trail");
            trail.transform.SetParent(obj.transform, false);
            trail.transform.localPosition = new Vector3(0, -18f, 0);
            var trailImg = trail.AddComponent<UnityEngine.UI.Image>();
            trailImg.raycastTarget = false;
            trailImg.sprite = trailSprite;
            trailImg.color = Color.white;
            trailImg.preserveAspect = false;
            RectTransform trailRt = trail.GetComponent<RectTransform>();
            trailRt.sizeDelta = new Vector2(10f, 22f);
            trailRt.pivot = new Vector2(0.5f, 1f);
            StartCoroutine(AnimateTrail(trailRt, trailImg, bright));

            glow.transform.SetAsFirstSibling();
            return obj;
        }

        private Sprite GenerateDrillSprite(Color baseColor, Color bright, Color dark)
        {
            int w = 32, h = 48;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;

            float cx = w / 2f;
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                float halfW;
                if (t < 0.6f)
                    halfW = Mathf.Lerp(6.5f, 3.5f, t / 0.6f);
                else
                    halfW = Mathf.Lerp(3.5f, 0f, Mathf.Pow((t - 0.6f) / 0.4f, 1.5f));

                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx >= halfW) continue;
                    float edge = dx / Mathf.Max(halfW, 0.01f);
                    Color c;
                    if (t > 0.7f) c = Color.Lerp(bright, baseColor, edge);
                    else if (t < 0.12f) c = Color.Lerp(dark, baseColor, edge * 0.5f);
                    else
                    {
                        float hl = Mathf.Pow(1f - edge, 3f);
                        c = Color.Lerp(baseColor, bright, hl * 0.5f);
                        c = Color.Lerp(c, dark, edge * 0.25f);
                    }
                    c.a = Mathf.Clamp01((halfW - dx) * 2.5f);
                    px[y * w + x] = c;
                }
            }
            // Fins
            for (int y = 0; y < 10; y++)
            {
                float ft = (float)y / 9f;
                float finW = Mathf.Lerp(9f, 5f, ft);
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx >= 4f && dx < finW)
                    {
                        float ef = (dx - 4f) / Mathf.Max(finW - 4f, 0.01f);
                        Color fc = dark; fc.a = (1f - ef) * (1f - ft * 0.6f);
                        Color ex = px[y * w + x];
                        px[y * w + x] = Color.Lerp(ex, fc, fc.a);
                    }
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite GenerateCircleSprite(int sz, Color col)
        {
            Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[sz * sz];
            float c = sz / 2f, r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / r;
                    px[y * sz + x] = d < 1f
                        ? new Color(col.r, col.g, col.b, col.a * (1f - d * d))
                        : Color.clear;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite GenerateTrailSprite(Color baseColor, Color bright)
        {
            int w = 16, h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] px = new Color[w * h];
            float cx = w / 2f;
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                float halfW = Mathf.Lerp(0.5f, w / 2f - 1f, t);
                float alpha = Mathf.Lerp(0f, 0.65f, t);
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - cx);
                    if (dx >= halfW) { px[y * w + x] = Color.clear; continue; }
                    float ef = dx / Mathf.Max(halfW, 0.01f);
                    Color c = Color.Lerp(bright, baseColor, ef);
                    c.a = alpha * (1f - ef * ef);
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f);
        }


        private IEnumerator PulseGlow(RectTransform rt, UnityEngine.UI.Image img)
        {
            float time = 0f;
            Color baseColor = img.color;
            while (rt != null && img != null)
            {
                time += Time.deltaTime * 8f;
                float pulse = 1f + 0.3f * Mathf.Sin(time);
                rt.sizeDelta = new Vector2(56f * pulse, 56f * pulse);
                baseColor.a = 0.25f + 0.2f * Mathf.Sin(time);
                img.color = baseColor;
                yield return null;
            }
        }

private IEnumerator AnimateTrail(RectTransform rt, UnityEngine.UI.Image img, Color baseColor)
        {
            float time = 0f;
            while (rt != null && img != null)
            {
                time += Time.deltaTime * 10f;
                // 트레일 길이 변화 (널랥이는 느낌)
                float len = 35f + 8f * Mathf.Sin(time);
                rt.sizeDelta = new Vector2(5f, len);
                // 투명도 변화
                float alpha = 0.25f + 0.1f * Mathf.Sin(time * 1.3f);
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
        }


        // ============================================================
        // 블록 파괴 + 파편 이펙트
        // ============================================================

        private IEnumerator DestroyBlockWithDebris(HexBlock block, Color blockColor)
        {
            if (block == null) yield break;

            Vector3 pos = block.transform.position;

            // 충격파
            StartCoroutine(ImpactWave(pos, blockColor));

            // 파편 생성
            int count = debrisCount + Random.Range(0, 3);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateDebris(pos, blockColor));

            // 블록 찌그러짐 + 사라짐
            float duration = 0.12f;
            float elapsed = 0f;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float sx = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
                float sy = 1f - t;
                block.transform.localScale = new Vector3(origScale.x * sx, origScale.y * sy, 1f);
                yield return null;
            }

            block.transform.localScale = Vector3.one;
            block.ClearData();
        }

        private IEnumerator AnimateDebris(Vector3 center, Color color)
        {
            GameObject debris = new GameObject("Debris");
            debris.transform.SetParent(effectParent != null ? effectParent : hexGrid.transform, false);
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
            float w = Random.Range(4f, 12f);
            float h = Random.Range(4f, 10f);
            rt.sizeDelta = new Vector2(w, h);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(150f, 400f);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float gravityY = -800f;
            float rotSpeed = Random.Range(-720f, 720f);
            float lifetime = Random.Range(0.25f, 0.5f);
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

        private IEnumerator ImpactWave(Vector3 pos, Color color)
        {
            GameObject wave = new GameObject("ImpactWave");
            wave.transform.SetParent(effectParent != null ? effectParent : hexGrid.transform, false);
            wave.transform.position = pos;

            var image = wave.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, 0.6f);

            RectTransform rt = wave.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(10f, 10f);

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = 1f + t * 5f;
                rt.sizeDelta = new Vector2(10f * scale, 10f * scale);
                image.color = new Color(color.r, color.g, color.b, 0.6f * (1f - t));
                yield return null;
            }

            Destroy(wave);
        }

        // ============================================================
        // 드릴 발사 시작 이펙트
        // ============================================================

        private IEnumerator DrillLaunchEffect(Vector3 pos, DrillDirection direction, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 중앙 플래시
            GameObject flash = new GameObject("LaunchFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 0.8f, 1f);

            RectTransform rt = flash.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40f, 40f);

            // 방향 빛줄기
            StartCoroutine(LaunchBeam(pos, direction, true, color));
            StartCoroutine(LaunchBeam(pos, direction, false, color));

            // 스파크 버스트
            for (int i = 0; i < 8; i++)
                StartCoroutine(LaunchSpark(pos, color));

            // 화면 흔들림
            StartCoroutine(ScreenShake(shakeIntensity, 0.1f));

            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scale = 1f + t * 3f;
                rt.sizeDelta = new Vector2(40f * scale, 40f * scale);
                img.color = new Color(1f, 1f, 0.8f, (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        private IEnumerator LaunchBeam(Vector3 pos, DrillDirection direction, bool positive, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject beam = new GameObject("LaunchBeam");
            beam.transform.SetParent(parent, false);
            beam.transform.position = pos;

            var img = beam.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0.7f);

            RectTransform rt = beam.GetComponent<RectTransform>();
            float angle = GetDirectionAngle(direction, positive);
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(8f, 0f);

            float duration = 0.15f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rt.sizeDelta = new Vector2(8f * (1f - t * 0.5f), 120f * t);
                img.color = new Color(color.r, color.g, color.b, 0.7f * (1f - t));
                yield return null;
            }

            Destroy(beam);
        }

        private IEnumerator LaunchSpark(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            GameObject spark = new GameObject("Spark");
            spark.transform.SetParent(parent, false);
            spark.transform.position = center;

            var img = spark.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            img.color = new Color(
                Mathf.Min(1f, color.r + 0.3f),
                Mathf.Min(1f, color.g + 0.3f),
                Mathf.Min(1f, color.b + 0.3f),
                1f
            );

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(3f, 7f);
            rt.sizeDelta = new Vector2(size, size);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(100f, 300f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(0.1f, 0.2f);
            float elapsed = 0f;
            Color c = img.color;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= 0.93f;
                c.a = 1f - t;
                img.color = c;
                yield return null;
            }

            Destroy(spark);
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

        // ============================================================
        // 유틸리티
        // ============================================================

        private Color GetDrillColor(HexBlock block)
        {
            if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                return GemColors.GetColor(block.Data.gemType);
            return Color.white;
        }

        /// <summary>
        /// 각 DrillDirection의 화면상 각도
        /// Vertical = 상하(90도), Slash = /(약 60도), BackSlash = 수평~약간대각(약 0도)
        /// 실제 hex 좌표계의 ToWorldPosition 기반으로 계산
        /// </summary>
        private float GetDirectionAngle(DrillDirection direction, bool positive)
        {
            // hex 좌표계에서 실제 world position delta로 각도 계산
            HexCoord delta = GetDirectionDelta(direction, true);
            Vector2 worldDelta = delta.ToWorldPosition(1f); // hexSize=1로 방향만 계산
            float angle = Mathf.Atan2(worldDelta.y, worldDelta.x) * Mathf.Rad2Deg;
            return positive ? angle : angle + 180f;
        }

        private Vector3 GetWorldDirection(DrillDirection direction, bool positive)
        {
            float angle = GetDirectionAngle(direction, positive) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
        }

        // ============================================================
        // 패턴 감지 (레거시 호환)
        // ============================================================

        public DrillDirection? DetectDrillPattern(List<HexBlock> matchedBlocks)
        {
            if (matchedBlocks == null || matchedBlocks.Count < 4)
                return null;
            return null;
        }
    }
}

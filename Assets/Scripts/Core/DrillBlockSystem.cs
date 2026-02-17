using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

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

        public event System.Action<int> OnDrillComplete;

        private int activeDrillCount = 0;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();

        public bool IsDrilling => activeDrillCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);
        public void ForceReset()
        {
            activeDrillCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.LogWarning("[DrillBlockSystem] ForceReset called");
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
            if (drillBlock == null) return;
            if (drillBlock.Data == null || drillBlock.Data.specialType != SpecialBlockType.Drill)
                return;
            Debug.Log($"[DrillBlockSystem] Activating drill at {drillBlock.Coord}, direction={drillBlock.Data.drillDirection}");
            StartCoroutine(DrillCoroutine(drillBlock));
        }

private IEnumerator DrillCoroutine(HexBlock drillBlock)
        {
            activeDrillCount++;
            activeBlocks.Add(drillBlock);

            DrillDirection direction = drillBlock.Data.drillDirection;
            HexCoord startCoord = drillBlock.Coord;
            Vector3 drillWorldPos = drillBlock.transform.position;
            Color drillColor = GetDrillColor(drillBlock);

            Debug.Log($"[DrillBlockSystem] === DRILL ACTIVATED === Coord={startCoord}, Direction={direction}, WorldPos={drillWorldPos}");

            // Pre-Fire 압축 애니메이션
            yield return StartCoroutine(PreFireCompression(drillBlock));

            // Hit Stop (Small tier)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationSmall));

            // Zoom Punch (Small tier)
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // 발사 순간 블록 클리어
            drillBlock.ClearData();

            // 양방향 타겟 수집
            List<HexBlock> targets1 = GetBlocksInDirection(startCoord, direction, true);
            List<HexBlock> targets2 = GetBlocksInDirection(startCoord, direction, false);

            Debug.Log($"[DrillBlockSystem] Targets: positive={targets1.Count}, negative={targets2.Count}");
            foreach (var t in targets1)
                Debug.Log($"[DrillBlockSystem]   +target: {t.Coord} pos={t.transform.position}");
            foreach (var t in targets2)
                Debug.Log($"[DrillBlockSystem]   -target: {t.Coord} pos={t.transform.position}");

            // 파괴 대상 블록의 티어별 기본 점수 미리 계산 (ClearData 전)
            int blockScoreSum = 0;
            int basicBlockCount = 0;  // 기본 블록(GemType 1-5) 카운트
            var sm = GameManager.Instance?.GetComponent<ScoreManager>();
            List<HexBlock> allDrillTargets = new List<HexBlock>(targets1);
            allDrillTargets.AddRange(targets2);
            foreach (var t in allDrillTargets)
            {
                if (t == null || t.Data == null || t.Data.gemType == GemType.None) continue;
                if (t.Data.specialType != SpecialBlockType.None && t.Data.specialType != SpecialBlockType.FixedBlock) continue;
                blockScoreSum += ScoreCalculator.GetBlockBaseScore(t.Data.tier);

                // 기본 블록 카운트 (GemType 1-5)
                if ((int)t.Data.gemType >= 1 && (int)t.Data.gemType <= 5)
                    basicBlockCount++;

                // 적군 점수 (드릴 = SpecialBasic)
                if (sm != null)
                {
                    if (t.Data.hasThorn)
                        sm.AddEnemyScore(EnemyType.ThornParasite, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                    if (t.Data.hasChain)
                        sm.AddEnemyScore(EnemyType.ChainAnchor, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                    if (t.Data.gemType == GemType.Gray)
                        sm.AddEnemyScore(EnemyType.Chromophage, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                    if (t.Data.enemyType != EnemyType.None && t.Data.enemyType != EnemyType.Chromophage
                        && t.Data.enemyType != EnemyType.ChainAnchor && t.Data.enemyType != EnemyType.ThornParasite)
                        sm.AddEnemyScore(t.Data.enemyType, RemovalMethod.SpecialBasic,
                            RemovalCondition.Normal, t.transform.position);
                }
            }

            // 드릴 발사 시작 이펙트 (중앙 폭발)
            StartCoroutine(DrillLaunchEffect(drillWorldPos, direction, drillColor));

            // 양방향 동시 발사
            Coroutine drill1 = StartCoroutine(DrillLineWithProjectile(
                drillWorldPos, targets1, direction, true, drillColor));
            Coroutine drill2 = StartCoroutine(DrillLineWithProjectile(
                drillWorldPos, targets2, direction, false, drillColor));

            yield return drill1;
            yield return drill2;

            // 드릴이 파괴한 기본 블록 미션 카운팅
            if (basicBlockCount > 0 && GameManager.Instance != null)
            {
                Debug.Log($"[DrillBlockSystem] 📊 드릴 미션: 기본블록 {basicBlockCount}개 제거");
                GameManager.Instance.OnSpecialBlockDestroyedBasicBlocks(basicBlockCount, "Drill");
            }

            int totalScore = 100 + blockScoreSum;
            Debug.Log($"[DrillBlockSystem] === DRILL COMPLETE === Score={totalScore} (base:100 + blockTierSum:{blockScoreSum})");
            OnDrillComplete?.Invoke(totalScore);
            activeBlocks.Remove(drillBlock);
            activeDrillCount--;
        }

        // ============================================================
        // 방향별 블록 수집
        // ============================================================

private List<HexBlock> GetBlocksInDirection(HexCoord start, DrillDirection direction, bool positive)
        {
            List<HexBlock> blocks = new List<HexBlock>();
            if (hexGrid == null) return blocks;

            // 모든 방향을 hex 단일 축으로 직선 이동 (지그재그 제거)
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

        /// <summary>
        /// DrillDirection에 따른 hex 좌표 이동 delta
        /// Vertical: r축 (화면상 상하)
        /// Slash: q+,r- 대각선 (화면상 / 방향)
        /// BackSlash: q축 (화면상 수평... 이름과 다르지만 hex구조상 이렇게 됨)
        /// </summary>
private HexCoord GetDirectionDelta(DrillDirection direction, bool positive)
        {
            // Flat-top hex 배치 기준 (CalculateFlatTopHexPosition)
            // x = 1.5*q, y = -√3*(r + q/2)
            // (0,±1) = 순수 세로, (±1,0) = \\ 방향, (±1,∓1) = / 방향
            int sign = positive ? 1 : -1;
            switch (direction)
            {
                // Vertical = r축: 화면상 순수 세로 ↕
                case DrillDirection.Vertical:  return new HexCoord(0, sign);
                // Slash = s축: 화면상 / 방향 (세로에서 시계 60°)
                case DrillDirection.Slash:     return new HexCoord(sign, -sign);
                // BackSlash = q축: 화면상 \\ 방향 (세로에서 시계 120°)
                case DrillDirection.BackSlash: return new HexCoord(sign, 0);
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
            List<Coroutine> destroyCoroutines = new List<Coroutine>();

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

                // Bug #15 fix: target.Data가 동시 실행 중 null이 될 수 있음
                if (target.Data == null || target.Data.gemType == GemType.None) continue;

                // ReflectionShield: 방패 흡수 (드릴 = 특수블록)
                if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(target))
                {
                    StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));
                    continue; // 방패가 흡수 → 블록 보존
                }

                if (target.Data.specialType != SpecialBlockType.None && target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    Debug.Log($"[DrillBlockSystem] Special block at {target.Coord} type={target.Data.specialType} -> queued");
                    if (!pendingSpecialBlocks.Contains(target))
                    {
                        pendingSpecialBlocks.Add(target);
                        // 영향을 준 즉시 빨간색 테두리로 변경
                        target.SetPendingActivation();
                        target.StartWarningBlink(10f); // 낙하 중 점멸 (발동 시 StopWarningBlink로 종료)
                    }
                    StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));
                }
                else
                {
                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    float drillAngle = GetDirectionAngle(direction, positive);
                    destroyCoroutines.Add(StartCoroutine(DestroyBlockWithDebris(target, blockColor, drillAngle)));
                    StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));
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

            // 모든 파괴 애니메이션이 완료될 때까지 대기 (ClearData 보장)
            foreach (var co in destroyCoroutines)
                yield return co;

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

            Color bright = VisualConstants.DrillBrighten(color);
            bright.a = 1f;
            Color dark = VisualConstants.Darken(color);
            dark.a = 1f;

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

            // White coreline (bright center line for polish)
            GameObject coreline = new GameObject("Coreline");
            coreline.transform.SetParent(obj.transform, false);
            coreline.transform.localPosition = Vector3.zero;
            var coreImg = coreline.AddComponent<UnityEngine.UI.Image>();
            coreImg.raycastTarget = false;
            coreImg.color = new Color(1f, 1f, 1f, 0.7f);
            RectTransform coreRt = coreline.GetComponent<RectTransform>();
            coreRt.sizeDelta = new Vector2(2f, 28f);

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

        private IEnumerator DestroyBlockWithDebris(HexBlock block, Color blockColor, float drillAngle = -1f)
        {
            if (block == null) yield break;

            Vector3 pos = block.transform.position;

            // 화이트 플래시 오버레이
            StartCoroutine(DestroyFlashOverlay(block));

            // 충격파
            StartCoroutine(ImpactWave(pos, blockColor));

            // 파편 생성 (directional bias along drill perpendicular)
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int count = Mathf.RoundToInt((VisualConstants.DebrisBaseCount + Random.Range(0, 3)) * cascadeMult);
            for (int i = 0; i < count; i++)
                StartCoroutine(AnimateDebris(pos, blockColor, drillAngle));

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

        private IEnumerator AnimateDebris(Vector3 center, Color color, float directionAngle = -1f)
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
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            float w = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax);
            float h = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax * 0.9f);
            rt.sizeDelta = new Vector2(w, h);

            // Directional bias: debris flies perpendicular to drill axis
            float angle;
            if (directionAngle >= 0f)
            {
                float perpAngle = (directionAngle + 90f) * Mathf.Deg2Rad;
                float spread = Random.Range(-60f, 60f) * Mathf.Deg2Rad;
                angle = perpAngle + spread;
                if (Random.value > 0.5f) angle += Mathf.PI;
            }
            else
            {
                angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            }
            float speed = Random.Range(VisualConstants.DebrisBaseSpeedMin, VisualConstants.DebrisBaseSpeedMax) * cascadeMult;
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

        private IEnumerator ImpactWave(Vector3 pos, Color color)
        {
            GameObject wave = new GameObject("ImpactWave");
            wave.transform.SetParent(effectParent != null ? effectParent : hexGrid.transform, false);
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

        // ============================================================
        // 드릴 발사 시작 이펙트
        // ============================================================

        private IEnumerator DrillLaunchEffect(Vector3 pos, DrillDirection direction, Color color)
        {
            Transform parent = effectParent != null ? effectParent : hexGrid.transform;

            // 블룸 레이어 (메인 플래시 뒤에)
            StartCoroutine(BloomLayer(pos, VisualConstants.FlashColorDrill, VisualConstants.FlashInitialSize, VisualConstants.FlashDuration));

            // 중앙 플래시
            GameObject flash = new GameObject("LaunchFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var img = flash.AddComponent<UnityEngine.UI.Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(VisualConstants.FlashColorDrill.r, VisualConstants.FlashColorDrill.g, VisualConstants.FlashColorDrill.b, 1f);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float initSize = VisualConstants.FlashInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            // 방향 빛줄기
            StartCoroutine(LaunchBeam(pos, direction, true, color));
            StartCoroutine(LaunchBeam(pos, direction, false, color));

            // 스파크 버스트
            float cascadeMult = removalSystem != null ? VisualConstants.GetCascadeMultiplier(removalSystem.CurrentCascadeDepth) : 1f;
            int sparkCount = Mathf.RoundToInt(VisualConstants.SparkMediumCount * cascadeMult);
            for (int i = 0; i < sparkCount; i++)
                StartCoroutine(LaunchSpark(pos, color));

            // 화면 흔들림
            StartCoroutine(ScreenShake(VisualConstants.ShakeMediumIntensity, VisualConstants.ShakeMediumDuration));

            float duration = VisualConstants.FlashDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.FlashExpand - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                img.color = new Color(VisualConstants.FlashColorDrill.r, VisualConstants.FlashColorDrill.g, VisualConstants.FlashColorDrill.b, 1f - t);
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
            img.color = VisualConstants.DrillBrighten(color);

            RectTransform rt = spark.GetComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkMediumSizeMin, VisualConstants.SparkMediumSizeMax);
            rt.sizeDelta = new Vector2(size, size);

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(VisualConstants.SparkMediumSpeedMin, VisualConstants.SparkMediumSpeedMax);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float lifetime = Random.Range(VisualConstants.SparkMediumLifetimeMin, VisualConstants.SparkMediumLifetimeMax);
            float elapsed = 0f;
            Color c = img.color;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / lifetime;
                spark.transform.position += new Vector3(vel.x, vel.y, 0) * Time.deltaTime;
                vel *= VisualConstants.SparkDeceleration;
                c.a = 1f - t;
                img.color = c;
                float s = size * (1f - t * 0.5f);
                rt.sizeDelta = new Vector2(s, s);
                yield return null;
            }

            Destroy(spark);
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
                // EaseInQuad decay: smooth fade-out
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
            // Flat-top hex 배치 기준 화면 각도
            // CalculateFlatTopHexPosition: x=1.5*q, y=-√3*(r+q/2)
            // (0,1) → (0, -√3) → 아래쪽 = -90° (positive = 아래쪽)
            // (1,-1) → (1.5, √3/2) → 오른쪽 위 = 30°
            // (1,0) → (1.5, -√3/2) → 오른쪽 아래 = -30°
            float angle;
            switch (direction)
            {
                case DrillDirection.Vertical:  angle = -90f; break; // r축: 순수 세로 (아래)
                case DrillDirection.Slash:     angle = 30f;  break; // s축: / 방향 (오른쪽 위)
                case DrillDirection.BackSlash: angle = -30f; break; // q축: \\ 방향 (오른쪽 아래)
                default: angle = -90f; break;
            }
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

        // ============================================================
        // Phase 1 VFX: 공통 유틸리티 메서드
        // ============================================================

        /// <summary>
        /// Pre-Fire 압축 애니메이션: 1 → 0.85 → 1.12 → 폭발
        /// </summary>
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

        /// <summary>
        /// Hit Stop: 타임스케일 0 → 슬로모 복구
        /// </summary>
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

        /// <summary>
        /// Zoom Punch: 보드 줌인 → 줌아웃
        /// </summary>
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

        /// <summary>
        /// 파괴 순간 백색 플래시 오버레이
        /// </summary>
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

        /// <summary>
        /// 블룸 시뮬레이션: 센터 플래시 뒤의 발광 레이어
        /// </summary>
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

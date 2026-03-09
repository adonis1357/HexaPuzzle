// ============================================================================
// GoblinSystem.cs - 고블린 적군 시스템
// ============================================================================
// 고블린은 그리드 상단 빈 공간 3줄에서 포털을 통해 소환되며,
// 매 턴 블록이 있는 방향을 우선하여 이동한다.
// 이동 전 블록을 공격하여 파괴한 뒤 해당 위치로 이동.
// 블록 낙하 시 고블린에게 충돌 데미지를 주어 제거하는 메카닉.
//
// EnemySystem과 별개의 독립 시스템 (고블린은 블록 오버레이가 아닌 독립 엔티티)
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 고블린 스테이지 설정 데이터
    /// </summary>
    [System.Serializable]
    public class GoblinStageConfig
    {
        public int minSpawnPerTurn;    // 턴당 최소 소환 수
        public int maxSpawnPerTurn;    // 턴당 최대 소환 수
        public int missionKillCount;   // 미션 목표 제거 수
        public int maxOnBoard = 8;     // 보드 최대 동시 존재 수
    }

    /// <summary>
    /// 고블린 개체 데이터
    /// </summary>
    public class GoblinData
    {
        public HexCoord position;       // 현재 위치 (그리드 외부 좌표 포함)
        public int hp = 20;             // 현재 체력
        public int maxHp = 20;          // 최대 체력
        public GameObject visualObject; // 비주얼 GameObject
        public Image goblinImage;       // 고블린 스프라이트
        public Image hpBarBg;           // HP바 배경
        public Image hpBarFill;         // HP바 채움
        public Text hpText;            // HP 숫자 텍스트
        public bool isAlive = true;     // 생존 여부

        // 데미지 팝업 누적 표시용
        public float lastDamageTime = -1f;           // 마지막 데미지 시간
        public int accumulatedDamage = 0;            // 누적 데미지 (짧은 시간 내)
        public GameObject activeDamagePopup;         // 현재 활성 데미지 팝업
        public Text activeDamageText;                // 팝업 텍스트 참조
    }

    /// <summary>
    /// 고블린 적군 시스템 - 독립 엔티티 기반
    /// 그리드 상단에서 소환, 아래로 이동, 블록 공격, 낙하 충돌로 제거
    /// </summary>
    public class GoblinSystem : MonoBehaviour
    {
        // ============================================================
        // 싱글톤
        // ============================================================
        public static GoblinSystem Instance { get; private set; }

        // ============================================================
        // 참조
        // ============================================================
        private HexGrid hexGrid;
        private GoblinStageConfig currentConfig;
        private List<GoblinData> goblins = new List<GoblinData>();
        private int totalKills = 0;
        private int totalSpawned = 0; // 총 소환된 고블린 수 (미션 목표까지만 소환)

        // 프로시저럴 스프라이트 캐시
        private static Sprite goblinSprite;
        private static Sprite portalSprite;

        // 아래쪽 3방향 (flat-top hex: 화면 아래로 이동)
        private static readonly HexCoord[] DownDirections = new HexCoord[]
        {
            new HexCoord(1, 0),    // 오른쪽 아래
            new HexCoord(0, 1),    // 바로 아래
            new HexCoord(-1, 1)    // 왼쪽 아래
        };

        // 화면 흔들림 중첩 관리 (다른 시스템과 동일한 패턴)
        private int shakeCount = 0;
        private Vector3 shakeOriginalPos;

        // ============================================================
        // 이벤트
        // ============================================================
        /// <summary>고블린 제거 시 호출 (미션 시스템 연동)</summary>
        public event System.Action<int> OnGoblinKilled;

        /// <summary>현재 보드 위 고블린 수</summary>
        public int AliveCount => goblins.Count(g => g.isAlive);

        /// <summary>총 제거 수</summary>
        public int TotalKills => totalKills;

        // ============================================================
        // 초기화
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 스테이지 시작 시 초기화
        /// </summary>
        public void Initialize(HexGrid grid, GoblinStageConfig config)
        {
            hexGrid = grid;
            currentConfig = config;
            totalKills = 0;
            totalSpawned = 0;

            // 기존 고블린 정리
            CleanupAll();

            Debug.Log($"[GoblinSystem] 초기화: 소환 {config.minSpawnPerTurn}~{config.maxSpawnPerTurn}/턴, " +
                      $"목표 {config.missionKillCount}킬, 최대 {config.maxOnBoard}마리");
        }

        /// <summary>
        /// 모든 고블린 정리
        /// </summary>
        public void CleanupAll()
        {
            foreach (var goblin in goblins)
            {
                if (goblin.visualObject != null)
                    Destroy(goblin.visualObject);
            }
            goblins.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ============================================================
        // 턴 처리 메인 루프
        // ============================================================

        /// <summary>
        /// 매 턴 호출: 이동(공격 포함) → 소환
        /// GameManager에서 캐스케이드 완료 후 호출
        /// 고블린은 블록이 있는 방향을 우선 선택 → 공격 후 파괴 → 이동
        /// </summary>
        public IEnumerator ProcessTurn()
        {
            if (currentConfig == null || hexGrid == null) yield break;

            // 1단계: 기존 고블린 이동 (블록 공격+파괴 통합)
            yield return StartCoroutine(MoveAllGoblins());

            // 2단계: 새 고블린 소환
            yield return StartCoroutine(SpawnGoblins());
        }

        // ============================================================
        // 1단계: 고블린 이동
        // ============================================================

        private IEnumerator MoveAllGoblins()
        {
            var aliveGoblins = goblins.Where(g => g.isAlive).ToList();
            if (aliveGoblins.Count == 0) yield break;

            // ──────────────────────────────────────────────
            // 1단계: 블록이 있는 방향으로 공격 + 파괴 (동시)
            // ──────────────────────────────────────────────
            // 각 고블린의 이동 타겟 결정 (블록이 있는 방향 우선)
            Dictionary<GoblinData, HexCoord> moveTargets = new Dictionary<GoblinData, HexCoord>();
            List<GoblinData> goblinsWithBlockTarget = new List<GoblinData>();
            Dictionary<GoblinData, HexBlock> attackTargetBlocks = new Dictionary<GoblinData, HexBlock>();

            // 이미 선점된 좌표 추적 (고블린끼리 같은 곳 이동 방지)
            HashSet<HexCoord> reservedCoords = new HashSet<HexCoord>();

            foreach (var goblin in aliveGoblins)
            {
                var shuffled = DownDirections.OrderBy(x => Random.value).ToArray();
                HexCoord bestTarget = goblin.position;
                bool found = false;
                HexBlock targetBlock = null;

                // 1차: 블록이 있는 방향 우선 탐색
                foreach (var dir in shuffled)
                {
                    HexCoord target = goblin.position + dir;

                    // 좌우 경계 체크
                    if (Mathf.Abs(target.q) > hexGrid.GridRadius) continue;

                    // r 범위 체크 (그리드 내부만, 상단 빈공간 제외)
                    int rMin = hexGrid.GetTopR(target.q);
                    int rMax = Mathf.Min(hexGrid.GridRadius, -target.q + hexGrid.GridRadius);
                    if (target.r < rMin || target.r > rMax) continue;

                    // 다른 고블린 선점 확인
                    bool occupied = goblins.Any(g => g.isAlive && g != goblin && g.position == target);
                    if (occupied) continue;
                    if (reservedCoords.Contains(target)) continue;

                    // 그리드 내부 좌표 → 블록 확인 (쉘 블록은 공격 대상에서 제외)
                    HexBlock block = hexGrid.GetBlock(target);
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None
                        && !block.Data.isShell)
                    {
                        bestTarget = target;
                        targetBlock = block;
                        found = true;
                        break;
                    }
                }

                // 2차: 블록이 없으면 바로 아래(0,1)로 이동
                if (!found)
                {
                    HexCoord downTarget = goblin.position + new HexCoord(0, 1);

                    bool canMoveDown = true;

                    // 좌우 경계 체크
                    if (Mathf.Abs(downTarget.q) > hexGrid.GridRadius) canMoveDown = false;

                    if (canMoveDown)
                    {
                        int rMin = hexGrid.GetTopR(downTarget.q);
                        int rMax = Mathf.Min(hexGrid.GridRadius, -downTarget.q + hexGrid.GridRadius);
                        if (downTarget.r < rMin - 3 || downTarget.r > rMax) canMoveDown = false;
                    }

                    if (canMoveDown)
                    {
                        bool occupied = goblins.Any(g => g.isAlive && g != goblin && g.position == downTarget);
                        if (occupied) canMoveDown = false;
                        if (reservedCoords.Contains(downTarget)) canMoveDown = false;
                    }

                    if (canMoveDown)
                    {
                        // 빈 공간인지 확인 (블록이 없거나 그리드 밖)
                        if (hexGrid.IsInsideGrid(downTarget))
                        {
                            HexBlock block = hexGrid.GetBlock(downTarget);
                            if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                                canMoveDown = false; // 블록 있는 곳은 스킵
                        }
                    }

                    if (canMoveDown)
                    {
                        bestTarget = downTarget;
                        found = true;
                    }
                }

                if (found)
                {
                    moveTargets[goblin] = bestTarget;
                    reservedCoords.Add(bestTarget);

                    if (targetBlock != null)
                    {
                        goblinsWithBlockTarget.Add(goblin);
                        attackTargetBlocks[goblin] = targetBlock;
                    }
                }
            }

            // ──────────────────────────────────────────────
            // 2단계: 블록 공격 애니메이션 (동시 실행)
            // ──────────────────────────────────────────────
            if (goblinsWithBlockTarget.Count > 0)
            {
                List<Coroutine> attackCoroutines = new List<Coroutine>();
                foreach (var goblin in goblinsWithBlockTarget)
                {
                    HexBlock block = attackTargetBlocks[goblin];
                    HexCoord targetCoord = moveTargets[goblin];
                    attackCoroutines.Add(StartCoroutine(AnimateAttackAndDestroy(goblin, block, targetCoord)));
                }

                // 모든 공격 애니메이션 대기
                foreach (var co in attackCoroutines)
                    yield return co;
            }

            // ──────────────────────────────────────────────
            // 3단계: 모든 고블린 이동 (동시 실행)
            // 고블린은 공격 후 항상 타겟 위치로 이동
            // ──────────────────────────────────────────────
            List<Coroutine> moveCoroutines = new List<Coroutine>();
            foreach (var kvp in moveTargets)
            {
                var goblin = kvp.Key;
                var target = kvp.Value;

                goblin.position = target;
                Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(target);
                moveCoroutines.Add(StartCoroutine(AnimateGoblinMove(goblin, worldPos)));
            }

            foreach (var co in moveCoroutines)
                yield return co;

            // 안전장치: 모든 애니메이션 완료 후 hexGrid 위치 강제 리셋
            // MicroShake/ScreenShake가 완전히 끝나지 않았을 경우를 대비
            if (hexGrid != null)
            {
                hexGrid.transform.localPosition = Vector3.zero;
                shakeCount = 0;
            }
        }

        private IEnumerator AnimateGoblinMove(GoblinData goblin, Vector2 targetPos)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 startPos = rt.anchoredPosition;
            float duration = 0.3f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutCubic(Mathf.Clamp01(elapsed / duration));
                rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            rt.anchoredPosition = targetPos;
        }

        // ============================================================
        // 블록 공격 + 파괴 (이동 전 실행)
        // ============================================================

        /// <summary>
        /// 이동 방향의 블록을 공격 → 파괴 → 이후 이동 가능하도록 처리
        /// 고블린이 블록 방향으로 돌진 → 타격 → 블록 파괴 연출 → 원위치 복귀
        /// </summary>
        private IEnumerator AnimateAttackAndDestroy(GoblinData goblin, HexBlock block, HexCoord targetCoord)
        {
            if (goblin.visualObject == null || block == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 origPos = rt.anchoredPosition;
            Vector2 targetWorldPos = hexGrid.CalculateFlatTopHexPosition(targetCoord);
            // 돌진 위치: 현재 위치에서 타겟 방향으로 40% 지점
            Vector2 lungePos = Vector2.Lerp(origPos, targetWorldPos, 0.4f);

            Vector3 origScale = rt.localScale;
            float elapsed;

            // 1. 몽둥이 올리기 (스케일 확대 + 회전)
            float dur1 = 0.12f;
            elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur1;
                rt.localScale = origScale * (1f + 0.25f * t);
                rt.localEulerAngles = new Vector3(0, 0, -18f * t);
                yield return null;
            }

            // 2. 타겟 방향으로 돌진 + 내려치기
            float dur2 = 0.08f;
            elapsed = 0f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / dur2);
                rt.anchoredPosition = Vector2.Lerp(origPos, lungePos, VisualConstants.EaseOutCubic(t));
                rt.localScale = origScale * (1.25f - 0.25f * t);
                rt.localEulerAngles = new Vector3(0, 0, -18f + 30f * t);
                yield return null;
            }

            // 3. 타격 순간: 금간 → 껍데기(쉘) 처리
            if (block.Data != null && !block.Data.isCracked)
            {
                // 첫 타격: 블록에 금이 감 (완전 파괴 아님)
                var data = block.Data;
                data.isCracked = true;
                block.SetBlockData(data);
                Debug.Log($"[GoblinSystem] 공격→금간: ({goblin.position}) → ({targetCoord}) 블록에 금이 감");
            }
            else if (block.Data != null && block.Data.isCracked && !block.Data.isShell)
            {
                // 두 번째 타격: 가운데 깨지고 테두리만 남은 껍데기(쉘) 상태
                // 매칭 불가 + 낙하 장애물로 인식
                var data = block.Data;
                data.isShell = true;
                data.specialType = SpecialBlockType.None; // 특수 블록 해제
                data.tier = BlockTier.Normal;
                block.SetBlockData(data);
                Debug.Log($"[GoblinSystem] 공격→껍데기: ({goblin.position}) → ({targetCoord}) 테두리만 남은 장애물");
            }
            else
            {
                // 이미 껍데기 블록: 완전 파괴
                block.ClearData();
                Debug.Log($"[GoblinSystem] 공격→완전파괴: ({goblin.position}) → ({targetCoord}) 껍데기 블록 파괴");
            }

            // 타격 이펙트 (블록 위치에 플래시)
            StartCoroutine(BlockDestroyFlash(targetWorldPos));

            // 화면 미세 흔들림 (shakeCount 패턴 적용)
            if (hexGrid != null)
                StartCoroutine(ScreenShake(VisualConstants.ShakeSmallIntensity, VisualConstants.ShakeSmallDuration));

            // 4. 원위치 복귀
            float dur3 = 0.12f;
            elapsed = 0f;
            while (elapsed < dur3)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / dur3));
                rt.anchoredPosition = Vector2.Lerp(lungePos, origPos, t);
                rt.localScale = Vector3.Lerp(origScale * 1.0f, origScale, t);
                rt.localEulerAngles = Vector3.Lerp(new Vector3(0, 0, 12f), Vector3.zero, t);
                yield return null;
            }

            rt.anchoredPosition = origPos;
            rt.localScale = origScale;
            rt.localEulerAngles = Vector3.zero;
        }

        /// <summary>
        /// 블록 파괴 시 해당 위치에 파괴 플래시 이펙트
        /// </summary>
        private IEnumerator BlockDestroyFlash(Vector2 position)
        {
            Transform parent = hexGrid.GridContainer;

            GameObject flash = new GameObject("GoblinDestroyFlash");
            flash.transform.SetParent(parent, false);

            RectTransform frt = flash.AddComponent<RectTransform>();
            frt.anchoredPosition = position;
            float size = hexGrid.HexSize * 1.8f;
            frt.sizeDelta = new Vector2(size, size);

            Image fimg = flash.AddComponent<Image>();
            fimg.sprite = HexBlock.GetHexFlashSprite();
            fimg.color = new Color(1f, 0.6f, 0.2f, 0.9f); // 주황색 플래시
            fimg.raycastTarget = false;

            // 확장 + 페이드아웃
            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + 0.4f * VisualConstants.EaseOutCubic(t);
                frt.localScale = Vector3.one * scale;
                fimg.color = new Color(1f, 0.6f, 0.2f, 0.9f * (1f - t));
                yield return null;
            }

            Destroy(flash);
        }

        /// <summary>
        /// 화면 흔들림 코루틴 (다른 시스템과 동일한 shakeCount 패턴 적용).
        /// Bug fix: 기존 MicroShake는 여러 고블린이 동시에 공격할 때
        /// 각각 orig 위치를 잘못 캡처하여 hexGrid.transform.localPosition이
        /// 영구적으로 어긋나는 문제가 있었음.
        /// </summary>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            // 다수 특수 블록 동시 발동 시 필드 바운스는 하나만 실행
            bool isOwner = VisualConstants.TryBeginScreenShake();
            if (!isOwner) yield break;

            Transform target = hexGrid != null ? hexGrid.transform : transform;
            // 첫 번째 흔들림이면 현재 위치를 "원래 위치"로 저장
            if (shakeCount == 0)
                shakeOriginalPos = target.localPosition;
            shakeCount++;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 시간이 지날수록 흔들림이 약해짐 (EaseInQuad 감쇠)
                float decay = 1f - VisualConstants.EaseInQuad(t);
                float x = Random.Range(-1f, 1f) * intensity * decay;
                float y = Random.Range(-1f, 1f) * intensity * decay;
                target.localPosition = shakeOriginalPos + new Vector3(x, y, 0);
                yield return null;
            }

            // 흔들림 카운터 감소. 마지막 흔들림이 끝나면 원래 위치로 복원
            shakeCount--;
            if (shakeCount <= 0)
            {
                shakeCount = 0;
                target.localPosition = shakeOriginalPos;
            }
            VisualConstants.EndScreenShake();
        }

        // ============================================================
        // 3단계: 고블린 소환
        // ============================================================

        private IEnumerator SpawnGoblins()
        {
            if (currentConfig == null) yield break;

            // 미션 목표 수량까지만 소환 (총 소환 수 = 미션 킬 목표)
            int spawnBudget = currentConfig.missionKillCount - totalSpawned;
            if (spawnBudget <= 0) yield break;

            int aliveCount = AliveCount;
            int remaining = currentConfig.maxOnBoard - aliveCount;
            if (remaining <= 0) yield break;

            int spawnCount = Random.Range(currentConfig.minSpawnPerTurn, currentConfig.maxSpawnPerTurn + 1);
            spawnCount = Mathf.Min(spawnCount, remaining);
            spawnCount = Mathf.Min(spawnCount, spawnBudget); // 미션 목표 초과 방지

            if (spawnCount <= 0) yield break;

            // 소환 가능 위치: 그리드 상단 빈 공간 3줄 중 고블린이 없는 곳
            var spawnableCoords = hexGrid.GetExtendedTopCoords()
                .Where(c => !goblins.Any(g => g.isAlive && g.position == c))
                .OrderBy(x => Random.value)
                .Take(spawnCount)
                .ToList();

            List<Coroutine> spawnCoroutines = new List<Coroutine>();

            foreach (var coord in spawnableCoords)
            {
                GoblinData newGoblin = new GoblinData
                {
                    position = coord,
                    hp = 20,
                    maxHp = 20,
                    isAlive = true
                };

                goblins.Add(newGoblin);
                totalSpawned++;
                spawnCoroutines.Add(StartCoroutine(SpawnSingleGoblin(newGoblin)));
            }

            foreach (var co in spawnCoroutines)
                yield return co;

            Debug.Log($"[GoblinSystem] {spawnableCoords.Count}마리 소환 완료, 보드 총 {AliveCount}마리, 누적 소환 {totalSpawned}/{currentConfig.missionKillCount}");
        }

        private IEnumerator SpawnSingleGoblin(GoblinData goblin)
        {
            Vector2 worldPos = hexGrid.CalculateFlatTopHexPosition(goblin.position);

            // 포털 이펙트 생성
            yield return StartCoroutine(PortalSpawnEffect(worldPos));

            // 고블린 비주얼 생성
            CreateGoblinVisual(goblin, worldPos);

            // 등장 포효 모션
            yield return StartCoroutine(RoarAnimation(goblin));
        }

        // ============================================================
        // 포털 소환 이펙트
        // ============================================================

        private IEnumerator PortalSpawnEffect(Vector2 position)
        {
            Transform parent = hexGrid.GridContainer;

            // 포털 오브젝트 생성
            GameObject portal = new GameObject("GoblinPortal");
            portal.transform.SetParent(parent, false);

            RectTransform prt = portal.AddComponent<RectTransform>();
            prt.anchoredPosition = position;
            float portalSize = hexGrid.HexSize * 2f;
            prt.sizeDelta = new Vector2(portalSize, portalSize * 0.6f); // 타원형
            prt.localScale = Vector3.zero;

            Image pimg = portal.AddComponent<Image>();
            if (portalSprite == null)
                portalSprite = CreatePortalSprite(128);
            pimg.sprite = portalSprite;
            pimg.color = new Color(0.4f, 0.2f, 0.9f, 0.8f);
            pimg.raycastTarget = false;

            // 포털 가장자리 회전 링 (장식용)
            GameObject ring = new GameObject("PortalRing");
            ring.transform.SetParent(portal.transform, false);
            RectTransform rrt = ring.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0.5f);
            rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(portalSize * 1.2f, portalSize * 0.72f);
            Image rimg = ring.AddComponent<Image>();
            rimg.sprite = portalSprite;
            rimg.color = new Color(0.6f, 0.3f, 1f, 0.4f);
            rimg.raycastTarget = false;

            // 1단계: 포털 등장 (0→full 스케일, 0.3초)
            float dur1 = 0.3f;
            float elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / dur1));
                prt.localScale = Vector3.one * t;
                // 링 회전
                rrt.localEulerAngles = new Vector3(0, 0, elapsed * 360f);
                yield return null;
            }
            prt.localScale = Vector3.one;

            // 2단계: 유지 + 링 회전 (0.3초)
            elapsed = 0f;
            float dur2 = 0.3f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                rrt.localEulerAngles = new Vector3(0, 0, (dur1 + elapsed) * 360f);
                // 포털 펄스 효과
                float pulse = 1f + 0.05f * Mathf.Sin(elapsed * Mathf.PI * 6f);
                prt.localScale = Vector3.one * pulse;
                yield return null;
            }

            // 3단계: 포털 축소 → 사라짐 (0.2초)
            float dur3 = 0.2f;
            elapsed = 0f;
            while (elapsed < dur3)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / dur3);
                prt.localScale = Vector3.one * t;
                pimg.color = new Color(0.4f, 0.2f, 0.9f, 0.8f * t);
                rrt.localEulerAngles = new Vector3(0, 0, (dur1 + dur2 + elapsed) * 360f);
                yield return null;
            }

            Destroy(portal);
        }

        // ============================================================
        // 고블린 비주얼 생성
        // ============================================================

        private void CreateGoblinVisual(GoblinData goblin, Vector2 position)
        {
            Transform parent = hexGrid.GridContainer;

            // 메인 오브젝트
            GameObject obj = new GameObject("Goblin");
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            float size = hexGrid.HexSize * 1.6f;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.zero; // 소환 시 0에서 시작

            // 고블린 이미지
            if (goblinSprite == null)
                goblinSprite = CreateGoblinSprite(256);

            Image img = obj.AddComponent<Image>();
            img.sprite = goblinSprite;
            img.color = Color.white;
            img.raycastTarget = false;

            goblin.visualObject = obj;
            goblin.goblinImage = img;

            // HP바 생성
            CreateHPBar(goblin, obj.transform, size);
        }

        private void CreateHPBar(GoblinData goblin, Transform parent, float goblinSize)
        {
            float barWidth = goblinSize * 0.6f;
            float barHeight = 6f;
            float offsetY = -goblinSize * 0.45f;

            // HP바 배경
            GameObject bgObj = new GameObject("HPBarBg");
            bgObj.transform.SetParent(parent, false);

            RectTransform bgRt = bgObj.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0.5f, 0.5f);
            bgRt.anchorMax = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = new Vector2(0, offsetY);
            bgRt.sizeDelta = new Vector2(barWidth, barHeight);

            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            bgImg.raycastTarget = false;

            // HP바 채움
            GameObject fillObj = new GameObject("HPBarFill");
            fillObj.transform.SetParent(bgObj.transform, false);

            RectTransform fillRt = fillObj.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);

            // pivot을 왼쪽으로 설정하여 오른쪽에서 줄어들게
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = new Vector2(1f, 1f);
            fillRt.offsetMax = new Vector2(-1f, -1f);

            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.9f, 0.2f); // 초록색
            fillImg.raycastTarget = false;

            goblin.hpBarBg = bgImg;
            goblin.hpBarFill = fillImg;

            // HP 숫자 텍스트 (HP바 위에 표시)
            GameObject textObj = new GameObject("HPText");
            textObj.transform.SetParent(parent, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0, offsetY - barHeight - 2f);
            textRt.sizeDelta = new Vector2(barWidth * 2f, 36f);

            Text hpTxt = textObj.AddComponent<Text>();
            hpTxt.text = $"{goblin.hp}";
            hpTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpTxt.fontSize = 27;
            hpTxt.alignment = TextAnchor.MiddleCenter;
            hpTxt.color = Color.white;
            hpTxt.raycastTarget = false;

            // 텍스트 외곽선 (가독성) - 1px 두께
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1f, -1f);

            goblin.hpText = hpTxt;
        }

        private void UpdateHPBar(GoblinData goblin)
        {
            if (goblin.hpBarFill == null) return;

            float hpRatio = (float)goblin.hp / goblin.maxHp;
            RectTransform fillRt = goblin.hpBarFill.GetComponent<RectTransform>();

            // 스케일 X로 HP 비율 표현
            fillRt.localScale = new Vector3(hpRatio, 1f, 1f);

            // 색상 변화: 녹색 → 노랑 → 빨강
            if (hpRatio > 0.5f)
            {
                float t = (hpRatio - 0.5f) / 0.5f;
                goblin.hpBarFill.color = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.9f, 0.2f), t);
            }
            else
            {
                float t = hpRatio / 0.5f;
                goblin.hpBarFill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.9f, 0.1f), t);
            }

            // HP 숫자 텍스트 업데이트
            if (goblin.hpText != null)
                goblin.hpText.text = $"{goblin.hp}";
        }

        // ============================================================
        // 포효(등장) 모션
        // ============================================================

        private IEnumerator RoarAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();

            // 1. 등장: 0 → 1 스케일 (0.25초)
            float dur1 = 0.25f;
            float elapsed = 0f;
            while (elapsed < dur1)
            {
                elapsed += Time.deltaTime;
                float t = VisualConstants.EaseOutBack(Mathf.Clamp01(elapsed / dur1));
                rt.localScale = Vector3.one * t;
                yield return null;
            }
            rt.localScale = Vector3.one;

            // 2. 포효: 1.0 → 1.3 → 1.0 (0.3초)
            float dur2 = 0.3f;
            elapsed = 0f;
            while (elapsed < dur2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur2;
                float scale;
                if (t < 0.4f)
                {
                    scale = 1f + 0.3f * VisualConstants.EaseOutCubic(t / 0.4f);
                }
                else
                {
                    scale = 1.3f - 0.3f * VisualConstants.EaseOutBack((t - 0.4f) / 0.6f);
                }
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            rt.localScale = Vector3.one;

            // 3. 충격파 링 이펙트
            StartCoroutine(ShockwaveEffect(goblin));
        }

        private IEnumerator ShockwaveEffect(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            Transform parent = goblin.visualObject.transform.parent;
            Vector2 pos = goblin.visualObject.GetComponent<RectTransform>().anchoredPosition;

            GameObject wave = new GameObject("GoblinShockwave");
            wave.transform.SetParent(parent, false);

            RectTransform wrt = wave.AddComponent<RectTransform>();
            wrt.anchoredPosition = pos;
            wrt.sizeDelta = new Vector2(20f, 20f);

            Image wimg = wave.AddComponent<Image>();
            if (portalSprite == null)
                portalSprite = CreatePortalSprite(128);
            wimg.sprite = portalSprite;
            wimg.color = new Color(0.3f, 0.8f, 0.2f, 0.6f);
            wimg.raycastTarget = false;

            float duration = 0.3f;
            float elapsed = 0f;
            float maxSize = hexGrid.HexSize * 3f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float size = Mathf.Lerp(20f, maxSize, VisualConstants.EaseOutCubic(t));
                wrt.sizeDelta = new Vector2(size, size);
                wimg.color = new Color(0.3f, 0.8f, 0.2f, 0.6f * (1f - t));
                yield return null;
            }

            Destroy(wave);
        }

        // ============================================================
        // 낙하 충돌 데미지
        // ============================================================

        /// <summary>
        /// ProcessFalling 완료 후 호출.
        /// 각 열에서 낙하한 블록 수로 고블린에 데미지 적용.
        /// fallDamageMap: key=HexCoord(고블린이 있는 열), value=낙하 블록 수
        /// </summary>
        public IEnumerator ApplyFallDamage(Dictionary<int, int> columnFallCounts)
        {
            if (columnFallCounts == null || columnFallCounts.Count == 0) yield break;

            List<GoblinData> damaged = new List<GoblinData>();
            List<GoblinData> killed = new List<GoblinData>();
            // 각 고블린이 받은 낙하 데미지량 기록 (팝업 표시용)
            Dictionary<GoblinData, int> damageAmounts = new Dictionary<GoblinData, int>();

            foreach (var goblin in goblins.Where(g => g.isAlive))
            {
                int colKey = goblin.position.q;
                if (columnFallCounts.TryGetValue(colKey, out int fallCount) && fallCount > 0)
                {
                    // 낙하 블록이 몬스터와 충돌 → 블록당 1 대미지
                    int fallDamage = fallCount;
                    goblin.hp -= fallDamage;
                    damaged.Add(goblin);
                    damageAmounts[goblin] = fallDamage;

                    Debug.Log($"[GoblinSystem] 낙하 데미지: ({goblin.position}) HP {goblin.hp + fallDamage} → {goblin.hp} (-{fallDamage}) [열 낙하 {fallCount}개]");

                    // ★ 즉시 HP바 반영
                    UpdateHPBar(goblin);

                    // ★ 데미지 숫자 팝업 표시 ("-1" 1회)
                    if (goblin.visualObject != null)
                        SpawnDamagePopups(goblin, fallDamage);

                    if (goblin.hp <= 0)
                    {
                        goblin.hp = 0;
                        goblin.isAlive = false;
                        killed.Add(goblin);
                    }
                }
            }

            // 데미지 애니메이션
            List<Coroutine> dmgCoroutines = new List<Coroutine>();
            foreach (var goblin in damaged)
            {
                if (!killed.Contains(goblin))
                    dmgCoroutines.Add(StartCoroutine(DamageFlashAnimation(goblin)));
            }

            foreach (var co in dmgCoroutines)
                yield return co;

            // 제거 연출
            List<Coroutine> killCoroutines = new List<Coroutine>();
            foreach (var goblin in killed)
            {
                totalKills++;
                OnGoblinKilled?.Invoke(totalKills);
                killCoroutines.Add(StartCoroutine(DeathAnimation(goblin)));
            }

            foreach (var co in killCoroutines)
                yield return co;

            // 제거된 고블린 리스트 정리
            goblins.RemoveAll(g => !g.isAlive && g.visualObject == null);
        }

        /// <summary>
        /// 간단한 낙하 데미지 적용 (열 기반이 아닌 개별 좌표 기반)
        /// 특정 좌표에 블록이 낙하/정착했을 때 호출
        /// </summary>
        public void ApplyDamageAtPosition(HexCoord position, int damage = 1)
        {
            var goblin = goblins.FirstOrDefault(g => g.isAlive && g.position == position);
            if (goblin == null) return;

            goblin.hp -= damage;

            // ★ 즉시 HP바 반영 (코루틴 대기 없이)
            UpdateHPBar(goblin);

            // ★ 데미지 숫자 팝업 표시 (각 타격마다 개별 "-1")
            if (goblin.visualObject != null)
                SpawnDamagePopups(goblin, damage);

            // 피격 플래시 + 흔들림 애니메이션
            StartCoroutine(DamageFlashAnimation(goblin));

            Debug.Log($"[GoblinSystem] 위치 데미지: ({position}) -{damage} → HP {goblin.hp}/{goblin.maxHp}");

            if (goblin.hp <= 0)
            {
                goblin.hp = 0;
                goblin.isAlive = false;
                totalKills++;
                OnGoblinKilled?.Invoke(totalKills);
                StartCoroutine(DeathAnimation(goblin));
            }
        }

        /// <summary>
        /// 복수 좌표에서 발생한 데미지를 고블린별로 누적하여 일괄 적용.
        /// 몬스터가 서있는 블록 + 주변 블록이 동시에 삭제되면 해당 수량만큼 대미지.
        /// key=좌표, value=해당 좌표에 가할 데미지
        /// </summary>
        public void ApplyBatchDamage(Dictionary<HexCoord, int> damageMap)
        {
            if (damageMap == null || damageMap.Count == 0) return;

            // 고블린별 누적 데미지 계산
            Dictionary<GoblinData, int> goblinDamage = new Dictionary<GoblinData, int>();
            foreach (var kvp in damageMap)
            {
                var goblin = goblins.FirstOrDefault(g => g.isAlive && g.position == kvp.Key);
                if (goblin == null) continue;

                if (goblinDamage.ContainsKey(goblin))
                    goblinDamage[goblin] += kvp.Value;
                else
                    goblinDamage[goblin] = kvp.Value;
            }

            // 누적된 대미지를 한번에 적용
            foreach (var kvp in goblinDamage)
            {
                GoblinData goblin = kvp.Key;
                int totalDamage = kvp.Value;

                goblin.hp -= totalDamage;
                UpdateHPBar(goblin);

                // 각 타격마다 개별 "-1" 팝업 (대미지 수만큼)
                if (goblin.visualObject != null)
                    SpawnDamagePopups(goblin, totalDamage);

                StartCoroutine(DamageFlashAnimation(goblin));

                Debug.Log($"[GoblinSystem] 일괄 데미지: ({goblin.position}) -{totalDamage} → HP {goblin.hp}/{goblin.maxHp}");

                if (goblin.hp <= 0)
                {
                    goblin.hp = 0;
                    goblin.isAlive = false;
                    totalKills++;
                    OnGoblinKilled?.Invoke(totalKills);
                    StartCoroutine(DeathAnimation(goblin));
                }
            }
        }

        /// <summary>
        /// 특정 좌표에 고블린이 있는지 확인
        /// </summary>
        public bool HasGoblinAt(HexCoord coord)
        {
            return goblins.Any(g => g.isAlive && g.position == coord);
        }

        /// <summary>
        /// 살아있는 모든 고블린 목록 반환
        /// </summary>
        public List<GoblinData> GetAliveGoblins()
        {
            return goblins.Where(g => g.isAlive).ToList();
        }

        /// <summary>
        /// 특정 열(q)에 있는 살아있는 고블린 목록
        /// </summary>
        public List<GoblinData> GetGoblinsInColumn(int q)
        {
            return goblins.Where(g => g.isAlive && g.position.q == q).ToList();
        }

        // ============================================================
        // 데미지 / 사망 애니메이션
        // ============================================================

        private IEnumerator DamageFlashAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            // HP바는 ApplyDamageAtPosition에서 즉시 업데이트됨 (여기서는 생략)

            // 빨간색 플래시 (피격 표현) — 원래 색은 항상 White 기준
            if (goblin.goblinImage != null)
            {
                float duration = 0.15f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    if (goblin.goblinImage == null) yield break;
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    // 흰→빨강→흰 (원색은 항상 White로 고정)
                    if (t < 0.5f)
                        goblin.goblinImage.color = Color.Lerp(Color.white, Color.red, t * 2f);
                    else
                        goblin.goblinImage.color = Color.Lerp(Color.red, Color.white, (t - 0.5f) * 2f);
                    yield return null;
                }

                if (goblin.goblinImage != null)
                    goblin.goblinImage.color = Color.white;
            }

            // 흔들림
            if (goblin.visualObject != null)
            {
                RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
                Vector2 orig = rt.anchoredPosition;
                float shakeDur = 0.1f;
                float elapsed2 = 0f;

                while (elapsed2 < shakeDur)
                {
                    if (goblin.visualObject == null) yield break;
                    elapsed2 += Time.deltaTime;
                    float intensity = 4f * (1f - elapsed2 / shakeDur);
                    rt.anchoredPosition = orig + new Vector2(
                        Random.Range(-intensity, intensity),
                        Random.Range(-intensity, intensity));
                    yield return null;
                }
                if (goblin.visualObject != null)
                    rt.anchoredPosition = orig;
            }
        }

        // ============================================================
        // 데미지 팝업 시스템 — 누적 표시 (-1 → -2 → -3)
        // ============================================================

        /// <summary>
        /// 누적 데미지 팝업 윈도우 (초).
        /// 이 시간 내에 반복 데미지가 들어오면 기존 팝업에 누적 표시.
        /// </summary>
        private const float DAMAGE_ACCUMULATE_WINDOW = 0.15f;

        /// <summary>
        /// 데미지 팝업 — 짧은 시간 내 반복 데미지는 누적 표시 (-1 → -2 → -3).
        /// 최초 데미지 시 새 팝업 생성, 이후 데미지는 기존 팝업 숫자 갱신.
        /// </summary>
        private void SpawnDamagePopups(GoblinData goblin, int totalDamage)
        {
            if (goblin.visualObject == null || hexGrid == null) return;

            if (Time.time - goblin.lastDamageTime < DAMAGE_ACCUMULATE_WINDOW
                && goblin.activeDamagePopup != null)
            {
                // 기존 팝업에 누적
                goblin.accumulatedDamage += totalDamage;
                goblin.lastDamageTime = Time.time;
                if (goblin.activeDamageText != null)
                    goblin.activeDamageText.text = $"-{goblin.accumulatedDamage}";
                // 숫자 증가 시 스케일 펄스 강조
                if (goblin.activeDamagePopup != null)
                    StartCoroutine(DamagePopupPulse(goblin.activeDamagePopup.GetComponent<RectTransform>()));
            }
            else
            {
                // 이전 팝업 제거
                if (goblin.activeDamagePopup != null)
                    Destroy(goblin.activeDamagePopup);

                // 새 누적 팝업 시작
                goblin.accumulatedDamage = totalDamage;
                goblin.lastDamageTime = Time.time;
                StartCoroutine(AccumulatingDamagePopup(goblin));
            }
        }

        /// <summary>
        /// 누적 데미지 팝업 코루틴.
        /// 누적 윈도우 동안 대기 → 추가 데미지 없으면 위로 떠오르며 페이드아웃.
        /// </summary>
        private IEnumerator AccumulatingDamagePopup(GoblinData goblin)
        {
            if (goblin.visualObject == null || hexGrid == null) yield break;

            Transform parent = goblin.visualObject.transform.parent;

            // 팝업 오브젝트 생성
            GameObject popup = new GameObject("DamagePopup");
            popup.transform.SetParent(parent, false);

            RectTransform popupRt = popup.AddComponent<RectTransform>();
            RectTransform goblinRt = goblin.visualObject.GetComponent<RectTransform>();
            popupRt.anchoredPosition = goblinRt.anchoredPosition + new Vector2(0, hexGrid.HexSize * 0.5f);
            popupRt.sizeDelta = new Vector2(60f, 30f);

            // 텍스트
            var textObj = new GameObject("DmgText");
            textObj.transform.SetParent(popup.transform, false);

            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = $"-{goblin.accumulatedDamage}";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.3f, 0.2f, 1f);
            text.raycastTarget = false;

            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 고블린에 팝업 참조 저장
            goblin.activeDamagePopup = popup;
            goblin.activeDamageText = text;

            // 누적 대기: 새로운 데미지가 없을 때까지 대기
            while (Time.time - goblin.lastDamageTime < DAMAGE_ACCUMULATE_WINDOW)
            {
                if (popup == null) yield break;
                // 텍스트 실시간 갱신 (SpawnDamagePopups에서도 갱신하지만 안전장치)
                if (text != null)
                    text.text = $"-{goblin.accumulatedDamage}";
                yield return null;
            }

            // 누적 완료 → 위로 떠오르며 페이드아웃
            Vector2 startPos = popupRt.anchoredPosition;
            float duration = 0.6f;
            float elapsed = 0f;
            float riseHeight = 30f;

            while (elapsed < duration)
            {
                if (popup == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 위로 이동 (EaseOut)
                float rise = riseHeight * VisualConstants.EaseOutCubic(t);
                popupRt.anchoredPosition = startPos + new Vector2(0, rise);

                // 절반 이후부터 페이드아웃
                float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;
                text.color = new Color(1f, 0.3f, 0.2f, alpha);
                outline.effectColor = new Color(0, 0, 0, 0.8f * alpha);

                yield return null;
            }

            if (popup != null) Destroy(popup);
            if (goblin.activeDamagePopup == popup)
            {
                goblin.activeDamagePopup = null;
                goblin.activeDamageText = null;
            }
        }

        /// <summary>
        /// 데미지 숫자 증가 시 스케일 펄스 (강조 효과)
        /// </summary>
        private IEnumerator DamagePopupPulse(RectTransform rt)
        {
            if (rt == null) yield break;
            float duration = 0.1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // 1.0 → 1.3 → 1.0 스케일 바운스
                float scale = t < 0.5f
                    ? Mathf.Lerp(1f, 1.3f, t * 2f)
                    : Mathf.Lerp(1.3f, 1f, (t - 0.5f) * 2f);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        private IEnumerator DeathAnimation(GoblinData goblin)
        {
            if (goblin.visualObject == null) yield break;

            RectTransform rt = goblin.visualObject.GetComponent<RectTransform>();
            Vector2 deathPos = rt.anchoredPosition;

            // 1. 흰색 플래시
            if (goblin.goblinImage != null)
                goblin.goblinImage.color = Color.white;
            yield return new WaitForSeconds(0.08f);

            // 2. 파편 산개
            StartCoroutine(SpawnDeathDebris(rt.parent, deathPos));

            // 3. 스케일 축소 → 사라짐
            float duration = 0.25f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 스핀 + 축소
                rt.localScale = Vector3.one * (1f - t);
                rt.localEulerAngles = new Vector3(0, 0, t * 360f);

                if (goblin.goblinImage != null)
                    goblin.goblinImage.color = new Color(1f, 1f, 1f, 1f - t);

                yield return null;
            }

            // 정리
            if (goblin.visualObject != null)
                Destroy(goblin.visualObject);
            goblin.visualObject = null;

            Debug.Log($"[GoblinSystem] 고블린 제거: ({goblin.position}), 총 킬: {totalKills}");
        }

        private IEnumerator SpawnDeathDebris(Transform parent, Vector2 position)
        {
            int debrisCount = Random.Range(8, 13);
            List<GameObject> debrisList = new List<GameObject>();

            for (int i = 0; i < debrisCount; i++)
            {
                GameObject debris = new GameObject("GoblinDebris");
                debris.transform.SetParent(parent, false);

                RectTransform drt = debris.AddComponent<RectTransform>();
                drt.anchoredPosition = position;
                float dSize = Random.Range(4f, 10f);
                drt.sizeDelta = new Vector2(dSize, dSize);

                Image dimg = debris.AddComponent<Image>();
                // 녹색 파편 (고블린 색상)
                dimg.color = new Color(
                    Random.Range(0.15f, 0.45f),
                    Random.Range(0.5f, 0.85f),
                    Random.Range(0.1f, 0.3f),
                    1f);
                dimg.raycastTarget = false;

                debrisList.Add(debris);
            }

            // 파편 애니메이션
            float duration = 0.4f;
            float elapsed = 0f;
            Vector2[] velocities = new Vector2[debrisCount];
            float[] rotSpeeds = new float[debrisCount];

            for (int i = 0; i < debrisCount; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(100f, 300f);
                velocities[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                rotSpeeds[i] = Random.Range(-720f, 720f);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                for (int i = 0; i < debrisList.Count; i++)
                {
                    if (debrisList[i] == null) continue;
                    RectTransform drt = debrisList[i].GetComponent<RectTransform>();
                    drt.anchoredPosition = position + velocities[i] * t;
                    drt.localEulerAngles = new Vector3(0, 0, rotSpeeds[i] * t);

                    Image dimg = debrisList[i].GetComponent<Image>();
                    if (dimg != null)
                    {
                        Color c = dimg.color;
                        c.a = 1f - t;
                        dimg.color = c;
                    }
                }
                yield return null;
            }

            foreach (var d in debrisList)
            {
                if (d != null) Destroy(d);
            }
        }

        // ============================================================
        // 프로시저럴 스프라이트 생성
        // ============================================================

        /// <summary>
        /// 고블린 프로시저럴 스프라이트 (256x256)
        /// 녹색 몸체 + 눈 + 몽둥이 + 뾰족한 귀
        /// </summary>
        private static Sprite CreateGoblinSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            // 투명 초기화
            Color[] clear = new Color[size * size];
            for (int i = 0; i < clear.Length; i++)
                clear[i] = Color.clear;
            tex.SetPixels(clear);

            int cx = size / 2;
            int cy = size / 2;

            // === 몸체 (녹색 원형, 반지름 ~45% of size) ===
            int bodyRadius = (int)(size * 0.35f);
            Color bodyColor = new Color(0.3f, 0.65f, 0.2f);
            Color bodyDark = new Color(0.2f, 0.5f, 0.12f);
            Color bodyLight = new Color(0.4f, 0.78f, 0.3f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist < bodyRadius)
                    {
                        // 그라데이션 (왼쪽 상단 밝게)
                        float shade = Mathf.Clamp01((dx + dy) / (bodyRadius * 2f) + 0.5f);
                        Color c = Color.Lerp(bodyLight, bodyDark, shade);
                        tex.SetPixel(x, y, c);
                    }
                    else if (dist < bodyRadius + 2f)
                    {
                        // 안티앨리어싱 가장자리
                        float alpha = 1f - (dist - bodyRadius) / 2f;
                        tex.SetPixel(x, y, new Color(bodyDark.r, bodyDark.g, bodyDark.b, alpha));
                    }
                }
            }

            // === 뾰족한 귀 (왼쪽, 오른쪽) ===
            Color earColor = new Color(0.25f, 0.55f, 0.18f);
            DrawTriangle(tex, cx - bodyRadius + 5, cy + bodyRadius - 10,
                         cx - bodyRadius - 15, cy + bodyRadius + 25,
                         cx - bodyRadius + 20, cy + bodyRadius + 5, earColor);
            DrawTriangle(tex, cx + bodyRadius - 5, cy + bodyRadius - 10,
                         cx + bodyRadius + 15, cy + bodyRadius + 25,
                         cx + bodyRadius - 20, cy + bodyRadius + 5, earColor);

            // === 눈 (흰색 동공 + 빨간 눈동자) ===
            int eyeRadius = (int)(size * 0.07f);
            int eyeOffsetX = (int)(size * 0.12f);
            int eyeOffsetY = (int)(size * 0.06f);

            // 왼쪽 눈
            DrawFilledCircle(tex, cx - eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx - eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.8f, 0.1f, 0.1f));

            // 오른쪽 눈
            DrawFilledCircle(tex, cx + eyeOffsetX, cy + eyeOffsetY, eyeRadius, Color.white);
            DrawFilledCircle(tex, cx + eyeOffsetX + 2, cy + eyeOffsetY, eyeRadius / 2, new Color(0.8f, 0.1f, 0.1f));

            // === 입 (짧은 곡선) ===
            Color mouthColor = new Color(0.15f, 0.35f, 0.1f);
            for (int i = -12; i <= 12; i++)
            {
                int mx = cx + i;
                int my = cy - (int)(size * 0.08f) - Mathf.Abs(i) / 3;
                if (mx >= 0 && mx < size && my >= 0 && my < size)
                    tex.SetPixel(mx, my, mouthColor);
                if (mx >= 0 && mx < size && my - 1 >= 0)
                    tex.SetPixel(mx, my - 1, mouthColor);
            }

            // === 몽둥이 (오른쪽 위 대각선) ===
            Color clubColor = new Color(0.55f, 0.35f, 0.15f);
            Color clubDark = new Color(0.4f, 0.25f, 0.1f);
            int clubStartX = cx + bodyRadius - 10;
            int clubStartY = cy + 5;
            int clubEndX = cx + bodyRadius + 35;
            int clubEndY = cy + bodyRadius + 30;

            // 몽둥이 자루 (직선)
            DrawThickLine(tex, clubStartX, clubStartY, clubEndX, clubEndY, 4, clubColor);

            // 몽둥이 머리 (타원)
            DrawFilledCircle(tex, clubEndX, clubEndY, 12, clubDark);
            DrawFilledCircle(tex, clubEndX - 2, clubEndY + 2, 10, clubColor);

            // === 이빨 (작은 삼각형 2개) ===
            Color toothColor = new Color(0.95f, 0.95f, 0.85f);
            int toothY = cy - (int)(size * 0.08f) - 2;
            DrawTriangle(tex, cx - 5, toothY, cx - 2, toothY - 7, cx + 1, toothY, toothColor);
            DrawTriangle(tex, cx + 3, toothY, cx + 6, toothY - 7, cx + 9, toothY, toothColor);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>
        /// 포털 스프라이트 (원형 글로우, 128x128)
        /// </summary>
        private static Sprite CreatePortalSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            int cx = size / 2;
            int cy = size / 2;
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist < radius)
                    {
                        // 중앙은 밝고 가장자리는 어두운 원형 그라데이션
                        float t = dist / radius;
                        float alpha = 1f - t * t;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ============================================================
        // 드로잉 헬퍼
        // ============================================================

        private static void DrawFilledCircle(Texture2D tex, int cx, int cy, int radius, Color color)
        {
            int size = tex.width;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || x >= size || y < 0 || y >= size) continue;
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius)
                        tex.SetPixel(x, y, color);
                }
            }
        }

        private static void DrawTriangle(Texture2D tex, int x0, int y0, int x1, int y1, int x2, int y2, Color color)
        {
            int size = tex.width;
            int minX = Mathf.Max(0, Mathf.Min(x0, Mathf.Min(x1, x2)));
            int maxX = Mathf.Min(size - 1, Mathf.Max(x0, Mathf.Max(x1, x2)));
            int minY = Mathf.Max(0, Mathf.Min(y0, Mathf.Min(y1, y2)));
            int maxY = Mathf.Min(size - 1, Mathf.Max(y0, Mathf.Max(y1, y2)));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    // 바리센트릭 좌표로 삼각형 내부 판정
                    float d = (float)((y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2));
                    if (Mathf.Abs(d) < 0.001f) continue;

                    float a = ((y1 - y2) * (x - x2) + (x2 - x1) * (y - y2)) / d;
                    float b = ((y2 - y0) * (x - x2) + (x0 - x2) * (y - y2)) / d;
                    float c = 1f - a - b;

                    if (a >= 0f && b >= 0f && c >= 0f)
                        tex.SetPixel(x, y, color);
                }
            }
        }

        private static void DrawThickLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color color)
        {
            int size = tex.width;
            float dx = x1 - x0;
            float dy = y1 - y0;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 1f) return;

            dx /= length;
            dy /= length;

            for (float t = 0; t < length; t += 0.5f)
            {
                int cx = (int)(x0 + dx * t);
                int cy = (int)(y0 + dy * t);

                for (int oy = -thickness / 2; oy <= thickness / 2; oy++)
                {
                    for (int ox = -thickness / 2; ox <= thickness / 2; ox++)
                    {
                        int px = cx + ox;
                        int py = cy + oy;
                        if (px >= 0 && px < size && py >= 0 && py < size)
                            tex.SetPixel(px, py, color);
                    }
                }
            }
        }

        // ============================================================
        // 이펙트 정리
        // ============================================================

        /// <summary>
        /// 고블린 관련 이펙트 정리
        /// </summary>
        public void CleanupEffects()
        {
            if (hexGrid == null || hexGrid.GridContainer == null) return;

            string[] effectNames = { "GoblinPortal", "GoblinShockwave", "GoblinHitFlash", "GoblinDebris" };

            foreach (Transform child in hexGrid.GridContainer)
            {
                if (child == null) continue;
                foreach (var name in effectNames)
                {
                    if (child.name == name)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        // ============================================================
        // 유틸리티
        // ============================================================

        /// <summary>
        /// 현재 보드의 모든 살아있는 고블린 위치 반환
        /// </summary>
        public List<HexCoord> GetAllGoblinPositions()
        {
            return goblins.Where(g => g.isAlive).Select(g => g.position).ToList();
        }

        /// <summary>
        /// 활성 상태인지 (config가 설정되어 있는지)
        /// </summary>
        public bool IsActive => currentConfig != null;

        /// <summary>
        /// 고블린 스프라이트 외부 접근용 (미션 아이콘 등)
        /// </summary>
        public static Sprite GetGoblinSprite()
        {
            if (goblinSprite == null)
                goblinSprite = CreateGoblinSprite(256);
            return goblinSprite;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 10종 적군 통합 관리 시스템
    /// 스폰, 턴 라이프사이클, 적군별 행동 로직
    /// </summary>
    public class EnemySystem : MonoBehaviour
    {
        public static EnemySystem Instance { get; private set; }

        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private MatchingSystem matchingSystem;

        // 쌍둥이 페어 관리
        private int nextTwinId = 0;
        private Dictionary<int, List<HexBlock>> twinPairs = new Dictionary<int, List<HexBlock>>();

        // 턴 내 처치 추적
        private List<EnemyKillData> turnKills = new List<EnemyKillData>();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null) Debug.Log("[EnemySystem] HexGrid auto-found");
            }
            if (matchingSystem == null)
            {
                matchingSystem = FindObjectOfType<MatchingSystem>();
                if (matchingSystem != null) Debug.Log("[EnemySystem] MatchingSystem auto-found");
            }
        }

        // ============================================================
        // 스폰 설정
        // ============================================================

        [System.Serializable]
        public class EnemySpawnConfig
        {
            public EnemyType type;
            public float weight;       // 가중치 (확률)
            public int minRotation;    // 최소 회전수
            public int maxOnBoard;     // 보드 최대 수
        }

        /// <summary>
        /// 스테이지별 스폰 설정 반환
        /// </summary>
        private List<EnemySpawnConfig> GetSpawnConfigs(int stage)
        {
            var configs = new List<EnemySpawnConfig>();

            switch (stage)
            {
                case 1:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Chromophage, weight = 1f, minRotation = 0, maxOnBoard = 20 });
                    break;
                case 2:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Chromophage, weight = 0.5f, minRotation = 0, maxOnBoard = 15 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ChainAnchor, weight = 0.5f, minRotation = 0, maxOnBoard = 10 });
                    break;
                case 3:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ChainAnchor, weight = 0.5f, minRotation = 0, maxOnBoard = 10 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ThornParasite, weight = 0.5f, minRotation = 0, maxOnBoard = 8 });
                    break;
                case 4:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Chromophage, weight = 0.3f, minRotation = 0, maxOnBoard = 10 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ChainAnchor, weight = 0.2f, minRotation = 0, maxOnBoard = 8 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Divider, weight = 0.5f, minRotation = 0, maxOnBoard = 6 });
                    break;
                case 5:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Chromophage, weight = 0.2f, minRotation = 0, maxOnBoard = 8 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.GravityWarper, weight = 0.4f, minRotation = 0, maxOnBoard = 4 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ReflectionShield, weight = 0.4f, minRotation = 0, maxOnBoard = 4 });
                    break;
                case 6:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ChainAnchor, weight = 0.2f, minRotation = 0, maxOnBoard = 6 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.TimeFreezer, weight = 0.4f, minRotation = 0, maxOnBoard = 3 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Divider, weight = 0.4f, minRotation = 0, maxOnBoard = 5 });
                    break;
                case 7:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ResonanceTwin, weight = 0.5f, minRotation = 0, maxOnBoard = 4 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ShadowSpore, weight = 0.3f, minRotation = 0, maxOnBoard = 4 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.GravityWarper, weight = 0.2f, minRotation = 0, maxOnBoard = 3 });
                    break;
                case 8:
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ShadowSpore, weight = 0.3f, minRotation = 0, maxOnBoard = 5 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ReflectionShield, weight = 0.3f, minRotation = 0, maxOnBoard = 4 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.TimeFreezer, weight = 0.2f, minRotation = 0, maxOnBoard = 3 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Divider, weight = 0.2f, minRotation = 0, maxOnBoard = 4 });
                    break;
                default: // 9+
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ChaosOverlord, weight = 0.15f, minRotation = 0, maxOnBoard = 1 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ShadowSpore, weight = 0.2f, minRotation = 0, maxOnBoard = 5 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ResonanceTwin, weight = 0.2f, minRotation = 0, maxOnBoard = 4 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.ReflectionShield, weight = 0.15f, minRotation = 0, maxOnBoard = 3 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.Divider, weight = 0.15f, minRotation = 0, maxOnBoard = 4 });
                    configs.Add(new EnemySpawnConfig { type = EnemyType.TimeFreezer, weight = 0.15f, minRotation = 0, maxOnBoard = 3 });
                    break;
            }

            return configs;
        }

        // ============================================================
        // 보드 상태 카운팅
        // ============================================================

        private Dictionary<EnemyType, int> CountEnemiesOnBoard()
        {
            var counts = new Dictionary<EnemyType, int>();
            if (hexGrid == null) return counts;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.enemyType == EnemyType.None) continue;

                if (!counts.ContainsKey(block.Data.enemyType))
                    counts[block.Data.enemyType] = 0;
                counts[block.Data.enemyType]++;
            }
            return counts;
        }

        // ============================================================
        // 스폰
        // ============================================================

        /// <summary>
        /// 스테이지별 적군 스폰 (GameManager에서 호출)
        /// </summary>
        public IEnumerator SpawnEnemiesForStage(int stageNumber, int count, int rotationCount)
        {
            if (hexGrid == null) yield break;

            var configs = GetSpawnConfigs(stageNumber);
            var boardCounts = CountEnemiesOnBoard();

            // 후보 블록 수집
            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None || block.Data.gemType == GemType.Gray) continue;
                if (block.Data.specialType != SpecialBlockType.None) continue;
                if (block.Data.hasChain || block.Data.hasThorn) continue;
                if (block.Data.enemyType != EnemyType.None) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) yield break;

            int spawnCount = Mathf.Min(count, candidates.Count);
            List<Coroutine> animations = new List<Coroutine>();

            for (int i = 0; i < spawnCount; i++)
            {
                if (candidates.Count == 0) break;

                // 가중치 기반 적군 타입 선택
                EnemyType selectedType = SelectEnemyType(configs, boardCounts, rotationCount);
                if (selectedType == EnemyType.None)
                {
                    // fallback: Chromophage
                    selectedType = EnemyType.Chromophage;
                }

                // 쌍둥이는 2개씩 스폰
                if (selectedType == EnemyType.ResonanceTwin && candidates.Count >= 2)
                {
                    int idx1 = Random.Range(0, candidates.Count);
                    HexBlock block1 = candidates[idx1];
                    candidates.RemoveAt(idx1);

                    int idx2 = Random.Range(0, candidates.Count);
                    HexBlock block2 = candidates[idx2];
                    candidates.RemoveAt(idx2);

                    int twinId = nextTwinId++;
                    animations.Add(StartCoroutine(SpawnEnemyWithAnimation(block1, EnemyType.ResonanceTwin, i * 0.15f, twinId)));
                    animations.Add(StartCoroutine(SpawnEnemyWithAnimation(block2, EnemyType.ResonanceTwin, i * 0.15f + 0.08f, twinId)));

                    // 페어 등록
                    twinPairs[twinId] = new List<HexBlock> { block1, block2 };
                    i++; // 2개 소모

                    if (!boardCounts.ContainsKey(EnemyType.ResonanceTwin))
                        boardCounts[EnemyType.ResonanceTwin] = 0;
                    boardCounts[EnemyType.ResonanceTwin] += 2;
                }
                else
                {
                    int idx = Random.Range(0, candidates.Count);
                    HexBlock block = candidates[idx];
                    candidates.RemoveAt(idx);

                    animations.Add(StartCoroutine(SpawnEnemyWithAnimation(block, selectedType, i * 0.15f)));

                    if (!boardCounts.ContainsKey(selectedType))
                        boardCounts[selectedType] = 0;
                    boardCounts[selectedType]++;
                }
            }

            foreach (var co in animations)
                yield return co;
        }

        /// <summary>
        /// 가중치 기반 적군 타입 선택
        /// </summary>
        private EnemyType SelectEnemyType(List<EnemySpawnConfig> configs, Dictionary<EnemyType, int> boardCounts, int rotationCount)
        {
            float totalWeight = 0f;
            List<EnemySpawnConfig> validConfigs = new List<EnemySpawnConfig>();

            foreach (var cfg in configs)
            {
                if (rotationCount < cfg.minRotation) continue;

                int currentCount = boardCounts.ContainsKey(cfg.type) ? boardCounts[cfg.type] : 0;
                if (currentCount >= cfg.maxOnBoard) continue;

                validConfigs.Add(cfg);
                totalWeight += cfg.weight;
            }

            if (validConfigs.Count == 0 || totalWeight <= 0f) return EnemyType.None;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            foreach (var cfg in validConfigs)
            {
                cumulative += cfg.weight;
                if (roll <= cumulative)
                    return cfg.type;
            }

            return validConfigs[validConfigs.Count - 1].type;
        }

        // ============================================================
        // 블록에 적군 적용
        // ============================================================

        /// <summary>
        /// 블록에 적군 적용 (dual-write: 기존 필드도 설정)
        /// </summary>
        public void ApplyEnemyToBlock(HexBlock block, EnemyType type, int twinId = -1)
        {
            if (block == null || block.Data == null) return;

            block.Data.enemyType = type;

            // dual-write: 기존 시스템 호환
            switch (type)
            {
                case EnemyType.Chromophage:
                    block.SetBlockData(new BlockData(GemType.Gray) { enemyType = EnemyType.Chromophage });
                    break;
                case EnemyType.ChainAnchor:
                    block.Data.hasChain = true;
                    block.Data.enemyType = EnemyType.ChainAnchor;
                    block.UpdateVisuals();
                    break;
                case EnemyType.ThornParasite:
                    block.Data.hasThorn = true;
                    block.Data.enemyType = EnemyType.ThornParasite;
                    block.UpdateVisuals();
                    break;
                case EnemyType.ReflectionShield:
                    block.Data.enemyShieldCount = 1;
                    block.Data.enemyType = EnemyType.ReflectionShield;
                    block.UpdateVisuals();
                    break;
                case EnemyType.ShadowSpore:
                    block.Data.enemySpreadTimer = GetSporeBaseTimer();
                    block.Data.enemyType = EnemyType.ShadowSpore;
                    block.UpdateVisuals();
                    break;
                case EnemyType.ResonanceTwin:
                    block.Data.enemyTwinId = twinId;
                    block.Data.enemyType = EnemyType.ResonanceTwin;
                    block.UpdateVisuals();
                    break;
                case EnemyType.ChaosOverlord:
                    block.Data.chaosHitCount = 0;
                    block.Data.enemyType = EnemyType.ChaosOverlord;
                    RerollChaosEffects(block);
                    block.UpdateVisuals();
                    break;
                default:
                    block.Data.enemyType = type;
                    block.UpdateVisuals();
                    break;
            }

            Debug.Log($"[EnemySystem] 적군 적용: ({block.Coord}) type={type}");
        }

        /// <summary>
        /// 적군 스폰 애니메이션 (스케일 펄스 + 흔들림)
        /// </summary>
        private IEnumerator SpawnEnemyWithAnimation(HexBlock block, EnemyType type, float delay, int twinId = -1)
        {
            if (block == null || block.Data == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (block == null || block.Data == null) yield break;

            Color originalColor = GemColors.GetColor(block.Data.gemType);
            ApplyEnemyToBlock(block, type, twinId);

            // 스폰 애니메이션
            float duration = 0.3f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (block == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scalePulse = 1f + 0.15f * Mathf.Sin(t * Mathf.PI);
                block.transform.localScale = Vector3.one * scalePulse;

                float shake = (1f - t) * 2f;
                float offsetX = Mathf.Sin(t * Mathf.PI * 8f) * shake;

                RectTransform rt = block.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Vector2 basePos = rt.anchoredPosition;
                    rt.anchoredPosition = new Vector2(
                        basePos.x + offsetX * Time.deltaTime * 60f,
                        basePos.y);
                }

                yield return null;
            }

            if (block != null)
            {
                block.transform.localScale = Vector3.one;
                block.UpdateVisuals();
            }
        }

        // ============================================================
        // 턴 라이프사이클
        // ============================================================

        /// <summary>
        /// 턴 시작 시 호출 (회전 완료 직후)
        /// </summary>
        public void OnTurnStart()
        {
            turnKills.Clear();
        }

        /// <summary>
        /// 턴 종료 시 호출 (캐스케이드 완료 후, 적군 스폰 전)
        /// </summary>
        public void OnTurnEnd()
        {
            ProcessTwinRegeneration();
            ProcessSporeSpread();
            RerollAllChaosEffects();
        }

        /// <summary>
        /// 처치 기록
        /// </summary>
        public void RegisterKill(EnemyKillData killData)
        {
            turnKills.Add(killData);
            Debug.Log($"[EnemySystem] 적군 처치: {killData.enemyType} via {killData.method} ({killData.condition})");
        }

        // ============================================================
        // Divider (#4): 분열 처리
        // ============================================================

        /// <summary>
        /// 분열체 제거 후 인접 빈칸에 새 분열체 생성
        /// method가 SpecialBasic(드릴)일 때만 분열 발생
        /// </summary>
        public void ProcessDividerSplits(List<HexBlock> removedDividers, RemovalMethod method)
        {
            if (method != RemovalMethod.Match && method != RemovalMethod.SpecialBasic) return;

            foreach (var block in removedDividers)
            {
                if (block == null || hexGrid == null) continue;

                // 인접 빈칸 찾기
                var neighbors = hexGrid.GetNeighbors(block.Coord);
                List<HexBlock> emptyNeighbors = new List<HexBlock>();
                foreach (var n in neighbors)
                {
                    if (n != null && n.Data != null && n.Data.gemType == GemType.None)
                        emptyNeighbors.Add(n);
                }

                if (emptyNeighbors.Count > 0)
                {
                    // 랜덤 빈칸 하나에 분열체 생성
                    HexBlock target = emptyNeighbors[Random.Range(0, emptyNeighbors.Count)];
                    GemType randomGem = GemTypeHelper.GetRandom();
                    target.SetBlockData(new BlockData(randomGem));
                    ApplyEnemyToBlock(target, EnemyType.Divider);
                    Debug.Log($"[EnemySystem] Divider 분열: ({block.Coord}) → ({target.Coord})");
                }
            }
        }

        // ============================================================
        // ReflectionShield (#6): 특수블록 흡수
        // ============================================================

        /// <summary>
        /// 특수블록 공격을 방패가 흡수하는지 확인
        /// true 반환 시: 방패만 파괴, 블록은 보존
        /// </summary>
        public bool TryAbsorbSpecialHit(HexBlock block)
        {
            if (block == null || block.Data == null) return false;
            if (!block.Data.HasShield()) return false;

            block.Data.enemyShieldCount--;
            Debug.Log($"[EnemySystem] 방패 흡수: ({block.Coord}) 남은={block.Data.enemyShieldCount}");

            if (block.Data.enemyShieldCount <= 0)
            {
                // 방패 파괴, 적군 타입 제거
                if (block.Data.enemyType == EnemyType.ReflectionShield)
                {
                    block.Data.enemyType = EnemyType.None;
                    RegisterKill(new EnemyKillData
                    {
                        enemyType = EnemyType.ReflectionShield,
                        method = RemovalMethod.SpecialAdvanced,
                        condition = RemovalCondition.ShieldBroken
                    });
                }
            }

            block.UpdateVisuals();
            return true;
        }

        // ============================================================
        // TimeFreezer (#7): 회전 비용 계산
        // ============================================================

        /// <summary>
        /// 회전에 포함된 블록들의 TimeFreezer 비용 계산
        /// </summary>
        public int GetRotationCost(HexBlock b1, HexBlock b2, HexBlock b3)
        {
            HexBlock[] blocks = { b1, b2, b3 };
            foreach (var b in blocks)
            {
                if (b != null && b.Data != null)
                {
                    if (b.Data.enemyType == EnemyType.TimeFreezer)
                        return 2;
                    if (b.Data.enemyType == EnemyType.ChaosOverlord &&
                        (b.Data.chaosEffectMask & ChaosEffect.TimeFreezer) != 0)
                        return 2;
                }
            }
            return 1;
        }

        // ============================================================
        // ResonanceTwin (#8): 쌍둥이 재생
        // ============================================================

        private void ProcessTwinRegeneration()
        {
            List<int> pairsToRemove = new List<int>();

            foreach (var kvp in twinPairs)
            {
                int twinId = kvp.Key;
                var pair = kvp.Value;

                // 유효성 검증
                pair.RemoveAll(b => b == null || b.Data == null || b.Data.enemyType != EnemyType.ResonanceTwin || b.Data.enemyTwinId != twinId);

                if (pair.Count == 0)
                {
                    // 둘 다 죽음 → 완전 소멸
                    pairsToRemove.Add(twinId);
                    Debug.Log($"[EnemySystem] 쌍둥이 완전소멸: twinId={twinId}");
                }
                else if (pair.Count == 1)
                {
                    // 한쪽만 남음 → 재생
                    HexBlock survivor = pair[0];
                    if (hexGrid == null) continue;

                    // 인접 빈칸 찾기
                    var neighbors = hexGrid.GetNeighbors(survivor.Coord);
                    HexBlock regenTarget = null;
                    foreach (var n in neighbors)
                    {
                        if (n != null && n.Data != null && n.Data.gemType != GemType.None &&
                            n.Data.specialType == SpecialBlockType.None && n.Data.enemyType == EnemyType.None)
                        {
                            regenTarget = n;
                            break;
                        }
                    }

                    if (regenTarget != null)
                    {
                        ApplyEnemyToBlock(regenTarget, EnemyType.ResonanceTwin, twinId);
                        pair.Add(regenTarget);
                        Debug.Log($"[EnemySystem] 쌍둥이 재생: twinId={twinId}, ({regenTarget.Coord})");
                    }
                }
                // pair.Count == 2 → 정상
            }

            foreach (int id in pairsToRemove)
                twinPairs.Remove(id);
        }

        // ============================================================
        // ShadowSpore (#9): 포자 확산
        // ============================================================

        private int GetSporeBaseTimer()
        {
            int sporeCount = 0;
            if (hexGrid != null)
            {
                foreach (var b in hexGrid.GetAllBlocks())
                    if (b != null && b.Data != null && b.Data.enemyType == EnemyType.ShadowSpore)
                        sporeCount++;
            }
            return Mathf.Max(1, 3 - (sporeCount - 1));
        }

        private void ProcessSporeSpread()
        {
            if (hexGrid == null) return;

            List<HexBlock> spores = new List<HexBlock>();
            foreach (var b in hexGrid.GetAllBlocks())
            {
                if (b != null && b.Data != null && b.Data.enemyType == EnemyType.ShadowSpore)
                    spores.Add(b);
            }

            foreach (var spore in spores)
            {
                spore.Data.enemySpreadTimer--;
                if (spore.Data.enemySpreadTimer <= 0)
                {
                    // 확산
                    var neighbors = hexGrid.GetNeighbors(spore.Coord);
                    List<HexBlock> spreadTargets = new List<HexBlock>();
                    foreach (var n in neighbors)
                    {
                        if (n != null && n.Data != null && n.Data.gemType != GemType.None &&
                            n.Data.specialType == SpecialBlockType.None && n.Data.enemyType == EnemyType.None &&
                            !n.Data.hasChain)
                        {
                            spreadTargets.Add(n);
                        }
                    }

                    if (spreadTargets.Count > 0)
                    {
                        HexBlock target = spreadTargets[Random.Range(0, spreadTargets.Count)];
                        ApplyEnemyToBlock(target, EnemyType.ShadowSpore);
                        Debug.Log($"[EnemySystem] 포자 확산: ({spore.Coord}) → ({target.Coord})");
                    }

                    // 타이머 리셋 (가속)
                    spore.Data.enemySpreadTimer = GetSporeBaseTimer();
                }
            }
        }

        // ============================================================
        // ChaosOverlord (#10): 효과 리롤
        // ============================================================

        /// <summary>
        /// 단일 카오스 군주 효과 리롤
        /// </summary>
        public void RerollChaosEffects(HexBlock block)
        {
            if (block == null || block.Data == null) return;
            if (block.Data.enemyType != EnemyType.ChaosOverlord) return;

            // 2~3개 효과 랜덤 선택
            ChaosEffect[] allEffects = {
                ChaosEffect.Chromophage,
                ChaosEffect.ChainAnchor,
                ChaosEffect.GravityWarper,
                ChaosEffect.ReflectionShield,
                ChaosEffect.TimeFreezer
            };

            int effectCount = Random.Range(2, 4); // 2~3
            ChaosEffect mask = ChaosEffect.None;
            List<int> indices = new List<int>();
            for (int i = 0; i < allEffects.Length; i++) indices.Add(i);

            for (int i = 0; i < effectCount && indices.Count > 0; i++)
            {
                int pick = Random.Range(0, indices.Count);
                mask |= allEffects[indices[pick]];
                indices.RemoveAt(pick);
            }

            block.Data.chaosEffectMask = mask;

            // ReflectionShield 효과가 있으면 방패도 부여
            if ((mask & ChaosEffect.ReflectionShield) != 0)
                block.Data.enemyShieldCount = 1;

            Debug.Log($"[EnemySystem] 카오스 효과 리롤: ({block.Coord}) mask={mask}");
        }

        private void RerollAllChaosEffects()
        {
            if (hexGrid == null) return;
            foreach (var b in hexGrid.GetAllBlocks())
            {
                if (b != null && b.Data != null && b.Data.enemyType == EnemyType.ChaosOverlord)
                    RerollChaosEffects(b);
            }
        }

        // ============================================================
        // ChaosOverlord 피격 처리
        // ============================================================

        /// <summary>
        /// 카오스 군주 피격. 도넛=1회, 나머지=3회
        /// true 반환: 아직 살아있음 (블록 보존)
        /// false 반환: 사망 (블록 제거 가능)
        /// </summary>
        public bool ProcessChaosHit(HexBlock block, RemovalMethod method)
        {
            if (block == null || block.Data == null) return false;
            if (block.Data.enemyType != EnemyType.ChaosOverlord) return false;

            if (method == RemovalMethod.Donut)
            {
                // 도넛은 1회 제거
                block.Data.enemyType = EnemyType.None;
                block.Data.chaosEffectMask = ChaosEffect.None;
                RegisterKill(new EnemyKillData
                {
                    enemyType = EnemyType.ChaosOverlord,
                    method = method,
                    condition = RemovalCondition.Normal
                });
                return false;
            }

            block.Data.chaosHitCount++;
            if (block.Data.chaosHitCount >= 3)
            {
                block.Data.enemyType = EnemyType.None;
                block.Data.chaosEffectMask = ChaosEffect.None;
                RegisterKill(new EnemyKillData
                {
                    enemyType = EnemyType.ChaosOverlord,
                    method = method,
                    condition = RemovalCondition.ChaosWeakened
                });
                return false;
            }

            Debug.Log($"[EnemySystem] 카오스 피격: ({block.Coord}) hit={block.Data.chaosHitCount}/3");
            block.UpdateVisuals();
            return true; // 아직 살아있음
        }

        // ============================================================
        // 매칭 제외 체크 (ChaosOverlord Chromophage 효과)
        // ============================================================

        /// <summary>
        /// 해당 블록이 매칭에서 제외되어야 하는지 (ChaosOverlord의 Chromophage 효과)
        /// </summary>
        public bool IsMatchExcluded(HexBlock block)
        {
            if (block == null || block.Data == null) return false;
            return block.Data.enemyType == EnemyType.ChaosOverlord &&
                   (block.Data.chaosEffectMask & ChaosEffect.Chromophage) != 0;
        }

        /// <summary>
        /// 해당 블록이 회전 불가인지 (ChaosOverlord의 ChainAnchor 효과)
        /// </summary>
        public bool IsRotationBlocked(HexBlock block)
        {
            if (block == null || block.Data == null) return false;
            return block.Data.enemyType == EnemyType.ChaosOverlord &&
                   (block.Data.chaosEffectMask & ChaosEffect.ChainAnchor) != 0;
        }
    }
}

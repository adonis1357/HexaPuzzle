using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    /// <summary>
    /// 드론 특수 블록 시스템
    /// 나비 패턴(5개) 매칭 시 생성, 발동 시 우선순위 기반 단일 타격 (1데미지)
    /// </summary>
    public class DroneBlockSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private BlockRemovalSystem removalSystem;
        private StageManager stageManager;

        public event System.Action<int> OnDroneComplete;

        private int activeDroneCount = 0;
        private List<HexBlock> pendingSpecialBlocks = new List<HexBlock>();
        private HashSet<HexBlock> activeBlocks = new HashSet<HexBlock>();
        private Transform effectParent;
        private static Sprite droneIconSprite;

        public bool IsActivating => activeDroneCount > 0;
        public List<HexBlock> PendingSpecialBlocks => pendingSpecialBlocks;
        public bool IsBlockActive(HexBlock block) => activeBlocks.Contains(block);

        public void ForceReset()
        {
            activeDroneCount = 0;
            pendingSpecialBlocks.Clear();
            activeBlocks.Clear();
            StopAllCoroutines();
            CleanupEffects();
            Debug.Log("[DroneBlockSystem] ForceReset called");
        }

        private void CleanupEffects()
        {
            if (effectParent == null) return;
            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);
        }

        private void SetupEffectParent()
        {
            if (effectParent != null) return;
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                GameObject epObj = new GameObject("DroneEffects");
                epObj.transform.SetParent(canvas.transform, false);
                effectParent = epObj.transform;
            }
        }

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[DroneBlockSystem] HexGrid auto-found: " + hexGrid.name);
                else
                    Debug.LogError("[DroneBlockSystem] HexGrid not found!");
            }
            if (removalSystem == null)
                removalSystem = FindObjectOfType<BlockRemovalSystem>();
            if (stageManager == null)
                stageManager = FindObjectOfType<StageManager>();

            SetupEffectParent();
        }

        // ============================================================
        // 드론 블록 생성
        // ============================================================

        public void CreateDroneBlock(HexBlock block, GemType gemType)
        {
            if (block == null) return;
            BlockData droneData = new BlockData(gemType);
            droneData.specialType = SpecialBlockType.Drone;
            block.SetBlockData(droneData);
            Debug.Log($"[DroneBlockSystem] Created drone at {block.Coord}, gemType={gemType}");
        }

        // ============================================================
        // 드론 발동
        // ============================================================

        public void ActivateDrone(HexBlock droneBlock)
        {
            if (droneBlock == null) return;
            if (droneBlock.Data == null || droneBlock.Data.specialType != SpecialBlockType.Drone)
                return;
            Debug.Log($"[DroneBlockSystem] Activating drone at {droneBlock.Coord}");
            StartCoroutine(DroneCoroutine(droneBlock));
        }

        private IEnumerator DroneCoroutine(HexBlock droneBlock)
        {
            activeDroneCount++;
            activeBlocks.Add(droneBlock);
            SetupEffectParent();

            bool isFirst = (activeDroneCount == 1);

            // 정보 캐싱
            Vector3 droneWorldPos = droneBlock.transform.position;
            Color droneColor = GemColors.GetColor(droneBlock.Data.gemType);

            // 1. Pre-Fire 압축 애니메이션
            if (isFirst)
                yield return StartCoroutine(PreFireCompression(droneBlock));

            // 2. 드론 블록 클리어
            droneBlock.ClearData();

            // 3. 타겟 선택
            HexBlock target = FindTargetBlock(droneBlock);

            if (target == null)
            {
                Debug.Log("[DroneBlockSystem] 타겟 없음, 발동 종료");
                OnDroneComplete?.Invoke(50);
                activeBlocks.Remove(droneBlock);
                activeDroneCount--;
                yield break;
            }

            // 타격 전 타겟 정보 캐싱 (타격 후 Data가 변경될 수 있으므로)
            GemType targetGemType = target.Data != null ? target.Data.gemType : GemType.None;
            SpecialBlockType targetSpecialType = target.Data != null ? target.Data.specialType : SpecialBlockType.None;

            Debug.Log($"[DroneBlockSystem] 타겟 선택: {target.Coord} ({targetGemType}, tier={target.Data?.tier}, special={targetSpecialType})");

            // 4. 드론 비행 이펙트
            Vector3 targetWorldPos = target.transform.position;
            yield return StartCoroutine(DroneFlyEffect(droneWorldPos, targetWorldPos, droneColor));

            // 5. 화면 흔들림
            if (isFirst)
                StartCoroutine(ScreenShake(3f, 0.15f));

            // 6. 타격 이펙트
            yield return StartCoroutine(DroneStrikeEffect(target, droneColor));

            // 7. 타격 효과 적용 (1데미지)
            ApplyDroneStrike(target);

            // 8. 점수
            int score = 150;
            OnDroneComplete?.Invoke(score);

            // 9. 미션 알림 (캐싱된 정보 사용)
            if (GameManager.Instance != null && targetGemType != GemType.None)
            {
                var colorDict = new Dictionary<GemType, int>();
                colorDict[targetGemType] = 1;
                GameManager.Instance.OnSpecialBlockDestroyedBlocksByColor(colorDict, "Drone");
            }

            activeBlocks.Remove(droneBlock);
            activeDroneCount--;
        }

        // ============================================================
        // 타겟팅 시스템
        // ============================================================

        /// <summary>
        /// 현재 활성 미션에서 수집 대상 GemType 목록 추출
        /// 무한모드(MissionSystem) + 스테이지모드(StageManager) 모두 지원
        /// </summary>
        private HashSet<GemType> GetMissionTargetGemTypes()
        {
            HashSet<GemType> targets = new HashSet<GemType>();

            // ── 1) 무한모드: MissionSystem (우선 확인) ──
            if (MissionSystem.Instance != null)
            {
                var sm = MissionSystem.Instance.CurrentMission;
                if (sm != null && !sm.IsComplete)
                {
                    Debug.Log($"[DroneBlockSystem] 미션 감지: type={sm.type}, target1={sm.targetGemType}, target2={sm.targetGemType2}, progress={sm.currentCount}/{sm.targetCount}");
                    switch (sm.type)
                    {
                        case SurvivalMissionType.CollectGem:
                            if (sm.targetGemType != GemType.None)
                                targets.Add(sm.targetGemType);
                            break;
                        case SurvivalMissionType.ProcessGem:
                            if (sm.targetGemType != GemType.None)
                                targets.Add(sm.targetGemType);
                            break;
                        case SurvivalMissionType.CollectMulti:
                            if (sm.targetGemType != GemType.None) targets.Add(sm.targetGemType);
                            if (sm.targetGemType2 != GemType.None) targets.Add(sm.targetGemType2);
                            break;
                        default:
                            Debug.Log($"[DroneBlockSystem] 색상 타겟 없는 미션 타입: {sm.type}");
                            break;
                    }
                }
                else
                {
                    Debug.Log($"[DroneBlockSystem] MissionSystem: CurrentMission={sm != null}, IsComplete={sm?.IsComplete}");
                }
            }
            else
            {
                Debug.Log("[DroneBlockSystem] MissionSystem.Instance == null");
            }

            // ── 2) 스테이지모드: StageManager ──
            if (stageManager != null)
            {
                MissionProgress[] progress = stageManager.GetMissionProgress();
                if (progress != null)
                {
                    foreach (var mp in progress)
                    {
                        if (mp == null || mp.isComplete || mp.mission == null) continue;

                        if (mp.mission.type == MissionType.CollectGem && mp.mission.targetGemType != GemType.None)
                            targets.Add(mp.mission.targetGemType);
                        else if (mp.mission.type == MissionType.ProcessGem && mp.mission.targetGemType != GemType.None)
                            targets.Add(mp.mission.targetGemType);
                        else if (mp.mission.type == MissionType.CollectMultiGem)
                        {
                            if (mp.mission.targetGemType != GemType.None) targets.Add(mp.mission.targetGemType);
                            if (mp.mission.secondaryGemType != GemType.None) targets.Add(mp.mission.secondaryGemType);
                        }
                    }
                }
            }

            if (targets.Count > 0)
                Debug.Log($"[DroneBlockSystem] ★ 미션 타겟 색상: {string.Join(", ", targets)}");
            else
                Debug.Log("[DroneBlockSystem] ⚠ 미션 타겟 색상 없음 (비색상 미션이거나 미션 미활성)");

            return targets;
        }

        /// <summary>
        /// 현재 활성 미션에서 비닐/적군/시한폭탄 관련 미션이 있는지 확인
        /// </summary>
        private bool HasObstacleMission()
        {
            // 스테이지모드
            if (stageManager != null)
            {
                MissionProgress[] progress = stageManager.GetMissionProgress();
                if (progress != null)
                {
                    foreach (var mp in progress)
                    {
                        if (mp == null || mp.isComplete || mp.mission == null) continue;
                        if (mp.mission.type == MissionType.RemoveVinyl ||
                            mp.mission.type == MissionType.RemoveDoubleVinyl ||
                            mp.mission.type == MissionType.RemoveEnemy)
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 우선순위 기반 타겟 선택 (미션 연동)
        /// 0. 미션 타겟 최우선 (미션에서 요구하는 색상/장애물)
        /// 1. 장애물 (FixedBlock > TimeBomb > 비닐 > 사슬 > 적군)
        /// 2. 높은 티어 블록
        /// 3. 기본 블록 (Normal 티어, 특수 아님) — 미션 색상 > 랜덤
        /// 4. 특수 블록 (희귀도 낮은 순)
        /// </summary>
        private HexBlock FindTargetBlock(HexBlock droneBlock)
        {
            List<HexBlock> candidates = new List<HexBlock>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block == droneBlock) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) return null;

            // 미션 타겟 정보 수집
            HashSet<GemType> missionGemTargets = GetMissionTargetGemTypes();
            bool hasObstacleMission = HasObstacleMission();

            HexCoord origin = new HexCoord(0, 0);

            candidates.Sort((a, b) =>
            {
                int scoreA = GetTargetPriority(a, missionGemTargets, hasObstacleMission);
                int scoreB = GetTargetPriority(b, missionGemTargets, hasObstacleMission);
                if (scoreA != scoreB) return scoreB.CompareTo(scoreA);

                // 동일 우선순위: 외곽 우선
                int distA = a.Coord.DistanceTo(origin);
                int distB = b.Coord.DistanceTo(origin);
                if (distA != distB) return distB.CompareTo(distA);

                // 거리도 같으면: 아래쪽 우선 (r값 큰 것 = 화면 아래)
                return b.Coord.r.CompareTo(a.Coord.r);
            });

            // 최상위 우선순위가 미션 기본 블록(100) 또는 일반 기본 블록(50)이면
            // 같은 우선순위 블록들 중 랜덤 선택
            int topPriority = GetTargetPriority(candidates[0], missionGemTargets, hasObstacleMission);

            // 상위 3개 후보 로그 (디버깅용)
            int logCount = Mathf.Min(3, candidates.Count);
            for (int i = 0; i < logCount; i++)
            {
                var c = candidates[i];
                int p = GetTargetPriority(c, missionGemTargets, hasObstacleMission);
                Debug.Log($"[DroneBlockSystem] 후보[{i}] {c.Coord} gem={c.Data.gemType} tier={c.Data.tier} special={c.Data.specialType} → priority={p}");
            }

            if (topPriority == 100 || topPriority == 50)
            {
                List<HexBlock> sameGroup = new List<HexBlock>();
                foreach (var c in candidates)
                {
                    if (GetTargetPriority(c, missionGemTargets, hasObstacleMission) == topPriority)
                        sameGroup.Add(c);
                }
                HexBlock chosen = sameGroup[Random.Range(0, sameGroup.Count)];
                Debug.Log($"[DroneBlockSystem] ★ 타겟 선택 (priority={topPriority}, 후보={sameGroup.Count}개): {chosen.Coord} ({chosen.Data.gemType})");
                return chosen;
            }

            Debug.Log($"[DroneBlockSystem] ★ 타겟 선택 (priority={topPriority}): {candidates[0].Coord} ({candidates[0].Data.gemType}, special={candidates[0].Data.specialType})");
            return candidates[0];
        }

        /// <summary>
        /// 미션 연동 우선순위 점수 계산
        /// </summary>
        private int GetTargetPriority(HexBlock block, HashSet<GemType> missionGemTargets, bool hasObstacleMission)
        {
            var data = block.Data;
            if (data == null) return 0;

            // === 1순위: 장애물 (미션에서 요구하면 추가 부스트) ===
            if (data.specialType == SpecialBlockType.FixedBlock) return hasObstacleMission ? 210 : 200;
            if (data.specialType == SpecialBlockType.TimeBomb) return 190;
            if (data.vinylLayer > 0) return hasObstacleMission ? 185 : 180;
            if (data.hasChain) return 170;
            if (data.enemyType != EnemyType.None) return hasObstacleMission ? 165 : 160;

            // === 2순위: 높은 티어 블록 (미션 색상이면 부스트) ===
            bool isMissionColor = missionGemTargets.Count > 0 && missionGemTargets.Contains(data.gemType);

            if (data.tier >= BlockTier.ProcessedGem) return isMissionColor ? 145 : 140;
            if (data.tier >= BlockTier.Tier3) return isMissionColor ? 135 : 130;
            if (data.tier >= BlockTier.Tier2) return isMissionColor ? 125 : 120;
            if (data.tier >= BlockTier.Tier1) return isMissionColor ? 115 : 110;

            // === 3순위: 미션 색상 기본 블록 (Normal 티어, 특수 아님) — 최우선 타겟! ===
            if (data.specialType == SpecialBlockType.None && isMissionColor) return 100;

            // === 4순위: 일반 기본 블록 (Normal 티어, 특수 아님) ===
            if (data.specialType == SpecialBlockType.None) return 50;

            // === 5순위: 특수 블록 (희귀도 낮은 순, 기본 블록 없을 때만 타겟됨) ===
            if (data.specialType == SpecialBlockType.Drill) return 14;
            if (data.specialType == SpecialBlockType.Bomb) return 12;
            if (data.specialType == SpecialBlockType.Rainbow) return 8;
            if (data.specialType == SpecialBlockType.XBlock) return 6;

            // 기타 특수 블록
            return 4;
        }

        // ============================================================
        // 타격 효과 적용 (1데미지)
        // ============================================================

        /// <summary>
        /// 드론 타격: 1데미지 적용
        /// - 비닐: 1겹 제거
        /// - 사슬: 제거
        /// - 방패: 1 감소
        /// - Normal 기본 블록: 파괴 (ClearData)
        /// - 높은 티어: 1단계 강등
        /// - 특수 블록: 발동 없이 파괴
        /// </summary>
        private void ApplyDroneStrike(HexBlock target)
        {
            if (target == null || target.Data == null) return;

            var data = target.Data;

            // 비닐이 있으면 1겹 제거
            if (data.vinylLayer > 0)
            {
                data.vinylLayer--;
                target.SetBlockData(data);
                Debug.Log($"[DroneBlockSystem] 비닐 제거 → 남은 겹: {data.vinylLayer}");
                return;
            }

            // 사슬 제거
            if (data.hasChain)
            {
                data.hasChain = false;
                target.SetBlockData(data);
                Debug.Log("[DroneBlockSystem] 사슬 제거");
                return;
            }

            // 방패 감소
            if (data.enemyShieldCount > 0)
            {
                data.enemyShieldCount--;
                target.SetBlockData(data);
                Debug.Log($"[DroneBlockSystem] 방패 감소 → 남은: {data.enemyShieldCount}");
                return;
            }

            // Normal 티어면 파괴
            if (data.tier == BlockTier.Normal)
            {
                target.ClearData();
                Debug.Log("[DroneBlockSystem] Normal 블록 파괴");
                return;
            }

            // 그 외: 티어 1단계 강등
            BlockTier prevTier = data.tier;
            data.tier = (BlockTier)((int)data.tier - 1);
            target.SetBlockData(data);
            Debug.Log($"[DroneBlockSystem] 티어 강등: {prevTier} → {data.tier}");
        }

        // ============================================================
        // VFX
        // ============================================================

        /// <summary>Pre-Fire 압축 애니메이션</summary>
        private IEnumerator PreFireCompression(HexBlock block)
        {
            if (block == null) yield break;
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector3 origScale = rt.localScale;
            float duration = 0.12f;
            float t = 0f;

            // 축소 → 확대
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = t / duration;
                float scale;
                if (p < 0.5f)
                    scale = Mathf.Lerp(1f, 0.78f, p * 2f);
                else
                    scale = Mathf.Lerp(0.78f, 1.18f, (p - 0.5f) * 2f);
                rt.localScale = origScale * scale;
                yield return null;
            }
            rt.localScale = origScale;
        }

        /// <summary>
        /// 드론 비행 이펙트: 이륙 → 호버링 → 급강하 타겟 돌진
        /// BombBlockSystem과 동일하게 transform.position 직접 사용
        /// </summary>
        private IEnumerator DroneFlyEffect(Vector3 startPos, Vector3 targetPos, Color color)
        {
            if (effectParent == null) yield break;

            // ---- 드론 비행체 구성 ----
            GameObject droneObj = new GameObject("DroneFlyer");
            droneObj.transform.SetParent(effectParent, false);
            RectTransform droneRt = droneObj.AddComponent<RectTransform>();
            droneRt.sizeDelta = new Vector2(100f, 100f);

            // 드론 본체 아이콘
            Image droneImg = droneObj.AddComponent<Image>();
            droneImg.sprite = GetDroneIconSprite();
            droneImg.type = Image.Type.Simple;
            droneImg.preserveAspect = true;
            droneImg.color = Color.white;
            droneImg.raycastTarget = false;

            // 그림자
            GameObject shadowObj = new GameObject("DroneShadow");
            shadowObj.transform.SetParent(effectParent, false);
            RectTransform shadowRt = shadowObj.AddComponent<RectTransform>();
            shadowRt.sizeDelta = new Vector2(70f, 24f);
            Image shadowImg = shadowObj.AddComponent<Image>();
            shadowImg.color = new Color(0f, 0f, 0f, 0.2f);
            shadowImg.raycastTarget = false;

            // 좌측 프로펠러 블러
            GameObject propL = new GameObject("PropL");
            propL.transform.SetParent(droneObj.transform, false);
            RectTransform propLRt = propL.AddComponent<RectTransform>();
            propLRt.anchoredPosition = new Vector2(-32f, 16f);
            propLRt.sizeDelta = new Vector2(40f, 8f);
            Image propLImg = propL.AddComponent<Image>();
            propLImg.color = new Color(0.7f, 0.7f, 0.8f, 0.6f);
            propLImg.raycastTarget = false;

            // 우측 프로펠러 블러
            GameObject propR = new GameObject("PropR");
            propR.transform.SetParent(droneObj.transform, false);
            RectTransform propRRt = propR.AddComponent<RectTransform>();
            propRRt.anchoredPosition = new Vector2(32f, 16f);
            propRRt.sizeDelta = new Vector2(40f, 8f);
            Image propRImg = propR.AddComponent<Image>();
            propRImg.color = new Color(0.7f, 0.7f, 0.8f, 0.6f);
            propRImg.raycastTarget = false;

            // ---- 이륙 위치 (위로 60px) ----
            Vector3 riseOffset = new Vector3(0f, 60f, 0f);
            Vector3 riseEndPos = startPos + riseOffset;

            // ---- Phase 1: 이륙 (0.264초) ----
            float riseDuration = 0.264f;
            float t = 0f;
            droneRt.localScale = Vector3.zero;
            droneObj.transform.position = startPos;

            while (t < riseDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / riseDuration);
                float eased = 1f - (1f - p) * (1f - p);
                droneObj.transform.position = Vector3.Lerp(startPos, riseEndPos, eased);

                float s = eased < 0.7f
                    ? Mathf.Lerp(0f, 1.2f, eased / 0.7f)
                    : Mathf.Lerp(1.2f, 1f, (eased - 0.7f) / 0.3f);
                droneRt.localScale = new Vector3(s, s, 1f);

                // 프로펠러 회전
                float propRot = t * 2400f;
                propLRt.localRotation = Quaternion.Euler(0, 0, propRot);
                propRRt.localRotation = Quaternion.Euler(0, 0, -propRot);

                // 그림자
                shadowObj.transform.position = startPos + new Vector3(2f, -5f, 0f);
                float shadowScale = Mathf.Lerp(1f, 0.6f, eased);
                shadowRt.localScale = new Vector3(shadowScale, shadowScale, 1f);
                shadowImg.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.2f, 0.08f, eased));

                yield return null;
            }

            // ---- Phase 2: 호버링 (0.198초) ----
            float hoverDuration = 0.198f;
            t = 0f;
            Vector3 hoverCenter = riseEndPos;

            while (t < hoverDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / hoverDuration);
                float wobbleX = Mathf.Sin(p * Mathf.PI * 4f) * 4f;
                float wobbleY = Mathf.Sin(p * Mathf.PI * 6f) * 2f;
                droneObj.transform.position = hoverCenter + new Vector3(wobbleX, wobbleY, 0f);

                float propRot = (riseDuration + t) * 2400f;
                propLRt.localRotation = Quaternion.Euler(0, 0, propRot);
                propRRt.localRotation = Quaternion.Euler(0, 0, -propRot);

                droneRt.localRotation = Quaternion.Euler(0, 0, wobbleX * 0.5f);

                yield return null;
            }

            // ---- Phase 3: 급강하 비행 (0.396초) ----
            float flyDuration = 0.396f;
            t = 0f;
            Vector3 flyStart = droneObj.transform.position;
            Vector3 flyEnd = targetPos;
            Vector3 prevWorldPos = flyStart;

            while (t < flyDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / flyDuration);
                float eased = p * p * p * p; // EaseInQuart
                float smoothP = p < 0.4f
                    ? p * p * (3f - 2f * p)
                    : Mathf.Lerp(0.352f, 1f, (p - 0.4f) / 0.6f * ((p - 0.4f) / 0.6f));
                Vector3 pos = Vector3.Lerp(flyStart, flyEnd, Mathf.Max(eased, smoothP));
                // 포물선 아크
                float arc = 80f * 4f * p * (1f - p) * (1f - p);
                pos.y += arc;
                droneObj.transform.position = pos;

                // 비행 방향 기울기
                Vector3 moveDir = pos - prevWorldPos;
                if (moveDir.sqrMagnitude > 0.01f)
                {
                    float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
                    float tilt = Mathf.Clamp(angle - 90f, -35f, 35f);
                    droneRt.localRotation = Quaternion.Euler(0, 0, tilt);
                }

                // 프로펠러 고속 회전
                float propRot = (riseDuration + hoverDuration + t) * 3000f;
                propLRt.localRotation = Quaternion.Euler(0, 0, propRot);
                propRRt.localRotation = Quaternion.Euler(0, 0, -propRot);

                // 그림자 타겟 수렴
                shadowObj.transform.position = new Vector3(pos.x + 2f, targetPos.y - 5f, 0f);
                float shadowAlpha = Mathf.Lerp(0.05f, 0.2f, p);
                float shadowS = Mathf.Lerp(0.5f, 1f, p);
                shadowRt.localScale = new Vector3(shadowS, shadowS, 1f);
                shadowImg.color = new Color(0f, 0f, 0f, shadowAlpha);

                // 트레일 파티클
                float trailChance = Mathf.Lerp(0.15f, 0.6f, p);
                if (Random.value < trailChance)
                    SpawnTrailParticle(pos, color, Mathf.Lerp(6f, 14f, p));

                prevWorldPos = pos;
                yield return null;
            }

            // 착탄 속도선 (3줄)
            for (int i = 0; i < 3; i++)
            {
                Vector3 lineStart = flyEnd + (Vector3)(Random.insideUnitCircle.normalized * Random.Range(20f, 40f));
                SpawnSpeedLine(lineStart, flyEnd, color);
            }

            Destroy(shadowObj);
            Destroy(droneObj);
        }

        // ============================================================
        // 공개 비행 이펙트 (합성 시스템 등 외부에서 호출)
        // ============================================================

        /// <summary>
        /// 외부에서 호출 가능한 드론 비행 이펙트 (이륙→호버→급강하 전체)
        /// SpecialBlockComboSystem 등에서 합성 드론 비행에 사용
        /// </summary>
        public IEnumerator PlayDroneFlyEffect(Vector3 startPos, Vector3 targetPos, Color color)
        {
            SetupEffectParent();
            yield return StartCoroutine(DroneFlyEffect(startPos, targetPos, color));
        }

        /// <summary>
        /// 딜레이 후 드론 비행 이펙트 (동시 발사 시 시간차용)
        /// </summary>
        public IEnumerator PlayDroneFlyEffectWithDelay(Vector3 startPos, Vector3 targetPos, Color color, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            SetupEffectParent();
            yield return StartCoroutine(DroneFlyEffect(startPos, targetPos, color));
        }

        /// <summary>트레일 파티클 생성 (transform.position 기반)</summary>
        private void SpawnTrailParticle(Vector3 worldPos, Color color, float size)
        {
            if (effectParent == null) return;
            GameObject trail = new GameObject("DroneTrail");
            trail.transform.SetParent(effectParent, false);
            RectTransform trailRt = trail.AddComponent<RectTransform>();
            trailRt.sizeDelta = new Vector2(size, size);
            trail.transform.position = worldPos + (Vector3)(Random.insideUnitCircle * 3f);
            Image trailImg = trail.AddComponent<Image>();
            trailImg.color = new Color(color.r, color.g, color.b, 0.6f);
            trailImg.raycastTarget = false;
            StartCoroutine(FadeAndShrink(trail, Random.Range(0.15f, 0.3f)));
        }

        /// <summary>속도선 이펙트 (transform.position 기반)</summary>
        private void SpawnSpeedLine(Vector3 from, Vector3 to, Color color)
        {
            if (effectParent == null) return;
            GameObject line = new GameObject("SpeedLine");
            line.transform.SetParent(effectParent, false);
            RectTransform lineRt = line.AddComponent<RectTransform>();
            Vector3 dir = (to - from);
            float length = dir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            line.transform.position = (from + to) / 2f;
            lineRt.sizeDelta = new Vector2(length, 2f);
            lineRt.localRotation = Quaternion.Euler(0, 0, angle);
            Image lineImg = line.AddComponent<Image>();
            lineImg.color = new Color(1f, 1f, 1f, 0.7f);
            lineImg.raycastTarget = false;
            StartCoroutine(FadeAndDestroy(line, 0.12f));
        }

        /// <summary>드론 타격 이펙트: 임팩트 웨이브 + 플래시 + 파편 + 블록 흔들림</summary>
        private IEnumerator DroneStrikeEffect(HexBlock target, Color color)
        {
            if (target == null) yield break;

            Vector3 targetWorldPos = target.transform.position;

            // 1. 임팩트 플래시 (블록의 자식으로)
            GameObject flashObj = new GameObject("DroneFlash");
            flashObj.transform.SetParent(target.transform, false);
            RectTransform flashRt = flashObj.AddComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = new Vector2(-8f, -8f);
            flashRt.offsetMax = new Vector2(8f, 8f);
            Image flashImg = flashObj.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 1f);
            flashImg.raycastTarget = false;

            // 2. 임팩트 웨이브 링 (transform.position 기반)
            StartCoroutine(ImpactWaveRing(targetWorldPos, color, 60f, 0.25f));
            StartCoroutine(ImpactWaveRing(targetWorldPos, Color.white, 40f, 0.18f));

            // 3. 타겟 블록 흔들림
            StartCoroutine(BlockShake(target, 6f, 0.15f));

            // 플래시 페이드아웃
            float flashDuration = 0.12f;
            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                float p = t / flashDuration;
                if (flashImg != null)
                {
                    Color flashColor = Color.Lerp(Color.white, color, p * 0.5f);
                    flashColor.a = 1f * (1f - p * p);
                    flashImg.color = flashColor;
                }
                yield return null;
            }
            if (flashObj != null) Destroy(flashObj);

            // 4. 파편 방출 (6~10개, 중력 적용)
            int debrisCount = Random.Range(6, 11);
            for (int i = 0; i < debrisCount; i++)
            {
                if (effectParent == null) break;
                SpawnImpactDebris(targetWorldPos, color);
            }

            // 5. 스파크 방출 (4~6개)
            int sparkCount = Random.Range(4, 7);
            for (int i = 0; i < sparkCount; i++)
            {
                if (effectParent == null) break;
                SpawnImpactSpark(targetWorldPos, color);
            }

            yield return new WaitForSeconds(0.08f);
        }

        /// <summary>임팩트 웨이브 링 (transform.position 기반)</summary>
        private IEnumerator ImpactWaveRing(Vector3 worldPos, Color color, float maxSize, float duration)
        {
            if (effectParent == null) yield break;

            GameObject ringObj = new GameObject("DroneImpactRing");
            ringObj.transform.SetParent(effectParent, false);
            ringObj.transform.position = worldPos;
            RectTransform ringRt = ringObj.AddComponent<RectTransform>();
            ringRt.sizeDelta = new Vector2(10f, 10f);
            Image ringImg = ringObj.AddComponent<Image>();
            ringImg.color = new Color(color.r, color.g, color.b, 0.7f);
            ringImg.raycastTarget = false;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - (1f - p) * (1f - p);
                float size = Mathf.Lerp(10f, maxSize, eased);
                ringRt.sizeDelta = new Vector2(size, size);
                ringImg.color = new Color(color.r, color.g, color.b, 0.7f * (1f - p));
                yield return null;
            }
            Destroy(ringObj);
        }

        /// <summary>타겟 블록 흔들림</summary>
        private IEnumerator BlockShake(HexBlock block, float intensity, float duration)
        {
            if (block == null) yield break;
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt == null) yield break;

            Vector2 origPos = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / duration);
                float ox = Random.Range(-intensity, intensity) * decay;
                float oy = Random.Range(-intensity, intensity) * decay;
                rt.anchoredPosition = origPos + new Vector2(ox, oy);
                yield return null;
            }
            rt.anchoredPosition = origPos;
        }

        /// <summary>임팩트 파편 (transform.position 기반, 중력 적용)</summary>
        private void SpawnImpactDebris(Vector3 worldPos, Color color)
        {
            if (effectParent == null) return;
            GameObject debris = new GameObject("DroneDebris");
            debris.transform.SetParent(effectParent, false);
            debris.transform.position = worldPos;
            RectTransform debrisRt = debris.AddComponent<RectTransform>();
            float size = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax);
            debrisRt.sizeDelta = new Vector2(size, size);
            Image debrisImg = debris.AddComponent<Image>();
            debrisImg.color = VisualConstants.Brighten(color);
            debrisImg.raycastTarget = false;

            Vector2 dir = Random.insideUnitCircle.normalized;
            float speed = Random.Range(VisualConstants.DebrisBaseSpeedMin, VisualConstants.DebrisBaseSpeedMax);
            float lifetime = Random.Range(VisualConstants.DebrisLifetimeMin, VisualConstants.DebrisLifetimeMax);
            float rotSpeed = Random.Range(VisualConstants.DebrisRotSpeedMin, VisualConstants.DebrisRotSpeedMax);
            StartCoroutine(AnimateDebrisGravity(debris, dir, speed, rotSpeed, lifetime));
        }

        /// <summary>임팩트 스파크 (transform.position 기반)</summary>
        private void SpawnImpactSpark(Vector3 worldPos, Color color)
        {
            if (effectParent == null) return;
            GameObject spark = new GameObject("DroneSpark");
            spark.transform.SetParent(effectParent, false);
            spark.transform.position = worldPos;
            RectTransform sparkRt = spark.AddComponent<RectTransform>();
            float size = Random.Range(VisualConstants.SparkSmallSizeMin, VisualConstants.SparkSmallSizeMax);
            sparkRt.sizeDelta = new Vector2(size, size);
            Image sparkImg = spark.AddComponent<Image>();
            sparkImg.color = new Color(1f, 1f, 0.85f, 1f);
            sparkImg.raycastTarget = false;

            Vector2 dir = Random.insideUnitCircle.normalized;
            float speed = Random.Range(VisualConstants.SparkSmallSpeedMin, VisualConstants.SparkSmallSpeedMax);
            float lifetime = Random.Range(VisualConstants.SparkSmallLifetimeMin, VisualConstants.SparkSmallLifetimeMax);
            StartCoroutine(AnimateSparkDecel(spark, dir, speed, lifetime));
        }

        /// <summary>파편 애니메이션 (중력 + 회전)</summary>
        private IEnumerator AnimateDebrisGravity(GameObject obj, Vector2 dir, float speed, float rotSpeed, float duration)
        {
            RectTransform rt = obj?.GetComponent<RectTransform>();
            Image img = obj?.GetComponent<Image>();
            if (rt == null || img == null) yield break;

            Vector3 pos = obj.transform.position;
            Vector2 velocity = dir * speed;
            float t = 0f;
            Color origColor = img.color;

            while (t < duration && obj != null)
            {
                t += Time.deltaTime;
                float p = t / duration;
                velocity.y += VisualConstants.DebrisGravity * Time.deltaTime;
                pos += (Vector3)(velocity * Time.deltaTime);
                obj.transform.position = pos;
                rt.localRotation = Quaternion.Euler(0, 0, rotSpeed * t);
                img.color = new Color(origColor.r, origColor.g, origColor.b, origColor.a * (1f - p));
                float s = Mathf.Lerp(1f, 0.15f, p);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (obj != null) Destroy(obj);
        }

        /// <summary>스파크 애니메이션 (감속)</summary>
        private IEnumerator AnimateSparkDecel(GameObject obj, Vector2 dir, float speed, float duration)
        {
            RectTransform rt = obj?.GetComponent<RectTransform>();
            Image img = obj?.GetComponent<Image>();
            if (rt == null || img == null) yield break;

            Vector3 pos = obj.transform.position;
            float t = 0f;
            float currentSpeed = speed;

            while (t < duration && obj != null)
            {
                t += Time.deltaTime;
                float p = t / duration;
                currentSpeed *= VisualConstants.SparkDeceleration;
                pos += (Vector3)(dir * currentSpeed * Time.deltaTime);
                obj.transform.position = pos;
                img.color = new Color(1f, 1f, 0.85f, 1f - p);
                float s = Mathf.Lerp(1f, 0.1f, p);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (obj != null) Destroy(obj);
        }

        /// <summary>페이드 + 축소하며 소멸</summary>
        private IEnumerator FadeAndShrink(GameObject obj, float duration)
        {
            RectTransform rt = obj?.GetComponent<RectTransform>();
            Image img = obj?.GetComponent<Image>();
            if (rt == null || img == null) { if (obj != null) Destroy(obj); yield break; }

            float t = 0f;
            Color origColor = img.color;
            Vector3 origScale = rt.localScale;
            while (t < duration && obj != null)
            {
                t += Time.deltaTime;
                float p = t / duration;
                img.color = new Color(origColor.r, origColor.g, origColor.b, origColor.a * (1f - p));
                rt.localScale = origScale * (1f - p * 0.7f);
                yield return null;
            }
            if (obj != null) Destroy(obj);
        }

        private IEnumerator FadeAndDestroy(GameObject obj, float duration)
        {
            Image img = obj?.GetComponent<Image>();
            if (img == null) { if (obj != null) Destroy(obj); yield break; }

            float t = 0f;
            Color origColor = img.color;
            while (t < duration && obj != null)
            {
                t += Time.deltaTime;
                img.color = new Color(origColor.r, origColor.g, origColor.b, origColor.a * (1f - t / duration));
                yield return null;
            }
            if (obj != null) Destroy(obj);
        }

        /// <summary>화면 흔들림</summary>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            // 다수 특수 블록 동시 발동 시 필드 바운스는 하나만 실행
            bool isOwner = VisualConstants.TryBeginScreenShake();
            if (!isOwner) yield break;

            if (hexGrid == null) { VisualConstants.EndScreenShake(); yield break; }
            Transform gridTrans = hexGrid.transform;
            Vector3 origPos = gridTrans.localPosition;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float decay = 1f - (t / duration);
                float offsetX = Random.Range(-intensity, intensity) * decay;
                float offsetY = Random.Range(-intensity, intensity) * decay;
                gridTrans.localPosition = origPos + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }
            gridTrans.localPosition = origPos;
            VisualConstants.EndScreenShake();
        }

        // ============================================================
        // 드론 아이콘 스프라이트 (프로시저럴)
        // ============================================================

        public static Sprite GetDroneIconSprite()
        {
            if (droneIconSprite == null)
                droneIconSprite = CreateDroneSprite(256);
            return droneIconSprite;
        }

        /// <summary>
        /// 드론 아이콘 프로시저럴 생성 - 중앙 본체 + 좌우 날개(프로펠러)
        /// </summary>
        private static Sprite CreateDroneSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Vector2 center = new Vector2(size * 0.5f, size * 0.45f);
            float bodyRadius = size * 0.15f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x, y);

                    // 중앙 본체 (민트 그린)
                    float distCenter = Vector2.Distance(p, center);
                    if (distCenter < bodyRadius)
                    {
                        float edge = distCenter / bodyRadius;
                        float hl = Mathf.Pow(1f - edge, 3f) * 0.3f;
                        Color c = new Color(0.55f + hl, 0.88f + hl * 0.05f, 0.82f + hl * 0.05f, 1f);
                        float aa = Mathf.Clamp01((bodyRadius - distCenter) * 2f);
                        c.a = aa;
                        pixels[y * size + x] = c;
                    }

                    // 눈 (어두운 원 2개)
                    Vector2 leftEye = center + new Vector2(-bodyRadius * 0.4f, bodyRadius * 0.2f);
                    Vector2 rightEye = center + new Vector2(bodyRadius * 0.4f, bodyRadius * 0.2f);
                    float eyeRadius = bodyRadius * 0.2f;
                    if (Vector2.Distance(p, leftEye) < eyeRadius || Vector2.Distance(p, rightEye) < eyeRadius)
                    {
                        pixels[y * size + x] = new Color(0.2f, 0.35f, 0.33f, 1f);
                    }

                    // 좌측 날개 (파스텔 스카이블루)
                    Vector2 leftWing = center + new Vector2(-size * 0.25f, size * 0.05f);
                    float wingW = size * 0.18f;
                    float wingH = size * 0.06f;
                    float lwx = (p.x - leftWing.x) / wingW;
                    float lwy = (p.y - leftWing.y) / wingH;
                    if (lwx * lwx + lwy * lwy < 1f)
                    {
                        float we = Mathf.Sqrt(lwx * lwx + lwy * lwy);
                        float wa = Mathf.Clamp01((1f - we) * 3f);
                        Color wc = new Color(0.65f, 0.82f, 0.95f, wa);
                        if (pixels[y * size + x].a < wa)
                            pixels[y * size + x] = wc;
                    }

                    // 우측 날개 (파스텔 스카이블루)
                    Vector2 rightWing = center + new Vector2(size * 0.25f, size * 0.05f);
                    float rwx = (p.x - rightWing.x) / wingW;
                    float rwy = (p.y - rightWing.y) / wingH;
                    if (rwx * rwx + rwy * rwy < 1f)
                    {
                        float we = Mathf.Sqrt(rwx * rwx + rwy * rwy);
                        float wa = Mathf.Clamp01((1f - we) * 3f);
                        Color wc = new Color(0.65f, 0.82f, 0.95f, wa);
                        if (pixels[y * size + x].a < wa)
                            pixels[y * size + x] = wc;
                    }

                    // 프로펠러 (좌우 날개 위, 작은 라인)
                    Vector2 leftProp = leftWing + new Vector2(0f, wingH + size * 0.03f);
                    Vector2 rightProp = rightWing + new Vector2(0f, wingH + size * 0.03f);
                    float propW = size * 0.12f;
                    float propH = size * 0.015f;
                    if (Mathf.Abs(p.x - leftProp.x) < propW && Mathf.Abs(p.y - leftProp.y) < propH)
                    {
                        float pa = Mathf.Clamp01((propH - Mathf.Abs(p.y - leftProp.y)) * 4f);
                        Color pc = new Color(0.7f, 0.7f, 0.75f, pa * 0.7f);
                        if (pixels[y * size + x].a < pa * 0.7f)
                            pixels[y * size + x] = pc;
                    }
                    if (Mathf.Abs(p.x - rightProp.x) < propW && Mathf.Abs(p.y - rightProp.y) < propH)
                    {
                        float pa = Mathf.Clamp01((propH - Mathf.Abs(p.y - rightProp.y)) * 4f);
                        Color pc = new Color(0.7f, 0.7f, 0.75f, pa * 0.7f);
                        if (pixels[y * size + x].a < pa * 0.7f)
                            pixels[y * size + x] = pc;
                    }

                    // 다리 (하단 좌우, 작은 막대)
                    float legW = size * 0.02f;
                    float legStartY = center.y - bodyRadius;
                    float legEndY = center.y - bodyRadius - size * 0.1f;
                    if (Mathf.Abs(p.x - (center.x - bodyRadius * 0.5f)) < legW &&
                        p.y > legEndY && p.y < legStartY)
                    {
                        pixels[y * size + x] = new Color(0.5f, 0.5f, 0.55f, 0.8f);
                    }
                    if (Mathf.Abs(p.x - (center.x + bodyRadius * 0.5f)) < legW &&
                        p.y > legEndY && p.y < legStartY)
                    {
                        pixels[y * size + x] = new Color(0.5f, 0.5f, 0.55f, 0.8f);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}

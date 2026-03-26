using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

            // ★ 우선순위 타겟팅: GoblinBomb → 궁수 → 방패 → 갑옷 → 일반 → 스마트 블록
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var priorityTarget = FindPriorityGoblinTarget();
                if (priorityTarget != null)
                {
                    Debug.Log($"[DroneBlockSystem] 우선순위 타겟: {priorityTarget.Value.type} at {priorityTarget.Value.coord}");
                    ActivateDroneToGoblin(droneBlock, priorityTarget.Value.coord);
                    return;
                }
            }

            Debug.Log($"[DroneBlockSystem] Activating drone at {droneBlock.Coord} → 스마트 블록 타겟");
            StartCoroutine(DroneCoroutine(droneBlock, null));
        }

        /// <summary>
        /// 지정된 타겟으로 드론 발동 (콤보 시스템에서 사전 할당된 타겟 사용)
        /// </summary>
        public void ActivateDroneWithTarget(HexBlock droneBlock, HexBlock assignedTarget)
        {
            if (droneBlock == null) return;
            if (droneBlock.Data == null || droneBlock.Data.specialType != SpecialBlockType.Drone)
                return;
            Debug.Log($"[DroneBlockSystem] Activating drone at {droneBlock.Coord} → 사전 할당 타겟 {assignedTarget?.Coord}");
            StartCoroutine(DroneCoroutine(droneBlock, assignedTarget));
        }

        /// <summary>
        /// 고블린 직접 타격: 블록 없이 고블린 위치로 직접 비행하여 1 대미지
        /// 낙하 대미지만 1인 경우 직접 타격이 더 효율적
        /// </summary>
        public void ActivateDroneToGoblin(HexBlock droneBlock, HexCoord goblinPos)
        {
            if (droneBlock == null) return;
            if (droneBlock.Data == null || droneBlock.Data.specialType != SpecialBlockType.Drone)
                return;
            Debug.Log($"[DroneBlockSystem] Activating drone at {droneBlock.Coord} → 고블린 직접 타격 ({goblinPos})");
            StartCoroutine(DroneGoblinStrikeCoroutine(droneBlock, goblinPos));
        }

        /// <summary>
        /// 고블린 직접 타격 코루틴: 블록 파괴 없이 고블린 위치로 비행 → 1 대미지
        /// </summary>
        private IEnumerator DroneGoblinStrikeCoroutine(HexBlock droneBlock, HexCoord goblinPos)
        {
            activeDroneCount++;
            activeBlocks.Add(droneBlock);
            SetupEffectParent();

            bool isFirst = (activeDroneCount == 1);

            Vector3 droneWorldPos = droneBlock.transform.position;
            Color droneColor = GemColors.GetColor(droneBlock.Data.gemType);

            // 1. Pre-Fire 압축 애니메이션
            if (isFirst)
                yield return StartCoroutine(PreFireCompression(droneBlock));

            // 2. 드론 블록 클리어
            droneBlock.ClearData();

            // 3. 고블린 월드 위치 결정
            Vector3 goblinWorldPos = Vector3.zero;
            bool goblinFound = false;
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();
                foreach (var g in aliveGoblins)
                {
                    if (g.isAlive && g.position == goblinPos && g.visualObject != null)
                    {
                        goblinWorldPos = g.visualObject.transform.position;
                        goblinFound = true;
                        break;
                    }
                }
            }

            // 고블린이 사라졌거나 비주얼이 없으면 → 블록 타겟으로 폴백
            if (!goblinFound)
            {
                Debug.Log($"[DroneBlockSystem] 고블린 직접 타격 실패 ({goblinPos}): 고블린 없음 → 블록 타겟으로 전환");
                HexBlock fallbackTarget = FindTargetBlock(droneBlock);
                if (fallbackTarget != null)
                {
                    // 블록 타겟 코루틴으로 전환 (현재 코루틴 종료)
                    activeBlocks.Remove(droneBlock);
                    activeDroneCount--;
                    StartCoroutine(DroneCoroutine(droneBlock, fallbackTarget));
                    yield break;
                }
                // 블록 타겟도 없으면 소멸
                OnDroneComplete?.Invoke(150);
                activeBlocks.Remove(droneBlock);
                activeDroneCount--;
                yield break;
            }

            // 4. 드론 비행
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDroneSound();
            yield return StartCoroutine(DroneFlyEffect(droneWorldPos, goblinWorldPos, droneColor));

            // 5. 화면 흔들림
            if (isFirst)
                StartCoroutine(ScreenShake(3f, 0.15f));

            // 6. 타격 이펙트 (블록 없이 위치 기반)
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopDroneSound();
                AudioManager.Instance.PlayDroneStrikeSound();
            }
            yield return StartCoroutine(DroneGoblinStrikeEffect(goblinWorldPos, droneColor));

            // 7. 고블린 직접 대미지
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                GoblinSystem.Instance.ApplyDamageAtPosition(goblinPos, 1);
                Debug.Log($"[DroneBlockSystem] 고블린 직접 타격: ({goblinPos}) 1 대미지");
            }

            // 8. 점수
            OnDroneComplete?.Invoke(150);

            activeBlocks.Remove(droneBlock);
            activeDroneCount--;
        }

        /// <summary>
        /// 고블린 직접 타격 이펙트: 블록 없이 월드 좌표 기반으로 임팩트 웨이브 + 파편 + 스파크
        /// </summary>
        private IEnumerator DroneGoblinStrikeEffect(Vector3 worldPos, Color color)
        {
            // 1. 임팩트 플래시 (effectParent 자식으로)
            if (effectParent != null)
            {
                GameObject flashObj = new GameObject("DroneGoblinFlash");
                flashObj.transform.SetParent(effectParent, false);
                flashObj.transform.position = worldPos;
                RectTransform flashRt = flashObj.AddComponent<RectTransform>();
                flashRt.sizeDelta = new Vector2(70f, 70f);
                Image flashImg = flashObj.AddComponent<Image>();
                flashImg.color = new Color(1f, 1f, 1f, 1f);
                flashImg.raycastTarget = false;
                Destroy(flashObj, 0.15f);
            }

            // 2. 임팩트 웨이브 링
            StartCoroutine(ImpactWaveRing(worldPos, color, 60f, 0.25f));
            StartCoroutine(ImpactWaveRing(worldPos, Color.white, 40f, 0.18f));

            // 3. 파편 방출
            int debrisCount = Random.Range(6, 11);
            for (int i = 0; i < debrisCount; i++)
            {
                if (effectParent == null) break;
                SpawnImpactDebris(worldPos, color);
            }

            // 4. 스파크 방출
            int sparkCount = Random.Range(4, 7);
            for (int i = 0; i < sparkCount; i++)
            {
                if (effectParent == null) break;
                SpawnImpactSpark(worldPos, color);
            }

            yield return new WaitForSeconds(0.08f);
        }

        private IEnumerator DroneCoroutine(HexBlock droneBlock, HexBlock assignedTarget)
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

            // 3. 타겟 선택: 사전 할당 타겟이 유효하면 사용, 아니면 자동 탐색
            HexBlock target = null;
            if (assignedTarget != null && assignedTarget.Data != null && assignedTarget.Data.gemType != GemType.None)
                target = assignedTarget;
            else
                target = FindTargetBlock(droneBlock);

            if (target == null)
            {
                Debug.Log("[DroneBlockSystem] 타겟 없음, 발동 종료");
                OnDroneComplete?.Invoke(150);
                activeBlocks.Remove(droneBlock);
                activeDroneCount--;
                yield break;
            }

            // 타격 전 타겟 정보 캐싱 (타격 후 Data가 변경될 수 있으므로)
            GemType targetGemType = target.Data != null ? target.Data.gemType : GemType.None;
            SpecialBlockType targetSpecialType = target.Data != null ? target.Data.specialType : SpecialBlockType.None;

            Debug.Log($"[DroneBlockSystem] 타겟 선택: {target.Coord} ({targetGemType}, tier={target.Data?.tier}, special={targetSpecialType})");

            // 4. 드론 비행 이펙트 + 비행 사운드
            Vector3 targetWorldPos = target.transform.position;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDroneSound();
            yield return StartCoroutine(DroneFlyEffect(droneWorldPos, targetWorldPos, droneColor));

            // 5. 화면 흔들림
            if (isFirst)
                StartCoroutine(ScreenShake(3f, 0.15f));

            // 6. 비행 버즈 정지 + 타격 이펙트 + 충돌 파괴 사운드
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopDroneSound();
                AudioManager.Instance.PlayDroneStrikeSound();
            }
            yield return StartCoroutine(DroneStrikeEffect(target, droneColor));

            // 7. 타격 효과 적용 (1데미지)
            HexCoord targetCoord = target.Coord;
            ApplyDroneStrike(target);

            // 7-1. 충돌 대미지: 타겟 위치에 고블린이 있으면 1 추가 대미지
            if (GoblinSystem.Instance != null)
            {
                var goblinSystem = GoblinSystem.Instance;
                var goblinsAtTarget = goblinSystem.GetAliveGoblins();
                foreach (var goblin in goblinsAtTarget)
                {
                    if (goblin.position == targetCoord)
                    {
                        goblinSystem.ApplyDamageAtPosition(targetCoord, 1);
                        Debug.Log($"[DroneBlockSystem] 충돌 대미지: ({targetCoord}) 고블린에게 1 대미지");
                        break; // 같은 위치에 고블린은 1마리
                    }
                }
            }

            // 8. 점수
            int score = 150;
            OnDroneComplete?.Invoke(score);

            // 9. 미션 카운팅: 블록 파괴 시점에 1개씩 개별 보고 (Stage/Infinite 모두 지원)
            if (targetGemType != GemType.None)
                GameManager.Instance?.OnSingleGemDestroyedForMission(targetGemType);

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
        /// 타겟 선택: 고블린 존재 시 스마트 열 기반, 없으면 기존 우선순위 기반
        /// </summary>
        private HexBlock FindTargetBlock(HexBlock droneBlock)
        {
            // 점수 기반 타겟팅 (고블린 유무 모두 대응)
            HexBlock scoreTarget = FindBestTargetByScore(droneBlock);
            if (scoreTarget != null) return scoreTarget;

            // 폴백: 기존 우선순위 기반
            return FindPriorityTargetBlock(droneBlock);
        }

        // ============================================================
        // 점수 기반 드론 타겟팅
        // ============================================================

        /// <summary>몬스터 타입별 기본 점수</summary>
        private static int GetGoblinScore(GoblinData g)
        {
            if (g == null || !g.isAlive) return 0;
            if (g.isBomb) return 5;
            if (g.isShielded) return 4;
            if (g.isArcher) return 3;
            if (g.isArmored) return 2;
            return 1; // Regular
        }

        /// <summary>GoblinBomb 점수 = 6</summary>
        private const int GOBLIN_BOMB_SCORE = 6;

        /// <summary>
        /// 모든 블록에 대해 ScoreForBlock을 계산하고 최고 점수 블록 반환.
        /// 동점 처리: 하단 행 → 외곽(|q| 큰) → 왼쪽(q 작은)
        /// </summary>
        private HexBlock FindBestTargetByScore(HexBlock droneBlock)
        {
            if (hexGrid == null) return null;

            HexBlock bestBlock = null;
            int bestScore = 0;
            int bestR = int.MinValue;   // 하단 행 우선 (r 큰 값)
            int bestAbsQ = -1;          // 외곽 우선 (|q| 큰 값)
            int bestQ = int.MaxValue;   // 왼쪽 우선 (q 작은 값)

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue;
                if (block == droneBlock) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;

                int score = ScoreForBlock(block.Coord);

                // 동점 처리
                bool isBetter = false;
                if (score > bestScore)
                    isBetter = true;
                else if (score == bestScore && score > 0)
                {
                    int r = block.Coord.r;
                    int absQ = Mathf.Abs(block.Coord.q);
                    int q = block.Coord.q;

                    if (r > bestR)
                        isBetter = true;
                    else if (r == bestR && absQ > bestAbsQ)
                        isBetter = true;
                    else if (r == bestR && absQ == bestAbsQ && q < bestQ)
                        isBetter = true;
                }

                if (isBetter)
                {
                    bestBlock = block;
                    bestScore = score;
                    bestR = block.Coord.r;
                    bestAbsQ = Mathf.Abs(block.Coord.q);
                    bestQ = block.Coord.q;
                }
            }

            if (bestBlock != null)
                Debug.Log($"[DroneScore] 최적 타겟: {bestBlock.Coord} 점수={bestScore}");

            return bestBlock;
        }

        /// <summary>
        /// 특정 블록 좌표를 타격했을 때 얻는 총 점수.
        /// 직접 타격 점수 + 낙하 대미지 점수.
        /// </summary>
        public int ScoreForBlock(HexCoord targetCoord)
        {
            int score = 0;

            if (GoblinSystem.Instance == null || hexGrid == null) return 0;

            // GoblinBomb 직접 타격
            HexBlock targetBlock = hexGrid.GetBlock(targetCoord);
            if (targetBlock != null && targetBlock.Data != null && targetBlock.Data.hasGoblinBomb)
                score += GOBLIN_BOMB_SCORE;

            // 직접 타격: 해당 좌표에 몬스터가 있으면
            GoblinData directGoblin = GoblinSystem.Instance.GetGoblinAt(targetCoord);
            if (directGoblin != null)
                score += GetGoblinScore(directGoblin);

            // 낙하 대미지: targetCoord 위쪽 블록이 낙하하면서 맞는 몬스터
            // hex 좌표에서 "위"는 r이 감소하는 방향 (flat-top: (0,-1) 또는 자체 열)
            // 같은 열(q)에서 r < targetCoord.r 인 블록들이 낙하 후보
            var allGoblins = GoblinSystem.Instance.GetAliveGoblins();
            foreach (var g in allGoblins)
            {
                if (g == directGoblin) continue; // 직접 타격은 이미 계산됨
                if (g.isArcher) continue;         // 궁수는 낙하 면역

                // 같은 열에서 targetCoord보다 위(r 작음)에 있는 몬스터는 블록 낙하로 데미지 가능
                if (g.position.q == targetCoord.q && g.position.r < targetCoord.r)
                    score += GetGoblinScore(g);
            }

            return score;
        }

        /// <summary>
        /// 활 고블린 중 최적 타겟 선택 (직접 타격용)
        /// 우선순위: HP 낮은 순 → 왼쪽 열
        /// </summary>
        private GoblinData FindBestArcherTarget()
        {
            if (GoblinSystem.Instance == null) return null;
            var archers = GoblinSystem.Instance.GetAliveGoblins()
                .Where(g => g.isAlive && g.isArcher)
                .ToList();
            if (archers.Count == 0) return null;

            GoblinData best = archers[0];
            for (int i = 1; i < archers.Count; i++)
            {
                var a = archers[i];
                if (a.hp < best.hp || (a.hp == best.hp && a.position.q < best.position.q))
                    best = a;
            }
            return best;
        }

        /// <summary>
        /// 드론 우선순위 타겟팅:
        /// 1순위=GoblinBomb(카운트다운 가장 적은 것), 2순위=궁수, 3순위=방패(방패 본체 좌표),
        /// 4순위=갑옷(HP 낮은 순), 5순위=일반(HP 낮은 순)
        /// </summary>
        private struct DroneTarget
        {
            public string type;
            public HexCoord coord;
        }

        private DroneTarget? FindPriorityGoblinTarget()
        {
            if (GoblinSystem.Instance == null || hexGrid == null) return null;

            var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();
            if (aliveGoblins.Count == 0) return null;

            // 비주얼이 있는(화면에 보이는) 고블린만 타겟 대상
            var visibleGoblins = aliveGoblins.Where(g => g.visualObject != null).ToList();

            // 1순위: GoblinBomb (블록에 설치된 폭탄) — 카운트다운 가장 적은 것
            HexBlock bestBomb = null;
            int bestCountdown = int.MaxValue;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null && block.Data != null && block.Data.hasGoblinBomb)
                {
                    if (block.Data.goblinBombCountdown < bestCountdown)
                    {
                        bestCountdown = block.Data.goblinBombCountdown;
                        bestBomb = block;
                    }
                }
            }
            if (bestBomb != null)
                return new DroneTarget { type = "GoblinBomb", coord = bestBomb.Coord };

            // 2순위: 궁수 고블린
            GoblinData bestArcher = FindBestArcherTarget();
            if (bestArcher != null)
                return new DroneTarget { type = "Archer", coord = bestArcher.position };

            // 3순위: 방패 고블린 — 방패 고블린 본체 좌표로 직접 타격
            GoblinData bestShield = null;
            foreach (var g in visibleGoblins)
            {
                if (!g.isAlive || !g.isShielded) continue;
                if (bestShield == null || g.shieldHp < bestShield.shieldHp
                    || (g.shieldHp == bestShield.shieldHp && g.position.q < bestShield.position.q))
                    bestShield = g;
            }
            if (bestShield != null)
                return new DroneTarget { type = "Shield", coord = bestShield.position };

            // 4순위: 갑옷 고블린 (HP 낮은 순)
            GoblinData bestArmored = null;
            foreach (var g in visibleGoblins)
            {
                if (!g.isAlive || !g.isArmored) continue;
                if (bestArmored == null || g.hp < bestArmored.hp
                    || (g.hp == bestArmored.hp && g.position.q < bestArmored.position.q))
                    bestArmored = g;
            }
            if (bestArmored != null)
                return new DroneTarget { type = "Armored", coord = bestArmored.position };

            // 5순위: 일반 고블린 (HP 낮은 순)
            GoblinData bestRegular = null;
            foreach (var g in visibleGoblins)
            {
                if (!g.isAlive || g.isArcher || g.isArmored || g.isShielded || g.isBomb) continue;
                if (bestRegular == null || g.hp < bestRegular.hp
                    || (g.hp == bestRegular.hp && g.position.q < bestRegular.position.q))
                    bestRegular = g;
            }
            if (bestRegular != null)
                return new DroneTarget { type = "Regular", coord = bestRegular.position };

            return null;
        }

        /// <summary>
        /// 스마트 타겟 선택: 블록 단위 최적 공격 대상 선택
        /// 각 블록별 총 대미지 = 같은 열 고블린 수(낙하) + 충돌 보너스(고블린 위 블록)
        /// 킬 우선순위: 처치 가능 고블린 수 최우선 (hp==1 낙하 사망 + hp<=2 충돌 사망)
        /// 동률: 총 대미지 → 갑옷 고블린 수 → HP 합 → 가장자리 → 왼쪽 열
        /// </summary>
        private HexBlock FindSmartTargetBlock(HexBlock droneBlock)
        {
            var goblinSystem = GoblinSystem.Instance;
            // 모든 고블린 포함 (활 고블린도 열 우선순위 평가에 포함)
            var allGoblins = goblinSystem.GetAliveGoblins();
            // 낙하 대미지 대상은 활 고블린 제외 (낙하 면역)
            var fallTargets = allGoblins.Where(g => !g.isArcher).ToList();
            if (allGoblins.Count == 0) return null;

            // 열(q)별 고블린 그룹화 (낙하 대상만)
            var columnGoblins = new Dictionary<int, List<GoblinData>>();
            foreach (var goblin in fallTargets)
            {
                int q = goblin.position.q;
                if (!columnGoblins.ContainsKey(q))
                    columnGoblins[q] = new List<GoblinData>();
                columnGoblins[q].Add(goblin);
            }

            // 열(q)별 활 고블린 수 (타겟팅 우선순위용)
            var archerByCol = new Dictionary<int, int>();
            foreach (var goblin in allGoblins)
            {
                if (!goblin.isArcher) continue;
                int q = goblin.position.q;
                archerByCol[q] = archerByCol.ContainsKey(q) ? archerByCol[q] + 1 : 1;
            }

            // 고블린이 있는 열 집합 (낙하 대상 + 활 고블린 모두)
            var columnsWithGoblins = new HashSet<int>(columnGoblins.Keys);
            foreach (int q in archerByCol.Keys) columnsWithGoblins.Add(q);

            // 열(q)별 방패 고블린 수 (모든 고블린 대상)
            var shieldByCol = new Dictionary<int, int>();
            foreach (var goblin in allGoblins)
            {
                if (!goblin.isShielded) continue;
                int q = goblin.position.q;
                shieldByCol[q] = shieldByCol.ContainsKey(q) ? shieldByCol[q] + 1 : 1;
            }

            // 열(q)별 갑옷 고블린 수
            var armoredByCol = new Dictionary<int, int>();
            foreach (var goblin in allGoblins)
            {
                if (!goblin.isArmored) continue;
                int q = goblin.position.q;
                armoredByCol[q] = armoredByCol.ContainsKey(q) ? armoredByCol[q] + 1 : 1;
            }

            // 모든 후보 블록에 대해 개별 평가
            HexBlock bestBlock = null;
            int bestShieldInCol = -1;
            int bestShieldDirectHit = -1;
            int bestArcherCount = -1;
            int bestArmoredCount = -1;
            int bestKillCount = -1;
            int bestTotalDamage = -1;
            int bestHpSum = -1;
            int bestEdgeDist = -1;
            int bestQ = int.MaxValue;

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue;
                if (block == droneBlock) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;

                int q = block.Coord.q;
                if (!columnsWithGoblins.Contains(q)) continue;

                var goblinsInCol = columnGoblins.ContainsKey(q) ? columnGoblins[q] : new List<GoblinData>();
                int monsterCount = goblinsInCol.Count;

                // 충돌 판정: 이 블록 위치에 고블린이 있으면 직접 타격
                GoblinData collisionGoblin = null;
                foreach (var goblin in goblinsInCol)
                {
                    if (goblin.position == block.Coord)
                    {
                        collisionGoblin = goblin;
                        break;
                    }
                }
                bool hasCollision = collisionGoblin != null;

                // ★ 방패 직접 타격: 이 블록 위치에 방패 고블린이 있어야만 대미지 가능
                int shieldDirectHit = (hasCollision && collisionGoblin.isShielded) ? 1 : 0;

                // 대미지 계산 (방패 고블린은 낙하 대미지 면역 → 제외)
                int killCount = 0;
                int totalDamage = 0;
                int hpSum = 0;
                foreach (var g in goblinsInCol)
                {
                    if (g.isShielded)
                    {
                        // 방패는 직접 충돌만 대미지 가능
                        if (hasCollision && g.position == block.Coord)
                        {
                            totalDamage += 1; // 충돌 1대미지 (방패에)
                            hpSum += g.hp;
                        }
                        continue; // 낙하 대미지 제외
                    }

                    hpSum += g.hp;
                    if (hasCollision && g.position == block.Coord)
                    {
                        totalDamage += 2; // 충돌 + 낙하
                        if (g.hp <= 2) killCount++;
                    }
                    else
                    {
                        totalDamage += 1; // 낙하만
                        if (g.hp <= 1) killCount++;
                    }
                }

                // 이 열의 방패/활/갑옷 고블린 수
                int shieldInCol = shieldByCol.ContainsKey(q) ? shieldByCol[q] : 0;
                int archerCount = archerByCol.ContainsKey(q) ? archerByCol[q] : 0;
                int armoredCount = armoredByCol.ContainsKey(q) ? armoredByCol[q] : 0;

                int edgeDist = Mathf.Abs(q);

                // ★ 우선순위: 방패열 → 방패직접 → 활 → 갑옷 → killCount → totalDamage → hpSum → edgeDist → q
                bool isBetter = false;
                if (bestBlock == null)
                {
                    isBetter = true;
                }
                else if (shieldInCol > bestShieldInCol)
                {
                    isBetter = true;
                }
                else if (shieldInCol == bestShieldInCol)
                {
                    if (shieldDirectHit > bestShieldDirectHit)
                        isBetter = true;
                    else if (shieldDirectHit == bestShieldDirectHit)
                    {
                        if (archerCount > bestArcherCount)
                            isBetter = true;
                        else if (archerCount == bestArcherCount)
                        {
                            if (armoredCount > bestArmoredCount)
                                isBetter = true;
                            else if (armoredCount == bestArmoredCount)
                            {
                                if (killCount > bestKillCount)
                                    isBetter = true;
                                else if (killCount == bestKillCount)
                                {
                                    if (totalDamage > bestTotalDamage)
                                        isBetter = true;
                                    else if (totalDamage == bestTotalDamage)
                                    {
                                        if (hpSum > bestHpSum)
                                            isBetter = true;
                                        else if (hpSum == bestHpSum)
                                        {
                                            if (edgeDist > bestEdgeDist)
                                                isBetter = true;
                                            else if (edgeDist == bestEdgeDist && q < bestQ)
                                                isBetter = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (isBetter)
                {
                    bestBlock = block;
                    bestShieldInCol = shieldInCol;
                    bestShieldDirectHit = shieldDirectHit;
                    bestArcherCount = archerCount;
                    bestArmoredCount = armoredCount;
                    bestKillCount = killCount;
                    bestTotalDamage = totalDamage;
                    bestHpSum = hpSum;
                    bestEdgeDist = edgeDist;
                    bestQ = q;
                }
            }

            if (bestBlock != null)
                Debug.Log($"[DroneBlockSystem] ★ 스마트 타겟: {bestBlock.Coord} (방패열={bestShieldInCol}, 방패직접={bestShieldDirectHit}, 활={bestArcherCount}, 갑옷={bestArmoredCount}, 킬={bestKillCount}, 대미지={bestTotalDamage})");

            return bestBlock;
        }

        /// <summary>
        /// 기존 우선순위 기반 타겟 선택 (고블린 없을 때 사용)
        /// </summary>
        private HexBlock FindPriorityTargetBlock(HexBlock droneBlock)
        {
            List<HexBlock> candidates = new List<HexBlock>();

            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
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

            // ---- Phase 1: 이륙 (0.264초) ---- 도플러: 1.0 → 1.1 (상승 가속)
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

                // 도플러 피치: 이륙 가속 → 피치 상승
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetDronePitch(Mathf.Lerp(1.0f, 1.1f, eased));

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

            // ---- Phase 2: 호버링 (0.198초) ---- 도플러: 1.1 → 1.0 (감속 정지)
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

                // 도플러 피치: 호버링 → 원래 피치로 복귀
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetDronePitch(Mathf.Lerp(1.1f, 1.0f, p));

                float propRot = (riseDuration + t) * 2400f;
                propLRt.localRotation = Quaternion.Euler(0, 0, propRot);
                propRRt.localRotation = Quaternion.Euler(0, 0, -propRot);

                droneRt.localRotation = Quaternion.Euler(0, 0, wobbleX * 0.5f);

                yield return null;
            }

            // ---- Phase 3: 급강하 비행 (0.396초) ---- 도플러: 1.0 → 1.5 (급가속 접근)
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

                // 도플러 피치: 급강하 가속 → EaseInQuart와 동기화하여 피치 급상승
                if (AudioManager.Instance != null)
                {
                    float dopplerPitch = Mathf.Lerp(1.0f, 1.5f, eased);
                    AudioManager.Instance.SetDronePitch(dopplerPitch);
                }

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
            Vector3 origPos = Vector3.zero;
            float t = 0f;

            try
            {
                while (t < duration)
                {
                    t += Time.deltaTime;
                    float decay = 1f - (t / duration);
                    float offsetX = Random.Range(-intensity, intensity) * decay;
                    float offsetY = Random.Range(-intensity, intensity) * decay;
                    gridTrans.localPosition = origPos + new Vector3(offsetX, offsetY, 0f);
                    yield return null;
                }
            }
            finally
            {
                gridTrans.localPosition = Vector3.zero;
                VisualConstants.EndScreenShake();
            }
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

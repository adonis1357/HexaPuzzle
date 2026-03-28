// ============================================================================
// SpecialBlockComboSystem.cs - 특수 블록 합성 시스템
// ============================================================================
//
// [한줄 요약]
// 두 특수 블록(드릴, 폭탄, X블록)을 합성하여 더 강력한 효과를 발동하는 시스템.
//
// [합성이란?]
// 인접한 두 특수 블록을 스왑하면, 둘이 합쳐져서 단일 블록보다 훨씬 강력한
// 합성 효과가 발동됩니다. 총 6가지 조합이 존재합니다.
//
// [합성 조합 6종]
//   1. 드릴+드릴 → 6방향 드릴 (3축 양방향 동시 발사)
//   2. 드릴+폭탄 → Ring1+Ring2 폭발 후, 드릴 방향 축으로 확장 범위에서 드릴 발사
//   3. 드릴+X블록 → 같은 색 블록 전부 드릴로 변환 후 동시 발동
//   4. 폭탄+폭탄 → 4칸 범위 순차 폭발
//   5. 폭탄+X블록 → 같은 색 블록 전부 폭탄으로 변환 후 동시 폭발
//   6. X블록+X블록 → 전체 블록 거리순 순차 파괴 (전판 클리어)
//
// [처리 흐름]
// CanCombo() → ExecuteCombo() → ComboCoroutine() → 합성 타입별 코루틴 → 낙하+캐스케이드
//
// ============================================================================

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
    /// 특수 블록 합성 시스템.
    /// 인접한 두 특수 블록(드릴, 폭탄, X블록)을 합성하여
    /// 더 강력한 효과를 발동시키는 코어 시스템 클래스.
    /// </summary>
    public class SpecialBlockComboSystem : MonoBehaviour
    {
        // ============================================================
        // 참조 필드
        // ============================================================

        /// <summary>육각형 그리드 참조. 블록 위치, 좌표 검증, 이웃 탐색 등에 사용.</summary>
        private HexGrid hexGrid;

        /// <summary>드릴 특수 블록 시스템. 드릴 생성/발동/투사체 발사 기능 사용.</summary>
        private DrillBlockSystem drillSystem;

        /// <summary>폭탄 특수 블록 시스템. 폭탄 생성/발동/폭발 이펙트 기능 사용.</summary>
        private BombBlockSystem bombSystem;

        /// <summary>X블록 특수 블록 시스템. X블록 생성/발동 기능 사용.</summary>
        private XBlockSystem xBlockSystem;

        /// <summary>드론 특수 블록 시스템. 드론 생성/발동 기능 사용.</summary>
        private DroneBlockSystem droneSystem;

        /// <summary>블록 제거 시스템. 낙하 및 캐스케이드 처리 위임.</summary>
        private BlockRemovalSystem blockRemovalSystem;

        /// <summary>스테이지 매니저. 미션 타겟 정보 조회.</summary>
        private StageManager stageManager;

        /// <summary>이펙트 오브젝트들의 부모 Transform. Canvas 내부에 별도 레이어로 생성.</summary>
        private Transform effectParent;

        /// <summary>화면 흔들림 중첩 관리 카운터. 마지막 흔들림이 끝날 때만 위치 복원.</summary>
        private int shakeCount = 0;

        /// <summary>흔들림 시작 시 저장한 원래 위치.</summary>
        private Vector3 shakeOriginalPos;

        // ============================================================
        // 상태
        // ============================================================

        /// <summary>현재 합성이 진행 중인지 여부.</summary>
        private bool isComboActive = false;

        /// <summary>외부에서 합성 진행 상태를 확인하는 프로퍼티.</summary>
        public bool IsComboActive => isComboActive;

        // ============================================================
        // 이벤트
        // ============================================================

        /// <summary>합성 완료 시 점수를 전달하는 이벤트.</summary>
        public event System.Action<int> OnComboComplete;

        // ============================================================
        // 초기화
        // ============================================================

        /// <summary>
        /// 합성 시스템을 초기화합니다. GameManager 또는 BlockRemovalSystem에서 호출.
        /// </summary>
        /// <param name="grid">육각형 그리드 참조</param>
        public void Initialize(HexGrid grid)
        {
            hexGrid = grid;
            drillSystem = FindObjectOfType<DrillBlockSystem>();
            bombSystem = FindObjectOfType<BombBlockSystem>();
            xBlockSystem = FindObjectOfType<XBlockSystem>();
            droneSystem = FindObjectOfType<DroneBlockSystem>();
            blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            stageManager = FindObjectOfType<StageManager>();

            if (drillSystem == null) Debug.LogWarning("[ComboSystem] DrillBlockSystem not found!");
            if (bombSystem == null) Debug.LogWarning("[ComboSystem] BombBlockSystem not found!");
            if (xBlockSystem == null) Debug.LogWarning("[ComboSystem] XBlockSystem not found!");
            if (droneSystem == null) Debug.LogWarning("[ComboSystem] DroneBlockSystem not found!");
            if (blockRemovalSystem == null) Debug.LogWarning("[ComboSystem] BlockRemovalSystem not found!");
        }

        /// <summary>
        /// 비상 초기화. 합성이 무한 루프에 빠지거나 비정상 상태일 때 강제 리셋.
        /// </summary>
        public void ForceReset()
        {
            isComboActive = false;
            StopAllCoroutines();
            CleanupEffects();
            Debug.Log("[ComboSystem] ForceReset called");
        }

        // ============================================================
        // 핵심 API
        // ============================================================

        /// <summary>
        /// 두 블록이 합성 가능한지 판단합니다.
        /// 조건: 둘 다 null이 아님, 둘 다 Drill/Bomb/XBlock 중 하나, 서로 인접.
        /// </summary>
        /// <param name="a">첫 번째 블록</param>
        /// <param name="b">두 번째 블록</param>
        /// <returns>합성 가능하면 true</returns>
        public bool CanCombo(HexBlock a, HexBlock b)
        {
            if (a == null || b == null) return false;
            if (a.Data == null || b.Data == null) return false;

            // 합성 가능한 특수 블록 타입: Drill, Bomb, XBlock만
            bool aValid = IsComboableType(a.Data.specialType);
            bool bValid = IsComboableType(b.Data.specialType);
            if (!aValid || !bValid) return false;

            // 서로 인접 (거리 1)
            if (a.Coord.DistanceTo(b.Coord) != 1) return false;

            return true;
        }

        /// <summary>
        /// 합성 가능한 특수 블록 타입인지 확인합니다.
        /// Drill, Bomb, XBlock, Drone만 합성 가능.
        /// </summary>
        private bool IsComboableType(SpecialBlockType type)
        {
            return type == SpecialBlockType.Drill ||
                   type == SpecialBlockType.Bomb ||
                   type == SpecialBlockType.XBlock ||
                   type == SpecialBlockType.Drone;
        }

        /// <summary>
        /// 합성을 실행합니다. 이미 합성 중이면 무시.
        /// </summary>
        /// <param name="source">이동하는 블록 (source → target 위치로 이동)</param>
        /// <param name="target">목표 위치의 블록</param>
        public void ExecuteCombo(HexBlock source, HexBlock target)
        {
            if (isComboActive) return;
            StartCoroutine(ComboCoroutine(source, target));
        }

        // ============================================================
        // 메인 합성 코루틴
        // ============================================================

        /// <summary>
        /// 합성의 전체 과정을 처리하는 메인 코루틴.
        ///
        /// 흐름:
        ///   1. 합성 데이터 캐싱 (ClearData 전에 정보 저장)
        ///   2. 스왑 이동 애니메이션 (호 궤적)
        ///   3. 두 블록 ClearData
        ///   4. 합성 타입 분기 → 해당 합성 코루틴 실행
        ///   5. 이펙트 정리
        ///   6. 낙하 + 캐스케이드 트리거
        ///   7. 완료 대기
        /// </summary>
        private IEnumerator ComboCoroutine(HexBlock source, HexBlock target)
        {
            isComboActive = true;

            Debug.Log($"[ComboSystem] === COMBO START === source={source.Coord}({source.Data?.specialType}), target={target.Coord}({target.Data?.specialType})");

            // [1단계] 합성에 필요한 데이터를 미리 캐싱 (ClearData 후에는 데이터가 사라짐)
            SpecialBlockType sourceType = source.Data.specialType;
            SpecialBlockType targetType = target.Data.specialType;
            GemType sourceGem = source.Data.gemType;
            GemType targetGem = target.Data.gemType;
            DrillDirection sourceDrillDir = source.Data.drillDirection;
            DrillDirection targetDrillDir = target.Data.drillDirection;
            HexCoord sourceCoord = source.Coord;
            HexCoord targetCoord = target.Coord;
            Vector3 sourcePos = source.transform.position;
            Vector3 targetPos = target.transform.position;

            // [2단계] 스왑 이동 애니메이션 (source → target 위치로 호 궤적 이동)
            yield return StartCoroutine(SwapMoveAnimation(source, sourcePos, targetPos, 0.25f));

            // [3단계] 두 블록 모두 데이터 클리어 (합성 실행 위치에서 빈 공간 생성)
            source.ClearData();
            target.ClearData();

            // 합성 실행 위치 = target의 좌표와 월드 좌표
            HexCoord comboPos = targetCoord;
            Vector3 comboWorldPos = targetPos;

            // 합성 타입에 사용할 색상 결정 (XBlock이 있으면 XBlock의 색상 우선)
            GemType comboColor = targetGem;
            DrillDirection comboDrillDir = sourceDrillDir;

            // [4~5단계] 합성 타입 분기
            if (sourceType == SpecialBlockType.Drill && targetType == SpecialBlockType.Drill)
            {
                Debug.Log("[ComboSystem] >> Drill + Drill Combo");
                yield return StartCoroutine(DrillDrillCombo(comboPos, comboWorldPos, comboColor));
            }
            else if ((sourceType == SpecialBlockType.Drill && targetType == SpecialBlockType.Bomb) ||
                     (sourceType == SpecialBlockType.Bomb && targetType == SpecialBlockType.Drill))
            {
                Debug.Log("[ComboSystem] >> Drill + Bomb Combo");
                DrillDirection drillDir = sourceType == SpecialBlockType.Drill ? sourceDrillDir : targetDrillDir;
                GemType color = sourceType == SpecialBlockType.Bomb ? sourceGem : targetGem;
                yield return StartCoroutine(DrillBombCombo(comboPos, comboWorldPos, color, drillDir));
            }
            else if ((sourceType == SpecialBlockType.Drill && targetType == SpecialBlockType.XBlock) ||
                     (sourceType == SpecialBlockType.XBlock && targetType == SpecialBlockType.Drill))
            {
                Debug.Log("[ComboSystem] >> Drill + XBlock Combo");
                GemType xColor = sourceType == SpecialBlockType.XBlock ? sourceGem : targetGem;
                DrillDirection drillDir = sourceType == SpecialBlockType.Drill ? sourceDrillDir : targetDrillDir;
                yield return StartCoroutine(DrillXBlockCombo(comboPos, comboWorldPos, xColor, drillDir));
            }
            else if (sourceType == SpecialBlockType.Bomb && targetType == SpecialBlockType.Bomb)
            {
                Debug.Log("[ComboSystem] >> Bomb + Bomb Combo");
                yield return StartCoroutine(BombBombCombo(comboPos, comboWorldPos, comboColor));
            }
            else if ((sourceType == SpecialBlockType.Bomb && targetType == SpecialBlockType.XBlock) ||
                     (sourceType == SpecialBlockType.XBlock && targetType == SpecialBlockType.Bomb))
            {
                Debug.Log("[ComboSystem] >> Bomb + XBlock Combo");
                GemType xColor = sourceType == SpecialBlockType.XBlock ? sourceGem : targetGem;
                yield return StartCoroutine(BombXBlockCombo(comboPos, comboWorldPos, xColor));
            }
            else if (sourceType == SpecialBlockType.XBlock && targetType == SpecialBlockType.XBlock)
            {
                Debug.Log("[ComboSystem] >> XBlock + XBlock Combo");
                yield return StartCoroutine(XBlockXBlockCombo(comboPos, comboWorldPos));
            }
            // ── 드론 합성 조합 (4가지) ──
            else if (sourceType == SpecialBlockType.Drone && targetType == SpecialBlockType.Drone)
            {
                Debug.Log("[ComboSystem] >> Drone + Drone Combo");
                yield return StartCoroutine(DroneDroneCombo(comboPos, comboWorldPos, comboColor));
            }
            else if ((sourceType == SpecialBlockType.Drone && targetType == SpecialBlockType.Drill) ||
                     (sourceType == SpecialBlockType.Drill && targetType == SpecialBlockType.Drone))
            {
                Debug.Log("[ComboSystem] >> Drone + Drill Combo");
                DrillDirection drillDir = sourceType == SpecialBlockType.Drill ? sourceDrillDir : targetDrillDir;
                yield return StartCoroutine(DroneDrillCombo(comboPos, comboWorldPos, comboColor, drillDir));
            }
            else if ((sourceType == SpecialBlockType.Drone && targetType == SpecialBlockType.Bomb) ||
                     (sourceType == SpecialBlockType.Bomb && targetType == SpecialBlockType.Drone))
            {
                Debug.Log("[ComboSystem] >> Drone + Bomb Combo");
                yield return StartCoroutine(DroneBombCombo(comboPos, comboWorldPos, comboColor));
            }
            else if ((sourceType == SpecialBlockType.Drone && targetType == SpecialBlockType.XBlock) ||
                     (sourceType == SpecialBlockType.XBlock && targetType == SpecialBlockType.Drone))
            {
                Debug.Log("[ComboSystem] >> Drone + XBlock Combo");
                GemType xColor = sourceType == SpecialBlockType.XBlock ? sourceGem : targetGem;
                yield return StartCoroutine(DroneXBlockCombo(comboPos, comboWorldPos, xColor));
            }
            else
            {
                Debug.LogWarning($"[ComboSystem] Unknown combo: {sourceType} + {targetType}");
            }

            // [6단계] 이펙트 정리
            CleanupEffects();

            // [7단계] 낙하 + 캐스케이드 트리거
            if (blockRemovalSystem != null)
            {
                blockRemovalSystem.TriggerFallOnly();

                // 캐스케이드 완료 대기
                while (blockRemovalSystem.IsProcessing)
                    yield return null;
            }

            Debug.Log("[ComboSystem] === COMBO COMPLETE ===");
            isComboActive = false;
        }

        // ============================================================
        // 스왑 이동 애니메이션
        // ============================================================

        /// <summary>
        /// 블록을 호 궤적으로 이동시키는 애니메이션.
        /// 직선이 아닌 살짝 위로 볼록한 호를 그리며 이동합니다.
        /// </summary>
        private IEnumerator SwapMoveAnimation(HexBlock block, Vector3 from, Vector3 to, float duration)
        {
            if (block == null) yield break;

            float elapsed = 0f;
            Vector3 midPoint = (from + to) / 2f + Vector3.up * 20f; // 호의 꼭대기

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                // 이차 베지어 곡선으로 호 궤적 계산
                Vector3 a = Vector3.Lerp(from, midPoint, eased);
                Vector3 b = Vector3.Lerp(midPoint, to, eased);
                block.transform.position = Vector3.Lerp(a, b, eased);

                yield return null;
            }

            block.transform.position = to;
        }

        // ============================================================
        // 합성 조합 1: 드릴 + 드릴
        // 3축 6방향 동시 드릴 발사 (별 모양 관통)
        // ============================================================

        /// <summary>
        /// 드릴+드릴 합성: 합성 위치에서 3축(Vertical, Slash, BackSlash)의
        /// 양방향(positive/negative) 총 6방향으로 드릴 투사체를 동시 발사합니다.
        /// </summary>
        private IEnumerator DrillDrillCombo(HexCoord pos, Vector3 worldPos, GemType color)
        {
            SetupEffectParent();

            // 히트스톱 + 줌펀치 (Large 강도)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            Color comboColor = GemColors.GetColor(color);

            // 3축 = Vertical, Slash, BackSlash
            DrillDirection[] axes = { DrillDirection.Vertical, DrillDirection.Slash, DrillDirection.BackSlash };
            bool[] sides = { true, false };

            // 점수 및 미션 데이터 수집용
            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();
            var pendingSpecials = new List<HexBlock>();
            var allCoroutines = new List<Coroutine>();

            // 6방향 각각에 대해 타겟 수집 → 투사체 발사
            foreach (var axis in axes)
            {
                foreach (bool positive in sides)
                {
                    if (drillSystem == null) continue;

                    List<HexBlock> targets = drillSystem.GetBlocksInDirectionPublic(pos, axis, positive);

                    // 타겟 중 특수블록은 pending으로 분리, 일반 블록만 드릴에 전달
                    List<HexBlock> normalTargets = new List<HexBlock>();
                    foreach (var t in targets)
                    {
                        if (t == null || t.Data == null || t.Data.gemType == GemType.None) continue;

                        // 적군 방패 흡수 체크
                        if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(t))
                            continue;

                        if (t.Data.specialType != SpecialBlockType.None &&
                            t.Data.specialType != SpecialBlockType.FixedBlock)
                        {
                            if (!pendingSpecials.Contains(t))
                            {
                                pendingSpecials.Add(t);
                                t.SetPendingActivation();
                                t.StartWarningBlink(10f);
                            }
                        }
                        else
                        {
                            // 점수 사전 수집
                            blockScoreSum += ScoreCalculator.GetBlockBaseScore(t.Data.tier);
                            CollectGemCount(t, gemCountsByColor);
                            normalTargets.Add(t);
                        }
                    }

                    // ★ 드릴 투사체 발사 (순차 0.05초 간격)
                    allCoroutines.Add(StartCoroutine(
                        drillSystem.DrillLineWithProjectilePublic(
                            worldPos, normalTargets, axis, positive, comboColor, pos, true)));
                    yield return new WaitForSeconds(0.05f);
                }
            }

            // 화면 흔들림 (Large)
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            // 모든 투사체 코루틴 완료 대기
            foreach (var co in allCoroutines)
                yield return co;

            // 점수 계산 및 이벤트 발동
            int totalScore = 500 + blockScoreSum;
            Debug.Log($"[ComboSystem] DrillDrill complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 2: 드릴 + 폭탄
        // 폭탄 2단 폭발 후, 드릴 방향 축으로 폭발범위를 확장하여 드릴 발사
        // ============================================================

        /// <summary>
        /// 드릴+폭탄 합성:
        ///   1단계: Ring1(인접 6칸) 폭발
        ///   2단계: Ring2(2칸 범위) 폭발
        ///   3단계: 폭발 범위 경계에서 드릴 방향에 수직으로 나란히 배치된
        ///          약 5개 블록이 드릴 방향으로 병렬 드릴 동시 발사 (양방향 총 ~10발)
        ///
        /// 연출: 폭발 파동 → 발사 기지 플래시 → 병렬 드릴 동시 발사
        /// </summary>
        private IEnumerator DrillBombCombo(HexCoord pos, Vector3 worldPos, GemType color, DrillDirection drillDir)
        {
            SetupEffectParent();

            Color comboColor = GemColors.GetColor(color);

            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();
            var pendingSpecials = new List<HexBlock>();
            HashSet<HexCoord> alreadyTargeted = new HashSet<HexCoord>();
            alreadyTargeted.Add(pos); // 합성 위치

            var allDestroyCoroutines = new List<Coroutine>();

            // ================================================================
            // 1단계: Ring1 폭발 (인접 6칸)
            // ================================================================

            // 중앙 폭발 이펙트
            if (bombSystem != null)
                StartCoroutine(bombSystem.BombExplosionEffectPublic(worldPos, comboColor));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            // Ring1 타겟 수집
            List<HexBlock> ring1Targets = new List<HexBlock>();
            if (hexGrid != null)
            {
                var neighbors = hexGrid.GetNeighbors(pos);
                foreach (var neighbor in neighbors)
                {
                    if (neighbor == null || neighbor.Data == null || neighbor.Data.gemType == GemType.None) continue;
                    alreadyTargeted.Add(neighbor.Coord);
                    ring1Targets.Add(neighbor);
                }
            }

            // Ring1 블록 파괴
            foreach (var target in ring1Targets)
            {
                if (target == null || target.Data == null || target.Data.gemType == GemType.None) continue;

                if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(target))
                    continue;

                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    if (!pendingSpecials.Contains(target))
                    {
                        pendingSpecials.Add(target);
                        target.SetPendingActivation();
                        target.StartWarningBlink(10f);
                    }
                }
                else
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
                    CollectGemCount(target, gemCountsByColor);

                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    if (bombSystem != null)
                        allDestroyCoroutines.Add(StartCoroutine(
                            bombSystem.DestroyBlockWithExplosionPublic(target, blockColor, worldPos, true)));
                }
            }

            // ================================================================
            // 2단계: 0.1초 후 Ring2 폭발 (2칸 범위)
            // ================================================================
            yield return new WaitForSeconds(0.1f);

            // Ring2 충격파 이펙트
            StartCoroutine(ComboExplosionWave(worldPos, comboColor, 0.85f));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity * 0.7f, VisualConstants.ShakeLargeDuration * 0.8f));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            // Ring2 타겟 수집 (Ring1과 중복 제외)
            List<HexBlock> ring2Targets = new List<HexBlock>();
            if (hexGrid != null)
            {
                var allInR2 = HexCoord.GetHexesInRadius(pos, 2);
                foreach (var coord in allInR2)
                {
                    if (alreadyTargeted.Contains(coord)) continue;
                    if (!hexGrid.IsValidCoord(coord)) continue;

                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;

                    alreadyTargeted.Add(coord);
                    ring2Targets.Add(block);
                }
            }

            // Ring2 블록 파괴
            foreach (var target in ring2Targets)
            {
                if (target == null || target.Data == null || target.Data.gemType == GemType.None) continue;

                if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(target))
                    continue;

                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    if (!pendingSpecials.Contains(target))
                    {
                        pendingSpecials.Add(target);
                        target.SetPendingActivation();
                        target.StartWarningBlink(10f);
                    }
                }
                else
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
                    CollectGemCount(target, gemCountsByColor);

                    Color blockColor = GemColors.GetColor(target.Data.gemType);
                    if (bombSystem != null)
                        allDestroyCoroutines.Add(StartCoroutine(
                            bombSystem.DestroyBlockWithExplosionPublic(target, blockColor, worldPos, true)));
                }
            }

            // ================================================================
            // 3단계: 폭발 범위 경계에서 드릴 방향으로 5개 병렬 드릴 발사
            //
            // 폭발 범위(Ring2)의 경계에서 드릴 방향에 수직인 라인을 따라
            // 나란히 배치된 블록들(약 5개)이 각각 드릴 방향으로 동시 발사.
            // 양방향(positive/negative) 모두 실행하므로 총 약 10개 드릴.
            // ================================================================
            yield return new WaitForSeconds(0.12f);

            // 히트스톱 + 줌펀치 (드릴 발사 강조)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            var drillCoroutines = new List<Coroutine>();

            // 드릴 방향 델타
            HexCoord drillDelta = drillSystem != null
                ? drillSystem.GetDirectionDeltaPublic(drillDir, true)
                : new HexCoord(0, 1);

            // 드릴 방향에 수직인 2개 축 구하기
            // hex 6방향: (0,1), (1,-1), (1,0), (0,-1), (-1,1), (-1,0)
            // drillDelta와 내적이 0인 (= 수직인) 방향들을 수집
            HexCoord[] allHexDirs = {
                new HexCoord(0, 1), new HexCoord(1, -1), new HexCoord(1, 0),
                new HexCoord(0, -1), new HexCoord(-1, 1), new HexCoord(-1, 0)
            };
            // 수직 방향 = drillDelta와 같지도, 반대도 아닌 4방향 중 인접한 2쌍
            // 실제로는 drillDelta의 양쪽 60° 방향 2개가 수직 성분
            List<HexCoord> perpDirs = new List<HexCoord>();
            foreach (var d in allHexDirs)
            {
                // drillDelta와 같거나 반대 방향이면 스킵
                if ((d.q == drillDelta.q && d.r == drillDelta.r) ||
                    (d.q == -drillDelta.q && d.r == -drillDelta.r))
                    continue;
                perpDirs.Add(d);
            }

            // 양방향(positive/negative)으로 드릴 발사
            bool[] drillSides = { true, false };
            foreach (bool positive in drillSides)
            {
                HexCoord fireDir = positive ? drillDelta : new HexCoord(-drillDelta.q, -drillDelta.r);

                // Ring2 경계에서 드릴 방향 쪽 끝 좌표 = pos + fireDir * 2
                HexCoord edgeCenter = pos + new HexCoord(fireDir.q * 2, fireDir.r * 2);

                // edgeCenter + 수직 방향으로 ±2칸 탐색하여 발사 기지 좌표 수집
                // (edgeCenter 자체 포함, 최대 5개 좌표)
                List<HexCoord> launchCoords = new List<HexCoord>();
                launchCoords.Add(edgeCenter); // 중앙

                // 수직 방향 중 한 쌍을 골라 양쪽으로 확장
                // perpDirs에서 서로 반대가 아닌 독립 방향 2개를 선택
                HashSet<HexCoord> launchSet = new HashSet<HexCoord>();
                launchSet.Add(edgeCenter);

                foreach (var pd in perpDirs)
                {
                    for (int step = 1; step <= 2; step++)
                    {
                        HexCoord candidate = edgeCenter + new HexCoord(pd.q * step, pd.r * step);
                        if (!launchSet.Contains(candidate))
                        {
                            // Ring2 경계 또는 바로 바깥에 있는 좌표만 (거리 2~3)
                            // ★ 그리드 밖이어도 발사 기지로 등록 (블록 없는 곳에서도 드릴 발사)
                            int distFromCenter = candidate.DistanceTo(pos);
                            if (distFromCenter >= 2)
                            {
                                launchSet.Add(candidate);
                                launchCoords.Add(candidate);
                            }
                        }
                    }
                }

                // 발사 전 글로우 이펙트: 각 발사 기지에 플래시
                foreach (var launchCoord in launchCoords)
                {
                    Vector3 lp = GetWorldPosition(launchCoord);
                    StartCoroutine(DrillExtImpactFlash(lp, VisualConstants.Brighten(comboColor)));
                }

                yield return new WaitForSeconds(0.08f);

                // 각 발사 기지에서 드릴 방향으로 드릴 투사체 발사
                foreach (var launchCoord in launchCoords)
                {
                    if (drillSystem == null) continue;

                    // 발사 기지 블록이 아직 남아있으면 파괴 (폭발 범위 안이면 이미 파괴됨)
                    if (!alreadyTargeted.Contains(launchCoord))
                    {
                        alreadyTargeted.Add(launchCoord);
                        HexBlock launchBlock = hexGrid != null ? hexGrid.GetBlock(launchCoord) : null;
                        if (launchBlock != null && launchBlock.Data != null && launchBlock.Data.gemType != GemType.None)
                        {
                            if (launchBlock.Data.specialType != SpecialBlockType.None &&
                                launchBlock.Data.specialType != SpecialBlockType.FixedBlock)
                            {
                                if (!pendingSpecials.Contains(launchBlock))
                                {
                                    pendingSpecials.Add(launchBlock);
                                    launchBlock.SetPendingActivation();
                                    launchBlock.StartWarningBlink(10f);
                                }
                            }
                            else
                            {
                                blockScoreSum += ScoreCalculator.GetBlockBaseScore(launchBlock.Data.tier);
                                CollectGemCount(launchBlock, gemCountsByColor);
                                StartCoroutine(DestroyFlash(launchBlock.transform.position,
                                    GemColors.GetColor(launchBlock.Data.gemType)));
                                allDestroyCoroutines.Add(StartCoroutine(DualEasingDestroy(launchBlock, 0.15f)));
                            }
                        }
                    }

                    // 드릴 방향으로 타겟 수집
                    List<HexBlock> drillTargets = drillSystem.GetBlocksInDirectionPublic(
                        launchCoord, drillDir, positive);

                    List<HexBlock> filteredTargets = new List<HexBlock>();
                    foreach (var t in drillTargets)
                    {
                        if (t == null || t.Data == null || t.Data.gemType == GemType.None) continue;
                        if (alreadyTargeted.Contains(t.Coord)) continue;

                        alreadyTargeted.Add(t.Coord);

                        if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(t))
                            continue;

                        if (t.Data.specialType != SpecialBlockType.None &&
                            t.Data.specialType != SpecialBlockType.FixedBlock)
                        {
                            if (!pendingSpecials.Contains(t))
                            {
                                pendingSpecials.Add(t);
                                t.SetPendingActivation();
                                t.StartWarningBlink(10f);
                            }
                        }
                        else
                        {
                            blockScoreSum += ScoreCalculator.GetBlockBaseScore(t.Data.tier);
                            CollectGemCount(t, gemCountsByColor);
                            filteredTargets.Add(t);
                        }
                    }

                    // ★ 순차 발사 (0.05초 간격)
                    {
                        Vector3 launchWorldPos = GetWorldPosition(launchCoord);
                        drillCoroutines.Add(StartCoroutine(
                            drillSystem.DrillLineWithProjectilePublic(
                                launchWorldPos, filteredTargets, drillDir, positive, comboColor, launchCoord, true)));
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            }

            // 드릴 발사 순간 추가 화면 흔들림
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            // 모든 파괴 애니메이션 완료 대기
            foreach (var co in allDestroyCoroutines)
                yield return co;

            // 드릴 완료 대기
            foreach (var co in drillCoroutines)
                yield return co;

            // ★ 폭탄 넉백: 폭발 범위 내 몬스터 밀어냄
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                yield return StartCoroutine(GoblinSystem.Instance.KnockbackFromBomb(pos));
            }

            // 점수 (기본 700 + 블록 점수)
            int totalScore = 700 + blockScoreSum;
            Debug.Log($"[ComboSystem] DrillBomb complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        /// <summary>
        /// 드릴 발사 기지에서의 임팩트 플래시 이펙트.
        /// 병렬 드릴 발사 직전 각 발사 위치에 플래시를 표시.
        /// </summary>
        private IEnumerator DrillExtImpactFlash(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            GameObject flash = new GameObject("DrillExtImpact");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var img = flash.AddComponent<Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.85f);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 40f;
            rt.sizeDelta = new Vector2(size, size);

            float elapsed = 0f;
            float duration = 0.12f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                yield return null;

                // yield 이후 오브젝트 파괴 체크
                if ((object)flash == null || flash == null) yield break;

                float scale = 1f + t * 1.5f;
                rt.sizeDelta = new Vector2(size * scale, size * scale);

                // 백색 → 컬러 전환 + 페이드
                Color c = Color.Lerp(Color.white, color, t);
                c.a = 0.85f * (1f - t);
                img.color = c;
            }

            if ((object)flash != null && flash != null) Destroy(flash);
        }

        // ============================================================
        // 합성 조합 3: 드릴 + X블록
        // 같은 색 블록을 전부 드릴로 변환 후 동시 발동
        // ============================================================

        /// <summary>
        /// 드릴+X블록 합성:
        ///   1. X블록 색상과 같은 블록을 모두 수집
        ///   2. 수집된 블록을 드릴로 변환 (플래시 이펙트)
        ///   3. 변환된 모든 드릴 동시 발동
        /// </summary>
        private IEnumerator DrillXBlockCombo(HexCoord pos, Vector3 worldPos, GemType xColor, DrillDirection drillDir)
        {
            SetupEffectParent();

            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();

            // 같은 색 블록 수집 (합성 위치 제외)
            List<HexBlock> colorTargets = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                    if (block.Coord == pos) continue;

                    if (block.Data.gemType == xColor)
                        colorTargets.Add(block);
                }
            }

            // 합성 위치에서 가까운 순으로 정렬
            colorTargets.Sort((a, b) => a.Coord.DistanceTo(pos).CompareTo(b.Coord.DistanceTo(pos)));

            Debug.Log($"[ComboSystem] DrillXBlock: {colorTargets.Count} blocks of color {xColor}");

            // 순차적으로 시간차를 두며 드릴로 변환
            float perBlockDelay = Mathf.Clamp(0.8f / Mathf.Max(1, colorTargets.Count), 0.02f, 0.1f);
            int transformIndex = 0;
            int transformTotal = colorTargets.Count;

            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;

                // 점수 사전 수집 (변환 전)
                if (block.Data.specialType == SpecialBlockType.None ||
                    block.Data.specialType == SpecialBlockType.FixedBlock)
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(block.Data.tier);
                    CollectGemCount(block, gemCountsByColor);
                }

                // 드릴로 변환
                if (drillSystem != null)
                    drillSystem.CreateDrillBlock(block, drillDir, block.Data.gemType);

                // 변환 플래시 이펙트 + 사운드 + 스케일 펀치
                StartCoroutine(TransformFlash(block.transform.position, GemColors.GetColor(xColor)));
                StartCoroutine(TransformScalePunch(block));
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayTransformTickSound(transformIndex, transformTotal);
                transformIndex++;

                yield return new WaitForSeconds(perBlockDelay);
            }

            // 마지막 변환 후 빨간 테두리 표시 + 0.3초 딜레이
            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.specialType != SpecialBlockType.Drill) continue;
                SetRedBorder(block);
            }

            yield return new WaitForSeconds(0.3f);

            // 변환된 모든 드릴 동시 발동
            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.specialType != SpecialBlockType.Drill) continue;

                ClearRedBorder(block);
                if (drillSystem != null)
                    drillSystem.ActivateDrill(block);
            }

            // 히트스톱 + 줌펀치 + 화면 흔들림 (Large)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            // 모든 드릴 완료 대기 (최대 5초 타임아웃)
            float timeout = 5f;
            float waited = 0f;
            while (waited < timeout && drillSystem != null && drillSystem.IsDrilling)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (waited >= timeout)
                Debug.LogWarning("[ComboSystem] DrillXBlock timeout! Forcing completion.");

            // 점수
            int totalScore = 700 + blockScoreSum;
            Debug.Log($"[ComboSystem] DrillXBlock complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 4: 폭탄 + 폭탄
        // 4칸 범위 순차 폭발 (링 1~4 순차)
        // ============================================================

        /// <summary>
        /// 폭탄+폭탄 합성:
        ///   Ring1 → Ring2 → Ring3 → Ring4 순차적으로 폭발.
        ///   각 링의 폭발 강도가 점차 감소합니다.
        /// </summary>
        private IEnumerator BombBombCombo(HexCoord pos, Vector3 worldPos, GemType color)
        {
            SetupEffectParent();

            Color comboColor = GemColors.GetColor(color);

            // 히트스톱 + 줌펀치 (Large)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            // 중앙 폭발 이펙트
            if (bombSystem != null)
                StartCoroutine(bombSystem.BombExplosionEffectPublic(worldPos, comboColor));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();
            var pendingSpecials = new List<HexBlock>();
            var allDestroyCoroutines = new List<Coroutine>();

            // Ring 1~4 순차 폭발
            for (int ring = 1; ring <= 4; ring++)
            {
                // 현재 링 거리의 블록들 수집
                var allInRange = HexCoord.GetHexesInRadius(pos, ring);
                var prevRange = HexCoord.GetHexesInRadius(pos, ring - 1);
                var ringCoords = allInRange.Except(prevRange).ToList();

                // 폭발 강도 점감
                float intensity = 1.0f - ring * 0.15f;
                float shakeIntensity = VisualConstants.ShakeLargeIntensity * intensity;
                float shakeDuration = VisualConstants.ShakeLargeDuration * intensity;

                List<Coroutine> ringDestroyCoroutines = new List<Coroutine>();

                foreach (var coord in ringCoords)
                {
                    if (hexGrid == null) continue;
                    if (!hexGrid.IsValidCoord(coord)) continue;

                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;

                    // 적군 방패 흡수
                    if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(block))
                        continue;

                    if (block.Data.specialType != SpecialBlockType.None &&
                        block.Data.specialType != SpecialBlockType.FixedBlock)
                    {
                        if (!pendingSpecials.Contains(block))
                        {
                            pendingSpecials.Add(block);
                            block.SetPendingActivation();
                            block.StartWarningBlink(10f);
                        }
                    }
                    else
                    {
                        blockScoreSum += ScoreCalculator.GetBlockBaseScore(block.Data.tier);
                        CollectGemCount(block, gemCountsByColor);

                        Color blockColor = GemColors.GetColor(block.Data.gemType);
                        if (bombSystem != null)
                        {
                            ringDestroyCoroutines.Add(StartCoroutine(
                                bombSystem.DestroyBlockWithExplosionPublic(block, blockColor, worldPos, true)));
                        }
                    }
                }

                // 폭발 이펙트 (강도 점감)
                if (ring <= 2)
                {
                    Vector3 ringCenter = worldPos; // 링 중심은 여전히 합성 위치
                    StartCoroutine(ComboExplosionWave(ringCenter, comboColor, intensity));
                }

                StartCoroutine(ScreenShake(shakeIntensity, shakeDuration));

                allDestroyCoroutines.AddRange(ringDestroyCoroutines);

                // 링 간 대기
                yield return new WaitForSeconds(0.1f);
            }

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            // 모든 파괴 애니메이션 완료 대기
            foreach (var co in allDestroyCoroutines)
                yield return co;

            // ★ 폭탄×폭탄 전용 데미지: 0칸=8, 1칸=4, 2칸=3, 3칸=2, 4칸=1
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var bbDamageMap = new Dictionary<HexCoord, int>();
                var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();
                foreach (var g in aliveGoblins)
                {
                    int gdq = g.position.q - pos.q;
                    int gdr = g.position.r - pos.r;
                    int gds = -gdq - gdr;
                    int gdist = Mathf.Max(Mathf.Abs(gdq), Mathf.Max(Mathf.Abs(gdr), Mathf.Abs(gds)));
                    int dmg = 0;
                    switch (gdist)
                    {
                        case 0: dmg = 8; break;
                        case 1: dmg = 4; break;
                        case 2: dmg = 3; break;
                        case 3: dmg = 2; break;
                        case 4: dmg = 1; break;
                    }
                    if (dmg > 0 && !bbDamageMap.ContainsKey(g.position))
                        bbDamageMap[g.position] = dmg;
                }
                if (bbDamageMap.Count > 0)
                {
                    var directHits = new HashSet<HexCoord>(bbDamageMap.Keys);
                    GoblinSystem.Instance.ApplyBatchDamage(bbDamageMap, directHits);
                }

                // ★ 폭탄×폭탄 넉백: 범위 4칸, 목적지 = bombPos + 방향 × 5
                yield return StartCoroutine(GoblinSystem.Instance.KnockbackFromBomb(pos, 4));
            }

            // 점수
            int totalScore = 800 + blockScoreSum;
            Debug.Log($"[ComboSystem] BombBomb complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 5: 폭탄 + X블록
        // 같은 색 블록을 전부 폭탄으로 변환 후 동시 폭발
        // ============================================================

        /// <summary>
        /// 폭탄+X블록 합성:
        ///   1. X블록 색상과 같은 블록을 모두 수집
        ///   2. 수집된 블록을 폭탄으로 변환
        ///   3. 변환된 모든 폭탄 동시 발동
        /// </summary>
        private IEnumerator BombXBlockCombo(HexCoord pos, Vector3 worldPos, GemType xColor)
        {
            SetupEffectParent();

            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();

            // 같은 색 블록 수집
            List<HexBlock> colorTargets = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                    if (block.Coord == pos) continue;

                    if (block.Data.gemType == xColor)
                        colorTargets.Add(block);
                }
            }

            // 합성 위치에서 가까운 순으로 정렬
            colorTargets.Sort((a, b) => a.Coord.DistanceTo(pos).CompareTo(b.Coord.DistanceTo(pos)));

            Debug.Log($"[ComboSystem] BombXBlock: {colorTargets.Count} blocks of color {xColor}");

            // 순차적으로 시간차를 두며 폭탄으로 변환
            float perBlockDelay = Mathf.Clamp(0.8f / Mathf.Max(1, colorTargets.Count), 0.02f, 0.1f);
            int transformIndex = 0;
            int transformTotal = colorTargets.Count;

            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;

                // 점수 사전 수집
                if (block.Data.specialType == SpecialBlockType.None ||
                    block.Data.specialType == SpecialBlockType.FixedBlock)
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(block.Data.tier);
                    CollectGemCount(block, gemCountsByColor);
                }

                // 폭탄으로 변환
                if (bombSystem != null)
                    bombSystem.CreateBombBlock(block, block.Data.gemType);

                // 변환 플래시 이펙트 + 사운드 + 스케일 펀치
                StartCoroutine(TransformFlash(block.transform.position, GemColors.GetColor(xColor)));
                StartCoroutine(TransformScalePunch(block));
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayTransformTickSound(transformIndex, transformTotal);
                transformIndex++;

                yield return new WaitForSeconds(perBlockDelay);
            }

            // 마지막 변환 후 빨간 테두리 표시 + 0.3초 딜레이
            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.specialType != SpecialBlockType.Bomb) continue;
                SetRedBorder(block);
            }

            yield return new WaitForSeconds(0.3f);

            // 변환된 모든 폭탄 동시 발동
            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.specialType != SpecialBlockType.Bomb) continue;

                ClearRedBorder(block);
                if (bombSystem != null)
                    bombSystem.ActivateBomb(block);
            }

            // 히트스톱 + 줌펀치 + 화면 흔들림 (Large)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            // 모든 폭탄 완료 대기 (최대 5초 타임아웃)
            float timeout = 5f;
            float waited = 0f;
            while (waited < timeout && bombSystem != null && bombSystem.IsBombing)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (waited >= timeout)
                Debug.LogWarning("[ComboSystem] BombXBlock timeout! Forcing completion.");

            // 점수
            int totalScore = 700 + blockScoreSum;
            Debug.Log($"[ComboSystem] BombXBlock complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 6: X블록 + X블록
        // 전체 블록 거리순 순차 파괴 (전판 클리어)
        // ============================================================

        /// <summary>
        /// X블록+X블록 합성:
        ///   전체 블록을 합성 위치에서의 거리순으로 정렬하여 순차 파괴.
        ///   가장 강력한 합성. 전판 클리어에 가까운 효과.
        /// </summary>
        private IEnumerator XBlockXBlockCombo(HexCoord pos, Vector3 worldPos)
        {
            SetupEffectParent();

            // 히트스톱 + 줌펀치 (Large)
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();

            // 모든 블록 수집
            List<HexBlock> allBlocks = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block.Coord == pos) continue; // 합성 위치 제외

                    allBlocks.Add(block);
                }
            }

            // 합성 위치에서 거리순 정렬
            allBlocks.Sort((a, b) =>
            {
                int distA = a.Coord.DistanceTo(pos);
                int distB = b.Coord.DistanceTo(pos);
                return distA.CompareTo(distB);
            });

            Debug.Log($"[ComboSystem] XBlockXBlock: {allBlocks.Count} total blocks");

            // 최대 거리 계산
            int maxDistance = 0;
            foreach (var block in allBlocks)
            {
                int dist = block.Coord.DistanceTo(pos);
                if (dist > maxDistance) maxDistance = dist;
            }

            // 점수 사전 수집
            foreach (var block in allBlocks)
            {
                if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                if (block.Data.specialType == SpecialBlockType.FixedBlock) continue;

                if (block.Data.specialType == SpecialBlockType.None)
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(block.Data.tier);
                    CollectGemCount(block, gemCountsByColor);
                }
            }

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            // 고블린 데미지 추적용 (중복 방지)
            HashSet<HexCoord> damagedGoblinCoords = new HashSet<HexCoord>();

            // Ring별 순차 파괴
            for (int ring = 1; ring <= maxDistance; ring++)
            {
                // 현재 거리의 블록들 필터
                var ringBlocks = allBlocks.Where(b => b.Coord.DistanceTo(pos) == ring).ToList();

                foreach (var block in ringBlocks)
                {
                    if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;

                    // FixedBlock은 파괴 불가
                    if (block.Data.specialType == SpecialBlockType.FixedBlock) continue;

                    // 특수 블록은 pending 처리
                    if (block.Data.specialType != SpecialBlockType.None)
                    {
                        block.SetPendingActivation();
                        block.StartWarningBlink(10f);
                        continue;
                    }

                    // 적군 방패 흡수
                    if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(block))
                        continue;

                    // 미션 카운팅: DualEasingDestroy(ClearData) 전에 개별 보고
                    if (block.Data.gemType != GemType.None)
                        GameManager.Instance?.OnSingleGemDestroyedForMission(block.Data.gemType);

                    // 플래시 + 파괴 애니메이션
                    Color blockColor = GemColors.GetColor(block.Data.gemType);
                    StartCoroutine(DestroyFlash(block.transform.position, blockColor));
                    StartCoroutine(SpawnDebris(block.transform.position, blockColor, 4));
                    StartCoroutine(DualEasingDestroy(block));

                    // 사운드 (간헐적)
                    if (AudioManager.Instance != null && Random.value > 0.5f)
                        AudioManager.Instance.PlayBlockDestroySound();

                    // 해당 링에서 고블린 데미지 (블록 좌표)
                    if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                    {
                        if (!damagedGoblinCoords.Contains(block.Coord))
                        {
                            GoblinSystem.Instance.ApplyDamageAtPosition(block.Coord, 1);
                            damagedGoblinCoords.Add(block.Coord);
                        }
                    }
                }

                // 화면 흔들림 (강도 점감)
                float intensity = Mathf.Max(0.3f, 1.0f - ring * 0.12f);
                StartCoroutine(ScreenShake(
                    VisualConstants.ShakeMediumIntensity * intensity,
                    VisualConstants.ShakeMediumDuration * intensity));

                // 링 간 대기
                yield return new WaitForSeconds(0.08f);
            }

            // --- 화면 밖까지 퍼지는 충격파: 필드의 모든 고블린에게 데미지 ---
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();
                if (aliveGoblins.Count > 0)
                {
                    // 추가 확장 링 (그리드 밖까지 퍼지는 연출, 3링 추가)
                    for (int extraRing = 0; extraRing < 3; extraRing++)
                    {
                        // 확장 충격파 비주얼 이펙트
                        float ringRadius = (maxDistance + 1 + extraRing) * 50f * 2f;
                        StartCoroutine(ShockwaveRingEffect(worldPos, ringRadius, new Color(0f, 1f, 1f, 0.5f)));

                        // 화면 흔들림 (약하게)
                        StartCoroutine(ScreenShake(
                            VisualConstants.ShakeSmallIntensity * 0.5f,
                            VisualConstants.ShakeSmallDuration * 0.5f));

                        yield return new WaitForSeconds(0.06f);
                    }

                    // 모든 살아있는 고블린에게 1 데미지 (위치 무관)
                    foreach (var goblin in aliveGoblins)
                    {
                        if (goblin.isAlive)
                        {
                            GoblinSystem.Instance.ApplyDamageAtPosition(goblin.position, 1);

                            // 고블린 위치에 히트 이펙트
                            if (goblin.visualObject != null)
                                StartCoroutine(DestroyFlash(goblin.visualObject.transform.position, new Color(0f, 1f, 1f, 1f)));
                        }
                    }

                    Debug.Log($"[ComboSystem] XBlockXBlock: 전체 고블린 {aliveGoblins.Count}마리에 충격파 데미지 적용");
                }
            }

            // 파괴 애니메이션 완료 대기
            yield return new WaitForSeconds(0.3f);

            // 점수
            int totalScore = 1000 + blockScoreSum;
            Debug.Log($"[ComboSystem] XBlockXBlock complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 7: 드론 + 드론
        // 3개 랜덤 타겟 순차 타격 (우선순위 기반)
        // ============================================================

        /// <summary>
        /// 드론+드론 합성: 5회 우선순위 기반 몬스터 직접 타격.
        /// 블록 파괴 없이 GoblinBomb → 궁수 → 방패 → 갑옷 → 일반 순서로 타격.
        /// 몬스터가 부족하면 남은 드론은 스마트 블록 타겟.
        /// </summary>
        private IEnumerator DroneDroneCombo(HexCoord pos, Vector3 worldPos, GemType color)
        {
            SetupEffectParent();

            Color comboColor = GemColors.GetColor(color);

            StartCoroutine(HitStop(VisualConstants.HitStopDurationMedium));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDroneSound();

            int strikes = 5;

            // ★ 점수 기반 몬스터 직접 타격 (상위 5마리)
            List<HexCoord> goblinTargets = new List<HexCoord>();
            List<HexBlock> blockTargets = new List<HexBlock>();
            HashSet<HexCoord> usedGoblinCoords = new HashSet<HexCoord>();

            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();

                // 점수 기반 정렬: BombGoblin(5) > Shield(4) > Archer(3) > Armored(2) > Regular(1)
                // GoblinBomb은 블록 기반이므로 별도 수집
                if (hexGrid != null)
                {
                    var bombs = new List<HexBlock>();
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block != null && block.Data != null && block.Data.hasGoblinBomb)
                            bombs.Add(block);
                    }
                    bombs.Sort((a, b) => a.Data.goblinBombCountdown.CompareTo(b.Data.goblinBombCountdown));
                    foreach (var bomb in bombs)
                    {
                        if (goblinTargets.Count >= strikes) break;
                        goblinTargets.Add(bomb.Coord);
                        usedGoblinCoords.Add(bomb.Coord);
                    }
                }

                // 몬스터를 점수 내림차순 정렬 후 수집
                var sortedGoblins = aliveGoblins
                    .OrderByDescending(g => GoblinScoreForDroneDrone(g))
                    .ThenBy(g => g.hp)
                    .ThenBy(g => g.position.q)
                    .ToList();

                foreach (var g in sortedGoblins)
                {
                    if (goblinTargets.Count >= strikes) break;
                    if (usedGoblinCoords.Contains(g.position)) continue;
                    // 궁수: HP만큼 배정
                    if (g.isArcher)
                    {
                        int assign = Mathf.Min(g.hp, strikes - goblinTargets.Count);
                        for (int d = 0; d < assign; d++)
                            goblinTargets.Add(g.position);
                    }
                    else
                    {
                        goblinTargets.Add(g.position);
                    }
                    usedGoblinCoords.Add(g.position);
                }
            }

            // 남은 드론은 블록 타겟 (점수 기반)
            int remainingStrikes = strikes - goblinTargets.Count;
            HashSet<HexBlock> alreadyStruck = new HashSet<HexBlock>();
            for (int i = 0; i < remainingStrikes; i++)
            {
                HexBlock target = FindSmartDroneComboTarget(alreadyStruck);
                if (target == null) target = FindBestDroneComboTarget(pos, alreadyStruck);
                if (target == null) break;
                alreadyStruck.Add(target);
                blockTargets.Add(target);
            }

            int totalDrones = goblinTargets.Count + blockTargets.Count;
            if (totalDrones == 0)
            {
                OnComboComplete?.Invoke(400);
                yield break;
            }

            // 드론 비행
            int droneIndex = 0;

            foreach (var coord in goblinTargets)
            {
                Vector3 targetWorldPos = hexGrid != null
                    ? (Vector3)hexGrid.CalculateFlatTopHexPosition(coord)
                    : worldPos;
                // 고블린 비주얼이 있으면 해당 위치 사용
                if (GoblinSystem.Instance != null)
                {
                    var g = GoblinSystem.Instance.GetGoblinAt(coord);
                    if (g != null && g.visualObject != null)
                        targetWorldPos = g.visualObject.transform.position;
                }
                float delay = droneIndex * 0.06f;
                if (droneSystem != null)
                    StartCoroutine(droneSystem.PlayDroneFlyEffectWithDelay(worldPos, targetWorldPos, comboColor, delay));
                droneIndex++;
            }

            foreach (var target in blockTargets)
            {
                float delay = droneIndex * 0.06f;
                if (droneSystem != null)
                    StartCoroutine(droneSystem.PlayDroneFlyEffectWithDelay(worldPos, target.transform.position, comboColor, delay));
                droneIndex++;
            }

            float maxWait = (totalDrones - 1) * 0.06f + 0.86f;
            yield return new WaitForSeconds(maxWait);

            // 동시 타격
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            // 몬스터 직접 타격 (1 대미지 → 방패는 ApplyDamageAtPosition 내부에서 방패 처리)
            foreach (var coord in goblinTargets)
            {
                if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                {
                    GoblinSystem.Instance.ApplyDamageAtPosition(coord, 1);
                    Vector3 hitPos = hexGrid != null
                        ? (Vector3)hexGrid.CalculateFlatTopHexPosition(coord)
                        : worldPos;
                    StartCoroutine(DestroyFlash(hitPos, comboColor));
                }
            }

            // 블록 타격 (폴백)
            foreach (var target in blockTargets)
            {
                if (target == null || target.Data == null) continue;
                StartCoroutine(DestroyFlash(target.transform.position, comboColor));
                if (target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    target.SetPendingActivation();
                    target.StartWarningBlink(10f);
                }
                else
                {
                    ApplyDroneStrikeCombo(target);
                }
                if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                    GoblinSystem.Instance.ApplyDamageAtPosition(target.Coord, 1);
            }

            yield return new WaitForSeconds(0.1f);

            int totalScore = 500;
            Debug.Log($"[ComboSystem] DroneDrone: 몬스터직접={goblinTargets.Count}, 블록={blockTargets.Count}, Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 8: 드론 + 드릴
        // 드론이 타겟 블록까지 비행 후 해당 블록에서 3축 드릴 발사
        // ============================================================

        /// <summary>
        /// 드론+드릴 합성: 고블린이 가장 많은 드릴 라인을 찾아 비행 후,
        /// 타겟 위치에서 원래 드릴 방향(drillDir) 양쪽 2방향 드릴 발사.
        /// 고블린이 없으면 기존 미션 블록 최적화 로직 사용.
        /// </summary>
        private IEnumerator DroneDrillCombo(HexCoord pos, Vector3 worldPos, GemType color, DrillDirection drillDir)
        {
            SetupEffectParent();

            Color comboColor = GemColors.GetColor(color);
            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();
            var pendingSpecials = new List<HexBlock>();

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDroneSound();

            // 타겟 선택
            HexCoord drillPos = pos;
            Vector3 drillWorldPos = worldPos;
            HexBlock blockTarget = null;
            bool targetIsGoblin = false;

            // ★ 1순위: 활 고블린 직접 타격 (낙하 면역이므로 직접 타격만 유효)
            GoblinData targetArcher = FindBestArcherForCombo();
            if (targetArcher != null)
            {
                Vector3 archerWorldPos = GetArcherWorldPos(targetArcher);

                // 드론 비행 → 활 고블린 직접 타격
                if (droneSystem != null)
                    yield return StartCoroutine(droneSystem.PlayDroneFlyEffect(worldPos, archerWorldPos, comboColor));
                else
                    yield return new WaitForSeconds(0.4f);

                // 활 고블린 직접 대미지
                if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                    GoblinSystem.Instance.ApplyDamageAtPosition(targetArcher.position, 1);
                Debug.Log($"[ComboSystem] DroneDrill: 활 고블린 직접 타격 ({targetArcher.position}) 1 대미지");

                // 드릴 발사 위치: 활 고블린의 실제 위치에서 발사
                drillPos = targetArcher.position;
                drillWorldPos = archerWorldPos;
                targetIsGoblin = true;
            }
            // 2순위: 일반 고블린 최적 드릴 라인 탐색
            else
            {
                HexCoord bestGoblinLinePos;
                int bestGoblinCount;
                FindBestGoblinDrillLine(pos, drillDir, out bestGoblinLinePos, out bestGoblinCount);

                if (bestGoblinCount > 0)
                {
                    // 고블린이 있는 최적 라인 위치로 비행
                    drillPos = bestGoblinLinePos;
                    drillWorldPos = GetWorldPosition(drillPos);
                    targetIsGoblin = true;

                    Debug.Log($"[ComboSystem] DroneDrill: 고블린 라인 타겟 ({drillPos}), 라인 내 고블린 {bestGoblinCount}마리");

                    // 드론 비행
                    if (droneSystem != null)
                        yield return StartCoroutine(droneSystem.PlayDroneFlyEffect(worldPos, drillWorldPos, comboColor));
                    else
                        yield return new WaitForSeconds(0.4f);

                    // 착지 위치에 블록이 있으면 파괴
                    HexBlock landingBlock = hexGrid != null ? hexGrid.GetBlock(drillPos) : null;
                    if (landingBlock != null && landingBlock.Data != null && landingBlock.Data.gemType != GemType.None)
                    {
                        blockScoreSum += ScoreCalculator.GetBlockBaseScore(landingBlock.Data.tier);
                        CollectGemCount(landingBlock, gemCountsByColor);
                        StartCoroutine(DestroyFlash(drillWorldPos, comboColor));
                        landingBlock.ClearData();
                    }

                    // 착지 위치 고블린에 데미지
                    if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                        GoblinSystem.Instance.ApplyDamageAtPosition(drillPos, 1);
                }
                else
                {
                    // 고블린 없음 → 기존 미션 블록 최적화 타겟
                    blockTarget = FindBestDrillComboTarget(pos, drillDir);

                    if (blockTarget != null)
                    {
                        drillPos = blockTarget.Coord;
                        drillWorldPos = blockTarget.transform.position;

                        // 드론 비행
                        if (droneSystem != null)
                            yield return StartCoroutine(droneSystem.PlayDroneFlyEffect(worldPos, drillWorldPos, comboColor));
                        else
                            yield return new WaitForSeconds(0.4f);

                        // 타겟 블록 파괴
                        if (blockTarget.Data != null)
                        {
                            blockScoreSum += ScoreCalculator.GetBlockBaseScore(blockTarget.Data.tier);
                            CollectGemCount(blockTarget, gemCountsByColor);
                            StartCoroutine(DestroyFlash(drillWorldPos, comboColor));
                            blockTarget.ClearData();
                        }
                    }
                }
            }

            // 히트스톱 + 줌펀치
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            // 타겟 위치에서 원래 드릴 방향(drillDir) 양쪽 2방향만 발사
            bool[] sides = { true, false };
            var allCoroutines = new List<Coroutine>();

            foreach (bool positive in sides)
            {
                if (drillSystem == null) continue;

                List<HexBlock> targets = drillSystem.GetBlocksInDirectionPublic(drillPos, drillDir, positive);
                List<HexBlock> normalTargets = new List<HexBlock>();
                foreach (var t in targets)
                {
                    if (t == null || t.Data == null || t.Data.gemType == GemType.None) continue;
                    if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(t))
                        continue;
                    if (t.Data.specialType != SpecialBlockType.None &&
                        t.Data.specialType != SpecialBlockType.FixedBlock)
                    {
                        if (!pendingSpecials.Contains(t))
                        {
                            pendingSpecials.Add(t);
                            t.SetPendingActivation();
                            t.StartWarningBlink(10f);
                        }
                    }
                    else
                    {
                        blockScoreSum += ScoreCalculator.GetBlockBaseScore(t.Data.tier);
                        CollectGemCount(t, gemCountsByColor);
                        normalTargets.Add(t);
                    }
                }

                // ★ 순차 발사 (0.05초 간격)
                allCoroutines.Add(StartCoroutine(
                    drillSystem.DrillLineWithProjectilePublic(
                        drillWorldPos, normalTargets, drillDir, positive, comboColor, drillPos, true)));
                yield return new WaitForSeconds(0.05f);
            }

            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDrillSound();

            foreach (var co in allCoroutines)
                yield return co;

            int totalScore = 600 + blockScoreSum;
            Debug.Log($"[ComboSystem] DroneDrill complete. drillDir={drillDir}, Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 9: 드론 + 폭탄
        // 드론이 타겟까지 비행 후 해당 위치에서 폭탄 폭발
        // ============================================================

        /// <summary>
        /// 드론+폭탄 합성: 우선순위 타겟까지 드론 비행 후,
        /// 타겟 위치에서 Ring1+Ring2 2단 폭발
        /// </summary>
        private IEnumerator DroneBombCombo(HexCoord pos, Vector3 worldPos, GemType color)
        {
            SetupEffectParent();

            Color comboColor = GemColors.GetColor(color);
            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();
            var pendingSpecials = new List<HexBlock>();
            var allDestroyCoroutines = new List<Coroutine>();

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDroneSound();

            HexBlock target = null;
            HexCoord bombPos = pos;
            Vector3 bombWorldPos = worldPos;

            // ★ 1순위: 활 고블린 직접 타격 (낙하 면역이므로 직접 타격만 유효)
            GoblinData bombArcherTarget = FindBestArcherForCombo();
            if (bombArcherTarget != null)
            {
                Vector3 archerWorldPos = GetArcherWorldPos(bombArcherTarget);

                // 최적 폭발 위치 계산: 활 고블린 범위 내 + 고블린/블록 최대 효율
                bombPos = FindOptimalBombPositionForArcher(bombArcherTarget);
                bombWorldPos = GetWorldPositionAny(bombPos);

                // 드론 비행 → 활 고블린 직접 타격 후 폭발 위치로
                if (droneSystem != null)
                    yield return StartCoroutine(droneSystem.PlayDroneFlyEffect(worldPos, archerWorldPos, comboColor));
                else
                    yield return new WaitForSeconds(0.4f);

                // 활 고블린 직접 대미지
                if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                    GoblinSystem.Instance.ApplyDamageAtPosition(bombArcherTarget.position, 1);
                Debug.Log($"[ComboSystem] DroneBomb: 활 고블린 직접 타격 ({bombArcherTarget.position}) 1 대미지, 폭발 위치={bombPos}");

                // 폭발 중심 블록 파괴 (그리드 내에 있으면)
                HexBlock centerBlock = hexGrid != null ? hexGrid.GetBlock(bombPos) : null;
                if (centerBlock != null && centerBlock.Data != null && centerBlock.Data.gemType != GemType.None)
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(centerBlock.Data.tier);
                    CollectGemCount(centerBlock, gemCountsByColor);
                    centerBlock.ClearData();
                }
            }
            // 2순위: 기존 미션/고블린 최적 타겟
            else
            {
                target = FindBestBombComboTarget(pos);

                if (target != null)
                {
                    bombPos = target.Coord;
                    bombWorldPos = target.transform.position;

                    // 드론 비행 이펙트 (정식 이륙→호버→급강하)
                    if (droneSystem != null)
                        yield return StartCoroutine(droneSystem.PlayDroneFlyEffect(worldPos, bombWorldPos, comboColor));
                    else
                        yield return new WaitForSeconds(0.4f);

                    // 타겟 블록 파괴
                    if (target.Data != null)
                    {
                        blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
                        CollectGemCount(target, gemCountsByColor);
                        target.ClearData();
                    }
                }
            }

            // 히트스톱 + 줌펀치
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));

            // 폭발 이펙트
            if (bombSystem != null)
                StartCoroutine(bombSystem.BombExplosionEffectPublic(bombWorldPos, comboColor));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            // Ring1 폭발
            HashSet<HexCoord> alreadyTargeted = new HashSet<HexCoord>();
            alreadyTargeted.Add(bombPos);

            if (hexGrid != null)
            {
                var neighbors = hexGrid.GetNeighbors(bombPos);
                foreach (var neighbor in neighbors)
                {
                    if (neighbor == null || neighbor.Data == null || neighbor.Data.gemType == GemType.None) continue;
                    alreadyTargeted.Add(neighbor.Coord);

                    if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(neighbor))
                        continue;

                    if (neighbor.Data.specialType != SpecialBlockType.None &&
                        neighbor.Data.specialType != SpecialBlockType.FixedBlock)
                    {
                        if (!pendingSpecials.Contains(neighbor))
                        {
                            pendingSpecials.Add(neighbor);
                            neighbor.SetPendingActivation();
                            neighbor.StartWarningBlink(10f);
                        }
                    }
                    else
                    {
                        blockScoreSum += ScoreCalculator.GetBlockBaseScore(neighbor.Data.tier);
                        CollectGemCount(neighbor, gemCountsByColor);
                        Color blockColor = GemColors.GetColor(neighbor.Data.gemType);
                        if (bombSystem != null)
                            allDestroyCoroutines.Add(StartCoroutine(
                                bombSystem.DestroyBlockWithExplosionPublic(neighbor, blockColor, bombWorldPos, true)));
                    }
                }
            }

            // Ring2 폭발 (0.1초 후)
            yield return new WaitForSeconds(0.1f);
            StartCoroutine(ComboExplosionWave(bombWorldPos, comboColor, 0.85f));

            if (hexGrid != null)
            {
                var allInR2 = HexCoord.GetHexesInRadius(bombPos, 2);
                foreach (var coord in allInR2)
                {
                    if (alreadyTargeted.Contains(coord)) continue;
                    if (!hexGrid.IsValidCoord(coord)) continue;

                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;

                    alreadyTargeted.Add(coord);

                    if (EnemySystem.Instance != null && EnemySystem.Instance.TryAbsorbSpecialHit(block))
                        continue;

                    if (block.Data.specialType != SpecialBlockType.None &&
                        block.Data.specialType != SpecialBlockType.FixedBlock)
                    {
                        if (!pendingSpecials.Contains(block))
                        {
                            pendingSpecials.Add(block);
                            block.SetPendingActivation();
                            block.StartWarningBlink(10f);
                        }
                    }
                    else
                    {
                        blockScoreSum += ScoreCalculator.GetBlockBaseScore(block.Data.tier);
                        CollectGemCount(block, gemCountsByColor);
                        Color blockColor = GemColors.GetColor(block.Data.gemType);
                        if (bombSystem != null)
                            allDestroyCoroutines.Add(StartCoroutine(
                                bombSystem.DestroyBlockWithExplosionPublic(block, blockColor, bombWorldPos, true)));
                    }
                }
            }

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            foreach (var co in allDestroyCoroutines)
                yield return co;

            // ★ 폭발 범위(Ring2) 내 모든 고블린에 대미지 (블록 외 영역 포함)
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                var blastGoblins = GoblinSystem.Instance.GetAliveGoblins();
                int blastDamageCount = 0;
                foreach (var goblin in blastGoblins)
                {
                    if (!goblin.isAlive) continue;
                    int dist = bombPos.DistanceTo(goblin.position);
                    if (dist <= 2 && !alreadyTargeted.Contains(goblin.position))
                    {
                        GoblinSystem.Instance.ApplyDamageAtPosition(goblin.position, 1);
                        blastDamageCount++;
                    }
                }
                if (blastDamageCount > 0)
                {
                    // 폭발 범위 충격파 이펙트: 고블린 피격 위치에 시각 효과
                    StartCoroutine(BombBlastShockwave(bombWorldPos, comboColor));
                    Debug.Log($"[ComboSystem] DroneBomb: 폭발 범위 내 고블린 {blastDamageCount}마리 추가 대미지");
                }
            }

            // ★ 폭탄 넉백: 폭발 범위 내 몬스터 밀어냄
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                yield return StartCoroutine(GoblinSystem.Instance.KnockbackFromBomb(bombPos));
            }

            int totalScore = 600 + blockScoreSum;
            Debug.Log($"[ComboSystem] DroneBomb complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 10: 드론 + X블록
        // 같은 색 블록을 모두 드론으로 변환 후 동시 발동
        // ============================================================

        /// <summary>
        /// 드론+X블록 합성: X블록 색상과 같은 블록을 모두 드론으로 변환 후
        /// 순차적으로 드론 발동 (각각 우선순위 타겟 타격)
        /// </summary>
        private IEnumerator DroneXBlockCombo(HexCoord pos, Vector3 worldPos, GemType xColor)
        {
            SetupEffectParent();

            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();
            Color comboColor = GemColors.GetColor(xColor);

            // 같은 색 블록 수집
            List<HexBlock> colorTargets = new List<HexBlock>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null || block.Data == null) continue;
                    if (block.Data.gemType == GemType.None) continue;
                    if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                    if (block.Coord == pos) continue;
                    if (block.Data.gemType == xColor)
                        colorTargets.Add(block);
                }
            }

            // 가까운 순 정렬
            colorTargets.Sort((a, b) => a.Coord.DistanceTo(pos).CompareTo(b.Coord.DistanceTo(pos)));

            Debug.Log($"[ComboSystem] DroneXBlock: {colorTargets.Count} blocks of color {xColor}");

            // 순차적으로 드론으로 변환
            float perBlockDelay = Mathf.Clamp(0.8f / Mathf.Max(1, colorTargets.Count), 0.02f, 0.1f);
            int transformIndex = 0;
            int transformTotal = colorTargets.Count;

            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;

                if (block.Data.specialType == SpecialBlockType.None ||
                    block.Data.specialType == SpecialBlockType.FixedBlock)
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(block.Data.tier);
                    CollectGemCount(block, gemCountsByColor);
                }

                // 드론으로 변환
                if (droneSystem != null)
                    droneSystem.CreateDroneBlock(block, block.Data.gemType);

                StartCoroutine(TransformFlash(block.transform.position, comboColor));
                StartCoroutine(TransformScalePunch(block));
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayTransformTickSound(transformIndex, transformTotal);
                transformIndex++;

                yield return new WaitForSeconds(perBlockDelay);
            }

            // 빨간 테두리 표시
            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.specialType != SpecialBlockType.Drone) continue;
                SetRedBorder(block);
            }

            yield return new WaitForSeconds(0.3f);

            // ★ 변환된 모든 드론 수집
            List<HexBlock> droneBlocks = new List<HexBlock>();
            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.specialType != SpecialBlockType.Drone) continue;
                droneBlocks.Add(block);
            }

            // ★ 타겟 목록 생성: DroneBlockSystem의 우선순위 타겟팅 사용
            // 각 드론마다 DroneBlockSystem.ActivateDrone()을 호출하면 내부에서
            // FindPriorityGoblinTarget → 스마트 블록 순으로 타겟 선정
            var damageTargets = BuildDroneTargetsByDamage(new HashSet<HexBlock>(droneBlocks));

            if (damageTargets.Count > 0)
            {
                int directCount = 0;
                int blockCount = 0;
                for (int i = 0; i < droneBlocks.Count; i++)
                {
                    HexBlock drone = droneBlocks[i];
                    var entry = damageTargets[i % damageTargets.Count];
                    ClearRedBorder(drone);

                    // ★ 드론 위치에 고블린이 서있으면 떠오르는 순간 1 대미지
                    if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                        GoblinSystem.Instance.ApplyDamageAtPosition(drone.Coord, 1);

                    if (droneSystem != null)
                    {
                        if (entry.isDirectGoblin)
                        {
                            droneSystem.ActivateDroneToGoblin(drone, entry.goblinPos);
                            directCount++;
                        }
                        else
                        {
                            droneSystem.ActivateDroneWithTarget(drone, entry.blockTarget);
                            blockCount++;
                        }
                    }
                }
                Debug.Log($"[ComboSystem] DroneXBlock: {droneBlocks.Count}대 드론 → {damageTargets.Count}개 타겟 순환 (블록:{blockCount}, 고블린직접:{directCount})");
            }
            else
            {
                // 고블린 없음: 미션 기반 타겟 폴백
                HashSet<HexBlock> missionExclude = new HashSet<HexBlock>(droneBlocks);
                List<HexBlock> missionTargets = new List<HexBlock>();
                while (missionTargets.Count < droneBlocks.Count)
                {
                    HexBlock mt = FindBestDroneComboTarget(pos, missionExclude);
                    if (mt == null) break;
                    missionExclude.Add(mt);
                    missionTargets.Add(mt);
                }

                for (int i = 0; i < droneBlocks.Count; i++)
                {
                    HexBlock drone = droneBlocks[i];
                    ClearRedBorder(drone);

                    // ★ 드론 위치에 고블린이 서있으면 떠오르는 순간 1 대미지
                    if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
                        GoblinSystem.Instance.ApplyDamageAtPosition(drone.Coord, 1);

                    if (droneSystem != null)
                    {
                        if (missionTargets.Count > 0)
                            droneSystem.ActivateDroneWithTarget(drone, missionTargets[i % missionTargets.Count]);
                        else
                            droneSystem.ActivateDrone(drone);
                    }
                }
            }

            // 히트스톱 + 줌펀치
            StartCoroutine(HitStop(VisualConstants.HitStopDurationLarge));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleLarge));
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDroneSound();

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            // 드론 완료 대기 (최대 5초)
            float timeout = 5f;
            float waited = 0f;
            while (waited < timeout && droneSystem != null && droneSystem.IsActivating)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (waited >= timeout)
                Debug.LogWarning("[ComboSystem] DroneXBlock timeout! Forcing completion.");

            int totalScore = 700 + blockScoreSum;
            Debug.Log($"[ComboSystem] DroneXBlock complete. Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 드론 합성 유틸 메서드
        // ============================================================

        /// <summary>
        /// 드론×X블록 콤보용: 낙하 대미지 포함 총 대미지 기준 타겟 목록 생성
        /// 각 블록 타겟: 총 대미지 = 해당 열 비아처 고블린 수(낙하) + 충돌 보너스
        /// 킬 우선순위: 처치 가능 고블린 수 포함 정렬
        /// 아처 고블린: 낙하 면역 → 낙하 대미지 카운트에서 제외, 직접 타격 엔트리로 별도 처리
        /// </summary>
        private List<(HexBlock blockTarget, HexCoord goblinPos, bool isDirectGoblin, int totalDamage)>
            BuildDroneTargetsByDamage(HashSet<HexBlock> excludeBlocks)
        {
            var results = new List<(HexBlock blockTarget, HexCoord goblinPos, bool isDirectGoblin, int totalDamage)>();
            if (hexGrid == null) return results;

            var goblinSystem = GoblinSystem.Instance;
            if (goblinSystem == null || !goblinSystem.IsActive) return results;

            var aliveGoblins = goblinSystem.GetAliveGoblins();
            if (aliveGoblins.Count == 0) return results;

            // ★ 열(q)별 비아처 고블린 수 = 해당 열 블록 파괴 시 실제 낙하 대미지
            // 아처 고블린은 낙하 면역이므로 낙하 대미지 카운트에서 제외
            var colNonArcherCount = new Dictionary<int, int>();
            var colNonArcherGoblins = new Dictionary<int, List<GoblinData>>();
            foreach (var g in aliveGoblins)
            {
                if (!g.isAlive || g.isArcher) continue;
                int q = g.position.q;
                if (!colNonArcherCount.ContainsKey(q))
                {
                    colNonArcherCount[q] = 0;
                    colNonArcherGoblins[q] = new List<GoblinData>();
                }
                colNonArcherCount[q]++;
                colNonArcherGoblins[q].Add(g);
            }

            // ★ 0) 활 고블린 최우선: HP만큼 엔트리 추가 (낙하 면역, 직접 타격만 유효)
            foreach (var g in aliveGoblins)
            {
                if (!g.isAlive || !g.isArcher) continue;
                for (int h = 0; h < g.hp; h++)
                    results.Add((null, g.position, true, 100));
            }

            // 1) 블록 타겟: 비아처 고블린이 있는 열의 모든 블록에 대미지+킬 계산
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                if (excludeBlocks.Contains(block)) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;

                int q = block.Coord.q;
                if (!colNonArcherCount.ContainsKey(q)) continue; // 비아처 고블린 없는 열 제외

                int fallDamage = colNonArcherCount[q];
                // 충돌: 비아처 고블린만 (아처는 직접 타격 엔트리로 처리)
                bool hasCollision = false;
                foreach (var g in colNonArcherGoblins[q])
                {
                    if (g.position == block.Coord)
                    {
                        hasCollision = true;
                        break;
                    }
                }
                int collision = hasCollision ? 1 : 0;
                int total = fallDamage + collision;

                // ★ 킬 카운트 계산 (정렬에 반영)
                int killCount = 0;
                foreach (var g in colNonArcherGoblins[q])
                {
                    if (hasCollision && g.position == block.Coord)
                    {
                        if (g.hp <= 2) killCount++;
                    }
                    else
                    {
                        if (g.hp <= 1) killCount++;
                    }
                }

                // totalDamage에 킬 카운트 보너스 (킬 1개 = +50점)
                int sortScore = total + killCount * 50;
                results.Add((block, default(HexCoord), false, sortScore));
            }

            // 2) 고블린 직접 타격: 블록이 없는 위치(확장 영역 등)의 고블린 (아처 제외)
            foreach (var g in aliveGoblins)
            {
                if (!g.isAlive) continue;
                if (g.isArcher) continue;
                HexBlock blockAt = hexGrid.GetBlock(g.position);
                bool hasUsableBlock = blockAt != null && blockAt.Data != null
                    && blockAt.Data.gemType != GemType.None
                    && !excludeBlocks.Contains(blockAt);
                if (hasUsableBlock) continue;
                // 직접 타격 1대미지, hp==1이면 킬 가능 (+50)
                int sortScore = 1 + (g.hp <= 1 ? 50 : 0);
                results.Add((null, g.position, true, sortScore));
            }

            // 정렬: sortScore 내림차순 → 직접 타격 우선 → 가장자리 → 왼쪽
            results.Sort((a, b) =>
            {
                if (a.totalDamage != b.totalDamage) return b.totalDamage.CompareTo(a.totalDamage);
                if (a.isDirectGoblin != b.isDirectGoblin) return a.isDirectGoblin ? -1 : 1;
                int aq = a.isDirectGoblin ? a.goblinPos.q : (a.blockTarget != null ? a.blockTarget.Coord.q : 0);
                int bq = b.isDirectGoblin ? b.goblinPos.q : (b.blockTarget != null ? b.blockTarget.Coord.q : 0);
                int distA = Mathf.Abs(aq);
                int distB = Mathf.Abs(bq);
                if (distA != distB) return distB.CompareTo(distA);
                return aq.CompareTo(bq);
            });

            return results;
        }

        /// <summary>
        /// 우선순위 기반 최적 타겟 블록 검색 (이미 타격한 블록 제외)
        /// </summary>
        private HexBlock FindBestTarget(HexCoord fromPos, HashSet<HexBlock> excludeBlocks)
        {
            if (hexGrid == null) return null;

            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                if (excludeBlocks.Contains(block)) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) return null;

            // 미션 타겟 정보 수집
            HashSet<GemType> missionGemTargets = GetComboMissionTargetGemTypes();
            bool hasObstacleMission = HasComboObstacleMission();

            HexCoord origin = new HexCoord(0, 0);
            candidates.Sort((a, b) =>
            {
                int prioA = GetComboTargetPriority(a, missionGemTargets, hasObstacleMission);
                int prioB = GetComboTargetPriority(b, missionGemTargets, hasObstacleMission);
                if (prioA != prioB) return prioB.CompareTo(prioA);
                int distA = a.Coord.DistanceTo(origin);
                int distB = b.Coord.DistanceTo(origin);
                if (distA != distB) return distB.CompareTo(distA);
                return b.Coord.r.CompareTo(a.Coord.r);
            });

            // 최상위 우선순위가 미션 기본 블록(100) 또는 일반 기본 블록(50)이면
            // 같은 우선순위 블록들 중 랜덤 선택
            int topPriority = GetComboTargetPriority(candidates[0], missionGemTargets, hasObstacleMission);
            if (topPriority == 100 || topPriority == 50)
            {
                List<HexBlock> sameGroup = new List<HexBlock>();
                foreach (var c in candidates)
                {
                    if (GetComboTargetPriority(c, missionGemTargets, hasObstacleMission) == topPriority)
                        sameGroup.Add(c);
                }
                return sameGroup[Random.Range(0, sameGroup.Count)];
            }

            return candidates[0];
        }

        /// <summary>
        /// 미션 타겟 GemType 수집 (합성 시스템용)
        /// 무한모드(MissionSystem) + 스테이지모드(StageManager) 모두 지원
        /// </summary>
        private HashSet<GemType> GetComboMissionTargetGemTypes()
        {
            HashSet<GemType> targets = new HashSet<GemType>();

            // ── 1) 무한모드: MissionSystem (우선 확인) ──
            if (MissionSystem.Instance != null)
            {
                var sm = MissionSystem.Instance.CurrentMission;
                if (sm != null && !sm.IsComplete)
                {
                    Debug.Log($"[ComboSystem] 미션 감지: type={sm.type}, target1={sm.targetGemType}, target2={sm.targetGemType2}");
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
                    }
                }
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
                Debug.Log($"[ComboSystem] ★ 미션 타겟 색상: {string.Join(", ", targets)}");

            return targets;
        }

        /// <summary>
        /// 장애물 관련 미션 존재 여부 (합성 시스템용)
        /// </summary>
        private bool HasComboObstacleMission()
        {
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
        /// 합성용 타겟 우선순위 (미션 연동)
        /// 0순위: 미션 타겟 > 1순위: 장애물 > 2순위: 높은 티어 > 3순위: 기본 블록 > 4순위: 특수 블록
        /// </summary>
        private int GetComboTargetPriority(HexBlock block, HashSet<GemType> missionGemTargets, bool hasObstacleMission)
        {
            var data = block.Data;
            if (data == null) return 0;

            // 1순위: 장애물 (미션 부스트)
            if (data.specialType == SpecialBlockType.FixedBlock) return hasObstacleMission ? 210 : 200;
            if (data.specialType == SpecialBlockType.TimeBomb) return 190;
            if (data.vinylLayer > 0) return hasObstacleMission ? 185 : 180;
            if (data.hasChain) return 170;
            if (data.enemyType != EnemyType.None) return hasObstacleMission ? 165 : 160;

            // 2순위: 높은 티어 블록 (미션 색상 부스트)
            bool isMissionColor = missionGemTargets.Count > 0 && missionGemTargets.Contains(data.gemType);

            if (data.tier >= BlockTier.ProcessedGem) return isMissionColor ? 145 : 140;
            if (data.tier >= BlockTier.Tier3) return isMissionColor ? 135 : 130;
            if (data.tier >= BlockTier.Tier2) return isMissionColor ? 125 : 120;
            if (data.tier >= BlockTier.Tier1) return isMissionColor ? 115 : 110;

            // 3순위: 미션 색상 기본 블록 — 최우선 타겟!
            if (data.specialType == SpecialBlockType.None && isMissionColor) return 100;

            // 4순위: 일반 기본 블록
            if (data.specialType == SpecialBlockType.None) return 50;

            // 5순위: 특수 블록 (희귀도 낮은 순, 기본 블록 없을 때만 타겟)
            if (data.specialType == SpecialBlockType.Drill) return 14;
            if (data.specialType == SpecialBlockType.Bomb) return 12;
            if (data.specialType == SpecialBlockType.Rainbow) return 8;
            if (data.specialType == SpecialBlockType.XBlock) return 6;

            return 4;
        }

        /// <summary>
        /// 합성용 드론 타격 효과: 티어 1단계 강등, Normal이면 파괴
        /// </summary>
        private void ApplyDroneStrikeCombo(HexBlock target)
        {
            if (target == null || target.Data == null) return;
            var data = target.Data;

            if (data.vinylLayer > 0) { data.vinylLayer--; target.SetBlockData(data); return; }
            if (data.hasChain) { data.hasChain = false; target.SetBlockData(data); return; }
            if (data.enemyShieldCount > 0) { data.enemyShieldCount--; target.SetBlockData(data); return; }
            if (data.tier == BlockTier.Normal)
            {
                // 미션 카운팅: ClearData 전에 개별 보고 (Stage/Infinite 모두 지원)
                if (data.gemType != GemType.None)
                    GameManager.Instance?.OnSingleGemDestroyedForMission(data.gemType);
                target.ClearData();
                return;
            }

            data.tier = (BlockTier)((int)data.tier - 1);
            target.SetBlockData(data);
        }

        // ============================================================
        // 미션 최적화 타겟 선정 메서드 (콤보 효과 범위 시뮬레이션)
        // ============================================================

        /// <summary>
        /// 활 고블린 우선 타겟: HP 낮은 순 → 왼쪽 열(q 작은것)
        /// 콤보에서 활 고블린 직접 타격 시 사용
        /// </summary>
        private GoblinData FindBestArcherForCombo()
        {
            if (GoblinSystem.Instance == null || !GoblinSystem.Instance.IsActive) return null;

            var archers = GoblinSystem.Instance.GetAliveGoblins()
                .Where(g => g.isAlive && g.isArcher).ToList();
            if (archers.Count == 0) return null;

            // HP가 낮은 활 고블린 우선 (처치 가능성↑), 동률 시 왼쪽(q 작은것)
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
        /// 활 고블린의 월드 좌표 반환 (visualObject 우선, 없으면 수학 계산)
        /// </summary>
        private Vector3 GetArcherWorldPos(GoblinData archer)
        {
            if (archer.visualObject != null)
                return archer.visualObject.transform.position;
            return GetWorldPositionAny(archer.position);
        }

        /// <summary>
        /// 그리드 내/외 어떤 HexCoord든 월드 좌표로 변환
        /// 그리드 내: 블록 transform.position 사용
        /// 그리드 외: hexGrid.CalculateFlatTopHexPosition + GridContainer 변환
        /// </summary>
        private Vector3 GetWorldPositionAny(HexCoord coord)
        {
            if (hexGrid != null)
            {
                HexBlock block = hexGrid.GetBlock(coord);
                if (block != null)
                    return block.transform.position;

                // 그리드 밖 좌표: 수학적 계산 + GridContainer 로컬→월드 변환
                Vector2 localPos = hexGrid.CalculateFlatTopHexPosition(coord);
                return hexGrid.GridContainer.TransformPoint(new Vector3(localPos.x, localPos.y, 0f));
            }

            // hexGrid 없으면 폴백
            float hexSize = 50f;
            Vector2 pos2D = coord.ToWorldPosition(hexSize);
            return new Vector3(pos2D.x, pos2D.y, 0f);
        }

        /// <summary>
        /// 드론×폭탄 콤보: 활 고블린 범위 내 최적 폭발 위치 계산.
        /// 활 고블린으로부터 거리 2 이내 모든 후보 위치를 탐색하여
        /// (범위 내 고블린 수 → 범위 내 블록 수 → 그리드 근접도) 최대화.
        /// 폭탄 범위 끝에만 포함되어도 대미지가 적용됨.
        /// </summary>
        private HexCoord FindOptimalBombPositionForArcher(GoblinData targetArcher)
        {
            if (GoblinSystem.Instance == null) return targetArcher.position;

            var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();

            // 후보: 타겟 활 고블린으로부터 거리 2 이내의 모든 좌표
            var candidates = HexCoord.GetHexesInRadius(targetArcher.position, 2);

            HexCoord bestPos = targetArcher.position;
            int bestGoblinHits = 0;
            int bestBlockCount = 0;
            int bestGridOverlap = 0; // 폭발 범위 중 그리드와 겹치는 셀 수

            foreach (var candidate in candidates)
            {
                int goblinHits = 0;
                int blockCount = 0;
                int gridOverlap = 0;

                // 이 위치에 폭발 시 범위(Ring2) 내 고블린 수 계산
                foreach (var goblin in aliveGoblins)
                {
                    if (!goblin.isAlive) continue;
                    int dist = candidate.DistanceTo(goblin.position);
                    if (dist <= 2) goblinHits++;
                }

                // 범위 내 그리드 블록 수 + 그리드 겹침 계산
                var blastArea = HexCoord.GetHexesInRadius(candidate, 2);
                foreach (var hex in blastArea)
                {
                    if (hexGrid != null && hexGrid.IsValidCoord(hex))
                    {
                        gridOverlap++;
                        HexBlock block = hexGrid.GetBlock(hex);
                        if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                            blockCount++;
                    }
                }

                // 비교: 고블린 수 → 블록 수 → 그리드 겹침(가까울수록 이펙트 가시성↑)
                bool isBetter = false;
                if (goblinHits > bestGoblinHits)
                    isBetter = true;
                else if (goblinHits == bestGoblinHits && blockCount > bestBlockCount)
                    isBetter = true;
                else if (goblinHits == bestGoblinHits && blockCount == bestBlockCount && gridOverlap > bestGridOverlap)
                    isBetter = true;

                if (isBetter)
                {
                    bestPos = candidate;
                    bestGoblinHits = goblinHits;
                    bestBlockCount = blockCount;
                    bestGridOverlap = gridOverlap;
                }
            }

            Debug.Log($"[ComboSystem] 최적 폭발 위치: {bestPos} (고블린 {bestGoblinHits}마리, 블록 {bestBlockCount}개, 그리드겹침 {bestGridOverlap}셀)");
            return bestPos;
        }

        /// <summary>
        /// 블록 외 지역 폭발 충격파 이펙트: 2단 확장 웨이브 + 중심 플래시.
        /// 그리드 밖에서 폭탄이 터질 때 눈에 보이는 시각 효과.
        /// </summary>
        private IEnumerator BombBlastShockwave(Vector3 center, Color color)
        {
            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            // 1단: 중심 플래시 (큰 원형)
            GameObject flash = new GameObject("BombBlastFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = center;
            var flashImg = flash.AddComponent<Image>();
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(1f, 1f, 1f, 0.9f);
            RectTransform flashRt = flash.GetComponent<RectTransform>();
            flashRt.sizeDelta = new Vector2(60f, 60f);

            // 2단: 외곽 충격파 링 (오렌지-적색)
            Color waveColor = new Color(
                Mathf.Min(1f, color.r * 1.2f),
                color.g * 0.6f,
                color.b * 0.3f,
                0.7f);
            StartCoroutine(ShockwaveRingEffect(center, 300f, waveColor));

            // 3단: 내부 충격파 링 (밝은 색)
            yield return new WaitForSeconds(0.05f);
            Color innerColor = new Color(1f, 0.95f, 0.8f, 0.5f);
            StartCoroutine(ShockwaveRingEffect(center, 200f, innerColor));

            // 중심 플래시 축소+페이드
            float flashDur = 0.2f;
            float elapsed = 0f;
            while (elapsed < flashDur)
            {
                if (flash == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flashDur);
                float scale = 1f + t * 2f;
                flashRt.sizeDelta = new Vector2(60f * scale, 60f * scale);
                flashImg.color = new Color(1f, 1f, 1f, 0.9f * (1f - t));
                yield return null;
            }

            if (flash != null) Destroy(flash);
        }

        /// <summary>
        /// 드론+드릴 콤보: 드릴 라인(양방향) 파괴 후 낙하 대미지까지 포함하여 총 피해 최대화.
        /// 직접 라인 충돌 + 열별 낙하 통과 대미지를 합산.
        /// 동률: HP합 → 가장자리 → 왼쪽 (일반 드론과 동일)
        /// </summary>
        private void FindBestGoblinDrillLine(HexCoord fromPos, DrillDirection drillDir,
            out HexCoord bestPos, out int bestGoblinCount)
        {
            bestPos = fromPos;
            bestGoblinCount = 0;

            if (GoblinSystem.Instance == null || !GoblinSystem.Instance.IsActive || drillSystem == null)
                return;

            var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();
            if (aliveGoblins.Count == 0) return;

            // 고블린 위치 데이터 준비
            HashSet<HexCoord> goblinPositions = new HashSet<HexCoord>();
            foreach (var g in aliveGoblins)
                goblinPositions.Add(g.position);

            // 모든 그리드 좌표 + 고블린 좌표를 후보로
            HashSet<HexCoord> candidates = new HashSet<HexCoord>();
            if (hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block != null) candidates.Add(block.Coord);
                }
            }
            foreach (var gPos in goblinPositions)
                candidates.Add(gPos);

            int bestShieldDirectHit = -1;
            int bestArcherCount = -1;
            int bestArmoredCount = -1;
            int bestHpSum = 0;
            int bestEdgeDist = -1;
            int bestQ = int.MaxValue;

            foreach (var candidate in candidates)
            {
                // 드릴 라인에 의해 파괴될 블록 좌표 수집
                var destroyedSet = new HashSet<HexCoord>();

                // 착탄 지점 자체
                if (hexGrid != null && hexGrid.IsValidCoord(candidate))
                {
                    HexBlock centerBlock = hexGrid.GetBlock(candidate);
                    if (centerBlock != null && centerBlock.Data != null && centerBlock.Data.gemType != GemType.None)
                        destroyedSet.Add(candidate);
                }

                // 양쪽 드릴 라인
                bool[] sides = { true, false };
                foreach (bool positive in sides)
                {
                    HexCoord delta = drillSystem.GetDirectionDeltaPublic(drillDir, positive);
                    HexCoord walkCoord = candidate + delta;
                    int safetyLimit = 0;
                    while (safetyLimit < 20)
                    {
                        if (hexGrid != null && hexGrid.IsValidCoord(walkCoord))
                        {
                            HexBlock block = hexGrid.GetBlock(walkCoord);
                            if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                                destroyedSet.Add(walkCoord);
                        }
                        else
                        {
                            // 그리드 밖, 고블린만 확인 후 없으면 중단
                            if (!goblinPositions.Contains(walkCoord))
                                break;
                        }
                        walkCoord = walkCoord + delta;
                        safetyLimit++;
                    }
                }

                // 열(q)별 파괴 블록 r값 수집
                var destroyedRByCol = new Dictionary<int, List<int>>();
                foreach (var coord in destroyedSet)
                {
                    if (!destroyedRByCol.ContainsKey(coord.q))
                        destroyedRByCol[coord.q] = new List<int>();
                    destroyedRByCol[coord.q].Add(coord.r);
                }

                // 각 고블린별 총 대미지 추정 (직접 충돌 + 낙하 대미지)
                int totalDamage = 0;
                int shieldDirectHit = 0;
                int archerCount = 0;
                int armoredCount = 0;
                int hpSum = 0;

                foreach (var goblin in aliveGoblins)
                {
                    int gq = goblin.position.q;
                    int gr = goblin.position.r;

                    // 1. 드릴 라인 위에 있는지 확인 (직접 타격)
                    bool onDrillLine = (goblin.position == candidate);
                    if (!onDrillLine)
                    {
                        foreach (bool positive in sides)
                        {
                            HexCoord delta = drillSystem.GetDirectionDeltaPublic(drillDir, positive);
                            HexCoord walkCoord = candidate + delta;
                            int safetyLimit = 0;
                            while (safetyLimit < 20)
                            {
                                if (walkCoord == goblin.position) { onDrillLine = true; break; }
                                if (hexGrid != null && !hexGrid.IsValidCoord(walkCoord) && !goblinPositions.Contains(walkCoord))
                                    break;
                                walkCoord = walkCoord + delta;
                                safetyLimit++;
                            }
                            if (onDrillLine) break;
                        }
                    }

                    if (goblin.isShielded)
                    {
                        // ★ 방패: 드릴 라인 직접 충돌만 대미지 가능 (낙하 면역)
                        if (onDrillLine)
                        {
                            shieldDirectHit++;
                            totalDamage += 1;
                            hpSum += goblin.hp;
                        }
                        continue; // 낙하 대미지 제외
                    }

                    int damage = 0;
                    if (onDrillLine) damage += 1;

                    // 2. 낙하 대미지 (방패 아닌 고블린만)
                    if (destroyedRByCol.ContainsKey(gq))
                    {
                        foreach (int dr in destroyedRByCol[gq])
                        {
                            if (dr >= gr) damage++;
                        }
                    }

                    if (damage > 0)
                    {
                        totalDamage += damage;
                        hpSum += goblin.hp;
                        if (goblin.isArcher) archerCount++;
                        if (goblin.isArmored) armoredCount++;
                    }
                }

                if (totalDamage == 0 && shieldDirectHit == 0) continue;

                int edgeDist = Mathf.Abs(candidate.q);

                // ★ 우선순위: 방패직접타격 → 활 → 갑옷 → totalDamage → hpSum → edgeDist → q
                bool isBetter = false;
                if (bestGoblinCount == 0 && bestShieldDirectHit <= 0)
                {
                    isBetter = true;
                }
                else if (shieldDirectHit > bestShieldDirectHit)
                {
                    isBetter = true;
                }
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
                            if (totalDamage > bestGoblinCount)
                                isBetter = true;
                            else if (totalDamage == bestGoblinCount)
                            {
                                if (hpSum > bestHpSum)
                                    isBetter = true;
                                else if (hpSum == bestHpSum)
                                {
                                    if (edgeDist > bestEdgeDist)
                                        isBetter = true;
                                    else if (edgeDist == bestEdgeDist && candidate.q < bestQ)
                                        isBetter = true;
                                }
                            }
                        }
                    }
                }

                if (isBetter)
                {
                    bestGoblinCount = totalDamage;
                    bestShieldDirectHit = shieldDirectHit;
                    bestArcherCount = archerCount;
                    bestArmoredCount = armoredCount;
                    bestPos = candidate;
                    bestHpSum = hpSum;
                    bestEdgeDist = edgeDist;
                    bestQ = candidate.q;
                }
            }

            if (bestGoblinCount > 0 || bestShieldDirectHit > 0)
                Debug.Log($"[ComboSystem] FindBestGoblinDrillLine: 최적 위치={bestPos}, 방패직접={bestShieldDirectHit}, 활={bestArcherCount}, 갑옷={bestArmoredCount}, 대미지={bestGoblinCount}, HP합={bestHpSum}");
        }

        /// <summary>
        /// 드론+드릴 콤보 최적 타겟: 드릴 라인(양방향)에 미션 블록이 가장 많은 위치 선정
        /// </summary>
        private HexBlock FindBestDrillComboTarget(HexCoord fromPos, DrillDirection drillDir)
        {
            if (hexGrid == null || drillSystem == null) return FindBestTarget(fromPos, new HashSet<HexBlock>());

            HashSet<GemType> missionGemTargets = GetComboMissionTargetGemTypes();
            bool hasObstacleMission = HasComboObstacleMission();

            // 미션 타겟이 없으면 기존 로직 사용
            if (missionGemTargets.Count == 0 && !hasObstacleMission)
                return FindBestTarget(fromPos, new HashSet<HexBlock>());

            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                if (block.Data.specialType == SpecialBlockType.Drone) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) return null;

            HexBlock bestTarget = null;
            int bestScore = -1;

            foreach (var candidate in candidates)
            {
                HexCoord pos = candidate.Coord;
                int missionHitCount = 0;

                // 후보 블록 자체가 미션 블록인지
                if (IsMissionRelevantBlock(candidate, missionGemTargets, hasObstacleMission))
                    missionHitCount++;

                // 양쪽 드릴 라인 시뮬레이션
                bool[] sides = { true, false };
                foreach (bool positive in sides)
                {
                    List<HexBlock> lineBlocks = drillSystem.GetBlocksInDirectionPublic(pos, drillDir, positive);
                    foreach (var lb in lineBlocks)
                    {
                        if (lb == null || lb.Data == null) continue;
                        if (IsMissionRelevantBlock(lb, missionGemTargets, hasObstacleMission))
                            missionHitCount++;
                    }
                }

                // 미션 히트 수가 같으면 기존 우선순위로 타이브레이크
                int priorityScore = GetComboTargetPriority(candidate, missionGemTargets, hasObstacleMission);
                int combinedScore = missionHitCount * 10000 + priorityScore;

                if (combinedScore > bestScore)
                {
                    bestScore = combinedScore;
                    bestTarget = candidate;
                }
            }

            if (bestTarget != null)
            {
                // 시뮬레이션 결과 로그
                int finalHits = bestScore / 10000;
                Debug.Log($"[ComboSystem] DrillCombo 최적 타겟: {bestTarget.Coord} (드릴방향={drillDir}, 미션블록 {finalHits}개 적중 예상)");
            }

            return bestTarget ?? FindBestTarget(fromPos, new HashSet<HexBlock>());
        }

        /// <summary>
        /// 드론+폭탄 콤보 최적 타겟:
        /// 1순위: 고블린 존재 시 → 폭발 범위(Ring1+Ring2)에 고블린이 가장 많은 위치
        ///        (몬스터 없는 블록이라도 폭파 범위 고블린이 더 많으면 선택)
        ///        동률: HP합 → 가장자리 → 왼쪽
        /// 2순위: 고블린 없음 → 미션 블록 최적화
        /// </summary>
        private HexBlock FindBestBombComboTarget(HexCoord fromPos)
        {
            if (hexGrid == null) return FindBestTarget(fromPos, new HashSet<HexBlock>());

            // ── 1순위: 고블린 폭발 범위 타겟팅 ──
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive
                && GoblinSystem.Instance.AliveCount > 0)
            {
                HexBlock goblinTarget = FindBestBombGoblinTarget(fromPos);
                if (goblinTarget != null)
                    return goblinTarget;
            }

            // ── 2순위: 미션 블록 최적화 (기존 로직) ──
            HashSet<GemType> missionGemTargets = GetComboMissionTargetGemTypes();
            bool hasObstacleMission = HasComboObstacleMission();

            if (missionGemTargets.Count == 0 && !hasObstacleMission)
                return FindBestTarget(fromPos, new HashSet<HexBlock>());

            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) return null;

            HexBlock bestTarget = null;
            int bestScore = -1;

            foreach (var candidate in candidates)
            {
                HexCoord pos = candidate.Coord;
                int missionHitCount = 0;

                if (IsMissionRelevantBlock(candidate, missionGemTargets, hasObstacleMission))
                    missionHitCount++;

                var hexesInRange = HexCoord.GetHexesInRadius(pos, 2);
                foreach (var coord in hexesInRange)
                {
                    if (coord == pos) continue;
                    if (!hexGrid.IsValidCoord(coord)) continue;
                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block == null || block.Data == null || block.Data.gemType == GemType.None) continue;
                    if (IsMissionRelevantBlock(block, missionGemTargets, hasObstacleMission))
                        missionHitCount++;
                }

                int priorityScore = GetComboTargetPriority(candidate, missionGemTargets, hasObstacleMission);
                int combinedScore = missionHitCount * 10000 + priorityScore;

                if (combinedScore > bestScore)
                {
                    bestScore = combinedScore;
                    bestTarget = candidate;
                }
            }

            if (bestTarget != null)
            {
                int finalHits = bestScore / 10000;
                Debug.Log($"[ComboSystem] BombCombo 미션 타겟: {bestTarget.Coord} (폭발 범위 미션블록 {finalHits}개 적중 예상)");
            }

            return bestTarget ?? FindBestTarget(fromPos, new HashSet<HexBlock>());
        }

        /// <summary>
        /// 드론×폭탄 고블린 최적 타겟: 낙하 대미지까지 포함하여 총 피해 최대화
        /// 폭발 범위(Ring1+Ring2) 블록 파괴 → 열별 낙하 → 고블린 통과 대미지 추정
        /// 몬스터 없는 블록이라도 폭파 범위 낙하 대미지가 더 크면 선택
        /// 동률: HP합 → 가장자리 → 왼쪽 (일반 드론과 동일)
        /// </summary>
        private HexBlock FindBestBombGoblinTarget(HexCoord fromPos)
        {
            var aliveGoblins = GoblinSystem.Instance.GetAliveGoblins();
            if (aliveGoblins.Count == 0) return null;

            // 모든 블록을 후보로 (몬스터 없는 블록도 포함)
            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                if (block.Data.specialType == SpecialBlockType.Drone) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) return null;

            HexBlock bestTarget = null;
            int bestTotalDamage = 0;
            int bestShieldDirectHit = -1;
            int bestArcherCount = -1;
            int bestArmoredCount = -1;
            int bestHpSum = 0;
            int bestEdgeDist = -1;
            int bestQ = int.MaxValue;

            foreach (var candidate in candidates)
            {
                HexCoord bombPos = candidate.Coord;

                // 폭발 범위(Ring1+Ring2) 좌표 집합 (직접 타격 범위)
                var hexesInRange = HexCoord.GetHexesInRadius(bombPos, 2);
                var explosionRange = new HashSet<HexCoord>(hexesInRange);

                // 실제 파괴되는 블록 좌표 수집
                var destroyedSet = new HashSet<HexCoord>();
                foreach (var coord in hexesInRange)
                {
                    if (!hexGrid.IsValidCoord(coord)) continue;
                    HexBlock block = hexGrid.GetBlock(coord);
                    if (block != null && block.Data != null && block.Data.gemType != GemType.None)
                        destroyedSet.Add(coord);
                }

                if (destroyedSet.Count == 0) continue;

                // 열(q)별 파괴 블록의 r값 수집
                var destroyedRByCol = new Dictionary<int, List<int>>();
                foreach (var coord in destroyedSet)
                {
                    if (!destroyedRByCol.ContainsKey(coord.q))
                        destroyedRByCol[coord.q] = new List<int>();
                    destroyedRByCol[coord.q].Add(coord.r);
                }

                // 각 고블린별 대미지 추정 (방패 = 직접 타격만, 일반 = 직접+낙하)
                int totalDamage = 0;
                int shieldDirectHit = 0;
                int archerCount = 0;
                int armoredCount = 0;
                int hpSum = 0;

                foreach (var goblin in aliveGoblins)
                {
                    int gq = goblin.position.q;
                    int gr = goblin.position.r;

                    // 폭발 범위 안에 고블린이 있는지 (직접 타격)
                    bool inExplosion = explosionRange.Contains(goblin.position);

                    if (goblin.isShielded)
                    {
                        // ★ 방패: 폭발 범위 안에서만 직접 타격 대미지 (낙하 면역)
                        if (inExplosion)
                        {
                            shieldDirectHit++;
                            totalDamage += 1;
                            hpSum += goblin.hp;
                        }
                        continue; // 낙하 대미지 제외
                    }

                    int damage = 0;

                    // 직접 폭발 대미지
                    if (inExplosion) damage += 1;

                    // 낙하 대미지
                    if (destroyedRByCol.ContainsKey(gq))
                    {
                        foreach (int dr in destroyedRByCol[gq])
                        {
                            if (dr >= gr) damage++;
                        }
                    }

                    // 착탄 위치 직접 충돌 보너스
                    if (goblin.position == bombPos) damage += 1;

                    if (damage > 0)
                    {
                        totalDamage += damage;
                        hpSum += goblin.hp;
                        if (goblin.isArcher) archerCount++;
                        if (goblin.isArmored) armoredCount++;
                    }
                }

                if (totalDamage == 0 && shieldDirectHit == 0) continue;

                int edgeDist = Mathf.Abs(bombPos.q);

                // ★ 우선순위: 방패직접타격 → 활 → 갑옷 → totalDamage → hpSum → edgeDist → q
                bool isBetter = false;
                if (bestTarget == null)
                {
                    isBetter = true;
                }
                else if (shieldDirectHit > bestShieldDirectHit)
                {
                    isBetter = true;
                }
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
                                    else if (edgeDist == bestEdgeDist && bombPos.q < bestQ)
                                        isBetter = true;
                                }
                            }
                        }
                    }
                }

                if (isBetter)
                {
                    bestTarget = candidate;
                    bestTotalDamage = totalDamage;
                    bestShieldDirectHit = shieldDirectHit;
                    bestArcherCount = archerCount;
                    bestArmoredCount = armoredCount;
                    bestHpSum = hpSum;
                    bestEdgeDist = edgeDist;
                    bestQ = bombPos.q;
                }
            }

            if (bestTarget != null)
                Debug.Log($"[ComboSystem] BombCombo 고블린 타겟: {bestTarget.Coord} (방패직접={bestShieldDirectHit}, 활={bestArcherCount}, 갑옷={bestArmoredCount}, 대미지={bestTotalDamage}, HP합={bestHpSum})");

            return bestTarget;
        }

        /// <summary>
        /// <summary>
        /// 드론×드론 몬스터 점수: BombGoblin=5, Shield=4, Archer=3, Armored=2, Regular=1
        /// </summary>
        /// <summary>GoblinData.DroneTargetScore 중앙화 프로퍼티 사용</summary>
        private static int GoblinScoreForDroneDrone(GoblinData g)
        {
            if (g == null || !g.isAlive) return 0;
            return g.DroneTargetScore;
        }

        /// 드론×드론 콤보 스마트 타겟: 블록 단위 최적 공격 대상 선택.
        /// 아처 고블린 제외 (낙하 면역, DroneDroneCombo에서 직접 타격 처리).
        /// 킬 우선순위: 처치 가능 고블린 수 최우선.
        /// 동률: 총 대미지 → 갑옷 고블린 수 → HP 합 → 가장자리 → 왼쪽 열.
        /// excludeBlocks로 이미 선택된 블록을 제외하여 분산 공격.
        /// </summary>
        private HexBlock FindSmartDroneComboTarget(HashSet<HexBlock> excludeBlocks)
        {
            if (hexGrid == null || GoblinSystem.Instance == null) return null;

            // 모든 고블린 포함 (활 고블린도 열 우선순위 평가에 포함)
            var allGoblins = GoblinSystem.Instance.GetAliveGoblins().Where(g => g.isAlive).ToList();
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

            // 열(q)별 활 고블린 수
            var archerByCol = new Dictionary<int, int>();
            foreach (var goblin in allGoblins)
            {
                if (!goblin.isArcher) continue;
                int q = goblin.position.q;
                archerByCol[q] = archerByCol.ContainsKey(q) ? archerByCol[q] + 1 : 1;
            }

            var columnsWithGoblins = new HashSet<int>(columnGoblins.Keys);
            foreach (int q in archerByCol.Keys) columnsWithGoblins.Add(q);

            // 열(q)별 방패 고블린 수
            var shieldByCol = new Dictionary<int, int>();
            foreach (var goblin in allGoblins)
            {
                if (!goblin.isShielded) continue;
                int q = goblin.position.q;
                shieldByCol[q] = shieldByCol.ContainsKey(q) ? shieldByCol[q] + 1 : 1;
            }
            foreach (int q in shieldByCol.Keys) columnsWithGoblins.Add(q);

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
                if (excludeBlocks.Contains(block)) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;

                int q = block.Coord.q;
                if (!columnsWithGoblins.Contains(q)) continue;

                var goblinsInCol = columnGoblins.ContainsKey(q) ? columnGoblins[q] : new List<GoblinData>();

                // 충돌 판정
                GoblinData collisionGoblin = null;
                foreach (var goblin in goblinsInCol)
                {
                    if (goblin.position == block.Coord) { collisionGoblin = goblin; break; }
                }
                bool hasCollision = collisionGoblin != null;

                int shieldDirectHit = (hasCollision && collisionGoblin.isShielded) ? 1 : 0;

                // 대미지 계산 (방패 고블린은 낙하 대미지 면역 → 제외)
                int killCount = 0;
                int totalDamage = 0;
                int hpSum = 0;
                foreach (var g in goblinsInCol)
                {
                    if (g.isShielded)
                    {
                        if (hasCollision && g.position == block.Coord)
                        {
                            totalDamage += 1;
                            hpSum += g.hp;
                        }
                        continue;
                    }

                    hpSum += g.hp;
                    if (hasCollision && g.position == block.Coord)
                    {
                        totalDamage += 2;
                        if (g.hp <= 2) killCount++;
                    }
                    else
                    {
                        totalDamage += 1;
                        if (g.hp <= 1) killCount++;
                    }
                }

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
                Debug.Log($"[ComboSystem] DroneDrone 스마트 타겟: {bestBlock.Coord} (방패열={bestShieldInCol}, 방패직접={bestShieldDirectHit}, 활={bestArcherCount}, 갑옷={bestArmoredCount}, 킬={bestKillCount}, 대미지={bestTotalDamage})");

            return bestBlock;
        }

        /// <summary>
        /// 드론+드론 콤보 최적 타겟: 미션 블록을 우선 타겟 (기존 FindBestTarget에서 미션 우선순위 강화)
        /// </summary>
        private HexBlock FindBestDroneComboTarget(HexCoord fromPos, HashSet<HexBlock> excludeBlocks)
        {
            if (hexGrid == null) return null;

            HashSet<GemType> missionGemTargets = GetComboMissionTargetGemTypes();
            bool hasObstacleMission = HasComboObstacleMission();

            List<HexBlock> candidates = new List<HexBlock>();
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.gemType == GemType.None) continue;
                if (block.Data.gemType == GemType.Gray) continue; // 회색(쉘) 블록 타겟 제외
                if (excludeBlocks.Contains(block)) continue;
                if (block.Data.specialType == SpecialBlockType.Drone) continue;
                candidates.Add(block);
            }

            if (candidates.Count == 0) return null;

            // 미션 관련 블록 우선 분리
            List<HexBlock> missionBlocks = new List<HexBlock>();
            List<HexBlock> otherBlocks = new List<HexBlock>();

            foreach (var c in candidates)
            {
                if (missionGemTargets.Count > 0 && IsMissionRelevantBlock(c, missionGemTargets, hasObstacleMission))
                    missionBlocks.Add(c);
                else
                    otherBlocks.Add(c);
            }

            // 미션 블록이 있으면 미션 블록 중에서 선택
            if (missionBlocks.Count > 0)
            {
                HexCoord origin = new HexCoord(0, 0);
                missionBlocks.Sort((a, b) =>
                {
                    int prioA = GetComboTargetPriority(a, missionGemTargets, hasObstacleMission);
                    int prioB = GetComboTargetPriority(b, missionGemTargets, hasObstacleMission);
                    if (prioA != prioB) return prioB.CompareTo(prioA);
                    int distA = a.Coord.DistanceTo(origin);
                    int distB = b.Coord.DistanceTo(origin);
                    if (distA != distB) return distB.CompareTo(distA);
                    return b.Coord.r.CompareTo(a.Coord.r);
                });

                // 같은 최고 우선순위 중 랜덤 선택
                int topPrio = GetComboTargetPriority(missionBlocks[0], missionGemTargets, hasObstacleMission);
                List<HexBlock> topGroup = new List<HexBlock>();
                foreach (var m in missionBlocks)
                {
                    if (GetComboTargetPriority(m, missionGemTargets, hasObstacleMission) == topPrio)
                        topGroup.Add(m);
                }
                return topGroup[Random.Range(0, topGroup.Count)];
            }

            // 미션 블록 없으면 기존 로직
            return FindBestTarget(fromPos, excludeBlocks);
        }

        /// <summary>
        /// 블록이 미션과 관련있는지 판단 (미션 색상 매칭 또는 장애물 미션 대상)
        /// </summary>
        private bool IsMissionRelevantBlock(HexBlock block, HashSet<GemType> missionGemTargets, bool hasObstacleMission)
        {
            if (block == null || block.Data == null) return false;
            var data = block.Data;

            // 미션 색상 매칭 (기본 블록 + 티어 블록)
            if (missionGemTargets.Count > 0 && missionGemTargets.Contains(data.gemType)
                && (data.specialType == SpecialBlockType.None || data.specialType == SpecialBlockType.FixedBlock))
                return true;

            // 장애물 미션 대상
            if (hasObstacleMission)
            {
                if (data.specialType == SpecialBlockType.FixedBlock) return true;
                if (data.vinylLayer > 0) return true;
                if (data.enemyType != EnemyType.None) return true;
            }

            return false;
        }

        /// <summary>
        /// 임팩트 링 확장 후 페이드아웃
        /// </summary>
        private IEnumerator ExpandAndFade(GameObject obj, float duration, float targetSize)
        {
            if (obj == null) yield break;
            RectTransform rt = obj.GetComponent<RectTransform>();
            Image img = obj.GetComponent<Image>();
            if (rt == null || img == null) { Destroy(obj); yield break; }

            Color startColor = img.color;
            Vector2 startSize = rt.sizeDelta;
            Vector2 endSize = new Vector2(targetSize, targetSize);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (obj == null || rt == null || img == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rt.sizeDelta = Vector2.Lerp(startSize, endSize, t);
                img.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
                yield return null;
            }

            if (obj != null) Destroy(obj);
        }

        // ============================================================
        // VFX 유틸 메서드
        // ============================================================

        /// <summary>
        /// 이펙트 레이어를 Canvas 내부에 생성합니다.
        /// 이미 생성되어 있으면 건너뜁니다.
        /// </summary>
        private void SetupEffectParent()
        {
            if (effectParent != null) return;
            if (hexGrid == null) return;

            Canvas canvas = hexGrid.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            GameObject layer = new GameObject("ComboEffectLayer");
            layer.transform.SetParent(canvas.transform, false);

            RectTransform rt = layer.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            layer.transform.SetAsLastSibling();
            effectParent = layer.transform;

            Debug.Log("[ComboSystem] Effect layer created under Canvas");
        }

        /// <summary>
        /// X블록 생성 시 기존 특수 블록과 즉시 합성 실행 (스왑 애니메이션 없이).
        /// BlockRemovalSystem에서 X블록 생성 직후 호출됩니다.
        /// </summary>
        public IEnumerator ExecuteInPlaceCombo(
            HexCoord pos, Vector3 worldPos, GemType xBlockGemType,
            SpecialBlockType existingSpecialType, DrillDirection existingDrillDir)
        {
            Debug.Log($"[ComboSystem] ExecuteInPlaceCombo: pos=({pos}), xGem={xBlockGemType}, existing={existingSpecialType}");
            SetupEffectParent();

            if (existingSpecialType == SpecialBlockType.Drill)
                yield return StartCoroutine(DrillXBlockCombo(pos, worldPos, xBlockGemType, existingDrillDir));
            else if (existingSpecialType == SpecialBlockType.Bomb)
                yield return StartCoroutine(BombXBlockCombo(pos, worldPos, xBlockGemType));
            else if (existingSpecialType == SpecialBlockType.XBlock)
                yield return StartCoroutine(XBlockXBlockCombo(pos, worldPos));
            else if (existingSpecialType == SpecialBlockType.Drone)
                yield return StartCoroutine(DroneXBlockCombo(pos, worldPos, xBlockGemType));

            CleanupEffects();
        }

        /// <summary>
        /// effectParent의 모든 자식 이펙트 오브젝트를 제거하고,
        /// effectParent 자체도 제거합니다.
        /// </summary>
        private void CleanupEffects()
        {
            if (effectParent == null) return;

            for (int i = effectParent.childCount - 1; i >= 0; i--)
                Destroy(effectParent.GetChild(i).gameObject);

            Destroy(effectParent.gameObject);
            effectParent = null;
        }

        /// <summary>
        /// 화면을 랜덤하게 흔드는 코루틴.
        /// shakeCount로 중첩을 관리하며, 마지막 흔들림이 끝날 때만 원래 위치로 복원.
        /// </summary>
        private IEnumerator ScreenShake(float intensity, float duration)
        {
            // 다수 특수 블록 동시 발동 시 필드 바운스는 하나만 실행
            bool isOwner = VisualConstants.TryBeginScreenShake();
            if (!isOwner) yield break;

            Transform target = hexGrid != null ? hexGrid.transform : transform;

            if (shakeCount == 0)
                shakeOriginalPos = Vector3.zero;
            shakeCount++;

            float elapsed = 0f;

            try
            {
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
            }
            finally
            {
                shakeCount--;
                if (shakeCount <= 0)
                {
                    shakeCount = 0;
                    target.localPosition = Vector3.zero;
                }
                VisualConstants.EndScreenShake();
            }
        }

        /// <summary>
        /// 히트스톱: 게임 시간을 잠깐 멈춘 뒤 슬로모션을 거쳐 정상 복귀.
        /// 쿨다운이 있어 너무 자주 실행되지 않음.
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
        /// 줌 펀치: hexGrid.transform을 잠깐 확대했다가 원래 크기로 돌려놓습니다.
        /// </summary>
        private IEnumerator ZoomPunch(float targetScale)
        {
            // 다수 특수 블록 동시 발동 시 줌 펀치는 하나만 실행
            bool isOwner = VisualConstants.TryBeginZoomPunch();
            if (!isOwner) yield break;

            Transform target = hexGrid != null ? hexGrid.transform : transform;
            Vector3 origScale = Vector3.one;
            Vector3 punchScale = origScale * targetScale;

            // 줌인
            float elapsed = 0f;
            try
            {
                while (elapsed < VisualConstants.ZoomPunchInDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchInDuration);
                    target.localScale = Vector3.Lerp(origScale, punchScale, VisualConstants.EaseOutCubic(t));
                    yield return null;
                }

                // 줌아웃
                elapsed = 0f;
                while (elapsed < VisualConstants.ZoomPunchOutDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / VisualConstants.ZoomPunchOutDuration);
                    target.localScale = Vector3.Lerp(punchScale, origScale, VisualConstants.EaseOutCubic(t));
                    yield return null;
                }
            }
            finally
            {
                target.localScale = Vector3.one;
                VisualConstants.EndZoomPunch();
            }
        }

        /// <summary>
        /// 이중 이징 파괴 애니메이션:
        ///   전반 20%: 1.0 → 1.2 확장 (EaseOutCubic)
        ///   후반 80%: 1.2 → 0.0 축소 + 가로 찌그러짐 (EaseInQuad)
        ///   완료 후 ClearData()
        /// </summary>
        private IEnumerator DualEasingDestroy(HexBlock block, float duration = 0.3f)
        {
            if (block == null) yield break;

            float elapsed = 0f;
            float expandRatio = VisualConstants.DestroyExpandPhaseRatio;
            Vector3 origScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (t < expandRatio)
                {
                    // 전반부: 확장
                    float expandT = t / expandRatio;
                    float scale = 1f + (VisualConstants.DestroyExpandScale - 1f) * VisualConstants.EaseOutCubic(expandT);
                    block.transform.localScale = origScale * scale;
                }
                else
                {
                    // 후반부: 찌그러짐 + 축소
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

        /// <summary>
        /// 블록 파괴 시 백색 플래시 오버레이 생성.
        /// effectParent 자식으로 생성되며, 0.15초 동안 페이드아웃.
        /// </summary>
        private IEnumerator DestroyFlash(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            GameObject flash = new GameObject("ComboDestroyFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;

            var img = flash.AddComponent<Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha);

            RectTransform rt = flash.GetComponent<RectTransform>();
            float size = 60f * VisualConstants.DestroyFlashSizeMultiplier;
            rt.sizeDelta = new Vector2(size, size);

            float elapsed = 0f;
            float duration = 0.15f;

            while (elapsed < duration)
            {
                // 외부에서 flash가 먼저 파괴된 경우 코루틴 종료
                if (flash == null || img == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                img.color = new Color(1f, 1f, 1f, VisualConstants.DestroyFlashAlpha * (1f - t));

                // 약간 확장
                float scale = 1f + t * 0.3f;
                rt.sizeDelta = new Vector2(size * scale, size * scale);

                yield return null;
            }

            if (flash != null) Destroy(flash);
        }

        /// <summary>
        /// 파편 입자를 생성하는 코루틴.
        /// 사방으로 흩어지며 중력으로 떨어지고, 회전하면서 투명해집니다.
        /// </summary>
        private IEnumerator SpawnDebris(Vector3 pos, Color color, int count = 6)
        {
            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            for (int i = 0; i < count; i++)
            {
                StartCoroutine(AnimateDebrisParticle(pos, color, parent));
            }
            yield break;
        }

        /// <summary>
        /// 개별 파편 입자 애니메이션.
        /// </summary>
        private IEnumerator AnimateDebrisParticle(Vector3 center, Color color, Transform parent)
        {
            GameObject debris = new GameObject("ComboDebris");
            debris.transform.SetParent(parent, false);
            debris.transform.position = center;

            var image = debris.AddComponent<Image>();
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
            float w = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax);
            float h = Random.Range(VisualConstants.DebrisBaseSizeMin, VisualConstants.DebrisBaseSizeMax * 0.9f);
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
                Vector3 p = debris.transform.position;
                p.x += velocity.x * Time.deltaTime;
                p.y += velocity.y * Time.deltaTime;
                debris.transform.position = p;

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

        /// <summary>
        /// 블록 변환 시 플래시+링+스파크 이펙트.
        /// XBlock 합성에서 블록이 특수 블록으로 변환될 때 사용.
        /// </summary>
        private IEnumerator TransformFlash(Vector3 pos, Color color)
        {
            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);
            Color brightColor = VisualConstants.Brighten(color);

            // ── 1. 중앙 플래시 (빠른 팽창 + 페이드아웃) ──
            GameObject flash = new GameObject("TransformFlash");
            flash.transform.SetParent(parent, false);
            flash.transform.position = pos;
            var flashImg = flash.AddComponent<Image>();
            flashImg.sprite = HexBlock.GetHexFlashSprite();
            flashImg.raycastTarget = false;
            flashImg.color = new Color(1f, 1f, 1f, 0.95f); // 밝은 백색 시작
            RectTransform flashRt = flash.GetComponent<RectTransform>();
            float flashSize = 30f;
            flashRt.sizeDelta = new Vector2(flashSize, flashSize);

            // ── 2. 확산 링 (테두리 스프라이트로 원형 파동) ──
            GameObject ring = new GameObject("TransformRing");
            ring.transform.SetParent(parent, false);
            ring.transform.position = pos;
            var ringImg = ring.AddComponent<Image>();
            ringImg.sprite = HexBlock.GetHexBorderSprite();
            ringImg.raycastTarget = false;
            ringImg.color = new Color(brightColor.r, brightColor.g, brightColor.b, 0.8f);
            RectTransform ringRt = ring.GetComponent<RectTransform>();
            ringRt.sizeDelta = new Vector2(10f, 10f);

            // ── 3. 작은 스파크 4개 ──
            GameObject[] sparks = new GameObject[4];
            Vector2[] sparkDirs = {
                new Vector2(1f, 0.5f).normalized,
                new Vector2(-1f, 0.5f).normalized,
                new Vector2(0.5f, -1f).normalized,
                new Vector2(-0.5f, -1f).normalized
            };
            for (int i = 0; i < 4; i++)
            {
                sparks[i] = new GameObject("TransformSpark");
                sparks[i].transform.SetParent(parent, false);
                sparks[i].transform.position = pos;
                var sImg = sparks[i].AddComponent<Image>();
                sImg.sprite = HexBlock.GetHexFlashSprite();
                sImg.raycastTarget = false;
                sImg.color = brightColor;
                RectTransform sRt = sparks[i].GetComponent<RectTransform>();
                sRt.sizeDelta = new Vector2(8f, 8f);
            }

            // ── 애니메이션 (0.18초) ──
            float elapsed = 0f;
            float duration = 0.18f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 플래시: 급팽창 후 페이드
                float flashScale = 1f + t * 2.5f;
                flashRt.sizeDelta = new Vector2(flashSize * flashScale, flashSize * flashScale);
                float flashAlpha = t < 0.3f ? 0.95f : 0.95f * (1f - (t - 0.3f) / 0.7f);
                Color fc = Color.Lerp(Color.white, brightColor, t);
                fc.a = Mathf.Max(0f, flashAlpha);
                flashImg.color = fc;

                // 링: 확산 + 페이드
                float ringScale = 10f + t * 70f;
                ringRt.sizeDelta = new Vector2(ringScale, ringScale);
                ringImg.color = new Color(brightColor.r, brightColor.g, brightColor.b, 0.8f * (1f - t));

                // 스파크: 바깥으로 사출 + 축소 + 페이드
                for (int i = 0; i < 4; i++)
                {
                    if (sparks[i] == null) continue;
                    float dist = t * 35f;
                    sparks[i].transform.position = pos + (Vector3)(sparkDirs[i] * dist);
                    float sScale = Mathf.Max(0f, 1f - t * 1.2f);
                    sparks[i].GetComponent<RectTransform>().sizeDelta = new Vector2(8f * sScale, 8f * sScale);
                    var sImg = sparks[i].GetComponent<Image>();
                    Color sc = brightColor;
                    sc.a = Mathf.Max(0f, 1f - t * 1.5f);
                    sImg.color = sc;
                }

                yield return null;
            }

            Destroy(flash);
            Destroy(ring);
            for (int i = 0; i < 4; i++)
                if (sparks[i] != null) Destroy(sparks[i]);
        }

        // ── XBlock 합성 전용: 변환 스케일 펀치 + 빨간 테두리 표시/해제 ──

        /// <summary>
        /// 블록이 특수 블록으로 변환될 때 짧은 스케일 펀치 (1.0 → 1.25 → 1.0)
        /// </summary>
        private IEnumerator TransformScalePunch(HexBlock block)
        {
            if (block == null) yield break;

            float elapsed = 0f;
            float duration = 0.12f;
            Vector3 originalScale = block.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 빠른 팽창 후 복귀: sin 곡선
                float punch = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
                block.transform.localScale = originalScale * punch;
                yield return null;
            }

            if (block != null)
                block.transform.localScale = originalScale;
        }

        /// <summary>
        /// 블록에 빨간 경고 테두리(오버레이)를 추가한다.
        /// 발동 직전 0.3초 동안 유지되어 플레이어에게 실행 예고를 알린다.
        /// </summary>
        private void SetRedBorder(HexBlock block)
        {
            if (block == null) return;

            // 기존 오버레이 제거 (중복 방지)
            ClearRedBorder(block);

            GameObject border = new GameObject("ComboRedBorder");
            border.transform.SetParent(block.transform, false);
            RectTransform rt = border.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-2f, -2f);
            rt.offsetMax = new Vector2(2f, 2f);

            Image img = border.AddComponent<Image>();
            img.sprite = HexBlock.GetHexBorderSprite();
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1f, 0.15f, 0.1f, 0.95f); // 선명한 빨간색
        }

        /// <summary>
        /// SetRedBorder로 추가한 빨간 테두리 오버레이를 제거한다.
        /// </summary>
        private void ClearRedBorder(HexBlock block)
        {
            if (block == null) return;
            Transform existing = block.transform.Find("ComboRedBorder");
            if (existing != null)
                Destroy(existing.gameObject);
        }

        /// <summary>
        /// 합성 폭발 파동 이펙트.
        /// BombBomb 합성에서 링별 폭발 시 사용하는 충격파 원형 파동.
        /// </summary>
        private IEnumerator ComboExplosionWave(Vector3 pos, Color color, float intensityMult)
        {
            Transform parent = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            GameObject wave = new GameObject("ComboWave");
            wave.transform.SetParent(parent, false);
            wave.transform.position = pos;

            var img = wave.AddComponent<Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = new Color(color.r, color.g, color.b, 0.6f * intensityMult);

            RectTransform rt = wave.GetComponent<RectTransform>();
            float initSize = VisualConstants.WaveLargeInitialSize;
            rt.sizeDelta = new Vector2(initSize, initSize);

            float duration = VisualConstants.WaveLargeDuration;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (wave == null || rt == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.WaveLargeExpand * intensityMult - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                img.color = new Color(color.r, color.g, color.b, 0.6f * intensityMult * (1f - t));
                yield return null;
            }

            if (wave != null) Destroy(wave);
        }

        /// <summary>
        /// 충격파 링 이펙트 — 화면 밖까지 퍼지는 원형 파동.
        /// 타겟+타겟 콤보에서 고블린 데미지 연출용.
        /// </summary>
        private IEnumerator ShockwaveRingEffect(Vector3 center, float targetRadius, Color color)
        {
            Transform par = effectParent != null ? effectParent : (hexGrid != null ? hexGrid.transform : transform);

            GameObject ring = new GameObject("ShockwaveRing");
            ring.transform.SetParent(par, false);
            ring.transform.position = center;

            var img = ring.AddComponent<Image>();
            img.sprite = HexBlock.GetHexFlashSprite();
            img.raycastTarget = false;
            img.color = color;

            RectTransform rt = ring.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(20f, 20f);

            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);

                float size = Mathf.Lerp(20f, targetRadius, eased);
                rt.sizeDelta = new Vector2(size, size);
                img.color = new Color(color.r, color.g, color.b, color.a * (1f - t));
                yield return null;
            }

            Destroy(ring);
        }

        // ============================================================
        // 좌표/방향 유틸 메서드
        // ============================================================

        /// <summary>
        /// HexCoord delta → DrillDirection + positive 변환.
        /// Ring1의 각 이웃에서 바깥 방향 드릴을 발사할 때 사용.
        /// </summary>
        private (DrillDirection dir, bool positive) DeltaToDrillDirection(HexCoord delta)
        {
            // Vertical: r축 (0, +1) / (0, -1)
            if (delta.q == 0 && delta.r > 0) return (DrillDirection.Vertical, true);
            if (delta.q == 0 && delta.r < 0) return (DrillDirection.Vertical, false);

            // Slash: s축 (+1, -1) / (-1, +1)
            if (delta.q > 0 && delta.r < 0) return (DrillDirection.Slash, true);
            if (delta.q < 0 && delta.r > 0) return (DrillDirection.Slash, false);

            // BackSlash: q축 (+1, 0) / (-1, 0)
            if (delta.q > 0 && delta.r == 0) return (DrillDirection.BackSlash, true);
            if (delta.q < 0 && delta.r == 0) return (DrillDirection.BackSlash, false);

            return (DrillDirection.Vertical, true); // 기본값
        }

        /// <summary>
        /// HexCoord 좌표에 위치한 블록의 월드 좌표를 반환합니다.
        /// 블록이 있으면 RectTransform.position 사용, 없으면 좌표 계산.
        /// </summary>
        private Vector3 GetWorldPosition(HexCoord coord)
        {
            if (hexGrid != null)
            {
                HexBlock block = hexGrid.GetBlock(coord);
                if (block != null)
                    return block.transform.position;
            }

            // 블록이 없는 경우 수학적 계산
            float hexSize = 50f; // 기본 hexSize
            Vector2 pos2D = coord.ToWorldPosition(hexSize);
            return new Vector3(pos2D.x, pos2D.y, 0);
        }

        /// <summary>
        /// HexBlock의 월드 좌표를 반환합니다.
        /// </summary>
        private Vector3 GetBlockWorldPosition(HexBlock block)
        {
            if (block != null)
                return block.transform.position;
            return Vector3.zero;
        }

        // ============================================================
        // 점수/미션 유틸 메서드
        // ============================================================

        /// <summary>
        /// 블록의 기본 점수를 반환합니다.
        /// </summary>
        private int CalculateBlockScore(HexBlock block)
        {
            if (block == null || block.Data == null) return 0;
            return ScoreCalculator.GetBlockBaseScore(block.Data.tier);
        }

        /// <summary>
        /// 블록의 젬 타입을 색상별 카운트 딕셔너리에 누적하고,
        /// 미션 시스템에 개별 보고합니다 (Stage/Infinite 모두 지원).
        /// </summary>
        private void CollectGemCount(HexBlock block, Dictionary<GemType, int> gemCounts)
        {
            if (block == null || block.Data == null) return;
            if (block.Data.gemType == GemType.None) return;

            // 미션 카운팅: 블록 파괴 시점에 1개씩 개별 보고 (Stage/Infinite 모두 지원)
            GameManager.Instance?.OnSingleGemDestroyedForMission(block.Data.gemType);

            // 기본 블록(GemType 1~5: Red, Blue, Green, Yellow, Purple)만 카운트
            int gemValue = (int)block.Data.gemType;
            if (gemValue >= 1 && gemValue <= 5)
            {
                if (gemCounts.ContainsKey(block.Data.gemType))
                    gemCounts[block.Data.gemType]++;
                else
                    gemCounts[block.Data.gemType] = 1;
            }
        }
    }
}

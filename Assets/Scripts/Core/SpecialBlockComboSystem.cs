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

                    // 드릴 투사체 발사 (병렬)
                    if (normalTargets.Count > 0)
                    {
                        allCoroutines.Add(StartCoroutine(
                            drillSystem.DrillLineWithProjectilePublic(
                                worldPos, normalTargets, axis, positive, comboColor, true)));
                    }
                }
            }

            // 화면 흔들림 (Large)
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBombSound();

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

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
                        if (hexGrid != null && hexGrid.IsValidCoord(candidate) && !launchSet.Contains(candidate))
                        {
                            // Ring2 경계 또는 바로 바깥에 있는 좌표만 (거리 2~3)
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

                    if (filteredTargets.Count > 0)
                    {
                        Vector3 launchWorldPos = GetWorldPosition(launchCoord);
                        drillCoroutines.Add(StartCoroutine(
                            drillSystem.DrillLineWithProjectilePublic(
                                launchWorldPos, filteredTargets, drillDir, positive, comboColor, true)));
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
                }

                // 화면 흔들림 (강도 점감)
                float intensity = Mathf.Max(0.3f, 1.0f - ring * 0.12f);
                StartCoroutine(ScreenShake(
                    VisualConstants.ShakeMediumIntensity * intensity,
                    VisualConstants.ShakeMediumDuration * intensity));

                // 링 간 대기
                yield return new WaitForSeconds(0.08f);
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
        /// 드론+드론 합성: 우선순위 기반으로 3개 타겟을 순차 타격.
        /// 각 타격마다 드론 비행 + 티어 강등 적용.
        /// </summary>
        private IEnumerator DroneDroneCombo(HexCoord pos, Vector3 worldPos, GemType color)
        {
            SetupEffectParent();

            Color comboColor = GemColors.GetColor(color);
            int blockScoreSum = 0;
            var gemCountsByColor = new Dictionary<GemType, int>();

            // 히트스톱 + 줌펀치
            StartCoroutine(HitStop(VisualConstants.HitStopDurationMedium));
            StartCoroutine(ZoomPunch(VisualConstants.ZoomPunchScaleSmall));

            // 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDroneSound();

            // 5개 드론 동시 비행 타격
            int strikes = 5;
            HashSet<HexBlock> alreadyStruck = new HashSet<HexBlock>();

            // 1) 타겟 5개 미리 선정 (미션 블록 우선)
            List<HexBlock> targets = new List<HexBlock>();
            for (int i = 0; i < strikes; i++)
            {
                HexBlock target = FindBestDroneComboTarget(pos, alreadyStruck);
                if (target == null)
                {
                    Debug.Log($"[ComboSystem] DroneDrone: 타겟 {i + 1} - 타겟 없음");
                    break;
                }
                alreadyStruck.Add(target);
                targets.Add(target);

                // 점수 수집
                if (target.Data != null && target.Data.specialType == SpecialBlockType.None)
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
                    CollectGemCount(target, gemCountsByColor);
                }
            }

            if (targets.Count == 0)
            {
                Debug.Log("[ComboSystem] DroneDrone: 타겟 없음, 종료");
                OnComboComplete?.Invoke(400);
                yield break;
            }

            // 2) 5개 드론 동시 비행 시작 (DroneBlockSystem의 정식 비행 이펙트 사용)
            List<Coroutine> flyCoroutines = new List<Coroutine>();
            for (int i = 0; i < targets.Count; i++)
            {
                Vector3 targetPos = targets[i].transform.position;
                float delay = i * 0.06f;
                if (droneSystem != null)
                    flyCoroutines.Add(StartCoroutine(droneSystem.PlayDroneFlyEffectWithDelay(worldPos, targetPos, comboColor, delay)));
            }

            // 3) 모든 드론 비행 완료 대기 (이륙0.264 + 호버0.198 + 비행0.396 + 딜레이)
            float maxWait = (targets.Count - 1) * 0.06f + 0.86f;
            yield return new WaitForSeconds(maxWait);

            // 4) 모든 타겟에 동시 타격 적용
            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            for (int i = 0; i < targets.Count; i++)
            {
                HexBlock target = targets[i];
                if (target == null || target.Data == null) continue;

                // 타격 플래시
                StartCoroutine(DestroyFlash(target.transform.position, comboColor));

                // 타격 효과
                if (target.Data != null && target.Data.specialType != SpecialBlockType.None &&
                    target.Data.specialType != SpecialBlockType.FixedBlock)
                {
                    target.SetPendingActivation();
                    target.StartWarningBlink(10f);
                }
                else
                {
                    ApplyDroneStrikeCombo(target);
                }
            }

            yield return new WaitForSeconds(0.1f);

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

            int totalScore = 500 + blockScoreSum;
            Debug.Log($"[ComboSystem] DroneDrone complete. Targets={targets.Count}, Score={totalScore}");
            OnComboComplete?.Invoke(totalScore);
        }

        // ============================================================
        // 합성 조합 8: 드론 + 드릴
        // 드론이 타겟 블록까지 비행 후 해당 블록에서 3축 드릴 발사
        // ============================================================

        /// <summary>
        /// 드론+드릴 합성: 드론이 우선순위 타겟까지 비행 후,
        /// 타겟 위치에서 원래 드릴 방향(drillDir) 양쪽 2방향 드릴 발사
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

            // 미션 최적화 타겟 선택: 드릴 라인에 미션 블록이 가장 많은 위치
            HexBlock target = FindBestDrillComboTarget(pos, drillDir);
            HexCoord drillPos = pos;
            Vector3 drillWorldPos = worldPos;

            if (target != null)
            {
                drillPos = target.Coord;
                drillWorldPos = target.transform.position;

                // 드론 비행 이펙트 (정식 이륙→호버→급강하)
                if (droneSystem != null)
                    yield return StartCoroutine(droneSystem.PlayDroneFlyEffect(worldPos, drillWorldPos, comboColor));
                else
                    yield return new WaitForSeconds(0.4f);

                // 타겟 블록 파괴 (1데미지)
                if (target.Data != null)
                {
                    blockScoreSum += ScoreCalculator.GetBlockBaseScore(target.Data.tier);
                    CollectGemCount(target, gemCountsByColor);
                    StartCoroutine(DestroyFlash(drillWorldPos, comboColor));
                    target.ClearData();
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

                if (normalTargets.Count > 0)
                {
                    allCoroutines.Add(StartCoroutine(
                        drillSystem.DrillLineWithProjectilePublic(
                            drillWorldPos, normalTargets, drillDir, positive, comboColor, true)));
                }
            }

            StartCoroutine(ScreenShake(VisualConstants.ShakeLargeIntensity, VisualConstants.ShakeLargeDuration));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayDrillSound();

            // 미션 카운팅은 CollectGemCount()에서 OnSingleGemDestroyedForMission()으로 개별 처리

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

            // 미션 최적화 타겟 선택: 폭발 범위에 미션 블록이 가장 많은 위치
            HexBlock target = FindBestBombComboTarget(pos);
            HexCoord bombPos = pos;
            Vector3 bombWorldPos = worldPos;

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

            // 변환된 모든 드론 동시 발동
            foreach (var block in colorTargets)
            {
                if (block == null || block.Data == null) continue;
                if (block.Data.specialType != SpecialBlockType.Drone) continue;

                ClearRedBorder(block);
                if (droneSystem != null)
                    droneSystem.ActivateDrone(block);
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
        /// 드론+폭탄 콤보 최적 타겟: Ring1+Ring2 폭발 범위에 미션 블록이 가장 많은 위치 선정
        /// </summary>
        private HexBlock FindBestBombComboTarget(HexCoord fromPos)
        {
            if (hexGrid == null) return FindBestTarget(fromPos, new HashSet<HexBlock>());

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

                // 후보 블록 자체
                if (IsMissionRelevantBlock(candidate, missionGemTargets, hasObstacleMission))
                    missionHitCount++;

                // Ring1 + Ring2 범위 시뮬레이션 (반경 2)
                var hexesInRange = HexCoord.GetHexesInRadius(pos, 2);
                foreach (var coord in hexesInRange)
                {
                    if (coord == pos) continue; // 중심 제외 (이미 카운트)
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
                Debug.Log($"[ComboSystem] BombCombo 최적 타겟: {bestTarget.Coord} (폭발 범위 미션블록 {finalHits}개 적중 예상)");
            }

            return bestTarget ?? FindBestTarget(fromPos, new HashSet<HexBlock>());
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
            VisualConstants.EndScreenShake();
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
            Vector3 origScale = target.localScale;
            Vector3 punchScale = origScale * targetScale;

            // 줌인
            float elapsed = 0f;
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

            target.localScale = origScale;
            VisualConstants.EndZoomPunch();
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
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = VisualConstants.EaseOutCubic(t);
                float scale = 1f + eased * (VisualConstants.WaveLargeExpand * intensityMult - 1f);
                rt.sizeDelta = new Vector2(initSize * scale, initSize * scale);
                img.color = new Color(color.r, color.g, color.b, 0.6f * intensityMult * (1f - t));
                yield return null;
            }

            Destroy(wave);
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

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    public class RotationSystem : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float rotationDuration = 0.3f;
        [SerializeField] private AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("References")]
        [SerializeField] private HexGrid hexGrid;
        [SerializeField] private MatchingSystem matchingSystem;

        private bool clockwiseRotation = true;
        private bool isRotating = false;
        private int lastRotationCost = 1;
        private bool oneTimeCounterClockwise = false; // 1회성 반시계 회전 플래그

        public event System.Action<bool> OnRotationComplete;
        public event System.Action OnRotationStarted;
        /// <summary>매칭 감지 시 발생 — int: 매칭된 총 블록 수 (GameSoundController에서 구독)</summary>
        public event System.Action<int> OnMatchDetected;

        public bool IsRotating => isRotating;
        public bool IsClockwise => clockwiseRotation && !oneTimeCounterClockwise;
        public bool IsOneTimeCounterClockwiseActive => oneTimeCounterClockwise;
        public int LastRotationCost => lastRotationCost;

        /// <summary>마지막 매칭이 발생한 블록들의 월드 좌표 중심점 (ReverseRotationItem 등에서 팝업 위치로 사용)</summary>
        public Vector3 LastMatchedWorldCenter { get; private set; }

        private void Start()
        {
            if (hexGrid == null)
            {
                hexGrid = FindObjectOfType<HexGrid>();
                if (hexGrid != null)
                    Debug.Log("[RotationSystem] HexGrid auto-found: " + hexGrid.name);
            }

            if (matchingSystem == null)
            {
                matchingSystem = FindObjectOfType<MatchingSystem>();
                if (matchingSystem != null)
                    Debug.Log("[RotationSystem] MatchingSystem auto-found: " + matchingSystem.name);
            }
        }

        public void ToggleRotationDirection()
        {
            clockwiseRotation = !clockwiseRotation;
        }

        public void SetRotationDirection(bool clockwise)
        {
            clockwiseRotation = clockwise;
        }

        /// <summary>
        /// 1회성 반시계 회전 설정 (아이템 사용 시 호출).
        /// 다음 회전 1회만 반시계로 실행 후 자동 리셋됩니다.
        /// </summary>
        public void SetOneTimeCounterClockwise()
        {
            oneTimeCounterClockwise = true;
            Debug.Log("[RotationSystem] 1회성 반시계 회전 활성화");
        }

        /// <summary>
        /// 1회성 반시계 회전 해제 (아이템 비활성화 시 호출).
        /// 역회전 아이템을 사용하지 않고 비활성화할 때 플래그를 초기화합니다.
        /// </summary>
        public void ClearOneTimeCounterClockwise()
        {
            oneTimeCounterClockwise = false;
            Debug.Log("[RotationSystem] 1회성 반시계 회전 해제");
        }

/// <summary>
        /// 강제 리셋 - stuck 복구 시 호출
        /// </summary>
        public void ForceReset()
        {
            StopAllCoroutines();
            isRotating = false;
            Debug.Log("[RotationSystem] ForceReset called");
        }


        public void TryRotate(HexBlock block1, HexBlock block2, HexBlock block3)
        {
            if (isRotating) return;
            if (block1 == null || block2 == null || block3 == null) return;
            if (!IsValidTriangle(block1, block2, block3))
            {
                Debug.LogWarning($"[RotationSystem] REJECTED: Not a valid triangle! " +
                    $"({block1.Coord}↔{block2.Coord}={block1.Coord.DistanceTo(block2.Coord)}, " +
                    $"{block2.Coord}↔{block3.Coord}={block2.Coord.DistanceTo(block3.Coord)}, " +
                    $"{block1.Coord}↔{block3.Coord}={block1.Coord.DistanceTo(block3.Coord)})");
                return;
            }
            if (!CanRotate(block1, block2, block3))
            {
                // 흙더미·사슬·고정블록 등 회전 차단 사유 안내
                bool hasDirt = (block1.Data?.dirtMound > 0) || (block2.Data?.dirtMound > 0) || (block3.Data?.dirtMound > 0);
                bool hasChain = (block1.Data?.hasChain == true) || (block2.Data?.hasChain == true) || (block3.Data?.hasChain == true);
                bool hasFixed = (block1.Data?.specialType == SpecialBlockType.FixedBlock)
                             || (block2.Data?.specialType == SpecialBlockType.FixedBlock)
                             || (block3.Data?.specialType == SpecialBlockType.FixedBlock);
                if (UIManager.Instance != null)
                {
                    if (hasDirt) UIManager.Instance.ShowToast("<color=#A06030>흙더미</color>가 있어 회전할 수 없어요!");
                    else if (hasChain) UIManager.Instance.ShowToast("사슬에 묶인 블록이 있어 회전 불가!");
                    else if (hasFixed) UIManager.Instance.ShowToast("고정 블록이 있어 회전 불가!");
                }
                return;
            }

            // ★ 회전 시작 전, 클러스터의 모든 블록에서 MP 부족 흔들림 코루틴 중단
            //   흔들림이 anchoredPosition을 덮어쓰는 중에 회전 트윈이 시작되면 위치 충돌로
            //   블록이 셀 좌표에서 벗어난 위치에 고정되는 버그 방지.
            block1.CancelInsufficientShake();
            block2.CancelInsufficientShake();
            block3.CancelInsufficientShake();

            // 블록을 시계방향 순서로 정렬
            HexBlock[] sorted = SortBlocksClockwise(block1, block2, block3);

            StartCoroutine(RotateCoroutine(sorted[0], sorted[1], sorted[2]));
        }

        /// <summary>
        /// RectTransform의 anchoredPosition을 안전하게 가져오기
        /// UI 블록은 anchoredPosition으로 배치되므로 localPosition 대신 사용
        /// </summary>
        private Vector2 GetAnchoredPos(HexBlock block)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            return rt != null ? rt.anchoredPosition : (Vector2)block.transform.localPosition;
        }

        private void SetAnchoredPos(HexBlock block, Vector2 pos)
        {
            RectTransform rt = block.GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = pos;
            else block.transform.localPosition = new Vector3(pos.x, pos.y, 0);
        }

        /// <summary>
        /// 3개 블록을 시계방향 순서로 정렬
        /// RectTransform.anchoredPosition 기준으로 각도 계산
        /// </summary>
        private HexBlock[] SortBlocksClockwise(HexBlock a, HexBlock b, HexBlock c)
        {
            Vector2 posA = GetAnchoredPos(a);
            Vector2 posB = GetAnchoredPos(b);
            Vector2 posC = GetAnchoredPos(c);

            Vector2 center = (posA + posB + posC) / 3f;

            float angleA = Mathf.Atan2(posA.y - center.y, posA.x - center.x) * Mathf.Rad2Deg;
            float angleB = Mathf.Atan2(posB.y - center.y, posB.x - center.x) * Mathf.Rad2Deg;
            float angleC = Mathf.Atan2(posC.y - center.y, posC.x - center.x) * Mathf.Rad2Deg;

            HexBlock[] arr = { a, b, c };
            float[] angles = { angleA, angleB, angleC };

            // 각도 내림차순 정렬 (시계방향)
            for (int i = 0; i < 2; i++)
            {
                for (int j = i + 1; j < 3; j++)
                {
                    if (angles[j] > angles[i])
                    {
                        float tempA = angles[i]; angles[i] = angles[j]; angles[j] = tempA;
                        HexBlock tempB = arr[i]; arr[i] = arr[j]; arr[j] = tempB;
                    }
                }
            }

            return arr;
        }

        private bool IsValidTriangle(HexBlock a, HexBlock b, HexBlock c)
        {
            bool ab = a.Coord.DistanceTo(b.Coord) == 1;
            bool bc = b.Coord.DistanceTo(c.Coord) == 1;
            bool ac = a.Coord.DistanceTo(c.Coord) == 1;
            return ab && bc && ac;
        }

        private bool CanRotate(HexBlock b1, HexBlock b2, HexBlock b3)
        {
            if (b1.Data == null || b2.Data == null || b3.Data == null)
                return false;
            // 빈 블록(GemType.None)은 회전 불가
            if (b1.Data.gemType == GemType.None || b2.Data.gemType == GemType.None || b3.Data.gemType == GemType.None)
                return false;

            // ChaosOverlord ChainAnchor 효과: 회전 불가
            if (EnemySystem.Instance != null)
            {
                if (EnemySystem.Instance.IsRotationBlocked(b1) ||
                    EnemySystem.Instance.IsRotationBlocked(b2) ||
                    EnemySystem.Instance.IsRotationBlocked(b3))
                    return false;
            }

            return b1.Data.CanMove() && b2.Data.CanMove() && b3.Data.CanMove();
        }

        private IEnumerator RotateCoroutine(HexBlock block1, HexBlock block2, HexBlock block3)
        {
            isRotating = true;
            OnRotationStarted?.Invoke();

            // TimeFreezer 비용 계산
            lastRotationCost = 1;
            if (EnemySystem.Instance != null)
                lastRotationCost = EnemySystem.Instance.GetRotationCost(block1, block2, block3);

            // 회전 시작 사운드 → GameSoundController에서 OnRotationStarted 이벤트로 처리

            HexBlock[] blocks = { block1, block2, block3 };

            // 원본 데이터 백업
            BlockData[] originalData = new BlockData[3];
            for (int i = 0; i < 3; i++)
                originalData[i] = blocks[i].Data.Clone();

            // 원래 위치 저장 (anchoredPosition 사용 - UI 블록 위치 일관성)
            Vector2[] originalPositions = new Vector2[3];
            for (int i = 0; i < 3; i++)
                originalPositions[i] = GetAnchoredPos(blocks[i]);

            // 삼각형 유효성 검증 로그
            float d01 = blocks[0].Coord.DistanceTo(blocks[1].Coord);
            float d12 = blocks[1].Coord.DistanceTo(blocks[2].Coord);
            float d02 = blocks[0].Coord.DistanceTo(blocks[2].Coord);
            Vector2 centroid = (originalPositions[0] + originalPositions[1] + originalPositions[2]) / 3f;

            Debug.Log($"[Rotation] Start ({(IsClockwise ? "CW" : "CCW")}{(oneTimeCounterClockwise ? " [1회역회전]" : "")}) " +
                $"coords=({blocks[0].Coord},{blocks[1].Coord},{blocks[2].Coord}) " +
                $"hexDist=({d01},{d12},{d02}) " +
                $"pos=({originalPositions[0]},{originalPositions[1]},{originalPositions[2]}) " +
                $"centroid={centroid} " +
                $"gems=({originalData[0].gemType},{originalData[1].gemType},{originalData[2].gemType})");

            if (d01 != 1 || d12 != 1 || d02 != 1)
            {
                Debug.LogError($"[Rotation] NON-TRIANGLE DETECTED! hexDist=({d01},{d12},{d02}) — aborting rotation");
                isRotating = false;
                OnRotationComplete?.Invoke(false);
                yield break;
            }

            // === 첫 번째 회전 (120도) ===
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            for (int i = 0; i < 3; i++)
                SetAnchoredPos(blocks[i], originalPositions[i]);

            SwapData(blocks);
            yield return null;

            List<MatchingSystem.MatchGroup> matches = matchingSystem.FindMatches();

            if (matches.Count > 0)
            {
                Debug.Log($"[Rotation] Match at 120°! ({matches.Count} groups)");
                // 매칭 사운드 → GameSoundController에서 OnMatchDetected 이벤트로 처리
                int totalBlocks = 0;
                foreach (var m in matches) totalBlocks += m.blocks.Count;
                OnMatchDetected?.Invoke(totalBlocks);
                LastMatchedWorldCenter = ComputeMatchedCenter(matches);
                oneTimeCounterClockwise = false; // 1회성 역회전 리셋
                isRotating = false;
                OnRotationComplete?.Invoke(true);
                yield break;
            }

            // === 두 번째 회전 (240도) ===
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            for (int i = 0; i < 3; i++)
                SetAnchoredPos(blocks[i], originalPositions[i]);

            SwapData(blocks);
            yield return null;

            matches = matchingSystem.FindMatches();
            if (matches.Count > 0)
            {
                Debug.Log($"[Rotation] Match at 240°! ({matches.Count} groups)");
                // 매칭 사운드 → GameSoundController에서 OnMatchDetected 이벤트로 처리
                int totalBlocks = 0;
                foreach (var m in matches) totalBlocks += m.blocks.Count;
                OnMatchDetected?.Invoke(totalBlocks);
                LastMatchedWorldCenter = ComputeMatchedCenter(matches);
                oneTimeCounterClockwise = false; // 1회성 역회전 리셋
                isRotating = false;
                OnRotationComplete?.Invoke(true);
                yield break;
            }

            // === 매칭 실패 - 원복 ===
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            for (int i = 0; i < 3; i++)
                SetAnchoredPos(blocks[i], originalPositions[i]);

            for (int i = 0; i < 3; i++)
                blocks[i].SetBlockData(originalData[i]);

            Debug.Log("[Rotation] No match, reverted");
            // 실패 사운드 → GameSoundController에서 OnRotationComplete(false) 이벤트로 처리
            oneTimeCounterClockwise = false; // 1회성 역회전 리셋
            isRotating = false;
            OnRotationComplete?.Invoke(false);
        }

        /// <summary>
        /// 매칭된 모든 블록의 월드 좌표 평균을 계산.
        /// 역회전 등 매칭 위치 기반 팝업 표시용.
        /// </summary>
        private Vector3 ComputeMatchedCenter(List<MatchingSystem.MatchGroup> matches)
        {
            if (matches == null || matches.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var group in matches)
            {
                if (group?.blocks == null) continue;
                foreach (var b in group.blocks)
                {
                    if (b == null) continue;
                    sum += b.transform.position;
                    count++;
                }
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        /// <summary>
        /// 회전 애니메이션 (120도) - anchoredPosition 사용
        /// 시계방향: -120도, 반시계방향: +120도
        /// </summary>
        private IEnumerator AnimateRotation(HexBlock[] blocks, Vector2[] originalPositions)
        {
            Vector2 center = (originalPositions[0] + originalPositions[1] + originalPositions[2]) / 3f;

            float elapsed = 0f;
            bool effectiveClockwise = clockwiseRotation && !oneTimeCounterClockwise;
            float targetAngle = effectiveClockwise ? -120f : 120f;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = rotationCurve.Evaluate(elapsed / rotationDuration);
                float angle = targetAngle * t * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                for (int i = 0; i < 3; i++)
                {
                    Vector2 offset = originalPositions[i] - center;
                    Vector2 rotated = new Vector2(
                        offset.x * cos - offset.y * sin,
                        offset.x * sin + offset.y * cos
                    );
                    SetAnchoredPos(blocks[i], center + rotated);
                }

                yield return null;
            }

            // 최종 위치 보정
            float finalAngle = targetAngle * Mathf.Deg2Rad;
            float finalCos = Mathf.Cos(finalAngle);
            float finalSin = Mathf.Sin(finalAngle);
            for (int i = 0; i < 3; i++)
            {
                Vector2 offset = originalPositions[i] - center;
                Vector2 rotated = new Vector2(
                    offset.x * finalCos - offset.y * finalSin,
                    offset.x * finalSin + offset.y * finalCos
                );
                SetAnchoredPos(blocks[i], center + rotated);
            }
        }

        /// <summary>
        /// 데이터 교환 - Clone으로 안전하게
        /// 
        /// 블록은 시계방향으로 정렬됨 (0→1→2가 시계방향)
        /// 
        /// 시계방향 회전(-120도):
        ///   비주얼: 0→1위치, 1→2위치, 2→0위치
        ///   데이터: 0에 2데이터, 1에 0데이터, 2에 1데이터
        ///
        /// 반시계방향 회전(+120도):
        ///   비주얼: 0→2위치, 1→0위치, 2→1위치
        ///   데이터: 0에 1데이터, 1에 2데이터, 2에 0데이터
        /// </summary>
        private void SwapData(HexBlock[] blocks)
        {
            BlockData d0 = blocks[0].Data.Clone();
            BlockData d1 = blocks[1].Data.Clone();
            BlockData d2 = blocks[2].Data.Clone();

            bool effectiveClockwise = clockwiseRotation && !oneTimeCounterClockwise;
            if (effectiveClockwise)
            {
                // 시계방향: 각 블록의 데이터가 다음 블록으로 이동
                blocks[0].SetBlockData(d2);
                blocks[1].SetBlockData(d0);
                blocks[2].SetBlockData(d1);
            }
            else
            {
                // 반시계방향: 각 블록의 데이터가 이전 블록으로 이동
                blocks[0].SetBlockData(d1);
                blocks[1].SetBlockData(d2);
                blocks[2].SetBlockData(d0);
            }
        }

        public (HexBlock, HexBlock, HexBlock)? GetClusterAtTouchPosition(Vector2 pos)
        {
            return hexGrid?.GetClusterAtPosition(pos);
        }
    }
}
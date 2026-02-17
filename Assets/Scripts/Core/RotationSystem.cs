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

        public event System.Action<bool> OnRotationComplete;
        public event System.Action OnRotationStarted;

        public bool IsRotating => isRotating;
        public bool IsClockwise => clockwiseRotation;
        public int LastRotationCost => lastRotationCost;

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
            if (!CanRotate(block1, block2, block3)) return;

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

            // 회전 시작 사운드
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayRotateSound();

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

            Debug.Log($"[Rotation] Start ({(clockwiseRotation ? "CW" : "CCW")}) " +
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
                if (AudioManager.Instance != null)
                {
                    int totalBlocks = 0;
                    foreach (var m in matches) totalBlocks += m.blocks.Count;
                    AudioManager.Instance.PlayMatchSound(totalBlocks);
                }
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
                if (AudioManager.Instance != null)
                {
                    int totalBlocks = 0;
                    foreach (var m in matches) totalBlocks += m.blocks.Count;
                    AudioManager.Instance.PlayMatchSound(totalBlocks);
                }
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
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayFailSound();
            isRotating = false;
            OnRotationComplete?.Invoke(false);
        }

        /// <summary>
        /// 회전 애니메이션 (120도) - anchoredPosition 사용
        /// 시계방향: -120도, 반시계방향: +120도
        /// </summary>
        private IEnumerator AnimateRotation(HexBlock[] blocks, Vector2[] originalPositions)
        {
            Vector2 center = (originalPositions[0] + originalPositions[1] + originalPositions[2]) / 3f;

            float elapsed = 0f;
            float targetAngle = clockwiseRotation ? -120f : 120f;

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

            if (clockwiseRotation)
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
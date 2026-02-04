using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Data;

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

        public event System.Action<bool> OnRotationComplete;
        public event System.Action OnRotationStarted;

        public bool IsRotating => isRotating;
        public bool IsClockwise => clockwiseRotation;

        private void Start()
        {
            // 참조가 없으면 자동으로 찾기
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

        public void TryRotate(HexBlock block1, HexBlock block2, HexBlock block3)
        {
            if (isRotating) return;
            if (block1 == null || block2 == null || block3 == null) return;
            if (!IsValidTriangle(block1, block2, block3)) return;
            if (!CanRotate(block1, block2, block3)) return;

            StartCoroutine(RotateCoroutine(block1, block2, block3));
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
            return b1.Data.CanMove() && b2.Data.CanMove() && b3.Data.CanMove();
        }

        private IEnumerator RotateCoroutine(HexBlock block1, HexBlock block2, HexBlock block3)
        {
            isRotating = true;
            OnRotationStarted?.Invoke();

            HexBlock[] blocks = { block1, block2, block3 };

            // 원본 데이터 백업
            BlockData[] originalData = new BlockData[3];
            for (int i = 0; i < 3; i++)
            {
                originalData[i] = blocks[i].Data.Clone();
            }

            // 원래 위치 저장
            Vector3[] originalPositions = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                originalPositions[i] = blocks[i].transform.localPosition;
            }

            // === 첫 번째 회전 (120도) ===
            // 1. 애니메이션
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            // 2. 위치 복원
            for (int i = 0; i < 3; i++)
            {
                blocks[i].transform.localPosition = originalPositions[i];
            }

            // 3. 데이터 교환
            SwapData(blocks);

            // 4. 대기
            yield return null;
            yield return null;

            // 5. 매칭 체크
            List<MatchingSystem.MatchGroup> matches = matchingSystem.FindMatches();
            if (matches.Count > 0)
            {
                Debug.Log("Match at 120 degrees!");
                isRotating = false;
                OnRotationComplete?.Invoke(true);
                yield break;
            }

            // === 두 번째 회전 (240도) ===
            // 1. 애니메이션
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            // 2. 위치 복원
            for (int i = 0; i < 3; i++)
            {
                blocks[i].transform.localPosition = originalPositions[i];
            }

            // 3. 데이터 교환
            SwapData(blocks);

            // 4. 대기
            yield return null;
            yield return null;

            // 5. 매칭 체크
            matches = matchingSystem.FindMatches();
            if (matches.Count > 0)
            {
                Debug.Log("Match at 240 degrees!");
                isRotating = false;
                OnRotationComplete?.Invoke(true);
                yield break;
            }

            // === 매칭 실패 - 원복 ===
            // 1. 애니메이션
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            // 2. 위치 복원
            for (int i = 0; i < 3; i++)
            {
                blocks[i].transform.localPosition = originalPositions[i];
            }

            // 3. 원본 데이터 복원
            for (int i = 0; i < 3; i++)
            {
                blocks[i].SetBlockData(originalData[i]);
            }

            Debug.Log("No match, reverted");
            isRotating = false;
            OnRotationComplete?.Invoke(false);
        }

        /// <summary>
        /// 회전 애니메이션 (120도)
        /// </summary>
        private IEnumerator AnimateRotation(HexBlock[] blocks, Vector3[] originalPositions)
        {
            Vector3 center = (originalPositions[0] + originalPositions[1] + originalPositions[2]) / 3f;

            float elapsed = 0f;
            float targetAngle = clockwiseRotation ? -120f : 120f;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = rotationCurve.Evaluate(elapsed / rotationDuration);
                float angle = targetAngle * t;

                for (int i = 0; i < 3; i++)
                {
                    Vector3 offset = originalPositions[i] - center;
                    Vector3 rotated = Quaternion.Euler(0, 0, angle) * offset;
                    blocks[i].transform.localPosition = center + rotated;
                }

                yield return null;
            }

            // 애니메이션 종료 시 정확한 120도 위치로
            float finalAngle = targetAngle;
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = originalPositions[i] - center;
                Vector3 rotated = Quaternion.Euler(0, 0, finalAngle) * offset;
                blocks[i].transform.localPosition = center + rotated;
            }
        }

        /// <summary>
        /// 데이터 교환
        /// </summary>
        private void SwapData(HexBlock[] blocks)
        {
            BlockData d0 = blocks[0].Data;
            BlockData d1 = blocks[1].Data;
            BlockData d2 = blocks[2].Data;

            if (clockwiseRotation)
            {
                blocks[0].SetBlockData(d1);
                blocks[1].SetBlockData(d2);
                blocks[2].SetBlockData(d0);
            }
            else
            {
                blocks[0].SetBlockData(d2);
                blocks[1].SetBlockData(d0);
                blocks[2].SetBlockData(d1);
            }
        }

        public (HexBlock, HexBlock, HexBlock)? GetClusterAtTouchPosition(Vector2 pos)
        {
            return hexGrid?.GetClusterAtPosition(pos);
        }
    }
}
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

            // 블록을 시계방향 순서로 정렬
            HexBlock[] sorted = SortBlocksClockwise(block1, block2, block3);

            StartCoroutine(RotateCoroutine(sorted[0], sorted[1], sorted[2]));
        }

        /// <summary>
        /// 3개 블록을 시계방향 순서로 정렬
        /// 중심점 기준으로 각도를 계산하여 시계방향(각도 감소) 순서로 배치
        /// </summary>
        private HexBlock[] SortBlocksClockwise(HexBlock a, HexBlock b, HexBlock c)
        {
            Vector3 posA = a.transform.localPosition;
            Vector3 posB = b.transform.localPosition;
            Vector3 posC = c.transform.localPosition;

            Vector3 center = (posA + posB + posC) / 3f;

            // 각 블록의 각도 계산 (Y축이 반전된 UI 좌표계)
            float angleA = Mathf.Atan2(posA.y - center.y, posA.x - center.x) * Mathf.Rad2Deg;
            float angleB = Mathf.Atan2(posB.y - center.y, posB.x - center.x) * Mathf.Rad2Deg;
            float angleC = Mathf.Atan2(posC.y - center.y, posC.x - center.x) * Mathf.Rad2Deg;

            // 시계방향 = 각도 내림차순 (Unity UI에서 Y 반전이므로 오름차순이 시계방향)
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

            Debug.Log($"[Rotation] Start ({(clockwiseRotation ? "CW" : "CCW")}) - B0:{originalData[0].gemType} B1:{originalData[1].gemType} B2:{originalData[2].gemType}");

            // === 첫 번째 회전 (120도) ===
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            for (int i = 0; i < 3; i++)
                blocks[i].transform.localPosition = originalPositions[i];

            SwapData(blocks);
            yield return null;

            Debug.Log($"[Rotation] After 120° - B0:{blocks[0].Data.gemType} B1:{blocks[1].Data.gemType} B2:{blocks[2].Data.gemType}");

            List<MatchingSystem.MatchGroup> matches = matchingSystem.FindMatches();
            if (matches.Count > 0)
            {
                Debug.Log($"[Rotation] Match at 120°! ({matches.Count} groups)");
                isRotating = false;
                OnRotationComplete?.Invoke(true);
                yield break;
            }

            // === 두 번째 회전 (240도) ===
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            for (int i = 0; i < 3; i++)
                blocks[i].transform.localPosition = originalPositions[i];

            SwapData(blocks);
            yield return null;

            Debug.Log($"[Rotation] After 240° - B0:{blocks[0].Data.gemType} B1:{blocks[1].Data.gemType} B2:{blocks[2].Data.gemType}");

            matches = matchingSystem.FindMatches();
            if (matches.Count > 0)
            {
                Debug.Log($"[Rotation] Match at 240°! ({matches.Count} groups)");
                isRotating = false;
                OnRotationComplete?.Invoke(true);
                yield break;
            }

            // === 매칭 실패 - 원복 ===
            yield return StartCoroutine(AnimateRotation(blocks, originalPositions));

            for (int i = 0; i < 3; i++)
                blocks[i].transform.localPosition = originalPositions[i];

            for (int i = 0; i < 3; i++)
                blocks[i].SetBlockData(originalData[i]);

            Debug.Log("[Rotation] No match, reverted");
            isRotating = false;
            OnRotationComplete?.Invoke(false);
        }

        /// <summary>
        /// 회전 애니메이션 (120도)
        /// 시계방향: -120도, 반시계방향: +120도
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

            float finalAngle = targetAngle;
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = originalPositions[i] - center;
                Vector3 rotated = Quaternion.Euler(0, 0, finalAngle) * offset;
                blocks[i].transform.localPosition = center + rotated;
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
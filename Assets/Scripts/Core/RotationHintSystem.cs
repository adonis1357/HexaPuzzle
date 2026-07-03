// ============================================================================
// RotationHintSystem.cs — 무입력 힌트 시스템
// ============================================================================
// 인게임에서 10초간 회전(터치)이 없으면, 현재 필드에서 정상 회전 1회로
// 가장 많은 블록을 정화할 수 있는 클러스터를 찾아 스케일 펄스로 강조한다.
//
// - 탐색: MatchingSystem.FindBestMatchableCluster (CW/CCW 전수 시뮬레이션, 1회성)
// - 연출: 클러스터 3블록이 1.0→1.09 부드러운 호흡 펄스 (유저가 알아볼 수 있는 수준)
// - 해제: 터치/회전/캐스케이드/모달/고블린 턴 등 어떤 활동이든 감지되면 즉시 해제 + 타이머 리셋
// ============================================================================

using System.Collections;
using UnityEngine;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.Core
{
    public class RotationHintSystem : MonoBehaviour
    {
        private const float IDLE_SECONDS = 10f;    // 힌트 발동까지 무입력 시간
        private const float PULSE_PERIOD = 0.9f;   // 펄스 1회 주기 (초)
        private const float PULSE_SCALE = 1.09f;   // 최대 확대 비율

        private MatchingSystem matchingSystem;
        private RotationSystem rotationSystem;
        private BlockRemovalSystem blockRemovalSystem;
        private InputSystem inputSystem;
        private HexGrid hexGrid;

        private float idleTimer = 0f;
        private HexBlock[] hintCluster;
        private Coroutine pulseCoroutine;

        private void Start()
        {
            matchingSystem = FindObjectOfType<MatchingSystem>();
            rotationSystem = FindObjectOfType<RotationSystem>();
            blockRemovalSystem = FindObjectOfType<BlockRemovalSystem>();
            inputSystem = FindObjectOfType<InputSystem>();
            hexGrid = FindObjectOfType<HexGrid>();
        }

        private void Update()
        {
            // 힌트를 보여줄 수 없는 상태(회전/캐스케이드/모달/로비 등) → 타이머 리셋 + 힌트 해제.
            //   실제 회전이 시작되면 rotationSystem.IsRotating으로 여기서 해제된다.
            if (!CanShowHint())
            {
                ResetIdle();
                return;
            }

            // 유저 터치/클릭 감지 — ★ 힌트 표시 "전"에만 타이머 리셋 (활동 중인 유저에게 안 띄움).
            //   힌트 표시 "중"에는 빈 곳 클릭/실패 터치로 사라지지 않고,
            //   블록을 실제로 회전시킬 때까지 계속 표시된다 (위 CanShowHint 게이트가 해제 담당).
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            {
                if (hintCluster == null)
                {
                    idleTimer = 0f;
                    return;
                }
            }

            idleTimer += Time.deltaTime;
            if (hintCluster == null && idleTimer >= IDLE_SECONDS)
                ShowHint();
        }

        /// <summary>힌트 표시 가능 상태인지 — 인게임 Playing + 어떤 시스템도 처리 중이 아님.</summary>
        private bool CanShowHint()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing || gm.IsPaused) return false;
            if (gm.IsPurchasePopupOpen) return false;
            if (inputSystem == null || !inputSystem.IsEnabled) return false;          // 로비/연출 중엔 입력 비활성
            if (rotationSystem != null && rotationSystem.IsRotating) return false;
            if (blockRemovalSystem != null && blockRemovalSystem.IsProcessing) return false;
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsProcessingTurn) return false;
            if (MPManager.Instance != null && MPManager.Instance.IsManaPurchasePopupOpen) return false;
            if (SkillUpgradeOfferSystem.Instance != null && SkillUpgradeOfferSystem.Instance.IsChoiceModalOpen) return false;
            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive) return false; // 튜토리얼은 자체 안내
            if (hexGrid == null || !hexGrid.gameObject.activeInHierarchy) return false;
            return true;
        }

        private void ResetIdle()
        {
            idleTimer = 0f;
            HideHint();
        }

        private void ShowHint()
        {
            if (matchingSystem == null) return;

            int clearCount;
            var cluster = matchingSystem.FindBestMatchableCluster(out clearCount);
            if (cluster == null || clearCount <= 0)
            {
                // 회전 매칭 불가(교착) — 교착 시스템이 별도 처리. 잠시 후 재시도.
                idleTimer = 0f;
                return;
            }

            hintCluster = cluster;
            Debug.Log($"[RotationHintSystem] 힌트 표시 — 최대 {clearCount}블록 정화 클러스터 " +
                      $"({cluster[0].Coord}, {cluster[1].Coord}, {cluster[2].Coord})");
            pulseCoroutine = StartCoroutine(PulseCluster());
        }

        private void HideHint()
        {
            if (hintCluster == null) return;

            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
            // 스케일 원복
            foreach (var b in hintCluster)
            {
                if (b != null)
                    b.transform.localScale = Vector3.one;
            }
            hintCluster = null;
        }

        /// <summary>
        /// 클러스터 3블록 호흡 펄스 — 1.0→1.09→1.0 사인 곡선 반복.
        /// 블록이 파괴/변경되면 자동 중단 (다음 무입력 주기에 재탐색).
        /// </summary>
        private IEnumerator PulseCluster()
        {
            float t = 0f;
            while (true)
            {
                // 유효성: 블록이 사라졌거나 데이터가 비면 중단
                bool valid = true;
                foreach (var b in hintCluster)
                {
                    if (b == null || b.Data == null || b.Data.gemType == JewelsHexaPuzzle.Data.GemType.None)
                    { valid = false; break; }
                }
                if (!valid)
                {
                    HideHint();
                    idleTimer = 0f;
                    yield break;
                }

                t += Time.deltaTime;
                // 부드러운 호흡: sin 0~1 매핑
                float k = (Mathf.Sin(t / PULSE_PERIOD * Mathf.PI * 2f - Mathf.PI / 2f) + 1f) * 0.5f;
                float scale = Mathf.Lerp(1f, PULSE_SCALE, k);
                foreach (var b in hintCluster)
                {
                    if (b != null)
                        b.transform.localScale = Vector3.one * scale;
                }
                yield return null;
            }
        }
    }
}

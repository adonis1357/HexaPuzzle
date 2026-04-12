// ============================================================================
// MonsterSpawnController.cs - 몬스터 소환 컨트롤러
// ============================================================================
// 활성 미션 기반 즉시 소환 시스템:
//   규칙1: 게임 시작 시 활성 미션의 모든 몬스터를 즉시 소환 (100%)
//   규칙2: 남은 이동 횟수가 전체의 40% 이하가 되면 잔여 전부 소환
//   규칙3: 필드 몬스터 3마리 이하이고 잔여 있으면 추가 소환
//   대기 미션 활성화 시 TriggerImmediateSpawn으로 즉시 소환
//
// GoblinSystem.SpawnWaveBatch에 위임하여 실제 소환 실행.
// ============================================================================

using UnityEngine;
using System.Collections;
using JewelsHexaPuzzle.Core;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 몬스터 소환 컨트롤러 — 3단계 소환 규칙 자동 적용
    /// </summary>
    public class MonsterSpawnController : MonoBehaviour
    {
        // ============================================================
        // 싱글톤
        // ============================================================
        public static MonsterSpawnController Instance { get; private set; }

        // ============================================================
        // 핵심 필드
        // ============================================================
        private int totalMonsterCount;   // 전체 미션 몬스터 수 (소환 대상 총합)
        private int spawnedCount = 0;    // 지금까지 소환 요청한 수
        private int totalMoves;          // 전체 이동 횟수
        private bool allSpawned = false; // 전부 소환 완료 플래그
        private bool initialized = false;

        /// <summary>남은 소환 가능 수</summary>
        public int RemainingCount => Mathf.Max(0, totalMonsterCount - spawnedCount);

        // ============================================================
        // 생명주기
        // ============================================================
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ============================================================
        // 초기화 (스테이지 시작 시 GameManager에서 호출)
        // ============================================================

        /// <summary>
        /// 스테이지 시작 시 호출. 전체 미션 몬스터 수와 이동 횟수를 받아
        /// 1차 소환(40~50%)을 실행한다.
        /// </summary>
        /// <param name="totalMissionMonsters">전체 미션 몬스터 수 (GetTotalMissionTarget 결과)</param>
        /// <param name="moves">전체 이동 횟수 (initialTurns)</param>
        public IEnumerator Initialize(int totalMissionMonsters, int moves)
        {
            totalMonsterCount = totalMissionMonsters;
            totalMoves = moves;
            spawnedCount = 0;
            allSpawned = false;
            initialized = true;

            // 규칙1: 활성 미션 몬스터 전체 즉시 소환 (100%)
            int firstWaveCount = totalMonsterCount;

            // maxOnBoard를 활성 미션 총수에 맞춰 동적 확장
            if (GoblinSystem.Instance != null)
            {
                var config = GoblinSystem.Instance.CurrentConfig;
                if (config != null && config.maxOnBoard < totalMonsterCount)
                {
                    Debug.Log($"[MonsterSpawnController] maxOnBoard 동적 확장: {config.maxOnBoard} → {totalMonsterCount}");
                    config.maxOnBoard = totalMonsterCount;
                }
            }

            Debug.Log($"[MonsterSpawnController] 초기화: 전체={totalMonsterCount}, 이동={totalMoves}, 즉시소환={firstWaveCount} (활성 미션 전체)");
            yield return SpawnBatch(firstWaveCount);
        }

        // ============================================================
        // 소환 배치 실행
        // ============================================================

        /// <summary>
        /// spawnQueue에서 count만큼 소환. GoblinSystem.SpawnWaveBatch 활용.
        /// </summary>
        private IEnumerator SpawnBatch(int count)
        {
            if (GoblinSystem.Instance == null || count <= 0) yield break;

            // 남은 수 이상으로 소환하지 않음
            int actual = Mathf.Min(count, RemainingCount);
            if (actual <= 0)
            {
                allSpawned = true;
                yield break;
            }

            spawnedCount += actual;
            Debug.Log($"[MonsterSpawnController] SpawnBatch: {actual}마리 요청 (누적={spawnedCount}/{totalMonsterCount})");

            yield return GoblinSystem.Instance.StartCoroutine(
                GoblinSystem.Instance.SpawnWaveBatchPublic(actual)
            );

            // 전부 소환 완료 체크
            if (RemainingCount <= 0)
            {
                allSpawned = true;
                Debug.Log("[MonsterSpawnController] 전체 소환 완료!");
            }
        }

        // ============================================================
        // 턴 종료 시 호출 (GameManager.UseTurn에서)
        // ============================================================

        /// <summary>
        /// 매 턴 종료 시 호출. 규칙2, 규칙3을 순차 체크.
        /// </summary>
        /// <param name="remainingMoves">남은 이동 횟수</param>
        public void OnTurnEnd(int remainingMoves)
        {
            if (!initialized || allSpawned) return;
            if (GoblinSystem.Instance == null) return;
            if (GoblinSystem.Instance.MissionComplete) return;

            // 규칙2: 남은 이동이 전체의 40% 이하면 잔여 전부 소환
            if (remainingMoves <= totalMoves * 0.4f && RemainingCount > 0)
            {
                int remaining = RemainingCount;
                Debug.Log($"[MonsterSpawnController] 규칙2 트리거: 남은이동={remainingMoves}/{totalMoves} (40%={totalMoves * 0.4f}), 잔여 {remaining}마리 전부 소환");
                StartCoroutine(SpawnBatch(remaining));
                return; // 규칙2 발동 시 규칙3은 스킵
            }

            // 규칙3: 필드 3마리 미만이면 소환 수량 증가
            // ★ 필드 몬스터가 적을수록 더 많이 소환 (원래 소환 수량 초과 가능, 미션 상한 준수)
            int aliveCount = GoblinSystem.Instance.GetAliveGoblinCount();
            if (aliveCount < 3 && RemainingCount > 0)
            {
                // 부족한 수(3 - alive)에 비례하여 증가: 0마리→5~7, 1마리→3~5, 2마리→2~3
                int deficit = 3 - aliveCount;
                int baseExtra = deficit * 2;
                int extra = baseExtra + Random.Range(0, deficit + 1);
                extra = Mathf.Min(extra, RemainingCount); // ★ 절대 미션 상한 초과 금지
                Debug.Log($"[MonsterSpawnController] 규칙3 트리거: 필드={aliveCount}마리(부족{deficit}), 추가 {extra}마리 소환 (잔여={RemainingCount})");
                StartCoroutine(SpawnBatch(extra));
            }
        }

        /// <summary>
        /// 리셋 (로비 복귀 등)
        /// </summary>
        public void Reset()
        {
            totalMonsterCount = 0;
            spawnedCount = 0;
            totalMoves = 0;
            allSpawned = false;
            initialized = false;
        }
    }
}

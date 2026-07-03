using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// Stage 1 전용 어시스트 시스템.
    ///  1. 매칭 불가능 상태 감지 → 토스트 + 재배치
    ///  2. 10초 이상 미조작 시 최적 매칭 3블록 외곽선 점멸 힌트
    ///
    /// Stage 1 진입 시 EnableForStage1() 호출, 이탈 시 Disable() 호출.
    /// </summary>
    public class Stage1AssistSystem : MonoBehaviour
    {
        private HexGrid hexGrid;
        private MatchingSystem matchingSystem;
        private BlockRemovalSystem removalSystem;
        private RotationSystem rotationSystem;

        private bool isEnabled = false;
        private float lastInteractionTime;
        private const float IDLE_THRESHOLD = 10f;

        private HexBlock[] currentHintCluster = null;
        private Coroutine hintPulseCoroutine = null;

        private bool reshuffleCheckQueued = false;

        public static Stage1AssistSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            AutoWire();
        }

        private void AutoWire()
        {
            if (hexGrid == null) hexGrid = FindObjectOfType<HexGrid>();
            if (matchingSystem == null) matchingSystem = FindObjectOfType<MatchingSystem>();
            if (removalSystem == null) removalSystem = FindObjectOfType<BlockRemovalSystem>();
            if (rotationSystem == null) rotationSystem = FindObjectOfType<RotationSystem>();
        }

        /// <summary>Stage 1 진입 시 호출 — 어시스트 시스템 활성화</summary>
        public void EnableForStage1()
        {
            AutoWire();
            if (removalSystem == null || matchingSystem == null)
            {
                Debug.LogWarning("[Stage1Assist] 필수 시스템 참조 부족 — 활성화 실패");
                return;
            }

            removalSystem.OnCascadeComplete -= OnCascadeComplete;
            removalSystem.OnCascadeComplete += OnCascadeComplete;

            if (rotationSystem != null)
            {
                rotationSystem.OnRotationComplete -= OnPlayerRotated;
                rotationSystem.OnRotationComplete += OnPlayerRotated;
            }

            isEnabled = true;
            lastInteractionTime = Time.unscaledTime;
            ClearHint();
            Debug.Log("[Stage1Assist] 활성화");
        }

        /// <summary>Stage 이탈 시 호출 — 이벤트 해제</summary>
        public void Disable()
        {
            if (removalSystem != null)
                removalSystem.OnCascadeComplete -= OnCascadeComplete;
            if (rotationSystem != null)
                rotationSystem.OnRotationComplete -= OnPlayerRotated;
            isEnabled = false;
            ClearHint();
            Debug.Log("[Stage1Assist] 비활성화");
        }

        private void OnPlayerRotated(bool matched)
        {
            // 플레이어 상호작용 감지 → idle 타이머 리셋
            lastInteractionTime = Time.unscaledTime;
            ClearHint();
        }

        private void OnCascadeComplete()
        {
            if (!isEnabled) return;
            // 프레임 종료 후 매칭 가능성 체크 (블록 데이터 안정화 대기)
            if (reshuffleCheckQueued) return;
            reshuffleCheckQueued = true;
            StartCoroutine(CheckMatchablePossibility());
        }

        private IEnumerator CheckMatchablePossibility()
        {
            yield return new WaitForSecondsRealtime(0.3f);
            reshuffleCheckQueued = false;

            if (!isEnabled || matchingSystem == null || hexGrid == null) yield break;

            var cluster = matchingSystem.FindMatchableCluster();
            if (cluster != null)
            {
                lastInteractionTime = Time.unscaledTime;
                yield break;
            }

            // 매칭 불가능 상태 — 5초 대기 → 토스트 → 1초 대기 → 재배치
            Debug.Log("[Stage1Assist] 매칭 불가능 상태 감지 — 5초 대기 후 재배치 시작");
            yield return new WaitForSecondsRealtime(5f);

            // 5초 동안 상태 변화로 매칭 가능해졌으면 취소
            if (!isEnabled) yield break;
            if (matchingSystem.FindMatchableCluster() != null)
            {
                Debug.Log("[Stage1Assist] 5초 대기 중 매칭 가능 상태로 전환 — 재배치 취소");
                yield break;
            }

            // 토스트 발행
            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast("<color=#FFD060><b>회전으로 매칭이 불가능해 블록을 섞습니다!</b></color>");

            yield return new WaitForSecondsRealtime(1f);

            if (!isEnabled) yield break;

            // 시각 재배치 — 매칭 가능 상태가 될 때까지 반복
            yield return StartCoroutine(VisualReshuffleLoop());

            lastInteractionTime = Time.unscaledTime;
        }

        /// <summary>
        /// 시각 재배치 루프 — 매칭 가능 상태가 될 때까지 시각적 회전 재배치 반복.
        /// 안전장치: 최대 8회 시도.
        /// </summary>
        private IEnumerator VisualReshuffleLoop()
        {
            for (int loop = 1; loop <= 8; loop++)
            {
                yield return StartCoroutine(VisualReshuffleOnce());
                yield return new WaitForSecondsRealtime(0.15f);

                // 매칭 가능 + 초기 매칭 없는 상태 확인
                var matchableCluster = matchingSystem.FindMatchableCluster();
                var matches = matchingSystem.FindMatches();
                bool hasInitialMatch = matches != null && matches.Count > 0;

                if (matchableCluster != null && !hasInitialMatch)
                {
                    Debug.Log($"[Stage1Assist] 시각 재배치 성공 (반복 {loop}회)");
                    yield break;
                }
            }
            Debug.LogWarning("[Stage1Assist] 시각 재배치 8회 시도 후 매칭 가능 상태 미수렴 — 강제 재할당으로 폴백");
            // 폴백: 강제 데이터 재할당 (이전 방식)
            FallbackReshuffleBoard();
        }

        /// <summary>
        /// 시각 재배치 1회 — 랜덤한 5개 클러스터를 순차적으로 빠르게 회전 시켜 색상 섞기.
        /// 각 클러스터: 120° 또는 240° 랜덤 회전 (CW 1회 또는 2회 데이터 swap).
        /// </summary>
        private IEnumerator VisualReshuffleOnce()
        {
            var allClusters = matchingSystem.GetAllRotatableClusters();
            if (allClusters == null || allClusters.Count == 0) yield break;

            // 5개 랜덤 선택 (중복 없이)
            var shuffled = new List<HexBlock[]>(allClusters);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, shuffled.Count);
                var tmp = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = tmp;
            }
            int pickCount = Mathf.Min(5, shuffled.Count);

            // 순차 빠른 회전 (각 0.18초)
            for (int i = 0; i < pickCount; i++)
            {
                bool ccw = UnityEngine.Random.value < 0.5f; // 120(CW) 또는 240(CW=CCW 1회)
                yield return StartCoroutine(QuickRotateCluster(shuffled[i], ccw));
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        /// <summary>
        /// 단일 클러스터 빠른 회전 애니메이션 — 스케일 펄스 + 데이터 swap.
        /// cw=true: 120° CW (1회 swap), cw=false: 240° (= CCW 1회 swap, CW 2회와 동일).
        /// </summary>
        private IEnumerator QuickRotateCluster(HexBlock[] cluster, bool ccw)
        {
            if (cluster == null || cluster.Length != 3) yield break;
            if (cluster[0] == null || cluster[1] == null || cluster[2] == null) yield break;

            // 스케일 펄스 (0.18초)
            float dur = 0.18f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = t / dur;
                float scale = 1f + 0.18f * Mathf.Sin(p * Mathf.PI);
                for (int i = 0; i < 3; i++)
                {
                    if (cluster[i] != null)
                        cluster[i].transform.localScale = Vector3.one * scale;
                }
                yield return null;
            }

            // 데이터 swap (CW 회전: pos0←old2, pos1←old0, pos2←old1)
            var d0 = cluster[0].Data?.Clone();
            var d1 = cluster[1].Data?.Clone();
            var d2 = cluster[2].Data?.Clone();

            if (d0 == null || d1 == null || d2 == null) yield break;

            if (!ccw)
            {
                // 120° CW: 한 번 swap
                cluster[0].SetBlockData(d2);
                cluster[1].SetBlockData(d0);
                cluster[2].SetBlockData(d1);
            }
            else
            {
                // 240° (= CCW 또는 CW 2회)
                cluster[0].SetBlockData(d1);
                cluster[1].SetBlockData(d2);
                cluster[2].SetBlockData(d0);
            }

            // 스케일 복원
            for (int i = 0; i < 3; i++)
            {
                if (cluster[i] != null)
                    cluster[i].transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 폴백 — 시각 재배치 8회 후에도 매칭 가능 상태가 안 되면 모든 블록을 강제 랜덤 재할당.
        /// </summary>
        private void FallbackReshuffleBoard()
        {
            if (hexGrid == null || matchingSystem == null) return;

            var blocks = new List<HexBlock>();
            foreach (var b in hexGrid.GetAllBlocks())
            {
                if (b == null) continue;
                if (b.Data == null) continue;
                if (b.Data.gemType == GemType.None) continue;
                blocks.Add(b);
            }

            for (int attempt = 0; attempt < 30; attempt++)
            {
                foreach (var b in blocks)
                {
                    var data = new BlockData();
                    data.gemType = GemTypeHelper.GetRandom();
                    data.isCracked = false;
                    data.isShell = false;
                    data.specialType = SpecialBlockType.None;
                    data.tier = BlockTier.Normal;
                    b.SetBlockData(data);
                }

                var matches = matchingSystem.FindMatches();
                bool hasInitialMatch = matches != null && matches.Count > 0;
                var matchableCluster = matchingSystem.FindMatchableCluster();

                if (!hasInitialMatch && matchableCluster != null)
                {
                    Debug.Log($"[Stage1Assist] 폴백 재할당 성공 (시도 {attempt + 1}회)");
                    return;
                }
            }
            Debug.LogWarning("[Stage1Assist] 폴백 재할당 30회 후 미수렴 — 현재 상태 유지");
        }

        private void Update()
        {
            if (!isEnabled) return;

            float idleTime = Time.unscaledTime - lastInteractionTime;

            // 10초 이상 미조작 + 힌트 미표시 → 힌트 활성화
            if (idleTime >= IDLE_THRESHOLD && currentHintCluster == null)
            {
                ShowHint();
            }
        }

        private void ShowHint()
        {
            if (matchingSystem == null) return;
            var cluster = matchingSystem.FindMatchableCluster();
            if (cluster == null || cluster.Length != 3) return;

            currentHintCluster = cluster;
            if (hintPulseCoroutine != null) StopCoroutine(hintPulseCoroutine);
            hintPulseCoroutine = StartCoroutine(PulseHintCluster(cluster));
            Debug.Log($"[Stage1Assist] 힌트 표시: ({cluster[0].Coord}, {cluster[1].Coord}, {cluster[2].Coord})");
        }

        private void ClearHint()
        {
            if (hintPulseCoroutine != null)
            {
                StopCoroutine(hintPulseCoroutine);
                hintPulseCoroutine = null;
            }
            if (currentHintCluster != null)
            {
                foreach (var b in currentHintCluster)
                {
                    if (b != null)
                    {
                        b.SetStage1Hint(false);
                        b.ClearStage1HintBounce(); // 스케일/면 플래시 복원
                    }
                }
                currentHintCluster = null;
            }
        }

        /// <summary>
        /// 힌트 클러스터 3블록을 외곽선 점멸 + 5초 주기 바운스·플래시로 강조.
        ///  - 외곽선: 1.2초 sin 주기로 알파 0.35 ↔ 1.0 은은하게 점멸
        ///  - 5초마다: 0.4초 동안 스케일 1.0 → 1.15 → 1.0 + 면 흰색 플래시 0 → 1 → 0
        /// </summary>
        private IEnumerator PulseHintCluster(HexBlock[] cluster)
        {
            float bounceTimer = 0f;
            const float BOUNCE_INTERVAL = 5f;
            const float BOUNCE_DURATION = 0.4f;

            while (isEnabled && currentHintCluster != null)
            {
                bounceTimer += Time.unscaledDeltaTime;

                // ── 상시: 외곽선 은은한 점멸 ──
                float pulseT = (Time.unscaledTime * 1.5f) % (2f * Mathf.PI);
                float borderAlpha = Mathf.Lerp(0.35f, 1.0f, (Mathf.Sin(pulseT) + 1f) * 0.5f);
                foreach (var b in cluster)
                {
                    if (b == null) continue;
                    b.SetStage1Hint(true, borderAlpha);
                }

                // ── 5초 주기: 바운스 + 면 플래시 ──
                if (bounceTimer >= BOUNCE_INTERVAL)
                {
                    bounceTimer = 0f;
                    yield return StartCoroutine(PlayBounceAndFlash(cluster, BOUNCE_DURATION));
                }

                yield return null;
            }
        }

        /// <summary>
        /// 한 회 바운스+플래시 실행 (블록이 살짝 커졌다가 복귀 + 면이 밝게 번쩍).
        ///  0~50%: 스케일 1.0 → 1.15, 플래시 0 → 1 (EaseOutCubic)
        ///  50~100%: 스케일 1.15 → 1.0, 플래시 1 → 0 (EaseInQuad)
        /// </summary>
        private IEnumerator PlayBounceAndFlash(HexBlock[] cluster, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && isEnabled)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float scalePulse, flashIntensity;
                if (t < 0.5f)
                {
                    float u = t / 0.5f;
                    float eased = 1f - Mathf.Pow(1f - u, 3f); // EaseOutCubic
                    scalePulse = 0.15f * eased;               // 0 → 0.15 (스케일 1.0 → 1.15)
                    flashIntensity = eased;                    // 0 → 1
                }
                else
                {
                    float u = (t - 0.5f) / 0.5f;
                    float eased = u * u; // EaseInQuad
                    scalePulse = 0.15f * (1f - eased);         // 0.15 → 0
                    flashIntensity = 1f - eased;               // 1 → 0
                }

                foreach (var b in cluster)
                {
                    if (b == null) continue;
                    b.ApplyStage1HintBounce(scalePulse, flashIntensity);
                }

                yield return null;
            }

            // 종료 시 원상복구
            foreach (var b in cluster)
            {
                if (b == null) continue;
                b.ClearStage1HintBounce();
            }
        }
    }
}

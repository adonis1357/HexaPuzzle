// =====================================================================================
// TutorialManager.cs — 튜토리얼 시스템 매니저
// =====================================================================================
// 싱글톤 패턴. 튜토리얼 시퀀스 진행, 트리거 감지, 완료 상태 저장(PlayerPrefs).
// GameManager, InputSystem, BlockRemovalSystem 등과 연동하여 튜토리얼을 제어.
// =====================================================================================
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Data;
using JewelsHexaPuzzle.UI;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 튜토리얼 시스템 중앙 매니저 (싱글톤)
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        // ============================================================
        // 싱글톤
        // ============================================================
        public static TutorialManager Instance { get; private set; }

        // ============================================================
        // 상태
        // ============================================================
        private bool isTutorialActive = false;
        public bool IsTutorialActive => isTutorialActive;

        private TutorialSequence currentSequence;
        private int currentStepIndex;
        private bool waitingForTap = false;
        private bool waitingForEvent = false;
        private TutorialWaitEvent pendingWaitEvent = TutorialWaitEvent.None;

        // 드릴 튜토리얼 전용 상태
        private bool isPausedForTutorial = false;
        private HexCoord? pendingDrillCoord = null;

        /// <summary>
        /// BRS 캐스케이드 pause 동기화용 프로퍼티.
        /// true이면 BRS가 yield return null로 대기.
        /// </summary>
        public bool IsPausedForTutorial => isPausedForTutorial;

        // ============================================================
        // 데이터
        // ============================================================
        private List<TutorialSequence> allSequences;
        private HashSet<string> completedTutorials = new HashSet<string>();
        private const string PREFS_KEY = "CompletedTutorials";

        // ============================================================
        // 참조
        // ============================================================
        private TutorialUI tutorialUI;
        private InputSystem inputSystem;
        private HexGrid hexGrid;
        private MatchingSystem matchingSystem;
        private Coroutine sequenceCoroutine;

        // ============================================================
        // 이벤트
        // ============================================================
        public event Action OnTutorialStarted;
        public event Action OnTutorialEnded;

        // ============================================================
        // 초기화
        // ============================================================

        private bool isInitialized = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadCompletedTutorials();
            allSequences = TutorialDatabase.GetAllSequences();
            Debug.Log($"[TutorialManager] Awake: {allSequences.Count}개 시퀀스 로드, {completedTutorials.Count}개 완료됨");
        }

        private void Start()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 참조/UI 초기화 보장 (지연 초기화 패턴)
        /// Awake에서 못하는 초기화(FindObjectOfType 등)를 여기서 수행
        /// </summary>
        private void EnsureInitialized()
        {
            if (isInitialized) return;

            // 참조 자동 탐색
            inputSystem = FindObjectOfType<InputSystem>();
            hexGrid = FindObjectOfType<HexGrid>();
            matchingSystem = FindObjectOfType<MatchingSystem>();

            // TutorialUI 생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                if (tutorialUI == null)
                {
                    GameObject uiObj = new GameObject("TutorialUI");
                    uiObj.transform.SetParent(this.transform);
                    tutorialUI = uiObj.AddComponent<TutorialUI>();
                    tutorialUI.Initialize(canvas);
                }
                isInitialized = true;
                Debug.Log($"[TutorialManager] 초기화 완료: inputSystem={inputSystem != null}, hexGrid={hexGrid != null}, tutorialUI={tutorialUI != null}");
            }
            else
            {
                Debug.LogWarning("[TutorialManager] Canvas를 찾을 수 없습니다! 초기화 보류.");
            }
        }

        // ============================================================
        // PlayerPrefs 저장/로드
        // ============================================================

        private void LoadCompletedTutorials()
        {
            string data = PlayerPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(data))
            {
                foreach (string id in data.Split(','))
                {
                    if (!string.IsNullOrEmpty(id))
                        completedTutorials.Add(id);
                }
            }
        }

        private void SaveCompletedTutorials()
        {
            string data = string.Join(",", completedTutorials);
            PlayerPrefs.SetString(PREFS_KEY, data);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 튜토리얼 완료 여부 확인
        /// </summary>
        public bool IsTutorialCompleted(string sequenceId)
        {
            return completedTutorials.Contains(sequenceId);
        }

        /// <summary>
        /// 튜토리얼 완료 마킹
        /// </summary>
        public void MarkCompleted(string sequenceId)
        {
            if (completedTutorials.Add(sequenceId))
            {
                SaveCompletedTutorials();
                Debug.Log($"[TutorialManager] 튜토리얼 완료: {sequenceId}");
            }
        }

        /// <summary>
        /// 모든 튜토리얼 완료 상태 리셋 (디버그용)
        /// </summary>
        public void ResetAllTutorials()
        {
            completedTutorials.Clear();
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
            Debug.Log("[TutorialManager] 모든 튜토리얼 리셋");
        }

        // ============================================================
        // 트리거 체크 (외부에서 호출)
        // ============================================================

        /// <summary>
        /// 스테이지 시작 시 호출 — 해당 스테이지의 온보딩 튜토리얼 체크
        /// </summary>
        public void OnStageStart(int stageNumber)
        {
            EnsureInitialized();
            Debug.Log($"[TutorialManager] OnStageStart({stageNumber}): active={isTutorialActive}, tutorialUI={tutorialUI != null}, sequences={allSequences?.Count}");

            // 드릴 튜토리얼 보드 사전 배치 (완료되지 않은 경우만)
            if (stageNumber == 11 && !IsTutorialCompleted("stage11_drill_tutorial"))
            {
                SetupDrillTutorialBoard();
            }

            if (isTutorialActive) return;
            CheckTrigger(TutorialTrigger.OnStageStart, stageNumber);
        }

        /// <summary>
        /// 드릴 튜토리얼 보드 사전 배치 — CW 회전으로 BackSlash 4-in-line 생성 가능하도록 설정
        /// </summary>
        private void SetupDrillTutorialBoard()
        {
            if (hexGrid == null) return;

            var presets = new Dictionary<HexCoord, GemType>
            {
                // 4-in-line 핵심 블록 (BackSlash q축)
                { new HexCoord(-1, 0), GemType.Blue },
                { new HexCoord( 0, 0), GemType.Blue },    // 클러스터 (CW → (1,0) 위치로)
                { new HexCoord( 0, 1), GemType.Blue },    // 클러스터 (CW → (0,0) 위치로)
                { new HexCoord( 1, 0), GemType.Red },     // 클러스터 (CW → (0,1) 위치로)
                { new HexCoord( 2, 0), GemType.Blue },

                // 삼각형 매칭 방지용 안전 블록
                { new HexCoord(-1, 1), GemType.Red },
                { new HexCoord( 0,-1), GemType.Red },
                { new HexCoord( 1,-1), GemType.Green },
                { new HexCoord(-2, 0), GemType.Green }
            };

            hexGrid.SetPresetBlocks(presets);
            Debug.Log("[TutorialManager] 드릴 튜토리얼 보드 사전 배치 완료");
        }

        /// <summary>
        /// 매칭 발생 시 호출
        /// </summary>
        public void OnMatchOccurred(int matchCount)
        {
            // 이벤트 대기 중이면 해제
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.MatchOccurred)
            {
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                return;
            }
        }

        /// <summary>
        /// 회전 완료 시 호출
        /// </summary>
        public void OnRotationComplete(bool success)
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.RotationComplete)
            {
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
            }
        }

        /// <summary>
        /// 특수 블록 생성 시 호출
        /// </summary>
        public void OnSpecialBlockCreated(SpecialBlockType type)
        {
            EnsureInitialized();
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.SpecialBlockCreated)
            {
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
            }

            // 첫 생성 힌트 체크 (진행 중인 튜토리얼이 없을 때만)
            if (!isTutorialActive)
            {
                var hint = allSequences.FirstOrDefault(s =>
                    s.trigger == TutorialTrigger.OnFirstSpecialCreate &&
                    s.triggerSpecialType == type &&
                    s.showOnce && !IsTutorialCompleted(s.sequenceId));

                if (hint != null)
                    StartSequence(hint);
            }
        }

        /// <summary>
        /// 적군 등장 시 호출
        /// </summary>
        public void OnEnemyEncountered(EnemyType type)
        {
            EnsureInitialized();
            if (isTutorialActive) return;

            var hint = allSequences.FirstOrDefault(s =>
                s.trigger == TutorialTrigger.OnFirstEnemyEncounter &&
                s.triggerEnemyType == type &&
                s.showOnce && !IsTutorialCompleted(s.sequenceId));

            if (hint != null)
                StartSequence(hint);
        }

        /// <summary>
        /// 캐스케이드 완료 시 호출
        /// </summary>
        public void OnCascadeComplete()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.CascadeComplete)
            {
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
            }
        }

        // ============================================================
        // 트리거 매칭
        // ============================================================

        private void CheckTrigger(TutorialTrigger trigger, int stageNumber = -1)
        {
            Debug.Log($"[TutorialManager] CheckTrigger: trigger={trigger}, stage={stageNumber}, 총 시퀀스={allSequences?.Count}");
            bool foundAny = false;
            foreach (var seq in allSequences)
            {
                if (seq.trigger != trigger) continue;
                foundAny = true;
                if (seq.triggerStage >= 0 && seq.triggerStage != stageNumber)
                {
                    Debug.Log($"[TutorialManager]   → '{seq.sequenceId}' 스테이지 불일치 (요구={seq.triggerStage}, 현재={stageNumber})");
                    continue;
                }
                if (seq.showOnce && IsTutorialCompleted(seq.sequenceId))
                {
                    Debug.Log($"[TutorialManager]   → '{seq.sequenceId}' 이미 완료됨");
                    continue;
                }

                Debug.Log($"[TutorialManager]   → '{seq.sequenceId}' 매칭! 시퀀스 시작");
                StartSequence(seq);
                break; // 한 번에 하나의 시퀀스만
            }
            if (!foundAny)
                Debug.Log($"[TutorialManager]   → trigger={trigger}에 해당하는 시퀀스 없음");
        }

        // ============================================================
        // 시퀀스 재생
        // ============================================================

        private void StartSequence(TutorialSequence sequence)
        {
            if (isTutorialActive) return;
            if (sequence == null || sequence.steps == null || sequence.steps.Length == 0)
            {
                Debug.LogWarning($"[TutorialManager] StartSequence 실패: sequence={sequence != null}, steps={sequence?.steps?.Length}");
                return;
            }

            // UI 준비 확인
            EnsureInitialized();
            if (tutorialUI == null)
            {
                Debug.LogError("[TutorialManager] StartSequence 실패: tutorialUI가 null! Canvas가 없거나 초기화 실패.");
                return;
            }

            currentSequence = sequence;
            currentStepIndex = 0;
            isTutorialActive = true;

            OnTutorialStarted?.Invoke();
            Debug.Log($"[TutorialManager] ★ 시퀀스 시작: {sequence.sequenceId} ({sequence.steps.Length}스텝)");

            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(PlaySequenceCoroutine());
        }

        private IEnumerator PlaySequenceCoroutine()
        {
            while (currentStepIndex < currentSequence.steps.Length)
            {
                TutorialStep step = currentSequence.steps[currentStepIndex];
                yield return StartCoroutine(PlayStepCoroutine(step));
                currentStepIndex++;
            }

            // 시퀀스 완료
            CompleteSequence();
        }

        private IEnumerator PlayStepCoroutine(TutorialStep step)
        {
            Debug.Log($"[TutorialManager] 스텝 실행: {step.id} ({step.type})");

            switch (step.type)
            {
                case TutorialStepType.Dialog:
                    yield return StartCoroutine(HandleDialogStep(step));
                    break;

                case TutorialStepType.Highlight:
                    yield return StartCoroutine(HandleHighlightStep(step));
                    break;

                case TutorialStepType.ForcedAction:
                    yield return StartCoroutine(HandleForcedActionStep(step));
                    break;

                case TutorialStepType.FreePlayHint:
                    yield return StartCoroutine(HandleFreePlayHintStep(step));
                    break;

                case TutorialStepType.WaitForEvent:
                    yield return StartCoroutine(HandleWaitForEventStep(step));
                    break;
            }
        }

        // ── Dialog 스텝 ──
        private IEnumerator HandleDialogStep(TutorialStep step)
        {
            if (step.pauseGame)
                LockInput();

            // 하이라이트 (선택적)
            if (step.useHighlight && tutorialUI != null)
                tutorialUI.ShowHighlight(step.highlightScreenPos, step.highlightRadius);

            // 블록 좌표 하이라이트 (드릴 튜토리얼용)
            if (step.highlightBlockCoords != null && step.highlightBlockCoords.Length > 0 && hexGrid != null)
            {
                HashSet<HexCoord> highlightSet = new HashSet<HexCoord>();
                foreach (var c in step.highlightBlockCoords) highlightSet.Add(c);

                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null) continue;
                    if (highlightSet.Contains(block.Coord))
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // 드릴 하이라이트 (동적 좌표 — step 6: 드릴 완성 설명)
            if (step.id == "s11_drill_explain" && pendingDrillCoord.HasValue && hexGrid != null)
            {
                foreach (var block in hexGrid.GetAllBlocks())
                {
                    if (block == null) continue;
                    if (block.Coord.Equals(pendingDrillCoord.Value))
                        block.SetTutorialGlow(true);
                    else
                        block.SetTutorialDimmed(true);
                }
            }

            // 대화 표시
            waitingForTap = true;
            if (tutorialUI != null)
            {
                bool showTap = step.autoAdvanceDelay <= 0f;
                tutorialUI.ShowDialog(step.characterName, step.title, step.message, showTap, () =>
                {
                    waitingForTap = false;
                });
            }

            // 자동 진행 또는 탭 대기
            if (step.autoAdvanceDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(step.autoAdvanceDelay);
            }
            else
            {
                while (waitingForTap)
                    yield return null;
            }

            // 정리
            if (tutorialUI != null)
            {
                tutorialUI.HideDialog();
                tutorialUI.HideHighlight();
            }

            // 블록 하이라이트 정리
            if ((step.highlightBlockCoords != null && step.highlightBlockCoords.Length > 0) ||
                step.id == "s11_drill_explain")
            {
                ClearBlockHighlights();
            }

            // Dialog에서 resume 처리 (매칭/드릴 pause 후 탭 시)
            if (isPausedForTutorial)
            {
                isPausedForTutorial = false;
                Debug.Log("[TutorialManager] BRS pause 해제 (Dialog 탭)");
            }

            if (step.pauseGame)
                UnlockInput();
        }

        // ── Highlight 스텝 ──
        private IEnumerator HandleHighlightStep(TutorialStep step)
        {
            if (step.pauseGame)
                LockInput();

            if (tutorialUI != null)
            {
                tutorialUI.ShowHighlight(step.highlightScreenPos, step.highlightRadius);

                waitingForTap = true;
                tutorialUI.ShowDialog(step.characterName, step.title, step.message, true, () =>
                {
                    waitingForTap = false;
                });
            }

            while (waitingForTap)
                yield return null;

            if (tutorialUI != null)
            {
                tutorialUI.HideDialog();
                tutorialUI.HideHighlight();
            }

            if (step.pauseGame)
                UnlockInput();
        }

        // ── ForcedAction 스텝 ──
        private HexBlock[] highlightedCluster = null;

        private IEnumerator HandleForcedActionStep(TutorialStep step)
        {
            // 오버레이 끄기 (게임 보이게) + 힌트 표시
            if (tutorialUI != null)
            {
                tutorialUI.SetDimOverlayActive(false);
                tutorialUI.ShowHintBanner(step.message, 999f); // 계속 표시
                tutorialUI.ShowSkipButton(() => SkipTutorial());
            }

            // ★ 매칭 가능한 클러스터 자동 탐색 + 하이라이트
            bool autoHighlight = (step.allowedCoords == null || step.allowedCoords.Length == 0);
            highlightedCluster = null;

            if (autoHighlight && matchingSystem != null && hexGrid != null)
            {
                HexBlock[] cluster = matchingSystem.FindMatchableCluster();
                if (cluster != null && cluster.Length == 3)
                {
                    highlightedCluster = cluster;
                    Debug.Log($"[TutorialManager] 매칭 가능 클러스터 발견: {cluster[0].Coord}, {cluster[1].Coord}, {cluster[2].Coord}");

                    // 1) 모든 블록 디밍
                    HashSet<HexCoord> clusterCoords = new HashSet<HexCoord>();
                    foreach (var b in cluster) clusterCoords.Add(b.Coord);

                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block == null) continue;
                        if (clusterCoords.Contains(block.Coord))
                            block.SetTutorialGlow(true);   // 밝게 + 펄스
                        else
                            block.SetTutorialDimmed(true);  // 어둡게
                    }

                    // 2) 입력 제한: 클러스터 좌표만 허용
                    if (inputSystem != null)
                    {
                        inputSystem.SetRestrictedMode(true, clusterCoords);
                        inputSystem.SetEnabled(true);
                    }

                    // 3) 손가락 가이드: 손끝이 클러스터 중심을 가리키도록 배치
                    if (tutorialUI != null)
                    {
                        Vector2 centerWorld = Vector2.zero;
                        foreach (var b in cluster)
                        {
                            RectTransform brt = b.GetComponent<RectTransform>();
                            if (brt != null) centerWorld += brt.anchoredPosition;
                        }
                        centerWorld /= 3f;
                        // 손가락 이미지(높이 100) 위로 올려서 손끝이 클러스터 중심을 가리키도록
                        centerWorld.y += 100f;
                        tutorialUI.ShowFingerGuide(centerWorld);
                    }
                }
                else
                {
                    Debug.LogWarning("[TutorialManager] 매칭 가능 클러스터를 찾지 못함 — 자유 입력");
                    if (inputSystem != null) inputSystem.SetEnabled(true);
                    if (step.showFingerGuide && tutorialUI != null)
                        tutorialUI.ShowFingerGuide(step.fingerGuidePos);
                }
            }
            else if (step.forceDrillClick && pendingDrillCoord.HasValue)
            {
                // 드릴 클릭 강제: 드릴 좌표만 허용
                var coords = new HashSet<HexCoord> { pendingDrillCoord.Value };
                if (inputSystem != null)
                {
                    inputSystem.SetRestrictedMode(true, coords);
                    inputSystem.SetEnabled(true);
                }

                // 드릴 블록 글로우 + 나머지 딤
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block == null) continue;
                        if (block.Coord.Equals(pendingDrillCoord.Value))
                            block.SetTutorialGlow(true);
                        else
                            block.SetTutorialDimmed(true);
                    }
                }

                // 손가락 가이드: 드릴 블록 위치
                if (step.showFingerGuide && tutorialUI != null && hexGrid != null)
                {
                    var drillBlock = hexGrid.GetBlock(pendingDrillCoord.Value);
                    if (drillBlock != null)
                    {
                        RectTransform brt = drillBlock.GetComponent<RectTransform>();
                        if (brt != null)
                        {
                            Vector2 pos = brt.anchoredPosition;
                            pos.y += 100f; // 손끝이 드릴을 가리키도록
                            tutorialUI.ShowFingerGuide(pos);
                        }
                    }
                }
            }
            else if (!autoHighlight)
            {
                // 지정 좌표로 제한
                var coords = new HashSet<HexCoord>();
                foreach (var c in step.allowedCoords) coords.Add(c);

                // 지정 좌표 글로우 + 나머지 딤
                if (hexGrid != null)
                {
                    foreach (var block in hexGrid.GetAllBlocks())
                    {
                        if (block == null) continue;
                        if (coords.Contains(block.Coord))
                            block.SetTutorialGlow(true);
                        else
                            block.SetTutorialDimmed(true);
                    }
                }

                if (inputSystem != null)
                {
                    inputSystem.SetRestrictedMode(true, coords);
                    inputSystem.SetEnabled(true);
                }

                // 손가락 가이드: 클러스터 중심
                if (step.showFingerGuide && tutorialUI != null && hexGrid != null)
                {
                    Vector2 centerWorld = Vector2.zero;
                    int count = 0;
                    foreach (var c in step.allowedCoords)
                    {
                        var block = hexGrid.GetBlock(c);
                        if (block != null)
                        {
                            RectTransform brt = block.GetComponent<RectTransform>();
                            if (brt != null) { centerWorld += brt.anchoredPosition; count++; }
                        }
                    }
                    if (count > 0)
                    {
                        centerWorld /= count;
                        centerWorld.y += 100f;
                        tutorialUI.ShowFingerGuide(centerWorld);
                    }
                }
            }
            else
            {
                // 폴백: 자유 입력
                if (inputSystem != null) inputSystem.SetEnabled(true);
                if (step.showFingerGuide && tutorialUI != null)
                    tutorialUI.ShowFingerGuide(step.fingerGuidePos);
            }

            // ForcedAction은 비주얼 설정만 하고 즉시 종료
            // → 정리는 다음 WaitForEvent 스텝 완료 시 또는 시퀀스 완료 시 수행
            yield return null;
        }

        /// <summary>
        /// 모든 블록의 튜토리얼 비주얼 초기화
        /// </summary>
        private void ClearBlockHighlights()
        {
            if (hexGrid == null) return;
            foreach (var block in hexGrid.GetAllBlocks())
            {
                if (block != null)
                    block.ClearTutorialVisuals();
            }
            highlightedCluster = null;
        }

        // ── FreePlayHint 스텝 ──
        private IEnumerator HandleFreePlayHintStep(TutorialStep step)
        {
            // 입력 해제, 힌트만 표시 후 자동 소멸
            UnlockInput();

            if (tutorialUI != null)
            {
                tutorialUI.HideAll();
                tutorialUI.ShowHintBanner(step.message, step.hintDuration);
            }

            yield return new WaitForSecondsRealtime(step.hintDuration + 0.5f);
        }

        // ── WaitForEvent 스텝 ──
        private IEnumerator HandleWaitForEventStep(TutorialStep step)
        {
            waitingForEvent = true;
            pendingWaitEvent = step.waitEvent;

            Debug.Log($"[TutorialManager] 이벤트 대기: {step.waitEvent}");

            // 드릴 튜토리얼 pause 이벤트는 타임아웃 60초 (캐스케이드 대기)
            bool isDrillPauseEvent = step.waitEvent == TutorialWaitEvent.MatchHighlightPause ||
                                     step.waitEvent == TutorialWaitEvent.DrillCreatedPause ||
                                     step.waitEvent == TutorialWaitEvent.DrillActivated;
            float timeout = isDrillPauseEvent ? 60f : 15f;

            float elapsed = 0f;
            while (waitingForEvent && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (waitingForEvent)
            {
                Debug.LogWarning($"[TutorialManager] 이벤트 대기 타임아웃: {step.waitEvent}");
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
            }

            // ★ ForcedAction에서 설정한 비주얼/입력 제한 정리
            ClearBlockHighlights();

            if (tutorialUI != null)
            {
                tutorialUI.HideFingerGuide();
                tutorialUI.HideHintBanner();
            }

            if (inputSystem != null)
                inputSystem.SetRestrictedMode(false, null);

            // 캐스케이드 완료 대기 (매칭 후 블록 정리 시간) — pause 이벤트에서는 스킵
            if (!isDrillPauseEvent)
                yield return new WaitForSecondsRealtime(0.5f);
        }

        // ============================================================
        // 드릴 튜토리얼 콜백 (BRS/DrillBlockSystem에서 호출)
        // ============================================================

        /// <summary>
        /// 매칭 하이라이트 완료 시 호출 (BRS에서 특수 블록 생성 전).
        /// MatchHighlightPause 대기 중이면 pause 걸고 이벤트 해제.
        /// </summary>
        public void OnMatchHighlightComplete()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.MatchHighlightPause)
            {
                isPausedForTutorial = true;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] MatchHighlightPause 트리거 — BRS pause 시작");
            }
        }

        /// <summary>
        /// 드릴 생성 완료 시 호출 (BRS에서 드릴 블록 생성 후).
        /// DrillCreatedPause 대기 중이면 pause 걸고 이벤트 해제.
        /// </summary>
        public void OnDrillCreated(HexCoord coord)
        {
            pendingDrillCoord = coord;

            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.DrillCreatedPause)
            {
                isPausedForTutorial = true;
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log($"[TutorialManager] DrillCreatedPause 트리거 — 드릴 좌표={coord}, BRS pause 시작");
            }
        }

        /// <summary>
        /// 드릴 발동 완료 시 호출 (DrillBlockSystem에서 드릴 코루틴 끝).
        /// DrillActivated 대기 중이면 이벤트 해제.
        /// </summary>
        public void OnDrillActivated()
        {
            if (waitingForEvent && pendingWaitEvent == TutorialWaitEvent.DrillActivated)
            {
                waitingForEvent = false;
                pendingWaitEvent = TutorialWaitEvent.None;
                Debug.Log("[TutorialManager] DrillActivated 트리거");
            }
        }

        // ============================================================
        // 시퀀스 완료 / 스킵
        // ============================================================

        private void CompleteSequence()
        {
            if (currentSequence != null && currentSequence.showOnce)
                MarkCompleted(currentSequence.sequenceId);

            isTutorialActive = false;
            currentSequence = null;
            currentStepIndex = 0;
            waitingForTap = false;
            waitingForEvent = false;
            pendingWaitEvent = TutorialWaitEvent.None;

            // 블록 하이라이트 정리
            ClearBlockHighlights();

            if (tutorialUI != null) tutorialUI.HideAll();
            UnlockInput();

            OnTutorialEnded?.Invoke();
            Debug.Log("[TutorialManager] 시퀀스 완료");
        }

        /// <summary>
        /// 현재 튜토리얼 스킵 (전체 시퀀스 완료 처리)
        /// </summary>
        public void SkipTutorial()
        {
            if (!isTutorialActive) return;

            Debug.Log($"[TutorialManager] 튜토리얼 스킵: {currentSequence?.sequenceId}");

            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
                sequenceCoroutine = null;
            }

            // BRS pause 해제
            isPausedForTutorial = false;
            pendingDrillCoord = null;

            // 입력 제한 해제
            if (inputSystem != null)
                inputSystem.SetRestrictedMode(false, null);

            CompleteSequence();
        }

        // ============================================================
        // 입력 제어
        // ============================================================

        private void LockInput()
        {
            if (inputSystem != null)
                inputSystem.SetEnabled(false);
        }

        private void UnlockInput()
        {
            if (inputSystem != null)
                inputSystem.SetEnabled(true);
        }

        // ============================================================
        // 디버그
        // ============================================================

        /// <summary>
        /// 특정 시퀀스를 강제로 재생 (디버그용)
        /// </summary>
        public void DebugPlaySequence(string sequenceId)
        {
            var seq = allSequences.FirstOrDefault(s => s.sequenceId == sequenceId);
            if (seq != null)
            {
                // 완료 상태 무시하고 재생
                if (isTutorialActive) SkipTutorial();
                StartSequence(seq);
            }
            else
            {
                Debug.LogWarning($"[TutorialManager] 시퀀스 '{sequenceId}' 를 찾을 수 없음");
            }
        }
    }
}

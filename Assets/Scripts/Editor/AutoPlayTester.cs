// ============================================================================
// AutoPlayTester.cs — 자동 플레이테스트 봇 (에디터 전용)
// ============================================================================
// 레벨 디자인 검증용: 게임을 스스로 플레이하며 스테이지별 결과를 기록한다.
//
// 시작: ".claude/autoplay_start" 파일 생성 (내용: "시작스테이지 끝스테이지", 예: "1 10")
// 결과: ".claude/autoplay/results.log" 에 스테이지별 한 줄씩 누적
//   형식: stage,result,startTurns,leftTurns,elapsedSec,missions
//
// 플레이 정책 (상급 유저 근사):
//   - 0.6초 간격으로: 특수블록 있고 MP 충분하면 발동, 아니면 최다 정화 회전 실행
//   - 리워드 모달 → 첫 카드 자동 선택 / 클리어 팝업 → 다음 / 게임오버 → 기록 후 로비→다음 스테이지
// ============================================================================

using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.EditorTools
{
    [InitializeOnLoad]
    public static class AutoPlayTester
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string StartTriggerPath => Path.Combine(ProjectRoot, ".claude", "autoplay_start");
        private static string ResultsDir => Path.Combine(ProjectRoot, ".claude", "autoplay");
        private static string ResultsPath => Path.Combine(ResultsDir, "results.log");

        // 세션 상태 (도메인 리로드 생존)
        private const string KeyActive = "AutoPlay_Active";
        private const string KeyAlwaysOn = "AutoPlay_AlwaysOn"; // 상시 가동 (정지/로비 방치 자동 복구)
        private static double stoppedSince;     // Play 꺼짐 감지 시각
        private static double lobbyIdleSince;   // 로비 방치 감지 시각
        private static bool interactionModeSet; // 에디터 무스로틀 1회 설정
        private const string KeyCurStage = "AutoPlay_CurStage";
        private const string KeyEndStage = "AutoPlay_EndStage";
        private const string KeyStartTurns = "AutoPlay_StartTurns";
        private const string KeyStageStartTime = "AutoPlay_StageStartTime";
        private const string KeyPhase = "AutoPlay_Phase"; // 0=스테이지선택대기 1=플레이중 2=종료처리중

        private static double nextActTime;
        private static double nextPollTime;
        private static string lastRotKey = "";  // 직전 회전 클러스터 (실패 감지 → 방향 반전)
        private static bool lastRotFlip = false;

        static AutoPlayTester()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            // 0.3초 폴링
            if (EditorApplication.timeSinceStartup < nextPollTime) return;
            nextPollTime = EditorApplication.timeSinceStartup + 0.3;

            // ★ 포커스를 잃어도(커서 뺏김) 게임 루프가 계속 돌도록 보장 — 플레이 중 상시 재설정
            if (EditorApplication.isPlaying && !Application.runInBackground)
                Application.runInBackground = true;
            if (!interactionModeSet)
            {
                // 에디터 백그라운드 스로틀 해제 (No Throttling) — 실패해도 무해
                interactionModeSet = true;
                try { EditorPrefs.SetInt("InteractionMode", 1); } catch { }
            }

            // ── 중지 트리거 (.claude/autoplay_stop) — 상시 가동 포함 완전 중지 ──
            string stopTrigger = Path.Combine(ProjectRoot, ".claude", "autoplay_stop");
            if (File.Exists(stopTrigger))
            {
                try { File.Delete(stopTrigger); } catch { }
                SessionState.SetBool(KeyAlwaysOn, false);
                SessionState.SetBool("AutoPlay_Learn", false); // 학습 게이트 해제(검증 재개 가능)
                Stop();
                Log("=== 자동 플레이테스트 완전 중지 (stop 트리거) ===");
                return;
            }

            // ── 수동 스크린샷 트리거 (.claude/autoplay_screenshot 생성 시 즉시 캡처) ──
            string shotTrigger = Path.Combine(ProjectRoot, ".claude", "autoplay_screenshot");
            if (EditorApplication.isPlaying && File.Exists(shotTrigger))
            {
                try { File.Delete(shotTrigger); } catch { }
                string p = CaptureScreenshot("manual");
                Log($"  수동 스크린샷: {p}");
            }

            // ── 시작 트리거 감지 (진행 중이어도 트리거가 있으면 새 세션으로 재시작) ──
            if (File.Exists(StartTriggerPath))
            {
                string[] parts;
                try
                {
                    parts = File.ReadAllText(StartTriggerPath).Trim().Split(' ');
                    File.Delete(StartTriggerPath);
                }
                catch { return; }

                int s = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 1;
                int e = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : s;
                bool resetTutorial = parts.Length > 2 && parts[2] == "reset";
                bool fullReset = parts.Length > 2 && parts[2] == "fullreset";
                // ── 학습 모드: "s e learn [N=k]" — 가중치 자가튜닝 주행 ──
                bool learn = parts.Length > 2 && parts[2] == "learn";
                int learnN = 3;
                if (learn)
                    for (int pi = 3; pi < parts.Length; pi++)
                        if (parts[pi].StartsWith("N=") && int.TryParse(parts[pi].Substring(2), out var nn)) learnN = Mathf.Max(1, nn);
                if (learn && SessionState.GetBool("Verify_Active", false))
                {
                    Log("WARN: 검증 실행 중 — 학습 시작 거부(상호배제). verify_stop 후 재시도");
                    return;
                }
                SessionState.SetBool("AutoPlay_Learn", learn);
                SessionState.SetInt("Learn_StartStage", s);
                SessionState.SetBool(KeyActive, true);
                SessionState.SetBool(KeyAlwaysOn, true); // ★ 상시 가동: 정지/로비 방치 시 자동 복구
                SessionState.SetInt(KeyCurStage, s);
                SessionState.SetInt(KeyEndStage, e);
                SessionState.SetInt(KeyPhase, 0);
                SessionState.SetInt("AutoPlay_Retry", 0);
                SessionState.SetBool("AutoPlay_ResetTutorial", resetTutorial);
                SessionState.SetBool("AutoPlay_FullReset", fullReset);
                SessionState.SetFloat("AutoPlay_WaitUntil", 0f);
                Directory.CreateDirectory(ResultsDir);
                // 학습: 시작 스테이지가 잠겨 있어도 진입 가능하도록 해금(클리어/게임오버 시 다음 레벨은 자동 해금됨)
                if (learn) { try { JewelsHexaPuzzle.Data.LevelRegistry.UnlockLevelsUpTo(s); } catch { } }
                if (learn) LearningAutoPlay.BeginSession(s, e, learnN);
                Log($"=== 자동 플레이테스트 시작: Stage {s} ~ {e}{(learn ? $" (학습 모드 N={learnN})" : fullReset ? " (전체 데이터 초기화 — 신규 유저 모드)" : resetTutorial ? " (튜토리얼 리셋)" : "")} ===");
                if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = true;
                return;
            }

            // ── ★ 상시 가동 복구: Play 꺼짐 → 자동 재생 / 봇 꺼진 채 로비 방치 → 최고 해금 레벨부터 재개 ──
            if (SessionState.GetBool(KeyAlwaysOn, false))
            {
                if (!EditorApplication.isPlaying)
                {
                    // 갱신 사이클(UnityAutoRefresh)이나 컴파일/전환 중에는 개입하지 않음
                    if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode ||
                        SessionState.GetBool("HexaPuzzle_AutoRefresh_ResumePlay", false))
                    {
                        stoppedSince = 0;
                    }
                    else if (stoppedSince <= 0)
                    {
                        stoppedSince = EditorApplication.timeSinceStartup;
                    }
                    else if (EditorApplication.timeSinceStartup - stoppedSince > 10.0)
                    {
                        stoppedSince = 0;
                        Log("상시 가동: 에디터 정지 감지 → 자동 재생");
                        EditorApplication.isPlaying = true;
                    }
                    return;
                }
                stoppedSince = 0;

                if (!SessionState.GetBool(KeyActive, false))
                {
                    var gmIdle = GameManager.Instance;
                    bool inLobby = gmIdle != null
                        && gmIdle.CurrentState != GameState.Playing
                        && gmIdle.CurrentState != GameState.Processing
                        && gmIdle.CurrentState != GameState.StageClear
                        && gmIdle.CurrentState != GameState.GameOver;
                    if (inLobby)
                    {
                        if (lobbyIdleSince <= 0)
                        {
                            lobbyIdleSince = EditorApplication.timeSinceStartup;
                        }
                        else if (EditorApplication.timeSinceStartup - lobbyIdleSince > 15.0)
                        {
                            lobbyIdleSince = 0;
                            int highest = GetHighestUnlockedLevel();
                            SessionState.SetBool(KeyActive, true);
                            SessionState.SetInt(KeyCurStage, highest);
                            SessionState.SetInt(KeyEndStage, 150);
                            SessionState.SetInt(KeyPhase, 0);
                            SessionState.SetInt("AutoPlay_Retry", 0);
                            SessionState.SetFloat("AutoPlay_WaitUntil", 0f);
                            Log($"상시 가동: 로비 방치 감지 → 최고 해금 Stage {highest}부터 주행 재개 (~150)");
                        }
                    }
                    else
                    {
                        lobbyIdleSince = 0;
                    }
                }
            }

            if (!SessionState.GetBool(KeyActive, false)) return;

            if (!EditorApplication.isPlaying) return;
            var gm = GameManager.Instance;
            if (gm == null) return;

            int phase = SessionState.GetInt(KeyPhase, 0);
            int curStage = SessionState.GetInt(KeyCurStage, 1);

            // ── Phase 0: 로비에서 스테이지 시작 ──
            if (phase == 0)
            {
                // 로비 표시 대기 후 스테이지 선택 (리플렉션 — private OnLobbyStageSelected)
                if (gm.CurrentState == GameState.Playing || gm.CurrentState == GameState.Processing) return; // 아직 이전 게임?
                // 초기화 후 안정화 대기 (씬 리로드 직후 매니저 초기화 시간)
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat("AutoPlay_WaitUntil", 0f))
                    return;

                // ★ 전환 연출(2.5초) 종료 대기 — 실유저는 backdrop raycast로 차단되는 재진입을 봇도 따른다.
                //   (전환 중 StartGame 재호출이 "이미 전환 중" 안전망 경고 1,353건의 원인)
                //   오버레이 고착 대비 5초 상한: 초과 시 통과.
                var lto = JewelsHexaPuzzle.UI.LobbyTransitionOverlay.Instance;
                if (lto != null && lto.IsRunning)
                {
                    float tw = SessionState.GetFloat("AutoPlay_TransWaitStart", -1f);
                    if (tw < 0f)
                    {
                        SessionState.SetFloat("AutoPlay_TransWaitStart", (float)EditorApplication.timeSinceStartup);
                        return;
                    }
                    if (EditorApplication.timeSinceStartup - tw < 5f) return;
                    // 5초 초과 — 고착 판단, 통과
                }
                SessionState.SetFloat("AutoPlay_TransWaitStart", -1f);

                // ★ 전체 데이터 초기화 옵션 (최초 1회): PlayerPrefs 전체 삭제 + 씬 리로드 — 진짜 신규 유저 상태
                if (SessionState.GetBool("AutoPlay_FullReset", false))
                {
                    SessionState.SetBool("AutoPlay_FullReset", false);
                    var rmi = typeof(GameManager).GetMethod("ResetAllGameData",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (rmi != null)
                    {
                        Log("전체 데이터 초기화 실행 (PlayerPrefs.DeleteAll + 씬 리로드) — 신규 유저 모드");
                        SessionState.SetFloat("AutoPlay_WaitUntil",
                            (float)EditorApplication.timeSinceStartup + 6f); // 씬 리로드 안정화 대기
                        rmi.Invoke(gm, null);
                    }
                    else Log("WARN: ResetAllGameData 미발견 — 초기화 생략");
                    return;
                }

                // 튜토리얼 리셋 옵션 (최초 1회): 모든 튜토리얼 미완료 상태로 — 1레벨부터 자연 진행
                if (SessionState.GetBool("AutoPlay_ResetTutorial", false))
                {
                    SessionState.SetBool("AutoPlay_ResetTutorial", false);
                    if (TutorialManager.Instance != null)
                    {
                        TutorialManager.Instance.ResetAllTutorials();
                        TutorialManager.Instance.ResetNotifiedFeatures();
                        TutorialManager.Instance.SyncFeatureUnlocks();
                        Log("튜토리얼 전체 리셋 완료 — 튜토리얼 포함 검증 모드");
                    }
                }

                var mi = typeof(GameManager).GetMethod("OnLobbyStageSelected",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi == null) { Log("ERROR: OnLobbyStageSelected 미발견"); Stop(); return; }
                Log($"--- Stage {curStage} 시작 ---");
                mi.Invoke(gm, new object[] { curStage });
                SessionState.SetInt(KeyPhase, 1);
                SessionState.SetFloat(KeyStageStartTime, (float)EditorApplication.timeSinceStartup);
                SessionState.SetInt(KeyStartTurns, -1); // 시작 턴은 Playing 진입 후 캡처
                MarkAlive(); // 워치독 기준점 리셋
                lastTutorialStepKey = "";
                tutorialLastProgressTime = 0;
                return;
            }

            // ── Phase 3: 클리어 팝업 클릭 후 전환 대기 (로비행/즉시 다음 스테이지 모두 대응) ──
            if (phase == 3)
            {
                float waitUntil3 = SessionState.GetFloat("AutoPlay_WaitUntil", 0f);
                if (EditorApplication.timeSinceStartup < waitUntil3) return;
                // ★ 전환 분기 타임아웃 — 20초 넘게 로비도 인게임도 아니면 Phase 0으로 강제 복귀
                if (EditorApplication.timeSinceStartup - waitUntil3 > 20.0)
                {
                    Log("WARN,PHASE3_TIMEOUT — 전환 감지 실패, 로비 선택으로 강제 복귀");
                    SessionState.SetInt(KeyPhase, 0);
                    return;
                }
                if (gm.CurrentState == GameState.Playing && gm.SelectedStage == curStage && gm.CurrentTurns > 0)
                {
                    // 버튼이 곧장 다음 스테이지를 시작한 경우 (NextStage형)
                    SessionState.SetInt(KeyPhase, 1);
                    SessionState.SetFloat(KeyStageStartTime, (float)EditorApplication.timeSinceStartup);
                    SessionState.SetInt(KeyStartTurns, -1);
                    Log($"--- Stage {curStage} 진행 (팝업 → 즉시 시작 감지) ---");
                }
                else if (gm.CurrentState != GameState.Playing && gm.CurrentState != GameState.Processing
                         && gm.CurrentState != GameState.StageClear)
                {
                    // 로비로 돌아간 경우 (확인→ReturnToLobby형) → Phase 0이 스테이지 선택
                    SessionState.SetInt(KeyPhase, 0);
                    Log($"--- 로비 복귀 감지 → Stage {curStage} 선택 예정 ---");
                }
                // 그 외(전환 연출 중)는 다음 폴링에서 재판정
                return;
            }

            // ── Phase 1: 플레이 중 ──
            if (phase == 1)
            {
                // 시작 턴 캡처 (게임 로딩 완료 후 1회)
                if (SessionState.GetInt(KeyStartTurns, -1) < 0 && gm.CurrentState == GameState.Playing && gm.CurrentTurns > 0)
                    SessionState.SetInt(KeyStartTurns, gm.CurrentTurns);

                // ★ 전역 무행동 워치독 — 턴/상태 변화가 40초간 전무하면 어떤 원인이든 강제 탈출
                if (lastAliveTime <= 0) MarkAlive();
                if (gm.CurrentTurns != lastSeenTurns || gm.CurrentState != lastSeenState)
                {
                    lastSeenTurns = gm.CurrentTurns;
                    lastSeenState = gm.CurrentState;
                    MarkAlive();
                }
                if (EditorApplication.timeSinceStartup - lastAliveTime > WATCHDOG_SECONDS)
                {
                    EscapeStuck(gm, curStage);
                    return;
                }

                // ★ Phase 1인데 로비 상태 = 비정상 (갱신 자동 재개/예외 복귀) → 즉시 재선택
                //   (잔류 튜토리얼 시퀀스가 로비에서 무의미한 행동을 반복하는 패턴 차단)
                if (gm.CurrentState == GameState.Lobby)
                {
                    Log($"  로비 상태 감지 (Phase 1) → Stage {curStage} 재선택");
                    if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                        TutorialManager.Instance.AbortTutorial();
                    SessionState.SetInt(KeyPhase, 0);
                    MarkAlive();
                    return;
                }

                // ★ 튜토리얼 진행 (다이얼로그 탭 / 강제 액션 수행 / 멈춤 감지)
                //   반환 true = 튜토리얼이 입력을 점유(대기/강제 단계) → 일반 행동 금지
                //   반환 false = 자유 플레이 대기 단계(예: "드릴을 만들어 보세요") → 일반 행동 계속
                if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive)
                {
                    if (HandleTutorial()) return;
                }
                else
                    tutorialLastProgressTime = 0; // 튜토리얼 비활성 → 멈춤 타이머 리셋

                // 리워드 모달 → 첫 카드 선택
                if (SkillUpgradeOfferSystem.Instance != null && SkillUpgradeOfferSystem.Instance.IsChoiceModalOpen)
                {
                    var modal = GameObject.Find("SkillUpgradeChoiceModal");
                    if (modal != null)
                    {
                        foreach (var btn in modal.GetComponentsInChildren<Button>())
                        {
                            if (btn.name.StartsWith("ChoiceBtn_"))
                            {
                                Log($"  리워드 선택: {btn.name}");
                                btn.onClick.Invoke();
                                MarkAlive();
                                break;
                            }
                        }
                    }
                    return;
                }

                // 스테이지 클리어 → 기록 + 팝업 버튼 실제 클릭으로 진행 (실유저 흐름)
                if (gm.CurrentState == GameState.StageClear)
                {
                    var popup = GameObject.Find("StageClearPopup");
                    if (popup == null || !popup.activeInHierarchy) return; // 팝업 연출 대기
                    RecordResult($"CLEAR(시도{SessionState.GetInt("AutoPlay_Retry", 0) + 1}회만에)", gm);

                    int endStage = SessionState.GetInt(KeyEndStage, curStage);
                    bool learnClr = SessionState.GetBool("AutoPlay_Learn", false);
                    int nextStage;
                    if (curStage >= endStage)
                    {
                        if (learnClr)
                        {
                            // 학습: 범위 1패스 완주 → ES 세대 진행, startStage로 랩(연속 학습)
                            LearningAutoPlay.OnPassComplete();
                            if (LearningAutoPlay.IsFinished())
                            {
                                Log("=== 학습 완료 (최대 세대 도달) ===");
                                SessionState.SetBool("AutoPlay_Learn", false);
                                Stop();
                                return;
                            }
                            nextStage = SessionState.GetInt("Learn_StartStage", curStage);
                        }
                        else
                        {
                            Log("=== 자동 플레이테스트 완료 (목표 스테이지 도달) ===");
                            Stop();
                            return;
                        }
                    }
                    else nextStage = curStage + 1;

                    // 팝업 내부 버튼 클릭 — "다음" 텍스트 우선, 없으면 첫 활성 버튼("확인" 등)
                    Button clickTarget = null;
                    foreach (var btn in popup.GetComponentsInChildren<Button>())
                    {
                        var txt = btn.GetComponentInChildren<Text>();
                        if (txt != null && txt.text.Contains("다음")) { clickTarget = btn; break; }
                        if (clickTarget == null) clickTarget = btn;
                    }
                    SessionState.SetInt(KeyCurStage, nextStage);
                    SessionState.SetInt("AutoPlay_Retry", 0);
                    SessionState.SetInt(KeyPhase, 3); // 전환 대기 — 클릭 결과(로비/바로 다음 스테이지)를 보고 분기
                    SessionState.SetFloat("AutoPlay_WaitUntil", (float)EditorApplication.timeSinceStartup + 2f);
                    if (clickTarget != null)
                    {
                        Log($"  클리어 팝업 버튼 클릭: '{clickTarget.GetComponentInChildren<Text>()?.text}'");
                        clickTarget.onClick.Invoke();
                    }
                    else
                    {
                        Log("WARN: 클리어 팝업에 버튼 미발견 → NextStage() 폴백");
                        gm.NextStage();
                    }
                    return;
                }

                // 게임오버 → 기록 + 재도전(신규 유저 흐름 — 다음 스테이지는 잠겨 있을 수 있음)
                if (gm.CurrentState == GameState.GameOver)
                {
                    int retry = SessionState.GetInt("AutoPlay_Retry", 0);
                    RecordResult($"GAMEOVER(시도{retry + 1})", gm);

                    // 팝업 닫고 로비로
                    var fld = typeof(GameManager).GetField("gameOverPopupObj",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var popupObj = fld != null ? fld.GetValue(gm) as GameObject : null;
                    if (popupObj != null) popupObj.SetActive(false);
                    Time.timeScale = 1f;
                    gm.ExitToLobby();

                    // ── 학습 모드: 재도전 없이(첫 시도만 fitness 표본) 다음 레벨/랩으로 진행 ──
                    if (SessionState.GetBool("AutoPlay_Learn", false))
                    {
                        int endL = SessionState.GetInt(KeyEndStage, curStage);
                        if (curStage >= endL)
                        {
                            LearningAutoPlay.OnPassComplete();
                            if (LearningAutoPlay.IsFinished())
                            {
                                Log("=== 학습 완료 (최대 세대 도달) ===");
                                SessionState.SetBool("AutoPlay_Learn", false);
                                Stop();
                                return;
                            }
                            SessionState.SetInt(KeyCurStage, SessionState.GetInt("Learn_StartStage", curStage));
                        }
                        else
                        {
                            JewelsHexaPuzzle.Data.LevelRegistry.UnlockLevelsUpTo(curStage + 1); // 잠김 방지
                            SessionState.SetInt(KeyCurStage, curStage + 1);
                        }
                        SessionState.SetInt("AutoPlay_Retry", 0);
                        SessionState.SetInt(KeyPhase, 0);
                        return;
                    }

                    if (retry < 4)
                    {
                        // 같은 스테이지 재도전 (클리어까지 최대 5회 시도)
                        SessionState.SetInt("AutoPlay_Retry", retry + 1);
                        SessionState.SetInt(KeyPhase, 0);
                        Log($"  Stage {curStage} 재도전 ({retry + 2}/5회차)");
                        return;
                    }

                    // 5회 모두 실패 → 밸런스 문제 신호! 기록 후 다음 스테이지 강제 해금하고 계속
                    Log($"WARN,STAGE_TOO_HARD,stage={curStage} — 5회 연속 게임오버 (레벨 디자인 점검 필요)");
                    int endStage = SessionState.GetInt(KeyEndStage, curStage);
                    if (curStage >= endStage) { Log("=== 완료 (마지막 스테이지) ==="); Stop(); return; }
                    JewelsHexaPuzzle.Data.LevelRegistry.UnlockLevelsUpTo(curStage + 1); // 미클리어라 잠김 → 테스트 계속 위해 해금
                    SessionState.SetInt(KeyCurStage, curStage + 1);
                    SessionState.SetInt("AutoPlay_Retry", 0);
                    SessionState.SetInt(KeyPhase, 0);
                    return;
                }

                // 행동 가능 상태에서만 플레이 (0.6초 간격)
                if (EditorApplication.timeSinceStartup < nextActTime) return;
                if (gm.CurrentState != GameState.Playing || gm.IsPaused) return;

                var brs = Object.FindObjectOfType<BlockRemovalSystem>();
                var rot = Object.FindObjectOfType<RotationSystem>();
                var match = Object.FindObjectOfType<MatchingSystem>();
                var grid = Object.FindObjectOfType<HexGrid>();
                if (brs == null || rot == null || match == null || grid == null) return;
                if (brs.IsProcessing || rot.IsRotating) return;
                if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsProcessingTurn) return;

                // ★ 실제 플레이어와 동일 게이트: 입력이 비활성인 구간(스테이지 인트로/캐스케이드→고블린 턴
                //   사이/전환 연출)에서 행동하면 처리 흐름이 꼬여 BRS 스턱(PostRecovery timeout)을 유발한다.
                var inp = Object.FindObjectOfType<InputSystem>();
                if (inp == null || !inp.IsEnabled) return;

                nextActTime = EditorApplication.timeSinceStartup + 0.35;

                // ★ 장기 주행 가속: 외부 정지(모달/퍼즈)가 아니면 3배속 유지
                //   (HitStop 등이 1로 되돌려도 매 행동 틱마다 재설정)
                if (!VisualConstants.IsGamePausedExternally() && Time.timeScale > 0f && Time.timeScale < 3f)
                    Time.timeScale = 3f;

                // 1) 특수블록 발동 (MP 충분하면 — 몬스터 스테이지 클리어에 필수)
                if (TryActivateSpecial(gm, grid)) { MarkAlive(); return; }

                // 2) 망치 아이템 사용 (게이지 충전 + MP 충분 시 — 고블린 직격 우선)
                if (TryUseHammer(grid)) { MarkAlive(); return; }

                // 3) ★ 미션 지향 회전 — 미션 타겟 색(+8)/고블린 점유(+12)/낙하 데미지(+4) 가중
                //    점수가 같으면 정화 셀 수가 많은 쪽이 자연히 우세 (셀당 기본 1점)
                int clearCount;
                bool clockwise;
                var cluster = match.FindBestMissionCluster(out clearCount, out clockwise);

                // ★ 폴백: '회전 셀 포함 삼각형' 기준으로 못 찾으면 보드 전체 매칭 기준(FindMatchableCluster —
                //   게임 교착 판정 HasPossibleMoves와 동일 계열)으로 재탐색. 이 갭이 Stage 4
                //   "MP 고갈 + 봇만 무행동" 워치독 발동의 원인이었음 (실유저는 매칭 가능한 보드).
                if (cluster == null || clearCount <= 0)
                {
                    cluster = match.FindMatchableCluster();
                    if (cluster != null) { clearCount = 1; clockwise = true; } // 방향 미상 — 반복 감지가 반전 처리
                }

                if (cluster != null && clearCount > 0)
                {
                    // 직전과 같은 클러스터가 또 최적 = 직전 회전이 매칭 실패(되돌림)했다는 신호
                    // → 시뮬레이션과 실제 회전의 방향 규약 차이 대비, 방향을 반전해 재시도
                    string rkey = $"{cluster[0].Coord}|{cluster[1].Coord}|{cluster[2].Coord}";
                    if (rkey == lastRotKey) lastRotFlip = !lastRotFlip;
                    else { lastRotKey = rkey; lastRotFlip = false; }
                    bool useCw = lastRotFlip ? !clockwise : clockwise;

                    if (!useCw) rot.SetOneTimeCounterClockwise();
                    rot.TryRotate(cluster[0], cluster[1], cluster[2]);
                    MarkAlive();
                }
                // 회전 불가(교착)면 교착 시스템이 처리(자동 재배치) — 봇은 대기, 워치독이 최종 감시
                return;
            }
        }

        // ============================================================
        // 튜토리얼 자동 진행 + 문제 감지
        // ============================================================
        private static double tutorialLastProgressTime;
        private static double nextTutorialActTime;
        private static string lastForcedRotKey = "";   // 강제 회전 반복 감지
        private static int forcedRotRepeat = 0;
        private static string lastTutorialStepKey = ""; // 스텝 전진 감지 (진행 판단의 단일 기준)
        private static int sameStepActCount = 0;        // 같은 스텝에서 wait 행동 반복 횟수 (무효 행동 가드)

        // ── 전역 무행동 워치독 — 튜토리얼/비튜토리얼 불문 모든 멈춤의 최종 안전망 ──
        private static double lastAliveTime;
        private static int lastSeenTurns = -1;
        private static GameState lastSeenState = GameState.Loading;
        private static int watchdogStrikes = 0;
        private const double WATCHDOG_SECONDS = 40.0;

        private static void MarkAlive()
        {
            lastAliveTime = EditorApplication.timeSinceStartup;
            watchdogStrikes = 0;
        }

        /// <summary>스테이지별 튜토리얼 STUCK 임계값 — 첫 막힘 15초, 이후 8초 (반복 막힘 시 빠른 탈출).</summary>
        private static double GetTutorialStuckThreshold(int stage)
        {
            return SessionState.GetInt($"AutoPlay_TutStuck_{stage}", 0) == 0 ? 15.0 : 8.0;
        }

        /// <summary>
        /// 강제 멈춤 탈출 — 1차: 튜토리얼 스킵 + 입력/배속 정상화, 2차(연속): 로비로 나가 스테이지 재시작.
        /// </summary>
        private static void EscapeStuck(GameManager gm, int curStage)
        {
            watchdogStrikes++;
            var inp = Object.FindObjectOfType<InputSystem>();
            var tm = TutorialManager.Instance;
            bool tutActive = tm != null && tm.IsTutorialActive;
            string shot = CaptureScreenshot($"stuck_stage{curStage}_strike{watchdogStrikes}");
            Log($"WARN,BOT_WATCHDOG,stage={curStage},strike={watchdogStrikes},state={gm.CurrentState}," +
                $"paused={gm.IsPaused},tutorial={tutActive}" +
                $"{(tutActive ? ",step=" + GetCurrentTutorialStepInfo(tm) : "")}," +
                $"inputEnabled={(inp != null && inp.IsEnabled)},timeScale={Time.timeScale:0.##}" +
                $" — {WATCHDOG_SECONDS}초 무진행, 강제 탈출. 스크린샷={shot}");

            if (watchdogStrikes >= 2)
            {
                // 2연속 탈출 실패 → 로비로 나가 같은 스테이지 재시작 (시도 횟수 미증가)
                Log($"WARN,HARD_STUCK_RESTART,stage={curStage} — 1차 탈출 무효, 스테이지 강제 재시작");
                if (tutActive) { tm.SkipTutorial(); tm.ForceUnpause(); }
                Time.timeScale = 1f;
                gm.ExitToLobby();
                SessionState.SetInt(KeyPhase, 0);
                MarkAlive();
                return;
            }

            // 1차 탈출: 튜토리얼 스킵 + 제한 해제 + 배속 정상화
            if (tutActive)
            {
                SessionState.SetInt($"AutoPlay_TutStuck_{curStage}",
                    SessionState.GetInt($"AutoPlay_TutStuck_{curStage}", 0) + 1);
                tm.SkipTutorial();
                tm.ForceUnpause();
            }
            if (inp != null)
            {
                inp.SetRestrictedMode(false, null);
                inp.SetEnabled(true);
            }
            if (!VisualConstants.IsGamePausedExternally() && Time.timeScale <= 0f)
                Time.timeScale = 1f;
            lastAliveTime = EditorApplication.timeSinceStartup; // strike는 유지 (연속 감지)
        }

        /// <summary>
        /// 튜토리얼 자동 진행:
        ///   - 다이얼로그 탭 대기(waitingForTap) → 탭 처리 (리플렉션으로 플래그 해제 = 화면 탭과 동등)
        ///   - 입력 제한 모드(강제 회전) → 허용 좌표(allowedCoords) 클러스터를 회전
        ///   - 25초 무진행 → TUTORIAL_STUCK 기록(문제 발견!) 후 SkipTutorial로 탈출해 테스트 계속
        /// </summary>
        private static bool HandleTutorial()
        {
            if (EditorApplication.timeSinceStartup < nextTutorialActTime) return true;
            nextTutorialActTime = EditorApplication.timeSinceStartup + 0.8;

            var tm = TutorialManager.Instance;
            int tutStage = SessionState.GetInt(KeyCurStage, 0);
            if (tutorialLastProgressTime <= 0)
                tutorialLastProgressTime = EditorApplication.timeSinceStartup;

            // 0) ★ 즉시 스킵 모드 — 이 스테이지에서 이미 3회+ 막혔으면 후속 튜토리얼 전부 즉시 제거
            if (SessionState.GetInt($"AutoPlay_TutStuck_{tutStage}", 0) >= 3)
            {
                Log($"  튜토리얼 즉시 스킵 (stage {tutStage} 반복 막힘 누적): {GetCurrentTutorialStepInfo(tm)}");
                tm.SkipTutorial();
                tm.ForceUnpause();
                MarkAlive();
                return true;
            }

            // 0.5) ★ 스텝 전진 감지 — 시퀀스/스텝이 바뀌었으면 그 자체가 '진행' (자유 단계 포함 단일 기준)
            string curStepKey = GetCurrentTutorialStepInfo(tm);
            if (curStepKey != lastTutorialStepKey)
            {
                lastTutorialStepKey = curStepKey;
                tutorialLastProgressTime = EditorApplication.timeSinceStartup;
                sameStepActCount = 0;
                MarkAlive();
            }

            // 1) 다이얼로그 탭 대기 → 탭 (waitingForTap 플래그 해제)
            var tapFld = typeof(TutorialManager).GetField("waitingForTap",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (tapFld != null && (bool)tapFld.GetValue(tm))
            {
                tapFld.SetValue(tm, false);
                tutorialLastProgressTime = EditorApplication.timeSinceStartup;
                MarkAlive();
                return true;
            }

            // 1.5) ★ WaitForEvent 스텝 — 기다리는 이벤트를 "그 스텝에 진입한 뒤에" 충족시킨다.
            //   (ForcedAction 단계에서 미리 행동하면 이벤트가 wait 진입 전에 발사돼 유실되는
            //    레이스가 있었음 — 망치 2회 타격에도 tut_hammer_wait_used가 안 풀리던 원인)
            if (!tm.IsPausedForTutorial)
            {
                string waitEv = GetCurrentWaitEvent(tm);
                if (waitEv != "")
                {
                    // ★ 같은 스텝에서 행동이 6회+ 반복돼도 스텝이 안 넘어가면 — 행동이 무효한 상태
                    //   (로비 잔류 시퀀스의 "라인 버튼 클릭" 무한 반복 패턴) → 대기로 전환, STUCK이 처리
                    if (sameStepActCount >= 6) return true;

                    int acted = TryFulfillWaitEvent(tm, waitEv);
                    if (acted == 1) // 대응 행동 수행함 (입력 점유)
                    {
                        sameStepActCount++;
                        tutorialLastProgressTime = EditorApplication.timeSinceStartup;
                        MarkAlive();
                        return true;
                    }
                    if (acted == 0) // 자유 플레이로 충족해야 하는 이벤트 (매칭/생성 등)
                    {
                        // progressTime 갱신하지 않음 — 스텝 전진(0.5)만이 진행의 증거.
                        // (무조건 갱신하던 버그가 Stage 7에서 7분 무한 멈춤을 만들었음)
                        return false;
                    }
                    // -1: 이번 틱은 대기 (진행 중 등)
                    return true;
                }
            }

            // 2) 입력 제한 모드(강제 액션 단계) → 허용 좌표로 회전 또는 특수블록 클릭
            var inp = Object.FindObjectOfType<InputSystem>();
            var rot = Object.FindObjectOfType<RotationSystem>();
            var brs = Object.FindObjectOfType<BlockRemovalSystem>();
            var grid = Object.FindObjectOfType<HexGrid>();
            if (inp != null && inp.IsRestrictedMode && rot != null && !rot.IsRotating
                && brs != null && !brs.IsProcessing && grid != null && inp.IsEnabled
                && !tm.IsPausedForTutorial)
            {
                var coordsFld = typeof(InputSystem).GetField("allowedCoords",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var coords = coordsFld != null
                    ? coordsFld.GetValue(inp) as System.Collections.Generic.HashSet<HexCoord> : null;

                // ★ 허용 좌표 1~2개 = 클릭 강제 단계 (드릴 클릭 유도 등) → 해당 특수블록 발동
                if (coords != null && coords.Count > 0 && coords.Count < 3)
                {
                    var gmRef = GameManager.Instance;
                    foreach (var c in coords)
                    {
                        var b = grid.GetBlock(c);
                        if (b == null || b.Data == null) continue;
                        if (b.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.None && gmRef != null)
                        {
                            Log($"  튜토리얼 강제 클릭 수행: {b.Data.specialType} at {c}");
                            ActivateSpecialAt(gmRef, b);
                            tutorialLastProgressTime = EditorApplication.timeSinceStartup;
                            MarkAlive();
                            return true;
                        }
                    }
                }

                if (coords != null && coords.Count >= 3)
                {
                    var blocks = new System.Collections.Generic.List<HexBlock>();
                    foreach (var c in coords)
                    {
                        var b = grid.GetBlock(c);
                        if (b != null) blocks.Add(b);
                        if (blocks.Count == 3) break;
                    }
                    if (blocks.Count == 3)
                    {
                        // ★ 동일 클러스터 반복 감지 — 회전해도 튜토리얼이 진행되지 않는 루프 대응
                        string key = $"{blocks[0].Coord}|{blocks[1].Coord}|{blocks[2].Coord}";
                        if (key == lastForcedRotKey) forcedRotRepeat++;
                        else { lastForcedRotKey = key; forcedRotRepeat = 0; }

                        if (forcedRotRepeat == 2)
                        {
                            // 3회째: 역방향(반시계) 1회 시도 — 정방향 매칭 실패 보드 대응
                            Log($"  튜토리얼 강제 회전 반복 감지 → 역방향 시도: ({key})");
                            rot.SetOneTimeCounterClockwise();
                        }
                        else if (forcedRotRepeat >= 4)
                        {
                            // 5회째: 양방향 모두 진행 실패 — 기록 후 ★즉시 스킵 (대기 없이 바로 탈출)
                            Log($"WARN,TUTORIAL_FORCED_ROTATION_LOOP,stage={tutStage}," +
                                $"cluster=({key}) — 강제 회전이 양방향 모두 진행을 만들지 못함 (튜토리얼 설계 점검 필요) → 즉시 스킵");
                            SessionState.SetInt($"AutoPlay_TutStuck_{tutStage}",
                                SessionState.GetInt($"AutoPlay_TutStuck_{tutStage}", 0) + 1);
                            forcedRotRepeat = 0;
                            lastForcedRotKey = "";
                            tm.SkipTutorial();
                            tm.ForceUnpause();
                            MarkAlive();
                            return true;
                        }

                        Log($"  튜토리얼 강제 회전 수행: ({key}){(forcedRotRepeat == 2 ? " [역방향]" : "")}");
                        rot.TryRotate(blocks[0], blocks[1], blocks[2]);
                        tutorialLastProgressTime = EditorApplication.timeSinceStartup;
                        MarkAlive();
                        return true;
                    }
                }
                return true; // 제한 모드인데 처리 불가 → 대기 (STUCK 타이머가 감시)
            }

            // 3) 멈춤 감지 — 스텝 전진이 일정 시간 없으면 튜토리얼 문제로 기록 후 스킵
            //    (첫 막힘 15초, 같은 스테이지 재막힘부터 8초 — 반복 지연 최소화)
            if (EditorApplication.timeSinceStartup - tutorialLastProgressTime > GetTutorialStuckThreshold(tutStage))
            {
                // ★ 멈춘 스텝 정보 (시퀀스 id + 스텝 index/id/type) — 정확한 문제 지점 식별
                string stepInfo = GetCurrentTutorialStepInfo(tm);
                string shot = CaptureScreenshot($"tutorial_stuck_stage{tutStage}");
                Log($"WARN,TUTORIAL_STUCK,stage={tutStage},step={stepInfo},paused={tm.IsPausedForTutorial}," +
                    $"restricted={(inp != null && inp.IsRestrictedMode)},inputEnabled={(inp != null && inp.IsEnabled)}" +
                    $" — 무진행, SkipTutorial로 탈출. 스크린샷={shot}");
                SessionState.SetInt($"AutoPlay_TutStuck_{tutStage}",
                    SessionState.GetInt($"AutoPlay_TutStuck_{tutStage}", 0) + 1);
                tm.SkipTutorial();
                tm.ForceUnpause();
                if (inp != null) inp.SetEnabled(true); // 스킵 후 입력 비활성 잔류 방지 (Stage 8에서 확인)
                tutorialLastProgressTime = EditorApplication.timeSinceStartup;
                MarkAlive();
                return true;
            }

            // 4) 일시정지 대기 단계(탭 아님 — 내부 연출/이벤트 처리 중) → 대기 (STUCK 타이머가 감시)
            if (tm.IsPausedForTutorial) return true;

            // 5) ★ 자유 플레이 대기 단계 — 튜토리얼이 유저의 자유 행동(매칭/특수블록 생성 등)
            //    이벤트를 기다리는 중. 일반 플레이를 계속해 이벤트를 발생시킨다.
            //    progressTime은 갱신하지 않음 — 스텝 전진(0.5)만이 진행의 증거.
            return false;
        }

        /// <summary>현재 스텝이 WaitForEvent면 그 이벤트명, 아니면 "".</summary>
        private static string GetCurrentWaitEvent(TutorialManager tm)
        {
            try
            {
                var seqFld = typeof(TutorialManager).GetField("currentSequence",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var idxFld = typeof(TutorialManager).GetField("currentStepIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var seq = seqFld?.GetValue(tm);
                int idx = idxFld != null ? (int)idxFld.GetValue(tm) : -1;
                if (seq == null) return "";
                var steps = seq.GetType().GetField("steps")?.GetValue(seq) as System.Array;
                if (steps == null || idx < 0 || idx >= steps.Length) return "";
                var step = steps.GetValue(idx);
                var stepType = step.GetType().GetField("type")?.GetValue(step)?.ToString() ?? "";
                if (stepType != "WaitForEvent") return "";
                return step.GetType().GetField("waitEvent")?.GetValue(step)?.ToString() ?? "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// WaitForEvent 스텝의 이벤트를 직접 충족시킨다.
        /// 반환: 1=행동 수행(입력 점유) / 0=자유 플레이로 충족(매칭·생성 등) / -1=이번 틱 대기.
        /// 테스트 지속을 위해 게이지/MP 부족 시 안전망 충전 사용.
        /// </summary>
        private static int TryFulfillWaitEvent(TutorialManager tm, string ev)
        {
            var grid = Object.FindObjectOfType<HexGrid>();
            if (grid == null) return -1;

            switch (ev)
            {
                // ── 망치: 버튼 클릭 → 타격 ──
                case "HammerActivated":
                {
                    var gauge = JewelsHexaPuzzle.Items.HammerGauge.Instance;
                    if (gauge == null) return 0;
                    if (gauge.GaugeLayer < 1) gauge.AddGauge(50);
                    gauge.ActivateUseReady(); // OnHammerActivated 발사
                    Log("  [튜토리얼] 망치 버튼 클릭 수행");
                    return 1;
                }
                case "HammerUsed":
                {
                    var item = Object.FindObjectOfType<JewelsHexaPuzzle.Items.HammerItem>();
                    var gauge = JewelsHexaPuzzle.Items.HammerGauge.Instance;
                    if (item == null || gauge == null) return 0;
                    var procFld = typeof(JewelsHexaPuzzle.Items.HammerItem).GetField("isProcessing",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (procFld != null && (bool)procFld.GetValue(item)) return -1; // 타격 진행 중
                    if (!item.IsActive)
                    {
                        if (gauge.GaugeLayer < 1) gauge.AddGauge(50);
                        gauge.ActivateUseReady();
                        return 1;
                    }
                    HexBlock target = tm.HammerTargetCoord.HasValue ? grid.GetBlock(tm.HammerTargetCoord.Value) : null;
                    if (target == null || target.Data == null || target.Data.gemType == JewelsHexaPuzzle.Data.GemType.None)
                        target = FindAnyNormalBlock(grid);
                    if (target == null) return 0;
                    if (MPManager.Instance != null)
                    {
                        int cost = MPManager.Instance.GetItemCost(ItemType.Hammer);
                        if (MPManager.Instance.CurrentMP < cost) MPManager.Instance.AddMP(cost);
                    }
                    var smash = typeof(JewelsHexaPuzzle.Items.HammerItem).GetMethod("SmashByLevel",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (smash == null) return 0;
                    if (procFld != null) procFld.SetValue(item, true);
                    item.StartCoroutine((System.Collections.IEnumerator)smash.Invoke(item, new object[] { target, 0 }));
                    Log($"  [튜토리얼] 망치 타격 수행: {target.Coord} → OnHammerUsed");
                    return 1;
                }

                // ── 스왑: 버튼 클릭 → 두 블록 교환 ──
                case "SwapActivated":
                {
                    var gauge = JewelsHexaPuzzle.Items.SwapGauge.Instance;
                    if (gauge == null) return 0;
                    if (gauge.GaugeLayer < 1) gauge.AddGauge(50);
                    gauge.ActivateUseReady();
                    Log("  [튜토리얼] 스왑 버튼 클릭 수행");
                    return 1;
                }
                case "SwapUsed":
                {
                    var item = Object.FindObjectOfType<JewelsHexaPuzzle.Items.SwapItem>();
                    var gauge = JewelsHexaPuzzle.Items.SwapGauge.Instance;
                    if (item == null || gauge == null) return 0;
                    var procFld = typeof(JewelsHexaPuzzle.Items.SwapItem).GetField("isProcessing",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (procFld != null && (bool)procFld.GetValue(item)) return -1;
                    var activeFld = typeof(JewelsHexaPuzzle.Items.SwapItem).GetField("isActive",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (activeFld != null && !(bool)activeFld.GetValue(item))
                    {
                        if (gauge.GaugeLayer < 1) gauge.AddGauge(50);
                        gauge.ActivateUseReady();
                        return 1;
                    }
                    // 타겟: 튜토리얼 지정 좌표 우선, 없으면 인접 일반 블록 2개
                    HexBlock a = null, b = null;
                    if (tm.SwapSourceCoord.HasValue && tm.SwapDestCoord.HasValue)
                    {
                        a = grid.GetBlock(tm.SwapSourceCoord.Value);
                        b = grid.GetBlock(tm.SwapDestCoord.Value);
                    }
                    if (a == null || b == null)
                    {
                        a = FindAnyNormalBlock(grid);
                        if (a != null)
                            foreach (var n in grid.GetNeighbors(a.Coord))
                            {
                                if (n != null && n.Data != null &&
                                    n.Data.gemType != JewelsHexaPuzzle.Data.GemType.None && !n.Data.isShell)
                                { b = n; break; }
                            }
                    }
                    if (a == null || b == null) return 0;
                    if (MPManager.Instance != null)
                    {
                        int cost = MPManager.Instance.GetItemCost(ItemType.Bomb); // 스왑 비용 키
                        if (MPManager.Instance.CurrentMP < cost) MPManager.Instance.AddMP(cost);
                    }
                    var exec = typeof(JewelsHexaPuzzle.Items.SwapItem).GetMethod("ExecuteSwap",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (exec == null) return 0;
                    if (procFld != null) procFld.SetValue(item, true);
                    item.StartCoroutine((System.Collections.IEnumerator)exec.Invoke(item, new object[] { a, b }));
                    Log($"  [튜토리얼] 스왑 수행: {a.Coord} ↔ {b.Coord} → OnSwapUsed");
                    return 1;
                }

                // ── 라인 그리기: 아이템 직접 활성화 (게이지 상태 게이트 우회), 사용은 자유 플레이 ──
                case "LineDrawActivated":
                {
                    var li = Object.FindObjectOfType<JewelsHexaPuzzle.Items.LineDrawItem>();
                    if (li == null) return 0;
                    var liActive = typeof(JewelsHexaPuzzle.Items.LineDrawItem).GetField("isActive",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (liActive != null && (bool)liActive.GetValue(li)) return -1; // 이미 활성 — 대기
                    var gauge = JewelsHexaPuzzle.Items.LineGauge.Instance;
                    if (gauge != null && gauge.GaugeLayer < 1) gauge.AddGauge(50);
                    // LineGauge.ActivateUseReady는 Ready 상태에서만 동작(게이트) → 아이템 직접 활성화.
                    // Activate 내부에서 OnLineDrawActivated 발사됨 (LineDrawItem 157).
                    li.Activate();
                    Log("  [튜토리얼] 라인 아이템 직접 활성화");
                    return 1;
                }

                // ── 역회전 ──
                case "ReverseActivated":
                {
                    var rev = Object.FindObjectOfType<JewelsHexaPuzzle.Items.ReverseRotationItem>();
                    if (rev == null) return 0;
                    if (ItemManager.Instance != null &&
                        ItemManager.Instance.GetGaugeCount(ItemType.ReverseRotation) < 1)
                        return 0; // 스택 없음 → 자유 플레이로 채우기
                    rev.Activate();
                    Log("  [튜토리얼] 역회전 버튼 클릭 수행");
                    return 1;
                }

                // ── 특수블록 발동 대기: 보드의 해당 블록을 클릭 ──
                case "DrillActivated":   return ActivateSpecialForTutorial(grid, JewelsHexaPuzzle.Data.SpecialBlockType.Drill);
                case "BombActivated":    return ActivateSpecialForTutorial(grid, JewelsHexaPuzzle.Data.SpecialBlockType.Bomb);
                case "DroneActivated":   return ActivateSpecialForTutorial(grid, JewelsHexaPuzzle.Data.SpecialBlockType.Drone);
                case "RainbowActivated": return ActivateSpecialForTutorial(grid, JewelsHexaPuzzle.Data.SpecialBlockType.Rainbow);
                case "XBlockActivated":  return ActivateSpecialForTutorial(grid, JewelsHexaPuzzle.Data.SpecialBlockType.XBlock);

                // ── 아무 입력 대기: 이벤트 플래그 직접 충족 (화면 탭과 동등) ──
                case "AnyInput":
                {
                    var weFld = typeof(TutorialManager).GetField("waitingForEvent",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var peFld = typeof(TutorialManager).GetField("pendingWaitEvent",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (weFld != null) weFld.SetValue(tm, false);
                    if (peFld != null && peFld.FieldType.IsEnum)
                        peFld.SetValue(tm, System.Enum.ToObject(peFld.FieldType, 0)); // None
                    Log("  [튜토리얼] AnyInput 충족 (탭 수행)");
                    return 1;
                }

                // ── 자유 플레이로 충족되는 이벤트 (매칭/생성/캐스케이드/회전 등) ──
                default:
                    return 0;
            }
        }

        /// <summary>보드에서 지정 타입 특수블록을 찾아 발동 (튜토리얼용 — 없으면 자유 플레이 0).</summary>
        private static int ActivateSpecialForTutorial(HexGrid grid, JewelsHexaPuzzle.Data.SpecialBlockType type)
        {
            var gm = GameManager.Instance;
            if (gm == null) return 0;
            foreach (var b in grid.GetAllBlocks())
            {
                if (b == null || b.Data == null || b.Data.specialType != type) continue;
                if (b.Data.pendingActivation) continue;
                if (MPManager.Instance != null)
                {
                    int cost = MPManager.Instance.GetSpecialBlockCost(type);
                    if (MPManager.Instance.CurrentMP < cost) MPManager.Instance.AddMP(cost); // 안전망
                }
                Log($"  [튜토리얼] 특수블록 발동: {type} at {b.Coord}");
                ActivateSpecialAt(gm, b);
                return 1;
            }
            return 0; // 보드에 없음 — 자유 플레이로 생성부터
        }

        /// <summary>아무 일반 블록 1개 (타격/스왑 폴백 타겟).</summary>
        private static HexBlock FindAnyNormalBlock(HexGrid grid)
        {
            foreach (var b in grid.GetAllBlocks())
            {
                if (b == null || b.Data == null) continue;
                if (b.Data.gemType == JewelsHexaPuzzle.Data.GemType.None || b.Data.isShell) continue;
                if (b.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.None) continue;
                return b;
            }
            return null;
        }

        /// <summary>현재 튜토리얼 시퀀스/스텝 정보 문자열 (리플렉션 — 문제 지점 식별용).</summary>
        private static string GetCurrentTutorialStepInfo(TutorialManager tm)
        {
            try
            {
                var seqFld = typeof(TutorialManager).GetField("currentSequence",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var idxFld = typeof(TutorialManager).GetField("currentStepIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var seq = seqFld?.GetValue(tm);
                int idx = idxFld != null ? (int)idxFld.GetValue(tm) : -1;
                if (seq == null) return "seq=null";
                var seqId = seq.GetType().GetField("id")?.GetValue(seq) ?? "?";
                var steps = seq.GetType().GetField("steps")?.GetValue(seq) as System.Array;
                if (steps == null || idx < 0 || idx >= steps.Length) return $"{seqId}[{idx}/?]";
                var step = steps.GetValue(idx);
                var stepId = step.GetType().GetField("id")?.GetValue(step) ?? "?";
                var stepType = step.GetType().GetField("type")?.GetValue(step) ?? "?";
                var waitEv = step.GetType().GetField("waitEvent")?.GetValue(step) ?? "";
                return $"{seqId}[{idx}]:{stepId}({stepType}{(waitEv.ToString() != "None" && waitEv.ToString() != "" ? "/" + waitEv : "")})";
            }
            catch (System.Exception e) { return "reflect-err:" + e.Message; }
        }

        /// <summary>외부(검증 시스템)에서 스크린샷을 찍기 위한 공개 래퍼.</summary>
        public static string CaptureScreenshotPublic(string tag) => CaptureScreenshot(tag);

        /// <summary>게임 화면 캡처 — .claude/autoplay/ 에 PNG 저장, 경로 반환.</summary>
        private static string CaptureScreenshot(string tag)
        {
            try
            {
                string file = Path.Combine(ResultsDir, $"{tag}_{System.DateTime.Now:HHmmss}.png");
                Directory.CreateDirectory(ResultsDir);
                ScreenCapture.CaptureScreenshot(file);
                return file;
            }
            catch { return "(캡처 실패)"; }
        }

        /// <summary>지정 특수블록 발동 (턴 차감 + MP 소모 + 시스템 호출 — InputSystem 클릭 경로 모방).</summary>
        private static void ActivateSpecialAt(GameManager gm, HexBlock block)
        {
            var st = block.Data.specialType;
            int cost = MPManager.Instance != null ? MPManager.Instance.GetSpecialBlockCost(st) : 0;
            if (MPManager.Instance != null && MPManager.Instance.CurrentMP < cost) return;
            gm.UseOneTurn();
            if (MPManager.Instance != null)
                MPManager.Instance.TryConsumeMP(cost, block.transform.position);
            switch (st)
            {
                case JewelsHexaPuzzle.Data.SpecialBlockType.Drill:
                    var ds = Object.FindObjectOfType<DrillBlockSystem>(); if (ds != null) ds.ActivateDrill(block); break;
                case JewelsHexaPuzzle.Data.SpecialBlockType.Bomb:
                    var bs = Object.FindObjectOfType<BombBlockSystem>(); if (bs != null) bs.ActivateBomb(block); break;
                case JewelsHexaPuzzle.Data.SpecialBlockType.Rainbow:
                    var dos = Object.FindObjectOfType<DonutBlockSystem>(); if (dos != null) dos.ActivateDonut(block); break;
                case JewelsHexaPuzzle.Data.SpecialBlockType.XBlock:
                    var xs = Object.FindObjectOfType<XBlockSystem>(); if (xs != null) xs.ActivateXBlock(block); break;
                case JewelsHexaPuzzle.Data.SpecialBlockType.Drone:
                    var drs = Object.FindObjectOfType<DroneBlockSystem>(); if (drs != null) drs.ActivateDrone(block); break;
            }
        }

        /// <summary>
        /// 망치 아이템 사용 — 실제 유저 흐름 모방 (HammerItem.HandleDragEnd의 클릭 경로 복제):
        ///   조건: 망치 해금 + 게이지 1층 이상 + MP가 망치비용+특수블록 여유(6) 이상
        ///   타겟: 고블린 위치 블록 우선(직격 데미지 1 포함), 없으면 미션 타겟 색 블록
        ///   실행: SmashByLevel(block, 0) 리플렉션 — MP/게이지 소모는 코루틴 내부에서 처리됨
        /// </summary>
        /// <summary>고블린이 처치 미션 타겟 타입 집합에 속하는지 (Lv2는 기본형과 동일 패밀리 취급).</summary>
        private static bool IsGoblinOfType(GoblinData g, System.Collections.Generic.HashSet<JewelsHexaPuzzle.Data.EnemyType> targets)
        {
            foreach (var t in targets)
            {
                switch (t)
                {
                    case JewelsHexaPuzzle.Data.EnemyType.ArmoredGoblin:
                    case JewelsHexaPuzzle.Data.EnemyType.ArmoredGoblinLv2:
                        if (g.isArmored) return true;
                        break;
                    case JewelsHexaPuzzle.Data.EnemyType.ArcherGoblin:
                    case JewelsHexaPuzzle.Data.EnemyType.ArcherGoblinLv2:
                        if (g.isArcher) return true;
                        break;
                    case JewelsHexaPuzzle.Data.EnemyType.ShieldGoblin:
                    case JewelsHexaPuzzle.Data.EnemyType.ShieldGoblinLv2:
                        if (g.isShieldType) return true;
                        break;
                    case JewelsHexaPuzzle.Data.EnemyType.BombGoblin: if (g.isBomb) return true; break;
                    case JewelsHexaPuzzle.Data.EnemyType.HealerGoblin: if (g.isHealer) return true; break;
                    case JewelsHexaPuzzle.Data.EnemyType.HeavyGoblin: if (g.isHeavy) return true; break;
                    case JewelsHexaPuzzle.Data.EnemyType.WizardGoblin: if (g.isWizard) return true; break;
                    case JewelsHexaPuzzle.Data.EnemyType.ThiefGoblin: if (g.isThief) return true; break;
                    case JewelsHexaPuzzle.Data.EnemyType.WitchGoblin: if (g.isWitch) return true; break;
                    case JewelsHexaPuzzle.Data.EnemyType.Goblin:
                    case JewelsHexaPuzzle.Data.EnemyType.GoblinLv2:
                        if (!g.isArmored && !g.isArcher && !g.isShieldType && !g.isBomb && !g.isHealer
                            && !g.isHeavy && !g.isWizard && !g.isThief && !g.isWitch) return true;
                        break;
                }
            }
            return false;
        }

        private static bool TryUseHammer(HexGrid grid)
        {
            var gauge = JewelsHexaPuzzle.Items.HammerGauge.Instance;
            if (gauge == null || gauge.GaugeLayer < 1) return false;
            if (TutorialManager.Instance == null ||
                !TutorialManager.Instance.IsFeatureUnlocked(TutorialManager.FEATURE_ITEM_HAMMER)) return false;
            if (MPManager.Instance == null) return false;
            int cost = MPManager.Instance.GetItemCost(ItemType.Hammer);
            if (MPManager.Instance.CurrentMP < cost) return false; // ★ 적극 사용 — MP 닿는 대로

            var item = Object.FindObjectOfType<JewelsHexaPuzzle.Items.HammerItem>();
            if (item == null) return false;
            // 이미 처리 중이면 스킵
            var procFld = typeof(JewelsHexaPuzzle.Items.HammerItem).GetField("isProcessing",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (procFld != null && (bool)procFld.GetValue(item)) return false;

            // 타겟 선정: ★미션 타겟 적(처치 미션의 targetEnemyType — 갑옷 등 고체력형 포함) 최우선
            //   → 아무 고블린 → 미션 타겟 색 블록
            HexBlock target = null;
            bool targetHasGoblin = false;
            if (GoblinSystem.Instance != null && GoblinSystem.Instance.IsActive)
            {
                // 미수행 처치 미션의 타겟 적 타입 수집
                var enemyTargets = new System.Collections.Generic.HashSet<JewelsHexaPuzzle.Data.EnemyType>();
                var sm = Object.FindObjectOfType<StageManager>();
                if (sm != null)
                {
                    var missions = sm.GetIncompleteMissions();
                    if (missions != null)
                        foreach (var m in missions)
                            if (m != null && m.type == MissionType.RemoveEnemy)
                                enemyTargets.Add(m.targetEnemyType);
                }

                HexBlock anyGoblinBlock = null;
                foreach (var b in grid.GetAllBlocks())
                {
                    if (b == null || b.Data == null || b.Data.gemType == JewelsHexaPuzzle.Data.GemType.None) continue;
                    var g = GoblinSystem.Instance.GetGoblinAt(b.Coord);
                    if (g == null) continue;
                    if (anyGoblinBlock == null) anyGoblinBlock = b;
                    // 미션 타겟 적이면 즉시 확정 (갑옷=isArmored, 기본 고블린 등 타입 매칭)
                    if (enemyTargets.Count > 0 && IsGoblinOfType(g, enemyTargets))
                    {
                        target = b;
                        targetHasGoblin = true;
                        break;
                    }
                }
                if (target == null && anyGoblinBlock != null)
                {
                    target = anyGoblinBlock;
                    targetHasGoblin = true;
                }
            }
            if (target == null)
            {
                foreach (var b in grid.GetAllBlocks())
                {
                    if (b == null || b.Data == null) continue;
                    if (b.Data.gemType == JewelsHexaPuzzle.Data.GemType.None || b.Data.isShell) continue;
                    if (b.Data.specialType != JewelsHexaPuzzle.Data.SpecialBlockType.None) continue;
                    if (JewelsHexaPuzzle.Data.MissionTargetColors.Contains(b.Data.gemType)) { target = b; break; }
                }
            }
            if (target == null) return false;

            // 고블린 직격 (HandleDragEnd 흐름과 동일 — SmashByLevel엔 몬스터 타격이 없음)
            if (targetHasGoblin)
                GoblinSystem.Instance.ApplyDamageAtPosition(target.Coord, 1);

            // SmashByLevel(block, 0) — 중심 1블록 파괴, MP/게이지 소모 내장
            var smash = typeof(JewelsHexaPuzzle.Items.HammerItem).GetMethod("SmashByLevel",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (smash == null) return false;
            if (procFld != null) procFld.SetValue(item, true); // 호출자 책임 플래그 (원 코드 주석 준수)
            // ★ 레이스 수정(2026-07-03): 실유저 경로처럼 망치 애니 동안 입력 차단 —
            //   차단 없이는 봇이 망치 낙하 중 회전을 시작해 "BRS still processing! Force reset" 충돌 유발.
            //   SmashByLevel 종료부가 Playing 상태면 재활성한다 (MP는 위에서 선검사 완료).
            var inpSys = UnityEngine.Object.FindObjectOfType<JewelsHexaPuzzle.Core.InputSystem>();
            if (inpSys != null) inpSys.SetEnabled(false);
            item.StartCoroutine((System.Collections.IEnumerator)smash.Invoke(item, new object[] { target, 0 }));
            Log($"  망치 사용: {target.Coord}{(targetHasGoblin ? " (고블린 직격)" : " (미션 색)")} — 게이지 {gauge.GaugeLayer}층, MP {MPManager.Instance.CurrentMP}");
            return true;
        }

        /// <summary>보드의 특수블록 1개 발동 (InputSystem의 발동 경로 모방: 턴 차감 + MP 소모 + Activate).</summary>
        private static bool TryActivateSpecial(GameManager gm, HexGrid grid)
        {
            if (MPManager.Instance == null) return false;
            foreach (var block in grid.GetAllBlocks())
            {
                if (block == null || block.Data == null) continue;
                var st = block.Data.specialType;
                if (st != JewelsHexaPuzzle.Data.SpecialBlockType.Drill &&
                    st != JewelsHexaPuzzle.Data.SpecialBlockType.Bomb &&
                    st != JewelsHexaPuzzle.Data.SpecialBlockType.Rainbow &&
                    st != JewelsHexaPuzzle.Data.SpecialBlockType.XBlock &&
                    st != JewelsHexaPuzzle.Data.SpecialBlockType.Drone) continue;
                if (block.Data.pendingActivation) continue;

                int cost = MPManager.Instance.GetSpecialBlockCost(st);
                if (MPManager.Instance.CurrentMP < cost) continue;

                // 해금 체크 (InputSystem과 동일)
                if (TutorialManager.Instance == null || !TutorialManager.Instance.IsSpecialBlockUnlocked(st)) continue;

                gm.UseOneTurn();
                MPManager.Instance.TryConsumeMP(cost, block.transform.position);

                switch (st)
                {
                    case JewelsHexaPuzzle.Data.SpecialBlockType.Drill:
                        var ds = Object.FindObjectOfType<DrillBlockSystem>(); if (ds != null) ds.ActivateDrill(block); break;
                    case JewelsHexaPuzzle.Data.SpecialBlockType.Bomb:
                        var bs = Object.FindObjectOfType<BombBlockSystem>(); if (bs != null) bs.ActivateBomb(block); break;
                    case JewelsHexaPuzzle.Data.SpecialBlockType.Rainbow:
                        var dos = Object.FindObjectOfType<DonutBlockSystem>(); if (dos != null) dos.ActivateDonut(block); break;
                    case JewelsHexaPuzzle.Data.SpecialBlockType.XBlock:
                        var xs = Object.FindObjectOfType<XBlockSystem>(); if (xs != null) xs.ActivateXBlock(block); break;
                    case JewelsHexaPuzzle.Data.SpecialBlockType.Drone:
                        var drs = Object.FindObjectOfType<DroneBlockSystem>(); if (drs != null) drs.ActivateDrone(block); break;
                }
                Log($"  특수블록 발동: {st} at {block.Coord} (MP -{cost})");
                return true;
            }
            return false;
        }

        private static void RecordResult(string result, GameManager gm)
        {
            int curStage = SessionState.GetInt(KeyCurStage, 0);
            int startTurns = SessionState.GetInt(KeyStartTurns, -1);
            float elapsed = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyStageStartTime, 0f);
            string missions = "";
            int done = 0, total = 0;
            var sm = gm.StageManagerRef;
            if (sm != null)
            {
                var prog = sm.GetMissionProgress();
                foreach (var p in prog) if (p.isComplete) done++;
                total = prog.Length;
                missions = $"{done}/{prog.Length}활성+대기{sm.PendingMissionCount}";
            }
            // ★ 학습 잔여이동 = 미션 완료 순간의 남은 이동(드릴 변환 소진 전). 팝업 시점 CurrentTurns는 0이라 무의미.
            bool cleared = result.StartsWith("CLEAR");
            int leftForFitness = cleared ? gm.TurnsAtStageClear : gm.CurrentTurns;
            int goldSpent = gm.GoldSpentThisStage;      // ★ 골드 소비 → 마이너스 점수
            int continues = gm.ContinueCountThisStage;  // ★ 이어하기 사용 → 실패 데이터
            var scoreMgr = Object.FindObjectOfType<ScoreManager>();
            int gameScore = scoreMgr != null ? scoreMgr.CurrentScore : 0; // ★ 게임 점수 (성공 시 데이터 저장)
            Log($"RESULT,stage={curStage},{result},startTurns={startTurns},leftTurns={gm.CurrentTurns},leftAtClear={gm.TurnsAtStageClear},goldSpent={goldSpent},continues={continues},score={gameScore},elapsed={elapsed:F0}s,missions={missions}");

            // 학습 모드: fitness 표본 기록 (클리어=CLEAR_BONUS+잔여이동*계수 − 골드소비)
            if (SessionState.GetBool("AutoPlay_Learn", false))
                LearningAutoPlay.OnRunComplete(curStage, cleared, startTurns, leftForFitness, done, total, goldSpent, continues, gameScore);
        }

        private static void Stop()
        {
            SessionState.SetBool(KeyActive, false);
            SessionState.SetInt(KeyPhase, 0);
            if (EditorApplication.isPlaying) Time.timeScale = 1f; // 3배속 복원
        }

        /// <summary>현재 해금된 최고 레벨 (상시 가동 모드의 재개 지점).</summary>
        private static int GetHighestUnlockedLevel()
        {
            try
            {
                var unlocked = JewelsHexaPuzzle.Data.LevelRegistry.GetUnlockedLevels();
                if (unlocked == null || unlocked.Count == 0) return 1;
                return unlocked[unlocked.Count - 1].levelId; // levelId 오름차순 정렬 보장
            }
            catch { return 1; }
        }

        private static void Log(string msg)
        {
            Debug.Log($"[AutoPlayTester] {msg}");
            try
            {
                Directory.CreateDirectory(ResultsDir);
                File.AppendAllText(ResultsPath, $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }
    }
}

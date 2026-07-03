// ============================================================================
// RuntimeInvariantVerifier — 런타임 버그 검증 루프 시스템 (Editor 전용)
// ============================================================================
// 정적 LevelMissionVerifier(데이터층)와 별개로, "재생 중" 게임 상태의 불변식을
// 매 폴 감시하고 위반을 VERIFY_FAIL 로그 + 리포트로 남긴다.
//
// 트리거: ".claude/verify_start" (내용 "시작 끝 [회수]", 예 "1 150 3")
//   → Verify_Active 설정 + 봇 주행(autoplay_start) 위임 + 리포트 초기화.
//   ".claude/verify_stop" → 중지.
// 항상 감시: verify_start 없이도 재생 중이면 불변식을 감시(수동 플레이/일반 봇 주행 포함).
//
// 상호배제: 학습 모드(AutoPlay_Learn) 중이면 감시 스킵 — 학습이 W를 극단 변이시켜
//   "비정상 플레이"를 유도하므로 버그와 구분 불가(비평 #10).
//
// 오탐 방지: Loading/Paused/전환·리로드 직후 유예, 스톨/최종미션은 디바운스,
//   상태전이·캐스케이드는 폴링이 아닌 로그로 판정(비평 #7/#8).
// ============================================================================
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using JewelsHexaPuzzle.Core;
using JewelsHexaPuzzle.Managers;

namespace JewelsHexaPuzzle.EditorTools
{
    [InitializeOnLoad]
    public static class RuntimeInvariantVerifier
    {
        private static string BaseDir => Path.Combine(
            Path.GetDirectoryName(Application.dataPath), ".claude", "autoplay", "verify");
        private static string RawLog => Path.Combine(BaseDir, "verify_raw.log");
        private static string Report => Path.Combine(BaseDir, "verify_report.txt");
        private static string StartTrigger => Path.Combine(
            Path.GetDirectoryName(Application.dataPath), ".claude", "verify_start");
        private static string StopTrigger => Path.Combine(
            Path.GetDirectoryName(Application.dataPath), ".claude", "verify_stop");

        private const double POLL = 0.25;
        private const double RELOAD_GRACE = 1.5;
        private const double PROCESSING_TIMEOUT = 90.0;
        private const double STALL_TIMEOUT = 90.0;       // 봇 워치독(40s)보다 크게 — 이중경보 방지
        private const double FINAL_MISSION_GRACE = 5.0;
        private const double SCREENSHOT_COOLDOWN = 8.0;

        private static double nextPoll, lastReloadTime, procEnterTime = -1, stallStart = -1;
        private static int lastTurns = -999;
        private static GameState lastState = GameState.Loading;
        private static double lastScreenshotTime = -100;

        // 카테고리별 누적 위반수 + 최초 스테이지 + 마지막 detail (리포트용)
        private static readonly Dictionary<string, int> failCounts = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> failFirstStage = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> failLastDetail = new Dictionary<string, string>();
        private static readonly Dictionary<string, double> failLastTime = new Dictionary<string, double>();
        private static readonly Dictionary<string, double> pending = new Dictionary<string, double>(); // 디바운스

        static RuntimeInvariantVerifier()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceivedThreaded += OnLog;
            AssemblyReloadEvents.afterAssemblyReload += () => lastReloadTime = EditorApplication.timeSinceStartup;
            Debug.Log("[Verify] RuntimeInvariantVerifier loaded");
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + POLL;

            HandleTriggers();

            if (!EditorApplication.isPlaying) return;
            if (SessionState.GetBool("AutoPlay_Learn", false)) return;             // 상호배제
            if (EditorApplication.timeSinceStartup - lastReloadTime < RELOAD_GRACE) return; // 리로드 유예

            var gm = GameManager.Instance;
            if (gm == null) return;
            DrainLogHits(gm);                 // 로그 훅에서 쌓인 예외/마커를 메인 폴에서 확정
            var state = gm.CurrentState;

            // Loading/Paused/전환은 상태판정 스킵 (오탐 방지)
            if (state == GameState.Loading)
            {
                // SM-03: Loading에서만 timeScale 검사
                if (Mathf.Abs(Time.timeScale - 1f) > 0.01f && !SessionState.GetBool("AutoPlay_Active", false))
                    Confirm("TIMESCALE_LOADING", $"ts={Time.timeScale:F2}", gm);
                procEnterTime = -1; stallStart = -1; lastState = state; return;
            }
            if (state == GameState.Paused) { procEnterTime = -1; stallStart = -1; lastState = state; return; }

            double now = EditorApplication.timeSinceStartup;
            int curStage = SessionState.GetInt("AutoPlay_CurStage", 0);

            // SM-02: Processing 90초 하드캡
            if (state == GameState.Processing)
            {
                if (procEnterTime < 0) procEnterTime = now;
                else if (now - procEnterTime > PROCESSING_TIMEOUT)
                    Confirm("PROCESSING_TIMEOUT", $"{now - procEnterTime:F0}s", gm);
            }
            else procEnterTime = -1;

            // CS-01: 캐스케이드 상한
            var brs = UnityEngine.Object.FindObjectOfType<BlockRemovalSystem>();
            if (brs != null && brs.CurrentCascadeDepth > 20)
                Confirm("CASCADE_OVERFLOW", $"depth={brs.CurrentCascadeDepth}", gm);

            // MP-01: MP 클램프
            var mp = MPManager.Instance;
            if (mp != null && (mp.CurrentMP < 0 || mp.CurrentMP > mp.MaxMP))
                Confirm("MP_CLAMP", $"mp={mp.CurrentMP}/{mp.MaxMP}", gm);

            // GB-03: 고블린 필드 밖 갇힘
            var gob = GoblinSystem.Instance;
            var grid = UnityEngine.Object.FindObjectOfType<HexGrid>();
            if (gob != null && grid != null)
            {
                var alive = gob.GetAliveGoblins();
                if (alive != null)
                    foreach (var g in alive)
                        if (g != null && !grid.IsInGameField(g.position))
                        { Confirm("GOBLIN_OUT_OF_BOUNDS", $"coord={g.position}", gm); break; }
            }

            // MR-06: 활성 미션 한도 초과
            var sm = gm.StageManagerRef;
            if (sm != null)
            {
                var prog = sm.GetMissionProgress();
                if (prog != null && prog.Length > sm.CurrentActiveLimit)
                    Confirm("MISSION_OVERFLOW", $"{prog.Length}>{sm.CurrentActiveLimit}", gm);

                // MR-02: 최종미션 완료(활성 all + 대기 0)인데 StageClear 미전이 (5초 디바운스)
                // ★ 오탐 수정(2026-07-03): 로비/게임오버 등에선 직전 스테이지 잔존 데이터로 무한 오발 (n=142 실측)
                //   → 인게임(Playing/Processing)에서만 검사.
                bool inGame = state == GameState.Playing || state == GameState.Processing;
                bool allDone = inGame && prog != null && prog.Length > 0;
                if (allDone) foreach (var p in prog) if (!p.isComplete) { allDone = false; break; }
                bool stuck = allDone && sm.PendingMissionCount == 0 && state != GameState.StageClear;
                Debounce("FINAL_MISSION_STUCK", stuck, now, FINAL_MISSION_GRACE,
                         $"활성전부완료+대기0인데 {state}", gm);
            }

            // A8: 전역 스톨 (Playing + 입력활성인데 90초+ 무진행)
            // ★ 오탐 수정(2026-07-03): 봇이 정지된 뒤엔 게임이 유휴로 남는 게 정상(사람 AFK와 동일)
            //   → 봇이 실제 주행 중일 때만 무진행을 결함으로 판정.
            var inp = UnityEngine.Object.FindObjectOfType<InputSystem>();
            bool inputOn = inp != null && inp.IsEnabled;
            bool botDriving = SessionState.GetBool("AutoPlay_Active", false);
            if (state == GameState.Playing && inputOn && botDriving)
            {
                if (gm.CurrentTurns == lastTurns && state == lastState)
                {
                    if (stallStart < 0) stallStart = now;
                    else if (now - stallStart > STALL_TIMEOUT)
                    { Confirm("RUNTIME_STALL", $"{now - stallStart:F0}s 무진행", gm); stallStart = now; }
                }
                else stallStart = now;
            }
            else stallStart = -1;

            lastTurns = gm.CurrentTurns;
            lastState = state;

            // 봇 주행이 끝났고(검증 세션 위임) 활성 아니면 리포트 마감
            if (SessionState.GetBool("Verify_Active", false) && !SessionState.GetBool("AutoPlay_Active", false)
                && now - SessionState.GetFloat("Verify_StartTime", 0f) > 8f)
            {
                WriteReport("봇 주행 종료");
                SessionState.SetBool("Verify_Active", false);
                Debug.Log("[Verify] === 검증 세션 종료 — 리포트 기록 ===");
            }
        }

        // ── 트리거 처리 ──
        private static void HandleTriggers()
        {
            if (File.Exists(StopTrigger))
            {
                try { File.Delete(StopTrigger); } catch { }
                if (SessionState.GetBool("Verify_Active", false)) WriteReport("수동 중지");
                SessionState.SetBool("Verify_Active", false);
                Debug.Log("[Verify] 검증 중지");
            }
            if (!File.Exists(StartTrigger)) return;
            string content;
            try { content = File.ReadAllText(StartTrigger).Trim(); File.Delete(StartTrigger); }
            catch { return; }

            if (SessionState.GetBool("AutoPlay_Learn", false))
            {
                Debug.LogWarning("[Verify] 학습 모드 중 — 검증 시작 거부(상호배제)");
                return;
            }
            var parts = content.Split(' ');
            int s = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 1;
            int e = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : s;
            // 카운터 리셋
            failCounts.Clear(); failFirstStage.Clear(); failLastDetail.Clear(); failLastTime.Clear(); pending.Clear();
            SessionState.SetBool("Verify_Active", true);
            SessionState.SetFloat("Verify_StartTime", (float)EditorApplication.timeSinceStartup);
            Directory.CreateDirectory(BaseDir);
            // 봇 주행 위임 (봇이 이미 활성이면 피기백만)
            if (!SessionState.GetBool("AutoPlay_Active", false))
            {
                try { File.WriteAllText(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), ".claude", "autoplay_start"), $"{s} {e}"); }
                catch { }
            }
            Debug.Log($"[Verify] === 검증 세션 시작: stage {s}~{e} (봇 주행 감시) ===");
        }

        // ── 로그 훅: 예외/특정 결함 마커 (스레드 세이프하게 카운트만, 파일쓰기는 메인 폴에서) ──
        private static readonly object logLock = new object();
        private static readonly List<string> logHits = new List<string>();
        private static void OnLog(string condition, string stack, LogType type)
        {
            if (SessionState.GetBool("AutoPlay_Learn", false)) return;
            string cat = null;
            if (type == LogType.Exception ||
                condition.Contains("NullReferenceException") || condition.Contains("IndexOutOfRangeException") ||
                condition.Contains("KeyNotFoundException")) cat = "EXCEPTION";
            else if (condition.Contains("Leftover pending block")) cat = "PENDING_LEFTOVER";
            else if (condition.Contains("hit max iterations")) cat = "CASCADE_MAXITER";
            else if (condition.Contains("STUCK DETECTED")) cat = "STUCK_DETECTED";
            else if (condition.Contains("성공=False")) cat = "RESHUFFLE_FAIL";
            if (cat == null) return;
            lock (logLock) logHits.Add($"{cat}\t{condition.Replace('\n', ' ').Replace('\t', ' ')}");
        }

        private static void DrainLogHits(GameManager gm)
        {
            List<string> hits = null;
            lock (logLock) { if (logHits.Count > 0) { hits = new List<string>(logHits); logHits.Clear(); } }
            if (hits == null) return;
            foreach (var h in hits)
            {
                int t = h.IndexOf('\t');
                string cat = t > 0 ? h.Substring(0, t) : h;
                string detail = t > 0 ? h.Substring(t + 1) : "";
                Confirm(cat, detail.Length > 120 ? detail.Substring(0, 120) : detail, gm);
            }
        }

        // ── 디바운스: 조건이 grace초 지속될 때만 확정 ──
        private static void Debounce(string cat, bool condition, double now, double grace, string detail, GameManager gm)
        {
            if (!condition) { pending.Remove(cat); return; }
            if (!pending.ContainsKey(cat)) pending[cat] = now;
            else if (now - pending[cat] >= grace) { Confirm(cat, detail, gm); pending.Remove(cat); }
        }

        // ── 위반 확정 (카테고리별 쿨다운으로 중복 억제) ──
        private static void Confirm(string cat, string detail, GameManager gm)
        {
            double now = EditorApplication.timeSinceStartup;
            int curStage = SessionState.GetInt("AutoPlay_CurStage", 0);
            // 같은 카테고리 재확정은 5초 쿨다운(폴 스팸 억제) — 단 카운트는 유지
            bool cooled = !failLastTime.ContainsKey(cat) || now - failLastTime[cat] > 5.0;

            failCounts[cat] = failCounts.TryGetValue(cat, out var c) ? c + 1 : 1;
            if (!failFirstStage.ContainsKey(cat)) failFirstStage[cat] = curStage;
            failLastDetail[cat] = detail;
            if (!cooled) return;
            failLastTime[cat] = now;

            string ts = gm != null ? $",state={gm.CurrentState},ts={Time.timeScale:F2}" : "";
            string line = $"VERIFY_FAIL,category={cat},stage={curStage},detail={detail}{ts},n={failCounts[cat]}";
            Debug.LogError(line);
            try { Directory.CreateDirectory(BaseDir); File.AppendAllText(RawLog, $"[{DateTime.Now:HH:mm:ss}] {line}\n"); }
            catch { }
            // 스크린샷(쿨다운)
            if (now - lastScreenshotTime > SCREENSHOT_COOLDOWN)
            {
                lastScreenshotTime = now;
                try { AutoPlayTester.CaptureScreenshotPublic($"verify_{cat}_stage{curStage}"); } catch { }
            }
        }

        private static void WriteReport(string reason)
        {
            try
            {
                Directory.CreateDirectory(BaseDir);
                var sb = new StringBuilder();
                sb.AppendLine($"=== 런타임 검증 리포트 ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) — {reason} ===");
                if (failCounts.Count == 0)
                    sb.AppendLine("결과: 위반 0건 — 모든 감시 불변식 PASS ✅");
                else
                {
                    sb.AppendLine($"결과: {failCounts.Count}개 범주 위반 감지");
                    sb.AppendLine("범주 | 위반수 | 최초스테이지 | 마지막 detail");
                    sb.AppendLine("----|----|----|----");
                    foreach (var kv in failCounts)
                        sb.AppendLine($"{kv.Key} | {kv.Value} | {(failFirstStage.TryGetValue(kv.Key, out var st) ? st : 0)} | {(failLastDetail.TryGetValue(kv.Key, out var d) ? d : "")}");
                }
                File.WriteAllText(Report, sb.ToString());
            }
            catch { }
        }
    }
}
#endif

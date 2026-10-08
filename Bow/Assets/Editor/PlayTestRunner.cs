#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bow.EditorTools
{
    /// <summary>
    /// 자동 플레이 테스트 러너 (Unity MCP 없이도 Play 모드 검증 가능).
    ///
    /// 사용법
    ///  - 메뉴: Tools → 활 → 플레이 테스트 (자동 봇 대전 30초, 로그 저장)
    ///  - 명령줄: Unity.exe -projectPath "...\Bow" -executeMethod Bow.EditorTools.PlayTestRunner.RunAndQuit
    ///
    /// 동작: BowDuel 씬 열기 → PlayerPrefs "hwal_autotest"=1 → Play 진입(봇 대전 자동 시작, 자동 사격)
    ///       → 30초 동안 Console 로그 수집 → Logs/playtest.log 저장 → Play 종료 (명령줄이면 에디터 종료).
    /// 도메인 리로드를 넘기기 위해 상태는 SessionState에 보관하고 [InitializeOnLoad]로 재구독한다.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayTestRunner
    {
        private const string ScenePath = "Assets/Scenes/BowDuel.unity";
        private const string KeyActive = "Bow.PlayTest.Active";
        private const string KeyQuit = "Bow.PlayTest.Quit";
        private const string KeyStart = "Bow.PlayTest.Start";
        private const string KeyPhase = "Bow.PlayTest.Phase"; // 0 대기, 1 플레이 중, 2 종료 처리
        private const float Duration = 30f;

        private static StringBuilder log;
        private static int errorCount, exceptionCount, warningCount;

        static PlayTestRunner()
        {
            if (!SessionState.GetBool(KeyActive, false)) return;
            // 도메인 리로드 후 재진입
            EditorApplication.delayCall += Resume;
        }

        [MenuItem("Tools/활/플레이 테스트 (자동 봇 대전 30초, 로그 저장)")]
        public static void Run()
        {
            Start(false);
        }

        /// <summary>명령줄용: 테스트 후 에디터 종료</summary>
        public static void RunAndQuit()
        {
            Start(true);
        }

        private static void Start(bool quitAfter)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[PlayTest] 이미 Play 모드입니다."); return; }
            SessionState.SetBool(KeyActive, true);
            SessionState.SetBool(KeyQuit, quitAfter);
            SessionState.SetInt(KeyPhase, 0);
            PlayerPrefs.SetInt("hwal_autotest", 1);
            PlayerPrefs.Save();
            Append("[PlayTest] 시작 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " / Unity " + Application.unityVersion);
            EditorSceneManager.OpenScene(ScenePath);
            Append("[PlayTest] 씬 열림: " + ScenePath);
            Flush();
            SessionState.SetInt(KeyPhase, 1);
            SessionState.SetFloat(KeyStart, -1f);
            Subscribe();
            EditorApplication.isPlaying = true;
        }

        private static void Resume()
        {
            int phase = SessionState.GetInt(KeyPhase, 0);
            if (phase == 1 && EditorApplication.isPlaying)
            {
                Subscribe();
                if (SessionState.GetFloat(KeyStart, -1f) < 0f)
                {
                    SessionState.SetFloat(KeyStart, (float)EditorApplication.timeSinceStartup);
                    Append("[PlayTest] Play 모드 진입 확인");
                }
            }
            else if (phase == 2 && !EditorApplication.isPlaying)
            {
                Finish();
            }
            else if (phase == 1 && !EditorApplication.isPlaying)
            {
                // Play 진입 실패 (컴파일 오류 등)
                Append("[PlayTest] Play 모드 진입 실패 — 컴파일 오류 여부 확인 필요");
                SessionState.SetInt(KeyPhase, 2);
                Finish();
            }
        }

        private static void Subscribe()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>Play 종료 시 도메인 리로드가 없어도(Enter Play Mode Options) 마무리가 호출되도록 보강</summary>
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetInt(KeyPhase, 0) == 2)
                EditorApplication.delayCall += Finish;
            else if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(KeyPhase, 0) == 1
                     && SessionState.GetFloat(KeyStart, -1f) < 0f)
            {
                SessionState.SetFloat(KeyStart, (float)EditorApplication.timeSinceStartup);
                Append("[PlayTest] Play 모드 진입 확인");
            }
        }

        private static void Tick()
        {
            if (SessionState.GetInt(KeyPhase, 0) != 1) return;
            if (!EditorApplication.isPlaying) return;
            float start = SessionState.GetFloat(KeyStart, -1f);
            if (start < 0f) { SessionState.SetFloat(KeyStart, (float)EditorApplication.timeSinceStartup); return; }
            if (EditorApplication.timeSinceStartup - start >= Duration)
            {
                Append("[PlayTest] " + Duration + "초 경과 → Play 종료");
                Flush();
                SessionState.SetInt(KeyPhase, 2);
                EditorApplication.update -= Tick;
                EditorApplication.isPlaying = false;
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Assert) errorCount++;
            else if (type == LogType.Exception) exceptionCount++;
            else if (type == LogType.Warning) warningCount++;
            string line = "[" + type + "] " + condition;
            if ((type == LogType.Exception || type == LogType.Error) && !string.IsNullOrEmpty(stackTrace))
                line += "\n" + stackTrace.TrimEnd();
            Append(line);
            if (type == LogType.Exception || type == LogType.Error) Flush();
        }

        private static void Finish()
        {
            if (!SessionState.GetBool(KeyActive, false)) return; // 중복 호출 방지
            SessionState.SetBool(KeyActive, false);
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            PlayerPrefs.DeleteKey("hwal_autotest");
            PlayerPrefs.Save();
            Append("[PlayTest] 완료 — Error " + errorCount + " / Exception " + exceptionCount + " / Warning " + warningCount);
            Flush();
            bool quit = SessionState.GetBool(KeyQuit, false);
            SessionState.EraseBool(KeyActive);
            SessionState.EraseBool(KeyQuit);
            SessionState.EraseInt(KeyPhase);
            SessionState.EraseFloat(KeyStart);
            Debug.Log("[PlayTest] 로그 저장: " + LogPath());
            if (quit) EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        private static string LogPath()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(Path.Combine(root, "Logs"), "playtest.log");
        }

        private static void Append(string line)
        {
            if (log == null) log = new StringBuilder();
            log.AppendLine(line);
        }

        private static void Flush()
        {
            if (log == null || log.Length == 0) return;
            try
            {
                string p = LogPath();
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                File.AppendAllText(p, log.ToString(), Encoding.UTF8);
                log.Length = 0;
            }
            catch (Exception e) { Debug.LogWarning("[PlayTest] 로그 저장 실패: " + e.Message); }
        }
    }
}
#endif

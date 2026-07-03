// ============================================================================
// UnityAutoRefresh.cs — 파일 트리거 기반 자동 갱신+재생 사이클
// ============================================================================
// 외부 자동화(Claude Code 등)가 트리거 파일을 생성하면 에디터가 스스로:
//   1) Play 중이면 Play 종료
//   2) AssetDatabase.Refresh (에셋/스크립트 갱신, 필요 시 컴파일+도메인 리로드)
//   3) 갱신 완료 후 Play 재시작
//
// 트리거: 프로젝트 루트의 ".claude/refresh_trigger" 파일 생성 (내용 무관)
//   - 외부에서:  touch ".claude/refresh_trigger"
//   - 키 입력/창 포커스가 전혀 필요 없어 백그라운드 자동화에서도 100% 신뢰성
//     (HexaBuildScript의 Builds/DO_BUILD 파일감시 패턴과 동일)
//
// 재생 재개는 도메인 리로드를 넘어 SessionState 플래그로 이어진다.
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace JewelsHexaPuzzle.EditorTools
{
    [InitializeOnLoad]
    public static class UnityAutoRefresh
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string TriggerPath => Path.Combine(ProjectRoot, ".claude", "refresh_trigger");

        // 도메인 리로드 후에도 "재생 재개 예약"이 살아남도록 SessionState 사용
        private const string ResumePlayKey = "HexaPuzzle_AutoRefresh_ResumePlay";

        private static double nextPollTime;
        private static bool pendingResume;       // 리로드 없는 경우(에셋만 갱신)의 인메모리 재개 예약
        private static double resumeNotBefore;   // 컴파일 시작 감지 유예 시간

        static UnityAutoRefresh()
        {
            EditorApplication.update += Tick;

            // ★ 도메인 리로드 직후: 재생 재개 플래그 확인 (스크립트 컴파일을 거친 경로)
            if (SessionState.GetBool(ResumePlayKey, false))
            {
                SessionState.SetBool(ResumePlayKey, false);
                // 에디터 초기화가 끝난 뒤 안전하게 재생 시작
                EditorApplication.delayCall += () =>
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
                    {
                        Debug.Log("[UnityAutoRefresh] 컴파일 완료 — Play 재시작");
                        EditorApplication.isPlaying = true;
                    }
                };
            }
        }

        private static void Tick()
        {
            // 0.5초 주기 폴링 (파일 IO 절약)
            if (EditorApplication.timeSinceStartup < nextPollTime) return;
            nextPollTime = EditorApplication.timeSinceStartup + 0.5;

            // ── 재개 대기 처리 (리로드가 일어나지 않은 에셋 갱신 경로) ──
            if (pendingResume)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return; // 컴파일 시작됨 → 리로드 경로가 처리
                if (EditorApplication.timeSinceStartup < resumeNotBefore) return;          // 컴파일 시작 감지 유예
                pendingResume = false;
                SessionState.SetBool(ResumePlayKey, false); // 인메모리 경로로 처리 — 리로드 플래그 정리
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.Log("[UnityAutoRefresh] 갱신 완료(컴파일 없음) — Play 재시작");
                    EditorApplication.isPlaying = true;
                }
                return;
            }

            // ── 트리거 감지 ──
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(TriggerPath)) return;

            try { File.Delete(TriggerPath); }
            catch { return; } // 잠금 등 일시 실패 → 다음 폴링에서 재시도

            Debug.Log("[UnityAutoRefresh] 트리거 감지 — 갱신 사이클 시작 (Play종료→Refresh→Play)");
            SessionState.SetBool(ResumePlayKey, true);

            if (EditorApplication.isPlaying)
            {
                // Play 종료 완료 시점을 콜백으로 받아 Refresh 실행
                EditorApplication.playModeStateChanged += OnExitedPlayMode;
                EditorApplication.isPlaying = false;
            }
            else
            {
                DoRefresh();
            }
        }

        private static void OnExitedPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnExitedPlayMode;
            DoRefresh();
        }

        private static void DoRefresh()
        {
            AssetDatabase.Refresh();
            // 스크립트 변경이 있으면 곧 컴파일+도메인 리로드 → 정적 생성자가 ResumePlayKey로 재생 재개.
            // 변경이 에셋뿐이면 리로드가 없음 → 1.5초 유예 후에도 컴파일이 시작되지 않으면 직접 재생.
            pendingResume = true;
            resumeNotBefore = EditorApplication.timeSinceStartup + 1.5;
        }
    }
}

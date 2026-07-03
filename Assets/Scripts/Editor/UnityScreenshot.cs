// ============================================================================
// UnityScreenshot.cs — 파일 트리거 기반 게임뷰 스크린샷 캡처
// ============================================================================
// 외부 자동화(Claude Code 등)가 ".claude/screenshot_trigger" 파일을 생성하면
// 에디터가 현재 게임뷰(재생 중이면 실행 화면)를 PNG로 캡처한다.
//   출력: ".claude/captures/game.png"  (상태: ".claude/captures/last.txt")
// 키 입력/포커스 불필요 — 백그라운드 자동화에서 100% 신뢰성(UnityAutoRefresh와 동일 패턴).
// ============================================================================

using System.IO;
using UnityEditor;
using UnityEngine;

namespace JewelsHexaPuzzle.EditorTools
{
    [InitializeOnLoad]
    public static class UnityScreenshot
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string TriggerPath => Path.Combine(ProjectRoot, ".claude", "screenshot_trigger");
        private static string CaptureDir => Path.Combine(ProjectRoot, ".claude", "captures");
        private static string CapturePath => Path.Combine(CaptureDir, "game.png");
        private static string StatusPath => Path.Combine(CaptureDir, "last.txt");

        private static double nextPollTime;
        private static int pendingFrames;   // 캡처 요청 후 파일 기록 대기 프레임
        private static string pendingName;

        static UnityScreenshot()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            // 캡처 요청 후 파일 기록 확인 (CaptureScreenshot은 다음 프레임에 비동기 기록)
            if (pendingFrames > 0)
            {
                pendingFrames--;
                if (pendingFrames == 0)
                {
                    bool ok = File.Exists(CapturePath);
                    try { File.WriteAllText(StatusPath, (ok ? "OK " : "PENDING ") + pendingName); } catch { }
                }
                return;
            }

            if (EditorApplication.timeSinceStartup < nextPollTime) return;
            nextPollTime = EditorApplication.timeSinceStartup + 0.5;

            if (!File.Exists(TriggerPath)) return;
            try { File.Delete(TriggerPath); }
            catch { return; }

            try
            {
                Directory.CreateDirectory(CaptureDir);
                if (File.Exists(CapturePath)) { try { File.Delete(CapturePath); } catch { } }
                ScreenCapture.CaptureScreenshot(CapturePath);
                pendingName = System.DateTime.Now.ToString("HH:mm:ss");
                pendingFrames = 30; // ~30 에디터 틱 후 기록 확인
                Debug.Log("[UnityScreenshot] 게임뷰 캡처 → " + CapturePath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[UnityScreenshot] 캡처 실패: " + e.Message);
            }
        }
    }
}

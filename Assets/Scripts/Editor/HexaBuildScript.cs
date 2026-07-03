using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JewelsHexaPuzzle.EditorTools
{
    /// <summary>
    /// 핸드폰(안드로이드) 테스트 빌드 자동화.
    /// 트리거: 메뉴(HexaPuzzle ▸ Build Android APK, 단축키 Ctrl+Shift+M) 또는
    ///         트리거 파일 생성(Builds/DO_BUILD) — 외부 자동화에서 키 입력 없이 발동.
    /// 출력: &lt;프로젝트&gt;/Builds/HexaPuzzle.apk, 결과: Builds/build_status.txt
    ///
    /// 파일 감시(EditorApplication.update) 방식이라 키 포커스 문제 없이 발동되며,
    /// Android 타겟 전환에 따른 도메인 리로드도 트리거 파일을 유지해 견딘다.
    /// </summary>
    [InitializeOnLoad]
    public static class HexaBuildScript
    {
        private const string AppId = "com.matchmine.hexapuzzle";
        private const string MainScene = "Assets/Scenes/GameScene.unity";

        // ★ 프로젝트 루트 = Application.dataPath(.../Assets)의 상위 — Unity CWD에 의존하지 않음
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string BuildDir => Path.Combine(ProjectRoot, "Builds");
        private static string ApkPath => Path.Combine(BuildDir, "HexaPuzzle.apk");
        private static string StatusPath => Path.Combine(BuildDir, "build_status.txt");
        private static string TriggerPath => Path.Combine(BuildDir, "DO_BUILD");

        static HexaBuildScript()
        {
            // 에디터 상주 훅: 트리거 파일을 주기적으로 감시
            EditorApplication.update += Tick;
            Debug.Log("[HexaBuildScript] 감시 훅 등록됨 — 트리거: " + TriggerPath);
        }

        private static void Tick()
        {
            if (!File.Exists(TriggerPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return; // 플레이 중엔 대기
            if (BuildPipeline.isBuildingPlayer) return;

            // 타겟이 Android가 아니면 먼저 전환 (트리거 파일은 유지 → 리로드 후 재진입하여 빌드)
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[HexaBuildScript] 빌드 타겟 → Android 전환 (최초 전환은 에셋 재임포트로 시간이 걸립니다)");
                bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                if (!ok)
                {
                    SafeDelete(TriggerPath);
                    Write(StatusPath, "FAILED | Android 빌드 타겟 전환 실패 (Android Build Support 확인 필요)");
                    Debug.LogError("[HexaBuildScript] Android 타겟 전환 실패");
                }
                return; // 전환(및 리로드) 후 다음 Tick에서 빌드
            }

            // 여기까지 왔으면 타겟 = Android → 빌드 1회 실행
            SafeDelete(TriggerPath);   // 중복 발동 방지
            BuildAndroidInternal();
        }

        [MenuItem("HexaPuzzle/Build Android APK %#m")]
        public static void BuildAndroidMenu()
        {
            // 메뉴 발동: 트리거 파일을 만들어 동일 경로(Tick)로 처리 (타겟 전환 안전)
            Directory.CreateDirectory(BuildDir);
            Write(TriggerPath, DateTime.Now.ToString("o"));
            Debug.Log("[HexaBuildScript] 빌드 트리거 생성 (Tick에서 처리)");
        }

        /// <summary>
        /// 배치모드(헤드리스) 직접 빌드 진입점.
        /// 사용: Unity.exe -batchmode -quit -projectPath "..." -buildTarget Android
        ///        -executeMethod JewelsHexaPuzzle.EditorTools.HexaBuildScript.BuildAndroidBatch -logFile build.log
        /// (-buildTarget Android로 실행하면 활성 타겟이 Android라 전환/리로드 없이 바로 빌드)
        /// </summary>
        public static void BuildAndroidBatch()
        {
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

                BuildAndroidInternal();

                bool ok = File.Exists(ApkPath) && new FileInfo(ApkPath).Length > 0;
                if (Application.isBatchMode)
                    EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception e)
            {
                Write(StatusPath, "EXCEPTION(batch) | " + e.Message);
                Debug.LogError("[HexaBuildScript] 배치 빌드 예외: " + e);
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }

        private static void BuildAndroidInternal()
        {
            try
            {
                Directory.CreateDirectory(BuildDir);

                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AppId);
                EditorUserBuildSettings.buildAppBundle = false; // 사이드로딩용 단일 APK

                var scenes = new List<string>();
                if (File.Exists(Path.Combine(ProjectRoot, MainScene)))
                    scenes.Add(MainScene);
                else
                    foreach (var s in EditorBuildSettings.scenes)
                        if (s.enabled) scenes.Add(s.path);

                if (scenes.Count == 0)
                {
                    Write(StatusPath, "FAILED | 빌드할 씬을 찾지 못했습니다 (GameScene 누락)");
                    Debug.LogError("[HexaBuildScript] 빌드 씬 없음");
                    return;
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                };

                Debug.Log($"[HexaBuildScript] 빌드 시작 → {ApkPath} (씬: {string.Join(", ", scenes)})");
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;

                string line = $"{summary.result} | apk={ApkPath} | sizeBytes={summary.totalSize} " +
                              $"| errors={summary.totalErrors} | warnings={summary.totalWarnings} " +
                              $"| seconds={summary.totalTime.TotalSeconds:F0} | appId={AppId}";
                Write(StatusPath, line);

                if (summary.result == BuildResult.Succeeded)
                    Debug.Log($"[HexaBuildScript] ✅ 빌드 성공 → {ApkPath}");
                else
                    Debug.LogError($"[HexaBuildScript] ❌ 빌드 실패: {summary.result} (errors={summary.totalErrors})");
            }
            catch (Exception e)
            {
                Write(StatusPath, "EXCEPTION | " + e.Message);
                Debug.LogError("[HexaBuildScript] 빌드 예외: " + e);
            }
        }

        private static void Write(string path, string text)
        {
            try { File.WriteAllText(path, text); } catch { }
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}

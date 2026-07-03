#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace HexaPuzzle.StageClear.EditorTools
{
    /// <summary>
    /// 스크립트 리로드 시 1회: Parchment 팝업 스프라이트+프리팹을 생성하고
    /// 런타임에서 로드할 수 있도록 Resources 폴더에 복사한다.
    /// - 데모 씬/열린 씬은 건드리지 않음 (사용자의 GameScene 보호).
    /// - 모달 다이얼로그가 있는 CreatePrefabs() 대신 private BuildAndSavePrefab(Parchment)만 reflection 호출.
    /// - 이미 Resources에 준비되어 있으면 스킵(자가치유 + 중복방지).
    /// </summary>
    public static class StageClearAutoSetup
    {
        private const string ResourcesDir    = "Assets/HexaPuzzle/StageClear/Resources";
        private const string ResourcesPrefab = ResourcesDir + "/StageClearPopup_Parchment.prefab";
        private const string SrcPrefab       = "Assets/HexaPuzzle/StageClear/Prefabs/StageClearPopup_Parchment.prefab";

        [DidReloadScripts]
        private static void OnReload() => EditorApplication.delayCall += RunOnce;

        private static void RunOnce()
        {
            // 이미 준비됨 → 스킵
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPrefab) != null) return;

            try
            {
                // 1) 스프라이트 생성 (다이얼로그 없음)
                StageClearSetupMenu.EnsureSprites();

                // 2) Prefabs 폴더 보장 (CreatePrefabs를 우회하므로 직접 생성)
                if (!AssetDatabase.IsValidFolder("Assets/HexaPuzzle/StageClear/Prefabs"))
                {
                    AssetDatabase.CreateFolder("Assets/HexaPuzzle/StageClear", "Prefabs");
                    AssetDatabase.Refresh();
                }

                // 3) Parchment 프리팹만 생성 (private 메서드 — 다이얼로그 우회)
                var m = typeof(StageClearSetupMenu).GetMethod(
                    "BuildAndSavePrefab", BindingFlags.NonPublic | BindingFlags.Static);
                if (m == null) { Debug.LogWarning("[StageClearAutoSetup] BuildAndSavePrefab 미발견"); return; }
                m.Invoke(null, new object[] { StageClearTheme.Parchment });
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefab) == null)
                { Debug.LogWarning("[StageClearAutoSetup] 프리팹 생성 실패: " + SrcPrefab); return; }

                // 3) Resources로 복사 (런타임 Resources.Load 용)
                if (!System.IO.Directory.Exists(ResourcesDir))
                {
                    System.IO.Directory.CreateDirectory(ResourcesDir);
                    AssetDatabase.Refresh();
                }
                if (AssetDatabase.CopyAsset(SrcPrefab, ResourcesPrefab))
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("[StageClearAutoSetup] 완료 — Parchment 팝업을 Resources에 준비 (런타임 자동 로드)");
                }
                else Debug.LogWarning("[StageClearAutoSetup] Resources 복사 실패");
            }
            catch (System.Exception e) { Debug.LogError("[StageClearAutoSetup] " + e); }
        }
    }
}
#endif

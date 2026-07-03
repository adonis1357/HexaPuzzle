#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace HexaPuzzle.RuneBlocks.EditorTools
{
    /// <summary>
    /// 스크립트 리로드 시 1회:
    ///   1) Rune Stone 6장 스프라이트를 RuneBlockSetupMenu.GenerateRuneStone(private) 리플렉션 호출로 생성
    ///      (모달 다이얼로그 우회, 데모 씬 / 프리팹 단계는 건너뜀 — UI Image 블록을 쓰는 본 프로젝트엔 불필요)
    ///   2) 생성된 PNG의 바이트를 Assets/Resources/Gems/gem_{red,orange,yellow,green,blue,purple}.png 위에
    ///      덮어써서 GemSpriteProvider가 자동으로 룬 스톤 텍스처를 로드하도록 한다.
    ///      대상 .meta(GUID/스프라이트 설정)는 그대로 보존 → 씬/프리팹 참조 깨지지 않음.
    ///   3) 이미 생성된 흔적(RuneStone_Amethyst.png)이 있으면 스킵 — 자가치유 + 중복 실행 방지.
    /// </summary>
    public static class RuneBlockAutoSetup
    {
        private const string SpritesPath = "Assets/HexaPuzzle/RuneBlocks/Sprites";
        private const int SpriteSize = 256;
        private const string SessionDoneKey = "RuneBlocks_AutoSetup_v1";

        [DidReloadScripts]
        private static void OnReload() => EditorApplication.delayCall += RunOnce;

        private static void RunOnce()
        {
            if (SessionState.GetBool(SessionDoneKey, false)) return;

            string marker = SpritesPath + "/RuneStone_Amethyst.png";
            if (File.Exists(marker))
            {
                // 이미 한 번 생성+적용 완료 → 스킵
                SessionState.SetBool(SessionDoneKey, true);
                return;
            }
            SessionState.SetBool(SessionDoneKey, true);

            try
            {
                // 1) 스프라이트 폴더 보장
                if (!AssetDatabase.IsValidFolder(SpritesPath))
                {
                    AssetDatabase.CreateFolder("Assets/HexaPuzzle/RuneBlocks", "Sprites");
                    AssetDatabase.Refresh();
                }

                // 2) RuneBlockSetupMenu의 private GenerateRuneStone(path, meta, size) 리플렉션 호출 → 다이얼로그 없이 6장 생성
                var t = typeof(RuneBlockSetupMenu);
                var gen = t.GetMethod("GenerateRuneStone", BindingFlags.NonPublic | BindingFlags.Static);
                if (gen == null) { Debug.LogWarning("[RuneBlockAutoSetup] GenerateRuneStone 미발견"); return; }

                foreach (var meta in RuneColorMeta.All)
                {
                    var path = $"{SpritesPath}/RuneStone_{meta.englishName}.png";
                    gen.Invoke(null, new object[] { path, meta, SpriteSize });
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 3) Resources/Gems/gem_{color}.png의 PNG 바이트만 덮어쓰기 (대상 .meta는 그대로 → GUID 보존)
                var map = new (string srcEnglish, string destColor)[]
                {
                    ("Ruby",     "red"),
                    ("Amber",    "orange"),
                    ("Citrine",  "yellow"),
                    ("Emerald",  "green"),
                    ("Sapphire", "blue"),
                    ("Amethyst", "purple"),
                };
                int overwritten = 0;
                foreach (var pair in map)
                {
                    string src = $"{SpritesPath}/RuneStone_{pair.srcEnglish}.png";
                    string dst = $"Assets/Resources/Gems/gem_{pair.destColor}.png";
                    if (!File.Exists(src)) { Debug.LogWarning("[RuneBlockAutoSetup] 소스 누락: " + src); continue; }
                    if (!File.Exists(dst))
                    {
                        // 대상이 없으면 그냥 복사 (Resources/Gems가 비어 있는 케이스)
                        File.Copy(src, dst);
                    }
                    else
                    {
                        File.Copy(src, dst, true); // PNG 바이트만 덮어씀 — .meta는 그대로
                    }
                    AssetDatabase.ImportAsset(dst);
                    overwritten++;
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[RuneBlockAutoSetup] 완료 — Rune Stone {overwritten}장을 기본 블록(gem_*) 텍스처로 교체");
            }
            catch (System.Exception e) { Debug.LogError("[RuneBlockAutoSetup] " + e); }
        }
    }
}
#endif

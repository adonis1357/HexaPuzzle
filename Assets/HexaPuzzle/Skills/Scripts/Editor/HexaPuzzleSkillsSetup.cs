#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HexaPuzzle.Skills.EditorTools
{
    /// <summary>
    /// 스킬 패키지 자동 셋업.
    ///   HexaPuzzle → Skills → ⚡ Build Catalog From JSON
    /// skills.json 을 읽어 SkillDefinition 에셋들 + SkillCatalog.asset 을 생성/갱신하고,
    /// Sprites 폴더의 PNG 를 이름으로 자동 연결합니다.
    ///
    /// JsonUtility 는 최상위 배열/딕셔너리를 직접 파싱하지 못하므로 래퍼 구조체를 사용합니다.
    /// </summary>
    public static class HexaPuzzleSkillsSetup
    {
        const string Root      = "Assets/HexaPuzzle/Skills";
        const string JsonPath  = Root + "/Data/skills.json";
        const string SprDir    = Root + "/Sprites";
        const string DefDir    = Root + "/Definitions";
        const string CatPath   = Root + "/SkillCatalog.asset";

        // ─── JSON DTO ──────────────────────────────────────────────
        [System.Serializable] class JLevel { public int level; public int value; public string label; }
        [System.Serializable] class JSkill {
            public string id, category, nameKo, nameEn, desc, stat, sprite, powerup, item;
            public int baseValue, maxLevel;
            public string[] levelSprites;
            public string[] mix;
            public JLevel[] levels;
        }
        [System.Serializable] class JRoot { public JSkill[] skills; }

        // ★ 스크립트 리로드 시 1회 자동 빌드 — 에디터 메뉴 수동 실행 없이 카탈로그/정의 생성.
        //   카탈로그가 아직 없을 때만 빌드 (이미 있으면 메뉴로 수동 갱신).
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void AutoBuildOnReload()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("HexaSkills_AutoBuilt_v1", false)) return;
                SessionState.SetBool("HexaSkills_AutoBuilt_v1", true);
                if (AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatPath) == null)
                {
                    Debug.Log("[Skills] 카탈로그 미존재 — 자동 빌드 실행");
                    BuildCatalog();
                }
            };
        }

        [MenuItem("HexaPuzzle/Skills/⚡ Build Catalog From JSON")]
        public static void BuildCatalog()
        {
            var jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            if (jsonAsset == null) { Debug.LogError("[Skills] skills.json 을 찾을 수 없습니다: " + JsonPath); return; }

            var root = JsonUtility.FromJson<JRoot>(jsonAsset.text);
            if (root == null || root.skills == null) { Debug.LogError("[Skills] JSON 파싱 실패."); return; }

            EnsureFolder(DefDir);
            var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SkillCatalog>();
                AssetDatabase.CreateAsset(catalog, CatPath);
            }
            catalog.skills.Clear();

            int created = 0, linked = 0, missing = 0;
            foreach (var js in root.skills)
            {
                string defPath = DefDir + "/Skill_" + js.id + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(defPath);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<SkillDefinition>();
                    AssetDatabase.CreateAsset(def, defPath);
                    created++;
                }

                def.id          = js.id;
                def.category    = ParseCategory(js.category);
                def.nameKo      = js.nameKo;
                def.nameEn      = js.nameEn;
                def.description = js.desc;
                def.stat        = js.stat;
                def.baseValue   = js.baseValue;
                def.maxLevel    = Mathf.Max(1, js.maxLevel);
                def.powerup     = js.powerup;
                def.item        = js.item;
                def.mix         = js.mix;

                def.levels = new List<SkillLevel>();
                if (js.levels != null)
                    foreach (var l in js.levels)
                        def.levels.Add(new SkillLevel { level = l.level, value = l.value, label = l.label });

                // 아이콘 연결
                def.icon = LoadSprite(js.sprite, ref linked, ref missing);
                if (js.levelSprites != null && js.levelSprites.Length > 0)
                {
                    def.levelIcons = new Sprite[js.levelSprites.Length];
                    for (int i = 0; i < js.levelSprites.Length; i++)
                        def.levelIcons[i] = LoadSprite(js.levelSprites[i], ref linked, ref missing);
                }
                else def.levelIcons = new Sprite[0];

                EditorUtility.SetDirty(def);
                catalog.skills.Add(def);
            }

            // ★ 데이터 없는 아이콘 자동 추가 — Sprites 폴더의 PNG 중 json에 정의/연결되지 않은
            //   스프라이트가 있으면 기본 SkillDefinition을 자동 생성해 카탈로그에 포함한다.
            //   (사용자 요청: "스킬 아이콘은 있는데 데이터가 없을 경우 해당 내용도 추가")
            var usedSprites = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var js in root.skills)
            {
                if (!string.IsNullOrEmpty(js.sprite)) usedSprites.Add(js.sprite);
                if (js.levelSprites != null)
                    foreach (var ls in js.levelSprites)
                        if (!string.IsNullOrEmpty(ls)) usedSprites.Add(ls);
            }

            int autoAdded = 0;
            if (AssetDatabase.IsValidFolder(SprDir))
            {
                string[] sprGuids = AssetDatabase.FindAssets("t:Sprite", new[] { SprDir });
                foreach (var guid in sprGuids)
                {
                    string sprPath = AssetDatabase.GUIDToAssetPath(guid);
                    string sprName = System.IO.Path.GetFileNameWithoutExtension(sprPath);
                    if (usedSprites.Contains(sprName)) continue;   // 이미 데이터에 연결됨
                    usedSprites.Add(sprName);

                    string autoId = ToSnakeId(sprName);
                    string defPath = DefDir + "/Skill_" + autoId + ".asset";
                    var def = AssetDatabase.LoadAssetAtPath<SkillDefinition>(defPath);
                    if (def == null)
                    {
                        def = ScriptableObject.CreateInstance<SkillDefinition>();
                        AssetDatabase.CreateAsset(def, defPath);
                        created++;
                    }
                    def.id          = autoId;
                    def.category    = GuessCategory(sprName);
                    def.nameKo      = sprName.Replace("_", " ");
                    def.nameEn      = sprName.Replace("_", " ");
                    def.description = "자동 생성된 스킬(데이터 미정의) — skills.json에 정의를 추가하세요.";
                    def.stat        = autoId;
                    def.baseValue   = 0;
                    def.maxLevel    = 1;
                    def.powerup     = null;
                    def.item        = null;
                    def.mix         = null;
                    def.levels      = new List<SkillLevel> { new SkillLevel { level = 1, value = 1, label = "ON" } };
                    var spr = AssetDatabase.LoadAssetAtPath<Sprite>(sprPath);
                    if (spr != null) { def.icon = spr; linked++; }
                    def.levelIcons  = new Sprite[0];
                    EditorUtility.SetDirty(def);
                    catalog.skills.Add(def);
                    autoAdded++;
                    Debug.LogWarning($"[Skills] 데이터 없는 아이콘 자동 추가: {sprName} → 기본 정의 생성(id={autoId}, cat={def.category})");
                }
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
            Debug.Log($"[Skills] 카탈로그 빌드 완료 — 정의 {root.skills.Length}개 + 자동추가 {autoAdded}개 (신규 {created}), 스프라이트 연결 {linked}개, 누락 {missing}개.\n→ {CatPath}");
        }

        /// <summary>스프라이트 이름 → json 스타일 snake_case id. 예: "Drill_damage" → "drill_damage".</summary>
        static string ToSnakeId(string name)
        {
            return name.ToLowerInvariant();
        }

        /// <summary>스프라이트 이름 접두사로 카테고리 추정 (데이터 없는 아이콘 자동 분류용).</summary>
        static SkillCategory GuessCategory(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.StartsWith("deploy")) return SkillCategory.Deploy;
            if (n.StartsWith("drill") || n.StartsWith("bomb") || n.StartsWith("drone") || n.StartsWith("cannon"))
                return SkillCategory.PowerUpUpgrade;
            if (n.StartsWith("hammer") || n.StartsWith("swap") || n.StartsWith("line"))
                return SkillCategory.ItemUpgrade;
            if (n.StartsWith("skill_") || n.Contains("crit") || n.Contains("hit"))
                return SkillCategory.Combat;
            if (n.StartsWith("gauge")) return SkillCategory.Meta;
            return SkillCategory.Meta;
        }

        static Sprite LoadSprite(string name, ref int linked, ref int missing)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string p = SprDir + "/" + name + ".png";
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (spr != null) linked++;
            else { missing++; Debug.LogWarning("[Skills] 스프라이트 누락: " + p); }
            return spr;
        }

        static SkillCategory ParseCategory(string c)
        {
            return System.Enum.TryParse<SkillCategory>(c, true, out var v) ? v : SkillCategory.Meta;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif

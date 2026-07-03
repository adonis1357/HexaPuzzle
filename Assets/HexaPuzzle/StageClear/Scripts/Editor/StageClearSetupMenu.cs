#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HexaPuzzle.StageClear.EditorTools
{
    /// <summary>
    /// HexaPuzzle → StageClear 메뉴.
    /// 코드 1줄로 두 가지 테마(Parchment / Mystic) 프리팹과 데모 씬을 생성합니다.
    /// </summary>
    public static class StageClearSetupMenu
    {
        private const string Root        = "Assets/HexaPuzzle/StageClear";
        private const string SpritesPath = Root + "/Sprites";
        private const string PrefabsPath = Root + "/Prefabs";
        private const string ScenesPath  = Root + "/Scenes";

        [MenuItem("HexaPuzzle/StageClear/⚡ Do Everything", priority = 30)]
        public static void DoEverything()
        {
            EnsureSprites();
            CreatePrefabs();
            CreateDemoScene();
        }

        [MenuItem("HexaPuzzle/StageClear/1. Generate Sprites", priority = 40)]
        public static void EnsureSprites()
        {
            EnsureFolder(SpritesPath);
            GenerateStar(  $"{SpritesPath}/SC_Star.png",       size: 128);
            GenerateRound( $"{SpritesPath}/SC_Round.png",      size: 64, radius: 20);
            GenerateRound( $"{SpritesPath}/SC_Pill.png",       size: 64, radius: 30);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("HexaPuzzle/StageClear/2. Create Popup Prefabs (2)", priority = 50)]
        public static void CreatePrefabs()
        {
            EnsureSprites();
            EnsureFolder(PrefabsPath);
            BuildAndSavePrefab(StageClearTheme.Parchment);
            BuildAndSavePrefab(StageClearTheme.Mystic);
            EditorUtility.DisplayDialog("HexaPuzzle · StageClear",
                "✅ 두 가지 테마 프리팹을 생성했습니다.\n\n" +
                PrefabsPath + "/StageClearPopup_Parchment.prefab\n" +
                PrefabsPath + "/StageClearPopup_Mystic.prefab\n\n" +
                "본인 게임 HUD Canvas 의 자식으로 드래그하거나, 별도 Canvas 에 사용하세요.",
                "OK");
        }

        [MenuItem("HexaPuzzle/StageClear/3. Create Demo Scene", priority = 60)]
        public static void CreateDemoScene()
        {
            EnsureFolder(ScenesPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var bgGO = new GameObject("Background", typeof(SpriteRenderer));
            bgGO.transform.position = new Vector3(0, 0, 10);
            Camera.main.backgroundColor = new Color(0.12f, 0.10f, 0.18f);
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.orthographic = true;
            Object.DestroyImmediate(bgGO); // no sprite needed; solid color clear

            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // 두 팝업을 한 Canvas 에 좌/우로 배치해서 비교
            var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;

            // 각 테마 인스턴스 — left & right
            var leftPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/StageClearPopup_Parchment.prefab");
            var rightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/StageClearPopup_Mystic.prefab");

            if (leftPrefab != null)
            {
                var left = (GameObject)PrefabUtility.InstantiatePrefab(leftPrefab, canvasGO.transform);
                var lrt  = (RectTransform)left.transform;
                lrt.anchoredPosition = new Vector2(-360, 0);
                left.AddComponent<StageClearAutoShow>();
            }
            if (rightPrefab != null)
            {
                var right = (GameObject)PrefabUtility.InstantiatePrefab(rightPrefab, canvasGO.transform);
                var rrt   = (RectTransform)right.transform;
                rrt.anchoredPosition = new Vector2(360, 0);
                right.AddComponent<StageClearAutoShow>();
            }

            var scenePath = $"{ScenesPath}/StageClearDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorUtility.DisplayDialog("HexaPuzzle · StageClear",
                "✅ 데모 씬을 만들었어요.\n\n" + scenePath +
                "\n\n▶ Play 를 누르면 두 팝업이 자동으로 표시됩니다.",
                "OK");
        }

        // ════════════════════════════════════════════════════════════════════
        // Prefab build
        // ════════════════════════════════════════════════════════════════════

        private static void BuildAndSavePrefab(StageClearTheme theme)
        {
            var root = BuildPopup(theme);
            var path = $"{PrefabsPath}/StageClearPopup_{theme}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static GameObject BuildPopup(StageClearTheme theme)
        {
            var pal = StageClearPalette.Resolve(theme);
            var roundedSprite = Load($"{SpritesPath}/SC_Round.png");
            var pillSprite    = Load($"{SpritesPath}/SC_Pill.png");
            var starSprite    = Load($"{SpritesPath}/SC_Star.png");

            // ─── Root ────────────────────────────────────────────────────────
            var root = new GameObject($"StageClearPopup_{theme}",
                typeof(RectTransform), typeof(CanvasGroup));
            var rootRT = (RectTransform)root.transform;
            rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero; rootRT.offsetMax = Vector2.zero;

            var cg = root.GetComponent<CanvasGroup>();

            // ─── Backdrop (dim) ──────────────────────────────────────────────
            var backdrop = AddImage(root.transform, "Backdrop", new Color(0,0,0,0.45f), null);
            Stretch((RectTransform)backdrop.transform);

            // ─── Card ────────────────────────────────────────────────────────
            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(root.transform, false);
            var cardRT = (RectTransform)card.transform;
            cardRT.sizeDelta = new Vector2(560, 760);
            cardRT.anchorMin = cardRT.anchorMax = new Vector2(0.5f, 0.5f);

            // Card shadow (slight offset, behind card visuals)
            var shadow = AddImage(card.transform, "CardShadow", new Color(0,0,0,0.45f), roundedSprite);
            var shadowRT = (RectTransform)shadow.transform;
            shadowRT.anchorMin = Vector2.zero; shadowRT.anchorMax = Vector2.one;
            shadowRT.offsetMin = new Vector2(-6, -16); shadowRT.offsetMax = new Vector2(6, -4);

            // Card border (accent color)
            var border = AddImage(card.transform, "CardBorder", pal.border, roundedSprite);
            Stretch((RectTransform)border.transform);

            // Card body (inset 4px)
            var body = AddImage(card.transform, "CardBody", pal.cardBg, roundedSprite);
            var bodyRT = (RectTransform)body.transform;
            bodyRT.anchorMin = Vector2.zero; bodyRT.anchorMax = Vector2.one;
            bodyRT.offsetMin = new Vector2(4, 4); bodyRT.offsetMax = new Vector2(-4, -4);

            // Inner cream layer for Parchment (extra 6px inset double-stroke feel)
            if (theme == StageClearTheme.Parchment)
            {
                var inner = AddImage(card.transform, "CardInner", pal.cardBg, roundedSprite);
                var innerRT = (RectTransform)inner.transform;
                innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
                innerRT.offsetMin = new Vector2(10, 10); innerRT.offsetMax = new Vector2(-10, -10);
                var outline = inner.AddComponent<Outline>();
                outline.effectColor = new Color(0.78f, 0.74f, 0.66f, 1f);
                outline.effectDistance = new Vector2(0, -1);
            }

            // ─── Stars row ───────────────────────────────────────────────────
            var starsRow = new GameObject("Stars", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            starsRow.transform.SetParent(card.transform, false);
            var starsRT = (RectTransform)starsRow.transform;
            starsRT.anchorMin = new Vector2(0.5f, 1f);
            starsRT.anchorMax = new Vector2(0.5f, 1f);
            starsRT.pivot     = new Vector2(0.5f, 1f);
            starsRT.anchoredPosition = new Vector2(0, -50);
            starsRT.sizeDelta = new Vector2(220, 60);
            var starsHL = starsRow.GetComponent<HorizontalLayoutGroup>();
            starsHL.spacing = 14;
            starsHL.childAlignment = TextAnchor.MiddleCenter;
            starsHL.childForceExpandWidth = false; starsHL.childForceExpandHeight = false;
            starsHL.childControlWidth = false; starsHL.childControlHeight = false;

            var starRTs   = new RectTransform[3];
            var starImgs  = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var s = AddImage(starsRow.transform, $"Star_{i}", pal.accent, starSprite);
                var sRT = (RectTransform)s.transform;
                sRT.sizeDelta = new Vector2(46, 46);
                if (i == 1) sRT.anchoredPosition += new Vector2(0, 4); // center star lifted
                starRTs[i]  = sRT;
                starImgs[i] = s.GetComponent<Image>();
            }

            // ─── Title ───────────────────────────────────────────────────────
            var title = AddText(card.transform, "Title", "STAGE CLEAR",
                fontSize: 44, color: pal.accent, bold: true);
            var titleRT = (RectTransform)title.transform;
            titleRT.anchorMin = new Vector2(0, 1); titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot     = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0, -120);
            titleRT.sizeDelta = new Vector2(0, 52);
            title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            title.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Subtitle
            var subtitle = AddText(card.transform, "Subtitle", "멋진 플레이였어요!",
                fontSize: 18, color: pal.sub, bold: false);
            var subRT = (RectTransform)subtitle.transform;
            subRT.anchorMin = new Vector2(0, 1); subRT.anchorMax = new Vector2(1, 1);
            subRT.pivot     = new Vector2(0.5f, 1f);
            subRT.anchoredPosition = new Vector2(0, -176);
            subRT.sizeDelta = new Vector2(0, 26);
            subtitle.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // ─── Score panel ─────────────────────────────────────────────────
            var scorePanel = AddImage(card.transform, "ScorePanel", pal.innerPanel, roundedSprite);
            var spRT = (RectTransform)scorePanel.transform;
            spRT.anchorMin = new Vector2(0.5f, 1f); spRT.anchorMax = new Vector2(0.5f, 1f);
            spRT.pivot     = new Vector2(0.5f, 1f);
            spRT.anchoredPosition = new Vector2(0, -220);
            spRT.sizeDelta = new Vector2(480, 170);
            var spOutline = scorePanel.AddComponent<Outline>();
            spOutline.effectColor = new Color(pal.accent.r, pal.accent.g, pal.accent.b, 0.35f);
            spOutline.effectDistance = new Vector2(0, -1);

            var scoreLabel = AddText(scorePanel.transform, "ScoreLabel", "SCORE",
                fontSize: 18, color: pal.sub, bold: true);
            var slRT = (RectTransform)scoreLabel.transform;
            slRT.anchorMin = new Vector2(0, 1); slRT.anchorMax = new Vector2(1, 1);
            slRT.pivot     = new Vector2(0.5f, 1f);
            slRT.anchoredPosition = new Vector2(0, -16);
            slRT.sizeDelta = new Vector2(0, 24);
            scoreLabel.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            ApplyLetterSpacing(scoreLabel.GetComponent<Text>(), "S C O R E");

            var scoreValue = AddText(scorePanel.transform, "ScoreValue", "19,980",
                fontSize: 54, color: pal.ink, bold: true);
            var svRT = (RectTransform)scoreValue.transform;
            svRT.anchorMin = new Vector2(0, 1); svRT.anchorMax = new Vector2(1, 1);
            svRT.pivot     = new Vector2(0.5f, 1f);
            svRT.anchoredPosition = new Vector2(0, -44);
            svRT.sizeDelta = new Vector2(0, 64);
            scoreValue.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            scoreValue.GetComponent<Text>().fontStyle = FontStyle.Bold;

            // Gold pill
            var goldPill = AddImage(scorePanel.transform, "GoldPill",
                theme == StageClearTheme.Mystic
                    ? new Color(0.20f, 0.16f, 0.08f, 0.7f)
                    : new Color(0.99f, 0.93f, 0.74f, 1f),
                pillSprite);
            var gpRT = (RectTransform)goldPill.transform;
            gpRT.anchorMin = new Vector2(0.5f, 0f); gpRT.anchorMax = new Vector2(0.5f, 0f);
            gpRT.pivot     = new Vector2(0.5f, 0f);
            gpRT.anchoredPosition = new Vector2(0, 14);
            gpRT.sizeDelta = new Vector2(180, 36);
            var gpOutline = goldPill.AddComponent<Outline>();
            gpOutline.effectColor = StageClearPalette.Gold;
            gpOutline.effectDistance = new Vector2(0, -1);

            var goldText = AddText(goldPill.transform, "GoldText", "+10 GOLD",
                fontSize: 18, color: theme == StageClearTheme.Mystic ? StageClearPalette.Gold : StageClearPalette.GoldDeep,
                bold: true);
            Stretch((RectTransform)goldText.transform);
            goldText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

            // ─── Stat rows ───────────────────────────────────────────────────
            var (stageBestVal, stageBestLbl, stageBadge) = AddStatRow(card.transform, "StageBest",
                "STAGE BEST", "19,980", pal, starSprite, anchoredY: -410, showBadge: true);
            var (myBestVal,    myBestLbl,    myBadge)     = AddStatRow(card.transform, "MyBest",
                "MY BEST",    "19,980", pal, starSprite, anchoredY: -464, showBadge: true);

            // Divider above StageBest
            AddDivider(card.transform, pal.divider, anchoredY: -390);

            // ─── Button ──────────────────────────────────────────────────────
            var btnGO = AddImage(card.transform, "ConfirmButton", pal.buttonFill, roundedSprite);
            var btn   = btnGO.AddComponent<Button>();
            var btnRT = (RectTransform)btnGO.transform;
            btnRT.anchorMin = new Vector2(0.5f, 0f); btnRT.anchorMax = new Vector2(0.5f, 0f);
            btnRT.pivot     = new Vector2(0.5f, 0f);
            btnRT.anchoredPosition = new Vector2(0, 36);
            btnRT.sizeDelta = new Vector2(480, 68);

            var btnShadow = btnGO.AddComponent<Shadow>();
            btnShadow.effectColor = new Color(pal.buttonShadow.r, pal.buttonShadow.g, pal.buttonShadow.b, 0.6f);
            btnShadow.effectDistance = new Vector2(0, -4);

            var btnText = AddText(btnGO.transform, "Label", "확인",
                fontSize: 26, color: pal.buttonText, bold: true);
            Stretch((RectTransform)btnText.transform);
            btnText.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            btnText.GetComponent<Text>().fontStyle = FontStyle.Bold;
            ApplyLetterSpacing(btnText.GetComponent<Text>(), "확  인");

            // ─── Popup component wiring ─────────────────────────────────────
            var popup = root.AddComponent<StageClearPopup>();
            popup.theme        = theme;
            popup.canvasGroup  = cg;
            popup.card         = cardRT;
            popup.backdrop     = backdrop;
            popup.stars        = starRTs;
            popup.starImages   = starImgs;
            popup.titleText    = title.GetComponent<Text>();
            popup.subtitleText = subtitle.GetComponent<Text>();
            popup.scoreLabel   = scoreLabel.GetComponent<Text>();
            popup.scoreValue   = scoreValue.GetComponent<Text>();
            popup.goldText     = goldText.GetComponent<Text>();
            popup.stageBestLabel = stageBestLbl;
            popup.stageBestValue = stageBestVal;
            popup.myBestLabel    = myBestLbl;
            popup.myBestValue    = myBestVal;
            popup.stageBestNewBadge = stageBadge;
            popup.myBestNewBadge    = myBadge;
            popup.confirmButton  = btn;
            popup.confirmText    = btnText.GetComponent<Text>();

            // Hook button click to popup
            btn.onClick.AddListener(popup.HandleConfirm);

            return root;
        }

        // ════════════════════════════════════════════════════════════════════
        // UI helpers
        // ════════════════════════════════════════════════════════════════════

        private static GameObject AddImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            return go;
        }

        private static GameObject AddText(Transform parent, string name, string txt,
            int fontSize, Color color, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = txt;
            t.fontSize = fontSize;
            t.color = color;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyle.Bold;
            return go;
        }

        private static void ApplyLetterSpacing(Text t, string spaced)
        {
            // Unity Legacy Text 는 letterSpacing 이 없어서, 공백 포함 텍스트로 시뮬레이트
            t.text = spaced;
        }

        private static (Text valueText, Text labelText, GameObject badge) AddStatRow(
            Transform card, string id, string label, string value,
            StageClearPalette.Resolved pal, Sprite starSprite,
            float anchoredY, bool showBadge)
        {
            var row = new GameObject(id, typeof(RectTransform));
            row.transform.SetParent(card, false);
            var rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, anchoredY);
            rt.sizeDelta = new Vector2(-60, 44);

            var lbl = AddText(row.transform, "Label", label, 16, pal.sub, bold: true);
            var lrt = (RectTransform)lbl.transform;
            lrt.anchorMin = new Vector2(0, 0.5f); lrt.anchorMax = new Vector2(0, 0.5f);
            lrt.pivot     = new Vector2(0, 0.5f);
            lrt.anchoredPosition = new Vector2(0, 0);
            lrt.sizeDelta = new Vector2(200, 24);
            lbl.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

            var val = AddText(row.transform, "Value", value, 22, pal.ink, bold: true);
            var vrt = (RectTransform)val.transform;
            vrt.anchorMin = new Vector2(1, 0.5f); vrt.anchorMax = new Vector2(1, 0.5f);
            vrt.pivot     = new Vector2(1, 0.5f);
            vrt.anchoredPosition = new Vector2(-72, 0);
            vrt.sizeDelta = new Vector2(180, 28);
            val.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

            GameObject badge = null;
            if (showBadge)
            {
                badge = new GameObject("NewBadge", typeof(RectTransform), typeof(Image));
                badge.transform.SetParent(row.transform, false);
                var brt = (RectTransform)badge.transform;
                brt.anchorMin = new Vector2(1, 0.5f); brt.anchorMax = new Vector2(1, 0.5f);
                brt.pivot     = new Vector2(1, 0.5f);
                brt.anchoredPosition = new Vector2(0, 0);
                brt.sizeDelta = new Vector2(56, 22);
                var bi = badge.GetComponent<Image>();
                bi.color = pal.accent;
                bi.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesPath}/SC_Pill.png");
                bi.type = Image.Type.Sliced;

                var star = AddImage(badge.transform, "Star", pal.buttonText, starSprite);
                var srt = (RectTransform)star.transform;
                srt.anchorMin = new Vector2(0, 0.5f); srt.anchorMax = new Vector2(0, 0.5f);
                srt.pivot     = new Vector2(0, 0.5f);
                srt.anchoredPosition = new Vector2(4, 0);
                srt.sizeDelta = new Vector2(14, 14);

                var bt = AddText(badge.transform, "Text", "NEW", 12, pal.buttonText, bold: true);
                var btr = (RectTransform)bt.transform;
                btr.anchorMin = new Vector2(0, 0); btr.anchorMax = new Vector2(1, 1);
                btr.offsetMin = new Vector2(20, 0); btr.offsetMax = new Vector2(-6, 0);
                bt.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
            }

            return (val.GetComponent<Text>(), lbl.GetComponent<Text>(), badge);
        }

        private static void AddDivider(Transform parent, Color color, float anchoredY)
        {
            var d = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            d.transform.SetParent(parent, false);
            var rt = (RectTransform)d.transform;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, anchoredY);
            rt.sizeDelta = new Vector2(-80, 1);
            d.GetComponent<Image>().color = color;
            d.GetComponent<Image>().raycastTarget = false;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ════════════════════════════════════════════════════════════════════
        // Procedural sprite generation
        // ════════════════════════════════════════════════════════════════════

        private static void GenerateRound(string path, int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px  = new Color32[size * size];
            int r2 = radius * radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int dx = x < radius             ? radius - x :
                         x > size - radius - 1  ? x - (size - radius - 1) : 0;
                int dy = y < radius             ? radius - y :
                         y > size - radius - 1  ? y - (size - radius - 1) : 0;
                int dist2 = dx * dx + dy * dy;
                float a;
                if (dist2 <= r2 - radius)        a = 1f;
                else if (dist2 >= r2 + radius)   a = 0f;
                else                              a = 1f - (Mathf.Sqrt(dist2) - (radius - 1f));
                a = Mathf.Clamp01(a);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType   = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 100;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spriteBorder  = new Vector4(radius, radius, radius, radius);
            imp.SaveAndReimport();
        }

        private static void GenerateStar(string path, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px  = new Color32[size * size];
            float cx = size * 0.5f, cy = size * 0.5f;
            float rOut = size * 0.46f;
            float rIn  = rOut * 0.42f;
            // 10 vertices alternating outer/inner, starting straight up
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (-Mathf.PI / 2f) + i * (Mathf.PI / 5f);
                float r = (i % 2 == 0) ? rOut : rIn;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            // Point-in-polygon with sub-pixel sampling for AA
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0; int samples = 4;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float px2 = x + (sx + 0.5f) / samples;
                    float py2 = y + (sy + 0.5f) / samples;
                    if (PointInPoly(px2, py2, pts)) hits++;
                }
                float a = hits / (float)(samples * samples);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }

        private static bool PointInPoly(float x, float y, Vector2[] pts)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if (((pts[i].y > y) != (pts[j].y > y)) &&
                    (x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x))
                    inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>데모 씬에서 Play 시 자동으로 팝업을 한 번 띄워주는 헬퍼.</summary>
    public class StageClearAutoShow : MonoBehaviour
    {
        public StageClearResult result = StageClearResult.Demo();
        private void Start()
        {
            var p = GetComponent<StageClearPopup>();
            if (p != null) p.Show(result);
        }
    }
}
#endif

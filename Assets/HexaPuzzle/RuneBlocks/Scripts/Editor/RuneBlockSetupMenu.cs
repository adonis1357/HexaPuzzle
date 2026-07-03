#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HexaPuzzle.RuneBlocks.EditorTools
{
    /// <summary>
    /// HexaPuzzle → RuneBlocks 메뉴.
    /// 6장 룬 스톤 스프라이트(256×256 PNG) + RuneBlock 프리팹 + 데모 씬을 자동 생성합니다.
    ///
    /// 스프라이트는 hex-crystals.js 의 "Rune Stone" 스타일을 충실히 옮긴 절차 생성 결과:
    ///   - 어두운 돌 헥사 베벨
    ///   - 색마다 고유 룬 (삼각형/마름모/별/잎/물방울/원형)
    ///   - 룬 뒤의 마법 글로우
    ///   - 돌 텍스처 점들
    /// </summary>
    public static class RuneBlockSetupMenu
    {
        private const string Root        = "Assets/HexaPuzzle/RuneBlocks";
        private const string SpritesPath = Root + "/Sprites";
        private const string PrefabsPath = Root + "/Prefabs";
        private const string ScenesPath  = Root + "/Scenes";
        private const int    SpriteSize  = 256;

        [MenuItem("HexaPuzzle/RuneBlocks/⚡ Do Everything", priority = 30)]
        public static void DoEverything()
        {
            EnsureSprites();
            CreatePrefab();
            CreateDemoScene();
        }

        [MenuItem("HexaPuzzle/RuneBlocks/1. Generate Sprites (6 colors)", priority = 40)]
        public static void EnsureSprites()
        {
            EnsureFolder(SpritesPath);
            foreach (var meta in RuneColorMeta.All)
            {
                var path = $"{SpritesPath}/RuneStone_{meta.englishName}.png";
                GenerateRuneStone(path, meta, SpriteSize);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("HexaPuzzle · RuneBlocks",
                $"✅ 6장 스프라이트 생성 완료\n\n{SpritesPath}/RuneStone_*.png", "OK");
        }

        [MenuItem("HexaPuzzle/RuneBlocks/2. Create RuneBlock Prefab", priority = 50)]
        public static void CreatePrefab()
        {
            EnsureSprites();
            EnsureFolder(PrefabsPath);

            var go = new GameObject("RuneBlock", typeof(SpriteRenderer), typeof(RuneBlock));
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sortingOrder = 10;

            // Load sprites in RuneColor order (0..5)
            var sprites = new Sprite[RuneColorMeta.Count];
            foreach (var meta in RuneColorMeta.All)
                sprites[(int)meta.color] = Load($"{SpritesPath}/RuneStone_{meta.englishName}.png");
            sr.sprite = sprites[0];

            // Inject sprites array via reflection (no public setter to avoid runtime mutation)
            var t = typeof(RuneBlock);
            t.GetField("sprites", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(go.GetComponent<RuneBlock>(), sprites);

            var path = $"{PrefabsPath}/RuneBlock.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            EditorUtility.DisplayDialog("HexaPuzzle · RuneBlocks",
                $"✅ 프리팹 생성 완료\n\n{path}\n\n" +
                "씬에 드래그하고 RuneBlock 컴포넌트의 Color 를 바꾸면 즉시 다른 룬이 표시됩니다.", "OK");
        }

        [MenuItem("HexaPuzzle/RuneBlocks/3. Create Demo Scene", priority = 60)]
        public static void CreateDemoScene()
        {
            EnsureFolder(ScenesPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var cam = Camera.main;
            cam.backgroundColor = new Color(0.12f, 0.10f, 0.18f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = true;
            cam.orthographicSize = 4f;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/RuneBlock.prefab");
            if (prefab == null)
            {
                CreatePrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/RuneBlock.prefab");
            }

            // 6개를 한 줄로 배치 — 각각 다른 RuneColor
            for (int i = 0; i < 6; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.position = new Vector3((i - 2.5f) * 1.4f, 0f, 0f);
                inst.GetComponent<RuneBlock>().SetColor((RuneColor)i);
                inst.name = $"RuneBlock_{(RuneColor)i}";
            }

            var scenePath = $"{ScenesPath}/RuneBlocksDemo.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorUtility.DisplayDialog("HexaPuzzle · RuneBlocks",
                $"✅ 데모 씬 생성 완료\n\n{scenePath}\n\n▶ Play 로 6개 룬을 확인하세요.", "OK");
        }

        // ════════════════════════════════════════════════════════════════
        // Procedural sprite generation — port of hex-crystals.js runeStone()
        // ════════════════════════════════════════════════════════════════
        private static void GenerateRuneStone(string path, RuneColorMeta.Entry meta, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];

            // viewBox 0..200 → texture 0..size
            float scale = size / 200f;
            float cx = 100f, cy = 100f, r = 86f;
            var hexOuter = HexVerts(cx, cy, r);
            var hexInner = HexVerts(cx, cy, r * 0.88f);

            // Color stops (same recipe as gem*())
            var stoneTop = OklchToRgb(0.32f, 0.04f, meta.hue);
            var stoneMid = OklchToRgb(0.22f, 0.05f, meta.hue);
            var stoneBot = OklchToRgb(0.14f, 0.06f, meta.hue);
            var stroke   = OklchToRgb(0.10f, 0.05f, meta.hue);
            var baseC    = OklchToRgb(0.68f, 0.20f, meta.hue);
            var light    = OklchToRgb(0.88f, 0.13f, meta.hue);
            var glow     = OklchToRgb(0.95f, 0.10f, meta.hue);

            // Fill base — transparent everywhere
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            // Sample each texture pixel against the SVG viewBox
            for (int ty = 0; ty < size; ty++)
            {
                // Flip Y — Unity texture origin is bottom-left, SVG is top-left
                int sy = size - 1 - ty;
                float vy = sy / scale;
                for (int tx = 0; tx < size; tx++)
                {
                    float vx = tx / scale;

                    if (!PointInPoly(vx, vy, hexOuter))
                    {
                        px[ty * size + tx] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Outer stone gradient (top→bot, 0..200 vertical → top..bot)
                    float gy = Mathf.InverseLerp(cy - r, cy + r, vy);
                    Color baseStone = SampleStoneGradient(gy, stoneTop, stoneMid, stoneBot);

                    // Inner inset visible region (within innerVerts) is slightly darker (stoneBot)
                    bool inInner = PointInPoly(vx, vy, hexInner);
                    if (inInner)
                        baseStone = Color.Lerp(baseStone, stoneBot, 0.70f);

                    // Magic glow disc behind rune — radial from center
                    float d = Mathf.Sqrt((vx - cx) * (vx - cx) + (vy - cy) * (vy - cy));
                    float glowRad = 60f;
                    if (d < glowRad)
                    {
                        // 0%=glow@0.9, 40%=light@0.55, 100%=base@0
                        float t = d / glowRad;
                        Color gCol; float gAlpha;
                        if (t < 0.40f)
                        {
                            float k = t / 0.40f;
                            gCol = Color.Lerp(glow, light, k);
                            gAlpha = Mathf.Lerp(0.90f, 0.55f, k);
                        }
                        else
                        {
                            float k = (t - 0.40f) / 0.60f;
                            gCol = Color.Lerp(light, baseC, k);
                            gAlpha = Mathf.Lerp(0.55f, 0f, k);
                        }
                        baseStone = AlphaBlend(baseStone, gCol, gAlpha);
                    }

                    // Stone texture specks (tiny soft circles)
                    baseStone = AddSpeck(vx, vy, 60f, 70f, 1.5f, baseStone, stoneTop, 0.5f);
                    baseStone = AddSpeck(vx, vy, 140f, 120f, 1.2f, baseStone, stoneTop, 0.5f);
                    baseStone = AddSpeck(vx, vy, 76f, 140f, 1.0f, baseStone, stoneTop, 0.6f);
                    baseStone = AddSpeck(vx, vy, 142f, 76f, 1.3f, baseStone, stoneTop, 0.5f);

                    px[ty * size + tx] = ToColor32(baseStone);
                }
            }

            // ─── Stroke the outer hex (thick) ────────────────────────────
            StrokePolyline(px, size, scale, hexOuter, 3f, stroke, closed: true);

            // ─── Inner inset hex outline (faint stoneTop) ────────────────
            StrokePolyline(px, size, scale, hexInner, 1.2f, Color.Lerp(stoneTop, baseC, 0f), closed: true, alpha: 0.7f);

            // ─── Rim highlight on top edge: V2 → V1 (upper edge) ─────────
            StrokeSegment(px, size, scale, hexOuter[2], hexOuter[1], 3f, stoneTop, alpha: 0.9f);

            // ─── Draw the rune (3-pass: blurred underglow, crisp body, thin top) ──
            DrawRune(px, size, scale, meta.color, glow, light);

            tex.SetPixels32(px);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spritePixelsPerUnit = 256f;     // 1 unit ≈ 1 sprite at scale=1
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.SaveAndReimport();
        }

        private static Color SampleStoneGradient(float t, Color top, Color mid, Color bot)
        {
            if (t < 0.5f) return Color.Lerp(top, mid, t / 0.5f);
            return Color.Lerp(mid, bot, (t - 0.5f) / 0.5f);
        }

        private static Color AddSpeck(float vx, float vy, float sx, float sy, float radius,
            Color baseC, Color speckColor, float alpha)
        {
            float dx = vx - sx, dy = vy - sy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > radius + 0.5f) return baseC;
            float a = Mathf.SmoothStep(1f, 0f, d / (radius + 0.5f)) * alpha;
            return AlphaBlend(baseC, speckColor, a);
        }

        // ─── Rune drawing ───────────────────────────────────────────────
        private static void DrawRune(Color32[] px, int size, float scale,
            RuneColor color, Color glow, Color light)
        {
            // Build polyline strokes for the rune (mirrors the SVG path commands)
            var strokes = GetRuneStrokes(color);

            // 3-pass to mimic SVG: 1) wide blurred glow  2) crisp light body  3) thin highlight
            foreach (var s in strokes)
            {
                StrokePolyline(px, size, scale, s, 7f, glow, closed: false, alpha: 0.45f, blur: 3f);
            }
            foreach (var s in strokes)
            {
                StrokePolyline(px, size, scale, s, 4f, light, closed: false, alpha: 1.0f);
            }
            foreach (var s in strokes)
            {
                StrokePolyline(px, size, scale, s, 1.5f, glow, closed: false, alpha: 0.9f);
            }
        }

        // Returns one or more polylines that form the rune for the given color.
        // Vertices are in viewBox space (0..200).
        private static Vector2[][] GetRuneStrokes(RuneColor color)
        {
            switch (color)
            {
                case RuneColor.Ruby:
                    // Triangle: outer + inner concentric
                    return new[] {
                        ClosedPoly(new Vector2(100,40), new Vector2(60,150), new Vector2(140,150)),
                        ClosedPoly(new Vector2(100,60), new Vector2(78,144), new Vector2(122,144)),
                    };
                case RuneColor.Amber:
                    // Rhombus (diamond): outer + inner
                    return new[] {
                        ClosedPoly(new Vector2(100,38), new Vector2(150,100), new Vector2(100,162), new Vector2(50,100)),
                        ClosedPoly(new Vector2(100,60), new Vector2(130,100), new Vector2(100,140), new Vector2(70,100)),
                    };
                case RuneColor.Citrine:
                    // 5-point star
                    return new[] {
                        ClosedPoly(
                            new Vector2(100,38),
                            new Vector2(113,88),
                            new Vector2(165,88),
                            new Vector2(122,118),
                            new Vector2(138,168),
                            new Vector2(100,138),
                            new Vector2(62,168),
                            new Vector2(78,118),
                            new Vector2(35,88),
                            new Vector2(87,88)),
                    };
                case RuneColor.Emerald:
                    // Leaf outline + center vein + side veins
                    return new[] {
                        // Leaf outline (approximated with arcs as polyline)
                        ApproxLeaf(),
                        // Center vein
                        Open(new Vector2(100,56), new Vector2(100,158)),
                        // Side veins (4)
                        Open(new Vector2(76,90), new Vector2(88,100), new Vector2(100,100)),
                        Open(new Vector2(124,90), new Vector2(112,100), new Vector2(100,100)),
                        Open(new Vector2(68,120), new Vector2(86,130), new Vector2(100,130)),
                        Open(new Vector2(132,120), new Vector2(114,130), new Vector2(100,130)),
                    };
                case RuneColor.Sapphire:
                    // Water drop outline
                    return new[] { ApproxDrop() };
                case RuneColor.Amethyst:
                    // 4 concentric circles
                    return new[] {
                        Circle(100, 100, 50),
                        Circle(100, 100, 35),
                        Circle(100, 100, 20),
                        Circle(100, 100,  8),
                    };
            }
            return new Vector2[0][];
        }

        private static Vector2[] ClosedPoly(params Vector2[] pts)
        {
            var r = new Vector2[pts.Length + 1];
            for (int i = 0; i < pts.Length; i++) r[i] = pts[i];
            r[pts.Length] = pts[0];
            return r;
        }
        private static Vector2[] Open(params Vector2[] pts) => pts;

        private static Vector2[] Circle(float cx, float cy, float r, int seg = 32)
        {
            var pts = new Vector2[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                pts[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
            }
            return pts;
        }

        private static Vector2[] ApproxLeaf()
        {
            // Mirror the SVG: M100 40 Q60 70 60 110 Q60 150 100 162 Q140 150 140 110 Q140 70 100 40 Z
            var pts = new List<Vector2>();
            BezierQuadratic(pts, new Vector2(100,40),  new Vector2(60,70),   new Vector2(60,110), 16);
            BezierQuadratic(pts, new Vector2(60,110),  new Vector2(60,150),  new Vector2(100,162), 16);
            BezierQuadratic(pts, new Vector2(100,162), new Vector2(140,150), new Vector2(140,110), 16);
            BezierQuadratic(pts, new Vector2(140,110), new Vector2(140,70),  new Vector2(100,40), 16);
            return pts.ToArray();
        }
        private static Vector2[] ApproxDrop()
        {
            // M100 38 Q70 90 70 122 Q70 156 100 162 Q130 156 130 122 Q130 90 100 38 Z
            var pts = new List<Vector2>();
            BezierQuadratic(pts, new Vector2(100,38),  new Vector2(70,90),   new Vector2(70,122), 18);
            BezierQuadratic(pts, new Vector2(70,122),  new Vector2(70,156),  new Vector2(100,162), 14);
            BezierQuadratic(pts, new Vector2(100,162), new Vector2(130,156), new Vector2(130,122), 14);
            BezierQuadratic(pts, new Vector2(130,122), new Vector2(130,90),  new Vector2(100,38), 18);
            return pts.ToArray();
        }

        private static void BezierQuadratic(List<Vector2> outPts, Vector2 a, Vector2 b, Vector2 c, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                float omt = 1f - t;
                outPts.Add(omt*omt*a + 2f*omt*t*b + t*t*c);
            }
        }

        // ─── Hex geometry ──────────────────────────────────────────────
        private static Vector2[] HexVerts(float cx, float cy, float r)
        {
            var v = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                v[i] = new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a));
            }
            return v;
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

        // ─── Stroking ─────────────────────────────────────────────────
        private static void StrokePolyline(Color32[] px, int size, float scale,
            Vector2[] verts, float widthSvg, Color color, bool closed,
            float alpha = 1f, float blur = 0f)
        {
            int n = verts.Length;
            int last = closed ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                var a = verts[i];
                var b = verts[(i + 1) % n];
                StrokeSegment(px, size, scale, a, b, widthSvg, color, alpha, blur);
            }
        }

        private static void StrokeSegment(Color32[] px, int size, float scale,
            Vector2 a, Vector2 b, float widthSvg, Color color,
            float alpha = 1f, float blur = 0f)
        {
            // Convert to texture space; remember Y flip
            float half = widthSvg * 0.5f;
            float minX = Mathf.Min(a.x, b.x) - half - blur - 1f;
            float maxX = Mathf.Max(a.x, b.x) + half + blur + 1f;
            float minY = Mathf.Min(a.y, b.y) - half - blur - 1f;
            float maxY = Mathf.Max(a.y, b.y) + half + blur + 1f;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(minX * scale));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(maxX * scale));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(minY * scale));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(maxY * scale));

            Vector2 ab = b - a;
            float ab2 = Vector2.Dot(ab, ab);
            if (ab2 < 0.0001f) return;

            for (int sy = y0; sy <= y1; sy++)
            {
                int ty = size - 1 - sy;
                float vy = sy / scale;
                for (int x = x0; x <= x1; x++)
                {
                    float vx = x / scale;
                    var p = new Vector2(vx, vy);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2);
                    Vector2 q = a + ab * t;
                    float d = (p - q).magnitude;

                    float coverage;
                    if (blur <= 0f)
                    {
                        // Hard stroke with 1px AA
                        if (d > half + 0.5f) continue;
                        coverage = Mathf.Clamp01(half + 0.5f - d);
                    }
                    else
                    {
                        if (d > half + blur) continue;
                        if (d <= half) coverage = 1f;
                        else
                        {
                            float k = (d - half) / blur;
                            coverage = Mathf.Pow(1f - k, 2f); // soft falloff
                        }
                    }

                    int idx = ty * size + x;
                    Color dst = ((Color)px[idx]);
                    dst = AlphaBlend(dst, color, coverage * alpha);
                    px[idx] = ToColor32(dst);
                }
            }
        }

        // ─── Color helpers ─────────────────────────────────────────────
        private static Color AlphaBlend(Color dst, Color src, float a)
        {
            a *= src.a;
            if (a <= 0f) return dst;
            float outA = a + dst.a * (1f - a);
            if (outA <= 0f) return new Color(0, 0, 0, 0);
            var rgb = (src * a + dst * dst.a * (1f - a)) / outA;
            return new Color(rgb.r, rgb.g, rgb.b, outA);
        }

        private static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255));
        }

        // ─── OKLCH → sRGB (approximation good enough for sprite generation) ──
        private static Color OklchToRgb(float L, float C, float hueDeg)
        {
            float h = hueDeg * Mathf.Deg2Rad;
            float a = C * Mathf.Cos(h);
            float b = C * Mathf.Sin(h);
            // Oklab → linear sRGB (Björn Ottosson)
            float l_ = L + 0.3963377774f * a + 0.2158037573f * b;
            float m_ = L - 0.1055613458f * a - 0.0638541728f * b;
            float s_ = L - 0.0894841775f * a - 1.2914855480f * b;
            float l3 = l_ * l_ * l_;
            float m3 = m_ * m_ * m_;
            float s3 = s_ * s_ * s_;
            float rL =  4.0767416621f * l3 - 3.3077115913f * m3 + 0.2309699292f * s3;
            float gL = -1.2684380046f * l3 + 2.6097574011f * m3 - 0.3413193965f * s3;
            float bL = -0.0041960863f * l3 - 0.7034186147f * m3 + 1.7076147010f * s3;
            return new Color(LinearToSrgb(rL), LinearToSrgb(gL), LinearToSrgb(bL), 1f);
        }

        private static float LinearToSrgb(float x)
        {
            x = Mathf.Clamp01(x);
            return x <= 0.0031308f ? 12.92f * x : 1.055f * Mathf.Pow(x, 1f / 2.4f) - 0.055f;
        }

        // ─── Utils ─────────────────────────────────────────────────────
        private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif

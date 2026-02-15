using UnityEngine;
using UnityEditor;
using System.IO;

namespace JewelsHexaPuzzle.Editor
{
    public class IconPreviewGenerator : EditorWindow
    {
        private const int SIZE = 128;
        private const string OUTPUT_DIR = "Assets/IconPreviews";

        [MenuItem("Tools/Generate Icon Previews")]
        public static void Generate()
        {
            if (!Directory.Exists(OUTPUT_DIR))
                Directory.CreateDirectory(OUTPUT_DIR);

            // Bomb variants
            SaveIcon(GenerateBomb_Macaron(), "Bomb_1_MacaronBomb");
            SaveIcon(GenerateBomb_CherryBom(), "Bomb_2_CherryBom");
            SaveIcon(GenerateBomb_FluffyBomb(), "Bomb_3_FluffyBomb");
            SaveIcon(GenerateBomb_CreamPuff(), "Bomb_4_CreamPuff");
            SaveIcon(GenerateBomb_StrawberryCakePop(), "Bomb_5_StrawberryCakePop");

            // Donut variants
            SaveIcon(GenerateDonut_GlazedDonut(), "Donut_1_GlazedDonut");
            SaveIcon(GenerateDonut_MacaronRing(), "Donut_2_MacaronRing");
            SaveIcon(GenerateDonut_FlowerDonut(), "Donut_3_FlowerDonut");
            SaveIcon(GenerateDonut_KawaiiDonut(), "Donut_4_KawaiiDonut");
            SaveIcon(GenerateDonut_JewelRosette(), "Donut_5_JewelRosette");

            // XBlock variants
            SaveIcon(GenerateX_RibbonCross(), "XBlock_1_RibbonCross");
            SaveIcon(GenerateX_StarCandy(), "XBlock_2_StarCandy");
            SaveIcon(GenerateX_CookieX(), "XBlock_3_CookieX");
            SaveIcon(GenerateX_SnowCrystal(), "XBlock_4_SnowCrystal");
            SaveIcon(GenerateX_FlowerPinwheel(), "XBlock_5_FlowerPinwheel");

            // Laser variants
            SaveIcon(GenerateLaser_SparkleStar(), "Laser_1_SparkleStar");
            SaveIcon(GenerateLaser_CreamSunburst(), "Laser_2_CreamSunburst");
            SaveIcon(GenerateLaser_MagicWand(), "Laser_3_MagicWand");
            SaveIcon(GenerateLaser_DiamondPrism(), "Laser_4_DiamondPrism");
            SaveIcon(GenerateLaser_SnowflakeLaser(), "Laser_5_SnowflakeLaser");

            // Drill variants
            SaveIcon(GenerateDrill_LollipopArrow(), "Drill_1_LollipopArrow");
            SaveIcon(GenerateDrill_MacaronPierce(), "Drill_2_MacaronPierce");
            SaveIcon(GenerateDrill_IceCreamDrill(), "Drill_3_IceCreamDrill");
            SaveIcon(GenerateDrill_KawaiiArrow(), "Drill_4_KawaiiArrow");
            SaveIcon(GenerateDrill_PeperoStick(), "Drill_5_PeperoStick");

            AssetDatabase.Refresh();
            Debug.Log($"[IconPreviewGenerator] 25 icon previews saved to {OUTPUT_DIR}/");
            EditorUtility.DisplayDialog("Icon Preview Generator",
                $"25 icons generated!\nCheck {OUTPUT_DIR}/ folder.", "OK");
        }

        private static void SaveIcon(Texture2D tex, string name)
        {
            byte[] png = tex.EncodeToPNG();
            string path = $"{OUTPUT_DIR}/{name}.png";
            File.WriteAllBytes(path, png);
            Object.DestroyImmediate(tex);
        }

        private static Color[] InitPixels()
        {
            Color[] px = new Color[SIZE * SIZE];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
            return px;
        }

        private static Texture2D Finalize(Color[] px)
        {
            Texture2D tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        private static Vector2 Center => new Vector2(SIZE * 0.5f, SIZE * 0.5f);

        private static float Dist(Vector2 a, Vector2 b) => Vector2.Distance(a, b);
        private static float Dist(float x, float y, Vector2 c) => Dist(new Vector2(x, y), c);

        private static float SmoothEdge(float dist, float radius, float width = 1.5f)
        {
            return Mathf.Clamp01((radius - dist) / width);
        }

        private static void DrawCircle(Color[] px, Vector2 center, float radius, Color color, float aa = 1.5f)
        {
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float d = Dist(x, y, center);
                    if (d < radius + aa)
                    {
                        float a = SmoothEdge(d, radius, aa);
                        Color src = px[y * SIZE + x];
                        Color c = color;
                        c.a *= a;
                        px[y * SIZE + x] = BlendOver(src, c);
                    }
                }
        }

        private static void DrawRing(Color[] px, Vector2 center, float innerR, float outerR, Color color, float aa = 1.5f)
        {
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float d = Dist(x, y, center);
                    if (d >= innerR - aa && d <= outerR + aa)
                    {
                        float aOuter = SmoothEdge(d, outerR, aa);
                        float aInner = SmoothEdge(innerR, d, aa);
                        float a = aOuter * aInner;
                        Color src = px[y * SIZE + x];
                        Color c = color;
                        c.a *= a;
                        px[y * SIZE + x] = BlendOver(src, c);
                    }
                }
        }

        private static void DrawLine(Color[] px, Vector2 from, Vector2 to, float width, Color color, float aa = 1.0f)
        {
            Vector2 dir = (to - from).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            float len = Vector2.Distance(from, to);

            int minX = Mathf.Max(0, (int)(Mathf.Min(from.x, to.x) - width - 2));
            int maxX = Mathf.Min(SIZE - 1, (int)(Mathf.Max(from.x, to.x) + width + 2));
            int minY = Mathf.Max(0, (int)(Mathf.Min(from.y, to.y) - width - 2));
            int maxY = Mathf.Min(SIZE - 1, (int)(Mathf.Max(from.y, to.y) + width + 2));

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 p = new Vector2(x, y) - from;
                    float along = Vector2.Dot(p, dir);
                    float across = Mathf.Abs(Vector2.Dot(p, perp));

                    if (along >= -aa && along <= len + aa && across <= width + aa)
                    {
                        float aEdge = SmoothEdge(across, width, aa);
                        float aTip1 = Mathf.Clamp01((along + aa) / (aa * 2));
                        float aTip2 = Mathf.Clamp01((len + aa - along) / (aa * 2));
                        float a = aEdge * aTip1 * aTip2;

                        Color src = px[y * SIZE + x];
                        Color c = color;
                        c.a *= a;
                        px[y * SIZE + x] = BlendOver(src, c);
                    }
                }
        }

        private static Color BlendOver(Color dst, Color src)
        {
            float sa = src.a;
            float da = dst.a;
            float oa = sa + da * (1f - sa);
            if (oa < 0.001f) return Color.clear;
            return new Color(
                (src.r * sa + dst.r * da * (1f - sa)) / oa,
                (src.g * sa + dst.g * da * (1f - sa)) / oa,
                (src.b * sa + dst.b * da * (1f - sa)) / oa,
                oa
            );
        }

        private static Color Pastel(float h, float s = 0.35f, float v = 0.95f)
        {
            return Color.HSVToRGB(h, s, v);
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(
                Mathf.Min(1f, c.r + amount),
                Mathf.Min(1f, c.g + amount),
                Mathf.Min(1f, c.b + amount),
                c.a);
        }

        private static Color Darken(Color c, float amount)
        {
            return new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);
        }

        // ==================================================================
        // BOMB VARIANTS
        // ==================================================================

        // 1. Macaron Bomb - round macaron shape with fuse
        private static Texture2D GenerateBomb_Macaron()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color body = new Color(0.95f, 0.72f, 0.75f); // soft coral pink
            Color cream = new Color(1f, 0.95f, 0.88f);    // cream filling
            Color dark = Darken(body, 0.15f);

            // Bottom half
            DrawCircle(px, c + new Vector2(0, -4), 36, dark);
            // Cream layer (middle)
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float d = Dist(x, y, c);
                    if (d < 38 && Mathf.Abs(y - c.y + 2) < 5)
                    {
                        float a = SmoothEdge(Mathf.Abs(y - c.y + 2), 5f) * SmoothEdge(d, 38f);
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(cream, a * 0.9f));
                    }
                }
            // Top half (lighter)
            DrawCircle(px, c + new Vector2(0, 4), 34, Lighten(body, 0.06f));
            // Highlight
            DrawCircle(px, c + new Vector2(-10, 12), 12, WithAlpha(Color.white, 0.3f));
            // Fuse
            DrawLine(px, c + new Vector2(5, 34), c + new Vector2(12, 52), 2.5f, new Color(0.82f, 0.70f, 0.58f));
            // Spark
            DrawCircle(px, c + new Vector2(14, 54), 5, WithAlpha(new Color(1f, 0.95f, 0.7f), 0.9f));
            DrawCircle(px, c + new Vector2(14, 54), 3, WithAlpha(Color.white, 0.7f));
            return Finalize(px);
        }

        // 2. Cherry Bom - cherry shape
        private static Texture2D GenerateBomb_CherryBom()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color cherry = new Color(0.92f, 0.45f, 0.50f); // cherry red pastel

            // Two cherry bodies
            DrawCircle(px, c + new Vector2(-14, -8), 26, Darken(cherry, 0.1f));
            DrawCircle(px, c + new Vector2(-14, -8), 24, cherry);
            DrawCircle(px, c + new Vector2(-14, -8) + new Vector2(-6, 8), 8, WithAlpha(Lighten(cherry, 0.2f), 0.5f));

            DrawCircle(px, c + new Vector2(14, -8), 26, Darken(cherry, 0.1f));
            DrawCircle(px, c + new Vector2(14, -8), 24, cherry);
            DrawCircle(px, c + new Vector2(14, -8) + new Vector2(-6, 8), 8, WithAlpha(Lighten(cherry, 0.2f), 0.5f));

            // Stems
            Color stem = new Color(0.55f, 0.78f, 0.55f);
            DrawLine(px, c + new Vector2(-14, 16), c + new Vector2(-2, 42), 2.5f, stem);
            DrawLine(px, c + new Vector2(14, 16), c + new Vector2(2, 42), 2.5f, stem);

            // Leaf
            Color leaf = new Color(0.65f, 0.88f, 0.60f);
            DrawCircle(px, c + new Vector2(6, 42), 8, leaf);
            DrawCircle(px, c + new Vector2(12, 44), 6, Lighten(leaf, 0.1f));

            // Spark on one cherry
            DrawCircle(px, c + new Vector2(-14, 18), 4, WithAlpha(new Color(1f, 0.95f, 0.7f), 0.8f));
            return Finalize(px);
        }

        // 3. Fluffy Bomb - cloud-like puffy shape
        private static Texture2D GenerateBomb_FluffyBomb()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color puff = new Color(0.95f, 0.80f, 0.85f); // soft pink

            // Cloud bumps forming round bomb shape
            float[] offsets = { 0, 60, 120, 180, 240, 300 };
            foreach (float angle in offsets)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = c + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 18f;
                DrawCircle(px, pos, 24, puff);
            }
            // Center fill
            DrawCircle(px, c, 28, Lighten(puff, 0.05f));
            // Highlights on top bumps
            DrawCircle(px, c + new Vector2(-8, 16), 10, WithAlpha(Color.white, 0.25f));
            DrawCircle(px, c + new Vector2(8, 12), 8, WithAlpha(Color.white, 0.2f));
            // Fuse on top
            DrawLine(px, c + new Vector2(0, 38), c + new Vector2(5, 52), 2f, new Color(0.82f, 0.70f, 0.58f));
            DrawCircle(px, c + new Vector2(6, 54), 4, WithAlpha(new Color(1f, 0.92f, 0.7f), 0.9f));
            return Finalize(px);
        }

        // 4. Cream Puff Bomb
        private static Texture2D GenerateBomb_CreamPuff()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color top = new Color(0.85f, 0.65f, 0.45f); // golden brown pastry
            Color bottom = Darken(top, 0.1f);
            Color cream = new Color(1f, 0.97f, 0.88f);

            // Bottom pastry
            DrawCircle(px, c + new Vector2(0, -6), 32, bottom);
            // Cream overflow
            for (int angle = 0; angle < 360; angle += 40)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector2 pos = c + new Vector2(Mathf.Cos(rad) * 28, Mathf.Sin(rad) * 6 + 2);
                DrawCircle(px, pos, 8, cream);
            }
            DrawCircle(px, c + new Vector2(0, 2), 26, cream);
            // Top pastry
            DrawCircle(px, c + new Vector2(0, 8), 30, top);
            DrawCircle(px, c + new Vector2(-8, 16), 10, WithAlpha(Lighten(top, 0.2f), 0.5f));
            // Powder sugar dots
            DrawCircle(px, c + new Vector2(10, 14), 3, WithAlpha(Color.white, 0.4f));
            DrawCircle(px, c + new Vector2(-5, 20), 2, WithAlpha(Color.white, 0.3f));
            DrawCircle(px, c + new Vector2(15, 6), 2, WithAlpha(Color.white, 0.35f));
            // Fuse
            DrawLine(px, c + new Vector2(2, 36), c + new Vector2(8, 50), 2f, new Color(0.82f, 0.70f, 0.58f));
            DrawCircle(px, c + new Vector2(9, 52), 4, WithAlpha(new Color(1f, 0.92f, 0.7f), 0.9f));
            return Finalize(px);
        }

        // 5. Strawberry Cake Pop
        private static Texture2D GenerateBomb_StrawberryCakePop()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color berry = new Color(0.95f, 0.55f, 0.58f); // strawberry pink

            // Stick
            DrawLine(px, c + new Vector2(0, -40), c + new Vector2(0, -10), 3f, new Color(0.90f, 0.85f, 0.80f));

            // Strawberry body (slightly pointy at bottom)
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dy = y - c.y + 5;
                    float dx = x - c.x;
                    float bodyW = 30f - Mathf.Max(0, -dy * 0.4f);
                    if (dy < -30 || dy > 25) continue;
                    float d = Mathf.Sqrt(dx * dx / (bodyW * bodyW) + dy * dy / (28f * 28f));
                    if (d < 1f)
                    {
                        float a = SmoothEdge(d, 1f, 0.03f);
                        float hl = Mathf.Pow(Mathf.Max(0, 1f - d), 2f) * 0.15f;
                        Color bc = Lighten(berry, hl);
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(bc, a));
                    }
                }
            // Seeds
            Color seed = new Color(1f, 0.95f, 0.7f);
            DrawCircle(px, c + new Vector2(-10, 8), 2.5f, seed);
            DrawCircle(px, c + new Vector2(8, 4), 2.5f, seed);
            DrawCircle(px, c + new Vector2(-4, -6), 2.5f, seed);
            DrawCircle(px, c + new Vector2(12, -4), 2.5f, seed);
            DrawCircle(px, c + new Vector2(-12, -2), 2.5f, seed);
            // Leaves on top
            Color leaf = new Color(0.65f, 0.88f, 0.58f);
            DrawCircle(px, c + new Vector2(-8, 24), 7, leaf);
            DrawCircle(px, c + new Vector2(8, 24), 7, leaf);
            DrawCircle(px, c + new Vector2(0, 26), 6, Lighten(leaf, 0.1f));
            // Highlight
            DrawCircle(px, c + new Vector2(-8, 12), 8, WithAlpha(Color.white, 0.2f));
            return Finalize(px);
        }

        // ==================================================================
        // DONUT VARIANTS
        // ==================================================================

        // 1. Glazed Donut - classic with icing drips
        private static Texture2D GenerateDonut_GlazedDonut()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color dough = new Color(0.90f, 0.78f, 0.60f); // golden dough
            Color glaze = new Color(0.95f, 0.70f, 0.78f);  // pink glaze

            // Dough ring
            DrawRing(px, c, 18, 42, dough);
            // 3D shading on ring
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float d = Dist(x, y, c);
                    if (d >= 18 && d <= 42)
                    {
                        float ringMid = 30f;
                        float ringT = 1f - Mathf.Abs(d - ringMid) / 12f;
                        float hl = Mathf.Pow(ringT, 2f) * 0.15f;
                        Color cur = px[y * SIZE + x];
                        px[y * SIZE + x] = Lighten(cur, hl);
                    }
                }
            // Pink glaze on top half
            for (int y = (int)(c.y); y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float d = Dist(x, y, c);
                    if (d >= 16 && d <= 44)
                    {
                        // Drip effect
                        float dripOffset = Mathf.Sin(x * 0.15f) * 6f;
                        float glazeBottom = c.y + dripOffset - 3;
                        if (y >= glazeBottom)
                        {
                            float a = SmoothEdge(d, 44f) * SmoothEdge(16f, d);
                            float topFade = SmoothEdge(y - glazeBottom + 2, 4f);
                            px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(glaze, a * topFade * 0.85f));
                        }
                    }
                }
            // Sprinkles
            Color[] sprinkleColors = { new Color(0.95f, 0.65f, 0.65f), new Color(0.65f, 0.85f, 0.95f),
                                       new Color(0.95f, 0.92f, 0.65f), new Color(0.75f, 0.65f, 0.95f) };
            System.Random rng = new System.Random(42);
            for (int i = 0; i < 12; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                float r = 25f + (float)rng.NextDouble() * 12f;
                Vector2 sp = c + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r + 4);
                float sAngle = (float)rng.NextDouble() * 180f;
                Vector2 sDir = new Vector2(Mathf.Cos(sAngle * Mathf.Deg2Rad), Mathf.Sin(sAngle * Mathf.Deg2Rad));
                DrawLine(px, sp - sDir * 3, sp + sDir * 3, 1.5f, sprinkleColors[i % sprinkleColors.Length]);
            }
            return Finalize(px);
        }

        // 2. Rainbow Macaron Ring - ring of small macarons
        private static Texture2D GenerateDonut_MacaronRing()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            int count = 8;
            float ringRadius = 30f;

            for (int i = 0; i < count; i++)
            {
                float angle = (float)i / count * Mathf.PI * 2f;
                Vector2 pos = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                float hue = (float)i / count;
                Color macColor = Pastel(hue, 0.35f, 0.95f);
                // Mini macaron (two halves + cream)
                DrawCircle(px, pos + new Vector2(0, -2), 11, Darken(macColor, 0.08f));
                DrawCircle(px, pos + new Vector2(0, 2), 10, macColor);
                DrawCircle(px, pos + new Vector2(-3, 4), 4, WithAlpha(Color.white, 0.25f));
            }
            // Center sparkle
            DrawCircle(px, c, 8, WithAlpha(new Color(1f, 1f, 0.9f), 0.5f));
            DrawCircle(px, c, 4, WithAlpha(Color.white, 0.6f));
            return Finalize(px);
        }

        // 3. Flower Donut - flower-shaped ring
        private static Texture2D GenerateDonut_FlowerDonut()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            int petals = 6;
            Color petal = new Color(0.95f, 0.75f, 0.82f);   // soft pink
            Color petalDark = Darken(petal, 0.12f);
            Color center = new Color(1f, 0.95f, 0.70f);     // yellow center

            for (int i = 0; i < petals; i++)
            {
                float angle = (float)i / petals * Mathf.PI * 2f + 0.3f;
                Vector2 pos = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 24f;
                DrawCircle(px, pos, 18, petalDark);
                DrawCircle(px, pos, 16, petal);
                // Petal highlight
                Vector2 hlPos = pos + new Vector2(-3, 4);
                DrawCircle(px, hlPos, 6, WithAlpha(Lighten(petal, 0.15f), 0.5f));
            }
            // Center
            DrawCircle(px, c, 14, center);
            DrawCircle(px, c, 12, Lighten(center, 0.1f));
            DrawCircle(px, c + new Vector2(-3, 4), 5, WithAlpha(Color.white, 0.3f));
            // Center hole
            DrawCircle(px, c, 6, WithAlpha(new Color(0.9f, 0.85f, 0.6f), 0.8f));
            return Finalize(px);
        }

        // 4. Kawaii Donut - cute face donut
        private static Texture2D GenerateDonut_KawaiiDonut()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color dough = new Color(0.92f, 0.80f, 0.62f);
            Color glaze = new Color(0.88f, 0.70f, 0.90f); // purple glaze

            // Ring body
            DrawRing(px, c, 16, 42, dough);
            // Glaze top
            DrawRing(px, c + new Vector2(0, 4), 14, 44, WithAlpha(glaze, 0.8f));
            // Eyes (on the ring, upper portion)
            Color eye = new Color(0.2f, 0.2f, 0.25f);
            DrawCircle(px, c + new Vector2(-12, 8), 4, eye);
            DrawCircle(px, c + new Vector2(12, 8), 4, eye);
            // Eye highlights
            DrawCircle(px, c + new Vector2(-13, 10), 1.5f, Color.white);
            DrawCircle(px, c + new Vector2(11, 10), 1.5f, Color.white);
            // Blush
            DrawCircle(px, c + new Vector2(-20, 2), 5, WithAlpha(new Color(0.95f, 0.60f, 0.62f), 0.35f));
            DrawCircle(px, c + new Vector2(20, 2), 5, WithAlpha(new Color(0.95f, 0.60f, 0.62f), 0.35f));
            // Smile
            for (int x = -6; x <= 6; x++)
            {
                float sy = -Mathf.Sqrt(36 - x * x) * 0.5f;
                DrawCircle(px, c + new Vector2(x, sy + 1), 1f, WithAlpha(eye, 0.8f));
            }
            return Finalize(px);
        }

        // 5. Jewel Rosette - decorative jeweled ring
        private static Texture2D GenerateDonut_JewelRosette()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color gold = new Color(0.95f, 0.88f, 0.65f);
            Color goldDark = Darken(gold, 0.15f);

            // Gold ring base
            DrawRing(px, c, 20, 40, goldDark);
            DrawRing(px, c, 22, 38, gold);
            // Jewels around the ring
            Color[] jewels = { new Color(0.85f, 0.50f, 0.55f), new Color(0.50f, 0.75f, 0.90f),
                               new Color(0.70f, 0.55f, 0.88f), new Color(0.55f, 0.88f, 0.65f),
                               new Color(0.95f, 0.80f, 0.50f), new Color(0.88f, 0.55f, 0.75f) };
            for (int i = 0; i < 6; i++)
            {
                float angle = (float)i / 6 * Mathf.PI * 2f;
                Vector2 pos = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 30f;
                DrawCircle(px, pos, 7, Darken(jewels[i], 0.1f));
                DrawCircle(px, pos, 6, jewels[i]);
                DrawCircle(px, pos + new Vector2(-1, 2), 2.5f, WithAlpha(Lighten(jewels[i], 0.3f), 0.6f));
            }
            // Center glow
            DrawCircle(px, c, 10, WithAlpha(new Color(1f, 0.98f, 0.85f), 0.4f));
            return Finalize(px);
        }

        // ==================================================================
        // X BLOCK VARIANTS
        // ==================================================================

        // 1. Ribbon Cross - bow/ribbon shape
        private static Texture2D GenerateX_RibbonCross()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color ribbon = new Color(0.92f, 0.65f, 0.72f); // pink ribbon
            Color ribbonLight = Lighten(ribbon, 0.12f);

            // Four ribbon loops
            float armLen = 28;
            float armW = 14;
            float[] angles = { 45, 135, 225, 315 };
            foreach (float aDeg in angles)
            {
                float aRad = aDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(aRad), Mathf.Sin(aRad));
                Vector2 tip = c + dir * armLen;
                // Draw tapered ribbon arm
                for (int y = 0; y < SIZE; y++)
                    for (int x = 0; x < SIZE; x++)
                    {
                        Vector2 p = new Vector2(x, y) - c;
                        float along = Vector2.Dot(p, dir);
                        if (along < 0 || along > armLen) continue;
                        float t = along / armLen;
                        float width = armW * (0.3f + 0.7f * Mathf.Sin(t * Mathf.PI));
                        Vector2 perp = new Vector2(-dir.y, dir.x);
                        float across = Mathf.Abs(Vector2.Dot(p, perp));
                        if (across < width)
                        {
                            float a = SmoothEdge(across, width);
                            float hl = Mathf.Pow(1f - across / width, 2f) * 0.15f;
                            Color col = Lighten(ribbon, hl);
                            px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(col, a));
                        }
                    }
            }
            // Center knot
            DrawCircle(px, c, 10, Darken(ribbon, 0.08f));
            DrawCircle(px, c, 8, ribbonLight);
            DrawCircle(px, c + new Vector2(-2, 3), 3, WithAlpha(Color.white, 0.3f));
            return Finalize(px);
        }

        // 2. Star Candy - 4-pointed star
        private static Texture2D GenerateX_StarCandy()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color candy = new Color(0.88f, 0.78f, 0.95f); // lavender

            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    Vector2 p = new Vector2(x, y) - c;
                    // 4-pointed star using min of two rotated diamond shapes
                    float d1 = Mathf.Abs(p.x) + Mathf.Abs(p.y); // diamond
                    float p2x = (p.x + p.y) * 0.707f;
                    float p2y = (-p.x + p.y) * 0.707f;
                    float d2 = Mathf.Abs(p2x) + Mathf.Abs(p2y); // rotated diamond
                    float d = Mathf.Min(d1 * 0.7f, d2);
                    float radius = 38f;

                    if (d < radius)
                    {
                        float t = d / radius;
                        float a = SmoothEdge(d, radius, 2f);
                        float hl = Mathf.Pow(1f - t, 2f) * 0.2f;
                        Color col = Lighten(candy, hl);
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(col, a));
                    }
                }
            // Center gem
            DrawCircle(px, c, 10, Lighten(candy, 0.1f));
            DrawCircle(px, c + new Vector2(-2, 3), 4, WithAlpha(Color.white, 0.4f));
            return Finalize(px);
        }

        // 3. Kawaii Cookie X - cute X-shaped cookie
        private static Texture2D GenerateX_CookieX()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color cookie = new Color(0.92f, 0.82f, 0.62f); // golden cookie
            Color icing = new Color(0.95f, 0.75f, 0.80f);   // pink icing

            // X shape with rounded ends
            float width = 16;
            float len = 34;
            DrawLine(px, c + new Vector2(-len, -len) * 0.707f, c + new Vector2(len, len) * 0.707f, width, cookie);
            DrawLine(px, c + new Vector2(len, -len) * 0.707f, c + new Vector2(-len, len) * 0.707f, width, cookie);
            // Round ends
            float[] endAngles = { 45, 135, 225, 315 };
            foreach (float a in endAngles)
            {
                float rad = a * Mathf.Deg2Rad;
                Vector2 pos = c + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * len * 0.707f;
                DrawCircle(px, pos, width * 0.6f, cookie);
            }
            // Center circle (thicker junction)
            DrawCircle(px, c, width * 0.7f, Lighten(cookie, 0.05f));
            // Icing zigzag across X arms
            for (int i = -3; i <= 3; i++)
            {
                float t = (i + 3f) / 6f;
                Vector2 pos1 = Vector2.Lerp(c + new Vector2(-len, -len) * 0.5f, c + new Vector2(len, len) * 0.5f, t);
                float offset = (i % 2 == 0) ? 4 : -4;
                Vector2 perpDir = new Vector2(0.707f, -0.707f);
                DrawCircle(px, pos1 + perpDir * offset, 3, icing);
            }
            // Eyes on center
            DrawCircle(px, c + new Vector2(-5, 3), 2.5f, new Color(0.25f, 0.22f, 0.20f));
            DrawCircle(px, c + new Vector2(5, 3), 2.5f, new Color(0.25f, 0.22f, 0.20f));
            return Finalize(px);
        }

        // 4. Snow Crystal - snowflake
        private static Texture2D GenerateX_SnowCrystal()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color ice = new Color(0.80f, 0.90f, 0.98f); // icy blue

            // 6 arms
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tip = c + dir * 40f;
                DrawLine(px, c, tip, 3f, ice);

                // Side branches
                for (int b = 1; b <= 2; b++)
                {
                    float bt = b * 0.35f;
                    Vector2 branchStart = c + dir * 40f * bt;
                    float branchAngle1 = angle + 60f * Mathf.Deg2Rad;
                    float branchAngle2 = angle - 60f * Mathf.Deg2Rad;
                    float branchLen = 14f * (1f - bt * 0.3f);
                    DrawLine(px, branchStart, branchStart + new Vector2(Mathf.Cos(branchAngle1), Mathf.Sin(branchAngle1)) * branchLen, 2f, ice);
                    DrawLine(px, branchStart, branchStart + new Vector2(Mathf.Cos(branchAngle2), Mathf.Sin(branchAngle2)) * branchLen, 2f, ice);
                }
                // Tip dots
                DrawCircle(px, tip, 4, Lighten(ice, 0.1f));
            }
            // Center gem
            DrawCircle(px, c, 8, Lighten(ice, 0.1f));
            DrawCircle(px, c + new Vector2(-2, 2), 3, WithAlpha(Color.white, 0.5f));
            return Finalize(px);
        }

        // 5. Flower Pinwheel - spinning flower shape
        private static Texture2D GenerateX_FlowerPinwheel()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color[] petalColors = { new Color(0.95f, 0.72f, 0.75f), new Color(0.75f, 0.85f, 0.95f),
                                    new Color(0.95f, 0.90f, 0.72f), new Color(0.78f, 0.95f, 0.78f) };

            // 4 curved petals
            for (int i = 0; i < 4; i++)
            {
                float baseAngle = i * 90f + 10f; // slightly rotated for pinwheel feel
                Color petal = petalColors[i];

                for (int y = 0; y < SIZE; y++)
                    for (int x = 0; x < SIZE; x++)
                    {
                        Vector2 p = new Vector2(x, y) - c;
                        float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                        if (angle < 0) angle += 360;
                        float dist = p.magnitude;
                        if (dist > 42 || dist < 6) continue;

                        // Petal angular range (shifted slightly for curve)
                        float curveShift = (dist / 42f) * 15f; // more curve at tips
                        float relAngle = Mathf.DeltaAngle(angle, baseAngle + curveShift);
                        if (Mathf.Abs(relAngle) < 35f)
                        {
                            float angFade = 1f - Mathf.Abs(relAngle) / 35f;
                            float distFade = 1f - dist / 42f;
                            float a = angFade * Mathf.Min(1, dist / 10f);
                            float hl = distFade * angFade * 0.15f;
                            Color col = Lighten(petal, hl);
                            a *= SmoothEdge(dist, 42f, 2f);
                            if (a > 0.02f)
                                px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(col, a));
                        }
                    }
            }
            // Center button
            DrawCircle(px, c, 7, new Color(1f, 0.95f, 0.80f));
            DrawCircle(px, c + new Vector2(-1, 2), 3, WithAlpha(Color.white, 0.4f));
            return Finalize(px);
        }

        // ==================================================================
        // LASER VARIANTS
        // ==================================================================

        // 1. Sparkle Star - 6-pointed star with sparkles
        private static Texture2D GenerateLaser_SparkleStar()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color star = new Color(0.70f, 0.85f, 0.98f); // soft sky blue

            // 6-pointed star
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    Vector2 p = new Vector2(x, y) - c;
                    float maxR = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * 60f * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        float dot = Vector2.Dot(p.normalized, dir);
                        maxR = Mathf.Max(maxR, dot);
                    }
                    float radius = Mathf.Lerp(18f, 40f, Mathf.Pow(maxR, 3f));
                    float d = p.magnitude;
                    if (d < radius)
                    {
                        float t = d / radius;
                        float a = SmoothEdge(d, radius, 2f);
                        float hl = Mathf.Pow(1f - t, 2f) * 0.2f;
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(Lighten(star, hl), a));
                    }
                }
            // Sparkle dots at tips
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector2 tip = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 38f;
                DrawCircle(px, tip, 4, WithAlpha(Color.white, 0.6f));
            }
            // Center glow
            DrawCircle(px, c, 10, WithAlpha(Color.white, 0.4f));
            return Finalize(px);
        }

        // 2. Cream Sunburst - soft rays from center
        private static Texture2D GenerateLaser_CreamSunburst()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color ray = new Color(1f, 0.92f, 0.75f);     // warm cream
            Color center = new Color(1f, 0.85f, 0.65f);   // golden center

            // 3 axis rays (like laser: vertical, 30, -30)
            float[] rayAngles = { 90, 30, -30 };
            foreach (float aDeg in rayAngles)
            {
                float aRad = aDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(aRad), Mathf.Sin(aRad));
                // Both directions
                DrawLine(px, c - dir * 48, c + dir * 48, 6f, WithAlpha(ray, 0.7f));
                DrawLine(px, c - dir * 48, c + dir * 48, 3f, WithAlpha(Lighten(ray, 0.15f), 0.8f));
            }
            // Center circle
            DrawCircle(px, c, 14, center);
            DrawCircle(px, c, 10, Lighten(center, 0.15f));
            DrawCircle(px, c + new Vector2(-3, 4), 4, WithAlpha(Color.white, 0.4f));
            return Finalize(px);
        }

        // 3. Magic Wand Star - wand with star tip
        private static Texture2D GenerateLaser_MagicWand()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color wand = new Color(0.90f, 0.82f, 0.95f); // lavender wand
            Color star = new Color(1f, 0.92f, 0.65f);     // golden star

            // Wand stick (diagonal)
            DrawLine(px, c + new Vector2(-24, -32), c + new Vector2(8, 0), 3.5f, wand);
            DrawLine(px, c + new Vector2(-24, -32), c + new Vector2(8, 0), 1.5f, Lighten(wand, 0.15f));

            // Star at top
            Vector2 starC = c + new Vector2(12, 8);
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    Vector2 p = new Vector2(x, y) - starC;
                    float maxR = 0;
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = (i * 72f + 90f) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        float dot = Vector2.Dot(p.normalized, dir);
                        maxR = Mathf.Max(maxR, dot);
                    }
                    float radius = Mathf.Lerp(10f, 26f, Mathf.Pow(maxR, 3f));
                    float d = p.magnitude;
                    if (d < radius)
                    {
                        float a = SmoothEdge(d, radius, 2f);
                        float hl = Mathf.Pow(1f - d / radius, 2f) * 0.2f;
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(Lighten(star, hl), a));
                    }
                }
            // Sparkles around star
            DrawCircle(px, starC + new Vector2(20, 12), 3, WithAlpha(star, 0.6f));
            DrawCircle(px, starC + new Vector2(-10, 18), 2, WithAlpha(star, 0.5f));
            DrawCircle(px, starC + new Vector2(14, -14), 2.5f, WithAlpha(star, 0.55f));
            return Finalize(px);
        }

        // 4. Diamond Prism - hexagonal diamond
        private static Texture2D GenerateLaser_DiamondPrism()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color prism = new Color(0.75f, 0.88f, 0.98f); // ice blue

            // Hexagonal prism shape
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    Vector2 p = new Vector2(x, y) - c;
                    // Hex distance
                    float hexD = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = i * 60f * Mathf.Deg2Rad;
                        Vector2 axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        hexD = Mathf.Max(hexD, Mathf.Abs(Vector2.Dot(p, axis)));
                    }
                    float radius = 36;
                    if (hexD < radius)
                    {
                        float t = hexD / radius;
                        float a = SmoothEdge(hexD, radius, 2f);
                        // Faceted look - different brightness per facet
                        float facetAngle = Mathf.Atan2(p.y, p.x);
                        float facetBright = (Mathf.Sin(facetAngle * 3f) * 0.5f + 0.5f) * 0.12f;
                        float hl = Mathf.Pow(1f - t, 1.5f) * 0.15f + facetBright;
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(Lighten(prism, hl), a));
                    }
                }
            // Light rays from prism
            float[] rayAngles = { 90, 30, -30 };
            foreach (float aDeg in rayAngles)
            {
                float aRad = aDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(aRad), Mathf.Sin(aRad));
                DrawLine(px, c + dir * 36, c + dir * 52, 2f, WithAlpha(Lighten(prism, 0.15f), 0.6f));
                DrawLine(px, c - dir * 36, c - dir * 52, 2f, WithAlpha(Lighten(prism, 0.15f), 0.6f));
            }
            // Center highlight
            DrawCircle(px, c + new Vector2(-6, 8), 8, WithAlpha(Color.white, 0.3f));
            return Finalize(px);
        }

        // 5. Snowflake Laser - snowflake with laser beams
        private static Texture2D GenerateLaser_SnowflakeLaser()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color beam = new Color(0.65f, 0.82f, 0.98f);
            Color ice = new Color(0.85f, 0.92f, 0.98f);

            // 3 laser beams (matching hex axes)
            float[] angles = { 90, 30, -30 };
            foreach (float aDeg in angles)
            {
                float aRad = aDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(aRad), Mathf.Sin(aRad));
                DrawLine(px, c - dir * 50, c + dir * 50, 4f, WithAlpha(beam, 0.6f));
                DrawLine(px, c - dir * 50, c + dir * 50, 1.5f, WithAlpha(Color.white, 0.5f));
            }
            // Snowflake center
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(px, c, c + dir * 20, 2.5f, ice);
                // Side branches
                Vector2 mid = c + dir * 14;
                float ba1 = angle + 60f * Mathf.Deg2Rad;
                float ba2 = angle - 60f * Mathf.Deg2Rad;
                DrawLine(px, mid, mid + new Vector2(Mathf.Cos(ba1), Mathf.Sin(ba1)) * 8, 1.5f, ice);
                DrawLine(px, mid, mid + new Vector2(Mathf.Cos(ba2), Mathf.Sin(ba2)) * 8, 1.5f, ice);
            }
            DrawCircle(px, c, 6, Lighten(ice, 0.1f));
            DrawCircle(px, c, 3, WithAlpha(Color.white, 0.6f));
            return Finalize(px);
        }

        // ==================================================================
        // DRILL VARIANTS
        // ==================================================================

        // 1. Lollipop Arrow - candy lollipop with arrow direction
        private static Texture2D GenerateDrill_LollipopArrow()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color stick = new Color(0.92f, 0.88f, 0.82f);
            Color candy1 = new Color(0.95f, 0.65f, 0.70f); // pink
            Color candy2 = new Color(1f, 0.95f, 0.90f);     // cream

            // Stick (vertical)
            DrawLine(px, c + new Vector2(0, -42), c + new Vector2(0, 0), 3f, stick);
            // Arrow tip at bottom
            DrawLine(px, c + new Vector2(0, -42), c + new Vector2(-8, -30), 2.5f, stick);
            DrawLine(px, c + new Vector2(0, -42), c + new Vector2(8, -30), 2.5f, stick);
            // Arrow tip at top (opposite)
            DrawLine(px, c + new Vector2(0, 42), c + new Vector2(-8, 30), 2.5f, stick);
            DrawLine(px, c + new Vector2(0, 42), c + new Vector2(8, 30), 2.5f, stick);
            DrawLine(px, c + new Vector2(0, 0), c + new Vector2(0, 42), 3f, stick);

            // Lollipop circle at center
            DrawCircle(px, c, 20, candy1);
            // Spiral pattern
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float d = Dist(x, y, c);
                    if (d < 18)
                    {
                        float angle = Mathf.Atan2(y - c.y, x - c.x);
                        float spiral = Mathf.Sin(angle * 3f + d * 0.3f);
                        if (spiral > 0)
                            px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(candy2, 0.6f * SmoothEdge(d, 18f)));
                    }
                }
            DrawCircle(px, c + new Vector2(-5, 6), 6, WithAlpha(Color.white, 0.25f));
            return Finalize(px);
        }

        // 2. Macaron Pierce - macaron with arrow through it
        private static Texture2D GenerateDrill_MacaronPierce()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color mac = new Color(0.85f, 0.92f, 0.78f); // mint macaron
            Color cream = new Color(1f, 0.97f, 0.90f);
            Color arrow = new Color(0.92f, 0.72f, 0.75f); // pink arrow

            // Arrow shaft (vertical, goes through macaron)
            DrawLine(px, c + new Vector2(0, -44), c + new Vector2(0, 44), 2.5f, arrow);
            // Arrow tips
            DrawLine(px, c + new Vector2(0, 44), c + new Vector2(-6, 34), 2f, arrow);
            DrawLine(px, c + new Vector2(0, 44), c + new Vector2(6, 34), 2f, arrow);
            DrawLine(px, c + new Vector2(0, -44), c + new Vector2(-6, -34), 2f, arrow);
            DrawLine(px, c + new Vector2(0, -44), c + new Vector2(6, -34), 2f, arrow);

            // Macaron body (horizontal oriented)
            DrawCircle(px, c + new Vector2(0, -4), 22, Darken(mac, 0.06f)); // bottom
            // Cream filling
            for (int x2 = -20; x2 <= 20; x2++)
            {
                float w = Mathf.Sqrt(400 - x2 * x2);
                DrawCircle(px, c + new Vector2(x2, 0), 3f, cream);
            }
            DrawCircle(px, c + new Vector2(0, 4), 20, mac); // top
            DrawCircle(px, c + new Vector2(-6, 10), 7, WithAlpha(Lighten(mac, 0.15f), 0.4f));
            return Finalize(px);
        }

        // 3. Ice Cream Drill - ice cream cone pointing direction
        private static Texture2D GenerateDrill_IceCreamDrill()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color cone = new Color(0.90f, 0.78f, 0.55f);  // waffle cone
            Color scoop = new Color(0.95f, 0.78f, 0.82f);  // strawberry

            // Cone (triangle pointing down)
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dy = y - (c.y - 5);
                    if (dy > 0 || dy < -40) continue;
                    float width = (-dy / 40f) * 22f;
                    float dx = Mathf.Abs(x - c.x);
                    if (dx < width)
                    {
                        float a = SmoothEdge(dx, width, 1.5f);
                        // Waffle pattern
                        float pattern = (Mathf.Sin(x * 0.5f + y * 0.3f) * 0.5f + 0.5f) * 0.08f;
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(Lighten(cone, pattern), a));
                    }
                }
            // Cone also pointing up (double-ended drill)
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dy = y - (c.y + 5);
                    if (dy < 0 || dy > 40) continue;
                    float width = (dy / 40f) * 22f;
                    float dx = Mathf.Abs(x - c.x);
                    if (dx < width)
                    {
                        float a = SmoothEdge(dx, width, 1.5f);
                        float pattern = (Mathf.Sin(x * 0.5f - y * 0.3f) * 0.5f + 0.5f) * 0.08f;
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(Lighten(cone, pattern), a));
                    }
                }
            // Scoop at center
            DrawCircle(px, c, 18, scoop);
            DrawCircle(px, c + new Vector2(-5, 6), 6, WithAlpha(Lighten(scoop, 0.15f), 0.4f));
            // Sprinkles
            DrawCircle(px, c + new Vector2(8, 3), 2, new Color(0.70f, 0.85f, 0.95f));
            DrawCircle(px, c + new Vector2(-3, -6), 2, new Color(0.95f, 0.90f, 0.65f));
            DrawCircle(px, c + new Vector2(4, 10), 2, new Color(0.78f, 0.95f, 0.78f));
            return Finalize(px);
        }

        // 4. Kawaii Double Arrow - cute double-ended arrow
        private static Texture2D GenerateDrill_KawaiiArrow()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color body = new Color(0.82f, 0.90f, 0.98f); // soft blue
            Color tip = new Color(0.95f, 0.80f, 0.85f);   // pink tips

            // Arrow shaft
            DrawLine(px, c + new Vector2(0, -36), c + new Vector2(0, 36), 6f, body);
            DrawLine(px, c + new Vector2(0, -36), c + new Vector2(0, 36), 2.5f, Lighten(body, 0.12f));

            // Top arrowhead (triangle)
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dy = y - (c.y + 30);
                    if (dy < 0 || dy > 22) continue;
                    float width = (1f - dy / 22f) * 16f;
                    float dx = Mathf.Abs(x - c.x);
                    if (dx < width)
                    {
                        float a = SmoothEdge(dx, width, 1.5f);
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(tip, a));
                    }
                }
            // Bottom arrowhead (flipped)
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dy = (c.y - 30) - y;
                    if (dy < 0 || dy > 22) continue;
                    float width = (1f - dy / 22f) * 16f;
                    float dx = Mathf.Abs(x - c.x);
                    if (dx < width)
                    {
                        float a = SmoothEdge(dx, width, 1.5f);
                        px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(tip, a));
                    }
                }
            // Kawaii face at center
            DrawCircle(px, c, 10, Lighten(body, 0.08f));
            DrawCircle(px, c + new Vector2(-4, 2), 2, new Color(0.2f, 0.2f, 0.25f));
            DrawCircle(px, c + new Vector2(4, 2), 2, new Color(0.2f, 0.2f, 0.25f));
            // Blush
            DrawCircle(px, c + new Vector2(-8, -1), 3, WithAlpha(new Color(0.95f, 0.65f, 0.65f), 0.3f));
            DrawCircle(px, c + new Vector2(8, -1), 3, WithAlpha(new Color(0.95f, 0.65f, 0.65f), 0.3f));
            return Finalize(px);
        }

        // 5. Pepero Stick - chocolate-dipped stick
        private static Texture2D GenerateDrill_PeperoStick()
        {
            Color[] px = InitPixels();
            Vector2 c = Center;
            Color biscuit = new Color(0.95f, 0.88f, 0.72f); // biscuit color
            Color choco = new Color(0.70f, 0.48f, 0.40f);   // chocolate
            Color drizzle = new Color(1f, 0.85f, 0.88f);     // pink drizzle

            // Main stick (vertical)
            DrawLine(px, c + new Vector2(0, -48), c + new Vector2(0, 48), 7f, biscuit);
            DrawLine(px, c + new Vector2(-2, -48), c + new Vector2(-2, 48), 2f, Lighten(biscuit, 0.08f));

            // Chocolate dip top half
            for (int y = (int)(c.y - 5); y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    float dx = Mathf.Abs(x - c.x);
                    if (dx < 8)
                    {
                        // Chocolate with drip edge
                        float dripEdge = c.y - 5 + Mathf.Sin(x * 0.5f) * 4f;
                        if (y >= dripEdge)
                        {
                            float a = SmoothEdge(dx, 8f, 1f) * SmoothEdge(y - dripEdge + 2, 3f);
                            if (y < c.y + 48)
                                px[y * SIZE + x] = BlendOver(px[y * SIZE + x], WithAlpha(choco, a * 0.9f));
                        }
                    }
                }
            // Pink drizzle zigzag
            for (int i = 0; i < 8; i++)
            {
                float yy = c.y + i * 8f - 5;
                float xx = c.x + ((i % 2 == 0) ? 4 : -4);
                DrawCircle(px, new Vector2(xx, yy), 2f, drizzle);
            }
            // Highlight
            DrawLine(px, c + new Vector2(-4, 10), c + new Vector2(-4, 40), 1f, WithAlpha(Lighten(choco, 0.25f), 0.4f));
            return Finalize(px);
        }
    }
}

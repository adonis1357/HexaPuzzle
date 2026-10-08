using UnityEngine;
using Bow.Core;

namespace Bow.Art
{
    /// <summary>
    /// 런타임 프로시저럴 스프라이트/텍스처 생성 (외부 아트 에셋 없음).
    /// 생성 결과는 캐시되어 재사용된다.
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite whiteRect, circle64, softCircle32, ring256, stroke64, inkBlob, triangle32, button, enso;
        private static Sprite[] arrowByTeam = new Sprite[3];
        private static Material spriteMat;

        /// <summary>Sprites/Default 머티리얼 (SpriteRenderer, LineRenderer, Mesh 공용)</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMat == null)
                {
                    Shader sh = Shader.Find("Sprites/Default");
                    spriteMat = new Material(sh);
                }
                return spriteMat;
            }
        }

        private static Texture2D NewTex(int w, int h)
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        private static Sprite Make(Texture2D t, float ppu, Vector2 pivot)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, ppu, 0, SpriteMeshType.FullRect);
        }

        // ---------------------------------------------------------------
        // 기본 도형
        // ---------------------------------------------------------------

        /// <summary>16×16 흰 사각 (ppu 16 → 1×1m). 스케일로 크기 지정, color로 착색.</summary>
        public static Sprite WhiteRect()
        {
            if (whiteRect != null) return whiteRect;
            Texture2D t = NewTex(16, 16);
            Color[] px = new Color[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px); t.Apply();
            whiteRect = Make(t, 16f, new Vector2(0.5f, 0.5f));
            return whiteRect;
        }

        /// <summary>64×64 AA 원 (ppu 64 → 지름 1m)</summary>
        public static Sprite Circle()
        {
            if (circle64 != null) return circle64;
            circle64 = MakeDisc(64, 1f, 0f);
            return circle64;
        }

        /// <summary>32×32 소프트 원 (가장자리 부드러운 알파)</summary>
        public static Sprite SoftCircle()
        {
            if (softCircle32 != null) return softCircle32;
            softCircle32 = MakeDisc(32, 0.45f, 0.5f);
            return softCircle32;
        }

        private static Sprite MakeDisc(int size, float edgeStart, float softness)
        {
            Texture2D t = NewTex(size, size);
            Color[] px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / r; // 0 중심 ~ 1 가장자리
                    float a;
                    if (softness <= 0f) a = Mathf.Clamp01((1f - d) * r); // 1texel AA
                    else a = Mathf.Clamp01((1f - d) / softness);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            return Make(t, size, new Vector2(0.5f, 0.5f));
        }

        /// <summary>32×32 삼각형(오른쪽을 향함) — 바람 화살표 머리/마커</summary>
        public static Sprite Triangle()
        {
            if (triangle32 != null) return triangle32;
            int s = 32;
            Texture2D t = NewTex(s, s);
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = x / (float)(s - 1);
                    float halfH = (1f - u) * 0.5f;
                    float v = Mathf.Abs(y / (float)(s - 1) - 0.5f);
                    float a = Mathf.Clamp01((halfH - v) * s);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            triangle32 = Make(t, s, new Vector2(0.5f, 0.5f));
            return triangle32;
        }

        /// <summary>256×256 환형 링 (외반경 126, 내반경 100) — 호흡 링 UI</summary>
        public static Sprite Ring()
        {
            if (ring256 != null) return ring256;
            int s = 256; float ro = 126f, ri = 100f;
            Texture2D t = NewTex(s, s);
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - 128f, dy = y + 0.5f - 128f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(ro - d) * Mathf.Clamp01(d - ri);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            ring256 = Make(t, 100f, new Vector2(0.5f, 0.5f));
            return ring256;
        }

        /// <summary>64×4 양끝 테이퍼 먹선 (바람 입자). ppu 64 → 길이 1m</summary>
        public static Sprite Stroke()
        {
            if (stroke64 != null) return stroke64;
            int w = 64, h = 4;
            Texture2D t = NewTex(w, h);
            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1);
                    float taper = Mathf.Sin(u * Mathf.PI);
                    float vy = 1f - Mathf.Abs(y - 1.5f) / 2f;
                    px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(taper * vy * 1.4f));
                }
            t.SetPixels(px); t.Apply();
            stroke64 = Make(t, 64f, new Vector2(0.5f, 0.5f));
            return stroke64;
        }

        /// <summary>
        /// 화살 128×16, ppu 128 → 길이 1.0m, 피벗 = 촉 끝(1, 0.5). 샤프트/촉 먹, 깃 팀색.
        /// </summary>
        public static Sprite Arrow(int teamId)
        {
            int idx = teamId == 0 ? 0 : 1;
            if (arrowByTeam[idx] != null) return arrowByTeam[idx];
            int w = 128, h = 16;
            Color ink = Palette.Ink, team = Palette.Team(idx);
            Texture2D t = NewTex(w, h);
            Color[] px = new Color[w * h];
            Color clear = new Color(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            // 샤프트 x 14~112, 두께 6px (y 5~10)
            for (int x = 14; x < 112; x++)
                for (int y = 5; y < 11; y++) px[y * w + x] = ink;
            // 촉 삼각형 x 112~128, 높이 14px
            for (int x = 112; x < 128; x++)
            {
                float u = (x - 112) / 16f;
                int half = Mathf.RoundToInt(7f * (1f - u));
                for (int y = 8 - half; y <= 7 + half; y++)
                    if (y >= 0 && y < h) px[y * w + x] = ink;
            }
            // 깃 x 0~28: 위아래 평행사변형 5px 돌출 팀색
            for (int x = 0; x < 28; x++)
            {
                float u = x / 28f;
                int ext = Mathf.RoundToInt(5f * Mathf.Sin(u * Mathf.PI));
                for (int k = 1; k <= ext; k++)
                {
                    int yu = 10 + k, yd = 5 - k;
                    if (yu < h) px[yu * w + x] = team;
                    if (yd >= 0) px[yd * w + x] = team;
                }
                for (int y = 5; y < 11; y++) px[y * w + x] = ink;
            }
            t.SetPixels(px); t.Apply();
            arrowByTeam[idx] = Make(t, 128f, new Vector2(1f, 0.5f));
            return arrowByTeam[idx];
        }

        /// <summary>256×256 잉크 블롭 (로브 12개) — 머리 명중 스플래시</summary>
        public static Sprite InkBlob()
        {
            if (inkBlob != null) return inkBlob;
            int s = 256;
            Texture2D t = NewTex(s, s);
            Color[] px = new Color[s * s];
            float cx = 128f, cy = 128f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float rad = 78f + 22f * Mathf.Sin(ang * 12f) + 10f * Mathf.Sin(ang * 5f + 1.3f);
                    float a = Mathf.Clamp01((rad - d) * 0.5f);
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            inkBlob = Make(t, 128f, new Vector2(0.5f, 0.5f));
            return inkBlob;
        }

        /// <summary>버튼 9-slice (128×128, 먹 테두리 6px, 모서리 지터). color로 착색: 면=흰, 테두리=검정 분리 불가하므로 면/테두리 2장 사용 권장</summary>
        public static Sprite RoundedBox()
        {
            if (button != null) return button;
            int s = 128; int border = 6;
            Texture2D t = NewTex(s, s);
            Color[] px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // 가장자리 두께 노이즈 ±1px
                    int bx = border + ((x * 7 + y * 3) % 3) - 1;
                    bool edge = x < bx || y < bx || x >= s - bx || y >= s - bx;
                    px[y * s + x] = edge ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            t.SetPixels(px); t.Apply();
            button = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
            return button;
        }

        /// <summary>엔소(붓 원) 512×512, 330° 호, 두께 56→12px, 건필 노이즈</summary>
        public static Sprite Enso()
        {
            if (enso != null) return enso;
            int s = 512;
            Texture2D t = NewTex(s, s);
            Color[] px = new Color[s * s];
            float cx = 256f, cy = 256f, R = 200f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx); // -π..π
                    float u = Mathf.Repeat((ang - 2.2f) / (2f * Mathf.PI), 1f); // 시작점 기준 0..1
                    float a = 0f;
                    if (u < 330f / 360f)
                    {
                        float prog = u / (330f / 360f);
                        float thick = Mathf.Lerp(56f, 12f, prog);
                        float dist = Mathf.Abs(d - R);
                        a = Mathf.Clamp01((thick * 0.5f - dist) * 0.8f);
                        // 건필 노이즈 (끝으로 갈수록 마른 붓)
                        float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                        if (n < 0.25f + prog * 0.3f) a *= 0.35f;
                    }
                    px[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            enso = Make(t, 100f, new Vector2(0.5f, 0.5f));
            return enso;
        }

        // ---------------------------------------------------------------
        // 배경류 (경기당 1회 생성)
        // ---------------------------------------------------------------

        /// <summary>한지 배경 540×960 (그라디언트 + value noise + 섬유 + 얼룩 + 비네트)</summary>
        public static Sprite Paper(int seed)
        {
            int w = 540, h = 960;
            Texture2D t = NewTex(w, h);
            Color[] px = new Color[w * h];
            System.Random rng = new System.Random(seed);
            float horizonV = 700f / 1920f; // 화면 하단 700px 지점
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float g = v < horizonV ? 1f : 1f - (v - horizonV) / (1f - horizonV);
                Color baseC = Color.Lerp(Palette.PaperTop, Palette.PaperHorizon, g);
                for (int x = 0; x < w; x++)
                {
                    float n = 0.5f * Mathf.PerlinNoise(x / 4f + seed, y / 4f) + 0.3f * Mathf.PerlinNoise(x / 9f, y / 9f + seed) + 0.2f * Mathf.PerlinNoise(x / 23f, y / 23f);
                    float lum = 1f + (n - 0.5f) * 0.06f;
                    px[y * w + x] = new Color(baseC.r * lum, baseC.g * lum, baseC.b * lum, 1f);
                }
            }
            // 섬유 400가닥
            for (int i = 0; i < 400; i++)
            {
                int len = rng.Next(20, 60);
                float sx = (float)rng.NextDouble() * w, sy = (float)rng.NextDouble() * h;
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float curv = ((float)rng.NextDouble() - 0.5f) * 0.02f;
                float a = 0.05f + (float)rng.NextDouble() * 0.04f;
                for (int k = 0; k < len; k++)
                {
                    int xx = (int)(sx + Mathf.Cos(ang + curv * k) * k), yy = (int)(sy + Mathf.Sin(ang + curv * k) * k);
                    if (xx < 0 || yy < 0 || xx >= w || yy >= h) break;
                    px[yy * w + xx] = Color.Lerp(px[yy * w + xx], Palette.Fiber, a);
                }
            }
            // 얼룩 4개 (커피링)
            for (int i = 0; i < 4; i++)
            {
                float cx = (float)rng.NextDouble() * w, cy = (float)rng.NextDouble() * h;
                float r = 60f + (float)rng.NextDouble() * 70f; // 540px 기준 (1080 기준 120~260의 절반)
                int x0 = Mathf.Max(0, (int)(cx - r)), x1 = Mathf.Min(w - 1, (int)(cx + r));
                int y0 = Mathf.Max(0, (int)(cy - r)), y1 = Mathf.Min(h - 1, (int)(cy + r));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / r;
                        if (d > 1f) continue;
                        float a = 0.35f * (1f - d * d);
                        if (d > 0.9f) a += 0.10f;
                        px[y * w + x] = Color.Lerp(px[y * w + x], Palette.PaperD, a);
                    }
            }
            // 비네트
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float ex = Mathf.Abs(x / (float)w - 0.5f) * 2f, ey = Mathf.Abs(y / (float)h - 0.5f) * 2f;
                    float e = Mathf.Clamp01((ex * ex + ey * ey - 0.6f) / 0.9f);
                    px[y * w + x] = Color.Lerp(px[y * w + x], Palette.Ink, e * 0.06f);
                }
            t.SetPixels(px); t.Apply();
            return Make(t, 100f, new Vector2(0.5f, 0.5f));
        }

        /// <summary>원경 산 한 겹 (1280×512). heightPx: 최대 높이, color/alpha, 하단 40% 안개 페이드</summary>
        public static Sprite MountainLayer(int seed, int layer, float heightFrac, Color color, float alpha)
        {
            int w = 1280, h = 512;
            Texture2D t = NewTex(w, h);
            Color[] px = new Color[w * h];
            System.Random rng = new System.Random(seed * 31 + layer);
            float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f, p3 = (float)rng.NextDouble() * 6.28f;
            float fscale = layer == 2 ? 1.5f : 1f;
            float A = h * heightFrac;
            for (int x = 0; x < w; x++)
            {
                float xf = x * fscale;
                float hx = A * (0.55f + 0.25f * Mathf.Sin(0.004f * xf + p1) + 0.12f * Mathf.Sin(0.011f * xf + p2) + 0.08f * Mathf.Sin(0.027f * xf + p3));
                if (layer == 1) hx = Mathf.Lerp(hx, A * (1f - Mathf.Abs(Mathf.Sin(0.006f * xf + p1))), 0.3f);
                for (int y = 0; y < h; y++)
                {
                    float a = Mathf.Clamp01(hx - y) * alpha * color.a;
                    float fog = Mathf.Clamp01(y / (hx * 0.4f + 1f));
                    a *= fog;
                    px[y * w + x] = new Color(color.r, color.g, color.b, a);
                }
            }
            t.SetPixels(px); t.Apply();
            return Make(t, 100f, new Vector2(0.5f, 0f));
        }

        /// <summary>
        /// 지면 2048×512 베이크 (ppu 51.2 → 40m × 10m). 텍스처 상단 = 월드 y +2m, 하단 = −8m, 중심 x=0.
        /// </summary>
        public static Sprite Ground(MatchSetup setup)
        {
            int w = 2048, h = 512;
            float ppu = 51.2f;
            float topY = 2f;
            Texture2D t = NewTex(w, h);
            Color[] px = new Color[w * h];
            System.Random rng = new System.Random(setup.Seed);
            float phi1 = (float)rng.NextDouble() * 6.28f, phi2 = (float)rng.NextDouble() * 6.28f;
            Color clear = new Color(0, 0, 0, 0);
            float[] H = new float[w];
            for (int x = 0; x < w; x++)
            {
                float xm = (x + 0.5f) / ppu - 20f;
                float baseH = setup.GroundHeight(xm);
                // 궁수 주변 ±0.8m 평탄 고원: 잡음 가중치 0
                float d0 = Mathf.Abs(xm - setup.Feet(0).x), d1 = Mathf.Abs(xm - setup.Feet(1).x);
                float flat = Mathf.Clamp01(Mathf.Min(d0, d1) / 0.8f - 1f);
                float noise = (0.18f * Mathf.Sin(0.9f * xm + phi1) + 0.10f * Mathf.Sin(2.3f * xm + phi2)) * flat;
                H[x] = baseH + noise;
            }
            for (int y = 0; y < h; y++)
            {
                float ym = topY - (h - 1 - y) / ppu; // y=h-1 → topY
                for (int x = 0; x < w; x++)
                {
                    float d = H[x] - ym; // 윗선 아래 깊이
                    if (d < -1f / ppu) { px[y * w + x] = clear; continue; }
                    float edge = Mathf.Clamp01(d * ppu + 1f); // 1texel AA
                    float a;
                    if (d < 0.3f) a = 0.92f;
                    else if (d < 1.5f) a = Mathf.Lerp(0.92f, 0.55f, (d - 0.3f) / 1.2f);
                    else a = Mathf.Lerp(0.55f, 0f, Mathf.Clamp01((d - 1.5f) / 4.5f));
                    Color c = Color.Lerp(Palette.Ink, Palette.InkL, Mathf.Clamp01(d / 6f));
                    // 건필: 윗선 아래 0.7m 띠에 가로 줄무늬 노이즈
                    if (d > 0.05f && d < 0.7f)
                    {
                        float n = Mathf.PerlinNoise(x * 0.02f, y * 0.9f);
                        if (n < 0.38f) a *= 0.25f;
                    }
                    px[y * w + x] = new Color(c.r, c.g, c.b, a * edge);
                }
            }
            // 풀 붓 터치: 윗선 위 가는 삼각 스트로크
            float gx = -20f;
            while (gx < 20f)
            {
                gx += 0.25f + (float)rng.NextDouble() * 0.25f;
                int xi = (int)((gx + 20f) * ppu);
                if (xi < 2 || xi >= w - 2) continue;
                float gh = 0.12f + (float)rng.NextDouble() * 0.18f;
                int hpx = (int)(gh * ppu);
                int baseY = (int)((H[xi] - topY) * ppu + h - 1);
                int lean = rng.Next(-1, 2);
                for (int k = 0; k < hpx; k++)
                {
                    int yy = baseY + k;
                    int xx = xi + (lean * k) / 3;
                    if (yy < 0 || yy >= h || xx < 1 || xx >= w - 1) break;
                    float a = 0.8f * (1f - k / (float)hpx);
                    px[yy * w + xx] = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, Mathf.Max(px[yy * w + xx].a, a));
                    if (k < hpx / 2) px[yy * w + xx - 1] = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, Mathf.Max(px[yy * w + xx - 1].a, a * 0.6f));
                }
            }
            t.SetPixels(px); t.Apply();
            // 피벗: 텍스처 상단 중앙 → 월드 (0, topY)에 배치
            return Make(t, ppu, new Vector2(0.5f, 1f));
        }
    }
}

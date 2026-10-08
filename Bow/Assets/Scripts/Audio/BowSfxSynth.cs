using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bow.Audio
{
    /// <summary>
    /// 사운드 사양서 §5 레이어 표를 그대로 코드화한 프로시저럴 SFX 합성기.
    /// 파일 SFX(Resources/Audio/SFX/&lt;id&gt;.wav)가 없을 때 폴백으로 사용한다. 클립은 ID별 1회 생성·캐시.
    /// </summary>
    public static class BowSfxSynth
    {
        private const int SR = 44100;

        public enum Wave { Sine, Tri, Saw, Noise }

        /// <summary>합성 레이어 (주파수는 f0→f1 지수 스윕, ADSR은 초 단위)</summary>
        public sealed class Layer
        {
            public Wave wave = Wave.Sine;
            public float f0 = 440f, f1 = -1f;      // f1<0 → 고정
            public float dur = 0.2f;
            public float A = 0.001f, D = 0.05f, S = 0f, R = 0.05f;
            public float lp = -1f, lp1 = -1f;      // 로우패스 (lp1≥0 → 스윕)
            public float hp = -1f;
            public float amp = 0.5f;
            public float delay = 0f;
            public float h2 = 0f, h3 = 0f;         // 배음 진폭
            public float vibFreq = 0f, vibDepth = 0f; // 비브라토 (Hz, ±Hz)
            public float amFreq = 0f, amDepth = 0f;   // 트레몰로

            public Layer(Wave w, float f0, float f1, float dur, float A, float D, float S, float R, float amp, float delay = 0f)
            {
                wave = w; this.f0 = f0; this.f1 = f1; this.dur = dur; this.A = A; this.D = D; this.S = S; this.R = R; this.amp = amp; this.delay = delay;
            }
            public Layer LP(float fc, float fc1 = -1f) { lp = fc; lp1 = fc1; return this; }
            public Layer HP(float fc) { hp = fc; return this; }
            public Layer Harm(float h2, float h3) { this.h2 = h2; this.h3 = h3; return this; }
            public Layer Vib(float f, float d) { vibFreq = f; vibDepth = d; return this; }
            public Layer AM(float f, float d) { amFreq = f; amDepth = d; return this; }
        }

        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private static readonly System.Random noiseRng = new System.Random(12345);

        public static AudioClip Get(string id)
        {
            AudioClip c;
            if (cache.TryGetValue(id, out c) && c != null) return c;
            c = Build(id);
            cache[id] = c;
            return c;
        }

        // ---------------------------------------------------------------
        // 레이어 → 샘플
        // ---------------------------------------------------------------

        private static float Env(float t, Layer L)
        {
            float sustainLen = Mathf.Max(0f, L.dur - L.A - L.D - L.R);
            if (t < L.A) return L.A > 0f ? t / L.A : 1f;
            t -= L.A;
            if (t < L.D) return Mathf.Lerp(1f, L.S, L.D > 0f ? t / L.D : 1f);
            t -= L.D;
            if (t < sustainLen) return L.S;
            t -= sustainLen;
            if (t < L.R) return Mathf.Lerp(L.S, 0f, L.R > 0f ? t / L.R : 1f);
            return 0f;
        }

        private static float Osc(Wave w, float phase)
        {
            float p = phase - Mathf.Floor(phase); // 0..1
            switch (w)
            {
                case Wave.Sine: return Mathf.Sin(p * 2f * Mathf.PI);
                case Wave.Tri: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Wave.Saw: return 2f * p - 1f;
                default: return (float)(noiseRng.NextDouble() * 2.0 - 1.0);
            }
        }

        private static void Render(Layer L, float[] outBuf)
        {
            int n = Mathf.CeilToInt(L.dur * SR);
            int start = Mathf.RoundToInt(L.delay * SR);
            float[] buf = new float[n];
            float phase = 0f;
            float f1 = L.f1 < 0f ? L.f0 : L.f1;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float u = n > 1 ? i / (float)(n - 1) : 0f;
                if (L.wave != Wave.Noise)
                {
                    float f = L.f0 * Mathf.Pow(f1 / L.f0, u);
                    if (L.vibFreq > 0f) f += L.vibDepth * Mathf.Sin(2f * Mathf.PI * L.vibFreq * t);
                    phase += f / SR;
                }
                float s;
                if (L.wave == Wave.Noise) s = Osc(Wave.Noise, 0f);
                else
                {
                    s = Osc(L.wave, phase);
                    if (L.h2 > 0f) s += L.h2 * Osc(L.wave, phase * 2f);
                    if (L.h3 > 0f) s += L.h3 * Osc(L.wave, phase * 3f);
                }
                float am = L.amFreq > 0f ? 1f - L.amDepth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * L.amFreq * t)) : 1f;
                buf[i] = s * Env(t, L) * am;
            }
            if (L.hp > 0f) HighPass(buf, L.hp);
            if (L.lp > 0f) LowPass(buf, L.lp, L.lp1);
            for (int i = 0; i < n; i++)
            {
                int k = start + i;
                if (k >= 0 && k < outBuf.Length) outBuf[k] += buf[i] * L.amp;
            }
        }

        private static void LowPass(float[] b, float fc0, float fc1)
        {
            float y = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                float fc = fc1 > 0f ? Mathf.Lerp(fc0, fc1, i / (float)b.Length) : fc0;
                float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / SR);
                y += a * (b[i] - y);
                b[i] = y;
            }
        }

        private static void HighPass(float[] b, float fc)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc / SR);
            float lp = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                lp += a * (b[i] - lp);
                b[i] = b[i] - lp;
            }
        }

        private static void Reverb(float[] d, float delayMs, float decay, int taps)
        {
            int ds = Mathf.RoundToInt(delayMs * SR / 1000f);
            for (int tap = 1; tap <= taps; tap++)
            {
                float g = Mathf.Pow(decay, tap);
                int off = ds * tap;
                for (int i = d.Length - 1; i >= off; i--) d[i] += d[i - off] * g;
            }
        }

        private static AudioClip Finish(string id, float[] data, bool loop)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) { float a = Mathf.Abs(data[i]); if (a > peak) peak = a; }
            if (peak > 1e-5f) { float g = 0.9f / peak; for (int i = 0; i < data.Length; i++) data[i] *= g; }
            if (loop)
            {
                // 양 끝 50ms 등전력 크로스페이드로 이음새 제거
                int n = Mathf.Min(SR / 20, data.Length / 4);
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)n;
                    float gIn = Mathf.Sin(u * Mathf.PI * 0.5f), gOut = Mathf.Cos(u * Mathf.PI * 0.5f);
                    data[i] = data[i] * gIn + data[data.Length - n + i] * gOut;
                }
                for (int i = 0; i < n; i++) data[data.Length - n + i] = data[i];
            }
            else
            {
                int fi = Mathf.Min(64, data.Length / 4), fo = Mathf.Min(128, data.Length / 4);
                for (int i = 0; i < fi; i++) data[i] *= i / (float)fi;
                for (int i = 0; i < fo; i++) data[data.Length - 1 - i] *= i / (float)fo;
            }
            AudioClip clip = AudioClip.Create(id, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float[] Mix(float totalDur, params Layer[] layers)
        {
            float[] buf = new float[Mathf.CeilToInt(totalDur * SR)];
            for (int i = 0; i < layers.Length; i++) Render(layers[i], buf);
            return buf;
        }

        private static float TotalDur(params Layer[] layers)
        {
            float d = 0f;
            for (int i = 0; i < layers.Length; i++) d = Mathf.Max(d, layers[i].delay + layers[i].dur);
            return d;
        }

        private static AudioClip Simple(string id, bool loop, params Layer[] layers)
        {
            float[] buf = Mix(TotalDur(layers), layers);
            return Finish(id, buf, loop);
        }

        // ---------------------------------------------------------------
        // ID별 레시피 (사양서 §5.2)
        // ---------------------------------------------------------------

        private static AudioClip Build(string id)
        {
            switch (id)
            {
                case "bow_draw_start":
                    return Simple(id, false,
                        new Layer(Wave.Noise, 0, -1, 0.18f, 0.02f, 0.06f, 0.40f, 0.08f, 0.50f).HP(600f).LP(1300f),
                        new Layer(Wave.Tri, 110f, 150f, 0.18f, 0.01f, 0.05f, 0.50f, 0.10f, 0.35f).LP(800f),
                        new Layer(Wave.Sine, 392f, -1, 0.06f, 0.002f, 0.03f, 0f, 0.02f, 0.15f, 0.02f));
                case "bow_draw_loop":
                    return Simple(id, true,
                        new Layer(Wave.Noise, 0, -1, 1.2f, 0.05f, 0.05f, 1f, 0.05f, 0.25f).HP(500f).LP(1000f).AM(5f, 0.35f),
                        new Layer(Wave.Saw, 82f, -1, 1.2f, 0.05f, 0.05f, 1f, 0.05f, 0.30f).LP(400f).Vib(0.8333f, 3f));
                case "string_tension_loop":
                    return Simple(id, true,
                        new Layer(Wave.Sine, 147f, -1, 1.0f, 0.02f, 0.02f, 1f, 0.02f, 0.50f).LP(1800f).Harm(0.25f, 0.10f),
                        new Layer(Wave.Sine, 148f, -1, 1.0f, 0.02f, 0.02f, 1f, 0.02f, 0.30f).LP(1800f));
                case "meter_tick":
                    return Simple(id, false,
                        new Layer(Wave.Tri, 660f, 520f, 0.05f, 0.001f, 0.015f, 0.25f, 0.025f, 0.60f).LP(3500f),
                        new Layer(Wave.Noise, 0, -1, 0.008f, 0f, 0.004f, 0f, 0.004f, 0.20f).HP(2500f));
                case "release":
                    return Simple(id, false,
                        new Layer(Wave.Sine, 392f, 370f, 0.40f, 0.001f, 0.25f, 0f, 0.14f, 0.50f).Harm(0.25f, 0.12f),
                        new Layer(Wave.Noise, 0, -1, 0.35f, 0.02f, 0.12f, 0.30f, 0.20f, 0.50f, 0.01f).HP(500f).LP(600f, 3200f),
                        new Layer(Wave.Sine, 90f, 55f, 0.08f, 0.001f, 0.05f, 0f, 0.03f, 0.40f));
                case "arrow_whistle":
                    return Simple(id, true,
                        new Layer(Wave.Noise, 0, -1, 0.8f, 0.05f, 0.05f, 1f, 0.05f, 0.35f).HP(2200f).LP(2600f).AM(30f, 0.2f),
                        new Layer(Wave.Sine, 1900f, -1, 0.8f, 0.05f, 0.05f, 1f, 0.05f, 0.12f).LP(6000f).Vib(3f, 40f).AM(30f, 0.2f));
                case "wind_loop":
                    return Simple(id, true,
                        new Layer(Wave.Noise, 0, -1, 4.0f, 0.25f, 0.25f, 1f, 0.25f, 0.60f).HP(300f).LP(900f).AM(0.25f, 0.4f),
                        new Layer(Wave.Noise, 0, -1, 4.0f, 0.25f, 0.25f, 1f, 0.25f, 0.20f).HP(1500f).LP(5000f).AM(0.5f, 0.25f));
                case "hit_body":
                    return Simple(id, false,
                        new Layer(Wave.Sine, 140f, 60f, 0.32f, 0.001f, 0.08f, 0.20f, 0.20f, 0.70f),
                        new Layer(Wave.Noise, 0, -1, 0.15f, 0.001f, 0.04f, 0.10f, 0.10f, 0.45f).LP(1200f),
                        new Layer(Wave.Sine, 520f, -1, 0.25f, 0.005f, 0.05f, 0.40f, 0.20f, 0.15f, 0.04f).Vib(14f, 25f));
                case "hit_head":
                    return Simple(id, false,
                        new Layer(Wave.Sine, 110f, 45f, 0.55f, 0.001f, 0.10f, 0.25f, 0.30f, 0.85f),
                        new Layer(Wave.Noise, 0, -1, 0.05f, 0f, 0.015f, 0f, 0.03f, 0.50f).HP(1800f).LP(7000f),
                        new Layer(Wave.Noise, 0, -1, 0.40f, 0.01f, 0.12f, 0.30f, 0.30f, 0.40f, 0.03f).HP(300f).LP(1400f, 400f),
                        new Layer(Wave.Sine, 220f, -1, 0.45f, 0.01f, 0.20f, 0.30f, 0.30f, 0.12f, 0.02f),
                        new Layer(Wave.Sine, 331f, -1, 0.45f, 0.01f, 0.20f, 0.30f, 0.30f, 0.10f, 0.02f));
                case "perfect_bell":
                {
                    Layer[] L = {
                        new Layer(Wave.Sine, 880f, -1, 1.2f, 0.001f, 1.0f, 0f, 0.10f, 0.50f),
                        new Layer(Wave.Sine, 1760f, -1, 0.8f, 0.001f, 0.7f, 0f, 0.10f, 0.25f),
                        new Layer(Wave.Sine, 2429f, -1, 0.6f, 0.001f, 0.5f, 0f, 0.10f, 0.20f),
                        new Layer(Wave.Sine, 4752f, -1, 0.3f, 0.001f, 0.25f, 0f, 0.05f, 0.08f),
                        new Layer(Wave.Noise, 0, -1, 0.005f, 0f, 0.005f, 0f, 0f, 0.20f).HP(4000f) };
                    float[] buf = Mix(TotalDur(L), L);
                    for (int i = 0; i < buf.Length; i++) buf[i] *= 1f - 0.08f * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * 6f * i / SR));
                    Reverb(buf, 40f, 0.30f, 3);
                    return Finish(id, buf, false);
                }
                case "miss_ground":
                    return Simple(id, false,
                        new Layer(Wave.Sine, 160f, 80f, 0.20f, 0.001f, 0.04f, 0.20f, 0.10f, 0.50f),
                        new Layer(Wave.Noise, 0, -1, 0.08f, 0f, 0.02f, 0.10f, 0.05f, 0.35f).LP(900f),
                        new Layer(Wave.Sine, 300f, 260f, 0.27f, 0.003f, 0.12f, 0.20f, 0.15f, 0.25f, 0.03f).Vib(18f, 15f));
                case "reload_ready":
                    return Simple(id, false,
                        new Layer(Wave.Tri, 520f, 400f, 0.05f, 0.001f, 0.02f, 0f, 0.03f, 0.50f).LP(4000f),
                        new Layer(Wave.Sine, 587.33f, -1, 0.27f, 0.005f, 0.10f, 0.30f, 0.15f, 0.30f, 0.03f).Harm(0.20f, 0f));
                case "breath_peak":
                {
                    Layer[] L = {
                        new Layer(Wave.Sine, 587.33f, -1, 1.0f, 0.01f, 0.50f, 0f, 0.30f, 0.20f),
                        new Layer(Wave.Sine, 589.33f, -1, 1.0f, 0.01f, 0.50f, 0f, 0.30f, 0.20f),
                        new Layer(Wave.Sine, 880f, -1, 0.85f, 0.01f, 0.45f, 0f, 0.30f, 0.35f, 0.15f),
                        new Layer(Wave.Sine, 2349f, -1, 0.7f, 0.02f, 0.30f, 0f, 0.30f, 0.06f, 0.10f).AM(7f, 0.5f) };
                    float[] buf = Mix(TotalDur(L), L);
                    Reverb(buf, 60f, 0.35f, 3);
                    return Finish(id, buf, false);
                }
                case "countdown_tick":
                    return Simple(id, false,
                        new Layer(Wave.Tri, 440f, 420f, 0.22f, 0.001f, 0.05f, 0.20f, 0.10f, 0.60f),
                        new Layer(Wave.Sine, 880f, -1, 0.10f, 0.001f, 0.03f, 0f, 0.06f, 0.20f),
                        new Layer(Wave.Noise, 0, -1, 0.005f, 0f, 0.005f, 0f, 0f, 0.20f).LP(2000f));
                case "countdown_go":
                    return Simple(id, false,
                        new Layer(Wave.Sine, 100f, 50f, 0.70f, 0.001f, 0.15f, 0.20f, 0.40f, 0.70f),
                        new Layer(Wave.Noise, 0, -1, 0.04f, 0f, 0.01f, 0.20f, 0.02f, 0.30f).HP(400f).LP(1000f),
                        new Layer(Wave.Sine, 587.33f, -1, 0.50f, 0.005f, 0.15f, 0.40f, 0.40f, 0.25f, 0.02f),
                        new Layer(Wave.Sine, 880f, -1, 0.50f, 0.005f, 0.15f, 0.40f, 0.40f, 0.25f, 0.02f),
                        new Layer(Wave.Sine, 1174.66f, -1, 0.50f, 0.005f, 0.15f, 0.40f, 0.40f, 0.25f, 0.02f));
                case "victory_stinger":
                {
                    Layer[] L = {
                        new Layer(Wave.Sine, 587.33f, -1, 1.2f, 0.002f, 0.35f, 0.15f, 0.30f, 0.30f, 0.00f).LP(5000f).Harm(0.2f, 0f),
                        new Layer(Wave.Sine, 739.99f, -1, 1.2f, 0.002f, 0.35f, 0.15f, 0.30f, 0.30f, 0.18f).LP(5000f).Harm(0.2f, 0f),
                        new Layer(Wave.Sine, 880f, -1, 1.2f, 0.002f, 0.35f, 0.15f, 0.30f, 0.30f, 0.36f).LP(5000f).Harm(0.2f, 0f),
                        new Layer(Wave.Sine, 1174.66f, -1, 1.2f, 0.002f, 0.35f, 0.15f, 0.30f, 0.30f, 0.54f).LP(5000f).Harm(0.2f, 0f),
                        new Layer(Wave.Sine, 587.33f, -1, 1.9f, 0.05f, 0.30f, 0.60f, 0.50f, 0.18f, 0.54f).LP(4000f),
                        new Layer(Wave.Sine, 880f, -1, 1.9f, 0.05f, 0.30f, 0.60f, 0.50f, 0.18f, 0.54f).LP(4000f),
                        new Layer(Wave.Sine, 1174.66f, -1, 1.9f, 0.05f, 0.30f, 0.60f, 0.50f, 0.18f, 0.54f).LP(4000f),
                        new Layer(Wave.Sine, 1479.98f, -1, 1.9f, 0.05f, 0.30f, 0.60f, 0.50f, 0.18f, 0.54f).LP(4000f),
                        new Layer(Wave.Sine, 880f, -1, 1.2f, 0.001f, 1.0f, 0f, 0.10f, 0.15f, 0.54f),
                        new Layer(Wave.Sine, 2429f, -1, 0.6f, 0.001f, 0.5f, 0f, 0.10f, 0.06f, 0.54f) };
                    float[] buf = Mix(TotalDur(L), L);
                    Reverb(buf, 45f, 0.30f, 3);
                    return Finish(id, buf, false);
                }
                case "defeat_stinger":
                    return Simple(id, false,
                        new Layer(Wave.Sine, 100f, 45f, 0.60f, 0.001f, 0.15f, 0.20f, 0.30f, 0.50f),
                        new Layer(Wave.Tri, 440f, -1, 0.6f, 0.02f, 0.25f, 0.40f, 0.30f, 0.35f, 0.0f).LP(2500f),
                        new Layer(Wave.Tri, 349.23f, -1, 0.6f, 0.02f, 0.25f, 0.40f, 0.30f, 0.35f, 0.4f).LP(2500f),
                        new Layer(Wave.Tri, 293.66f, -1, 0.6f, 0.02f, 0.25f, 0.40f, 0.30f, 0.35f, 0.8f).LP(2500f),
                        new Layer(Wave.Saw, 293.66f, -1, 2.0f, 0.15f, 0.30f, 0.50f, 1.00f, 0.25f, 0.80f).LP(1800f).Vib(5f, 8f),
                        new Layer(Wave.Sine, 146.83f, -1, 1.5f, 0.10f, 0.40f, 0.50f, 0.80f, 0.30f, 1.30f));
                case "ui_click":
                    return Simple(id, false,
                        new Layer(Wave.Tri, 780f, 620f, 0.09f, 0.001f, 0.02f, 0.20f, 0.05f, 0.45f).LP(3500f),
                        new Layer(Wave.Noise, 0, -1, 0.004f, 0f, 0.004f, 0f, 0f, 0.15f).HP(3000f));
                case "ui_back":
                    return Simple(id, false,
                        new Layer(Wave.Tri, 620f, 480f, 0.09f, 0.001f, 0.02f, 0.20f, 0.05f, 0.45f).LP(3000f));
                case "time_warning":
                    return Simple(id, false,
                        new Layer(Wave.Tri, 1000f, -1, 0.12f, 0.001f, 0.03f, 0.30f, 0.06f, 0.50f).LP(4000f));
                default:
                    Debug.LogWarning("[BowSfxSynth] 알 수 없는 SFX id: " + id);
                    return Simple(id, false, new Layer(Wave.Sine, 440f, -1, 0.1f, 0.001f, 0.05f, 0f, 0.04f, 0.5f));
            }
        }
    }
}

using UnityEngine;

namespace JewelsHexaPuzzle.Utils
{
    /// <summary>
    /// 런타임 프로시저럴 AudioClip 생성 유틸리티 (V3 - SFX 사양서 적용)
    /// 파스텔 톤: 부드러운 어택, 따뜻한 음색(3kHz+ 롤오프), 리버브 공간감
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SAMPLE_RATE = 44100;

        // ============================================================
        // 파형 타입
        // ============================================================

        public enum Waveform { Sine, Triangle, Sawtooth, Square, Pulse25 }

        private static float GenerateWaveform(Waveform type, float phase)
        {
            float p = (phase % (2f * Mathf.PI)) / (2f * Mathf.PI);
            if (p < 0f) p += 1f;

            switch (type)
            {
                case Waveform.Sine:
                    return Mathf.Sin(phase);
                case Waveform.Triangle:
                    return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case Waveform.Sawtooth:
                    return 2f * p - 1f;
                case Waveform.Square:
                    return p < 0.5f ? 1f : -1f;
                case Waveform.Pulse25:
                    return p < 0.25f ? 1f : -0.333f;
                default:
                    return Mathf.Sin(phase);
            }
        }

        // ============================================================
        // ADSR 엔벨로프
        // ============================================================

        private static float ADSR(float tNorm,
            float attack = 0.02f, float decay = 0.15f,
            float sustain = 0.6f, float release = 0.25f)
        {
            float attackEnd = attack;
            float decayEnd = attack + decay;
            float releaseStart = 1f - release;

            if (tNorm < attackEnd)
                return attackEnd > 0f ? tNorm / attackEnd : 1f;
            else if (tNorm < decayEnd)
                return 1f - (1f - sustain) * ((tNorm - attackEnd) / Mathf.Max(0.001f, decay));
            else if (tNorm < releaseStart)
                return sustain;
            else
                return sustain * Mathf.Max(0f, 1f - (tNorm - releaseStart) / Mathf.Max(0.001f, release));
        }

        // ============================================================
        // 하모닉 생성
        // ============================================================

        private static float ToneWithHarmonics(float phase, int harmonics = 5, float rolloff = 0.5f)
        {
            float sample = 0f;
            float totalAmp = 0f;
            for (int h = 1; h <= harmonics; h++)
            {
                float amp = Mathf.Pow(rolloff, h - 1);
                sample += Mathf.Sin(phase * h) * amp;
                totalAmp += amp;
            }
            return sample / totalAmp;
        }

        private static float WaveWithHarmonics(Waveform type, float phase, int harmonics = 3, float rolloff = 0.5f)
        {
            float sample = 0f;
            float totalAmp = 0f;
            for (int h = 1; h <= harmonics; h++)
            {
                float amp = Mathf.Pow(rolloff, h - 1);
                sample += GenerateWaveform(type, phase * h) * amp;
                totalAmp += amp;
            }
            return sample / totalAmp;
        }

        // ============================================================
        // DSP 유틸리티
        // ============================================================

        private static void ApplyLowPass(float[] data, float cutoff)
        {
            if (cutoff <= 0f) return;
            float alpha = 1f - Mathf.Clamp01(cutoff);
            float prev = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = alpha * data[i] + (1f - alpha) * prev;
                prev = data[i];
            }
        }

        private static void ApplyReverb(float[] data, float delayMs = 25f, float decay = 0.25f, int taps = 3)
        {
            for (int tap = 1; tap <= taps; tap++)
            {
                int delaySamples = Mathf.CeilToInt(SAMPLE_RATE * delayMs * tap / 1000f);
                float tapDecay = Mathf.Pow(decay, tap);
                for (int i = data.Length - 1; i >= delaySamples; i--)
                {
                    data[i] += data[i - delaySamples] * tapDecay;
                }
            }
            Normalize(data, 0.9f);
        }

        private static void Normalize(float[] data, float targetPeak = 0.9f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max > targetPeak)
            {
                float scale = targetPeak / max;
                for (int i = 0; i < data.Length; i++)
                    data[i] *= scale;
            }
        }

        private static void ApplyFades(float[] data, int fadeInSamples = 64, int fadeOutSamples = 128)
        {
            for (int i = 0; i < Mathf.Min(fadeInSamples, data.Length); i++)
                data[i] *= (float)i / fadeInSamples;
            for (int i = 0; i < Mathf.Min(fadeOutSamples, data.Length); i++)
            {
                int idx = data.Length - 1 - i;
                data[idx] *= (float)i / fadeOutSamples;
            }
        }

        // ============================================================
        // 유틸리티: 노트 엔벨로프 (절대 시간 기반)
        // ============================================================

        private static float NoteEnvelope(float noteTime, float attack, float sustain, float decay)
        {
            float total = attack + sustain + decay;
            if (noteTime < 0f) return 0f;
            if (noteTime < attack)
                return attack > 0f ? noteTime / attack : 1f;
            else if (noteTime < attack + sustain)
                return 1f;
            else if (noteTime < total)
                return 1f - (noteTime - attack - sustain) / Mathf.Max(0.001f, decay);
            else
                return 0f;
        }

        // ============================================================
        // 사운드 생성 메서드 (V3 - SFX 사양서 기반)
        // ============================================================

        public static AudioClip CreateTone(float frequency, float duration, float fadeOut = 0.05f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float phase = 2f * Mathf.PI * frequency * t;
                float envelope = ADSR(tNorm, 0.01f, 0.1f, 0.7f, fadeOut / duration);
                data[i] = ToneWithHarmonics(phase, 4, 0.4f) * envelope * 0.5f;
            }
            ApplyFades(data, 48, Mathf.CeilToInt(SAMPLE_RATE * fadeOut));
            ApplyReverb(data, 20f, 0.15f, 2);
            AudioClip clip = AudioClip.Create("Tone", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 1. 버튼 클릭 - Sine 880Hz, +200Hz 상향 슬라이드, "뽁" 팝 느낌
        /// </summary>
        public static AudioClip CreateClick(float duration = 0.11f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = NoteEnvelope(t, 0f, 0.03f, 0.08f);
                float freq = 880f + 200f * tNorm;
                phase += 2f * Mathf.PI * freq / SAMPLE_RATE;
                data[i] = Mathf.Sin(phase) * envelope * 0.4f;
            }
            ApplyFades(data, 4, 32);
            ApplyReverb(data, 15f, 0.15f, 2);
            AudioClip clip = AudioClip.Create("Click", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 2. 팝업 열기 - Sine+Triangle, 440→880Hz 상승 슬라이드
        /// </summary>
        public static AudioClip CreatePopupSound(float duration = 0.3f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = NoteEnvelope(t, 0.05f, 0.1f, 0.15f);
                float freq = Mathf.Lerp(440f, 880f, tNorm);
                phase += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float sample = Mathf.Sin(phase) * 0.7f
                             + GenerateWaveform(Waveform.Triangle, phase) * 0.3f;
                data[i] = sample * envelope * 0.4f;
            }
            ApplyFades(data, 16, 64);
            ApplyLowPass(data, 0.15f);
            ApplyReverb(data, 25f, 0.2f, 3);
            AudioClip clip = AudioClip.Create("Popup", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 3. 블록 회전 - 노이즈 밴드패스, 600→1200→600Hz 스윕
        /// </summary>
        public static AudioClip CreateRotateSound(float duration = 0.23f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(55);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = NoteEnvelope(t, 0.03f, 0.08f, 0.12f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float centerFreq = tNorm < 0.5f
                    ? Mathf.Lerp(600f, 1200f, tNorm * 2f)
                    : Mathf.Lerp(1200f, 600f, (tNorm - 0.5f) * 2f);
                float tonePhase = 2f * Mathf.PI * centerFreq * t;
                float sample = noise * 0.4f + Mathf.Sin(tonePhase) * 0.3f;
                data[i] = sample * envelope * 0.25f;
            }
            ApplyFades(data, 16, 64);
            ApplyLowPass(data, 0.4f);
            ApplyReverb(data, 15f, 0.15f, 2);
            AudioClip clip = AudioClip.Create("Rotate", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 4. 3매치 아르페지오 - C6→E6→G6, 30ms 간격
        /// </summary>
        public static AudioClip CreateMatchArpeggio3(float duration = 0.3f)
        {
            float[] notes = { 1046.5f, 1318.5f, 1568f }; // C6, E6, G6
            float interval = 0.03f;
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * interval;
                    float noteTime = t - noteStart;
                    if (noteTime < 0f) continue;
                    float env = NoteEnvelope(noteTime, 0f, 0.06f, 0.15f);
                    float phase = 2f * Mathf.PI * notes[n] * noteTime;
                    sample += Mathf.Sin(phase) * env;
                }
                data[i] = sample * 0.5f / notes.Length * 2f;
            }
            ApplyFades(data, 4, 128);
            ApplyReverb(data, 30f, 0.25f, 3);
            AudioClip clip = AudioClip.Create("Match3", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 5. 4매치 아르페지오 - C6→E6→G6→C7, 25ms 간격, Sine+Triangle
        /// </summary>
        public static AudioClip CreateMatchArpeggio4(float duration = 0.35f)
        {
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f }; // C6, E6, G6, C7
            float interval = 0.025f;
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * interval;
                    float noteTime = t - noteStart;
                    if (noteTime < 0f) continue;
                    float env = NoteEnvelope(noteTime, 0f, 0.05f, 0.18f);
                    float phase = 2f * Mathf.PI * notes[n] * noteTime;
                    float tone = Mathf.Sin(phase) * 0.7f
                               + GenerateWaveform(Waveform.Triangle, phase) * 0.3f;
                    if (n == notes.Length - 1)
                    {
                        float shimmer = 1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 2f * noteTime);
                        tone *= shimmer;
                    }
                    sample += tone * env;
                }
                data[i] = sample * 0.55f / notes.Length * 2f;
            }
            ApplyFades(data, 4, 128);
            ApplyReverb(data, 35f, 0.28f, 3);
            AudioClip clip = AudioClip.Create("Match4", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 6. 5매치 아르페지오 - C6→E6→G6→C7→E7, 20ms 간격, Sine+Square(10%)
        /// </summary>
        public static AudioClip CreateMatchArpeggio5(float duration = 0.5f)
        {
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f, 2637f }; // C6→E7
            float interval = 0.02f;
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(42);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * interval;
                    float noteTime = t - noteStart;
                    if (noteTime < 0f) continue;
                    float env = NoteEnvelope(noteTime, 0f, 0.04f, 0.22f);
                    float phase = 2f * Mathf.PI * notes[n] * noteTime;
                    float tone = Mathf.Sin(phase) * 0.9f
                               + GenerateWaveform(Waveform.Square, phase) * 0.1f;
                    if (n >= notes.Length - 2)
                    {
                        float shimmer = 1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 3f * noteTime);
                        tone *= shimmer;
                    }
                    sample += tone * env;
                }
                float sparkle = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.03f * (1f - tNorm);
                data[i] = (sample * 0.6f / notes.Length * 2f) + sparkle;
            }
            ApplyFades(data, 4, 192);
            ApplyReverb(data, 40f, 0.3f, 4);
            AudioClip clip = AudioClip.Create("Match5", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 7. 매치 실패 - Sine, E5→C5 2음 하행, 100ms 간격
        /// </summary>
        public static AudioClip CreateFailSound(float duration = 0.35f)
        {
            float[] notes = { 659.25f, 523.25f }; // E5, C5
            float interval = 0.1f;
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * interval;
                    float noteTime = t - noteStart;
                    if (noteTime < 0f) continue;
                    float env = NoteEnvelope(noteTime, 0.02f, 0.08f, 0.15f);
                    float pitchBend = 1f - 0.012f * noteTime;
                    float phase = 2f * Mathf.PI * notes[n] * pitchBend * noteTime;
                    sample += Mathf.Sin(phase) * env;
                }
                data[i] = sample * 0.3f;
            }
            ApplyFades(data, 16, 96);
            ApplyLowPass(data, 0.2f);
            ApplyReverb(data, 20f, 0.2f, 3);
            AudioClip clip = AudioClip.Create("Fail", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 8. 블록 파괴 - Sine 1200Hz, -800Hz 급하강, "톡" 버블 팝
        /// </summary>
        public static AudioClip CreateNoiseBurst(float duration = 0.07f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float env = NoteEnvelope(t, 0f, 0.01f, 0.06f);
                float freq = 1200f - 800f * tNorm;
                phase += 2f * Mathf.PI * freq / SAMPLE_RATE;
                data[i] = Mathf.Sin(phase) * env * 0.35f;
            }
            ApplyFades(data, 4, 16);
            ApplyReverb(data, 8f, 0.1f, 2);
            AudioClip clip = AudioClip.Create("NoiseBurst", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 9. 블록 착지 - Sine+Noise(LP500Hz), 180Hz, -60Hz 하강
        /// </summary>
        public static AudioClip CreateBounce(float duration = 0.12f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(42);
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float env = NoteEnvelope(t, 0f, 0.02f, 0.1f);
                float freq = 180f - 60f * tNorm;
                phase += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float sine = Mathf.Sin(phase) * 0.7f;
                float noiseEnv = t < 0.03f ? (1f - t / 0.03f) : 0f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * noiseEnv * 0.3f;
                data[i] = (sine + noise) * env * 0.3f;
            }
            ApplyFades(data, 4, 32);
            ApplyLowPass(data, 0.3f);
            ApplyReverb(data, 10f, 0.1f, 2);
            AudioClip clip = AudioClip.Create("Bounce", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 10. 캐스케이드 개별 음 - Sine+Triangle, 펜타토닉 단일 노트
        /// </summary>
        public static AudioClip CreateCascadeNote(float frequency, float duration = 0.15f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = NoteEnvelope(t, 0f, 0.04f, 0.12f);
                float phase = 2f * Mathf.PI * frequency * t;
                float sample = Mathf.Sin(phase) * 0.65f
                             + GenerateWaveform(Waveform.Triangle, phase) * 0.35f;
                data[i] = sample * env * 0.45f;
            }
            ApplyFades(data, 4, 64);
            ApplyReverb(data, 25f, 0.2f, 3);
            AudioClip clip = AudioClip.Create("Cascade", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 11. 경고 비프 - Sine+Square(5%), A5 2회 반복, 80ms 간격
        /// </summary>
        public static AudioClip CreateWarningBeep(float duration = 0.25f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float sample = 0f;
                for (int n = 0; n < 2; n++)
                {
                    float noteStart = n * 0.08f;
                    float noteTime = t - noteStart;
                    if (noteTime < 0f) continue;
                    float env = NoteEnvelope(noteTime, 0.01f, 0.05f, 0.05f);
                    float vibrato = 1f + 0.03f * Mathf.Sin(2f * Mathf.PI * 6f * noteTime);
                    float phase = 2f * Mathf.PI * 880f * vibrato * noteTime;
                    float tone = Mathf.Sin(phase) * 0.95f
                               + GenerateWaveform(Waveform.Square, phase) * 0.05f;
                    sample += tone * env;
                }
                data[i] = sample * 0.45f;
            }
            ApplyFades(data, 16, 48);
            ApplyLowPass(data, 0.2f);
            ApplyReverb(data, 10f, 0.1f, 2);
            AudioClip clip = AudioClip.Create("Warning", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ============================================================
        // 특수 블록 사운드 (파스텔 톤 - 뮤직박스/장난감 느낌)
        // ============================================================

        /// <summary>
        /// 드릴 - 귀여운 장난감 오르골 태엽 소리, 부드러운 윙윙
        /// </summary>
        public static AudioClip CreateDrillSound(float duration = 0.5f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.02f, 0.1f, 0.6f, 0.3f);
                float clickEnv = t < 0.02f ? (1f - t / 0.02f) : 0f;
                float click = Mathf.Sin(2f * Mathf.PI * 2000f * t) * clickEnv * 0.3f;
                float whirFreq = Mathf.Lerp(800f, 1000f, tNorm * 0.5f);
                float tremolo = 1f + 0.2f * Mathf.Sin(2f * Mathf.PI * 15f * t);
                float whirPhase = 2f * Mathf.PI * whirFreq * t;
                float whir = Mathf.Sin(whirPhase) * 0.3f * tremolo;
                float musicBox = Mathf.Sin(2f * Mathf.PI * 1568f * t) * 0.15f
                               * Mathf.Max(0f, 1f - t * 4f);
                data[i] = (click + whir + musicBox) * envelope * 0.4f;
            }
            ApplyFades(data, 32, 128);
            ApplyLowPass(data, 0.2f);
            ApplyReverb(data, 20f, 0.2f, 3);
            AudioClip clip = AudioClip.Create("Drill", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 폭탄 - 부드러운 "퐁" 꽃가루 캐논, 따뜻하고 둥근 저역 + 반짝이 고역
        /// </summary>
        public static AudioClip CreateExplosion(float duration = 0.3f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(123);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.01f, 0.15f, 0.4f, 0.4f);
                float bodyFreq = Mathf.Lerp(250f, 120f, tNorm);
                float body = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * 0.5f;
                float poof = (float)(rng.NextDouble() * 2.0 - 1.0)
                           * Mathf.Max(0f, 1f - tNorm * 3f) * 0.25f;
                float sparkle = Mathf.Sin(2f * Mathf.PI * 2200f * t) * 0.12f
                              * Mathf.Max(0f, 1f - tNorm * 2f);
                data[i] = (body + poof + sparkle) * envelope * 0.4f;
            }
            ApplyFades(data, 16, 96);
            ApplyLowPass(data, 0.25f);
            ApplyReverb(data, 25f, 0.25f, 3);
            AudioClip clip = AudioClip.Create("Explosion", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 레이저 - 요정 지팡이 쉬머 빔, 크리스탈 "띵" + 지속 쉬머
        /// </summary>
        public static AudioClip CreateLaserSound(float duration = 0.5f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.01f, 0.1f, 0.55f, 0.35f);
                float ting = Mathf.Sin(2f * Mathf.PI * 2093f * t) * 0.4f
                           * Mathf.Max(0f, 1f - t * 8f);
                float shimmerFreq = Mathf.Lerp(1200f, 1800f, tNorm);
                float shimmer = Mathf.Sin(2f * Mathf.PI * shimmerFreq * t) * 0.25f;
                float vibrato = 0.08f * Mathf.Sin(2f * Mathf.PI * 4f * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * shimmerFreq * 1.5f * t) * 0.15f
                               * (1f + vibrato);
                data[i] = (ting + shimmer + harmonic) * envelope * 0.4f;
            }
            ApplyFades(data, 16, 128);
            ApplyLowPass(data, 0.15f);
            ApplyReverb(data, 35f, 0.3f, 4);
            AudioClip clip = AudioClip.Create("Laser", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 도넛/레인보우 - 상승 차임 + 따뜻한 확장 쉬머 물결
        /// </summary>
        public static AudioClip CreateRainbowSound(float duration = 0.8f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(77);
            float[] chimeNotes = { 1046.5f, 1318.5f, 1568f, 2093f }; // C6,E6,G6,C7
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.02f, 0.1f, 0.55f, 0.35f);
                float sample = 0f;
                for (int n = 0; n < chimeNotes.Length; n++)
                {
                    float noteStart = n * 0.04f;
                    float noteTime = t - noteStart;
                    if (noteTime < 0f) continue;
                    float env = NoteEnvelope(noteTime, 0f, 0.05f, 0.3f);
                    sample += Mathf.Sin(2f * Mathf.PI * chimeNotes[n] * noteTime)
                            * env * 0.2f;
                }
                float shimmerFreq = Mathf.Lerp(800f, 1600f, tNorm * 0.5f);
                float shimmer = Mathf.Sin(2f * Mathf.PI * shimmerFreq * t) * 0.15f
                              + GenerateWaveform(Waveform.Triangle, 2f * Mathf.PI * shimmerFreq * 0.5f * t) * 0.1f;
                float windChime = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.04f
                                * (0.3f + 0.7f * tNorm);
                data[i] = (sample + shimmer + windChime) * envelope * 0.4f;
            }
            ApplyFades(data, 32, 192);
            ApplyLowPass(data, 0.15f);
            ApplyReverb(data, 40f, 0.3f, 4);
            AudioClip clip = AudioClip.Create("Rainbow", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// X블록 - 두 크리스탈 "띵" 교차 + 스파클 확산
        /// </summary>
        public static AudioClip CreateXBlockSound(float duration = 0.6f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(88);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.01f, 0.1f, 0.5f, 0.4f);
                float ting1Env = Mathf.Max(0f, 1f - t * 6f);
                float ting1 = Mathf.Sin(2f * Mathf.PI * 2093f * t) * ting1Env * 0.35f;
                float ting2Time = t - 0.02f;
                float ting2Env = ting2Time > 0f ? Mathf.Max(0f, 1f - ting2Time * 6f) : 0f;
                float ting2 = ting2Time > 0f
                    ? Mathf.Sin(2f * Mathf.PI * 2637f * ting2Time) * ting2Env * 0.3f : 0f;
                float sparkle = 0f;
                if (tNorm > 0.05f)
                {
                    float spFreq = 1568f + (float)(rng.NextDouble() * 400f);
                    sparkle = Mathf.Sin(2f * Mathf.PI * spFreq * t) * 0.12f
                            * Mathf.Max(0f, 1f - (tNorm - 0.05f) * 1.5f);
                }
                float body = Mathf.Sin(2f * Mathf.PI * 784f * t) * 0.15f
                           * Mathf.Max(0f, 1f - tNorm * 2f);
                data[i] = (ting1 + ting2 + sparkle + body) * envelope * 0.4f;
            }
            ApplyFades(data, 16, 128);
            ApplyLowPass(data, 0.15f);
            ApplyReverb(data, 30f, 0.28f, 4);
            AudioClip clip = AudioClip.Create("XBlock", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 드론 - 프로펠러 윙윙 상승 + 급하강 타격음
        /// 부드러운 윙윙거림이 점점 높아지다가 급하강하며 타격
        /// </summary>
        public static AudioClip CreateDroneSound(float duration = 0.5f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(99);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.03f, 0.1f, 0.6f, 0.3f);

                // 프로펠러 윙윙: 삼각파 + 약간의 노이즈 (주파수 상승)
                float propFreq = Mathf.Lerp(200f, 600f, Mathf.Min(tNorm * 1.5f, 1f));
                float propeller = GenerateWaveform(Waveform.Triangle, 2f * Mathf.PI * propFreq * t) * 0.25f;

                // 프로펠러 블레이드 모듈레이션 (15~25Hz)
                float bladeModFreq = Mathf.Lerp(15f, 25f, tNorm);
                float bladeMod = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * bladeModFreq * t);
                propeller *= bladeMod;

                // 급하강 타격 (후반 30%)
                float strikeComponent = 0f;
                if (tNorm > 0.7f)
                {
                    float strikeT = (tNorm - 0.7f) / 0.3f;
                    float strikeFreq = Mathf.Lerp(800f, 150f, strikeT); // 급하강
                    strikeComponent = Mathf.Sin(2f * Mathf.PI * strikeFreq * t)
                                    * 0.35f * Mathf.Max(0f, 1f - strikeT * 1.5f);
                    // 임팩트 노이즈
                    if (strikeT > 0.5f)
                    {
                        float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                        strikeComponent += noise * 0.15f * Mathf.Max(0f, 1f - (strikeT - 0.5f) * 4f);
                    }
                }

                // 고주파 윙 (장식음)
                float whine = Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.08f
                            * Mathf.Max(0f, 1f - tNorm * 2.5f);

                data[i] = (propeller + strikeComponent + whine) * envelope * 0.45f;
            }
            ApplyFades(data, 16, 128);
            ApplyLowPass(data, 0.12f);
            ApplyReverb(data, 25f, 0.22f, 3);
            AudioClip clip = AudioClip.Create("Drone", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 특수 젬 생성 - 크리스탈 쉬머 빌드업 + "띵글링" + 스파클 꼬리
        /// </summary>
        public static AudioClip CreateSpecialGemSound(float duration = 0.5f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.02f, 0.1f, 0.55f, 0.35f);
                float buildUp = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(800f, 1500f, Mathf.Min(tNorm * 2.5f, 1f)) * t)
                              * 0.2f * Mathf.Min(tNorm * 5f, 1f);
                float tingTime = t - 0.2f * duration;
                float ting = 0f;
                if (tingTime > 0f)
                {
                    float tingEnv = Mathf.Max(0f, 1f - tingTime * 4f);
                    ting = Mathf.Sin(2f * Mathf.PI * 2093f * tingTime) * 0.35f * tingEnv
                         + Mathf.Sin(2f * Mathf.PI * 2637f * tingTime) * 0.2f * tingEnv;
                }
                float sparkle = Mathf.Sin(2f * Mathf.PI * 3136f * t) * 0.08f
                              * Mathf.Max(0f, tNorm - 0.3f);
                data[i] = (buildUp + ting + sparkle) * envelope * 0.4f;
            }
            ApplyFades(data, 16, 128);
            ApplyLowPass(data, 0.15f);
            ApplyReverb(data, 30f, 0.28f, 3);
            AudioClip clip = AudioClip.Create("SpecialGem", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 스테이지 클리어 팡파레 - 오르골 축하 멜로디 (상행 장조)
        /// </summary>
        public static AudioClip CreateVictoryFanfare(float duration = 2.0f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            // "da-da-da-DAAA" 패턴: C5, E5, G5, C6(길게)
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            float[] starts = { 0f, 0.25f, 0.5f, 0.8f };
            float[] lengths = { 0.2f, 0.2f, 0.25f, 1.0f };
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float noteTime = t - starts[n];
                    if (noteTime < 0f || noteTime > lengths[n]) continue;
                    float env = NoteEnvelope(noteTime, 0.01f, lengths[n] * 0.3f, lengths[n] * 0.6f);
                    float vibrato = 1f + 0.008f * Mathf.Sin(2f * Mathf.PI * 5f * noteTime)
                                  * Mathf.Min(noteTime * 3f, 1f);
                    float phase = 2f * Mathf.PI * notes[n] * vibrato * noteTime;
                    float tone = Mathf.Sin(phase) * 0.6f
                               + GenerateWaveform(Waveform.Triangle, phase) * 0.3f
                               + Mathf.Sin(phase * 2f) * 0.1f;
                    if (n == notes.Length - 1)
                    {
                        float shimmer = 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 3f * noteTime);
                        tone *= shimmer;
                    }
                    sample += tone * env * 0.35f;
                }
                float bellTime = t - 0.85f;
                if (bellTime > 0f && bellTime < 0.5f)
                {
                    float bellEnv = Mathf.Max(0f, 1f - bellTime * 2f);
                    sample += Mathf.Sin(2f * Mathf.PI * 2093f * bellTime) * 0.1f * bellEnv;
                }
                float trailShimmer = tNorm > 0.8f
                    ? Mathf.Sin(2f * Mathf.PI * 1568f * t) * 0.05f * (1f - (tNorm - 0.8f) * 5f)
                    : 0f;
                data[i] = sample + trailShimmer;
            }
            ApplyFades(data, 48, 512);
            ApplyLowPass(data, 0.1f);
            ApplyReverb(data, 45f, 0.3f, 4);
            AudioClip clip = AudioClip.Create("VictoryFanfare", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 게임 오버 - 오르골이 천천히 멈추는 하행 단조 멜로디
        /// </summary>
        public static AudioClip CreateGameOverSound(float duration = 1.5f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            // 하행 단조: A4, F4, D4, (느리게)
            float[] notes = { 440f, 349.23f, 293.66f };
            float[] starts = { 0f, 0.35f, 0.75f };
            float[] lengths = { 0.4f, 0.45f, 0.7f };
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float sample = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float noteTime = t - starts[n];
                    if (noteTime < 0f || noteTime > lengths[n]) continue;
                    float env = NoteEnvelope(noteTime, 0.02f, lengths[n] * 0.25f, lengths[n] * 0.7f);
                    float slowDown = 1f - 0.02f * tNorm;
                    float phase = 2f * Mathf.PI * notes[n] * slowDown * noteTime;
                    float tone = Mathf.Sin(phase) * 0.6f
                               + GenerateWaveform(Waveform.Triangle, phase) * 0.3f;
                    sample += tone * env * 0.3f;
                }
                float pad = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.08f
                          + Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.06f;
                pad *= Mathf.Max(0f, 1f - tNorm * 0.5f);
                data[i] = sample + pad;
            }
            ApplyFades(data, 64, 384);
            ApplyLowPass(data, 0.2f);
            ApplyReverb(data, 50f, 0.35f, 4);
            AudioClip clip = AudioClip.Create("GameOver", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ============================================================
        // 유틸리티 (하위 호환)
        // ============================================================

        public static AudioClip CreateComboRise(float duration)
        {
            return CreateCascadeNote(523.25f, duration);
        }

        public static AudioClip CreateImpact(float duration)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(99);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.003f, 0.08f, 0.3f, 0.55f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - tNorm * 0.8f);
                float subPhase = 2f * Mathf.PI * Mathf.Lerp(200f, 60f, tNorm) * t;
                float sub = Mathf.Sin(subPhase) * 0.5f;
                float mid = Mathf.Sin(2f * Mathf.PI * 400f * t) * 0.2f * (1f - tNorm);
                data[i] = (noise * 0.3f + sub + mid) * envelope * 0.5f;
            }
            ApplyFades(data, 8, 64);
            ApplyLowPass(data, 0.3f);
            ApplyReverb(data, 20f, 0.2f, 2);
            AudioClip clip = AudioClip.Create("Impact", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 특수 블록 변환 사운드 — 짧고 밝은 "틱!" (XBlock 합성에서 블록이 하나씩 변환될 때)
        /// 상승 톤 + 금속성 클릭으로 변환 느낌 연출
        /// </summary>
        public static AudioClip CreateTransformTick(float duration = 0.08f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = (1f - tNorm) * (1f - tNorm); // 빠른 감쇠
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(1200f, 1800f, tNorm) * t); // 상승 톤
                float click = Mathf.Sin(2f * Mathf.PI * 4000f * t) * (1f - tNorm * 3f); // 금속 클릭
                click = Mathf.Clamp01(click) * 0.3f;
                data[i] = (tone * 0.6f + click) * envelope * 0.4f;
            }
            ApplyFades(data, 4, 32);
            AudioClip clip = AudioClip.Create("TransformTick", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 적군 스폰 사운드 — 저음 럼블 + 불길한 단조 톤 (0.25s)
        /// </summary>
        public static AudioClip CreateEnemySpawnSound(float duration = 0.25f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(777);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float envelope = ADSR(tNorm, 0.01f, 0.15f, 0.4f, 0.3f);

                // 저음 럼블 (60Hz → 40Hz 하강)
                float rumbleFreq = Mathf.Lerp(60f, 40f, tNorm);
                float rumble = Mathf.Sin(2f * Mathf.PI * rumbleFreq * t) * 0.5f;

                // 불길한 단조 톤 (Eb minor: Eb4=311Hz, Gb4=370Hz)
                float minor1 = Mathf.Sin(2f * Mathf.PI * 311f * t) * 0.2f * (1f - tNorm);
                float minor2 = Mathf.Sin(2f * Mathf.PI * 370f * t) * 0.15f * (1f - tNorm * 1.2f);

                // 노이즈 텍스처
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.1f * (1f - tNorm);

                data[i] = (rumble + minor1 + minor2 + noise) * envelope * 0.45f;
            }

            ApplyFades(data, 8, 64);
            ApplyLowPass(data, 0.35f);
            ApplyReverb(data, 15f, 0.25f, 2);

            AudioClip clip = AudioClip.Create("EnemySpawn", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ============================================================
        // BGM 메서드
        // ============================================================

        public static AudioClip CreateLobbySereneBGM(float duration = 120f)
        {
            return AudioClip.Create("LobbySereneBGM", Mathf.CeilToInt(44100 * duration), 1, 44100, false);
        }

        public static AudioClip CreateLobbyBrightBGM(float duration = 120f)
        {
            return AudioClip.Create("LobbyBrightBGM", Mathf.CeilToInt(44100 * duration), 1, 44100, false);
        }

        public static AudioClip CreateLobbyDreamyBGM(float duration = 120f)
        {
            return AudioClip.Create("LobbyDreamyBGM", Mathf.CeilToInt(44100 * duration), 1, 44100, false);
        }

        public static AudioClip CreateGameplayTenseBGM(float duration = 90f)
        {
            return AudioClip.Create("GameplayTenseBGM", Mathf.CeilToInt(44100 * duration), 1, 44100, false);
        }

        public static AudioClip CreateGameplayEnergeticBGM(float duration = 90f)
        {
            return AudioClip.Create("GameplayEnergeticBGM", Mathf.CeilToInt(44100 * duration), 1, 44100, false);
        }

        public static AudioClip CreateGameplayEpicBGM(float duration = 120f)
        {
            return AudioClip.Create("GameplayEpicBGM", Mathf.CeilToInt(44100 * duration), 1, 44100, false);
        }
    }
}

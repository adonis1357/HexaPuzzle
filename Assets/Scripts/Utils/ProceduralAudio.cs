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

        /// <summary>
        /// 양방향 피크 정규화: 피크가 목표보다 크면 줄이고, 작으면 키움
        /// 모든 클립이 일관된 볼륨 수준을 갖도록 보장
        /// </summary>
        private static void Normalize(float[] data, float targetPeak = 0.9f)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++)
                max = Mathf.Max(max, Mathf.Abs(data[i]));
            // 무음(0.001 미만)이면 정규화 건너뜀
            if (max < 0.001f) return;
            // 피크와 목표 차이가 1% 이상이면 스케일 조정 (상향+하향 모두)
            if (Mathf.Abs(max - targetPeak) > 0.01f)
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
                float freq = 640f - 160f * tNorm;
                phase += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float sine = Mathf.Sin(phase) * 0.7f;
                float noiseEnv = t < 0.03f ? (1f - t / 0.03f) : 0f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * noiseEnv * 0.3f;
                data[i] = (sine + noise) * env * 0.3f;
            }
            ApplyFades(data, 4, 32);
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
        /// 모기 날개짓 "윙~" 사운드 — 비행 중 연속 재생, 충돌 시 수동 정지
        /// 500~700Hz 고음 사인파 기반 + 얕은 AM + 피치 워블로 날카로운 지속 윙 소리
        /// </summary>
        public static AudioClip CreateDroneSound(float duration = 2.0f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase = 0f;        // 메인 윙 위상
            float whinePhase = 0f;   // 초고주파 배음 위상

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;

                // 엔벨로프: 빠른 페이드인 → 지속 → 느린 페이드아웃
                float env = 1f;
                if (tNorm < 0.04f) env = tNorm / 0.04f;
                else if (tNorm > 0.96f) env = (1f - tNorm) / 0.04f;

                // 메인 윙 주파수: 550~650Hz (모기 날개짓 대역) + 느린 피치 워블
                float wobble = Mathf.Sin(2f * Mathf.PI * 4f * t) * 35f;         // 4Hz 피치 흔들림
                float drift = Mathf.Sin(2f * Mathf.PI * 0.5f * t) * 20f;        // 0.5Hz 느린 이동
                float whineFreq = 600f + wobble + drift;

                // 위상 누적 (클릭 방지)
                phase += 2f * Mathf.PI * whineFreq / SAMPLE_RATE;

                // 메인 톤: 순수 사인파 (모기의 깨끗한 "윙~" 소리)
                float mainTone = Mathf.Sin(phase) * 0.50f;

                // 2배음 (1200Hz대): 모기 특유의 날카로움 추가
                float harmonic2 = Mathf.Sin(phase * 2.0f) * 0.18f;

                // 3배음 (1800Hz대): 얇고 찌르는 듯한 느낌
                float harmonic3 = Mathf.Sin(phase * 3.0f) * 0.08f;

                // 얕은 AM 모듈레이션: 날개짓 떨림 (500Hz, 깊이 15%)
                // 모기는 파리보다 AM이 얕아서 더 지속적인 "윙~" 느낌
                float wingAM = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 500f * t);
                float tone = (mainTone + harmonic2 + harmonic3) * wingAM;

                // 초고주파 윙: ~3200Hz (귀에 거슬리는 모기 특유음, 아주 약하게)
                whinePhase += 2f * Mathf.PI * 3200f / SAMPLE_RATE;
                float ultraWhine = Mathf.Sin(whinePhase) * 0.03f;

                data[i] = (tone + ultraWhine) * env * 0.45f;
            }
            ApplyFades(data, 32, 32);
            AudioClip clip = AudioClip.Create("Drone", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 드론 타격/파괴 사운드 — 임팩트 크런치 + 파편 산개
        /// 짧고 강렬한 충돌음, 파괴 시 사용
        /// </summary>
        public static AudioClip CreateDroneStrikeSound(float duration = 0.2f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(77);
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;

                // 임팩트 톤: 고음에서 저음으로 급강하 (600→80Hz)
                float freq = 600f - 520f * tNorm;
                phase += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float impactEnv = tNorm < 0.15f ? 1f : Mathf.Max(0f, 1f - (tNorm - 0.15f) / 0.85f);
                float impact = Mathf.Sin(phase) * 0.5f * impactEnv;

                // 크런치 노이즈: 초반 강한 노이즈 + 빠른 감쇠
                float noiseEnv = tNorm < 0.1f ? 1f : Mathf.Max(0f, 1f - (tNorm - 0.1f) * 3f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.4f * noiseEnv;

                // 파편 산개음: 중반부터 고주파 흩어지는 소리
                float debrisEnv = tNorm > 0.1f && tNorm < 0.7f
                    ? Mathf.Sin((tNorm - 0.1f) / 0.6f * Mathf.PI) : 0f;
                float debris = Mathf.Sin(2f * Mathf.PI * (1800f - 1200f * tNorm) * t) * 0.12f * debrisEnv;

                data[i] = (impact + noise + debris) * 0.5f;
            }
            ApplyFades(data, 4, 64);
            ApplyLowPass(data, 0.2f);
            ApplyReverb(data, 15f, 0.18f, 2);
            AudioClip clip = AudioClip.Create("DroneStrike", sampleCount, 1, SAMPLE_RATE, false);
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
            Normalize(data, 0.85f);
            AudioClip clip = AudioClip.Create("TransformTick", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 적군 스폰 사운드 — 저음 럼블 + 불길한 단조 톤 (0.25s)
        /// </summary>
        /// <summary>
        /// 고블린 소환 사운드 — 시공간 수축 도플러 효과 (0.35s)
        /// 고주파→저주파 빠른 하강 스윕 + 공간 수축 우웅 + 짧은 임팩트
        /// </summary>
        public static AudioClip CreateEnemySpawnSound(float duration = 0.35f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(777);

            float phase1 = 0f; // 도플러 스윕 위상
            float phase2 = 0f; // 서브베이스 위상
            float phase3 = 0f; // 와블 위상

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;

                // ── 엔벨로프: 즉각 어택, 빠른 수축 ──
                float env;
                if (tNorm < 0.05f)
                    env = tNorm / 0.05f; // 매우 빠른 어택
                else if (tNorm < 0.6f)
                    env = 1f - 0.3f * ((tNorm - 0.05f) / 0.55f); // 서서히 감쇠
                else
                    env = 0.7f * (1f - (tNorm - 0.6f) / 0.4f); // 릴리즈
                env = Mathf.Max(0f, env);

                // ── 레이어 1: 도플러 스윕 (1800Hz → 80Hz 지수 하강) ──
                // 시공간이 빠르게 수축하는 느낌 — 접근하는 물체의 주파수 변화
                float sweepT = tNorm * tNorm; // 가속 커브 (후반부 급격히 내려감)
                float sweepFreq = 1800f * Mathf.Pow(80f / 1800f, sweepT);
                phase1 += 2f * Mathf.PI * sweepFreq / SAMPLE_RATE;
                float sweep = Mathf.Sin(phase1) * 0.45f;
                // 스윕 후반부에 saw 텍스처 혼합 (금속성)
                float sawMix = tNorm > 0.3f ? (tNorm - 0.3f) / 0.7f * 0.3f : 0f;
                float sawPhase = (phase1 % (2f * Mathf.PI)) / (2f * Mathf.PI);
                sweep = sweep * (1f - sawMix) + (2f * sawPhase - 1f) * sawMix * 0.35f;

                // ── 레이어 2: 서브베이스 우웅 (50Hz → 35Hz) ──
                float subFreq = Mathf.Lerp(50f, 35f, tNorm);
                phase2 += 2f * Mathf.PI * subFreq / SAMPLE_RATE;
                float sub = Mathf.Sin(phase2) * 0.4f;

                // ── 레이어 3: 공간 와블 (300Hz 중심, AM 변조) ──
                float woblFreq = 300f * (1f - tNorm * 0.5f);
                phase3 += 2f * Mathf.PI * woblFreq / SAMPLE_RATE;
                float amMod = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 18f * t); // 18Hz AM
                float wobl = Mathf.Sin(phase3) * amMod * 0.2f * (1f - tNorm);

                // ── 레이어 4: 수축 임팩트 (끝부분 짧은 펀치) ──
                float impact = 0f;
                if (tNorm > 0.55f && tNorm < 0.75f)
                {
                    float impT = (tNorm - 0.55f) / 0.2f;
                    impact = Mathf.Sin(2f * Mathf.PI * 55f * t) * (1f - impT) * 0.5f;
                }

                // ── 레이어 5: 노이즈 텍스처 (시공간 왜곡감) ──
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.08f * (1f - tNorm * 0.7f);

                data[i] = (sweep + sub + wobl + impact + noise) * env * 0.4f;
            }

            ApplyFades(data, 8, 80);
            ApplyLowPass(data, 0.45f);
            ApplyReverb(data, 12f, 0.3f, 2);

            AudioClip clip = AudioClip.Create("EnemySpawn", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 쉘 변환 사운드 — 벽돌 내려놓는 짧은 둔탁음 (0.08s)
        /// 저주파 임팩트 + 돌 부딪히는 노이즈 텍스처
        /// </summary>
        public static AudioClip CreateShellConvertSound(float duration = 0.08f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(333);

            for (int i = 0; i < sampleCount; i++)
            {
                float tNorm = (float)i / sampleCount;

                // 매우 빠른 감쇠 엔벨로프 (즉시 어택 → 급속 감쇠)
                float env = Mathf.Exp(-tNorm * 8f);

                // 둔탁한 저음 임팩트 (120Hz → 70Hz 빠른 하강)
                float freq = Mathf.Lerp(120f, 70f, tNorm);
                float t = (float)i / SAMPLE_RATE;
                float thud = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;

                // 돌 부딪히는 노이즈 (초반 강하게, 빠르게 감쇠)
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.4f * Mathf.Exp(-tNorm * 12f);

                data[i] = (thud + noise) * env * 0.5f;
            }

            ApplyFades(data, 4, 32);
            ApplyLowPass(data, 0.5f);

            AudioClip clip = AudioClip.Create("ShellConvert", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 미션 완료 사운드 — 상승 아르페지오 차임 (0.4s)
        /// C5→E5→G5 밝은 3화음 + 글로우 리버브
        /// </summary>
        public static AudioClip CreateMissionCompleteSound(float duration = 0.4f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase1 = 0f, phase2 = 0f, phase3 = 0f;

            // C5=523, E5=659, G5=784 (C메이저 화음)
            float[] freqs = { 523.25f, 659.25f, 783.99f };
            // 각 노트 시작 시점 (staggered)
            float[] starts = { 0f, 0.08f, 0.16f };

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                float sample = 0f;

                // 노트 1: C5
                if (t >= starts[0])
                {
                    float nt = t - starts[0];
                    float env = NoteEnvelope(nt, 0.01f, 0.05f, duration - starts[0]);
                    phase1 += 2f * Mathf.PI * freqs[0] / SAMPLE_RATE;
                    sample += Mathf.Sin(phase1) * env * 0.35f;
                }
                // 노트 2: E5
                if (t >= starts[1])
                {
                    float nt = t - starts[1];
                    float env = NoteEnvelope(nt, 0.01f, 0.05f, duration - starts[1]);
                    phase2 += 2f * Mathf.PI * freqs[1] / SAMPLE_RATE;
                    sample += Mathf.Sin(phase2) * env * 0.35f;
                }
                // 노트 3: G5
                if (t >= starts[2])
                {
                    float nt = t - starts[2];
                    float env = NoteEnvelope(nt, 0.01f, 0.05f, duration - starts[2]);
                    phase3 += 2f * Mathf.PI * freqs[2] / SAMPLE_RATE;
                    sample += (Mathf.Sin(phase3) * 0.6f + GenerateWaveform(Waveform.Triangle, phase3) * 0.4f) * env * 0.4f;
                }

                data[i] = sample * 0.5f;
            }

            ApplyFades(data, 8, 96);
            ApplyLowPass(data, 0.18f);
            ApplyReverb(data, 30f, 0.25f, 3);

            AudioClip clip = AudioClip.Create("MissionComplete", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 미션 등장 사운드 — 부드러운 스윕(whoosh) + 밝은 차임(ding) (0.2s)
        /// UI 슬라이드인에 어울리는 경쾌한 효과음
        /// </summary>
        public static AudioClip CreateMissionEntranceSound(float duration = 0.2f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            float phase1 = 0f;
            float phase2 = 0f;
            System.Random rng = new System.Random(321);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;

                // 스윕 (400Hz → 700Hz 상승) — 슬라이드인 느낌
                float sweepFreq = Mathf.Lerp(400f, 700f, tNorm * tNorm);
                phase1 += 2f * Mathf.PI * sweepFreq / SAMPLE_RATE;
                float sweep = GenerateWaveform(Waveform.Triangle, phase1) * 0.35f;
                float sweepEnv = (1f - tNorm) * Mathf.Clamp01(tNorm * 10f); // 빠른 공격 + 감쇠

                // 차임 (E5=659Hz) — 도착 시 밝은 울림
                float chimeFreq = 659.25f;
                phase2 += 2f * Mathf.PI * chimeFreq / SAMPLE_RATE;
                float chime = Mathf.Sin(phase2) * 0.5f;
                float chimeEnv = Mathf.Pow(Mathf.Clamp01(tNorm * 3f - 1.5f), 0.5f) * (1f - Mathf.Pow(tNorm, 2f));

                // 약간의 노이즈 텍스처 (whoosh 느낌)
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.08f * sweepEnv;

                data[i] = (sweep * sweepEnv + chime * chimeEnv + noise) * 0.5f;
            }

            ApplyFades(data, 8, 48);
            ApplyLowPass(data, 0.2f);
            ApplyReverb(data, 20f, 0.15f, 2);

            AudioClip clip = AudioClip.Create("MissionEntrance", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ============================================================
        // BGM 메서드 — 프로시저럴 배경음악 생성
        // 리듬감 있는 루프 비트, 펜타토닉 멜로디 + 드럼 + 베이스
        // ============================================================

        /// <summary>킥 드럼 샘플 (짧은 사인 스윕, 60→30Hz)</summary>
        private static float DrumKick(float st, float vol = 0.09f)
        {
            if (st >= 0.06f) return 0f;
            float e = 1f - st / 0.06f;
            return Mathf.Sin(2f * Mathf.PI * (55f - 25f * st / 0.06f) * st) * e * e * vol;
        }

        /// <summary>하이햇 샘플 (필터드 노이즈 버스트)</summary>
        private static float DrumHat(float st, float noiseVal, float vol = 0.035f)
        {
            if (st >= 0.025f) return 0f;
            return noiseVal * (1f - st / 0.025f) * vol;
        }

        /// <summary>
        /// Heavy 고블린 점프 사운드 — 무거운 몸체가 도약하는 낮은 whoosh.
        /// 저음 서브베이스(70Hz) + 상승 스윕 + 노이즈 퍼프.
        /// </summary>
        public static AudioClip CreateHeavyJumpSound(float duration = 0.28f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(71);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                // 엔벨로프: 빠른 어택 → 빠른 감쇠
                float envelope = ADSR(tNorm, 0.02f, 0.3f, 0.0f, 0.5f);
                // 서브베이스: 70→130Hz 상승 스윕 (도약 느낌)
                float sweepFreq = Mathf.Lerp(70f, 130f, tNorm);
                float sub = Mathf.Sin(2f * Mathf.PI * sweepFreq * t) * 0.6f;
                // 하체 충격 퍼프 노이즈 (초반만)
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0)
                            * Mathf.Max(0f, 1f - tNorm * 4f) * 0.3f;
                // 저음 확장 하모닉
                float body = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.25f * (1f - tNorm);
                data[i] = (sub + noise + body) * envelope * 0.45f;
            }
            ApplyFades(data, 16, 128);
            ApplyLowPass(data, 0.35f);
            ApplyReverb(data, 18f, 0.18f, 2);
            AudioClip clip = AudioClip.Create("HeavyJump", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Heavy 고블린 착지 충격음 — 지진 느낌의 강한 저음 충격.
        /// 초저음(50Hz) 펀치 + 먼지 노이즈 + 짧은 여진 리버브.
        /// </summary>
        public static AudioClip CreateHeavyLandSound(float duration = 0.38f)
        {
            int sampleCount = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sampleCount];
            System.Random rng = new System.Random(83);
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tNorm = (float)i / sampleCount;
                // 초고속 어택 → 강한 감쇠 (쿵! 느낌)
                float envelope = ADSR(tNorm, 0.003f, 0.25f, 0.05f, 0.7f);
                // 초저음 펀치: 50→30Hz 하강 (지면 충격)
                float punchFreq = Mathf.Lerp(50f, 30f, tNorm);
                float punch = Mathf.Sin(2f * Mathf.PI * punchFreq * t) * 0.7f;
                // 중저음 두께: 120Hz
                float mid = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.35f * (1f - tNorm);
                // 먼지 노이즈 (전체 구간, 시간에 따라 감쇠)
                float dust = (float)(rng.NextDouble() * 2.0 - 1.0)
                           * (1f - tNorm * 0.7f) * 0.25f;
                // 고주파 충격 스파크 (초반 5%만)
                float spark = Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.1f
                            * Mathf.Max(0f, 1f - tNorm * 20f);
                data[i] = (punch + mid + dust + spark) * envelope * 0.5f;
            }
            ApplyFades(data, 4, 256);
            ApplyLowPass(data, 0.28f);
            ApplyReverb(data, 30f, 0.3f, 3);
            AudioClip clip = AudioClip.Create("HeavyLand", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>노이즈 테이블 생성 (하이햇/퍼커션용)</summary>
        private static float[] MakeNoiseTable(int seed = 42)
        {
            float[] n = new float[SAMPLE_RATE];
            System.Random r = new System.Random(seed);
            for (int i = 0; i < n.Length; i++)
                n[i] = (float)(r.NextDouble() * 2.0 - 1.0);
            return n;
        }

        /// <summary>
        /// 로비 세레나데 BGM — Lo-fi 칠 비트 (C 펜타토닉, 85BPM)
        /// 붐뱁 킥 + 레이지 하이햇 + 싱코페이션 오르골 멜로디
        /// </summary>
        public static AudioClip CreateLobbySereneBGM(float duration = 120f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            float[] nz = MakeNoiseTable(42);
            // C 펜타토닉 2옥타브: C4, D4, E4, G4, A4, C5, D5, E5
            float[] scale = { 261.63f, 293.66f, 329.63f, 392f, 440f, 523.25f, 587.33f, 659.25f };
            float bpm = 72f; float s16 = 15f / bpm;
            // 4마디(64 steps) — 싱코페이션 훅: E→C'→A→G 반복 변주
            int[] mel = {
                -1, 2,-1,-1, -1, 5,-1,-1,  4,-1, 3,-1, -1,-1, 2,-1,
                -1, 4,-1,-1, -1,-1, 3,-1,  2,-1,-1,-1, -1, 3,-1, 4,
                -1, 2,-1,-1, -1, 5,-1,-1,  4,-1, 3,-1, -1,-1, 1,-1,
                -1, 3,-1,-1, -1, 2,-1, 5, -1, 4,-1, 3, -1,-1, 0,-1 };
            int[] ki = {
                1,0,0,0, 0,0,1,0, 1,0,0,0, 0,0,0,1,
                1,0,0,0, 0,0,1,0, 1,0,0,0, 0,1,0,0,
                1,0,0,0, 0,0,1,0, 1,0,0,0, 0,0,0,1,
                1,0,0,0, 0,0,1,0, 1,0,0,0, 0,0,1,0 };
            int[] hh = {
                2,0,1,0, 2,0,1,0, 2,0,1,0, 2,0,1,1,
                2,0,1,0, 2,0,1,0, 2,0,1,0, 2,0,1,0,
                2,0,1,0, 2,0,1,1, 2,0,1,0, 2,0,1,0,
                2,0,1,0, 2,0,1,0, 2,0,1,0, 2,1,1,0 };
            int[] bas = {
                0,-1,-1,-1, -1,-1, 0,-1,  2,-1,-1,-1, -1,-1, 2,-1,
                0,-1,-1,-1, -1,-1, 3,-1,  2,-1,-1,-1, -1,-1, 0,-1,
                0,-1,-1,-1, -1,-1, 0,-1,  2,-1,-1,-1, -1,-1, 3,-1,
                0,-1,-1,-1, -1,-1, 2,-1,  0,-1,-1,-1, -1,-1, 0,-1 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, mA = 99f, bA = 99f;
            float mF = scale[4], bF = scale[0] * 0.5f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen; float st = pt - step * s16;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    if (bas[step] >= 0) { bF = scale[bas[step]] * 0.5f; bA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                v += (Mathf.Sin(mPh) * 0.7f + GenerateWaveform(Waveform.Triangle, mPh) * 0.3f)
                     * NoteEnvelope(mA, 0.02f, s16 * 1.5f, s16 * 8f) * 0.16f;
                mA += dt;
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.02f, s16 * 3f, s16 * 10f) * 0.08f;
                bA += dt;
                if (ki[step] > 0) v += DrumKick(st);
                if (hh[step] > 0) v += DrumHat(st, nz[i % nz.Length], hh[step] == 2 ? 0.035f : 0.018f);
                v += Mathf.Sin(2f * Mathf.PI * 130.81f * t) * 0.04f;
                data[i] = v;
            }
            ApplyLowPass(data, 0.15f);
            ApplyReverb(data, 45f, 0.3f, 4);
            Normalize(data, 0.5f);
            AudioClip clip = AudioClip.Create("LobbySereneBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 로비 밝은 BGM — 바운시 팝 비트 (G 펜타토닉, 100BPM)
        /// 경쾌한 스타카토 멜로디 + 타이트 킥 + 밝은 하이햇
        /// </summary>
        public static AudioClip CreateLobbyBrightBGM(float duration = 120f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            float[] nz = MakeNoiseTable(77);
            // G 펜타토닉 2옥타브: G4, A4, B4, D5, E5, G5, A5, B5
            float[] scale = { 392f, 440f, 493.88f, 587.33f, 659.25f, 783.99f, 880f, 987.77f };
            float bpm = 84f; float s16 = 15f / bpm;
            // 바운시 멜로디 — B-B-D'-E' 반복 모티프
            int[] mel = {
                2,-1, 2,-1, 3,-1,-1, 4, -1, 3,-1,-1,  2,-1, 1,-1,
                1,-1, 2,-1, 3,-1, 3,-1, -1, 4,-1,-1, -1, 2,-1,-1,
                2,-1, 2,-1, 3,-1,-1, 5, -1, 4,-1, 3, -1, 2,-1,-1,
                4,-1, 3,-1, -1, 2,-1, 1, -1, 0,-1,-1, -1, 2,-1,-1 };
            int[] ki = {
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0,
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0,
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,1,
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0 };
            int[] hh = {
                1,0,1,0, 1,0,1,1, 1,0,1,0, 1,0,1,0,
                1,0,1,0, 1,0,1,1, 1,0,1,0, 1,0,1,0,
                1,0,1,0, 1,0,1,0, 1,0,1,1, 1,0,1,0,
                1,0,1,0, 1,0,1,0, 1,0,1,0, 1,1,1,0 };
            int[] bas = {
                0,-1,-1,-1, 0,-1,-1,-1, 2,-1,-1,-1, 2,-1,-1,-1,
                0,-1,-1,-1, 0,-1,-1,-1, 3,-1,-1,-1, 3,-1,-1,-1,
                0,-1,-1,-1, 0,-1,-1,-1, 2,-1,-1,-1, 2,-1, 3,-1,
                3,-1,-1,-1, 2,-1,-1,-1, 0,-1,-1,-1, 0,-1,-1,-1 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, mA = 99f, bA = 99f;
            float mF = scale[2], bF = scale[0] * 0.5f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen; float st = pt - step * s16;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    if (bas[step] >= 0) { bF = scale[bas[step]] * 0.5f; bA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                v += (Mathf.Sin(mPh) * 0.65f + GenerateWaveform(Waveform.Triangle, mPh) * 0.35f)
                     * NoteEnvelope(mA, 0.015f, s16 * 1.5f, s16 * 7f) * 0.16f;
                mA += dt;
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.02f, s16 * 3f, s16 * 10f) * 0.07f;
                bA += dt;
                if (ki[step] > 0) v += DrumKick(st, 0.08f);
                if (hh[step] > 0) v += DrumHat(st, nz[i % nz.Length], 0.03f);
                // 밝은 쉬머
                v += Mathf.Sin(2f * Mathf.PI * mF * 2f * t) * 0.02f;
                data[i] = v;
            }
            ApplyLowPass(data, 0.12f);
            ApplyReverb(data, 35f, 0.25f, 4);
            Normalize(data, 0.5f);
            AudioClip clip = AudioClip.Create("LobbyBrightBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 로비 몽환 BGM — 앰비언트 그루브 (F 펜타토닉, 72BPM)
        /// 여백 많은 멜로디 + 미니멀 킥 + 넓은 리버브 패드
        /// </summary>
        public static AudioClip CreateLobbyDreamyBGM(float duration = 120f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            float[] nz = MakeNoiseTable(99);
            // F 펜타토닉 2옥타브: F4, G4, A4, C5, D5, F5, G5, A5
            float[] scale = { 349.23f, 392f, 440f, 523.25f, 587.33f, 698.46f, 783.99f, 880f };
            float bpm = 60f; float s16 = 15f / bpm;
            // 넓은 도약, 여백 있는 멜로디
            int[] mel = {
                -1,-1, 2,-1, -1,-1,-1,-1, -1,-1, 5,-1, -1,-1,-1,-1,
                -1,-1, 4,-1, -1,-1,-1,-1, -1,-1, 3,-1, -1,-1,-1,-1,
                -1,-1, 2,-1, -1,-1,-1,-1, -1,-1, 6,-1, -1,-1,-1,-1,
                -1,-1, 5,-1, -1,-1,-1,-1, -1,-1, 3,-1, -1,-1, 0,-1 };
            int[] ki = {
                1,0,0,0, 0,0,0,0, 1,0,0,0, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,0,0, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,0,0, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,0,0, 0,0,0,0 };
            int[] hh = {
                0,0,0,0, 1,0,0,0, 0,0,0,0, 1,0,0,0,
                0,0,0,0, 1,0,0,0, 0,0,0,0, 1,0,0,0,
                0,0,0,0, 1,0,0,0, 0,0,0,0, 1,0,0,0,
                0,0,0,0, 1,0,0,0, 0,0,0,0, 1,0,0,0 };
            int[] bas = {
                0,-1,-1,-1, -1,-1,-1,-1, 0,-1,-1,-1, -1,-1,-1,-1,
                3,-1,-1,-1, -1,-1,-1,-1, 3,-1,-1,-1, -1,-1,-1,-1,
                0,-1,-1,-1, -1,-1,-1,-1, 2,-1,-1,-1, -1,-1,-1,-1,
                3,-1,-1,-1, -1,-1,-1,-1, 0,-1,-1,-1, -1,-1,-1,-1 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, mA = 99f, bA = 99f;
            float mF = scale[2], bF = scale[0] * 0.5f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen; float st = pt - step * s16;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    if (bas[step] >= 0) { bF = scale[bas[step]] * 0.5f; bA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                v += (Mathf.Sin(mPh) * 0.75f + GenerateWaveform(Waveform.Triangle, mPh) * 0.25f)
                     * NoteEnvelope(mA, 0.04f, s16 * 2f, s16 * 12f) * 0.15f;
                mA += dt;
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.03f, s16 * 4f, s16 * 12f) * 0.07f;
                bA += dt;
                if (ki[step] > 0) v += DrumKick(st, 0.06f);
                if (hh[step] > 0) v += DrumHat(st, nz[i % nz.Length], 0.025f);
                // 몽환 패드 (5도 하모니)
                float padF = mF * 0.667f;
                v += Mathf.Sin(2f * Mathf.PI * padF * t) * 0.06f;
                data[i] = v;
            }
            ApplyLowPass(data, 0.10f);
            ApplyReverb(data, 60f, 0.35f, 5);
            Normalize(data, 0.45f);
            AudioClip clip = AudioClip.Create("LobbyDreamyBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 게임플레이 긴장 BGM — 다크 드라이브 (A단조 펜타토닉, 120BPM)
        /// 포 온 더 플로어 킥 + 16분 하이햇 + 반복 리프 멜로디
        /// </summary>
        public static AudioClip CreateGameplayTenseBGM(float duration = 90f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            float[] nz = MakeNoiseTable(55);
            // A 단조 펜타토닉 2옥타브: A4, C5, D5, E5, G5, A5, C6, D6
            float[] scale = { 440f, 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.66f };
            float bpm = 100f; float s16 = 15f / bpm;
            // 드라이빙 리프 — A-A-C-D 반복 + 변주
            int[] mel = {
                0,-1, 0,-1, 1,-1, 2,-1,  0,-1, 0,-1, 4,-1, 3,-1,
                0,-1, 0,-1, 1,-1, 2,-1,  3,-1, 4,-1, -1, 3,-1, 2,
                0,-1, 0,-1, 1,-1, 2,-1,  0,-1, 0,-1, 5,-1, 4,-1,
                0,-1, 0,-1, 1,-1, 3,-1,  2,-1, 0,-1, -1,-1,-1,-1 };
            int[] ki = {
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0,
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0,
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,0,0,
                1,0,0,0, 1,0,0,0, 1,0,0,0, 1,0,1,0 };
            int[] hh = {
                2,1,1,1, 2,1,1,1, 2,1,1,1, 2,1,1,1,
                2,1,1,1, 2,1,1,1, 2,1,1,1, 2,1,1,1,
                2,1,1,1, 2,1,1,1, 2,1,1,1, 2,1,1,1,
                2,1,1,1, 2,1,1,1, 2,1,1,1, 2,1,2,1 };
            int[] bas = {
                0,-1,-1,-1, 0,-1,-1,-1, 0,-1, 0,-1, -1,-1,-1,-1,
                0,-1,-1,-1, 0,-1,-1,-1, 2,-1,-1,-1, -1,-1,-1,-1,
                0,-1,-1,-1, 0,-1,-1,-1, 0,-1, 0,-1, -1,-1,-1,-1,
                0,-1,-1,-1, 0,-1,-1,-1, 0,-1,-1,-1, -1, 0,-1,-1 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, mA = 99f, bA = 99f;
            float mF = scale[0], bF = scale[0] * 0.25f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen; float st = pt - step * s16;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    if (bas[step] >= 0) { bF = scale[bas[step]] * 0.25f; bA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                float mEnv = NoteEnvelope(mA, 0.015f, s16 * 1f, s16 * 6f);
                v += (Mathf.Sin(mPh) * 0.6f + GenerateWaveform(Waveform.Triangle, mPh) * 0.3f
                     + GenerateWaveform(Waveform.Square, mPh) * 0.1f) * mEnv * 0.16f;
                mA += dt;
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.02f, s16 * 3f, s16 * 10f) * 0.09f;
                bA += dt;
                if (ki[step] > 0) v += DrumKick(st, 0.1f);
                if (hh[step] > 0) v += DrumHat(st, nz[i % nz.Length], hh[step] == 2 ? 0.03f : 0.015f);
                // 긴장 펄스 (2.5Hz 트레몰로)
                v += Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.04f
                     * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2.5f * t));
                data[i] = v;
            }
            ApplyLowPass(data, 0.14f);
            ApplyReverb(data, 30f, 0.22f, 3);
            Normalize(data, 0.5f);
            AudioClip clip = AudioClip.Create("GameplayTenseBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 게임플레이 에너제틱 BGM — 캔디 팝 (C장조 펜타토닉, 128BPM)
        /// 중독성 훅 멜로디 + 그루비 킥 + 오프비트 하이햇
        /// </summary>
        public static AudioClip CreateGameplayEnergeticBGM(float duration = 90f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            float[] nz = MakeNoiseTable(33);
            // C 장조 펜타토닉 고음: C5, D5, E5, G5, A5, C6, D6, E6
            float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.66f, 1318.51f };
            float bpm = 108f; float s16 = 15f / bpm;
            // 캐치 훅: C-E-G..A G-E-C → 변주 → 해결
            int[] mel = {
                0,-1, 2,-1, 3,-1,-1, 4,  3,-1, 2,-1, 0,-1,-1,-1,
                0,-1, 2,-1, 3,-1, 5,-1, -1, 4,-1, 3, -1, 2,-1,-1,
                4,-1, 3,-1, 2,-1,-1, 0,  1,-1, 2,-1, -1,-1,-1,-1,
                0,-1, 2,-1, 4,-1, 3,-1,  2,-1, 0,-1, -1,-1,-1,-1 };
            int[] ki = {
                1,0,0,0, 0,0,0,0, 1,0,0,1, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,0,0, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,0,1, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,0,0, 0,0,1,0 };
            int[] hh = {
                2,0,1,0, 2,0,1,0, 2,0,1,0, 2,0,1,0,
                2,0,1,0, 2,0,1,0, 2,0,1,0, 2,0,1,0,
                2,0,1,0, 2,0,1,0, 2,0,1,0, 2,0,1,1,
                2,0,1,0, 2,0,1,0, 2,0,1,0, 2,1,1,1 };
            int[] bas = {
                0,-1, 0,-1, -1,-1, 3,-1, 0,-1, 0,-1, -1,-1, 4,-1,
                3,-1,-1,-1, 2,-1,-1, 0, -1,-1,-1,-1, -1, 0,-1,-1,
                0,-1, 0,-1, -1,-1, 3,-1, 4,-1,-1,-1, 3,-1,-1,-1,
                3,-1,-1,-1, 2,-1, 0,-1, -1,-1, 0,-1, -1,-1,-1,-1 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, mA = 99f, bA = 99f;
            float mF = scale[0], bF = scale[0] * 0.25f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen; float st = pt - step * s16;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    if (bas[step] >= 0) { bF = scale[bas[step]] * 0.25f; bA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                v += (Mathf.Sin(mPh) * 0.65f + GenerateWaveform(Waveform.Triangle, mPh) * 0.35f)
                     * NoteEnvelope(mA, 0.015f, s16 * 1.5f, s16 * 7f) * 0.16f;
                mA += dt;
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.02f, s16 * 3f, s16 * 10f) * 0.08f;
                bA += dt;
                if (ki[step] > 0) v += DrumKick(st, 0.09f);
                if (hh[step] > 0) v += DrumHat(st, nz[i % nz.Length], hh[step] == 2 ? 0.03f : 0.015f);
                // 밝은 쉬머 (옥타브 위 하모닉)
                float mEnv = NoteEnvelope(mA - dt, 0.015f, s16 * 1.5f, s16 * 7f);
                v += Mathf.Sin(mPh * 2f) * 0.03f * mEnv;
                data[i] = v;
            }
            ApplyLowPass(data, 0.12f);
            ApplyReverb(data, 25f, 0.2f, 3);
            Normalize(data, 0.5f);
            AudioClip clip = AudioClip.Create("GameplayEnergeticBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 게임플레이 에픽 BGM — 타이탄 그루브 (D단조 펜타토닉, 108BPM)
        /// 넓은 도약 멜로디 + 헤비 킥 + 두꺼운 하모닉 + 5도 더블링
        /// </summary>
        public static AudioClip CreateGameplayEpicBGM(float duration = 120f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            float[] nz = MakeNoiseTable(66);
            // D 단조 펜타토닉 2옥타브: D4, F4, G4, A4, C5, D5, F5, G5
            float[] scale = { 293.66f, 349.23f, 392f, 440f, 523.25f, 587.33f, 698.46f, 783.99f };
            float bpm = 92f; float s16 = 15f / bpm;
            // 드라마틱 와이드 멜로디
            int[] mel = {
                0,-1,-1, 3, -1,-1, 5,-1, -1, 4,-1,-1,  3,-1,-1,-1,
                0,-1,-1, 3, -1,-1, 6,-1, -1, 5,-1, 4, -1, 3,-1,-1,
                0,-1,-1, 3, -1,-1, 5,-1, -1, 7,-1,-1,  5,-1,-1,-1,
                3,-1,-1, 5, -1,-1, 4,-1,  3,-1,-1, 2, -1,-1, 0,-1 };
            int[] ki = {
                1,0,0,0, 0,0,0,0, 1,0,1,0, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,1,0, 0,0,0,0,
                1,0,0,0, 0,0,0,0, 1,0,1,0, 0,1,0,0,
                1,0,0,0, 0,0,0,0, 1,0,0,0, 1,0,1,0 };
            int[] hh = {
                2,0,1,0, 2,1,1,0, 2,0,1,0, 2,1,1,0,
                2,0,1,0, 2,1,1,0, 2,0,1,0, 2,1,1,0,
                2,0,1,0, 2,1,1,0, 2,0,1,0, 2,1,1,1,
                2,0,1,0, 2,1,1,0, 2,0,1,0, 2,1,2,1 };
            int[] bas = {
                0,-1,-1,-1, -1,-1, 0,-1, 0,-1,-1,-1, -1,-1,-1,-1,
                0,-1,-1,-1, -1,-1, 0,-1, 2,-1,-1,-1, -1,-1,-1,-1,
                0,-1,-1,-1, -1,-1, 0,-1, 0,-1,-1,-1, -1,-1, 3,-1,
                3,-1,-1,-1, -1,-1, 4,-1, 0,-1,-1,-1, -1, 0,-1,-1 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, mA = 99f, bA = 99f;
            float mF = scale[0], bF = scale[0] * 0.5f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen; float st = pt - step * s16;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    if (bas[step] >= 0) { bF = scale[bas[step]] * 0.5f; bA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                float mEnv = NoteEnvelope(mA, 0.025f, s16 * 1.5f, s16 * 8f);
                // 두꺼운 톤: Sine + Triangle + Sawtooth
                v += (Mathf.Sin(mPh) * 0.55f + GenerateWaveform(Waveform.Triangle, mPh) * 0.3f
                     + GenerateWaveform(Waveform.Sawtooth, mPh) * 0.15f) * mEnv * 0.16f;
                mA += dt;
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.02f, s16 * 3f, s16 * 10f) * 0.09f;
                bA += dt;
                if (ki[step] > 0) v += DrumKick(st, 0.11f);
                if (hh[step] > 0) v += DrumHat(st, nz[i % nz.Length], hh[step] == 2 ? 0.035f : 0.018f);
                // 옥타브 더블링 + 5도 하모니
                v += Mathf.Sin(mPh * 0.5f) * mEnv * 0.08f;
                v += Mathf.Sin(2f * Mathf.PI * mF * 1.5f / SAMPLE_RATE * i) * mEnv * 0.05f;
                data[i] = v;
            }
            ApplyLowPass(data, 0.14f);
            ApplyReverb(data, 45f, 0.3f, 4);
            Normalize(data, 0.5f);
            AudioClip clip = AudioClip.Create("GameplayEpicBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

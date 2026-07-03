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
                // ★ 2026-07-02 재질 강화: 금속 클릭 0.3→0.5 + 200Hz 저역 기계 임팩트 추가 (드릴=금속 기계)
                float click = Mathf.Sin(2f * Mathf.PI * 2000f * t) * clickEnv * 0.5f;
                float lowImpact = Mathf.Sin(2f * Mathf.PI * 200f * t) * Mathf.Max(0f, 1f - t * 8f) * 0.35f;
                float whirFreq = Mathf.Lerp(800f, 1000f, tNorm * 0.5f);
                float tremolo = 1f + 0.2f * Mathf.Sin(2f * Mathf.PI * 15f * t);
                float whirPhase = 2f * Mathf.PI * whirFreq * t;
                float whir = Mathf.Sin(whirPhase) * 0.3f * tremolo;
                float musicBox = Mathf.Sin(2f * Mathf.PI * 1568f * t) * 0.15f
                               * Mathf.Max(0f, 1f - t * 4f);
                data[i] = (click + lowImpact + whir + musicBox) * envelope * 0.4f;
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
                // ★ 2026-07-02 재질 강화: 초두 500Hz 임팩트 펀치 추가 (화약 폭발 강렬함)
                float punch = Mathf.Sin(2f * Mathf.PI * 500f * t) * Mathf.Max(0f, 1f - t * 25f) * 0.4f;
                data[i] = (body + poof + sparkle + punch) * envelope * 0.4f;
            }
            ApplyFades(data, 16, 96);
            ApplyLowPass(data, 0.25f);
            ApplyReverb(data, 25f, 0.25f, 3);
            AudioClip clip = AudioClip.Create("Explosion", sampleCount, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 타겟 레이저 - 상승 차임 + 따뜻한 확장 쉬머 물결
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
                float harmonic3 = Mathf.Sin(phase * 3.0f) * 0.15f; // ★ 2026-07-02 재질 강화: 8→15% (기계 위협감)

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

        // ============================================================
        // ★ 2026-07-02 전수 효과음 보강 — 누락 이벤트 20종 (재질 매핑)
        //   고블린=생물, 망치/드릴=금속, 폭탄=화약, 마법=차임/쉬머, UI=다크글래스 팝
        // ============================================================

        /// <summary>고블린 피격 — 짧은 '퍽'(노이즈 임팩트 + 180→90Hz 저중음 바디).</summary>
        public static AudioClip CreateGoblinHit(float duration = 0.14f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(7);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = NoteEnvelope(t, 0f, 0.015f, 0.11f);
                float freq = 180f - 90f * tn;
                ph += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float body = Mathf.Sin(ph) * 0.7f;
                float nz = (float)(rng.NextDouble() * 2 - 1) * (t < 0.04f ? (1f - t / 0.04f) : 0f) * 0.5f;
                d[i] = (body + nz) * env * 0.4f;
            }
            ApplyLowPass(d, 0.5f); ApplyFades(d, 4, 32);
            var c = AudioClip.Create("GoblinHit", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>고블린 사망 — 하행 워블 '우엑'(350→110Hz 비브라토) + 퍽 테일.</summary>
        public static AudioClip CreateGoblinDeath(float duration = 0.4f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(11);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = NoteEnvelope(t, 0.005f, 0.05f, duration - 0.06f);
                float freq = Mathf.Lerp(350f, 110f, tn) * (1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                ph += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float voice = WaveWithHarmonics(Waveform.Sine, ph, 3, 0.5f) * 0.6f;
                float nz = (float)(rng.NextDouble() * 2 - 1) * (tn > 0.7f ? (tn - 0.7f) / 0.3f * 0.25f : 0f);
                d[i] = (voice + nz) * env * 0.38f;
            }
            ApplyLowPass(d, 0.45f); ApplyFades(d, 8, 64); ApplyReverb(d, 18f, 0.18f, 2);
            var c = AudioClip.Create("GoblinDeath", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>고블린 이동 발걸음 — 아주 가벼운 저음 '톡' (볼륨 낮게 재생 권장).</summary>
        public static AudioClip CreateGoblinStep(float duration = 0.07f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(13);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = NoteEnvelope(t, 0f, 0.008f, 0.055f);
                ph += 2f * Mathf.PI * (240f - 120f * tn) / SAMPLE_RATE;
                float nz = (float)(rng.NextDouble() * 2 - 1) * (t < 0.015f ? 1f - t / 0.015f : 0f) * 0.4f;
                d[i] = (Mathf.Sin(ph) * 0.6f + nz) * env * 0.3f;
            }
            ApplyLowPass(d, 0.4f); ApplyFades(d, 4, 24);
            var c = AudioClip.Create("GoblinStep", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>파이어볼 발사 — 화염 우~쉬(노이즈 스웰 + 저역 화염 럼블).</summary>
        public static AudioClip CreateFireballCast(float duration = 0.45f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(17);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(tn)) ;      // 스웰(부풀었다 잦아듦)
                float nz = (float)(rng.NextDouble() * 2 - 1) * 0.55f;      // 화염 노이즈
                ph += 2f * Mathf.PI * (90f + 60f * tn) / SAMPLE_RATE;      // 저역 럼블 상승
                float rumble = Mathf.Sin(ph) * 0.45f;
                float crackle = ((i % 977) < 25 && tn > 0.2f) ? 0.25f : 0f; // 간헐 크래클
                d[i] = (nz + rumble + crackle) * env * 0.32f;
            }
            ApplyLowPass(d, 0.35f); ApplyFades(d, 32, 128); ApplyReverb(d, 22f, 0.2f, 2);
            var c = AudioClip.Create("FireballCast", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>번개 낙뢰 — 화이트노이즈 크랙 스파이크 + 저음 럼블 테일.</summary>
        public static AudioClip CreateLightningStrike(float duration = 0.5f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(19);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float crackEnv = t < 0.06f ? 1f - t / 0.06f : 0f;                 // 초두 크랙
                float crack = (float)(rng.NextDouble() * 2 - 1) * crackEnv;
                ph += 2f * Mathf.PI * (55f + 20f * Mathf.Sin(2f * Mathf.PI * 3f * t)) / SAMPLE_RATE;
                float rumble = Mathf.Sin(ph) * Mathf.Exp(-2.6f * tn) * 0.6f;      // 감쇠 럼블
                float sizzle = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-5f * tn) * 0.15f;
                d[i] = (crack * 0.9f + rumble + sizzle) * 0.42f;
            }
            ApplyFades(d, 2, 160); ApplyReverb(d, 35f, 0.28f, 3);
            var c = AudioClip.Create("LightningStrike", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>폭탄 설치/카운트 틱 — 태엽 '틱-틱' 금속 클릭 2회 + 낮은 톤.</summary>
        public static AudioClip CreateBombPlant(float duration = 0.22f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float tick1 = t < 0.03f ? Mathf.Exp(-90f * t) : 0f;
                float t2 = t - 0.10f;
                float tick2 = (t2 > 0f && t2 < 0.03f) ? Mathf.Exp(-90f * t2) : 0f;
                ph += 2f * Mathf.PI * 1500f / SAMPLE_RATE;
                float click = Mathf.Sin(ph) * (tick1 + tick2 * 0.8f);
                float low = Mathf.Sin(2f * Mathf.PI * 140f * t) * NoteEnvelope(t, 0f, 0.05f, 0.15f) * 0.3f;
                d[i] = (click * 0.6f + low) * 0.4f;
            }
            ApplyFades(d, 2, 32);
            var c = AudioClip.Create("BombPlant", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>도둑 훔침 — 재빠른 '슉' 상행 노이즈 스윕.</summary>
        public static AudioClip CreateThiefSteal(float duration = 0.2f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(23);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = Mathf.Sin(Mathf.PI * tn);
                ph += 2f * Mathf.PI * Mathf.Lerp(500f, 1900f, tn) / SAMPLE_RATE;
                float body = Mathf.Sin(ph) * 0.3f;
                float nz = (float)(rng.NextDouble() * 2 - 1) * 0.5f;
                d[i] = (body + nz * env) * env * 0.3f;
            }
            ApplyLowPass(d, 0.75f); ApplyFades(d, 8, 48);
            var c = AudioClip.Create("ThiefSteal", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>힐러 회복 — 부드러운 상승 차임(E6→G6→B6) + 글로우.</summary>
        public static AudioClip CreateHealChime(float duration = 0.45f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float[] notes = { 1318.5f, 1568.0f, 1975.5f };
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * 0.09f;
                float ph = 0f;
                for (int i = Mathf.CeilToInt(start * SAMPLE_RATE); i < n; i++)
                {
                    float nt = (float)i / SAMPLE_RATE - start;
                    float env = NoteEnvelope(nt, 0.008f, 0.06f, 0.3f);
                    ph += 2f * Mathf.PI * notes[k] / SAMPLE_RATE;
                    d[i] += Mathf.Sin(ph) * env * 0.22f;
                }
            }
            ApplyFades(d, 16, 96); ApplyReverb(d, 30f, 0.3f, 3);
            var c = AudioClip.Create("HealChime", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>마녀 소환 — 어두운 하행 스윕(900→150Hz) + 와블 + 서브 임팩트.</summary>
        public static AudioClip CreateWitchSummon(float duration = 0.6f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = NoteEnvelope(t, 0.01f, 0.1f, duration - 0.12f);
                float freq = Mathf.Lerp(900f, 150f, tn) * (1f + 0.09f * Mathf.Sin(2f * Mathf.PI * 6f * t));
                ph += 2f * Mathf.PI * freq / SAMPLE_RATE;
                ph2 += 2f * Mathf.PI * 55f / SAMPLE_RATE;
                float sub = Mathf.Sin(ph2) * (tn > 0.6f ? (tn - 0.6f) / 0.4f : 0f) * 0.5f;
                d[i] = (WaveWithHarmonics(Waveform.Sine, ph, 4, 0.45f) * 0.5f + sub) * env * 0.36f;
            }
            ApplyLowPass(d, 0.5f); ApplyFades(d, 16, 96); ApplyReverb(d, 40f, 0.32f, 3);
            var c = AudioClip.Create("WitchSummon", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>망치 타격 — 금속 '쾅'(임팩트 노이즈 + 200→60Hz 바디 + 2400Hz 금속 링).</summary>
        public static AudioClip CreateHammerImpact(float duration = 0.3f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(29);
            float ph = 0f, phR = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float impact = (float)(rng.NextDouble() * 2 - 1) * (t < 0.025f ? 1f - t / 0.025f : 0f);
                ph += 2f * Mathf.PI * (200f - 140f * tn) / SAMPLE_RATE;
                float body = Mathf.Sin(ph) * NoteEnvelope(t, 0f, 0.02f, 0.2f) * 0.8f;
                phR += 2f * Mathf.PI * 2400f / SAMPLE_RATE;
                float ring = Mathf.Sin(phR) * Mathf.Exp(-14f * t) * 0.3f;
                d[i] = (impact * 0.8f + body + ring) * 0.42f;
            }
            ApplyFades(d, 2, 64); ApplyReverb(d, 16f, 0.18f, 2);
            var c = AudioClip.Create("HammerImpact", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>스왑 — '휙↔휙' 교차 노이즈 스윕 2연속(상행+하행).</summary>
        public static AudioClip CreateSwapWhoosh(float duration = 0.32f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(31);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                bool first = tn < 0.5f;
                float seg = first ? tn / 0.5f : (tn - 0.5f) / 0.5f;
                float env = Mathf.Sin(Mathf.PI * seg) * (first ? 1f : 0.85f);
                float freq = first ? Mathf.Lerp(400f, 1100f, seg) : Mathf.Lerp(1100f, 400f, seg);
                ph += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float nz = (float)(rng.NextDouble() * 2 - 1) * 0.45f;
                d[i] = (Mathf.Sin(ph) * 0.35f + nz * env) * env * 0.3f;
            }
            ApplyLowPass(d, 0.6f); ApplyFades(d, 8, 48);
            var c = AudioClip.Create("SwapWhoosh", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>라인 발동 — 전기 '지잉' 버즈 상승 + 크리스탈 파열 마무리.</summary>
        public static AudioClip CreateLineZap(float duration = 0.38f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph = 0f, phC = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float freq = Mathf.Lerp(300f, 900f, Mathf.Min(1f, tn / 0.7f));
                ph += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float buzz = (Mathf.Sin(ph) * 0.5f + Mathf.Sign(Mathf.Sin(ph * 2f)) * 0.12f)
                             * (tn < 0.7f ? NoteEnvelope(t, 0.01f, 0.18f, 0.1f) : 0f);
                float ct = t - duration * 0.68f;
                phC += 2f * Mathf.PI * 2093f / SAMPLE_RATE;
                float chime = ct > 0f ? Mathf.Sin(phC) * Mathf.Exp(-9f * ct) * 0.5f : 0f;
                d[i] = (buzz + chime) * 0.36f;
            }
            ApplyFades(d, 8, 64); ApplyReverb(d, 18f, 0.2f, 2);
            var c = AudioClip.Create("LineZap", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>역회전 — 태엽 되감기(하행 워블 800→300Hz) + 틱틱.</summary>
        public static AudioClip CreateReverseWind(float duration = 0.42f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = NoteEnvelope(t, 0.01f, 0.1f, duration - 0.12f);
                float freq = Mathf.Lerp(800f, 300f, tn) * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 14f * t));
                ph += 2f * Mathf.PI * freq / SAMPLE_RATE;
                float tick = ((i % 3307) < 40) ? 0.25f * Mathf.Exp(-60f * ((i % 3307) / (float)SAMPLE_RATE)) : 0f;
                d[i] = (WaveWithHarmonics(Waveform.Triangle, ph, 3, 0.5f) * 0.5f + tick) * env * 0.34f;
            }
            ApplyFades(d, 8, 64);
            var c = AudioClip.Create("ReverseWind", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>게이지 만충 — 밝은 완성 벨(C7 + 옥타브 배음).</summary>
        public static AudioClip CreateGaugeFull(float duration = 0.4f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph1 = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float env = NoteEnvelope(t, 0.004f, 0.08f, 0.3f);
                ph1 += 2f * Mathf.PI * 2093f / SAMPLE_RATE;   // C7
                ph2 += 2f * Mathf.PI * 4186f / SAMPLE_RATE;   // C8 배음
                d[i] = (Mathf.Sin(ph1) * 0.6f + Mathf.Sin(ph2) * 0.18f) * env * 0.32f;
            }
            ApplyFades(d, 8, 96); ApplyReverb(d, 26f, 0.26f, 3);
            var c = AudioClip.Create("GaugeFull", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>잠금 해제 — 금속 클릭 + 상승 차임(G5→C6). 미션 슬롯 해금.</summary>
        public static AudioClip CreateUnlockChime(float duration = 0.45f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float phC = 0f, ph1 = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                phC += 2f * Mathf.PI * 1800f / SAMPLE_RATE;
                float click = Mathf.Sin(phC) * (t < 0.02f ? Mathf.Exp(-70f * t) : 0f) * 0.7f;
                float t1 = t - 0.06f, t2 = t - 0.18f;
                ph1 += 2f * Mathf.PI * 784f / SAMPLE_RATE;    // G5
                ph2 += 2f * Mathf.PI * 1046.5f / SAMPLE_RATE; // C6
                float n1 = t1 > 0f ? Mathf.Sin(ph1) * NoteEnvelope(t1, 0.005f, 0.05f, 0.2f) * 0.4f : 0f;
                float n2 = t2 > 0f ? Mathf.Sin(ph2) * NoteEnvelope(t2, 0.005f, 0.06f, 0.22f) * 0.45f : 0f;
                d[i] = (click + n1 + n2) * 0.4f;
            }
            ApplyFades(d, 4, 96); ApplyReverb(d, 24f, 0.24f, 2);
            var c = AudioClip.Create("UnlockChime", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>리워드 픽 확정 — 밝은 2음 '띠링'(E6→A6).</summary>
        public static AudioClip CreateRewardPick(float duration = 0.3f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph1 = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                ph1 += 2f * Mathf.PI * 1318.5f / SAMPLE_RATE;
                ph2 += 2f * Mathf.PI * 1760f / SAMPLE_RATE;
                float a = Mathf.Sin(ph1) * NoteEnvelope(t, 0.004f, 0.04f, 0.12f) * 0.5f;
                float t2 = t - 0.09f;
                float b = t2 > 0f ? Mathf.Sin(ph2) * NoteEnvelope(t2, 0.004f, 0.05f, 0.16f) * 0.55f : 0f;
                d[i] = (a + b) * 0.38f;
            }
            ApplyFades(d, 4, 64); ApplyReverb(d, 20f, 0.2f, 2);
            var c = AudioClip.Create("RewardPick", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>팝업 닫기 — 하행 팝(880→440Hz, 열기의 역방향).</summary>
        public static AudioClip CreatePopupClose(float duration = 0.2f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = NoteEnvelope(t, 0.004f, 0.05f, 0.13f);
                ph += 2f * Mathf.PI * Mathf.Lerp(880f, 440f, tn) / SAMPLE_RATE;
                d[i] = (Mathf.Sin(ph) * 0.75f + GenerateWaveform(Waveform.Triangle, ph) * 0.25f) * env * 0.3f;
            }
            ApplyFades(d, 4, 48);
            var c = AudioClip.Create("PopupClose", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>데드락 재배치 — '샤라락' 셰이커 연타 + 정리 차임.</summary>
        public static AudioClip CreateReshuffle(float duration = 0.55f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(37);
            float phC = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                // 6회 셰이커 버스트
                float shake = 0f;
                for (int k = 0; k < 6; k++)
                {
                    float st = t - k * 0.055f;
                    if (st > 0f && st < 0.03f) shake = (float)(rng.NextDouble() * 2 - 1) * (1f - st / 0.03f) * 0.5f;
                }
                float ct = t - 0.38f;
                phC += 2f * Mathf.PI * 1568f / SAMPLE_RATE; // G6
                float chime = ct > 0f ? Mathf.Sin(phC) * Mathf.Exp(-8f * ct) * 0.5f : 0f;
                d[i] = (shake * (1f - tn * 0.4f) + chime) * 0.36f;
            }
            ApplyLowPass(d, 0.7f); ApplyFades(d, 8, 64);
            var c = AudioClip.Create("Reshuffle", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>MP 부족 거부 — 저음 버즈 '붕붕' 2회.</summary>
        public static AudioClip CreateDenyBuzz(float duration = 0.28f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                float g1 = (t < 0.09f) ? 1f : 0f;
                float t2 = t - 0.13f;
                float g2 = (t2 > 0f && t2 < 0.09f) ? 0.85f : 0f;
                ph += 2f * Mathf.PI * 130f / SAMPLE_RATE;
                float buzz = (Mathf.Sin(ph) * 0.6f + Mathf.Sign(Mathf.Sin(ph)) * 0.15f);
                d[i] = buzz * (g1 + g2) * 0.34f;
            }
            ApplyLowPass(d, 0.35f); ApplyFades(d, 8, 48);
            var c = AudioClip.Create("DenyBuzz", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>특수블록 합성 — 듀얼톤 융합 상승 + 마법 임팩트.</summary>
        public static AudioClip CreateComboMerge(float duration = 0.45f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(41);
            float ph1 = 0f, ph2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float rise = Mathf.Min(1f, tn / 0.65f);
                ph1 += 2f * Mathf.PI * Mathf.Lerp(520f, 1040f, rise) / SAMPLE_RATE;
                ph2 += 2f * Mathf.PI * Mathf.Lerp(780f, 1560f, rise) / SAMPLE_RATE;
                float duo = (Mathf.Sin(ph1) * 0.4f + Mathf.Sin(ph2) * 0.3f) * (tn < 0.65f ? NoteEnvelope(t, 0.01f, 0.2f, 0.12f) : 0f);
                float it = t - duration * 0.63f;
                float impact = it > 0f ? ((float)(rng.NextDouble() * 2 - 1) * 0.4f + Mathf.Sin(2f * Mathf.PI * 220f * it) * 0.5f) * Mathf.Exp(-10f * it) : 0f;
                d[i] = (duo + impact) * 0.38f;
            }
            ApplyFades(d, 8, 80); ApplyReverb(d, 24f, 0.24f, 3);
            var c = AudioClip.Create("ComboMerge", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>쉘(돌) 파괴 — 돌 크런치(저역 노이즈 버스트 + 파편).</summary>
        public static AudioClip CreateShellBreak(float duration = 0.22f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            var rng = new System.Random(43);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float crunch = (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-16f * tn);
                ph += 2f * Mathf.PI * (150f - 70f * tn) / SAMPLE_RATE;
                float body = Mathf.Sin(ph) * NoteEnvelope(t, 0f, 0.02f, 0.16f) * 0.5f;
                float debris = ((i % 1531) < 30 && tn > 0.3f) ? 0.2f : 0f;
                d[i] = (crunch * 0.7f + body + debris) * 0.4f;
            }
            ApplyLowPass(d, 0.45f); ApplyFades(d, 4, 48);
            var c = AudioClip.Create("ShellBreak", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
        }

        /// <summary>영혼 오브 흡수 — 아주 작은 '톡' 상승 방울(700→1100Hz).</summary>
        public static AudioClip CreateOrbAbsorb(float duration = 0.09f)
        {
            int n = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLE_RATE; float tn = (float)i / n;
                float env = NoteEnvelope(t, 0.003f, 0.02f, 0.06f);
                ph += 2f * Mathf.PI * Mathf.Lerp(700f, 1100f, tn) / SAMPLE_RATE;
                d[i] = Mathf.Sin(ph) * env * 0.28f;
            }
            ApplyFades(d, 4, 24);
            var c = AudioClip.Create("OrbAbsorb", n, 1, SAMPLE_RATE, false); c.SetData(d, 0); return c;
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
        /// <summary>
        /// 게임플레이 Serene BGM — 잔잔한 아르페지오 (C장조, 72BPM)
        /// 부드러운 사인파 아르페지오 + 따뜻한 패드 + 드럼 없음
        /// </summary>
        public static AudioClip CreateGameplayTenseBGM(float duration = 90f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            // C장조 음계: C4, E4, G4, A4, C5, E5, G5, A5
            float[] scale = { 261.63f, 329.63f, 392f, 440f, 523.25f, 659.25f, 783.99f, 880f };
            float bpm = 72f; float s16 = 15f / bpm;
            // 부드러운 아르페지오 패턴 (4마디)
            int[] mel = {
                0,-1,-1,-1, 1,-1,-1,-1, 2,-1,-1,-1, 3,-1,-1,-1,
                4,-1,-1,-1, 3,-1,-1,-1, 2,-1,-1,-1, 1,-1,-1,-1,
                0,-1,-1,-1, 2,-1,-1,-1, 4,-1,-1,-1, 5,-1,-1,-1,
                4,-1,-1,-1, 2,-1,-1,-1, 1,-1,-1,-1, 0,-1,-1,-1 };
            // 느린 베이스 (전음 롱 톤)
            int[] bas = {
                0,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
                2,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
                0,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
                1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, padPh1 = 0f, padPh2 = 0f, padPh3 = 0f;
            float mA = 99f, bA = 99f;
            float mF = scale[0], bF = scale[0] * 0.25f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    if (bas[step] >= 0) { bF = scale[bas[step]] * 0.25f; bA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                // 멜로디: 부드러운 사인파 + 삼각파 블렌드 (긴 어택, 긴 디케이)
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                float mEnv = NoteEnvelope(mA, 0.08f, s16 * 2f, s16 * 12f);
                v += (Mathf.Sin(mPh) * 0.8f + GenerateWaveform(Waveform.Triangle, mPh) * 0.2f) * mEnv * 0.12f;
                mA += dt;
                // 베이스: 따뜻한 사인파 롱 톤
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.1f, s16 * 8f, s16 * 20f) * 0.06f;
                bA += dt;
                // 패드: C-E-G 코드 지속음 (느린 스웰)
                padPh1 += 2f * Mathf.PI * 261.63f / SAMPLE_RATE;
                padPh2 += 2f * Mathf.PI * 329.63f / SAMPLE_RATE;
                padPh3 += 2f * Mathf.PI * 392f / SAMPLE_RATE;
                float padSwell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.08f * t); // 12초 주기
                v += (Mathf.Sin(padPh1) + Mathf.Sin(padPh2) + Mathf.Sin(padPh3)) * 0.018f * padSwell;
                data[i] = v;
            }
            ApplyLowPass(data, 0.08f);
            ApplyReverb(data, 50f, 0.35f, 4);
            Normalize(data, 0.45f);
            AudioClip clip = AudioClip.Create("GameplaySereneBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 게임플레이 Gentle Flow BGM — 부드러운 흐름 (G장조, 76BPM)
        /// 유려한 사인파 멜로디 + 코드 패드 + 미세 퍼커션
        /// </summary>
        public static AudioClip CreateGameplayEnergeticBGM(float duration = 90f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            float[] nz = MakeNoiseTable(33);
            // G장조 음계: G3, B3, D4, E4, G4, B4, D5, E5
            float[] scale = { 196f, 246.94f, 293.66f, 329.63f, 392f, 493.88f, 587.33f, 659.25f };
            float bpm = 76f; float s16 = 15f / bpm;
            // 유려한 선율 (느린 상승-하강)
            int[] mel = {
                0,-1,-1,-1,-1,-1, 1,-1, -1,-1,-1,-1, 2,-1,-1,-1,
                -1,-1, 3,-1,-1,-1,-1,-1,  4,-1,-1,-1,-1,-1,-1,-1,
                5,-1,-1,-1,-1,-1, 4,-1, -1,-1,-1,-1, 3,-1,-1,-1,
                -1,-1, 2,-1,-1,-1, 1,-1, -1,-1,-1,-1, 0,-1,-1,-1 };
            // 롱 톤 베이스 (2마디 간격)
            int[] bas = {
                0,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
                -1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
                2,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1,
                -1,-1,-1,-1,-1,-1,-1,-1, -1,-1,-1,-1,-1,-1,-1,-1 };
            // 미세 퍼커션 (소프트 하이햇만, 반박자)
            int[] hh = {
                0,0,0,0, 1,0,0,0, 0,0,0,0, 1,0,0,0,
                0,0,0,0, 1,0,0,0, 0,0,0,0, 1,0,0,0,
                0,0,0,0, 1,0,0,0, 0,0,0,0, 1,0,0,0,
                0,0,0,0, 1,0,0,0, 0,0,0,0, 0,0,0,0 };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, bPh = 0f, mA = 99f, bA = 99f;
            float mF = scale[0], bF = scale[0] * 0.25f;
            float padPh1 = 0f, padPh2 = 0f, padPh3 = 0f;
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
                // 멜로디: 순수 사인파 (긴 어택 0.12초, 매우 긴 디케이)
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                float mEnv = NoteEnvelope(mA, 0.12f, s16 * 3f, s16 * 14f);
                v += Mathf.Sin(mPh) * mEnv * 0.13f;
                mA += dt;
                // 베이스: 따뜻한 저음
                bPh += 2f * Mathf.PI * bF / SAMPLE_RATE;
                v += Mathf.Sin(bPh) * NoteEnvelope(bA, 0.15f, s16 * 12f, s16 * 24f) * 0.05f;
                bA += dt;
                // 패드: G-B-D 코드 (느린 호흡)
                padPh1 += 2f * Mathf.PI * 196f / SAMPLE_RATE;
                padPh2 += 2f * Mathf.PI * 246.94f / SAMPLE_RATE;
                padPh3 += 2f * Mathf.PI * 293.66f / SAMPLE_RATE;
                float padBreath = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.06f * t); // 16초 주기
                v += (Mathf.Sin(padPh1) + Mathf.Sin(padPh2) + Mathf.Sin(padPh3)) * 0.015f * padBreath;
                // 미세 하이햇 (매우 작은 소리)
                if (hh[step] > 0) v += DrumHat(st, nz[i % nz.Length], 0.008f);
                data[i] = v;
            }
            ApplyLowPass(data, 0.07f);
            ApplyReverb(data, 55f, 0.38f, 4);
            Normalize(data, 0.42f);
            AudioClip clip = AudioClip.Create("GameplayGentleFlowBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// 게임플레이 Dreamy Waltz BGM — 몽환적 왈츠 (F장조, 68BPM)
        /// 넓은 코드 패드 + 천천히 떠다니는 멜로디 + 하모닉 잔향
        /// </summary>
        public static AudioClip CreateGameplayEpicBGM(float duration = 120f)
        {
            int sc = Mathf.CeilToInt(SAMPLE_RATE * duration);
            float[] data = new float[sc];
            // F장조 음계: F3, A3, C4, D4, F4, A4, C5, D5
            float[] scale = { 174.61f, 220f, 261.63f, 293.66f, 349.23f, 440f, 523.25f, 587.33f };
            float bpm = 68f; float s16 = 15f / bpm;
            // 몽환적 느린 선율 (반박자 쉼이 많아 여유로움)
            int[] mel = {
                2,-1,-1,-1,-1,-1,-1,-1,  4,-1,-1,-1,-1,-1,-1,-1,
                5,-1,-1,-1,-1,-1, 4,-1, -1,-1,-1,-1,-1,-1,-1,-1,
                3,-1,-1,-1,-1,-1,-1,-1,  5,-1,-1,-1,-1,-1,-1,-1,
                4,-1,-1,-1,-1,-1, 2,-1, -1,-1,-1,-1,-1,-1,-1,-1 };
            // 코드 패드 체인지 (8마디마다)
            // F maj (F-A-C), Dm (D-F-A), Bb maj approx (F-A-D), C (C-E-G≈C-F-A)
            float[][] chords = {
                new[] { 174.61f, 220f, 261.63f },     // F major
                new[] { 146.83f, 174.61f, 220f },     // Dm
                new[] { 174.61f, 220f, 293.66f },     // F/D
                new[] { 130.81f, 174.61f, 220f }      // C-F-A
            };
            int pLen = mel.Length; float pDur = pLen * s16;
            float mPh = 0f, mA = 99f, mF = scale[2];
            float cPh1 = 0f, cPh2 = 0f, cPh3 = 0f;
            float dt = 1f / SAMPLE_RATE; int pS = -1;
            for (int i = 0; i < sc; i++)
            {
                float t = (float)i / SAMPLE_RATE;
                int aS = (int)(t / s16); float pt = t % pDur;
                int step = (int)(pt / s16) % pLen;
                if (aS != pS) {
                    if (mel[step] >= 0) { mF = scale[mel[step]]; mA = 0f; }
                    pS = aS;
                }
                float v = 0f;
                // 멜로디: 순수 사인파 + 미세 삼각파 (매우 긴 어택/디케이)
                mPh += 2f * Mathf.PI * mF / SAMPLE_RATE;
                float mEnv = NoteEnvelope(mA, 0.15f, s16 * 4f, s16 * 18f);
                v += (Mathf.Sin(mPh) * 0.85f + GenerateWaveform(Waveform.Triangle, mPh) * 0.15f)
                     * mEnv * 0.11f;
                // 옥타브 아래 더블링 (은은한 깊이)
                v += Mathf.Sin(mPh * 0.5f) * mEnv * 0.04f;
                mA += dt;
                // 코드 패드: 4마디(16step) 간격으로 코드 변경, 느린 호흡
                int chordIdx = ((int)(pt / (16f * s16))) % chords.Length;
                float[] chord = chords[chordIdx];
                cPh1 += 2f * Mathf.PI * chord[0] / SAMPLE_RATE;
                cPh2 += 2f * Mathf.PI * chord[1] / SAMPLE_RATE;
                cPh3 += 2f * Mathf.PI * chord[2] / SAMPLE_RATE;
                float padSwell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.05f * t); // 20초 주기
                v += (Mathf.Sin(cPh1) + Mathf.Sin(cPh2) + Mathf.Sin(cPh3)) * 0.02f * padSwell;
                // 은은한 옥타브 위 쉬머 (하모닉 잔향)
                v += Mathf.Sin(cPh3 * 2f) * 0.006f * padSwell;
                data[i] = v;
            }
            ApplyLowPass(data, 0.06f);
            ApplyReverb(data, 60f, 0.4f, 5);
            Normalize(data, 0.4f);
            AudioClip clip = AudioClip.Create("GameplayDreamyWaltzBGM", sc, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

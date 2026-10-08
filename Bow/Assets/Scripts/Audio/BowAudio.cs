using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bow.Audio
{
    /// <summary>
    /// 오디오 매니저 (싱글톤). BGM은 Resources/Audio/BGM/&lt;이름&gt; (Suno AI 제작 파일), SFX는 Resources/Audio/SFX/&lt;id&gt;.
    /// 파일이 없으면 SFX는 BowSfxSynth 폴백, BGM은 무음.
    /// 히트스톱 중에는 AudioListener.pause 대신 BGM 로우패스만 적용한다 (사운드 §6.3).
    /// </summary>
    public sealed class BowAudio : MonoBehaviour
    {
        private static BowAudio instance;
        public static BowAudio I
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("BowAudio");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<BowAudio>();
                    instance.Init();
                }
                return instance;
            }
        }

        public float bgmVolume = 0.7f;
        public float sfxVolume = 1.0f;

        private readonly List<AudioSource> sfxPool = new List<AudioSource>();
        private AudioSource bgmA, bgmB;
        private AudioSource windSrc;
        private AudioLowPassFilter windLp;
        private AudioLowPassFilter bgmLpA;
        private bool bgmUsingA = true;
        private string currentBgm = "";
        private readonly Dictionary<string, AudioClip> fileCache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();
        private Coroutine bgmFade;

        private void Init()
        {
            // BGM 전용 오브젝트 (AudioLowPassFilter는 같은 GameObject의 모든 AudioSource에 걸리므로 분리)
            GameObject bgmGo = new GameObject("BGM"); bgmGo.transform.SetParent(transform, false);
            bgmA = bgmGo.AddComponent<AudioSource>(); bgmA.loop = true; bgmA.playOnAwake = false;
            bgmB = bgmGo.AddComponent<AudioSource>(); bgmB.loop = true; bgmB.playOnAwake = false;
            bgmLpA = bgmGo.AddComponent<AudioLowPassFilter>(); bgmLpA.cutoffFrequency = 22000f;

            GameObject windGo = new GameObject("Wind"); windGo.transform.SetParent(transform, false);
            windSrc = windGo.AddComponent<AudioSource>(); windSrc.loop = true; windSrc.playOnAwake = false;
            windLp = windGo.AddComponent<AudioLowPassFilter>(); windLp.cutoffFrequency = 1200f;

            for (int i = 0; i < 10; i++) sfxPool.Add(NewSfxSource());
        }

        private AudioSource NewSfxSource()
        {
            GameObject go = new GameObject("sfx" + sfxPool.Count);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            return s;
        }

        private AudioSource GetFree()
        {
            for (int i = 0; i < sfxPool.Count; i++) if (!sfxPool[i].isPlaying) return sfxPool[i];
            AudioSource s = NewSfxSource();
            sfxPool.Add(s);
            return s;
        }

        /// <summary>파일 우선, 없으면 프로시저럴 폴백</summary>
        public AudioClip GetClip(string id)
        {
            AudioClip c;
            if (fileCache.TryGetValue(id, out c)) return c;
            c = Resources.Load<AudioClip>("Audio/SFX/" + id);
            if (c == null) c = BowSfxSynth.Get(id);
            fileCache[id] = c;
            return c;
        }

        /// <summary>SFX 재생 (볼륨, 피치 범위, 최소 재생 간격)</summary>
        public void Play(string id, float volume = 1f, float pitchMin = 1f, float pitchMax = 1f, float minInterval = 0f)
        {
            if (minInterval > 0f)
            {
                float last;
                if (lastPlayTime.TryGetValue(id, out last) && Time.unscaledTime - last < minInterval) return;
            }
            lastPlayTime[id] = Time.unscaledTime;
            AudioClip c = GetClip(id);
            if (c == null) return;
            AudioSource s = GetFree();
            s.clip = c;
            s.volume = volume * sfxVolume;
            s.pitch = pitchMin == pitchMax ? pitchMin : Random.Range(pitchMin, pitchMax);
            s.Play();
        }

        /// <summary>루프 보이스 생성 (활 당김, 화살 휘파람 등). 호출자가 Stop/Destroy 관리.</summary>
        public AudioSource StartLoop(string id, float volume, float pitch)
        {
            AudioClip c = GetClip(id);
            GameObject go = new GameObject("loop_" + id);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.clip = c; s.loop = true; s.volume = volume * sfxVolume; s.pitch = pitch; s.playOnAwake = false;
            s.Play();
            return s;
        }

        public void StopLoop(AudioSource s, float fade = 0.08f)
        {
            if (s == null) return;
            StartCoroutine(FadeOutAndDestroy(s, fade));
        }

        private IEnumerator FadeOutAndDestroy(AudioSource s, float fade)
        {
            float v0 = s.volume, t = 0f;
            while (t < fade && s != null) { t += Time.unscaledDeltaTime; s.volume = Mathf.Lerp(v0, 0f, t / fade); yield return null; }
            if (s != null) Destroy(s.gameObject);
        }

        // ---------------------------------------------------------------
        // 바람 루프 (§6.1)
        // ---------------------------------------------------------------

        public void StartWind()
        {
            if (windSrc.isPlaying) return;
            windSrc.clip = GetClip("wind_loop");
            windSrc.volume = 0f;
            windSrc.Play();
        }

        public void SetWind(float w)
        {
            if (!windSrc.isPlaying) return;
            float u = Mathf.Clamp01(Mathf.Abs(w) / 7f);
            float vol = 0.10f + 0.50f * Mathf.Pow(u, 1.3f);
            windSrc.volume = Mathf.Lerp(windSrc.volume, vol * sfxVolume, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            windSrc.pitch = Mathf.Lerp(0.90f, 1.15f, u);
            windLp.cutoffFrequency = Mathf.Lerp(500f, 4000f, u);
        }

        public void StopWind(float fade = 1f) { StartCoroutine(FadeWind(fade)); }

        private IEnumerator FadeWind(float fade)
        {
            float v0 = windSrc.volume, t = 0f;
            while (t < fade) { t += Time.unscaledDeltaTime; windSrc.volume = Mathf.Lerp(v0, 0f, t / fade); yield return null; }
            windSrc.Stop();
        }

        // ---------------------------------------------------------------
        // BGM (Suno AI 파일, 크로스페이드)
        // ---------------------------------------------------------------

        /// <summary>BGM 재생. 파일이 없으면 조용히 무시 (Suno 파일 투입 전 상태).</summary>
        public void PlayBgm(string name, float crossfade = 1.5f, bool syncSamples = false)
        {
            if (currentBgm == name) return;
            AudioClip c = Resources.Load<AudioClip>("Audio/BGM/" + name);
            currentBgm = name;
            AudioSource from = bgmUsingA ? bgmA : bgmB;
            AudioSource to = bgmUsingA ? bgmB : bgmA;
            bgmUsingA = !bgmUsingA;
            if (bgmFade != null) StopCoroutine(bgmFade);
            if (c == null)
            {
                bgmFade = StartCoroutine(CrossFade(from, null, crossfade));
                return;
            }
            to.clip = c;
            to.volume = 0f;
            to.Play();
            if (syncSamples && from.isPlaying && from.clip != null && from.clip.samples == c.samples) to.timeSamples = from.timeSamples;
            bgmFade = StartCoroutine(CrossFade(from, to, crossfade));
        }

        public void StopBgm(float fade = 1f)
        {
            currentBgm = "";
            if (bgmFade != null) StopCoroutine(bgmFade);
            bgmFade = StartCoroutine(CrossFade(bgmUsingA ? bgmA : bgmB, null, fade));
            bgmFade = StartCoroutine(CrossFade(bgmUsingA ? bgmB : bgmA, null, fade));
        }

        private IEnumerator CrossFade(AudioSource from, AudioSource to, float dur)
        {
            float t = 0f;
            float v0 = from != null ? from.volume : 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                // 등전력
                if (from != null) from.volume = v0 * Mathf.Cos(u * Mathf.PI * 0.5f);
                if (to != null) to.volume = bgmVolume * Mathf.Sin(u * Mathf.PI * 0.5f);
                yield return null;
            }
            if (from != null) { from.volume = 0f; from.Stop(); }
            if (to != null) to.volume = bgmVolume;
        }

        /// <summary>히트스톱 중 BGM 로우패스 (§6.3)</summary>
        public void DuckBgm(float duration)
        {
            StartCoroutine(DuckRoutine(duration));
        }

        private IEnumerator DuckRoutine(float dur)
        {
            bgmLpA.cutoffFrequency = 800f;
            float t = 0f;
            while (t < dur) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < 0.2f) { t += Time.unscaledDeltaTime; bgmLpA.cutoffFrequency = Mathf.Lerp(800f, 22000f, t / 0.2f); yield return null; }
            bgmLpA.cutoffFrequency = 22000f;
        }
    }
}

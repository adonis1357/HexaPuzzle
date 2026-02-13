using UnityEngine;
using System.Collections.Generic;

namespace JewelsHexaPuzzle.Managers
{
    /// <summary>
    /// 오디오 매니저
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("BGM Clips")]
        [SerializeField] private AudioClip mainBGM;
        [SerializeField] private AudioClip gameBGM;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip rotateSound;
        [SerializeField] private AudioClip matchSound;
        [SerializeField] private AudioClip match4Sound;
        [SerializeField] private AudioClip match5Sound;
        [SerializeField] private AudioClip processSound;
        [SerializeField] private AudioClip specialGemSound;
        [SerializeField] private AudioClip perfectGemSound;
        [SerializeField] private AudioClip bigBangSound;
        [SerializeField] private AudioClip comboSound;
        [SerializeField] private AudioClip failSound;
        [SerializeField] private AudioClip buttonClick;
        [SerializeField] private AudioClip popupOpen;
        [SerializeField] private AudioClip stageClear;
        [SerializeField] private AudioClip gameOver;

        [Header("Special Block SFX")]
        [SerializeField] private AudioClip drillSound;
        [SerializeField] private AudioClip bombSound;
        [SerializeField] private AudioClip laserSound;
        [SerializeField] private AudioClip donutSound;
        [SerializeField] private AudioClip xBlockSound;
        [SerializeField] private AudioClip blockDestroySound;
        [SerializeField] private AudioClip blockLandSound;

        [Header("Settings")]
        [SerializeField] private float bgmVolume = 0.7f;
        [SerializeField] private float sfxVolume = 1f;

        private List<AudioSource> sfxPool = new List<AudioSource>();

        // 프로시저럴 오디오 클립 캐시
        private AudioClip proceduralRotate;
        private AudioClip proceduralMatch;
        private AudioClip proceduralMatch4;
        private AudioClip proceduralMatch5;
        private AudioClip proceduralFail;
        private AudioClip proceduralCombo;
        private AudioClip proceduralButtonClick;
        private AudioClip proceduralPopupOpen;
        private AudioClip proceduralBlockDestroy;
        private AudioClip proceduralBlockLand;
        private AudioClip proceduralDrill;
        private AudioClip proceduralBomb;
        private AudioClip proceduralLaser;
        private AudioClip proceduralDonut;
        private AudioClip proceduralXBlock;
        private AudioClip proceduralStageClear;
        private AudioClip proceduralGameOver;
        private AudioClip proceduralWarningBeep;
        private AudioClip proceduralSpecialGem;
        private AudioClip proceduralSpecialImpact;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null)
                    transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                InitializeAudioPool();
                LoadVolumeSettings();
                GenerateProceduralClips();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeAudioPool()
        {
            for (int i = 0; i < 10; i++)
            {
                GameObject sfxObj = new GameObject($"SFX_Pool_{i}");
                sfxObj.transform.SetParent(transform);
                AudioSource source = sfxObj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfxPool.Add(source);
            }
        }

        private void LoadVolumeSettings()
        {
            bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.7f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
        }

        /// <summary>
        /// 프로시저럴 AudioClip 생성 및 캐싱
        /// Inspector에서 AudioClip이 할당되지 않은 경우 프로시저럴 클립으로 폴백
        /// </summary>
        private void GenerateProceduralClips()
        {
            // 회전음: 짧은 스윕 톤
            proceduralRotate = Utils.ProceduralAudio.CreateSweep(300f, 500f, 0.1f);

            // 매칭 톤: C-E-G 화음 (3매치)
            proceduralMatch = Utils.ProceduralAudio.CreateChord(
                new float[] { 523f, 659f, 784f }, 0.15f);

            // 4매치 톤: 더 높은 화음
            proceduralMatch4 = Utils.ProceduralAudio.CreateChord(
                new float[] { 659f, 784f, 988f }, 0.18f);

            // 5매치 톤: 풍부한 화음
            proceduralMatch5 = Utils.ProceduralAudio.CreateChord(
                new float[] { 784f, 988f, 1175f, 1319f }, 0.2f);

            // 실패음: 하강 톤
            proceduralFail = Utils.ProceduralAudio.CreateSweep(400f, 200f, 0.2f);

            // 콤보 상승톤
            proceduralCombo = Utils.ProceduralAudio.CreateSweep(400f, 800f, 0.15f);

            // UI 클릭: 고주파 짧은 톤
            proceduralButtonClick = Utils.ProceduralAudio.CreateClick(0.05f);

            // 팝업 열기: 짧은 스윕 업
            proceduralPopupOpen = Utils.ProceduralAudio.CreateSweep(600f, 900f, 0.08f);

            // 블록 파괴: 짧은 노이즈 버스트 + 톤
            proceduralBlockDestroy = Utils.ProceduralAudio.CreateNoiseBurst(0.08f);

            // 블록 착지: 감쇠 바운스
            proceduralBlockLand = Utils.ProceduralAudio.CreateBounce(0.08f);

            // 드릴: 하강 스윕 (기계적)
            proceduralDrill = Utils.ProceduralAudio.CreateSweep(800f, 200f, 0.3f);

            // 폭탄: 노이즈 + 저음
            proceduralBomb = Utils.ProceduralAudio.CreateExplosion(0.35f);

            // 레이저: 고주파 스윕
            proceduralLaser = Utils.ProceduralAudio.CreateSweep(1200f, 600f, 0.25f);

            // 도넛: 밝은 상승톤
            proceduralDonut = Utils.ProceduralAudio.CreateSweep(500f, 1200f, 0.2f);

            // X블록: 이중 스윕
            proceduralXBlock = Utils.ProceduralAudio.CreateSweep(600f, 1000f, 0.2f);

            // 스테이지 클리어: 상승 화음
            proceduralStageClear = Utils.ProceduralAudio.CreateVictoryFanfare(0.6f);

            // 게임 오버: 하강 화음
            proceduralGameOver = Utils.ProceduralAudio.CreateGameOverSound(0.5f);

            // 경고 비프
            proceduralWarningBeep = Utils.ProceduralAudio.CreateTone(800f, 0.1f, 0.03f);

            // 특수 젬 생성
            proceduralSpecialGem = Utils.ProceduralAudio.CreateSweep(600f, 1400f, 0.15f);

            // 특수 블록 임팩트
            proceduralSpecialImpact = Utils.ProceduralAudio.CreateNoiseBurst(0.1f);
        }

        // ============================================================
        // BGM
        // ============================================================

        public void PlayBGM(AudioClip clip, bool loop = true)
        {
            if (bgmSource == null || clip == null) return;
            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }

        public void PlayMainBGM() => PlayBGM(mainBGM);
        public void PlayGameBGM() => PlayBGM(gameBGM);
        public void StopBGM() { if (bgmSource != null) bgmSource.Stop(); }

        // ============================================================
        // SFX 재생 (기본)
        // ============================================================

        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            AudioSource source = GetAvailableSource();
            if (source != null)
            {
                source.pitch = 1f;
                source.clip = clip;
                source.volume = sfxVolume * volumeMultiplier;
                source.Play();
            }
        }

        /// <summary>
        /// 피치 조절 SFX 재생 - 캐스케이드 깊이별 피치 상승에 사용
        /// </summary>
        public void PlaySFXWithPitch(AudioClip clip, float pitch, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            AudioSource source = GetAvailableSource();
            if (source != null)
            {
                source.clip = clip;
                source.pitch = pitch;
                source.volume = sfxVolume * volumeMultiplier;
                source.Play();
            }
        }

        /// <summary>
        /// 동시 재생 제한 SFX - 같은 클립이 N개 이상 동시 재생 중이면 차단
        /// 블록 파괴음, 낙하음 등 대량 동시 발생 사운드에 사용
        /// </summary>
        public void PlaySFXWithLimit(AudioClip clip, int maxSimultaneous, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            int playingCount = 0;
            foreach (var source in sfxPool)
            {
                if (source.isPlaying && source.clip == clip)
                    playingCount++;
            }

            if (playingCount >= maxSimultaneous) return;

            PlaySFX(clip, volumeMultiplier);
        }

        /// <summary>
        /// 피치 + 동시 재생 제한 SFX
        /// </summary>
        public void PlaySFXWithPitchAndLimit(AudioClip clip, float pitch, int maxSimultaneous, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            int playingCount = 0;
            foreach (var source in sfxPool)
            {
                if (source.isPlaying && source.clip == clip)
                    playingCount++;
            }

            if (playingCount >= maxSimultaneous) return;

            PlaySFXWithPitch(clip, pitch, volumeMultiplier);
        }

        /// <summary>
        /// 프로시저럴 톤 생성 (런타임)
        /// </summary>
        public AudioClip GenerateTone(float frequency, float duration, float fadeOut = 0.05f)
        {
            return Utils.ProceduralAudio.CreateTone(frequency, duration, fadeOut);
        }

        private AudioSource GetAvailableSource()
        {
            foreach (var source in sfxPool)
            {
                if (!source.isPlaying) return source;
            }
            GameObject sfxObj = new GameObject($"SFX_Pool_{sfxPool.Count}");
            sfxObj.transform.SetParent(transform);
            AudioSource newSource = sfxObj.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            sfxPool.Add(newSource);
            return newSource;
        }

        // ============================================================
        // 클립 해결: Inspector 할당 우선, 없으면 프로시저럴 폴백
        // ============================================================

        private AudioClip Resolve(AudioClip assigned, AudioClip procedural)
        {
            return assigned != null ? assigned : procedural;
        }

        // ============================================================
        // 게임 사운드 단축 메서드
        // ============================================================

        public void PlayRotateSound() => PlaySFX(Resolve(rotateSound, proceduralRotate));

        public void PlayMatchSound(int count = 3)
        {
            if (count >= 5) PlaySFX(Resolve(match5Sound, proceduralMatch5));
            else if (count >= 4) PlaySFX(Resolve(match4Sound, proceduralMatch4));
            else PlaySFX(Resolve(matchSound, proceduralMatch));
        }

        public void PlayProcessSound() => PlaySFX(processSound);
        public void PlaySpecialGemSound() => PlaySFX(Resolve(specialGemSound, proceduralSpecialGem));
        public void PlayPerfectGemSound() => PlaySFX(perfectGemSound);
        public void PlayBigBangSound() => PlaySFX(Resolve(bigBangSound, proceduralBomb));

        public void PlayComboSound() => PlaySFX(Resolve(comboSound, proceduralCombo));

        /// <summary>
        /// 캐스케이드 깊이에 따른 콤보 사운드 (피치 상승)
        /// pitch = 1.0 + cascadeDepth * 0.05 (반음 단위, 최대 +6반음)
        /// </summary>
        public void PlayComboSound(int cascadeDepth)
        {
            float pitch = 1.0f + Mathf.Min(cascadeDepth, 12) * 0.05f;
            PlaySFXWithPitch(Resolve(comboSound, proceduralCombo), pitch);
        }

        public void PlayFailSound() => PlaySFX(Resolve(failSound, proceduralFail));
        public void PlayButtonClick() => PlaySFX(Resolve(buttonClick, proceduralButtonClick), 0.8f);
        public void PlayPopupOpen() => PlaySFX(Resolve(popupOpen, proceduralPopupOpen));
        public void PlayStageClear() => PlaySFX(Resolve(stageClear, proceduralStageClear));
        public void PlayGameOver() => PlaySFX(Resolve(gameOver, proceduralGameOver));

        // 특수 블록 사운드
        public void PlayDrillSound() => PlaySFX(Resolve(drillSound, proceduralDrill));
        public void PlayBombSound() => PlaySFX(Resolve(bombSound, proceduralBomb));
        public void PlayLaserSound() => PlaySFX(Resolve(laserSound, proceduralLaser));
        public void PlayDonutSound() => PlaySFX(Resolve(donutSound, proceduralDonut));
        public void PlayXBlockSound() => PlaySFX(Resolve(xBlockSound, proceduralXBlock));

        // 블록 파괴/착지 (동시 재생 제한)
        public void PlayBlockDestroySound() => PlaySFXWithLimit(Resolve(blockDestroySound, proceduralBlockDestroy), 3, 0.7f);
        public void PlayBlockLandSound() => PlaySFXWithLimit(Resolve(blockLandSound, proceduralBlockLand), 5, 0.5f);

        // 경고 비프
        public void PlayWarningBeep() => PlaySFX(proceduralWarningBeep, 0.6f);

        // 특수 블록 임팩트
        public void PlaySpecialImpactSound() => PlaySFX(proceduralSpecialImpact, 0.8f);

        // 매칭 감지 톤
        public void PlayMatchDetectTone() => PlaySFX(Resolve(matchSound, proceduralMatch), 0.5f);

        // ============================================================
        // 볼륨 설정
        // ============================================================

        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
            PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
        }

        public void MuteAll(bool mute)
        {
            if (bgmSource != null) bgmSource.mute = mute;
            foreach (var source in sfxPool) source.mute = mute;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;
using JewelsHexaPuzzle.Utils;

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

        [Header("Enemy SFX")]
        [SerializeField] private AudioClip chromophageRemovalSound;

        [Header("Settings")]
        [SerializeField] private float bgmVolume = 0.7f;
        [SerializeField] private float sfxVolume = 1f;

        private bool isMuted = false;
        public bool IsMuted => isMuted;

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
        private AudioClip proceduralEnemySpawn;
        private AudioClip proceduralChromophageRemoval;

        // 캐스케이드 펜타토닉 음계 (C5, D5, E5, G5, A5, C6)
        private AudioClip[] proceduralCascadeNotes;

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
            isMuted = PlayerPrefs.GetInt("AudioMuted", 0) == 1;
            if (bgmSource != null) bgmSource.volume = bgmVolume;
            if (isMuted) MuteAll(true);
        }

        /// <summary>
        /// 프로시저럴 AudioClip 생성 및 캐싱
        /// Inspector에서 AudioClip이 할당되지 않은 경우 프로시저럴 클립으로 폴백
        /// </summary>
        private void GenerateProceduralClips()
        {
            // 회전음: 노이즈 밴드패스 스윕 (0.23s)
            proceduralRotate = ProceduralAudio.CreateRotateSound(0.23f);

            // 3매치: C6→E6→G6 아르페지오 (0.3s)
            proceduralMatch = ProceduralAudio.CreateMatchArpeggio3(0.3f);

            // 4매치: C6→E6→G6→C7 아르페지오 (0.35s)
            proceduralMatch4 = ProceduralAudio.CreateMatchArpeggio4(0.35f);

            // 5매치: C6→E6→G6→C7→E7 아르페지오 (0.5s)
            proceduralMatch5 = ProceduralAudio.CreateMatchArpeggio5(0.5f);

            // 실패음: E5→C5 하행 2음 (0.35s)
            proceduralFail = ProceduralAudio.CreateFailSound(0.35f);

            // 콤보: 기본 캐스케이드 노트 (하위 호환)
            proceduralCombo = ProceduralAudio.CreateComboRise(0.15f);

            // UI 클릭: Sine 880Hz +200Hz slide (0.11s)
            proceduralButtonClick = ProceduralAudio.CreateClick(0.11f);

            // 팝업 열기: Sine+Tri 440→880Hz (0.3s)
            proceduralPopupOpen = ProceduralAudio.CreatePopupSound(0.3f);

            // 블록 파괴: Sine 1200Hz -800Hz 급하강 (0.07s)
            proceduralBlockDestroy = ProceduralAudio.CreateNoiseBurst(0.07f);

            // 블록 착지: Sine+Noise 180Hz -60Hz (0.12s)
            proceduralBlockLand = ProceduralAudio.CreateBounce(0.12f);

            // 드릴: 오르골 태엽 + 부드러운 윙윙 (0.5s)
            proceduralDrill = ProceduralAudio.CreateDrillSound(0.5f);

            // 폭탄: 부드러운 "퐁" 꽃가루 캐논 (0.3s)
            proceduralBomb = ProceduralAudio.CreateExplosion(0.3f);

            // 레이저: 요정 지팡이 쉬머 빔 (0.5s)
            proceduralLaser = ProceduralAudio.CreateLaserSound(0.5f);

            // 도넛: 레인보우 쉬머 확장 물결 (0.8s)
            proceduralDonut = ProceduralAudio.CreateRainbowSound(0.8f);

            // X블록: 이중 크리스탈 띵 + 스파클 (0.6s)
            proceduralXBlock = ProceduralAudio.CreateXBlockSound(0.6f);

            // 스테이지 클리어: 오르골 팡파레 (2.0s)
            proceduralStageClear = ProceduralAudio.CreateVictoryFanfare(2.0f);

            // 게임 오버: 오르골 와인딩 다운 (1.5s)
            proceduralGameOver = ProceduralAudio.CreateGameOverSound(1.5f);

            // 경고 비프: Sine+Sq(5%) A5 2회 반복 (0.25s)
            proceduralWarningBeep = ProceduralAudio.CreateWarningBeep(0.25f);

            // 특수 젬 생성: 크리스탈 쉬머 + 띵글링 (0.5s)
            proceduralSpecialGem = ProceduralAudio.CreateSpecialGemSound(0.5f);

            // 특수 블록 임팩트: 노이즈 + 서브베이스 펀치
            proceduralSpecialImpact = ProceduralAudio.CreateImpact(0.1f);
            proceduralEnemySpawn = ProceduralAudio.CreateEnemySpawnSound(0.25f);

            // 캐스케이드 펜타토닉 개별 음 (C5, D5, E5, G5, A5, C6)
            float[] cascadeFreqs = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f };
            proceduralCascadeNotes = new AudioClip[cascadeFreqs.Length];
            for (int i = 0; i < cascadeFreqs.Length; i++)
                proceduralCascadeNotes[i] = ProceduralAudio.CreateCascadeNote(cascadeFreqs[i], 0.15f);
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
            return ProceduralAudio.CreateTone(frequency, duration, fadeOut);
        }

        /// <summary>
        /// 피치 랜덤 변형 SFX - 같은 사운드 반복 시 단조로움 방지
        /// 블록 파괴, 착지 등 반복 사운드에 사용
        /// pitchVariation: 0.05 = ±5% 피치 변형
        /// </summary>
        public void PlaySFXWithVariation(AudioClip clip, float pitchVariation = 0.05f, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            float pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            PlaySFXWithPitch(clip, pitch, volumeMultiplier);
        }

        /// <summary>
        /// 피치 랜덤 변형 + 동시 재생 제한 SFX
        /// </summary>
        public void PlaySFXWithVariationAndLimit(AudioClip clip, float pitchVariation, int maxSimultaneous, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            int playingCount = 0;
            foreach (var source in sfxPool)
            {
                if (source.isPlaying && source.clip == clip)
                    playingCount++;
            }

            if (playingCount >= maxSimultaneous) return;

            PlaySFXWithVariation(clip, pitchVariation, volumeMultiplier);
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

        // 볼륨 우선순위 (SFX 사양서 믹싱 가이드):
        // 스테이지 클리어 > 5매치 > 4매치 > 경고 > 3매치 > 버튼 > 특수블록 > 블록파괴 > 게임오버 > 착지 > 캐스케이드 > 회전
        public void PlayRotateSound() => PlaySFX(Resolve(rotateSound, proceduralRotate), 0.5f);

        public void PlayMatchSound(int count = 3)
        {
            if (count >= 5) PlaySFX(Resolve(match5Sound, proceduralMatch5), 1.0f);
            else if (count >= 4) PlaySFX(Resolve(match4Sound, proceduralMatch4), 0.9f);
            else PlaySFX(Resolve(matchSound, proceduralMatch), 0.8f);
        }

        public void PlayProcessSound() => PlaySFX(processSound);
        public void PlaySpecialGemSound() => PlaySFX(Resolve(specialGemSound, proceduralSpecialGem), 0.8f);
        public void PlayPerfectGemSound() => PlaySFX(perfectGemSound);
        public void PlayBigBangSound() => PlaySFX(Resolve(bigBangSound, proceduralBomb));

        public void PlayComboSound() => PlaySFX(Resolve(comboSound, proceduralCombo), 0.6f);

        /// <summary>
        /// 캐스케이드 깊이에 따른 콤보 사운드 - 펜타토닉 음계 개별 음 재생
        /// C5→D5→E5→G5→A5→C6 순서로 깊이별 다음 음
        /// </summary>
        public void PlayComboSound(int cascadeDepth)
        {
            if (comboSound != null)
            {
                float pitch = 1.0f + Mathf.Min(cascadeDepth, 12) * 0.05f;
                PlaySFXWithPitch(comboSound, pitch);
                return;
            }
            if (proceduralCascadeNotes != null && proceduralCascadeNotes.Length > 0)
            {
                int idx = Mathf.Min(cascadeDepth, proceduralCascadeNotes.Length - 1);
                float vol = Mathf.Lerp(0.35f, 0.55f, (float)idx / (proceduralCascadeNotes.Length - 1));
                PlaySFX(proceduralCascadeNotes[idx], vol);
            }
        }

        public void PlayFailSound() => PlaySFX(Resolve(failSound, proceduralFail), 0.6f);
        public void PlayButtonClick() => PlaySFX(Resolve(buttonClick, proceduralButtonClick), 0.8f);
        public void PlayPopupOpen() => PlaySFX(Resolve(popupOpen, proceduralPopupOpen), 0.7f);
        public void PlayStageClear() => PlaySFX(Resolve(stageClear, proceduralStageClear), 1.0f);
        public void PlayGameOver() => PlaySFX(Resolve(gameOver, proceduralGameOver), 0.6f);

        // 특수 블록 사운드
        public void PlayDrillSound() => PlaySFX(Resolve(drillSound, proceduralDrill), 0.7f);
        public void PlayBombSound() => PlaySFX(Resolve(bombSound, proceduralBomb), 0.7f);
        public void PlayLaserSound() => PlaySFX(Resolve(laserSound, proceduralLaser), 0.7f);
        public void PlayDonutSound() => PlaySFX(Resolve(donutSound, proceduralDonut), 0.7f);
        public void PlayXBlockSound() => PlaySFX(Resolve(xBlockSound, proceduralXBlock), 0.7f);

        // 블록 파괴/착지 (피치 변형 + 동시 재생 제한)
        public void PlayBlockDestroySound() => PlaySFXWithVariationAndLimit(Resolve(blockDestroySound, proceduralBlockDestroy), 0.08f, 3, 0.65f);
        public void PlayBlockLandSound() => PlaySFXWithVariationAndLimit(Resolve(blockLandSound, proceduralBlockLand), 0.06f, 5, 0.5f);

        // 경고 비프
        public void PlayWarningBeep() => PlaySFX(proceduralWarningBeep, 0.8f);

        // 특수 블록 임팩트
        public void PlaySpecialImpactSound() => PlaySFX(proceduralSpecialImpact, 0.7f);

        public void PlayEnemySpawnSound() => PlaySFX(proceduralEnemySpawn, 0.7f);

        // 색상도둑 제거 사운드 (슬라임 분해음 느낌)
        public void PlayChromophageRemovalSound() => PlaySFX(Resolve(chromophageRemovalSound, proceduralChromophageRemoval), 0.75f);

        // 매칭 감지 톤
        public void PlayMatchDetectTone() => PlaySFX(Resolve(matchSound, proceduralMatch), 0.4f);

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
            isMuted = mute;
            if (bgmSource != null) bgmSource.mute = mute;
            foreach (var source in sfxPool) source.mute = mute;
            PlayerPrefs.SetInt("AudioMuted", mute ? 1 : 0);
        }

        public bool ToggleMute()
        {
            MuteAll(!isMuted);
            return isMuted;
        }
    }
}

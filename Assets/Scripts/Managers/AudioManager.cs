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
        
        [Header("Settings")]
        [SerializeField] private float bgmVolume = 0.7f;
        [SerializeField] private float sfxVolume = 1f;
        
        private List<AudioSource> sfxPool = new List<AudioSource>();
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioPool();
                LoadVolumeSettings();
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
        
        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null) return;
            AudioSource source = GetAvailableSource();
            if (source != null)
            {
                source.clip = clip;
                source.volume = sfxVolume * volumeMultiplier;
                source.Play();
            }
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
        
        // 게임 사운드 단축 메서드
        public void PlayRotateSound() => PlaySFX(rotateSound);
        public void PlayMatchSound(int count = 3)
        {
            if (count >= 5) PlaySFX(match5Sound);
            else if (count >= 4) PlaySFX(match4Sound);
            else PlaySFX(matchSound);
        }
        public void PlayProcessSound() => PlaySFX(processSound);
        public void PlaySpecialGemSound() => PlaySFX(specialGemSound);
        public void PlayPerfectGemSound() => PlaySFX(perfectGemSound);
        public void PlayBigBangSound() => PlaySFX(bigBangSound);
        public void PlayComboSound() => PlaySFX(comboSound);
        public void PlayFailSound() => PlaySFX(failSound);
        public void PlayButtonClick() => PlaySFX(buttonClick, 0.8f);
        public void PlayPopupOpen() => PlaySFX(popupOpen);
        public void PlayStageClear() => PlaySFX(stageClear);
        public void PlayGameOver() => PlaySFX(gameOver);
        
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

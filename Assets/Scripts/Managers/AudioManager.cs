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

        // SFX/BGM 개별 음소거
        private bool isSfxMuted = false;
        private bool isBgmMuted = false;
        public bool IsSfxMuted => isSfxMuted;
        public bool IsBgmMuted => isBgmMuted;

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
        private AudioClip proceduralDonut;
        private AudioClip proceduralXBlock;
        private AudioClip proceduralStageClear;
        private AudioClip proceduralGameOver;
        private AudioClip proceduralWarningBeep;
        private AudioClip proceduralSpecialGem;
        private AudioClip proceduralSpecialImpact;
        private AudioClip proceduralEnemySpawn;
        private AudioClip proceduralShellConvert;
        private AudioClip proceduralChromophageRemoval;
        private AudioClip proceduralTransformTick;
        private AudioClip proceduralDroneSound;
        private AudioClip proceduralDroneStrike;
        private AudioClip proceduralMissionEntrance;
        private AudioClip proceduralMissionComplete;
        private AudioClip proceduralHeavyJump;
        private AudioClip proceduralHeavyLand;

        // 배경음악 캐시
        private AudioClip proceduralLobbySereneBGM;
        private AudioClip proceduralLobbyBrightBGM;
        private AudioClip proceduralLobbyDreamyBGM;
        private AudioClip proceduralGameplayTenseBGM;
        private AudioClip proceduralGameplayEnergeticBGM;
        private AudioClip proceduralGameplayEpicBGM;

        // 캐스케이드 펜타토닉 음계 (C5, D5, E5, G5, A5, C6)
        private AudioClip[] proceduralCascadeNotes;

        // AudioListener 주기적 체크 간격
        private float audioListenerCheckTimer = 0f;
        private const float LISTENER_CHECK_INTERVAL = 3f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent != null)
                    transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                EnsureAudioListener();
                EnsureAudioSources();
                InitializeAudioPool();
                LoadVolumeSettings();
                GenerateProceduralClips();

                LogAudioHealth();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // AudioListener가 사라진 경우를 대비한 주기적 체크
            audioListenerCheckTimer += Time.unscaledDeltaTime;
            if (audioListenerCheckTimer >= LISTENER_CHECK_INTERVAL)
            {
                audioListenerCheckTimer = 0f;
                EnsureAudioListener();
            }
        }

        /// <summary>
        /// 씬에 AudioListener가 없으면 AudioManager에 추가
        /// AudioListener가 없으면 모든 오디오가 재생되지 않음
        /// </summary>
        private void EnsureAudioListener()
        {
            AudioListener listener = FindObjectOfType<AudioListener>();
            if (listener == null)
            {
                gameObject.AddComponent<AudioListener>();
                Debug.Log("[AudioManager] AudioListener가 씬에 없어서 자동 생성했습니다.");
            }
        }

        /// <summary>
        /// bgmSource가 Inspector에서 할당되지 않은 경우 자동 생성
        /// </summary>
        private void EnsureAudioSources()
        {
            if (bgmSource == null)
            {
                GameObject bgmObj = new GameObject("BGM_Source");
                bgmObj.transform.SetParent(transform);
                bgmSource = bgmObj.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.volume = bgmVolume;
                Debug.Log("[AudioManager] bgmSource가 미할당되어 자동 생성했습니다.");
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
                source.mute = false;
                // HitStop(timeScale=0) 중에도 SFX 재생 가능
                source.ignoreListenerPause = true;
                sfxPool.Add(source);
            }
        }

        private void LoadVolumeSettings()
        {
            bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.7f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);

            // 저장된 음소거 상태 복원 (에디터/빌드 모두)
            isMuted = PlayerPrefs.GetInt("AudioMuted", 0) == 1;
            isSfxMuted = PlayerPrefs.GetInt("SFXMuted", 0) == 1;
            isBgmMuted = PlayerPrefs.GetInt("BGMMuted", 0) == 1;

            if (bgmSource != null) bgmSource.volume = bgmVolume;

            // 개별 음소거 상태 적용
            if (isMuted)
                MuteAll(true);
            else
            {
                if (isSfxMuted) MuteSFX(true);
                if (isBgmMuted) MuteBGM(true);
            }
        }

        /// <summary>
        /// 프로시저럴 AudioClip 생성 및 캐싱
        /// Inspector에서 AudioClip이 할당되지 않은 경우 프로시저럴 클립으로 폴백
        /// 각 클립 생성을 개별 try-catch로 보호 — 하나 실패해도 나머지 정상 생성
        /// </summary>
        private void GenerateProceduralClips()
        {
            int successCount = 0;
            int failCount = 0;

            // SFX 클립 생성 (개별 보호)
            proceduralRotate = SafeCreateClip("Rotate", () => ProceduralAudio.CreateRotateSound(0.23f), ref successCount, ref failCount);
            proceduralMatch = SafeCreateClip("Match3", () => ProceduralAudio.CreateMatchArpeggio3(0.3f), ref successCount, ref failCount);
            proceduralMatch4 = SafeCreateClip("Match4", () => ProceduralAudio.CreateMatchArpeggio4(0.35f), ref successCount, ref failCount);
            proceduralMatch5 = SafeCreateClip("Match5", () => ProceduralAudio.CreateMatchArpeggio5(0.5f), ref successCount, ref failCount);
            proceduralFail = SafeCreateClip("Fail", () => ProceduralAudio.CreateFailSound(0.35f), ref successCount, ref failCount);
            proceduralCombo = SafeCreateClip("Combo", () => ProceduralAudio.CreateComboRise(0.15f), ref successCount, ref failCount);
            proceduralButtonClick = SafeCreateClip("Click", () => ProceduralAudio.CreateClick(0.11f), ref successCount, ref failCount);
            proceduralPopupOpen = SafeCreateClip("Popup", () => ProceduralAudio.CreatePopupSound(0.3f), ref successCount, ref failCount);
            proceduralBlockDestroy = SafeCreateClip("BlockDestroy", () => ProceduralAudio.CreateNoiseBurst(0.07f), ref successCount, ref failCount);
            proceduralBlockLand = SafeCreateClip("BlockLand", () => ProceduralAudio.CreateBounce(0.12f), ref successCount, ref failCount);
            proceduralDrill = SafeCreateClip("Drill", () => ProceduralAudio.CreateDrillSound(0.5f), ref successCount, ref failCount);
            proceduralBomb = SafeCreateClip("Bomb", () => ProceduralAudio.CreateExplosion(0.3f), ref successCount, ref failCount);
            proceduralDonut = SafeCreateClip("Donut", () => ProceduralAudio.CreateRainbowSound(0.8f), ref successCount, ref failCount);
            proceduralXBlock = SafeCreateClip("XBlock", () => ProceduralAudio.CreateXBlockSound(0.6f), ref successCount, ref failCount);
            proceduralStageClear = SafeCreateClip("StageClear", () => ProceduralAudio.CreateVictoryFanfare(2.0f), ref successCount, ref failCount);
            proceduralGameOver = SafeCreateClip("GameOver", () => ProceduralAudio.CreateGameOverSound(1.5f), ref successCount, ref failCount);
            proceduralWarningBeep = SafeCreateClip("Warning", () => ProceduralAudio.CreateWarningBeep(0.25f), ref successCount, ref failCount);
            proceduralSpecialGem = SafeCreateClip("SpecialGem", () => ProceduralAudio.CreateSpecialGemSound(0.5f), ref successCount, ref failCount);
            proceduralSpecialImpact = SafeCreateClip("Impact", () => ProceduralAudio.CreateImpact(0.1f), ref successCount, ref failCount);
            proceduralEnemySpawn = SafeCreateClip("EnemySpawn", () => ProceduralAudio.CreateEnemySpawnSound(0.35f), ref successCount, ref failCount);
            proceduralShellConvert = SafeCreateClip("ShellConvert", () => ProceduralAudio.CreateShellConvertSound(0.08f), ref successCount, ref failCount);
            proceduralChromophageRemoval = SafeCreateClip("ChromophageRemoval", () => ProceduralAudio.CreateNoiseBurst(0.15f), ref successCount, ref failCount);
            proceduralTransformTick = SafeCreateClip("TransformTick", () => ProceduralAudio.CreateTransformTick(0.08f), ref successCount, ref failCount);
            proceduralDroneSound = SafeCreateClip("Drone", () => ProceduralAudio.CreateDroneSound(2.0f), ref successCount, ref failCount);
            proceduralDroneStrike = SafeCreateClip("DroneStrike", () => ProceduralAudio.CreateDroneStrikeSound(0.2f), ref successCount, ref failCount);
            proceduralMissionEntrance = SafeCreateClip("MissionEntrance", () => ProceduralAudio.CreateMissionEntranceSound(0.2f), ref successCount, ref failCount);
            proceduralMissionComplete = SafeCreateClip("MissionComplete", () => ProceduralAudio.CreateMissionCompleteSound(0.4f), ref successCount, ref failCount);
            proceduralHeavyJump = SafeCreateClip("HeavyJump", () => ProceduralAudio.CreateHeavyJumpSound(0.28f), ref successCount, ref failCount);
            proceduralHeavyLand = SafeCreateClip("HeavyLand", () => ProceduralAudio.CreateHeavyLandSound(0.38f), ref successCount, ref failCount);

            // 캐스케이드 펜타토닉 개별 음 (C5, D5, E5, G5, A5, C6)
            float[] cascadeFreqs = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f };
            proceduralCascadeNotes = new AudioClip[cascadeFreqs.Length];
            for (int i = 0; i < cascadeFreqs.Length; i++)
                proceduralCascadeNotes[i] = SafeCreateClip($"Cascade_{i}", () => ProceduralAudio.CreateCascadeNote(cascadeFreqs[i], 0.15f), ref successCount, ref failCount);

            // BGM 클립 생성 (30초 루프)
            proceduralLobbySereneBGM = SafeCreateClip("LobbySerene", () => ProceduralAudio.CreateLobbySereneBGM(30f), ref successCount, ref failCount);
            proceduralLobbyBrightBGM = SafeCreateClip("LobbyBright", () => ProceduralAudio.CreateLobbyBrightBGM(30f), ref successCount, ref failCount);
            proceduralLobbyDreamyBGM = SafeCreateClip("LobbyDreamy", () => ProceduralAudio.CreateLobbyDreamyBGM(30f), ref successCount, ref failCount);
            proceduralGameplayTenseBGM = SafeCreateClip("GameTense", () => ProceduralAudio.CreateGameplayTenseBGM(30f), ref successCount, ref failCount);
            proceduralGameplayEnergeticBGM = SafeCreateClip("GameEnergetic", () => ProceduralAudio.CreateGameplayEnergeticBGM(30f), ref successCount, ref failCount);
            proceduralGameplayEpicBGM = SafeCreateClip("GameEpic", () => ProceduralAudio.CreateGameplayEpicBGM(30f), ref successCount, ref failCount);

            Debug.Log($"[AudioManager] 프로시저럴 클립 생성 완료: 성공={successCount}, 실패={failCount}");
        }

        /// <summary>
        /// 개별 클립 생성 — 예외 발생 시 null 반환, 나머지 클립 생성에 영향 없음
        /// </summary>
        private AudioClip SafeCreateClip(string name, System.Func<AudioClip> creator, ref int success, ref int fail)
        {
            try
            {
                AudioClip clip = creator();
                if (clip != null)
                {
                    success++;
                    return clip;
                }
                Debug.LogWarning($"[AudioManager] 클립 생성 결과 null: {name}");
                fail++;
                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AudioManager] 클립 생성 실패 [{name}]: {e.Message}");
                fail++;
                return null;
            }
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
            bgmSource.mute = isBgmMuted;   // 저장된 음소거 상태 반영
            bgmSource.Play();
        }

        /// <summary>메인 BGM (Inspector 할당 → 프로시저럴 로비 폴백)</summary>
        public void PlayMainBGM() => PlayBGM(mainBGM != null ? mainBGM : proceduralLobbySereneBGM);

        /// <summary>게임 BGM (Inspector 할당 → 프로시저럴 게임플레이 폴백)</summary>
        public void PlayGameBGM() => PlayBGM(gameBGM != null ? gameBGM : proceduralGameplayEnergeticBGM);

        public void StopBGM() { if (bgmSource != null) bgmSource.Stop(); }

        // 로비 배경음악
        public void PlayLobbySereneBGM() => PlayBGM(proceduralLobbySereneBGM);
        public void PlayLobbyBrightBGM() => PlayBGM(proceduralLobbyBrightBGM);
        public void PlayLobbyDreamyBGM() => PlayBGM(proceduralLobbyDreamyBGM);

        /// <summary>로비 BGM 랜덤 재생 (3곡 중 1곡)</summary>
        public void PlayRandomLobbyBGM()
        {
            AudioClip[] lobbyClips = { proceduralLobbySereneBGM, proceduralLobbyBrightBGM, proceduralLobbyDreamyBGM };
            AudioClip pick = lobbyClips[Random.Range(0, lobbyClips.Length)];
            PlayBGM(pick);
        }

        // 인게임 배경음악
        public void PlayGameplayTenseBGM() => PlayBGM(proceduralGameplayTenseBGM);
        public void PlayGameplayEnergeticBGM() => PlayBGM(proceduralGameplayEnergeticBGM);
        public void PlayGameplayEpicBGM() => PlayBGM(proceduralGameplayEpicBGM);

        /// <summary>인게임 BGM 랜덤 재생 (3곡 중 1곡)</summary>
        public void PlayRandomGameplayBGM()
        {
            AudioClip[] gameClips = { proceduralGameplayTenseBGM, proceduralGameplayEnergeticBGM, proceduralGameplayEpicBGM };
            AudioClip pick = gameClips[Random.Range(0, gameClips.Length)];
            PlayBGM(pick);
        }

        // ============================================================
        // SFX 재생 (기본)
        // ============================================================

        public void PlaySFX(AudioClip clip, float volumeMultiplier = 1f)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlaySFX 호출됨 - clip이 null!");
                return;
            }
            AudioSource source = GetAvailableSource();
            if (source != null)
            {
                source.pitch = 1f;
                source.clip = clip;
                source.volume = sfxVolume * volumeMultiplier;
                source.Play();
                Debug.Log($"[AudioManager] PlaySFX: {clip.name}, vol={source.volume:F2}, mute={source.mute}, isPlaying={source.isPlaying}");
            }
            else
            {
                Debug.LogError("[AudioManager] PlaySFX: GetAvailableSource가 null 반환!");
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
                if (source != null && source.isPlaying && source.clip == clip)
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
                if (source != null && source.isPlaying && source.clip == clip)
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
                if (source != null && source.isPlaying && source.clip == clip)
                    playingCount++;
            }

            if (playingCount >= maxSimultaneous) return;

            PlaySFXWithVariation(clip, pitchVariation, volumeMultiplier);
        }

        private AudioSource GetAvailableSource()
        {
            // 기존 풀에서 사용 가능한 소스 탐색 (null 안전)
            for (int i = sfxPool.Count - 1; i >= 0; i--)
            {
                if (sfxPool[i] == null)
                {
                    sfxPool.RemoveAt(i);
                    continue;
                }
                if (!sfxPool[i].isPlaying) return sfxPool[i];
            }
            // 풀 확장 (SFX 개별 음소거 상태 반영, ignoreListenerPause 적용)
            GameObject sfxObj = new GameObject($"SFX_Pool_{sfxPool.Count}");
            sfxObj.transform.SetParent(transform);
            AudioSource newSource = sfxObj.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            newSource.mute = isSfxMuted;
            newSource.ignoreListenerPause = true;
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
        public void PlayDonutSound() => PlaySFX(Resolve(donutSound, proceduralDonut), 0.7f);
        public void PlayXBlockSound() => PlaySFX(Resolve(xBlockSound, proceduralXBlock), 0.7f);
        public void PlayDroneSound() => PlaySFX(proceduralDroneSound, 0.7f);
        public void PlayDroneStrikeSound() => PlaySFX(proceduralDroneStrike, 0.8f);

        /// <summary>
        /// 드론 비행 버즈 피치 실시간 조절 — 도플러 효과용
        /// sfxPool에서 드론 클립 재생 중인 소스를 찾아 피치 변경
        /// </summary>
        public void SetDronePitch(float pitch)
        {
            if (proceduralDroneSound == null) return;
            foreach (var source in sfxPool)
            {
                if (source != null && source.isPlaying && source.clip == proceduralDroneSound)
                {
                    source.pitch = pitch;
                }
            }
        }

        /// <summary>
        /// 드론 비행 버즈 사운드 정지 — 충돌 시점에 호출
        /// sfxPool에서 드론 클립 재생 중인 소스를 찾아 정지
        /// </summary>
        public void StopDroneSound()
        {
            if (proceduralDroneSound == null) return;
            foreach (var source in sfxPool)
            {
                if (source != null && source.isPlaying && source.clip == proceduralDroneSound)
                {
                    source.Stop();
                    source.clip = null;
                }
            }
        }

        // 블록 파괴/착지 (피치 변형 + 동시 재생 제한)
        public void PlayBlockDestroySound() => PlaySFXWithVariationAndLimit(Resolve(blockDestroySound, proceduralBlockDestroy), 0.08f, 3, 0.65f);
        public void PlayBlockLandSound() => PlaySFXWithVariationAndLimit(Resolve(blockLandSound, proceduralBlockLand), 0.06f, 5, 0.5f);

        // 경고 비프
        public void PlayWarningBeep() => PlaySFX(proceduralWarningBeep, 0.8f);

        /// <summary>
        /// 카운트업 틱 사운드 — progress(0~1)에 따라 피치가 올라감
        /// </summary>
        public void PlayCountUpTick(float progress)
        {
            // 피치: 800Hz → 1200Hz로 점진적 상승
            float freq = Mathf.Lerp(800f, 1200f, progress);
            AudioClip tick = ProceduralAudio.CreateTone(freq, 0.04f, 0.02f);
            if (tick != null)
                PlaySFX(tick, 0.5f);
        }

        // 특수 블록 임팩트
        public void PlaySpecialImpactSound() => PlaySFX(proceduralSpecialImpact, 0.7f);

        public void PlayEnemySpawnSound() => PlaySFX(proceduralEnemySpawn, 0.7f);

        // 쉘 변환 사운드 (벽돌 내려놓기)
        public void PlayShellConvertSound() => PlaySFX(proceduralShellConvert, 0.55f);

        // 색상도둑 제거 사운드 (슬라임 분해음 느낌)
        public void PlayChromophageRemovalSound() => PlaySFX(Resolve(chromophageRemovalSound, proceduralChromophageRemoval), 0.75f);

        // 매칭 감지 톤
        public void PlayMatchDetectTone() => PlaySFX(Resolve(matchSound, proceduralMatch), 0.4f);

        /// <summary>
        /// 미션 등장 사운드 — 복수 미션 시 순번에 따라 피치 상승
        /// </summary>
        public void PlayMissionEntranceSound(int index = 0, int total = 1)
        {
            float pitch = 1.0f + 0.15f * ((float)index / Mathf.Max(1, total - 1));
            if (total <= 1) pitch = 1.0f;
            PlaySFXWithPitch(proceduralMissionEntrance, pitch, 0.7f);
        }

        /// <summary>
        /// 미션 완료 사운드 — C메이저 상승 아르페지오 차임
        /// </summary>
        public void PlayMissionCompleteSound() => PlaySFX(proceduralMissionComplete, 0.8f);

        // Heavy 고블린 사운드
        /// <summary>Heavy 고블린 점프 시작 — 낮은 저음 whoosh</summary>
        public void PlayHeavyJumpSound() => PlaySFX(proceduralHeavyJump, 0.85f);
        /// <summary>Heavy 고블린 착지 충격 — 지진 느낌의 강한 저음 충격음</summary>
        public void PlayHeavyLandSound() => PlaySFX(proceduralHeavyLand, 1.0f);

        // 특수 블록 변환 틱 사운드 (XBlock 합성 시 순차 변환용)
        // index: 변환 순번, total: 전체 블록 수 → 피치를 점진적으로 올림
        public void PlayTransformTickSound(int index = 0, int total = 1)
        {
            float pitch = 1.0f + 0.5f * ((float)index / Mathf.Max(1, total - 1));
            PlaySFXWithPitchAndLimit(proceduralTransformTick, pitch, 4, 0.55f);
        }

        // ============================================================
        // 볼륨 설정
        // ============================================================

        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null) bgmSource.volume = bgmVolume;
            PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
            PlayerPrefs.Save();
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.Save();
        }

        public void MuteAll(bool mute)
        {
            isMuted = mute;
            isSfxMuted = mute;
            isBgmMuted = mute;
            if (bgmSource != null) bgmSource.mute = mute;
            foreach (var source in sfxPool)
            {
                if (source != null) source.mute = mute;
            }
            PlayerPrefs.SetInt("AudioMuted", mute ? 1 : 0);
            PlayerPrefs.SetInt("SFXMuted", mute ? 1 : 0);
            PlayerPrefs.SetInt("BGMMuted", mute ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool ToggleMute()
        {
            MuteAll(!isMuted);
            return isMuted;
        }

        // ── SFX 개별 음소거 ──

        public void MuteSFX(bool mute)
        {
            isSfxMuted = mute;
            foreach (var source in sfxPool)
            {
                if (source != null) source.mute = mute;
            }
            PlayerPrefs.SetInt("SFXMuted", mute ? 1 : 0);
            // 전체 음소거 상태 동기화
            isMuted = isSfxMuted && isBgmMuted;
            PlayerPrefs.SetInt("AudioMuted", isMuted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool ToggleSFXMute()
        {
            MuteSFX(!isSfxMuted);
            return isSfxMuted;
        }

        // ── BGM 개별 음소거 ──

        public void MuteBGM(bool mute)
        {
            isBgmMuted = mute;
            if (bgmSource != null) bgmSource.mute = mute;
            PlayerPrefs.SetInt("BGMMuted", mute ? 1 : 0);
            // 전체 음소거 상태 동기화
            isMuted = isSfxMuted && isBgmMuted;
            PlayerPrefs.SetInt("AudioMuted", isMuted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool ToggleBGMMute()
        {
            MuteBGM(!isBgmMuted);
            return isBgmMuted;
        }

        // ============================================================
        // 오디오 건강 체크 & 강제 복구
        // ============================================================

        /// <summary>
        /// 프로시저럴 클립이 생성되었는지 확인, 없으면 재생성
        /// 초기화 실패나 예외로 클립이 null인 경우 복구
        /// </summary>
        public void EnsureClipsGenerated()
        {
            if (proceduralRotate == null || proceduralMatch == null || proceduralBlockDestroy == null)
            {
                Debug.LogWarning("[AudioManager] 프로시저럴 클립이 null! 재생성 시작...");
                GenerateProceduralClips();
                LogAudioHealth();
            }
        }

        /// <summary>
        /// 오디오 시스템 상태 로그 출력 (디버그용)
        /// </summary>
        private void LogAudioHealth()
        {
            int nullClips = 0;
            int totalClips = 0;
            System.Action<AudioClip, string> check = (clip, name) =>
            {
                totalClips++;
                if (clip == null) { nullClips++; Debug.LogWarning($"  [NULL] {name}"); }
            };
            check(proceduralRotate, "Rotate");
            check(proceduralMatch, "Match3");
            check(proceduralMatch4, "Match4");
            check(proceduralMatch5, "Match5");
            check(proceduralFail, "Fail");
            check(proceduralButtonClick, "Click");
            check(proceduralPopupOpen, "Popup");
            check(proceduralBlockDestroy, "BlockDestroy");
            check(proceduralBlockLand, "BlockLand");
            check(proceduralDrill, "Drill");
            check(proceduralBomb, "Bomb");
            check(proceduralDonut, "Donut");
            check(proceduralXBlock, "XBlock");
            check(proceduralStageClear, "StageClear");
            check(proceduralGameOver, "GameOver");
            check(proceduralDroneSound, "Drone");

            AudioListener listener = FindObjectOfType<AudioListener>();
            Debug.Log($"[AudioManager] ══ 오디오 건강 체크 ══\n" +
                      $"  isMuted={isMuted}, sfxVolume={sfxVolume}, bgmVolume={bgmVolume}\n" +
                      $"  poolSize={sfxPool.Count}, nullClips={nullClips}/{totalClips}\n" +
                      $"  AudioListener존재={listener != null}, AudioListener.volume={AudioListener.volume}\n" +
                      $"  AudioListener.pause={AudioListener.pause}\n" +
                      $"  bgmSource null={bgmSource == null}");
        }

        /// <summary>
        /// 오디오 시스템 강제 리셋 (문제 발생 시 호출)
        /// mute 해제, 볼륨 복원, AudioListener 확보, 프로시저럴 클립 재생성
        /// </summary>
        public void ForceResetAudio()
        {
            Debug.Log("[AudioManager] 오디오 시스템 강제 리셋 시작...");

            // mute 해제 (전체 + 개별 플래그 모두 리셋)
            isMuted = false;
            isBgmMuted = false;
            isSfxMuted = false;
            PlayerPrefs.SetInt("AudioMuted", 0);
            PlayerPrefs.SetInt("BGMMuted", 0);
            PlayerPrefs.SetInt("SFXMuted", 0);
            if (bgmSource != null) bgmSource.mute = false;
            foreach (var source in sfxPool)
            {
                if (source != null)
                {
                    source.mute = false;
                    source.Stop();
                }
            }

            // 볼륨 기본값 복원
            sfxVolume = 1f;
            bgmVolume = 0.7f;
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
            if (bgmSource != null) bgmSource.volume = bgmVolume;

            // AudioListener 확보
            EnsureAudioListener();

            // 프로시저럴 클립 재생성
            GenerateProceduralClips();

            LogAudioHealth();
            Debug.Log("[AudioManager] 오디오 시스템 강제 리셋 완료");
        }

        /// <summary>
        /// 에디터 전용: 시작 시 테스트 사운드를 재생하여 오디오 시스템이 정상인지 확인
        /// 1초 후 버튼 클릭음을 재생합니다. 이 소리가 들리면 오디오 시스템은 정상입니다.
        /// </summary>
        private System.Collections.IEnumerator PlayStartupTestSound()
        {
            yield return new WaitForSeconds(1.0f);

            Debug.Log("========================================");
            Debug.Log("[AudioManager] ★ 시작 테스트 사운드 재생 시도 ★");
            Debug.Log($"  isMuted={isMuted}, sfxVolume={sfxVolume}");
            Debug.Log($"  proceduralButtonClick null? {proceduralButtonClick == null}");
            Debug.Log($"  sfxPool.Count={sfxPool.Count}");

            AudioListener listener = FindObjectOfType<AudioListener>();
            Debug.Log($"  AudioListener 존재? {listener != null}");
            if (listener != null)
                Debug.Log($"  AudioListener 활성? {listener.enabled}, GO 활성? {listener.gameObject.activeInHierarchy}");

            // AudioListener.volume 확인
            Debug.Log($"  AudioListener.volume={AudioListener.volume}");
            Debug.Log($"  AudioListener.pause={AudioListener.pause}");

            if (proceduralButtonClick != null)
            {
                AudioSource testSource = GetAvailableSource();
                if (testSource != null)
                {
                    Debug.Log($"  테스트 소스: mute={testSource.mute}, enabled={testSource.enabled}, GO활성={testSource.gameObject.activeInHierarchy}");
                    testSource.pitch = 1f;
                    testSource.clip = proceduralButtonClick;
                    testSource.volume = 1f; // 최대 볼륨
                    testSource.mute = false; // 강제 음소거 해제
                    testSource.Play();
                    Debug.Log($"  ★ 테스트 사운드 재생됨! isPlaying={testSource.isPlaying} ★");
                    Debug.Log($"  → 이 소리가 들리면 오디오 시스템 정상");
                    Debug.Log($"  → 안 들리면 Unity 에디터 Game 뷰의 Mute Audio 버튼 또는 시스템 볼륨 확인");
                }
                else
                {
                    Debug.LogError("  테스트 소스를 가져올 수 없습니다!");
                }
            }
            else
            {
                Debug.LogError("  proceduralButtonClick이 null - GenerateProceduralClips 실패!");
            }
            Debug.Log("========================================");
        }
    }
}

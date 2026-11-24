using UnityEngine;

public class AudioManager : MonoBehaviour
{
    internal static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _musicEnabled = PlayerPrefs.GetInt(PrefKeyMusic, 1) == 1;
        _sfxEnabled   = PlayerPrefs.GetInt(PrefKeysfx,   1) == 1;
        _musicVolume  = PlayerPrefs.GetFloat(PrefKeyMusicVol, 0.5f);
        _sfxVolume    = PlayerPrefs.GetFloat(PrefKeySfxVol,   1.0f);

        ApplyMusicVolume();
        ApplySfxVolume();
    }

    private const string PrefKeyMusic    = "audio_music_enabled";
    private const string PrefKeysfx      = "audio_sfx_enabled";
    private const string PrefKeyMusicVol = "audio_music_volume";
    private const string PrefKeySfxVol   = "audio_sfx_volume";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource wheelSegmentSource;
    [SerializeField] private AudioSource reserveSource;
    // FF: Optional dedicated source for looping SFX (falls back to reserveSource if null)
    [SerializeField] private AudioSource loopSfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip clipGameMainBg;
    [SerializeField] private AudioClip clipBetPlusMinus;
    [SerializeField] private AudioClip clipMaxBetReached;
    [SerializeField] private AudioClip clip3UspinWinLineLoop;
    [SerializeField] private AudioClip clipWinObjectBg;
    [SerializeField] private AudioClip clipPrimaryActionButton;
    [SerializeField] private AudioClip clipGeneralButtonClick;
    [SerializeField] private AudioClip clipPopupOpenClose;
    [SerializeField] private AudioClip clipAutoplayPanelOpen;
    [SerializeField] private AudioClip clipFeatureOpenLoop;
    [SerializeField] private AudioClip clipFreeSpinBg;
    [SerializeField] private AudioClip clipWheelSegmentTick;
    [SerializeField] private AudioClip clipWinLinePhase1Start;
    [SerializeField] private AudioClip clipReelStop;

    #region FF: Future Fortune Sound Clips
    [Header("FF Sounds - Reels")]
    [SerializeField] private AudioClip clipSpinStart;
    [SerializeField] private AudioClip clipReelSpinLoop;
    [SerializeField] private AudioClip clipFFReelStop;
    [SerializeField] private AudioClip clipBonusLand;

    [Header("FF Sounds - Nudge")]
    [SerializeField] private AudioClip clipNudgeShow;
    [SerializeField] private AudioClip clipNudgeTap;
    [SerializeField] private AudioClip clipNudgeSlide;

    [Header("FF Sounds - Wins")]
    [SerializeField] private AudioClip clipWinSmall;
    [SerializeField] private AudioClip clipWinBig;
    [SerializeField] private AudioClip clipCountUpLoop;
    [SerializeField] private AudioClip clipCountUpEnd;

    [Header("FF Sounds - Free Games")]
    [SerializeField] private AudioClip clipFreeGamesTrigger;
    [SerializeField] private AudioClip clipFreeGamesAward;
    [SerializeField] private AudioClip clipStartButton;
    [SerializeField] private AudioClip clipGridOpen;
    [SerializeField] private AudioClip clipGridClose;
    [SerializeField] private AudioClip clipFreeSpinTick;
    [SerializeField] private AudioClip clipRetrigger;
    [SerializeField] private AudioClip clipOutcomeOpen;
    [SerializeField] private AudioClip clipTake;

    [Header("FF Sounds - Jackpot")]
    [SerializeField] private AudioClip clipJackpotWin;
    #endregion

    private bool _musicEnabled = true;
    private bool _sfxEnabled   = true;
    private float _musicVolume = 0.5f;
    private float _sfxVolume   = 1.0f;

    internal bool MusicEnabled => _musicEnabled;
    internal bool SfxEnabled   => _sfxEnabled;
    internal float MusicVolume => _musicVolume;
    internal float SfxVolume   => _sfxVolume;

    internal void SetMusicEnabled(bool on)
    {
        _musicEnabled = on;
        PlayerPrefs.SetInt(PrefKeyMusic, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    internal void SetSfxEnabled(bool on)
    {
        _sfxEnabled = on;
        PlayerPrefs.SetInt(PrefKeysfx, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplySfxVolume();
    }

    internal void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeyMusicVol, _musicVolume);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    internal void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeySfxVol, _sfxVolume);
        PlayerPrefs.Save();
        ApplySfxVolume();
    }

    private void ApplyMusicVolume()
    {
        if (bgMusicSource == null) return;
        bgMusicSource.volume = _musicEnabled ? _musicVolume : 0f;
    }

    private void ApplySfxVolume()
    {
        float v = _sfxEnabled ? _sfxVolume : 0f;
        if (uiSource           != null) uiSource.volume           = v;
        if (wheelSegmentSource != null) wheelSegmentSource.volume = v;
        if (reserveSource      != null) reserveSource.volume      = v;
        if (loopSfxSource      != null) loopSfxSource.volume      = v; // FF
    }

    /// <summary>
    /// Uses UI source (AudioSource 2). If busy/playing, falls back to reserve source (AudioSource 4).
    /// </summary>
    private void PlayUISound(AudioClip clip)
    {
        if (!_sfxEnabled || clip == null) return;

        if (uiSource != null && !uiSource.isPlaying)
        {
            uiSource.PlayOneShot(clip);
        }
        else if (reserveSource != null)
        {
            reserveSource.PlayOneShot(clip);
        }
        else if (uiSource != null)
        {
            uiSource.PlayOneShot(clip);
        }
    }

    private void PlayLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip   = clip;
        source.loop   = true;
        source.volume = _musicEnabled ? _musicVolume : 0f;
        source.Play();
    }

    private void StopSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.loop = false;
    }

    // 1. Game Main BG
    internal void PlayBgMusic()
    {
        if (bgMusicSource == null || clipGameMainBg == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clipGameMainBg) return;

        bgMusicSource.clip   = clipGameMainBg;
        bgMusicSource.loop   = true;
        bgMusicSource.volume = _musicEnabled ? _musicVolume : 0f;
        bgMusicSource.Play();
    }

    internal void PlayMainBg() => PlayBgMusic();

    internal void StopBgMusic()
    {
        StopSource(bgMusicSource);
    }

    // 2. Bet Plus / Bet Minus (one for both)
    internal void PlayBetPlusMinus()
    {
        PlayUISound(clipBetPlusMinus);
    }

    internal void PlayBetPlus()  => PlayBetPlusMinus();
    internal void PlayBetMinus() => PlayBetPlusMinus();

    // 3. Max Bet Reached
    internal void PlayMaxBetReached()
    {
        PlayUISound(clipMaxBetReached);
    }

    // 4. 3 USpin Win Line Loop
    internal void Play3UspinWinLineLoop()
    {
        if (!_sfxEnabled || clip3UspinWinLineLoop == null) return;
        PlayLoop(uiSource, clip3UspinWinLineLoop);
    }

    internal void Stop3UspinWinLineLoop()
    {
        if (uiSource != null && uiSource.clip == clip3UspinWinLineLoop)
        {
            StopSource(uiSource);
        }
    }

    // 5. Win Object BG (Play at Open)
    internal void PlayWinObjectBg()
    {
        if (!_sfxEnabled || clipWinObjectBg == null) return;
        PlayLoop(uiSource, clipWinObjectBg);
    }

    internal void StopWinObjectBg()
    {
        if (uiSource != null && uiSource.clip == clipWinObjectBg)
        {
            StopSource(uiSource);
        }
        if (reserveSource != null && reserveSource.clip == clipWinObjectBg)
        {
            StopSource(reserveSource);
        }
    }

    // 6. Spin / Stop / Take / AutoplayStop / WheelStart Btn Sound
    internal void PlayPrimaryActionButton()
    {
        PlayUISound(clipPrimaryActionButton != null ? clipPrimaryActionButton : clipGeneralButtonClick);
    }

    internal void PlaySpinStart()    => PlayPrimaryActionButton();
    internal void PlaySpinStop()     => PlayPrimaryActionButton();
    internal void PlayTakeButton()   => PlayPrimaryActionButton();
    internal void PlayAutoplayStop() => PlayPrimaryActionButton();
    internal void PlayWheelStart()   => PlayPrimaryActionButton();

    // 7. General Button Click
    internal void PlayButton()
    {
        PlayUISound(clipGeneralButtonClick);
    }

    internal void PlayGeneralButtonClick() => PlayButton();

    // 8. Popup Open Close Sound
    internal void PlayPopupOpenClose()
    {
        PlayUISound(clipPopupOpenClose != null ? clipPopupOpenClose : clipGeneralButtonClick);
    }

    internal void PlayPopupClose() => PlayPopupOpenClose();
    internal void PlayPopupOpen()  => PlayPopupOpenClose();

    // 9. Autoplay Panel Open Sound
    internal void PlayAutoplayPanelOpen()
    {
        PlayUISound(clipAutoplayPanelOpen != null ? clipAutoplayPanelOpen : clipPopupOpenClose);
    }

    // 10. Bonus Wheel & MoneyBag Feature Open Sound (loop until feature enabled)
    internal void PlayFeatureOpenLoop()
    {
        if (clipFeatureOpenLoop == null) return;
        PlayLoop(bgMusicSource, clipFeatureOpenLoop);
    }

    internal void StopFeatureOpenLoop()
    {
        if (bgMusicSource != null && bgMusicSource.clip == clipFeatureOpenLoop)
        {
            StopBgMusic();
            PlayBgMusic(); // Resume main BG
        }
    }

    // 11. FreeSpin BG (loop while free spin)
    internal void PlayFreeSpinBg()
    {
        if (clipFreeSpinBg == null) return;
        PlayLoop(bgMusicSource, clipFreeSpinBg);
    }

    // 12. Bonus Wheel Spin Segment Tick
    internal void PlayWheelSegmentTick()
    {
        if (!_sfxEnabled || clipWheelSegmentTick == null) return;
        if (wheelSegmentSource != null)
        {
            wheelSegmentSource.PlayOneShot(clipWheelSegmentTick);
        }
        else
        {
            PlayUISound(clipWheelSegmentTick);
        }
    }

    // 13. Win Line Phase 1 Start
    internal void PlayWinLinePhase1Start()
    {
        PlayUISound(clipWinLinePhase1Start);
    }

    // 14. Slot Reel Column Stop Sound
    internal void PlayReelStop()
    {
        if (!_sfxEnabled || clipReelStop == null) return;

        if (wheelSegmentSource != null)
            wheelSegmentSource.PlayOneShot(clipReelStop);
        else
            PlayUISound(clipReelStop);
    }

    #region FF: Future Fortune Sound System

    // FF: Per-clip cooldown tracker to prevent duplicate one-shots in the same frame (e.g. turbo 5 reels stopping together)
    private readonly System.Collections.Generic.Dictionary<AudioClip, float> _clipLastPlayed = new System.Collections.Generic.Dictionary<AudioClip, float>();
    private const float MinClipInterval = 0.03f;

    private AudioSource _activeSpinLoopSource;
    private AudioSource _activeCountUpLoopSource;

    private bool IsClipOnCooldown(AudioClip clip)
    {
        if (clip == null) return false;
        float now = Time.unscaledTime;
        if (_clipLastPlayed.TryGetValue(clip, out float lastTime))
        {
            if (now - lastTime < MinClipInterval) return true;
        }
        _clipLastPlayed[clip] = now;
        return false;
    }

    /// <summary>
    /// FF: Plays one-shot SFX with fallback and per-clip cooldown.
    /// If both ff and fallback are null, remains silent without exceptions.
    /// </summary>
    private void PlayOneShot(AudioClip ff, AudioClip fallback, float pitch = 1.0f)
    {
        if (!_sfxEnabled) return;
        AudioClip clip = ff != null ? ff : fallback;
        if (clip == null) return; // silence

        if (IsClipOnCooldown(clip)) return;

        AudioSource source = null;
        if (!Mathf.Approximately(pitch, 1.0f))
        {
            // Use wheelSegmentSource (or reserveSource) for pitch-adjusted one-shots (e.g. bonus landings)
            source = wheelSegmentSource != null ? wheelSegmentSource : (reserveSource != null ? reserveSource : uiSource);
            if (source != null)
            {
                source.pitch = pitch;
                source.PlayOneShot(clip);
            }
            return;
        }

        // Standard non-pitched one-shot
        if (uiSource != null && !uiSource.isPlaying) source = uiSource;
        else if (reserveSource != null && !reserveSource.isPlaying) source = reserveSource;
        else if (wheelSegmentSource != null && !wheelSegmentSource.isPlaying) source = wheelSegmentSource;
        else source = uiSource != null ? uiSource : reserveSource;

        if (source != null)
        {
            source.pitch = 1.0f;
            source.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// FF: Starts an SFX loop on preferred source (or fallback loopSfxSource/reserveSource/uiSource).
    /// </summary>
    private void StartLoop(AudioClip ff, AudioClip fallback, ref AudioSource activeSourceRef, AudioSource preferredSource)
    {
        if (!_sfxEnabled) return;
        AudioClip clip = ff != null ? ff : fallback;
        if (clip == null) return; // silence if no clip

        AudioSource src = preferredSource;
        if (src == null) src = loopSfxSource != null ? loopSfxSource : (reserveSource != null ? reserveSource : uiSource);
        if (src == null) return;

        src.clip = clip;
        src.loop = true;
        src.volume = _sfxEnabled ? _sfxVolume : 0f;
        src.pitch = 1.0f;
        src.Play();
        activeSourceRef = src;
    }

    /// <summary>
    /// FF: Stops an active SFX loop.
    /// </summary>
    private void StopLoop(ref AudioSource activeSourceRef)
    {
        if (activeSourceRef != null)
        {
            StopSource(activeSourceRef);
            activeSourceRef = null;
        }
    }

    // ── REELS ──────────────────────────────────────────────────────────
    internal void PlayFFSpinStart() => PlayOneShot(clipSpinStart, clipPrimaryActionButton);

    internal void StartReelSpinLoop()
    {
        AudioSource preferred = loopSfxSource != null ? loopSfxSource : (wheelSegmentSource != null ? wheelSegmentSource : reserveSource);
        StartLoop(clipReelSpinLoop, null, ref _activeSpinLoopSource, preferred);
    }

    internal void StopReelSpinLoop() => StopLoop(ref _activeSpinLoopSource);

    internal void PlayFFReelStop() => PlayOneShot(clipFFReelStop, clipReelStop);

    internal void PlayBonusLand(int nth)
    {
        float pitch = nth <= 1 ? 1.0f : (nth == 2 ? 1.1f : 1.2f);
        PlayOneShot(clipBonusLand, clipReelStop, pitch);
    }

    // ── NUDGE ──────────────────────────────────────────────────────────
    internal void PlayNudgeShow()  => PlayOneShot(clipNudgeShow, clipPopupOpenClose);
    internal void PlayNudgeTap()   => PlayOneShot(clipNudgeTap, clipGeneralButtonClick);
    internal void PlayNudgeSlide() => PlayOneShot(clipNudgeSlide, clipReelStop);

    // ── WINS ───────────────────────────────────────────────────────────
    internal void PlayWinSmall() => PlayOneShot(clipWinSmall, clipWinLinePhase1Start);
    internal void PlayWinBig()   => PlayOneShot(clipWinBig, clipWinObjectBg);

    internal void StartCountUpLoop()
    {
        AudioSource preferred = reserveSource != null ? reserveSource : uiSource;
        StartLoop(clipCountUpLoop, null, ref _activeCountUpLoopSource, preferred);
    }

    internal void StopCountUpLoop() => StopLoop(ref _activeCountUpLoopSource);

    internal void PlayCountUpEnd() => PlayOneShot(clipCountUpEnd, null);

    // ── FREE GAMES ─────────────────────────────────────────────────────
    internal void PlayFreeGamesTrigger() => PlayOneShot(clipFreeGamesTrigger, clip3UspinWinLineLoop);
    internal void PlayFreeGamesAward()   => PlayOneShot(clipFreeGamesAward, clipWinObjectBg);
    internal void PlayStartButton()      => PlayOneShot(clipStartButton, clipPrimaryActionButton);
    internal void PlayGridOpen()          => PlayOneShot(clipGridOpen, null);
    internal void PlayGridClose()         => PlayOneShot(clipGridClose, null);
    internal void PlayFreeSpinTick()      => PlayOneShot(clipFreeSpinTick, null);
    internal void PlayRetrigger()         => PlayOneShot(clipRetrigger, clipWinObjectBg);
    internal void PlayOutcomeOpen()       => PlayOneShot(clipOutcomeOpen, clipWinObjectBg);
    internal void PlayFFTake()            => PlayOneShot(clipTake, clipPrimaryActionButton);

    // ── JACKPOT ────────────────────────────────────────────────────────
    internal void PlayJackpotWin() => PlayOneShot(clipJackpotWin, clipWinObjectBg);

    // ── SAFETY ─────────────────────────────────────────────────────────
    internal void StopAllLoops()
    {
        StopReelSpinLoop();
        StopCountUpLoop();
        StopWinObjectBg();
        Stop3UspinWinLineLoop();
        StopFeatureOpenLoop();
    }

    #endregion

    private bool isForceMuted = false;

    internal void SetMuteAll(bool forceMute)
    {
        if (forceMute == isForceMuted) return;
        isForceMuted = forceMute;

        if (forceMute)
        {
            StopAllLoops(); // FF: Stop all active loops on mute
        }

        AudioListener.volume = forceMute ? 0f : 1f;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetMuteAll(!hasFocus);
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            StopAllLoops(); // FF: Stop all active loops on app pause
        }
        SetMuteAll(isPaused);
    }
}

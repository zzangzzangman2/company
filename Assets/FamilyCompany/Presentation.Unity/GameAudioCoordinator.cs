using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    public enum GamePaperSfx
    {
        Place = 0,
        Rustle = 1,
        PageFlip = 2
    }

    public enum GameUiSfx
    {
        Click = 0,
        Confirm = 1,
        Error = 2,
        Back = 3,
        Select = 4,
        Switch = 5,
        Tick = 6,
        Open = 7,
        Close = 8
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class GameAudioCoordinator : MonoBehaviour
    {
        public const string TitleBgmId = "title_gentle_theme";
        public const string OfficeBgmId = "hub_gentle_brew";

        private const string BgmResourceRoot = "Audio/BGM/";
        private const string SfxResourceRoot = "Audio/SFX/";
        private const float ScreenPollIntervalSeconds = 0.2f;
        private const float DefaultFadeSeconds = 0.7f;

        private static GameAudioCoordinator _instance;

        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

        private readonly Dictionary<string, AudioClip> _bgmClips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioClip> _sfxClips =
            new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;
        private Coroutine _bgmFadeRoutine;
        private string _requestedBgmId = string.Empty;
        private string _currentBgmId = string.Empty;
        private float _screenPollRemaining;
        private int _footstepSequence;

        public static GameAudioCoordinator Instance
        {
            get
            {
                EnsureRuntimeInstance();
                return _instance;
            }
        }

        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;
        public string CurrentBgmId => _currentBgmId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateAfterSceneLoad()
        {
            EnsureRuntimeInstance();
        }

        private static void EnsureRuntimeInstance()
        {
            if (_instance != null) return;
            _instance = FindFirstObjectByType<GameAudioCoordinator>();
            if (_instance != null) return;

            var audioObject = new GameObject(nameof(GameAudioCoordinator));
            _instance = audioObject.AddComponent<GameAudioCoordinator>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.spatialBlend = 0f;
            _bgmSource.ignoreListenerPause = true;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
            _sfxSource.loop = false;
            _sfxSource.spatialBlend = 0f;
            _sfxSource.ignoreListenerPause = true;
            _sfxSource.volume = sfxVolume;
            PlayBgm(TitleBgmId, 0f);
        }

        private void Update()
        {
            _screenPollRemaining -= Time.unscaledDeltaTime;
            if (_screenPollRemaining > 0f) return;
            _screenPollRemaining = ScreenPollIntervalSeconds;
            RefreshScreenBgm();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (_bgmSource != null && _bgmFadeRoutine == null)
            {
                _bgmSource.volume = bgmVolume;
            }
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            if (_sfxSource != null) _sfxSource.volume = sfxVolume;
        }

        public bool PlayBgm(string bgmId, float fadeSeconds = DefaultFadeSeconds)
        {
            var normalizedId = NormalizeClipId(bgmId);
            if (string.IsNullOrEmpty(normalizedId)) return false;

            var clip = LoadClip(_bgmClips, BgmResourceRoot, normalizedId);
            if (clip == null)
            {
                Debug.LogWarning($"Missing BGM resource: {normalizedId}", this);
                return false;
            }

            if (_requestedBgmId == normalizedId)
            {
                if (_bgmSource != null && !_bgmSource.isPlaying)
                {
                    _bgmSource.Play();
                }

                return true;
            }

            _requestedBgmId = normalizedId;
            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(FadeToBgm(clip, normalizedId, Mathf.Max(0f, fadeSeconds)));
            return true;
        }

        public void StopBgm(float fadeSeconds = DefaultFadeSeconds)
        {
            _requestedBgmId = string.Empty;
            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            _bgmFadeRoutine = StartCoroutine(FadeOutBgm(Mathf.Max(0f, fadeSeconds)));
        }

        public bool PlaySfx(string sfxId, float volumeScale = 1f)
        {
            var normalizedId = NormalizeClipId(sfxId);
            if (string.IsNullOrEmpty(normalizedId)) return false;
            var clip = LoadClip(_sfxClips, SfxResourceRoot, normalizedId);
            if (clip == null)
            {
                Debug.LogWarning($"Missing SFX resource: {normalizedId}", this);
                return false;
            }

            _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
            return true;
        }

        public bool PlayPaperSfx(GamePaperSfx cue = GamePaperSfx.Place)
        {
            switch (cue)
            {
                case GamePaperSfx.Rustle: return PlaySfx("paper_rustle");
                case GamePaperSfx.PageFlip: return PlaySfx("page_flip");
                default: return PlaySfx("paper_place");
            }
        }

        public bool PlayDoorSfx(bool opening)
        {
            return PlaySfx(opening ? "door_open" : "door_close");
        }

        public bool PlayFootstepSfx()
        {
            var clipId = (_footstepSequence++ & 1) == 0 ? "footstep_1" : "footstep_2";
            return PlaySfx(clipId);
        }

        public bool PlayCoinsSfx(bool large = false)
        {
            return PlaySfx(large ? "coins_large" : "coins");
        }

        public bool PlayUiSfx(GameUiSfx cue = GameUiSfx.Click)
        {
            switch (cue)
            {
                case GameUiSfx.Confirm: return PlaySfx("ui_confirm");
                case GameUiSfx.Error: return PlaySfx("ui_error");
                case GameUiSfx.Back: return PlaySfx("ui_back");
                case GameUiSfx.Select: return PlaySfx("ui_select");
                case GameUiSfx.Switch: return PlaySfx("ui_switch");
                case GameUiSfx.Tick: return PlaySfx("ui_tick");
                case GameUiSfx.Open: return PlaySfx("ui_open");
                case GameUiSfx.Close: return PlaySfx("ui_close");
                default: return PlaySfx("ui_click");
            }
        }

        private void RefreshScreenBgm()
        {
            var bootstrap = FindFirstObjectByType<PrototypeBootstrap>();
            var useOfficeBgm = bootstrap != null && bootstrap.HasSession &&
                               bootstrap.UiScreen != PrototypeUiScreen.MainMenu &&
                               bootstrap.UiScreen != PrototypeUiScreen.NewGameSlots &&
                               bootstrap.UiScreen != PrototypeUiScreen.ConfirmNewGame;
            PlayBgm(useOfficeBgm ? OfficeBgmId : TitleBgmId);
        }

        private IEnumerator FadeToBgm(AudioClip clip, string bgmId, float fadeSeconds)
        {
            if (_bgmSource.isPlaying && fadeSeconds > 0f)
            {
                var fadeOutSeconds = fadeSeconds * 0.5f;
                var startVolume = _bgmSource.volume;
                for (var elapsed = 0f; elapsed < fadeOutSeconds; elapsed += Time.unscaledDeltaTime)
                {
                    _bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeOutSeconds);
                    yield return null;
                }
            }

            _bgmSource.Stop();
            _bgmSource.clip = clip;
            _bgmSource.volume = fadeSeconds > 0f ? 0f : bgmVolume;
            _bgmSource.Play();
            _currentBgmId = bgmId;

            if (fadeSeconds > 0f)
            {
                var fadeInSeconds = fadeSeconds * 0.5f;
                for (var elapsed = 0f; elapsed < fadeInSeconds; elapsed += Time.unscaledDeltaTime)
                {
                    _bgmSource.volume = Mathf.Lerp(0f, bgmVolume, elapsed / fadeInSeconds);
                    yield return null;
                }
            }

            _bgmSource.volume = bgmVolume;
            _bgmFadeRoutine = null;
        }

        private IEnumerator FadeOutBgm(float fadeSeconds)
        {
            var startVolume = _bgmSource.volume;
            if (fadeSeconds > 0f)
            {
                for (var elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
                {
                    _bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeSeconds);
                    yield return null;
                }
            }

            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmSource.volume = bgmVolume;
            _currentBgmId = string.Empty;
            _bgmFadeRoutine = null;
        }

        private static AudioClip LoadClip(
            IDictionary<string, AudioClip> cache,
            string resourceRoot,
            string clipId)
        {
            if (cache.TryGetValue(clipId, out var cached)) return cached;
            var clip = Resources.Load<AudioClip>(resourceRoot + clipId);
            if (clip != null) cache.Add(clipId, clip);
            return clip;
        }

        private static string NormalizeClipId(string clipId)
        {
            if (string.IsNullOrWhiteSpace(clipId)) return string.Empty;
            var normalized = clipId.Trim();
            if (normalized.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - 4);
            }

            return normalized.IndexOfAny(new[] { '/', '\\' }) >= 0 ? string.Empty : normalized;
        }
    }
}

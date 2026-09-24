using System;
using BotanicalGardenQR.FrontendShell.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class FullScriptAudioSettings
    {
        const float MusicVideoDuck = .12f;
        bool _muted, _sinkVideoPlaying;

        internal float MusicVolume { get; private set; } = .18f;
        internal float EffectsVolume { get; private set; } = .42f;
        internal bool Muted => _muted;
        internal float EffectiveMusicGain => _muted ? 0f : MusicVolume * (_sinkVideoPlaying ? MusicVideoDuck : 1f);
        internal float EffectiveEffectsGain => _muted ? 0f : EffectsVolume;

        internal void AdjustMusic(float delta) => MusicVolume = Mathf.Clamp01(MusicVolume + delta);
        internal void AdjustEffects(float delta) => EffectsVolume = Mathf.Clamp01(EffectsVolume + delta);
        internal void SetMuted(bool muted) => _muted = muted;
        internal void SetSinkVideoPlaying(bool playing) => _sinkVideoPlaying = playing;
    }

    // One service survives room changes. Only its two authorized sources bypass
    // the journey's listener silence; other narration and media remain muted.
    internal sealed class FullScriptAudioRuntime : MonoBehaviour, IDisposable
    {
        const float VolumeStep = .1f;
        readonly FullScriptAudioSettings _settings = new FullScriptAudioSettings();
        AudioSource _music, _effects;
        AudioClip _musicClip, _confirmClip, _pageClip, _roomClip;
        GameObject _controls;
        TMP_Text _musicValue, _effectsValue, _muteLabel, _soundLabel;
        Button _soundButton;
        Func<bool> _inputAllowed;
        bool _musicStarted, _disposed, _appPaused, _appFocused = true;
        float _musicGain;

        internal FullScriptAudioSettings Settings => _settings;
        internal bool ApplicationAudioSuspended => _appPaused || !_appFocused;

        internal static FullScriptAudioRuntime Create(Transform viewer, TMP_FontAsset font, Func<bool> inputAllowed)
        {
            if (!viewer) throw new ArgumentNullException(nameof(viewer));
            var root = new GameObject("FullScriptAudioRuntime");
            var runtime = root.AddComponent<FullScriptAudioRuntime>();
            try
            {
                runtime.Initialize(viewer, font, inputAllowed);
                return runtime;
            }
            catch
            {
                runtime.Dispose();
                throw;
            }
        }

        void Initialize(Transform viewer, TMP_FontAsset font, Func<bool> inputAllowed)
        {
            _inputAllowed = inputAllowed;
            _music = gameObject.AddComponent<AudioSource>();
            _effects = gameObject.AddComponent<AudioSource>();
            ConfigureSource(_music);
            ConfigureSource(_effects);
            _music.loop = true;
            if (Application.isPlaying)
            {
                _musicClip = CreateBackgroundClip();
                _confirmClip = CreateFeedbackClip("FullScriptConfirm", 880f, .12f);
                _pageClip = CreateFeedbackClip("FullScriptPage", 660f, .1f);
                _roomClip = CreateFeedbackClip("FullScriptRoomChange", 523.25f, .18f);
            }
            _music.clip = _musicClip;
            _musicGain = _settings.EffectiveMusicGain;
            _music.volume = _musicGain;
            _effects.volume = _settings.EffectiveEffectsGain;
            CreateControls(viewer, font);
            ClinicalNearTouch.ActionAccepted += HandleTouchAccepted;
        }

        internal static void ConfigureSource(AudioSource source)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = true;
            source.ignoreListenerVolume = true;
        }

        internal static AudioClip CreateBackgroundClip(int sampleRate = 22050)
        {
            var samples = CreateBackgroundSamples(sampleRate);
            var clip = AudioClip.Create("FullScriptSoftAmbientLoop", samples.Length, 1, Mathf.Max(8000, sampleRate), false);
            clip.SetData(samples, 0);
            return clip;
        }

        internal static float[] CreateBackgroundSamples(int sampleRate = 22050)
        {
            sampleRate = Mathf.Max(8000, sampleRate);
            const float seconds = 16f;
            var sampleCount = Mathf.RoundToInt(sampleRate * seconds);
            var samples = new float[sampleCount];
            var chords = new[,]
            {
                { 130.81f, 164.81f, 196f },
                { 110f, 130.81f, 164.81f },
                { 87.31f, 110f, 130.81f },
                { 98f, 123.47f, 146.83f }
            };
            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var chord = Mathf.Min(3, (int)(time / 4f));
                var inChord = time - chord * 4f;
                var phrase = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inChord / .24f)) *
                             (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((inChord - 3.76f) / .24f)));
                var loopEdge = Mathf.Clamp01(Mathf.Min(time, seconds - time) / .55f);
                var envelope = phrase * loopEdge * .024f;
                var value = .55f * Mathf.Sin(2f * Mathf.PI * chords[chord, 0] * time) +
                            .30f * Mathf.Sin(2f * Mathf.PI * chords[chord, 1] * time) +
                            .15f * Mathf.Sin(2f * Mathf.PI * chords[chord, 2] * time);
                samples[i] = value * envelope;
            }
            return samples;
        }

        static AudioClip CreateFeedbackClip(string name, float frequency, float seconds, int sampleRate = 22050)
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * seconds));
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleCount;
                var envelope = Mathf.Min(1f, t * 35f) * (1f - t) * (1f - t) * .28f;
                var sweep = frequency * (1f - .12f * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * sweep * t) * envelope;
            }
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void CreateControls(Transform viewer, TMP_FontAsset font)
        {
            _controls = new GameObject("FullScriptSoundControls", typeof(RectTransform), typeof(Canvas));
            var board = (RectTransform)_controls.transform;
            board.sizeDelta = new Vector2(500, 360);
            board.localScale = Vector3.one * .0008f;
            var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            board.SetPositionAndRotation(viewer.position + forward * .62f - viewer.right * .34f + Vector3.up * .27f,
                Quaternion.LookRotation(forward, Vector3.up));
            var canvas = _controls.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewer.GetComponent<Camera>();
            canvas.sortingOrder = 115;

            var panel = Rect(board, "SoundSettingsPanel", 0, 60, 470, 270);
            Frame(panel);
            Label(panel, font, "MusicTitle", 0, 93, 390, 38, 23).text = "背景音乐";
            ClinicalPanelStyle.Button(panel, font, "MusicDown", "−", -130, 44, 84, 54,
                () => AdjustMusic(-VolumeStep));
            _musicValue = Label(panel, font, "MusicVolume", 0, 44, 120, 48, 22);
            _musicValue.alignment = TextAlignmentOptions.Center;
            ClinicalPanelStyle.Button(panel, font, "MusicUp", "+", 130, 44, 84, 54,
                () => AdjustMusic(VolumeStep), true);
            Label(panel, font, "EffectsTitle", 0, -15, 390, 38, 23).text = "按键音效";
            ClinicalPanelStyle.Button(panel, font, "EffectsDown", "−", -130, -64, 84, 54,
                () => AdjustEffects(-VolumeStep));
            _effectsValue = Label(panel, font, "EffectsVolume", 0, -64, 120, 48, 22);
            _effectsValue.alignment = TextAlignmentOptions.Center;
            ClinicalPanelStyle.Button(panel, font, "EffectsUp", "+", 130, -64, 84, 54,
                () => AdjustEffects(VolumeStep), true);
            var mute = ClinicalPanelStyle.Button(panel, font, "ToggleSoundMute", "", -106, -126, 174, 50,
                ToggleMute);
            _muteLabel = mute.GetComponentInChildren<TMP_Text>();
            ClinicalPanelStyle.Button(panel, font, "CloseSoundSettings", "关闭", 112, -126, 150, 50,
                () => SetPanelVisible(false));
            panel.gameObject.SetActive(false);

            _soundButton = ClinicalPanelStyle.Button(board, font, "OpenSoundSettings", "声音", 0, -143, 180, 64,
                () => SetPanelVisible(!panel.gameObject.activeSelf));
            _soundLabel = _soundButton.GetComponentInChildren<TMP_Text>();
            ClinicalNearTouch.Bind(board, () => _inputAllowed == null || _inputAllowed());
            RefreshControls();
        }

        void SetPanelVisible(bool visible)
        {
            if (!_controls) return;
            var panel = _controls.transform.Find("SoundSettingsPanel");
            if (panel) panel.gameObject.SetActive(visible);
            if (_soundLabel) _soundLabel.text = visible ? "声音设置" : "声音";
        }

        void AdjustMusic(float delta) { _settings.AdjustMusic(delta); RefreshControls(); }
        void AdjustEffects(float delta) { _settings.AdjustEffects(delta); RefreshControls(); }
        void ToggleMute() { _settings.SetMuted(!_settings.Muted); RefreshControls(); }

        void RefreshControls()
        {
            if (_musicValue) _musicValue.text = Mathf.RoundToInt(_settings.MusicVolume * 100f) + "%";
            if (_effectsValue) _effectsValue.text = Mathf.RoundToInt(_settings.EffectsVolume * 100f) + "%";
            if (_muteLabel) _muteLabel.text = _settings.Muted ? "开启声音" : "静音";
            if (_soundLabel && _soundLabel.text == "") _soundLabel.text = "声音";
            if (_music) _music.volume = _settings.EffectiveMusicGain;
            if (_effects) _effects.volume = _settings.EffectiveEffectsGain;
        }

        void HandleTouchAccepted(ClinicalTouchFeedbackKind kind)
        {
            var clip = kind == ClinicalTouchFeedbackKind.RoomChange ? _roomClip :
                kind == ClinicalTouchFeedbackKind.Page ? _pageClip : _confirmClip;
            PlayEffect(clip);
        }

        internal void PlayConfirmation(AudioClip authoredClip) => PlayEffect(authoredClip ? authoredClip : _confirmClip);

        void PlayEffect(AudioClip clip)
        {
            if (!Application.isPlaying || _disposed || _appPaused || !_appFocused || !_effects || !clip) return;
            _effects.PlayOneShot(clip);
        }

        internal void StartMusic()
        {
            if (!Application.isPlaying || _disposed || !_music || !_musicClip || _musicStarted) return;
            _musicStarted = true;
            if (!_appPaused && _appFocused) _music.Play();
        }

        internal void SetSinkVideoPlaying(bool playing) => _settings.SetSinkVideoPlaying(playing);

        void Update()
        {
            if (_disposed) return;
            _musicGain = Mathf.MoveTowards(_musicGain, _settings.EffectiveMusicGain, Time.unscaledDeltaTime * .8f);
            if (_music) _music.volume = _musicGain;
            if (_effects) _effects.volume = _settings.EffectiveEffectsGain;
        }

        void OnApplicationPause(bool paused) => SetApplicationPaused(paused);
        void OnApplicationFocus(bool focused) => SetApplicationFocused(focused);

        internal void SetApplicationPaused(bool paused) { _appPaused = paused; SyncApplicationAudioState(); }
        internal void SetApplicationFocused(bool focused) { _appFocused = focused; SyncApplicationAudioState(); }

        void SyncApplicationAudioState()
        {
            if (_appPaused || !_appFocused)
            {
                if (_music) _music.Pause();
                if (_effects) _effects.Pause();
                return;
            }
            if (_musicStarted && _music) _music.UnPause();
            if (_effects) _effects.UnPause();
        }

        void OnDestroy()
        {
            Cleanup(destroyRoot: false);
        }

        public void Dispose() => Cleanup(destroyRoot: true);

        void Cleanup(bool destroyRoot)
        {
            if (_disposed) return;
            _disposed = true;
            ClinicalNearTouch.ActionAccepted -= HandleTouchAccepted;
            if (_music) _music.Stop();
            if (_effects) _effects.Stop();
            if (_musicClip) DestroyAsset(_musicClip);
            if (_confirmClip) DestroyAsset(_confirmClip);
            if (_pageClip) DestroyAsset(_pageClip);
            if (_roomClip) DestroyAsset(_roomClip);
            var controls = _controls;
            _controls = null;
            if (controls) DestroyObject(controls);
            if (destroyRoot && this)
            {
                var root = gameObject;
                if (root) DestroyObject(root);
            }
        }

        static void DestroyAsset(UnityEngine.Object asset)
        {
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }

        static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}

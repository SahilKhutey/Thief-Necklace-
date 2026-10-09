using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ThiefAndTheNecklace.UI
{
    /// <summary>Optional sound playback. Empty cue libraries are valid and silent.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const string MasterKey = "tn.audio.master";
        private const string MusicKey = "tn.audio.music";
        private const string AmbienceKey = "tn.audio.ambience";
        private const string SfxKey = "tn.audio.sfx";
        private const string UiKey = "tn.audio.ui";
        private readonly Dictionary<AudioBus, AudioSource> _sources = new Dictionary<AudioBus, AudioSource>();
        private Dictionary<string, AudioCue> _cues;
        private IDisposable _phaseSubscription;
        private IDisposable _voteSubscription;
        private IDisposable _revealSubscription;
        private IDisposable _scoreSubscription;

        public float MasterVolume { get => PlayerPrefs.GetFloat(MasterKey, 1f); set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); ApplyVolumes(); } }
        public float MusicVolume { get => PlayerPrefs.GetFloat(MusicKey, 0.7f); set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); ApplyVolumes(); } }
        public float AmbienceVolume { get => PlayerPrefs.GetFloat(AmbienceKey, 0.6f); set { PlayerPrefs.SetFloat(AmbienceKey, Mathf.Clamp01(value)); ApplyVolumes(); } }
        public float SfxVolume { get => PlayerPrefs.GetFloat(SfxKey, 0.8f); set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); ApplyVolumes(); } }
        public float UiVolume { get => PlayerPrefs.GetFloat(UiKey, 0.8f); set { PlayerPrefs.SetFloat(UiKey, Mathf.Clamp01(value)); ApplyVolumes(); } }

        public void Initialize(GameEventBus bus)
        {
            foreach (AudioBus category in Enum.GetValues(typeof(AudioBus)))
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = category == AudioBus.Music || category == AudioBus.Ambience;
                _sources[category] = source;
            }
            var library = Resources.Load<AudioCueLibrary>("Audio/AudioCueLibrary");
            _cues = library == null || library.cues == null
                ? new Dictionary<string, AudioCue>()
                : library.cues.Where(c => c != null && !string.IsNullOrWhiteSpace(c.cueId)).GroupBy(c => c.cueId).ToDictionary(g => g.Key, g => g.First());
            if (bus != null)
            {
                _phaseSubscription = bus.Subscribe<RoundPhaseChangedEvent>(OnPhaseChanged);
                _voteSubscription = bus.Subscribe<VoteFeedbackEvent>(e => PlayCue(e.Locked ? "vote_locked" : "vote_selected"));
                _revealSubscription = bus.Subscribe<RevealFeedbackEvent>(e => PlayCue(e.ThiefCaught ? "thief_caught" : "thief_escaped"));
                _scoreSubscription = bus.Subscribe<ScoreFeedbackEvent>(_ => PlayCue("score_settle"));
            }
            ApplyVolumes();
        }

        public void PlayCue(string cueId)
        {
            if (string.IsNullOrEmpty(cueId) || _cues == null || !_cues.TryGetValue(cueId, out var cue) || cue.clips == null || cue.clips.Length == 0) return;
            var clip = cue.clips[UnityEngine.Random.Range(0, cue.clips.Length)];
            if (clip == null || !_sources.TryGetValue(cue.bus, out var source)) return;
            source.pitch = UnityEngine.Random.Range(Mathf.Min(cue.pitchMin, cue.pitchMax), Mathf.Max(cue.pitchMin, cue.pitchMax));
            if (cue.bus == AudioBus.Music || cue.bus == AudioBus.Ambience)
            {
                source.clip = clip;
                source.loop = true;
                source.volume = CategoryVolume(cue.bus) * cue.volume;
                source.Play();
            }
            else source.PlayOneShot(clip, cue.volume);
        }

        public void SaveSettings() => PlayerPrefs.Save();

        private void OnPhaseChanged(RoundPhaseChangedEvent e)
        {
            switch (e.Phase)
            {
                case RoundPhase.Intro: PlayCue("scenario_intro"); break;
                case RoundPhase.PrivateReveal: PlayCue("card_reveal"); break;
                case RoundPhase.Discussion: PlayCue("discussion_start"); break;
                case RoundPhase.Voting: PlayCue("voting_start"); break;
                case RoundPhase.Reveal: PlayCue("truth_reveal"); break;
                case RoundPhase.MatchEnd: PlayCue("match_result"); break;
            }
        }

        private void ApplyVolumes()
        {
            foreach (var pair in _sources) pair.Value.volume = CategoryVolume(pair.Key);
        }

        private float CategoryVolume(AudioBus bus)
        {
            var category = bus == AudioBus.Music ? MusicVolume : bus == AudioBus.Ambience ? AmbienceVolume : bus == AudioBus.Ui ? UiVolume : SfxVolume;
            return MasterVolume * category;
        }

        private void OnDestroy()
        {
            _phaseSubscription?.Dispose(); _voteSubscription?.Dispose();
            _revealSubscription?.Dispose(); _scoreSubscription?.Dispose();
        }
    }
}

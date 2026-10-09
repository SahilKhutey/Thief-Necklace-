using UnityEngine;

namespace ThiefAndTheNecklace.UI
{
    public enum AudioBus { Music, Ambience, Sfx, Ui }

    [CreateAssetMenu(fileName = "AudioCue", menuName = "Thief & the Necklace/Audio/Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        public string cueId;
        public AudioBus bus = AudioBus.Sfx;
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitchMin = 1f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1f;
    }
}

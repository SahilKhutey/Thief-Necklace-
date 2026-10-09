using UnityEngine;

namespace ThiefAndTheNecklace.UI
{
    [CreateAssetMenu(fileName = "AudioCueLibrary", menuName = "Thief & the Necklace/Audio/Cue Library")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        public AudioCue[] cues = new AudioCue[0];
    }
}

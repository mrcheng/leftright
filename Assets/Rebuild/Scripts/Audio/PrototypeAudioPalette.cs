using UnityEngine;

namespace LeftRight.Rebuild
{
    [CreateAssetMenu(menuName = "LeftRight/Audio palette")]
    public sealed class PrototypeAudioPalette : ScriptableObject
    {
        public AudioClip titleMusic;
        public AudioClip roundMusic;
        [Header("Music beat timing")]
        public float titleBeatsPerMinute = 115f;
        public float titleFirstBeatSeconds = .5109f;
        public float roundBeatsPerMinute = 137f;
        public float roundFirstBeatSeconds = .4106f;
        public AudioClip leftCall;
        public AudioClip rightCall;
        public AudioClip landing;
        public AudioClip healthPickup;
        public AudioClip[] walking;
        public AudioClip[] running;
    }
}

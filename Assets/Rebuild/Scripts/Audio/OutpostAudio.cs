using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class OutpostAudio : MonoBehaviour
    {
        public PrototypeAudioPalette palette;
        public AudioSource music;
        public AudioSource voice;
        public AudioSource effects;
        public double MusicStartDspTime { get; private set; }
        public bool MusicHasStarted => music.isPlaying && AudioSettings.dspTime >= MusicStartDspTime;

        void Start()
        {
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel", 1f));
            music.volume *= volume;
            voice.volume *= volume;
            effects.volume *= volume;
        }

        public void PlayTitleMusic() => PlayMusic(palette.titleMusic);
        public void PlayRoundMusic() => PlayMusic(palette.roundMusic);
        public void ScheduleTitleOnFirstBeat(double dspTime)
        {
            music.Stop();
            music.clip = palette.titleMusic;
            music.loop = true;
            music.timeSamples = Mathf.RoundToInt(palette.titleFirstBeatSeconds * music.clip.frequency);
            MusicStartDspTime = dspTime;
            music.PlayScheduled(dspTime);
        }

        public void StopPlayback()
        {
            music.Stop();
            voice.Stop();
            effects.Stop();
            MusicStartDspTime = 0;
        }
        public void PlayDirection(bool right)
        {
            voice.clip = right ? palette.rightCall : palette.leftCall;
            voice.Play();
        }
        public void PlayLanding() => effects.PlayOneShot(palette.landing);
        public void PlayHealthPickup() => effects.PlayOneShot(palette.healthPickup);

        public void PlayFootstep(bool running)
        {
            var clips = running ? palette.running : palette.walking;
            if (clips != null && clips.Length > 0)
                effects.PlayOneShot(clips[Random.Range(0, clips.Length)]);
        }

        void PlayMusic(AudioClip clip)
        {
            if (music.clip == clip && music.isPlaying) return;
            music.Stop();
            MusicStartDspTime = 0;
            music.clip = clip;
            music.loop = true;
            music.Play();
        }
    }
}

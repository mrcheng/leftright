using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class OutpostAudio : MonoBehaviour
    {
        public PrototypeAudioPalette palette;
        public AudioSource music;
        public AudioSource voice;
        public AudioSource effects;
        [Range(0,1)] public float windVolume=.15f;
        public AudioSource Wind { get; private set; }
        public double MusicStartDspTime { get; private set; }
        public int PlaybackVersion { get; private set; }
        public bool MusicHasStarted => music.isPlaying && AudioSettings.dspTime >= MusicStartDspTime;

        void Start()
        {
            float volume = Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel", 1f));
            music.volume *= volume;
            voice.volume *= volume;
            effects.volume *= volume;
            // Atmosphere is independent of round music: countdowns, menu changes
            // and victory playback must not restart the loop or interrupt its seam.
            Wind=gameObject.AddComponent<AudioSource>();
            Wind.clip=Resources.Load<AudioClip>("EveningWind");
            Wind.loop=true;Wind.playOnAwake=false;Wind.spatialBlend=0;
            Wind.volume=windVolume*volume;
            if(Wind.clip!=null)Wind.Play();
        }

        void OnEnable(){if(Wind!=null&&Wind.clip!=null)Wind.Play();}
        void OnDisable(){if(Wind!=null)Wind.Stop();}

        public void PlayTitleMusic() => PlayMusic(palette.titleMusic);
        public void PlayRoundMusic() => PlayMusic(palette.roundMusic);
        public void ScheduleTitleOnFirstBeat(double dspTime)
        {
            PlaybackVersion++;
            music.Stop();
            music.clip = palette.titleMusic;
            music.loop = true;
            music.timeSamples = Mathf.RoundToInt(palette.titleFirstBeatSeconds * music.clip.frequency);
            MusicStartDspTime = dspTime;
            music.PlayScheduled(dspTime);
        }

        public void StopPlayback()
        {
            PlaybackVersion++;
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
            PlaybackVersion++;
            music.Stop();
            MusicStartDspTime = 0;
            music.clip = clip;
            music.loop = true;
            music.Play();
        }
    }
}

using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>One storm mix for the whole course, independent of lane count.
    /// Every lane may request the warning/impact, but each phase plays only once
    /// per round. Lane cleanup never stops or resets another lane's storm audio.</summary>
    [DisallowMultipleComponent]
    public sealed class SharedLightningAudio : MonoBehaviour
    {
        public bool WarningPlayed {get;private set;}
        public bool StrikePlayed {get;private set;}
        SpikeCourse course;
        AudioSource warning,impact;
        bool paused;

        public static SharedLightningAudio ForCourse(SpikeCourse owner)
        {
            var audio=owner.GetComponent<SharedLightningAudio>();
            if(audio==null)audio=owner.gameObject.AddComponent<SharedLightningAudio>();
            audio.Initialize(owner);return audio;
        }
        void Initialize(SpikeCourse owner)
        {
            if(course!=null)return; // Additional lanes reuse the existing voices/state.
            course=owner;warning=Voice();impact=Voice();
            course.round.Preparing+=ResetRound;course.round.Cancelled+=ResetRound;
            course.Completed+=RoundCompleted;
        }
        AudioSource Voice()
        {
            var source=gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;source.spatialBlend=0;source.loop=false;return source;
        }
        public bool PlayWarning(AudioClip clip)
        {
            if(WarningPlayed||StrikePlayed||clip==null)return false;
            WarningPlayed=true;warning.clip=clip;warning.volume=.7f;warning.Play();return true;
        }
        public bool PlayStrike(AudioClip clap,AudioClip wood)
        {
            if(StrikePlayed)return false;
            StrikePlayed=true;warning.Stop();
            if(clap!=null)impact.PlayOneShot(clap,.95f);
            // The single trimmed wood crash starts with the bolt, never on a fall.
            if(wood!=null)impact.PlayOneShot(wood,.85f);
            return true;
        }
        void Update()
        {
            // Pause with gameplay, but let a completed strike's decay finish after
            // a death. Only round cancellation/replay clears the shared playback.
            bool shouldPause=course.round.HasStarted&&!course.IsGameOver&&!course.round.masterBeat.IsPlaying;
            if(shouldPause==paused)return;paused=shouldPause;
            if(paused){warning.Pause();impact.Pause();}else{warning.UnPause();impact.UnPause();}
        }
        void RoundCompleted(RecruitController winner)
        {if(!StrikePlayed)warning.Stop();}
        void ResetRound()
        {warning.Stop();impact.Stop();WarningPlayed=StrikePlayed=paused=false;}
        void OnDestroy()
        {
            if(course!=null)
            {course.round.Preparing-=ResetRound;course.round.Cancelled-=ResetRound;course.Completed-=RoundCompleted;}
        }
    }
}

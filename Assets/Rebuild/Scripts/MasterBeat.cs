using System;
using UnityEngine;
using UnityEngine.Events;

namespace LeftRight.Rebuild
{
    /// <summary>The course's musical clock. Beat zero is the measured opening downbeat.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class MasterBeat : MonoBehaviour
    {
        public OutpostAudio audioOutput;
        public UnityEvent onBeat = new UnityEvent();

        public bool IsPlaying => isActiveAndEnabled && audioOutput != null && audioOutput.MusicHasStarted;
        public bool IsCountingIn => isActiveAndEnabled && audioOutput != null && audioOutput.music.isPlaying &&
            audioOutput.MusicStartDspTime > AudioSettings.dspTime;
        // Negative beats animate the silent lead-in and meet the music at beat zero.
        // Beat notifications and gameplay penalties still begin with audible music.
        public double AnimationBeatPosition => IsCountingIn
            ? (AudioSettings.dspTime - audioOutput.MusicStartDspTime) * BeatsPerMinute / 60.0
            : BeatPosition;
        public double BeatsPerMinute { get; private set; }
        public double SecondsPerBeat => BeatsPerMinute > 0 ? 60.0 / BeatsPerMinute : 0;
        public double BeatPosition { get; private set; }
        public long BeatIndex => (long)Math.Floor(BeatPosition);
        public double BeatPhase => BeatPosition - Math.Floor(BeatPosition);
        /// <summary>One notification for the current beat; seeks/loops never replay missed beats.
        /// Delivered on the first rendered frame observing the beat, not on the audio thread.</summary>
        public event Action<long> Beat;

        AudioClip previousClip;
        int playbackVersion = -1;
        long lastBeat;

        void Update()
        {
            if (audioOutput == null || audioOutput.music == null) return;
            var music = audioOutput.music;
            bool changed = previousClip != music.clip || playbackVersion != audioOutput.PlaybackVersion;
            if (changed)
            {
                previousClip = music.clip;
                playbackVersion = audioOutput.PlaybackVersion;
                BeatPosition = 0;
                lastBeat = -1;
            }
            if (music.clip == null) return;
            var timing = audioOutput.palette;
            bool round = music.clip == timing.roundMusic;
            BeatsPerMinute = round ? timing.roundBeatsPerMinute : timing.titleBeatsPerMinute;
            if (!IsPlaying) return; // Scheduled lead-in and paused audio cannot advance the clock.
            double firstBeat = round ? timing.roundFirstBeatSeconds : timing.titleFirstBeatSeconds;
            double seconds = (double)music.timeSamples / music.clip.frequency;
            BeatPosition = (seconds - firstBeat) * BeatsPerMinute / 60.0;
            long current = BeatIndex;
            if (current == lastBeat) return;
            lastBeat = current;
            if (current < 0) return;
            Beat?.Invoke(current);
            onBeat.Invoke();
        }

        public void ResetClock()
        {
            previousClip = null;
            playbackVersion = -1;
            BeatPosition = 0;
            lastBeat = -1;
        }

        void OnDisable() => ResetClock();
    }
}

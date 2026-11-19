using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class RebuildRoundStart : MonoBehaviour
    {
        public OutpostAudio audioOutput;
        public OfficerDirections officer;
        public OfficerMarch march;
        public MasterBeat masterBeat;
        public float leadInSeconds = 3f;
        public bool playOnStart = true;
        public StartSceneDirector startScenes;
        public float IntroSeconds {get;private set;}
        public bool HasStarted { get; private set; }
        public bool IsCountingDown { get; private set; }
        public double ScheduledStartDspTime { get; private set; }
        public event Action Preparing;
        public event Action Cancelled;
        public Func<bool> NetworkStartRequest;
        bool remoteOfficer;

        void Start()
        {
            if (playOnStart) BeginRound();
        }

        public void BeginRound()
        {
            if (NetworkStartRequest != null && NetworkStartRequest()) return;
            float duration=leadInSeconds<1?Mathf.Max(0,leadInSeconds):Mathf.Clamp(leadInSeconds,3,5);
            BeginScheduled(AudioSettings.dspTime + duration, false,-1,duration);
        }

        public void BeginScheduled(double dspTime, bool remote,int introIndex=-1,float introSeconds=-1)
        {
            float duration=introSeconds>=0?introSeconds:(float)Math.Max(0,dspTime-AudioSettings.dspTime);
            CancelStart();
            remoteOfficer = remote;
            ScheduledStartDspTime = dspTime;
            audioOutput.ScheduleTitleOnFirstBeat(ScheduledStartDspTime);
            IsCountingDown = true;
            Preparing?.Invoke();
            IntroSeconds=Mathf.Clamp(duration,0,5);
            startScenes?.Play(introIndex>=0?introIndex:remote?-1:startScenes.ChooseIndex(),dspTime,IntroSeconds);
        }

        public void CancelStart()
        {
            IsCountingDown = false;
            HasStarted = false;
            audioOutput.StopPlayback();
            if (masterBeat != null) masterBeat.ResetClock();
            officer.ResetDirections();
            march.ResetMarch();
            Cancelled?.Invoke();
        }

        void Update()
        {
            if (!IsCountingDown || AudioSettings.dspTime < ScheduledStartDspTime) return;
            startScenes?.Finish(); // Restore the instructor before the first cue/beat.
            IsCountingDown = false;
            HasStarted = true;
            if (!remoteOfficer) officer.ActivateDirections(firstCueImmediately: true);
        }

        void OnDisable()
        {
            if (audioOutput != null && officer != null && march != null) CancelStart();
        }
    }
}

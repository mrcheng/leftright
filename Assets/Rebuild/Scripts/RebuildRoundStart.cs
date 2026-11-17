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
            BeginScheduled(AudioSettings.dspTime + Mathf.Max(0, leadInSeconds), false);
        }

        public void BeginScheduled(double dspTime, bool remote)
        {
            CancelStart();
            remoteOfficer = remote;
            ScheduledStartDspTime = dspTime;
            audioOutput.ScheduleTitleOnFirstBeat(ScheduledStartDspTime);
            IsCountingDown = true;
            Preparing?.Invoke();
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

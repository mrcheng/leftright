using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class RebuildRoundStart : MonoBehaviour
    {
        public OutpostAudio audioOutput;
        public OfficerDirections officer;
        public OfficerMarch march;
        public float leadInSeconds = 3f;
        public bool playOnStart = true;
        public bool HasStarted { get; private set; }
        public bool IsCountingDown { get; private set; }
        public double ScheduledStartDspTime { get; private set; }

        void Start()
        {
            if (playOnStart) BeginRound();
        }

        public void BeginRound()
        {
            CancelStart();
            ScheduledStartDspTime = AudioSettings.dspTime + Mathf.Max(0, leadInSeconds);
            audioOutput.ScheduleTitleOnFirstBeat(ScheduledStartDspTime);
            IsCountingDown = true;
        }

        public void CancelStart()
        {
            IsCountingDown = false;
            HasStarted = false;
            audioOutput.StopPlayback();
            officer.ResetDirections();
            march.ResetMarch();
        }

        void Update()
        {
            if (!IsCountingDown || AudioSettings.dspTime < ScheduledStartDspTime) return;
            IsCountingDown = false;
            HasStarted = true;
            officer.ActivateDirections(firstCueImmediately: true);
        }

        void OnDisable()
        {
            if (audioOutput != null && officer != null && march != null) CancelStart();
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace LeftRight.Rebuild
{
    [DefaultExecutionOrder(100)]
    public sealed class SpikeCourse : MonoBehaviour
    {
        public RebuildRoundStart round;
        public RecruitController[] players;
        public LaneSpikeTrap[] traps;
        public float reactionBeats = .5f;
        public bool IsGameOver { get; private set; }
        public int AliveCount { get; private set; }
        long pendingBeat = -1;
        double cueBeat;
        double animationTime;

        void OnEnable()
        {
            round.Preparing += Prepare;
            round.Cancelled += ResetCourse;
            round.masterBeat.Beat += OnBeat;
            round.officer.DirectionChanged += OnDirection;
        }

        void OnDisable()
        {
            round.Preparing -= Prepare;
            round.Cancelled -= ResetCourse;
            round.masterBeat.Beat -= OnBeat;
            round.officer.DirectionChanged -= OnDirection;
        }

        void Prepare()
        {
            animationTime = Time.realtimeSinceStartupAsDouble;
            foreach (var trap in traps) trap.Raise();
        }

        void ResetCourse()
        {
            pendingBeat = -1;
            IsGameOver = false;
            AliveCount = players.Length;
            foreach (var player in players) player.ResetPlayer();
            foreach (var trap in traps) trap.ResetTrap();
        }

        void OnBeat(long beat) { pendingBeat = beat; }
        void OnDirection(bool right) { cueBeat = round.masterBeat.BeatPosition; }

        void Update()
        {
            if (IsGameOver && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                round.BeginRound();
        }

        void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float elapsed = (float)(now - animationTime);
            animationTime = now;
            if (IsGameOver)
            {
                // Finish the visible crush after the last recruit is eliminated.
                foreach (var trap in traps) trap.Animate(elapsed);
                foreach (var player in players) player.FollowPinnedBank();
                return;
            }
            bool playing = round.HasStarted && round.masterBeat.IsPlaying;
            if (round.IsCountingDown || playing)
                foreach (var trap in traps) trap.Animate(elapsed);
            foreach (var player in players) player.FollowPinnedBank();
            if (!playing) { pendingBeat = -1; return; }
            if (pendingBeat >= 0)
            {
                // Evaluate after this frame's input and officer cue, giving a new cue half a beat to react.
                if (pendingBeat > 0 && round.masterBeat.BeatPosition - cueBeat >= reactionBeats)
                    for (int i = 0; i < players.Length; i++)
                        if (!players[i].IsEliminated && (!players[i].IsMoving || players[i].DirectionRight != round.officer.DirectionRight))
                            traps[i].MissBeat();
                pendingBeat = -1;
            }
            for (int i = 0; i < players.Length; i++)
                if (!players[i].IsEliminated)
                {
                    traps[i].ResolveContact(players[i]);
                    if (players[i].IsEliminated) AliveCount--;
                }
            if (AliveCount > 0) return;
            IsGameOver = true;
            round.officer.StopDirections();
            round.audioOutput.StopPlayback();
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            GUI.Label(new Rect(0, Screen.height - 34, Screen.width, 28),
                "Move: A / D", style);
            if (IsGameOver)
            {
                style.fontSize = 30;
                GUI.Label(new Rect(0, Screen.height * .4f, Screen.width, 100), "Game over\nEnter to restart", style);
            }
        }
    }
}

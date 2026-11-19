using System.Collections.Generic;
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
        public RecruitController Winner { get; private set; }
        public event System.Action<RecruitController> Completed;
        long pendingBeat = -1;
        double cueBeat;
        double animationTime;
        readonly System.Random winnerRandom = new System.Random();
        readonly List<RecruitController> latestDeaths = new List<RecruitController>();
        readonly List<RecruitController> living = new List<RecruitController>();
        readonly List<RecruitController> safe = new List<RecruitController>();
        int lastDeathFrame = -1;
        public bool NetworkReplica { get; set; }
        public void ApplyNetworkResult(int winnerIndex)
        {
            if (IsGameOver) return;
            IsGameOver = true; Winner = winnerIndex >= 0 ? players[winnerIndex] : null;
            AliveCount = Winner != null ? 1 : 0;
            round.officer.StopDirections(); round.audioOutput.StopPlayback();
            if (Winner != null) Winner.StopForVictory();
        }

        void OnEnable()
        {
            round.Preparing += Prepare;
            round.Cancelled += ResetCourse;
            round.masterBeat.Beat += OnBeat;
            round.officer.DirectionChanged += OnDirection;
            foreach (var player in players) player.Eliminated += OnEliminated;
        }

        void OnDisable()
        {
            round.Preparing -= Prepare;
            round.Cancelled -= ResetCourse;
            round.masterBeat.Beat -= OnBeat;
            round.officer.DirectionChanged -= OnDirection;
            foreach (var player in players) player.Eliminated -= OnEliminated;
        }

        void Prepare()
        {
            animationTime = Time.realtimeSinceStartupAsDouble;
        }

        void ResetCourse()
        {
            pendingBeat = -1;
            IsGameOver = false;
            Winner = null;
            latestDeaths.Clear(); lastDeathFrame = -1;
            AliveCount = players.Length;
            foreach (var player in players) player.ResetPlayer();
            foreach (var trap in traps) trap.ResetTrap();
        }

        void OnBeat(long beat) { pendingBeat = beat; }
        public void SetRoster(RecruitController[] recruits, LaneSpikeTrap[] lanes)
        {
            // Change the actual simulation roster, not merely renderer visibility.
            // Cancel/reset must happen first so old lane pickups and death effects clear.
            foreach (var player in players) player.Eliminated -= OnEliminated;
            players = recruits; traps = lanes;
            foreach (var player in players) player.Eliminated += OnEliminated;
            AliveCount = players.Length;
        }
        void OnDirection(bool right) { cueBeat = round.masterBeat.BeatPosition; }

        void OnEliminated(RecruitController player)
        {
            // Keep only the final death batch. Earlier casualties never enter a tie draw.
            if (lastDeathFrame != Time.frameCount) { latestDeaths.Clear(); lastDeathFrame = Time.frameCount; }
            if (!latestDeaths.Contains(player)) latestDeaths.Add(player);
        }

        void Update()
        {
            if (IsGameOver && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame &&
                !(GetComponent<StartMenu>()?.Results?.Visible??false))
                round.BeginRound();
        }

        void LateUpdate()
        {
            if (NetworkReplica) return; // Remote snapshots, never a second match simulation.
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
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i].IsEliminated) continue;
                    // Recovery gets its own outward beat instead of fighting an inward penalty.
                    if (traps[i].ProcessRecoveryBeat(pendingBeat)) continue;
                    // A shock blocks input for two musical beats. Those forced
                    // idle beats cannot also count as disobeying the instructor.
                    if (players[i].IsShocked) continue;
                    if (pendingBeat > 0 && round.masterBeat.BeatPosition - cueBeat >= reactionBeats &&
                        (!players[i].IsMoving || players[i].DirectionRight != round.officer.DirectionRight))
                            traps[i].MissBeat();
                }
                pendingBeat = -1;
            }
            living.Clear(); safe.Clear();
            for (int i = 0; i < players.Length; i++)
                if (!players[i].IsEliminated)
                {
                    living.Add(players[i]);
                    if (!traps[i].WouldEliminate(players[i])) safe.Add(players[i]);
                }
            RecruitController reservedWinner = null;
            if (players.Length > 1 && living.Count > 0 && safe.Count <= 1)
            {
                // Decide the whole contact batch BEFORE applying deaths. If every final
                // contender would die together, reserve one uniformly at random instead
                // of allowing lane iteration order to decide the match or killing everyone.
                reservedWinner = safe.Count == 1 ? safe[0] : living[winnerRandom.Next(living.Count)];
            }
            for (int i = 0; i < players.Length; i++)
                if (!players[i].IsEliminated)
                {
                    if (players[i] != reservedWinner) traps[i].ResolveContact(players[i]);
                }
            AliveCount = 0;
            foreach (var player in players) if (!player.IsEliminated) AliveCount++;
            // Single-player testing retains the normal lose condition.
            // A single-recruit test round must still allow that recruit to play normally.
            if (AliveCount > 1 || (AliveCount == 1 && players.Length == 1)) return;
            Winner = null;
            foreach (var player in players) if (!player.IsEliminated) Winner = player;
            // External/network death batches may arrive already applied. Their last
            // simultaneous batch uses the same random winner rule, never an older corpse.
            if (Winner == null && players.Length > 1 && latestDeaths.Count > 0)
                Winner = latestDeaths[winnerRandom.Next(latestDeaths.Count)];
            IsGameOver = true;
            round.officer.StopDirections();
            round.audioOutput.StopPlayback();
            if (Winner != null)
            {
                Winner.StopForVictory();
                AliveCount = 1;
                for (int i = 0; i < players.Length; i++)
                    if (players[i] == Winner) traps[i].RetractForVictory();
            }
            Completed?.Invoke(Winner);
        }

    }
}

using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Local practice opponent, reading the same public direction cue a human
    /// sees. No health cheats, teleporting, private future cues or gameplay RNG calls.</summary>
    [DefaultExecutionOrder(-20)]
    public sealed class RecruitComputer : MonoBehaviour
    {
        public RecruitController player;
        public float reactionMinSeconds = .18f;
        public float reactionMaxSeconds = .65f;
        [Range(0, 1)] public float mistakeChance = .22f;
        readonly System.Random random = new System.Random();
        bool lastDirection, lastRunning, mistaken;
        float reactAt, mistakeEndsAt;

        void OnEnable() { player.ComputerInput = 0; lastRunning = false; }
        void Update()
        {
            if (!player.computerControlled) { player.ComputerInput = 0; return; }
            var round = player.round;
            bool running = round.HasStarted && round.masterBeat.IsPlaying && !player.course.IsGameOver;
            if (!running || player.IsPinned || player.IsEliminated)
            { player.ComputerInput = 0; lastRunning = false; return; }
            bool right = round.officer.DirectionRight;
            if (!lastRunning || right != lastDirection)
            {
                // Each cue gets a fresh human-scale reaction delay and possible mistake.
                // Mistakes last several musical beats, letting the spike course punish bots
                // under exactly the same rules. They cannot become perfect immortal rivals.
                reactAt = Time.unscaledTime + Mathf.Lerp(reactionMinSeconds, reactionMaxSeconds, (float)random.NextDouble());
                mistaken = random.NextDouble() < mistakeChance;
                mistakeEndsAt = reactAt + (float)round.masterBeat.SecondsPerBeat * random.Next(2, 5);
                lastDirection = right; lastRunning = true;
            }
            if (Time.unscaledTime < reactAt) return;
            float input = right ? 1 : -1;
            if (mistaken && Time.unscaledTime < mistakeEndsAt) input = -input;
            // The input still goes through normal walking, contact and beat penalties.
            player.ComputerInput = input;
        }
        void OnDisable() { if (player != null) player.ComputerInput = 0; }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    [DefaultExecutionOrder(200)]
    public sealed class VictoryDirector : MonoBehaviour
    {
        public SpikeCourse course;
        public GameViewSettings view;
        public Camera gameCamera;
        [Min(0), Tooltip("Seconds after the last opponent dies before the victory sequence starts.")]
        public float victoryDelaySeconds = 1f;
        [Tooltip("Each distinct available asset gets one equal chance: 1/N. Add new sequence assets here.")]
        public VictorySequence[] sequences;
        public RecruitController Winner { get; private set; }
        public VictorySequence SelectedSequence { get; private set; }
        public bool IsPending { get; private set; }
        public bool IsPlaying => playback != null;
        readonly System.Random random = new System.Random();
        VictoryPlayback playback;
        float beginsAt, startedAt;

        void OnEnable()
        {
            course.Completed += OnCompleted;
            course.round.Cancelled += ResetVictory;
        }

        void OnCompleted(RecruitController winner)
        {
            ResetVictory();
            // A draw/all-dead round has no winner and must never play a celebration.
            if (winner == null) return;
            Winner = winner;
            beginsAt = Time.unscaledTime + Mathf.Max(0, victoryDelaySeconds);
            IsPending = true;
        }

        public VictorySequence[] AvailableSequences()
        {
            var pool = new List<VictorySequence>();
            if (sequences != null)
                foreach (var sequence in sequences)
                    if (sequence != null && sequence.available && !pool.Contains(sequence)) pool.Add(sequence);
            return pool.ToArray();
        }

        void LateUpdate()
        {
            if (IsPending && Time.unscaledTime >= beginsAt)
            {
                IsPending = false;
                var pool = AvailableSequences();
                // Uniform draw with replacement: repeats are allowed and every entry
                // stays at 1/N probability, independent of gameplay/officer randomness.
                if (pool.Length == 0) return;
                SelectedSequence = pool[random.Next(pool.Length)];
                playback = SelectedSequence.CreatePlayback(Winner, gameCamera);
                startedAt = Time.unscaledTime;
                if (view != null) view.CinematicActive = true;
            }
            playback?.Tick(Time.unscaledTime - startedAt);
        }

        public void ResetVictory()
        {
            playback?.Stop(); playback = null;
            if (view != null) view.CinematicActive = false;
            Winner = null; SelectedSequence = null; IsPending = false;
        }

        void OnDisable()
        {
            course.Completed -= OnCompleted;
            course.round.Cancelled -= ResetVictory;
            ResetVictory();
        }
    }
}

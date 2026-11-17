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
        [Min(.1f)] public float victoryDurationSeconds = 5f;
        [Min(.1f)] public float zoomOutSeconds = 1.5f;
        public AudioSource cheerSource;
        public AudioClip cheerClip;
        [Range(0, 1)] public float cheerVolume = .9f;
        [Tooltip("Each distinct available asset gets one equal chance: 1/N. Add new sequence assets here.")]
        public VictorySequence[] sequences;
        public RecruitController Winner { get; private set; }
        public VictorySequence SelectedSequence { get; private set; }
        public bool IsPending { get; private set; }
        public bool IsPlaying => playback != null && !IsReturning;
        public bool IsReturning { get; private set; }
        public bool ShowRestartPrompt { get; private set; }
        readonly System.Random random = new System.Random();
        VictoryPlayback playback;
        float beginsAt, startedAt;
        float returnStartedAt, returnFov, returnTargetFov;
        Vector3 returnPosition, returnTargetPosition;
        Quaternion returnRotation, returnTargetRotation;

        void OnEnable()
        {
            course.Completed += OnCompleted;
            course.round.Cancelled += ResetVictory;
        }

        void OnCompleted(RecruitController winner)
        {
            ResetVictory();
            // Single-player losses have no winner; multiplayer ties are resolved by the course.
            if (winner == null) return;
            Winner = winner;
            // Choose immediately when the result is decided; only playback waits for
            // the configured delay. No delayed selection can race with another death.
            var pool = AvailableSequences();
            if (pool.Length > 0) SelectedSequence = pool[random.Next(pool.Length)];
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
                // Uniform draw with replacement: repeats are allowed and every entry
                // stays at 1/N probability, independent of gameplay/officer randomness.
                if (SelectedSequence == null) { ShowRestartPrompt = true; return; }
                playback = SelectedSequence.CreatePlayback(Winner, gameCamera);
                startedAt = Time.unscaledTime;
                if (view != null) view.CinematicActive = true;
                if (SelectedSequence.PlayOpeningCheer && cheerSource != null && cheerClip != null)
                {
                    cheerSource.clip = cheerClip;
                    cheerSource.volume = cheerVolume * Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel", 1));
                    cheerSource.Play();
                }
            }
            if (IsPlaying)
            {
                float elapsed = Time.unscaledTime - startedAt;
                playback.Tick(Mathf.Min(elapsed, victoryDurationSeconds));
                if (elapsed >= victoryDurationSeconds) BeginZoomOut();
            }
            if (IsReturning) UpdateZoomOut();
        }

        void BeginZoomOut()
        {
            IsReturning = true; returnStartedAt = Time.unscaledTime;
            if (cheerSource != null) cheerSource.Stop();
            returnPosition = gameCamera.transform.position; returnRotation = gameCamera.transform.rotation;
            returnFov = gameCamera.fieldOfView;
            returnTargetPosition = view != null ? view.CoursePosition : returnPosition;
            returnTargetRotation = view != null ? view.CourseRotation : returnRotation;
            returnTargetFov = view != null ? view.CourseFieldOfView : returnFov;
        }

        void UpdateZoomOut()
        {
            float progress = Mathf.Clamp01((Time.unscaledTime - returnStartedAt) / Mathf.Max(.1f, zoomOutSeconds));
            float ease = Mathf.SmoothStep(0, 1, progress);
            if (progress >= 1)
            {
                // Release sequence-owned poses/resources before assigning the final wide
                // camera pose, since Stop() may restore its original camera snapshot.
                playback?.Stop(); playback = null; IsReturning = false;
                ShowRestartPrompt = true;
                if (view != null) { view.RoundSummaryActive = true; view.CinematicActive = false; }
            }
            gameCamera.transform.SetPositionAndRotation(Vector3.Lerp(returnPosition, returnTargetPosition, ease),
                Quaternion.Slerp(returnRotation, returnTargetRotation, ease));
            gameCamera.fieldOfView = Mathf.Lerp(returnFov, returnTargetFov, ease);
        }

        public void StartAgain() => course.round.BeginRound();

        void OnGUI()
        {
            if (!ShowRestartPrompt) return;
            var panel = new Rect((Screen.width - 250) * .5f, Screen.height - 135, 250, 105);
            GUI.Box(panel, "Play again?");
            if (GUI.Button(new Rect(panel.x + 20, panel.y + 38, 210, 46), "Start again")) StartAgain();
        }

        public void ResetVictory()
        {
            playback?.Stop(); playback = null;
            if (cheerSource != null) cheerSource.Stop();
            IsReturning = false; ShowRestartPrompt = false;
            if (view != null) { view.CinematicActive = false; view.RoundSummaryActive = false; }
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

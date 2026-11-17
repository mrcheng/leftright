using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Gives every lane the same health opportunities; randomness chooses placement only.</summary>
    public sealed class HealthCrateSpawner : MonoBehaviour
    {
        public SpikeCourse course;
        public HealthCratePickup cratePrefab;
        [Tooltip("First opportunity after the opening music beat. No pickups during the lead-in.")]
        public int firstSpawnBeat = 8;
        [Tooltip("Each lane gets an opportunity this many beats apart, even when a prior opportunity was skipped.")]
        public int intervalBeats = 16;
        public int lifetimeBeats = 12;
        public int restoredBeats = 3;
        [Tooltip("Only offer health when the banks have advanced by at least this many missed beats.")]
        public int minimumLostBeats = 2;
        public float minimumSpawnDistance = 1.5f;
        public float maximumSpawnDistance = 2f;
        [Tooltip("Extra room between a crate's center and the current spike tips.")]
        public float spikeMargin = .8f;
        public bool automaticSpawning = true;
        readonly List<HealthCratePickup> active = new List<HealthCratePickup>();
        // This RNG must stay separate from Unity.Random: pickups must not alter instructor cue timing.
        readonly System.Random random = new System.Random();
        long[] nextOpportunity;

        void OnEnable()
        {
            course.round.Cancelled += Clear;
            course.round.masterBeat.Beat += OnBeat;
            ResetSchedule();
        }

        void OnDisable()
        {
            course.round.Cancelled -= Clear;
            course.round.masterBeat.Beat -= OnBeat;
            Clear();
        }

        void Clear()
        {
            foreach (var crate in active) if (crate != null) Destroy(crate.gameObject);
            foreach (var burst in GetComponentsInChildren<HealthCrateBurst>()) Destroy(burst.gameObject);
            active.Clear(); ResetSchedule();
        }

        void ResetSchedule()
        {
            nextOpportunity = new long[course.players.Length];
            for (int lane = 0; lane < nextOpportunity.Length; lane++)
                nextOpportunity[lane] = Mathf.Max(0, firstSpawnBeat);
        }

        void OnBeat(long beat)
        {
            if (!automaticSpawning || !course.round.HasStarted || course.IsGameOver) return;
            if (nextOpportunity == null || nextOpportunity.Length != course.players.Length) ResetSchedule();
            for (int lane = 0; lane < course.players.Length; lane++)
            {
                if (beat < nextOpportunity[lane]) continue;
                // Consume the slot even if this lane needs no health or still has a crate.
                // Collection/expiry never resets the timer, so fast collectors cannot farm crates.
                // Keep the original beat grid after a seek or dropped frame; never replay old slots.
                long first = Mathf.Max(0, firstSpawnBeat), interval = Mathf.Max(1, intervalBeats);
                nextOpportunity[lane] = first + ((beat - first) / interval + 1) * interval;
                var player = course.players[lane]; var trap = course.traps[lane];
                if (player.IsPinned || player.IsEliminated || HasCrate(lane)) continue;
                // Use remaining lost space, not historical MissedBeats: healing may have restored it already.
                if (trap.TravelAfterRecovery + .0001f < Mathf.Max(0, minimumLostBeats) * trap.inwardPerMiss) continue;
                if (TryChoosePosition(lane, out float x)) SpawnCrate(lane, x);
            }
        }

        bool TryChoosePosition(int lane, out float x)
        {
            var player = course.players[lane]; var trap = course.traps[lane];
            float center = player.transform.position.x;
            float safeLeft = trap.LeftTip + spikeMargin, safeRight = trap.RightTip - spikeMargin;
            float near = Mathf.Max(cratePrefab.collectionRadius + .1f, minimumSpawnDistance);
            float far = Mathf.Max(near, maximumSpawnDistance);
            // Intersect each side's 1.5–2m range with the safe lane. Clamping a random point
            // could put the crate under the player or against a spike; a missing range means skip it.
            float leftMin = Mathf.Max(safeLeft, center - far), leftMax = Mathf.Min(safeRight, center - near);
            float rightMin = Mathf.Max(safeLeft, center + near), rightMax = Mathf.Min(safeRight, center + far);
            bool leftValid = leftMin <= leftMax, rightValid = rightMin <= rightMax;
            if (!leftValid && !rightValid) { x = 0; return false; }
            bool left = leftValid && (!rightValid || random.Next(2) == 0);
            x = Mathf.Lerp(left ? leftMin : rightMin, left ? leftMax : rightMax, (float)random.NextDouble());
            return true;
        }

        bool HasCrate(int lane)
        {
            active.RemoveAll(crate => crate == null || crate.IsCollected);
            return active.Exists(crate => crate.LaneIndex == lane);
        }

        public HealthCratePickup SpawnCrate(int lane, float x)
        {
            if (lane < 0 || lane >= course.players.Length || HasCrate(lane)) return null;
            var player = course.players[lane]; var trap = course.traps[lane];
            if (!course.round.HasStarted || course.IsGameOver || player.IsPinned || player.IsEliminated ||
                x <= trap.LeftTip + .45f || x >= trap.RightTip - .45f) return null;
            var crate = Instantiate(cratePrefab, new Vector3(x, .65f, player.transform.position.z), Quaternion.identity, transform);
            crate.restoredBeats = restoredBeats;
            crate.Initialize(course, lane, course.round.masterBeat.BeatPosition + lifetimeBeats);
            active.Add(crate);
            return crate;
        }

        void Update()
        {
            if (course.IsGameOver && active.Count > 0) Clear();
        }
    }
}

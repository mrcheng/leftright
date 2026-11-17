using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class HealthCratePickup : MonoBehaviour
    {
        public Transform visual;
        public HealthCrateBurst burstPrefab;
        public int restoredBeats = 3;
        public float collectionRadius = .55f;
        public bool IsCollected { get; private set; }
        public int LaneIndex { get; private set; }
        public double ExpiresAtBeat { get; private set; }
        public event Action Collected;
        SpikeCourse course;
        Vector3 visualRest;

        public void Initialize(SpikeCourse owner, int lane, double expires)
        {
            course = owner; LaneIndex = lane; ExpiresAtBeat = expires;
            visualRest = visual.localPosition;
        }

        public bool TryCollect(RecruitController player)
        {
            if (IsCollected || course == null || !course.round.HasStarted || !course.round.masterBeat.IsPlaying ||
                course.IsGameOver || player != course.players[LaneIndex] || player.IsPinned || player.IsEliminated) return false;
            if (Mathf.Abs(player.transform.position.x - transform.position.x) > collectionRadius ||
                Mathf.Abs(player.transform.position.z - transform.position.z) > .5f) return false;
            // Claim before applying the effect: another collection in this frame cannot heal twice.
            IsCollected = true;
            course.traps[LaneIndex].RecoverBeats(restoredBeats);
            course.round.audioOutput.PlayHealthPickup();
            // Detached presentation can finish after the collectible is removed, without
            // keeping its trigger alive or allowing another heal during the burst.
            if (burstPrefab != null) Instantiate(burstPrefab, visual.position, transform.rotation, transform.parent);
            visual.gameObject.SetActive(false);
            Collected?.Invoke();
            Destroy(gameObject);
            return true;
        }

        void Update()
        {
            // Lifetime is musical time, so paused music freezes both expiry and presentation.
            if (course == null || IsCollected || !course.round.masterBeat.IsPlaying) return;
            if (course.IsGameOver || course.round.masterBeat.BeatPosition >= ExpiresAtBeat) { Destroy(gameObject); return; }
            float pulse = (float)Math.Sin(course.round.masterBeat.BeatPosition * Math.PI * 2);
            visual.localPosition = visualRest + Vector3.up * (.04f + .035f * pulse);
            var trap = course.traps[LaneIndex];
            // Remove crates swallowed by advancing spikes rather than leaving an unreachable reward.
            if (transform.position.x <= trap.LeftTip + .45f || transform.position.x >= trap.RightTip - .45f)
            { Destroy(gameObject); return; }
            TryCollect(course.players[LaneIndex]);
        }
    }
}

using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class LaneSpikeTrap : MonoBehaviour
    {
        public Transform leftBank, rightBank;
        public Transform[] leftPillars, rightPillars;
        public float inwardPerMiss = .45f;
        public float slideSeconds = .20f;
        public float spikeReach = .65f;
        public float finalGap = .25f;
        public float pillarThickness = .30f;
        public Material bloodMaterial;
        public float returnSeconds = 1.2f;
        public bool HasFinishedSqueeze { get; private set; }
        public bool HasReturned { get; private set; }
        public float MaximumTravel => Mathf.Max(0, (rightOpen.x - leftOpen.x - pillarThickness - finalGap) * .5f);
        public int MissedBeats { get; private set; }
        public float TargetTravel { get; private set; }
        public int PendingRecoverySteps { get; private set; }
        public float TravelAfterRecovery => Mathf.Max(0, TargetTravel - PendingRecoverySteps * inwardPerMiss);
        long recoveryStartsAtBeat;
        long lastRecoveryBeat = long.MinValue;
        MasterBeat masterBeat;
        public float LeftTip => leftBank.position.x + spikeReach;
        public float RightTip => rightBank.position.x - spikeReach;
        Vector3 leftOpen, rightOpen;
        float travel;
        RecruitController victim;
        BloodSplash blood;
        BloodSplash firstHitBlood;

        void Awake()
        {
            leftOpen = leftBank.localPosition;
            rightOpen = rightBank.localPosition;
            masterBeat = GetComponentInParent<MasterBeat>();
            ResetTrap();
        }

        public void ResetTrap()
        {
            travel = TargetTravel = 0;
            MissedBeats = 0;
            PendingRecoverySteps = 0;
            lastRecoveryBeat = long.MinValue;
            victim = null; HasFinishedSqueeze = HasReturned = false;
            if (blood != null) { blood.gameObject.SetActive(false); Destroy(blood.gameObject); blood = null; }
            if (firstHitBlood != null) { firstHitBlood.gameObject.SetActive(false); Destroy(firstHitBlood.gameObject); firstHitBlood = null; }
            ApplyPose();
        }

        public void MissBeat()
        {
            if (victim != null) return;
            MissedBeats++;
            TargetTravel = Mathf.Min(MaximumTravel, TargetTravel + inwardPerMiss);
        }

        public void FinishSqueeze() => TargetTravel = MaximumTravel;

        public float RecoverBeats(int beats)
        {
            if (victim != null) return 0;
            // Reserve recovery immediately but move only on subsequent musical beats.
            // Reserving prevents stacked health from recovering beyond the original width.
            float available = TravelAfterRecovery;
            int steps = Mathf.Min(Mathf.Max(0, beats), Mathf.CeilToInt(available / Mathf.Max(.001f, inwardPerMiss)));
            if (PendingRecoverySteps == 0)
                recoveryStartsAtBeat = masterBeat != null ? masterBeat.BeatIndex + 1 : 0;
            PendingRecoverySteps += steps;
            return Mathf.Min(available, steps * inwardPerMiss);
        }

        public bool ProcessRecoveryBeat(long beat)
        {
            if (PendingRecoverySteps == 0 || beat < recoveryStartsAtBeat || beat == lastRecoveryBeat || victim != null) return false;
            lastRecoveryBeat = beat;
            PendingRecoverySteps--;
            TargetTravel = Mathf.Max(0, TargetTravel - inwardPerMiss);
            // A crisp mechanical step on the beat, followed by a hold. Health does not
            // lerp across the entire reward or get cancelled by a simultaneous penalty.
            travel = TargetTravel;
            ApplyPose();
            return true;
        }

        public void Animate(float deltaTime)
        {
            float speed = HasFinishedSqueeze && TargetTravel == 0
                ? MaximumTravel / Mathf.Max(.01f, returnSeconds) : inwardPerMiss / Mathf.Max(.01f, slideSeconds);
            travel = Mathf.MoveTowards(travel, TargetTravel, speed * deltaTime);
            ApplyPose();
            if (victim != null && !HasFinishedSqueeze && Mathf.Abs(travel - MaximumTravel) < .001f)
            {
                // Finish the visible crush, then retract and slowly tip the body together.
                // This continues after game over so the final recruit gets the same sequence.
                victim.FollowPinnedBank();
                HasFinishedSqueeze = true;
                var position = new Vector3((leftBank.position.x + rightBank.position.x) * .5f,
                    transform.position.y + .012f, victim.transform.position.z);
                if (bloodMaterial != null) blood = BloodSplash.Spawn(position, bloodMaterial, transform);
                TargetTravel = 0;
                victim.ReleaseAfterSqueeze(transform.position.y);
            }
            if (HasFinishedSqueeze && !HasReturned && TargetTravel == 0 && travel == 0)
            {
                HasReturned = true;
            }
        }

        void ApplyPose()
        {
            leftBank.localPosition = leftOpen + Vector3.right * travel;
            rightBank.localPosition = rightOpen + Vector3.left * travel;
            for (int i = 0; i < leftPillars.Length; i++)
            {
                // Pillars are already at full height when the scene opens and after every reset.
                var left = leftPillars[i].localPosition; left.y = 0; leftPillars[i].localPosition = left;
                var right = rightPillars[i].localPosition; right.y = 0; rightPillars[i].localPosition = right;
            }
        }

        public bool Touches(RecruitController player) =>
            (player.transform.position.x - player.collisionRadius <= LeftTip ||
             player.transform.position.x + player.collisionRadius >= RightTip);

        public void ResolveContact(RecruitController player)
        {
            if (player.IsEliminated) return;
            if (!player.IsPinned)
            {
                bool left = player.transform.position.x - player.collisionRadius <= LeftTip;
                bool right = player.transform.position.x + player.collisionRadius >= RightTip;
                if (!left && !right) return;
                // Choose the nearest bank if both happen to arrive in the same frame.
                bool fromLeft = left && (!right || Mathf.Abs(player.transform.position.x - LeftTip) <= Mathf.Abs(player.transform.position.x - RightTip));
                player.PinTo(fromLeft ? leftBank : rightBank, fromLeft, spikeReach);
                if (bloodMaterial != null)
                    firstHitBlood = BloodSplash.Spawn(new Vector3(player.transform.position.x,
                        transform.position.y + .012f, player.transform.position.z), bloodMaterial, transform, .45f);
                return;
            }
            player.FollowPinnedBank();
            bool oppositeHit = player.PinnedFromLeft
                ? player.transform.position.x + player.collisionRadius >= RightTip
                : player.transform.position.x - player.collisionRadius <= LeftTip;
            if (oppositeHit)
            {
                player.Eliminate();
                player.SqueezeBetween(leftBank, rightBank, pillarThickness);
                victim = player;
                FinishSqueeze();
            }
        }
    }
}

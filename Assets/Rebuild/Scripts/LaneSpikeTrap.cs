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
        public float MaximumTravel => Mathf.Max(0, (rightOpen.x - leftOpen.x - pillarThickness - finalGap) * .5f);
        public int MissedBeats { get; private set; }
        public float TargetTravel { get; private set; }
        public float LeftTip => leftBank.position.x + spikeReach;
        public float RightTip => rightBank.position.x - spikeReach;
        Vector3 leftOpen, rightOpen;
        float travel;

        void Awake()
        {
            leftOpen = leftBank.localPosition;
            rightOpen = rightBank.localPosition;
            ResetTrap();
        }

        public void ResetTrap()
        {
            travel = TargetTravel = 0;
            MissedBeats = 0;
            ApplyPose();
        }

        public void MissBeat()
        {
            MissedBeats++;
            TargetTravel = Mathf.Min(MaximumTravel, TargetTravel + inwardPerMiss);
        }

        public void FinishSqueeze() => TargetTravel = MaximumTravel;

        public float RecoverBeats(int beats)
        {
            // Retreat the movement target; Animate handles the visible slide. Clamp at the
            // original width so repeated health pickups cannot push a bank beyond its lane.
            float previous = TargetTravel;
            TargetTravel = Mathf.Max(0, TargetTravel - Mathf.Max(0, beats) * inwardPerMiss);
            return previous - TargetTravel;
        }

        public void Animate(float deltaTime)
        {
            travel = Mathf.MoveTowards(travel, TargetTravel, inwardPerMiss / Mathf.Max(.01f, slideSeconds) * deltaTime);
            ApplyPose();
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
                FinishSqueeze();
            }
        }
    }
}

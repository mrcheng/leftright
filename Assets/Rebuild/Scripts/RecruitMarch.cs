using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    public enum RecruitRole { Rifle, Sniper, Mortar, Medic }

    /// <summary>In-place recruit drill using the course clock and officer's direction.</summary>
    public sealed class RecruitMarch : MonoBehaviour
    {
        public RecruitRole role;
        public bool female;
        public MasterBeat masterBeat;
        public OfficerDirections directions;
        public RecruitController player;
        public Transform leftHip, rightHip, leftKnee, rightKnee, leftArm, rightArm;
        [Range(0, 45)] public float legLiftDegrees = 30;
        [Range(0, 15)] public float kneeBendDegrees = 5;
        [Range(0, 90)] public float inPlaceLegLiftDegrees = 82;
        [Range(0, 90)] public float inPlaceKneeBendDegrees = 82;
        public float armSwingDegrees = 18;
        public float walkingBodyBob = .13f;
        public float inPlaceBodyBob = .045f;
        public double BeatPosition { get; private set; }
        Quaternion[] rest;
        Transform[] joints;
        Quaternion restFacing;
        float inPlaceBlend = 1;
        Transform visualRoot, torso, leftFoot, rightFoot;
        Vector3 visualRestPosition, torsoRestPosition;
        Quaternion leftFootRest, rightFootRest;
        float footRestHeight;

        void Awake()
        {
            joints = new[] { leftHip, rightHip, leftKnee, rightKnee, leftArm, rightArm };
            rest = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) rest[i] = joints[i].localRotation;
            restFacing = transform.localRotation;
            visualRoot = transform.Find("Model orientation");
            var bones = GetComponentsInChildren<Transform>();
            foreach (var bone in bones)
            {
                if (bone.name == "Abdomen") torso = bone;
                if (bone.name == "Foot.L") leftFoot = bone;
                if (bone.name == "Foot.R") rightFoot = bone;
            }
            visualRestPosition = visualRoot.localPosition;
            torsoRestPosition = torso.localPosition;
            leftFootRest = leftFoot.localRotation;
            rightFootRest = rightFoot.localRotation;
            footRestHeight = Mathf.Min(leftFoot.position.y, rightFoot.position.y) - transform.position.y;
        }

        void LateUpdate()
        {
            if(player!=null&&(player.IsJumping||player.LandingAmount>0))
            {
                AnimateJump();return;
            }
            if (masterBeat == null || (!masterBeat.IsPlaying && !masterBeat.IsCountingIn))
            {
                // Pause holds the drill pose; a cancelled/restarting round returns to attention.
                if (directions != null && !directions.IsRunning) ResetMarch();
                return;
            }
            // Silent lead-in permits player-controlled walking, but idle drill starts
            // only with audible music. Releasing input before that returns to attention.
            if (masterBeat.IsCountingIn && (player == null || !player.IsMoving))
            {
                ResetMarch();
                return;
            }
            BeatPosition = masterBeat.AnimationBeatPosition;
            bool inPlace = player == null || !player.IsMoving;
            inPlaceBlend = masterBeat.IsCountingIn ? 0 :
                Mathf.MoveTowards(inPlaceBlend, inPlace ? 1 : 0, Time.unscaledDeltaTime / .12f);
            float bend = Mathf.Lerp(kneeBendDegrees, inPlaceKneeBendDegrees, inPlaceBlend);
            float stride = (float)Math.Sin(BeatPosition * Math.PI);
            float left = Mathf.Max(0, stride), right = Mathf.Max(0, -stride);
            float leftAngle = Mathf.Lerp(stride * legLiftDegrees, left * inPlaceLegLiftDegrees, inPlaceBlend);
            float rightAngle = Mathf.Lerp(-stride * legLiftDegrees, right * inPlaceLegLiftDegrees, inPlaceBlend);
            // Walking pushes the supporting leg backward as the other steps forward.
            Rotate(0, leftAngle);
            Rotate(1, rightAngle);
            Rotate(2, -left * bend);
            Rotate(3, -right * bend);
            SetFoot(leftFoot, leftFootRest, -(leftAngle - left * bend));
            SetFoot(rightFoot, rightFootRest, -(rightAngle - right * bend));
            Rotate(4, -stride * armSwingDegrees);
            Rotate(5, stride * armSwingDegrees);
            bool rightFacing = player != null ? player.DirectionRight : directions.DirectionRight;
            float yaw = player != null ? (inPlace ? 0 : rightFacing ? -90 : 90)
                : directions.IsIdling ? 0 : rightFacing ? -90 : 90;
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                restFacing * Quaternion.Euler(0, yaw, 0), 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
            // Correct the model height, not gameplay position, to keep the support boot planted.
            visualRoot.localPosition = visualRestPosition;
            float supportHeight = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            visualRoot.position += Vector3.up * (transform.position.y + footRestHeight - supportHeight);
            // Head, shoulders, equipment and torso rise and settle on every master beat.
            float bob = Mathf.Lerp(walkingBodyBob, inPlaceBodyBob, inPlaceBlend) * stride * stride;
            torso.localPosition = torsoRestPosition + torso.parent.InverseTransformVector(Vector3.up * bob);
        }

        void SetFoot(Transform foot, Quaternion neutral, float angle)
        {
            var axis = foot.parent.InverseTransformDirection(transform.right);
            foot.localRotation = Quaternion.AngleAxis(angle, axis) * neutral;
        }

        void AnimateJump()
        {
            float tuck=player.IsJumping?Mathf.Sin(player.JumpPhase*Mathf.PI):0;
            float land=player.LandingAmount;
            float hip=35*tuck+18*land,knee=70*tuck+36*land;
            Rotate(0,hip);Rotate(1,hip);Rotate(2,-knee);Rotate(3,-knee);
            Rotate(4,-65*tuck);Rotate(5,-65*tuck);
            SetFoot(leftFoot,leftFootRest,knee-hip);SetFoot(rightFoot,rightFootRest,knee-hip);
            // Jump height belongs to the controller. Disable planted-foot correction
            // while airborne, otherwise it would visually straighten out the tuck.
            visualRoot.localPosition=visualRestPosition;
            visualRoot.position-=Vector3.up*(.08f*land);
            torso.localPosition=torsoRestPosition;
            if(player.IsMoving)
                transform.localRotation=Quaternion.Slerp(transform.localRotation,
                    restFacing*Quaternion.Euler(0,player.DirectionRight?-90:90,0),1-Mathf.Exp(-18*Time.unscaledDeltaTime));
        }

        void Rotate(int index, float angle)
        {
            var joint = joints[index];
            Vector3 axis = joint.parent.InverseTransformDirection(transform.right);
            joint.localRotation = Quaternion.AngleAxis(angle, axis) * rest[index];
        }

        public void ResetMarch()
        {
            if (rest == null) return;
            for (int i = 0; i < joints.Length; i++) joints[i].localRotation = rest[i];
            transform.localRotation = restFacing;
            BeatPosition = 0;
            inPlaceBlend = 1;
            if (visualRoot != null) visualRoot.localPosition = visualRestPosition;
            if (torso != null) torso.localPosition = torsoRestPosition;
            if (leftFoot != null) leftFoot.localRotation = leftFootRest;
            if (rightFoot != null) rightFoot.localRotation = rightFootRest;
        }

        void OnDisable() => ResetMarch();
    }
}

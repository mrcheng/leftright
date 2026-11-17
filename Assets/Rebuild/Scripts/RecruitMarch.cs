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
        public Transform leftHip, rightHip, leftKnee, rightKnee, leftArm, rightArm;
        [Range(0, 45)] public float legLiftDegrees = 30;
        [Range(0, 15)] public float kneeBendDegrees = 5;
        public float armSwingDegrees = 18;
        public double BeatPosition { get; private set; }
        Quaternion[] rest;
        Transform[] joints;
        Quaternion restFacing;

        void Awake()
        {
            joints = new[] { leftHip, rightHip, leftKnee, rightKnee, leftArm, rightArm };
            rest = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) rest[i] = joints[i].localRotation;
            restFacing = transform.localRotation;
        }

        void LateUpdate()
        {
            if (masterBeat == null || !masterBeat.IsPlaying)
            {
                // Pause holds the drill pose; a cancelled/restarting round returns to attention.
                if (directions != null && !directions.IsRunning) ResetMarch();
                return;
            }
            BeatPosition = masterBeat.BeatPosition;
            float stride = (float)Math.Sin(BeatPosition * Math.PI);
            float left = Mathf.Max(0, stride), right = Mathf.Max(0, -stride);
            Rotate(0, left * legLiftDegrees);
            Rotate(1, right * legLiftDegrees);
            Rotate(2, -left * kneeBendDegrees);
            Rotate(3, -right * kneeBendDegrees);
            Rotate(4, -stride * armSwingDegrees);
            Rotate(5, stride * armSwingDegrees);
            float yaw = directions.IsIdling ? 0 : directions.DirectionRight ? -90 : 90;
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                restFacing * Quaternion.Euler(0, yaw, 0), 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
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
        }

        void OnDisable() => ResetMarch();
    }
}

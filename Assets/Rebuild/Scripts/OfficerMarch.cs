using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class OfficerMarch : MonoBehaviour
    {
        public OfficerDirections directions;
        public OutpostAudio audioOutput;
        public MasterBeat masterBeat;
        public Transform body;
        public Transform leftHip;
        public Transform rightHip;
        public Transform leftKnee;
        public Transform rightKnee;
        public Transform leftArmSwing;
        public Transform rightArmSwing;
        public float kneeLiftDegrees = 82f;
        public float armSwingDegrees = 12f;
        public float bodyBounce = .015f;
        public bool playFootsteps = true;
        [Range(0, 1)] public float footstepVolume = .18f;

        public double BeatPosition { get; private set; }
        Vector3 bodyRest;
        Vector3 leftShoulderRest;
        Vector3 rightShoulderRest;
        AudioClip previousClip;
        long lastFootstepBeat;

        void Awake()
        {
            bodyRest = body.localPosition;
            leftShoulderRest = directions.leftShoulder.localPosition;
            rightShoulderRest = directions.rightShoulder.localPosition;
        }

        void LateUpdate()
        {
            var music = audioOutput.music;
            if (masterBeat == null || !masterBeat.IsPlaying) return;
            var timing = audioOutput.palette;
            BeatPosition = masterBeat.BeatPosition;
            long beat = (long)Math.Floor(BeatPosition);
            if (previousClip != music.clip)
            {
                previousClip = music.clip;
                lastFootstepBeat = audioOutput.MusicStartDspTime > 0 && beat == 0 ? -1 : beat;
            }
            if (beat != lastFootstepBeat)
            {
                var steps = timing.walking;
                if (playFootsteps && beat == lastFootstepBeat + 1 && steps.Length > 0)
                {
                    // Cycle the original boot sounds without changing direction RNG.
                    int index = (int)((beat % steps.Length + steps.Length) % steps.Length);
                    audioOutput.effects.PlayOneShot(steps[index], footstepVolume);
                }
                lastFootstepBeat = beat;
            }
            float stride = (float)Math.Sin(BeatPosition * Math.PI);
            float leftLift = Mathf.Max(0, stride) * kneeLiftDegrees;
            float rightLift = Mathf.Max(0, -stride) * kneeLiftDegrees;
            leftHip.localRotation = Quaternion.Euler(leftLift, 0, 0);
            rightHip.localRotation = Quaternion.Euler(rightLift, 0, 0);
            // Counter-rotate the shins so a raised thigh leaves its boot hanging flat.
            leftKnee.localRotation = Quaternion.Euler(-leftLift, 0, 0);
            rightKnee.localRotation = Quaternion.Euler(-rightLift, 0, 0);

            Vector3 bounce = Vector3.up * Mathf.Abs(stride) * bodyBounce;
            body.localPosition = bodyRest + bounce;
            directions.leftShoulder.localPosition = leftShoulderRest + bounce;
            directions.rightShoulder.localPosition = rightShoulderRest + bounce;
            bool pointing = !directions.IsIdling;
            leftArmSwing.localRotation = Quaternion.Euler(pointing && !directions.DirectionRight ? 0 : -stride * armSwingDegrees, 0, 0);
            rightArmSwing.localRotation = Quaternion.Euler(pointing && directions.DirectionRight ? 0 : stride * armSwingDegrees, 0, 0);
        }

        void OnDisable() => ResetMarch();

        public void ResetMarch()
        {
            previousClip = null;
            BeatPosition = 0;
            lastFootstepBeat = 0;
            if (leftHip != null) leftHip.localRotation = Quaternion.identity;
            if (rightHip != null) rightHip.localRotation = Quaternion.identity;
            if (leftKnee != null) leftKnee.localRotation = Quaternion.identity;
            if (rightKnee != null) rightKnee.localRotation = Quaternion.identity;
            if (leftArmSwing != null) leftArmSwing.localRotation = Quaternion.identity;
            if (rightArmSwing != null) rightArmSwing.localRotation = Quaternion.identity;
            if (body != null) body.localPosition = bodyRest;
            if (directions != null)
            {
                directions.leftShoulder.localPosition = leftShoulderRest;
                directions.rightShoulder.localPosition = rightShoulderRest;
            }
        }
    }
}

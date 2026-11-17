using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    public sealed class OfficerDirections : MonoBehaviour
    {
        public Transform leftShoulder;
        public Transform rightShoulder;
        public OutpostAudio audioOutput;
        public bool playOnStart;
        public float minTimeTurn = 2f;
        public float maxTimeTurn = 5f;
        public float swingDuration = .25f;

        public bool DirectionRight { get; private set; } = true;
        public bool IsIdling { get; private set; } = true;
        public float CurrentInterval { get; private set; }
        public event Action<bool> DirectionChanged;
        public event Action<bool> RunningChanged;
        public bool IsRunning => running;

        bool running;
        float flipTimer;
        float swingTimer;
        Quaternion leftStart, rightStart;
        Quaternion leftTarget = Quaternion.identity;
        Quaternion rightTarget = Quaternion.identity;

        void Start()
        {
            if (playOnStart) ActivateDirections();
        }

        public void ActivateDirections(bool firstCueImmediately = false)
        {
            running = true;
            ResetPose();
            RunningChanged?.Invoke(true);
            if (firstCueImmediately) IssueCue();
            else CurrentInterval = UnityEngine.Random.Range(minTimeTurn, maxTimeTurn);
        }

        public void ResetDirections()
        {
            running = false;
            ResetPose();
            RunningChanged?.Invoke(false);
        }

        void ResetPose()
        {
            IsIdling = true;
            DirectionRight = true;
            flipTimer = 0f;
            CurrentInterval = 0;
            leftShoulder.localRotation = rightShoulder.localRotation = Quaternion.identity;
            leftStart = rightStart = leftTarget = rightTarget = Quaternion.identity;
            swingTimer = 0f;
        }

        public void StopDirections()
        {
            running = false;
            RunningChanged?.Invoke(false);
        }

        void Update()
        {
            if (running)
            {
                flipTimer += Time.deltaTime;
                if (flipTimer > CurrentInterval)
                {
                    IssueCue();
                }
            }

            swingTimer += Time.deltaTime;
            float t = swingDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, swingTimer / swingDuration);
            leftShoulder.localRotation = Quaternion.Slerp(leftStart, leftTarget, t);
            rightShoulder.localRotation = Quaternion.Slerp(rightStart, rightTarget, t);
        }

        void IssueCue()
        {
            // The prototype alternates directions; only its wait is random.
            DirectionRight = !DirectionRight;
            IsIdling = false;
            flipTimer = 0f;
            CurrentInterval = UnityEngine.Random.Range(minTimeTurn, maxTimeTurn);
            leftStart = leftShoulder.localRotation;
            rightStart = rightShoulder.localRotation;
            leftTarget = DirectionRight ? Quaternion.identity : Quaternion.Euler(0, 0, -90);
            rightTarget = DirectionRight ? Quaternion.Euler(0, 0, 90) : Quaternion.identity;
            swingTimer = 0f;
            audioOutput.PlayDirection(DirectionRight);
            DirectionChanged?.Invoke(DirectionRight);
        }
    }
}

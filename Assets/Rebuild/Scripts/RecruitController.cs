using UnityEngine;
using UnityEngine.InputSystem;

namespace LeftRight.Rebuild
{
    public sealed class RecruitController : MonoBehaviour
    {
        public int playerIndex;
        [Tooltip("Only the locally owned recruit reads this client's keyboard/gamepad. Lane index is not input ownership.")]
        public bool isLocallyControlled;
        public RecruitMarch march;
        public RebuildRoundStart round;
        public SpikeCourse course;
        public RecruitAudio hitAudio;
        public float walkSpeed = 1.5f;
        public float collisionRadius = .25f;
        public bool IsEliminated { get; private set; }
        public bool IsPinned => pinnedBank != null;
        public bool PinnedFromLeft { get; private set; }
        public float penetrationDepth = .20f;
        public float Horizontal { get; private set; }
        public bool IsMoving => Mathf.Abs(Horizontal) > .2f && !IsEliminated && !IsPinned;
        public bool DirectionRight { get; private set; }
        public float? InputOverride { get; set; }
        Vector3 startingPosition;
        Quaternion startingRotation;
        float fallProgress;
        Vector3 deathPosition;
        Quaternion deathRotation;
        double movementTime;
        Transform pinnedBank;
        Vector3 pinnedOffset;
        Quaternion pinnedFacing;
        Transform visualRoot, squeezeLeft, squeezeRight;
        Vector3 visualRestScale;
        float visualWidth, squeezeThickness;

        void Awake()
        {
            startingPosition = transform.localPosition;
            startingRotation = transform.localRotation;
            movementTime = Time.realtimeSinceStartupAsDouble;
            visualRoot = transform.Find("Model orientation");
            visualRestScale = visualRoot.localScale;
        }

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float elapsed = (float)(now - movementTime);
            movementTime = now;
            bool canMove = round.IsCountingDown || (round.HasStarted && round.masterBeat.IsPlaying);
            if (IsEliminated || IsPinned || !canMove || course.IsGameOver)
            {
                Horizontal = 0;
                return;
            }
            Horizontal = Mathf.Clamp(InputOverride ?? ReadInput(), -1, 1);
            if (!IsMoving) { Horizontal = 0; return; }
            DirectionRight = Horizontal > 0;
            transform.localPosition += Vector3.right * Horizontal * walkSpeed * elapsed;
        }

        float ReadInput()
        {
            // Online opponents must never consume this client's hardware input.
            // InputOverride is a separate input path for simulation/remote input later.
            // Presentation currently puts the local green recruit in front (lane 1);
            // future character selection can change its model without changing these controls.
            if (!isLocallyControlled) return 0;
            var keyboard = Keyboard.current;
            float axis = keyboard == null ? 0 :
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            if (Gamepad.current != null)
            {
                var pad = Gamepad.current;
                float stick = pad.leftStick.x.ReadValue(), dpad = pad.dpad.x.ReadValue();
                float padAxis = Mathf.Abs(dpad) > Mathf.Abs(stick) ? dpad : stick;
                if (Mathf.Abs(padAxis) > Mathf.Abs(axis)) axis = padAxis;
            }
            return axis;
        }

        public void ResetPlayer()
        {
            if (hitAudio != null) hitAudio.ResetAudio();
            IsEliminated = false;
            pinnedBank = null;
            squeezeLeft = squeezeRight = null;
            visualRoot.localScale = visualRestScale;
            PinnedFromLeft = false;
            Horizontal = 0;
            DirectionRight = false;
            fallProgress = 0;
            movementTime = Time.realtimeSinceStartupAsDouble;
            transform.SetLocalPositionAndRotation(startingPosition, startingRotation);
            march.enabled = true;
            march.ResetMarch();
        }

        public void Eliminate()
        {
            if (IsEliminated) return;
            IsEliminated = true;
            if (IsPinned && hitAudio != null) hitAudio.PlaySqueezed();
            Horizontal = 0;
            if (!IsPinned)
            {
                march.ResetMarch();
                march.enabled = false;
            }
            else transform.localRotation = pinnedFacing;
            deathPosition = transform.localPosition;
            deathRotation = transform.localRotation;
        }

        public void PinTo(Transform bank, bool fromLeft, float spikeReach)
        {
            if (IsPinned || IsEliminated) return;
            Horizontal = 0;
            PinnedFromLeft = fromLeft;
            march.ResetMarch();
            march.enabled = false;
            // Hold an upright side-on pose so the spike visibly passes into the torso.
            pinnedFacing = startingRotation * Quaternion.Euler(0, fromLeft ? -90 : 90, 0);
            transform.localRotation = pinnedFacing;
            pinnedBank = bank;
            if (hitAudio != null) hitAudio.PlaySpiked();
            var position = transform.position;
            position.x = bank.position.x + (fromLeft ? 1 : -1) * (spikeReach - penetrationDepth);
            transform.position = position;
            pinnedOffset = bank.InverseTransformPoint(position);
            visualWidth = GetComponentInChildren<SkinnedMeshRenderer>().bounds.size.x;
        }

        public void SqueezeBetween(Transform left, Transform right, float thickness)
        {
            squeezeLeft = left; squeezeRight = right; squeezeThickness = thickness;
            FollowPinnedBank();
        }

        public void FollowPinnedBank()
        {
            if (IsPinned)
            {
                transform.position = pinnedBank.TransformPoint(pinnedOffset);
                transform.localRotation = pinnedFacing;
                if (squeezeLeft != null && squeezeRight != null)
                {
                    float gap = Mathf.Max(.01f, squeezeRight.position.x - squeezeLeft.position.x - squeezeThickness);
                    float compression = Mathf.Clamp(gap / Mathf.Max(.01f, visualWidth), .05f, 1);
                    // Sideways facing maps model depth onto the lane's horizontal axis.
                    visualRoot.localScale = new Vector3(visualRestScale.x, visualRestScale.y, visualRestScale.z * compression);
                    var position = transform.position;
                    position.x = (squeezeLeft.position.x + squeezeRight.position.x) * .5f;
                    transform.position = position;
                }
            }
        }

        void LateUpdate()
        {
            if (IsPinned) { FollowPinnedBank(); return; }
            if (!IsEliminated) return;
            fallProgress = Mathf.Min(1, fallProgress + Time.unscaledDeltaTime / .4f);
            float t = Mathf.SmoothStep(0, 1, fallProgress);
            transform.localRotation = deathRotation * Quaternion.Euler(0, 0, -85 * t);
            transform.localPosition = deathPosition + Vector3.up * (.2f * t);
        }
    }
}

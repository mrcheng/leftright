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
        public RecruitRagdoll ragdoll;
        public float walkSpeed = 1.5f;
        public float collisionRadius = .25f;
        [Min(.1f)] public float jumpHeight = 1f;
        [Min(.2f)] public float jumpDuration = 1.05f;
        public bool IsJumping { get; private set; }
        public float JumpPhase { get; private set; }
        public float JumpOffset => Mathf.Max(0,transform.localPosition.y-startingPosition.y);
        public bool WasBlasted { get; private set; }
        public float LandingAmount => Mathf.Clamp01((float)((landingUntil-Time.realtimeSinceStartupAsDouble)/.14));
        double jumpStarted, landingUntil;
        public bool IsEliminated { get; private set; }
        public event System.Action<RecruitController> Eliminated;
        public event System.Action<RecruitController> Spiked;
        public bool IsPinned => pinnedBank != null;
        public bool PinnedFromLeft { get; private set; }
        public float penetrationDepth = .20f;
        public float Horizontal { get; private set; }
        public bool IsMoving => Mathf.Abs(Horizontal) > .2f && !IsEliminated && !IsPinned;
        public bool DirectionRight { get; private set; }
        public float? InputOverride { get; set; }
        public bool computerControlled;
        public float ComputerInput { get; set; }
        public bool NetworkReplica { get; set; }
        public float ReadNetworkInput() => ReadInput();
        public void ApplyNetworkMovement(float x, float horizontal)
        {
            if (!IsEliminated && !IsPinned)
            { var position = transform.position; position.x = x; transform.position = position; }
            Horizontal = horizontal; if (Mathf.Abs(horizontal) > .2f) DirectionRight = horizontal > 0;
        }
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
        bool releasedAfterSqueeze;

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
            if (NetworkReplica) { UpdateJump(); return; }
            bool canMove = round.IsCountingDown || (round.HasStarted && round.masterBeat.IsPlaying);
            if (IsEliminated || IsPinned || !canMove || course.IsGameOver)
            {
                Horizontal = 0;
                CancelJump();
                return;
            }
            if(ReadJumpPressed())TryJump();
            UpdateJump();
            Horizontal = Mathf.Clamp(InputOverride ?? (computerControlled ? ComputerInput : ReadInput()), -1, 1);
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

        public bool ReadJumpPressed() => isLocallyControlled &&
            ((Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame)||
             (Gamepad.current!=null&&Gamepad.current.buttonSouth.wasPressedThisFrame));

        public bool TryJump()
        {
            // Jumping changes height and pose, while ordinary direction penalties and
            // spike contacts still apply. Holding Space never creates repeated jumps.
            if(IsJumping||IsPinned||IsEliminated||course.IsGameOver||
                !(round.IsCountingDown||(round.HasStarted&&round.masterBeat.IsPlaying)))return false;
            IsJumping=true;JumpPhase=0;jumpStarted=Time.realtimeSinceStartupAsDouble;landingUntil=0;
            return true;
        }
        void UpdateJump()
        {
            if(!IsJumping)return;
            JumpPhase=Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble-jumpStarted)/Mathf.Max(.2f,jumpDuration)));
            // A parabola gives a brisk takeoff, a slow apex and accelerating descent.
            var position=transform.localPosition;
            position.y=startingPosition.y+4*jumpHeight*JumpPhase*(1-JumpPhase);
            transform.localPosition=position;
            if(JumpPhase>=1){IsJumping=false;landingUntil=Time.realtimeSinceStartupAsDouble+.14;}
        }
        public void ApplyNetworkJump(bool jumping,float phase)
        {
            if(IsPinned||IsEliminated)return;
            if(!jumping){if(IsJumping){CancelJump();landingUntil=Time.realtimeSinceStartupAsDouble+.14;}return;}
            IsJumping=true;jumpStarted=Time.realtimeSinceStartupAsDouble-Mathf.Clamp01(phase)*jumpDuration;
            UpdateJump();
        }
        void CancelJump()
        {
            if(IsJumping){var position=transform.localPosition;position.y=startingPosition.y;transform.localPosition=position;}
            IsJumping=false;JumpPhase=0;landingUntil=0;
        }

        public void ResetPlayer()
        {
            var explosion=GetComponent<RecruitExplosion>();if(explosion!=null)explosion.ResetExplosion();
            WasBlasted=false;
            CancelJump();
            if (ragdoll != null) ragdoll.ResetRagdoll();
            if (hitAudio != null) hitAudio.ResetAudio();
            IsEliminated = false;
            releasedAfterSqueeze = false;
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
            CancelJump();
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
            Eliminated?.Invoke(this);
        }

        public void Explode(Material blood,AudioClip sound)
        {
            if(IsEliminated||IsPinned)return;
            WasBlasted=true;Eliminate();
            var explosion=GetComponent<RecruitExplosion>();
            if(explosion==null)explosion=gameObject.AddComponent<RecruitExplosion>();
            explosion.Burst(this,blood,sound);
        }

        public void PinTo(Transform bank, bool fromLeft, float spikeReach)
        {
            if (IsPinned || IsEliminated) return;
            CancelJump();
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
            Spiked?.Invoke(this);
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

        public void HoldAfterSqueeze()
        {
            if (!IsEliminated) return;
            // Preserve the completed crush independently of bank movement, so their
            // increasing gap cannot expand or drag the corpse during its slow fall.
            pinnedBank = null; squeezeLeft = squeezeRight = null;
            releasedAfterSqueeze = true;
        }

        public void StopForVictory()
        {
            CancelJump();
            // A tie supplied by an external death batch can already have marked the
            // selected winner dead. Restore only that recruit, preserving their location.
            if (IsEliminated)
            {
                Vector3 position = transform.position; Quaternion rotation = transform.rotation;
                ResetPlayer(); transform.SetPositionAndRotation(position, rotation);
            }
            Horizontal = 0;
            pinnedBank = null; squeezeLeft = squeezeRight = null;
            Quaternion facing = transform.rotation;
            march.enabled = false;
            march.ResetMarch();
            transform.rotation = facing;
        }

        public void ReleaseAfterSqueeze(float laneSurfaceY)
        {
            if (!IsEliminated) return;
            HoldAfterSqueeze();
            if (ragdoll != null) ragdoll.Activate(laneSurfaceY);
        }

        void LateUpdate()
        {
            if (IsPinned) { FollowPinnedBank(); return; }
            if (releasedAfterSqueeze) return; // The flexible snapshot now owns the visible fall.
            if (!IsEliminated) return;
            fallProgress = Mathf.Min(1, fallProgress + Time.unscaledDeltaTime / .4f);
            float t = Mathf.SmoothStep(0, 1, fallProgress);
            transform.localRotation = Quaternion.AngleAxis(-85 * t, Vector3.forward) * deathRotation;
            transform.localPosition = deathPosition + Vector3.up * (.2f * t);
        }
    }
}

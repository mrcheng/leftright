using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>World-scale physics proxies drive the flattened skin without putting rigidbodies
    /// under the FBX's scaled skeleton. Round reset restores the animation skeleton exactly.</summary>
    [DefaultExecutionOrder(300)]
    public sealed class RecruitRagdoll : MonoBehaviour
    {
        public bool IsActive => physicsRoot != null;
        GameObject physicsRoot;
        readonly List<Transform> bones = new List<Transform>();
        readonly List<Transform> proxies = new List<Transform>();
        readonly List<Quaternion> offsets = new List<Quaternion>();
        Transform[] skeleton;
        Vector3[] restPositions;
        Quaternion[] restRotations;

        public void Activate(bool fromLeft)
        {
            if (IsActive) return;
            skeleton = GetComponentInChildren<SkinnedMeshRenderer>().bones;
            restPositions = new Vector3[skeleton.Length]; restRotations = new Quaternion[skeleton.Length];
            var named = new Dictionary<string, Transform>();
            for (int i = 0; i < skeleton.Length; i++)
            {
                restPositions[i] = skeleton[i].localPosition; restRotations[i] = skeleton[i].localRotation;
                named[skeleton[i].name] = skeleton[i];
            }
            physicsRoot = new GameObject(name + " flattened ragdoll physics");
            var bodies = new Dictionary<string, Rigidbody>();
            var colliders = new List<Collider>();
            void Part(string boneName, string tipName, string parentName, float radius, float mass)
            {
                var bone = named[boneName];
                Vector3 direction = named[tipName].position - bone.position;
                float length = Mathf.Max(.08f, direction.magnitude);
                var part = new GameObject(boneName + " physics"); part.layer = 2; // Ignore raycasts, still collide with the walkway.
                part.transform.SetParent(physicsRoot.transform, false);
                part.transform.position = bone.position;
                part.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                var collider = part.AddComponent<CapsuleCollider>();
                collider.radius = radius; collider.height = Mathf.Max(length, radius * 2);
                collider.center = Vector3.up * (length * .5f);
                colliders.Add(collider);
                var body = part.AddComponent<Rigidbody>();
                body.mass = mass; body.linearDamping = .7f; body.angularDamping = 1.8f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.solverIterations = 12;
                // Keep corpses in their narrow lane while allowing a limp fall along its length.
                body.constraints = RigidbodyConstraints.FreezePositionZ;
                if (parentName != null)
                {
                    var joint = part.AddComponent<CharacterJoint>();
                    joint.connectedBody = bodies[parentName];
                    joint.anchor = Vector3.zero;
                    joint.enablePreprocessing = false;
                    joint.lowTwistLimit = new SoftJointLimit { limit = -25 };
                    joint.highTwistLimit = new SoftJointLimit { limit = 25 };
                    joint.swing1Limit = new SoftJointLimit { limit = 45 };
                    joint.swing2Limit = new SoftJointLimit { limit = 30 };
                }
                bodies.Add(boneName, body);
                bones.Add(bone); proxies.Add(part.transform);
                offsets.Add(Quaternion.Inverse(part.transform.rotation) * bone.rotation);
            }
            Part("Hips", "Abdomen", null, .09f, 5);
            Part("Torso", "Neck", "Hips", .10f, 6);
            Part("Head", "Head", "Torso", .105f, 2);
            foreach (string side in new[] { ".L", ".R" })
            {
                Part("UpperArm" + side, "LowerArm" + side, "Torso", .045f, 1);
                Part("LowerArm" + side, "Fist" + side, "UpperArm" + side, .04f, .7f);
                Part("UpperLeg" + side, "LowerLeg" + side, "Hips", .065f, 2);
                Part("LowerLeg" + side, "Foot" + side, "UpperLeg" + side, .055f, 1.5f);
            }
            // Internal contacts fight the joints on a deliberately flattened character.
            // Disable self-collision while preserving real contact with the ground.
            for (int i = 0; i < colliders.Count; i++)
                for (int j = i + 1; j < colliders.Count; j++) Physics.IgnoreCollision(colliders[i], colliders[j]);
            // A sideways release impulse moves the centre of mass past the boots; otherwise
            // a perfectly upright compressed rig can balance in place despite being limp.
            foreach (var body in bodies.Values)
                body.AddForce(Vector3.right * (fromLeft ? .65f : -.65f), ForceMode.VelocityChange);
            bodies["Torso"].AddTorque(Vector3.forward * (fromLeft ? -5f : 5f), ForceMode.Impulse);
            bodies["Hips"].AddTorque(Vector3.forward * (fromLeft ? -2f : 2f), ForceMode.Impulse);
        }

        void LateUpdate()
        {
            if (!IsActive) return;
            // Parent parts precede their limbs, so child world poses are applied last.
            for (int i = 0; i < bones.Count; i++)
                bones[i].SetPositionAndRotation(proxies[i].position, proxies[i].rotation * offsets[i]);
        }

        public void ResetRagdoll()
        {
            if (!IsActive) return;
            physicsRoot.SetActive(false); Destroy(physicsRoot); physicsRoot = null;
            for (int i = 0; i < skeleton.Length; i++)
                skeleton[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
            bones.Clear(); proxies.Clear(); offsets.Clear();
        }

        void OnDestroy() { if (physicsRoot != null) Destroy(physicsRoot); }
    }
}

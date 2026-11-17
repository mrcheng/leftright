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
        public bool IsSettled { get; private set; }
        public float settledThickness = .06f;
        public float fallSeconds = 1.6f;
        public float flattenSeconds = .4f;
        GameObject physicsRoot;
        readonly List<Transform> bones = new List<Transform>();
        readonly List<Transform> proxies = new List<Transform>();
        readonly List<Quaternion> offsets = new List<Quaternion>();
        Transform[] skeleton;
        Vector3[] restPositions;
        Quaternion[] restRotations;
        float age, floorHeight, originalBottom, originalHeight;
        Renderer[] originalRenderers;
        bool[] originalVisibility;
        readonly List<Mesh> settledMeshes = new List<Mesh>();
        readonly List<Vector3[]> settledVertices = new List<Vector3[]>();
        readonly List<Vector3[]> settledWorking = new List<Vector3[]>();
        readonly Dictionary<Renderer, Vector3[]> headTemplates = new Dictionary<Renderer, Vector3[]>();
        readonly Dictionary<Renderer, bool[]> headVertices = new Dictionary<Renderer, bool[]>();
        Transform headBone;

        public void Activate(bool fromLeft)
        {
            if (IsActive) return;
            age = 0; IsSettled = false;
            var player = GetComponent<RecruitController>();
            floorHeight = player.course.traps[player.playerIndex].transform.position.y + .006f;
            skeleton = GetComponentInChildren<SkinnedMeshRenderer>().bones;
            restPositions = new Vector3[skeleton.Length]; restRotations = new Quaternion[skeleton.Length];
            var named = new Dictionary<string, Transform>();
            for (int i = 0; i < skeleton.Length; i++)
            {
                restPositions[i] = skeleton[i].localPosition; restRotations[i] = skeleton[i].localRotation;
                named[skeleton[i].name] = skeleton[i];
            }
            headBone = named["Head"];
            CaptureHeadPlane();
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
            age += Time.unscaledDeltaTime;
            // Parent parts precede their limbs, so child world poses are applied last.
            for (int i = 0; i < bones.Count; i++)
                bones[i].SetPositionAndRotation(proxies[i].position, proxies[i].rotation * offsets[i]);
            if (age >= fallSeconds && settledMeshes.Count == 0) BakeSettledBody();
            if (settledMeshes.Count > 0 && !IsSettled)
            {
                float blend = Mathf.SmoothStep(0, 1, (age - fallSeconds) / Mathf.Max(.01f, flattenSeconds));
                for (int i = 0; i < settledMeshes.Count; i++)
                {
                    for (int v = 0; v < settledVertices[i].Length; v++)
                    {
                        var position = settledVertices[i][v];
                        float layer = (position.y - originalBottom) / originalHeight;
                        position.y = Mathf.Lerp(position.y, floorHeight + layer * settledThickness, blend);
                        settledWorking[i][v] = position;
                    }
                    settledMeshes[i].vertices = settledWorking[i];
                    settledMeshes[i].RecalculateNormals(); settledMeshes[i].RecalculateBounds();
                }
                IsSettled = blend >= 1;
            }
        }

        void BakeSettledBody()
        {
            // Physics gives the fall its loose pose, then the cartoon crush must win over
            // capsule thickness: otherwise boots, heads and rifles can stand upright again.
            // Bake every visible part into one common world-height band, preserving colors
            // and the fallen silhouette while giving the whole corpse paper-like thickness.
            foreach (var body in physicsRoot.GetComponentsInChildren<Rigidbody>())
            {
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            originalRenderers = GetComponentsInChildren<Renderer>();
            originalVisibility = new bool[originalRenderers.Length];
            originalBottom = float.MaxValue; float top = float.MinValue;
            for (int i = 0; i < originalRenderers.Length; i++)
            {
                var renderer = originalRenderers[i]; originalVisibility[i] = renderer.enabled;
                if (!renderer.enabled) continue;
                Mesh mesh;
                bool skinned = renderer is SkinnedMeshRenderer;
                if (skinned)
                {
                    mesh = new Mesh(); ((SkinnedMeshRenderer)renderer).BakeMesh(mesh);
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    mesh = Instantiate(filter.sharedMesh);
                }
                mesh.name = renderer.name + " flattened corpse";
                var vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    // BakeMesh already includes imported renderer scale; static FBX vertices do not.
                    vertices[v] = skinned ? renderer.transform.position + renderer.transform.rotation * vertices[v]
                        : renderer.transform.TransformPoint(vertices[v]);
                    // Use the squeezed head's face-up plane, not the tumbling proxy's
                    // sideways plane. The original compression becomes vertical thickness
                    // on the planks, instead of squeezing the face across the wrong axis.
                    if (headTemplates.TryGetValue(renderer, out var template) && headVertices[renderer][v])
                        vertices[v] = headBone.position + template[v];
                    originalBottom = Mathf.Min(originalBottom, vertices[v].y); top = Mathf.Max(top, vertices[v].y);
                }
                mesh.vertices = vertices;
                var part = new GameObject(renderer.name + " settled flat");
                part.transform.SetParent(physicsRoot.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                settledMeshes.Add(mesh); settledVertices.Add(vertices); settledWorking.Add(new Vector3[vertices.Length]);
                renderer.enabled = false;
            }
            originalHeight = Mathf.Max(.001f, top - originalBottom);
        }

        void CaptureHeadPlane()
        {
            Quaternion layFaceUp = Quaternion.FromToRotation(-transform.forward, Vector3.up);
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                Mesh mesh; bool temporary = renderer is SkinnedMeshRenderer;
                if (temporary) { mesh = new Mesh(); ((SkinnedMeshRenderer)renderer).BakeMesh(mesh); }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    mesh = filter.sharedMesh;
                }
                var vertices = mesh.vertices;
                var template = new Vector3[vertices.Length]; var mask = new bool[vertices.Length];
                BoneWeight[] weights = temporary ? ((SkinnedMeshRenderer)renderer).sharedMesh.boneWeights : null;
                Transform[] skinBones = temporary ? ((SkinnedMeshRenderer)renderer).bones : null;
                for (int v = 0; v < vertices.Length; v++)
                {
                    mask[v] = temporary ? skinBones[weights[v].boneIndex0] == headBone : renderer.transform.IsChildOf(headBone);
                    Vector3 world = temporary ? renderer.transform.position + renderer.transform.rotation * vertices[v]
                        : renderer.transform.TransformPoint(vertices[v]);
                    template[v] = layFaceUp * (world - headBone.position);
                }
                headTemplates[renderer] = template; headVertices[renderer] = mask;
                if (temporary) Destroy(mesh);
            }
        }

        public void ResetRagdoll()
        {
            if (!IsActive) return;
            physicsRoot.SetActive(false); Destroy(physicsRoot); physicsRoot = null;
            if (originalRenderers != null)
                for (int i = 0; i < originalRenderers.Length; i++)
                    if (originalRenderers[i] != null) originalRenderers[i].enabled = originalVisibility[i];
            ClearSettledMeshes(); IsSettled = false;
            for (int i = 0; i < skeleton.Length; i++)
                skeleton[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
            bones.Clear(); proxies.Clear(); offsets.Clear();
        }

        void ClearSettledMeshes()
        {
            foreach (var mesh in settledMeshes) if (mesh != null) Destroy(mesh);
            settledMeshes.Clear(); settledVertices.Clear(); settledWorking.Clear();
            headTemplates.Clear(); headVertices.Clear();
        }

        void OnDestroy() { ClearSettledMeshes(); if (physicsRoot != null) Destroy(physicsRoot); }
    }
}

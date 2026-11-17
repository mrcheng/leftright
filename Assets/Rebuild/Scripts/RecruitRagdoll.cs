using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>The crushed recruit falls as one thin physical sheet. Its geometry is baked
    /// once at release, so there is no second flattening, head rotation or mesh swap on landing.</summary>
    public sealed class RecruitRagdoll : MonoBehaviour
    {
        public bool IsActive => physicsRoot != null;
        public bool IsSettled { get; private set; }
        public float settledThickness = .06f;
        GameObject physicsRoot;
        Rigidbody sheetBody;
        PhysicsMaterial friction;
        Renderer[] originalRenderers;
        bool[] originalVisibility;
        readonly List<Mesh> meshes = new List<Mesh>();

        public void Activate(bool fromLeft)
        {
            if (IsActive) return;
            IsSettled = false;
            originalRenderers = GetComponentsInChildren<Renderer>();
            originalVisibility = new bool[originalRenderers.Length];
            var verticesByPart = new List<Vector3[]>();
            var materialsByPart = new List<Material[]>();
            var labels = new List<string>();
            Vector3 minimum = Vector3.one * float.MaxValue, maximum = Vector3.one * float.MinValue;
            for (int i = 0; i < originalRenderers.Length; i++)
            {
                var renderer = originalRenderers[i]; originalVisibility[i] = renderer.enabled;
                if (!renderer.enabled) continue;
                bool skinned = renderer is SkinnedMeshRenderer;
                Mesh mesh;
                if (skinned) { mesh = new Mesh(); ((SkinnedMeshRenderer)renderer).BakeMesh(mesh); }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    mesh = Instantiate(filter.sharedMesh);
                }
                var vertices = mesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    // BakeMesh includes the imported renderer's scale, while static mesh
                    // vertices still need the full import transform. All parts share one plane.
                    vertices[v] = skinned ? renderer.transform.position + renderer.transform.rotation * vertices[v]
                        : renderer.transform.TransformPoint(vertices[v]);
                    minimum = Vector3.Min(minimum, vertices[v]); maximum = Vector3.Max(maximum, vertices[v]);
                }
                meshes.Add(mesh); verticesByPart.Add(vertices);
                materialsByPart.Add(renderer.sharedMaterials); labels.Add(renderer.name);
                renderer.enabled = false;
            }
            Vector3 center = (minimum + maximum) * .5f;
            Vector3 size = maximum - minimum;
            physicsRoot = new GameObject(name + " flattened ragdoll physics");
            physicsRoot.layer = 2; // Liquid casts ignore the corpse; it still collides with planks.
            physicsRoot.transform.position = center;
            float squeeze = settledThickness / Mathf.Max(.001f, size.x);
            for (int i = 0; i < meshes.Count; i++)
            {
                var vertices = verticesByPart[i];
                for (int v = 0; v < vertices.Length; v++)
                {
                    vertices[v] -= center;
                    vertices[v].x *= squeeze;
                }
                var mesh = meshes[i]; mesh.name = labels[i] + " crushed sheet";
                mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var part = new GameObject(labels[i] + " crushed sheet");
                part.transform.SetParent(physicsRoot.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = materialsByPart[i];
            }
            // One thin box preserves the crushed silhouette instead of eleven round limb
            // colliders twisting it out of its plane. Gravity still drives the actual fall.
            var collider = physicsRoot.AddComponent<BoxCollider>();
            collider.size = new Vector3(settledThickness, size.y, size.z);
            collider.contactOffset = .001f;
            friction = new PhysicsMaterial("Flattened recruit friction")
            {
                dynamicFriction = .8f, staticFriction = .8f, bounciness = 0,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            collider.sharedMaterial = friction;
            sheetBody = physicsRoot.AddComponent<Rigidbody>();
            sheetBody.mass = 8; sheetBody.linearDamping = .35f; sheetBody.angularDamping = .6f;
            sheetBody.interpolation = RigidbodyInterpolation.Interpolate;
            sheetBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            sheetBody.solverIterations = 12;
            sheetBody.constraints = RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            // Fall face-up: the compressed X axis rotates onto world Y. The head never
            // gets an independent correction, and the face retains its original proportions.
            float side = fromLeft ? 1 : -1;
            sheetBody.linearVelocity = Vector3.left * (side * .55f);
            sheetBody.angularVelocity = Vector3.forward * (side * 2.5f);
        }

        void Update()
        {
            if (!IsActive) return;
            IsSettled = Mathf.Abs(Vector3.Dot(physicsRoot.transform.right, Vector3.up)) > .985f &&
                sheetBody.linearVelocity.sqrMagnitude < .04f && sheetBody.angularVelocity.sqrMagnitude < .04f;
        }

        public void ResetRagdoll()
        {
            if (!IsActive) return;
            physicsRoot.SetActive(false); Destroy(physicsRoot); physicsRoot = null;
            for (int i = 0; i < originalRenderers.Length; i++)
                if (originalRenderers[i] != null) originalRenderers[i].enabled = originalVisibility[i];
            ClearMeshes(); IsSettled = false;
        }

        void ClearMeshes()
        {
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            meshes.Clear();
            if (friction != null) Destroy(friction);
        }

        void OnDestroy() { ClearMeshes(); if (physicsRoot != null) Destroy(physicsRoot); }
    }
}

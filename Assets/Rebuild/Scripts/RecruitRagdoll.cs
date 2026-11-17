using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>A thin flexible corpse: cloth strips drive a continuously deformed snapshot.
    /// There is no delayed flattening, head correction or mesh replacement on landing.</summary>
    public sealed class RecruitRagdoll : MonoBehaviour
    {
        public bool IsActive => physicsRoot != null;
        public bool IsSettled { get; private set; }
        public bool FallDirectionRight { get; private set; }
        public float settledThickness = .06f;
        public float fallSpeed = 1f;
        GameObject physicsRoot;
        const int StripCount = 5;
        readonly Vector3[] nodes = new Vector3[StripCount + 1];
        readonly Vector3[] previousNodes = new Vector3[StripCount + 1];
        readonly float[] incomingVerticalSpeed = new float[StripCount + 1];
        readonly bool[] hasBounced = new bool[StripCount + 1];
        float fallAge, previousStep;
        readonly List<Vector3[]> restVertices = new List<Vector3[]>();
        readonly List<Vector3[]> bentVertices = new List<Vector3[]>();
        float stripHeight, firstStripY, floorY;
        Vector3 footAnchor;
        Renderer[] originalRenderers;
        bool[] originalVisibility;
        readonly List<Mesh> meshes = new List<Mesh>();

        public void Activate(float laneSurfaceY)
        {
            if (IsActive) return;
            IsSettled = false;
            fallAge = 0;
            previousStep = Time.fixedDeltaTime * fallSpeed * .1f;
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
                mesh.MarkDynamic();
                restVertices.Add(vertices); bentVertices.Add(new Vector3[vertices.Length]);
                var part = new GameObject(labels[i] + " crushed sheet");
                part.transform.SetParent(physicsRoot.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = materialsByPart[i];
            }
            // A small position-based cloth solver keeps connected strips stable even while
            // the spike colliders withdraw. Length constraints preserve the silhouette;
            // softer second-neighbour constraints give the sheet bending stiffness.
            stripHeight = size.y / StripCount;
            firstStripY = -size.y * .5f;
            // Use the owning lane's actual surface, never a ray that can hit the spikes.
            floorY = laneSurfaceY + settledThickness * .5f + .003f;
            footAnchor = new Vector3(center.x, floorY, center.z);
            // Cosmetic randomness stays separate from the officer and gameplay RNG.
            FallDirectionRight = new System.Random(System.Guid.NewGuid().GetHashCode()).Next(2) == 1;
            float side = FallDirectionRight ? -1 : 1;
            for (int i = 0; i <= StripCount; i++)
            {
                float y = firstStripY + i * stripHeight;
                nodes[i] = footAnchor + Vector3.up * (i * stripHeight);
                hasBounced[i] = false;
                // The upper body falls faster than the feet, starting a natural curl.
                previousNodes[i] = nodes[i] + Vector3.right * side * (.65f + (y - firstStripY) * 1.8f) * previousStep;
            }
            previousNodes[0] = nodes[0] = footAnchor;
        }

        void FixedUpdate()
        {
            if (!IsActive) return;
            // Give the first two seconds a slow, visible tip, then let the final second
            // accelerate sharply into impact. There is no pose switch or teleport:
            // the same gravity/constraints continue throughout the timing curve.
            fallAge += Time.fixedDeltaTime;
            float snap = Mathf.Clamp01(fallAge - 2);
            float speed = fallAge <= 2 ? Mathf.Lerp(.1f, .2f, fallAge / 2)
                : Mathf.Lerp(.2f, 2.5f, snap * snap);
            float lean = Vector3.Angle(nodes[StripCount] - footAnchor, Vector3.up);
            // Once the upper body has tipped past balance, whip the remaining head/
            // torso fall down in roughly a tenth of a second, rather than float down.
            speed = Mathf.Max(speed, Mathf.Lerp(0, 10, Mathf.InverseLerp(40, 55, lean)));
            // Return to normal time after the head lands so the rebound reads clearly
            // instead of being rushed through by the accelerated final drop.
            if (hasBounced[StripCount]) speed = 1;
            float duration = Time.fixedDeltaTime * fallSpeed * speed;
            // The final snap needs small cloth steps to preserve lengths and catch the
            // ground. Its impact cannot skip through the planks in one large timestep.
            int substeps = Mathf.Max(1, Mathf.CeilToInt(duration / .02f));
            for (int step = 0; step < substeps; step++)
            {
                SimulateStep(duration / substeps);
                if (hasBounced[StripCount]) break; // Let the rebound play next frame at normal speed.
            }
        }

        void SimulateStep(float dt)
        {
            for (int i = 1; i < nodes.Length; i++)
            {
                Vector3 current = nodes[i];
                Vector3 velocity = (current - previousNodes[i]) * (dt / previousStep) * .99f;
                incomingVerticalSpeed[i] = velocity.y / dt + Physics.gravity.y * dt;
                if (current.y <= floorY + .002f) velocity.x *= .75f;
                nodes[i] += velocity + Physics.gravity * (dt * dt);
                previousNodes[i] = current;
            }
            for (int iteration = 0; iteration < 16; iteration++)
            {
                // This bending constraint acts from release onward, never as a timed
                // post-landing straighten/flatten stage. Ground contact damps it naturally.
                // Gentle bending resistance lets the head and legs fold noticeably while
                // the stronger neighbouring constraints keep the sheet from stretching.
                for (int i = 0; i < nodes.Length - 2; i++) Constrain(i, i + 2, stripHeight * 2, .025f);
                for (int i = 0; i < nodes.Length - 1; i++) Constrain(i, i + 1, stripHeight, 1);
                for (int i = 0; i < nodes.Length; i++) nodes[i].y = Mathf.Max(floorY, nodes[i].y);
                // The figure tips around its grounded feet. Constraint corrections may
                // bend the rest of the sheet but must never launch its base into the air.
                nodes[0] = previousNodes[0] = footAnchor;
            }
            for (int i = 1; i < nodes.Length; i++)
            {
                if (nodes[i].y > floorY + .002f || incomingVerticalSpeed[i] >= -.4f || hasBounced[i]) continue;
                // One small, impact-driven rebound per section. Cap its ballistic rise
                // at 5.5 cm and keep the feet pinned, avoiding another airborne launch.
                float rebound = Mathf.Min(-incomingVerticalSpeed[i] * .3f, Mathf.Sqrt(2 * Mathf.Abs(Physics.gravity.y) * .055f));
                previousNodes[i].y = nodes[i].y - rebound * dt;
                hasBounced[i] = true;
            }
            previousStep = dt;
        }

        void Constrain(int a, int b, float length, float stiffness)
        {
            Vector3 difference = nodes[b] - nodes[a];
            float distance = difference.magnitude;
            if (distance < .00001f) return;
            Vector3 correction = difference * ((distance - length) / distance * stiffness);
            if (a == 0) nodes[b] -= correction; // Pinned foot has zero inverse mass.
            else { nodes[a] += correction * .5f; nodes[b] -= correction * .5f; }
        }

        void LateUpdate()
        {
            if (!IsActive) return;
            IsSettled = true;
            for (int i = 0; i < nodes.Length; i++)
                IsSettled &= nodes[i].y < floorY + .015f &&
                    (nodes[i] - previousNodes[i]).sqrMagnitude < .000016f;
            // Adjacent strip frames blend across each vertex, so skin and all equipment
            // form one continuous flexible surface. Always deform the original snapshot;
            // never accumulate errors or apply another flatten when physics comes to rest.
            for (int part = 0; part < meshes.Count; part++)
            {
                var rest = restVertices[part]; var bent = bentVertices[part];
                for (int v = 0; v < rest.Length; v++)
                {
                    var vertex = rest[v];
                    float coordinate = Mathf.Clamp((vertex.y - firstStripY) / stripHeight, 0, StripCount);
                    int lower = Mathf.Min(Mathf.FloorToInt(coordinate), StripCount - 1);
                    float blend = coordinate - lower;
                    Vector3 tangent = (nodes[lower + 1] - nodes[lower]).normalized;
                    Vector3 normal = new Vector3(tangent.y, -tangent.x, 0);
                    Vector3 position = Vector3.Lerp(nodes[lower], nodes[lower + 1], blend) + normal * vertex.x + Vector3.forward * vertex.z;
                    bent[v] = physicsRoot.transform.InverseTransformPoint(position);
                }
                meshes[part].vertices = bent;
                meshes[part].RecalculateNormals(); meshes[part].RecalculateBounds();
            }
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
            restVertices.Clear(); bentVertices.Clear();
        }

        void OnDestroy() { ClearMeshes(); if (physicsRoot != null) Destroy(physicsRoot); }
    }
}

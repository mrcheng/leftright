using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Stylized droplets with gravity/swept collisions and a persistent puddle.</summary>
    public sealed class BloodSplash : MonoBehaviour
    {
        Mesh puddleMesh, dropletMesh;
        Transform puddle;
        Transform[] drops;
        Vector3[] velocities;
        float age;
        int[] impacts;
        int landed;
        System.Random random;
        float strength;
        [Tooltip("Seconds for the main puddle to spread after droplets begin landing.")]
        public float spreadSeconds = 7f;
        public float laneHalfWidth = .56f;
        public float runoffSeconds = 12f;
        public bool HasReachedEdge { get; private set; }
        public int DripsEmitted { get; private set; }
        Vector3[] puddleOutline;
        Vector3[] spreadingVertices;
        Transform[] edgeDrops;
        Vector3[] edgeVelocities;
        float[] edgeAges;
        float spreadAge, edgeAge, nextDrip;

        public static BloodSplash Spawn(Vector3 position, Material material, Transform parent, float strength = 1)
        {
            var root = new GameObject("Blood splash and puddle");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            var effect = root.AddComponent<BloodSplash>();
            effect.strength = Mathf.Clamp(strength, .1f, 1);
            effect.Build(material);
            return effect;
        }

        public void AimBurst(Vector3 origin, Vector3 direction)
        {
            // The floor pool stays on the lane while the first spray starts at the hit.
            // Existing spike splashes keep their original emission unless this is called.
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i].position = origin;
                velocities[i] += direction.normalized * (2 + (float)random.NextDouble());
            }
        }

        void Build(Material material)
        {
            // A local RNG keeps cosmetic variation from changing instructor cues or pickup placement.
            random = new System.Random(Guid.NewGuid().GetHashCode());
            const int sides = 28;
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                float radius = .8f + (float)random.NextDouble() * .2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius * .85f, 0, Mathf.Sin(angle) * radius * .43f) * strength;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % sides + 1; triangles[i * 3 + 2] = i + 1;
            }
            puddleMesh = new Mesh { name = "Irregular blood puddle" };
            puddleMesh.vertices = vertices; puddleMesh.triangles = triangles;
            puddleMesh.RecalculateNormals(); puddleMesh.RecalculateBounds();
            puddleOutline = (Vector3[])vertices.Clone();
            spreadingVertices = new Vector3[vertices.Length];
            puddle = Part("Spreading puddle", puddleMesh, material);
            puddle.localScale = Vector3.one * .05f;
            dropletMesh = new Mesh { name = "Low-poly liquid droplet" };
            dropletMesh.vertices = new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            dropletMesh.triangles = new[] { 0,4,3,0,2,4,0,5,2,0,3,5,1,3,4,1,4,2,1,2,5,1,5,3 };
            dropletMesh.RecalculateNormals(); dropletMesh.RecalculateBounds();
            drops = new Transform[Mathf.RoundToInt(28 * strength)]; velocities = new Vector3[drops.Length]; impacts = new int[drops.Length];
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = Part("Liquid droplet", dropletMesh, material);
                drops[i].localPosition = Vector3.up * .75f;
                drops[i].localScale = new Vector3(.035f, .065f, .035f) * (.7f + (float)random.NextDouble());
                velocities[i] = new Vector3(((float)random.NextDouble() - .5f) * 3,
                    1.3f + (float)random.NextDouble() * 1.6f, ((float)random.NextDouble() - .5f) * .65f);
            }
            // A fixed pool bounds cost even when several lanes drip at the same time.
            edgeDrops = new Transform[24]; edgeVelocities = new Vector3[24]; edgeAges = new float[24];
            for (int i = 0; i < edgeDrops.Length; i++)
            {
                edgeDrops[i] = Part("Lane edge drip", dropletMesh, material);
                edgeDrops[i].gameObject.SetActive(false);
            }
        }

        Transform Part(string label, Mesh mesh, Material material)
        {
            var part = new GameObject(label); part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return part.transform;
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime; age += delta;
            SpreadAndDrip(delta);
            for (int i = 0; i < drops.Length; i++)
            {
                if (!drops[i].gameObject.activeSelf) continue;
                velocities[i] += Vector3.down * (8 * delta);
                Vector3 previous = drops[i].position;
                Vector3 next = previous + velocities[i] * delta;
                Vector3 movement = next - previous;
                // Sweep the travel segment so fast droplets cannot skip through the narrow
                // walkway. Off-edge drops continue falling instead of painting floating stains.
                if (Physics.SphereCast(previous, .018f, movement.normalized, out RaycastHit hit,
                    movement.magnitude + .018f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    landed++;
                    var stain = Part("Liquid impact splat", puddleMesh, GetComponentInChildren<Renderer>().sharedMaterial);
                    stain.position = hit.point + hit.normal * .004f;
                    stain.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
                    stain.localScale = Vector3.one * (.12f + (float)random.NextDouble() * .18f);
                    // One small recoil creates secondary drips; the next impact is absorbed
                    // into the pool. Energy decays rather than bouncing like rubber balls.
                    impacts[i]++;
                    if (impacts[i] >= 2) { drops[i].gameObject.SetActive(false); continue; }
                    drops[i].position = hit.point + hit.normal * .04f;
                    velocities[i] = Vector3.Reflect(velocities[i], hit.normal) * .18f + hit.normal * .35f;
                }
                else drops[i].position = next;
                float stretch = Mathf.Clamp(velocities[i].magnitude * .028f, .035f, .14f);
                drops[i].localScale = new Vector3(.028f, stretch, .028f);
                if (velocities[i].sqrMagnitude > .01f) drops[i].rotation = Quaternion.FromToRotation(Vector3.up, velocities[i]);
                if (age > 2.5f) drops[i].gameObject.SetActive(false);
            }
        }

        void SpreadAndDrip(float delta)
        {
            if (landed > 0) spreadAge += delta;
            float growth = Mathf.Lerp(.03f, 1.85f, Mathf.SmoothStep(0, 1, spreadAge / Mathf.Max(.1f, spreadSeconds)));
            puddle.localScale = new Vector3(growth, 1, growth);
            float radiusX = 0, radiusZ = 0;
            for (int i = 0; i < puddleOutline.Length; i++)
            {
                var point = puddleOutline[i];
                radiusX = Mathf.Max(radiusX, Mathf.Abs(point.x) * growth);
                radiusZ = Mathf.Max(radiusZ, Mathf.Abs(point.z) * growth);
                // Stop the horizontal pool at the plank edge rather than letting a flat
                // blood decal hover over the trench. Overflow is represented by falling drops.
                point.z = Mathf.Clamp(point.z, -laneHalfWidth / growth, laneHalfWidth / growth);
                spreadingVertices[i] = point;
            }
            puddleMesh.vertices = spreadingVertices; puddleMesh.RecalculateBounds();
            HasReachedEdge = radiusZ >= laneHalfWidth;
            if (HasReachedEdge)
            {
                edgeAge += delta; nextDrip -= delta;
                if (edgeAge < runoffSeconds && nextDrip <= 0)
                {
                    nextDrip = .15f + (float)random.NextDouble() * .20f;
                    float wetWidth = radiusX * Mathf.Sqrt(Mathf.Clamp01(1 - laneHalfWidth * laneHalfWidth / (radiusZ * radiusZ)));
                    EmitEdgeDrip(wetWidth);
                }
            }
            for (int i = 0; i < edgeDrops.Length; i++)
            {
                if (!edgeDrops[i].gameObject.activeSelf) continue;
                edgeAges[i] += delta;
                edgeVelocities[i] += Vector3.down * (8 * delta);
                edgeDrops[i].position += edgeVelocities[i] * delta;
                edgeDrops[i].localScale = new Vector3(.022f, Mathf.Clamp(edgeVelocities[i].magnitude * .035f, .045f, .18f), .022f);
                if (edgeAges[i] > 1.5f) edgeDrops[i].gameObject.SetActive(false);
            }
        }

        void EmitEdgeDrip(float wetWidth)
        {
            for (int i = 0; i < edgeDrops.Length; i++)
            {
                if (edgeDrops[i].gameObject.activeSelf) continue;
                float side = random.Next(2) == 0 ? -1 : 1;
                edgeDrops[i].position = transform.position + new Vector3(
                    ((float)random.NextDouble() * 2 - 1) * wetWidth * .8f, -.04f, side * (laneHalfWidth + .035f));
                edgeDrops[i].rotation = Quaternion.identity;
                edgeDrops[i].gameObject.SetActive(true);
                edgeVelocities[i] = new Vector3(0, -.12f, side * .045f);
                edgeAges[i] = 0; DripsEmitted++;
                return;
            }
        }

        void OnDestroy()
        {
            if (puddleMesh != null) Destroy(puddleMesh);
            if (dropletMesh != null) Destroy(dropletMesh);
        }
    }
}

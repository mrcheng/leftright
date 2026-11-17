using UnityEngine;

namespace LeftRight.Rebuild
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WindFlag : MonoBehaviour
    {
        public float width = 1.05f;
        public float height = 1.52f;
        public float amplitude = .18f;
        public float speed = 1.8f;
        public float phaseOffset;

        MeshFilter filter;
        Mesh originalMesh;
        Mesh animatedMesh;
        Vector3[] vertices;
        Vector2[] coordinates;

        void OnEnable()
        {
            filter = GetComponent<MeshFilter>();
            originalMesh = filter.sharedMesh;
            animatedMesh = Instantiate(originalMesh);
            animatedMesh.name = "Wind flag instance";
            animatedMesh.MarkDynamic();
            vertices = animatedMesh.vertices;
            coordinates = animatedMesh.uv;
            filter.sharedMesh = animatedMesh;
        }

        void LateUpdate()
        {
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = ClothPosition(coordinates[i], Time.time * speed, phaseOffset, width, height, amplitude);
            animatedMesh.vertices = vertices;
            animatedMesh.RecalculateNormals();
            animatedMesh.RecalculateBounds();
        }

        void OnDisable()
        {
            if (filter != null) filter.sharedMesh = originalMesh;
            if (animatedMesh != null) Destroy(animatedMesh);
        }

        static Vector3 ClothPosition(Vector2 uv, float time, float phase, float width, float height, float amplitude)
        {
            // Zero movement along the entire pole edge, increasing toward the tip.
            float free = Mathf.Pow(uv.x, 1.3f);
            float gust = .8f + .2f * Mathf.Sin(time * .37f);
            float swell = Mathf.Sin(uv.x * 8.5f - time * 2f + phase);
            float ripple = Mathf.Sin(uv.x * 19f + uv.y * 3.5f - time * 3.3f + phase);
            float z = free * amplitude * gust * (swell + .3f * ripple);
            float y = -uv.y * height - .10f * uv.x * uv.x
                + free * amplitude * .18f * Mathf.Sin(uv.x * 9f - time * 2f + phase);
            float x = uv.x * width - free * amplitude * .12f * (1f - Mathf.Cos(uv.x * 8.5f - time * 2f + phase));
            return new Vector3(x, y, z);
        }

        public static Mesh CreateClothMesh(int columns = 14, int rows = 18)
        {
            var mesh = new Mesh { name = "Flag cloth grid" };
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int row = 0; row <= rows; row++)
                for (int col = 0; col <= columns; col++)
                {
                    int index = row * (columns + 1) + col;
                    uv[index] = new Vector2((float)col / columns, (float)row / rows);
                    vertices[index] = ClothPosition(uv[index], 0, 0, 1.05f, 1.52f, .18f);
                }
            int t = 0;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                {
                    int a = row * (columns + 1) + col;
                    triangles[t++] = a;
                    triangles[t++] = a + columns + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + columns + 1;
                    triangles[t++] = a + columns + 2;
                }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

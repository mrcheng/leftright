using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>A short cosmetic reward: gameplay healing happens before this effect starts.</summary>
    public sealed class HealthCrateBurst : MonoBehaviour
    {
        public Transform lid;
        public Transform[] walls;
        public Transform syringe;
        public float duration = 1.1f;
        Vector3 lidRest, syringeRest;
        Quaternion lidRotation, syringeRotation;
        Vector3[] wallRest, wallDirections;
        Quaternion[] wallRotation;
        Renderer[] renderers;
        Color[][] colors;
        MaterialPropertyBlock block;
        float age;

        void Awake()
        {
            lidRest = lid.position; lidRotation = lid.rotation;
            syringeRest = syringe.position; syringeRotation = syringe.rotation;
            wallRest = new Vector3[walls.Length]; wallDirections = new Vector3[walls.Length];
            wallRotation = new Quaternion[walls.Length];
            for (int i = 0; i < walls.Length; i++)
            {
                wallRest[i] = walls[i].position; wallRotation[i] = walls[i].rotation;
                var outward = walls[i].position - transform.position; outward.y = 0;
                wallDirections[i] = outward.normalized;
            }
            renderers = GetComponentsInChildren<Renderer>();
            colors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials; colors[i] = new Color[materials.Length];
                for (int j = 0; j < materials.Length; j++) colors[i][j] = materials[j].GetColor("_BaseColor");
            }
            block = new MaterialPropertyBlock();
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            float burst = Mathf.SmoothStep(0, 1, age / .28f);
            for (int i = 0; i < walls.Length; i++)
            {
                // Rotate around each panel's bottom edge so it falls outward instead of spinning in place.
                Vector3 axis = Vector3.Cross(Vector3.up, wallDirections[i]);
                walls[i].rotation = Quaternion.AngleAxis(90 * burst, axis) * wallRotation[i];
                walls[i].position = wallRest[i];
            }
            float lidTime = Mathf.Min(age, .65f);
            lid.position = lidRest + transform.right * (.65f * Mathf.Min(age / .4f, 1))
                + Vector3.up * Mathf.Max(-.46f, 2.5f * lidTime - 6 * lidTime * lidTime);
            lid.rotation = Quaternion.AngleAxis(age * 210, transform.forward) * lidRotation;
            // The oversized red-filled syringe reads as a reward icon above the breaking box.
            float rise = Mathf.SmoothStep(0, 1, age / .9f);
            syringe.position = syringeRest + Vector3.up * (.1f + 1.55f * rise)
                - transform.forward * (.6f * Mathf.SmoothStep(0, 1, age / .2f));
            syringe.rotation = Quaternion.AngleAxis(-20 * rise, transform.forward) * syringeRotation;
            for (int i = 0; i < renderers.Length; i++)
            {
                bool isSyringe = renderers[i].transform == syringe || renderers[i].transform.IsChildOf(syringe);
                float fadeStart = isSyringe ? .55f : .45f;
                float alpha = 1 - Mathf.Clamp01((age - fadeStart) / Mathf.Max(.01f, duration - fadeStart));
                for (int j = 0; j < colors[i].Length; j++)
                {
                    var color = colors[i][j]; color.a = alpha;
                    block.Clear(); block.SetColor("_BaseColor", color);
                    renderers[i].SetPropertyBlock(block, j);
                }
            }
            if (age >= duration) Destroy(gameObject);
        }
    }
}

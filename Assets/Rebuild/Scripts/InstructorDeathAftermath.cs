using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Owns the instructor corpse and platform blood until the next round.
    /// Sequence cleanup can remove the pistol without making the instructor stand up.</summary>
    public sealed class InstructorDeathAftermath : MonoBehaviour
    {
        public bool HasLanded { get; private set; }
        public BloodSplash Blood { get; private set; }
        public Vector3 FocusPoint => actor.position + Vector3.right * (-fallSide * .8f) + Vector3.up * .25f;
        RebuildRoundStart round;
        Transform actor;
        Transform[] bones;
        Quaternion[] neutral, hitPose;
        Vector3 startPosition;
        Quaternion startRotation;
        bool directionsEnabled, marchEnabled;
        float age, floorY, fallSide;
        Renderer[] renderers;
        Mesh poseMesh;

        public void Initialize(RebuildRoundStart round, Vector3 hit, Vector3 direction,
            Material material, Transform[] bones, Quaternion[] neutral, bool directionsEnabled, bool marchEnabled)
        {
            this.round = round; actor = round.officer.transform; this.bones = bones; this.neutral = neutral;
            this.directionsEnabled = directionsEnabled; this.marchEnabled = marchEnabled;
            startPosition = actor.position; startRotation = actor.rotation;
            floorY = startPosition.y; fallSide = direction.x >= 0 ? -1 : 1;
            hitPose = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) hitPose[i] = bones[i].localRotation;
            renderers = actor.GetComponentsInChildren<Renderer>(); poseMesh = new Mesh();
            round.Cancelled += Clear;
            // The command platform art has no liquid collision surface. This invisible
            // temporary catcher lets the existing droplets land and grow a real pool.
            var surface = new GameObject("Instructor blood catch surface"); surface.transform.SetParent(transform, false);
            surface.transform.position = startPosition + Vector3.down * .05f;
            surface.AddComponent<BoxCollider>().size = new Vector3(5, .1f, 2.2f);
            if (material != null)
            {
                Blood = BloodSplash.Spawn(new Vector3(startPosition.x - fallSide * 1.1f, floorY + .012f, startPosition.z), material, transform);
                Blood.AimBurst(hit, direction); Blood.spreadSeconds = 2.5f; Blood.laneHalfWidth = 1;
            }
        }

        void Update()
        {
            if (actor == null || HasLanded) return;
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / .7f);
            actor.SetPositionAndRotation(startPosition, Quaternion.AngleAxis(fallSide * 90 * t * t, Vector3.forward) * startRotation);
            for (int i = 0; i < bones.Length; i++)
                bones[i].localRotation = Quaternion.Slerp(hitPose[i], neutral[i], Mathf.SmoothStep(0, 1, t));
            float bottom = float.PositiveInfinity;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    skin.BakeMesh(poseMesh, true);
                    foreach (var vertex in poseMesh.vertices)
                        bottom = Mathf.Min(bottom, (skin.transform.position + skin.transform.rotation * vertex).y);
                }
                else bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            }
            actor.position += Vector3.up * (floorY + .008f - bottom);
            if (t >= 1) HasLanded = true;
        }

        void Clear()
        {
            enabled = false;
            if (actor != null) actor.SetPositionAndRotation(startPosition, startRotation);
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = neutral[i];
            if (round != null)
            {
                round.officer.ResetDirections(); round.march.ResetMarch();
                round.officer.enabled = directionsEnabled; round.march.enabled = marchEnabled;
            }
            gameObject.SetActive(false); Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (round != null) round.Cancelled -= Clear;
            if (poseMesh != null) Destroy(poseMesh);
        }
    }
}

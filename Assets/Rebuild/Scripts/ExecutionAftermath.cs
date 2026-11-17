using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Presentation after the match result is final. A shot does not change
    /// the recorded winner or re-enter gameplay elimination/tie resolution.</summary>
    public sealed class ExecutionAftermath : MonoBehaviour
    {
        public bool HasLanded { get; private set; }
        public BloodSplash Blood { get; private set; }
        RecruitController winner;
        RebuildRoundStart round;
        Vector3 startPosition;
        Quaternion startRotation;
        Quaternion[] rest;
        Transform[] joints;
        Renderer[] renderers;
        float surfaceY, age, side;
        Mesh poseMesh;

        public void Initialize(RecruitController player, Vector3 hitPosition, Vector3 bulletDirection, Material bloodMaterial)
        {
            winner = player; round = player.round;
            round.Cancelled += Clear;
            startPosition = player.transform.position; startRotation = player.transform.rotation;
            surfaceY = player.course.traps[System.Array.IndexOf(player.course.players, player)].transform.position.y;
            side = new System.Random(System.Guid.NewGuid().GetHashCode()).Next(2) == 0 ? -1 : 1;
            joints = new[] { player.march.leftHip, player.march.rightHip, player.march.leftKnee,
                player.march.rightKnee, player.march.leftArm, player.march.rightArm };
            rest = new Quaternion[joints.Length];
            for (int i = 0; i < rest.Length; i++) rest[i] = joints[i].localRotation;
            renderers = player.GetComponentsInChildren<Renderer>();
            poseMesh = new Mesh();
            if (bloodMaterial != null)
            {
                Blood = BloodSplash.Spawn(new Vector3(startPosition.x - side * .85f, surfaceY + .012f, startPosition.z), bloodMaterial, transform);
                Blood.AimBurst(hitPosition, bulletDirection);
                Blood.spreadSeconds = 3.5f;
            }
        }

        void Update()
        {
            if (winner == null || HasLanded) return;
            age += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(age / .65f);
            float angle = 90 * t * t;
            // A limp side collapse keeps the body on its own narrow lane. Its existing
            // mesh keeps normal proportions; the spike-crush flattening is never used.
            winner.transform.SetPositionAndRotation(startPosition,
                Quaternion.AngleAxis(side * angle, Vector3.forward) * startRotation);
            for (int i = 0; i < joints.Length; i++)
            {
                float bend = i < 2 ? 18 : i < 4 ? -36 : 28;
                joints[i].localRotation = Quaternion.Euler(bend * Mathf.Sin(t * Mathf.PI * .5f), 0, 0) * rest[i];
            }
            // Keep the actual visible body resting on the boards as it rolls over.
            // Adjusting by its lower bound prevents penetration or an airborne bounce.
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
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                        foreach (var vertex in filter.sharedMesh.vertices)
                            bottom = Mathf.Min(bottom, filter.transform.TransformPoint(vertex).y);
                }
            }
            winner.transform.position += Vector3.up * (surfaceY + .008f - bottom);
            if (t >= 1) HasLanded = true;
        }

        void Clear()
        {
            // Round reset owns the recruit's original position, proportions and bones.
            // Disable immediately so a deferred Destroy cannot overwrite the next round.
            enabled = false; gameObject.SetActive(false); Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (round != null) round.Cancelled -= Clear;
            if (poseMesh != null) Destroy(poseMesh);
        }
    }
}

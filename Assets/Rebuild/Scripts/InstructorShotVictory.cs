using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    [CreateAssetMenu(menuName = "LeftRight/Victory/Instructor pistol shot")]
    public sealed class InstructorShotVictory : VictorySequence
    {
        public GameObject pistolPrefab;
        public Material flashMaterial, bulletMaterial, bloodMaterial;
        public AudioClip gunshot;
        public float drawStartsAt = .65f;
        public float aimSeconds = .9f;
        public float fireAt = 1.9f;
        public float bulletTravelSeconds = .28f;
        public float instructorZoomSeconds = 1.1f;

        public override VictoryPlayback CreatePlayback(RecruitController winner, Camera camera)
            => new Playback(this, winner, camera);

        sealed class Playback : VictoryPlayback
        {
            readonly InstructorShotVictory settings;
            readonly RecruitController winner;
            readonly Camera camera;
            readonly RebuildRoundStart round;
            readonly Transform instructor, shoulder, hand, head;
            readonly GameObject props, pistol, flash, bullet;
            readonly AudioSource audio;
            readonly Vector3 cameraStart, closeInstructor, instructorFocus, restArmDirection;
            readonly Quaternion cameraRotation, instructorRotation, shoulderRotation;
            readonly float originalFov;
            readonly bool directionsEnabled, marchEnabled;
            Vector3 muzzle, target, shotDirection;
            bool fired, hit;

            public Playback(InstructorShotVictory settings, RecruitController winner, Camera camera)
            {
                this.settings = settings; this.winner = winner; this.camera = camera; round = winner.round;
                instructor = round.officer.transform;
                directionsEnabled = round.officer.enabled; marchEnabled = round.march.enabled;
                round.officer.enabled = false; round.march.enabled = false;
                round.officer.ResetDirections(); round.march.ResetMarch();
                instructorRotation = instructor.rotation;
                shoulder = round.officer.rightShoulder; shoulderRotation = shoulder.localRotation;
                hand = instructor.GetComponentsInChildren<Transform>().Single(t => t.name == "Fist.R");
                head = winner.GetComponentsInChildren<Transform>().Single(t => t.name == "Head");
                restArmDirection = shoulder.InverseTransformDirection((hand.position - shoulder.position).normalized);
                cameraStart = camera.transform.position; cameraRotation = camera.transform.rotation; originalFov = camera.fieldOfView;
                instructorFocus = instructor.position + Vector3.up * 1.55f;
                closeInstructor = instructorFocus + new Vector3(1.5f, .6f, -3.8f);
                props = new GameObject("Instructor shot victory props");
                pistol = Object.Instantiate(settings.pistolPrefab, props.transform); pistol.name = "Victory pistol";
                flash = Part("Victory muzzle flash", PrimitiveType.Sphere, settings.flashMaterial, new Vector3(.18f, .18f, .32f)); flash.SetActive(false);
                bullet = Part("Victory bullet", PrimitiveType.Capsule, settings.bulletMaterial, new Vector3(.035f, .16f, .035f)); bullet.SetActive(false);
                audio = props.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                audio.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel", 1)) * .85f;
            }

            GameObject Part(string name, PrimitiveType type, Material material, Vector3 scale)
            {
                var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(props.transform, false);
                Object.Destroy(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = material;
                part.transform.localScale = scale; return part;
            }

            public override void Tick(float elapsed)
            {
                if (props == null) return;
                float aim = Mathf.SmoothStep(0, 1, (elapsed - settings.drawStartsAt) / Mathf.Max(.01f, settings.aimSeconds));
                Vector3 victimHead = head.position;
                Vector3 toward = Vector3.ProjectOnPlane(victimHead - instructor.position, Vector3.up).normalized;
                instructor.rotation = Quaternion.Slerp(instructorRotation, Quaternion.LookRotation(-toward), aim);
                shoulder.localRotation = shoulderRotation;
                Quaternion neutral = shoulder.rotation;
                Quaternion aimed = Quaternion.FromToRotation(neutral * restArmDirection, victimHead - shoulder.position) * neutral;
                shoulder.rotation = Quaternion.Slerp(neutral, aimed, aim);
                Vector3 gunDirection = (victimHead - hand.position).normalized;
                Vector3 holster = instructor.position + Vector3.up * .95f + instructor.right * .35f;
                pistol.transform.position = Vector3.Lerp(holster, hand.position + gunDirection * .04f, aim) + Vector3.up * (Mathf.Sin(aim * Mathf.PI) * .12f);
                pistol.transform.rotation = Quaternion.LookRotation(gunDirection);

                if (!fired && elapsed >= settings.fireAt)
                {
                    fired = true; target = victimHead; shotDirection = gunDirection;
                    muzzle = pistol.transform.TransformPoint(new Vector3(0, 0, .43f));
                    audio.PlayOneShot(settings.gunshot);
                }
                flash.SetActive(fired && elapsed < settings.fireAt + .085f);
                flash.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(gunDirection));
                if (fired)
                {
                    float flight = (elapsed - settings.fireAt) / Mathf.Max(.01f, settings.bulletTravelSeconds);
                    bullet.SetActive(flight < 1.35f);
                    bullet.transform.position = Vector3.LerpUnclamped(muzzle, target + shotDirection * .4f, Mathf.Min(flight, 1.35f));
                    bullet.transform.rotation = Quaternion.FromToRotation(Vector3.up, shotDirection);
                    float recoil = Mathf.Clamp01(1 - (elapsed - settings.fireAt) / .18f);
                    pistol.transform.position -= shotDirection * (.06f * recoil);
                    if (!hit && flight >= 1)
                    {
                        hit = true;
                        var aftermath = new GameObject("Execution aftermath").AddComponent<ExecutionAftermath>();
                        aftermath.Initialize(winner, target, shotDirection, settings.bloodMaterial);
                        var lens = winner.course.GetComponent<LensSplatter>();
                        if (lens != null) lens.AddSplatter(target, 1, .85f);
                        if (winner.hitAudio != null && winner.hitAudio.spikeImpact != null) audio.PlayOneShot(winner.hitAudio.spikeImpact);
                    }
                }
                // First feature the draw, then move rapidly to the victim before impact.
                float zoom = Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, settings.instructorZoomSeconds));
                Vector3 position = Vector3.Lerp(cameraStart, closeInstructor, zoom);
                Quaternion rotation = Quaternion.Slerp(cameraRotation, Quaternion.LookRotation(instructorFocus - closeInstructor), zoom);
                float fov = Mathf.Lerp(originalFov, 38, zoom);
                if (elapsed >= settings.fireAt)
                {
                    float cut = Mathf.SmoothStep(0, 1, (elapsed - settings.fireAt) / Mathf.Max(.01f, settings.bulletTravelSeconds * .85f));
                    float collapse = Mathf.SmoothStep(0, 1, (elapsed - settings.fireAt - settings.bulletTravelSeconds) / .8f);
                    Vector3 focus = Vector3.Lerp(winner.transform.position + Vector3.up * 1.2f,
                        winner.transform.position + Vector3.up * .35f, collapse);
                    Vector3 end = focus + new Vector3(.8f, 2.2f, -4.3f);
                    position = Vector3.Lerp(closeInstructor, end, cut);
                    rotation = Quaternion.Slerp(Quaternion.LookRotation(instructorFocus - closeInstructor), Quaternion.LookRotation(focus - end), cut);
                    fov = Mathf.Lerp(38, 43, cut);
                }
                camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            }

            public override void Stop()
            {
                if (props != null) { props.SetActive(false); Object.Destroy(props); }
                if (instructor != null) instructor.rotation = instructorRotation;
                if (round != null)
                {
                    round.officer.ResetDirections(); round.march.ResetMarch();
                    round.officer.enabled = directionsEnabled; round.march.enabled = marchEnabled;
                }
                if (camera != null) { camera.transform.SetPositionAndRotation(cameraStart, cameraRotation); camera.fieldOfView = originalFov; }
                // The corpse/puddle remain in ExecutionAftermath until round reset.
            }
        }
    }
}

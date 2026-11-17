using UnityEngine;

namespace LeftRight.Rebuild
{
    [CreateAssetMenu(menuName = "LeftRight/Victory/Turn and raise arms")]
    public sealed class ArmsRaisedVictory : VictorySequence
    {
        public float turnSeconds = .8f;
        public float armDelaySeconds = .3f;
        public float armRaiseSeconds = 1.1f;
        public float raisedArmDegrees = 145;
        public float zoomSeconds = 4f;
        public float cameraDistance = 4.8f;
        public float fieldOfView = 42;

        public override VictoryPlayback CreatePlayback(RecruitController winner, Camera camera)
            => new Playback(this, winner, camera);

        sealed class Playback : VictoryPlayback
        {
            readonly ArmsRaisedVictory settings;
            readonly RecruitController winner;
            readonly Camera camera;
            readonly Vector3 cameraStart, cameraEnd, focus;
            readonly Quaternion rotationStart, winnerStart, winnerEnd;
            readonly float fovStart;
            readonly Transform[] arms;
            readonly Quaternion[] rest;
            readonly float[] signs;

            public Playback(ArmsRaisedVictory settings, RecruitController winner, Camera camera)
            {
                this.settings = settings; this.winner = winner; this.camera = camera;
                cameraStart = camera.transform.position; rotationStart = camera.transform.rotation;
                fovStart = camera.fieldOfView; winnerStart = winner.transform.rotation;
                focus = winner.transform.position + Vector3.up * 1.15f;
                cameraEnd = focus - camera.transform.forward * settings.cameraDistance;
                Vector3 towardCamera = Vector3.ProjectOnPlane(cameraEnd - winner.transform.position, Vector3.up);
                // Recruit model conversion faces opposite the controller's forward axis.
                winnerEnd = Quaternion.LookRotation(-towardCamera.normalized, Vector3.up);
                arms = new[] { winner.march.leftArm, winner.march.rightArm };
                rest = new[] { arms[0].localRotation, arms[1].localRotation };
                signs = new float[2];
                for (int i = 0; i < arms.Length; i++)
                    signs[i] = Vector3.Dot(arms[i].position - winner.transform.position, winner.transform.right) > 0 ? 1 : -1;
            }

            public override void Tick(float elapsed)
            {
                float turn = Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, settings.turnSeconds));
                winner.transform.rotation = Quaternion.Slerp(winnerStart, winnerEnd, turn);
                float raise = Mathf.SmoothStep(0, 1, (elapsed - settings.armDelaySeconds) / Mathf.Max(.01f, settings.armRaiseSeconds));
                for (int i = 0; i < arms.Length; i++)
                {
                    Vector3 axis = arms[i].parent.InverseTransformDirection(winner.transform.forward);
                    arms[i].localRotation = Quaternion.AngleAxis(signs[i] * settings.raisedArmDegrees * raise, axis) * rest[i];
                }
                float zoom = Mathf.SmoothStep(0, 1, elapsed / Mathf.Max(.01f, settings.zoomSeconds));
                camera.transform.position = Vector3.Lerp(cameraStart, cameraEnd, zoom);
                camera.transform.rotation = Quaternion.Slerp(rotationStart, Quaternion.LookRotation(focus - cameraEnd), zoom);
                camera.fieldOfView = Mathf.Lerp(fovStart, settings.fieldOfView, zoom);
            }

            public override void Stop()
            {
                for (int i = 0; i < arms.Length; i++) if (arms[i] != null) arms[i].localRotation = rest[i];
                if (camera == null) return;
                camera.transform.SetPositionAndRotation(cameraStart, rotationStart); camera.fieldOfView = fovStart;
            }
        }
    }
}

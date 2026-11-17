using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    [CreateAssetMenu(menuName = "LeftRight/Victory/Instructor shouts and shoots himself")]
    public sealed class InstructorSelfShotVictory : VictorySequence
    {
        public GameObject pistolPrefab;
        public Material flashMaterial, bloodMaterial;
        public AudioClip shoutClip, gunshot;
        public float shoutAt = 1.05f;
        public float drawAt = 1.85f;
        public float drawSeconds = .9f;
        public float fireAt = 3.1f;
        public override bool PlayOpeningCheer => false;
        public override VictoryPlayback CreatePlayback(RecruitController winner, Camera camera) => new Playback(this, winner, camera);

        sealed class Playback : VictoryPlayback
        {
            readonly InstructorSelfShotVictory settings;
            readonly Camera camera;
            readonly RebuildRoundStart round;
            readonly Transform actor, upper, lower, hand, head;
            readonly Transform[] bones;
            readonly Quaternion[] neutral;
            readonly Vector3 startPosition, cameraStart, side, armBend;
            readonly Quaternion startRotation, cameraRotation;
            readonly float originalFov;
            readonly bool directionsEnabled, marchEnabled;
            readonly GameObject props, pistol, flash;
            readonly AudioSource audio;
            InstructorDeathAftermath aftermath;
            bool shouted, fired;
            Quaternion gripRotation;

            public Playback(InstructorSelfShotVictory settings, RecruitController winner, Camera camera)
            {
                this.settings = settings; this.camera = camera; round = winner.round; actor = round.officer.transform;
                directionsEnabled = round.officer.enabled; marchEnabled = round.march.enabled;
                round.officer.enabled = false; round.march.enabled = false;
                round.officer.ResetDirections(); round.march.ResetMarch();
                var transforms = actor.GetComponentsInChildren<Transform>();
                upper = transforms.Single(t => t.name == "UpperArm.R"); lower = transforms.Single(t => t.name == "LowerArm.R");
                hand = transforms.Single(t => t.name == "Fist.R"); head = transforms.Single(t => t.name == "Head");
                bones = new[] { upper, lower, head, round.officer.rightShoulder, round.march.rightArmSwing,
                    round.march.leftHip, round.march.rightHip, round.march.leftKnee, round.march.rightKnee };
                neutral = bones.Select(t => t.localRotation).ToArray();
                startPosition = actor.position; startRotation = actor.rotation;
                side = Vector3.ProjectOnPlane(upper.position - actor.position, Vector3.up).normalized;
                armBend = (side - actor.forward * .6f).normalized;
                cameraStart = camera.transform.position; cameraRotation = camera.transform.rotation; originalFov = camera.fieldOfView;
                props = new GameObject("Instructor self-shot props");
                pistol = Object.Instantiate(settings.pistolPrefab, props.transform); pistol.name = "Self-shot pistol";
                pistol.transform.localScale = Vector3.one * .8f;
                flash = GameObject.CreatePrimitive(PrimitiveType.Sphere); flash.name = "Self-shot muzzle flash";
                Object.Destroy(flash.GetComponent<Collider>()); flash.transform.SetParent(props.transform, false);
                flash.transform.localScale = new Vector3(.13f,.13f,.2f); flash.GetComponent<Renderer>().sharedMaterial = settings.flashMaterial; flash.SetActive(false);
                audio = props.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0;
                audio.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel", 1));
            }

            void AimArm(Vector3 target, float blend)
            {
                upper.localRotation = neutral[0]; lower.localRotation = neutral[1];
                Quaternion aRest = upper.rotation, bRest = lower.rotation;
                Vector3 shoulder = upper.position;
                float a = Vector3.Distance(shoulder,lower.position), b = Vector3.Distance(lower.position,hand.position);
                Vector3 delta = target - shoulder; float distance = Mathf.Clamp(delta.magnitude,.01f,a+b-.001f);
                Vector3 direction = delta.normalized;
                float cosine = Mathf.Clamp((a*a+distance*distance-b*b)/(2*a*distance),-1,1);
                Vector3 bend = Vector3.ProjectOnPlane(armBend,direction).normalized;
                Vector3 elbow = shoulder + direction*(a*cosine) + bend*(a*Mathf.Sqrt(1-cosine*cosine));
                upper.rotation = Quaternion.FromToRotation(lower.position-shoulder,elbow-shoulder)*upper.rotation;
                lower.rotation = Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
                Quaternion aAim=upper.rotation,bAim=lower.rotation;
                upper.rotation=Quaternion.Slerp(aRest,aAim,blend); lower.rotation=Quaternion.Slerp(bRest,bAim,blend);
            }

            public override void Tick(float elapsed)
            {
                if (props == null) return;
                if (!shouted && elapsed >= settings.shoutAt) { shouted = true; audio.PlayOneShot(settings.shoutClip); }
                if (!fired)
                {
                    float shout = elapsed >= settings.shoutAt && elapsed < settings.shoutAt + settings.shoutClip.length ? 1 : 0;
                    head.localRotation = neutral[2] * Quaternion.Euler(-6*shout,0,0);
                    Vector3 temple = head.position + Vector3.up*.15f + side*.22f;
                    Vector3 barrelDirection = -side;
                    float draw = Mathf.SmoothStep(0,1,(elapsed-settings.drawAt)/Mathf.Max(.01f,settings.drawSeconds));
                    AimArm(temple - barrelDirection*(.43f*.8f+.04f),draw);
                    Vector3 holster = actor.position + Vector3.up*.95f + side*.35f;
                    pistol.transform.position=Vector3.Lerp(holster,hand.position+barrelDirection*.04f,draw)+Vector3.up*(Mathf.Sin(draw*Mathf.PI)*.12f);
                    pistol.transform.rotation=Quaternion.LookRotation(barrelDirection);
                    if (elapsed >= settings.fireAt)
                    {
                        fired=true; gripRotation=Quaternion.Inverse(hand.rotation)*pistol.transform.rotation;
                        audio.PlayOneShot(settings.gunshot,.85f);
                        flash.transform.SetPositionAndRotation(temple,Quaternion.LookRotation(barrelDirection));
                        aftermath=new GameObject("Instructor death aftermath").AddComponent<InstructorDeathAftermath>();
                        aftermath.Initialize(round,temple,barrelDirection,settings.bloodMaterial,bones,neutral,directionsEnabled,marchEnabled);
                    }
                }
                else
                {
                    pistol.transform.SetPositionAndRotation(hand.position + hand.rotation*gripRotation*Vector3.forward*.04f,hand.rotation*gripRotation);
                }
                flash.SetActive(fired && elapsed < settings.fireAt+.085f);
                float zoom=Mathf.SmoothStep(0,1,elapsed/1.1f);
                Vector3 focus=startPosition+Vector3.up*1.55f;
                Vector3 close=focus+side*1.6f-actor.forward*3.6f+Vector3.up*.35f;
                Quaternion closeRotation=Quaternion.LookRotation(focus-close);
                Vector3 position=Vector3.Lerp(cameraStart,close,zoom);
                Quaternion rotation=Quaternion.Slerp(cameraRotation,closeRotation,zoom);
                float fov=Mathf.Lerp(originalFov,38,zoom);
                if(fired&&aftermath!=null)
                {
                    float drop=Mathf.SmoothStep(0,1,(elapsed-settings.fireAt)/.8f);
                    Vector3 floorFocus=aftermath.FocusPoint;
                    Vector3 wide=floorFocus+new Vector3(1.4f,2.3f,-4.3f);
                    position=Vector3.Lerp(close,wide,drop);
                    rotation=Quaternion.Slerp(closeRotation,Quaternion.LookRotation(floorFocus-wide),drop);
                    fov=Mathf.Lerp(38,43,drop);
                }
                camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
            }

            public override void Stop()
            {
                if(props!=null){props.SetActive(false);Object.Destroy(props);}
                // Once shot, the aftermath owns the corpse until Cancelled restores it.
                // Before the shot, cancellation must restore everything immediately.
                if(aftermath==null&&actor!=null)
                {
                    actor.SetPositionAndRotation(startPosition,startRotation);
                    for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=neutral[i];
                    round.officer.ResetDirections();round.march.ResetMarch();
                    round.officer.enabled=directionsEnabled;round.march.enabled=marchEnabled;
                }
                if(camera!=null){camera.transform.SetPositionAndRotation(cameraStart,cameraRotation);camera.fieldOfView=originalFov;}
            }
        }
    }
}

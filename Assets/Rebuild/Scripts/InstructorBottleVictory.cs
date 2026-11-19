using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    [CreateAssetMenu(menuName="LeftRight/Victory/Instructor bottle smash")]
    public sealed class InstructorBottleVictory : VictorySequence
    {
        public AudioClip glassHit;
        public Material bloodMaterial;
        public float grabAt=1.15f,windupAt=1.85f,smashAt=2.65f;
        public override bool PlayOpeningCheer=>false;
        public override VictoryPlayback CreatePlayback(RecruitController winner,Camera camera)=>new Playback(this,winner,camera);

        sealed class Playback : VictoryPlayback
        {
            readonly InstructorBottleVictory settings;readonly Camera camera;readonly RebuildRoundStart round;
            readonly OfficerVodkaBottle bottle;readonly Transform actor,upper,lower,hand,head,torso,leftFoot,rightFoot;
            readonly Transform[] bones;readonly Quaternion[] rest;
            readonly Vector3 start,cameraStart,side,bendDirection,homeGrip,headHitLocal;
            readonly Quaternion rotation,cameraRotation;readonly float fov,footOffset;
            readonly bool directionsEnabled,marchEnabled;
            readonly GameObject props;readonly AudioSource audio;
            bool grabbed,smashed;
            Vector3 pickupBody;
            const float BottleExtension=.32f;
            public Playback(InstructorBottleVictory settings,RecruitController winner,Camera camera)
            {
                this.settings=settings;this.camera=camera;round=winner.round;actor=round.officer.transform;
                bottle=winner.course.GetComponentInChildren<OfficerVodkaBottle>(true);bottle.Initialize(round);bottle.Restore();
                directionsEnabled=round.officer.enabled;marchEnabled=round.march.enabled;
                round.officer.enabled=false;round.march.enabled=false;round.officer.ResetDirections();round.march.ResetMarch();
                var all=actor.GetComponentsInChildren<Transform>();
                upper=all.Single(t=>t.name=="UpperArm.R");lower=all.Single(t=>t.name=="LowerArm.R");hand=all.Single(t=>t.name=="Fist.R");head=all.Single(t=>t.name=="Head");torso=all.Single(t=>t.name=="Abdomen");
                leftFoot=all.Single(t=>t.name=="Foot.L");rightFoot=all.Single(t=>t.name=="Foot.R");footOffset=Mathf.Min(leftFoot.position.y,rightFoot.position.y)-actor.position.y;
                bones=new[]{upper,lower,head,torso,round.officer.rightShoulder,round.march.rightArmSwing,round.march.leftHip,round.march.rightHip,round.march.leftKnee,round.march.rightKnee};
                rest=bones.Select(t=>t.localRotation).ToArray();start=actor.position;rotation=actor.rotation;
                side=Vector3.ProjectOnPlane(upper.position-actor.position,Vector3.up).normalized;bendDirection=(side-actor.forward*.5f).normalized;
                homeGrip=bottle.transform.TransformPoint(Vector3.up*bottle.gripHeight);
                // Locate the front of the actual stylized head (including its large
                // proportions), rather than putting effects inside a bone pivot.
                var headBounds=new Bounds(head.position+Vector3.up*.25f,Vector3.one*.6f);
                bool foundHead=false;
                foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    int index=System.Array.IndexOf(skin.bones,head);if(index<0)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh);var vertices=mesh.vertices;var weights=skin.sharedMesh.boneWeights;
                    for(int i=0;i<vertices.Length;i++)
                        if((weights[i].boneIndex0==index&&weights[i].weight0>.5f)||(weights[i].boneIndex1==index&&weights[i].weight1>.5f))
                        {
                            var point=skin.transform.position+skin.transform.rotation*vertices[i];
                            if(!foundHead){headBounds=new Bounds(point,Vector3.zero);foundHead=true;}else headBounds.Encapsulate(point);
                        }
                    Object.Destroy(mesh);
                }
                headHitLocal=head.InverseTransformPoint(headBounds.center+Vector3.up*(headBounds.extents.y*.15f)-actor.forward*(headBounds.extents.z*.98f)+side*(headBounds.extents.x*.35f));
                cameraStart=camera.transform.position;cameraRotation=camera.transform.rotation;fov=camera.fieldOfView;
                props=new GameObject("Instructor bottle victory effects");audio=props.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;audio.volume=Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel",1));
            }
            void AimArm(Vector3 target,float extension=0)
            {
                upper.localRotation=rest[0];lower.localRotation=rest[1];
                Vector3 shoulder=upper.position;float a=Vector3.Distance(shoulder,lower.position),b=Vector3.Distance(lower.position,hand.position)+extension;
                Vector3 delta=target-shoulder;float distance=Mathf.Clamp(delta.magnitude,.01f,a+b-.001f);Vector3 direction=delta.normalized;
                float cosine=Mathf.Clamp((a*a+distance*distance-b*b)/(2*a*distance),-1,1);
                Vector3 bend=Vector3.ProjectOnPlane(bendDirection,direction).normalized;
                Vector3 elbow=shoulder+direction*(a*cosine)+bend*(a*Mathf.Sqrt(1-cosine*cosine));
                upper.rotation=Quaternion.FromToRotation(lower.position-shoulder,elbow-shoulder)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            }
            public override void Tick(float elapsed)
            {
                if(props==null)return;
                float crouch=elapsed<settings.grabAt?Mathf.SmoothStep(0,1,(elapsed-.3f)/.6f):1-Mathf.SmoothStep(0,1,(elapsed-settings.grabAt)/.6f);
                actor.SetPositionAndRotation(start-Vector3.up*(.22f*crouch),rotation);
                for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
                torso.localRotation=rest[3]*Quaternion.Euler(0,0,-side.x*18*crouch);
                bones[6].localRotation=rest[6]*Quaternion.Euler(50*crouch,0,0);bones[7].localRotation=rest[7]*Quaternion.Euler(50*crouch,0,0);
                bones[8].localRotation=rest[8]*Quaternion.Euler(-90*crouch,0,0);bones[9].localRotation=rest[9]*Quaternion.Euler(-90*crouch,0,0);
                if(smashed)
                {
                    float hurt=Mathf.Exp(-(elapsed-settings.smashAt)*1.5f);
                    head.localRotation=rest[2]*Quaternion.Euler(8*hurt,0,-side.x*22*hurt);
                    torso.localRotation=rest[3]*Quaternion.Euler(0,0,-side.x*8*hurt);
                }
                // Plant the supporting boot during the crouch instead of letting
                // a root-only dip make both feet hover above the command platform.
                actor.position+=Vector3.up*(start.y+footOffset-Mathf.Min(leftFoot.position.y,rightFoot.position.y));
                Vector3 temple=head.TransformPoint(headHitLocal);
                Vector3 windup=temple+side*.65f+Vector3.up*.15f+actor.forward*.15f;
                float lift=Mathf.SmoothStep(0,1,(elapsed-settings.grabAt)/Mathf.Max(.1f,settings.windupAt-settings.grabAt));
                float strike=Mathf.Clamp01((elapsed-(settings.smashAt-.22f))/.22f);strike*=strike;
                if(!grabbed)
                {
                    AimArm(Vector3.Lerp(hand.position,homeGrip,Mathf.SmoothStep(0,1,elapsed/.8f)));
                    if(elapsed>=settings.grabAt)
                    {
                        grabbed=true;
                        var alongArm=(hand.position-lower.position).normalized;
                        bottle.AttachToHand(hand,Quaternion.FromToRotation(Vector3.up,-alongArm));
                        pickupBody=bottle.transform.TransformPoint(Vector3.up*(bottle.gripHeight-BottleExtension));
                    }
                }
                if(grabbed)
                {
                    // Solve the arm with the bottle as a rigid extension of the
                    // forearm. The hand/skeleton supplies the entire clockwise arc;
                    // the bottle's local grip remains unchanged throughout the smash.
                    AimArm(Vector3.Lerp(Vector3.Lerp(pickupBody,windup,lift),temple,strike),BottleExtension);
                }
                if(!smashed&&elapsed>=settings.smashAt)
                {
                    smashed=true;bottle.Break();audio.PlayOneShot(settings.glassHit,.95f);
                    props.AddComponent<BottleSmashEffects>().Initialize(temple,head,side,start,bottle.glassMaterial,bottle.liquidMaterial,settings.bloodMaterial,-actor.forward);
                    round.GetComponent<LensSplatter>()?.AddSplatter(head.position,.35f,.5f);
                }
                float zoom=Mathf.SmoothStep(0,1,elapsed/1.0f);Vector3 focus=start+Vector3.up*Mathf.Lerp(1.1f,1.65f,lift);
                Vector3 close=focus+side*1.25f-actor.forward*3.5f+Vector3.up*.25f;
                camera.transform.SetPositionAndRotation(Vector3.Lerp(cameraStart,close,zoom),Quaternion.Slerp(cameraRotation,Quaternion.LookRotation(focus-close),zoom));
                camera.fieldOfView=Mathf.Lerp(fov,Mathf.Lerp(44,38,lift),zoom);
            }
            public override void Stop()
            {
                if(props!=null){props.SetActive(false);Object.Destroy(props);}
                if(actor!=null)
                {actor.SetPositionAndRotation(start,rotation);for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=rest[i];round.officer.ResetDirections();round.march.ResetMarch();round.officer.enabled=directionsEnabled;round.march.enabled=marchEnabled;}
                if(bottle!=null)bottle.Restore();
                if(camera!=null){camera.transform.SetPositionAndRotation(cameraStart,cameraRotation);camera.fieldOfView=fov;}
            }
        }
    }
}

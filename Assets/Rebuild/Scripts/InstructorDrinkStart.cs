using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    [CreateAssetMenu(menuName="LeftRight/Start/Instructor drink and burp")]
    public sealed class InstructorDrinkStart : StartSequence
    {
        public AudioClip drink,burp,bottleDown;
        public override StartPlayback CreatePlayback(StartContext context)=>new Playback(this,context);
        sealed class Playback : StartPlayback
        {
            readonly InstructorDrinkStart settings;readonly StartContext context;
            readonly Transform actor,upper,lower,hand,head,torso,leftFoot,rightFoot;
            readonly Transform[] bones;readonly Quaternion[] rest;
            readonly Vector3 home,side,bend,gripHome,capHome,mouthLocal;
            readonly Quaternion rotation;readonly float footOffset;
            readonly bool directionsEnabled,marchEnabled;
            readonly OfficerVodkaBottle bottle;readonly GameObject props;
            Vector3 pickupCap;bool grabbed,placed;
            const float CapHeight=.63f;
            public Playback(InstructorDrinkStart settings,StartContext context)
            {
                this.settings=settings;this.context=context;var round=context.Round;actor=round.officer.transform;
                bottle=context.menu.GetComponentInChildren<OfficerVodkaBottle>(true);bottle.Initialize(round);bottle.Restore();
                directionsEnabled=round.officer.enabled;marchEnabled=round.march.enabled;
                round.officer.enabled=false;round.march.enabled=false;round.officer.ResetDirections();round.march.ResetMarch();
                var all=actor.GetComponentsInChildren<Transform>();
                upper=all.Single(t=>t.name=="UpperArm.R");lower=all.Single(t=>t.name=="LowerArm.R");hand=all.Single(t=>t.name=="Fist.R");head=all.Single(t=>t.name=="Head");torso=all.Single(t=>t.name=="Abdomen");
                leftFoot=all.Single(t=>t.name=="Foot.L");rightFoot=all.Single(t=>t.name=="Foot.R");
                bones=new[]{upper,lower,head,torso,round.officer.rightShoulder,round.march.rightArmSwing,round.march.leftHip,round.march.rightHip,round.march.leftKnee,round.march.rightKnee};rest=bones.Select(t=>t.localRotation).ToArray();
                home=actor.position;rotation=actor.rotation;footOffset=Mathf.Min(leftFoot.position.y,rightFoot.position.y)-home.y;
                side=Vector3.ProjectOnPlane(upper.position-actor.position,Vector3.up).normalized;bend=(side-actor.forward*.5f).normalized;
                gripHome=bottle.transform.TransformPoint(Vector3.up*bottle.gripHeight);capHome=bottle.transform.TransformPoint(Vector3.up*CapHeight);
                mouthLocal=head.InverseTransformPoint(head.position+Vector3.up*.15f-actor.forward*.42f+side*.1f);
                props=new GameObject("Instructor opening drink sounds");
                Schedule(.35f,settings.drink,.9f);Schedule(.59f,settings.burp,.9f);Schedule(.82f,settings.bottleDown,.8f);
            }
            static float Ease(float u,float a,float b)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,u));
            void Aim(Vector3 target,float extension)
            {
                upper.localRotation=rest[0];lower.localRotation=rest[1];Vector3 shoulder=upper.position;
                float a=Vector3.Distance(shoulder,lower.position),b=Vector3.Distance(lower.position,hand.position)+extension;
                Vector3 delta=target-shoulder;float d=Mathf.Clamp(delta.magnitude,.01f,a+b-.001f);Vector3 direction=delta.normalized;
                float cosine=Mathf.Clamp((a*a+d*d-b*b)/(2*a*d),-1,1);
                Vector3 elbow=shoulder+direction*(a*cosine)+Vector3.ProjectOnPlane(bend,direction).normalized*(a*Mathf.Sqrt(1-cosine*cosine));
                upper.rotation=Quaternion.FromToRotation(lower.position-shoulder,elbow-shoulder)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            }
            void Schedule(float at,AudioClip clip,float volume)
            {
                double dsp=context.Round.ScheduledStartDspTime-context.duration+at*context.duration;
                // Use the very same audio clock as music, independently of frame
                // hitches. Late peers skip past sounds instead of replaying a pile.
                if(clip==null||dsp<AudioSettings.dspTime)return;
                var voice=props.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.loop=false;voice.clip=clip;
                voice.volume=volume*Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel",1));voice.PlayScheduled(dsp);
            }
            public override void Tick(float elapsed)
            {
                // Normalize the whole performance to the shared 3–5 second lead-in:
                // reach/pick up, sip, burp, set down, then pull back over the last
                // 18%. No coroutine can extend this beyond the scheduled downbeat.
                if(props==null)return;float u=Mathf.Clamp01(elapsed/context.duration);
                float lift=Ease(u,.20f,.38f),put=Ease(u,.61f,.82f);
                float crouch=u<.4f?Ease(u,0,.18f)*(1-Ease(u,.20f,.38f)):Ease(u,.63f,.78f)*(1-Ease(u,.82f,.92f));
                for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];actor.SetPositionAndRotation(home-Vector3.up*.22f*crouch,rotation);
                torso.localRotation=rest[3]*Quaternion.Euler(0,0,-side.x*18*crouch);
                bones[6].localRotation=rest[6]*Quaternion.Euler(50*crouch,0,0);bones[7].localRotation=rest[7]*Quaternion.Euler(50*crouch,0,0);
                bones[8].localRotation=rest[8]*Quaternion.Euler(-90*crouch,0,0);bones[9].localRotation=rest[9]*Quaternion.Euler(-90*crouch,0,0);
                float sip=Ease(u,.29f,.38f)*(1-Ease(u,.58f,.66f));float burpNod=Mathf.Sin(Mathf.Clamp01((u-.59f)/.1f)*Mathf.PI);
                head.localRotation=rest[2]*Quaternion.Euler(-10*sip+4*burpNod,0,0);
                actor.position+=Vector3.up*(home.y+footOffset-Mathf.Min(leftFoot.position.y,rightFoot.position.y));
                if(!grabbed)
                {
                    Aim(Vector3.Lerp(hand.position,gripHome,Ease(u,0,.18f)),0);
                    if(u>=.20f){grabbed=true;bottle.AttachToHand(hand,Quaternion.FromToRotation(Vector3.up,-(hand.position-lower.position).normalized));pickupCap=bottle.transform.TransformPoint(Vector3.up*CapHeight);}
                }
                if(grabbed&&!placed)
                {
                    // Keep the same rigid grip as the smash sequence. For drinking,
                    // solve to the mouth using the bottle's lip behind the fist.
                    var cap=Vector3.Lerp(Vector3.Lerp(pickupCap,head.TransformPoint(mouthLocal),lift),capHome,put);
                    Aim(cap,-(CapHeight-bottle.gripHeight));
                    if(u>=.82f){placed=true;bottle.Restore();}
                }
                else if(placed)
                {
                    Aim(capHome,-(CapHeight-bottle.gripHeight));float release=Ease(u,.82f,.94f);
                    upper.localRotation=Quaternion.Slerp(upper.localRotation,rest[0],release);lower.localRotation=Quaternion.Slerp(lower.localRotation,rest[1],release);
                }
                float focusHeight=Mathf.Lerp(1.15f,1.65f,lift*(1-put));Vector3 focus=home+Vector3.up*focusHeight;
                Vector3 close=focus+side*1.15f-actor.forward*3.6f+Vector3.up*.3f;Quaternion closeRotation=Quaternion.LookRotation(focus-close);
                context.menu.view.GameplayPose(out var game,out var gameRotation,out float gameFov);
                float pull=Ease(u,.82f,1);
                // A short upward arc clears the front recruit while pulling back;
                // a straight line would pass through their head during the cut.
                Vector3 position=Vector3.Lerp(close,game,pull)+Vector3.up*(Mathf.Sin(pull*Mathf.PI)*2.2f);
                Quaternion look=pull>0?Quaternion.LookRotation(Vector3.Lerp(focus,context.menu.view.GameplayFocus,pull)-position):closeRotation;
                context.Camera.transform.SetPositionAndRotation(position,pull>=1?gameRotation:look);
                context.Camera.fieldOfView=Mathf.Lerp(Mathf.Lerp(44,38,lift*(1-put)),gameFov,pull);
            }
            public override void Stop()
            {
                if(props!=null){foreach(var voice in props.GetComponents<AudioSource>())voice.Stop();props.SetActive(false);Object.Destroy(props);}
                if(actor!=null)
                {actor.SetPositionAndRotation(home,rotation);for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=rest[i];context.Round.officer.ResetDirections();context.Round.march.ResetMarch();context.Round.officer.enabled=directionsEnabled;context.Round.march.enabled=marchEnabled;}
                if(bottle!=null)bottle.Restore();
            }
        }
    }
}

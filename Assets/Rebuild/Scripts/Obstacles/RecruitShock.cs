using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Cosmetic skeleton/pose and input lock share the course's beat clock.
    /// The host supplies absolute beat bounds, so pauses and remote snapshots never
    /// create a second timer or restart the two-beat lock on every packet.</summary>
    [DefaultExecutionOrder(80)]
    public sealed class RecruitShock : MonoBehaviour
    {
        public bool IsActive {get;private set;}
        public double StartedBeat {get;private set;}
        public double EndsBeat {get;private set;}
        public bool SkeletonVisible=>skeleton!=null&&skeleton.activeSelf;
        RecruitController player;Renderer[] skin;bool[] visible;
        Transform[] joints;Quaternion[] rest;bool marchEnabled;
        GameObject skeleton;Material bone,black;AudioSource audioOutput;
        readonly List<(Transform part,Transform from,Transform to,float width)> links=new List<(Transform,Transform,Transform,float)>();
        public void Begin(RecruitController owner,double start,double end)
        {
            if(IsActive&&Math.Abs(start-StartedBeat)<.0001){EndsBeat=end;return;}
            Stop();player=owner;StartedBeat=start;EndsBeat=end;IsActive=true;
            marchEnabled=player.march.enabled;player.march.enabled=false;
            joints=new[]{player.march.leftArm,player.march.rightArm,player.march.leftHip,player.march.rightHip};
            rest=joints.Select(j=>j.localRotation).ToArray();
            skin=GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();visible=skin.Select(r=>r.enabled).ToArray();
            bone=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.85f,.96f,1)};
            black=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.015f,.025f,.04f)};
            skeleton=new GameObject("Shock skeleton");skeleton.transform.SetParent(transform,false);
            Transform Find(string n)=>GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==n);
            var head=Find("Head");var body=Find("Abdomen");
            var skull=Part("Blocky skull",head,new Vector3(0,.04f,0),new Vector3(.43f,.43f,.39f),bone);
            skullMesh=SkullMesh();skull.GetComponent<MeshFilter>().sharedMesh=skullMesh;
            // Head-bone +Z faces out of the face (the imported model has a turn
            // under Model orientation). Attach sockets directly to that bone so
            // they never lag a frame behind another helper's LateUpdate.
            Part("Eye L",head,new Vector3(-.105f,.08f,.198f),new Vector3(.09f,.10f,.025f),black);
            Part("Eye R",head,new Vector3(.105f,.08f,.198f),new Vector3(.09f,.10f,.025f),black);
            Part("Nose socket",head,new Vector3(0,-.02f,.20f),new Vector3(.04f,.055f,.025f),black);
            Part("Jaw",head,new Vector3(0,-.22f,-.015f),new Vector3(.3f,.08f,.3f),bone);
            for(int i=0;i<4;i++)Part("Teeth",head,new Vector3(-.09f+i*.06f,-.14f,.17f),new Vector3(.04f,.06f,.04f),bone);
            Part("Spine",body,new Vector3(0,.08f,0),new Vector3(.07f,.5f,.07f),bone);
            for(int i=0;i<4;i++)Part("Rib "+i,body,new Vector3(0,.23f-i*.10f,0),new Vector3(.39f-i*.025f,.035f,.13f),bone);
            Part("Pelvis",body,new Vector3(0,-.23f,0),new Vector3(.31f,.1f,.15f),bone);
            foreach(string side in new[]{"L","R"})
            {
                Link(Find("UpperArm."+side),Find("LowerArm."+side),.065f);
                Link(Find("LowerArm."+side),Find("Fist."+side),.055f);
                Link(side=="L"?player.march.leftHip:player.march.rightHip,side=="L"?player.march.leftKnee:player.march.rightKnee,.075f);
                Link(side=="L"?player.march.leftKnee:player.march.rightKnee,Find("Foot."+side),.065f);
            }
            audioOutput=gameObject.AddComponent<AudioSource>();audioOutput.playOnAwake=false;audioOutput.spatialBlend=0;audioOutput.loop=true;
            audioOutput.clip=Resources.Load<AudioClip>("Electrocution");audioOutput.volume=.55f*Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel",1));
            if(audioOutput.clip!=null)audioOutput.Play();
        }
        Transform Part(string name,Transform anchor,Vector3 offset,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
            var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            go.transform.SetParent(skeleton.transform,false);go.GetComponent<Renderer>().sharedMaterial=material;
            // Bones carry their own helper transforms; no changes to the imported rig.
            var follow=go.AddComponent<ShockBone>();follow.anchor=anchor;follow.offset=offset;follow.size=size;
            return go.transform;
        }
        void Link(Transform from,Transform to,float width)
        {if(from!=null&&to!=null){var part=Part("Limb bone",from,Vector3.zero,Vector3.one,bone);var follow=part.GetComponent<ShockBone>();follow.enabled=false;Destroy(follow);links.Add((part,from,to,width));}}
        Mesh skullMesh;
        static Mesh SkullMesh()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            // Eight sides and bevelled crown/chin retain the camp's faceted style.
            for(int ring=0;ring<4;ring++)for(int i=0;i<8;i++)
            {float angle=(i+.5f)*Mathf.PI/4;float radius=ring==0||ring==3?.34f:.5f;vertices.Add(new Vector3(Mathf.Sin(angle)*radius,ring==0?-.5f:ring==1?-.32f:ring==2?.32f:.5f,Mathf.Cos(angle)*radius));}
            for(int ring=0;ring<3;ring++)for(int i=0;i<8;i++){int a=ring*8+i,b=ring*8+(i+1)%8;triangles.AddRange(new[]{a,b,a+8,b,b+8,a+8});}
            for(int i=1;i<7;i++){triangles.AddRange(new[]{0,i+1,i,24,24+i,25+i});}
            var mesh=new Mesh{name="Eight-sided shock skull"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void Update()
        {
            if(!IsActive)return;
            if(player.course.IsGameOver||player.IsEliminated||player.IsPinned||player.IsTrappedInHole||player.round.masterBeat.BeatPosition>=EndsBeat){Stop();return;}
            if(audioOutput!=null)
            {
                audioOutput.volume=.55f*Mathf.Clamp01(PlayerPrefs.GetFloat("AudioLevel",1))*Mathf.Clamp01((float)(EndsBeat-player.round.masterBeat.BeatPosition)/.12f);
                if(player.round.masterBeat.IsPlaying)audioOutput.UnPause();else audioOutput.Pause();
            }
        }
        void LateUpdate()
        {
            if(!IsActive)return;
            double phase=player.round.masterBeat.BeatPosition-StartedBeat;
            float shake=Mathf.Sin((float)phase*60)*7;
            for(int i=0;i<joints.Length;i++)joints[i].localRotation=rest[i]*Quaternion.Euler(i<2?0:shake,0,i<2?(i==0?55:-55)+shake:0);
            bool flash=((int)(phase*4)&1)==0; skeleton.SetActive(flash);
            for(int i=0;i<skin.Length;i++)if(skin[i]!=null)skin[i].enabled=!flash&&visible[i];
            foreach(var link in links){var delta=link.to.position-link.from.position;link.part.position=(link.from.position+link.to.position)*.5f;link.part.rotation=Quaternion.FromToRotation(Vector3.up,delta);link.part.localScale=new Vector3(link.width,delta.magnitude,link.width);}
        }
        public void Stop()
        {
            if(!IsActive)return;IsActive=false;
            for(int i=0;i<skin.Length;i++)if(skin[i]!=null)skin[i].enabled=visible[i];
            for(int i=0;i<joints.Length;i++)if(joints[i]!=null)joints[i].localRotation=rest[i];
            if(player!=null&&player.march!=null)player.march.enabled=marchEnabled;
            if(audioOutput!=null){audioOutput.Stop();Destroy(audioOutput);}
            if(skeleton!=null){skeleton.SetActive(false);Destroy(skeleton);}links.Clear();
            if(bone!=null)Destroy(bone);if(black!=null)Destroy(black);StartedBeat=EndsBeat=0;
            if(skullMesh!=null)Destroy(skullMesh);
        }
        void OnDisable()=>Stop();
    }
}

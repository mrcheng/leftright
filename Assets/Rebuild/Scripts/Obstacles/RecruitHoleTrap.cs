using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Hole -> wedged/kicking -> separated at spike capture. The recruit
    /// stays alive until the ordinary second-bank squeeze decides their result.
    /// The lower half is cosmetic and independent of the carried upper snapshot.</summary>
    [DefaultExecutionOrder(20)]
    public sealed class RecruitHoleTrap : MonoBehaviour
    {
        public bool IsWedged {get;private set;}
        public bool IsSeparated {get;private set;}
        public bool IsActive=>IsWedged||IsSeparated;
        public bool LowerSettled {get;private set;}
        public float UpperWidth
        {
            get{var bounds=new Bounds(upper.transform.position,Vector3.zero);foreach(var r in upper.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);return bounds.size.x;}
        }
        RecruitController player;
        Transform[] joints;Quaternion[] rest;
        Vector3 entry,target;float age,lowerAge;float cutY;
        GameObject upper,effects,lower;
        Renderer[] originals;bool[] visibility;
        readonly List<Mesh> meshes=new List<Mesh>();
        Vector3 lowerVelocity;float lowerAngle;

        public void Begin(RecruitController owner,float laneY)
        {
            ResetTrap();player=owner;
            joints=new[]{owner.march.leftHip,owner.march.rightHip,owner.march.leftKnee,owner.march.rightKnee,owner.march.leftArm,owner.march.rightArm};
            rest=new Quaternion[joints.Length];for(int i=0;i<joints.Length;i++)rest[i]=joints[i].localRotation;
            entry=transform.position;
            // Settle the waist at the opening while the head/chest remain above it.
            // Bent knees keep the boots kicking beneath the deck instead of planting.
            float waist=(joints[0].position.y+joints[1].position.y)*.5f+.10f;
            cutY=laneY+.045f;target=entry-Vector3.up*Mathf.Max(.1f,waist-cutY);
            IsWedged=true;age=0;
        }
        void LateUpdate()
        {
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(IsWedged)
            {
                if(!player.round.masterBeat.IsPlaying)return;
                age+=dt;float t=Mathf.Clamp01(age/.55f);
                transform.position=Vector3.Lerp(entry,target,t*t*(3-2*t));
                float kick=Mathf.Sin(age*13),other=Mathf.Sin(age*13+Mathf.PI);
                Pose(0,50+24*kick);Pose(1,50+24*other);
                // An outward flail occasionally exposes the boots beyond the ledge,
                // making the below-deck struggle readable from the game camera.
                for(int i=0;i<2;i++)
                {
                    var axis=joints[i].parent.InverseTransformDirection(Vector3.right);
                    joints[i].localRotation=Quaternion.AngleAxis(35+20*Mathf.Sin(age*10+i*Mathf.PI),axis)*joints[i].localRotation;
                }
                Pose(2,-65-22*Mathf.Max(0,kick));Pose(3,-65-22*Mathf.Max(0,other));
                Pose(4,-35+12*other);Pose(5,-35+12*kick);
            }
            if(IsSeparated&&!LowerSettled)
            {
                lowerAge+=dt;lowerVelocity+=Vector3.down*(12*dt);lower.transform.position+=lowerVelocity*dt;
                lowerAngle=Mathf.MoveTowards(lowerAngle,85,160*dt);lower.transform.rotation=Quaternion.Euler(0,0,lowerAngle);
                float minimum=float.MaxValue;foreach(var renderer in lower.GetComponentsInChildren<Renderer>())minimum=Mathf.Min(minimum,renderer.bounds.min.y);
                if(minimum<.025f)
                {
                    lower.transform.position+=Vector3.up*(.025f-minimum);
                    lowerVelocity.y=Mathf.Abs(lowerVelocity.y)*.10f;lowerVelocity.x*=.4f;
                    if(lowerAge>1.1f&&lowerAngle>=85)LowerSettled=true;
                }
            }
        }
        void Pose(int index,float degrees)
        {
            var joint=joints[index];var axis=joint.parent.InverseTransformDirection(transform.right);
            joint.localRotation=Quaternion.AngleAxis(degrees,axis)*rest[index];
        }
        public void Capture(bool fromLeft,Material blood)
        {
            if(!IsWedged)return;
            // Complete the short slide before baking, even if a nearby bank arrives
            // during the entry animation. Bake the actual kicking pose only once.
            transform.position=target;IsWedged=false;IsSeparated=true;
            originals=player.GetComponentsInChildren<Renderer>();visibility=new bool[originals.Length];
            upper=new GameObject("Carried head and torso");upper.transform.position=new Vector3(transform.position.x,cutY,transform.position.z);
            upper.transform.SetParent(player.transform.Find("Model orientation"),true);
            effects=new GameObject("Hole victim lower half and blood");effects.transform.SetParent(player.course.transform,false);
            lower=new GameObject("Detached kicking legs");lower.transform.SetParent(effects.transform,true);lower.transform.position=upper.transform.position;
            for(int i=0;i<originals.Length;i++)
            {
                var renderer=originals[i];visibility[i]=renderer.enabled;if(!visibility[i])continue;
                Mesh source;bool skin=renderer is SkinnedMeshRenderer;
                if(skin){source=new Mesh();((SkinnedMeshRenderer)renderer).BakeMesh(source);}
                else{var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;source=Instantiate(filter.sharedMesh);}
                Vector3[] vertices=source.vertices;var uv=source.uv;
                for(int v=0;v<vertices.Length;v++)vertices[v]=skin?renderer.transform.position+renderer.transform.rotation*vertices[v]:renderer.transform.TransformPoint(vertices[v]);
                Extract(source,vertices,uv,renderer.sharedMaterials,upper.transform,true,blood);
                Extract(source,vertices,uv,renderer.sharedMaterials,lower.transform,false,blood);
                Destroy(source);renderer.enabled=false;
            }
            // Recenter the free lower snapshot so its rotation is around its mass,
            // rather than orbiting the waist or inheriting the moving spike bank.
            var bounds=new Bounds(lower.transform.position,Vector3.zero);
            foreach(var renderer in lower.GetComponentsInChildren<Renderer>())bounds.Encapsulate(renderer.bounds);
            var children=new List<Transform>();foreach(Transform child in lower.transform)children.Add(child);
            foreach(var child in children)child.SetParent(effects.transform,true);
            lower.transform.position=bounds.center;foreach(var child in children)child.SetParent(lower.transform,true);
            lowerVelocity=new Vector3(fromLeft?.25f:-.25f,-.2f,-.8f);lowerAngle=lowerAge=0;
            if(blood!=null)
                BloodSplash.Spawn(new Vector3(transform.position.x,.015f,transform.position.z-.65f),blood,effects.transform,.8f)
                    .AimBurst(new Vector3(transform.position.x,cutY,transform.position.z),new Vector3(fromLeft?1:-1,-.8f,-.5f));
        }
        struct Vertex
        {public Vector3 position;public Vector2 uv;public Vertex(Vector3 p,Vector2 u){position=p;uv=u;}}
        void Extract(Mesh source,Vector3[] world,Vector2[] uv,Material[] materials,Transform parent,bool above,Material blood)
        {
            var vertices=new List<Vector3>();var coords=new List<Vector2>();var submeshes=new List<int[]>();var cuts=new List<Vector3>();
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var indices=new List<int>();var triangles=source.GetTriangles(sub);
                for(int t=0;t<triangles.Length;t+=3)
                {
                    var polygon=new List<Vertex>();
                    for(int edge=0;edge<3;edge++)
                    {
                        int a=triangles[t+edge],b=triangles[t+(edge+1)%3];
                        var va=new Vertex(world[a],uv.Length==world.Length?uv[a]:Vector2.zero);
                        var vb=new Vertex(world[b],uv.Length==world.Length?uv[b]:Vector2.zero);
                        bool insideA=above?va.position.y>=cutY:va.position.y<=cutY,insideB=above?vb.position.y>=cutY:vb.position.y<=cutY;
                        if(insideA)polygon.Add(va);
                        if(insideA!=insideB)
                        {
                            float fraction=(cutY-va.position.y)/(vb.position.y-va.position.y);
                            var intersection=new Vertex(Vector3.Lerp(va.position,vb.position,fraction),Vector2.Lerp(va.uv,vb.uv,fraction));
                            polygon.Add(intersection);cuts.Add(intersection.position);
                        }
                    }
                    for(int i=1;i<polygon.Count-1;i++)foreach(var v in new[]{polygon[0],polygon[i],polygon[i+1]})
                    {indices.Add(vertices.Count);vertices.Add(parent.InverseTransformPoint(v.position));coords.Add(v.uv);}
                }
                submeshes.Add(indices.ToArray());
            }
            if(vertices.Count==0)return;
            // Close the cut with a dark red surface, preserving original materials
            // and palette UVs on the actual skin/equipment triangles around it.
            var cap=new List<int>();
            if(cuts.Count>2&&blood!=null)
            {
                Vector3 center=Vector3.zero;foreach(var p in cuts)center+=p;center/=cuts.Count;
                cuts.Sort((a,b)=>Mathf.Atan2(a.z-center.z,a.x-center.x).CompareTo(Mathf.Atan2(b.z-center.z,b.x-center.x)));
                for(int i=0;i<cuts.Count;i++)foreach(var p in new[]{center,cuts[above?i:(i+1)%cuts.Count],cuts[above?(i+1)%cuts.Count:i]})
                {cap.Add(vertices.Count);vertices.Add(parent.InverseTransformPoint(p));coords.Add(Vector2.zero);}
            }
            var mesh=new Mesh{name=above?"Waist-cut upper body":"Waist-cut lower body"};meshes.Add(mesh);
            mesh.SetVertices(vertices);mesh.SetUVs(0,coords);mesh.subMeshCount=submeshes.Count+1;
            for(int i=0;i<submeshes.Count;i++)mesh.SetTriangles(submeshes[i],i);mesh.SetTriangles(cap,submeshes.Count);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var part=new GameObject(mesh.name);part.transform.SetParent(parent,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.AddComponent<MeshRenderer>();var palette=new Material[submeshes.Count+1];
            for(int i=0;i<submeshes.Count;i++)palette[i]=materials[Mathf.Min(i,materials.Length-1)];palette[palette.Length-1]=blood!=null?blood:materials[0];renderer.sharedMaterials=palette;
        }
        public void ResetTrap()
        {
            if(originals!=null)for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].enabled=visibility[i];
            if(upper!=null){upper.SetActive(false);Destroy(upper);}if(effects!=null){effects.SetActive(false);Destroy(effects);}
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);meshes.Clear();
            if(joints!=null)for(int i=0;i<joints.Length;i++)if(joints[i]!=null)joints[i].localRotation=rest[i];
            upper=effects=lower=null;originals=null;IsWedged=IsSeparated=LowerSettled=false;
        }
        void OnDestroy()=>ResetTrap();
    }
}

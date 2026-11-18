using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Cosmetic mesh fragmentation. Gameplay dies once on the host; pieces,
    /// sparks and liquid use their own RNG and cannot affect scoring or spawn rolls.</summary>
    public sealed class RecruitExplosion : MonoBehaviour
    {
        sealed class Fragment
        {
            public Transform part;public Vector3 velocity,spin;public int bounces;
        }
        sealed class Bucket
        {
            public readonly List<Vector3> vertices=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>();
        }
        GameObject root;
        Renderer[] originals;
        bool[] visible;
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Fragment> pieces=new List<Fragment>();
        readonly List<Transform> sparks=new List<Transform>();
        readonly List<Vector3> sparkVelocities=new List<Vector3>();
        readonly List<Material> materials=new List<Material>();
        readonly System.Random random=new System.Random(Guid.NewGuid().GetHashCode());
        Light flash;
        float age;
        public int FragmentCount=>pieces.Count;
        public bool IsActive=>root!=null;
        public bool HasBlastSound { get; private set; }
        float Random(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());

        public void Burst(RecruitController player,Material blood,AudioClip sound)
        {
            ResetExplosion();
            originals=GetComponentsInChildren<Renderer>();visible=new bool[originals.Length];
            root=new GameObject(name+" mine explosion");root.transform.SetParent(transform.parent,true);
            root.transform.position=player.transform.position;
            age=0;
            for(int i=0;i<originals.Length;i++)
            {
                var renderer=originals[i];visible[i]=renderer.enabled;if(!visible[i])continue;
                Mesh source;bool skinned=renderer is SkinnedMeshRenderer;
                if(skinned){source=new Mesh();((SkinnedMeshRenderer)renderer).BakeMesh(source);}
                else {var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;source=Instantiate(filter.sharedMesh);}
                var vertices=source.vertices;var uv=source.uv;
                var bounds=new Bounds(renderer.bounds.center,renderer.bounds.size);
                for(int v=0;v<vertices.Length;v++)vertices[v]=skinned?renderer.transform.position+renderer.transform.rotation*vertices[v]:renderer.transform.TransformPoint(vertices[v]);
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    var buckets=new Bucket[6];var triangles=source.GetTriangles(sub);
                    for(int t=0;t<triangles.Length;t+=3)
                    {
                        Vector3 center=(vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3;
                        int row=Mathf.Clamp((int)((center.y-bounds.min.y)/Mathf.Max(.01f,bounds.size.y)*3),0,2);
                        int index=row*2+(center.x>bounds.center.x?1:0);
                        var bucket=buckets[index]??(buckets[index]=new Bucket());
                        for(int j=0;j<3;j++){int v=triangles[t+j];bucket.vertices.Add(vertices[v]);bucket.uv.Add(uv.Length==vertices.Length?uv[v]:Vector2.zero);}
                    }
                    foreach(var bucket in buckets)
                    {
                        if(bucket==null)continue;
                        var center=Vector3.zero;foreach(var vertex in bucket.vertices)center+=vertex;center/=bucket.vertices.Count;
                        for(int v=0;v<bucket.vertices.Count;v++)bucket.vertices[v]-=center;
                        var indices=new int[bucket.vertices.Count];for(int v=0;v<indices.Length;v++)indices[v]=v;
                        var mesh=new Mesh{name="Shattered recruit section"};mesh.SetVertices(bucket.vertices);mesh.SetUVs(0,bucket.uv);mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                        var part=new GameObject("Body fragment");part.layer=2;part.transform.SetParent(root.transform,true);part.transform.position=center;
                        part.AddComponent<MeshFilter>().sharedMesh=mesh;
                        part.AddComponent<MeshRenderer>().sharedMaterial=renderer.sharedMaterials[Mathf.Min(sub,renderer.sharedMaterials.Length-1)];
                        Vector3 outward=(center-player.transform.position).normalized;
                        pieces.Add(new Fragment{part=part.transform,velocity=new Vector3(outward.x*Random(2,5)+Random(-2,2),Random(3,7),Random(-2.7f,2.7f)),spin=new Vector3(Random(-300,300),Random(-300,300),Random(-300,300))});
                    }
                }
                Destroy(source);renderer.enabled=false;
            }
            if(blood!=null)
            {
                // Three offset sprays give the blast broad coverage while preserving
                // existing gravity, landing splashes, spreading pools and edge drips.
                for(int i=0;i<3;i++)
                {
                    var floor=new Vector3(player.transform.position.x+(i-1)*.25f,.662f,player.transform.position.z);
                    BloodSplash.Spawn(floor,blood,root.transform,1).AimBurst(player.transform.position+Vector3.up*.6f,new Vector3((i-1)*1.5f,1,Random(-.5f,.5f)));
                }
            }
            var sparkMesh=new Mesh{name="Tiny blast spark"};
            sparkMesh.vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
            sparkMesh.triangles=new[]{0,4,3,0,2,4,0,5,2,0,3,5,1,3,4,1,4,2,1,2,5,1,5,3};
            sparkMesh.RecalculateNormals();sparkMesh.RecalculateBounds();meshes.Add(sparkMesh);
            var sparkMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(1,.5f,.08f)};
            sparkMaterial.EnableKeyword("_EMISSION");sparkMaterial.SetColor("_EmissionColor",new Color(2,1,.1f));materials.Add(sparkMaterial);
            for(int i=0;i<10;i++)
            {
                // Start tiny, then move outward and shrink. No full-size first frame,
                // growing smoke shells or solid geometry obscuring the body fragments.
                var spark=new GameObject("Blast spark");spark.layer=2;
                spark.AddComponent<MeshFilter>().sharedMesh=sparkMesh;spark.AddComponent<MeshRenderer>().sharedMaterial=sparkMaterial;
                spark.transform.SetParent(root.transform,false);spark.transform.localPosition=Vector3.up*.12f;
                spark.transform.localScale=new Vector3(.025f,.08f,.025f);sparks.Add(spark.transform);
                var velocity=new Vector3(Random(-3,3),Random(1,4),Random(-2,2));sparkVelocities.Add(velocity);
                spark.transform.rotation=Quaternion.FromToRotation(Vector3.up,velocity);
            }
            flash=root.AddComponent<Light>();flash.type=LightType.Point;flash.color=new Color(1,.55f,.16f);flash.range=6;flash.intensity=9;
            var output=root.AddComponent<AudioSource>();output.spatialBlend=.15f;output.volume=.95f;
            HasBlastSound=sound!=null;if(sound!=null)output.PlayOneShot(sound);
        }
        void Update()
        {
            if(root==null)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.04f);age+=dt;
            if(flash!=null)flash.intensity=9*Mathf.Clamp01(1-age/.12f);
            for(int i=0;i<sparks.Count;i++)
            {
                var spark=sparks[i];if(spark==null)continue;
                float life=.16f+i*.01f,t=age/life;
                spark.gameObject.SetActive(t<1);
                spark.localScale=new Vector3(.025f,.08f,.025f)*Mathf.Clamp01(1-t);
                spark.position+=sparkVelocities[i]*dt;
            }
            foreach(var piece in pieces)
            {
                if(piece.bounces>=3||age>8)continue;
                piece.velocity+=Vector3.down*(16*dt);Vector3 delta=piece.velocity*dt;
                if(delta.sqrMagnitude>0&&Physics.SphereCast(piece.part.position,.035f,delta.normalized,out var hit,delta.magnitude,~(1<<2),QueryTriggerInteraction.Ignore))
                {
                    piece.part.position=hit.point+hit.normal*.045f;piece.velocity=Vector3.Reflect(piece.velocity,hit.normal)*.22f;piece.spin*=.35f;piece.bounces++;
                }
                else piece.part.position+=delta;
                piece.part.Rotate(piece.spin*dt,Space.World);
            }
        }
        public void ResetExplosion()
        {
            if(originals!=null)for(int i=0;i<originals.Length;i++)if(originals[i]!=null)originals[i].enabled=visible[i];
            if(root!=null)Destroy(root);root=null;
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(var material in materials)if(material!=null)Destroy(material);
            meshes.Clear();materials.Clear();pieces.Clear();sparks.Clear();sparkVelocities.Clear();originals=null;HasBlastSound=false;
        }
        void OnDestroy()=>ResetExplosion();
    }
}

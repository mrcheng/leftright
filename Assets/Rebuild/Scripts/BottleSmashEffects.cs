using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Bounded glass/vodka spray and head blood owned by one playback.
    /// Cosmetic RNG never alters gameplay cues or the next victory selection.</summary>
    public sealed class BottleSmashEffects : MonoBehaviour
    {
        sealed class Piece {public Transform part;public Vector3 velocity,spin;public bool glass;}
        readonly List<Piece> pieces=new List<Piece>();readonly List<Mesh> meshes=new List<Mesh>();
        readonly System.Random random=new System.Random();
        Vector3 stage;float age;GameObject wound;
        public BloodSplash Blood {get;private set;}
        public int GlassCount=>14;
        public int VodkaCount=>18;
        float Roll(float a,float b)=>Mathf.Lerp(a,b,(float)random.NextDouble());
        public void Initialize(Vector3 hit,Transform head,Vector3 side,Vector3 floor,Material glass,Material vodka,Material blood,Vector3 woundNormal)
        {
            stage=floor;
            var shard=new Mesh{name="Bottle glass shard",vertices=new[]{Vector3.zero,new Vector3(.07f,.11f,.015f),new Vector3(-.045f,.065f,0)},triangles=new[]{0,1,2}};shard.RecalculateNormals();meshes.Add(shard);
            var drop=new Mesh{name="Low poly vodka droplet",vertices=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back},triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2}};drop.RecalculateNormals();meshes.Add(drop);
            for(int i=0;i<GlassCount+VodkaCount;i++)
            {
                bool isGlass=i<GlassCount;var go=new GameObject(isGlass?"Flying bottle glass":"Vodka splash droplet");go.transform.SetParent(transform,false);go.transform.position=hit;
                go.AddComponent<MeshFilter>().sharedMesh=isGlass?shard:drop;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=isGlass?glass:vodka;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale=isGlass?Vector3.one*Roll(.65f,1.4f):new Vector3(.022f,.045f,.022f)*Roll(.7f,1.2f);
                pieces.Add(new Piece{part=go.transform,glass=isGlass,velocity=side*Roll(.6f,2.5f)+new Vector3(Roll(-1,1),Roll(.3f,2.4f),Roll(-1.7f,1)),spin=new Vector3(Roll(-260,260),Roll(-260,260),Roll(-260,260))});
            }
            var catcher=new GameObject("Bottle splash platform catch surface");catcher.transform.SetParent(transform,false);catcher.transform.position=floor-Vector3.up*.05f;catcher.AddComponent<BoxCollider>().size=new Vector3(5,.1f,2.2f);
            if(blood!=null)
            {
                Blood=BloodSplash.Spawn(floor+Vector3.up*.012f,blood,transform,.6f);Blood.AimBurst(hit,side-Vector3.forward*.3f);Blood.laneHalfWidth=1;Blood.spreadSeconds=2;
                wound=new GameObject("Bottle cut on instructor head");wound.transform.position=hit;wound.transform.rotation=Quaternion.LookRotation(woundNormal);wound.transform.SetParent(head,true);
                var cut=new Mesh{name="Head cut splatter",vertices=new[]{new Vector3(-.04f,-.02f,0),new Vector3(.025f,-.045f,0),new Vector3(.05f,.035f,0),new Vector3(-.03f,.025f,0)},triangles=new[]{0,1,2,0,2,3}};cut.RecalculateNormals();meshes.Add(cut);
                wound.AddComponent<MeshFilter>().sharedMesh=cut;wound.AddComponent<MeshRenderer>().sharedMaterial=blood;
            }
        }
        void Update()
        {
            float dt=Mathf.Min(.05f,Time.unscaledDeltaTime);age+=dt;
            foreach(var p in pieces)
            {
                if(!p.part.gameObject.activeSelf)continue;
                p.velocity+=Vector3.down*(9.81f*dt);p.part.position+=p.velocity*dt;p.part.Rotate(p.spin*dt,Space.World);
                float floor=Mathf.Abs(p.part.position.x-stage.x)<2.5f&&Mathf.Abs(p.part.position.z-stage.z)<1.1f?stage.y:.015f;
                if(p.part.position.y<floor+.012f)
                {
                    if(!p.glass){p.part.gameObject.SetActive(false);continue;}
                    var pos=p.part.position;pos.y=floor+.012f;p.part.position=pos;p.velocity=Vector3.zero;p.spin=Vector3.zero;
                }
                if(age>(p.glass?3.5f:1.5f))p.part.gameObject.SetActive(false);
            }
        }
        void OnDisable(){if(wound!=null)wound.SetActive(false);}
        void OnDestroy(){if(wound!=null)Destroy(wound);foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}

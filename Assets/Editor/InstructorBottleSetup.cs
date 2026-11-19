using System.Collections.Generic;
using System.Linq;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LeftRight.Editor
{
    /// <summary>Builds the low-poly bottle as a saved, visible environment prop and
    /// registers a standalone victory asset. Safe to rerun without duplicate props.</summary>
    public static class InstructorBottleSetup
    {
        const string Folder="Assets/Rebuild/Victories/";
        [MenuItem("LeftRight/Rebuild/Install instructor bottle victory")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
            var glass=Material("VodkaGlass",new Color(.22f,.48f,.3f,.35f),true);
            var liquid=Material("VodkaLiquid",new Color(.78f,.89f,.92f,.55f),true);
            var paper=Material("VodkaLabel",new Color(.86f,.83f,.63f),false);var ink=Material("VodkaInk",new Color(.035f,.045f,.025f),false);
            var body=SaveMesh("VodkaBottleBody",Rings(new[]{0f,.035f,.35f,.44f},new[]{.105f,.12f,.12f,.05f},false));
            var neck=SaveMesh("VodkaBottleNeck",Rings(new[]{.44f,.60f,.63f},new[]{.05f,.05f,.062f},false));
            // Half of the main body's volume, not liquid extending into the neck.
            var fill=SaveMesh("VodkaHalfFill",Rings(new[]{.025f,.215f},new[]{.104f,.104f},true));
            var label=SaveMesh("VodkaLabelPaper",Quad(-.08f,.08f,.12f,.29f,-.122f));
            var letters=SaveMesh("VodkaLabelLetters",Label());
            var bottle=new GameObject("Instructor vodka bottle");var component=bottle.AddComponent<OfficerVodkaBottle>();
            component.glassMaterial=glass;component.liquidMaterial=liquid;
            var intact=new GameObject("Intact half-empty bottle");intact.transform.SetParent(bottle.transform,false);component.intactBody=intact;
            Part(intact.transform,"Green glass body",body,glass);Part(intact.transform,"Half-full vodka",fill,liquid);
            Part(intact.transform,"Vodka paper label",label,paper);Part(intact.transform,"VODKA lettering",letters,ink);
            Part(bottle.transform,"Bottle neck grip",neck,glass);
            const string bottlePath=Folder+"OfficerVodkaBottle.prefab";PrefabUtility.SaveAsPrefabAsset(bottle,bottlePath);Object.DestroyImmediate(bottle);
            const string sequencePath=Folder+"InstructorBottleSmash.asset";
            var sequence=AssetDatabase.LoadAssetAtPath<InstructorBottleVictory>(sequencePath);
            if(sequence==null){sequence=ScriptableObject.CreateInstance<InstructorBottleVictory>();AssetDatabase.CreateAsset(sequence,sequencePath);}
            sequence.glassHit=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Rebuild/Audio/Resources/VodkaBottleSmash.wav");
            sequence.bloodMaterial=AssetDatabase.LoadAssetAtPath<InstructorSelfShotVictory>(Folder+"InstructorSelfShot.asset").bloodMaterial;EditorUtility.SetDirty(sequence);
            const string environmentPath="Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";var environment=PrefabUtility.LoadPrefabContents(environmentPath);
            try
            {
                var old=environment.GetComponentInChildren<OfficerVodkaBottle>(true);if(old!=null)Object.DestroyImmediate(old.gameObject);
                var actor=environment.GetComponentInChildren<OfficerDirections>().transform;
                var arm=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="UpperArm.R");
                var side=Vector3.ProjectOnPlane(arm.position-actor.position,Vector3.up).normalized;
                var prop=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(bottlePath),environment.transform);
                prop.name="Instructor vodka bottle";prop.transform.position=actor.position+side*.78f-actor.forward*.15f;
                var director=environment.GetComponent<VictoryDirector>();director.sequences=(director.sequences??new VictorySequence[0]).Concat(new VictorySequence[]{sequence}).Distinct().ToArray();
                PrefabUtility.SaveAsPrefabAsset(environment,environmentPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(environment);}
            AssetDatabase.SaveAssets();
        }
        static Material Material(string name,Color color,bool transparent)
        {
            string path=Folder+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",transparent?.92f:.12f);
            if(transparent)
            {material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;}
            EditorUtility.SetDirty(material);return material;
        }
        static void Part(Transform parent,string name,Mesh mesh,Material material)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;}
        static Mesh SaveMesh(string name,Mesh mesh)
        {
            mesh.name=name;string path=Folder+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);return saved;
        }
        static Mesh Rings(float[] height,float[] radius,bool cap)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();const int sides=8;
            void Triangle(Vector3 a,Vector3 b,Vector3 c){int index=vertices.Count;vertices.AddRange(new[]{a,b,c});triangles.AddRange(new[]{index,index+1,index+2});}
            Vector3 At(int ring,int side){float angle=side*Mathf.PI*2/sides+Mathf.PI/8;return new Vector3(Mathf.Cos(angle)*radius[ring],height[ring],Mathf.Sin(angle)*radius[ring]);}
            for(int ring=0;ring<height.Length-1;ring++)for(int side=0;side<sides;side++)
            {int next=(side+1)%sides;Triangle(At(ring,side),At(ring+1,side),At(ring+1,next));Triangle(At(ring,side),At(ring+1,next),At(ring,next));}
            if(cap)for(int side=0;side<sides;side++)Triangle(Vector3.up*height[height.Length-1],At(height.Length-1,(side+1)%sides),At(height.Length-1,side));
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh Quad(float left,float right,float bottom,float top,float z)
        {var mesh=new Mesh{vertices=new[]{new Vector3(left,bottom,z),new Vector3(left,top,z),new Vector3(right,top,z),new Vector3(right,bottom,z)},triangles=new[]{0,1,2,0,2,3}};mesh.RecalculateNormals();return mesh;}
        static Mesh Label()
        {
            string[] glyphs={"101101101101010","111101101101111","110101101101110","101110100110101","010101111101101"};
            var vertices=new List<Vector3>();var triangles=new List<int>();float pixel=.007f;
            for(int g=0;g<glyphs.Length;g++)for(int y=0;y<5;y++)for(int x=0;x<3;x++)if(glyphs[g][y*3+x]=='1')
            {var quad=Quad((g*4+x-9.5f)*pixel,(g*4+x-8.5f)*pixel,.24f-y*pixel,.247f-y*pixel,-.124f);int offset=vertices.Count;vertices.AddRange(quad.vertices);triangles.AddRange(quad.triangles.Select(i=>i+offset));Object.DestroyImmediate(quad);}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();return mesh;
        }
    }
}

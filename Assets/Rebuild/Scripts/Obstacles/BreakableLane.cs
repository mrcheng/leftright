using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Separates the actual walkway's disconnected wooden boards, keeping
    /// their palette UVs/materials. The imported source stays intact for other modes.</summary>
    public sealed class BreakableLane : IDisposable
    {
        public sealed class Board
        {
            public Transform transform;
            public Renderer renderer;
            public Bounds bounds;
            public bool broken;
        }
        public readonly List<Board> Boards=new List<Board>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly Renderer original;
        readonly GameObject root;
        readonly bool originallyEnabled;
        public BreakableLane(Transform lane)
        {
            var filter=lane.GetComponentInChildren<MeshFilter>();
            original=filter.GetComponent<Renderer>();originallyEnabled=original.enabled;
            root=new GameObject("Breakable Survival deck");root.transform.SetParent(filter.transform,false);
            var source=filter.sharedMesh;var vertices=source.vertices;var normals=source.normals;var uv=source.uv;
            // FBX hard edges duplicate vertices. Weld positions for connectivity only;
            // retain the original vertices when extracting so normals/UVs stay sharp.
            int[] parent=Enumerable.Range(0,vertices.Length).ToArray();
            int Find(int n){while(parent[n]!=n){parent[n]=parent[parent[n]];n=parent[n];}return n;}
            void Join(int a,int b){parent[Find(a)]=Find(b);}
            var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var key=Vector3Int.RoundToInt(vertices[i]*1000000);
                if(welded.TryGetValue(key,out int other))Join(i,other);else welded[key]=i;
            }
            var triangles=Enumerable.Range(0,source.subMeshCount).Select(source.GetTriangles).ToArray();
            foreach(var sub in triangles)for(int i=0;i<sub.Length;i+=3){Join(sub[i],sub[i+1]);Join(sub[i],sub[i+2]);}
            var groups=new Dictionary<int,List<int>[]>();
            for(int s=0;s<triangles.Length;s++)for(int i=0;i<triangles[s].Length;i+=3)
            {
                int key=Find(triangles[s][i]);
                if(!groups.TryGetValue(key,out var lists))groups[key]=lists=Enumerable.Range(0,triangles.Length).Select(_=>new List<int>()).ToArray();
                lists[s].AddRange(new[]{triangles[s][i],triangles[s][i+1],triangles[s][i+2]});
            }
            foreach(var group in groups.Values)
            {
                int[] indices=group.SelectMany(t=>t).Distinct().ToArray();var map=new Dictionary<int,int>();
                var bounds=new Bounds(vertices[indices[0]],Vector3.zero);foreach(int i in indices)bounds.Encapsulate(vertices[i]);
                for(int i=0;i<indices.Length;i++)map[indices[i]]=i;
                var mesh=new Mesh{name="Survival walkway piece"};meshes.Add(mesh);
                mesh.vertices=indices.Select(i=>vertices[i]-bounds.center).ToArray();
                if(normals.Length==vertices.Length)mesh.normals=indices.Select(i=>normals[i]).ToArray();
                if(uv.Length==vertices.Length)mesh.uv=indices.Select(i=>uv[i]).ToArray();
                mesh.subMeshCount=group.Length;
                for(int s=0;s<group.Length;s++)mesh.SetTriangles(group[s].Select(i=>map[i]).ToArray(),s);
                mesh.RecalculateBounds();
                var part=new GameObject("Walkway frame piece");part.transform.SetParent(root.transform,false);part.transform.localPosition=bounds.center;
                part.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterials=original.sharedMaterials;
                var world=renderer.bounds;
                if(world.size.x>.1f&&world.size.x<.8f&&world.size.z>.7f&&world.size.y<.3f)
                {part.name="Breakable plank";Boards.Add(new Board{transform=part.transform,renderer=renderer,bounds=world});}
            }
            Boards.Sort((a,b)=>a.bounds.center.x.CompareTo(b.bounds.center.x));
            if(Boards.Count<2){Dispose();throw new InvalidOperationException("Walkway must contain separate plank geometry.");}
            original.enabled=false;
        }
        public void Dispose()
        {
            if(original!=null)original.enabled=originallyEnabled;
            if(root!=null){root.SetActive(false);UnityEngine.Object.Destroy(root);}
            foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);
        }
    }

    /// <summary>Small ballistic board debris with a ground contact/bounce, without
    /// adding permanent rigidbody/collider work to the playable lane.</summary>
    public sealed class FallingLanePlank : MonoBehaviour
    {
        Vector3 velocity,spin;float age;Renderer shape;
        public void Initialize(System.Random random)
        {shape=GetComponent<Renderer>();velocity=new Vector3((float)random.NextDouble()*.5f-.25f,-.4f,0);spin=new Vector3(80+(float)random.NextDouble()*100,15,40);}
        void Update()
        {
            float dt=Mathf.Min(.05f,Time.unscaledDeltaTime);age+=dt;velocity.y-=9.81f*dt;
            transform.position+=velocity*dt;transform.Rotate(spin*dt,Space.World);
            if(shape.bounds.min.y<.015f)
            {
                transform.position+=Vector3.up*(.015f-shape.bounds.min.y);
                velocity.y=Mathf.Abs(velocity.y)*.16f;velocity.x*=.4f;spin*=.3f;
                if(age>.8f)enabled=false;
            }
        }
    }
}

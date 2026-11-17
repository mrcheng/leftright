using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();
void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var round=UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>();
var officer=round.officer;var march=round.march;
var skin=officer.GetComponentInChildren<SkinnedMeshRenderer>();
round.CancelStart();
var bones=officer.GetComponentsInChildren<Transform>();
Transform Bone(string n)=>bones.Single(t=>t.name==n);
try
{
 Check(skin!=null && skin.sharedMesh.triangles.Length/3<8000,"Instructor uses a low-poly skinned soldier in the recruits' style");
 var model=officer.transform.Find("Instructor model - 125 percent recruit scale");
 var recruit=UnityEngine.Object.FindObjectsByType<RecruitMarch>().First(r=>r.role==RecruitRole.Sniper);
 Check(Mathf.Abs(model.localScale.x/recruit.transform.Find("Model orientation").localScale.x-1.25f)<.001f,"Instructor is 25 percent larger than the standard recruit");
 Check(skin.sharedMaterials.Any(m=>m.name=="Recruit_Instructor_Uniform" && m.GetColor("_BaseColor").maxColorComponent<.08f),"Instructor wears a black tunic");
 Check(bones.Any(t=>t.name=="Black peaked officer cap") && bones.Any(t=>t.name=="Square belt buckle"),"Instructor has distinct officer headgear and belt");
 Check(skin.bones.All(b=>b!=null) && skin.sharedMaterials.All(m=>m!=null),"Instructor rig and materials are complete");
 officer.ActivateDirections(true);
 float leftTimeout=Time.realtimeSinceStartup+6;
 while(officer.DirectionRight && Time.realtimeSinceStartup<leftTimeout)await Task.Delay(20);
 await Task.Delay(400);
 Vector3 delta=Bone("Fist.L").position-officer.leftShoulder.position;
 Check(Vector3.Dot(delta.normalized,Vector3.left)>.98f,"New instructor's left hand points horizontally left");
 float until=Time.realtimeSinceStartup+6;
 while(!officer.DirectionRight && Time.realtimeSinceStartup<until)await Task.Delay(20);
 await Task.Delay(400);
 delta=Bone("Fist.R").position-officer.rightShoulder.position;
 Check(officer.DirectionRight && Vector3.Dot(delta.normalized,Vector3.right)>.98f,"New instructor's right hand points horizontally right");
 round.BeginRound();
 while(!round.HasStarted)await Task.Delay(10);
 bool highLeft=false,highRight=false,planted=true;
 var mesh=new Mesh();
 try
 {
  until=Time.realtimeSinceStartup+2.5f;
  while(Time.realtimeSinceStartup<until)
  {
   await Task.Delay(20);
   highLeft|=Quaternion.Angle(march.leftHip.localRotation,Quaternion.identity)>75;
   highRight|=Quaternion.Angle(march.rightHip.localRotation,Quaternion.identity)>75;
   skin.BakeMesh(mesh);
   float sole=mesh.vertices.Min(v=>(skin.transform.position+skin.transform.rotation*v).y);
   planted&=sole>.36f && sole<.48f;
  }
 }finally{UnityEngine.Object.DestroyImmediate(mesh);}
 Check(highLeft&&highRight,"Instructor retains alternating high-knee marching");
 Check(planted,"New instructor keeps a supporting boot planted on the command platform");
 Check(Math.Abs(march.BeatPosition-round.masterBeat.BeatPosition)<.2,"Instructor marches on the course master beat");
}
finally{round.BeginRound();}
return new{checksPassed=checks.Count,checks};

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
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();
var players=course.players;var round=course.round;
var speeds=players.Select(p=>p.walkSpeed).ToArray();
round.CancelStart();
var skins=players.Select(p=>p.GetComponentInChildren<SkinnedMeshRenderer>()).ToArray();
var feet=skins.Select(s=>new[]{s.bones.Single(b=>b.name=="Foot.L"),s.bones.Single(b=>b.name=="Foot.R")}).ToArray();
var baseline=feet.Select(pair=>pair.Select(f=>f.position.y).ToArray()).ToArray();
try
{
 for(int i=0;i<players.Length;i++)
 {
  players[i].InputOverride=0;players[i].walkSpeed=0;
  Check(feet[i][0].IsChildOf(players[i].march.leftKnee)&&feet[i][1].IsChildOf(players[i].march.rightKnee),players[i].march.role+" feet inherit the lower-leg motion");
 }
 round.BeginRound();
 while(!round.HasStarted)await Task.Delay(10);
 bool[] leftHigh=new bool[4],rightHigh=new bool[4];
 bool attached=true,grounded=true;
 var mesh=new Mesh();
 try
 {
  float until=Time.realtimeSinceStartup+2.2f;
  while(Time.realtimeSinceStartup<until)
  {
   await Task.Delay(20);
   for(int i=0;i<4;i++)
   {
    var s=skins[i];s.BakeMesh(mesh);var vertices=mesh.vertices;var weights=s.sharedMesh.boneWeights;
    leftHigh[i]|=feet[i][0].position.y-baseline[i][0]>.20f;
    rightHigh[i]|=feet[i][1].position.y-baseline[i][1]>.20f;
    grounded&=Mathf.Min(feet[i][0].position.y-baseline[i][0],feet[i][1].position.y-baseline[i][1])<.02f;
    foreach(var f in feet[i])
    {
     int index=Array.IndexOf(s.bones,f);
     // Boot vertices must remain near their actual moving foot bone, not the floor.
     for(int v=0;v<vertices.Length;v++)
      if(weights[v].boneIndex0==index && weights[v].weight0>.999f)
       attached&=Vector3.Distance(s.transform.position+s.transform.rotation*vertices[v],f.position)<.42f;
    }
   }
  }
 }finally{UnityEngine.Object.DestroyImmediate(mesh);}
 Check(leftHigh.All(v=>v)&&rightHigh.All(v=>v),"Every left and right boot lifts with the high-knee march");
 Check(attached,"Skinned boot geometry stays attached to its moving foot bone");
 Check(grounded,"The supporting foot stays planted while the other boot lifts");
 var instructor=round.officer.GetComponentInChildren<SkinnedMeshRenderer>();
 Check(instructor.bones.Where(b=>b.name.StartsWith("Foot.")).All(b=>b.parent.name.StartsWith("LowerLeg.")),"Instructor feet use the corrected hierarchy too");
}
finally
{
 for(int i=0;i<players.Length;i++){players[i].walkSpeed=speeds[i];players[i].InputOverride=null;}
 round.BeginRound();
}
return new{checksPassed=checks.Count,checks};

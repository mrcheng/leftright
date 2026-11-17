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
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var round=course.round;
var players=course.players;var speeds=players.Select(p=>p.walkSpeed).ToArray();
round.CancelStart();
var torsos=players.Select(p=>p.GetComponentsInChildren<Transform>().Single(t=>t.name=="Abdomen")).ToArray();
var feet=players.Select(p=>p.GetComponentsInChildren<Transform>().Single(t=>t.name=="Foot.L")).ToArray();
var torsoRest=torsos.Select(t=>t.localPosition).ToArray();
try
{
 foreach(var p in players){p.walkSpeed=0;p.InputOverride=1;}
 round.BeginRound();await Task.Delay(300);
 var min=Enumerable.Repeat(float.MaxValue,4).ToArray();var max=Enumerable.Repeat(float.MinValue,4).ToArray();
 bool grounded=true,backward=false;
 var mesh=new Mesh();
 try
 {
  float until=Time.realtimeSinceStartup+1.6f;
  while(Time.realtimeSinceStartup<until)
  {
   await Task.Delay(20);
   for(int i=0;i<4;i++)
   {
    float displacement=torsos[i].parent.TransformVector(torsos[i].localPosition-torsoRest[i]).y;
    min[i]=Mathf.Min(min[i],displacement);max[i]=Mathf.Max(max[i],displacement);
    var m=players[i].march;
    // Compare world knee position to hip along the model's forward direction.
    backward|=Vector3.Dot((m.leftKnee.position-m.leftHip.position).normalized,-players[i].transform.forward)<-.2f;
    var skin=players[i].GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(mesh);
    float sole=mesh.vertices.Min(v=>(skin.transform.position+skin.transform.rotation*v).y);
    grounded&=sole>.61f&&sole<.72f;
   }
  }
 }finally{UnityEngine.Object.DestroyImmediate(mesh);}
 Check(max.Select((v,i)=>v-min[i]>.035f).All(v=>v),"Every recruit's torso visibly rises and falls while walking");
 Check(backward,"Supporting legs push back instead of hanging vertically beneath the body");
 Check(grounded,"Walking boot soles remain on the platform throughout the stride");
 Check(players.All(p=>Mathf.Abs(p.transform.localPosition.y-.671566f)<.01f),"Visual height correction does not change gameplay position");
 // Use the standard pause check after audible music starts.
 round.BeginRound();while(!round.HasStarted)await Task.Delay(10);
 round.audioOutput.music.Pause();await Task.Delay(60);
 var held=torsos.Select(t=>t.localPosition).ToArray();await Task.Delay(150);
 Check(torsos.Select((t,i)=>t.localPosition==held[i]).All(v=>v),"Body bob freezes with paused music");
 round.CancelStart();
 Check(torsos.Select((t,i)=>Vector3.Distance(t.localPosition,torsoRest[i])<.0001f).All(v=>v),"Reset restores the neutral torso pose");
}
finally{for(int i=0;i<4;i++){players[i].walkSpeed=speeds[i];players[i].InputOverride=null;}round.BeginRound();}
return new{checksPassed=checks.Count,checks};

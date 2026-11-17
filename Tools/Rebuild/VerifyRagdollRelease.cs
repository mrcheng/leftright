using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var round=course.round;
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
var slowAngles=new float[4];var impactTimes=new float[4];var snapStarts=new float[4];
try
{
 foreach(var p in course.players){p.walkSpeed=0;p.InputOverride=0;}
 round.BeginRound();while(!round.HasStarted)await Task.Delay(10);
 for(int i=0;i<4;i++)
 {
  var trap=course.traps[i];var player=course.players[i];
  player.PinTo(i%2==0?trap.leftBank:trap.rightBank,i%2==0,trap.spikeReach);
  Check(player.IsPinned&&!player.ragdoll.IsActive,player.march.role+" stays pinned before full closure");
  for(int step=0;step<30;step++)trap.MissBeat();
 }
 float until=Time.realtimeSinceStartup+7;
 while(!course.players.All(p=>p.ragdoll.IsActive)&&Time.realtimeSinceStartup<until)await Task.Delay(5);
 Check(course.players.All(p=>p.ragdoll.IsActive)&&course.traps.All(t=>!t.HasReturned&&t.TargetTravel==0),"Slow falling starts immediately with spike retraction, without a hold");
 var earlyNodes=(Vector3[])typeof(RecruitRagdoll).GetField("nodes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(course.players[0].ragdoll);
 Check(Vector3.Angle(earlyNodes[earlyNodes.Length-1]-earlyNodes[0],Vector3.up)<15,"Body begins upright instead of dropping flat at release");
 var roots=course.players.Select(p=>GameObject.Find(p.name+" flattened ragdoll physics")).ToArray();
 var parts=roots.Select(b=>b.GetComponentsInChildren<MeshFilter>()).ToArray();
 var meshes=parts.Select(a=>a.Select(f=>f.sharedMesh).ToArray()).ToArray();
 var nodeField=typeof(RecruitRagdoll).GetField("nodes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var lengthField=typeof(RecruitRagdoll).GetField("stripHeight",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var bounceField=typeof(RecruitRagdoll).GetField("hasBounced",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var previousField=typeof(RecruitRagdoll).GetField("previousNodes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var stepField=typeof(RecruitRagdoll).GetField("previousStep",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 var ageField=typeof(RecruitRagdoll).GetField("fallAge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
 float initialStep=(float)stepField.GetValue(course.players[0].ragdoll);
 var rebounds=new bool[4];
 bool[] bent=new bool[4];
 for(int sample=0;sample<120;sample++)
 {
  for(int lane=0;lane<4;lane++)
  {
   var nodes=(Vector3[])nodeField.GetValue(course.players[lane].ragdoll);
   var previous=(Vector3[])previousField.GetValue(course.players[lane].ragdoll);
   var bounced=(bool[])bounceField.GetValue(course.players[lane].ragdoll);
   float age=(float)ageField.GetValue(course.players[lane].ragdoll);
   if(age>=2&&slowAngles[lane]==0)slowAngles[lane]=Vector3.Angle(nodes[nodes.Length-1]-nodes[0],Vector3.up);
   if(snapStarts[lane]==0&&Vector3.Angle(nodes[nodes.Length-1]-nodes[0],Vector3.up)>=45)snapStarts[lane]=age;
   if(bounced[bounced.Length-1]&&impactTimes[lane]==0)impactTimes[lane]=age;
   for(int i=1;i<nodes.Length;i++)rebounds[lane]|=bounced[i]&&nodes[i].y>previous[i].y+.0001f;
   float length=(float)lengthField.GetValue(course.players[lane].ragdoll);
   float floor=course.traps[lane].transform.position.y+course.players[lane].ragdoll.settledThickness*.5f+.003f;
   if(Mathf.Abs(nodes[0].y-floor)>.001f)throw new Exception("Feet lifted off the lane during the fall");
   if(nodes.Any(n=>n.y>floor+length*(nodes.Length-1)+.025f))throw new Exception("Body jumped above its initial standing height");
   for(int i=0;i<nodes.Length-1;i++)
   {
    if(float.IsNaN(nodes[i].x)||float.IsInfinity(nodes[i].y))throw new Exception("Non-finite cloth simulation");
    if(Mathf.Abs(Vector3.Distance(nodes[i],nodes[i+1])-length)>.025f)throw new Exception("Cloth stretched its silhouette");
    if(i>0&&Vector3.Angle(nodes[i]-nodes[i-1],nodes[i+1]-nodes[i])>5) bent[lane]=true;
   }
   for(int part=0;part<parts[lane].Length;part++)
    if(parts[lane][part].sharedMesh!=meshes[lane][part])throw new Exception("Mesh swapped during fall");
  }
  await Task.Delay(50);
 }
 Check(true,"Continuous cloth simulation stays finite, preserves strip lengths and never swaps meshes");
 Check(true,"Feet stay grounded throughout the fall and the body never jumps above its standing height");
 Check((float)stepField.GetValue(course.players[0].ragdoll)>initialStep*3,"Fall accelerates from its slow upright start");
 Check(rebounds.All(value=>value),"All four recruits show a small upward rebound after ground impact");
 Check(slowAngles.All(angle=>angle>5&&angle<45),"All four recruits are still slowly tipping at two seconds");
 Check(impactTimes.All(time=>time>2.5f&&time<3.5f),"All four recruits snap into ground impact during the final second around three seconds");
 Check(impactTimes.Select((time,lane)=>time-snapStarts[lane]).All(duration=>duration>=0&&duration<.22f),"The final upper-body/head collapse completes in roughly a tenth of a second");
 for(int lane=0;lane<4;lane++)
 {
  var player=course.players[lane];var trap=course.traps[lane];
  var renderers=roots[lane].GetComponentsInChildren<MeshRenderer>();float bottom=renderers.Min(r=>r.bounds.min.y),top=renderers.Max(r=>r.bounds.max.y);
  Check(bent[lane],player.march.role+" visibly flexes during the fall");
  Check(player.ragdoll.IsSettled&&top-bottom<.08f,player.march.role+" settles as a thin layer including head, boots and equipment");
  Check(bottom>=trap.transform.position.y-.005f&&bottom<trap.transform.position.y+.06f,player.march.role+" rests directly on the planks");
  var nodes=(Vector3[])nodeField.GetValue(player.ragdoll);
  Check((nodes[nodes.Length-1].x-nodes[0].x)*(player.ragdoll.FallDirectionRight?1:-1)>.5f,player.march.role+" lands in its randomly chosen fall direction");
 }
 Check(course.IsGameOver&&course.traps.All(t=>t.HasReturned),"All four deaths finish and pillars return after game over");
 round.BeginRound();await Task.Delay(80);
 Check(course.players.All(p=>!p.ragdoll.IsActive&&!p.IsEliminated&&p.march.enabled&&p.GetComponentInChildren<SkinnedMeshRenderer>().enabled),"Restart restores every original character renderer and animation");
 Check(UnityEngine.Object.FindObjectsByType<Transform>().All(b=>!b.name.Contains("flattened ragdoll physics")),"Restart removes all sheet physics");
 Check(UnityEngine.Object.FindObjectsByType<BloodSplash>().Length==0,"Restart removes the blood effects");
}
finally{for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}round.BeginRound();}
return new{checksPassed=checks.Count,checks,slowAngles,impactTimes,snapSeconds=impactTimes.Select((time,lane)=>time-snapStarts[lane]).ToArray()};

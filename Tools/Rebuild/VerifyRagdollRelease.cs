using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();var round=course.round;
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
var nodeField=typeof(RecruitRagdoll).GetField("nodes",BindingFlags.Instance|BindingFlags.NonPublic);
var lengthField=typeof(RecruitRagdoll).GetField("stripHeight",BindingFlags.Instance|BindingFlags.NonPublic);
var bounceField=typeof(RecruitRagdoll).GetField("hasBounced",BindingFlags.Instance|BindingFlags.NonPublic);
var previousField=typeof(RecruitRagdoll).GetField("previousNodes",BindingFlags.Instance|BindingFlags.NonPublic);
var stepField=typeof(RecruitRagdoll).GetField("previousStep",BindingFlags.Instance|BindingFlags.NonPublic);
var ageField=typeof(RecruitRagdoll).GetField("fallAge",BindingFlags.Instance|BindingFlags.NonPublic);
try
{
 foreach(var p in course.players){p.walkSpeed=0;p.InputOverride=0;}
 // Test two casualties per round. Killing all four together now deliberately reserves
 // a random winner, so it is no longer a valid setup for four simultaneous corpse tests.
 for(int batch=0;batch<2;batch++)
 {
  round.BeginRound();while(!round.HasStarted)await Task.Delay(10);
  var players=course.players.Skip(batch*2).Take(2).ToArray();var traps=course.traps.Skip(batch*2).Take(2).ToArray();
  for(int lane=0;lane<2;lane++){
   players[lane].PinTo(lane%2==0?traps[lane].leftBank:traps[lane].rightBank,lane%2==0,traps[lane].spikeReach);
   Check(players[lane].IsPinned&&!players[lane].ragdoll.IsActive,players[lane].march.role+" stays pinned before full closure");
   for(int i=0;i<30;i++)traps[lane].MissBeat();
  }
  float until=Time.realtimeSinceStartup+7;while(!players.All(p=>p.ragdoll.IsActive)&&Time.realtimeSinceStartup<until)await Task.Delay(5);
  Check(players.All(p=>p.ragdoll.IsActive)&&traps.All(t=>!t.HasReturned&&t.TargetTravel==0),"Batch "+batch+" starts falling immediately with retraction");
  var early=(Vector3[])nodeField.GetValue(players[0].ragdoll);
  Check(Vector3.Angle(early[early.Length-1]-early[0],Vector3.up)<15,"Batch "+batch+" starts upright");
  var roots=players.Select(p=>GameObject.Find(p.name+" flattened ragdoll physics")).ToArray();
  var parts=roots.Select(b=>b.GetComponentsInChildren<MeshFilter>()).ToArray();var meshes=parts.Select(a=>a.Select(f=>f.sharedMesh).ToArray()).ToArray();
  var bent=new bool[2];var rebounds=new bool[2];var slowAngles=new float[2];var impacts=new float[2];var snaps=new float[2];
  float initialStep=(float)stepField.GetValue(players[0].ragdoll);
  for(int sample=0;sample<120;sample++){
   for(int lane=0;lane<2;lane++){
    var nodes=(Vector3[])nodeField.GetValue(players[lane].ragdoll);var previous=(Vector3[])previousField.GetValue(players[lane].ragdoll);var bounced=(bool[])bounceField.GetValue(players[lane].ragdoll);
    float age=(float)ageField.GetValue(players[lane].ragdoll);float length=(float)lengthField.GetValue(players[lane].ragdoll);
    float angle=Vector3.Angle(nodes[nodes.Length-1]-nodes[0],Vector3.up);
    if(age>=2&&slowAngles[lane]==0)slowAngles[lane]=angle;if(snaps[lane]==0&&angle>=45)snaps[lane]=age;if(bounced[bounced.Length-1]&&impacts[lane]==0)impacts[lane]=age;
    float floor=traps[lane].transform.position.y+players[lane].ragdoll.settledThickness*.5f+.003f;
    if(Mathf.Abs(nodes[0].y-floor)>.001f||nodes.Any(n=>n.y>floor+length*(nodes.Length-1)+.025f))throw new Exception("Corpse lifted from the lane");
    for(int i=1;i<nodes.Length;i++)rebounds[lane]|=bounced[i]&&nodes[i].y>previous[i].y+.0001f;
    for(int i=0;i<nodes.Length-1;i++){
     if(float.IsNaN(nodes[i].x)||float.IsInfinity(nodes[i].y)||Mathf.Abs(Vector3.Distance(nodes[i],nodes[i+1])-length)>.025f)throw new Exception("Unstable or stretched cloth");
     if(i>0&&Vector3.Angle(nodes[i]-nodes[i-1],nodes[i+1]-nodes[i])>5)bent[lane]=true;
    }
    for(int part=0;part<parts[lane].Length;part++)if(parts[lane][part].sharedMesh!=meshes[lane][part])throw new Exception("Mesh swapped during fall");
   }
   await Task.Delay(50);
  }
  Check(true,"Batch "+batch+" preserves grounded feet, strip lengths and mesh identity");
  Check((float)stepField.GetValue(players[0].ragdoll)>initialStep*3,"Batch "+batch+" accelerates from its slow start");
  Check(rebounds.All(v=>v),"Batch "+batch+" has impact rebounds");
  Check(slowAngles.All(a=>a>5&&a<45),"Batch "+batch+" is slowly tipping at two seconds");
  Check(impacts.All(t=>t>2.5f&&t<3.5f)&&impacts.Select((t,lane)=>t-snaps[lane]).All(t=>t>=0&&t<.22f),"Batch "+batch+" completes its final head snap in roughly a tenth of a second");
  for(int lane=0;lane<2;lane++){
   var renderers=roots[lane].GetComponentsInChildren<MeshRenderer>();float bottom=renderers.Min(r=>r.bounds.min.y),top=renderers.Max(r=>r.bounds.max.y);
   Check(bent[lane]&&players[lane].ragdoll.IsSettled&&top-bottom<.08f,players[lane].march.role+" flexes then settles as a thin layer");
   Check(bottom>=traps[lane].transform.position.y-.005f&&bottom<traps[lane].transform.position.y+.06f,players[lane].march.role+" rests on the planks");
   var nodes=(Vector3[])nodeField.GetValue(players[lane].ragdoll);
   Check((nodes[nodes.Length-1].x-nodes[0].x)*(players[lane].ragdoll.FallDirectionRight?1:-1)>.5f,players[lane].march.role+" lands in its chosen direction");
  }
  Check(traps.All(t=>t.HasReturned),"Batch "+batch+" returns its spike banks");
  round.BeginRound();await Task.Delay(80);
  Check(course.players.All(p=>!p.ragdoll.IsActive&&!p.IsEliminated&&p.march.enabled&&p.GetComponentInChildren<SkinnedMeshRenderer>().enabled),"Batch "+batch+" resets every rig and renderer");
  Check(UnityEngine.Object.FindObjectsByType<Transform>().All(t=>!t.name.Contains("flattened ragdoll physics"))&&UnityEngine.Object.FindObjectsByType<BloodSplash>().Length==0,"Batch "+batch+" clears corpse meshes and blood");
 }
}
finally{for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}round.BeginRound();}
return new{checksPassed=checks.Count,checks};

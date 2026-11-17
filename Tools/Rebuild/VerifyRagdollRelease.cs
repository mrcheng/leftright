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
 var bodies=course.players.Select(p=>UnityEngine.Object.FindObjectsByType<Rigidbody>().Single(b=>b.name==p.name+" flattened ragdoll physics")).ToArray();
 var parts=bodies.Select(b=>b.GetComponentsInChildren<MeshFilter>()).ToArray();
 var meshes=parts.Select(a=>a.Select(f=>f.sharedMesh).ToArray()).ToArray();
 var vertices=parts.Select(a=>a.Select(f=>f.sharedMesh.vertices).ToArray()).ToArray();
 Check(bodies.All(b=>b.GetComponent<BoxCollider>()!=null&&b.GetComponent<CharacterJoint>()==null),"Thin sheet colliders replace twisting limb proxies");
 Check(parts.All(a=>Mathf.Abs(a.Max(f=>f.sharedMesh.bounds.max.x)-a.Min(f=>f.sharedMesh.bounds.min.x)-.06f)<.002f),"Every body and accessory is already six centimetres thick at release");
 for(int sample=0;sample<7;sample++)
 {
  await Task.Delay(450);
  bool unchanged=true;
  for(int lane=0;lane<4;lane++)for(int part=0;part<parts[lane].Length;part++)
   unchanged &= parts[lane][part].sharedMesh==meshes[lane][part]&&parts[lane][part].sharedMesh.vertices.SequenceEqual(vertices[lane][part]);
  Check(unchanged,"Fall sample "+sample+" preserves geometry without a second flattening or head correction");
 }
 for(int lane=0;lane<4;lane++)
 {
  var player=course.players[lane];var trap=course.traps[lane];var body=bodies[lane];
  var renderers=body.GetComponentsInChildren<MeshRenderer>();float bottom=renderers.Min(r=>r.bounds.min.y),top=renderers.Max(r=>r.bounds.max.y);
  Check(player.ragdoll.IsSettled&&top-bottom<.075f,player.march.role+" lands as one thin layer including boots, head and equipment");
  Check(bottom>=trap.transform.position.y-.005f&&bottom<trap.transform.position.y+.06f,player.march.role+" rests directly on the planks");
  Check(Vector3.Dot(body.transform.right,Vector3.up)*(lane%2==0?1:-1)>.985f,player.march.role+" lands face-up from either spike side");
 }
 Check(course.IsGameOver&&course.traps.All(t=>t.HasReturned),"All four deaths finish and pillars return after game over");
 round.BeginRound();await Task.Delay(80);
 Check(course.players.All(p=>!p.ragdoll.IsActive&&!p.IsEliminated&&p.march.enabled&&p.GetComponentInChildren<SkinnedMeshRenderer>().enabled),"Restart restores every original character renderer and animation");
 Check(UnityEngine.Object.FindObjectsByType<Rigidbody>().All(b=>!b.name.Contains("flattened ragdoll physics")),"Restart removes all sheet physics");
 Check(UnityEngine.Object.FindObjectsByType<BloodSplash>().Length==0,"Restart removes the blood effects");
}
finally{for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}round.BeginRound();}
return new{checksPassed=checks.Count,checks};

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
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
try
{
 foreach(var p in course.players){p.walkSpeed=0;p.InputOverride=0;}
 round.BeginRound();while(!round.HasStarted)await Task.Delay(10);
 var player=course.players[0];var trap=course.traps[0];
 var position=player.transform.position;position.x=trap.LeftTip+player.collisionRadius-.01f;player.transform.position=position;
 await Task.Delay(80);
 Check(player.IsPinned&&!player.IsEliminated,"First hit still pins the recruit without releasing it");
 var first=trap.GetComponentsInChildren<BloodSplash>();
 Check(first.Length==1&&first[0].GetComponentsInChildren<MeshRenderer>().Length>5,"First spike hit produces its own smaller blood burst");
 Check(!player.ragdoll.IsActive,"Pinned recruit stays attached rather than ragdolling early");
 for(int i=0;i<30;i++)trap.MissBeat();
 float until=Time.realtimeSinceStartup+7;
 while(!player.ragdoll.IsActive&&Time.realtimeSinceStartup<until)await Task.Delay(10);
 Check(player.IsEliminated&&!player.IsPinned&&player.ragdoll.IsActive,"Full squeeze releases the flattened body into physics");
 Check(trap.GetComponentsInChildren<BloodSplash>().Length==2,"Final squeeze adds a second larger blood splash");
 var skin=player.GetComponentInChildren<SkinnedMeshRenderer>();var head=skin.bones.Single(b=>b.name=="Head");
 float headStart=head.position.y;
 var bodies=UnityEngine.Object.FindObjectsByType<Rigidbody>().Where(b=>b.transform.root.name.Contains("flattened ragdoll physics")).ToArray();
 Check(bodies.Length==11&&bodies.Count(b=>b.GetComponent<CharacterJoint>()!=null)==10,"Corpse has articulated physics for torso, head and four limbs");
 await Task.Delay(2200);
 Check(head.position.y<headStart-.6f,"Limp body falls down onto the walkway");
 Check(bodies.All(b=>float.IsFinite(b.position.x)&&float.IsFinite(b.position.y)&&b.position.y>-.1f&&b.position.y<3),"Ragdoll remains stable and collides with the walkway");
 Check(trap.HasReturned,"Spikes retract while the ragdoll remains behind");
 Check(player.transform.Find("Model orientation").localScale.z<.5f,"Corpse retains its squeezed proportions");
 Check(!player.IsMoving&&!player.march.enabled,"Death physics cannot restore movement or marching");
 round.BeginRound();await Task.Delay(80);
 Check(!player.ragdoll.IsActive&&!player.IsEliminated&&player.march.enabled,"Restart restores normal animation and disables death physics");
 Check(UnityEngine.Object.FindObjectsByType<Rigidbody>().All(b=>!b.transform.root.name.Contains("flattened ragdoll physics")),"Restart removes every physics proxy");
 Check(UnityEngine.Object.FindObjectsByType<BloodSplash>().Length==0,"Restart removes both first-hit and final-squeeze blood");
}
finally{for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}round.BeginRound();}
return new{checksPassed=checks.Count,checks};

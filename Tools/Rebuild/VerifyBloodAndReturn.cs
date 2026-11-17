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
 var trap=course.traps[0];var player=course.players[0];
 float leftStart=trap.leftBank.position.x,rightStart=trap.rightBank.position.x;
 Check(trap.bloodMaterial!=null,"Spike lane has its liquid material assigned");
 for(int i=0;i<30;i++)trap.MissBeat();
 float until=Time.realtimeSinceStartup+7;
 while(!trap.HasFinishedSqueeze&&Time.realtimeSinceStartup<until)await Task.Delay(10);
 Check(trap.HasFinishedSqueeze&&player.IsEliminated,"Liquid burst waits until the recruit is fully squeezed");
 Check(Mathf.Abs(trap.rightBank.position.x-trap.leftBank.position.x-trap.pillarThickness-.25f)<.002f,"Crush reaches one-plank width before retreating");
 Check(!player.IsPinned&&!player.IsMoving&&!player.march.enabled,"Fully crushed body is detached from bank motion and cannot move");
 Check(Mathf.Abs(player.transform.forward.x)>.99f,"Crushed body keeps its left/right facing");
    var effect=trap.GetComponentsInChildren<BloodSplash>().Last();
 Check(effect!=null&&effect.GetComponentsInChildren<MeshRenderer>().Length>=29,"Crush creates low-poly droplets and an irregular puddle");
 var firstDrop=effect.transform.Find("Liquid droplet");var before=firstDrop.position;
 await Task.Delay(100);
 Check(Vector3.Distance(firstDrop.position,before)>.02f,"Liquid droplets physically travel through the air");
 Vector3 bodyPosition=player.transform.position;
 until=Time.realtimeSinceStartup+3;
 while(!trap.HasReturned&&Time.realtimeSinceStartup<until)await Task.Delay(20);
 Check(trap.HasReturned&&Mathf.Abs(trap.leftBank.position.x-leftStart)<.001f&&Mathf.Abs(trap.rightBank.position.x-rightStart)<.001f,"Both spike sets animate back to their original lane ends");
 Check(Vector3.Distance(bodyPosition,player.transform.position)<.001f,"Returning spikes do not drag the flattened body away");
 Check(effect.GetComponentsInChildren<Transform>().Any(t=>t.name=="Liquid impact splat"),"Drops collide with the platform and leave secondary splats");
    Check(effect.transform.Find("Spreading puddle").localScale.x>.03f,"Accumulating droplets begin spreading the puddle gradually");
 // Trigger a second lane: cosmetic shapes should vary without using gameplay RNG.
 for(int i=0;i<30;i++)course.traps[1].MissBeat();
 until=Time.realtimeSinceStartup+7;
 while(!course.traps[1].HasFinishedSqueeze&&Time.realtimeSinceStartup<until)await Task.Delay(10);
    var second=course.traps[1].GetComponentsInChildren<BloodSplash>().Last();
 var shape1=effect.transform.Find("Spreading puddle").GetComponent<MeshFilter>().sharedMesh.vertices;
 var shape2=second.transform.Find("Spreading puddle").GetComponent<MeshFilter>().sharedMesh.vertices;
 Check(!shape1.SequenceEqual(shape2),"Separate splashes have different randomized puddle outlines");
 for(int lane=2;lane<4;lane++)for(int i=0;i<30;i++)course.traps[lane].MissBeat();
 until=Time.realtimeSinceStartup+8;
 while(!course.traps.All(t=>t.HasReturned)&&Time.realtimeSinceStartup<until)await Task.Delay(20);
 Check(course.IsGameOver&&course.traps.All(t=>t.HasReturned),"Final splash and return continue after game over");
 round.BeginRound();await Task.Delay(60);
 Check(UnityEngine.Object.FindObjectsByType<BloodSplash>().Length==0&&course.players.All(p=>!p.IsEliminated),"Restart removes puddles and droplets and restores recruits");
}
finally
{
 for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}
 round.BeginRound();
}
return new{checksPassed=checks.Count,checks};

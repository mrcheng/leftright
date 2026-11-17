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
 var trap=course.traps[0];for(int i=0;i<30;i++)trap.MissBeat();
 float until=Time.realtimeSinceStartup+7;
 while(!trap.HasFinishedSqueeze&&Time.realtimeSinceStartup<until)await Task.Delay(10);
 var effect=trap.GetComponentsInChildren<BloodSplash>().Last();
 var pool=effect.transform.Find("Spreading puddle");
 await Task.Delay(1800);float early=pool.localScale.x;
 Check(early>.03f&&early<.6f,"Puddle starts small instead of instantly reaching full size");
 Check(effect.DripsEmitted==0,"Blood does not drip from a ledge before the pool reaches it");
 await Task.Delay(3000);float middle=pool.localScale.x;
 Check(middle>early+.3f,"Puddle visibly spreads over several seconds");
 await Task.Delay(3300);
 Check(pool.localScale.x>middle&&pool.localScale.x<=1.851f,"Puddle completes a gradual bounded spread");
 Check(effect.HasReachedEdge&&effect.DripsEmitted>0,"Reaching the lane edge starts runoff");
 var vertices=pool.GetComponent<MeshFilter>().sharedMesh.vertices;
 Check(vertices.All(v=>Mathf.Abs(pool.TransformPoint(v).z-effect.transform.position.z)<=effect.laneHalfWidth+.001f),
    "Main blood surface stays clipped to the lane instead of hovering over its edge");
 var drops=effect.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Lane edge drip").ToArray();
 Check(drops.Length==24,"Runoff reuses a fixed-size pool of droplets");
 Check(drops.Any(t=>t.gameObject.activeSelf&&t.position.y<effect.transform.position.y-.1f&&
    Mathf.Abs(t.position.z-effect.transform.position.z)>effect.laneHalfWidth),"Drips fall beneath and outside the lane ledge");
 var drop=drops.First(t=>t.gameObject.activeSelf);float y=drop.position.y;
 await Task.Delay(60);
 Check(drop.position.y<y,"Edge drips accelerate downward under gravity");
 round.BeginRound();await Task.Delay(60);
 Check(UnityEngine.Object.FindObjectsByType<BloodSplash>().Length==0,"Restart clears puddles and their pooled edge drips");
}
finally{for(int i=0;i<course.players.Length;i++){course.players[i].walkSpeed=speeds[i];course.players[i].InputOverride=null;}round.BeginRound();}
return new{checksPassed=checks.Count,checks};

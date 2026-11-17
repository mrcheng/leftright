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
var spawner=UnityEngine.Object.FindAnyObjectByType<HealthCrateSpawner>();var round=spawner.course.round;
try
{
 spawner.automaticSpawning=false;round.BeginRound();while(!round.HasStarted)await Task.Delay(10);
 var player=spawner.course.players[0];var trap=spawner.course.traps[0];
 for(int i=0;i<4;i++)trap.MissBeat();
 var crate=spawner.SpawnCrate(0,1.5f);
 Check(crate!=null&&crate.burstPrefab!=null,"Collectible references the breaking-crate reward prefab");
 Check(crate.visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Syringe").gameObject.activeSelf==false,
    "Syringe stays hidden inside the closed crate");
 var position=player.transform.position;position.x=1.5f;player.transform.position=position;
 float before=trap.TargetTravel;
 Check(crate.TryCollect(player)&&trap.PendingRecoverySteps==3&&Mathf.Abs(trap.TargetTravel-before)<.001f,
    "Collection immediately queues three outward beats without gliding back");
 Check(!crate.TryCollect(player),"Burst animation cannot apply health twice");
 var burst=spawner.GetComponentInChildren<HealthCrateBurst>();
 Check(burst!=null&&burst.walls.Length==4&&burst.syringe.gameObject.activeSelf,"Pickup starts one burst with four walls and a visible syringe");
 var rotations=burst.walls.Select(w=>w.rotation).ToArray();
 var hinges=burst.walls.Select(w=>w.position).ToArray();
 Vector3 lid=burst.lid.position,syringe=burst.syringe.position;
 await Task.Delay(180);
 Check(Vector3.Distance(burst.lid.position,lid)>.1f,"Lid flies off immediately");
 Check(burst.walls.Select((w,i)=>Quaternion.Angle(w.rotation,rotations[i])>35).All(v=>v),"All four walls fall outward from their base hinges");
 Check(burst.walls.Select((w,i)=>Vector3.Distance(w.position,hinges[i])<.001f).All(v=>v),"Wall hinges stay in place instead of flying outward");
 await Task.Delay(500);
 Check(burst.syringe.position.y>syringe.y+.65f,"Red-filled syringe rises above the breaking box");
 Check(Vector3.Dot(burst.syringe.position-syringe,-burst.transform.forward)>.5f,"Syringe rises in front of the recruit instead of being hidden inside it");
 var renderer=burst.syringe.GetComponentInChildren<Renderer>();
 var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block,0);
 float alpha=block.GetColor("_BaseColor").a;
 Check(alpha>0&&alpha<1&&renderer.sharedMaterial.GetFloat("_Surface")==1,"Syringe fades through transparent materials");
 await Task.Delay(600);
 Check(burst==null,"Short reward animation removes its debris and syringe");
 var remaining=UnityEngine.Object.Instantiate(spawner.cratePrefab.burstPrefab,spawner.transform);
 round.BeginRound();await Task.Delay(60);
 Check(remaining==null&&spawner.GetComponentsInChildren<HealthCrateBurst>().Length==0,"Restart also clears a burst that is still playing");
}
finally{spawner.automaticSpawning=true;round.BeginRound();}
return new{checksPassed=checks.Count,checks};

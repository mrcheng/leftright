using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var course=menu.course;var obstacles=menu.Obstacles;
var health=course.GetComponentInChildren<HealthCrateSpawner>();
float lead=course.round.leadInSeconds,reaction=course.reactionBeats;
bool automatic=obstacles.automaticSpawning,healthAuto=health.automaticSpawning;
float wireChance=obstacles.electricWireChance;obstacles.electricWireChance=0;
int checks=0;void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
const string bestKey="Rebuild.SurvivalBestSeconds";bool hadBest=PlayerPrefs.HasKey(bestKey);float best=PlayerPrefs.GetFloat(bestKey);
void Beat(int n)=>typeof(ObstacleSpawner).GetMethod("OnBeat",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obstacles,new object[]{(long)n});
void Arm(LaneMine mine)=>typeof(LaneMine).GetProperty("ArmsAtBeat").SetValue(mine,-1.0);
try
{
 menu.Open();menu.SetPlayerCount(4);course.round.leadInSeconds=.2f;course.reactionBeats=10000;
 obstacles.automaticSpawning=false;health.automaticSpawning=false;
 Check(obstacles.ExplosionClip!=null&&obstacles.ExplosionClip.length>1,"Original blast audio loads");
 menu.Play();foreach(var p in course.players)p.InputOverride=0;
 Check(obstacles.SpawnMine(0,1.7f)==null,"No mines during countdown");
 await Task.Delay(400);
 Check(obstacles.SpawnMine(0,course.traps[0].LeftTip)==null,"Spike boundary spawn rejected");
 obstacles.automaticSpawning=true;Beat(obstacles.firstSpawnBeat-1);
 Check(obstacles.ActiveMines.Count==0,"No opportunities before first configured beat");
 Beat(obstacles.firstSpawnBeat);
 Check(obstacles.ActiveMines.Count==4,"Every active lane gets one mine opportunity");
 foreach(var mine in obstacles.ActiveMines)Check(Mathf.Abs(mine.transform.position.x-course.players[mine.LaneIndex].transform.position.x)>=obstacles.minimumSpawnDistance-.001f,"Spawn stays away from recruit");
 Beat(obstacles.firstSpawnBeat);
 Check(obstacles.ActiveMines.Count==4,"Same beat cannot spawn duplicate mines");
 obstacles.automaticSpawning=false;
 var target=obstacles.ActiveMines[0];var player=course.players[target.LaneIndex];
 Check(obstacles.SpawnMine(target.LaneIndex,-target.transform.position.x)==null,"Only one mine per lane");
 Check(health.SpawnCrate(target.LaneIndex,target.transform.position.x)==null,"Health cannot overlap a mine");
 Check(!target.CheckContact(player)&&!target.IsArmed,"Warning period is nonlethal");
 Arm(target);Check(!target.CheckContact(course.players[1]),"Another lane cannot trigger this mine");
 float mineX=target.transform.position.x;
 var position=player.transform.position;position.x=mineX-.5f;player.transform.position=position;
 Check(player.TryJump(),"Recruit can jump toward an armed mine");player.InputOverride=1;
 await Task.Delay((int)((player.jumpDuration+.15f)*1000));player.InputOverride=0;
 Check(!player.IsEliminated&&!target.Detonated&&player.transform.position.x>mineX+.32f,"Full jump arc clears pressure plate and lands safely");
 position=player.transform.position;position.x=mineX;player.transform.position=position;
 Check(target.CheckContact(player),"Ground contact detonates mine");
 Check(player.IsEliminated&&player.WasBlasted,"Mine uses real elimination and blast death state");
 var explosion=player.GetComponent<RecruitExplosion>();
 Check(explosion!=null&&explosion.FragmentCount>5,"Recruit mesh breaks into multiple physical pieces");
 Check(explosion.HasBlastSound,"Blast presentation includes bomb sound");
 Check(UnityEngine.Object.FindObjectsByType<BloodSplash>().Length>=3,"Explosion produces broad blood sprays and puddles");
 Check(!target.CheckContact(player),"Mine cannot detonate twice");
 menu.Open();await Task.Delay(30);
 Check(obstacles.ActiveMines.Count==0&&!explosion.IsActive&&!player.WasBlasted,"Reset clears mines, body fragments and blast state");
 Check(player.GetComponentInChildren<SkinnedMeshRenderer>().enabled,"Reset restores intact recruit mesh");
 menu.SetPlayerCount(2);menu.Play();foreach(var p in course.players)p.InputOverride=0;await Task.Delay(400);
 var crate=health.SpawnCrate(0,1.7f);Check(crate!=null,"Existing health reward can spawn normally");
 Check(obstacles.SpawnMine(0,1.7f)==null,"Mine cannot overlap an existing health crate");
 UnityEngine.Object.DestroyImmediate(crate.gameObject);
 obstacles.automaticSpawning=true;Beat(obstacles.firstSpawnBeat);
 Check(obstacles.ActiveMines.Count==2,"Removed lanes receive no obstacles");obstacles.automaticSpawning=false;
 var expiring=obstacles.ActiveMines[1];typeof(LaneMine).GetProperty("ExpiresAtBeat").SetValue(expiring,-1.0);
 course.round.audioOutput.music.Pause();await Task.Delay(50);Check(expiring!=null,"Paused music freezes mine expiry");
 course.round.audioOutput.music.UnPause();await Task.Delay(80);Check(expiring==null,"Expired mine despawns when music resumes");
 obstacles.automaticSpawning=true;Beat(obstacles.firstSpawnBeat);Check(!obstacles.HasMine(1),"Expiry cannot farm another opportunity on the same beat");obstacles.automaticSpawning=false;
 var swept=obstacles.ActiveMines[0];Arm(swept);player=course.players[0];position=player.transform.position;position.x=swept.transform.position.x-.8f;player.transform.position=position;swept.CheckContact(player);
 position.x=swept.transform.position.x+.8f;player.transform.position=position;
 course.NetworkReplica=true;Check(!swept.CheckContact(player),"Replica cannot author local mine damage");course.NetworkReplica=false;
 Check(swept.CheckContact(player),"Swept contact catches crossing between frames");
 return checks+" mine checks passed";
}
finally
{
 obstacles.electricWireChance=wireChance;
 course.NetworkReplica=false;obstacles.automaticSpawning=automatic;health.automaticSpawning=healthAuto;
 course.round.leadInSeconds=lead;course.reactionBeats=reaction;menu.Open();menu.SetPlayerCount(4);
 if(hadBest)PlayerPrefs.SetFloat(bestKey,best);else PlayerPrefs.DeleteKey(bestKey);PlayerPrefs.Save();
}

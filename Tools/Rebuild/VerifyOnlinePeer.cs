using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var online=UnityEngine.Object.FindAnyObjectByType<OnlineMatch>();var course=online.menu.course;var round=course.round;var spawner=course.GetComponentInChildren<HealthCrateSpawner>();
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
async Task Wait(Func<bool> condition,float seconds=5){float until=Time.realtimeSinceStartup+seconds;while(!condition()&&Time.realtimeSinceStartup<until)await Task.Delay(10);if(!condition())throw new Exception("Timed out: "+online.Status);}
string report=Path.GetFullPath("Temp/peer-client.json"),input=Path.GetFullPath("Temp/peer-input.txt");
JObject Read(){try{return JObject.Parse(File.ReadAllText(report));}catch{return null;}}
int Int(string field){var r=Read();return r==null?-1:(int)r[field];}
var all=course.GetComponentsInChildren<RecruitController>(true);var speeds=all.Select(p=>p.walkSpeed).ToArray();bool auto=spawner.automaticSpawning;
Process peer=null;Action<bool> follow=null;
try
{
 if(File.Exists(report))File.Delete(report);File.WriteAllText(input,"0");online.port=7793;online.TestInput=0;
 Check(!online.Join("invalid address")&&!online.Active,"Malformed host addresses are rejected without opening a session");
 foreach(var p in all){p.walkSpeed=0;p.InputOverride=0;}
 spawner.automaticSpawning=false;
 Check(online.Host(4),"Direct host starts without cloud services");
 peer=Process.Start(new ProcessStartInfo{FileName=Path.GetFullPath("Builds/OnlineTest/LeftRight.exe"),Arguments="-batchmode -nographics -lr-test -lr-join 127.0.0.1 -lr-port 7793 -lr-report \""+report+"\" -lr-input-file \""+input+"\" -logFile \""+Path.GetFullPath("Temp/peer-client.log")+"\"",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
 await Wait(()=>online.HumanCount==2&&Int("snapshots")>2,20);
 Check(!online.RoundActive&&!round.HasStarted,"Lobby waits for host rather than auto-starting");
 Check(online.RequestRound(),"Host schedules an online round");
 await Wait(()=>Int("round")==1);
 var r=Read();Check(Math.Abs((double)r["startsAt"]-online.StartTime)<.0001,"Both processes share the exact scheduled network start timestamp");
 Check(Int("slot")==1&&Math.Abs((float)((JArray)r["z"])[1]+3.3f)<.01f,"Joining client owns slot one and displays it in the front lane");
 Check(course.players[2].computerControlled&&course.players[3].computerControlled&&!course.players[1].computerControlled,"Only empty online slots use host-controlled AI");
 await Wait(()=>round.HasStarted,5);
 int number=online.RoundNumber;online.menu.OpenMatchSettings();
 Check(online.Active&&online.RoundActive&&online.menu.Page==StartMenu.MenuPage.Settings,"Opening online volume settings does not disconnect or pause the match");
 online.menu.Back();Check(!online.menu.IsOpen&&online.RoundNumber==number,"Closing online settings returns to the same match");
 await Wait(()=>Read()!=null&&(double)Read()["beat"]>1,3);
 r=Read();double expected=((double)r["now"]-(double)r["startsAt"])*round.masterBeat.BeatsPerMinute/60;
 Check(Math.Abs((double)r["beat"]-expected)<.12,"Client beat follows the shared network timeline");
 // Stop incidental damage while exercising authority, by following every public cue.
 follow=right=>{float axis=right?1:-1;online.TestInput=axis;File.WriteAllText(input,axis.ToString(System.Globalization.CultureInfo.InvariantCulture));foreach(var p in course.players.Skip(2))p.InputOverride=axis;};
 round.officer.DirectionChanged+=follow;follow(round.officer.DirectionRight);
 float hostX=course.players[0].transform.position.x;course.players[1].walkSpeed=1.5f;File.WriteAllText(input,"1");
 await Task.Delay(450);await Wait(()=>course.players[1].Horizontal>.9f);
 Check(course.players[1].transform.position.x>.2f&&Math.Abs(course.players[0].transform.position.x-hostX)<.01f,"Remote keyboard input moves only the sender-owned recruit on the host");
 await Wait(()=>Read()!=null&&Math.Abs((float)((JArray)Read()["x"])[1]-course.players[1].transform.position.x)<.15f);
 Check(true,"Authoritative remote movement is mirrored back to the client");
 course.players[1].walkSpeed=0;follow(round.officer.DirectionRight);
 var trap=course.traps[0];for(int i=0;i<10;i++)trap.MissBeat();trap.Animate(5);
 for(int pickup=1;pickup<=2;pickup++)
 {
  await Wait(()=>round.masterBeat.BeatPhase>.3&&round.masterBeat.BeatPhase<.5);
  var waiting=course.players[0].transform.position;waiting.x=0;course.players[0].transform.position=waiting; var crate=spawner.SpawnCrate(0,1.5f);Check(crate!=null,"Online health crate "+pickup+" is created on the host");
  await Wait(()=>Int("snapshots")>0);await Task.Delay(120);
  var pos=course.players[0].transform.position;pos.x=1.5f;course.players[0].transform.position=pos;
  float before=trap.TargetTravel;Check(crate.TryCollect(course.players[0]),"Online health crate "+pickup+" is collected by host authority");
  await Wait(()=>trap.PendingRecoverySteps==0,3);await Task.Delay(150);
  Check(Math.Abs(trap.TargetTravel-(before-3*trap.inwardPerMiss))<.001f,"Online health crate "+pickup+" delivers three beat-stepped recoveries");
  r=Read();Check((int)((JArray)r["recovery"])[0]==pickup&&Math.Abs((float)((JArray)r["travel"])[0]-trap.CurrentTravel)<.01f,"Client mirrors recovery "+pickup+" and final bank position");
 }
 // Force the two bots and remote contender to lose; host chooses the sole winner.
 for(int lane=1;lane<4;lane++)for(int i=0;i<30;i++)course.traps[lane].MissBeat();
 await Wait(()=>course.IsGameOver,8);await Wait(()=>Read()!=null&&(bool)Read()["over"]);
 r=Read();Check((int)r["winner"]==Array.IndexOf(course.players,course.Winner),"Host and client agree on the winner");
 Check((int)r["sequence"]==Array.IndexOf(course.GetComponent<VictoryDirector>().sequences,course.GetComponent<VictoryDirector>().SelectedSequence),"Host and client play the same randomly selected victory sequence");
 round.BeginRound();await Wait(()=>Int("round")==2);
 Check(!course.IsGameOver&&!(bool)Read()["over"],"Host rematch resets both processes to the next shared countdown");
 peer.Kill();peer.WaitForExit();await Wait(()=>!online.Active,8);
 Check(online.menu.IsOpen&&!course.NetworkReplica,"A peer disconnect ends the test session and returns safely to the menu");
}
finally
{
 if(follow!=null)round.officer.DirectionChanged-=follow;
 if(peer!=null&&!peer.HasExited){peer.Kill();peer.WaitForExit();}peer?.Dispose();
 if(online.Active)online.Leave();online.TestInput=null;spawner.automaticSpawning=auto;
 for(int i=0;i<all.Length;i++){all[i].walkSpeed=speeds[i];all[i].InputOverride=null;}
 online.menu.Open();
}
return new{checksPassed=checks.Count,checks};

using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var online=UnityEngine.Object.FindAnyObjectByType<OnlineMatch>();
var course=online.menu.course;
var spawner=course.GetComponentInChildren<HealthCrateSpawner>();bool automatic=spawner.automaticSpawning;
string report=Path.GetFullPath("Temp/relay-peer.json"),input=Path.GetFullPath("Temp/relay-peer-input.txt");
Process peer=null;int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
JObject Read(){try{return JObject.Parse(File.ReadAllText(report));}catch{return null;}}
async Task Wait(Func<bool> condition,int seconds=20){double until=Time.realtimeSinceStartupAsDouble+seconds;while(!condition()&&Time.realtimeSinceStartupAsDouble<until)await Task.Delay(20);Check(condition(),"Timed out: "+online.Status);}
try
{
 online.Leave();spawner.automaticSpawning=false;
 if(File.Exists(report))File.Delete(report);File.WriteAllText(input,"0");
 Check(await online.HostInternet(4),"Live Relay allocation failed: "+online.Status);
 Check(online.UsesRelay&&online.JoinCode.Length>=4,"Host did not receive join code");
 peer=Process.Start(new ProcessStartInfo{FileName=Path.GetFullPath("Builds/OnlineTest/LeftRight.exe"),
  Arguments="-batchmode -nographics -lr-test -lr-relay-join "+online.JoinCode+" -lr-report \""+report+"\" -lr-input-file \""+input+"\" -logFile \""+Path.GetFullPath("Temp/relay-peer.log")+"\"",
  UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
 await Wait(()=>online.HumanCount==2&&Read()!=null&&(int)Read()["snapshots"]>2);
 Check(!online.RoundActive,"Lobby should wait for host");
 Check(online.RequestRound(),"Host could not start round");
 await Wait(()=>Read()!=null&&(int)Read()["round"]==online.RoundNumber);
 Check(Math.Abs((double)Read()["startsAt"]-online.StartTime)<.001,"Shared start timestamp mismatch");
 await Wait(()=>course.round.HasStarted);
 Check((int)Read()["slot"]==1,"Peer owns wrong slot");
 Check(Math.Abs((float)((JArray)Read()["z"])[1]+3.3f)<.01f,"Peer recruit is not in front lane");
 Check(course.players[2].computerControlled&&course.players[3].computerControlled,"Empty slots are not AI");
 Check(Math.Abs((double)Read()["beat"]-course.round.masterBeat.BeatPosition)<.5,"Network beat drift");
 File.WriteAllText(input,"1");await Wait(()=>course.players[1].Horizontal>.9f,3);
 File.WriteAllText(input,"0");
 peer.Kill();peer.WaitForExit(3000);await Wait(()=>!online.Active,12);
 Check(online.menu.IsOpen,"Disconnect did not restore menu");
 return checks+" live Relay checks passed: allocation/code, two processes, lobby, shared start, beat, ownership, front lane, AI, input, disconnect";
}
finally
{
 if(peer!=null){if(!peer.HasExited){peer.Kill();peer.WaitForExit(3000);}peer.Dispose();}
 spawner.automaticSpawning=automatic;online.Leave();online.menu.Open();
}

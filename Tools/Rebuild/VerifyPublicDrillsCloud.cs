using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var online=menu.online;var browser=menu.PublicDrills;
var cloud=new UnityPublicDrillDirectory();var checks=new List<string>();
void Check(bool value,string label){if(!value)throw new Exception(label+" ("+browser.HostStatus+" / "+online.Status+")");checks.Add(label);}
async Task Wait(Func<bool> ready){float until=Time.realtimeSinceStartup+20;while(!ready()&&Time.realtimeSinceStartup<until)await Task.Delay(100);Check(ready(),"Cloud operation completes");}
async Task<PublicDrillRoom> Find(string code){return (await cloud.QueryAsync()).FirstOrDefault(room=>room.JoinCode==code);}
async Task WaitGone(string code){for(int i=0;i<12;i++){await Task.Delay(1300);if(await Find(code)==null)return;}throw new Exception("Room still listed");}
string code=null;
try
{
 online.Leave();await Task.Delay(100);browser.SetPublic(true);
 Check(await online.HostInternet(4),"Real Relay host starts");code=online.JoinCode;
 await Wait(()=>browser.IsListed);
 var room=await Find(code);Check(room!=null&&room.Players==1&&room.Capacity==4,"Live query exposes a public waiting host with 1/4 humans");
 // Count propagation is driven by the same host property updated by NGO callbacks;
 // real multi-browser joins are validated separately by VerifyPublicBrowser.cjs.
 typeof(OnlineMatch).GetProperty("HumanCount",BindingFlags.Instance|BindingFlags.Public).SetValue(online,2);
 await Task.Delay(1600);room=await Find(code);Check(room!=null&&room.Players==2,"Cloud listing updates to 2/4 humans");
 browser.SetPublic(false);await WaitGone(code);Check(!browser.IsListed,"Private checkbox removes the real cloud listing");
 browser.SetPublic(true);await Wait(()=>browser.IsListed);await Task.Delay(1300);
 Check(await Find(code)!=null,"Public checkbox republishes the real room");
 Check(online.RequestRound(),"Host starts the live room");await WaitGone(code);
 Check(!browser.IsListed,"Started rounds disappear from live queries");
 online.Leave();
}
finally{online.Leave();browser.SetPublic(true);menu.Open();}
return new{checksPassed=checks.Count,checks};

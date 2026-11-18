using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var online=menu.online;var browser=menu.PublicDrills;
var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
var checks=new List<string>();void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
Task Reconcile(){return (Task)typeof(PublicDrillLobby).GetMethod("ReconcileHostAsync",flags).Invoke(browser,null);}
void Count(int n){typeof(OnlineMatch).GetProperty("HumanCount",flags).SetValue(online,n);}
void RefreshReady(){typeof(PublicDrillLobby).GetField("nextQuery",flags).SetValue(browser,0f);}
var fake=new PublicDirectoryFake();var relay=new PublicRelayFake();
bool wasEnabled=browser.enabled;browser.enabled=false;online.Leave();
while(browser.Refreshing)await Task.Delay(50);
browser.DirectoryOverride=fake;online.RelayConnectionOverride=relay;
try
{
 Check(browser.IsPublic,"Hosting defaults to public");
 browser.SetPublic(false);Check(await online.HostInternet(4),"Private Relay hosting still works");
 await Reconcile();Check(fake.Created==0&&!browser.IsListed,"Private hosts never publish a listing");
 browser.SetPublic(true);await Reconcile();Check(browser.IsListed&&fake.Created==1,"Public host publishes one listing");
 await Reconcile();Check(fake.Created==1,"Repeated reconciliation does not duplicate rooms");
 Count(2);await Reconcile();Check(fake.Players==2&&fake.Updated==1,"Authoritative human count updates the listing");
 Check(fake.Heartbeats>0,"Waiting host sends a heartbeat");
 browser.SetPublic(false);Check(!browser.IsListed&&fake.Deleted.Contains("room1"),"Unchecking Public removes the waiting room");
 browser.SetPublic(true);await Reconcile();Check(browser.IsListed&&fake.Created==2,"Rechecking Public advertises again");
 Count(4);await Reconcile();Check(!browser.IsListed&&browser.HostStatus.Contains("full"),"Full rooms disappear and the host sees a full-room status");
 Count(2);await Reconcile();Check(browser.IsListed,"A room with a newly free place returns");
 Check(online.RequestRound(),"Host can start after another player connects");
 Check(!browser.IsListed,"Starting a round removes the listing immediately");await Reconcile();Check(!browser.IsListed,"Running rounds cannot recreate a listing");
 online.Leave();await Task.Delay(100);fake.HoldCreate=true;
 Check(await online.HostInternet(4),"Host can open another room: "+online.Status);
 var pending=Reconcile();online.Leave();fake.CreatePending.SetResult("late-room");await pending;
 Check(!browser.IsListed&&fake.Deleted.Contains("late-room"),"Late creation after leaving is deleted");await Task.Delay(100);
 fake.HoldCreate=false;fake.FailCreate=true;await online.HostInternet(4);await Reconcile();
 Check(online.Active&&!browser.IsListed&&browser.HostStatus.Contains("Share"),"Directory failures preserve private code hosting with a useful message");online.Leave();
 fake.FailCreate=false;fake.QueryPending=new TaskCompletionSource<PublicDrillRoom[]>();RefreshReady();
 var query=browser.RefreshAsync();await browser.RefreshAsync();Check(fake.Queries==1,"Duplicate refresh clicks share one in-flight query");
 var room=new PublicDrillRoom{Id="candidate",Name="Drill TEST01",JoinCode="TEST01",Players=1,Capacity=4};
 fake.QueryPending.SetResult(new[]{room});await query;Check(browser.Rooms.Length==1&&!browser.Refreshing,"Query results reach the menu");
 fake.GetPending=new TaskCompletionSource<PublicDrillRoom>();menu.Open();var joining=browser.JoinAsync(room);menu.ShowLocalGame();fake.GetPending.SetResult(room);await joining;
 Check(relay.JoinCalls==0&&!online.Active&&!browser.Joining&&browser.BrowserStatus!="Joining drill...","Changing menus cancels a late public join and clears progress");
 menu.Open();fake.GetPending=new TaskCompletionSource<PublicDrillRoom>();joining=browser.JoinAsync(room);fake.GetPending.SetResult(null);await joining;
 Check(!online.Active&&browser.BrowserStatus.Contains("no longer"),"A stale/full/started room fails safely before Relay");
 RefreshReady();await browser.RefreshAsync();fake.GetPending=new TaskCompletionSource<PublicDrillRoom>();joining=browser.JoinAsync(room);fake.GetPending.SetException(new Exception("gone"));await joining;
 Check(!browser.Joining&&browser.Rooms.Length==1&&browser.BrowserStatus.Contains("unavailable"),"Lookup failures retain a useful visible status alongside existing rows");
 fake.FailQuery=true;fake.QueryPending=null;RefreshReady();await browser.RefreshAsync();
 Check(browser.Rooms.Length==0&&!browser.Refreshing&&browser.BrowserStatus.Contains("Refresh"),"Query failure clears stale rows and permits retry");
}
finally
{
 online.Leave();browser.StopAdvertising();browser.DirectoryOverride=null;online.RelayConnectionOverride=null;browser.SetPublic(true);browser.enabled=wasEnabled;menu.Open();RefreshReady();
}
return new{checksPassed=checks.Count,checks};

public sealed class PublicRelayFake:IRelayConnection
{
 public int JoinCalls;
 public Task<RelayTransportTicket> HostAsync(int capacity)=>Task.FromResult(new RelayTransportTicket("TEST01",t=>t.SetConnectionData("127.0.0.1",27811,"127.0.0.1")));
 public Task<RelayTransportTicket> JoinAsync(string code){JoinCalls++;throw new Exception("Unexpected Relay join");}
}
public sealed class PublicDirectoryFake:IPublicDrillDirectory
{
 public int Created,Updated,Players,Heartbeats,Queries;public bool HoldCreate,FailCreate,FailQuery;
 public HashSet<string> Deleted=new HashSet<string>();
 public TaskCompletionSource<string> CreatePending;
 public TaskCompletionSource<PublicDrillRoom[]> QueryPending;
 public TaskCompletionSource<PublicDrillRoom> GetPending;
 public Task<PublicDrillRoom[]> QueryAsync(){Queries++;if(FailQuery)throw new Exception("offline");return QueryPending!=null?QueryPending.Task:Task.FromResult(Array.Empty<PublicDrillRoom>());}
 public Task<PublicDrillRoom> GetAsync(string id)=>GetPending.Task;
 public Task<string> CreateAsync(string code,int capacity,int players){Created++;Players=players;if(FailCreate)throw new Exception("offline");if(HoldCreate){CreatePending=new TaskCompletionSource<string>();return CreatePending.Task;}return Task.FromResult("room"+Created);}
 public Task UpdateAsync(string id,int players){Updated++;Players=players;return Task.CompletedTask;}
 public Task HeartbeatAsync(string id){Heartbeats++;return Task.CompletedTask;}
 public Task DeleteAsync(string id){Deleted.Add(id);return Task.CompletedTask;}
}

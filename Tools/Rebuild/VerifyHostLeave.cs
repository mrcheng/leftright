using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var online=menu.online;var directory=menu.PublicDrills;var dialog=menu.SessionDialog;var fake=new LeaveDirectory();var flags=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Public;int checks=0;
void Check(bool test,string name){if(!test)throw new Exception(name);checks++;}
async Task Reconcile(){var pending=(Task)typeof(PublicDrillLobby).GetMethod("ReconcileHostAsync",flags).Invoke(directory,null);await pending;}
async Task Ended(){float end=Time.realtimeSinceStartup+3;while(online.Active&&Time.realtimeSinceStartup<end)await Task.Delay(5);Check(!online.Active,"Session ended");}
bool enabled=directory.enabled;directory.enabled=false;directory.DirectoryOverride=fake;online.Leave();menu.Open();
try{
 online.port=7798;Check(online.Host(2),"Host opens");typeof(OnlineMatch).GetProperty("UsesRelay",flags).SetValue(online,true);directory.SetPublic(true);await Reconcile();Check(directory.IsListed&&fake.created==1,"Public listing exists");
 menu.Back();Check(dialog.Confirming&&online.Active&&directory.IsListed,"Leaving asks before touching room");dialog.Dismiss();Check(online.Active&&directory.IsListed&&!dialog.Visible,"Stay retains hosting and listing");
 menu.Open();Check(dialog.Confirming&&online.Active,"Menu/escape exit also requires confirmation");dialog.ConfirmLeave();Check(online.Closing&&!directory.IsListed&&!online.CanStart&&fake.deleted==1,"Confirmation immediately removes listing and prevents start");await Reconcile();Check(fake.created==1,"Closing room cannot be advertised again");await Ended();Check(menu.IsOpen&&menu.Page==StartMenu.MenuPage.Main&&!dialog.Visible,"Host returns to main");
 await Task.Delay(120);online.port++;Check(online.Host(2),"Can rehost after leaving: "+online.Status);typeof(OnlineMatch).GetProperty("HumanCount",flags).SetValue(online,2);Check(online.RequestRound(),"Online countdown starts");menu.Open();Check(dialog.Confirming&&online.Active&&online.RoundActive&&menu.course.round.IsCountingDown,"Confirmation preserves running countdown");dialog.Dismiss();Check(online.RoundActive&&!dialog.Visible,"Stay preserves online round");menu.OpenMatchSettings();Check(!dialog.Visible&&online.Active,"Opening settings doesn't ask to leave");menu.Back();Check(online.Active&&!menu.IsOpen,"Settings Back resumes game");menu.RequestLeaveOnline();Check(dialog.Confirming,"Explicit settings leave requests confirmation");dialog.ConfirmLeave();await Ended();
 dialog.Notify(OnlineSessionDialog.HostEnded);Check(dialog.Visible&&!dialog.Confirming&&dialog.Notice.Contains("kicked in the butt"),"Guest notice remains visible on main menu");menu.Back();Check(!dialog.Visible&&menu.Page==StartMenu.MenuPage.Main,"Notice dismisses without leaving another session");
 menu.Play();Check(!dialog.Visible,"Offline drill unaffected");menu.Open();
 await Task.Delay(120);online.port++;Check(online.Host(2),"Rehost for stale-close test");online.EndHostedSession();online.Leave();await Task.Delay(120);online.port++;Check(online.Host(2),"New host after interrupted teardown");await Task.Delay(350);Check(online.Active&&!online.Closing,"Old close operation cannot terminate new room");
 return new{checksPassed=checks};
}finally{online.Leave();dialog.Dismiss();directory.StopAdvertising();directory.DirectoryOverride=null;directory.enabled=enabled;menu.Open();}
public class LeaveDirectory:IPublicDrillDirectory{
 public int created,deleted;public Task<PublicDrillRoom[]> QueryAsync()=>Task.FromResult(Array.Empty<PublicDrillRoom>());public Task<PublicDrillRoom> GetAsync(string id)=>Task.FromResult<PublicDrillRoom>(null);public Task<string> CreateAsync(string code,int capacity,int players){created++;return Task.FromResult("leave-test");}public Task UpdateAsync(string id,int players)=>Task.CompletedTask;public Task HeartbeatAsync(string id)=>Task.CompletedTask;public Task DeleteAsync(string id){deleted++;return Task.CompletedTask;}}

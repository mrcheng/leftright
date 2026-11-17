using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
using Unity.Services.Relay;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var online=UnityEngine.Object.FindAnyObjectByType<OnlineMatch>();
var checks=new List<string>();
void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
online.Leave();
var fake=new RelayMenuFake();online.RelayConnectionOverride=fake;
try
{
 Check(!await online.JoinInternet("bad code!"),"Malformed codes rejected before services");
 Check(fake.Calls==0,"Invalid codes never call Relay");
 var pending=online.HostInternet(4);
 Check(online.Busy&&!online.Active,"Allocation marks menu busy before transport starts");
 Check(fake.Capacity==3,"Four-player host reserves three guest connections");
 Check(!await online.HostInternet(2)&&fake.Calls==1,"Duplicate host clicks cannot allocate twice");
 online.menu.Back();
 Check(!online.Busy&&!online.Active,"Back cancels a pending allocation");
 bool configured=false;
 fake.Pending.SetResult(new RelayTransportTicket("ABCDEF",t=>configured=true));
 Check(!await pending&&!configured&&!online.Active,"Late allocation cannot start transport after cancellation");
 var join=online.JoinInternet("  abc123  ");
 Check(fake.Code=="ABC123","Friend codes normalized before Relay lookup");
 fake.Pending.SetException(new RelayServiceException(RelayExceptionReason.JoinCodeNotFound,"Test expired code"));
 Check(!await join&&!online.Busy&&!online.Active,"Expired code restores usable menu");
 Check(online.Status.Contains("fresh code"),"Expired code gives a useful retry message");
 var retry=online.HostInternet(2);
 Check(fake.Capacity==1,"Two-player retry reserves one guest");
 fake.Pending.SetException(new Exception("Private backend detail"));
 Check(!await retry&&!online.Status.Contains("Private"),"Service errors hide backend details and allow retry");
 Check(UnityRelayConnection.FriendlyError(new TimeoutException()).Contains("too long"),"Stalled request has a friendly timeout message");
 return checks.Count+" checks passed: "+string.Join("; ",checks);
}
finally{online.RelayConnectionOverride=null;online.Leave();online.menu.Open();}

public sealed class RelayMenuFake : IRelayConnection
{
 public int Calls,Capacity;public string Code;
 public TaskCompletionSource<RelayTransportTicket> Pending;
 public Task<RelayTransportTicket> HostAsync(int capacity){Capacity=capacity;return Next();}
 public Task<RelayTransportTicket> JoinAsync(string code){Code=code;return Next();}
 Task<RelayTransportTicket> Next(){Calls++;Pending=new TaskCompletionSource<RelayTransportTicket>();return Pending.Task;}
}


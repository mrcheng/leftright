using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var lens=UnityEngine.Object.FindAnyObjectByType<LensSplatter>();var round=lens.course.round;
float originalLife=lens.lifetimeSeconds;
try
{
 round.BeginRound();
 Check(lens.Overlay!=null&&!lens.Overlay.raycastTarget,"Screen-space lens effect does not intercept player input");
 Check(lens.ActiveSplatCount==0&&!lens.Overlay.enabled,"A fresh round starts with a clear lens");
 Check(lens.RuntimeMaterial!=lens.splatterMaterial&&lens.RuntimeMaterial.shader.isSupported,"Lens uses an independent supported shader instance");
 var player=lens.course.players[0];var trap=lens.course.traps[0];
 player.PinTo(trap.leftBank,true,trap.spikeReach);
 Check(lens.ActiveSplatCount==1,"First spike hit adds a small lens splat before elimination");
 Check(lens.RuntimeMaterial.GetVectorArray("_Splats")[0].w<lens.splatSize*.7f&&
    lens.RuntimeMaterial.GetVectorArray("_Seeds")[0].w<1,"First-hit splats are smaller and lighter than death splats");
 player.PinTo(trap.leftBank,true,trap.spikeReach);
 Check(lens.ActiveSplatCount==1,"Remaining pinned cannot repeat the first-hit lens splash");
 Check(lens.RuntimeMaterial.GetColor("_Color").a<=.45f&&lens.RuntimeMaterial.GetFloat("_Softness")>=.6f,
    "Lens blood uses translucent tint and broad defocused edges");
 lens.Clear();player.Eliminate();
 Check(lens.ActiveSplatCount==1,"One recruit death adds one camera splat");
 player.Eliminate();
 Check(lens.ActiveSplatCount==1,"Repeated elimination cannot duplicate the same death splat");
 for(int i=1;i<4;i++)lens.course.players[i].Eliminate();
 Check(lens.ActiveSplatCount==4,"All four recruit deaths contribute separate small splats");
 var state=UnityEngine.Random.state;
 float expected=UnityEngine.Random.value;UnityEngine.Random.state=state;
 lens.AddSplatter(player.transform.position);
 Check(UnityEngine.Random.value==expected,"Cosmetic splatter does not change gameplay randomness");
 for(int i=0;i<12;i++)lens.AddSplatter(player.transform.position);
 Check(lens.ActiveSplatCount==8,"Repeated hits use a bounded eight-splat buffer");
 lens.Clear();lens.lifetimeSeconds=.25f;lens.AddSplatter(player.transform.position);
 await Task.Delay(450);
 Check(lens.ActiveSplatCount==0&&!lens.Overlay.enabled,"Expired blood clears the lens and disables the overlay draw");
 lens.lifetimeSeconds=originalLife;lens.AddSplatter(player.transform.position);
 var canvas=lens.Overlay.GetComponentInParent<Canvas>();
 lens.enabled=false;
 Check(lens.ActiveSplatCount==0&&!canvas.gameObject.activeSelf,"Disabling removes the effect immediately");
 lens.enabled=true;round.BeginRound();
 lens.course.players[1].Eliminate();
 Check(lens.ActiveSplatCount==1,"Re-enabling subscribes once to death events");
 round.BeginRound();
 Check(lens.ActiveSplatCount==0,"Restart clears accumulated lens blood");
}
finally{lens.lifetimeSeconds=originalLife;lens.enabled=true;round.BeginRound();}
return new{checksPassed=checks.Count,checks};

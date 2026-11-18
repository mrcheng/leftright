using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var course=menu.course;
float lead=course.round.leadInSeconds;int checks=0;
void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
try
{
 menu.Open();var p=course.players[0];
 Check(!p.TryJump(),"Menu cannot start a jump");
 course.round.leadInSeconds=5;menu.Play();
 foreach(var recruit in course.players)recruit.InputOverride=0;
 float ground=p.transform.localPosition.y;float x=p.transform.localPosition.x;
 Check(p.TryJump(),"Jump works during pre-music countdown");
 Check(!p.TryJump(),"Air jumping is rejected");
 p.InputOverride=1;await Task.Delay(220);
 Check(p.IsJumping&&p.transform.localPosition.y>ground+.4f,"Jump lifts recruit off lane");
 Check(p.transform.localPosition.x>x,"Horizontal movement continues during jump");
 Check(Quaternion.Angle(p.march.leftKnee.localRotation,p.march.rightKnee.localRotation)<5,"Both knees tuck together");
 Check(!course.players[1].IsJumping,"Other recruits do not copy local jump");
 p.InputOverride=0;await Task.Delay((int)(p.jumpDuration*1000));
 Check(!p.IsJumping&&Mathf.Abs(p.transform.localPosition.y-ground)<.001f,"Landing restores exact lane height");
 Check(p.TryJump(),"A new press can jump again after landing");
 await Task.Delay(100);
 var trap=course.traps[0];p.PinTo(trap.leftBank,true,trap.spikeReach);
 Check(!p.IsJumping&&Mathf.Abs(p.transform.localPosition.y-ground)<.001f,"Spike contact cancels jump without floating");
 Check(!p.TryJump(),"Pinned recruit cannot jump");
 p.ResetPlayer();p.Eliminate();Check(!p.TryJump(),"Dead recruit cannot jump");
 p.ResetPlayer();p.NetworkReplica=true;p.ApplyNetworkJump(true,.5f);
 Check(p.IsJumping&&Mathf.Abs(p.transform.localPosition.y-ground-p.jumpHeight)<.02f,"Replica renders host jump phase and height");
 p.ApplyNetworkJump(false,1);
 Check(!p.IsJumping&&Mathf.Abs(p.transform.localPosition.y-ground)<.001f,"Replica landing returns to ground");
 p.NetworkReplica=false;p.TryJump();p.ResetPlayer();
 Check(!p.IsJumping&&p.LandingAmount==0,"Round reset clears jump and landing animation");
 return checks+" recruit jump checks passed";
}
finally
{
 foreach(var p in course.players)p.NetworkReplica=false;
 course.round.leadInSeconds=lead;menu.Open();
}

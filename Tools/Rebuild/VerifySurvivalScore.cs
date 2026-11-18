using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();var course=menu.course;var score=menu.Scores;
var overrides=course.GetComponentsInChildren<RecruitController>(true).ToDictionary(p=>p,p=>p.InputOverride);
string key="Rebuild.SurvivalBestSeconds";bool had=PlayerPrefs.HasKey(key);float old=PlayerPrefs.GetFloat(key);
float lead=course.round.leadInSeconds,reaction=course.reactionBeats;
int checks=0;void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
try
{
 menu.Open();menu.SetPlayerCount(4);course.round.leadInSeconds=.3f;course.reactionBeats=10000;
 menu.Play();foreach(var p in course.players)p.InputOverride=0;
 await Task.Delay(100);
 Check(score.HasRound&&score.PersonalSeconds==0,"Countdown must not count as survival");
 await Task.Delay(450);
 Check(score.PersonalSeconds>0,"Timer starts at music downbeat");
 course.players[1].Eliminate();await Task.Delay(60);float dead=score.Times[1];
 await Task.Delay(150);
 Check(Mathf.Abs(score.Times[1]-dead)<.001f,"Eliminated recruit time stays frozen");
 Check(score.PersonalSeconds>dead,"Living recruit continues accumulating time");
 course.players[2].Eliminate();course.players[3].Eliminate();await Task.Delay(80);
 Check(course.IsGameOver&&score.Finished,"Completed round freezes results");
 float final=score.PersonalSeconds;await Task.Delay(150);
 Check(Mathf.Abs(score.PersonalSeconds-final)<.001f,"Victory playback adds no survival time");
 Check(PlayerPrefs.GetFloat(key)>=final,"Completed personal score is persisted");
 Check(SurvivalScore.Format(65.25f)=="01:05.25","Timer formatting includes minutes and hundredths");
 menu.Open();Check(!score.HasRound,"Main menu clears round timer");
 course.NetworkReplica=true;menu.Play();course.NetworkReplica=true;
 score.ApplyNetwork(12,new[]{5f,12f,9f,10f},false);
 Check(Mathf.Abs(score.PersonalSeconds-5)<.001f,"Online display accepts authoritative personal survival time");
 score.ApplyNetwork(13,new[]{5f,13f,9f,10f},true);
 score.ApplyNetwork(99,new[]{99f,99f,99f,99f},true);
 Check(score.Finished&&Mathf.Abs(score.PersonalSeconds-5)<.001f,"Final online result cannot be inflated by later cosmetic deaths");
 return checks+" survival score checks passed";
}
finally
{
 course.NetworkReplica=false;course.round.leadInSeconds=lead;course.reactionBeats=reaction;menu.Open();
 foreach(var entry in overrides)entry.Key.InputOverride=entry.Value;
 if(had)PlayerPrefs.SetFloat(key,old);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
}

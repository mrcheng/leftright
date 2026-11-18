using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var menu=UnityEngine.Object.FindAnyObjectByType<StartMenu>();menu.Open();
var course=menu.course;var round=course.round;var results=menu.Results;var victory=menu.GetComponent<VictoryDirector>();
var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
var field=typeof(SurvivalResults).GetField("store",flags);var oldStore=field.GetValue(results);
var fake=new SurvivalResultsFake();field.SetValue(results,fake);
var all=menu.GetComponentsInChildren<RecruitController>(true).OrderBy(p=>p.playerIndex).ToArray();
var z=all.Select(p=>p.transform.localPosition.z).ToArray();var lanes=menu.transform.Find("Player lanes - gameplay anchors");
float lead=round.leadInSeconds,reaction=course.reactionBeats,delay=victory.soloResultsDelaySeconds;int count=menu.PlayerCount;
string normalKey="Rebuild.SurvivalBestSeconds",soloKey="Rebuild.SurvivalSoloBestSeconds",nameKey="Rebuild.ScoreName";
bool hadNormal=PlayerPrefs.HasKey(normalKey),hadSolo=PlayerPrefs.HasKey(soloKey),hadName=PlayerPrefs.HasKey(nameKey);
float normal=PlayerPrefs.GetFloat(normalKey),solo=PlayerPrefs.GetFloat(soloKey);string name=PlayerPrefs.GetString(nameKey,"");
var checks=new List<string>();void Check(bool c,string label){if(!c)throw new Exception(label);checks.Add(label);}
async Task Wait(Func<bool> predicate){float until=Time.realtimeSinceStartup+4;while(!predicate()&&Time.realtimeSinceStartup<until)await Task.Delay(20);if(!predicate())throw new Exception("Timed out");}
async Task Finish(){course.players[0].Eliminate();await Wait(()=>results.Visible&&!results.Loading&&results.Table!=null);}
try
{
 round.leadInSeconds=.15f;course.reactionBeats=10000;victory.soloResultsDelaySeconds=.05f;
 menu.PlaySurvival();
 Check(menu.IsSurvival&&menu.PlayerCount==1&&course.players.Length==1&&course.traps.Length==1,"Survival uses one actual recruit and spike lane");
 Check(all.Count(p=>p.gameObject.activeSelf)==1&&!all[0].computerControlled&&all[0].isLocallyControlled,"Only the human recruit is active");
 Check(Enumerable.Range(1,4).Count(i=>lanes.Find("Player lane "+i).gameObject.activeSelf)==1,"Only one walkway is visible");
 Check(Mathf.Abs(all[0].transform.localPosition.z-lanes.Find("Player lane 1").localPosition.z)<.001f&&Mathf.Abs(all[0].transform.localPosition.z-z[3])<.001f,"Player, walkway and spikes align beside the instructor after reset");
 await Wait(()=>round.HasStarted);await Task.Delay(180);
 Check(!course.IsGameOver&&menu.Scores.PersonalSeconds>0,"Lone survivor keeps playing and the timer advances");
 Check(fake.Loads==0&&fake.Submits==0,"Playing does not submit or load a high score");
 await Finish();
 Check(course.IsGameOver&&course.Winner==null&&menu.Scores.Finished,"Death finishes the one-lane run");
 Check(!victory.IsPending&&!victory.IsPlaying&&!victory.IsReturning&&victory.SelectedSequence==null,"Survival skips every winner sequence");
 Check(victory.cheerSource==null||!victory.cheerSource.isPlaying,"Survival does not play a winner cheer");
 Check(results.Qualifies&&fake.Loads==1,"A finished Survival time can qualify for an empty global top 10");
 int final=results.PersonalScore;await Task.Delay(160);Check(results.PersonalScore==final,"Death/results animation cannot inflate the finished score");
 Check(PlayerPrefs.GetFloat(soloKey)>=menu.Scores.PersonalSeconds&&PlayerPrefs.GetFloat(normalKey)==normal,"Survival personal best stays separate from multiplayer bests");
 await results.SubmitAsync("  <>  ");Check(fake.Submits==0&&results.Message.Contains("name"),"An empty/sanitized name cannot submit");
 fake.Hold=true;var pending=results.SubmitAsync("Recruit One");await results.SubmitAsync("Duplicate");
 Check(fake.Submits==1&&results.Submitting,"Repeated taps create only one pending submission");
 fake.Complete();await pending;
 Check(results.Saved&&!results.Qualifies&&results.Table.entries.Length==1&&fake.Score==final,"Named submission saves the frozen personal score once");
 string oldRun=results.RunId;results.PlayAgain();await Wait(()=>round.HasStarted);
 Check(menu.IsSurvival&&course.players.Length==1&&!results.Saved&&results.RunId!=oldRun,"Play Again keeps Survival and clears only the previous result");
 Check(Mathf.Abs(all[0].transform.localPosition.z-lanes.Find("Player lane 1").localPosition.z)<.001f,"Replay keeps the recruit attached to its single lane");
 fake.Table=new SurvivalLeaderboardTable();await Finish();fake.Hold=true;oldRun=results.RunId;pending=results.SubmitAsync("Late Save");results.PlayAgain();fake.Complete();await pending;
 Check(!results.Visible&&!results.Saved&&results.RunId!=oldRun,"A late submission response cannot reopen or overwrite a new run");
 fake.Hold=false;fake.Table=new SurvivalLeaderboardTable{entries=Enumerable.Range(0,10).Select(i=>new SurvivalLeaderboardEntry{name="RECRUIT",runId="old"+i,hundredths=(10-i)*1000}).ToArray()};
 await Wait(()=>round.HasStarted);await Task.Delay(100);await Finish();int calls=fake.Submits;
 await results.SubmitAsync("Too Short");Check(!results.Qualifies&&fake.Submits==calls,"A run outside the top 10 cannot submit a name");
 Check(!SurvivalLeaderboardRules.Qualifies(fake.Table,1000)&&SurvivalLeaderboardRules.Qualifies(fake.Table,1001),"The tenth-place cut-off keeps older equal times");
 fake.Table.personalBest=5000;Check(!SurvivalLeaderboardRules.Qualifies(fake.Table,4000),"Global entries require improving the same player's existing best");
 Check(SurvivalLeaderboardRules.Score(1.15f)==115&&SurvivalLeaderboardRules.Time(6525)=="01:05.25","Leaderboard times match the game's hundredths formatting");
 Check(SurvivalLeaderboardRules.Name("<Åsa_1>")=="Åsa_1"&&SurvivalLeaderboardRules.Name(new string('A',30)).Length==14,"Names stay plain and fit the table");
 results.MainMenu();Check(menu.IsOpen&&!menu.IsSurvival&&course.players.Length==count,"Main Menu restores the multiplayer roster");
 Check(all.Select((p,i)=>Mathf.Abs(p.transform.localPosition.z-z[i])<.001f).All(value=>value),"Main Menu restores every original lane position");
 menu.SetPlayerCount(2);menu.Play();await Wait(()=>round.HasStarted);int loads=fake.Loads;
 course.players[1].Eliminate();await Wait(()=>course.IsGameOver);
 typeof(VictoryDirector).GetProperty("ShowRestartPrompt",flags).SetValue(victory,true);await Task.Delay(100);
 Check(results.Visible&&!menu.IsSurvival&&!results.Qualifies&&fake.Loads==loads,"Local multiplayer results never use the Survival leaderboard");
 await results.SubmitAsync("Not Survival");Check(fake.Submits==calls,"Other game modes cannot submit Survival records");
}
finally
{
 menu.Open();round.leadInSeconds=lead;course.reactionBeats=reaction;victory.soloResultsDelaySeconds=delay;
 field.SetValue(results,oldStore);menu.SetPlayerCount(count);
 if(hadNormal)PlayerPrefs.SetFloat(normalKey,normal);else PlayerPrefs.DeleteKey(normalKey);
 if(hadSolo)PlayerPrefs.SetFloat(soloKey,solo);else PlayerPrefs.DeleteKey(soloKey);
 if(hadName)PlayerPrefs.SetString(nameKey,name);else PlayerPrefs.DeleteKey(nameKey);
 results.DraftName=name;PlayerPrefs.Save();
}
return new{checksPassed=checks.Count,checks};

public sealed class SurvivalResultsFake:ISurvivalLeaderboard
{
 public string Scope=>"GLOBAL SURVIVAL";public int Loads,Submits,Score;public bool Hold;
 public SurvivalLeaderboardTable Table=new SurvivalLeaderboardTable();TaskCompletionSource<SurvivalLeaderboardSubmission> pending;
 string run,name;
 public Task<SurvivalLeaderboardTable> LoadAsync(){Loads++;return Task.FromResult(Table);}
 public Task<SurvivalLeaderboardSubmission> SubmitAsync(string id,string value,int score)
 {Submits++;run=id;name=value;Score=score;pending=new TaskCompletionSource<SurvivalLeaderboardSubmission>();if(!Hold)Complete();return pending.Task;}
 public void Complete(){Table=new SurvivalLeaderboardTable{personalBest=Score,entries=new[]{new SurvivalLeaderboardEntry{name=name,runId=run,hundredths=Score}}};pending.SetResult(new SurvivalLeaderboardSubmission{saved=true,table=Table});}
}

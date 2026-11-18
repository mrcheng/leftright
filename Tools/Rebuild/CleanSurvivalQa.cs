// Editor-only verification helper. Deletes only recorded QA player/run pairs,
// after checking live score metadata; never clears the leaderboard itself.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Unity.Services.Leaderboards;
using Unity.Services.Core.Editor.Environments;
using LeftRight.Rebuild;
using Newtonsoft.Json.Linq;
var records=JObject.Parse(File.ReadAllText("Logs/WebQA/survival-test-players.json"))["records"].Select(r=>new{playerId=(string)r["playerId"],runId=(string)r["runId"],name=(string)r["name"]}).ToArray();
if(records.Length==0)return new{removed=0};
if(records.Any(r=>!r.name.StartsWith("QA_")||string.IsNullOrEmpty(r.runId)||string.IsNullOrEmpty(r.playerId)))throw new Exception("Invalid QA record");
await UnityRelayConnection.Ready();
var ids=records.Select(r=>r.playerId).Distinct().ToList();
var page=await LeaderboardsService.Instance.GetScoresAsync(UnitySurvivalLeaderboard.DefaultId,new GetScoresOptions{Limit=100,IncludeMetadata=true});
var scores=page.Results.Where(score=>ids.Contains(score.PlayerId)).ToList();
foreach(var score in scores){var known=records.Single(r=>r.playerId==score.PlayerId);var meta=JObject.Parse(score.Metadata);if((string)meta["runId"]!=known.runId||(string)meta["name"]!=known.name)throw new Exception("Refusing to delete a different run");}
await EnvironmentsApi.Instance.RefreshAsync();EnvironmentsApi.Instance.SetActiveEnvironment("production");
var authoring=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Unity.Services.Leaderboards.Editor.Authoring");
var core=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Unity.Services.Leaderboards.Editor.Authoring.Core");
var servicesType=authoring.GetType("Unity.Services.Leaderboards.Editor.Authoring.LeaderboardAuthoringServices",true);
var flags=BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy;
var services=servicesType.GetProperty("Instance",flags).GetValue(null);
var contract=core.GetType("Unity.Services.Leaderboards.Authoring.Core.Service.ILeaderboardsClient",true);
var wrapper=servicesType.GetMethod("GetService").MakeGenericMethod(contract).Invoke(services,null);
var prepare=(Task)wrapper.GetType().GetMethod("Get").Invoke(wrapper,new object[]{UnitySurvivalLeaderboard.DefaultId,CancellationToken.None});await prepare;
var client=wrapper.GetType().GetField("m_Client",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(wrapper);
var requestType=authoring.GetType("Unity.Services.Leaderboards.Authoring.Client.Default.DeleteLeaderboardPlayerScoreRequest",true);
foreach(var score in scores){var request=Activator.CreateInstance(requestType,new object[]{Guid.Parse(CloudProjectSettings.projectId),EnvironmentsApi.Instance.ActiveEnvironmentId.Value,UnitySurvivalLeaderboard.DefaultId,score.PlayerId});var deletion=(Task)client.GetType().GetMethod("DeleteLeaderboardPlayerScoreAsync").Invoke(client,new object[]{request,null});await deletion;}
var remaining=await LeaderboardsService.Instance.GetScoresAsync(UnitySurvivalLeaderboard.DefaultId,new GetScoresOptions{Limit=100});
if(remaining.Results.Any(score=>ids.Contains(score.PlayerId)))throw new Exception("QA entries remain");
return new{removed=scores.Count,remaining=remaining.Results.Count};


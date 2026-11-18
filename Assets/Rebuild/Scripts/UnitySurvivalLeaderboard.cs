using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>One best Survival time per anonymous player, shared across devices.
    /// The service sorts descending and keeps the best score; names belong to the
    /// submitted score metadata, not to the unrelated room browser.</summary>
    public sealed class UnitySurvivalLeaderboard : ISurvivalLeaderboard
    {
        public const string DefaultId = "survival_all_time";
        public string Scope => "GLOBAL SURVIVAL";
        readonly string id;
        [Serializable] sealed class Metadata { public string name, runId; }
        public UnitySurvivalLeaderboard(string leaderboardId=DefaultId){id=leaderboardId;}
        public async Task<SurvivalLeaderboardTable> LoadAsync()
        {
            await UnityRelayConnection.Ready();
            var page=await LeaderboardsService.Instance.GetScoresAsync(id,new GetScoresOptions{Limit=10,IncludeMetadata=true});
            string player=AuthenticationService.Instance.PlayerId;
            var result=new SurvivalLeaderboardTable();
            result.personalBest=(int)Math.Min(int.MaxValue,page.Results.Where(entry=>entry.PlayerId==player).Select(entry=>entry.Score).DefaultIfEmpty(0).Max());
            result.entries=page.Results.Select(entry=>
            {
                Metadata metadata=null;
                try{if(!string.IsNullOrEmpty(entry.Metadata))metadata=JsonUtility.FromJson<Metadata>(entry.Metadata);}catch{/* Older entries can have no metadata. */}
                string name=SurvivalLeaderboardRules.Name(metadata?.name);
                return new SurvivalLeaderboardEntry{name=string.IsNullOrEmpty(name)?"RECRUIT":name,runId=metadata?.runId??"",
                    hundredths=(int)Math.Min(int.MaxValue,Math.Max(0,entry.Score))};
            }).Take(10).ToArray();
            return result;
        }
        public async Task<SurvivalLeaderboardSubmission> SubmitAsync(string runId,string name,int hundredths)
        {
            if(hundredths<=0||string.IsNullOrEmpty(runId)||string.IsNullOrEmpty(SurvivalLeaderboardRules.Name(name)))throw new ArgumentException("A completed run and name are required.");
            await UnityRelayConnection.Ready();
            await LeaderboardsService.Instance.AddPlayerScoreAsync(id,hundredths,new AddPlayerScoreOptions{
                Metadata=new Metadata{name=SurvivalLeaderboardRules.Name(name),runId=runId}});
            return new SurvivalLeaderboardSubmission{saved=true,table=await LoadAsync()};
        }
    }
}

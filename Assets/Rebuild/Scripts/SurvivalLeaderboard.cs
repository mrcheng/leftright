using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace LeftRight.Rebuild
{
    [Serializable]
    public sealed class SurvivalLeaderboardEntry
    {
        public string name, runId;
        public int hundredths;
    }

    public sealed class SurvivalLeaderboardTable
    {
        public SurvivalLeaderboardEntry[] entries = Array.Empty<SurvivalLeaderboardEntry>();
        public int personalBest;
    }

    public sealed class SurvivalLeaderboardSubmission
    {
        public SurvivalLeaderboardTable table;
        public bool saved;
    }

    public interface ISurvivalLeaderboard
    {
        string Scope { get; }
        Task<SurvivalLeaderboardTable> LoadAsync();
        Task<SurvivalLeaderboardSubmission> SubmitAsync(string runId, string name, int hundredths);
    }

    public static class SurvivalLeaderboardRules
    {
        public const int Limit = 10, NameLength = 14;
        public static int Score(float seconds) => float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0
            ? 0 : seconds>=int.MaxValue/100f?int.MaxValue:Mathf.FloorToInt(seconds*100f);
        public static string Name(string value) => new string((value??"").Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='_')
            .Take(NameLength).ToArray()).Trim();
        // An existing equal time wins a tie at the cut-off. A global player's own
        // previous best must also improve; AI results never enter this comparison.
        public static bool Qualifies(SurvivalLeaderboardTable table,int score) => table!=null&&score>0&&score>table.personalBest&&
            (table.entries.Length<Limit||score>table.entries[Limit-1].hundredths);
        public static string Time(int hundredths) => (hundredths/6000).ToString("00")+":"+
            ((hundredths/100)%60).ToString("00")+"."+(hundredths%100).ToString("00");
    }
}

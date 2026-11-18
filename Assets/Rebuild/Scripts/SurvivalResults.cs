using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LeftRight.Rebuild
{
    /// <summary>Completed-round presentation and opt-in named score submission.
    /// Backend requests cannot hold up replay/menu or mutate a later round's UI.</summary>
    public sealed class SurvivalResults : MonoBehaviour
    {
        public bool Visible => menu!=null&&!menu.IsOpen&&victory.ShowRestartPrompt&&menu.Scores.Finished;
        public bool Loading { get; private set; }
        public bool Submitting { get; private set; }
        public bool Saved { get; private set; }
        public bool Qualifies => menu.IsSurvival&&!Saved&&SurvivalLeaderboardRules.Qualifies(Table,PersonalScore);
        public bool CanReplay => menu.online==null||!menu.online.Active||menu.online.IsHost;
        public int PersonalScore => SurvivalLeaderboardRules.Score(menu.Scores.PersonalSeconds);
        public string Message { get; private set; } = "";
        public string DraftName = "";
        public string RunId { get; private set; }
        public SurvivalLeaderboardTable Table { get; private set; }
        public string Scope => store.Scope;
        public StartMenu Menu => menu;
        StartMenu menu;
        VictoryDirector victory;
        ISurvivalLeaderboard store;
        int epoch;
        [Serializable] public sealed class Row { public string name,time;public bool you,winner; }
        [Serializable] public sealed class BrowserState
        {
            public bool visible,survival,loading,submitting,saved,qualified,canReplay,newBest,hasTable;
            public string runId,status,personal,best,suggestedName,scope;
            public Row[] round;
            public SurvivalLeaderboardEntry[] top10;
        }
        public BrowserState State()
        {
            if(!Visible)return new BrowserState{visible=false,survival=menu.IsSurvival,runId=RunId};
            int local=menu.online!=null&&menu.online.Active?menu.online.LocalSlot:0;
            string[] names={"GREEN","BLUE","YELLOW","RED"};
            var ranking=Enumerable.Range(0,menu.Scores.Times.Length).OrderByDescending(i=>menu.course.players[i]==menu.course.Winner).ThenByDescending(i=>menu.Scores.Times[i]);
            return new BrowserState{visible=true,survival=menu.IsSurvival,loading=Loading,submitting=Submitting,saved=Saved,
                qualified=Qualifies,canReplay=CanReplay,newBest=menu.Scores.NewBest,hasTable=Table!=null,runId=RunId,
                status=Message,personal=SurvivalLeaderboardRules.Time(PersonalScore),best=SurvivalScore.Format(menu.Scores.BestSeconds),
                suggestedName=DraftName,scope=Scope,top10=Table?.entries??Array.Empty<SurvivalLeaderboardEntry>(),
                round=ranking.Select(i=>new Row{name=names[i],time=SurvivalScore.Format(menu.Scores.Times[i]),you=i==local,winner=menu.course.players[i]==menu.course.Winner}).ToArray()};
        }

        public void Initialize(StartMenu owner,VictoryDirector director,ISurvivalLeaderboard backend)
        {
            menu=owner;victory=director;store=backend;
            DraftName=PlayerPrefs.GetString("Rebuild.ScoreName","");
            menu.course.round.Preparing+=Reset;
            menu.course.round.Cancelled+=Reset;
            Reset();
        }
        void Reset()
        {
            epoch++;RunId=Guid.NewGuid().ToString("N");Table=null;
            Loading=Submitting=Saved=false;Message="";
        }
        void Update()
        {
            if(!Visible)return;
            if(menu.IsSurvival&&Table==null&&!Loading&&string.IsNullOrEmpty(Message))_=RefreshAsync();
            if(Keyboard.current!=null&&Keyboard.current.enterKey.wasPressedThisFrame)
            {
                if(Qualifies)SubmitName(DraftName);else if(CanReplay&&!Loading)PlayAgain();
            }
        }
        public void Refresh()=>_=RefreshAsync();
        public async Task RefreshAsync()
        {
            if(!Visible||!menu.IsSurvival||Loading||Submitting)return;
            int version=epoch;Loading=true;Message="Loading the top 10...";
            try
            {
                var table=await store.LoadAsync();if(this==null||epoch!=version)return;
                Table=table;Message="";
            }
            catch{if(this!=null&&epoch==version)Message="Could not load the top 10. Try again.";}
            finally{if(this!=null&&epoch==version)Loading=false;}
        }
        public void SubmitName(string value)=>_=SubmitAsync(value);
        public async Task SubmitAsync(string value)
        {
            if(!Visible||!Qualifies||Loading||Submitting)return;
            string name=SurvivalLeaderboardRules.Name(value);
            if(name.Length==0){Message="Enter a name first.";return;}
            // Capture the finished time and round identity before awaiting anything.
            // Repeated taps cannot duplicate a submission; replay/menu invalidate the
            // response without cancelling a score the player already chose to save.
            int version=epoch, score=PersonalScore;string run=RunId;
            Submitting=true;Message="Saving your time...";
            try
            {
                var result=await store.SubmitAsync(run,name,score);
                if(this==null||epoch!=version)return;
                Table=result.table;Saved=result.saved;DraftName=name;
                if(Saved){PlayerPrefs.SetString("Rebuild.ScoreName",name);PlayerPrefs.Save();}
                Message=Saved?(Table.entries.Any(entry=>entry.runId==run)?"YOUR TIME IS SAVED. HONOR TO YOU!":"SCORE SAVED. THE TOP 10 HAS CHANGED."):
                    "The top 10 changed. Try to beat it next round!";
            }
            catch{if(this!=null&&epoch==version)Message="Could not save your score. Try again.";}
            finally{if(this!=null&&epoch==version)Submitting=false;}
        }
        public void PlayAgain(){if(Visible&&CanReplay)victory.StartAgain();}
        public void MainMenu(){if(Visible)menu.Open();}
        void OnDestroy()
        {
            if(menu==null)return;
            menu.course.round.Preparing-=Reset;menu.course.round.Cancelled-=Reset;
        }
    }
}

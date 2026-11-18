using System;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Seconds survived, independent of victory animation or camera timing.
    /// The host supplies online results; each device saves only its own human's best.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class SurvivalScore : MonoBehaviour
    {
        const string MatchBestKey = "Rebuild.SurvivalBestSeconds", SoloBestKey = "Rebuild.SurvivalSoloBestSeconds";
        string BestKey => menu.IsSurvival?SoloBestKey:MatchBestKey;
        public float ElapsedSeconds { get; private set; }
        public float BestSeconds { get; private set; }
        public bool NewBest { get; private set; }
        public bool HasRound { get; private set; }
        public bool Finished { get; private set; }
        public float[] Times { get; private set; } = Array.Empty<float>();
        bool[] stopped = Array.Empty<bool>();
        SpikeCourse course;
        StartMenu menu;

        public void Initialize(SpikeCourse value, StartMenu owner)
        {
            course=value;menu=owner;BestSeconds=PlayerPrefs.GetFloat(BestKey,0);
            course.round.Preparing+=Begin;
            course.round.Cancelled+=Clear;
            course.Completed+=Complete;
        }
        void Clear()
        {
            HasRound=Finished=NewBest=false;ElapsedSeconds=0;
            Times=new float[course.players.Length];stopped=new bool[Times.Length];
        }
        void Begin(){Clear();BestSeconds=PlayerPrefs.GetFloat(BestKey,0);HasRound=true;}

        void LateUpdate()
        {
            if(!HasRound||Finished||course.NetworkReplica)return;
            Capture();
        }
        void Capture()
        {
            // Use the scheduled first downbeat, not wall time since opening the menu.
            // The same authoritative network timeline drives online music and scoring.
            var online=menu.online;
            double seconds=online!=null&&online.Active ? online.Now-online.StartTime
                : AudioSettings.dspTime-course.round.ScheduledStartDspTime;
            ElapsedSeconds=course.round.HasStarted?(float)Math.Max(0,seconds):0;
            for(int i=0;i<Times.Length;i++)
            {
                if(stopped[i])continue;
                Times[i]=ElapsedSeconds;
                if(course.players[i].IsEliminated)stopped[i]=true;
            }
        }
        void Complete(RecruitController winner)
        {
            if(!HasRound||Finished)return;
            Capture();Finished=true;SavePersonalBest();
        }
        public float PersonalSeconds
        {
            get
            {
                int slot=menu.online!=null&&menu.online.Active?menu.online.LocalSlot:0;
                return slot>=0&&slot<Times.Length?Times[slot]:0;
            }
        }
        void SavePersonalBest()
        {
            // A result is committed only once the round completes. AI records and
            // the extra seconds spent watching an ending never inflate the high score.
            float score=PersonalSeconds;
            NewBest=score>BestSeconds;
            if(!NewBest)return;
            BestSeconds=score;PlayerPrefs.SetFloat(BestKey,score);PlayerPrefs.Save();
        }
        public void ApplyNetwork(float elapsed,float[] times,bool finished)
        {
            if(!HasRound||Finished||times==null||times.Length!=course.players.Length)return;
            ElapsedSeconds=Mathf.Max(0,elapsed);Times=(float[])times.Clone();
            if(finished){Finished=true;SavePersonalBest();}
        }
        public static string Format(float seconds)
        {
            int hundredths=Mathf.FloorToInt(Mathf.Max(0,seconds)*100);
            return (hundredths/6000).ToString("00")+":"+((hundredths/100)%60).ToString("00")+"."+(hundredths%100).ToString("00");
        }
        void OnDestroy()
        {
            if(course==null)return;
            course.round.Preparing-=Begin;course.round.Cancelled-=Clear;course.Completed-=Complete;
        }
    }
}

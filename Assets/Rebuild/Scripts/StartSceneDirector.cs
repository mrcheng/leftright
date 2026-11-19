using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Per-round, equal 1/N opening selection and camera ownership. Online
    /// hosts select once; peers use the replicated index and scheduled first beat.</summary>
    [DefaultExecutionOrder(-90)]
    public sealed class StartSceneDirector : MonoBehaviour
    {
        public StartSequence[] sequences=Array.Empty<StartSequence>();
        public StartSequence SelectedSequence {get;private set;}
        public int SelectedIndex {get;private set;}=-1;
        public bool IsPlaying=>playback!=null;
        readonly System.Random random=new System.Random();
        StartMenu menu;StartPlayback playback;double firstBeat;float duration;
        public void Initialize(StartMenu owner)
        {menu=owner;owner.course.round.startScenes=this;owner.course.round.Cancelled+=Cancel;}
        public StartSequence[] AvailableSequences()
        {
            var pool=new List<StartSequence>();
            foreach(var sequence in sequences??Array.Empty<StartSequence>())if(sequence!=null&&sequence.available&&!pool.Contains(sequence))pool.Add(sequence);
            return pool.ToArray();
        }
        public int ChooseIndex()
        {var pool=AvailableSequences();return pool.Length==0?-1:Array.IndexOf(sequences,pool[random.Next(pool.Length)]);}
        public void Play(int index,double scheduledBeat,float totalSeconds)
        {
            Cancel();SelectedIndex=index>=0&&sequences!=null&&index<sequences.Length?index:-1;
            SelectedSequence=SelectedIndex>=0?sequences[SelectedIndex]:null;firstBeat=scheduledBeat;duration=Mathf.Clamp(totalSeconds,0,5);
            double remaining=firstBeat-AudioSettings.dspTime;
            // Very short externally scheduled countdowns and late peers simply
            // join gameplay, rather than squeezing a cutscene across the downbeat.
            if(SelectedSequence==null||duration<3||remaining<.15)return;
            menu.view.SnapToCourse();menu.view.CinematicActive=true;
            float elapsed=Mathf.Clamp(duration-(float)remaining,0,duration);
            playback=SelectedSequence.CreatePlayback(new StartContext(menu,duration,elapsed));playback.Tick(elapsed);
        }
        void Update()
        {if(playback!=null)playback.Tick(Mathf.Clamp(duration-(float)(firstBeat-AudioSettings.dspTime),0,duration));}
        public void Finish()
        {
            if(playback==null)return;
            playback.Tick(duration);playback.Stop();playback=null;
            menu.view.CinematicActive=false;menu.view.SnapToGameplay();
        }
        void Cancel()
        {
            if(playback!=null){playback.Stop();playback=null;if(menu!=null&&menu.view!=null){menu.view.CinematicActive=false;menu.view.SnapToCourse();}}
            SelectedSequence=null;SelectedIndex=-1;
        }
        void OnDestroy(){if(menu!=null)menu.course.round.Cancelled-=Cancel;Cancel();}
    }
}

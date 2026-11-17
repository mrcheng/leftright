using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LeftRight.Rebuild
{
    /// <summary>Opt-in standalone smoke-test driver. No command-line flag means this
    /// component does nothing. Reports contain game state only, never service credentials.</summary>
    public sealed class OnlineTestDriver : MonoBehaviour
    {
        OnlineMatch online;
        string reportPath,inputPath;
        float nextReport;
        bool autoStart;
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();
            if(!args.Contains("-lr-test")){enabled=false;yield break;}
            online=GetComponent<OnlineMatch>();
            string Value(string key,string fallback=""){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
            reportPath=Value("-lr-report");inputPath=Value("-lr-input-file");autoStart=args.Contains("-lr-autostart");
            if(ushort.TryParse(Value("-lr-port","7777"),out ushort port))online.port=port;
            yield return new WaitForSecondsRealtime(.5f);
            if(args.Contains("-lr-relay-host"))online.HostInternetFromMenu(int.Parse(Value("-lr-relay-host","2")));
            else if(args.Contains("-lr-relay-join"))online.JoinInternetFromMenu(Value("-lr-relay-join"));
            else if(args.Contains("-lr-host"))online.Host(int.Parse(Value("-lr-host","2")));
            else if(args.Contains("-lr-join"))online.Join(Value("-lr-join","127.0.0.1"));
        }
        void Update()
        {
            if(online==null)return;
            if(autoStart&&online.CanStart&&!online.RoundActive)online.RequestRound();
            if(Time.unscaledTime<nextReport)return;nextReport=Time.unscaledTime+.1f;
            try
            {
                if(!string.IsNullOrEmpty(inputPath)&&File.Exists(inputPath)&&float.TryParse(File.ReadAllText(inputPath),NumberStyles.Float,CultureInfo.InvariantCulture,out float input))online.TestInput=Mathf.Clamp(input,-1,1);
                if(!string.IsNullOrEmpty(reportPath))
                {
                    var course=online.menu.course;
                    var report=new Report{active=online.Active,host=online.IsHost,round=online.RoundNumber,slot=online.LocalSlot,
                        humans=online.HumanCount,snapshots=online.ReceivedSnapshots,beat=course.round.masterBeat.BeatPosition,
                        status=online.Status,joinCode=online.JoinCode,now=online.Now,startsAt=online.StartTime,over=course.IsGameOver,
                        winner=Array.IndexOf(course.players,course.Winner),sequence=Array.IndexOf(GetComponent<VictoryDirector>().sequences,GetComponent<VictoryDirector>().SelectedSequence),
                        x=course.players.Select(p=>p.transform.position.x).ToArray(),z=course.players.Select(p=>p.transform.position.z).ToArray(),
                        travel=course.traps.Select(t=>t.CurrentTravel).ToArray(),pending=course.traps.Select(t=>t.PendingRecoverySteps).ToArray(),
                        recovery=course.traps.Select(t=>t.RecoveryVersion).ToArray(),dead=course.players.Select(p=>p.IsEliminated).ToArray()};
                    File.WriteAllText(reportPath,JsonUtility.ToJson(report));
                }
            }
            catch(IOException){} // A test reader may briefly hold the report/input file.
        }
        [Serializable] sealed class Report
        {
            public bool active,host,over; public int round,slot,humans,snapshots,winner,sequence;
            public double beat,now,startsAt; public string status,joinCode;
            public float[] x,z,travel;public int[] pending,recovery;public bool[] dead;
        }
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if(!EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
var checks=new List<string>();
void Check(bool condition,string message) { if(!condition) throw new Exception(message);checks.Add(message); }
var course=UnityEngine.Object.FindAnyObjectByType<SpikeCourse>();
var round=course.round;
var speeds=course.players.Select(p=>p.walkSpeed).ToArray();
var visualScales=course.players.Select(p=>p.transform.Find("Model orientation").localScale).ToArray();
try
{
    foreach(var p in course.players) {p.InputOverride=0;p.walkSpeed=0;}
    round.BeginRound();
    while(!round.HasStarted) await Task.Delay(10);
    // Exercise both left and right attachment with the actual course contact handling.
    for(int i=0;i<2;i++)
    {
        var p=course.players[i];var trap=course.traps[i];
        var pos=p.transform.position;
        pos.x=i==0 ? trap.LeftTip+p.collisionRadius-.01f : trap.RightTip-p.collisionRadius+.01f;
        p.transform.position=pos;
    }
    await Task.Delay(80);
    Check(course.players.Take(2).All(p=>p.IsPinned && !p.IsEliminated && !p.march.enabled),"First contact pins both recruits and stops marching without eliminating them");
    Check(course.players[0].PinnedFromLeft && !course.players[1].PinnedFromLeft,"Correct bank owns each pinned recruit");
    Check(Vector3.Dot(-course.players[0].transform.forward,Vector3.right)>.999f &&
        Vector3.Dot(-course.players[1].transform.forward,Vector3.left)>.999f,
        "Pinned recruits face exactly left or right");
    Check(course.AliveCount==4 && !course.IsGameOver,"Pinned recruits remain alive until the opposite set arrives");
    for(int i=0;i<2;i++)
    {
        var p=course.players[i];var trap=course.traps[i];
        float depth=i==0 ? trap.LeftTip-p.transform.position.x : p.transform.position.x-trap.RightTip;
        Check(Mathf.Abs(depth-p.penetrationDepth)<.001f,"Spike tips visibly penetrate recruit "+i);
        p.InputOverride=i==0 ? 1 : -1;p.walkSpeed=10;
    }
    var offsets=course.players.Take(2).Select((p,i)=>p.transform.position.x-(i==0?course.traps[i].leftBank:course.traps[i].rightBank).position.x).ToArray();
    var positions=course.players.Take(2).Select(p=>p.transform.position.x).ToArray();
    foreach(var trap in course.traps.Take(2)) trap.MissBeat();
    await Task.Delay(300);
    Check(course.players.Take(2).All(p=>!p.IsMoving && p.Horizontal==0),"Input cannot move a pinned recruit");
    Check(course.players[0].transform.position.x>positions[0]+.2f && course.players[1].transform.position.x<positions[1]-.2f,"Left and right sets carry their recruits inward");
    Check(course.players.Take(2).Select((p,i)=>Mathf.Abs(p.transform.position.x-(i==0?course.traps[i].leftBank:course.traps[i].rightBank).position.x-offsets[i])<.001f).All(v=>v),"Pinned recruits keep their exact attachment offset while travelling");
    round.audioOutput.music.Pause();await Task.Delay(60);
    positions=course.players.Take(2).Select(p=>p.transform.position.x).ToArray();
    await Task.Delay(200);
    Check(course.players.Take(2).Select((p,i)=>Mathf.Abs(p.transform.position.x-positions[i])<.001f).All(v=>v),"Music pause holds the attached recruits and spike banks");
    round.audioOutput.music.UnPause();
    // Queue full closure; pinned lanes continue advancing on beats because they cannot move.
    foreach(var trap in course.traps.Take(2)) for(int i=0;i<30;i++) trap.MissBeat();
    float until=Time.realtimeSinceStartup+6;
    while(!course.players.Take(2).All(p=>p.IsEliminated) && Time.realtimeSinceStartup<until) await Task.Delay(20);
    Check(course.players.Take(2).All(p=>p.IsEliminated && p.IsPinned) && course.AliveCount==2,"Opposite spike contact completes elimination while bodies stay attached");
    await Task.Delay(150);
    Check(Vector3.Dot(-course.players[0].transform.forward,Vector3.right)>.999f &&
        Vector3.Dot(-course.players[1].transform.forward,Vector3.left)>.999f,
        "Squeezed recruits retain their sideways facing after elimination");
    foreach(var trap in course.traps.Skip(2)) for(int i=0;i<30;i++) trap.MissBeat();
    until=Time.realtimeSinceStartup+6;
    while(!course.IsGameOver && Time.realtimeSinceStartup<until) await Task.Delay(20);
    Check(course.IsGameOver && course.AliveCount==1 && course.Winner!=null,"Final opposite-set contacts leave a protected winner");
    until=Time.realtimeSinceStartup+3;
    while(!course.traps.Select((t,i)=>!course.players[i].IsEliminated||t.HasFinishedSqueeze).All(v=>v)&&Time.realtimeSinceStartup<until)await Task.Delay(10);
    Check(course.traps.Select((t,i)=>!course.players[i].IsEliminated||t.HasFinishedSqueeze).All(v=>v),
        "Losing sets complete their one-plank crush before returning");
    Check(course.players.Select((p,i)=>!p.IsEliminated||p.transform.Find("Model orientation").localScale.z<visualScales[i].z*.7f).All(v=>v),
        "Losing recruit meshes visibly compress along the closing direction");
    Check(course.players.Select((p,i)=>!p.IsEliminated||Mathf.Abs(p.transform.position.x-(course.traps[i].leftBank.position.x+course.traps[i].rightBank.position.x)*.5f)<.001f).All(v=>v),
        "Compressed recruits remain between the pillar faces");
    round.BeginRound();
    Check(course.players.All(p=>!p.IsPinned && !p.IsEliminated && p.march.enabled && Mathf.Abs(p.transform.localPosition.x)<.001f),"Restart releases every attachment and restores recruits to center");
    Check(course.players.Select((p,i)=>p.transform.Find("Model orientation").localScale==visualScales[i]).All(v=>v),
        "Restart restores each recruit's original mesh proportions");
}
finally
{
    for(int i=0;i<course.players.Length;i++){course.players[i].InputOverride=null;course.players[i].walkSpeed=speeds[i];}
    round.BeginRound();
}
return new {checksPassed=checks.Count,checks};

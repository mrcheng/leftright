using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var round=UnityEngine.Object.FindAnyObjectByType<RebuildRoundStart>();
var officer=round.officer;var lights=UnityEngine.Object.FindAnyObjectByType<DirectionFloodlights>();
var savedRandom=UnityEngine.Random.state;var checks=new List<string>();
void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
int left=0,right=0;
try
{
 round.CancelStart();
 for(int seed=0;seed<16;seed++)
 {
  UnityEngine.Random.InitState(seed);
  bool expected=UnityEngine.Random.Range(0,2)==1;
  float delay=UnityEngine.Random.Range(officer.minTimeTurn,officer.maxTimeTurn);
  UnityEngine.Random.InitState(seed);
  officer.ActivateDirections(true);
  Check(officer.DirectionRight==expected&&Mathf.Abs(officer.CurrentInterval-delay)<.0001f,
    "Opening "+seed+" samples an independent direction before its next random wait");
  Check(lights.rightLight.enabled==expected&&lights.leftLight.enabled==!expected&&
    officer.audioOutput.voice.clip==(expected?officer.audioOutput.palette.rightCall:officer.audioOutput.palette.leftCall),
    "Opening "+seed+" keeps voice and floodlight consistent with its random direction");
  if(expected)right++;else left++;
 }
 Check(left>0&&right>0,"Fresh activations can start in either direction");
}
finally{UnityEngine.Random.state=savedRandom;round.BeginRound();}
return new{checksPassed=checks.Count,leftOpenings=left,rightOpenings=right,checks};

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;
if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first.");
var checks=new List<string>();void Check(bool c,string m){if(!c)throw new Exception(m);checks.Add(m);}
var lens=UnityEngine.Object.FindAnyObjectByType<LensSplatter>();lens.course.round.CancelStart();
var camera=lens.gameCamera;var canvas=lens.Overlay.GetComponentInParent<Canvas>();
var mode=canvas.renderMode;var world=canvas.worldCamera;float distance=canvas.planeDistance;
var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;float aspect=camera.aspect;
const int width=1280,height=720;
var target=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
Color32[] Capture(string path)
{
 Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
 texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
 if(path!=null)File.WriteAllBytes(path,texture.EncodeToPNG());return texture.GetPixels32();
}
try
{
 Check(!ShaderUtil.ShaderHasError(lens.RuntimeMaterial.shader),"Lens shader compiles without errors");
 canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;
 camera.targetTexture=target;camera.aspect=(float)width/height;
 lens.Clear();var baseline=Capture(null);
 foreach(var player in lens.course.players)lens.AddSplatter(player.transform.position);
 lens.RuntimeMaterial.SetFloat("_Aspect",(float)width/height);
 lens.RuntimeMaterial.SetFloat("_Now",Time.unscaledTime+.25f);
 var early=Capture("Temp/lens-splatter-early.png");
 lens.RuntimeMaterial.SetFloat("_Now",Time.unscaledTime+5f);
 var later=Capture("Temp/lens-splatter-dripping.png");
 int earlyPixels=0,latePixels=0;double earlyY=0,lateY=0;
 for(int i=0;i<baseline.Length;i++)
 {
  int a=Math.Abs(early[i].r-baseline[i].r)+Math.Abs(early[i].g-baseline[i].g)+Math.Abs(early[i].b-baseline[i].b);
  int b=Math.Abs(later[i].r-baseline[i].r)+Math.Abs(later[i].g-baseline[i].g)+Math.Abs(later[i].b-baseline[i].b);
  if(a>20){earlyPixels++;earlyY+=i/width;}
  if(b>20){latePixels++;lateY+=i/width;}
 }
 Check(earlyPixels>300,"Death splats are visible in rendered camera pixels");
 Check(earlyPixels<width*height*.15&&latePixels<width*height*.15,"Four deaths leave most of the game view clear");
 Check(latePixels>300&&lateY/latePixels<earlyY/earlyPixels-2,"Visible blood moves downward over time");
 lens.RuntimeMaterial.SetFloat("_Now",Time.unscaledTime+20);
 var expired=Capture(null);int remaining=0;
 for(int i=0;i<baseline.Length;i++)if(Math.Abs(expired[i].r-baseline[i].r)+Math.Abs(expired[i].g-baseline[i].g)+Math.Abs(expired[i].b-baseline[i].b)>20)remaining++;
 Check(remaining<100,"Shader fades old splats completely away");
}
finally
{
 lens.Clear();canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=distance;
 camera.targetTexture=oldTarget;camera.aspect=aspect;RenderTexture.active=oldActive;
 UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);lens.course.round.BeginRound();
}
return new{checksPassed=checks.Count,checks};

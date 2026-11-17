using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

var checks=new List<string>();
void Check(bool condition,string description)
{
 if(!condition) throw new Exception(description);
 checks.Add(description);
}
var camera=Camera.main;
var sky=RenderSettings.skybox;
Check(camera!=null && camera.clearFlags==CameraClearFlags.Skybox,"Game camera draws the skybox");
Check(sky!=null && sky.shader.name=="LeftRight/Storm Evening Sky" && sky.shader.isSupported,"Storm sky material uses a supported shader");
Check(ShaderUtil.GetShaderMessages(sky.shader).All(m=>m.severity.ToString()!="Error"),"Sky shader has no compilation errors");
Check(sky.GetFloat("_WindSpeed")>0,"Clouds drift slowly in the wind");
Check(RenderSettings.sun!=null && RenderSettings.sun.intensity>=.85f,"Evening sunlight stays bright enough for gameplay");
Check(RenderSettings.ambientSkyColor.grayscale>.4f,"Cool ambient fill keeps shadowed scenery readable");
Check(RenderSettings.fog && RenderSettings.fogStartDistance>=30 && RenderSettings.fogEndDistance>RenderSettings.fogStartDistance,"Damp atmospheric haze is configured");
Check(Quaternion.Angle(camera.transform.rotation,Quaternion.Euler(24.408024f,0,0))<.001f
    && Vector3.Distance(camera.transform.position,new Vector3(0,7.567722f,-11.445186f))<.001f,
    "User's close, horizontally straight framing is preserved");
var previous=RenderTexture.active;
var previousTarget=camera.targetTexture;
var rt=new RenderTexture(800,450,24);
var texture=new Texture2D(800,450,TextureFormat.RGB24,false);
try
{
 camera.targetTexture=rt;
 camera.Render();
 RenderTexture.active=rt;
 texture.ReadPixels(new Rect(0,0,800,450),0,0);
 texture.Apply();
 var pixels=texture.GetPixels(20,430,760,20).Select(c=>c.grayscale).ToArray();
 float mean=pixels.Average();
 double variance=pixels.Average(v=>Math.Pow(v-mean,2));
 Check(mean>.1f,"Rendered sky remains evening rather than black night");
 Check(variance>.001,"Rendered sky has visible cloud formations rather than a flat background");
 Check(!texture.GetPixels().Any(c=>c.r>.95f && c.b>.95f && c.g<.05f),"No shader-error magenta appears in the scene");
}
finally
{
 camera.targetTexture=previousTarget;
 RenderTexture.active=previous;
 UnityEngine.Object.DestroyImmediate(rt);
 UnityEngine.Object.DestroyImmediate(texture);
}
return new{checksPassed=checks.Count,checks};

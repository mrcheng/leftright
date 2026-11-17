using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using LeftRight.Rebuild;

if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode on Main first.");
var checks = new List<string>();
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks.Add(description);
}
var flags = UnityEngine.Object.FindObjectsByType<WindFlag>();
Check(flags.Length == 2, "Both flags have wind animation");
var source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Rebuild/Art/Outpost/Models/FlagCloth.asset");
var sourceVertices = source.vertices;
Check(source.vertexCount > 200, "Fabric has enough subdivisions to bend into ripples");
var instances = flags.Select(f => f.GetComponent<MeshFilter>().sharedMesh).ToArray();
Check(instances.All(m => m != source) && instances[0] != instances[1], "Flags own separate animated meshes");
Check(flags.All(f => Vector3.Dot(f.transform.TransformDirection(Vector3.right), Vector3.left) > .9999f),
    "Both flags extend in the original right flag's world direction");
Check(flags[0].phaseOffset == flags[1].phaseOffset, "Both flags use the same wind phase");
var poles = flags.Select(f => f.transform.parent.Find("Flagpole")).ToArray();
var polePositions = poles.Select(p => p.position).ToArray();
var poleRotations = poles.Select(p => p.rotation).ToArray();
var before = instances.Select(m => m.vertices).ToArray();
await Task.Delay(650);
Check(instances[0].vertices.Zip(instances[1].vertices, (a,b) => Vector3.Distance(a,b)).All(d => d < .00001f),
    "Both fabrics ripple together in the same wind");
for(int i=0;i<flags.Length;i++)
{
    var mesh = instances[i];
    var after = mesh.vertices;
    var uv = mesh.uv;
    var pinned = Enumerable.Range(0,after.Length).Where(v => uv[v].x == 0f).ToArray();
    var tips = Enumerable.Range(0,after.Length).Where(v => uv[v].x == 1f).ToArray();
    Check(pinned.All(v => Vector3.Distance(before[i][v],after[v]) < .000001f), "Flag " + (i+1) + ": pole edge stays pinned");
    Check(tips.Max(v => Vector3.Distance(before[i][v],after[v])) > .025f, "Flag " + (i+1) + ": free edge visibly sways");
    Check(after.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), "Flag " + (i+1) + ": cloth vertices remain finite");
    Check(mesh.normals.Length == mesh.vertexCount && mesh.normals.All(n => n.sqrMagnitude > .9f), "Flag " + (i+1) + ": normals update for wind lighting");
    Check(after.All(v => mesh.bounds.Contains(v)), "Flag " + (i+1) + ": bounds follow the moving cloth");
    Check(flags[i].GetComponent<Renderer>().sharedMaterial.GetFloat("_Cull") == 0,
        "Flag " + (i+1) + ": fabric is visible from both sides");
    Check(poles[i].position == polePositions[i] && Quaternion.Angle(poles[i].rotation,poleRotations[i]) < .001f,
        "Flag " + (i+1) + ": pole stays rigid");
}
Check(source.vertices.SequenceEqual(sourceVertices), "Original shared cloth asset is unchanged by animation");
flags[0].enabled=false;
Check(flags[0].GetComponent<MeshFilter>().sharedMesh == source, "Disabling animation restores the source mesh");
flags[0].enabled=true;
await Task.Delay(50);
Check(flags[0].GetComponent<MeshFilter>().sharedMesh != source && flags[0].GetComponent<MeshFilter>().sharedMesh != instances[0],
    "Re-enabling animation creates a fresh instance");
Check(UnityEngine.Object.FindAnyObjectByType<OfficerDirections>() != null,
    "Animated officer is retained alongside the flags");
return new { checksPassed=checks.Count, checks };

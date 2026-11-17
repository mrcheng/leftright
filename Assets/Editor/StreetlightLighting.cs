using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LeftRight.Editor
{
    public static class StreetlightLighting
    {
        [MenuItem("LeftRight/Rebuild/Update streetlamp lighting")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode before updating streetlights.");
            const string path = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AddTo(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject environment)
        {
            OutpostSceneBuilder.RefreshOutpostMaterialBindings(environment);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/Art/Outpost/Materials/Lamp.mat");
            material.SetColor("_EmissionColor", new Color(1f, .62f, .20f) * 2.7f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);

            var lamps = environment.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "CampLamp" &&
                    (t.parent.name == "Command post" || t.parent.name == "Barracks and supply camp")).ToArray();
            var lights = environment.GetComponentsInChildren<Light>()
                .Where(l => l.type == LightType.Point && l.name == "Warm camp lamp").ToList();
            if (lamps.Length != 4 || lights.Count != 4)
                throw new InvalidOperationException("Expected four streetlamp fixtures and point lights.");
            foreach (var lamp in lamps)
            {
                var light = lights.OrderBy(l => (l.transform.position - lamp.position).sqrMagnitude).First();
                lights.Remove(light);
                light.transform.SetParent(lamp, true);
                // The model's head extends half a metre from its pole.
                // Convert from the FBX's local coordinates using the actual mesh.
                var meshFilter = lamp.GetComponentInChildren<MeshFilter>();
                var renderer = meshFilter.GetComponent<Renderer>();
                int lens = Array.FindIndex(renderer.sharedMaterials, m => m.name == "Lamp");
                var mesh = meshFilter.sharedMesh;
                var indices = mesh.GetIndices(lens).Distinct().ToArray();
                var vertices = mesh.vertices;
                Vector3 center = indices.Select(i => meshFilter.transform.TransformPoint(vertices[i]))
                    .Aggregate(Vector3.zero, (a, b) => a + b) / indices.Length;
                light.transform.position = center - Vector3.up * .10f;
                bool nearCommandPost = Mathf.Abs(lamp.position.x) < 10;
                light.lightUnit = LightUnit.Candela;
                light.intensity = nearCommandPost ? 10 : 14;
                light.range = nearCommandPost ? 8 : 9;
                light.color = new Color(1f, .74f, .45f);
                light.renderMode = LightRenderMode.ForcePixel;
                light.shadows = LightShadows.None;
            }
        }
    }
}

using System;
using System.Linq;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LeftRight.Editor
{
    public static class TowerFloodlights
    {
        const string Art = "Assets/Rebuild/Art/Outpost/";
        const string PrefabPath = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";

        [MenuItem("LeftRight/Rebuild/Install tower floodlights")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode before installing floodlights.");
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                AddTo(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject environment)
        {
            EnsureLensMaterial();
            var towers = environment.GetComponentsInChildren<Transform>()
                .Where(t => t.name == "Watchtower" && t.parent.name == "Barracks and supply camp")
                .OrderBy(t => t.position.x).ToArray();
            if (towers.Length != 2) throw new InvalidOperationException("Expected two outpost watchtowers.");
            var lights = new Light[2];
            var lamps = new Renderer[2];
            var lampMaterialIndices = new int[2];
            for (int i = 0; i < towers.Length; i++)
            {
                var tower = towers[i];
                string name = i == 0 ? "Left tower floodlight" : "Right tower floodlight";
                var previous = tower.Find(name);
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var fixture = new GameObject(name).transform;
                fixture.SetParent(tower, false);
                fixture.localPosition = new Vector3(0, 6.35f, 0);
                Model("FloodlightMount", fixture);
                var head = new GameObject("Aim toward play area").transform;
                head.SetParent(fixture, false);
                head.localPosition = new Vector3(0, .72f, 0);
                Vector3 target = new Vector3(i == 0 ? -3 : 3, 1f, .8f);
                head.rotation = Quaternion.LookRotation(target - head.position, Vector3.up);
                Model("FloodlightHead", head);

                var emitter = new GameObject("Warm white floodlight");
                emitter.transform.SetParent(head, false);
                emitter.transform.localPosition = new Vector3(0, 0, .40f);
                var light = emitter.AddComponent<Light>();
                light.type = LightType.Spot;
                light.lightUnit = LightUnit.Candela;
                light.color = new Color(1f, .92f, .78f);
                light.intensity = 1000;
                light.range = 45;
                light.spotAngle = 60;
                light.innerSpotAngle = 38;
                light.renderMode = LightRenderMode.ForcePixel;
                // The shared prototype URP asset has additional-light shadows off.
                light.shadows = LightShadows.None;
                lights[i] = light;
                lamps[i] = head.GetComponentInChildren<Renderer>();
                lampMaterialIndices[i] = Array.FindIndex(lamps[i].sharedMaterials, m => m.name == "FloodLamp");
            }
            var cues = environment.GetComponent<DirectionFloodlights>() ?? environment.AddComponent<DirectionFloodlights>();
            cues.officer = environment.GetComponentInChildren<OfficerDirections>();
            cues.leftLight = lights[0];
            cues.rightLight = lights[1];
            cues.leftLamp = lamps[0];
            cues.rightLamp = lamps[1];
            cues.leftLampMaterialIndex = lampMaterialIndices[0];
            cues.rightLampMaterialIndex = lampMaterialIndices[1];
        }

        static void EnsureLensMaterial()
        {
            string path = Art + "Materials/FloodLamp.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(.95f, .94f, .80f));
            material.SetColor("_EmissionColor", new Color(1f, .94f, .80f) * 3.5f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.EnableKeyword("_EMISSION");
            material.SetFloat("_Smoothness", .25f);
            EditorUtility.SetDirty(material);
        }

        static void Model(string name, Transform parent)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/" + name + ".fbx");
            if (model == null) throw new InvalidOperationException("Missing model: " + name);
            var conversion = new GameObject(name).transform;
            conversion.SetParent(parent, false);
            conversion.localRotation = Quaternion.Euler(0, 180, 0);
            PrefabUtility.InstantiatePrefab(model, conversion);
            foreach (var renderer in conversion.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + m.name + ".mat")
                    ?? throw new InvalidOperationException("Missing floodlight material: " + m.name)).ToArray();
        }
    }
}

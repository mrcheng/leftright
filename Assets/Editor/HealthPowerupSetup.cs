using System;
using System.Linq;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class HealthPowerupSetup
    {
        [MenuItem("LeftRight/Rebuild/Update health crate powerup")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode.");
            const string path = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var environment = PrefabUtility.LoadPrefabContents(path);
            try { AddTo(environment); PrefabUtility.SaveAsPrefabAsset(environment, path); }
            finally { PrefabUtility.UnloadPrefabContents(environment); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject environment)
        {
            const string path = "Assets/Rebuild/Prefabs/HealthCrate.prefab";
            const string burstPath = "Assets/Rebuild/Prefabs/HealthCrateBurst.prefab";
            var burstRoot = new GameObject("Health crate burst and syringe");
            try
            {
                var burstModel = MakeVisual(burstRoot.transform, true);
                var parts = burstModel.GetComponentsInChildren<Transform>();
                var burst = burstRoot.AddComponent<HealthCrateBurst>();
                burst.lid = parts.Single(t => t.name == "Lid");
                burst.walls = new[] { "WallFront", "WallBack", "WallLeft", "WallRight" }
                    .Select(name => parts.Single(t => t.name == name)).ToArray();
                burst.syringe = parts.Single(t => t.name == "Syringe");
                PrefabUtility.SaveAsPrefabAsset(burstRoot, burstPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(burstRoot); }
            var root = new GameObject("Health crate - retreats spike banks");
            try
            {
                var visual = new GameObject("Wooden medical crate").transform;
                visual.SetParent(root.transform, false);
                var model = MakeVisual(visual, false);
                model.GetComponentsInChildren<Transform>().Single(t => t.name == "Syringe").gameObject.SetActive(false);
                var pickup = root.AddComponent<HealthCratePickup>();
                pickup.visual = visual;
                pickup.burstPrefab = AssetDatabase.LoadAssetAtPath<HealthCrateBurst>(burstPath);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var group = environment.transform.Find("Powerups");
            if (group == null)
            {
                group = new GameObject("Powerups").transform;
                group.SetParent(environment.transform, false);
            }
            var spawner = group.GetComponent<HealthCrateSpawner>() ?? group.gameObject.AddComponent<HealthCrateSpawner>();
            spawner.course = environment.GetComponent<SpikeCourse>();
            spawner.cratePrefab = AssetDatabase.LoadAssetAtPath<HealthCratePickup>(path);
            // Apply the agreed baseline to existing prefabs as well as newly generated scenes.
            spawner.firstSpawnBeat = 8;
            spawner.intervalBeats = 16;
            spawner.lifetimeBeats = 12;
            spawner.restoredBeats = 3;
            spawner.minimumLostBeats = 2;
        }
        static GameObject MakeVisual(Transform parent, bool transparent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/Art/Outpost/Models/HealthCrate.fbx");
            if (asset == null) throw new InvalidOperationException("Generate HealthCrate.fbx first.");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    const string art = "Assets/Rebuild/Art/Outpost/Materials/";
                    var original = AssetDatabase.LoadAssetAtPath<Material>(art + source.name + ".mat");
                    if (!transparent) return original;
                    string fadePath = art + "CrateBurst" + source.name + ".mat";
                    var fade = AssetDatabase.LoadAssetAtPath<Material>(fadePath);
                    if (fade == null)
                    {
                        fade = new Material(original); AssetDatabase.CreateAsset(fade, fadePath);
                    }
                    fade.SetFloat("_Surface", 1);
                    fade.SetFloat("_Blend", 0);
                    fade.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    fade.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    fade.SetFloat("_ZWrite", 0);
                    fade.SetOverrideTag("RenderType", "Transparent");
                    fade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    fade.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    fade.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    EditorUtility.SetDirty(fade);
                    return fade;
                }).ToArray();
                if (transparent) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return model;
        }

    }
}

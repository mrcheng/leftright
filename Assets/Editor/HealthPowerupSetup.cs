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
            var root = new GameObject("Health crate - retreats spike banks");
            try
            {
                var visual = new GameObject("Wooden medical crate").transform;
                visual.SetParent(root.transform, false);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Rebuild/Art/Outpost/Models/HealthCrate.fbx");
                if (asset == null) throw new InvalidOperationException("Generate HealthCrate.fbx first.");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, visual);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                        AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/Art/Outpost/Materials/" + m.name + ".mat")).ToArray();
                var pickup = root.AddComponent<HealthCratePickup>();
                pickup.visual = visual;
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
    }
}

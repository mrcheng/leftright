using System;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class RoundStartSetup
    {
        [MenuItem("LeftRight/Rebuild/Update round start timing")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode before updating round timing.");
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
            var start = environment.GetComponent<RebuildRoundStart>() ?? environment.AddComponent<RebuildRoundStart>();
            start.audioOutput = environment.GetComponentInChildren<OutpostAudio>();
            start.officer = environment.GetComponentInChildren<OfficerDirections>();
            start.march = environment.GetComponentInChildren<OfficerMarch>();
            start.officer.playOnStart = false;
            start.leadInSeconds = 3f;
        }
    }
}

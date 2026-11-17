using System;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class GameViewSetup
    {
        [MenuItem("LeftRight/Rebuild/Update local and online camera modes")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode.");
            const string path = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { AddTo(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject root)
        {
            var settings = root.GetComponent<GameViewSettings>() ?? root.AddComponent<GameViewSettings>();
            settings.course = root.GetComponent<SpikeCourse>();
            settings.gameCamera = root.GetComponentInChildren<Camera>();
        }
    }
}

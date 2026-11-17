using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class PrototypeTools
    {
        public const string PrototypeScene = "Assets/Prototype/Scenes/Prototype.unity";
        public const string RebuildScene = "Assets/Rebuild/Scenes/Main.unity";

        [MenuItem("LeftRight/Open prototype reference")]
        public static void OpenPrototype() => Open(PrototypeScene);

        [MenuItem("LeftRight/Open rebuild")]
        public static void OpenRebuild() => Open(RebuildScene);

        static void Open(string path)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit Play Mode before switching scenes.");
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }

        [MenuItem("LeftRight/Validate scenes")]
        public static void ValidateScenes()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play Mode and save scene edits before validating.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { PrototypeScene, RebuildScene })
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    int missing = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
                    if (missing != 0)
                        throw new InvalidOperationException($"{path} has {missing} missing scripts.");
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("Prototype and rebuild scenes validated: no missing scripts.");
        }

        [MenuItem("LeftRight/Build prototype for Windows")]
        public static void BuildPrototypeWindows()
        {
            ValidateScenes();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PrototypeScene },
                locationPathName = "Builds/Prototype/LeftRightPrototype.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Prototype build failed: {report.summary.totalErrors} errors.");
        }
    }
}

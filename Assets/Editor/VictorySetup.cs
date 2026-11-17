using System;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class VictorySetup
    {
        [MenuItem("LeftRight/Rebuild/Update victory system")]
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
            const string folder = "Assets/Rebuild/Victories";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Rebuild", "Victories");
            const string path = folder + "/TurnAndRaiseArms.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<ArmsRaisedVictory>(path);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<ArmsRaisedVictory>();
                AssetDatabase.CreateAsset(sequence, path);
            }
            var director = root.GetComponent<VictoryDirector>() ?? root.AddComponent<VictoryDirector>();
            director.course = root.GetComponent<SpikeCourse>();
            director.view = root.GetComponent<GameViewSettings>();
            director.gameCamera = root.GetComponentInChildren<Camera>();
            director.cheerClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Rebuild/Audio/Victory/Cheers.ogg");
            var cheer = root.transform.Find("Victory cheer");
            if (cheer == null)
            {
                cheer = new GameObject("Victory cheer").transform;
                cheer.SetParent(root.transform, false);
            }
            var source = cheer.GetComponent<AudioSource>();
            if (source == null) source = cheer.gameObject.AddComponent<AudioSource>();
            director.cheerSource = source;
            director.cheerSource.playOnAwake = false; director.cheerSource.loop = false;
            director.cheerSource.spatialBlend = 0;
            if (director.sequences == null || director.sequences.Length == 0)
                director.sequences = new VictorySequence[] { sequence };
        }
    }
}

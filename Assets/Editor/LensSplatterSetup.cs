using System;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class LensSplatterSetup
    {
        [MenuItem("LeftRight/Rebuild/Update lens splatter")]
        public static void Install()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode.");
            const string path="Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{AddTo(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject root)
        {
            const string path="Assets/Rebuild/Settings/LensSplatter.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                material=new Material(Shader.Find("LeftRight/Lens Splatter"));
                AssetDatabase.CreateAsset(material,path);
            }
            material.SetColor("_Color",new Color(.28f,.008f,.016f,.45f));
            material.SetFloat("_Softness",.6f);EditorUtility.SetDirty(material);
            var effect=root.GetComponent<LensSplatter>()??root.AddComponent<LensSplatter>();
            effect.course=root.GetComponent<SpikeCourse>();effect.gameCamera=root.GetComponentInChildren<Camera>();
            effect.splatterMaterial=material;
        }
    }
}

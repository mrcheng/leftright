using System;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class StartMenuSetup
    {
        [MenuItem("LeftRight/Rebuild/Update start menu and computer players")]
        public static void Install()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(EditorApplication.isPlaying||scene.path!=PrototypeTools.RebuildScene)throw new InvalidOperationException("Open Main outside Play Mode.");
            const string path="Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{AddTo(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        public static void AddTo(GameObject root)
        {
            var menu=root.GetComponent<StartMenu>();if(menu==null)menu=root.AddComponent<StartMenu>();
            menu.course=root.GetComponent<SpikeCourse>();menu.view=root.GetComponent<GameViewSettings>();
            menu.menuFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/Rebuild/UI/Kenney/Kenney Future.ttf");
            menu.buttonTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Rebuild/UI/Kenney/button_rectangle_depth_flat.png");
            menu.course.round.playOnStart=false;
            foreach(var player in menu.course.players)
            {
                var bot=player.GetComponent<RecruitComputer>();if(bot==null)bot=player.gameObject.AddComponent<RecruitComputer>();
                bot.player=player;
            }
        }
    }
}

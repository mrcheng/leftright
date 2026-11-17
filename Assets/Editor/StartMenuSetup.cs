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
            const string pixel="Assets/Rebuild/UI/Pixel/";
            foreach(string name in new[]{"button.png","button_pressed.png"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(pixel+name);
                importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            var fontImporter=(TrueTypeFontImporter)AssetImporter.GetAtPath(pixel+"PressStart2P.ttf");
            fontImporter.fontRenderingMode=FontRenderingMode.HintedRaster; fontImporter.SaveAndReimport();
            menu.menuFont=AssetDatabase.LoadAssetAtPath<Font>(pixel+"PressStart2P.ttf");
            menu.buttonTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(pixel+"button.png");
            menu.pressedButtonTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(pixel+"button_pressed.png");
            menu.course.round.playOnStart=false;
            var online=root.GetComponent<OnlineMatch>();if(online==null)online=root.AddComponent<OnlineMatch>();
            online.menu=menu;menu.online=online;
            if(root.GetComponent<OnlineTestDriver>()==null)root.AddComponent<OnlineTestDriver>();
            foreach(var player in menu.course.players)
            {
                var bot=player.GetComponent<RecruitComputer>();if(bot==null)bot=player.gameObject.AddComponent<RecruitComputer>();
                bot.player=player;
            }
        }
    }
}

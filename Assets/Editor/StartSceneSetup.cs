using System.IO;
using System.Linq;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class StartSceneSetup
    {
        [MenuItem("LeftRight/Rebuild/Install randomized start scenes")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
            const string folder="Assets/Rebuild/Starts";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var zoom=AssetDatabase.LoadAssetAtPath<CameraZoomStart>(folder+"/CameraZoom.asset");
            if(zoom==null){zoom=ScriptableObject.CreateInstance<CameraZoomStart>();AssetDatabase.CreateAsset(zoom,folder+"/CameraZoom.asset");}
            var drink=AssetDatabase.LoadAssetAtPath<InstructorDrinkStart>(folder+"/InstructorDrink.asset");
            if(drink==null){drink=ScriptableObject.CreateInstance<InstructorDrinkStart>();AssetDatabase.CreateAsset(drink,folder+"/InstructorDrink.asset");}
            AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Rebuild/Audio/Resources/"+name+".wav");
            drink.drink=Clip("OpeningDrink");drink.burp=Clip("OpeningBurp");drink.bottleDown=Clip("OpeningBottleDown");EditorUtility.SetDirty(drink);
            const string path="Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var starts=root.GetComponent<StartSceneDirector>()??root.AddComponent<StartSceneDirector>();
                starts.sequences=(starts.sequences??new StartSequence[0]).Concat(new StartSequence[]{zoom,drink}).Distinct().ToArray();
                root.GetComponent<RebuildRoundStart>().startScenes=starts;PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
    }
}

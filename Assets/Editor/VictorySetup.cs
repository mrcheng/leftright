using System;
using System.Collections.Generic;
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
            var shot = InstallShot(folder);
            var pool = new List<VictorySequence>(director.sequences);
            if (!pool.Contains(shot)) pool.Add(shot);
            const string selfPath = "Assets/Rebuild/Victories/InstructorSelfShot.asset";
            var self = AssetDatabase.LoadAssetAtPath<InstructorSelfShotVictory>(selfPath);
            if (self == null) { self = ScriptableObject.CreateInstance<InstructorSelfShotVictory>(); AssetDatabase.CreateAsset(self, selfPath); }
            self.pistolPrefab = shot.pistolPrefab; self.flashMaterial = shot.flashMaterial;
            self.bloodMaterial = shot.bloodMaterial; self.gunshot = shot.gunshot;
            self.shoutClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Rebuild/Audio/Victory/InstructorFuckThis.wav");
            EditorUtility.SetDirty(self);
            if (!pool.Contains(self)) pool.Add(self);
            director.sequences = pool.ToArray();
        }

        static InstructorShotVictory InstallShot(string folder)
        {
            const string path = "Assets/Rebuild/Victories/InstructorPistolShot.asset";
            var shot = AssetDatabase.LoadAssetAtPath<InstructorShotVictory>(path);
            if (shot == null) { shot = ScriptableObject.CreateInstance<InstructorShotVictory>(); AssetDatabase.CreateAsset(shot, path); }
            Material Material(string name, Color color, bool unlit = false)
            {
                string materialPath = folder + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                    material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, materialPath);
                }
                return material;
            }
            var metal = Material("PistolMetal", new Color(.075f,.08f,.085f));
            var grip = Material("PistolGrip", new Color(.22f,.12f,.07f));
            shot.flashMaterial = Material("PistolFlash", new Color(5,2.5f,.3f), true);
            shot.bulletMaterial = Material("PistolBullet", new Color(2,1.2f,.2f), true);
            const string prefabPath = "Assets/Rebuild/Victories/OfficerPistol.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var root = new GameObject("Low-poly officer pistol");
                void Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, float pitch = 0)
                {
                    var part = GameObject.CreatePrimitive(type); part.name = name; part.transform.SetParent(root.transform, false);
                    UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                    part.transform.localPosition = position; part.transform.localScale = scale;
                    part.transform.localRotation = Quaternion.Euler(pitch,0,0); part.GetComponent<Renderer>().sharedMaterial = material;
                }
                try
                {
                    Part("Receiver",PrimitiveType.Cube,new Vector3(0,.03f,.12f),new Vector3(.09f,.085f,.22f),metal);
                    Part("Barrel",PrimitiveType.Cylinder,new Vector3(0,.04f,.31f),new Vector3(.05f,.13f,.05f),metal,90);
                    Part("Grip",PrimitiveType.Cube,new Vector3(0,-.09f,.015f),new Vector3(.075f,.19f,.095f),grip,-12);
                    Part("Trigger guard",PrimitiveType.Cube,new Vector3(0,-.055f,.1f),new Vector3(.025f,.06f,.09f),metal);
                    Part("Front sight",PrimitiveType.Cube,new Vector3(0,.07f,.4f),new Vector3(.018f,.025f,.025f),metal);
                    PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            shot.pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            shot.bloodMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rebuild/Art/Outpost/Materials/Blood.mat");
            shot.gunshot = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Rebuild/Audio/Victory/PistolShot.wav");
            EditorUtility.SetDirty(shot);
            return shot;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeftRight.Editor
{
    public static class RecruitRig
    {
        const string Art = "Assets/Rebuild/Art/Recruits/";

        [MenuItem("LeftRight/Rebuild/Update four recruits")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode before updating recruits.");
            const string path = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AddTo(prefab);
                SpikeCourseSetup.AddTo(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject environment)
        {
            Directory.CreateDirectory(Art + "Materials");
            AssetDatabase.Refresh();
            var colors = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float[]>>(
                File.ReadAllText("SourceArt/Characters/recruit-palette.json"));
            foreach (var entry in colors)
            {
                string path = Art + "Materials/" + entry.Key + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", new Color(entry.Value[0], entry.Value[1], entry.Value[2]));
                material.SetFloat("_Smoothness", .12f);
                EditorUtility.SetDirty(material);
            }
            var old = environment.transform.Find("Recruits");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Recruits").transform;
            group.SetParent(environment.transform, false);
            var lanes = environment.transform.Find("Player lanes - gameplay anchors");
            var clock = environment.GetComponent<MasterBeat>();
            var officer = environment.GetComponentInChildren<OfficerDirections>();
            var roles = new[] { RecruitRole.Rifle, RecruitRole.Sniper, RecruitRole.Mortar, RecruitRole.Medic };
            for (int i = 0; i < roles.Length; i++)
            {
                string name = "Recruit" + roles[i];
                string modelPath = Art + "Models/" + name + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                if (importer.animationType != ModelImporterAnimationType.Generic || importer.importAnimation)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.importAnimation = false;
                    importer.SaveAndReimport();
                }
                var recruit = new GameObject(roles[i] + (i == 0 || i == 3 ? " - female" : " - male"));
                recruit.transform.SetParent(group, false);
                // Locate by name so unrelated lane props cannot change character assignment.
                var lane = lanes.Find("Player lane " + (i + 1));
                recruit.transform.localPosition = new Vector3(0, .65f, lane.localPosition.z);
                var conversion = new GameObject("Model orientation").transform;
                conversion.SetParent(recruit.transform, false);
                conversion.localRotation = Quaternion.Euler(0, 180, 0);
                conversion.localScale = Vector3.one * (i == 2 ? .66f : .62f);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, conversion);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                        AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + m.name + ".mat")
                        ?? throw new InvalidOperationException("Missing recruit material: " + m.name)).ToArray();
                var transforms = model.GetComponentsInChildren<Transform>();
                Transform Bone(string bone) => transforms.Single(t => t.name == bone);
                var march = recruit.AddComponent<RecruitMarch>();
                march.role = roles[i];
                march.walkingBodyBob = .13f;
                march.inPlaceBodyBob = .045f;
                march.female = i == 0 || i == 3;
                march.masterBeat = clock;
                march.directions = officer;
                march.leftHip = Bone("UpperLeg.L"); march.rightHip = Bone("UpperLeg.R");
                march.leftKnee = Bone("LowerLeg.L"); march.rightKnee = Bone("LowerLeg.R");
                march.leftArm = Bone("UpperArm.L"); march.rightArm = Bone("UpperArm.R");
                var bounds = model.GetComponentsInChildren<Renderer>()[0].bounds;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                recruit.transform.position += Vector3.up * (.65f - bounds.min.y);
            }
        }
    }
}

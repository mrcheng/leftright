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
    public static class OfficerRig
    {
        const string Art = "Assets/Rebuild/Art/Outpost/";

        [MenuItem("LeftRight/Rebuild/Update marching officer")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode before updating the officer.");
            const string path = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var audio = prefab.GetComponentInChildren<OutpostAudio>();
                var officer = prefab.GetComponentInChildren<OfficerDirections>();
                Configure(officer.gameObject, audio);
                ConfigureTiming(audio.palette);
                var oldStand = officer.transform.parent.Find("CommandStand");
                Vector3 position = oldStand.localPosition;
                Quaternion rotation = oldStand.localRotation;
                Vector3 scale = oldStand.localScale;
                var parent = oldStand.parent;
                UnityEngine.Object.DestroyImmediate(oldStand.gameObject);
                var stand = Model("CommandStand", parent);
                stand.localPosition = position;
                stand.localRotation = rotation;
                stand.localScale = scale;
                OutpostSceneBuilder.RefreshOutpostMaterialBindings(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static GameObject Create(Transform parent)
        {
            var officer = new GameObject("Officer - animated direction cues");
            officer.transform.SetParent(parent, false);
            officer.transform.localPosition = new Vector3(0, .4f, 7.2f);
            officer.transform.localScale = Vector3.one * 1.15f;
            Configure(officer, null);
            return officer;
        }

        static void Configure(GameObject officer, OutpostAudio audio)
        {
            foreach (Transform child in officer.transform.Cast<Transform>().ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            officer.transform.localScale = Vector3.one;
            const string recruitArt = "Assets/Rebuild/Art/Recruits/";
            var palette = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float[]>>(
                File.ReadAllText("SourceArt/Characters/recruit-palette.json"));
            foreach (var entry in palette)
            {
                string path = recruitArt + "Materials/" + entry.Key + ".mat";
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
            var conversion = new GameObject("Instructor model - 125 percent recruit scale").transform;
            conversion.SetParent(officer.transform, false);
            conversion.localRotation = Quaternion.Euler(0, 180, 0);
            conversion.localScale = Vector3.one * .775f;
            string modelPath = recruitArt + "Models/RecruitInstructor.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer.importAnimation || importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), conversion);
            // The procedural pivots reparent bones; save a concrete rig rather than nested-FBX overrides.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    AssetDatabase.LoadAssetAtPath<Material>(recruitArt + "Materials/" + m.name + ".mat")).ToArray();
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            // The FBX root has its own import rotation; lift via the logical conversion instead.
            conversion.position += Vector3.up * (officer.transform.position.y - skin.bounds.min.y);
            var bones = model.GetComponentsInChildren<Transform>();
            Transform Bone(string name) => bones.Single(t => t.name == name);
            Transform Wrap(string name, Transform bone, Transform parent)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(parent, false);
                pivot.position = bone.position;
                pivot.rotation = officer.transform.rotation;
                bone.SetParent(pivot, true);
                return pivot;
            }
            var directions = officer.GetComponent<OfficerDirections>() ?? officer.AddComponent<OfficerDirections>();
            var march = officer.GetComponent<OfficerMarch>() ?? officer.AddComponent<OfficerMarch>();
            directions.playOnStart = false;
            directions.audioOutput = audio;
            march.directions = directions;
            march.bodyBounce = .065f;
            march.audioOutput = audio;
            march.masterBeat = audio != null ? audio.GetComponentInParent<MasterBeat>() : null;
            march.body = Wrap("Torso bounce", Bone("Abdomen"), officer.transform);
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                string suffix = left ? ".L" : ".R";
                string name = left ? "Left" : "Right";
                var shoulder = new GameObject(name + " shoulder").transform;
                shoulder.SetParent(officer.transform, false);
                shoulder.position = Bone("UpperArm" + suffix).position;
                var swing = Wrap(name + " arm march swing", Bone("UpperArm" + suffix), shoulder);
                var hip = Wrap(name + " hip", Bone("UpperLeg" + suffix), officer.transform);
                var knee = Wrap(name + " knee", Bone("LowerLeg" + suffix), hip);
                if (left)
                {
                    directions.leftShoulder = shoulder;
                    march.leftArmSwing = swing;
                    march.leftHip = hip;
                    march.leftKnee = knee;
                }
                else
                {
                    directions.rightShoulder = shoulder;
                    march.rightArmSwing = swing;
                    march.rightHip = hip;
                    march.rightKnee = knee;
                }
            }
        }

        public static void ConfigureTiming(PrototypeAudioPalette palette)
        {
            var beats = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("SourceArt/Audio/PrototypeMusicBeats.json"));
            palette.titleBeatsPerMinute = (float)beats["title"]["beatsPerMinute"];
            palette.titleFirstBeatSeconds = (float)beats["title"]["firstBeatSeconds"];
            palette.roundBeatsPerMinute = (float)beats["round"]["beatsPerMinute"];
            palette.roundFirstBeatSeconds = (float)beats["round"]["firstBeatSeconds"];
            EditorUtility.SetDirty(palette);
        }

        static Transform Model(string name, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Models/" + name + ".fbx");
            if (asset == null) throw new InvalidOperationException("Missing officer model: " + name);
            var wrapper = new GameObject(name).transform;
            wrapper.SetParent(parent, false);
            wrapper.localRotation = Quaternion.Euler(0, 180, 0);
            PrefabUtility.InstantiatePrefab(asset, wrapper);
            foreach (var renderer in wrapper.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + m.name + ".mat")
                    ?? throw new InvalidOperationException("Missing officer material: " + m.name)).ToArray();
            return wrapper;
        }
    }
}

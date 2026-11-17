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
            PrepareMaterials();
            foreach (Transform child in officer.transform.Cast<Transform>().ToArray())
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            var body = Model("OfficerPlaceholder", officer.transform);
            var directions = officer.GetComponent<OfficerDirections>() ?? officer.AddComponent<OfficerDirections>();
            directions.playOnStart = false;
            var march = officer.GetComponent<OfficerMarch>() ?? officer.AddComponent<OfficerMarch>();
            directions.audioOutput = audio;
            march.directions = directions;
            march.audioOutput = audio;
            march.masterBeat = audio != null ? audio.GetComponentInParent<MasterBeat>() : null;
            march.body = body;
            Transform Joint(string name, Transform parent, Vector3 position)
            {
                var joint = new GameObject(name).transform;
                joint.SetParent(parent, false);
                joint.localPosition = position;
                return joint;
            }
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                string name = left ? "Left" : "Right";
                var shoulder = Joint(name + " shoulder", officer.transform, new Vector3(left ? -.35f : .35f, 1.53f, 0));
                var swing = Joint(name + " arm march swing", shoulder, Vector3.zero);
                Model("OfficerArm", swing);
                var hip = Joint(name + " hip", officer.transform, new Vector3(left ? -.17f : .17f, 1.06f, 0));
                Model("OfficerThigh", hip);
                var knee = Joint(name + " knee", hip, new Vector3(0, -.5f, 0));
                Model("OfficerShin", knee);
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

        static void PrepareMaterials()
        {
            var palette = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float[]>>(
                File.ReadAllText("SourceArt/Environment/outpost-palette.json"));
            foreach (string name in new[] { "Uniform", "Leather", "Shirt", "Skin", "Brass", "Olive" })
            {
                string path = Art + "Materials/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, path);
                }
                var color = palette[name];
                material.SetColor("_BaseColor", new Color(color[0], color[1], color[2]));
                material.SetFloat("_Smoothness", .12f);
                EditorUtility.SetDirty(material);
            }
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace LeftRight.Editor
{
    public static class OutpostSceneBuilder
    {
        const string Art = "Assets/Rebuild/Art/";
        const string Settings = "Assets/Rebuild/Settings/";
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static Transform root;

        [MenuItem("LeftRight/Rebuild/Build outpost environment")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Exit Play Mode and save edits before rebuilding the environment.");
            var scene = EditorSceneManager.OpenScene(PrototypeTools.RebuildScene);
            var previous = GameObject.Find("Outpost Environment");
            if (previous != null) Object.DestroyImmediate(previous);
            else
            {
                var defaults = scene.GetRootGameObjects();
                if (defaults.All(g => new[] { "Main Camera", "Directional Light", "Global Volume" }.Contains(g.name)))
                    foreach (var item in defaults) Object.DestroyImmediate(item);
            }
            Directory.CreateDirectory(Settings);
            Directory.CreateDirectory(Art + "Outpost/Materials");
            AssetDatabase.Refresh();
            Materials.Clear();
            MakeMaterials();
            root = new GameObject("Outpost Environment").transform;
            Random.InitState(1943);

            var ground = Group("Terrain and trees");
            Prop("Terrain", Vector3.zero, 1, 0, ground);
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 19; i++)
                {
                    float x = -35 + i * 3.9f + Random.Range(-1.0f, 1.0f);
                    float z = 21 + row * 4.4f + Random.Range(-1f, 1f);
                    Kit(i % 3 == 0 ? "tree-tall" : "tree", new Vector3(x, -.3f, z), Random.Range(3.3f, 5.8f), Random.Range(0, 360), ground);
                }
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 9; i++)
                    Kit("tree", new Vector3(side * Random.Range(25f, 34f), -.3f, -13 + i * 4.0f), Random.Range(3.8f, 5.7f), Random.Range(0, 360), ground);
            for (int i = 0; i < 30; i++)
            {
                float x = Random.Range(-32f, 32f);
                float z = Random.Range(9f, 29f);
                if (Mathf.Abs(x) < 19 && z < 20) continue;
                Kit(i % 2 == 0 ? "rock-a" : "rock-b", new Vector3(x, -.35f, z), Random.Range(1.4f, 3.5f), Random.Range(0, 360), ground);
            }

            var lanes = Group("Player lanes - gameplay anchors");
            for (int i = 0; i < 4; i++)
            {
                float z = -3.3f + i * 2.2f;
                var lane = Prop("Walkway", new Vector3(0, .25f, z), 1, 0, lanes);
                lane.name = "Player lane " + (i + 1);
                var collider = lane.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .25f, 0);
                collider.size = new Vector3(20, .36f, 1.16f);
                foreach (float x in new[] { -10.25f, 10.25f })
                {
                    Primitive("Walkway footing", new Vector3(x, -.3f, z), new Vector3(.6f, 1.7f, 1.4f), Materials["Wood"], lanes);
                    Primitive("Bank retaining board", new Vector3(x * 1.08f, -.8f, z), new Vector3(.2f, 1.4f, 2f), Materials["Wood"], lanes);
                }
            }

            var command = Group("Command post");
            Prop("CommandStand", new Vector3(0, -.30f, 7.1f), 1, 0, command);
            OfficerRig.Create(command);
            AddFlag(new Vector3(-3.2f, -.4f, 7.3f), 180, 1.7f, command);
            AddFlag(new Vector3(3.2f, -.4f, 7.3f), 180, 1.7f, command);
            for (int side = -1; side <= 1; side += 2)
            {
                Prop("SandbagWall", new Vector3(side * 5.7f, -.4f, 7.7f), 1, 0, command);
                Prop("CampLamp", new Vector3(side * 4.3f, -.4f, 5.8f), 1, side > 0 ? 180 : 0, command);
                PointLight(new Vector3(side * 4.1f, 2.9f, 5.8f), new Color(1f, .74f, .45f), 10f, 8f, command);
            }

            var buildings = Group("Barracks and supply camp");
            Prop("Barracks", new Vector3(-10.8f, -.4f, 14.6f), 1.15f, -8, buildings);
            Prop("Barracks", new Vector3(10.8f, -.4f, 15.8f), 1.15f, 9, buildings);
            Prop("Watchtower", new Vector3(-20.0f, -.35f, 14.1f), 1.05f, 12, buildings);
            Prop("Watchtower", new Vector3(20.5f, -.35f, 15.8f), 1.05f, -12, buildings);
            for (int side = -1; side <= 1; side += 2)
            {
                Kit("tent", new Vector3(side * 17f, -.32f, 6.7f), 7f, side * 15, buildings);
                Kit("tent-canvas", new Vector3(side * 17f, -.32f, 6.7f), 7f, side * 15, buildings);
                Kit("tent", new Vector3(side * 18.7f, -.32f, .7f), 5.5f, side * -12, buildings);
                Kit("tent-canvas", new Vector3(side * 18.7f, -.32f, .7f), 5.5f, side * -12, buildings);
                Kit("box-large", new Vector3(side * 13.2f, -.35f, 4.6f), 3.3f, side * 8, buildings);
                Kit("box", new Vector3(side * 13.1f, .45f, 4.6f), 2.8f, side * -8, buildings);
                Kit("barrel", new Vector3(side * 14.7f, -.35f, 4.8f), 3.4f, 0, buildings);
                Kit("box", new Vector3(side * 15.3f, -.35f, 3.4f), 2.6f, 27, buildings);
                Kit("resource-planks", new Vector3(side * 14.5f, -.33f, -4.6f), 3f, 90, buildings);
                Kit("tree-log", new Vector3(side * 19f, -.34f, -5.5f), 2.4f, side * 20, buildings);
                Prop("CampLamp", new Vector3(side * 14f, -.4f, 9.3f), 1, side > 0 ? 180 : 0, buildings);
                PointLight(new Vector3(side * 13.6f, 2.9f, 9.3f), new Color(1f, .74f, .45f), 14f, 9f, buildings);
            }
            Kit("workbench", new Vector3(-12f, -.3f, 10f), 3.2f, 0, buildings);
            Kit("tool-shovel", new Vector3(-11.2f, -.1f, 10.2f), 2.8f, 30, buildings);
            Kit("bedroll", new Vector3(18.5f, -.30f, 2.1f), 3.5f, 90, buildings);

            var perimeter = Group("Wire and defensive cover");
            for (int i = 0; i < 11; i++)
                Prop("WireSection", new Vector3(-20 + i * 4, -.4f, 20), 1, 0, perimeter);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                    Prop("WireSection", new Vector3(side * 23f, -.4f, -5 + i * 4.4f), 1, 90, perimeter);
                for (int i = 0; i < 3; i++)
                    Prop("SteelObstacle", new Vector3(side * (12.8f + i * 3.0f), -.45f, -7.8f + Random.Range(-.5f, .5f)), 1, Random.Range(0, 60), perimeter);
                for (int i = 0; i < 3; i++)
                    Prop("SandbagWall", new Vector3(side * (12.5f + i * 3.1f), -.35f, -5.6f), 1, 0, perimeter);
            }
            AddLighting();
            AddAudio();
            TowerFloodlights.AddTo(root.gameObject);
            StreetlightLighting.AddTo(root.gameObject);
            RoundStartSetup.AddTo(root.gameObject);
            RecruitRig.AddTo(root.gameObject);
            SpikeCourseSetup.AddTo(root.gameObject);
            FlagLetters.AddTo(root.gameObject);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject,
                "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("Outpost environment built. Static scene/art foundation; gameplay is not connected yet.");
        }

        static Transform Group(string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            return group;
        }

        static void AddFlag(Vector3 position, float yaw, float phase, Transform parent)
        {
            var flag = new GameObject("Wind flag").transform;
            flag.SetParent(parent, false);
            flag.localPosition = position;
            flag.localRotation = Quaternion.Euler(0, yaw, 0);
            Prop("Flagpole", Vector3.zero, 1, 0, flag);
            string meshPath = Art + "Outpost/Models/FlagCloth.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = WindFlag.CreateClothMesh();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            var fabric = new GameObject("Flag fabric");
            fabric.transform.SetParent(flag, false);
            fabric.transform.localPosition = new Vector3(.045f, 5.06f, -.015f);
            fabric.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = fabric.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials["FlagFabric"];
            renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            fabric.AddComponent<WindFlag>().phaseOffset = phase;
        }

        static void MakeMaterials()
        {
            var palette = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, float[]>>(
                File.ReadAllText("SourceArt/Environment/outpost-palette.json"));
            foreach (var entry in palette)
            {
                var c = entry.Value;
                var material = SaveMaterial(entry.Key, new Color(c[0], c[1], c[2]));
                if (entry.Key == "Lamp")
                {
                    material.SetColor("_EmissionColor", new Color(1f, .62f, .20f) * 2.7f);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    material.EnableKeyword("_EMISSION");
                }
                Materials.Add(entry.Key, material);
            }
            var kenney = SaveMaterial("KenneyPalette", new Color(.68f, .68f, .50f));
            kenney.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "KenneySurvivalKit/Models/Textures/colormap.png"));
            Materials.Add("colormap", kenney);
            var trees = SaveMaterial("ForestPalette", new Color(.60f, .45f, .38f));
            trees.SetTexture("_BaseMap", kenney.GetTexture("_BaseMap"));
            Materials.Add("ForestPalette", trees);
            var fabric = Materials["Red"];
            fabric.SetFloat("_Cull", (float)CullMode.Off);
            fabric.doubleSidedGI = true;
            Materials.Add("FlagFabric", fabric);
        }

        static Material SaveMaterial(string name, Color color)
        {
            string path = Art + "Outpost/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", .12f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static void RefreshOutpostMaterialBindings(GameObject environment)
        {
            // Re-exported FBX meshes can reorder material slots. Bind by source
            // names rather than retaining overrides against the previous order.
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>())
            {
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                if (source == null || !AssetDatabase.GetAssetPath(source).StartsWith(Art + "Outpost/Models/")) continue;
                renderer.sharedMaterials = source.sharedMaterials.Select(m =>
                    AssetDatabase.LoadAssetAtPath<Material>(Art + "Outpost/Materials/" + m.name + ".mat")
                    ?? throw new InvalidOperationException("Missing model material: " + m.name)).ToArray();
            }
            var cues = environment.GetComponent<DirectionFloodlights>();
            if (cues != null)
            {
                cues.leftLampMaterialIndex = Array.FindIndex(cues.leftLamp.sharedMaterials, m => m.name == "FloodLamp");
                cues.rightLampMaterialIndex = Array.FindIndex(cues.rightLamp.sharedMaterials, m => m.name == "FloodLamp");
            }
        }

        static GameObject Prop(string model, Vector3 position, float scale, float yaw, Transform parent) =>
            Place(Art + "Outpost/Models/" + model + ".fbx", position, scale, yaw, parent);

        static GameObject Kit(string model, Vector3 position, float scale, float yaw, Transform parent) =>
            Place(Art + "KenneySurvivalKit/Models/" + model + ".fbx", position, scale, yaw, parent);

        static GameObject Place(string path, Vector3 position, float scale, float yaw, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing outpost model: " + path);
            // Keep the FBX's imported axis/unit conversion on a child. Gameplay
            // anchors and their colliders use ordinary Unity units on the parent.
            var instance = new GameObject(Path.GetFileNameWithoutExtension(path));
            instance.transform.SetParent(parent, false);
            PrefabUtility.InstantiatePrefab(asset, instance.transform);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0, yaw + (path.Contains("Outpost/Models/") ? 180 : 0), 0);
            instance.transform.localScale = Vector3.one * scale;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                {
                    string modelName = Path.GetFileNameWithoutExtension(path);
                    if (modelName == "tree" || modelName == "tree-tall") return Materials["ForestPalette"];
                    if (modelName == "tent-canvas") return Materials["Canvas"];
                    if (m == null || !Materials.TryGetValue(m.name, out var replacement))
                        throw new InvalidOperationException("Unmapped model material in " + path + ": " + (m == null ? "null" : m.name));
                    return replacement;
                }).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            return instance;
        }

        static void Primitive(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
        }

        static void PointLight(Vector3 position, Color color, float intensity, float range, Transform parent)
        {
            var obj = new GameObject("Warm camp lamp");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        static void AddLighting()
        {
            var lighting = Group("Camera and lighting");
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(lighting, false);
            cameraObject.transform.localPosition = new Vector3(0, 7.567722f, -11.445186f);
            cameraObject.transform.localRotation = Quaternion.Euler(24.408024f, 0, 0);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60;
            camera.nearClipPlane = .2f;
            camera.farClipPlane = 180;
            camera.allowHDR = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cameraObject.AddComponent<AudioListener>();
            var sunObject = new GameObject("Evening light beneath storm clouds");
            sunObject.transform.SetParent(lighting, false);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = .03f;

            var volumeObject = new GameObject("Outpost color grade");
            volumeObject.transform.SetParent(lighting, false);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Settings + "OutpostVolume.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, Settings + "OutpostVolume.asset");
            }
            foreach (var old in profile.components.ToArray()) Object.DestroyImmediate(old, true);
            profile.components.Clear();
            profile.Add<ColorAdjustments>(true);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.1f;
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = .17f;
            vignette.smoothness.value = .55f;
            foreach (var component in profile.components)
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
            OutpostWeather.Configure(camera, sun, profile);
        }

        static void AddAudio()
        {
            var parent = Group("Prototype audio - shared original clips");
            var palette = AssetDatabase.LoadAssetAtPath<PrototypeAudioPalette>(Settings + "PrototypeAudio.asset");
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<PrototypeAudioPalette>();
                AssetDatabase.CreateAsset(palette, Settings + "PrototypeAudio.asset");
            }
            const string source = "Assets/Prototype/Sounds/";
            AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(source + path)
                ?? throw new InvalidOperationException("Missing original audio clip: " + path);
            palette.titleMusic = Clip("Music/Warmongers Title.mp3");
            palette.roundMusic = Clip("Music/defcon_one_stereo.mp3");
            OfficerRig.ConfigureTiming(palette);
            palette.leftCall = Clip("Vo/snd_vo_links.wav");
            palette.rightCall = Clip("Vo/snd_vo_rechts.wav");
            palette.landing = Clip("Footsteps/Footsteps1/snd_foot1_land1.wav");
            palette.healthPickup = Clip("Powerups/HealthPotion/snd_health_powerup1.wav");
            palette.walking = Enumerable.Range(1, 10).Select(i => Clip($"Footsteps/Footsteps1/snd_foot1_walk{i}.wav")).ToArray();
            palette.running = Enumerable.Range(1, 10).Select(i => Clip($"Footsteps/Footsteps1/snd_foot1_run{i}.wav")).ToArray();
            EditorUtility.SetDirty(palette);
            AudioSource Source(string name, float volume)
            {
                var child = new GameObject(name);
                child.transform.SetParent(parent, false);
                var audio = child.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.volume = volume;
                return audio;
            }
            var controller = parent.gameObject.AddComponent<OutpostAudio>();
            controller.palette = palette;
            controller.music = Source("Original music", .25f);
            controller.voice = Source("Original direction calls", .8f);
            controller.effects = Source("Original footsteps and pickups", .6f);
            root.GetComponentInChildren<OfficerDirections>().audioOutput = controller;
            root.GetComponentInChildren<OfficerMarch>().audioOutput = controller;
        }
    }
}

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LeftRight.Editor
{
    public static class OutpostWeather
    {
        const string SkyPath = "Assets/Rebuild/Settings/StormEveningSky.mat";

        [MenuItem("LeftRight/Rebuild/Apply stormy evening")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode before applying weather.");
            var volume = UnityEngine.Object.FindObjectsByType<Volume>()
                .Single(v => v.name == "Outpost color grade");
            Configure(Camera.main, RenderSettings.sun, volume.sharedProfile);
            PrefabUtility.RecordPrefabInstancePropertyModifications(Camera.main);
            PrefabUtility.RecordPrefabInstancePropertyModifications(RenderSettings.sun);
            PrefabUtility.RecordPrefabInstancePropertyModifications(RenderSettings.sun.transform);

            const string prefabPath = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SetCameraAndLight(prefab.GetComponentInChildren<Camera>(),
                    prefab.GetComponentsInChildren<Light>().Single(l => l.type == LightType.Directional));
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void Configure(Camera camera, Light sun, VolumeProfile profile)
        {
            SetCameraAndLight(camera, sun);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                var shader = Shader.Find("LeftRight/Storm Evening Sky");
                if (shader == null) throw new InvalidOperationException("Storm sky shader is not imported yet.");
                sky = new Material(shader);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .49f, .59f);
            RenderSettings.ambientEquatorColor = new Color(.36f, .40f, .47f);
            RenderSettings.ambientGroundColor = new Color(.20f, .21f, .24f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.32f, .38f, .43f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 35;
            RenderSettings.fogEndDistance = 95;
            if (profile.TryGet<ColorAdjustments>(out var grade))
            {
                grade.contrast.Override(6);
                grade.saturation.Override(-16);
                grade.postExposure.Override(.25f);
            }
            if (profile.TryGet<Bloom>(out var bloom)) bloom.intensity.Override(.3f);
            EditorUtility.SetDirty(profile);
        }

        static void SetCameraAndLight(Camera camera, Light sun)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.32f, .38f, .43f);
            sun.name = "Evening light beneath storm clouds";
            sun.transform.rotation = Quaternion.Euler(18, -38, 0);
            sun.color = new Color(.98f, .80f, .64f);
            sun.intensity = 1f;
            sun.shadowStrength = .6f;
        }
    }
}

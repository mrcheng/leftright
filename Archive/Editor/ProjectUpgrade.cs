using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LeftRight.Editor
{
    public static class ProjectUpgrade
    {
        public const string ScenePath = "Assets/Scenes/Scene1.unity";

        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            const string rendererPath = "Assets/Settings/LeftRightRenderer.asset";
            const string pipelinePath = "Assets/Settings/LeftRightPipeline.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            var serialized = new SerializedObject(pipeline);
            serialized.FindProperty("m_SupportsDynamicBatching").boolValue = false;
            serialized.FindProperty("m_UseSRPBatcher").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            int originalQuality = QualitySettings.GetQualityLevel();
            for (int quality = 0; quality < QualitySettings.names.Length; quality++)
            {
                QualitySettings.SetQualityLevel(quality, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(originalQuality, false);
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 50;
            EditorUtility.SetDirty(pipeline);

            // Read colors and textures before assigning the new shader.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null || unlit == null) throw new InvalidOperationException("URP shaders unavailable.");
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.shader != null && material.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                Texture texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Vector2 scale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
                Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
                string oldName = material.shader == null ? "" : material.shader.name;
                material.shader = oldName.Contains("Unlit") ? unlit : lit;
                material.SetColor("_BaseColor", color);
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
                if (color.a < 0.999f || oldName.Contains("Transparent"))
                {
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)RenderQueue.Transparent;
                }
                EditorUtility.SetDirty(material);
            }
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            var oldInputObject = GameObject.Find("Rewired Input Manager");
            if (oldInputObject != null) UnityEngine.Object.DestroyImmediate(oldInputObject);
            foreach (var events in UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            {
                var legacyInput = events.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (legacyInput != null) UnityEngine.Object.DestroyImmediate(legacyInput);
                var input = events.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                if (input == null) input = events.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                input.AssignDefaultActions();
            }
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                camera.GetUniversalAdditionalCameraData();
            Validate();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.ForceReserializeAssets(AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") &&
                new[] { ".prefab", ".mat", ".asset", ".controller", ".anim" }.Contains(Path.GetExtension(path))),
                ForceReserializeAssetsOptions.ReserializeAssets);
            AssetDatabase.SaveAssets();
            Debug.Log("LeftRight intermediate migration complete.");
        }

        [MenuItem("LeftRight/Validate game")]
        public static void Validate()
        {
            int missing = EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
            if (missing != 0) throw new InvalidOperationException($"Scene has {missing} missing scripts.");
            Debug.Log("LeftRight validation passed: no missing scripts.");
        }

        public static void BuildWindows()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/LeftRight.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Build failed: {report.summary.totalErrors} errors.");
            Debug.Log($"LeftRight Windows build passed: {report.summary.outputPath}");
        }
    }
}

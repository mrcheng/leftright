using System;
using System.IO;
using LeftRight.Rebuild;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LeftRight.Editor
{
    public static class FlagLetters
    {
        [MenuItem("LeftRight/Rebuild/Update flag letters")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != PrototypeTools.RebuildScene)
                throw new InvalidOperationException("Open Main outside Play Mode.");
            const string path = "Assets/Rebuild/Prefabs/OutpostEnvironment.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { AddTo(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, path); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        public static void AddTo(GameObject environment)
        {
            const string directory = "Assets/Rebuild/Art/Outpost/Textures";
            Directory.CreateDirectory(directory);
            foreach (var flag in environment.GetComponentsInChildren<WindFlag>())
            {
                bool left = flag.transform.position.x < 0;
                string letter = left ? "L" : "R";
                string[] glyph = left ? new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" }
                    : new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" };
                const int size = 256;
                var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
                var pixels = new Color[size * size];
                Color red = new Color(.46f, .085f, .075f);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // Fabric UVs run down from the pole top. Mirror X to read from the game camera.
                        int col = (255 - x - 58) / 28, row = (y - 30) / 28;
                        bool ink = 255-x >= 58 && 255-x < 198 && y >= 30 && y < 226 && glyph[row][col] == '1';
                        pixels[y * size + x] = ink ? new Color(.015f,.015f,.015f) : red;
                    }
                texture.SetPixels(pixels); texture.Apply();
                string path = directory + "/Flag" + letter + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                string materialPath = "Assets/Rebuild/Art/Outpost/Materials/Flag" + letter + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                material.SetFloat("_Smoothness", .12f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                material.doubleSidedGI = true;
                flag.GetComponent<Renderer>().sharedMaterial = material;
                EditorUtility.SetDirty(material);
            }
        }
    }
}

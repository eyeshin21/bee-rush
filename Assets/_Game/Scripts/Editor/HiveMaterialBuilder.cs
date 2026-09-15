using UnityEditor;
using UnityEngine;

namespace HoneyBeeRush.EditorTools
{
    public static class HiveMaterialBuilder
    {
        private const string ModelPath = "Assets/_Game/Models/Hive.fbx";
        private const string MaterialFolder = "Assets/_Game/Models/Materials/Hive";
        private const string ShaderName = "HoneyBeeRush/ToonLit";

        private struct Entry
        {
            public string Name;
            public Color Color;

            public Entry(string name, Color color)
            {
                Name = name;
                Color = color;
            }
        }

        private static readonly Entry[] Entries =
        {
            new Entry("beehive", new Color(0.973f, 0.693f, 0.031f, 1f)),
            new Entry("inner_bee", new Color(0.404f, 0.231f, 0.063f, 1f)),
            new Entry("Leaf", new Color(0.572f, 0.844f, 0.010f, 1f)),
            new Entry("Wood", new Color(0.915f, 0.561f, 0.203f, 1f))
        };

        [MenuItem("Honey Bee Rush/Models/Apply Hive Materials")]
        public static void Build()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[HiveMaterialBuilder] " + ModelPath + " is not an imported model. Aborting.");
                return;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError("[HiveMaterialBuilder] Shader '" + ShaderName + "' not found. Aborting.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                System.IO.Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            for (int i = 0; i < Entries.Length; i++)
            {
                Entry entry = Entries[i];
                string path = MaterialFolder + "/Hive_" + entry.Name + ".mat";

                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                if (material.HasProperty("_Color")) material.SetColor("_Color", entry.Color);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", entry.Color);
                EditorUtility.SetDirty(material);

                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.Name), material);
                Debug.Log("[HiveMaterialBuilder] remapped '" + entry.Name + "' -> " + path);
            }

            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            Debug.Log("[HiveMaterialBuilder] " + Entries.Length + " hive materials created and remapped on " + ModelPath);
        }
    }
}

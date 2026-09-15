using System.Collections.Generic;
using System.Text;
using HoneyBeeRush.Data;
using UnityEditor;
using UnityEngine;

namespace HoneyBeeRush.EditorTools
{
    public static class ColorMaterialSetGenerator
    {
        public const string MappingAssetPath = "Assets/_Game/Resources/Data/ColorMaterialMapping.asset";
        public const string GeneratedRoot = "Assets/_Game/Models/Materials/Generated";

        private const string CrateTemplatePath = "Assets/_Game/Models/Materials/Crate/Box.mat";
        private const string CellTemplatePath = "Assets/_Game/Models/Materials/Cell/Cell.mat";
        private const string BeeTemplatePath = "Assets/_Game/Models/Materials/BeeBody/Bee_Body.mat";
        private const string LockTemplatePath = "Assets/_Game/Models/Materials/DelayLock.mat";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly Color LockedTint = new Color(0.60f, 0.58f, 0.55f, 1f);
        private const float LockedBlend = 0.72f;
        private static readonly Color InactiveTint = new Color(0.62f, 0.64f, 0.70f, 1f);
        private const float InactiveBlend = 0.42f;
        private const float InactiveShade = 0.88f;
        private static readonly Color HiddenCrateColor = new Color(0.78f, 0.72f, 0.60f, 1f);
        private static readonly Color FallbackColor = new Color(1f, 0f, 1f, 1f);

        public static readonly ColorType[] PlayableColors =
        {
            ColorType.Red, ColorType.Blue, ColorType.Green, ColorType.Yellow,
            ColorType.Purple, ColorType.Orange, ColorType.Pink, ColorType.Cyan,
            ColorType.Black, ColorType.Brown, ColorType.White, ColorType.Grey,
            ColorType.Teal, ColorType.Lime, ColorType.Magenta, ColorType.Indigo
        };

        private static readonly Dictionary<ColorType, Color> Palette = new Dictionary<ColorType, Color>
        {
            { ColorType.Red, Hex("E52626") },
            { ColorType.Blue, Hex("2173F2") },
            { ColorType.Green, Hex("33B84D") },
            { ColorType.Yellow, Hex("FFD41F") },
            { ColorType.Purple, Hex("9E40DB") },
            { ColorType.Orange, Hex("FF8514") },
            { ColorType.Pink, Hex("FA5CAD") },
            { ColorType.Cyan, Hex("1AD9ED") },
            { ColorType.Black, Hex("0A0A0A") },
            { ColorType.Brown, Hex("804D24") },
            { ColorType.White, Hex("FFFFFF") },
            { ColorType.Grey, Hex("7A7A80") },
            { ColorType.Teal, Hex("008587") },
            { ColorType.Lime, Hex("B8E51F") },
            { ColorType.Magenta, Hex("D10F85") },
            { ColorType.Indigo, Hex("4738CC") }
        };

        private enum ColorTone
        {
            Normal,
            Muted,
            Inactive
        }

        private struct SetSpec
        {
            public ColorMaterialSet Set;
            public string Folder;
            public string Prefix;
            public string TemplatePath;
            public ColorTone Tone;
        }

        private static readonly SetSpec[] Specs =
        {
            new SetSpec { Set = ColorMaterialSet.CrateActive, Folder = "Crate", Prefix = "Crate", TemplatePath = CrateTemplatePath, Tone = ColorTone.Normal },
            new SetSpec { Set = ColorMaterialSet.CrateLocked, Folder = "CrateLocked", Prefix = "CrateLocked", TemplatePath = CrateTemplatePath, Tone = ColorTone.Muted },
            new SetSpec { Set = ColorMaterialSet.CrateInactive, Folder = "CrateInactive", Prefix = "CrateInactive", TemplatePath = CrateTemplatePath, Tone = ColorTone.Inactive },
            new SetSpec { Set = ColorMaterialSet.Cell, Folder = "Cell", Prefix = "Cell", TemplatePath = CellTemplatePath, Tone = ColorTone.Normal },
            new SetSpec { Set = ColorMaterialSet.CellLocked, Folder = "CellLocked", Prefix = "CellLocked", TemplatePath = CellTemplatePath, Tone = ColorTone.Muted },
            new SetSpec { Set = ColorMaterialSet.Bee, Folder = "Bee", Prefix = "Bee", TemplatePath = BeeTemplatePath, Tone = ColorTone.Normal },
            new SetSpec { Set = ColorMaterialSet.LockBody, Folder = "Lock", Prefix = "Lock", TemplatePath = LockTemplatePath, Tone = ColorTone.Normal },
            new SetSpec { Set = ColorMaterialSet.Shard, Folder = "Shard", Prefix = "Shard", TemplatePath = CellTemplatePath, Tone = ColorTone.Normal }
        };

        [MenuItem("Honey Bee Rush/Color/Generate Color Material Sets")]
        public static void Generate()
        {
            Run(false);
        }

        [MenuItem("Honey Bee Rush/Color/Generate Color Material Sets (Force Recolor)")]
        public static void GenerateForceRecolor()
        {
            Run(true);
        }

        [MenuItem("Honey Bee Rush/Color/Validate Color Material Sets")]
        public static void Validate()
        {
            var mapping = AssetDatabase.LoadAssetAtPath<ColorMaterialMapping>(MappingAssetPath);
            if (mapping == null)
            {
                Debug.LogError("[ColorMaterialSetGenerator] No mapping asset at " + MappingAssetPath + ". Run Generate first.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("Color material set coverage (" + PlayableColors.Length + " playable colours):");

            int totalMissing = 0;
            for (int i = 0; i < Specs.Length; i++)
            {
                int present = 0;
                var missing = new List<string>();
                for (int c = 0; c < PlayableColors.Length; c++)
                {
                    Material found;
                    if (mapping.EditorTryGet(Specs[i].Set, PlayableColors[c], out found)) present++;
                    else missing.Add(PlayableColors[c].ToString());
                }

                totalMissing += missing.Count;
                sb.Append("  ").Append(Specs[i].Set).Append(": ").Append(present).Append('/').Append(PlayableColors.Length);
                if (missing.Count > 0) sb.Append("  MISSING: ").Append(string.Join(", ", missing));
                sb.AppendLine();
            }

            sb.Append("  CrateHidden: ").AppendLine(mapping.CrateHidden != null ? "ok" : "MISSING");
            sb.Append("  Total materials expected: ").Append(Specs.Length * PlayableColors.Length + 2);
            sb.Append(", missing: ").Append(totalMissing);

            if (totalMissing > 0) Debug.LogWarning(sb.ToString(), mapping);
            else Debug.Log(sb.ToString(), mapping);
        }

        private static void Run(bool forceRecolor)
        {
            ColorMaterialMapping mapping = EnsureMappingAsset();
            if (mapping == null) return;

            int created = 0;
            int recolored = 0;
            int linked = 0;

            for (int i = 0; i < Specs.Length; i++)
            {
                EnsureFolder(GeneratedRoot + "/" + Specs[i].Folder);
            }
            EnsureFolder(GeneratedRoot + "/Shared");
            AssetDatabase.Refresh();

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < Specs.Length; i++)
                {
                    SetSpec spec = Specs[i];
                    Material template = AssetDatabase.LoadAssetAtPath<Material>(spec.TemplatePath);
                    if (template == null)
                    {
                        Debug.LogError("[ColorMaterialSetGenerator] Missing template " + spec.TemplatePath + " for set " + spec.Set + ".");
                        continue;
                    }

                    string folder = GeneratedRoot + "/" + spec.Folder;

                    for (int c = 0; c < PlayableColors.Length; c++)
                    {
                        ColorType colorType = PlayableColors[c];
                        string path = folder + "/" + spec.Prefix + "_" + colorType + ".mat";
                        Color color = ColorFor(colorType, spec.Tone);

                        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null)
                        {
                            material = new Material(template);
                            material.enableInstancing = true;
                            ApplyColor(material, color);
                            AssetDatabase.CreateAsset(material, path);
                            created++;
                        }
                        else if (forceRecolor)
                        {
                            ApplyColor(material, color);
                            EditorUtility.SetDirty(material);
                            recolored++;
                        }

                        mapping.EditorAssign(spec.Set, colorType, material);
                        linked++;
                    }
                }

                Material crateTemplate = AssetDatabase.LoadAssetAtPath<Material>(CrateTemplatePath);
                if (crateTemplate != null)
                {
                    mapping.EditorSetCrateHidden(EnsureSingle(GeneratedRoot + "/Shared/CrateHidden.mat", crateTemplate, HiddenCrateColor, forceRecolor, ref created, ref recolored));
                    mapping.EditorSetFallback(EnsureSingle(GeneratedRoot + "/Shared/ColorMissing.mat", crateTemplate, FallbackColor, forceRecolor, ref created, ref recolored));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(mapping);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ColorMaterialSetGenerator] " + Specs.Length + " sets x " + PlayableColors.Length +
                      " colours. created " + created + ", recoloured " + recolored + ", linked " + linked + ".", mapping);
        }

        private static Material EnsureSingle(string path, Material template, Color color, bool forceRecolor, ref int created, ref int recolored)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(template);
                material.enableInstancing = true;
                ApplyColor(material, color);
                AssetDatabase.CreateAsset(material, path);
                created++;
                return material;
            }

            if (forceRecolor)
            {
                ApplyColor(material, color);
                EditorUtility.SetDirty(material);
                recolored++;
            }

            return material;
        }

        private static ColorMaterialMapping EnsureMappingAsset()
        {
            var mapping = AssetDatabase.LoadAssetAtPath<ColorMaterialMapping>(MappingAssetPath);
            if (mapping != null) return mapping;

            EnsureFolder("Assets/_Game/Resources/Data");
            mapping = ScriptableObject.CreateInstance<ColorMaterialMapping>();
            AssetDatabase.CreateAsset(mapping, MappingAssetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<ColorMaterialMapping>(MappingAssetPath);
        }

        private static Color ColorFor(ColorType colorType, ColorTone tone)
        {
            Color color;
            if (!Palette.TryGetValue(colorType, out color)) color = FallbackColor;

            switch (tone)
            {
                case ColorTone.Muted:
                    return Color.Lerp(color, LockedTint, LockedBlend);
                case ColorTone.Inactive:
                    Color inactive = Color.Lerp(color, InactiveTint, InactiveBlend) * InactiveShade;
                    inactive.a = 1f;
                    return inactive;
                default:
                    return color;
            }
        }

        private static void ApplyColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            else if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static Color Hex(string hex)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString("#" + hex, out color)) return color;
            return FallbackColor;
        }
    }
}

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using HoneyBeeRush.Data;

namespace HoneyBeeRush.EditorTools
{
    public static class BoardVolumeConverter
    {
        [MenuItem("Honey Bee Rush/Levels/Convert Level Assets To 3D Board")]
        public static void ConvertAllFromMenu()
        {
            ConvertAll(true);
        }

        public static int ConvertAll(bool log)
        {
            string folder = LevelPackImporter.LevelFolder;
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (log)
                {
                    Debug.LogError("[BoardVolumeConverter] Level folder " + folder +
                                   " does not exist. Run Honey Bee Rush/Levels/Import Level Pack To Assets first.");
                }

                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { folder });
            List<string> paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(EditorUtility.NaturalCompare);

            BoardVolumeSettings settings = BoardVolumeSettings.Default;
            int converted = 0;
            int skipped = 0;
            int warnings = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                    if (level == null)
                    {
                        continue;
                    }

                    string levelName = Path.GetFileNameWithoutExtension(path);

                    if (level.IsVolumetric)
                    {
                        skipped++;
                        if (log)
                        {
                            Debug.Log("[BoardVolumeConverter] " + levelName + " skipped: already volumetric (" +
                                      level.CellCount + " cells, " + level.LayerCount + " layers).", level);
                        }

                        continue;
                    }

                    string conversionReport;
                    if (!BoardVolumeBuilder.ConvertPlanarToVolume(level, settings, out conversionReport))
                    {
                        skipped++;
                        warnings++;
                        if (log)
                        {
                            Debug.LogWarning("[BoardVolumeConverter] " + levelName + " not converted: " + conversionReport, level);
                        }

                        continue;
                    }

                    EditorUtility.SetDirty(level);
                    converted++;

                    string capacityReport;
                    bool balanced = LevelLibrary.ValidatePerColorCapacity(level, out capacityReport);

                    string structureReport;
                    bool structured = LevelLibrary.ValidateBoardStructure(level, out structureReport);

                    bool clean = balanced && structured && conversionReport.IndexOf("WARNING", System.StringComparison.Ordinal) < 0;
                    if (!clean)
                    {
                        warnings++;
                    }

                    if (!log)
                    {
                        continue;
                    }

                    string line = "[BoardVolumeConverter] " + levelName + " converted.\n" + conversionReport + "\n" +
                                  capacityReport + structureReport;
                    if (clean)
                    {
                        Debug.Log(line, level);
                    }
                    else
                    {
                        Debug.LogWarning(line, level);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            if (log)
            {
                Debug.Log("[BoardVolumeConverter] Scanned " + paths.Count + " level assets under " + folder + ": " +
                          converted + " converted, " + skipped + " skipped, " + warnings + " with warnings.");
            }

            return converted;
        }
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using HoneyBeeRush.Data;

namespace HoneyBeeRush.EditorTools
{
    public static class LevelPackImporter
    {
        public const string LevelFolder = "Assets/_Game/Resources/Levels";

        [MenuItem("Honey Bee Rush/Levels/Import Level Pack To Assets")]
        public static void ImportAll()
        {
            int count = LevelLibrary.LevelCount;
            if (count <= 0)
            {
                Debug.LogError("[LevelPackImporter] Resources/" + LevelLibrary.LevelPackResourcePath +
                               " holds no levels. Nothing to import.");
                return;
            }

            EnsureFolder();

            int created = 0;
            int updated = 0;
            int failed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 1; i <= count; i++)
                {
                    LevelData source = LevelLibrary.Load(i);
                    if (source == null)
                    {
                        Debug.LogError("[LevelPackImporter] Level " + i + " is missing from the pack.");
                        failed++;
                        continue;
                    }

                    string path = PathFor(i);
                    var existing = AssetDatabase.LoadAssetAtPath<LevelData>(path);

                    if (existing == null)
                    {
                        var asset = ScriptableObject.CreateInstance<LevelData>();
                        CopyInto(source, asset, i);
                        AssetDatabase.CreateAsset(asset, path);
                        created++;
                        continue;
                    }

                    CopyInto(source, existing, i);
                    EditorUtility.SetDirty(existing);
                    updated++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[LevelPackImporter] Imported " + count + " levels into " + LevelFolder +
                      " (" + created + " created, " + updated + " updated, " + failed + " failed).");
        }

        [MenuItem("Honey Bee Rush/Levels/Validate Level Assets")]
        public static void ValidateAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { LevelFolder });
            if (guids.Length == 0)
            {
                Debug.LogError("[LevelPackImporter] No LevelData assets under " + LevelFolder +
                               ". Run Import Level Pack To Assets first.");
                return;
            }

            int warned = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null) continue;

                string structureReport;
                if (level.CellCount == 0)
                {
                    Debug.LogWarning("[LevelPackImporter] " + Path.GetFileNameWithoutExtension(path) +
                                     " has no cells.", level);
                    warned++;
                }
                else if (!LevelLibrary.ValidateBoardStructure(level, out structureReport))
                {
                    Debug.LogWarning("[LevelPackImporter] " + Path.GetFileNameWithoutExtension(path) +
                                     " board structure invalid:\n" + structureReport, level);
                    warned++;
                }
                else if (!level.IsVolumetric)
                {
                    Debug.LogWarning("[LevelPackImporter] " + Path.GetFileNameWithoutExtension(path) +
                                     " is still a planar board. Run Honey Bee Rush/Levels/Convert Level Assets To 3D Board.", level);
                    warned++;
                }

                string report;
                if (!LevelLibrary.ValidatePerColorCapacity(level, out report))
                {
                    Debug.LogWarning("[LevelPackImporter] " + Path.GetFileNameWithoutExtension(path) +
                                     " capacity imbalance:\n" + report, level);
                    warned++;
                }
            }

            Debug.Log("[LevelPackImporter] Validated " + guids.Length + " level assets, " +
                      warned + " with warnings.");
        }

        public static string PathFor(int levelNumber) => LevelFolder + "/Level_" + levelNumber + ".asset";

        public static void EnsureFolder()
        {
            if (Directory.Exists(LevelFolder)) return;

            Directory.CreateDirectory(LevelFolder);
            AssetDatabase.Refresh();
        }

        private static void CopyInto(LevelData source, LevelData target, int levelNumber)
        {
            target.index = levelNumber;
            target.difficulty = source.difficulty;
            target.notes = source.notes;
            target.winRateTarget = source.winRateTarget;
            target.gridScaleMultiplier = source.gridScaleMultiplier;
            target.gridPositionOffset = source.gridPositionOffset;
            target.gridRotation = source.gridRotation;
            target.launchSlots = source.launchSlots;
            target.cellFormat = source.cellFormat;
            target.boardViewYaw = source.boardViewYaw;
            target.boardViewPitch = source.boardViewPitch;

            target.cells = new System.Collections.Generic.List<CellDef>(source.CellCount);
            for (int i = 0; i < source.cells.Count; i++)
            {
                CellDef cell = source.cells[i];
                if (cell == null) continue;

                target.cells.Add(new CellDef
                {
                    q = cell.q,
                    r = cell.r,
                    layer = cell.layer,
                    colorType = cell.colorType,
                    nectar = cell.nectar,
                    locked = cell.locked
                });
            }

            target.crates = new System.Collections.Generic.List<CrateDef>(source.CrateCount);
            for (int i = 0; i < source.crates.Count; i++)
            {
                CrateDef crate = source.crates[i];
                if (crate == null) continue;

                target.crates.Add(new CrateDef
                {
                    colorType = crate.colorType,
                    beeCount = crate.beeCount,
                    column = crate.column,
                    hidden = crate.hidden,
                    linkGroup = crate.linkGroup,
                    releaseDelay = crate.releaseDelay
                });
            }

            target.gridBoardData = null;
            target.Normalize();
        }
    }
}

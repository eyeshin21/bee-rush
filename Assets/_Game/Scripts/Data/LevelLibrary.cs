using System.Collections.Generic;
using System.Text;
using UnityEngine;
using HoneyBeeRush.Core;

namespace HoneyBeeRush.Data
{
    public static class LevelLibrary
    {
        public const string ColorCatalogResourcePath = "Data/colorcatalog";
        public const string LevelPackResourcePath = "Data/levelpack";

        private const int LegacyCellStride = 5;
        private const int VolumeCellStride = 6;
        private const int MaxReportedDuplicates = 10;
        private const int CrateStride = 6;

#pragma warning disable 649

        [System.Serializable]
        private sealed class ColorEntryDto
        {
            public int index;
            public string name;
            public string hex;
            public int family;
        }

        [System.Serializable]
        private sealed class ColorCatalogDto
        {
            public ColorEntryDto[] colors;
        }

        [System.Serializable]
        private sealed class LevelDto
        {
            public string name;
            public int index;
            public int difficulty;
            public string notes;
            public float winRateTarget;
            public float gridScale;
            public float gridOffsetX;
            public float gridOffsetY;
            public float gridRotation;
            public int launchSlots;
            public int cellStride;
            public float boardViewYaw;
            public float boardViewPitch;
            public int[] cellData;
            public int[] crateData;
        }

        [System.Serializable]
        private sealed class LevelPackDto
        {
            public LevelDto[] levels;
        }

#pragma warning restore 649

        private static ColorCatalog _colors;
        private static LevelPackDto _pack;
        private static bool _packLoaded;
        private static readonly Dictionary<int, LevelData> _levelCache = new Dictionary<int, LevelData>();

        public static ColorCatalog Colors
        {
            get
            {
                EnsureColors();
                return _colors;
            }
        }

        public static int LevelCount
        {
            get
            {
                EnsurePack();
                return _pack != null && _pack.levels != null ? _pack.levels.Length : 0;
            }
        }

        public static LevelData Load(int levelIndex)
        {
            if (_levelCache.TryGetValue(levelIndex, out LevelData cached) && cached != null)
            {
                return cached;
            }

            EnsurePack();

            LevelDto dto = FindDto(levelIndex);
            if (dto == null)
            {
                Debug.LogError("[LevelLibrary] Level with index " + levelIndex + " was not found in Resources/" + LevelPackResourcePath + ".");
                return null;
            }

            LevelData data = Build(dto);
            _levelCache[levelIndex] = data;
            return data;
        }

        public static bool ValidatePerColorCapacity(LevelData data, out string report)
        {
            if (data == null)
            {
                report = "[LevelLibrary] ValidatePerColorCapacity: level data is null.";
                return false;
            }

            Dictionary<ColorType, int> nectarByColor = new Dictionary<ColorType, int>();
            Dictionary<ColorType, int> beesByColor = new Dictionary<ColorType, int>();
            List<ColorType> colorTypes = new List<ColorType>();

            int totalNectar = 0;
            int totalBees = 0;

            if (data.cells != null)
            {
                for (int i = 0; i < data.cells.Count; i++)
                {
                    CellDef cell = data.cells[i];
                    if (cell == null)
                    {
                        continue;
                    }

                    Accumulate(nectarByColor, colorTypes, cell.colorType, cell.nectar);
                    totalNectar += cell.nectar;
                }
            }

            if (data.crates != null)
            {
                for (int i = 0; i < data.crates.Count; i++)
                {
                    CrateDef crate = data.crates[i];
                    if (crate == null)
                    {
                        continue;
                    }

                    Accumulate(beesByColor, colorTypes, crate.colorType, crate.beeCount);
                    totalBees += crate.beeCount;
                }
            }

            colorTypes.Sort();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Per-colour capacity for level " + data.index + " (" + data.name + ")");

            bool balanced = true;
            for (int i = 0; i < colorTypes.Count; i++)
            {
                ColorType colorType = colorTypes[i];
                nectarByColor.TryGetValue(colorType, out int nectar);
                beesByColor.TryGetValue(colorType, out int bees);
                if (nectar == bees)
                {
                    continue;
                }

                balanced = false;
                sb.AppendLine("  MISMATCH colour " + colorType + ": nectar " + nectar + ", bees " + bees + ", delta " + (bees - nectar));
            }

            if (balanced)
            {
                sb.AppendLine("  All " + colorTypes.Count + " colour(s) balanced.");
            }

            sb.AppendLine("  TOTALS: nectar " + totalNectar + ", bees " + totalBees + ", delta " + (totalBees - totalNectar));

            report = sb.ToString();
            return balanced && totalNectar == totalBees;
        }

        public static bool ValidateBoardStructure(LevelData data, out string report)
        {
            if (data == null)
            {
                report = "[LevelLibrary] ValidateBoardStructure: level data is null.";
                return false;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Board structure for level " + data.index + " (" + data.name + ")");

            int cellCount = 0;
            int nullCount = 0;
            int minLayer = int.MaxValue;
            int maxLayer = int.MinValue;
            int duplicateCount = 0;
            HashSet<HexCoord3> seen = new HashSet<HexCoord3>();
            List<HexCoord3> duplicates = new List<HexCoord3>();

            if (data.cells != null)
            {
                for (int i = 0; i < data.cells.Count; i++)
                {
                    CellDef cell = data.cells[i];
                    if (cell == null)
                    {
                        nullCount++;
                        continue;
                    }

                    cellCount++;
                    if (cell.layer < minLayer)
                    {
                        minLayer = cell.layer;
                    }

                    if (cell.layer > maxLayer)
                    {
                        maxLayer = cell.layer;
                    }

                    HexCoord3 coord = new HexCoord3(cell.q, cell.r, cell.layer);
                    if (seen.Add(coord))
                    {
                        continue;
                    }

                    duplicateCount++;
                    if (duplicates.Count < MaxReportedDuplicates)
                    {
                        duplicates.Add(coord);
                    }
                }
            }

            string format = FormatName(data.cellFormat);

            if (cellCount == 0)
            {
                sb.AppendLine("  ERROR no cells (format " + format + ").");
                report = sb.ToString();
                return false;
            }

            bool valid = true;
            sb.AppendLine("  " + cellCount + " cell(s), layers " + minLayer + ".." + maxLayer + " (" + (maxLayer - minLayer + 1) + " layer(s)), format " + format + ".");

            if (nullCount > 0)
            {
                sb.AppendLine("  NOTE " + nullCount + " null cell entries ignored.");
            }

            if (data.cellFormat != LevelData.CellFormatPlanar && data.cellFormat != LevelData.CellFormatVolume)
            {
                valid = false;
                sb.AppendLine("  ERROR unknown cellFormat " + data.cellFormat + ".");
            }

            if (duplicateCount > 0)
            {
                valid = false;
                sb.Append("  ERROR " + duplicateCount + " duplicate (q, r, layer) cell(s):");
                for (int i = 0; i < duplicates.Count; i++)
                {
                    sb.Append(" " + duplicates[i]);
                }

                if (duplicateCount > duplicates.Count)
                {
                    sb.Append(" ... +" + (duplicateCount - duplicates.Count) + " more");
                }

                sb.AppendLine();
            }

            if (data.cellFormat == LevelData.CellFormatPlanar && (minLayer != 0 || maxLayer != 0))
            {
                valid = false;
                sb.AppendLine("  ERROR planar-format level carries layers " + minLayer + ".." + maxLayer + "; convert it or mark it volumetric.");
            }

            if (valid)
            {
                sb.AppendLine("  OK.");
            }

            report = sb.ToString();
            return valid;
        }

        public static void ClearCache()
        {
            _levelCache.Clear();
            _pack = null;
            _packLoaded = false;
            _colors = null;
        }

        private static void Accumulate(Dictionary<ColorType, int> map, List<ColorType> keys, ColorType key, int amount)
        {
            if (map.TryGetValue(key, out int current))
            {
                map[key] = current + amount;
                return;
            }

            map[key] = amount;
            if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        private static LevelDto FindDto(int levelIndex)
        {
            if (_pack == null || _pack.levels == null)
            {
                return null;
            }

            for (int i = 0; i < _pack.levels.Length; i++)
            {
                LevelDto dto = _pack.levels[i];
                if (dto != null && dto.index == levelIndex)
                {
                    return dto;
                }
            }

            return null;
        }

        private static LevelData Build(LevelDto dto)
        {
            LevelData data = ScriptableObject.CreateInstance<LevelData>();
            data.name = string.IsNullOrEmpty(dto.name) ? "Level" + dto.index : dto.name;
            data.index = dto.index;
            data.difficulty = ToTier(dto.difficulty);
            data.notes = dto.notes ?? string.Empty;
            data.winRateTarget = dto.winRateTarget;
            data.gridScaleMultiplier = dto.gridScale;
            data.gridPositionOffset = new Vector2(dto.gridOffsetX, dto.gridOffsetY);
            data.gridRotation = dto.gridRotation;
            data.launchSlots = dto.launchSlots;

            int stride = ResolveCellStride(dto);
            data.cells = ExpandCells(dto, stride);
            data.crates = ExpandCrates(dto);

            if (stride == VolumeCellStride)
            {
                data.cellFormat = LevelData.CellFormatVolume;
                data.boardViewYaw = dto.boardViewYaw;
                data.boardViewPitch = dto.boardViewPitch;
            }
            else if (!BoardVolumeBuilder.ConvertPlanarToVolume(data, BoardVolumeSettings.Default, out string conversionReport))
            {
                Debug.LogWarning("[LevelLibrary] Level " + dto.index + " was not converted to a 3D board: " + conversionReport);
            }

            return data;
        }

        private static int ResolveCellStride(LevelDto dto)
        {
            if (dto.cellStride == VolumeCellStride)
            {
                return VolumeCellStride;
            }

            if (dto.cellStride != 0 && dto.cellStride != LegacyCellStride)
            {
                Debug.LogError("[LevelLibrary] Level " + dto.index + " has unsupported cellStride " + dto.cellStride + "; reading cellData as legacy stride " + LegacyCellStride + ".");
            }

            return LegacyCellStride;
        }

        private static List<CellDef> ExpandCells(LevelDto dto, int stride)
        {
            int[] raw = dto.cellData;
            if (raw == null || raw.Length == 0)
            {
                return new List<CellDef>();
            }

            int count = raw.Length / stride;
            if (raw.Length % stride != 0)
            {
                Debug.LogError("[LevelLibrary] Level " + dto.index + " cellData length " + raw.Length + " is not a multiple of " + stride + "; trailing values ignored.");
            }

            int offset = stride == VolumeCellStride ? 1 : 0;
            List<CellDef> cells = new List<CellDef>(count);
            for (int i = 0; i < count; i++)
            {
                int o = i * stride;
                CellDef cell = new CellDef
                {
                    q = raw[o],
                    r = raw[o + 1],
                    layer = offset > 0 ? raw[o + 2] : 0,
                    colorType = (ColorType)raw[o + offset + 2],
                    nectar = raw[o + offset + 3],
                    locked = raw[o + offset + 4] != 0
                };
                cells.Add(cell);
            }

            return cells;
        }

        private static List<CrateDef> ExpandCrates(LevelDto dto)
        {
            int[] raw = dto.crateData;
            if (raw == null || raw.Length == 0)
            {
                return new List<CrateDef>();
            }

            int count = raw.Length / CrateStride;
            if (raw.Length % CrateStride != 0)
            {
                Debug.LogError("[LevelLibrary] Level " + dto.index + " crateData length " + raw.Length + " is not a multiple of " + CrateStride + "; trailing values ignored.");
            }

            List<CrateDef> crates = new List<CrateDef>(count);
            for (int i = 0; i < count; i++)
            {
                int o = i * CrateStride;
                CrateDef crate = new CrateDef
                {
                    colorType = (ColorType)raw[o],
                    beeCount = raw[o + 1],
                    column = raw[o + 2],
                    hidden = raw[o + 3] != 0,
                    linkGroup = raw[o + 4],
                    releaseDelay = raw[o + 5]
                };
                crates.Add(crate);
            }

            return crates;
        }

        private static string FormatName(int cellFormat)
        {
            if (cellFormat == LevelData.CellFormatVolume)
            {
                return "volume";
            }

            if (cellFormat == LevelData.CellFormatPlanar)
            {
                return "planar";
            }

            return "unknown " + cellFormat;
        }

        private static DifficultyTier ToTier(int value)
        {
            int min = (int)DifficultyTier.Easy;
            int max = (int)DifficultyTier.SuperHard;
            return (DifficultyTier)Mathf.Clamp(value, min, max);
        }

        private static void EnsurePack()
        {
            if (_packLoaded && _pack != null)
            {
                return;
            }

            _packLoaded = true;
            _pack = new LevelPackDto();
            _pack.levels = new LevelDto[0];

            TextAsset asset = Resources.Load<TextAsset>(LevelPackResourcePath);
            if (asset == null)
            {
                Debug.LogError("[LevelLibrary] Missing TextAsset at Resources/" + LevelPackResourcePath + ".");
                return;
            }

            LevelPackDto parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<LevelPackDto>(asset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LevelLibrary] Failed to parse Resources/" + LevelPackResourcePath + ": " + e.Message);
            }

            if (parsed == null || parsed.levels == null)
            {
                Debug.LogError("[LevelLibrary] Resources/" + LevelPackResourcePath + " contained no levels array.");
                return;
            }

            _pack = parsed;
        }

        private static void EnsureColors()
        {
            if (_colors != null)
            {
                return;
            }

            ColorCatalog catalog = ScriptableObject.CreateInstance<ColorCatalog>();
            catalog.name = "ColorCatalog";
            catalog.entries = new ColorEntry[0];
            catalog.RebuildIndex();
            _colors = catalog;

            TextAsset asset = Resources.Load<TextAsset>(ColorCatalogResourcePath);
            if (asset == null)
            {
                Debug.LogError("[LevelLibrary] Missing TextAsset at Resources/" + ColorCatalogResourcePath + ".");
                return;
            }

            ColorCatalogDto dto = null;
            try
            {
                dto = JsonUtility.FromJson<ColorCatalogDto>(asset.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LevelLibrary] Failed to parse Resources/" + ColorCatalogResourcePath + ": " + e.Message);
            }

            if (dto == null || dto.colors == null)
            {
                Debug.LogError("[LevelLibrary] Resources/" + ColorCatalogResourcePath + " contained no colors array.");
                return;
            }

            ColorEntry[] entries = new ColorEntry[dto.colors.Length];
            for (int i = 0; i < dto.colors.Length; i++)
            {
                ColorEntryDto source = dto.colors[i];
                ColorEntry entry = new ColorEntry
                {
                    index = source != null ? source.index : i,
                    name = source != null && !string.IsNullOrEmpty(source.name) ? source.name : ColorCatalog.UnknownName,
                    family = source != null ? source.family : 0,
                    color = ColorCatalog.MissingColor
                };

                string hex = source != null ? source.hex : null;
                if (!string.IsNullOrEmpty(hex))
                {
                    string html = hex[0] == '#' ? hex : "#" + hex;
                    if (ColorUtility.TryParseHtmlString(html, out Color parsedColor))
                    {
                        parsedColor.a = 1f;
                        entry.color = parsedColor;
                    }
                    else
                    {
                        Debug.LogError("[LevelLibrary] Colour " + entry.index + " (" + entry.name + ") has unparseable hex '" + hex + "'.");
                    }
                }

                entries[i] = entry;
            }

            catalog.entries = entries;
            catalog.RebuildIndex();
        }
    }
}

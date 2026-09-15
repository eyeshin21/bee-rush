using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using HoneyBeeRush.Data;
using HoneyBeeRush.Managers;

namespace HoneyBeeRush.EditorTools
{
    public sealed class LevelCatalog
    {
        private static readonly Regex s_levelName = new Regex(@"^Level_(\d+)$");

        private readonly List<LevelData> m_levels = new List<LevelData>();
        private string[] m_displayNames = new string[0];

        public IReadOnlyList<LevelData> Levels => m_levels;
        public string[] DisplayNames => m_displayNames;
        public int Count => m_levels.Count;
        public string LoadError { get; private set; }

        public void Refresh()
        {
            m_levels.Clear();
            m_displayNames = new string[0];
            LoadError = null;

            string folder = LevelPackImporter.LevelFolder;

            if (!Directory.Exists(folder))
            {
                LoadError = "Level folder '" + folder + "' does not exist. " +
                            "Run Honey Bee Rush/Levels/Import Level Pack To Assets.";
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:LevelData", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ParentFolder(path) != folder) continue;

                var asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (asset != null) m_levels.Add(asset);
            }

            m_levels.Sort(CompareByNumber);

            m_displayNames = new string[m_levels.Count];
            for (int i = 0; i < m_levels.Count; i++)
            {
                m_displayNames[i] = "#" + (i + 1) + "  " + DisplayName(m_levels[i]);
            }
        }

        public LevelData At(int index) => index >= 0 && index < m_levels.Count ? m_levels[index] : null;

        public int IndexOf(LevelData level)
        {
            for (int i = 0; i < m_levels.Count; i++)
            {
                if (m_levels[i] == level) return i;
            }
            return -1;
        }

        public LevelData CreateNewLevel()
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            level.launchSlots = 5;
            level.difficulty = Core.DifficultyTier.Easy;
            level.cellFormat = LevelData.CellFormatVolume;
            level.Normalize();

            return CreateAsset(level, NextFreeLevelNumber());
        }

        public LevelData Duplicate(LevelData source)
        {
            if (source == null) return null;

            var copy = Object.Instantiate(source);
            copy.name = string.Empty;

            return CreateAsset(copy, NextFreeLevelNumber());
        }

        public static void Save(LevelData level)
        {
            if (level == null) return;

            level.Normalize();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
        }

        public static string DisplayName(LevelData level)
        {
            if (level == null) return "(none)";

            string path = AssetDatabase.GetAssetPath(level);
            if (!string.IsNullOrEmpty(path)) return Path.GetFileNameWithoutExtension(path);

            return string.IsNullOrEmpty(level.name) ? "Level " + level.index : level.name;
        }

        public static int LevelNumber(LevelData level)
        {
            if (level == null) return 0;

            Match match = s_levelName.Match(DisplayName(level));
            if (match.Success && int.TryParse(match.Groups[1].Value, out int number)) return number;

            return level.index;
        }

        public static bool IsLoadableByNumber(LevelData level) =>
            level != null && s_levelName.IsMatch(DisplayName(level));

        private LevelData CreateAsset(LevelData level, int number)
        {
            level.index = number;
            level.Normalize();

            LevelPackImporter.EnsureFolder();

            string path = AssetDatabase.GenerateUniqueAssetPath(LevelPackImporter.PathFor(number));
            AssetDatabase.CreateAsset(level, path);
            AssetDatabase.SaveAssets();
            Refresh();

            return level;
        }

        private int NextFreeLevelNumber()
        {
            var usedNumbers = new HashSet<int>();
            for (int i = 0; i < m_levels.Count; i++) usedNumbers.Add(LevelNumber(m_levels[i]));

            int candidate = Mathf.Max(1, m_levels.Count + 1);
            while (usedNumbers.Contains(candidate) || File.Exists(LevelPackImporter.PathFor(candidate)))
            {
                candidate++;
            }

            return candidate;
        }

        private static int CompareByNumber(LevelData a, LevelData b)
        {
            int na = LevelNumber(a);
            int nb = LevelNumber(b);

            if (na != nb) return na.CompareTo(nb);
            return EditorUtility.NaturalCompare(DisplayName(a), DisplayName(b));
        }

        private static string ParentFolder(string assetPath) =>
            string.IsNullOrEmpty(assetPath) ? string.Empty : Path.GetDirectoryName(assetPath).Replace("\\", "/");

        [MenuItem("Honey Bee Rush/Levels/New Level Asset")]
        private static void MenuCreateNewLevel()
        {
            var catalog = new LevelCatalog();
            catalog.Refresh();

            LevelData created = catalog.CreateNewLevel();
            if (created == null) return;

            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
            Debug.Log("[LevelCatalog] Created " + DisplayName(created) + ".");
        }

        [MenuItem("Honey Bee Rush/Levels/Play Selected Level")]
        private static void MenuPlaySelectedLevel()
        {
            var level = Selection.activeObject as LevelData;
            if (level == null)
            {
                Debug.LogError("[LevelCatalog] Select a LevelData asset first.");
                return;
            }

            if (!IsLoadableByNumber(level))
            {
                Debug.LogError("[LevelCatalog] " + DisplayName(level) +
                               " is not named Level_<number>, so GameManager cannot resolve it.");
                return;
            }

            EditorPrefs.SetInt(GameManager.EditorTestLevelKey, LevelNumber(level));
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Honey Bee Rush/Levels/Play Selected Level", true)]
        private static bool MenuPlaySelectedLevelValidate() => Selection.activeObject is LevelData;
    }
}

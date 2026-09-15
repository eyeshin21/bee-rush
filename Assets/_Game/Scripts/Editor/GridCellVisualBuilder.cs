using System.Collections.Generic;
using System.IO;
using HoneyBeeRush.Gameplay.Core;
using UnityEditor;
using UnityEngine;

namespace HoneyBeeRush.EditorTools
{
    public static class GridCellVisualBuilder
    {
        private const string SourcePrefabPath = "Assets/_Game/Models/GridCell/HexaFallGridcell.prefab";
        private const string CellPrefabPath = "Assets/_Game/Prefabs/GridCell.prefab";
        private const string PieceFolder = "Assets/_Game/Prefabs/GridCellPieces";
        private const string VisualName = "Visual";
        private const string FloorName = "bottom_slot";

        private static readonly Dictionary<BlockGridBorderType, string> BorderSourceNames = new Dictionary<BlockGridBorderType, string>
        {
            { BlockGridBorderType.Top, "Top" },
            { BlockGridBorderType.Bottom, "Bot" },
            { BlockGridBorderType.Left, "Left" },
            { BlockGridBorderType.Right, "Right" },
            { BlockGridBorderType.TopLeft, "TopLeft" },
            { BlockGridBorderType.TopRight, "TopRight" },
            { BlockGridBorderType.BottomLeft, "BotLeft" },
            { BlockGridBorderType.BottomRight, "BotRight" },
            { BlockGridBorderType.LeftTopRight, "LeftTopRight" },
            { BlockGridBorderType.TopRightBottom, "TopRightBot" },
            { BlockGridBorderType.RightBottomLeft, "RightBotLeft" },
            { BlockGridBorderType.BottomLeftTop, "BotLeftTop" },
            { BlockGridBorderType.ClosedBorder, "CloseBorder" },
            { BlockGridBorderType.Wall, "Wall" },
            { BlockGridBorderType.TopBottom, "TopBot" },
            { BlockGridBorderType.LeftRight, "LeftRight" }
        };

        private static readonly Dictionary<BlockGridCornerType, string> CornerSourceNames = new Dictionary<BlockGridCornerType, string>
        {
            { BlockGridCornerType.TopLeftOut, "CornerTopLeftOut" },
            { BlockGridCornerType.TopRightOut, "CornerTopRightOut" },
            { BlockGridCornerType.BottomLeftOut, "CornerBotLeftOut" },
            { BlockGridCornerType.BottomRightOut, "CornerBotRightOut" }
        };

        [MenuItem("Honey Bee Rush/Grid/Build Grid Cell Visuals")]
        public static void Build()
        {
            GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (sourceAsset == null)
            {
                Debug.LogError("[GridCellVisualBuilder] Missing source prefab at " + SourcePrefabPath + ". Aborting.");
                return;
            }

            if (!Directory.Exists(PieceFolder))
            {
                Directory.CreateDirectory(PieceFolder);
                AssetDatabase.Refresh();
            }

            GameObject source = Object.Instantiate(sourceAsset);
            Transform visual = source.transform.Find(VisualName);
            if (visual == null)
            {
                Object.DestroyImmediate(source);
                Debug.LogError("[GridCellVisualBuilder] Source prefab has no '" + VisualName + "' child. Aborting.");
                return;
            }

            Vector3 visualOffset = visual.localPosition;
            var borderPrefabs = new Dictionary<BlockGridBorderType, GameObject>();
            var cornerPrefabs = new Dictionary<BlockGridCornerType, GameObject>();

            try
            {
                foreach (KeyValuePair<BlockGridBorderType, string> pair in BorderSourceNames)
                {
                    GameObject piece = ExtractPiece(visual, pair.Value, visualOffset, "Border_" + pair.Key);
                    if (piece != null) borderPrefabs[pair.Key] = piece;
                }

                foreach (KeyValuePair<BlockGridCornerType, string> pair in CornerSourceNames)
                {
                    GameObject piece = ExtractPiece(visual, pair.Value, visualOffset, "Corner_" + pair.Key);
                    if (piece != null) cornerPrefabs[pair.Key] = piece;
                }

                BuildCellPrefab(source, borderPrefabs, cornerPrefabs);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GridCellVisualBuilder] Extracted " + borderPrefabs.Count + " border and " + cornerPrefabs.Count
                + " corner pieces from " + SourcePrefabPath + " into " + PieceFolder + ".");
        }

        private static GameObject ExtractPiece(Transform visual, string sourceName, Vector3 visualOffset, string assetName)
        {
            Transform variant = visual.Find(sourceName);
            if (variant == null)
            {
                Debug.LogWarning("[GridCellVisualBuilder] Source prefab has no variant named '" + sourceName + "'.");
                return null;
            }

            var root = new GameObject(assetName);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            root.transform.localScale = Vector3.one;

            var holder = new GameObject(VisualName);
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = visualOffset;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            Vector3 localPosition = variant.localPosition;
            Quaternion localRotation = variant.localRotation;
            Vector3 localScale = variant.localScale;

            variant.SetParent(holder.transform, false);
            variant.localPosition = localPosition;
            variant.localRotation = localRotation;
            variant.localScale = localScale;
            variant.gameObject.SetActive(true);

            StripColliders(root);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, PieceFolder + "/" + assetName + ".prefab");
            Object.DestroyImmediate(root);
            return asset;
        }

        private static void BuildCellPrefab(GameObject source, Dictionary<BlockGridBorderType, GameObject> borderPrefabs, Dictionary<BlockGridCornerType, GameObject> cornerPrefabs)
        {
            GameObject cell = PrefabUtility.LoadPrefabContents(CellPrefabPath);
            if (cell == null)
            {
                Debug.LogError("[GridCellVisualBuilder] Could not load " + CellPrefabPath + ".");
                return;
            }

            try
            {
                var controller = cell.GetComponent<GridCellController>();
                if (controller == null)
                {
                    Debug.LogError("[GridCellVisualBuilder] " + CellPrefabPath + " has no GridCellController.");
                    return;
                }

                DestroyChild(cell.transform, "Cube");
                DestroyChild(cell.transform, "Borders");
                DestroyChild(cell.transform, "Corners");
                DestroyChild(cell.transform, "Floor");
                Transform pieceRoot = ResetChild(cell.transform, "Pieces");

                MeshRenderer floorRenderer = BuildFloor(source, cell.transform);

                var serialized = new SerializedObject(controller);
                WriteMap(serialized, "m_listBorder", borderPrefabs);
                WriteMap(serialized, "m_listCorner", cornerPrefabs);
                serialized.FindProperty("m_pieceRoot").objectReferenceValue = pieceRoot;
                serialized.FindProperty("m_padRenderer").objectReferenceValue = floorRenderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(cell, CellPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(cell);
            }
        }

        private static MeshRenderer BuildFloor(GameObject source, Transform cellRoot)
        {
            Transform sourceFloor = source.transform.Find(FloorName);
            if (sourceFloor == null)
            {
                Debug.LogWarning("[GridCellVisualBuilder] Source prefab has no '" + FloorName + "' child; cell will have no floor.");
                return null;
            }

            var holder = new GameObject("Floor");
            holder.transform.SetParent(cellRoot, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            holder.transform.localScale = Vector3.one;

            GameObject floor = Object.Instantiate(sourceFloor.gameObject, holder.transform, false);
            floor.name = FloorName;
            floor.transform.localPosition = sourceFloor.localPosition;
            floor.transform.localRotation = sourceFloor.localRotation;
            floor.transform.localScale = sourceFloor.localScale;
            floor.SetActive(true);

            StripColliders(holder);
            return floor.GetComponentInChildren<MeshRenderer>();
        }

        private static void WriteMap<TKey>(SerializedObject serialized, string fieldName, Dictionary<TKey, GameObject> entries)
        {
            SerializedProperty list = serialized.FindProperty(fieldName + "._serializedList");
            if (list == null)
            {
                Debug.LogError("[GridCellVisualBuilder] Could not find serialized list for " + fieldName + ".");
                return;
            }

            list.arraySize = entries.Count;
            int index = 0;
            foreach (KeyValuePair<TKey, GameObject> pair in entries)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("Key").intValue = (int)(object)pair.Key;
                entry.FindPropertyRelative("Value").objectReferenceValue = pair.Value;
                index++;
            }
        }

        private static void StripColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Object.DestroyImmediate(colliders[i]);
            }
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static Transform ResetChild(Transform parent, string name)
        {
            DestroyChild(parent, name);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }
    }
}

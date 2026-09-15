using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay;
using HoneyBeeRush.Gameplay.Config;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.Gameplay.Domain;
using HoneyBeeRush.Gameplay.Runtime;
using HoneyBeeRush.Managers;
using HoneyBeeRush.View;

namespace HoneyBeeRush.EditorTools
{
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Gameplay.unity";
        public const string TuningAssetPath = "Assets/_Game/Resources/Data/TuningConfig.asset";

        private const string PrefabFolder = "Assets/_Game/Prefabs";
        private const string ColorMappingPath = "Assets/_Game/Resources/Data/ColorMaterialMapping.asset";

        [MenuItem("Honey Bee Rush/Build Gameplay Scene")]
        public static void BuildScene() => BuildScene(false, false);

        [MenuItem("Honey Bee Rush/Build Gameplay Scene (Recording)")]
        public static void BuildSceneForRecording() => BuildScene(true, true);

        public static void BuildScene(bool autoPlay, bool withRecorder)
        {
            EnsureTuningAsset();

            string dir = Path.GetDirectoryName(ScenePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = BuildCamera();
            LevelController levelController = BuildGameRoot(camera, autoPlay, withRecorder);
            BuildManagers(levelController);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.68f, 1f);
            RenderSettings.fog = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[GameSceneBuilder] Gameplay scene built at " + ScenePath +
                      " (autoPlay=" + autoPlay + ", recorder=" + withRecorder + ").");
        }

        private static Camera BuildCamera()
        {
            var camGo = new GameObject("MainCamera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, -0.9f, -19.5f);
            camGo.transform.rotation = Quaternion.identity;

            var camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03137255f, 0.54901963f, 0.36078432f, 1f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 120f;
            camera.depth = 0;

            camGo.AddComponent<AudioListener>();
            return camera;
        }

        private static LevelController BuildGameRoot(Camera camera, bool autoPlay, bool withRecorder)
        {
            var root = new GameObject("Game");
            var levelController = root.AddComponent<LevelController>();

            var colorMaterials = AssetDatabase.LoadAssetAtPath<ColorMaterialMapping>(ColorMappingPath);
            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningAssetPath);

            BoardController board = BuildBoard(root.transform);
            GridBoardController gridBoard = BuildGridBoard(root.transform);
            SlotBoardController slots = BuildSlots(root.transform);
            BeeSwarmController swarm = BuildSwarm(root.transform);

            var envView = NewChild<EnvironmentView>(root.transform, "Environment");
            var hiveView = NewChild<HiveView>(root.transform, "Hive");

            var hiveModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/BeeHive.prefab");
            if (hiveModel != null)
            {
                var hiveSo = new SerializedObject(hiveView);
                hiveSo.FindProperty("m_hivePrefab").objectReferenceValue = hiveModel;
                hiveSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[GameSceneBuilder] Assets/_Game/Prefabs/BeeHive.prefab not found; HiveView left unwired.");
            }

            var so = new SerializedObject(levelController);
            so.FindProperty("m_board").objectReferenceValue = board;
            so.FindProperty("m_gridBoard").objectReferenceValue = gridBoard;
            so.FindProperty("m_slots").objectReferenceValue = slots;
            so.FindProperty("m_beeSwarm").objectReferenceValue = swarm;
            so.FindProperty("m_camera").objectReferenceValue = camera;
            so.FindProperty("m_envView").objectReferenceValue = envView;
            so.FindProperty("m_hiveView").objectReferenceValue = hiveView;
            so.FindProperty("m_tuning").objectReferenceValue = tuning;
            so.FindProperty("m_colorMaterials").objectReferenceValue = colorMaterials;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (autoPlay) root.AddComponent<AutoPlayer>();

            if (withRecorder)
            {
                var recorder = root.AddComponent<FrameRecorder>();
                recorder.width = 720;
                recorder.height = 1280;
                recorder.captureFramerate = 30;
                recorder.outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Recordings/frames"));
            }

            return levelController;
        }

        private static BoardController BuildBoard(Transform parent)
        {
            BoardController board = NewChild<BoardController>(parent, "Board");
            Transform boardRoot = NewChild(board.transform, "BoardRoot");

            var so = new SerializedObject(board);
            so.FindProperty("m_cellPrefab").objectReferenceValue = LoadPrefab<CellController>("Cell");
            so.FindProperty("m_shardPrefab").objectReferenceValue = LoadPrefab<TileShardController>("TileShard");
            so.FindProperty("m_boardRoot").objectReferenceValue = boardRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            return board;
        }

        private static GridBoardController BuildGridBoard(Transform parent)
        {
            GridBoardController gridBoard = NewChild<GridBoardController>(parent, "GridBoard");
            Transform cellRoot = NewChild(gridBoard.transform, "CellRoot");

            var so = new SerializedObject(gridBoard);
            so.FindProperty("m_cellPrefab").objectReferenceValue = LoadPrefab<GridCellController>("GridCell");
            so.FindProperty("m_cratePrefab").objectReferenceValue = LoadPrefab<CrateController>("Crate");
            so.FindProperty("m_cellRoot").objectReferenceValue = cellRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            return gridBoard;
        }

        private static SlotBoardController BuildSlots(Transform parent)
        {
            SlotBoardController slots = NewChild<SlotBoardController>(parent, "Slots");
            Transform slotRoot = NewChild(slots.transform, "SlotRoot");

            var so = new SerializedObject(slots);
            so.FindProperty("m_slotPrefab").objectReferenceValue = LoadPrefab<SlotController>("Slot");
            so.FindProperty("m_slotRoot").objectReferenceValue = slotRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            return slots;
        }

        private static BeeSwarmController BuildSwarm(Transform parent)
        {
            BeeSwarmController swarm = NewChild<BeeSwarmController>(parent, "Swarm");
            Transform swarmRoot = NewChild(swarm.transform, "SwarmRoot");

            var so = new SerializedObject(swarm);
            so.FindProperty("m_beePrefab").objectReferenceValue = LoadPrefab<BeeController>("Bee");
            so.FindProperty("m_swarmRoot").objectReferenceValue = swarmRoot;
            so.ApplyModifiedPropertiesWithoutUndo();

            return swarm;
        }

        private static void BuildManagers(LevelController levelController)
        {
            var gameManagerGo = new GameObject("GameManager");
            var gameManager = gameManagerGo.AddComponent<GameManager>();

            var tuning = AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningAssetPath);

            var gmSo = new SerializedObject(gameManager);
            gmSo.FindProperty("m_levelController").objectReferenceValue = levelController;
            gmSo.FindProperty("m_tuning").objectReferenceValue = tuning;
            gmSo.FindProperty("m_maxLevel").intValue = Mathf.Max(1, LevelLibrary.LevelCount);
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            var uiManagerGo = new GameObject("UIManager");
            uiManagerGo.AddComponent<UIManager>();
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static T NewChild<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static T LoadPrefab<T>(string prefabName) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<T>(PrefabFolder + "/" + prefabName + ".prefab");
            if (prefab == null)
            {
                Debug.LogError("[GameSceneBuilder] Missing prefab " + PrefabFolder + "/" + prefabName + ".prefab.");
            }
            return prefab;
        }

        [MenuItem("Honey Bee Rush/Create Tuning Config Asset")]
        public static void EnsureTuningAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<TuningConfig>(TuningAssetPath) != null) return;

            string dir = Path.GetDirectoryName(TuningAssetPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var asset = ScriptableObject.CreateInstance<TuningConfig>();
            AssetDatabase.CreateAsset(asset, TuningAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[GameSceneBuilder] Created " + TuningAssetPath + ".");
        }

        [MenuItem("Honey Bee Rush/Validate All Levels")]
        public static void ValidateAllLevels()
        {
            int count = LevelLibrary.LevelCount;
            int bad = 0;
            int structureFailures = 0;

            for (int i = 1; i <= count; i++)
            {
                string path = LevelPackImporter.PathFor(i);
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    Debug.LogError("[GameSceneBuilder] Level asset " + path + " missing.");
                    bad++;
                    continue;
                }

                bool warned = false;

                if (!level.IsVolumetric)
                {
                    Debug.LogWarning("[GameSceneBuilder] " + path + " is still a planar board. Run Honey Bee Rush/Levels/Convert Level Assets To 3D Board.");
                    structureFailures++;
                    warned = true;
                }

                string report;
                if (!LevelLibrary.ValidatePerColorCapacity(level, out report))
                {
                    Debug.LogWarning("[GameSceneBuilder] Level " + i + " imbalance:\n" + report);
                    warned = true;
                }

                string structureReport;
                if (!LevelLibrary.ValidateBoardStructure(level, out structureReport))
                {
                    Debug.LogWarning("[GameSceneBuilder] Level " + i + " board structure invalid:\n" + structureReport);
                    structureFailures++;
                    warned = true;
                }

                if (warned) bad++;
            }

            Debug.Log("[GameSceneBuilder] Validated " + count + " levels, " + bad + " with warnings (" +
                      structureFailures + " board structure failures).");
        }
    }
}

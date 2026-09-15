using System.Text;
using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Core;
using HoneyBeeRush.View;
using UnityEditor;
using UnityEngine;

namespace HoneyBeeRush.EditorTools
{
    public static class GameplayModelApplier
    {
        private const string CellPrefabPath = "Assets/_Game/Prefabs/Cell.prefab";
        private const string CratePrefabPath = "Assets/_Game/Prefabs/Crate.prefab";
        private const string BeePrefabPath = "Assets/_Game/Prefabs/Bee.prefab";
        private const string ShardPrefabPath = "Assets/_Game/Prefabs/TileShard.prefab";

        private const string HexaModelPath = "Assets/_Game/Models/Hexa.fbx";
        private const string CubeModelPath = "Assets/_Game/Models/Cube.fbx";
        private const string BeeModelPath = "Assets/_Game/Models/Bee.fbx";

        private const string BeeStripeMaterialPath = "Assets/_Game/Models/Materials/BeeStripe/Bee_Stripe.mat";
        private const string BeeWingMaterialPath = "Assets/_Game/Models/Materials/BeeWings/Bee_Wing.mat";

        private const ColorType PreviewColor = ColorType.Yellow;

        private const float LegacyCellCircumRadius = 1.10606f;
        private const float LegacyBeeLength = 0.3240f;
        private const float LegacyShardEdge = 1f;

        private static readonly StringBuilder Report = new StringBuilder();

        [MenuItem("Honey Bee Rush/Models/Apply Gameplay Models")]
        public static void ApplyAll()
        {
            Report.Clear();
            Report.AppendLine("Apply gameplay models:");

            var mapping = AssetDatabase.LoadAssetAtPath<ColorMaterialMapping>(ColorMaterialSetGenerator.MappingAssetPath);
            if (mapping == null)
            {
                Debug.LogError("[GameplayModelApplier] Missing " + ColorMaterialSetGenerator.MappingAssetPath + ". Run the colour set generator first.");
                return;
            }

            ApplyCell(mapping);
            ApplyCrate(mapping);
            ApplyShard(mapping);
            ApplyBee(mapping);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(Report.ToString());
        }

        private static LayoutConfig LoadTunedLayout()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<Gameplay.Config.TuningConfig>("Assets/_Game/Resources/Data/TuningConfig.asset");
            if (tuning != null && tuning.Layout != null) return tuning.Layout;

            Report.AppendLine("  WARNING: TuningConfig asset not found, falling back to LayoutConfig defaults.");
            return new LayoutConfig();
        }

        private static void ApplyCell(ColorMaterialMapping mapping)
        {
            Mesh hexa = LoadMesh(HexaModelPath, "Hexa");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HexaModelPath);
            if (hexa == null || model == null)
            {
                Report.AppendLine("  Cell: SKIPPED, missing " + HexaModelPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(CellPrefabPath);
            try
            {
                LayoutConfig layout = LoadTunedLayout();
                float rootXY = layout.hexSize / layout.tileMeshCircumRadius * layout.tileGapScale;
                float rootZ = layout.tileDepth / layout.tileMeshDepth;
                float meshRadius = MaxPlanarRadius(hexa);
                float inPlane = LegacyCellCircumRadius / meshRadius;
                float depth = inPlane * (rootXY / rootZ);

                StripRootRenderer(root);

                Transform visual = root.transform.Find("Hexa");
                if (visual == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    instance.name = "Hexa";
                    visual = instance.transform;
                }

                visual.SetSiblingIndex(0);
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.localScale = new Vector3(inPlane, depth, inPlane);

                var renderer = visual.GetComponentInChildren<MeshRenderer>(true);
                var filter = visual.GetComponentInChildren<MeshFilter>(true);
                SetSingleMaterial(renderer, Resolve(mapping, ColorMaterialSet.Cell));

                var lockVisual = root.transform.Find("LockVisual");
                MeshRenderer lockRenderer = lockVisual != null ? lockVisual.GetComponent<MeshRenderer>() : null;
                SetSingleMaterial(lockRenderer, Resolve(mapping, ColorMaterialSet.LockBody));

                var controller = root.GetComponent<CellController>();
                var so = new SerializedObject(controller);
                SetRef(so, "m_meshFilter", filter);
                SetRef(so, "m_renderer", renderer);
                SetRef(so, "m_lockRoot", lockVisual != null ? lockVisual.gameObject : null);
                SetRef(so, "m_lockRenderer", lockRenderer);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
                Report.AppendLine("  Cell: Hexa.fbx, visual scale " + visual.localScale.ToString("F1") + ", renderer rewired to child");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyCrate(ColorMaterialMapping mapping)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CratePrefabPath);
            try
            {
                Transform visual = root.transform.Find("Hexa");
                if (visual == null)
                {
                    Report.AppendLine("  Crate: SKIPPED, no 'Hexa' child");
                    return;
                }

                var renderer = visual.GetComponentInChildren<MeshRenderer>(true);
                SetSingleMaterial(renderer, Resolve(mapping, ColorMaterialSet.CrateActive));

                Transform lockMesh = root.transform.Find("DelayLock/LockMesh");
                MeshRenderer lockRenderer = lockMesh != null ? lockMesh.GetComponent<MeshRenderer>() : null;
                SetSingleMaterial(lockRenderer, Resolve(mapping, ColorMaterialSet.LockBody));

                var controller = root.GetComponent<CrateController>();
                var so = new SerializedObject(controller);
                SetRef(so, "m_renderer", renderer);
                SetRef(so, "m_lockMeshRenderer", lockRenderer);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, CratePrefabPath);
                Report.AppendLine("  Crate: Hexa.fbx body kept, materials switched to generated crate/lock sets");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyShard(ColorMaterialMapping mapping)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CubeModelPath);
            Mesh cube = LoadMesh(CubeModelPath, "Cube");
            if (model == null || cube == null)
            {
                Report.AppendLine("  TileShard: SKIPPED, missing " + CubeModelPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ShardPrefabPath);
            try
            {
                StripRootRenderer(root);

                Transform visual = root.transform.Find("Cube");
                if (visual == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                    instance.name = "Cube";
                    visual = instance.transform;
                }

                float normalise = LegacyShardEdge / Mathf.Max(0.00001f, cube.bounds.size.x);
                visual.localPosition = new Vector3(0f, -cube.bounds.center.y * normalise, 0f);
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one * normalise;

                var renderer = visual.GetComponentInChildren<MeshRenderer>(true);
                var filter = visual.GetComponentInChildren<MeshFilter>(true);
                SetSingleMaterial(renderer, Resolve(mapping, ColorMaterialSet.Shard));

                var controller = root.GetComponent<TileShardController>();
                var so = new SerializedObject(controller);
                SetRef(so, "m_meshFilter", filter);
                SetRef(so, "m_renderer", renderer);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ShardPrefabPath);
                Report.AppendLine("  TileShard: Cube.fbx, visual scale " + normalise.ToString("F1") + ", material from generated shard set");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ApplyBee(ColorMaterialMapping mapping)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BeeModelPath);
            if (model == null)
            {
                Report.AppendLine("  Bee: SKIPPED, missing " + BeeModelPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(BeePrefabPath);
            try
            {
                Transform rig = root.transform.Find("Rig");
                if (rig == null)
                {
                    var rigGo = new GameObject("Rig");
                    rigGo.transform.SetParent(root.transform, false);
                    rig = rigGo.transform;
                }

                for (int i = rig.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(rig.GetChild(i).gameObject);
                }

                rig.localPosition = Vector3.zero;
                rig.localRotation = Quaternion.identity;
                rig.localScale = Vector3.one;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, rig);
                instance.name = "BeeModel";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                float modelLength = Mathf.Max(0.0001f, CombinedBounds(instance).size.z);
                float rigScale = LegacyBeeLength / modelLength;
                rig.localScale = Vector3.one * rigScale;

                Material stripe = AssetDatabase.LoadAssetAtPath<Material>(BeeStripeMaterialPath);
                Material wing = AssetDatabase.LoadAssetAtPath<Material>(BeeWingMaterialPath);
                Material body = Resolve(mapping, ColorMaterialSet.Bee);

                Transform bodyTransform = FindDeep(instance.transform, "Body");
                MeshRenderer bodyRenderer = bodyTransform != null ? bodyTransform.GetComponent<MeshRenderer>() : null;
                int bodySlot = 0;
                if (bodyRenderer != null)
                {
                    Material[] slots = bodyRenderer.sharedMaterials;
                    bodySlot = slots.Length > 1 ? 1 : 0;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        slots[i] = i == bodySlot ? body : stripe;
                    }
                    bodyRenderer.sharedMaterials = slots;
                }

                ApplyMaterialToChild(instance.transform, "Antennae", stripe);
                ApplyMaterialToChild(instance.transform, "Eyes", stripe);
                ApplyMaterialToChild(instance.transform, "Wing_Left", wing);
                ApplyMaterialToChild(instance.transform, "Wing_Right", wing);

                Transform wingL = FindDeep(instance.transform, "Wing_Left");
                Transform wingR = FindDeep(instance.transform, "Wing_Right");

                var controller = root.GetComponent<BeeController>();
                var so = new SerializedObject(controller);
                SetRef(so, "m_rig", rig);
                SetRef(so, "m_bodyRenderer", bodyRenderer);
                SetRef(so, "m_wingL", wingL);
                SetRef(so, "m_wingR", wingR);
                SetRef(so, "m_wingLRenderer", wingL != null ? wingL.GetComponent<MeshRenderer>() : null);
                SetRef(so, "m_wingRRenderer", wingR != null ? wingR.GetComponent<MeshRenderer>() : null);
                var slot = so.FindProperty("m_bodyMaterialIndex");
                if (slot != null) slot.intValue = bodySlot;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BeePrefabPath);
                Report.AppendLine("  Bee: Bee.fbx, rig scale " + rigScale.ToString("F2") + ", body material slot " + bodySlot);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Material Resolve(ColorMaterialMapping mapping, ColorMaterialSet set)
        {
            Material material;
            return mapping.EditorTryGet(set, PreviewColor, out material) ? material : null;
        }

        private static void StripRootRenderer(GameObject root)
        {
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer, true);
            var filter = root.GetComponent<MeshFilter>();
            if (filter != null) Object.DestroyImmediate(filter, true);
        }

        private static void ApplyMaterialToChild(Transform root, string childName, Material material)
        {
            Transform child = FindDeep(root, childName);
            if (child == null) return;
            SetSingleMaterial(child.GetComponent<MeshRenderer>(), material);
        }

        private static void SetSingleMaterial(Renderer renderer, Material material)
        {
            if (renderer == null || material == null) return;
            Material[] slots = renderer.sharedMaterials;
            if (slots.Length <= 1)
            {
                renderer.sharedMaterial = material;
                return;
            }

            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }

        private static void SetRef(SerializedObject so, string path, Object value)
        {
            var property = so.FindProperty(path);
            if (property != null) property.objectReferenceValue = value;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        private static Bounds CombinedBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static float MaxPlanarRadius(Mesh mesh)
        {
            float best = 0f;
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                float radius = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                if (radius > best) best = radius;
            }
            return Mathf.Max(0.00001f, best);
        }

        private static Mesh LoadMesh(string assetPath, string meshName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                var mesh = assets[i] as Mesh;
                if (mesh != null && mesh.name == meshName) return mesh;
            }
            return null;
        }
    }
}

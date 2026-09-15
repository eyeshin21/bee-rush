using HoneyBeeRush.Data;
using HoneyBeeRush.Gameplay.Core;
using UnityEditor;
using UnityEngine;

namespace HoneyBeeRush.EditorTools
{
    public static class BeeCarryVisualBuilder
    {
        private const string BeePrefabPath = "Assets/_Game/Prefabs/Bee.prefab";
        private const string CarryModelPath = "Assets/_Game/Models/Hexa_Fly.fbx";
        private const string StripeMaterialPath = "Assets/_Game/Models/Materials/BeeStripe/Bee_Stripe.mat";
        private const string WingMaterialPath = "Assets/_Game/Models/Materials/BeeWings/Bee_Wing.mat";

        private const string RigName = "Rig";
        private const string BeeVisualName = "BeeModel";
        private const string CarryVisualName = "HexaFlyModel";
        private const int ColourSlot = 1;
        private const ColorType PreviewColor = ColorType.Yellow;

        [MenuItem("Honey Bee Rush/Models/Apply Bee Carry Visual")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CarryModelPath);
            if (model == null)
            {
                Debug.LogError("[BeeCarryVisualBuilder] Missing " + CarryModelPath + ". Aborting.");
                return;
            }

            var mapping = AssetDatabase.LoadAssetAtPath<ColorMaterialMapping>(ColorMaterialSetGenerator.MappingAssetPath);
            Material stripe = AssetDatabase.LoadAssetAtPath<Material>(StripeMaterialPath);
            Material wing = AssetDatabase.LoadAssetAtPath<Material>(WingMaterialPath);

            GameObject root = PrefabUtility.LoadPrefabContents(BeePrefabPath);
            if (root == null)
            {
                Debug.LogError("[BeeCarryVisualBuilder] Could not load " + BeePrefabPath + ".");
                return;
            }

            try
            {
                Transform rig = root.transform.Find(RigName);
                if (rig == null)
                {
                    Debug.LogError("[BeeCarryVisualBuilder] " + BeePrefabPath + " has no '" + RigName + "' child.");
                    return;
                }

                Transform beeVisual = rig.Find(BeeVisualName);

                Transform existing = rig.Find(CarryVisualName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, rig);
                instance.name = CarryVisualName;
                Transform carry = instance.transform;
                carry.localPosition = beeVisual != null ? beeVisual.localPosition : Vector3.zero;
                carry.localRotation = beeVisual != null ? beeVisual.localRotation : Quaternion.identity;
                carry.localScale = beeVisual != null ? beeVisual.localScale : Vector3.one;

                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++) Object.DestroyImmediate(colliders[i]);

                MeshRenderer body = FindRenderer(carry, "Body");
                MeshRenderer cargo = FindRenderer(carry, "Hexa");
                MeshRenderer antennae = FindRenderer(carry, "Antennae");
                MeshRenderer wingL = FindRenderer(carry, "Wing_Left");
                MeshRenderer wingR = FindRenderer(carry, "Wing_Right");

                if (antennae != null && stripe != null) antennae.sharedMaterial = stripe;
                if (wingL != null && wing != null) wingL.sharedMaterial = wing;
                if (wingR != null && wing != null) wingR.sharedMaterial = wing;

                if (body != null)
                {
                    Material[] mats = body.sharedMaterials;
                    if (mats.Length > 0 && stripe != null) mats[0] = stripe;
                    if (mats.Length > ColourSlot && mapping != null) mats[ColourSlot] = mapping.Bee(PreviewColor);
                    body.sharedMaterials = mats;
                }

                if (cargo != null && mapping != null) cargo.sharedMaterial = mapping.Cell(PreviewColor);

                instance.SetActive(false);

                var controller = root.GetComponent<BeeController>();
                var so = new SerializedObject(controller);
                SetRef(so, "m_beeVisual", beeVisual != null ? beeVisual.gameObject : null);
                SetRef(so, "m_carryVisual", instance);
                SetRef(so, "m_carryBodyRenderer", body);
                SetRef(so, "m_carryCargoRenderer", cargo);
                SetRef(so, "m_carryWingL", wingL != null ? wingL.transform : null);
                SetRef(so, "m_carryWingR", wingR != null ? wingR.transform : null);
                so.FindProperty("m_carryBodyMaterialIndex").intValue = ColourSlot;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BeePrefabPath);
                Debug.Log("[BeeCarryVisualBuilder] Added '" + CarryVisualName + "' to " + BeePrefabPath
                    + " (body=" + (body != null) + " cargo=" + (cargo != null) + " wings=" + (wingL != null && wingR != null) + ")");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static MeshRenderer FindRenderer(Transform root, string name)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].name == name) return renderers[i];
            }
            return null;
        }

        private static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning("[BeeCarryVisualBuilder] No serialized field '" + field + "'.");
                return;
            }
            prop.objectReferenceValue = value;
        }
    }
}

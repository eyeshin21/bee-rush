using System.Collections.Generic;
using UnityEngine;

namespace HoneyBeeRush.View
{
    public sealed class HiveView : MonoBehaviour
    {
        private struct Drop
        {
            public Transform Tr;
            public Vector3 Vel;
            public float Life;
            public float MaxLife;
            public float Scale;
        }

        [SerializeField] private GameObject m_hivePrefab;
        [SerializeField] private string m_entryPointName = "entry_point";

        private Transform _root;
        private Transform _hive;
        private Transform _model;
        private Transform _entryPoint;
        private float _modelHeight = 1f;
        private Quaternion _baseRotation = Quaternion.identity;
        private Vector3 _baseScale;
        private float _pulse;
        private float _pulseBiasX;
        private float _pulseBiasY;
        private float _danceTime = -1f;

        private readonly List<Drop> _drops = new List<Drop>(128);
        private readonly Stack<Transform> _dropPool = new Stack<Transform>(128);
        private MaterialPropertyBlock _mpb;

        public float pulseDuration = 0.24f;
        public float pulseSquash = 0.14f;
        public int dropCount = 3;
        public float dropSpeed = 3.4f;
        public float dropSpread = 1.5f;
        public float dropGravity = 12f;
        public float dropLife = 0.55f;
        public float dropSize = 0.075f;
        public Color honeyColor = new Color(1f, 0.78f, 0.15f, 1f);

        public Vector3 HivePosition
        {
            get { return _hive != null ? _hive.position : transform.position; }
        }

        public void Build(LayoutConfig layout)
        {
            _root = transform;
            _mpb = new MaterialPropertyBlock();

            var go = new GameObject("Hive");
            _hive = go.transform;
            _hive.SetParent(_root, false);
            _hive.localPosition = layout.hivePosition;

            BuildModel();

            _baseRotation = Quaternion.Euler(layout.hiveRotation);
            _hive.localRotation = _baseRotation;
            _baseScale = ResolveHiveScale(layout);
            _hive.localScale = _baseScale;
        }

        public void ApplyLayoutSizes(LayoutConfig layout)
        {
            if (_hive == null || layout == null) return;

            _hive.localPosition = layout.hivePosition;
            _baseRotation = Quaternion.Euler(layout.hiveRotation);
            _hive.localRotation = _baseRotation;
            _baseScale = ResolveHiveScale(layout);
            if (_pulse <= 0f && _danceTime < 0f) _hive.localScale = _baseScale;
        }

        private void BuildModel()
        {
            if (m_hivePrefab == null)
            {
                Debug.LogWarning("[HiveView] No hive model assigned; the hive will be invisible.");
                return;
            }

            GameObject instance = Instantiate(m_hivePrefab, _hive, false);
            instance.name = "HiveModel";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            _model = instance.transform;

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Destroy(colliders[i]);

            _entryPoint = FindDeep(_model, m_entryPointName);
            _modelHeight = MeasureHeight(instance);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name) return all[i];
            }
            return null;
        }

        private static float MeasureHeight(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 1f;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return Mathf.Max(0.0001f, b.size.y);
        }

        public Vector3 EntrancePosition(LayoutConfig layout)
        {
            if (_entryPoint != null) return _entryPoint.position;

            return _hive != null
                ? _hive.position + new Vector3(0f, -layout.HiveHeight * 0.16f, 0f)
                : layout.hivePosition;
        }

        public Vector3 EntranceForward()
        {
            return _entryPoint != null ? _entryPoint.forward : Vector3.back;
        }

        private Vector3 ResolveHiveScale(LayoutConfig layout)
        {
            if (_model == null || _modelHeight <= 0.0001f) return Vector3.one;

            float uniform = layout.HiveHeight / _modelHeight;
            return new Vector3(uniform, uniform, uniform);
        }

        public void Bounce()
        {
            _pulse = pulseDuration;
            _pulseBiasX = Random.Range(-0.35f, 0.35f);
            _pulseBiasY = Random.Range(-0.2f, 0.2f);
        }

        public void SplashHoney(Vector3 at)
        {
            if (_root == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < dropCount; i++)
            {
                Transform t = RentDrop();
                t.position = at;
                float s = dropSize * Random.Range(0.7f, 1.35f);
                t.localScale = Vector3.one * s;
                t.gameObject.SetActive(true);
                var mr = t.GetComponent<MeshRenderer>();
                _mpb.Clear();
                _mpb.SetColor(ShaderIds.Color, honeyColor);
                mr.SetPropertyBlock(_mpb);

                Vector3 dir = new Vector3(Random.Range(-dropSpread, dropSpread), Random.Range(0.35f, 1.2f), Random.Range(-0.6f, -0.05f)).normalized;
                _drops.Add(new Drop
                {
                    Tr = t,
                    Vel = dir * dropSpeed * Random.Range(0.7f, 1.3f),
                    Life = 0f,
                    MaxLife = dropLife * Random.Range(0.8f, 1.25f),
                    Scale = s
                });
            }
        }

        public void StartDance()
        {
            _danceTime = 0f;
        }

        public void StopDance()
        {
            if (_danceTime < 0f) return;
            _danceTime = -1f;
            if (_hive == null) return;
            _hive.localRotation = _baseRotation;
            if (_pulse <= 0f) _hive.localScale = _baseScale;
        }

        public void Tick(float dt)
        {
            if (_hive != null)
            {
                Vector3 s = _baseScale;
                if (_pulse > 0f)
                {
                    _pulse -= dt;
                    float k = Mathf.Clamp01(_pulse / pulseDuration);
                    float wave = Mathf.Sin(k * Mathf.PI);
                    s = new Vector3(
                        _baseScale.x * (1f + wave * pulseSquash * (1f + _pulseBiasX)),
                        _baseScale.y * (1f - wave * pulseSquash * (1f + _pulseBiasY)),
                        _baseScale.z);
                }
                if (_danceTime >= 0f)
                {
                    _danceTime += dt;
                    float w = Mathf.Sin(_danceTime * 8.5f);
                    s = new Vector3(s.x * (1f + w * 0.10f), s.y * (1f - w * 0.10f), s.z);
                    _hive.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(_danceTime * 6.2f) * 7f);
                }
                _hive.localScale = s;
            }

            for (int i = _drops.Count - 1; i >= 0; i--)
            {
                Drop d = _drops[i];
                d.Life += dt;
                if (d.Life >= d.MaxLife)
                {
                    d.Tr.gameObject.SetActive(false);
                    _dropPool.Push(d.Tr);
                    _drops.RemoveAt(i);
                    continue;
                }
                d.Vel += Vector3.down * dropGravity * dt;
                d.Tr.position += d.Vel * dt;
                float t = 1f - d.Life / d.MaxLife;
                d.Tr.localScale = Vector3.one * (d.Scale * Mathf.Clamp01(t * 1.5f));
                _drops[i] = d;
            }
        }

        private Transform RentDrop()
        {
            if (_dropPool.Count > 0) return _dropPool.Pop();
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.name = "HoneyDrop";
            go.transform.SetParent(_root, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Toon;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }
    }
}

using UnityEngine;

namespace HoneyBeeRush.View
{
    public sealed class EnvironmentView : MonoBehaviour
    {
        public static readonly Color BackdropCentre = new Color32(0x14, 0xAE, 0x76, 0xFF);
        public static readonly Color BackdropEdge = new Color32(0x08, 0x8C, 0x5C, 0xFF);
        public static readonly Color PanelColor = new Color32(0xDF, 0xD1, 0xB7, 0xFF);

        private Transform _root;
        private Transform _panel;

        public Transform Panel
        {
            get { return _panel; }
        }

        public void Build(LayoutConfig layout, Camera cam)
        {
            _root = transform;

            if (cam == null) cam = Camera.main;

            float aspect = 720f / 1280f;
            float camFov = cam != null ? cam.fieldOfView : 60f;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            float tanHalf = Mathf.Tan(camFov * 0.5f * Mathf.Deg2Rad);

            float bgHalfH = tanHalf * Mathf.Abs(camPos.z - layout.backgroundZ);
            float bgHalfW = bgHalfH * aspect;

            var backdrop = MakeQuad("Backdrop", _root, new Vector3(0f, 0f, layout.backgroundZ),
                new Vector3(bgHalfW * 2.15f, bgHalfH * 2.15f, 1f),
                MaterialLibrary.NewTextured(BuildBackdropTexture(), false, 1000));
            backdrop.GetComponent<MeshRenderer>().sharedMaterial.color = Color.white;

            float canopyHalfH = tanHalf * Mathf.Abs(camPos.z - layout.canopyZ);
            float canopyHalfW = canopyHalfH * aspect;

            var panelTex = TextureLibrary.Load("bgground");
            var panelMat = panelTex != null
                ? MaterialLibrary.NewTextured(panelTex, true, 1900)
                : MaterialLibrary.NewToon(PanelColor);
            var panelGo = MakeQuad("BoardPanel", _root,
                new Vector3(layout.boardCentre.x, layout.boardCentre.y, layout.panelZ),
                new Vector3(layout.PanelWidth, layout.PanelHeight, 1f), panelMat);
            _panel = panelGo.transform;

            var leaves = TextureLibrary.Load("leaves");
            if (leaves != null)
            {
                float leafW = canopyHalfW * 2.05f;
                float leafAspect = leaves.height / (float)leaves.width;
                float leafH = leafW * leafAspect;
                float topEdge = camPos.y + canopyHalfH;
                MakeQuad("Canopy", _root,
                    new Vector3(0f, topEdge - leafH * 0.34f + layout.canopyYOffset, layout.canopyZ),
                    new Vector3(leafW, leafH, 1f),
                    MaterialLibrary.NewTextured(leaves, true, 3400));
            }

            var flowers = TextureLibrary.Load("flowers");
            if (flowers != null)
            {
                var fm = MaterialLibrary.NewTextured(flowers, true, 2400);
                MakeQuad("Flowers1", _root, new Vector3(-3.35f, -3.55f, layout.groundDecorZ), new Vector3(1.15f, 1.15f, 1f), fm);
                MakeQuad("Flowers2", _root, new Vector3(3.42f, -2.55f, layout.groundDecorZ), new Vector3(0.85f, 0.85f, 1f), fm);
                MakeQuad("Flowers3", _root, new Vector3(3.15f, -6.35f, layout.groundDecorZ), new Vector3(0.95f, 0.95f, 1f), fm);
            }

            var grass = TextureLibrary.Load("grass");
            if (grass != null)
            {
                var gm = MaterialLibrary.NewTextured(grass, true, 2400);
                MakeQuad("Grass1", _root, new Vector3(-3.55f, -4.35f, layout.groundDecorZ), new Vector3(0.95f, 0.95f, 1f), gm);
                MakeQuad("Grass2", _root, new Vector3(3.62f, -3.35f, layout.groundDecorZ), new Vector3(0.80f, 0.80f, 1f), gm);
                MakeQuad("Grass3", _root, new Vector3(-3.20f, -6.65f, layout.groundDecorZ), new Vector3(0.85f, 0.85f, 1f), gm);
            }
        }

        private static GameObject MakeQuad(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshLibrary.Quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private static Texture2D BuildBackdropTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HBR_Backdrop", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float ny = (y / (float)(size - 1)) * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(nx * nx * 0.85f + ny * ny * 0.55f) / 1.25f);
                    Color c = Color.Lerp(BackdropCentre, BackdropEdge, d * d);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}

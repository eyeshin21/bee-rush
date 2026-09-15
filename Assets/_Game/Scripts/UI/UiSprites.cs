using UnityEngine;

namespace HoneyBeeRush.UI
{
    public static class UiSprites
    {
        private static Sprite _roundedCard;
        private static Sprite _pill;
        private static Sprite _disc;

        public static Sprite RoundedCard
        {
            get
            {
                if (_roundedCard == null) _roundedCard = BuildRounded(96, 96, 26, 36f);
                return _roundedCard;
            }
        }

        public static Sprite Pill
        {
            get
            {
                if (_pill == null) _pill = BuildRounded(64, 64, 31, 30f);
                return _pill;
            }
        }

        public static Sprite Disc
        {
            get
            {
                if (_disc == null) _disc = BuildDisc(64);
                return _disc;
            }
        }

        private static Sprite BuildRounded(int w, int h, int radius, float border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "HBR_Rounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, w, h, radius);
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        private static Sprite BuildDisc(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HBR_Disc",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - (r - 1f);
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static float RoundedDistance(float x, float y, int w, int h, int radius)
        {
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            float px = Mathf.Abs(x - hw) - (hw - radius);
            float py = Mathf.Abs(y - hh) - (hh - radius);
            float ox = Mathf.Max(px, 0f);
            float oy = Mathf.Max(py, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
        }
    }
}

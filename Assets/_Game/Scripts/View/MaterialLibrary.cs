using UnityEngine;

namespace HoneyBeeRush.View
{
    public static class ShaderIds
    {
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int Emission = Shader.PropertyToID("_Emission");
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
    }

    public static class FontLibrary
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }
    }

    public static class TextureLibrary
    {
        public static Texture2D Load(string fileName)
        {
            return Resources.Load<Texture2D>("Textures/" + fileName);
        }
    }

    public static class MaterialLibrary
    {
        private static Material _toon;
        private static Material _toonFade;

        public static Material Toon
        {
            get
            {
                if (_toon == null)
                {
                    var sh = Shader.Find("HoneyBeeRush/ToonLit");
                    if (sh == null) sh = Shader.Find("Unlit/Color");
                    _toon = new Material(sh) { name = "HBR_Toon" };
                    _toon.enableInstancing = true;
                    _toon.SetFloat("_Ambient", 0.38f);
                    _toon.SetColor("_ShadeTint", new Color(0.55f, 0.52f, 0.62f, 1f));
                    _toon.SetFloat("_RimStrength", 0.16f);
                    _toon.SetFloat("_RimPower", 4.2f);
                }
                return _toon;
            }
        }

        public static Material ToonFade
        {
            get
            {
                if (_toonFade == null)
                {
                    var sh = Shader.Find("HoneyBeeRush/ToonLitFade");
                    if (sh == null) sh = Shader.Find("Unlit/Transparent");
                    _toonFade = new Material(sh) { name = "HBR_ToonFade" };
                    _toonFade.enableInstancing = true;
                }
                return _toonFade;
            }
        }

        public static Material NewToon(Color c)
        {
            var m = new Material(Toon) { name = "HBR_ToonInstance" };
            m.SetColor(ShaderIds.Color, c);
            m.enableInstancing = true;
            return m;
        }

        public static Material NewToonFade(Color c)
        {
            var m = new Material(ToonFade) { name = "HBR_ToonFadeInstance" };
            m.SetColor(ShaderIds.Color, c);
            m.enableInstancing = true;
            return m;
        }

        public static Material NewTextured(Texture tex, bool transparent, int renderQueue)
        {
            var sh = Shader.Find(transparent ? "Unlit/Transparent" : "Unlit/Texture");
            var m = new Material(sh) { name = "HBR_Tex" };
            m.mainTexture = tex;
            if (renderQueue > 0) m.renderQueue = renderQueue;
            return m;
        }

        public static Material NewTintedTexture(Texture tex, Color tint, int renderQueue)
        {
            var sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = "HBR_TexTint" };
            m.mainTexture = tex;
            m.SetColor(ShaderIds.Color, tint);
            if (renderQueue > 0) m.renderQueue = renderQueue;
            return m;
        }
    }
}

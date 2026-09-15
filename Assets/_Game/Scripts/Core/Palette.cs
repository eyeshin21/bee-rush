using UnityEngine;

namespace HoneyBeeRush.Core
{
    public static class Palette
    {
        public static Color AdjustSaturation(Color c, float saturation)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s = Mathf.Clamp01(s * saturation);
            Color result = Color.HSVToRGB(h, s, v);
            result.a = c.a;
            return result;
        }

        public static Color Lighten(Color c, float t)
        {
            float k = Mathf.Clamp01(t);
            return new Color(
                Mathf.Lerp(c.r, 1f, k),
                Mathf.Lerp(c.g, 1f, k),
                Mathf.Lerp(c.b, 1f, k),
                c.a);
        }

        public static Color Darken(Color c, float t)
        {
            float k = Mathf.Clamp01(t);
            return new Color(
                Mathf.Lerp(c.r, 0f, k),
                Mathf.Lerp(c.g, 0f, k),
                Mathf.Lerp(c.b, 0f, k),
                c.a);
        }

    }
}

using UnityEngine;

namespace HoneyBeeRush.Core
{
    public static class HexLayout
    {
        public const float Sqrt3 = 1.7320508f;

        public static Vector2 ToLocal(HexCoord c, float size)
        {
            return ToLocal(c.Q, c.R, size);
        }

        public static Vector2 ToLocal(int q, int r, float size)
        {
            float x = size * Sqrt3 * (q + r * 0.5f);
            float y = size * 1.5f * r;
            return new Vector2(x, y);
        }

        public static Vector3 ToLocal3(HexCoord3 c, float size, float layerPitch)
        {
            Vector2 planar = ToLocal(c.Q, c.R, size);
            return new Vector3(planar.x, planar.y, c.Layer * layerPitch);
        }

        public static Vector3 FaceNormal(int dir)
        {
            if (dir == HexCoord3.FaceLayerFront) return Vector3.back;
            if (dir == HexCoord3.FaceLayerBack) return Vector3.forward;

            int i = dir % HexCoord3.LateralDirectionCount;
            if (i < 0) i += HexCoord3.LateralDirectionCount;

            HexCoord3 d = HexCoord3.Directions[i];
            Vector2 planar = ToLocal(d.Q, d.R, 1f);
            return new Vector3(planar.x, planar.y, 0f).normalized;
        }

        public static HexCoord FromLocalRound(Vector2 p, float size)
        {
            if (size <= 0f)
            {
                return new HexCoord(0, 0);
            }

            float down = p.y;
            float qf = ((Sqrt3 / 3f) * p.x - (1f / 3f) * down) / size;
            float rf = ((2f / 3f) * down) / size;
            return CubeRound(qf, rf);
        }

        public static HexCoord3 FromLocalRound(Vector3 p, float size, float layerPitch)
        {
            HexCoord planar = FromLocalRound(new Vector2(p.x, p.y), size);
            int layer = layerPitch > 0f ? Mathf.RoundToInt(p.z / layerPitch) : 0;
            return new HexCoord3(planar, layer);
        }

        public static float Width(float size)
        {
            return Sqrt3 * size;
        }

        public static float Height(float size)
        {
            return 2f * size;
        }

        private static HexCoord CubeRound(float qf, float rf)
        {
            float x = qf;
            float z = rf;
            float y = -x - z;

            int rx = Mathf.RoundToInt(x);
            int ry = Mathf.RoundToInt(y);
            int rz = Mathf.RoundToInt(z);

            float dx = Mathf.Abs(rx - x);
            float dy = Mathf.Abs(ry - y);
            float dz = Mathf.Abs(rz - z);

            if (dx > dy && dx > dz)
            {
                rx = -ry - rz;
            }
            else if (dy > dz)
            {
                ry = -rx - rz;
            }
            else
            {
                rz = -rx - ry;
            }

            return new HexCoord(rx, rz);
        }
    }
}

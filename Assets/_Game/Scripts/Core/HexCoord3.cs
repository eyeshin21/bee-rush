using System;

namespace HoneyBeeRush.Core
{
    public readonly struct HexCoord3 : IEquatable<HexCoord3>
    {
        public const int LateralDirectionCount = 6;
        public const int DirectionCount = 8;
        public const int FaceLayerFront = 6;
        public const int FaceLayerBack = 7;
        public const byte AllFacesMask = 0xFF;

        public static readonly HexCoord3[] Directions =
        {
            new HexCoord3(1, 0, 0),
            new HexCoord3(1, -1, 0),
            new HexCoord3(0, -1, 0),
            new HexCoord3(-1, 0, 0),
            new HexCoord3(-1, 1, 0),
            new HexCoord3(0, 1, 0),
            new HexCoord3(0, 0, -1),
            new HexCoord3(0, 0, 1)
        };

        public int Q { get; }
        public int R { get; }
        public int Layer { get; }

        public HexCoord3(int q, int r, int layer)
        {
            Q = q;
            R = r;
            Layer = layer;
        }

        public HexCoord3(HexCoord planar, int layer)
        {
            Q = planar.Q;
            R = planar.R;
            Layer = layer;
        }

        public int S => -Q - R;

        public HexCoord Planar => new HexCoord(Q, R);

        public HexCoord3 Neighbor(int dir)
        {
            int i = dir % DirectionCount;
            if (i < 0)
            {
                i += DirectionCount;
            }

            HexCoord3 d = Directions[i];
            return new HexCoord3(Q + d.Q, R + d.R, Layer + d.Layer);
        }

        public static int OppositeDirection(int dir)
        {
            if (dir == FaceLayerFront) return FaceLayerBack;
            if (dir == FaceLayerBack) return FaceLayerFront;
            return (dir + 3) % LateralDirectionCount;
        }

        public static HexCoord3 operator +(HexCoord3 a, HexCoord3 b)
        {
            return new HexCoord3(a.Q + b.Q, a.R + b.R, a.Layer + b.Layer);
        }

        public static HexCoord3 operator -(HexCoord3 a, HexCoord3 b)
        {
            return new HexCoord3(a.Q - b.Q, a.R - b.R, a.Layer - b.Layer);
        }

        public static bool operator ==(HexCoord3 a, HexCoord3 b)
        {
            return a.Q == b.Q && a.R == b.R && a.Layer == b.Layer;
        }

        public static bool operator !=(HexCoord3 a, HexCoord3 b)
        {
            return a.Q != b.Q || a.R != b.R || a.Layer != b.Layer;
        }

        public static int Distance(HexCoord3 a, HexCoord3 b)
        {
            int dl = a.Layer - b.Layer;
            return HexCoord.Distance(a.Planar, b.Planar) + (dl < 0 ? -dl : dl);
        }

        public bool Equals(HexCoord3 other)
        {
            return Q == other.Q && R == other.R && Layer == other.Layer;
        }

        public override bool Equals(object o)
        {
            return o is HexCoord3 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 73856093) ^ (R * 19349663) ^ (Layer * 83492791);
            }
        }

        public override string ToString()
        {
            return "(" + Q + ", " + R + ", " + Layer + ")";
        }
    }
}

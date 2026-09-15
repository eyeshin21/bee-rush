using System;

namespace HoneyBeeRush.Core
{
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0),
            new HexCoord(1, -1),
            new HexCoord(0, -1),
            new HexCoord(-1, 0),
            new HexCoord(-1, 1),
            new HexCoord(0, 1)
        };

        public int Q { get; }
        public int R { get; }

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S => -Q - R;

        public HexCoord Neighbor(int dir)
        {
            int i = dir % 6;
            if (i < 0)
            {
                i += 6;
            }

            return new HexCoord(Q + Directions[i].Q, R + Directions[i].R);
        }

        public static HexCoord operator +(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.Q + b.Q, a.R + b.R);
        }

        public static HexCoord operator -(HexCoord a, HexCoord b)
        {
            return new HexCoord(a.Q - b.Q, a.R - b.R);
        }

        public static bool operator ==(HexCoord a, HexCoord b)
        {
            return a.Q == b.Q && a.R == b.R;
        }

        public static bool operator !=(HexCoord a, HexCoord b)
        {
            return a.Q != b.Q || a.R != b.R;
        }

        public static int Distance(HexCoord a, HexCoord b)
        {
            int dq = a.Q - b.Q;
            int dr = a.R - b.R;
            int ds = a.S - b.S;
            int absQ = dq < 0 ? -dq : dq;
            int absR = dr < 0 ? -dr : dr;
            int absS = ds < 0 ? -ds : ds;
            int max = absQ > absR ? absQ : absR;
            if (absS > max)
            {
                max = absS;
            }

            return max;
        }

        public bool Equals(HexCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object o)
        {
            return o is HexCoord other && Q == other.Q && R == other.R;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 73856093) ^ (R * 19349663);
            }
        }

        public override string ToString()
        {
            return "(" + Q + ", " + R + ")";
        }
    }
}

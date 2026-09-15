using System.Collections.Generic;
using UnityEngine;

namespace HoneyBeeRush.View
{
    public static class HexPrism
    {
        public static Mesh Create(float circumRadius, float depth, float bevel)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();

            var outer = new Vector3[6];
            var inner = new Vector3[6];
            float innerRadius = Mathf.Max(0.01f, circumRadius - bevel);
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Deg2Rad * (60f * i - 90f);
                float ca = Mathf.Cos(a);
                float sa = Mathf.Sin(a);
                outer[i] = new Vector3(circumRadius * ca, circumRadius * sa, 0f);
                inner[i] = new Vector3(innerRadius * ca, innerRadius * sa, 0f);
            }

            bool hasBevel = bevel > 0.0001f;

            int capCentre = verts.Count;
            verts.Add(Vector3.zero);
            norms.Add(-Vector3.forward);
            int capStart = verts.Count;
            for (int i = 0; i < 6; i++)
            {
                verts.Add(inner[i]);
                norms.Add(-Vector3.forward);
            }
            for (int i = 0; i < 6; i++)
            {
                tris.Add(capCentre);
                tris.Add(capStart + (i + 1) % 6);
                tris.Add(capStart + i);
            }

            if (hasBevel)
            {
                int bevStart = verts.Count;
                for (int i = 0; i < 6; i++)
                {
                    verts.Add(inner[i]);
                    norms.Add(new Vector3(inner[i].x, inner[i].y, -circumRadius * 0.9f).normalized);
                }
                for (int i = 0; i < 6; i++)
                {
                    verts.Add(new Vector3(outer[i].x, outer[i].y, bevel));
                    norms.Add(new Vector3(outer[i].x, outer[i].y, -circumRadius * 0.35f).normalized);
                }
                for (int i = 0; i < 6; i++)
                {
                    int n = (i + 1) % 6;
                    tris.Add(bevStart + i);
                    tris.Add(bevStart + 6 + n);
                    tris.Add(bevStart + 6 + i);
                    tris.Add(bevStart + i);
                    tris.Add(bevStart + n);
                    tris.Add(bevStart + 6 + n);
                }
            }

            float sideTop = hasBevel ? bevel : 0f;
            int sideStart = verts.Count;
            for (int i = 0; i < 6; i++)
            {
                Vector3 a = outer[i];
                Vector3 b = outer[(i + 1) % 6];
                Vector3 nrm = Vector3.Cross(b - a, Vector3.forward).normalized;
                verts.Add(new Vector3(a.x, a.y, sideTop));
                verts.Add(new Vector3(b.x, b.y, sideTop));
                verts.Add(new Vector3(a.x, a.y, depth));
                verts.Add(new Vector3(b.x, b.y, depth));
                norms.Add(nrm);
                norms.Add(nrm);
                norms.Add(nrm);
                norms.Add(nrm);
                int o = sideStart + i * 4;
                tris.Add(o);
                tris.Add(o + 1);
                tris.Add(o + 2);
                tris.Add(o + 1);
                tris.Add(o + 3);
                tris.Add(o + 2);
            }

            int backCentre = verts.Count;
            verts.Add(new Vector3(0f, 0f, depth));
            norms.Add(Vector3.forward);
            int backStart = verts.Count;
            for (int i = 0; i < 6; i++)
            {
                verts.Add(new Vector3(outer[i].x, outer[i].y, depth));
                norms.Add(Vector3.forward);
            }
            for (int i = 0; i < 6; i++)
            {
                tris.Add(backCentre);
                tris.Add(backStart + i);
                tris.Add(backStart + (i + 1) % 6);
            }

            var m = new Mesh { name = "HBR_HexPrism" };
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}

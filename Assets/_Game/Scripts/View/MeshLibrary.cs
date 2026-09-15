using UnityEngine;

namespace HoneyBeeRush.View
{
    public static class MeshLibrary
    {
        private static Mesh _hex;
        private static Mesh _beeBody;
        private static Mesh _wingLeft;
        private static Mesh _wingRight;
        private static Mesh _quad;
        private static Mesh _cube;
        private static Mesh _roundedHex;

        public static Mesh Hex
        {
            get
            {
                if (_hex == null) _hex = Resources.Load<Mesh>("Meshes/Hex Base");
                if (_hex == null) _hex = HexPrism.Create(1f, 4f, 0f);
                return _hex;
            }
        }

        public static Mesh RoundedHex
        {
            get
            {
                if (_roundedHex == null) _roundedHex = Resources.Load<Mesh>("Meshes/RoundedHex");
                if (_roundedHex == null) _roundedHex = HexPrism.Create(1f, 1f, 0.14f);
                return _roundedHex;
            }
        }

        public static Mesh BeeBody
        {
            get
            {
                if (_beeBody == null) _beeBody = Resources.Load<Mesh>("Meshes/Plane_0");
                return _beeBody;
            }
        }

        public static Mesh WingLeft
        {
            get
            {
                if (_wingLeft == null) _wingLeft = Resources.Load<Mesh>("Meshes/Wing.L");
                return _wingLeft;
            }
        }

        public static Mesh WingRight
        {
            get
            {
                if (_wingRight == null) _wingRight = Resources.Load<Mesh>("Meshes/Wing.R");
                return _wingRight;
            }
        }

        public static Mesh Quad
        {
            get
            {
                if (_quad == null) _quad = BuildQuad();
                return _quad;
            }
        }

        public static Mesh Cube
        {
            get
            {
                if (_cube == null) _cube = Resources.Load<Mesh>("Meshes/Cube");
                if (_cube == null)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    _cube = go.GetComponent<MeshFilter>().sharedMesh;
                    go.SetActive(false);
                    Object.DestroyImmediate(go);
                }
                return _cube;
            }
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "HBR_Quad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            m.normals = new[] { -Vector3.forward, -Vector3.forward, -Vector3.forward, -Vector3.forward };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            return m;
        }
    }
}

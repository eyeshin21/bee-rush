using UnityEngine;
using UnityEngine.UI;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Anvil
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public partial class UIMeshBehaviour : MaskableGraphic, ILayoutElement
    {
        protected static readonly float PixelsPerUnit = 100;

        protected enum TriangleType
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
        }

        [SerializeField, CheckValueChanged("OnSpriteChanged")]
        protected Sprite _sprite;
        
        [SerializeField] private float _pixelsPerUnitMultiplier = 1f;

        public float pixelsPerUnitMultiplier
        {
            get => _pixelsPerUnitMultiplier;
            set
            {
                if (!Mathf.Approximately(_pixelsPerUnitMultiplier, value))
                {
                    _pixelsPerUnitMultiplier = Mathf.Max(0.01f, value);
                    SetAllDirty();
                }
            }
        }

        public virtual Sprite sprite
        {
            get => _sprite;
            set
            {
                if (Utilities.TrySet(ref _sprite, value))
                {
                    GeneratedUVs();
                    SetAllDirty();
                }
            }
        }

        [NonSerialized]
        protected Sprite _overrideSprite;
        public virtual Sprite overrideSprite
        {
            get => _overrideSprite;
            set
            {
                if (Utilities.TrySet(ref _overrideSprite, value))
                {
                    GeneratedUVs();
                    SetAllDirty();
                }
            }
        }

        public float Alpha
        {
            get => color.a;
            set
            {
                var color = this.color;
                color.a = value;
                this.color = color;
            }
        }

        //[SerializeField]
        //protected bool _useNativeSize;
        //public bool UseNativeSize
        //{
        //    get => _useNativeSize;
        //    set
        //    {
        //        if (_useNativeSize != value)
        //        {
        //            _useNativeSize = value;
        //            SetAllDirty();
        //        }
        //    }
        //}

        protected virtual Sprite activeSprite => _overrideSprite != null ? _overrideSprite : _sprite;

        static Material _defaultETC1Material = null;
        /// <summary>
        /// Default material used to draw everything if no explicit material was specified.
        /// </summary>
        public static Material defaultETC1Material
        {
            get
            {
                if (_defaultETC1Material == null)
                {
                    _defaultETC1Material = Canvas.GetETC1SupportedCanvasMaterial();
                }
                return _defaultETC1Material;
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (activeSprite == null)
                {
                    if (material != null && material.mainTexture != null)
                    {
                        return material.mainTexture;
                    }
                    return s_WhiteTexture;
                }

                return activeSprite.texture;
            }
        }

        public override Material material
        {
            get
            {
                if (m_Material != null)
                    return m_Material;

                if (activeSprite != null && activeSprite.associatedAlphaSplitTexture != null)
                    return defaultETC1Material;

                return defaultMaterial;
            }
        }

        // public float pixelsPerUnit
        // {
        //     get
        //     {
        //         float spritePixelsPerUnit = 100;
        //         if (activeSprite)
        //             spritePixelsPerUnit = activeSprite.pixelsPerUnit;
        //
        //         float referencePixelsPerUnit = 100;
        //         if (canvas)
        //             referencePixelsPerUnit = canvas.referencePixelsPerUnit;
        //
        //         return spritePixelsPerUnit / referencePixelsPerUnit;
        //     }
        // }
        public float pixelsPerUnit
        {
            get
            {
                float spritePixelsPerUnit = 100;
                if (activeSprite)
                    spritePixelsPerUnit = activeSprite.pixelsPerUnit;

                float referencePixelsPerUnit = 100;
                if (canvas)
                    referencePixelsPerUnit = canvas.referencePixelsPerUnit;

                // Apply the multiplier here
                return (spritePixelsPerUnit / referencePixelsPerUnit) * _pixelsPerUnitMultiplier;
            }
        }

        protected UIMeshBehaviour()
        {
            useLegacyMeshGeneration = false;
        }

        protected virtual void GeneratedUVs() { }

        protected virtual void ResolutionToNativeSize(float distance) { }

#if UNITY_EDITOR
        protected virtual void OnSpriteChanged()
        {

        }

        public virtual void OnInspectorGUI()
        {
            _showMesh = EditorGUILayout.Toggle("Show Mesh", _showMesh);
        }

#endif

        #region ILayoutElement
        public virtual float minWidth => 0;
        public virtual float preferredWidth => activeSprite ? activeSprite.rect.size.x / pixelsPerUnit : 0;
        public virtual float flexibleWidth => -1;
        public virtual float minHeight => 0;
        public virtual float preferredHeight => activeSprite ? activeSprite.rect.size.y / pixelsPerUnit : 0;
        public virtual float flexibleHeight => -1;
        public virtual int layoutPriority => 0;

        public virtual void CalculateLayoutInputHorizontal() { }

        public virtual void CalculateLayoutInputVertical() { }
        #endregion

        #region Helper
        protected static UIVertex[] _quadVertices = new UIVertex[4];

        protected virtual void SetNativeSize(Sprite sprite)
        {
            if (sprite != null)
            {
                rectTransform.sizeDelta = sprite.rect.size;
            }
        }

        protected void GetAABB(out float left, out float top, out float right, out float bottom)
        {
            float sizeX = rectTransform.rect.width;
            float sizeY = rectTransform.rect.height;
            float offsetX = -rectTransform.pivot.x * sizeX;
            float offsetY = -rectTransform.pivot.y * sizeY;
            left = offsetX;
            right = left + sizeX;
            bottom = offsetY;
            top = bottom + sizeY;
        }

        protected void GetBorders(Sprite sprite, out float left, out float top, out float right, out float bottom)
        {
            if (sprite != null)
            {
                var border = sprite.border;
                left = border.x;
                bottom = border.y;
                right = border.z;
                top = border.w;
            }
            else
            {
                left = top = right = bottom = 0;
            }
        }

        protected void GetUVs(Sprite sprite, out float uvLeft, out float uvTop, out float uvRight, out float uvBottom)
        {
            if (sprite != null)
            {
                var outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                uvLeft = outer.x;
                uvBottom = outer.y;
                uvRight = outer.z;
                uvTop = outer.w;
            }
            else
            {
                uvLeft = 0;
                uvRight = 1;
                uvBottom = 0;
                uvTop = 1;
            }
        }

        protected void GetUVs(Sprite sprite, out float uvLeft, out float uvTop, out float uvRight, out float uvBottom,
                                out float uvX, out float uvY, out float uvZ, out float uvW)
        {
            if (sprite != null)
            {
                var outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                uvLeft = outer.x;
                uvBottom = outer.y;
                uvRight = outer.z;
                uvTop = outer.w;

                var inner = UnityEngine.Sprites.DataUtility.GetInnerUV(sprite);
                uvX = inner.x;
                uvY = inner.y;
                uvZ = inner.z;
                uvW = inner.w;
            }
            else
            {
                uvX = uvLeft = 0;
                uvZ = uvRight = 1;
                uvY = uvBottom = 0;
                uvW = uvTop = 1;
            }
        }

        protected void AddQuad(VertexHelper vh, float left, float top, float right, float bottom,
                               float uvLeft, float uvTop, float uvRight, float uvBottom)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;

            /*
             *      3		2
             * 		+-------+
             * 		| 	    |
             * 		|   	|
             * 		|	  	|
             * 		+-------+
             *      0       1
             */
            //vertex.position = new Vector3(left, bottom);
            //vertex.uv0 = new Vector4(uvLeft, uvBottom);
            //_quadVertices[0] = vertex;

            //vertex.position = new Vector3(right, bottom);
            //vertex.uv0 = new Vector4(uvRight, uvBottom);
            //_quadVertices[1] = vertex;

            //vertex.position = new Vector3(right, top);
            //vertex.uv0 = new Vector4(uvRight, uvTop);
            //_quadVertices[2] = vertex;

            //vertex.position = new Vector3(left, top);
            //vertex.uv0 = new Vector4(uvLeft, uvTop);
            //_quadVertices[3] = vertex;

            //vh.AddUIVertexQuad(_quadVertices);

            /*
             *      1		3
             * 		+-------+
             * 		| 	  / |
             * 		|   /  	|
             * 		| /	  	|
             * 		+-------+
             *      0       2
             */
            int index = vh.currentVertCount;
            vertex.position = new Vector3(left, bottom);
            vertex.uv0 = new Vector4(uvLeft, uvBottom);
            vh.AddVert(vertex);

            vertex.position = new Vector3(left, top);
            vertex.uv0 = new Vector4(uvLeft, uvTop);
            vh.AddVert(vertex);

            vertex.position = new Vector3(right, bottom);
            vertex.uv0 = new Vector4(uvRight, uvBottom);
            vh.AddVert(vertex);

            vertex.position = new Vector3(right, top);
            vertex.uv0 = new Vector4(uvRight, uvTop);
            vh.AddVert(vertex);

            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }

        protected void AddQuad(VertexHelper vh, float left, float top, float right, float bottom,
                               float uvLeft, float uvTop, float uvRight, float uvBottom, UVTransformType transformType)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            int index = vh.currentVertCount;

            // (1, 3, 0, 2)
            /*
             *      1		3
             * 		+-------o
             * 		|` 	    |
             * 		|  `   	|
             * 		| 	 ` 	|
             * 		o------`o
             *      0       2
             */
            if (transformType == UVTransformType.RotateLeft)
            {
                vertex.position = new Vector3(left, bottom);
                vertex.uv0 = new Vector4(uvLeft, uvTop);
                vh.AddVert(vertex);

                vertex.position = new Vector3(left, top);
                vertex.uv0 = new Vector4(uvRight, uvTop);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, bottom);
                vertex.uv0 = new Vector4(uvLeft, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, top);
                vertex.uv0 = new Vector4(uvRight, uvBottom);
                vh.AddVert(vertex);

                vh.AddTriangle(index + 1, index + 3, index + 2);
                vh.AddTriangle(index + 1, index + 2, index);

                return;
            }

            // (2, 0, 3, 1)
            /*
             *      1		3
             * 		o-------o
             * 		|` 	    |
             * 		|  `   	|
             * 		| 	 ` 	|
             * 		o------`+
             *      0       2
             */
            if (transformType == UVTransformType.RotateRight)
            {
                vertex.position = new Vector3(left, bottom);
                vertex.uv0 = new Vector4(uvRight, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(left, top);
                vertex.uv0 = new Vector4(uvLeft, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, bottom);
                vertex.uv0 = new Vector4(uvRight, uvTop);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, top);
                vertex.uv0 = new Vector4(uvLeft, uvTop);
                vh.AddVert(vertex);

                vh.AddTriangle(index + 2, index, index + 1);
                vh.AddTriangle(index + 2, index + 1, index + 3);

                return;
            }

            if (transformType == UVTransformType.FlipHorizontal)
            {
                var tmp = uvLeft;
                uvLeft = uvRight;
                uvRight = tmp;
            }
            else if (transformType == UVTransformType.FlipVertical)
            {
                var tmp = uvTop;
                uvTop = uvBottom;
                uvBottom = tmp;
            }
            else if (transformType == UVTransformType.FlipAll)
            {
                var tmp = uvLeft;
                uvLeft = uvRight;
                uvRight = tmp;

                tmp = uvTop;
                uvTop = uvBottom;
                uvBottom = tmp;
            }

            /*
             *      1		3
             * 		+-------+
             * 		| 	  / |
             * 		|   /  	|
             * 		| /	  	|
             * 		+-------+
             *      0       2
             */
            vertex.position = new Vector3(left, bottom);
            vertex.uv0 = new Vector4(uvLeft, uvBottom);
            vh.AddVert(vertex);

            vertex.position = new Vector3(left, top);
            vertex.uv0 = new Vector4(uvLeft, uvTop);
            vh.AddVert(vertex);

            vertex.position = new Vector3(right, bottom);
            vertex.uv0 = new Vector4(uvRight, uvBottom);
            vh.AddVert(vertex);

            vertex.position = new Vector3(right, top);
            vertex.uv0 = new Vector4(uvRight, uvTop);
            vh.AddVert(vertex);

            vh.AddTriangle(index, index + 1, index + 3);
            vh.AddTriangle(index, index + 3, index + 2);
        }

        protected void AddQuadFromCenter(VertexHelper vh, float x, float y, float width, float height,
                                            float uvLeft, float uvTop, float uvRight, float uvBottom)
        {
            float left = x - width * 0.5f;
            float right = left + width;
            float bottom = y - height * 0.5f;
            float top = bottom + height;
            AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
        }

        protected void AddQuadFromCenter(VertexHelper vh, float x, float y, float width, float height,
                                            float uvLeft, float uvTop, float uvRight, float uvBottom, UVTransformType transformType)
        {
            if (transformType == UVTransformType.RotateLeft || transformType == UVTransformType.RotateRight)
            {
                float tmp = width;
                width = height;
                height = tmp;
            }
            float left = x - width * 0.5f;
            float right = left + width;
            float bottom = y - height * 0.5f;
            float top = bottom + height;
            AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, transformType);
        }

        protected void AddTriangle(VertexHelper vh, float left, float top, float right, float bottom,
                               float uvLeft, float uvTop, float uvRight, float uvBottom, TriangleType triangleType)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;

            /*
             *      1		3
             * 		+-------+
             * 		| 	  / |
             * 		|   /  	|
             * 		| /	  	|
             * 		+-------+
             *      0       2
             */
            int index = vh.currentVertCount;
            if (triangleType == TriangleType.TopLeft)
            {
                vertex.position = new Vector3(left, bottom);
                vertex.uv0 = new Vector4(uvLeft, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(left, top);
                vertex.uv0 = new Vector4(uvLeft, uvTop);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, top);
                vertex.uv0 = new Vector4(uvRight, uvTop);
                vh.AddVert(vertex);
            }
            else if (triangleType == TriangleType.TopRight)
            {
                vertex.position = new Vector3(right, bottom);
                vertex.uv0 = new Vector4(uvRight, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(left, top);
                vertex.uv0 = new Vector4(uvLeft, uvTop);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, top);
                vertex.uv0 = new Vector4(uvRight, uvTop);
                vh.AddVert(vertex);
            }
            else if (triangleType == TriangleType.BottomLeft)
            {
                vertex.position = new Vector3(right, bottom);
                vertex.uv0 = new Vector4(uvRight, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(left, bottom);
                vertex.uv0 = new Vector4(uvLeft, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(left, top);
                vertex.uv0 = new Vector4(uvLeft, uvTop);
                vh.AddVert(vertex);
            }
            else if (triangleType == TriangleType.BottomRight)
            {
                vertex.position = new Vector3(left, bottom);
                vertex.uv0 = new Vector4(uvLeft, uvBottom);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, top);
                vertex.uv0 = new Vector4(uvRight, uvTop);
                vh.AddVert(vertex);

                vertex.position = new Vector3(right, bottom);
                vertex.uv0 = new Vector4(uvRight, uvBottom);
                vh.AddVert(vertex);
            }

            vh.AddTriangle(index, index + 1, index + 2);
        }

        protected UIVertex[] SetVbo(Vector2[] vertices, Vector2[] uvs)
        {
            UIVertex[] vbo = new UIVertex[4];
            for (int i = 0; i < vertices.Length; i++)
            {
                var vert = UIVertex.simpleVert;
                vert.color = color;
                vert.position = vertices[i];
                vert.uv0 = uvs[i];
                vbo[i] = vert;
            }
            return vbo;
        }
        #endregion

#if UNITY_EDITOR
        Mesh _mesh;
        bool _showMesh;

        protected void FillMesh(VertexHelper vh)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh();
            }
            vh.FillMesh(_mesh);
        }

        protected virtual void OnDrawGizmos()
        {
            if (_showMesh)
            {
                var mesh = _mesh;
                if (mesh != null && mesh.vertexCount > 0)
                {
                    var color = Gizmos.color;
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireMesh(mesh, transform.position, transform.localRotation, transform.localScale * 0.01f);
                    Gizmos.color = color;

                }
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(UIMeshBehaviour), true), CanEditMultipleObjects, DisallowMultipleComponent]
    public class UIMeshBehaviourEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var uiMeshBehaviour = target as UIMeshBehaviour;
            uiMeshBehaviour.OnInspectorGUI();
        }
    }
#endif
}
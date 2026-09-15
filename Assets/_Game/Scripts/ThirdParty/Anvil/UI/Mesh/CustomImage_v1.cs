using UnityEngine;
using UnityEngine.UI;

namespace Anvil
{
    public class CustomImage_v1 : UIMeshBehaviour
    {
        enum ImageType
        {
            Simple,

            FlipHorizontal,
            FlipVertical,
            FlipAll,

            AddFlipHorizontal,
            AddFlipHorizontalReversed,
            AddFlipVertical,
            AddFlipAll,

            Sliced,
            SlicedAddFlipHorizontal,
            SlicedAddFlipVertical,
            SlicedAddFlipAll,

            SlicedTiledHorizontal = Sliced + 4,
            //SlicedTiledVertical,
            //SlicedTiledAll,

            Tiled = SlicedTiledHorizontal + 3,
        }

        [SerializeField] ImageType _imageType;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var sprite = activeSprite;
            if (sprite == null) return;

            GetAABB(out float left, out float top, out float right, out float bottom);
            sprite.GetUVs(out float uvLeft, out float uvTop, out float uvRight, out float uvBottom);

            if (_imageType == ImageType.FlipHorizontal)
            {
                AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipHorizontal);
            }
            else if (_imageType == ImageType.FlipVertical)
            {
                AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipVertical);
            }
            else if (_imageType == ImageType.FlipAll)
            {
                AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipAll);
            }
            else if (_imageType == ImageType.AddFlipHorizontal)
            {
                float middle = (left + right) * 0.5f;
                AddQuad(vh, left, top, middle, bottom, uvLeft, uvTop, uvRight, uvBottom);
                AddQuad(vh, middle, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom,
                    UVTransformType.FlipHorizontal);
            }
            else if (_imageType == ImageType.AddFlipHorizontalReversed)
            {
                float middle = (left + right) * 0.5f;
                AddQuad(vh, middle, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                AddQuad(vh, left, top, middle, bottom, uvLeft, uvTop, uvRight, uvBottom,
                    UVTransformType.FlipHorizontal);
            }
            else if (_imageType == ImageType.AddFlipVertical)
            {
                float middle = (bottom + top) * 0.5f;
                AddQuad(vh, left, top, right, middle, uvLeft, uvTop, uvRight, uvBottom);
                AddQuad(vh, left, middle, right, bottom, uvLeft, uvTop, uvRight, uvBottom,
                    UVTransformType.FlipVertical);
            }
            else if (_imageType == ImageType.AddFlipAll)
            {
                float middle1 = (left + right) * 0.5f;
                float middle2 = (bottom + top) * 0.5f;
                AddQuad(vh, left, top, middle1, middle2, uvLeft, uvTop, uvRight, uvBottom);
                AddQuad(vh, middle1, top, right, middle2, uvLeft, uvTop, uvRight, uvBottom,
                    UVTransformType.FlipHorizontal);
                AddQuad(vh, left, middle2, middle1, bottom, uvLeft, uvTop, uvRight, uvBottom,
                    UVTransformType.FlipVertical);
                AddQuad(vh, middle1, middle2, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipAll);
            }
            else if (_imageType == ImageType.Sliced)
            {
                AddSliced(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
            }
  
            else if (_imageType == ImageType.SlicedTiledHorizontal)
            {
                float width = right - left;
                var size = sprite.rect.size;
                var border = sprite.border;
                bool sliceHorizontal = width > size.x && (border.x > 0 || border.z > 0);
                if (sliceHorizontal)
                {
                    var texture = sprite.texture;
                    float widthFactor = 1f / texture.width;
                    float uvX = uvLeft + border.x * widthFactor;
                    float uvZ = uvRight - border.z * widthFactor;
                    float leftRight = border.x + border.z;
                    float middle = size.x - leftRight;
                    int middleCount = Mathf.Max(Mathf.RoundToInt((width - leftRight) / middle), 1);
                    float scaleX = width / (leftRight + middleCount * middle);
                    float x = left + border.x * scaleX;
                    float stepX = middle * scaleX;
                    // Left
                    AddQuad(vh, left, top, x, bottom, uvLeft, uvTop, uvX, uvBottom);
                    // Middle
                    for (int i = 0; i < middleCount; i++)
                    {
                        float x2 = x + stepX;
                        AddQuad(vh, x, top, x2, bottom, uvX, uvTop, uvZ, uvBottom);
                        x = x2;
                    }

                    // Right
                    AddQuad(vh, x, top, right, bottom, uvZ, uvTop, uvRight, uvBottom);
                }
                else
                {
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                }
            }
            else if (_imageType == ImageType.SlicedAddFlipVertical)
            {
                float width = right - left;
                float height = top - bottom;
                float spriteWidth = sprite.GetRectWidth();
                float spriteHeight = sprite.GetRectHeight();

                if (height > spriteHeight)
                {
                    float heightFactor = 1f / sprite.texture.height;
                    var border = sprite.border;
                    float uvW = uvTop - border.w * heightFactor;
                    float uvY = uvBottom + border.y * heightFactor;

                    float yTop = top - border.w;
                    float yBottom = bottom + border.w;

                    if (width > spriteWidth)
                    {
                        float widthFactor = 1f / sprite.texture.width;
                        float uvX = uvLeft + border.x * widthFactor;
                        float uvZ = uvRight - border.z * widthFactor;

                        float xLeft = left + border.x;
                        float xRight = right - border.z;

                        // Bottom
                        AddQuad(vh, left, yBottom, xLeft, bottom, uvLeft, uvTop, uvX, uvW, UVTransformType.FlipVertical);
                        AddQuad(vh, xLeft, yBottom, xRight, bottom, uvX, uvTop, uvZ, uvW, UVTransformType.FlipVertical);
                        AddQuad(vh, xRight, yBottom, right, bottom, uvZ, uvTop, uvRight, uvW, UVTransformType.FlipVertical);

                        // Middle
                        AddQuad(vh, left, yTop, xLeft, yBottom, uvLeft, uvW, uvX, uvY);
                        AddQuad(vh, xLeft, yTop, xRight, yBottom, uvX, uvW, uvZ, uvY);
                        AddQuad(vh, xRight, yTop, right, yBottom, uvZ, uvW, uvRight, uvY);

                        // Top
                        AddQuad(vh, left, top, xLeft, yTop, uvLeft, uvTop, uvX, uvW);
                        AddQuad(vh, xLeft, top, xRight, yTop, uvX, uvTop, uvZ, uvW);
                        AddQuad(vh, xRight, top, right, yTop, uvZ, uvTop, uvRight, uvW);
                    }
                    else
                    {
                        // Bottom
                        AddQuad(vh, left, yBottom, right, bottom, uvLeft, uvTop, uvRight, uvW, UVTransformType.FlipVertical);
                        // Middle
                        AddQuad(vh, left, yTop, right, yBottom, uvLeft, uvW, uvRight, uvY);
                        // Top
                        AddQuad(vh, left, top, right, yTop, uvLeft, uvTop, uvRight, uvW);
                    }
                }
                else
                {
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                }
            }
            else if (_imageType == ImageType.SlicedAddFlipHorizontal)
            {
                float width = right - left;
                float height = top - bottom;
                float spriteWidth = sprite.GetRectWidth();
                float spriteHeight = sprite.GetRectHeight();

                if (width > spriteWidth)
                {
                    float widthFactor = 1f / sprite.texture.width;
                    var border = sprite.border;
                    float uvX = uvLeft + border.x * widthFactor;
                    float uvZ = uvRight - border.z * widthFactor;
                    
                    float xLeft = left + border.x;
                    float xRight = right - border.x;

                    if (height > spriteHeight)
                    {
                        float heightFactor = 1f / sprite.texture.height;
                        float uvW = uvTop - border.w * heightFactor;
                        float uvY = uvBottom + border.y * heightFactor;

                        float yBottom = bottom + border.y;
                        float yTop = top - border.w;

                        // Bottom
                        AddQuad(vh, left, yBottom, xLeft, bottom, uvLeft, uvY, uvX, uvBottom);
                        AddQuad(vh, xLeft, yBottom, xRight, bottom, uvX, uvY, uvZ, uvBottom);
                        AddQuad(vh, xRight, yBottom, right, bottom, uvLeft, uvY, uvX, uvBottom, UVTransformType.FlipHorizontal);

                        // Middle
                        AddQuad(vh, left, yTop, xLeft, yBottom, uvLeft, uvW, uvX, uvY);
                        AddQuad(vh, xLeft, yTop, xRight, yBottom, uvX, uvW, uvZ, uvY);
                        AddQuad(vh, xRight, yTop, right, yBottom, uvLeft, uvW, uvX, uvY, UVTransformType.FlipHorizontal);

                        // Top
                        AddQuad(vh, left, top, xLeft, yTop, uvLeft, uvTop, uvX, uvW);
                        AddQuad(vh, xLeft, top, xRight, yTop, uvX, uvTop, uvZ, uvW);
                        AddQuad(vh, xRight, top, right, yTop, uvLeft, uvTop, uvX, uvW, UVTransformType.FlipHorizontal);
                    }
                    else
                    {
                        // Left
                        AddQuad(vh, left, top, xLeft, bottom, uvLeft, uvTop, uvX, uvBottom);
                        // Right
                        AddQuad(vh, xRight, top, right, bottom, uvLeft, uvTop, uvX, uvBottom, UVTransformType.FlipHorizontal);
                        // Middle
                        AddQuad(vh, xLeft, top, xRight, bottom, uvX, uvTop, uvZ, uvBottom);
                    }
                }
                else
                {
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                }
            }
            else if (_imageType == ImageType.SlicedAddFlipAll)
            {
                AddSlicedAddFlipAll(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, sprite);
            }
            else if (_imageType == ImageType.Tiled)
            {
                float width = right - left;
                float height = top - bottom;
                sprite.GetSizeInPixels(out float spriteWidth, out float spriteHeight);
                int columnCount = Mathf.CeilToInt((width - Mathf.Epsilon) / spriteWidth);
                int rowCount = Mathf.CeilToInt((height - Mathf.Epsilon) / spriteHeight);
                float t = top;
                for (int i = 0; i < rowCount; i++)
                {
                    float l = left;
                    float b = t - spriteHeight;
                    for (int j = 0; j < columnCount; j++)
                    {
                        float t2 = t;
                        float b2 = b;
                        if (t2 > bottom)
                        {
                            float uvBottom2 = uvBottom;
                            if (b2 < bottom)
                            {
                                b2 = bottom;
                                uvBottom2 = uvTop - (uvTop - uvBottom) * (t2 - bottom) / spriteHeight;
                            }

                            float r = l + spriteWidth;
                            float uvRight2 = uvRight;
                            if (r > right)
                            {
                                r = right;
                                uvRight2 = uvLeft + (uvRight - uvLeft) * (right - l) / spriteWidth;
                            }

                            AddQuad(vh, l, t2, r, b2, uvLeft, uvTop, uvRight2, uvBottom2);
                        }

                        l += spriteWidth;
                    }

                    t -= spriteHeight;
                }
            }
            else
            {
                AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
            }
        }
        void AddSlicedAddFlipAll(
            VertexHelper vh,
            float left, float top, float right, float bottom,
            float uvLeft, float uvTop, float uvRight, float uvBottom,
            Sprite sprite)
        {
            float width = right - left;
            float height = top - bottom;

            float spriteWidth = sprite.GetRectWidth();
            float spriteHeight = sprite.GetRectHeight();

            if (width <= spriteWidth || height <= spriteHeight)
            {
                AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                return;
            }

            var border = sprite.border;
            float texW = sprite.texture.width;
            float texH = sprite.texture.height;

            // UV split
            float uvX = uvLeft + border.x / texW;
            float uvZ = uvRight - border.z / texW;
            float uvY = uvBottom + border.y / texH;
            float uvW = uvTop - border.w / texH;

            // Position split
            float xL = left + border.x;
            float xR = right - border.z;
            float yB = bottom + border.y;
            float yT = top - border.w;

            // --- Bottom row ---
            AddQuad(vh, left,  yB, xL, bottom, uvLeft,  uvY, uvX,  uvBottom, UVTransformType.FlipAll);
            AddQuad(vh, xL,    yB, xR, bottom, uvX,     uvY, uvZ,  uvBottom, UVTransformType.FlipVertical);
            AddQuad(vh, xR,    yB, right, bottom, uvX,  uvY, uvRight, uvBottom, UVTransformType.FlipAll);

            // --- Middle row ---
            AddQuad(vh, left,  yT, xL, yB, uvLeft, uvW, uvX, uvY, UVTransformType.FlipHorizontal);
            AddQuad(vh, xL,    yT, xR, yB, uvX,    uvW, uvZ, uvY);
            AddQuad(vh, xR,    yT, right, yB, uvX, uvW, uvRight, uvY, UVTransformType.FlipHorizontal);

            // --- Top row ---
            AddQuad(vh, left,  top, xL, yT, uvLeft, uvTop, uvX, uvW, UVTransformType.FlipAll);
            AddQuad(vh, xL,    top, xR, yT, uvX,    uvTop, uvZ, uvW, UVTransformType.FlipVertical);
            AddQuad(vh, xR,    top, right, yT, uvX, uvTop, uvRight, uvW, UVTransformType.FlipAll);
        }

        protected override void SetNativeSize(Sprite sprite)
        {
            if (sprite == null) return;

            var size = sprite.rect.size;
            if (_imageType == ImageType.AddFlipHorizontal)
            {
                size.x *= 2;
            }
            else if (_imageType == ImageType.AddFlipVertical)
            {
                size.y *= 2;
            }
            else if (_imageType == ImageType.AddFlipAll)
            {
                size.x *= 2;
                size.y *= 2;
            }

            rectTransform.sizeDelta = size;
#if UNITY_EDITOR
            gameObject.SetDirty();
#endif
        }

#if UNITY_EDITOR
      
#endif
    }
}

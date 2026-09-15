using UnityEngine;
using UnityEngine.UI;

namespace Anvil
{
    public partial class UIMeshBehaviour
    {
        protected void AddSliced(VertexHelper vh, float left, float top, float right, float bottom,
                                float uvLeft, float uvTop, float uvRight, float uvBottom)
        {
            float width = right - left;
            float height = top - bottom;
            sprite.GetSizeInPixels(out float spriteWidth, out float spriteHeight);
            var border = sprite.border;
            bool sliceHorizontal = width > spriteWidth && (border.x > 0 || border.z > 0);
            bool sliceVertical = height > spriteHeight && (border.y > 0 || border.w > 0);
            if (sliceHorizontal || sliceVertical)
            {
                var texture = sprite.texture;
                float widthFactor = 1f / texture.width;
                float heightFactor = 1f / texture.height;
                float uvX = uvLeft + border.x * widthFactor;
                float uvY = uvBottom + border.y * heightFactor;
                float uvZ = uvRight - border.z * widthFactor;
                float uvW = uvTop - border.w * heightFactor;
                if (sliceHorizontal)
                {
                    float x = left + border.x;
                    float z = right - border.z;
                    if (sliceVertical)
                    {
                        float y = bottom + border.y;
                        float w = top - border.w;
                        // Top
                        AddQuad(vh, left, top, x, w, uvLeft, uvTop, uvX, uvW);
                        AddQuad(vh, x, top, z, w, uvX, uvTop, uvZ, uvW);
                        AddQuad(vh, z, top, right, w, uvZ, uvTop, uvRight, uvW);
                        // Middle
                        AddQuad(vh, left, w, x, y, uvLeft, uvW, uvX, uvY);
                        AddQuad(vh, x, w, z, y, uvX, uvW, uvZ, uvY);
                        AddQuad(vh, z, w, right, y, uvZ, uvW, uvRight, uvY);
                        // Bottom
                        AddQuad(vh, left, y, x, bottom, uvLeft, uvY, uvX, uvBottom);
                        AddQuad(vh, x, y, z, bottom, uvX, uvY, uvZ, uvBottom);
                        AddQuad(vh, z, y, right, bottom, uvZ, uvY, uvRight, uvBottom);
                    }
                    else
                    {
                        // Left
                        AddQuad(vh, left, top, x, bottom, uvLeft, uvTop, uvX, uvBottom);
                        // Middle
                        AddQuad(vh, x, top, z, bottom, uvX, uvTop, uvZ, uvBottom);
                        // Right
                        AddQuad(vh, z, top, right, bottom, uvZ, uvTop, uvRight, uvBottom);
                    }
                }
                else
                {
                    float y = bottom + border.y;
                    float w = top - border.w;
                    // Top
                    AddQuad(vh, left, top, right, w, uvLeft, uvTop, uvRight, uvW);
                    // Middle
                    AddQuad(vh, left, w, right, y, uvLeft, uvW, uvRight, uvY);
                    // Bottom
                    AddQuad(vh, left, y, right, bottom, uvLeft, uvY, uvRight, uvBottom);
                }
            }
            else
            {
                AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
            }
        }

        protected void Add9Slices(VertexHelper vh, Sprite sprite,
                                    float left, float top, float right, float bottom,
                                    float uvLeft, float uvTop, float uvRight, float uvBottom)
        {
            sprite.GetBorderAndUVs(out float borderLeft, out float borderTop, out float borderRight, out float borderBottom, out float uvX, out float uvY, out float uvZ, out float uvW);
            Add9Slices(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, borderLeft, borderTop, borderRight, borderBottom, uvX, uvY, uvZ, uvW);
        }

        protected void Add9Slices(VertexHelper vh,
                                    float left, float top, float right, float bottom,
                                    float uvLeft, float uvTop, float uvRight, float uvBottom,
                                    float borderLeft, float borderTop, float borderRight, float borderBottom,
                                    float uvX, float uvY, float uvZ, float uvW)
        {
            float x1 = left + borderLeft;
            float x2 = right - borderRight;
            float y1 = top - borderTop;
            float y2 = bottom + borderBottom;

            // Top
            AddQuad(vh, left, top, x1, y1, uvLeft, uvTop, uvX, uvW);
            AddQuad(vh, x1, top, x2, y1, uvX, uvTop, uvZ, uvW);
            AddQuad(vh, x2, top, right, y1, uvZ, uvTop, uvRight, uvW);

            // Middle
            AddQuad(vh, left, y1, x1, y2, uvLeft, uvW, uvX, uvY);
            AddQuad(vh, x2, y1, right, y2, uvZ, uvW, uvRight, uvY);

            // Bottom
            AddQuad(vh, left, y2, x1, bottom, uvLeft, uvY, uvX, uvBottom);
            AddQuad(vh, x1, y2, x2, bottom, uvX, uvY, uvZ, uvBottom);
            AddQuad(vh, x2, y2, right, bottom, uvZ, uvY, uvRight, uvBottom);
        }
    }
}
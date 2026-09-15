using UnityEngine;
using UnityEngine.UI;

namespace Anvil
{
    public class CustomImage : UIMeshBehaviour
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
            Tiled = SlicedTiledHorizontal + 3,
        }

        [SerializeField] ImageType _imageType;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float ppu = pixelsPerUnit;
            var sprite = activeSprite;
            if (sprite == null) return;

            GetAABB(out float left, out float top, out float right, out float bottom);
            sprite.GetUVs(out float uvLeft, out float uvTop, out float uvRight, out float uvBottom);

            switch (_imageType)
            {
                case ImageType.Simple:
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                    break;

                case ImageType.FlipHorizontal:
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipHorizontal);
                    break;

                case ImageType.FlipVertical:
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipVertical);
                    break;

                case ImageType.FlipAll:
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipAll);
                    break;

                case ImageType.AddFlipHorizontal:
                    {
                        float midX = (left + right) * 0.5f;
                        AddQuad(vh, left, top, midX, bottom, uvLeft, uvTop, uvRight, uvBottom);
                        AddQuad(vh, midX, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipHorizontal);
                    }
                    break;

                case ImageType.AddFlipHorizontalReversed:
                    {
                        float midX = (left + right) * 0.5f;
                        AddQuad(vh, midX, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                        AddQuad(vh, left, top, midX, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipHorizontal);
                    }
                    break;

                case ImageType.AddFlipVertical:
                    {
                        float midY = (bottom + top) * 0.5f;
                        AddQuad(vh, left, top, right, midY, uvLeft, uvTop, uvRight, uvBottom);
                        AddQuad(vh, left, midY, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipVertical);
                    }
                    break;

                case ImageType.AddFlipAll:
                    {
                        float midX = (left + right) * 0.5f;
                        float midY = (bottom + top) * 0.5f;
                        AddQuad(vh, left, top, midX, midY, uvLeft, uvTop, uvRight, uvBottom);
                        AddQuad(vh, midX, top, right, midY, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipHorizontal);
                        AddQuad(vh, left, midY, midX, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipVertical);
                        AddQuad(vh, midX, midY, right, bottom, uvLeft, uvTop, uvRight, uvBottom, UVTransformType.FlipAll);
                    }
                    break;

                case ImageType.Sliced:
                    AddSliced(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                    break;

                case ImageType.SlicedAddFlipHorizontal:
                    AddSlicedAddFlipHorizontal(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, sprite, ppu);
                    break;

                case ImageType.SlicedAddFlipVertical:
                    AddSlicedAddFlipVertical(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, sprite, ppu);
                    break;

                case ImageType.SlicedAddFlipAll:
                    AddSlicedAddFlipAll(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, sprite, ppu);
                    break;

                case ImageType.Tiled:
                    AddTiled(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, sprite);
                    break;

                case ImageType.SlicedTiledHorizontal:
                    AddSlicedTiledHorizontal(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom, sprite);
                    break;

                default:
                    AddQuad(vh, left, top, right, bottom, uvLeft, uvTop, uvRight, uvBottom);
                    break;
            }
        }

        #region Helper Generation Methods

        void AddSlicedAddFlipHorizontal(VertexHelper vh, float l, float t, float r, float b, float uvL, float uvT, float uvR, float uvB, Sprite sprite, float ppu)
        {
            var border = sprite.border;
            float midX = (l + r) * 0.5f;
            float bW = border.x / ppu;
            float uvX_mid = uvL + (border.x / sprite.texture.width);

            // Left Side
            AddQuad(vh, l, t, l + bW, b, uvL, uvT, uvX_mid, uvB);
            AddQuad(vh, l + bW, t, midX, b, uvX_mid, uvT, uvR, uvB);
            // Right Side (Mirror)
            AddQuad(vh, midX, t, r - bW, b, uvX_mid, uvT, uvR, uvB, UVTransformType.FlipHorizontal);
            AddQuad(vh, r - bW, t, r, b, uvL, uvT, uvX_mid, uvB, UVTransformType.FlipHorizontal);
        }

        void AddSlicedAddFlipVertical(VertexHelper vh, float l, float t, float r, float b, float uvL, float uvT, float uvR, float uvB, Sprite sprite, float ppu)
        {
            var border = sprite.border;
            float midY = (t + b) * 0.5f;
            float bH = border.y / ppu;
            float uvY_mid = uvB + (border.y / sprite.texture.height);

            // Top Side
            AddQuad(vh, l, t, r, t - bH, uvL, uvT, uvR, uvY_mid);
            AddQuad(vh, l, t - bH, r, midY, uvL, uvY_mid, uvR, uvB);
            // Bottom Side (Mirror)
            AddQuad(vh, l, midY, r, b + bH, uvL, uvY_mid, uvR, uvB, UVTransformType.FlipVertical);
            AddQuad(vh, l, b + bH, r, b, uvL, uvT, uvR, uvY_mid, UVTransformType.FlipVertical);
        }

        void AddSlicedAddFlipAll(VertexHelper vh, float l, float t, float r, float b, float uvL, float uvT, float uvR, float uvB, Sprite sprite, float ppu)
        {
            var border = sprite.border;
            float texW = sprite.texture.width;
            float texH = sprite.texture.height;

            float midX = (l + r) * 0.5f;
            float midY = (t + b) * 0.5f;

            float bW = border.x / ppu;
            float bH = border.y / ppu;

            float uvX_mid = uvL + (border.x / texW);
            float uvY_mid = uvB + (border.y / texH);

            // Bottom-Left Quad (Source)
            AddQuad(vh, l, b + bH, l + bW, b, uvL, uvY_mid, uvX_mid, uvB); // Corner
            AddQuad(vh, l + bW, b + bH, midX, b, uvX_mid, uvY_mid, uvR, uvB); // Bottom edge
            AddQuad(vh, l, midY, l + bW, b + bH, uvL, uvT, uvX_mid, uvY_mid); // Left edge
            AddQuad(vh, l + bW, midY, midX, b + bH, uvX_mid, uvT, uvR, uvY_mid); // Center

            // Bottom-Right Quad (Mirror H)
            AddQuad(vh, r - bW, b + bH, r, b, uvL, uvY_mid, uvX_mid, uvB, UVTransformType.FlipHorizontal);
            AddQuad(vh, midX, b + bH, r - bW, b, uvX_mid, uvY_mid, uvR, uvB, UVTransformType.FlipHorizontal);
            AddQuad(vh, r - bW, midY, r, b + bH, uvL, uvT, uvX_mid, uvY_mid, UVTransformType.FlipHorizontal);
            AddQuad(vh, midX, midY, r - bW, b + bH, uvX_mid, uvT, uvR, uvY_mid, UVTransformType.FlipHorizontal);

            // Top-Left Quad (Mirror V)
            AddQuad(vh, l, t, l + bW, t - bH, uvL, uvY_mid, uvX_mid, uvB, UVTransformType.FlipVertical);
            AddQuad(vh, l + bW, t, midX, t - bH, uvX_mid, uvY_mid, uvR, uvB, UVTransformType.FlipVertical);
            AddQuad(vh, l, t - bH, l + bW, midY, uvL, uvT, uvX_mid, uvY_mid, UVTransformType.FlipVertical);
            AddQuad(vh, l + bW, t - bH, midX, midY, uvX_mid, uvT, uvR, uvY_mid, UVTransformType.FlipVertical);

            // Top-Right Quad (Mirror All)
            AddQuad(vh, r - bW, t, r, t - bH, uvL, uvY_mid, uvX_mid, uvB, UVTransformType.FlipAll);
            AddQuad(vh, midX, t, r - bW, t - bH, uvX_mid, uvY_mid, uvR, uvB, UVTransformType.FlipAll);
            AddQuad(vh, r - bW, t - bH, r, midY, uvL, uvT, uvX_mid, uvY_mid, UVTransformType.FlipAll);
            AddQuad(vh, midX, t - bH, r - bW, midY, uvX_mid, uvT, uvR, uvY_mid, UVTransformType.FlipAll);
        }

        void AddTiled(VertexHelper vh, float left, float top, float right, float bottom, float uvLeft, float uvTop, float uvRight, float uvBottom, Sprite sprite)
        {
            sprite.GetSizeInPixels(out float sW, out float sH);
            int cols = Mathf.CeilToInt((right - left - Mathf.Epsilon) / sW);
            int rows = Mathf.CeilToInt((top - bottom - Mathf.Epsilon) / sH);
            float currT = top;
            for (int i = 0; i < rows; i++)
            {
                float currL = left;
                float currB = currT - sH;
                for (int j = 0; j < cols; j++)
                {
                    float t2 = currT, b2 = currB, r2 = currL + sW;
                    float uvR2 = uvRight, uvB2 = uvBottom;

                    if (b2 < bottom) { b2 = bottom; uvB2 = uvTop - (uvTop - uvBottom) * (t2 - bottom) / sH; }
                    if (r2 > right) { r2 = right; uvR2 = uvLeft + (uvRight - uvLeft) * (right - currL) / sW; }

                    if (t2 > bottom && currL < right)
                        AddQuad(vh, currL, t2, r2, b2, uvLeft, uvTop, uvR2, uvB2);
                    currL += sW;
                }
                currT -= sH;
            }
        }

        void AddSlicedTiledHorizontal(VertexHelper vh, float l, float t, float r, float b, float uvL, float uvT, float uvR, float uvB, Sprite sprite)
        {
            var border = sprite.border;
            float uvX = uvL + border.x / sprite.texture.width;
            float uvZ = uvR - border.z / sprite.texture.width;
            float bL = border.x, bR = border.z;
            float midW = sprite.rect.width - (bL + bR);

            AddQuad(vh, l, t, l + bL, b, uvL, uvT, uvX, uvB); // Left
            float currX = l + bL;
            while (currX < r - bR)
            {
                float nextX = Mathf.Min(currX + midW, r - bR);
                float currUVR = uvX + (uvZ - uvX) * ((nextX - currX) / midW);
                AddQuad(vh, currX, t, nextX, b, uvX, uvT, currUVR, uvB);
                currX = nextX;
            }
            AddQuad(vh, r - bR, t, r, b, uvZ, uvT, uvR, uvB); // Right
        }

        #endregion

        protected override void SetNativeSize(Sprite sprite)
        {
            if (sprite == null) return;
            float ppu = pixelsPerUnit;
            Vector2 size = sprite.rect.size / ppu;

            switch (_imageType)
            {
                case ImageType.AddFlipHorizontal:
                case ImageType.SlicedAddFlipHorizontal:
                    size.x *= 2; break;
                case ImageType.AddFlipVertical:
                case ImageType.SlicedAddFlipVertical:
                    size.y *= 2; break;
                case ImageType.AddFlipAll:
                case ImageType.SlicedAddFlipAll:
                    size.x *= 2; size.y *= 2; break;
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
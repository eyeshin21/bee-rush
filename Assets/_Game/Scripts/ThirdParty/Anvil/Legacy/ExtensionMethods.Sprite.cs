using UnityEngine;

namespace Anvil
{
    public static partial class ExtensionMethods
    {
        public static void GetSize(this Sprite sprite, out float width, out float height)
        {
            if (sprite != null)
            {
                var size = sprite.bounds.size;
                width = size.x;
                height = size.y;
            }
            else
            {
                width = height = 0;
            }
        }

        public static void GetSizeInPixels(this Sprite sprite, out float width, out float height)
        {
            if (sprite != null)
            {
                var size = sprite.rect.size;
                width = size.x;
                height = size.y;
            }
            else
            {
                width = height = 0;
            }
        }

        public static void GetUVs(this Sprite sprite, out float uvLeft, out float uvTop, out float uvRight, out float uvBottom)
        {
            var texture = sprite != null ? sprite.texture : null;
            if (texture != null)
            {
                var rect = sprite.textureRect;
                var rectOffset = sprite.textureRectOffset;

                uvLeft = (rect.x + rectOffset.x) / texture.width;
                uvRight = uvLeft + rect.width / texture.width;
                uvBottom = (rect.y + rectOffset.y) / texture.height;
                uvTop = uvBottom + rect.height / texture.height;
            }
            else
            {
                uvLeft = uvTop = uvRight = uvBottom = 0;
            }
        }

        public static void GetUVs(this Sprite sprite,
                                  out float uvLeft, out float uvTop, out float uvRight, out float uvBottom,
                                  out float uvX, out float uvY, out float uvZ, out float uvW)
        {
            var texture = sprite != null ? sprite.texture : null;
            if (texture != null)
            {
                var rect = sprite.textureRect;
                var rectOffset = sprite.textureRectOffset;
                float widthFactor = 1f / texture.width;
                float heightFactor = 1f / texture.height;

                uvLeft = (rect.x + rectOffset.x) * widthFactor;
                uvRight = uvLeft + rect.width * widthFactor;
                uvBottom = (rect.y + rectOffset.y) * heightFactor;
                uvTop = uvBottom + rect.height * heightFactor;

                var border = sprite.border;
                uvX = uvLeft + border.x * widthFactor;
                uvY = uvBottom + border.y * heightFactor;
                uvZ = uvRight - border.z * widthFactor;
                uvW = uvTop - border.w * heightFactor;
            }
            else
            {
                uvLeft = uvTop = uvRight = uvBottom = 0;
                uvX = uvY = uvZ = uvW = 0;
            }
        }

        public static bool GetHorizontalBorderAndUVs(this Sprite sprite, out float borderLeft, out float borderRight, out float uvX, out float uvZ)
        {
            var texture = sprite != null ? sprite.texture : null;
            if (texture != null)
            {
                var border = sprite.border;
                borderLeft = border.x;
                borderRight = border.z;
                if (borderLeft > 0 || borderRight > 0)
                {
                    var rect = sprite.textureRect;
                    var rectOffset = sprite.textureRectOffset;
                    float widthFactor = 1f / texture.width;
                    float uvLeft = (rect.x + rectOffset.x) * widthFactor;
                    float uvRight = uvLeft + rect.width * widthFactor;
                    uvX = uvLeft + border.x * widthFactor;
                    uvZ = uvRight - border.z * widthFactor;
                    return true;
                }
            }

            borderLeft = borderRight = 0;
            uvX = uvZ = 0;
            return false;
        }

        public static bool GetBorderAndUVs(this Sprite sprite, out float borderLeft, out float borderTop, out float borderRight, out float borderBottom,
                                            out float uvX, out float uvY, out float uvZ, out float uvW)
        {
            var texture = sprite != null ? sprite.texture : null;
            if (texture != null)
            {
                var border = sprite.border;
                borderLeft = border.x;
                borderTop = border.w;
                borderRight = border.z;
                borderBottom = border.y;
                if (borderLeft > 0 || borderTop > 0 || borderRight > 0 || borderBottom > 0)
                {
                    var rect = sprite.textureRect;
                    var rectOffset = sprite.textureRectOffset;
                    float widthFactor = 1f / texture.width;
                    float heightFactor = 1f / texture.height;

                    float uvLeft = (rect.x + rectOffset.x) * widthFactor;
                    float uvRight = uvLeft + rect.width * widthFactor;
                    float uvBottom = (rect.y + rectOffset.y) * heightFactor;
                    float uvTop = uvBottom + rect.height * heightFactor;

                    uvX = uvLeft + border.x * widthFactor;
                    uvY = uvBottom + border.y * heightFactor;
                    uvZ = uvRight - border.z * widthFactor;
                    uvW = uvTop - border.w * heightFactor;

                    return true;
                }
            }

            borderLeft = borderTop = borderRight = borderBottom = 0;
            uvX = uvY = uvZ = uvW = 0;
            return false;
        }
        
        /// <summary>
        /// Returns width in pixels.
        /// </summary>
        public static float GetRectWidth(this Sprite sprite)
        {
            return sprite != null ? sprite.rect.width : 0;
        }

        /// <summary>
        /// Returns height in pixels.
        /// </summary>
        public static float GetRectHeight(this Sprite sprite)
        {
            return sprite != null ? sprite.rect.height : 0;
        }
    }
}
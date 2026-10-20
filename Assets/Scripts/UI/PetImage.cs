using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    /// <summary>An Image whose pointer target follows the opaque pixels of its current frame.</summary>
    public sealed class PetImage : Image, IDisposable
    {
        private readonly Dictionary<Texture2D, byte[]> alphaMasks = new Dictionary<Texture2D, byte[]>();
        private Sprite cachedSprite;
        private Vector2[] vertices, textureCoordinates;
        private ushort[] triangles;

        public PetImage()
        {
            scaleMode = ScaleMode.ScaleToFit;
            pickingMode = PickingMode.Position;
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            if (!base.ContainsPoint(localPoint) || tintColor.a <= 0) return false;
            var current = sprite;
            var texture = current != null ? current.texture : image as Texture2D;
            if (texture == null) return false;

            Vector2 size = current != null ? current.rect.size
                : new Vector2(texture.width * Mathf.Abs(uv.width), texture.height * Mathf.Abs(uv.height));
            var area = contentRect;
            if (size.x <= 0 || size.y <= 0 || area.width <= 0 || area.height <= 0) return false;
            // The preview uses ScaleToFit: exclude the letterboxing inside the Image element.
            float fit = Mathf.Min(area.width / size.x, area.height / size.y);
            var fitted = new Rect(area.center - size * fit * 0.5f, size * fit);
            if (!fitted.Contains(localPoint)) return false;
            var position = new Vector2((localPoint.x - fitted.x) / fitted.width,
                1f - (localPoint.y - fitted.y) / fitted.height);

            Vector2 texelUV;
            if (current != null)
            {
                if (cachedSprite != current)
                {
                    cachedSprite = current;
                    vertices = current.vertices;
                    triangles = current.triangles;
                    textureCoordinates = current.uv;
                }
                // Use the actual sprite mesh/UVs, including sliced and atlas-packed frames.
                var bounds = current.bounds;
                var meshPoint = (Vector2)bounds.min + Vector2.Scale(position, bounds.size);
                if (!TryGetUV(meshPoint, out texelUV)) return false;
            }
            else texelUV = uv.position + Vector2.Scale(position, uv.size);

            if (!alphaMasks.TryGetValue(texture, out var mask))
            {
                mask = ReadAlpha(texture);
                alphaMasks.Add(texture, mask);
            }
            int x = Mathf.Clamp(Mathf.FloorToInt(texelUV.x * texture.width), 0, texture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(texelUV.y * texture.height), 0, texture.height - 1);
            return mask != null && mask[y * texture.width + x] >= 26;
        }

        private bool TryGetUV(Vector2 point, out Vector2 result)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var ab = vertices[b] - vertices[a];
                var ac = vertices[c] - vertices[a];
                var ap = point - vertices[a];
                float determinant = ab.x * ac.y - ab.y * ac.x;
                if (Mathf.Abs(determinant) < 0.000001f) continue;
                float weightB = (ap.x * ac.y - ap.y * ac.x) / determinant;
                float weightC = (ab.x * ap.y - ab.y * ap.x) / determinant;
                float weightA = 1f - weightB - weightC;
                if (weightA < -0.00001f || weightB < -0.00001f || weightC < -0.00001f) continue;
                result = textureCoordinates[a] * weightA + textureCoordinates[b] * weightB + textureCoordinates[c] * weightC;
                return true;
            }
            result = default;
            return false;
        }

        private static byte[] ReadAlpha(Texture2D texture)
        {
            // Imported art need not enable Read/Write. Read back once per texture, not per frame.
            RenderTexture temporary = null;
            Texture2D readable = null;
            var previous = RenderTexture.active;
            try
            {
                Color32[] pixels;
                if (texture.isReadable) pixels = texture.GetPixels32();
                else
                {
                    temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(texture, temporary);
                    RenderTexture.active = temporary;
                    readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    readable.Apply();
                    pixels = readable.GetPixels32();
                }
                var mask = new byte[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) mask[i] = pixels[i].a;
                return mask;
            }
            catch (UnityException exception)
            {
                Debug.LogWarning("Could not read pet transparency: " + exception.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (readable != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(readable);
                    else UnityEngine.Object.DestroyImmediate(readable);
                }
            }
        }

        public void Dispose()
        {
            alphaMasks.Clear();
            cachedSprite = null;
            vertices = textureCoordinates = null;
            triangles = null;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingSeriousGame.UI
{
    /// <summary>A bounded, undoable paint layer clipped to the original image's alpha.</summary>
    public sealed class PetPaintTexture : IDisposable
    {
        private readonly Color32[] original;
        private Color32[] paint;
        private readonly Color32[] composite;
        private readonly List<Color32[]> undo = new List<Color32[]>();
        private bool strokeOpen;
        private bool strokeChanged;
        private Color32[] strokeBefore;
        public Texture2D Texture { get; }
        public bool CanUndo => undo.Count > 0;

        public PetPaintTexture(Texture source, Rect uv, int maxSize = 512)
        {
            float width = source.width * uv.width, height = source.height * uv.height;
            float scale = Mathf.Min(1, maxSize / Mathf.Max(width, height));
            int w = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            Texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Custom pet", wrapMode = TextureWrapMode.Clamp };
            // A GPU copy works even when the imported pet image has Read/Write disabled.
            var temporary = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, temporary, uv.size, uv.position);
                RenderTexture.active = temporary;
                Texture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                Texture.Apply(false);
            }
            finally
            {
                GL.sRGBWrite = previousSrgb;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
            original = Texture.GetPixels32();
            paint = new Color32[original.Length];
            composite = new Color32[original.Length];
        }

        public void BeginStroke()
        {
            EndStroke();
            strokeOpen = true;
            strokeChanged = false;
            strokeBefore = (Color32[])paint.Clone();
        }

        public void PaintLine(Vector2 from, Vector2 to, float radius, Color32 colour, bool erase)
        {
            if (!strokeOpen) return;
            radius = Mathf.Clamp(radius, 1, 64);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / Mathf.Max(1, radius * 0.4f)));
            // Input is constrained to the preview; cap work if a pointer jumps unexpectedly.
            steps = Mathf.Min(steps, 2048);
            for (int step = 0; step <= steps; step++) Stamp(Vector2.Lerp(from, to, step / (float)steps), radius, colour, erase);
            Refresh();
        }

        private void Stamp(Vector2 centre, float radius, Color32 colour, bool erase)
        {
            int w = Texture.width, h = Texture.height;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(centre.y - radius)); y <= Mathf.Min(h - 1, Mathf.CeilToInt(centre.y + radius)); y++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt(centre.x - radius)); x <= Mathf.Min(w - 1, Mathf.CeilToInt(centre.x + radius)); x++)
            {
                int index = y * w + x;
                if (original[index].a == 0 || (new Vector2(x, y) - centre).sqrMagnitude > radius * radius) continue;
                Color32 next = erase ? new Color32(0, 0, 0, 0) : colour;
                if (paint[index].Equals(next)) continue;
                paint[index] = next;
                strokeChanged = true;
            }
        }

        public void EndStroke()
        {
            if (!strokeOpen) return;
            if (strokeChanged) Remember(strokeBefore);
            strokeBefore = null;
            strokeOpen = false;
        }

        public void Undo()
        {
            EndStroke();
            if (!CanUndo) return;
            paint = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Refresh();
        }

        public void Clear()
        {
            EndStroke();
            if (!Array.Exists(paint, pixel => pixel.a != 0)) return;
            Remember((Color32[])paint.Clone());
            Array.Clear(paint, 0, paint.Length);
            Refresh();
        }

        private void Remember(Color32[] pixels)
        {
            if (undo.Count == 12) undo.RemoveAt(0);
            undo.Add(pixels);
        }

        private void Refresh()
        {
            for (int i = 0; i < original.Length; i++)
            {
                var pixel = Color.Lerp(original[i], paint[i], paint[i].a / 255f);
                pixel.a = original[i].a / 255f;
                composite[i] = pixel;
            }
            Texture.SetPixels32(composite);
            Texture.Apply(false);
        }

        public byte[] ExportPng() => Texture.EncodeToPNG();
        public void Dispose() => UnityEngine.Object.Destroy(Texture);
    }
}

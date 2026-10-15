using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    /// <summary>Displays imported Sprite frames directly, including sliced or packed sprites.</summary>
    public sealed class PetSpritePreview : IDisposable
    {
        private readonly Image image;
        private IVisualElementScheduledItem animation;
        private Sprite[] frames = Array.Empty<Sprite>();
        private int frame;
        public bool animationPlaying = true;


        public bool HasImage => image.sprite != null || image.image != null;
        public bool CanAnimate => frames.Length > 1;

        public PetSpritePreview(Image image)
        {
            this.image = image;
            image.scaleMode = ScaleMode.ScaleToFit;
        }

        public void Bind(FileInfo[] sheetPath, string stillPath, float framesPerSecond)
        {
            Dispose();
            image.image = null;
            image.sprite = null;
            frame = 0;
            var collectedFrames = new List<Sprite>();
            Array.Sort(sheetPath, (a, b) =>
    string.CompareOrdinal(a.Name, b.Name));
            foreach (var item in sheetPath)
            {
                var itemPath = Regex.Replace(
                    item.FullName, @".*[\\/]Resources[\\/]", string.Empty)
                    .Replace('\\', '/');

                itemPath = Path.ChangeExtension(itemPath, null);

                var loadedFrames = Resources.LoadAll<Sprite>(itemPath);

                Array.Sort(loadedFrames, (a, b) =>
                {
                    int row = b.rect.y.CompareTo(a.rect.y);
                    return row != 0 ? row : a.rect.x.CompareTo(b.rect.x);
                });

                collectedFrames.AddRange(loadedFrames);
            }
            frames = collectedFrames.ToArray();
            ShowPreview(stillPath, framesPerSecond);
        }
        public void Bind(string sheetPath, string stillPath, float framesPerSecond)
        {
            Dispose();
            image.image = null;
            image.sprite = null;
            frame = 0;

            // Resources paths are relative to Resources, without the file extension.
            frames = string.IsNullOrWhiteSpace(sheetPath)
                ? Array.Empty<Sprite>()
                : Resources.LoadAll<Sprite>(sheetPath);

            Array.Sort(frames, (a, b) =>
            {
                // Keep each source sheet together when loading an entire folder.
                int sheet = string.CompareOrdinal(a.texture.name, b.texture.name);
                if (sheet != 0) return sheet;
                int row = b.rect.y.CompareTo(a.rect.y);
                int column = a.rect.x.CompareTo(b.rect.x);
                return row != 0 ? row : column != 0 ? column : string.CompareOrdinal(a.name, b.name);
            });

            ShowPreview(stillPath, framesPerSecond);
            if (!HasImage && !string.IsNullOrWhiteSpace(sheetPath))
            {
                var textures = Resources.LoadAll<Texture2D>(sheetPath);
                Array.Sort(textures, (a, b) => string.CompareOrdinal(a.name, b.name));
                if (textures.Length > 0) image.image = textures[0];
            }
        }

        private void ShowPreview(string stillPath, float framesPerSecond)
        {
            if (frames.Length > 0) image.sprite = frames[0];
            else if (!string.IsNullOrWhiteSpace(stillPath))
            {
                image.sprite = Resources.Load<Sprite>(stillPath);
                if (image.sprite == null) image.image = Resources.Load<Texture2D>(stillPath);
            }

            if (!CanAnimate) return;
            float fps = float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond) || framesPerSecond <= 0
                ? 10 : Mathf.Clamp(framesPerSecond, 1, 30);
            animation = image.schedule.Execute(() =>
            {
                frame = (frame + 1) % frames.Length;
                image.sprite = frames[frame];
            }).Every((long)(1000 / fps));
            if (!animationPlaying) animation.Pause();
        }

        public void SetPlaying(bool playing)
        {
            if (playing)
            {
                animation?.Resume();
                animationPlaying = true;
            }
            else
            {
                animation?.Pause();
                animationPlaying = false;
            }
        }

        public void Dispose()
        {
            animation?.Pause();
            animation = null;
        }
    }
}

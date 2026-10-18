using System;
using UnityEngine;

namespace GamblingSeriousGame.Pets
{
    public enum PetAnimationCategory { Activity, Sleep, Death, Reaction }

    /// <summary>One action from a sprite sheet, with frames in playback order.</summary>
    [CreateAssetMenu(menuName = "Pets/Animation")]
    public sealed class PetAnimationDefinition : ScriptableObject
    {
        public PetAnimationCategory category;
        public Texture2D texture;
        public Rect[] frameRects = Array.Empty<Rect>();
        [Min(1)] public float framesPerSecond = 10;
        public bool loop = true;

        public bool IsPlayable
        {
            get
            {
                if (texture == null || frameRects == null || frameRects.Length == 0 ||
                    float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond) ||
                    framesPerSecond <= 0) return false;
                foreach (var rect in frameRects)
                    if (!(rect.x >= 0 && rect.y >= 0 && rect.width > 0 && rect.height > 0 &&
                        rect.xMax <= texture.width && rect.yMax <= texture.height)) return false;
                return true;
            }
        }

        /// <summary>The caller owns these sprites and must Destroy them when finished.</summary>
        public Sprite[] CreateFrames()
        {
            if (!IsPlayable) return Array.Empty<Sprite>();
            var frames = new Sprite[frameRects.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Sprite.Create(texture, frameRects[i], new Vector2(0.5f, 0.5f),
                    100, 0, SpriteMeshType.FullRect);
                frames[i].name = name + "_" + i;
            }
            return frames;
        }

        /// <summary>Load one category only; Resources.LoadAll also searches nested folders.</summary>
        public static PetAnimationDefinition[] Load(string animationFolder,
            PetAnimationCategory category = PetAnimationCategory.Activity)
        {
            if (string.IsNullOrWhiteSpace(animationFolder))
                return Array.Empty<PetAnimationDefinition>();
            var animations = Resources.LoadAll<PetAnimationDefinition>(animationFolder);
            var matching = Array.FindAll(animations,
                animation => animation.category == category && animation.IsPlayable);
            Array.Sort(matching, (a, b) => string.CompareOrdinal(a.name, b.name));
            return matching;
        }
    }
}

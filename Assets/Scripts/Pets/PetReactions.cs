using System.Collections.Generic;
using UnityEngine;

namespace GamblingSeriousGame.Pets
{
    public enum PetMood { Sad, Surprised, Happy }

    public static class PetReactions
    {
        public static PetMood GetMood(int health, int maxHealth)
        {
            float fraction = Mathf.Clamp01((float)health / Mathf.Max(1, maxHealth));
            return fraction < 0.35f ? PetMood.Sad : fraction < 0.7f ? PetMood.Surprised : PetMood.Happy;
        }

        public static PetAnimationDefinition Select(PetAnimationSet animations, PetMood mood)
        {
            if (animations == null) return null;
            string[] names = mood == PetMood.Sad ? new[] { "Crying", "Sad", "Hurt" }
                : mood == PetMood.Surprised ? new[] { "Surprised", "Happy", "Barking" }
                : new[] { "Happy", "Excited", "Surprised", "Barking" };
            foreach (string name in names)
            {
                var match = Find(animations.Reactions, name) ?? Find(animations.Activities, name);
                if (match != null) return match;
            }
            return null;
        }

        private static PetAnimationDefinition Find(IReadOnlyList<PetAnimationDefinition> actions, string name)
        {
            foreach (var action in actions)
                if (action != null && action.name == name && action.IsPlayable) return action;
            return null;
        }
    }
}

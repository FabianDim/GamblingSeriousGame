using System.Collections.Generic;
using GamblingSeriousGame.Data;
using UnityEngine;

namespace GamblingSeriousGame.Gameplay
{
    // All coordinates are fractions of the fitted background, independent of screen size.
    public static class RoomLayout
    {
        public static readonly Rect ItemArea = new Rect(.03f, .30f, .94f, .67f);
        // Inset the walking area from the back and side skirting; keep the front edge.
        public static readonly Rect PetArea = new Rect(.12f, .70f, .76f, .27f);
        public static readonly Vector2 PetSize = new Vector2(.24f, .18f);

        public static Rect PetBounds(Vector2 center) => new Rect(center - PetSize / 2, PetSize);

        public static Rect ItemBounds(ShopItem item, Vector2 center)
        {
            // The room art is 180 x 320. Preserve each sprite's proportions.
            float width = Mathf.Clamp(item.Sprite.rect.width / 180f, .09f, .30f);
            float height = width * 180f / 320f * item.Sprite.rect.height / item.Sprite.rect.width;
            if (height > .26f) { width *= .26f / height; height = .26f; }
            return new Rect(center - new Vector2(width, height) / 2, new Vector2(width, height));
        }

        public static bool Contains(Rect area, Rect bounds) =>
            IsFinite(bounds.x) && IsFinite(bounds.y) && bounds.xMin >= area.xMin &&
            bounds.yMin >= area.yMin && bounds.xMax <= area.xMax && bounds.yMax <= area.yMax;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static bool IsFree(Rect bounds, IEnumerable<Rect> obstacles)
        {
            foreach (var obstacle in obstacles)
                if (bounds.Overlaps(obstacle)) return false;
            return true;
        }

        public static bool ClearPath(Vector2 from, Vector2 to, IEnumerable<Rect> obstacles)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .01f));
            for (int i = 0; i <= steps; i++)
            {
                var bounds = PetBounds(Vector2.Lerp(from, to, i / (float)steps));
                if (!Contains(PetArea, bounds) || !IsFree(bounds, obstacles)) return false;
            }
            return true;
        }

        public static IEnumerable<Vector2> PetPositions()
        {
            for (int y = 0; y <= 8; y++)
            for (int x = 0; x <= 28; x++)
                yield return new Vector2(
                    Mathf.Lerp(PetArea.xMin + PetSize.x / 2 + .001f, PetArea.xMax - PetSize.x / 2 - .001f, x / 28f),
                    Mathf.Lerp(PetArea.yMin + PetSize.y / 2 + .001f, PetArea.yMax - PetSize.y / 2 - .001f, y / 8f));
        }

        public static bool TryFindEscape(Vector2 from, Rect proposed, IEnumerable<Rect> obstacles, out Vector2 target)
        {
            target = from;
            float nearest = float.PositiveInfinity;
            // Include a little clearance, so the item does not appear to touch the pet.
            proposed = Rect.MinMaxRect(proposed.xMin - .01f, proposed.yMin - .01f,
                proposed.xMax + .01f, proposed.yMax + .01f);
            foreach (var point in PetPositions())
            {
                float distance = (point - from).sqrMagnitude;
                if (distance >= nearest || PetBounds(point).Overlaps(proposed) || !ClearPath(from, point, obstacles)) continue;
                nearest = distance;
                target = point;
            }
            return !float.IsPositiveInfinity(nearest);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingSeriousGame.UI
{
    /// <summary>One player's pet identity and choices, referencing the animal catalog by ID.</summary>
    [Serializable]
    public sealed class PetProfile
    {
        // Unity does not serialize System.Guid fields; store the identifier as text instead.
        public string uniquePlayerPetID;
        public string animalId;
        public string breedId;
        public string petName;
        public int backgroundId = 1;
    }

    public static class PetProfileRules
    {
        public const int MaxNameLength = 24;

        public static bool IsValidName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxNameLength) return false;
            foreach (char character in value)
                if (char.IsControl(character)) return false;
            return true;
        }

        // Old catalogs without breeds retain a usable default appearance.
        public static List<BreedDefinition> GetBreeds(AnimalDefinition animal)
        {
            var result = new List<BreedDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var breed in animal?.breeds ?? Array.Empty<BreedDefinition>())
                if (breed != null && !string.IsNullOrWhiteSpace(breed.id) &&
                    !string.IsNullOrWhiteSpace(breed.name) && ids.Add(breed.id)) result.Add(breed);
            if (result.Count == 0 && animal != null)
                result.Add(new BreedDefinition
                {
                    id = "default",
                    name = animal.name,
                    description = animal.description,
                    previewImageFolder = animal.previewImageFolder,
                    imageResourcePath = animal.imageResourcePath,
                    previewFramesPerSecond = animal.previewFramesPerSecond
                });
            return result;
        }
    }
}

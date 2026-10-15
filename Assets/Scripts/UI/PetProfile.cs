using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingSeriousGame.UI
{
    [Serializable]
    public sealed class PetBreedDefinition
    {
        public string id;
        public string name;
        public string description;
        public string previewImageFolder;
        public string imageResourcePath;
        public float previewFramesPerSecond = 10;
    }

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
        public static List<PetBreedDefinition> GetBreeds(PetDefinition pet)
        {
            var result = new List<PetBreedDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var breed in pet?.breeds ?? Array.Empty<PetBreedDefinition>())
                if (breed != null && !string.IsNullOrWhiteSpace(breed.id) &&
                    !string.IsNullOrWhiteSpace(breed.name) && ids.Add(breed.id)) result.Add(breed);
            if (result.Count == 0 && pet != null)
                result.Add(new PetBreedDefinition
                {
                    id = "default",
                    name = pet.name,
                    description = pet.description,
                    previewImageFolder = pet.previewImageFolder,
                    imageResourcePath = pet.imageResourcePath,
                    previewFramesPerSecond = pet.previewFramesPerSecond
                });
            return result;
        }
    }
}

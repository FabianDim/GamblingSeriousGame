using System;
using System.Collections.Generic;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.UI;

namespace GamblingSeriousGame.Pets
{
    /// <summary>One resolved player pet. The full animal catalog is not retained here.</summary>
    public sealed class SelectedPet
    {
        public string Id { get; }
        public PetAnimalType Animal { get; }
        public BreedDefinition Breed { get; }
        public PetAnimationSet Animations { get; }
        public PetSaveData SaveData { get; }

        // A snapshot of the current save data, so name/background changes cannot leave a stale profile.
        // To change animal or breed, save a profile through PetSaveStore and reload the selection.
        public PetProfile Profile => new PetProfile
        {
            uniquePlayerPetID = Id,
            animalId = Animal.Id,
            breedId = Breed.id,
            petName = SaveData.petName,
            backgroundId = SaveData.backgroundId
        };

        public SelectedPet(string id, PetSaveData saveData, AnimalDefinition animal, BreedDefinition breed)
        {
            if (!Guid.TryParse(id, out var uuid) || uuid == Guid.Empty)
                throw new ArgumentException("A selected pet requires a valid pet UUID.", nameof(id));
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));
            if (animal == null) throw new ArgumentNullException(nameof(animal));
            if (breed == null) throw new ArgumentNullException(nameof(breed));
            if (saveData.animalId != animal.id || saveData.breedId != breed.id ||
                !PetProfileRules.GetBreeds(animal).Exists(option => option.id == breed.id))
                throw new ArgumentException("The animal and breed must match the saved pet.");

            Id = uuid.ToString();
            SaveData = saveData;
            Animal = new PetAnimalType(animal);
            Breed = new BreedDefinition
            {
                id = breed.id,
                name = breed.name,
                description = breed.description,
                previewImageFolder = breed.previewImageFolder,
                imageResourcePath = breed.imageResourcePath,
                previewFramesPerSecond = breed.previewFramesPerSecond,
                animationFolder = breed.animationFolder
            };
            Animations = new PetAnimationSet(Breed.animationFolder);
        }
    }

    /// <summary>The selected animal's type and preview, without a list of breed choices.</summary>
    public sealed class PetAnimalType
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string PreviewImageFolder { get; }
        public string ImageResourcePath { get; }
        public float PreviewFramesPerSecond { get; }

        internal PetAnimalType(AnimalDefinition animal)
        {
            Id = animal.id;
            Name = animal.name;
            Description = animal.description;
            PreviewImageFolder = animal.previewImageFolder;
            ImageResourcePath = animal.imageResourcePath;
            PreviewFramesPerSecond = animal.previewFramesPerSecond;
        }
    }

    /// <summary>Playable animations for just the selected breed, loaded once per selection.</summary>
    public sealed class PetAnimationSet
    {
        public IReadOnlyList<PetAnimationDefinition> Activities { get; }
        public IReadOnlyList<PetAnimationDefinition> Sleep { get; }
        public IReadOnlyList<PetAnimationDefinition> Death { get; }
        public IReadOnlyList<PetAnimationDefinition> Reactions { get; }

        internal PetAnimationSet(string folder)
        {
            Activities = Array.AsReadOnly(PetAnimationDefinition.Load(folder));
            Sleep = Array.AsReadOnly(PetAnimationDefinition.Load(folder, PetAnimationCategory.Sleep));
            Death = Array.AsReadOnly(PetAnimationDefinition.Load(folder, PetAnimationCategory.Death));
            Reactions = Array.AsReadOnly(PetAnimationDefinition.Load(folder, PetAnimationCategory.Reaction));
        }
    }
}

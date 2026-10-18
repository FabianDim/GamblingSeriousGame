using System;

namespace GamblingSeriousGame.UI
{
    /// <summary>An animal type in the catalog, such as Cat, with its available breeds.</summary>
    [Serializable]
    public sealed class AnimalDefinition
    {
        public string id;
        public string name;
        public string description;
        public string imageResourcePath;
        public string previewImageFolder;
        public float previewFramesPerSecond = 10;
        public const string SavedAccessibilityKey = "Accessibility.Profile.v1";
        public string[] suggestedNames;
        public BreedDefinition[] breeds;
    }

    /// <summary>The animal types available for pet creation.</summary>
    [Serializable]
    public sealed class AnimalCatalog
    {
        public AnimalDefinition[] animals;
    }

    /// <summary>A breed or appearance option within an animal type.</summary>
    [Serializable]
    public sealed class BreedDefinition
    {
        public string id;
        public string name;
        public string description;
        public string previewImageFolder;
        // Resources-relative folder containing this breed's PetAnimationDefinition assets.
        public string animationFolder;
        public string imageResourcePath;
        public float previewFramesPerSecond = 10;
    }
}

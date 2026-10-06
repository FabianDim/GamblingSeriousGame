using System;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.UI;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public const string SavedProfileKey = PetCustomizationScreen.SavedProfileKey;
    private const string BackgroundCatalogPath = "Pets/PetMobileGameAsset/Backgrounds/Classic/Backgrounds";

    [Serializable]
    public sealed class BackgroundDefinition
    {
        public int id;
        public string name;
        public string imageResourcePath;
    }

    [Serializable]
    private sealed class BackgroundCatalog
    {
        public BackgroundDefinition[] backgrounds = Array.Empty<BackgroundDefinition>();
    }

    [SerializeField] private TextAsset animalJson;

    private PetDefinition pet;
    private PetBreedDefinition breed;
    public PetDefinition Pet => pet;
    public PetBreedDefinition Breed => breed;
    public string PetName { get; private set; } = string.Empty;
    public string PetUID { get; private set; } = string.Empty;
    public PetSaveData PetInfo { get; private set; }
    public int BackgroundId => PetInfo?.backgroundId ?? 1;
    public BackgroundDefinition Background { get; private set; }
    public Texture2D BackgroundImage { get; private set; }
    public bool HasPet => pet != null && breed != null;

    private void Awake() => LoadSavedPet();

    // Only confirmed companions are persisted; customization drafts stay in memory.
    public void LoadSavedPet()
    {
        pet = null;
        breed = null;
        PetName = string.Empty;
        PetUID = string.Empty;
        PetInfo = null;

        try
        {
            PetSaveStore.EnsureExists();
            var json = PlayerPrefs.GetString(SavedProfileKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            if (animalJson == null)
                animalJson = Resources.Load<TextAsset>("Pets/Animals");
            if (animalJson == null)
            {
                Debug.LogWarning("Assign Animal Json on GameStateManager to load the saved companion.", this);
                return;
            }

            var profile = JsonUtility.FromJson<PetProfile>(json);
            if (profile == null || string.IsNullOrWhiteSpace(profile.animalId) ||
                !PetProfileRules.IsValidName(profile.petName)) return;

            var catalog = JsonUtility.FromJson<PetCatalog>(animalJson.text);
            var savedPet = Array.Find(catalog?.animals ?? Array.Empty<PetDefinition>(), entry =>
                entry != null && entry.id == profile.animalId && !string.IsNullOrWhiteSpace(entry.name));
            var savedBreed = PetProfileRules.GetBreeds(savedPet).Find(entry => entry.id == profile.breedId);
            if (savedPet == null || savedBreed == null) return;

            // Migrate the previous single-profile save on first load.
            if (!Guid.TryParse(profile.uniquePlayerPetID, out var uuid) || uuid == Guid.Empty)
                profile.uniquePlayerPetID = Guid.NewGuid().ToString();
            else profile.uniquePlayerPetID = uuid.ToString();
            var pets = PetSaveStore.LoadAll();
            if (!pets.TryGetValue(profile.uniquePlayerPetID, out var info) || info == null)
                info = PetSaveStore.SaveProfile(profile);
            profile.backgroundId = info.backgroundId;
            PlayerPrefs.SetString(SavedProfileKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();

            pet = savedPet;
            breed = savedBreed;
            PetName = info.petName;
            PetUID = profile.uniquePlayerPetID;
            PetInfo = info;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not load the saved companion: " + exception.Message, this);
        }
        finally
        {
            LoadBackground();
        }
    }

    public void LoadBackground()
    {
        Background = null;
        BackgroundImage = null;
        try
        {
            var json = Resources.Load<TextAsset>(BackgroundCatalogPath);
            if (json == null)
            {
                Debug.LogWarning("Background catalog is missing: " + BackgroundCatalogPath, this);
                return;
            }

            var catalog = JsonUtility.FromJson<BackgroundCatalog>(json.text);
            var backgrounds = catalog?.backgrounds ?? Array.Empty<BackgroundDefinition>();
            var selected = Array.Find(backgrounds, entry => entry != null && entry.id == BackgroundId);
            if (TryLoadBackground(selected)) return;

            // Use the default image if a saved selection was removed or cannot be loaded.
            if (BackgroundId != 1 && TryLoadBackground(Array.Find(backgrounds, entry => entry != null && entry.id == 1)))
                return;
            Debug.LogWarning("Could not load background " + BackgroundId + " or the default background.", this);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning("Could not load the background catalog: " + exception.Message, this);
        }
    }

    private bool TryLoadBackground(BackgroundDefinition definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.imageResourcePath)) return false;
        var image = Resources.Load<Texture2D>(definition.imageResourcePath);
        if (image == null) return false;
        Background = definition;
        BackgroundImage = image;
        return true;
    }
}

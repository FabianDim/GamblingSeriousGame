using System;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.UI;
using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public const string SavedProfileKey = PetCustomizationScreen.SavedProfileKey;

    [SerializeField] private TextAsset animalJson;

    private PetDefinition pet;
    private PetBreedDefinition breed;
    public PetDefinition Pet => pet;
    public PetBreedDefinition Breed => breed;
    public string PetName { get; private set; } = string.Empty;
    public string PetUID { get; private set; } = string.Empty;
    public PetSaveData PetInfo { get; private set; }
    public int BackgroundId => PetInfo?.backgroundId ?? 1;
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
    }
}

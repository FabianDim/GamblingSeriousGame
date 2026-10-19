// Run in an isolated Unity project named SelectedPetValidation*, with Scripts and Resources copied.
// Place this file under Assets/Editor and execute SelectedPetValidation.Run.
using System;
using System.Reflection;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.Pets;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEngine;

public static class SelectedPetValidation
{
    public static void Run()
    {
        GameObject go = null;
        try
        {
            Require(Application.productName.StartsWith("SelectedPetValidation"), "Isolated saves required");
            PlayerPrefs.DeleteKey(GameStateManager.SavedProfileKey);
            go = new GameObject("Selected pet validation");
            go.SetActive(false);
            var manager = go.AddComponent<GameStateManager>();
            manager.LoadSavedPet();
            Require(manager.SelectedPet == null && !manager.HasPet, "No profile means no selected pet");

            // Migrate a PlayerPrefs-only profile without a UUID.
            var profile = new PetProfile { animalId = "cat", breedId = "black", petName = "Mochi" };
            Select(manager, profile);
            var pet = manager.SelectedPet;
            Require(manager.HasPet && Guid.TryParse(pet.Id, out _), "Legacy identity migration");
            Require(pet.Profile.animalId == "cat" && pet.Animal.Id == "cat" && pet.Breed.id == "black",
                "One consistent animal and breed");
            Require(pet.Animal.GetType().GetField("breeds") == null &&
                pet.Animal.GetType().GetProperty("Breeds") == null,
                "Selected animal does not expose catalog breed choices");
            Require(pet.Animations.Activities.Count > 1 && pet.Animations.Sleep.Count > 0 &&
                pet.Animations.Death.Count > 0, "Selected breed animations loaded");
            foreach (var action in pet.Animations.Activities)
            {
                Require(action.category == PetAnimationCategory.Activity, "No special actions in ambient pool");
                Require(AssetDatabase.GetAssetPath(action).StartsWith("Assets/Resources/Pets/Animations/cat/black/"),
                    "Only selected breed animations loaded");
            }

            pet.SaveData.health = 72;
            pet.SaveData.experience = 125;
            pet.SaveData.petName = "Cleo";
            pet.SaveData.backgroundId = 2;
            Require(pet.Profile.petName == "Cleo" && pet.Profile.backgroundId == 2, "Profile reflects live save data");
            var snapshot = pet.Profile;
            snapshot.breedId = "grey";
            Require(pet.Profile.breedId == "black", "Editing a snapshot cannot desynchronise the selection");
            PetSaveStore.SavePet(pet.Id, pet.SaveData);
            manager.LoadSavedPet();
            Require(manager.SelectedPet.Profile.petName == "Cleo" && manager.BackgroundId == 2 &&
                manager.SelectedPet.SaveData.health == 72 && manager.SelectedPet.SaveData.experience == 125,
                "Saved progression and current profile survive reload");

            // A different pet has newer type/breed details in its save than in PlayerPrefs.
            var dogProfile = new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(),
                animalId = "dog", breedId = "retriever", petName = "Biscuit" };
            PetSaveStore.SaveProfile(dogProfile);
            var stale = new PetProfile { uniquePlayerPetID = dogProfile.uniquePlayerPetID,
                animalId = "cat", breedId = "grey", petName = "Old name" };
            Select(manager, stale);
            Require(manager.SelectedPet.Animal.Id == "dog" && manager.SelectedPet.Breed.id == "retriever" &&
                manager.SelectedPet.Profile.petName == "Biscuit", "Save record resolves stale selection details");
            foreach (var action in manager.SelectedPet.Animations.Activities)
                Require(AssetDatabase.GetAssetPath(action).StartsWith("Assets/Resources/Pets/Animations/dog/retriever/"),
                    "Switching pets replaces the animation set");
            Require(PetSaveStore.LoadAll()[pet.Id].experience == 125, "Switching preserves the previous pet");

            dogProfile.breedId = "corgi";
            PetSaveStore.SaveProfile(dogProfile);
            manager.LoadSavedPet();
            Require(manager.SelectedPet.Breed.id == "corgi" && manager.SelectedPet.Animations.Activities.Count == 0 &&
                manager.SelectedPet.Animations.Death.Count == 0, "Missing art keeps the selected breed with empty animations");

            dogProfile.breedId = "missing";
            PetSaveStore.SaveProfile(dogProfile);
            manager.LoadSavedPet();
            Require(manager.SelectedPet == null && !manager.HasPet, "Invalid breed clears the entire selection");

            var legacyCatalog = new TextAsset("{\"animals\":[{\"id\":\"legacy\",\"name\":\"Legacy\"}]}");
            typeof(GameStateManager).GetField("animalJson", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, legacyCatalog);
            Select(manager, new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(),
                animalId = "legacy", breedId = "default", petName = "Pip" });
            Require(manager.SelectedPet?.Breed.id == "default", "Catalogs without breeds retain the legacy default");
            UnityEngine.Object.DestroyImmediate(legacyCatalog);

            PlayerPrefs.DeleteKey(GameStateManager.SavedProfileKey);
            manager.LoadSavedPet();
            Require(manager.SelectedPet == null, "Removing the selection cannot leave stale pet data");
            Debug.Log("SELECTED_PET_VALIDATION_PASS: migration, profile, single breed, animations, persistence, switching, missing art and invalid selection");
            UnityEngine.Object.DestroyImmediate(go);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Select(GameStateManager manager, PetProfile profile)
    {
        PlayerPrefs.SetString(GameStateManager.SavedProfileKey, JsonUtility.ToJson(profile));
        manager.LoadSavedPet();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

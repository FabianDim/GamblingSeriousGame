// Copy to Assets/Editor in an isolated Unity project with Scripts, UI, Resources and Outfit fonts.
// Use a distinct company/product name to keep all save and PlayerPrefs writes isolated.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class PetShopValidation
{
    private const string Running = "PetShopValidation.Running";
    private static UIDocument document;
    private static RenderTexture target;
    private static double next;
    private static int step;
    private static readonly Vector2Int[] Sizes = { new Vector2Int(390, 844), new Vector2Int(320, 568), new Vector2Int(844, 390) };

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            Resize(0);
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Require(Application.productName.StartsWith("PetShopValidation"), "Isolated product required");
            Directory.CreateDirectory("Validation");
            var profile = new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(), animalId = "cat", breedId = "black", petName = "Mochi" };
            var pet = PetSaveStore.SaveProfile(profile);
            var items = ShopCatalog.Items;
            Require(items.Count > 100 && items.All(item => item.Sprite != null), "Sliced shop sprites loaded");
            Require(items.Select(item => item.Id).Distinct().Count() == items.Count, "Stable unique item IDs");
            var starter = items.First(item => item.RequiredXp == 0);
            var locked = items.First(item => item.RequiredXp > 0);
            Require(!PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, locked.Id, out _), "Locked claim rejected");
            Require(!PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, "unknown", out _), "Unknown claim rejected");
            Require(PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, starter.Id, out _), "Starter claim succeeds");
            Require(!PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, starter.Id, out _), "Duplicate rejected");
            pet.experience = locked.RequiredXp;
            Require(PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, locked.Id, out _), "Exact XP threshold unlocks");
            Require(pet.experience == locked.RequiredXp, "XP never spent");
            var reloaded = PetSaveStore.LoadAll()[profile.uniquePlayerPetID];
            Require(reloaded.ownedItems.Count == 2 && reloaded.experience == pet.experience, "Collection persists");
            Require(PetSaveStore.SaveProfile(profile).ownedItems.Count == 2, "Profile edits preserve ownership");
            // A failed disk write must not grant an unsaved item.
            string saved = File.ReadAllText(PetSaveStore.SavePath);
            File.WriteAllText(PetSaveStore.SavePath, "broken json");
            var another = items.First(item => item.RequiredXp <= pet.experience && !pet.ownedItems.Contains(item.Id));
            Require(!PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, another.Id, out _), "Save failure reported");
            Require(!pet.ownedItems.Contains(another.Id), "Save failure leaves ownership unchanged");
            File.WriteAllText(PetSaveStore.SavePath, saved);
            // Existing saves have no ownedItems field.
            File.WriteAllText(PetSaveStore.SavePath, "{\"" + profile.uniquePlayerPetID + "\":{\"experience\":42}}");
            Require(PetSaveStore.LoadAll()[profile.uniquePlayerPetID].ownedItems != null, "Old saves initialize collection");
            File.WriteAllText(PetSaveStore.SavePath, saved);
            pet.experience = 125;
            PetSaveStore.SavePet(profile.uniquePlayerPetID, pet);
            PlayerPrefs.SetString(GameStateManager.SavedProfileKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("State").AddComponent<GameStateManager>();
            var go = new GameObject("Game UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Settings/AppPanelSettings.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/GameScreen.uxml");
            go.AddComponent<BasePageScreen>();
            go.AddComponent<GameplayScreen>();
            go.SetActive(true);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Validation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Resize(int index)
    {
        if (target != null) { document.panelSettings.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); }
        target = new RenderTexture(Sizes[index].x, Sizes[index].y, 24);
        target.Create();
        document.panelSettings.targetTexture = target;
        next = EditorApplication.timeSinceStartup + 2;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            var root = document.rootVisualElement;
            var background = root.Q("gameplay-background");
            if (step == 0)
            {
                Require(root.Q<Label>("gameplay-xp").text == "125 XP", "XP counter binds saved XP");
                Capture("Gameplay");
                Submit(root.Q<Button>("gameplay-menu"));
                Require(root.Q("gameplay-menu-options").style.display == DisplayStyle.Flex, "Dropdown opens");
                Submit(root.Q("gameplay-menu-options").Q<Button>());
                next = EditorApplication.timeSinceStartup + 2;
            }
            else if (step <= 3)
            {
                var shop = root.Q("pet-shop");
                var xp = root.Q("gameplay-xp");
                var health = root.Q("gameplay-health");
                Require(shop.resolvedStyle.display == DisplayStyle.Flex, "Shop open");
                Require(shop.worldBound.xMin >= background.worldBound.xMin && shop.worldBound.xMax <= background.worldBound.xMax + 1 &&
                    shop.worldBound.yMax <= background.worldBound.yMax + 1, "Shop stays inside background");
                Require(health.worldBound.xMax <= xp.worldBound.xMin + 1, "XP next to health without overlap");
                Require(root.Q<ScrollView>("shop-items").layout.height > 40, "Shop list has usable space");
                Capture("Shop");
                if (step < 3) Resize(step);
                else
                {
                    root.Q<DropdownField>("shop-category").value = "Bowls";
                    var buttons = root.Q("shop-items").Query<Button>().ToList();
                    Require(buttons.Count > 0 && buttons.All(b => b.name.Contains("BowlSprites/")), "Category filters items");
                    var claim = buttons.First(b => b.enabledSelf);
                    Submit(claim);
                    Require(claim.text == "Owned" && !claim.enabledSelf, "Collect changes to Owned");
                    Require(UnityEngine.Object.FindAnyObjectByType<GameStateManager>().PetInfo.experience == 125, "UI claim preserves XP");
                    Submit(root.Q<Button>("shop-back"));
                    Require(shop.style.display == DisplayStyle.None, "Back closes shop");
                    var screen = document.GetComponent<GameplayScreen>();
                    screen.enabled = false;
                    screen.enabled = true;
                    Require(root.Query("gameplay-scene").ToList().Count == 1, "Re-enable creates one scene");
                    Debug.Log("PET_SHOP_VALIDATION_PASS: catalog, migration, claims, persistence, failure rollback, dropdown, filters, lifecycle and three layouts");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                }
            }
            step++;
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Capture(string name)
    {
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes($"Validation/{name}-{target.width}x{target.height}.png", image.EncodeToPNG());
        UnityEngine.Object.Destroy(image);
        RenderTexture.active = null;
    }

    private static void Submit(Button button)
    {
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception ex) { Debug.LogException(ex); SessionState.SetBool(Running, false); EditorApplication.Exit(1); }
}

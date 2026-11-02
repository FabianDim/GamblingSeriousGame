// Run in an isolated project with Scripts, UI, Resources and Outfit fonts copied from the game.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.Gameplay;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class PetRoomValidation
{
    private const string Running = "PetRoomValidation.Running";
    private static UIDocument document;
    private static GameplayScreen screen;
    private static GameStateManager state;
    private static RenderTexture target;
    private static int step, sizeIndex;
    private static double next, deadline;
    private static Vector2 initial;
    private static string itemId;
    private static GamblingSeriousGame.Pets.PetAnimationDefinition restAnimation;
    private static readonly Vector2Int[] Sizes = { new Vector2Int(390, 844), new Vector2Int(320, 568), new Vector2Int(844, 390) };

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            screen = document.GetComponent<GameplayScreen>();
            state = UnityEngine.Object.FindAnyObjectByType<GameStateManager>();
            itemId = state.SelectedPet.SaveData.ownedItems[0];
            Resize(0);
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Require(Application.productName.StartsWith("PetRoomValidation"), "Isolated product required");
            Directory.CreateDirectory("Validation");
            CheckPersistence();
            var profile = new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(), animalId = "cat", breedId = "black", petName = "Mochi" };
            var pet = PetSaveStore.SaveProfile(profile);
            var starter = ShopCatalog.Items.First(item => item.RequiredXp == 0);
            Require(PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, starter.Id, out _), "Collect fixture item");
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
            go.AddComponent<PetMovement>(); // The legacy controller must not compete with room activities.
            go.SetActive(true);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Validation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void CheckPersistence()
    {
        var profile = new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(), animalId = "cat", breedId = "black", petName = "Tests" };
        var pet = PetSaveStore.SaveProfile(profile);
        var items = ShopCatalog.Items.Where(item => item.RequiredXp == 0).Take(3).ToArray();
        string id = items[0].Id;
        Require(!PetSaveStore.TryPlaceItem(profile.uniquePlayerPetID, pet, id, new Vector2(.5f, .5f), out _), "Unowned placement rejected");
        foreach (var item in items) Require(PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, item.Id, out _), "Claim fixture");
        foreach (var invalid in new[] { new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(.5f, 0), new Vector2(.5f, 1), new Vector2(float.NaN, .5f) })
            Require(!PetSaveStore.TryPlaceItem(profile.uniquePlayerPetID, pet, id, invalid, out _), "Full item bounds rejected");
        Require(PetSaveStore.TryPlaceItem(profile.uniquePlayerPetID, pet, id, new Vector2(.5f, .5f), out _), "Valid placement saved");
        Require(!PetSaveStore.TryPlaceItem(profile.uniquePlayerPetID, pet, items[1].Id, new Vector2(.5f, .5f), out _), "Overlapping items rejected");
        Require(PetSaveStore.TryPlaceItem(profile.uniquePlayerPetID, pet, id, new Vector2(.3f, .5f), out _), "Move succeeds");
        Require(pet.placedItems.Count == 1, "Move does not duplicate item");
        Require(PetSaveStore.SaveProfile(profile).placedItems.Count == 1, "Profile edits preserve room");
        var extra = ShopCatalog.Items.First(item => item.RequiredXp == 0 && !pet.ownedItems.Contains(item.Id));
        Require(PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, pet, extra.Id, out _), "Additional claim succeeds");
        Require(PetSaveStore.LoadAll()[profile.uniquePlayerPetID].placedItems.Count == 1, "Shop claim preserves room");
        string saved = File.ReadAllText(PetSaveStore.SavePath);
        File.WriteAllText(PetSaveStore.SavePath, "broken json");
        Require(!PetSaveStore.TryPlaceItem(profile.uniquePlayerPetID, pet, id, null, out _), "Failed save reports error");
        Require(pet.placedItems.Count == 1, "Failed save preserves live room");
        File.WriteAllText(PetSaveStore.SavePath, saved);
        Require(PetSaveStore.TryPlaceItem(profile.uniquePlayerPetID, pet, id, null, out _), "Store item succeeds");
        Require(pet.placedItems.Count == 0 && pet.ownedItems.Contains(id), "Store preserves ownership");
        File.WriteAllText(PetSaveStore.SavePath, "{\"" + profile.uniquePlayerPetID + "\":{\"experience\":42}}");
        Require(PetSaveStore.LoadAll()[profile.uniquePlayerPetID].placedItems != null, "Old saves initialize room");
        File.WriteAllText(PetSaveStore.SavePath, saved);
        var start = new Vector2(.5f, .81f);
        Require(RoomLayout.TryFindEscape(start, RoomLayout.ItemBounds(items[0], start), new List<Rect>(), out var escape), "Pet finds reachable escape");
        Require(!RoomLayout.PetBounds(escape).Overlaps(RoomLayout.ItemBounds(items[0], start)), "Escape clears item");
        Require(!RoomLayout.TryFindEscape(start, RoomLayout.ItemArea, new List<Rect>(), out _), "No-space placement cannot evict pet");
        Require(!RoomLayout.ClearPath(new Vector2(.26f, .8f), new Vector2(.74f, .8f), new[] { new Rect(.45f, .3f, .1f, .67f) }), "Movement cannot tunnel through furniture");
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Room validation timed out");
            var root = document.rootVisualElement;
            var room = (PetRoomView)typeof(GameplayScreen).GetField("room", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screen);
            var pet = root.Q<PetImage>("gameplay-pet");
            var background = root.Q("gameplay-background");
            switch (step)
            {
                case 0:
                    initial = pet.worldBound.center;
                    step++; Wait(1); break;
                case 1:
                    if (Vector2.Distance(initial, pet.worldBound.center) < 2) return;
                    Require(background.worldBound.Contains(pet.worldBound.min) && background.worldBound.Contains(pet.worldBound.max), "Roaming pet stays in background");
                    if (room.IsMoving) return;
                    restAnimation = screen.CurrentPetAnimation;
                    Require(restAnimation != null && restAnimation.name != "Idle" && restAnimation.name != "Running" && restAnimation.name != "Jump", "Arrival starts a stationary activity");
                    initial = pet.worldBound.center;
                    step = 10; Wait(1); break;
                case 10:
                    Require(!room.IsMoving && Vector2.Distance(initial, pet.worldBound.center) < .1f && screen.CurrentPetAnimation == restAnimation, "Activity holds position without legacy controller interruption");
                    Require(screen.ReactToPet(), "Tap reaction can interrupt a rest activity");
                    step++; Wait(.1); break;
                case 11:
                    if (screen.IsPetReacting) return;
                    Require(screen.CurrentPetAnimation == restAnimation, "Rest activity resumes after tap reaction");
                    step++; Wait(2); break;
                case 12:
                    Require(!room.IsMoving && Vector2.Distance(initial, pet.worldBound.center) < .1f && screen.CurrentPetAnimation == restAnimation, "Pet gets a longer activity break before walking again");
                    room.OpenInventory();
                    Require(root.Q<Button>("place-" + itemId) != null, "Collected item appears in inventory");
                    Submit(root.Q<Button>("place-" + itemId));
                    step = 2; Wait(.2); break;
                case 2:
                    ClickRoom(new Vector2(.01f, .5f));
                    Require(state.SelectedPet.SaveData.placedItems.Count == 0, "UI edge placement rejected");
                    var center = background.WorldToLocal(pet.worldBound.center);
                    initial = new Vector2(center.x / background.contentRect.width, center.y / background.contentRect.height);
                    ClickRoom(initial);
                    Require(room.IsMoving && state.SelectedPet.SaveData.placedItems.Count == 0, "Pet moves before item is saved");
                    Require(!screen.IsPetReacting, "Placement on pet does not trigger tap reaction");
                    step++; break;
                case 3:
                    if (room.IsPlacing) return;
                    Require(state.SelectedPet.SaveData.placedItems.Count == 1, "Item saved after pet moves");
                    var placed = root.Q<Image>("placed-" + itemId);
                    Require(!placed.worldBound.Overlaps(pet.worldBound), "Placed item does not overlap pet");
                    Require(PetSaveStore.LoadAll()[state.SelectedPet.Id].placedItems.Count == 1, "Placement persists on disk");
                    Capture("Inventory");
                    Submit(root.Q<Button>("place-" + itemId));
                    step++; Wait(.2); break;
                case 4:
                    ClickRoom(new Vector2(.5f, .46f));
                    Require(state.SelectedPet.SaveData.placedItems.Count == 1 && Math.Abs(state.SelectedPet.SaveData.placedItems[0].y - .46f) < .001f, "UI move updates one item");
                    Submit(root.Q<Button>("place-" + itemId));
                    Submit(root.Q<Button>("placement-cancel"));
                    Require(Math.Abs(state.SelectedPet.SaveData.placedItems[0].y - .46f) < .001f, "Cancel keeps original layout");
                    Submit(root.Q<Button>("store-" + itemId));
                    Require(state.SelectedPet.SaveData.placedItems.Count == 0 && state.SelectedPet.SaveData.ownedItems.Contains(itemId), "Store returns item to inventory");
                    Submit(root.Q<Button>("place-" + itemId));
                    step++; Wait(.2); break;
                case 5:
                    ClickRoom(new Vector2(.5f, .46f));
                    screen.enabled = false; screen.enabled = true;
                    Require(root.Query("gameplay-scene").ToList().Count == 1, "Re-enable has one room");
                    Require(root.Q("placed-" + itemId) != null, "Re-enable restores placed items");
                    step++; Wait(.5); break;
                case 6:
                    room.OpenInventory();
                    step++; Wait(.3); break;
                case 7:
                    var inventory = root.Q("pet-inventory");
                    Require(inventory.worldBound.xMin >= background.worldBound.xMin - 1 && inventory.worldBound.xMax <= background.worldBound.xMax + 1 && inventory.worldBound.yMax <= background.worldBound.yMax + 1, "Inventory stays in fitted background");
                    Require(root.Q<ScrollView>("inventory-items").layout.height > 40, "Inventory has usable scrolling area");
                    Capture("Inventory");
                    Submit(root.Q<Button>("place-" + itemId));
                    step++; Wait(.2); break;
                case 8:
                    Capture("Placement");
                    room.CancelPlacement();
                    Require(!room.IsPlacing, "Cancel leaves no pending placement");
                    room.OpenInventory();
                    if (sizeIndex < Sizes.Length - 1) { Resize(++sizeIndex); step = 7; }
                    else { step++; Wait(.2); }
                    break;
                case 9:
                    using (var evt = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None))
                    { evt.target = root.Q<Button>("inventory-back"); root.Q<Button>("inventory-back").SendEvent(evt); }
                    Require(!room.IsInventoryOpen, "Escape closes inventory through shop overlay");
                    Debug.Log("PET_ROOM_VALIDATION_PASS: roaming, stationary activities, reaction restoration, legacy controller coordination, bounds, collisions, pet evasion, inventory, move/store/cancel, persistence, migration, rollback, resizing and lifecycle.");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void ClickRoom(Vector2 normalized)
    {
        var background = document.rootVisualElement.Q("gameplay-background");
        var surface = document.rootVisualElement.Q("room-placement");
        var world = background.LocalToWorld(Vector2.Scale(normalized, background.contentRect.size));
        Require(document.rootVisualElement.panel.Pick(world) == surface, "Placement surface receives pointer, including over pet");
        using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = world }))
        { evt.target = surface; surface.SendEvent(evt); }
    }
    private static void Resize(int index)
    {
        if (target != null) { document.panelSettings.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); }
        target = new RenderTexture(Sizes[index].x, Sizes[index].y, 24); target.Create();
        document.panelSettings.targetTexture = target;
        Wait(1.5);
    }
    private static void Capture(string name)
    {
        RenderTexture.active = target;
        var picture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); picture.Apply();
        File.WriteAllBytes($"Validation/{name}-{target.width}x{target.height}.png", picture.EncodeToPNG());
        UnityEngine.Object.Destroy(picture); RenderTexture.active = null;
    }
    private static void Submit(Button button)
    { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
    private static void Wait(double seconds) => next = EditorApplication.timeSinceStartup + seconds;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception ex) { Debug.LogException(ex); SessionState.SetBool(Running, false); EditorApplication.Exit(1); }
}

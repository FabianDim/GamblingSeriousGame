// Run in an isolated project with UI, UI scripts/prefabs, Resources and Outfit fonts copied.
// Place under Assets/Editor; execute PetCustomizationValidation.Run in Unity batchmode.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public static class PetCustomizationValidation
{
    private const string Running = "PetCustomizationValidation.Running";
    private static PetCustomizationScreen screen;
    private static UIDocument document;
    private static RenderTexture target;
    private static int step;
    private static double nextStep;
    private static Sprite pausedFrame;
    private static int callbacks;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            EditorApplication.update += Tick;
            nextStep = EditorApplication.timeSinceStartup + 1;
        };
    }

    public static void Run()
    {
        try
        {
            PlayerSettings.companyName = "CompanionValidation";
            PlayerSettings.productName = "IsolatedCompanionValidation";
            Directory.CreateDirectory("Validation");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var selection = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PetSelectionUI.prefab"));
            var creation = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PetCreationUI.prefab"));
            creation.SetActive(false);
            var manager = new GameObject("Navigation").AddComponent<AppNavigationManager>();
            SetObject(manager, "petSelectionScreen", selection);
            SetObject(manager, "petCreationScreen", creation);
            UnityEventTools.AddPersistentListener((UnityEvent<string>)Field(typeof(PetCreationScreen), "onPetConfirmed").GetValue(selection.GetComponent<PetCreationScreen>()), manager.ConfirmPetSelection);
            UnityEventTools.AddPersistentListener((UnityEvent)Field(typeof(PetCustomizationScreen), "onBackRequested").GetValue(creation.GetComponent<PetCustomizationScreen>()), manager.BackButton);
            EditorSceneManager.SaveScene(scene, "Assets/Validation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        nextStep = EditorApplication.timeSinceStartup + 0.6;
        try
        {
            switch (step++)
            {
                case 0:
                    PlayerPrefs.DeleteKey(PetCustomizationScreen.SavedProfileKey);
                    var selection = UnityEngine.Object.FindAnyObjectByType<PetCreationScreen>();
                    var cards = selection.GetComponent<UIDocument>().rootVisualElement.Query<Button>(className: "pet-card").ToList();
                    Require(cards.Count == 3, "Three animals");
                    Require(cards[0].Q<Image>("pet-image").sprite != null, "Selection sprite sheet loads");
                    Require(cards[1].Q<Image>("pet-image").sprite == null, "Dog must not borrow cat art");
                    Submit(cards[0]);
                    Submit(selection.GetComponent<UIDocument>().rootVisualElement.Q<Button>("ConfirmPetSelection"));
                    screen = UnityEngine.Object.FindAnyObjectByType<PetCustomizationScreen>();
                    Require(screen != null && screen.SelectedPetId == "cat", "Navigation passes animal ID");
                    document = screen.GetComponent<UIDocument>();
                    Resize(390, 844);
                    break;
                case 1:
                    VerifyBehavior();
                    Submit(document.rootVisualElement.Q<Button>("toggle-animation"));
                    pausedFrame = document.rootVisualElement.Q<Image>("pet-preview").sprite;
                    break;
                case 2:
                    Require(pausedFrame == document.rootVisualElement.Q<Image>("pet-preview").sprite, "Pause holds frame");
                    VerifyLayout();
                    Capture("Validation/PetCreation-390.png");
                    Submit(document.rootVisualElement.Q<Button>("toggle-animation"));
                    VerifyLifecycleAndData();
                    Resize(320, 568);
                    break;
                case 3:
                    Capture("Validation/PetCreation-320-top.png");
                    VerifyLayout();
                    var scroll = document.rootVisualElement.Q<ScrollView>("customization-scroll");
                    Require(scroll.verticalScroller.highValue > 0, "Short display scrolls");
                    Capture("Validation/PetCreation-320-top.png");
                    scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
                    break;
                case 4:
                    var viewport = document.rootVisualElement.Q<ScrollView>("customization-scroll").contentViewport.worldBound;
                    Require(document.rootVisualElement.Q<Button>("back-to-selection").worldBound.yMax <= viewport.yMax + 2, "Back reachable by scrolling");
                    Require(document.rootVisualElement.Q<Button>("finish-pet").worldBound.yMin >= viewport.yMin - 2, "Save reachable by scrolling");
                    Capture("Validation/PetCreation-320-bottom.png");
                    Resize(844, 390);
                    break;
                case 5:
                    VerifyLayout();
                    Debug.Log("PET_CUSTOMIZATION_VALIDATION_PASSED: sprite sheets, pause, breeds, names, local save/restore, event payloads, drafts, navigation, lifecycle, malformed data and portrait/landscape layouts.");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void VerifyBehavior()
    {
        var root = document.rootVisualElement;
        var done = root.Q<Button>("finish-pet");
        var name = root.Q<TextField>("pet-name-input");
        Require(root.Q("paint-tools") == null && root.Q("draw-mode") == null, "Painting absent");
        Require(!done.enabledSelf, "Name required");
        name.value = "   ";
        Require(!done.enabledSelf, "Whitespace rejected");
        Require(!PetProfileRules.IsValidName("Bad\nName") && !PetProfileRules.IsValidName(new string('x', 25)), "Control characters and long names rejected");
        name.value = new string('x', 30);
        Require(name.maxLength == 24 && !done.enabledSelf, "Typing limit configured; oversized programmatic input rejected");
        Submit(root.Q<Button>("suggest-name"));
        Require(done.enabledSelf && name.value.Length > 0, "Suggestion supplies valid name");
        var options = root.Query<Button>(className: "breed-option").ToList();
        Require(options.Count == 3 && screen.SelectedBreedId == "shorthair", "Default breed selected");
        Require(root.Q<Image>("pet-preview").sprite != null, "Sliced cat sprite preview");
        Submit(options[1]);
        Require(screen.SelectedBreedId == "tabby" && root.Q<Image>("pet-preview").sprite == null, "Breed swaps preview without stale frame");
        Require(root.Q<Label>("preview-placeholder").style.display.value == DisplayStyle.Flex, "Missing art placeholder");
        Require(root.Query<Button>(className: "breed-option--selected").ToList().Count == 1, "Single selected breed");
        name.value = "  Mochi  ";
        ((UnityEvent<PetProfile>)Field(typeof(PetCustomizationScreen), "onProfileCreated").GetValue(screen)).AddListener(profile => {
            callbacks++;
            Require(profile.animalId == "cat" && profile.breedId == "tabby" && profile.petName == "Mochi", "Complete profile event");
        });
        int legacyCallbacks = 0;
        ((UnityEvent<string, string>)Field(typeof(PetCustomizationScreen), "onPetCreated").GetValue(screen)).AddListener((id, value) => {
            legacyCallbacks++;
            Require(id == "cat" && value == "Mochi", "Legacy listener preserved");
        });
        Submit(done);
        Submit(done);
        Require(callbacks == 1 && legacyCallbacks == 1 && !done.enabledSelf, "Save emits once, repeated submit ignored");
        var saved = JsonUtility.FromJson<PetProfile>(PlayerPrefs.GetString(PetCustomizationScreen.SavedProfileKey));
        Require(saved.breedId == "tabby" && saved.petName == "Mochi", "Local save includes breed and trimmed name");
        screen.enabled = false;
        screen.enabled = true;
        root = document.rootVisualElement;
        Require(root.Query<Button>(className: "breed-option").ToList().Count == 3, "No duplicate options after enable");
        root.Q<TextField>("pet-name-input").value = "Mochi ";
        Submit(root.Q<Button>("finish-pet"));
        Require(callbacks == 2 && legacyCallbacks == 2, "No duplicate handlers after enable");
        Submit(root.Query<Button>(className: "breed-option").First());
    }

    private static void VerifyLifecycleAndData()
    {
        Submit(document.rootVisualElement.Q<Button>("back-to-selection"));
        Require(!screen.gameObject.activeSelf, "Back returns to animals");
        UnityEngine.Object.FindAnyObjectByType<AppNavigationManager>().ConfirmPetSelection("cat");
        Require(screen.PetName == "Mochi" && screen.SelectedBreedId == "shorthair", "Back preserves draft");
        screen.SetPet("dog");
        document.rootVisualElement.Q<TextField>("pet-name-input").value = "Scout";
        screen.SetPet("cat");
        Require(screen.PetName == "Mochi", "Per-animal drafts");
        screen.SetPet("missing");
        Require(!document.rootVisualElement.Q<Button>("finish-pet").enabledSelf && screen.CurrentProfile == null, "Unknown ID fails safely");
        var original = Field(typeof(PetCustomizationScreen), "animalJson").GetValue(screen);
        Field(typeof(PetCustomizationScreen), "animalJson").SetValue(screen, new TextAsset("not json"));
        screen.SetPet("cat");
        Require(screen.SelectedPetId == null, "Malformed catalog fails safely");
        var test = new PetDefinition { id = "legacy", name = "Legacy", breeds = new[] {
            null, new PetBreedDefinition { id = "" }, new PetBreedDefinition { id = "one", name = "One" },
            new PetBreedDefinition { id = "one", name = "Duplicate" }
        } };
        Require(PetProfileRules.GetBreeds(test).Count == 1, "Invalid and duplicate breeds filtered");
        test.breeds = null;
        Require(PetProfileRules.GetBreeds(test)[0].id == "default", "Legacy catalog default breed");
        Field(typeof(PetCustomizationScreen), "animalJson").SetValue(screen, original);
        // A fresh screen restores the saved profile rather than relying on the draft dictionary.
        var restoredObject = UnityEngine.Object.Instantiate(screen.gameObject);
        var restored = restoredObject.GetComponent<PetCustomizationScreen>();
        restored.SetPet("cat");
        Require(restored.PetName == "Mochi" && restored.SelectedBreedId == "tabby", "Saved profile restores on fresh instance");
        restoredObject.SetActive(false);
        UnityEngine.Object.Destroy(restoredObject);
        screen.SetPet("cat");
    }

    private static void VerifyLayout()
    {
        var root = document.rootVisualElement;
        // Panel scaling snaps dimensions to physical pixels; allow two panel units of rounding.
        Require(root.Q("pet-viewport").layout.height >= 158, "Preview visible");
        foreach (var button in root.Query<Button>().ToList())
        {
            if (!button.ClassListContains("breed-option") && !button.ClassListContains("profile-action") &&
                !button.ClassListContains("app-button")) continue;
            Require(button.layout.height >= 43.5f, "Touch target: " + button.name + " / " + button.text + " / " + button.layout.height);
            Require(button.worldBound.xMin >= root.worldBound.xMin - 1 && button.worldBound.xMax <= root.worldBound.xMax + 1, "No horizontal overflow: " + button.name);
        }
    }

    private static void Resize(int w, int h)
    {
        if (target != null) { document.panelSettings.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); }
        target = new RenderTexture(w, h, 24); target.Create(); document.panelSettings.targetTexture = target;
    }
    private static void Submit(Button button) { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void SetObject(UnityEngine.Object obj, string field, UnityEngine.Object value) { var serialized = new SerializedObject(obj); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Capture(string path) { RenderTexture.active = target; var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = null; }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception exception) { Debug.LogException(exception); SessionState.SetBool(Running, false); EditorApplication.Exit(1); }
}

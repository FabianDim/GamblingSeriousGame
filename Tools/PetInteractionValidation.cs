// Run only in an isolated Unity project named PetInteractionValidation*.
// Copy Scripts, Resources, UI, fonts and this file (under Assets/Editor).
using System;
using System.IO;
using System.Reflection;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.Pets;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class PetInteractionValidation
{
    private const string Running = "PetInteractionValidation.Running";
    private static GameplayScreen screen;
    private static GameStateManager state;
    private static UIDocument document;
    private static PetImage image, fixture;
    private static Sprite first, second;
    private static Texture2D texture;
    private static RenderTexture target;
    private static PetAnimationDefinition idle;
    private static Vector2 oldPosition;
    private static int stage;
    private static double next, deadline;
    private static string runtimeError;
    private static PetSpritePreview preview;
    private static PetAnimationDefinition oneFrame;
    private static int completions;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (stack.Contains("UnityEditor.Search.")) return;
            if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Exception)) runtimeError = message;
        };
        EditorApplication.playModeStateChanged += change =>
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            screen = UnityEngine.Object.FindAnyObjectByType<GameplayScreen>();
            state = UnityEngine.Object.FindAnyObjectByType<GameStateManager>();
            document = screen.GetComponent<UIDocument>();
            image = document.rootVisualElement.Q<PetImage>("gameplay-pet");
            target = new RenderTexture(390, 844, 24);
            target.Create();
            document.panelSettings.targetTexture = target;
            stage = 0;
            deadline = EditorApplication.timeSinceStartup + 60;
            Wait(2);
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Require(Application.productName.StartsWith("PetInteractionValidation"), "Isolated saves required");
            Directory.CreateDirectory("Validation");
            var profile = new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(), animalId = "cat", breedId = "black", petName = "Mochi" };
            PetSaveStore.SaveProfile(profile);
            PlayerPrefs.SetString(GameStateManager.SavedProfileKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("State").AddComponent<GameStateManager>();
            var go = new GameObject("Game UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Settings/AppPanelSettings.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/GameScreen.uxml");
            go.AddComponent<BasePageScreen>();
            go.AddComponent<GameplayScreen>();
            go.SetActive(true);
            EditorSceneManager.SaveScene(scene, "Assets/PetInteractionValidation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Timed out waiting for reaction completion");
            Require(runtimeError == null, "Runtime error: " + runtimeError);
            switch (stage)
            {
                case 0:
                    CheckSelection();
                    texture = new Texture2D(8, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                    var pixels = new Color32[32];
                    pixels[1 * 8 + 1] = new Color32(255, 0, 0, 255);
                    pixels[3 * 8 + 7] = new Color32(0, 255, 0, 255);
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true); // Exercise GPU readback on unreadable imported-style art.
                    first = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
                    second = Sprite.Create(texture, new Rect(4, 0, 4, 4), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
                    fixture = new PetImage { sprite = first };
                    fixture.style.position = Position.Absolute;
                    fixture.style.width = 120;
                    fixture.style.height = 80;
                    fixture.style.left = 10;
                    fixture.style.top = 100;
                    document.rootVisualElement.Add(fixture);
                    stage++; Wait(.2); break;
                case 1:
                    Require(fixture.ContainsPoint(new Vector2(50, 50)), "Opaque first-frame pixel is clickable");
                    Require(!fixture.ContainsPoint(new Vector2(30, 10)), "Transparent pixels do not count");
                    Require(!fixture.ContainsPoint(new Vector2(10, 40)), "ScaleToFit letterbox does not count");
                    oldPosition = fixture.LocalToWorld(new Vector2(50, 50));
                    Require(document.rootVisualElement.panel.Pick(oldPosition) == fixture, "Panel picks actual opaque pixel");
                    fixture.sprite = second;
                    Require(!fixture.ContainsPoint(new Vector2(50, 50)), "Hit area updates when frame changes");
                    Require(fixture.ContainsPoint(new Vector2(90, 10)), "Sprite-sheet offset maps to second-frame alpha");
                    fixture.style.translate = new Translate(150, 40);
                    fixture.style.scale = new Scale(new Vector3(.8f, 1.2f, 1));
                    stage++; Wait(.2); break;
                case 2:
                    Require(document.rootVisualElement.panel.Pick(oldPosition) != fixture, "Old screen position no longer hits moved pet");
                    Require(document.rootVisualElement.panel.Pick(fixture.LocalToWorld(new Vector2(90, 10))) == fixture, "Hit follows translation and scaling");
                    fixture.RemoveFromHierarchy(); fixture.Dispose();
                    idle = Find(state.SelectedPet.Animations.Activities, "Idle");
                    Require(screen.PlayPetAnimation(idle), "Start an ambient activity");
                    var transparent = FindPoint(image, false);
                    Click(image, transparent);
                    Require(!screen.IsPetReacting, "Transparent click does not trigger reaction");
                    state.SelectedPet.SaveData.health = 100;
                    Click(image, FindPoint(image, true));
                    Require(screen.IsPetReacting && screen.CurrentPetAnimation.name == "Happy", "Opaque tap triggers happy at high health");
                    Require(!screen.ReactToPet() && !screen.PlayPetAnimation(idle), "Repeated taps and ambient requests cannot interrupt reaction");
                    stage++; Wait(.2); break;
                case 3:
                    Capture("Pet-Happy");
                    stage++; break;
                case 4:
                    if (screen.IsPetReacting) return;
                    Require(screen.CurrentPetAnimation == idle, "Ambient playback restored after reaction");
                    Require(Find(state.SelectedPet.Animations.Activities, "Happy").loop, "Shared looping asset not changed");
                    state.SelectedPet.SaveData.health = 50;
                    Click(image, FindPoint(image, true));
                    Require(screen.CurrentPetAnimation.name == "Surprised", "Medium health chooses surprised");
                    stage++; Wait(.2); break;
                case 5:
                    Capture("Pet-Surprised"); stage++; break;
                case 6:
                    if (screen.IsPetReacting) return;
                    state.SelectedPet.SaveData.health = 10;
                    Click(image, FindPoint(image, true));
                    Require(screen.CurrentPetAnimation.name == "Crying", "Low health chooses sad animation");
                    stage++; Wait(.2); break;
                case 7:
                    Capture("Pet-Sad"); stage++; break;
                case 8:
                    if (screen.IsPetReacting) return;
                    Require(screen.CurrentPetAnimation == idle && !screen.IsPetReacting, "Sad reaction completes and restores activity");
                    state.SelectedPet.SaveData.health = 50;
                    screen.ReactToPet();
                    screen.gameObject.SetActive(false);
                    Require(!screen.IsPetReacting, "Disabling clears active reaction");
                    stage++; Wait(2); break;
                case 9:
                    screen.gameObject.SetActive(true);
                    image = document.rootVisualElement.Q<PetImage>("gameplay-pet");
                    stage++; Wait(.2); break;
                case 10:
                    Require(document.rootVisualElement.Query<PetImage>().ToList().Count == 1, "Re-enable has one pet target");
                    // Keep this breed's preview but omit animation assets to exercise mood-only feedback.
                    var animal = JsonUtility.FromJson<AnimalCatalog>(Resources.Load<TextAsset>("Pets/Animals").text).animals[0];
                    var breed = animal.breeds[0]; breed.animationFolder = "";
                    var pet = new SelectedPet(Guid.NewGuid().ToString(), new PetSaveData { animalId = animal.id, breedId = breed.id, health = 10 }, animal, breed);
                    typeof(GameStateManager).GetField("<SelectedPet>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, pet);
                    Require(screen.ReactToPet(), "Missing animation still responds");
                    Require(document.rootVisualElement.Q<Label>("pet-reaction").text == "Feeling low", "Fallback conveys low-health mood");
                    stage++; Wait(1.5); break;
                case 11:
                    Require(!screen.IsPetReacting && image.sprite != null, "Fallback finishes and retains the pet preview");
                    var holder = new Image();
                    document.rootVisualElement.Add(holder);
                    preview = new PetSpritePreview(holder);
                    oneFrame = ScriptableObject.CreateInstance<PetAnimationDefinition>();
                    oneFrame.texture = texture; oneFrame.frameRects = new[] { new Rect(0, 0, 4, 4) }; oneFrame.loop = true; oneFrame.framesPerSecond = 10;
                    preview.Bind(oneFrame, false, () => completions++);
                    stage++; Wait(.4); break;
                case 12:
                    Require(completions == 1 && oneFrame.loop, "Single-frame override completes once without modifying definition");
                    preview.Bind(oneFrame, false, () => completions++);
                    preview.Dispose();
                    stage++; Wait(.3); break;
                case 13:
                    Require(completions == 1, "Disposal cancels pending completion callbacks");
                    Debug.Log("PET_INTERACTION_VALIDATION_PASS: frame-alpha hit testing, unreadable textures, moving/scaled picking, health thresholds, one-shot reactions, activity protection/restoration, missing-art fallback and lifecycle.");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void CheckSelection()
    {
        Require(PetReactions.GetMood(34, 100) == PetMood.Sad, "Below 35% is sad");
        Require(PetReactions.GetMood(35, 100) == PetMood.Surprised, "35% boundary");
        Require(PetReactions.GetMood(69, 100) == PetMood.Surprised, "Below 70% is surprised");
        Require(PetReactions.GetMood(70, 100) == PetMood.Happy, "70% boundary");
        Require(PetReactions.GetMood(50, 200) == PetMood.Sad, "Uses maximum health, not raw value");
        Require(PetReactions.GetMood(-1, 0) == PetMood.Sad, "Invalid health stays bounded");
        var catalog = JsonUtility.FromJson<AnimalCatalog>(Resources.Load<TextAsset>("Pets/Animals").text);
        foreach (var animal in catalog.animals)
        foreach (var breed in animal.breeds)
        {
            var pet = new SelectedPet(Guid.NewGuid().ToString(), new PetSaveData { animalId = animal.id, breedId = breed.id }, animal, breed);
            var sad = PetReactions.Select(pet.Animations, PetMood.Sad);
            if (animal.id == "cat") Require(sad != null && sad.name == "Crying", "Each cat breed has sad reaction");
            if (breed.id == "retriever") Require(sad != null && sad.name == "Hurt", "Retriever uses own sad fallback");
        }
    }

    private static PetAnimationDefinition Find(System.Collections.Generic.IReadOnlyList<PetAnimationDefinition> entries, string name)
    { foreach (var entry in entries) if (entry.name == name) return entry; throw new Exception("Missing " + name); }
    private static Vector2 FindPoint(PetImage pet, bool opaque)
    { for (float y = 1; y < pet.contentRect.height; y += 2) for (float x = 1; x < pet.contentRect.width; x += 2) if (pet.ContainsPoint(new Vector2(x, y)) == opaque) return new Vector2(x, y); throw new Exception("No suitable pixel"); }
    private static void Click(PetImage pet, Vector2 local)
    { using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = pet.LocalToWorld(local) })) { evt.target = pet; pet.SendEvent(evt); } }
    private static void Capture(string name)
    {
        RenderTexture.active = target;
        var picture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); picture.Apply();
        File.WriteAllBytes("Validation/" + name + ".png", picture.EncodeToPNG());
        UnityEngine.Object.Destroy(picture); RenderTexture.active = null;
    }
    private static void Wait(double seconds) => next = EditorApplication.timeSinceStartup + seconds;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Fail(Exception ex) { SessionState.SetBool(Running, false); Debug.LogException(ex); EditorApplication.Exit(1); }
}

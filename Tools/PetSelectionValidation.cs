// Copy into an isolated project's Assets/Editor alongside this project's UI assets,
// PetCreationScreen.cs and PetCreationUI.prefab. Run with -executeMethod PetSelectionValidation.Run.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public static class PetSelectionValidation
{
    private const string Running = "PetSelectionValidation.Running";
    private static int frames;
    private static double nextStep;
    private static UIDocument document;
    private static RenderTexture target;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            Resize(390, 844);
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Validation");
            Directory.CreateDirectory("Assets/Resources/Pets");
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.green, Color.green, Color.green, Color.green });
            texture.Apply();
            File.WriteAllBytes("Assets/Resources/Pets/Test.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PetCreationUI.prefab"));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Validation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Resize(int width, int height)
    {
        target = new RenderTexture(width, height, 24);
        target.Create();
        document.panelSettings.targetTexture = target;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        nextStep = EditorApplication.timeSinceStartup + 0.02;
        try
        {
            frames++;
            if (frames == 40)
            {
                VerifyBehavior();
                Resize(390, 844);
            }
            if (frames == 70)
            {
                VerifyLayout();
                Capture("Validation/PetSelection-390.png");
                var animals = Enumerable.Range(0, 12).Select(i => new PetDefinition {
                    id = "pet-" + i, name = "Companion " + (i + 1),
                    description = "A friendly companion with a description that wraps onto multiple lines."
                }).ToArray();
                CheckJson(document.GetComponent<PetCreationScreen>(), JsonUtility.ToJson(new PetCatalog { animals = animals }), 12);
                Submit(document.rootVisualElement.Query<Button>(className: "pet-card").First());
                Resize(320, 568);
            }
            if (frames == 100)
            {
                VerifyLayout();
                var scroll = document.rootVisualElement.Q<ScrollView>("page-scroll");
                Require(scroll.verticalScroller.highValue > 0, "Short screen should scroll");
                scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
            }
            if (frames == 120)
            {
                var scroll = document.rootVisualElement.Q<ScrollView>("page-scroll");
                var confirm = document.rootVisualElement.Q<Button>("ConfirmPetSelection");
                Require(confirm.worldBound.yMax <= scroll.contentViewport.worldBound.yMax + 2, "Confirm is reachable by scrolling");
                Capture("Validation/PetSelection-320.png");
                Debug.Log("PET_SELECTION_VALIDATION_PASSED: JSON population, selection, confirmation, reload, lifecycle, invalid data, images, layout and scrolling.");
                SessionState.SetBool(Running, false);
                EditorApplication.Exit(0);
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void VerifyBehavior()
    {
        var screen = document.GetComponent<PetCreationScreen>();
        var root = document.rootVisualElement;
        var cards = root.Query<Button>(className: "pet-card").ToList();
        var confirm = root.Q<Button>("ConfirmPetSelection");
        Require(cards.Count == 3, "One card per example animal");
        Require(cards[0].Q<Label>("pet-name").text == "Cat", "Name bound from JSON");
        Require(cards[0].Q<Label>("pet-description").text.Length > 20, "Description bound from JSON");
        Require(!confirm.enabledSelf && screen.SelectedPet == null, "Confirm starts disabled");
        Submit(cards[0]);
        Require(screen.SelectedPet.id == "cat" && confirm.enabledSelf, "Selecting enables confirmation");
        Submit(cards[1]);
        Require(screen.SelectedPet.id == "dog", "Can change selection");
        Require(!cards[0].ClassListContains("pet-card--selected") && cards[1].ClassListContains("pet-card--selected"), "Only one selected card");
        string confirmed = null;
        int count = 0;
        ((UnityEvent<string>)Field("onPetConfirmed").GetValue(screen)).AddListener(id => { confirmed = id; count++; });
        Submit(confirm);
        Require(confirmed == "dog" && count == 1, "Confirmation emits selected ID once");
        screen.enabled = false;
        screen.enabled = true;
        Require(root.Query<Button>(className: "pet-card").ToList().Count == 3 && !confirm.enabledSelf, "Re-enable repopulates without duplicates");
        Submit(root.Query<Button>(className: "pet-card").First());
        Submit(confirm);
        Require(count == 2, "Re-enable does not duplicate confirmation callbacks");

        var originalJson = Field("animalJson").GetValue(screen);
        CheckJson(screen, "{\"animals\":[]}", 0);
        Require(!confirm.enabledSelf, "Empty list disables confirmation");
        CheckJson(screen, "not json", 0);
        Require(root.Q<Label>("pet-list-message") != null, "Invalid JSON shows message");
        CheckJson(screen, "{\"animals\":[null,{\"id\":\"x\"},{\"id\":\"cat\",\"name\":\"Cat\",\"imageResourcePath\":\"Pets/Test\"},{\"id\":\"cat\",\"name\":\"Duplicate\"}]}", 1);
        Require(root.Q<Image>("pet-image").image != null, "Texture loads from Resources");
        CheckJson(screen, "{\"animals\":[{\"id\":\"cat\",\"name\":\"Cat\",\"imageResourcePath\":\"Missing\"}]}", 1);
        Require(root.Q<Label>("pet-image-placeholder").style.display.value == DisplayStyle.Flex, "Missing image retains placeholder");
        Field("animalJson").SetValue(screen, originalJson);
        screen.ReloadPets();
        Submit(root.Query<Button>(className: "pet-card").First());
    }

    private static void CheckJson(PetCreationScreen screen, string json, int expected)
    {
        Field("animalJson").SetValue(screen, new TextAsset(json));
        screen.ReloadPets();
        Require(document.rootVisualElement.Query<Button>(className: "pet-card").ToList().Count == expected, "JSON card count: " + expected);
    }

    private static void VerifyLayout()
    {
        var root = document.rootVisualElement;
        foreach (var card in root.Query<Button>(className: "pet-card").ToList())
        {
            Require(card.layout.height >= 108, "Card height fits image and padding");
            Require(card.worldBound.xMax <= root.worldBound.xMax + 1, "Cards fit screen width");
            Require(card.Q<Label>("pet-description").layout.height > 0, "Description has visible height");
        }
    }

    private static FieldInfo Field(string name) => typeof(PetCreationScreen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Submit(Button button)
    {
        using (var evt = NavigationSubmitEvent.GetPooled())
        {
            evt.target = button;
            button.SendEvent(evt);
        }
    }
    private static void Capture(string path)
    {
        RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        RenderTexture.active = null;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception exception)
    {
        Debug.LogException(exception);
        SessionState.SetBool(Running, false);
        EditorApplication.Exit(1);
    }
}

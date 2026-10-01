// Run only in an isolated project containing copies of the UI assets and scripts.
// Put this file in Assets/Editor and launch with -executeMethod PetCustomizationValidation.Run.
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
    private static UIDocument document;
    private static PetCustomizationScreen screen;
    private static RenderTexture target;
    private static int step;
    private static double nextStep;

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
            Directory.CreateDirectory("Assets/Resources/Pets");
            Directory.CreateDirectory("Validation");
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                bool head = (new Vector2(x, y) - new Vector2(64, 59)).sqrMagnitude < 45 * 45;
                bool ears = y > 76 && y < 118 && ((x > 21 && x < 50 && x - 21 < (118 - y) * 0.7f) || (x > 78 && x < 107 && 107 - x < (118 - y) * 0.7f));
                bool eyes = (new Vector2(x, y) - new Vector2(47, 64)).sqrMagnitude < 4 * 4 || (new Vector2(x, y) - new Vector2(81, 64)).sqrMagnitude < 4 * 4;
                pixels[y * 128 + x] = head || ears ? (eyes ? new Color(0.1f, 0.14f, 0.2f) : new Color(0.86f, 0.81f, 0.72f)) : Color.clear;
            }
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes("Assets/Resources/Pets/ValidationPet.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            File.WriteAllText("Assets/ValidationAnimals.json", "{\"animals\":[{\"id\":\"cat\",\"name\":\"Cat\",\"description\":\"Test pet\",\"imageResourcePath\":\"Pets/ValidationPet\"}]}");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var selection = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PetSelectionUI.prefab"));
            var creation = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PetCreationUI.prefab"));
            creation.SetActive(false);
            var manager = new GameObject("Navigation Manager").AddComponent<AppNavigationManager>();
            SetObject(manager, "petSelectionScreen", selection);
            SetObject(manager, "petCreationScreen", creation);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ValidationAnimals.json");
            SetObject(selection.GetComponent<PetCreationScreen>(), "animalJson", json);
            SetObject(creation.GetComponent<PetCustomizationScreen>(), "animalJson", json);
            UnityEventTools.AddPersistentListener((UnityEvent<string>)Field(typeof(PetCreationScreen), "onPetConfirmed").GetValue(selection.GetComponent<PetCreationScreen>()), manager.ConfirmPetSelection);
            UnityEventTools.AddPersistentListener((UnityEvent)Field(typeof(PetCustomizationScreen), "onBackRequested").GetValue(creation.GetComponent<PetCustomizationScreen>()), manager.BackToPetSelection);
            EditorSceneManager.SaveScene(scene, "Assets/CustomizationValidation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextStep) return;
        nextStep = EditorApplication.timeSinceStartup + 0.7;
        try
        {
            switch (step++)
            {
                case 0:
                    var selection = UnityEngine.Object.FindAnyObjectByType<PetCreationScreen>();
                    Submit(selection.GetComponent<UIDocument>().rootVisualElement.Query<Button>(className: "pet-card").First());
                    Submit(selection.GetComponent<UIDocument>().rootVisualElement.Q<Button>("ConfirmPetSelection"));
                    screen = UnityEngine.Object.FindAnyObjectByType<PetCustomizationScreen>();
                    Require(screen != null && screen.SelectedPetId == "cat", "Navigation passes selected pet");
                    document = screen.GetComponent<UIDocument>();
                    Resize(390, 844);
                    break;
                case 1:
                    VerifyPaintLayer();
                    VerifyUI();
                    break;
                case 2:
                    Capture("Validation/PetCreation-390.png");
                    Resize(320, 568);
                    break;
                case 3:
                    VerifyLayout();
                    var scroll = document.rootVisualElement.Q<ScrollView>("customization-scroll");
                    Require(scroll.verticalScroller.highValue > 0, "Small screen scrolls");
                    scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
                    break;
                case 4:
                    Capture("Validation/PetCreation-320.png");
                    var root = document.rootVisualElement;
                    var view = root.Q<ScrollView>("customization-scroll");
                    Require(root.Q<Button>("back-to-selection").worldBound.yMax <= view.contentViewport.worldBound.yMax + 2, "Footer is reachable");
                    VerifyLifecycle();
                    Debug.Log("PET_CUSTOMIZATION_VALIDATION_PASSED: navigation, naming, painting, alpha clipping, erasing, undo, clear, pan/zoom, export, lifecycle, and mobile layout.");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void VerifyPaintLayer()
    {
        var source = Resources.Load<Texture2D>("Pets/ValidationPet");
        Require(!source.isReadable, "Non-readable imported image supported");
        using (var layer = new PetPaintTexture(source, new Rect(0, 0, 1, 1)))
        {
            var original = layer.Texture.GetPixels32();
            layer.BeginStroke();
            layer.PaintLine(Vector2.zero, new Vector2(127, 127), 5, new Color32(255, 0, 0, 255), false);
            layer.EndStroke();
            Require(layer.Texture.GetPixel(64, 64).r > 0.95f, "Paint appears");
            Require(layer.Texture.GetPixel(0, 0).a == 0, "Paint clips to silhouette");
            Require(layer.CanUndo, "Stroke creates undo");
            layer.Undo();
            Require(original.SequenceEqual(layer.Texture.GetPixels32()), "Undo restores original");
            layer.BeginStroke(); layer.PaintLine(new Vector2(64, 64), new Vector2(64, 64), 5, Color.red, false); layer.EndStroke();
            layer.BeginStroke(); layer.PaintLine(new Vector2(64, 64), new Vector2(64, 64), 5, Color.red, true); layer.EndStroke();
            Require(original.SequenceEqual(layer.Texture.GetPixels32()), "Eraser restores base image");
            layer.Undo(); layer.Clear();
            Require(original.SequenceEqual(layer.Texture.GetPixels32()), "Clear removes drawing");
            layer.Undo();
            Require(layer.Texture.GetPixel(64, 64).r > 0.95f, "Clear can be undone");
            Require(layer.ExportPng().Length > 0, "PNG export works");
        }
    }

    private static void VerifyUI()
    {
        var root = document.rootVisualElement;
        Require(screen.PetTexture != null, "Preview loaded");
        var done = root.Q<Button>("finish-pet");
        Require(!done.enabledSelf, "Name required");
        root.Q<TextField>("pet-name-input").value = "   ";
        Require(!done.enabledSelf, "Whitespace name rejected");
        root.Q<TextField>("pet-name-input").value = "  Mochi  ";
        string receivedId = null, receivedName = null;
        ((UnityEvent<string, string>)Field(typeof(PetCustomizationScreen), "onPetCreated").GetValue(screen)).AddListener((id, name) => { receivedId = id; receivedName = name; });
        Submit(done);
        Require(receivedId == "cat" && receivedName == "Mochi", "Completion passes ID and trimmed name");
        var viewport = root.Q("pet-viewport");
        Submit(root.Q<Button>("draw-mode"));
        var before = screen.PetTexture.GetPixels32();
        var centre = viewport.worldBound.center;
        Pointer(viewport, EventType.MouseDown, centre - new Vector2(20, 0));
        Pointer(viewport, EventType.MouseDrag, centre + new Vector2(20, 0));
        Pointer(viewport, EventType.MouseUp, centre + new Vector2(20, 0));
        Require(!before.SequenceEqual(screen.PetTexture.GetPixels32()), "Pointer drag paints");
        Require(root.Q<Button>("undo-paint").enabledSelf, "Undo enabled after drag");
        Submit(root.Q<Button>("undo-paint"));
        Require(before.SequenceEqual(screen.PetTexture.GetPixels32()), "UI undo works");
        Submit(root.Q<Button>("zoom-in"));
        Submit(root.Q<Button>("zoom-in"));
        Require((float)Field(typeof(PetCustomizationScreen), "zoom").GetValue(screen) > 1, "Zoom controls work");
        Submit(root.Q<Button>("inspect-mode"));
        Pointer(viewport, EventType.MouseDown, centre);
        Pointer(viewport, EventType.MouseDrag, centre + new Vector2(15, 0));
        Pointer(viewport, EventType.MouseUp, centre + new Vector2(15, 0));
        Require(((Vector2)Field(typeof(PetCustomizationScreen), "pan").GetValue(screen)).x > 0, "Inspect pans");
        Require(before.SequenceEqual(screen.PetTexture.GetPixels32()), "Inspect does not paint");
        Submit(root.Q<Button>("reset-view"));
        TouchPointer(viewport, TouchPhase.Began, 0, centre - new Vector2(30, 0));
        TouchPointer(viewport, TouchPhase.Began, 1, centre + new Vector2(30, 0));
        TouchPointer(viewport, TouchPhase.Moved, 1, centre + new Vector2(75, 0));
        Require((float)Field(typeof(PetCustomizationScreen), "zoom").GetValue(screen) > 1.5f, "Two-finger pinch zooms");
        TouchPointer(viewport, TouchPhase.Ended, 0, centre - new Vector2(30, 0));
        TouchPointer(viewport, TouchPhase.Ended, 1, centre + new Vector2(75, 0));
        Submit(root.Q<Button>("draw-mode"));
        var imageRect = (Rect)Field(typeof(PetCustomizationScreen), "imageRect").GetValue(screen);
        var paintPosition = viewport.LocalToWorld(imageRect.position + imageRect.size * 0.5f);
        TouchPointer(viewport, TouchPhase.Began, 0, paintPosition);
        TouchPointer(viewport, TouchPhase.Moved, 0, paintPosition + new Vector2(10, 0));
        TouchPointer(viewport, TouchPhase.Canceled, 0, paintPosition + new Vector2(10, 0));
        Require(screen.PetTexture.GetPixel(64, 64).g > 0.85f, "Touch painting stays aligned after zoom");
        Require((int)Field(typeof(PetCustomizationScreen), "drawingPointer").GetValue(screen) == -1, "Touch cancellation ends stroke");
        Submit(root.Q<Button>("undo-paint"));
        Require(before.SequenceEqual(screen.PetTexture.GetPixels32()), "Canceled stroke remains undoable");
        Submit(root.Q<Button>("reset-view"));
        Submit(root.Q<Button>("draw-mode"));
        Pointer(viewport, EventType.MouseDown, centre - new Vector2(35, 20));
        Pointer(viewport, EventType.MouseDrag, centre + new Vector2(35, 25));
        Pointer(viewport, EventType.MouseUp, centre + new Vector2(35, 25));
        VerifyLayout();
    }

    private static void VerifyLifecycle()
    {
        var texture = screen.PetTexture;
        Submit(document.rootVisualElement.Q<Button>("back-to-selection"));
        Require(!screen.gameObject.activeSelf, "Back hides customization");
        UnityEngine.Object.FindAnyObjectByType<AppNavigationManager>().ConfirmPetSelection("cat");
        Require(screen.PetTexture == texture && screen.PetName == "Mochi", "Reopening same pet keeps edits");
        screen.SetPet("missing");
        Require(!document.rootVisualElement.Q<Button>("finish-pet").enabledSelf && screen.PetTexture == null, "Unknown pet handled");
    }

    private static void VerifyLayout()
    {
        var root = document.rootVisualElement;
        Require(root.Q("pet-viewport").layout.height >= 280, "Large preview retained");
        foreach (var button in root.Query<Button>().ToList())
            Require(button.worldBound.xMax <= root.worldBound.xMax + 1, "Buttons fit horizontally: " + button.name);
    }
    private static void Resize(int w, int h) { target = new RenderTexture(w, h, 24); target.Create(); document.panelSettings.targetTexture = target; }
    private static void Pointer(VisualElement targetElement, EventType type, Vector2 position)
    {
        var systemEvent = new Event { type = type, mousePosition = position, button = 0 };
        EventBase evt = type == EventType.MouseDown ? (EventBase)PointerDownEvent.GetPooled(systemEvent) :
            type == EventType.MouseUp ? PointerUpEvent.GetPooled(systemEvent) : PointerMoveEvent.GetPooled(systemEvent);
        using (evt) { evt.target = targetElement; targetElement.SendEvent(evt); }
    }
    private static void Submit(Button button) { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
    private static void TouchPointer(VisualElement targetElement, TouchPhase phase, int finger, Vector2 position)
    {
        var touch = new Touch { fingerId = finger, phase = phase, position = position, tapCount = 1, pressure = 1, maximumPossiblePressure = 1 };
        EventBase evt = phase == TouchPhase.Began ? (EventBase)PointerDownEvent.GetPooled(touch) :
            phase == TouchPhase.Ended ? PointerUpEvent.GetPooled(touch) :
            phase == TouchPhase.Canceled ? PointerCancelEvent.GetPooled(touch) : PointerMoveEvent.GetPooled(touch);
        using (evt) { evt.target = targetElement; targetElement.SendEvent(evt); }
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void SetObject(UnityEngine.Object targetObject, string field, UnityEngine.Object value) { var serialized = new SerializedObject(targetObject); serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Capture(string path) { RenderTexture.active = target; var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image); RenderTexture.active = null; }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception exception) { Debug.LogException(exception); SessionState.SetBool(Running, false); EditorApplication.Exit(1); }
}

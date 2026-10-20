// Copy into Assets/Editor of an isolated Unity project, then execute ResourcesValidation.Run.
// Uses fake outbound callbacks: validation never opens websites or contacts support services.
using System;
using System.IO;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class ResourcesValidation
{
    private const string Running = "ResourcesValidation.Running";
    private static UIDocument document;
    private static RenderTexture target;
    private static int stage;
    private static double next;
    private static string runtimeError;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            // Unity's background Editor search index can fail on a fresh batch-mode project.
            if (stack.Contains("UnityEditor.Search.")) return;
            if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Exception))
                runtimeError = message;
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            document = UnityEngine.Object.FindAnyObjectByType<ResourcesScreen>().GetComponent<UIDocument>();
            stage = 0;
            Resize(390, 844);
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Validation");
            EditorSceneManager.OpenScene("Assets/Scenes/PetCreationUIToolkit.unity");
            var shipped = new SerializedObject(UnityEngine.Object.FindAnyObjectByType<AppNavigationManager>());
            var wired = shipped.FindProperty("resourcesScreen").objectReferenceValue as GameObject;
            Require(wired != null && wired.GetComponent<ResourcesScreen>() != null, "Resources prefab wired in shipped scene");
            Require(!wired.activeSelf, "Resources does not obscure startup screens");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var resources = Prefab("ResourcesScreen");
            var home = Prefab("BasePageUI");
            home.SetActive(false);
            var navObject = new GameObject("Navigation");
            navObject.SetActive(false);
            var nav = navObject.AddComponent<AppNavigationManager>();
            Set(nav, "homeScreen", home);
            Set(nav, "resourcesScreen", resources);
            navObject.SetActive(true);
            EditorSceneManager.SaveScene(scene, "Assets/ResourcesValidation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void ValidateCatalogAndActions()
    {
        var text = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Support/AustralianGamblingResources.json");
        var catalog = JsonUtility.FromJson<SupportResourceCatalog>(text.text);
        var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Elements/ResourcesContent.uxml");
        var root = template.Instantiate();
        document.rootVisualElement.Add(root);
        string called = null, opened = null;
        using (var view = new ResourcesView(root, catalog, url => opened = url, phone => called = phone))
        {
            Require(root.Q("resources-list").childCount == catalog.services.Length - 1, "All catalog services rendered");
            Submit(root.Q<Button>("resources-call-help"));
            Require(called == "1800 858 858", "Helpline callback uses verified number");
            Submit(root.Q<Button>("resources-chat"));
            Require(opened == catalog.services[0].url, "Chat callback uses official website");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var service in catalog.services)
            {
                Require(ids.Add(service.id), "Unique service id");
                Require(Uri.TryCreate(service.url, UriKind.Absolute, out var uri) && uri.Scheme == "https", "HTTPS service URL");
                if (service.id == "gambling-help") continue;
                Submit(root.Q<Button>("visit-" + service.id));
                Require(opened == service.url, "Correct website for " + service.id);
                if (!string.IsNullOrEmpty(service.phone))
                {
                    Require(System.Text.RegularExpressions.Regex.IsMatch(service.phone.Replace(" ", ""), @"^\d{6,10}$"), "Valid dial string");
                    Submit(root.Q<Button>("call-" + service.id));
                    Require(called == service.phone, "Correct phone for " + service.id);
                }
            }
            view.SelectCategory("Local");
            Require(root.Q("resources-list").childCount == 8, "All eight states and territories");
            foreach (var state in new[] { "nsw", "vic", "qld", "sa", "wa", "tas", "act", "nt" })
                Require(root.Q("resource-" + state) != null, "Local coverage: " + state);
            view.SelectCategory("Money");
            Require(root.Q("resources-list").childCount == 1 && root.Q("resource-national-debt") != null, "Money filter");
            view.SelectCategory("Tools");
            Require(root.Q("resources-list").childCount == 1 && root.Q("resource-betstop") != null, "Tools filter");
            view.SelectCategory("All");
            Require(root.Q("resources-list").childCount == catalog.services.Length - 1, "Filter reset");
        }
        root.RemoveFromHierarchy();
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            Require(runtimeError == null, "Runtime error: " + runtimeError);
            var root = document.rootVisualElement;
            CheckLayout();
            switch (stage++)
            {
                case 0:
                    ValidateCatalogAndActions();
                    Capture("Resources-390x844");
                    Submit(root.Q<Button>("resources-filter-local"));
                    root.Q<ScrollView>("base-page-scroll").ScrollTo(root.Q("resource-nsw"));
                    Wait();
                    break;
                case 1:
                    Require(root.Q("resources-list").childCount == 8, "Live filter click");
                    Capture("Resources-Local-390x844");
                    Resize(320, 568);
                    root.Q<ScrollView>("base-page-scroll").scrollOffset = Vector2.zero;
                    break;
                case 2:
                    Capture("Resources-320x568");
                    root.Q<ScrollView>("base-page-scroll").ScrollTo(root.Q("visit-nt"));
                    Wait();
                    break;
                case 3:
                    Require(root.Q("visit-nt").worldBound.yMax <= root.Q("bottom-dock").worldBound.yMin + 2, "Last local service reachable on small screen");
                    Capture("Resources-End-320x568");
                    Resize(844, 390);
                    root.Q<ScrollView>("base-page-scroll").scrollOffset = Vector2.zero;
                    break;
                case 4:
                    Capture("Resources-844x390");
                    var nav = UnityEngine.Object.FindAnyObjectByType<AppNavigationManager>();
                    var resources = document.gameObject;
                    resources.GetComponent<BasePageScreen>().Navigate("home");
                    Require(!resources.activeSelf, "Home navigation hides resources");
                    var home = (GameObject)new SerializedObject(nav).FindProperty("homeScreen").objectReferenceValue;
                    home.GetComponent<BasePageScreen>().Navigate("resources");
                    Require(resources.activeSelf && !home.activeSelf, "Resources dock returns to page");
                    Require(document.rootVisualElement.Query<VisualElement>("resources-content").ToList().Count == 1, "Re-enabling does not duplicate content");
                    Wait();
                    break;
                case 5:
                    Debug.Log("RESOURCES_VALIDATION_PASS: scene wiring, 16 services, phone and website callbacks, filters, navigation, lifecycle, 3 screen sizes and scroll reachability.");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void CheckLayout()
    {
        var root = document.rootVisualElement;
        var dock = root.Q("bottom-dock");
        Require(dock.layout.height >= 70, "Dock stays visible");
        Require(root.Q<ScrollView>("base-page-scroll").worldBound.yMax <= dock.worldBound.yMin + 1, "Content scrolls above dock");
        foreach (var button in root.Query<Button>().ToList())
            if (button.layout.height > 0) Require(button.layout.height >= 43.5f, "44px touch target: " + button.name + " (" + button.layout.height + ")");
        foreach (var label in root.Query<Label>().ToList())
            if (label.worldBound.width > 0)
                Require(label.worldBound.xMin >= root.worldBound.xMin - 1 && label.worldBound.xMax <= root.worldBound.xMax + 1, "Text fits: " + label.text);
    }

    private static GameObject Prefab(string name) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab"));
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value) { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Submit(Button button) { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
    private static void Resize(int width, int height)
    {
        if (target != null) { document.panelSettings.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); }
        target = new RenderTexture(width, height, 24);
        target.Create();
        document.panelSettings.targetTexture = target;
        Wait();
    }
    private static void Capture(string name)
    {
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes("Validation/" + name + ".png", image.EncodeToPNG());
        UnityEngine.Object.Destroy(image);
        RenderTexture.active = null;
    }
    private static void Wait() => next = EditorApplication.timeSinceStartup + 2;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception ex) { SessionState.SetBool(Running, false); Debug.LogException(ex); EditorApplication.Exit(1); }
}

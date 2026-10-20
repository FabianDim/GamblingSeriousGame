// Run in an isolated Unity project with Scripts, Resources, UI, prefabs, scenes and fonts copied.
// Place under Assets/Editor; execute HomeScreenValidation.Run without -nographics to render screenshots.
using System;
using System.IO;
using System.Reflection;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class HomeScreenValidation
{
    private const string Running = "HomeScreenValidation.Running";
    private static HomeScreen home;
    private static UIDocument document;
    private static RenderTexture target;
    private static int stage;
    private static double next;
    private static string runtimeError;

    private sealed class Provider : IScreenTimeUsageProvider
    {
        public event Action Changed;
        public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
        public UsageSnapshot Snapshot = ScreenTimeExamples.Create(UsagePeriod.Today);
        public bool Throw;
        public int Refreshes;
        public UsagePeriod LastPeriod;
        public UsageSnapshot GetSnapshot(UsagePeriod period)
        {
            LastPeriod = period;
            if (Throw) throw new InvalidOperationException("Test provider failure");
            return Snapshot;
        }
        public void Refresh() { Refreshes++; Changed?.Invoke(); }
        public void Publish() => Changed?.Invoke();
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Running, false) && (type == LogType.Error || type == LogType.Exception))
                runtimeError = message;
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            home = UnityEngine.Object.FindAnyObjectByType<HomeScreen>();
            if (home == null) { Fail(new Exception("Saved pet did not start on Home")); return; }
            document = home.GetComponent<UIDocument>();
            stage = 0;
            Resize(390, 844);
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Require(Application.productName.StartsWith("HomeScreenValidation"), "Isolated saves required");
            Directory.CreateDirectory("Validation");
            // Verify the shipped scene's new prefab reference before using a minimal render fixture.
            EditorSceneManager.OpenScene("Assets/Scenes/PetCreationUIToolkit.unity");
            var existingNav = UnityEngine.Object.FindAnyObjectByType<AppNavigationManager>();
            var homeObject = (GameObject)Field(existingNav, "homeScreen").GetValue(existingNav);
            Require(homeObject != null && homeObject.GetComponent<HomeScreen>() != null, "Home is wired into the shipped scene");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var homeGo = Prefab("HomeScreen");
            var gameGo = Prefab("GameScreen");
            var interventionGo = Prefab("InterventionsScreen");
            var nav = new GameObject("Navigation").AddComponent<AppNavigationManager>();
            Set(nav, "homeScreen", homeGo);
            Set(nav, "GamePlayScreen", gameGo);
            Set(nav, "interventionsScreen", interventionGo);
            var state = new GameObject("State").AddComponent<GameStateManager>();
            Set(state, "navigationManager", nav);
            var profile = new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(),
                animalId = "cat", breedId = "black", petName = "Mochi" };
            PetSaveStore.SaveProfile(profile);
            PlayerPrefs.SetString(GameStateManager.SavedProfileKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
            EditorSceneManager.SaveScene(scene, "Assets/HomeValidation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static GameObject Prefab(string name)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab");
        Require(source != null, name + " prefab imported");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        go.SetActive(false);
        return go;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            Require(runtimeError == null, "Unexpected runtime error: " + runtimeError);
            var root = document.rootVisualElement;
            CheckLayout();
            switch (stage++)
            {
                case 0:
                    Require(root.Q<Label>("usage-total").text == "—", "Disconnected data is unknown, not zero");
                    Require(root.Q<Label>("home-pet-name").text == "Mochi", "Selected pet appears on home");
                    Capture("Home-Disconnected-390x844");
                    CheckProvider();
                    Submit(root.Q<Button>("usage-example"));
                    Require(home.ShowingExample && root.Q<Label>("usage-badge").text == "Example data", "Examples are explicitly labelled");
                    Require(root.Q<Label>("usage-total").text == "27 min", "Today totals equal per-app entries");
                    Wait();
                    break;
                case 1:
                    Capture("Home-Today-390x844");
                    Submit(root.Q<Button>("usage-week"));
                    Require(home.Period == UsagePeriod.LastSevenDays && root.Q<Label>("usage-total").text == "3 hr 3 min", "Period toggle changes totals");
                    Require(root.Q("usage-chart").childCount == 7, "Seven-day chart");
                    Wait();
                    break;
                case 2:
                    Capture("Home-Week-390x844");
                    Resize(320, 568);
                    break;
                case 3:
                    Capture("Home-Week-320x568");
                    root.Q<ScrollView>("base-page-scroll").ScrollTo(root.Q("home-open-interventions"));
                    Wait();
                    break;
                case 4:
                    Require(root.Q<Button>("home-open-interventions").worldBound.yMax <= root.Q("bottom-dock").worldBound.yMin + 2,
                        "Small-screen actions are reachable by scrolling");
                    Capture("Home-Actions-320x568");
                    Resize(844, 390);
                    break;
                case 5:
                    root.Q<ScrollView>("base-page-scroll").ScrollTo(root.Q("home-open-interventions"));
                    Wait();
                    break;
                case 6:
                    Require(root.Q<Button>("home-open-interventions").worldBound.yMax <= root.Q("bottom-dock").worldBound.yMin + 2,
                        "Landscape actions remain reachable");
                    Capture("Home-Actions-844x390");
                    CheckNavigation();
                    Require(runtimeError == null, "Navigation generated an error: " + runtimeError);
                    Debug.Log("HOME_SCREEN_VALIDATION_PASS: scene wiring, startup, navigation, usage states, example periods, provider updates/lifecycle, safe text and three sizes");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception exception) { Fail(exception); }
    }

    private static void CheckProvider()
    {
        var provider = new Provider();
        home.SetUsageProvider(provider);
        Require(provider.Subscribers == 1 && !home.ShowingExample, "Provider subscribes once");
        var root = document.rootVisualElement;
        Submit(root.Q<Button>("usage-refresh"));
        Require(provider.Refreshes == 1, "Refresh delegates to provider");
        foreach (var status in new[] { UsageStatus.NotConnected, UsageStatus.Loading, UsageStatus.PermissionRequired, UsageStatus.Unavailable, UsageStatus.Error })
        {
            provider.Snapshot.status = status;
            provider.Publish();
            Require(root.Q<Label>("usage-total").text == "—" && root.Q("usage-apps").childCount == 0, "Non-ready states clear stale usage");
        }
        provider.Snapshot = new UsageSnapshot { status = UsageStatus.Ready };
        provider.Publish();
        Require(root.Q<Label>("usage-total").text == "0 min", "Confirmed empty usage displays zero");
        provider.Snapshot.apps = new[]
        {
            new AppUsageEntry { appId = "a", displayName = "<b>Literal app</b>", foregroundSeconds = 90 },
            new AppUsageEntry { appId = "a", displayName = "Duplicate", foregroundSeconds = 90 },
            new AppUsageEntry { appId = "b", displayName = "Negative", foregroundSeconds = -20 }
        };
        provider.Publish();
        Require(root.Q<Label>("usage-total").text == "1 min" && root.Q("usage-apps").childCount == 2, "Duplicate/negative data handled");
        Require(!root.Q<Label>(className: "usage-app-name").enableRichText, "Provider text stays literal");
        home.SelectPeriod(UsagePeriod.LastSevenDays);
        Require(provider.LastPeriod == UsagePeriod.LastSevenDays, "Provider receives selected period");
        provider.Throw = true;
        provider.Publish();
        Require(root.Q<Label>("usage-badge").text == "Try again", "Provider exceptions have a recoverable state");
        provider.Throw = false;
        home.enabled = false;
        Require(provider.Subscribers == 0, "Disabled screens unsubscribe");
        home.enabled = true;
        Require(provider.Subscribers == 1 && root.Query("home-content").ToList().Count == 1, "Re-enable does not duplicate content or listeners");
        home.SetUsageProvider(null);
        Require(provider.Subscribers == 0, "Provider replacement detaches old service");
        home.SelectPeriod(UsagePeriod.Today);
    }

    private static void CheckNavigation()
    {
        var nav = UnityEngine.Object.FindAnyObjectByType<AppNavigationManager>();
        var state = UnityEngine.Object.FindAnyObjectByType<GameStateManager>();
        state.LoadSavedPet();
        Require(home.gameObject.activeSelf, "Reloading pet does not navigate away from Home");
        Submit(document.rootVisualElement.Q<Button>("home-open-pet"));
        var game = (GameObject)Field(nav, "GamePlayScreen").GetValue(nav);
        Require(game.activeSelf && !home.gameObject.activeSelf, "My Pet link opens gameplay");
        game.GetComponent<BasePageScreen>().Navigate("home");
        Require(home.gameObject.activeSelf && !game.activeSelf, "Home dock returns from gameplay");
        Submit(document.rootVisualElement.Q<Button>("home-open-interventions"));
        var interventions = (GameObject)Field(nav, "interventionsScreen").GetValue(nav);
        Require(interventions.activeSelf && !home.gameObject.activeSelf, "Pause link opens interventions");
        interventions.GetComponent<BasePageScreen>().Navigate("home");
        Require(home.gameObject.activeSelf && !interventions.activeSelf, "Home dock returns from interventions");
    }

    private static void CheckLayout()
    {
        var root = document.rootVisualElement;
        var dock = root.Q("bottom-dock");
        var scroll = root.Q<ScrollView>("base-page-scroll");
        Require(dock.layout.height >= 70 && scroll.worldBound.yMax <= dock.worldBound.yMin + 1, "Dock remains visible outside scrolling content");
        foreach (var button in root.Query<Button>().ToList())
            if (button.resolvedStyle.display != DisplayStyle.None && button.layout.height > 0)
                Require(button.layout.height >= 43.5f, "Touch target: " + button.name);
        foreach (var item in root.Query<Label>().ToList())
            if (item.worldBound.width > 0 && item.resolvedStyle.display != DisplayStyle.None)
                Require(item.worldBound.xMin >= root.worldBound.xMin - 1 && item.worldBound.xMax <= root.worldBound.xMax + 1,
                    "Text stays within screen width: " + item.name);
    }
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
    private static void Submit(Button button) { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception exception)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Debug.LogException(exception);
        EditorApplication.Exit(1);
    }
}

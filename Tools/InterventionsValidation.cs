// Run in an isolated Unity project with Scripts, UI, Resources and Outfit fonts.
using System;
using System.IO;
using System.Reflection;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class InterventionsValidation
{
    private const string Running = "InterventionsValidation.Running";
    private static UIDocument doc;
    private static RenderTexture target;
    private static double next;
    private static int step;
    private static bool checkingGrounding;
    private static int groundingChecks;
    private static string firstGroundingTitle;
    private static bool runtimeError;
    private static bool checkingActivities;
    private static int activityChecks;
    private static InterventionActivity[] activities;
    private static readonly System.Collections.Generic.HashSet<string> visited = new System.Collections.Generic.HashSet<string>();
    private static readonly Vector2Int[] Sizes = { new Vector2Int(390, 844), new Vector2Int(320, 568), new Vector2Int(844, 390) };

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            Application.logMessageReceived += (message, trace, type) =>
            {
                // Unity's cached editor search index can fail during batch startup;
                // it is not part of the running game or this UI validation.
                if (trace.Contains("UnityEditor.Search.SearchDatabase")) return;
                if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) runtimeError = true;
            };
            doc = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            Submit(doc.rootVisualElement.Q<Button>("dock-interventions"));
            doc = UnityEngine.Object.FindAnyObjectByType<InterventionsScreen>().GetComponent<UIDocument>();
            Resize(0);
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Require(Application.productName.StartsWith("PetShopValidation"), "Use isolated validation project");
            CheckGroundingData();
            InterventionRewardsValidation.Check();
            var parent = new VisualElement();
            var view = new InterventionsView(parent);
            Require(view.Root.Query(className: "intervention-card").ToList().Count == 5, "Five JSON activities");
            foreach (string invalid in new[] { "{", "{}", "{\"activities\":[]}", "null" })
            {
                view.LoadJson(invalid);
                Require(view.Root.Q(className: "interventions-empty") != null, "Invalid/empty content fallback");
            }
            view.LoadJson("{\"activities\":[{\"id\":\"one\",\"title\":\"<b>Literal</b>\",\"description\":\"Example\"},{\"id\":\"one\",\"title\":\"Duplicate\",\"description\":\"Example\"},null]}");
            Require(view.Root.Query(className: "intervention-card").ToList().Count == 1, "Duplicate and null records skipped");
            Require(!view.Root.Q<Label>(className: "intervention-title").enableRichText, "Literal JSON text");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gameState = new GameObject("State").AddComponent<GameStateManager>();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameScreen.prefab"));
            doc = go.GetComponent<UIDocument>();
            go.AddComponent<GameplayScreen>();
            var interventions = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InterventionsScreen.prefab"));
            Require(interventions.GetComponent<UIDocument>() != doc, "Independent UIDocuments");
            var navigation = new GameObject("Navigation");
            navigation.SetActive(false);
            var manager = navigation.AddComponent<AppNavigationManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("GamePlayScreen").objectReferenceValue = go;
            serialized.FindProperty("interventionsScreen").objectReferenceValue = interventions;
            var creation = new GameObject("Creation (validation placeholder)");
            creation.SetActive(false);
            serialized.FindProperty("petCreationScreen").objectReferenceValue = creation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var stateSerialized = new SerializedObject(gameState);
            var stateNavigation = stateSerialized.FindProperty("navigationManager");
            if (stateNavigation != null) stateNavigation.objectReferenceValue = manager;
            stateSerialized.ApplyModifiedPropertiesWithoutUndo();
            go.SetActive(true);
            navigation.SetActive(true);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/InterventionsValidation.unity");
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Resize(int index)
    {
        if (target != null) { doc.panelSettings.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); }
        target = new RenderTexture(Sizes[index].x, Sizes[index].y, 24);
        target.Create();
        doc.panelSettings.targetTexture = target;
        next = EditorApplication.timeSinceStartup + 2;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            var root = doc.rootVisualElement;
            var scroll = root.Q<ScrollView>("base-page-scroll");
            var page = doc.GetComponent<BasePageScreen>();
            if (checkingActivities)
            {
                CheckActivityLayout(root);
                return;
            }
            if (checkingGrounding)
            {
                CheckGroundingLayout(root);
                return;
            }
            Require(page.SelectedDestination == "interventions", "Heart selects interventions");
            Require(root.Q<Label>("base-page-title").text == "Interventions" && root.Q("base-page-title").resolvedStyle.display == DisplayStyle.Flex, "Visible title");
            Require(root.Q("gameplay-scene") == null && UnityEngine.Object.FindAnyObjectByType<GameplayScreen>() == null, "Gameplay is a separate inactive screen");
            Require(root.Query(className: "intervention-card").ToList().Count == 5, "All activities rendered");
            Require(scroll.verticalScroller.highValue > 0, "Activities scroll");
            Require(scroll.worldBound.yMax <= root.Q("bottom-dock").worldBound.yMin + 1, "Dock outside scroll area");
            Require(root.Q<Foldout>() == null, "Cards contain summaries only");
            {
                foreach (var card in root.Query(className: "intervention-card").ToList())
                    Require(card.worldBound.xMin >= 0 && card.worldBound.xMax <= root.worldBound.xMax + 1, "Cards fit screen width");
                Directory.CreateDirectory("Validation");
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes($"Validation/Interventions-{target.width}x{target.height}.png", image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
                RenderTexture.active = null;
                var entry = root.Q<Button>("open-grounding");
                Require(entry.text == "I feel like gambling" && entry.layout.height >= 80, "Large grounding entry button");
                Submit(entry);
                checkingGrounding = true;
                groundingChecks = 0;
                visited.Clear();
                firstGroundingTitle = root.Q<Label>("grounding-title").text;
                next = EditorApplication.timeSinceStartup + 0.5;
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void CheckGroundingLayout(VisualElement root)
    {
        var grounding = root.Q("grounding-screen");
        var scroll = root.Q<ScrollView>("grounding-scroll");
        var back = root.Q<Button>("grounding-back");
        var previous = root.Q<Button>("grounding-previous");
        var nextButton = root.Q<Button>("grounding-next");
        Require(root.Q("page-shell").resolvedStyle.display == DisplayStyle.None, "Activity list and dock hidden");
        Require(grounding.resolvedStyle.display == DisplayStyle.Flex && grounding.Q("bottom-dock") == null, "Focused grounding page without dock");
        Require(scroll.worldBound.yMin >= back.worldBound.yMax - 1 && scroll.worldBound.yMax <= root.Q("grounding-footer").worldBound.yMin + 1, "Back and navigation stay outside scrolling steps");
        foreach (var button in new[] { back, previous, nextButton })
            Require(button.worldBound.xMin >= -1 && button.worldBound.xMax <= root.worldBound.xMax + 1 &&
                button.worldBound.yMin >= -1 && button.worldBound.yMax <= root.worldBound.yMax + 1 &&
                button.layout.height >= 48 && button.layout.width >= 48, "Visible accessible grounding buttons: " + button.name);
        foreach (var label in root.Q("grounding-steps").Query<Label>().ToList())
            Require(label.worldBound.xMin >= -1 && label.worldBound.xMax <= root.worldBound.xMax + 1, "Steps fit width");
        Require(scroll.scrollOffset.y < 1, "Changing exercises resets scroll");
        Require(root.Q(className: "grounding-step-text").worldBound.yMin < scroll.worldBound.yMax - 15, "First step visible without scrolling");
        Require(visited.Add(root.Q<Label>("grounding-title").text), "Each exercise appears once before wrapping");
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes($"Validation/Grounding-{target.width}x{target.height}-{groundingChecks}.png", image.EncodeToPNG());
        UnityEngine.Object.Destroy(image);
        RenderTexture.active = null;
        scroll.scrollOffset = new Vector2(0, 100);
        Submit(nextButton);
        groundingChecks++;
        if (groundingChecks < 5)
        {
            next = EditorApplication.timeSinceStartup + 0.5;
            return;
        }
        Require(root.Q<Label>("grounding-title").text == firstGroundingTitle, "Next wraps through all five exercises");
        Submit(previous);
        Require(root.Q<Label>("grounding-title").text != firstGroundingTitle, "Previous changes exercise");
        Submit(nextButton);
        Require(root.Q<Label>("grounding-title").text == firstGroundingTitle, "Previous and next are reversible");
        Submit(back);
        Require(grounding.style.display.value == DisplayStyle.None && root.Q("page-shell").style.display.value != DisplayStyle.None, "Back restores list and dock");
        Require(root.focusController.focusedElement == root.Q<Button>("open-grounding"), "Back restores entry focus");
        checkingGrounding = false;
        activities = JsonUtility.FromJson<InterventionCatalog>(Resources.Load<TextAsset>(InterventionsView.ResourcePath).text).activities;
        activityChecks = 0;
        checkingActivities = true;
        Submit(root.Q<Button>("start-" + activities[0].id));
        next = EditorApplication.timeSinceStartup + 0.5;
    }

    private static void CheckActivityLayout(VisualElement root)
    {
        var activity = activities[activityChecks];
        var controller = doc.GetComponent<InterventionsScreen>();
        var detail = (InterventionDetailView)typeof(InterventionsScreen).GetField("detail", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        var collect = root.Q<Button>("activity-collect");
        var back = root.Q<Button>("activity-back");
        var scroll = root.Q<ScrollView>("activity-scroll");
        Require(detail.IsOpen && root.Q<Label>("activity-title").text == activity.title, "Each card opens its instructions");
        Require(root.Q("page-shell").resolvedStyle.display == DisplayStyle.None, "Activity hides dock and list");
        Require(root.Q("activity-steps").childCount == activity.steps.Length, "All numbered steps rendered");
        Require(scroll.horizontalScrollerVisibility == ScrollerVisibility.Hidden && scroll.verticalScrollerVisibility == ScrollerVisibility.Hidden, "Scrollbars hidden");
        Require(scroll.worldBound.yMin >= back.worldBound.yMax - 1 && scroll.worldBound.yMax <= root.Q("activity-footer").worldBound.yMin + 1, "Back and collection stay outside scroll");
        foreach (var button in new[] { back, collect })
            Require(button.layout.height >= 48 && button.worldBound.xMin >= -1 && button.worldBound.xMax <= root.worldBound.xMax + 1 && button.worldBound.yMax <= root.worldBound.yMax + 1, "Activity controls fit");
        foreach (var label in root.Q("activity-steps").Query<Label>().ToList())
            Require(label.worldBound.xMin >= -1 && label.worldBound.xMax <= root.worldBound.xMax + 1, "Instructions fit width");
        Require(!collect.enabledSelf, "Timer initially locks collection");
        Capture($"Activity-{activity.id}-{target.width}x{target.height}");
        var session = detail.Session;
        detail.Advance(20, true);
        double elapsed = session.ElapsedSeconds;
        Submit(back);
        detail.Advance(1000, true);
        Require(session.ElapsedSeconds == elapsed && !detail.IsOpen, "Back pauses practice");
        Require(root.focusController.focusedElement == root.Q<Button>("start-" + activity.id), "Back focuses originating activity");
        Submit(root.Q<Button>("start-" + activity.id));
        Require(detail.Session == session, "Reopening resumes same session");
        detail.Advance(session.RemainingSeconds, true);
        Require(collect.enabledSelf, "Timed button unlocks");
        var state = UnityEngine.Object.FindAnyObjectByType<GameStateManager>();
        int before = state.SelectedPet.SaveData.experience;
        Submit(collect);
        Require(session.Claimed && state.SelectedPet.SaveData.experience == before + activity.rewardXp && !collect.enabledSelf, "Collect grants exactly one reward");
        Require(PetSaveStore.LoadAll()[state.SelectedPet.Id].experience == state.SelectedPet.SaveData.experience, "Reward saved to disk");
        Submit(collect);
        Require(state.SelectedPet.SaveData.experience == before + activity.rewardXp, "Rapid repeat collection gives no extra XP");
        Submit(root.Q<Button>("activity-restart"));
        Require(detail.Session != session && detail.Session.ElapsedSeconds == 0 && !collect.enabledSelf, "Practise again requires a fresh timer");
        Submit(back);
        activityChecks++;
        if (activityChecks < activities.Length)
        {
            Submit(root.Q<Button>("start-" + activities[activityChecks].id));
            next = EditorApplication.timeSinceStartup + 0.5;
            return;
        }
        checkingActivities = false;
        FinishSize(root);
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

    private static void FinishSize(VisualElement root)
    {
        Submit(root.Q<Button>("dock-my-pet"));
        Require(!doc.gameObject.activeSelf, "Interventions screen deactivated");
        var gameplay = UnityEngine.Object.FindAnyObjectByType<GameplayScreen>();
        Require(gameplay != null && gameplay.GetComponent<BasePageScreen>().SelectedDestination == "my-pet", "Return to game screen");
        Submit(gameplay.GetComponent<UIDocument>().rootVisualElement.Q<Button>("dock-interventions"));
        root = doc.rootVisualElement;
        Require(root.Query("interventions-screen").ToList().Count == 1, "No duplicate screen");
        if (step < 2) Resize(step + 1);
        else
        {
            var controller = doc.GetComponent<InterventionsScreen>();
            controller.enabled = false;
            controller.enabled = true;
            Require(!runtimeError, "No runtime errors during grounding and dock navigation");
            Require(root.Query("interventions-screen").ToList().Count == 1, "Lifecycle restores one view");
            Require(root.Query("grounding-screen").ToList().Count == 1, "No duplicate grounding screen after re-enable");
            Submit(root.Q<Button>("open-grounding"));
            controller.enabled = false;
            Require(root.Q("page-shell").style.display.value != DisplayStyle.None && root.Q("grounding-screen") == null, "Disabling during grounding restores shell");
            controller.enabled = true;
            Debug.Log("INTERVENTIONS_VALIDATION_PASS: grounding, all five activity pages, timers, pause/resume, XP collection, replay/repeat, hidden scrollbars, navigation, three sizes");
            SessionState.SetBool(Running, false);
            EditorApplication.Exit(0);
        }
        step++;
    }

    private static void CheckGroundingData()
    {
        var view = new GroundingView(new VisualElement(), () => { });
        Require(view.ExerciseCount == 5, "Five grounding exercises");
        var indices = new System.Collections.Generic.HashSet<int>();
        var randomState = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(42);
            for (int i = 0; i < 100; i++)
            {
                view.OpenRandom();
                Require(view.CurrentIndex >= 0 && view.CurrentIndex < 5, "Random index in range");
                indices.Add(view.CurrentIndex);
            }
        }
        finally { UnityEngine.Random.state = randomState; }
        Require(indices.Count == 5, "Random entry can reach every exercise");
        foreach (string json in new[] { "{", "{}", "null", "{\"exercises\":[]}" })
        {
            view.LoadJson(json);
            Require(view.ExerciseCount == 1 && view.Root.Q("grounding-steps").childCount == 3, "Usable fallback exercise");
            Require(!view.Root.Q<Button>("grounding-next").enabledSelf && !view.Root.Q<Button>("grounding-previous").enabledSelf, "Single exercise navigation disabled");
        }
        view.LoadJson("{\"exercises\":[null,{\"id\":\"bad\",\"title\":\"Bad\",\"steps\":[\"\"]},{\"id\":\"a\",\"title\":\"<b>Literal</b>\",\"steps\":[\"One\"]},{\"id\":\"a\",\"title\":\"Duplicate\",\"steps\":[\"Two\"]}]}");
        Require(view.ExerciseCount == 1 && view.Root.Q<Label>("grounding-title").text == "<b>Literal</b>" && !view.Root.Q<Label>("grounding-title").enableRichText, "Invalid/duplicate steps filtered; literal text");
    }

    private static void Submit(Button button)
    {
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception ex) { Debug.LogException(ex); SessionState.SetBool(Running, false); EditorApplication.Exit(1); }
}


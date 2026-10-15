// Copy into Assets/Editor of an isolated project with UI assets/scripts and Outfit fonts.
using System;
using System.IO;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class BasePageValidation
{
    private const string Running = "BasePageValidation.Running";
    private static UIDocument document;
    private static RenderTexture target;
    private static double next;
    private static int sizeIndex;
    private static readonly Vector2Int[] Sizes = { new Vector2Int(390, 844), new Vector2Int(320, 568), new Vector2Int(844, 390) };

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
            Resize();
            EditorApplication.update += Tick;
        };
    }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Assets/Prefabs/UI");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Validation");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Base Page UI");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Settings/AppPanelSettings.asset");
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/BasePage.uxml");
            Require(doc.panelSettings != null && doc.visualTreeAsset != null, "Page assets imported");
            go.AddComponent<BasePageScreen>();
            go.SetActive(true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, "Assets/Prefabs/UI/BasePageUI.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/BasePageUIToolkit.unity");
            AssetDatabase.SaveAssets();
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Resize()
    {
        if (target != null) { document.panelSettings.targetTexture = null; target.Release(); UnityEngine.Object.Destroy(target); }
        var size = Sizes[sizeIndex];
        target = new RenderTexture(size.x, size.y, 24);
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
            var page = document.GetComponent<BasePageScreen>();
            var dock = root.Q("bottom-dock");
            var content = root.Q<ScrollView>("base-page-scroll");
            Require(root.Query<DockIcon>().ToList().Count == 5, "Five vector icons");
            Require(dock.layout.height >= 70, "Dock stays visible");
            var background = root.Q("base-page").resolvedStyle.backgroundColor;
            Require(Vector4.Distance(background, (Color)new Color32(11, 14, 23, 255)) < 0.01f, "Existing app background");
            var primary = root.Q<Button>("dock-interventions");
            Require(primary.ClassListContains("button--primary") &&
                Vector4.Distance(primary.resolvedStyle.backgroundColor, (Color)new Color32(0, 230, 118, 255)) < 0.01f,
                "Interventions uses the shared green primary button");
            Require(content.worldBound.yMax <= dock.worldBound.yMin + 1, "Content does not overlap dock");
            foreach (var button in dock.Query<Button>().ToList())
            {
                // Panel scaling rounds the 48-unit size to physical pixels.
                Require(button.layout.width >= 47.5f && button.layout.height >= 47.5f,
                    "48-unit touch targets: " + button.name + " " + button.layout.size);
                Require(button.worldBound.xMin >= root.worldBound.xMin && button.worldBound.xMax <= root.worldBound.xMax, "Dock within screen width");
            }

            int calls = 0;
            string selected = null;
            UnityEngine.Events.UnityAction<string> listener = value => { calls++; selected = value; };
            page.OnNavigate.AddListener(listener);
            var destinations = new[] { "home", "my-pet", "interventions", "settings", "resources" };
            for (int i = 0; i < destinations.Length; i++)
            {
                var button = root.Q<Button>("dock-" + destinations[i]);
                Submit(button);
                Require(calls == i + 1 && selected == destinations[i] && page.SelectedDestination == destinations[i], "Dock event and selection");
                Require(root.Q<Label>("base-page-title").text == button.tooltip, "Destination title matches label");
                Require(root.Query<Button>(className: "dock-button--selected").ToList().Count == 1, "Exactly one active destination");
            }
            page.enabled = false;
            page.enabled = true;
            Submit(root.Q<Button>("dock-home"));
            Require(calls == 6 && page.SelectedDestination == "home", "Re-enable does not duplicate callbacks");
            page.Navigate("unknown");
            Require(calls == 6, "Unknown destination ignored");
            page.OnNavigate.RemoveListener(listener);

            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes("Validation/BasePage-" + target.width + "x" + target.height + ".png", image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
            RenderTexture.active = null;
            if (++sizeIndex < Sizes.Length) { Resize(); return; }
            Debug.Log("BASE_PAGE_VALIDATION_PASS: navigation, lifecycle, icons and three screen sizes");
            SessionState.SetBool(Running, false);
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Submit(Button button)
    {
        using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Fail(Exception ex) { Debug.LogException(ex); SessionState.SetBool(Running, false); EditorApplication.Exit(1); }
}

// Copied into an isolated Unity project's Assets/Editor for migration verification.
// Not part of the game's runtime or its editor assembly.
using System;
using System.IO;
using System.Linq;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Events;

public static class UIToolkitValidation
{
    private const string Running = "UIToolkitMigration.Validation";
    private static int frames;
    private static int sizeIndex;
    private static RenderTexture target;
    private static UIDocument document;
    private static readonly Vector2Int[] Sizes = {
        new Vector2Int(390, 844), new Vector2Int(320, 568),
        new Vector2Int(844, 390), new Vector2Int(1080, 1920)
    };

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            {
                document = UnityEngine.Object.FindAnyObjectByType<UIDocument>();
                SetTarget();
                EditorApplication.update += Tick;
            }
        };
    }

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Assets/UI/Settings");
            Directory.CreateDirectory("Assets/Prefabs/UI");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Validation");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "AppPanelSettings";
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/Themes/AppTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(390, 844);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 10;
            AssetDatabase.CreateAsset(panel, "Assets/UI/Settings/AppPanelSettings.asset");

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(11, 14, 23, 255);

            var go = new GameObject("Pet Creation UI");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/PetCreation.uxml");
            go.AddComponent<PetCreationScreen>();
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/Themes/AppTheme.tss");
            Debug.Log("UXML assets: " + string.Join(", ", AssetDatabase.LoadAllAssetsAtPath("Assets/UI/Screens/PetCreation.uxml").Select(a => a.GetType().FullName)));
            Require(doc.visualTreeAsset != null, "Missing UXML import");
            Require(panel.themeStyleSheet != null, "Missing TSS import");
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, "Assets/Prefabs/UI/PetCreationUI.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/PetCreationUIToolkit.unity");
            AssetDatabase.SaveAssets();

            // Import and instantiate every reusable element and the style guide as well.
            foreach (var id in AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { "Assets/UI" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath(id));
                Require(asset.CloneTree().childCount > 0, "Empty UXML: " + asset.name);
            }
            SessionState.SetBool(Running, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void SetTarget()
    {
        frames = 0;
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        var size = Sizes[sizeIndex];
        target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        target.Create();
        document.panelSettings.targetTexture = target;
    }

    private static void Tick()
    {
        if (++frames < 90) return;
        try
        {
            var root = document.rootVisualElement;
            var button = root.Q<Button>("primary-button");
            var login = root.Q<Button>("secondary-button");
            var header = root.Q<Label>("page-title");
            var scroll = root.Q<ScrollView>("page-scroll");
            Require(button != null && login != null && header != null && scroll != null, "Screen elements missing");
            Require(header.text == "Choose Companion" && login.text == "Log In", "Text not migrated");
            Require(button.resolvedStyle.width > 250 && button.resolvedStyle.height >= 64, "Button size invalid");
            Require(button.worldBound.xMin >= 0 && button.worldBound.xMax <= root.resolvedStyle.width + 1, "Horizontal overflow");
            Require(Mathf.Abs(button.resolvedStyle.backgroundColor.g - 230f / 255f) < .02f, "Primary colour mismatch");
            // Border widths are snapped to physical pixels at fractional panel scales.
            Require(login.resolvedStyle.borderTopWidth >= 1 && login.resolvedStyle.borderTopLeftRadius >= 18, "Outlined button styling missing");
            Require(button.resolvedStyle.unityFont != null, "Outfit font not resolved");
            Require(UnityEngine.Object.FindObjectsByType<Canvas>().Length == 0, "Legacy Canvas still present");

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes("Validation/PetCreation-" + target.width + "x" + target.height + ".png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = previous;
            Debug.Log("UI VALIDATED " + target.width + "x" + target.height + " | " + button.worldBound);

            if (++sizeIndex < Sizes.Length) { SetTarget(); return; }

            // Confirm a long page can actually scroll and that input reaches the action hooks.
            scroll.Add(new VisualElement { style = { height = 1800, flexShrink = 0 } });
            root.schedule.Execute(() => {
                try
                {
                    Require(scroll.verticalScroller.highValue > 0, "Scroll content does not overflow");
                    scroll.scrollOffset = new Vector2(0, 150);
                    Require(scroll.scrollOffset.y > 0, "Page cannot scroll");
                    int primaryCount = 0;
                    var screen = document.GetComponent<PetCreationScreen>();
                    var field = typeof(PetCreationScreen).GetField("onPrimaryAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    ((UnityEvent)field.GetValue(screen)).AddListener(() => primaryCount++);
                    using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
                    Require(primaryCount == 1, "Primary action not invoked exactly once");
                    File.WriteAllText("Validation/result.txt", "PASS: UXML/TSS imports; 4 screen sizes; Outfit fonts; colour and outline styles; no Canvas; scrolling; primary action input.");
                    SessionState.SetBool(Running, false);
                    EditorApplication.Exit(0);
                }
                catch (Exception ex) { Fail(ex); }
            }).StartingIn(500);
            EditorApplication.update -= Tick;
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Fail(Exception ex)
    {
        Debug.LogException(ex);
        File.WriteAllText("Validation/result.txt", "FAIL: " + ex);
        SessionState.SetBool(Running, false);
        EditorApplication.Exit(1);
    }
}

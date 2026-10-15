using UnityEngine;
using UnityEngine.UIElements;
using GamblingSeriousGame.UI;

[RequireComponent(typeof(UIDocument))]
[DefaultExecutionOrder(100)]
public class GameplayScreen : MonoBehaviour
{
    [SerializeField] private GameStateManager gameStateManager;
    private VisualElement sceneArea;
    private Image background;
    private VisualElement overlay;
    private PetSpritePreview petPreview;
    private ProgressBar healthBar;
    private Label xpCounter;
    private PetShopView shop;
    private IVisualElementScheduledItem healthUpdate;

    private void OnEnable()
    {
        if (gameStateManager == null)
            gameStateManager = FindAnyObjectByType<GameStateManager>();

        if (gameStateManager == null)
        {
            Debug.LogWarning("Assign a GameStateManager on GameplayScreen or add one to the scene.", this);
            return;
        }

        gameStateManager.LoadSavedPet();

        var root = GetComponent<UIDocument>().rootVisualElement;
        var dock = root.Q("dock-container");
        if (dock?.parent == null)
        {
            Debug.LogWarning("GameplayScreen requires the GameScreen layout with a dock-container.", this);
            return;
        }

        sceneArea = new VisualElement { name = "gameplay-scene" };
        sceneArea.AddToClassList("gameplay-scene");
        background = new Image
        {
            name = "gameplay-background",
            image = gameStateManager.BackgroundImage,
            scaleMode = ScaleMode.ScaleToFit,
            pickingMode = PickingMode.Ignore
        };
        background.AddToClassList("gameplay-background");
        var petImage = new Image { name = "gameplay-pet", pickingMode = PickingMode.Ignore };
        petImage.AddToClassList("gameplay-pet");
        background.Add(petImage);
        healthBar = new ProgressBar
        {
            name = "gameplay-health",
            lowValue = 0,
            pickingMode = PickingMode.Ignore
        };
        healthBar.AddToClassList("gameplay-health");
        var stats = new VisualElement { name = "gameplay-stats", pickingMode = PickingMode.Ignore };
        stats.AddToClassList("gameplay-stats");
        stats.Add(healthBar);
        xpCounter = new Label { name = "gameplay-xp", pickingMode = PickingMode.Ignore };
        xpCounter.AddToClassList("gameplay-xp");
        stats.Add(xpCounter);
        overlay = new VisualElement { name = "gameplay-overlay", pickingMode = PickingMode.Ignore };
        overlay.AddToClassList("gameplay-overlay");
        overlay.Add(stats);
        background.Add(overlay);
        shop = new PetShopView(overlay, gameStateManager);
        RefreshHealth();
        healthUpdate = healthBar.schedule.Execute(RefreshHealth).Every(250);
        sceneArea.Add(background);
        dock.parent.Insert(dock.parent.IndexOf(dock), sceneArea);

        petPreview = new PetSpritePreview(petImage);
        var breed = gameStateManager.Breed;
        var pet = gameStateManager.Pet;
        if (pet != null)
        {
            petPreview.Bind(breed?.previewImageFolder, breed?.imageResourcePath,
                breed?.previewFramesPerSecond ?? 10);
            if (!petPreview.HasImage)
                petPreview.Bind(pet.previewImageFolder, pet.imageResourcePath, pet.previewFramesPerSecond);
            if (!petPreview.HasImage)
                Debug.LogWarning("No sprite found for pet " + pet.id + " and breed " + breed?.id + ".", this);
        }
        else Debug.LogWarning("No saved pet was loaded for the gameplay screen.", this);

        sceneArea.RegisterCallback<GeometryChangedEvent>(OnSceneGeometryChanged);
        FitBackground();
    }

    private void OnSceneGeometryChanged(GeometryChangedEvent evt) => FitBackground();

    private void RefreshHealth()
    {
        var info = gameStateManager.PetInfo;
        int maximum = Mathf.Max(1, info?.maxHealth ?? 100);
        int current = Mathf.Clamp(info?.health ?? maximum, 0, maximum);
        healthBar.highValue = maximum;
        healthBar.value = current;
        healthBar.title = $"Health {current} / {maximum}";
        xpCounter.text = $"{Mathf.Max(0, info?.experience ?? 0):N0} XP";
        shop?.Refresh();
    }

    private void FitBackground()
    {
        var texture = gameStateManager.BackgroundImage;
        if (texture == null)
        {
            background.style.display = DisplayStyle.None;
            return;
        }
        var available = sceneArea.contentRect;
        if (float.IsNaN(available.width) || float.IsNaN(available.height) ||
            available.width <= 0 || available.height <= 0) return;
        float scale = Mathf.Min(available.width / texture.width, available.height / texture.height);
        background.style.width = texture.width * scale;
        background.style.height = texture.height * scale;
        // Preserve the HUD's proportions when a portrait background is fitted into landscape.
        float overlayScale = Mathf.Min(1, texture.width * scale / 280f);
        overlay.style.width = texture.width * scale / overlayScale;
        overlay.style.height = texture.height * scale / overlayScale;
        overlay.style.scale = new Scale(new Vector3(overlayScale, overlayScale, 1));
    }

    private void OnDisable()
    {
        healthUpdate?.Pause();
        healthUpdate = null;
        healthBar = null;
        xpCounter = null;
        shop = null;
        petPreview?.Dispose();
        petPreview = null;
        sceneArea?.UnregisterCallback<GeometryChangedEvent>(OnSceneGeometryChanged);
        sceneArea?.RemoveFromHierarchy();
        sceneArea = null;
        background = null;
        overlay = null;
    }
}

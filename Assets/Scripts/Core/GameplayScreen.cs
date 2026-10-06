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
    private PetSpritePreview petPreview;

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
    }

    private void OnDisable()
    {
        petPreview?.Dispose();
        petPreview = null;
        sceneArea?.UnregisterCallback<GeometryChangedEvent>(OnSceneGeometryChanged);
        sceneArea?.RemoveFromHierarchy();
        sceneArea = null;
        background = null;
    }
}

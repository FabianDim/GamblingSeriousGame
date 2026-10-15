using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [Serializable]
    public sealed class PetDefinition
    {
        public string id;
        public string name;
        public string description;
        public string imageResourcePath;
        public string previewImageFolder;
        public float previewFramesPerSecond = 10;
        public const string SavedAccessibilityKey = "Accessibility.Profile.v1";
        public string[] suggestedNames;
        public PetBreedDefinition[] breeds;
    }

    [Serializable]
    public sealed class PetCatalog
    {
        public PetDefinition[] animals;
    }

    /// <summary>
    /// Populates selectable pet cards from JSON and respects mobile safe areas.
    /// Card layout and styling remain editable in UXML/USS.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class PetCreationScreen : MonoBehaviour
    {
        [SerializeField] private TextAsset animalJson;
        [SerializeField] private VisualTreeAsset petCardTemplate;
        [SerializeField] private UnityEvent<string> onPetConfirmed = new UnityEvent<string>();
        [SerializeField]
        private UnityEvent onStopAnimation = new UnityEvent();
        [SerializeField] private UnityEvent onPrimaryAction = new UnityEvent();
        [SerializeField] private UnityEvent onLogIn = new UnityEvent();

        private VisualElement root;
        private VisualElement safeArea;
        private Button primaryButton;
        private Button loginButton;
        private Button confirmButton;
        private VisualElement petContainer;
        private Button selectedCard;

        private Button stopAnimation;
        private readonly List<Button> cards = new List<Button>();
        private readonly List<PetSpritePreview> previews = new List<PetSpritePreview>();
        private IVisualElementScheduledItem safeAreaUpdate;

        public PetDefinition SelectedPet { get; private set; }

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            safeArea = root.Q<VisualElement>("safe-area");
            primaryButton = root.Q<Button>("primary-button");
            loginButton = root.Q<Button>("secondary-button");
            confirmButton = root.Q<Button>("ConfirmPetSelection");
            petContainer = root.Q<VisualElement>("companion-content");
            stopAnimation = root.Q<Button>("StopAnimation");
            if (primaryButton != null) primaryButton.clicked += PrimaryAction;
            if (loginButton != null) loginButton.clicked += LogIn;
            if (confirmButton != null) confirmButton.clicked += ConfirmSelection;
            if (stopAnimation != null)
                stopAnimation.clicked += ToggleAnimations;

            ReloadPets();
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            safeAreaUpdate = root.schedule.Execute(ApplySafeArea).Every(250);
            ApplySafeArea();
        }

        private void OnDisable()
        {
            if (primaryButton != null) primaryButton.clicked -= PrimaryAction;
            if (loginButton != null) loginButton.clicked -= LogIn;
            if (confirmButton != null) confirmButton.clicked -= ConfirmSelection;
            if (stopAnimation != null)
                stopAnimation.clicked -= ToggleAnimations;
            ClearPets();
            root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            safeAreaUpdate?.Pause();
        }

        private void PrimaryAction() => onPrimaryAction.Invoke();
        private void LogIn() => onLogIn.Invoke();
        private void OnGeometryChanged(GeometryChangedEvent evt) => ApplySafeArea();

        public void ReloadPets()
        {
            ClearPets();
            if (petContainer == null) return;
            if (animalJson == null || petCardTemplate == null)
            {
                ShowPetMessage("Pet choices are not available yet.");
                Debug.LogWarning("Assign Animal Json and Pet Card Template on PetCreationScreen.", this);
                return;
            }

            PetCatalog catalog;
            try
            {
                catalog = JsonUtility.FromJson<PetCatalog>(animalJson.text);
            }
            catch (ArgumentException exception)
            {
                ShowPetMessage("Pet choices could not be loaded.");
                Debug.LogWarning("Invalid animal JSON: " + exception.Message, this);
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pet in catalog?.animals ?? Array.Empty<PetDefinition>())
            {
                if (pet == null || string.IsNullOrWhiteSpace(pet.id) ||
                    string.IsNullOrWhiteSpace(pet.name) || !ids.Add(pet.id))
                {
                    Debug.LogWarning("Skipping animal with missing id/name or duplicate id.", this);
                    continue;
                }

                var instance = petCardTemplate.Instantiate();
                var card = instance.Q<Button>("pet-card");
                if (card == null || card.Q<Label>("pet-name") == null ||
                    card.Q<Label>("pet-description") == null || card.Q<Image>("pet-image") == null ||
                    card.Q<Label>("pet-image-placeholder") == null || card.Q<Label>("pet-selection-state") == null)
                {
                    ClearPets();
                    ShowPetMessage("Pet choices could not be displayed.");
                    Debug.LogError("Pet Card Template must use the named elements in PetCard.uxml.", this);
                    return;
                }

                instance.style.flexShrink = 0;
                card.Q<Label>("pet-name").text = pet.name;
                card.Q<Label>("pet-description").text = pet.description ?? string.Empty;
                card.Q<Label>("pet-name").enableRichText = false;
                card.Q<Label>("pet-description").enableRichText = false;
                card.tooltip = "Select " + pet.name;
                SetPetImage(card, pet);
                card.clicked += () => SelectPet(pet, card);
                cards.Add(card);
                petContainer.Add(instance);
            }

            if (cards.Count == 0) ShowPetMessage("No pets are available yet.");
        }

        private void ClearPets()
        {
            SelectedPet = null;
            selectedCard = null;
            cards.Clear();
            foreach (var preview in previews) preview.Dispose();
            previews.Clear();
            petContainer?.Clear();
            confirmButton?.SetEnabled(false);
        }

        private void ShowPetMessage(string message)
        {
            var label = new Label(message) { name = "pet-list-message" };
            label.AddToClassList("body-copy");
            petContainer.Add(label);
        }

        private void SetPetImage(Button card, PetDefinition pet)
        {
            var preview = new PetSpritePreview(card.Q<Image>("pet-image"));
            previews.Add(preview);
            preview.Bind(pet.previewImageFolder, pet.imageResourcePath, pet.previewFramesPerSecond);
            card.Q<Label>("pet-image-placeholder").style.display =
                preview.HasImage ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void SelectPet(PetDefinition pet, Button card)
        {
            if (selectedCard != null)
            {
                selectedCard.RemoveFromClassList("pet-card--selected");
                selectedCard.Q<Label>("pet-selection-state").text = "Select";
            }
            SelectedPet = pet;
            selectedCard = card;
            card.AddToClassList("pet-card--selected");
            card.Q<Label>("pet-selection-state").text = "Selected";
            confirmButton?.SetEnabled(true);
        }

        private void ConfirmSelection()
        {
            if (SelectedPet == null) return;
            onPetConfirmed.Invoke(SelectedPet.id);
            onPrimaryAction.Invoke();
        }

        private void ToggleAnimations()
        {
            if (previews.Any(preview => preview.animationPlaying))
                StopAnimations();
            else
                PlayAnimations();
        }

        private void StopAnimations()
        {
            foreach (var preview in previews)
            {
                if (preview.animationPlaying)
                    preview.SetPlaying(false);
            }
            stopAnimation.text = "Play The Animations";

        }
        private void PlayAnimations()
        {
            foreach (var preview in previews)
            {
                if (!preview.animationPlaying)
                    preview.SetPlaying(true);
            }
            stopAnimation.text = "Stop The Animations";
        }

        private void ApplySafeArea()
        {
            if (safeArea == null || root.panel == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            // Convert physical pixels into panel units after PanelSettings scaling.
            var width = root.resolvedStyle.width;
            var height = root.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width <= 0 || height <= 0)
                return;

            var rect = Screen.safeArea;
            root.Q<VisualElement>("app")?.EnableInClassList("app--compact", rect.height * height / Screen.height < 600);
            safeArea.style.paddingLeft = rect.xMin * width / Screen.width;
            safeArea.style.paddingRight = (Screen.width - rect.xMax) * width / Screen.width;
            safeArea.style.paddingTop = (Screen.height - rect.yMax) * height / Screen.height;
            safeArea.style.paddingBottom = rect.yMin * height / Screen.height;
        }
    }
}

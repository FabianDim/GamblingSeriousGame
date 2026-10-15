using System;
using System.Collections.Generic;
using GamblingSeriousGame.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class PetCustomizationScreen : MonoBehaviour
    {
        public const string SavedProfileKey = "Companion.Profile.v1";
        [SerializeField] private TextAsset animalJson;
        // Retained for existing scene listeners; the detailed event also supplies the breed.
        [SerializeField] private UnityEvent<string, string> onPetCreated = new UnityEvent<string, string>();
        [SerializeField] private UnityEvent<PetProfile> onProfileCreated = new UnityEvent<PetProfile>();
        [SerializeField] private UnityEvent onBackRequested = new UnityEvent();

        private sealed class Draft
        {
            public Guid playerPetUID = Guid.NewGuid();
            public string name = "";
            public string breedId;
            public int backgroundId = 1;
            public bool saved;
        }
        private readonly Dictionary<string, Draft> drafts = new Dictionary<string, Draft>(StringComparer.Ordinal);
        private readonly List<Button> breedButtons = new List<Button>();
        private List<PetBreedDefinition> breeds = new List<PetBreedDefinition>();
        private PetDefinition pet;
        private PetBreedDefinition breed;

        private Draft draft;
        private VisualElement root, breedList;
        private TextField nameInput;
        private Label status, placeholder, summary, nameCount, breedDescription;
        private Button doneButton, backButton, suggestButton, animationButton;
        private PetSpritePreview preview;
        private IVisualElementScheduledItem safeAreaUpdate;
        private bool playing = true;
        private int suggestionIndex;

        public string SelectedPetId => pet?.id;
        public string SelectedBreedId => breed?.id;
        public string PetName => draft?.name ?? string.Empty;
        public PetProfile CurrentProfile => pet == null || breed == null ? null : new PetProfile
        {
            uniquePlayerPetID = draft.playerPetUID.ToString(),
            animalId = pet.id,
            breedId = breed.id,
            petName = PetName.Trim(),
            backgroundId = draft.backgroundId
        };

        /// <summary>Call before opening. Each animal keeps its own draft for this session.</summary>
        public void SetPet(string petId)
        {
            pet = null;
            try
            {
                var catalog = animalJson == null ? null : JsonUtility.FromJson<PetCatalog>(animalJson.text);
                pet = Array.Find(catalog?.animals ?? Array.Empty<PetDefinition>(), entry =>
                    entry != null && !string.IsNullOrWhiteSpace(entry.id) &&
                    !string.IsNullOrWhiteSpace(entry.name) && entry.id == petId);
            }
            catch (ArgumentException exception) { Debug.LogWarning("Invalid animal JSON: " + exception.Message, this); }
            draft = null;
            breed = null;
            breeds = PetProfileRules.GetBreeds(pet);
            if (pet != null)
            {
                if (!drafts.TryGetValue(pet.id, out draft))
                {
                    draft = new Draft();
                    drafts.Add(pet.id, draft);
                    RestoreSavedProfile();
                }
                breed = breeds.Find(entry => entry.id == draft.breedId) ?? breeds[0];
                draft.breedId = breed.id;
            }
            if (root != null) BindPet();
        }

        private void RestoreSavedProfile()
        {
            try
            {
                var json = PlayerPrefs.GetString(SavedProfileKey, "");
                var saved = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PetProfile>(json);
                if (saved?.animalId != pet.id || !PetProfileRules.IsValidName(saved.petName) ||
                    !breeds.Exists(entry => entry.id == saved.breedId)) return;
                draft.name = saved.petName;
                draft.breedId = saved.breedId;
                draft.backgroundId = saved.backgroundId > 0 ? saved.backgroundId : 1;
                if (Guid.TryParse(saved.uniquePlayerPetID, out var savedUID) && savedUID != Guid.Empty)
                    draft.playerPetUID = savedUID;
                // Restored profiles can be confirmed again in a new session.
            }
            catch (ArgumentException) { /* A damaged local save must not block creation. */ }
        }

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            breedList = root.Q("breed-options");
            nameInput = root.Q<TextField>("pet-name-input");
            status = root.Q<Label>("customization-status");
            placeholder = root.Q<Label>("preview-placeholder");
            summary = root.Q<Label>("profile-summary");
            nameCount = root.Q<Label>("name-count");
            breedDescription = root.Q<Label>("breed-description");
            doneButton = root.Q<Button>("finish-pet");
            backButton = root.Q<Button>("back-to-selection");
            suggestButton = root.Q<Button>("suggest-name");
            animationButton = root.Q<Button>("toggle-animation");
            preview = new PetSpritePreview(root.Q<Image>("pet-preview"));
            foreach (var label in root.Query<Label>().ToList()) label.enableRichText = false;
            nameInput.maxLength = PetProfileRules.MaxNameLength;
            nameInput.RegisterValueChangedCallback(OnNameChanged);
            doneButton.clicked += Finish;
            backButton.clicked += Back;
            suggestButton.clicked += SuggestName;
            animationButton.clicked += ToggleAnimation;
            safeAreaUpdate = root.schedule.Execute(ApplySafeArea).Every(250);
            BindPet();
        }

        private void OnDisable()
        {
            nameInput?.UnregisterValueChangedCallback(OnNameChanged);
            if (doneButton != null) doneButton.clicked -= Finish;
            if (backButton != null) backButton.clicked -= Back;
            if (suggestButton != null) suggestButton.clicked -= SuggestName;
            if (animationButton != null) animationButton.clicked -= ToggleAnimation;
            preview?.Dispose();
            safeAreaUpdate?.Pause();
            breedList?.Clear();
            breedButtons.Clear();
            root = null;
        }

        private void BindPet()
        {
            root.Q<Label>("pet-species").text = pet?.name ?? "Your new companion";
            root.Q<Label>("pet-description").text = pet?.description ?? "Choose an animal to get started.";
            nameInput.SetValueWithoutNotify(PetName);
            nameInput.SetEnabled(pet != null);
            suggestButton.SetEnabled(pet != null);
            breedList.Clear();
            breedButtons.Clear();
            foreach (var option in breeds)
            {
                var button = new Button(() => SelectBreed(option)) { text = option.name, tooltip = option.description };
                button.AddToClassList("breed-option");
                button.enableRichText = false;
                button.userData = option.id;
                breedList.Add(button);
                breedButtons.Add(button);
            }
            BindBreed();
            UpdateControls();
            ApplySafeArea();
        }

        private void SelectBreed(PetBreedDefinition next)
        {
            if (breed == next) return;
            breed = next;
            draft.breedId = next.id;
            draft.saved = false;
            BindBreed();
            UpdateControls();
        }

        private void BindBreed()
        {
            foreach (var button in breedButtons)
            {
                bool selected = (string)button.userData == breed?.id;
                button.EnableInClassList("breed-option--selected", selected);
            }
            breedDescription.text = breed?.description ?? "No animal selected.";
            preview.Bind(breed?.previewImageFolder, breed?.imageResourcePath, breed?.previewFramesPerSecond ?? 10);
            placeholder.text = pet == null ? "Choose a companion first" : "Preview coming soon";
            placeholder.style.display = preview.HasImage ? DisplayStyle.None : DisplayStyle.Flex;
            animationButton.SetEnabled(preview.CanAnimate);
            preview.SetPlaying(playing);
            UpdateAnimationButton();
        }

        private void OnNameChanged(ChangeEvent<string> evt)
        {
            if (draft == null) return;
            draft.name = evt.newValue ?? "";
            draft.saved = false;
            UpdateControls();
        }

        private void SuggestName()
        {
            if (draft == null) return;
            var choices = new List<string>();
            foreach (string name in pet.suggestedNames ?? Array.Empty<string>())
                if (PetProfileRules.IsValidName(name)) choices.Add(name.Trim());
            if (choices.Count == 0) choices.AddRange(new[] { "Sunny", "Clover", "Pip", "Pebble", "Mochi" });
            string suggestion = choices[suggestionIndex++ % choices.Count];
            if (suggestion == PetName && choices.Count > 1) suggestion = choices[suggestionIndex++ % choices.Count];
            nameInput.value = suggestion;
        }

        private void ToggleAnimation()
        {
            playing = !playing;
            preview.SetPlaying(playing);
            UpdateAnimationButton();
        }

        private void UpdateAnimationButton()
        {
            string action = playing ? "Pause preview" : "Play preview";
            animationButton.tooltip = action;
            var label = animationButton.Q<Label>("preview-playback-label");
            if (label != null) label.text = action;
            else animationButton.text = action;
            var icon = animationButton.Q<DockIcon>("preview-playback-icon");
            if (icon != null) icon.Kind = playing ? DockIconKind.Pause : DockIconKind.Play;
        }

        private void Back() => onBackRequested.Invoke();

        private void Finish()
        {
            if (pet == null || breed == null || draft.saved || !PetProfileRules.IsValidName(PetName)) return;
            draft.name = PetName.Trim();
            nameInput.SetValueWithoutNotify(PetName);
            var profile = CurrentProfile;
            try
            {
                PetSaveStore.SaveProfile(profile);
                PlayerPrefs.SetString(SavedProfileKey, JsonUtility.ToJson(profile));
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                status.text = "Could not save your companion. Please try again.";
                Debug.LogWarning("Companion save failed: " + exception.Message, this);
                return;
            }
            // PlayerPrefs selects the latest companion; pets.json retains all saved pets.
            foreach (var otherDraft in drafts.Values) otherDraft.saved = false;
            draft.saved = true;
            UpdateControls();
            onPetCreated.Invoke(profile.animalId, profile.petName);
            onProfileCreated.Invoke(profile);
        }

        private void UpdateControls()
        {
            bool validName = PetProfileRules.IsValidName(PetName);
            bool saved = draft?.saved == true;
            doneButton.SetEnabled(pet != null && breed != null && validName && !saved);
            doneButton.text = saved ? "Companion saved" : "Save companion";
            nameCount.text = PetName.Length + " / " + PetProfileRules.MaxNameLength;
            summary.text = pet == null ? "Your companion details will appear here." :
                (string.IsNullOrWhiteSpace(PetName) ? "Your companion" : PetName.Trim()) + " · " + pet.name + " · " + breed.name;
            status.text = pet == null ? "Go back and choose a companion." : saved ?
                PetName + " is saved on this device. You can still change their details." :
                !validName ? "Enter a name of 1–24 characters, without line breaks." : "Ready to meet your companion.";
        }

        private void ApplySafeArea()
        {
            if (root?.panel == null || Screen.width <= 0 || Screen.height <= 0) return;
            float x = root.resolvedStyle.width / Screen.width, y = root.resolvedStyle.height / Screen.height;
            if (float.IsNaN(x) || float.IsNaN(y) || x <= 0 || y <= 0) return;
            var rect = Screen.safeArea;
            var safeArea = root.Q("safe-area");
            root.Q("app").EnableInClassList("app--compact", rect.height * y < 600);
            safeArea.style.paddingLeft = rect.xMin * x;
            safeArea.style.paddingRight = (Screen.width - rect.xMax) * x;
            safeArea.style.paddingTop = (Screen.height - rect.yMax) * y;
            safeArea.style.paddingBottom = rect.yMin * y;
        }
    }
}

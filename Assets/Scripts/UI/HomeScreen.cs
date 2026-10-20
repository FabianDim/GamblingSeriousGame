using System;
using GamblingSeriousGame.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [RequireComponent(typeof(UIDocument), typeof(BasePageScreen))]
    [DefaultExecutionOrder(100)]
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset contentTemplate;
        [SerializeField] private GameStateManager gameStateManager;
        [Tooltip("Optional MonoBehaviour implementing IScreenTimeUsageProvider. Empty means not connected.")]
        [SerializeField] private MonoBehaviour usageProvider;
        private IScreenTimeUsageProvider provider;
        private bool providerAssigned;
        private VisualElement content;
        private ScreenTimeView usageView;
        private PetSpritePreview petPreview;
        private UsagePeriod period;
        private bool example;

        public UsagePeriod Period => period;
        public bool ShowingExample => example;

        private void OnEnable()
        {
            var parent = GetComponent<UIDocument>().rootVisualElement.Q("page-content");
            if (parent == null || contentTemplate == null)
            {
                Debug.LogWarning("HomeScreen needs BasePage and a HomeContent template.", this);
                return;
            }
            content = contentTemplate.Instantiate();
            parent.Add(content);
            usageView = new ScreenTimeView(content);
            content.Q<Button>("usage-today").clicked += ShowToday;
            content.Q<Button>("usage-week").clicked += ShowWeek;
            content.Q<Button>("usage-example").clicked += ToggleExample;
            content.Q<Button>("usage-refresh").clicked += RefreshUsage;
            content.Q<Button>("home-open-pet").clicked += OpenPet;
            content.Q<Button>("home-open-interventions").clicked += OpenInterventions;
            if (!providerAssigned) provider = usageProvider as IScreenTimeUsageProvider;
            if (provider != null) provider.Changed += RenderUsage;
            if (gameStateManager == null) gameStateManager = FindAnyObjectByType<GameStateManager>();
            RenderCompanion();
            RenderUsage();
            GetComponent<BasePageScreen>().SelectDestination("home");
        }

        /// <summary>Inject the real platform service later. Calls and Changed events must use Unity's main thread.</summary>
        public void SetUsageProvider(IScreenTimeUsageProvider source)
        {
            if (provider != null && content != null) provider.Changed -= RenderUsage;
            provider = source;
            providerAssigned = true;
            example = false;
            if (provider != null && content != null) provider.Changed += RenderUsage;
            RenderUsage();
        }

        public void SelectPeriod(UsagePeriod value)
        {
            period = value;
            RenderUsage();
        }
        public void SetExample(bool value) { example = value; RenderUsage(); }
        private void ShowToday() => SelectPeriod(UsagePeriod.Today);
        private void ShowWeek() => SelectPeriod(UsagePeriod.LastSevenDays);
        private void ToggleExample() => SetExample(!example);
        private void OpenPet() => GetComponent<BasePageScreen>().Navigate("my-pet");
        private void OpenInterventions() => GetComponent<BasePageScreen>().Navigate("interventions");

        private void RefreshUsage()
        {
            try { provider?.Refresh(); RenderUsage(); }
            catch (Exception) { ShowProviderError(); }
        }
        private void RenderUsage()
        {
            if (usageView == null) return;
            try
            {
                var snapshot = example ? ScreenTimeExamples.Create(period) : provider?.GetSnapshot(period);
                usageView.Render(snapshot, period, example, provider != null);
            }
            catch (Exception) { ShowProviderError(); }
        }
        private void ShowProviderError() => usageView?.Render(new UsageSnapshot { status = UsageStatus.Error }, period, false, provider != null);

        private void RenderCompanion()
        {
            var pet = gameStateManager != null ? gameStateManager.SelectedPet : null;
            var nameLabel = content.Q<Label>("home-pet-name");
            nameLabel.enableRichText = false;
            nameLabel.text = pet != null ? pet.Profile.petName : "A little company";
            content.Q<Label>("home-pet-description").text = pet != null ? "A familiar face, whenever you need one." : "Your companion is a tap away.";
            petPreview = new PetSpritePreview(content.Q<Image>("home-pet-image"));
            if (pet != null)
            {
                petPreview.Bind(pet.Breed.previewImageFolder, pet.Breed.imageResourcePath, pet.Breed.previewFramesPerSecond);
                if (!petPreview.HasImage)
                    petPreview.Bind(pet.Animal.PreviewImageFolder, pet.Animal.ImageResourcePath, pet.Animal.PreviewFramesPerSecond);
            }
            content.Q("home-pet-placeholder").style.display = petPreview.HasImage ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnDisable()
        {
            if (provider != null && content != null) provider.Changed -= RenderUsage;
            if (content != null)
            {
                content.Q<Button>("usage-today").clicked -= ShowToday;
                content.Q<Button>("usage-week").clicked -= ShowWeek;
                content.Q<Button>("usage-example").clicked -= ToggleExample;
                content.Q<Button>("usage-refresh").clicked -= RefreshUsage;
                content.Q<Button>("home-open-pet").clicked -= OpenPet;
                content.Q<Button>("home-open-interventions").clicked -= OpenInterventions;
            }
            petPreview?.Dispose();
            petPreview = null;
            content?.RemoveFromHierarchy();
            content = null;
            usageView = null;
        }
    }
}

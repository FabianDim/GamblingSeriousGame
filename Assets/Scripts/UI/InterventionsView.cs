using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [Serializable]
    public sealed class InterventionActivity
    {
        public string id;
        public string title;
        public string category;
        public int minutes;
        public string description;
        public int rewardXp;
        public string[] steps;

        public bool HasInstructions => minutes > 0 && minutes <= 60 && rewardXp > 0 &&
            steps != null && steps.Length > 0 && !Array.Exists(steps, string.IsNullOrWhiteSpace);
    }

    [Serializable]
    public sealed class InterventionCatalog
    {
        public InterventionActivity[] activities;
    }

    public sealed class InterventionsView
    {
        public VisualElement Root { get; }
        public const string ResourcePath = "Interventions/Activities";
        private readonly Action openGrounding;
        private readonly Action<InterventionActivity> openActivity;

        public InterventionsView(VisualElement parent, Action openGrounding = null,
            Action<InterventionActivity> openActivity = null)
        {
            this.openGrounding = openGrounding;
            this.openActivity = openActivity;
            Root = new VisualElement { name = "interventions-screen" };
            Root.AddToClassList("interventions-screen");
            parent.Add(Root);
            var asset = Resources.Load<TextAsset>(ResourcePath);
            LoadJson(asset != null ? asset.text : null);
        }

        public void LoadJson(string json)
        {
            Root.Clear();
            var groundingButton = new Button(() => openGrounding?.Invoke())
            {
                name = "open-grounding",
                text = "I feel like gambling"
            };
            groundingButton.AddToClassList("app-button");
            groundingButton.AddToClassList("grounding-entry");
            Root.Add(groundingButton);
            Root.Add(Text("Take a short pause with a grounding exercise.", "grounding-entry-hint"));
            Root.Add(Text("Small steps, at your pace", "interventions-heading"));
            Root.Add(Text("Explore an activity to notice gambling thoughts, manage urges or plan a different response.", "interventions-intro"));
            Root.Add(Text("Example CBT-informed self-help activities. These do not replace individual support from a qualified professional.", "interventions-note"));
            InterventionCatalog catalog = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(json)) catalog = JsonUtility.FromJson<InterventionCatalog>(json);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("The interventions activity JSON could not be read.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (var activity in catalog?.activities ?? Array.Empty<InterventionActivity>())
            {
                if (activity == null || string.IsNullOrWhiteSpace(activity.id) ||
                    string.IsNullOrWhiteSpace(activity.title) || string.IsNullOrWhiteSpace(activity.description) ||
                    !ids.Add(activity.id)) continue;
                Root.Add(Card(activity));
                count++;
            }
            if (count == 0)
                Root.Add(Text("Activities are unavailable right now. Please try again later.", "interventions-empty"));
        }

        private VisualElement Card(InterventionActivity activity)
        {
            var card = new VisualElement { name = "activity-" + activity.id };
            card.AddToClassList("intervention-card");
            string duration = activity.minutes > 0 ? $" · {activity.minutes} min" : "";
            card.Add(Text((activity.category ?? "Reflection") + duration, "intervention-meta"));
            card.Add(Text(activity.title, "intervention-title"));
            card.Add(Text(activity.description, "intervention-description"));
            if (activity.HasInstructions)
            {
                card.Add(Text($"+{activity.rewardXp} XP for practising", "intervention-reward"));
                var start = new Button(() => openActivity?.Invoke(activity))
                {
                    name = "start-" + activity.id,
                    text = "Open activity"
                };
                start.AddToClassList("app-button");
                start.AddToClassList("button--secondary");
                start.AddToClassList("intervention-start");
                card.Add(start);
            }
            return card;
        }

        private static Label Text(string text, string className)
        {
            var label = new Label(text) { enableRichText = false };
            label.AddToClassList(className);
            return label;
        }
    }
}

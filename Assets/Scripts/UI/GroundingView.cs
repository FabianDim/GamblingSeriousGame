using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [Serializable]
    public sealed class GroundingExercise
    {
        public string id;
        public string title;
        public string duration;
        public string[] steps;
    }

    [Serializable]
    public sealed class GroundingCatalog
    {
        public GroundingExercise[] exercises;
    }

    // A focused subpage: back and exercise navigation stay outside the scrolling steps.
    public sealed class GroundingView
    {
        public const string ResourcePath = "Interventions/Grounding";
        public VisualElement Root { get; }
        public int CurrentIndex { get; private set; }
        public int ExerciseCount => exercises.Count;
        private readonly List<GroundingExercise> exercises = new List<GroundingExercise>();
        private readonly ScrollView scroll;
        private readonly Label progress, title, duration;
        private readonly VisualElement steps;
        private readonly Button back, previous, next, resources;

        public GroundingView(VisualElement parent, Action onBack, Action toResources)
        {
            Root = new VisualElement { name = "grounding-screen" };
            var nav_card = new VisualElement { name = "nav-buttons" };
            Root.AddToClassList("content-panel");
            Root.AddToClassList("grounding-screen");
            Root.style.display = DisplayStyle.None;
            parent.Add(Root);

            back = MakeButton("grounding-back", "Back to interventions", onBack, "button--secondary");
            resources = MakeButton("resources-btn", "Gambling Resources", toResources, "button--secondary");
            back.AddToClassList("grounding-back");
            resources.AddToClassList("resources-btn");
            nav_card.Add(resources);
            nav_card.style.flexDirection = FlexDirection.Row;
            nav_card.style.alignItems = Align.Center;
            nav_card.style.justifyContent = Justify.SpaceBetween;
            nav_card.Add(back);
            Root.Add(nav_card);

            scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "grounding-scroll",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("page-scroll");
            Root.Add(scroll);
            scroll.Add(Text("A moment to pause", "grounding-heading"));
            scroll.Add(Text("Bring your attention to what is around you, one step at a time.", "grounding-intro"));

            var card = new VisualElement { name = "grounding-exercise" };
            card.AddToClassList("grounding-card");
            scroll.Add(card);
            duration = Text("", "grounding-duration");
            card.Add(duration);
            title = Text("", "grounding-title");
            title.name = "grounding-title";
            card.Add(title);
            steps = new VisualElement { name = "grounding-steps" };
            card.Add(steps);
            scroll.Add(Text("Go at your own pace. Skip any step that doesn't suit you, or try another exercise.", "grounding-permission"));

            var footer = new VisualElement { name = "grounding-footer" };
            footer.AddToClassList("grounding-footer");
            Root.Add(footer);
            progress = Text("", "grounding-progress");
            progress.name = "grounding-progress";
            footer.Add(progress);
            var actions = new VisualElement();
            actions.AddToClassList("grounding-actions");
            footer.Add(actions);
            previous = MakeButton("grounding-previous", "Previous", () => Move(-1), "button--secondary");
            next = MakeButton("grounding-next", "Next exercise", () => Move(1), "button--primary");
            actions.Add(previous);
            actions.Add(next);

            var asset = Resources.Load<TextAsset>(ResourcePath);
            LoadJson(asset != null ? asset.text : null);
        }

        public void LoadJson(string json)
        {
            exercises.Clear();
            GroundingCatalog catalog = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(json)) catalog = JsonUtility.FromJson<GroundingCatalog>(json);
            }
            catch (ArgumentException) { Debug.LogWarning("The grounding exercise JSON could not be read."); }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var exercise in catalog?.exercises ?? Array.Empty<GroundingExercise>())
            {
                if (exercise == null || string.IsNullOrWhiteSpace(exercise.id) ||
                    string.IsNullOrWhiteSpace(exercise.title) || exercise.steps == null ||
                    exercise.steps.Length == 0 || Array.Exists(exercise.steps, string.IsNullOrWhiteSpace) ||
                    !ids.Add(exercise.id)) continue;
                exercises.Add(exercise);
            }
            // The urgent entry point remains usable even when its content asset is missing.
            if (exercises.Count == 0)
                exercises.Add(new GroundingExercise
                {
                    id = "notice-an-object",
                    title = "Notice one object",
                    duration = "About 1 minute",
                    steps = new[] { "Choose an everyday object near you.",
                        "Look closely at its colour, shape and small details.",
                        "Name three details, slowly. Return to them if your attention wanders." }
                });
            previous.SetEnabled(exercises.Count > 1);
            next.SetEnabled(exercises.Count > 1);
            Show(0);
        }

        public void OpenRandom()
        {
            Root.style.display = DisplayStyle.Flex;
            Show(UnityEngine.Random.Range(0, exercises.Count));
            back.Focus();
        }

        private void Move(int offset) => Show((CurrentIndex + offset + exercises.Count) % exercises.Count);

        private void Show(int index)
        {
            CurrentIndex = index;
            var exercise = exercises[index];
            title.text = exercise.title;
            duration.text = exercise.duration ?? "At your own pace";
            progress.text = $"Exercise {index + 1} of {exercises.Count}";
            steps.Clear();
            for (int i = 0; i < exercise.steps.Length; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("grounding-step");
                row.Add(Text((i + 1).ToString(), "grounding-step-number"));
                row.Add(Text(exercise.steps[i], "grounding-step-text"));
                steps.Add(row);
            }
            scroll.scrollOffset = Vector2.zero;
        }

        private static Button MakeButton(string name, string text, Action action, string style)
        {
            var button = new Button(action) { name = name, text = text };
            button.AddToClassList("app-button");
            button.AddToClassList(style);
            return button;
        }

        private static Label Text(string text, string style)
        {
            var label = new Label(text) { enableRichText = false };
            label.AddToClassList(style);
            return label;
        }
    }
}

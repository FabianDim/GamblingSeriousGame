using System;
using GamblingSeriousGame.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    public sealed class InterventionDetailView
    {
        public VisualElement Root { get; }
        public InterventionSession Session { get; private set; }
        public bool IsOpen { get; private set; }
        private readonly Func<string> petId;
        private readonly Func<PetSaveData> petInfo;
        private readonly ScrollView scroll;
        private readonly VisualElement steps;
        private readonly Label title, summary, metadata, status;
        private readonly Button back, collect, restart;
        private string claimMessage;

        public InterventionDetailView(VisualElement parent, Action onBack, Action onRestart,
            Func<string> petId, Func<PetSaveData> petInfo)
        {
            this.petId = petId;
            this.petInfo = petInfo;
            Root = new VisualElement { name = "intervention-detail" };
            Root.AddToClassList("content-panel");
            Root.AddToClassList("grounding-screen");
            Root.AddToClassList("intervention-detail");
            Root.style.display = DisplayStyle.None;
            parent.Add(Root);
            back = Button("activity-back", "Back to interventions", onBack, "button--secondary");
            back.AddToClassList("grounding-back");
            Root.Add(back);
            scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "activity-scroll",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("page-scroll");
            Root.Add(scroll);
            var card = new VisualElement();
            card.AddToClassList("grounding-card");
            scroll.Add(card);
            metadata = Text("activity-meta", "grounding-duration");
            title = Text("activity-title", "grounding-title");
            summary = Text("activity-summary", "intervention-description");
            card.Add(metadata);
            card.Add(title);
            card.Add(summary);
            steps = new VisualElement { name = "activity-steps" };
            card.Add(steps);
            var hint = Text("activity-hint", "grounding-permission");
            hint.text = "Take your time. The timer pauses when you leave this page or app. You can return during this visit to continue. Your reflections stay with you; there is nothing to submit.";
            scroll.Add(hint);
            var footer = new VisualElement { name = "activity-footer" };
            footer.AddToClassList("grounding-footer");
            Root.Add(footer);
            status = Text("activity-status", "grounding-progress");
            footer.Add(status);
            collect = Button("activity-collect", "Collect XP", Claim, "button--primary");
            footer.Add(collect);
            restart = Button("activity-restart", "Practise again", onRestart, "text-button");
            restart.style.display = DisplayStyle.None;
            footer.Add(restart);
        }

        public void Open(InterventionActivity activity, InterventionSession session)
        {
            Session = session;
            claimMessage = null;
            IsOpen = true;
            Root.style.display = DisplayStyle.Flex;
            title.text = activity.title;
            summary.text = activity.description;
            metadata.text = $"{activity.category}  |  {activity.minutes} min  |  +{session.RewardXp} XP";
            steps.Clear();
            for (int i = 0; i < activity.steps.Length; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("grounding-step");
                var number = Text(null, "grounding-step-number");
                number.text = (i + 1).ToString();
                var instruction = Text(null, "grounding-step-text");
                instruction.text = activity.steps[i];
                row.Add(number);
                row.Add(instruction);
                steps.Add(row);
            }
            scroll.scrollOffset = Vector2.zero;
            Refresh();
            back.Focus();
        }

        public void Hide()
        {
            IsOpen = false;
            Root.style.display = DisplayStyle.None;
        }

        public void Advance(double seconds, bool foreground)
        {
            if (!IsOpen || Session == null) return;
            if (foreground && HasMatchingPet()) Session.Advance(seconds);
            Refresh();
        }

        private bool HasMatchingPet() => petInfo() != null && Guid.TryParse(petId(), out var id) &&
            id != Guid.Empty && id.ToString() == Session.PetId;

        private void Claim()
        {
            if (!IsOpen || Session == null) return;
            Session.TryClaim(petId(), petInfo(), out claimMessage);
            Refresh();
        }

        private void Refresh()
        {
            bool matchingPet = HasMatchingPet();
            collect.SetEnabled(Session.Ready && matchingPet);
            collect.text = Session.Claimed ? "XP collected" : $"Finish & collect {Session.RewardXp} XP";
            restart.style.display = Session.Claimed ? DisplayStyle.Flex : DisplayStyle.None;
            if (Session.Claimed) status.text = $"+{Session.RewardXp} XP saved for your pet.";
            else if (!matchingPet) status.text = "Choose your saved pet in My Pet to earn XP.";
            else if (!string.IsNullOrEmpty(claimMessage)) status.text = claimMessage;
            else if (Session.Ready) status.text = "Ready when you've finished the steps.";
            else
            {
                int remaining = (int)Math.Ceiling(Session.RemainingSeconds);
                status.text = $"Practice time remaining  {remaining / 60:00}:{remaining % 60:00}";
            }
        }

        private static Button Button(string name, string text, Action action, string style)
        {
            var button = new Button(action) { name = name, text = text };
            button.AddToClassList("app-button");
            button.AddToClassList(style);
            return button;
        }

        private static Label Text(string name, string style)
        {
            var label = new Label { name = name, enableRichText = false };
            label.AddToClassList(style);
            return label;
        }
    }
}

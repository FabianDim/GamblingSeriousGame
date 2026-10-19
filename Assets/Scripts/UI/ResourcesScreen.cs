using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [Serializable]
    public sealed class SupportResource
    {
        public string id, category, title, description, detail, phone, url, action;
    }

    [Serializable]
    public sealed class SupportResourceCatalog
    {
        public string reviewed;
        public SupportResource[] services = Array.Empty<SupportResource>();
    }

    [RequireComponent(typeof(UIDocument), typeof(BasePageScreen))]
    [DefaultExecutionOrder(100)]
    public sealed class ResourcesScreen : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset contentTemplate;
        [SerializeField] private TextAsset resourceCatalog;
        private VisualElement content;
        private Label status;
        private ResourcesView view;

        private void OnEnable()
        {
            var parent = GetComponent<UIDocument>().rootVisualElement.Q("page-content");
            if (parent == null || contentTemplate == null || resourceCatalog == null)
            {
                Debug.LogWarning("ResourcesScreen needs its content template and resource catalog.", this);
                return;
            }
            content = contentTemplate.Instantiate();
            parent.Add(content);
            status = content.Q<Label>("resources-status");
#if !UNITY_ANDROID || UNITY_EDITOR
            content.Q<Label>("resources-link-note").text = "Phone buttons copy the number. Website links open your browser.";
#endif
            var catalog = JsonUtility.FromJson<SupportResourceCatalog>(resourceCatalog.text);
            view = new ResourcesView(content, catalog, OpenWebsite, CallPhone);
            GetComponent<BasePageScreen>().SelectDestination("resources");
        }

        private void OpenWebsite(string url)
        {
            Application.OpenURL(url);
            ShowStatus("Opening the service's website in your browser.");
        }

        private void CallPhone(string phone)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // ACTION_DIAL leaves the final call to the player; no CALL_PHONE permission.
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "tel:" + phone.Replace(" ", "")))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.DIAL", uri))
                    activity.Call("startActivity", intent);
                ShowStatus("Your phone app will open with " + phone + ". Tap Call when you're ready.");
            }
            catch (AndroidJavaException)
            {
                ShowStatus("Couldn't open a phone app. You can call " + phone + " from another phone.");
            }
#else
            GUIUtility.systemCopyBuffer = phone;
            ShowStatus(phone + " copied. Call from your phone when you're ready.");
#endif
        }

        private void ShowStatus(string message)
        {
            status.text = message;
            status.style.display = DisplayStyle.Flex;
            GetComponent<UIDocument>().rootVisualElement.Q<ScrollView>("base-page-scroll")?.ScrollTo(status);
        }

        private void OnDisable()
        {
            view?.Dispose();
            view = null;
            content?.RemoveFromHierarchy();
            content = null;
            status = null;
        }
    }

    /// <summary>Callbacks are injected so UI validation never calls support services.</summary>
    public sealed class ResourcesView : IDisposable
    {
        private readonly VisualElement root;
        private readonly SupportResourceCatalog catalog;
        private readonly Action<string> openWebsite, callPhone;
        private readonly List<Button> filters = new List<Button>();
        private readonly List<Action> handlers = new List<Action>();
        private readonly Action callHelp, openChat;

        public ResourcesView(VisualElement root, SupportResourceCatalog catalog,
            Action<string> openWebsite, Action<string> callPhone)
        {
            this.root = root;
            this.catalog = catalog;
            this.openWebsite = openWebsite;
            this.callPhone = callPhone;
            var helpline = Array.Find(catalog.services, item => item.id == "gambling-help");
            callHelp = () => callPhone(helpline.phone);
            openChat = () => openWebsite(helpline.url);
            root.Q<Button>("resources-call-help").text = "Call " + helpline.phone;
            root.Q<Button>("resources-call-help").clicked += callHelp;
            root.Q<Button>("resources-chat").clicked += openChat;
            root.Q<Label>("resources-reviewed").text = "Details checked " + catalog.reviewed + ". Check each service's website for current availability.";
            foreach (string category in new[] { "All", "Money", "Tools", "Community", "Local" })
            {
                string selected = category;
                var button = root.Q<Button>("resources-filter-" + category.ToLowerInvariant());
                Action handler = () => SelectCategory(selected);
                button.clicked += handler;
                filters.Add(button);
                handlers.Add(handler);
            }
            SelectCategory("All");
        }

        public void SelectCategory(string category)
        {
            foreach (var button in filters)
            {
                bool selected = button.name == "resources-filter-" + category.ToLowerInvariant();
                button.EnableInClassList("resource-filter--selected", selected);
                button.tooltip = selected ? "Selected: " + button.text : "Show " + button.text.ToLowerInvariant() + " resources";
            }
            var list = root.Q("resources-list");
            list.Clear();
            int count = 0;
            foreach (var service in catalog.services)
            {
                if (service.id == "gambling-help" || (category != "All" && service.category != category)) continue;
                var card = new VisualElement { name = "resource-" + service.id };
                card.AddToClassList("resource-card");
                AddLabel(card, service.detail, "resource-eyebrow");
                AddLabel(card, service.title, "resource-title");
                AddLabel(card, service.description, "resource-description");
                if (!string.IsNullOrEmpty(service.phone))
                    AddButton(card, "call-" + service.id, "Call " + service.phone, () => callPhone(service.phone), "resource-call");
                AddButton(card, "visit-" + service.id, service.action, () => openWebsite(service.url), "resource-visit");
                list.Add(card);
                count++;
            }
            root.Q<Label>("resources-count").text = category == "All"
                ? "Explore support · " + count + " resources"
                : category + " · " + count + " resources";
        }

        private static void AddLabel(VisualElement parent, string text, string style)
        {
            var label = new Label(text);
            label.AddToClassList(style);
            parent.Add(label);
        }

        private static void AddButton(VisualElement parent, string name, string text, Action action, string style)
        {
            var button = new Button(action) { name = name, text = text };
            button.AddToClassList("resource-button");
            button.AddToClassList(style);
            parent.Add(button);
        }

        public void Dispose()
        {
            root.Q<Button>("resources-call-help").clicked -= callHelp;
            root.Q<Button>("resources-chat").clicked -= openChat;
            for (int i = 0; i < filters.Count; i++) filters[i].clicked -= handlers[i];
            root.Q("resources-list").Clear();
        }
    }
}

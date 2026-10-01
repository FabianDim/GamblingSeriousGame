using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class BasePageScreen : MonoBehaviour
    {
        private static readonly string[] Destinations = { "home", "interventions", "my-pet", "settings", "resources" };
        private static readonly string[] DestinationLabels = { "Home", "Interventions", "My Pet", "Settings", "Resources" };
        [SerializeField] private string initialDestination = "home";
        [SerializeField] private UnityEvent<string> onNavigate = new UnityEvent<string>();
        private readonly Button[] buttons = new Button[5];
        private readonly Action[] handlers = new Action[5];
        private VisualElement root, page, safeArea;
        private IVisualElementScheduledItem safeAreaUpdate;

        public string SelectedDestination { get; private set; }
        public VisualElement Content => root?.Q("page-content");
        public UnityEvent<string> OnNavigate => onNavigate;

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            page = root.Q("base-page");
            safeArea = root.Q("base-safe-area");
            for (int i = 0; i < Destinations.Length; i++)
            {
                string destination = Destinations[i];
                buttons[i] = root.Q<Button>("dock-" + destination);
                handlers[i] = () => Navigate(destination);
                if (buttons[i] != null) buttons[i].clicked += handlers[i];
            }
            SelectDestination(SelectedDestination ?? initialDestination);
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            safeAreaUpdate = root.schedule.Execute(ApplySafeArea).Every(250);
            ApplySafeArea();
        }

        private void OnDisable()
        {
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].clicked -= handlers[i];
            root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            safeAreaUpdate?.Pause();
            root = page = safeArea = null;
        }

        public void Navigate(string destination)
        {
            if (Array.IndexOf(Destinations, destination) < 0) return;
            SelectDestination(destination);
            onNavigate.Invoke(destination);
        }

        /// <summary>Synchronize the active tab without firing another navigation event.</summary>
        public void SelectDestination(string destination)
        {
            if (Array.IndexOf(Destinations, destination) < 0) destination = "home";
            SelectedDestination = destination;
            for (int i = 0; i < buttons.Length; i++)
                buttons[i]?.EnableInClassList("dock-button--selected", Destinations[i] == destination);
            var title = root?.Q<Label>("base-page-title");
            if (title != null) title.text = DestinationLabels[Array.IndexOf(Destinations, destination)];
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) => ApplySafeArea();

        private void ApplySafeArea()
        {
            if (safeArea == null || root.panel == null || Screen.width <= 0 || Screen.height <= 0) return;
            float x = root.resolvedStyle.width / Screen.width;
            float y = root.resolvedStyle.height / Screen.height;
            if (float.IsNaN(x) || float.IsNaN(y) || x <= 0 || y <= 0) return;
            var rect = Screen.safeArea;
            safeArea.style.paddingLeft = rect.xMin * x;
            safeArea.style.paddingRight = (Screen.width - rect.xMax) * x;
            safeArea.style.paddingTop = (Screen.height - rect.yMax) * y;
            safeArea.style.paddingBottom = rect.yMin * y;
            page?.EnableInClassList("app--compact", rect.height * y < 600);
        }
    }
}

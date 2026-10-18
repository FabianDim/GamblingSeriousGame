using UnityEngine;
using UnityEngine.UIElements;
using GamblingSeriousGame.Data;
using System.Collections.Generic;

namespace GamblingSeriousGame.UI
{
    [RequireComponent(typeof(UIDocument), typeof(BasePageScreen))]
    [DefaultExecutionOrder(100)]
    public sealed class InterventionsScreen : MonoBehaviour
    {
        private InterventionsView view;
        private GroundingView grounding;
        private VisualElement pageShell;
        private StyleEnum<DisplayStyle> previousShellDisplay;
        private InterventionDetailView detail;
        private InterventionActivity activeActivity;
        private GameStateManager state;
        private bool paused;
        private bool skipTimerFrame = true;
        private readonly Dictionary<string, InterventionSession> sessions = new Dictionary<string, InterventionSession>();

        private void OnEnable()
        {
            skipTimerFrame = true;
            var root = GetComponent<UIDocument>().rootVisualElement;
            var content = root.Q("page-content");
            if (content == null)
            {
                Debug.LogWarning("InterventionsScreen requires the InterventionsScreen layout.", this);
                return;
            }
            pageShell = root.Q("page-shell");
            var safeArea = root.Q("base-safe-area");
            if (pageShell == null || safeArea == null) return;
            previousShellDisplay = pageShell.style.display;
            state = FindAnyObjectByType<GameStateManager>();
            grounding = new GroundingView(safeArea, CloseGrounding);
            detail = new InterventionDetailView(safeArea, CloseActivity, RestartActivity,
                () => state != null ? state.PetUID : null, () => state != null ? state.SavedPet : null);
            view = new InterventionsView(content, OpenGrounding, OpenActivity);
            GetComponent<BasePageScreen>().SelectDestination("interventions");
        }

        private void Update()
        {
            // Never count the frame spanning a focus change or app suspension.
            if (skipTimerFrame) { skipTimerFrame = false; return; }
            detail?.Advance(Time.unscaledDeltaTime, Application.isFocused && !paused);
        }

        private void OnApplicationFocus(bool value) => skipTimerFrame = true;
        private void OnApplicationPause(bool value) { paused = value; skipTimerFrame = true; }

        private string SessionKey(InterventionActivity activity) => (state != null ? state.PetUID : "") + ":" + activity.id;

        private void OpenActivity(InterventionActivity activity)
        {
            if (!activity.HasInstructions) return;
            activeActivity = activity;
            string key = SessionKey(activity);
            if (!sessions.TryGetValue(key, out var session))
            {
                session = new InterventionSession(activity, state != null ? state.PetUID : null);
                sessions.Add(key, session);
            }
            pageShell.style.display = DisplayStyle.None;
            detail.Open(activity, session);
        }

        private void CloseActivity()
        {
            detail.Hide();
            pageShell.style.display = previousShellDisplay;
            view.Root.Q<Button>("start-" + activeActivity.id)?.Focus();
        }

        private void RestartActivity()
        {
            if (activeActivity == null || detail.Session?.Claimed != true) return;
            sessions.Remove(SessionKey(activeActivity));
            OpenActivity(activeActivity);
        }

        private void OpenGrounding()
        {
            pageShell.style.display = DisplayStyle.None;
            grounding.OpenRandom();
        }

        private void CloseGrounding()
        {
            grounding.Root.style.display = DisplayStyle.None;
            pageShell.style.display = previousShellDisplay;
            view.Root.Q<Button>("open-grounding")?.Focus();
        }

        private void OnDisable()
        {
            if (pageShell != null) pageShell.style.display = previousShellDisplay;
            detail?.Hide();
            detail?.Root.RemoveFromHierarchy();
            detail = null;
            activeActivity = null;
            grounding?.Root.RemoveFromHierarchy();
            grounding = null;
            pageShell = null;
            view?.Root.RemoveFromHierarchy();
            view = null;
        }
    }
}

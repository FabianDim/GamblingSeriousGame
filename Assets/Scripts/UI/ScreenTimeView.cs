using System;
using System.Collections.Generic;
using GamblingSeriousGame.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    /// <summary>Renders a usage snapshot; does not read device activity or request permissions.</summary>
    public sealed class ScreenTimeView
    {
        private readonly VisualElement root;
        public ScreenTimeView(VisualElement root) { this.root = root; }

        public void Render(UsageSnapshot snapshot, UsagePeriod period, bool example, bool canRefresh)
        {
            snapshot = snapshot ?? new UsageSnapshot();
            bool ready = snapshot.status == UsageStatus.Ready;
            root.Q<Button>("usage-today").EnableInClassList("usage-period--selected", period == UsagePeriod.Today);
            root.Q<Button>("usage-week").EnableInClassList("usage-period--selected", period == UsagePeriod.LastSevenDays);
            var badge = root.Q<Label>("usage-badge");
            badge.text = example ? "Example data" : ready ? "Connected" : StatusLabel(snapshot.status);
            badge.EnableInClassList("usage-badge--example", example);
            root.Q<Button>("usage-example").text = example ? "Exit example" : "Preview example";
            Show("usage-refresh", canRefresh && !example);
            root.Q<Button>("usage-refresh").SetEnabled(snapshot.status != UsageStatus.Loading);
            var apps = root.Q("usage-apps");
            var chart = root.Q("usage-chart");
            apps.Clear();
            chart.Clear();
            Show("usage-app-section", false);
            Show("usage-chart-section", false);
            Text("usage-updated", ready && !example ? snapshot.updatedAtLabel ?? "" : "");
            Show("usage-updated", ready && !example && !string.IsNullOrWhiteSpace(snapshot.updatedAtLabel));

            if (!ready)
            {
                Text("usage-total", "—");
                Text("usage-summary", snapshot.status == UsageStatus.Loading ? "Updating your usage" : "Your usage will appear here");
                Text("usage-status", StatusMessage(snapshot.status));
                return;
            }

            var entries = new List<AppUsageEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in snapshot.apps ?? Array.Empty<AppUsageEntry>())
                if (entry != null && !string.IsNullOrWhiteSpace(entry.appId) &&
                    !string.IsNullOrWhiteSpace(entry.displayName) && ids.Add(entry.appId)) entries.Add(entry);
            entries.Sort((a, b) => b.foregroundSeconds.CompareTo(a.foregroundSeconds));
            long total = 0;
            long maximum = period == UsagePeriod.Today ? 86400 : 604800;
            foreach (var entry in entries)
            {
                long seconds = Math.Min(maximum, Math.Max(0, entry.foregroundSeconds));
                total += seconds;
                var row = new VisualElement();
                row.AddToClassList("usage-app-row");
                row.Add(Label(entry.displayName.Substring(0, 1).ToUpperInvariant(), "usage-app-symbol"));
                row.Add(Label(entry.displayName, "usage-app-name"));
                row.Add(Label(FormatDuration(seconds), "usage-app-time"));
                apps.Add(row);
            }
            Text("usage-total", FormatDuration(total));
            Text("usage-summary", total == 0 ? "No recorded use in this period" :
                $"Across {entries.Count} sports betting app{(entries.Count == 1 ? "" : "s")}");
            Text("usage-status", example ? "Example data only — this is not your app usage." :
                "Time spent in your tracked sports betting apps.");
            Show("usage-app-section", entries.Count > 0);
            var days = snapshot.days;
            bool hasWeek = period == UsagePeriod.LastSevenDays && days != null && days.Length == 7 &&
                Array.TrueForAll(days, day => day != null && !string.IsNullOrWhiteSpace(day.label));
            if (!hasWeek) return;
            long peak = 1;
            foreach (var day in days) peak = Math.Max(peak, Math.Min(86400, Math.Max(0, day.foregroundSeconds)));
            foreach (var day in days)
            {
                long seconds = Math.Min(86400, Math.Max(0, day.foregroundSeconds));
                var column = new VisualElement { tooltip = day.label + ": " + FormatDuration(seconds) };
                column.AddToClassList("usage-day");
                var track = new VisualElement();
                track.AddToClassList("usage-bar-track");
                var bar = new VisualElement();
                bar.AddToClassList("usage-bar");
                bar.style.height = Length.Percent(100f * seconds / peak);
                track.Add(bar);
                column.Add(track);
                column.Add(Label(day.label, "usage-day-label"));
                column.Add(Label(FormatDuration(seconds), "usage-day-value"));
                chart.Add(column);
            }
            Show("usage-chart-section", true);
        }

        public static string FormatDuration(long seconds)
        {
            if (seconds <= 0) return "0 min";
            if (seconds < 60) return "<1 min";
            long minutes = seconds / 60;
            return minutes < 60 ? minutes + " min" : minutes % 60 == 0 ? minutes / 60 + " hr" :
                minutes / 60 + " hr " + minutes % 60 + " min";
        }

        private static Label Label(string text, string style)
        {
            var label = new Label(text) { enableRichText = false };
            label.AddToClassList(style);
            return label;
        }
        private void Text(string name, string value)
        {
            var label = root.Q<Label>(name);
            label.enableRichText = false;
            label.text = value;
        }
        private void Show(string name, bool visible) => root.Q(name).style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        private static string StatusLabel(UsageStatus status)
        {
            switch (status)
            {
                case UsageStatus.Loading: return "Updating";
                case UsageStatus.PermissionRequired: return "Access needed";
                case UsageStatus.Unavailable: return "Unavailable";
                case UsageStatus.Error: return "Try again";
                default: return "Not connected";
            }
        }
        private static string StatusMessage(UsageStatus status)
        {
            switch (status)
            {
                case UsageStatus.Loading: return "Waiting for the latest screen-time data…";
                case UsageStatus.PermissionRequired: return "Screen-time access is needed before usage can be shown.";
                case UsageStatus.Unavailable: return "Screen-time tracking is not available on this device.";
                case UsageStatus.Error: return "Usage couldn't be updated. Try refreshing in a moment.";
                default: return "Screen-time tracking isn't connected yet. No app activity is being read.";
            }
        }
    }
}

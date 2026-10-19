using System;

namespace GamblingSeriousGame.Data
{
    public enum UsagePeriod { Today, LastSevenDays }
    public enum UsageStatus { NotConnected, Loading, Ready, PermissionRequired, Unavailable, Error }

    [Serializable]
    public sealed class AppUsageEntry
    {
        public string appId;
        public string displayName;
        public long foregroundSeconds;
    }

    [Serializable]
    public sealed class DailyUsageEntry
    {
        public string label;
        public long foregroundSeconds;
    }

    /// <summary>Aggregated sports-betting app usage for the requested local-date period.</summary>
    [Serializable]
    public sealed class UsageSnapshot
    {
        public UsageStatus status;
        public AppUsageEntry[] apps = Array.Empty<AppUsageEntry>();
        // Oldest to newest. A missing day is unknown, not zero; supply all seven to show a chart.
        public DailyUsageEntry[] days = Array.Empty<DailyUsageEntry>();
        public string updatedAtLabel;
    }

    /// <summary>Future platform adapter. Publish Changed on Unity's main thread; snapshots must be non-blocking.</summary>
    public interface IScreenTimeUsageProvider
    {
        event Action Changed;
        UsageSnapshot GetSnapshot(UsagePeriod period);
        void Refresh();
    }

    /// <summary>Explicitly requested UI examples only; never a fallback for missing real usage.</summary>
    public static class ScreenTimeExamples
    {
        public static UsageSnapshot Create(UsagePeriod period)
        {
            bool today = period == UsagePeriod.Today;
            var days = new DailyUsageEntry[7];
            int[] minutes = { 22, 35, 18, 41, 16, 24, 27 };
            for (int i = 0; i < days.Length; i++)
                days[i] = new DailyUsageEntry
                {
                    label = DateTime.Today.AddDays(i - 6).ToString("ddd"),
                    foregroundSeconds = minutes[i] * 60
                };
            return new UsageSnapshot
            {
                status = UsageStatus.Ready,
                apps = new[]
                {
                    new AppUsageEntry { appId = "example-a", displayName = "Sports app A", foregroundSeconds = (today ? 18 : 112) * 60 },
                    new AppUsageEntry { appId = "example-b", displayName = "Sports app B", foregroundSeconds = (today ? 9 : 71) * 60 }
                },
                days = days
            };
        }
    }
}

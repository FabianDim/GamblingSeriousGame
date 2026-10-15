using System;
using GamblingSeriousGame.UI;

namespace GamblingSeriousGame.Data
{
    // One timed practice for one pet. Only foreground time on its detail page advances it.
    public sealed class InterventionSession
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string PetId { get; }
        public int RewardXp { get; }
        public double DurationSeconds { get; }
        public double ElapsedSeconds { get; private set; }
        public double RemainingSeconds => Math.Max(0, DurationSeconds - ElapsedSeconds);
        public bool Claimed { get; private set; }
        public bool Ready => !Claimed && RemainingSeconds <= 0;

        public InterventionSession(InterventionActivity activity, string petId)
        {
            if (activity == null || !activity.HasInstructions)
                throw new ArgumentException("An intervention needs valid instructions, duration and XP.", nameof(activity));
            PetId = Guid.TryParse(petId, out var id) && id != Guid.Empty ? id.ToString() : string.Empty;
            RewardXp = activity.rewardXp;
            DurationSeconds = activity.minutes * 60d;
        }

        public void Advance(double seconds)
        {
            if (Claimed || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            ElapsedSeconds = Math.Min(DurationSeconds, ElapsedSeconds + seconds);
        }

        public bool TryClaim(string currentPetId, PetSaveData pet, out string message)
        {
            if (Claimed) { message = "XP already collected for this practice."; return false; }
            if (!Ready) { message = "Spend a little more time with this activity first."; return false; }
            if (string.IsNullOrEmpty(PetId) || !Guid.TryParse(currentPetId, out var current) || current.ToString() != PetId)
            {
                message = "Return to your original pet to collect this XP.";
                return false;
            }
            if (!PetSaveStore.TryAwardInterventionXp(PetId, pet, Id, RewardXp, out message)) return false;
            Claimed = true;
            return true;
        }
    }
}

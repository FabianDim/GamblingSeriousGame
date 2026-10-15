// Copy beside InterventionsValidation.cs in the isolated validation project's Assets/Editor.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.UI;
using UnityEngine;
using UnityEngine.UIElements;

public static class InterventionRewardsValidation
{
    public static void Check()
    {
        Require(Application.productName.StartsWith("PetShopValidation"), "Isolated saves required");
        var activities = JsonUtility.FromJson<InterventionCatalog>(Resources.Load<TextAsset>(InterventionsView.ResourcePath).text).activities;
        Require(activities.Length == 5 && activities.All(a => a.HasInstructions && a.rewardXp == a.minutes * 5), "Five complete activities at 5 XP/minute");
        var profile = new PetProfile { uniquePlayerPetID = Guid.NewGuid().ToString(), animalId = "cat", breedId = "black", petName = "Practice" };
        var pet = PetSaveStore.SaveProfile(profile);
        pet.health = 72;
        PetSaveStore.SavePet(profile.uniquePlayerPetID, pet);
        var activity = activities[1];
        var session = new InterventionSession(activity, profile.uniquePlayerPetID);
        Require(session.DurationSeconds == 180 && session.RewardXp == 15, "Three minutes awards 15 XP");
        session.Advance(double.NaN);
        session.Advance(double.PositiveInfinity);
        session.Advance(-1);
        Require(session.ElapsedSeconds == 0, "Invalid elapsed time ignored");
        session.Advance(179.5);
        Require(!session.TryClaim(profile.uniquePlayerPetID, pet, out _) && pet.experience == 0, "No early claim");
        session.Advance(0.5);
        Require(session.Ready && !session.TryClaim(Guid.NewGuid().ToString(), pet, out _), "Cannot award another pet");

        // A failed replacement leaves the live pet untouched and permits retry.
        string temporary = PetSaveStore.SavePath + ".tmp";
        Require(!File.Exists(temporary) && !Directory.Exists(temporary), "No pending temporary save");
        Directory.CreateDirectory(temporary);
        try
        {
            Require(!session.TryClaim(profile.uniquePlayerPetID, pet, out _) && !session.Claimed && pet.experience == 0, "Failed save grants no XP");
        }
        finally { Directory.Delete(temporary); }
        Require(session.TryClaim(profile.uniquePlayerPetID, pet, out _) && session.Claimed && pet.experience == 15, "Retry saves reward");
        Require(!session.TryClaim(profile.uniquePlayerPetID, pet, out _) && pet.experience == 15, "Double claim rejected");
        var reloaded = PetSaveStore.LoadAll()[profile.uniquePlayerPetID];
        Require(reloaded.experience == 15 && reloaded.health == 72 && reloaded.claimedInterventionSessions.Contains(session.Id), "XP and receipt persisted together, health preserved");

        var award = typeof(PetSaveStore).GetMethod("TryAwardInterventionXp", BindingFlags.Static | BindingFlags.NonPublic);
        var args = new object[] { profile.uniquePlayerPetID, reloaded, session.Id, 15, null };
        Require((bool)award.Invoke(null, args) && PetSaveStore.LoadAll()[profile.uniquePlayerPetID].experience == 15, "Saved receipt prevents replay");
        var item = ShopCatalog.Items.First(i => i.RequiredXp > 0 && i.RequiredXp <= 15);
        Require(PetSaveStore.TryClaimItem(profile.uniquePlayerPetID, reloaded, item.Id, out _), "Earned XP unlocks shop item");
        Require(PetSaveStore.LoadAll()[profile.uniquePlayerPetID].claimedInterventionSessions.Contains(session.Id), "Shop preserves reward receipts");
        Require(PetSaveStore.SaveProfile(profile).claimedInterventionSessions.Contains(session.Id), "Profile edits preserve receipts");

        string saved = File.ReadAllText(PetSaveStore.SavePath);
        try
        {
            File.WriteAllText(PetSaveStore.SavePath, "{\"" + profile.uniquePlayerPetID + "\":{\"experience\":42}}");
            Require(PetSaveStore.LoadAll()[profile.uniquePlayerPetID].claimedInterventionSessions.Count == 0, "Old saves migrate with empty receipts");
            File.WriteAllText(PetSaveStore.SavePath, "broken json");
            var failed = new InterventionSession(activity, profile.uniquePlayerPetID);
            failed.Advance(180);
            Require(!failed.TryClaim(profile.uniquePlayerPetID, reloaded, out _) && !failed.Claimed && reloaded.experience == 15, "Corrupt saves preserved without granting XP");
            Require(File.ReadAllText(PetSaveStore.SavePath) == "broken json", "Bad file not overwritten");
        }
        finally { File.WriteAllText(PetSaveStore.SavePath, saved); }

        var second = new InterventionSession(activity, profile.uniquePlayerPetID);
        var view = new InterventionDetailView(new VisualElement(), () => { }, () => { }, () => profile.uniquePlayerPetID, () => reloaded);
        view.Open(activity, second);
        view.Advance(20, true);
        view.Advance(300, false);
        view.Hide();
        view.Advance(300, true);
        Require(second.ElapsedSeconds == 20, "Hidden and background time does not count");
        view.Open(activity, second);
        view.Advance(159, true);
        Require(!view.Root.Q<Button>("activity-collect").enabledSelf, "Collect disabled until full duration");
        view.Advance(1, true);
        Require(view.Root.Q<Button>("activity-collect").enabledSelf, "Collect enabled at duration");
        reloaded.experience = 0; // Stale live state must not overwrite saved XP.
        Require(second.TryClaim(profile.uniquePlayerPetID, reloaded, out _) && reloaded.experience == 30, "Repeated practice adds to saved XP, not stale live XP");
        var missing = new InterventionSession(activity, Guid.NewGuid().ToString());
        missing.Advance(180);
        Require(!missing.TryClaim(missing.PetId, new PetSaveData(), out _), "Missing pet does not create an XP-only save");
        reloaded.experience = int.MaxValue;
        PetSaveStore.SavePet(profile.uniquePlayerPetID, reloaded);
        var overflow = new InterventionSession(activity, profile.uniquePlayerPetID);
        overflow.Advance(180);
        Require(!overflow.TryClaim(profile.uniquePlayerPetID, reloaded, out _) && reloaded.experience == int.MaxValue, "XP overflow rejected");

        reloaded.experience = 0;
        PetSaveStore.SavePet(profile.uniquePlayerPetID, reloaded);
        PlayerPrefs.SetString(GameStateManager.SavedProfileKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
        Debug.Log("INTERVENTION_REWARDS_PASS: timer, pause/resume, save/reload, retries, replay protection, migration, shop, overflow");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}

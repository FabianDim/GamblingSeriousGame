using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace GamblingSeriousGame.Data
{
    [DataContract]
    public sealed class PetItemPlacement
    {
        [DataMember] public string itemId;
        [DataMember] public float x;
        [DataMember] public float y;
    }

    [DataContract]
    public sealed class PetSaveData
    {
        [DataMember] public string animalId;
        [DataMember] public string breedId;
        [DataMember] public string petName;
        [DataMember] public int backgroundId = 1;
        [DataMember] public int level = 1;
        [DataMember] public int health = 100;
        [DataMember] public int maxHealth = 100;
        [DataMember] public int experience;
        [DataMember] public List<string> ownedItems = new List<string>();
        [DataMember] public List<PetItemPlacement> placedItems = new List<PetItemPlacement>();
        [DataMember] public List<string> claimedInterventionSessions = new List<string>();

        [OnDeserializing]
        private void SetDefaults(StreamingContext context)
        {
            // Data contract deserialization does not run field initializers.
            backgroundId = 1;
            ownedItems = new List<string>();
            placedItems = new List<PetItemPlacement>();
            claimedInterventionSessions = new List<string>();
        }
    }

    public static class PetSaveStore
    {
        // A new player build starts fresh; relaunching that build keeps its progress.
        // Preserve the editor's existing save location for play-mode development.
        public static string SaveDirectory => Application.isEditor
            ? Application.persistentDataPath
            : Path.Combine(Application.persistentDataPath, "Builds", Application.buildGUID);

        public static string SavedProfileKey => Application.isEditor
            ? "Companion.Profile.v1"
            : "Companion.Profile.v1." + Application.buildGUID;

        public static string SavePath => Path.Combine(SaveDirectory, "pets.json");

        public static void EnsureExists()
        {
            if (!File.Exists(SavePath)) Write(new Dictionary<string, PetSaveData>());
        }

        private static DataContractJsonSerializer CreateSerializer() =>
            new DataContractJsonSerializer(typeof(Dictionary<string, PetSaveData>),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        public static Dictionary<string, PetSaveData> LoadAll()
        {
            EnsureExists();
            using (var stream = File.OpenRead(SavePath))
            {
                // Never replace a damaged save with an empty collection.
                return (Dictionary<string, PetSaveData>)CreateSerializer().ReadObject(stream)
                    ?? throw new SerializationException("The pet save must contain a JSON object.");
            }
        }

        // Updating a profile preserves the pet's existing progression and every other pet.
        public static PetSaveData SaveProfile(UI.PetProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string id = NormalizeId(profile.uniquePlayerPetID);
            var pets = LoadAll();
            if (!pets.TryGetValue(id, out var pet) || pet == null) pet = new PetSaveData();
            pet.animalId = profile.animalId;
            pet.breedId = profile.breedId;
            pet.petName = profile.petName;
            pet.backgroundId = profile.backgroundId > 0 ? profile.backgroundId : 1;
            pets[id] = pet;
            Write(pets);
            return pet;
        }

        public static void SavePet(string uuid, PetSaveData pet)
        {
            if (pet == null) throw new ArgumentNullException(nameof(pet));
            string id = NormalizeId(uuid);
            var pets = LoadAll();
            pets[id] = pet;
            Write(pets);
        }

        // XP is an unlock threshold, never a currency. Save before changing live state.
        public static bool TryClaimItem(string uuid, PetSaveData pet, string itemId, out string message)
        {
            var item = ShopCatalog.Find(itemId);
            if (pet == null || !Guid.TryParse(uuid, out var id) || id == Guid.Empty)
            {
                message = "Choose a pet before collecting items.";
                return false;
            }
            if (item == null)
            {
                message = "This item is unavailable.";
                return false;
            }
            if (pet.ownedItems?.Contains(item.Id) == true)
            {
                message = "You already own this item.";
                return false;
            }
            if (pet.experience < item.RequiredXp)
            {
                message = $"Reach {item.RequiredXp} XP to unlock this item.";
                return false;
            }

            var owned = new List<string>(pet.ownedItems ?? new List<string>()) { item.Id };
            var updated = new PetSaveData
            {
                animalId = pet.animalId, breedId = pet.breedId, petName = pet.petName,
                backgroundId = pet.backgroundId, level = pet.level, health = pet.health,
                maxHealth = pet.maxHealth, experience = pet.experience, ownedItems = owned,
                placedItems = new List<PetItemPlacement>(pet.placedItems ?? new List<PetItemPlacement>()),
                claimedInterventionSessions = new List<string>(pet.claimedInterventionSessions ?? new List<string>())
            };
            try { SavePet(uuid, updated); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is SerializationException || exception is System.Xml.XmlException)
            {
                Debug.LogWarning("Could not save shop item: " + exception.Message);
                message = "Couldn't save this item. Please try again.";
                return false;
            }
            pet.ownedItems = owned;
            message = item.Name + " added to your collection!";
            return true;
        }

        // A null position returns an item to inventory. Persist before changing the live room.
        public static bool TryPlaceItem(string uuid, PetSaveData pet, string itemId,
            Vector2? position, out string message)
        {
            var item = ShopCatalog.Find(itemId);
            if (pet == null || !Guid.TryParse(uuid, out var id) || id == Guid.Empty ||
                item?.Sprite == null || pet.ownedItems?.Contains(itemId) != true)
            {
                message = "Collect this item in the shop first.";
                return false;
            }
            var placements = new List<PetItemPlacement>(pet.placedItems ?? new List<PetItemPlacement>());
            placements.RemoveAll(entry => entry == null || entry.itemId == itemId);
            if (position.HasValue)
            {
                var bounds = Gameplay.RoomLayout.ItemBounds(item, position.Value);
                if (!Gameplay.RoomLayout.Contains(Gameplay.RoomLayout.ItemArea, bounds))
                {
                    message = "Keep the whole item inside the highlighted area.";
                    return false;
                }
                foreach (var entry in placements)
                {
                    var other = ShopCatalog.Find(entry.itemId);
                    if (other?.Sprite != null && bounds.Overlaps(Gameplay.RoomLayout.ItemBounds(other, new Vector2(entry.x, entry.y))))
                    {
                        message = "Leave space between items.";
                        return false;
                    }
                }
                placements.Add(new PetItemPlacement { itemId = itemId, x = position.Value.x, y = position.Value.y });
            }
            var updated = new PetSaveData
            {
                animalId = pet.animalId, breedId = pet.breedId, petName = pet.petName,
                backgroundId = pet.backgroundId, level = pet.level, health = pet.health,
                maxHealth = pet.maxHealth, experience = pet.experience,
                ownedItems = pet.ownedItems, placedItems = placements,
                claimedInterventionSessions = pet.claimedInterventionSessions
            };
            try { SavePet(uuid, updated); }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is SerializationException || exception is System.Xml.XmlException)
            {
                Debug.LogWarning("Could not save room: " + exception.Message);
                message = "Couldn't save your room. Please try again.";
                return false;
            }
            pet.placedItems = placements;
            message = position.HasValue ? item.Name + " placed!" : item.Name + " returned to inventory.";
            return true;
        }

        // The receipt and XP are written together. Retrying a claim cannot grant it twice.
        internal static bool TryAwardInterventionXp(string uuid, PetSaveData livePet,
            string sessionId, int rewardXp, out string message)
        {
            if (livePet == null || rewardXp <= 0 || !Guid.TryParse(uuid, out var id) || id == Guid.Empty ||
                !Guid.TryParse(sessionId, out var receipt) || receipt == Guid.Empty)
            {
                message = "Choose a saved pet before collecting XP.";
                return false;
            }
            try
            {
                var pets = LoadAll();
                if (!pets.TryGetValue(id.ToString(), out var saved) || saved == null)
                {
                    message = "Your pet could not be found. Return to My Pet and try again.";
                    return false;
                }
                var receipts = saved.claimedInterventionSessions ?? new List<string>();
                if (!receipts.Contains(receipt.ToString()))
                {
                    if (saved.experience > int.MaxValue - rewardXp)
                    {
                        message = "Your pet has reached the XP limit.";
                        return false;
                    }
                    saved.experience = Math.Max(0, saved.experience) + rewardXp;
                    receipts.Add(receipt.ToString());
                    saved.claimedInterventionSessions = receipts;
                    Write(pets);
                }
                livePet.experience = saved.experience;
                livePet.claimedInterventionSessions = new List<string>(receipts);
                message = $"+{rewardXp} XP saved for your pet.";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is SerializationException || exception is System.Xml.XmlException)
            {
                Debug.LogWarning("Could not save intervention XP: " + exception.Message);
                message = "Couldn't save your XP. Please try collecting it again.";
                return false;
            }
        }

        private static string NormalizeId(string uuid)
        {
            if (!Guid.TryParse(uuid, out var id) || id == Guid.Empty)
                throw new ArgumentException("A pet must have a non-empty UUID.", nameof(uuid));
            return id.ToString();
        }

        private static void Write(Dictionary<string, PetSaveData> pets)
        {
            Directory.CreateDirectory(SaveDirectory);
            string temporaryPath = SavePath + ".tmp";
            try
            {
                using (var stream = File.Create(temporaryPath))
                    CreateSerializer().WriteObject(stream, pets);
                if (File.Exists(SavePath)) File.Replace(temporaryPath, SavePath, null);
                else File.Move(temporaryPath, SavePath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}

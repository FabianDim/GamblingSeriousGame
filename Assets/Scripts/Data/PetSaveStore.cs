using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace GamblingSeriousGame.Data
{
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

        [OnDeserializing]
        private void SetDefaults(StreamingContext context)
        {
            // Data contract deserialization does not run field initializers.
            backgroundId = 1;
        }
    }

    public static class PetSaveStore
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "pets.json");

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

        private static string NormalizeId(string uuid)
        {
            if (!Guid.TryParse(uuid, out var id) || id == Guid.Empty)
                throw new ArgumentException("A pet must have a non-empty UUID.", nameof(uuid));
            return id.ToString();
        }

        private static void Write(Dictionary<string, PetSaveData> pets)
        {
            Directory.CreateDirectory(Application.persistentDataPath);
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

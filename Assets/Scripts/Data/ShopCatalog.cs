using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamblingSeriousGame.Data
{
    public sealed class ShopItem
    {
        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public int RequiredXp { get; }
        public Sprite Sprite { get; }

        internal ShopItem(string id, string name, string category, int requiredXp, Sprite sprite)
        {
            Id = id;
            Name = name;
            Category = category;
            RequiredXp = requiredXp;
            Sprite = sprite;
        }
    }

    public static class ShopCatalog
    {
        private const string Root = "Pets/PetMobileGameAsset/";
        private static List<ShopItem> items;
        public static IReadOnlyList<ShopItem> Items => items ?? (items = Load());

        public static ShopItem Find(string id)
        {
            foreach (var item in Items)
                if (item.Id == id) return item;
            return null;
        }

        private static List<ShopItem> Load()
        {
            var result = new List<ShopItem>();
            AddSheet(result, "Furnitures,Playgrounds,Toys", "Furniture & toy", "Furniture & toys", 25);
            AddSheet(result, "BowlSprites", "Pet bowl", "Bowls", 0);
            AddSheet(result, "Birddecoration", "Bird decoration", "Bird decorations", 20);
            AddBunnySheet(result, "BunnyBeds", "Rabbit bed", 25);
            AddBunnySheet(result, "BunnyHomeCages", "Rabbit home", 40);
            AddBunnySheet(result, "BunnyToys", "Rabbit toy", 15);
            AddBunnySheet(result, "BunnyToyBalls", "Toy ball", 10);
            AddBunnySheet(result, "BunnyFoods", "Rabbit food", 0);
            AddBunnySheet(result, "BunnyFlowerfood", "Flower food", 5);
            AddBunnySheet(result, "BunnyVitamins", "Vitamins", 10);
            AddBunnySheet(result, "FoodBowl", "Food bowl", 10);
            AddBunnySheet(result, "FoodBowlEmpty", "Empty food bowl", 10);
            AddBunnySheet(result, "WaterBowls", "Water bowl", 10);
            AddBunnySheet(result, "WaterBowlsEmpty", "Empty water bowl", 10);
            AddBunnySheet(result, "GrassandWoodSnacks", "Grass & wood snack", 5);
            AddBunnySheet(result, "HairBrush", "Grooming brush", 15);
            AddBunnySheet(result, "PetCarryBox", "Pet carrier", 30);
            AddBunnySheet(result, "VitaminPaste", "Vitamin paste", 10);
            return result;
        }

        private static void AddBunnySheet(List<ShopItem> result, string sheet, string name, int requiredXp) =>
            AddSheet(result, "Bunnies/BunnyStuffs/" + sheet, name, "Rabbit supplies", requiredXp);

        private static void AddSheet(List<ShopItem> result, string sheet, string name, string category, int requiredXp)
        {
            var sprites = Resources.LoadAll<Sprite>(Root + sheet);
            Array.Sort(sprites, (a, b) => SpriteIndex(a.name).CompareTo(SpriteIndex(b.name)));
            foreach (var sprite in sprites)
                result.Add(new ShopItem(sheet + "/" + sprite.name,
                    name + " " + (SpriteIndex(sprite.name) + 1), category,
                    requiredXp + SpriteIndex(sprite.name) / 4 * 25, sprite));
        }

        private static int SpriteIndex(string name) =>
            int.TryParse(name.Substring(name.LastIndexOf('_') + 1), out int index) ? index : 0;
    }
}


// Copy into Assets/Editor in an isolated Unity project with the animation script,
// Animals.json, animation assets and referenced textures. Execute Run in batch mode.
using System;
using System.IO;
using GamblingSeriousGame.Pets;
using UnityEditor;
using UnityEngine;

public static class PetAnimationsValidation
{
    [Serializable] private sealed class Catalog { public Animal[] animals; }
    [Serializable] private sealed class Animal { public string id; public Breed[] breeds; }
    [Serializable] private sealed class Breed { public string id; public string animationFolder; }

    public static void Run()
    {
        try
        {
            var catalog = JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Pets/Animals").text);
            int breeds = 0, definitions = 0, frames = 0;
            foreach (var animal in catalog.animals)
            foreach (var breed in animal.breeds)
            {
                breeds++;
                string path = "Assets/Resources/" + breed.animationFolder;
                Require(!string.IsNullOrWhiteSpace(breed.animationFolder) && Directory.Exists(path),
                    animal.id + "/" + breed.id + " folder missing");
                Require(Directory.Exists(path + "/death") && Directory.Exists(path + "/sleep"),
                    "Special folders missing: " + path);
                var all = Resources.LoadAll<PetAnimationDefinition>(breed.animationFolder);
                bool hasArt = animal.id == "cat" || (animal.id == "dog" && breed.id == "retriever");
                Require(hasArt ? all.Length > 0 : all.Length == 0, "Unexpected art mapping: " + path);
                foreach (var definition in all)
                {
                    definitions++;
                    Require(definition.IsPlayable, "Invalid definition: " + AssetDatabase.GetAssetPath(definition) +
                        " texture=" + definition.texture + " size=" + definition.texture?.width + "x" +
                        definition.texture?.height + " frames=" + definition.frameRects?.Length +
                        " first=" + (definition.frameRects?.Length > 0 ? definition.frameRects[0].ToString() : "none") +
                        " fps=" + definition.framesPerSecond);
                    string assetPath = AssetDatabase.GetAssetPath(definition);
                    if (definition.category == PetAnimationCategory.Death)
                        Require(assetPath.StartsWith(path + "/death/") && !definition.loop,
                            "Death must be nested and non-looping: " + assetPath);
                    if (definition.category == PetAnimationCategory.Sleep)
                        Require(assetPath.StartsWith(path + "/sleep/"), "Sleep must be nested: " + assetPath);
                    var sprites = definition.CreateFrames();
                    Require(sprites.Length == definition.frameRects.Length, "Frame count mismatch");
                    foreach (var sprite in sprites)
                    {
                        Require(sprite != null && sprite.texture == definition.texture, "Invalid sprite");
                        frames++;
                        UnityEngine.Object.DestroyImmediate(sprite);
                    }
                }
                var activities = PetAnimationDefinition.Load(breed.animationFolder);
                Require(!hasArt || activities.Length > 1, "Insufficient random activities");
                foreach (var definition in activities)
                    Require(definition.category == PetAnimationCategory.Activity,
                        "Special action leaked into random pool");
                foreach (PetAnimationCategory category in Enum.GetValues(typeof(PetAnimationCategory)))
                    Require(PetAnimationDefinition.Load(breed.animationFolder, category).Length ==
                        Array.FindAll(all, animation => animation.category == category).Length,
                        "Category loader omitted or mixed actions");
            }
            Require(PetAnimationDefinition.Load(null).Length == 0 &&
                PetAnimationDefinition.Load("").Length == 0 &&
                PetAnimationDefinition.Load("Missing/Pet").Length == 0, "Missing-folder handling");
            var invalid = ScriptableObject.CreateInstance<PetAnimationDefinition>();
            Require(!invalid.IsPlayable && invalid.CreateFrames().Length == 0, "Missing texture handling");
            invalid.texture = new Texture2D(64, 64);
            invalid.frameRects = new[] { new Rect(63, 0, 64, 64) };
            Require(!invalid.IsPlayable && invalid.CreateFrames().Length == 0, "Out-of-bounds handling");
            invalid.frameRects = new[] { new Rect(0, 0, 64, 64) };
            invalid.framesPerSecond = float.NaN;
            Require(!invalid.IsPlayable, "Invalid timing handling");
            UnityEngine.Object.DestroyImmediate(invalid.texture);
            UnityEngine.Object.DestroyImmediate(invalid);
            Debug.Log($"PET_ANIMATIONS_VALIDATION_PASS: {breeds} breeds, {definitions} actions, {frames} frames");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

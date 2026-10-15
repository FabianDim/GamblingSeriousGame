// Run in an isolated Unity project with UI scripts, UI and Resources copied.
// Place under Assets/Editor and execute PetPreviewValidation.Run.
using System;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public static class PetPreviewValidation
{
    public static void Run()
    {
        try
        {
            var catalog = JsonUtility.FromJson<PetCatalog>(
                AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Pets/Animals.json").text);
            var image = new Image();
            using (var preview = new PetSpritePreview(image))
            {
                foreach (var pet in catalog.animals)
                {
                    preview.Bind(pet.previewImageFolder, pet.imageResourcePath, 10);
                    Require(preview.HasImage, pet.id + " selection preview");
                }
                foreach (var breed in Array.Find(catalog.animals, pet => pet.id == "cat").breeds)
                {
                    preview.Bind(breed.previewImageFolder, breed.imageResourcePath, 10);
                    Require(preview.HasImage && image.sprite != null && image.sprite.texture != null,
                        breed.id + " breed preview has a valid sprite and texture");
                    Require(preview.CanAnimate, breed.id + " animation frames");
                }

                const string single = "Pets/PreviewImages/Cats/ColourChoices/Black/AllCatsBlack_0";
                preview.Bind(single, null, 10);
                Require(preview.HasImage && !preview.CanAnimate, "Single sprite stays visible");
                preview.Bind("Missing/Preview", single, 10);
                Require(preview.HasImage && !preview.CanAnimate, "Still fallback stays visible");
                preview.Bind("Missing/Preview", null, 10);
                Require(!preview.HasImage && !preview.CanAnimate, "Missing art clears previous preview");
                preview.Bind("", null, 10);
                Require(!preview.HasImage, "Empty path does not load unrelated Resources");
            }
            Debug.Log("PET_PREVIEW_VALIDATION_PASS: selection, four cat breeds, single sprite, fallback and missing art");
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

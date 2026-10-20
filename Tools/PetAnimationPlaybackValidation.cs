// Run in an isolated Unity project with Scripts and Resources copied.
// Place under Assets/Editor and execute PetAnimationPlaybackValidation.Run.
using System;
using GamblingSeriousGame.Pets;
using GamblingSeriousGame.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PetAnimationPlaybackValidation : EditorWindow
{
    private static PetAnimationPlaybackValidation window;
    private static Image image;
    private static PetSpritePreview preview;
    private static PetAnimationDefinition action;
    private static Texture2D texture;
    private static Sprite first, last;
    private static double deadline, phaseStart;
    private static int phase;
    private static bool sawLast;

    public static void Run()
    {
        try
        {
            window = CreateInstance<PetAnimationPlaybackValidation>();
            window.Show();
            image = new Image();
            window.rootVisualElement.Add(image);
            texture = new Texture2D(192, 64);
            action = CreateInstance<PetAnimationDefinition>();
            action.texture = texture;
            action.frameRects = new[] { new Rect(0, 0, 64, 64), new Rect(64, 0, 64, 64), new Rect(128, 0, 64, 64) };
            action.framesPerSecond = 10;
            action.loop = false;
            preview = new PetSpritePreview(image);
            preview.SetPlaying(false);
            preview.Bind(action);
            first = image.sprite;
            Require(first != null && first.rect.x == 0 && preview.CanAnimate, "Bind displays first frame");
            phase = 0;
            phaseStart = EditorApplication.timeSinceStartup;
            deadline = phaseStart + 10;
            EditorApplication.update += Tick;
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Tick()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            Require(now < deadline, "Playback scheduler did not complete within ten seconds");
            switch (phase)
            {
                case 0:
                    Require(image.sprite == first, "Binding preserves an explicit pause");
                    if (now - phaseStart < 0.3) return;
                    preview.SetPlaying(true);
                    phase = 1;
                    break;
                case 1:
                    if (image.sprite.rect.x != 128) return;
                    last = image.sprite;
                    phaseStart = now;
                    phase = 2;
                    break;
                case 2:
                    Require(image.sprite == last, "Non-looping actions hold the last frame");
                    if (now - phaseStart < 0.3) return;
                    action.loop = true;
                    preview.Bind(action);
                    Require(first == null && last == null, "Rebinding releases generated sprites");
                    sawLast = false;
                    phase = 3;
                    break;
                case 3:
                    if (image.sprite.rect.x == 128) sawLast = true;
                    if (!sawLast || image.sprite.rect.x != 0) return;
                    var generated = image.sprite;
                    const string importedPath = "Pets/PreviewImages/Cats/ColourChoices/Black/AllCatsBlack_0";
                    preview.Bind(importedPath, null, 10);
                    Require(generated == null, "Resource binding also frees generated frames");
                    var imported = image.sprite;
                    Require(imported != null && !preview.CanAnimate, "Existing single-sprite binding still works");
                    preview.Dispose();
                    Require(imported != null && Resources.Load<Sprite>(importedPath) != null,
                        "Disposal does not destroy imported assets");
                    preview.Bind(action);
                    generated = image.sprite;
                    preview.Bind((PetAnimationDefinition)null);
                    Require(generated == null && !preview.HasImage && !preview.CanAnimate,
                        "Null definition clears stale frames and playback");
                    action.frameRects = new[] { new Rect(190, 0, 64, 64) };
                    preview.Bind(action);
                    Require(!preview.HasImage && !preview.CanAnimate, "Invalid definition remains empty");
                    Finish(null);
                    break;
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Finish(Exception exception)
    {
        EditorApplication.update -= Tick;
        preview?.Dispose();
        if (action != null) DestroyImmediate(action);
        if (texture != null) DestroyImmediate(texture);
        if (window != null) window.Close();
        if (exception != null) Debug.LogException(exception);
        else Debug.Log("PET_ANIMATION_PLAYBACK_VALIDATION_PASS: scheduler, pause/resume, looping, final frame, rebinding, ownership and invalid definitions");
        EditorApplication.Exit(exception == null ? 0 : 1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

# Breed animations

Each breed in `Assets/Resources/Pets/Animals.json` has an `animationFolder`, relative
to Unity's `Resources` directory. Its assets live under
`Assets/Resources/Pets/Animations/{animalId}/{breedId}`:

```text
cat/black/
  Idle.asset
  Running.asset
  ...
  death/
    Dead.asset
    Dead1.asset
    Dead2.asset
  sleep/
    Sleeping.asset
```

Each `PetAnimationDefinition` asset represents one action, referencing an existing
texture and an ordered set of frame rectangles. The cat and retriever sheets use
64 × 64 cells, preserving a stable pivot and including detached details such as
sleep bubbles. Artwork remains in the source asset pack; no duplicate textures
or changes to selection previews are needed. Rectangles use Unity's bottom-left
texture origin. Edit timing, looping and category in the Inspector.

The four cat breeds and Golden Retriever have matching artwork and populated
sets. Corgi, Terrier, Lop, Dwarf and Lionhead have reserved folders but no matching
art in this asset pack. Their folders return an empty animation list until art is
added; keep the existing preview fallback in callers. Do not substitute another
breed's artwork silently. `.gitkeep` files preserve empty directories in Git, with
folder metadata for Unity.

For future inactive-screen behaviour:

```csharp
var choices = manager.SelectedPet?.Animations.Activities;
if (choices != null && choices.Count > 0)
{
    var action = choices[UnityEngine.Random.Range(0, choices.Count)];
    preview.Bind(action); // PetSpritePreview: displays and schedules playback immediately.
}
```

`PetSpritePreview.Bind(PetAnimationDefinition)` uses the existing preview scheduler,
respects FPS and looping, and frees its generated sprites when rebound or disposed.
Non-looping actions hold their final frame. An explicitly paused preview remains
paused after binding; use `SetPlaying(true)` to resume it. Imported preview sprites
are never destroyed. `GameplayScreen.PlayPetAnimation(action)` forwards to the
gameplay preview and returns false if the screen or action is unavailable.
`PetRoomView` coordinates roaming and stationary activities through `GameplayScreen`.
After reaching a destination, the pet usually takes a 6–12 second activity break;
otherwise it idles for 2–4 seconds. Its first arrival gets an activity, and two
idle-only stops cannot occur in a row when suitable animations exist. Activities
are chosen from the selected breed's activity and sleep clips, excluding Idle,
Running and Jump. Consecutive activities differ when alternatives are available.
Cats can chill or dance, while retrievers can sniff, sit or lie down; sleep is also
available. Death and reaction-only clips are never selected for these breaks.
Non-looping clips finish and hold their final pose, and every break lasts at least
one full playback. Breeds without suitable clips retain their idle/preview fallback.

The legacy `PetMovement` sequencer yields while a room controller is present, so
the two controllers cannot overwrite each other's animations. Its run/jump/run
sequence remains a fallback outside the room controller.

## Tapping the companion

The gameplay pet responds to mouse clicks and touch through `PetImage`, an
`Image` with alpha-based picking. Its target is the visible part of the current
sprite frame, not a separate fixed box. Transparent pixels and the empty margins
from ScaleToFit do not count. UI Toolkit supplies local pointer coordinates, so
the target follows layout changes, translation and scaling. The sprite mesh and
UVs are used to locate the pixel in the source texture, including sliced/packed
sprites. Alpha masks are cached per texture and released when the screen closes;
unreadable textures use a one-time GPU readback without changing import settings.

`PetReactions.GetMood` uses current health divided by maximum health:

| Health | Mood | Preferred clip |
| --- | --- | --- |
| Below 35% | Sad | Crying, then Sad or Hurt |
| 35% to below 70% | Surprised | Surprised, then Happy or Barking |
| 70% and above | Happy | Happy, then Excited, Surprised or Barking |

Only the selected breed's assets are used. Cats have Happy, Surprised and Crying;
the retriever uses Barking/Hurt when those clips are unavailable. A small mood
bubble follows the pet element. With no matching reaction asset, the bubble
displays for 1.2 seconds while the existing preview is paused.

`GameplayScreen.ReactToPet()` plays the reaction once, overriding looping for
that playback without changing the asset. Further taps and ambient playback
requests are ignored until completion. The room pauses its break timer during
the reaction and while menus are open; the previous activity restarts when the
reaction ends. Tapping does not alter
health, XP or saved data. Closing the screen cancels pending reaction callbacks.
Furniture placement can interrupt a break when the pet needs to move aside.

`PetSpritePreview.Bind(definition, false, onCompleted)` provides one-shot
playback with a completion callback, including single-frame clips. The first
frame now receives a full frame interval. Rebinding/disposal cancels callbacks.

`Tools/PetInteractionValidation.cs` checks transparent/opaque pixels, sprite-sheet
frame changes, unreadable textures, translated/scaled picking, health boundaries,
reaction priority, restoration, missing-art feedback and disable/re-enable cleanup
in an isolated Unity project. Existing preview playback checks remain in
`Tools/PetAnimationPlaybackValidation.cs`.

Both checks passed in Unity 6000.6.2f1. The mouse path was exercised in Unity;
physical Android touch testing is still outstanding. Saved previews:
[happy](UI/Pet-Happy.png) and [sad](UI/Pet-Sad.png).

The picking implementation uses Unity's [VisualElement picking API](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/uielements/visualelement)
and matches the ScaleToFit sprite geometry used by the
[UI Toolkit renderer](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Modules/UIElements/Core/Renderer/UIRMeshGenerator.cs).

## Animation categories and assets

The default loader returns only playable `Activity` definitions. Death and sleep
definitions belong in the lowercase `death` and `sleep` subfolders and must have
the corresponding category. Attacks, hurt and emotional reactions use `Reaction`
and are also excluded from ambient activity. The selected pet caches these
definitions in separate lists; use `manager.SelectedPet.Animations.Sleep` for
sleep actions, and `.Death` or `.Reactions` for those categories.
Never choose from an unfiltered `Resources.LoadAll` result: Unity searches nested
folders too. Cache definitions and generated frames instead of creating sprites
every update. `PetRoomView` coordinates position movement and activity breaks
through `GameplayScreen`; player inactivity detection is not implemented.

New assets can be created with **Assets > Create > Pets > Animation**. Keep one
action per asset and list its frames in playback order. Death clips should not
loop. `Tools/PetAnimationsValidation.cs` checks the catalog, special categories,
frame bounds and runtime frame creation in an isolated Unity project.

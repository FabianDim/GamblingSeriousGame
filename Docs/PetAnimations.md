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
When attached to the gameplay screen, `PetMovement` builds an animation array
from the selected breed: `{ Running, Jump, Running }`. The sequence is one random
choice alongside individual activities, including on startup. When chosen, it
plays all three entries in order, then returns to random selection.
Looping clips run for 10–40 seconds; non-looping
clips advance after one playback, using their frame count and preview FPS.
Breeds missing either clip retain random activity playback. The sequence rebuilds
when the selected pet changes. Enabling the screen starts a fresh random choice.

The default loader returns only playable `Activity` definitions. Death and sleep
definitions belong in the lowercase `death` and `sleep` subfolders and must have
the corresponding category. Attacks, hurt and emotional reactions use `Reaction`
and are also excluded from ambient activity. The selected pet caches these
definitions in separate lists; use `manager.SelectedPet.Animations.Sleep` for
sleep actions, and `.Death` or `.Reactions` for those categories.
Never choose from an unfiltered `Resources.LoadAll` result: Unity searches nested
folders too. Cache definitions and generated frames instead of creating sprites
every update. `PetMovement` controls animation playback through `GameplayScreen`;
position movement and inactivity detection are not implemented yet.

New assets can be created with **Assets > Create > Pets > Animation**. Keep one
action per asset and list its frames in playback order. Death clips should not
loop. `Tools/PetAnimationsValidation.cs` checks the catalog, special categories,
frame bounds and runtime frame creation in an isolated Unity project.

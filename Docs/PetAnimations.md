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
var choices = PetAnimationDefinition.Load(breed.animationFolder);
if (choices.Length > 0)
{
    var action = choices[UnityEngine.Random.Range(0, choices.Length)];
    var frames = action.CreateFrames();
    // Play frames at action.framesPerSecond; respect action.loop.
    // Destroy each generated Sprite when this playback/cache is disposed.
}
```

The default loader returns only playable `Activity` definitions. Death and sleep
definitions belong in the lowercase `death` and `sleep` subfolders and must have
the corresponding category. Attacks, hurt and emotional reactions use `Reaction`
and are also excluded from ambient activity. Load special categories explicitly,
for example `PetAnimationDefinition.Load(breed.animationFolder, PetAnimationCategory.Sleep)`.
Never choose from an unfiltered `Resources.LoadAll` result: Unity searches nested
folders too. Cache definitions and generated frames instead of creating sprites
every update. Movement, inactivity detection and random playback are not wired
into `GameplayScreen` yet.

New assets can be created with **Assets > Create > Pets > Animation**. Keep one
action per asset and list its frames in playback order. Death clips should not
loop. `Tools/PetAnimationsValidation.cs` checks the catalog, special categories,
frame bounds and runtime frame creation in an isolated Unity project.

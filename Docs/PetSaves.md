# Pet saves

`AnimalCatalog` contains `AnimalDefinition` entries such as Cat and Dog. Each
animal definition lists its available `BreedDefinition` options. These shared
catalog entries describe animal types and appearances.

`GameStateManager.SelectedPet` is one resolved player pet, or `null` if no valid
pet was loaded. It exposes:

- `Id`: the pet's UUID.
- `Profile`: a `PetProfile` snapshot containing its identity, name and choices.
- `Animal`: a `PetAnimalType` containing its type and preview, with no breeds array.
- `Breed`: its one selected breed.
- `Animations`: that breed's cached `Activities`, `Sleep`, `Death` and `Reactions`.
- `SaveData`: its mutable `PetSaveData`, including health, XP, owned items and room placements.

The catalog stays in the creation flow and is used to resolve the selected pet
when loading. `SelectedPet` does not retain an `AnimalDefinition`. Its profile
snapshot reads the current name and background from `SaveData`; editing that
snapshot does not change live state. To change animal or breed, edit a profile,
call `PetSaveStore.SaveProfile(profile)`, then `manager.LoadSavedPet()`.
The animation folder is `manager.SelectedPet.Breed.animationFolder`.

Confirmed companions are stored in `pets.json` under `PetSaveStore.SaveDirectory`.
In a player build this is `Application.persistentDataPath/Builds/<build GUID>/`.
Each new Unity build gets its own save directory and selected-companion key, so it
starts at the beginning even on a device with progress from an earlier build.
Relaunching the same build restores its progress. Runtime saves are created outside
the build output and are not packaged with the game.
The Editor continues using `Application.persistentDataPath/pets.json`; existing
Editor and older-build saves are retained but are not imported into new builds.
The build identity comes from Unity's
[`Application.buildGUID`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-buildGUID.html).
`PetSaveStore.SavePath` exposes the full runtime path. The game state manager creates an empty
JSON object (`{}`) at startup if the file is missing, even before a pet has been saved.
The store also creates it on first access, so no Inspector assignment is needed. Existing files are retained;
`Pets.example.json` shows the format and is only an example, not a player save.

The root object maps pet UUIDs to profile details and progression. New pets start at level 1,
100 health, 100 max health, and 0 experience. Saving profile edits preserves existing stats
and other pets. Drafts are not written to this file.

Each profile and saved pet also stores `backgroundId`, defaulting to 1 (including older
saves without the field). The catalog at
`Assets/Resources/Pets/PetMobileGameAsset/Backgrounds/Classic/Backgrounds.json` maps IDs
1–20 to the corresponding PNGs using extension-free Unity Resources paths.
`GameStateManager.BackgroundId` exposes the loaded selection. To change it in a profile,
set `backgroundId` before saving; to change it during gameplay, set
`manager.SelectedPet.SaveData.backgroundId` before calling `SavePet`.

`GameStateManager` also loads the background catalog and selected texture automatically,
exposing `Background` (the resolved definition) and `BackgroundImage` (a `Texture2D`).
It uses background 1 when there is no saved pet or the selected background cannot be loaded.
`Background.id` identifies the image actually loaded when a fallback is used; `BackgroundId`
remains the saved selection. Call `LoadBackground()` after changing the selection during gameplay.

PlayerPrefs records the latest selected companion under `Companion.Profile.v1.<build GUID>`
in player builds, and `Companion.Profile.v1` in the Editor.
`PetSaveStore.SavedProfileKey` supplies the key used by both creation and loading.
`GameStateManager.LoadSavedPet()` resolves that companion as `SelectedPet`.
The UUID chooses the save record; the record supplies the current name, animal,
breed, background and progression, even if PlayerPrefs has older details.
PlayerPrefs-only saves for the current key are added to the file on load.

To persist gameplay changes, update `manager.SelectedPet.SaveData` and call
`PetSaveStore.SavePet(manager.SelectedPet.Id, manager.SelectedPet.SaveData)`.
`LoadAll()` returns all saved pets.
File writes use a temporary file before replacement. Unreadable JSON raises an error and
is preserved instead of being replaced with an empty save.

Timed interventions add XP through `InterventionSession.TryClaim`. Each pet stores
`claimedInterventionSessions`, a list of unique practice-session receipts; old
saves default to an empty list. XP and the receipt are written together so retrying
the same claim cannot add XP twice. The award reads the latest saved XP rather
than overwriting it with a stale displayed total. Profile edits and shop claims
preserve the receipts. Timers and unfinished practice sessions remain in memory;
only claimed rewards persist across game restarts.

Collected shop items are stored in `ownedItems`. Room decorations are stored in
`placedItems`, with one `{ itemId, x, y }` entry per placed item. Coordinates are
normalized centers within the fitted background, so layouts survive resolution
changes. Old saves initialize an empty placement list. `TryPlaceItem` checks
ownership, full-item bounds and other items, then saves before updating live state;
passing a null position stores the item without removing ownership. The room UI
also checks the pet and waits for it to move clear before calling the save method.
Profile edits, shop claims and intervention rewards preserve placements.

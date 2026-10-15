# Pet saves

Confirmed companions are stored in `pets.json` under `Application.persistentDataPath`.
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
`manager.PetInfo.backgroundId` before calling `SavePet`.

`GameStateManager` also loads the background catalog and selected texture automatically,
exposing `Background` (the resolved definition) and `BackgroundImage` (a `Texture2D`).
It uses background 1 when there is no saved pet or the selected background cannot be loaded.
`Background.id` identifies the image actually loaded when a fallback is used; `BackgroundId`
remains the saved selection. Call `LoadBackground()` after changing the selection during gameplay.

PlayerPrefs still records the latest selected companion under `Companion.Profile.v1`.
`GameStateManager.LoadSavedPet()` loads that companion's stats into `PetInfo` and exposes
its UUID as `PetUID`. Previous PlayerPrefs-only saves are added to the file on load.

To persist gameplay changes, update `PetInfo` and call
`PetSaveStore.SavePet(manager.PetUID, manager.PetInfo)`. `LoadAll()` returns all saved pets.
File writes use a temporary file before replacement. Unreadable JSON raises an error and
is preserved instead of being replaced with an empty save.

Timed interventions add XP through `InterventionSession.TryClaim`. Each pet stores
`claimedInterventionSessions`, a list of unique practice-session receipts; old
saves default to an empty list. XP and the receipt are written together so retrying
the same claim cannot add XP twice. The award reads the latest saved XP rather
than overwriting it with a stale displayed total. Profile edits and shop claims
preserve the receipts. Timers and unfinished practice sessions remain in memory;
only claimed rewards persist across game restarts.

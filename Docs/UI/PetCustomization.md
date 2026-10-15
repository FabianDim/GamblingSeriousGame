# Companion setup

The setup flow is animal selection, breed/type selection, naming, then saving a
companion profile. Painting is archived in `Archive/PetPainting`, outside Unity's
Assets folder. The existing scene and controller GUIDs remain intact.

## Editing and wiring

- `Assets/UI/Screens/PetCreation.uxml`: setup form, editable in UI Builder via
  **Game > UI > Edit Pet Creation Screen**.
- `Assets/UI/Themes/PetCreation.uss`: preview, breed buttons, name form and summary.
- `Assets/Resources/Pets/Animals.json`: animals, breed/type options, descriptions,
  suggested names and sprite paths. Both screens should use the same catalog.
- `PetCreationScreen`: the animal selection controller (historical class name).
- `PetCustomizationScreen`: the breed/name setup controller.

The existing `PetCreationUI` prefab uses the new form automatically. The selection
screen's **On Pet Confirmed (String)** calls `AppNavigationManager.ConfirmPetSelection`
using the dynamic string callback. This sets the pet before activating setup.
**On Back Requested** calls `AppNavigationManager.BackButton`.

Each animal retains its draft name and breed while navigating back and forth in the
current session. Reopening the screen does not multiply callbacks or animations.
Unknown animal IDs and invalid catalogs disable saving. Invalid/duplicate breed IDs
are skipped; catalogs without valid breeds receive a `default` breed using the animal's
existing preview fields.

## Add an animal or breed

Use stable unique animal IDs and breed IDs (breed IDs are unique within each animal).
Breed/type here means a preset appearance, not an offspring or genetics system.
Descriptions are flavour text and do not change gameplay stats.

```json
{
  "animals": [
    {
      "id": "cat",
      "name": "Cat",
      "description": "A curious companion.",
      "previewImage": "Pets/PreviewImages/Cat_Idle",
      "previewFramesPerSecond": 10,
      "suggestedNames": ["Mochi", "Cleo", "Miso"],
      "breeds": [
        {
          "id": "shorthair",
          "name": "Shorthair",
          "description": "A little everyday explorer.",
          "previewImage": "Pets/PreviewImages/Cat_Idle",
          "previewFramesPerSecond": 10
        }
      ]
    }
  ]
}
```

The bundled shorthair uses the existing cat animation. The other example types have
reserved paths and display **Preview coming soon** until their artwork is added.
The selection screen uses the animal-level preview; setup uses the selected breed's
preview. Missing breed art never falls back to another animal or breed's image.

Put an idle sheet under `Assets/Resources`, import it as **Sprite (2D and UI)**,
set **Sprite Mode: Multiple**, and slice its frames in Sprite Editor. Set
`previewImage` to its Resources-relative path, without extension. Use one animation
per sheet: all slices play left-to-right, then top-to-bottom, ordered by their source
rectangles (not lexical frame names). FPS defaults to 10 and is bounded to 1�30.
For pixel art use Point filtering and an appropriate compression setting.

`PetSpritePreview` displays the imported sprites directly; no editable texture copy,
PNG export or Read/Write texture setting is required. Sprite atlas packing is supported.
A still Sprite/Texture2D can instead use `imageResourcePath`. Missing art leaves the
form usable. Pause preview supports reduced motion; it remains paused when switching
types or reopening the screen in the same session.

## Names and completion

Names are limited to 24 characters, reject blank/control-character input, and are
trimmed on save. Suggest a name cycles through catalog suggestions, with generic
fallbacks. The live summary shows the chosen name, animal and breed. All user/catalog
text is displayed literally with rich text disabled.

**Save companion** stores the latest confirmed profile on this device using
PlayerPrefs key `Companion.Profile.v1`, then invokes both events:

- **On Pet Created (String, String)**: existing animal ID/name callback, retained for
  compatibility with scene listeners.
- **On Profile Created (PetProfile)**: animal ID, breed ID and name for gameplay or
  your eventual save system. A handler accepts `PetProfile profile`.

```csharp
PetProfile profile = customization.CurrentProfile;
// profile.animalId, profile.breedId, profile.petName
```

A fresh setup instance restores the latest saved profile when that animal is chosen,
provided its breed still exists in the catalog. Saving another animal replaces that
single latest profile; this is not a multi-pet inventory or cloud save. In-session
drafts for other animals remain available. The button acknowledges a successful save
and prevents repeat submissions until an edit is made. Save errors retain the draft
and allow a retry. Completion stays on this screen unless an event listener navigates.
The former `PetTexture` and `ExportPetPng` APIs exist only in the archive.

## Validation

`Tools/PetCustomizationValidation.cs` runs in an isolated Unity 6000.6.2f1 project.
Copy `Assets/UI`, `Assets/Scripts/UI`, `Assets/Resources`, both UI prefabs and Outfit
fonts with metadata. Put the utility under `Assets/Editor`, then launch Unity with:

```
-batchmode -projectPath <isolated-project> -executeMethod PetCustomizationValidation.Run
```

It exercises navigation, sprite previews/pause, breed selection, missing art, name
validation/suggestions, trimmed save payloads, duplicate-submit prevention, event
compatibility, saved-profile restore, independent drafts, enable/disable lifecycle,
invalid catalog data, and 390 x 844 / 320 x 568 / 844 x 390 layouts. It captures PNGs
under that project's `Validation` directory. Device-specific safe areas still benefit
from checking in Device Simulator or on the target phone.

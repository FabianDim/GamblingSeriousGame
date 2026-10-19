# UI Toolkit

Open **Game > UI > Open UI Toolkit Scene**, then press Play. The migrated scene is
`Assets/Scenes/PetCreationUIToolkit.unity`. The original Canvas scene and prefabs
are retained as references; they are not used by the migrated scene.

## Editing your UI

The Home screen and future sports-betting screen-time integration are documented in
[`Docs/UI/HomeScreen.md`](../../Docs/UI/HomeScreen.md). Edit `Elements/HomeContent.uxml`
and `Themes/HomeScreen.uss`; `HomeScreen.prefab` is connected to the Home dock tab.

For breed/type choices, animated sprite previews, naming and local profile saving, see
[`Docs/UI/PetCustomization.md`](../../Docs/UI/PetCustomization.md).
Its UXML is `Screens/PetCreation.uxml` and its prefab is `PetCreationUI.prefab`.

- Double-click `Screens/PetSelection.uxml` to open the screen in **UI Builder**.
- Choose **Unity Default Runtime Theme** in UI Builder and use a **390 x 844**
  preview to start. The screen imports its stylesheet, so the look is available
  in UI Builder without entering Play mode.
- Edit the palette and reusable classes in `Themes/App.uss`. Its first block
  contains the background, green accent, text, outline and interaction colours.
- `Themes/AppTheme.tss` combines Unity's runtime control theme with the app styles.
- `Screens/StyleGuide.uxml` shows the colour palette, typography and button variants.

## Mapping from your Canvas UI

| Previous element | UI Toolkit equivalent |
| --- | --- |
| Canvas | UIDocument with `Settings/AppPanelSettings.asset` |
| BackgroundImage | `.app` element and its background colour |
| Content Panel | `.content-panel` VisualElement (transparent) |
| Top Page Header | `Elements/PageHeader.uxml` |
| Primary Button | `Elements/PrimaryButton.uxml` |
| Rounded Log In button | `Elements/SecondaryButton.uxml` |
| Plain text button | `Elements/TextButton.uxml` |
| Scroll View | `page-scroll` ScrollView in the screen UXML |

The background (#0B0E17), accent (#00E676) and Outfit font come from your saved
prefabs. The slate outline (#202C3D) and rounded secondary button follow your
Log In reference image. Hover, pressed, keyboard focus and disabled states are
included. The primary text remains "Button" from your prototype.

The main screen uses the primary and outlined Log In buttons. The text-only
variant from your first screenshot is available in Elements and the style guide.

## Adding content and reusing styles

`PetCreationScreen` populates `companion-content` inside `page-scroll` at runtime.
The header stays fixed, while pet cards and Confirm scroll within the viewport.
The component works with either screen UXML as long as it contains
`companion-content` and a Button named `ConfirmPetSelection`.

### JSON-driven pet cards

- Edit `Elements/PetCard.uxml` in UI Builder to change the reusable card layout.
  Each card is a Button containing an image box, name, description, and selection label.
  Its appearance is controlled by the `.pet-card` rules in `Themes/App.uss`.
- The `PetSelectionUI` prefab already has **Animal Json** set to
  `Assets/Resources/Pets/Animals.json` and **Pet Card Template** set to `PetCard.uxml`.
  Replace the example JSON with your own TextAsset in the Inspector.
- Cards are generated in Play mode, not in the UI Builder screen preview.
- Click or tap a card, or focus it with Tab and submit it with the keyboard.
  Only one card is selected at a time. Confirm is disabled until a selection is made.
- Connect **On Pet Confirmed (String)** in the Inspector to a method accepting a
  string; choose the dynamic string callback to receive the selected animal's `id`.
  Other scripts can read `PetCreationScreen.SelectedAnimal` for the full animal data.
  Selection stays in this screen; saving it or changing scenes belongs in that callback.

JSON uses an object containing an `animals` array (field names are case-sensitive):

```json
{
  "animals": [
    {
      "id": "cat",
      "name": "Cat",
      "description": "A curious, quiet companion.",
      "imageResourcePath": "Pets/Cat"
    }
  ]
}
```

IDs must be unique and nonempty; names must be nonempty. Put the image at
`Assets/Resources/Pets/Cat.png` for the example path above. The path is relative to
a `Resources` folder, without the extension. Both Sprite and Texture2D imports
are supported. An empty or missing image displays a placeholder box.
The bundled cat has a sliced idle animation; other types show placeholders until their art is added. See the companion setup guide for breed and sprite-sheet fields.
Call `ReloadPets()` after replacing the source data to rebuild the list and reset selection.

For a new button, add a Button in UI Builder and assign `app-button` plus
`button--primary` or `button--secondary`. For a background-free green text button,
use `class="text-button"` on its own. Or drag the corresponding
UXML template into the hierarchy. For template text changes, use an attribute
override on the template instance, or edit the template to update all instances.

The reusable `Assets/Prefabs/UI/PetSelectionUI.prefab` contains the UIDocument and
the screen script. Its **On Primary Action** and **On Log In** UnityEvents are
ready to connect to your game. Confirm also invokes **On Primary Action** after
**On Pet Confirmed**. Account authentication and saving the selected pet are not included.

## Mobile layout

Panel Settings scales from a 390 x 844 reference with **Match Width Or Height**
set to **0.5**, preserving
readable proportions across portrait and landscape sizes. Content is centred
with a maximum width of 480 panel units. `PetCreationScreen.cs` applies the
device safe-area padding at runtime and updates it when the screen changes.
It also reduces the vertical spacing on short screens, such as landscape phones.
Use Device Simulator to check specific notched phones.

## Reference

[Unity runtime UI setup](https://docs.unity.com/en-us/engine/6000.3/manual/uitoolkits/uielements/uie-support-for-runtime-ui/uie-get-started-with-runtime-ui)

`Tools/UIToolkitValidation.cs` is an isolated-project validation utility, not a
runtime script. It generates the migration assets and checks the rendered UI,
layout, scrolling and input in Unity 6000.6.2f1.

`Tools/PetSelectionValidation.cs` checks JSON population, selection and confirmation,
re-enabling the component, invalid/empty data, image fallback, and scrolling with
12 animals at a 320 x 568 render size. It also renders the example at 390 x 844.
Run it in an isolated Unity 6000.6.2f1 project with copies of `Assets/UI`,
the UI scripts, `PetSelectionUI.prefab`, their metadata, and the Outfit fonts.
Place the validation script under `Assets/Editor` in that project and launch Unity
with `-batchmode -projectPath <isolated-project> -executeMethod PetSelectionValidation.Run`.
The current example preview is `Docs/UI/PetSelection.png`.

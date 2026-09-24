# Pet creation and finger drawing

`Assets/UI/Screens/PetCreation.uxml` is the customization screen. Open it in UI Builder,
or use **Game > UI > Edit Pet Creation Screen**. Layout and appearance are in
`Assets/UI/Themes/PetCreation.uss`, with the existing app colours and fonts inherited
from `App.uss`.

The screen contains a name field, a large interactive preview, Inspect / Draw / Erase
modes, six colours, brush size, undo, clear, zoom controls, Done, and Back to pets.
The form scrolls on smaller displays. Cards are still managed by the older
`PetCreationScreen` component on the **selection** screen; the new editor uses
`PetCustomizationScreen`.

## Connect it in Unity

1. Open your app scene, currently `PetCreationUIToolkit`.
2. Drag `Assets/Prefabs/UI/PetCreationUI.prefab` into the scene. This prefab already
   has a UIDocument referencing `PetCreation.uxml`, the shared PanelSettings, and
   the `PetCustomizationScreen` component with the example animal JSON assigned.
3. Make this new GameObject inactive initially, so selection is shown first.
4. On the **App Navigation Manager**, assign the selection GameObject to **Pet
   Selection Screen** and this new GameObject to **Pet Creation Screen**. Keep the
   manager on a separate, active GameObject.
5. On the selection screen's **Pet Creation Screen** component, connect **On Pet
   Confirmed (String)** to the scene manager's **ConfirmPetSelection**, using the
   **Dynamic string** entry. Add this only once if you already connected it.
6. On the new **Pet Customization Screen** component, connect **On Back Requested**
   to the same manager's **BackToPetSelection**.
7. Press Play, select an animal, and confirm. The manager now calls
   `SetPet(petId)` before opening customization.

Both screens should use the same animal catalog. Scene navigation references are
assigned to scene instances in the Inspector, not to the prefab asset in the Project
window. The task adds a prefab and updates the navigation script; it does not modify
your scene or replace any in-progress scene setup.

## Add an image

The current example animals have empty image paths. Add your own transparent PNG,
for example `Assets/Resources/Pets/Cat.png`, and set the cat's JSON field to:

```json
"imageResourcePath": "Pets/Cat"
```

Use a Texture2D or an unpacked Sprite. A sprite sheet rectangle is supported, but
sprites packed into an atlas are not used for customization. Read/Write does **not**
need to be enabled: the controller makes an editable copy through a temporary GPU
render target. Assign **Preview Sprite** on the new component if you want a temporary
image for animals that do not yet have their own art.

Transparency defines where drawing is allowed. With an opaque rectangular background,
the rectangle is paintable too. A transparent background around the pet gives the
expected silhouette. Missing art shows a placeholder and disables painting and zoom.

## Gestures

- **Inspect:** one finger pans a zoomed image; two fingers pinch to zoom. On desktop,
  drag with the mouse and use the wheel. Zoom is limited to 1–4x; Reset view recentres.
- **Draw:** one finger or a mouse drag adds a stroke. Other fingers do not add marks.
- **Erase:** removes only the paint layer, revealing the original pet image.
- Switch back to Inspect to reposition the image while working. Drawing coordinates
  account for zoom and pan. The original alpha is retained and the source asset is
  never modified.
- **Undo** retains the last 12 changed strokes or clears. **Clear drawing** is undoable.
  Brush size is in paint-texture pixels, so the brush appears larger when zoomed in.

Pointer capture keeps a gesture assigned to the preview. Releasing, cancelling,
changing tool, resizing, or hiding the screen ends that gesture. Leaving and re-entering
the preview while drawing does not connect a line across the out-of-bounds gap.

## Finish and save

Done requires a non-whitespace name and a valid selected pet. Names have a 24-character
input limit and are trimmed on completion. **On Pet Created (String, String)** emits
the animal ID and chosen name. Connect it to your own game-state/save method or next
screen. This event does not automatically save data or navigate.

The customization component exposes:

```csharp
string id = customization.SelectedPetId;
string name = customization.PetName;
Texture2D appearance = customization.PetTexture;
byte[] png = customization.ExportPetPng(); // null when no image is available
```

Read/export the image in your completion callback before destroying the screen or
switching to a different animal. The texture belongs to the component; other consumers
must copy it if they need a separate lifetime. Export includes the original image and
paint together. Reopening the same pet retains edits in memory; choosing another pet
resets them. Closing the application discards edits unless your save system persists
the ID, name, and PNG. The separate editable paint/undo history is not exported.

## Implementation and verification

`PetCustomizationScreen.cs` connects the UI, navigation data, gestures, and events.
`PetPaintTexture.cs` handles alpha clipping, paint compositing, erasing, history, and
PNG export. Images are resized to at most 512 pixels on their longest side to bound
CPU painting and undo memory on mobile. Higher-resolution/GPU brush painting can be
added if device testing shows a need; this version is a functional 2D prototype.

`Tools/PetCustomizationValidation.cs` runs in an isolated Unity 6000.6.2f1 project.
Copy the UI assets, UI scripts, both UI prefabs, and Outfit fonts with their metadata.
Place the tool under that project's `Assets/Editor`, then run Unity with
`-batchmode -projectPath <isolated-project> -executeMethod PetCustomizationValidation.Run`.
It creates temporary test artwork and checks navigation, name validation, mouse/touch
painting, pinch zoom, erasing, undo, transparency, export, screen lifecycle, and layouts
at 390 × 844 and 320 × 568. The screenshot uses synthetic test artwork, not a bundled
game pet. Physical-device touch responsiveness still needs testing on your target phones.

References: [Unity pointer events](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-Pointer-Events.html),
[Unity RenderTexture](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/rendertexture).

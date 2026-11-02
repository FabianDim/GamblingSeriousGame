# Gameplay XP and pet shop

The gameplay HUD shows saved pet XP beside health. Open **Menu > Shop** inside
the background to browse the asset pack's sliced furniture, toys, bowls, bird
decorations and rabbit supplies. **Back** or Escape returns to the pet.

XP is an unlock threshold, not a currency. Unlocked items can be collected free,
with no XP deduction or limit on how many different unlocked items are collected.
Collected items show **Owned** and are saved per pet in `pets.json` as `ownedItems`.
Existing saves initialize an empty collection. Open **Menu > Inventory** to see
collected items. Choose **Place**, then click or tap a spot inside the green outline.
The entire item must fit inside the environment and cannot overlap another item.
The preview turns red at invalid spots. If the pet occupies the chosen spot, it
moves aside before placement is saved; if it cannot reach a clear spot, choose a
different location or store an item to make room.

Placed items remain listed as **In room**, with **Move** and **Store** controls.
Store returns the item to inventory without losing ownership. **Cancel** or Escape
while placing preserves the previous layout. Escape or Back closes the inventory.
Items are decorations; placing food or vitamins does not consume them or alter health.

The pet roams the lower part of the environment, uses its running animation when
available, and avoids placed items and the edges. Between walks it mixes short
idle pauses with 6–12 second activities from its breed, such as chilling, dancing,
sniffing, sitting or sleeping. It pauses its movement and break timer for tap reactions and
while the inventory or shop is open. `RoomLayout` defines normalized placement and
walking bounds; furniture and the pet stay aligned when the fitted room resizes.
Placement allows wall decorations as well as floor items, below the room's HUD.
The pet chooses reachable straight paths and waits if furniture leaves no clear path.

Each pet saves its layout as `placedItems` (item ID and normalized center x/y).
Placement, movement and storage save before updating the live inventory. Profile
edits, new shop claims and XP awards preserve the layout. Old saves default to an
empty room. Pet roaming positions are temporary and restart at a free spot on load.

`Assets/Scripts/Data/ShopCatalog.cs` defines the included resource sheets and each
sheet's starting XP threshold. Every group of four sprite variants adds 25 XP.
The first four pet bowls and rabbit foods are available at 0 XP. These initial
thresholds can be tuned in the catalog. Item IDs combine resource path and sprite
name, so existing IDs should be preserved when updating art.

The HUD reads `GameStateManager.SelectedPet.SaveData.experience` every 250 ms. The shop uses that
same value to update unlocks while open. XP earning mechanics are separate from
this UI. `PetSaveStore.TryClaimItem` validates the item and threshold, prevents
duplicates, and saves before updating the live collection.

## Validation

`Tools/PetShopValidation.cs` runs in an isolated Unity project with copies of
`Assets/Scripts`, `Assets/UI`, `Assets/Resources` and the Outfit fonts. Put the
validation script under `Assets/Editor`. Use a company/product name separate from
the game; the product name must begin with `PetShopValidation`. Configure a working
UI input provider (legacy input is sufficient for this isolated validation).

Launch Unity with `-batchmode -projectPath <isolated-project> -executeMethod
PetShopValidation.Run`. It checks sprite loading, old-save migration, XP thresholds,
duplicate prevention, persistence, failed-save rollback, dropdown navigation,
category filtering and re-enabling. It also renders gameplay and the shop at
390×844, 320×568 and 844×390 to the isolated project's `Validation` folder.

`Tools/PetRoomValidation.cs` uses the same isolated setup, with product name
`PetRoomValidation`, and runs via `-executeMethod PetRoomValidation.Run`. It checks
roaming, whole-item bounds, furniture collisions, moving the pet before committing,
inventory actions, persistence, old saves, failed-write rollback, cancellation,
resizing and screen lifecycle. It also renders the inventory and placement UI.

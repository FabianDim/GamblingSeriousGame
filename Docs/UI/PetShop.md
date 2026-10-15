# Gameplay XP and pet shop

The gameplay HUD shows saved pet XP beside health. Open **Menu > Shop** inside
the background to browse the asset pack's sliced furniture, toys, bowls, bird
decorations and rabbit supplies. **Back** or Escape returns to the pet.

XP is an unlock threshold, not a currency. Unlocked items can be collected free,
with no XP deduction or limit on how many different unlocked items are collected.
Collected items show **Owned** and are saved per pet in `pets.json` as `ownedItems`.
Existing saves initialize an empty collection. Items currently record ownership;
placing furniture and consuming supplies are not implemented.

`Assets/Scripts/Data/ShopCatalog.cs` defines the included resource sheets and each
sheet's starting XP threshold. Every group of four sprite variants adds 25 XP.
The first four pet bowls and rabbit foods are available at 0 XP. These initial
thresholds can be tuned in the catalog. Item IDs combine resource path and sprite
name, so existing IDs should be preserved when updating art.

The HUD reads `GameStateManager.PetInfo.experience` every 250 ms. The shop uses that
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

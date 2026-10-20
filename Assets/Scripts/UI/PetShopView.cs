using System.Collections.Generic;
using GamblingSeriousGame.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    // Owned by GameplayScreen; all controls remain within the fitted background.
    public sealed class PetShopView
    {
        private readonly GameStateManager state;
        private readonly VisualElement panel;
        private readonly VisualElement menu;
        private readonly Button menuButton;
        private readonly ScrollView list;
        private readonly Label status;
        private readonly Label summary;
        private readonly DropdownField category;
        private readonly List<(ShopItem item, Button button)> rows = new List<(ShopItem, Button)>();
        private int lastXp = -1, lastOwned = -1;

        public PetShopView(VisualElement background, GameStateManager state)
        {
            this.state = state;
            var navigation = new VisualElement { name = "gameplay-navigation" };
            navigation.AddToClassList("gameplay-navigation");
            menuButton = new Button(ToggleMenu) { name = "gameplay-menu", text = "Menu ▾" };
            menuButton.AddToClassList("gameplay-menu-button");
            navigation.Add(menuButton);
            menu = new VisualElement { name = "gameplay-menu-options" };
            menu.AddToClassList("gameplay-menu-options");
            menu.style.display = DisplayStyle.None;
            var openShop = new Button(Open) { text = "Shop" };
            openShop.AddToClassList("gameplay-menu-option");
            menu.Add(openShop);
            navigation.Add(menu);

            panel = new VisualElement { name = "pet-shop" };
            panel.AddToClassList("pet-shop");
            panel.style.display = DisplayStyle.None;
            var header = new VisualElement();
            header.AddToClassList("shop-header");
            var heading = new Label("Pet shop");
            heading.AddToClassList("shop-heading");
            header.Add(heading);
            var close = new Button(Close) { text = "Back", name = "shop-back" };
            close.AddToClassList("shop-back");
            header.Add(close);
            panel.Add(header);
            summary = new Label();
            summary.AddToClassList("shop-copy");
            panel.Add(summary);
            var hint = new Label("Earn XP to unlock items. Collect them free—your XP stays yours.");
            hint.AddToClassList("shop-copy");
            panel.Add(hint);
            category = new DropdownField(new List<string>
                { "All items", "Furniture & toys", "Bowls", "Bird decorations", "Rabbit supplies" }, 0)
                { name = "shop-category" };
            category.RegisterValueChangedCallback(_ => BuildRows());
            panel.Add(category);
            list = new ScrollView(ScrollViewMode.Vertical) { name = "shop-items" };
            list.AddToClassList("shop-items");
            panel.Add(list);
            status = new Label { name = "shop-status" };
            status.AddToClassList("shop-copy");
            panel.Add(status);
            background.Add(panel);
            // Keep the dropdown above the shop so it can still be used while browsing.
            background.Add(navigation);
            background.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                Close();
                evt.StopPropagation();
            });
        }

        private void ToggleMenu()
        {
            bool opening = menu.style.display == DisplayStyle.None;
            menu.style.display = opening ? DisplayStyle.Flex : DisplayStyle.None;
            menuButton.text = opening ? "Menu ▴" : "Menu ▾";
        }

        private void HideMenu()
        {
            menu.style.display = DisplayStyle.None;
            menuButton.text = "Menu ▾";
        }

        private void Open()
        {
            HideMenu();
            panel.style.display = DisplayStyle.Flex;
            status.text = "";
            if (rows.Count == 0) BuildRows();
            Refresh(true);
            panel.Q<Button>("shop-back").Focus();
        }

        private void Close()
        {
            panel.style.display = DisplayStyle.None;
            HideMenu();
            menuButton.Focus();
        }

        private void BuildRows()
        {
            rows.Clear();
            list.Clear();
            list.scrollOffset = Vector2.zero;
            foreach (var item in ShopCatalog.Items)
            {
                if (category.value != "All items" && category.value != item.Category) continue;
                var row = new VisualElement();
                row.AddToClassList("shop-item");
                var preview = new Image { sprite = item.Sprite, scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore };
                preview.AddToClassList("shop-item-image");
                row.Add(preview);
                var details = new VisualElement();
                details.AddToClassList("shop-item-details");
                var name = new Label(item.Name);
                name.AddToClassList("shop-item-name");
                details.Add(name);
                var requirement = new Label(item.RequiredXp == 0 ? "Starter item · Free" : $"Unlocks at {item.RequiredXp} XP · Free");
                requirement.AddToClassList("shop-item-requirement");
                details.Add(requirement);
                var claim = new Button(() => Claim(item)) { name = "claim-" + item.Id };
                claim.AddToClassList("shop-claim");
                details.Add(claim);
                row.Add(details);
                list.Add(row);
                rows.Add((item, claim));
            }
            if (rows.Count == 0) list.Add(new Label("No items available in this category."));
            Refresh(true);
        }

        private void Claim(ShopItem item)
        {
            PetSaveStore.TryClaimItem(state.SelectedPet?.Id, state.SelectedPet?.SaveData, item.Id, out string message);
            status.text = message;
            Refresh(true);
        }

        public void Refresh(bool force = false)
        {
            if (panel.style.display == DisplayStyle.None) return;
            var pet = state.SelectedPet?.SaveData;
            int xp = Mathf.Max(0, pet?.experience ?? 0);
            int owned = pet?.ownedItems?.Count ?? 0;
            if (!force && lastXp == xp && lastOwned == owned) return;
            lastXp = xp;
            lastOwned = owned;
            summary.text = $"{xp:N0} XP  ·  {owned} collected";
            foreach (var (item, button) in rows)
            {
                bool collected = pet?.ownedItems?.Contains(item.Id) == true;
                bool unlocked = xp >= item.RequiredXp;
                button.text = collected ? "Owned" : !unlocked ? $"{item.RequiredXp - xp} XP to unlock" : "Collect free";
                button.SetEnabled(pet != null && !string.IsNullOrEmpty(state.SelectedPet?.Id) && !collected && unlocked);
                button.EnableInClassList("shop-claim--owned", collected);
            }
        }
    }
}

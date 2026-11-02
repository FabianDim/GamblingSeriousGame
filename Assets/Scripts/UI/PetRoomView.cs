using System;
using System.Collections.Generic;
using GamblingSeriousGame.Data;
using GamblingSeriousGame.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    // Owns the room's lifetime, including placement gestures and the roaming scheduler.
    public sealed class PetRoomView : IDisposable
    {
        private readonly VisualElement background, itemLayer, surface, boundary, panel, toolbar;
        private readonly Image ghost;
        private readonly PetImage petImage;
        private readonly ScrollView inventory;
        private readonly Label status, placementHint;
        private readonly GameStateManager state;
        private readonly GameplayScreen screen;
        private readonly IVisualElementScheduledItem tick;
        private readonly Dictionary<string, Image> placedImages = new Dictionary<string, Image>();
        private readonly List<Rect> obstacles = new List<Rect>();
        private ShopItem selected;
        private Vector2 petPosition = new Vector2(.5f, .81f), destination, candidate;
        private bool waitingForPet, hasCandidate;
        private float lastTime, waitRemaining;
        private bool lastStopWasIdle = true;
        public bool IsMoving { get; private set; }
        public bool IsPlacing => selected != null;
        public bool IsInventoryOpen => panel.style.display == DisplayStyle.Flex;
        public Func<bool> IsShopOpen { private get; set; }
        public Action CloseShop { private get; set; }

        public PetRoomView(VisualElement background, VisualElement overlay, PetImage petImage,
            GameStateManager state, GameplayScreen screen)
        {
            this.background = background;
            this.petImage = petImage;
            this.state = state;
            this.screen = screen;
            itemLayer = new VisualElement { name = "room-items", pickingMode = PickingMode.Ignore };
            itemLayer.AddToClassList("room-layer");
            background.Insert(0, itemLayer);
            surface = new VisualElement { name = "room-placement", focusable = true };
            surface.AddToClassList("room-layer");
            surface.style.display = DisplayStyle.None;
            boundary = new VisualElement { name = "room-placement-bounds", pickingMode = PickingMode.Ignore };
            boundary.AddToClassList("room-placement-bounds");
            SetRect(boundary, RoomLayout.ItemArea);
            surface.Add(boundary);
            ghost = new Image { name = "room-placement-preview", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            ghost.AddToClassList("room-item-preview");
            surface.Add(ghost);
            // Placement intercepts the pet, while menus and cancellation remain above it.
            background.Insert(background.IndexOf(overlay), surface);
            surface.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            surface.RegisterCallback<PointerDownEvent>(OnPointerDown);
            background.RegisterCallback<KeyDownEvent>(OnKeyDown);

            panel = new VisualElement { name = "pet-inventory" };
            panel.AddToClassList("pet-shop");
            panel.style.display = DisplayStyle.None;
            var header = new VisualElement();
            header.AddToClassList("shop-header");
            var heading = new Label("Inventory");
            heading.AddToClassList("shop-heading");
            header.Add(heading);
            var back = new Button(CloseInventory) { text = "Back", name = "inventory-back" };
            back.AddToClassList("shop-back");
            header.Add(back);
            panel.Add(header);
            var copy = new Label("Your collected items. Place them in the room, move them, or store them here.");
            copy.AddToClassList("shop-copy");
            panel.Add(copy);
            inventory = new ScrollView(ScrollViewMode.Vertical) { name = "inventory-items" };
            inventory.AddToClassList("shop-items");
            panel.Add(inventory);
            status = new Label { name = "inventory-status" };
            status.AddToClassList("shop-copy");
            panel.Add(status);
            overlay.Add(panel);

            toolbar = new VisualElement { name = "placement-toolbar" };
            toolbar.AddToClassList("placement-toolbar");
            toolbar.style.display = DisplayStyle.None;
            placementHint = new Label { name = "placement-status" };
            placementHint.AddToClassList("shop-copy");
            toolbar.Add(placementHint);
            var cancel = new Button(() => { CancelPlacement(); OpenInventory(); }) { text = "Cancel", name = "placement-cancel" };
            cancel.AddToClassList("shop-back");
            toolbar.Add(cancel);
            overlay.Add(toolbar);

            RebuildRoom();
            if (!RoomLayout.IsFree(RoomLayout.PetBounds(petPosition), obstacles))
                foreach (var position in RoomLayout.PetPositions())
                    if (RoomLayout.IsFree(RoomLayout.PetBounds(position), obstacles)) { petPosition = position; break; }
            SetRect(petImage, RoomLayout.PetBounds(petPosition));
            lastTime = Time.realtimeSinceStartup;
            waitRemaining = 1;
            tick = background.schedule.Execute(Tick).Every(16);
        }

        public void OpenInventory()
        {
            CloseShop?.Invoke();
            CancelPlacement();
            StopWalking();
            panel.style.display = DisplayStyle.Flex;
            status.text = "";
            BuildInventory();
            panel.Q<Button>("inventory-back").Focus();
        }

        public void CloseInventory() => panel.style.display = DisplayStyle.None;

        public void PrepareForShop()
        {
            CancelPlacement();
            CloseInventory();
            StopWalking();
        }

        private void BuildInventory()
        {
            inventory.Clear();
            var pet = state.SelectedPet?.SaveData;
            int count = 0;
            foreach (var item in ShopCatalog.Items)
            {
                if (pet?.ownedItems?.Contains(item.Id) != true) continue;
                count++;
                bool placed = placedImages.ContainsKey(item.Id);
                var row = new VisualElement();
                row.AddToClassList("shop-item");
                var preview = new Image { sprite = item.Sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                preview.AddToClassList("shop-item-image");
                row.Add(preview);
                var details = new VisualElement();
                details.AddToClassList("shop-item-details");
                var title = new Label(item.Name);
                title.AddToClassList("shop-item-name");
                details.Add(title);
                var location = new Label(placed ? "In room" : "In inventory");
                location.AddToClassList("shop-item-requirement");
                details.Add(location);
                var place = new Button(() => BeginPlacement(item)) { text = placed ? "Move" : "Place", name = "place-" + item.Id };
                place.AddToClassList("shop-claim");
                details.Add(place);
                if (placed)
                {
                    var store = new Button(() => StoreItem(item)) { text = "Store", name = "store-" + item.Id };
                    store.AddToClassList("shop-back");
                    details.Add(store);
                }
                row.Add(details);
                inventory.Add(row);
            }
            if (count == 0)
            {
                var empty = new Label("Your inventory is empty. Open Menu > Shop to collect your first free item.");
                empty.AddToClassList("shop-copy");
                inventory.Add(empty);
            }
        }

        private void StoreItem(ShopItem item)
        {
            if (PetSaveStore.TryPlaceItem(state.SelectedPet?.Id, state.SelectedPet?.SaveData, item.Id, null, out var message))
            {
                RebuildRoom();
                BuildInventory();
            }
            status.text = message;
        }

        private void RebuildRoom()
        {
            itemLayer.Clear();
            placedImages.Clear();
            obstacles.Clear();
            var pet = state.SelectedPet?.SaveData;
            if (pet?.placedItems == null) return;
            foreach (var entry in pet.placedItems)
            {
                var item = entry == null ? null : ShopCatalog.Find(entry.itemId);
                if (item?.Sprite == null || pet.ownedItems?.Contains(item.Id) != true || placedImages.ContainsKey(item.Id)) continue;
                var bounds = RoomLayout.ItemBounds(item, new Vector2(entry.x, entry.y));
                if (!RoomLayout.Contains(RoomLayout.ItemArea, bounds) || !RoomLayout.IsFree(bounds, obstacles)) continue;
                var image = new Image { name = "placed-" + item.Id, sprite = item.Sprite, scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore };
                SetRect(image, bounds);
                itemLayer.Add(image);
                placedImages.Add(item.Id, image);
                obstacles.Add(bounds);
            }
        }

        private void BeginPlacement(ShopItem item)
        {
            CancelPlacement();
            CloseInventory();
            StopWalking();
            selected = item;
            surface.style.display = DisplayStyle.Flex;
            toolbar.style.display = DisplayStyle.Flex;
            ghost.sprite = item.Sprite;
            ghost.style.display = DisplayStyle.None;
            placementHint.text = "Tap inside the outline to place " + item.Name + ".";
            if (placedImages.TryGetValue(item.Id, out var original)) original.style.opacity = .4f;
            surface.Focus();
        }

        public void CancelPlacement()
        {
            if (selected != null && placedImages.TryGetValue(selected.Id, out var original)) original.style.opacity = 1;
            selected = null;
            waitingForPet = hasCandidate = false;
            surface.style.display = toolbar.style.display = DisplayStyle.None;
            StopWalking();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || (!IsPlacing && !IsInventoryOpen)) return;
            if (IsPlacing) { CancelPlacement(); OpenInventory(); }
            else CloseInventory();
            evt.StopPropagation();
        }

        private bool ReadPosition(Vector3 world, out Vector2 position)
        {
            var size = background.contentRect.size;
            position = Vector2.zero;
            if (size.x <= 0 || size.y <= 0) return false;
            var local = background.WorldToLocal(new Vector2(world.x, world.y));
            position = new Vector2(local.x / size.x, local.y / size.y);
            return true;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (selected == null || waitingForPet || !ReadPosition(evt.position, out var position)) return;
            Preview(position);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!evt.isPrimary || evt.button != 0 || selected == null) return;
            evt.StopPropagation();
            if (waitingForPet || !ReadPosition(evt.position, out var position)) return;
            Preview(position);
            if (!CanPlace(candidate, out var message)) { placementHint.text = message; return; }
            var bounds = RoomLayout.ItemBounds(selected, candidate);
            if (bounds.Overlaps(RoomLayout.PetBounds(petPosition)))
            {
                if (!RoomLayout.TryFindEscape(petPosition, bounds, obstacles, out destination))
                {
                    placementHint.text = "Your pet needs more space to move. Choose another spot or store an item.";
                    return;
                }
                waitingForPet = true;
                placementHint.text = "Your pet is moving aside...";
                StartWalking(destination);
                return;
            }
            CommitPlacement();
        }

        private void Preview(Vector2 position)
        {
            candidate = position;
            hasCandidate = true;
            SetRect(ghost, RoomLayout.ItemBounds(selected, candidate));
            ghost.style.display = DisplayStyle.Flex;
            bool valid = CanPlace(candidate, out var message);
            ghost.EnableInClassList("room-item-preview--invalid", !valid);
            placementHint.text = valid ? (RoomLayout.ItemBounds(selected, candidate).Overlaps(RoomLayout.PetBounds(petPosition))
                ? "Tap here and your pet will move aside." : "Tap to place. Cancel to keep your original layout.") : message;
        }

        private bool CanPlace(Vector2 position, out string message)
        {
            var bounds = RoomLayout.ItemBounds(selected, position);
            if (!RoomLayout.Contains(RoomLayout.ItemArea, bounds))
            { message = "Keep the whole item inside the highlighted area."; return false; }
            var pet = state.SelectedPet?.SaveData;
            foreach (var entry in pet?.placedItems ?? new List<PetItemPlacement>())
            {
                if (entry == null || entry.itemId == selected.Id) continue;
                var other = ShopCatalog.Find(entry.itemId);
                if (other?.Sprite != null && bounds.Overlaps(RoomLayout.ItemBounds(other, new Vector2(entry.x, entry.y))))
                { message = "Leave space between items."; return false; }
            }
            message = "";
            return true;
        }

        private void CommitPlacement()
        {
            waitingForPet = false;
            if (!hasCandidate || selected == null || !CanPlace(candidate, out _) ||
                RoomLayout.ItemBounds(selected, candidate).Overlaps(RoomLayout.PetBounds(petPosition))) return;
            if (!PetSaveStore.TryPlaceItem(state.SelectedPet?.Id, state.SelectedPet?.SaveData, selected.Id, candidate, out var message))
            { placementHint.text = message; return; }
            CancelPlacement();
            RebuildRoom();
            OpenInventory();
            status.text = message;
        }

        private void Tick()
        {
            float now = Time.realtimeSinceStartup;
            float delta = Mathf.Clamp(now - lastTime, 0, .05f);
            lastTime = now;
            if (state.SelectedPet == null || screen.IsPetReacting || IsInventoryOpen || IsShopOpen?.Invoke() == true) return;
            if (IsMoving)
            {
                var next = Vector2.MoveTowards(petPosition, destination, delta * .14f);
                // Every step is checked, including after a layout update.
                if (!RoomLayout.ClearPath(petPosition, next, obstacles))
                {
                    StopWalking();
                    waitingForPet = false;
                    if (IsPlacing) placementHint.text = "Your pet can't reach that space. Choose another spot.";
                    return;
                }
                petPosition = next;
                SetRect(petImage, RoomLayout.PetBounds(petPosition));
                if ((petPosition - destination).sqrMagnitude < .000001f)
                {
                    StopWalking(takeBreak: !waitingForPet);
                    if (waitingForPet) CommitPlacement();
                }
                return;
            }
            if (IsPlacing) return;
            // Count only active room time, so taps and open menus don't consume a break.
            waitRemaining -= delta;
            if (waitRemaining > 0) return;
            waitRemaining = UnityEngine.Random.Range(2f, 4f);
            int count = 0;
            var target = petPosition;
            foreach (var point in RoomLayout.PetPositions())
            {
                if (Vector2.Distance(point, petPosition) < .08f || !RoomLayout.ClearPath(petPosition, point, obstacles)) continue;
                if (UnityEngine.Random.Range(0, ++count) == 0) target = point;
            }
            if (count > 0) StartWalking(target);
            else ChooseRestActivity();
        }

        private void StartWalking(Vector2 target)
        {
            destination = target;
            IsMoving = true;
            screen.SetPetWalking(true, destination.x < petPosition.x);
        }

        private void StopWalking(bool takeBreak = false)
        {
            bool wasMoving = IsMoving;
            IsMoving = false;
            if (wasMoving) screen.SetPetWalking(false, false);
            // Opening menus or cancelling placement must not shorten an existing activity.
            if (wasMoving || waitRemaining <= 0) waitRemaining = UnityEngine.Random.Range(2f, 4f);
            if (takeBreak) ChooseRestActivity();
        }

        private void ChooseRestActivity()
        {
            // Mix short idle pauses with longer activities, avoiding consecutive idle-only stops.
            if (!lastStopWasIdle && UnityEngine.Random.value >= .7f)
            {
                lastStopWasIdle = true;
                return;
            }
            float duration = screen.PlayPetRestActivity();
            lastStopWasIdle = duration <= 0;
            if (duration > 0) waitRemaining = duration;
        }

        private static void SetRect(VisualElement element, Rect rect)
        {
            element.style.position = Position.Absolute;
            element.style.left = Length.Percent(rect.x * 100);
            element.style.top = Length.Percent(rect.y * 100);
            element.style.width = Length.Percent(rect.width * 100);
            element.style.height = Length.Percent(rect.height * 100);
            element.style.bottom = StyleKeyword.Auto;
        }

        public void Dispose()
        {
            tick.Pause();
            background.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            surface.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            surface.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        }
    }
}

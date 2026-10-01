using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace GamblingSeriousGame.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class PetCustomizationScreen : MonoBehaviour
    {
        [SerializeField] private TextAsset animalJson;
        [Tooltip("Optional preview image when the selected animal has no Resources image yet.")]
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private UnityEvent<string, string> onPetCreated = new UnityEvent<string, string>();
        [SerializeField] private UnityEvent onBackRequested = new UnityEvent();

        private enum Tool { Inspect, Draw, Erase }
        private Tool tool;
        private VisualElement root, viewport, palette, paintTools;
        private Image preview;
        private TextField nameInput;
        private Slider brushSize;
        private Label placeholder, hint, status;
        private Button inspectButton, drawButton, eraseButton, undoButton, clearButton;
        private Button zoomInButton, zoomOutButton, resetButton, doneButton, backButton;
        private IVisualElementScheduledItem safeAreaUpdate;
        private PetDefinition pet;
        private PetPaintTexture painting;
        private Color32 brushColour = new Color32(0, 230, 118, 255);
        private string colourName = "colour-green";
        private float brushRadius = 12;
        private float zoom = 1;
        private Vector2 pan;
        private Rect imageRect;
        private readonly Dictionary<int, Vector2> pointers = new Dictionary<int, Vector2>();
        private int drawingPointer = -1;
        private Vector2? previousPaintPoint;

        public string SelectedPetId => pet?.id;
        public string PetName { get; private set; } = string.Empty;
        public Texture2D PetTexture => painting?.Texture;
        public byte[] ExportPetPng() => painting?.ExportPng();

        /// <summary>Call before activating this screen. Reopening the same pet retains edits.</summary>
        public void SetPet(string petId)
        {
            if (pet != null && pet.id == petId) return;
            PetDefinition next = null;
            try
            {
                var catalog = animalJson == null ? null : JsonUtility.FromJson<PetCatalog>(animalJson.text);
                if (catalog?.animals != null)
                    next = Array.Find(catalog.animals, entry => entry != null && entry.id == petId);
            }
            catch (ArgumentException exception) { Debug.LogWarning("Invalid animal JSON: " + exception.Message, this); }
            StopGesture();
            painting?.Dispose();
            painting = null;
            pet = next;
            PetName = string.Empty;
            pan = Vector2.zero;
            zoom = 1;
            if (isActiveAndEnabled && root != null) BindPet();
        }

        private void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            viewport = root.Q("pet-viewport");
            preview = root.Q<Image>("pet-preview");
            placeholder = root.Q<Label>("preview-placeholder");
            hint = root.Q<Label>("gesture-hint");
            status = root.Q<Label>("customization-status");
            nameInput = root.Q<TextField>("pet-name-input");
            brushSize = root.Q<Slider>("brush-size");
            palette = root.Q("paint-palette");
            paintTools = root.Q("paint-tools");
            inspectButton = root.Q<Button>("inspect-mode");
            drawButton = root.Q<Button>("draw-mode");
            eraseButton = root.Q<Button>("erase-mode");
            undoButton = root.Q<Button>("undo-paint");
            clearButton = root.Q<Button>("clear-paint");
            zoomInButton = root.Q<Button>("zoom-in");
            zoomOutButton = root.Q<Button>("zoom-out");
            resetButton = root.Q<Button>("reset-view");
            doneButton = root.Q<Button>("finish-pet");
            backButton = root.Q<Button>("back-to-selection");
            inspectButton.clicked += Inspect;
            drawButton.clicked += Draw;
            eraseButton.clicked += Erase;
            undoButton.clicked += Undo;
            clearButton.clicked += Clear;
            zoomInButton.clicked += ZoomIn;
            zoomOutButton.clicked += ZoomOut;
            resetButton.clicked += ResetView;
            doneButton.clicked += Finish;
            backButton.clicked += Back;
            nameInput.RegisterValueChangedCallback(OnNameChanged);
            brushSize.RegisterValueChangedCallback(OnBrushChanged);
            palette.RegisterCallback<ClickEvent>(OnColourClicked);
            viewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
            viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
            viewport.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            viewport.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            viewport.RegisterCallback<WheelEvent>(OnWheel);
            viewport.RegisterCallback<GeometryChangedEvent>(OnViewportChanged);
            safeAreaUpdate = root.schedule.Execute(ApplySafeArea).Every(250);
            BindPet();
        }

        private void OnDisable()
        {
            StopGesture();
            inspectButton.clicked -= Inspect;
            drawButton.clicked -= Draw;
            eraseButton.clicked -= Erase;
            undoButton.clicked -= Undo;
            clearButton.clicked -= Clear;
            zoomInButton.clicked -= ZoomIn;
            zoomOutButton.clicked -= ZoomOut;
            resetButton.clicked -= ResetView;
            doneButton.clicked -= Finish;
            backButton.clicked -= Back;
            nameInput.UnregisterValueChangedCallback(OnNameChanged);
            brushSize.UnregisterValueChangedCallback(OnBrushChanged);
            palette.UnregisterCallback<ClickEvent>(OnColourClicked);
            viewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            viewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            viewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            viewport.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            viewport.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            viewport.UnregisterCallback<WheelEvent>(OnWheel);
            viewport.UnregisterCallback<GeometryChangedEvent>(OnViewportChanged);
            safeAreaUpdate?.Pause();
            root = null;
        }

        private void OnDestroy() => painting?.Dispose();

        private void BindPet()
        {
            if (painting == null)
            {
                Texture source = null;
                Sprite sprite = null;
                if (!string.IsNullOrWhiteSpace(pet?.imageResourcePath))
                {
                    sprite = Resources.Load<Sprite>(pet.imageResourcePath);
                    if (sprite == null) source = Resources.Load<Texture2D>(pet.imageResourcePath);
                }
                if (sprite == null && source == null) sprite = previewSprite;
                var uv = new Rect(0, 0, 1, 1);
                if (sprite != null)
                {
                    // Use standalone, unpacked sprites so the paint layer matches their orientation.
                    if (sprite.packed)
                        Debug.LogWarning("Use an unpacked pet sprite for customization.", this);
                    else
                    {
                        source = sprite.texture;
                        var rect = sprite.rect;
                        uv = new Rect(rect.x / source.width, rect.y / source.height, rect.width / source.width, rect.height / source.height);
                    }
                }
                if (source != null) painting = new PetPaintTexture(source, uv);
            }
            root.Q<Label>("pet-species").text = pet?.name ?? "Your new companion";
            root.Q<Label>("pet-species").enableRichText = false;
            nameInput.SetValueWithoutNotify(PetName);
            brushSize.SetValueWithoutNotify(brushRadius);
            preview.image = PetTexture;
            placeholder.text = pet == null ? "Choose a companion first." : "No pet image available yet.";
            placeholder.style.display = painting == null ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var button in palette.Query<Button>().ToList())
                button.EnableInClassList("colour-swatch--selected", button.name == colourName);
            SetTool(tool);
            UpdateView();
            UpdateControls();
            ApplySafeArea();
        }

        private void OnNameChanged(ChangeEvent<string> evt) { PetName = evt.newValue; UpdateControls(); }
        private void OnBrushChanged(ChangeEvent<float> evt) => brushRadius = Mathf.Clamp(evt.newValue, 2, 32);
        private void Inspect() => SetTool(Tool.Inspect);
        private void Draw() => SetTool(Tool.Draw);
        private void Erase() => SetTool(Tool.Erase);
        private void SetTool(Tool next)
        {
            StopGesture();
            tool = next;
            inspectButton.EnableInClassList("customization-tool--selected", tool == Tool.Inspect);
            drawButton.EnableInClassList("customization-tool--selected", tool == Tool.Draw);
            eraseButton.EnableInClassList("customization-tool--selected", tool == Tool.Erase);
            paintTools.style.display = tool == Tool.Inspect ? DisplayStyle.None : DisplayStyle.Flex;
            hint.text = tool == Tool.Inspect ? "Drag to move. Pinch or use + / - to zoom." :
                tool == Tool.Draw ? "Draw on your pet with one finger. Inspect to zoom." : "Rub over marks to erase them. Your pet stays unchanged.";
        }

        private void OnColourClicked(ClickEvent evt)
        {
            if (!(evt.target is Button button)) return;
            switch (button.name)
            {
                case "colour-green": brushColour = new Color32(0, 230, 118, 255); break;
                case "colour-blue": brushColour = new Color32(85, 185, 255, 255); break;
                case "colour-pink": brushColour = new Color32(255, 128, 171, 255); break;
                case "colour-yellow": brushColour = new Color32(255, 214, 90, 255); break;
                case "colour-white": brushColour = new Color32(255, 255, 255, 255); break;
                case "colour-black": brushColour = new Color32(32, 35, 43, 255); break;
                default: return;
            }
            colourName = button.name;
            foreach (var swatch in palette.Query<Button>().ToList())
                swatch.EnableInClassList("colour-swatch--selected", swatch == button);
            SetTool(Tool.Draw);
        }

        private void Undo() { StopGesture(); painting?.Undo(); UpdateControls(); }
        private void Clear() { StopGesture(); painting?.Clear(); UpdateControls(); }
        private void Back() { StopGesture(); onBackRequested.Invoke(); }
        private void Finish()
        {
            if (pet == null || string.IsNullOrWhiteSpace(PetName)) return;
            StopGesture();
            PetName = PetName.Trim();
            nameInput.SetValueWithoutNotify(PetName);
            onPetCreated.Invoke(pet.id, PetName);
        }

        private void UpdateControls()
        {
            bool hasImage = painting != null;
            drawButton.SetEnabled(hasImage);
            eraseButton.SetEnabled(hasImage);
            paintTools.SetEnabled(hasImage);
            undoButton.SetEnabled(painting?.CanUndo == true);
            clearButton.SetEnabled(hasImage);
            zoomInButton.SetEnabled(hasImage);
            zoomOutButton.SetEnabled(hasImage);
            resetButton.SetEnabled(hasImage);
            doneButton.SetEnabled(pet != null && !string.IsNullOrWhiteSpace(PetName));
            status.text = pet == null ? "Go back and choose a companion." :
                string.IsNullOrWhiteSpace(PetName) ? "Give your companion a name to continue." : "Ready when you are.";
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (painting == null || evt.button != 0) return;
            evt.StopPropagation();
            if (pointers.Count >= (tool == Tool.Inspect ? 2 : 1)) return;
            var point = (Vector2)evt.localPosition;
            pointers[evt.pointerId] = point;
            viewport.CapturePointer(evt.pointerId);
            viewport.Focus();
            if (tool == Tool.Inspect) return;
            drawingPointer = evt.pointerId;
            painting.BeginStroke();
            PaintAt(point);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!pointers.TryGetValue(evt.pointerId, out var previous)) return;
            evt.StopPropagation();
            var point = (Vector2)evt.localPosition;
            if (tool != Tool.Inspect)
            {
                if (evt.pointerId == drawingPointer) PaintAt(point);
                pointers[evt.pointerId] = point;
                return;
            }
            if (pointers.Count == 1) pan += point - previous;
            else
            {
                Vector2 other = Vector2.zero;
                foreach (var pointer in pointers) if (pointer.Key != evt.pointerId) other = pointer.Value;
                float oldDistance = Vector2.Distance(previous, other);
                if (oldDistance > 1)
                {
                    var oldMid = (previous + other) * 0.5f;
                    var newMid = (point + other) * 0.5f;
                    float nextZoom = Mathf.Clamp(zoom * Vector2.Distance(point, other) / oldDistance, 1, 4);
                    pan = newMid - viewport.contentRect.center - (oldMid - viewport.contentRect.center - pan) * (nextZoom / zoom);
                    zoom = nextZoom;
                }
            }
            pointers[evt.pointerId] = point;
            UpdateView();
        }

        private void PaintAt(Vector2 point)
        {
            if (!viewport.contentRect.Contains(point) || !imageRect.Contains(point)) { previousPaintPoint = null; return; }
            var uv = (point - imageRect.position) / imageRect.size;
            var pixel = new Vector2(uv.x * (PetTexture.width - 1), (1 - uv.y) * (PetTexture.height - 1));
            painting.PaintLine(previousPaintPoint ?? pixel, pixel, brushRadius, brushColour, tool == Tool.Erase);
            previousPaintPoint = pixel;
        }

        private void OnPointerUp(PointerUpEvent evt) { if (pointers.ContainsKey(evt.pointerId)) { ReleasePointer(evt.pointerId); evt.StopPropagation(); } }
        private void OnPointerCancel(PointerCancelEvent evt) { if (pointers.ContainsKey(evt.pointerId)) { ReleasePointer(evt.pointerId); evt.StopPropagation(); } }
        private void OnCaptureOut(PointerCaptureOutEvent evt) { if (pointers.ContainsKey(evt.pointerId)) ReleasePointer(evt.pointerId); }
        private void ReleasePointer(int id)
        {
            pointers.Remove(id);
            if (id == drawingPointer)
            {
                painting?.EndStroke();
                drawingPointer = -1;
                previousPaintPoint = null;
                UpdateControls();
            }
            if (viewport.HasPointerCapture(id)) viewport.ReleasePointer(id);
        }

        private void StopGesture()
        {
            foreach (var id in new List<int>(pointers.Keys)) ReleasePointer(id);
            painting?.EndStroke();
            previousPaintPoint = null;
        }

        private void ZoomIn() => ZoomAt(zoom * 1.25f, viewport.contentRect.center);
        private void ZoomOut() => ZoomAt(zoom / 1.25f, viewport.contentRect.center);
        private void ResetView() { StopGesture(); zoom = 1; pan = Vector2.zero; UpdateView(); }
        private void OnWheel(WheelEvent evt)
        {
            if (painting == null) return;
            ZoomAt(zoom * Mathf.Exp(-evt.delta.y * 0.08f), evt.localMousePosition);
            evt.StopPropagation();
        }
        private void ZoomAt(float value, Vector2 anchor)
        {
            StopGesture();
            float nextZoom = Mathf.Clamp(value, 1, 4);
            pan = anchor - viewport.contentRect.center - (anchor - viewport.contentRect.center - pan) * (nextZoom / zoom);
            zoom = nextZoom;
            UpdateView();
        }

        private void OnViewportChanged(GeometryChangedEvent evt) { StopGesture(); UpdateView(); }
        private void UpdateView()
        {
            if (painting == null || viewport.contentRect.width <= 0 || viewport.contentRect.height <= 0) return;
            var area = viewport.contentRect;
            float fit = Mathf.Min(area.width / PetTexture.width, area.height / PetTexture.height) * 0.9f;
            var size = new Vector2(PetTexture.width, PetTexture.height) * fit * zoom;
            var limit = new Vector2(Mathf.Max(0, (size.x - area.width) / 2), Mathf.Max(0, (size.y - area.height) / 2));
            pan = new Vector2(Mathf.Clamp(pan.x, -limit.x, limit.x), Mathf.Clamp(pan.y, -limit.y, limit.y));
            imageRect = new Rect(area.center + pan - size / 2, size);
            preview.style.left = imageRect.x;
            preview.style.top = imageRect.y;
            preview.style.width = imageRect.width;
            preview.style.height = imageRect.height;
        }

        private void ApplySafeArea()
        {
            if (root?.panel == null || Screen.width <= 0 || Screen.height <= 0) return;
            var safeArea = root.Q("safe-area");
            float x = root.resolvedStyle.width / Screen.width, y = root.resolvedStyle.height / Screen.height;
            if (float.IsNaN(x) || float.IsNaN(y)) return;
            var rect = Screen.safeArea;
            safeArea.style.paddingLeft = rect.xMin * x;
            safeArea.style.paddingRight = (Screen.width - rect.xMax) * x;
            safeArea.style.paddingTop = (Screen.height - rect.yMax) * y;
            safeArea.style.paddingBottom = rect.yMin * y;
        }
    }
}

using System;
using System.Collections.Generic;
using TMPro;
using RTLTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adeeb.EditorApp
{
    public sealed class EditorView : MonoBehaviour
    {
        public event Action NewRequested, MenuRequested, SaveRequested, PreviewRequested, AddPageRequested, DeleteRequested, RetryRequested;
        public event Action<int> PageRequested;
        public event Action<float> ScaleRequested;
        public event Action<string> TitleChanged, BackgroundRequested, ObjectRequested, Selected;
        public event Action<string, bool> OpenRequested;
        public event Action<string, float, float> Moved;
        TMP_FontAsset font, chromeFont;
        static readonly Color32 Ink = new Color32(14, 42, 51, 255);
        static readonly Color32 PanelInk = new Color32(24, 62, 70, 255);
        static readonly Color32 Field = new Color32(36, 80, 87, 255);
        static readonly Color32 Accent = new Color32(112, 220, 189, 255);
        static readonly Color32 Paper = new Color32(247, 247, 239, 255);
        static readonly Color32 Muted = new Color32(172, 202, 201, 255);
        RemoteAssetService assets;
        RectTransform root, screen, menuScreen, editorScreen, emptyState, toolbar, sidebar, stage, stageHolder, confirmation;
        TMP_InputField title;
        TextMeshProUGUI status, pageLabel;
        readonly List<Button> editingButtons = new List<Button>();
        readonly Dictionary<string, Button> backgroundButtons = new Dictionary<string, Button>();
        readonly List<Texture2D> covers = new List<Texture2D>();
        readonly List<Sprite> coverSprites = new List<Sprite>();
        Button previous, next, preview, save, retry;
        GridLayoutGroup menuGrid;
        RectTransform menuViewport;
        bool menu = true;
        bool paletteBound;
        EditorController state;
        Vector2 lastSize;

        public void Build(RemoteAssetService assets)
        {
            this.assets = assets;
            font = Resources.Load<TMP_FontAsset>("Fonts/SearchFont");
            chromeFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            var canvas = new GameObject("EditorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            root = Panel(canvas.transform, "Root", Ink); Stretch(root);
            var heading = Label(root, "ADEEB  /  STORY STUDIO", 26);
            heading.color = Accent; heading.fontStyle = FontStyles.Bold;
            Bounds(heading.rectTransform, .03f, .93f, .75f, .99f);
            status = Label(root, "Loading remote assets...", 22);
            status.gameObject.name = "Status";
            status.color = Muted;
            Bounds(status.rectTransform, .03f, .01f, .86f, .065f);
            retry = Button(root, "Retry", () => RetryRequested?.Invoke());
            Bounds((RectTransform)retry.transform, .88f, .015f, .97f, .065f);
            var binding = Panel(root, "ShelfBinding", Accent); Bounds(binding, 0f, 0f, .007f, 1f);
            binding.GetComponent<Image>().raycastTarget = false;
            screen = Panel(root, "Screen", PanelInk); Bounds(screen, .025f, .08f, .975f, .92f);
        }
        // Called in Edit Mode by SceneComposer. The resulting controls are saved in the scene.
        public void BuildScene(AssetCatalog previewCatalog)
        {
            Build(null);
            menuScreen = Panel(screen, "MenuScreen", Color.clear); Stretch(menuScreen);
            BuildMenuChrome();
            editorScreen = Panel(screen, "EditorScreen", Color.clear); Stretch(editorScreen);
            BuildEditor(previewCatalog);
            editorScreen.gameObject.SetActive(false);
        }
        public void BindScene(RemoteAssetService service)
        {
            assets = service;
            font = Resources.Load<TMP_FontAsset>("Fonts/SearchFont");
            chromeFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            root = (RectTransform)transform.Find("EditorCanvas/Root");
            if (root == null) { Debug.LogError("EditorCanvas is missing from the scene."); return; }
            screen = (RectTransform)root.Find("Screen");
            menuScreen = (RectTransform)screen.Find("MenuScreen");
            editorScreen = (RectTransform)screen.Find("EditorScreen");
            status = root.Find("Status").GetComponent<TextMeshProUGUI>();
            retry = root.Find("Retry").GetComponent<Button>();
            retry.onClick.AddListener(() => RetryRequested?.Invoke());
            menuViewport = (RectTransform)menuScreen.Find("SavedProjects");
            menuGrid = menuViewport.Find("Cards").GetComponent<GridLayoutGroup>();
            emptyState = (RectTransform)menuScreen.Find("EmptyState");
            menuScreen.Find("New story").GetComponent<Button>().onClick.AddListener(() => NewRequested?.Invoke());
            toolbar = (RectTransform)editorScreen.Find("Toolbar");
            toolbar.Find("Menu").GetComponent<Button>().onClick.AddListener(() => MenuRequested?.Invoke());
            title = toolbar.Find("Title").GetComponent<TMP_InputField>();
            title.onEndEdit.AddListener(value => TitleChanged?.Invoke(value));
            save = toolbar.Find("Save").GetComponent<Button>(); save.onClick.AddListener(() => SaveRequested?.Invoke());
            preview = toolbar.Find("Preview").GetComponent<Button>(); preview.onClick.AddListener(() => PreviewRequested?.Invoke());
            sidebar = (RectTransform)editorScreen.Find("AssetPalette");
            sidebar.Find("Smaller −").GetComponent<Button>().onClick.AddListener(() => ScaleRequested?.Invoke(.85f));
            sidebar.Find("Larger +").GetComponent<Button>().onClick.AddListener(() => ScaleRequested?.Invoke(1.15f));
            sidebar.Find("Remove selected").GetComponent<Button>().onClick.AddListener(() => DeleteRequested?.Invoke());
            editingButtons.Add(sidebar.Find("Smaller −").GetComponent<Button>());
            editingButtons.Add(sidebar.Find("Larger +").GetComponent<Button>());
            editingButtons.Add(sidebar.Find("Remove selected").GetComponent<Button>());
            stageHolder = (RectTransform)editorScreen.Find("StageHolder");
            stage = (RectTransform)stageHolder.Find("Page");
            var controls = editorScreen.Find("PageControls");
            previous = controls.Find("Previous").GetComponent<Button>(); previous.onClick.AddListener(() => PageRequested?.Invoke(-1));
            pageLabel = controls.Find("Label").GetComponent<TextMeshProUGUI>();
            next = controls.Find("Next").GetComponent<Button>(); next.onClick.AddListener(() => PageRequested?.Invoke(1));
            var add = controls.Find("+ Page").GetComponent<Button>(); add.onClick.AddListener(() => AddPageRequested?.Invoke()); editingButtons.Add(add);
            menu = true;
        }
        public void BindPalette()
        {
            if (paletteBound || assets?.Catalog == null) return;
            foreach (var asset in assets.Catalog.Backgrounds)
            {
                var button = sidebar.Find(asset.Name)?.GetComponent<Button>();
                if (button == null) continue;
                string id = asset.Id;
                button.onClick.AddListener(() => BackgroundRequested?.Invoke(id));
                editingButtons.Add(button); backgroundButtons[id] = button;
            }
            foreach (var asset in assets.Catalog.Objects)
            {
                var button = sidebar.Find("+ " + asset.Name)?.GetComponent<Button>();
                if (button == null) continue;
                string id = asset.Id;
                button.onClick.AddListener(() => ObjectRequested?.Invoke(id));
                editingButtons.Add(button);
            }
            paletteBound = true;
        }
        public void ShowStatus(string value) { status.text = value; }
        void ClearCards()
        {
            foreach (Transform child in menuGrid.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var cover in coverSprites) Destroy(cover);
            foreach (var cover in covers) Destroy(cover);
            covers.Clear(); coverSprites.Clear();
        }
        public void ShowMenu(IReadOnlyList<ProjectSummary> projects)
        {
            retry.gameObject.SetActive(true);
            menu = true; menuScreen.gameObject.SetActive(true); editorScreen.gameObject.SetActive(false); ClearCards();
            emptyState.gameObject.SetActive(projects.Count == 0);
            var rect = (RectTransform)menuGrid.transform;
            foreach (var project in projects)
            {
                var card = Panel(rect, "Project", Paper);
                var spine = Panel(card, "BookSpine", Accent); Bounds(spine, 0f, 0f, .015f, 1f);
                spine.GetComponent<Image>().raycastTarget = false;
                var image = Panel(card, "Cover", Color.white); Bounds(image, .035f, .32f, .975f, .97f);
                if (!string.IsNullOrEmpty(project.CoverPng))
                {
                    try
                    {
                        var texture = new Texture2D(2, 2); covers.Add(texture);
                        if (texture.LoadImage(Convert.FromBase64String(project.CoverPng)))
                        {
                            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                            coverSprites.Add(sprite); image.GetComponent<Image>().sprite = sprite;
                        }
                    }
                    catch (FormatException) { }
                }
                var text = Label(card, project.Title + "  •  " + project.PageCount + " pages", 24); text.color = Ink; text.fontStyle = FontStyles.Bold; Bounds(text.rectTransform, .05f, .18f, .97f, .30f);
                var edit = Button(card, "Edit", () => OpenRequested?.Invoke(project.Id, false)); Bounds((RectTransform)edit.transform, .03f, .02f, .48f, .16f);
                var play = Button(card, "Play", () => OpenRequested?.Invoke(project.Id, true), true); Bounds((RectTransform)play.transform, .52f, .02f, .97f, .16f);
            }
            Canvas.ForceUpdateCanvases(); UpdateMenuColumns();
            ShowStatus(projects.Count == 0 ? "No saved stories yet. Create your first one." : projects.Count + " saved stories. Choose Edit or Play.");
        }
        void BuildMenuChrome()
        {
            var eyebrow = Label(menuScreen, "YOUR SHELF", 20); eyebrow.color = Accent; eyebrow.fontStyle = FontStyles.Bold;
            Bounds(eyebrow.rectTransform, .03f, .95f, .7f, .99f);
            var welcome = Label(menuScreen, "Your stories", 48); welcome.font = font; welcome.fontStyle = FontStyles.Bold;
            Bounds(welcome.rectTransform, .03f, .83f, .7f, .95f);
            var create = Button(menuScreen, "New story", () => NewRequested?.Invoke(), true); Bounds((RectTransform)create.transform, .78f, .90f, .97f, .98f);
            var viewport = Panel(menuScreen, "SavedProjects", Color.clear); Bounds(viewport, .025f, .025f, .975f, .86f);
            menuViewport = viewport;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var content = new GameObject("Cards", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport, false); var rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero;
            var grid = content.GetComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(490, 400); grid.spacing = new Vector2(24, 24); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            menuGrid = grid;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = rect; scroll.scrollSensitivity = 35;
            emptyState = Panel(menuScreen, "EmptyState", new Color32(36, 80, 87, 255));
            Bounds(emptyState, .13f, .16f, .87f, .74f);
            var bookOne = Panel(emptyState, "BookOne", Paper); Bounds(bookOne, .085f, .20f, .18f, .76f);
            var bookTwo = Panel(emptyState, "BookTwo", new Color32(244, 190, 107, 255)); Bounds(bookTwo, .195f, .15f, .30f, .83f);
            var bookThree = Panel(emptyState, "BookThree", Accent); Bounds(bookThree, .315f, .25f, .42f, .70f);
            bookOne.GetComponent<Image>().raycastTarget = false;
            bookTwo.GetComponent<Image>().raycastTarget = false;
            bookThree.GetComponent<Image>().raycastTarget = false;
            var note = Label(emptyState, "A NEW CHAPTER", 18); note.color = Accent; note.fontStyle = FontStyles.Bold; Bounds(note.rectTransform, .49f, .76f, .94f, .88f);
            var emptyTitle = Label(emptyState, "Your shelf starts here", 46); emptyTitle.font = font; emptyTitle.fontStyle = FontStyles.Bold; Bounds(emptyTitle.rectTransform, .49f, .53f, .96f, .77f);
            var emptyHint = Label(emptyState, "Create a story to make your first page.", 23); emptyHint.color = Muted; Bounds(emptyHint.rectTransform, .49f, .34f, .95f, .53f);
        }
        public void ShowEditor() { retry.gameObject.SetActive(false); menu = false; menuScreen.gameObject.SetActive(false); editorScreen.gameObject.SetActive(true); }
        void BuildEditor(AssetCatalog catalog)
        {
            toolbar = Row(editorScreen, "Toolbar"); Bounds(toolbar, .015f, .90f, .985f, .985f);
            Button(toolbar, "Menu", () => MenuRequested?.Invoke());
            title = Input(toolbar); title.onEndEdit.AddListener(value => TitleChanged?.Invoke(value));
            save = Button(toolbar, "Save", () => SaveRequested?.Invoke(), true);
            preview = Button(toolbar, "Preview", () => PreviewRequested?.Invoke());
            sidebar = Panel(editorScreen, "AssetPalette", Ink); Bounds(sidebar, .015f, .025f, .225f, .88f);
            var column = sidebar.gameObject.AddComponent<VerticalLayoutGroup>(); column.padding = new RectOffset(20, 20, 17, 17); column.spacing = 10; column.childControlWidth = true; column.childForceExpandWidth = true; column.childControlHeight = true; column.childForceExpandHeight = false;
            var backgrounds = Label(sidebar, "BACKGROUNDS", 21); backgrounds.color = Accent; backgrounds.fontStyle = FontStyles.Bold;
            foreach (var asset in catalog.Backgrounds)
            {
                var choice = Button(sidebar, asset.Name, () => BackgroundRequested?.Invoke(asset.Id));
                editingButtons.Add(choice); backgroundButtons[asset.Id] = choice;
            }
            var objects = Label(sidebar, "OBJECTS", 21); objects.color = Accent; objects.fontStyle = FontStyles.Bold;
            foreach (var asset in catalog.Objects) editingButtons.Add(Button(sidebar, "+ " + asset.Name, () => ObjectRequested?.Invoke(asset.Id)));
            var help = Label(sidebar, "Drag to move. Select to resize or remove.", 18); help.color = Muted;
            editingButtons.Add(Button(sidebar, "Smaller −", () => ScaleRequested?.Invoke(.85f)));
            editingButtons.Add(Button(sidebar, "Larger +", () => ScaleRequested?.Invoke(1.15f)));
            editingButtons.Add(Button(sidebar, "Remove selected", () => DeleteRequested?.Invoke()));
            stageHolder = Panel(editorScreen, "StageHolder", Color.clear); Bounds(stageHolder, .245f, .14f, .985f, .88f);
            stage = Panel(stageHolder, "Page", Color.white); Stretch(stage);
            var aspect = stage.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectRatio = 16f / 9; aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            stage.gameObject.AddComponent<RectMask2D>();
            var pageEdge = stage.gameObject.AddComponent<Outline>(); pageEdge.effectColor = Paper; pageEdge.effectDistance = new Vector2(3, -3);
            var bottom = Row(editorScreen, "PageControls"); Bounds(bottom, .245f, .025f, .985f, .115f);
            previous = Button(bottom, "Previous", () => PageRequested?.Invoke(-1));
            pageLabel = Label(bottom, "", 23); pageLabel.color = Muted; pageLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            next = Button(bottom, "Next", () => PageRequested?.Invoke(1));
            editingButtons.Add(Button(bottom, "+ Page", () => AddPageRequested?.Invoke(), true));
        }
        public void Render(EditorController controller)
        {
            state = controller;
            if (menu || controller.Project == null) return;
            title.SetTextWithoutNotify(controller.Project.Title);
            title.interactable = controller.CanEdit;
            save.interactable = controller.CanEdit;
            preview.interactable = !controller.Busy;
            preview.GetComponentInChildren<TMP_Text>().text = controller.Playback ? "Back to editor" : "Preview";
            foreach (var button in editingButtons) button.interactable = controller.CanEdit;
            foreach (var choice in backgroundButtons)
            {
                bool selected = choice.Key == controller.Page.BackgroundId;
                choice.Value.GetComponent<Image>().color = selected ? Accent : Field;
                choice.Value.GetComponentInChildren<TMP_Text>().color = selected ? Ink : Paper;
            }
            sidebar.gameObject.SetActive(!controller.Playback);
            Bounds(stageHolder, controller.Playback ? .03f : .245f, .14f, .985f, .88f);
            previous.interactable = !controller.Busy && controller.PageIndex > 0;
            next.interactable = !controller.Busy && controller.PageIndex + 1 < controller.Project.Pages.Count;
            pageLabel.text = $"Page {controller.PageIndex + 1} / {controller.Project.Pages.Count}" + (controller.Playback ? "  ·  Read-only playback" : controller.Dirty ? "  ·  Unsaved changes" : "  ·  Saved");
            Canvas.ForceUpdateCanvases();
            RenderPage();
        }
        void LateUpdate() { if (menu) UpdateMenuColumns(); if (stage != null && stage.rect.size != lastSize) RenderPage(); }
        void UpdateMenuColumns()
        {
            if (menuGrid == null || menuViewport == null) return;
            int count = Mathf.Clamp(Mathf.FloorToInt((menuViewport.rect.width + menuGrid.spacing.x) /
                (menuGrid.cellSize.x + menuGrid.spacing.x)), 1, 3);
            if (menuGrid.constraintCount != count) menuGrid.constraintCount = count;
        }
        void RenderPage()
        {
            if (stage == null || state?.Page == null) return;
            lastSize = stage.rect.size;
            foreach (Transform child in stage) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            stage.GetComponent<Image>().sprite = assets.Find(state.Page.BackgroundId);
            foreach (var item in state.Page.Objects)
            {
                var rect = Panel(stage, "Object-" + item.Id, Color.white); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = Vector2.one * stage.rect.width * .17f * item.Scale;
                rect.anchoredPosition = new Vector2((item.X - .5f) * stage.rect.width, (item.Y - .5f) * stage.rect.height);
                rect.GetComponent<Image>().sprite = assets.Find(item.AssetId); rect.GetComponent<Image>().preserveAspect = true;
                if (!state.Playback && item.Id == state.SelectedId) { var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color32(60, 190, 245, 255); outline.effectDistance = new Vector2(3, 3); }
                var drag = rect.gameObject.AddComponent<PlacedObjectView>(); drag.Id = item.Id; drag.Stage = stage; drag.Editable = state.CanEdit;
                drag.Selected = id => Selected?.Invoke(id); drag.Moved = (id, x, y) => Moved?.Invoke(id, x, y);
            }
        }
        public void ConfirmDiscard(Action discard)
        {
            if (confirmation != null) return;
            confirmation = Panel(root, "UnsavedChanges", new Color32(40, 54, 75, 255)); Bounds(confirmation, .25f, .36f, .75f, .65f);
            var label = Label(confirmation, "You have unsaved changes.\nReturn to editing to save them, or discard them.", 28); Bounds(label.rectTransform, .04f, .42f, .96f, .95f);
            var keep = Button(confirmation, "Keep editing", () => { Destroy(confirmation.gameObject); confirmation = null; }); Bounds((RectTransform)keep.transform, .04f, .08f, .47f, .32f);
            var leave = Button(confirmation, "Discard & menu", () => { Destroy(confirmation.gameObject); confirmation = null; discard(); }); Bounds((RectTransform)leave.transform, .53f, .08f, .96f, .32f);
        }
        RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); go.GetComponent<Image>().color = color; return (RectTransform)go.transform;
        }
        RectTransform Row(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)); go.transform.SetParent(parent, false);
            var layout = go.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 14; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false; return (RectTransform)go.transform;
        }
        TextMeshProUGUI Label(Transform parent, string text, float size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(RTLTextMeshPro)); go.transform.SetParent(parent, false);
            var label = go.GetComponent<RTLTextMeshPro>(); label.Farsi = false; label.PreserveNumbers = true;
            label.font = ContainsArabic(text) || chromeFont == null ? font : chromeFont;
            label.fontSize = size; label.color = Paper; label.richText = false; label.raycastTarget = false; label.text = text; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        Button Button(Transform parent, string text, Action click, bool primary = false)
        {
            var rect = Panel(parent, text, primary ? Accent : Field);
            var size = rect.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = 170; size.minHeight = 46;
            var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => click());
            var label = Label(rect, text, 22); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center;
            label.color = primary ? Ink : Paper;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.95f, .95f, .95f, 1f);
            colors.pressedColor = new Color(.77f, .77f, .77f, 1f);
            colors.disabledColor = new Color(.52f, .59f, .62f, 1f);
            button.colors = colors;
            return button;
        }
        TMP_InputField Input(Transform parent)
        {
            var rect = Panel(parent, "Title", Paper); rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var edge = rect.gameObject.AddComponent<Outline>(); edge.effectColor = Accent; edge.effectDistance = new Vector2(2, -2);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)); area.transform.SetParent(rect, false); Stretch((RectTransform)area.transform);
            ((RectTransform)area.transform).offsetMin = new Vector2(16, 0);
            ((RectTransform)area.transform).offsetMax = new Vector2(-16, 0);
            // Keep the editable logical text in TMP; shaping applies to rendered cards/headings.
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); text.transform.SetParent(area.transform, false); Stretch((RectTransform)text.transform);
            var label = text.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 25; label.color = Ink; label.richText = false;
            input.textViewport = (RectTransform)area.transform; input.textComponent = label; input.characterLimit = 80; return input;
        }
        static void Stretch(RectTransform rect) => Bounds(rect, 0, 0, 1, 1);
        static bool ContainsArabic(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value) if ((c >= '؀' && c <= 'ۿ') || (c >= 'ݐ' && c <= 'ݿ') || (c >= 'ﭐ' && c <= '﷿')) return true;
            return false;
        }
        static void Bounds(RectTransform rect, float x0, float y0, float x1, float y1)
        { rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}

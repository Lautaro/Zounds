// ZUIStyleEditorWindow.cs
// Open via:  Tools / ZUI Style Editor

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ── Editor window ─────────────────────────────────────────────────────────────

public class ZUIStyleEditorWindow : ZUIWindow
{
    // ── State ─────────────────────────────────────────────────────────────────

    private ZUIStyleSheetAsset _sheet;

    private int _activeTab;        // 0 = Buttons, 1 = Boxes, 2 = Text, 3 = Sliders, 4 = Global, 5 = Palette, 6 = Missing
    private int _globalSubTab;     // 0 = Button, 1 = Box, 2 = Layout
    private int _selectedButton;
    private int _selectedBox;
    private int _selectedText;
    private int _selectedSlider;
    private int _buttonStateTab;      // 0 = Normal, 1 = Hover, 2 = Active
    private int _sliderThumbModeTab;  // 0 = Normal (single), 1 = MinMax (two thumbs)
    private int _sliderThumbMinState; // 0 = Normal, 1 = Hover, 2 = Active  (min thumb inspector)
    private int _sliderThumbMaxState; // 0 = Normal, 1 = Hover, 2 = Active  (max thumb inspector)

    [SerializeField] private string _previewButtonText   = "Button";
    [SerializeField] private bool   _previewToggleValue  = false;
    [SerializeField] private bool   _previewIsToggleMode = false;  // false=Button preview, true=Toggle preview
    [SerializeField] private int    _buttonPreviewBgMode  = 0;  // 0=None, 1=Box
    [SerializeField] private int    _buttonPreviewBoxIndex = 0;
    private string _previewBoxTitle     = "Box Title";
    private string _previewBoxContent   = "Sample content that wraps when the box is narrow enough to show word wrap in action.";
    private string _previewTextContent       = "Sample text with this style";
    private int    _textPreviewBgMode        = 0;  // 0=None, 1=Box, 2=Button
    private int    _textPreviewBoxIndex      = 0;
    private int    _textPreviewButtonIndex   = 0;

    private Vector2 _listScroll;
    private Vector2 _inspectorScroll;

    private bool  _listCollapsed   = false;
    private bool  _listHovered     = false;
    private const float k_CollapsedWidth = 14f;

    // Legacy corner preview — when true, DrawManualButton renders with r=0
    private bool _simulateLegacy;

    private string _exportClassName = "ZUIStyles";
    private string _exportPath      = "Assets/Editor/ZUIStyles.Generated.cs";

    private GUIStyle _listItemStyle;
    private GUIStyle _listItemActiveStyle;
    private GUIStyle _sectionHeaderStyle;

    private const float k_ListWidth  = 170f;
    private const float k_LabelWidth = 82f;

    // ── Foldout state ─────────────────────────────────────────────────────────

    private readonly Dictionary<string, bool> _foldouts = new Dictionary<string, bool>();

    bool GetFoldout(string key)  => !_foldouts.TryGetValue(key, out var v) || v;   // default = expanded
    void SetFoldout(string key, bool v) => _foldouts[key] = v;

    // ── Copy / paste clipboards ───────────────────────────────────────────────

    private static ZUIButtonDef _clipButton;
    private static ZUIBoxDef    _clipBox;

    private static ZUIGradient _clipGradient;

    private static (ZUIGradient n, ZUIGradient h, ZUIGradient a)? _clipBtnBackground;
    private static (Color color, int fontSize, FontStyle fontStyle)? _clipBtnText;
    private static ZUIGradient                                    _clipBtnHoverBg;
    private static ZUIGradient                                    _clipBtnActiveBg;
    private static (Color color, int fontSize, FontStyle fontStyle)? _clipBtnHoverText;
    private static (Color color, int fontSize, FontStyle fontStyle)? _clipBtnActiveText;
    private static (Color c1, Color c2, bool dual, float w)?         _clipBtnHoverBorder;
    private static (Color c1, Color c2, bool dual, float w)?         _clipBtnActiveBorder;
    private static (int r, bool useGlobal)?                          _clipBtnShape;
    private static (int h, int v, bool useGlobal)?                   _clipBtnPadding;
    private static (Color c1, Color c2, bool dual, float w, bool useGlobal)? _clipBtnBorder;

    private static ZUIGradient                                               _clipBoxBackground;
    private static (Color c1, Color c2, bool dual, float w, bool useGlobal)? _clipBoxBorder;
    private static (Color color, int fontSize, FontStyle fontStyle)?          _clipBoxLabel;
    private static (Color color, int fontSize, FontStyle fontStyle)?          _clipBoxContentText;
    private static (int h, int v, bool useGlobal)?                            _clipBoxPadding;
    private static (int r, bool useGlobal)?                                   _clipBoxShape;

    // ── Open ──────────────────────────────────────────────────────────────────

    [MenuItem("Tools/ZUI Style Editor")]
    public static void Open() => GetWindow<ZUIStyleEditorWindow>("ZUI Style Editor");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override void OnZUIEnable()
    {
        var lastPath = EditorPrefs.GetString("ZUIStyleEditor_LastSheet", "");
        if (string.IsNullOrEmpty(lastPath)) lastPath = ZUI.k_DefaultSheetPath;
        var asset = AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(lastPath);
        if (asset != null) SetSheet(asset);
    }

    private void OnDisable()
    {
        ZUI.SimulateLegacyCorners = false;
        if (_sheet != null)
            EditorPrefs.SetString("ZUIStyleEditor_LastSheet", AssetDatabase.GetAssetPath(_sheet));
    }

    // ── OnZUI ─────────────────────────────────────────────────────────────────

    protected override void OnZUI()
    {
        EnsureStyles();
        DrawTopBar();
        EditorGUILayout.Space(2f);

        if (_sheet == null) { DrawNoSheetUI(); return; }

        DrawTabBar();

        var contentRect = new Rect(0, 56f, position.width, position.height - 56f);
        GUILayout.BeginArea(contentRect);

        if (_activeTab == 4)
        {
            DrawGlobalInspector();
        }
        else if (_activeTab == 5)
        {
            DrawPaletteTab();
        }
        else if (_activeTab == 6)
        {
            DrawMissingTab();
        }
        else
        {
            GUILayout.BeginHorizontal();
            DrawListPanel();
            DrawDivider();
            DrawInspectorPanel();
            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    // ── Top bar ───────────────────────────────────────────────────────────────

    void DrawTopBar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Sheet:", GUILayout.Width(40f));
        var newSheet = EditorGUILayout.ObjectField(_sheet, typeof(ZUIStyleSheetAsset), false,
            GUILayout.Width(180f)) as ZUIStyleSheetAsset;
        if (newSheet != _sheet) SetSheet(newSheet);
        if (GUILayout.Button("New Sheet", EditorStyles.toolbarButton, GUILayout.Width(76f)))
            CreateNewSheet();
        if (_sheet != null)
        {
            GUILayout.Space(10f);
            EditorGUILayout.LabelField("Icons:", GUILayout.Width(38f));
            EditorGUI.BeginChangeCheck();
            _sheet.iconLibrary = EditorGUILayout.ObjectField(_sheet.iconLibrary,
                typeof(ZUIIconLibraryAsset), false, GUILayout.Width(150f)) as ZUIIconLibraryAsset;
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_sheet);
            if (GUILayout.Button("Browser", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                GetWindow<ZUIAssetBrowserWindow>("Asset Browser");
        }
        GUILayout.FlexibleSpace();
        if (_sheet != null)
        {
            EditorGUILayout.LabelField("Class:", GUILayout.Width(36f));
            _exportClassName = EditorGUILayout.TextField(_exportClassName, GUILayout.Width(100f));
            if (GUILayout.Button("Export C#", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                Export();
            if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                ExportJson();
        }
        if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            ImportJson();
        GUILayout.EndHorizontal();
    }

    // ── Tab bar ───────────────────────────────────────────────────────────────

    void DrawTabBar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        var labels = new[] { "Buttons", "Boxes", "Text", "Sliders", "Global", "Palette" };
        for (int i = 0; i < labels.Length; i++)
        {
            bool active = _activeTab == i;
            if (GUILayout.Toggle(active, labels[i], EditorStyles.toolbarButton, GUILayout.Width(70f)) && !active)
                _activeTab = i;
        }

        // Missing tab — shows badge count when there are unresolved style lookups
        int missingCount = ZUIMissingStyleRegistry.Count;
        string missingLabel = missingCount > 0 ? $"Missing ({missingCount})" : "Missing";
        bool missingActive = _activeTab == 6;
        var prevColor = GUI.color;
        if (missingCount > 0) GUI.color = new Color(1f, 0.45f, 0.35f, 1f);
        if (GUILayout.Toggle(missingActive, missingLabel, EditorStyles.toolbarButton, GUILayout.Width(90f)) && !missingActive)
            _activeTab = 6;
        GUI.color = prevColor;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    // ── No sheet ──────────────────────────────────────────────────────────────

    void DrawNoSheetUI()
    {
        GUILayout.Space(40f);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical(GUILayout.Width(300f));
        EditorGUILayout.LabelField("No style sheet loaded.", EditorStyles.boldLabel);
        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Assign an existing ZUIStyleSheetAsset above,\nor create a new one.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10f);
        if (GUILayout.Button("Create New Style Sheet")) CreateNewSheet();
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    // ── List panel ────────────────────────────────────────────────────────────

    void DrawListPanel()
    {
        bool showExpanded = !_listCollapsed || _listHovered;
        float panelWidth  = showExpanded ? k_ListWidth : k_CollapsedWidth;

        GUILayout.BeginVertical(GUILayout.Width(panelWidth), GUILayout.ExpandHeight(true));

        if (showExpanded)
        {
            // ── Collapse toggle button ─────────────────────────────────────
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("◀", EditorStyles.miniButton, GUILayout.Width(20f), GUILayout.Height(16f)))
            {
                _listCollapsed = true;
                _listHovered   = false;
            }
            GUILayout.EndHorizontal();

            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));

            if (_activeTab == 0)
                DrawDynamicList(_sheet.buttons, ref _selectedButton,
                    () => new ZUIButtonDef("New Button",
                        new Color(.22f, .22f, .26f, 1f), new Color(.30f, .30f, .36f, 1f),
                        new Color(.16f, .16f, .20f, 1f), new Color(.88f, .88f, .88f, 1f)));
            else if (_activeTab == 1)
                DrawDynamicList(_sheet.boxes, ref _selectedBox,
                    () => new ZUIBoxDef("New Box",
                        new Color(.18f, .18f, .22f, 1f), new Color(.90f, .90f, .90f, 1f),
                        new Color(1f, 1f, 1f, .06f), 1f, 8, 6));
            else if (_activeTab == 2)
                DrawDynamicList(_sheet.textStyles, ref _selectedText,
                    () => new ZUITextStyleDef { name = "New Text Style" });
            else // tab 3 = Sliders
                DrawDynamicList(_sheet.sliders, ref _selectedSlider,
                    () => new ZUISliderDef { name = "New Slider" });

            GUILayout.EndScrollView();
        }
        else
        {
            // ── Collapsed strip ───────────────────────────────────────────
            var stripRect = GUILayoutUtility.GetRect(k_CollapsedWidth, 9999f,
                GUILayout.Width(k_CollapsedWidth), GUILayout.ExpandHeight(true));

            // Hover detection
            bool nowHovered = stripRect.Contains(Event.current.mousePosition);
            if (nowHovered != _listHovered) { _listHovered = nowHovered; Repaint(); }

            // Background — darker normally, slightly lighter on hover
            var prevColor = GUI.color;
            GUI.color = _listHovered ? new Color(.35f, .35f, .40f, 1f) : new Color(.22f, .22f, .26f, 1f);
            GUI.DrawTexture(stripRect, EditorGUIUtility.whiteTexture);
            GUI.color = prevColor;

            // Click anywhere on strip to expand
            EditorGUIUtility.AddCursorRect(stripRect, MouseCursor.Arrow);
            if (GUI.Button(stripRect, GUIContent.none, GUIStyle.none))
            {
                _listCollapsed = false;
                _listHovered   = false;
            }

            // Rotated "◀ Styles" label
            var matrix  = GUI.matrix;
            var pivot   = new Vector2(stripRect.x + stripRect.width * 0.5f, stripRect.y + stripRect.height * 0.5f);
            GUIUtility.RotateAroundPivot(-90f, pivot);
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(.80f, .80f, .85f, 1f) }
            };
            GUI.Label(new Rect(pivot.x - stripRect.height * 0.5f,
                               pivot.y - 7f,
                               stripRect.height, 14f),
                      "◀ Styles", labelStyle);
            GUI.matrix = matrix;
        }

        GUILayout.EndVertical();
    }

    void DrawDynamicList<T>(List<T> items, ref int selected, Func<T> createNew) where T : class
    {
        bool dirty = false;
        int  duplicateAt = -1;
        int  removeAt    = -1;

        for (int i = 0; i < items.Count; i++)
        {
            string label    = GetStyleName(items[i]) ?? i.ToString();
            bool   isActive = selected == i;
            var    style    = isActive ? _listItemActiveStyle : _listItemStyle;

            GUILayout.BeginHorizontal();
            var rect = GUILayoutUtility.GetRect(new GUIContent(label), style,
                           GUILayout.ExpandWidth(true), GUILayout.Height(22f));
            if (GUI.Button(rect, label, style)) selected = i;

            if (GUILayout.Button("⧉", EditorStyles.miniButton, GUILayout.Width(20f), GUILayout.Height(22f)))
                duplicateAt = i;

            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20f), GUILayout.Height(22f))
                && items.Count > 1)
                removeAt = i;

            GUILayout.EndHorizontal();
        }

        // Process deferred mutations after the loop (avoids modifying list mid-iteration)
        if (duplicateAt >= 0)
        {
            var clone = DuplicateStyleItem(items[duplicateAt]);
            items.Insert(duplicateAt + 1, clone);
            selected = duplicateAt + 1;
            dirty = true;
        }
        if (removeAt >= 0)
        {
            items.RemoveAt(removeAt);
            if (selected >= items.Count) selected = items.Count - 1;
            dirty = true;
        }

        GUILayout.Space(4f);
        if (GUILayout.Button("+ Add Style", EditorStyles.miniButton))
        {
            items.Add(createNew());
            selected = items.Count - 1;
            dirty = true;
        }

        if (dirty) EditorUtility.SetDirty(_sheet);
    }

    T DuplicateStyleItem<T>(T source) where T : class
    {
        // Serialize via JSON for a deep copy (works for all ZUI def types)
        string json = JsonUtility.ToJson(source);
        var clone   = JsonUtility.FromJson<T>(json);
        // Append " (Copy)" to the name field via reflection
        var nameProp = clone.GetType().GetField("name");
        if (nameProp != null)
        {
            string n = nameProp.GetValue(clone) as string ?? "";
            if (!n.EndsWith(" (Copy)")) n += " (Copy)";
            nameProp.SetValue(clone, n);
        }
        // Invalidate cached GUIStyles if the type supports it
        clone.GetType().GetMethod("Invalidate")?.Invoke(clone, null);
        return clone;
    }

    // ── Inspector panel ───────────────────────────────────────────────────────

    void DrawInspectorPanel()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
        if (_activeTab == 0)      DrawButtonInspector();
        else if (_activeTab == 1) DrawBoxInspector();
        else if (_activeTab == 2) DrawTextStyleInspector();
        else                      DrawSliderInspector();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // ── Button inspector ──────────────────────────────────────────────────────

    void DrawButtonInspector()
    {
        if (_selectedButton < 0 || _selectedButton >= _sheet.buttons.Count)
        { CenteredLabel("Select a button style."); return; }

        var def = _sheet.buttons[_selectedButton];
        bool changed = false;
        EditorGUIUtility.labelWidth = k_LabelWidth;

        InspectorHeader("Button Style");

        GUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        def.name = EditorGUILayout.TextField("Name", def.name);
        if (EditorGUI.EndChangeCheck()) { ZUIMissingStyleRegistry.Remove(ZUIMissingStyleRegistry.EntryType.Button, def.name); changed = true; }
        if (GUILayout.Button("Flash", EditorStyles.miniButton, GUILayout.Width(44f))) ZUI.StartFlash(def.name, ZUI.FlashDefType.Button);
        if (GUILayout.Button("Copy",  EditorStyles.miniButton, GUILayout.Width(44f))) _clipButton = CopyButtonDef(def);
        GUI.enabled = _clipButton != null;
        if (GUILayout.Button("Paste", EditorStyles.miniButton, GUILayout.Width(44f)))
            { PasteButtonDef(def, _clipButton); def.Invalidate(); changed = true; }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        DrawPreviewHeader();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preview as", GUILayout.Width(k_LabelWidth));
        int previewMode = GUILayout.Toolbar(def.previewAsToggle ? 1 : 0,
            new[] { "Button", "Toggle" }, EditorStyles.miniButton);
        if (def.previewAsToggle != (previewMode == 1)) { def.previewAsToggle = previewMode == 1; changed = true; }
        _previewIsToggleMode = def.previewAsToggle;
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);

        if (_previewIsToggleMode)
            DrawTogglePreview(def);
        else
            DrawButtonPreview(def);
        GUILayout.Space(4f);

        var stateTabs = _previewIsToggleMode
            ? new[] { "Normal (Off)", "Active (On)" }
            : new[] { "Normal", "Hover", "Active" };
        // Clamp only when out of range for the current mode (e.g. index 2 when in toggle mode which has 2 tabs).
        if (_buttonStateTab >= stateTabs.Length) _buttonStateTab = stateTabs.Length - 1;
        _buttonStateTab = GUILayout.Toolbar(_buttonStateTab, stateTabs, EditorStyles.miniButton);
        GUILayout.Space(2f);

        if (_previewIsToggleMode)
        {
            if (_buttonStateTab == 0) changed |= DrawButtonNormalState(def);
            else                      changed |= DrawButtonActiveState(def);
        }
        else
        {
            if (_buttonStateTab == 0)      changed |= DrawButtonNormalState(def);
            else if (_buttonStateTab == 1) changed |= DrawButtonHoverState(def);
            else                           changed |= DrawButtonActiveState(def);
        }

        if (changed) { EditorUtility.SetDirty(_sheet); RepaintShowcase(); }

        GUILayout.Space(4f);
        bool shapeGlobalNewTop;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Shape",
            () => _clipBtnShape = (def.cornerRadius, def.useGlobalShape),
            () => { if (_clipBtnShape.HasValue)
                    { def.cornerRadius = _clipBtnShape.Value.r;
                      def.useGlobalShape = _clipBtnShape.Value.useGlobal;
                      def.Invalidate(); changed = true; } },
            _clipBtnShape.HasValue, def.useGlobalShape, out shapeGlobalNewTop, "btn_shape_top"))
        {
            EditorGUI.BeginChangeCheck();
            {
                var gs = def.useGlobalShape ? ZUI.ActiveSheet?.globalButton : null;
                int dispR = gs != null ? gs.cornerRadius : def.cornerRadius;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalShape))
                {
                    int newR = EditorGUILayout.IntSlider("Corner Radius", dispR, 0, 16);
                    if (!def.useGlobalShape) def.cornerRadius = newR;

                    if (dispR > 0 && !def.useGlobalShape)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Round corners", GUILayout.Width(k_LabelWidth));
                        def.roundTL = EditorGUILayout.ToggleLeft("TL", def.roundTL, GUILayout.Width(34f));
                        def.roundTR = EditorGUILayout.ToggleLeft("TR", def.roundTR, GUILayout.Width(34f));
                        def.roundBL = EditorGUILayout.ToggleLeft("BL", def.roundBL, GUILayout.Width(34f));
                        def.roundBR = EditorGUILayout.ToggleLeft("BR", def.roundBR, GUILayout.Width(34f));
                        GUILayout.EndHorizontal();
                    }
                }
            }
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }
        }
        if (shapeGlobalNewTop != def.useGlobalShape) { def.useGlobalShape = shapeGlobalNewTop; def.Invalidate(); changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }

        GUILayout.Space(4f);
        if (InspectorSubheaderWithToggles("Animation", "btn_anim",
                "Hover", ref def.hoverAnimEnabled,
                "Click", ref def.clickAnimEnabled,
                out bool animChanged))
        {
            EditorGUI.BeginChangeCheck();
            if (def.hoverAnimEnabled)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Hover In / Out (s)", GUILayout.Width(k_LabelWidth));
                def.hoverInDuration  = Mathf.Max(0.01f, EditorGUILayout.FloatField(def.hoverInDuration,  GUILayout.Width(50f)));
                EditorGUILayout.LabelField("/", GUILayout.Width(12f));
                def.hoverOutDuration = Mathf.Max(0.01f, EditorGUILayout.FloatField(def.hoverOutDuration, GUILayout.Width(50f)));
                GUILayout.EndHorizontal();
            }
            if (def.clickAnimEnabled)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Click In / Out (s)", GUILayout.Width(k_LabelWidth));
                def.clickInDuration  = Mathf.Max(0.01f, EditorGUILayout.FloatField(def.clickInDuration,  GUILayout.Width(50f)));
                EditorGUILayout.LabelField("/", GUILayout.Width(12f));
                def.clickOutDuration = Mathf.Max(0.01f, EditorGUILayout.FloatField(def.clickOutDuration, GUILayout.Width(50f)));
                GUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck()) { changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }
        }
        if (animChanged) { changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }

        GUILayout.Space(10f);
        DrawExportPathField();
    }

    bool DrawButtonNormalState(ZUIButtonDef def)
    {
        bool changed = false;
        Action invalidate = () => { def.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };

        bool bgGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Background",
            () => _clipBtnBackground = (def.normal.Clone(), def.hover.Clone(), def.active.Clone()),
            () => { if (_clipBtnBackground.HasValue) { PasteGrad(def.normal, _clipBtnBackground.Value.n); def.Invalidate(); changed = true; } },
            _clipBtnBackground.HasValue, def.useGlobalBackground, out bgGlobalNew, "btn_n_bg"))
        {
            var bgSource = def.useGlobalBackground ? (ZUI.ActiveSheet?.globalButton?.normal ?? def.normal) : def.normal;
            using (new EditorGUI.DisabledGroupScope(def.useGlobalBackground))
            {
                if (DrawGradientField("Fill", bgSource, def.useGlobalBackground ? null : invalidate)) { def.Invalidate(); changed = true; }
            }
        }
        if (bgGlobalNew != def.useGlobalBackground) { def.useGlobalBackground = bgGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        bool borderGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Border",
            () => _clipBtnBorder = (def.border.gradient.colorA, def.border.gradient.colorB, def.border.gradient.isGradient, def.border.width, def.useGlobalBorder),
            () => { if (_clipBtnBorder.HasValue) {
                        var c = _clipBtnBorder.Value;
                        def.border.gradient.colorA = c.c1; def.border.gradient.colorB = c.c2;
                        def.border.gradient.isGradient = c.dual; def.border.gradient.Invalidate();
                        def.border.width = c.w;
                        def.useGlobalBorder = c.useGlobal; changed = true; } },
            _clipBtnBorder.HasValue, def.useGlobalBorder, out borderGlobalNew, "btn_n_border"))
        {
            EditorGUI.BeginChangeCheck();
            if (def.useGlobalBorder)
            {
                var gb = ZUI.ActiveSheet?.globalButton;
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawBorderField(gb ?? def, null);
            }
            else
                DrawBorderField(def, () => { EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); });
            if (EditorGUI.EndChangeCheck()) changed = true;
        }
        if (borderGlobalNew != def.useGlobalBorder) { def.useGlobalBorder = borderGlobalNew; changed = true; }

        GUILayout.Space(2f);

        bool txtGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Text",
            () => _clipBtnText = (def.text.color, def.text.fontSize, def.text.fontStyle),
            () => { if (_clipBtnText.HasValue) {
                        var c = _clipBtnText.Value;
                        def.text.color = c.color; def.text.fontSize = c.fontSize;
                        def.text.fontStyle = c.fontStyle; def.Invalidate(); changed = true; } },
            _clipBtnText.HasValue, def.useGlobalText, out txtGlobalNew, "btn_n_text"))
        {
            var  txtSource = def.useGlobalText      ? (ZUI.ActiveSheet?.globalButton?.text ?? def.text)
                           : !string.IsNullOrEmpty(def.textStyleId) ? (ZUI.ActiveSheet?.FindText(def.textStyleId)?.text ?? def.text)
                           : def.text;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(def.useGlobalText))
            {
                DrawTextRowWithStyleRef(txtSource, ref def.textStyleId, out bool refChg);
                if (refChg) { def.Invalidate(); changed = true; }
            }
            bool txtLocked = def.useGlobalText || !string.IsNullOrEmpty(def.textStyleId);
            using (new EditorGUI.DisabledGroupScope(txtLocked))
                DrawShadowTextRow(txtSource);
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }
        if (txtGlobalNew != def.useGlobalText) { def.useGlobalText = txtGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        bool sizeGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Padding",
            () => _clipBtnPadding = (def.padH, def.padV, def.useGlobalPadding),
            () => { if (_clipBtnPadding.HasValue) {
                        def.padH = _clipBtnPadding.Value.h; def.padV = _clipBtnPadding.Value.v;
                        def.useGlobalPadding = _clipBtnPadding.Value.useGlobal;
                        def.Invalidate(); changed = true; } },
            _clipBtnPadding.HasValue, def.useGlobalPadding, out sizeGlobalNew, "btn_n_size"))
        {
            EditorGUI.BeginChangeCheck();
            // Text pad + icon pad on one row
            GUILayout.BeginHorizontal();
            {
                var gp = def.useGlobalPadding ? ZUI.ActiveSheet?.globalButton : null;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalPadding))
                {
                    float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
                    EditorGUILayout.LabelField("Text", GUILayout.Width(28f));
                    int tH = Mathf.Max(0, EditorGUILayout.IntField("H", gp != null ? gp.padH : def.padH, GUILayout.Width(42f)));
                    int tV = Mathf.Max(0, EditorGUILayout.IntField("V", gp != null ? gp.padV : def.padV, GUILayout.Width(42f)));
                    GUILayout.Space(8f);
                    EditorGUILayout.LabelField("Icon", GUILayout.Width(28f));
                    int iH = Mathf.Max(0, EditorGUILayout.IntField("H", gp != null ? gp.iconPadH : def.iconPadH, GUILayout.Width(42f)));
                    int iV = Mathf.Max(0, EditorGUILayout.IntField("V", gp != null ? gp.iconPadV : def.iconPadV, GUILayout.Width(42f)));
                    EditorGUIUtility.labelWidth = _lw;
                    if (!def.useGlobalPadding) { def.padH = tH; def.padV = tV; def.iconPadH = iH; def.iconPadV = iV; }
                }
            }
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }
        if (sizeGlobalNew != def.useGlobalPadding) { def.useGlobalPadding = sizeGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        if (InspectorSubheaderWithToggle("Background Shadow", "btn_n_shadow", ref def.bgShadow.enabled, out bool shadowToggleChanged))
        {
            if (def.bgShadow.enabled)
            {
                EditorGUI.BeginChangeCheck();
                DrawBgShadowFields(def.bgShadow.color, def.bgShadow.offset, ref def.bgShadow.colorRef, ref def.bgShadow.colorSlot,
                    out Color newColor, out Vector2 newOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    def.bgShadow.color   = newColor;
                    def.bgShadow.offset  = newOffset;
                    changed = true;
                }
            }
        }
        if (shadowToggleChanged) changed = true;

        return changed;
    }

    bool DrawButtonHoverState(ZUIButtonDef def)
    {
        bool changed = false;
        Action invalidate = () => { def.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };

        bool hoverBgOvNew;
        Action revertHoverBg = () => { PasteGrad(def.hover, def.normal); def.Invalidate(); changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };
        bool bgExp = InspectorSubheaderWithOverrideCopyPaste("Background", def.hoverBgOverride, out hoverBgOvNew,
            () => _clipBtnHoverBg = def.hover.Clone(),
            () => { if (_clipBtnHoverBg != null) { PasteGrad(def.hover, _clipBtnHoverBg); def.Invalidate(); changed = true; } },
            _clipBtnHoverBg != null,
            def.hoverBgOverride ? revertHoverBg : null, "btn_h_bg");
        if (hoverBgOvNew != def.hoverBgOverride) { def.hoverBgOverride = hoverBgOvNew; def.Invalidate(); changed = true; }
        if (bgExp)
        {
            if (def.hoverBgOverride)
            {
                if (DrawGradientField("Fill", def.hover, invalidate, def.normal, "Normal"))
                    { def.Invalidate(); changed = true; }
            }
            else
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawGradientField("Fill (Normal)", def.normal, null);
            }
        }

        GUILayout.Space(2f);

        bool hoverBdrOvNew;
        Action revertHoverBdr = () => {
            PasteGrad(def.hoverBorder.gradient, def.border.gradient);
            def.hoverBorder.width = def.border.width;
            changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint();
        };
        bool bdrExp = InspectorSubheaderWithOverrideCopyPaste("Border", def.hoverBorderOverride, out hoverBdrOvNew,
            () => _clipBtnHoverBorder = (def.hoverBorder.gradient.colorA, def.hoverBorder.gradient.colorB, def.hoverBorder.gradient.isGradient, def.hoverBorder.width),
            () => { if (_clipBtnHoverBorder.HasValue) { var c = _clipBtnHoverBorder.Value; def.hoverBorder.gradient.colorA = c.c1; def.hoverBorder.gradient.colorB = c.c2; def.hoverBorder.gradient.isGradient = c.dual; def.hoverBorder.gradient.Invalidate(); def.hoverBorder.width = c.w; changed = true; } },
            _clipBtnHoverBorder.HasValue,
            def.hoverBorderOverride ? revertHoverBdr : null, "btn_h_border");
        if (hoverBdrOvNew != def.hoverBorderOverride) { def.hoverBorderOverride = hoverBdrOvNew; changed = true; }
        if (bdrExp)
        {
            EditorGUI.BeginChangeCheck();
            if (def.hoverBorderOverride)
                DrawHoverBorderRow(def);
            else
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawBorderReadOnlyRow(def.border);
            }
            if (EditorGUI.EndChangeCheck()) changed = true;
        }

        GUILayout.Space(2f);

        bool hoverTxtOvNew;
        Action revertHoverTxt = () => {
            def.hoverText.color = def.text.color; def.hoverText.fontSize = def.text.fontSize;
            def.hoverText.fontStyle = def.text.fontStyle; def.Invalidate();
            changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint();
        };
        bool txtExp = InspectorSubheaderWithOverrideCopyPaste("Text", def.hoverTextOverride, out hoverTxtOvNew,
            () => _clipBtnHoverText = (def.hoverText.color, def.hoverText.fontSize, def.hoverText.fontStyle),
            () => { if (_clipBtnHoverText.HasValue) { var c = _clipBtnHoverText.Value; def.hoverText.color = c.color; def.hoverText.fontSize = c.fontSize; def.hoverText.fontStyle = c.fontStyle; def.Invalidate(); changed = true; } },
            _clipBtnHoverText.HasValue,
            def.hoverTextOverride ? revertHoverTxt : null, "btn_h_text");
        if (hoverTxtOvNew != def.hoverTextOverride) { def.hoverTextOverride = hoverTxtOvNew; def.Invalidate(); changed = true; }
        if (txtExp)
        {
            var hoverTxtSource = !string.IsNullOrEmpty(def.hoverTextStyleId) ? (ZUI.ActiveSheet?.FindText(def.hoverTextStyleId)?.text ?? def.hoverText)
                               : def.hoverTextOverride ? def.hoverText
                               : def.text;
            bool hoverTxtLocked = !def.hoverTextOverride && string.IsNullOrEmpty(def.hoverTextStyleId);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(hoverTxtLocked))
            {
                DrawTextRowWithStyleRef(hoverTxtSource, ref def.hoverTextStyleId, out bool refChg);
                if (refChg) { def.Invalidate(); changed = true; }
            }
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }

        return changed;
    }

    bool DrawButtonActiveState(ZUIButtonDef def)
    {
        bool changed = false;
        Action invalidate = () => { def.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };
        var hoverGrad = def.GetHoverGradient();
        string bgParent = def.hoverBgOverride ? "Hover" : "Normal";

        bool activeBgOvNew;
        Action revertActiveBg = () => { PasteGrad(def.active, hoverGrad); def.Invalidate(); changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };
        bool bgExp = InspectorSubheaderWithOverrideCopyPaste("Background", def.activeBgOverride, out activeBgOvNew,
            () => _clipBtnActiveBg = def.active.Clone(),
            () => { if (_clipBtnActiveBg != null) { PasteGrad(def.active, _clipBtnActiveBg); def.Invalidate(); changed = true; } },
            _clipBtnActiveBg != null,
            def.activeBgOverride ? revertActiveBg : null, "btn_a_bg");
        if (activeBgOvNew != def.activeBgOverride) { def.activeBgOverride = activeBgOvNew; def.Invalidate(); changed = true; }
        if (bgExp)
        {
            if (def.activeBgOverride)
            {
                if (DrawGradientField("Fill", def.active, invalidate, hoverGrad, bgParent))
                    { def.Invalidate(); changed = true; }
            }
            else
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawGradientField($"Fill ({bgParent})", hoverGrad, null);
            }
        }

        GUILayout.Space(2f);

        bool activeBdrOvNew;
        Action revertActiveBdr = () => {
            var src = def.GetHoverBorder();
            PasteGrad(def.activeBorder.gradient, src.gradient);
            def.activeBorder.width = src.width;
            changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint();
        };
        bool bdrExp = InspectorSubheaderWithOverrideCopyPaste("Border", def.activeBorderOverride, out activeBdrOvNew,
            () => _clipBtnActiveBorder = (def.activeBorder.gradient.colorA, def.activeBorder.gradient.colorB, def.activeBorder.gradient.isGradient, def.activeBorder.width),
            () => { if (_clipBtnActiveBorder.HasValue) { var c = _clipBtnActiveBorder.Value; def.activeBorder.gradient.colorA = c.c1; def.activeBorder.gradient.colorB = c.c2; def.activeBorder.gradient.isGradient = c.dual; def.activeBorder.gradient.Invalidate(); def.activeBorder.width = c.w; changed = true; } },
            _clipBtnActiveBorder.HasValue,
            def.activeBorderOverride ? revertActiveBdr : null, "btn_a_border");
        if (activeBdrOvNew != def.activeBorderOverride) { def.activeBorderOverride = activeBdrOvNew; changed = true; }
        if (bdrExp)
        {
            EditorGUI.BeginChangeCheck();
            if (def.activeBorderOverride)
                DrawActiveBorderRow(def);
            else
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawBorderReadOnlyRow(def.GetHoverBorder());
            }
            if (EditorGUI.EndChangeCheck()) changed = true;
        }

        GUILayout.Space(2f);

        bool activeTxtOvNew;
        Action revertActiveTxt = () => {
            var ht = def.GetHoverText();
            def.activeText.color = ht.color; def.activeText.fontSize = ht.fontSize;
            def.activeText.fontStyle = ht.fontStyle; def.Invalidate();
            changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint();
        };
        bool txtExp = InspectorSubheaderWithOverrideCopyPaste("Text", def.activeTextOverride, out activeTxtOvNew,
            () => _clipBtnActiveText = (def.activeText.color, def.activeText.fontSize, def.activeText.fontStyle),
            () => { if (_clipBtnActiveText.HasValue) { var c = _clipBtnActiveText.Value; def.activeText.color = c.color; def.activeText.fontSize = c.fontSize; def.activeText.fontStyle = c.fontStyle; def.Invalidate(); changed = true; } },
            _clipBtnActiveText.HasValue,
            def.activeTextOverride ? revertActiveTxt : null, "btn_a_text");
        if (activeTxtOvNew != def.activeTextOverride) { def.activeTextOverride = activeTxtOvNew; def.Invalidate(); changed = true; }
        if (txtExp)
        {
            var activeTxtSource = !string.IsNullOrEmpty(def.activeTextStyleId) ? (ZUI.ActiveSheet?.FindText(def.activeTextStyleId)?.text ?? def.activeText)
                                : def.activeTextOverride ? def.activeText
                                : def.GetHoverText();
            bool activeTxtLocked = !def.activeTextOverride && string.IsNullOrEmpty(def.activeTextStyleId);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(activeTxtLocked))
            {
                DrawTextRowWithStyleRef(activeTxtSource, ref def.activeTextStyleId, out bool refChg);
                if (refChg) { def.Invalidate(); changed = true; }
            }
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }

        return changed;
    }

    void DrawHoverBorderRow(ZUIButtonDef def)
    {
        DrawBorderColorAndWidth(def.hoverBorder,
            () => { EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); });
    }

    void DrawActiveBorderRow(ZUIButtonDef def)
    {
        DrawBorderColorAndWidth(def.activeBorder,
            () => { EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); });
    }

    void DrawBorderColorAndWidth(ZUIBorderDef bDef, Action onChange)
    {
        var g = bDef.gradient;
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Border", GUILayout.Width(k_LabelWidth - 2f));

        EditorGUI.BeginChangeCheck();
        g.isGradient = GUILayout.Toggle(g.isGradient, g.isGradient ? "▾" : "▸",
            EditorStyles.miniButton, GUILayout.Width(20f));
        if (EditorGUI.EndChangeCheck()) { g.Invalidate(); onChange?.Invoke(); }

        if (ZUIColorPickerInline(ref g.colorA, ref g.colorARef, ref g.colorASlot)) { g.Invalidate(); onChange?.Invoke(); }
        if (g.isGradient)
            if (ZUIColorPickerInline(ref g.colorB, ref g.colorBRef, ref g.colorBSlot)) { g.Invalidate(); onChange?.Invoke(); }

        float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
        bDef.width = Mathf.Max(0f, EditorGUILayout.FloatField("W", bDef.width, GUILayout.Width(46f)));
        EditorGUIUtility.labelWidth = _lw;
        GUILayout.EndHorizontal();
    }

    static void DrawBorderReadOnlyRow(ZUIBorderDef bDef)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color A", GUILayout.Width(k_LabelWidth - 2f));
        bool dual = bDef.gradient.isGradient;
        GUILayout.Toggle(dual, dual ? "▾" : "▸", EditorStyles.miniButton, GUILayout.Width(20f));
        EditorGUILayout.ColorField(GUIContent.none, bDef.gradient.GetColorA(), true, true, false);
        if (dual) EditorGUILayout.ColorField(GUIContent.none, bDef.gradient.GetColorB(), true, true, false);
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
          EditorGUILayout.FloatField("W", bDef.width, GUILayout.Width(50f));
          EditorGUIUtility.labelWidth = _lw; }
        GUILayout.EndHorizontal();
    }

    // ── Box inspector ─────────────────────────────────────────────────────────

    void DrawBoxInspector()
    {
        if (_selectedBox < 0 || _selectedBox >= _sheet.boxes.Count)
        { CenteredLabel("Select a box style."); return; }

        var def = _sheet.boxes[_selectedBox];
        bool changed = false;
        EditorGUIUtility.labelWidth = k_LabelWidth;

        InspectorHeader("Box Style");

        GUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        def.name = EditorGUILayout.TextField("Name", def.name);
        if (EditorGUI.EndChangeCheck()) { ZUIMissingStyleRegistry.Remove(ZUIMissingStyleRegistry.EntryType.Box, def.name); changed = true; }
        if (GUILayout.Button("Flash", EditorStyles.miniButton, GUILayout.Width(44f))) ZUI.StartFlash(def.name, ZUI.FlashDefType.Box);
        if (GUILayout.Button("Copy",  EditorStyles.miniButton, GUILayout.Width(44f))) _clipBox = CopyBoxDef(def);
        GUI.enabled = _clipBox != null;
        if (GUILayout.Button("Paste", EditorStyles.miniButton, GUILayout.Width(44f)))
            { PasteBoxDef(def, _clipBox); def.Invalidate(); changed = true; }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        if (InspectorSubheader("Preview"))
            DrawBoxPreview(def);
        GUILayout.Space(6f);

        // ── Background ───────────────────────────────────────────────────────
        bool boxBgGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Background",
            () => _clipBoxBackground = def.background.Clone(),
            () => { if (_clipBoxBackground != null) { PasteGrad(def.background, _clipBoxBackground); def.Invalidate(); changed = true; } },
            _clipBoxBackground != null, def.useGlobalBackground, out boxBgGlobalNew))
        {
            var bgSource = def.useGlobalBackground ? (ZUI.ActiveSheet?.globalBox?.background ?? def.background) : def.background;
            Action bgChanged = def.useGlobalBackground ? null : () => { def.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };
            using (new EditorGUI.DisabledGroupScope(def.useGlobalBackground))
            {
                if (DrawGradientField("Fill", bgSource, bgChanged)) { def.Invalidate(); changed = true; }
            }
        }
        if (boxBgGlobalNew != def.useGlobalBackground) { def.useGlobalBackground = boxBgGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        // ── Border ───────────────────────────────────────────────────────────
        bool boxBdrGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Border",
            () => _clipBoxBorder = (def.border.gradient.colorA, def.border.gradient.colorB, def.border.gradient.isGradient, def.border.width, def.useGlobalBorder),
            () => { if (_clipBoxBorder.HasValue) {
                        var c = _clipBoxBorder.Value;
                        def.border.gradient.colorA = c.c1; def.border.gradient.colorB = c.c2;
                        def.border.gradient.isGradient = c.dual; def.border.gradient.Invalidate();
                        def.border.width = c.w;
                        def.useGlobalBorder = c.useGlobal; changed = true; } },
            _clipBoxBorder.HasValue, def.useGlobalBorder, out boxBdrGlobalNew))
        {
            EditorGUI.BeginChangeCheck();
            if (def.useGlobalBorder)
            {
                var gb = ZUI.ActiveSheet?.globalBox;
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawBorderField(gb ?? def, null);
            }
            else
                DrawBorderField(def, () => { EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); });
            if (EditorGUI.EndChangeCheck()) changed = true;
        }
        if (boxBdrGlobalNew != def.useGlobalBorder) { def.useGlobalBorder = boxBdrGlobalNew; changed = true; }

        GUILayout.Space(2f);

        // ── Title Text ───────────────────────────────────────────────────────
        bool boxTitleGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Title Text",
            () => _clipBoxLabel = (def.titleText.color, def.titleText.fontSize, def.titleText.fontStyle),
            () => { if (_clipBoxLabel.HasValue) {
                        var c = _clipBoxLabel.Value;
                        def.titleText.color = c.color; def.titleText.fontSize = c.fontSize;
                        def.titleText.fontStyle = c.fontStyle; changed = true; } },
            _clipBoxLabel.HasValue, def.useGlobalTitleText, out boxTitleGlobalNew))
        {
            var  titleSource = def.useGlobalTitleText         ? (ZUI.ActiveSheet?.globalBox?.titleText ?? def.titleText)
                             : !string.IsNullOrEmpty(def.titleTextStyleId) ? (ZUI.ActiveSheet?.FindText(def.titleTextStyleId)?.text ?? def.titleText)
                             : def.titleText;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(def.useGlobalTitleText))
            {
                DrawTextRowWithStyleRef(titleSource, ref def.titleTextStyleId, out bool refChg);
                if (refChg) { def.Invalidate(); changed = true; }
            }
            bool titleLocked = def.useGlobalTitleText || !string.IsNullOrEmpty(def.titleTextStyleId);
            using (new EditorGUI.DisabledGroupScope(titleLocked))
                DrawShadowTextRow(titleSource);
            if (EditorGUI.EndChangeCheck()) changed = true;
        }
        if (boxTitleGlobalNew != def.useGlobalTitleText) { def.useGlobalTitleText = boxTitleGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        // ── Content Text ─────────────────────────────────────────────────────
        bool boxContentGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Content Text",
            () => _clipBoxContentText = (def.contentText.color, def.contentText.fontSize, def.contentText.fontStyle),
            () => { if (_clipBoxContentText.HasValue) {
                        var c = _clipBoxContentText.Value;
                        def.contentText.color = c.color; def.contentText.fontSize = c.fontSize;
                        def.contentText.fontStyle = c.fontStyle; changed = true; } },
            _clipBoxContentText.HasValue, def.useGlobalContentText, out boxContentGlobalNew))
        {
            var  contentSource = def.useGlobalContentText         ? (ZUI.ActiveSheet?.globalBox?.contentText ?? def.contentText)
                               : !string.IsNullOrEmpty(def.contentTextStyleId) ? (ZUI.ActiveSheet?.FindText(def.contentTextStyleId)?.text ?? def.contentText)
                               : def.contentText;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(def.useGlobalContentText))
            {
                DrawTextRowWithStyleRef(contentSource, ref def.contentTextStyleId, out bool refChg);
                if (refChg) { def.Invalidate(); changed = true; }
            }
            bool contentLocked = def.useGlobalContentText || !string.IsNullOrEmpty(def.contentTextStyleId);
            using (new EditorGUI.DisabledGroupScope(contentLocked))
                DrawShadowTextRow(contentSource);
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }
        if (boxContentGlobalNew != def.useGlobalContentText) { def.useGlobalContentText = boxContentGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        // ── Background Shadow ─────────────────────────────────────────────────
        if (InspectorSubheaderWithToggle("Background Shadow", "box_shadow", ref def.bgShadow.enabled, out bool boxShadowToggleChanged))
        {
            if (def.bgShadow.enabled)
            {
                EditorGUI.BeginChangeCheck();
                DrawBgShadowFields(def.bgShadow.color, def.bgShadow.offset, ref def.bgShadow.colorRef, ref def.bgShadow.colorSlot,
                    out Color newColor, out Vector2 newOffset);
                if (EditorGUI.EndChangeCheck())
                {
                    def.bgShadow.color   = newColor;
                    def.bgShadow.offset  = newOffset;
                    def.Invalidate(); changed = true;
                }
            }
        }
        if (boxShadowToggleChanged) { def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        // ── Padding ──────────────────────────────────────────────────────────
        bool boxPadGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Padding",
            () => _clipBoxPadding = (def.padH, def.padV, def.useGlobalPadding),
            () => { if (_clipBoxPadding.HasValue) {
                        def.padH = _clipBoxPadding.Value.h; def.padV = _clipBoxPadding.Value.v;
                        def.useGlobalPadding = _clipBoxPadding.Value.useGlobal;
                        def.Invalidate(); changed = true; } },
            _clipBoxPadding.HasValue, def.useGlobalPadding, out boxPadGlobalNew))
        {
            EditorGUI.BeginChangeCheck();
            // Padding + Margin on one row
            GUILayout.BeginHorizontal();
            {
                var gp = def.useGlobalPadding ? ZUI.ActiveSheet?.globalBox : null;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalPadding))
                {
                    float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
                    EditorGUILayout.LabelField("Pad", GUILayout.Width(24f));
                    int newH = Mathf.Max(0, EditorGUILayout.IntField("H", gp != null ? gp.padH : def.padH, GUILayout.Width(42f)));
                    int newV = Mathf.Max(0, EditorGUILayout.IntField("V", gp != null ? gp.padV : def.padV, GUILayout.Width(42f)));
                    if (!def.useGlobalPadding) { def.padH = newH; def.padV = newV; }
                    EditorGUIUtility.labelWidth = _lw;
                }
                GUILayout.Space(8f);
                float _lw2 = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
                EditorGUILayout.LabelField("Margin", GUILayout.Width(40f));
                def.marginH = Mathf.Max(0, EditorGUILayout.IntField("H", def.marginH, GUILayout.Width(42f)));
                def.marginV = Mathf.Max(0, EditorGUILayout.IntField("V", def.marginV, GUILayout.Width(42f)));
                EditorGUIUtility.labelWidth = _lw2;
            }
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }
        if (boxPadGlobalNew != def.useGlobalPadding) { def.useGlobalPadding = boxPadGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        // ── Shape ────────────────────────────────────────────────────────────
        bool boxShapeGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Shape",
            () => _clipBoxShape = (def.shape.cornerRadius, def.useGlobalShape),
            () => { if (_clipBoxShape.HasValue)
                    { def.shape.cornerRadius = _clipBoxShape.Value.r;
                      def.useGlobalShape     = _clipBoxShape.Value.useGlobal;
                      changed = true; } },
            _clipBoxShape.HasValue, def.useGlobalShape, out boxShapeGlobalNew))
        {
            EditorGUI.BeginChangeCheck();
            {
                var gs = def.useGlobalShape ? ZUI.ActiveSheet?.globalBox : null;
                int dispR = gs != null ? gs.shape.cornerRadius : def.shape.cornerRadius;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalShape))
                {
                    int newR = EditorGUILayout.IntSlider("Corner Radius", dispR, 0, 24);
                    if (!def.useGlobalShape) def.shape.cornerRadius = newR;

                    if (dispR > 0 && !def.useGlobalShape)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Round corners", GUILayout.Width(k_LabelWidth));
                        def.shape.roundTL = EditorGUILayout.ToggleLeft("TL", def.shape.roundTL, GUILayout.Width(34f));
                        def.shape.roundTR = EditorGUILayout.ToggleLeft("TR", def.shape.roundTR, GUILayout.Width(34f));
                        def.shape.roundBL = EditorGUILayout.ToggleLeft("BL", def.shape.roundBL, GUILayout.Width(34f));
                        def.shape.roundBR = EditorGUILayout.ToggleLeft("BR", def.shape.roundBR, GUILayout.Width(34f));
                        GUILayout.EndHorizontal();
                    }
                }
            }
            if (EditorGUI.EndChangeCheck()) changed = true;
        }
        if (boxShapeGlobalNew != def.useGlobalShape) { def.useGlobalShape = boxShapeGlobalNew; changed = true; }

        if (changed) { EditorUtility.SetDirty(_sheet); RepaintShowcase(); }

        GUILayout.Space(4f);
        if (InspectorSubheader("Title Icon", "box_title_icon"))
        {
            EditorGUI.BeginChangeCheck();
            def.titleIcon = (Texture2D)EditorGUILayout.ObjectField("Texture", def.titleIcon, typeof(Texture2D), false);
            if (def.titleIcon != null)
                def.titleIconSize = EditorGUILayout.IntSlider("Icon Size", def.titleIconSize, 8, 32);
            if (EditorGUI.EndChangeCheck()) { changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }
        }

        GUILayout.Space(10f);
        DrawExportPathField();
    }

    // ── Text style inspector ──────────────────────────────────────────────────

    void DrawTextStyleInspector()
    {
        if (_selectedText < 0 || _selectedText >= _sheet.textStyles.Count)
        { CenteredLabel("Select a text style."); return; }

        var def = _sheet.textStyles[_selectedText];
        bool changed = false;
        EditorGUIUtility.labelWidth = k_LabelWidth;

        InspectorHeader("Text Style");

        EditorGUI.BeginChangeCheck();
        def.name = EditorGUILayout.TextField("Name", def.name);
        if (EditorGUI.EndChangeCheck()) { ZUIMissingStyleRegistry.Remove(ZUIMissingStyleRegistry.EntryType.Text, def.name); changed = true; }

        GUILayout.Space(4f);

        if (InspectorSubheader("Text", $"ts_{_selectedText}_text"))
        {
            EditorGUI.BeginChangeCheck();
            DrawTextRow(def.text);
            DrawShadowTextRow(def.text);
            if (EditorGUI.EndChangeCheck() || GUI.changed) { def.Invalidate(); changed = true; }
        }

        if (changed) { EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); }

        GUILayout.Space(10f);

        if (InspectorSubheader("Preview", $"ts_{_selectedText}_preview"))
        {
            // Text + Background + style picker on one row
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Text", GUILayout.Width(28f));
            _previewTextContent = EditorGUILayout.TextField(_previewTextContent, GUILayout.MaxWidth(120f));
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Bg", GUILayout.Width(18f));
            _textPreviewBgMode = GUILayout.Toolbar(_textPreviewBgMode,
                new[] { "None", "Box", "Btn" }, EditorStyles.miniButton, GUILayout.MaxWidth(100f));
            if (_textPreviewBgMode == 1 && _sheet.boxes.Count > 0)
            {
                _textPreviewBoxIndex = Mathf.Clamp(_textPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
                var names = new string[_sheet.boxes.Count];
                for (int i = 0; i < _sheet.boxes.Count; i++) names[i] = _sheet.boxes[i].name;
                _textPreviewBoxIndex = EditorGUILayout.Popup(_textPreviewBoxIndex, names, GUILayout.MaxWidth(100f));
            }
            else if (_textPreviewBgMode == 2 && _sheet.buttons.Count > 0)
            {
                _textPreviewButtonIndex = Mathf.Clamp(_textPreviewButtonIndex, 0, _sheet.buttons.Count - 1);
                var names = new string[_sheet.buttons.Count];
                for (int i = 0; i < _sheet.buttons.Count; i++) names[i] = _sheet.buttons[i].name;
                _textPreviewButtonIndex = EditorGUILayout.Popup(_textPreviewButtonIndex, names, GUILayout.MaxWidth(100f));
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);

            if (_textPreviewBgMode == 1 && _sheet.boxes.Count > 0)
            {
                _textPreviewBoxIndex = Mathf.Clamp(_textPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
                using (ZUI.Box(null, _sheet.boxes[_textPreviewBoxIndex]))
                    ZUI.Label(_previewTextContent, def);
            }
            else if (_textPreviewBgMode == 2 && _sheet.buttons.Count > 0)
            {
                _textPreviewButtonIndex = Mathf.Clamp(_textPreviewButtonIndex, 0, _sheet.buttons.Count - 1);
                var btnDef    = _sheet.buttons[_textPreviewButtonIndex];
                var textStyle = def.GetStyle();
                var content   = new GUIContent(_previewTextContent);

                // Resolve button padding so the visual is sized the same way a real button would be.
                int pH = btnDef.padH, pV = btnDef.padV;
                if (btnDef.useGlobalPadding) { var g = ZUI.ActiveSheet?.globalButton; if (g != null) { pH = g.padH; pV = g.padV; } }

                var bgRect = GUILayoutUtility.GetRect(1f, 46f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(bgRect, new Color(.13f, .13f, .15f, 1f));
                if (Event.current.type == EventType.Repaint)
                {
                    // Button rect = text content size + button padding on each side.
                    var textSize = textStyle.CalcSize(content);
                    var btnSize  = new Vector2(textSize.x + pH * 2f, textSize.y + pV * 2f);
                    var btnRect  = new Rect(
                        bgRect.x + (bgRect.width  - btnSize.x) * 0.5f,
                        bgRect.y + (bgRect.height - btnSize.y) * 0.5f,
                        btnSize.x, btnSize.y);
                    int r = ZUI.SimulateLegacyCorners ? 0 : btnDef.GetResolvedCornerRadius();
                    btnDef.DrawVisual(btnRect, ZUIButtonDrawState.Normal, r);
                    // Text drawn inside the padded inner area using the text style def.
                    var textRect = new Rect(btnRect.x + pH, btnRect.y + pV, textSize.x, textSize.y);
                    ZUI.DrawLabel(textRect, content, textStyle, def.text);
                }
            }
            else
            {
                ZUI.Label(_previewTextContent, def);
            }
        }

        GUILayout.Space(10f);
        DrawExportPathField();
    }

    // ── Global tab ────────────────────────────────────────────────────────────

    void DrawGlobalInspector()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // Subtab bar
        GUILayout.Space(4f);
        _globalSubTab = GUILayout.Toolbar(_globalSubTab, new[] { "Button", "Box", "Layout" }, EditorStyles.miniButton, GUILayout.Height(20f));
        GUILayout.Space(6f);

        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
        EditorGUIUtility.labelWidth = k_LabelWidth;
        bool changed = false;

        switch (_globalSubTab)
        {
            case 0: DrawGlobalButtonSubTab(ref changed); break;
            case 1: DrawGlobalBoxSubTab(ref changed);    break;
            case 2: DrawGlobalLayoutSubTab(ref changed); break;
        }

        if (changed) { EditorUtility.SetDirty(_sheet); RepaintShowcase(); }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    void DrawGlobalButtonSubTab(ref bool changed)
    {
        InspectorHeader("Global Button Defaults");
        EditorGUILayout.LabelField("Button styles with 'Use Global' inherit these values.", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(4f);

        if (InspectorSubheader("Background", "global_btn_bg"))
        {
            if (DrawGradientField("Fill", _sheet.globalButton.normal, () => { _sheet.globalButton.Invalidate(); foreach (var b in _sheet.buttons) if (b.useGlobalBackground) b.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); }))
            { _sheet.globalButton.Invalidate(); foreach (var b in _sheet.buttons) if (b.useGlobalBackground) b.Invalidate(); changed = true; }
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Text", "global_btn_text"))
        {
            EditorGUI.BeginChangeCheck();
            DrawTextRow(_sheet.globalButton.text);
            if (EditorGUI.EndChangeCheck()) { _sheet.globalButton.Invalidate(); foreach (var b in _sheet.buttons) if (b.useGlobalText) b.Invalidate(); changed = true; }
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Shape", "global_btn_shape"))
        {
            EditorGUI.BeginChangeCheck();
            _sheet.globalButton.cornerRadius = EditorGUILayout.IntSlider("Corner Radius", _sheet.globalButton.cornerRadius, 0, 16);
            if (EditorGUI.EndChangeCheck()) { _sheet.globalButton.Invalidate(); changed = true; }
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Border", "global_btn_border"))
        {
            EditorGUI.BeginChangeCheck();
            DrawBorderField(_sheet.globalButton, null);
            if (EditorGUI.EndChangeCheck()) changed = true;
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Padding", "global_btn_size"))
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pad (text)", GUILayout.Width(k_LabelWidth - 2f));
            float _glbLW = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
            _sheet.globalButton.padH = Mathf.Max(0, EditorGUILayout.IntField("H", _sheet.globalButton.padH, GUILayout.Width(46f)));
            _sheet.globalButton.padV = Mathf.Max(0, EditorGUILayout.IntField("V", _sheet.globalButton.padV, GUILayout.Width(46f)));
            EditorGUIUtility.labelWidth = _glbLW;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pad (icon)", GUILayout.Width(k_LabelWidth - 2f));
            float _glbLW2 = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
            _sheet.globalButton.iconPadH = Mathf.Max(0, EditorGUILayout.IntField("H", _sheet.globalButton.iconPadH, GUILayout.Width(46f)));
            _sheet.globalButton.iconPadV = Mathf.Max(0, EditorGUILayout.IntField("V", _sheet.globalButton.iconPadV, GUILayout.Width(46f)));
            EditorGUIUtility.labelWidth = _glbLW2;
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                _sheet.globalButton.Invalidate();
                foreach (var b in _sheet.buttons) if (b.useGlobalPadding) b.Invalidate();
                changed = true;
            }
        }
    }

    void DrawGlobalBoxSubTab(ref bool changed)
    {
        InspectorHeader("Global Box Defaults");
        EditorGUILayout.LabelField("Box styles with 'Use Global' inherit these values.", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(4f);

        if (InspectorSubheader("Background", "global_box_bg"))
        {
            if (DrawGradientField("Fill", _sheet.globalBox.background, () => { _sheet.globalBox.Invalidate(); foreach (var b in _sheet.boxes) if (b.useGlobalBackground) b.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); }))
            { _sheet.globalBox.Invalidate(); foreach (var b in _sheet.boxes) if (b.useGlobalBackground) b.Invalidate(); changed = true; }
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Title Text", "global_box_title"))
        {
            EditorGUI.BeginChangeCheck();
            DrawTextRow(_sheet.globalBox.titleText);
            if (EditorGUI.EndChangeCheck()) { _sheet.globalBox.Invalidate(); foreach (var b in _sheet.boxes) if (b.useGlobalTitleText) b.Invalidate(); changed = true; }
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Content Text", "global_box_content"))
        {
            EditorGUI.BeginChangeCheck();
            DrawTextRow(_sheet.globalBox.contentText);
            if (EditorGUI.EndChangeCheck()) { _sheet.globalBox.Invalidate(); foreach (var b in _sheet.boxes) if (b.useGlobalContentText) b.Invalidate(); changed = true; }
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Shape", "global_box_shape"))
        {
            EditorGUI.BeginChangeCheck();
            _sheet.globalBox.shape.cornerRadius = EditorGUILayout.IntSlider("Corner Radius", _sheet.globalBox.shape.cornerRadius, 0, 24);
            if (EditorGUI.EndChangeCheck()) changed = true;
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Border", "global_box_border"))
        {
            EditorGUI.BeginChangeCheck();
            DrawBorderField(_sheet.globalBox, null);
            if (EditorGUI.EndChangeCheck()) changed = true;
        }

        GUILayout.Space(2f);
        if (InspectorSubheader("Padding", "global_box_padding"))
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("H / V", GUILayout.Width(k_LabelWidth - 2f));
            float _gbLW = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
            _sheet.globalBox.padH = Mathf.Max(0, EditorGUILayout.IntField("H", _sheet.globalBox.padH, GUILayout.Width(46f)));
            _sheet.globalBox.padV = Mathf.Max(0, EditorGUILayout.IntField("V", _sheet.globalBox.padV, GUILayout.Width(46f)));
            EditorGUIUtility.labelWidth = _gbLW;
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                _sheet.globalBox.Invalidate();
                foreach (var b in _sheet.boxes) if (b.useGlobalPadding) b.Invalidate();
                changed = true;
            }
        }
    }

    void DrawGlobalLayoutSubTab(ref bool changed)
    {
        InspectorHeader("Spacing");
        GUILayout.Space(4f);

        // Vertical base
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Vertical", GUILayout.Width(k_LabelWidth));
        EditorGUI.BeginChangeCheck();
        float newVSpacing = EditorGUILayout.Slider(_sheet.verticalSpacing, 0f, 24f);
        if (EditorGUI.EndChangeCheck()) { _sheet.verticalSpacing = newVSpacing; changed = true; }
        if (GUILayout.Button("Flash", EditorStyles.miniButton, GUILayout.Width(40f)))
            ZUI.StartVerticalSpaceFlash();
        GUILayout.EndHorizontal();

        // Horizontal base
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Horizontal", GUILayout.Width(k_LabelWidth));
        EditorGUI.BeginChangeCheck();
        float newHSpacing = EditorGUILayout.Slider(_sheet.horizontalSpacing, 0f, 24f);
        if (EditorGUI.EndChangeCheck()) { _sheet.horizontalSpacing = newHSpacing; changed = true; }
        GUILayout.Space(44f); // align with Flash button width
        GUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            "Base units for ZUI.VerticalSpace() / HorizontalSpace(). " +
            "Pass a float scale (0.5f, 2f) or a named scale (see below).",
            EditorStyles.wordWrappedMiniLabel);

        GUILayout.Space(8f);
        InspectorHeader("Named Scales");
        EditorGUILayout.LabelField(
            "ZUI.VerticalSpace(\"name\") or HorizontalSpace(\"name\") multiplies the base by this scale.",
            EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(4f);

        var scales = _sheet.spacingScales;
        int removeAt = -1;
        for (int i = 0; i < scales.Count; i++)
        {
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            scales[i].name  = EditorGUILayout.TextField(scales[i].name,  GUILayout.Width(110f));
            scales[i].scale = EditorGUILayout.Slider(scales[i].scale, 0f, 4f);
            float resolvedV = _sheet.verticalSpacing   * scales[i].scale;
            float resolvedH = _sheet.horizontalSpacing * scales[i].scale;
            EditorGUILayout.LabelField($"V:{resolvedV:F1}px  H:{resolvedH:F1}px", EditorStyles.miniLabel, GUILayout.Width(90f));
            if (EditorGUI.EndChangeCheck()) changed = true;
            if (GUILayout.Button("−", EditorStyles.miniButton, GUILayout.Width(20f))) removeAt = i;
            GUILayout.EndHorizontal();
        }
        if (removeAt >= 0) { scales.RemoveAt(removeAt); changed = true; }

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Add Scale", EditorStyles.miniButton, GUILayout.Width(80f)))
        {
            scales.Add(new ZUISpacingScale { name = "New Scale", scale = 1f });
            changed = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(12f);

        InspectorHeader("Flash Settings");
        EditorGUILayout.LabelField("Controls speed and duration of all ZUI flash animations.", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(4f);

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Count", GUILayout.Width(k_LabelWidth));
        EditorGUI.BeginChangeCheck();
        int newCount = EditorGUILayout.IntSlider(_sheet.flashCount, 1, 30);
        if (EditorGUI.EndChangeCheck()) { _sheet.flashCount = newCount; changed = true; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Speed (sec/pulse)", GUILayout.Width(k_LabelWidth));
        EditorGUI.BeginChangeCheck();
        float newInterval = EditorGUILayout.Slider(_sheet.flashInterval, 0.02f, 0.5f);
        if (EditorGUI.EndChangeCheck()) { _sheet.flashInterval = newInterval; changed = true; }
        GUILayout.EndHorizontal();

        float totalSec = _sheet.flashCount * _sheet.flashInterval;
        EditorGUILayout.LabelField($"Total duration: {totalSec:F1}s  ({_sheet.flashCount} pulses × {_sheet.flashInterval:F2}s)", EditorStyles.miniLabel);
    }

    // ── Gradient field ────────────────────────────────────────────────────────

    // parentGrad / parentState: when set, adds "Revert to [parentState]" items in the context menu.
    bool DrawGradientField(string label, ZUIGradient g, Action onExternalPaste,
                           ZUIGradient parentGrad = null, string parentState = null, bool hidePxEdge = false)
    {
        bool changed = false;

        var fieldRect = EditorGUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(k_LabelWidth - 2f));

        EditorGUI.BeginChangeCheck();
        g.isGradient = GUILayout.Toggle(g.isGradient, g.isGradient ? "▾" : "▸",
            EditorStyles.miniButton, GUILayout.Width(20f));
        if (EditorGUI.EndChangeCheck()) { g.Invalidate(); changed = true; }

        if (ZUIColorPickerInline(ref g.colorA, ref g.colorARef, ref g.colorASlot)) { g.Invalidate(); changed = true; }

        if (g.isGradient)
        {
            if (ZUIColorPickerInline(ref g.colorB, ref g.colorBRef, ref g.colorBSlot)) { g.Invalidate(); changed = true; }
        }

        GUILayout.EndHorizontal();

        if (g.isGradient)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            // Mode radio: 0 = Linear, 1 = Radial, 2 = Fixed (Fixed hidden for borders)
            int mode    = g.isRadial ? 1 : (g.usePixelLength ? 2 : 0);
            int newMode = mode;
            if (!hidePxEdge)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Mode", GUILayout.Width(k_LabelWidth - 2f));
                newMode = GUILayout.Toolbar(mode, new[] { "Linear", "Radial", "Fixed" }, EditorStyles.miniButton);
                if (newMode != mode)
                {
                    g.isRadial       = newMode == 1;
                    g.usePixelLength = newMode == 2;
                }
                // Edges on same row as mode radio, visible only in Fixed mode
                if (newMode == 2)
                {
                    GUILayout.Space(4f);
                    foreach (var (lbl, edge, tip) in k_Edges)
                    {
                        bool active = (g.pixelEdges & edge) != 0;
                        bool next   = GUILayout.Toggle(active, new GUIContent(lbl, tip),
                                          EditorStyles.miniButton, GUILayout.Width(22f));
                        if (next != active) g.pixelEdges = next ? (g.pixelEdges | edge) : (g.pixelEdges & ~edge);
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                // Border mode: always linear (no radial/fixed); reset if was otherwise
                if (g.isRadial || g.usePixelLength) { g.isRadial = false; g.usePixelLength = false; }
                newMode = 0;
            }

            // Per-mode controls on a compact second row
            GUILayout.BeginHorizontal();
            if (newMode == 0) // Linear: Angle + Curve
            {
                EditorGUILayout.LabelField("Angle", GUILayout.Width(k_LabelWidth - 2f));
                g.angle = EditorGUILayout.Slider(GUIContent.none, g.angle, 0f, 360f);
                EditorGUILayout.LabelField("Curve", GUILayout.Width(44f));
                g.bias  = EditorGUILayout.Slider(GUIContent.none, g.bias, 0f, 1f);
            }
            else if (newMode == 1) // Radial: Curve only
            {
                EditorGUILayout.LabelField("Curve", GUILayout.Width(k_LabelWidth - 2f));
                g.bias = EditorGUILayout.Slider(GUIContent.none, g.bias, 0f, 1f);
            }
            else // Fixed: Length + Curve
            {
                float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = k_LabelWidth - 2f;
                g.pixelLength = Mathf.Max(1, EditorGUILayout.IntField("Length", g.pixelLength, GUILayout.Width(k_LabelWidth + 48f)));
                EditorGUIUtility.labelWidth = _lw;
                EditorGUILayout.LabelField("Curve", GUILayout.Width(44f));
                g.bias = EditorGUILayout.Slider(GUIContent.none, g.bias, 0f, 1f);
            }
            GUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck()) { g.Invalidate(); changed = true; }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        // Right-click context menu
        if (Event.current.type == EventType.ContextClick && fieldRect.Contains(Event.current.mousePosition))
        {
            var capturedG = g;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Gradient"), false, () => _clipGradient = capturedG.Clone());
            if (_clipGradient != null)
                menu.AddItem(new GUIContent("Paste Gradient"), false, () =>
                {
                    PasteGrad(capturedG, _clipGradient);
                    onExternalPaste?.Invoke();
                    Repaint();
                });
            else
                menu.AddDisabledItem(new GUIContent("Paste Gradient"));

            if (parentGrad != null)
            {
                string n = parentState ?? "Parent";
                menu.AddSeparator("");
                menu.AddItem(new GUIContent($"Revert All to {n}"),    false, () => { PasteGrad(capturedG, parentGrad); onExternalPaste?.Invoke(); Repaint(); });
                menu.AddItem(new GUIContent($"Revert Color A to {n}"), false, () => { capturedG.colorA = parentGrad.colorA; capturedG.Invalidate(); onExternalPaste?.Invoke(); Repaint(); });
                if (capturedG.isGradient)
                    menu.AddItem(new GUIContent($"Revert Color B to {n}"), false, () => { capturedG.colorB = parentGrad.colorB; capturedG.Invalidate(); onExternalPaste?.Invoke(); Repaint(); });
                menu.AddItem(new GUIContent($"Revert Curve to {n}"),   false, () => { capturedG.bias = parentGrad.bias; capturedG.Invalidate(); onExternalPaste?.Invoke(); Repaint(); });
                menu.AddItem(new GUIContent($"Revert Mode to {n}"),    false, () =>
                {
                    capturedG.isGradient = parentGrad.isGradient; capturedG.isRadial = parentGrad.isRadial;
                    capturedG.usePixelLength = parentGrad.usePixelLength; capturedG.pixelEdges = parentGrad.pixelEdges;
                    capturedG.Invalidate(); onExternalPaste?.Invoke(); Repaint();
                });
            }

            menu.ShowAsContext();
            Event.current.Use();
        }

        return changed;
    }

    // ── Direction picker (pixel mode only) ───────────────────────────────────
    // Each button toggles an edge independently — multiple edges can be active at once.
    // colorA appears at the edge; colorB fills the rest after pixelLength pixels.

    static readonly (string label, ZUIPixelEdges edge, string tooltip)[] k_Edges =
    {
        ("←", ZUIPixelEdges.Left,   "Left edge  — colorA on left"),
        ("→", ZUIPixelEdges.Right,  "Right edge  — colorA on right"),
        ("↑", ZUIPixelEdges.Bottom, "Bottom edge — colorA at bottom"),
        ("↓", ZUIPixelEdges.Top,    "Top edge    — colorA at top"),
    };

    static void DrawDirectionPicker(ZUIGradient g)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Edges", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
        foreach (var (lbl, edge, tip) in k_Edges)
        {
            bool active = (g.pixelEdges & edge) != 0;
            bool next   = GUILayout.Toggle(active, new GUIContent(lbl, tip),
                              EditorStyles.miniButton, GUILayout.Width(28f));
            if (next != active)
                g.pixelEdges = next ? (g.pixelEdges | edge) : (g.pixelEdges & ~edge);
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    // ── Text row (compact single-line) ───────────────────────────────────────

    void DrawTextRow(ZUITextDef text)
    {
        float prevLW = EditorGUIUtility.labelWidth;
        // Color (with optional gradient toggle) + Size + Style on one row
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Text", GUILayout.Width(32f));
        ZUIColorPickerInline(ref text.color, ref text.colorRef, ref text.colorSlot);
        // Gradient toggle — switches color from flat to A→B gradient
        text.gradientEnabled = GUILayout.Toggle(text.gradientEnabled, text.gradientEnabled ? "\u2192" : "\u2022",
            EditorStyles.miniButton, GUILayout.Width(20f));
        if (text.gradientEnabled)
            ZUIColorPickerInline(ref text.colorB, ref text.colorBRef, ref text.colorBSlot);
        EditorGUIUtility.labelWidth = 28f;
        text.fontSize  = Mathf.Max(0, EditorGUILayout.IntField("Sz", text.fontSize, GUILayout.Width(56f)));
        EditorGUIUtility.labelWidth = prevLW;
        text.fontStyle = (FontStyle)EditorGUILayout.EnumPopup(GUIContent.none, text.fontStyle, GUILayout.Width(70f));
        GUILayout.EndHorizontal();
    }

    // Text row with an inline style ref popup at the start (replaces the "Text" label with a dropdown).
    void DrawTextRowWithStyleRef(ZUITextDef text, ref string styleId, out bool refChanged)
    {
        refChanged = false;
        float prevLW = EditorGUIUtility.labelWidth;
        GUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        string newRef = DrawTextStyleRefPopupInline(styleId);
        if (EditorGUI.EndChangeCheck()) { styleId = newRef; refChanged = true; }

        bool hasRef = !string.IsNullOrEmpty(styleId);
        using (new EditorGUI.DisabledGroupScope(hasRef))
        {
            ZUIColorPickerInline(ref text.color, ref text.colorRef, ref text.colorSlot);
            text.gradientEnabled = GUILayout.Toggle(text.gradientEnabled, text.gradientEnabled ? "\u2192" : "\u2022",
                EditorStyles.miniButton, GUILayout.Width(20f));
            if (text.gradientEnabled)
                ZUIColorPickerInline(ref text.colorB, ref text.colorBRef, ref text.colorBSlot);
            EditorGUIUtility.labelWidth = 28f;
            text.fontSize  = Mathf.Max(0, EditorGUILayout.IntField("Sz", text.fontSize, GUILayout.Width(56f)));
            EditorGUIUtility.labelWidth = prevLW;
            text.fontStyle = (FontStyle)EditorGUILayout.EnumPopup(GUIContent.none, text.fontStyle, GUILayout.Width(70f));
        }
        GUILayout.EndHorizontal();
    }

    void DrawShadowTextRow(ZUITextDef text)
    {
        // Shadow toggle + color + X/Y all on one row
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Shadow", GUILayout.Width(48f));
        text.shadowEnabled = EditorGUILayout.Toggle(text.shadowEnabled, GUILayout.Width(16f));
        if (text.shadowEnabled)
        {
            ZUIColorPickerInline(ref text.shadowColor, ref text.shadowColorRef, ref text.shadowColorSlot);
            float lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
            text.shadowOffset.x = EditorGUILayout.FloatField("X", text.shadowOffset.x, GUILayout.Width(48f));
            text.shadowOffset.y = EditorGUILayout.FloatField("Y", text.shadowOffset.y, GUILayout.Width(48f));
            EditorGUIUtility.labelWidth = lw;
        }
        GUILayout.EndHorizontal();

        // Outline toggle + color + width + pass count
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Outline", GUILayout.Width(48f));
        text.outlineEnabled = EditorGUILayout.Toggle(text.outlineEnabled, GUILayout.Width(16f));
        if (text.outlineEnabled)
        {
            ZUIColorPickerInline(ref text.outlineColor, ref text.outlineColorRef, ref text.outlineColorSlot);
            float lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
            text.outlineWidth = EditorGUILayout.IntField("W", text.outlineWidth, GUILayout.Width(40f));
            text.outlineWidth = Mathf.Clamp(text.outlineWidth, 1, 3);
            EditorGUIUtility.labelWidth = lw;
            // Pass toggle: 4 (cardinal) or 8 (cardinal + diagonal)
            text.outlinePasses = GUILayout.Toolbar(text.outlinePasses >= 8 ? 1 : 0,
                new[] { "4", "8" }, EditorStyles.miniButton, GUILayout.Width(44f)) == 1 ? 8 : 4;
        }
        GUILayout.EndHorizontal();
    }

    // Draws a "Text Style" popup — returns the selected style's name, or "" for inline.
    // Call without Begin/EndHorizontal — caller handles horizontal layout.
    string DrawTextStyleRefPopupInline(string currentId)
    {
        var styles = _sheet?.textStyles;
        if (styles == null || styles.Count == 0) return currentId;

        int currentIdx = 0;
        var names = new string[styles.Count + 1];
        names[0] = "— Inline —";
        for (int i = 0; i < styles.Count; i++)
        {
            names[i + 1] = styles[i].name;
            if (styles[i].name == currentId) currentIdx = i + 1;
        }

        int newIdx = EditorGUILayout.Popup(currentIdx, names, GUILayout.Width(90f));
        return newIdx == 0 ? "" : names[newIdx];
    }

    void DrawBgShadowFields(Color color, Vector2 offset, ref string paletteRef, ref ZUIPaletteSlot slot,
                            out Color outColor, out Vector2 outOffset)
    {
        outColor = color; outOffset = offset;
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color", GUILayout.Width(48f));
        ZUIColorPickerInline(ref outColor, ref paletteRef, ref slot);
        float lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
        outOffset.x = EditorGUILayout.FloatField("X", offset.x, GUILayout.Width(48f));
        outOffset.y = EditorGUILayout.FloatField("Y", offset.y, GUILayout.Width(48f));
        EditorGUIUtility.labelWidth = lw;
        GUILayout.EndHorizontal();
    }

    // ── Border field ──────────────────────────────────────────────────────────

    void DrawBorderField(ZUIBoxDef def, Action onExternalPaste)
    {
        var fieldRect = EditorGUILayout.BeginVertical();

        EditorGUI.BeginChangeCheck();
        DrawGradientField("Color", def.border.gradient, onExternalPaste, null, null, hidePxEdge: true);
        if (EditorGUI.EndChangeCheck()) { def.border.gradient.Invalidate(); }

        GUILayout.BeginHorizontal();
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = k_LabelWidth - 2f;
          def.border.width = Mathf.Max(0f, EditorGUILayout.FloatField("Width", def.border.width));
          EditorGUIUtility.labelWidth = _lw; }
        GUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        if (Event.current.type == EventType.ContextClick && fieldRect.Contains(Event.current.mousePosition))
        {
            var capturedDef = def;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Border"), false, () =>
                _clipBoxBorder = (capturedDef.border.gradient.colorA, capturedDef.border.gradient.colorB,
                                  capturedDef.border.gradient.isGradient, capturedDef.border.width, capturedDef.useGlobalBorder));
            if (_clipBoxBorder.HasValue)
                menu.AddItem(new GUIContent("Paste Border"), false, () =>
                {
                    var c = _clipBoxBorder.Value;
                    capturedDef.border.gradient.colorA     = c.c1;
                    capturedDef.border.gradient.colorB     = c.c2;
                    capturedDef.border.gradient.isGradient = c.dual;
                    capturedDef.border.width               = c.w;
                    capturedDef.border.gradient.Invalidate();
                    onExternalPaste?.Invoke();
                    Repaint();
                });
            else
                menu.AddDisabledItem(new GUIContent("Paste Border"));
            menu.ShowAsContext();
            Event.current.Use();
        }
    }

    void DrawBorderField(ZUIButtonDef def, Action onExternalPaste)
    {
        var fieldRect = EditorGUILayout.BeginVertical();

        EditorGUI.BeginChangeCheck();
        DrawGradientField("Color", def.border.gradient, onExternalPaste, null, null, hidePxEdge: true);
        if (EditorGUI.EndChangeCheck()) { def.border.gradient.Invalidate(); }

        GUILayout.BeginHorizontal();
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = k_LabelWidth - 2f;
          def.border.width = Mathf.Max(0f, EditorGUILayout.FloatField("Width", def.border.width));
          EditorGUIUtility.labelWidth = _lw; }
        GUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        if (Event.current.type == EventType.ContextClick && fieldRect.Contains(Event.current.mousePosition))
        {
            var capturedDef = def;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Border"), false, () =>
                _clipBtnBorder = (capturedDef.border.gradient.colorA, capturedDef.border.gradient.colorB,
                                  capturedDef.border.gradient.isGradient, capturedDef.border.width, capturedDef.useGlobalBorder));
            if (_clipBtnBorder.HasValue)
                menu.AddItem(new GUIContent("Paste Border"), false, () =>
                {
                    var c = _clipBtnBorder.Value;
                    capturedDef.border.gradient.colorA     = c.c1;
                    capturedDef.border.gradient.colorB     = c.c2;
                    capturedDef.border.gradient.isGradient = c.dual;
                    capturedDef.border.width               = c.w;
                    capturedDef.border.gradient.Invalidate();
                    onExternalPaste?.Invoke();
                    Repaint();
                });
            else
                menu.AddDisabledItem(new GUIContent("Paste Border"));
            menu.ShowAsContext();
            Event.current.Use();
        }
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    void DrawPreviewHeader()
    {
        GUILayout.Space(2f);
        var rect = GUILayoutUtility.GetRect(1f, 16f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        EditorGUI.LabelField(new Rect(rect.x + 6f, rect.y, rect.width - 110f, rect.height),
            "Preview", EditorStyles.miniLabel);

        const float tw = 100f;
        var toggleRect = new Rect(rect.xMax - tw - 4f, rect.y + 1f, tw, 14f);
        EditorGUI.BeginChangeCheck();
        _simulateLegacy = GUI.Toggle(toggleRect, _simulateLegacy, "Simulate No Rounding", EditorStyles.miniButton);
        if (EditorGUI.EndChangeCheck())
        {
            ZUI.SimulateLegacyCorners = _simulateLegacy;
            RepaintShowcase();
        }

        GUILayout.Space(4f);
    }

    void DrawButtonPreview(ZUIButtonDef def)
    {
        // Label + Background on one row
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Label", GUILayout.Width(34f));
        _previewButtonText = EditorGUILayout.TextField(_previewButtonText, GUILayout.MaxWidth(120f));
        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Bg", GUILayout.Width(18f));
        _buttonPreviewBgMode = GUILayout.Toolbar(_buttonPreviewBgMode,
            new[] { "None", "Box" }, EditorStyles.miniButton, GUILayout.MaxWidth(90f));
        if (_buttonPreviewBgMode == 1 && _sheet.boxes.Count > 0)
        {
            _buttonPreviewBoxIndex = Mathf.Clamp(_buttonPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
            var names = new string[_sheet.boxes.Count];
            for (int i = 0; i < _sheet.boxes.Count; i++) names[i] = _sheet.boxes[i].name;
            _buttonPreviewBoxIndex = EditorGUILayout.Popup(_buttonPreviewBoxIndex, names, GUILayout.MaxWidth(100f));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);

        var content    = new GUIContent(_previewButtonText);
        var labelStyle = def.GetLabelStyle();
        var btnSize    = labelStyle.CalcSize(content);

        bool useBox = _buttonPreviewBgMode == 1 && _sheet.boxes.Count > 0;
        ZUIBoxDef boxDef = useBox ? _sheet.boxes[_buttonPreviewBoxIndex] : null;

        if (useBox)
        {
            using (ZUI.Box(null, boxDef))
            {
                DrawButtonPreviewInner(def, content, btnSize, hasBoxBg: true);
            }
        }
        else
        {
            DrawButtonPreviewInner(def, content, btnSize, hasBoxBg: false);
        }
    }

    void DrawButtonPreviewInner(ZUIButtonDef def, GUIContent content, UnityEngine.Vector2 btnSize, bool hasBoxBg = false)
    {
        if (_buttonStateTab == 0)
        {
            var bgRect = GUILayoutUtility.GetRect(1f, 46f, GUILayout.ExpandWidth(true));
            if (!hasBoxBg) EditorGUI.DrawRect(bgRect, new Color(.13f, .13f, .15f, 1f));
            EditorGUI.LabelField(new Rect(bgRect.x + 8f, bgRect.y + 2f, 200f, 14f),
                "Hover and click to test", EditorStyles.miniLabel);
            var btnRect = new Rect(bgRect.x + (bgRect.width  - btnSize.x) * 0.5f,
                                   bgRect.y + (bgRect.height - btnSize.y) * 0.5f + 4f,
                                   btnSize.x, btnSize.y);
            ZUI.Button(btnRect, content, def);
        }
        else
        {
            var forcedState = _buttonStateTab == 1 ? ZUIButtonDrawState.Hover : ZUIButtonDrawState.Active;
            var bgRect = GUILayoutUtility.GetRect(1f, 46f, GUILayout.ExpandWidth(true));
            if (!hasBoxBg) EditorGUI.DrawRect(bgRect, new Color(.13f, .13f, .15f, 1f));
            var stateLabel = _buttonStateTab == 1 ? "Forced Hover state" : "Forced Active state";
            EditorGUI.LabelField(new Rect(bgRect.x + 8f, bgRect.y + 2f, 200f, 14f),
                stateLabel, EditorStyles.miniLabel);

            if (Event.current.type == EventType.Repaint)
            {
                var btnRect = new Rect(bgRect.x + (bgRect.width  - btnSize.x) * 0.5f,
                                       bgRect.y + (bgRect.height - btnSize.y) * 0.5f + 4f,
                                       btnSize.x, btnSize.y);
                int r = ZUI.SimulateLegacyCorners ? 0 : def.GetResolvedCornerRadius();
                def.DrawVisual(btnRect, forcedState, r);
                ZUI.DrawButtonLabel(btnRect, content, def.GetLabelStyle(forcedState), null, ZIconPlacement.LeftOfLabel, def);
            }
        }
    }

    void DrawTogglePreview(ZUIButtonDef def)
    {
        // Label + Background on one row
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Label", GUILayout.Width(34f));
        _previewButtonText = EditorGUILayout.TextField(_previewButtonText, GUILayout.MaxWidth(120f));
        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Bg", GUILayout.Width(18f));
        _buttonPreviewBgMode = GUILayout.Toolbar(_buttonPreviewBgMode,
            new[] { "None", "Box" }, EditorStyles.miniButton, GUILayout.MaxWidth(90f));
        if (_buttonPreviewBgMode == 1 && _sheet.boxes.Count > 0)
        {
            _buttonPreviewBoxIndex = Mathf.Clamp(_buttonPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
            var names = new string[_sheet.boxes.Count];
            for (int i = 0; i < _sheet.boxes.Count; i++) names[i] = _sheet.boxes[i].name;
            _buttonPreviewBoxIndex = EditorGUILayout.Popup(_buttonPreviewBoxIndex, names, GUILayout.MaxWidth(100f));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);

        bool useBox = _buttonPreviewBgMode == 1 && _sheet.boxes.Count > 0;
        if (useBox)
        {
            using (ZUI.Box(null, _sheet.boxes[_buttonPreviewBoxIndex]))
                DrawTogglePreviewInner(def, hasBoxBg: true);
        }
        else
        {
            DrawTogglePreviewInner(def, hasBoxBg: false);
        }
    }

    void DrawTogglePreviewInner(ZUIButtonDef def, bool hasBoxBg = false)
    {
        var bgRect = GUILayoutUtility.GetRect(1f, 48f, GUILayout.ExpandWidth(true));
        if (!hasBoxBg) EditorGUI.DrawRect(bgRect, new Color(.13f, .13f, .15f, 1f));
        EditorGUI.LabelField(new Rect(bgRect.x + 8f, bgRect.y + 2f, 200f, 14f),
            "ZToggle preview — click to toggle", EditorStyles.miniLabel);

        var content    = new GUIContent(_previewButtonText);
        var labelStyle = def.GetLabelStyle();
        var btnSize    = labelStyle.CalcSize(content);
        float btnW     = Mathf.Max(btnSize.x, 60f);
        float btnH     = btnSize.y;
        float x        = bgRect.x + (bgRect.width - btnW) * 0.5f;
        float y        = bgRect.y + (bgRect.height - btnH - 14f) * 0.5f + 4f;

        var toggleRect = new Rect(x, y, btnW, btnH);
        _previewToggleValue = ZUI.Toggle(toggleRect, _previewToggleValue, content, def);

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.LabelField(new Rect(toggleRect.x, toggleRect.yMax + 2f, toggleRect.width, 12f),
                _previewToggleValue ? "On — click to turn Off" : "Off — click to turn On",
                EditorStyles.centeredGreyMiniLabel);
        }
    }

    void DrawBoxPreview(ZUIBoxDef def)
    {
        // Title + Content on one row
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Title", GUILayout.Width(32f));
        _previewBoxTitle = EditorGUILayout.TextField(_previewBoxTitle, GUILayout.MaxWidth(120f));
        GUILayout.Space(6f);
        EditorGUILayout.LabelField("Content", GUILayout.Width(48f));
        _previewBoxContent = EditorGUILayout.TextField(_previewBoxContent);
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);

        using (ZUI.Box(_previewBoxTitle, def))
        {
            if (!string.IsNullOrEmpty(_previewBoxContent))
                ZUI.Label(_previewBoxContent);
        }
    }

    // ── Export ────────────────────────────────────────────────────────────────

    void DrawExportPathField()
    {
        if (!InspectorSubheader("Export", "export")) return;
        GUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = k_LabelWidth;
        _exportPath = EditorGUILayout.TextField("Output", _exportPath);
        if (GUILayout.Button("…", GUILayout.Width(24f)))
        {
            var chosen = EditorUtility.SaveFilePanel("Export ZUI Styles",
                "Assets/Editor", _exportClassName, "cs");
            if (!string.IsNullOrEmpty(chosen))
            {
                var root = Application.dataPath.Replace("/Assets", "");
                _exportPath = chosen.StartsWith(root)
                    ? chosen.Substring(root.Length + 1) : chosen;
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(4f);
        if (GUILayout.Button($"Export  →  {_exportClassName}.cs", GUILayout.Height(24f)))
            Export();
    }

    void Export()
    {
        if (_sheet == null) return;
        var sb        = new StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd  HH:mm");

        sb.AppendLine($"// {_exportClassName}.cs");
        sb.AppendLine($"// Generated by ZUI Style Editor — {timestamp}");
        sb.AppendLine("// Do not edit manually — use Tools / ZUI Style Editor.");
        sb.AppendLine();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine($"public static class {_exportClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    public static class Buttons");
        sb.AppendLine("    {");

        foreach (var b in _sheet.buttons)
        {
            string id = SanitizeIdentifier(b.name);
            sb.AppendLine($"        public static readonly ZUIButtonDef {id} = new ZUIButtonDef(");
            sb.AppendLine($"            name:         \"{b.name}\",");
            sb.AppendLine($"            normal:       {G(b.normal)},");
            sb.AppendLine($"            hover:        {G(b.hover)},");
            sb.AppendLine($"            active:       {G(b.active)},");
            sb.AppendLine($"            textColor:    {C(b.textColor)},");
            sb.AppendLine($"            cornerRadius: {b.cornerRadius}");
            sb.Append    ("        )");
            if (b.border.width > 0f || b.padH != 10 || b.padV != 3)
            {
                sb.AppendLine();
                sb.AppendLine("        {");
                if (b.border.width > 0f)
                {
                    sb.AppendLine($"            border           = new ZUIBorderDef({C(b.border.gradient.GetColorA())}, {b.border.width:F1}f),");
                }
                if (b.padH != 10 || b.padV != 3)
                {
                    sb.AppendLine($"            padH = {b.padH},");
                    sb.AppendLine($"            padV = {b.padV},");
                }
                sb.Append("        }");
            }
            sb.AppendLine(";");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static class Boxes");
        sb.AppendLine("    {");

        foreach (var x in _sheet.boxes)
        {
            string id = SanitizeIdentifier(x.name);
            sb.AppendLine($"        public static readonly ZUIBoxDef {id} = new ZUIBoxDef(");
            sb.AppendLine($"            name:        \"{x.name}\",");
            sb.AppendLine($"            background:  {G(x.background)},");
            sb.AppendLine($"            labelColor:  {C(x.labelColor)},");
            sb.AppendLine($"            borderColor: {C(x.border.gradient.GetColorA())},");
            sb.AppendLine($"            borderWidth: {x.border.width:F1}f,");
            sb.AppendLine($"            padH:        {x.padH},");
            sb.AppendLine($"            padV:        {x.padV}");
            sb.Append    ("        )");
            if (x.shape.cornerRadius > 0)
            {
                sb.AppendLine();
                sb.AppendLine("        {");
                sb.AppendLine($"            shape            = {{ cornerRadius = {x.shape.cornerRadius} }},");
                sb.Append("        }");
            }
            sb.AppendLine(";");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        string fullPath = Path.Combine(
            Application.dataPath.Replace("/Assets", ""),
            _exportPath.TrimStart('/'));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("ZUI Style Editor",
            $"Exported {_sheet.buttons.Count} button(s) and {_sheet.boxes.Count} box(es).\n\nAccess via  {_exportClassName}.Buttons.Default\n\n{_exportPath}", "Ok");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetSheet(ZUIStyleSheetAsset sheet)
    {
        _sheet = sheet; _selectedButton = 0; _selectedBox = 0;
        ZUI.ActiveSheet = sheet;
        RepaintShowcase();
    }

    void CreateNewSheet()
    {
        var path = EditorUtility.SaveFilePanelInProject("Create Style Sheet",
            "ZUIStyleSheet", "asset", "Choose location for new ZUI Style Sheet");
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<ZUIStyleSheetAsset>();
        asset.EnsureDefaults();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        SetSheet(asset);
    }

    // ── JSON transfer ─────────────────────────────────────────────────────────

    void ExportJson()
    {
        if (_sheet == null) return;
        var path = EditorUtility.SaveFilePanel("Export Style Sheet as JSON",
            Application.dataPath, _sheet.name, "json");
        if (string.IsNullOrEmpty(path)) return;
        var json = JsonUtility.ToJson(_sheet, prettyPrint: true);
        File.WriteAllText(path, json, Encoding.UTF8);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("ZUI Style Editor", $"Style sheet exported to:\n{path}", "Ok");
    }

    void ImportJson()
    {
        var path = EditorUtility.OpenFilePanel("Import Style Sheet from JSON",
            Application.dataPath, "json");
        if (string.IsNullOrEmpty(path)) return;

        string json;
        try   { json = File.ReadAllText(path); }
        catch (Exception e) { EditorUtility.DisplayDialog("ZUI Style Editor", $"Failed to read file:\n{e.Message}", "Ok"); return; }

        if (_sheet == null)
        {
            var assetPath = EditorUtility.SaveFilePanelInProject("Save Imported Style Sheet",
                "ZUIStyleSheet", "asset", "Choose location for the imported style sheet");
            if (string.IsNullOrEmpty(assetPath)) return;
            var newAsset = CreateInstance<ZUIStyleSheetAsset>();
            AssetDatabase.CreateAsset(newAsset, assetPath);
            AssetDatabase.SaveAssets();
            SetSheet(AssetDatabase.LoadAssetAtPath<ZUIStyleSheetAsset>(assetPath));
        }

        JsonUtility.FromJsonOverwrite(json, _sheet);
        foreach (var b in _sheet.buttons) b.Invalidate();
        foreach (var b in _sheet.boxes)   b.Invalidate();
        EditorUtility.SetDirty(_sheet);
        AssetDatabase.SaveAssets();
        Repaint();
    }

    static void RepaintShowcase()
    {
        foreach (var w in Resources.FindObjectsOfTypeAll<ZUIShowcaseWindow>()) w.Repaint();
    }

    // ── Copy / paste helpers ──────────────────────────────────────────────────

    static void PasteGrad(ZUIGradient dst, ZUIGradient src)
    {
        dst.isGradient     = src.isGradient;
        dst.isRadial       = src.isRadial;
        dst.colorA         = src.colorA;
        dst.colorB         = src.colorB;
        dst.bias           = src.bias;
        dst.angle          = src.angle;
        dst.usePixelLength = src.usePixelLength;
        dst.pixelLength    = src.pixelLength;
        dst.pixelEdges     = src.pixelEdges;
        dst.Invalidate();
    }

    static ZUIButtonDef CopyButtonDef(ZUIButtonDef src) => new ZUIButtonDef
    {
        name             = src.name,
        // Normal
        normal           = src.normal.Clone(),
        text             = new ZUITextDef(src.text.color) { fontSize = src.text.fontSize, fontStyle = src.text.fontStyle },
        border           = src.border,
        cornerRadius     = src.cornerRadius,     padH             = src.padH,   padV = src.padV,
        roundTL = src.roundTL, roundTR = src.roundTR, roundBL = src.roundBL, roundBR = src.roundBR,
        useGlobalShape   = src.useGlobalShape,   useGlobalPadding = src.useGlobalPadding,
        useGlobalBorder  = src.useGlobalBorder,
        // Hover
        hoverBgOverride     = src.hoverBgOverride,     hover        = src.hover.Clone(),
        hoverTextOverride   = src.hoverTextOverride,   hoverText    = new ZUITextDef(src.hoverText.color) { fontSize = src.hoverText.fontSize, fontStyle = src.hoverText.fontStyle },
        hoverBorderOverride = src.hoverBorderOverride, hoverBorder  = src.hoverBorder,
        // Active
        activeBgOverride     = src.activeBgOverride,     active       = src.active.Clone(),
        activeTextOverride   = src.activeTextOverride,   activeText   = new ZUITextDef(src.activeText.color) { fontSize = src.activeText.fontSize, fontStyle = src.activeText.fontStyle },
        activeBorderOverride = src.activeBorderOverride, activeBorder = src.activeBorder,
    };

    static void PasteButtonDef(ZUIButtonDef dst, ZUIButtonDef src)
    {
        // Normal
        PasteGrad(dst.normal, src.normal);
        dst.text.color = src.text.color; dst.text.fontSize = src.text.fontSize; dst.text.fontStyle = src.text.fontStyle;
        dst.border = src.border;
        dst.cornerRadius = src.cornerRadius; dst.padH = src.padH; dst.padV = src.padV;
        dst.roundTL = src.roundTL; dst.roundTR = src.roundTR; dst.roundBL = src.roundBL; dst.roundBR = src.roundBR;
        dst.useGlobalShape = src.useGlobalShape; dst.useGlobalPadding = src.useGlobalPadding; dst.useGlobalBorder = src.useGlobalBorder;
        // Hover
        dst.hoverBgOverride = src.hoverBgOverride; PasteGrad(dst.hover, src.hover);
        dst.hoverTextOverride = src.hoverTextOverride; dst.hoverText.color = src.hoverText.color; dst.hoverText.fontSize = src.hoverText.fontSize; dst.hoverText.fontStyle = src.hoverText.fontStyle;
        dst.hoverBorderOverride = src.hoverBorderOverride; dst.hoverBorder = src.hoverBorder;
        // Active
        dst.activeBgOverride = src.activeBgOverride; PasteGrad(dst.active, src.active);
        dst.activeTextOverride = src.activeTextOverride; dst.activeText.color = src.activeText.color; dst.activeText.fontSize = src.activeText.fontSize; dst.activeText.fontStyle = src.activeText.fontStyle;
        dst.activeBorderOverride = src.activeBorderOverride; dst.activeBorder = src.activeBorder;
    }

    static ZUIBoxDef CopyBoxDef(ZUIBoxDef src) => new ZUIBoxDef
    {
        name             = src.name,
        background       = src.background.Clone(),
        titleText        = new ZUITextDef(src.titleText.color) { fontSize = src.titleText.fontSize, fontStyle = src.titleText.fontStyle },
        border           = src.border,
        shape            = src.shape,
        padH             = src.padH,
        padV             = src.padV,
        useGlobalBorder  = src.useGlobalBorder,
        useGlobalPadding = src.useGlobalPadding,
        useGlobalShape   = src.useGlobalShape,
    };

    static void PasteBoxDef(ZUIBoxDef dst, ZUIBoxDef src)
    {
        PasteGrad(dst.background, src.background);
        dst.titleText.color     = src.titleText.color;
        dst.titleText.fontSize  = src.titleText.fontSize;
        dst.titleText.fontStyle = src.titleText.fontStyle;
        dst.border           = src.border;
        dst.shape            = src.shape;
        dst.padH             = src.padH;
        dst.padV             = src.padV;
        dst.useGlobalBorder  = src.useGlobalBorder;
        dst.useGlobalPadding = src.useGlobalPadding;
        dst.useGlobalShape   = src.useGlobalShape;
    }

    // ── GUI style helpers ─────────────────────────────────────────────────────

    void EnsureStyles()
    {
        if (_listItemStyle != null) return;
        _listItemStyle = new GUIStyle(EditorStyles.label) { padding = new RectOffset(10, 6, 3, 3), fontSize = 12 };
        _listItemActiveStyle = new GUIStyle(_listItemStyle);
        _listItemActiveStyle.normal.background        = MakeSolidTex(new Color(.25f, .42f, .62f, 1f));
        _listItemActiveStyle.normal.scaledBackgrounds = new[] { _listItemActiveStyle.normal.background };
        _listItemActiveStyle.normal.textColor         = Color.white;
        _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
    }

    static GUIContent _iconCopy;
    static GUIContent _iconPaste;

    // Use Unity's built-in "copy" icon; fall back to a procedural one if unavailable.
    // Paste has no standard built-in equivalent so always uses the procedural icon.
    static GUIContent IconCopy  => _iconCopy  ??= new GUIContent(EditorGUIUtility.FindTexture("copy") ?? BuildFallbackCopyIcon(),  "Copy");
    static GUIContent IconPaste => _iconPaste ??= new GUIContent(EditorGUIUtility.FindTexture("paste") ?? BuildFallbackPasteIcon(), "Paste");

    // Fallback copy icon — two overlapping pages, 20×20 white-on-transparent.
    static Texture2D BuildFallbackCopyIcon()
    {
        const int S = 20;
        var t   = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pix = new Color32[S * S];
        var c   = new Color32(210, 210, 210, 255);
        var f   = new Color32(38,  38,  46,  220);

        IHLine(pix, S, 5, 17, 14, c);
        IHLine(pix, S, 5, 17,  3, c);
        IVLine(pix, S,  5,  3, 14, c);
        IVLine(pix, S, 17,  3, 14, c);

        for (int y = 6; y <= 17; y++)
        for (int x = 1; x <= 13; x++) pix[y * S + x] = f;
        IHLine(pix, S, 1, 13, 17, c);
        IHLine(pix, S, 1, 13,  6, c);
        IVLine(pix, S,  1,  6, 17, c);
        IVLine(pix, S, 13,  6, 17, c);

        t.SetPixels32(pix); t.Apply(); return t;
    }

    // Fallback paste icon — clipboard with text lines, 20×20 white-on-transparent.
    static Texture2D BuildFallbackPasteIcon()
    {
        const int S = 20;
        var t   = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pix = new Color32[S * S];
        var c   = new Color32(210, 210, 210, 255);

        IHLine(pix, S, 1, 17, 14, c);
        IHLine(pix, S, 1, 17,  0, c);
        IVLine(pix, S,  1,  0, 14, c);
        IVLine(pix, S, 17,  0, 14, c);

        IHLine(pix, S, 6, 12, 18, c);
        IVLine(pix, S,  6, 14, 18, c);
        IVLine(pix, S, 12, 14, 18, c);

        IHLine(pix, S, 4, 14, 10, c);
        IHLine(pix, S, 4, 14,  7, c);
        IHLine(pix, S, 4, 11,  4, c);

        t.SetPixels32(pix); t.Apply(); return t;
    }

    static void IHLine(Color32[] p, int S, int x0, int x1, int y, Color32 c)
        { for (int x = x0; x <= x1; x++) if (y >= 0 && y < S && x >= 0 && x < S) p[y * S + x] = c; }
    static void IVLine(Color32[] p, int S, int x, int y0, int y1, Color32 c)
        { for (int y = y0; y <= y1; y++) if (y >= 0 && y < S && x >= 0 && x < S) p[y * S + x] = c; }

    void InspectorHeader(string title)
    {
        EditorGUILayout.LabelField(title, _sectionHeaderStyle);
        GUILayout.Space(2f);
    }

    bool InspectorSubheader(string title, string key = null)
    {
        string k        = key ?? $"{_activeTab}_{title}";
        bool   expanded = GetFoldout(k);
        GUILayout.Space(2f);
        var   rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        float cy = rect.y + (rect.height - 14f) * 0.5f;
        EditorGUI.LabelField(new Rect(rect.x + 4f,  cy, 14f,              14f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.x + 18f, cy, rect.width - 22f, 14f), title,                EditorStyles.miniLabel);
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            expanded = !expanded;
            SetFoldout(k, expanded);
            Event.current.Use();
            Repaint();
        }
        GUILayout.Space(4f);
        return expanded;
    }

    bool InspectorSubheaderWithToggle(string title, string key, ref bool toggle, out bool toggleChanged)
    {
        string k       = key;
        bool   expanded = GetFoldout(k);
        toggleChanged = false;
        GUILayout.Space(2f);
        var   rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        float cy = rect.y + (rect.height - 14f) * 0.5f;

        // Arrow + title + toggle on the left
        const float toggleW = 16f, gap = 4f;
        float titleX = rect.x + 18f;
        float titleW = 100f;
        float toggleX = titleX + titleW + gap;

        EditorGUI.LabelField(new Rect(rect.x + 4f, cy, 14f, 14f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(titleX, cy, titleW, 14f), title, EditorStyles.miniLabel);
        bool newToggle = GUI.Toggle(new Rect(toggleX, cy, toggleW, 14f), toggle, GUIContent.none);
        if (newToggle != toggle) { toggle = newToggle; toggleChanged = true; }

        var toggleRect = new Rect(toggleX, cy, toggleW, 14f);
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)
            && !toggleRect.Contains(Event.current.mousePosition))
        {
            expanded = !expanded;
            SetFoldout(k, expanded);
            Event.current.Use();
            Repaint();
        }
        GUILayout.Space(4f);
        return expanded;
    }

    bool InspectorSubheaderWithToggles(string title, string key,
        string label1, ref bool toggle1, string label2, ref bool toggle2,
        out bool togglesChanged)
    {
        string k       = key;
        bool   expanded = GetFoldout(k);
        togglesChanged = false;
        GUILayout.Space(2f);
        var   rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        float cy = rect.y + (rect.height - 14f) * 0.5f;

        // Arrow + title + labelled toggles on the left
        const float toggleW = 16f, labelW = 32f, gap = 4f;
        float titleX = rect.x + 18f;
        float titleW = 68f;
        float gx = titleX + titleW + gap;

        EditorGUI.LabelField(new Rect(rect.x + 4f, cy, 14f, 14f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(titleX, cy, titleW, 14f), title, EditorStyles.miniLabel);

        var miniRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
        EditorGUI.LabelField(new Rect(gx, cy, labelW, 14f), label1, miniRight);
        bool new1 = GUI.Toggle(new Rect(gx + labelW, cy, toggleW, 14f), toggle1, GUIContent.none);
        float x2 = gx + labelW + toggleW + gap;
        EditorGUI.LabelField(new Rect(x2, cy, labelW, 14f), label2, miniRight);
        bool new2 = GUI.Toggle(new Rect(x2 + labelW, cy, toggleW, 14f), toggle2, GUIContent.none);
        if (new1 != toggle1) { toggle1 = new1; togglesChanged = true; }
        if (new2 != toggle2) { toggle2 = new2; togglesChanged = true; }

        float togglesEnd = x2 + labelW + toggleW;
        var togglesRect = new Rect(gx, rect.y, togglesEnd - gx, rect.height);
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition)
            && !togglesRect.Contains(Event.current.mousePosition))
        {
            expanded = !expanded;
            SetFoldout(k, expanded);
            Event.current.Use();
            Repaint();
        }
        GUILayout.Space(4f);
        return expanded;
    }

    bool InspectorSubheaderWithCopyPaste(string title, Action onCopy, Action onPaste, bool canPaste, string key = null)
    {
        string k        = key ?? $"{_activeTab}_{title}";
        bool   expanded = GetFoldout(k);
        GUILayout.Space(2f);
        var   rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        float cy14 = rect.y + (rect.height - 14f) * 0.5f;
        float cy24 = rect.y + (rect.height - 24f) * 0.5f;

        const float btnW = 28f, pad = 2f, margin = 4f;
        float bPx    = rect.xMax - margin - btnW;
        float bCx    = bPx - pad - btnW;
        float titleW = bCx - (rect.x + 18f) - 4f;

        EditorGUI.LabelField(new Rect(rect.x + 4f,  cy14, 14f,    14f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.x + 18f, cy14, titleW, 14f), title,                EditorStyles.miniLabel);
        if (GUI.Button(new Rect(bCx, cy24, btnW, 24f), IconCopy,  EditorStyles.miniButton)) onCopy();
        GUI.enabled = canPaste;
        if (GUI.Button(new Rect(bPx, cy24, btnW, 24f), IconPaste, EditorStyles.miniButton)) onPaste();
        GUI.enabled = true;

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            expanded = !expanded;
            SetFoldout(k, expanded);
            Event.current.Use();
            Repaint();
        }
        GUILayout.Space(4f);
        return expanded;
    }

    // Header with Override toggle on the right (hover/active state sections).
    // The override checkbox can be clicked independently of the expand/collapse area.
    // onRevert: when not null and override is active, a ↩ button and right-click menu appear.
    bool InspectorSubheaderWithOverride(string title, bool currentOverride, out bool newOverride, Action onRevert = null, string key = null)
    {
        string k        = key ?? $"{_activeTab}_{title}";
        bool   expanded = GetFoldout(k);
        GUILayout.Space(2f);
        var   rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        float cy14 = rect.y + (rect.height - 14f) * 0.5f;
        float cy12 = rect.y + (rect.height - 12f) * 0.5f;

        const float chkW = 14f, ovLblW = 48f, margin = 4f, revW = 18f;
        float chkX   = rect.xMax - chkW - margin;
        float lblX   = chkX - 2f - ovLblW;
        float revX   = lblX - 2f - revW;
        float titleW = (onRevert != null && currentOverride ? revX : lblX) - (rect.x + 18f) - 4f;

        EditorGUI.LabelField(new Rect(rect.x + 4f,  cy14, 14f,    14f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.x + 18f, cy14, titleW, 14f), title,                EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(lblX, cy14, ovLblW, 14f), "Override", EditorStyles.miniLabel);
        newOverride = EditorGUI.Toggle(new Rect(chkX, cy12, chkW, 12f), currentOverride);

        if (onRevert != null && currentOverride)
        {
            if (GUI.Button(new Rect(revX, cy12, revW, 12f), "↩", EditorStyles.miniButton))
            {
                onRevert();
                GUIUtility.ExitGUI();
            }
        }

        // Right-click context menu for revert
        if (onRevert != null && currentOverride &&
            Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Revert to Parent"), false, () => onRevert());
            menu.ShowAsContext();
            Event.current.Use();
        }

        // Expand/collapse: exclude the right-side controls zone
        float controlsStartX = onRevert != null && currentOverride ? revX - 4f : lblX - 4f;
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            if (Event.current.mousePosition.x < controlsStartX)
            {
                expanded = !expanded;
                SetFoldout(k, expanded);
                Event.current.Use();
                Repaint();
            }
        }
        GUILayout.Space(4f);
        return expanded;
    }

    // Header combining Override toggle + C/P buttons + optional revert button.
    bool InspectorSubheaderWithOverrideCopyPaste(string title, bool currentOverride, out bool newOverride,
        Action onCopy, Action onPaste, bool canPaste, Action onRevert = null, string key = null)
    {
        string k        = key ?? $"{_activeTab}_{title}";
        bool   expanded = GetFoldout(k);
        GUILayout.Space(2f);
        var   rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        float cy14 = rect.y + (rect.height - 14f) * 0.5f;
        float cy12 = rect.y + (rect.height - 12f) * 0.5f;
        float cy24 = rect.y + (rect.height - 24f) * 0.5f;

        const float btnW = 28f, pad = 2f, margin = 4f, chkW = 14f, ovLblW = 48f, revW = 18f;
        float bPx  = rect.xMax - margin - btnW;
        float bCx  = bPx - pad - btnW;
        float chkX = bCx - 4f - chkW;
        float lblX = chkX - 2f - ovLblW;
        float revX = lblX - 2f - revW;
        float titleEndX = (onRevert != null && currentOverride) ? revX - 4f : lblX - 4f;
        float titleW = titleEndX - (rect.x + 18f);

        EditorGUI.LabelField(new Rect(rect.x + 4f,  cy14, 14f,    14f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.x + 18f, cy14, titleW, 14f), title,                EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(lblX, cy14, ovLblW, 14f), "Override", EditorStyles.miniLabel);
        newOverride = EditorGUI.Toggle(new Rect(chkX, cy12, chkW, 12f), currentOverride);
        if (GUI.Button(new Rect(bCx, cy24, btnW, 24f), IconCopy,  EditorStyles.miniButton)) onCopy();
        GUI.enabled = canPaste;
        if (GUI.Button(new Rect(bPx, cy24, btnW, 24f), IconPaste, EditorStyles.miniButton)) onPaste();
        GUI.enabled = true;

        if (onRevert != null && currentOverride)
        {
            if (GUI.Button(new Rect(revX, cy12, revW, 12f), "↩", EditorStyles.miniButton))
            {
                onRevert();
                GUIUtility.ExitGUI();
            }
        }

        if (onRevert != null && currentOverride &&
            Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Revert to Parent"), false, () => onRevert());
            menu.ShowAsContext();
            Event.current.Use();
        }

        float controlsStartX = (onRevert != null && currentOverride) ? revX - 4f : lblX - 4f;
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            if (Event.current.mousePosition.x < controlsStartX)
            {
                expanded = !expanded;
                SetFoldout(k, expanded);
                Event.current.Use();
                Repaint();
            }
        }
        GUILayout.Space(4f);
        return expanded;
    }

    // Header with Copy/Paste buttons AND a "Global" toggle on the right.
    // Copy/Paste buttons consume their own clicks; global toggle is handled independently.
    bool InspectorSubheaderWithCopyPasteAndGlobal(string title, Action onCopy, Action onPaste, bool canPaste,
                                                   bool currentGlobal, out bool newGlobal, string key = null)
    {
        string k        = key ?? $"{_activeTab}_{title}";
        bool   expanded = GetFoldout(k);
        GUILayout.Space(2f);
        var   rect = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.14f, .14f, .16f, 1f));
        float cy14 = rect.y + (rect.height - 14f) * 0.5f;
        float cy12 = rect.y + (rect.height - 12f) * 0.5f;
        float cy24 = rect.y + (rect.height - 24f) * 0.5f;

        const float btnW = 28f, pad = 2f, margin = 4f, chkW = 14f, glblLbl = 40f;
        float bPx    = rect.xMax - margin - btnW;
        float bCx    = bPx - pad - btnW;
        float chkX   = bCx - 4f - chkW;
        float glbX   = chkX - 2f - glblLbl;
        float titleW = glbX - (rect.x + 18f) - 4f;

        EditorGUI.LabelField(new Rect(rect.x + 4f,  cy14, 14f,    14f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.x + 18f, cy14, titleW, 14f), title,                EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(glbX, cy14, glblLbl, 14f), "Global", EditorStyles.miniLabel);
        newGlobal = EditorGUI.Toggle(new Rect(chkX, cy12, chkW, 12f), currentGlobal);
        if (GUI.Button(new Rect(bCx, cy24, btnW, 24f), IconCopy,  EditorStyles.miniButton)) onCopy();
        GUI.enabled = canPaste;
        if (GUI.Button(new Rect(bPx, cy24, btnW, 24f), IconPaste, EditorStyles.miniButton)) onPaste();
        GUI.enabled = true;

        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            expanded = !expanded;
            SetFoldout(k, expanded);
            Event.current.Use();
            Repaint();
        }
        GUILayout.Space(4f);
        return expanded;
    }

    static void DrawDivider()
    {
        var rect = GUILayoutUtility.GetRect(1f, float.MaxValue, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, .4f));
    }

    static void CenteredLabel(string text)
    {
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(text, EditorStyles.centeredGreyMiniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
    }

    static Texture2D MakeSolidTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }

    static string C(Color c) => $"new Color({c.r:F2}f, {c.g:F2}f, {c.b:F2}f, {c.a:F2}f)";

    static string G(ZUIGradient g)
    {
        if (!g.isGradient) return $"new ZUIGradient({C(g.colorA)})";
        if (g.isRadial)
            return $"new ZUIGradient({C(g.colorA)}, {C(g.colorB)}, 90f, {g.bias:F2}f) {{ isRadial = true }}";
        return $"new ZUIGradient({C(g.colorA)}, {C(g.colorB)}, {g.angle:F1}f, {g.bias:F2}f)";
    }

    static string SanitizeIdentifier(string name)
    {
        var sb = new StringBuilder(); bool cap = true;
        foreach (char ch in name)
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(cap ? char.ToUpper(ch) : ch); cap = false; }
            else { cap = true; }
        }
        if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.Length > 0 ? sb.ToString() : "Style";
    }

    static string GetStyleName<T>(T item) where T : class
    {
        if (item is ZUIButtonDef    b) return b.name;
        if (item is ZUIBoxDef       x) return x.name;
        if (item is ZUITextStyleDef t) return t.name;
        if (item is ZUISliderDef    s) return s.name;
        return null;
    }

    // ── ZUIColorPicker ────────────────────────────────────────────────────────
    // Two-control color picker: [swatch or ColorField] [⊞ button]
    // ⊞ opens a popover to toggle between Direct color and Palette color modes.
    //
    // ZUIColorPicker(label, ...)  — draws a full labeled row (BeginHorizontal inside)
    // ZUIColorPickerInline(...)   — draws just the two controls, for embedding in an existing horizontal
    //
    // Because lambdas cannot capture ref parameters, the popover communicates back via a
    // pending-write slot (_cpPendingKey / _cpPendingResult). Each call site passes a unique key
    // string; on the next OnGUI pass the caller reads the pending result and applies it.

    string                                   _cpPendingKey;
    (Color color, string paletteRef, ZUIPaletteSlot slot)? _cpPendingResult;
    readonly Dictionary<string, Rect>        _cpBtnRects = new Dictionary<string, Rect>();

    bool ZUIColorPicker(string label, ref Color color, ref string paletteRef, ref ZUIPaletteSlot slot,
        float labelWidth = 82f)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));
        bool changed = ZUIColorPickerInline(ref color, ref paletteRef, ref slot);
        EditorGUILayout.EndHorizontal();
        return changed;
    }

    // key  — a stable per-layout-position ID so the pending write goes to the right field.
    bool ZUIColorPickerInline(string key, ref Color color, ref string paletteRef, ref ZUIPaletteSlot slot)
    {
        bool changed = false;

        // Apply any pending write from the popover
        if (_cpPendingKey == key && _cpPendingResult.HasValue)
        {
            color      = _cpPendingResult.Value.color;
            paletteRef = _cpPendingResult.Value.paletteRef;
            slot       = _cpPendingResult.Value.slot;
            _cpPendingKey    = null;
            _cpPendingResult = null;
            changed          = true;
        }

        bool hasRef    = !string.IsNullOrEmpty(paletteRef);
        var  pal       = hasRef ? _sheet?.FindPaletteColor(paletteRef) : null;
        Color resolved = pal != null ? pal.Resolve(slot) : color;

        // Left control: read-only swatch (palette mode) or editable ColorField (direct mode)
        if (hasRef)
        {
            var swatchRect = GUILayoutUtility.GetRect(90f, EditorGUIUtility.singleLineHeight, GUILayout.Width(90f));
            EditorGUI.DrawRect(swatchRect, resolved);
            string slotChar = slot == ZUIPaletteSlot.Highlight ? "H" : slot == ZUIPaletteSlot.Shade ? "S" : "P";
            var labelStyle  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            EditorGUI.LabelField(swatchRect, $" {paletteRef} · {slotChar}", labelStyle);
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            color = EditorGUILayout.ColorField(GUIContent.none, color, true, true, false, GUILayout.Width(90f));
            if (EditorGUI.EndChangeCheck()) changed = true;
        }

        // Right control: ⊞ button opens popover anchored directly to the button rect.
        // Rect is captured during Repaint (reliable) and stored in _cpBtnRects by key.
        // The Button() call below handles click detection; we then look up the stored rect.
        if (Event.current.type == EventType.Repaint)
        {
            GUILayout.Button("⊞", EditorStyles.miniButton, GUILayout.Width(20f));
            _cpBtnRects[key] = GUILayoutUtility.GetLastRect();
        }
        else if (GUILayout.Button("⊞", EditorStyles.miniButton, GUILayout.Width(20f)))
        {
            _cpBtnRects.TryGetValue(key, out var btnRect);
            var popup = new ZUIColorPickerPopup(color, paletteRef, slot, _sheet?.palette,
                (newColor, newRef, newSlot) =>
                {
                    _cpPendingKey    = key;
                    _cpPendingResult = (newColor, newRef, newSlot);
                    Repaint();
                });
            PopupWindow.Show(btnRect, popup);
        }

        return changed;
    }

    // Overload without explicit key — derives key from GUIUtility.GetControlID for uniqueness
    bool ZUIColorPickerInline(ref Color color, ref string paletteRef, ref ZUIPaletteSlot slot)
    {
        // Use a stable key based on the current layout cursor position
        int id  = GUIUtility.GetControlID(FocusType.Passive);
        return ZUIColorPickerInline(id.ToString(), ref color, ref paletteRef, ref slot);
    }

    // ── Color picker popover content ──────────────────────────────────────────

    class ZUIColorPickerPopup : PopupWindowContent
    {
        Color  _color;
        string _paletteRef;
        ZUIPaletteSlot _slot;
        List<ZUIPaletteColor> _palette;
        Action<Color, string, ZUIPaletteSlot> _onChanged;

        bool _paletteMode;

        public ZUIColorPickerPopup(Color color, string paletteRef, ZUIPaletteSlot slot,
            List<ZUIPaletteColor> palette, Action<Color, string, ZUIPaletteSlot> onChanged)
        {
            _color       = color;
            _paletteRef  = paletteRef;
            _slot        = slot;
            _palette     = palette;
            _onChanged   = onChanged;
            _paletteMode = !string.IsNullOrEmpty(paletteRef);
        }

        const float k_SwatchW  = 22f;
        const float k_RowH     = 18f;
        const float k_Pad      = 6f;
        const float k_NameW    = 90f;
        const float k_PopW     = 220f;

        // Height: mode radio row (22) + padding (4) + direct picker row (18+4) OR palette rows
        public override Vector2 GetWindowSize()
        {
            float h = 22f + 4f; // mode radio + gap
            if (_paletteMode)
            {
                int count = (_palette != null ? _palette.Count : 0);
                h += count > 0 ? count * (k_RowH + 2f) + 4f : 20f; // rows or "empty" msg
            }
            else
            {
                h += k_RowH + 4f; // color field
            }
            return new Vector2(k_PopW, h);
        }

        public override void OnGUI(Rect rect)
        {
            bool changed = false;

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(k_Pad);

            // Mode radio
            bool newPaletteMode = GUILayout.Toggle(_paletteMode,  "Palette", EditorStyles.miniButtonLeft,  GUILayout.Width(60f));
            GUILayout.Toggle(!_paletteMode, "Direct", EditorStyles.miniButtonRight, GUILayout.Width(60f));
            if (newPaletteMode != _paletteMode)
            {
                _paletteMode = newPaletteMode;
                if (!_paletteMode) _paletteRef = "";
                changed = true;
                editorWindow?.Repaint();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            if (_paletteMode)
            {
                if (_palette == null || _palette.Count == 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(k_Pad);
                    EditorGUILayout.LabelField("No palette entries defined.", EditorStyles.miniLabel);
                    GUILayout.EndHorizontal();
                }
                else
                {
                    // One row per palette entry: [Name label][P swatch][H swatch][S swatch]
                    // Clicking any swatch selects that entry + slot in one tap.
                    var nameStyle = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
                    foreach (var entry in _palette)
                    {
                        bool isSelected = entry.name == _paletteRef;
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(k_Pad);

                        // Highlight selected row
                        if (isSelected)
                        {
                            var rowRect = GUILayoutUtility.GetRect(k_PopW - k_Pad * 2f, k_RowH, GUILayout.Width(k_PopW - k_Pad * 2f), GUILayout.Height(k_RowH));
                            EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.08f));
                            GUILayout.EndHorizontal();
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(k_Pad);
                        }

                        // Name
                        EditorGUILayout.LabelField(entry.name, nameStyle, GUILayout.Width(k_NameW), GUILayout.Height(k_RowH));

                        // P / H / S swatches
                        foreach (ZUIPaletteSlot s in new[] { ZUIPaletteSlot.Primary, ZUIPaletteSlot.Highlight, ZUIPaletteSlot.Shade })
                        {
                            Color swColor  = entry.Resolve(s);
                            bool  isActive = isSelected && _slot == s;
                            var   swRect   = GUILayoutUtility.GetRect(k_SwatchW, k_RowH, GUILayout.Width(k_SwatchW), GUILayout.Height(k_RowH));

                            if (Event.current.type == EventType.Repaint)
                            {
                                EditorGUI.DrawRect(swRect, swColor);
                                if (isActive)
                                {
                                    // White outline on selected swatch
                                    float b = 2f;
                                    EditorGUI.DrawRect(new Rect(swRect.x, swRect.y, swRect.width, b), Color.white);
                                    EditorGUI.DrawRect(new Rect(swRect.x, swRect.yMax - b, swRect.width, b), Color.white);
                                    EditorGUI.DrawRect(new Rect(swRect.x, swRect.y, b, swRect.height), Color.white);
                                    EditorGUI.DrawRect(new Rect(swRect.xMax - b, swRect.y, b, swRect.height), Color.white);
                                }
                                // Slot letter
                                string lbl = s == ZUIPaletteSlot.Highlight ? "H" : s == ZUIPaletteSlot.Shade ? "S" : "P";
                                float luminance = swColor.r * 0.299f + swColor.g * 0.587f + swColor.b * 0.114f;
                                var   txtColor  = luminance > 0.45f ? Color.black : Color.white;
                                var   lblStyle  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = txtColor }, alignment = TextAnchor.MiddleCenter, fontSize = 9 };
                                GUI.Label(swRect, lbl, lblStyle);
                            }

                            if (Event.current.type == EventType.MouseDown && swRect.Contains(Event.current.mousePosition))
                            {
                                _paletteRef = entry.name;
                                _slot       = s;
                                changed     = true;
                                Event.current.Use();
                                editorWindow?.Repaint();
                            }
                        }

                        GUILayout.Space(k_Pad);
                        GUILayout.EndHorizontal();
                        GUILayout.Space(2f);
                    }
                }
            }
            else
            {
                // Direct color picker
                GUILayout.BeginHorizontal();
                GUILayout.Space(k_Pad);
                EditorGUI.BeginChangeCheck();
                _color = EditorGUILayout.ColorField(GUIContent.none, _color, true, true, false);
                if (EditorGUI.EndChangeCheck()) changed = true;
                GUILayout.Space(k_Pad);
                GUILayout.EndHorizontal();
            }

            if (changed) _onChanged?.Invoke(_color, _paletteMode ? _paletteRef : "", _slot);
        }
    }

    // ── Palette tab ───────────────────────────────────────────────────────────

    // ── Missing tab ───────────────────────────────────────────────────────────

    void DrawMissingTab()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Missing Style Lookups", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50f)))
            ZUIMissingStyleRegistry.Clear();
        GUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            "Any style name that was looked up but not found in the sheet. Resets on domain reload — just repaint your windows to re-populate.",
            EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(6f);

        var entries = new List<ZUIMissingStyleRegistry.Entry>(ZUIMissingStyleRegistry.Entries);
        if (entries.Count == 0)
        {
            var okStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.4f, 0.9f, 0.4f, 1f) } };
            EditorGUILayout.LabelField("No missing styles detected.", okStyle);
        }
        else
        {
            var rowStyle = new GUIStyle(EditorStyles.label)
            {
                normal   = { textColor = new Color(1f, 0.38f, 0.3f, 1f) },
                richText = true,
            };
            var countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal    = { textColor = new Color(0.7f, 0.7f, 0.7f, 1f) },
                alignment = TextAnchor.MiddleRight,
            };

            // Sort: Button first, then Box, then Text, alphabetically within each type.
            entries.Sort((a, b) =>
            {
                int tc = a.type.CompareTo(b.type);
                return tc != 0 ? tc : string.Compare(a.requestedName, b.requestedName, System.StringComparison.Ordinal);
            });

            ZUIMissingStyleRegistry.EntryType? lastType = null;
            foreach (var entry in entries)
            {
                if (entry.type != lastType)
                {
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField(entry.type.ToString() + " Styles", EditorStyles.miniLabel);
                    lastType = entry.type;
                }

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  \u2022 " + entry.requestedName, rowStyle);  // bullet
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("x" + entry.hitCount, countStyle, GUILayout.Width(36f));
                GUILayout.EndHorizontal();
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // ── Palette tab ───────────────────────────────────────────────────────────

    void DrawPaletteTab()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
        EditorGUIUtility.labelWidth = k_LabelWidth;

        InspectorHeader("Color Palette");
        EditorGUILayout.LabelField("Named colors that can be referenced by any style field.", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(6f);

        bool dirty = false;
        var palette = _sheet.palette;
        int duplicatePaletteAt = -1;
        int removePaletteAt    = -1;

        // Column headers
        GUILayout.BeginHorizontal();
        GUILayout.Space(120f);
        EditorGUILayout.LabelField("Primary",   EditorStyles.miniLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("Highlight", EditorStyles.miniLabel, GUILayout.Width(80f));
        EditorGUILayout.LabelField("Shade",     EditorStyles.miniLabel, GUILayout.Width(80f));
        GUILayout.Space(44f);
        GUILayout.EndHorizontal();

        for (int i = 0; i < palette.Count; i++)
        {
            var entry = palette[i];
            GUILayout.BeginHorizontal();

            string oldName = entry.name;
            EditorGUI.BeginChangeCheck();
            entry.name = EditorGUILayout.TextField(entry.name, GUILayout.Width(120f));
            if (EditorGUI.EndChangeCheck() && entry.name != oldName)
            {
                // Rename: update all refs in buttons and boxes
                foreach (var b in _sheet.buttons)
                {
                    bool changed = false;
                    if (b.border.gradient.colorARef             == oldName) { b.border.gradient.colorARef             = entry.name; changed = true; }
                    if (b.border.gradient.colorBRef             == oldName) { b.border.gradient.colorBRef             = entry.name; changed = true; }
                    if (b.hoverBorder.gradient.colorARef        == oldName) { b.hoverBorder.gradient.colorARef        = entry.name; changed = true; }
                    if (b.hoverBorder.gradient.colorBRef        == oldName) { b.hoverBorder.gradient.colorBRef        = entry.name; changed = true; }
                    if (b.activeBorder.gradient.colorARef       == oldName) { b.activeBorder.gradient.colorARef       = entry.name; changed = true; }
                    if (b.activeBorder.gradient.colorBRef       == oldName) { b.activeBorder.gradient.colorBRef       = entry.name; changed = true; }
                    if (b.bgShadow.colorRef            == oldName) { b.bgShadow.colorRef            = entry.name; changed = true; }
                    if (b.text.colorRef                == oldName) { b.text.colorRef                = entry.name; changed = true; }
                    if (b.text.colorBRef               == oldName) { b.text.colorBRef               = entry.name; changed = true; }
                    if (b.text.shadowColorRef          == oldName) { b.text.shadowColorRef          = entry.name; changed = true; }
                    if (b.text.outlineColorRef         == oldName) { b.text.outlineColorRef         = entry.name; changed = true; }
                    if (b.normal.colorARef == oldName) { b.normal.colorARef = entry.name; changed = true; }
                    if (b.normal.colorBRef == oldName) { b.normal.colorBRef = entry.name; changed = true; }
                    if (b.hover.colorARef  == oldName) { b.hover.colorARef  = entry.name; changed = true; }
                    if (b.hover.colorBRef  == oldName) { b.hover.colorBRef  = entry.name; changed = true; }
                    if (b.active.colorARef == oldName) { b.active.colorARef = entry.name; changed = true; }
                    if (b.active.colorBRef == oldName) { b.active.colorBRef = entry.name; changed = true; }
                    if (changed) b.Invalidate();
                }
                foreach (var b in _sheet.boxes)
                {
                    bool changed = false;
                    if (b.border.gradient.colorARef     == oldName) { b.border.gradient.colorARef     = entry.name; changed = true; }
                    if (b.border.gradient.colorBRef     == oldName) { b.border.gradient.colorBRef     = entry.name; changed = true; }
                    if (b.bgShadow.colorRef            == oldName) { b.bgShadow.colorRef            = entry.name; changed = true; }
                    if (b.titleText.colorRef           == oldName) { b.titleText.colorRef           = entry.name; changed = true; }
                    if (b.titleText.colorBRef          == oldName) { b.titleText.colorBRef          = entry.name; changed = true; }
                    if (b.titleText.shadowColorRef     == oldName) { b.titleText.shadowColorRef     = entry.name; changed = true; }
                    if (b.titleText.outlineColorRef    == oldName) { b.titleText.outlineColorRef    = entry.name; changed = true; }
                    if (b.contentText.colorRef         == oldName) { b.contentText.colorRef         = entry.name; changed = true; }
                    if (b.contentText.colorBRef        == oldName) { b.contentText.colorBRef        = entry.name; changed = true; }
                    if (b.contentText.shadowColorRef   == oldName) { b.contentText.shadowColorRef   = entry.name; changed = true; }
                    if (b.contentText.outlineColorRef  == oldName) { b.contentText.outlineColorRef  = entry.name; changed = true; }
                    if (b.background.colorARef         == oldName) { b.background.colorARef         = entry.name; changed = true; }
                    if (b.background.colorBRef         == oldName) { b.background.colorBRef         = entry.name; changed = true; }
                    if (changed) b.Invalidate();
                }
                foreach (var t in _sheet.textStyles)
                {
                    bool changed = false;
                    if (t.text.colorRef           == oldName) { t.text.colorRef           = entry.name; changed = true; }
                    if (t.text.colorBRef          == oldName) { t.text.colorBRef          = entry.name; changed = true; }
                    if (t.text.shadowColorRef     == oldName) { t.text.shadowColorRef     = entry.name; changed = true; }
                    if (t.text.outlineColorRef    == oldName) { t.text.outlineColorRef    = entry.name; changed = true; }
                    if (changed) t.Invalidate();
                }
                dirty = true;
            }

            EditorGUI.BeginChangeCheck();
            entry.color     = EditorGUILayout.ColorField(GUIContent.none, entry.color,     true, true, false, GUILayout.Width(80f));
            entry.highlight = EditorGUILayout.ColorField(GUIContent.none, entry.highlight, true, true, false, GUILayout.Width(80f));
            entry.shade     = EditorGUILayout.ColorField(GUIContent.none, entry.shade,     true, true, false, GUILayout.Width(80f));
            if (EditorGUI.EndChangeCheck())
            {
                InvalidatePaletteRefs(entry.name);
                dirty = true;
                Repaint();
            }

            if (GUILayout.Button("⧉", EditorStyles.miniButton, GUILayout.Width(20f)))
                duplicatePaletteAt = i;

            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20f)))
                removePaletteAt = i;

            GUILayout.EndHorizontal();
        }

        if (duplicatePaletteAt >= 0)
        {
            var src   = palette[duplicatePaletteAt];
            var clone = JsonUtility.FromJson<ZUIPaletteColor>(JsonUtility.ToJson(src));
            if (!clone.name.EndsWith(" (Copy)")) clone.name += " (Copy)";
            palette.Insert(duplicatePaletteAt + 1, clone);
            dirty = true;
        }
        if (removePaletteAt >= 0)
        {
            palette.RemoveAt(removePaletteAt);
            dirty = true;
        }

        GUILayout.Space(4f);
        if (GUILayout.Button("+ Add Color", EditorStyles.miniButton))
        {
            palette.Add(new ZUIPaletteColor { name = "New Color", color = Color.white });
            dirty = true;
        }

        if (dirty) { EditorUtility.SetDirty(_sheet); RepaintShowcase(); }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // ── Palette invalidation helpers ──────────────────────────────────────────

    void InvalidatePaletteRefs(string paletteName)
    {
        if (_sheet == null) return;
        foreach (var b in _sheet.buttons)
            if (ReferencesColor(b, paletteName)) b.Invalidate();
        foreach (var b in _sheet.boxes)
            if (ReferencesColor(b, paletteName)) b.Invalidate();
        foreach (var t in _sheet.textStyles)
            if (ReferencesColor(t.text, paletteName)) t.Invalidate();
    }

    static bool ReferencesColor(ZUITextDef t, string name) =>
        t.colorRef == name || t.colorBRef == name ||
        t.shadowColorRef == name || t.outlineColorRef == name;

    bool ReferencesColor(ZUIButtonDef b, string name) =>
        b.border.gradient.colorARef == name || b.border.gradient.colorBRef == name ||
        b.hoverBorder.gradient.colorARef == name || b.hoverBorder.gradient.colorBRef == name ||
        b.activeBorder.gradient.colorARef == name || b.activeBorder.gradient.colorBRef == name ||
        b.bgShadow.colorRef == name ||
        ReferencesColor(b.text, name) ||
        b.normal.colorARef == name || b.normal.colorBRef == name ||
        b.hover.colorARef == name || b.hover.colorBRef == name ||
        b.active.colorARef == name || b.active.colorBRef == name;

    bool ReferencesColor(ZUIBoxDef b, string name) =>
        b.border.gradient.colorARef == name || b.border.gradient.colorBRef == name ||
        b.bgShadow.colorRef == name ||
        ReferencesColor(b.titleText, name) ||
        ReferencesColor(b.contentText, name) ||
        b.background.colorARef == name || b.background.colorBRef == name;

    // ── Slider inspector ──────────────────────────────────────────────────────

    [SerializeField] private float _sliderPreviewValue    = 0.6f;
    [SerializeField] private int   _sliderPreviewBgMode   = 0;   // 0=None, 1=Box
    [SerializeField] private int   _sliderPreviewBoxIndex = 0;

    void DrawSliderInspector()
    {
        if (_selectedSlider < 0 || _selectedSlider >= _sheet.sliders.Count)
        { CenteredLabel("Select a slider style."); return; }

        var  def     = _sheet.sliders[_selectedSlider];
        bool changed = false;
        EditorGUIUtility.labelWidth = k_LabelWidth;

        InspectorHeader("Slider Style");

        GUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        def.name = EditorGUILayout.TextField("Name", def.name);
        if (EditorGUI.EndChangeCheck()) { ZUIMissingStyleRegistry.Remove(ZUIMissingStyleRegistry.EntryType.Slider, def.name); changed = true; }
        if (GUILayout.Button("Flash", EditorStyles.miniButton, GUILayout.Width(44f))) ZUI.StartFlash(def.name, ZUI.FlashDefType.Slider);
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        DrawPreviewHeader();

        // ── Preview ───────────────────────────────────────────────────────────
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Background", GUILayout.Width(k_LabelWidth));
        _sliderPreviewBgMode = GUILayout.Toolbar(_sliderPreviewBgMode,
            new[] { "None", "Box" }, EditorStyles.miniButton);
        GUILayout.EndHorizontal();

        if (_sliderPreviewBgMode == 1 && _sheet.boxes.Count > 0)
        {
            _sliderPreviewBoxIndex = Mathf.Clamp(_sliderPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
            var names = new string[_sheet.boxes.Count];
            for (int i = 0; i < _sheet.boxes.Count; i++) names[i] = _sheet.boxes[i].name;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Box Style", GUILayout.Width(k_LabelWidth));
            _sliderPreviewBoxIndex = EditorGUILayout.Popup(_sliderPreviewBoxIndex, names);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4f);

        float sliderH = Mathf.Max(def.thumbHeight > 0f ? def.thumbHeight : 20f, def.trackHeight);

        if (_sliderPreviewBgMode == 1 && _sheet.boxes.Count > 0)
        {
            _sliderPreviewBoxIndex = Mathf.Clamp(_sliderPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
            using (ZUI.Box(null, _sheet.boxes[_sliderPreviewBoxIndex]))
                _sliderPreviewValue = ZUI.Slider(_sliderPreviewValue, 0f, 1f, "Preview", def, null, GUILayout.ExpandWidth(true));
        }
        else
        {
            var bgRect = GUILayoutUtility.GetRect(1f, sliderH + 16f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bgRect, new Color(.13f, .13f, .15f, 1f));
            var sliderRect = new Rect(bgRect.x + 8f, bgRect.y + (bgRect.height - sliderH) * 0.5f,
                                      bgRect.width - 16f, sliderH);
            _sliderPreviewValue = ZUI.Slider(sliderRect, _sliderPreviewValue, 0f, 1f, "Preview", def);
        }

        GUILayout.Space(4f);

        // ── Layout ────────────────────────────────────────────────────────────
        if (InspectorSubheader("Layout", "slider_layout"))
        {
            EditorGUI.BeginChangeCheck();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Track Height", GUILayout.Width(k_LabelWidth));
            def.trackHeight = EditorGUILayout.Slider(def.trackHeight, 2f, 40f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Thumb Width", GUILayout.Width(k_LabelWidth));
            def.thumbWidth = EditorGUILayout.Slider(def.thumbWidth, 4f, 60f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Thumb Height", GUILayout.Width(k_LabelWidth));
            def.thumbHeight = EditorGUILayout.Slider(def.thumbHeight, 0f, 60f);
            EditorGUILayout.LabelField("(0 = full height)", EditorStyles.miniLabel, GUILayout.Width(90f));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show Value Field", GUILayout.Width(k_LabelWidth));
            def.showValueField = EditorGUILayout.Toggle(def.showValueField);
            GUILayout.EndHorizontal();
            if (def.showValueField)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Value Width", GUILayout.Width(k_LabelWidth));
                def.valueWidth = EditorGUILayout.Slider(def.valueWidth, 20f, 120f);
                GUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }

        GUILayout.Space(2f);

        // ── Track (empty / right) ─────────────────────────────────────────────
        if (InspectorSubheader("Track (Empty)", "slider_track"))
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.Space(2f);
            if (def.track == null) def.track = new ZUIBoxDef("Track",
                new Color(.14f, .14f, .18f, 1f), new Color(.88f,.88f,.88f,1f),
                new Color(1f,1f,1f,.08f), 1f, 0, 0);
            DrawInlineBoxDef(def.track, "slider_track");
            if (EditorGUI.EndChangeCheck()) { def.track.Invalidate(); changed = true; }
        }

        GUILayout.Space(2f);

        // ── Track fill ────────────────────────────────────────────────────────
        if (InspectorSubheader("Track Fill", "slider_trackfill"))
        {
            EditorGUI.BeginChangeCheck();
            if (def.trackFill == null) def.trackFill = new ZUIBoxDef("TrackFill",
                new Color(.20f,.38f,.55f,1f), new Color(.88f,.88f,.88f,1f),
                new Color(.30f,.60f,1f,.30f), 1f, 0, 0);
            DrawInlineBoxDef(def.trackFill, "slider_trackfill");
            if (EditorGUI.EndChangeCheck()) { def.trackFill.Invalidate(); changed = true; }
        }

        GUILayout.Space(2f);

        // ── Thumb ─────────────────────────────────────────────────────────────
        if (InspectorSubheader("Thumb", "slider_thumb_header"))
        {
            // Normal | MinMax mode selector
            int newMode = GUILayout.Toolbar(_sliderThumbModeTab, new[] { "Normal", "Min / Max" }, EditorStyles.miniButton);
            if (newMode != _sliderThumbModeTab)
            {
                _sliderThumbModeTab = newMode;
                // Enabling MinMax: seed thumbMax from thumb
                if (newMode == 1 && def.thumbMax == null)
                {
                    var src = def.thumb;
                    def.thumbMax = new ZUIButtonDef("ThumbMax",
                        src?.normal.colorA ?? new Color(.30f,.54f,.78f,1f),
                        src?.hover.colorA  ?? new Color(.40f,.64f,.90f,1f),
                        src?.active.colorA ?? new Color(.20f,.40f,.62f,1f),
                        src?.textColor     ?? new Color(.92f,.96f,1f,1f));
                    changed = true;
                }
                // Disabling MinMax: clear thumbMax
                if (newMode == 0 && def.thumbMax != null)
                {
                    def.thumbMax = null;
                    changed = true;
                }
            }
            // Keep mode in sync with data (e.g. loaded from asset)
            if (_sliderThumbModeTab == 0 && def.thumbMax != null) _sliderThumbModeTab = 1;

            GUILayout.Space(3f);

            if (_sliderThumbModeTab == 0)
            {
                // Single thumb
                if (def.thumb == null) def.thumb = new ZUIButtonDef("Thumb",
                    new Color(.30f,.54f,.78f,1f), new Color(.40f,.64f,.90f,1f),
                    new Color(.20f,.40f,.62f,1f), new Color(.92f,.96f,1f,1f));
                EditorGUI.BeginChangeCheck();
                DrawInlineButtonDefFlat(def.thumb, ref _sliderThumbMinState);
                if (EditorGUI.EndChangeCheck()) { def.thumb.Invalidate(); changed = true; }
            }
            else
            {
                // Min thumb
                if (InspectorSubheader("Min Thumb (left)", "slider_thumb_min"))
                {
                    if (def.thumb == null) def.thumb = new ZUIButtonDef("Thumb",
                        new Color(.30f,.54f,.78f,1f), new Color(.40f,.64f,.90f,1f),
                        new Color(.20f,.40f,.62f,1f), new Color(.92f,.96f,1f,1f));
                    EditorGUI.BeginChangeCheck();
                    DrawInlineButtonDefFlat(def.thumb, ref _sliderThumbMinState);
                    if (EditorGUI.EndChangeCheck()) { def.thumb.Invalidate(); changed = true; }
                }
                GUILayout.Space(2f);
                // Max thumb
                if (InspectorSubheader("Max Thumb (right)", "slider_thumb_max"))
                {
                    if (def.thumbMax == null) def.thumbMax = new ZUIButtonDef("ThumbMax",
                        def.thumb?.normal.colorA ?? new Color(.30f,.54f,.78f,1f),
                        def.thumb?.hover.colorA  ?? new Color(.40f,.64f,.90f,1f),
                        def.thumb?.active.colorA ?? new Color(.20f,.40f,.62f,1f),
                        def.thumb?.textColor     ?? new Color(.92f,.96f,1f,1f));
                    EditorGUI.BeginChangeCheck();
                    DrawInlineButtonDefFlat(def.thumbMax, ref _sliderThumbMaxState);
                    if (EditorGUI.EndChangeCheck()) { def.thumbMax.Invalidate(); changed = true; }
                }
            }
        }

        GUILayout.Space(2f);

        // ── Label text ────────────────────────────────────────────────────────
        if (InspectorSubheader("Label Text", "slider_labeltext"))
        {
            EditorGUI.BeginChangeCheck();
            DrawTextRow(def.labelText);
            DrawShadowTextRow(def.labelText);
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }

        GUILayout.Space(2f);

        // ── Value text ────────────────────────────────────────────────────────
        if (InspectorSubheader("Value Text", "slider_valuetext"))
        {
            EditorGUI.BeginChangeCheck();
            DrawTextRow(def.valueText);
            DrawShadowTextRow(def.valueText);
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }

        if (changed) { EditorUtility.SetDirty(_sheet); RepaintShowcase(); }
        GUILayout.Space(10f);
        DrawExportPathField();
    }

    // Draws a ZUIBoxDef inline (background, border, shape — no padding/margin, no title).
    static void DrawCompactBorderRow(ZUIBorderDef border)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Border Width", GUILayout.Width(k_LabelWidth));
        border.width = EditorGUILayout.Slider(border.width, 0f, 4f);
        if (border.width > 0f)
        {
            GUILayout.Space(4f);
            EditorGUILayout.LabelField("Color", GUILayout.Width(38f));
            border.gradient.colorA = EditorGUILayout.ColorField(GUIContent.none, border.gradient.colorA, true, true, false, GUILayout.Width(50f));
        }
        GUILayout.EndHorizontal();
    }

    void DrawInlineBoxDef(ZUIBoxDef box, string keyPrefix)
    {
        Action inv = () => { box.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };

        DrawGradientField("Fill", box.background, inv);

        EditorGUI.BeginChangeCheck();
        DrawCompactBorderRow(box.border);
        if (EditorGUI.EndChangeCheck()) { box.border.gradient.Invalidate(); inv(); }

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Corner Radius", GUILayout.Width(k_LabelWidth));
        box.shape.cornerRadius = EditorGUILayout.IntSlider(box.shape.cornerRadius, 0, 24);
        GUILayout.EndHorizontal();
    }

    // Draws a ZUIButtonDef inline for the slider thumb — sub-sections use a visually
    // indented style (lighter bg, smaller height) to differentiate from parent sections.
    void DrawInlineButtonDef(ZUIButtonDef btn, string keyPrefix)
    {
        Action inv = () => { btn.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };

        if (InspectorSubsection("Normal BG", keyPrefix + "_norm"))
            DrawGradientField("Fill", btn.normal, inv);

        if (InspectorSubsection("Hover BG", keyPrefix + "_hov"))
        {
            btn.hoverBgOverride = EditorGUILayout.Toggle("Override", btn.hoverBgOverride);
            if (btn.hoverBgOverride)
                DrawGradientField("Fill", btn.hover, inv);
        }

        if (InspectorSubsection("Active BG", keyPrefix + "_act"))
        {
            btn.activeBgOverride = EditorGUILayout.Toggle("Override", btn.activeBgOverride);
            if (btn.activeBgOverride)
                DrawGradientField("Fill", btn.active, inv);
        }

        if (InspectorSubsection("Shape", keyPrefix + "_shape"))
        {
            btn.cornerRadius = EditorGUILayout.IntSlider("Corner Radius", btn.cornerRadius, 0, 24);
            if (btn.cornerRadius > 0)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Round corners", GUILayout.Width(k_LabelWidth));
                btn.roundTL = EditorGUILayout.ToggleLeft("TL", btn.roundTL, GUILayout.Width(34f));
                btn.roundTR = EditorGUILayout.ToggleLeft("TR", btn.roundTR, GUILayout.Width(34f));
                btn.roundBL = EditorGUILayout.ToggleLeft("BL", btn.roundBL, GUILayout.Width(34f));
                btn.roundBR = EditorGUILayout.ToggleLeft("BR", btn.roundBR, GUILayout.Width(34f));
                GUILayout.EndHorizontal();
            }
        }

        if (InspectorSubsection("Border", keyPrefix + "_bdr"))
        {
            EditorGUI.BeginChangeCheck();
            DrawCompactBorderRow(btn.border);
            if (EditorGUI.EndChangeCheck()) { btn.border.gradient.Invalidate(); inv(); }
        }
    }

    // Flat (no foldout subsections) button def editor used inside slider thumb sections.
    // States shown via a Normal|Hover|Active tab. Shape+Border shown inline.
    void DrawInlineButtonDefFlat(ZUIButtonDef btn, ref int stateTab)
    {
        Action inv = () => { btn.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); };

        // State tab
        stateTab = GUILayout.Toolbar(stateTab, new[] { "Normal", "Hover", "Active" }, EditorStyles.miniButton);
        GUILayout.Space(2f);

        if (stateTab == 0)
        {
            DrawGradientField("Fill", btn.normal, inv);
        }
        else if (stateTab == 1)
        {
            btn.hoverBgOverride = EditorGUILayout.Toggle("Override", btn.hoverBgOverride);
            if (btn.hoverBgOverride) DrawGradientField("Fill", btn.hover, inv);
        }
        else
        {
            btn.activeBgOverride = EditorGUILayout.Toggle("Override", btn.activeBgOverride);
            if (btn.activeBgOverride) DrawGradientField("Fill", btn.active, inv);
        }

        GUILayout.Space(2f);

        // Shape — inline single row
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Corner Radius", GUILayout.Width(k_LabelWidth));
        btn.cornerRadius = EditorGUILayout.IntSlider(btn.cornerRadius, 0, 24);
        GUILayout.EndHorizontal();

        if (btn.cornerRadius > 0)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Round corners", GUILayout.Width(k_LabelWidth));
            btn.roundTL = EditorGUILayout.ToggleLeft("TL", btn.roundTL, GUILayout.Width(34f));
            btn.roundTR = EditorGUILayout.ToggleLeft("TR", btn.roundTR, GUILayout.Width(34f));
            btn.roundBL = EditorGUILayout.ToggleLeft("BL", btn.roundBL, GUILayout.Width(34f));
            btn.roundBR = EditorGUILayout.ToggleLeft("BR", btn.roundBR, GUILayout.Width(34f));
            GUILayout.EndHorizontal();
        }

        // Border — inline
        EditorGUI.BeginChangeCheck();
        DrawCompactBorderRow(btn.border);
        if (EditorGUI.EndChangeCheck()) { btn.border.gradient.Invalidate(); inv(); }
    }

    // A visually distinct sub-section header — smaller, indented, lighter colour.
    // Used inside parent InspectorSubheader blocks to show hierarchy.
    bool InspectorSubsection(string title, string key)
    {
        bool expanded = GetFoldout(key);
        GUILayout.Space(1f);
        var rect = GUILayoutUtility.GetRect(1f, 22f, GUILayout.ExpandWidth(true));
        // Slightly lighter and taller indent than a normal subheader
        EditorGUI.DrawRect(new Rect(rect.x + 8f, rect.y, rect.width - 8f, rect.height),
                           new Color(.18f, .18f, .22f, 1f));
        float cy = rect.y + (rect.height - 12f) * 0.5f;
        EditorGUI.LabelField(new Rect(rect.x + 16f, cy, 12f,              12f), expanded ? "▾" : "▸", EditorStyles.miniLabel);
        EditorGUI.LabelField(new Rect(rect.x + 28f, cy, rect.width - 32f, 12f), title,                EditorStyles.miniLabel);
        if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
        {
            expanded = !expanded;
            SetFoldout(key, expanded);
            Event.current.Use();
            Repaint();
        }
        GUILayout.Space(2f);
        return expanded;
    }
}

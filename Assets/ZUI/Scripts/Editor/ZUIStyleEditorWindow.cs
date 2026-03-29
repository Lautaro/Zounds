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
        int previewMode = GUILayout.Toolbar(_previewIsToggleMode ? 1 : 0,
            new[] { "Button", "Toggle" }, EditorStyles.miniButton);
        _previewIsToggleMode = previewMode == 1;
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
        if (InspectorSubheader("Icon", "btn_icon"))
        {
            EditorGUI.BeginChangeCheck();
            def.icon = (Texture2D)EditorGUILayout.ObjectField("Texture", def.icon, typeof(Texture2D), false);
            if (def.icon != null)
            {
                def.iconPlacement = (ZIconPlacement)EditorGUILayout.EnumPopup("Placement", def.iconPlacement);
                def.iconSize      = EditorGUILayout.IntSlider("Icon Size", def.iconSize, 8, 32);
            }
            if (EditorGUI.EndChangeCheck()) { changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }
        }

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
        if (InspectorSubheader("Hover Animation", "btn_hoveranim"))
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enable", GUILayout.Width(k_LabelWidth));
            def.hoverAnimEnabled = EditorGUILayout.Toggle(def.hoverAnimEnabled, GUILayout.Width(20f));
            GUILayout.EndHorizontal();
            if (def.hoverAnimEnabled)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("In / Out (s)", GUILayout.Width(k_LabelWidth));
                def.hoverInDuration  = Mathf.Max(0.01f, EditorGUILayout.FloatField(def.hoverInDuration,  GUILayout.Width(50f)));
                EditorGUILayout.LabelField("/", GUILayout.Width(12f));
                def.hoverOutDuration = Mathf.Max(0.01f, EditorGUILayout.FloatField(def.hoverOutDuration, GUILayout.Width(50f)));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Tint Color", GUILayout.Width(k_LabelWidth));
                def.hoverAnimFillColor = EditorGUILayout.ColorField(GUIContent.none, def.hoverAnimFillColor, true, true, false, GUILayout.Width(90f));
                GUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck()) { changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }
        }

        GUILayout.Space(4f);
        if (InspectorSubheader("Click Animation", "btn_clickanim"))
        {
            EditorGUI.BeginChangeCheck();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enable", GUILayout.Width(k_LabelWidth));
            def.clickAnimEnabled = EditorGUILayout.Toggle(def.clickAnimEnabled, GUILayout.Width(20f));
            GUILayout.EndHorizontal();
            if (def.clickAnimEnabled)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Duration (s)", GUILayout.Width(k_LabelWidth));
                def.clickDuration = Mathf.Max(0.01f, EditorGUILayout.FloatField(def.clickDuration, GUILayout.Width(50f)));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Flash Color", GUILayout.Width(k_LabelWidth));
                def.clickAnimFillColor = EditorGUILayout.ColorField(GUIContent.none, def.clickAnimFillColor, true, true, false, GUILayout.Width(90f));
                GUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck()) { changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); }
        }

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
            () => _clipBtnBorder = (def.borderColor, def.borderColorEnd, def.isBorderGradient, def.borderWidth, def.useGlobalBorder),
            () => { if (_clipBtnBorder.HasValue) {
                        var c = _clipBtnBorder.Value;
                        def.borderColor = c.c1; def.borderColorEnd = c.c2;
                        def.isBorderGradient = c.dual; def.borderWidth = c.w;
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
            using (new EditorGUI.DisabledGroupScope(def.useGlobalText))
            {
                EditorGUI.BeginChangeCheck();
                string newRef = DrawTextStyleRefPopup(def.textStyleId);
                if (EditorGUI.EndChangeCheck()) { def.textStyleId = newRef; def.Invalidate(); changed = true; }
            }
            bool txtLocked = def.useGlobalText || !string.IsNullOrEmpty(def.textStyleId);
            var  txtSource = def.useGlobalText      ? (ZUI.ActiveSheet?.globalButton?.text ?? def.text)
                           : !string.IsNullOrEmpty(def.textStyleId) ? (ZUI.ActiveSheet?.FindText(def.textStyleId)?.text ?? def.text)
                           : def.text;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(txtLocked))
            {
                DrawTextRow(txtSource);
                DrawShadowTextRow(txtSource);
            }
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
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pad (text)", GUILayout.Width(k_LabelWidth - 2f));
            {
                var gp = def.useGlobalPadding ? ZUI.ActiveSheet?.globalButton : null;
                int dispH = gp != null ? gp.padH : def.padH;
                int dispV = gp != null ? gp.padV : def.padV;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalPadding))
                {
                    float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
                    int newH = Mathf.Max(0, EditorGUILayout.IntField("H", dispH, GUILayout.Width(46f)));
                    int newV = Mathf.Max(0, EditorGUILayout.IntField("V", dispV, GUILayout.Width(46f)));
                    EditorGUIUtility.labelWidth = _lw;
                    if (!def.useGlobalPadding) { def.padH = newH; def.padV = newV; }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pad (icon)", GUILayout.Width(k_LabelWidth - 2f));
            {
                var gp = def.useGlobalPadding ? ZUI.ActiveSheet?.globalButton : null;
                int dispH = gp != null ? gp.iconPadH : def.iconPadH;
                int dispV = gp != null ? gp.iconPadV : def.iconPadV;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalPadding))
                {
                    float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
                    int newH = Mathf.Max(0, EditorGUILayout.IntField("H", dispH, GUILayout.Width(46f)));
                    int newV = Mathf.Max(0, EditorGUILayout.IntField("V", dispV, GUILayout.Width(46f)));
                    EditorGUIUtility.labelWidth = _lw;
                    if (!def.useGlobalPadding) { def.iconPadH = newH; def.iconPadV = newV; }
                }
            }
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }
        if (sizeGlobalNew != def.useGlobalPadding) { def.useGlobalPadding = sizeGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        if (InspectorSubheader("Background Shadow", "btn_n_shadow"))
        {
            EditorGUI.BeginChangeCheck();
            DrawBgShadowRow(def.bgShadowEnabled, def.bgShadowColor, def.bgShadowOffset, ref def.bgShadowColorRef, ref def.bgShadowColorSlot,
                out bool newEnabled, out Color newColor, out Vector2 newOffset);
            if (EditorGUI.EndChangeCheck())
            {
                def.bgShadowEnabled = newEnabled;
                def.bgShadowColor   = newColor;
                def.bgShadowOffset  = newOffset;
                changed = true;
            }
        }

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
            def.hoverBorderColor = def.borderColor; def.hoverBorderColorEnd = def.borderColorEnd;
            def.hoverIsBorderGrad = def.isBorderGradient; def.hoverBorderWidth = def.borderWidth;
            changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint();
        };
        bool bdrExp = InspectorSubheaderWithOverrideCopyPaste("Border", def.hoverBorderOverride, out hoverBdrOvNew,
            () => _clipBtnHoverBorder = (def.hoverBorderColor, def.hoverBorderColorEnd, def.hoverIsBorderGrad, def.hoverBorderWidth),
            () => { if (_clipBtnHoverBorder.HasValue) { var c = _clipBtnHoverBorder.Value; def.hoverBorderColor = c.c1; def.hoverBorderColorEnd = c.c2; def.hoverIsBorderGrad = c.dual; def.hoverBorderWidth = c.w; changed = true; } },
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
                    DrawBorderReadOnlyRow(def.borderColor, def.borderColorEnd, def.isBorderGradient, def.borderWidth);
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
            EditorGUI.BeginChangeCheck();
            string newHoverRef = DrawTextStyleRefPopup(def.hoverTextStyleId);
            if (EditorGUI.EndChangeCheck()) { def.hoverTextStyleId = newHoverRef; def.Invalidate(); changed = true; }

            bool hasHoverRef = !string.IsNullOrEmpty(def.hoverTextStyleId);
            EditorGUI.BeginChangeCheck();
            if (hasHoverRef)
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawTextRow(ZUI.ActiveSheet?.FindText(def.hoverTextStyleId)?.text ?? def.hoverText);
            }
            else if (def.hoverTextOverride)
                DrawTextRow(def.hoverText);
            else
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawTextRow(def.text);
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
            var (c1, c2, dual, bw) = def.GetHoverBorder();
            def.activeBorderColor = c1; def.activeBorderColorEnd = c2;
            def.activeIsBorderGrad = dual; def.activeBorderWidth = bw;
            changed = true; EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint();
        };
        bool bdrExp = InspectorSubheaderWithOverrideCopyPaste("Border", def.activeBorderOverride, out activeBdrOvNew,
            () => _clipBtnActiveBorder = (def.activeBorderColor, def.activeBorderColorEnd, def.activeIsBorderGrad, def.activeBorderWidth),
            () => { if (_clipBtnActiveBorder.HasValue) { var c = _clipBtnActiveBorder.Value; def.activeBorderColor = c.c1; def.activeBorderColorEnd = c.c2; def.activeIsBorderGrad = c.dual; def.activeBorderWidth = c.w; changed = true; } },
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
                var (bc1, bc2, bdual, bw) = def.GetHoverBorder();
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawBorderReadOnlyRow(bc1, bc2, bdual, bw);
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
            EditorGUI.BeginChangeCheck();
            string newActiveRef = DrawTextStyleRefPopup(def.activeTextStyleId);
            if (EditorGUI.EndChangeCheck()) { def.activeTextStyleId = newActiveRef; def.Invalidate(); changed = true; }

            bool hasActiveRef = !string.IsNullOrEmpty(def.activeTextStyleId);
            EditorGUI.BeginChangeCheck();
            if (hasActiveRef)
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawTextRow(ZUI.ActiveSheet?.FindText(def.activeTextStyleId)?.text ?? def.activeText);
            }
            else if (def.activeTextOverride)
                DrawTextRow(def.activeText);
            else
            {
                using (new EditorGUI.DisabledGroupScope(true))
                    DrawTextRow(def.GetHoverText());
            }
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }

        return changed;
    }

    void DrawHoverBorderRow(ZUIButtonDef def)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color A", GUILayout.Width(k_LabelWidth - 2f));
        def.hoverIsBorderGrad = GUILayout.Toggle(def.hoverIsBorderGrad,
            def.hoverIsBorderGrad ? "▾" : "▸", EditorStyles.miniButton, GUILayout.Width(20f));
        def.hoverBorderColor = EditorGUILayout.ColorField(GUIContent.none, def.hoverBorderColor, true, true, false, GUILayout.Width(90f));
        if (def.hoverIsBorderGrad)
            def.hoverBorderColorEnd = EditorGUILayout.ColorField(GUIContent.none, def.hoverBorderColorEnd, true, true, false, GUILayout.Width(90f));
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
          def.hoverBorderWidth = Mathf.Max(0f, EditorGUILayout.FloatField("W", def.hoverBorderWidth, GUILayout.Width(50f)));
          EditorGUIUtility.labelWidth = _lw; }
        PaletteSlotPopup(ref def.hoverBorderColorRef, ref def.hoverBorderColorSlot);
        if (def.hoverIsBorderGrad)
            PaletteSlotPopup(ref def.hoverBorderColorEndRef, ref def.hoverBorderColorEndSlot);
        GUILayout.EndHorizontal();
        if (def.hoverIsBorderGrad)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Top + Left = A  ·  Bottom + Right = B", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
    }

    void DrawActiveBorderRow(ZUIButtonDef def)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color A", GUILayout.Width(k_LabelWidth - 2f));
        def.activeIsBorderGrad = GUILayout.Toggle(def.activeIsBorderGrad,
            def.activeIsBorderGrad ? "▾" : "▸", EditorStyles.miniButton, GUILayout.Width(20f));
        def.activeBorderColor = EditorGUILayout.ColorField(GUIContent.none, def.activeBorderColor, true, true, false, GUILayout.Width(90f));
        if (def.activeIsBorderGrad)
            def.activeBorderColorEnd = EditorGUILayout.ColorField(GUIContent.none, def.activeBorderColorEnd, true, true, false, GUILayout.Width(90f));
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
          def.activeBorderWidth = Mathf.Max(0f, EditorGUILayout.FloatField("W", def.activeBorderWidth, GUILayout.Width(50f)));
          EditorGUIUtility.labelWidth = _lw; }
        PaletteSlotPopup(ref def.activeBorderColorRef, ref def.activeBorderColorSlot);
        if (def.activeIsBorderGrad)
            PaletteSlotPopup(ref def.activeBorderColorEndRef, ref def.activeBorderColorEndSlot);
        GUILayout.EndHorizontal();
        if (def.activeIsBorderGrad)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Top + Left = A  ·  Bottom + Right = B", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
    }

    static void DrawBorderReadOnlyRow(Color c1, Color c2, bool dual, float w)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color A", GUILayout.Width(k_LabelWidth - 2f));
        GUILayout.Toggle(dual, dual ? "▾" : "▸", EditorStyles.miniButton, GUILayout.Width(20f));
        EditorGUILayout.ColorField(GUIContent.none, c1, true, true, false);
        if (dual) EditorGUILayout.ColorField(GUIContent.none, c2, true, true, false);
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
          EditorGUILayout.FloatField("W", w, GUILayout.Width(50f));
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
            () => _clipBoxBorder = (def.borderColor, def.borderColorEnd, def.isBorderGradient, def.borderWidth, def.useGlobalBorder),
            () => { if (_clipBoxBorder.HasValue) {
                        var c = _clipBoxBorder.Value;
                        def.borderColor = c.c1; def.borderColorEnd = c.c2;
                        def.isBorderGradient = c.dual; def.borderWidth = c.w;
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
            using (new EditorGUI.DisabledGroupScope(def.useGlobalTitleText))
            {
                EditorGUI.BeginChangeCheck();
                string newRef = DrawTextStyleRefPopup(def.titleTextStyleId);
                if (EditorGUI.EndChangeCheck()) { def.titleTextStyleId = newRef; def.Invalidate(); changed = true; }
            }
            bool titleLocked = def.useGlobalTitleText || !string.IsNullOrEmpty(def.titleTextStyleId);
            var  titleSource = def.useGlobalTitleText         ? (ZUI.ActiveSheet?.globalBox?.titleText ?? def.titleText)
                             : !string.IsNullOrEmpty(def.titleTextStyleId) ? (ZUI.ActiveSheet?.FindText(def.titleTextStyleId)?.text ?? def.titleText)
                             : def.titleText;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(titleLocked))
            {
                DrawTextRow(titleSource);
                DrawShadowTextRow(titleSource);
            }
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
            using (new EditorGUI.DisabledGroupScope(def.useGlobalContentText))
            {
                EditorGUI.BeginChangeCheck();
                string newRef = DrawTextStyleRefPopup(def.contentTextStyleId);
                if (EditorGUI.EndChangeCheck()) { def.contentTextStyleId = newRef; def.Invalidate(); changed = true; }
            }
            bool contentLocked = def.useGlobalContentText || !string.IsNullOrEmpty(def.contentTextStyleId);
            var  contentSource = def.useGlobalContentText         ? (ZUI.ActiveSheet?.globalBox?.contentText ?? def.contentText)
                               : !string.IsNullOrEmpty(def.contentTextStyleId) ? (ZUI.ActiveSheet?.FindText(def.contentTextStyleId)?.text ?? def.contentText)
                               : def.contentText;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledGroupScope(contentLocked))
            {
                DrawTextRow(contentSource);
                DrawShadowTextRow(contentSource);
            }
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }
        if (boxContentGlobalNew != def.useGlobalContentText) { def.useGlobalContentText = boxContentGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        // ── Background Shadow ─────────────────────────────────────────────────
        if (InspectorSubheader("Background Shadow", "box_shadow"))
        {
            EditorGUI.BeginChangeCheck();
            DrawBgShadowRow(def.bgShadowEnabled, def.bgShadowColor, def.bgShadowOffset, ref def.bgShadowColorRef, ref def.bgShadowColorSlot,
                out bool newEnabled, out Color newColor, out Vector2 newOffset);
            if (EditorGUI.EndChangeCheck())
            {
                def.bgShadowEnabled = newEnabled;
                def.bgShadowColor   = newColor;
                def.bgShadowOffset  = newOffset;
                def.Invalidate(); changed = true;
            }
        }

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
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Padding H / V", GUILayout.Width(k_LabelWidth - 2f));
            {
                var gp = def.useGlobalPadding ? ZUI.ActiveSheet?.globalBox : null;
                int dispH = gp != null ? gp.padH : def.padH;
                int dispV = gp != null ? gp.padV : def.padV;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalPadding))
                {
                    float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
                    int newH = Mathf.Max(0, EditorGUILayout.IntField("H", dispH, GUILayout.Width(46f)));
                    int newV = Mathf.Max(0, EditorGUILayout.IntField("V", dispV, GUILayout.Width(46f)));
                    EditorGUIUtility.labelWidth = _lw;
                    if (!def.useGlobalPadding) { def.padH = newH; def.padV = newV; }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Margin H / V", GUILayout.Width(k_LabelWidth - 2f));
            {
                float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 12f;
                def.marginH = Mathf.Max(0, EditorGUILayout.IntField("H", def.marginH, GUILayout.Width(46f)));
                def.marginV = Mathf.Max(0, EditorGUILayout.IntField("V", def.marginV, GUILayout.Width(46f)));
                EditorGUIUtility.labelWidth = _lw;
            }
            GUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) { def.Invalidate(); changed = true; }
        }
        if (boxPadGlobalNew != def.useGlobalPadding) { def.useGlobalPadding = boxPadGlobalNew; def.Invalidate(); changed = true; }

        GUILayout.Space(2f);

        // ── Shape ────────────────────────────────────────────────────────────
        bool boxShapeGlobalNew;
        if (InspectorSubheaderWithCopyPasteAndGlobal("Shape",
            () => _clipBoxShape = (def.cornerRadius, def.useGlobalShape),
            () => { if (_clipBoxShape.HasValue)
                    { def.cornerRadius   = _clipBoxShape.Value.r;
                      def.useGlobalShape = _clipBoxShape.Value.useGlobal;
                      changed = true; } },
            _clipBoxShape.HasValue, def.useGlobalShape, out boxShapeGlobalNew))
        {
            EditorGUI.BeginChangeCheck();
            {
                var gs = def.useGlobalShape ? ZUI.ActiveSheet?.globalBox : null;
                int dispR = gs != null ? gs.cornerRadius : def.cornerRadius;
                using (new EditorGUI.DisabledGroupScope(def.useGlobalShape))
                {
                    int newR = EditorGUILayout.IntSlider("Corner Radius", dispR, 0, 24);
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
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Text", GUILayout.Width(k_LabelWidth));
            _previewTextContent = EditorGUILayout.TextField(_previewTextContent);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Background", GUILayout.Width(k_LabelWidth));
            _textPreviewBgMode = GUILayout.Toolbar(_textPreviewBgMode,
                new[] { "None", "Box", "Button" }, EditorStyles.miniButton);
            GUILayout.EndHorizontal();

            if (_textPreviewBgMode == 1 && _sheet.boxes.Count > 0)
            {
                _textPreviewBoxIndex = Mathf.Clamp(_textPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
                var names = new string[_sheet.boxes.Count];
                for (int i = 0; i < _sheet.boxes.Count; i++) names[i] = _sheet.boxes[i].name;
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Box Style", GUILayout.Width(k_LabelWidth));
                _textPreviewBoxIndex = EditorGUILayout.Popup(_textPreviewBoxIndex, names);
                GUILayout.EndHorizontal();
            }
            else if (_textPreviewBgMode == 2 && _sheet.buttons.Count > 0)
            {
                _textPreviewButtonIndex = Mathf.Clamp(_textPreviewButtonIndex, 0, _sheet.buttons.Count - 1);
                var names = new string[_sheet.buttons.Count];
                for (int i = 0; i < _sheet.buttons.Count; i++) names[i] = _sheet.buttons[i].name;
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Button Style", GUILayout.Width(k_LabelWidth));
                _textPreviewButtonIndex = EditorGUILayout.Popup(_textPreviewButtonIndex, names);
                GUILayout.EndHorizontal();
            }

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
        _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll);
        EditorGUIUtility.labelWidth = k_LabelWidth;
        bool changed = false;

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

        GUILayout.Space(12f);

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
            _sheet.globalBox.cornerRadius = EditorGUILayout.IntSlider("Corner Radius", _sheet.globalBox.cornerRadius, 0, 24);
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

        if (changed) { EditorUtility.SetDirty(_sheet); RepaintShowcase(); }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // ── Gradient field ────────────────────────────────────────────────────────

    // parentGrad / parentState: when set, adds "Revert to [parentState]" items in the context menu.
    bool DrawGradientField(string label, ZUIGradient g, Action onExternalPaste,
                           ZUIGradient parentGrad = null, string parentState = null)
    {
        bool changed = false;

        var fieldRect = EditorGUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(k_LabelWidth - 2f));

        EditorGUI.BeginChangeCheck();
        g.isGradient = GUILayout.Toggle(g.isGradient, g.isGradient ? "▾" : "▸",
            EditorStyles.miniButton, GUILayout.Width(20f));
        if (EditorGUI.EndChangeCheck()) { g.Invalidate(); changed = true; }

        EditorGUI.BeginChangeCheck();
        g.colorA = EditorGUILayout.ColorField(GUIContent.none, g.colorA, true, true, false, GUILayout.Width(90f));
        if (EditorGUI.EndChangeCheck()) { g.Invalidate(); changed = true; }

        if (g.isGradient)
        {
            EditorGUI.BeginChangeCheck();
            g.colorB = EditorGUILayout.ColorField(GUIContent.none, g.colorB, true, true, false, GUILayout.Width(90f));
            if (EditorGUI.EndChangeCheck()) { g.Invalidate(); changed = true; }
        }

        // Palette slot pickers for colorA (and colorB when gradient)
        if (PaletteSlotPopup(ref g.colorARef, ref g.colorASlot)) { g.Invalidate(); changed = true; }
        if (g.isGradient)
            if (PaletteSlotPopup(ref g.colorBRef, ref g.colorBSlot)) { g.Invalidate(); changed = true; }

        GUILayout.EndHorizontal();

        if (g.isGradient)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            // Mode radio: 0 = Linear, 1 = Radial, 2 = Fixed
            int mode    = g.isRadial ? 1 : (g.usePixelLength ? 2 : 0);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mode", GUILayout.Width(k_LabelWidth - 2f));
            int newMode = GUILayout.Toolbar(mode, new[] { "Linear", "Radial", "Fixed" }, EditorStyles.miniButton);
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
        // Color field with palette support
        if (ZUIColorField("Color", ref text.color, ref text.colorRef, ref text.colorSlot, k_LabelWidth - 2f, 90f))
        {
            // change detected inline; caller's BeginChangeCheck will catch it
        }
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(k_LabelWidth - 2f));
        EditorGUIUtility.labelWidth = 28f;
        text.fontSize  = Mathf.Max(0, EditorGUILayout.IntField("Size", text.fontSize, GUILayout.Width(62f)));
        EditorGUIUtility.labelWidth = prevLW;
        EditorGUILayout.LabelField("Style", GUILayout.Width(32f));
        text.fontStyle = (FontStyle)EditorGUILayout.EnumPopup(GUIContent.none, text.fontStyle);
        GUILayout.EndHorizontal();
    }

    void DrawShadowTextRow(ZUITextDef text)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Text Shadow", GUILayout.Width(k_LabelWidth - 2f));
        text.shadowEnabled = EditorGUILayout.Toggle(text.shadowEnabled, GUILayout.Width(16f));
        GUILayout.EndHorizontal();
        if (text.shadowEnabled)
        {
            ZUIColorField("Shadow Color", ref text.shadowColor, ref text.shadowColorRef, ref text.shadowColorSlot, k_LabelWidth - 2f, 90f);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(k_LabelWidth - 2f));
            float lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
            text.shadowOffset.x = EditorGUILayout.FloatField("X", text.shadowOffset.x, GUILayout.Width(52f));
            text.shadowOffset.y = EditorGUILayout.FloatField("Y", text.shadowOffset.y, GUILayout.Width(52f));
            EditorGUIUtility.labelWidth = lw;
            GUILayout.EndHorizontal();
        }
    }

    // Draws a "Text Style" popup — returns the selected style's name, or "" for inline.
    string DrawTextStyleRefPopup(string currentId)
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

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Text Style", GUILayout.Width(k_LabelWidth - 2f));
        int newIdx = EditorGUILayout.Popup(currentIdx, names);
        GUILayout.EndHorizontal();
        return newIdx == 0 ? "" : names[newIdx];
    }

    void DrawBgShadowRow(bool enabled, Color color, Vector2 offset, ref string paletteRef, ref ZUIPaletteSlot slot,
                         out bool outEnabled, out Color outColor, out Vector2 outOffset)
    {
        outEnabled = enabled; outColor = color; outOffset = offset;
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Enabled", GUILayout.Width(k_LabelWidth - 2f));
        outEnabled = EditorGUILayout.Toggle(enabled);
        GUILayout.EndHorizontal();
        if (outEnabled)
        {
            ZUIColorField("Color", ref outColor, ref paletteRef, ref slot, k_LabelWidth - 2f, 90f);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Offset", GUILayout.Width(k_LabelWidth - 2f));
            float lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
            outOffset.x = EditorGUILayout.FloatField("X", offset.x, GUILayout.Width(58f));
            outOffset.y = EditorGUILayout.FloatField("Y", offset.y, GUILayout.Width(58f));
            EditorGUIUtility.labelWidth = lw;
            GUILayout.EndHorizontal();
        }
    }

    // ── Border field ──────────────────────────────────────────────────────────

    void DrawBorderField(ZUIBoxDef def, Action onExternalPaste)
    {
        var fieldRect = EditorGUILayout.BeginVertical();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color A", GUILayout.Width(k_LabelWidth - 2f));
        def.isBorderGradient = GUILayout.Toggle(def.isBorderGradient,
            def.isBorderGradient ? "▾" : "▸", EditorStyles.miniButton, GUILayout.Width(20f));
        EditorGUI.BeginChangeCheck();
        def.borderColor = EditorGUILayout.ColorField(GUIContent.none, def.borderColor, true, true, false, GUILayout.Width(90f));
        if (EditorGUI.EndChangeCheck()) { /* change picked up by outer BeginChangeCheck */ }
        if (def.isBorderGradient)
            def.borderColorEnd = EditorGUILayout.ColorField(GUIContent.none, def.borderColorEnd, true, true, false, GUILayout.Width(90f));
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
          def.borderWidth = Mathf.Max(0f, EditorGUILayout.FloatField("W", def.borderWidth, GUILayout.Width(50f)));
          EditorGUIUtility.labelWidth = _lw; }
        // Palette slot pickers (box border)
        PaletteSlotPopup(ref def.borderColorRef, ref def.borderColorSlot);
        if (def.isBorderGradient)
            PaletteSlotPopup(ref def.borderColorEndRef, ref def.borderColorEndSlot);
        GUILayout.EndHorizontal();

        if (def.isBorderGradient)
        {
            EditorGUI.indentLevel++;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Angle°", GUILayout.Width(k_LabelWidth - 2f));
            def.borderGradientAngle = EditorGUILayout.Slider(def.borderGradientAngle, 0f, 360f);
            GUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Top + Left = A  ·  Bottom + Right = B", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        // Right-click context menu
        if (Event.current.type == EventType.ContextClick && fieldRect.Contains(Event.current.mousePosition))
        {
            var capturedDef = def;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Border"), false, () =>
                _clipBoxBorder = (capturedDef.borderColor, capturedDef.borderColorEnd,
                                  capturedDef.isBorderGradient, capturedDef.borderWidth, capturedDef.useGlobalBorder));
            if (_clipBoxBorder.HasValue)
                menu.AddItem(new GUIContent("Paste Border"), false, () =>
                {
                    var c = _clipBoxBorder.Value;
                    capturedDef.borderColor      = c.c1;
                    capturedDef.borderColorEnd   = c.c2;
                    capturedDef.isBorderGradient = c.dual;
                    capturedDef.borderWidth      = c.w;
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

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Color A", GUILayout.Width(k_LabelWidth - 2f));
        def.isBorderGradient = GUILayout.Toggle(def.isBorderGradient,
            def.isBorderGradient ? "▾" : "▸", EditorStyles.miniButton, GUILayout.Width(20f));
        def.borderColor = EditorGUILayout.ColorField(GUIContent.none, def.borderColor, true, true, false, GUILayout.Width(90f));
        if (def.isBorderGradient)
            def.borderColorEnd = EditorGUILayout.ColorField(GUIContent.none, def.borderColorEnd, true, true, false, GUILayout.Width(90f));
        { float _lw = EditorGUIUtility.labelWidth; EditorGUIUtility.labelWidth = 14f;
          def.borderWidth = Mathf.Max(0f, EditorGUILayout.FloatField("W", def.borderWidth, GUILayout.Width(50f)));
          EditorGUIUtility.labelWidth = _lw; }
        // Palette slot pickers (button border)
        PaletteSlotPopup(ref def.borderColorRef, ref def.borderColorSlot);
        if (def.isBorderGradient)
            PaletteSlotPopup(ref def.borderColorEndRef, ref def.borderColorEndSlot);
        GUILayout.EndHorizontal();

        if (def.isBorderGradient)
        {
            EditorGUI.indentLevel++;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Angle°", GUILayout.Width(k_LabelWidth - 2f));
            def.borderGradientAngle = EditorGUILayout.Slider(def.borderGradientAngle, 0f, 360f);
            GUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Top + Left = A  ·  Bottom + Right = B", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        if (Event.current.type == EventType.ContextClick && fieldRect.Contains(Event.current.mousePosition))
        {
            var capturedDef = def;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Copy Border"), false, () =>
                _clipBtnBorder = (capturedDef.borderColor, capturedDef.borderColorEnd,
                                  capturedDef.isBorderGradient, capturedDef.borderWidth, capturedDef.useGlobalBorder));
            if (_clipBtnBorder.HasValue)
                menu.AddItem(new GUIContent("Paste Border"), false, () =>
                {
                    var c = _clipBtnBorder.Value;
                    capturedDef.borderColor      = c.c1;
                    capturedDef.borderColorEnd   = c.c2;
                    capturedDef.isBorderGradient = c.dual;
                    capturedDef.borderWidth      = c.w;
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
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Label", GUILayout.Width(k_LabelWidth));
        _previewButtonText = EditorGUILayout.TextField(_previewButtonText);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Background", GUILayout.Width(k_LabelWidth));
        _buttonPreviewBgMode = GUILayout.Toolbar(_buttonPreviewBgMode,
            new[] { "None", "Box" }, EditorStyles.miniButton);
        GUILayout.EndHorizontal();

        if (_buttonPreviewBgMode == 1 && _sheet.boxes.Count > 0)
        {
            _buttonPreviewBoxIndex = Mathf.Clamp(_buttonPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
            var names = new string[_sheet.boxes.Count];
            for (int i = 0; i < _sheet.boxes.Count; i++) names[i] = _sheet.boxes[i].name;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Box Style", GUILayout.Width(k_LabelWidth));
            _buttonPreviewBoxIndex = EditorGUILayout.Popup(_buttonPreviewBoxIndex, names);
            GUILayout.EndHorizontal();
        }

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
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Label", GUILayout.Width(k_LabelWidth));
        _previewButtonText = EditorGUILayout.TextField(_previewButtonText);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Background", GUILayout.Width(k_LabelWidth));
        _buttonPreviewBgMode = GUILayout.Toolbar(_buttonPreviewBgMode,
            new[] { "None", "Box" }, EditorStyles.miniButton);
        GUILayout.EndHorizontal();

        if (_buttonPreviewBgMode == 1 && _sheet.boxes.Count > 0)
        {
            _buttonPreviewBoxIndex = Mathf.Clamp(_buttonPreviewBoxIndex, 0, _sheet.boxes.Count - 1);
            var names = new string[_sheet.boxes.Count];
            for (int i = 0; i < _sheet.boxes.Count; i++) names[i] = _sheet.boxes[i].name;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Box Style", GUILayout.Width(k_LabelWidth));
            _buttonPreviewBoxIndex = EditorGUILayout.Popup(_buttonPreviewBoxIndex, names);
            GUILayout.EndHorizontal();
        }

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
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Title", GUILayout.Width(k_LabelWidth));
        _previewBoxTitle = EditorGUILayout.TextField(_previewBoxTitle);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Content", GUILayout.Width(k_LabelWidth));
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
            if (b.borderWidth > 0f || b.padH != 10 || b.padV != 3)
            {
                sb.AppendLine();
                sb.AppendLine("        {");
                if (b.borderWidth > 0f)
                {
                    sb.AppendLine($"            borderColor      = {C(b.borderColor)},");
                    sb.AppendLine($"            borderColorEnd   = {C(b.borderColorEnd)},");
                    sb.AppendLine($"            isBorderGradient = {(b.isBorderGradient ? "true" : "false")},");
                    sb.AppendLine($"            borderWidth      = {b.borderWidth:F1}f,");
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
            sb.AppendLine($"            borderColor: {C(x.borderColor)},");
            sb.AppendLine($"            borderWidth: {x.borderWidth:F1}f,");
            sb.AppendLine($"            padH:        {x.padH},");
            sb.AppendLine($"            padV:        {x.padV}");
            sb.Append    ("        )");
            if (x.cornerRadius > 0 || x.borderColorEnd.a > 0f && x.isBorderGradient)
            {
                sb.AppendLine();
                sb.AppendLine("        {");
                if (x.cornerRadius > 0)
                    sb.AppendLine($"            cornerRadius     = {x.cornerRadius},");
                if (x.isBorderGradient)
                {
                    sb.AppendLine($"            borderColorEnd   = {C(x.borderColorEnd)},");
                    sb.AppendLine($"            isBorderGradient = true,");
                }
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
        borderColor      = src.borderColor,      borderColorEnd   = src.borderColorEnd,
        isBorderGradient = src.isBorderGradient, borderWidth      = src.borderWidth,
        cornerRadius     = src.cornerRadius,     padH             = src.padH,   padV = src.padV,
        roundTL = src.roundTL, roundTR = src.roundTR, roundBL = src.roundBL, roundBR = src.roundBR,
        useGlobalShape   = src.useGlobalShape,   useGlobalPadding = src.useGlobalPadding,
        useGlobalBorder  = src.useGlobalBorder,
        // Hover
        hoverBgOverride     = src.hoverBgOverride,     hover            = src.hover.Clone(),
        hoverTextOverride   = src.hoverTextOverride,   hoverText        = new ZUITextDef(src.hoverText.color) { fontSize = src.hoverText.fontSize, fontStyle = src.hoverText.fontStyle },
        hoverBorderOverride = src.hoverBorderOverride, hoverBorderColor = src.hoverBorderColor,
        hoverBorderColorEnd = src.hoverBorderColorEnd, hoverIsBorderGrad = src.hoverIsBorderGrad, hoverBorderWidth = src.hoverBorderWidth,
        // Active
        activeBgOverride     = src.activeBgOverride,     active            = src.active.Clone(),
        activeTextOverride   = src.activeTextOverride,   activeText        = new ZUITextDef(src.activeText.color) { fontSize = src.activeText.fontSize, fontStyle = src.activeText.fontStyle },
        activeBorderOverride = src.activeBorderOverride, activeBorderColor = src.activeBorderColor,
        activeBorderColorEnd = src.activeBorderColorEnd, activeIsBorderGrad = src.activeIsBorderGrad, activeBorderWidth = src.activeBorderWidth,
    };

    static void PasteButtonDef(ZUIButtonDef dst, ZUIButtonDef src)
    {
        // Normal
        PasteGrad(dst.normal, src.normal);
        dst.text.color = src.text.color; dst.text.fontSize = src.text.fontSize; dst.text.fontStyle = src.text.fontStyle;
        dst.borderColor = src.borderColor; dst.borderColorEnd = src.borderColorEnd;
        dst.isBorderGradient = src.isBorderGradient; dst.borderWidth = src.borderWidth;
        dst.cornerRadius = src.cornerRadius; dst.padH = src.padH; dst.padV = src.padV;
        dst.roundTL = src.roundTL; dst.roundTR = src.roundTR; dst.roundBL = src.roundBL; dst.roundBR = src.roundBR;
        dst.useGlobalShape = src.useGlobalShape; dst.useGlobalPadding = src.useGlobalPadding; dst.useGlobalBorder = src.useGlobalBorder;
        // Hover
        dst.hoverBgOverride = src.hoverBgOverride; PasteGrad(dst.hover, src.hover);
        dst.hoverTextOverride = src.hoverTextOverride; dst.hoverText.color = src.hoverText.color; dst.hoverText.fontSize = src.hoverText.fontSize; dst.hoverText.fontStyle = src.hoverText.fontStyle;
        dst.hoverBorderOverride = src.hoverBorderOverride; dst.hoverBorderColor = src.hoverBorderColor; dst.hoverBorderColorEnd = src.hoverBorderColorEnd; dst.hoverIsBorderGrad = src.hoverIsBorderGrad; dst.hoverBorderWidth = src.hoverBorderWidth;
        // Active
        dst.activeBgOverride = src.activeBgOverride; PasteGrad(dst.active, src.active);
        dst.activeTextOverride = src.activeTextOverride; dst.activeText.color = src.activeText.color; dst.activeText.fontSize = src.activeText.fontSize; dst.activeText.fontStyle = src.activeText.fontStyle;
        dst.activeBorderOverride = src.activeBorderOverride; dst.activeBorderColor = src.activeBorderColor; dst.activeBorderColorEnd = src.activeBorderColorEnd; dst.activeIsBorderGrad = src.activeIsBorderGrad; dst.activeBorderWidth = src.activeBorderWidth;
    }

    static ZUIBoxDef CopyBoxDef(ZUIBoxDef src) => new ZUIBoxDef
    {
        name             = src.name,
        background       = src.background.Clone(),
        titleText        = new ZUITextDef(src.titleText.color) { fontSize = src.titleText.fontSize, fontStyle = src.titleText.fontStyle },
        borderColor      = src.borderColor,
        borderColorEnd   = src.borderColorEnd,
        isBorderGradient = src.isBorderGradient,
        borderWidth      = src.borderWidth,
        cornerRadius     = src.cornerRadius,
        roundTL = src.roundTL, roundTR = src.roundTR, roundBL = src.roundBL, roundBR = src.roundBR,
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
        dst.borderColor      = src.borderColor;
        dst.borderColorEnd   = src.borderColorEnd;
        dst.isBorderGradient = src.isBorderGradient;
        dst.borderWidth      = src.borderWidth;
        dst.cornerRadius     = src.cornerRadius;
        dst.roundTL = src.roundTL; dst.roundTR = src.roundTR; dst.roundBL = src.roundBL; dst.roundBR = src.roundBR;
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

    // ── ZUIColorField ─────────────────────────────────────────────────────────
    // Draws: [label] [color swatch (read-only if ref set) OR editable ColorField] [palette popup] [P/G toggle if ref set]
    // Returns true if any value changed.

    bool ZUIColorField(string label, ref Color color, ref string paletteRef,
        float labelWidth = 82f, float colorWidth = 90f)
    {
        var dummy = ZUIPaletteSlot.Primary;
        return ZUIColorField(label, ref color, ref paletteRef, ref dummy, labelWidth, colorWidth);
    }

    bool ZUIColorField(string label, ref Color color, ref string paletteRef, ref ZUIPaletteSlot slot,
        float labelWidth = 82f, float colorWidth = 90f)
    {
        bool changed = false;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(labelWidth));

        bool hasRef = !string.IsNullOrEmpty(paletteRef);
        var  pal    = hasRef ? _sheet.FindPaletteColor(paletteRef) : null;
        Color resolved = pal != null ? pal.Resolve(slot) : color;

        if (hasRef)
        {
            var rect = GUILayoutUtility.GetRect(colorWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(colorWidth));
            EditorGUI.DrawRect(rect, resolved);
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } };
            EditorGUI.LabelField(rect, $" {paletteRef}", style);
            // Copy-value button: stamps resolved color into the field without keeping the ref
            if (GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(16f)))
            {
                color      = resolved;
                paletteRef = "";
                changed    = true;
            }
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            color = EditorGUILayout.ColorField(GUIContent.none, color, true, true, false, GUILayout.Width(colorWidth));
            if (EditorGUI.EndChangeCheck()) changed = true;
        }

        if (_sheet?.palette != null && _sheet.palette.Count > 0)
        {
            var names = new string[_sheet.palette.Count + 1];
            names[0] = "\u2014";
            for (int i = 0; i < _sheet.palette.Count; i++) names[i + 1] = _sheet.palette[i].name;
            string paletteRefVal = paletteRef;
            int current = hasRef ? (_sheet.palette.FindIndex(p => p.name == paletteRefVal) + 1) : 0;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup(current, names, GUILayout.Width(72f));
            if (EditorGUI.EndChangeCheck())
            {
                paletteRef = selected == 0 ? "" : _sheet.palette[selected - 1].name;
                changed = true;
            }

            if (hasRef)
            {
                EditorGUI.BeginChangeCheck();
                if (DrawSlotToggle(ref slot)) changed = true;
            }
        }

        EditorGUILayout.EndHorizontal();
        return changed;
    }

    // Draws [palette popup] [P/H/S slot buttons if ref set]. Used inline in border/gradient rows.
    bool PaletteSlotPopup(ref string paletteRef, ref ZUIPaletteSlot slot)
    {
        if (_sheet?.palette == null || _sheet.palette.Count == 0) return false;
        bool changed = false;
        var names = new string[_sheet.palette.Count + 1];
        names[0] = "\u2014";
        for (int i = 0; i < _sheet.palette.Count; i++) names[i + 1] = _sheet.palette[i].name;
        string refVal = paletteRef;
        bool hasRef = !string.IsNullOrEmpty(paletteRef);
        int current = hasRef ? (_sheet.palette.FindIndex(p => p.name == refVal) + 1) : 0;
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUILayout.Popup(current, names, GUILayout.Width(72f));
        if (EditorGUI.EndChangeCheck()) { paletteRef = selected == 0 ? "" : _sheet.palette[selected - 1].name; changed = true; }
        if (!string.IsNullOrEmpty(paletteRef))
        {
            if (DrawSlotToggle(ref slot)) changed = true;
        }
        return changed;
    }

    // Draws a P / H / S mini-button that cycles through ZUIPaletteSlot values. Returns true if changed.
    bool DrawSlotToggle(ref ZUIPaletteSlot slot)
    {
        string label = slot == ZUIPaletteSlot.Highlight ? "H" : slot == ZUIPaletteSlot.Shade ? "S" : "P";
        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(20f)))
        {
            slot = slot == ZUIPaletteSlot.Primary   ? ZUIPaletteSlot.Highlight
                 : slot == ZUIPaletteSlot.Highlight ? ZUIPaletteSlot.Shade
                 :                                    ZUIPaletteSlot.Primary;
            return true;
        }
        return false;
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
                    if (b.borderColorRef          == oldName) { b.borderColorRef          = entry.name; changed = true; }
                    if (b.borderColorEndRef       == oldName) { b.borderColorEndRef       = entry.name; changed = true; }
                    if (b.hoverBorderColorRef     == oldName) { b.hoverBorderColorRef     = entry.name; changed = true; }
                    if (b.hoverBorderColorEndRef  == oldName) { b.hoverBorderColorEndRef  = entry.name; changed = true; }
                    if (b.activeBorderColorRef    == oldName) { b.activeBorderColorRef    = entry.name; changed = true; }
                    if (b.activeBorderColorEndRef == oldName) { b.activeBorderColorEndRef = entry.name; changed = true; }
                    if (b.bgShadowColorRef        == oldName) { b.bgShadowColorRef        = entry.name; changed = true; }
                    if (b.text.colorRef           == oldName) { b.text.colorRef           = entry.name; changed = true; }
                    if (b.text.shadowColorRef     == oldName) { b.text.shadowColorRef     = entry.name; changed = true; }
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
                    if (b.borderColorRef       == oldName) { b.borderColorRef       = entry.name; changed = true; }
                    if (b.borderColorEndRef    == oldName) { b.borderColorEndRef    = entry.name; changed = true; }
                    if (b.bgShadowColorRef     == oldName) { b.bgShadowColorRef     = entry.name; changed = true; }
                    if (b.titleText.colorRef   == oldName) { b.titleText.colorRef   = entry.name; changed = true; }
                    if (b.titleText.shadowColorRef   == oldName) { b.titleText.shadowColorRef   = entry.name; changed = true; }
                    if (b.contentText.colorRef == oldName) { b.contentText.colorRef = entry.name; changed = true; }
                    if (b.contentText.shadowColorRef == oldName) { b.contentText.shadowColorRef = entry.name; changed = true; }
                    if (b.background.colorARef == oldName) { b.background.colorARef = entry.name; changed = true; }
                    if (b.background.colorBRef == oldName) { b.background.colorBRef = entry.name; changed = true; }
                    if (changed) b.Invalidate();
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
            if (t.text.colorRef == paletteName || t.text.shadowColorRef == paletteName) t.Invalidate();
    }

    bool ReferencesColor(ZUIButtonDef b, string name) =>
        b.borderColorRef == name || b.borderColorEndRef == name ||
        b.hoverBorderColorRef == name || b.hoverBorderColorEndRef == name ||
        b.activeBorderColorRef == name || b.activeBorderColorEndRef == name ||
        b.bgShadowColorRef == name ||
        b.text.colorRef == name || b.text.shadowColorRef == name ||
        b.normal.colorARef == name || b.normal.colorBRef == name ||
        b.hover.colorARef == name || b.hover.colorBRef == name ||
        b.active.colorARef == name || b.active.colorBRef == name;

    bool ReferencesColor(ZUIBoxDef b, string name) =>
        b.borderColorRef == name || b.borderColorEndRef == name ||
        b.bgShadowColorRef == name ||
        b.titleText.colorRef == name || b.titleText.shadowColorRef == name ||
        b.contentText.colorRef == name || b.contentText.shadowColorRef == name ||
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
                _sliderPreviewValue = ZUI.Slider(_sliderPreviewValue, 0f, 1f, "Preview", def, GUILayout.ExpandWidth(true));
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
    void DrawInlineBoxDef(ZUIBoxDef box, string keyPrefix)
    {
        DrawGradientField("Fill", box.background, () => { box.Invalidate(); EditorUtility.SetDirty(_sheet); RepaintShowcase(); Repaint(); });

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Border Color", GUILayout.Width(k_LabelWidth));
        box.borderColor = EditorGUILayout.ColorField(box.borderColor, GUILayout.Width(60f));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Border Width", GUILayout.Width(k_LabelWidth));
        box.borderWidth = EditorGUILayout.Slider(box.borderWidth, 0f, 4f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Corner Radius", GUILayout.Width(k_LabelWidth));
        box.cornerRadius = EditorGUILayout.IntSlider(box.cornerRadius, 0, 24);
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
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Border Color", GUILayout.Width(k_LabelWidth));
            btn.borderColor = EditorGUILayout.ColorField(btn.borderColor, GUILayout.Width(60f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Border Width", GUILayout.Width(k_LabelWidth));
            btn.borderWidth = EditorGUILayout.Slider(btn.borderWidth, 0f, 4f);
            GUILayout.EndHorizontal();
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
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Border", GUILayout.Width(k_LabelWidth));
        btn.borderColor = EditorGUILayout.ColorField(btn.borderColor, GUILayout.Width(60f));
        btn.borderWidth = EditorGUILayout.Slider(btn.borderWidth, 0f, 4f);
        GUILayout.EndHorizontal();
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

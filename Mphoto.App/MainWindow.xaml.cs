using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Mphoto.Core;
using Mphoto.Dialogs;

namespace Mphoto;

public partial class MainWindow : Window
{
    private enum Tool
    {
        Hand,
        Zoom,
        RectSelect,
        EllipseSelect,
        Lasso,
        Crop,
        Brush,
        Eraser,
        Fill,
        Shape,
        Text,
        Eyedropper
    }

    private enum Interaction
    {
        None,
        Stroke,
        Shape,
        Pan,
        RectSelect,
        EllipseSelect,
        Lasso,
        CropDrag
    }

    private Document _doc = null!;
    private readonly HistoryManager _history = new();
    private WriteableBitmap _bitmap = null!;
    private byte[] _displayPixels = Array.Empty<byte>();
    private byte[]? _editBackup;

    private Tool _tool = Tool.Brush;
    private Interaction _interaction = Interaction.None;
    private bool _uiReady;
    private bool _fitMode = true;

    private double _brushSize = 24;
    private double _brushHardness = 85;
    private double _brushOpacity = 100;
    private double _shapeStroke = 4;
    private double _shapeOpacity = 100;
    private double _fillTolerance = 16;
    private ShapeKind _shapeKind = ShapeKind.Rectangle;
    private bool _shapeFilled;

    private Color _primary = Colors.Black;
    private Color _secondary = Colors.White;

    private Point _startDoc;
    private Point _lastDocPoint;
    private Point _lastScreenPoint;
    private readonly List<Pt> _lassoPoints = new();
    private readonly List<LayerEntry> _layerEntries = new();

    private Layer? _strokeLayer;
    private uint _strokeColor;
    private bool _strokeErase;
    private int _strokeMinX;
    private int _strokeMinY;
    private int _strokeMaxX;
    private int _strokeMaxY;

    private bool _cropPending;
    private int _dirtyBaseline;
    private bool _rebuildingLayers;
    private bool _rebuildingHistory;
    private bool _updatingLayerUi;

    public MainWindow()
    {
        InitializeComponent();
        _uiReady = true;

        BuildBlendBox();
        BuildQuickPalette();

        Canvas.MouseDown += Canvas_MouseDown;
        Canvas.MouseMove += Canvas_MouseMove;
        Canvas.MouseUp += Canvas_MouseUp;
        Canvas.MouseWheel += Canvas_MouseWheel;
        Canvas.MouseLeave += Canvas_MouseLeave;
        StateChanged += (_, _) => UpdateWindowState();
        Canvas.ViewChanged += (_, _) => UpdateZoomText();
        Canvas.ViewportChanged += (_, _) =>
        {
            if (_fitMode)
            {
                Canvas.Fit(Canvas.Viewport);
                UpdateZoomText();
            }
        };

        UpdateColorSwatches();
        UpdateBrushLabels();
        UpdateShapeLabels();

        LoadDocument(new Document(1600, 1000, "Sin título"), null);
        SetTool(Tool.Brush);
        Loaded += (_, _) =>
        {
            _fitMode = true;
            Canvas.Fit(Canvas.Viewport);
            UpdateZoomText();
            Canvas.Focus();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        UpdateWindowState();
    }

    private void TitleArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void WindowClose_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void UpdateWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaxButton.Content = maximized ? "" : "";
        MaxButton.ToolTip = maximized ? "Restaurar" : "Maximizar";
        RootBorder.Margin = maximized ? new Thickness(7) : new Thickness(0);
    }

    // ================= Documento =================

    private void LoadDocument(Document document, string? path)
    {
        _doc = document;
        _doc.FilePath = path;
        _history.Clear();
        _cropPending = false;
        _interaction = Interaction.None;
        _strokeLayer = null;
        _editBackup = null;

        Canvas.CropRect = null;
        Canvas.LiveGeometry = null;
        Canvas.ShapeGeometry = null;
        Canvas.BrushCursor = null;
        MenuApplyCrop.IsEnabled = false;

        CreateDisplayBitmap();
        RefreshDisplay();

        Canvas.SetDocumentSize(_doc.Width, _doc.Height);
        Canvas.SetSelection(_doc.Selection);

        _doc.IsDirty = false;
        _dirtyBaseline = 0;
        _fitMode = true;
        Canvas.Fit(Canvas.Viewport);
        UpdateZoomText();

        RefreshLayerList();
        RefreshHistory();
        UpdateHistoryUi();
        UpdateTitle();
        UpdateStatus();
    }

    private void CreateDisplayBitmap()
    {
        _displayPixels = new byte[_doc.Width * _doc.Height * 4];
        _bitmap = new WriteableBitmap(_doc.Width, _doc.Height, 96, 96, PixelFormats.Bgra32, null);
        Canvas.Image = _bitmap;
        _editBackup = null;
    }

    private void RefreshDisplay()
    {
        Compositor.CompositeRegion(_doc, 0, 0, _doc.Width, _doc.Height, _displayPixels);
        _bitmap.WritePixels(new Int32Rect(0, 0, _doc.Width, _doc.Height), _displayPixels, _doc.Width * 4, 0);
        Canvas.Refresh();
    }

    private void RefreshRegion(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var x0 = Math.Clamp(x, 0, _doc.Width);
        var y0 = Math.Clamp(y, 0, _doc.Height);
        var x1 = Math.Clamp(x + width, 0, _doc.Width);
        var y1 = Math.Clamp(y + height, 0, _doc.Height);
        var w = x1 - x0;
        var h = y1 - y0;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        Compositor.CompositeRegion(_doc, x0, y0, w, h, _displayPixels);
        _bitmap.WritePixels(new Int32Rect(x0, y0, w, h), _displayPixels, _doc.Width * 4,
            (y0 * _doc.Width + x0) * 4);
        Canvas.Refresh();
    }

    private void UpdateTitle()
    {
        if (_doc == null)
        {
            return;
        }

        var star = _doc.IsDirty ? " *" : string.Empty;
        Title = $"Mphoto — {_doc.Name}{star}";
        TitleText.Text = $"Mphoto — {_doc.Name}{star}  ·  {_doc.Width} × {_doc.Height}";
    }

    private void UpdateStatus()
    {
        DocSizeText.Text = $"{_doc.Width} × {_doc.Height} px · {_doc.Layers.Count} capa{( _doc.Layers.Count == 1 ? "" : "s")}";
        HintText.Text = _tool switch
        {
            Tool.Hand => "Mano: arrastra para mover la vista",
            Tool.Zoom => "Zoom: clic para acercar · Alt+clic o botón derecho para alejar",
            Tool.RectSelect => "Selección rectangular: arrastra sobre el lienzo",
            Tool.EllipseSelect => "Selección elíptica: arrastra sobre el lienzo",
            Tool.Lasso => "Lazo: dibuja el contorno de la selección",
            Tool.Crop => "Recorte: arrastra para marcar la zona y pulsa Intro para aplicarla",
            Tool.Brush => "Pincel: arrastra para pintar · botón derecho usa el color secundario",
            Tool.Eraser => "Borrador: arrastra para borrar",
            Tool.Fill => "Relleno: clic para rellenar una zona de color uniforme",
            Tool.Shape => "Formas: arrastra para dibujar en la capa activa",
            Tool.Text => "Texto: haz clic en el lienzo para insertar texto",
            Tool.Eyedropper => "Cuentagotas: clic para tomar el color principal · botón derecho, el secundario",
            _ => string.Empty
        };
    }

    private void UpdateZoomText()
    {
        ZoomText.Text = $"{Math.Round(Canvas.Zoom * 100)} %";
    }

    // ================= Capas =================

    private void BuildBlendBox()
    {
        foreach (var mode in BlendModes.All)
        {
            BlendBox.Items.Add(new ComboBoxItem { Content = BlendModes.DisplayName(mode), Tag = mode });
        }

        BlendBox.SelectedIndex = 0;
    }

    private void RefreshLayerList()
    {
        _rebuildingLayers = true;

        _layerEntries.Clear();
        for (var i = _doc.Layers.Count - 1; i >= 0; i--)
        {
            _layerEntries.Add(new LayerEntry(_doc.Layers[i], CreateThumbnail(_doc.Layers[i])));
        }

        LayerList.ItemsSource = null;
        LayerList.ItemsSource = _layerEntries;
        LayerList.SelectedIndex = Math.Clamp(_doc.Layers.Count - 1 - _doc.ActiveLayerIndex, 0, _layerEntries.Count - 1);

        _rebuildingLayers = false;
        UpdateLayerControls();
    }

    private static BitmapSource CreateThumbnail(Layer layer)
    {
        const int maxWidth = 36;
        const int maxHeight = 34;
        var scale = Math.Min((double)maxWidth / layer.Width, (double)maxHeight / layer.Height);
        var width = Math.Max(1, (int)Math.Round(layer.Width * scale));
        var height = Math.Max(1, (int)Math.Round(layer.Height * scale));
        var bytes = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Min(layer.Height - 1, y * layer.Height / height);
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(layer.Width - 1, x * layer.Width / width);
                Buffer.BlockCopy(layer.Pixels, (sourceY * layer.Width + sourceX) * 4, bytes, (y * width + x) * 4, 4);
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bytes, width * 4);
    }

    private void UpdateLayerControls()
    {
        if (_doc.ActiveLayer is not { } layer)
        {
            return;
        }

        _updatingLayerUi = true;
        LayerOpacitySlider.Value = Math.Round(layer.Opacity * 100);
        LayerOpacityValue.Text = $"{(int)Math.Round(layer.Opacity * 100)}%";
        BlendBox.SelectedIndex = Array.IndexOf(BlendModes.All, layer.Blend);
        _updatingLayerUi = false;
    }

    private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rebuildingLayers || LayerList.SelectedItem is not LayerEntry entry)
        {
            return;
        }

        var index = _doc.IndexOf(entry.Layer);
        if (index < 0)
        {
            return;
        }

        _doc.ActiveLayerIndex = index;
        UpdateLayerControls();
        UpdateStatus();
    }

    private void LayerVisibility_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton || _doc == null)
        {
            return;
        }

        _doc.IsDirty = true;
        RefreshDisplay();
        UpdateTitle();
    }

    private void LayerOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = e.NewValue;
        if (!_uiReady)
        {
            return;
        }

        LayerOpacityValue.Text = $"{value:0}%";
        if (_updatingLayerUi || _doc?.ActiveLayer is not { } layer)
        {
            return;
        }

        layer.Opacity = value / 100;
        _doc.IsDirty = true;
        RefreshDisplay();
        RefreshActiveLayerInfo(layer);
        UpdateTitle();
    }

    private void Blend_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _updatingLayerUi || _doc?.ActiveLayer is not { } layer)
        {
            return;
        }

        if (BlendBox.SelectedItem is ComboBoxItem { Tag: BlendMode mode })
        {
            layer.Blend = mode;
            _doc.IsDirty = true;
            RefreshDisplay();
            RefreshActiveLayerInfo(layer);
            UpdateTitle();
        }
    }

    private void RefreshActiveLayerInfo(Layer layer)
    {
        var entry = _layerEntries.FirstOrDefault(item => ReferenceEquals(item.Layer, layer));
        entry?.RefreshInfo();
    }

    private void AddLayer_Click(object sender, RoutedEventArgs e)
    {
        DocumentOps.AddLayer(_doc);
        _history.Clear();
        AfterStructuralChange(false);
    }

    private void DuplicateLayer_Click(object sender, RoutedEventArgs e)
    {
        DocumentOps.DuplicateLayer(_doc, _doc.ActiveLayerIndex);
        _history.Clear();
        AfterStructuralChange(false);
    }

    private void MergeDown_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentOps.MergeDown(_doc, _doc.ActiveLayerIndex))
        {
            _history.Clear();
            AfterStructuralChange(false);
        }
    }

    private void DeleteLayer_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentOps.DeleteLayer(_doc, _doc.ActiveLayerIndex))
        {
            _history.Clear();
            AfterStructuralChange(false);
        }
    }

    private void MoveLayerUp_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentOps.MoveLayer(_doc, _doc.ActiveLayerIndex, _doc.ActiveLayerIndex + 1))
        {
            _history.Clear();
            AfterStructuralChange(false);
        }
    }

    private void MoveLayerDown_Click(object sender, RoutedEventArgs e)
    {
        if (DocumentOps.MoveLayer(_doc, _doc.ActiveLayerIndex, _doc.ActiveLayerIndex - 1))
        {
            _history.Clear();
            AfterStructuralChange(false);
        }
    }

    private void ClearLayer_Click(object sender, RoutedEventArgs e)
    {
        ApplyToLayer("Limpiar capa", layer => DrawOps.Clear(layer), useSelection: false);
    }

    // ================= Historial =================

    private void RefreshHistory()
    {
        _rebuildingHistory = true;

        var items = new List<HistoryEntry>();
        var undo = _history.UndoSteps;
        for (var i = 0; i < undo.Count; i++)
        {
            items.Add(new HistoryEntry(undo[i], i + 1));
        }

        items.Add(new HistoryEntry("Estado actual", _history.UndoCount, isCurrent: true));

        var redo = _history.RedoSteps;
        for (var i = 0; i < redo.Count; i++)
        {
            items.Add(new HistoryEntry(redo[redo.Count - 1 - i], _history.UndoCount + i + 1));
        }

        HistoryList.ItemsSource = null;
        HistoryList.ItemsSource = items;
        HistoryList.SelectedIndex = undo.Count;
        HistoryList.ScrollIntoView(HistoryList.SelectedItem);

        _rebuildingHistory = false;
    }

    private void UpdateHistoryUi()
    {
        MenuUndo.IsEnabled = _history.CanUndo;
        MenuRedo.IsEnabled = _history.CanRedo;
        MenuUndo.Header = _history.CanUndo ? $"Deshacer {_history.NextUndoName?.ToLowerInvariant()}" : "Deshacer";
        MenuRedo.Header = _history.CanRedo ? $"Rehacer {_history.NextRedoName?.ToLowerInvariant()}" : "Rehacer";
    }

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_rebuildingHistory || HistoryList.SelectedItem is not HistoryEntry entry)
        {
            return;
        }

        var target = entry.Target;
        var guard = 0;
        while (_history.UndoCount > target && _history.CanUndo && guard++ < 1000)
        {
            _history.Undo();
        }

        guard = 0;
        while (_history.UndoCount < target && _history.CanRedo && guard++ < 1000)
        {
            _history.Redo();
        }

        UpdateDirtyFromHistory();
        RefreshDisplay();
        Canvas.SetSelection(_doc.Selection);
        RefreshLayerList();
        RefreshHistory();
        UpdateHistoryUi();
        UpdateTitle();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (!_history.CanUndo)
        {
            return;
        }

        _history.Undo();
        UpdateDirtyFromHistory();
        RefreshDisplay();
        RefreshLayerList();
        RefreshHistory();
        UpdateHistoryUi();
        UpdateTitle();
        HintText.Text = "Deshacer";
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (!_history.CanRedo)
        {
            return;
        }

        _history.Redo();
        UpdateDirtyFromHistory();
        RefreshDisplay();
        RefreshLayerList();
        RefreshHistory();
        UpdateHistoryUi();
        UpdateTitle();
        HintText.Text = "Rehacer";
    }

    private void UpdateDirtyFromHistory()
    {
        _doc.IsDirty = _dirtyBaseline < 0 || _history.UndoCount != _dirtyBaseline;
    }

    private void AfterEdit()
    {
        _doc.IsDirty = true;
        RefreshDisplay();
        Canvas.SetSelection(_doc.Selection);
        RefreshLayerList();
        RefreshHistory();
        UpdateHistoryUi();
        UpdateTitle();
    }

    private void AfterStructuralChange(bool fit)
    {
        _dirtyBaseline = -1;
        _doc.IsDirty = true;

        if (_bitmap.PixelWidth != _doc.Width || _bitmap.PixelHeight != _doc.Height)
        {
            CreateDisplayBitmap();
        }

        RefreshDisplay();
        Canvas.SetDocumentSize(_doc.Width, _doc.Height);
        Canvas.SetSelection(_doc.Selection);
        MenuApplyCrop.IsEnabled = _cropPending;

        RefreshLayerList();
        RefreshHistory();
        UpdateHistoryUi();
        UpdateTitle();
        UpdateStatus();

        if (fit)
        {
            _fitMode = true;
            Canvas.Fit(Canvas.Viewport);
        }
        else
        {
            Canvas.Center(Canvas.Viewport);
        }

        UpdateZoomText();
    }

    // ================= Herramientas =================

    private void Tool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string tag } || !Enum.TryParse<Tool>(tag, out var tool))
        {
            return;
        }

        if (tool == Tool.RectSelect && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            tool = Tool.EllipseSelect;
        }

        SetTool(tool);
    }

    private void SetTool(Tool tool)
    {
        _tool = tool;
        HandTool.IsChecked = tool == Tool.Hand;
        ZoomTool.IsChecked = tool == Tool.Zoom;
        RectSelectTool.IsChecked = tool == Tool.RectSelect;
        EllipseSelectTool.IsChecked = tool == Tool.EllipseSelect;
        LassoTool.IsChecked = tool == Tool.Lasso;
        CropTool.IsChecked = tool == Tool.Crop;
        BrushTool.IsChecked = tool is Tool.Brush or Tool.Eraser;
        EraserTool.IsChecked = tool == Tool.Eraser;
        FillTool.IsChecked = tool == Tool.Fill;
        ShapeTool.IsChecked = tool == Tool.Shape;
        TextTool.IsChecked = tool == Tool.Text;
        EyedropperTool.IsChecked = tool == Tool.Eyedropper;

        Canvas.ShowRing = tool is Tool.Brush or Tool.Eraser;
        Canvas.CursorRadius = _brushSize / 2;
        Canvas.Cursor = tool switch
        {
            Tool.Hand => Cursors.Hand,
            Tool.Text => Cursors.IBeam,
            Tool.Brush or Tool.Eraser => Cursors.None,
            _ => Cursors.Cross
        };

        UpdateToolOptions();
        UpdateStatus();
        Canvas.Refresh();
    }

    private void UpdateToolOptions()
    {
        BrushOptions.Visibility = _tool is Tool.Brush or Tool.Eraser ? Visibility.Visible : Visibility.Collapsed;
        ShapeOptions.Visibility = _tool == Tool.Shape ? Visibility.Visible : Visibility.Collapsed;
        FillOptions.Visibility = _tool == Tool.Fill ? Visibility.Visible : Visibility.Collapsed;
        SelectOptions.Visibility = _tool is Tool.RectSelect or Tool.EllipseSelect or Tool.Lasso
            ? Visibility.Visible
            : Visibility.Collapsed;
        CropOptions.Visibility = _tool == Tool.Crop ? Visibility.Visible : Visibility.Collapsed;
        TextOptions.Visibility = _tool == Tool.Text ? Visibility.Visible : Visibility.Collapsed;
        ZoomOptions.Visibility = _tool == Tool.Zoom ? Visibility.Visible : Visibility.Collapsed;
        HandOptions.Visibility = _tool == Tool.Hand ? Visibility.Visible : Visibility.Collapsed;
        EyedropperOptions.Visibility = _tool == Tool.Eyedropper ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateBrushLabels()
    {
        BrushSizeValue.Text = $"{_brushSize:0} px";
        BrushHardnessValue.Text = $"{_brushHardness:0}%";
        BrushOpacityValue.Text = $"{_brushOpacity:0}%";
    }

    private void UpdateShapeLabels()
    {
        ShapeStrokeValue.Text = $"{_shapeStroke:0} px";
        ShapeOpacityValue.Text = $"{_shapeOpacity:0}%";
    }

    private void BrushSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _brushSize = e.NewValue;
        if (!_uiReady)
        {
            return;
        }

        BrushSizeValue.Text = $"{_brushSize:0} px";
        Canvas.CursorRadius = _brushSize / 2;
        Canvas.Refresh();
    }

    private void BrushHardness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _brushHardness = e.NewValue;
        if (_uiReady)
        {
            BrushHardnessValue.Text = $"{_brushHardness:0}%";
        }
    }

    private void BrushOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _brushOpacity = e.NewValue;
        if (_uiReady)
        {
            BrushOpacityValue.Text = $"{_brushOpacity:0}%";
        }
    }

    private void ShapeStroke_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _shapeStroke = e.NewValue;
        if (_uiReady)
        {
            ShapeStrokeValue.Text = $"{_shapeStroke:0} px";
        }
    }

    private void ShapeOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _shapeOpacity = e.NewValue;
        if (_uiReady)
        {
            ShapeOpacityValue.Text = $"{_shapeOpacity:0}%";
        }
    }

    private void ShapeFill_Changed(object sender, RoutedEventArgs e)
    {
        _shapeFilled = ShapeFillCheck.IsChecked == true;
    }

    private void ShapeKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        _shapeKind = ShapeKindBox.SelectedIndex switch
        {
            1 => ShapeKind.Ellipse,
            2 => ShapeKind.Line,
            _ => ShapeKind.Rectangle
        };
    }

    private void FillTolerance_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _fillTolerance = e.NewValue;
        if (_uiReady)
        {
            FillToleranceValue.Text = $"{_fillTolerance:0}";
        }
    }

    // ================= Colores =================

    private void BuildQuickPalette()
    {
        foreach (var hex in ColorUtil.Palette)
        {
            if (!ColorUtil.TryParseHex(hex, out var color))
            {
                continue;
            }

            var button = new Button
            {
                Style = (Style)FindResource("SwatchButton"),
                Background = new SolidColorBrush(color),
                ToolTip = hex
            };
            button.Click += (_, _) => SetPrimaryColor(color);
            QuickPalette.Children.Add(button);
        }
    }

    private void SetPrimaryColor(Color color)
    {
        _primary = color;
        UpdateColorSwatches();
    }

    private void UpdateColorSwatches()
    {
        var primary = new SolidColorBrush(_primary);
        var secondary = new SolidColorBrush(_secondary);
        PrimarySwatch.Background = primary;
        DockPrimarySwatch.Background = primary;
        SecondarySwatch.Background = secondary;
        DockSecondarySwatch.Background = secondary;
        PrimaryHexText.Text = ColorUtil.ToHex(_primary);
    }

    private void PrimarySwatch_Click(object sender, RoutedEventArgs e)
    {
        var picked = ColorPickerDialog.Pick(this, _primary);
        if (picked is { } color)
        {
            SetPrimaryColor(color);
        }
    }

    private void SecondarySwatch_Click(object sender, RoutedEventArgs e)
    {
        var picked = ColorPickerDialog.Pick(this, _secondary);
        if (picked is { } color)
        {
            _secondary = color;
            UpdateColorSwatches();
        }
    }

    private void SwapColors_Click(object sender, RoutedEventArgs e)
    {
        (_primary, _secondary) = (_secondary, _primary);
        UpdateColorSwatches();
    }

    // ================= Ratón sobre el lienzo =================

    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        Canvas.Focus();

        if (_doc == null)
        {
            return;
        }

        var screen = e.GetPosition(Canvas);
        var doc = Canvas.ScreenToDoc(screen);
        var right = e.ChangedButton == MouseButton.Right;

        if (e.ChangedButton == MouseButton.Middle)
        {
            BeginPan(screen);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left && !right)
        {
            return;
        }

        switch (_tool)
        {
            case Tool.Hand:
                BeginPan(screen);
                break;

            case Tool.Zoom:
                var factor = right || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? 1 / 1.3 : 1.3;
                Canvas.ZoomAt(screen, factor);
                _fitMode = false;
                UpdateZoomText();
                break;

            case Tool.Eyedropper:
                PickColor(doc, right);
                break;

            default:
                if (!Canvas.IsInsideDoc(screen))
                {
                    return;
                }

                switch (_tool)
                {
                    case Tool.Brush:
                        BeginStroke(ClampToCanvas(doc), false, right ? _secondary : _primary);
                        break;
                    case Tool.Eraser:
                        BeginStroke(ClampToCanvas(doc), true, _primary);
                        break;
                    case Tool.Fill:
                        FloodFillAt(doc, right ? _secondary : _primary);
                        break;
                    case Tool.Shape:
                        BeginShape(ClampToCanvas(doc));
                        break;
                    case Tool.Text:
                        InsertText(doc);
                        break;
                    case Tool.RectSelect:
                        BeginSelection(Interaction.RectSelect, doc);
                        break;
                    case Tool.EllipseSelect:
                        BeginSelection(Interaction.EllipseSelect, doc);
                        break;
                    case Tool.Lasso:
                        BeginSelection(Interaction.Lasso, doc);
                        break;
                    case Tool.Crop:
                        BeginCrop(doc);
                        break;
                }

                break;
        }

        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_doc == null)
        {
            return;
        }

        var screen = e.GetPosition(Canvas);
        var doc = Canvas.ScreenToDoc(screen);

        if (Canvas.IsInsideDoc(screen))
        {
            CursorText.Text = $"{(int)Math.Floor(doc.X)}, {(int)Math.Floor(doc.Y)}";
        }
        else
        {
            CursorText.Text = "—";
        }

        if (Canvas.ShowRing)
        {
            Canvas.BrushCursor = doc;
            Canvas.CursorRadius = _brushSize / 2;
            Canvas.Refresh();
        }

        if (_interaction == Interaction.None)
        {
            return;
        }

        switch (_interaction)
        {
            case Interaction.Pan:
                Canvas.PanBy(screen.X - _lastScreenPoint.X, screen.Y - _lastScreenPoint.Y);
                _lastScreenPoint = screen;
                _fitMode = false;
                break;

            case Interaction.Stroke:
                ContinueStroke(doc);
                break;

            case Interaction.Shape:
                UpdateShapePreview(doc);
                break;

            case Interaction.RectSelect:
            case Interaction.EllipseSelect:
            case Interaction.Lasso:
                UpdateSelectionPreview(doc);
                break;

            case Interaction.CropDrag:
                UpdateCropPreview(doc);
                break;
        }
    }

    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_doc == null || _interaction == Interaction.None)
        {
            return;
        }

        var screen = e.GetPosition(Canvas);
        var doc = Canvas.ScreenToDoc(screen);

        switch (_interaction)
        {
            case Interaction.Stroke:
                CommitStroke();
                break;
            case Interaction.Shape:
                CommitShape(doc);
                break;
            case Interaction.RectSelect:
            case Interaction.EllipseSelect:
            case Interaction.Lasso:
                CommitSelection(doc);
                break;
            case Interaction.CropDrag:
                CommitCrop(doc);
                break;
        }

        _interaction = Interaction.None;
        if (Canvas.IsMouseCaptured)
        {
            Canvas.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var anchor = e.GetPosition(Canvas);
        var factor = e.Delta > 0 ? 1.18 : 1 / 1.18;
        Canvas.ZoomAt(anchor, factor);
        _fitMode = false;
        UpdateZoomText();
        e.Handled = true;
    }

    private void Canvas_MouseLeave(object sender, MouseEventArgs e)
    {
        Canvas.BrushCursor = null;
        Canvas.Refresh();
    }

    private void BeginPan(Point screen)
    {
        _interaction = Interaction.Pan;
        _lastScreenPoint = screen;
        Canvas.CaptureMouse();
        Canvas.Cursor = Cursors.ScrollAll;
    }

    private void PickColor(Point doc, bool secondary)
    {
        var x = (int)Math.Floor(doc.X);
        var y = (int)Math.Floor(doc.Y);
        if (x < 0 || y < 0 || x >= _doc.Width || y >= _doc.Height)
        {
            return;
        }

        var index = (y * _doc.Width + x) * 4;
        var color = Color.FromRgb(_displayPixels[index + 2], _displayPixels[index + 1], _displayPixels[index]);
        if (secondary)
        {
            _secondary = color;
        }
        else
        {
            _primary = color;
        }

        UpdateColorSwatches();
        HintText.Text = $"Color tomado: {ColorUtil.ToHex(color)}";
    }

    private Point ClampToCanvas(Point point) => new(
        Math.Clamp(point.X, 0, _doc.Width - 0.01),
        Math.Clamp(point.Y, 0, _doc.Height - 0.01));

    // ================= Pincel y borrador =================

    private void EnsureEditBackup()
    {
        var needed = _doc.Width * _doc.Height * 4;
        if (_editBackup == null || _editBackup.Length < needed)
        {
            _editBackup = new byte[needed];
        }
    }

    private void BeginStroke(Point doc, bool erase, Color color)
    {
        var layer = _doc.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        EnsureEditBackup();
        Buffer.BlockCopy(layer.Pixels, 0, _editBackup!, 0, Math.Min(layer.Pixels.Length, _editBackup!.Length));

        _strokeLayer = layer;
        _strokeErase = erase;
        _strokeColor = ColorUtil.ToUint(color);
        _strokeMinX = int.MaxValue;
        _strokeMinY = int.MaxValue;
        _strokeMaxX = int.MinValue;
        _strokeMaxY = int.MinValue;
        _lastDocPoint = doc;
        _interaction = Interaction.Stroke;
        Canvas.CaptureMouse();

        DrawOps.Stroke(layer, _doc.Selection.Mask, new[] { new Pt(doc.X, doc.Y) }, _brushSize / 2,
            _brushHardness / 100, _brushOpacity / 100, _strokeColor, erase);

        TrackStroke(doc.X, doc.Y);
        _doc.IsDirty = true;
        RefreshRegionAround(doc.X, doc.Y);
    }

    private void ContinueStroke(Point doc)
    {
        if (_strokeLayer == null)
        {
            return;
        }

        DrawOps.Stroke(_strokeLayer, _doc.Selection.Mask,
            new[] { new Pt(_lastDocPoint.X, _lastDocPoint.Y), new Pt(doc.X, doc.Y) },
            _brushSize / 2, _brushHardness / 100, _brushOpacity / 100, _strokeColor, _strokeErase);

        TrackStroke(doc.X, doc.Y);
        RefreshRegionAround(doc.X, doc.Y);
        _lastDocPoint = doc;
    }

    private void TrackStroke(double x, double y)
    {
        var margin = (int)Math.Ceiling(_brushSize / 2 + 4);
        _strokeMinX = Math.Min(_strokeMinX, (int)Math.Floor(x) - margin);
        _strokeMinY = Math.Min(_strokeMinY, (int)Math.Floor(y) - margin);
        _strokeMaxX = Math.Max(_strokeMaxX, (int)Math.Ceiling(x) + margin);
        _strokeMaxY = Math.Max(_strokeMaxY, (int)Math.Ceiling(y) + margin);
    }

    private void RefreshRegionAround(double x, double y)
    {
        var radius = _brushSize / 2 + 5;
        var x0 = (int)Math.Floor(x - radius);
        var y0 = (int)Math.Floor(y - radius);
        var x1 = (int)Math.Ceiling(x + radius);
        var y1 = (int)Math.Ceiling(y + radius);
        RefreshRegion(x0, y0, x1 - x0, y1 - y0);
    }

    private void CommitStroke()
    {
        var layer = _strokeLayer;
        var snapshot = _editBackup;

        if (layer == null || snapshot == null || _strokeMinX > _strokeMaxX)
        {
            _strokeLayer = null;
            AfterEdit();
            return;
        }

        var x0 = Math.Clamp(_strokeMinX, 0, _doc.Width - 1);
        var y0 = Math.Clamp(_strokeMinY, 0, _doc.Height - 1);
        var x1 = Math.Clamp(_strokeMaxX, 0, _doc.Width - 1);
        var y1 = Math.Clamp(_strokeMaxY, 0, _doc.Height - 1);
        var width = x1 - x0 + 1;
        var height = y1 - y0 + 1;

        if (width > 0 && height > 0)
        {
            var before = ExtractRegion(snapshot, _doc.Width, x0, y0, width, height);
            var after = ExtractRegion(layer.Pixels, _doc.Width, x0, y0, width, height);
            if (!before.AsSpan().SequenceEqual(after))
            {
                _history.Push(new PixelEdit(layer, x0, y0, width, height, before, after,
                    _strokeErase ? "Borrador" : "Pincel"));
            }
        }

        _strokeLayer = null;
        AfterEdit();
    }

    private static byte[] ExtractRegion(byte[] pixels, int sourceWidth, int x, int y, int width, int height)
    {
        var buffer = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(pixels, ((y + row) * sourceWidth + x) * 4, buffer, row * width * 4, width * 4);
        }

        return buffer;
    }

    // ================= Relleno =================

    private void FloodFillAt(Point doc, Color color)
    {
        var layer = _doc.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        var x = (int)Math.Floor(doc.X);
        var y = (int)Math.Floor(doc.Y);
        if (x < 0 || y < 0 || x >= _doc.Width || y >= _doc.Height)
        {
            return;
        }

        EnsureEditBackup();
        Buffer.BlockCopy(layer.Pixels, 0, _editBackup!, 0, Math.Min(layer.Pixels.Length, _editBackup!.Length));

        if (!DrawOps.FloodFill(layer, _doc.Selection.Mask, x, y, ColorUtil.ToUint(color),
                (int)Math.Round(_fillTolerance), out var bounds))
        {
            HintText.Text = "La zona ya tiene ese color o está fuera de la selección.";
            return;
        }

        var before = ExtractRegion(_editBackup!, _doc.Width, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var after = ExtractRegion(layer.Pixels, _doc.Width, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        _history.Push(new PixelEdit(layer, bounds.X, bounds.Y, bounds.Width, bounds.Height, before, after, "Relleno"));
        AfterEdit();
    }

    // ================= Formas =================

    private void BeginShape(Point doc)
    {
        _interaction = Interaction.Shape;
        _startDoc = doc;
        _lastDocPoint = doc;
        Canvas.ShapeGeometry = null;
        Canvas.CaptureMouse();
    }

    private void UpdateShapePreview(Point doc)
    {
        Canvas.ShapeFilled = _shapeFilled;
        Canvas.ShapeColor = Color.FromArgb((byte)Math.Round(255 * _shapeOpacity / 100), _primary.R, _primary.G, _primary.B);
        Canvas.ShapeStrokeWidth = _shapeStroke;
        Canvas.ShapeGeometry = CreateShapeGeometry(_startDoc, doc);
        Canvas.Refresh();
    }

    private Geometry? CreateShapeGeometry(Point start, Point end)
    {
        switch (_shapeKind)
        {
            case ShapeKind.Line:
                var line = new LineGeometry(start, end);
                line.Freeze();
                return line;
            case ShapeKind.Ellipse:
                var rect = NormalizeRect(start, end);
                if (rect.Width < 1 || rect.Height < 1)
                {
                    return null;
                }

                var ellipse = new EllipseGeometry(rect);
                ellipse.Freeze();
                return ellipse;
            default:
                var rectangle = NormalizeRect(start, end);
                if (rectangle.Width < 1 || rectangle.Height < 1)
                {
                    return null;
                }

                var geometry = new RectangleGeometry(rectangle);
                geometry.Freeze();
                return geometry;
        }
    }

    private static Rect NormalizeRect(Point a, Point b) => new(
        Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private void CommitShape(Point end)
    {
        Canvas.ShapeGeometry = null;
        Canvas.Refresh();

        var layer = _doc.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        double x, y, width, height;
        if (_shapeKind == ShapeKind.Line)
        {
            x = _startDoc.X;
            y = _startDoc.Y;
            width = end.X - _startDoc.X;
            height = end.Y - _startDoc.Y;
            if (Math.Abs(width) < 2 && Math.Abs(height) < 2)
            {
                return;
            }
        }
        else
        {
            var rect = NormalizeRect(_startDoc, end);
            x = rect.X;
            y = rect.Y;
            width = rect.Width;
            height = rect.Height;
            if (width < 2 || height < 2)
            {
                return;
            }
        }

        var margin = (int)Math.Ceiling(_shapeStroke + 4);
        var bx = (int)Math.Floor(Math.Min(x, x + width)) - margin;
        var by = (int)Math.Floor(Math.Min(y, y + height)) - margin;
        var bw = (int)Math.Ceiling(Math.Abs(width)) + margin * 2;
        var bh = (int)Math.Ceiling(Math.Abs(height)) + margin * 2;

        var edit = EditRecorder.Record(layer, bx, by, bw, bh, () =>
            DrawOps.Shape(layer, _doc.Selection.Mask, _shapeKind, x, y, width, height,
                ColorUtil.ToUint(_primary), _shapeStroke, _shapeOpacity / 100, _shapeFilled), "Forma");

        _history.Push(edit);
        AfterEdit();
    }

    // ================= Selecciones =================

    private void BeginSelection(Interaction kind, Point doc)
    {
        _interaction = kind;
        _startDoc = doc;
        _lastDocPoint = doc;
        _lassoPoints.Clear();
        if (kind == Interaction.Lasso)
        {
            _lassoPoints.Add(new Pt(doc.X, doc.Y));
        }

        Canvas.LiveGeometry = null;
        Canvas.CaptureMouse();
    }

    private void UpdateSelectionPreview(Point doc)
    {
        switch (_interaction)
        {
            case Interaction.RectSelect:
                var rect = NormalizeRect(_startDoc, doc);
                Canvas.LiveGeometry = new RectangleGeometry(rect);
                break;
            case Interaction.EllipseSelect:
                var ellipseRect = NormalizeRect(_startDoc, doc);
                Canvas.LiveGeometry = ellipseRect.Width < 1 || ellipseRect.Height < 1
                    ? null
                    : new EllipseGeometry(ellipseRect);
                break;
            case Interaction.Lasso:
                if (_lassoPoints.Count == 0 ||
                    Math.Abs(doc.X - _lastDocPoint.X) + Math.Abs(doc.Y - _lastDocPoint.Y) > 1.5)
                {
                    _lassoPoints.Add(new Pt(doc.X, doc.Y));
                    _lastDocPoint = doc;
                }

                if (_lassoPoints.Count >= 2)
                {
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open())
                    {
                        context.BeginFigure(new Point(_lassoPoints[0].X, _lassoPoints[0].Y), false, true);
                        var points = new List<Point>();
                        for (var i = 1; i < _lassoPoints.Count; i++)
                        {
                            points.Add(new Point(_lassoPoints[i].X, _lassoPoints[i].Y));
                        }

                        context.PolyLineTo(points, true, false);
                    }

                    geometry.Freeze();
                    Canvas.LiveGeometry = geometry;
                }

                break;
        }

        Canvas.Refresh();
    }

    private void CommitSelection(Point end)
    {
        Canvas.LiveGeometry = null;

        switch (_interaction)
        {
            case Interaction.RectSelect:
                var rect = NormalizeRect(_startDoc, end);
                if (rect.Width < 1.5 || rect.Height < 1.5)
                {
                    SelectionOps.Deselect(_doc.Selection);
                }
                else
                {
                    SelectionOps.SelectRectangle(_doc.Selection,
                        (int)Math.Round(rect.X), (int)Math.Round(rect.Y),
                        Math.Max(1, (int)Math.Round(rect.Width)), Math.Max(1, (int)Math.Round(rect.Height)));
                }

                break;

            case Interaction.EllipseSelect:
                var ellipseRect = NormalizeRect(_startDoc, end);
                if (ellipseRect.Width < 2 || ellipseRect.Height < 2)
                {
                    SelectionOps.Deselect(_doc.Selection);
                }
                else
                {
                    SelectionOps.SelectEllipse(_doc.Selection,
                        (int)Math.Round(ellipseRect.X), (int)Math.Round(ellipseRect.Y),
                        Math.Max(1, (int)Math.Round(ellipseRect.Width)), Math.Max(1, (int)Math.Round(ellipseRect.Height)));
                }

                break;

            case Interaction.Lasso:
                if (_lassoPoints.Count >= 3)
                {
                    SelectionOps.SelectPolygon(_doc.Selection, _lassoPoints);
                }
                else
                {
                    SelectionOps.Deselect(_doc.Selection);
                }

                break;
        }

        Canvas.SetSelection(_doc.Selection);
        Canvas.Refresh();

        if (_doc.Selection.IsActive)
        {
            var bounds = SelectionOps.Bounds(_doc.Selection);
            HintText.Text = $"Selección de {bounds.Width} × {bounds.Height} px";
        }
        else
        {
            HintText.Text = "Selección cancelada";
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        SelectionOps.SelectAll(_doc.Selection);
        Canvas.SetSelection(_doc.Selection);
        Canvas.Refresh();
        HintText.Text = "Todo el lienzo seleccionado";
    }

    private void Deselect_Click(object sender, RoutedEventArgs e)
    {
        SelectionOps.Deselect(_doc.Selection);
        Canvas.SetSelection(_doc.Selection);
        Canvas.Refresh();
        HintText.Text = "Selección cancelada";
    }

    private void InvertSelection_Click(object sender, RoutedEventArgs e)
    {
        if (!_doc.Selection.IsActive)
        {
            SelectionOps.SelectAll(_doc.Selection);
        }
        else
        {
            SelectionOps.Invert(_doc.Selection);
        }

        Canvas.SetSelection(_doc.Selection);
        Canvas.Refresh();
        HintText.Text = "Selección invertida";
    }

    private void DeleteSelection_Click(object sender, RoutedEventArgs e)
    {
        if (!_doc.Selection.IsActive)
        {
            HintText.Text = "No hay ninguna selección activa";
            return;
        }

        var layer = _doc.ActiveLayer;
        if (layer == null)
        {
            return;
        }

        var bounds = SelectionOps.Bounds(_doc.Selection);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var mask = _doc.Selection.Mask;
        var edit = EditRecorder.Record(layer, bounds.X, bounds.Y, bounds.Width, bounds.Height, () =>
        {
            for (var row = 0; row < bounds.Height; row++)
            {
                var sourceRow = (bounds.Y + row) * layer.Width + bounds.X;
                for (var column = 0; column < bounds.Width; column++)
                {
                    var maskValue = mask == null ? 255 : mask[sourceRow + column];
                    if (maskValue == 0)
                    {
                        continue;
                    }

                    var index = (sourceRow + column) * 4;
                    layer.Pixels[index + 3] = (byte)(layer.Pixels[index + 3] * (255 - maskValue) / 255);
                }
            }
        }, "Borrar selección");

        _history.Push(edit);
        AfterEdit();
    }

    // ================= Recorte =================

    private void BeginCrop(Point doc)
    {
        _interaction = Interaction.CropDrag;
        _startDoc = doc;
        _lastDocPoint = doc;
        _cropPending = false;
        MenuApplyCrop.IsEnabled = false;
        Canvas.CropRect = null;
        Canvas.CaptureMouse();
        CropInfoText.Text = "Arrastra para marcar la zona";
    }

    private void UpdateCropPreview(Point doc)
    {
        var rect = NormalizeRect(_startDoc, doc);
        Canvas.CropRect = rect.Width > 1 && rect.Height > 1 ? rect : null;
        CropInfoText.Text = $"{(int)Math.Round(rect.Width)} × {(int)Math.Round(rect.Height)} px";
        Canvas.Refresh();
    }

    private void CommitCrop(Point end)
    {
        var rect = NormalizeRect(_startDoc, end);
        if (rect.Width < 4 || rect.Height < 4)
        {
            Canvas.CropRect = null;
            CropInfoText.Text = "Arrastra para marcar la zona";
            _cropPending = false;
            MenuApplyCrop.IsEnabled = false;
            Canvas.Refresh();
            return;
        }

        var snapped = new Rect(
            Math.Round(rect.X), Math.Round(rect.Y),
            Math.Max(4, Math.Round(rect.Width)), Math.Max(4, Math.Round(rect.Height)));
        Canvas.CropRect = snapped;
        _cropPending = true;
        MenuApplyCrop.IsEnabled = true;
        CropInfoText.Text = $"{snapped.Width:0} × {snapped.Height:0} px · Intro para aplicar";
        HintText.Text = "Pulsa Intro para aplicar el recorte o Esc para cancelarlo";
        Canvas.Refresh();
    }

    private void ApplyCrop_Click(object sender, RoutedEventArgs e)
    {
        if (!_cropPending || Canvas.CropRect is not { } rect)
        {
            return;
        }

        DocumentOps.Crop(_doc, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        _history.Clear();
        _cropPending = false;
        Canvas.CropRect = null;
        MenuApplyCrop.IsEnabled = false;
        AfterStructuralChange(true);
        HintText.Text = "Imagen recortada";
    }

    private void CancelCrop_Click(object sender, RoutedEventArgs e)
    {
        _cropPending = false;
        Canvas.CropRect = null;
        MenuApplyCrop.IsEnabled = false;
        CropInfoText.Text = string.Empty;
        Canvas.Refresh();
        HintText.Text = "Recorte cancelado";
    }

    private void CropToSelection_Click(object sender, RoutedEventArgs e)
    {
        if (!_doc.Selection.IsActive)
        {
            HintText.Text = "Selecciona primero la zona que quieres conservar";
            return;
        }

        var bounds = SelectionOps.Bounds(_doc.Selection);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        DocumentOps.Crop(_doc, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        _history.Clear();
        AfterStructuralChange(true);
        HintText.Text = "Imagen recortada a la selección";
    }

    // ================= Texto =================

    private void InsertText(Point doc)
    {
        var dialog = new TextDialog(_primary) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var baseline = doc.Y + dialog.FontSizeValue * 0.82;
        ApplyToLayer("Texto", layer => DrawOps.Text(layer, _doc.Selection.Mask, dialog.TextValue,
            doc.X, baseline, dialog.FontFamilyName, dialog.FontSizeValue, ColorUtil.ToUint(dialog.TextColor),
            dialog.Bold, dialog.Italic, 1.0));
    }

    // ================= Ajustes y filtros =================

    private bool ApplyToLayer(string name, Action<Layer> action, bool useSelection = true)
    {
        var layer = _doc.ActiveLayer;
        if (layer == null)
        {
            return false;
        }

        var x = 0;
        var y = 0;
        var width = _doc.Width;
        var height = _doc.Height;

        if (useSelection && _doc.Selection.IsActive)
        {
            var bounds = SelectionOps.Bounds(_doc.Selection);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                HintText.Text = "La selección está vacía.";
                return false;
            }

            (x, y, width, height) = (bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        var edit = EditRecorder.Record(layer, x, y, width, height, () => action(layer), name);
        _history.Push(edit);
        AfterEdit();
        return true;
    }

    private byte[] CreatePreview(out int width, out int height, out double scale)
    {
        const int maxWidth = 420;
        const int maxHeight = 190;
        scale = Math.Min(1.0, Math.Min((double)maxWidth / _doc.Width, (double)maxHeight / _doc.Height));
        width = Math.Max(1, (int)Math.Round(_doc.Width * scale));
        height = Math.Max(1, (int)Math.Round(_doc.Height * scale));
        var buffer = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Min(_doc.Height - 1, y * _doc.Height / height);
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Min(_doc.Width - 1, x * _doc.Width / width);
                Buffer.BlockCopy(_displayPixels, (sourceY * _doc.Width + sourceX) * 4,
                    buffer, (y * width + x) * 4, 4);
            }
        }

        return buffer;
    }

    private void BrightnessContrast_Click(object sender, RoutedEventArgs e)
    {
        var preview = CreatePreview(out var width, out var height, out _);
        var dialog = new AdjustmentsDialog(AdjustmentsDialog.Mode.BrightnessContrast, preview, width, height)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || (dialog.First == 0 && dialog.Second == 0))
        {
            return;
        }

        var first = dialog.First;
        var second = dialog.Second;
        ApplyToLayer("Brillo y contraste", layer =>
            Adjustments.BrightnessContrast(layer, _doc.Selection.Mask, first, second));
    }

    private void HueSaturation_Click(object sender, RoutedEventArgs e)
    {
        var preview = CreatePreview(out var width, out var height, out _);
        var dialog = new AdjustmentsDialog(AdjustmentsDialog.Mode.HueSaturation, preview, width, height)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true || (dialog.First == 0 && dialog.Second == 0))
        {
            return;
        }

        var first = dialog.First;
        var second = dialog.Second;
        ApplyToLayer("Tono y saturación", layer =>
            Adjustments.HueSaturation(layer, _doc.Selection.Mask, first, second));
    }

    private void Desaturate_Click(object sender, RoutedEventArgs e) =>
        ApplyToLayer("Desaturar", layer => Adjustments.Desaturate(layer, _doc.Selection.Mask));

    private void Sepia_Click(object sender, RoutedEventArgs e) =>
        ApplyToLayer("Sepia", layer => Adjustments.Sepia(layer, _doc.Selection.Mask));

    private void InvertColors_Click(object sender, RoutedEventArgs e) =>
        ApplyToLayer("Invertir colores", layer => Adjustments.Invert(layer, _doc.Selection.Mask));

    private void Blur_Click(object sender, RoutedEventArgs e) =>
        ApplyFilter(FilterDialog.Mode.Blur, 8, "Desenfoque",
            (layer, value, scale) => Filters.Blur(layer, _doc.Selection.Mask, Math.Max(1, (int)Math.Round(value * scale))));

    private void Sharpen_Click(object sender, RoutedEventArgs e) =>
        ApplyFilter(FilterDialog.Mode.Sharpen, 100, "Enfoque",
            (layer, value, _) => Filters.Sharpen(layer, _doc.Selection.Mask, value));

    private void Pixelate_Click(object sender, RoutedEventArgs e) =>
        ApplyFilter(FilterDialog.Mode.Pixelate, 12, "Pixelar",
            (layer, value, scale) => Filters.Pixelate(layer, _doc.Selection.Mask, Math.Max(2, (int)Math.Round(value * scale))));

    private void Emboss_Click(object sender, RoutedEventArgs e) =>
        ApplyFilter(FilterDialog.Mode.Emboss, 100, "Relieve",
            (layer, value, _) => Filters.Emboss(layer, _doc.Selection.Mask, value));

    private void ApplyFilter(FilterDialog.Mode mode, double initial, string name,
        Action<Layer, double, double> apply)
    {
        var preview = CreatePreview(out var width, out var height, out var scale);
        var dialog = new FilterDialog(mode, preview, width, height, scale, initial) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var amount = dialog.Amount;
        ApplyToLayer(name, layer => apply(layer, amount, scale));
    }

    // ================= Imagen =================

    private void ResizeImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ResizeDialog(_doc.Width, _doc.Height) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.ResultWidth == _doc.Width && dialog.ResultHeight == _doc.Height)
        {
            return;
        }

        DocumentOps.Resize(_doc, dialog.ResultWidth, dialog.ResultHeight);
        _history.Clear();
        AfterStructuralChange(true);
        HintText.Text = $"Imagen redimensionada a {_doc.Width} × {_doc.Height} px";
    }

    private void RotateRight_Click(object sender, RoutedEventArgs e) => Rotate(90);
    private void RotateLeft_Click(object sender, RoutedEventArgs e) => Rotate(270);
    private void Rotate180_Click(object sender, RoutedEventArgs e) => Rotate(180);

    private void Rotate(int degrees)
    {
        DocumentOps.Rotate(_doc, degrees);
        _history.Clear();
        AfterStructuralChange(true);
        HintText.Text = "Imagen girada";
    }

    private void FlipHorizontal_Click(object sender, RoutedEventArgs e) => Flip(true);

    private void FlipVertical_Click(object sender, RoutedEventArgs e) => Flip(false);

    private void Flip(bool horizontal)
    {
        DocumentOps.Flip(_doc, horizontal);
        _history.Clear();
        AfterStructuralChange(false);
        HintText.Text = horizontal ? "Imagen volteada horizontalmente" : "Imagen volteada verticalmente";
    }

    // ================= Archivo =================

    private void New_Click(object sender, RoutedEventArgs e) => NewDocument();

    private void NewDocument()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var dialog = new NewDocumentDialog { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var document = new Document(dialog.ResultWidth, dialog.ResultHeight, "Sin título");
        if (dialog.Transparent)
        {
            Array.Clear(document.Layers[0].Pixels);
        }

        LoadDocument(document, null);
        HintText.Text = "Documento nuevo creado";
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenFile();

    private void OpenFile()
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Abrir imagen",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tif;*.tiff;*.ico|Todos los archivos|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            OpenPath(dialog.FileName);
        }
    }

    private void OpenPath(string path)
    {
        try
        {
            var (pixels, width, height) = ImageIo.Load(path);
            var document = Document.FromImage(Path.GetFileNameWithoutExtension(path), width, height, pixels);
            LoadDocument(document, path);
            HintText.Text = $"Abierto: {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "No se pudo abrir la imagen.\n\n" + exception.Message, "Mphoto",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveDocument();

    private bool SaveDocument()
    {
        if (string.IsNullOrEmpty(_doc.FilePath))
        {
            return SaveAs();
        }

        return SaveTo(_doc.FilePath, updateDocument: true);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveAs();

    private bool SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Guardar imagen",
            FileName = _doc.Name,
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|WebP (*.webp)|*.webp|BMP (*.bmp)|*.bmp",
            DefaultExt = ".png",
            AddExtension = true
        };

        return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName, updateDocument: true);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar copia",
            FileName = _doc.Name,
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|WebP (*.webp)|*.webp|BMP (*.bmp)|*.bmp",
            DefaultExt = ".png",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            SaveTo(dialog.FileName, updateDocument: false);
        }
    }

    private bool SaveTo(string path, bool updateDocument)
    {
        try
        {
            var pixels = Compositor.Composite(_doc);
            var format = ImageIo.FormatFromPath(path);
            if (format is ImageFormat.Jpeg or ImageFormat.Bmp)
            {
                pixels = FlattenOnWhite(pixels);
            }

            ImageIo.Save(path, pixels, _doc.Width, _doc.Height, format, 92);

            if (updateDocument)
            {
                _doc.FilePath = path;
                _doc.Name = Path.GetFileNameWithoutExtension(path);
                _dirtyBaseline = _history.UndoCount;
                _doc.IsDirty = false;
                UpdateTitle();
            }

            HintText.Text = $"Imagen guardada como {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, "No se pudo guardar la imagen.\n\n" + exception.Message, "Mphoto",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private static byte[] FlattenOnWhite(byte[] pixels)
    {
        var result = new byte[pixels.Length];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3] / 255.0;
            result[i] = (byte)Math.Clamp(Math.Round(pixels[i] * alpha + 255 * (1 - alpha)), 0, 255);
            result[i + 1] = (byte)Math.Clamp(Math.Round(pixels[i + 1] * alpha + 255 * (1 - alpha)), 0, 255);
            result[i + 2] = (byte)Math.Clamp(Math.Round(pixels[i + 2] * alpha + 255 * (1 - alpha)), 0, 255);
            result[i + 3] = 255;
        }

        return result;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private bool ConfirmDiscardChanges()
    {
        if (_doc == null || !_doc.IsDirty)
        {
            return true;
        }

        var choice = ConfirmDialog.Ask(this, "Cambios sin guardar",
            $"«{_doc.Name}» tiene cambios sin guardar.\n¿Quieres guardarlos antes de continuar?",
            "Guardar", "No guardar", "Cancelar");

        return choice switch
        {
            ConfirmDialog.Choice.Primary => SaveDocument(),
            ConfirmDialog.Choice.Secondary => true,
            _ => false
        };
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var path = files.FirstOrDefault(ImageIo.CanOpen);
        if (path != null && ConfirmDiscardChanges())
        {
            OpenPath(path);
        }
    }

    // ================= Ver =================

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        _fitMode = true;
        Canvas.Fit(Canvas.Viewport);
        UpdateZoomText();
    }

    private void ActualSize_Click(object sender, RoutedEventArgs e)
    {
        _fitMode = false;
        Canvas.SetZoom(1, Canvas.Viewport);
        UpdateZoomText();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomBy(1.25);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.25);

    private void ZoomBy(double factor)
    {
        _fitMode = false;
        Canvas.SetZoom(Canvas.Zoom * factor, Canvas.Viewport);
        UpdateZoomText();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutDialog { Owner = this }.ShowDialog();
    }

    // ================= Teclado =================

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_doc == null)
        {
            return;
        }

        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (control)
        {
            switch (key)
            {
                case Key.N when shift:
                    AddLayer_Click(sender, e);
                    break;
                case Key.N:
                    NewDocument();
                    break;
                case Key.O:
                    OpenFile();
                    break;
                case Key.S when shift:
                    SaveAs();
                    break;
                case Key.S:
                    SaveDocument();
                    break;
                case Key.E when shift:
                    Export_Click(sender, e);
                    break;
                case Key.E:
                    MergeDown_Click(sender, e);
                    break;
                case Key.Z when shift:
                    Redo_Click(sender, e);
                    break;
                case Key.Z:
                    Undo_Click(sender, e);
                    break;
                case Key.Y:
                    Redo_Click(sender, e);
                    break;
                case Key.A:
                    SelectAll_Click(sender, e);
                    break;
                case Key.D:
                    Deselect_Click(sender, e);
                    break;
                case Key.I when shift:
                    InvertSelection_Click(sender, e);
                    break;
                case Key.D0:
                case Key.NumPad0:
                    Fit_Click(sender, e);
                    break;
                case Key.D1:
                case Key.NumPad1:
                    ActualSize_Click(sender, e);
                    break;
                case Key.OemPlus:
                case Key.Add:
                    ZoomBy(1.25);
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    ZoomBy(1 / 1.25);
                    break;
                default:
                    return;
            }

            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBox or ComboBox)
        {
            return;
        }

        switch (key)
        {
            case Key.B:
                SetTool(Tool.Brush);
                break;
            case Key.E:
                SetTool(Tool.Eraser);
                break;
            case Key.G:
                SetTool(Tool.Fill);
                break;
            case Key.T:
                SetTool(Tool.Text);
                break;
            case Key.U:
                SetTool(Tool.Shape);
                break;
            case Key.M when shift:
                SetTool(Tool.EllipseSelect);
                break;
            case Key.M:
                SetTool(Tool.RectSelect);
                break;
            case Key.L:
                SetTool(Tool.Lasso);
                break;
            case Key.C:
                SetTool(Tool.Crop);
                break;
            case Key.I:
                SetTool(Tool.Eyedropper);
                break;
            case Key.H:
                SetTool(Tool.Hand);
                break;
            case Key.Z:
                SetTool(Tool.Zoom);
                break;
            case Key.Delete:
            case Key.Back:
                DeleteSelection_Click(sender, e);
                break;
            case Key.Enter:
                if (_cropPending)
                {
                    ApplyCrop_Click(sender, e);
                }
                else
                {
                    return;
                }

                break;
            case Key.Escape:
                if (_cropPending)
                {
                    CancelCrop_Click(sender, e);
                }
                else if (_doc.Selection.IsActive)
                {
                    Deselect_Click(sender, e);
                }
                else
                {
                    return;
                }

                break;
            case Key.OemPlus:
            case Key.Add:
                ZoomBy(1.25);
                break;
            case Key.OemMinus:
            case Key.Subtract:
                ZoomBy(1 / 1.25);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}

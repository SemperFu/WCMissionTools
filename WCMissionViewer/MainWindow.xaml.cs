using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using WCMissionCore;

namespace WCMissionViewer;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    List<ViewerMission>? _missions;
    ViewerMission? _currentMission;
    bool _isWc2;
    int _selectedNavIndex = -1;
    readonly Dictionary<int, (TextBlock label, TextBlock coord, Ellipse marker, Color origColor)> _navElements = new();
    readonly Dictionary<(int, int), List<ViewerNavPoint>> _overlappingNavs = new();
    readonly Dictionary<(int, int), int> _overlapCycleIndex = new();

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (s, e) => { if (_currentMission != null) DrawNavMap(_currentMission); };
        Loaded += (_, _) => EnableDarkTitleBar();
    }

    void EnableDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int value = 1;
        DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref value, sizeof(int));

        var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (System.IO.File.Exists(icoPath))
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(icoPath));
    }

    void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open MODULE file",
            Filter = "MODULE files (MODULE.*)|MODULE.*|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            byte[] header = new byte[8];
            using (var fs = System.IO.File.OpenRead(dlg.FileName))
                fs.ReadExactly(header, 0, 8);

            _isWc2 = Wc2ModuleParser.IsWc2Module(header);

            if (_isWc2)
            {
                // Extract module number from filename (MODULE.001 → 1) for class ID disambiguation
                int moduleNum = 0;
                var ext = System.IO.Path.GetExtension(dlg.FileName);
                if (ext.Length > 1) int.TryParse(ext.AsSpan(1), out moduleNum);

                var parser = new Wc2ModuleParser();
                var module = parser.Parse(dlg.FileName);
                _missions = ViewerModelConverter.FromWc2(module, moduleNum);
            }
            else
            {
                var parser = new ModuleParser();
                var module = parser.Parse(dlg.FileName);
                _missions = ViewerModelConverter.FromWc1(module);
            }

            string game = _isWc2 ? "WC2" : "WC1";
            int displayCount = _isWc2 ? _missions.Count(m => m.SystemIndex > 0) : _missions.Count;
            Title = $"{game} Mission Viewer";
            FileLabel.Text = System.IO.Path.GetFileName(dlg.FileName) + $" — {game} — {displayCount} missions";
            PopulateSystemSelector();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to parse file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    List<int> GetSystemIndices()
    {
        if (_missions == null) return [];
        // WC2 system 0 is simulator/campaign/dialog data, not real missions
        return _missions.Select(m => m.SystemIndex).Distinct().OrderBy(s => s)
            .Where(s => !_isWc2 || s > 0).ToList();
    }

    void PopulateSystemSelector()
    {
        if (_missions == null) return;
        var systems = GetSystemIndices();
        SystemSelector.ItemsSource = systems.Select(s =>
        {
            var sysName = _missions.FirstOrDefault(m => m.SystemIndex == s)?.SystemName;
            if (!string.IsNullOrEmpty(sysName)) return $"{s}: {sysName}";
            return $"{s}: System {s}";
        }).ToList();
        if (systems.Count > 0) SystemSelector.SelectedIndex = 0;
    }

    void SystemSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_missions == null || SystemSelector.SelectedIndex < 0) return;
        var systems = GetSystemIndices();
        if (SystemSelector.SelectedIndex >= systems.Count) return;
        int sys = systems[SystemSelector.SelectedIndex];

        var missions = _missions.Where(m => m.SystemIndex == sys).OrderBy(m => m.MissionIndex).ToList();
        MissionSelector.ItemsSource = missions.Select((m, i) =>
            _isWc2 && !string.IsNullOrEmpty(m.Label) ? m.Label : $"Mission {i + 1}"
        ).ToList();
        if (missions.Count > 0)
        {
            MissionSelector.SelectedIndex = 0;
            _currentMission = missions[0];
            SortieLabel.Text = FormatSortieLabel(_currentMission);
            LoadMission(_currentMission);
        }
    }

    void MissionSelector_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_missions == null || SystemSelector.SelectedIndex < 0 || MissionSelector.SelectedIndex < 0) return;
        var systems = GetSystemIndices();
        if (SystemSelector.SelectedIndex >= systems.Count) return;
        int sys = systems[SystemSelector.SelectedIndex];
        var missions = _missions.Where(m => m.SystemIndex == sys).OrderBy(m => m.MissionIndex).ToList();
        if (MissionSelector.SelectedIndex >= missions.Count) return;

        _currentMission = missions[MissionSelector.SelectedIndex];
        SortieLabel.Text = FormatSortieLabel(_currentMission);
        LoadMission(_currentMission);
    }

    static string FormatSortieLabel(ViewerMission m)
    {
        var parts = new List<string> { $"(Sortie {m.SortieIndex})" };
        if (!string.IsNullOrEmpty(m.Label)) parts.Add($"★ {m.Label}");
        return string.Join("  ", parts);
    }

    void LoadMission(ViewerMission mission)
    {
        LoadShipList(mission);
        DrawNavMap(mission);
        LoadBriefing(mission);
        DetailPanel.Children.Clear();
        DetailHeader.Text = "DETAILS";
    }

    // --- Ship List ---

    void LoadShipList(ViewerMission mission)
    {
        ShipList.ItemsSource = mission.Ships.Select(s => new ShipViewModel(s)).ToList();
    }

    void ShipList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShipList.SelectedItem is not ShipViewModel vm) return;
        ShowShipDetail(vm.Ship);
    }

    void ShowShipDetail(ViewerShip ship)
    {
        DetailHeader.Text = $"SHIP [{ship.Index:D2}] — {ship.ClassName}";
        DetailPanel.Children.Clear();

        AddDetailRow("Class", ship.ClassName);
        AddDetailRow("Allegiance", ship.Allegiance.ToString());
        AddDetailRow(_isWc2 ? "Character" : "Pilot", ship.PilotName);
        AddDetailRow("Orders", ship.OrdersName);
        AddDetailRow("Position", $"({ship.X}, {ship.Y}, {ship.Z})");
        AddDetailRow("Rotation", $"({ship.RotationX}, {ship.RotationY}, {ship.RotationZ})");
        AddDetailRow("Speed", $"{ship.Speed}");
        if (ship.Size > 0) AddDetailRow("Size", $"{ship.Size}");
        if (ship.Leader >= 0) AddDetailRow("Leader", $"Ship [{ship.Leader:D2}]");
        if (ship.PrimaryTarget >= 0) AddDetailRow("Pri Target", $"Ship [{ship.PrimaryTarget:D2}]");
        if (ship.SecondaryTarget >= 0) AddDetailRow("Sec Target", $"Ship [{ship.SecondaryTarget:D2}]");
        if (ship.Formation >= 0) AddDetailRow("Formation", $"{ship.Formation}");
        AddDetailRow("AI Level", $"{ship.AiLevel}");
    }

    void ShowNavDetail(ViewerNavPoint nav)
    {
        // Reset previous selection
        if (_selectedNavIndex >= 0 && _navElements.TryGetValue(_selectedNavIndex, out var prev))
        {
            var origBrush = new SolidColorBrush(prev.origColor);
            prev.label.Foreground = origBrush;
            prev.coord.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88));
            prev.marker.Stroke = origBrush;
            prev.marker.Fill = new SolidColorBrush(Color.FromArgb(60, prev.origColor.R, prev.origColor.G, prev.origColor.B));
        }

        // Highlight new selection yellow
        _selectedNavIndex = nav.Index;
        if (_navElements.TryGetValue(nav.Index, out var cur))
        {
            var yellow = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0x00));
            cur.label.Foreground = yellow;
            cur.coord.Foreground = yellow;
            cur.marker.Stroke = yellow;
            cur.marker.Fill = new SolidColorBrush(Color.FromArgb(60, 0xFF, 0xFF, 0x00));
        }

        DetailHeader.Text = nav.Name;
        DetailPanel.Children.Clear();

        AddDetailRow("Slot", $"{nav.Index}");
        string typeName = nav.NavType switch
        {
            0 => "Manipulated (hidden)",
            1 => "Dominant",
            >= 2 and <= 5 => "Follow-up",
            _ => $"{nav.NavType}"
        };
        AddDetailRow("Type", typeName);
        AddDetailRow("Position", $"({nav.X}, {nav.Y}, {nav.Z})");
        if (nav.Radius > 0) AddDetailRow("Radius", $"{nav.Radius:N0}");

        // Show briefing notes — check this nav and any colocated navs
        var notes = new List<string>();
        if (!string.IsNullOrEmpty(nav.BriefingNote))
            notes.Add(nav.BriefingNote);
        if (_currentMission != null)
        {
            // Also gather briefing entries that target colocated navs
            var colocatedIndices = _currentMission.NavPoints
                .Where(n => n.X == nav.X && n.Z == nav.Z && n.Index != nav.Index)
                .Select(n => n.Index).ToHashSet();
            foreach (var b in _currentMission.Briefing)
            {
                if (colocatedIndices.Contains(b.TargetNav))
                {
                    var text = b.Description.TrimStart('?', '.').Trim();
                    if (!string.IsNullOrEmpty(text) && !notes.Contains(text))
                        notes.Add(text);
                }
            }
        }
        if (notes.Count > 0)
            AddDetailRow("Notes", string.Join("; ", notes));

        if (nav.ShipIndices.Length > 0)
            AddDetailRow("Ships", string.Join(", ", nav.ShipIndices.Select(i => $"[{i:D2}]")));
        if (nav.PreloadNames.Length > 0)
            AddDetailRow("Preloads", string.Join(", ", nav.PreloadNames));

        for (int t = 0; t < nav.Triggers.Length; t++)
        {
            if (nav.Triggers[t].Length >= 2)
            {
                int targetSlot = nav.Triggers[t][1];
                string targetName = _currentMission?.NavPoints
                    .FirstOrDefault(n => n.Index == targetSlot)?.Name ?? $"slot {targetSlot}";
                string action = nav.Triggers[t][0] switch
                {
                    0 => $"Deactivates \"{targetName}\" (slot {targetSlot})",
                    1 => $"Activates \"{targetName}\" (slot {targetSlot})",
                    _ => $"Type={nav.Triggers[t][0]}, Nav={targetSlot}"
                };
                AddDetailRow($"Trigger {t + 1}", action);
            }
        }
    }

    void AddDetailRow(string label, string value)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
        sp.Children.Add(new TextBlock
        {
            Text = label + ": ",
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            Width = 90,
            FontSize = 11
        });
        sp.Children.Add(new TextBlock
        {
            Text = value,
            Foreground = Brushes.White,
            FontSize = 11
        });
        DetailPanel.Children.Add(sp);
    }

    // --- Nav Map ---

    void DrawNavMap(ViewerMission mission)
    {
        NavMapCanvas.Children.Clear();
        if (mission.NavPoints.Count == 0) return;

        NavMapCanvas.Dispatcher.InvokeAsync(() => DrawNavMapInternal(mission),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    void DrawNavMapInternal(ViewerMission mission)
    {
        NavMapCanvas.Children.Clear();
        _navElements.Clear();
        _overlappingNavs.Clear();
        _overlapCycleIndex.Clear();
        _selectedNavIndex = -1;
        double cw = NavMapCanvas.ActualWidth;
        double ch = NavMapCanvas.ActualHeight;
        if (cw < 10 || ch < 10) return;

        double fontScale = Math.Clamp(Math.Min(cw, ch) / 500.0, 0.8, 2.0);

        var navs = mission.NavPoints;
        double minX = navs.Min(n => n.X), maxX = navs.Max(n => n.X);
        double minZ = navs.Min(n => n.Z), maxZ = navs.Max(n => n.Z);

        double rangeX = Math.Max(maxX - minX, 1000);
        double rangeZ = Math.Max(maxZ - minZ, 1000);
        double pad = Math.Max(rangeX, rangeZ) * 0.15;
        minX -= pad; maxX += pad; minZ -= pad; maxZ += pad;
        rangeX = maxX - minX;
        rangeZ = maxZ - minZ;

        double scale = Math.Min((cw - 60) / rangeX, (ch - 60) / rangeZ);
        double offX = (cw - rangeX * scale) / 2;
        double offZ = (ch - rangeZ * scale) / 2;

        double ToScreenX(double x) => offX + (x - minX) * scale;
        double ToScreenZ(double z) => offZ + (maxZ - z) * scale;

        // Draw grid
        var gridBrush = new SolidColorBrush(Color.FromArgb(30, 100, 100, 255));
        for (int i = 0; i <= 4; i++)
        {
            double gx = i * cw / 4;
            NavMapCanvas.Children.Add(new Line { X1 = gx, Y1 = 0, X2 = gx, Y2 = ch, Stroke = gridBrush });
            double gz = i * ch / 4;
            NavMapCanvas.Children.Add(new Line { X1 = 0, Y1 = gz, X2 = cw, Y2 = gz, Stroke = gridBrush });
        }

        // Draw route lines connecting sequential navs
        for (int i = 0; i < navs.Count - 1; i++)
        {
            var line = new Line
            {
                X1 = ToScreenX(navs[i].X), Y1 = ToScreenZ(navs[i].Z),
                X2 = ToScreenX(navs[i + 1].X), Y2 = ToScreenZ(navs[i + 1].Z),
                Stroke = new SolidColorBrush(Color.FromArgb(100, 0, 200, 255)),
                StrokeThickness = 1.5,
                StrokeDashArray = [4, 3]
            };
            NavMapCanvas.Children.Add(line);
        }

        // Draw ship positions as small dots (and asteroid/mine rings)
        foreach (var ship in mission.Ships)
        {
            if (ship.IsCarrier) continue;
            bool isField = ship.IsAsteroidField || ship.IsMineField;
            if (ship.X == 0 && ship.Y == 0 && ship.Z == 0 && !isField) continue;

            var parentNav = mission.NavPoints.FirstOrDefault(n => n.ShipIndices.Contains(ship.Index));
            double baseX = parentNav?.X ?? 0;
            double baseZ = parentNav?.Z ?? 0;
            double cx = ToScreenX(baseX + ship.X);
            double cz = ToScreenZ(baseZ + ship.Z);

            if (ship.IsAsteroidField || ship.IsMineField)
            {
                double radius = Math.Clamp(ship.Size * scale * 0.10, 12, 120);
                var ring = new Ellipse
                {
                    Width = radius * 2, Height = radius * 2,
                    Fill = Brushes.Transparent,
                    Stroke = ship.IsMineField
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44))
                        : new SolidColorBrush(Color.FromRgb(0xCC, 0x99, 0x44)),
                    StrokeThickness = 1.2,
                    StrokeDashArray = [3, 2],
                    Opacity = 0.7,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(ring, cx - radius);
                Canvas.SetTop(ring, cz - radius);
                NavMapCanvas.Children.Add(ring);

                var fieldLabel = new TextBlock
                {
                    Text = ship.IsMineField ? "Mines" : "Asteroids",
                    Foreground = ship.IsMineField
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44))
                        : new SolidColorBrush(Color.FromRgb(0xCC, 0x99, 0x44)),
                    FontSize = 9 * fontScale,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(fieldLabel, cx - 20 * fontScale);
                Canvas.SetTop(fieldLabel, cz + radius + 2);
                NavMapCanvas.Children.Add(fieldLabel);
                continue;
            }

            var dot = new Ellipse
            {
                Width = 5, Height = 5,
                Fill = ship.Allegiance switch
                {
                    Allegiance.Confed => Brushes.DodgerBlue,
                    Allegiance.Kilrathi => Brushes.OrangeRed,
                    _ => Brushes.Gray
                },
                Opacity = 0.6
            };

            Canvas.SetLeft(dot, cx - 2.5);
            Canvas.SetTop(dot, cz - 2.5);
            NavMapCanvas.Children.Add(dot);
        }

        // Draw nav points — detect overlapping positions
        var positionCount = new Dictionary<(int, int), int>();
        foreach (var n in navs)
        {
            var key = (n.X, n.Z);
            positionCount[key] = positionCount.GetValueOrDefault(key) + 1;
            if (!_overlappingNavs.ContainsKey(key))
                _overlappingNavs[key] = [];
            _overlappingNavs[key].Add(n);
        }
        var positionSeen = new Dictionary<(int, int), int>();
        var markerDrawn = new HashSet<(int, int)>();
        var sharedMarkers = new Dictionary<(int, int), Ellipse>();
        var sharedCoords = new Dictionary<(int, int), TextBlock>();

        // Detect carrier nav points (has a carrier ship assigned)
        var carrierNavIndices = new HashSet<int>(
            mission.Ships.Where(s => s.IsCarrier)
                .SelectMany(s => mission.NavPoints.Where(n => n.ShipIndices.Contains(s.Index)).Select(n => n.Index))
        );

        for (int i = 0; i < navs.Count; i++)
        {
            var nav = navs[i];
            double sx = ToScreenX(nav.X);
            double sz = ToScreenZ(nav.Z);
            var posKey = (nav.X, nav.Z);
            int overlapIndex = positionSeen.GetValueOrDefault(posKey);
            positionSeen[posKey] = overlapIndex + 1;
            bool isOverlap = positionCount[posKey] > 1;

            bool isCarrierNav = carrierNavIndices.Contains(nav.Index);
            bool isHidden = nav.NavType == 0 || nav.NavType >= 2;
            bool isEncounter = isHidden || nav.Name.StartsWith(".");
            double size = (isCarrierNav ? 14 : isEncounter ? 7 : 10) * fontScale;
            double opacity = isHidden ? 0.45 : 1.0;
            var color = isCarrierNav
                ? Color.FromRgb(0x00, 0xFF, 0x88)
                : isEncounter
                    ? Color.FromRgb(0xFF, 0xAA, 0x33)
                    : Color.FromRgb(0x00, 0xCC, 0xFF);

            Ellipse marker;
            if (!isOverlap || !markerDrawn.Contains(posKey))
            {
                marker = new Ellipse
                {
                    Width = size, Height = size,
                    Fill = new SolidColorBrush(Color.FromArgb(60, color.R, color.G, color.B)),
                    Stroke = new SolidColorBrush(color),
                    StrokeThickness = 1.5,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = nav,
                    Opacity = opacity
                };
                var capturedKey = posKey;
                marker.MouseLeftButtonDown += (s, e) =>
                {
                    var navsAtPos = _overlappingNavs[capturedKey];
                    if (navsAtPos.Count <= 1)
                    {
                        ShowNavDetail(navsAtPos[0]);
                    }
                    else
                    {
                        int idx = _overlapCycleIndex.GetValueOrDefault(capturedKey);
                        ShowNavDetail(navsAtPos[idx]);
                        _overlapCycleIndex[capturedKey] = (idx + 1) % navsAtPos.Count;
                    }
                    e.Handled = true;
                };
                Canvas.SetLeft(marker, sx - size / 2);
                Canvas.SetTop(marker, sz - size / 2);
                NavMapCanvas.Children.Add(marker);
                markerDrawn.Add(posKey);
                sharedMarkers[posKey] = marker;
            }
            else
            {
                marker = sharedMarkers[posKey];
            }

            double labelX = sx + size / 2 + 3;
            double labelY = sz - 7;
            double labelStep = 14 * fontScale;
            if (isOverlap)
            {
                labelX = sx + size / 2 + 3;
                labelY = sz - 7 + overlapIndex * labelStep;
            }

            var label = new TextBlock
            {
                Text = nav.Name,
                Foreground = new SolidColorBrush(color),
                FontSize = 10 * fontScale,
                Opacity = opacity,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            label.MouseLeftButtonDown += (s, e) => { ShowNavDetail(nav); e.Handled = true; };
            Canvas.SetLeft(label, labelX);
            Canvas.SetTop(label, labelY);
            NavMapCanvas.Children.Add(label);

            TextBlock coordLabel;
            bool isLastInGroup = isOverlap && overlapIndex == positionCount[posKey] - 1;
            if (!isOverlap)
            {
                coordLabel = new TextBlock
                {
                    Text = $"({nav.X / 1000}k, {nav.Z / 1000}k)",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88)),
                    FontSize = 9 * fontScale,
                    IsHitTestVisible = false,
                    Opacity = opacity
                };
                Canvas.SetLeft(coordLabel, labelX);
                Canvas.SetTop(coordLabel, labelY + labelStep);
                NavMapCanvas.Children.Add(coordLabel);
            }
            else if (isLastInGroup)
            {
                coordLabel = new TextBlock
                {
                    Text = $"({nav.X / 1000}k, {nav.Z / 1000}k)",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88)),
                    FontSize = 9 * fontScale,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(coordLabel, labelX);
                Canvas.SetTop(coordLabel, labelY + labelStep);
                NavMapCanvas.Children.Add(coordLabel);
                sharedCoords[posKey] = coordLabel;
            }
            else
            {
                coordLabel = null!;
            }

            _navElements[nav.Index] = (label, coordLabel!, marker, color);
        }

        // Fix up shared coord references for non-last overlap navs
        foreach (var nav in navs)
        {
            var posKey = (nav.X, nav.Z);
            if (sharedCoords.TryGetValue(posKey, out var shared) && _navElements.TryGetValue(nav.Index, out var elem) && elem.coord == null)
            {
                _navElements[nav.Index] = (elem.label, shared, elem.marker, elem.origColor);
            }
        }
    }

    void NavMap_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Clicking empty space clears detail
    }

    // --- Briefing ---

    void LoadBriefing(ViewerMission mission)
    {
        BriefingPanel.Children.Clear();
        if (mission.Briefing.Count == 0)
        {
            BriefingPanel.Children.Add(new TextBlock
            {
                Text = "No briefing data for this mission.",
                Foreground = Brushes.Gray,
                FontStyle = FontStyles.Italic,
                FontSize = 11
            });
            return;
        }

        foreach (var item in mission.Briefing)
        {
            bool isDevNote = item.Description.StartsWith(".");
            var text = item.Description.TrimStart('?', '.').Trim();
            if (string.IsNullOrEmpty(text)) continue;
            BriefingPanel.Children.Add(new TextBlock
            {
                Text = isDevNote ? $"  {text}" : $"• {text}",
                Foreground = isDevNote ? new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88)) : Brushes.White,
                FontStyle = isDevNote ? FontStyles.Italic : FontStyles.Normal,
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 2),
                TextWrapping = TextWrapping.Wrap
            });
        }
    }
}

// --- View Model for Ship List ---

class ShipViewModel
{
    public ViewerShip Ship { get; }

    public ShipViewModel(ViewerShip ship) => Ship = ship;

    public string IndexLabel => $"{Ship.Index:D2}";
    public string ClassName => Ship.ClassName;
    public string PilotName => Ship.PilotName;
    public string OrdersName => Ship.OrdersName;

    public Brush FactionColor => Ship.Allegiance switch
    {
        Allegiance.Confed => new SolidColorBrush(Color.FromRgb(0x40, 0xA0, 0xFF)),
        Allegiance.Kilrathi => new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)),
        _ => new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99))
    };
}
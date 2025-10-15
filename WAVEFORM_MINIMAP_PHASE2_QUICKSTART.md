# Waveform MiniMap Phase 2 - Quick Start Integration

## ? 5-Minute Integration Guide

This guide will get Phase 2 features working in your application in just 5 minutes.

---

## Step 1: Add Properties to ViewModel (1 minute)

```csharp
// In MainViewModel.cs
public class MainViewModel : ViewModelBase
{
    // Phase 2: Activity Heatmap
    private bool _showHeatmap = false;
    public bool ShowHeatmap
    {
        get => _showHeatmap;
        set => SetProperty(ref _showHeatmap, value);
    }

    // Phase 2: Markers
    private ObservableCollection<WaveformMarker> _markers = new();
    public ObservableCollection<WaveformMarker> Markers
    {
        get => _markers;
        set => SetProperty(ref _markers, value);
    }
}
```

---

## Step 2: Update XAML Binding (1 minute)

```xaml
<!-- In MainWindow.xaml -->

<!-- Add heatmap toggle -->
<CheckBox Content="Show Activity Heatmap"
          IsChecked="{Binding ShowHeatmap}"
          Margin="5"/>

<!-- Update WaveformMiniMap -->
<controls:WaveformMiniMap
    x:Name="WaveformMiniMap"
    WaveformData="{Binding WaveformData}"
    FrequencyWaveforms="{Binding FrequencyWaveforms}"
    ZoomStartTime="{Binding ZoomStartTime}"
    ZoomEndTime="{Binding ZoomEndTime}"
    PlayheadPosition="{Binding PlayheadPosition}"
    ShowActivityHeatmap="{Binding ShowHeatmap}"
    Markers="{Binding Markers}"
    MinimapClicked="WaveformMiniMap_Clicked"
    MinimapDragged="WaveformMiniMap_Dragged"
    MarkerAdded="WaveformMiniMap_MarkerAdded"
    MarkerClicked="WaveformMiniMap_MarkerClicked"/>
```

---

## Step 3: Add Event Handlers (2 minutes)

```csharp
// In MainWindow.xaml.cs

private void WaveformMiniMap_MarkerAdded(object sender, MarkerEventArgs e)
{
    var marker = e.Marker;
    var time = TimeSpan.FromSeconds(marker.Position * _viewModel.TotalTime.TotalSeconds);
    _viewModel.StatusText = $"Marker added at {time:mm\\:ss\\.fff}";
}

private void WaveformMiniMap_MarkerClicked(object sender, MarkerEventArgs e)
{
    // Jump to marker position
    var time = TimeSpan.FromSeconds(e.Marker.Position * _viewModel.TotalTime.TotalSeconds);
    _viewModel.StatusText = $"Jumped to marker: {e.Marker.Label}";
    
    // Center viewport on marker (adjust zoom to keep current zoom level)
    var zoomRange = _viewModel.ZoomEndTime - _viewModel.ZoomStartTime;
    var newStart = Math.Clamp(e.Marker.Position - zoomRange / 2.0, 0.0, 1.0 - zoomRange);
    var newEnd = newStart + zoomRange;
    
    _viewModel.ZoomStartTime = newStart;
    _viewModel.ZoomEndTime = newEnd;
}
```

---

## Step 4: Add Navigation Buttons (Optional - 1 minute)

```xaml
<!-- In MainWindow.xaml, in your toolbar area -->

<StackPanel Orientation="Horizontal" Margin="5">
    <Button Content="? Back"
            Width="60"
            Click="ZoomBack_Click"
            IsEnabled="{Binding ElementName=WaveformMiniMap, 
                              Path=CanGoBackInHistory}"
            ToolTip="Go back in zoom history"/>
    
    <Button Content="Forward ?"
            Width="80"
            Margin="5,0,0,0"
            Click="ZoomForward_Click"
            IsEnabled="{Binding ElementName=WaveformMiniMap, 
                              Path=CanGoForwardInHistory}"
            ToolTip="Go forward in zoom history"/>
</StackPanel>
```

```csharp
// In MainWindow.xaml.cs

private void ZoomBack_Click(object sender, RoutedEventArgs e)
{
    WaveformMiniMap.GoBackInZoomHistory();
}

private void ZoomForward_Click(object sender, RoutedEventArgs e)
{
    WaveformMiniMap.GoForwardInZoomHistory();
}
```

---

## ? That's It! You're Done!

Your application now has:
- ? **Activity Heatmap**: Toggle with checkbox
- ? **Markers**: Right-click to add, left-click to jump
- ? **Zoom History**: Back/Forward navigation buttons

---

## ?? Test It Out

### Test Markers
1. **Load a recording**
2. **Right-click** on the minimap ? Marker appears
3. **Left-click** the marker ? Viewport jumps to position
4. **Right-click** the marker again ? Marker removed

### Test Heatmap
1. **Check** "Show Activity Heatmap"
2. **See** colored background (blue = low, red = high)
3. **Uncheck** to disable

### Test History
1. **Zoom in** multiple times
2. **Click "? Back"** ? Returns to previous zoom
3. **Click "Forward ?"** ? Goes forward again

---

## ?? Optional: Enhance UI

### Add Marker List
```xaml
<GroupBox Header="Markers" Margin="5">
    <ListView ItemsSource="{Binding Markers}" Height="150">
        <ListView.ItemTemplate>
            <DataTemplate>
                <StackPanel Orientation="Horizontal">
                    <Ellipse Width="10" Height="10" 
                             Fill="{Binding Color, Converter={StaticResource ColorToBrushConverter}}"
                             Margin="0,0,5,0"/>
                    <TextBlock Text="{Binding Label}" Width="100"/>
                    <TextBlock Text="{Binding Position, StringFormat=P1}"/>
                </StackPanel>
            </DataTemplate>
        </ListView.ItemTemplate>
    </ListView>
</GroupBox>
```

### Add Clear Markers Button
```xaml
<Button Content="Clear All Markers"
        Click="ClearMarkers_Click"/>
```

```csharp
private void ClearMarkers_Click(object sender, RoutedEventArgs e)
{
    _viewModel.Markers.Clear();
}
```

### Add Clear History Button
```xaml
<Button Content="Clear History"
        Click="ClearHistory_Click"/>
```

```csharp
private void ClearHistory_Click(object sender, RoutedEventArgs e)
{
    WaveformMiniMap.ClearZoomHistory();
}
```

---

## ?? Advanced: Programmatic Marker Management

### Add Marker Programmatically
```csharp
public void AddMarkerAtCurrentPosition()
{
    var marker = new WaveformMarker
    {
        Position = _viewModel.PlayheadPosition,
        Label = $"Event at {_viewModel.CurrentTime:mm\\:ss}",
        Color = Color.FromRgb(255, 152, 0), // Orange
        Notes = "Auto-generated marker"
    };
    
    _viewModel.Markers.Add(marker);
}
```

### Export Markers to CSV
```csharp
public void ExportMarkers(string filePath)
{
    using var writer = new StreamWriter(filePath);
    writer.WriteLine("Label,Position,Time,Created");
    
    foreach (var marker in _viewModel.Markers)
    {
        var time = TimeSpan.FromSeconds(
            marker.Position * _viewModel.TotalTime.TotalSeconds);
        writer.WriteLine($"\"{marker.Label}\",{marker.Position:F4}," +
                        $"{time},{marker.CreatedAt:o}");
    }
}
```

### Load Markers from CSV
```csharp
public void ImportMarkers(string filePath)
{
    if (!File.Exists(filePath)) return;
    
    _viewModel.Markers.Clear();
    
    var lines = File.ReadAllLines(filePath).Skip(1); // Skip header
    foreach (var line in lines)
    {
        var parts = line.Split(',');
        if (parts.Length >= 4)
        {
            var marker = new WaveformMarker
            {
                Label = parts[0].Trim('"'),
                Position = double.Parse(parts[1]),
                CreatedAt = DateTime.Parse(parts[3])
            };
            _viewModel.Markers.Add(marker);
        }
    }
}
```

---

## ?? Troubleshooting

### Markers Not Appearing?
- ? Check `Markers` property is bound in XAML
- ? Verify `MarkerAdded` event handler is connected
- ? Ensure `ObservableCollection<WaveformMarker>` is initialized

### Heatmap Not Showing?
- ? Check `ShowActivityHeatmap` property is bound
- ? Verify waveform data is loaded
- ? Try toggling the property to trigger redraw

### History Buttons Always Disabled?
- ? Check `IsEnabled` binding to `CanGoBackInHistory` / `CanGoForwardInHistory`
- ? Verify `x:Name="WaveformMiniMap"` is set
- ? Try zooming in to create history

### Compilation Errors?
- ? Ensure you're using the updated `WaveformMiniMap.cs` file
- ? Add `using System.Collections.ObjectModel;` if needed
- ? Check that all event signatures match

---

## ?? Next Steps

1. **Read Full Documentation**
   - `WAVEFORM_MINIMAP_PHASE2_IMPLEMENTATION.md` - Technical details
   - `WAVEFORM_MINIMAP_PHASE2_USER_GUIDE.md` - Visual guide
   - `WAVEFORM_MINIMAP_PHASE2_SUMMARY.md` - Complete summary

2. **Experiment with Features**
   - Try different marker colors
   - Analyze heatmap patterns
   - Test history navigation

3. **Customize for Your Needs**
   - Add custom marker types
   - Implement marker persistence
   - Create keyboard shortcuts

---

## ?? Tips

### Marker Tips
- **Color-code** markers by event type (red = critical, orange = normal, green = success)
- **Label clearly** with descriptive names
- **Limit quantity** to 10-20 for readability

### Heatmap Tips
- **Use for overview** - Toggle off when examining detail
- **Identify patterns** - Red areas indicate high activity
- **Combine with zoom** - Heatmap + detailed waveform = best analysis

### History Tips
- **Navigate freely** - Don't worry about wrong zooms, just go back
- **Clear on load** - Start fresh with each new recording
- **Use with markers** - Mark important views before exploring

---

## ? Checklist

- [ ] Added `ShowHeatmap` property to ViewModel
- [ ] Added `Markers` collection to ViewModel
- [ ] Updated XAML bindings
- [ ] Added event handlers
- [ ] Added navigation buttons (optional)
- [ ] Tested markers (add, click, remove)
- [ ] Tested heatmap (toggle on/off)
- [ ] Tested history (back/forward)
- [ ] Build successful
- [ ] No warnings or errors

---

## ?? Success!

You now have a fully functional Phase 2 Waveform MiniMap with:
- ?? Markers for bookmarking
- ?? Activity heatmap for analysis
- ?? Zoom history for navigation

**Happy analyzing!** ??

---

**Integration Time**: ~5 minutes  
**Difficulty**: Easy  
**Status**: ? Complete  

**Framework**: .NET 9, WPF  
**Language**: C# 13  
